# Cue sounds

Each sound has one meaning. Source: `sounds/cues/batch.json` (genny); re-render with `genny render sounds/cues/batch.json`, then build to copy them into the plugin. All are mono; pan and timing are applied at runtime by FMOD.

| File | Meaning | Where |
|---|---|---|
| tap_left / tap_mid / tap_right | a hit is due in this lane, one cue lead later (kalimba + marimba + rim; A4 / E5 / B5) | lane cues, boss counterattacks, minigame presses |
| hold_* | a hold note starts in this lane (tap + soft sustained tone) | wyrm heads, minigame holds |
| release_* | let go of the hold now | end of hold notes |
| heart_* | food or a heart: hit it to heal (two-note celesta) | health items |
| dodge_* | a boss attack: press this direction to dodge (soft whoosh + tone) | boss battles |
| click | exactly the moment to press (optional "Click on the hit beat") | everywhere, and the cue test |
| vibe_ready | vibe power can be activated (glass arpeggio) | Rhythm Rift |
| warning | health at or below the threshold (falling two notes) | Rhythm Rift |
| vibe_on | spare (not used yet) | |

Directions without a lane (boss and minigame up/down) use the middle sound pitched up (x1.26) or down (x0.79).
