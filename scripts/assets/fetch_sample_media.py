"""Download the real photographs and footage the sample day is built from.

The sources are listed in scripts/assets/sample_media.json: for each one the file's page, its direct URL, licence,
author and SHA-256. Every file goes into a new folder of its own under .asset-cache/sample-media/ (git-ignored);
nothing is unpacked or executed. A file whose SHA-256 is recorded is checked against it; for a new source the hash is
printed so it can be recorded.

    python scripts/assets/fetch_sample_media.py            # fetch what is missing
    python scripts/assets/fetch_sample_media.py --list     # show the sources without downloading

Then build the app's copies with scripts/assets/build_real_sample_media.py.
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = Path(__file__).with_name("sample_media.json")
CACHE = ROOT / ".asset-cache" / "sample-media"
MAX_BYTES = 400 * 1024 * 1024
USER_AGENT = "asset-fetch-script/1.0"   # generic on purpose: no project or personal details leave this machine


def sha256(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def fetch(item: dict) -> Path:
    folder = CACHE / item["id"]
    target = folder / item["file"]
    if target.exists():
        return target
    folder.mkdir(parents=True, exist_ok=True)
    request = urllib.request.Request(item["url"], headers={"User-Agent": USER_AGENT})
    part = target.with_suffix(target.suffix + ".part")
    for attempt in range(1, 7):
        try:
            done = 0
            with urllib.request.urlopen(request, timeout=120) as response, part.open("wb") as out:
                while True:
                    block = response.read(1 << 20)
                    if not block:
                        break
                    done += len(block)
                    if done > MAX_BYTES:
                        part.unlink(missing_ok=True)
                        raise SystemExit(f"{item['id']}: larger than {MAX_BYTES // (1024 * 1024)} MB, stopped")
                    out.write(block)
            part.replace(target)
            return target
        except urllib.error.HTTPError as e:
            # the file server asks clients to slow down now and then: wait as long as it says, then try again
            if e.code not in (429, 503) or attempt == 6:
                raise
            wait = min(180, int(e.headers.get("Retry-After") or 0) or 20 * attempt)
            print(f"{item['id']}: the server asked to slow down (HTTP {e.code}); waiting {wait} s (try {attempt} of 6)", flush=True)
            time.sleep(wait)
    return target


def main() -> None:
    items = json.loads(MANIFEST.read_text(encoding="utf-8"))["items"]
    if "--list" in sys.argv:
        for item in items:
            print(f"{item['id']:18} {item['licence']:16} {item.get('bytes', 0) / 1048576:6.1f} MB  {item['page']}")
        return
    failed = False
    for item in items:
        fresh = not (CACHE / item["id"] / item["file"]).exists()
        path = fetch(item)
        if fresh:
            time.sleep(3)   # one file at a time, unhurried
        digest = sha256(path)
        expected = item.get("sha256") or ""
        if expected and digest != expected:
            print(f"{item['id']}: SHA-256 differs from the recorded one. The source may have changed: look at it before using it.")
            print(f"  recorded {expected}\n  now      {digest}")
            failed = True
        else:
            print(f"{item['id']}: {path.stat().st_size / 1048576:.1f} MB  sha256 {digest}{'' if expected else '  (not recorded yet)'}")
    if failed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
