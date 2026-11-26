using System;
using HarmonyLib;
using UnityEngine;

namespace RiftAccess.Core
{
    /// <summary>
    /// Developer fast boot (config [Speech] DevFastBoot = true, not in the F10 menu): skips the splash animations
    /// and the intro cinematic, and presses "any button" on the title screen, so test runs reach the main menu
    /// in a few seconds. Off for players.
    /// </summary>
    internal static class DevBoot
    {
        private static AccessTools.FieldRef<Shared.Title.MainMenuManager, bool> _anyInput;
        private static Shared.Title.MainMenuManager _menu;
        private static float _menuSeenAt = -1f;
        private static bool _titleDone;

        public static void TryPatch(Harmony harmony)
        {
            if (!ModConfig.DevFastBoot.Value) return;
            try
            {
                var splash = typeof(Shared.SceneLoading.SplashScreenController);
                harmony.Patch(AccessTools.Method(splash, "Start"), prefix: new HarmonyMethod(typeof(DevBoot), nameof(SplashPrefix)));
                harmony.Patch(AccessTools.Method(splash, "GoToIntroCinematic"), prefix: new HarmonyMethod(typeof(DevBoot), nameof(SkipCinematicPrefix)));
                _anyInput = AccessTools.FieldRefAccess<Shared.Title.MainMenuManager, bool>("_wasAnyInputGivenThisFrame");
                Plugin.LogInfo("Dev fast boot on");
            }
            catch (Exception ex) { Plugin.LogError("Dev boot: " + ex); }
        }

        private static void SplashPrefix(Shared.SceneLoading.SplashScreenController __instance)
        {
            try { AccessTools.Field(__instance.GetType(), "_shouldSkipAnimations").SetValue(__instance, true); } catch { }
        }

        private static bool SkipCinematicPrefix()
        {
            Shared.SceneLoading.SceneLoadingController.Instance.GoToScene("MainMenu");
            return false;
        }

        public static void Update()
        {
            if (_titleDone || _anyInput == null) return;
            if (_menu == null)
            {
                if (Time.frameCount % 15 != 0) return;
                _menu = UnityEngine.Object.FindObjectOfType<Shared.Title.MainMenuManager>();
                if (_menu != null) _menuSeenAt = Time.unscaledTime;
                return;
            }
            float t = Time.unscaledTime - _menuSeenAt;
            GameObject title = null;
            try { title = AccessTools.Field(typeof(Shared.Title.MainMenuManager), "_titleScreenObject").GetValue(_menu) as GameObject; } catch { }
            if (t > 10f || (t > 1f && (title == null || !title.activeSelf))) { _titleDone = true; return; }
            if (t > 1f) _anyInput(_menu) = true;
        }
    }
}
