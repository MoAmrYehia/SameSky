"""Photo-derived avatar API (SameSky contracts, not provider endpoints). Everything under /me is owner-only.

    GET    /v1/me/avatar-studio                  everything the owner needs to manage their avatar
    POST   /v1/me/avatar-consents                record processing + likeness-sharing choices
    POST   /v1/me/avatar-consents/{id}/withdraw  stop any generation that relies on it
    POST   /v1/me/avatar-sources                 upload a private source photo (multipart)
    DELETE /v1/me/avatar-sources/{id}            delete a source photo (independent of any model)
    POST   /v1/me/avatar-jobs                    start an idempotent generation job
    GET    /v1/me/avatar-jobs/{id}               job state, progress, safe error
    POST   /v1/me/avatar-jobs/{id}/cancel        cancel; a late provider result can't activate anything
    POST   /v1/me/avatar-jobs/{id}/retry         try a failed or cancelled job again
    POST   /v1/me/avatar-assets                  import a GLB made elsewhere (multipart; validated)
    POST   /v1/me/avatar-assets/{id}/approve     "This looks like me": activate this exact version
    POST   /v1/me/avatar-assets/{id}/reject      discard a result
    POST   /v1/me/avatar-assets/{id}/activate    use a previously approved version again
    PATCH  /v1/me/avatar-assets/{id}             change who can see it (rooms | direct | private)
    DELETE /v1/me/avatar-assets/{id}             delete a model (and the provider record, where supported)
    POST   /v1/me/avatar/demo                    use the labelled demo avatar instead
    DELETE /v1/me/avatar-data                    delete every avatar model and source photo
    GET    /v1/users/{id}/avatar                 that person's avatar as this viewer may see it
    GET    /v1/avatar-assets/{id}/model          signed, viewer-bound, re-authorized model download
    GET    /v1/avatar-sources/{id}/image         signed, owner-only source photo
"""

from __future__ import annotations

from fastapi import APIRouter, Depends, File, Form, UploadFile
from fastapi.responses import FileResponse
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session

from .. import serializers
from ..db import get_db
from ..deps import current_user
from ..errors import ApiError, invalid, not_found
from ..models import Avatar, AvatarAsset, AvatarConsent, AvatarJob, AvatarSource, User, utcnow
from ..realtime import hub
from ..security import sign_resource_url, verify_media_signature
from ..services import avatars

router = APIRouter(prefix="/v1")


# ---------------------------------------------------------------------------
# Shapes
# ---------------------------------------------------------------------------


def source_out(src: AvatarSource) -> dict:
    return {
        "id": src.id,
        "view": src.view,
        "width": src.width,
        "height": src.height,
        "sizeBytes": src.size_bytes,
        "retentionUntil": src.retention_until.isoformat(),
        "deletionStatus": src.deletion_status,
        "imageUrl": sign_resource_url("/v1/avatar-sources", src.id, "image", src.owner_id) if src.deletion_status == "kept" else None,
        "createdAt": src.created_at.isoformat(),
    }


def job_out(job: AvatarJob) -> dict:
    return {
        "id": job.id,
        "state": job.state,
        "progress": job.progress,
        "safeError": job.safe_error,
        "provider": job.provider,
        "options": job.options,
        "sourceIds": job.source_ids,
        "assetId": job.asset_id,
        "attempt": job.attempt,
        "providerCleanup": job.provider_cleanup,
        "createdAt": job.created_at.isoformat(),
        "updatedAt": job.updated_at.isoformat(),
        "completedAt": job.completed_at.isoformat() if job.completed_at else None,
    }


def asset_out(asset: AvatarAsset) -> dict:
    return {
        "id": asset.id,
        "state": asset.state,
        "version": asset.version,
        "provider": asset.provider,
        "sourceKind": asset.source_kind,
        "visibility": asset.visibility,
        "jobId": asset.job_id,
        "approvedAt": asset.approved_at.isoformat() if asset.approved_at else None,
        "rig": asset.rig,
        "sizeBytes": asset.size_bytes,
        "contentHash": asset.content_hash,
        "modelUrl": serializers.avatar_url(asset.id, "model", asset.owner_id) if asset.glb_path else None,
        "createdAt": asset.created_at.isoformat(),
    }


def consent_out(c: AvatarConsent) -> dict:
    return {
        "id": c.id,
        "provider": c.provider,
        "processing": c.processing,
        "likenessScope": c.likeness_scope,
        "textVersion": c.text_version,
        "createdAt": c.created_at.isoformat(),
        "withdrawnAt": c.withdrawn_at.isoformat() if c.withdrawn_at else None,
    }


# ---------------------------------------------------------------------------
# Owner studio
# ---------------------------------------------------------------------------


@router.get("/me/avatar-studio")
def studio(user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    profile = db.get(Avatar, user.id)
    assets = db.scalars(select(AvatarAsset).where(AvatarAsset.owner_id == user.id, AvatarAsset.state.not_in(("deleted", "rejected"))).order_by(AvatarAsset.created_at.desc())).all()
    jobs_ = db.scalars(select(AvatarJob).where(AvatarJob.owner_id == user.id).order_by(AvatarJob.created_at.desc()).limit(10)).all()
    sources = db.scalars(select(AvatarSource).where(AvatarSource.owner_id == user.id, AvatarSource.deletion_status == "kept").order_by(AvatarSource.created_at.desc())).all()
    consents = db.scalars(select(AvatarConsent).where(AvatarConsent.owner_id == user.id, AvatarConsent.withdrawn_at.is_(None)).order_by(AvatarConsent.created_at.desc())).all()
    return {
        "provider": avatars.provider_info(),
        "consentTextVersion": avatars.CONSENT_TEXT_VERSION,
        "active": serializers.avatar_view(db, user.id, profile, user.id),
        "activeAssetId": profile.active_asset_id if profile else None,
        "assets": [asset_out(a) for a in assets],
        "jobs": [job_out(j) for j in jobs_],
        "sources": [source_out(s) for s in sources],
        "consents": [consent_out(c) for c in consents],
    }


class ConsentIn(BaseModel):
    provider: str
    processing: bool = False
    likeness_scope: str = "rooms"


@router.post("/me/avatar-consents")
def create_consent(body: ConsentIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return consent_out(avatars.record_consent(db, user, body.provider, body.processing, body.likeness_scope))


@router.post("/me/avatar-consents/{consent_id}/withdraw")
def withdraw(consent_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    avatars.withdraw_consent(db, user, consent_id)
    hub.avatar_changed(user.id)
    return {"ok": True}


@router.post("/me/avatar-sources")
async def upload_source(
    file: UploadFile = File(...),
    view: str = Form("front"),
    consent_id: str = Form(...),
    user: User = Depends(current_user),
    db: Session = Depends(get_db),
) -> dict:
    data = await file.read(avatars.SOURCE_MAX_BYTES + 1)
    return source_out(avatars.store_source(db, user, data, view, consent_id))


@router.delete("/me/avatar-sources/{source_id}")
def remove_source(source_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return source_out(avatars.delete_source(db, user, source_id))


class JobIn(BaseModel):
    source_ids: list[str] = Field(min_length=1, max_length=3)
    consent_id: str
    options: dict = Field(default_factory=dict)
    idempotency_key: str = Field(min_length=6, max_length=80)


@router.post("/me/avatar-jobs")
def create_job(body: JobIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return job_out(avatars.start_job(db, user, source_ids=body.source_ids, consent_id=body.consent_id, options=body.options, idempotency_key=body.idempotency_key))


@router.get("/me/avatar-jobs/{job_id}")
def read_job(job_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return job_out(avatars.get_job(db, user.id, job_id))


@router.post("/me/avatar-jobs/{job_id}/cancel")
def cancel(job_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return job_out(avatars.cancel_job(db, user, job_id))


@router.post("/me/avatar-jobs/{job_id}/retry")
def retry(job_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return job_out(avatars.retry_job(db, user, job_id))


@router.post("/me/avatar-assets")
async def import_model(
    file: UploadFile = File(...),
    consent_id: str = Form(...),
    attested: bool = Form(False),
    user: User = Depends(current_user),
    db: Session = Depends(get_db),
) -> dict:
    from ..config import get_settings

    limit = get_settings().avatar_max_glb_bytes
    data = await file.read(limit + 1)
    if len(data) > limit:
        raise invalid("model_too_large", f"Models can be up to {limit // (1024 * 1024)} MB.")
    return asset_out(avatars.import_asset(db, user, data, consent_id, attested))


class ApproveIn(BaseModel):
    visibility: str = "rooms"
    confirmed: bool = False


@router.post("/me/avatar-assets/{asset_id}/approve")
def approve(asset_id: str, body: ApproveIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = avatars.approve_asset(db, user, asset_id, body.visibility, body.confirmed)
    hub.avatar_changed(user.id)
    return asset_out(asset)


@router.post("/me/avatar-assets/{asset_id}/reject")
def reject(asset_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    return asset_out(avatars.reject_asset(db, user, asset_id))


@router.post("/me/avatar-assets/{asset_id}/activate")
def activate(asset_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = avatars.get_owned_asset(db, user.id, asset_id)
    if asset.state != "superseded" or asset.approved_at is None or not asset.glb_path:
        raise ApiError(409, "not_reusable", "Only a model you approved before can be used again.")
    profile = avatars._profile(db, user.id)
    if profile.active_asset_id:
        current = db.get(AvatarAsset, profile.active_asset_id)
        if current is not None and current.state == "active":
            current.state = "superseded"
    asset.state = "active"
    profile.active_asset_id = asset.id
    profile.version += 1
    profile.updated_at = utcnow()
    db.commit()
    hub.avatar_changed(user.id)
    return asset_out(asset)


class VisibilityIn(BaseModel):
    visibility: str


@router.patch("/me/avatar-assets/{asset_id}")
def change_visibility(asset_id: str, body: VisibilityIn, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = avatars.set_visibility(db, user, asset_id, body.visibility)
    hub.avatar_changed(user.id)
    return asset_out(asset)


@router.delete("/me/avatar-assets/{asset_id}")
def delete_model(asset_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    asset = avatars.delete_asset(db, user, asset_id)
    hub.avatar_changed(user.id)
    return asset_out(asset)


@router.post("/me/avatar/demo")
def use_demo(user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    profile = avatars._profile(db, user.id)
    if profile.active_asset_id:
        current = db.get(AvatarAsset, profile.active_asset_id)
        if current is not None and current.state == "active":
            current.state = "superseded"
        profile.active_asset_id = None
        profile.version += 1
        profile.updated_at = utcnow()
    db.commit()
    hub.avatar_changed(user.id)
    return serializers.avatar_view(db, user.id, profile, user.id) or {}


@router.delete("/me/avatar-data")
def delete_all(user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    result = avatars.delete_everything(db, user)
    hub.avatar_changed(user.id)
    return result


# ---------------------------------------------------------------------------
# Viewing other people's avatars
# ---------------------------------------------------------------------------


@router.get("/users/{user_id}/avatar")
def user_avatar(user_id: str, user: User = Depends(current_user), db: Session = Depends(get_db)) -> dict:
    if user_id != user.id and not avatars.shares_room(db, user_id, user.id):
        raise not_found("person")
    owner = db.get(User, user_id)
    if owner is None:
        raise not_found("person")
    return {"userId": owner.id, "displayName": owner.display_name, "avatar": serializers.avatar_view(db, owner.id, db.get(Avatar, owner.id), user.id)}


@router.get("/avatar-assets/{asset_id}/model")
def serve_model(asset_id: str, u: str, e: int, s: str, db: Session = Depends(get_db)):
    """Short-lived signed link *and* a fresh authorization check: revocation or leaving the room blocks new reads."""
    if not verify_media_signature(asset_id, "model", u, e, s):
        raise ApiError(403, "link_expired", "This avatar link has expired. Refresh to get a new one.")
    asset = db.get(AvatarAsset, asset_id)
    if asset is None or not avatars.can_view_asset(db, asset, u):
        raise not_found("avatar")
    return FileResponse(asset.glb_path, media_type="model/gltf-binary", headers={"Cache-Control": "private, max-age=300", "X-Content-Type-Options": "nosniff"})


@router.get("/avatar-sources/{source_id}/image")
def serve_source(source_id: str, u: str, e: int, s: str, db: Session = Depends(get_db)):
    if not verify_media_signature(source_id, "image", u, e, s):
        raise ApiError(403, "link_expired", "This photo link has expired. Refresh to get a new one.")
    src = avatars.get_source(db, u, source_id)  # owner-only: the signed user must be the owner
    if not src.blob_path:
        raise not_found("photo")
    return FileResponse(src.blob_path, media_type="image/jpeg", headers={"Cache-Control": "private, no-store", "X-Content-Type-Options": "nosniff"})
