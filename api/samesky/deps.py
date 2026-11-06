"""Request dependencies: the actor is always derived from the verified token, never a supplied id."""

from __future__ import annotations

from fastapi import Depends, Header
from sqlalchemy.orm import Session

from .db import get_db
from .errors import ApiError
from .models import User
from .security import TokenError, decode_access_token


def current_user(authorization: str | None = Header(default=None), db: Session = Depends(get_db)) -> User:
    if not authorization or not authorization.lower().startswith("bearer "):
        raise ApiError(401, "unauthenticated", "Sign in to continue.")
    try:
        issuer, subject = decode_access_token(authorization.split(" ", 1)[1].strip())
    except TokenError:
        raise ApiError(401, "invalid_token", "Your session has expired. Sign in again.") from None
    user = db.get(User, subject)
    if user is None and issuer == "supabase":
        raise ApiError(403, "profile_missing", "Finish creating your SameSky profile first.")
    if user is None:
        raise ApiError(401, "invalid_token", "Your session has expired. Sign in again.")
    return user
