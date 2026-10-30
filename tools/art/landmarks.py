"""숲 랜드마크: 기억할 만한 구조물 (위치를 외우고, 길을 찾는 기준)
고대 나무 · 무너진 돌 아치 · 부서진 기둥 · 이끼 낀 수호상 · 갈림길 표지판 · 길가 돌무더기 · 연못가 갈대
→ assets/sprites/landmarks.png + landmarks.json
"""
from px import *
from pack import pack
import flora

MOSS = [GRASS[2], GRASS[3], GRASS[4]]
RUIN = ["#2a2a30", "#3e3e46", "#56565e", "#707078", "#8c8c92", "#a8a8ac", "#c4c4c4"]   # 차가운 회색 돌 (절벽 회갈색과 구분)


def stone_block(a, m, seed, light=0.0):
    h, w = m.shape
    xs, ys = grid(w, h)
    t = 0.55 + light - (xs / w) * 0.25 - (ys / h) * 0.12 + (vnoise(w, h, 3, seed) - 0.5) * 0.15
    ramp(a, m, RUIN[1:7], t, 0.3)


def arch(W=64, H=70):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    # 두 기둥
    for (x0, broken) in ((4, 0), (W - 16, 10)):
        col = (xs >= x0) & (xs < x0 + 12) & (ys >= 18 + broken) & (ys <= H - 1)
        stone_block(a, col, x0)
        for y in range(24 + broken, H, 9):
            paint(a, col & (np.abs(ys - y) < 0.6), RUIN[1])
        paint(a, col & (xs == x0 + 11), RUIN[1])
    # 아치 (왼쪽만 이어지고 오른쪽은 무너짐)
    cx, cy = W / 2, 26
    ring = ell(W, H, cx, cy, 26, 18) & ~ell(W, H, cx, cy, 16, 9) & (ys < cy + 1) & (xs < W - 20)
    stone_block(a, ring, 7, 0.08)
    for k in range(7):
        ang = math.pi + k * math.pi / 8
        x, y = cx + math.cos(ang) * 21, cy + math.sin(ang) * 13.5
        paint(a, ring & (np.abs((xs - cx) * math.sin(ang) * 0.69 - (ys - cy) * math.cos(ang)) < 0.6) & (np.hypot(xs - x, ys - y) < 6), RUIN[1])
    # 무너진 조각
    paint(a, ell(W, H, W - 8, H - 4, 5, 3), RUIN[3])
    # 이끼·덩굴
    moss = (a[..., 3] > 0) & (vnoise(W, H, 4, 3) > 0.62) & (ys < H - 6)
    ramp(a, moss, MOSS, 0.6 - ys / H, 0.3)
    for vx in (8, 14, 46):
        for y in range(10, 40):
            if a[y, vx, 3] > 0 and (y + vx) % 7 < 4:
                a[y, vx] = hx(GRASS[3] if y % 3 else GRASS[5])
    outline(a, "#1a1a20")
    return a


def broken_pillar(seed, W=20, H=40):
    a = canvas(W, H)
    xs, ys = grid(W, H)
    R = random.Random(seed)
    top = R.randint(4, 14)
    col = (xs >= 3) & (xs <= W - 4) & (ys >= top + (xs * 0.4).astype(int) % 5)
    stone_block(a, col, seed)
    for y in range(top + 8, H, 8):
        paint(a, col & (np.abs(ys - y) < 0.6), RUIN[1])
    moss = col & (vnoise(W, H, 3, seed) > 0.6)
    paint(a, moss, GRASS[3])
    outline(a, "#1a1a20")
    return a


def statue(W=30, H=58):
    """이끼 낀 수호상: 받침 + 망토 두른 기사 (검을 땅에 꽂음)"""
    a = canvas(W, H)
    xs, ys = grid(W, H)
    cx = W / 2
    base = (xs >= 2) & (xs <= W - 3) & (ys >= H - 12)
    stone_block(a, base, 1)
    paint(a, base & (ys == H - 12), RUIN[5])
    body = ell(W, H, cx, H - 24, 8, 14) & (ys < H - 12)
    stone_block(a, body, 2, 0.05)
    head = ell(W, H, cx, 12, 5, 5.5)
    stone_block(a, head, 3, 0.1)
    # 투구 장식
    a[5:8, int(cx)] = hx(RUIN[5])
    # 검 (가운데 아래로)
    a[24:H - 12, int(cx)] = hx(RUIN[6])
    a[24, int(cx) - 3:int(cx) + 4] = hx(RUIN[5])
    # 이끼
    moss = (a[..., 3] > 0) & (vnoise(W, H, 3, 8) > 0.6)
    ramp(a, moss, MOSS, 0.5 - ys / H, 0.3)
    outline(a, "#1a1a20")
    return a


def signpost2(W=34, H=38):
    """갈림길 표지판: 위 화살표(둥지) · 아래 화살표(캠프)"""
    a = canvas(W, H)
    xs, ys = grid(W, H)
    WOOD = ["#3a2618", "#5a3c26", "#7a5434", "#9a6c42", "#b88654", "#d4a46a"]
    a[6:H, 16:18] = hx(WOOD[2])
    a[6:H, 17] = hx(WOOD[1])
    for (y0, right) in ((6, True), (16, False)):
        board = (ys >= y0) & (ys <= y0 + 7) & (xs >= 3) & (xs <= W - 4)
        if right:
            board &= ~((xs >= W - 8) & (np.abs(ys - y0 - 3.5) > (W - 4 - xs) * 1.0))
        else:
            board &= ~((xs <= 6) & (np.abs(ys - y0 - 3.5) > (xs - 3) * 1.0))
        t = 0.6 - (ys - y0) / 10
        ramp(a, board, WOOD[2:6], t, 0.3)
        paint(a, board & (ys == y0 + 7), WOOD[1])
        for x in range(8, W - 10, 2):
            a[y0 + 3, x] = hx(WOOD[0])
    # 위 표지 끝에 거미줄 (둥지 쪽이라는 표시)
    a[4:7, W - 6] = hx("#e8e6f0")
    a[5, W - 7:W - 4] = hx("#e8e6f0")
    outline(a, INK_B)
    return a


def cairn(seed, W=16, H=14):
    """길가 돌무더기 (길 안내 — 작고 밝은 돌)"""
    a = canvas(W, H)
    R = random.Random(seed)
    for k, (cx, cy, r) in enumerate(((8, 11, 5.5), (7, 7, 4), (8.5, 3.5, 2.6))):
        m = ell(W, H, cx + R.uniform(-0.6, 0.6), cy, r, r * 0.7)
        xs, ys = grid(W, H)
        ramp(a, m, RUIN[2:7], 0.75 - (xs - cx) / 12 - (ys - cy) / 8, 0.3)
    outline(a, INK_B)
    return a


def reeds(seed, W=18, H=20):
    a = canvas(W, H)
    R = random.Random(seed)
    for k in range(6):
        x = 2 + k * 2.6 + R.uniform(-0.5, 0.5)
        hgt = R.randint(10, 18)
        for y in range(H - hgt, H):
            a[y, int(x)] = hx(GRASS[3] if y > H - hgt + 3 else GRASS[5])
        if k % 2 == 0:
            a[H - hgt:H - hgt + 4, int(x)] = hx("#7a5434")
    return a


def main():
    items = [
        ("arch", arch()),
        ("pillar0", broken_pillar(1)),
        ("pillar1", broken_pillar(2, W=18, H=30)),
        ("statue", statue()),
        ("sign2", signpost2()),
        ("giant_tree", flora.tree_broad(41, W=170, H=190, trunk_h=48)),
    ]
    for i in range(3):
        items.append((f"cairn{i}", cairn(10 + i)))
    for i in range(2):
        items.append((f"reeds{i}", reeds(20 + i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "landmarks"), 512)
    preview(at, "landmarks_pv.png", 3)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
