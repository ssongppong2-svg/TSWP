"""캠프 소품 (모험 출발 전 쉬는 곳)

천막 · 모닥불(4프레임) · 통나무 의자 · 나무 상자 · 통 · 등불 기둥 · 표지판 · 깃발(4프레임) · 수레 · 장작 더미 · 냄비
→ assets/sprites/camp.png + camp.json
"""
from px import *
from pack import pack

WOOD = ["#3a2618", "#5a3c26", "#7a5434", "#9a6c42", "#b88654", "#d4a46a"]
CLOTH_R = ["#5a2a2a", "#8a3a34", "#b8524a", "#d8786a", "#f0a088"]
CLOTH_C = ["#7a6a50", "#a8987a", "#d0c4a0", "#ece2c4", "#fff8e6"]
CLOTH_B = ["#283a4a", "#3a5468", "#527490", "#7aa0b8", "#a8c8d8"]
METAL = ["#2a2c30", "#4a4e54", "#70767c", "#a0a6aa", "#d0d4d4"]
FIRE = ["#7a2a18", "#c8481e", "#f07a28", "#ffb040", "#ffe080", "#fff8d0"]
EMBER = "#ff9a3a"


def plank_wall(a, m, cols, vertical=True, seed=0, gap=4):
    """판자 무늬: 줄마다 이음새, 왼쪽 위 밝게"""
    h, w = m.shape
    xs, ys = grid(w, h)
    t = 0.55 - (xs / w) * 0.25 - (ys / h) * 0.15 + (vnoise(w, h, 3, seed) - 0.5) * 0.15
    ramp(a, m, cols, t, 0.4)
    line = ((xs.astype(int) % gap) == 0) if vertical else ((ys.astype(int) % gap) == 0)
    paint(a, m & line, cols[0])


def tent(seed, cols, W=56, H=46, stripe=None):
    """A자 천막: 앞에서 본 삼각형 + 오른쪽 옆면이 살짝 보이고 입구는 어둡게, 앞 자락 젖힘"""
    a = canvas(W, H)
    xs, ys = grid(W, H)
    apex_x, apex_y = W * 0.45, 4
    base_y = H - 3
    left_x, right_x = 3, W * 0.82
    # 옆면 (오른쪽, 어둡게): 꼭대기 → 오른쪽 뒤 모서리
    side = (ys >= apex_y) & (ys <= base_y - 2) & (xs >= apex_x) & (xs <= W - 3)
    side &= (ys - apex_y) >= (xs - apex_x) * 0.55
    side &= (xs - apex_x) <= (ys - apex_y) * 1.6 + 2
    ramp(a, side, cols[:3], 0.3 + (ys - apex_y) / H * 0.3, 0.5)
    # 앞면 삼각형
    k = (ys - apex_y) / (base_y - apex_y)
    front = (ys >= apex_y) & (ys <= base_y) & (xs >= apex_x - k * (apex_x - left_x)) & (xs <= apex_x + k * (right_x - apex_x))
    t = 0.65 - (xs - left_x) / (right_x - left_x) * 0.35 + (vnoise(W, H, 4, seed) - 0.5) * 0.08
    ramp(a, front, cols[1:], t, 0.4)
    if stripe:
        st = front & (((xs - apex_x) * 0.0 + ys).astype(int) % 9 < 3) & (ys > apex_y + 6)
        st = front & (np.abs(xs - apex_x) < 1.2 + k * 3) & (ys > apex_y + 3)
        paint(a, st, stripe)
    # 입구 (어두운 안쪽 + 젖힌 자락)
    door = front & (ys > apex_y + (base_y - apex_y) * 0.35) & (np.abs(xs - apex_x) < k * (right_x - left_x) * 0.22)
    paint(a, door, "#1e1a1c")
    paint(a, door & (ys > base_y - 3), "#2e2624")
    flap = front & ~door & (ys > apex_y + (base_y - apex_y) * 0.35) & (np.abs(xs - apex_x) < k * (right_x - left_x) * 0.22 + 3) & (xs > apex_x)
    paint(a, flap, cols[-1])
    # 꼭대기 장대 + 줄
    a[0:apex_y + 1, int(apex_x)] = hx(WOOD[2])
    a[0, int(apex_x)] = hx(WOOD[4])
    # 바닥 그늘 줄
    paint(a, front & (ys >= base_y - 1), cols[0])
    # 말뚝
    for px_ in (1, W - 3):
        a[base_y - 2:base_y + 2, px_] = hx(WOOD[1])
    outline(a, INK_B)
    return a


def campfire_frame(i, W=28, H=30):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx = W / 2
    base = H - 4
    # 돌 테두리
    R = random.Random(5)
    for k in range(9):
        ang = math.pi * (k / 8.0)
        sx = cx + math.cos(ang) * 10
        sy = base + 1 + math.sin(ang) * 2.4 * (1 if k % 2 else 0.8)
        st = ell(W, H, sx, sy, 2.6, 1.8)
        ramp(a, st, STONE[2:7], 0.7 - (ys - sy + 2) / 4.0, 0.3)
    # 장작 (X자)
    for (x0, y0, x1, y1) in ((cx - 7, base + 1, cx + 6, base - 3), (cx + 7, base + 1, cx - 6, base - 3)):
        for s in range(30):
            u = s / 29
            x = x0 + (x1 - x0) * u
            y = y0 + (y1 - y0) * u
            for o in (0, 1):
                xi, yi = int(x), int(y) + o
                if 0 <= xi < W and 0 <= yi < H:
                    a[yi, xi] = hx(WOOD[2] if o == 0 else WOOD[1])
    paint(a, ell(W, H, cx, base - 1, 3.5, 1.4), "#3a1a10")
    # 불꽃: 프레임마다 흔들리는 물방울 모양 3겹
    ph = i * math.pi / 2
    for layer, (rw, rh, col_i) in enumerate(((6.5, 15, 1), (4.6, 11, 3), (2.6, 6.5, 5))):
        sway = math.sin(ph + layer) * (1.6 - layer * 0.4)
        top = base - 2 - rh
        tip_x = cx + sway
        u = np.clip((ys - top) / rh, 0, 1)
        half = rw * np.sqrt(np.clip(u, 0, 1)) * (1 - np.clip((u - 0.75) / 0.25, 0, 1) * 0.35)
        lean = (1 - u) * sway
        m = (ys >= top) & (ys <= base - 1) & (np.abs(xs + 0.5 - (cx + lean)) <= half)
        # 불꽃 가장자리 날름거림
        flick = vnoise(W, H, 2, 30 + i * 7 + layer) > 0.25
        m &= flick | (np.abs(xs + 0.5 - (cx + lean)) < half - 1.2)
        paint(a, m, FIRE[col_i])
        if layer == 0:
            paint(a, m & ~erode(m), FIRE[0])
    # 불티
    for k in range(3):
        R2 = random.Random(i * 13 + k)
        x = int(cx + R2.uniform(-6, 6))
        y = int(base - 18 - R2.uniform(0, 8))
        if 0 <= y < H:
            a[y, x] = hx(EMBER)
    return a


def log_bench(W=34, H=14):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    body = (xs > 2) & (xs < W - 5) & (ys > 2) & (ys < H - 2)
    ramp(a, body, WOOD[1:6], 0.85 - (ys - 2) / (H - 4) * 0.8, 0.4)
    for x in range(7, W - 7, 7):
        a[4:H - 3, x] = hx(WOOD[1])
    end = ell(W, H, W - 5, H / 2, 3, 5)
    paint(a, end, TAN[6])
    paint(a, ell(W, H, W - 5, H / 2, 1.5, 2.5), TAN[4])
    # 다리
    for x in (5, W - 10):
        a[H - 2:H, x:x + 2] = hx(WOOD[0])
    outline(a, INK_B)
    return a


def crate(W=18, H=19):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    top = (ys < 6) & (xs >= 1) & (xs <= W - 2)
    front = (ys >= 6) & (ys <= H - 1) & (xs >= 1) & (xs <= W - 2)
    plank_wall(a, top, WOOD[2:6], vertical=False, seed=1, gap=3)
    plank_wall(a, front, WOOD[1:5], vertical=True, seed=2, gap=4)
    # 테두리 각목
    for m in (front & ((xs <= 2) | (xs >= W - 3) | (ys <= 7) | (ys >= H - 2)),):
        paint(a, m, WOOD[3])
    paint(a, front & (np.abs((xs - 2) - (ys - 7) * (W - 5) / (H - 9)) < 1.0), WOOD[3])
    paint(a, top & (ys == 5), WOOD[1])
    outline(a, INK_B)
    return a


def barrel(W=16, H=20):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx = W / 2
    bulge = 6.5 + np.sin((ys - 3) / (H - 4) * math.pi) * 1.2
    body = (np.abs(xs + 0.5 - cx) <= bulge) & (ys >= 4) & (ys <= H - 1)
    t = 0.75 - (xs - cx + 7) / 16.0 * 0.6
    ramp(a, body, WOOD[1:6], t, 0.4)
    for x in range(int(cx - 5), int(cx + 6), 3):
        paint(a, body & (xs.astype(int) == x), WOOD[1])
    for y in (6, H - 4):
        paint(a, body & (ys.astype(int) == y), METAL[2])
        paint(a, body & (ys.astype(int) == y) & (xs > cx + 2), METAL[1])
    lid = ell(W, H, cx, 4.5, 6.5, 2.4)
    ramp(a, lid, WOOD[2:6], 0.6 - (xs - cx) / 14, 0.3)
    paint(a, lid & ~erode(lid), WOOD[1])
    outline(a, INK_B)
    return a


def lantern_post(i=0, W=14, H=40):
    a = canvas(W, H)
    cx = W // 2
    # 기둥
    a[10:H, cx - 1:cx + 1] = hx(WOOD[2])
    a[10:H, cx] = hx(WOOD[1])
    a[H - 2:H, cx - 3:cx + 3] = hx(WOOD[1])
    # 팔 + 등
    a[8, cx - 1:cx + 5] = hx(WOOD[2])
    lx, ly = cx + 3, 10
    a[ly:ly + 7, lx - 2:lx + 3] = hx(METAL[1])
    a[ly + 1:ly + 6, lx - 1:lx + 2] = hx(FIRE[4] if i == 0 else FIRE[3])
    a[ly + 2:ly + 4, lx] = hx(FIRE[5])
    a[ly - 1, lx - 1:lx + 2] = hx(METAL[2])
    outline(a, INK_B)
    return a


def signpost(W=26, H=30):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    a[8:H, 12:14] = hx(WOOD[2])
    a[8:H, 13] = hx(WOOD[1])
    board = (xs >= 2) & (xs <= W - 3) & (ys >= 3) & (ys <= 11)
    board &= ~((xs >= W - 5) & (np.abs(ys - 7) > (W - 3 - xs) * 1.6))      # 화살표 끝
    plank_wall(a, board, WOOD[2:6], vertical=False, seed=3, gap=4)
    # 글씨 흉내
    for x in range(5, W - 8, 2):
        a[6, x] = hx(WOOD[0])
        if x % 4 == 1:
            a[8, x] = hx(WOOD[0])
    outline(a, INK_B)
    return a


def flag_frame(i, W=22, H=40, cols=CLOTH_R):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    a[2:H, 2:4] = hx(WOOD[2])
    a[2:H, 3] = hx(WOOD[1])
    a[1, 2:4] = hx(METAL[3])
    ph = i * math.pi / 2
    wave = np.sin(xs * 0.5 - ph) * 1.4
    m = (xs >= 4) & (xs <= W - 2) & (ys >= 4 + wave) & (ys <= 15 + wave) & ~((xs > W - 6) & (np.abs(ys - 9.5 - wave) < (xs - (W - 6)) * 1.0))
    t = 0.6 + np.cos(xs * 0.5 - ph) * 0.25
    ramp(a, m, cols[1:], t, 0.3)
    # 문양 (방패)
    em = ell(W, H, 11, 9.5 + np.sin(11 * 0.5 - ph) * 1.4, 2.2, 2.6) & m
    paint(a, em, "#f0d890")
    outline(a, INK_B)
    return a


def cart(W=46, H=30):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    bed = (xs >= 3) & (xs <= W - 10) & (ys >= 9) & (ys <= 19)
    plank_wall(a, bed, WOOD[1:5], vertical=False, seed=7, gap=4)
    top = (xs >= 3) & (xs <= W - 10) & (ys >= 6) & (ys < 9)
    paint(a, top, WOOD[4])
    # 실린 짐 (자루·상자)
    sack = ell(W, H, 14, 5, 6, 4.5)
    ramp(a, sack, CLOTH_C[:4], 0.6 - (xs - 14) / 14 - (ys - 5) / 12, 0.4)
    sack2 = ell(W, H, 25, 5, 5, 4)
    ramp(a, sack2, CLOTH_C[:4], 0.6 - (xs - 25) / 12 - (ys - 5) / 10, 0.4)
    # 손잡이
    for x in range(W - 10, W - 1):
        a[16 - (x - (W - 10)) // 3, x] = hx(WOOD[2])
    # 바퀴
    for wx in (10, W - 17):
        wh = ell(W, H, wx, H - 7, 6, 6)
        paint(a, wh, WOOD[1])
        paint(a, ell(W, H, wx, H - 7, 4.5, 4.5), WOOD[3])
        paint(a, ell(W, H, wx, H - 7, 1.5, 1.5), METAL[2])
        for k in range(4):
            ang = k * math.pi / 4
            for r in range(2, 5):
                a[int(H - 7 + math.sin(ang) * r), int(wx + math.cos(ang) * r)] = hx(WOOD[1])
    outline(a, INK_B)
    return a


def woodpile(W=30, H=18):
    a = canvas(W, H)
    rows = [(4, 6), (3, 11), (2, 16)]
    for r, (n, y) in enumerate([(4, 13), (3, 8), (2, 3)]):
        for k in range(n):
            x = 3 + r * 3 + k * 7
            e = ell(W, H, x + 3, y + 2.5, 3.2, 3)
            paint(a, e, WOOD[2])
            paint(a, ell(W, H, x + 3, y + 2.5, 2.2, 2), TAN[6])
            paint(a, ell(W, H, x + 3, y + 2.5, 0.8, 0.8), TAN[4])
    outline(a, INK_B)
    return a


def pot(W=20, H=24):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx = W / 2
    # 삼각대
    for (x0, x1) in ((2, cx), (W - 3, cx)):
        for y in range(2, H):
            u = (y - 2) / (H - 2)
            x = int(x1 + (x0 - x1) * u)
            a[y, x] = hx(WOOD[1])
    body = ell(W, H, cx, H - 8, 6.5, 5.5) & (ys > H - 12)
    ramp(a, body, METAL[:4], 0.7 - (xs - cx + 6) / 14, 0.3)
    rim = ell(W, H, cx, H - 12, 6.5, 1.6)
    paint(a, rim, METAL[3])
    paint(a, ell(W, H, cx, H - 12, 5, 1), "#c8a050")
    a[1:H - 12, int(cx)] = hx(METAL[1])
    outline(a, INK_B)
    return a


def main():
    items = [
        ("tent_red", tent(1, CLOTH_R, stripe=CLOTH_C[3])),
        ("tent_cream", tent(2, CLOTH_C, stripe=CLOTH_R[2])),
        ("tent_blue", tent(3, CLOTH_B, W=48, H=40, stripe=CLOTH_C[3])),
        ("bench", log_bench()),
        ("crate", crate()),
        ("barrel", barrel()),
        ("sign", signpost()),
        ("cart", cart()),
        ("woodpile", woodpile()),
        ("pot", pot()),
    ]
    for i in range(4):
        items.append((f"fire{i}", campfire_frame(i)))
        items.append((f"flag{i}", flag_frame(i)))
    for i in range(2):
        items.append((f"lantern{i}", lantern_post(i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "camp"), 512)
    preview(at, "camp_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
