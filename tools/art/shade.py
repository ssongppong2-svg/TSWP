"""깔끔한 셀 음영 도구 (적·보스·소품 공용) — 플레이어 그림(chars.py)과 같은 결

 · 디더 없이 3~5단 띠 음영 (빛은 왼쪽 위)
 · 색 외곽선(sel-out): 안쪽 색을 어둡게 한 색으로 테를 두른다 → 검정 테보다 부드럽고 덩어리가 읽힌다
 · 반사광(rim): 아래·오른쪽 가장자리 한 줄을 살짝 밝게 → 입체감
"""
import math

import numpy as np
from PIL import Image, ImageDraw

from px import canvas, grid, hx, nb, dilate, ell

LIGHT = (-0.55, -0.75, 0.6)       # 왼쪽 위 앞


def _norm(v):
    l = math.sqrt(sum(c * c for c in v))
    return tuple(c / l for c in v)


LV = _norm(LIGHT)


def sphere_t(W, H, cx, cy, rx, ry, amb=0.18, k=0.82):
    """타원을 공처럼 보고 빛 받는 정도 0..1"""
    xs, ys = grid(W, H)
    nx = (xs + 0.5 - cx) / max(rx, 0.01)
    ny = (ys + 0.5 - cy) / max(ry, 0.01)
    nz = np.sqrt(np.clip(1.0 - nx * nx - ny * ny, 0.0, 1.0))
    d = nx * LV[0] + ny * LV[1] + nz * LV[2]
    return np.clip(amb + k * d, 0.0, 1.0)


def bands(a, m, cols, t, cut=None):
    """t(0..1)를 cols 개 띠로 (디더 없음). cut: 띠 경계 목록 (기본 균등)"""
    n = len(cols)
    if cut is None:
        cut = [(i + 1) / n for i in range(n - 1)]
    k = np.zeros(t.shape, int)
    for c in cut:
        k += (t >= c).astype(int)
    pal = np.array([hx(c) for c in cols], np.uint8)
    a[m] = pal[np.clip(k, 0, n - 1)[m]]


def blob(a, cx, cy, rx, ry, cols, cut=None, amb=0.3, mask=None):
    W, H = a.shape[1], a.shape[0]
    m = ell(W, H, cx, cy, rx, ry)
    if mask is not None:
        m &= mask
    bands(a, m, cols, sphere_t(W, H, cx, cy, rx, ry, amb), cut)
    return m


def poly_mask(W, H, pts):
    im = Image.new("L", (W, H), 0)
    ImageDraw.Draw(im).polygon([(float(x), float(y)) for x, y in pts], fill=255)
    return np.asarray(im) > 0


def line_mask(W, H, p0, p1, th=1.0):
    """두께 th 의 선 (도장 찍기)"""
    m = np.zeros((H, W), bool)
    n = max(2, int(math.hypot(p1[0] - p0[0], p1[1] - p0[1]) * 2) + 1)
    xs, ys = grid(W, H)
    r = th / 2.0
    for s in range(n + 1):
        u = s / n
        x = p0[0] + (p1[0] - p0[0]) * u
        y = p0[1] + (p1[1] - p0[1]) * u
        if r <= 0.55:
            ix, iy = int(math.floor(x)), int(math.floor(y))
            if 0 <= ix < W and 0 <= iy < H:
                m[iy, ix] = True
        else:
            m |= ((xs + 0.5 - x) ** 2 + (ys + 0.5 - y) ** 2) <= r * r
    return m


def put(a, x, y, col):
    H, W = a.shape[:2]
    x, y = int(round(x)), int(round(y))
    if 0 <= x < W and 0 <= y < H:
        a[y, x] = hx(col) if isinstance(col, str) else col


def selout(a, ink="#221a26", k=0.38, mix_ink=0.55):
    """색 외곽선: 바깥 테 픽셀 = (가장 가까운 안쪽 색 × k) 와 잉크를 섞은 색. 아래쪽 테는 잉크 그대로(땅에 붙어 보이게)"""
    m = a[..., 3] > 0
    e = dilate(m) & ~m
    H, W = m.shape
    ink_c = hx(ink).astype(np.float32)
    out = a.copy()
    ys, xs = np.nonzero(e)
    for y, x in zip(ys, xs):
        acc = None
        for dx, dy in ((0, 1), (1, 0), (-1, 0), (0, -1)):
            xx, yy = x + dx, y + dy
            if 0 <= xx < W and 0 <= yy < H and m[yy, xx]:
                acc = a[yy, xx, :3].astype(np.float32)
                below = dy == -1          # 안쪽이 위 → 이 테는 아래쪽
                break
        if acc is None:
            continue
        c = acc * k * (1 - mix_ink) + ink_c[:3] * mix_ink
        if below:
            c = ink_c[:3]
        out[y, x, :3] = np.clip(c, 0, 255).astype(np.uint8)
        out[y, x, 3] = 255
    a[...] = out


def rim(a, m, col, side=(1, 1)):
    """반사광: 마스크의 오른쪽/아래 가장자리 한 줄"""
    e = m & ~nb(m, side[0], 0) if side[0] else np.zeros_like(m)
    e2 = m & ~nb(m, 0, side[1]) if side[1] else np.zeros_like(m)
    paint_m = (e | e2) & m
    a[paint_m] = hx(col)


def eye(a, x, y, h=2, col="#1a1420", shine="#ffffff", glow=None):
    """세로로 긴 점 눈 + 반짝임"""
    for k in range(h):
        put(a, x, y + k, glow if glow else col)
    if shine and not glow:
        put(a, x, y, shine)


def fill(a, m, col):
    a[m] = hx(col) if isinstance(col, str) else col
