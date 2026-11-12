"""Response shaping. Every serializer takes the *viewer* and only emits what that viewer may see."""

from __future__ import annotations

from pathlib import Path
from zoneinfo import ZoneInfo

from sqlalchemy import select
from sqlalchemy.orm import Session

from .config import get_settings
from .models import (
    Avatar,
    AvatarAsset,
    MediaAsset,
    Moment,
    Reaction,
    Recap,
    RecapApproval,
    Reply,
    Room,
    RoomObject,
    User,
)
from .security import sign_media_url, sign_resource_url
from .services.access import (
    can_view_moment,
    can_view_recap,
    current_valid_recipients,
    display_names,
    recap_contributor_ids,
)
from .services.media import describe as describe_media


def media_url(asset_id: str, variant: str, viewer_id: str) -> str:
    return (get_settings().public_api_url or "").rstrip("/") + sign_media_url(asset_id, variant, viewer_id)


RIG_FIELDS = ("bones", "blink", "expressions", "morphTargets", "triangles", "joints", "textures", "maxTexturePx")


def avatar_url(asset_id: str, variant: str, viewer_id: str) -> str:
    return (get_settings().public_api_url or "").rstrip("/") + sign_resource_url("/v1/avatar-assets", asset_id, variant, viewer_id)


def avatar_view(db: Session | None, owner_id: str, avatar: Avatar | None, viewer_id: str | None) -> dict | None:
    """What this viewer sees of a person's avatar: the owner-approved photo-derived model when they may see it,
    otherwise the clearly labelled demo preset. Model URLs are short-lived, viewer-bound and re-authorized on fetch."""
    if avatar is None:
        return None
    data: dict = {"kind": "preset_demo", "definition": avatar.definition, "version": avatar.version}
    if db is None or not avatar.active_asset_id or not viewer_id:
        return data
    from .services.avatars import can_view_asset

    asset = db.get(AvatarAsset, avatar.active_asset_id)
    if asset is None or asset.state != "active" or not can_view_asset(db, asset, viewer_id):
        return data
    data.update(
        {
            "kind": "photo_derived",
            "assetId": asset.id,
            "assetVersion": asset.version,
            "modelUrl": avatar_url(asset.id, "model", viewer_id),
            "rig": {k: (asset.rig or {}).get(k) for k in RIG_FIELDS},
            "provider": asset.provider,
            "approvedAt": asset.approved_at.isoformat() if asset.approved_at else None,
        }
    )
    if viewer_id == owner_id:
        data["visibility"] = asset.visibility
    return data


def user_public(user: User, avatar: Avatar | None = None, db: Session | None = None, viewer_id: str | None = None) -> dict:
    return {
        "id": user.id,
        "displayName": user.display_name,
        "timeZone": user.time_zone,
        "avatar": avatar_view(db, user.id, avatar, viewer_id),
    }


def me(user: User, avatar: Avatar | None, db: Session | None = None) -> dict:
    return {
        **user_public(user, avatar, db, user.id),
        "email": user.email,
        "quietStart": user.quiet_start,
        "quietEnd": user.quiet_end,
        "preferredCallMinutes": user.preferred_call_minutes,
        "shareAvailabilityHints": user.share_availability_hints,
        "notificationPrefs": user.notification_prefs or {},
        "callLink": user.call_link,
        "suggestionsPausedUntil": user.suggestions_paused_until.isoformat() if user.suggestions_paused_until else None,
        "likenessConsent": avatar.likeness_consent if avatar else False,
    }


def room(db: Session, r: Room, viewer: User, members: list[tuple] | None = None) -> dict:
    from .services.rooms import room_members

    members = members if members is not None else room_members(db, r.id)
    avatars = {a.user_id: a for a in db.scalars(select(Avatar).where(Avatar.user_id.in_([u.id for _, u in members])))}
    viewer_membership = next((m for m, u in members if u.id == viewer.id), None)
    return {
        "id": r.id,
        "kind": r.kind,
        "name": r.name,
        "capacity": r.capacity,
        "reportingZone": r.reporting_zone,
        "dailyDraftTime": r.daily_draft_time,
        "isOwner": r.owner_id == viewer.id,
        "muted": bool(viewer_membership and viewer_membership.muted),
        "members": [
            {**user_public(u, avatars.get(u.id), db, viewer.id), "role": m.role, "label": m.label, "joinedAt": m.joined_at.isoformat()}
            for m, u in members
        ],
    }


def moment(db: Session, m: Moment, viewer: User) -> dict:
    assert can_view_moment(db, m, viewer.id)
    asset = db.get(MediaAsset, m.media_asset_id) if m.media_asset_id else None
    owner = db.get(User, m.owner_id)
    recipients = sorted(current_valid_recipients(db, m.audience_snapshot_id)) if m.audience_snapshot_id else []
    names = display_names(db, recipients)
    local = m.capture_at.astimezone(ZoneInfo(m.capture_zone))
    reactions = db.scalars(select(Reaction).where(Reaction.moment_id == m.id)).all()
    data = {
        "id": m.id,
        "roomId": m.room_id,
        "ownerId": m.owner_id,
        "ownerName": owner.display_name if owner else "Someone",
        "kind": m.kind,
        "caption": m.caption,
        "captureAt": m.capture_at.isoformat(),
        "captureLocal": {"time": local.strftime("%H:%M"), "date": local.date().isoformat(), "zone": m.capture_zone, "abbreviation": local.tzname()},
        "roomDate": m.room_date.isoformat(),
        "sharingState": m.sharing_state if m.owner_id == viewer.id else "shared",
        "sourceVersion": m.source_version,
        "sharingVersion": m.sharing_version,
        "audience": [{"userId": r, "displayName": names.get(r, "Member")} for r in recipients],
        "isMine": m.owner_id == viewer.id,
        "recordedGesture": m.recorded_gesture,
        "reactions": [{"userId": r.user_id, "kind": r.kind} for r in reactions],
        "media": None,
    }
    if m.owner_id == viewer.id:
        data["aiAllowed"] = m.ai_allowed
        data["transcriptConsent"] = m.transcript_consent
    if m.transcript_consent and m.transcript:
        data["transcript"] = m.transcript
    if asset is not None:
        media = describe_media(asset)
        if asset.processing_state == "ready":
            media["url"] = media_url(asset.id, "display", viewer.id)
            if asset.thumbnail_path:
                media["thumbUrl"] = media_url(asset.id, "thumb", viewer.id)
            if asset.kind == "audio" and asset.derivative_path and Path(asset.derivative_path).with_suffix(".ogg").exists():
                media["oggUrl"] = media_url(asset.id, "ogg", viewer.id)  # for native clients without AAC decoding
        if m.owner_id != viewer.id:
            media.pop("inputProfile", None)
            media.pop("safeError", None)
        data["media"] = media
    return data


def reply(db: Session, r: Reply) -> dict:
    author = db.get(User, r.author_id)
    return {
        "id": r.id,
        "momentId": r.moment_id,
        "authorId": r.author_id,
        "authorName": author.display_name if author else "Someone",
        "text": r.text,
        "createdAt": r.created_at.isoformat(),
        "label": "Recorded message",
    }


def room_object(o: RoomObject) -> dict:
    return {
        "id": o.id,
        "objectType": o.object_type,
        "momentId": o.moment_id,
        "replyId": o.reply_id,
        "ownerId": o.owner_id,
        "transform": {"x": o.x, "y": o.y, "z": o.z, "yaw": o.yaw},
        "version": o.version,
        "updatedAt": o.updated_at.isoformat(),
    }


def recap(db: Session, r: Recap, viewer: User) -> dict:
    contributors = recap_contributor_ids(db, r.id)
    approvals = {a.contributor_id for a in db.scalars(select(RecapApproval).where(RecapApproval.recap_id == r.id))}
    names = display_names(db, contributors)
    data = {
        "id": r.id,
        "roomId": r.room_id,
        "roomDate": r.room_date.isoformat(),
        "version": r.version,
        "state": r.state,
        "generator": r.generator,
        "generatorLabel": {"ai": "AI-drafted from shared moments", "chronological": "Chronological recap (no AI)"}.get(r.generator, "Pending"),
        "omittedCount": r.omitted_count,
        "invalidationReason": r.invalidation_reason,
        "contributors": [{"userId": c, "displayName": names.get(c, "Member"), "approved": c in approvals} for c in sorted(contributors)],
        "publishedAt": r.published_at.isoformat() if r.published_at else None,
        "plan": None,
        "playback": [],
    }
    if r.plan is not None and can_view_recap(db, r, viewer.id):
        data["plan"] = r.plan
        moment_ids = [mid for ch in r.plan.get("chapters", []) for mid in ch.get("playbackMomentIds", [])]
        for mid in moment_ids:
            m = db.get(Moment, mid)
            if m is not None and can_view_moment(db, m, viewer.id):
                data["playback"].append(moment(db, m, viewer))
        recipients = sorted(current_valid_recipients(db, r.audience_snapshot_id))
        audience_names = display_names(db, recipients)
        data["audience"] = [{"userId": u, "displayName": audience_names.get(u, "Member")} for u in recipients]
    return data
