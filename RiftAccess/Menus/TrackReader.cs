using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using RiftAccess.Core;
using Shared.MenuOptions;
using Shared.TrackData;
using Shared.TrackSelection;
using UnityEngine;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Track lists (BaseTrackSelectionOptionGroup and its infinite/custom variants). The list recycles a pool of
    /// option objects, so the track comes from the group's data (_trackMetaData[_selectedTrackIndex]) and its
    /// status (new, free, locked, grade) from the highlighted option. Announced on every OnSelectedTrackChanged,
    /// a frame later so the option has been refreshed.
    /// </summary>
    internal static class TrackReader
    {
        private static AccessTools.FieldRef<BaseTrackSelectionOptionGroup, List<ITrackMetadata>> _meta;
        private static AccessTools.FieldRef<BaseTrackSelectionOptionGroup, Shared.Difficulty> _difficulty;
        private static AccessTools.FieldRef<BaseTrackSelectionOptionGroup, int> _index;
        private static BaseTrackSelectionOptionGroup _pending;
        private static int _pendingFrame;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _meta = AccessTools.FieldRefAccess<BaseTrackSelectionOptionGroup, List<ITrackMetadata>>("_trackMetaData");
                _index = AccessTools.FieldRefAccess<BaseTrackSelectionOptionGroup, int>("_selectedTrackIndex");
                _difficulty = AccessTools.FieldRefAccess<BaseTrackSelectionOptionGroup, Shared.Difficulty>("_selectedDifficulty");
                harmony.Patch(AccessTools.Method(typeof(BaseTrackSelectionOptionGroup), "OnSelectedTrackChanged"),
                    postfix: new HarmonyMethod(typeof(TrackReader), nameof(ChangedPostfix)));
                Plugin.LogInfo("Patched BaseTrackSelectionOptionGroup.OnSelectedTrackChanged (track lists)");
            }
            catch (Exception ex) { Plugin.LogError("Track reader: " + ex); }
        }

        public static bool IsTrackList(SelectableOption o) => o != null && o.GetComponentInParent<BaseTrackSelectionOptionGroup>() != null;

        private static void ChangedPostfix(BaseTrackSelectionOptionGroup __instance)
        {
            _pending = __instance;
            _pendingFrame = Time.frameCount;
        }

        public static void Update()
        {
            if (_pending == null || Time.frameCount < _pendingFrame + 2) return;
            var group = _pending;
            _pending = null;
            if (!group.isActiveAndEnabled || ModSettingsMenu.IsOpen || HelpMenu.IsOpen) return;
            try
            {
                var parts = new List<string>();
                var list = _meta(group);
                int i = _index(group);
                ITrackMetadata meta = list != null && i >= 0 && i < list.Count ? list[i] : null;
                var option = group.GetSelectedOption();
                if (meta is FolderTrackMetadata)
                {
                    parts.Add(meta.TrackName);
                    parts.Add(Loc.T("folder"));
                }
                else if (meta != null)
                {
                    if (!string.IsNullOrEmpty(meta.TrackName)) parts.Add(meta.TrackName);
                    if (!string.IsNullOrEmpty(meta.TrackSubtitle)) parts.Add(meta.TrackSubtitle);
                    if (!string.IsNullOrEmpty(meta.ArtistName)) parts.Add(meta.ArtistName);
                }
                if (option != null)
                {
                    // Status texts of the highlighted row: new, free, locked, grade, level. Name and artist already said.
                    foreach (var s in MenuReader.Describe(option))
                    {
                        if (parts.Contains(s) || s == "--" || s.StartsWith("sfg", StringComparison.Ordinal)) continue;
                        if (meta != null && (s == meta.ArtistName || s == meta.TrackName)) continue;
                        parts.Add(s);
                    }
                }
                if (meta != null && !(meta is FolderTrackMetadata) && meta.BeatsPerMinute.HasValue && meta.BeatsPerMinute.Value > 0)
                    parts.Add(Loc.F("bpm", meta.BeatsPerMinute.Value.ToString("0", CultureInfo.InvariantCulture)));
                // The best grade on this difficulty: drawn as a letter badge on the row, never as text.
                if (meta != null && !(meta is FolderTrackMetadata) && !string.IsNullOrEmpty(meta.LevelId) && _difficulty != null)
                {
                    try
                    {
                        string g = Shared.PlayerData.PlayerDataUtil.GetLetterGradeForDifficulty(meta.LevelId, _difficulty(group));
                        if (!string.IsNullOrEmpty(g) && g != "-" && g != "DNF") parts.Add(Loc.F("results.grade", g));
                    }
                    catch { }
                }
                if (ModConfig.ReadPosition.Value && list != null) parts.Add(Loc.F("position", i + 1, list.Count));
                if (parts.Count == 0) return;
                MenuReader.SetFocus(option);
                Speech.SayFocus(string.Join(", ", parts));
            }
            catch (Exception ex) { Plugin.LogError("Track reader: " + ex.Message); }
        }
    }
}
