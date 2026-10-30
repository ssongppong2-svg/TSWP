"""땅 재질 텍스처 (각 128x128, 이음새 없이 반복) + 절벽 바위 (64x64)

assets/tex/ground.png : 세로로 쌓은 재질 N장 (DB.GROUND 순서와 같아야 함)
  0 잔디   1 짙은 잔디   2 흙길   3 돌바닥   4 숲 바닥(낙엽)   5 모래
assets/tex/wall.png   : 절벽면
"""
import sys

from px import *

S = 128
P = (S, S)


def wrap_put(a, stamp, x, y):
    """이음새 없게: 가장자리를 넘으면 반대편에도 찍는다"""
    h, w = stamp.shape[:2]
    for dx in (0, -S, S):
        for dy in (0, -S, S):
            over(a, stamp, x + dx, y + dy)


def tones(base_cols, seed, cell=40, levels=(0.3, 0.72)):
    """큰 얼룩 2~3톤 (부드러운 디더 경계)"""
    a = canvas(S, S)
    t = fbm(S, S, cell, seed, 3, P)
    t2 = vnoise(S, S, 8, seed + 5, P)
    t = t * 0.8 + t2 * 0.2
    m = np.ones((S, S), bool)
    lo, hi = levels
    tt = np.interp(t, [lo - 0.08, lo + 0.08, hi - 0.08, hi + 0.08], [0.0, 0.34, 0.66, 1.0])
    ramp(a, m, base_cols, tt, dither=0.9)
    return a


def blade(col_hi, col_lo, kind):
    """작은 풀잎 표시 (3~5px)"""
    s = canvas(5, 4)
    if kind == 0:     # v
        for (x, y) in ((0, 0), (1, 1), (2, 2), (3, 1), (4, 0)):
            s[y, x] = hx(col_hi)
        s[3, 2] = hx(col_lo)
    elif kind == 1:   # 한 가닥
        for (x, y) in ((2, 0), (2, 1), (1, 2)):
            s[y, x] = hx(col_hi)
        s[3, 1] = hx(col_lo)
    else:             # 두 가닥
        for (x, y) in ((1, 0), (1, 1), (3, 1), (3, 2)):
            s[y, x] = hx(col_hi)
        s[3, 2] = hx(col_lo)
    return s


def big_blade(col, shade, kind):
    """나무·캐릭터와 픽셀 밀도를 맞춘 굵은 풀 표시 (두 줄기 'ㅅ', 2px 굵기)"""
    s = canvas(7, 6)
    pts = {0: ((0, 0), (1, 1), (2, 2), (3, 3), (6, 0), (5, 1), (4, 2)),
           1: ((1, 0), (2, 1), (3, 2), (3, 3), (5, 1), (4, 2)),
           2: ((0, 1), (1, 2), (2, 3), (5, 0), (5, 1), (4, 2), (4, 3))}[kind]
    for (x, y) in pts:
        s[y, x] = hx(col)
        if y + 1 < 6:
            s[y + 1, x] = hx(shade)
    return s


def grass(seed, cols, hi, lo, n_blades=70, specks=None):
    a = tones(cols, seed)
    r = rnd(seed)
    for _ in range(n_blades):
        x, y = r.randrange(S), r.randrange(S)
        wrap_put(a, big_blade(lo, hi, r.randrange(3)), x, y)
    if specks:
        for _ in range(40):
            x, y = r.randrange(S), r.randrange(S)
            a[y % S, x % S] = hx(r.choice(specks))
    return a


def dirt(seed):
    a = tones(DIRT[3:6], seed, cell=36, levels=(0.32, 0.7))
    r = rnd(seed)
    # 자갈
    for _ in range(46):
        x, y = r.randrange(S), r.randrange(S)
        w_, h_ = r.choice([(2, 1), (3, 2), (2, 2), (4, 2)])
        s = canvas(w_ + 1, h_ + 1)
        s[:h_, :w_] = hx(DIRT[5])
        s[0, :w_] = hx(TAN[6])
        s[h_, 1:w_ + 1] = hx(DIRT[1])
        wrap_put(a, s, x, y)
    # 바퀴·발자국 같은 짙은 줄
    for _ in range(14):
        x, y = r.randrange(S), r.randrange(S)
        L = r.randrange(3, 8)
        for i in range(L):
            a[(y + (i // 3)) % S, (x + i) % S] = hx(DIRT[1])
    return a


def stone_floor(seed):
    """판석 바닥 (보로노이) — 캠프 광장"""
    a = canvas(S, S)
    r = rnd(seed)
    pts = []
    gx = 4
    for gy_ in range(gx):
        for gx_ in range(gx):
            pts.append(((gx_ + 0.2 + r.random() * 0.6) * S / gx, (gy_ + 0.2 + r.random() * 0.6) * S / gx))
    xs, ys = grid(S, S)
    best = np.full((S, S), 1e9, np.float32)
    second = np.full((S, S), 1e9, np.float32)
    idx = np.zeros((S, S), int)
    for i, (px_, py_) in enumerate(pts):
        for ox in (-S, 0, S):
            for oy in (-S, 0, S):
                d = (xs - px_ - ox) ** 2 + (ys - py_ - oy) ** 2
                upd = d < best
                second = np.where(upd, best, np.minimum(second, d))
                best = np.where(upd, d, best)
                idx = np.where(upd, i, idx)
    edge = (np.sqrt(second) - np.sqrt(best)) < 1.6
    shade = np.array([r.random() for _ in pts])[idx]
    t = 0.35 + shade * 0.35 + (vnoise(S, S, 6, seed + 3, P) - 0.5) * 0.25
    ramp(a, np.ones((S, S), bool), TAN[3:8], t, 0.6)
    paint(a, edge, TAN[1])
    # 이음새 밑(아래쪽) 그늘 · 위쪽 빛
    paint(a, nb(edge, 0, -1) & ~edge, TAN[7])
    moss = edge & (vnoise(S, S, 10, seed + 9, P) > 0.7)
    paint(a, moss, GRASS[3])
    return a


def forest_floor(seed):
    a = tones(["#3c5634", "#4a663a", "#587442"], seed, cell=32, levels=(0.34, 0.7))
    r = rnd(seed)
    cols = ["#6e5236", "#8a6844", "#9a7a4e", "#7a4e34"]
    for _ in range(90):
        x, y = r.randrange(S), r.randrange(S)
        c = r.choice(cols)
        a[y % S, x % S] = hx(c)
        a[y % S, (x + 1) % S] = hx(c)
        if r.random() < 0.4:
            a[(y + 1) % S, x % S] = hx("#2c3e28")
    return a


def sand(seed):
    a = tones(TAN[5:8], seed, cell=36, levels=(0.32, 0.7))
    r = rnd(seed)
    for _ in range(60):
        x, y = r.randrange(S), r.randrange(S)
        a[y % S, x % S] = hx(r.choice([TAN[3], TAN[8]]))
    return a


def wall(seed):
    """절벽 바위면 64x64: 세로로 긴 바위판(보로노이) — 판마다 왼쪽 위 밝고 오른쪽 아래 어둡게, 틈은 짙은 갈색"""
    W = 64
    a = canvas(W, W)
    xs, ys = grid(W, W)
    r = rnd(seed)
    pts = []
    for gy_ in range(4):
        for gx_ in range(5):
            pts.append(((gx_ + 0.15 + r.random() * 0.7) * W / 5, (gy_ + 0.15 + r.random() * 0.7) * W / 4))
    best = np.full((W, W), 1e9, np.float32)
    second = np.full((W, W), 1e9, np.float32)
    idx = np.zeros((W, W), int)
    cx = np.zeros((W, W), np.float32)
    cy = np.zeros((W, W), np.float32)
    for i, (px_, py_) in enumerate(pts):
        for ox in (-W, 0, W):
            for oy in (-W, 0, W):
                d = ((xs - px_ - ox) * 1.25) ** 2 + ((ys - py_ - oy) * 0.7) ** 2
                upd = d < best
                second = np.where(upd, best, np.minimum(second, d))
                best = np.where(upd, d, best)
                idx = np.where(upd, i, idx)
                cx = np.where(upd, px_ + ox, cx)
                cy = np.where(upd, py_ + oy, cy)
    gap = np.sqrt(second) - np.sqrt(best)
    edge = gap < 1.3
    base = np.array([0.42 + r.random() * 0.22 for _ in pts])[idx]
    # 판 안 입체: 중심에서 왼쪽 위로 갈수록 밝게
    lx = (cx - xs) / 10.0
    ly = (cy - ys) / 14.0
    t = base + np.clip(lx * 0.12 + ly * 0.16, -0.25, 0.25) + (fbm(W, W, 8, seed + 2, 2, (W, W)) - 0.5) * 0.18
    # 틈 바로 옆: 아래·오른쪽 판은 밝은 모서리, 위·왼쪽은 그늘
    t = np.where((gap < 2.6) & (lx < 0), t - 0.14, t)
    t = np.where((gap < 2.6) & (lx >= 0), t + 0.12, t)
    ramp(a, np.ones((W, W), bool), WALL[1:7], t, 0.55)
    paint(a, edge, WALL[0])
    moss = (vnoise(W, W, 9, seed + 7, (W, W)) > 0.8) & ~edge & (BAYER4[ys.astype(int) % 4, xs.astype(int) % 4] > 0.3)
    paint(a, moss, GRASS[2])
    return a


GROUND = [
    ("grass", lambda: grass(11, GRASS[4:7], GRASS[3], GRASS[2], 14, [GRASS[7]])),
    ("grass_lush", lambda: grass(23, GRASS[3:6], GRASS[2], GRASS[1], 16)),
    ("dirt", lambda: dirt(31)),
    ("stone", lambda: stone_floor(41)),
    ("forest", lambda: forest_floor(51)),
    ("sand", lambda: sand(61)),
]


def main():
    stack = canvas(S, S * len(GROUND))
    for i, (name, fn) in enumerate(GROUND):
        stack[i * S:(i + 1) * S] = fn()
    save(stack, os.path.join(OUT, "tex", "ground.png"))
    w = wall(71)
    save(w, os.path.join(OUT, "tex", "wall.png"))
    # 미리보기: 2x2 타일링
    pv = canvas(S * 2 * len(GROUND), S * 2)
    for i in range(len(GROUND)):
        t = stack[i * S:(i + 1) * S]
        pv[:, i * S * 2:(i + 1) * S * 2] = np.tile(t, (2, 2, 1))
    preview(pv, "ground_pv.png", 2)
    preview(np.tile(w, (2, 4, 1)), "wall_pv.png", 4)
    print("ground", stack.shape, "wall", w.shape)


if __name__ == "__main__":
    main()
