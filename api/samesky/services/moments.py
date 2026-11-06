"""Moments: save privately, allow AI separately, share with an explicit confirmed audience, edit, withdraw.

Three decisions are kept apart and never inferred from an upload:
  1. saving a private moment, 2. allowing AI processing, 3. sharing with a chosen audience.
"""

from __future__ import annotations

import hashlib
import hmac
from datetime import datetime
from zoneinfo import ZoneInfo

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import conflict, forbidden, invalid, not_found
from ..models import (
    MediaAsset,
    Moment,
    Reaction,
    Recap,
    RecapSource,
    Reply,
    Room,
    RoomObject,
    User,
    ViewingSession,
    utcnow,
)
from . import awards, notifications
from .access import (
    can_view_moment,
    can_view_snapshot,
    create_snapshot,
    current_valid_recipients,
    display_names,
    get_visible_moment,
    require_member,
    revoke_snapshot,
)
from .rooms import validate_zone

KIND_TO_MEDIA = {"photo": "image", "clip": "video", "voice": "audio", "text": None}
REACTIONS = ("heart", "smile", "wow", "hug", "sun")


def room_date_for(room: Room, instant: datetime):
    return instant.astimezone(ZoneInfo(room.reporting_zone)).date()


def create_moment(
    db: Session,
    actor: User,
    room_id: str,
    *,
    kind: str,
    caption: str,
    media_asset_id: str | None,
    capture_at: datetime | None,
    capture_zone: str | None,
    ai_allowed: bool,
    transcript: str | None = None,
    transcript_consent: bool = False,
    recorded_gesture: str | None = None,
) -> Moment:
    room, _ = require_member(db, room_id, actor.id)
    if kind not in KIND_TO_MEDIA:
        raise invalid("invalid_kind", "A moment is a photo, clip, voice note or text.")
    expected_media = KIND_TO_MEDIA[kind]
    if expected_media is None:
        if media_asset_id:
            raise invalid("unexpected_media", "Text moments don't carry media.")
        if not caption.strip():
            raise invalid("empty_text", "Write a few words for a text moment.")
    else:
        asset = db.get(MediaAsset, media_asset_id) if media_asset_id else None
        if asset is None or asset.owner_id != actor.id:
            raise not_found("media")
        if asset.kind != expected_media:
            raise invalid("kind_mismatch", "That media doesn't match the moment type.")
        capture_at = capture_at or asset.capture_at
    if recorded_gesture is not None and recorded_gesture not in ("wave", "nod", "heart_hands", "point"):
        raise invalid("invalid_gesture", "Unknown recorded gesture.")
    capture_at = capture_at or utcnow()
    moment = Moment(
        owner_id=actor.id,
        room_id=room.id,
        media_asset_id=media_asset_id,
        kind=kind,
        caption=caption.strip()[:500],
        transcript=(transcript or None) if transcript_consent else None,
        transcript_consent=transcript_consent,
        capture_at=capture_at,
        capture_zone=validate_zone(capture_zone or actor.time_zone),
        room_date=room_date_for(room, capture_at),
        ai_allowed=ai_allowed,
        sharing_state="private",
        recorded_gesture=recorded_gesture,
    )
    db.add(moment)
    db.commit()
    return moment


def confirmation_token(moment: Moment, recipient_ids: list[str]) -> str:
    message = f"{moment.id}:{moment.sharing_version}:{','.join(sorted(set(recipient_ids)))}".encode()
    return hmac.new(get_settings().secret_key.encode(), message, hashlib.sha256).hexdigest()[:32]


def resolve_audience(db: Session, actor: User, moment_id: str, recipient_ids: list[str]) -> dict:
    """Step 1 of sharing: show exactly who will see it, e.g. "Only Maya" or "Family circle: Maya, Adam, Lina"."""
    moment = db.get(Moment, moment_id)
    if moment is None or moment.owner_id != actor.id:
        raise not_found("moment")
    room, _ = require_member(db, moment.room_id, actor.id)
    recipients = sorted(set(recipient_ids) - {actor.id})
    if not recipients:
        raise invalid("empty_audience", "Choose at least one person to share with.")
    for rid in recipients:
        try:
            require_member(db, room.id, rid)
        except Exception:
            raise invalid("recipient_not_member", "Every recipient must be a current member of this room.") from None
    names = display_names(db, recipients)
    ordered = [names[r] for r in recipients]
    label = f"Only {ordered[0]}" if len(ordered) == 1 else f"{room.name}: {', '.join(ordered)}"
    return {
        "recipients": [{"userId": r, "displayName": names[r]} for r in recipients],
        "label": label,
        "confirmationToken": confirmation_token(moment, recipients),
        "aiAllowed": moment.ai_allowed,
    }


def share_moment(db: Session, actor: User, moment_id: str, recipient_ids: list[str], token: str) -> Moment:
    moment = db.get(Moment, moment_id)
    if moment is None or moment.owner_id != actor.id:
        raise not_found("moment")
    room, _ = require_member(db, moment.room_id, actor.id)
    recipients = sorted(set(recipient_ids) - {actor.id})
    if not hmac.compare_digest(token or "", confirmation_token(moment, recipients)):
        raise conflict("confirmation_required", "Confirm the exact recipients before sharing.")
    if moment.sharing_state == "shared":
        raise conflict("already_shared", "This moment is already shared. To share with other people, publish a separate copy.")
    if moment.sharing_state == "deleted":
        raise not_found("moment")
    if moment.media_asset_id:
        asset = db.get(MediaAsset, moment.media_asset_id)
        if asset is None or asset.processing_state != "ready":
            raise conflict("media_not_ready", "This media is still processing (or failed). It can be shared once it's ready.")
    shared_today = db.scalar(
        select(func.count()).select_from(Moment).where(
            Moment.room_id == room.id, Moment.owner_id == actor.id, Moment.room_date == moment.room_date,
            Moment.sharing_state == "shared",
        )
    ) or 0
    if shared_today >= get_settings().max_daily_contributions:
        raise conflict("daily_limit", "You've already shared three moments for this day.")
    snapshot = create_snapshot(db, room, "moment", moment.id, actor.id, recipients)
    moment.audience_snapshot_id = snapshot.id
    moment.sharing_state = "shared"
    moment.sharing_version += 1
    moment.updated_at = utcnow()
    db.commit()
    return moment


def republish(db: Session, actor: User, moment_id: str, target_room_id: str) -> Moment:
    """Share to a different audience = a new, separately confirmed private publication of the same asset."""
    source = db.get(Moment, moment_id)
    if source is None or source.owner_id != actor.id or source.sharing_state == "deleted":
        raise not_found("moment")
    return create_moment(
        db, actor, target_room_id, kind=source.kind, caption=source.caption, media_asset_id=source.media_asset_id,
        capture_at=source.capture_at, capture_zone=source.capture_zone, ai_allowed=False,
    )


def edit_moment(db: Session, actor: User, moment_id: str, *, caption: str | None, ai_allowed: bool | None, capture_at: datetime | None = None) -> Moment:
    moment = db.get(Moment, moment_id)
    if moment is None or moment.owner_id != actor.id or moment.sharing_state == "deleted":
        raise not_found("moment")
    changed_source = False
    if caption is not None and caption.strip()[:500] != moment.caption:
        moment.caption = caption.strip()[:500]
        changed_source = True
    if capture_at is not None:
        room = db.get(Room, moment.room_id)
        assert room is not None
        moment.capture_at = capture_at
        moment.room_date = room_date_for(room, capture_at)
        changed_source = True
    ai_revoked = ai_allowed is False and moment.ai_allowed
    if ai_allowed is not None:
        moment.ai_allowed = ai_allowed
    if changed_source:
        moment.source_version += 1
    moment.updated_at = utcnow()
    if changed_source or ai_revoked:
        invalidate_recaps_for(db, moment.id, "source_changed" if changed_source else "ai_permission_withdrawn")
    db.commit()
    return moment


def withdraw_moment(db: Session, actor: User, moment_id: str, all_copies: bool = False) -> list[str]:
    moment = db.get(Moment, moment_id)
    if moment is None or moment.owner_id != actor.id or moment.sharing_state == "deleted":
        raise not_found("moment")
    targets = [moment]
    if all_copies and moment.media_asset_id:
        targets = list(
            db.scalars(
                select(Moment).where(
                    Moment.owner_id == actor.id, Moment.media_asset_id == moment.media_asset_id,
                    Moment.sharing_state.in_(("private", "shared")),
                )
            )
        )
    for m in targets:
        _withdraw_one(db, m)
    db.commit()
    return [m.id for m in targets]


def _withdraw_one(db: Session, moment: Moment) -> None:
    revoke_snapshot(db, moment.audience_snapshot_id)
    if moment.sharing_state == "shared":
        moment.sharing_state = "withdrawn"
    moment.sharing_version += 1
    moment.updated_at = utcnow()
    invalidate_recaps_for(db, moment.id, "source_withdrawn")
    # Replies to it become unreachable via the parent check; cancel their pending notifications.
    for reply in db.scalars(select(Reply).where(Reply.moment_id == moment.id)):
        notifications.cancel_for_resource(db, reply.id)
    for session in db.scalars(select(ViewingSession).where(ViewingSession.moment_id == moment.id, ViewingSession.ended_at.is_(None))):
        session.ended_at = utcnow()
        from ..realtime import hub

        hub.end_session(session.id, reason="source_withdrawn")


def delete_moment(db: Session, actor: User, moment_id: str) -> None:
    moment = db.get(Moment, moment_id)
    if moment is None or moment.owner_id != actor.id or moment.sharing_state == "deleted":
        raise not_found("moment")
    _withdraw_one(db, moment)
    moment.sharing_state = "deleted"
    moment.caption = ""
    moment.transcript = None
    asset_id = moment.media_asset_id
    moment.media_asset_id = None
    db.flush()
    if asset_id and not db.scalar(select(Moment.id).where(Moment.media_asset_id == asset_id)):
        asset = db.get(MediaAsset, asset_id)
        if asset is not None:
            from .media import delete_files

            delete_files(asset)
            asset.processing_state = "failed"
            asset.safe_error = "deleted"
            asset.derivative_path = asset.thumbnail_path = asset.original_path = None
    db.commit()


def invalidate_recaps_for(db: Session, moment_id: str, reason: str) -> None:
    recap_ids = db.scalars(select(RecapSource.recap_id).where(RecapSource.moment_id == moment_id)).all()
    for recap in db.scalars(select(Recap).where(Recap.id.in_(recap_ids))):
        if recap.state in ("invalidated", "failed"):
            continue
        recap.state = "invalidated"
        recap.invalidation_reason = reason
        recap.plan = None  # never keep withdrawn text around
        notifications.cancel_for_resource(db, recap.id)
        awards.detach_recap(db, recap.id)


def list_moments(db: Session, actor: User, room_id: str, room_date=None) -> list[Moment]:
    require_member(db, room_id, actor.id)
    query = select(Moment).where(Moment.room_id == room_id, Moment.sharing_state.in_(("private", "shared", "withdrawn")))
    if room_date is not None:
        query = query.where(Moment.room_date == room_date)
    rows = db.scalars(query.order_by(Moment.capture_at)).all()
    return [m for m in rows if can_view_moment(db, m, actor.id) and (m.owner_id == actor.id or m.sharing_state == "shared")]


# ---------------------------------------------------------------------------
# Replies, reactions, objects
# ---------------------------------------------------------------------------


def create_reply(db: Session, actor: User, moment_id: str, text: str, client_key: str | None = None) -> Reply:
    parent = get_visible_moment(db, moment_id, actor.id)
    if parent.sharing_state != "shared":
        raise conflict("not_shared", "Replies are for shared moments.")
    text = text.strip()
    if not text or len(text) > 1000:
        raise invalid("invalid_reply", "Replies are 1-1000 characters.")
    if client_key:
        existing = db.scalar(select(Reply).where(Reply.author_id == actor.id, Reply.client_key == client_key))
        if existing is not None:
            return existing  # idempotent retry
    reply = Reply(moment_id=parent.id, room_id=parent.room_id, author_id=actor.id, text=text, client_key=client_key)
    db.add(reply)
    db.flush()
    # Replies inherit the parent's audience and never widen it: notify only people who can see the parent.
    audience = current_valid_recipients(db, parent.audience_snapshot_id) | {parent.owner_id}
    for uid in audience - {actor.id}:
        notifications.queue(db, kind="reply_received", room_id=parent.room_id, recipient_id=uid, resource_type="reply", resource_id=reply.id)
    from ..models import AudienceSnapshot

    snapshot = db.get(AudienceSnapshot, parent.audience_snapshot_id)
    awards.record_reply(db, parent.room_id, actor.id, bool(snapshot and snapshot.room_wide))
    db.commit()
    return reply


def list_replies(db: Session, actor: User, moment_id: str) -> list[Reply]:
    parent = get_visible_moment(db, moment_id, actor.id)
    return list(db.scalars(select(Reply).where(Reply.moment_id == parent.id, Reply.withdrawn.is_(False)).order_by(Reply.created_at)))


def react(db: Session, actor: User, moment_id: str, kind: str) -> Reaction:
    if kind not in REACTIONS:
        raise invalid("invalid_reaction", f"Reactions: {', '.join(REACTIONS)}.")
    parent = get_visible_moment(db, moment_id, actor.id)
    existing = db.scalar(select(Reaction).where(Reaction.moment_id == parent.id, Reaction.user_id == actor.id, Reaction.kind == kind))
    if existing:
        return existing
    reaction = Reaction(moment_id=parent.id, user_id=actor.id, kind=kind)
    db.add(reaction)
    db.commit()
    return reaction


OBJECT_TYPES = ("frame", "cup", "postcard", "audioStone", "note")
TABLE_BOUNDS = {"x": (-0.55, 0.55), "y": (0.0, 0.25), "z": (-0.35, 0.35)}


def _clamp(value: float, bounds: tuple[float, float]) -> float:
    return max(bounds[0], min(bounds[1], value))


def object_visible(db: Session, obj: RoomObject, user_id: str) -> bool:
    if obj.moment_id:
        moment = db.get(Moment, obj.moment_id)
        if moment is None or not can_view_moment(db, moment, user_id):
            return False
    if obj.reply_id:
        reply = db.get(Reply, obj.reply_id)
        if reply is None or reply.withdrawn:
            return False
        parent = db.get(Moment, reply.moment_id)
        if parent is None or not can_view_moment(db, parent, user_id):
            return False
    if not obj.moment_id and not obj.reply_id:
        return obj.owner_id == user_id
    return True


def place_object(db: Session, actor: User, room_id: str, *, object_type: str, moment_id: str | None, reply_id: str | None, x: float, y: float, z: float, yaw: float) -> RoomObject:
    require_member(db, room_id, actor.id)
    if object_type not in OBJECT_TYPES:
        raise invalid("invalid_object_type", "Unknown object type.")
    if moment_id:
        moment = get_visible_moment(db, moment_id, actor.id)
        if moment.room_id != room_id:
            raise not_found("moment")
    if reply_id:
        reply = db.get(Reply, reply_id)
        if reply is None or reply.room_id != room_id:
            raise not_found("reply")
        get_visible_moment(db, reply.moment_id, actor.id)
    obj = RoomObject(
        room_id=room_id, owner_id=actor.id, object_type=object_type, moment_id=moment_id, reply_id=reply_id,
        x=_clamp(x, TABLE_BOUNDS["x"]), y=_clamp(y, TABLE_BOUNDS["y"]), z=_clamp(z, TABLE_BOUNDS["z"]), yaw=yaw % 360,
        updated_by=actor.id,
    )
    db.add(obj)
    db.commit()
    return obj


def move_object(db: Session, actor: User, object_id: str, *, expected_version: int, x: float, y: float, z: float, yaw: float) -> RoomObject:
    obj = db.get(RoomObject, object_id)
    if obj is None:
        raise not_found("object")
    require_member(db, obj.room_id, actor.id)
    if not object_visible(db, obj, actor.id):
        raise not_found("object")
    if obj.owner_id != actor.id and obj.reply_id is None and obj.moment_id is None:
        raise forbidden()
    if obj.version != expected_version:
        # Conflict policy: optimistic concurrency; the client refetches and re-applies.
        raise conflict("version_conflict", "Someone moved this at the same time. The latest position has been loaded.")
    obj.x, obj.y, obj.z = _clamp(x, TABLE_BOUNDS["x"]), _clamp(y, TABLE_BOUNDS["y"]), _clamp(z, TABLE_BOUNDS["z"])
    obj.yaw = yaw % 360
    obj.version += 1
    obj.updated_by = actor.id
    obj.updated_at = utcnow()
    db.commit()
    return obj


def list_objects(db: Session, actor: User, room_id: str) -> list[RoomObject]:
    require_member(db, room_id, actor.id)
    rows = db.scalars(select(RoomObject).where(RoomObject.room_id == room_id)).all()
    return [o for o in rows if object_visible(db, o, actor.id)]


def snapshot_visible(db: Session, snapshot_id: str | None, user_id: str) -> bool:
    return can_view_snapshot(db, snapshot_id, user_id)
