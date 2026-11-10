"""S2 효과음 (기획서 6장의 P1 50종)을 합성한다. 근거: docs/sound-design.md 3장(차원 음색), 6장(목록).
usage: python -I tools/audio/sfx_s2.py <out_root>   (기본 Assets/Audio/SFX)
같은 시드로 항상 같은 파일이 나온다. 루프 파일(LOOPS)은 끝과 처음이 이어지게 만든다."""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synth import *  # noqa: E402,F401,F403
from sfx_s1 import note  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Audio/SFX'


# ---------------- 공통 재료 ----------------

def blip(f0, f1, dur, wave='tri', decay=None):
    f = sweep_exp(f0, f1, dur)
    osc = {'tri': tri, 'sine': sine, 'square': square}[wave](f)
    return osc * env_exp(dur, decay or dur * 0.35)


def whoosh(dur, lo, hi, seed, peak=0.5, q=1.5):
    t = np.linspace(0, 1, n_samples(dur))
    centers = lo + (hi - lo) * np.sin(np.pi * t)
    return sweep_bandpass(noise(dur, seed), centers, q=q) * env_swell(dur, peak, 1.6)


def explosion(dur, seed, low=110, lp=1400, crackle=0.0):
    """노이즈 폭발 + 저역 펀치. crackle > 0 이면 전기 튀김을 섞는다"""
    body = lowpass(noise(dur, seed), lp) * env_exp(dur, dur * 0.28, attack=0.001)
    punch = sine(sweep_exp(low, 35, dur * 0.7)) * env_exp(dur * 0.7, dur * 0.18)
    parts = [body * 0.9, punch]
    if crackle > 0:
        parts.append(bandpass(noise(dur, seed + 1), 1500, 5000)
                     * gate_random(dur, 40, 160, seed + 2, 0.4) * env_exp(dur, dur * 0.3) * crackle)
    return drive(mix(*parts), 1.5)


def metal_ring(base, dur, decay, partials=((1.0, 0.5), (1.63, 0.35), (2.37, 0.25), (3.1, 0.12))):
    return sum(sine(const(base * r, dur)) * a for r, a in partials) * env_exp(dur, decay, attack=0.0005)


def marimba(freq, dur, decay=0.12):
    return fm(const(freq, dur), 4.0, 1.6, env_exp(dur, decay * 0.4)) * env_exp(dur, decay)


def chord(notes, dur, voice, spread=0.0):
    """notes 를 spread 초 간격으로 겹쳐 친다. voice(freq, dur) -> 배열"""
    return mix(*[at(voice(note(n), dur), i * spread) for i, n in enumerate(notes)])


def sparkle(dur, seed, count=6, lo=2500, hi=5000):
    rng = np.random.default_rng(seed)
    parts = []
    for _ in range(count):
        f = rng.uniform(lo, hi)
        st = rng.uniform(0, dur * 0.6)
        parts.append(at(sine(const(f, 0.08)) * env_exp(0.08, 0.02) * 0.3, st))
    return mix(*parts)


# ---------------- UI ----------------

def ui_back():
    return blip(900, 560, 0.08)


def ui_denied():
    b = lowpass(square(const(185, 0.05), 0.5), 2500) * env_exp(0.05, 0.03)
    return mix(b, at(b, 0.07))


def ui_slider():
    return sine(const(1000, 0.05)) * env_exp(0.05, 0.012)


def ui_start():
    d = 0.5
    arp = mix(*[at(lowpass(square(const(note(n), 0.14), 0.25), 5000) * env_exp(0.14, 0.06) * 0.6, i * 0.07)
                for i, n in enumerate(['A4', 'C#5', 'E5', 'A5', 'C#6'])])
    rise = sweep_bandpass(noise(d, 20), sweep_exp(400, 4000, d), q=2.0) * env_swell(d, 0.8, 1.2) * 0.6
    return mix(arp, rise)


def dim_cursor():
    return blip(950, 950, 0.05, decay=0.012)


def dim_cancel():
    return blip(620, 360, 0.12)


def dim_confirm_forest():
    return chord(['A4', 'C5', 'E5', 'A5'], 0.4, lambda f, d: marimba(f, d, 0.14), spread=0.03)


def dim_confirm_factory():
    tones = chord(['A3', 'E4', 'A4'], 0.4, lambda f, d: lowpass(square(const(f, d), 0.5), 3000)
                  * env_exp(d, 0.15) * 0.5)
    clank = metal_ring(1300, 0.15, 0.03) * 0.6
    return mix(bitcrush(tones, 7), clank)


def dim_confirm_storm():
    tones = chord(['A3', 'E4', 'C5'], 0.4, lambda f, d: lowpass(saw(const(f, d)), 3500) * env_exp(d, 0.15) * 0.5)
    zap = bandpass(noise(0.25, 21), 1500, 5000) * gate_random(0.25, 60, 250, 22, 0.4) * env_exp(0.25, 0.08) * 0.5
    return mix(tones, zap)


def dim_ready():
    d = 0.9
    rise = sweep_bandpass(noise(d, 23), sweep_exp(300, 5000, d), q=2.5) * np.linspace(0, 1, n_samples(d)) ** 2
    tone = lowpass(saw(sweep_exp(110, 440, d)), 2500) * np.linspace(0, 1, n_samples(d)) ** 2 * 0.35
    hit = explosion(0.5, 24, low=90, lp=2000) * 0.9
    sting = chord(['A3', 'E4', 'A4', 'C#5'], 0.5, lambda f, dd: square(const(f, dd), 0.25) * env_exp(dd, 0.2) * 0.25)
    return mix(rise * 0.7, tone, at(hit, d), at(lowpass(sting, 5000), d))


# ---------------- 레벨업 · 진행 ----------------

def lvl_weapon():
    ring = metal_ring(1500, 0.4, 0.12) * 0.5
    ch = chord(['A4', 'C#5', 'E5'], 0.4, lambda f, d: lowpass(square(const(f, d), 0.25), 5000)
               * env_adsr(d, 0.004, 0.08, 0.5, 0.25) * 0.35, spread=0.04)
    return mix(ring, at(ch, 0.02))


def lvl_upgrade():
    a = lowpass(square(const(note('A5'), 0.1), 0.25), 6000) * env_exp(0.1, 0.05)
    b = lowpass(square(const(note('E6'), 0.16), 0.25), 6000) * env_exp(0.16, 0.07)
    return mix(a * 0.6, at(b * 0.6, 0.08))


def lvl_star():
    return mix(lvl_upgrade(), at(sparkle(0.45, 25, count=8), 0.1), at(sine(const(note('A6'), 0.35)) * env_exp(0.35, 0.12) * 0.25, 0.16))


def lvl_buff():
    d = 0.25
    rise = tri(sweep_exp(420, 940, d)) * env_adsr(d, 0.01, 0.05, 0.7, 0.12) * 0.7
    chime = sine(const(note('E6'), 0.15)) * env_exp(0.15, 0.05) * 0.35
    return mix(rise, at(chime, 0.12))


def lvl_debuff():
    d = 0.25
    fall = lowpass(square(sweep_exp(420, 140, d), 0.5), 1800) * env_adsr(d, 0.01, 0.05, 0.7, 0.12) * 0.6
    return fall


def pause_open():
    d = 0.15
    x = mix(noise(d, 26) * 0.4, saw(const(220, d)) * 0.3)
    return sweep_bandpass(x, sweep_exp(4000, 300, d), q=1.2) * env_exp(d, 0.08)


def pause_close():
    d = 0.15
    x = mix(noise(d, 27) * 0.4, saw(const(220, d)) * 0.3)
    return sweep_bandpass(x, sweep_exp(300, 4000, d), q=1.2) * env_swell(d, 0.7, 1.2)


def fanfare_voice(f, d):
    return lowpass(mix(square(const(f, d), 0.25) * 0.4, saw(const(f * 1.003, d)) * 0.25), 4500) \
        * env_adsr(d, 0.01, 0.1, 0.7, min(0.3, d * 0.4))


def result_win():
    seq = [('A4', 0.0, 0.18), ('C#5', 0.18, 0.18), ('E5', 0.36, 0.18), ('A5', 0.54, 0.9)]
    lead = mix(*[at(fanfare_voice(note(n), d), s) for n, s, d in seq])
    pad = at(chord(['A3', 'E4', 'A4', 'C#5'], 1.6, lambda f, d: fanfare_voice(f, d) * 0.4), 0.54)
    drum = mix(*[at(explosion(0.25, 28 + i, low=120, lp=900) * 0.5, s) for i, s in enumerate([0.0, 0.36, 0.54])])
    return echo(mix(lead, pad, drum), 0.15, 0.35, 0.3, taps=3)


def result_lose():
    seq = [('E5', 0.0, 0.3), ('C5', 0.3, 0.3), ('A4', 0.6, 0.35), ('A3', 0.95, 1.3)]
    lead = mix(*[at(lowpass(fanfare_voice(note(n), d), 2500), s) for n, s, d in seq])
    boom = at(explosion(0.8, 31, low=70, lp=500) * 0.6, 0.95)
    return echo(mix(lead, boom), 0.2, 0.3, 0.25, taps=2)


def horn(f, d):
    x = mix(saw(const(f, d)), saw(const(f * 1.005, d)) * 0.7, saw(const(f * 1.5, d)) * 0.4)
    return lowpass(x, 900) * env_adsr(d, 0.04, 0.1, 0.8, d * 0.4)


def wave_start():
    return horn(note('A2'), 0.8)


def alarm(count, seed_gap=0.18):
    parts = []
    for i in range(count):
        f = 880 if i % 2 == 0 else 660
        parts.append(at(lowpass(square(const(f, seed_gap * 0.9), 0.5), 4000) * env_adsr(seed_gap * 0.9, 0.005, 0.02, 0.8, 0.03) * 0.4,
                        i * seed_gap))
    return mix(*parts)


def snare(seed):
    d = 0.12
    return mix(bandpass(noise(d, seed), 1200, 6000) * env_exp(d, 0.04), sine(const(190, d)) * env_exp(d, 0.03) * 0.5)


def wave_rush():
    fill = mix(*[at(snare(40 + i), 0.75 + i * 0.09) for i in range(6)])
    return mix(alarm(4), at(horn(note('A2'), 0.9) * 0.6, 0.55), fill * 0.7)


def wave_final():
    boom = at(explosion(0.9, 50, low=60, lp=700), 1.1)
    return mix(alarm(6, 0.17), at(horn(note('E2'), 1.2) * 0.8, 0.5), boom * 0.8)


# ---------------- 플레이어 ----------------

def plr_low_hp_loop():
    # 심장 박동 1.0초 루프 (lub-dub). 끝에 박동이 없으므로 이음새가 자연스럽다
    def beat(f, amp):
        d = 0.16
        return lowpass(sine(sweep_exp(f, f * 0.6, d)) * env_exp(d, 0.05), 300) * amp
    x = mix(beat(62, 1.0), at(beat(55, 0.7), 0.24), np.zeros(n_samples(1.0)))
    return x[:n_samples(1.0)]


# ---------------- 무기: 숲 ----------------

def wpn_poison_throw():
    return whoosh(0.15, 500, 1600, 60, peak=0.3) * 1.2


def wpn_poison_land():
    d = 0.35
    splash = lowpass(noise(0.12, 61), 1800) * env_exp(0.12, 0.03)
    sizzle = bandpass(noise(d, 62), 2500, 6000) * gate_random(d, 200, 600, 63, 0.5) * env_exp(d, 0.12) * 0.35
    bubble = mix(*[at(sine(sweep_exp(300 + 120 * i, 700 + 120 * i, 0.04)) * env_exp(0.04, 0.012) * 0.3, 0.06 + i * 0.07)
                   for i in range(3)])
    return mix(splash, sizzle, bubble)


def wpn_sweep():
    d = 0.18
    cut = sweep_bandpass(noise(d, 64), sweep_exp(1500, 4500, d), q=2.0) * env_swell(d, 0.3, 1.4)
    grass = bandpass(noise(d, 65), 3000, 7000) * gate_random(d, 150, 400, 66, 0.6) * env_swell(d, 0.35, 1.4) * 0.4
    return mix(cut, grass)


def wpn_split_fire():
    pop = sine(sweep_exp(380, 720, 0.07)) * env_exp(0.07, 0.025)
    click = highpass(noise(0.005, 67), 2500) * 0.4
    return mix(pop, click)


def wpn_split_burst():
    burst = lowpass(noise(0.08, 68), 2500) * env_exp(0.08, 0.02)
    pops = mix(*[at(sine(sweep_exp(500 + 150 * i, 900 + 150 * i, 0.04)) * env_exp(0.04, 0.012) * 0.4, 0.02 + i * 0.035)
                 for i in range(4)])
    return mix(burst, pops)


def wpn_spirit_spawn():
    d = 0.3
    bell = fm(const(note('E6'), d), 3.5, 1.2, env_exp(d, 0.08)) * env_exp(d, 0.12)
    return mix(bell * 0.7, sparkle(0.3, 69, count=4, lo=3000, hi=5000))


def wpn_root():
    d = 0.4
    rng = np.random.default_rng(70)
    wobble = np.repeat(rng.uniform(0.8, 1.3, 30), n_samples(d) // 30 + 1)[:n_samples(d)]
    creak = lowpass(saw(sweep_exp(70, 120, d) * wobble), 900) * env_swell(d, 0.5, 0.6) * 0.6
    crunch = lowpass(noise(d, 71), 1500) * env_swell(d, 0.6, 0.6) * 0.6
    crack = lowpass(noise(0.03, 99), 2500) * env_exp(0.03, 0.008) * 0.5
    thud = at(sine(sweep_exp(110, 50, 0.15)) * env_exp(0.15, 0.04), 0.22)
    return mix(crack, creak, crunch, thud)


# ---------------- 무기: 공장 ----------------

def wpn_drone():
    return bitcrush(lowpass(square(sweep_exp(1300, 800, 0.06), 0.5), 6000), 6) * env_exp(0.06, 0.018)


def wpn_mine_set():
    return mix(metal_ring(900, 0.12, 0.02), highpass(noise(0.006, 73), 3000) * 0.5,
               at(metal_ring(1400, 0.08, 0.015) * 0.6, 0.045))


def wpn_mine_boom():
    return mix(explosion(0.5, 74, low=120, lp=1800), metal_ring(700, 0.4, 0.09) * 0.25)


def wpn_saw_loop():
    d, xf = 1.0, 0.08
    total = d + xf
    t = t_axis(total)
    am = 0.65 + 0.35 * np.sin(2 * np.pi * 24 * t)  # 톱니가 지나가는 떨림
    whine = mix(square(const(1100, total), 0.3) * 0.25, sine(const(2200, total)) * 0.3)
    grind = bandpass(noise(total, 75), 2000, 5000) * 0.4
    return make_loop(lowpass(mix(whine, grind), 6000) * am, d, xf)


def wpn_flame_loop():
    d, xf = 1.0, 0.1
    total = d + xf
    roar = lowpass(noise(total, 76), 1400)
    rumble = lowpass(noise(total, 77), 200) * 1.5
    crack = bandpass(noise(total, 78), 1500, 4000) * gate_random(total, 10, 40, 79, 0.15) * 0.5
    return make_loop(mix(roar, rumble, crack), d, xf)


def wpn_spinblade_tell():
    d = 0.8
    f = sweep_exp(60, 380, d)
    t = t_axis(d)
    am = 0.6 + 0.4 * np.sin(phase(sweep_exp(4, 30, d)))
    motor = lowpass(saw(f), 2500) * am * np.linspace(0.2, 1, n_samples(d))
    return fade(motor, 0.01, 0.05)


def wpn_spinblade_loop():
    d, xf = 1.0, 0.08
    total = d + xf
    t = t_axis(total)
    am = 0.7 + 0.3 * np.sin(2 * np.pi * 30 * t)
    whir = lowpass(mix(saw(const(380, total)) * 0.5, saw(const(383, total)) * 0.3), 3000)
    air = bandpass(noise(total, 80), 1500, 4500) * 0.3
    return make_loop(mix(whir, air) * am, d, xf)


# ---------------- 무기: 폭풍 ----------------

def wpn_shockwave():
    d = 0.5
    whoom = sine(sweep_exp(95, 38, d)) * env_swell(d, 0.15, 1.2)
    air = lowpass(noise(d, 81), 600) * env_swell(d, 0.2, 1.4) * 0.8
    return drive(mix(whoom, air), 1.4)


def wpn_explosive_fire():
    return mix(sine(sweep_exp(300, 120, 0.12)) * env_exp(0.12, 0.035), lowpass(noise(0.05, 82), 1200) * env_exp(0.05, 0.012) * 0.6)


def wpn_explosive_boom():
    return explosion(0.5, 83, low=100, lp=1600, crackle=0.5)


def wpn_tornado_loop():
    d, xf = 2.0, 0.15
    total = d + xf
    t = t_axis(total)
    # 중심 주파수를 d 주기로 흔들어 루프 길이와 맞춘다
    centers = 900 + 500 * np.sin(2 * np.pi * t / d) + 200 * np.sin(2 * np.pi * 3 * t / d)
    wind = sweep_bandpass(noise(total, 84), centers, q=2.5)
    rumble = lowpass(noise(total, 85), 180) * 0.8
    return make_loop(mix(wind * 1.3, rumble), d, xf)


def wpn_lightning_mark():
    d = 0.45
    charge = lowpass(saw(sweep_exp(180, 1100, d)), 3000) * np.linspace(0.2, 1, n_samples(d)) * 0.45
    crackle = bandpass(noise(d, 86), 1500, 5000) * gate_random(d, 30, 300, 87, 0.4) * np.linspace(0, 1, n_samples(d)) * 0.6
    return fade(mix(charge, crackle), 0.01, 0.02)


def wpn_lightning_strike():
    d = 0.3
    crack = bandpass(noise(0.06, 88), 1200, 6000) * env_exp(0.06, 0.012)
    thunder = lowpass(noise(d, 89), 900) * env_exp(d, 0.09) * 0.9
    boom = sine(sweep_exp(110, 40, d)) * env_exp(d, 0.07)
    return drive(mix(crack * 1.2, thunder, boom), 1.6)


def wpn_stormorb_loop():
    d, xf = 1.0, 0.08
    total = d + xf
    hum = lowpass(mix(saw(const(110, total)) * 0.4, saw(const(110.5, total)) * 0.4), 800)
    fizz = bandpass(noise(total, 90), 2000, 5000) * gate_random(total, 20, 80, 91, 0.3) * 0.15
    return make_loop(mix(hum, fizz), d, xf)


# ---------------- 몬스터 ----------------

def mon_die_m():
    d = 0.22
    blip_ = bitcrush(lowpass(square(sweep_exp(380, 80, 0.18), 0.5), 3500), 6) * env_exp(0.18, 0.06) * 0.5
    pop = lowpass(noise(d, 92), 1200) * env_exp(d, 0.035)
    return mix(pop, blip_)


def mon_die_l():
    d = 0.6
    rng = np.random.default_rng(93)
    rocks = mix(*[at(lowpass(noise(0.08, 94 + i), rng.uniform(500, 1100)) * env_exp(0.08, 0.02) * rng.uniform(0.4, 0.9),
                     rng.uniform(0, 0.4)) for i in range(9)])
    thump = sine(sweep_exp(80, 40, 0.4)) * env_exp(0.4, 0.12)
    return mix(rocks, thump)


def mon_bomber_tell():
    times = [0.0, 0.2, 0.34, 0.45, 0.53, 0.59]
    return mix(*[at(lowpass(square(const(1400, 0.04), 0.5), 5000) * env_exp(0.04, 0.02) * 0.5, s) for s in times])


def mon_bomber_boom():
    return explosion(0.6, 104, low=90, lp=2200)


def mon_teleport():
    d = 0.25
    tone = sine(sweep_exp(300, 1500, d)) * env_swell(d, 0.7, 0.7) * 0.5
    air = sweep_bandpass(noise(d, 105), sweep_exp(800, 4000, d), q=2.0) * env_swell(d, 0.7, 0.7)
    return mix(tone, air)


def mon_summon():
    d = 0.6
    x = mix(saw(const(note('A2'), d)), saw(const(note('A2') * 1.01, d)), saw(const(note('E3'), d)) * 0.5)
    t = np.linspace(0, 1, n_samples(d))
    vowel = sweep_bandpass(x, 500 + 400 * np.sin(np.pi * t), q=3.0)  # "오-아-오" 모음 느낌
    return vowel * env_adsr(d, 0.08, 0.1, 0.8, 0.2)


def mon_revive():
    d = 0.5
    ramp = 0.15 + 0.85 * np.linspace(0, 1, n_samples(d)) ** 2
    swell = sweep_bandpass(noise(d, 106), sweep_exp(500, 3000, d), q=2.0) * ramp
    tone = tri(sweep_exp(200, 800, d)) * ramp * 0.5
    return fade(mix(swell, tone), 0.01, 0.012)


SOUNDS = {
    'UI/UI_BACK': ui_back, 'UI/UI_DENIED': ui_denied, 'UI/UI_SLIDER': ui_slider, 'UI/UI_START': ui_start,
    'UI/DIM_CURSOR': dim_cursor, 'UI/DIM_CANCEL': dim_cancel, 'UI/DIM_READY': dim_ready,
    'UI/DIM_CONFIRM_FOREST': dim_confirm_forest, 'UI/DIM_CONFIRM_FACTORY': dim_confirm_factory,
    'UI/DIM_CONFIRM_STORM': dim_confirm_storm,
    'UI/LVL_WEAPON': lvl_weapon, 'UI/LVL_UPGRADE': lvl_upgrade, 'UI/LVL_STAR': lvl_star,
    'UI/LVL_BUFF': lvl_buff, 'UI/LVL_DEBUFF': lvl_debuff, 'UI/PAUSE_OPEN': pause_open, 'UI/PAUSE_CLOSE': pause_close,
    'UI/RESULT_WIN': result_win, 'UI/RESULT_LOSE': result_lose,
    # WAVE_START / WAVE_RUSH / WAVE_FINAL 은 쓰지 않는다 (웨이브 전환 무음, 2026-11-10). 함수는 남겨 둔다
    'Weapon/WPN_POISON_THROW': wpn_poison_throw, 'Weapon/WPN_POISON_LAND': wpn_poison_land,
    'Weapon/WPN_SWEEP': wpn_sweep, 'Weapon/WPN_SPLIT_FIRE': wpn_split_fire, 'Weapon/WPN_SPLIT_BURST': wpn_split_burst,
    'Weapon/WPN_SPIRIT_SPAWN': wpn_spirit_spawn, 'Weapon/WPN_ROOT': wpn_root,
    'Weapon/WPN_DRONE': wpn_drone, 'Weapon/WPN_MINE_SET': wpn_mine_set, 'Weapon/WPN_MINE_BOOM': wpn_mine_boom,
    'Weapon/WPN_SPINBLADE_TELL': wpn_spinblade_tell,
    'Weapon/WPN_SHOCKWAVE': wpn_shockwave, 'Weapon/WPN_EXPLOSIVE_FIRE': wpn_explosive_fire,
    'Weapon/WPN_EXPLOSIVE_BOOM': wpn_explosive_boom,
    'Weapon/WPN_LIGHTNING_MARK': wpn_lightning_mark, 'Weapon/WPN_LIGHTNING_STRIKE': wpn_lightning_strike,
    'Monster/MON_DIE_M': mon_die_m, 'Monster/MON_DIE_L': mon_die_l, 'Monster/MON_BOMBER_TELL': mon_bomber_tell,
    'Monster/MON_BOMBER_BOOM': mon_bomber_boom, 'Monster/MON_TELEPORT': mon_teleport,
    'Monster/MON_SUMMON': mon_summon, 'Monster/MON_REVIVE': mon_revive,
}
LOOPS = {
    'Player/PLR_LOW_HP': plr_low_hp_loop,
    'Weapon/WPN_SAW_LOOP': wpn_saw_loop, 'Weapon/WPN_FLAME_LOOP': wpn_flame_loop,
    'Weapon/WPN_SPINBLADE_LOOP': wpn_spinblade_loop, 'Weapon/WPN_TORNADO_LOOP': wpn_tornado_loop,
    'Weapon/WPN_STORMORB_LOOP': wpn_stormorb_loop,
}


def main():
    for rel, fn in SOUNDS.items():
        path = os.path.join(OUT, rel + '.wav')
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write_wav(path, finalize(fn()))
    for rel, fn in LOOPS.items():
        path = os.path.join(OUT, rel + '.wav')
        os.makedirs(os.path.dirname(path), exist_ok=True)
        write_wav(path, finalize_loop(fn()))
    print(len(SOUNDS) + len(LOOPS), 'files')


if __name__ == '__main__':
    main()
