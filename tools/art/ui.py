"""도트 UI 그림: 9칸 판(패널·선택 패널·키캡) · 아이콘(하트·금화·보석·직업·스킬) · 로고
→ assets/sprites/ui.png + ui.json, assets/sprites/logo.png
빛은 왼쪽 위, 외곽선은 짙은 보라/갈색 (검정 X) — 게임 화면과 같은 규칙
"""
from PIL import Image, ImageDraw, ImageFont

from px import *
from pack import pack

FONT_BIG = os.path.join(OUT, "fonts", "Galmuri11.ttf")
FONT_SMALL = os.path.join(OUT, "fonts", "Galmuri9.ttf")


def bitmap(rows, pal):
    h, w = len(rows), max(len(r) for r in rows)
    a = canvas(w, h)
    for y, r in enumerate(rows):
        for x, ch in enumerate(r):
            if ch in pal:
                a[y, x] = hx(pal[ch]) if isinstance(pal[ch], str) else pal[ch]
    return a


# ------------------------------------------------------------------ 9칸 판
def panel(w=12, h=12, fill=("#211a2c", 232), lite="#6e5c86", dark="#3a2e48", edge="#0e0b12"):
    a = canvas(w, h)
    a[1:h - 1, 1:w - 1] = hx(fill[0], fill[1])
    # 바깥 테두리 (모서리 한 칸 깎아 둥글게)
    a[0, 1:w - 1] = hx(edge)
    a[h - 1, 1:w - 1] = hx(edge)
    a[1:h - 1, 0] = hx(edge)
    a[1:h - 1, w - 1] = hx(edge)
    # 안쪽 빛 테두리: 위·왼쪽 밝게, 아래·오른쪽 어둡게
    a[1, 1:w - 1] = hx(lite)
    a[1:h - 1, 1] = hx(lite)
    a[h - 2, 2:w - 1] = hx(dark)
    a[2:h - 1, w - 2] = hx(dark)
    # 모서리 징 (밝은 점)
    for (x, y) in [(2, 2), (w - 3, 2), (2, h - 3), (w - 3, h - 3)]:
        a[y, x] = hx(lite)
    return a


def keycap(w=9, h=10):
    a = canvas(w, h)
    a[1:h - 1, 1:w - 1] = hx("#e9e1cf")
    a[h - 3:h - 1, 1:w - 1] = hx("#9a8f7c")          # 아래 두께
    a[1, 2:w - 2] = hx("#fffaf0")
    a[0, 1:w - 1] = hx("#1a1420")
    a[h - 1, 1:w - 1] = hx("#1a1420")
    a[1:h - 1, 0] = hx("#1a1420")
    a[1:h - 1, w - 1] = hx("#1a1420")
    return a


# ------------------------------------------------------------------ 아이콘
OUTL = "#24141c"

HEART = [
    ".oo...oo.",
    "oRhRoRRRo",
    "oRhRRRRro",
    "oRRRRRRro",
    ".oRRRRro.",
    "..oRRro..",
    "...oro...",
    "....o....",
]
COIN = [
    "..oooo..",
    ".oyYYyo.",
    "oyYhYYyo",
    "oYhYYYyo",
    "oYYYYYyo",
    "oyYYYyyo",
    ".oyyyyo.",
    "..oooo..",
]
GEM = [
    "..ooo..",
    ".ohCCo.",
    "ohCCCco",
    "oCCCcco",
    ".oCcco.",
    "..oco..",
    "...o...",
]


def sword(a, ox=0, oy=0, big=False):
    """대각선 칼 (손잡이 왼쪽 아래 → 끝 오른쪽 위)"""
    n = 9 if big else 8
    for k in range(n):
        x, y = ox + 5 + k, oy + 10 - k
        a[y, x] = hx("#f4f8fc")
        a[y + 1, x] = hx("#a8b6c8")
        if k > 0:
            a[y, x - 1] = hx("#d0dae6") if a[y, x - 1, 3] == 0 else a[y, x - 1]
    a[oy + 10 - n + 1, ox + 5 + n - 1] = hx("#ffffff")
    # 가로대 (금)
    for (x, y) in [(2, 9), (3, 10), (4, 11), (5, 12), (6, 13)]:
        a[oy + y - 1, ox + x + 1] = hx("#f0c050")
    a[oy + 8, ox + 3] = hx("#fff0a0")
    # 손잡이
    for (x, y) in [(3, 12), (2, 13)]:
        a[oy + y, ox + x] = hx("#8a4e2a")
    a[oy + 14, ox + 1] = hx("#f0c050")
    outline(a, OUTL)


def bomb(a, ox=0, oy=0, r=5):
    w = a.shape[1]
    cx, cy = ox + 7.5, oy + 9.0
    xs, ys = grid(a.shape[1], a.shape[0])
    m = ((xs + 0.5 - cx) ** 2 + (ys + 0.5 - cy) ** 2) <= r * r
    t = np.clip(((xs - cx) * 0.6 + (ys - cy) * 0.8) / r * 0.5 + 0.5, 0, 1)
    ramp(a, m, ["#5a6078", "#3a3e52", "#262838"], t, 0.0)
    hl = ((xs + 0.5 - (cx - 2)) ** 2 + (ys + 0.5 - (cy - 2)) ** 2) <= 1.6
    paint(a, hl & m, "#a8b0c8")
    # 꼭지 + 심지
    a[int(cy - r) - 1, int(cx) - 1:int(cx) + 2] = hx("#6a6e80")
    a[int(cy - r) - 2, int(cx) + 1] = hx("#c89a5a")
    a[int(cy - r) - 3, int(cx) + 2] = hx("#c89a5a")
    outline(a, OUTL)
    # 불꽃 (테두리 밖)
    sx, sy = int(cx) + 3, int(cy - r) - 4
    a[sy, sx] = hx("#fff4a0")
    a[sy - 1, sx] = hx("#ffb040")
    a[sy, sx + 1] = hx("#ffb040")
    a[sy + 1, sx] = hx("#ff7a30")
    a[sy, sx - 1] = hx("#ff7a30")


def cross(a, ox=0, oy=0):
    """의사: 둥근 흰 판 위 초록 십자"""
    w, h = 13, 13
    b = canvas(w, h)
    b[1:h - 1, 1:w - 1] = hx("#f4efe4")
    b[0, 2:w - 2] = hx("#f4efe4")
    b[h - 1, 2:w - 2] = hx("#f4efe4")
    b[2:h - 2, 0] = hx("#f4efe4")
    b[2:h - 2, w - 1] = hx("#f4efe4")
    b[h - 3:h - 1, 2:w - 2] = hx("#c8bfae")
    b[2:h - 2, w - 2] = hx("#c8bfae")
    b[3:10, 5:8] = hx("#4cc070")
    b[5:8, 3:10] = hx("#4cc070")
    b[3, 5:8] = hx("#a8f0b0")
    b[5, 3:5] = hx("#a8f0b0")
    b[9, 5:8] = hx("#2a8a4a")
    b[7, 8:10] = hx("#2a8a4a")
    over(a, b, ox + 1, oy + 2)
    outline(a, OUTL)


def bow(a, ox=0, oy=0):
    # 활 몸 (왼쪽으로 휜 호) + 시위 + 화살
    for y in range(2, 15):
        t = (y - 8.5) / 6.5
        x = int(round(4 + 3.2 * (t * t)))
        a[oy + y, ox + x] = hx("#c8864a")
        a[oy + y, ox + x + 1] = hx("#8a4e2a")
    a[oy + 2, ox + 7:ox + 9] = hx("#e0c090")
    a[oy + 14, ox + 7:ox + 9] = hx("#e0c090")
    for y in range(3, 14):
        a[oy + y, ox + 8] = hx("#e8e0d0")
    # 화살
    for x in range(3, 14):
        a[oy + 8, ox + x] = hx("#d8c8a8")
    a[oy + 7, ox + 13] = hx("#f4f8fc")
    a[oy + 8, ox + 14] = hx("#f4f8fc")
    a[oy + 9, ox + 13] = hx("#f4f8fc")
    a[oy + 7, ox + 2] = hx("#e05a5a")
    a[oy + 9, ox + 2] = hx("#e05a5a")
    outline(a, OUTL)


def burst_bg(a, col1="#ffd860", col2="#ff9a40"):
    """스킬 아이콘 뒤 방사형 빛 (도트 별)"""
    h, w = a.shape[:2]
    cx, cy = w / 2 - 0.5, h / 2 - 0.5
    for k in range(8):
        ang = k * math.pi / 4
        for r in range(4, 8):
            x, y = int(round(cx + math.cos(ang) * r)), int(round(cy + math.sin(ang) * r))
            if 0 <= x < w and 0 <= y < h:
                a[y, x] = hx(col1 if r < 6 else col2)


def skill_icon(job):
    a = canvas(18, 18)
    if job == "warrior":
        burst_bg(a)
        s = canvas(18, 18)
        sword(s, 0, 1, big=True)
        over(a, s, 0, 0)
    elif job == "bomber":
        burst_bg(a, "#ff9a40", "#e05a30")
        s = canvas(18, 18)
        bomb(s, 1, 2, r=5.5)
        over(a, s, 0, 0)
    elif job == "doctor":
        # 하트 + 퍼지는 물결 두 겹
        xs, ys = grid(18, 18)
        d = np.sqrt((xs - 8.5) ** 2 + (ys - 9.5) ** 2)
        paint(a, (d > 7.0) & (d < 8.0) & (ys < 15), "#58d088")
        paint(a, (d > 5.4) & (d < 6.3) & (ys < 15), "#a8f0b0")
        h = bitmap(HEART, {"o": OUTL, "R": "#4cc070", "r": "#2a8a4a", "h": "#c8ffd0"})
        over(a, h, 4, 6)
    elif job == "archer":
        for i, (x0, y0) in enumerate([(1, 1), (6, 0), (2, 7)]):
            for k in range(7):
                a[y0 + k + 2, x0 + k + 2] = hx("#d8c8a8")
            a[y0 + 9, x0 + 9] = hx("#f4f8fc")
            a[y0 + 8, x0 + 9] = hx("#f4f8fc")
            a[y0 + 9, x0 + 8] = hx("#f4f8fc")
            a[y0 + 2, x0 + 1] = hx("#e05a5a")
            a[y0 + 1, x0 + 2] = hx("#e05a5a")
        outline(a, OUTL)
    return a


def job_icon(job):
    a = canvas(18, 18)
    if job == "warrior":
        sword(a, 0, 1)
    elif job == "bomber":
        bomb(a, 1, 1, r=5)
    elif job == "doctor":
        cross(a, 1, 0)
    elif job == "archer":
        bow(a, 1, 0)
    return a


def cursor_hand():
    return bitmap([
        "oo.....",
        "oWo....",
        "oWWo...",
        "oWWWo..",
        "oWWWWo.",
        "oWWoo..",
        "ooWo...",
        "..oo...",
    ], {"o": OUTL, "W": "#fff4d6"})


def arrow_sel():
    return bitmap([
        "oo...",
        "oYo..",
        "oYYo.",
        "oYo..",
        "oo...",
    ], {"o": OUTL, "Y": "#ffd860"})


# ------------------------------------------------------------------ 로고
def text_mask(s, font_path, size):
    f = ImageFont.truetype(font_path, size)
    l, t, r, b = f.getbbox(s)
    im = Image.new("L", (r - l + 2, b - t + 2), 0)
    d = ImageDraw.Draw(im)
    d.fontmode = "1"
    d.text((1 - l, 1 - t), s, font=f, fill=255)
    return np.array(im) > 127


def logo():
    m = text_mask("세상에서 가장 강한 파티", FONT_BIG, 12)
    m = np.repeat(np.repeat(m, 2, 0), 2, 1)                  # 2배 (도트 2x2)
    m = np.pad(m, ((0, 1), (0, 1)))
    m = m | np.roll(m, 1, 1) | np.roll(m, 1, 0)                # 획을 3px 로 굵게 (오른쪽·아래로 한 칸)
    h, w = m.shape
    P = 6
    a = canvas(w + P * 2, h + P * 2 + 16)
    mm = np.zeros(a.shape[:2], bool)
    mm[P:P + h, P:P + w] = m
    # 그림자 (오른쪽 아래 2px, 짙은 자주)
    sh = nb(nb(mm, -2, 0), 0, -2) if False else np.roll(np.roll(mm, 2, 0), 2, 1)
    sh = dilate(sh, 2)
    paint(a, sh, "#1a0e18")
    # 두꺼운 외곽 (2px)
    ol = dilate(mm, 2)
    paint(a, ol, "#2a1420")
    # 글자 몸: 위 크림 → 금 → 주황 그라데이션 (가로줄 띠)
    ys = np.arange(a.shape[0])[:, None] * np.ones((1, a.shape[1]))
    t = np.clip((ys - P) / h, 0, 1)
    ramp(a, mm, ["#fff8d8", "#ffe07a", "#ffb040", "#e06a2a"], t, 0.0)
    # 위쪽 가장자리 하이라이트
    paint(a, mm & ~nb(mm, 0, -1) & (t < 0.5), "#ffffff")
    # 리본 (영문 부제)
    sub = text_mask("THE STRONGEST WARRIOR PARTY", FONT_SMALL, 10)
    sh2, sw2 = sub.shape
    rw = sw2 + 24
    rx = (a.shape[1] - rw) // 2
    ry = P + h + 5
    rb = canvas(rw, 13)
    rb[1:12, 3:rw - 3] = hx("#8a2a36")
    rb[1, 3:rw - 3] = hx("#c0485a")
    rb[11, 3:rw - 3] = hx("#5a1424")
    # 리본 끝 갈라짐
    for k in range(4):
        rb[1 + k, 0 + k // 2] = hx("#6a1e2c")
        rb[11 - k, 0 + k // 2] = hx("#6a1e2c")
        rb[1 + k, rw - 1 - k // 2] = hx("#6a1e2c")
        rb[11 - k, rw - 1 - k // 2] = hx("#6a1e2c")
    rb[5:8, 0:3] = 0
    rb[5:8, rw - 3:rw] = 0
    rb[1:12, 1:3] = np.where(rb[1:12, 1:3, 3:4] > 0, hx("#6a1e2c"), rb[1:12, 1:3])
    rb[1:12, rw - 3:rw - 1] = np.where(rb[1:12, rw - 3:rw - 1, 3:4] > 0, hx("#6a1e2c"), rb[1:12, rw - 3:rw - 1])
    outline(rb, "#2a1420")
    over(a, rb, rx, ry)
    sm = np.zeros(a.shape[:2], bool)
    sx0 = (a.shape[1] - sw2) // 2
    sm[ry + 2:ry + 2 + sh2, sx0:sx0 + sw2] = sub[:min(sh2, a.shape[0] - ry - 2)]
    paint(a, sm, "#ffe9b8")
    return a


def main():
    items = [
        ("panel", panel()),
        ("panel_hi", panel(fill=("#3a2a1e", 240), lite="#ffd27a", dark="#a8742a", edge="#1a0e08")),
        ("panel_dark", panel(fill=("#120e18", 200), lite="#3e3450", dark="#241c30", edge="#08060c")),
        ("key", keycap()),
        ("heart", bitmap(HEART, {"o": OUTL, "R": "#e8505e", "r": "#a8283c", "h": "#ffb8b8"})),
        ("heart_empty", bitmap(HEART, {"o": OUTL, "R": "#3e2a38", "r": "#2e1e2a", "h": "#4e3646"})),
        ("coin", bitmap(COIN, {"o": "#5a3a10", "Y": "#ffd860", "y": "#d09a30", "h": "#fff6c8"})),
        ("gem", bitmap(GEM, {"o": "#1c2a40", "C": "#8ad8ff", "c": "#4a90c8", "h": "#e8fbff"})),
        ("hand", cursor_hand()),
        ("sel", arrow_sel()),
    ]
    for j in ["warrior", "bomber", "doctor", "archer"]:
        items.append(("job_" + j, job_icon(j)))
        items.append(("skill_" + j, skill_icon(j)))
    at, pos = pack(items, os.path.join(OUT, "sprites", "ui"), 256, keep_bottom=False, trim_cells=False)
    preview(at, "ui_pv.png", 4, bg=(60, 70, 60))
    lg = logo()
    save(lg, os.path.join(OUT, "sprites", "logo.png"))
    preview(lg, "logo_pv.png", 3, bg=(60, 90, 70))
    print(len(pos), at.shape, lg.shape)


if __name__ == "__main__":
    main()
