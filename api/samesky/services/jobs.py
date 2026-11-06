"""Durable Postgres/SQLite job outbox: idempotency keys, leases, retries with backoff, expiry.

A job is claimed by atomically moving it to `running` with a lease. If a worker dies, the lease
expires and the job is reclaimed on restart (recovery path). Every handler re-validates its own
preconditions so a retry or late run never produces a duplicate or stale effect.
"""

from __future__ import annotations

from datetime import datetime, timedelta

from sqlalchemy import or_, select, update
from sqlalchemy.exc import IntegrityError
from sqlalchemy.orm import Session

from ..models import Job, utcnow

LEASE_SECONDS = 60


def enqueue(
    db: Session,
    kind: str,
    idempotency_key: str,
    payload: dict | None = None,
    *,
    run_after: datetime | None = None,
    expires_at: datetime | None = None,
    max_attempts: int = 5,
) -> Job:
    """Insert once per idempotency key. Re-enqueueing the same key returns the existing job."""
    existing = db.scalar(select(Job).where(Job.idempotency_key == idempotency_key))
    if existing is not None:
        return existing
    job = Job(
        kind=kind,
        idempotency_key=idempotency_key,
        payload=payload or {},
        run_after=run_after or utcnow(),
        expires_at=expires_at,
        max_attempts=max_attempts,
    )
    try:
        with db.begin_nested():
            db.add(job)
            db.flush()
    except IntegrityError:
        existing = db.scalar(select(Job).where(Job.idempotency_key == idempotency_key))
        assert existing is not None
        return existing
    return job


def claim_next(db: Session, now: datetime | None = None, kinds: tuple[str, ...] | None = None) -> Job | None:
    now = now or utcnow()
    query = (
        select(Job)
        .where(
            or_(
                (Job.state == "queued") & (Job.run_after <= now),
                (Job.state == "running") & (Job.lease_until < now),  # crashed worker recovery
            )
        )
        .order_by(Job.run_after)
        .limit(10)
    )
    if kinds:
        query = query.where(Job.kind.in_(kinds))
    for candidate in db.scalars(query).all():
        if candidate.expires_at is not None and candidate.expires_at <= now:
            candidate.state = "expired"
            candidate.updated_at = now
            db.commit()
            continue
        # Compare-and-set claim; on Postgres this is safe across several workers.
        result = db.execute(
            update(Job)
            .where(Job.id == candidate.id, Job.state == candidate.state, Job.attempts == candidate.attempts)
            .values(
                state="running",
                attempts=candidate.attempts + 1,
                lease_until=now + timedelta(seconds=LEASE_SECONDS),
                updated_at=now,
            )
        )
        db.commit()
        if result.rowcount == 1:
            db.refresh(candidate)
            return candidate
    return None


def complete(db: Session, job: Job) -> None:
    job.state = "done"
    job.lease_until = None
    job.updated_at = utcnow()
    db.commit()


def cancel(db: Session, job: Job, reason: str) -> None:
    job.state = "cancelled"
    job.last_error = reason[:120]
    job.lease_until = None
    job.updated_at = utcnow()
    db.commit()


def fail(db: Session, job: Job, safe_error: str, *, now: datetime | None = None) -> None:
    now = now or utcnow()
    job.last_error = safe_error[:120]
    job.lease_until = None
    job.updated_at = now
    if job.attempts >= job.max_attempts:
        job.state = "failed"
    else:
        job.state = "queued"
        job.run_after = now + timedelta(seconds=min(300, 5 * (2 ** (job.attempts - 1))))
    db.commit()


def cancel_matching(db: Session, kind: str, key_prefix: str, reason: str) -> int:
    jobs = db.scalars(
        select(Job).where(
            Job.kind == kind, Job.idempotency_key.like(f"{key_prefix}%"), Job.state.in_(("queued", "running"))
        )
    ).all()
    for job in jobs:
        job.state = "cancelled"
        job.last_error = reason[:120]
        job.updated_at = utcnow()
    return len(jobs)
