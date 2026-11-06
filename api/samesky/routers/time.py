"""Availability, Free now, call opportunities, notification inbox and Web Push subscriptions."""

from __future__ import annotations

from datetime import datetime

from fastapi import APIRouter, Depends
from pydantic import BaseModel, Field
from sqlalchemy import delete, select
from sqlalchemy.orm import Session

from ..config import get_settings
from ..db import get_db
from ..deps import current_user
from ..errors import invalid, not_found
from ..models import AvailabilityException, AvailabilityRule, FreeNow, Notification, PushSubscription, User, utcnow
from ..services import availability, notifications
from ..services.access import require_member

router = APIRouter(prefix="/v1")


class RuleIn(BaseModel):
    weekday: int = Field(ge=0, le=6)
    start: str
    end: str


class RulesIn(BaseModel):
    rules: list[RuleIn] = Field(max_length=42)


@router.get("/rooms/{room_id}/availability")
def my_availability(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    require_member(db, room_id, user.id)
    rules = db.scalars(select(AvailabilityRule).where(AvailabilityRule.user_id == user.id, AvailabilityRule.room_id == room_id)).all()
    exceptions = db.scalars(
        select(AvailabilityException).where(AvailabilityException.user_id == user.id, AvailabilityException.end_at > utcnow())
    ).all()
    free = db.scalar(select(FreeNow).where(FreeNow.user_id == user.id, FreeNow.room_id == room_id))
    return {
        "timeZone": user.time_zone,
        "rules": [{"weekday": r.weekday, "start": r.start_local, "end": r.end_local, "version": r.version} for r in rules],
        "exceptions": [
            {"id": e.id, "kind": e.kind, "roomId": e.room_id, "start": e.start_at.isoformat(), "end": e.end_at.isoformat()} for e in exceptions
        ],
        "freeNowUntil": free.expires_at.isoformat() if free and free.expires_at > utcnow() else None,
    }


@router.put("/rooms/{room_id}/availability/rules")
def put_rules(room_id: str, body: RulesIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    created = availability.set_rules(db, user, room_id, [r.model_dump() for r in body.rules])
    return {"count": len(created)}


class ExceptionIn(BaseModel):
    kind: str
    start: datetime
    end: datetime
    room_id: str | None = None


@router.post("/availability/exceptions")
def add_exception(body: ExceptionIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    exc = availability.add_exception(db, user, body.room_id, body.kind, body.start, body.end)
    return {"id": exc.id}


@router.delete("/availability/exceptions/{exception_id}")
def delete_exception(exception_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    exc = db.get(AvailabilityException, exception_id)
    if exc is None or exc.user_id != user.id:
        raise not_found("exception")
    db.delete(exc)
    db.commit()
    return {"ok": True}


@router.post("/rooms/{room_id}/free-now")
def set_free(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    row = availability.set_free_now(db, user, room_id)
    return {"freeNowUntil": row.expires_at.isoformat()}


@router.delete("/rooms/{room_id}/free-now")
def clear_free(room_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    availability.clear_free_now(db, user, room_id)
    return {"freeNowUntil": None}


class OpportunityQuery(BaseModel):
    participant_ids: list[str] = Field(min_length=1, max_length=5)
    days: int = Field(default=7, ge=1, le=7)


@router.post("/rooms/{room_id}/opportunities")
def compute(room_id: str, body: OpportunityQuery, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return availability.opportunities(db, user, room_id, body.participant_ids, days=body.days)


class SuggestIn(BaseModel):
    participant_ids: list[str] = Field(min_length=1, max_length=5)
    start_utc: datetime


@router.post("/rooms/{room_id}/opportunities/suggest")
def suggest(room_id: str, body: SuggestIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    if body.start_utc.tzinfo is None:
        raise invalid("invalid_interval", "Use a UTC timestamp.")
    opp = availability.suggest(db, user, room_id, body.participant_ids, body.start_utc)
    return {"id": opp.id, "state": opp.state, "startUtc": opp.start_at.isoformat(), "endUtc": opp.end_at.isoformat(), "participantIds": opp.participant_ids}


class RespondIn(BaseModel):
    action: str


@router.post("/opportunities/{opp_id}/respond")
def respond(opp_id: str, body: RespondIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return availability.respond(db, user, opp_id, body.action)


# ---------------------------------------------------------------------------
# Inbox and push
# ---------------------------------------------------------------------------


@router.get("/notifications")
def inbox(user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    rows = notifications.inbox(db, user)
    return {
        "unread": sum(1 for n in rows if n.read_at is None),
        "items": [
            {
                "id": n.id, "kind": n.kind, "roomId": n.room_id, "resourceType": n.resource_type, "resourceId": n.resource_id,
                "text": n.payload.get("text"), "createdAt": n.created_at.isoformat(), "read": n.read_at is not None,
                "pushState": n.push_state,
            }
            for n in rows
        ],
    }


@router.post("/notifications/{notification_id}/read")
def mark_read(notification_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    n = db.get(Notification, notification_id)
    if n is None or n.recipient_id != user.id:
        raise not_found("notification")
    n.read_at = n.read_at or utcnow()
    db.commit()
    return {"ok": True}


@router.get("/push/vapid-public-key")
def vapid_key() -> dict:
    key = get_settings().vapid_public_key
    return {"publicKey": key, "enabled": bool(key and get_settings().vapid_private_key)}


class SubscriptionKeys(BaseModel):
    p256dh: str = Field(max_length=200)
    auth: str = Field(max_length=100)


class SubscriptionIn(BaseModel):
    endpoint: str = Field(max_length=2000)
    keys: SubscriptionKeys
    user_agent_family: str | None = Field(default=None, max_length=40)


@router.post("/push/subscriptions")
def save_subscription(body: SubscriptionIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    if not body.endpoint.startswith("https://"):
        raise invalid("invalid_endpoint", "Push endpoints must use HTTPS.")
    existing = db.scalar(select(PushSubscription).where(PushSubscription.endpoint == body.endpoint))
    if existing is not None and existing.user_id != user.id:
        db.delete(existing)  # endpoint moved to another account on this browser
        db.flush()
        existing = None
    if existing is None:
        db.add(PushSubscription(user_id=user.id, endpoint=body.endpoint, p256dh=body.keys.p256dh, auth=body.keys.auth, user_agent_family=body.user_agent_family))
    else:
        existing.p256dh, existing.auth, existing.failure_count = body.keys.p256dh, body.keys.auth, 0
    db.commit()
    return {"ok": True}


class SubscriptionDelete(BaseModel):
    endpoint: str


@router.post("/push/subscriptions/delete")
def delete_subscription(body: SubscriptionDelete, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    db.execute(delete(PushSubscription).where(PushSubscription.user_id == user.id, PushSubscription.endpoint == body.endpoint))
    db.commit()
    return {"ok": True}
