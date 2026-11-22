using System;
using System.Collections.Generic;
using HarmonyLib;
using RhythmRift;
using RiftAccess.Audio;
using RiftAccess.Core;
using Shared;
using Shared.RhythmEngine;
using UnityEngine;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Standing state of a Rhythm Rift stage that a sighted player reads off the HUD: health, vibe power, combo.
    /// Changes become sounds or short announcements; the status hotkey reads them all; results are read at the end.
    /// </summary>
    internal static class StageWatcher
    {
        private static RRStageController _stage;
        private static AccessTools.FieldRef<RRStageController, RRPlayerController> _player;
        private static AccessTools.FieldRef<RRStageController, float> _vibe;
        private static AccessTools.FieldRef<RRStageController, int> _vibeMin;
        private static AccessTools.FieldRef<RRStageController, int> _vibeMax;
        private static AccessTools.FieldRef<RRStageController, bool> _vibeActive;
        private static Func<RRStageController, float, float> _progress;
        private static System.Reflection.FieldInfo _inputRecord;

        private static int _lastHealth = -1;
        private static bool _vibeReady;
        private static int _lastCombo;
        private static float _stageStartTime;

        /// <summary>True while a song is being played (the enemy controller is updating).</summary>
        public static bool InSong => LaneCues.Controller != null && Time.unscaledTime - LaneCues.LastUpdateTime < 0.5f;

        /// <summary>A Rhythm Rift stage is loaded (playing, paused, game over or results).</summary>
        public static bool InStage => _stage != null || _otherStage != null || DirectionCues.InMinigame;

        /// <summary>A boss battle or minigame stage (its own HUD; lane cues are not used there).</summary>
        private static UnityEngine.MonoBehaviour _otherStage;

        /// <summary>The results of the current stage are on screen.</summary>
        public static bool ShowingResults { get; private set; }

        /// <summary>The game over screen is up (the game pauses its audio behind it).</summary>
        public static bool GameOver { get; private set; }

        private static float _nextStageSearch;
        private static bool _hadStage;

        public static bool SpeechAllowed => !InSong || ModConfig.SpeakDuringSongs.Value;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _player = AccessTools.FieldRefAccess<RRStageController, RRPlayerController>("_playerController");
                _vibe = AccessTools.FieldRefAccess<RRStageController, float>("_currentVibePower");
                _vibeMin = AccessTools.FieldRefAccess<RRStageController, int>("_minVibePowerToActivate");
                _vibeMax = AccessTools.FieldRefAccess<RRStageController, int>("_maxVibePower");
                _vibeActive = AccessTools.FieldRefAccess<RRStageController, bool>("_isVibePowerActive");
                var prog = AccessTools.Method(typeof(RRStageController), "GetPercentageThroughStage");
                if (prog != null) _progress = AccessTools.MethodDelegate<Func<RRStageController, float, float>>(prog);
                _inputRecord = AccessTools.Field(typeof(RRStageController), "_stageInputRecord");

                var show = AccessTools.Method(typeof(StageFlowUiController), nameof(StageFlowUiController.ShowResults));
                harmony.Patch(show, postfix: new HarmonyMethod(typeof(StageWatcher), nameof(ShowResultsPostfix)));
                Plugin.LogInfo("Patched StageFlowUiController.ShowResults");
                harmony.Patch(AccessTools.Method(typeof(GameOverScreen), nameof(GameOverScreen.SetFinalProgressPercentage)),
                    postfix: new HarmonyMethod(typeof(StageWatcher), nameof(GameOverPostfix)));
                harmony.Patch(AccessTools.Method(typeof(Shared.Audio.AudioManager), nameof(Shared.Audio.AudioManager.SetGamePaused)),
                    postfix: new HarmonyMethod(typeof(StageWatcher), nameof(PausedPostfix)));
                harmony.Patch(AccessTools.Method(typeof(Shared.Audio.AudioManager), nameof(Shared.Audio.AudioManager.SetGameUnpaused)),
                    postfix: new HarmonyMethod(typeof(StageWatcher), nameof(UnpausedPostfix)));
            }
            catch (Exception ex) { Plugin.LogError("Stage watcher: " + ex); }
        }

        private static float _savedMusic = -1f;

        /// <summary>Lowers the game's music for the song (Music volume during songs). Never saved by the game:
        /// SetAudioVolume only moves the VCA, and the music is restored whenever the game is paused.</summary>
        public static void DuckMusic()
        {
            try
            {
                int pct = ModConfig.MusicVolume.Value;
                var am = Shared.Audio.AudioManager.Instance;
                if (pct >= 100 || am == null || _savedMusic >= 0f) return;
                _savedMusic = am.GetAudioVolume(Shared.Audio.AudioManager.MUSIC_VOLUME_ID);
                if (_savedMusic < 0f) return;
                am.SetAudioVolume(Shared.Audio.AudioManager.MUSIC_VOLUME_ID, _savedMusic * pct / 100f);
                Plugin.LogDebug($"event music lowered {_savedMusic:0.00} -> {am.GetAudioVolume(Shared.Audio.AudioManager.MUSIC_VOLUME_ID):0.00}");
            }
            catch (Exception ex) { Plugin.LogDebug("Duck music: " + ex.Message); }
        }

        public static void RestoreMusic()
        {
            if (_savedMusic < 0f) return;
            try
            {
                Shared.Audio.AudioManager.Instance?.SetAudioVolume(Shared.Audio.AudioManager.MUSIC_VOLUME_ID, _savedMusic);
                Plugin.LogDebug($"event music restored {_savedMusic:0.00}");
            }
            catch (Exception ex) { Plugin.LogDebug("Restore music: " + ex.Message); }
            _savedMusic = -1f;
        }

        /// <summary>True while a stage is paused (its HUD numbers are left out of screen reading).</summary>
        public static bool Paused { get; private set; }

        private static void PausedPostfix() { Paused = true; RestoreMusic(); }
        private static void UnpausedPostfix() { Paused = false; DuckMusic(); }

        public static void OnStageStarted()
        {
            CueCheck.Report();
            Paused = false;
            ShowingResults = false;
            GameOver = false;
            RestoreMusic();
            DuckMusic();
            _stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
            _lastHealth = -1;
            _vibeReady = false;
            _lastCombo = 0;
            _stageStartTime = Time.unscaledTime;
            Plugin.LogInfo("Stage started" + (_stage != null ? "" : " (no RRStageController found)"));
        }

        private static StageInputRecord Record => _stage != null && _inputRecord != null ? _inputRecord.GetValue(_stage) as StageInputRecord : null;

        public static void Update()
        {
            if (_otherStage == null) _otherStage = null; // destroyed with its scene
            if (_stage == null)
            {
                if (_hadStage) { _hadStage = false; CueCheck.Report(); }
                Paused = false; // left the stage from the pause menu
                GameOver = false;
                // Know about the stage before its first enemy moves (countdown, HUD), so screen reading stays quiet.
                if (Time.unscaledTime >= _nextStageSearch)
                {
                    _nextStageSearch = Time.unscaledTime + 0.5f;
                    _stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
                    if (_otherStage == null)
                    {
                        _otherStage = UnityEngine.Object.FindObjectOfType<BossBattles.BossBattleStageController>();
                    }
                }
            }
            else _hadStage = true;
            if (!InSong)
            {
                if (!Paused) RestoreMusic();
                return;
            }
            if (!Paused) DuckMusic();
            if (_stage == null) return;
            try
            {
                var player = _player(_stage);
                if (player != null)
                {
                    int hp = player.CurrentHealth;
                    if (_lastHealth >= 0 && hp != _lastHealth)
                    {
                        bool hurt = hp < _lastHealth;
                        if (hurt && ModConfig.LowHealthSound.Value && hp > 0 && hp <= ModConfig.LowHealthThreshold.Value)
                        {
                            CuePlayer.Play("warning", 0f, 0f, ModConfig.CueVolume.Value / 100f);
                            Plugin.LogDebug("event low health warning at " + hp);
                        }
                        if (ModConfig.AnnounceDamage.Value && SpeechAllowed && hp > 0)
                            Speech.SayEvent(Loc.F(hurt ? "health" : "healed", hp));
                    }
                    _lastHealth = hp;
                }

                bool ready = _vibeMax(_stage) > 0 && !_vibeActive(_stage) && _vibe(_stage) >= _vibeMin(_stage);
                if (ready && !_vibeReady)
                {
                    if (ModConfig.VibeReadyCue.Value) CuePlayer.Play("vibe_ready", 0f, 0f, ModConfig.CueVolume.Value / 100f * 0.8f);
                    Plugin.LogDebug("event vibe ready");
                    CueBot.OnVibeReady();
                    if (ModConfig.VibeReadySpeech.Value && SpeechAllowed) Speech.SayEvent(Loc.T("vibe_ready"));
                }
                _vibeReady = ready;

                var rec = Record;
                if (rec != null)
                {
                    int combo = rec.CurrentComboCount;
                    int step = ModConfig.ComboMilestone.Value;
                    if (step > 0 && combo > _lastCombo && combo / step > _lastCombo / step && SpeechAllowed)
                        Speech.SayEvent(Loc.F("combo", combo / step * step));
                    if (combo == 0 && _lastCombo >= 10 && ModConfig.AnnounceComboLost.Value && SpeechAllowed)
                        Speech.SayEvent(Loc.T("combo_lost"));
                    _lastCombo = combo;
                }
            }
            catch (Exception ex) { Plugin.LogDebug("Stage watcher: " + ex.Message); }
        }

        /// <summary>Hotkey readout of the HUD.</summary>
        public static string StatusText()
        {
            if (!InSong || _stage == null) return null;
            var parts = new List<string>();
            var player = _player(_stage);
            if (player != null) parts.Add(Loc.F("status.health", player.CurrentHealth, player.MaxHealth));
            var rec = Record;
            if (rec != null)
            {
                parts.Add(Loc.F("status.combo", rec.CurrentComboCount));
                parts.Add(Loc.F("status.score", rec.TotalScoreWithMultiplier));
            }
            if (_vibeMax(_stage) > 0)
            {
                if (_vibeActive(_stage)) parts.Add(Loc.T("status.vibe_active"));
                else parts.Add(Loc.F(_vibeReady ? "status.vibe_ready" : "status.vibe", Mathf.RoundToInt(_vibe(_stage) * 100f / _vibeMax(_stage))));
            }
            if (_progress != null)
            {
                try { parts.Add(Loc.F("status.progress", Mathf.RoundToInt(_progress(_stage, LaneCues.LastCapsule.TrueBeatNumber) * 100f))); } catch { }
            }
            return string.Join(", ", parts);
        }

        private static void GameOverPostfix(float finalProgressPercentage)
        {
            try
            {
                CuePlayer.CancelAllPending();
                RestoreMusic();
                GameOver = true;
                CueCheck.Report();
                float pct = finalProgressPercentage <= 1.0001f ? finalProgressPercentage * 100f : finalProgressPercentage;
                Speech.SayEvent(Loc.F("results.gameover", Mathf.RoundToInt(pct)));
            }
            catch (Exception ex) { Plugin.LogError("Game over: " + ex.Message); }
        }

        private static void ShowResultsPostfix(StageInputRecord stageInputRecord, bool isNewHighScore, bool didNotFinish, bool wasFullCombo)
        {
            try
            {
                CuePlayer.CancelAllPending();
                RestoreMusic();
                ShowingResults = true;
                CueCheck.Report();
                if (!ModConfig.AnnounceResults.Value || stageInputRecord == null) return;
                var r = stageInputRecord;
                var parts = new List<string>
                {
                    Loc.T(didNotFinish ? "results.failed" : "results.complete"),
                    Loc.F("results.score", r.TotalScoreWithMultiplier)
                };
                if (isNewHighScore) parts.Add(Loc.T("results.highscore"));
                if (wasFullCombo) parts.Add(Loc.T("results.fullcombo"));
                parts.Add(Loc.F("results.maxcombo", r.MaxComboCount));
                parts.Add(Loc.F("results.perfect", r.GetNumInputsForRating(InputRating.Perfect)));
                parts.Add(Loc.F("results.great", r.GetNumInputsForRating(InputRating.Great)));
                parts.Add(Loc.F("results.good", r.GetNumInputsForRating(InputRating.Good)));
                parts.Add(Loc.F("results.ok", r.GetNumInputsForRating(InputRating.Ok)));
                parts.Add(Loc.F("results.missed", r.TotalMisses));
                Speech.SayEvent(string.Join(", ", parts));
            }
            catch (Exception ex) { Plugin.LogError("Results: " + ex.Message); }
        }
    }
}
