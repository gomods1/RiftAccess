# Status

## Current state (2026-10-28, session 1)
Game build 26228537 (1.20.0-b24174), BepInEx 5.4.23.5 x64, Rift Access 0.1.0, Prism 0.18.3. Tested in Spanish with NVDA.

### Verified by playing (cue bot + cue check)
The cue bot (Ctrl+F11 with Debug logging on) plays using only the mod's cues: the lane from the sound, the key one cue
lead after the sound starts, Down when all three lanes sound together, Space after the vibe ready chime. The cue check
confirms for every hit window the game opens that a cue already sounded, in the lane the monster is really in, and
measures the lead against the setting.

| Run | Result | Cue check |
|---|---|---|
| Disco Disaster, Easy, full song | cleared, full combo, 142 perfect + 1 great of 143 | 143/143, lead error avg 1 ms |
| Disco Disaster, Medium, full song | cleared, full combo, 228/228 perfect; combo announced every 25; vibe ready fired | 228/228, max 3 ms |
| Harmonie boss battle (Medium) | cleared, 45 perfect + 5 great, 0 missed (80 direction cues: dodges, counters, pummels) | n/a |
| Boss battle tutorial | completed | n/a |
| Yoga minigame (Medium) | cleared, 23/23 then 22/22 perfect (after switching to beatmap CheckInput cues) | n/a |
| Minigame tutorial (yoga) | completed (it has no assist events: cues come from the beatmap's CheckInput events) | n/a |
| Advanced boss tutorial | completed (dialogue only) | n/a |
| Tutorials Fundamentos / Tiempos y técnica / Monstruos complicados | completed (full-row and vibe lessons need the Down and Space rules) | 61/61, 42/42, 27/27 |
| Monster tutorials: red bat 70, yellow bat 60, red zombie 24, green zombie 20, wyrm 10 (hold + release), blue blademaster 20, red skull 78, red harpy 21, double-shield skeleton 54 | | all correct, 0 wrong lane, 0 missing (red skull lead error max 24 ms) |
| Mechanic tutorials: bounce 25, portal 19, mystery 34, coal 36 traps; off-beat 30; arbitrary beat 40; lane crossing 51; ham (food) 192 | | all correct |

Fixes that came out of these runs: every remaining hit of multi-hit monsters is cued up front (armadillo third hits
were 200 ms late); lanes predicted for zombies, bats, sideways skulls; minigame moves are cued from the beatmap's upcoming
CheckInput events (assist events exist only in some beatmaps and are drawn only in Assist mode); pummel indicators
(no direction) get a centre cue; dialogue boxes are left to the narration reader (no double reading).

### Verified in the running game (speech and sounds)
- Audio output captured (tools/record.py + tools/find_cues.py) during bot play at the release defaults: 79 cues in 30 s
  found in the game's mix at the right pan; within each cue's own pitch band (A4/E5/B5 and octaves) the cues stand
  +20.8 dB over the music on median, 91 % more than +6 dB (broadband only +1.8 dB: the music's bass dominates RMS).
- Fresh config (release defaults) checked: Debug logging off (bot and cue check unavailable to players), lead 1 beat,
  cue volume 100, music 75 % during songs, tutorial guide and instant dialogue on.
- Music lowering 1.00 -> 0.75 during songs, restored on leaving; low health warning at 3, 2, 1; vibe ready chime.
- Speech: loaded message, F1 help, F2, H status, F10 settings (all items, Spanish), first-run sound settings, calibration
  screen and skip popup, title, warnings, promo/event popups, main menu, track list (name, artist, BPM, new, folders,
  unlock price), loadout (start, practice mode, range "inicio/final", speed), pause menu, quit stage, game over line,
  results (cleared and failed), diamonds popup, story mode list and story dialogue, Extras (tutorials, minigames, boss
  battles lists), game settings (accessibility page: checkboxes, carousel "Animación de fondo, Completa").
- Tutorial narration replaced by by-ear explanations in Rhythm Rift and boss tutorials; tutorial prompts read.
- All 11 language tables pass tools/check-lang.py.

### Not exercised
- Minigames other than yoga and bosses other than Harmonie: locked behind diamonds on this profile. They use the same
  code paths (beatmap CheckInput events, boss indicators). Hard boss battles: no indicators, so no cues (by design).
- Custom tracks: menu read; none installed (they play as Rhythm Rift stages). The level editor is visual, not covered.
- Daily challenge: loadout read; not played (it posts to a public leaderboard).
- Key rebinding: list read (key + action); no binding was changed.
- Note: bot runs uploaded scores to the game's online leaderboards under the developer's Steam account.
- A human listening test: cue clarity and taste against real music, and whether one beat of lead feels right.

### Fixed after user report (not yet re-tested by playing)
- Moving monsters (bats, red skulls) cued in the wrong lane when hit slightly early: during the snap onto the action row
  the mod re-cued the next hit in the lane just hit. Now predicted one lane over; red bats' turn after a hit is applied
  during the hit move too. Cue check also counts misleading extra cues (wrong lane before the right one, cues for hits
  that never came).

## Next steps
1. Listening test by the user: Fundamentos tutorial, then Disco Disaster Easy by ear. Tune cue volume, music volume,
   lead and timing offset in F10 if needed.
2. More boss battles and minigames as they unlock.
3. Release: `pwsh tools/package.ps1` -> release/RiftAccess-<version>.zip.
