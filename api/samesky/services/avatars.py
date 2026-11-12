"""Photo-derived avatars: consent, private source photos, generation jobs, owner approval, sharing, deletion.

Ownership rules (every function re-checks them; routes only translate errors):
- Each person manages their own likeness. Nobody can create, approve, view privately, or delete another
  person's sources, jobs or assets.
- Source photos are private: never moments, recap sources, notification images or room media.
- Only an `active` (owner-approved) asset is shown to others, and only to people who currently share a
  room with the owner within the chosen scope (`rooms`, `direct` rooms only, or `private`).
- A late provider result can never activate anything: completion re-checks the job's state and consent,
  and only the owner's explicit approval activates an asset.
"""

from __future__ import annotations

import hashlib
import io
import logging
import shutil
from datetime import datetime, timedelta
from pathlib import Path

from sqlalchemy import select, update
from sqlalchemy.orm import Session

from ..config import get_settings
from ..errors import ApiError, invalid, not_found
from ..models import (
    Avatar,
    AvatarAsset,
    AvatarConsent,
    AvatarJob,
    AvatarSource,
    Membership,
    Room,
    User,
    utcnow,
)
from . import glb, jobs
from .avatar_providers import ProviderError, get_provider
from .media import sniff

log = logging.getLogger("samesky.avatars")

CONSENT_TEXT_VERSION = "2026-11-08"
PROVIDERS = ("metaperson", "manual_import")
SCOPES = ("rooms", "direct", "private")
BODIES = ("female", "male")  # the provider's body models; chosen by the person, never inferred
ACTIVE_JOB_STATES = ("validated", "queued", "processing", "validating_asset")
POLL_SECONDS = 5
SOURCE_MAX_BYTES = 15 * 1024 * 1024
SOURCE_MIN_SIDE = 512
SOURCE_MAX_SIDE = 8000


# ---------------------------------------------------------------------------
# Storage (private, outside every static path)
# ---------------------------------------------------------------------------


def private_dir(owner_id: str, *parts: str) -> Path:
    path = get_settings().media_root / "avatar-private" / owner_id
    for p in parts:
        path = path / p
    path.mkdir(parents=True, exist_ok=True)
    return path


def _delete_file(path: str | None) -> None:
    if path:
        try:
            Path(path).unlink(missing_ok=True)
        except OSError:
            log.warning("avatar file delete failed")


# ---------------------------------------------------------------------------
# Consent
# ---------------------------------------------------------------------------


def record_consent(db: Session, user: User, provider: str, processing: bool, likeness_scope: str) -> AvatarConsent:
    if provider not in PROVIDERS:
        raise invalid("invalid_provider", "Choose how the avatar will be made.")
    if likeness_scope not in SCOPES:
        raise invalid("invalid_scope", "Choose who can see your avatar.")
    if provider == "metaperson" and not processing:
        raise invalid("consent_required", "Generating an avatar from photos needs your permission for the avatar service to process them.")
    consent = AvatarConsent(owner_id=user.id, provider=provider, processing=processing, likeness_scope=likeness_scope, text_version=CONSENT_TEXT_VERSION)
    db.add(consent)
    db.commit()
    return consent


def _valid_consent(db: Session, user: User, consent_id: str, provider: str) -> AvatarConsent:
    consent = db.get(AvatarConsent, consent_id)
    if consent is None or consent.owner_id != user.id:
        raise not_found("consent")
    if consent.withdrawn_at is not None:
        raise invalid("consent_withdrawn", "You withdrew this permission. Start again to give it.")
    if consent.provider != provider:
        raise invalid("consent_mismatch", "This permission was given for a different way of making the avatar.")
    if provider == "metaperson" and not consent.processing:
        raise invalid("consent_required", "Generating needs your permission for the avatar service to process your photos.")
    return consent


def withdraw_consent(db: Session, user: User, consent_id: str) -> None:
    consent = db.get(AvatarConsent, consent_id)
    if consent is None or consent.owner_id != user.id:
        raise not_found("consent")
    consent.withdrawn_at = utcnow()
    for job in db.scalars(select(AvatarJob).where(AvatarJob.consent_id == consent.id, AvatarJob.state.in_(ACTIVE_JOB_STATES))):
        _cancel(db, job, "You withdrew permission, so generation stopped.")
    db.commit()


# ---------------------------------------------------------------------------
# Source photos
# ---------------------------------------------------------------------------


def store_source(db: Session, user: User, data: bytes, view: str, consent_id: str) -> AvatarSource:
    consent = db.get(AvatarConsent, consent_id)
    if consent is None or consent.owner_id != user.id:
        raise not_found("consent")
    _valid_consent(db, user, consent_id, consent.provider)
    if view not in ("front", "left", "right"):
        raise invalid("invalid_view", "Photos are front, left or right views.")
    if len(data) > SOURCE_MAX_BYTES:
        raise invalid("too_large", "Photos can be up to 15 MB.")
    detected = sniff(data[:16])
    if detected is None or detected[0] != "image":
        raise invalid("unsupported_format", "Use a JPEG or PNG photo. (HEIC: export it as JPEG first.)")
    from PIL import Image, ImageOps

    Image.MAX_IMAGE_PIXELS = SOURCE_MAX_SIDE * SOURCE_MAX_SIDE
    try:
        img = Image.open(io.BytesIO(data))
        img.load()
    except Exception:  # noqa: BLE001 - undecodable or a decompression bomb
        raise invalid("unreadable_image", "This photo couldn't be read. Take or export it again.") from None
    img = ImageOps.exif_transpose(img).convert("RGB")
    w, h = img.size
    if min(w, h) < SOURCE_MIN_SIDE:
        raise invalid("too_small", f"This photo is {w}×{h}. Use one at least {SOURCE_MIN_SIDE} pixels on its shorter side so your face has enough detail.")
    if max(w, h) > SOURCE_MAX_SIDE:
        raise invalid("too_large", "This photo is too large. Use one under 8000 pixels on its longer side.")
    img.thumbnail((2048, 2048))
    out = io.BytesIO()
    img.save(out, "JPEG", quality=92)  # re-encoded: no EXIF, GPS or maker notes carried over
    clean = out.getvalue()
    settings = get_settings()
    source = AvatarSource(
        owner_id=user.id,
        consent_id=consent_id,
        view=view,
        mime="image/jpeg",
        width=img.size[0],
        height=img.size[1],
        size_bytes=len(clean),
        content_hash=hashlib.sha256(clean).hexdigest(),
        retention_until=utcnow() + timedelta(days=settings.avatar_source_retention_days),
    )
    db.add(source)
    db.flush()
    path = private_dir(user.id, "sources") / f"{source.id}.jpg"
    path.write_bytes(clean)
    source.blob_path = str(path)
    db.commit()
    return source


def get_source(db: Session, user_id: str, source_id: str) -> AvatarSource:
    source = db.get(AvatarSource, source_id)
    if source is None or source.owner_id != user_id or source.deletion_status != "kept":
        raise not_found("photo")
    return source


def delete_source(db: Session, user: User, source_id: str) -> AvatarSource:
    source = db.get(AvatarSource, source_id)
    if source is None or source.owner_id != user.id:
        raise not_found("photo")
    _delete_file(source.blob_path)
    source.blob_path = None
    source.deletion_status = "deleted"
    source.deleted_at = utcnow()
    db.commit()
    return source


def purge_expired_sources(db: Session, now: datetime | None = None) -> int:
    now = now or utcnow()
    count = 0
    for source in db.scalars(select(AvatarSource).where(AvatarSource.deletion_status == "kept", AvatarSource.retention_until <= now)):
        _delete_file(source.blob_path)
        source.blob_path = None
        source.deletion_status = "deleted"
        source.deleted_at = now
        count += 1
    db.commit()
    return count


# ---------------------------------------------------------------------------
# Generation jobs
# ---------------------------------------------------------------------------


def provider_info() -> dict:
    s = get_settings()
    provider = get_provider(s.avatar_provider)
    return {
        "name": s.avatar_provider,
        "label": "Avatar SDK MetaPerson (itSeez3D)" if s.avatar_provider == "metaperson" else s.avatar_provider,
        "configured": bool(provider and provider.configured()),
        "photoViews": ["front"],
        "bodies": list(BODIES),
        "retentionDays": s.avatar_source_retention_days,
    }


def start_job(db: Session, user: User, *, source_ids: list[str], consent_id: str, options: dict, idempotency_key: str) -> AvatarJob:
    if not idempotency_key or len(idempotency_key) > 80:
        raise invalid("invalid_key", "Missing request key.")
    existing = db.scalar(select(AvatarJob).where(AvatarJob.owner_id == user.id, AvatarJob.idempotency_key == idempotency_key))
    if existing is not None:
        return existing
    settings = get_settings()
    provider = get_provider(settings.avatar_provider)
    if provider is None or not provider.configured():
        raise ApiError(
            503,
            "provider_unavailable",
            "Photo-to-avatar generation isn't set up on this server yet (no avatar-service credentials). "
            "You can import a model you made elsewhere, or use a demo avatar for now.",
        )
    _valid_consent(db, user, consent_id, provider.name)
    sources = [db.get(AvatarSource, sid) for sid in source_ids]
    if not sources or any(s is None or s.owner_id != user.id or s.deletion_status != "kept" for s in sources):
        raise invalid("invalid_sources", "Add a front-facing photo first.")
    if not any(s.view == "front" for s in sources):
        raise invalid("front_required", "A sharp, front-facing photo is required.")
    body = options.get("body")
    if body not in BODIES:
        raise invalid("body_required", "Choose one of the avatar service's body models. We never infer it from your photo.")
    outfit = options.get("outfit")
    if outfit is not None and (not isinstance(outfit, str) or len(outfit) > 40):
        raise invalid("invalid_outfit", "Unknown outfit.")
    clean_options = {"body": body, "outfit": outfit, "remove_glasses": bool(options.get("remove_glasses", True))}
    job = AvatarJob(
        owner_id=user.id,
        consent_id=consent_id,
        provider=provider.name,
        source_ids=[s.id for s in sources],
        options=clean_options,
        idempotency_key=idempotency_key,
        state="validated",
    )
    db.add(job)
    db.flush()
    jobs.enqueue(db, "avatar.generate", f"avatar:{job.id}:submit", {"job_id": job.id}, max_attempts=4)
    job.state = "queued"
    job.updated_at = utcnow()
    db.commit()
    return job


def get_job(db: Session, user_id: str, job_id: str) -> AvatarJob:
    job = db.get(AvatarJob, job_id)
    if job is None or job.owner_id != user_id:
        raise not_found("avatar job")
    return job


def _schedule_cleanup(db: Session, job: AvatarJob) -> None:
    if job.provider_ref and job.provider_cleanup not in ("done", "pending"):
        job.provider_cleanup = "pending"
        jobs.enqueue(db, "avatar.provider_cleanup", f"avatar:{job.id}:cleanup", {"job_id": job.id}, max_attempts=6)
    elif not job.provider_ref and job.provider_cleanup is None:
        job.provider_cleanup = "not_needed"


def _cancel(db: Session, job: AvatarJob, reason: str) -> None:
    job.state = "cancelled"
    job.safe_error = reason[:240]
    job.completed_at = utcnow()
    job.updated_at = utcnow()
    jobs.cancel_matching(db, "avatar.generate", f"avatar:{job.id}:", reason)
    _schedule_cleanup(db, job)


def cancel_job(db: Session, user: User, job_id: str) -> AvatarJob:
    job = get_job(db, user.id, job_id)
    if job.state in ACTIVE_JOB_STATES:
        _cancel(db, job, "Cancelled.")
    elif job.state == "awaiting_owner_approval" and job.asset_id:
        reject_asset(db, user, job.asset_id)
        db.refresh(job)
    db.commit()
    return job


def retry_job(db: Session, user: User, job_id: str) -> AvatarJob:
    job = get_job(db, user.id, job_id)
    if job.state not in ("failed", "cancelled"):
        raise invalid("not_retryable", "Only a failed or cancelled generation can be tried again.")
    return start_job(db, user, source_ids=list(job.source_ids), consent_id=job.consent_id, options=dict(job.options), idempotency_key=f"{job.id}:retry:{job.attempt + 1}")


def _fail(db: Session, job: AvatarJob, message: str) -> str:
    job.state = "failed"
    job.safe_error = message[:240]
    job.completed_at = utcnow()
    job.updated_at = utcnow()
    _schedule_cleanup(db, job)
    db.commit()
    return "failed"


def _schedule_poll(db: Session, job: AvatarJob) -> None:
    job.poll_count += 1
    jobs.enqueue(db, "avatar.generate", f"avatar:{job.id}:poll:{job.poll_count}", {"job_id": job.id}, run_after=utcnow() + timedelta(seconds=POLL_SECONDS), max_attempts=4)


def run_generation(db: Session, job_id: str) -> str:
    """Worker step (idempotent): submit once, then poll; on completion validate and stage for owner approval."""
    job = db.get(AvatarJob, job_id)
    if job is None or job.state not in ("queued", "processing"):
        return "skipped"
    owner = db.get(User, job.owner_id)
    consent = db.get(AvatarConsent, job.consent_id)
    if owner is None or consent is None or consent.withdrawn_at is not None:
        _cancel(db, job, "Permission was withdrawn, so generation stopped.")
        db.commit()
        return "cancelled"
    provider = get_provider(job.provider)
    if provider is None or not provider.configured():
        return _fail(db, job, "The avatar service isn't configured on this server any more.")
    settings = get_settings()
    try:
        if not job.provider_ref:
            sources = [db.get(AvatarSource, sid) for sid in job.source_ids]
            front = next((s for s in sources if s is not None and s.view == "front" and s.deletion_status == "kept" and s.blob_path), None)
            if front is None:
                return _fail(db, job, "The photo was deleted before it could be processed.")
            ref = provider.submit(Path(front.blob_path), dict(job.options))
            job.provider_ref = ref
            job.state = "processing"
            job.updated_at = utcnow()
            _schedule_poll(db, job)
            db.commit()
            return "submitted"
        if utcnow() - job.created_at > timedelta(minutes=settings.avatar_generation_timeout_minutes):
            return _fail(db, job, "Generation took too long, so we stopped it. Try again later.")
        status = provider.status(job.provider_ref)
        if status.state in ("queued", "computing"):
            job.progress = status.progress
            job.updated_at = utcnow()
            _schedule_poll(db, job)
            db.commit()
            return "polling"
        if status.state in ("failed", "timed_out"):
            return _fail(db, job, status.message or "The avatar service couldn't finish this avatar.")
        data = provider.fetch_export(job.provider_ref, settings.avatar_max_glb_bytes)
        if data is None:
            _schedule_poll(db, job)  # exported model not ready yet
            db.commit()
            return "exporting"
    except ProviderError as exc:
        if exc.retryable:
            job.safe_error = exc.message[:240]
            _schedule_poll(db, job)
            db.commit()
            return "retry_later"
        return _fail(db, job, exc.message)

    job.state = "validating_asset"
    job.updated_at = utcnow()
    db.commit()
    try:
        inspection = glb.inspect(data, glb.Limits(max_bytes=settings.avatar_max_glb_bytes))
    except glb.GlbRejected as exc:
        return _fail(db, job, f"The returned model didn't pass checks: {exc.message}")
    # Re-check: cancelled or permission withdrawn while we were downloading? Then nothing is staged.
    db.refresh(job)
    db.refresh(consent)
    if job.state != "validating_asset" or consent.withdrawn_at is not None:
        return "discarded"
    asset = _create_asset(db, owner, data, inspection, provider=job.provider, job=job, provider_model_version=f"{provider.name}:{job.options.get('body')}")
    job.state = "awaiting_owner_approval"
    job.asset_id = asset.id
    job.progress = 100
    job.updated_at = utcnow()
    db.commit()
    return "awaiting_owner_approval"


def run_provider_cleanup(db: Session, job_id: str) -> str:
    job = db.get(AvatarJob, job_id)
    if job is None or not job.provider_ref:
        return "skipped"
    provider = get_provider(job.provider)
    if provider is None or not provider.configured():
        job.provider_cleanup = "failed"
        db.commit()
        return "not_configured"
    try:
        provider.delete(job.provider_ref)
    except ProviderError as exc:
        if exc.retryable:
            raise RuntimeError("retry") from exc  # the outbox retries with backoff
        job.provider_cleanup = "failed"
        db.commit()
        return "failed"
    job.provider_cleanup = "done"
    db.commit()
    return "done"


# ---------------------------------------------------------------------------
# Assets: import, approval, sharing, deletion
# ---------------------------------------------------------------------------


def _create_asset(db: Session, owner: User, data: bytes, inspection: glb.Inspection, *, provider: str, job: AvatarJob | None, provider_model_version: str | None) -> AvatarAsset:
    count = len(db.scalars(select(AvatarAsset.id).where(AvatarAsset.owner_id == owner.id)).all())
    asset = AvatarAsset(
        owner_id=owner.id,
        source_kind="photo_derived",
        provider=provider,
        provider_model_version=provider_model_version,
        job_id=job.id if job else None,
        version=count + 1,
        content_hash=inspection.content_hash,
        size_bytes=inspection.size_bytes,
        rig=inspection.rig,
        state="awaiting_owner_approval",
    )
    db.add(asset)
    db.flush()
    path = private_dir(owner.id, "assets") / f"{asset.id}.glb"
    path.write_bytes(data)
    asset.glb_path = str(path)
    db.flush()
    return asset


def import_asset(db: Session, user: User, data: bytes, consent_id: str, attested: bool) -> AvatarAsset:
    if not attested:
        raise invalid("attestation_required", "Confirm this model was made from photos of you, with your consent.")
    _valid_consent(db, user, consent_id, "manual_import")
    try:
        inspection = glb.inspect(data, glb.Limits(max_bytes=get_settings().avatar_max_glb_bytes))
    except glb.GlbRejected as exc:
        raise invalid(f"model_{exc.code}", exc.message) from None
    asset = _create_asset(db, user, data, inspection, provider="manual_import", job=None, provider_model_version=inspection.rig.get("generator") or None)
    db.commit()
    return asset


def get_owned_asset(db: Session, user_id: str, asset_id: str) -> AvatarAsset:
    asset = db.get(AvatarAsset, asset_id)
    if asset is None or asset.owner_id != user_id or asset.state in ("deleted",):
        raise not_found("avatar")
    return asset


def _profile(db: Session, user_id: str) -> Avatar:
    avatar = db.get(Avatar, user_id)
    if avatar is None:
        avatar = Avatar(user_id=user_id, definition={}, version=1)
        db.add(avatar)
        db.flush()
    return avatar


def approve_asset(db: Session, user: User, asset_id: str, visibility: str, confirmed: bool) -> AvatarAsset:
    if not confirmed:
        raise invalid("confirmation_required", "Confirm that this looks like you before using it.")
    if visibility not in SCOPES:
        raise invalid("invalid_scope", "Choose who can see your avatar.")
    asset = get_owned_asset(db, user.id, asset_id)
    if asset.state != "awaiting_owner_approval":
        raise ApiError(409, "not_awaiting_approval", "This avatar isn't waiting for your approval.")
    if asset.job_id:
        job = db.get(AvatarJob, asset.job_id)
        consent = db.get(AvatarConsent, job.consent_id) if job else None
        if job is None or job.state not in ("awaiting_owner_approval",) or consent is None or consent.withdrawn_at is not None:
            raise ApiError(409, "not_awaiting_approval", "This generation was cancelled, so it can't be used.")
    now = utcnow()
    # Compare-and-set: only one approval can win, and only from the awaiting state.
    result = db.execute(
        update(AvatarAsset)
        .where(AvatarAsset.id == asset.id, AvatarAsset.state == "awaiting_owner_approval")
        .values(state="active", approved_at=now, visibility=visibility)
    )
    if result.rowcount != 1:
        db.rollback()
        raise ApiError(409, "not_awaiting_approval", "This avatar isn't waiting for your approval.")
    profile = _profile(db, user.id)
    if profile.active_asset_id and profile.active_asset_id != asset.id:
        previous = db.get(AvatarAsset, profile.active_asset_id)
        if previous is not None and previous.state == "active":
            previous.state = "superseded"
    profile.active_asset_id = asset.id
    profile.version += 1
    profile.updated_at = now
    if asset.job_id:
        job = db.get(AvatarJob, asset.job_id)
        if job is not None:
            job.state = "completed"
            job.completed_at = now
    db.commit()
    db.refresh(asset)
    return asset


def reject_asset(db: Session, user: User, asset_id: str) -> AvatarAsset:
    asset = get_owned_asset(db, user.id, asset_id)
    if asset.state != "awaiting_owner_approval":
        raise ApiError(409, "not_awaiting_approval", "Only an avatar waiting for approval can be rejected; delete an active one instead.")
    asset.state = "rejected"
    _delete_file(asset.glb_path)
    asset.glb_path = None
    if asset.job_id:
        job = db.get(AvatarJob, asset.job_id)
        if job is not None and job.state == "awaiting_owner_approval":
            job.state = "cancelled"
            job.safe_error = "You rejected this result."
            job.completed_at = utcnow()
            _schedule_cleanup(db, job)
    db.commit()
    return asset


def set_visibility(db: Session, user: User, asset_id: str, visibility: str) -> AvatarAsset:
    if visibility not in SCOPES:
        raise invalid("invalid_scope", "Choose who can see your avatar.")
    asset = get_owned_asset(db, user.id, asset_id)
    if asset.state != "active":
        raise ApiError(409, "not_active", "Only your active avatar has a sharing setting.")
    asset.visibility = visibility
    asset.revocation_version += 1
    profile = _profile(db, user.id)
    profile.version += 1
    db.commit()
    return asset


def delete_asset(db: Session, user: User, asset_id: str) -> AvatarAsset:
    asset = get_owned_asset(db, user.id, asset_id)
    now = utcnow()
    profile = _profile(db, user.id)
    if profile.active_asset_id == asset.id:
        profile.active_asset_id = None
        profile.version += 1
        profile.updated_at = now
    asset.state = "deleting"
    asset.revoked_at = now
    asset.revocation_version += 1
    db.flush()
    _delete_file(asset.glb_path)
    asset.glb_path = None
    asset.state = "deleted"
    if asset.job_id:
        job = db.get(AvatarJob, asset.job_id)
        if job is not None:
            _schedule_cleanup(db, job)
    db.commit()
    return asset


def delete_everything(db: Session, user: User) -> dict:
    """Remove every avatar asset and source photo of this person (the 'delete my avatar data' action)."""
    assets = db.scalars(select(AvatarAsset).where(AvatarAsset.owner_id == user.id, AvatarAsset.state != "deleted")).all()
    for asset in assets:
        delete_asset(db, user, asset.id)
    sources = db.scalars(select(AvatarSource).where(AvatarSource.owner_id == user.id, AvatarSource.deletion_status == "kept")).all()
    for source in sources:
        delete_source(db, user, source.id)
    for job in db.scalars(select(AvatarJob).where(AvatarJob.owner_id == user.id, AvatarJob.state.in_(ACTIVE_JOB_STATES))):
        _cancel(db, job, "You deleted your avatar data.")
    db.commit()
    shutil.rmtree(private_dir(user.id), ignore_errors=True)
    return {"assets": len(assets), "sources": len(sources)}


# ---------------------------------------------------------------------------
# Who may see a model
# ---------------------------------------------------------------------------


def shares_room(db: Session, owner_id: str, viewer_id: str, *, direct_only: bool = False) -> bool:
    mine = select(Membership.room_id).where(Membership.user_id == owner_id, Membership.status == "active")
    query = (
        select(Membership.room_id)
        .join(Room, Room.id == Membership.room_id)
        .where(Membership.user_id == viewer_id, Membership.status == "active", Membership.room_id.in_(mine))
    )
    if direct_only:
        query = query.where(Room.kind == "direct")
    return db.scalar(query.limit(1)) is not None


def can_view_asset(db: Session, asset: AvatarAsset, viewer_id: str) -> bool:
    if asset.state == "deleted" or not asset.glb_path:
        return False
    if asset.owner_id == viewer_id:
        return True
    if asset.state != "active" or asset.visibility == "private":
        return False
    return shares_room(db, asset.owner_id, viewer_id, direct_only=asset.visibility == "direct")
