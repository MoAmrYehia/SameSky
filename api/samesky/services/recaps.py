"""Daily recaps: eligible-source selection, bounded generation, validation, per-contributor approval, publication.

State machine: queued -> generating -> private_preview -> awaiting_approval -> shared
               (any non-terminal) -> invalidated | failed
A recap only ever uses moments that are currently shared, AI-authorized and visible to *every*
member of its target audience. The audience is chosen by the server (from the requester's explicit
choice), frozen in a snapshot, and echoed by the plan; the model can never widen it.
"""

from __future__ import annotations

import hashlib
import json
from datetime import date, datetime, timedelta
from zoneinfo import ZoneInfo

from sqlalchemy import func, select
from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import conflict, invalid, not_found
from ..models import (
    AudienceSnapshot,
    MediaAsset,
    Moment,
    Recap,
    RecapApproval,
    RecapSource,
    Room,
    User,
    new_id,
    utcnow,
)
from . import ai, awards, jobs, notifications
from .access import (
    active_member_ids,
    can_see_recap_status,
    can_view_recap,
    create_snapshot,
    current_valid_recipients,
    display_names,
    recap_contributor_ids,
    require_member,
    snapshot_grants,
)

OBJECT_FOR_KIND = {"photo": "frame", "clip": "postcard", "voice": "cup", "text": "frame"}
ACTIVE_STATES = ("queued", "generating", "private_preview", "awaiting_approval")


class PlanInvalid(Exception):
    def __init__(self, code: str):
        super().__init__(code)
        self.code = code


# ---------------------------------------------------------------------------
# Eligibility
# ---------------------------------------------------------------------------


def _moment_eligible(db: Session, moment: Moment, audience: set[str]) -> bool:
    if moment.sharing_state != "shared" or not moment.ai_allowed or moment.audience_snapshot_id is None:
        return False
    if moment.media_asset_id:
        asset = db.get(MediaAsset, moment.media_asset_id)
        if asset is None or asset.processing_state != "ready":
            return False
    visible_to = current_valid_recipients(db, moment.audience_snapshot_id) | {moment.owner_id}
    if moment.owner_id not in set(active_member_ids(db, moment.room_id)):
        return False
    return audience <= visible_to


def eligible_moments(db: Session, room: Room, room_date: date, audience: set[str]) -> list[Moment]:
    rows = db.scalars(
        select(Moment).where(Moment.room_id == room.id, Moment.room_date == room_date, Moment.sharing_state == "shared")
        .order_by(Moment.capture_at)
    ).all()
    return [m for m in rows if _moment_eligible(db, m, audience)]


def select_sources(moments: list[Moment], budget: int) -> tuple[list[Moment], int]:
    """At least one highlight per contributor when the budget permits, then fill chronologically."""
    by_owner: dict[str, list[Moment]] = {}
    for m in moments:
        by_owner.setdefault(m.owner_id, []).append(m)
    order = sorted(by_owner, key=lambda o: by_owner[o][0].capture_at)
    chosen: list[Moment] = []
    while len(chosen) < budget and any(by_owner.values()):
        for owner in order:
            if by_owner[owner] and len(chosen) < budget:
                chosen.append(by_owner[owner].pop(0))
    chosen.sort(key=lambda m: m.capture_at)
    return chosen, len(moments) - len(chosen)


def _cache_key(db: Session, room: Room, recipients: list[str], sources: list[Moment]) -> str:
    members = {}
    for uid in recipients:
        from .access import active_membership

        m = active_membership(db, room.id, uid)
        members[uid] = m.generation if m else -1
    material = {
        "room": room.id,
        "audience": sorted(members.items()),
        "sources": sorted((m.id, m.source_version, m.sharing_version) for m in sources),
        "model": ai.model_config_fingerprint(),
    }
    return hashlib.sha256(json.dumps(material, sort_keys=True).encode()).hexdigest()


# ---------------------------------------------------------------------------
# Request / generation
# ---------------------------------------------------------------------------


def request_recap(db: Session, actor: User, room_id: str, room_date: date, recipient_ids: list[str] | None) -> Recap:
    room, _ = require_member(db, room_id, actor.id)
    members = set(active_member_ids(db, room.id))
    audience = set(recipient_ids) if recipient_ids else set(members)
    audience.add(actor.id)
    if not audience <= members:
        raise invalid("recipient_not_member", "Every recipient must be a current member of this room.")
    if len(audience) < 2:
        raise invalid("empty_audience", "A shared recap needs at least one other person.")
    moments = eligible_moments(db, room, room_date, audience)
    if not moments:
        raise conflict("nothing_shared", "Nothing was shared with everyone in this audience for that day yet.")
    sources, omitted = select_sources(moments, get_settings().recap_max_sources)
    recipients = sorted(audience)
    cache_key = _cache_key(db, room, recipients, sources)
    existing = db.scalar(
        select(Recap).where(Recap.room_id == room.id, Recap.cache_key == cache_key, Recap.state.in_(ACTIVE_STATES + ("shared",)))
        .order_by(Recap.created_at.desc())
    )
    if existing is not None:
        return existing  # retry / cache hit: same audience, same generations, same source versions
    recent = db.scalar(
        select(func.count()).select_from(Recap).where(
            Recap.room_id == room.id, Recap.room_date == room_date, Recap.created_at > utcnow() - timedelta(days=1)
        )
    ) or 0
    if recent >= get_settings().ai_generations_per_room_per_day:
        raise conflict("generation_limit", "This day has been regenerated several times already. Try again tomorrow.")
    version = (db.scalar(select(func.max(Recap.version)).where(Recap.room_id == room.id, Recap.room_date == room_date)) or 0) + 1
    recap_id = new_id()
    snapshot = create_snapshot(db, room, "recap", recap_id, actor.id, recipients, include_creator_in_room_wide=False)
    recap = Recap(
        id=recap_id, room_id=room.id, room_date=room_date, version=version, requested_by=actor.id,
        audience_snapshot_id=snapshot.id, cache_key=cache_key, omitted_count=omitted, state="queued",
    )
    db.add(recap)
    db.flush()
    for m in sources:
        db.add(RecapSource(recap_id=recap.id, moment_id=m.id, contributor_id=m.owner_id, source_version=m.source_version, sharing_version=m.sharing_version))
    jobs.enqueue(db, "recap.generate", f"recap:{recap.id}", {"recap_id": recap.id}, max_attempts=3)
    db.commit()
    return recap


def _sources(db: Session, recap: Recap) -> list[tuple[RecapSource, Moment]]:
    rows = db.execute(
        select(RecapSource, Moment).join(Moment, Moment.id == RecapSource.moment_id).where(RecapSource.recap_id == recap.id)
        .order_by(Moment.capture_at)
    ).all()
    return [(s, m) for s, m in rows]


def _audience(db: Session, recap: Recap) -> set[str]:
    return {g.user_id for g in snapshot_grants(db, recap.audience_snapshot_id)}


def sources_still_valid(db: Session, recap: Recap) -> str | None:
    """Return an invalidation reason, or None if every source and the audience are still intact."""
    audience = _audience(db, recap)
    if current_valid_recipients(db, recap.audience_snapshot_id) != audience:
        return "membership_changed"
    for src, moment in _sources(db, recap):
        if moment.sharing_state != "shared":
            return "source_withdrawn"
        if not moment.ai_allowed:
            return "ai_permission_withdrawn"
        if moment.source_version != src.source_version or moment.sharing_version != src.sharing_version:
            return "source_changed"
        if not _moment_eligible(db, moment, audience):
            return "audience_changed"
    return None


def invalidate(db: Session, recap: Recap, reason: str) -> None:
    recap.state = "invalidated"
    recap.invalidation_reason = reason
    recap.plan = None
    notifications.cancel_for_resource(db, recap.id)
    awards.detach_recap(db, recap.id)


def run_generation(db: Session, recap_id: str) -> str:
    recap = db.get(Recap, recap_id)
    if recap is None or recap.state not in ("queued", "generating"):
        return "skipped"
    reason = sources_still_valid(db, recap)
    if reason:
        invalidate(db, recap, reason)
        db.commit()
        return f"invalidated:{reason}"
    recap.state = "generating"
    db.commit()

    room = db.get(Room, recap.room_id)
    assert room is not None
    pairs = _sources(db, recap)
    names = display_names(db, [m.owner_id for _, m in pairs])
    plan = None
    generator = "chronological"
    reused = db.scalar(
        select(Recap).where(Recap.cache_key == recap.cache_key, Recap.id != recap.id, Recap.plan.is_not(None), Recap.generator == "ai")
    )
    if reused is not None and reused.audience_snapshot_id:
        # Cache keys include the exact audience + generations, so a hit is the same audience by construction.
        candidate = dict(reused.plan)
        candidate["audienceSnapshotId"] = recap.audience_snapshot_id
        candidate["recapId"] = recap.id
        try:
            validate_plan(db, recap, candidate)
            plan, generator = candidate, "ai"
        except PlanInvalid:
            plan = None
    if plan is None:
        try:
            inputs = [
                ai.SourceInput(
                    moment_id=m.id, contributor_id=m.owner_id, contributor_name=names.get(m.owner_id, "Someone"), kind=m.kind,
                    caption=m.caption, transcript=m.transcript if m.transcript_consent else None,
                    capture_local=m.capture_at.astimezone(ZoneInfo(m.capture_zone)).strftime("%H:%M ") + m.capture_zone,
                    source_version=m.source_version, sharing_version=m.sharing_version,
                    duration_ms=_duration(db, m),
                )
                for _, m in pairs
            ]
            order = list(dict.fromkeys(m.owner_id for _, m in pairs))
            draft = ai.generate_plan(inputs, recap.room_date.isoformat(), room.reporting_zone, order)
            plan = plan_from_draft(db, recap, room, draft)
            validate_plan(db, recap, plan)
            generator = "ai"
        except (ai.AiUnavailable, PlanInvalid) as exc:
            recap.failure_code = getattr(exc, "code", None) or str(exc)[:40]
            plan = chronological_plan(db, recap, room)
            generator = "chronological"

    # Re-check consent, grants and membership at *finish*, not only at start.
    db.refresh(recap)
    if recap.state != "generating":
        return "superseded"
    reason = sources_still_valid(db, recap)
    if reason:
        invalidate(db, recap, reason)
        db.commit()
        return f"invalidated:{reason}"
    validate_plan(db, recap, plan)
    recap.plan = plan
    recap.generator = generator
    recap.state = "private_preview"
    db.commit()
    return f"preview:{generator}"


def _duration(db: Session, moment: Moment) -> int | None:
    if not moment.media_asset_id:
        return None
    asset = db.get(MediaAsset, moment.media_asset_id)
    return asset.duration_ms if asset else None


def _base_plan(recap: Recap, room: Room, title: str) -> dict:
    return {
        "schemaVersion": 2,
        "recapId": recap.id,
        "roomId": recap.room_id,
        "audienceSnapshotId": recap.audience_snapshot_id,
        "roomDate": recap.room_date.isoformat(),
        "roomTimeZone": room.reporting_zone,
        "title": title[:80],
        "chapters": [],
        "omittedCount": recap.omitted_count,
    }


def plan_from_draft(db: Session, recap: Recap, room: Room, draft: dict) -> dict:
    """Server fills every identity/version field; the model only supplied prose, ids and layout tokens."""
    by_id = {m.id: m for _, m in _sources(db, recap)}
    plan = _base_plan(recap, room, str(draft.get("title") or "Our day"))
    plan["closingLine"] = str(draft.get("closingLine") or "")[:200]
    for ch in draft.get("chapters") or []:
        segments = []
        for seg in ch.get("segments") or []:
            refs = []
            for mid in seg.get("momentIds") or []:
                m = by_id.get(mid)
                if m is None:
                    raise PlanInvalid("unknown_source")
                refs.append({"momentId": mid, "sourceVersion": m.source_version, "sharingVersion": m.sharing_version})
            segments.append({"text": str(seg.get("text") or ""), "kind": seg.get("kind", "stated"), "sources": refs})
        plan["chapters"].append(
            {
                "contributorId": ch.get("contributorId"),
                "segments": segments,
                "playbackMomentIds": list(ch.get("playbackMomentIds") or []),
                "objectType": ch.get("objectType"),
                "durationSeconds": int(ch.get("durationSeconds") or 0),
            }
        )
    return plan


def chronological_plan(db: Session, recap: Recap, room: Room) -> dict:
    """Deterministic fallback, clearly labelled: no AI, just the shared moments in order."""
    pairs = _sources(db, recap)
    names = display_names(db, [m.owner_id for _, m in pairs])
    plan = _base_plan(recap, room, f"Our day · {recap.room_date.strftime('%A %d %B').replace(' 0', ' ')}")
    plan["closingLine"] = "That's everything that was shared for this day."
    plan["fallback"] = "chronological"
    order = list(dict.fromkeys(m.owner_id for _, m in pairs))
    for owner in order:
        mine = [m for _, m in pairs if m.owner_id == owner]
        segments = []
        for m in mine:
            name = names.get(owner, "Someone")
            local = m.capture_at.astimezone(ZoneInfo(m.capture_zone)).strftime("%H:%M")
            noun = {"photo": "a photo", "clip": "a clip", "voice": "a voice note", "text": "a note"}[m.kind]
            if m.kind == "voice" and m.transcript_consent and m.transcript:
                text, kind = f"{name} left {noun} at {local}: “{m.transcript[:160]}”", "quote"
            elif m.caption:
                text, kind = f"{name} shared {noun} at {local}: “{m.caption[:160]}”", "stated"
            else:
                text, kind = f"{name} shared {noun} at {local}.", "stated"
            segments.append({"text": text, "kind": kind, "sources": [{"momentId": m.id, "sourceVersion": m.source_version, "sharingVersion": m.sharing_version}]})
        seconds = sum(min(15, int((_duration(db, m) or 8000) / 1000)) for m in mine) + 5
        plan["chapters"].append(
            {
                "contributorId": owner,
                "segments": segments,
                "playbackMomentIds": [m.id for m in mine if m.kind in ("photo", "clip", "voice")],
                "objectType": OBJECT_FOR_KIND[mine[0].kind],
                "durationSeconds": max(15, min(40, seconds)),
            }
        )
    return plan


def validate_plan(db: Session, recap: Recap, plan: dict) -> None:
    """Server-side validation of references, ownership, grants, consent, versions, durations and layout."""
    room = db.get(Room, recap.room_id)
    if room is None:
        raise PlanInvalid("room_missing")
    if plan.get("schemaVersion") != 2:
        raise PlanInvalid("schema_version")
    if plan.get("roomId") != recap.room_id or plan.get("audienceSnapshotId") != recap.audience_snapshot_id:
        raise PlanInvalid("audience_mismatch")
    if plan.get("roomDate") != recap.room_date.isoformat() or plan.get("roomTimeZone") != room.reporting_zone:
        raise PlanInvalid("date_mismatch")
    title = plan.get("title")
    if not isinstance(title, str) or not 1 <= len(title) <= 80:
        raise PlanInvalid("bad_title")
    audience = _audience(db, recap)
    sources = {m.id: (s, m) for s, m in _sources(db, recap)}
    contributors = {m.owner_id for _, m in sources.values()}
    chapters = plan.get("chapters")
    if not isinstance(chapters, list) or not chapters:
        raise PlanInvalid("no_chapters")
    seen_contributors = set()
    total = 0
    for ch in chapters:
        cid = ch.get("contributorId")
        if cid not in contributors or cid in seen_contributors:
            raise PlanInvalid("bad_contributor")
        seen_contributors.add(cid)
        if ch.get("objectType") not in ai.OBJECT_TYPES:
            raise PlanInvalid("bad_layout")
        duration = ch.get("durationSeconds")
        if not isinstance(duration, int) or not 5 <= duration <= 60:
            raise PlanInvalid("bad_duration")
        total += duration
        segments = ch.get("segments")
        if not isinstance(segments, list) or not segments:
            raise PlanInvalid("no_segments")
        for seg in segments:
            text = seg.get("text")
            if not isinstance(text, str) or not 1 <= len(text) <= 400:
                raise PlanInvalid("bad_text")
            if seg.get("kind") not in ("stated", "visual_interpretation", "quote"):
                raise PlanInvalid("bad_segment_kind")
            refs = seg.get("sources")
            if not isinstance(refs, list) or not refs:
                raise PlanInvalid("unsourced_segment")
            for ref in refs:
                pair = sources.get(ref.get("momentId"))
                if pair is None:
                    raise PlanInvalid("unknown_source")
                src, moment = pair
                if moment.owner_id != cid:
                    raise PlanInvalid("misattributed_source")
                if ref.get("sourceVersion") != moment.source_version or ref.get("sharingVersion") != moment.sharing_version:
                    raise PlanInvalid("stale_version")
                if src.source_version != moment.source_version or src.sharing_version != moment.sharing_version:
                    raise PlanInvalid("stale_version")
                if not _moment_eligible(db, moment, audience):
                    raise PlanInvalid("source_not_authorized")
                start, end = ref.get("transcriptStartMs"), ref.get("transcriptEndMs")
                if start is not None or end is not None:
                    dur = _duration(db, moment) or 0
                    if not moment.transcript_consent or start is None or end is None or not 0 <= start < end <= dur:
                        raise PlanInvalid("bad_transcript_range")
        for mid in ch.get("playbackMomentIds") or []:
            pair = sources.get(mid)
            if pair is None or pair[1].owner_id != cid:
                raise PlanInvalid("bad_playback_source")
    if seen_contributors != contributors:
        raise PlanInvalid("missing_contributor_chapter")
    if not 15 <= total <= 180:
        raise PlanInvalid("bad_total_duration")


# ---------------------------------------------------------------------------
# Review, approval, publication
# ---------------------------------------------------------------------------


def _fingerprint(db: Session, recap: Recap, contributor_id: str) -> str:
    mine = sorted((m.id, m.source_version, m.sharing_version) for s, m in _sources(db, recap) if m.owner_id == contributor_id)
    material = {"snapshot": recap.audience_snapshot_id, "sources": mine, "plan": recap.plan}
    return hashlib.sha256(json.dumps(material, sort_keys=True, default=str).encode()).hexdigest()


def approve(db: Session, actor: User, recap_id: str) -> Recap:
    recap = db.get(Recap, recap_id)
    if recap is None or not can_view_recap(db, recap, actor.id):
        raise not_found("recap")
    if recap.state not in ("private_preview", "awaiting_approval"):
        raise conflict("not_reviewable", "This recap isn't waiting for review.")
    reason = sources_still_valid(db, recap)
    if reason:
        invalidate(db, recap, reason)
        db.commit()
        raise conflict("recap_invalidated", "Something in this recap changed. Generate a fresh one.")
    fingerprint = _fingerprint(db, recap, actor.id)
    existing = db.scalar(select(RecapApproval).where(RecapApproval.recap_id == recap.id, RecapApproval.contributor_id == actor.id))
    if existing is None:
        db.add(RecapApproval(recap_id=recap.id, contributor_id=actor.id, audience_snapshot_id=recap.audience_snapshot_id, source_fingerprint=fingerprint))
    else:
        existing.source_fingerprint, existing.audience_snapshot_id = fingerprint, recap.audience_snapshot_id
    recap.state = "awaiting_approval"
    db.flush()
    if all_approved(db, recap):
        publish(db, recap, actor)
    db.commit()
    return recap


def all_approved(db: Session, recap: Recap) -> bool:
    approvals = {a.contributor_id: a for a in db.scalars(select(RecapApproval).where(RecapApproval.recap_id == recap.id))}
    for cid in recap_contributor_ids(db, recap.id):
        a = approvals.get(cid)
        if a is None or a.audience_snapshot_id != recap.audience_snapshot_id or a.source_fingerprint != _fingerprint(db, recap, cid):
            return False
    return True


def publish(db: Session, recap: Recap, actor: User) -> None:
    reason = sources_still_valid(db, recap)
    if reason:
        invalidate(db, recap, reason)
        raise conflict("recap_invalidated", "Something in this recap changed. Generate a fresh one.")
    if recap.plan is None:
        raise conflict("no_plan", "This recap has no approved content.")
    validate_plan(db, recap, recap.plan)
    if not all_approved(db, recap):
        raise conflict("approval_missing", "Every included contributor needs to approve their section first.")
    recap.state = "shared"
    recap.published_at = utcnow()
    snapshot = db.get(AudienceSnapshot, recap.audience_snapshot_id)
    contributors = recap_contributor_ids(db, recap.id)
    awards.record_capsule_published(db, recap, contributors, bool(snapshot and snapshot.room_wide))
    for uid in _audience(db, recap) - {actor.id}:
        notifications.queue(db, kind="recap_ready", room_id=recap.room_id, recipient_id=uid, resource_type="recap", resource_id=recap.id)


def get_recap(db: Session, actor: User, recap_id: str) -> Recap:
    recap = db.get(Recap, recap_id)
    if recap is None or not (can_view_recap(db, recap, actor.id) or can_see_recap_status(db, recap, actor.id)):
        raise not_found("recap")
    return recap


def archive(db: Session, actor: User, room_id: str) -> list[Recap]:
    require_member(db, room_id, actor.id)
    rows = db.scalars(select(Recap).where(Recap.room_id == room_id, Recap.state == "shared").order_by(Recap.room_date.desc(), Recap.version.desc())).all()
    return [r for r in rows if can_view_recap(db, r, actor.id)]


def pending_reviews(db: Session, actor: User, room_id: str) -> list[Recap]:
    require_member(db, room_id, actor.id)
    rows = db.scalars(select(Recap).where(Recap.room_id == room_id, Recap.state.in_(ACTIVE_STATES)).order_by(Recap.created_at.desc())).all()
    return [r for r in rows if can_see_recap_status(db, r, actor.id)]


def schedule_daily_drafts(db: Session, now: datetime | None = None) -> int:
    """Worker task: at each room's optional draft time (room zone), draft a room-wide recap for review."""
    now = now or utcnow()
    count = 0
    for room in db.scalars(select(Room).where(Room.daily_draft_time.is_not(None))):
        local = now.astimezone(ZoneInfo(room.reporting_zone))
        hh, mm = (int(x) for x in room.daily_draft_time.split(":"))
        if (local.hour, local.minute) < (hh, mm):
            continue
        job = jobs.enqueue(db, "recap.daily", f"daily:{room.id}:{local.date().isoformat()}", {"room_id": room.id, "room_date": local.date().isoformat()})
        if job.attempts == 0:
            count += 1
    db.commit()
    return count


def run_daily_draft(db: Session, room_id: str, room_date: date) -> str:
    room = db.get(Room, room_id)
    if room is None:
        return "skipped"
    owner = db.get(User, room.owner_id)
    if owner is None:
        return "skipped"
    try:
        request_recap(db, owner, room_id, room_date, None)
    except Exception as exc:  # noqa: BLE001 - "nothing shared" is an honest empty state, not a failure
        return f"no_draft:{getattr(exc, 'code', 'error')}"
    return "drafted"
