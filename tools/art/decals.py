"""땅에 구워 넣는 작은 무늬 (지형 데칼): 낙엽 더미 · 자갈 · 잔가지 · 이끼 · 꽃잎 · 흙 자국 · 발자국 · 금
타일 반복을 깨려고 지형 텍스처 위에 무작위로 찍는다 (Terrain.stamp — 실행 중 비용 0)
→ assets/sprites/decals.png + decals.json
"""
from px import *
from pack import pack

LEAVES = ["#8a4a24", "#b8662e", "#d88a3a", "#e8b050", "#a87a3a"]


def leaf_pile(seed, W=22, H=14):
    a = canvas(W, H)
    R = random.Random(seed)
    for _ in range(26):
        x, y = R.uniform(2, W - 3), R.uniform(2, H - 2)
        if ((x - W / 2) / (W / 2)) ** 2 + ((y - H / 2) / (H / 2)) ** 2 > 1:
            continue
        c = hx(R.choice(LEAVES))
        xi, yi = int(x), int(y)
        a[yi, xi] = c
        a[yi, xi + 1] = c
        if R.random() < 0.5:
            a[yi + 1, xi] = hx("#5a3018")
    return a


def pebbles(seed, W=14, H=10):
    a = canvas(W, H)
    R = random.Random(seed)
    for _ in range(R.randint(3, 6)):
        x, y = R.randrange(1, W - 3), R.randrange(1, H - 2)
        w, h = R.choice([(2, 1), (3, 2), (2, 2)])
        a[y:y + h, x:x + w] = hx("#9a9080")
        a[y, x:x + w] = hx("#c8bca8")
        a[y + h, x + 1:x + w + 1] = hx("#4a4038")
    return a


def twig(seed, W=16, H=8):
    a = canvas(W, H)
    R = random.Random(seed)
    y = R.uniform(2, H - 3)
    for x in range(2, W - 2):
        y += R.uniform(-0.6, 0.6)
        y = max(1, min(H - 2, y))
        a[int(y), x] = hx("#6a4a2e")
        a[int(y) + 1, x] = hx("#3a2818")
        if R.random() < 0.15:
            a[int(y) - 1, x + 1] = hx("#6a4a2e")
    return a


def moss(seed, W=20, H=12):
    a = canvas(W, H)
    R = random.Random(seed)
    xs, ys = grid(W, H)
    m = ell(W, H, W / 2, H / 2, W / 2 - 1, H / 2 - 1) & (vnoise(W, H, 3, seed) > 0.42)
    ramp(a, m, [GRASS[2], GRASS[3], GRASS[5]], vnoise(W, H, 2, seed + 1), 0.6)
    return a


def petals(seed, W=18, H=12, col=BLOOM):
    a = canvas(W, H)
    R = random.Random(seed)
    for _ in range(14):
        x, y = R.randrange(1, W - 1), R.randrange(1, H - 1)
        a[y, x] = hx(R.choice(col[2:6]))
    return a


def mud(seed, W=24, H=12):
    """젖은 흙 얼룩 + 발자국 (구멍처럼 보이지 않게: 테두리 없이 길 색보다 한두 단계만 짙게)"""
    a = canvas(W, H)
    R = random.Random(seed)
    xs, ys = grid(W, H)
    m = ell(W, H, W / 2, H / 2, W / 2 - 1, H / 2 - 1)
    m &= vnoise(W, H, 4, seed) > 0.42
    ramp(a, m, ["#977452", "#a07c56"], vnoise(W, H, 3, seed + 2), 0.7)
    # 발자국: 비스듬히 이어지는 작은 두 점 쌍
    x, y = R.uniform(2, 5), R.uniform(3, H - 4)
    while x < W - 3:
        yy = int(y + (1 if int(x) % 2 else -1))
        if 0 <= yy < H - 1:
            a[yy, int(x):int(x) + 2] = hx("#7e5e3e")
            if R.random() < 0.6:
                a[yy + 1, int(x)] = hx("#7e5e3e")
        x += R.uniform(3.5, 5)
        y += R.uniform(-0.6, 0.6)
    return a


def crack(seed, W=18, H=10):
    a = canvas(W, H)
    R = random.Random(seed)
    x, y = 1.0, H / 2
    while x < W - 1:
        a[int(y), int(x)] = hx("#3a2e24")
        x += 1
        y += R.choice([-1, 0, 0, 1]) * 0.7
        y = max(1, min(H - 2, y))
        if R.random() < 0.15:
            a[max(0, int(y) - 1), int(x)] = hx("#3a2e24")
    return a


def main():
    items = []
    for i in range(3):
        items.append((f"leaves{i}", leaf_pile(i)))
        items.append((f"pebbles{i}", pebbles(10 + i)))
        items.append((f"moss{i}", moss(20 + i)))
    for i in range(2):
        items.append((f"twig{i}", twig(30 + i)))
        items.append((f"petals{i}", petals(40 + i)))
        items.append((f"mud{i}", mud(50 + i)))
        items.append((f"crack{i}", crack(60 + i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "decals"), 256, keep_bottom=False)
    preview(at, "decals_pv.png", 4)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
