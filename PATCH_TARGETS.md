# Patch targets (verified against build 26228537)

| Target | Kind | Used for |
|---|---|---|
| `Shared.MenuOptions.SelectableOption.SetSelected(bool, bool)` | postfix | menu focus (read a frame later) |
| `Shared.TrackSelection.BestiarySelectionOption.SetSelected` | postfix | bestiary focus (does not call base) |
| `Shared.Title.MenuPopupScreen.Show` | postfix | popups (now read by ScreenTextWatcher) |
| `Shared.TrackSelection.BaseTrackSelectionOptionGroup.OnSelectedTrackChanged` (private) | postfix | track list readout |
| `Shared.NarrationTextManager.OpenTextBox` (private) | prefix + postfix | dialogue key + full text, tutorial replacement, skip typewriter |
| `RhythmRift.RREnemyController.UpdateSystem(FmodTimeCapsule)` | postfix | lane cue scheduler (frame's time capsule) |
| `RhythmRift.RREnemyController.ComputeAudioQueueDelay` (private) | delegate | beats to seconds (tempo changes, song speed) |
| `Shared.Audio.AudioManager.SetGamePaused` / `SetGameUnpaused` | postfix | cancel pending cues, pause flag, music restore/lower |
| `Shared.StageFlowUiController.ShowResults` | postfix | results announcement |
| `RhythmRift.GameOverScreen.SetFinalProgressPercentage(float)` | postfix | game over announcement |
| `BossBattles.BossBattleStageController.PlayHitIndicator` (private) | prefix | indicator length in beats |
| `BossBattles.BBHitIndicator.PlayHitIndicatorFx` | postfix | boss direction cues |
| `Minigames.MGAssistIndicator.PlayHitIndicatorFx` | postfix | minigame direction cues |
| `UnityEngine.InputSystem.InputAction.WasPerformedThisFrame/IsPressed/WasPressedThisFrame/WasReleasedThisFrame` | prefix | block the game's input while a mod menu is open |

Fields read by reflection: `RREnemyController._activeEnemies/_tileGridAccessor`, `RREnemy.EnemyLength`, `RRStageController._playerController/_currentVibePower/_minVibePowerToActivate/_maxVibePower/_isVibePowerActive/_stageInputRecord/GetPercentageThroughStage`, `RRSkullEnemy._moveSidewaysWhenHit`, `BaseTrackSelectionOptionGroup._trackMetaData/_selectedTrackIndex`, `NarrationTextManager._currentTextAnimatorPlayer/_queuedNarrationData`, `RangeSliderOptionController._textLabel/_sliderValueMin/_sliderValueMax`, `SelectableOptionGroup._options`, `TicToc.Localization.Localizer._isInitialized`.
