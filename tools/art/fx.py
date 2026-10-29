"""효과 그림: 베기 궤적 · 흙먼지 · 맞음 불꽃 · 조준점
→ assets/sprites/fx.png + fx.json
"""
from px import *
from pack import pack

SLASH = ["#fff8e0", "#ffe9a8", "#f0c070", "#c88a4a"]
DUST = ["#c8b890", "#e0d4b0", "#f4ecd4"]
SPARK = ["#ffffff", "#fff0a0", "#ffb040"]


def slash(i, W=40, H=40):
    """오른쪽을 향한 초승달 궤적 (땅에 눕혀 조준 방향으로 돌린다). i: 0 시작 … 3 사라짐"""
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx, cy = W * 0.3, H / 2
    r_out = 17.0
    thick = [5.5, 6.5, 4.0, 2.0][i]
    sweep = [0.55, 1.0, 1.0, 1.0][i]
    ang = np.arctan2(ys + 0.5 - cy, xs + 0.5 - cx)
    rr = np.sqrt((xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2)
    lo = -1.25 * sweep
    hi = 1.25 * sweep if i < 2 else 1.25
    if i >= 2:
        lo = -1.25 + (i - 1) * 0.6
    within = (ang > lo) & (ang < hi)
    # 가운데가 가장 두껍고 끝으로 갈수록 가늘게
    u = np.clip(1 - np.abs(ang) / 1.25, 0, 1)
    band = (rr < r_out) & (rr > r_out - thick * (0.35 + 0.65 * u))
    m = within & band
    t = (rr - (r_out - thick)) / max(thick, 0.1)
    ramp(a, m, SLASH[::-1], np.clip(t + 0.15 - i * 0.12, 0, 1), 0.3)
    return a


def dust(i, W=14, H=10):
    a = canvas(W, H)
    R = random.Random(3)
    for k in range(4):
        x = W / 2 + R.uniform(-4, 4) * (1 + i * 0.25)
        y = H - 3 - R.uniform(0, 2) - i * 0.8
        r = [2.2, 2.8, 2.4, 1.4][i] * R.uniform(0.8, 1.1)
        m = ell(W, H, x, y, r, r * 0.85)
        ramp(a, m, DUST, 0.7 - (grid(W, H)[1] - y) / (r * 2), 0.3)
    return a


def spark(i, W=16, H=16):
    a = canvas(W, H)
    cx, cy = W // 2, H // 2
    L = [3, 6, 7][i]
    col = SPARK[min(i, 2)]
    for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        for k in range(1, L):
            a[cy + dy * k, cx + dx * k] = hx(col if k < L - 1 else SPARK[2])
    for (dx, dy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
        for k in range(1, L // 2 + 1):
            a[cy + dy * k, cx + dx * k] = hx(SPARK[1])
    if i < 2:
        a[cy - 1:cy + 2, cx - 1:cx + 2] = hx(SPARK[0])
    return a


def cursor():
    """조준점 11x11: 테두리 있는 십자 + 가운데 빈칸"""
    a = canvas(11, 11)
    c = 5
    for k in (2, 3, 4):
        for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            a[c + dy * k, c + dx * k] = hx("#ffffff")
    for (dx, dy) in ((1, 1), (-1, -1), (1, -1), (-1, 1)):
        a[c + dy, c + dx] = hx("#ffffff")
    outline(a, "#2a2430", diag=True)
    return a


BOOM = ["#3a2a2a", "#6a3a2a", "#c84a28", "#f08a30", "#ffc850", "#fff0b0", "#ffffff"]
SMOKE = ["#3a3640", "#5a5662", "#7a7682", "#9a96a0"]


def arrow():
    a = canvas(14, 5)
    a[2, 1:11] = hx("#c8a070")
    a[2, 0:3] = hx("#8a6440")
    for (x, y) in ((0, 0), (1, 1), (0, 4), (1, 3), (2, 1), (2, 3)):
        a[y, x] = hx("#f0ece0")
    for (x, y) in ((11, 1), (11, 2), (11, 3), (12, 2), (13, 2), (12, 1), (12, 3)):
        a[y, x] = hx("#d0d4d8")
    a[2, 13] = hx("#ffffff")
    return a


def syringe():
    a = canvas(12, 5)
    a[1:4, 2:9] = hx("#e8f0f4")
    a[2, 3:8] = hx("#7af08a")
    a[1:4, 1] = hx("#a0a8b0")
    a[0:5, 0] = hx("#a0a8b0")
    a[2, 9:12] = hx("#d0d4d8")
    outline(a, "#2a2430")
    return a


def bomb(big=False):
    S = 14 if big else 9
    a = canvas(S, S + 3)
    r = S / 2 - 0.6
    m = ell(S, S + 3, S / 2, S / 2 + 3, r, r)
    xs, ys = grid(S, S + 3)
    ramp(a, m, ["#16141c", "#2a2834", "#44425a", "#6a6884"], 0.45 - (xs - S / 2) / S - (ys - S / 2 - 3) / S, 0.3)
    a[int(3 + S * 0.25), int(S * 0.3)] = hx("#c8c8e0")
    # 도화선 + 불씨
    cx = int(S / 2)
    a[1:4, cx] = hx("#8a6440")
    a[0, cx] = hx("#ffc850")
    a[0, cx + 1] = hx("#ff7a30")
    if big:
        paint(a, m & (np.abs(ys - (S / 2 + 3)) < 0.8), "#c84a28")
    outline(a, "#2a2430")
    return a


def boom(i, S=48):
    """폭발 6프레임: 번쩍 → 불덩이 → 연기"""
    a = canvas(S, S)
    xs, ys = grid(S, S)
    c = S / 2
    R = random.Random(9)
    if i == 0:
        paint(a, ell(S, S, c, c, 9, 9), BOOM[6])
        paint(a, ell(S, S, c, c, 6, 6), BOOM[5])
        return a
    k = i / 5
    rad = 10 + k * 12
    for b in range(9):
        ang = b / 9 * math.pi * 2 + R.uniform(-0.3, 0.3)
        d = rad * (0.45 + R.uniform(0, 0.3))
        bx, by = c + math.cos(ang) * d, c + math.sin(ang) * d * 0.9 - k * 4
        br = rad * (0.42 - k * 0.12) * R.uniform(0.8, 1.2)
        m = ell(S, S, bx, by, br, br)
        if i <= 2:
            ramp(a, m, BOOM[2:6], 0.75 - (np.sqrt((xs - c) ** 2 + (ys - c) ** 2) / rad) * 0.6 - (ys - by) / (br * 3), 0.5)
        else:
            ramp(a, m, SMOKE, 0.6 - (ys - by) / (br * 2.5) - (i - 3) * 0.15, 0.5)
    core = ell(S, S, c, c - k * 3, rad * (0.55 - k * 0.4), rad * (0.55 - k * 0.4))
    if i <= 3:
        ramp(a, core, BOOM[3:7], 0.85 - (np.sqrt((xs - c) ** 2 + (ys - c) ** 2) / rad), 0.4)
    # 불티
    for e in range(10):
        ang = R.uniform(0, math.pi * 2)
        d = rad * (1.0 + k * 0.6) * R.uniform(0.7, 1.1)
        x, y = int(c + math.cos(ang) * d), int(c + math.sin(ang) * d)
        if 0 <= x < S and 0 <= y < S and i < 5:
            a[y, x] = hx(BOOM[4])
    return a


def ring(i, col, S=32):
    """퍼지는 고리 (회복 파동 · 강타 충격파) — 땅에 눕혀 쓴다"""
    a = canvas(S, S)
    xs, ys = grid(S, S)
    r = 4 + i * 3.6
    d = np.sqrt((xs + 0.5 - S / 2) ** 2 + (ys + 0.5 - S / 2) ** 2)
    m = (d < r) & (d > r - (3 - i * 0.5))
    paint(a, m, col[min(i, len(col) - 1)])
    return a


def heal_plus(i):
    a = canvas(7, 7)
    col = ["#c8ffd0", "#7af08a", "#4ac060"][i]
    a[3, 1:6] = hx(col)
    a[1:6, 3] = hx(col)
    return a


def arrow_mark(i, S=40):
    """화살비 예고 원 (땅에)"""
    a = canvas(S, S)
    xs, ys = grid(S, S)
    d = np.sqrt((xs + 0.5 - S / 2) ** 2 + (ys + 0.5 - S / 2) ** 2)
    m = (d < S / 2 - 1) & (d > S / 2 - 2.2)
    dash = ((np.arctan2(ys - S / 2, xs - S / 2) * 8 / math.pi + i) % 2) < 1
    paint(a, m & dash, "#ffe9a8")
    return a


def main():
    items = []
    for i in range(4):
        items.append((f"slash{i}", slash(i)))
        items.append((f"dust{i}", dust(i)))
    for i in range(3):
        items.append((f"spark{i}", spark(i)))
    items.append(("cursor", cursor()))
    items.append(("arrow", arrow()))
    items.append(("syringe", syringe()))
    items.append(("bomb", bomb()))
    items.append(("bomb_big", bomb(True)))
    for i in range(6):
        items.append((f"boom{i}", boom(i)))
    for i in range(4):
        items.append((f"ring_heal{i}", ring(i, ["#c8ffd0", "#7af08a", "#4ac060", "#2a8a40"])))
        items.append((f"ring_hit{i}", ring(i, ["#fff8e0", "#ffe9a8", "#f0c070", "#c88a4a"])))
    for i in range(3):
        items.append((f"plus{i}", heal_plus(i)))
    for i in range(2):
        items.append((f"mark{i}", arrow_mark(i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "fx"), 512, keep_bottom=False, trim_cells=False)
    preview(at, "fx_pv.png", 4)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
