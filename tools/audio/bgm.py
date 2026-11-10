"""S3 BGM 9개를 합성한다. 근거: docs/sound-design.md 5장. 시안: bgm_sketch.py (승인 2026-11-10).
- 전투용 7개 (BGM_BASE_LOW/MID/HIGH, BGM_RUSH, BGM_LAYER_FOREST/FACTORY/STORM): 128 BPM, 32마디(60초),
  샘플 길이가 정확히 같은 루프. 게임에서 같은 DSP 시각에 동시에 재생하고 볼륨으로 섞는다.
- BGM_TITLE, BGM_SELECT: 96 BPM, 16마디(40초) 단독 루프.
- 모두 mono. 음량은 파일끼리 상대 크기를 지키도록 RMS 목표로 맞춘다 (피크 정규화하지 않음).
usage: python -I tools/audio/bgm.py <out_dir>   (기본 Assets/Audio/BGM)"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from music import *  # noqa: E402,F401,F403
from synth import echo, write_wav  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else 'Assets/Audio/BGM'

# A(8) - A'(8) - B(8) - A''(8). 마지막 4마디는 E 로 긴장을 만들고 처음 Am 으로 돌아간다
PROG = ['Am', 'Am', 'F', 'F', 'C', 'C', 'G', 'G',
        'Am', 'Am', 'F', 'F', 'C', 'C', 'G', 'G',
        'F', 'F', 'G', 'G', 'Am', 'Am', 'Em', 'Em',
        'F', 'F', 'G', 'G', 'C', 'G', 'E', 'E']
COMBAT = Song(bars=len(PROG), bpm=128)
BPB = 4


def bar(i):
    return i * BPB


def crash(amp=1.0, seed=530):
    d = 1.4
    return highpass(noise(d, seed), 5000) * env_exp(d, 0.45) * amp


# ---------------- 베이스(드럼 + 베이스) 강도별 ----------------

def bass_line(t, song, prog, pattern_fn, amp=0.55):
    for i, ch in enumerate(prog):
        root = hz(f'{CHORDS[ch][0]}2')
        for beat, f, length in pattern_fn(root):
            t.add(bar(i) + beat, bass(f, length * song.beat_sec * 0.9), amp)


def base_low():
    t = COMBAT.track()
    for i, _ in enumerate(PROG):
        b = bar(i)
        t.add(b, kick(), 0.8)
        t.add(b + 2, kick(), 0.7)
        t.add(b + 3, snare(seed=540 + i % 4), 0.3)
        for k in range(4):
            t.add(b + k, hat(seed=550 + k), 0.1)
    bass_line(t, COMBAT, PROG, lambda r: [(0, r, 2), (2, r, 1.5), (3.5, r * 1.5, 0.5)], 0.5)
    return t.render()


def base_mid():
    t = COMBAT.track()
    for i, _ in enumerate(PROG):
        b = bar(i)
        fill = i % 8 == 7
        for k in (0, 1.5, 2):
            t.add(b + k, kick(), 0.9)
        t.add(b + 1, snare(), 0.55)
        if not fill:
            t.add(b + 3, snare(seed=504), 0.55)
        else:
            for j, k in enumerate([3, 3.25, 3.5, 3.75]):
                t.add(b + k, snare(seed=510 + j), 0.35 + 0.08 * j)
        for k in range(8):
            t.add(b + k * 0.5, hat(open_=(k == 7 and not fill), seed=520 + k), 0.18 if k % 2 else 0.12)
    bass_line(t, COMBAT, PROG, lambda r: [(k * 0.5, f, 0.5) for k, f in
                                          enumerate([r, r, r * 2, r, r, r * 2, r * 1.5, r])])
    return t.render()


def base_high():
    t = COMBAT.track()
    for i, _ in enumerate(PROG):
        b = bar(i)
        fill = i % 8 == 7
        if i % 8 == 0:
            t.add(b, crash(), 0.25)
        for k in range(4):
            t.add(b + k, kick(), 0.9)
        t.add(b + 1, snare(), 0.6)
        t.add(b + 3, snare(seed=504), 0.6)
        t.add(b + 2.75, snare(seed=505), 0.15)  # 고스트 노트
        for k in range(16):
            acc = 0.2 if k % 4 == 2 else 0.11
            t.add(b + k * 0.25, hat(open_=(k % 4 == 2), seed=560 + k), acc)
        if fill:
            for j, (k, f) in enumerate([(2, 220), (2.5, 180), (3, 150), (3.5, 120)]):
                t.add(b + k, tom(f), 0.5)
    bass_line(t, COMBAT, PROG, lambda r: [(k * 0.5, f, 0.5) for k, f in
                                          enumerate([r, r * 2, r, r * 2, r, r * 2, r * 1.5, r * 2])], 0.6)
    return t.render()


def base_rush():
    t = COMBAT.track()
    for i, _ in enumerate(PROG):
        b = bar(i)
        if i % 4 == 0:
            t.add(b, crash(seed=531 + i), 0.3)
        for k in range(4):
            t.add(b + k, kick(), 0.95)
        t.add(b + 3.75, kick(), 0.6)
        for k in range(4):
            t.add(b + k + 0.5, snare(seed=570 + k), 0.45)  # 매 박 뒷박 스네어: 몰아치는 느낌
        for k in range(16):
            t.add(b + k * 0.25, hat(seed=580 + k), 0.14 if k % 2 else 0.2)
        if i % 4 == 3:
            for j in range(8):
                t.add(b + 2 + j * 0.25, snare(seed=590 + j), 0.2 + 0.05 * j)
    bass_line(t, COMBAT, PROG, lambda r: [(k * 0.25, r * (2 if k % 4 == 2 else 1), 0.25) for k in range(16)], 0.5)
    return t.render()


# ---------------- 차원 레이어 ----------------

def layer_forest():
    t = COMBAT.track()
    shape = [0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1, 2, 3]
    for i, ch in enumerate(PROG):
        tones = chord_notes(ch, 4, 5)
        b_section = 16 <= i < 24
        for k, idx in enumerate(shape):
            if b_section and k % 2 == 1:
                continue  # B 구간은 8분으로 숨을 돌린다
            accent = 1.0 if k % 4 == 0 else 0.7
            t.add(bar(i) + k * 0.25, marimba(tones[idx], 0.2), 0.32 * accent)
        if i % 2 == 0:
            t.add(bar(i), pluck(chord_notes(ch, 3, 1)[0], 1.2), 0.35)
    return t.render()


def layer_factory():
    t = COMBAT.track()
    for i, ch in enumerate(PROG):
        tones = chord_notes(ch, 3, 3)
        for k in range(4):
            for f in tones:
                t.add(bar(i) + k + 0.5, chip_stab(f, COMBAT.beat_sec * 0.3), 0.16)
        for k in range(16):
            amp = 0.22 if k in (6, 14) else (0.1 if k % 2 else 0.0)
            if amp > 0:
                t.add(bar(i) + k * 0.25, metal_tick(), amp)
        if i % 4 == 3:
            seq = chord_notes(ch, 4, 4)
            for k, f in enumerate(seq + seq[::-1]):
                t.add(bar(i) + k * 0.5, chip_stab(f, COMBAT.beat_sec * 0.4), 0.14)
    return t.render()


PHRASES = {
    'Am': [(0, 1, 'E5'), (1, 0.5, 'D5'), (1.5, 0.5, 'C5'), (2, 2, 'D5'), (4, 3.5, 'A4')],
    'F': [(0, 1, 'F5'), (1, 0.5, 'E5'), (1.5, 0.5, 'C5'), (2, 2, 'A4'), (4, 3.5, 'C5')],
    'C': [(0, 1, 'G5'), (1, 0.5, 'E5'), (1.5, 0.5, 'C5'), (2, 2, 'E5'), (4, 3.5, 'D5')],
    'G': [(0, 1, 'D5'), (1, 0.5, 'B4'), (1.5, 0.5, 'G4'), (2, 2, 'B4'), (4, 3.5, 'D5')],
    'Em': [(0, 1, 'B4'), (1, 0.5, 'G4'), (1.5, 0.5, 'E4'), (2, 2, 'G4'), (4, 3.5, 'B4')],
}
# B 구간은 긴 음으로 대비를 준다
PHRASES_B = {
    'F': [(0, 4, 'A5'), (4, 3.5, 'G5')], 'G': [(0, 4, 'B5'), (4, 3.5, 'A5')],
    'Am': [(0, 4, 'C6'), (4, 3.5, 'E5')], 'Em': [(0, 4, 'B5'), (4, 2, 'G5'), (6, 1.5, 'E5')],
}
ENDING = [(0, 2, 'E5'), (2, 2, 'D5'), (4, 2, 'B4'), (6, 1, 'G#4'), (7, 1, 'B4'), (8, 7.5, 'E5')]
ENDING_BAR = 28


def layer_storm():
    t = COMBAT.track()
    lead = COMBAT.track()
    for i in range(0, len(PROG), 2):
        t.add(bar(i), saw_pad(chord_notes(PROG[i], 3, 3), COMBAT.beat_sec * BPB * 2), 0.2)
    for i in range(0, ENDING_BAR, 2):
        table = PHRASES_B if 16 <= i < 24 else PHRASES
        for s, d, n in table[PROG[i]]:
            lead.add(bar(i) + s, saw_lead(hz(n), d * COMBAT.beat_sec), 0.22)
    for s, d, n in ENDING:
        lead.add(bar(ENDING_BAR) + s, saw_lead(hz(n), d * COMBAT.beat_sec), 0.22)
    for i in range(0, len(PROG), 4):
        t.add(bar(i), wind(COMBAT.beat_sec * BPB * 4, 600 + i), 0.18)
    wet = echo(lead.render(), COMBAT.beat_sec * 0.75, 0.35, 0.35, taps=3)
    # echo 꼬리도 처음으로 감아 루프 이음새를 맞춘다
    n = COMBAT.length
    looped = wet[:n].copy()
    looped[:len(wet) - n] += wet[n:]
    return t.render() + looped


# ---------------- 타이틀 / 차원 선택 (96 BPM) ----------------

MENU_PROG = ['Am', 'Am', 'F', 'F', 'C', 'C', 'G', 'G', 'Am', 'Am', 'F', 'F', 'G', 'G', 'E', 'E']
MENU = Song(bars=len(MENU_PROG), bpm=96)
MENU_MELODY = [  # (마디, 박, 길이, 음) — 숲 마림바로 연주하는 테마
    (0, 0, 1.5, 'E5'), (0, 1.5, 0.5, 'D5'), (0, 2, 2, 'C5'), (1, 0, 3, 'A4'),
    (2, 0, 1.5, 'F5'), (2, 1.5, 0.5, 'E5'), (2, 2, 2, 'C5'), (3, 0, 3, 'A4'),
    (4, 0, 1.5, 'G5'), (4, 1.5, 0.5, 'E5'), (4, 2, 2, 'C5'), (5, 0, 3, 'E5'),
    (6, 0, 1.5, 'D5'), (6, 1.5, 0.5, 'B4'), (6, 2, 2, 'G4'), (7, 0, 3, 'B4'),
    (8, 0, 1.5, 'E5'), (8, 1.5, 0.5, 'D5'), (8, 2, 2, 'C5'), (9, 0, 3, 'A4'),
    (10, 0, 1.5, 'F5'), (10, 1.5, 0.5, 'E5'), (10, 2, 2, 'A5'), (11, 0, 3, 'C6'),
    (12, 0, 2, 'B5'), (12, 2, 2, 'G5'), (13, 0, 3, 'D5'),
    (14, 0, 2, 'E5'), (14, 2, 2, 'G#5'), (15, 0, 3.5, 'B5'),
]


def menu_common(t):
    for i in range(0, len(MENU_PROG), 2):
        t.add(i * BPB, saw_pad(chord_notes(MENU_PROG[i], 3, 3), MENU.beat_sec * BPB * 2, cutoff=1200), 0.22)
    for bar_i, beat, length, n in MENU_MELODY:
        f = hz(n)
        # 마림바를 8분으로 굴려 긴 음을 표현한다 (트레몰로)
        steps = max(1, int(length / 0.5))
        for s in range(steps):
            t.add(bar_i * BPB + beat + s * 0.5, marimba(f, 0.25), 0.3 if s == 0 else 0.16)
    for i, ch in enumerate(MENU_PROG):
        for k in range(4):
            f = chord_notes(ch, 4, 3)[k % 3]
            t.add(i * BPB + k + 0.5, chip_stab(f, MENU.beat_sec * 0.2), 0.05)
    for i in range(0, len(MENU_PROG), 4):
        t.add(i * BPB, wind(MENU.beat_sec * BPB * 4, 700 + i), 0.12)


def bgm_title():
    t = MENU.track()
    menu_common(t)
    for i in range(0, len(MENU_PROG), 2):
        t.add(i * BPB, pluck(chord_notes(MENU_PROG[i], 2, 1)[0], 2.5), 0.4)
    return t.render()


def bgm_select():
    t = MENU.track()
    menu_common(t)
    for i, ch in enumerate(MENU_PROG):
        b = i * BPB
        t.add(b, kick(), 0.6)
        t.add(b + 2, kick(), 0.5)
        t.add(b + 3, snare(seed=600 + i % 4), 0.22)
        for k in range(8):
            t.add(b + k * 0.5, hat(seed=610 + k), 0.08 if k % 2 else 0.05)
        root = hz(f'{CHORDS[ch][0]}2')
        t.add(b, bass(root, MENU.beat_sec * 1.8), 0.4)
        t.add(b + 2, bass(root, MENU.beat_sec * 1.4), 0.35)
        t.add(b + 3.5, bass(root * 1.5, MENU.beat_sec * 0.45), 0.3)
    return t.render()


# 파일, 함수, 목표 RMS(dB). 강도가 높을수록 조금 크게.
TRACKS = [
    ('BGM_BASE_LOW', base_low, -19.0),
    ('BGM_BASE_MID', base_mid, -17.0),
    ('BGM_BASE_HIGH', base_high, -16.0),
    ('BGM_RUSH', base_rush, -16.0),
    ('BGM_LAYER_FOREST', layer_forest, -18.0),
    ('BGM_LAYER_FACTORY', layer_factory, -18.0),
    ('BGM_LAYER_STORM', layer_storm, -18.0),
    ('BGM_TITLE', bgm_title, -16.0),
    ('BGM_SELECT', bgm_select, -16.0),
]


def soft_limit(x, ceiling_db=-1.0, knee=0.75):
    """ceiling 의 knee 배 아래는 그대로 두고, 그 위만 tanh 로 둥글게 눌러 ceiling 을 넘지 않게 한다"""
    c = 10 ** (ceiling_db / 20)
    t = c * knee
    a = np.abs(x)
    over = a > t
    y = x.copy()
    y[over] = np.sign(x[over]) * (t + (c - t) * np.tanh((a[over] - t) / (c - t)))
    return y


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, fn, target in TRACKS:
        x = fn()
        x = x - np.mean(x)
        x = x * 10 ** ((target - rms_db(x)) / 20)
        x = soft_limit(x)
        write_wav(os.path.join(OUT, name + '.wav'), x)
        print(f'{name:<20} {len(x):>8} samples  rms {rms_db(x):6.1f}  peak {20*np.log10(np.max(np.abs(x))):5.1f}')


if __name__ == '__main__':
    main()
