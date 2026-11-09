"""Turns an in-game cover screenshot into the project's cover images. Standard library only (like make_icon.py).

1. Take the screenshot (Debug build, from src/SantaShift.Game/bin/Debug/net10.0):
     SantaShift.exe --workshop --cover --round 2 --carry bear:assembled:ribbon --yaw 180 --pitch 4 --distance 2.2
                    --frame 60 --screenshot cover_shot.png
   --cover draws the title, candy canes, garland and tagline instead of the HUD.
2. py tools/cover/make_cover.py cover_shot.png

Writes source-assets/cover/:
  cover_1280x720.png   the full 16:9 picture (jam page header, trailers, store banners)
  cover_630x500.png    itch.io's cover image size (centre crop)
  social_1280x640.png  GitHub's social preview size (Settings > General > Social preview)
"""
import struct
import sys
import zlib
from pathlib import Path

OUT = Path(__file__).resolve().parents[2] / "source-assets" / "cover"


def read_png(path):
    data = Path(path).read_bytes()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", "not a PNG"
    pos, idat, w = 8, b"", 0
    while pos < len(data):
        length, kind = struct.unpack(">I4s", data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + length]
        if kind == b"IHDR":
            w, h, depth, colour, _, _, interlace = struct.unpack(">IIBBBBB", body)
            assert depth == 8 and colour in (2, 6) and interlace == 0, "needs 8-bit RGB or RGBA, not interlaced"
            bpp = 3 if colour == 2 else 4
        elif kind == b"IDAT":
            idat += body
        pos += 12 + length
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev = [], bytearray(stride)
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        rows.append(line)
        prev = line
    out = []  # RGB rows (alpha dropped)
    for line in rows:
        if bpp == 4:
            rgb = bytearray(w * 3)
            rgb[0::3], rgb[1::3], rgb[2::3] = line[0::4], line[1::4], line[2::4]
            out.append(rgb)
        else:
            out.append(line)
    return w, h, out


def write_png(path, w, h, rows):
    raw = b"".join(b"\x00" + bytes(r) for r in rows)
    def chunk(kind, body):
        return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)
    png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    Path(path).write_bytes(png)


def crop(rows, x, y, w, h):
    return [r[x * 3:(x + w) * 3] for r in rows[y:y + h]]


def resize(rows, sw, sh, dw, dh):
    """Area-average downscale (each output pixel averages the source pixels it covers): smooth, no jaggies."""
    xs = [(i * sw / dw, (i + 1) * sw / dw) for i in range(dw)]
    out = []
    for j in range(dh):
        y0, y1 = j * sh / dh, (j + 1) * sh / dh
        acc = [0.0] * (sw * 3)
        total = 0.0
        for sy in range(int(y0), min(sh, int(y1 + 0.9999))):
            wy = min(y1, sy + 1) - max(y0, sy)
            if wy <= 0: continue
            r = rows[sy]
            for k in range(sw * 3): acc[k] += r[k] * wy
            total += wy
        line = bytearray(dw * 3)
        for i, (x0, x1) in enumerate(xs):
            s = [0.0, 0.0, 0.0]; wsum = 0.0
            for sx in range(int(x0), min(sw, int(x1 + 0.9999))):
                wx = min(x1, sx + 1) - max(x0, sx)
                if wx <= 0: continue
                for c in range(3): s[c] += acc[sx * 3 + c] * wx
                wsum += wx
            for c in range(3): line[i * 3 + c] = min(255, int(s[c] / (wsum * total) + 0.5))
        out.append(line)
    return out


def main():
    w, h, rows = read_png(sys.argv[1])
    OUT.mkdir(parents=True, exist_ok=True)
    if (w, h) != (1280, 720):
        rows = resize(rows, w, h, 1280, 720); w, h = 1280, 720
    write_png(OUT / "cover_1280x720.png", w, h, rows)

    cw = round(h * 630 / 500)  # centre crop to 630:500, then shrink
    write_png(OUT / "cover_630x500.png", 630, 500, resize(crop(rows, (w - cw) // 2, 0, cw, h), cw, h, 630, 500))

    write_png(OUT / "social_1280x640.png", 1280, 640, crop(rows, 0, 30, 1280, 640))  # keeps the title and the elf's feet
    for f in sorted(OUT.glob("*.png")): print(f"  {f.relative_to(OUT.parents[1])}")


if __name__ == "__main__":
    main()
