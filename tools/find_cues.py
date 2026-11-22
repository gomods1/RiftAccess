"""Finds the mod's lane cues in a recording of the game's output (matched filter per lane sound).
find_cues.py REC.wav [threshold] -> onset time, lane, match score and stereo balance for each detected cue."""
import sys, wave, numpy as np
from numpy.fft import rfft, irfft
def load(p):
    w = wave.open(p); sr = w.getframerate(); ch = w.getnchannels()
    a = np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float32) / 32768
    return (a.reshape(-1, ch) if ch > 1 else a[:, None]), sr
rec, sr = load(sys.argv[1]); thr = float(sys.argv[2]) if len(sys.argv) > 2 else 0.35
mono = rec.mean(axis=1)
res = []
for lane in ("left", "mid", "right"):
    t, tsr = load(f"sounds/cues/tap_{lane}.wav"); t = t[:, 0]
    t = np.interp(np.arange(0, len(t) * sr / tsr) * tsr / sr, np.arange(len(t)), t)[: int(0.12 * sr)]  # attack part
    t = (t - t.mean()) / np.linalg.norm(t)
    n = 1 << int(np.ceil(np.log2(len(mono) + len(t))))
    corr = irfft(rfft(mono, n) * np.conj(rfft(t, n)), n)[: len(mono) - len(t)]
    energy = np.sqrt(np.convolve(mono ** 2, np.ones(len(t)), "valid")[: len(corr)]) + 1e-9
    score = corr / energy
    i = 0
    while i < len(score):
        if score[i] > thr:
            j = i + int(0.05 * sr); k = i + int(np.argmax(score[i:j]))
            l, r = np.abs(rec[k:k + len(t), 0]).mean(), np.abs(rec[k:k + len(t), -1]).mean()
            res.append((k / sr, lane, score[k], (r - l) / (r + l + 1e-9)))
            i = k + int(0.15 * sr)
        else:
            i += 1
res.sort()
# Keep the best lane when several templates fire on the same onset.
best = []
for x in res:
    if best and abs(best[-1][0] - x[0]) < 0.04:
        if x[2] > best[-1][2]: best[-1] = x
    else:
        best.append(x)
for t0, lane, s, bal in best:
    print(f"{t0:7.3f}s  {lane:5}  match {s:.2f}  balance {bal:+.2f}")
print(f"{len(best)} cues found")
