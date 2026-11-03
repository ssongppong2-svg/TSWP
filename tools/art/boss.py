"""보스 01 부화의 여왕 (거미 여왕) + 고치 · 거미줄 장판 · 돌기둥 발판 · 레버 · 왕관
옆모습 (왼쪽은 뒤집어서). 동작: idle 2 · walk 4 · ready 1(예고) · slam 1 · charge 2 · hurt 1 · shell(껍질 굳음) 1
→ assets/sprites/boss.png + boss.json
"""
from px import *
from pack import pack

OUTL = "#1a1220"
CARA = ["#1e1428", "#2e1e3e", "#46305a", "#644680", "#8a66a8", "#b494cc"]
SAC = ["#5a2a2a", "#8a3a3a", "#c05a48", "#e88a5a", "#ffc080"]
SHELL = ["#3a3a48", "#5a5a6a", "#80808e", "#aaaab6", "#d4d4dc"]
GOLD = ["#7a5a1e", "#c8962e", "#f0c850", "#fff0a0"]
EYE = "#ff5a3a"


def queen(anim, i, shell=False):
    """v2: 셀 음영 + 색 외곽선. 뒤 배(알주머니 빛) · 가슴 · 머리(왕관 · 눈 6 · 송곳니) · 마디 다리 8"""
    from shade import blob, bands, sphere_t, selout, line_mask, poly_mask, put, fill
    from px import dilate
    W, H = 124, 76
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cols = SHELL if shell else CARA
    bob = {"idle": [0, 0, 1, 1], "walk": [0, -1, -2, 0, -1, -2], "ready": [2, 3], "slam": [-5, 1], "charge": [-1, 0], "hurt": [1, 2]}[anim][i]
    lean = {"ready": [-4, -6], "slam": [7, 8], "charge": [6, 8], "hurt": [-3, -4]}.get(anim, [0] * 6)[i]
    cx, cy = 62 + lean, 38 + bob
    floor = H - 2
    raise_front = anim in ("ready", "slam")
    def legs(side):
        for k in range(4):
            spread = k - 1.5
            ph = (k + (0 if side > 0 else 1)) % 2
            gait = [0, 1, 2, 1, 0, -1][(i + ph * 3) % 6] if anim == "walk" else 0
            root = (cx - 2 + spread * 5, cy - 2)
            knee = (cx - 2 + spread * 19 + side * 2 + gait * 1.5, cy - 31 + abs(spread) * 4 - (2 if gait > 0 else 0) - (1 if anim == "idle" and i >= 2 else 0) + (3 if side < 0 else 0))
            foot = (cx - 2 + spread * 33 + side * 3 + gait * 2.5, floor - (3 if gait > 1 else 0) - (2 if side < 0 else 0))
            if raise_front and k >= 2:
                lift = 0 if anim == "ready" else 1
                knee = (cx + 12 + k * 6, cy - 30 + lift * 10 - i * 2)
                foot = (cx + 24 + k * 6, cy - 16 + lift * 28)
            dark = side < 0
            c_main = cols[1] if dark else cols[2]
            c_lite = cols[2] if dark else cols[3]
            th = 3.0 if dark else 3.6
            mid = (knee[0] + (foot[0] - knee[0]) * 0.45 + spread * 1.2, knee[1] + (foot[1] - knee[1]) * 0.45)
            if not dark:                                         # 앞다리는 몸 위에 그리므로 어두운 테를 먼저 (몸과 섞이지 않게)
                lm = line_mask(W, H, root, knee, th) | line_mask(W, H, knee, mid, th * 0.75) | line_mask(W, H, mid, foot, th * 0.5)
                fill(a, dilate(lm) & ~lm, OUTL)
            fill(a, line_mask(W, H, root, knee, th), c_main)
            fill(a, line_mask(W, H, (root[0] - 0.8, root[1] - 1), (knee[0] - 0.8, knee[1] - 1), 1.0), c_lite)
            # 정강이: 무릎에서 살짝 바깥으로 휘었다 발끝으로 (가늘어짐)
            fill(a, line_mask(W, H, knee, mid, th * 0.75), c_main)
            fill(a, line_mask(W, H, mid, foot, th * 0.5), c_main)
            fill(a, line_mask(W, H, (knee[0] - 0.6, knee[1]), (mid[0] - 0.6, mid[1]), 0.6), c_lite)
            # 무릎 가시 + 마디 · 발톱
            fill(a, poly_mask(W, H, [(knee[0] - 1.6, knee[1] - 1), (knee[0] + 1.6, knee[1] - 1), (knee[0] + 0.8, knee[1] - 5)]), cols[0] if dark else cols[2])
            fill(a, ell(W, H, knee[0], knee[1], 2.3, 2.3), cols[2] if dark else cols[4])
            put(a, knee[0] - 1, knee[1] - 1, cols[3] if dark else cols[-1])
            put(a, foot[0], foot[1], "#e8dcc8" if not dark else cols[2])

    legs(-1)                                                     # 뒤쪽 다리 (몸 뒤)
    # ---- 배 (뒤, 크게): 마디 줄 + 알주머니
    ab = (cx - 21, cy - 5)
    pulse = (i % 2) if anim == "idle" else 0
    abd = blob(a, ab[0], ab[1], 20 + pulse * 0.4, 16 + pulse * 0.3, cols[1:6], cut=[0.22, 0.45, 0.68, 0.88])
    for k in range(5):                                           # 마디 띠 (아래쪽 어두운 곡선)
        xk = ab[0] - 14 + k * 7
        seg = abd & (np.abs(xs + 0.5 - xk - (ys - ab[1]) * 0.15) < 0.6) & (ys > ab[1] - 10)
        fill(a, seg, cols[1])
    if not shell:
        # 등의 무늬: 빛나는 알주머니 (속이 비치는 막 + 안의 알들)
        sac = ell(W, H, ab[0] - 1, ab[1] + 3, 11, 8.5) & abd
        glow = 0.12 * pulse + (0.25 if anim in ("ready", "slam") else 0.0)
        bands(a, sac, SAC[1:], np.clip(sphere_t(W, H, ab[0] - 3, ab[1] + 1, 11, 8.5, amb=0.45) + glow, 0, 1), cut=[0.35, 0.6, 0.85])
        fill(a, sac & ~nb(sac, 0, -1), SAC[3])                  # 윗가장자리 빛
        for (dx, dy, r) in ((-6, 0, 2.0), (-1, 3, 2.3), (4, 0, 1.9), (-3, -3, 1.7), (2, -4, 1.5), (-7, 5, 1.6), (5, 5, 1.7)):
            e = ell(W, H, ab[0] - 1 + dx, ab[1] + 3 + dy, r, r * 0.9) & sac
            fill(a, e, "#ffe0a8")
            fill(a, e & ~nb(e, 0, 1), SAC[3])
            put(a, ab[0] - 1 + dx - 0.6, ab[1] + 2.2 + dy, "#ffffff")
    else:
        for k in range(4):                                       # 굳은 껍질 판
            plate = ell(W, H, ab[0] - 10 + k * 7, ab[1] - 4 + (k % 2) * 3, 6, 7) & abd
            bands(a, plate, SHELL[1:], sphere_t(W, H, ab[0] - 12 + k * 7, ab[1] - 6, 6, 7), cut=[0.3, 0.6, 0.85])
            fill(a, plate & ~nb(plate, 0, 1), SHELL[0])
    # 털 (가장자리 몇 가닥)
    for (dx, dy) in ((-19, -9), (-14, -15), (-6, -16), (-21, 0), (-17, 9)):
        put(a, ab[0] + dx, ab[1] + dy, cols[3])
        put(a, ab[0] + dx - 1, ab[1] + dy - 1, cols[2])
    # ---- 가슴 · 머리
    tho = blob(a, cx + 2, cy - 3, 11, 9, cols[1:6], cut=[0.22, 0.45, 0.68, 0.88])
    hdx, hdy = cx + 17, cy - 5 + (2 if anim == "slam" and i == 0 else 0)
    head = blob(a, hdx, hdy, 10.5, 9.0, cols[1:6], cut=[0.2, 0.42, 0.66, 0.86])
    fill(a, dilate(head) & ~head & (xs > hdx - 6), OUTL)                 # 머리 윤곽 (가슴과 나뉘게)
    legs(1)                                                      # 앞쪽 다리 (몸 앞)
    # 더듬이 다리(촉지) 둘
    for k, dy in enumerate((4, 6)):
        p0 = (hdx + 5, hdy + dy - 2)
        p1 = (hdx + 10 + k * 2, hdy + dy + 3 - (3 if anim in ("ready", "charge") else 0))
        fill(a, line_mask(W, H, p0, p1, 1.6), cols[2])
        put(a, p1[0], p1[1], cols[4])
    # 왕관 (가시 셋 + 보석)
    for k, h in enumerate((7, 11, 7)):
        bx = hdx - 4 + k * 4
        spike = poly_mask(W, H, [(bx - 2, hdy - 8), (bx + 2, hdy - 8), (bx + 0.3, hdy - 8 - h)])
        fill(a, spike, GOLD[2])
        fill(a, spike & (xs + 0.5 < bx), GOLD[3])
        put(a, bx, hdy - 8 - h + 1, "#ffffff")
    band_ = (np.abs(ys + 0.5 - (hdy - 8)) < 1.2) & (np.abs(xs + 0.5 - hdx) < 7)
    fill(a, band_, GOLD[1])
    fill(a, ell(W, H, hdx, hdy - 8, 1.5, 1.3), "#e04a6a")
    put(a, hdx - 0.5, hdy - 8.5, "#ffb0c0")
    # 눈 6 (큰 둘 + 작은 넷) — 맞으면 질끈
    eye_c = EYE if not shell else "#c86a4a"
    if anim == "hurt":
        for (dx, dy) in ((3, -2), (6, -1)):
            for k in range(3):
                put(a, hdx + dx + k - 1, hdy + dy + (1 if k == 1 else 0), "#1a1220")
    else:
        for (dx, dy, r) in ((3.5, -1.5, 2.0), (8.0, -0.5, 2.0)):
            fill(a, ell(W, H, hdx + dx, hdy + dy, r + 0.7, r * 1.1 + 0.7), "#1a1220")
            fill(a, ell(W, H, hdx + dx, hdy + dy, r, r * 1.1), eye_c)
            put(a, hdx + dx - 0.7, hdy + dy - 1.0, "#ffffff")
            put(a, hdx + dx + 0.6, hdy + dy + 0.8, "#ffc0a0")
        for (dx, dy) in ((1, -4.5), (5, -5), (9, -4), (10.5, 2)):
            put(a, hdx + dx, hdy + dy, eye_c)
    # 송곳니 (협각) — 공격 때 벌어짐
    gap = 3 if anim in ("slam", "charge", "ready") else 1
    for k, dx in enumerate((3, 7)):
        p0 = (hdx + dx, hdy + 4)
        p1 = (hdx + dx + (k * 2 - 1) * gap * 0.6, hdy + 9 + gap)
        fill(a, line_mask(W, H, p0, p1, 1.8), "#2a1a24")
        fill(a, line_mask(W, H, (p0[0], p0[1] + 2), p1, 0.8), "#f4ecdc")
    if anim == "slam" and i == 0:                                  # 내려찍기 잔상
        for k in range(3):
            fill(a, line_mask(W, H, (cx + 34 + k * 4, 6 + k * 3), (cx + 34 + k * 4, 20 + k * 3), 0.6), "#fff4d6")
    if anim == "charge":
        for k in range(4):
            yy = cy - 12 + k * 6
            fill(a, line_mask(W, H, (4 + k * 3, yy), (16 + k * 3, yy), 0.6), "#e8dcff")
    selout(a, OUTL, k=0.35, mix_ink=0.6)
    return a


def cocoon(i):
    W, H = 18, 22
    a = canvas(W, H)
    xs, ys = grid(W, H)
    pulse = [0, 1, 0, 2][i]
    m = ell(W, H, 9, 12, 6 + pulse * 0.3, 8.5 + pulse * 0.3)
    ramp(a, m, ["#8a8494", "#b8b2c0", "#dcd8e4", "#f4f2f8"], 0.6 - (xs - 9) / 12 - (ys - 12) / 16, 0.4)
    # 감긴 실
    for k in range(5):
        y = 5 + k * 3 + (k % 2)
        paint(a, m & (np.abs(ys - y - (xs - 9) * 0.3) < 0.6), "#9a94a4")
    # 안에서 꿈틀 (빛)
    if i in (1, 3):
        paint(a, ell(W, H, 9, 13, 2, 3), "#c06a8a")
    a[0:5, 9] = hx("#dcd8e4")
    outline(a, OUTL)
    return a


def web_field(S=64):
    a = canvas(S, S)
    xs, ys = grid(S, S)
    c = S / 2
    d = np.sqrt((xs + 0.5 - c) ** 2 + (ys + 0.5 - c) ** 2)
    ang = np.arctan2(ys - c, xs - c)
    spokes = (np.abs(((ang / (math.pi * 2) * 12) % 1) - 0.5) > 0.46) & (d < c - 1)
    rings = (np.abs((d % 6) - 3) > 2.4) & (d < c - 1) & (d > 2)
    paint(a, spokes | rings, "#e8e6f0")
    paint(a, (spokes | rings) & ((xs.astype(int) + ys.astype(int)) % 5 == 0), "#b8b4c8")
    return a


def pillar(W=22, H=44):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    body = (xs >= 2) & (xs <= W - 3) & (ys >= 8)
    ramp(a, body, STONE[1:6], 0.75 - (xs - 2) / (W - 4) * 0.6 + (vnoise(W, H, 3, 4) - 0.5) * 0.2, 0.4)
    for y in range(14, H, 9):
        paint(a, body & (np.abs(ys - y) < 0.6), STONE[1])
    top = ell(W, H, W / 2 - 0.5, 8, W / 2 - 2, 4.5)
    ramp(a, top, STONE[3:7], 0.8 - (xs - W / 2) / W, 0.3)
    paint(a, top & ~nb(top, 0, 1), STONE[2])
    # 이끼
    paint(a, body & (ys < 13) & (vnoise(W, H, 2, 9) > 0.55), GRASS[4])
    outline(a, INK)
    return a


def lever(on):
    W, H = 18, 20
    a = canvas(W, H)
    xs, ys = grid(W, H)
    base = (xs >= 3) & (xs <= 14) & (ys >= 14) & (ys <= 18)
    ramp(a, base, STONE[1:5], 0.7 - (xs - 3) / 14, 0.3)
    # 손잡이
    x0, y0 = 9, 15
    x1, y1 = (14, 4) if on else (4, 4)
    for s in range(13):
        u = s / 12
        x, y = int(x0 + (x1 - x0) * u), int(y0 + (y1 - y0) * u)
        a[y, x] = hx("#7a5434")
        a[y, x + 1] = hx("#5a3c26")
    paint(a, ell(W, H, x1 + 0.5, y1, 2.2, 2.2), "#e05a4a" if not on else "#6ad07a")
    outline(a, INK)
    return a


def crown():
    W, H = 16, 12
    a = canvas(W, H)
    xs, ys = grid(W, H)
    band = (ys >= 7) & (ys <= 10) & (xs >= 2) & (xs <= 13)
    paint(a, band, GOLD[2])
    for k, x in enumerate((3, 7.5, 12)):
        tip = (np.abs(xs - x) < (ys - 1) * 0.5) & (ys >= 2) & (ys < 8)
        paint(a, tip, GOLD[2])
        a[1 if k == 1 else 2, int(x)] = hx(GOLD[3])
    paint(a, band & (ys == 7), GOLD[3])
    paint(a, band & (ys == 10), GOLD[1])
    for x in (5, 10):
        a[8:10, x] = hx("#e04a6a")
    outline(a, OUTL)
    return a


ANIMS = {"idle": 4, "walk": 6, "ready": 2, "slam": 2, "charge": 2, "hurt": 2}


def main():
    items = []
    for anim, n in ANIMS.items():
        for i in range(n):
            items.append((f"queen_{anim}{i}", queen(anim, i)))
            items.append((f"queenS_{anim}{i}", queen(anim, i, shell=True)))
    for i in range(4):
        items.append((f"cocoon{i}", cocoon(i)))
    items.append(("web_field", web_field()))
    items.append(("pillar", pillar()))
    items.append(("lever_off", lever(False)))
    items.append(("lever_on", lever(True)))
    items.append(("crown", crown()))
    at, pos = pack(items, os.path.join(OUT, "sprites", "boss"), 1024, keep_bottom=False, trim_cells=False)
    pv = canvas(128 * 6, 160)
    for k, n in enumerate(["queen_idle0", "queen_walk1", "queen_ready0", "queen_slam0", "queen_charge0", "queenS_idle0"]):
        over(pv, dict(items)[n], k * 128, 0)
    for k, n in enumerate(["cocoon0", "cocoon1", "web_field", "pillar", "lever_off", "lever_on", "crown"]):
        over(pv, dict(items)[n], k * 70, 82)
    preview(pv, "boss_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
