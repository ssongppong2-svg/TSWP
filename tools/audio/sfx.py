#!/usr/bin/env python3
"""TSWP 효과음 생성기 — 44.1kHz 모노 WAV, 소리마다 변주 여러 개.

사용: python3 tools/audio/sfx.py [이름 …]   (프로젝트 루트에서, 이름 없으면 전부)
결과: assets/sfx/<이름>.wav  또는  <이름>_1.wav … (변주)

설계: 소리는 '층'으로 만든다 — 순간(클릭/트랜지언트) + 몸통(음색) + 꼬리(잔향/잔해).
게임 엔진이 공간 울림(동굴·둥지)을 따로 얹으므로 여기서는 아주 짧은 잔향만 쓴다.
"""
import glob
import zlib
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa

OUT = os.path.join(ROOT, "assets", "sfx")
REG = {}


def snd(name, variants=1):
    def deco(fn):
        REG[name] = (fn, variants)
        return fn
    return deco


def r(a, b):
    return R().uniform(a, b)


def tick(f=3000, d=0.004):
    return modal(d * 8, [(f, d, 1.0), (f * 1.7, d * 0.6, 0.5)])


def thump(f0=120, f1=45, dur=0.18, decay=0.06):
    return osc(expline(dur, f0, f1, 0.6), dur) * env_exp(dur, decay, 0.001)


def whoosh(dur, f0, f1, q=1.6, color="pink"):
    x = noise(dur, color)
    y = sweep_bp(x, f0, f1, q)
    e = np.sin(np.pi * np.linspace(0, 1, len(y)) ** 0.7) ** 1.5
    return y * e


def metal(f, dur=0.6, bright=1.0):
    ratios = [1.0, 2.76, 5.4, 8.93, 13.34]
    return modal(dur, [(f * k, dur * (0.5 / (1 + i * 0.6)), bright ** i / (1 + i)) for i, k in enumerate(ratios)])


def wood(f=220, dur=0.25):
    return modal(dur, [(f, 0.05, 1.0), (f * 2.57, 0.03, 0.6), (f * 4.1, 0.015, 0.35), (f * 6.3, 0.01, 0.2)])


def gore(dur=0.18, f=600):
    """찰싹/퍽 — 몸에 맞는 둔탁한 질감"""
    x = noise(dur, "pink")
    x = bp(x, f * 0.4, f * 2.2) * env_exp(dur, 0.035)
    return mix(x, (thump(160, 60, dur, 0.04), 0, 0.8))


def chime(f, dur=0.8, idx=1.2):
    e = env_exp(dur, dur * 0.35, 0.002)
    return fm(f, 3.5, idx, dur, env_exp(dur, dur * 0.1)) * e


def bell(f, dur=1.2):
    return modal(dur, [(f, dur * 0.45, 1.0), (f * 2.0, dur * 0.3, 0.35), (f * 2.76, dur * 0.2, 0.3), (f * 5.4, dur * 0.1, 0.15), (f * 0.5, dur * 0.5, 0.2)])


def notes(seq, step, voice):
    parts = []
    for i, f in enumerate(seq):
        if f:
            parts.append((voice(f), i * step))
    return mix(*parts)


def creature(f0, f1, dur, vowel="a", rough=0.3, shift=1.0, vib=6.0):
    """짐승 울음: 톱니 + 거친 떨림 → 모음 필터"""
    n = n_of(dur)
    pitch = expline(dur, f0, f1, 0.8) * (1 + 0.03 * np.sin(2 * np.pi * vib * t_of(dur)))
    pitch *= 1 + rough * 0.15 * lp(R().standard_normal(n), 30, 1) * 8
    src = osc(pitch, dur, "saw") + rough * noise(dur, "pink")
    am = 1 + rough * 0.6 * np.sin(2 * np.pi * r(25, 45) * t_of(dur))
    y = formant(src * am, vowel, shift)
    return drive(y * 2.5, 1.5)


# ================================================================== UI
@snd("ui_click", 3)
def _(v):
    f = [2100, 2350, 1950][v]
    return mix(wood(f * 0.2, 0.08) * 0.5, tick(f, 0.003), (tick(f * 1.5, 0.002), 0.012, 0.4))


@snd("ui_hover", 3)
def _(v):
    return mix(wood([520, 560, 600][v], 0.06) * 0.6, tick(3400 + v * 200, 0.002) * 0.3)


@snd("ui_confirm")
def _(v):
    return mix(notes([523, 784, 1047], 0.06, lambda f: chime(f, 0.5, 0.8)), (wood(260, 0.1), 0, 0.3))


@snd("ui_back")
def _(v):
    return mix(notes([659, 440], 0.06, lambda f: chime(f, 0.35, 0.6)), (wood(200, 0.1), 0, 0.3))


@snd("ui_toggle", 2)
def _(v):
    return mix(tick(2600 if v == 0 else 1900, 0.003), (wood(300 if v == 0 else 240, 0.08), 0.0, 0.7))


@snd("ui_tick", 2)
def _(v):
    return mix(wood(880 + v * 60, 0.1) * 0.7, tick(4000, 0.002) * 0.4)


@snd("ui_start")
def _(v):
    seq = [392, 523, 659, 784, 1047]
    a = notes(seq, 0.07, lambda f: chime(f, 0.9, 1.0))
    b = notes([196, 0, 262], 0.14, lambda f: ks(f, 0.8, 0.6, 0.995) * 0.6)
    return reverb(mix(a, b, (whoosh(0.5, 300, 4000, 1.2), 0, 0.15)), 0.3, 1.4, 0.35)


@snd("ui_denied")
def _(v):
    a = osc(185, 0.22, "saw") * 0.4 + osc(196, 0.22, "saw") * 0.4
    return mix(lp(a, 1600) * adsr(0.22, 0.005, 0.05, 0.7, 0.08), (wood(140, 0.15), 0, 0.4))


@snd("ui_toast")
def _(v):
    return notes([880, 1175], 0.05, lambda f: chime(f, 0.4, 0.5))


# ================================================================== 움직임
@snd("step_grass", 6)
def _(v):
    d = 0.12
    x = bp(noise(d, "white"), 1800, 7000) * env_exp(d, 0.02)
    cr = bp(crackle(d, 900, 0.0008), 2500, 9000) * env_exp(d, 0.04)
    return mix(x * 0.7, cr * 0.8, (thump(r(90, 120), 50, 0.08, 0.02), 0, 0.5))


@snd("step_stone", 6)
def _(v):
    d = 0.1
    click = bp(noise(0.02), 1500, 6000) * env_exp(0.02, 0.004)
    grit = bp(crackle(d, 400, 0.0006), 2000, 8000) * env_exp(d, 0.03) * 0.5
    return mix(click, grit, (thump(r(110, 150), 60, 0.08, 0.018), 0, 0.7), (wood(r(380, 520), 0.06), 0, 0.15))


@snd("step_wood", 5)
def _(v):
    return mix(wood(r(150, 210), 0.18), (bp(noise(0.03), 800, 3000) * env_exp(0.03, 0.006), 0, 0.5), (thump(100, 50, 0.1, 0.03), 0, 0.5))


@snd("jump", 4)
def _(v):
    w = whoosh(0.16, 500, 2400, 1.2) * 0.8
    cloth = bp(noise(0.08, "pink"), 900, 4000) * env_exp(0.08, 0.02)
    return mix(cloth, (w, 0.01), (thump(90, 60, 0.07, 0.02), 0, 0.5))


@snd("double_jump", 3)
def _(v):
    w = whoosh(0.22, 700, 4500, 1.5)
    sparkle = notes([1320 + v * 80, 1760 + v * 100], 0.035, lambda f: chime(f, 0.25, 0.5) * 0.3)
    return mix(w, (sparkle, 0.02))


@snd("land", 4)
def _(v):
    d = 0.16
    dust = bp(noise(d, "pink"), 300, 3000) * env_exp(d, 0.03)
    return mix((thump(r(110, 140), 45, 0.16, 0.045), 0, 1.0), dust * 0.6, (bp(crackle(d, 300), 2000, 7000) * env_exp(d, 0.05), 0, 0.3))


@snd("land_heavy", 2)
def _(v):
    d = 0.4
    boom = thump(90, 32, d, 0.12)
    debris = bp(crackle(d, 500, 0.001), 1500, 7000) * env_exp(d, 0.12)
    dust = lp(noise(d, "brown"), 900) * env_exp(d, 0.1)
    return reverb(mix(boom * 1.2, debris * 0.5, dust * 0.6), 0.12, 0.6, 0.15)


@snd("dash", 3)
def _(v):
    w = whoosh(0.28, 3500, 500, 1.8) * 1.2
    air = hp(noise(0.28), 4000) * env_exp(0.28, 0.06) * 0.3
    return mix(w, air, (thump(140, 70, 0.08, 0.02), 0, 0.4))


@snd("climb", 4)
def _(v):
    return mix(wood(r(240, 320), 0.1) * 0.8, (bp(noise(0.06, "pink"), 1000, 4000) * env_exp(0.06, 0.015), 0.005, 0.5))


@snd("splash", 3)
def _(v):
    d = 0.45
    body = bp(noise(d, "white"), 600, 6000) * env_exp(d, 0.09)
    drops = notes([r(900, 1400) for _ in range(5)], 0.045, lambda f: osc(expline(0.05, f, f * 2.2), 0.05) * env_exp(0.05, 0.012) * 0.4)
    return mix(body, (drops, 0.06), (thump(120, 60, 0.1, 0.03), 0, 0.6))


# ================================================================== 전투 (플레이어)
@snd("swing", 5)
def _(v):
    d = r(0.17, 0.22)
    w = whoosh(d, r(400, 600), r(3500, 5000), 2.4)
    edge = hp(whoosh(d, 2000, 9000, 3.0), 3000) * 0.25
    return mix(w, edge)


@snd("sword_hit", 5)
def _(v):
    ring = metal(r(1500, 2100), 0.25, 0.8) * 0.35
    crack = bp(noise(0.05), 1500, 9000) * env_exp(0.05, 0.008)
    body = gore(0.16, r(500, 800))
    return mix(crack * 1.2, body * 0.9, (ring, 0.002))


@snd("smash", 3)
def _(v):
    d = 0.6
    boom = thump(150, 35, d, 0.16) * 1.4
    crunch = bp(crackle(d, 700, 0.0012), 800, 6000) * env_exp(d, 0.1)
    ring = metal(r(500, 650), 0.5, 0.6) * 0.25
    return reverb(mix(boom, crunch * 0.7, ring, (whoosh(0.1, 3000, 600), 0, 0.4)), 0.15, 0.7, 0.2)


@snd("throw", 4)
def _(v):
    return mix(whoosh(0.18, 900, 3000, 2.0), (bp(noise(0.04), 1500, 5000) * env_exp(0.04, 0.01), 0, 0.5))


@snd("bomb_bounce", 4)
def _(v):
    return mix(wood(r(160, 220), 0.12) * 0.8, metal(r(700, 900), 0.12, 0.5) * 0.2, (thump(140, 80, 0.06, 0.015), 0, 0.6))


@snd("fuse")
def _(v):
    d = 0.7
    hiss = bp(noise(d), 3000, 9000) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 9 * t_of(d))))
    sp = bp(crackle(d, 600, 0.0005), 3000, 10000)
    e = adsr(d, 0.02, 0.1, 0.8, 0.15)
    return mix((hiss * 0.6 + sp) * e, (tick(2500, 0.003), 0, 0.3))


@snd("fuse_loop")
def _(v):
    d = 2.0
    hiss = bp(noise(d), 3000, 9000) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 9 * t_of(d))))
    return loopify(hiss * 0.6 + bp(crackle(d, 600, 0.0005), 3000, 10000), 0.3)


def _boom(scale=1.0, dur=1.4):
    boom = thump(110 * scale, 28, dur, 0.25 * scale) * 1.5
    body = lp(noise(dur, "brown"), 1400) * env_exp(dur, 0.22 * scale)
    crack = bp(noise(0.08), 900, 9000) * env_exp(0.08, 0.012)
    debris = bp(crackle(dur, 260, 0.0015), 1500, 8000) * env_exp(dur, 0.35 * scale)
    x = mix(crack * 1.5, boom, body * 1.2, (debris * 0.5, 0.05))
    return drive(x * 1.6, 1.4)


@snd("explode_s", 4)
def _(v):
    return reverb(_boom(r(0.85, 1.1), 0.9), 0.2, 1.0, 0.25, 4000)


@snd("explode_l", 2)
def _(v):
    x = _boom(1.4, 2.2)
    sub = osc(expline(1.6, 55, 28), 1.6) * env_exp(1.6, 0.5)
    return reverb(mix(x, sub * 0.8), 0.3, 1.8, 0.5, 3500)


@snd("syringe", 3)
def _(v):
    return mix(whoosh(0.12, 2000, 6000, 3.0) * 0.8, (tick(4200, 0.002), 0.0, 0.4))


@snd("inject", 4)
def _(v):
    pf = osc(expline(0.12, r(700, 900), 300), 0.12) * env_exp(0.12, 0.03) * 0.6
    squish = bp(noise(0.1), 600, 2500) * env_exp(0.1, 0.03)
    return mix(squish, pf, (chime(r(1500, 1700), 0.3, 0.4), 0.03, 0.25))


@snd("heal_pulse", 2)
def _(v):
    d = 1.2
    pad = sum(osc(f, d, "tri") * 0.25 for f in [523, 659, 784, 1047]) * adsr(d, 0.05, 0.2, 0.5, 0.8)
    shim = notes([1568, 2093, 2637, 3136], 0.05, lambda f: chime(f, 0.6, 0.6) * 0.35)
    swell = whoosh(0.6, 400, 5000, 1.0) * 0.2
    return reverb(mix(pad, shim, swell), 0.35, 1.5, 0.45)


@snd("heal_get", 3)
def _(v):
    return reverb(notes([784 + v * 40, 1175 + v * 50], 0.06, lambda f: chime(f, 0.45, 0.6)), 0.25, 0.8, 0.3)


@snd("hurt", 5)
def _(v):
    hit = gore(0.15, r(400, 700))
    # 귀여운 '윽' — 짧은 모음
    voice = creature(r(380, 480), r(260, 320), 0.14, "u", 0.15, r(1.3, 1.6), 0) * env_exp(0.14, 0.05) * 0.35
    return mix(hit, (voice, 0.01))


@snd("die", 2)
def _(v):
    voice = creature(420, 160, 0.5, "a", 0.2, 1.4, 5) * adsr(0.5, 0.01, 0.1, 0.7, 0.2) * 0.4
    fall = mix(thump(110, 40, 0.3, 0.08), (bp(noise(0.2, "pink"), 300, 2500) * env_exp(0.2, 0.05), 0, 0.5))
    tone = notes([392, 330, 262, 196], 0.12, lambda f: ks(f, 0.6, 0.4, 0.993) * 0.35)
    return reverb(mix(voice, (fall, 0.25), (tone, 0.1)), 0.25, 1.0, 0.3)


@snd("revive", 2)
def _(v):
    up = notes([262, 330, 392, 523, 659, 784], 0.07, lambda f: chime(f, 0.8, 0.9) * 0.6)
    swell = whoosh(0.8, 200, 6000, 0.9) * 0.4
    choir = formant(supersaw(expline(1.2, 262, 262), 1.2, 5, 0.01), "a", 1.2) * adsr(1.2, 0.3, 0.2, 0.6, 0.5) * 0.4
    return reverb(mix(swell, up, choir), 0.4, 1.8, 0.5)


def _item(seq, dur, sparkle=0):
    a = notes(seq, 0.06, lambda f: chime(f, dur, 1.0) * 0.6)
    b = notes(seq, 0.06, lambda f: ks(f / 2, dur, 0.7, 0.997) * 0.3)
    parts = [a, b]
    if sparkle:
        sp = notes([R().choice([2093, 2349, 2637, 3136, 3520]) for _ in range(sparkle)], 0.04, lambda f: chime(f, 0.4, 0.4) * 0.25)
        parts.append((sp, 0.15))
    return reverb(mix(*parts), 0.3, 1.2, 0.35)


@snd("item_c")
def _(v):
    return _item([659, 880], 0.4)


@snd("item_u")
def _(v):
    return _item([587, 784, 988], 0.5)


@snd("item_r")
def _(v):
    return _item([523, 659, 784, 1047], 0.6, 3)


@snd("item_e")
def _(v):
    return _item([440, 554, 659, 880, 1109], 0.8, 6)


@snd("item_l")
def _(v):
    a = _item([392, 494, 587, 784, 988, 1175], 1.0, 10)
    choir = formant(supersaw(392, 1.6, 7, 0.012), "a", 1.1) * adsr(1.6, 0.2, 0.3, 0.6, 0.8) * 0.35
    return reverb(mix(a, (choir, 0.1), (bell(196, 1.6) * 0.4, 0)), 0.3, 1.8, 0.5)


@snd("coin", 4)
def _(v):
    f = r(1900, 2300)
    return mix(metal(f, 0.25, 0.7) * 0.6, (metal(f * 1.33, 0.3, 0.7) * 0.5, 0.05))


@snd("emote", 3)
def _(v):
    seq = [[523, 659], [659, 523, 784], [784, 988]][v]
    return notes(seq, 0.05, lambda f: mix(wood(f / 2, 0.15) * 0.5, chime(f, 0.3, 0.3) * 0.4))


@snd("ping", 2)
def _(v):
    return reverb(notes([1320, 1760] if v == 0 else [1175, 1568], 0.08, lambda f: chime(f, 0.5, 0.7)), 0.3, 1.0, 0.3)


# ================================================================== 장치
@snd("chest", 2)
def _(v):
    creak = sweep_bp(noise(0.35, "white") * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 38 * t_of(0.35)))), 500, 900, 6) * env_exp(0.35, 0.15) * 0.5
    clunk = mix(wood(130, 0.25), thump(120, 50, 0.15, 0.05))
    shine = _item([523, 659, 784, 1047, 1319], 0.7, 4)
    return mix(creak, (clunk, 0.28), (shine, 0.35, 0.8))


@snd("lever", 2)
def _(v):
    return mix(metal(r(380, 460), 0.2, 0.5) * 0.5, (wood(160, 0.15), 0.06), (tick(1800, 0.003), 0.08, 0.5), (thump(110, 50, 0.12, 0.04), 0.07, 0.6))


@snd("plate", 2)
def _(v):
    return mix(thump(r(150, 180), 60, 0.15, 0.04), (wood(r(110, 130), 0.2), 0.0, 0.8), (bp(crackle(0.1, 300), 2000, 7000) * env_exp(0.1, 0.03), 0.01, 0.4))


def _gate(up=True):
    d = 0.9
    rattle = bp(crackle(d, 160, 0.003), 800, 4000) * adsr(d, 0.05, 0.2, 0.7, 0.2)
    chain = sum(metal(R().uniform(900, 1500), 0.12, 0.5) * 0.1 for _ in range(1))
    grind = sweep_bp(noise(d, "pink"), 300 if up else 500, 500 if up else 300, 3) * adsr(d, 0.05, 0.1, 0.8, 0.2) * 0.5
    end = mix(metal(320, 0.5, 0.6) * 0.5, thump(130, 50, 0.2, 0.06))
    return reverb(mix(rattle, grind, (chain, 0.1), (end, d - 0.05)), 0.2, 0.9, 0.25)


@snd("gate_open")
def _(v):
    return _gate(True)


@snd("gate_close")
def _(v):
    return _gate(False)


@snd("crate_push", 3)
def _(v):
    d = 0.3
    scrape = bp(noise(d, "white") * (0.6 + 0.4 * np.sin(2 * np.pi * r(25, 40) * t_of(d))), 300, 1800) * adsr(d, 0.01, 0.05, 0.7, 0.1)
    return mix(scrape, wood(r(120, 150), 0.2) * 0.6)


@snd("crate_break", 3)
def _(v):
    parts = [(wood(R().uniform(200, 800), 0.18), R().uniform(0, 0.09), R().uniform(0.3, 0.7)) for _ in range(7)]
    crack = bp(noise(0.06), 1000, 8000) * env_exp(0.06, 0.01)
    return mix(crack, thump(140, 50, 0.2, 0.05), *parts)


@snd("wall_crumble", 2)
def _(v):
    d = 1.6
    rumble = lp(noise(d, "brown"), 400) * adsr(d, 0.02, 0.3, 0.6, 0.9)
    rocks = [(mix(thump(R().uniform(90, 200), 40, 0.2, 0.05), bp(noise(0.05), 1500, 6000) * env_exp(0.05, 0.01) * 0.4), R().uniform(0, 1.0), R().uniform(0.3, 0.9)) for _ in range(14)]
    crack = bp(noise(0.1), 800, 7000) * env_exp(0.1, 0.02)
    return reverb(mix(crack * 1.4, rumble, *rocks), 0.25, 1.4, 0.4, 3500)


@snd("shroom", 3)
def _(v):
    d = 0.35
    boing = osc(expline(d, r(180, 220), r(500, 600), 0.5) * (1 + 0.05 * np.sin(2 * np.pi * 22 * t_of(d))), d, "tri") * env_exp(d, 0.12)
    return mix(thump(120, 60, 0.1, 0.03), boing * 0.8, (bp(noise(0.05, "pink"), 300, 1500) * env_exp(0.05, 0.015), 0, 0.5))


@snd("bridge", 4)
def _(v):
    d = 0.4
    creak = sweep_bp(noise(d) * (0.4 + 0.6 * (np.sin(2 * np.pi * r(30, 55) * t_of(d)) > 0.3)), r(400, 600), r(250, 350), 8) * adsr(d, 0.04, 0.1, 0.7, 0.15)
    return mix(creak, wood(r(140, 180), 0.12) * 0.3)


@snd("thorns", 2)
def _(v):
    return mix(bp(crackle(0.25, 900, 0.0005), 2500, 9000) * env_exp(0.25, 0.08), gore(0.15, 900) * 0.6)


@snd("troll", 2)
def _(v):
    buzz = lp(osc(110, 0.6, "square") * 0.5 + osc(116, 0.6, "square") * 0.5, 2500) * adsr(0.6, 0.005, 0.05, 0.8, 0.1)
    honk = notes([392, 370, 349, 262], 0.16, lambda f: lp(osc(f * (1 + 0.02 * np.sin(2 * np.pi * 7 * t_of(0.3))), 0.3, "saw"), 2200) * adsr(0.3, 0.01, 0.05, 0.7, 0.1) * 0.5)
    return reverb(mix(buzz * 0.6, (honk, 0.55)), 0.2, 0.8, 0.25)


@snd("alarm")
def _(v):
    horn = lambda f: formant(supersaw(f, 0.7, 5, 0.008), "o", 1.0) * adsr(0.7, 0.05, 0.1, 0.8, 0.3)
    drum = thump(90, 40, 0.3, 0.1)
    return reverb(mix(horn(220) * 0.8, (horn(330) * 0.5, 0.0), drum, (drum, 0.18, 0.7)), 0.3, 1.4, 0.4)


@snd("checkpoint")
def _(v):
    return reverb(mix(bell(523, 1.4) * 0.6, (bell(784, 1.2) * 0.4, 0.08), (whoosh(0.6, 300, 3000, 1.0) * 0.3, 0)), 0.35, 1.6, 0.5)


@snd("crystal_ping", 3)
def _(v):
    f = [1568, 1760, 2093][v]
    return reverb(mix(modal(1.4, [(f, 0.5, 1.0), (f * 2.41, 0.3, 0.4), (f * 3.87, 0.2, 0.25), (f * 0.5, 0.4, 0.2)])), 0.4, 1.6, 0.5)


@snd("totem", 2)
def _(v):
    drum = lambda: mix(thump(110, 55, 0.4, 0.12), wood(90, 0.3) * 0.5)
    chant = formant(supersaw(147, 1.0, 5, 0.01), "o", 0.9) * adsr(1.0, 0.1, 0.2, 0.6, 0.4) * 0.4
    return reverb(mix(drum(), (drum(), 0.2, 0.8), (chant, 0.1), (bell(294, 1.0) * 0.4, 0.35)), 0.35, 1.5, 0.45)


@snd("crown_get")
def _(v):
    fan = notes([523, 659, 784, 1047, 784, 1047], 0.08, lambda f: lp(supersaw(f, 0.4, 3, 0.004), 4000) * adsr(0.4, 0.01, 0.1, 0.6, 0.2) * 0.5)
    return reverb(mix(fan, (bell(1047, 1.2) * 0.4, 0.4), (_item([1568, 2093], 0.6, 6), 0.4, 0.5)), 0.3, 1.6, 0.45)


@snd("crown_drop", 2)
def _(v):
    return mix(metal(r(900, 1100), 0.6, 0.8) * 0.6, (metal(r(1100, 1300), 0.4, 0.7) * 0.4, 0.12), (metal(1200, 0.3, 0.6) * 0.3, 0.2), notes([784, 659, 523], 0.08, lambda f: chime(f, 0.4, 0.4) * 0.3))


# ================================================================== 적
@snd("telegraph", 3)
def _(v):
    d = 0.35
    rise = osc(expline(d, r(500, 600), r(1100, 1300)), d, "tri") * adsr(d, 0.03, 0.05, 0.7, 0.05) * 0.5
    sh = hp(whoosh(d, 1000, 6000, 2), 1500) * 0.3
    return mix(rise, sh)


@snd("spawn", 3)
def _(v):
    d = 0.5
    pop = mix(bp(noise(0.08, "pink"), 300, 3000) * env_exp(0.08, 0.02), thump(200, 80, 0.12, 0.03))
    rumble = lp(noise(d, "brown"), 600) * adsr(d, 0.15, 0.1, 0.5, 0.2) * 0.6
    return mix(rumble, (pop, 0.3))


@snd("acorn_swing", 3)
def _(v):
    return mix(whoosh(0.16, 500, 2200, 1.6), (wood(r(220, 280), 0.1) * 0.4, 0))


@snd("acorn_hurt", 4)
def _(v):
    return mix(wood(r(180, 260), 0.15), gore(0.12, 500) * 0.5, (creature(r(500, 650), r(380, 450), 0.1, "i", 0.2, 1.6, 0) * env_exp(0.1, 0.04) * 0.3, 0.01))


@snd("acorn_die", 2)
def _(v):
    shell = [(wood(R().uniform(200, 500), 0.15), R().uniform(0, 0.12), 0.5) for _ in range(5)]
    squeak = creature(700, 300, 0.3, "i", 0.2, 1.8, 7) * adsr(0.3, 0.01, 0.05, 0.6, 0.1) * 0.3
    return mix(squeak, thump(130, 50, 0.2, 0.06), *shell)


@snd("shroom_shoot", 3)
def _(v):
    d = 0.22
    pf = osc(expline(d, r(350, 420), 140), d) * env_exp(d, 0.05)
    puff = bp(noise(d, "pink"), 400, 3000) * env_exp(d, 0.06)
    return mix(pf * 0.8, puff)


@snd("shroom_hurt", 4)
def _(v):
    sq = bp(noise(0.14, "pink"), 300, 1800) * env_exp(0.14, 0.04)
    return mix(sq, osc(expline(0.12, r(300, 380), 180), 0.12, "tri") * env_exp(0.12, 0.04) * 0.6)


@snd("shroom_die", 2)
def _(v):
    d = 0.6
    deflate = osc(expline(d, 300, 70), d, "tri") * env_exp(d, 0.2) * 0.6
    spores = bp(noise(d), 2000, 7000) * adsr(d, 0.05, 0.1, 0.4, 0.3) * 0.4
    return mix(deflate, spores, thump(110, 50, 0.2, 0.05))


@snd("spider_skitter", 4)
def _(v):
    d = 0.35
    ticks = [(tick(R().uniform(2500, 4500), 0.002), i * R().uniform(0.025, 0.04), R().uniform(0.3, 0.8)) for i in range(9)]
    return mix(*ticks)


@snd("spider_bite", 3)
def _(v):
    hiss = bp(noise(0.12), 2500, 8000) * env_exp(0.12, 0.03)
    snap = mix(tick(2200, 0.004), (tick(1800, 0.004), 0.03))
    return mix(hiss * 0.7, snap, (gore(0.12, 700) * 0.5, 0.03))


@snd("spider_hurt", 4)
def _(v):
    sc = creature(r(900, 1200), r(700, 900), 0.12, "i", 0.4, 2.2, 0) * env_exp(0.12, 0.05) * 0.4
    return mix(gore(0.12, 900), sc)


@snd("spider_die", 2)
def _(v):
    sc = creature(1200, 400, 0.4, "e", 0.5, 2.2, 0) * adsr(0.4, 0.005, 0.05, 0.6, 0.2) * 0.4
    curl = mix(*[(tick(R().uniform(1500, 3000), 0.003), i * 0.04 + 0.1, 0.4) for i in range(6)])
    return mix(sc, curl, gore(0.2, 500) * 0.6)


@snd("spore_hit", 3)
def _(v):
    return mix(bp(noise(0.2, "pink"), 500, 4000) * env_exp(0.2, 0.05), osc(expline(0.08, 500, 200), 0.08) * env_exp(0.08, 0.02) * 0.5)


@snd("web_shot", 3)
def _(v):
    d = 0.25
    sp = bp(noise(d), 1500, 6000) * env_exp(d, 0.08)
    tw = osc(expline(d, 900, 300), d, "tri") * env_exp(d, 0.06) * 0.3
    return mix(sp, tw)


@snd("web_hit", 3)
def _(v):
    d = 0.3
    stick = bp(noise(d, "pink"), 300, 2000) * env_exp(d, 0.08)
    sq = sweep_bp(noise(d), 2000, 800, 4) * env_exp(d, 0.1) * 0.6
    return mix(stick, sq)


# ================================================================== 보스 (부화 여왕)
def _roar(d, f0, f1, vowel="a", shift=0.55):
    a = creature(f0, f1, d, vowel, 0.6, shift, 4)
    b = creature(f0 * 1.5, f1 * 1.4, d, "o", 0.7, shift * 1.6, 5) * 0.5
    hiss = bp(noise(d), 2500, 8000) * 0.25
    e = adsr(d, 0.08, 0.2, 0.75, d * 0.35)
    sub = osc(expline(d, f0 / 2, f1 / 2), d) * 0.4
    return drive((a + b + hiss + sub) * e * 1.4, 1.3)


@snd("q_roar")
def _(v):
    return reverb(_roar(1.8, 140, 85), 0.35, 2.2, 0.6, 3500)


@snd("q_screech", 2)
def _(v):
    d = 0.9
    a = creature(r(700, 800), r(500, 600), d, "e", 0.7, 1.4, 9) * adsr(d, 0.02, 0.1, 0.7, 0.3)
    return reverb(drive(a * 1.5 + bp(noise(d), 3000, 9000) * 0.2 * adsr(d, 0.02, 0.1, 0.7, 0.3), 1.3), 0.3, 1.6, 0.45)


@snd("q_step", 4)
def _(v):
    return mix(thump(r(80, 100), 35, 0.3, 0.09) * 1.2, (tick(r(1200, 1800), 0.004) * 0.5, 0), (bp(crackle(0.2, 200), 1500, 5000) * env_exp(0.2, 0.05), 0.01, 0.3))


@snd("q_slam", 2)
def _(v):
    d = 1.4
    boom = thump(80, 25, d, 0.3) * 1.6
    crack = bp(noise(0.1), 700, 8000) * env_exp(0.1, 0.02)
    debris = bp(crackle(d, 300, 0.002), 1200, 6000) * env_exp(d, 0.35)
    return reverb(drive(mix(crack * 1.4, boom, debris * 0.6, (whoosh(0.25, 3000, 400), 0, 0.5)) * 1.4, 1.3), 0.3, 1.8, 0.5, 3000)


@snd("q_charge")
def _(v):
    d = 1.2
    windup = creature(110, 160, d, "o", 0.6, 0.6, 3) * adsr(d, 0.3, 0.2, 0.8, 0.2)
    scrape = bp(noise(d), 1000, 4000) * adsr(d, 0.5, 0.1, 0.6, 0.2) * 0.4
    return reverb(mix(windup, scrape), 0.25, 1.4, 0.4)


@snd("q_bite", 2)
def _(v):
    snap = mix(tick(1600, 0.006), (tick(1300, 0.006), 0.04))
    return mix(gore(0.25, 400) * 1.2, snap, (_roar(0.35, 220, 160, "a", 0.7) * 0.5, 0))


@snd("q_hiss")
def _(v):
    d = 1.2
    h = bp(noise(d), 2500, 9000) * adsr(d, 0.4, 0.1, 0.9, 0.2)
    rattle = h * (0.6 + 0.4 * np.sign(np.sin(2 * np.pi * 28 * t_of(d))))
    return mix(rattle, creature(300, 420, d, "i", 0.5, 1.0, 12) * adsr(d, 0.5, 0.1, 0.6, 0.2) * 0.35)


@snd("q_burst")
def _(v):
    d = 1.8
    gas = lp(noise(d, "pink"), 3500) * adsr(d, 0.01, 0.3, 0.5, 1.0)
    pop = _boom(1.2, 1.4)
    bub = mix(*[(osc(expline(0.08, R().uniform(200, 500), R().uniform(700, 1200)), 0.08) * env_exp(0.08, 0.02) * 0.3, R().uniform(0.1, 1.2)) for _ in range(18)])
    return reverb(mix(pop, gas * 0.8, bub), 0.3, 1.8, 0.5)


@snd("q_cocoon", 2)
def _(v):
    d = 0.7
    wet = sweep_bp(noise(d, "pink"), 300, 1200, 3) * adsr(d, 0.05, 0.2, 0.6, 0.3)
    return mix(wet, (gore(0.2, 400) * 0.6, 0.5))


@snd("q_hatch", 3)
def _(v):
    crack = mix(*[(tick(R().uniform(1500, 3000), 0.003), R().uniform(0, 0.12), 0.6) for _ in range(5)])
    goo = bp(noise(0.3, "pink"), 300, 1500) * env_exp(0.3, 0.08)
    sq = creature(1000, 1400, 0.15, "i", 0.4, 2.2, 0) * env_exp(0.15, 0.05) * 0.4
    return mix(crack, (goo, 0.08), (sq, 0.12))


@snd("q_shield")
def _(v):
    d = 1.4
    hum = sum(osc(f, d, "tri") * 0.3 for f in [110, 165, 220]) * adsr(d, 0.3, 0.2, 0.7, 0.6)
    crys = mix(*[(metal(R().uniform(1200, 2400), 0.6, 0.6) * 0.2, i * 0.06) for i in range(8)])
    return reverb(mix(hum, crys, whoosh(0.8, 200, 2000, 1.0) * 0.4), 0.35, 1.8, 0.5)


@snd("q_shatter")
def _(v):
    d = 1.6
    glass = mix(*[(metal(R().uniform(1500, 4500), R().uniform(0.2, 0.7), 0.7) * 0.25, R().uniform(0, 0.4)) for _ in range(20)])
    crack = bp(noise(0.12), 1500, 10000) * env_exp(0.12, 0.02)
    boom = thump(100, 30, 0.8, 0.2)
    scream = creature(600, 300, 1.0, "a", 0.6, 1.0, 6) * adsr(1.0, 0.02, 0.1, 0.6, 0.4) * 0.6
    return reverb(mix(crack * 1.5, boom, glass, (scream, 0.15)), 0.35, 2.0, 0.6)


@snd("q_weak", 3)
def _(v):
    sq = gore(0.2, 600) * 1.2
    shriek = creature(r(800, 950), r(600, 700), 0.25, "e", 0.6, 1.5, 0) * env_exp(0.25, 0.1) * 0.6
    sparkle = chime(r(1500, 1800), 0.3, 1.5) * 0.3
    return mix(sq, (shriek, 0.02), sparkle)


@snd("q_clink", 3)
def _(v):
    return mix(metal(r(1400, 1900), 0.2, 0.6) * 0.5, tick(3000, 0.003), wood(r(300, 400), 0.08) * 0.4)


@snd("q_open")
def _(v):
    d = 0.9
    clunk = mix(thump(120, 50, 0.2, 0.06), metal(260, 0.4, 0.6) * 0.4)
    hum = osc(expline(d, 220, 440), d, "tri") * adsr(d, 0.1, 0.1, 0.6, 0.3) * 0.4
    return reverb(mix(clunk, (hum, 0.1), (chime(880, 0.6, 1.0) * 0.4, 0.5)), 0.3, 1.4, 0.4)


@snd("q_wallhit")
def _(v):
    return reverb(mix(_boom(0.9, 1.0), (bp(crackle(1.2, 200, 0.002), 1000, 5000) * env_exp(1.2, 0.4), 0.05, 0.6), (_roar(0.6, 200, 130) * 0.4, 0.1)), 0.3, 1.6, 0.5)


@snd("q_die")
def _(v):
    d = 3.2
    death = _roar(2.4, 160, 45, "o", 0.5) * 1.0
    collapse = mix(*[(thump(R().uniform(70, 120), 30, 0.4, 0.12), 1.2 + i * R().uniform(0.12, 0.25), R().uniform(0.5, 1.0)) for i in range(6)])
    choir = formant(supersaw(220, 2.0, 7, 0.01), "a", 1.0) * adsr(2.0, 0.6, 0.3, 0.6, 0.8) * 0.35
    return reverb(mix(death, collapse, (choir, 1.4), (bell(220, 2.0) * 0.4, 1.6)), 0.4, 2.6, 0.8, 3000)


# ================================================================== 반복음
@snd("fire_loop")
def _(v):
    d = 6.0
    roar = lp(noise(d, "brown"), 500) * (0.7 + 0.3 * lp(R().standard_normal(n_of(d)), 3, 1) * 30)
    pops = bp(crackle(d, 40, 0.002), 800, 6000) * 1.5
    hiss = bp(noise(d), 2000, 7000) * 0.15
    return loopify(roar * 0.6 + pops + hiss, 0.5)


@snd("bubble_loop")
def _(v):
    d = 6.0
    b = mix(*[(osc(expline(0.06, R().uniform(250, 600), R().uniform(700, 1400)), 0.06) * env_exp(0.06, 0.015), R().uniform(0, d - 0.1), R().uniform(0.2, 0.8)) for _ in range(40)])
    bed = lp(noise(d, "pink"), 600) * 0.15
    return loopify(fit(b, n_of(d)) + bed, 0.4)


@snd("water_loop")
def _(v):
    d = 6.0
    drips = mix(*[(osc(expline(0.05, f, f * 1.8), 0.05) * env_exp(0.05, 0.012), R().uniform(0, d - 0.1), R().uniform(0.2, 0.6)) for f in R().uniform(900, 1800, 18)])
    flow = bp(noise(d, "pink"), 400, 3000) * 0.25
    return loopify(fit(drips, n_of(d)) + flow, 0.4)


@snd("crystal_loop")
def _(v):
    d = 8.0
    t = t_of(d)
    hum = sum(osc(f, d) * a for f, a in [(523, 0.2), (784, 0.15), (1047, 0.1), (1568, 0.06)])
    hum *= 0.7 + 0.3 * np.sin(2 * np.pi * 0.25 * t)
    return loopify(hum, 0.5)


# ================================================================== 실행
def build(names=None):
    os.makedirs(OUT, exist_ok=True)
    targets = names or list(REG)
    if not names:
        for f in glob.glob(os.path.join(OUT, "*.wav")) + glob.glob(os.path.join(OUT, "*.wav.import")):
            os.remove(f)
    for i, name in enumerate(targets):
        fn, nv = REG[name]
        for f in glob.glob(os.path.join(OUT, f"{name}.wav*")) + glob.glob(os.path.join(OUT, f"{name}_[0-9].wav*")):
            os.remove(f)
        for v in range(nv):
            seed(zlib.crc32(f"{name}:{v}".encode()))
            x = fn(v)
            loop = name.endswith("_loop")
            path = os.path.join(OUT, f"{name}.wav" if nv == 1 else f"{name}_{v + 1}.wav")
            save_wav(path, x, -1.0, do_trim=not loop)
        print(f"[{i + 1}/{len(targets)}] {name} ×{nv}", flush=True)


if __name__ == "__main__":
    build(sys.argv[1:] or None)
