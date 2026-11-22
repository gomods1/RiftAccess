# Rift Access: Rift of the NecroDancer blind-accessibility mod

Read STATUS.md first. Methodology: C:\Users\Nitropc\code\mods\starting_points\template_generic.md.

## Recon (verified)
- Game: Rift of the NecroDancer, Steam app 2073250, build 26228537, game version 1.20.0-b24174. Installed at `S:\steam\steamapps\common\RiftOfTheNecroDancerOSTVolume1` (odd folder name, it is the game).
- Unity 2021.3.43f1, **Mono**, x64. Audio is **FMOD** (FMODUnity); Unity audio is not part of the mix. Input: new Input System via one `RiftInputActions` (`InputAccessor.Instance.RiftInput`).
- Loader: BepInEx 5.4.23.5 x64 (vendor/BepInEx_win_x64_5.4.23.5.zip). `steam_appid.txt` (2073250) in the game folder lets the exe start directly.
- Languages (from the game's string table): English, Spanish_Spain, Spanish_LATAM, French, German, Italian, Portuguese_Brazil, Russian, Japanese, Korean, Chinese_Simplified, Chinese_Traditional. The mod ships all of them (RiftAccess/Lang).
- Decompiled code: `game/Assembly-CSharp` (gitignored, never ship). String table dump: `game/loc/loc_dump.tsv` (Ctrl+F12 in game writes it).

## Game map
- Menus: `Shared.MenuOptions.SelectableOption` (+ groups `OptionGroup<T>`, `GetSelectedOption()`); focus event = `SetSelected(true)`. Popups `Shared.Title.MenuPopupScreen`.
- Track lists: `BaseTrackSelectionOptionGroup` (`_trackMetaData`, `_selectedTrackIndex`, `OnSelectedTrackChanged`). Pooled option rows.
- Dialogue: `Shared.NarrationTextManager.OpenTextBox` (queue `_queuedNarrationData` of `NarrationData{NarrationLocKey}`); continue = Submit or any arrow.
- Rhythm Rift: `RRStageController` (health `_playerController.CurrentHealth`, vibe `_currentVibePower/_minVibePowerToActivate/_maxVibePower/_isVibePowerActive`, `_stageInputRecord`), `RREnemyController.UpdateSystem(FmodTimeCapsule)`, `_activeEnemies`, private `ComputeAudioQueueDelay(ftc, beat)`.
- Enemy timing: `RREnemy.NextActionRowTrueBeatNumber` (next arrival at the action row), `ExpectedFollowUpActionTrueBeatNumber`, `IsExpectingFollowUpAction`, `TargetGridPosition.x` = lane (0..NumColumns-1; HOME_ROW y=0), `ShouldSpoofInputForEnemy()` = needs an input (autoplay uses it), `IsHoldNote/IsBeingHeld/GetLastHoldBeatNumber()`, `IsHealthItem`.
- The game itself pre-queues hit sounds 1.5 beats ahead with `AudioManager.PlayAudioEvent(..., delay)` + `SetLaneNumber`, delay minus `(LatencyManager.AudioLatencyOffset - VideoLatencyOffset)`. The mod copies that model.
- Boss battles: `BBHitIndicator.PlayHitIndicatorFx(action, direction, lengthInSeconds, grid)`; minigames: `MGAssistIndicator.PlayHitIndicatorFx(direction, lengthInSeconds, beatLength, afterWindow, holdLength)`.
- Game over: `GameOverScreen.SetFinalProgressPercentage`. Results: `StageFlowUiController.ShowResults(StageInputRecord, ...)`.
- Focus loss pauses stages and mutes audio (`GameWindow.IsHandlingFocus`, internal flag, no player setting).
- Default keys: lanes Left/Up/Right (A/W/D), Down/S = all three lanes, Space/Shift = vibe, Escape = pause; menus arrows/Enter/Escape, Ctrl = tab, Tab = sort. Mod keys: F1, F2, F10, H, Ctrl+F12 (debug dump). None collide.

## Architecture (RiftAccess/)
- `Plugin.cs`: startup order, `AccessController` per-frame loop (KeyInput, Loc, Speech, InputBlocker, menus, readers, watchers, cues).
- `Core/`: `Speech` (only speech entry point, logs every line to RiftAccess_speech.log), `Output/Prism*` (Prism 0.18.3 P/Invoke; PrismConfig has `availability_baseline_callback`), `ModConfig` (BepInEx config + `Setting` objects that the menu renders), `Loc` (Lang/*.txt embedded, `tut.<game key>` overrides tutorial lines), `KeyInput` (key edges from isPressed; `wasPressedThisFrame` misses presses here).
- `Audio/CuePlayer`: FMOD core sounds from plugin `Sounds/*.wav`, own channel group under master, scheduled on the DSP clock (`setDelay`), cancellable until started.
- `Gameplay/LaneCues`: the lane cue scheduler (postfix of `RREnemyController.UpdateSystem`), lane prediction (zombies, bats, sideways skulls), cancel-and-reschedule on lane/beat change. `StageWatcher`: health, vibe, combo, status (H), results, game over, music lowering. `DirectionCues`: boss/minigame indicators. `CueDemo`.
- `Menus/`: `MenuReader` (focus, values, labels, range sliders, focus recovery), `TrackReader`, `NarrationReader` (tutorial replacement, instant dialogue), `ScreenTextWatcher` (stable visible text outside options; in a stage only pause menu, results and tutorial prompts), `ModSettingsMenu` (F10), `HelpMenu` + `Hotkeys`, `InputBlocker` (gates InputAction polling + disables maps while a mod menu is open).
- Sounds: `sounds/cues/batch.json` (genny), copied to the plugin on build.

## Build / test loop
- `dotnet build RiftAccess -c Release` deploys to the game. `pwsh tools/restart.ps1` (kill, build, launch). `tools/boot.sh [-n]` to the main menu. `tools/keys.sh KEYS [delayMs] ["-HoldMs N"]` sends scancodes (takes focus). `tools/shot.sh NAME`, `tools/speech.sh N`, `tools/log.sh N REGEX`, `python tools/check-lang.py`.
- The user shares the machine: keys can be lost when focus moves; send one key per call when it matters. Do not build background-input dev harnesses (user asked to keep it simple).

## Verification without ears (regression tests)
- Turn on Debug logging (F10 > Speech). Then:
  - **Cue check** (`Gameplay/CueCheck.cs`): for every hit window the game opens, checks a main cue already sounded for that hit, in the lane the monster is really in, and the real lead vs the setting. Summary spoken and logged at the end of each stage ("Cue check: N hits, N cued correctly, 0 wrong lane, 0 missing, lead error ...").
  - **Cue bot** (Ctrl+F11, `Gameplay/CueBot.cs`): plays using only cue events (lane + cue start + lead), keys through the Input System. A clear proves the cues carry the song.
- `tools/bot_run.sh SECS` (keeps focus, resumes pauses, advances dialogue), `tools/test_tutorial.sh "NAME" SECS` (Extras > Tutorials list, runs one, prints the cue check), `tools/to_extras.sh [tutorials]`.
- Audio: `python tools/record.py OUT.wav SECS` (loopback of the default output), `python tools/find_cues.py OUT.wav [thr]` (matched filter per lane cue: onset, lane, score, stereo balance).
- `tools/hold_focus.ps1 -Seconds N` keeps the game in front during a test (the user allowed stealing focus).

## Pitfalls found
- Synthetic taps need a hold (60 ms) or polled checks miss them.
- BepInEx sorts config sections; editing the cfg while the game runs is overwritten when the mod saves (closing F10). Change settings in game.
- Base `SetSelected` postfix runs before the subclass updates its labels: read a frame later.
- Option groups are SelectableOptions too: "inside an option" checks must not exclude whole popups.
- Track rows are pooled and include hidden template rows ("Folder Name", "sfgsdg..."): read from data.
- In stages the enemy controller keeps updating behind game over: use InStage/Paused flags, not InSong, for screen reading.
- Some options never call base SetSelected (BestiarySelectionOption): MenuReader keeps its own Selected set from the postfix.
- Multi-hit monsters: cue every remaining hit (health x follow-up spacing) up front, or the third hit of an armadillo is scheduled too late.
- `MSBuild` treats `xx.txt` as culture resources unless `WithCulture="false"` + LogicalName.
- An early hit snaps the enemy onto the action row for a few frames before its hit move is decided: TargetGridPosition is still the old lane (y = HOME row while IsPerformingHitMovement). Bats and sideways skulls must be predicted one lane over then. The cue bot never hits early, so its runs cannot show this.
