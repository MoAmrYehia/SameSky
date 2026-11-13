"""Puts fixed seated renders from an acceptance run on one sheet, each with a caption.

    api/.venv/Scripts/python scripts/native/contact_sheet.py <run folder> <out.jpg> [--columns 4] name=caption ...

`name` is a render without "seat-" and ".png" (hands, tour-market, ...). The pictures are what the app drew in the
simulator; the sheet only shrinks them and writes the caption underneath.
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

CELL_W, CELL_H, PAD, CAPTION_H = 800, 450, 18, 52
PAPER, INK = (246, 242, 234), (38, 44, 40)


def font(size: int) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main() -> None:
    args = sys.argv[1:]
    columns = 4
    if "--columns" in args:
        i = args.index("--columns")
        columns = int(args[i + 1])
        del args[i : i + 2]
    if len(args) < 3:
        sys.exit(__doc__)
    folder, out = Path(args[0]), Path(args[1])
    items = [a.split("=", 1) for a in args[2:]]
    rows = (len(items) + columns - 1) // columns
    sheet = Image.new("RGB", (PAD + columns * (CELL_W + PAD), PAD + rows * (CELL_H + CAPTION_H + PAD)), PAPER)
    draw = ImageDraw.Draw(sheet)
    face = font(26)
    for n, (name, caption) in enumerate(items):
        source = folder / f"seat-{name}.png"
        if not source.exists():
            sys.exit(f"missing render: {source}")
        x = PAD + (n % columns) * (CELL_W + PAD)
        y = PAD + (n // columns) * (CELL_H + CAPTION_H + PAD)
        with Image.open(source) as picture:
            sheet.paste(picture.convert("RGB").resize((CELL_W, CELL_H), Image.LANCZOS), (x, y))
        draw.text((x + 4, y + CELL_H + 10), caption, fill=INK, font=face)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out, "JPEG", quality=86, optimize=True)
    print(f"wrote {out} ({out.stat().st_size // 1024} KB, {sheet.width}x{sheet.height}, {len(items)} renders)")


if __name__ == "__main__":
    main()
