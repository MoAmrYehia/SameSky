"""Persistence across restart, push fallback, media pipeline: criteria 8, 10, 16, 17."""

import io
import subprocess
from pathlib import Path

from conftest import at, png_bytes, run_worker
from fastapi.testclient import TestClient

from samesky import db as dbmod
from samesky.main import create_app
from samesky.services.media import ffmpeg_path


def test_replies_and_placements_survive_restart(api, settings):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, "direct", "Us", members=[maya])
    m = api.text_moment(adam, room["id"], "Saved you a seat", at(6))
    api.share(adam, m["id"], [maya["id"]])
    reply = api.call(maya, "POST", f"/v1/moments/{m['id']}/replies", json={"text": "On my way"}).json()
    obj = api.call(maya, "POST", f"/v1/rooms/{room['id']}/objects", json={"object_type": "note", "reply_id": reply["id"], "x": 0.2, "z": 0.1}).json()
    moved = api.call(adam, "PATCH", f"/v1/objects/{obj['id']}", json={"expected_version": 1, "x": 0.3, "y": 0, "z": 0.05, "yaw": 30}).json()
    api.call(maya, "PATCH", f"/v1/objects/{obj['id']}", json={"expected_version": 1, "x": 9, "y": 0, "z": 0}, expect=409)  # stale write
    # "Restart": brand-new engine + app over the same database file.
    dbmod.init_engine(settings.database_url)
    with TestClient(create_app()) as c2:
        replies = c2.get(f"/v1/moments/{m['id']}/replies", headers=adam["h"]).json()
        assert [r["text"] for r in replies] == ["On my way"]
        objects = c2.get(f"/v1/rooms/{room['id']}/objects", headers=adam["h"]).json()
        assert objects[0]["transform"]["x"] == 0.3 and objects[0]["version"] == moved["version"] == 2


def test_object_movement_is_clamped_to_table(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, "direct", "Us", members=[maya])
    obj = api.call(adam, "POST", f"/v1/rooms/{room['id']}/objects", json={"object_type": "cup", "x": 5, "y": -3, "z": 9}).json()
    assert obj["transform"] == {"x": 0.55, "y": 0.0, "z": 0.35, "yaw": 0.0}


def test_inbox_works_without_push(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, "direct", "Us", members=[maya])
    m = api.text_moment(adam, room["id"], "Hi", at(6))
    api.share(adam, m["id"], [maya["id"]])
    api.call(maya, "POST", f"/v1/moments/{m['id']}/replies", json={"text": "Hello"})
    run_worker()
    inbox = api.call(adam, "GET", "/v1/notifications").json()
    assert inbox["unread"] == 1 and inbox["items"][0]["pushState"] == "none"
    assert inbox["items"][0]["text"] == "Someone left you a reply."  # generic, no personal content
    assert api.call(adam, "GET", "/v1/push/vapid-public-key").json()["enabled"] is False


def test_unprocessed_media_cannot_be_published(api):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, "direct", "Us", members=[maya])
    asset = api.upload(adam, png_bytes(), "image", "p.png").json()
    photo = api.call(adam, "POST", f"/v1/rooms/{room['id']}/moments", json={"kind": "photo", "media_asset_id": asset["id"]}).json()
    token = api.call(adam, "POST", f"/v1/moments/{photo['id']}/audience/resolve", json={"recipient_ids": [maya["id"]]}).json()["confirmationToken"]
    r = api.call(adam, "POST", f"/v1/moments/{photo['id']}/share", json={"recipient_ids": [maya["id"]], "confirmation_token": token}, expect=409)
    assert r.json()["error"]["code"] == "media_not_ready"


def test_filename_is_not_trusted(api):
    adam = api.signup("Adam")
    r = api.upload(adam, b"<html>not a photo</html>", "image", "holiday.jpg", expect=422)
    assert r.json()["error"]["code"] == "unsupported_format"
    r = api.upload(adam, png_bytes(), "video", "clip.mp4", expect=422)
    assert r.json()["error"]["code"] == "kind_mismatch"


def test_phone_photo_orientation_and_location_stripped(api):
    from PIL import Image

    img = Image.new("RGB", (80, 40), (10, 120, 200))
    exif = img.getexif()
    exif[274] = 6  # rotate 90° CW on display
    exif[34853] = {1: "N", 2: (25.0, 12.0, 0.0)}  # GPS IFD
    buf = io.BytesIO()
    img.save(buf, "JPEG", exif=exif)
    adam = api.signup("Adam")
    asset = api.upload(adam, buf.getvalue(), "image", "IMG_0001.HEIC.jpg", source_device="phone").json()
    run_worker()
    status = api.call(adam, "GET", f"/v1/media/{asset['id']}").json()
    assert status["processingState"] == "ready" and (status["width"], status["height"]) == (40, 80)
    derived = Image.open(io.BytesIO(api.c.get(status["url"]).content))
    assert 34853 not in derived.getexif() and 274 not in derived.getexif()


def _make_clip(tmp: Path, name: str, codec_args: list[str], extra: list[str] | None = None) -> bytes:
    out = tmp / name
    exe = ffmpeg_path()
    subprocess.run([exe, "-y", "-hide_banner", "-f", "lavfi", "-i", "testsrc=size=320x180:rate=30", "-f", "lavfi", "-i", "sine=frequency=440",
                    "-t", "3", *codec_args, *(extra or []), str(out)], check=True, capture_output=True)
    return out.read_bytes()


def test_action_camera_clip_processed_to_playback_profile(api, tmp_path):
    adam, maya = api.signup("Adam"), api.signup("Maya")
    room = api.room(adam, "direct", "Us", members=[maya])
    data = _make_clip(tmp_path, "GX010042.MP4", ["-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac"], ["-metadata:s:v", "rotate=90"])
    asset = api.upload(adam, data, "video", "GX010042.MP4", source_device="action_camera").json()
    run_worker()
    status = api.call(adam, "GET", f"/v1/media/{asset['id']}").json()
    assert status["processingState"] == "ready", status
    assert status["inputProfile"]["videoCodec"] == "h264"
    clip = api.call(adam, "POST", f"/v1/rooms/{room['id']}/moments", json={"kind": "clip", "media_asset_id": asset["id"], "caption": "Sunset ride"}).json()
    shared = api.share(adam, clip["id"], [maya["id"]])
    view = api.call(maya, "GET", f"/v1/moments/{shared['id']}").json()
    assert view["media"]["url"] and view["media"]["durationMs"] <= 15500
    resp = api.c.get(view["media"]["url"])
    assert resp.status_code == 200 and resp.headers["content-type"] == "video/mp4"


def test_unsupported_codec_gets_useful_message(api, tmp_path):
    adam = api.signup("Adam")
    data = _make_clip(tmp_path, "old.mov", ["-c:v", "mjpeg", "-c:a", "pcm_s16le"])
    asset = api.upload(adam, data, "video", "old.mov", source_device="camera").json()
    run_worker()
    status = api.call(adam, "GET", f"/v1/media/{asset['id']}").json()
    assert status["processingState"] == "failed"
    assert status["safeError"].startswith("unsupported_codec") and "H.264" in status["safeError"]


def test_headset_voice_note_gets_an_ogg_twin_for_native_playback(api, tmp_path):
    """A WAV recorded in the Quest app is processed like any voice note, plus an Ogg copy Unity can decode."""
    adam, maya, sam = api.signup("Adam"), api.signup("Maya"), api.signup("Sam")
    room = api.room(adam, "direct", "Us", members=[maya])
    wav = tmp_path / "reply.wav"
    subprocess.run([ffmpeg_path(), "-y", "-hide_banner", "-f", "lavfi", "-i", "sine=frequency=330", "-t", "2",
                    "-ac", "1", "-ar", "24000", str(wav)], check=True, capture_output=True)
    asset = api.upload(adam, wav.read_bytes(), "audio", "reply.wav", source_device="headset").json()
    run_worker()
    voice = api.call(adam, "POST", f"/v1/rooms/{room['id']}/moments", json={"kind": "voice", "media_asset_id": asset["id"], "caption": "Back home"}).json()
    shared = api.share(adam, voice["id"], [maya["id"]])
    view = api.call(maya, "GET", f"/v1/moments/{shared['id']}").json()
    assert view["media"]["url"] and view["media"]["oggUrl"]
    resp = api.c.get(view["media"]["oggUrl"])
    assert resp.status_code == 200 and resp.headers["content-type"] == "audio/ogg" and resp.content[:4] == b"OggS"
    # the Ogg twin is authorized exactly like the original: an outsider's forged link fails
    forged = view["media"]["oggUrl"].replace(f"u={maya['id']}", f"u={sam['id']}")
    assert api.c.get(forged).status_code == 403
