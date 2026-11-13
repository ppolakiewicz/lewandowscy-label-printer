#!/usr/bin/env python3
"""Generates src/LabelPrinter.Desktop/Resources/app.ico: a tilted label tag with a hole and lines of "text".

Standard library only. Small sizes are stored as 32-bit BMP (DIB) frames, 256 px as PNG, which is what
Windows and WPF expect. Shapes are drawn with signed distance functions and supersampling.

Usage: python3 tools/make_icon.py [output.ico]
"""

import math
import struct
import sys
import zlib
from pathlib import Path

SIZES = [16, 20, 24, 32, 40, 48, 64, 256]

FILL = (255, 196, 46)       # amber tag
OUTLINE = (92, 58, 6)       # dark brown edge
TEXT = (70, 45, 8)          # "text" lines
ANGLE = math.radians(28)    # tag tilt: the pointed end goes up-left

# Tag outline in unit coordinates (y down), pointing left, before the tilt.
TAG = [(0.07, 0.50), (0.30, 0.25), (0.93, 0.25), (0.93, 0.75), (0.30, 0.75)]
CORNER = 0.05
HOLE = (0.25, 0.50, 0.055)


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)
    t = max(0.0, min(1.0, t))
    return math.hypot(px - ax - t * dx, py - ay - t * dy)


def poly_sdf(px, py, pts):
    """Signed distance to a polygon: negative inside."""
    d = min(seg_dist(px, py, *pts[i], *pts[(i + 1) % len(pts)]) for i in range(len(pts)))
    inside = False
    j = len(pts) - 1
    for i in range(len(pts)):
        xi, yi = pts[i]
        xj, yj = pts[j]
        if (yi > py) != (yj > py) and px < (xj - xi) * (py - yi) / (yj - yi) + xi:
            inside = not inside
        j = i
    return -d if inside else d


def shrink(pts, amount):
    """Moves every vertex towards the centroid (good enough for rounding these convex corners)."""
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    out = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        length = math.hypot(dx, dy)
        out.append((x - dx / length * amount, y - dy / length * amount))
    return out


def sample(x, y, size, inner, lines, stroke, line_half):
    """Colour (r, g, b, a) at unit point (x, y), or None for transparent."""
    # Undo the tilt around the centre.
    cx, cy = x - 0.5, y - 0.5
    ca, sa = math.cos(-ANGLE), math.sin(-ANGLE)
    ux, uy = cx * ca - cy * sa + 0.5, cx * sa + cy * ca + 0.5

    d = poly_sdf(ux, uy, inner) - CORNER
    if d > 0:
        return None
    hx, hy, hr = HOLE
    dh = math.hypot(ux - hx, uy - hy) - hr
    if dh < 0:
        return None
    if d > -stroke or dh < stroke:
        return OUTLINE
    for (ax, ay, bx, by) in lines:
        if seg_dist(ux, uy, ax, ay, bx, by) < line_half:
            return TEXT
    return FILL


def render(size):
    stroke = max(0.045, 1.15 / size)
    line_half = max(0.03, 0.75 / size)
    if size >= 32:
        lines = [(0.40, 0.39, 0.82, 0.39), (0.40, 0.50, 0.86, 0.50), (0.40, 0.61, 0.70, 0.61)]
    elif size >= 24:
        lines = [(0.42, 0.42, 0.82, 0.42), (0.42, 0.58, 0.74, 0.58)]
    else:
        lines = [(0.45, 0.50, 0.80, 0.50)]
    inner = shrink(TAG, CORNER)
    ss = 4 if size <= 64 else 3
    pixels = []
    for py in range(size):
        row = []
        for px in range(size):
            r = g = b = a = 0.0
            for sy in range(ss):
                for sx in range(ss):
                    c = sample((px + (sx + 0.5) / ss) / size, (py + (sy + 0.5) / ss) / size,
                               size, inner, lines, stroke, line_half)
                    if c is not None:
                        r += c[0]
                        g += c[1]
                        b += c[2]
                        a += 1
            n = ss * ss
            if a == 0:
                row.append((0, 0, 0, 0))
            else:
                row.append((round(r / a), round(g / a), round(b / a), round(255 * a / n)))
        pixels.append(row)
    return pixels


def png(pixels):
    size = len(pixels)
    raw = b"".join(b"\x00" + bytes(v for p in row for v in p) for row in pixels)

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9))
            + chunk(b"IEND", b""))


def dib(pixels):
    size = len(pixels)
    mask_stride = (size + 31) // 32 * 4
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, size * size * 4 + mask_stride * size, 0, 0, 0, 0)
    color = b"".join(bytes((p[2], p[1], p[0], p[3])) for row in reversed(pixels) for p in row)
    mask = bytearray()
    for row in reversed(pixels):
        bits = bytearray(mask_stride)
        for x, p in enumerate(row):
            if p[3] < 128:
                bits[x // 8] |= 0x80 >> (x % 8)
        mask += bits
    return header + color + bytes(mask)


def main():
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else \
        Path(__file__).resolve().parent.parent / "src" / "LabelPrinter.Desktop" / "Resources" / "app.ico"
    images = []
    for size in SIZES:
        pixels = render(size)
        images.append((size, png(pixels) if size >= 256 else dib(pixels)))

    data = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    body = b""
    for size, blob in images:
        dim = 0 if size >= 256 else size
        data += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
        body += blob
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(data + body)
    print(f"Wrote {out} ({len(data + body)} bytes, sizes {SIZES})")


if __name__ == "__main__":
    main()
