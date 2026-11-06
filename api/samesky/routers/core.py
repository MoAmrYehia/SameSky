"""Identity, profile, avatar, rooms, invitations and awards."""

from __future__ import annotations

from datetime import UTC, datetime

from fastapi import APIRouter, Depends
from pydantic import BaseModel, EmailStr, Field
from sqlalchemy import select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from .. import serializers
from ..db import get_db
from ..deps import current_user
from ..errors import ApiError, conflict, invalid
from ..models import Avatar, Membership, User
from ..security import hash_password, issue_access_token, verify_password
from ..services import awards, rooms
from ..services.access import require_member
from ..services.availability import is_allowed_call_link
from ..services.availability_engine import parse_hhmm

router = APIRouter(prefix="/v1")

AVATAR_OPTIONS = {
    "face": ["soft", "defined"],
    "skin": ["#f6d7c3", "#e8b896", "#d29a72", "#b0754d", "#8a5634", "#5e3a22"],
    "hair": ["short", "wavy", "long", "bun", "curly", "buzz", "none"],
    "hairColor": ["#2b1d14", "#4a2f1f", "#7a4a2a", "#b98a4e", "#d9c49a", "#8e8e8e", "#1d1d24"],
    "top": ["#3f6b52", "#e9e0cf", "#2f4a6b", "#8a3b3b", "#c98f3a", "#5b4a7a"],
    "accessory": ["none", "glasses", "earrings", "scarf", "cap"],
}


class SignupIn(BaseModel):
    email: EmailStr
    password: str = Field(min_length=8, max_length=200)
    display_name: str = Field(min_length=1, max_length=60)
    time_zone: str = "Asia/Dubai"


class LoginIn(BaseModel):
    email: EmailStr
    password: str


def _avatar(db: Session, user_id: str) -> Avatar | None:
    return db.get(Avatar, user_id)


@router.get("/health")
def health() -> dict:
    return {"ok": True, "time": datetime.now(UTC).isoformat()}


@router.post("/auth/signup")
def signup(body: SignupIn, db: Session = Depends(get_db)) -> dict:
    rooms.validate_zone(body.time_zone)
    user = User(
        email=body.email.lower(),
        password_hash=hash_password(body.password),
        display_name=body.display_name.strip(),
        time_zone=body.time_zone,
        notification_prefs={"push": True, "call_suggestions": True, "celebrations": True},
    )
    db.add(user)
    try:
        db.flush()
    except IntegrityError:
        db.rollback()
        raise conflict("email_taken", "An account with that email already exists. Sign in instead.") from None
    db.add(Avatar(user_id=user.id, definition={"skin": "#e8b896", "hair": "wavy", "hairColor": "#4a2f1f", "top": "#3f6b52", "accessory": "none"}))
    db.commit()
    return {"token": issue_access_token(user.id), "user": serializers.me(user, _avatar(db, user.id))}


@router.post("/auth/login")
def login(body: LoginIn, db: Session = Depends(get_db)) -> dict:
    user = db.scalar(select(User).where(User.email == body.email.lower()))
    if user is None or not verify_password(body.password, user.password_hash):
        raise ApiError(401, "bad_credentials", "That email and password don't match.")
    return {"token": issue_access_token(user.id), "user": serializers.me(user, _avatar(db, user.id))}


@router.get("/me")
def get_me(user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.me(user, _avatar(db, user.id))


class ProfilePatch(BaseModel):
    display_name: str | None = Field(default=None, min_length=1, max_length=60)
    time_zone: str | None = None
    quiet_start: str | None = None
    quiet_end: str | None = None
    preferred_call_minutes: int | None = Field(default=None, ge=15, le=180)
    share_availability_hints: bool | None = None
    call_link: str | None = None
    notification_prefs: dict | None = None


@router.patch("/me")
def patch_me(body: ProfilePatch, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    if body.display_name is not None:
        user.display_name = body.display_name.strip()
    if body.time_zone is not None:
        # Travel: a manual zone change, no GPS. Historical timestamps are UTC and stay intact.
        user.time_zone = rooms.validate_zone(body.time_zone)
    for field in ("quiet_start", "quiet_end"):
        value = getattr(body, field)
        if value is not None:
            if value == "":
                setattr(user, field, None)
            else:
                try:
                    parse_hhmm(value)
                except ValueError:
                    raise invalid("invalid_local_time", "Quiet hours use HH:MM.") from None
                setattr(user, field, value)
    if body.preferred_call_minutes is not None:
        user.preferred_call_minutes = body.preferred_call_minutes
    if body.share_availability_hints is not None:
        user.share_availability_hints = body.share_availability_hints
    if body.call_link is not None:
        if body.call_link and not is_allowed_call_link(body.call_link):
            raise invalid("invalid_call_link", "Use an https://, facetime:, tel: or whatsapp: link.")
        user.call_link = body.call_link or None
    if body.notification_prefs is not None:
        allowed = {"push", "call_suggestions", "celebrations"}
        user.notification_prefs = {**(user.notification_prefs or {}), **{k: bool(v) for k, v in body.notification_prefs.items() if k in allowed}}
    db.commit()
    return serializers.me(user, _avatar(db, user.id))


class AvatarIn(BaseModel):
    skin: str
    hair: str
    hairColor: str
    top: str
    accessory: str = "none"
    face: str = "soft"  # sculpted face preset; older saved definitions without it render as "soft"
    likeness_consent: bool = False


@router.get("/avatar-options")
def avatar_options() -> dict:
    return AVATAR_OPTIONS


@router.put("/me/avatar")
def put_avatar(body: AvatarIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    definition = body.model_dump(exclude={"likeness_consent"})
    for key, value in definition.items():
        if value not in AVATAR_OPTIONS[key]:
            raise invalid("invalid_avatar", f"Unknown {key} option.")
    avatar = _avatar(db, user.id)
    if avatar is None:
        avatar = Avatar(user_id=user.id, definition=definition, likeness_consent=body.likeness_consent)
        db.add(avatar)
    else:
        avatar.definition = definition
        avatar.version += 1
        avatar.likeness_consent = body.likeness_consent
        avatar.updated_at = datetime.now(UTC)
    db.commit()
    return {"definition": avatar.definition, "version": avatar.version}


# ---------------------------------------------------------------------------
# Rooms
# ---------------------------------------------------------------------------


class RoomIn(BaseModel):
    kind: str
    name: str = Field(default="", max_length=80)
    reporting_zone: str | None = None


class RoomPatch(BaseModel):
    daily_draft_time: str | None = None
    muted: bool | None = None
    label: str | None = Field(default=None, max_length=40)


@router.get("/rooms")
def list_rooms(user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    return [serializers.room(db, r, user) for r, _ in rooms.list_rooms(db, user)]


@router.post("/rooms")
def create_room(body: RoomIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return serializers.room(db, rooms.create_room(db, user, body.kind, body.name, body.reporting_zone), user)


@router.get("/rooms/{room_id}")
def get_room(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    room, _ = require_member(db, room_id, user.id)
    return serializers.room(db, room, user)


@router.patch("/rooms/{room_id}")
def patch_room(room_id: str, body: RoomPatch, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    room, membership = require_member(db, room_id, user.id)
    if body.daily_draft_time is not None:
        if membership.role != "owner":
            raise ApiError(403, "forbidden", "Only the room's creator can set the daily draft time.")
        if body.daily_draft_time:
            parse_hhmm(body.daily_draft_time)
        room.daily_draft_time = body.daily_draft_time or None
    if body.muted is not None:
        membership.muted = body.muted
    if body.label is not None:
        membership.label = body.label or None
    db.commit()
    return serializers.room(db, room, user)


@router.post("/rooms/{room_id}/invites")
def create_invite(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    invite, token = rooms.issue_invite(db, room_id, user)
    return {"id": invite.id, "token": token, "expiresAt": invite.expires_at.isoformat(), "path": f"/join/{token}"}


@router.delete("/rooms/{room_id}/invites/{invite_id}")
def delete_invite(room_id: str, invite_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    rooms.revoke_invite(db, room_id, invite_id, user)
    return {"ok": True}


class RedeemIn(BaseModel):
    token: str = Field(min_length=10, max_length=100)
    label: str | None = Field(default=None, max_length=40)


@router.post("/invites/redeem")
def redeem(body: RedeemIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    membership: Membership = rooms.redeem_invite(db, body.token, user, body.label)
    room, _ = require_member(db, membership.room_id, user.id)
    return serializers.room(db, room, user)


@router.delete("/rooms/{room_id}/members/{member_id}")
def remove_member(room_id: str, member_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    rooms.remove_member(db, room_id, user, user.id if member_id == "me" else member_id)
    return {"ok": True}


@router.get("/rooms/{room_id}/awards")
def list_awards(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> list[dict]:
    require_member(db, room_id, user.id)
    return awards.room_awards(db, room_id)
