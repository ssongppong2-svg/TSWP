"""TSWP 소리 합성 공용 도구 (44.1kHz).

효과음·음악·환경음 생성기가 함께 쓴다. 외부 소리 파일 없이 전부 수식으로 만든다.
"""
import os
import subprocess
import wave

import numpy as np
from scipy import signal

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
rng = np.random.default_rng(7)


def seed(s):
    global rng
    rng = np.random.default_rng(s)


def R():
    return rng


# ------------------------------------------------------------------ 기본
def n_of(dur):
    return max(1, int(round(SR * dur)))


def t_of(dur):
    return np.arange(n_of(dur)) / SR


def silence(dur):
    return np.zeros(n_of(dur))


def fit(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def mix(*parts):
    """(소리, 시작초, 배율) 또는 소리 를 겹친다."""
    items = []
    for p in parts:
        if isinstance(p, tuple):
            x, at = p[0], p[1]
            g = p[2] if len(p) > 2 else 1.0
        else:
            x, at, g = p, 0.0, 1.0
        items.append((np.asarray(x, dtype=np.float64), int(round(at * SR)), g))
    n = max(len(x) + o for x, o, _ in items)
    out = np.zeros(n)
    for x, o, g in items:
        out[o:o + len(x)] += x * g
    return out


def cat(*xs):
    return np.concatenate([np.asarray(x, dtype=np.float64) for x in xs])


def stereo_mix(n, *parts):
    """[(L,R) 또는 mono, at, gain, pan] → (n,2)"""
    out = np.zeros((n, 2))
    for p in parts:
        x, at = p[0], p[1]
        g = p[2] if len(p) > 2 else 1.0
        pan = p[3] if len(p) > 3 else 0.0
        o = int(round(at * SR))
        if o >= n:
            continue
        if x.ndim == 1:
            l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
            seg = x[: n - o]
            out[o:o + len(seg), 0] += seg * g * l * 1.4142
            out[o:o + len(seg), 1] += seg * g * r * 1.4142
        else:
            seg = x[: n - o]
            out[o:o + len(seg)] += seg * g
    return out


# ------------------------------------------------------------------ 엔벨로프
def env_exp(dur, decay, attack=0.002):
    t = t_of(dur)
    e = np.exp(-t / max(decay, 1e-4))
    a = n_of(attack)
    if a > 1:
        e[:a] *= np.linspace(0, 1, a) ** 0.6
    return e


def adsr(dur, a=0.01, d=0.1, s=0.7, r=0.2):
    n = n_of(dur)
    na, nd, nr = n_of(a), n_of(d), n_of(r)
    ns = max(0, n - na - nd - nr)
    e = np.concatenate([
        np.linspace(0, 1, na, endpoint=False) ** 0.8,
        1 - (1 - s) * (1 - np.exp(-5 * np.linspace(0, 1, nd))),
        np.full(ns, s),
        s * np.exp(-5 * np.linspace(0, 1, nr)),
    ])
    return fit(e, n)


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    a, b = min(len(x), n_of(fin)), min(len(x), n_of(fout))
    if a > 1:
        x[:a] *= np.linspace(0, 1, a)
    if b > 1:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def line(dur, *pts):
    """line(dur, (t0,v0), (t1,v1) …) 구간 직선"""
    t = t_of(dur)
    ts = [p[0] for p in pts]
    vs = [p[1] for p in pts]
    return np.interp(t, ts, vs)


def expline(dur, a, b, curve=1.0):
    t = np.linspace(0, 1, n_of(dur)) ** curve
    return a * (b / a) ** t


# ------------------------------------------------------------------ 소리 원료
def osc(freq, dur, kind="sine", phase=0.0):
    n = n_of(dur)
    f = np.asarray(freq, dtype=np.float64)
    f = np.full(n, float(f)) if f.ndim == 0 else fit(f, n)
    ph = np.cumsum(f) / SR + phase
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    frac = ph - np.floor(ph)
    if kind == "tri":
        return 4 * np.abs(frac - 0.5) - 1
    if kind == "saw":
        # 대역 제한 근사 (polyBLEP)
        dt = np.clip(f / SR, 1e-6, 0.5)
        y = 2 * frac - 1
        m1 = frac < dt
        t1 = frac[m1] / dt[m1]
        y[m1] -= t1 + t1 - t1 * t1 - 1
        m2 = frac > 1 - dt
        t2 = (frac[m2] - 1) / dt[m2]
        y[m2] -= t2 * t2 + t2 + t2 + 1
        return y
    if kind == "square":
        return np.sign(np.sin(2 * np.pi * ph))
    raise ValueError(kind)


def supersaw(freq, dur, voices=5, detune=0.012):
    out = np.zeros(n_of(dur))
    for i in range(voices):
        d = 1 + detune * (i - (voices - 1) / 2) / max(1, (voices - 1) / 2)
        out += osc(np.asarray(freq) * d, dur, "saw", phase=rng.random())
    return out / voices


def noise(dur, color="white"):
    n = n_of(dur)
    w = rng.standard_normal(n)
    if color == "white":
        return w / 3
    if color == "pink":
        b, a = [0.049922035, -0.095993537, 0.050612699, -0.004408786], [1, -2.494956002, 2.017265875, -0.522189400]
        return signal.lfilter(b, a, w) * 0.9
    if color == "brown":
        y = np.cumsum(w)
        y = signal.lfilter([1, -1], [1, -0.995], y)
        return y / (np.max(np.abs(y)) + 1e-9)
    raise ValueError(color)


def crackle(dur, density=200, decay=0.002):
    """잔 불꽃·자갈 같은 톡톡 소리"""
    n = n_of(dur)
    out = np.zeros(n)
    k = int(density * dur)
    pos = rng.integers(0, n, k)
    amp = rng.random(k) ** 2
    out[pos] = amp * rng.choice([-1, 1], k)
    ir = np.exp(-np.arange(n_of(decay * 6)) / (decay * SR))
    return signal.fftconvolve(out, ir)[:n]


def modal(dur, partials, attack=0.0005):
    """[(주파수, 감쇠초, 크기)] 쇠·나무·유리 같은 타격음"""
    t = t_of(dur)
    out = np.zeros(len(t))
    for f, d, a in partials:
        out += a * np.sin(2 * np.pi * f * t + rng.random() * 6.28) * np.exp(-t / d)
    na = n_of(attack)
    if na > 1:
        out[:na] *= np.linspace(0, 1, na)
    return out


def ks(freq, dur, bright=0.5, decay=0.996):
    """카플러스-스트롱 현 (하프·기타·피치카토)"""
    n = n_of(dur)
    p = max(2, int(SR / freq))
    buf = rng.uniform(-1, 1, p)
    buf = signal.lfilter([bright, 1 - bright], [1], buf)
    out = np.zeros(n)
    idx = 0
    last = 0.0
    for i in range(n):
        v = buf[idx]
        out[i] = v
        nv = decay * (0.5 * (v + last))
        last = v
        buf[idx] = nv
        idx = (idx + 1) % p
    return out


def ks_fast(freq, dur, bright=0.5, decay=0.996):
    """KS 를 벡터화 (블록 단위) — 긴 음악용"""
    n = n_of(dur)
    p = max(2, int(round(SR / freq)))
    frac = SR / freq - p
    buf = rng.uniform(-1, 1, p) * 0.9
    buf = signal.lfilter([bright, 1 - bright], [1], buf)
    out = np.empty(n)
    pos = 0
    cur = buf
    # 한 주기씩: 다음 주기 = decay * 평균(cur, roll(cur))
    a = 0.5 + frac * 0.0
    while pos < n:
        m = min(p, n - pos)
        out[pos:pos + m] = cur[:m]
        nxt = decay * (a * cur + (1 - a) * np.roll(cur, 1))
        cur = nxt
        pos += m
    return out


def fm(carrier, ratio, index, dur, idx_env=None):
    t = t_of(dur)
    c = np.asarray(carrier, dtype=np.float64)
    c = np.full(len(t), float(c)) if c.ndim == 0 else fit(c, len(t))
    mod_ph = np.cumsum(c * ratio) / SR
    ie = index if idx_env is None else index * fit(idx_env, len(t))
    car_ph = np.cumsum(c) / SR
    return np.sin(2 * np.pi * car_ph + ie * np.sin(2 * np.pi * mod_ph))


# ------------------------------------------------------------------ 필터
def _sos(kind, f, order=2, q=None):
    nyq = SR / 2
    if kind == "bp":
        lo, hi = f
        return signal.butter(order, [max(10, lo) / nyq, min(hi, nyq * 0.98) / nyq], "bandpass", output="sos")
    f = min(max(f, 10), nyq * 0.98)
    return signal.butter(order, f / nyq, kind, output="sos")


def lp(x, f, order=2):
    return signal.sosfilt(_sos("low", f, order), x)


def hp(x, f, order=2):
    return signal.sosfilt(_sos("high", f, order), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bp", (lo, hi), order), x)


def peak(x, f, q, gain_db):
    """피킹 EQ (RBJ)"""
    A = 10 ** (gain_db / 40)
    w = 2 * np.pi * f / SR
    al = np.sin(w) / (2 * q)
    b = [1 + al * A, -2 * np.cos(w), 1 - al * A]
    a = [1 + al / A, -2 * np.cos(w), 1 - al / A]
    return signal.lfilter(b, a, x)


def sweep_lp(x, f0, f1, curve=1.0, block=256):
    """시간에 따라 움직이는 저역 통과 (상태 변수 필터)"""
    n = len(x)
    fs = f0 * (f1 / f0) ** (np.linspace(0, 1, n) ** curve) if f0 > 0 and f1 > 0 else np.linspace(f0, f1, n)
    return svf(x, fs, 0.7, "lp")


def sweep_bp(x, f0, f1, q=2.0, curve=1.0):
    n = len(x)
    fs = f0 * (f1 / f0) ** (np.linspace(0, 1, n) ** curve)
    return svf(x, fs, q, "bp")


def svf(x, freqs, q, mode="lp"):
    """채임벌린 상태 변수 필터 (주파수 배열 허용)"""
    freqs = np.asarray(freqs, dtype=np.float64)
    if freqs.ndim == 0:
        freqs = np.full(len(x), float(freqs))
    f = 2 * np.sin(np.pi * np.clip(freqs, 10, SR / 6.5) / SR)
    dmp = 1.0 / q
    low = band = 0.0
    out = np.empty(len(x))
    # 파이썬 루프지만 효과음 길이에선 충분히 빠르다
    for i in range(len(x)):
        fi = f[i]
        low += fi * band
        high = x[i] - low - dmp * band
        band += fi * high
        out[i] = low if mode == "lp" else band if mode == "bp" else high
    return out


def formant(x, vowel="a", shift=1.0, q=8.0):
    F = {
        "a": [(800, 1.0), (1150, 0.5), (2900, 0.25)],
        "o": [(450, 1.0), (800, 0.4), (2830, 0.1)],
        "u": [(325, 1.0), (700, 0.25), (2530, 0.08)],
        "e": [(400, 1.0), (1600, 0.4), (2700, 0.2)],
        "i": [(270, 1.0), (2140, 0.3), (2950, 0.2)],
    }[vowel]
    out = np.zeros(len(x))
    for f, g in F:
        fc = f * shift
        out += g * bp(x, fc / (1 + 1 / q), fc * (1 + 1 / q), 2)
    return out


# ------------------------------------------------------------------ 왜곡·공간
def drive(x, k=2.0):
    return np.tanh(x * k) / np.tanh(k)


def bitcrush(x, bits=8, down=1):
    q = 2 ** (bits - 1)
    y = np.round(x * q) / q
    if down > 1:
        y = np.repeat(y[::down], down)[: len(x)]
    return y


def room_ir(dur=1.2, decay=0.4, bright=6000, predelay=0.01, er=6, stereo=False, seed_=None):
    """간단한 합성 잔향 응답 (초기 반사 + 지수 감쇠 노이즈)"""
    g = np.random.default_rng(seed_) if seed_ is not None else rng
    n = n_of(dur)
    chans = []
    for c in range(2 if stereo else 1):
        t = np.arange(n) / SR
        tail = g.standard_normal(n) * np.exp(-t / decay)
        tail = signal.sosfilt(_sos("low", bright, 2), tail)
        # 시간이 지날수록 어두워지게: 뒤쪽을 한 번 더 깎는다
        dark = signal.sosfilt(_sos("low", bright * 0.35, 2), tail)
        w = np.clip(t / dur, 0, 1)
        tail = tail * (1 - w) + dark * w
        ir = np.zeros(n)
        pd = n_of(predelay)
        ir[pd:] = tail[: n - pd] * 0.5
        for _ in range(er):
            p = pd + int(g.uniform(0.002, 0.04) * SR)
            if p < n:
                ir[p] += g.uniform(-0.6, 0.6)
        ir[0] = 1.0
        chans.append(ir)
    return np.stack(chans, 1) if stereo else chans[0]


def reverb(x, wet=0.25, dur=1.2, decay=0.4, bright=6000, predelay=0.01):
    ir = room_ir(dur, decay, bright, predelay)
    ir[0] = 0
    y = signal.fftconvolve(x, ir)[: len(x) + len(ir) - 1]
    y = y / (np.max(np.abs(y)) + 1e-9) * (np.max(np.abs(x)) + 1e-9)
    return mix(x, (y, 0, wet))


def reverb_st(x, wet=0.25, dur=2.0, decay=0.6, bright=5000, predelay=0.02):
    """모노/스테레오 → 스테레오 잔향"""
    if x.ndim == 1:
        x = np.stack([x, x], 1)
    ir = room_ir(dur, decay, bright, predelay, stereo=True)
    ir[0, :] = 0
    out = np.zeros((len(x) + len(ir) - 1, 2))
    for c in range(2):
        y = signal.fftconvolve(x[:, c], ir[:, c])
        out[: len(y), c] += y
    norm = np.max(np.abs(out)) + 1e-9
    out *= (np.max(np.abs(x)) + 1e-9) / norm
    res = out * wet
    res[: len(x)] += x
    return res


def delay(x, t=0.25, fb=0.35, wet=0.3, lpf=4000):
    d = n_of(t)
    n = len(x) + d * 6
    y = fit(x, n).copy()
    tap = fit(x, n) * wet
    for k in range(1, 7):
        tap = np.roll(lp(tap, lpf, 1), d) * (fb if k > 1 else 1)
        tap[:d] = 0
        y += tap
    return y


def trim(x, thresh=1e-4, tail=0.02):
    a = np.abs(x) if x.ndim == 1 else np.max(np.abs(x), 1)
    idx = np.where(a > thresh * (np.max(a) + 1e-12))[0]
    if len(idx) == 0:
        return x[:1]
    end = min(len(x), idx[-1] + n_of(tail))
    return x[:end]


# ------------------------------------------------------------------ 저장
def normalize(x, peak_db=-1.0):
    p = np.max(np.abs(x)) + 1e-12
    return x / p * (10 ** (peak_db / 20))


def save_wav(path, x, peak_db=-1.0, do_trim=True):
    x = np.asarray(x, dtype=np.float64)
    x = hp(x, 25, 2) if x.ndim == 1 else np.stack([hp(x[:, 0], 25, 2), hp(x[:, 1], 25, 2)], 1)
    if do_trim:
        x = trim(x)
    x = fade(x, 0.0005, 0.006) if x.ndim == 1 else np.stack([fade(x[:, 0], 0.0005, 0.006), fade(x[:, 1], 0.0005, 0.006)], 1)
    x = normalize(x, peak_db)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1 if x.ndim == 1 else 2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def save_ogg(path, x, peak_db=-1.0, quality=5, loop_xfade=0.0):
    """스테레오 OGG. loop_xfade>0 이면 끝을 앞부분과 겹쳐 이음매 없는 반복으로."""
    x = np.asarray(x, dtype=np.float64)
    if x.ndim == 1:
        x = np.stack([x, x], 1)
    x = normalize(x, peak_db)
    tmp = path + ".tmp.wav"
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(tmp, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", str(quality), path], check=True)
    os.remove(tmp)


def loopify(x, xf=1.0):
    """끝 xf초를 앞쪽에 크로스페이드로 접어 넣는다 → 길이 len-xf, 끊김 없이 반복"""
    m = n_of(xf)
    if x.ndim == 1:
        x = x[:, None]
        mono = True
    else:
        mono = False
    head = x[:m].copy()
    tail = x[-m:]
    w = np.linspace(0, 1, m)[:, None]
    body = x[:-m].copy()
    body[:m] = head * np.sqrt(w) + tail * np.sqrt(1 - w)
    return body[:, 0] if mono else body
