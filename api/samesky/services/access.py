"""Authorization primitives shared by the API and the privileged worker.

Rule: a non-owner may read a resource only with an unrevoked explicit recipient grant whose
membership generation matches the reader's *current, active* membership in the resource's room.
Room membership alone never grants access to historical records; rejoining bumps the generation,
so revoked or pre-removal grants are never restored.
"""

from __future__ import annotations

from collections.abc import Iterable

from sqlalchemy import select
from sqlalchemy.orm import Session

from ..errors import invalid, not_found
from ..models import (
    AudienceSnapshot,
    Membership,
    Moment,
    Recap,
    RecapSource,
    RecipientGrant,
    Room,
    User,
    utcnow,
)


def active_membership(db: Session, room_id: str, user_id: str) -> Membership | None:
    return db.scalar(
        select(Membership).where(
            Membership.room_id == room_id, Membership.user_id == user_id, Membership.status == "active"
        )
    )


def require_member(db: Session, room_id: str, user_id: str) -> tuple[Room, Membership]:
    membership = active_membership(db, room_id, user_id)
    room = db.get(Room, room_id)
    if membership is None or room is None:
        raise not_found("room")
    return room, membership


def active_member_ids(db: Session, room_id: str) -> list[str]:
    return list(
        db.scalars(
            select(Membership.user_id).where(Membership.room_id == room_id, Membership.status == "active")
        )
    )


def create_snapshot(
    db: Session,
    room: Room,
    resource_type: str,
    resource_id: str,
    created_by: str,
    recipient_ids: Iterable[str],
    *,
    include_creator_in_room_wide: bool = True,
) -> AudienceSnapshot:
    """Freeze the accepted recipients (and their membership generations) at publication time."""
    recipients = sorted(set(recipient_ids))
    if not recipients:
        raise invalid("empty_audience", "Choose at least one person to share with.")
    members = {
        m.user_id: m
        for m in db.scalars(
            select(Membership).where(Membership.room_id == room.id, Membership.status == "active")
        )
    }
    for rid in recipients:
        if rid not in members:
            raise invalid("recipient_not_member", "Every recipient must be a current member of this room.")
    covered = set(recipients) | ({created_by} if include_creator_in_room_wide else set())
    snapshot = AudienceSnapshot(
        room_id=room.id,
        resource_type=resource_type,
        resource_id=resource_id,
        created_by=created_by,
        room_wide=covered >= set(members),
    )
    db.add(snapshot)
    db.flush()
    for rid in recipients:
        db.add(
            RecipientGrant(snapshot_id=snapshot.id, user_id=rid, membership_generation=members[rid].generation)
        )
    db.flush()
    return snapshot


def snapshot_grants(db: Session, snapshot_id: str) -> list[RecipientGrant]:
    return list(db.scalars(select(RecipientGrant).where(RecipientGrant.snapshot_id == snapshot_id)))


def can_view_snapshot(db: Session, snapshot_id: str | None, user_id: str) -> bool:
    if snapshot_id is None:
        return False
    snapshot = db.get(AudienceSnapshot, snapshot_id)
    if snapshot is None:
        return False
    grant = db.scalar(
        select(RecipientGrant).where(
            RecipientGrant.snapshot_id == snapshot_id,
            RecipientGrant.user_id == user_id,
            RecipientGrant.revoked_at.is_(None),
        )
    )
    if grant is None:
        return False
    membership = active_membership(db, snapshot.room_id, user_id)
    return membership is not None and membership.generation == grant.membership_generation


def current_valid_recipients(db: Session, snapshot_id: str) -> set[str]:
    """Recipients whose grant is still usable right now (unrevoked + matching active generation)."""
    snapshot = db.get(AudienceSnapshot, snapshot_id)
    if snapshot is None:
        return set()
    result = set()
    for grant in snapshot_grants(db, snapshot_id):
        if grant.revoked_at is not None:
            continue
        m = active_membership(db, snapshot.room_id, grant.user_id)
        if m is not None and m.generation == grant.membership_generation:
            result.add(grant.user_id)
    return result


def revoke_snapshot(db: Session, snapshot_id: str | None) -> None:
    if snapshot_id is None:
        return
    now = utcnow()
    for grant in snapshot_grants(db, snapshot_id):
        if grant.revoked_at is None:
            grant.revoked_at = now


def can_view_moment(db: Session, moment: Moment, user_id: str) -> bool:
    if moment.sharing_state == "deleted":
        return False
    if moment.owner_id == user_id:
        # Owner review access is separate from recipient grants, but still requires being in the room.
        return active_membership(db, moment.room_id, user_id) is not None
    if moment.sharing_state != "shared":
        return False
    return can_view_snapshot(db, moment.audience_snapshot_id, user_id)


def get_visible_moment(db: Session, moment_id: str, user_id: str) -> Moment:
    moment = db.get(Moment, moment_id)
    if moment is None or not can_view_moment(db, moment, user_id):
        raise not_found("moment")
    return moment


def recap_contributor_ids(db: Session, recap_id: str) -> set[str]:
    return set(db.scalars(select(RecapSource.contributor_id).where(RecapSource.recap_id == recap_id)))


def can_view_recap(db: Session, recap: Recap, user_id: str) -> bool:
    if recap.state == "shared":
        return can_view_snapshot(db, recap.audience_snapshot_id, user_id)
    if recap.state in ("private_preview", "awaiting_approval"):
        # Private preview: only included contributors who are still in the audience review it.
        return user_id in recap_contributor_ids(db, recap.id) and can_view_snapshot(
            db, recap.audience_snapshot_id, user_id
        )
    return False


def can_see_recap_status(db: Session, recap: Recap, user_id: str) -> bool:
    """Job status (never content) is visible to the requester and contributors."""
    if active_membership(db, recap.room_id, user_id) is None:
        return False
    return recap.requested_by == user_id or user_id in recap_contributor_ids(db, recap.id)


def display_names(db: Session, user_ids: Iterable[str]) -> dict[str, str]:
    ids = list(set(user_ids))
    if not ids:
        return {}
    return {u.id: u.display_name for u in db.scalars(select(User).where(User.id.in_(ids)))}
