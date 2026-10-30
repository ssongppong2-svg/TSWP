"""TSWP 도트 그리기 공용 도구 (레퍼런스: 숲속의 작은 마녀 · Wanderers.io · 마일드 던전)

- 팔레트는 레퍼런스 화면에서 직접 뽑은 색 (채도 낮은 청록 초록 · 따뜻한 갈색 · 크림 돌)
- 외곽선은 검정 대신 짙은 청록/갈색, 빛은 왼쪽 위
- 캔버스는 numpy RGBA (h, w, 4) uint8
"""
import math
import os
import random

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "assets")
SC = os.environ.get("TSWP_PREVIEW", "/tmp/claude-0/-home-claude/ffa1671d-1ecc-5e35-9d12-04a42bf637a4/scratchpad/")


def hx(c, a=255):
    c = c.lstrip("#")
    return np.array([int(c[0:2], 16), int(c[2:4], 16), int(c[4:6], 16), a], np.uint8)


# ------------------------------------------------------------------ 팔레트 (레퍼런스 추출)
# 초여름: 땅은 따뜻한 황록(밝음) · 나무는 짙은 청록(어두움) — 땅과 나무의 명도 차이를 크게
GRASS = ["#2c4430", "#3a5a38", "#4c7440", "#5e8a48", "#719e50", "#86b05a", "#9ac266", "#afd176", "#c6e08e", "#def0b0"]
LEAF = ["#132422", "#1b302d", "#233e37", "#2b4c41", "#355c4b", "#416e55", "#507f5f", "#64936b", "#7caa7c", "#98c290", "#b8d8a8"]
LEAF_LIGHT = ["#3a5a2c", "#4f7834", "#68963e", "#86b04a", "#a6c85e", "#c8de7e"]          # 햇빛 받은 연두 나무 (포인트)
BLOOM = ["#6a3e50", "#9a5a70", "#c8808e", "#e6a8b0", "#f6d0d0", "#fff0ec"]               # 꽃나무 (분홍 포인트)
WALL = ["#211a1e", "#33282a", "#483a38", "#5f4e46", "#786454", "#927c66", "#ac967e"]     # 절벽: 회갈색 (풀과 확실히 다르게)
BARK = ["#1e1818", "#2e2424", "#40332e", "#54443a", "#685444", "#7c644e", "#907458"]   # 차분한 회갈색 (캐릭터·흙길과 겹치지 않게)
TAN = ["#4a3a2a", "#634c34", "#7c5f42", "#8d7656", "#a08d68", "#b9a578", "#d0be8b", "#dfd2a2", "#efe6c0"]
DIRT = ["#5a4128", "#74563a", "#8e6c48", "#a8845a", "#c09c6e", "#d4b488"]
STONE = ["#3a3c3e", "#56585a", "#6f6e68", "#8a8678", "#a6a08c", "#c2bba2", "#dcd4b8"]
WATER = ["#24434a", "#2f5c62", "#3d7a7a", "#59a097", "#86c4b2", "#c4ecd8"]
FLOWER_W = ["#a6a07a", "#e9e6c8", "#fbf8e6"]
FLOWER_Y = ["#c88a2a", "#f0c050", "#ffe28c"]
FLOWER_P = ["#b04a6a", "#e87a98", "#ffb4c8"]
FLOWER_B = ["#4a6ab0", "#7aa0e8", "#b8d4ff"]
INK = "#232525"
INK_G = "#24352e"
INK_B = "#2c1f18"


# ------------------------------------------------------------------ 캔버스
def canvas(w, h):
    return np.zeros((h, w, 4), np.uint8)


def save(a, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(a, "RGBA").save(path)


def preview(a, name, scale=4, bg=(90, 128, 108)):
    im = Image.fromarray(a, "RGBA")
    b = Image.new("RGBA", im.size, bg + (255,))
    b.alpha_composite(im)
    b = b.resize((im.width * scale, im.height * scale), Image.NEAREST)
    b.save(os.path.join(SC, name))


def over(dst, src, x, y):
    """src 를 dst (x,y) 에 알파 합성 (잘림 처리)"""
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x0 >= x1 or y0 >= y1:
        return
    s = src[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    d = dst[y0:y1, x0:x1].astype(np.float32)
    a = s[..., 3:4] / 255.0
    out = d.copy()
    out[..., :3] = s[..., :3] * a + d[..., :3] * (1 - a)
    out[..., 3:4] = np.maximum(d[..., 3:4], s[..., 3:4])
    dst[y0:y1, x0:x1] = out.astype(np.uint8)


def grid(w, h):
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    return xs, ys


def ell(w, h, cx, cy, rx, ry):
    xs, ys = grid(w, h)
    return ((xs + 0.5 - cx) / max(rx, 0.01)) ** 2 + ((ys + 0.5 - cy) / max(ry, 0.01)) ** 2 <= 1.0


def nb(m, dx, dy):
    """m 을 (dx,dy) 만큼 민 것: 그 방향 이웃이 참인가"""
    o = np.zeros_like(m)
    h, w = m.shape
    ys = slice(max(0, -dy), h - max(0, dy))
    xs = slice(max(0, -dx), w - max(0, dx))
    yd = slice(max(0, dy), h - max(0, -dy))
    xd = slice(max(0, dx), w - max(0, -dx))
    o[ys, xs] = m[yd, xd]
    return o


def dilate(m, n=1):
    for _ in range(n):
        m = m | nb(m, 1, 0) | nb(m, -1, 0) | nb(m, 0, 1) | nb(m, 0, -1)
    return m


def erode(m, n=1):
    for _ in range(n):
        m = m & nb(m, 1, 0) & nb(m, -1, 0) & nb(m, 0, 1) & nb(m, 0, -1)
    return m


def edge_out(m):
    return dilate(m) & ~m


def paint(a, m, col):
    a[m] = hx(col) if isinstance(col, str) else col


def outline(a, col, mask=None, diag=False):
    m = a[..., 3] > 0 if mask is None else mask
    e = edge_out(m)
    if diag:
        e = (dilate(m) | nb(m, 1, 1) | nb(m, -1, -1) | nb(m, 1, -1) | nb(m, -1, 1)) & ~m
    a[e] = hx(col)


# ------------------------------------------------------------------ 노이즈 (주기 지정 시 이음새 없음)
def vnoise(w, h, cell, seed, period=None):
    rnd = np.random.RandomState(seed)
    pw, ph = period if period else (w + cell * 2, h + cell * 2)
    gx, gy = max(1, int(round(pw / cell))), max(1, int(round(ph / cell)))
    g = rnd.rand(gy, gx).astype(np.float32)
    xs, ys = grid(w, h)
    fx, fy = xs / cell, ys / cell
    x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - x0, fy - y0
    tx = tx * tx * (3 - 2 * tx)
    ty = ty * ty * (3 - 2 * ty)

    def G(ix, iy):
        return g[iy % gy, ix % gx]
    return (G(x0, y0) * (1 - tx) * (1 - ty) + G(x0 + 1, y0) * tx * (1 - ty) +
            G(x0, y0 + 1) * (1 - tx) * ty + G(x0 + 1, y0 + 1) * tx * ty)


def fbm(w, h, cell, seed, octaves=3, period=None):
    t = np.zeros((h, w), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        c = max(1, cell // (2 ** o))
        t += vnoise(w, h, c, seed + o * 17, period) * amp
        tot += amp
        amp *= 0.5
    return t / tot


BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], np.float32) / 16.0


def ramp(a, m, cols, t, dither=0.5):
    """마스크 m 영역을 값 t (0..1) 에 따라 색 사다리 cols 로 칠한다 (경계는 4x4 디더)"""
    h, w = m.shape
    xs, ys = grid(w, h)
    d = BAYER4[ys.astype(int) % 4, xs.astype(int) % 4] - 0.5
    n = len(cols)
    k = np.clip(np.floor(np.clip(t, 0, 0.9999) * n + d * dither), 0, n - 1).astype(int)
    pal = np.array([hx(c) for c in cols], np.uint8)
    a[m] = pal[k[m]]


def rnd(seed):
    return random.Random(seed)
