"""Download the CC0 source assets SameSky's visuals are built from (Poly Haven), with provenance.

Run:  api/.venv/Scripts/python -I scripts/assets/fetch_polyhaven.py
Downloads go to .asset-cache/ (git-ignored). Only optimized derivatives are committed (see docs/ASSET_MANIFEST.md).
Writes .asset-cache/provenance.json: asset id, title, authors, license, source page, files and SHA-256.
"""

from __future__ import annotations

import hashlib
import json
import sys
import time
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CACHE = ROOT / ".asset-cache"
UA = {"User-Agent": "SameSky-asset-pipeline/1.0 (+https://github.com/MoAmrYehia/SameSky)"}

# id -> what we derive from it
HDRIS = {
    "ballawley_park": {"want": ["tonemapped"], "use": "Maya's park photo (perspective crop)"},
    "golden_bay": {"want": ["tonemapped"], "use": "Adam's waterfront photo + 8 s camera clip (perspective crops)"},
    "courtyard_night": {"want": ["tonemapped"], "use": "Lina's courtyard photo (perspective crop)"},
    "rogland_sunset": {"want": ["tonemapped"], "use": "Adam's dunes photo (perspective crop)"},
    "dusseldorf_bridge": {"want": ["tonemapped"], "use": "Window skyline plate (cylindrical crop)"},
    "lythwood_lounge": {"want": ["hdr:1k", "tonemapped"], "use": "Room image-based lighting (PMREM) + companion backdrop"},
}
MODELS = {
    "round_wooden_table_01": "Shared round table",
    "ArmChair_01": "Partner's upholstered armchair",
    "round_wooden_table_02": "Lamp side table",
    "potted_plant_04": "Window-sill plants",
    "ceramic_vase_04": "Side-table vase",
}
TEXTURES = {
    "herringbone_parquet": "Floor",
    "wool_boucle": "Rug and cushions",
    "clay_plaster": "Walls",
    "american_walnut_veneer": "Frames, lamp base, keepsake plinths (tinted warm: the scan is a grey-washed veneer)",
    "knitted_fleece": "Avatar sweater detail (normal map)",
}
TEXTURE_MAPS = ["Diffuse", "nor_gl", "Rough", "AO"]


def get_json(url: str):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60) as r:
        return json.loads(r.read().decode("utf-8"))


def download(url: str, dest: Path) -> dict:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if not dest.exists():
        tmp = dest.with_suffix(dest.suffix + ".part")
        with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=600) as r, open(tmp, "wb") as f:
            while chunk := r.read(1 << 20):
                f.write(chunk)
        tmp.replace(dest)
        time.sleep(0.3)  # be gentle with the CDN
    digest = hashlib.sha256(dest.read_bytes()).hexdigest()
    return {"file": str(dest.relative_to(CACHE)).replace("\\", "/"), "url": url, "bytes": dest.stat().st_size, "sha256": digest}


def info(asset_id: str) -> dict:
    meta = get_json(f"https://api.polyhaven.com/info/{asset_id}")
    return {
        "id": asset_id,
        "title": meta.get("name", asset_id),
        "authors": sorted((meta.get("authors") or {}).keys()),
        "license": "CC0 1.0 (public domain)",
        "source": f"https://polyhaven.com/a/{asset_id}",
    }


def main() -> None:
    CACHE.mkdir(exist_ok=True)
    record = {"retrieved": time.strftime("%Y-%m-%d"), "hdris": [], "models": [], "textures": []}
    for asset_id, spec in HDRIS.items():
        files = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        entry = {**info(asset_id), "use": spec["use"], "files": []}
        for want in spec["want"]:
            if want == "tonemapped":
                url = files["tonemapped"]["url"]
                entry["files"].append(download(url, CACHE / "hdri" / f"{asset_id}_tonemapped.jpg"))
            else:
                res = want.split(":")[1]
                url = files["hdri"][res]["hdr"]["url"]
                entry["files"].append(download(url, CACHE / "hdri" / f"{asset_id}_{res}.hdr"))
        record["hdris"].append(entry)
        print("hdri", asset_id, [f["bytes"] for f in entry["files"]])
    for asset_id, use in MODELS.items():
        files = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        g = files["gltf"]["1k"]["gltf"]
        folder = CACHE / "models" / asset_id
        entry = {**info(asset_id), "use": use, "files": [download(g["url"], folder / Path(g["url"]).name)]}
        for rel, inc in (g.get("include") or {}).items():
            entry["files"].append(download(inc["url"], folder / rel))
        record["models"].append(entry)
        print("model", asset_id, sum(f["bytes"] for f in entry["files"]))
    for asset_id, use in TEXTURES.items():
        files = get_json(f"https://api.polyhaven.com/files/{asset_id}")
        entry = {**info(asset_id), "use": use, "files": []}
        for map_name in TEXTURE_MAPS:
            if map_name in files and "1k" in files[map_name]:
                fmt = "jpg" if "jpg" in files[map_name]["1k"] else "png"
                url = files[map_name]["1k"][fmt]["url"]
                entry["files"].append(download(url, CACHE / "textures" / asset_id / f"{map_name}.{fmt}"))
        record["textures"].append(entry)
        print("texture", asset_id, [Path(f["file"]).name for f in entry["files"]])
    (CACHE / "provenance.json").write_text(json.dumps(record, indent=2), encoding="utf-8")
    total = sum(f["bytes"] for group in ("hdris", "models", "textures") for e in record[group] for f in e["files"])
    print(f"done: {total / 1e6:.1f} MB in {CACHE}")


if __name__ == "__main__":
    sys.exit(main())
