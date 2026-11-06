"""In-app inbox + durable outbox delivery + opt-in Web Push.

Producers only enqueue `notify` jobs (idempotent per recipient + event). The worker re-checks the
recipient's membership and access to the resource *at dispatch time*, applies call-suggestion limits,
writes the inbox row exactly once, then attempts a push with a generic payload. Personal captions,
destinations and transcripts never go into push payloads.
"""

from __future__ import annotations

import json
import logging
from datetime import timedelta

from sqlalchemy import func, select
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..config import get_settings
from ..models import (
    CallOpportunity,
    Job,
    Membership,
    Moment,
    Notification,
    PushSubscription,
    Recap,
    Reply,
    User,
    utcnow,
)
from . import jobs
from .access import active_membership, can_view_moment, can_view_recap

log = logging.getLogger("samesky.notifications")

GENERIC_TEXT = {
    "recap_ready": "A shared moment is waiting.",
    "reply_received": "Someone left you a reply.",
    "call_opportunity": "You might have a moment to connect.",
}


def queue(
    db: Session,
    *,
    kind: str,
    room_id: str,
    recipient_id: str,
    resource_type: str,
    resource_id: str,
    expires_at=None,
) -> Job:
    event_key = f"{kind}:{resource_id}"
    return jobs.enqueue(
        db,
        "notify",
        f"notify:{room_id}:{recipient_id}:{event_key}",
        {
            "kind": kind,
            "room_id": room_id,
            "recipient_id": recipient_id,
            "resource_type": resource_type,
            "resource_id": resource_id,
            "event_key": event_key,
        },
        expires_at=expires_at,
    )


def cancel_for_resource(db: Session, resource_id: str) -> None:
    """Withdrawal/invalidation: cancel queued deliveries and hide already-delivered inbox rows."""
    now = utcnow()
    for job in db.scalars(select(Job).where(Job.kind == "notify", Job.state.in_(("queued", "running")))):
        if job.payload.get("resource_id") == resource_id:
            job.state = "cancelled"
            job.last_error = "resource_unavailable"
    for n in db.scalars(select(Notification).where(Notification.resource_id == resource_id, Notification.cancelled_at.is_(None))):
        n.cancelled_at = now


def resource_visible(db: Session, recipient_id: str, resource_type: str, resource_id: str) -> bool:
    if resource_type == "recap":
        recap = db.get(Recap, resource_id)
        return recap is not None and recap.state == "shared" and can_view_recap(db, recap, recipient_id)
    if resource_type == "reply":
        reply = db.get(Reply, resource_id)
        if reply is None or reply.withdrawn:
            return False
        parent = db.get(Moment, reply.moment_id)
        return parent is not None and can_view_moment(db, parent, recipient_id)
    if resource_type == "call_opportunity":
        opp = db.get(CallOpportunity, resource_id)
        return (
            opp is not None
            and opp.state in ("pending", "notified")
            and recipient_id in (opp.participant_ids or [])
            and opp.end_at > utcnow()
            and active_membership(db, opp.room_id, recipient_id) is not None
        )
    return False


def dispatch(db: Session, job: Job) -> str:
    """Returns a safe outcome code. Raises for retryable failures."""
    p = job.payload
    recipient = db.get(User, p["recipient_id"])
    membership = active_membership(db, p["room_id"], p["recipient_id"])
    if recipient is None or membership is None:
        return "skipped_not_member"
    if not resource_visible(db, recipient.id, p["resource_type"], p["resource_id"]):
        return "skipped_unavailable"
    existing = db.scalar(
        select(Notification).where(Notification.recipient_id == recipient.id, Notification.event_key == p["event_key"])
    )
    if existing is not None:
        return "duplicate_suppressed"

    if p["kind"] == "call_opportunity":
        outcome = _call_suggestion_gate(db, recipient, membership, p["resource_id"])
        if outcome:
            return outcome

    notification = Notification(
        recipient_id=recipient.id,
        room_id=p["room_id"],
        kind=p["kind"],
        event_key=p["event_key"],
        resource_type=p["resource_type"],
        resource_id=p["resource_id"],
        payload={"text": GENERIC_TEXT.get(p["kind"], "Something is waiting in SameSky.")},
    )
    try:
        with db.begin_nested():
            db.add(notification)
            db.flush()
    except IntegrityError:
        return "duplicate_suppressed"
    if p["kind"] == "call_opportunity":
        opp = db.get(CallOpportunity, p["resource_id"])
        if opp is not None and opp.state == "pending":
            opp.state = "notified"
    prefs = recipient.notification_prefs or {}
    notification.push_state = send_push(db, recipient.id, p["kind"]) if prefs.get("push", True) else "skipped"
    db.commit()
    return "delivered"


def _call_suggestion_gate(db: Session, recipient: User, membership: Membership, opp_id: str) -> str | None:
    settings = get_settings()
    now = utcnow()
    if membership.muted:
        return "skipped_room_muted"
    if recipient.suggestions_paused_until and recipient.suggestions_paused_until > now:
        return "skipped_paused"
    if not (recipient.notification_prefs or {}).get("call_suggestions", True):
        return "skipped_opted_out"
    recent = db.scalars(
        select(Notification.created_at).where(
            Notification.recipient_id == recipient.id,
            Notification.kind == "call_opportunity",
            Notification.created_at > now - timedelta(days=1),
        )
    ).all()
    if len(recent) >= settings.call_suggestions_per_day:
        return "skipped_daily_limit"
    if any(t > now - timedelta(hours=settings.call_suggestion_spacing_hours) for t in recent):
        return "skipped_spacing"
    # Re-verify the overlap still exists with *current* schedules (busy changes cancel delivery).
    from .availability import opportunity_still_valid

    opp = db.get(CallOpportunity, opp_id)
    if opp is None or not opportunity_still_valid(db, opp):
        if opp is not None:
            opp.state = "cancelled"
            db.commit()
        return "skipped_schedule_changed"
    return None


def send_push(db: Session, user_id: str, kind: str) -> str:
    settings = get_settings()
    subs = db.scalars(select(PushSubscription).where(PushSubscription.user_id == user_id)).all()
    if not subs:
        return "none"
    if not (settings.vapid_private_key and settings.vapid_public_key):
        return "skipped"
    from pywebpush import WebPushException, webpush

    payload = json.dumps({"title": "SameSky", "body": GENERIC_TEXT.get(kind, "A shared moment is waiting."), "kind": kind})
    sent = False
    for sub in subs:
        try:
            webpush(
                subscription_info={"endpoint": sub.endpoint, "keys": {"p256dh": sub.p256dh, "auth": sub.auth}},
                data=payload,
                vapid_private_key=settings.vapid_private_key,
                vapid_claims={"sub": settings.vapid_subject},
                ttl=3600,
            )
            sent = True
            sub.failure_count = 0
        except WebPushException as exc:
            status = getattr(exc.response, "status_code", None)
            if status in (404, 410):
                db.delete(sub)  # expired or unsubscribed endpoint
                log.info("push_subscription_removed status=%s", status)
            else:
                sub.failure_count += 1
                log.warning("push_failed status=%s", status)
        except Exception:  # noqa: BLE001 - network errors must not break the inbox
            sub.failure_count += 1
            log.warning("push_failed status=network")
    return "sent" if sent else "failed"


def inbox(db: Session, user: User, limit: int = 50) -> list[Notification]:
    rows = db.scalars(
        select(Notification)
        .where(Notification.recipient_id == user.id, Notification.cancelled_at.is_(None))
        .order_by(Notification.created_at.desc())
        .limit(limit)
    ).all()
    # Read-time recheck: never show a notification whose resource the user can no longer see.
    return [n for n in rows if n.kind == "call_opportunity" or resource_visible(db, user.id, n.resource_type, n.resource_id)]


def unread_count(db: Session, user: User) -> int:
    return db.scalar(
        select(func.count()).select_from(Notification).where(
            Notification.recipient_id == user.id, Notification.read_at.is_(None), Notification.cancelled_at.is_(None)
        )
    ) or 0
