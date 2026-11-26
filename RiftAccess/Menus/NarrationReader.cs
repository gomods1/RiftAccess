using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RiftAccess.Core;
using Shared;
using Shared.Dialogue;
using TMPro;
using UnityEngine;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Story, tutorial and coach dialogue (NarrationTextManager). Each line is read in full when its box opens
    /// (OpenTextBox), without waiting for the typewriter. The game continues on Enter or any arrow key.
    /// Tutorial lines that only make sense on screen ("when a monster reaches the Action Row...") are replaced by
    /// the mod's own explanation of how to play by ear (Lang: tut.&lt;game key&gt;), when the player's language has one.
    /// </summary>
    internal static class NarrationReader
    {
        private static AccessTools.FieldRef<NarrationTextManager, MonoBehaviour> _player;
        private static AccessTools.FieldRef<NarrationTextManager, Queue<NarrationData>> _queue;
        private static MethodInfo _skip;
        private static NarrationTextManager _pending;
        private static string _pendingKey;
        private static int _pendingFrame;
        private static bool _hintGiven;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var field = AccessTools.Field(typeof(NarrationTextManager), "_currentTextAnimatorPlayer");
                _player = AccessTools.FieldRefAccess<NarrationTextManager, MonoBehaviour>(field);
                _queue = AccessTools.FieldRefAccess<NarrationTextManager, Queue<NarrationData>>("_queuedNarrationData");
                _skip = AccessTools.Method(field.FieldType, "SkipTypewriter", Type.EmptyTypes);
                var open = AccessTools.Method(typeof(NarrationTextManager), "OpenTextBox");
                harmony.Patch(open, prefix: new HarmonyMethod(typeof(NarrationReader), nameof(OpenPrefix)),
                    postfix: new HarmonyMethod(typeof(NarrationReader), nameof(OpenPostfix)));
                Plugin.LogInfo("Patched NarrationTextManager.OpenTextBox (dialogue)");
            }
            catch (Exception ex) { Plugin.LogError("Narration reader: " + ex); }
        }

        /// <summary>The line about to be shown is still at the head of the queue: remember its key.</summary>
        private static void OpenPrefix(NarrationTextManager __instance)
        {
            try
            {
                var q = _queue(__instance);
                _pendingKey = q != null && q.Count > 0 ? q.Peek().NarrationLocKey : null;
            }
            catch { _pendingKey = null; }
        }

        private static void OpenPostfix(NarrationTextManager __instance)
        {
            _pending = __instance;
            _pendingFrame = Time.frameCount;
        }

        public static void Update()
        {
            if (_pending == null || Time.frameCount < _pendingFrame + 2) return;
            var manager = _pending;
            string key = _pendingKey;
            _pending = null;
            try
            {
                var player = _player(manager);
                TMP_Text text = player != null ? (player.GetComponent<TMP_Text>() ?? player.GetComponentInChildren<TMP_Text>()) : null;
                string line = text != null ? Speech.Clean(text.text) : "";

                string guide = !string.IsNullOrEmpty(key) && ModConfig.TutorialGuide.Value ? Loc.TryOwn("tut." + key) : null;
                if (guide != null)
                {
                    line = string.Format(guide, ModConfig.LeadText(ModConfig.LeadBeats.Value));
                    Plugin.LogDebug("Tutorial line " + key + " replaced");
                }
                else if (!string.IsNullOrEmpty(key)) Plugin.LogDebug("Narration line " + key);

                if (ModConfig.InstantDialogue.Value && player != null && _skip != null)
                {
                    try { _skip.Invoke(player, null); } catch { }
                }
                if (line.Length == 0) return;
                string who = guide == null ? SceneDescriber.TakeSongPrefix() : null;
                if (who != null) line = who + ": " + line;
                if (!_hintGiven)
                {
                    _hintGiven = true;
                    line += ". " + Loc.T("narration.continue");
                }
                Speech.SayEvent(line);
            }
            catch (Exception ex) { Plugin.LogError("Narration: " + ex.Message); }
        }
    }
}
