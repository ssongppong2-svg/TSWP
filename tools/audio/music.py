#!/usr/bin/env python3
"""TSWP 음악 생성기 — 맵별 테마 + 전투 겹(stem) + 짧은 조각(stinger).

사용: python3 tools/audio/music.py [트랙 …]
결과: assets/music/<트랙>.ogg (44.1kHz 스테레오, 끊김 없는 반복)

모든 곡은 하나의 '파티 테마'(라이트모티프)를 공유한다:
    D  A A G F E | F G A C B A | G F E D C E | D ———   (D 도리안)
맵마다 선법·악기·빠르기를 바꿔 같은 노래가 다른 표정을 짓는다.
  메뉴   D 도리안 · 하프+플루트 · 84
  캠프   F 장조 자장가 · 기타+칼림바 · 88
  숲     D 도리안 · 피치카토+마림바+플루트 · 100   (전투: 북+현 오스티나토+금관)
  동굴   E 프리지안 · 첼레스타+물방울+저음 드론 · 80 (전투: 큰북+떨림 현)
  둥지   C# 단조 · 합창+오르골 · 76                  (전투: 거미발 16분 피치카토+톰)
  보스   D 단조 · 오케스트라 · 140                    (2페이즈: 합창+금관 겹)
  왕관   D 장조 우스꽝 팡파르 · 128
  결과   D 장조 승리 · 90 / 패배 D 단조 오르골 · 66
"""
import os
import sys
import zlib

import numpy as np
from scipy import signal

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from dsp import *  # noqa

OUT = os.path.join(ROOT, "assets", "music")
NOTE = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(name, shift=0):
    """'C#4' → Hz"""
    letter = name[0]
    rest = name[1:]
    acc = 0
    while rest and rest[0] in "#b":
        acc += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    octave = int(rest)
    midi = 12 * (octave + 1) + NOTE[letter] + acc + shift
    return 440.0 * 2 ** ((midi - 69) / 12)


def parse(seq):
    """'D4:1 A4:.5 r:1 [D3,F3,A3]:2' → [(beats, dur, [notes] or None)]"""
    out = []
    t = 0.0
    for tok in seq.split():
        n, d = tok.rsplit(":", 1)
        d = float(d)
        if n == "r":
            out.append((t, d, None))
        elif n.startswith("["):
            out.append((t, d, n[1:-1].split(",")))
        else:
            out.append((t, d, [n]))
        t += d
    return out, t


# ================================================================== 악기 (모노, 길이는 꼬리 포함)
def i_harp(f, d, v):
    return ks_fast(f, d + 1.6, 0.55, 0.9985 if f < 400 else 0.997) * v


def i_guitar(f, d, v):
    x = ks_fast(f, d + 1.0, 0.45, 0.997)
    return peak(x, 2500, 1.0, -4) * v


def i_pizz(f, d, v):
    x = ks_fast(f, 0.45, 0.35, 0.985)
    return lp(x, 3000) * env_exp(0.45, 0.12) * v


def i_marimba(f, d, v):
    return modal(0.9, [(f, 0.35, 1.0), (f * 3.93, 0.06, 0.35), (f * 9.2, 0.02, 0.15)]) * v


def i_kalimba(f, d, v):
    return modal(1.4, [(f, 0.7, 1.0), (f * 5.4, 0.08, 0.35), (f * 2.01, 0.25, 0.1)]) * v


def i_celesta(f, d, v):
    return modal(1.8, [(f, 0.9, 1.0), (f * 2.0, 0.35, 0.3), (f * 3.0, 0.15, 0.12), (f * 4.2, 0.05, 0.1)]) * v


def i_musicbox(f, d, v):
    return modal(2.2, [(f, 1.2, 1.0), (f * 2.0, 0.5, 0.25), (f * 5.1, 0.15, 0.2), (f * 7.3, 0.05, 0.1)]) * v


def i_flute(f, d, v):
    dur = d + 0.15
    t = t_of(dur)
    vib = 1 + 0.006 * np.sin(2 * np.pi * 5.2 * t) * np.clip((t - 0.25) / 0.3, 0, 1)
    x = osc(f * vib, dur) + 0.22 * osc(2 * f * vib, dur) + 0.06 * osc(3 * f * vib, dur)
    breath = bp(noise(dur, "white"), f * 1.5, f * 4) * 0.12
    e = adsr(dur, 0.05, 0.1, 0.85, 0.14)
    return (x + breath) * e * v


def i_pad(f, d, v, cutoff=2200):
    dur = d + 0.9
    x = supersaw(f, dur, 5, 0.01)
    x = lp(x, cutoff, 2)
    return x * adsr(dur, min(0.5, d * 0.4), 0.3, 0.8, 0.9) * v


def i_trem(f, d, v):
    dur = d + 0.3
    x = lp(supersaw(f, dur, 4, 0.008), 2600)
    tr = 0.55 + 0.45 * (np.sin(2 * np.pi * 12 * t_of(dur)) > 0)
    tr = lp(tr, 60, 1)
    return x * tr * adsr(dur, 0.05, 0.1, 0.9, 0.25) * v


def i_stacc(f, d, v):
    dur = 0.35
    x = lp(supersaw(f, dur, 3, 0.006), 2200)
    return x * adsr(dur, 0.004, 0.08, 0.35, 0.12) * v


def i_choir(f, d, v, vowel="a"):
    dur = d + 1.0
    t = t_of(dur)
    vib = 1 + 0.008 * np.sin(2 * np.pi * 4.8 * t + R().random() * 6)
    x = formant(supersaw(f * vib, dur, 6, 0.012), vowel, 1.0 if f > 200 else 0.85, 6)
    return x * adsr(dur, 0.35, 0.3, 0.8, 1.0) * v * 2.2


def i_brass(f, d, v):
    dur = d + 0.25
    x = osc(f, dur, "saw") + osc(f * 1.004, dur, "saw") * 0.8
    bright = lp(x, 3800, 2)
    dark = lp(x, 1000, 2)
    w = np.clip(t_of(dur) / 0.25, 0, 1)
    y = bright * (1 - w) * 0.9 + dark * (0.25 + 0.75 * w)
    y = drive(y * 1.4, 1.2)
    return y * adsr(dur, 0.03, 0.15, 0.75, 0.2) * v


def i_bass(f, d, v):
    dur = d + 0.12
    x = osc(f, dur) + 0.35 * lp(osc(f, dur, "saw"), 700)
    return x * adsr(dur, 0.006, 0.12, 0.75, 0.1) * v


def i_drone(f, d, v):
    dur = d + 1.0
    x = osc(f, dur, "saw") * 0.5 + osc(f * 1.003, dur, "saw") * 0.5 + osc(f / 2, dur) * 0.6
    x = lp(x, 500)
    return x * adsr(dur, 1.0, 0.3, 0.9, 1.0) * v


def i_bell(f, d, v):
    dur = 3.0
    return modal(dur, [(f, 1.4, 1.0), (f * 2.0, 0.8, 0.35), (f * 2.76, 0.5, 0.3), (f * 5.4, 0.2, 0.15), (f * 0.5, 1.5, 0.2)]) * v


# ---- 타악
def d_kick(v):
    return osc(expline(0.35, 140, 42, 0.5), 0.35) * env_exp(0.35, 0.12, 0.001) * v


def d_taiko(v):
    body = osc(expline(0.6, 110, 55, 0.5), 0.6) * env_exp(0.6, 0.2, 0.001)
    skin = bp(noise(0.08, "pink"), 200, 2500) * env_exp(0.08, 0.02)
    return (body + fit(skin, len(body)) * 0.5) * v


def d_snare(v):
    tone = osc(expline(0.15, 240, 180), 0.15) * env_exp(0.15, 0.04)
    sn = bp(noise(0.22), 1500, 9000) * env_exp(0.22, 0.06)
    return (fit(tone, len(sn)) * 0.6 + sn) * v


def d_hat(v, open_=False):
    d = 0.25 if open_ else 0.06
    return hp(noise(d), 7000) * env_exp(d, 0.08 if open_ else 0.015) * v


def d_shaker(v):
    return bp(noise(0.09), 4000, 11000) * adsr(0.09, 0.02, 0.03, 0.4, 0.03) * v


def d_frame(v):
    body = osc(expline(0.4, 160, 90), 0.4) * env_exp(0.4, 0.12)
    slap = bp(noise(0.05, "pink"), 400, 3000) * env_exp(0.05, 0.012)
    return (body + fit(slap, len(body)) * 0.6) * v


def d_tom(v, f=140):
    return osc(expline(0.4, f * 1.3, f, 0.4), 0.4) * env_exp(0.4, 0.14) * v


def d_timp(v, f=73):
    return modal(1.4, [(f, 0.6, 1.0), (f * 1.5, 0.4, 0.4), (f * 1.99, 0.3, 0.25)]) * v + fit(bp(noise(0.05, "pink"), 100, 1000) * env_exp(0.05, 0.015), n_of(1.4)) * 0.4 * v


def d_crash(v):
    d = 2.0
    return hp(noise(d), 4000) * env_exp(d, 0.6) * v * 0.6


def d_swell(v, dur=2.0):
    return hp(noise(dur), 3000) * np.linspace(0, 1, n_of(dur)) ** 3 * v * 0.5


def d_drip(v):
    f = R().uniform(1200, 2200)
    return osc(expline(0.06, f, f * 1.9), 0.06) * env_exp(0.06, 0.015) * v


def d_click(v):
    f = R().uniform(2500, 4000)
    return modal(0.03, [(f, 0.004, 1.0)]) * v


def d_heart(v):
    a = osc(expline(0.25, 70, 40), 0.25) * env_exp(0.25, 0.08)
    return mix(a, (a * 0.7, 0.18)) * v


DRUMS = {"k": d_kick, "t": d_taiko, "s": d_snare, "h": d_hat, "o": lambda v: d_hat(v, True), "x": d_shaker,
         "f": d_frame, "m": d_tom, "M": lambda v: d_tom(v, 95), "T": d_timp, "C": d_crash, "w": d_swell,
         "d": d_drip, "c": d_click, "H": d_heart}


# ================================================================== 곡 만들기
class Song:
    def __init__(self, name, bpm, bars, beats_per_bar=4, seed_=0, verb=(2.6, 0.9, 5000, 0.03), loop=True):
        self.name = name
        self.bpm = bpm
        self.spb = 60.0 / bpm
        self.beats = bars * beats_per_bar
        self.bpb = beats_per_bar
        self.len_s = self.beats * self.spb
        self.loop = loop
        self.tail = 4.0
        n = n_of(self.len_s + self.tail + 3.0)
        self.layers = {"base": np.zeros((n, 2)), "stem": np.zeros((n, 2))}
        self.sends = {"base": np.zeros((n, 2)), "stem": np.zeros((n, 2))}
        self.verb = verb
        seed(zlib.crc32(name.encode()) + seed_)

    def sec(self, beat):
        return beat * self.spb

    def put(self, x, beat, gain=1.0, pan=0.0, send=0.25, layer="base", humanize=0.008):
        at = self.sec(beat) + (R().uniform(-humanize, humanize) if humanize else 0)
        at = max(0.0, at)
        o = n_of(at) if at > 0 else 0
        buf = self.layers[layer]
        sb = self.sends[layer]
        n = len(buf)
        if o >= n:
            return
        seg = x[: n - o] * gain
        l, r = np.cos((pan + 1) * np.pi / 4) * 1.4142, np.sin((pan + 1) * np.pi / 4) * 1.4142
        buf[o:o + len(seg), 0] += seg * l
        buf[o:o + len(seg), 1] += seg * r
        if send > 0:
            sb[o:o + len(seg), 0] += seg * l * send
            sb[o:o + len(seg), 1] += seg * r * send

    def line(self, inst, seq, start_bar=0, gain=1.0, pan=0.0, send=0.25, layer="base", shift=0, vel_jitter=0.12, legato=1.0, repeat=1, every=None):
        ev, total = parse(seq)
        step = every if every else total
        for rep in range(repeat):
            base = start_bar * self.bpb + rep * step
            for t, d, ns in ev:
                if ns is None:
                    continue
                for nn in ns:
                    f = hz(nn, shift)
                    v = 1.0 + R().uniform(-vel_jitter, vel_jitter)
                    x = inst(f, d * self.spb * legato, v)
                    self.put(x, base + t, gain / max(1, len(ns) ** 0.5), pan, send, layer)

    def drums(self, pattern, start_bar=0, bars=1, gain=1.0, pan=0.0, send=0.12, layer="base", steps_per_beat=4, accent=None, swing=0.0):
        """pattern: 'k...s...k.k.s...' 마디 하나(16분 기준), 여러 악기는 리스트"""
        pats = pattern if isinstance(pattern, list) else [pattern]
        for p in pats:
            for b in range(bars):
                for i, ch in enumerate(p.replace(" ", "")):
                    if ch == ".":
                        continue
                    beat = (start_bar + b) * self.bpb + i / steps_per_beat
                    if swing and i % 2 == 1:
                        beat += swing / steps_per_beat
                    v = 1.0 if (accent is None or i % accent == 0) else 0.72
                    v *= 1 + R().uniform(-0.1, 0.1)
                    fn = DRUMS[ch]
                    self.put(fn(v), beat, gain, pan + R().uniform(-0.05, 0.05), send, layer, humanize=0.004)

    def chords(self, inst, prog, start_bar=0, beats_each=4, gain=1.0, pan=0.0, send=0.4, layer="base", shift=0, legato=1.0):
        """prog: ['D3,F3,A3', 'G2,B2,D3', …] 마디마다 화음"""
        for i, ch in enumerate(prog):
            if ch in ("", "r"):
                continue
            notes = ch.split(",")
            for nn in notes:
                f = hz(nn, shift)
                x = inst(f, beats_each * self.spb * legato, 1.0)
                self.put(x, start_bar * self.bpb + i * beats_each, gain / len(notes) ** 0.5, pan + R().uniform(-0.25, 0.25), send, layer, humanize=0.0)

    def arp(self, inst, prog, pattern, start_bar=0, rate=0.5, gain=1.0, pan=0.0, send=0.3, layer="base", shift=0, beats_each=4, octave_up=False):
        """화음을 pattern(인덱스 목록)에 따라 펼쳐 친다"""
        for i, ch in enumerate(prog):
            if ch in ("", "r"):
                continue
            notes = ch.split(",")
            steps = int(beats_each / rate)
            for s in range(steps):
                idx = pattern[s % len(pattern)]
                if idx is None:
                    continue
                nn = notes[idx % len(notes)]
                sh = shift + (12 if octave_up and idx >= len(notes) else 0)
                x = inst(hz(nn, sh), rate * self.spb, 1.0 + R().uniform(-0.12, 0.12))
                self.put(x, start_bar * self.bpb + i * beats_each + s * rate, gain, pan, send, layer)

    def render(self):
        out = {}
        dur, decay, bright, pd = self.verb
        L = n_of(self.len_s)
        T = n_of(self.tail + 3.0)
        for k in ("base", "stem"):
            dry = self.layers[k]
            if not np.any(dry):
                continue
            wet = reverb_st(self.sends[k], 1.0, dur, decay, bright, pd)
            wet = wet[: len(dry)] - fit2(self.sends[k], len(dry))  # reverb_st 는 dry 포함 → 잔향만
            y = dry + fit2(wet, len(dry)) * 0.9
            if self.loop:
                body = y[:L].copy()
                tail = y[L:L + T]
                body[: len(tail)] += tail       # 끝을 앞으로 접어 이음매 없는 반복
                y = body
            else:
                y = trim(y, 1e-4, 0.1)
            out[k] = y
        return out


def fit2(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros((n - len(x), x.shape[1]))])


def glue(x, thr_db=-14.0, ratio=2.5):
    """버스 압축 (느린 RMS) — 소리를 한 덩어리로"""
    m = np.max(np.abs(x), 1)
    env = signal.lfilter([0.0015], [1, -0.9985], m ** 2) ** 0.5
    db = 20 * np.log10(env + 1e-9)
    over = np.maximum(0, db - thr_db)
    g = 10 ** (-(over - over / ratio) / 20)
    return x * g[:, None]


def write(song, target_rms=-18.0):
    layers = song.render()
    base = layers["base"]
    stem = layers.get("stem")
    base = glue(base)
    if stem is not None:
        stem = glue(fit2(stem, len(base)))
        full = base + stem
    else:
        full = base
    rms = np.sqrt(np.mean(base ** 2)) + 1e-9
    g = min(10 ** (target_rms / 20) / rms, 10 ** (-1.0 / 20) / (np.max(np.abs(full)) + 1e-9))
    save_ogg(os.path.join(OUT, song.name + ".ogg"), np.clip(base * g, -1, 1), peak_db=20 * np.log10(np.max(np.abs(base * g)) + 1e-9))
    if stem is not None:
        name = STEM_NAME.get(song.name, song.name + "_combat")
        save_ogg(os.path.join(OUT, name + ".ogg"), np.clip(stem * g, -1, 1), peak_db=20 * np.log10(np.max(np.abs(stem * g)) + 1e-9))
    print(f"  {song.name}: {song.len_s:.1f}s  gain {20*np.log10(g):+.1f}dB", flush=True)


STEM_NAME = {"boss": "boss_p2"}

# ================================================================== 라이트모티프
MOTIF = "D4:.75 A4:.25 A4:1 G4:.5 F4:.5 E4:1 F4:.5 G4:.5 A4:.5 C5:.5 B4:1 A4:1 G4:.5 F4:.5 E4:.5 D4:.5 C4:1 E4:1 D4:4"
MOTIF_B = "F4:1 G4:.5 A4:.5 Bb4:1 A4:1 G4:.5 A4:.5 C5:1 G4:2 A4:1 F4:.5 E4:.5 D4:1 F4:1 E4:3 r:1"
MOTIF_B_END = "A4:1 F4:.5 E4:.5 D4:1 C#4:1 D4:4"


def to_major(seq):
    """D 도리안 → D 장조 (F→F#, C→C#, Bb→B)"""
    toks = []
    for tok in seq.split():
        n, d = tok.rsplit(":", 1)
        if n != "r":
            if n.startswith("Bb"):
                n = "B" + n[2:]
            elif n[0] in "FC" and n[1] not in "#b":
                n = n[0] + "#" + n[1:]
        toks.append(f"{n}:{d}")
    return " ".join(toks)


def to_minor(seq):
    """D 도리안 → D 자연단조 (B→Bb)"""
    toks = []
    for tok in seq.split():
        n, d = tok.rsplit(":", 1)
        if n != "r" and n[0] == "B" and n[1] not in "#b":
            n = "Bb" + n[1:]
        toks.append(f"{n}:{d}")
    return " ".join(toks)


def stretch(seq, k):
    toks = []
    for tok in seq.split():
        n, d = tok.rsplit(":", 1)
        toks.append(f"{n}:{float(d) * k:g}")
    return " ".join(toks)


# 화음 (D 도리안 i–IV–VII–i / VI–VII–i–V)
PROG_A = ["D3,F3,A3", "G2,B2,D3", "C3,E3,G3", "D3,F3,A3"]
PROG_B = ["Bb2,D3,F3", "C3,E3,G3", "D3,F3,A3", "A2,C#3,E3"]
BASS_A = ["D2", "G1", "C2", "D2"]
BASS_B = ["Bb1", "C2", "D2", "A1"]


def bass_line(s, roots, start_bar, pattern, inst=i_bass, gain=0.8, layer="base", shift=0):
    """pattern: [(박, 길이, 반음)] 한 마디"""
    for i, r in enumerate(roots):
        for b, d, st in pattern:
            f = hz(r, shift + st)
            s.put(inst(f, d * s.spb, 1.0), (start_bar + i) * s.bpb + b, gain, 0, 0.05, layer)


# ================================================================== 곡들
def song_menu():
    s = Song("menu", 84, 16, verb=(3.0, 1.1, 5500, 0.03))
    for sec, prog, bass in [(0, PROG_A, BASS_A), (4, PROG_A, BASS_A), (8, PROG_B, BASS_B), (12, PROG_A, BASS_A)]:
        s.arp(i_harp, prog, [0, 1, 2, 3, 2, 1, 0, 2], sec, 0.5, 0.42, -0.25, 0.4, octave_up=True, shift=12)
        s.chords(i_pad, prog, sec, 4, 0.22, 0.0, 0.5)
        bass_line(s, bass, sec, [(0, 2, 0), (2, 1.5, 7), (3.5, 0.5, 12)], gain=0.5)
    s.line(i_flute, MOTIF, 4, 0.5, 0.2, 0.45, shift=12)
    s.line(i_flute, MOTIF_B, 8, 0.45, 0.2, 0.45, shift=12)
    s.line(i_flute, MOTIF, 12, 0.5, 0.2, 0.45, shift=12)
    s.line(i_celesta, MOTIF, 12, 0.2, -0.4, 0.5, shift=24)
    s.drums(["f...........f...", "........x.x.x..."], 4, 12, 0.35)
    s.drums("w...............", 3, 1, 0.3)
    s.drums("w...............", 11, 1, 0.3)
    s.drums("T...............", 4, 1, 0.5)
    s.drums("T...............", 12, 1, 0.5)
    return s


def song_camp():
    s = Song("camp", 88, 16, verb=(2.0, 0.7, 4500, 0.02))
    PROG = ["F2,A2,C3,F3", "Bb2,D3,F3,Bb3", "C3,E3,G3,C4", "F2,A2,C3,F3", "D3,F3,A3,D4", "Bb2,D3,F3,Bb3", "G2,Bb2,D3,G3", "C3,E3,G3,C4"]
    for sec in (0, 8):
        s.arp(i_guitar, PROG, [0, 2, 1, 3, 2, 1, 3, 2], sec, 0.5, 0.45, -0.3, 0.25)
        bass_line(s, ["F1", "Bb1", "C2", "F1", "D2", "Bb1", "G1", "C2"], sec, [(0, 1.5, 0), (2, 1.5, 7)], gain=0.45)
        s.drums(["x.x.x.x.x.x.x.x.", "f.......f.....f."], sec, 8, 0.25, swing=0.25)
    lull = "C5:1 A4:.5 Bb4:.5 C5:1 F4:1 D5:1 C5:.5 Bb4:.5 A4:2 G4:.5 A4:.5 Bb4:.5 C5:.5 A4:1 F4:1 G4:3 r:1"
    lull2 = "A4:1 F4:.5 G4:.5 A4:1 D5:1 Bb4:1 A4:.5 G4:.5 F4:2 G4:.5 A4:.5 Bb4:.5 G4:.5 E4:1 C4:1 F4:3 r:1"
    s.line(i_kalimba, lull + " " + lull2, 0, 0.42, 0.25, 0.35)
    s.line(i_flute, lull + " " + lull2, 8, 0.32, 0.3, 0.45, shift=-12)
    s.line(i_kalimba, lull2, 12, 0.25, -0.2, 0.4, shift=12)
    return s


def song_forest():
    s = Song("forest", 100, 32, verb=(2.4, 0.8, 6000, 0.02))
    secs = [(0, PROG_A, BASS_A), (4, PROG_A, BASS_A), (8, PROG_B, BASS_B), (12, PROG_A, BASS_A),
            (16, PROG_A, BASS_A), (20, PROG_B, BASS_B), (24, PROG_A, BASS_A), (28, PROG_A, BASS_A)]
    for sec, prog, bass in secs:
        s.arp(i_marimba, prog, [0, 2, 1, 2, 0, 2, 1, 2], sec, 0.5, 0.3, -0.35, 0.2, shift=12)
        s.arp(i_pizz, prog, [0, None, 2, None, 1, None, 2, 1], sec, 0.5, 0.35, 0.35, 0.2)
        s.chords(i_pad, prog, sec, 4, 0.13, 0.0, 0.5)
        bass_line(s, bass, sec, [(0, 0.75, 0), (1.5, 0.5, 0), (2, 0.75, 7), (3.5, 0.5, 12)], gain=0.55)
        s.drums(["x.x.x.x.x.x.x.x.", "f.....f...f....."], sec, 4, 0.3)
    s.line(i_flute, MOTIF, 4, 0.48, 0.15, 0.4, shift=12)
    s.line(i_flute, MOTIF_B, 8, 0.45, 0.15, 0.4, shift=12)
    s.line(i_flute, MOTIF, 12, 0.4, 0.15, 0.4, shift=12)
    s.line(i_kalimba, MOTIF, 12, 0.22, -0.3, 0.4, shift=24)
    s.line(i_flute, MOTIF_B, 20, 0.4, 0.15, 0.4, shift=12)
    s.line(i_kalimba, MOTIF, 24, 0.3, -0.3, 0.4, shift=12)
    s.line(i_flute, "A4:2 F4:2 G4:2 E4:2 F4:1 E4:1 D4:1 C4:1 D4:4", 28, 0.35, 0.15, 0.45, shift=12)
    # ---- 전투 겹: 같은 화성 위에 북 + 낮은 현 오스티나토 + 금관
    for sec, prog, bass in secs:
        s.drums(["t.......t.t.....", "....s.......s..s", "h.h.h.h.h.h.h.h."], sec, 4, 0.5, layer="stem", accent=2)
        s.arp(i_stacc, prog, [0, 0, 1, 0, 2, 0, 1, 0], sec, 0.25, 0.32, -0.15, 0.15, layer="stem", shift=-12)
    for sec in (4, 12, 20, 28):
        s.line(i_brass, "D4:1.5 A4:.5 A4:2 G4:1 F4:1 E4:2", sec, 0.28, 0.25, 0.3, layer="stem")
        s.line(i_brass, "D3:3 r:1", sec + 3, 0.25, 0.25, 0.3, layer="stem")
    s.drums("w...............", 7, 1, 0.35, layer="stem")
    s.drums("C...............", 8, 1, 0.3, layer="stem")
    return s


def song_cave():
    s = Song("cave", 80, 16, verb=(3.6, 1.6, 4500, 0.06))
    # E 프리지안: 모티프를 E로 옮기고 2도를 낮춘다 (F♮)
    m = "E4:.75 B4:.25 B4:1 A4:.5 G4:.5 F4:1 G4:.5 A4:.5 B4:.5 D5:.5 C5:1 B4:1 A4:.5 G4:.5 F4:.5 E4:.5 D4:1 F4:1 E4:4"
    PROG = ["E2,B2,E3", "F2,C3,F3", "D2,A2,D3", "E2,B2,E3"]
    for sec in range(0, 16, 4):
        s.chords(i_drone, ["E2", "F2", "D2", "E2"], sec, 4, 0.35, 0.0, 0.3)
        s.arp(i_celesta, PROG, [2, None, 1, None, None, 2, None, 0], sec, 0.5, 0.22, 0.4, 0.6, shift=24)
        s.drums(["d.......d.....d.", "........c......."], sec, 4, 0.3, pan=-0.4, send=0.7)
        s.drums(["....d.........d.", "c..............."], sec, 4, 0.25, pan=0.5, send=0.7)
    s.line(i_celesta, m, 4, 0.35, -0.2, 0.6, shift=12)
    s.line(i_flute, stretch(m, 1.0), 8, 0.3, 0.2, 0.6, shift=0)
    s.line(i_musicbox, "E5:2 B4:2 C5:2 A4:2 B4:4 r:4", 12, 0.22, 0.4, 0.7)
    # 전투 겹
    for sec in range(0, 16, 4):
        s.drums(["t.....t.t...t...", "......M.....m.m."], sec, 4, 0.55, layer="stem", send=0.25)
        s.chords(i_trem, PROG, sec, 4, 0.28, 0.0, 0.3, layer="stem", shift=12)
        bass_line(s, ["E1", "F1", "D1", "E1"], sec, [(0, 0.5, 0), (0.75, 0.25, 0), (1.5, 0.5, 0), (2, 0.5, 12), (3, 0.5, 0)], gain=0.55, layer="stem")
    s.line(i_brass, "E3:2 F3:2 E3:1 D3:1 E3:2", 4, 0.22, 0.0, 0.4, layer="stem")
    s.line(i_brass, "E3:2 F3:2 G3:1 F3:1 E3:2", 12, 0.22, 0.0, 0.4, layer="stem")
    return s


def song_nest():
    s = Song("nest", 76, 16, verb=(4.0, 1.8, 3500, 0.06))
    PROG = ["C#3,E3,G#3", "A2,C#3,E3", "D3,F#3,A3", "C#3,E3,G#3"]
    for sec in range(0, 16, 4):
        s.chords(lambda f, d, v: i_choir(f, d, v, "o"), PROG, sec, 4, 0.3, 0.0, 0.6)
        s.chords(i_drone, ["C#2", "A1", "D2", "C#2"], sec, 4, 0.3, 0.0, 0.3)
        s.drums(["H...............", "........H......."], sec, 4, 0.45, send=0.2)
        s.drums(["..c...c.....c...", "c.......c.c....."], sec, 4, 0.12, pan=0.6, send=0.6)
    # 모티프 조각을 낮고 느리게, 반음 어긋나게
    frag = "C#4:1.5 G#4:.5 G#4:2 F#4:1 E4:1 D4:2 C#4:4 r:4"
    s.line(i_musicbox, frag, 4, 0.28, -0.3, 0.7, shift=12)
    s.line(i_musicbox, frag, 12, 0.2, 0.3, 0.8, shift=13)   # 반음 위로 어긋난 메아리
    s.line(lambda f, d, v: i_choir(f, d, v, "u"), "G#4:4 A4:4 F#4:4 G#4:4", 8, 0.18, 0.2, 0.7)
    # 전투 겹: 거미 발소리 같은 16분 피치카토 + 톰
    for sec in range(0, 16, 4):
        s.arp(i_pizz, PROG, [0, 1, 2, 1, 0, 2, 1, 2], sec, 0.25, 0.3, -0.2, 0.2, layer="stem", shift=12)
        s.drums(["m...M...m.m.M...", "....s.......s...", "hhhhhhhhhhhhhhhh"], sec, 4, 0.4, layer="stem", accent=4)
        bass_line(s, ["C#2", "A1", "D2", "C#2"], sec, [(0, 0.5, 0), (0.5, 0.5, 12), (1, 0.5, 0), (2, 0.5, 0), (2.5, 0.5, 12), (3.5, 0.5, 1)], gain=0.5, layer="stem")
    return s


def song_boss():
    s = Song("boss", 140, 32, verb=(2.8, 1.0, 5000, 0.02))
    MN = to_minor(MOTIF)
    PA = ["D3,F3,A3", "Bb2,D3,F3", "C3,E3,G3", "A2,C#3,E3"]
    PB = ["G2,Bb2,D3", "D3,F3,A3", "Bb2,D3,F3", "A2,C#3,E3"]
    RA = ["D2", "Bb1", "C2", "A1"]
    RB = ["G1", "D2", "Bb1", "A1"]
    secs = [(0, PA, RA), (4, PA, RA), (8, PB, RB), (12, PA, RA), (16, PA, RA), (20, PB, RB), (24, PA, RA), (28, PB, RB)]
    for sec, prog, roots in secs:
        s.drums(["k.....k.k.......", "....s.......s...", "h.h.h.h.h.h.h.ho"], sec, 4, 0.55, accent=2)
        s.drums(["t.......t...t.t."], sec, 4, 0.45)
        s.arp(i_stacc, prog, [0, 0, 1, 0, 2, 0, 1, 2], sec, 0.25, 0.4, -0.25, 0.15, shift=-12)
        s.arp(i_stacc, prog, [2, 1, 2, 0, 2, 1, 2, 0], sec, 0.25, 0.22, 0.3, 0.15)
        bass_line(s, roots, sec, [(0, 0.5, 0), (0.75, 0.25, 0), (1.5, 0.5, 0), (2, 0.5, 0), (2.75, 0.25, 0), (3.5, 0.5, 12)], gain=0.65)
        s.chords(i_pad, prog, sec, 4, 0.12, 0.0, 0.4)
    s.line(i_brass, stretch(MN, 1.0), 4, 0.42, 0.1, 0.3)
    s.line(i_brass, stretch(MN, 1.0), 4, 0.25, -0.1, 0.3, shift=-12)
    s.line(i_trem, "D5:4 F5:4 E5:4 C#5:4", 8, 0.18, 0.3, 0.3)
    s.line(i_brass, "G4:2 A4:1 Bb4:1 A4:2 F4:2 D5:2 C5:1 Bb4:1 A4:4", 8, 0.38, 0.1, 0.3)
    s.line(i_brass, stretch(MN, 1.0), 16, 0.42, 0.1, 0.3)
    s.line(i_brass, "G4:2 A4:1 Bb4:1 A4:2 F4:2 D5:2 C5:1 Bb4:1 A4:4", 20, 0.38, 0.1, 0.3)
    s.line(i_flute, stretch(MN, 0.5) + " " + stretch(MN, 0.5), 24, 0.3, 0.35, 0.3, shift=12)
    s.drums("w...............", 3, 1, 0.4)
    s.drums("C...............", 4, 1, 0.35)
    s.drums("C...............", 16, 1, 0.35)
    s.drums("T.T.T.TTT.T.T.TT", 31, 1, 0.45)
    # 2페이즈 겹: 합창 + 높은 현 + 팀파니
    for sec, prog, roots in secs:
        s.chords(lambda f, d, v: i_choir(f, d, v, "a"), [",".join(c.split(",")) for c in prog], sec, 4, 0.36, 0.0, 0.5, layer="stem", shift=12)
        s.drums(["T.......T...T...", "..........m.M.m."], sec, 4, 0.45, layer="stem")
        s.arp(i_trem, prog, [2, 1, 0, 1], sec, 1.0, 0.16, 0.4, 0.3, layer="stem", shift=24)
    s.line(lambda f, d, v: i_choir(f, d, v, "o"), stretch(MN, 1.0), 12, 0.3, 0.0, 0.5, layer="stem")
    s.line(lambda f, d, v: i_choir(f, d, v, "o"), stretch(MN, 1.0), 24, 0.3, 0.0, 0.5, layer="stem")
    return s


def song_crown():
    s = Song("crown", 128, 16, verb=(1.8, 0.6, 6000, 0.02))
    MJ = to_major(MOTIF)
    PROG = ["D3,F#3,A3", "G2,B2,D3", "A2,C#3,E3", "D3,F#3,A3"]
    for sec in range(0, 16, 4):
        # 움파 움파 (튜바 + 오프비트 화음)
        bass_line(s, ["D2", "G1", "A1", "D2"], sec, [(0, 0.5, 0), (2, 0.5, 7)], inst=i_brass, gain=0.4)
        s.arp(i_stacc, PROG, [None, 0, None, 1, None, 2, None, 1], sec, 0.5, 0.3, 0.2, 0.15)
        s.drums(["k...k...k...k...", "..s...s...s...s.", "x.x.x.x.x.x.x.x."], sec, 4, 0.42)
    s.line(i_brass, MJ, 0, 0.42, 0.1, 0.3, shift=12)
    s.line(i_flute, MJ, 4, 0.4, -0.2, 0.3, shift=24)
    s.line(i_brass, to_major(MOTIF_B) + " " + "A4:1 F#4:.5 E4:.5 D4:1 C#4:1 D4:4", 8, 0.4, 0.1, 0.3, shift=12)
    s.line(i_marimba, MJ, 12, 0.35, -0.3, 0.3, shift=24)
    s.line(i_brass, MJ, 12, 0.35, 0.1, 0.3, shift=12)
    s.drums("T.T.T.T.TTTTTTTT", 15, 1, 0.4)
    return s


def song_results():
    s = Song("results", 90, 8, verb=(2.6, 0.9, 5500, 0.03))
    MJ = to_major(MOTIF)
    PROG = ["D3,F#3,A3", "G2,B2,D3", "A2,C#3,E3", "D3,F#3,A3"]
    for sec in (0, 4):
        s.arp(i_harp, PROG, [0, 1, 2, 3, 2, 1, 2, 3], sec, 0.5, 0.4, -0.3, 0.4, octave_up=True, shift=12)
        s.chords(i_pad, PROG, sec, 4, 0.2, 0.0, 0.5)
        bass_line(s, ["D2", "G1", "A1", "D2"], sec, [(0, 2, 0), (2, 2, 7)], gain=0.45)
        s.drums(["f.......f.......", "....x.......x..."], sec, 4, 0.3)
    s.line(i_flute, MJ, 0, 0.45, 0.2, 0.45, shift=12)
    s.line(i_brass, MJ, 4, 0.3, 0.1, 0.4)
    s.line(i_celesta, MJ, 4, 0.22, -0.3, 0.5, shift=24)
    return s


def song_gameover():
    s = Song("gameover", 66, 8, verb=(3.5, 1.4, 4000, 0.04))
    MN = to_minor(MOTIF)
    PROG = ["D3,F3,A3", "Bb2,D3,F3", "G2,Bb2,D3", "A2,C#3,E3"]
    for sec in (0, 4):
        s.chords(i_pad, PROG, sec, 4, 0.22, 0.0, 0.6, shift=-12)
    s.line(i_musicbox, MN, 0, 0.4, 0.0, 0.6, shift=12)
    s.line(i_musicbox, stretch(MN, 1.0), 4, 0.3, 0.2, 0.7, shift=12)
    s.line(i_celesta, "D4:8 A3:8", 4, 0.15, -0.3, 0.6, shift=12)
    return s


# ================================================================== 짧은 조각
def stingers():
    def st(name, bpm, build, verb=(2.4, 0.8, 5500, 0.02)):
        s = Song(name, bpm, 2, verb=verb, loop=False)
        build(s)
        write(s, -16.0)

    def clear(s):
        s.line(i_brass, "D4:.25 F#4:.25 A4:.25 D5:1.25", 0, 0.45)
        s.line(i_harp, "[D4,F#4,A4,D5]:2", 0.25, 0.4)
        s.drums("T...C...........", 0, 1, 0.3)
    st("st_clear", 120, clear)

    def item(s):
        s.line(i_celesta, "A5:.25 C#6:.25 E6:.25 A6:1", 0, 0.5)
        s.line(i_harp, "[A4,C#5,E5]:2", 0, 0.3)
    st("st_item", 120, item)

    def boss(s):
        s.drums(["T...............", "w..............."], 0, 1, 0.5)
        s.line(lambda f, d, v: i_choir(f, d, v, "a"), "[D3,A3,D4,F4]:4", 0, 0.6)
        s.line(i_brass, "D3:1 Eb3:1 D3:2", 1, 0.5)
        s.line(i_bell, "D3:4", 0, 0.4)
    st("st_boss", 70, boss, (3.5, 1.4, 4000, 0.03))

    def win(s):
        MJ = to_major("D4:.5 A4:.5 A4:1 G4:.5 F4:.5 E4:1 D5:4")
        s.line(i_brass, MJ, 0, 0.45)
        s.line(i_brass, "[D3,F#3,A3]:4", 1, 0.3)
        s.drums(["T.T.T.TTC.......", "f.f.f.ff........"], 0, 1, 0.4)
        s.line(i_harp, "[D4,F#4,A4,D5,F#5]:4", 1.5, 0.4)
    s2 = Song("st_win", 110, 3, verb=(2.4, 0.8, 5500, 0.02), loop=False)
    win(s2)
    write(s2, -16.0)

    def lose(s):
        s.line(i_musicbox, "A4:1 F4:1 E4:1 D4:3", 0, 0.5)
        s.line(i_pad, "[D3,F3,A3]:6", 0, 0.3, shift=-12)
    s3 = Song("st_lose", 70, 2, verb=(3.5, 1.4, 4000, 0.04), loop=False)
    lose(s3)
    write(s3, -18.0)

    def puzzle(s):
        s.line(i_marimba, "D5:.25 F5:.25 A5:.25 C6:.25 D6:1", 0, 0.5)
        s.line(i_kalimba, "[D4,A4]:2", 0.5, 0.4)
    st("st_puzzle", 130, puzzle)

    def secret(s):
        s.line(i_celesta, "G5:.25 F#5:.25 D#5:.25 A4:.25 G#4:.25 E5:.25 G#5:.25 C6:1", 0, 0.5)
    st("st_secret", 140, secret, (3.0, 1.2, 6000, 0.03))

    def phase(s):
        s.drums(["T.T.T.T.TTTTTTTT", "w..............."], 0, 1, 0.5)
        s.line(lambda f, d, v: i_choir(f, d, v, "o"), "[D3,F3,Ab3,D4]:4", 1, 0.6)
        s.line(i_brass, "D3:.5 D3:.5 D3:.5 F3:1.5 Ab3:2", 1, 0.5)
    st("st_phase", 120, phase, (3.0, 1.2, 4500, 0.03))


SONGS = {
    "menu": song_menu, "camp": song_camp, "forest": song_forest, "cave": song_cave, "nest": song_nest,
    "boss": song_boss, "crown": song_crown, "results": song_results, "gameover": song_gameover,
}


def main(names):
    os.makedirs(OUT, exist_ok=True)
    if not names:
        for f in os.listdir(OUT):
            if f.endswith(".wav") or f.endswith(".wav.import"):
                os.remove(os.path.join(OUT, f))
    for n in names or list(SONGS) + ["stingers"]:
        print(n, flush=True)
        if n == "stingers":
            stingers()
        else:
            write(SONGS[n]())


if __name__ == "__main__":
    main(sys.argv[1:])
