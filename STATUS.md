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

## Session 2 (2026-11-24): player feedback on 0.1.0
Feedback: first minigame (yoga) instructions confusing; hold cues not explained and hard to tell from taps; health
items played two notes that overlap the other lanes' pitches. Asked: regenerate every cue with genny-reloaded, each
clearly distinct, especially holds; minigames accessible and well worded.

Done (see CHANGELOG Unreleased, CUES.md):
- New cue set from `sounds/cues/make_batch.py` (genny-reloaded): per-lane instruments, accordion hold head, looped
  hold hum (`CuePlayer.PlayLoop`, DSP-scheduled end), heart = lane tap + sparkle above all lane pitches, rising/falling
  tap/hold/dodge for up/down. Lane-coded families level-matched (ear loudness -12).
- Hold tone setting; first-hold spoken hint; F1 sound guide (lines play their sound, own handles, never cancels song cues).
- Minigame and hold tutorial lines rewritten in all 11 languages; check-lang passes.

Verified in game (build 1.20.0-b24233, game updated since session 1): F1 sound guide reads and plays (recording shows
the hum from 0.5 s to 2.0 s as scheduled, looping past the file length); wyrm tutorial with the cue bot: hold head,
hum from press to release beat, release, combo kept, hint spoken; yoga tutorial completed by the bot twice with the new
wording, Up/Down moves cued with tap_up / tap_down.
Also: ProfileUnlockPopup ("new Rift profile item") only had its buttons read; UnlockPopupReader now reads title, items
and call to action (built, not yet heard: the one unlock on this profile was already shown).
Released as 0.2.0.
Not yet: a human listening test of the new sounds; burger/photo/show/breathing minigames (still locked here).

## Session 3 (2026-11-26): visual rules and scene descriptions
Player report: damage "for hitting certain lanes" with no different cue. Cause: game rules shown only on screen (key
lockout after stray presses, Down voided by an empty lane). User then asked to announce every visual-only rule and to
describe all artwork. Done (see CHANGELOG Unreleased): lockout cue, damage source, full-row miss, vibe chains,
modifiers, Bane auto-hits, grades, traps, boss/minigame HUD, countdown ticks, timing average, story scene staging,
cinematic audio description, boss scenes, review buffers (Alt+arrows) with picture descriptions, promo carousel.
Decompiled code re-extracted from b24233 (update changed only custom-track loading).

Verified in game: lockout (cue + speech + unlock), damage source, full-row miss, vibe chains, letter grade, traps
(bounce tutorial), boss H status and health quarters, Harmonie/Banshee defeat scenes, in-song dialogue speaker/mood/tone,
intro cinematic description timing, Alt+Up on a track (cover), on the carousel and on profile frames (read from the
preview card's part that changed last), carousel line, tutorial demo video line (Off-beat tutorial box, the box stays
open), paused-song portraits with their reaction (Cadence and the coach), main menu artwork (Cadence with guitar, logo,
city; hidden screens filtered by canvas, CanvasGroup alpha and on-screen area).
Not verified in game: modifiers (needs a pin equipped), Bane cueing, story dialogue scenes (no unlocked dialogue beat on
this save, and unlocking would change the save), outro cinematic (end of story), boss scenes other than Harmonie
(locked), minigame health, profile portraits and icons (same mechanism as frames), boss minion lines (all battles
with minions are locked; the 23 minion types come from the extracted BossMinionView data).
Described in the review buffer: 76 tutorial demo videos (templates filled with the game's monster names), the two
in-song portraits (27 portrait ids, reactions mirrored from RRPortraitView, rivals inverted), 23 boss minion types,
menu, song list (4 difficulties), story mode (6 chapter panels, 4 stage-type banners, backdrop) and profile backdrops.
Dot and stripe patterns map to the one menu backdrop line. Left out on purpose: masks (invisible), plain panels, bars
and row backers (UI chrome; their content is read as text).
Checks: `python tools/check-art.py` (needs the gitignored game/assets/video/tut export): 76 videos, 27 portrait ids, 23 minion types and 129
portrait/alias keys all resolve. In game, with Debug logging, every Alt+Up logs "Undescribed visible picture" for each
visible large image without a description; toured main menu, song list, story mode, Extras, profile: only chrome left.

## Next steps
1. Listening test by the user: Fundamentos tutorial, then Disco Disaster Easy by ear. Tune cue volume, music volume,
   lead and timing offset in F10 if needed.
2. More boss battles and minigames as they unlock.
3. Release: `pwsh tools/package.ps1` -> release/RiftAccess-<version>.zip.
