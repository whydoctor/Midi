#!/usr/bin/env python3
"""Generate a Chordara app icon (PNG + ICO). One-shot tool, not part of build."""
import struct, zlib, os

OUT_DIR = os.path.dirname(os.path.abspath(__file__))
ICO_PATH = os.path.join(OUT_DIR, "..", "Chordara", "chordara.ico")
PNG_PATH = os.path.join(OUT_DIR, "..", "Chordara", "Assets", "chordara.png")

BG   = (0x1E, 0x1E, 0x22, 0xFF)   # app bg
CARD = (0x26, 0x26, 0x2C, 0xFF)   # rounded card
KEY_W = (0xEA, 0xEA, 0xEA, 0xFF)  # white key
KEY_B = (0x16, 0x16, 0x1A, 0xFF)  # black key
ACC  = (0x5B, 0xA8, 0xFF, 0xFF)   # accent (note head)

def make_png(size: int) -> bytes:
    W = H = size
    px = bytearray()
    corner = size * 0.18    # rounded-corner radius
    margin = size * 0.08

    # Piano key layout inside the card
    key_top    = size * 0.52
    key_bottom = size - margin - 1
    n_white    = 7
    white_w    = (size - 2*margin) / n_white
    white_keys_x0 = margin

    # Positions of black keys as fractions of the 7-white-key span
    # (between C-D, D-E, F-G, G-A, A-B)
    black_offsets = [1, 2, 4, 5, 6]   # white-key indices that have a black key to their left

    # Note head (accent circle) top-right of the card
    head_cx = size * 0.62
    head_cy = size * 0.30
    head_r  = size * 0.12

    # Note stem
    stem_x1 = head_cx + head_r * 0.75
    stem_y1 = head_cy
    stem_x2 = stem_x1
    stem_y2 = size * 0.12

    def in_rounded_card(x, y):
        x0, y0 = margin, margin
        x1, y1 = size - margin, size - margin
        if x < x0 or x > x1 or y < y0 or y > y1:
            return False
        # corners
        for (cx, cy) in [(x0+corner, y0+corner), (x1-corner, y0+corner),
                         (x0+corner, y1-corner), (x1-corner, y1-corner)]:
            if ((x < cx and y < cy) or (x > cx and y < cy) or
                (x < cx and y > cy) or (x > cx and y > cy)):
                if (x-cx)**2 + (y-cy)**2 > corner*corner and \
                   ((x-cx)*(cx-size/2) > 0 or (y-cy)*(cy-size/2) > 0):
                    # simplified: only reject if we are past the corner arc
                    pass
        # simpler radial check at each corner region
        for (cx, cy, sx, sy) in [
            (x0+corner, y0+corner, -1, -1),
            (x1-corner, y0+corner,  1, -1),
            (x0+corner, y1-corner, -1,  1),
            (x1-corner, y1-corner,  1,  1),
        ]:
            if (x-cx)*sx > 0 and (y-cy)*sy > 0:
                if (x-cx)**2 + (y-cy)**2 > corner*corner:
                    return False
        return True

    for y in range(H):
        px.append(0)  # PNG filter byte: None
        for x in range(W):
            xf, yf = x + 0.5, y + 0.5
            color = BG

            if in_rounded_card(xf, yf):
                color = CARD

                # White keys
                if key_top <= yf <= key_bottom:
                    rel_x = xf - white_keys_x0
                    if 0 <= rel_x <= n_white * white_w:
                        color = KEY_W
                        # Key divider lines
                        idx = rel_x / white_w
                        if abs(idx - round(idx)) < (0.6 / white_w):
                            color = CARD

                # Black keys (shorter, overlap top portion)
                black_bottom = key_top + (key_bottom - key_top) * 0.60
                if key_top <= yf <= black_bottom:
                    for bi in black_offsets:
                        bx = white_keys_x0 + bi * white_w
                        bw = white_w * 0.60
                        if bx - bw/2 <= xf <= bx + bw/2:
                            color = KEY_B
                            break

                # Note stem
                if (stem_x1 - 1.2 <= xf <= stem_x1 + 1.2 and
                    min(stem_y1, stem_y2) <= yf <= max(stem_y1, stem_y2)):
                    color = ACC

                # Note head (filled circle)
                if (xf - head_cx)**2 + (yf - head_cy)**2 <= head_r**2:
                    color = ACC

            px.extend(color)

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (struct.pack(">I", len(data)) + tag + data
                + struct.pack(">I", zlib.crc32(tag + data)))

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(px), 9))
    png += chunk(b"IEND", b"")
    return png

def make_ico(sizes):
    pngs = [(s, make_png(s)) for s in sizes]
    header = struct.pack("<HHH", 0, 1, len(pngs))
    entries = b""
    offset = 6 + 16 * len(pngs)
    for (s, png) in pngs:
        w = 0 if s >= 256 else s
        h = 0 if s >= 256 else s
        entries += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    blob = header + entries + b"".join(p for _, p in pngs)
    return blob, pngs

if __name__ == "__main__":
    ico, pngs = make_ico([16, 32, 48, 64, 128, 256])
    os.makedirs(os.path.dirname(ICO_PATH), exist_ok=True)
    os.makedirs(os.path.dirname(PNG_PATH), exist_ok=True)
    with open(ICO_PATH, "wb") as f:
        f.write(ico)
    # Write the 128px entry as the standalone PNG
    target_png = next((p for (s, p) in pngs if s == 128), pngs[-1][1])
    with open(PNG_PATH, "wb") as f:
        f.write(target_png)
    print(f"Wrote {ICO_PATH} ({len(ico)} bytes)")
    print(f"Wrote {PNG_PATH} ({len(target_png)} bytes)")
