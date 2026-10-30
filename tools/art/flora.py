"""숲 식물 (레퍼런스: 숲속의 작은 마녀)

큰 활엽수 · 연두 활엽수 · 전나무 · 덤불 · 휘어진 풀 다발 · 꽃 · 고사리 · 그루터기 · 통나무 · 바위 · 버섯
→ assets/sprites/flora.png + flora.json  (발 위치 = 그림 아래 가운데)
"""
from px import *
from pack import pack

LX, LY = -0.62, -0.78       # 빛: 왼쪽 위


def darker(c, k=0.5):
    v = hx(c).astype(float)
    v[:3] *= k
    return v.astype(np.uint8)


def blob_mask(w, h, cx, cy, r, seed, bump=0.12, squash=0.9):
    """가장자리가 오돌토돌한 잎 뭉치 모양"""
    xs, ys = grid(w, h)
    dx = (xs + 0.5 - cx) / r
    dy = (ys + 0.5 - cy) / (r * squash)
    ang = np.arctan2(dy, dx)
    rr = np.sqrt(dx * dx + dy * dy)
    R = random.Random(seed)
    ph1, ph2 = R.random() * 6.28, R.random() * 6.28
    k = 1 + bump * np.sin(ang * 6 + ph1) + bump * 0.5 * np.sin(ang * 11 + ph2)
    return rr <= k, dx, dy


def leaf_clump(a, cx, cy, r, cols, seed, bright=0.0, rim_dark=None, tex=1.0, squash=0.9):
    """잎 뭉치 하나: 왼쪽 위 밝고 오른쪽 아래 어둡게 + 작은 잎 결 + 아래 테두리 그늘"""
    h, w = a.shape[:2]
    m, dx, dy = blob_mask(w, h, cx, cy, r, seed, squash=squash)
    lam = -(dx * LX + dy * LY)
    n = len(cols)
    # 뭉치 안은 2~3단 평평한 톤 (레퍼런스처럼 깔끔하게) — 잎 결은 작은 노이즈로만 살짝
    t = 0.40 + lam * 0.36 + bright
    nz = vnoise(w, h, 3, seed + 3)
    t += (nz - 0.5) * 0.07 * tex
    ramp(a, m, cols, t, dither=0.12)
    # 아래·오른쪽 가장자리 띠 = 그늘 (2~3px)
    sh = m & ~nb(m, 2, 3)
    t_sh = t - 0.3
    ramp(a, sh & (lam < 0.35), cols, t_sh, dither=0.1)
    # 왼쪽 위 가장자리 = 빛 받는 볼록한 잎 (오돌토돌한 하이라이트)
    hi = m & ~nb(m, -2, -3) & (lam > -0.1)
    jag = vnoise(w, h, 2, seed + 11) > 0.42
    ramp(a, hi & jag, cols, t + 0.28, dither=0.2)
    # 잎 결: 안쪽 윗부분에 밝은 'ㅅ' 모양 잎 끝 몇 개
    R = random.Random(seed + 99)
    ys_, xs_ = np.nonzero(erode(m, 3) & (dy < 0.2))
    for _ in range(int(r * 0.5)):
        if len(xs_) == 0:
            break
        i = R.randrange(len(xs_))
        x, y = xs_[i], ys_[i]
        k = int(np.clip(np.floor(t[y, x] * n) + 1, 0, n - 1))
        c = hx(cols[k])
        a[y, x] = c
        if x + 1 < w and m[y + 1 if y + 1 < h else y, x + 1]:
            a[min(h - 1, y + 1), x + 1] = c
        if x - 1 >= 0 and y + 1 < h and m[y + 1, x - 1]:
            a[y + 1, x - 1] = c
    if rim_dark:
        # 아래 테두리 1px 짙게 (앞 뭉치와 뒤 뭉치 구분)
        e = m & ~nb(m, 0, 1) & (dy > -0.1)
        paint(a, e, rim_dark)
    return m


def tree_broad(seed, W=112, H=132, cols=None, trunk_h=34, light=False):
    """큰 활엽수: 뿌리 퍼진 붉은 갈색 줄기 + 여러 겹 둥근 잎 뭉치"""
    cols = cols or LEAF[2:10]
    W0 = W
    W, H = W + 48, H + 20          # 넉넉한 캔버스에 그리고 나중에 잘라낸다
    a = canvas(W, H)
    R = random.Random(seed)
    cx = W / 2
    base = H - 2
    # ---- 줄기 + 뿌리
    tw = int((9 + R.randint(0, 3)) * max(1.0, W0 / 112.0) ** 1.6)
    xs, ys = grid(W, H)
    top = base - trunk_h - 26
    trunk = (np.abs(xs + 0.5 - cx - np.sin((ys - top) * 0.07) * 1.5) < tw / 2 + np.clip((ys - (base - 10)) * 0.55, 0, 7)) & (ys >= top) & (ys <= base)
    # 뿌리 갈래
    for side in (-1, 1):
        for k in range(2):
            rx = cx + side * (tw / 2 + 3 + k * 4)
            root = ell(W, H, rx, base - 1 - k, 3.0 - k * 0.6, 2.0)
            trunk |= root
    u = (xs + 0.5 - (cx - tw / 2 - 6)) / (tw + 12)
    t = 0.75 - u * 0.75 + (vnoise(W, H, 2, seed) - 0.5) * 0.25
    # 세로 결
    t -= ((xs.astype(int) + (ys.astype(int) // 5)) % 4 == 0) * 0.12
    ramp(a, trunk, BARK[1:7], t, 0.5)
    # ---- 잎 뭉치: 뒤(위·가장자리) → 앞(아래·가운데) 순서로
    crown_cy = top - 4
    # 뒤(위) 줄 → 앞(아래) 줄: 줄마다 큰 둥근 뭉치 몇 개 — 앞 뭉치의 밝은 윗머리가 덩어리 경계를 만든다
    rows = [(-22, 2, 0.20, 14), (-8, 3, 0.31, 16), (6, 4, 0.36, 15), (18, 3, 0.24, 14)]
    sc = W0 / 112.0
    clumps = []
    for (oy, cnt, spread, r) in rows:
        for k in range(cnt):
            u = (k / (cnt - 1) - 0.5) * 2 if cnt > 1 else 0
            rx = cx + u * spread * W0 + R.uniform(-3, 3)
            ry = crown_cy + oy * sc + R.uniform(-2, 2) + abs(u) * 4
            clumps.append((ry, rx, r * sc * R.uniform(0.9, 1.12)))
    clumps.sort()
    for j, (ry, rx, r) in enumerate(clumps):
        bright = (crown_cy - ry) / 75.0 + 0.03
        leaf_clump(a, rx, ry, r, cols, seed * 31 + j, bright=bright, rim_dark=cols[1])
    # 잎 아래 줄기에 드리운 그늘
    m_leaf = a[..., 3] > 0
    for x in range(W):
        col = np.nonzero(m_leaf[:, x])[0]
        if len(col) == 0:
            continue
        y0 = col.max() + 1
        for y in range(y0, min(H, y0 + 5)):
            if trunk[y, x]:
                a[y, x] = hx(BARK[0] if y < y0 + 2 else BARK[1])
    # 실루엣 테두리 (짙은 청록) — 아래쪽만 진하게
    m = a[..., 3] > 0
    e = edge_out(m)
    # 윤곽선은 모든 식생에서 같은 규칙: 잎 색의 가장 어두운 쪽을 더 어둡게 (한 덩어리로 읽히게)
    a[e] = darker(cols[0])
    return a


def tree_pine(seed, W=56, H=104):
    a = canvas(W, H)
    R = random.Random(seed)
    cx = W / 2
    base = H - 2
    xs, ys = grid(W, H)
    trunk = (np.abs(xs + 0.5 - cx) < 3.5) & (ys > base - 18) & (ys <= base)
    trunk |= ell(W, H, cx - 4, base - 1, 2.4, 1.6) | ell(W, H, cx + 4, base - 1, 2.4, 1.6)
    ramp(a, trunk, BARK[1:6], 0.7 - (xs - cx + 4) / 9.0, 0.4)
    cols = ["#14261f", "#1d3429", "#284534", "#335640", "#41684c", "#527c58", "#6a9466"]
    tiers = 6
    for i in range(tiers):
        k = i / (tiers - 1)                         # 0 꼭대기 … 1 아래
        ty = 6 + k * (H - 34)
        half = 6 + k * (W * 0.42)
        hgt = 18 + k * 8
        m = (ys >= ty) & (ys <= ty + hgt) & (np.abs(xs + 0.5 - cx) <= half * (ys - ty) / hgt + 1)
        # 아래 가장자리 톱니
        saw = ((xs.astype(int) + i * 2) % 5) < 2
        m &= ~((ys > ty + hgt - 2) & saw)
        lam = -((xs - cx) / (half + 1)) * 0.55 - ((ys - ty) / hgt - 0.5) * 0.3
        t = 0.55 + lam * 0.5 - k * 0.12 + (vnoise(W, H, 3, seed + i) - 0.5) * 0.1
        ramp(a, m, cols, t, 0.2)
        under = m & ~nb(m, 0, 1)
        paint(a, under, cols[0])
    outline(a, INK_G)
    return a


def bush(seed, W=40, H=30, cols=None, flowers=None):
    cols = cols or LEAF[3:10]
    a = canvas(W, H)
    R = random.Random(seed)
    cl = []
    for i in range(6):
        cl.append((H - 10 + R.uniform(-6, 2), W / 2 + R.uniform(-W * 0.3, W * 0.3), R.uniform(7, 10)))
    cl.sort()
    for j, (ry, rx, r) in enumerate(cl):
        leaf_clump(a, rx, ry, r, cols, seed * 7 + j, bright=(H - 10 - ry) / 30.0, rim_dark=cols[0])
    # 바닥선 평평하게
    a[H - 1:, :] = 0
    if flowers:
        m = a[..., 3] > 0
        ys_, xs_ = np.nonzero(m & erode(m, 2))
        for _ in range(6):
            if len(xs_) == 0:
                break
            i = R.randrange(len(xs_))
            x, y = xs_[i], ys_[i]
            if y > H - 6:
                continue
            for (ddx, ddy) in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                a[y + ddy, x + ddx] = hx(flowers[1])
            a[y, x] = hx(FLOWER_Y[1])
    outline(a, INK_G)
    return a


def grass_tuft(seed, W=16, H=14, cols=None):
    """분수처럼 솟았다 바깥으로 휘어 떨어지는 풀 다발 (레퍼런스)"""
    cols = cols or [GRASS[1], GRASS[2], GRASS[3], GRASS[5], GRASS[6]]
    a = canvas(W, H)
    R = random.Random(seed)
    base = H - 1
    cx = W / 2
    # 뿌리 쪽 짙은 덩어리
    paint(a, ell(W, H, cx, base, W * 0.22, 2.2) & (grid(W, H)[1] <= base), cols[0])
    n = R.randint(4, 6)
    blades = []
    for i in range(n):
        f = (i / (n - 1) - 0.5) * 2                      # -1 … 1
        ang = f * math.radians(62) + R.uniform(-0.12, 0.12)
        L = H * (1.0 - abs(f) * 0.38) * R.uniform(0.8, 1.0)
        blades.append((abs(f), f, ang, L))
    blades.sort(key=lambda b: -b[0])                     # 바깥 → 가운데 (가운데가 앞)
    for (af, f, ang, L) in blades:
        vx, vy = math.sin(ang), math.cos(ang)
        droop = 0.55 + af * 0.9                          # 바깥 잎일수록 끝이 많이 처짐
        steps = int(L * 2.5)
        prev = None
        for s_ in range(steps + 1):
            u = s_ / steps
            x = cx + f * 1.2 + vx * u * L * 0.9
            y = base - (vy * u - droop * u * u * 0.6) * L
            xi, yi = int(round(x)), int(round(y))
            if not (0 <= xi < W and 0 <= yi < H) or (xi, yi) == prev:
                continue
            prev = (xi, yi)
            k = min(len(cols) - 1, 1 + int(u * (len(cols) - 1)))
            a[yi, xi] = hx(cols[k])
            if u < 0.85:                                 # 2px 굵기 (나무·캐릭터와 픽셀 밀도 맞춤)
                xo = xi - (1 if f > 0 else -1) if abs(f) > 0.1 else xi + 1
                if 0 <= xo < W:
                    a[yi, xo] = hx(cols[max(1, k - 1)])
    return a


def flower_patch(seed, W=18, H=12, col=None):
    """꽃 무리: 잎 몇 장 + 꽃 3~5송이 (꽃잎 십자 + 가운데 노랑/흰)"""
    col = col or FLOWER_W
    a = canvas(W, H)
    R = random.Random(seed)
    for _ in range(4):
        x = R.randrange(2, W - 2)
        a[H - 2:H, x] = hx(GRASS[2])
        a[H - 3, x + (1 if R.random() < 0.5 else -1)] = hx(GRASS[3])
    for _ in range(R.randint(3, 5)):
        x, y = R.randrange(2, W - 2), R.randrange(2, H - 3)
        for (ddx, ddy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a[y + ddy, x + ddx] = hx(col[1])
        a[y - 1, x - 1] = hx(col[2])
        a[y + 1, x + 1] = hx(col[0])
        a[y, x] = hx(FLOWER_Y[1] if col is not FLOWER_Y else "#fff8e0")
    return a


def fern(seed, W=22, H=14):
    a = canvas(W, H)
    R = random.Random(seed)
    cols = [GRASS[3], GRASS[5], GRASS[6], GRASS[7]]
    for side in (-1, 1):
        for k in range(2):
            L = R.uniform(7, 10)
            ang0 = math.radians(R.uniform(25, 60)) + k * 0.4
            for s in range(int(L * 2)):
                u = s / (L * 2)
                x = W / 2 + side * math.cos(ang0 * (1 - u * 0.4)) * u * L
                y = H - 1 - math.sin(ang0) * u * L * 1.2 + u * u * 4
                xi, yi = int(x), int(y)
                if 0 <= xi < W and 0 <= yi < H:
                    a[yi, xi] = hx(cols[min(3, int(u * 4))])
                    if s % 2 == 0 and 0 <= yi - 1 < H and 0 <= xi + side < W:
                        a[yi - 1, xi + side] = hx(cols[2])
    return a


def stump(seed, W=26, H=20):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx = W / 2
    body = (np.abs(xs + 0.5 - cx) < 8 + np.clip((ys - H + 6) * 0.9, 0, 4)) & (ys > 6) & (ys < H)
    ramp(a, body, BARK[1:6], 0.8 - (xs - cx + 9) / 20.0, 0.4)
    top = ell(W, H, cx, 7, 8, 3.2)
    paint(a, top, TAN[5])
    ring = ell(W, H, cx, 7, 5, 2) & ~ell(W, H, cx, 7, 3.5, 1.2)
    paint(a, ring, TAN[3])
    a[7, int(cx)] = hx(TAN[2])
    # 이끼
    paint(a, body & (ys < 10) & (xs < cx - 3), GRASS[4])
    outline(a, INK_B)
    return a


def log(seed, W=40, H=16):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    body = (xs > 4) & (xs < W - 6) & (ys > 3) & (ys < H - 1)
    ramp(a, body, BARK[1:6], 0.85 - (ys - 3) / 13.0, 0.5)
    for x in range(8, W - 8, 6):
        a[5:H - 3, x] = hx(BARK[2])
    end = ell(W, H, W - 6, (H + 2) / 2, 4, 6)
    paint(a, end, TAN[5])
    paint(a, ell(W, H, W - 6, (H + 2) / 2, 2, 3), TAN[3])
    moss = body & (ys < 7) & (vnoise(W, H, 3, seed) > 0.5)
    paint(a, moss, GRASS[5])
    outline(a, INK_B)
    return a


def rock(seed, W=24, H=16, cols=None, flat=True):
    """납작한 크림색 바위 (레퍼런스)"""
    cols = cols or ["#4a4440", "#625a52", "#7a7064", "#948876", "#ac9f8a", "#c4b8a0"]   # 바위: 중간 명도 (아이템처럼 튀지 않게)
    a = canvas(W, H)
    R = random.Random(seed)
    m, dx, dy = blob_mask(W, H, W / 2, H / 2 + 1, W * 0.42, seed, bump=0.08, squash=(0.55 if flat else 0.85))
    lam = -(dx * LX + dy * LY)
    t = 0.55 + lam * 0.35 + (vnoise(W, H, 3, seed) - 0.5) * 0.2
    ramp(a, m, cols, t, 0.6)
    # 금 한 줄
    x0 = R.randrange(W // 3, W * 2 // 3)
    for i in range(4):
        y = int(H / 2 - 1 + i)
        if 0 <= y < H and m[y, x0 + i // 2]:
            a[y, x0 + i // 2] = hx(cols[0])
    # 아래 그늘
    paint(a, m & ~nb(m, 0, 1), cols[0])
    outline(a, INK_B)
    return a


def mushroom_patch(seed, W=16, H=12):
    a = canvas(W, H)
    R = random.Random(seed)
    for k in range(R.randint(2, 3)):
        x = R.randrange(3, W - 3)
        hgt = R.randint(3, 5)
        r = R.choice([2, 3])
        y = H - 1
        a[y - hgt:y + 1, x] = hx(FLOWER_W[1])
        cap = ell(W, H, x + 0.5, y - hgt, r + 0.5, 1.8)
        cap &= grid(W, H)[1] <= y - hgt + 0.5
        paint(a, cap, "#c8524c")
        a[int(y - hgt - 1), x - 1] = hx("#f0c0b0")
    return a


def glow_shroom(seed, W=18, H=14, cols=("#1a4a5a", "#2a8aa0", "#5ad8e0", "#c8fff8")):
    """빛나는 버섯 무리 (어두운 숲의 광원 — 청록 갓에 밝은 점)"""
    a = canvas(W, H)
    R = random.Random(seed)
    xs, ys = grid(W, H)
    for k in range(R.randint(3, 4)):
        x = R.randrange(3, W - 3)
        hgt = R.randint(3, 7)
        r = R.choice([2, 2.5, 3])
        y = H - 1
        a[y - hgt:y + 1, x] = hx("#d8e8e0")
        cap = ell(W, H, x + 0.5, y - hgt, r + 0.5, 1.9) & (ys <= y - hgt + 0.5)
        ramp(a, cap, list(cols[:3]), 0.7 - (xs - x) / 8 - (ys - y + hgt) / 4, 0.2)
        a[int(y - hgt - 1), x] = hx(cols[3])
        if r > 2:
            a[int(y - hgt), x + 1] = hx(cols[3])
    return a


def main():
    items = []
    items.append(("tree0", tree_broad(3)))
    items.append(("tree1", tree_broad(7, W=96, H=118, trunk_h=28)))
    items.append(("tree2", tree_broad(11, W=120, H=140, trunk_h=38)))
    items.append(("tree_light0", tree_broad(17, W=100, H=124, cols=LEAF_LIGHT, trunk_h=30)))
    items.append(("tree_bloom0", tree_broad(23, W=104, H=126, cols=BLOOM, trunk_h=30)))
    items.append(("tree_bloom1", tree_broad(29, W=88, H=110, cols=BLOOM, trunk_h=26)))
    items.append(("pine0", tree_pine(5)))
    items.append(("pine1", tree_pine(9, W=48, H=88)))
    for i in range(3):
        items.append((f"bush{i}", bush(20 + i, flowers=FLOWER_W if i == 1 else None)))
    items.append(("bush_light", bush(29, cols=LEAF_LIGHT)))
    for i in range(6):
        items.append((f"tuft{i}", grass_tuft(40 + i, W=16 if i < 4 else 20, H=13 if i < 4 else 16)))
    for i in range(2):
        items.append((f"tuft_dark{i}", grass_tuft(60 + i, cols=[GRASS[2], GRASS[4], GRASS[5], GRASS[6]])))
    for i in range(3):
        items.append((f"flower{i}", flower_patch(70 + i)))
    items.append(("flower_y", flower_patch(75, col=FLOWER_Y)))
    items.append(("flower_p", flower_patch(76, col=FLOWER_P)))
    items.append(("flower_b", flower_patch(77, col=FLOWER_B)))
    for i in range(2):
        items.append((f"fern{i}", fern(80 + i)))
    items.append(("stump0", stump(90)))
    items.append(("log0", log(91)))
    for i in range(3):
        items.append((f"rock{i}", rock(100 + i, W=[24, 18, 30][i], H=[16, 12, 18][i])))
    items.append(("mush0", mushroom_patch(110)))
    items.append(("glow0", glow_shroom(120)))
    items.append(("glow1", glow_shroom(121, cols=("#3a1a5a", "#7a3ab0", "#c080f0", "#f8e0ff"))))
    at, pos = pack(items, os.path.join(OUT, "sprites", "flora"), 512)
    preview(at, "flora_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
