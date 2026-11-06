"""End-to-end smoke test of a running stack (API + worker [+ Vite proxy]).

Usage: api/.venv/Scripts/python scripts/smoke_live.py [base_url]
Default base_url is http://localhost:5173 (the Vite dev server, which proxies /v1 to the API).
Creates three fresh accounts, so run it against a development database only.
"""

from __future__ import annotations

import asyncio
import io
import json
import sys
import time
import uuid

import httpx

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5173"
RUN = uuid.uuid4().hex[:6]


def ok(r: httpx.Response) -> dict:
    if r.status_code >= 400:
        raise SystemExit(f"FAIL {r.request.method} {r.request.url.path} -> {r.status_code} {r.text}")
    return r.json()


def signup(c: httpx.Client, name: str, zone: str) -> dict:
    data = ok(c.post("/v1/auth/signup", json={"email": f"{name.lower()}-{RUN}@example.com", "password": "correct horse", "display_name": name, "time_zone": zone}))
    return {"id": data["user"]["id"], "token": data["token"], "h": {"Authorization": f"Bearer {data['token']}"}, "name": name}


def wait_media(c: httpx.Client, user: dict, asset_id: str) -> dict:
    for _ in range(60):
        status = ok(c.get(f"/v1/media/{asset_id}", headers=user["h"]))
        if status["processingState"] in ("ready", "failed"):
            return status
        time.sleep(1)
    raise SystemExit("FAIL media processing timed out (is the worker running?)")


def share(c: httpx.Client, user: dict, moment_id: str, recipients: list[str]) -> dict:
    res = ok(c.post(f"/v1/moments/{moment_id}/audience/resolve", headers=user["h"], json={"recipient_ids": recipients}))
    print(f"  confirm: {res['label']}")
    return ok(c.post(f"/v1/moments/{moment_id}/share", headers=user["h"], json={"recipient_ids": recipients, "confirmation_token": res["confirmationToken"]}))


async def sync_check(adam: dict, maya: dict, session_id: str) -> None:
    import websockets  # type: ignore

    ws_url = BASE.replace("http", "ws") + "/v1/realtime"
    async with websockets.connect(ws_url) as a, websockets.connect(ws_url) as m:
        for ws, u in ((a, adam), (m, maya)):
            await ws.send(json.dumps({"type": "auth", "token": u["token"]}))
            await ws.send(json.dumps({"type": "join", "sessionId": session_id}))

        async def next_of(ws, kind):
            while True:
                msg = json.loads(await asyncio.wait_for(ws.recv(), 5))
                if msg["type"] == kind:
                    return msg

        welcome = await next_of(a, "welcome")
        await next_of(m, "welcome")
        sent = time.time()
        await a.send(json.dumps({"type": "playback", "action": "play", "positionMs": 0, "baseVersion": welcome["state"]["version"]}))
        state = await next_of(m, "state")
        print(f"  play reached Maya in {1000 * (time.time() - sent):.0f} ms, version {state['version']}, paused={state['paused']}")
        await m.send(json.dumps({"type": "pose", "pose": {"tracked": False, "source": "pointer", "head": {"p": [0, 1.2, 0.6], "q": [0, 0, 0, 1]}}}))
        pose = await next_of(a, "pose")
        print(f"  Adam received Maya's pose: head {pose['pose']['head']['p']}")


def main() -> None:
    with httpx.Client(base_url=BASE, timeout=30) as c:
        print(f"Smoke test against {BASE}")
        ok(c.get("/v1/health"))
        adam, maya, lina = signup(c, "Adam", "Asia/Dubai"), signup(c, "Maya", "Asia/Shanghai"), signup(c, "Lina", "Europe/London")
        direct = ok(c.post("/v1/rooms", headers=adam["h"], json={"kind": "direct", "name": "Adam + Maya"}))
        token = ok(c.post(f"/v1/rooms/{direct['id']}/invites", headers=adam["h"]))["token"]
        ok(c.post("/v1/invites/redeem", headers=maya["h"], json={"token": token}))
        print("1. direct room created and joined")

        from PIL import Image

        buf = io.BytesIO()
        Image.new("RGB", (320, 200), (200, 140, 90)).save(buf, "JPEG")
        asset = ok(c.post("/v1/media", headers=adam["h"], files={"file": ("sunset.jpg", buf.getvalue())}, data={"kind": "image", "source_device": "phone"}))
        status = wait_media(c, adam, asset["id"])
        print(f"2. photo processed by worker: {status['processingState']} {status['width']}x{status['height']}")
        photo = ok(c.post(f"/v1/rooms/{direct['id']}/moments", headers=adam["h"], json={"kind": "photo", "media_asset_id": asset["id"], "caption": "Sunset walk by the water", "ai_allowed": True}))
        share(c, adam, photo["id"], [maya["id"]])
        note = ok(c.post(f"/v1/rooms/{direct['id']}/moments", headers=maya["h"], json={"kind": "text", "caption": "Quiet walk after work", "ai_allowed": True}))
        share(c, maya, note["id"], [adam["id"]])
        assert c.get(f"/v1/moments/{photo['id']}", headers=lina["h"]).status_code == 404
        print("3. moments shared with explicit confirmation; outsider gets 404")

        day = photo["roomDate"]
        recap = ok(c.post(f"/v1/rooms/{direct['id']}/recaps", headers=adam["h"], json={"room_date": day}))
        for _ in range(30):
            recap = ok(c.get(f"/v1/recaps/{recap['id']}", headers=adam["h"]))
            if recap["state"] not in ("queued", "generating"):
                break
            time.sleep(1)
        print(f"4. recap drafted: {recap['state']} via {recap['generatorLabel']}")
        ok(c.post(f"/v1/recaps/{recap['id']}/approve", headers=adam["h"]))
        recap = ok(c.post(f"/v1/recaps/{recap['id']}/approve", headers=maya["h"]))
        print(f"5. both approved -> {recap['state']}: “{recap['plan']['title']}”")

        ok(c.post(f"/v1/moments/{photo['id']}/replies", headers=maya["h"], json={"text": "Save me the other half of that sunset"}))
        time.sleep(3)
        inbox = ok(c.get("/v1/notifications", headers=adam["h"]))
        print(f"6. Adam's inbox: {[i['kind'] for i in inbox['items']]} (generic text: “{inbox['items'][0]['text'] if inbox['items'] else '-'}”)")
        awards = {a["milestone"]: a["earned"] for a in ok(c.get(f"/v1/rooms/{direct['id']}/awards", headers=adam["h"]))}
        print(f"7. awards: {awards}")

        session = ok(c.post(f"/v1/rooms/{direct['id']}/sessions", headers=adam["h"], json={"moment_id": photo["id"], "participant_ids": [maya["id"]]}))
        try:
            asyncio.run(sync_check(adam, maya, session["id"]))
            print("8. live session: playback + pose relay OK")
        except ModuleNotFoundError:
            print("8. (skipped live WS check: pip install websockets)")
        print("SMOKE OK")


if __name__ == "__main__":
    main()
