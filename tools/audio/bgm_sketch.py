"""S3 전투 BGM 30초 시안 (16마디, 128 BPM, A단조). 근거: docs/sound-design.md 5장.
구조: 공통 BASE(드럼+베이스, 가운데) + 왼쪽 진영 차원 레이어(왼쪽) + 오른쪽 진영 차원 레이어(오른쪽).
가능한 차원 조합 3개를 스테레오 WAV 로 렌더링한다 (실제 게임에서는 레이어를 따로 재생한다).
usage: python -I tools/audio/bgm_sketch.py <out_dir>"""
import os
import sys

import numpy as np
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from music import *  # noqa: E402,F401,F403
from synth import echo  # noqa: E402

OUT = sys.argv[1] if len(sys.argv) > 1 else '.claude/tmp/audio'

# 마디별 코드 (i - VI - III - VII 진행, 마지막은 E 로 긴장 → 처음 Am 으로 돌아감)
PROG = ['Am', 'Am', 'F', 'F', 'C', 'C', 'G', 'G',
        'Am', 'Am', 'F', 'F', 'C', 'G', 'E', 'E']
song = Song(bars=len(PROG), bpm=128)
BPB = song.beats_per_bar


def bar_start(i):
    return i * BPB


# ---------------- BASE (중간 강도): 드럼 + 베이스 ----------------

def base_mid():
    t = song.track()
    for i, ch in enumerate(PROG):
        b = bar_start(i)
        fill = i in (7, 15)
        # 킥: 1, 2.5, 3 / 스네어: 2, 4 / 하이햇: 8분
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
        # 베이스: 8분 펄스, 근음 / 옥타브 / 5도
        root = hz(f'{CHORDS[ch][0]}2')
        fifth = root * 1.5
        pattern = [root, root, root * 2, root, root, root * 2, fifth, root]
        for k, f in enumerate(pattern):
            t.add(b + k * 0.5, bass(f, song.beat_sec * 0.45), 0.55)
    return t.render()


# ---------------- 숲: 마림바 아르페지오 ----------------

def layer_forest():
    t = song.track()
    shape = [0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1, 2, 3]  # 16분 16개 (한 마디)
    for i, ch in enumerate(PROG):
        tones = chord_notes(ch, 4, 5)
        for k, idx in enumerate(shape):
            accent = 1.0 if k % 4 == 0 else 0.7
            t.add(bar_start(i) + k * 0.25, marimba(tones[idx], 0.2), 0.32 * accent)
        # 2마디마다 맑은 현 튕김으로 코드 머리를 짚는다
        if i % 2 == 0:
            t.add(bar_start(i), pluck(chord_notes(ch, 3, 1)[0], 1.2), 0.35)
    return t.render()


# ---------------- 공장: 엇박 칩 화음 + 금속 타악 ----------------

def layer_factory():
    t = song.track()
    for i, ch in enumerate(PROG):
        tones = chord_notes(ch, 3, 3)
        for k in range(4):
            # 8분 엇박(and)에 짧은 화음
            for f in tones:
                t.add(bar_start(i) + k + 0.5, chip_stab(f, song.beat_sec * 0.3), 0.16)
        # 16분 금속 틱 (2·4박 16분 뒷박은 강하게)
        for k in range(16):
            amp = 0.22 if k in (6, 14) else (0.1 if k % 2 else 0.0)
            if amp > 0:
                t.add(bar_start(i) + k * 0.25, metal_tick(), amp)
        # 4마디마다 칩 시퀀스 (코드 위 상행 8분)
        if i % 4 == 3:
            seq = chord_notes(ch, 4, 4)
            for k, f in enumerate(seq + seq[::-1]):
                t.add(bar_start(i) + k * 0.5, chip_stab(f, song.beat_sec * 0.4), 0.14)
    return t.render()


# ---------------- 폭풍: 톱니 패드 + 리드 멜로디 + 바람 ----------------

# 2마디 프레이즈 (시작 박, 길이 박, 음) — 코드별
PHRASES = {
    'Am': [(0, 1, 'E5'), (1, 0.5, 'D5'), (1.5, 0.5, 'C5'), (2, 2, 'D5'), (4, 3.5, 'A4')],
    'F': [(0, 1, 'F5'), (1, 0.5, 'E5'), (1.5, 0.5, 'C5'), (2, 2, 'A4'), (4, 3.5, 'C5')],
    'C': [(0, 1, 'G5'), (1, 0.5, 'E5'), (1.5, 0.5, 'C5'), (2, 2, 'E5'), (4, 3.5, 'D5')],
    'G': [(0, 1, 'D5'), (1, 0.5, 'B4'), (1.5, 0.5, 'G4'), (2, 2, 'B4'), (4, 3.5, 'D5')],
}
# 마지막 4마디(C G E E) 는 따로
ENDING = [(0, 2, 'E5'), (2, 2, 'D5'), (4, 2, 'B4'), (6, 1, 'G#4'), (7, 1, 'B4'),
          (8, 6, 'E5')]


def layer_storm():
    t = song.track()
    lead = song.track()
    for i in range(0, len(PROG), 2):
        ch = PROG[i]
        dur = song.beat_sec * BPB * 2
        t.add(bar_start(i), saw_pad(chord_notes(ch, 3, 3), dur), 0.2)
    for i in range(0, 12, 2):
        for s, d, n in PHRASES[PROG[i]]:
            lead.add(bar_start(i) + s, saw_lead(hz(n), d * song.beat_sec), 0.22)
    for s, d, n in ENDING:
        lead.add(bar_start(12) + s, saw_lead(hz(n), d * song.beat_sec), 0.22)
    for i in range(0, len(PROG), 4):
        t.add(bar_start(i), wind(song.beat_sec * BPB * 4, 600 + i), 0.18)
    lead_buf = lead.render()
    wet = echo(lead_buf, song.beat_sec * 0.75, 0.35, 0.35, taps=3)[:song.length]
    return t.render() + wet


def main():
    os.makedirs(OUT, exist_ok=True)
    # 레이어 음량 맞춤: 차원 레이어가 묻히지 않도록 BASE 를 조금 낮게 둔다
    def level(x, target_db):
        return x * 10 ** ((target_db - rms_db(x)) / 20)
    base = level(base_mid(), -17.0)
    layers = {k: level(f(), -18.0) for k, f in
              [('forest', layer_forest), ('factory', layer_factory), ('storm', layer_storm)]}
    for name, x in [('base', base)] + list(layers.items()):
        print(f'{name:<8} rms {rms_db(x):6.1f} dB  peak {20*np.log10(np.max(np.abs(x))):6.1f} dB')

    combos = [('forest', 'factory'), ('forest', 'storm'), ('factory', 'storm')]
    for left, right in combos:
        mixl = pan_stereo(base, 0) + pan_stereo(layers[left], -0.5) + pan_stereo(layers[right], 0.5)
        mixl = normalize(bus(mixl * 0.9), -1.0)
        path = os.path.join(OUT, f'bgm_sketch_{left}_L_{right}_R.wav')
        wavfile.write(path, SR, (mixl * 32767).astype(np.int16))
        print(path, f'{len(mixl)/SR:.1f}s', f'rms {rms_db(mixl):.1f} dB')


if __name__ == '__main__':
    main()
