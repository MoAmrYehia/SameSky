"""Side-by-side comparisons for docs/VISUAL_QA.md: concept reference | before | after (same 1672x941 framing).

    api/.venv/Scripts/python -I scripts/visual_qa_compare.py

Reads docs/references/*.png, docs/visual-qa/before/*.jpg and docs/visual-qa/after/*.jpg (from
web/scripts/capture.mjs) and writes docs/visual-qa/compare/*.jpg. Labels are drawn outside the images.
"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
QA = ROOT / "docs" / "visual-qa"
REF = ROOT / "docs" / "references"
OUT = QA / "compare"

PAIRS = [
    ("room", "01-shared-room.png", "room-1672x941.jpg", "room-1672x941.jpg"),
    ("our-day", "02-daily-recap.png", "day-1672x941.jpg", "day-1672x941.jpg"),
    ("share", "03-share-moment.png", "share-1672x941.jpg", "share-populated-1672x941.jpg"),
    ("together", "04-together-keepsakes.png", "together-1672x941.jpg", "together-1672x941.jpg"),
    ("keepsakes", "04-together-keepsakes.png", "keepsakes-1672x941.jpg", "keepsakes-1672x941.jpg"),
]

W = 836  # each panel at half the capture size
H = 470
LABEL = 44


def font(size: int):
    for name in ("segoeuib.ttf", "arialbd.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def panel(path: Path) -> Image.Image:
    im = Image.open(path).convert("RGB")
    return im.resize((W, H), Image.LANCZOS)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    f = font(24)
    for name, ref, before, after in PAIRS:
        paths = [REF / ref, QA / "before" / before, QA / "after" / after]
        if not all(p.exists() for p in paths):
            print("skip", name, [str(p) for p in paths if not p.exists()])
            continue
        sheet = Image.new("RGB", (W * 3 + 20, H + LABEL), (246, 242, 234))
        draw = ImageDraw.Draw(sheet)
        for i, (p, label) in enumerate(zip(paths, ("Concept reference", "Before", "After"))):
            x = i * (W + 10)
            sheet.paste(panel(p), (x, LABEL))
            draw.text((x + 12, 9), label, fill=(29, 33, 31), font=f)
        sheet.save(OUT / f"{name}.jpg", "JPEG", quality=86, optimize=True)
        print("compare", name)


if __name__ == "__main__":
    main()
