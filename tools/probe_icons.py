"""Renders candidate icon glyphs from the Windows icon fonts into a contact sheet.

Codepoints alone are not enough: a glyph can exist yet look nothing like what its
name suggests, and Private Use Area mappings differ between Segoe Fluent Icons and
Segoe MDL2 Assets. Rendering them makes the choice verifiable by eye.

    python tools/probe_icons.py
"""
from __future__ import annotations

import os
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "tools")

FONTS = [
    ("Segoe Fluent Icons", "C:/Windows/Fonts/SegoeIcons.ttf"),
    ("Segoe MDL2 Assets", "C:/Windows/Fonts/segmdl2.ttf"),
]

# Candidate codepoints grouped by the meaning we want.
CANDIDATES: list[tuple[str, int]] = [
    # category icons
    ("overview/chart", 0xE9D2), ("overview/chart2", 0xE9D9), ("overview/chart3", 0xE904),
    ("folder/game", 0xE8B7), ("folder2", 0xE838), ("gamepad", 0xE7FC), ("game", 0xE7FC),
    ("workshop/puzzle", 0xEA86), ("toolbox", 0xE90F), ("package", 0xE7B8),
    ("save/database", 0xEE94), ("save2", 0xE74E), ("history", 0xE81C),
    ("directx/gpu", 0xE950), ("display", 0xE7F4), ("cpu", 0xE950),
    ("temp/clock", 0xE823), ("broom", 0xEA99), ("clean", 0xE894), ("delete", 0xE74D),
    ("crash/bug", 0xEBE8), ("warning", 0xE7BA), ("report", 0xEB90),
    ("settings/gear", 0xE713), ("search", 0xE721), ("refresh", 0xE72C),
    ("selectall", 0xE8B3), ("deselect", 0xE8E6), ("shield", 0xEA18),
    ("check", 0xE73E), ("checkmark", 0xE8FB), ("info", 0xE946),
    ("stop/cancel", 0xE711), ("play", 0xE768), ("shieldok", 0xE83D),
    ("lightning", 0xE945), ("flash", 0xE7E7), ("disk", 0xEDA2),
    ("library", 0xE8F1), ("apps", 0xE71D), ("browse", 0xE8B7),
]

CELL = 96
LABEL_H = 30
COLS = 6


def render_font(font_name: str, font_path: str) -> Image.Image:
    size = 44
    try:
        font = ImageFont.truetype(font_path, size)
    except OSError as exc:
        print(f"cannot load {font_name}: {exc}")
        return None

    rows = (len(CANDIDATES) + COLS - 1) // COLS
    img = Image.new("RGB", (COLS * CELL, rows * (CELL + LABEL_H) + 44), "white")
    d = ImageDraw.Draw(img)
    d.text((8, 10), f"{font_name}  ({os.path.basename(font_path)})", fill="black")

    small = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 12)
    for i, (name, cp) in enumerate(CANDIDATES):
        cx = (i % COLS) * CELL
        cy = 44 + (i // COLS) * (CELL + LABEL_H)
        d.rectangle([cx + 2, cy + 2, cx + CELL - 2, cy + CELL - 2], outline="#cccccc")

        # Missing glyphs render as .notdef (an empty box); that is the signal we want.
        d.text((cx + CELL / 2, cy + CELL / 2), chr(cp), font=font, fill="black", anchor="mm")
        d.text((cx + 6, cy + CELL + 3), f"{name}", fill="#333333", font=small)
        d.text((cx + 6, cy + CELL + 16), f"U+{cp:04X}", fill="#999999", font=small)

    return img


def main() -> None:
    sheets = []
    for name, path in FONTS:
        if not os.path.exists(path):
            print(f"missing font file: {path}")
            continue
        sheet = render_font(name, path)
        if sheet is None:
            continue
        out = os.path.join(OUT, f"icons-{name.split()[-1].lower()}.png")
        sheet.save(out)
        print("wrote", out, sheet.size)
        sheets.append(sheet)

    if len(sheets) == 2:
        w = max(s.width for s in sheets)
        h = sum(s.height for s in sheets) + 20
        combo = Image.new("RGB", (w, h), "white")
        y = 0
        for s in sheets:
            combo.paste(s, (0, y))
            y += s.height + 20
        out = os.path.join(OUT, "icons-contact-sheet.png")
        combo.save(out)
        print("wrote", out, combo.size)


if __name__ == "__main__":
    main()
