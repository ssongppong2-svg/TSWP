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


def main():
    items = []
    for i in range(4):
        items.append((f"slash{i}", slash(i)))
        items.append((f"dust{i}", dust(i)))
    for i in range(3):
        items.append((f"spark{i}", spark(i)))
    items.append(("cursor", cursor()))
    at, pos = pack(items, os.path.join(OUT, "sprites", "fx"), 256, keep_bottom=False, trim_cells=False)
    preview(at, "fx_pv.png", 4)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
