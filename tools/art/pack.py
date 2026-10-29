"""그림 여러 장을 아틀라스 한 장으로 + 좌표 json"""
import json
import os

import numpy as np

from px import canvas, over, save, OUT


def trim(a):
    m = a[..., 3] > 0
    if not m.any():
        return a[:1, :1]
    ys, xs = np.nonzero(m)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def trim_keep_bottom(a):
    """좌우·위만 자른다 (발 위치 = 그림 아래 가운데 유지: 좌우는 가운데 기준 대칭으로)"""
    m = a[..., 3] > 0
    if not m.any():
        return a[:1, :1]
    ys, xs = np.nonzero(m)
    h, w = a.shape[:2]
    cx = w / 2
    half = int(np.ceil(max(cx - xs.min(), xs.max() + 1 - cx)))
    x0 = int(max(0, np.floor(cx - half)))
    x1 = int(min(w, np.ceil(cx + half)))
    return a[ys.min():ys.max() + 1, x0:x1]


def pack(items, path, width=512, keep_bottom=True, trim_cells=True):
    """items: [(이름, 캔버스)] → path.png + path.json
    trim_cells=False 면 그림 크기를 그대로 둔다 (애니메이션 프레임처럼 발 위치가 같아야 할 때)"""
    cells = []
    for name, a in items:
        if not trim_cells:
            cells.append((name, a))
        else:
            cells.append((name, trim_keep_bottom(a) if keep_bottom else trim(a)))
    cells.sort(key=lambda it: -it[1].shape[0])
    x = y = rowh = 0
    pos = {}
    for name, a in cells:
        h, w = a.shape[:2]
        if x + w + 1 > width:
            x = 0
            y += rowh + 1
            rowh = 0
        pos[name] = [x, y, w, h]
        x += w + 1
        rowh = max(rowh, h)
    H = y + rowh + 1
    H = 1 << (H - 1).bit_length()
    at = canvas(width, H)
    for name, a in cells:
        p = pos[name]
        at[p[1]:p[1] + p[3], p[0]:p[0] + p[2]] = a
    save(at, path + ".png")
    json.dump(pos, open(path + ".json", "w"), ensure_ascii=False)
    return at, pos
