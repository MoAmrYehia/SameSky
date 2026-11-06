"""Persistent background worker: `python -m samesky.worker`.

Runs durable jobs from the `jobs` table (media processing, recap generation, daily drafts,
notification delivery) plus periodic scans (call opportunities, daily draft scheduling, expiry).
Safe to restart at any time: leases expire and jobs are reclaimed; every handler is idempotent and
re-validates its preconditions, so retries never duplicate effects or deliver stale notifications.
"""

from __future__ import annotations

import argparse
import logging
import signal
import time
from datetime import date, timedelta

from sqlalchemy import select

from .db import create_all, new_session
from .models import CallOpportunity, FreeNow, MediaAsset, utcnow
from .services import availability, jobs, media, notifications, recaps

log = logging.getLogger("samesky.worker")
_running = True


def handle(db, job) -> str:
    if job.kind == "media.process":
        asset = db.get(MediaAsset, job.payload["asset_id"])
        if asset is None:
            return "skipped_missing"
        media.process(db, asset)
        return asset.processing_state
    if job.kind == "recap.generate":
        return recaps.run_generation(db, job.payload["recap_id"])
    if job.kind == "recap.daily":
        return recaps.run_daily_draft(db, job.payload["room_id"], date.fromisoformat(job.payload["room_date"]))
    if job.kind == "notify":
        return notifications.dispatch(db, job)
    return "unknown_kind"


def run_once(now=None) -> int:
    """Process every runnable job once. Returns the number of jobs handled (used by tests too)."""
    handled = 0
    with new_session() as db:
        while True:
            job = jobs.claim_next(db, now=now)
            if job is None:
                break
            started = time.monotonic()
            try:
                outcome = handle(db, job)
                db.refresh(job)
                if job.state == "running":
                    jobs.complete(db, job)
                log.info("job kind=%s outcome=%s attempts=%d latency_ms=%d", job.kind, outcome, job.attempts, (time.monotonic() - started) * 1000)
            except Exception as exc:  # noqa: BLE001
                db.rollback()
                db.refresh(job)
                jobs.fail(db, job, type(exc).__name__)
                log.warning("job kind=%s failed error=%s attempts=%d", job.kind, type(exc).__name__, job.attempts)
            handled += 1
    return handled


def periodic(now=None) -> None:
    now = now or utcnow()
    with new_session() as db:
        # Expire opportunities whose overlap ended; drop expired Free now rows.
        for opp in db.scalars(select(CallOpportunity).where(CallOpportunity.state.in_(("pending", "notified")), CallOpportunity.end_at <= now)):
            opp.state = "expired"
            notifications.cancel_for_resource(db, opp.id)
        for row in db.scalars(select(FreeNow).where(FreeNow.expires_at <= now - timedelta(minutes=5))):
            db.delete(row)
        db.commit()
        availability.scan_direct_rooms(db, now)
        recaps.schedule_daily_drafts(db, now)


def main() -> None:
    parser = argparse.ArgumentParser(description="SameSky background worker")
    parser.add_argument("--once", action="store_true", help="process runnable jobs once and exit")
    parser.add_argument("--interval", type=float, default=2.0)
    args = parser.parse_args()
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")
    create_all()

    def stop(*_):
        global _running
        _running = False

    signal.signal(signal.SIGINT, stop)
    signal.signal(signal.SIGTERM, stop)
    last_periodic = 0.0
    log.info("worker started")
    while _running:
        if time.monotonic() - last_periodic > 60:
            try:
                periodic()
            except Exception as exc:  # noqa: BLE001
                log.warning("periodic failed error=%s", type(exc).__name__)
            last_periodic = time.monotonic()
        run_once()
        if args.once:
            break
        time.sleep(args.interval)
    log.info("worker stopped")


if __name__ == "__main__":
    main()
