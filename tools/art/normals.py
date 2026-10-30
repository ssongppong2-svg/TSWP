"""스프라이트 노멀맵 자동 생성 (2.5D 조명용)

높이 = (테두리에서 안쪽으로 얼마나 들어왔나 → 둥근 돔) + (밝기 → 잔결)
법선 = 높이의 기울기. 결과는 같은 배치의 <이름>_n.png (RGB = 법선 0..1, A = 원본 알파)
 · 화면 기준: x 오른쪽, y 위, z 화면 밖 (카메라 쪽)
 · 도트 느낌을 지키려고 기울기는 픽셀 단위로 계산하고 부드럽게 흐리지 않는다

python normals.py            → assets/sprites/*.png, assets/tex/*.png 모두
"""
import glob
import os
import sys

import numpy as np
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def dist_inside(m, maxd=12):
    """알파 안쪽 거리 (체스판 거리, 단계별 침식)"""
    d = np.zeros(m.shape, np.float32)
    cur = m.copy()
    for k in range(1, maxd + 1):
        n = cur.copy()
        n[1:, :] &= cur[:-1, :]
        n[:-1, :] &= cur[1:, :]
        n[:, 1:] &= cur[:, :-1]
        n[:, :-1] &= cur[:, 1:]
        if k % 2 == 0:                      # 십자·네모를 번갈아 → 팔각형 (마름모 무늬 방지)
            n[1:, 1:] &= cur[:-1, :-1]
            n[:-1, :-1] &= cur[1:, 1:]
            n[1:, :-1] &= cur[:-1, 1:]
            n[:-1, 1:] &= cur[1:, :-1]
        n[0, :] = n[-1, :] = False
        n[:, 0] = n[:, -1] = False
        d += cur
        cur = n
        if not cur.any():
            break
    return d


def blur3(h):
    p = np.pad(h, 1, mode="edge")
    return (p[:-2, :-2] + p[:-2, 1:-1] + p[:-2, 2:] + p[1:-1, :-2] + p[1:-1, 1:-1] * 2 + p[1:-1, 2:] + p[2:, :-2] + p[2:, 1:-1] + p[2:, 2:]) / 10.0


def sprite_normals(img, dome=1.0, detail=0.6, tile=False):
    a = np.asarray(img.convert("RGBA")).astype(np.float32) / 255.0
    alpha = a[..., 3] > 0.5
    lum = a[..., 0] * 0.3 + a[..., 1] * 0.59 + a[..., 2] * 0.11
    if tile:
        h = lum * detail * 3.0
        hp = np.pad(h, 1, mode="wrap")
    else:
        d = dist_inside(alpha, 18)
        # 돔: 안쪽 거리의 제곱근 (가장자리에서 급하게, 가운데는 완만) + 밝기 잔결 (칠한 덩어리마다 볼록)
        h = np.sqrt(np.minimum(d, 18.0) / 18.0) * 5.0 * dome + lum * detail * 4.0
        h = blur3(h) * 0.5 + h * 0.5
        h[~alpha] = 0.0
        hp = np.pad(h, 1, mode="edge")
    dx = (hp[1:-1, 2:] - hp[1:-1, :-2]) * 0.5
    dy = (hp[2:, 1:-1] - hp[:-2, 1:-1]) * 0.5      # 아래로 갈수록 +
    nx = -dx
    ny = dy                                        # 화면 위가 +y
    nz = np.ones_like(nx) * 1.0
    l = np.sqrt(nx * nx + ny * ny + nz * nz)
    n = np.stack([nx / l, ny / l, nz / l], -1)
    out = np.zeros(a.shape, np.float32)
    out[..., :3] = n * 0.5 + 0.5
    out[..., 3] = a[..., 3]
    if not tile:
        out[~alpha, :3] = (0.5, 0.5, 1.0)
    return Image.fromarray((out * 255).astype(np.uint8), "RGBA")


def main():
    done = []
    for p in sorted(glob.glob(os.path.join(ROOT, "assets", "sprites", "*.png"))):
        if p.endswith("_n.png"):
            continue
        name = os.path.basename(p)
        leafy = name.startswith(("flora", "landmarks"))
        sprite_normals(Image.open(p), dome=1.3 if leafy else 1.0, detail=1.1 if leafy else 0.6).save(p[:-4] + "_n.png")
        done.append(name)
    for p in sorted(glob.glob(os.path.join(ROOT, "assets", "tex", "*.png"))):
        if p.endswith("_n.png"):
            continue
        sprite_normals(Image.open(p), detail=0.9, tile=True).save(p[:-4] + "_n.png")
        done.append(os.path.basename(p))
    print("normals:", ", ".join(done))


if __name__ == "__main__":
    main()
