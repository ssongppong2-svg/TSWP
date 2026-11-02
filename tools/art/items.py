"""아이템 그림: 장비·소비 아이콘 16x16 (UI 와 땅에 떨어진 모습 둘 다) + 상자·진열대·바나나 껍질·상인·왕관
→ assets/sprites/items.png/.json (아이콘) · assets/sprites/loot.png/.json (세계 빌보드)
규칙: 빛은 왼쪽 위 (위·왼쪽 가장자리 밝게, 아래·오른쪽 어둡게), 외곽선은 짙은 자주 OUTL, 검정 금지
"""
from PIL import Image, ImageDraw

from px import *
from pack import pack

OUTL = "#24141c"
S = 16


# ------------------------------------------------------------------ 모양 → 마스크
def _m(w=S, h=S):
    im = Image.new("L", (w, h), 0)
    return im, ImageDraw.Draw(im)


def poly(pts, w=S, h=S):
    im, d = _m(w, h)
    d.polygon(pts, fill=255)
    return np.array(im) > 127


def ellm(x0, y0, x1, y1, w=S, h=S):
    im, d = _m(w, h)
    d.ellipse((x0, y0, x1, y1), fill=255)
    return np.array(im) > 127


def rectm(x0, y0, x1, y1, w=S, h=S):
    m = np.zeros((h, w), bool)
    m[y0:y1 + 1, x0:x1 + 1] = True
    return m


def linem(pts, width=1, w=S, h=S):
    im, d = _m(w, h)
    d.line(pts, fill=255, width=width)
    return np.array(im) > 127


def shade(a, m, cols):
    """3색 [어둠, 중간, 밝음] — 위·왼쪽 가장자리 밝게, 아래·오른쪽 가장자리 어둡게"""
    if isinstance(cols, str):
        cols = [cols, cols, cols]
    paint(a, m, cols[1])
    dark = m & (~nb(m, 1, 0) | ~nb(m, 0, 1))
    lite = m & (~nb(m, -1, 0) | ~nb(m, 0, -1)) & ~dark
    paint(a, dark, cols[0])
    paint(a, lite, cols[2])


def px1(a, pts, col):
    for x, y in pts:
        if 0 <= x < a.shape[1] and 0 <= y < a.shape[0]:
            a[y, x] = hx(col)


def fin(a, glow=None):
    outline(a, OUTL)
    return a


# ------------------------------------------------------------------ 팔레트
GOLD = ["#a8742a", "#f0c050", "#fff0a0"]
STEEL = ["#5a6478", "#a8b6c8", "#f4f8fc"]
RUST = ["#6a3a24", "#a8603a", "#d8945a"]
LEATH = ["#5a3a24", "#8a5a34", "#c08a5a"]
WOOD = ["#5a3a24", "#8a5a34", "#b8844e"]
RED = ["#8a2030", "#d84850", "#ff9a90"]
GREEN = ["#2a6a3a", "#4cb060", "#a8f0a0"]
BLUE = ["#2a4a8a", "#4a8ae0", "#a8d8ff"]
PURP = ["#4a2a6a", "#8a4ac0", "#d0a0ff"]
BONE = ["#a89a7a", "#e8e0c8", "#ffffff"]
DARK = ["#1e1a2e", "#3a3050", "#6a5a88"]
CREAM = ["#b8a888", "#ece0c0", "#fffaf0"]
CYAN = ["#2a7a8a", "#4ad0e0", "#c8fbff"]
ORNG = ["#a84a1e", "#f08a30", "#ffd080"]


# ------------------------------------------------------------------ 장비
def rusty_sword():
    a = canvas(S, S)
    shade(a, linem([(5, 10), (13, 2)], 2), RUST)
    px1(a, [(7, 7), (10, 5)], "#5a3020")                         # 녹 얼룩
    shade(a, linem([(2, 8), (7, 13)], 2), ["#6a4a2a", "#9a7a4a", "#c8a870"])
    shade(a, linem([(1, 14), (4, 11)], 2), LEATH)
    return fin(a)


def swift_glove():
    a = canvas(S, S)
    m = rectm(5, 6, 11, 12)
    for x0 in (5, 7, 9):
        m |= rectm(x0, 2 + (1 if x0 == 5 else 0), x0 + 1, 6)
    m |= rectm(11, 4, 12, 7)
    m |= poly([(12, 8), (14, 7), (14, 9), (12, 11)])
    shade(a, m, LEATH)
    shade(a, rectm(5, 12, 11, 14), ["#2a6a8a", "#4ab0d0", "#a8ecff"])
    for y in (5, 8, 11):                                         # 속도선
        px1(a, [(1, y), (2, y), (3, y)], "#c8f4ff")
    return fin(a)


def berserk_helm():
    a = canvas(S, S)
    shade(a, poly([(3, 5), (1, 1), (5, 4)]), BONE)
    shade(a, poly([(12, 5), (14, 1), (10, 4)]), BONE)
    shade(a, ellm(3, 3, 12, 14) & ~rectm(0, 13, 15, 15), ["#6a2a2a", "#b04a40", "#e88a70"])
    paint(a, rectm(5, 8, 10, 9), "#1a0e14")
    px1(a, [(6, 8), (9, 8)], "#ff4040")
    return fin(a)


def turtle_shell():
    a = canvas(S, S)
    m = ellm(1, 3, 14, 13)
    shade(a, m, ["#2e5a2a", "#5a9a44", "#a8d870"])
    for p in [linem([(5, 5), (4, 8), (5, 11)]), linem([(10, 5), (11, 8), (10, 11)]), linem([(4, 8), (11, 8)])]:
        paint(a, p & m, "#2e4a24")
    paint(a, rectm(1, 12, 14, 13) & m, "#c8a860")
    return fin(a)


def feather_boots():
    a = canvas(S, S)
    shade(a, poly([(4, 3), (9, 3), (9, 10), (13, 11), (13, 14), (4, 14)]), ["#3a5a7a", "#5a8ab0", "#a8d0f0"])
    shade(a, rectm(4, 13, 13, 14), LEATH)
    shade(a, poly([(3, 4), (0, 2), (1, 6), (0, 8), (3, 8)]), CREAM)
    return fin(a)


def vampire_fang():
    a = canvas(S, S)
    shade(a, poly([(3, 2), (7, 2), (6, 11), (5, 13)]), BONE)
    shade(a, poly([(9, 2), (13, 2), (11, 10), (10, 11)]), BONE)
    px1(a, [(5, 13), (5, 14), (10, 11), (10, 12)], "#d02030")
    px1(a, [(5, 15)], "#ff5060")
    return fin(a)


def thorn_armor():
    a = canvas(S, S)
    shade(a, poly([(3, 3), (12, 3), (13, 6), (11, 7), (11, 14), (4, 14), (4, 7), (2, 6)]), ["#3a4a3a", "#6a7a5a", "#a8b88a"])
    for (x, y) in [(1, 4), (14, 4), (2, 10), (13, 10), (7, 1), (8, 1)]:
        px1(a, [(x, y)], "#c8d8a0")
    paint(a, linem([(7, 4), (7, 13)]), "#3a4a2e")
    return fin(a)


def lucky_coin():
    a = canvas(S, S)
    shade(a, ellm(2, 2, 13, 13), GOLD)
    for (x, y) in [(7, 5), (6, 6), (8, 6), (5, 7), (9, 7), (7, 7), (6, 8), (8, 8), (7, 9), (7, 10)]:
        px1(a, [(x, y)], "#4c9a4a")
    return fin(a)


def storm_ring():
    a = canvas(S, S)
    shade(a, ellm(3, 5, 12, 14) & ~ellm(5, 7, 10, 12), STEEL)
    shade(a, poly([(7, 0), (10, 3), (7, 6), (4, 3)]), ["#a88a1a", "#ffe050", "#fffbd0"])
    px1(a, [(7, 2), (6, 3), (7, 3), (8, 4)], "#ffffff")
    return fin(a)


def flame_charm():
    a = canvas(S, S)
    paint(a, linem([(3, 0), (7, 5), (11, 0)]), "#c8b070")
    shade(a, ellm(4, 5, 11, 14), ["#7a2a14", "#d8582a", "#ffa060"])
    shade(a, poly([(7, 6), (9, 9), (9, 12), (6, 12), (5, 10)]), ["#e0802a", "#ffc040", "#fff4a0"])
    return fin(a)


def phoenix_feather():
    a = canvas(S, S)
    m = poly([(13, 1), (10, 2), (5, 7), (3, 11), (4, 12), (8, 10), (12, 6)])
    shade(a, m, ["#a83a1a", "#f07a2a", "#ffd060"])
    paint(a, linem([(12, 2), (2, 14)]), "#fff0a0")
    px1(a, [(2, 14), (1, 15)], "#a8742a")
    return fin(a)


def shadow_cloak():
    a = canvas(S, S)
    shade(a, poly([(7, 1), (11, 3), (13, 14), (2, 14), (4, 3)]), DARK)
    paint(a, ellm(5, 3, 9, 7), "#120e1c")
    px1(a, [(6, 5), (8, 5)], "#c8a0ff")
    return fin(a)


def cursed_crown():
    a = canvas(S, S)
    shade(a, poly([(1, 13), (1, 4), (4, 8), (7, 2), (10, 8), (14, 4), (14, 13)]), GOLD)
    paint(a, rectm(1, 11, 14, 12), "#a8742a")
    for x in (4, 7, 11):
        px1(a, [(x, 11)], "#b040e0")
    px1(a, [(7, 7)], "#d070ff")
    for (x, y) in [(0, 1), (15, 2), (13, 0), (2, 0)]:
        px1(a, [(x, y)], "#8a40c0")
    return fin(a)


def echo_bell():
    a = canvas(S, S)
    shade(a, poly([(7, 2), (9, 2), (11, 6), (12, 11), (4, 11), (5, 6)]), GOLD)
    shade(a, rectm(3, 11, 13, 12), ["#a8742a", "#d8a040", "#fff0a0"])
    shade(a, ellm(7, 12, 9, 14), ["#5a4a3a", "#8a7a6a", "#b8a898"])
    for (x, y) in [(1, 5), (1, 6), (0, 7), (14, 5), (14, 6), (15, 7)]:
        px1(a, [(x, y)], "#fff4c0")
    return fin(a)


def blood_pact():
    a = canvas(S, S)
    shade(a, rectm(3, 2, 12, 13), CREAM)
    for y in (4, 6, 8):
        paint(a, rectm(5, y, 10, y), "#a89878")
    shade(a, ellm(8, 9, 13, 14), RED)
    px1(a, [(10, 11)], "#ffd0d0")
    return fin(a)


def prank_glove():
    a = canvas(S, S)
    paint(a, linem([(2, 13), (4, 11), (3, 10), (5, 8), (4, 7), (6, 6)]), "#c8c8d0")
    shade(a, ellm(6, 1, 14, 9), RED)
    shade(a, ellm(5, 5, 8, 9), RED)
    shade(a, rectm(0, 12, 4, 15), WOOD)
    return fin(a)


def glass_cannon():
    a = canvas(S, S)
    shade(a, linem([(3, 11), (12, 4)], 4), ["#5a8ab0", "#a8d8f0", "#ffffff"])
    paint(a, linem([(4, 9), (11, 3)]), "#ffffff")
    shade(a, ellm(1, 9, 7, 15), WOOD)
    px1(a, [(4, 12)], "#3a2418")
    shade(a, ellm(11, 2, 14, 5), ["#3a5a7a", "#5a8ab0", "#c8ecff"])
    return fin(a)


def venom_fang():
    a = canvas(S, S)
    shade(a, poly([(13, 1), (14, 2), (7, 10), (6, 9)]), ["#3a7a3a", "#7ad06a", "#d8ffb0"])
    shade(a, linem([(3, 8), (8, 13)], 2), ["#3a2a3a", "#6a4a6a", "#9a7a9a"])
    shade(a, linem([(1, 14), (4, 11)], 2), LEATH)
    px1(a, [(9, 9), (9, 10), (10, 12)], "#7aff5a")
    return fin(a)


def giant_belt():
    a = canvas(S, S)
    shade(a, rectm(0, 6, 15, 10), LEATH)
    shade(a, rectm(5, 4, 10, 12) & ~rectm(7, 6, 8, 10), GOLD)
    paint(a, rectm(7, 7, 8, 9), "#5a3a24")
    return fin(a)


def hourglass():
    a = canvas(S, S)
    shade(a, rectm(3, 1, 12, 2), WOOD)
    shade(a, rectm(3, 13, 12, 14), WOOD)
    g = poly([(4, 3), (11, 3), (8, 8), (11, 12), (4, 12), (7, 8)])
    shade(a, g, ["#8ab0c0", "#c8e8f0", "#ffffff"])
    paint(a, poly([(5, 10), (10, 10), (11, 12), (4, 12)]) & g, "#f0c050")
    paint(a, poly([(6, 5), (9, 5), (8, 7), (7, 7)]) & g, "#f0c050")
    px1(a, [(7, 8), (7, 9)], "#f0c050")
    return fin(a)


def ult_battery():
    a = canvas(S, S)
    shade(a, rectm(3, 3, 12, 14), ["#2a3a4a", "#4a5a6a", "#8a9aaa"])
    shade(a, rectm(6, 1, 9, 2), STEEL)
    shade(a, poly([(8, 4), (5, 9), (8, 9), (6, 13), (11, 7), (8, 7), (10, 4)]), CYAN)
    return fin(a)


def heart_locket():
    a = canvas(S, S)
    paint(a, linem([(3, 0), (7, 4), (11, 0)]), "#c8b070")
    m = poly([(7, 6), (5, 4), (2, 5), (2, 8), (7, 14), (12, 8), (12, 5), (9, 4)])
    shade(a, m, GOLD)
    shade(a, m & ~(m & (~nb(m, 1, 0) | ~nb(m, 0, 1) | ~nb(m, -1, 0) | ~nb(m, 0, -1))), RED)
    return fin(a)


# ------------------------------------------------------------------ 소비
def bottle(liq, big=False):
    a = canvas(S, S)
    body = ellm(2, 5, 13, 15) if big else ellm(3, 6, 12, 15)
    shade(a, body, ["#8ab0c0", "#d0ecf4", "#ffffff"])
    inner = body & ~(body & (~nb(body, 1, 0) | ~nb(body, 0, 1) | ~nb(body, -1, 0) | ~nb(body, 0, -1)))
    paint(a, inner & ~rectm(0, 0, 15, 8 if big else 9), liq[1])
    paint(a, inner & rectm(0, 8 if big else 9, 15, 9 if big else 10), liq[2])
    shade(a, rectm(6, 2, 9, 5), ["#8ab0c0", "#d0ecf4", "#ffffff"])
    shade(a, rectm(5, 1, 10, 2), WOOD)
    px1(a, [(5, 10), (5, 11)], "#ffffff")
    if big:
        paint(a, rectm(2, 12, 13, 12) & body, "#f0c050")
    return fin(a)


def smoke():
    a = canvas(S, S)
    shade(a, ellm(3, 5, 12, 14), ["#3a3a48", "#6a6a7a", "#a8a8b8"])
    paint(a, linem([(9, 5), (11, 2)]), "#c89a5a")
    for (x0, y0, x1, y1) in [(10, 0, 14, 3), (12, 2, 15, 5)]:
        shade(a, ellm(x0, y0, x1, y1), ["#a8a8b8", "#d8d8e0", "#ffffff"])
    return fin(a)


def banana():
    a = canvas(S, S)
    m = ellm(0, -2, 15, 13) & ~ellm(-1, -6, 13, 9)              # 초승달 모양 바나나
    m &= ~rectm(0, 0, 1, 15)
    shade(a, m, ["#a8901a", "#f0d040", "#fff8b0"])
    px1(a, [(14, 3), (15, 2), (14, 2)], "#6a5020")              # 꼭지
    px1(a, [(2, 9), (3, 10)], "#6a5020")
    return fin(a)


def firecracker():
    a = canvas(S, S)
    for x0 in (3, 6, 9):
        shade(a, rectm(x0, 5 + (x0 % 2), x0 + 2, 14), RED)
        px1(a, [(x0 + 1, 4 + (x0 % 2))], "#c8b070")
    paint(a, rectm(3, 9, 11, 10), "#f0c050")
    for (x, y) in [(4, 1), (7, 0), (10, 2), (12, 1)]:
        px1(a, [(x, y)], "#ffd060")
    return fin(a)


def recall():
    a = canvas(S, S)
    shade(a, rectm(3, 3, 12, 12), ["#5a6aa8", "#8aa0e0", "#d0e0ff"])
    shade(a, rectm(2, 1, 13, 3), WOOD)
    shade(a, rectm(2, 12, 13, 14), WOOD)
    for (x, y) in [(7, 5), (8, 5), (9, 6), (9, 7), (8, 8), (7, 8), (6, 7), (6, 6), (7, 6)]:
        px1(a, [(x, y)], "#ffffff")
    return fin(a)


def curse_doll():
    a = canvas(S, S)
    shade(a, ellm(4, 1, 11, 7), ["#6a5a3a", "#a8946a", "#d8c8a0"])
    shade(a, poly([(5, 7), (10, 7), (13, 10), (11, 11), (11, 15), (9, 15), (8, 12), (7, 15), (4, 15), (4, 11), (2, 10)]), ["#6a5a3a", "#a8946a", "#d8c8a0"])
    px1(a, [(6, 4), (9, 4)], "#2a1a1a")
    paint(a, linem([(6, 6), (9, 6)]), "#8a2020")
    paint(a, linem([(9, 9), (14, 4)]), "#c8c8d0")
    px1(a, [(14, 4)], "#e04040")
    paint(a, linem([(6, 10), (1, 6)]), "#c8c8d0")
    px1(a, [(1, 6)], "#40a0e0")
    return fin(a)


ICONS = {
    "rusty_sword": rusty_sword, "swift_glove": swift_glove, "berserk_helm": berserk_helm, "turtle_shell": turtle_shell,
    "feather_boots": feather_boots, "vampire_fang": vampire_fang, "thorn_armor": thorn_armor, "lucky_coin": lucky_coin,
    "storm_ring": storm_ring, "flame_charm": flame_charm, "phoenix_feather": phoenix_feather, "shadow_cloak": shadow_cloak,
    "cursed_crown": cursed_crown, "echo_bell": echo_bell, "blood_pact": blood_pact, "prank_glove": prank_glove,
    "glass_cannon": glass_cannon, "venom_fang": venom_fang, "giant_belt": giant_belt, "hourglass": hourglass,
    "ult_battery": ult_battery, "heart_locket": heart_locket,
    "potion": lambda: bottle(RED), "big_potion": lambda: bottle(["#a83a8a", "#f070c0", "#ffc0e8"], True),
    "smoke": smoke, "banana": banana, "firecracker": firecracker, "recall": recall, "curse_doll": curse_doll,
}


# ------------------------------------------------------------------ 세계 빌보드 (상자 · 진열대 · 껍질 · 상인 · 왕관)
def chest(state):
    W, H = 22, 18
    a = canvas(W, H)
    body = rectm(1, 8, 20, 17, W, H)
    shade(a, body, WOOD)
    for x in (4, 17):
        shade(a, rectm(x, 8, x + 1, 17, W, H), GOLD)
    if state == "closed":
        lid = poly([(1, 8), (1, 4), (4, 1), (17, 1), (20, 4), (20, 8)], W, H)
        shade(a, lid, ["#6a4428", "#a06a3e", "#d0985e"])
        for x in (4, 17):
            shade(a, rectm(x, 2, x + 1, 8, W, H) & lid, GOLD)
        shade(a, rectm(9, 6, 12, 10, W, H), GOLD)
        px1(a, [(10, 8), (11, 8)], "#3a2418")
    else:
        lid = poly([(2, 7), (3, 0), (18, 0), (19, 7)], W, H)
        shade(a, lid, ["#4a2e1a", "#7a5030", "#a8784a"])
        paint(a, rectm(3, 8, 18, 9, W, H), "#2a1810" if state == "open" else "#5a0e1a")
        if state == "open":
            for (x, y) in [(6, 8), (9, 7), (12, 8), (15, 7)]:
                px1(a, [(x, y)], "#ffe060")
        else:                                                  # 미믹: 이빨·혀·눈
            for x in range(3, 19, 2):
                px1(a, [(x, 7), (x, 10)], "#fff8e8")
            paint(a, rectm(8, 9, 13, 11, W, H), "#e05a7a")
            px1(a, [(6, 3), (15, 3)], "#ff3a3a")
            px1(a, [(6, 2), (15, 2)], "#ffd0d0")
    return fin(a)


def pedestal():
    W, H = 16, 14
    a = canvas(W, H)
    shade(a, rectm(4, 4, 11, 12, W, H), ["#5a5a62", "#8a8a92", "#b8b8c0"])
    shade(a, rectm(2, 2, 13, 4, W, H), ["#6a2a3a", "#a84a5a", "#e08a90"])
    shade(a, rectm(3, 12, 12, 13, W, H), ["#4a4a52", "#6a6a72", "#9a9aa2"])
    return fin(a)


def peel():
    W, H = 14, 7
    a = canvas(W, H)
    shade(a, poly([(0, 6), (2, 2), (4, 5), (6, 6), (8, 5), (10, 2), (13, 6)], W, H), ["#a8901a", "#f0d040", "#fff8b0"])
    shade(a, ellm(4, 3, 9, 6, W, H), ["#c8b060", "#f8f0c0", "#ffffff"])
    px1(a, [(2, 2), (10, 2)], "#6a5020")
    return fin(a)


def merchant(f):
    W, H = 20, 28
    a = canvas(W, H)
    bob = f
    shade(a, ellm(8, 3 + bob, 19, 20, W, H), ["#5a3a24", "#8a5a34", "#b8844e"])          # 큰 등짐
    shade(a, rectm(13, 1 + bob, 15, 4 + bob, W, H), ["#6a6a72", "#9a9aa2", "#c8c8d0"])   # 냄비
    shade(a, poly([(3, 12 + bob), (12, 12 + bob), (13, 27), (2, 27)], W, H), ["#2e4a3a", "#4a7a5a", "#7ab08a"])
    shade(a, ellm(3, 4 + bob, 12, 14 + bob, W, H), ["#2e4a3a", "#4a7a5a", "#7ab08a"])   # 두건
    paint(a, ellm(5, 7 + bob, 10, 13 + bob, W, H), "#1a1420")
    px1(a, [(6, 10 + bob), (9, 10 + bob)], "#ffe070")
    shade(a, rectm(5, 18, 9, 20, W, H), ["#c89a6a", "#e8c09a", "#fff0d0"])               # 손
    return fin(a)


def crown_hat():
    W, H = 11, 7
    a = canvas(W, H)
    shade(a, poly([(0, 6), (0, 1), (3, 3), (5, 0), (7, 3), (10, 1), (10, 6)], W, H), GOLD)
    px1(a, [(5, 4)], "#b040e0")
    return fin(a)


def main():
    items = [(k, f()) for k, f in ICONS.items()]
    at, pos = pack(items, os.path.join(OUT, "sprites", "items"), 256, keep_bottom=False, trim_cells=False)
    preview(at, "items_pv.png", 4, bg=(60, 70, 60))
    world = [("chest_closed", chest("closed")), ("chest_open", chest("open")), ("chest_mimic", chest("mimic")),
             ("pedestal", pedestal()), ("peel", peel()), ("merchant0", merchant(0)), ("merchant1", merchant(1)),
             ("crown_hat", crown_hat())]
    at2, pos2 = pack(world, os.path.join(OUT, "sprites", "loot"), 128, keep_bottom=True, trim_cells=False)
    preview(at2, "loot_pv.png", 5, bg=(60, 90, 70))
    print(len(pos), at.shape, len(pos2), at2.shape)


if __name__ == "__main__":
    main()
