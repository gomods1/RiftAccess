using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RiftAccess.Audio;
using RiftAccess.Core;
using RiftAccess.Gameplay;
using RiftAccess.Menus;
using UnityEngine;

namespace RiftAccess
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "gomods.riftaccess";
        public const string Name = "Rift Access";
        public const string Version = "0.2.0";

        internal static Plugin Instance { get; private set; }
        internal static string PluginDir { get; private set; }
        private static ManualLogSource _log;
        private static Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            _log = Logger;
            PluginDir = Path.GetDirectoryName(Info.Location);
            LogInfo(Name + " " + Version + " starting in " + PluginDir);

            ModConfig.Bind(Config);
            Speech.Initialize(PluginDir);

            _harmony = new Harmony(Guid);
            InputBlocker.TryPatch(_harmony);
            MenuReader.TryPatch(_harmony);
            TrackReader.TryPatch(_harmony);
            NarrationReader.TryPatch(_harmony);
            UnlockPopupReader.TryPatch(_harmony);
            LaneCues.TryPatch(_harmony);
            StageWatcher.TryPatch(_harmony);
            DirectionCues.TryPatch(_harmony);

            // BepInEx's own manager object can be destroyed by scene loads in some games; keep ours separate.
            var host = new GameObject("RiftAccessHost");
            host.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(host);
            host.AddComponent<AccessController>();

        }

        internal static void LogInfo(string msg) => _log?.LogInfo(msg);
        internal static void LogWarning(string msg) => _log?.LogWarning(msg);
        internal static void LogError(string msg) => _log?.LogError(msg);
        internal static void LogDebug(string msg)
        {
            if (ModConfig.DebugLogging != null && ModConfig.DebugLogging.Value) _log?.LogInfo(msg);
        }
    }

    /// <summary>Runs every frame on the main thread: input, speech queue, cue engine, watchers.</summary>
    internal class AccessController : MonoBehaviour
    {
        private bool _cuesReady;
        private bool _announcedLoaded;
        private static readonly System.Reflection.FieldInfo LocReady =
            HarmonyLib.AccessTools.Field(typeof(TicToc.Localization.Localizer), "_isInitialized");

        private void Update()
        {
            try
            {
                if (!_cuesReady) _cuesReady = CuePlayer.TryInitialize();
                KeyInput.Update();
                Loc.Update();
                // Say "loaded" once the game's language is known, so it is in the player's language.
                if (!_announcedLoaded && (Time.unscaledTime > 20f || (LocReady != null && (bool)LocReady.GetValue(null))))
                {
                    _announcedLoaded = true;
                    Loc.Refresh();
                    Speech.Say(Loc.T("loaded"));
                }
                Speech.Update();
                InputBlocker.Update();
                if (ModSettingsMenu.IsOpen) ModSettingsMenu.Update();
                else if (HelpMenu.IsOpen) HelpMenu.Update();
                else Hotkeys.Update();
                MenuReader.Update();
                TrackReader.Update();
                NarrationReader.Update();
                UnlockPopupReader.Update();
                ScreenTextWatcher.Update();
                StageWatcher.Update();
                LaneCues.Update();
                CueBot.Update();
                DirectionCues.Update();
                CuePlayer.Update();
            }
            catch (Exception ex)
            {
                Plugin.LogError("Update failed: " + ex);
            }
        }

        private void OnApplicationQuit()
        {
            StageWatcher.RestoreMusic();
            CuePlayer.Shutdown();
            Speech.Shutdown();
        }
    }
}
