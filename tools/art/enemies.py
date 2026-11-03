"""숲 몬스터 v2 (옆모습 — 왼쪽은 뒤집어서) · 셀 음영 + 색 외곽선 (shade.py)

종류: 도토리 병정 · 자폭 도토리 · 독버섯 · 치유 버섯 · 숲 거미 · 서리 거미 · 황금 거미 + 탄(포자·거미줄·서리)
동작: idle 4 · move 6 · attack 3(준비·공격·회복) · hurt 2 · die 4
→ assets/sprites/enemies.png + enemies.json   이름: <종류>_<동작><번호>
"""
from px import *
from pack import pack
from shade import *

INK = "#221a26"

# ------------------------------------------------------------------ 팔레트
ACORN = ["#4a2a18", "#7a4a2a", "#a8683a", "#cc8a4e", "#eab070"]
ACORN_CAP = ["#2e2218", "#4a3826", "#6a5236", "#8c6e4a", "#ae8e62"]
BOOM = ["#5a1c18", "#8a2c22", "#ba4630", "#e2683e", "#ff9a5e"]
BOOM_CAP = ["#2a1c1a", "#44302a", "#62463a", "#84604c"]
SHROOM_CAP = ["#4e1430", "#7e2440", "#ae3a4c", "#d85a5a", "#f48a7a"]
HEAL_CAP = ["#163e3a", "#22605a", "#2e8a78", "#4ab496", "#86dcb2"]
STEM = ["#8a7a68", "#b4a48c", "#d8ccb2", "#f2ead6", "#fffaf0"]
GILL = ["#a08670", "#c8b094", "#e2ceae"]
SPIDER = ["#170f1e", "#251a34", "#3a2a50", "#56406e", "#76608e", "#9a86b0"]
FROST = ["#1e3450", "#2e5476", "#45789e", "#6aa4c6", "#9ccce4", "#dcf4ff"]
GOLDB = ["#5a3a10", "#8a5e1a", "#bc8a26", "#e6b83a", "#ffe068", "#fff8c0"]
WOOD = ["#5a3c22", "#80583a", "#a87c54"]
STONE_T = ["#5a5a66", "#8a8a96", "#c8c8d2"]
RED_EYE = "#ff4a3a"
WHITE = "#fffaf0"

ANIMS = {"idle": 4, "move": 6, "attack": 3, "hurt": 2, "die": 4}


def _frame(W, H):
    return canvas(W, H), *grid(W, H)


# ================================================================== 도토리 (병정 · 자폭)
def acorn_base(anim, i, boom=False):
    W, H = 26, 30
    a, xs, ys = _frame(W, H)
    body_c = BOOM if boom else ACORN
    cap_c = BOOM_CAP if boom else ACORN_CAP
    bob = {"idle": [0, 0, 1, 1], "move": [0, -1, -2, 0, -1, -2], "attack": [1, -1, 0], "hurt": [1, 1], "die": [1, 2, 3, 4]}[anim][i]
    lean = {"attack": [-2, 3, 1], "hurt": [-2, -1], "move": [1] * 6}.get(anim, [0] * 6)[i]
    swell = 0.0
    if boom and anim == "attack":
        swell = [1.2, 2.2, 1.6][i]
    if boom and anim == "move":
        lean = 2
    cx, cy = 12.5 + lean, 18.0 + bob
    if anim == "die":
        return acorn_die(a, xs, ys, i, body_c, cap_c, boom)
    # 다리 (짧고 통통) — 걸음 6단
    step = [0, 1, 2, 0, -1, -2][i] if anim == "move" else 0
    for k, (lx, sgn) in enumerate(((-2.5, 1), (2.0, -1))):
        off = step * sgn * 0.8
        lift = 1 if (anim == "move" and ((i // 3) == k)) else 0
        x = cx + lx + off
        fill(a, poly_mask(W, H, [(x - 1.2, cy + 5), (x + 1.4, cy + 5), (x + 1.6, H - 2 - lift), (x - 1.6, H - 2 - lift)]), body_c[1])
        fill(a, poly_mask(W, H, [(x - 1.8, H - 3 - lift), (x + 2.6, H - 3 - lift), (x + 2.6, H - 1.5 - lift), (x - 1.8, H - 1.5 - lift)]), body_c[0])
    # 몸 (도토리 알) + 뾰족한 끝
    rx, ry = 6.8 + swell, 7.4 + swell * 0.8
    body = blob(a, cx, cy, rx, ry, body_c[1:], cut=[0.3, 0.55, 0.8])
    tip = poly_mask(W, H, [(cx - 1.6, cy + ry - 1.2), (cx + 1.6, cy + ry - 1.2), (cx + 0.2, cy + ry + 1.6)])
    fill(a, tip & ~body, body_c[1])
    # 세로 결 두 줄 (어두운 면에만)
    for dx in (2.5, 4.4):
        fill(a, body & (np.abs(xs + 0.5 - (cx + dx)) < 0.5) & (ys > cy - 3) & (ys < cy + 5), body_c[1])
    # 깍정이 모자: 반원 + 비늘 줄무늬 + 꼭지
    cap_y = cy - ry + 3.0
    cap = ell(W, H, cx, cap_y, rx + 1.6, 4.8) & (ys < cap_y + 1.6)
    bands(a, cap, cap_c[1:], sphere_t(W, H, cx - 1, cap_y - 1, rx + 1.6, 4.8), cut=[0.3, 0.62])
    for row in range(3):
        yy = cap_y - 2.6 + row * 1.7
        for kx in range(-4, 5):
            px_ = cx + kx * 2.0 + (1.0 if row % 2 else 0.0)
            put_m = cap & (np.abs(xs + 0.5 - px_) < 0.6) & (np.abs(ys + 0.5 - yy) < 0.6)
            fill(a, put_m, cap_c[0])
    fill(a, cap & ~nb(cap, 0, 1), cap_c[0])                              # 모자 밑선
    fill(a, poly_mask(W, H, [(cx - 0.8, cap_y - 4.8), (cx + 0.8, cap_y - 4.8), (cx + 1.6, cap_y - 7.0), (cx + 0.4, cap_y - 7.2)]), cap_c[2])
    # 얼굴 (오른쪽을 봄)
    ex, ey = int(cx + 1.0), int(cy)
    if anim == "hurt":
        for (x0, y0) in ((ex, ey), (ex + 3, ey)):            # > < 질끈
            put(a, x0, y0, INK); put(a, x0 + 1, y0 + 1, INK); put(a, x0, y0 + 2, INK)
    elif boom:
        for x0 in (ex, ex + 3):                              # 동그랗게 뜬 눈 (흰자 + 작은 눈동자)
            fill(a, ell(W, H, x0 + 0.5, ey + 1, 1.3, 1.5), WHITE)
            put(a, x0 + 1, ey + 1, INK)
        for k in range(4):                                   # 히죽 웃는 입
            put(a, ex + k, ey + 4 + (1 if k in (1, 2) else 0), INK)
    else:
        for x0 in (ex, ex + 3):
            put(a, x0, ey, INK); put(a, x0 + 1, ey, INK); put(a, x0, ey + 1, INK); put(a, x0 + 1, ey + 1, INK)
            put(a, x0, ey, WHITE)
        for k in range(2):                                   # 화난 눈썹 (안쪽으로 내려감)
            put(a, ex - 1 + k, ey - 2 + k, INK)
            put(a, ex + 5 - k, ey - 2 + k, INK)
        if anim == "attack" and i == 1:
            fill(a, poly_mask(W, H, [(ex + 0.5, ey + 3.5), (ex + 3.5, ey + 3.5), (ex + 2, ey + 5.5)]), "#3a1a1a")
    # 볼 홍조
    put(a, ex - 1, ey + 3, body_c[4])
    if boom:
        # 도화선 + 불꽃 (번갈아 반짝)
        fx0, fy0 = cx + 0.5, cap_y - 6.5
        pts = [(fx0, fy0), (fx0 + 1.5, fy0 - 2.5), (fx0 + 3.5, fy0 - 3.5)]
        for p0, p1 in zip(pts, pts[1:]):
            fill(a, line_mask(W, H, p0, p1, 1.0), "#d8c8a0")
        sx, sy = pts[-1][0] + 1, pts[-1][1] - 0.5
        hot = (i + (1 if anim == "move" else 0)) % 2 == 0 or anim == "attack"
        fill(a, ell(W, H, sx, sy, 1.8 if hot else 1.3, 1.8 if hot else 1.3), "#ffe060")
        put(a, sx, sy, "#ffffff")
        if hot:
            for d in ((-2, -1), (2, -2), (1, 1), (-1, 2)):
                put(a, sx + d[0], sy + d[1], "#ff9a3a")
        if anim == "attack":                                  # 부풀며 금이 가고 속에서 빛이 샌다
            for (p0, p1) in (((cx - 3, cy - 3), (cx - 1, cy + 1)), ((cx - 1, cy + 1), (cx - 3, cy + 4)), ((cx + 3, cy + 2), (cx + 5, cy - 1))):
                fill(a, line_mask(W, H, p0, p1, 1.0) & body, "#fff0a0" if i == 1 else "#ffb040")
        # 팔: 만세
        for sx_ in (-1, 1):
            hx_ = cx + sx_ * (rx + 0.5)
            fill(a, line_mask(W, H, (cx + sx_ * (rx - 1.5), cy), (hx_ + sx_ * 1.0, cy - 4 - (i % 2)), 1.6), body_c[2])
    else:
        # 나뭇가지 창 + 돌촉 + 손
        if anim == "attack":
            base, tip = [((cx - 2, cy + 3), (cx - 7, cy - 9)), ((cx + 1, cy + 2), (cx + 13, cy + 0)), ((cx, cy + 3), (cx + 10, cy - 3))][i]
        else:
            sway = [0, 0.5, 1, 0.5, 0, -0.5][i % 6] if anim == "move" else [0, 0, 0.5, 0.5][i % 4]
            base, tip = (cx + 8.0, cy + 6), (cx + 9.5 + sway, cy - 11)
        fill(a, line_mask(W, H, base, tip, 1.4), WOOD[1])
        fill(a, line_mask(W, H, base, tip, 0.5), WOOD[2])
        ang = math.atan2(tip[1] - base[1], tip[0] - base[0])
        ux, uy = math.cos(ang), math.sin(ang)
        px_, py_ = -uy, ux
        head_ = poly_mask(W, H, [(tip[0] + ux * 3.2, tip[1] + uy * 3.2), (tip[0] + px_ * 1.6, tip[1] + py_ * 1.6), (tip[0] - px_ * 1.6, tip[1] - py_ * 1.6)])
        fill(a, head_, STONE_T[1])
        put(a, tip[0] + ux * 1.2 - px_ * 0.5, tip[1] + uy * 1.2 - py_ * 0.5, STONE_T[2])
        hand = (base[0] + (tip[0] - base[0]) * 0.32, base[1] + (tip[1] - base[1]) * 0.32)
        fill(a, ell(W, H, hand[0], hand[1], 1.5, 1.5), body_c[3])
        if anim == "attack" and i == 1:                       # 찌르기 잔상
            for k in range(3):
                yy = cy - 1 + k * 1.5
                fill(a, line_mask(W, H, (cx - 8 + k, yy), (cx - 4 + k, yy), 0.5), "#fff4d6")
    selout(a, INK)
    return a


def acorn_die(a, xs, ys, i, body_c, cap_c, boom):
    """쪼개져 두 쪽으로 → 땅에 누움 → 납작"""
    W, H = a.shape[1], a.shape[0]
    cx, cy = 12.5, 19.0 + i * 1.2
    gap = [0.5, 2.0, 3.5, 4.0][i]
    tilt = [0, 0.15, 0.4, 0.6][i]
    for side in (-1, 1):
        hcx = cx + side * gap
        half = ell(W, H, hcx, cy, 6.6, 7.2 - i * 0.8) & ((xs + 0.5 - hcx) * side >= -0.3 + (ys - cy) * tilt * side)
        bands(a, half, body_c[1:], sphere_t(W, H, hcx - side, cy, 6.6, 7.0), cut=[0.3, 0.55, 0.8])
        # 속살 (쪼개진 면)
        face = half & ~nb(half, -side, 0) | (half & (np.abs(xs + 0.5 - hcx) < 0.8))
        fill(a, face, "#f0d8a8")
    if i < 3:
        cap = ell(W, H, cx + 2 + i, cy - 8 + i * 2, 7.0, 4.0) & (ys < cy - 6 + i * 2)
        bands(a, cap, cap_c[1:], sphere_t(W, H, cx + 1 + i, cy - 9 + i * 2, 7, 4), cut=[0.3, 0.62])
    # 별빛 (기절 표시)
    if i <= 2:
        for k in range(3):
            ang = i * 0.9 + k * 2.1
            put(a, cx + math.cos(ang) * 6, cy - 12 + math.sin(ang) * 1.5, "#ffe070")
    selout(a, INK)
    if i == 3:
        a[..., 3] = (a[..., 3] * 0.7).astype(np.uint8)
    return a


def acorn(anim, i):
    return acorn_base(anim, i, False)


def boomer(anim, i):
    return acorn_base(anim, i, True)


# ================================================================== 버섯 (독 · 치유)
def shroom_base(anim, i, heal=False):
    W, H = 28, 30
    a, xs, ys = _frame(W, H)
    cap_c = HEAL_CAP if heal else SHROOM_CAP
    sq = {"idle": [0, 0.5, 1, 0.5], "move": [0, -1.5, -2.5, -1, 1, 0.5], "attack": [2, -2.5, 0], "hurt": [1.5, 1], "die": [1, 3, 5, 7]}[anim][i]
    cx = 13.5 + {"hurt": [-1, -1]}.get(anim, [0] * 6)[i]
    top = 4.0 + sq                                        # 갓 꼭대기
    lift = -sq if anim == "move" and sq < 0 else 0        # 깡충: 공중에 뜸
    if anim == "die":
        return shroom_die(a, xs, ys, i, cap_c, heal)
    floor = H - 2 - lift
    # 발 두 개
    for k, dx in enumerate((-3.0, 2.0)):
        fill(a, ell(W, H, cx + dx, floor - 0.6, 2.4, 1.6), STEM[1])
    # 줄기 몸 (아래가 통통)
    stem_top = top + 9.0
    body = poly_mask(W, H, [(cx - 3.6, stem_top), (cx + 3.6, stem_top), (cx + 5.2, floor - 1.2), (cx - 5.2, floor - 1.2)])
    body |= ell(W, H, cx, floor - 3.2, 5.2, 3.0)
    t = sphere_t(W, H, cx - 1, (stem_top + floor) / 2, 6.0, 8.0)
    bands(a, body, STEM[1:], t, cut=[0.3, 0.58, 0.85])
    # 얼굴
    ex, ey = int(cx + 1), int(stem_top + 3)
    if anim == "hurt":
        for x0 in (ex, ex + 3):
            put(a, x0, ey, INK); put(a, x0 + 1, ey + 1, INK); put(a, x0, ey + 2, INK)
    else:
        blink = anim == "idle" and i == 3
        for x0 in (ex, ex + 3):
            if blink:
                put(a, x0, ey + 1, INK)
            else:
                put(a, x0, ey, WHITE if not heal else "#ffffff"); put(a, x0, ey + 1, INK); put(a, x0, ey + 2, INK)
        if heal:
            put(a, ex, ey + 4, INK); put(a, ex + 1, ey + 5, INK); put(a, ex + 2, ey + 5, INK); put(a, ex + 3, ey + 4, INK)
        elif anim == "attack" and i == 1:
            fill(a, ell(W, H, ex + 1.5, ey + 5, 1.4, 1.2), "#3a1a2a")
        else:
            put(a, ex + 1, ey + 4, INK); put(a, ex + 2, ey + 4, INK)
        put(a, ex - 1, ey + 3, "#f0b0a0")
    # 팔 (작은 손)
    arm_y = stem_top + 6
    wave = [0, -1, 0, 1, 0, -1][i % 6] if anim in ("move", "idle") else (-3 if anim == "attack" and i == 1 else 0)
    fill(a, line_mask(W, H, (cx + 3.5, arm_y), (cx + 6.5, arm_y + 2 + wave), 1.6), STEM[2])
    fill(a, line_mask(W, H, (cx - 3.5, arm_y), (cx - 6.0, arm_y + 2 - wave), 1.6), STEM[1])
    if heal:                                             # 잎사귀 지팡이
        sx, sy = cx + 7.0, arm_y + 2 + wave
        fill(a, line_mask(W, H, (sx, sy + 4), (sx + 1, sy - 6), 1.0), WOOD[1])
        leaf = ell(W, H, sx + 2.2, sy - 7, 2.2, 1.4)
        fill(a, leaf, "#7ad08a")
        fill(a, leaf & (ys + 0.5 > sy - 7), "#4a9a5a")
        if anim == "attack" and i == 1:
            for d in ((-2, -2), (3, -3), (4, 1), (-1, 2)):
                put(a, sx + 2 + d[0], sy - 7 + d[1], "#d0ffd8")
    # 갓
    rx = 11.0 - sq * 0.35
    ry = 7.0 + sq * 0.25
    cap = ell(W, H, cx, top + ry, rx, ry) & (ys + 0.5 <= top + ry + 1.5)
    bands(a, cap, cap_c[1:], sphere_t(W, H, cx - 1.5, top + ry - 1, rx, ry), cut=[0.28, 0.55, 0.8])
    # 갓 아래 주름(gill) 띠
    rim_y = top + ry + 1.5
    gill = ell(W, H, cx, rim_y - 0.2, rx - 1.2, 1.8) & (ys + 0.5 > rim_y - 0.8)
    fill(a, gill, GILL[1])
    for kx in range(-int(rx) + 2, int(rx) - 1, 2):
        fill(a, gill & (np.abs(xs + 0.5 - (cx + kx)) < 0.5), GILL[0])
    fill(a, cap & ~nb(cap, 0, 1) & ~gill, cap_c[0])
    # 점박이 (치유 버섯은 잎 무늬 십자)
    if heal:
        for (dx, dy) in ((-4.5, 3.0), (3.0, 2.0), (6.5, 5.0)):
            px_, py_ = cx + dx, top + dy
            for d in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                fill(a, cap & (np.abs(xs + 0.5 - (px_ + d[0])) < 0.5) & (np.abs(ys + 0.5 - (py_ + d[1])) < 0.5), "#e8fff0")
        if anim == "attack":
            glow = cap & (sphere_t(W, H, cx, top + ry, rx, ry) > 0.75)
            fill(a, glow, "#c8ffe0")
    else:
        for (dx, dy, r) in ((-5.5, 3.2, 1.7), (1.5, 1.6, 2.1), (6.5, 4.4, 1.3), (-1.0, 5.6, 1.1), (4.0, 6.4, 0.9)):
            sp = ell(W, H, cx + dx, top + dy, r, r * 0.85) & cap
            fill(a, sp, STEM[3])
            fill(a, sp & ~nb(sp, 0, 1), STEM[1])
    # 하이라이트
    put(a, cx - rx * 0.45, top + 1.6, cap_c[4])
    put(a, cx - rx * 0.45 + 1, top + 1.6, cap_c[4])
    # 포자 뿜기 (독버섯 공격)
    if not heal and anim == "attack" and i == 1:
        for (dx, dy, r) in ((-3, -1.5, 2.0), (2, -2.5, 2.4), (6, -0.5, 1.6)):
            fill(a, ell(W, H, cx + dx, top + dy, r, r * 0.8), "#a8d060")
            put(a, cx + dx - 0.5, top + dy - 0.5, "#e0f8a0")
    selout(a, INK)
    return a


def shroom_die(a, xs, ys, i, cap_c, heal):
    W, H = a.shape[1], a.shape[0]
    cx = 13.5
    # 줄기가 주저앉고 갓이 옆으로 쓰러진다
    h = [8, 5, 3, 2][i]
    stem = poly_mask(W, H, [(cx - 4, H - 2 - h), (cx + 4, H - 2 - h), (cx + 5.5, H - 2), (cx - 5.5, H - 2)])
    bands(a, stem, STEM[1:], sphere_t(W, H, cx - 1, H - 4, 6, 5), cut=[0.3, 0.58, 0.85])
    ang = [0.15, 0.5, 0.9, 1.2][i]
    ccx, ccy = cx + 3 + i * 1.5, H - 2 - h - 3 + i * 1.6
    rx, ry = 10.5, 6.0 - i * 0.4
    cap = ell(W, H, ccx, ccy, rx, ry)
    # 기울이기: 위쪽 반만 남기되 축이 기운다
    cap &= ((ys + 0.5 - ccy) * math.cos(ang) - (xs + 0.5 - ccx) * math.sin(ang)) < 1.0
    bands(a, cap, cap_c[1:], sphere_t(W, H, ccx - 1, ccy - 1, rx, ry), cut=[0.28, 0.55, 0.8])
    if i < 3:
        for k in range(3):
            put(a, cx - 4 + k * 4, H - 12 - i * 2 - (k % 2), "#c0e080" if not heal else "#c0ffd8")
    selout(a, INK)
    if i == 3:
        a[..., 3] = (a[..., 3] * 0.7).astype(np.uint8)
    return a


def shroom(anim, i):
    return shroom_base(anim, i, False)


def healer(anim, i):
    return shroom_base(anim, i, True)


# ================================================================== 거미 (숲 · 서리 · 황금)
def spider_base(anim, i, kind="spider"):
    W, H = 34, 24
    a, xs, ys = _frame(W, H)
    pal = {"spider": SPIDER, "frost": FROST, "goldbug": GOLDB}[kind]
    bob = {"idle": [0, 0, 1, 1], "move": [0, -1, 0, 0, -1, 0], "attack": [1, -2, 0], "hurt": [1, 1], "die": [0, 1, 2, 3]}[anim][i]
    lean = {"attack": [-3, 4, 1], "hurt": [-2, -1]}.get(anim, [0] * 6)[i]
    cx, cy = 16.0 + lean, 13.0 + bob
    if anim == "die":
        return spider_die(a, xs, ys, i, pal, kind)
    floor = H - 1.5
    # 다리 8개: 몸 가운데에서 위로 꺾인 무릎 → 땅 (네 다리씩 번갈아)
    rear = anim == "attack" and i == 0
    for side in (-1, 1):                                     # -1 뒤쪽(어둡게) · 1 앞쪽
        for k in range(4):
            spread = k - 1.5
            ph = (k + (0 if side > 0 else 1)) % 2
            gait = 0
            if anim == "move":
                gait = [0, 1, 2, 1, 0, -1][(i + ph * 3) % 6]
            root = (cx + 1 + spread * 1.6, cy + 1)
            knee = (cx + 1 + spread * 6.2 + gait * 0.8 + side * 0.6, cy - 5.5 + abs(spread) * 0.9 - (1 if gait > 0 else 0))
            foot = (cx + 1 + spread * 10.5 + gait * 1.2 + side * 0.8, floor - (1.5 if gait > 1 else 0))
            if rear and k >= 2:                              # 앞다리 번쩍
                knee = (cx + 5 + k * 2, cy - 8)
                foot = (cx + 9 + k * 2, cy - 3)
            col = pal[1] if side < 0 else pal[2]
            fill(a, line_mask(W, H, root, knee, 1.6), col)
            fill(a, line_mask(W, H, knee, foot, 1.1), col)
            put(a, knee[0], knee[1] - 0.5, pal[3] if side > 0 else pal[2])
            if kind == "goldbug":
                put(a, foot[0], foot[1], pal[3])
    # 배 (뒤, 크게) + 머리가슴 (앞)
    ab_c = (cx - 5.5, cy - 1.0)
    abd = blob(a, ab_c[0], ab_c[1], 8.0, 6.2, pal[1:5], cut=[0.25, 0.5, 0.78])
    # 털 (가장자리 몇 가닥)
    for (dx, dy) in ((-7, -5), (-3, -6), (-10, -2), (-9, 3)):
        put(a, ab_c[0] + dx * 0.95, ab_c[1] + dy * 0.98, pal[2])
    tho = blob(a, cx + 3.5, cy, 4.6, 3.8, pal[1:5], cut=[0.25, 0.5, 0.78])
    head = blob(a, cx + 7.5, cy + 0.5, 3.4, 3.0, pal[1:6], cut=[0.25, 0.5, 0.75, 0.92])
    # 배 무늬
    if kind == "spider":
        hg = poly_mask(W, H, [(ab_c[0] - 2, ab_c[1] - 3), (ab_c[0] + 2, ab_c[1] - 3), (ab_c[0] - 0.5, ab_c[1]), (ab_c[0] + 2, ab_c[1] + 3), (ab_c[0] - 2, ab_c[1] + 3), (ab_c[0] + 0.5, ab_c[1])])
        fill(a, hg & abd, "#c8344a")
        put(a, ab_c[0] - 1, ab_c[1] - 2, "#ff6a7a")
    elif kind == "frost":
        for (dx, h) in ((-4, 5), (-1, 7), (2, 5)):           # 얼음 결정 가시
            x0 = ab_c[0] + dx
            spike = poly_mask(W, H, [(x0 - 1.4, ab_c[1] - 4.5), (x0 + 1.4, ab_c[1] - 4.5), (x0 + 0.3, ab_c[1] - 4.5 - h)])
            fill(a, spike, FROST[4])
            fill(a, spike & (xs + 0.5 < x0), FROST[5])
        fill(a, ell(W, H, ab_c[0] - 2, ab_c[1] + 1, 2.0, 1.4) & abd, FROST[5])
    else:                                                    # 황금: 동전 무늬 + 반짝 + 등에 보따리
        for (dx, dy) in ((-4, 0), (0, 2), (-1, -2)):
            m = ell(W, H, ab_c[0] + dx, ab_c[1] + dy, 1.5, 1.5) & abd
            fill(a, m, GOLDB[5])
            fill(a, m & ~nb(m, 0, 1), GOLDB[2])
        sack = ell(W, H, ab_c[0] - 1, ab_c[1] - 7, 3.6, 3.0)
        bands(a, sack, ["#6a4a2a", "#8a6a3e", "#ae8a54"], sphere_t(W, H, ab_c[0] - 2, ab_c[1] - 8, 3.6, 3.0), cut=[0.35, 0.7])
        fill(a, line_mask(W, H, (ab_c[0] - 2, ab_c[1] - 10), (ab_c[0] + 0.5, ab_c[1] - 10.5), 1.0), "#c8a060")
        put(a, ab_c[0] - 1, ab_c[1] - 7, "#ffe068")
        if (i % 2 == 0) and anim != "hurt":
            sx, sy = ab_c[0] - 6, ab_c[1] - 3
            for d in ((0, 0), (1, 0), (-1, 0), (0, 1), (0, -1)):
                put(a, sx + d[0], sy + d[1], "#ffffff" if d == (0, 0) else "#fff8c0")
    # 눈: 큰 눈 둘 + 작은 눈 둘
    eye_c = {"spider": RED_EYE, "frost": "#a8f0ff", "goldbug": "#1a1420"}[kind]
    hx0, hy0 = int(cx + 8.5), int(cy - 1)
    if anim == "hurt":
        put(a, hx0, hy0, INK); put(a, hx0 + 1, hy0 + 1, INK); put(a, hx0 + 2, hy0, INK)
    else:
        for (dx, dy) in ((0, 0), (2, 0)):
            put(a, hx0 + dx, hy0 + dy, eye_c)
            put(a, hx0 + dx, hy0 + dy + 1, eye_c)
        put(a, hx0 + 1, hy0 - 1, eye_c)
        put(a, hx0 - 1, hy0 + 1, eye_c)
        put(a, hx0, hy0, "#ffffff" if kind != "frost" else "#ffffff")
    # 송곳니
    fang = 2 if (anim == "attack" and i == 1) else 1
    for dx in (1, 3):
        for k in range(fang + 1):
            put(a, hx0 + dx - 1, cy + 3 + k, "#f4ecdc")
    if anim == "attack" and i == 1 and kind == "frost":
        for k in range(4):                                   # 서리 입김
            put(a, hx0 + 5 + k, cy + 2 + (k % 2), FROST[5] if k % 2 == 0 else FROST[4])
    selout(a, INK)
    return a


def spider_die(a, xs, ys, i, pal, kind):
    """뒤집혀 다리를 오므린다"""
    W, H = a.shape[1], a.shape[0]
    cx, cy = 16.0, 15.0 + i
    curl = [0.2, 0.5, 0.8, 1.0][i]
    abd = blob(a, cx - 3, cy, 7.5, 5.5 - i * 0.6, pal[1:5], cut=[0.25, 0.5, 0.78])
    blob(a, cx + 5, cy + 0.5, 4.2, 3.4 - i * 0.3, pal[1:5], cut=[0.25, 0.5, 0.78])
    for k in range(4):
        base = (cx - 3 + k * 3, cy - 4 + i * 0.6)
        tip = (base[0] + (k - 1.5) * 3 * (1 - curl), base[1] - 5 * (1 - curl) - 2)
        fill(a, line_mask(W, H, base, tip, 1.2), pal[2])
        put(a, tip[0], tip[1], pal[3])
    # X 눈
    put(a, cx + 6, cy - 1, INK); put(a, cx + 7, cy, INK); put(a, cx + 8, cy - 1, INK); put(a, cx + 6, cy + 1, INK); put(a, cx + 8, cy + 1, INK)
    selout(a, INK)
    if i == 3:
        a[..., 3] = (a[..., 3] * 0.7).astype(np.uint8)
    return a


def spider(anim, i):
    return spider_base(anim, i, "spider")


def frost(anim, i):
    return spider_base(anim, i, "frost")


def goldbug(anim, i):
    return spider_base(anim, i, "goldbug")


# ================================================================== 탄
def spore(i):
    a = canvas(10, 10)
    r = 3.0 + (i % 2) * 0.4
    blob(a, 5, 5, r, r, ["#4a6a2a", "#7aa040", "#b0d860", "#e0f8a0"], cut=[0.25, 0.55, 0.85])
    put(a, 4, 3, "#ffffff")
    if i % 2:
        for d in ((-4, 0), (4, 1), (0, -4)):
            put(a, 5 + d[0], 5 + d[1], "#c0e080")
    selout(a, "#1e2a16")
    return a


def web_ball(i):
    a = canvas(12, 12)
    c = 6
    for k in range(4):
        ang = k * math.pi / 4 + i * 0.4
        fill(a, line_mask(12, 12, (c - math.cos(ang) * 5, c - math.sin(ang) * 5), (c + math.cos(ang) * 5, c + math.sin(ang) * 5), 0.5), "#f0f0fa")
    xs, ys = grid(12, 12)
    d = np.sqrt((xs + 0.5 - c) ** 2 + (ys + 0.5 - c) ** 2)
    fill(a, (np.abs(d - 2.2) < 0.5) | (np.abs(d - 4.0) < 0.45), "#d8d8e6")
    fill(a, d < 1.2, "#ffffff")
    return a


def frost_ball(i):
    a = canvas(10, 10)
    blob(a, 5, 5, 3.2, 3.2, FROST[2:], cut=[0.3, 0.6, 0.85])
    for d in (((5, 0), (5, 10)), ((0, 5), (10, 5))):
        fill(a, line_mask(10, 10, d[0], d[1], 0.5) & (a[..., 3] == 0), FROST[5] if i % 2 == 0 else FROST[4])
    put(a, 4, 3, "#ffffff")
    return a


KINDS = (("acorn", acorn), ("boomer", boomer), ("shroom", shroom), ("healer", healer),
         ("spider", spider), ("frost", frost), ("goldbug", goldbug))


def main():
    items = []
    for name, fn in KINDS:
        for anim, n in ANIMS.items():
            for i in range(n):
                items.append((f"{name}_{anim}{i}", fn(anim, i)))
    for i in range(2):
        items.append((f"spore{i}", spore(i)))
        items.append((f"web{i}", web_ball(i)))
        items.append((f"frostb{i}", frost_ball(i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "enemies"), 512, keep_bottom=False, trim_cells=False)
    d = dict(items)
    cols = sum(ANIMS.values())
    pv = canvas(36 * cols, 32 * len(KINDS))
    for r, (name, _) in enumerate(KINDS):
        k = 0
        for anim, n in ANIMS.items():
            for i in range(n):
                over(pv, d[f"{name}_{anim}{i}"], k * 36, r * 32)
                k += 1
    preview(pv, "enemies_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
