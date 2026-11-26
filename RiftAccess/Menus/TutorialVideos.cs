using System.Collections.Generic;
using HarmonyLib;
using RiftAccess.Core;
using UnityEngine;
using UnityEngine.Video;

namespace RiftAccess.Menus
{
    /// <summary>
    /// The silent demonstration videos shown in tutorial and narration boxes. Described in the review buffer (Alt+Up)
    /// while one plays: a template (Lang vid.&lt;template&gt;) filled with the game's own monster names (bestiary keys),
    /// or a line of its own (vid.&lt;clip&gt;).
    /// </summary>
    internal static class TutorialVideos
    {
        // clip (without "tutorial_" and the size suffix) -> template, bestiary ids
        private static readonly Dictionary<string, string[]> Map = new Dictionary<string, string[]>
        {
            { "actionrow_greenslime", new[] { "monster", "GreenSlime" } },
            { "armadilloblue", new[] { "multihit", "BaseArmadillo" } },
            { "armadillored", new[] { "multihit", "RedArmadillo" } },
            { "armadilloyellow", new[] { "multihit", "YellowArmadillo" } },
            { "armadillosheildskeleton", new[] { "monster2", "YellowArmadillo", "ShieldedBaseSkeleton" } },
            { "batred", new[] { "moving", "RedBat" } },
            { "batyellow", new[] { "moving", "YellowBat" } },
            { "bluebat", new[] { "moving", "BlueBat" } },
            { "redyellowbat", new[] { "moving2", "RedBat", "YellowBat" } },
            { "blackskeletons", new[] { "skeleton", "BlackSkeleton" } },
            { "whiteskeletons", new[] { "skeleton", "BaseSkeleton" } },
            { "yellowskeletons", new[] { "skeleton", "YellowSkeleton" } },
            { "blademaster", new[] { "blademaster", "BaseBlademaster" } },
            { "blademasterstrong", new[] { "blademaster", "BlueBlademaster" } },
            { "blademasterslimes", new[] { "monster2", "BaseBlademaster", "GreenSlime" } },
            { "bluebatsslimes", new[] { "monster2", "BlueBat", "GreenSlime" } },
            { "doubleshieldskeleton", new[] { "shield", "ShieldedDoubleSkeleton" } },
            { "sheildedskeletonblack", new[] { "shield", "ShieldedBlackSkeleton" } },
            { "sheildedskeletonyellow", new[] { "shield", "ShieldedYellowSkeleton" } },
            { "shieldskeleton", new[] { "shield", "ShieldedBaseSkeleton" } },
            { "yellowblacksheildedskeleton", new[] { "monster2", "ShieldedYellowSkeleton", "ShieldedBlackSkeleton" } },
            { "food_apple", new[] { "food", "Apple" } },
            { "food_cheese", new[] { "food", "Cheese" } },
            { "food_drumstick", new[] { "food", "Drumstick" } },
            { "food_ham", new[] { "food", "Ham" } },
            { "harpie", new[] { "harpy", "BaseHarpy" } },
            { "harpiequick", new[] { "harpy", "RedHarpy" } },
            { "harpiestrong", new[] { "harpy", "BlueHarpy" } },
            { "redzombie", new[] { "moving", "YellowZombie" } },
            { "zombie", new[] { "moving", "GreenZombie" } },
            { "rr_othermonsters", new[] { "multihit", "BlueSlime" } },
            { "slimeblue", new[] { "multihit", "BlueSlime" } },
            { "slimeyellow", new[] { "multihit", "YellowSlime" } },
            { "slimes", new[] { "monster3", "GreenSlime", "BlueSlime", "YellowSlime" } },
            { "skullblue", new[] { "skull", "BlueSkull" } },
            { "skullred", new[] { "skull", "RedSkull" } },
            { "whiteskull", new[] { "skull", "BaseSkull" } },
            { "skullsstrongtricky", new[] { "skull2", "BlueSkull", "RedSkull" } },
            { "wrym", new[] { "wyrm", "BaseWyrm" } },
            { "blueslimes", new[] { "own", "actionrow" } },
            { "food", new[] { "own", "food_all" } },
        };

        private static AccessTools.FieldRef<Shared.NarrationTextManager, VideoPlayer> _player;

        /// <summary>The description of the tutorial video now playing, or null.</summary>
        public static string Current()
        {
            try
            {
                if (_player == null) _player = AccessTools.FieldRefAccess<Shared.NarrationTextManager, VideoPlayer>("_videoNarrationVideoPlayer");
                foreach (var m in Object.FindObjectsOfType<Shared.NarrationTextManager>())
                {
                    var vp = _player(m);
                    if (!m.IsShowing || vp == null || vp.clip == null || !vp.isActiveAndEnabled) continue;
                    return Describe(vp.clip.name);
                }
            }
            catch { }
            return null;
        }

        public static string Describe(string clip)
        {
            string n = clip.ToLowerInvariant().Replace("tutorial_", "").Replace("_720_noaudio", "").Replace("_480_noaudio", "");
            string line = null;
            if (Map.TryGetValue(n, out var t))
            {
                if (t[0] == "own") line = Loc.T("vid." + t[1]);
                else
                {
                    var names = new object[t.Length - 1];
                    for (int i = 1; i < t.Length; i++) names[i - 1] = Gameplay.RuleWatcher.GameName("NavItemDisplayName_Bestiary_" + t[i]) ?? t[i];
                    line = Loc.F("vid." + t[0], names);
                }
            }
            else if (Loc.Has("vid." + n)) line = Loc.T("vid." + n);
            if (line == null) { Plugin.LogDebug("Undescribed tutorial video " + clip); return null; }
            return Loc.F("vid.prefix", line);
        }
    }
}
