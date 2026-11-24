"""Writes batch.json for genny-reloaded: the Rift Access cue set.

Families (each one its own timbre, so the kind of cue is clear even in mono):
  tap_*      short hit, press now+lead. Each lane its own voice and pitch: left marimba A4 (wood),
             middle kalimba E5 (tine), right steel drum B5 (metal).
  heart_*    the lane's tap (one note) + a high sparkle above every lane pitch: food and hearts.
  hold_*     reed organ (accordion) at the lane pitch with a wooden attack, sustained and swelling:
             a hold note starts here. hold_loop plays while the key is held.
  release_*  a bubble pop with a quick upward flick: let go now.
  dodge_*    a noise whoosh with a low buzz: boss attack, dodge.
  *_up / *_down  boss and minigame directions without a lane: the middle sound with a rising or falling glide.
  click, vibe_ready, warning, vibe_on.
Run: python sounds/cues/make_batch.py && genny-reloaded render sounds/cues/batch.json --quiet
"""
import json, os

LANES = {"left": "A4", "mid": "E5", "right": "B5"}
HZ = {"left": 440, "mid": 659, "right": 988}
FADE = lambda out: {"type": "fade", "out": out}
MONO = {"type": "mono"}


def tap_layers(side):
    n = LANES[side]
    if side == "left":
        return [{"type": "synth", "inst": "marimba", "notes": n, "dur": 0.25, "vel": 0.95, "params": {"decay": 1.2}},
                {"type": "synth", "inst": "marimba", "notes": "A5", "dur": 0.2, "vel": 0.5, "gain": 0.3},
                {"type": "drum", "kind": "woodblock", "params": {"tune": 700}, "gain": 0.35}]
    if side == "mid":
        return [{"type": "synth", "inst": "kalimba", "notes": n, "dur": 0.2, "vel": 0.9},
                {"type": "drum", "kind": "rim", "gain": 0.3}]
    return [{"type": "synth", "inst": "steel_drum", "notes": n, "dur": 0.2, "vel": 0.8},
            {"type": "drum", "kind": "clave", "params": {"tune": 1600}, "gain": 0.25}]


def glide(direction, base_hz, gain=0.55, at=0.0):
    lo, hi = base_hz * 0.75, base_hz * 1.6
    kind = "sweep_up" if direction == "up" else "sweep_down"
    a, b = (lo, hi) if direction == "up" else (hi, lo)
    return {"type": "sfx", "kind": kind, "at": at, "gain": gain,
            "params": {"start": round(a), "end": round(b), "dur": 0.13, "wave": "triangle"},
            "fx": [FADE(0.05)]}


sounds = []
for side in LANES:
    sounds.append({"out": f"tap_{side}.wav", "duration": 0.32, "layers": tap_layers(side), "fx": [FADE(0.2), MONO]})
    sounds.append({"out": f"heart_{side}.wav", "duration": 0.7,
                   "layers": tap_layers(side) + [
                       {"type": "sfx", "kind": "magic", "at": 0.04, "gain": 0.45,
                        "params": {"dur": 0.5, "density": 16, "freq": "C7"}, "fx": [{"type": "lowpass", "cutoff": 4500}]}],
                   "fx": [{"type": "reverb", "mix": 0.12, "size": 0.3}, FADE(0.25), MONO]})
    sounds.append({"out": f"hold_{side}.wav", "duration": 0.75,
                   "layers": [{"type": "drum", "kind": "woodblock", "params": {"tune": 900}, "gain": 0.35},
                              {"type": "synth", "inst": "accordion", "notes": LANES[side], "dur": 0.7, "vel": 0.85,
                               "fx": [{"type": "tremolo", "rate": 9, "depth": 0.25}]}],
                   "fx": [{"type": "lowpass", "cutoff": 4500}, FADE(0.15), MONO]})
    sounds.append({"out": f"release_{side}.wav", "duration": 0.25,
                   "layers": [{"type": "sfx", "kind": "pop", "params": {"freq": HZ[side] * 0.6}, "gain": 0.9},
                              {"type": "sfx", "kind": "sweep_up", "gain": 0.4,
                               "params": {"start": round(HZ[side] * 0.5), "end": round(HZ[side] * 1.5), "dur": 0.07, "wave": "sine"}}],
                   "fx": [FADE(0.1), MONO]})
    tone = {"left": "A3", "mid": "E4", "right": "B4"}[side]
    sounds.append({"out": f"dodge_{side}.wav", "duration": 0.4,
                   "layers": [{"type": "sfx", "kind": "whoosh", "params": {"dur": 0.25, "low": 250, "high": 2400}, "gain": 0.8},
                              {"type": "synth", "inst": "square_lead", "notes": tone, "dur": 0.14, "vel": 0.5, "gain": 0.45,
                               "fx": [{"type": "lowpass", "cutoff": 1500}]}],
                   "fx": [FADE(0.15), MONO]})

# Directions without a lane (boss battles, minigames): the middle sound plus a rising or falling glide.
for d in ("up", "down"):
    sounds.append({"out": f"tap_{d}.wav", "duration": 0.35,
                   "layers": tap_layers("mid") + [glide(d, HZ["mid"], at=0.02)], "fx": [FADE(0.15), MONO]})
    sounds.append({"out": f"hold_{d}.wav", "duration": 0.75,
                   "layers": [{"type": "drum", "kind": "woodblock", "params": {"tune": 900}, "gain": 0.35},
                              {"type": "synth", "inst": "accordion", "notes": "E5", "dur": 0.7, "vel": 0.85,
                               "fx": [{"type": "tremolo", "rate": 9, "depth": 0.25}]},
                              glide(d, HZ["mid"], gain=0.5, at=0.02)],
                   "fx": [{"type": "lowpass", "cutoff": 4500}, FADE(0.15), MONO]})
    sounds.append({"out": f"dodge_{d}.wav", "duration": 0.4,
                   "layers": [{"type": "sfx", "kind": "whoosh", "params": {"dur": 0.25, "low": 250, "high": 2400, "direction": d}, "gain": 0.8},
                              {"type": "sfx", "kind": "sweep_up" if d == "up" else "sweep_down", "gain": 0.45,
                               "params": {"start": 220 if d == "up" else 440, "end": 440 if d == "up" else 220, "dur": 0.16, "wave": "square"},
                               "fx": [{"type": "lowpass", "cutoff": 1500}]}],
                   "fx": [FADE(0.15), MONO]})

sounds += [
    # Plays (looped) while a hold key is down; pitched per lane at runtime.
    {"out": "hold_loop.wav", "duration": 1.05,
     "layers": [{"type": "synth", "inst": "harmonium", "notes": "E4", "dur": 1.4, "vel": 0.7}],
     "fx": [{"type": "tremolo", "rate": 6, "depth": 0.3}, {"type": "lowpass", "cutoff": 2500},
            {"type": "loop", "crossfade": 0.1}, MONO], "trim": False, "normalize": -6},
    {"out": "click.wav", "duration": 0.12,
     "layers": [{"type": "drum", "kind": "woodblock", "params": {"tune": 1800}, "gain": 0.8},
                {"type": "sfx", "kind": "click", "gain": 0.4}],
     "fx": [FADE(0.06), MONO]},
    {"out": "vibe_ready.wav",
     "layers": [{"type": "seq", "inst": "vibraphone", "steps": "C5:0.07 G5:0.07 C6:0.07 G6:0.35", "vel": 0.6},
                {"type": "sfx", "kind": "riser", "params": {"dur": 0.35}, "gain": 0.25}],
     "fx": [{"type": "reverb", "mix": 0.25, "size": 0.5}, MONO]},
    {"out": "warning.wav",
     "layers": [{"type": "sfx", "kind": "heartbeat", "params": {"dur": 0.7, "rate": 1.4}, "gain": 0.9},
                {"type": "seq", "inst": "square_lead", "steps": "D4:0.12 -:0.04 A3:0.2", "vel": 0.6, "gain": 0.9,
                 "fx": [{"type": "lowpass", "cutoff": 1800}]}],
     "max_loudness": -10, "fx": [MONO]},
    {"out": "vibe_on.wav",
     "layers": [{"type": "sfx", "kind": "powerup", "gain": 0.6},
                {"type": "synth", "inst": "warm_pad", "notes": "C4:maj", "dur": 0.4, "gain": 0.4}],
     "fx": [MONO]},
]

for snd in sounds:
    if snd["out"].split("_")[0] in ("tap", "heart", "hold", "dodge") and snd["out"] != "hold_loop.wav":
        snd["max_loudness"] = -12
batch = {"out_dir": "sounds/cues", "normalize": -1, "sounds": sounds}
with open(os.path.join(os.path.dirname(__file__), "batch.json"), "w") as f:
    json.dump(batch, f, indent=1)
print(len(sounds), "sounds")
