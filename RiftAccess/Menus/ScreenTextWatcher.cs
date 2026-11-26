using System;
using System.Collections.Generic;
using RiftAccess.Core;
using RiftAccess.Gameplay;
using Shared.MenuOptions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Reads text that appears on screen outside menu items: screen titles, instructions ("press Enter to start
    /// calibration"), popup messages, dialogue, hints. Outside songs, the visible TextMeshPro texts are sampled a few
    /// times a second; a text is read once when it has been stable for a moment (typewriter effects finish first).
    /// Texts inside SelectableOptions are left to the focus reader. Texts that keep changing (timers, counters,
    /// scrolling credits) are muted after a few changes.
    /// </summary>
    internal static class ScreenTextWatcher
    {
        private const float Interval = 0.2f;
        private const float StableFor = 0.35f;

        private sealed class Entry
        {
            public string Text;
            public float Since;
            public string Announced;
            public int Changes;
            public float WindowStart;
            public bool Noisy;
            public bool Seen;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>();
        private static readonly List<int> Stale = new List<int>();
        private static readonly List<KeyValuePair<float, string>> Batch = new List<KeyValuePair<float, string>>();
        private static float _next;

        public static void Update()
        {
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + Interval;
            if (ModSettingsMenu.IsOpen || HelpMenu.IsOpen) return;
            // In a stage the HUD changes all the time: only tutorial prompts are read while playing; the pause menu
            // and the results are read in full.
            bool promptsOnly = (StageWatcher.InSong || StageWatcher.InStage) && (!StageWatcher.Paused || StageWatcher.GameOver) && !StageWatcher.ShowingResults;

            foreach (var e in Entries.Values) e.Seen = false;
            Batch.Clear();
            TMP_Text[] texts;
            try { texts = UnityEngine.Object.FindObjectsOfType<TMP_Text>(); }
            catch { return; }

            foreach (var t in texts)
            {
                if (t == null) continue;
                string s = Visible(t) ? Speech.Clean(t.text) : "";
                int id = t.GetInstanceID();
                if (!Entries.TryGetValue(id, out var e))
                {
                    e = new Entry { Text = s, Since = now, WindowStart = now };
                    Entries[id] = e;
                }
                e.Seen = true;
                if (s != e.Text)
                {
                    // A typewriter only adds to the end; that is one text appearing, not a changing counter.
                    bool growing = e.Text.Length > 0 && s.StartsWith(e.Text, StringComparison.Ordinal);
                    e.Text = s;
                    e.Since = now;
                    if (now - e.WindowStart > 5f) { e.WindowStart = now; e.Changes = 0; }
                    if (!growing && s.Length > 0 && ++e.Changes > 4) e.Noisy = true;
                    continue;
                }
                if (e.Noisy || s.Length < 1 || s == "x" || s == "X" || s == e.Announced || now - e.Since < StableFor) continue;
                e.Announced = s;
                if (InsideOption(t.transform) || IsKeyGlyph(t.transform, s) || t.GetComponentInParent<Shared.NarrationTextManager>() != null || (IsHud(t.transform) && !IsTutorialPrompt(t.transform)) || Speech.RecentlyFocused(s) || Speech.RecentlySaid(s)) continue;
                if (promptsOnly && !IsTutorialPrompt(t.transform)) continue;
                s = MenuReader.Label(t.transform, s);
                if (StageWatcher.Paused && !MenuReader.HasLetters(s)) continue; // the stage's HUD counters behind the pause menu
                if (!MenuReader.HasLetters(s) && s.IndexOf(':') > 0) continue; // ticking clocks (daily challenge countdown)
                // Top of the screen first.
                float y = -ScreenRect(t.rectTransform, t.canvas).yMax;
                Batch.Add(new KeyValuePair<float, string>(y, s));
                if (ModConfig.DebugLogging.Value) Plugin.LogInfo("Screen text " + MenuReader.Path(t.transform) + " => " + s);
            }

            Stale.Clear();
            foreach (var kv in Entries) if (!kv.Value.Seen) Stale.Add(kv.Key);
            foreach (var id in Stale) Entries.Remove(id);

            if (Batch.Count == 0) return;
            Batch.Sort((a, b) => a.Key.CompareTo(b.Key));
            var parts = new List<string>();
            foreach (var kv in Batch) if (!parts.Contains(kv.Value)) parts.Add(kv.Value);
            Speech.SayEvent(string.Join(". ", parts));
        }

        /// <summary>Strict: really drawn on screen now (on screen, not clipped by a mask). Lenient (strict = false): only
        /// active and not transparent, for the focused option itself, which the game keeps scrolled into view.</summary>
        public static bool Visible(TMP_Text t, bool strict = true)
        {
            if (!t.isActiveAndEnabled || t.color.a < 0.05f || t.alpha < 0.05f) return false;
            for (var tr = t.transform; tr != null; tr = tr.parent)
            {
                var cg = tr.GetComponent<CanvasGroup>();
                if (cg != null && cg.alpha < 0.05f) return false;
                if (cg != null && cg.ignoreParentGroups) break;
            }
            var canvas = t.canvas;
            if (canvas == null || !canvas.isActiveAndEnabled) return false;
            var scale = t.rectTransform.lossyScale;
            if (Mathf.Abs(scale.x) < 1e-4f || Mathf.Abs(scale.y) < 1e-4f) return false;
            if (!strict) return true;
            if (!OnScreen(t.rectTransform, canvas)) return false;
            // Clipped away by a mask it sits in?
            foreach (var mask in t.GetComponentsInParent<RectMask2D>())
                if (mask.isActiveAndEnabled && !Overlaps(t.rectTransform, mask.rectTransform, canvas)) return false;
            foreach (var mask in t.GetComponentsInParent<Mask>())
                if (mask.isActiveAndEnabled && mask.transform != t.transform && !Overlaps(t.rectTransform, mask.rectTransform, canvas)) return false;
            return true;
        }

        private static readonly Vector3[] Corners = new Vector3[4];

        private static Rect ScreenRect(RectTransform rt, Canvas canvas)
        {
            rt.GetWorldCorners(Corners);
            var root = canvas.rootCanvas;
            Camera cam = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 a = RectTransformUtility.WorldToScreenPoint(cam, Corners[0]);
            Vector2 b = RectTransformUtility.WorldToScreenPoint(cam, Corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        private static bool OnScreen(RectTransform rt, Canvas canvas)
        {
            var r = ScreenRect(rt, canvas);
            return r.width > 1f && r.height > 1f && r.Overlaps(new Rect(0, 0, Screen.width, Screen.height));
        }

        private static bool Overlaps(RectTransform a, RectTransform b, Canvas canvas)
        {
            var ra = ScreenRect(a, canvas);
            var rb = ScreenRect(b, canvas);
            if (rb.width < 1f || rb.height < 1f) return false;
            var inter = Rect.MinMaxRect(Mathf.Max(ra.xMin, rb.xMin), Mathf.Max(ra.yMin, rb.yMin), Mathf.Min(ra.xMax, rb.xMax), Mathf.Min(ra.yMax, rb.yMax));
            return inter.width > ra.width * 0.3f && inter.height > ra.height * 0.3f;
        }

        /// <summary>Debug: every TextMeshPro text with its path and visibility, to the BepInEx log.</summary>
        public static void Dump()
        {
            foreach (var t in UnityEngine.Object.FindObjectsOfType<TMP_Text>())
            {
                if (t == null || string.IsNullOrEmpty(t.text)) continue;
                var scale = t.rectTransform.lossyScale;
                Plugin.LogInfo("TMP " + (Visible(t) ? "V " : "- ") + (t.isActiveAndEnabled ? "" : "inactive ") + "a=" + t.color.a.ToString("0.00") +
                               " s=" + scale.x.ToString("0.###") + " opt=" + (t.GetComponentInParent<SelectableOption>()?.GetType().Name ?? "") +
                               " " + MenuReader.Path(t.transform) + " => " + Speech.Clean(t.text));
            }
            try
            {
                // The game's whole string table, for writing the mod's own texts (tutorial guidance).
                var table = TicToc.Localization.Localizer.LocalizationTableCopy;
                var sb = new System.Text.StringBuilder();
                foreach (var outer in table)
                    foreach (var inner in outer.Value)
                        sb.Append(outer.Key).Append('\t').Append(inner.Key).Append('\t').Append((inner.Value ?? "").Replace("\n", "\\n")).Append('\n');
                System.IO.File.WriteAllText(System.IO.Path.Combine(Plugin.PluginDir, "loc_dump.tsv"), sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { Plugin.LogWarning("Loc dump: " + ex.Message); }
            Speech.Say("Dumped screen texts to the log", true);
        }

        private static readonly System.Text.RegularExpressions.Regex KeyPath =
            new System.Text.RegularExpressions.Regex(@"Key|NavBar|HotKey|ButtonText|/Enter/|/Back/|Toggle", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>The key name shown on a hint ("ESC", "TAB", "INTRO"): the hint's action label says what matters.</summary>
        private static bool IsKeyGlyph(Transform t, string s)
        {
            if (s.Length > 7 || s != s.ToUpperInvariant()) return false;
            return KeyPath.IsMatch(MenuReader.Path(t));
        }

        /// <summary>The stage HUD (health, score, combo, multiplier): read on demand with H, never spoken as it changes.</summary>
        private static bool IsHud(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.IndexOf("BattleUI", StringComparison.Ordinal) >= 0 || t.name.IndexOf("StageUI", StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static bool IsTutorialPrompt(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.name.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("Prompt", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        private static bool InsideOption(Transform t)
        {
            return t.GetComponentInParent<SelectableOption>() != null;
        }
    }
}
