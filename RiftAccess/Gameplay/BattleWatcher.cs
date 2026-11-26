using System;
using System.Collections;
using BossBattles;
using HarmonyLib;
using RiftAccess.Core;
using UnityEngine;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// The boss battle and minigame HUD, which is only drawn: boss health and phase, your health, shield and vibe
    /// charges, the battle timer (boss grades are time based) and minigame health. H reads it; health changes and
    /// boss phases are announced.
    /// </summary>
    internal static class BattleWatcher
    {
        private static BossBattleStageController _boss;
        private static Minigames.MGPlayerController _mg;
        private static float _nextSearch;

        private static AccessTools.FieldRef<BossBattleStageController, BossController> _bossCtrl;
        private static AccessTools.FieldRef<BossBattleStageController, PlayerBossBattleController> _playerCtrl;
        private static AccessTools.FieldRef<BossBattleStageController, float> _time;
        private static AccessTools.FieldRef<PlayerBossBattleController, int> _shield;
        private static AccessTools.FieldRef<PlayerBossBattleController, int> _charges;
        private static AccessTools.FieldRef<BossController, int> _phase;
        private static System.Reflection.FieldInfo _phasesField;
        private static IList Phases(BossController b) => _phasesField?.GetValue(b) as IList;

        private static int _lastHealth = -1, _lastBossQuarter = -1, _lastPhase = -1;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _bossCtrl = AccessTools.FieldRefAccess<BossBattleStageController, BossController>("_bossController");
                _playerCtrl = AccessTools.FieldRefAccess<BossBattleStageController, PlayerBossBattleController>("_playerBossBattleController");
                _time = AccessTools.FieldRefAccess<BossBattleStageController, float>("_currentTimeSpentInBattle");
                _shield = AccessTools.FieldRefAccess<PlayerBossBattleController, int>("_currentShieldHealthBacking");
                _charges = AccessTools.FieldRefAccess<PlayerBossBattleController, int>("_currentVibeCharges");
                _phase = AccessTools.FieldRefAccess<BossController, int>("_currentBossPhaseIndex");
                _phasesField = AccessTools.Field(typeof(BossController), "_bossPhaseInstances");
                Plugin.LogInfo("Battle watcher ready");
            }
            catch (Exception ex) { Plugin.LogError("Battle watcher: " + ex); }
        }

        public static void Update()
        {
            if (Time.unscaledTime >= _nextSearch && (_boss == null && _mg == null))
            {
                _nextSearch = Time.unscaledTime + 0.5f;
                _boss = UnityEngine.Object.FindObjectOfType<BossBattleStageController>();
                if (_boss == null) _mg = UnityEngine.Object.FindObjectOfType<Minigames.MGPlayerController>();
                _lastHealth = -1; _lastBossQuarter = -1; _lastPhase = -1;
            }
            if (StageWatcher.Paused) return;
            try
            {
                int hp = -1;
                if (_boss != null)
                {
                    var p = _playerCtrl?.Invoke(_boss);
                    if (p != null) hp = p.PlayerCurrentHealth;
                    var b = _bossCtrl?.Invoke(_boss);
                    if (b != null && b.HasInitialized && b.MaxHealth > 0f)
                    {
                        int quarter = Mathf.CeilToInt(b.CurrentHealth / b.MaxHealth * 4f);
                        if (_lastBossQuarter > 0 && quarter < _lastBossQuarter && quarter > 0 && SpeechOk)
                            Speech.SayEvent(Loc.F("boss.health", quarter * 25));
                        _lastBossQuarter = quarter;
                        int phase = _phase(b);
                        int count = Phases(b)?.Count ?? 0;
                        if (phase != _lastPhase) Plugin.LogDebug($"event boss phase {phase + 1} of {count}");
                        _lastPhase = phase;
                    }
                }
                else if (_mg != null) hp = _mg.CurrentHealth;
                if (hp >= 0)
                {
                    if (_boss != null && _lastHealth > 0 && hp == 0) BossScenes.PlayerDefeated();
                    if (_lastHealth >= 0 && hp != _lastHealth && hp > 0 && ModConfig.AnnounceDamage.Value && SpeechOk)
                        Speech.SayEvent(Loc.F(hp < _lastHealth ? "health" : "healed", hp));
                    _lastHealth = hp;
                }
            }
            catch (Exception ex) { Plugin.LogDebug("Battle watcher: " + ex.Message); }
        }

        private static bool SpeechOk => ModConfig.SpeakDuringSongs.Value;

        /// <summary>H in a boss battle or minigame, else null.</summary>
        public static string StatusText()
        {
            try
            {
                var parts = new System.Collections.Generic.List<string>();
                if (_boss != null)
                {
                    var b = _bossCtrl?.Invoke(_boss);
                    if (b != null && b.MaxHealth > 0f)
                    {
                        parts.Add(Loc.F("boss.status", Mathf.CeilToInt(b.CurrentHealth / b.MaxHealth * 100f)));
                        int count = Phases(b)?.Count ?? 0;
                        if (count > 1) parts.Add(Loc.F("boss.phase", Mathf.Clamp(_phase(b) + 1, 1, count), count));
                    }
                    var p = _playerCtrl?.Invoke(_boss);
                    if (p != null)
                    {
                        parts.Add(Loc.F("status.health", p.PlayerCurrentHealth, p.PlayerMaxHealth));
                        int sh = _shield(p);
                        if (sh > 0) parts.Add(Loc.F("boss.shield", sh));
                        int ch = _charges(p);
                        if (ch > 0) parts.Add(Loc.F("boss.charges", ch));
                    }
                    int secs = Mathf.FloorToInt(_time(_boss));
                    parts.Add(Loc.F("boss.time", secs / 60, (secs % 60).ToString("00")));
                }
                else if (_mg != null) parts.Add(Loc.F("status.health", _mg.CurrentHealth, _mg.MaxHealth));
                return parts.Count > 0 ? string.Join(", ", parts) : null;
            }
            catch (Exception ex) { Plugin.LogDebug("Battle status: " + ex.Message); return null; }
        }
    }
}
