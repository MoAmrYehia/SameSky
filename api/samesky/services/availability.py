"""Room-scoped availability: rules, exceptions, Free now, and deterministic call opportunities.

Evidence language is fixed by the server so clients cannot overstate it:
* declared schedules overlap  -> "Your chosen schedules overlap for N minutes."
* every selected participant has an unexpired Free now signal and is outside busy/quiet time
                               -> "You're both marked free now." / "Maya, Omar and you are all marked free now."
* presence/online alone       -> never described as availability.
"""

from __future__ import annotations

from datetime import UTC, datetime, timedelta
from urllib.parse import urlparse

from sqlalchemy import delete, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import forbidden, invalid
from ..models import AvailabilityException, AvailabilityRule, CallOpportunity, FreeNow, User, utcnow
from . import notifications
from .access import display_names, require_member
from .availability_engine import (
    ParticipantSchedule,
    WeeklyRule,
    common_overlaps,
    is_blocked_now,
    local_label,
    parse_hhmm,
    schedule_fingerprint,
)


def _validate_hhmm(value: str | None) -> str | None:
    if value is None:
        return None
    try:
        parse_hhmm(value)
    except ValueError:
        raise invalid("invalid_local_time", "Times use 24-hour HH:MM, for example 21:30.") from None
    return value


def set_rules(db: Session, user: User, room_id: str, rules: list[dict]) -> list[AvailabilityRule]:
    require_member(db, room_id, user.id)
    if len(rules) > 42:
        raise invalid("too_many_rules", "Use at most 42 weekly windows.")
    previous = db.scalars(
        select(AvailabilityRule.version).where(AvailabilityRule.user_id == user.id, AvailabilityRule.room_id == room_id)
    ).all()
    version = (max(previous) if previous else 0) + 1
    db.execute(delete(AvailabilityRule).where(AvailabilityRule.user_id == user.id, AvailabilityRule.room_id == room_id))
    created = []
    for r in rules:
        weekday = int(r["weekday"])
        if not 0 <= weekday <= 6:
            raise invalid("invalid_weekday", "Weekday must be 0 (Monday) to 6 (Sunday).")
        rule = AvailabilityRule(
            user_id=user.id,
            room_id=room_id,
            weekday=weekday,
            start_local=_validate_hhmm(r["start"]),
            end_local=_validate_hhmm(r["end"]),
            version=version,
        )
        db.add(rule)
        created.append(rule)
    _cancel_stale_opportunities(db, room_id, user.id)
    db.commit()
    return created


def add_exception(db: Session, user: User, room_id: str | None, kind: str, start: datetime, end: datetime) -> AvailabilityException:
    if room_id is not None:
        require_member(db, room_id, user.id)
    if kind not in ("busy", "travel", "extra"):
        raise invalid("invalid_exception_kind", "Use busy, travel or extra.")
    if start.tzinfo is None or end.tzinfo is None or end <= start:
        raise invalid("invalid_interval", "Provide a UTC start before the end.")
    exc = AvailabilityException(user_id=user.id, room_id=room_id, kind=kind, start_at=start, end_at=end)
    db.add(exc)
    if kind in ("busy", "travel"):
        # A busy change cancels pending deliveries in every room this person belongs to.
        _cancel_stale_opportunities(db, room_id, user.id)
    db.commit()
    return exc


def _cancel_stale_opportunities(db: Session, room_id: str | None, user_id: str) -> None:
    query = select(CallOpportunity).where(CallOpportunity.state.in_(("pending", "notified")))
    if room_id is not None:
        query = query.where(CallOpportunity.room_id == room_id)
    for opp in db.scalars(query):
        if user_id in (opp.participant_ids or []) and not opportunity_still_valid(db, opp):
            opp.state = "cancelled"
            notifications.cancel_for_resource(db, opp.id)


def build_schedule(db: Session, user: User, room_id: str) -> ParticipantSchedule:
    rules = db.scalars(
        select(AvailabilityRule).where(AvailabilityRule.user_id == user.id, AvailabilityRule.room_id == room_id)
    ).all()
    exceptions = db.scalars(
        select(AvailabilityException).where(
            AvailabilityException.user_id == user.id,
            (AvailabilityException.room_id == room_id) | (AvailabilityException.room_id.is_(None)),
            AvailabilityException.end_at > utcnow() - timedelta(days=1),
        )
    ).all()
    return ParticipantSchedule(
        user_id=user.id,
        zone=user.time_zone,
        rules=[WeeklyRule(r.weekday, r.start_local, r.end_local) for r in rules],
        busy=[(e.start_at, e.end_at) for e in exceptions if e.kind in ("busy", "travel")],
        extra=[(e.start_at, e.end_at) for e in exceptions if e.kind == "extra"],
        quiet_start=user.quiet_start,
        quiet_end=user.quiet_end,
        min_minutes=max(get_settings().default_min_overlap_minutes, user.preferred_call_minutes or 15),
        rule_versions=tuple(sorted({r.version for r in rules})),
    )


def _participants(db: Session, actor: User, room_id: str, participant_ids: list[str]) -> list[User]:
    require_member(db, room_id, actor.id)
    ids = sorted(set(participant_ids) | {actor.id})
    if len(ids) < 2:
        raise invalid("need_participants", "Choose at least one other person.")
    users = []
    for uid in ids:
        user = db.get(User, uid)
        try:
            require_member(db, room_id, uid)
        except Exception:
            raise invalid("participant_not_member", "Everyone selected must be in this room.") from None
        assert user is not None
        if uid != actor.id and not user.share_availability_hints:
            raise forbidden(f"{user.display_name} doesn't share availability hints in SameSky.")
        users.append(user)
    return users


def free_now_active(db: Session, user_id: str, room_id: str, now: datetime | None = None) -> bool:
    now = now or utcnow()
    row = db.scalar(select(FreeNow).where(FreeNow.user_id == user_id, FreeNow.room_id == room_id))
    return row is not None and row.expires_at > now


def set_free_now(db: Session, user: User, room_id: str) -> FreeNow:
    require_member(db, room_id, user.id)
    now = utcnow()
    row = db.scalar(select(FreeNow).where(FreeNow.user_id == user.id, FreeNow.room_id == room_id))
    expires = now + timedelta(minutes=get_settings().free_now_minutes)
    if row is None:
        row = FreeNow(user_id=user.id, room_id=room_id, set_at=now, expires_at=expires)
        db.add(row)
    else:
        row.set_at, row.expires_at = now, expires
    db.commit()
    return row


def clear_free_now(db: Session, user: User, room_id: str) -> None:
    require_member(db, room_id, user.id)
    db.execute(delete(FreeNow).where(FreeNow.user_id == user.id, FreeNow.room_id == room_id))
    db.commit()


def _names_phrase(names: list[str]) -> str:
    if len(names) <= 2:
        return " and ".join(names)
    return ", ".join(names[:-1]) + " and " + names[-1]


def opportunities(db: Session, actor: User, room_id: str, participant_ids: list[str], now: datetime | None = None, days: int = 7) -> dict:
    now = now or utcnow()
    users = _participants(db, actor, room_id, participant_ids)
    schedules = [build_schedule(db, u, room_id) for u in users]
    overlaps = common_overlaps(schedules, now, days=days)
    names = display_names(db, [u.id for u in users])
    others = [names[u.id] for u in users if u.id != actor.id]

    all_free = all(free_now_active(db, u.id, room_id, now) for u in users) and not any(
        is_blocked_now(s, now) for s in schedules
    )
    if all_free:
        status_message = (
            "You're both marked free now. Want to connect?"
            if len(users) == 2
            else f"{_names_phrase(others)} and you are all marked free now. Want to connect?"
        )
    else:
        status_message = None

    candidates = []
    for start, end in overlaps[:10]:
        minutes = int((end - start).total_seconds() // 60)
        candidates.append(
            {
                "startUtc": start.isoformat(),
                "endUtc": end.isoformat(),
                "minutes": minutes,
                "message": f"Your chosen schedules overlap for {minutes} minutes.",
                "local": [
                    {"userId": u.id, "displayName": names[u.id], **{k: v for k, v in local_label(start, u.time_zone).items()}, "endTime": local_label(end, u.time_zone)["time"]}
                    for u in users
                ],
            }
        )
    return {
        "participants": [{"userId": u.id, "displayName": names[u.id], "timeZone": u.time_zone, "freeNow": free_now_active(db, u.id, room_id, now)} for u in users],
        "participantLabel": _names_phrase(others) + " and you",
        "allMarkedFreeNow": all_free,
        "statusMessage": status_message,
        "quietHoursRespected": True,
        "candidates": candidates,
        "fingerprint": schedule_fingerprint(schedules),
    }


def suggest(db: Session, actor: User, room_id: str, participant_ids: list[str], start_utc: datetime, now: datetime | None = None) -> CallOpportunity:
    """Explicitly offer one computed overlap to the selected participants only."""
    now = now or utcnow()
    users = _participants(db, actor, room_id, participant_ids)
    schedules = [build_schedule(db, u, room_id) for u in users]
    wanted = max(start_utc, now)  # a window that has already started is offered from "now"
    match = next(((max(s, wanted), e) for s, e in common_overlaps(schedules, now) if s <= wanted < e), None)
    if match is not None and match[1] - match[0] < timedelta(minutes=max(sch.min_minutes for sch in schedules)):
        match = None
    if match is None:
        raise invalid("not_an_overlap", "That time is no longer a shared opening. Refresh and try again.")
    ids = [u.id for u in users]
    key = ",".join(ids)
    existing = db.scalar(
        select(CallOpportunity).where(
            CallOpportunity.room_id == room_id, CallOpportunity.participants_key == key, CallOpportunity.start_at == match[0]
        )
    )
    if existing is not None:
        return existing
    opp = CallOpportunity(
        room_id=room_id,
        participants_key=key,
        participant_ids=ids,
        start_at=match[0],
        end_at=match[1],
        schedule_fingerprint=schedule_fingerprint(schedules),
        created_by=actor.id,
    )
    try:
        with db.begin_nested():
            db.add(opp)
            db.flush()
    except IntegrityError:
        return db.scalar(select(CallOpportunity).where(CallOpportunity.room_id == room_id, CallOpportunity.participants_key == key, CallOpportunity.start_at == match[0]))
    for uid in ids:
        if uid != actor.id:
            notifications.queue(
                db, kind="call_opportunity", room_id=room_id, recipient_id=uid,
                resource_type="call_opportunity", resource_id=opp.id, expires_at=opp.end_at,
            )
    db.commit()
    return opp


def opportunity_still_valid(db: Session, opp: CallOpportunity, now: datetime | None = None) -> bool:
    now = now or utcnow()
    if opp.state not in ("pending", "notified") or opp.end_at <= now:
        return False
    users = []
    for uid in opp.participant_ids or []:
        user = db.get(User, uid)
        try:
            require_member(db, opp.room_id, uid)
        except Exception:
            return False
        if user is None:
            return False
        users.append(user)
    schedules = [build_schedule(db, u, opp.room_id) for u in users]
    window_start = max(now, opp.start_at)
    for s, e in common_overlaps(schedules, now, min_minutes=1):
        if s <= window_start and e >= min(opp.end_at, window_start + timedelta(minutes=1)):
            return True
    return False


def scan_direct_rooms(db: Session, now: datetime | None = None) -> int:
    """Worker task: propose the next overlap in each direct room (circles need an explicit selection)."""
    from ..models import Room

    now = now or utcnow()
    created = 0
    for room in db.scalars(select(Room).where(Room.kind == "direct")):
        from .rooms import room_members

        members = [u for _, u in room_members(db, room.id)]
        if len(members) != 2 or not all(u.share_availability_hints for u in members):
            continue
        schedules = [build_schedule(db, u, room.id) for u in members]
        overlaps = [(s, e) for s, e in common_overlaps(schedules, now, days=1) if s <= now + timedelta(hours=6)]
        if not overlaps:
            continue
        start, end = overlaps[0]
        ids = sorted(u.id for u in members)
        key = ",".join(ids)
        if db.scalar(select(CallOpportunity.id).where(CallOpportunity.room_id == room.id, CallOpportunity.participants_key == key, CallOpportunity.start_at == start)):
            continue
        opp = CallOpportunity(
            room_id=room.id, participants_key=key, participant_ids=ids, start_at=start, end_at=end,
            schedule_fingerprint=schedule_fingerprint(schedules),
        )
        db.add(opp)
        db.flush()
        for uid in ids:
            notifications.queue(db, kind="call_opportunity", room_id=room.id, recipient_id=uid,
                                resource_type="call_opportunity", resource_id=opp.id, expires_at=end)
        created += 1
    db.commit()
    return created


def respond(db: Session, actor: User, opp_id: str, action: str) -> dict:
    opp = db.get(CallOpportunity, opp_id)
    if opp is None or actor.id not in (opp.participant_ids or []):
        from ..errors import not_found

        raise not_found("call opportunity")
    require_member(db, opp.room_id, actor.id)
    if action == "pause":
        actor.suggestions_paused_until = utcnow() + timedelta(days=7)
        db.commit()
        return {"state": opp.state, "pausedUntil": actor.suggestions_paused_until.isoformat()}
    if action in ("not_now", "later"):
        # "Later" keeps it in the inbox; "Not now" dismisses it for this person only.
        if action == "not_now":
            for n in notifications.inbox(db, actor):
                if n.resource_id == opp.id:
                    n.read_at = utcnow()
        db.commit()
        return {"state": opp.state}
    if action == "connect":
        # Never simulates a call: return the actor's validated handoff link (if any) and the room.
        link = actor.call_link if is_allowed_call_link(actor.call_link) else None
        return {"state": opp.state, "roomId": opp.room_id, "handoffLink": link, "builtInCalling": False}
    raise invalid("invalid_action", "Use connect, later, not_now or pause.")


def is_allowed_call_link(link: str | None) -> bool:
    if not link:
        return False
    parsed = urlparse(link)
    return parsed.scheme in get_settings().allowed_call_link_schemes and bool(parsed.netloc or parsed.path)


def utc(dt: datetime) -> datetime:
    return dt if dt.tzinfo else dt.replace(tzinfo=UTC)
