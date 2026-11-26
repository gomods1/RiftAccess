using System;
using HarmonyLib;
using RiftAccess.Core;
using RiftAccess.Menus;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Boss battle cutscenes, which are animations without words: the boss's entrance, its phase transitions and its
    /// defeat (Lang bbscene.&lt;boss&gt;.intro / .phase / .defeat), and Cadence's defeat. Full-screen cinematics played by
    /// the beatmap are logged and described when the mod has a line for them (bbscene.cine.&lt;name&gt;).
    /// </summary>
    internal static class BossScenes
    {
        private static BossBattles.BossBattleStageController _introFor;
        private static string _introBoss;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var t = typeof(BossBattles.BossBattleStageController);
                var me = typeof(BossScenes);
                harmony.Patch(AccessTools.Method(t, "HandleBossPhaseChange"), postfix: new HarmonyMethod(me, nameof(PhasePostfix)));
                harmony.Patch(AccessTools.Method(t, "HandleBossDefeatedEvent"), postfix: new HarmonyMethod(me, nameof(DefeatPostfix)));
                harmony.Patch(AccessTools.Method(t, "HandlePlayCinematicEvent"), postfix: new HarmonyMethod(me, nameof(CinematicPostfix)));
            }
            catch (Exception ex) { Plugin.LogError("Boss scenes: " + ex); }
        }

        private static void Say(string kind)
        {
            if (!ModConfig.DescribeScenes.Value) return;
            string id = PictureDescriber.BossId();
            if (id == null) return;
            string key = "bbscene." + id + "." + kind;
            Plugin.LogDebug("boss scene " + key);
            if (Loc.Has(key)) Speech.SayEvent(Loc.T(key));
        }

        /// <summary>Twice a second: the boss's entrance, once per battle and per boss form.</summary>
        private static float _next;

        public static void Update()
        {
            if (UnityEngine.Time.unscaledTime < _next) return;
            _next = UnityEngine.Time.unscaledTime + 0.5f;
            var stage = UnityEngine.Object.FindObjectOfType<BossBattles.BossBattleStageController>();
            if (stage == null) return;
            string id = PictureDescriber.BossId();
            if (id == null || (stage == _introFor && id == _introBoss)) return;
            // Once per battle and again when the boss changes form (Harmonie becomes the Banshee).
            _introFor = stage;
            _introBoss = id;
            Say("intro");
        }

        private static void PhasePostfix(object phaseTransitionBeatmap) { if (phaseTransitionBeatmap != null) Say("phase"); }
        private static void DefeatPostfix() => Say("defeat");

        private static void CinematicPostfix(string cinematicName)
        {
            Plugin.LogDebug("boss cinematic " + cinematicName);
            string key = "bbscene.cine." + (cinematicName ?? "").ToLowerInvariant();
            if (ModConfig.DescribeScenes.Value && Loc.Has(key)) Speech.SayEvent(Loc.T(key));
        }

        /// <summary>Cadence's defeat animation when the player's health runs out in a boss battle.</summary>
        public static void PlayerDefeated()
        {
            if (ModConfig.DescribeScenes.Value) Speech.SayEvent(Loc.T("bbscene.cadence.defeat"));
        }
    }
}
