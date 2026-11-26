using System;
using System.Collections.Generic;
using HarmonyLib;
using RhythmRift;
using RhythmRift.Enemies;
using RiftAccess.Audio;
using RiftAccess.Core;
using Unity.Mathematics;
using UnityEngine;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Rhythm Rift rules the game shows only on screen:
    /// - Input lockout: three presses with nothing to hit within a beat (or nine within five beats) while a monster
    ///   is close lock the lane keys for 1.5 beats (a padlock overlay). Monsters arriving meanwhile hit you.
    /// - What hurt you: the monster and its lane (seen on screen), or a stray press under the Glass Guitar pin.
    /// - A Down press (all three lanes) with one lane empty misses in every lane.
    /// - Vibe chains: start (glowing monsters, no sound), broken and complete (sounds without meaning).
    /// - Active modifiers (pins): only an icon in the HUD.
    /// - The letter grade on the results screen (an animation and a sound per grade, never the letter).
    /// - "Bane" modifiers: the game hits a damaged monster's follow-ups itself (LaneCues asks AutoHitsFollowUps).
    /// </summary>
    internal static class RuleWatcher
    {
        private static AccessTools.FieldRef<RRStageController, bool> _lockedOut;
        private static bool _wasLocked;
        private static bool _lockExplained, _fullRowExplained;
        private static float _nextFullRowSpeech;

        /// <summary>Why the player was last hurt, and when (unscaled time).</summary>
        private static string _hurtBy;
        private static float _hurtAt = -10f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _lockedOut = AccessTools.FieldRefAccess<RRStageController, bool>("_areInputsLockedOut");
                harmony.Patch(AccessTools.Method(typeof(RRStageController), "HandleEnemyAttack"),
                    prefix: new HarmonyMethod(typeof(RuleWatcher), nameof(EnemyAttackPrefix)));
                harmony.Patch(AccessTools.Method(typeof(RRStageController), "HandleCodaErrantDamage"),
                    prefix: new HarmonyMethod(typeof(RuleWatcher), nameof(StrayDamagePrefix)));
                harmony.Patch(AccessTools.Method(typeof(RREnemyController), nameof(RREnemyController.AttackEnemiesAtPositions)),
                    postfix: new HarmonyMethod(typeof(RuleWatcher), nameof(AttackPostfix)));
                var me = typeof(RuleWatcher);
                _chainCounts = AccessTools.FieldRefAccess<RRStageController, Dictionary<Guid, int>>("_vibeChainEnemyCounts");
                harmony.Patch(AccessTools.Method(typeof(RRStageController), "ActivateVibeChain"), postfix: new HarmonyMethod(me, nameof(ChainStartPostfix)));
                harmony.Patch(AccessTools.Method(typeof(RRStageController), "DeactivateVibeChain"), postfix: new HarmonyMethod(me, nameof(ChainEndPostfix)));
                harmony.Patch(AccessTools.Method(typeof(RRStageController), "VibeChainSuccess"), prefix: new HarmonyMethod(me, nameof(ChainSuccessPrefix)));
                harmony.Patch(AccessTools.Method(typeof(Shared.StageModifierUiView), nameof(Shared.StageModifierUiView.SetView)), postfix: new HarmonyMethod(me, nameof(ModifiersPostfix)));
                harmony.Patch(AccessTools.Method(typeof(Shared.ScoreResultsView), "PlayLetterGradeAnim"), postfix: new HarmonyMethod(me, nameof(GradePostfix)));
                _bane = AccessTools.PropertyGetter(typeof(RREnemy), "IsBanePinActive");
                harmony.Patch(AccessTools.Method(typeof(Shared.StageFlowUiController), nameof(Shared.StageFlowUiController.PerformCountdownStep)),
                    postfix: new HarmonyMethod(me, nameof(CountdownPostfix)));
                _rrCountdownSfx = AccessTools.Field(typeof(RRStageController), "_shouldPlayCountdownSfx");
                _bbCountdownSfx = AccessTools.Field(typeof(BossBattles.BossBattleStageController), "_shouldPlayCountdownSfx");
                harmony.Patch(AccessTools.Method(typeof(RhythmRift.Traps.RRTrapController), nameof(RhythmRift.Traps.RRTrapController.SpawnTrap)),
                    prefix: new HarmonyMethod(me, nameof(TrapPrefix)));
                Plugin.LogInfo("Patched stage rules (lockout, damage source, full row, vibe chains, modifiers, grade)");
            }
            catch (Exception ex) { Plugin.LogError("Rule watcher: " + ex); }
        }

        private static double _timingSum;
        private static int _timingCount;

        /// <summary>"average 12 ms late" over this song's hits, or null before the first hit.</summary>
        public static string TimingText()
        {
            if (_timingCount == 0) return null;
            int ms = (int)Math.Round(_timingSum / _timingCount * 1000.0);
            if (Math.Abs(ms) < 3) return Loc.T("timing.ontime");
            return Loc.F(ms < 0 ? "timing.early" : "timing.late", Math.Abs(ms));
        }

        public static void OnStageStarted()
        {
            _timingSum = 0; _timingCount = 0;
            _wasLocked = false;
            _hurtBy = null;
        }

        /// <summary>Per frame while a Rhythm Rift song plays.</summary>
        public static void Update(RRStageController stage)
        {
            FlushTraps();
            if (stage == null || _lockedOut == null) return;
            bool locked;
            try { locked = _lockedOut(stage); } catch { return; }
            if (locked == _wasLocked) return;
            _wasLocked = locked;
            if (!ModConfig.LockoutCue.Value) return;
            float vol = ModConfig.CueVolume.Value / 100f;
            if (locked)
            {
                CuePlayer.Play("locked", 0f, 0f, vol);
                Plugin.LogDebug("event input locked out");
                if (StageWatcher.SpeechAllowed)
                {
                    Speech.SayEvent(Loc.T(_lockExplained ? "locked" : "locked.first"));
                    _lockExplained = true;
                }
            }
            else
            {
                CuePlayer.Play("unlocked", 0f, 0f, vol * 0.8f);
                Plugin.LogDebug("event input unlocked");
            }
        }

        /// <summary>The reason for the health loss just seen, if it happened in the last moment, else null.</summary>
        public static string TakeHurtReason()
        {
            string r = Time.unscaledTime - _hurtAt < 0.5f ? _hurtBy : null;
            _hurtBy = null;
            return r;
        }

        private static void EnemyAttackPrefix(int2 attackLocation, int attackDamage, string enemyName)
        {
            if (attackDamage < 1) return;
            _hurtBy = Loc.F("hurt.by", EnemyName(enemyName), LaneName(attackLocation.x));
            _hurtAt = Time.unscaledTime;
        }

        private static void StrayDamagePrefix()
        {
            _hurtBy = Loc.T("hurt.stray");
            _hurtAt = Time.unscaledTime;
        }

        private static void AttackPostfix(List<int2> positionsToAttack, List<RREnemyController.EnemyHitData> __result)
        {
            try
            {
                if (__result != null)
                    foreach (var h in __result)
                        if (h.InputRating != Shared.InputRating.Miss)
                        {
                            _timingSum += (h.InputBeat - h.TargetBeat) * LaneCues.LastCapsule.BeatLengthInSeconds;
                            _timingCount++;
                        }
                if (positionsToAttack == null || __result == null || positionsToAttack.Count < 3) return;
                if (__result.Count == 0 || __result.Count >= positionsToAttack.Count) return;
                Plugin.LogDebug($"event full row press missed: {__result.Count} of {positionsToAttack.Count} lanes had a monster");
                if (!ModConfig.LockoutCue.Value || !StageWatcher.SpeechAllowed || Time.unscaledTime < _nextFullRowSpeech) return;
                _nextFullRowSpeech = Time.unscaledTime + 4f;
                Speech.SayEvent(Loc.T(_fullRowExplained ? "fullrow.missed" : "fullrow.missed.first"));
                _fullRowExplained = true;
            }
            catch (Exception ex) { Plugin.LogDebug("Full row check: " + ex.Message); }
        }

        // ---- Start countdown: four animated steps, silent unless the stage plays its own countdown sounds ----
        private static System.Reflection.FieldInfo _rrCountdownSfx, _bbCountdownSfx;
        private static int _countStep;
        private static float _lastCountStep = -10f;

        private static void CountdownPostfix()
        {
            try
            {
                float now = Time.unscaledTime;
                if (now - _lastCountStep > 3f) _countStep = 0;
                _lastCountStep = now;
                _countStep++;
                if (!ModConfig.CuesEnabled.Value) return;
                object stage = UnityEngine.Object.FindObjectOfType<RRStageController>();
                var field = _rrCountdownSfx;
                if (stage == null) { stage = UnityEngine.Object.FindObjectOfType<BossBattles.BossBattleStageController>(); field = _bbCountdownSfx; }
                if (stage != null && field != null && (bool)field.GetValue(stage)) return; // the game counts aloud itself
                CuePlayer.Play("click", 0f, 0f, ModConfig.CueVolume.Value / 100f * 0.7f, _countStep >= 4 ? 1.5f : 1f);
                Plugin.LogDebug("event countdown step " + _countStep);
            }
            catch (Exception ex) { Plugin.LogDebug("Countdown: " + ex.Message); }
        }

        // ---- Traps: drawn on the board, a generic spawn sound with no lane ----
        private static readonly List<string> _traps = new List<string>();

        private static void TrapPrefix(TrapSpawnData trapSpawnData)
        {
            try
            {
                var d = trapSpawnData;
                string key;
                switch (d.TrapType)
                {
                    case RRTrapType.Coals: key = "Coal"; break;
                    case RRTrapType.PortalIn: key = "Portal"; break;
                    case RRTrapType.Bounce: key = "Bounce"; break;
                    case RRTrapType.Mystery: key = "Mystery"; break;
                    case RRTrapType.Wind: case RRTrapType.Freeze: key = null; break;
                    default: return; // portal exits are announced with their entrance; debug traps never
                }
                string name = (key != null ? GameText("NavItemDisplayName_Trap_" + key) : null) ?? d.TrapType.ToString();
                string where = Loc.F("trap.where", LaneName(RRGridView.GetLaneGridXValue(d.TrapDropLane)), d.TrapDropRow);
                string extra = null;
                if (d.TrapType == RRTrapType.Bounce) extra = Loc.F("trap.bounce", DirName(d.TrapDirection));
                else if (d.TrapType == RRTrapType.PortalIn) extra = Loc.F("trap.portal", Loc.F("trap.where", LaneName(RRGridView.GetLaneGridXValue(d.ChildTrapLane)), d.ChildTrapRow));
                else if (d.TrapType == RRTrapType.Coals) extra = Loc.T("trap.coal");
                else if (d.TrapType == RRTrapType.Mystery) extra = Loc.T("trap.mystery");
                string text = name + ", " + where + (extra != null ? ", " + extra : "");
                Plugin.LogDebug("event trap " + text);
                if (ModConfig.AnnounceTraps.Value) _traps.Add(text);
            }
            catch (Exception ex) { Plugin.LogDebug("Trap: " + ex.Message); }
        }

        private static void FlushTraps()
        {
            if (_traps.Count == 0) return;
            if (StageWatcher.SpeechAllowed) Speech.SayEvent(string.Join(". ", _traps));
            _traps.Clear();
        }

        private static string DirName(RRTrapDirection d)
        {
            switch (d)
            {
                case RRTrapDirection.Left: return Loc.T("dir.left");
                case RRTrapDirection.Right: return Loc.T("dir.right");
                case RRTrapDirection.Up: return Loc.T("dir.back");
                case RRTrapDirection.Down: return Loc.T("dir.forward");
                case RRTrapDirection.UpLeft: return Loc.F("dir.diag", Loc.T("dir.left"), Loc.T("dir.back"));
                case RRTrapDirection.UpRight: return Loc.F("dir.diag", Loc.T("dir.right"), Loc.T("dir.back"));
                case RRTrapDirection.DownLeft: return Loc.F("dir.diag", Loc.T("dir.left"), Loc.T("dir.forward"));
                default: return Loc.F("dir.diag", Loc.T("dir.right"), Loc.T("dir.forward"));
            }
        }

        // ---- Vibe chains ----
        private static AccessTools.FieldRef<RRStageController, Dictionary<Guid, int>> _chainCounts;

        private static void ChainStartPostfix(RRStageController __instance, VibeChainStartData vibeChainStartData)
        {
            try
            {
                var counts = _chainCounts?.Invoke(__instance);
                if (counts == null || !counts.TryGetValue(vibeChainStartData.vibeChainEventId, out int n) || n <= 0) return;
                Plugin.LogDebug("event vibe chain of " + n);
                if (ModConfig.AnnounceVibeChains.Value && StageWatcher.SpeechAllowed) Speech.SayEvent(Loc.F("chain.start", n));
            }
            catch (Exception ex) { Plugin.LogDebug("Chain start: " + ex.Message); }
        }

        private static void ChainSuccessPrefix(object vibeChain)
        {
            try
            {
                bool perfect = vibeChain != null && (bool)AccessTools.Field(vibeChain.GetType(), "perfect").GetValue(vibeChain);
                Plugin.LogDebug("event vibe chain complete" + (perfect ? " perfect" : ""));
                if (ModConfig.AnnounceVibeChains.Value && StageWatcher.SpeechAllowed)
                    Speech.SayEvent(Loc.T(perfect ? "chain.perfect" : "chain.done"));
            }
            catch (Exception ex) { Plugin.LogDebug("Chain success: " + ex.Message); }
        }

        private static void ChainEndPostfix(bool endedByMiss)
        {
            if (!endedByMiss) return;
            Plugin.LogDebug("event vibe chain broken");
            if (ModConfig.AnnounceVibeChains.Value && StageWatcher.SpeechAllowed) Speech.SayEvent(Loc.T("chain.broken"));
        }

        // ---- Modifiers ----
        private static string _lastModifiers;
        private static float _lastModifiersTime = -100f;

        private static void ModifiersPostfix(Shared.Pins.PinDefinition[] activePins, Shared.StageModifierUiView.ModifierMode mode)
        {
            try
            {
                var parts = new List<string>();
                if (mode == Shared.StageModifierUiView.ModifierMode.MicroRift) parts.Add(Loc.T("mod.microrift"));
                else if (activePins != null)
                    foreach (var pin in activePins)
                    {
                        if (string.IsNullOrWhiteSpace(pin.PinName) || pin.PinType != Shared.Pins.PinType.Gameplay) continue;
                        string name = GameText(pin.PinNameLocKey) ?? pin.PinName;
                        string effect = GameText(pin.PinEffectLocKey);
                        parts.Add(effect != null ? name + ": " + effect : name);
                    }
                if (parts.Count == 0) return;
                string text = Loc.F("mod.active", string.Join(". ", parts));
                if (text == _lastModifiers && Time.unscaledTime - _lastModifiersTime < 60f) return;
                _lastModifiers = text;
                _lastModifiersTime = Time.unscaledTime;
                Plugin.LogDebug("event modifiers: " + text);
                Speech.SayEvent(text);
            }
            catch (Exception ex) { Plugin.LogDebug("Modifiers: " + ex.Message); }
        }

        // ---- Results grade ----
        private static void GradePostfix(string letterGrade)
        {
            if (string.IsNullOrEmpty(letterGrade) || !ModConfig.AnnounceResults.Value) return;
            Plugin.LogDebug("event letter grade " + letterGrade);
            Speech.SayEvent(Loc.F("results.grade", letterGrade));
        }

        // ---- Bane modifiers ----
        private static System.Reflection.MethodInfo _bane;
        private static int _baneFrame = -1;
        private static bool _anyBane;

        /// <summary>True when a "Bane" modifier makes the game hit this monster's follow-ups for the player.</summary>
        public static bool AutoHitsFollowUps(RREnemy e)
        {
            if (_baneFrame != Time.frameCount)
            {
                _baneFrame = Time.frameCount;
                _anyBane = false;
                try { foreach (var p in Shared.Pins.PinsController.GetActivePins()) if (p != null && p.EndsWith("Bane")) _anyBane = true; } catch { }
            }
            if (!_anyBane || _bane == null || e == null) return false;
            try { return (bool)_bane.Invoke(e, null); } catch { return false; }
        }

        public static string GameName(string key) => GameText(key);

        private static string GameText(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            try
            {
                string s = TicToc.Localization.Localizer.GetText(key);
                if (!string.IsNullOrEmpty(s) && s != key) return Speech.Clean(s);
            }
            catch { }
            return null;
        }

        private static string LaneName(int x) => x == 0 ? Loc.T("lane.left") : x == 2 ? Loc.T("lane.right") : Loc.T("lane.mid");

        /// <summary>A monster's name in the game's language (its bestiary entry), else the internal English name.</summary>
        public static string EnemyName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return Loc.T("hurt.monster");
            return GameText("NavItemDisplayName_Bestiary_" + displayName.Replace(" ", "")) ?? displayName;
        }
    }
}
