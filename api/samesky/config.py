"""Server configuration. Every secret is read from the environment and never sent to clients."""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from pathlib import Path

API_ROOT = Path(__file__).resolve().parent.parent


def _load_dotenv() -> None:
    """Minimal .env support (repo root, then api/). Real environment variables always win."""
    for path in (API_ROOT.parent / ".env", API_ROOT / ".env"):
        if not path.is_file():
            continue
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            os.environ.setdefault(key.strip(), value.strip().strip('"').strip("'"))


_load_dotenv()


def _env(name: str, default: str | None = None) -> str | None:
    value = os.environ.get(name)
    return value if value not in (None, "") else default


def _env_int(name: str, default: int) -> int:
    raw = _env(name)
    return int(raw) if raw is not None else default


@dataclass(frozen=True)
class MediaLimits:
    image_bytes: int = 10 * 1024 * 1024
    video_source_bytes: int = 100 * 1024 * 1024
    video_source_seconds: float = 60.0
    highlight_seconds: float = 15.0
    highlight_bytes: int = 25 * 1024 * 1024
    voice_bytes: int = 5 * 1024 * 1024
    voice_seconds: float = 30.0


@dataclass(frozen=True)
class Settings:
    database_url: str = field(default_factory=lambda: _env("DATABASE_URL", f"sqlite:///{API_ROOT / 'data' / 'samesky.db'}"))
    secret_key: str = field(default_factory=lambda: _env("SAMESKY_SECRET_KEY", "dev-only-change-me"))
    token_ttl_seconds: int = field(default_factory=lambda: _env_int("SAMESKY_TOKEN_TTL", 7 * 24 * 3600))
    media_url_ttl_seconds: int = field(default_factory=lambda: _env_int("SAMESKY_MEDIA_URL_TTL", 600))
    media_root: Path = field(default_factory=lambda: Path(_env("SAMESKY_MEDIA_ROOT", str(API_ROOT / "data" / "media"))))
    public_api_url: str = field(default_factory=lambda: _env("SAMESKY_PUBLIC_API_URL", ""))
    cors_origins: tuple[str, ...] = field(
        default_factory=lambda: tuple(
            (_env("SAMESKY_CORS_ORIGINS", "http://localhost:5173,https://localhost:5173") or "").split(",")
        )
    )
    # Optional Supabase Auth: when set, Supabase-issued JWTs are accepted as identity.
    supabase_jwt_secret: str | None = field(default_factory=lambda: _env("SUPABASE_JWT_SECRET"))
    # AI recap adapter (optional). Without a key the deterministic chronological recap is used.
    anthropic_api_key: str | None = field(default_factory=lambda: _env("ANTHROPIC_API_KEY"))
    ai_model: str = field(default_factory=lambda: _env("SAMESKY_AI_MODEL", "claude-opus-5-5"))
    ai_timeout_seconds: float = field(default_factory=lambda: float(_env("SAMESKY_AI_TIMEOUT", "45")))
    ai_max_input_chars: int = field(default_factory=lambda: _env_int("SAMESKY_AI_MAX_INPUT_CHARS", 12000))
    ai_generations_per_room_per_day: int = field(default_factory=lambda: _env_int("SAMESKY_AI_DAILY_LIMIT", 6))
    # Web Push (VAPID). The private key never leaves the server.
    vapid_public_key: str | None = field(default_factory=lambda: _env("VAPID_PUBLIC_KEY"))
    vapid_private_key: str | None = field(default_factory=lambda: _env("VAPID_PRIVATE_KEY"))
    vapid_subject: str = field(default_factory=lambda: _env("VAPID_SUBJECT", "mailto:admin@samesky.invalid"))
    # Product limits
    direct_capacity: int = 2
    circle_capacity: int = field(default_factory=lambda: _env_int("SAMESKY_CIRCLE_CAPACITY", 6))
    invite_ttl_hours: int = field(default_factory=lambda: _env_int("SAMESKY_INVITE_TTL_HOURS", 72))
    max_daily_contributions: int = 3
    recap_max_sources: int = 6
    free_now_minutes: int = 30
    default_min_overlap_minutes: int = 15
    call_suggestions_per_day: int = 2
    call_suggestion_spacing_hours: int = 4
    default_room_zone: str = "Asia/Dubai"
    allowed_call_link_schemes: tuple[str, ...] = ("https", "facetime", "facetime-audio", "tel", "whatsapp")
    media: MediaLimits = field(default_factory=MediaLimits)


_settings: Settings | None = None


def get_settings() -> Settings:
    global _settings
    if _settings is None:
        _settings = Settings()
    return _settings


def override_settings(settings: Settings) -> None:
    """Used by tests to point the app at an isolated database/media folder."""
    global _settings
    _settings = settings
