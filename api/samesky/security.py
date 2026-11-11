"""Password hashing, access tokens, invitation tokens and short-lived signed media URLs."""

from __future__ import annotations

import base64
import hashlib
import hmac
import secrets
import time
from datetime import UTC, datetime, timedelta

import jwt

from .config import get_settings

_PBKDF2_ROUNDS = 390_000


def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode(), salt, _PBKDF2_ROUNDS)
    return "pbkdf2$%d$%s$%s" % (
        _PBKDF2_ROUNDS,
        base64.b64encode(salt).decode(),
        base64.b64encode(digest).decode(),
    )


def verify_password(password: str, stored: str | None) -> bool:
    if not stored:
        return False
    try:
        scheme, rounds, salt_b64, digest_b64 = stored.split("$")
        if scheme != "pbkdf2":
            return False
        digest = hashlib.pbkdf2_hmac("sha256", password.encode(), base64.b64decode(salt_b64), int(rounds))
        return hmac.compare_digest(digest, base64.b64decode(digest_b64))
    except (ValueError, TypeError):
        return False


def issue_access_token(user_id: str) -> str:
    settings = get_settings()
    now = datetime.now(UTC)
    payload = {
        "sub": user_id,
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(seconds=settings.token_ttl_seconds)).timestamp()),
        "iss": "samesky",
    }
    return jwt.encode(payload, settings.secret_key, algorithm="HS256")


class TokenError(Exception):
    pass


def decode_access_token(token: str) -> tuple[str, str]:
    """Returns (issuer, subject). Accepts SameSky tokens and, if configured, Supabase Auth tokens."""
    settings = get_settings()
    try:
        payload = jwt.decode(token, settings.secret_key, algorithms=["HS256"], issuer="samesky")
        return "samesky", str(payload["sub"])
    except jwt.PyJWTError:
        pass
    if settings.supabase_jwt_secret:
        try:
            payload = jwt.decode(
                token, settings.supabase_jwt_secret, algorithms=["HS256"], audience="authenticated"
            )
            return "supabase", str(payload["sub"])
        except jwt.PyJWTError:
            pass
    raise TokenError("invalid_token")


def new_invite_token() -> tuple[str, str]:
    """Returns (plaintext token for the link, sha256 hash stored server-side)."""
    token = secrets.token_urlsafe(24)
    return token, hash_invite_token(token)


def hash_invite_token(token: str) -> str:
    return hashlib.sha256(token.encode()).hexdigest()


# Short invite codes are typed by hand (in a headset, on a spatial keypad), so they use an alphabet without
# look-alikes (no I, L, O, 0 or 1). 31^8 ≈ 8.5e11 codes; redemption needs a signed-in account and is
# rate-limited per account (services/rooms.py), and only an HMAC of the code is stored, so a database copy
# without the server key can't be used to enumerate live codes.
INVITE_CODE_ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"
INVITE_CODE_LENGTH = 8


def new_invite_code() -> tuple[str, str]:
    """Returns (display code such as 'K7PQ-4XMR', HMAC stored server-side)."""
    raw = "".join(secrets.choice(INVITE_CODE_ALPHABET) for _ in range(INVITE_CODE_LENGTH))
    return f"{raw[:4]}-{raw[4:]}", hash_invite_code(raw)


def normalize_invite_code(code: str) -> str | None:
    """Uppercases and drops spaces and dashes; None if it can't be a code."""
    cleaned = "".join(ch for ch in code.upper() if ch not in " -_\t")
    if len(cleaned) != INVITE_CODE_LENGTH or any(ch not in INVITE_CODE_ALPHABET for ch in cleaned):
        return None
    return cleaned


def hash_invite_code(normalized: str) -> str:
    key = get_settings().secret_key.encode()
    return hmac.new(key, f"invite-code:{normalized}".encode(), hashlib.sha256).hexdigest()


def sign_media_url(asset_id: str, variant: str, user_id: str, ttl: int | None = None) -> str:
    settings = get_settings()
    expires = int(time.time()) + (ttl or settings.media_url_ttl_seconds)
    message = f"{asset_id}:{variant}:{user_id}:{expires}".encode()
    sig = hmac.new(settings.secret_key.encode(), message, hashlib.sha256).hexdigest()[:40]
    return f"/v1/media/{asset_id}/{variant}?u={user_id}&e={expires}&s={sig}"


def sign_resource_url(prefix: str, resource_id: str, variant: str, user_id: str, ttl: int | None = None) -> str:
    """Same signature scheme as media, for other private resources (avatar models and source photos).
    The variant is part of the signed message, so a link for one resource kind can't be replayed for another."""
    settings = get_settings()
    expires = int(time.time()) + (ttl or settings.media_url_ttl_seconds)
    message = f"{resource_id}:{variant}:{user_id}:{expires}".encode()
    sig = hmac.new(settings.secret_key.encode(), message, hashlib.sha256).hexdigest()[:40]
    return f"{prefix}/{resource_id}/{variant}?u={user_id}&e={expires}&s={sig}"


def verify_media_signature(asset_id: str, variant: str, user_id: str, expires: int, sig: str) -> bool:
    if expires < int(time.time()):
        return False
    settings = get_settings()
    message = f"{asset_id}:{variant}:{user_id}:{expires}".encode()
    expected = hmac.new(settings.secret_key.encode(), message, hashlib.sha256).hexdigest()[:40]
    return hmac.compare_digest(expected, sig)
