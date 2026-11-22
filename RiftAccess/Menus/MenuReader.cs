using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RiftAccess.Core;
using Shared.MenuOptions;
using Shared.Title;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Speaks the game's own menu focus. The game's menus are built from SelectableOption components driven by
    /// its controller/keyboard navigation (tier A for us): SetSelected(true) is the focus event. Almost every
    /// override calls the base method, so one postfix on the base catches them; BestiarySelectionOption does not
    /// and is patched separately. Text is read a frame later, after the subclass has updated its labels, from the
    /// deepest selected option (a selected group also has a selected child: the child is what is focused).
    /// </summary>
    internal static class MenuReader
    {
        private static readonly List<SelectableOption> Candidates = new List<SelectableOption>();
        private static readonly List<MenuPopupScreen> Popups = new List<MenuPopupScreen>();
        private static int _queuedFrame = -1;
        private static int _popupFrame = -1;
        private static SelectableOption _focused;
        private static List<string> _focusedParts = new List<string>();
        private static float _nextPoll;
        private static FieldInfo _groupOptions;
        private static float _lostAt;
        private static SelectableOption _retryOption;
        private static float _retryAt;
        private static int _retries;
        private static FieldInfo _switchName;

        public static SelectableOption Focused => _focused;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                var post = new HarmonyMethod(typeof(MenuReader), nameof(SetSelectedPostfix));
                harmony.Patch(AccessTools.Method(typeof(SelectableOption), nameof(SelectableOption.SetSelected)), postfix: post);
                var bestiary = AccessTools.TypeByName("Shared.TrackSelection.BestiarySelectionOption");
                var bm = bestiary != null ? AccessTools.DeclaredMethod(bestiary, "SetSelected") : null;
                if (bm != null) harmony.Patch(bm, postfix: post);
                harmony.Patch(AccessTools.Method(typeof(MenuPopupScreen), nameof(MenuPopupScreen.Show)),
                    postfix: new HarmonyMethod(typeof(MenuReader), nameof(PopupShowPostfix)));
                _groupOptions = AccessTools.Field(typeof(SelectableOptionGroup), "_options");
                _switchName = AccessTools.Field(typeof(SwitchOption), "_optionNameText");
                Plugin.LogInfo("Patched SelectableOption.SetSelected and MenuPopupScreen.Show (menu reading)");
            }
            catch (Exception ex) { Plugin.LogError("Menu reader: " + ex); }
        }

        /// <summary>Options told SetSelected(true) and not yet false. Some (BestiarySelectionOption) never set the
        /// base IsSelected flag, so this is the mod's own record of what the game selected.</summary>
        private static readonly HashSet<SelectableOption> Selected = new HashSet<SelectableOption>();

        private static bool IsSel(SelectableOption o) => o != null && (o.IsSelected || Selected.Contains(o));

        private static void SetSelectedPostfix(SelectableOption __instance, bool __0)
        {
            if (__instance == null) return;
            if (!__0) { Selected.Remove(__instance); return; }
            if (Selected.Count > 256) Selected.RemoveWhere(o => o == null);
            Selected.Add(__instance);
            if (!Candidates.Contains(__instance)) Candidates.Add(__instance);
            _queuedFrame = Time.frameCount;
        }

        private static void PopupShowPostfix(MenuPopupScreen __instance)
        {
            if (__instance == null) return;
            Popups.Add(__instance);
            _popupFrame = Time.frameCount;
        }

        public static void Update()
        {
            Popups.Clear(); // popup messages are read by ScreenTextWatcher

            if (Candidates.Count > 0 && Time.frameCount > _queuedFrame)
            {
                SelectableOption pick = null;
                foreach (var c in Candidates)
                {
                    if (c == null || !IsSel(c) || !c.isActiveAndEnabled) continue;
                    if (pick == null || c.transform.IsChildOf(pick.transform)) pick = c;
                }
                Candidates.Clear();
                if (pick != null && !ModSettingsMenu.IsOpen && !HelpMenu.IsOpen)
                {
                    var deeper = DeepestSelectedChild(pick);
                    if (deeper != null) pick = deeper;
                    pick = ResolveGroup(pick);
                    _retries = 0;
                    if (!TrackReader.IsTrackList(pick)) Announce(pick);
                }
            }

            if (_retryOption != null && Time.unscaledTime >= _retryAt)
            {
                var r = _retryOption;
                _retryOption = null;
                if (r != null && IsSel(r) && r.isActiveAndEnabled) Announce(r);
                else _retries = 0;
            }

            // Focus recovery: the focused option went away (a popup closed) and nothing new was focused: find what the
            // game has selected now and announce it once.
            if (_focused == null && _lostAt > 0f && Time.unscaledTime - _lostAt > 0.4f && !ModSettingsMenu.IsOpen && !HelpMenu.IsOpen)
            {
                _lostAt = 0f;
                var current = FindSelected();
                if (current != null) Announce(current);
            }

            // Values that change without a focus change (sliders, carousels, toggles): speak what changed.
            if (_focused != null && Time.unscaledTime >= _nextPoll && !ModSettingsMenu.IsOpen && !HelpMenu.IsOpen)
            {
                _nextPoll = Time.unscaledTime + 0.15f;
                if (!IsSel(_focused) || !_focused.isActiveAndEnabled)
                {
                    _focused = null;
                    _lostAt = Time.unscaledTime;
                    return;
                }
                var parts = Describe(_focused);
                if (!SameParts(parts, _focusedParts))
                {
                    var changed = new List<string>();
                    foreach (var p in parts) if (!_focusedParts.Contains(p)) changed.Add(p);
                    _focusedParts = parts;
                    if (changed.Count > 0) Speech.SayFocus(string.Join(", ", changed));
                }
            }
        }

        private static readonly Dictionary<Type, MethodInfo> SelectedOptionGetters = new Dictionary<Type, MethodInfo>();

        /// <summary>A focused option group stands for its selected option (OptionGroup.GetSelectedOption), recursively.</summary>
        public static SelectableOption ResolveGroup(SelectableOption option)
        {
            for (int depth = 0; depth < 6 && option != null; depth++)
            {
                var type = option.GetType();
                if (!SelectedOptionGetters.TryGetValue(type, out var getter))
                {
                    getter = type.GetMethod("GetSelectedOption", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    SelectedOptionGetters[type] = getter;
                }
                if (getter == null) break;
                SelectableOption inner = null;
                try { inner = getter.Invoke(option, null) as SelectableOption; } catch { }
                if (inner == null || inner == option || !inner.isActiveAndEnabled) break;
                option = inner;
            }
            return option;
        }

        private static SelectableOption FindSelected()
        {
            SelectableOption pick = null;
            foreach (var o in UnityEngine.Object.FindObjectsOfType<SelectableOption>())
            {
                if (!IsSel(o) || !o.isActiveAndEnabled || TrackReader.IsTrackList(o)) continue;
                if (Describe(o).Count == 0) continue;
                if (pick == null || o.transform.IsChildOf(pick.transform)) pick = o;
            }
            return pick != null ? ResolveGroup(pick) : null;
        }

        private static SelectableOption DeepestSelectedChild(SelectableOption root)
        {
            SelectableOption best = null;
            foreach (var o in root.GetComponentsInChildren<SelectableOption>(false))
            {
                if (o == root || !IsSel(o)) continue;
                if (best == null || o.transform.IsChildOf(best.transform)) best = o;
            }
            return best;
        }

        private static void Announce(SelectableOption option)
        {
            var parts = Describe(option);
            _focused = option;
            _lostAt = 0f;
            _focusedParts = parts;
            string text = string.Join(", ", parts);
            if (string.IsNullOrEmpty(text))
            {
                // Nothing visible yet: often a control still fading in. Try again shortly; if it stays empty it is
                // icon-only or hidden, and its name goes to the log for writing a label for it.
                if (_retries < 4)
                {
                    _retries++;
                    _retryOption = option;
                    _retryAt = Time.unscaledTime + 0.25f;
                }
                else Plugin.LogDebug("Focus without visible text: " + Path(option.transform));
                return;
            }
            _retries = 0;
            _retryOption = null;
            string pos = ModConfig.ReadPosition.Value ? Position(option) : null;
            if (pos != null) text += ", " + pos;
            Speech.SayFocus(text);
            if (ModConfig.DebugLogging.Value) Plugin.LogInfo("Focus " + option.GetType().Name + " " + Path(option.transform) + " => " + text);
        }

        /// <summary>Set by readers that announce focus themselves, so value polling and F2 follow them.</summary>
        public static void SetFocus(SelectableOption option)
        {
            if (option == null) return;
            _focused = option;
            _focusedParts = Describe(option);
            _lostAt = 0f;
        }

        public static void RepeatFocus()
        {
            if (_focused != null && _focused.isActiveAndEnabled)
            {
                var parts = Describe(_focused);
                Speech.Say(parts.Count > 0 ? string.Join(", ", parts) : CleanName(_focused.gameObject.name), true);
            }
            else Speech.Say(string.IsNullOrEmpty(Speech.LastSpoken) ? Loc.T("nothing_focused") : Speech.LastSpoken, true);
        }

        /// <summary>The visible text of an option in hierarchy order, plus state that is only shown graphically.</summary>
        private static AccessTools.FieldRef<RangeSliderOptionController, TMP_Text> _rangeLabel;
        private static AccessTools.FieldRef<RangeSliderOptionController, int> _rangeMin, _rangeMax;

        /// <summary>A range slider's handle sits deep inside it; its label and values belong to the controller.</summary>
        private static List<string> DescribeRangeHandle(RangeSliderControlOption handle)
        {
            var c = handle.GetComponentInParent<RangeSliderOptionController>();
            if (c == null) return null;
            if (_rangeLabel == null)
            {
                _rangeLabel = AccessTools.FieldRefAccess<RangeSliderOptionController, TMP_Text>("_textLabel");
                _rangeMin = AccessTools.FieldRefAccess<RangeSliderOptionController, int>("_sliderValueMin");
                _rangeMax = AccessTools.FieldRefAccess<RangeSliderOptionController, int>("_sliderValueMax");
            }
            var parts = new List<string>();
            var label = _rangeLabel(c);
            if (label != null) parts.Add(Speech.Clean(label.text).TrimEnd(':', ' '));
            bool isMin = c.MinControlOption == handle;
            parts.Add(Loc.F(isMin ? "range.start" : "range.end", isMin ? _rangeMin(c) : _rangeMax(c)));
            return parts;
        }

        public static List<string> Describe(Component root)
        {
            if (root is RangeSliderControlOption handle)
            {
                var range = DescribeRangeHandle(handle);
                if (range != null) return range;
            }
            var parts = new List<string>();
            CollectTexts(root.transform, parts, null, strict: false);
            for (int i = 0; i < parts.Count; i++) parts[i] = parts[i].TrimEnd(':', ' ');
            // A carousel's value ("Completa") is labelled by the carousel ("Animación de fondo").
            if (root is CarouselSubOption)
            {
                var carousel = root.GetComponentInParent<CarouselOptionGroup>();
                if (carousel != null)
                {
                    var labels = new List<string>();
                    CollectTexts(carousel.transform, labels, tr => tr != carousel.transform && tr.GetComponent<SelectableOption>() != null, strict: false);
                    for (int i = labels.Count - 1; i >= 0; i--) if (!parts.Contains(labels[i])) parts.Insert(0, labels[i].TrimEnd(':', ' '));
                }
            }
            // A value with no words ("100%") gets its label from beside it: the nearest ancestor's text that is not
            // part of another option.
            if (root is SelectableOption && !parts.Exists(HasLetters))
            {
                var label = FindLabel(root.transform);
                if (label != null) parts.Insert(0, label);
            }
            if (root is SwitchOption sw)
            {
                parts.Add(Loc.T(sw.IsOn ? "on" : "off"));
            }
            else
            {
                var toggle = root.GetComponentInChildren<Toggle>(false);
                if (toggle != null) parts.Add(Loc.T(toggle.isOn ? "checked" : "not_checked"));
            }
            return parts;
        }

        /// <summary>Collects visible text under t. Subtrees for which skip returns true are left out.</summary>
        public static void CollectTexts(Transform t, List<string> parts, Func<Transform, bool> skip, bool strict = true)
        {
            if (!t.gameObject.activeInHierarchy) return;
            if (skip != null && skip(t)) return;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.05f) return;
            string s = null;
            var tmp = t.GetComponent<TMP_Text>();
            if (tmp != null && ScreenTextWatcher.Visible(tmp, strict)) s = tmp.text;
            else
            {
                var txt = t.GetComponent<Text>();
                if (txt != null && txt.enabled && txt.color.a > 0.05f) s = txt.text;
            }
            if (s != null)
            {
                s = Label(t, Speech.Clean(s));
                if (s.Length > 0 && (HasLetters(s) || s.Any(char.IsDigit)) && !IsPlaceholder(s) && (parts.Count == 0 || parts[parts.Count - 1] != s) && !parts.Contains(s)) parts.Add(s);
            }
            for (int i = 0; i < t.childCount; i++) CollectTexts(t.GetChild(i), parts, skip, strict);
        }

        /// <summary>Numbers and words whose meaning on screen comes from an icon next to them.</summary>
        public static string Label(Transform t, string s)
        {
            if (s.Length > 0 && t.name == "UnlockRequirementLabel" && s.Any(char.IsDigit)) return Loc.F("unlock_cost", s);
            return s;
        }

        public static bool HasLetters(string s)
        {
            foreach (char c in s) if (char.IsLetter(c)) return true;
            return false;
        }

        private static string FindLabel(Transform t)
        {
            var self = t;
            for (var a = t.parent; a != null && self != null; self = a, a = a.parent)
            {
                var found = new List<string>();
                CollectTexts(a, found, tr => tr == self || (tr != a && tr.GetComponent<SelectableOption>() != null));
                foreach (var f in found) if (HasLetters(f)) return f.TrimEnd(':', ' ');
                if (a.GetComponent<SelectableOptionGroup>() != null) break;
            }
            return null;
        }

        private static bool IsPlaceholder(string s)
        {
            return s == "New Text" || s.StartsWith("Lorem", StringComparison.OrdinalIgnoreCase) || s == "..." || s == "x" || s == "X";
        }

        private static void AnnouncePopup(MenuPopupScreen popup)
        {
            if (popup == null || !popup.isActiveAndEnabled) return;
            var parts = new List<string>();
            CollectTexts(popup.transform, parts, tr => tr.GetComponent<SelectableOption>() != null);
            if (parts.Count > 0) Speech.SayEvent(string.Join(". ", parts));
        }

        private static string Position(SelectableOption option)
        {
            try
            {
                var group = option.transform.parent != null ? option.transform.parent.GetComponentInParent<SelectableOptionGroup>() : null;
                if (group == null || _groupOptions == null) return null;
                if (!(_groupOptions.GetValue(group) is IList list)) return null;
                int index = 0, count = 0;
                foreach (var o in list)
                {
                    if (!(o is SelectableOption so) || so == null || !so.gameObject.activeSelf) continue;
                    count++;
                    if (so == option || option.transform.IsChildOf(so.transform)) index = count;
                }
                return index > 0 ? Loc.F("position", index, count) : null;
            }
            catch { return null; }
        }

        private static bool SameParts(List<string> a, List<string> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static string CleanName(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name.Replace("(Clone)", "").Replace("_", " "))
            {
                if (char.IsUpper(c) && sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
