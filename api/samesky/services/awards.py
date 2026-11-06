"""Cooperative milestone awards, computed per room from server-verified, room-wide shared events.

Idempotency: each qualifying event has a unique key (one capsule per room date, one reply-author per
room), and each award is unique per (room, milestone). Regenerating a recap, replaying, refreshing
or retrying never creates extra progress. Clients may animate a confirmed award but cannot grant one.
"""

from __future__ import annotations

from sqlalchemy import func, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..models import AchievementEvent, Award, Recap

MILESTONES = {
    "first_shared_day": {
        "title": "First Shared Day",
        "trigger": "Publish the first room-wide capsule with contributions from at least two members.",
        "keepsake": "frame",
        "target": 1,
    },
    "conversation_continued": {
        "title": "A Conversation Continued",
        "trigger": "At least two members each reply to a room-wide shared moment.",
        "keepsake": "linked_sculptures",
        "target": 2,
    },
    "seven_days": {
        "title": "Seven Days of Us",
        "trigger": "Publish qualifying shared capsules on seven distinct room dates.",
        "keepsake": "memory_tree",
        "target": 7,
    },
}


def _record_event(db: Session, **fields) -> bool:
    try:
        with db.begin_nested():
            db.add(AchievementEvent(**fields))
            db.flush()
        return True
    except IntegrityError:
        return False  # already counted


def _grant(db: Session, room_id: str, milestone: str, source_recap_id: str | None) -> None:
    if db.scalar(select(Award.id).where(Award.room_id == room_id, Award.milestone == milestone)):
        return
    try:
        with db.begin_nested():
            db.add(Award(room_id=room_id, milestone=milestone, source_recap_id=source_recap_id))
            db.flush()
    except IntegrityError:
        pass


def record_capsule_published(db: Session, recap: Recap, contributor_ids: set[str], room_wide: bool) -> None:
    """Call inside the publication transaction."""
    if not room_wide or len(contributor_ids) < 2:
        return
    _record_event(
        db,
        room_id=recap.room_id,
        kind="capsule_published",
        event_key=f"capsule:{recap.room_id}:{recap.room_date.isoformat()}",
        room_date=recap.room_date,
        recap_id=recap.id,
    )
    days = capsule_days(db, recap.room_id)
    if days >= 1:
        _grant(db, recap.room_id, "first_shared_day", recap.id)
    if days >= 7:
        _grant(db, recap.room_id, "seven_days", recap.id)


def record_reply(db: Session, room_id: str, author_id: str, parent_room_wide: bool) -> None:
    if not parent_room_wide:
        return
    _record_event(
        db, room_id=room_id, kind="reply_posted", event_key=f"reply:{room_id}:{author_id}", user_id=author_id
    )
    if reply_authors(db, room_id) >= 2:
        _grant(db, room_id, "conversation_continued", None)


def capsule_days(db: Session, room_id: str) -> int:
    return db.scalar(
        select(func.count(func.distinct(AchievementEvent.room_date))).where(
            AchievementEvent.room_id == room_id, AchievementEvent.kind == "capsule_published"
        )
    ) or 0


def reply_authors(db: Session, room_id: str) -> int:
    return db.scalar(
        select(func.count(func.distinct(AchievementEvent.user_id))).where(
            AchievementEvent.room_id == room_id, AchievementEvent.kind == "reply_posted"
        )
    ) or 0


def detach_recap(db: Session, recap_id: str) -> None:
    """When a recap is invalidated, keep the generic keepsake but drop the source link."""
    for award in db.scalars(select(Award).where(Award.source_recap_id == recap_id)):
        award.source_recap_id = None


def room_awards(db: Session, room_id: str) -> list[dict]:
    earned = {a.milestone: a for a in db.scalars(select(Award).where(Award.room_id == room_id))}
    progress = {
        "first_shared_day": min(capsule_days(db, room_id), 1),
        "conversation_continued": min(reply_authors(db, room_id), 2),
        "seven_days": min(capsule_days(db, room_id), 7),
    }
    out = []
    for key, meta in MILESTONES.items():
        award = earned.get(key)
        source_recap = None
        if award and award.source_recap_id:
            recap = db.get(Recap, award.source_recap_id)
            if recap is not None and recap.state == "shared":
                source_recap = recap.id
        out.append(
            {
                "milestone": key,
                "title": meta["title"],
                "trigger": meta["trigger"],
                "keepsake": meta["keepsake"],
                "earned": award is not None,
                "earnedAt": award.earned_at.isoformat() if award else None,
                "progress": progress[key],
                "target": meta["target"],
                "sourceRecapId": source_recap,
            }
        )
    return out
