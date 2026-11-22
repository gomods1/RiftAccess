"""Records what the default output device plays (WASAPI loopback) for N seconds: record.py OUT.wav SECONDS"""
import sys, warnings, numpy as np, soundcard as sc, wave
warnings.filterwarnings("ignore")
out, secs = sys.argv[1], float(sys.argv[2])
spk = sc.default_speaker()
mic = sc.get_microphone(id=str(spk.name), include_loopback=True)
with mic.recorder(samplerate=48000, channels=2) as r:
    data = r.record(numframes=int(48000 * secs))
pcm = (np.clip(data, -1, 1) * 32767).astype(np.int16)
with wave.open(out, "wb") as w:
    w.setnchannels(2); w.setsampwidth(2); w.setframerate(48000); w.writeframes(pcm.tobytes())
print(f"wrote {out} ({secs}s, peak {np.abs(data).max():.3f}) from {spk.name}")
