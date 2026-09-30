"""Generates the SteamScrup application icon (Assets/app.ico + preview PNG).

Run with the bundled Python:
    python tools/make_icon.py
"""
from __future__ import annotations

import os
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "src", "SteamScrup", "Assets")

# Steam-ish palette, kept readable on both light and dark backgrounds.
BG_TOP = (27, 40, 56, 255)       # #1B2838
BG_BOTTOM = (38, 58, 82, 255)    # #263A52
SHIELD = (102, 192, 244, 255)    # #66C0F4
SHIELD_EDGE = (44, 122, 170, 255)
CLEAN = (255, 255, 255, 255)

SS = 1024  # supersampled working size


def rounded_shield(size: int) -> Image.Image:
    """A shield silhouette: square top, rounded bottom converging to a point."""
    w = h = size
    img = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(img)

    # Shield outline as a polygon with a curved bottom, approximated by many points.
    top = int(h * 0.10)
    bottom = int(h * 0.93)
    left = int(w * 0.12)
    right = int(w * 0.88)
    shoulder = int(h * 0.55)

    pts: list[tuple[float, float]] = []
    pts.append((left, top + int(h * 0.06)))          # top-left corner
    pts.append((w / 2, top))                          # top peak
    pts.append((right, top + int(h * 0.06)))          # top-right corner
    pts.append((right, shoulder))
    # Right curve down to the bottom point.
    steps = 48
    for i in range(steps + 1):
        t = i / steps
        x = right + (w / 2 - right) * (t ** 1.6)
        y = shoulder + (bottom - shoulder) * t
        pts.append((x, y))
    for i in range(steps + 1):
        t = 1 - i / steps
        x = left + (w / 2 - left) * (t ** 1.6)
        y = shoulder + (bottom - shoulder) * t
        pts.append((x, y))

    d.polygon(pts, fill=255)
    return img


def build_master() -> Image.Image:
    img = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))

    # --- background: rounded square with a vertical gradient
    bg_mask = Image.new("L", (SS, SS), 0)
    bd = ImageDraw.Draw(bg_mask)
    inset = int(SS * 0.045)
    bd.rounded_rectangle([inset, inset, SS - inset, SS - inset],
                         radius=int(SS * 0.22), fill=255)

    grad = Image.new("RGBA", (SS, SS))
    gd = ImageDraw.Draw(grad)
    for y in range(SS):
        t = y / (SS - 1)
        gd.line(
            [(0, y), (SS, y)],
            fill=tuple(int(BG_TOP[i] + (BG_BOTTOM[i] - BG_TOP[i]) * t) for i in range(4)),
        )
    img.paste(grad, (0, 0), bg_mask)

    # --- shield
    shield_mask = rounded_shield(SS)
    shield_mask = shield_mask.resize((int(SS * 0.66), int(SS * 0.72)), Image.LANCZOS)
    sw, sh = shield_mask.size
    ox, oy = (SS - sw) // 2, int(SS * 0.145)

    # subtle darker edge for definition
    edge = shield_mask.filter(ImageFilter.MaxFilter(13))
    img.paste(Image.new("RGBA", (sw, sh), SHIELD_EDGE), (ox, oy), edge)
    img.paste(Image.new("RGBA", (sw, sh), SHIELD), (ox, oy), shield_mask)

    # --- three "clean sweep" strokes inside the shield
    # Draw on a full-size layer, then mask by the shield placed at its offset.
    shield_full = Image.new("L", (SS, SS), 0)
    shield_full.paste(shield_mask, (ox, oy))

    overlay = Image.new("RGBA", (SS, SS), (0, 0, 0, 0))
    od = ImageDraw.Draw(overlay)
    cx = SS / 2
    y0 = oy + int(sh * 0.30)
    gap = int(sh * 0.175)
    stroke_h = int(sh * 0.105)
    for i in range(3):
        y = y0 + i * gap
        length = int(sw * (0.50 - i * 0.07))
        x0 = cx - length / 2
        od.rounded_rectangle([x0, y, x0 + length, y + stroke_h],
                             radius=stroke_h // 2, fill=CLEAN)

    overlay.putalpha(ImageChops.multiply(overlay.getchannel("A"), shield_full))
    img.alpha_composite(overlay)

    return img


def main() -> None:
    os.makedirs(ASSETS, exist_ok=True)
    master = build_master()

    # Preview at a comfortable size.
    master.resize((256, 256), Image.LANCZOS).save(
        os.path.join(ASSETS, "app-preview.png"))

    # Windows ICO with the sizes Explorer and the taskbar actually use.
    sizes = [(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (24, 24), (16, 16)]
    frames = [master.resize(s, Image.LANCZOS) for s in sizes]
    ico_path = os.path.join(ASSETS, "app.ico")
    frames[0].save(ico_path, format="ICO", sizes=sizes, append_images=frames[1:])

    print("wrote", ico_path)
    print("wrote", os.path.join(ASSETS, "app-preview.png"))


if __name__ == "__main__":
    main()
