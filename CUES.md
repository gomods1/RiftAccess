# Cue sounds

Each sound has one meaning, and each kind has its own timbre so it is recognisable in mono or with one ear. Source:
`sounds/cues/make_batch.py` writes `sounds/cues/batch.json` for genny-reloaded; re-render with
`python sounds/cues/make_batch.py && genny-reloaded render sounds/cues/batch.json --quiet`, then build to copy them into
the plugin. All are mono; pan and timing are applied at runtime by FMOD. Lane-coded families are level-matched
(`max_loudness`).

| File | Meaning | Where |
|---|---|---|
| tap_left / tap_mid / tap_right | a hit is due in this lane, one cue lead later. Each lane its own instrument: marimba + woodblock (A4), kalimba + rim (E5), steel drum + clave (B5) | lane cues, boss counterattacks, minigame presses |
| tap_up / tap_down | up or down: the kalimba with a rising or falling glide | boss battles, minigames |
| hold_* (left, mid, right, up, down) | a hold note starts here: accordion note at the lane pitch with a wooden attack | wyrm heads, minigame holds |
| hold_loop | looped while a hold key is down (harmonium hum, E4, pitched per lane at runtime: a fifth down left, a fifth up right) | lanes: from the actual press until the release beat or the key comes up; minigames: scheduled for the hold length |
| release_* | let go now (bubble pop with a quick upward flick) | end of hold notes |
| heart_* | food or a heart: the lane's own tap (one note) plus a high sparkle above every lane pitch | health items |
| dodge_* (left, mid, right, up, down) | a boss attack: press this direction to dodge (noise whoosh + low buzz; up and down glide) | boss battles |
| click | exactly the moment to press (optional "Click on the hit beat") | everywhere, and the cue test |
| vibe_ready | vibe power can be activated (vibraphone arpeggio + riser) | Rhythm Rift |
| warning | health at or below the threshold (heartbeat + two falling notes) | Rhythm Rift |
| locked | the game locked the lane keys for 1.5 beats after stray presses (latch click + low buzz) | Rhythm Rift |
| unlocked | the lane keys work again (light click + rising two-note) | Rhythm Rift |
| vibe_on | spare (not used yet) | |

Older sound sets without tap_up / tap_down fall back to the middle sound pitched up (x1.26) or down (x0.79).
F1 ends with a sound guide whose lines play these sounds (HelpMenu).
