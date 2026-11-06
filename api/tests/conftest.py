from __future__ import annotations

import io
import os
from dataclasses import replace
from datetime import UTC, datetime

import pytest

os.environ.setdefault("SAMESKY_SECRET_KEY", "test-secret")
os.environ.pop("ANTHROPIC_API_KEY", None)

from fastapi.testclient import TestClient  # noqa: E402

from samesky import config, db  # noqa: E402
from samesky.main import create_app  # noqa: E402


@pytest.fixture()
def settings(tmp_path):
    base = config.Settings()
    s = replace(
        base,
        database_url=f"sqlite:///{tmp_path / 'test.db'}",
        media_root=tmp_path / "media",
        secret_key="test-secret",
        anthropic_api_key=None,
        vapid_private_key=None,
        vapid_public_key=None,
        public_api_url="",
    )
    config.override_settings(s)
    db.init_engine(s.database_url)
    db.create_all()
    yield s


@pytest.fixture()
def client(settings):
    with TestClient(create_app()) as c:
        yield c


class Api:
    """Tiny helper that keeps tests readable."""

    def __init__(self, client: TestClient):
        self.c = client

    def signup(self, name: str, zone: str = "Asia/Dubai") -> dict:
        r = self.c.post("/v1/auth/signup", json={"email": f"{name.lower()}@example.com", "password": "correct horse", "display_name": name, "time_zone": zone})
        assert r.status_code == 200, r.text
        data = r.json()
        return {"id": data["user"]["id"], "token": data["token"], "name": name, "h": {"Authorization": f"Bearer {data['token']}"}}

    def call(self, user, method: str, path: str, expect: int | None = 200, **kw):
        r = self.c.request(method, path, headers=user["h"], **kw)
        if expect is not None:
            assert r.status_code == expect, f"{method} {path} -> {r.status_code} {r.text}"
        return r

    def room(self, owner, kind="direct", name="Our room", members=()):
        room = self.call(owner, "POST", "/v1/rooms", json={"kind": kind, "name": name, "reporting_zone": "Asia/Dubai"}).json()
        for m in members:
            self.join(owner, m, room["id"])
        return room

    def join(self, owner, member, room_id):
        token = self.call(owner, "POST", f"/v1/rooms/{room_id}/invites").json()["token"]
        return self.call(member, "POST", "/v1/invites/redeem", json={"token": token}).json()

    def text_moment(self, user, room_id, caption, at: datetime, ai=True):
        return self.call(user, "POST", f"/v1/rooms/{room_id}/moments", json={
            "kind": "text", "caption": caption, "capture_at": at.isoformat(), "ai_allowed": ai,
        }).json()

    def share(self, user, moment_id, recipients):
        resolved = self.call(user, "POST", f"/v1/moments/{moment_id}/audience/resolve", json={"recipient_ids": recipients}).json()
        return self.call(user, "POST", f"/v1/moments/{moment_id}/share", json={"recipient_ids": recipients, "confirmation_token": resolved["confirmationToken"]}).json()

    def upload(self, user, data: bytes, kind: str, filename: str, source_device="phone", expect=200, **form):
        fields = {"kind": kind, "source_device": source_device, **{k: str(v) for k, v in form.items()}}
        return self.call(user, "POST", "/v1/media", expect=expect, files={"file": (filename, io.BytesIO(data))}, data=fields)

    def recap_published(self, requester, room_id, room_date, approvers, recipients=None):
        body = {"room_date": room_date}
        if recipients is not None:
            body["recipient_ids"] = recipients
        recap = self.call(requester, "POST", f"/v1/rooms/{room_id}/recaps", json=body).json()
        run_worker()
        for a in approvers:
            recap = self.call(a, "POST", f"/v1/recaps/{recap['id']}/approve").json()
        return recap


def run_worker(now=None) -> int:
    from samesky.worker import run_once

    return run_once(now=now)


@pytest.fixture()
def api(client):
    return Api(client)


def at(day: int, hour: int = 14, minute: int = 0) -> datetime:
    """A UTC instant on 2026-11-<day>; 14:00 UTC is 18:00 in Dubai (same room date)."""
    return datetime(2026, 11, day, hour, minute, tzinfo=UTC)


def png_bytes(w=64, h=48, color=(200, 160, 90)) -> bytes:
    from PIL import Image

    buf = io.BytesIO()
    Image.new("RGB", (w, h), color).save(buf, "PNG")
    return buf.getvalue()
