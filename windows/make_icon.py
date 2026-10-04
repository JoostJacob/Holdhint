#!/usr/bin/env python3
"""Draw holdhint.ico. A dark rounded square with a white H, stored as 32-bit BMP icons so NSIS can read it."""

import struct
import zlib
from pathlib import Path

OUT = Path(__file__).resolve().parent / "src" / "Holdhint.App" / "holdhint.ico"


def in_round_rect(x, y, left, top, right, bottom, radius):
    if x < left or y < top or x >= right or y >= bottom:
        return False
    radius = min(radius, (right - left) / 2, (bottom - top) / 2)
    dx = 0
    if x < left + radius:
        dx = left + radius - x
    elif x >= right - radius:
        dx = x - (right - radius) + 1
    dy = 0
    if y < top + radius:
        dy = top + radius - y
    elif y >= bottom - radius:
        dy = y - (bottom - radius) + 1
    if dx == 0 or dy == 0:
        return True
    return dx * dx + dy * dy <= radius * radius


def in_h(x, y, size):
    # Proportions that stay readable at 16px.
    left = size * 0.30
    right = size * 0.70
    bar = max(1, round(size * 0.11))
    top = size * 0.28
    bottom = size * 0.72
    mid_top = size * 0.46
    mid_bottom = size * 0.56
    if y < top or y >= bottom:
        return False
    if left <= x < left + bar or right - bar <= x < right:
        return True
    return left <= x < right and mid_top <= y < mid_bottom


def pixel(size, x, y):
    # 4x supersample.
    dark = (23, 26, 33, 255)
    light = (245, 245, 242, 255)
    margin = size * 0.07
    radius = size * 0.23
    samples = 4
    acc = [0, 0, 0, 0]
    hit = 0
    for sy in range(samples):
        for sx in range(samples):
            px = x + (sx + 0.5) / samples
            py = y + (sy + 0.5) / samples
            if not in_round_rect(px, py, margin, margin, size - margin, size - margin, radius):
                continue
            hit += 1
            color = light if in_h(px, py, size) else dark
            for i in range(4):
                acc[i] += color[i]
    if hit == 0:
        return (0, 0, 0, 0)
    return tuple(round(channel / (samples * samples)) for channel in acc)


def bmp_image(size):
    rows = []
    for y in range(size - 1, -1, -1):
        row = bytearray()
        for x in range(size):
            r, g, b, a = pixel(size, x, y)
            row += bytes((b, g, r, a))
        rows.append(bytes(row))
    xor = b"".join(rows)
    mask_stride = ((size + 31) // 32) * 4
    mask = bytes(mask_stride * size)
    header = struct.pack(
        "<IiiHHIIiiII",
        40,
        size,
        size * 2,
        1,
        32,
        0,
        len(xor),
        0,
        0,
        0,
        0,
    )
    return header + xor + mask


def main():
    sizes = [16, 32, 48, 256]
    images = [bmp_image(size) for size in sizes]
    count = len(images)
    header = struct.pack("<HHH", 0, 1, count)
    offset = 6 + 16 * count
    entries = b""
    blob = b""
    for size, image in zip(sizes, images):
        stored = 0 if size == 256 else size
        entries += struct.pack("<BBBBHHII", stored, stored, 0, 0, 1, 32, len(image), offset)
        offset += len(image)
        blob += image
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(header + entries + blob)
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
