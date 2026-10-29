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
    W, H = 112, 64
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cols = SHELL if shell else CARA
    bob = {"idle": [0, 1], "walk": [0, -1, 0, -1], "ready": [2], "slam": [-4], "charge": [-1, 0], "hurt": [1], "shell": [2]}[anim][i]
    lean = {"ready": [-4], "slam": [6], "charge": [6, 7], "hurt": [-3]}.get(anim, [0] * 4)[i]
    cx, cy = 56 + lean, 34 + bob
    # 다리 8개: 몸 가운데에서 바깥으로 꺾여 땅으로
    ph = i if anim == "walk" else 0
    raise_front = anim in ("ready", "slam")
    for k in range(4):
        for side in (-1, 1):                      # -1 뒤(어둡게) · 1 앞
            base_x = cx - 10 + k * 8
            lift = ((k + ph) % 2) * 3 if anim == "walk" else 0
            spread = (k - 1.5)
            knee = (cx + spread * 15 + side * 3 - 4, cy - 17 - lift - abs(spread) * 1.5)
            foot = (cx + spread * 25 + side * 4 - 4 + (lift * (1 if spread > 0 else -1)), H - 2 - lift)
            if raise_front and k >= 2:
                knee = (base_x + 8, cy - 22 + (6 if anim == "slam" else 0))
                foot = (base_x + 14, cy - 8 + (16 if anim == "slam" else 0))
            col = cols[1] if side < 0 else cols[3]
            for (p0, p1, th) in (((cx + (k - 1.5) * 4, cy - 2), knee, 1.7), (knee, foot, 1.25)):
                n = 24
                for s in range(n + 1):
                    u = s / n
                    x = p0[0] + (p1[0] - p0[0]) * u
                    y = p0[1] + (p1[1] - p0[1]) * u
                    m = ell(W, H, x, y, th, th)
                    paint(a, m, col)
            # 다리 마디 장식
            paint(a, ell(W, H, knee[0], knee[1], 1.9, 1.9), cols[4] if side > 0 else cols[2])
    # 알주머니 배 (뒤)
    abd = ell(W, H, cx - 18, cy - 4, 17, 14)
    lam = -((xs - cx) / 20) * 0.4 - ((ys - cy) / 14) * 0.6
    ramp(a, abd, cols, 0.45 + lam * 0.45, 0.4)
    # 배 무늬: 빛나는 알 주머니 (껍질이 굳으면 가려짐)
    if not shell:
        sac = ell(W, H, cx - 20, cy - 2, 9, 7)
        ramp(a, sac, SAC, 0.6 - (ys - cy) / 12 - (xs - cx + 20) / 30, 0.4)
        for (dx, dy) in ((-4, -2), (2, 1), (-1, 3), (4, -3)):
            paint(a, ell(W, H, cx - 20 + dx, cy - 2 + dy, 1.6, 1.4), SAC[4])
    else:
        for k in range(5):
            paint(a, abd & (np.abs(xs - (cx - 30 + k * 6)) < 0.7), SHELL[0])
    # 가슴 + 머리
    tho = ell(W, H, cx + 4, cy - 2, 11, 9)
    ramp(a, tho, cols, 0.5 + lam * 0.45, 0.4)
    head = ell(W, H, cx + 16, cy - 4, 8, 7)
    ramp(a, head, cols, 0.55 + lam * 0.45, 0.4)
    # 왕관 (가시 셋)
    for k, h in enumerate((7, 10, 7)):
        bx = int(cx + 12 + k * 4)
        for t in range(h):
            w_ = max(0, 2 - t // 3)
            a[int(cy - 10 - t), bx - w_:bx + w_ + 1] = hx(GOLD[2] if t < h - 2 else GOLD[3])
        a[int(cy - 10), bx - 2:bx + 3] = hx(GOLD[1])
    # 눈 (넷, 빨갛게 빛남)
    for (dx, dy) in ((5, -5), (7, -4), (4, -2), (6, -1)):
        a[int(cy + dy - 1), int(cx + 16 + dx)] = hx(EYE)
    # 송곳니
    fang = 3 if anim in ("slam", "charge") else 1
    for dx in (2, 5):
        a[int(cy + 2):int(cy + 3 + fang), int(cx + 18 + dx)] = hx("#f0e8d8")
    outline(a, OUTL)
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


ANIMS = {"idle": 2, "walk": 4, "ready": 1, "slam": 1, "charge": 2, "hurt": 1}


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
    pv = canvas(116 * 6, 140)
    for k, n in enumerate(["queen_idle0", "queen_walk1", "queen_ready0", "queen_slam0", "queen_charge0", "queenS_idle0"]):
        over(pv, dict(items)[n], k * 116, 0)
    for k, n in enumerate(["cocoon0", "cocoon1", "web_field", "pillar", "lever_off", "lever_on", "crown"]):
        over(pv, dict(items)[n], k * 70, 70)
    preview(pv, "boss_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
