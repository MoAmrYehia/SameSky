"""Relational data model. UUID primary keys, UTC instants, IANA zones and local recurrence rules.

Access-control state lives in explicit tables (memberships with generations, audience snapshots and
recipient grants) so every read can be authorized against "explicit grant + current matching
membership generation" rather than room membership alone.
"""

from __future__ import annotations

import uuid
from datetime import UTC, date, datetime

from sqlalchemy import (
    JSON,
    Boolean,
    Date,
    DateTime,
    Float,
    ForeignKey,
    Index,
    Integer,
    String,
    Text,
    TypeDecorator,
    UniqueConstraint,
)
from sqlalchemy.orm import Mapped, mapped_column

from .db import Base


def new_id() -> str:
    return str(uuid.uuid4())


def utcnow() -> datetime:
    return datetime.now(UTC)


class UTCDateTime(TypeDecorator):
    """Stores naive UTC, always returns timezone-aware UTC datetimes."""

    impl = DateTime
    cache_ok = True

    def process_bind_param(self, value, dialect):
        if value is None:
            return None
        if value.tzinfo is None:
            raise ValueError("naive datetimes are not allowed; use UTC-aware values")
        return value.astimezone(UTC).replace(tzinfo=None)

    def process_result_value(self, value, dialect):
        if value is None:
            return None
        return value.replace(tzinfo=UTC)


# ---------------------------------------------------------------------------
# Identity and profiles
# ---------------------------------------------------------------------------


class User(Base):
    __tablename__ = "users"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    email: Mapped[str] = mapped_column(String(320), unique=True)
    password_hash: Mapped[str | None] = mapped_column(String(256), nullable=True)
    display_name: Mapped[str] = mapped_column(String(60))
    time_zone: Mapped[str] = mapped_column(String(64), default="Asia/Dubai")
    quiet_start: Mapped[str | None] = mapped_column(String(5), nullable=True)  # local HH:MM
    quiet_end: Mapped[str | None] = mapped_column(String(5), nullable=True)
    preferred_call_minutes: Mapped[int] = mapped_column(Integer, default=15)
    share_availability_hints: Mapped[bool] = mapped_column(Boolean, default=True)
    notification_prefs: Mapped[dict] = mapped_column(JSON, default=dict)
    call_link: Mapped[str | None] = mapped_column(String(500), nullable=True)
    suggestions_paused_until: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Avatar(Base):
    """A person's profile avatar.

    `definition` is the demo/legacy preset (the sculpted demo kit) and stays readable for every existing
    row. `active_asset_id` points at the owner-approved, photo-derived 3D asset when there is one; it is
    independent of private source photos and of any unapproved generation job. `version` increases on
    every change so viewers' caches (portraits, loaded models) are keyed to the exact active version.
    """

    __tablename__ = "avatars"
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), primary_key=True)
    definition: Mapped[dict] = mapped_column(JSON, default=dict)
    version: Mapped[int] = mapped_column(Integer, default=1)
    likeness_consent: Mapped[bool] = mapped_column(Boolean, default=False)  # legacy flag; see AvatarConsent
    active_asset_id: Mapped[str | None] = mapped_column(ForeignKey("avatar_assets.id", ondelete="SET NULL"), nullable=True)
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class AvatarConsent(Base):
    """Explicit choices recorded before any photo is processed: which service, and who may see the result."""

    __tablename__ = "avatar_consents"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    provider: Mapped[str] = mapped_column(String(24))  # metaperson | manual_import
    processing: Mapped[bool] = mapped_column(Boolean, default=False)  # may this service process my photos
    likeness_scope: Mapped[str] = mapped_column(String(12), default="rooms")  # rooms | direct | private
    text_version: Mapped[str] = mapped_column(String(20))
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    withdrawn_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


class AvatarSource(Base):
    """A private source photo. Never served publicly, never a moment, recap source or notification image."""

    __tablename__ = "avatar_sources"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    consent_id: Mapped[str] = mapped_column(ForeignKey("avatar_consents.id", ondelete="CASCADE"))
    view: Mapped[str] = mapped_column(String(8), default="front")  # front | left | right
    blob_path: Mapped[str | None] = mapped_column(String(400), nullable=True)
    mime: Mapped[str] = mapped_column(String(40), default="image/jpeg")
    width: Mapped[int] = mapped_column(Integer, default=0)
    height: Mapped[int] = mapped_column(Integer, default=0)
    size_bytes: Mapped[int] = mapped_column(Integer, default=0)
    content_hash: Mapped[str] = mapped_column(String(64), default="")
    retention_until: Mapped[datetime] = mapped_column(UTCDateTime)
    deletion_status: Mapped[str] = mapped_column(String(10), default="kept")  # kept | deleted
    deleted_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class AvatarJob(Base):
    """One generation attempt. States: validated → queued → processing → validating_asset →
    awaiting_owner_approval (the asset then waits for the owner), or failed | cancelled | superseded."""

    __tablename__ = "avatar_jobs"
    __table_args__ = (UniqueConstraint("owner_id", "idempotency_key", name="uq_avatar_jobs_owner_key"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    consent_id: Mapped[str] = mapped_column(ForeignKey("avatar_consents.id", ondelete="CASCADE"))
    provider: Mapped[str] = mapped_column(String(24))
    provider_ref: Mapped[str | None] = mapped_column(String(120), nullable=True)
    source_ids: Mapped[list] = mapped_column(JSON, default=list)
    options: Mapped[dict] = mapped_column(JSON, default=dict)
    idempotency_key: Mapped[str] = mapped_column(String(80))
    state: Mapped[str] = mapped_column(String(24), default="validated")
    attempt: Mapped[int] = mapped_column(Integer, default=1)
    progress: Mapped[int] = mapped_column(Integer, default=0)
    safe_error: Mapped[str | None] = mapped_column(String(240), nullable=True)
    asset_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    provider_cleanup: Mapped[str | None] = mapped_column(String(16), nullable=True)  # pending | done | failed | not_needed
    poll_count: Mapped[int] = mapped_column(Integer, default=0)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    completed_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


class AvatarAsset(Base):
    """A validated, privately stored 3D avatar model (GLB) and its rig capabilities.

    States: awaiting_owner_approval → active (or rejected); active → superseded | revoked; then deleting →
    deleted. Only `active` assets are ever served to other people, and only within `visibility`.
    """

    __tablename__ = "avatar_assets"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    source_kind: Mapped[str] = mapped_column(String(16), default="photo_derived")  # photo_derived | preset_demo | legacy
    provider: Mapped[str] = mapped_column(String(24))  # metaperson | manual_import
    provider_model_version: Mapped[str | None] = mapped_column(String(60), nullable=True)
    job_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    version: Mapped[int] = mapped_column(Integer, default=1)
    glb_path: Mapped[str | None] = mapped_column(String(400), nullable=True)
    content_hash: Mapped[str] = mapped_column(String(64), default="")
    size_bytes: Mapped[int] = mapped_column(Integer, default=0)
    rig: Mapped[dict] = mapped_column(JSON, default=dict)
    state: Mapped[str] = mapped_column(String(24), default="awaiting_owner_approval")
    visibility: Mapped[str] = mapped_column(String(12), default="rooms")  # rooms | direct | private
    approved_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    revoked_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    revocation_version: Mapped[int] = mapped_column(Integer, default=0)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Rooms, memberships, invitations
# ---------------------------------------------------------------------------


class Room(Base):
    __tablename__ = "rooms"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    kind: Mapped[str] = mapped_column(String(10))  # direct | circle
    name: Mapped[str] = mapped_column(String(80))
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    capacity: Mapped[int] = mapped_column(Integer)
    reporting_zone: Mapped[str] = mapped_column(String(64), default="Asia/Dubai")
    daily_draft_time: Mapped[str | None] = mapped_column(String(5), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Membership(Base):
    __tablename__ = "memberships"
    __table_args__ = (UniqueConstraint("room_id", "user_id", name="uq_membership_room_user"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    role: Mapped[str] = mapped_column(String(10), default="member")  # owner | member
    status: Mapped[str] = mapped_column(String(10), default="active")  # active | removed | left
    label: Mapped[str | None] = mapped_column(String(40), nullable=True)  # relationship-neutral label
    generation: Mapped[int] = mapped_column(Integer, default=1)
    muted: Mapped[bool] = mapped_column(Boolean, default=False)
    joined_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    removed_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


class Invitation(Base):
    __tablename__ = "invitations"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    token_hash: Mapped[str] = mapped_column(String(64), unique=True)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    expires_at: Mapped[datetime] = mapped_column(UTCDateTime)
    redeemed_by: Mapped[str | None] = mapped_column(ForeignKey("users.id"), nullable=True)
    redeemed_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    revoked_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Audiences and grants
# ---------------------------------------------------------------------------


class AudienceSnapshot(Base):
    __tablename__ = "audience_snapshots"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    resource_type: Mapped[str] = mapped_column(String(16))  # moment | recap | session
    resource_id: Mapped[str] = mapped_column(String(36), index=True)
    created_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    room_wide: Mapped[bool] = mapped_column(Boolean, default=False)  # covered every active member at creation
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class RecipientGrant(Base):
    __tablename__ = "recipient_grants"
    __table_args__ = (UniqueConstraint("snapshot_id", "user_id", name="uq_grant_snapshot_user"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    snapshot_id: Mapped[str] = mapped_column(ForeignKey("audience_snapshots.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    membership_generation: Mapped[int] = mapped_column(Integer)
    granted_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    revoked_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


# ---------------------------------------------------------------------------
# Media and moments
# ---------------------------------------------------------------------------


class MediaAsset(Base):
    __tablename__ = "media_assets"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    kind: Mapped[str] = mapped_column(String(10))  # image | video | audio
    original_path: Mapped[str | None] = mapped_column(String(400), nullable=True)
    derivative_path: Mapped[str | None] = mapped_column(String(400), nullable=True)
    thumbnail_path: Mapped[str | None] = mapped_column(String(400), nullable=True)
    original_mime: Mapped[str | None] = mapped_column(String(80), nullable=True)
    derivative_mime: Mapped[str | None] = mapped_column(String(80), nullable=True)
    size_bytes: Mapped[int] = mapped_column(Integer, default=0)
    duration_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    width: Mapped[int | None] = mapped_column(Integer, nullable=True)
    height: Mapped[int | None] = mapped_column(Integer, nullable=True)
    rotation: Mapped[int] = mapped_column(Integer, default=0)
    rotation_override: Mapped[int | None] = mapped_column(Integer, nullable=True)
    source_device: Mapped[str] = mapped_column(String(20), default="phone")
    capture_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    capture_at_source: Mapped[str] = mapped_column(String(12), default="unknown")  # metadata | user | upload | unknown
    projection: Mapped[str] = mapped_column(String(16), default="flat")
    trim_start_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    trim_end_ms: Mapped[int | None] = mapped_column(Integer, nullable=True)
    input_profile: Mapped[dict] = mapped_column(JSON, default=dict)
    processing_state: Mapped[str] = mapped_column(String(12), default="pending")  # pending|processing|ready|failed
    safe_error: Mapped[str | None] = mapped_column(String(200), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Moment(Base):
    __tablename__ = "moments"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    media_asset_id: Mapped[str | None] = mapped_column(ForeignKey("media_assets.id"), nullable=True)
    kind: Mapped[str] = mapped_column(String(8))  # photo | clip | voice | text
    caption: Mapped[str] = mapped_column(String(500), default="")
    transcript: Mapped[str | None] = mapped_column(Text, nullable=True)  # only with transcript consent
    transcript_consent: Mapped[bool] = mapped_column(Boolean, default=False)
    capture_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    capture_zone: Mapped[str] = mapped_column(String(64))
    room_date: Mapped[date] = mapped_column(Date, index=True)
    source_version: Mapped[int] = mapped_column(Integer, default=1)
    sharing_version: Mapped[int] = mapped_column(Integer, default=0)
    sharing_state: Mapped[str] = mapped_column(String(10), default="private")  # private|shared|withdrawn|deleted
    ai_allowed: Mapped[bool] = mapped_column(Boolean, default=False)
    audience_snapshot_id: Mapped[str | None] = mapped_column(ForeignKey("audience_snapshots.id"), nullable=True)
    recorded_gesture: Mapped[str | None] = mapped_column(String(20), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Recaps
# ---------------------------------------------------------------------------


class Recap(Base):
    __tablename__ = "recaps"
    __table_args__ = (Index("ix_recap_room_date", "room_id", "room_date"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"))
    room_date: Mapped[date] = mapped_column(Date)
    version: Mapped[int] = mapped_column(Integer, default=1)
    requested_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    audience_snapshot_id: Mapped[str] = mapped_column(ForeignKey("audience_snapshots.id"))
    state: Mapped[str] = mapped_column(String(20), default="queued")
    generator: Mapped[str] = mapped_column(String(16), default="pending")  # ai | chronological
    plan: Mapped[dict | None] = mapped_column(JSON, nullable=True)
    omitted_count: Mapped[int] = mapped_column(Integer, default=0)
    cache_key: Mapped[str] = mapped_column(String(64), index=True)
    invalidation_reason: Mapped[str | None] = mapped_column(String(40), nullable=True)
    failure_code: Mapped[str | None] = mapped_column(String(40), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    published_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


class RecapSource(Base):
    __tablename__ = "recap_sources"
    __table_args__ = (UniqueConstraint("recap_id", "moment_id", name="uq_recap_source"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    recap_id: Mapped[str] = mapped_column(ForeignKey("recaps.id", ondelete="CASCADE"), index=True)
    moment_id: Mapped[str] = mapped_column(ForeignKey("moments.id", ondelete="CASCADE"), index=True)
    contributor_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    source_version: Mapped[int] = mapped_column(Integer)
    sharing_version: Mapped[int] = mapped_column(Integer)


class RecapApproval(Base):
    __tablename__ = "recap_approvals"
    __table_args__ = (UniqueConstraint("recap_id", "contributor_id", name="uq_recap_approval"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    recap_id: Mapped[str] = mapped_column(ForeignKey("recaps.id", ondelete="CASCADE"), index=True)
    contributor_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    audience_snapshot_id: Mapped[str] = mapped_column(String(36))
    source_fingerprint: Mapped[str] = mapped_column(String(64))
    approved_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Social: replies, reactions, room objects
# ---------------------------------------------------------------------------


class Reply(Base):
    __tablename__ = "replies"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    moment_id: Mapped[str] = mapped_column(ForeignKey("moments.id", ondelete="CASCADE"), index=True)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    author_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    text: Mapped[str] = mapped_column(String(1000))
    voice_asset_id: Mapped[str | None] = mapped_column(ForeignKey("media_assets.id"), nullable=True)
    client_key: Mapped[str | None] = mapped_column(String(64), nullable=True)
    withdrawn: Mapped[bool] = mapped_column(Boolean, default=False)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    __table_args__ = (UniqueConstraint("author_id", "client_key", name="uq_reply_idempotency"),)


class Reaction(Base):
    __tablename__ = "reactions"
    __table_args__ = (UniqueConstraint("moment_id", "user_id", "kind", name="uq_reaction"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    moment_id: Mapped[str] = mapped_column(ForeignKey("moments.id", ondelete="CASCADE"), index=True)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    kind: Mapped[str] = mapped_column(String(12))
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class RoomObject(Base):
    __tablename__ = "room_objects"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    owner_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    object_type: Mapped[str] = mapped_column(String(16))  # frame | cup | postcard | audioStone | note
    moment_id: Mapped[str | None] = mapped_column(ForeignKey("moments.id", ondelete="CASCADE"), nullable=True)
    reply_id: Mapped[str | None] = mapped_column(ForeignKey("replies.id", ondelete="CASCADE"), nullable=True)
    # Table-relative transform: metres from table centre, yaw in degrees.
    x: Mapped[float] = mapped_column(Float, default=0.0)
    y: Mapped[float] = mapped_column(Float, default=0.0)
    z: Mapped[float] = mapped_column(Float, default=0.0)
    yaw: Mapped[float] = mapped_column(Float, default=0.0)
    version: Mapped[int] = mapped_column(Integer, default=1)
    updated_by: Mapped[str] = mapped_column(ForeignKey("users.id"))
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Live viewing sessions
# ---------------------------------------------------------------------------


class ViewingSession(Base):
    __tablename__ = "viewing_sessions"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    moment_id: Mapped[str] = mapped_column(ForeignKey("moments.id", ondelete="CASCADE"))
    host_id: Mapped[str] = mapped_column(ForeignKey("users.id"))
    participant_ids: Mapped[list] = mapped_column(JSON, default=list)
    version: Mapped[int] = mapped_column(Integer, default=1)
    playhead_ms: Mapped[int] = mapped_column(Integer, default=0)
    paused: Mapped[bool] = mapped_column(Boolean, default=True)
    state_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    ended_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Awards
# ---------------------------------------------------------------------------


class AchievementEvent(Base):
    __tablename__ = "achievement_events"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    kind: Mapped[str] = mapped_column(String(24))  # capsule_published | reply_posted
    event_key: Mapped[str] = mapped_column(String(160), unique=True)
    room_date: Mapped[date | None] = mapped_column(Date, nullable=True)
    user_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    recap_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Award(Base):
    __tablename__ = "awards"
    __table_args__ = (UniqueConstraint("room_id", "milestone", name="uq_award_room_milestone"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    milestone: Mapped[str] = mapped_column(String(32))
    source_recap_id: Mapped[str | None] = mapped_column(String(36), nullable=True)
    earned_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Availability
# ---------------------------------------------------------------------------


class AvailabilityRule(Base):
    __tablename__ = "availability_rules"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    weekday: Mapped[int] = mapped_column(Integer)  # 0 = Monday
    start_local: Mapped[str] = mapped_column(String(5))
    end_local: Mapped[str] = mapped_column(String(5))  # end <= start means overnight
    version: Mapped[int] = mapped_column(Integer, default=1)


class AvailabilityException(Base):
    __tablename__ = "availability_exceptions"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    room_id: Mapped[str | None] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), nullable=True)
    kind: Mapped[str] = mapped_column(String(8))  # busy | travel | extra
    start_at: Mapped[datetime] = mapped_column(UTCDateTime)
    end_at: Mapped[datetime] = mapped_column(UTCDateTime)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class FreeNow(Base):
    __tablename__ = "free_now"
    __table_args__ = (UniqueConstraint("user_id", "room_id", name="uq_free_now"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"))
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"))
    set_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    expires_at: Mapped[datetime] = mapped_column(UTCDateTime)


class CallOpportunity(Base):
    __tablename__ = "call_opportunities"
    __table_args__ = (UniqueConstraint("room_id", "participants_key", "start_at", name="uq_call_opportunity"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"), index=True)
    participants_key: Mapped[str] = mapped_column(String(300))
    participant_ids: Mapped[list] = mapped_column(JSON, default=list)
    start_at: Mapped[datetime] = mapped_column(UTCDateTime)
    end_at: Mapped[datetime] = mapped_column(UTCDateTime)
    schedule_fingerprint: Mapped[str] = mapped_column(String(64))
    state: Mapped[str] = mapped_column(String(12), default="pending")  # pending|notified|accepted|dismissed|cancelled|expired
    created_by: Mapped[str | None] = mapped_column(String(36), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


# ---------------------------------------------------------------------------
# Notifications, push, jobs
# ---------------------------------------------------------------------------


class Notification(Base):
    __tablename__ = "notifications"
    __table_args__ = (UniqueConstraint("recipient_id", "event_key", name="uq_notification_event"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    recipient_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    room_id: Mapped[str] = mapped_column(ForeignKey("rooms.id", ondelete="CASCADE"))
    kind: Mapped[str] = mapped_column(String(20))  # recap_ready | reply_received | call_opportunity
    event_key: Mapped[str] = mapped_column(String(160))
    resource_type: Mapped[str] = mapped_column(String(16))
    resource_id: Mapped[str] = mapped_column(String(36))
    payload: Mapped[dict] = mapped_column(JSON, default=dict)  # safe, generic fields only
    push_state: Mapped[str] = mapped_column(String(12), default="none")  # none|sent|failed|skipped
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    read_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    cancelled_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)


class PushSubscription(Base):
    __tablename__ = "push_subscriptions"
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    user_id: Mapped[str] = mapped_column(ForeignKey("users.id", ondelete="CASCADE"), index=True)
    endpoint: Mapped[str] = mapped_column(Text, unique=True)
    p256dh: Mapped[str] = mapped_column(String(200))
    auth: Mapped[str] = mapped_column(String(100))
    user_agent_family: Mapped[str | None] = mapped_column(String(40), nullable=True)
    failure_count: Mapped[int] = mapped_column(Integer, default=0)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)


class Job(Base):
    __tablename__ = "jobs"
    __table_args__ = (Index("ix_jobs_claim", "state", "run_after"),)
    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=new_id)
    kind: Mapped[str] = mapped_column(String(40))
    idempotency_key: Mapped[str] = mapped_column(String(200), unique=True)
    payload: Mapped[dict] = mapped_column(JSON, default=dict)
    state: Mapped[str] = mapped_column(String(12), default="queued")  # queued|running|done|failed|cancelled|expired
    attempts: Mapped[int] = mapped_column(Integer, default=0)
    max_attempts: Mapped[int] = mapped_column(Integer, default=5)
    run_after: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    lease_until: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    expires_at: Mapped[datetime | None] = mapped_column(UTCDateTime, nullable=True)
    last_error: Mapped[str | None] = mapped_column(String(120), nullable=True)
    created_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
    updated_at: Mapped[datetime] = mapped_column(UTCDateTime, default=utcnow)
