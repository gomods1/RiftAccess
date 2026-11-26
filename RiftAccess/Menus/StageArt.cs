using System.Collections.Generic;
using HarmonyLib;
using RhythmRift;
using RiftAccess.Core;
using UnityEngine;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Pictures that move during a stage, for the review buffer (Alt+Up while paused): the two character portraits
    /// beside the Rhythm Rift board and how they are reacting, and the minions a boss has on the field.
    /// </summary>
    internal static class StageArt
    {
        // RR Portrait Database key (lower case) -> character key
        private static readonly Dictionary<string, string> PortraitChars = new Dictionary<string, string>
        {
            { "cadence", "char.cadence" }, { "cadence_supporter", "char.supporter_cadence" }, { "cadencecrypt", "char.cadence_crypt" },
            { "dove", "char.dove" }, { "necrodancerburger", "char.ndburger" }, { "necrodancercloak", "char.ndcloak" },
            { "necrodancercrypt", "char.nd_boss" }, { "heph", "char.heph" }, { "merlin", "char.merlin" }, { "suzu", "char.suzu" },
            { "nocturna", "char.nocturna" }, { "shopkeeper", "char.shopkeeper" }, { "matron", "char.matron" },
            { "harmonie", "char.harmonie" }, { "queen", "char.queen" }, { "reaper", "char.reaper" }, { "coda", "char.coda" },
            { "beastmaster", "char.beastmaster" }, { "apricot", "char.smb" }, { "banana", "char.madeline" },
            { "banana02", "char.badeline" }, { "cherry", "char.peppino" }, { "customcounterpartyuzu", "char.yuzu" },
        };

        private static AccessTools.FieldRef<RRPortraitUiController, string> _heroId, _counterId;
        private static AccessTools.FieldRef<RRPortraitUiController, RRPerformanceLevel> _level;
        private static AccessTools.FieldRef<RRPortraitUiController, RRPortraitView> _counterView;
        private static AccessTools.FieldRef<RRPortraitView, bool> _invert;

        /// <summary>One line per portrait beside the board: who it is and how they look right now.</summary>
        public static List<string> PortraitLines()
        {
            var lines = new List<string>();
            try
            {
                var ui = Object.FindObjectOfType<RRPortraitUiController>();
                if (ui == null) return lines;
                if (_heroId == null)
                {
                    _heroId = AccessTools.FieldRefAccess<RRPortraitUiController, string>("_heroPortraitId");
                    _counterId = AccessTools.FieldRefAccess<RRPortraitUiController, string>("_counterpartPortraitId");
                    _level = AccessTools.FieldRefAccess<RRPortraitUiController, RRPerformanceLevel>("_currentPerformanceLevel");
                    _counterView = AccessTools.FieldRefAccess<RRPortraitUiController, RRPortraitView>("_counterpartPortraitViewInstance");
                    _invert = AccessTools.FieldRefAccess<RRPortraitView, bool>("_shouldInvertReactions");
                }
                var level = _level(ui);
                string look = Look(level, false);
                Add(lines, _heroId(ui), look, false);
                var cv = _counterView(ui);
                bool rival = cv != null && _invert(cv);
                Add(lines, _counterId(ui), Look(level, rival), rival);
            }
            catch (System.Exception e) { Plugin.LogDebug("portrait lines: " + e.Message); }
            return lines;
        }

        private static void Add(List<string> lines, string id, string look, bool rival)
        {
            if (string.IsNullOrEmpty(id)) return;
            string who = PortraitChars.TryGetValue(id.ToLowerInvariant(), out var k) ? Loc.T(k)
                       : id.StartsWith("Custom") ? Loc.T("rrp.custom") : null;
            if (who == null) { Plugin.LogDebug("Undescribed song portrait " + id); return; }
            string line = Loc.F("rrp.line", who, look);
            if (rival) line += " " + Loc.T("rrp.rival");
            lines.Add(line);
        }

        // Mirrors RRPortraitView.PerformanceLevelChange: poor and awesome swap for a rival portrait.
        private static string Look(RRPerformanceLevel level, bool invert)
        {
            if (Shared.StorageData.Storage.User.Accessibility.ShowStaticPortraits) return Loc.T("rrp.static");
            switch (level)
            {
                case RRPerformanceLevel.GameOver:
                case RRPerformanceLevel.Terrible:
                case RRPerformanceLevel.Poor: return Loc.T(invert ? "rrp.well" : "rrp.poorly");
                case RRPerformanceLevel.Awesome:
                case RRPerformanceLevel.Amazing: return Loc.T(invert ? "rrp.poorly" : "rrp.well");
                case RRPerformanceLevel.VibePower: return Loc.T(invert ? "rrp.poorly" : "rrp.vibe");
                default: return Loc.T("rrp.normal");
            }
        }

        private static AccessTools.FieldRef<BossBattles.BossController, List<BossBattles.BossMinionView>> _minions;

        /// <summary>The boss's minions now on the field, described once per kind.</summary>
        public static List<string> MinionLines()
        {
            var lines = new List<string>();
            try
            {
                if (_minions == null) _minions = AccessTools.FieldRefAccess<BossBattles.BossController, List<BossBattles.BossMinionView>>("_activeMinionViews");
                foreach (var boss in Object.FindObjectsOfType<BossBattles.BossController>())
                    foreach (var m in _minions(boss))
                    {
                        if (m == null || !m.IsActive) continue;
                        string t = m.MinionType ?? "";
                        if (t.EndsWith("L") || t.EndsWith("R")) t = t.Substring(0, t.Length - 1);
                        string key = "mn." + t.ToLowerInvariant();
                        if (!Loc.Has(key)) { Plugin.LogDebug("Undescribed minion " + m.MinionType); continue; }
                        string s = Loc.T(key);
                        if (!lines.Contains(s)) lines.Add(s);
                    }
            }
            catch (System.Exception e) { Plugin.LogDebug("minion lines: " + e.Message); }
            return lines;
        }
    }
}
