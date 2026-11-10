"""효과음 합성용 작은 DSP 라이브러리 (numpy / scipy).
모든 신호는 44.1 kHz mono float64 배열이다. 소리 정의 스크립트(sfx_*.py)가 이 함수들을 조합한다."""
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100


def t_axis(dur):
    return np.arange(int(round(dur * SR))) / SR


def n_samples(dur):
    return int(round(dur * SR))


# ---------- 음높이 / 엔벨로프 ----------

def sweep_exp(f0, f1, dur):
    """f0 -> f1 지수 곡선 주파수 배열"""
    n = n_samples(dur)
    return f0 * (f1 / f0) ** (np.arange(n) / max(1, n - 1))


def sweep_lin(f0, f1, dur):
    return np.linspace(f0, f1, n_samples(dur))


def const(f, dur):
    return np.full(n_samples(dur), float(f))


def env_exp(dur, decay, attack=0.002):
    """빠른 어택 + 지수 감쇠. decay = 1/e 까지 걸리는 시간"""
    t = t_axis(dur)
    e = np.exp(-t / decay)
    if attack > 0:
        e *= np.clip(t / attack, 0, 1)
    return e


def env_adsr(dur, a, d, s, r):
    n = n_samples(dur)
    t = np.arange(n) / SR
    e = np.empty(n)
    rel_start = dur - r
    for i, x in enumerate(t):
        if x < a:
            e[i] = x / a
        elif x < a + d:
            e[i] = 1 - (1 - s) * (x - a) / d
        elif x < rel_start:
            e[i] = s
        else:
            e[i] = s * max(0.0, 1 - (x - rel_start) / r)
    return e


def env_swell(dur, peak_pos=0.5, curve=2.0):
    """0 -> 1 -> 0 로 부풀었다 줄어드는 엔벨로프 (peak_pos: 0~1)"""
    x = np.linspace(0, 1, n_samples(dur))
    up = (x / peak_pos) ** curve
    down = ((1 - x) / (1 - peak_pos)) ** curve
    return np.where(x < peak_pos, up, down)


# ---------- 오실레이터 ----------

def phase(freq):
    return 2 * np.pi * np.cumsum(freq) / SR


def sine(freq):
    return np.sin(phase(freq))


def tri(freq):
    p = (np.cumsum(freq) / SR) % 1.0
    return 4 * np.abs(p - 0.5) - 1


def square(freq, duty=0.5):
    p = (np.cumsum(freq) / SR) % 1.0
    return np.where(p < duty, 1.0, -1.0)


def saw(freq):
    p = (np.cumsum(freq) / SR) % 1.0
    return 2 * p - 1


def noise(dur, seed):
    return np.random.default_rng(seed).uniform(-1, 1, n_samples(dur))


def fm(freq, ratio, index, index_env=None):
    """2-op FM (마림바·벨 계열). index_env 가 있으면 변조 깊이에 곱한다"""
    mod = np.sin(phase(freq * ratio))
    idx = index * (index_env if index_env is not None else 1.0)
    return np.sin(phase(freq) + idx * mod)


# ---------- 필터 / 처리 ----------

def lowpass(x, fc, order=2):
    sos = signal.butter(order, fc, 'low', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def highpass(x, fc, order=2):
    sos = signal.butter(order, fc, 'high', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], 'band', fs=SR, output='sos')
    return signal.sosfilt(sos, x)


def sweep_bandpass(x, centers, q=2.0, block=256):
    """중심 주파수가 시간에 따라 바뀌는 밴드패스 (블록 단위로 계수 교체, 상태 유지)"""
    out = np.zeros_like(x)
    zi = None
    for start in range(0, len(x), block):
        c = float(centers[min(start, len(centers) - 1)])
        bw = c / q
        lo, hi = max(20.0, c - bw / 2), min(SR / 2 - 100, c + bw / 2)
        sos = signal.butter(2, [lo, hi], 'band', fs=SR, output='sos')
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        seg, zi = signal.sosfilt(sos, x[start:start + block], zi=zi)
        out[start:start + block] = seg
    return out


def bitcrush(x, bits=8, hold=1):
    """비트 수 줄이기 + 샘플 홀드 (hold 샘플마다 값 유지)"""
    q = 2 ** (bits - 1)
    y = np.round(x * q) / q
    if hold > 1:
        idx = (np.arange(len(y)) // hold) * hold
        y = y[idx]
    return y


def drive(x, amount=2.0):
    return np.tanh(x * amount) / np.tanh(amount)


def echo(x, delay, feedback, mix, taps=4):
    d = n_samples(delay)
    out = np.concatenate([x, np.zeros(d * taps)])
    for k in range(1, taps + 1):
        out[d * k:d * k + len(x)] += x * (feedback ** k) * mix
    return out


def gate_random(dur, rate_lo, rate_hi, seed, duty=0.5):
    """불규칙한 간격으로 켜졌다 꺼지는 게이트 (전기 튀김용)"""
    rng = np.random.default_rng(seed)
    n = n_samples(dur)
    g = np.zeros(n)
    i = 0
    while i < n:
        seg = int(SR / rng.uniform(rate_lo, rate_hi))
        on = int(seg * rng.uniform(duty * 0.5, min(1.0, duty * 1.5)))
        g[i:i + on] = rng.uniform(0.4, 1.0)
        i += seg
    return g


# ---------- 조합 / 출력 ----------

def mix(*parts):
    n = max(len(p) for p in parts)
    out = np.zeros(n)
    for p in parts:
        out[:len(p)] += p
    return out


def at(x, offset):
    """offset 초 뒤에 시작하도록 앞에 무음을 붙인다"""
    return np.concatenate([np.zeros(n_samples(offset)), x])


def fade(x, fade_in=0.001, fade_out=0.006):
    y = x.copy()
    a, b = n_samples(fade_in), n_samples(fade_out)
    if a > 0:
        y[:a] *= np.linspace(0, 1, a)
    if b > 0:
        y[-b:] *= np.linspace(1, 0, b)
    return y


def trim_tail(x, threshold_db=-60.0):
    """끝부분의 거의 무음 구간을 잘라 낸다"""
    thr = 10 ** (threshold_db / 20) * np.max(np.abs(x))
    idx = np.nonzero(np.abs(x) > thr)[0]
    return x[:idx[-1] + 1] if len(idx) else x


def finalize(x, peak_db=-1.0):
    """DC 제거 → 꼬리 정리 → 페이드 → 피크 정규화"""
    y = x - np.mean(x)
    y = highpass(y, 25, order=1)
    y = trim_tail(y)
    y = fade(y)
    peak = np.max(np.abs(y))
    if peak > 0:
        y *= 10 ** (peak_db / 20) / peak
    return y


def write_wav(path, x):
    pcm = np.clip(x, -1, 1)
    wavfile.write(path, SR, (pcm * 32767).astype(np.int16))


# ---------- 루프 ----------

def make_loop(x, loop_dur, xfade=0.08):
    """x(길이 >= loop_dur + xfade)의 끝 xfade 구간을 앞부분에 겹쳐 loop_dur 길이의 끊김 없는 루프로 만든다"""
    n, f = n_samples(loop_dur), n_samples(xfade)
    body = x[:n].copy()
    tail = x[n:n + f]
    w = np.sin(np.linspace(0, np.pi / 2, f)) ** 2  # equal-power 에 가까운 곡선
    body[:f] = body[:f] * w + tail * (1 - w)
    return body


def finalize_loop(x, peak_db=-1.0):
    """루프용 마무리: 꼬리 정리·페이드 없이 DC 제거와 피크 정규화만 한다"""
    y = x - np.mean(x)
    peak = np.max(np.abs(y))
    if peak > 0:
        y *= 10 ** (peak_db / 20) / peak
    return y
