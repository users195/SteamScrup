"""Checks whether candidate icon glyphs really exist in the Windows icon fonts.

PIL renders a missing glyph as .notdef, and so does WPF, so "there is a box" is not
evidence. This compares each codepoint against a guaranteed-missing codepoint: if the
two bitmaps are byte-identical, the glyph is absent.

    python tools/check_glyphs.py
"""
from __future__ import annotations

import hashlib
import os
from PIL import Image, ImageDraw, ImageFont

FONTS = {
    "Segoe Fluent Icons": "C:/Windows/Fonts/SegoeIcons.ttf",
    "Segoe MDL2 Assets": "C:/Windows/Fonts/segmdl2.ttf",
    "Segoe UI Symbol": "C:/Windows/Fonts/seguisym.ttf",
}

# Codepoints actually used by the app (mirrors UI/Icons.xaml).
USED = {
    "GlyphOverview": 0xE9D2,
    "GlyphCommon": 0xE8B7,
    "GlyphDirectX": 0xE950,
    "GlyphShader": 0xEDA2,
    "GlyphWorkshop": 0xEA86,
    "GlyphSteamCache": 0xE823,
    "GlyphCrash": 0xEBE8,
    "GlyphUserData": 0xE74E,
    "GlyphSettings": 0xE713,
    "GlyphScan": 0xE72C,
    "GlyphClean": 0xE894,
    "GlyphStop": 0xE711,
    "GlyphSelectAll": 0xE8B3,
    "GlyphSelectNone": 0xE8E6,
    "GlyphSafeOnly": 0xEA18,
    "GlyphSearch": 0xE721,
    "GlyphOpen": 0xE8B7,
    "GlyphRefresh": 0xE72C,
    "GlyphInfo": 0xE946,
    "GlyphWarning": 0xE7BA,
    "GlyphCheck": 0xE8FB,
    "GlyphError": 0xE711,
    "GlyphShield": 0xEA18,
}

# A Private Use Area codepoint that no icon font defines.
MISSING = 0xE000

SIZE = 48


def bitmap(font: ImageFont.FreeTypeFont, cp: int) -> bytes:
    img = Image.new("L", (SIZE * 2, SIZE * 2), 255)
    ImageDraw.Draw(img).text((SIZE // 2, SIZE // 2), chr(cp), font=font, fill=0)
    return hashlib.sha256(img.tobytes()).hexdigest()


def main() -> None:
    print(f"{'glyph':<18} {'cp':>7}  " + "  ".join(f"{n.split()[-1]:>12}" for n in FONTS))
    print("-" * 78)

    for path in FONTS.values():
        if not os.path.exists(path):
            print(f"missing font file: {path}")
            return

    loaded = {n: ImageFont.truetype(p, SIZE) for n, p in FONTS.items()}
    missing_sig = {n: bitmap(f, MISSING) for n, f in loaded.items()}

    absent_total = 0
    for name, cp in USED.items():
        cells = []
        for font_name, font in loaded.items():
            sig = bitmap(font, cp)
            present = sig != missing_sig[font_name]
            cells.append("present" if present else "ABSENT")
            if font_name == "Segoe Fluent Icons" and not present:
                absent_total += 1
        print(f"{name:<18} U+{cp:04X}  " + "  ".join(f"{c:>12}" for c in cells))

    print("-" * 78)
    print(f"absent in Segoe Fluent Icons: {absent_total} / {len(USED)}")


if __name__ == "__main__":
    main()
