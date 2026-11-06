"""Uniform, safe API errors: a stable code, a human message and retry guidance. No personal data."""

from __future__ import annotations

from fastapi import Request
from fastapi.responses import JSONResponse


class ApiError(Exception):
    def __init__(self, status: int, code: str, message: str, retryable: bool = False):
        super().__init__(code)
        self.status = status
        self.code = code
        self.message = message
        self.retryable = retryable


def not_found(what: str = "resource") -> ApiError:
    # Used for "does not exist" *and* "exists but you may not see it" so existence never leaks.
    return ApiError(404, "not_found", f"That {what} isn't available to you.")


def forbidden(message: str = "You can't do that here.") -> ApiError:
    return ApiError(403, "forbidden", message)


def conflict(code: str, message: str) -> ApiError:
    return ApiError(409, code, message)


def invalid(code: str, message: str) -> ApiError:
    return ApiError(422, code, message)


async def api_error_handler(_request: Request, exc: ApiError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status,
        content={"error": {"code": exc.code, "message": exc.message, "retryable": exc.retryable}},
    )
