"""Photo-derived avatars: consent, private sources, durable jobs, owner approval, sharing, revocation, deletion.

A fake provider stands in for the real service (no credentials or real faces are used in tests). It follows
the same adapter contract as the MetaPerson adapter: submit -> status (queued/computing/completed/failed)
-> exported GLB -> delete.
"""

from __future__ import annotations

import io
import json
import struct
from datetime import timedelta
from pathlib import Path

import pytest
from conftest import run_worker

from samesky.models import utcnow
from samesky.services import avatar_providers, glb
from samesky.services import avatars as avatars_service
from samesky.services.avatar_providers import ProviderError, ProviderStatus

HUMANOID = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "RightUpLeg", "RightLeg", "RightFoot"]


def make_glb(*, joints=HUMANOID, skinned=True, image_uri=None, required=(), triangles=1, morphs=("eyeBlinkLeft", "eyeBlinkRight")) -> bytes:
    """A tiny but structurally valid skinned GLB (one triangle, a humanoid joint list, blink morph names)."""
    positions = struct.pack("<9f", 0, 0, 0, 1, 0, 0, 0, 1, 0)
    indices = struct.pack("<3H", 0, 1, 2) + b"\x00\x00"
    binary = positions + indices
    nodes = [{"name": n} for n in joints] + [{"name": "Body", "mesh": 0, **({"skin": 0} if skinned else {})}]
    doc = {
        "asset": {"version": "2.0", "generator": "samesky-test"},
        "scene": 0,
        "scenes": [{"nodes": [len(nodes) - 1, 0]}],
        "nodes": nodes,
        "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "mode": 4}], "extras": {"targetNames": list(morphs)}}],
        "accessors": [
            {"bufferView": 0, "componentType": 5126, "count": 3, "type": "VEC3", "min": [0, 0, 0], "max": [1, 1, 0]},
            {"bufferView": 1, "componentType": 5123, "count": 3 * triangles, "type": "SCALAR"},
        ],
        "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 36}, {"buffer": 0, "byteOffset": 36, "byteLength": max(6, 6 * triangles)}],
        "buffers": [{"byteLength": len(binary)}],
    }
    if skinned:
        doc["skins"] = [{"joints": list(range(len(joints)))}]
    if image_uri:
        doc["images"] = [{"uri": image_uri}]
    if required:
        doc["extensionsRequired"] = list(required)
        doc["extensionsUsed"] = list(required)
    js = json.dumps(doc).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    binary += b"\x00" * ((4 - len(binary) % 4) % 4)
    total = 12 + 8 + len(js) + 8 + len(binary)
    return struct.pack("<III", 0x46546C67, 2, total) + struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(binary), 0x004E4942) + binary


def photo(w=900, h=1100, gps=True) -> bytes:
    from PIL import Image

    img = Image.new("RGB", (w, h), (182, 140, 120))
    exif = Image.Exif()
    exif[0x010F] = "TestCam"
    if gps:
        exif[0x8825] = {1: "N", 2: (25.0, 12.0, 0.0), 3: "E", 4: (55.0, 16.0, 0.0)}
    out = io.BytesIO()
    img.save(out, "JPEG", exif=exif)
    return out.getvalue()


class FakeProvider:
    name = "metaperson"

    def __init__(self, *, steps=("computing", "completed"), model: bytes | None = None, fail_submit: ProviderError | None = None):
        self.steps = list(steps)
        self.model = model if model is not None else make_glb()
        self.fail_submit = fail_submit
        self.submitted: list[tuple[str, dict]] = []
        self.deleted: list[str] = []

    def configured(self) -> bool:
        return True

    def submit(self, photo_path: Path, options: dict) -> str:
        if self.fail_submit:
            raise self.fail_submit
        assert photo_path.exists()
        self.submitted.append((photo_path.name, options))
        return f"fake-{len(self.submitted)}"

    def status(self, ref: str) -> ProviderStatus:
        state = self.steps.pop(0) if len(self.steps) > 1 else self.steps[0]
        return ProviderStatus(state=state, progress=100 if state == "completed" else 40, message="Blurry photo" if state == "failed" else None)

    def fetch_export(self, ref: str, max_bytes: int) -> bytes | None:
        return self.model

    def delete(self, ref: str) -> bool:
        self.deleted.append(ref)
        return True


@pytest.fixture()
def provider():
    fake = FakeProvider()
    avatar_providers.set_test_provider(fake)
    yield fake
    avatar_providers.set_test_provider(None)


class Clock:
    """Moves the avatar service's clock and the worker's claim time together, so each pass runs exactly one poll."""

    def __init__(self, monkeypatch):
        self.offset = timedelta(0)
        monkeypatch.setattr(avatars_service, "utcnow", lambda: utcnow() + self.offset)

    def now(self):
        return utcnow() + self.offset

    def run(self, advance: float = 6):
        self.offset += timedelta(seconds=advance)
        return run_worker(now=self.now())


@pytest.fixture()
def clock(monkeypatch):
    return Clock(monkeypatch)


def consent(api, user, provider="metaperson", scope="rooms"):
    return api.call(user, "POST", "/v1/me/avatar-consents", json={"provider": provider, "processing": True, "likeness_scope": scope}).json()["id"]


def upload_source(api, user, consent_id, data=None, view="front", expect=200):
    return api.call(user, "POST", "/v1/me/avatar-sources", expect=expect, files={"file": ("me.jpg", io.BytesIO(data or photo()))}, data={"view": view, "consent_id": consent_id})


def start(api, user, consent_id, source_id, key="key-0001", expect=200, body="female"):
    return api.call(user, "POST", "/v1/me/avatar-jobs", expect=expect, json={"source_ids": [source_id], "consent_id": consent_id, "options": {"body": body}, "idempotency_key": key})


def generate(api, user, clock, scope="rooms", key="key-0001"):
    """Run a full fake generation and return (job, asset id awaiting approval)."""
    cid = consent(api, user, scope=scope)
    src = upload_source(api, user, cid).json()
    job = start(api, user, cid, src["id"], key=key).json()
    clock.run(0)  # submit
    clock.run()  # poll: computing
    clock.run()  # poll: completed -> validate -> awaiting approval
    job = api.call(user, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()
    return job, job["assetId"]


def test_full_flow_private_sources_approval_and_viewer_access(api, provider, settings, clock):
    adam, maya, outsider = api.signup("Adam"), api.signup("Maya"), api.signup("Olive")
    api.room(adam, members=[maya])
    cid = consent(api, maya)
    src = upload_source(api, maya, cid).json()
    # source photo: EXIF/GPS stripped, private, owner-only link
    stored = Path(settings.media_root).glob(f"avatar-private/{maya['id']}/sources/*.jpg")
    from PIL import Image

    img = Image.open(next(stored))
    assert not img.getexif().get(0x8825) and not img.getexif().get(0x010F)
    assert api.c.get(src["imageUrl"]).status_code == 200
    forged = src["imageUrl"].replace(f"u={maya['id']}", f"u={adam['id']}")
    assert api.c.get(forged).status_code == 403

    job = start(api, maya, cid, src["id"]).json()
    assert job["state"] == "queued"
    assert start(api, maya, cid, src["id"]).json()["id"] == job["id"]  # idempotent submission
    api.call(adam, "GET", f"/v1/me/avatar-jobs/{job['id']}", expect=404)  # nobody else sees a private job
    clock.run(0)
    assert api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()["state"] == "processing"
    clock.run()
    assert api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()["progress"] == 40
    clock.run()
    job = api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()
    assert job["state"] == "awaiting_owner_approval" and job["assetId"]
    assert provider.submitted[0][1]["body"] == "female"

    # before approval the partner still sees the labelled demo preset
    room = api.call(adam, "GET", "/v1/rooms").json()[0]
    maya_view = next(m for m in room["members"] if m["id"] == maya["id"])["avatar"]
    assert maya_view is None or maya_view["kind"] == "preset_demo"
    api.call(adam, "POST", f"/v1/me/avatar-assets/{job['assetId']}/approve", expect=404, json={"confirmed": True})
    api.call(maya, "POST", f"/v1/me/avatar-assets/{job['assetId']}/approve", expect=422, json={"confirmed": False})
    asset = api.call(maya, "POST", f"/v1/me/avatar-assets/{job['assetId']}/approve", json={"confirmed": True, "visibility": "rooms"}).json()
    assert asset["state"] == "active" and asset["rig"]["humanoid"] and asset["rig"]["blink"]

    seen = api.call(adam, "GET", f"/v1/users/{maya['id']}/avatar").json()["avatar"]
    assert seen["kind"] == "photo_derived" and seen["assetId"] == job["assetId"]
    model = api.c.get(seen["modelUrl"])
    assert model.status_code == 200 and model.content[:4] == b"glTF"
    api.call(outsider, "GET", f"/v1/users/{maya['id']}/avatar", expect=404)
    outsider_link = seen["modelUrl"].replace(f"u={adam['id']}", f"u={outsider['id']}")
    assert api.c.get(outsider_link).status_code == 403
    # the owner's studio never exposes other people's data and lists the active model
    studio = api.call(maya, "GET", "/v1/me/avatar-studio").json()
    assert studio["activeAssetId"] == job["assetId"] and studio["provider"]["configured"]


def test_cancel_prevents_late_completion_and_cleans_up_provider(api, provider, clock):
    maya = api.signup("Maya")
    cid = consent(api, maya)
    src = upload_source(api, maya, cid).json()
    job = start(api, maya, cid, src["id"]).json()
    clock.run(0)  # submitted
    cancelled = api.call(maya, "POST", f"/v1/me/avatar-jobs/{job['id']}/cancel").json()
    assert cancelled["state"] == "cancelled" and cancelled["providerCleanup"] == "pending"
    clock.run(30)  # late poll + cleanup
    final = api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()
    assert final["state"] == "cancelled" and final["assetId"] is None and final["providerCleanup"] == "done"
    assert provider.deleted == ["fake-1"]
    assert api.call(maya, "GET", "/v1/me/avatar-studio").json()["assets"] == []


def test_new_generation_does_not_replace_the_approved_version_until_approved(api, provider, clock):
    maya = api.signup("Maya")
    _, first = generate(api, maya, clock)
    api.call(maya, "POST", f"/v1/me/avatar-assets/{first}/approve", json={"confirmed": True})
    provider.steps = ["computing", "completed"]
    _, second = generate(api, maya, clock, key="key-0002")
    assert second and second != first
    me = api.call(maya, "GET", "/v1/me").json()
    assert me["avatar"]["assetId"] == first  # still the approved one
    api.call(maya, "POST", f"/v1/me/avatar-assets/{second}/reject")
    assert api.call(maya, "GET", "/v1/me").json()["avatar"]["assetId"] == first


def test_revocation_member_removal_and_deletion_block_access(api, provider, clock):
    adam, maya, lina = api.signup("Adam"), api.signup("Maya"), api.signup("Lina")
    room = api.room(maya, kind="circle", name="Family", members=[adam, lina])
    _, asset_id = generate(api, maya, clock)
    api.call(maya, "POST", f"/v1/me/avatar-assets/{asset_id}/approve", json={"confirmed": True, "visibility": "rooms"})
    adam_link = api.call(adam, "GET", f"/v1/users/{maya['id']}/avatar").json()["avatar"]["modelUrl"]
    lina_link = api.call(lina, "GET", f"/v1/users/{maya['id']}/avatar").json()["avatar"]["modelUrl"]
    assert api.c.get(adam_link).status_code == 200
    # direct-only sharing hides it from a circle
    api.call(maya, "PATCH", f"/v1/me/avatar-assets/{asset_id}", json={"visibility": "direct"})
    assert api.call(adam, "GET", f"/v1/users/{maya['id']}/avatar").json()["avatar"]["kind"] == "preset_demo"
    assert api.c.get(adam_link).status_code == 404
    api.call(maya, "PATCH", f"/v1/me/avatar-assets/{asset_id}", json={"visibility": "rooms"})
    assert api.c.get(lina_link).status_code == 200
    # removal from the room blocks new reads immediately, even with a still-valid link
    api.call(maya, "DELETE", f"/v1/rooms/{room['id']}/members/{lina['id']}")
    assert api.c.get(lina_link).status_code == 404
    # deletion clears the active reference and every link; provider cleanup is recorded
    api.call(maya, "DELETE", f"/v1/me/avatar-assets/{asset_id}")
    assert api.c.get(adam_link).status_code == 404
    assert api.call(maya, "GET", "/v1/me").json()["avatar"]["kind"] == "preset_demo"
    clock.run(30)
    job = api.call(maya, "GET", "/v1/me/avatar-studio").json()["jobs"][0]
    assert job["providerCleanup"] == "done"
    api.call(adam, "DELETE", f"/v1/me/avatar-assets/{asset_id}", expect=404)


def test_invalid_inputs_and_honest_provider_states(api, provider, settings, clock):
    maya, adam = api.signup("Maya"), api.signup("Adam")
    cid = consent(api, maya)
    upload_source(api, maya, cid, data=b"not an image at all", expect=422)
    r = upload_source(api, maya, cid, data=photo(300, 300), expect=422)
    assert r.json()["error"]["code"] == "too_small"
    upload_source(api, adam, cid, expect=404)  # someone else's consent can't be used
    src = upload_source(api, maya, cid).json()
    r = api.call(maya, "POST", "/v1/me/avatar-jobs", expect=422, json={"source_ids": [src["id"]], "consent_id": cid, "options": {}, "idempotency_key": "key-0003"})
    assert r.json()["error"]["code"] == "body_required"  # never inferred from the photo
    # provider plan/credential failures become honest, final states
    provider.fail_submit = ProviderError("provider_plan_required", "The avatar service account has no active paid plan, so it can't create avatars right now.")
    job = start(api, maya, cid, src["id"], key="key-0004").json()
    clock.run(0)
    failed = api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()
    assert failed["state"] == "failed" and "paid plan" in failed["safeError"]
    # a bad photo reported by the provider
    provider.fail_submit = None
    provider.steps = ["failed"]
    job = start(api, maya, cid, src["id"], key="key-0005").json()
    clock.run(0)
    clock.run()
    assert api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()["safeError"] == "Blurry photo"
    retried = api.call(maya, "POST", f"/v1/me/avatar-jobs/{job['id']}/retry").json()
    assert retried["state"] == "queued" and retried["id"] != job["id"]


def test_provider_not_configured_is_reported_not_faked(api):
    maya = api.signup("Maya")
    cid = consent(api, maya)
    src = upload_source(api, maya, cid).json()
    r = start(api, maya, cid, src["id"], expect=503)
    assert r.json()["error"]["code"] == "provider_unavailable"
    assert api.call(maya, "GET", "/v1/me/avatar-studio").json()["provider"]["configured"] is False


def test_manual_import_validation(api):
    maya = api.signup("Maya")
    cid = consent(api, maya, provider="manual_import")
    up = lambda data, attested=True, expect=200: api.call(maya, "POST", "/v1/me/avatar-assets", expect=expect, files={"file": ("me.glb", io.BytesIO(data))}, data={"consent_id": cid, "attested": str(attested).lower()})
    assert up(make_glb(), attested=False, expect=422).json()["error"]["code"] == "attestation_required"
    assert up(make_glb(skinned=False), expect=422).json()["error"]["code"] == "model_no_skeleton"
    assert up(make_glb(image_uri="https://example.com/face.png"), expect=422).json()["error"]["code"] == "model_external_resource"
    assert up(make_glb(required=("KHR_draco_mesh_compression",)), expect=422).json()["error"]["code"] == "model_unsupported_extension"
    assert up(make_glb(joints=["Root", "Body", "Tail"]), expect=422).json()["error"]["code"] == "model_not_humanoid"
    assert up(b"PK\x03\x04 zip", expect=422).json()["error"]["code"] == "model_not_glb"
    ok = up(make_glb()).json()
    assert ok["state"] == "awaiting_owner_approval" and ok["provider"] == "manual_import"


def test_withdrawing_consent_stops_generation_and_sources_expire(api, provider, settings, clock):
    maya = api.signup("Maya")
    cid = consent(api, maya)
    src = upload_source(api, maya, cid).json()
    job = start(api, maya, cid, src["id"]).json()
    api.call(maya, "POST", f"/v1/me/avatar-consents/{cid}/withdraw")
    clock.run(0)
    assert api.call(maya, "GET", f"/v1/me/avatar-jobs/{job['id']}").json()["state"] == "cancelled"
    assert provider.submitted == []
    # retention: an expired source photo is deleted by the worker's periodic scan
    from samesky.worker import periodic

    periodic(now=utcnow() + timedelta(days=settings.avatar_source_retention_days + 1))
    assert api.call(maya, "GET", "/v1/me/avatar-studio").json()["sources"] == []
    assert api.c.get(src["imageUrl"]).status_code == 404


def test_glb_inspector_bounds():
    data = make_glb(triangles=1)
    info = glb.inspect(data)
    assert info.rig["bones"]["leftUpperArm"] == "LeftArm" and info.rig["expressions"] == "blink"
    with pytest.raises(glb.GlbRejected) as exc:
        glb.inspect(data, glb.Limits(max_bytes=10))
    assert exc.value.code == "too_large"
    with pytest.raises(glb.GlbRejected):
        glb.inspect(data[:-8])  # truncated


def test_new_people_get_valid_varied_demo_avatars(api):
    from samesky.routers.core import AVATAR_OPTIONS

    people = [api.signup(f"Person{i}") for i in range(6)]
    defs = [api.call(p, "GET", "/v1/me").json()["avatar"]["definition"] for p in people]
    for d in defs:
        assert all(d[k] in AVATAR_OPTIONS[k] for k in ("face", "skin", "hair", "hairColor", "top", "accessory"))
        assert d["skin"] == "#e8b896"  # never a guess about someone's skin
    assert len({(d["hair"], d["hairColor"], d["top"], d["face"]) for d in defs}) > 1  # not all identical
