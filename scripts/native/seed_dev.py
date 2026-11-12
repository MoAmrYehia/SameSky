"""Seeds the native-dev API with deterministic accounts and rooms through the public API (no backdoors).

Idempotent: existing accounts are signed into; rooms are created only if the owner has none with that name.

  --voice-note   Sam leaves Nora a short voice note in "Nora + Sam", so the native room has something waiting
                 in the cup (live mode). The audio is a generated two-tone chime, not anyone's voice. Needs the
                 worker running (scripts/native/start.ps1) to process it. Skipped if Sam already left one.
  --json-out F   also writes ids (users, rooms, the voice note) to F for scripts such as acceptance.ps1.
"""

from __future__ import annotations

import argparse
import io
import json
import math
import struct
import sys
import time
import wave
from pathlib import Path

import httpx

VOICE_CAPTION = "Development test voice note"
VOICE_TRANSCRIPT = "Development test voice note: a generated chime, not a voice."


def chime_wav(seconds: float = 2.5, rate: int = 22050) -> bytes:
    """A quiet two-tone chime (mono, 16-bit PCM) that the API's voice pipeline accepts."""
    frames = bytearray()
    for i in range(int(seconds * rate)):
        t = i / rate
        tone = 523.25 if t < seconds / 2 else 659.25
        envelope = min(1.0, t * 20) * math.exp(-1.2 * (t % (seconds / 2)))
        frames += struct.pack("<h", int(9000 * envelope * math.sin(2 * math.pi * tone * t)))
    buf = io.BytesIO()
    with wave.open(buf, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(bytes(frames))
    return buf.getvalue()


def seed_voice_note(c: httpx.Client, h, ids: dict[str, str], room_id: str) -> dict:
    """Sam -> Nora in the direct room: upload, wait for the worker, share with the exact audience."""
    existing = [
        m for m in c.get(f"/v1/rooms/{room_id}/moments", headers=h("sam")).json()
        if m["kind"] == "voice" and m["isMine"] and m["caption"] == VOICE_CAPTION and m["sharingState"] == "shared"
    ]
    if existing:
        moment_id = existing[0]["id"]
        print(f"voice note: exists ({moment_id})")
    else:
        asset = c.post(
            "/v1/media", headers=h("sam"),
            files={"file": ("chime.wav", chime_wav(), "audio/wav")},
            data={"kind": "audio", "source_device": "phone"},
        )
        asset.raise_for_status()
        asset_id = asset.json()["id"]
        state = "pending"
        for _ in range(90):
            state = c.get(f"/v1/media/{asset_id}", headers=h("sam")).json()["processingState"]
            if state in ("ready", "failed"):
                break
            time.sleep(1)
        if state != "ready":
            sys.exit(f"seed failed: voice note processing ended as '{state}' (is the worker running? scripts/native/start.ps1)")
        moment = c.post(
            f"/v1/rooms/{room_id}/moments", headers=h("sam"),
            json={"kind": "voice", "media_asset_id": asset_id, "caption": VOICE_CAPTION,
                  "transcript": VOICE_TRANSCRIPT, "transcript_consent": True, "ai_allowed": False},
        )
        moment.raise_for_status()
        moment_id = moment.json()["id"]
        audience = c.post(f"/v1/moments/{moment_id}/audience/resolve", headers=h("sam"), json={"recipient_ids": [ids["nora"]]})
        audience.raise_for_status()
        shared = c.post(
            f"/v1/moments/{moment_id}/share", headers=h("sam"),
            json={"recipient_ids": [ids["nora"]], "confirmation_token": audience.json()["confirmationToken"]},
        )
        shared.raise_for_status()
        print(f"voice note: shared with Nora ({moment_id})")
    seen = c.get(f"/v1/moments/{moment_id}", headers=h("nora")).json()
    has_ogg = bool((seen.get("media") or {}).get("oggUrl"))
    if not has_ogg:
        print("voice note: WARNING no Ogg copy (FFmpeg without libvorbis?); the native app can't play it")
    return {"momentId": moment_id, "from": "sam", "to": "nora", "room": "pair", "oggUrl": has_ogg}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://127.0.0.1:8000")
    ap.add_argument("--seed", default=str(Path(__file__).with_name("dev-seed.json")))
    ap.add_argument("--voice-note", action="store_true", help="Sam leaves Nora a voice note in the direct room")
    ap.add_argument("--json-out", help="write the seeded ids to this file")
    args = ap.parse_args()
    seed = json.loads(Path(args.seed).read_text(encoding="utf-8"))
    c = httpx.Client(base_url=args.api, timeout=20)
    tokens: dict[str, str] = {}
    ids: dict[str, str] = {}
    for a in seed["accounts"]:
        r = c.post("/v1/auth/signup", json={"email": a["email"], "password": a["password"], "display_name": a["name"], "time_zone": a["zone"]})
        if r.status_code == 409:
            r = c.post("/v1/auth/login", json={"email": a["email"], "password": a["password"]})
        r.raise_for_status()
        tokens[a["key"]] = r.json()["token"]
        ids[a["key"]] = r.json()["user"]["id"]
        print(f"account {a['key']}: {a['email']}")

    def h(key: str) -> dict:
        return {"Authorization": f"Bearer {tokens[key]}"}

    room_ids: dict[str, str] = {}
    for room in seed.get("rooms", []):
        existing = [x for x in c.get("/v1/rooms", headers=h(room["owner"])).json() if x["name"] == room["name"]]
        if existing:
            room_ids[room["key"]] = existing[0]["id"]
            print(f"room {room['key']}: exists ({len(existing[0]['members'])} members)")
            continue
        created = c.post("/v1/rooms", headers=h(room["owner"]), json={"kind": room["kind"], "name": room["name"]}).json()
        for member in room["members"]:
            code = c.post(f"/v1/rooms/{created['id']}/invites", headers=h(room["owner"])).json()["code"]
            joined = c.post("/v1/invites/redeem-code", headers=h(member), json={"code": code})
            joined.raise_for_status()
        room_ids[room["key"]] = created["id"]
        print(f"room {room['key']}: created with {1 + len(room['members'])} members")

    summary: dict = {"api": args.api, "users": ids, "rooms": room_ids}
    if args.voice_note:
        summary["voiceNote"] = seed_voice_note(c, h, ids, room_ids["pair"])
    print(json.dumps(summary, indent=2))
    if args.json_out:
        Path(args.json_out).write_text(json.dumps(summary, indent=2), encoding="utf-8")


if __name__ == "__main__":
    try:
        main()
    except httpx.HTTPError as e:
        sys.exit(f"seed failed: {e}")
