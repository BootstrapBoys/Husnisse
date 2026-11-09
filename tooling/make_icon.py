#!/usr/bin/env python3
"""Generate resources/icon.png without external image libraries."""
import math
import struct
import zlib

W = H = 256
SS = 4  # supersampling factor
S = W * SS


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def ellipse(x, y, cx, cy, rx, ry):
    return ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0


def triangle(x, y, a, b, c):
    def sign(p1, p2, p3):
        return (p1[0] - p3[0]) * (p2[1] - p3[1]) - (p2[0] - p3[0]) * (p1[1] - p3[1])

    d1 = sign((x, y), a, b)
    d2 = sign((x, y), b, c)
    d3 = sign((x, y), c, a)
    has_neg = d1 < 0 or d2 < 0 or d3 < 0
    has_pos = d1 > 0 or d2 > 0 or d3 > 0
    return not (has_neg and has_pos)


def shade(x, y):
    """Return RGB at supersampled pixel (x, y)."""
    u, v = x / S, y / S

    # background: deep green-black with a warm pool of light behind the bowl
    c = lerp((26, 36, 26), (8, 12, 8), v)
    d = math.hypot(u - 0.50, v - 0.55)
    if d < 0.62:
        c = lerp(c, (64, 52, 30), (1.0 - d / 0.62) ** 2 * 0.55)

    # red nisse cap, behind the eyes
    cap = [((0.35, 0.34), (0.65, 0.34), (0.52, 0.14))]
    if triangle(u, v, *cap[0]):
        c = lerp((150, 44, 36), (96, 24, 20), (v - 0.14) / 0.20)
    if 0.325 <= v <= 0.355 and 0.35 <= u <= 0.65:
        c = (206, 192, 170)

    # eyes
    for ex in (0.415, 0.585):
        if ellipse(u, v, ex, 0.415, 0.043, 0.050):
            c = (223, 242, 196)
        if ellipse(u, v, ex, 0.418, 0.017, 0.022):
            c = (18, 24, 16)

    # bowl body (lower half of an ellipse)
    if v >= 0.615 and ellipse(u, v, 0.50, 0.665, 0.305, 0.175):
        t = (u - 0.195) / 0.61
        c = lerp((126, 78, 44), (74, 42, 26), t)
        if ellipse(u, v, 0.42, 0.64, 0.05, 0.09):
            c = lerp(c, (176, 120, 72), 0.35)

    # rim
    if ellipse(u, v, 0.50, 0.625, 0.320, 0.052) and v <= 0.655:
        c = (196, 150, 96)
        if v > 0.638:
            c = (150, 105, 66)

    # porridge
    if ellipse(u, v, 0.50, 0.606, 0.275, 0.040):
        c = (232, 192, 105)
        # speckles
        if (int(u * 190) * 73856093 ^ int(v * 190) * 19349663) % 23 == 0:
            c = (206, 162, 78)

    # bowl outline
    if v >= 0.615 and ellipse(u, v, 0.50, 0.665, 0.305, 0.175):
        if not ellipse(u, v, 0.50, 0.665, 0.292, 0.162):
            c = (54, 30, 18)

    # vignette
    edge = math.hypot(u - 0.5, v - 0.5) / 0.72
    if edge > 0.78:
        c = lerp(c, (4, 6, 4), min(1.0, (edge - 0.78) / 0.30))
    return c


# render supersampled
pixels = bytearray()
for y in range(S):
    for x in range(S):
        r, g, b = shade(x, y)
        pixels.extend((r, g, b))

# box downsample
out = bytearray()
row = W * 4
for y in range(W):
    out.append(0)  # filter: none
    base = y * SS * S
    for x in range(W):
        r = g = b = 0
        for sy in range(SS):
            off = base + sy * S + x * SS
            for sx in range(SS):
                p = (off + sx) * 3
                r += pixels[p]
                g += pixels[p + 1]
                b += pixels[p + 2]
        n = SS * SS
        out.extend((r // n, g // n, b // n, 255))

raw = bytes(out)


def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(
        ">I", zlib.crc32(tag + data) & 0xFFFFFFFF
    )


png = b"\x89PNG\r\n\x1a\n"
png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
png += chunk(b"IDAT", zlib.compress(raw, 9))
png += chunk(b"IEND", b"")
open("resources/icon.png", "wb").write(png)
print("resources/icon.png", len(png), "bytes")
