using System;
using HarmonyLib;
using RiftAccess.Core;
using UnityEngine;
using UnityEngine.Video;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Audio description of the two story cinematics (intro and outro videos, which have music but no words).
    /// Each line (Lang cine.intro.N / cine.outro.N) is spoken when the video reaches its time.
    /// </summary>
    internal static class CinematicDescriber
    {
        // Start times in seconds, one per line, in order.
        private static readonly float[] Intro = { 0.5f, 4, 10, 12, 14, 16, 18, 20, 24, 32, 34, 36, 38, 40, 42.5f };
        private static readonly float[] Outro =
        {
            0.5f, 4, 9, 16, 20, 24, 28, 32, 36, 48, 52, 56, 60, 64, 80, 84, 88, 92,
            97, 101, 113, 121, 133, 141, 149, 161, 169, 177, 185, 197, 205, 217, 225, 233, 241, 253, 276, 280,
        };

        private static AccessTools.FieldRef<Shared.CinematicSceneController, VideoPlayer> _player;
        private static Shared.CinematicSceneController _scene;
        private static float _nextSearch;
        private static string _which;
        private static int _next;

        public static void TryPatch(Harmony harmony)
        {
            try { _player = AccessTools.FieldRefAccess<Shared.CinematicSceneController, VideoPlayer>("_videoPlayer"); }
            catch (Exception ex) { Plugin.LogError("Cinematic describer: " + ex); }
        }

        public static void Update()
        {
            if (_player == null) return;
            if (_scene == null)
            {
                _which = null;
                if (Time.unscaledTime < _nextSearch) return;
                _nextSearch = Time.unscaledTime + 0.5f;
                _scene = UnityEngine.Object.FindObjectOfType<Shared.CinematicSceneController>();
                _next = 0;
                return;
            }
            try
            {
                var vp = _player(_scene);
                if (vp == null || vp.clip == null || !vp.isPlaying) return;
                if (_which == null)
                {
                    string n = vp.clip.name;
                    _which = n.IndexOf("Intro", StringComparison.OrdinalIgnoreCase) >= 0 ? "intro"
                           : n.IndexOf("Outro", StringComparison.OrdinalIgnoreCase) >= 0 ? "outro" : "";
                    Plugin.LogDebug("Cinematic " + n);
                }
                if (_which.Length == 0 || !ModConfig.DescribeScenes.Value) return;
                var times = _which == "intro" ? Intro : Outro;
                double t = vp.time;
                // Skip lines already passed (seeking), say the latest one due.
                int due = -1;
                while (_next < times.Length && t >= times[_next]) due = _next++;
                if (due >= 0) Speech.SayEvent(Loc.T("cine." + _which + "." + (due + 1)));
            }
            catch (Exception ex) { Plugin.LogDebug("Cinematic: " + ex.Message); }
        }
    }
}
