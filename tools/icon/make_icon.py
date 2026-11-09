"""
Draws the Santa Shift launcher icon: a striped candy cane with a sprig of holly (two leaves, three berries) on a
night-blue tile with a gold rim, in the GDD palette (section on UI palette).

No outside art: every shape is a signed-distance function below, drawn with analytic anti-aliasing at each size, so
the icon is original work of the project (all rights reserved, see LICENSE.txt). Python 3.8+, standard library only.

    py tools/icon/make_icon.py

Writes:
    src/SantaShift.Game/Icon/santa_shift.ico     Windows executable icon (16-256 px; set as ApplicationIcon)
    src/SantaShift.Game/Icon/santa_shift.icns    macOS icon (128-1024 px), for an .app bundle at release time
    src/SantaShift.Game/Content/textures/icon_<n>.png   window / taskbar icons loaded by the game at start
    source-assets/icon/santa_shift_1024.png      full-size preview
"""
import math
import os
import random
import struct
import zlib

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")
ICON_DIR = os.path.join(ROOT, "src", "SantaShift.Game", "Icon")
TEX_DIR = os.path.join(ROOT, "src", "SantaShift.Game", "Content", "textures")
PREVIEW_DIR = os.path.join(ROOT, "source-assets", "icon")

ICO_SIZES = [16, 24, 32, 48, 64, 128, 256]
ICNS_SIZES = {128: b"ic07", 256: b"ic08", 512: b"ic09", 1024: b"ic10"}
WINDOW_SIZES = [16, 32, 48, 64, 256]


def hexc(h):
    return (int(h[1:3], 16) / 255, int(h[3:5], 16) / 255, int(h[5:7], 16) / 255)


# GDD palette, plus darker / lighter shades of it
SNOW = hexc("#F4F0DC")
INK = hexc("#202934")
INK_LIGHT = hexc("#33445A")
PINE = hexc("#225E4C")
BERRY = hexc("#B94146")
GOLD = hexc("#E8BB57")
LEAF_DARK = hexc("#1C5A3A")
LEAF_LIGHT = hexc("#2F8A52")
LEAF_RIB = hexc("#8CC77A")
BERRY_LIGHT = hexc("#E0575C")


def clamp(x, lo=0.0, hi=1.0):
    return lo if x < lo else hi if x > hi else x


def mix(a, b, t):
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def scale(c, k):
    return (clamp(c[0] * k), clamp(c[1] * k), clamp(c[2] * k))


# ---------------------------------------------------------------- shapes (units: 0..1 across the icon, y down)

def sd_round_rect(x, y, cx, cy, hw, hh, r):
    qx = abs(x - cx) - hw + r
    qy = abs(y - cy) - hh + r
    return math.hypot(max(qx, 0.0), max(qy, 0.0)) + min(max(qx, qy), 0.0) - r


class Cane:
    """Stem from the bottom up to the hook; the hook is a half circle curling left with a short tail."""

    def __init__(self):
        self.top = (0.555, 0.30)          # where the stem meets the hook
        self.bottom = (0.70, 0.86)
        dx, dy = self.bottom[0] - self.top[0], self.bottom[1] - self.top[1]
        self.length = math.hypot(dx, dy)
        self.down = (dx / self.length, dy / self.length)    # stem direction, top -> bottom
        self.right = (self.down[1], -self.down[0])          # screen-right of the stem
        if self.right[0] < 0:
            self.right = (-self.right[0], -self.right[1])
        self.hook_r = 0.135
        self.tail = 0.07
        self.radius = 0.052
        self.stripe = 0.072

    def local(self, x, y):
        """Pixel in cane coordinates: u across (right +), v along the stem (down +), origin at the top of the stem."""
        px, py = x - self.top[0], y - self.top[1]
        return px * self.right[0] + py * self.right[1], px * self.down[0] + py * self.down[1]

    def nearest(self, x, y):
        """(distance to centre line, arc length from the bottom, signed offset across, direction to pixel)."""
        u, v = self.local(x, y)
        L, R = self.length, self.hook_r
        best = None
        # stem: u = 0, v in [0, L]
        vv = clamp(v, 0.0, L)
        d = math.hypot(u, v - vv)
        best = (d, L - v, u, (u, v - vv))          # unclamped length: stripes run on round the cap
        # hook: centre (-R, 0), from angle 0 (the stem top) over the top to angle pi
        hx, hy = u + R, v
        ang = math.atan2(-hy, hx)               # 0 at the stem top, pi/2 at the very top, pi at the far side
        if ang < -math.pi / 2:
            ang += 2 * math.pi
        a = clamp(ang, 0.0, math.pi)
        qx, qy = R * math.cos(a), -R * math.sin(a)
        d = math.hypot(hx - qx, hy - qy)
        if d < best[0]:
            radial = math.hypot(hx, hy) - R    # outward from the hook centre
            best = (d, L + R * a, radial, (hx - qx, hy - qy))
        # tail: from (-2R, 0) straight down
        tv = clamp(v, 0.0, self.tail)
        d = math.hypot(u + 2 * R, v - tv)
        if d < best[0]:
            best = (d, L + math.pi * R + v, -(u + 2 * R), (u + 2 * R, v - tv))
        return best

    def sdf(self, x, y):
        return self.nearest(x, y)[0] - self.radius

    def colour(self, x, y):
        d, s, across, (lx, ly) = self.nearest(x, y)
        n = clamp(across / self.radius, -1.0, 1.0)
        band = math.floor((s + n * self.radius * 0.9) / self.stripe)
        base = BERRY if band % 2 == 0 else SNOW
        # round-tube shading: light from the upper left of the screen
        if d > 1e-9:
            # back to screen space
            sx = lx * self.right[0] + ly * self.down[0]
            sy = lx * self.right[1] + ly * self.down[1]
            nx, ny = sx / d, sy / d
        else:
            nx, ny = 0.0, 0.0
        t = clamp(d / self.radius)
        lam = -(nx * -0.6 + ny * -0.8) * t     # >0 faces away from the light
        shade = 1.0 - 0.32 * clamp(lam, 0, 1) + 0.06 * clamp(-lam, 0, 1) - 0.12 * t * t
        c = scale(base, shade)
        spec = clamp(1.0 - abs(-lam + 0.6) / 0.14) * 0.35 * t
        return mix(c, (1, 1, 1), spec)


class Leaf:
    def __init__(self, base, angle_deg, length, width, flip):
        self.bx, self.by = base
        a = math.radians(angle_deg)
        self.ax, self.ay = math.cos(a), math.sin(a)
        self.length, self.width = length, width
        self.flip = flip

    def local(self, x, y):
        px, py = x - self.bx, y - self.by
        along = px * self.ax + py * self.ay
        across = -px * self.ay + py * self.ax
        return along, across * self.flip

    def half_width(self, a):
        """a in [-1, 1] along the leaf: an oval, scalloped between five spines per side."""
        body = math.sqrt(max(0.0, 1 - a * a)) ** 0.85
        scallop = 1 - 0.30 * abs(math.sin(2.5 * math.pi * (a + 1)))
        return self.width * body * scallop

    def sdf(self, x, y):
        along, across = self.local(x, y)
        a = along / (self.length / 2) - 1
        if a < -1.2 or a > 1.2:
            return 1.0
        if abs(a) > 1:   # beyond a tip: distance to the tip point keeps the outline pointed
            return math.hypot(along - (self.length if a > 0 else 0.0), across)
        return abs(across) - self.half_width(a)

    def colour(self, x, y):
        along, across = self.local(x, y)
        a = along / (self.length / 2) - 1
        c = LEAF_LIGHT if across < 0 else LEAF_DARK
        hw = max(self.half_width(clamp(a, -1, 1)), 1e-6)
        c = scale(c, 1.0 + 0.18 * (1 - abs(across) / hw) - 0.1)
        rib = 0.006 + 0.004 * (1 - abs(a))
        if abs(across) < rib and abs(a) < 0.93:
            c = mix(c, LEAF_RIB, 0.75)
        return c


class Berry:
    def __init__(self, cx, cy, r):
        self.cx, self.cy, self.r = cx, cy, r

    def sdf(self, x, y):
        return math.hypot(x - self.cx, y - self.cy) - self.r

    def colour(self, x, y):
        dx, dy = (x - self.cx) / self.r, (y - self.cy) / self.r
        t = clamp(math.hypot(dx, dy))
        c = mix(BERRY_LIGHT, scale(BERRY, 0.7), clamp(t * 0.6 + (dx + dy) * 0.25 + 0.2))
        hl = clamp(1 - math.hypot(dx + 0.38, dy + 0.4) / 0.28)
        return mix(c, (1, 1, 1), hl * 0.85)


# ---------------------------------------------------------------- scene

CANE = Cane()
_holly = (CANE.top[0] + CANE.down[0] * CANE.length * 0.58, CANE.top[1] + CANE.down[1] * CANE.length * 0.58)
LEAVES = [
    Leaf((_holly[0] + 0.01, _holly[1] + 0.005), 192, 0.33, 0.085, 1),
    Leaf((_holly[0] + 0.01, _holly[1] + 0.02), 140, 0.29, 0.075, -1),
]
BERRIES = [
    Berry(_holly[0] - 0.005, _holly[1] - 0.025, 0.040),
    Berry(_holly[0] + 0.050, _holly[1] + 0.000, 0.040),
    Berry(_holly[0] + 0.012, _holly[1] + 0.045, 0.040),
]
SHAPES = [CANE] + LEAVES + BERRIES
SHADOW_OFFSET = (0.012, 0.018)

_rng = random.Random(1225)
SNOWFLAKES = [(_rng.uniform(0.1, 0.9), _rng.uniform(0.1, 0.9), _rng.uniform(0.006, 0.013)) for _ in range(26)]
SNOWFLAKES = [f for f in SNOWFLAKES if CANE.sdf(f[0], f[1]) > 0.04]

TILE = (0.5, 0.5, 0.46, 0.46, 0.2)       # centre x, y, half sizes, corner radius
RIM = 0.022


def coverage(d, px):
    return clamp(0.5 - d / px)


def over(dst, colour, alpha):
    """Composite straight-alpha colour over a premultiplied (r, g, b, a) pixel."""
    if alpha <= 0:
        return dst
    r, g, b, a = dst
    k = 1 - alpha
    return (colour[0] * alpha + r * k, colour[1] * alpha + g * k, colour[2] * alpha + b * k, alpha + a * k)


def render(size):
    px = 1.0 / size
    small = size <= 32
    pixels = []
    for j in range(size):
        y = (j + 0.5) * px
        for i in range(size):
            x = (i + 0.5) * px
            p = (0.0, 0.0, 0.0, 0.0)
            tile = sd_round_rect(x, y, *TILE)
            if tile > px:
                pixels.append(p)
                continue
            # tile: night sky, lighter at the centre, gold rim
            t = clamp(math.hypot(x - 0.45, y - 0.4) / 0.62)
            p = over(p, mix(INK_LIGHT, INK, t), coverage(tile, px))
            rim = max(tile, -(tile + RIM))
            p = over(p, GOLD, coverage(rim, px))
            if not small:
                for fx, fy, fr in SNOWFLAKES:
                    if abs(x - fx) < fr + px and abs(y - fy) < fr + px:
                        p = over(p, SNOW, 0.55 * coverage(math.hypot(x - fx, y - fy) - fr, px))
            # soft drop shadow of the cane and holly
            sx, sy = x - SHADOW_OFFSET[0], y - SHADOW_OFFSET[1]
            sd = min(s.sdf(sx, sy) for s in SHAPES)
            if sd < 0.04:
                p = over(p, (0, 0, 0), 0.45 * clamp(0.5 - sd / 0.035) * coverage(tile, px))
            # shapes, back to front, each with a thin dark outline
            for s in SHAPES:
                d = s.sdf(x, y)
                edge = max(0.006, px)
                if d < edge + px:
                    p = over(p, scale(INK, 0.6), coverage(d - edge, px))
                    if d < px:
                        p = over(p, s.colour(x, y), coverage(d, px))
            pixels.append(p)
    # premultiplied -> straight RGBA bytes
    out = bytearray()
    for r, g, b, a in pixels:
        if a > 1e-6:
            out += bytes((round(clamp(r / a) * 255), round(clamp(g / a) * 255), round(clamp(b / a) * 255), round(a * 255)))
        else:
            out += b"\0\0\0\0"
    return bytes(out)


# ---------------------------------------------------------------- file formats

def png_bytes(size, rgba):
    raw = b"".join(b"\0" + rgba[row * size * 4:(row + 1) * size * 4] for row in range(size))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def bmp_icon_bytes(size, rgba):
    """Classic 32-bit DIB icon entry (bottom-up BGRA plus a 1-bit AND mask); the safest form for small sizes."""
    header = struct.pack("<IiiHHIIiiII", 40, size, size * 2, 1, 32, 0, 0, 0, 0, 0, 0)
    rows = []
    for row in range(size - 1, -1, -1):
        line = rgba[row * size * 4:(row + 1) * size * 4]
        rows.append(b"".join(bytes((line[k + 2], line[k + 1], line[k], line[k + 3])) for k in range(0, len(line), 4)))
    mask_stride = ((size + 31) // 32) * 4
    mask = bytearray(mask_stride * size)
    for out_row, row in enumerate(range(size - 1, -1, -1)):
        for col in range(size):
            if rgba[(row * size + col) * 4 + 3] == 0:
                mask[out_row * mask_stride + col // 8] |= 0x80 >> (col % 8)
    return header + b"".join(rows) + bytes(mask)


def ico_bytes(images):
    count = len(images)
    entries, blobs = b"", b""
    offset = 6 + 16 * count
    for size, rgba in images:
        blob = png_bytes(size, rgba) if size >= 256 else bmp_icon_bytes(size, rgba)
        dim = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(blob), offset)
        blobs += blob
        offset += len(blob)
    return struct.pack("<HHH", 0, 1, count) + entries + blobs


def icns_bytes(images):
    body = b""
    for size, rgba in images:
        data = png_bytes(size, rgba)
        body += ICNS_SIZES[size] + struct.pack(">I", len(data) + 8) + data
    return b"icns" + struct.pack(">I", len(body) + 8) + body


def write(path, data):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(data)
    print("wrote", os.path.relpath(path, ROOT))


def main():
    sizes = sorted(set(ICO_SIZES) | set(ICNS_SIZES) | set(WINDOW_SIZES))
    images = {}
    for n in sizes:
        print("drawing", n, "px")
        images[n] = render(n)
    write(os.path.join(ICON_DIR, "santa_shift.ico"), ico_bytes([(n, images[n]) for n in ICO_SIZES]))
    write(os.path.join(ICON_DIR, "santa_shift.icns"), icns_bytes([(n, images[n]) for n in sorted(ICNS_SIZES)]))
    for n in WINDOW_SIZES:
        write(os.path.join(TEX_DIR, "icon_%d.png" % n), png_bytes(n, images[n]))
    write(os.path.join(PREVIEW_DIR, "santa_shift_1024.png"), png_bytes(1024, images[1024]))


if __name__ == "__main__":
    main()
