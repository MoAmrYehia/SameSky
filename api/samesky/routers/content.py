"""Media imports, moments, sharing, replies, reactions, table objects, recaps and viewing sessions."""

from __future__ import annotations

from datetime import date, datetime

from fastapi import APIRouter, Depends, File, Form, UploadFile, WebSocket
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field
from sqlalchemy.orm import Session

from .. import serializers
from ..config import get_settings
from ..db import get_db
from ..deps import current_user
from ..errors import ApiError, conflict, invalid, not_found
from ..models import MediaAsset, Moment, User, ViewingSession
from ..realtime import realtime_endpoint, session_state
from ..security import verify_media_signature
from ..services import jobs, media, moments, recaps
from ..services.access import active_membership, can_view_moment, get_visible_moment, require_member

router = APIRouter(prefix="/v1")


# ---------------------------------------------------------------------------
# Media
# ---------------------------------------------------------------------------


@router.post("/media")
async def upload_media(
    file: UploadFile = File(...),
    kind: str = Form(...),
    source_device: str = Form("phone"),
    projection: str = Form("flat"),
    capture_at: str | None = Form(None),
    trim_start_ms: int | None = Form(None),
    trim_end_ms: int | None = Form(None),
    user: User = Depends(current_user),
    db: Session = Depends(get_db),
) -> dict:
    limit = get_settings().media.video_source_bytes + 1
    data = await file.read(limit)
    if len(data) >= limit:
        raise invalid("too_large", "That file is larger than any supported upload.")
    parsed_capture = None
    if capture_at:
        try:
            parsed_capture = datetime.fromisoformat(capture_at.replace("Z", "+00:00"))
        except ValueError:
            raise invalid("invalid_capture_time", "Capture time must be ISO-8601 with a zone.") from None
        if parsed_capture.tzinfo is None:
            raise invalid("invalid_capture_time", "Capture time must include a UTC offset.")
    asset = media.store_upload(
        db, user, data, expected_kind=kind, source_device=source_device, projection=projection,
        capture_at=parsed_capture, trim_start_ms=trim_start_ms, trim_end_ms=trim_end_ms,
    )
    jobs.enqueue(db, "media.process", f"media:{asset.id}:1", {"asset_id": asset.id}, max_attempts=2)
    db.commit()
    return media.describe(asset)


def _own_asset(db: Session, asset_id: str, user: User) -> MediaAsset:
    asset = db.get(MediaAsset, asset_id)
    if asset is None or asset.owner_id != user.id:
        raise not_found("media")
    return asset


@router.get("/media/{asset_id}")
def media_status(asset_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = _own_asset(db, asset_id, user)
    data = media.describe(asset)
    if asset.processing_state == "ready":
        data["url"] = serializers.media_url(asset.id, "display", user.id)
        if asset.thumbnail_path:
            data["thumbUrl"] = serializers.media_url(asset.id, "thumb", user.id)
    return data


class MediaCorrection(BaseModel):
    rotation: int | None = Field(default=None, description="0, 90, 180 or 270 degrees clockwise")
    capture_at: datetime | None = None
    trim_start_ms: int | None = Field(default=None, ge=0)
    trim_end_ms: int | None = Field(default=None, ge=0)


@router.post("/media/{asset_id}/corrections")
def correct_media(asset_id: str, body: MediaCorrection, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = _own_asset(db, asset_id, user)
    reprocess = False
    if body.rotation is not None:
        if body.rotation not in (0, 90, 180, 270):
            raise invalid("invalid_rotation", "Rotation is 0, 90, 180 or 270.")
        asset.rotation_override, reprocess = body.rotation, True
    if body.trim_start_ms is not None or body.trim_end_ms is not None:
        asset.trim_start_ms, asset.trim_end_ms, reprocess = body.trim_start_ms, body.trim_end_ms, True
    if body.capture_at is not None:
        if body.capture_at.tzinfo is None:
            raise invalid("invalid_capture_time", "Capture time must include a UTC offset.")
        asset.capture_at, asset.capture_at_source = body.capture_at, "user"
    if reprocess:
        if asset.original_path is None:
            raise conflict("original_missing", "The original file is gone; upload it again.")
        asset.processing_state = "pending"
        jobs.enqueue(db, "media.process", f"media:{asset.id}:{datetime.now().timestamp()}", {"asset_id": asset.id}, max_attempts=2)
    db.commit()
    return media.describe(asset)


@router.get("/media/{asset_id}/{variant}")
def serve_media(asset_id: str, variant: str, u: str, e: int, s: str, db: Session = Depends(get_db)):
    """Short-lived signed URL *and* a fresh authorization check: withdrawal blocks future reads immediately."""
    if not verify_media_signature(asset_id, variant, u, e, s):
        raise ApiError(403, "link_expired", "This media link has expired. Refresh to get a new one.")
    asset = db.get(MediaAsset, asset_id)
    if asset is None:
        raise not_found("media")
    allowed = asset.owner_id == u
    if not allowed:
        for m in db.query(Moment).filter(Moment.media_asset_id == asset_id):
            if can_view_moment(db, m, u):
                allowed = True
                break
    if not allowed:
        raise not_found("media")
    resolved = media.variant_path(asset, variant)
    if resolved is None or not resolved[0].exists():
        raise not_found("media")
    path, mime = resolved
    return FileResponse(path, media_type=mime, headers={"Cache-Control": "private, max-age=300"})


# ---------------------------------------------------------------------------
# Moments
# ---------------------------------------------------------------------------


class MomentIn(BaseModel):
    kind: str
    caption: str = Field(default="", max_length=500)
    media_asset_id: str | None = None
    capture_at: datetime | None = None
    capture_zone: str | None = None
    ai_allowed: bool = False
    transcript: str | None = Field(default=None, max_length=2000)
    transcript_consent: bool = False
    recorded_gesture: str | None = None


@router.post("/rooms/{room_id}/moments")
def create_moment(room_id: str, body: MomentIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    m = moments.create_moment(
        db, user, room_id, kind=body.kind, caption=body.caption, media_asset_id=body.media_asset_id,
        capture_at=body.capture_at, capture_zone=body.capture_zone, ai_allowed=body.ai_allowed,
        transcript=body.transcript, transcript_consent=body.transcript_consent, recorded_gesture=body.recorded_gesture,
    )
    return serializers.moment(db, m, user)


@router.get("/rooms/{room_id}/moments")
def list_moments(room_id: str, room_date: date | None = None, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.moment(db, m, user) for m in moments.list_moments(db, user, room_id, room_date)]


@router.get("/moments/{moment_id}")
def get_moment(moment_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.moment(db, get_visible_moment(db, moment_id, user.id), user)


class MomentPatch(BaseModel):
    caption: str | None = Field(default=None, max_length=500)
    ai_allowed: bool | None = None
    capture_at: datetime | None = None


@router.patch("/moments/{moment_id}")
def patch_moment(moment_id: str, body: MomentPatch, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.moment(db, moments.edit_moment(db, user, moment_id, caption=body.caption, ai_allowed=body.ai_allowed, capture_at=body.capture_at), user)


class AudienceIn(BaseModel):
    recipient_ids: list[str] = Field(min_length=1, max_length=5)


class ShareIn(AudienceIn):
    confirmation_token: str


@router.post("/moments/{moment_id}/audience/resolve")
def resolve_audience(moment_id: str, body: AudienceIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return moments.resolve_audience(db, user, moment_id, body.recipient_ids)


@router.post("/moments/{moment_id}/share")
def share(moment_id: str, body: ShareIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.moment(db, moments.share_moment(db, user, moment_id, body.recipient_ids, body.confirmation_token), user)


class WithdrawIn(BaseModel):
    all_copies: bool = False


@router.post("/moments/{moment_id}/withdraw")
def withdraw(moment_id: str, body: WithdrawIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return {"withdrawn": moments.withdraw_moment(db, user, moment_id, body.all_copies)}


@router.delete("/moments/{moment_id}")
def delete(moment_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    moments.delete_moment(db, user, moment_id)
    return {"ok": True}


class RepublishIn(BaseModel):
    target_room_id: str


@router.post("/moments/{moment_id}/republish")
def republish(moment_id: str, body: RepublishIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.moment(db, moments.republish(db, user, moment_id, body.target_room_id), user)


class ReplyIn(BaseModel):
    text: str = Field(min_length=1, max_length=1000)
    client_key: str | None = Field(default=None, max_length=64)


@router.get("/moments/{moment_id}/replies")
def list_replies(moment_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.reply(db, r) for r in moments.list_replies(db, user, moment_id)]


@router.post("/moments/{moment_id}/replies")
def create_reply(moment_id: str, body: ReplyIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.reply(db, moments.create_reply(db, user, moment_id, body.text, body.client_key))


class ReactionIn(BaseModel):
    kind: str


@router.post("/moments/{moment_id}/reactions")
def react(moment_id: str, body: ReactionIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    r = moments.react(db, user, moment_id, body.kind)
    return {"id": r.id, "kind": r.kind}


class ObjectIn(BaseModel):
    object_type: str
    moment_id: str | None = None
    reply_id: str | None = None
    x: float = 0
    y: float = 0
    z: float = 0
    yaw: float = 0


class ObjectMove(BaseModel):
    expected_version: int
    x: float
    y: float
    z: float
    yaw: float = 0


@router.get("/rooms/{room_id}/objects")
def list_objects(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.room_object(o) for o in moments.list_objects(db, user, room_id)]


@router.post("/rooms/{room_id}/objects")
def place_object(room_id: str, body: ObjectIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.room_object(moments.place_object(db, user, room_id, **body.model_dump()))


@router.patch("/objects/{object_id}")
def move_object(object_id: str, body: ObjectMove, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.room_object(moments.move_object(db, user, object_id, **body.model_dump()))


# ---------------------------------------------------------------------------
# Recaps
# ---------------------------------------------------------------------------


class RecapIn(BaseModel):
    room_date: date
    recipient_ids: list[str] | None = None


@router.post("/rooms/{room_id}/recaps")
def request_recap(room_id: str, body: RecapIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.recap(db, recaps.request_recap(db, user, room_id, body.room_date, body.recipient_ids), user)


@router.get("/rooms/{room_id}/recaps")
def recap_archive(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.recap(db, r, user) for r in recaps.archive(db, user, room_id)]


@router.get("/rooms/{room_id}/recaps/pending")
def recap_pending(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.recap(db, r, user) for r in recaps.pending_reviews(db, user, room_id)]


@router.get("/recaps/{recap_id}")
def get_recap(recap_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.recap(db, recaps.get_recap(db, user, recap_id), user)


@router.post("/recaps/{recap_id}/approve")
def approve_recap(recap_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.recap(db, recaps.approve(db, user, recap_id), user)


# ---------------------------------------------------------------------------
# Live viewing sessions
# ---------------------------------------------------------------------------


class SessionIn(BaseModel):
    moment_id: str
    participant_ids: list[str] = Field(min_length=1, max_length=1)


@router.post("/rooms/{room_id}/sessions")
def create_session(room_id: str, body: SessionIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    require_member(db, room_id, user.id)
    participants = sorted(set(body.participant_ids) | {user.id})
    if len(participants) != 2:
        raise invalid("two_people", "Live viewing is for two people in this release.")
    moment = get_visible_moment(db, body.moment_id, user.id)
    if moment.room_id != room_id or moment.sharing_state != "shared":
        raise not_found("moment")
    for pid in participants:
        if active_membership(db, room_id, pid) is None or not can_view_moment(db, moment, pid):
            # Never reveal *why*: a member who can't see the source simply can't be invited to watch it.
            raise invalid("participant_cannot_view", "That person can't view this moment, so you can't watch it together.")
    session = ViewingSession(room_id=room_id, moment_id=moment.id, host_id=user.id, participant_ids=participants)
    db.add(session)
    db.commit()
    return {"id": session.id, "participantIds": participants, "state": session_state(session)}


@router.get("/sessions/{session_id}")
def get_session(session_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    from ..realtime import authorize_participant

    session = authorize_participant(db, session_id, user.id)
    if session is None:
        raise not_found("session")
    return {"id": session.id, "roomId": session.room_id, "momentId": session.moment_id, "participantIds": session.participant_ids, "state": session_state(session)}


@router.get("/rooms/{room_id}/sessions")
def list_sessions(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    require_member(db, room_id, user.id)
    from ..realtime import authorize_participant

    rows = db.query(ViewingSession).filter(ViewingSession.room_id == room_id, ViewingSession.ended_at.is_(None)).order_by(ViewingSession.created_at.desc()).limit(10)
    return [
        {"id": s.id, "momentId": s.moment_id, "participantIds": s.participant_ids, "hostId": s.host_id}
        for s in rows if authorize_participant(db, s.id, user.id)
    ]


@router.post("/sessions/{session_id}/end")
def end_session(session_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    from ..models import utcnow
    from ..realtime import authorize_participant, hub

    session = authorize_participant(db, session_id, user.id)
    if session is None:
        raise not_found("session")
    session.ended_at = utcnow()
    db.commit()
    hub.end_session(session.id, reason="ended")
    return {"ok": True}


@router.websocket("/realtime")
async def realtime(ws: WebSocket) -> None:
    await realtime_endpoint(ws)
