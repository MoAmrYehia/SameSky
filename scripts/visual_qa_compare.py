"""Side-by-side comparisons for docs/VISUAL_QA.md (same framing in every panel).

    api/.venv/Scripts/python -I scripts/visual_qa_compare.py

Pass 1: concept reference | before | after, from docs/references/*.png, docs/visual-qa/before/*.jpg and
docs/visual-qa/after/*.jpg. Pass 2 (realistic scenes, 8 Nov 2026): pass 1 | pass 2, from
docs/visual-qa/pass1/*.jpg (the captures committed after pass 1) and docs/visual-qa/after/*.jpg.
Writes docs/visual-qa/compare/*.jpg. Labels are drawn outside the images.
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

# pass 2: (output name, pass-1 file, pass-2 file)
PASS2 = [
    ("pass2-room", "room-1672x941.jpg", "room-1672x941.jpg"),
    ("pass2-room-1440", "room-1440x900.jpg", "room-1440x900.jpg"),
    ("pass2-our-day", "day-1672x941.jpg", "day-1672x941.jpg"),
    ("pass2-together", "together-1672x941.jpg", "together-1672x941.jpg"),
    ("pass2-share", "share-populated-1672x941.jpg", "share-populated-1672x941.jpg"),
    ("pass2-settings", "settings-1672x941.jpg", "settings-1672x941.jpg"),
]
PASS2_MOBILE = [
    ("pass2-mobile", ["room-390x844.jpg", "day-390x844.jpg", "together-390x844.jpg", "share-populated-390x844.jpg"]),
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
        # pass 1's own result: pass1/ holds the captures committed after pass 1 (after/ now holds pass 2)
        first = QA / "pass1" / after
        paths = [REF / ref, QA / "before" / before, first if first.exists() else QA / "after" / after]
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

    for name, before, after in PASS2:
        paths = [QA / "pass1" / before, QA / "after" / after]
        if not all(p.exists() for p in paths):
            print("skip", name, [str(p) for p in paths if not p.exists()])
            continue
        sheet = Image.new("RGB", (W * 2 + 10, H + LABEL), (246, 242, 234))
        draw = ImageDraw.Draw(sheet)
        for i, (p, label) in enumerate(zip(paths, ("Pass 1 (6 Nov)", "Pass 2 (8 Nov): neutral light, satin wood, quieter UI"))):
            x = i * (W + 10)
            sheet.paste(panel(p), (x, LABEL))
            draw.text((x + 12, 9), label, fill=(29, 33, 31), font=f)
        sheet.save(OUT / f"{name}.jpg", "JPEG", quality=86, optimize=True)
        print("compare", name)

    # phones: pass 1 on the top row, pass 2 below, each at half size
    pw, ph = 195, 422
    for name, files in PASS2_MOBILE:
        rows = [[QA / "pass1" / x for x in files], [QA / "after" / x for x in files]]
        if not all(p.exists() for row in rows for p in row):
            print("skip", name)
            continue
        sheet = Image.new("RGB", (len(files) * (pw + 10) + 150, 2 * (ph + 10)), (246, 242, 234))
        draw = ImageDraw.Draw(sheet)
        for r, (row, label) in enumerate(zip(rows, ("Pass 1", "Pass 2"))):
            y = r * (ph + 10)
            draw.text((12, y + ph // 2 - 12), label, fill=(29, 33, 31), font=f)
            for c, p in enumerate(row):
                sheet.paste(Image.open(p).convert("RGB").resize((pw, ph), Image.LANCZOS), (150 + c * (pw + 10), y))
        sheet.save(OUT / f"{name}.jpg", "JPEG", quality=86, optimize=True)
        print("compare", name)


if __name__ == "__main__":
    main()
