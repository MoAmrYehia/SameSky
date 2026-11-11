"""Seeds the native-dev API with deterministic accounts and rooms through the public API (no backdoors).

Idempotent: existing accounts are signed into; rooms are created only if the owner has none with that name.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import httpx


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--api", default="http://127.0.0.1:8000")
    ap.add_argument("--seed", default=str(Path(__file__).with_name("dev-seed.json")))
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

    for room in seed.get("rooms", []):
        existing = [x for x in c.get("/v1/rooms", headers=h(room["owner"])).json() if x["name"] == room["name"]]
        if existing:
            print(f"room {room['key']}: exists ({len(existing[0]['members'])} members)")
            continue
        created = c.post("/v1/rooms", headers=h(room["owner"]), json={"kind": room["kind"], "name": room["name"]}).json()
        for member in room["members"]:
            code = c.post(f"/v1/rooms/{created['id']}/invites", headers=h(room["owner"])).json()["code"]
            joined = c.post("/v1/invites/redeem-code", headers=h(member), json={"code": code})
            joined.raise_for_status()
        print(f"room {room['key']}: created with {1 + len(room['members'])} members")
    print(json.dumps({"api": args.api, "users": ids}, indent=2))


if __name__ == "__main__":
    try:
        main()
    except httpx.HTTPError as e:
        sys.exit(f"seed failed: {e}")
