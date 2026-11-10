"""BGM 합성용 스텝 시퀀서와 악기 (synth.py 위에 얹는다).
- 시간 단위는 박(beat). 128 BPM, 4/4 기준이지만 Song 생성 시 바꿀 수 있다.
- 각 레이어는 Track 에 음표를 쌓고 render() 로 mono 배열을 만든다. 같은 Song 의 레이어는 길이가 같아 샘플 단위로 맞물린다.
- 루프: 끝에서 넘친 꼬리(잔향·감쇠)를 처음에 더해(wrap) 이음새 없는 루프를 만든다."""
import numpy as np

from synth import (SR, sine, tri, square, saw, noise, fm, const, sweep_exp, env_exp, env_adsr,
                   lowpass, highpass, bandpass, bitcrush, n_samples)

NOTE_INDEX = {'C': -9, 'C#': -8, 'Db': -8, 'D': -7, 'D#': -6, 'Eb': -6, 'E': -5, 'F': -4, 'F#': -3, 'Gb': -3,
              'G': -2, 'G#': -1, 'Ab': -1, 'A': 0, 'A#': 1, 'Bb': 1, 'B': 2}


def hz(name):
    return 440.0 * 2 ** ((NOTE_INDEX[name[:-1]] + 12 * (int(name[-1]) - 4)) / 12)


CHORDS = {
    'Am': ['A', 'C', 'E'], 'F': ['F', 'A', 'C'], 'C': ['C', 'E', 'G'], 'G': ['G', 'B', 'D'],
    'E': ['E', 'G#', 'B'], 'Em': ['E', 'G', 'B'], 'Dm': ['D', 'F', 'A'],
}


def chord_notes(chord, octave, count=3):
    """코드 구성음을 octave 부터 위로 count 개 (자리바꿈 없이 근음부터)"""
    tones = CHORDS[chord]
    out = []
    o = octave
    prev = -999
    i = 0
    while len(out) < count:
        name = tones[i % len(tones)]
        f = hz(f'{name}{o}')
        if f <= prev:
            o += 1
            f = hz(f'{name}{o}')
        out.append(f)
        prev = f
        i += 1
    return out


class Song:
    def __init__(self, bars, bpm=128, beats_per_bar=4):
        self.bpm = bpm
        self.bars = bars
        self.beats_per_bar = beats_per_bar
        self.beat_sec = 60.0 / bpm
        self.length = n_samples(bars * beats_per_bar * self.beat_sec)

    def sample_at(self, beat):
        return int(round(beat * self.beat_sec * SR))

    def track(self):
        return Track(self)


class Track:
    def __init__(self, song):
        self.song = song
        self.buf = np.zeros(song.length + SR * 4)  # 꼬리 여유 4초

    def add(self, beat, signal, gain=1.0):
        s = self.song.sample_at(beat)
        self.buf[s:s + len(signal)] += signal * gain

    def render(self, loop=True):
        n = self.song.length
        out = self.buf[:n].copy()
        if loop:
            tail = self.buf[n:]
            out[:len(tail)] += tail[:n]  # 넘친 꼬리를 처음에 더한다 (루프 이음새)
        return out


# ---------------- 드럼 ----------------

def kick(amp=1.0):
    d = 0.28
    body = sine(sweep_exp(150, 45, d)) * env_exp(d, 0.09, attack=0.001)
    click = highpass(noise(0.004, 501), 2000) * 0.25
    return (body + np.pad(click, (0, len(body) - len(click)))) * amp


def snare(amp=1.0, seed=502):
    d = 0.18
    tone = sine(sweep_exp(220, 160, d)) * env_exp(d, 0.03) * 0.6
    rattle = bandpass(noise(d, seed), 1200, 7000) * env_exp(d, 0.05)
    return (tone + rattle) * amp


def hat(amp=1.0, open_=False, seed=503):
    d = 0.18 if open_ else 0.05
    return highpass(noise(d, seed), 7000) * env_exp(d, 0.06 if open_ else 0.012) * amp


def tom(f, amp=1.0):
    d = 0.2
    return sine(sweep_exp(f, f * 0.6, d)) * env_exp(d, 0.06) * amp


# ---------------- 음정 악기 ----------------

def bass(f, dur, amp=1.0):
    x = square(const(f, dur), 0.5) * 0.6 + tri(const(f, dur)) * 0.5
    return lowpass(x, 900) * env_adsr(dur, 0.004, 0.05, 0.75, min(0.04, dur * 0.3)) * amp


def marimba(f, dur, amp=1.0):
    d = max(dur, 0.25)
    return fm(const(f, d), 4.0, 1.4, env_exp(d, 0.03)) * env_exp(d, 0.11) * amp


def pluck(f, dur, amp=1.0):
    """나무 현을 튕기는 소리 (간단한 Karplus-Strong)"""
    d = max(dur, 0.3)
    n = n_samples(d)
    period = max(2, int(SR / f))
    rng = np.random.default_rng(int(f * 10))
    buf = rng.uniform(-1, 1, period)
    out = np.empty(n)
    for i in range(n):
        out[i] = buf[i % period]
        nxt = buf[(i + 1) % period]
        buf[i % period] = 0.996 * 0.5 * (out[i] + nxt)
    return lowpass(out, 4000) * amp


def chip_stab(f, dur, amp=1.0):
    x = square(const(f, dur), 0.25)
    return bitcrush(lowpass(x, 3500), 7) * env_adsr(dur, 0.002, 0.04, 0.4, min(0.03, dur * 0.4)) * amp


def metal_tick(amp=1.0, base=3100):
    d = 0.06
    parts = [(1.0, 0.5), (1.41, 0.3), (2.03, 0.2)]
    return sum(sine(const(base * r, d)) * a for r, a in parts) * env_exp(d, 0.012) * amp


def saw_pad(freqs, dur, amp=1.0, cutoff=1800):
    x = sum(saw(const(f, dur)) + saw(const(f * 1.006, dur)) * 0.8 for f in freqs) / max(1, len(freqs))
    return lowpass(x, cutoff) * env_adsr(dur, 0.15, 0.2, 0.75, min(0.4, dur * 0.3)) * amp


def saw_lead(f, dur, amp=1.0):
    t = np.arange(n_samples(dur)) / SR
    vib = 1 + 0.005 * np.sin(2 * np.pi * 5.5 * t) * np.clip((t - 0.15) / 0.2, 0, 1)
    x = saw(f * vib) * 0.6 + square(f * vib * 0.5, 0.5) * 0.25
    return lowpass(x, 3200) * env_adsr(dur, 0.01, 0.08, 0.7, min(0.12, dur * 0.4)) * amp


def wind(dur, seed, amp=1.0):
    t = np.linspace(0, 1, n_samples(dur))
    from synth import sweep_bandpass
    centers = 500 + 900 * np.sin(np.pi * t)
    return sweep_bandpass(noise(dur, seed), centers, q=3.0) * np.sin(np.pi * t) ** 2 * amp


# ---------------- 믹스 ----------------

def pan_stereo(x, pan):
    """equal-power 패닝 (pan -1 왼쪽 ~ +1 오른쪽) -> (n, 2)"""
    a = (pan + 1) * np.pi / 4
    return np.stack([x * np.cos(a), x * np.sin(a)], axis=1)


def bus(x, drive_amount=1.4):
    """가벼운 버스 새추레이션 (피크를 둥글게)"""
    return np.tanh(x * drive_amount) / np.tanh(drive_amount)


def normalize(x, peak_db=-1.0):
    p = np.max(np.abs(x))
    return x * (10 ** (peak_db / 20) / p) if p > 0 else x


def rms_db(x):
    return 20 * np.log10(max(np.sqrt(np.mean(np.square(x))), 1e-12))
