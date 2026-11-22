using System;
using System.Collections.Generic;
using BossBattles;
using HarmonyLib;
using Minigames;
using RiftAccess.Audio;
using RiftAccess.Core;
using Shared;
using Shared.RhythmEngine;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Cues for the modes that are not lanes: boss battles and minigames. Both show an indicator that shrinks
    /// toward the moment to press a direction (BBHitIndicator / MGAssistIndicator.PlayHitIndicatorFx, called when
    /// it appears, with the seconds until that moment). The cue plays LeadBeats before the moment, never earlier
    /// than the indicator itself appears. Direction is pan (left, right) and pitch (up high, down low); a boss
    /// attack to dodge has its own whoosh. Hidden indicators (boss battles on Hard) are never played, so blind and
    /// sighted players get the same warning.
    /// </summary>
    internal static class DirectionCues
    {
        private static float _bbBeatLength;
        private static int _botIds;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                harmony.Patch(AccessTools.Method(typeof(BossBattleStageController), "PlayHitIndicator"),
                    prefix: new HarmonyMethod(typeof(DirectionCues), nameof(BossEventPrefix)));
                harmony.Patch(AccessTools.Method(typeof(BBHitIndicator), nameof(BBHitIndicator.PlayHitIndicatorFx)),
                    postfix: new HarmonyMethod(typeof(DirectionCues), nameof(BossIndicatorPostfix)));
                Plugin.LogInfo("Patched boss battle and minigame indicators (direction cues)");
            }
            catch (Exception ex) { Plugin.LogError("Direction cues: " + ex); }
        }

        /// <summary>The boss event carries the indicator length in beats; with its length in seconds that gives the beat.</summary>
        private static void BossEventPrefix(BossBattleBeatmapPlayer.BossHitIndicatorEvent bossHitIndicatorEvent)
        {
            _bbBeatLength = bossHitIndicatorEvent.animLengthInBeats > 0f ? -bossHitIndicatorEvent.animLengthInBeats : 0f;
        }

        private static void BossIndicatorPostfix(BBHitIndicatorAction hitIndicatorAction, ActionDirection actionDirection, float lengthInSeconds)
        {
            try
            {
                float beat = _bbBeatLength < 0f ? lengthInSeconds / -_bbBeatLength : 0.5f;
                bool dodge = hitIndicatorAction == BBHitIndicatorAction.Dodge;
                // Pummel (boss stunned): any of up, left or right attacks; it comes without a direction. Cue it in the centre.
                if (actionDirection == ActionDirection.None && hitIndicatorAction != BBHitIndicatorAction.Other) actionDirection = ActionDirection.Up;
                Cue(actionDirection, lengthInSeconds, beat, dodge ? "dodge" : "tap", 0f);
            }
            catch (Exception ex) { Plugin.LogDebug("Boss cue: " + ex.Message); }
        }

        private static MinigameBeatmapPlayer _mgPlayer;
        private static float _nextSearch;
        private static AccessTools.FieldRef<BeatmapPlayer, Beatmap> _activeBeatmap;
        private static Func<BeatmapPlayer, float> _beforeWindow;
        private static readonly HashSet<string> MgDone = new HashSet<string>();
        private static Beatmap _mgBeatmap;
        private static float _mgLastRel;

        /// <summary>
        /// Minigames: every move the game will check is a "CheckInput" event in the active beatmap (input name, beat,
        /// hold length). The game only acts on it when its input window opens, too late to warn anyone, but the
        /// beatmap holds it from the start: the mod reads the upcoming ones and cues each LeadBeats before its beat.
        /// Sighted players get the same information from the characters' animations. Loop regions (tutorials) move
        /// the beatmap's start time back, so positions are measured from it and cues repeat with the loop.
        /// </summary>
        public static void Update()
        {
            if (_mgPlayer == null)
            {
                if (UnityEngine.Time.unscaledTime < _nextSearch) return;
                _nextSearch = UnityEngine.Time.unscaledTime + 0.5f;
                _mgPlayer = UnityEngine.Object.FindObjectOfType<MinigameBeatmapPlayer>();
                if (_mgPlayer == null) return;
                if (_activeBeatmap == null)
                {
                    _activeBeatmap = AccessTools.FieldRefAccess<BeatmapPlayer, Beatmap>("_activeBeatmap");
                    var g = AccessTools.PropertyGetter(typeof(BeatmapPlayer), "BeforeBeatWindowInSeconds");
                    if (g != null) _beforeWindow = AccessTools.MethodDelegate<Func<BeatmapPlayer, float>>(g);
                }
                MgDone.Clear();
                Plugin.LogInfo("Minigame found: cueing its moves");
            }
            try { ScheduleMinigame(); }
            catch (Exception ex) { Plugin.LogDebug("Minigame cues: " + ex.Message); }
        }

        public static bool InMinigame => _mgPlayer != null;

        private static void ScheduleMinigame()
        {
            var bm = _activeBeatmap(_mgPlayer);
            float beat = _mgPlayer.BeatLengthInSeconds;
            if (bm == null || beat <= 0f || !ModConfig.CuesEnabled.Value) return;
            float rel = (_mgPlayer.FmodTimeCapsule.Time - _mgPlayer.CurrentBeatmapStartTime) / beat;
            if (bm != _mgBeatmap || rel < _mgLastRel - 0.5f) { MgDone.Clear(); _mgBeatmap = bm; }
            _mgLastRel = rel;
            float window = _beforeWindow != null ? _beforeWindow(_mgPlayer) / beat : 0f;
            float lead = Math.Max(0f, ModConfig.LeadBeats.Value);
            var events = bm.BeatmapEvents;
            for (int i = 0; i < events.Count; i++)
            {
                var ev = events[i];
                if (ev.type != "CheckInput") continue;
                float target = (float)ev.startBeatNumber + window;
                float secondsToPress = (target - rel) * beat;
                if (secondsToPress < 0.1f) continue;
                float secondsToCue = secondsToPress - lead * beat;
                if (secondsToCue > 0.2f) continue;
                string key = i + "|" + Math.Round(target * 24f);
                if (!MgDone.Add(key)) continue;
                string input = ev.GetFirstEventDataAsString("InputName") ?? "";
                int hold = ev.GetFirstEventDataAsInt("HoldNoteLengthInBeats") ?? 0;
                Plugin.LogDebug($"minigame input {input} at beat {target:0.00} (hold {hold})");
                Cue(DirectionOf(input), secondsToPress, beat, hold > 0 ? "hold" : "tap", hold > 0 ? hold * beat : 0f);
            }
        }

        private static ActionDirection DirectionOf(string input)
        {
            if (input.IndexOf("Left", StringComparison.OrdinalIgnoreCase) >= 0) return ActionDirection.Left;
            if (input.IndexOf("Right", StringComparison.OrdinalIgnoreCase) >= 0) return ActionDirection.Right;
            if (input.IndexOf("Down", StringComparison.OrdinalIgnoreCase) >= 0) return ActionDirection.Down;
            if (input.IndexOf("Up", StringComparison.OrdinalIgnoreCase) >= 0) return ActionDirection.Up;
            return ActionDirection.Mid;
        }

        private static void Cue(ActionDirection dir, float secondsToPress, float beatLength, string kind, float holdSeconds)
        {
            if (!ModConfig.CuesEnabled.Value || dir == ActionDirection.None) return;
            float latency = ModConfig.TimingOffsetMs.Value / 1000f - (LatencyManager.AudioLatencyOffset - LatencyManager.VideoLatencyOffset);
            float lead = Math.Max(0f, ModConfig.LeadBeats.Value) * Math.Max(0.05f, beatLength);
            float delay = Math.Max(0f, secondsToPress - lead + latency);
            float vol = ModConfig.CueVolume.Value / 100f;
            float width = ModConfig.PanWidth.Value / 100f;

            // A press of several directions at once (e.g. all) gets one sound per direction.
            foreach (ActionDirection d in new[] { ActionDirection.Left, ActionDirection.Up, ActionDirection.Mid, ActionDirection.Right, ActionDirection.Down })
            {
                if (dir != ActionDirection.All && (dir & d) == 0) continue;
                float pan = d == ActionDirection.Left ? -width : d == ActionDirection.Right ? width : 0f;
                string side = !ModConfig.LanePitch.Value ? "mid" : d == ActionDirection.Left ? "left" : d == ActionDirection.Right ? "right" : "mid";
                float pitch = d == ActionDirection.Up ? 1.26f : d == ActionDirection.Down ? 0.79f : 1f;
                CuePlayer.Play(kind + "_" + side, delay, pan, vol, pitch);
                var key = d == ActionDirection.Left ? UnityEngine.InputSystem.Key.LeftArrow : d == ActionDirection.Right ? UnityEngine.InputSystem.Key.RightArrow
                        : d == ActionDirection.Down ? UnityEngine.InputSystem.Key.DownArrow : UnityEngine.InputSystem.Key.UpArrow;
                CueBot.OnDirection("dir" + (++_botIds), key, UnityEngine.Time.unscaledTime + secondsToPress, holdSeconds);
                if (ModConfig.HitClick.Value) CuePlayer.Play("click", Math.Max(0f, secondsToPress + latency), pan, vol * 0.6f, pitch);
                if (holdSeconds > 0f && ModConfig.HoldReleaseCue.Value)
                    CuePlayer.Play("release_" + side, Math.Max(0f, secondsToPress + holdSeconds + latency), pan, vol, pitch);
            }
            Plugin.LogDebug($"direction cue {kind} {dir} press in {secondsToPress:0.000}s, cue in {delay:0.000}s");
        }
    }
}
