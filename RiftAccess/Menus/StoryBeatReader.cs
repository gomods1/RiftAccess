using System;
using HarmonyLib;
using RiftAccess.Core;
using Shared;
using Shared.PlayerData;
using Shared.Storyline;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Story mode beat details that are only icons: completed (a checkmark) and the best letter grade (a badge).
    /// Spoken after the beat's own texts when the highlighted beat changes.
    /// </summary>
    internal static class StoryBeatReader
    {
        private static AccessTools.FieldRef<StoryBeatMenuController, StoryBeatData> _beat;
        private static AccessTools.FieldRef<StoryBeatMenuController, MenuManager> _menu;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _beat = AccessTools.FieldRefAccess<StoryBeatMenuController, StoryBeatData>("_currentStoryBeatData");
                _menu = AccessTools.FieldRefAccess<StoryBeatMenuController, MenuManager>("_menuManager");
                harmony.Patch(AccessTools.Method(typeof(StoryBeatMenuController), "HandleSelectedMenuItemChanged"),
                    postfix: new HarmonyMethod(typeof(StoryBeatReader), nameof(Postfix)));
                Plugin.LogInfo("Patched StoryBeatMenuController.HandleSelectedMenuItemChanged");
            }
            catch (Exception ex) { Plugin.LogError("Story beat reader: " + ex); }
        }

        private static void Postfix(StoryBeatMenuController __instance)
        {
            try
            {
                var beat = _beat(__instance);
                if (beat == null || string.IsNullOrEmpty(beat.LevelId)) return;
                if (PlayerDataUtil.GetLockedStatus(beat.UnlockCriteria)) return;
                var parts = new System.Collections.Generic.List<string>();
                if (PlayerDataUtil.HasLevelBeenCompleted(beat.LevelId, requireStoryCompletion: true)) parts.Add(Loc.T("story.completed"));
                if (!beat.IsTutorial())
                {
                    var menu = _menu(__instance);
                    Difficulty d = menu != null ? menu.SelectedDifficulty : Difficulty.Medium;
                    if (beat.IsMinigame()) d = d == Difficulty.Easy || d == Difficulty.Medium ? Difficulty.Medium : Difficulty.Hard;
                    string g = PlayerDataUtil.GetLetterGradeForDifficulty(beat.LevelId, d);
                    if (!string.IsNullOrEmpty(g) && g != "-" && g != "DNF") parts.Add(Loc.F("results.grade", g));
                }
                if (parts.Count > 0) Speech.Say(string.Join(", ", parts));
            }
            catch (Exception ex) { Plugin.LogDebug("Story beat: " + ex.Message); }
        }
    }
}
