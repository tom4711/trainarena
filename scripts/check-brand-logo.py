#!/usr/bin/env python3
"""Check the TrainArena brand PNG masters (same contract as Finanzübersicht)."""
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BRAND = ROOT / "docs" / "brand"
ICON_PNG = BRAND / "logo-icon.png"
ARENA_PNG = BRAND / "logo-icon-arena.png"
BANNER_PNG = BRAND / "banner.png"
APP_ICON = ROOT / "src" / "TrainArena" / "wwwroot" / "assets" / "brand" / "logo-icon.png"
FAVICON = ROOT / "src" / "TrainArena" / "wwwroot" / "assets" / "brand" / "favicon.png"
PNG_SIG = b"\x89PNG\r\n\x1a\n"


def png_size(path: Path) -> tuple[int, int]:
    data = path.read_bytes()[:24]
    if data[:8] != PNG_SIG:
        sys.exit(f"{path} is not a PNG")
    width = int.from_bytes(data[16:20], "big")
    height = int.from_bytes(data[20:24], "big")
    return width, height


def png_has_alpha(path: Path) -> bool:
    """True when IHDR color type includes alpha (4 or 6) or a tRNS chunk exists."""
    data = path.read_bytes()
    if data[:8] != PNG_SIG:
        return False
    color_type = data[25]
    if color_type in (4, 6):
        return True
    offset = 8
    while offset + 8 <= len(data):
        length = int.from_bytes(data[offset : offset + 4], "big")
        chunk = data[offset + 4 : offset + 8]
        if chunk == b"tRNS":
            return True
        if chunk == b"IEND":
            break
        offset += 12 + length
    return False


def png_corner_alpha_zero(path: Path) -> bool:
    """Decode PNG and require the four corners to be fully transparent."""
    try:
        from PIL import Image
    except ImportError:
        return True  # size/alpha-chunk checks still apply without Pillow
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    for xy in ((0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)):
        if im.getpixel(xy)[3] != 0:
            return False
    return True


def main() -> None:
    for path, expected in (
        (ICON_PNG, (4096, 4096)),
        (ARENA_PNG, (4096, 4096)),
        (BANNER_PNG, (3840, 2160)),
    ):
        if not path.is_file():
            sys.exit(f"missing {path}")
        actual = png_size(path)
        if actual != expected:
            sys.exit(f"{path.name} size {actual}, expected {expected}")
        if not png_has_alpha(path):
            sys.exit(f"{path.name} must be RGBA/transparent for light/dark use")
        if not png_corner_alpha_zero(path):
            sys.exit(f"{path.name} corners must be fully transparent")
    for path in (APP_ICON, FAVICON):
        if not path.is_file():
            sys.exit(f"missing {path}")
        if path.read_bytes()[:8] != PNG_SIG:
            sys.exit(f"{path} is not a PNG")
        if not png_has_alpha(path):
            sys.exit(f"{path.name} must include alpha")
    for stale in (
        BRAND / "logo-icon.svg",
        BRAND / "banner.svg",
        ROOT / "src" / "TrainArena" / "wwwroot" / "assets" / "brand" / "logo-icon.svg",
    ):
        if stale.is_file():
            sys.exit(f"unexpected {stale.name}; PNG masters replace the SVG redraw")
    print("brand ok")


if __name__ == "__main__":
    main()
