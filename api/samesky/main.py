"""FastAPI application entrypoint: `uvicorn samesky.main:app`."""

from __future__ import annotations

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

from .config import get_settings
from .db import create_all
from .errors import ApiError, api_error_handler
from .routers import content, core, time

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s %(message)s")


@asynccontextmanager
async def lifespan(_app: FastAPI):
    if get_settings().secret_key == "dev-only-change-me":
        logging.getLogger("samesky").warning("SAMESKY_SECRET_KEY is the development default; set a long random value before deploying.")
    create_all()
    get_settings().media_root.mkdir(parents=True, exist_ok=True)
    yield


def create_app() -> FastAPI:
    app = FastAPI(title="SameSky API", version="0.1.0", lifespan=lifespan)
    app.add_exception_handler(ApiError, api_error_handler)

    async def validation_handler(_request, exc: RequestValidationError):
        # Never echo submitted values (they may contain personal content); report field locations only.
        fields = [".".join(str(p) for p in err.get("loc", [])[1:]) for err in exc.errors()]
        return JSONResponse(
            status_code=422,
            content={"error": {"code": "invalid_request", "message": "Some fields are missing or invalid.", "fields": fields, "retryable": False}},
        )

    app.add_exception_handler(RequestValidationError, validation_handler)
    app.add_middleware(
        CORSMiddleware,
        allow_origins=[o for o in get_settings().cors_origins if o],
        allow_origin_regex=r"https?://(localhost|127\.0\.0\.1|\[::1\]|192\.168\.\d+\.\d+|10\.\d+\.\d+\.\d+)(:\d+)?",
        allow_credentials=False,
        allow_methods=["*"],
        allow_headers=["Authorization", "Content-Type"],
    )
    app.include_router(core.router)
    app.include_router(content.router)
    app.include_router(time.router)
    return app


app = create_app()
