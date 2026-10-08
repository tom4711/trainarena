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
    for path in (APP_ICON, FAVICON):
        if not path.is_file():
            sys.exit(f"missing {path}")
        if path.read_bytes()[:8] != PNG_SIG:
            sys.exit(f"{path} is not a PNG")
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
