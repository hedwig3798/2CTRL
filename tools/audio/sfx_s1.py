"""S1 슬라이스 효과음 13종 (변형 포함 21개 파일)을 합성한다. 근거: docs/sound-design.md 6장.
usage: python -I tools/audio/sfx_s1.py <out_root>   (기본 Assets/Audio/SFX)
같은 시드로 항상 같은 파일이 나온다."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *  # noqa: E402,F401,F403

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Audio/SFX'


def note(name):
    """'A4' 같은 음 이름 -> Hz"""
    names = {'C': -9, 'C#': -8, 'D': -7, 'D#': -6, 'E': -5, 'F': -4, 'F#': -3,
             'G': -2, 'G#': -1, 'A': 0, 'A#': 1, 'B': 2}
    return 440.0 * 2 ** ((names[name[:-1]] + 12 * (int(name[-1]) - 4)) / 12)


# ---------------- UI ----------------

def ui_click():
    d = 0.06
    body = tri(sweep_exp(1250, 900, d)) * env_exp(d, 0.014)
    tick = highpass(noise(0.006, 1), 3000) * env_exp(0.006, 0.0015) * 0.35
    return mix(body, tick)


def lvl_select():
    flick = bandpass(noise(0.03, 2), 2000, 6000) * env_exp(0.03, 0.006) * 0.8
    d = 0.08
    blip = tri(sweep_exp(650, 820, d)) * env_exp(d, 0.02)
    return mix(flick, at(blip, 0.008))


def lvl_up():
    # 밝은 3음 상승 (E5, A5, C#6) — 마지막 음만 길게, 비브라토
    parts = []
    notes = [('E5', 0.0, 0.12), ('A5', 0.09, 0.12), ('C#6', 0.18, 0.5)]
    for i, (n, start, dur) in enumerate(notes):
        f = const(note(n), dur)
        if i == 2:
            t = t_axis(dur)
            f = f * (1 + 0.006 * np.sin(2 * np.pi * 6 * t) * np.clip(t / 0.15, 0, 1))
        tone = square(f, 0.25) * 0.45 + sine(f * 2) * 0.25
        env = env_adsr(dur, 0.004, 0.05, 0.6, dur * 0.6)
        parts.append(at(lowpass(tone, 6000) * env, start))
    dry = mix(*parts)
    return echo(dry, 0.09, 0.4, 0.3, taps=2)


# ---------------- Player ----------------

def plr_hit():
    d = 0.15
    thump = sine(sweep_exp(170, 55, d)) * env_exp(d, 0.05)
    crunch = lowpass(noise(d, 3), 1600) * env_exp(d, 0.025) * 0.7
    grit = square(sweep_exp(220, 90, 0.06), 0.5) * env_exp(0.06, 0.015) * 0.18
    return drive(mix(thump, crunch, grit), 1.8)


def plr_death():
    d = 1.2
    boom = sine(sweep_exp(130, 32, 0.9)) * env_exp(0.9, 0.3)
    rumble = lowpass(noise(d, 4), 700) * env_exp(d, 0.25) * 0.8
    fall = square(sweep_exp(440, 98, 1.0), 0.5) * env_adsr(1.0, 0.01, 0.2, 0.5, 0.5) * 0.22
    fall = lowpass(fall, 3000)
    dry = drive(mix(boom, rumble, at(fall, 0.05)), 1.6)
    return echo(dry, 0.12, 0.35, 0.25, taps=3)


def exp_pickup():
    d = 0.06
    f = const(note('G6'), d)
    tone = sine(f) * 0.8 + sine(f * 2) * 0.12
    return tone * env_exp(d, 0.018, attack=0.001)


# ---------------- Weapon ----------------

def wpn_boomerang():
    d = 0.25
    t = np.linspace(0, 1, n_samples(d))
    centers = 700 + 1300 * np.sin(np.pi * t) ** 1.5  # 다가왔다 멀어지는 느낌
    whoosh = sweep_bandpass(noise(d, 5), centers, q=1.6) * env_swell(d, 0.45, 1.6)
    knock = fm(const(note('D5'), 0.05), 3.5, 2.0, env_exp(0.05, 0.01)) * env_exp(0.05, 0.012) * 0.35
    return mix(whoosh * 1.4, knock)


def wpn_laser():
    d = 0.18
    f = sweep_exp(1900, 280, d)
    zap = square(f, 0.5) * 0.6 + square(f * 1.007, 0.5) * 0.3  # 살짝 어긋난 두 사각파
    zap = bitcrush(lowpass(zap, 7000), bits=6, hold=2)
    return zap * env_exp(d, 0.06, attack=0.001)


def wpn_chain():
    d = 0.2
    # 고역(> 6 kHz)을 빼서 잦은 발사에도 귀가 덜 피로하게 한다
    crackle = bandpass(noise(d, 6), 1200, 5500) * gate_random(d, 90, 400, seed=7, duty=0.45)
    rng = np.random.default_rng(8)
    jitter = np.repeat(rng.uniform(0.85, 1.25, 40), n_samples(d) // 40 + 1)[:n_samples(d)]
    buzz = lowpass(saw(110 * jitter), 2500) * 0.45
    return mix(crackle * 1.1, buzz) * env_exp(d, 0.07, attack=0.001)


# ---------------- Hit (변형 3개씩) ----------------

def hit_organic(v):
    d = 0.05
    rng = np.random.default_rng(100 + v)
    f0 = 230 * rng.uniform(0.9, 1.1)
    thump = sine(sweep_exp(f0, f0 * 0.5, d)) * env_exp(d, 0.014)
    pap = lowpass(noise(d, 110 + v), 1200 * rng.uniform(0.85, 1.2)) * env_exp(d, 0.008) * 0.9
    return mix(thump, pap)


def hit_metal(v):
    d = 0.05
    rng = np.random.default_rng(200 + v)
    base = 1750 * rng.uniform(0.92, 1.08)
    partials = [(1.0, 0.5), (1.63, 0.35), (2.37, 0.25), (3.1, 0.12)]  # 비정수배 = 금속
    ring = sum(sine(const(base * r, d)) * a for r, a in partials) * env_exp(d, 0.013, attack=0.0005)
    click = highpass(noise(0.004, 210 + v), 4000) * 0.5
    return mix(ring, click)


def hit_shock(v):
    d = 0.05
    rng = np.random.default_rng(300 + v)
    sizzle = bandpass(noise(d, 310 + v), 1500, 5000) * gate_random(d, 300, 900, seed=320 + v, duty=0.5)
    zap = lowpass(square(sweep_exp(950 * rng.uniform(0.9, 1.1), 600, 0.03), 0.3), 4000) * env_exp(0.03, 0.008) * 0.5
    return mix(sizzle * env_exp(d, 0.016), zap)


# ---------------- Monster ----------------

def mon_die_s(v):
    d = 0.15
    rng = np.random.default_rng(400 + v)
    f0 = 520 * rng.uniform(0.9, 1.12)
    blip = square(sweep_exp(f0, 115, 0.11), 0.5) * env_exp(0.11, 0.035) * 0.45
    blip = bitcrush(lowpass(blip, 4500), bits=6)
    pop = lowpass(noise(d, 410 + v), 1800) * env_exp(d, 0.02) * 0.8
    return mix(pop, blip)


SOUNDS = {
    'UI/UI_CLICK': ui_click,
    'UI/LVL_SELECT': lvl_select,
    'UI/LVL_UP': lvl_up,
    'Player/PLR_HIT': plr_hit,
    'Player/PLR_DEATH': plr_death,
    'Player/EXP_PICKUP': exp_pickup,
    'Weapon/WPN_BOOMERANG': wpn_boomerang,
    'Weapon/WPN_LASER': wpn_laser,
    'Weapon/WPN_CHAIN': wpn_chain,
}
VARIANTS = {
    'Hit/HIT_ORGANIC': hit_organic,
    'Hit/HIT_METAL': hit_metal,
    'Hit/HIT_SHOCK': hit_shock,
    'Monster/MON_DIE_S': mon_die_s,
}


def main():
    written = []
    for rel, fn in SOUNDS.items():
        written.append((rel + '.wav', fn()))
    for rel, fn in VARIANTS.items():
        for v in range(1, 4):
            written.append((f'{rel}_{v:02d}.wav', fn(v)))
    for rel, x in written:
        path = os.path.join(OUT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write_wav(path, finalize(x))
        print(path)


if __name__ == '__main__':
    main()
