"""숲 몬스터 (옆모습 — 왼쪽은 뒤집어서)

도토리 병정(근접) · 독버섯(원거리 포자) · 숲 거미(빠름·거미줄) · 포자 탄 · 거미줄 탄
동작: idle 2 · move 4 · attack 3(준비·공격·회복) · hurt 1
→ assets/sprites/enemies.png + enemies.json   이름: <종류>_<동작><번호>
"""
from px import *
from pack import pack

OUTL = "#2a2430"
ACORN = ["#4a2c1a", "#6e4426", "#94603a", "#b47c4a", "#d49a62"]
CAP = ["#3a2a1c", "#5a4430", "#7a6044", "#9a7c58", "#bca07a"]
SHROOM_CAP = ["#5a1e3a", "#8a2e4e", "#b8445e", "#d86a70", "#f09a8a"]
STEM = ["#a89880", "#cfc0a4", "#ece0c8", "#fff8e8"]
SPIDER = ["#1e1828", "#2e2440", "#463a5e", "#62567c", "#8a7ea0"]
EYE_R = "#ff5040"


def acorn(anim, i):
    W, H = 22, 24
    a = canvas(W, H)
    xs, ys = grid(W, H)
    bob = {"idle": [0, 1], "move": [0, -1, 0, -1], "attack": [0, 0, 0], "hurt": [1]}[anim][i]
    lean = {"attack": [-1, 2, 1], "hurt": [-2]}.get(anim, [0] * 4)[i]
    cx, cy = 11 + lean, 14 + bob
    # 다리
    ph = i if anim == "move" else 0
    lx = [(-2, 2), (-1, 1), (2, -2), (1, -1)][ph]
    for k, dx in enumerate(lx):
        x = int(cx - 2 + k * 3 + dx * 0.5)
        a[20:23, x:x + 2] = hx(ACORN[0])
        a[22, x - (1 if k == 0 else 0):x + 2] = hx(ACORN[0])
    # 몸 (도토리 알)
    body = ell(W, H, cx, cy + 1, 6.2, 6.6)
    lam = -((xs - cx) / 6) * 0.5 - ((ys - cy) / 7) * 0.5
    ramp(a, body, ACORN, 0.5 + lam * 0.45, 0.4)
    # 뾰족한 아래 끝
    a[int(cy + 7), int(cx)] = hx(ACORN[1])
    # 모자 (깍정이): 격자 무늬
    cap = ell(W, H, cx, cy - 4, 7.2, 4.0) & (ys < cy - 2.2)
    ramp(a, cap, CAP, 0.55 + lam * 0.4, 0.3)
    paint(a, cap & (((xs.astype(int) + ys.astype(int)) % 3) == 0), CAP[0])
    a[int(cy - 8):int(cy - 6), int(cx)] = hx(CAP[1])
    # 얼굴 (오른쪽을 봄)
    ey = int(cy)
    a[ey:ey + 2, int(cx + 2)] = hx(OUTL)
    a[ey:ey + 2, int(cx + 4)] = hx(OUTL)
    if anim == "attack" and i == 1:
        a[ey + 3, int(cx + 2):int(cx + 5)] = hx(OUTL)          # 기합 입
    # 나뭇가지 창
    if anim == "attack":
        tip = [(cx - 4, cy - 6), (cx + 10, cy + 1), (cx + 8, cy + 3)][i]
        base = (cx - 1, cy + 3)
    else:
        tip = (cx + 8, cy - 8)
        base = (cx + 7, cy + 5)
    n = 14
    for s in range(n + 1):
        u = s / n
        x = int(base[0] + (tip[0] - base[0]) * u)
        y = int(base[1] + (tip[1] - base[1]) * u)
        if 0 <= x < W and 0 <= y < H:
            a[y, x] = hx("#c8a070" if s < n - 1 else "#f0ead8")
            if s < n - 1 and 0 <= x + 1 < W:
                a[y, x + 1] = hx("#8a6440")
    outline(a, OUTL)
    return a


def shroom(anim, i):
    W, H = 22, 24
    a = canvas(W, H)
    xs, ys = grid(W, H)
    squash = {"idle": [0, 1], "move": [0, 1, 0, -1], "attack": [1, -2, 0], "hurt": [2]}[anim][i]
    cx = 11
    top = 3 + squash
    # 줄기 (몸)
    stem = (np.abs(xs + 0.5 - cx) < 4.2 + (ys - 12) * 0.05) & (ys >= 11 + squash // 2) & (ys <= 21)
    ramp(a, stem, STEM, 0.75 - (xs - cx + 4) / 9.0 * 0.6, 0.3)
    # 발
    ph = i if anim == "move" else 0
    for k, dx in enumerate([(-1, 1), (0, 0), (1, -1), (0, 0)][ph]):
        x = int(cx - 3 + k * 4 + dx)
        a[21:23, x:x + 3] = hx(STEM[0])
    # 얼굴
    a[14:16, cx + 1] = hx(OUTL)
    a[14:16, cx + 3] = hx(OUTL)
    if anim == "attack" and i == 1:
        a[17, cx + 1:cx + 4] = hx(OUTL)
        a[18, cx + 2] = hx(OUTL)
    # 갓
    cap = ell(W, H, cx, top + 6, 9.6 - squash * 0.3, 6.4 + squash * 0.3) & (ys <= top + 8.5)
    lam = -((xs - cx) / 9) * 0.5 - ((ys - top - 5) / 6) * 0.6
    ramp(a, cap, SHROOM_CAP, 0.5 + lam * 0.45, 0.35)
    # 점박이
    for (dx, dy, r) in ((-4, 3, 1.4), (2, 1.5, 1.8), (5, 5, 1.1), (-1, 6, 1.0)):
        sp = ell(W, H, cx + dx, top + dy, r, r * 0.9) & cap
        paint(a, sp, STEM[2])
    # 갓 아래 그늘
    paint(a, cap & ~nb(cap, 0, 1), SHROOM_CAP[0])
    outline(a, OUTL)
    return a


def spider(anim, i):
    W, H = 28, 18
    a = canvas(W, H)
    xs, ys = grid(W, H)
    bob = {"idle": [0, 1], "move": [0, -1, 0, -1], "attack": [1, -2, 0], "hurt": [1]}[anim][i]
    lean = {"attack": [-2, 3, 1]}.get(anim, [0] * 4)[i]
    cx, cy = 13 + lean, 10 + bob
    # 다리 8개 (4쌍) — 걸을 때 번갈아
    ph = i if anim == "move" else 0
    for k in range(4):
        side_x = cx - 6 + k * 4
        lift = ((k + ph) % 2) * 2 if anim == "move" else 0
        for d in (-1, 1):
            # 무릎 → 발
            kx, ky = side_x + d * 1, cy - 3 - lift
            fx, fy = side_x + d * 3 + (k - 1.5), H - 2
            for s in range(7):
                u = s / 6
                x = int(cx - 1 + (kx - cx + 1) * u)
                y = int(cy - (cy - ky) * u)
                if 0 <= x < W and 0 <= y < H:
                    a[y, x] = hx(SPIDER[1])
            for s in range(9):
                u = s / 8
                x = int(kx + (fx - kx) * u)
                y = int(ky + (fy - ky) * u)
                if 0 <= x < W and 0 <= y < H:
                    a[y, x] = hx(SPIDER[0] if d < 0 else SPIDER[2])
    # 배 (뒤) + 머리 (앞)
    abd = ell(W, H, cx - 4, cy - 1, 6.5, 5.0)
    lam = -((xs - cx) / 8) * 0.4 - ((ys - cy) / 5) * 0.6
    ramp(a, abd, SPIDER, 0.45 + lam * 0.45, 0.35)
    # 배 무늬 (모래시계)
    mark = (np.abs(xs - (cx - 4)) < 1.2 + np.abs(ys - (cy - 1)) * 0.6) & (np.abs(ys - (cy - 1)) < 2.5) & abd
    paint(a, mark, "#c84050")
    head = ell(W, H, cx + 4, cy, 3.6, 3.0)
    ramp(a, head, SPIDER, 0.5 + lam * 0.45, 0.3)
    # 눈 (빨강 셋)
    for (dx, dy) in ((5, -1), (6, 0), (4, 0)):
        x, y = int(cx + dx), int(cy + dy)
        if 0 <= x < W:
            a[y, x] = hx(EYE_R)
    # 송곳니
    if anim == "attack" and i == 1:
        a[int(cy + 2):int(cy + 4), int(cx + 7)] = hx("#f0e8d8")
    outline(a, OUTL)
    return a


def spore(i):
    a = canvas(8, 8)
    m = ell(8, 8, 4, 4, 2.6 + (i % 2) * 0.4, 2.6 + (i % 2) * 0.4)
    ramp(a, m, ["#6a8a3a", "#9ac050", "#d0f080"], 0.6 - (grid(8, 8)[0] - 4) / 8 - (grid(8, 8)[1] - 4) / 8, 0.3)
    outline(a, "#2a3a20")
    return a


def web_ball(i):
    a = canvas(10, 10)
    c = 5
    for k in range(4):
        ang = k * math.pi / 4 + i * 0.4
        for r in range(1, 5):
            x, y = int(c + math.cos(ang) * r), int(c + math.sin(ang) * r)
            a[y, x] = hx("#f0f0f8")
            x, y = int(c - math.cos(ang) * r), int(c - math.sin(ang) * r)
            a[y, x] = hx("#f0f0f8")
    paint(a, ell(10, 10, c, c, 2.2, 2.2) & ~ell(10, 10, c, c, 1.2, 1.2), "#c8c8d8")
    return a


ANIMS = {"idle": 2, "move": 4, "attack": 3, "hurt": 1}


def main():
    items = []
    for name, fn in (("acorn", acorn), ("shroom", shroom), ("spider", spider)):
        for anim, n in ANIMS.items():
            for i in range(n):
                items.append((f"{name}_{anim}{i}", fn(anim, i)))
    for i in range(2):
        items.append((f"spore{i}", spore(i)))
        items.append((f"web{i}", web_ball(i)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "enemies"), 256, keep_bottom=False, trim_cells=False)
    # 미리보기: 종류별 한 줄
    pv = canvas(30 * 10, 26 * 3)
    for r, name in enumerate(("acorn", "shroom", "spider")):
        k = 0
        for anim, n in ANIMS.items():
            for i in range(n):
                f = dict(items)[f"{name}_{anim}{i}"]
                over(pv, f, k * 30, r * 26)
                k += 1
    preview(pv, "enemies_pv.png", 4)
    print(len(pos), at.shape)


if __name__ == "__main__":
    main()
