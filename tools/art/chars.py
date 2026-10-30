"""임시 꼬마 캐릭터 (레퍼런스: 마일드 던전 — 큰 머리 · 짧은 몸 · 둥근 실루엣)

진짜 캐릭터 시트가 오면 바꾼다. 지금은 움직임·카메라·조작 확인용.
방향: s(앞) n(뒤) e(옆, 왼쪽은 뒤집어서)
동작: idle 2 · walk 4 · run 4 · roll 4 · jump 1 · fall 1 · attack 3 · hurt 1
→ assets/sprites/chars.png + chars.json   이름: <캐릭터>_<동작>_<방향><번호>
그림 크기 24x32, 발바닥 = 맨 아래 줄
"""
from px import *
from pack import pack

FW, FH = 24, 32
OUTL = "#2a2430"
SKIN = ["#c98a72", "#e8b394", "#f6d5b8"]
EYE = "#2a2430"
BLUSH = "#f0a0a0"

CHARS = {
    # 이름: 머리색 3단, 옷 3단, 장식
    "warrior": {"hair": ["#4a2f22", "#6b4a32", "#8c6644"], "cloth": ["#7a2e2e", "#b84a3e", "#e0705a"], "acc": "band",
                "acc_col": "#e8d4a0", "boots": ["#3a2a22", "#5a4030"]},
    "bomber": {"hair": ["#9a4a1e", "#d9893a", "#f0b45a"], "cloth": ["#2f4a40", "#4a7058", "#6a9a74"], "acc": "goggles",
               "acc_col": "#7ac0d8", "boots": ["#3a2a22", "#5a4030"]},
    "healer": {"hair": ["#c07a9a", "#eaa8c0", "#fbd4e2"], "cloth": ["#9aa0b0", "#e8ecf0", "#ffffff"], "acc": "cross",
               "acc_col": "#5aaa6a", "boots": ["#5a5060", "#7a7088"]},
    "rogue": {"hair": ["#2a3048", "#3e4a6e", "#5a6a98"], "cloth": ["#283040", "#3a4660", "#566684"], "acc": "hood",
              "acc_col": "#c8a050", "boots": ["#1e1e28", "#34343e"]},
}


def put(a, x, y, col):
    if 0 <= x < a.shape[1] and 0 <= y < a.shape[0]:
        a[y, x] = hx(col) if isinstance(col, str) else col


def rect(a, x0, y0, x1, y1, col):
    """x1, y1 포함"""
    a[max(0, y0):y1 + 1, max(0, x0):x1 + 1] = hx(col)


def shade_cols(a, m, cols, xs_light, xs_dark):
    """가운데 색, 왼쪽(빛) 밝게, 오른쪽 어둡게"""
    xs, ys = grid(a.shape[1], a.shape[0])
    paint(a, m, cols[1])
    paint(a, m & (xs <= xs_light), cols[2])
    paint(a, m & (xs >= xs_dark), cols[0])


# ------------------------------------------------------------------ 부위
def head(a, d, ox, oy, c, blink=False):
    """머리 (얼굴 + 머리카락 + 장식). ox, oy: 머리 가운데 기준 이동"""
    cx, cy = 12 + ox, 12.5 + oy
    rx, ry = 7.6, 7.0
    xs, ys = grid(FW, FH)
    skull = ell(FW, FH, cx, cy, rx, ry)
    # 얼굴 영역
    if d == "s":
        face = ell(FW, FH, cx, cy + 2.4, rx - 1.3, ry - 2.0)
        jag = np.array([0, 1, 0, 2, 1, 0, 1, 2, 0, 1, 0, 1, 2, 0, 1, 0, 1, 0, 2, 1, 0, 1, 0, 0])
        face &= ys > (cy - 1.6 + jag[np.clip(xs.astype(int), 0, 23)] * 0.9)
    elif d == "e":
        face = ell(FW, FH, cx + 2.2, cy + 2.2, rx - 2.2, ry - 2.2) & (xs > cx - 1.0)
        jag = np.array([0, 1, 0, 2, 1, 0, 1, 0, 2, 0, 1, 0, 1, 2, 0, 1, 0, 1, 0, 2, 1, 0, 1, 0])
        face &= ys > (cy - 1.2 + jag[np.clip(xs.astype(int), 0, 23)] * 0.8)
    else:
        face = np.zeros_like(skull)
    face &= skull
    hair = skull & ~face
    # 옆머리가 얼굴 옆으로 조금 흘러내림
    if d == "s":
        hair |= (ell(FW, FH, cx - rx + 1.6, cy + 3.0, 1.6, 3.2) | ell(FW, FH, cx + rx - 1.6, cy + 3.0, 1.6, 3.2)) & ~face
    elif d == "e":
        hair |= ell(FW, FH, cx - rx + 2.0, cy + 3.2, 2.2, 3.4)
    elif d == "n":
        hair |= ell(FW, FH, cx, cy + 3.5, rx - 0.6, 4.0)
    # 칠하기: 머리카락 (윗머리 왼쪽 밝게)
    lam = -((xs - cx) / rx) * 0.5 - ((ys - cy) / ry) * 0.7
    t = 0.45 + lam * 0.45
    ramp(a, hair, c["hair"], t, 0.4)
    # 머리카락 빛 반사 한 줄 (둥근 머리 느낌)
    shine = hair & (np.abs(ys - (cy - ry * 0.55) - (xs - cx) * 0.25) < 0.6) & (xs < cx + 2) & (xs > cx - 5)
    paint(a, shine, c["hair"][2])
    # 얼굴
    ft = 0.62 - ((xs - cx) / rx) * 0.5
    ramp(a, face, SKIN, ft, 0.0)
    # 눈·볼
    ey = int(cy + 1.5)
    if d == "s":
        for ex in (int(cx - 4), int(cx + 2)):
            if blink:
                rect(a, ex, ey + 1, ex + 1, ey + 1, EYE)
            else:
                rect(a, ex, ey, ex + 1, ey + 2, EYE)
                put(a, ex, ey, "#ffffff")
        put(a, int(cx - 5), ey + 3, BLUSH)
        put(a, int(cx + 4), ey + 3, BLUSH)
        put(a, int(cx), ey + 3, SKIN[0])
    elif d == "e":
        ex = int(cx + 3)
        if blink:
            rect(a, ex, ey + 1, ex + 1, ey + 1, EYE)
        else:
            rect(a, ex, ey, ex + 1, ey + 2, EYE)
            put(a, ex + 1, ey, "#ffffff")
        put(a, ex - 1, ey + 3, BLUSH)
    # 장식
    acc, col = c["acc"], c["acc_col"]
    if acc == "band":
        band = skull & (np.abs(ys - (cy - 2.6)) < 0.7) & (hair if d != "n" else skull)
        paint(a, band, col)
        if d == "n":
            put(a, int(cx), int(cy - 1), col)
            put(a, int(cx + 1), int(cy), col)
            put(a, int(cx - 1), int(cy + 1), col)
    elif acc == "goggles":
        gy = int(cy - 3.5)
        if d == "s":
            rect(a, int(cx - 6), gy, int(cx + 5), gy, "#5a4030")
            for gx in (int(cx - 4), int(cx + 1)):
                rect(a, gx, gy - 1, gx + 2, gy + 1, OUTL)
                rect(a, gx + 1, gy - 1, gx + 1, gy, col)
                put(a, gx + 2, gy, "#d8f4ff")
        elif d == "e":
            rect(a, int(cx - 6), gy, int(cx + 6), gy, "#5a4030")
            rect(a, int(cx + 2), gy - 1, int(cx + 4), gy + 1, OUTL)
            rect(a, int(cx + 3), gy - 1, int(cx + 3), gy, col)
        else:
            rect(a, int(cx - 7), gy, int(cx + 6), gy, "#5a4030")
    elif acc == "cross":
        if d in ("s", "e"):
            hx_ = int(cx - 4) if d == "s" else int(cx - 3)
            hy = int(cy - 4)
            rect(a, hx_ - 1, hy, hx_ + 1, hy, col)
            rect(a, hx_, hy - 1, hx_, hy + 1, col)
    elif acc == "hood":
        hood = ell(FW, FH, cx, cy - 0.5, rx + 0.9, ry + 0.8) & ~face
        hood &= ys < cy + 5
        if d == "e":
            hood &= ~(face | ell(FW, FH, cx + 3, cy + 2, 4.5, 5))
        ramp(a, hood & (a[..., 3] > 0) | hood, c["cloth"], 0.45 + lam * 0.45, 0.4)
        if d == "n":
            put(a, int(cx), int(cy + 6), col)
    return skull | hair


def body(a, d, oy, c, lean=0):
    """몸통 + 허리띠"""
    xs, ys = grid(FW, FH)
    y0, y1 = 20 + oy, 27 + oy
    if d == "e":
        x0, x1 = 8 + lean, 15 + lean
    else:
        x0, x1 = 7, 16
    m = (xs >= x0) & (xs <= x1) & (ys >= y0) & (ys <= y1)
    # 둥근 모서리
    m &= ~(((xs == x0) | (xs == x1)) & ((ys == y0) | (ys == y1)))
    shade_cols(a, m, c["cloth"], x0 + 1, x1 - 1)
    # 허리띠
    belt = m & (ys == y1 - 2)
    paint(a, belt, "#4a3428")
    if d != "n":
        put(a, (x0 + x1) // 2 + (1 if d == "e" else 0), y1 - 2, "#d8b860")
    # 옷깃 (앞)
    if d == "s":
        put(a, 11, y0, c["cloth"][2])
        put(a, 12, y0, c["cloth"][2])
        if c["acc"] == "cross":
            rect(a, 11, y0 + 2, 12, y0 + 2, c["acc_col"])
            rect(a, 11, y0 + 1, 11, y0 + 3, c["acc_col"])
            put(a, 12, y0 + 1, c["cloth"][1])
            put(a, 12, y0 + 3, c["cloth"][1])
    return m


def arm(a, x, y, c, length=5, hand=True):
    rect(a, x, y, x + 1, y + length - 2, c["cloth"][0])
    put(a, x, y, c["cloth"][1])
    if hand:
        rect(a, x, y + length - 1, x + 1, y + length - 1, SKIN[1])


# 6프레임 걷기: 다리 단계 (옆: 앞뒤 벌림 · 앞뒤: 들린 발)
WALK_SIDE = [(0, 0), (-1, 1), (-2, 2), (0, 0), (1, -1), (2, -2)]
WALK_LIFT = [(0, 0), (1, 0), (1, 0), (0, 0), (0, 1), (0, 1)]
WALK_BOB = [0, -1, 0, 0, -1, 0]
WALK_ARM = [(0, 0), (1, -1), (1, -1), (0, 0), (-1, 1), (-1, 1)]


def legs(a, d, phase, oy, c, spread=0):
    """phase: 0~5 (6프레임 걷기) — 옆모습은 앞뒤로 벌리고, 앞뒤 모습은 발을 번갈아 든다"""
    b = c["boots"]
    if d == "e":
        f = WALK_SIDE[phase]
        if spread:
            f = (-spread, spread)
        # 뒷발(어둡게) 먼저
        for k, (dx, col) in enumerate(((f[0], b[0]), (f[1], b[1]))):
            x = 10 + dx + k
            lift = 1 if (phase in (1, 2, 4, 5) and dx < 0) else 0
            rect(a, x, 28 + oy - lift, x + 2, 30 + oy - lift, col)
            put(a, x + 3, 30 + oy - lift, col)
    else:
        lift_l, lift_r = WALK_LIFT[phase]
        rect(a, 8 - spread, 28 - lift_l + oy, 10 - spread, 30 - lift_l + oy, b[1])
        rect(a, 13 + spread, 28 - lift_r + oy, 15 + spread, 30 - lift_r + oy, b[0] if d == "s" else b[1])
        # 발바닥 줄
        rect(a, 8 - spread, 31 - lift_l, 10 - spread, 31 - lift_l, b[0])
        rect(a, 13 + spread, 31 - lift_r, 15 + spread, 31 - lift_r, b[0])


def finish(a):
    outline(a, OUTL)
    return a


# ------------------------------------------------------------------ 동작
def frame(c, d, anim, i):
    a = canvas(FW, FH)
    bob = 0
    phase = 0
    head_dy = 0
    lean = 0
    arm_dy = (0, 0)
    blink = False
    spread = 0
    smear = False
    if anim == "idle":
        head_dy = [0, 0, 1, 1][i]            # 숨쉬기 4프레임
        blink = i == 3
    elif anim in ("walk", "run"):
        phase = i
        bob = WALK_BOB[i]
        arm_dy = WALK_ARM[i]
        if anim == "run":
            lean = 1
            arm_dy = (arm_dy[0] * 2, arm_dy[1] * 2)
    elif anim == "jump":
        bob = -1
        spread = 1
        arm_dy = (-3, -3)
    elif anim == "fall":
        spread = 1
        arm_dy = (-1, -1)
    elif anim == "attack":
        # 5프레임: 예비동작 · 스미어(잔상) · 타격 · 따라감 · 회복
        lean = [-1, 1, 2, 2, 1][i]
        arm_dy = [(-3, 0), (0, 0), (2, 0), (2, 0), (1, 0)][i]
        smear = i == 1
    elif anim == "hurt":
        head_dy = 1
        blink = True
        lean = -1
    if anim == "roll":
        return finish(roll(c, d, i))
    # 다리 → 몸 → 팔 → 머리 (뒤에서 앞으로)
    if d == "e":
        legs(a, d, phase, 0, c, spread)
        # 뒤 팔
        arm(a, 10 + lean - arm_dy[1] // 2, 21 + bob, {**c, "cloth": [c["cloth"][0]] * 3}, 5)
        body(a, d, bob, c, lean)
        head(a, d, lean, bob + head_dy, c, blink)
        # 앞 팔: 공격이면 앞으로 뻗음 (스미어 프레임은 휘두른 궤적을 옅게 남긴다)
        if smear:
            for k2 in range(7):
                ang = -1.2 + k2 * 0.4
                x = int(14 + lean + math.cos(ang) * 6)
                y = int(22 + bob + math.sin(ang) * 6)
                rect(a, x, y, x + 1, y, "#f4ecd8" if k2 > 2 else "#c8c0b0")
            rect(a, 17 + lean, 21 + bob, 18 + lean, 22 + bob, SKIN[2])
        elif anim == "attack" and i >= 2:
            rect(a, 13 + lean, 22 + bob, 17 + lean, 23 + bob, c["cloth"][1])
            rect(a, 18 + lean, 22 + bob, 18 + lean, 23 + bob, SKIN[1])
        else:
            arm(a, 12 + lean + arm_dy[0] // 2, 21 + bob + min(0, arm_dy[0]), c, 5)
    else:
        legs(a, d, phase, 0, c, spread)
        body(a, d, bob, c)
        ly = 21 + bob + arm_dy[0]
        ry = 21 + bob + arm_dy[1]
        arm(a, 5, ly, c, 5 if anim != "jump" else 4)
        arm(a, 17, ry, c, 5 if anim != "jump" else 4)
        head(a, d, 0, bob + head_dy, c, blink)
    return finish(a)


def roll(c, d, i):
    """구르기: 몸을 공처럼 말아 굴린다 (머리카락 덮개가 한 바퀴)"""
    a = canvas(FW, FH)
    xs, ys = grid(FW, FH)
    cx, cy, r = 12, 24.5, 6.6
    ball = ell(FW, FH, cx, cy, r, r)
    ang = np.arctan2(ys - cy, xs - cx)
    rot = i * (math.pi / 3) * (1 if d != "n" else -1)
    cap = np.cos(ang - (-math.pi / 2 + rot)) > 0.15
    lam = -((xs - cx) / r) * 0.5 - ((ys - cy) / r) * 0.6
    ramp(a, ball & ~cap, c["cloth"], 0.45 + lam * 0.45, 0.4)
    ramp(a, ball & cap, c["hair"], 0.45 + lam * 0.45, 0.4)
    # 신발 한 짝 보이기
    bx = int(cx + math.cos(rot + math.pi / 2) * (r - 2))
    by = int(cy + math.sin(rot + math.pi / 2) * (r - 2))
    rect(a, bx - 1, by - 1, bx + 1, by, c["boots"][1])
    return a


ANIMS = {"idle": 4, "walk": 6, "run": 6, "roll": 6, "jump": 1, "fall": 1, "attack": 5, "hurt": 1}


def main():
    items = []
    sheet = []
    for name, c in CHARS.items():
        row = []
        for d in ("s", "n", "e"):
            for anim, n in ANIMS.items():
                for i in range(n):
                    f = frame(c, d, anim, i)
                    items.append((f"{name}_{anim}_{d}{i}", f))
                    row.append(f)
        sheet.append(row)
    # 아틀라스: 모든 프레임 같은 크기 → 자르지 않고 그대로 (발 위치 고정)
    at, pos = pack(items, os.path.join(OUT, "sprites", "chars"), 1024, keep_bottom=False, trim_cells=False)
    # 미리보기: 캐릭터마다 한 줄
    pv = canvas(FW * len(sheet[0]) + len(sheet[0]), (FH + 2) * len(sheet))
    for r, row in enumerate(sheet):
        for k, f in enumerate(row):
            over(pv, f, k * (FW + 1), r * (FH + 2))
    preview(pv, "chars_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
