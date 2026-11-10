"""효과음 WAV 수치 검사 + 파형·스펙트로그램 시트 + 연속 청취용 미리듣기 WAV.
검사 기준: docs/sound-design.md 11장 (피크 <= -1 dBFS, 앞 무음 <= 5 ms, 끝 샘플 거의 0).
루프 파일(이름에 _LOOP 또는 PLR_LOW_HP)은 앞 무음·끝 클릭 대신 이음새(끝→처음 값과 기울기 차이)를 검사한다.
usage: python -I tools/audio/check.py <sfx_root> <out_dir> [name_regex] [tag]
name_regex 를 주면 시트·미리듣기에는 이름이 맞는 파일만 넣는다 (표는 전부).
출력: <out_dir>/sfx_sheet[_tag].png, <out_dir>/sfx_preview[_tag].wav, 표준 출력에 표"""
import re
import glob
import os
import sys

import numpy as np
from scipy.io import wavfile

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt  # noqa: E402

ROOT, OUT = sys.argv[1], sys.argv[2]
PATTERN = re.compile(sys.argv[3]) if len(sys.argv) > 3 else None
TAG = ('_' + sys.argv[4]) if len(sys.argv) > 4 else ''


def is_loop(name):
    return '_LOOP' in name or 'PLR_LOW_HP' in name
os.makedirs(OUT, exist_ok=True)


def db(v):
    return 20 * np.log10(max(v, 1e-12))


def analyze(x, sr):
    peak = np.max(np.abs(x))
    thr = peak * 10 ** (-40 / 20)
    active = np.nonzero(np.abs(x) > thr)[0]
    lead_ms = active[0] / sr * 1000 if len(active) else 0
    body = x[active[0]:active[-1] + 1] if len(active) else x
    rms = np.sqrt(np.mean(body ** 2))
    spec = np.abs(np.fft.rfft(x * np.hanning(len(x))))
    freqs = np.fft.rfftfreq(len(x), 1 / sr)
    centroid = float(np.sum(freqs * spec) / max(np.sum(spec), 1e-12))
    return {
        'dur_ms': len(x) / sr * 1000,
        'peak_db': db(peak),
        'rms_db': db(rms),
        'crest_db': db(peak) - db(rms),
        'lead_ms': lead_ms,
        'end_abs': abs(x[-1]),
        'centroid': centroid,
    }


files = sorted(glob.glob(os.path.join(ROOT, '**', '*.wav'), recursive=True))
rows, signals = [], []
fail = 0
print(f"{'file':<28}{'ms':>7}{'peak':>7}{'rms':>7}{'crest':>7}{'lead':>6}{'centroid':>9}  result")
for f in files:
    sr, data = wavfile.read(f)
    x = data.astype(np.float64) / 32768.0
    if x.ndim > 1:
        x = x.mean(axis=1)
    a = analyze(x, sr)
    problems = []
    name = os.path.relpath(f, ROOT).replace('\\', '/')
    if a['peak_db'] > -0.9:
        problems.append('peak')
    if is_loop(name):
        # 이음새: 끝 샘플 -> 첫 샘플로 넘어갈 때의 값 차이가 평소 샘플 간 변화(99퍼센타일)보다 크면 클릭
        jump = abs(x[0] - x[-1])
        typical = np.percentile(np.abs(np.diff(x)), 99)
        if jump > typical * 1.5 + 1e-4:
            problems.append(f'seam({jump:.3f}>{typical:.3f})')
    else:
        if a['lead_ms'] > 5:
            problems.append('lead')
        if a['end_abs'] > 0.002:
            problems.append('end-click')
    fail += bool(problems)
    print(f"{name:<28}{a['dur_ms']:7.0f}{a['peak_db']:7.1f}{a['rms_db']:7.1f}{a['crest_db']:7.1f}"
          f"{a['lead_ms']:6.1f}{a['centroid']:9.0f}  {'OK' if not problems else ','.join(problems)}")
    rows.append((name, a))
    if PATTERN is None or PATTERN.search(name):
        signals.append((name, x, sr))

print(f"\n{len(files)} files, {fail} with problems")

# 파형 + 스펙트로그램 시트
n = len(signals)
fig, axes = plt.subplots(n, 2, figsize=(12, 1.25 * n), gridspec_kw={'width_ratios': [1, 1.4]})
for i, (name, x, sr) in enumerate(signals):
    t = np.arange(len(x)) / sr * 1000
    axes[i, 0].plot(t, x, lw=0.5, color='#2a6')
    axes[i, 0].set_ylim(-1, 1)
    axes[i, 0].set_ylabel(name.split('/')[-1].replace('.wav', ''), rotation=0, ha='right', fontsize=7)
    axes[i, 0].tick_params(labelsize=6)
    axes[i, 1].specgram(x, NFFT=512, Fs=sr, noverlap=384, cmap='magma', vmin=-120)
    axes[i, 1].set_ylim(0, 12000)
    axes[i, 1].tick_params(labelsize=6)
fig.tight_layout()
fig.savefig(os.path.join(OUT, f'sfx_sheet{TAG}.png'), dpi=80)

# 미리듣기: 파일마다 0.6초 간격으로 이어 붙임
gap = np.zeros(int(0.6 * 44100))
preview = np.concatenate([np.concatenate([x, gap]) for _, x, _ in signals])
wavfile.write(os.path.join(OUT, f'sfx_preview{TAG}.wav'), 44100, (preview * 32767).astype(np.int16))
