"""Direct rooms and private circles: creation, capacity, revocable single-use invitations, removal."""

from __future__ import annotations

from datetime import timedelta
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import conflict, forbidden, invalid, not_found
from ..models import (
    CallOpportunity,
    FreeNow,
    Invitation,
    Membership,
    Notification,
    Recap,
    RecapSource,
    RecipientGrant,
    Room,
    User,
    utcnow,
)
from ..security import hash_invite_token, new_invite_token
from . import jobs
from .access import require_member


def validate_zone(zone: str) -> str:
    try:
        ZoneInfo(zone)
    except (ZoneInfoNotFoundError, ValueError):
        raise invalid("invalid_time_zone", "Use a valid IANA time zone such as Asia/Dubai.") from None
    return zone


def create_room(db: Session, owner: User, kind: str, name: str, reporting_zone: str | None) -> Room:
    settings = get_settings()
    if kind not in ("direct", "circle"):
        raise invalid("invalid_room_kind", "A room is either 'direct' or 'circle'.")
    capacity = settings.direct_capacity if kind == "direct" else settings.circle_capacity
    room = Room(
        kind=kind,
        name=name.strip()[:80] or ("Our room" if kind == "direct" else "Our circle"),
        owner_id=owner.id,
        capacity=capacity,
        reporting_zone=validate_zone(reporting_zone or owner.time_zone or settings.default_room_zone),
    )
    db.add(room)
    db.flush()
    db.add(Membership(room_id=room.id, user_id=owner.id, role="owner", status="active", generation=1))
    db.commit()
    return room


def active_count(db: Session, room_id: str) -> int:
    return db.scalar(
        select(func.count()).select_from(Membership).where(Membership.room_id == room_id, Membership.status == "active")
    ) or 0


def issue_invite(db: Session, room_id: str, actor: User) -> tuple[Invitation, str]:
    room, membership = require_member(db, room_id, actor.id)
    if membership.role != "owner":
        raise forbidden("Only the room's creator can invite people.")
    if active_count(db, room.id) >= room.capacity:
        raise conflict("room_full", f"This room already has {room.capacity} members.")
    token, token_hash = new_invite_token()
    invite = Invitation(
        room_id=room.id,
        token_hash=token_hash,
        created_by=actor.id,
        expires_at=utcnow() + timedelta(hours=get_settings().invite_ttl_hours),
    )
    db.add(invite)
    db.commit()
    return invite, token


def revoke_invite(db: Session, room_id: str, invite_id: str, actor: User) -> None:
    _, membership = require_member(db, room_id, actor.id)
    if membership.role != "owner":
        raise forbidden("Only the room's creator can revoke invitations.")
    invite = db.get(Invitation, invite_id)
    if invite is None or invite.room_id != room_id:
        raise not_found("invitation")
    if invite.revoked_at is None and invite.redeemed_at is None:
        invite.revoked_at = utcnow()
        db.commit()


def redeem_invite(db: Session, token: str, actor: User, label: str | None = None) -> Membership:
    invite = db.scalar(select(Invitation).where(Invitation.token_hash == hash_invite_token(token)))
    if invite is None:
        raise not_found("invitation")
    now = utcnow()
    if invite.redeemed_at is not None:
        # Idempotent retry by the same person returns the membership; anyone else is refused.
        if invite.redeemed_by == actor.id:
            existing = db.scalar(
                select(Membership).where(Membership.room_id == invite.room_id, Membership.user_id == actor.id)
            )
            if existing is not None and existing.status == "active":
                return existing
        raise conflict("invite_used", "This invitation has already been used.")
    if invite.revoked_at is not None:
        raise conflict("invite_revoked", "This invitation was cancelled. Ask for a new one.")
    if invite.expires_at <= now:
        raise conflict("invite_expired", "This invitation has expired. Ask for a new one.")
    room = db.get(Room, invite.room_id)
    assert room is not None
    existing = db.scalar(select(Membership).where(Membership.room_id == room.id, Membership.user_id == actor.id))
    if existing is not None and existing.status == "active":
        raise conflict("already_member", "You're already in this room.")
    if active_count(db, room.id) >= room.capacity:
        raise conflict("room_full", f"This room already has {room.capacity} members.")
    if existing is not None:
        # Rejoin: a new generation. Grants from the old generation stay dead.
        existing.status = "active"
        existing.generation += 1
        existing.removed_at = None
        existing.joined_at = now
        existing.label = (label or existing.label or "")[:40] or None
        membership = existing
    else:
        membership = Membership(
            room_id=room.id, user_id=actor.id, role="member", status="active", generation=1,
            label=(label or "")[:40] or None,
        )
        db.add(membership)
    invite.redeemed_by = actor.id
    invite.redeemed_at = now
    db.commit()
    return membership


def remove_member(db: Session, room_id: str, actor: User, target_user_id: str) -> None:
    room, actor_membership = require_member(db, room_id, actor.id)
    leaving = target_user_id == actor.id
    if not leaving and actor_membership.role != "owner":
        raise forbidden("Only the room's creator can remove members.")
    target = db.scalar(
        select(Membership).where(
            Membership.room_id == room_id, Membership.user_id == target_user_id, Membership.status == "active"
        )
    )
    if target is None:
        raise not_found("member")
    if target.role == "owner" and not leaving:
        raise forbidden("The room's creator can't be removed.")
    target.status = "left" if leaving else "removed"
    target.removed_at = utcnow()
    on_membership_ended(db, room, target_user_id)
    db.commit()
    # Live sessions are dropped by the realtime hub, which re-checks membership on every message.
    from ..realtime import hub

    hub.kick_user_from_room(room_id, target_user_id)


def on_membership_ended(db: Session, room: Room, user_id: str) -> None:
    """Cancel everything pending that depended on this membership."""
    now = utcnow()
    # Pending notifications for this room are cancelled for the departing member.
    for n in db.scalars(
        select(Notification).where(
            Notification.room_id == room.id, Notification.recipient_id == user_id, Notification.cancelled_at.is_(None)
        )
    ):
        n.cancelled_at = now
    jobs.cancel_matching(db, "notify", f"notify:{room.id}:{user_id}:", reason="membership_changed")
    # Free-now signals and call opportunities that include them are void.
    for f in db.scalars(select(FreeNow).where(FreeNow.room_id == room.id, FreeNow.user_id == user_id)):
        db.delete(f)
    for opp in db.scalars(
        select(CallOpportunity).where(CallOpportunity.room_id == room.id, CallOpportunity.state.in_(("pending", "notified")))
    ):
        if user_id in (opp.participant_ids or []):
            opp.state = "cancelled"
    # Unpublished recaps whose audience or contributors include them must be regenerated.
    pending = db.scalars(
        select(Recap).where(
            Recap.room_id == room.id,
            Recap.state.in_(("queued", "generating", "private_preview", "awaiting_approval")),
        )
    ).all()
    for recap in pending:
        in_audience = db.scalar(
            select(RecipientGrant.id).where(
                RecipientGrant.snapshot_id == recap.audience_snapshot_id, RecipientGrant.user_id == user_id
            )
        )
        contributor = db.scalar(
            select(RecapSource.id).where(RecapSource.recap_id == recap.id, RecapSource.contributor_id == user_id)
        )
        if in_audience or contributor:
            recap.state = "invalidated"
            recap.invalidation_reason = "membership_changed"


def list_rooms(db: Session, user: User) -> list[tuple[Room, Membership]]:
    rows = db.execute(
        select(Room, Membership)
        .join(Membership, Membership.room_id == Room.id)
        .where(Membership.user_id == user.id, Membership.status == "active")
        .order_by(Room.created_at)
    ).all()
    return [(r, m) for r, m in rows]


def room_members(db: Session, room_id: str) -> list[tuple[Membership, User]]:
    rows = db.execute(
        select(Membership, User)
        .join(User, User.id == Membership.user_id)
        .where(Membership.room_id == room_id, Membership.status == "active")
        .order_by(Membership.joined_at)
    ).all()
    return [(m, u) for m, u in rows]
