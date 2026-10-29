#!/usr/bin/env python3
"""TSWP 환경음 생성기 — 맵별 배경 소리 (스테레오 OGG, 끊김 없는 반복 40초).

  forest : 나뭇잎 바람 + 새소리(좌우로 흩어짐) + 먼 벌레
  cave   : 낮은 웅웅거림 + 메아리 진 물방울 + 먼 바람 울음 + 자갈
  nest   : 축축한 저음 + 딸깍거리는 거미 소리 + 끈적한 소리 + 느린 맥박
  camp   : 밤 귀뚜라미 + 잔바람 + 가끔 부엉이
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa

OUT = os.path.join(ROOT, "assets", "ambience")
DUR = 40.0


def slow_lfo(dur, rate=0.1, depth=0.5):
    n = n_of(dur)
    x = lp(R().standard_normal(n), rate, 1)
    x = x / (np.max(np.abs(x)) + 1e-9)
    return 1 - depth * 0.5 + depth * 0.5 * x


def scatter(dur, count, make, pan_range=0.9, gain=(0.3, 1.0)):
    n = n_of(dur)
    out = np.zeros((n + n_of(4.0), 2))
    for _ in range(count):
        at = R().uniform(0, dur)
        x = make()
        pan = R().uniform(-pan_range, pan_range)
        g = R().uniform(*gain)
        o = n_of(at)
        l, r = np.cos((pan + 1) * np.pi / 4) * 1.414, np.sin((pan + 1) * np.pi / 4) * 1.414
        out[o:o + len(x), 0] += x * g * l
        out[o:o + len(x), 1] += x * g * r
    # 넘친 끝을 앞으로 접는다 (반복 이음매)
    body = out[:n].copy()
    tail = out[n:]
    body[: len(tail)] += tail
    return body


def bed(dur, color, lo, hi, lfo_rate=0.08, depth=0.6):
    st = []
    for _ in range(2):
        x = bp(noise(dur + 2, color), lo, hi)[n_of(1):n_of(1) + n_of(dur)]
        st.append(x * slow_lfo(dur, lfo_rate, depth))
    return np.stack(st, 1)


def bird():
    kind = R().integers(0, 3)
    f = R().uniform(2500, 4500)
    if kind == 0:     # 짹짹
        parts = []
        for i in range(R().integers(2, 5)):
            d = R().uniform(0.05, 0.09)
            parts.append((osc(expline(d, f * 1.2, f * 0.8), d) * env_exp(d, d * 0.4, 0.005), i * R().uniform(0.09, 0.14)))
        return mix(*parts)
    if kind == 1:     # 휘이익
        d = R().uniform(0.25, 0.4)
        pitch = f * (1 + 0.25 * np.sin(np.linspace(0, np.pi, n_of(d))))
        return osc(pitch * (1 + 0.02 * np.sin(2 * np.pi * 30 * t_of(d))), d) * adsr(d, 0.03, 0.05, 0.7, 0.1)
    d = 0.6           # 트릴
    t = t_of(d)
    return osc(f * (1 + 0.06 * np.sign(np.sin(2 * np.pi * 18 * t))), d) * adsr(d, 0.02, 0.1, 0.6, 0.2) * (0.6 + 0.4 * np.sin(2 * np.pi * 18 * t))


def forest():
    leaves = bed(DUR, "pink", 600, 6000, 0.12, 0.8) * 0.5
    wind = bed(DUR, "brown", 80, 500, 0.05, 0.7) * 0.6
    birds = scatter(DUR, 34, lambda: lp(bird(), 7000) * 0.35, 0.95, (0.15, 0.6))
    # 먼 벌레: 고음 맥동
    t = t_of(DUR)
    gate = lp((slow_lfo(DUR, 0.08, 1.0) > 0.78).astype(float), 2, 1)       # 가끔만 운다
    bug = osc(R().uniform(4800, 5600) * (1 + 0.01 * np.sin(2 * np.pi * 0.3 * t)), DUR) * (0.5 + 0.5 * np.sin(2 * np.pi * 24 * t)) ** 4 * gate * 0.006
    bugs = np.stack([bug, np.roll(bug, n_of(0.37))], 1)
    y = leaves + wind + birds + bugs
    return reverb_st(y, 0.18, 2.0, 0.6, 5000)


def cave():
    rumble = bed(DUR, "brown", 30, 160, 0.03, 0.4) * 1.2
    t = t_of(DUR)
    hum = (osc(55, DUR) * 0.25 + osc(82.5, DUR) * 0.12) * slow_lfo(DUR, 0.04, 0.6)
    howl = np.stack([sweep_bp_slow(noise(DUR, "pink"), 300, 700, 0.03, 5) for _ in range(2)], 1) * 0.35
    drips = scatter(DUR, 30, lambda: d_drip(), 0.9, (0.3, 1.0))
    pebbles = scatter(DUR, 6, lambda: bp(crackle(0.5, 60, 0.002), 1500, 6000) * env_exp(0.5, 0.15), 0.9, (0.2, 0.5))
    y = rumble + np.stack([hum, hum], 1) + howl + drips * 0.5 + pebbles * 0.4
    y = delay_st(y, 0.42, 0.35, 0.35)
    return reverb_st(y, 0.5, 3.5, 1.5, 3500, 0.05)


def d_drip():
    f = R().uniform(900, 2000)
    d = 0.08
    return osc(expline(d, f, f * 1.8), d) * env_exp(d, 0.018)


def sweep_bp_slow(x, lo, hi, rate, q):
    n = len(x)
    lfo = slow_lfo(len(x) / SR, rate, 1.0)
    fs = lo + (hi - lo) * np.clip(lfo, 0, 1)
    return svf(x, fs, q, "bp") * (0.4 + 0.6 * lfo)


def delay_st(y, t, fb, wet):
    out = y.copy()
    d = n_of(t)
    tap = y.copy()
    for k in range(5):
        tap = np.roll(tap, d, 0) * (fb if k else 1)
        tap = np.stack([lp(tap[:, 0], 3000, 1), lp(tap[:, 1], 3000, 1)], 1)
        out += tap[:, ::-1] * wet   # 좌우를 바꿔가며 핑퐁
    return out


def nest():
    low = bed(DUR, "brown", 30, 220, 0.04, 0.5) * 1.0
    t = t_of(DUR)
    drone = (osc(69.3, DUR, "saw") * 0.5 + osc(69.3 * 1.006, DUR, "saw") * 0.5)
    drone = lp(drone, 260) * 0.25 * slow_lfo(DUR, 0.05, 0.8)
    clicks = scatter(DUR, 140, lambda: mix(*[(modal(0.02, [(R().uniform(2000, 4500), 0.003, 1.0)]), i * R().uniform(0.02, 0.05)) for i in range(R().integers(2, 7))]) * 0.3, 0.95, (0.1, 0.5))
    goo = scatter(DUR, 12, lambda: sweep_bp(noise(0.5, "pink"), R().uniform(200, 400), R().uniform(600, 1200), 4) * adsr(0.5, 0.05, 0.1, 0.6, 0.2) * 0.6, 0.8, (0.2, 0.6))
    beat = np.zeros(n_of(DUR))
    hb = mix(osc(expline(0.25, 60, 38), 0.25) * env_exp(0.25, 0.08), (osc(expline(0.25, 55, 36), 0.25) * env_exp(0.25, 0.08) * 0.7, 0.2))
    step = 1.5
    for k in range(int(DUR / step)):
        o = n_of(k * step)
        beat[o:o + len(hb)] += hb[: len(beat) - o]
    y = low + np.stack([drone, drone], 1) + clicks + goo + np.stack([beat, beat], 1) * 0.35
    return reverb_st(y, 0.45, 3.5, 1.6, 2800, 0.05)


def camp():
    t = t_of(DUR)
    def cricket():
        f = R().uniform(4200, 5200)
        d = R().uniform(0.25, 0.5)
        tt = t_of(d)
        return osc(f, d) * (np.sin(2 * np.pi * R().uniform(28, 40) * tt) > 0.2) * adsr(d, 0.01, 0.05, 0.8, 0.05) * 0.12
    crickets = scatter(DUR, 120, cricket, 0.95, (0.1, 0.7))
    wind = bed(DUR, "pink", 200, 2500, 0.06, 0.8) * 0.25
    def owl():
        parts = []
        for i, (f, d) in enumerate([(420, 0.35), (380, 0.25), (400, 0.6)]):
            parts.append((formant(osc(f * (1 + 0.01 * np.sin(2 * np.pi * 5 * t_of(d))), d, "tri"), "u", 1.0) * adsr(d, 0.06, 0.1, 0.7, 0.15), i * 0.45))
        return mix(*parts) * 0.6
    owls = scatter(DUR, 2, owl, 0.8, (0.3, 0.5))
    y = crickets + wind + owls
    return reverb_st(y, 0.25, 2.2, 0.8, 5000)


def main(names):
    os.makedirs(OUT, exist_ok=True)
    for n in names or ["forest", "cave", "nest", "camp"]:
        seed({"forest": 11, "cave": 12, "nest": 13, "camp": 14}[n])
        y = globals()[n]()
        y = y[: n_of(DUR)]
        y = loopify(y, 1.5)
        save_ogg(os.path.join(OUT, n + ".ogg"), y, -6.0, 4)
        print(n, flush=True)


if __name__ == "__main__":
    main(sys.argv[1:])
