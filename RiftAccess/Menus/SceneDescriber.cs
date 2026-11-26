using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RiftAccess.Core;
using Shared.Dialogue;
using UnityEngine;

namespace RiftAccess.Menus
{
    /// <summary>
    /// What story dialogue scenes show without words: who speaks and with what expression (the pose written in the
    /// line, "NAME: (smile, *jump*) text"), who enters or leaves, the location (background art, described by the mod:
    /// Lang bg.*), the screen darkening and the Rift's effects. In-song dialogue gets the speaker and the bubble's
    /// tone (shout, thought). Lines are spoken here in full, so the screen reader below skips the same text.
    /// </summary>
    internal static class SceneDescriber
    {
        private static AccessTools.FieldRef<DialogueManager, DialogueCharacterSlot[]> _slots;
        private static AccessTools.FieldRef<DialogueManager, DialogueCharacterSlot> _speaking;
        private static System.Reflection.FieldInfo _slotAlias;

        private static readonly Dictionary<string, string> Moods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                BuildMoods();
                var t = typeof(DialogueManager);
                var me = typeof(SceneDescriber);
                _slots = AccessTools.FieldRefAccess<DialogueManager, DialogueCharacterSlot[]>("_dialogueCharacterSlots");
                _speaking = AccessTools.FieldRefAccess<DialogueManager, DialogueCharacterSlot>("_characterSpeakingSlot");
                _slotAlias = AccessTools.Field(typeof(DialogueCharacterSlot), "_alias");
                harmony.Patch(AccessTools.Method(t, "SetDialogueLine"), prefix: new HarmonyMethod(me, nameof(LinePrefix)), postfix: new HarmonyMethod(me, nameof(LinePostfix)));
                foreach (var cmd in new[] { "SetCharacter", "SetBackground", "DarkenScreen", "RiftSeize", "RiftRelease", "SetRiftBackground", "SetRiftOverlay", "PlayVfx", "SetAnimation" })
                    harmony.Patch(AccessTools.Method(t, cmd), prefix: new HarmonyMethod(me, nameof(CommandPrefix)));
                harmony.Patch(AccessTools.Method(typeof(RhythmRift.RRDialogueTextView), nameof(RhythmRift.RRDialogueTextView.SetDialogueLineData)),
                    postfix: new HarmonyMethod(me, nameof(SongLinePostfix)));
                Plugin.LogInfo("Patched dialogue scenes (scene descriptions)");
            }
            catch (Exception ex) { Plugin.LogError("Scene describer: " + ex); }
        }

        // ---- Story lines ----
        private static string _mood, _anim, _body;

        private static void LinePrefix(string line)
        {
            _mood = _anim = _body = null;
            try
            {
                if (string.IsNullOrEmpty(line)) return;
                int colon = line.IndexOf(": ", StringComparison.Ordinal);
                if (colon < 0) return;
                string rest = line.Substring(colon + 2);
                if (rest.StartsWith("(") && rest.IndexOf(')') > 0)
                {
                    int close = rest.IndexOf(')');
                    foreach (var raw in rest.Substring(1, close - 1).Split(','))
                    {
                        string p = raw.Trim();
                        if (p.StartsWith("*")) _anim = p.Trim('*').ToLowerInvariant();
                        else if (p.Length > 0) _mood = p.ToLowerInvariant();
                    }
                    rest = rest.Substring(close + 1).TrimStart();
                }
                _body = Speech.Clean(rest);
            }
            catch { }
        }

        private static void LinePostfix(DialogueManager __instance)
        {
            try
            {
                if (_body == null || !ModConfig.DescribeScenes.Value) return;
                var slot = _speaking(__instance);
                string name = SlotName(slot);
                var parts = new List<string>();
                string mood = MoodText(_mood);
                string anim = AnimText(_anim);
                string who = name;
                if (mood != null) who = who != null ? Loc.F("scene.who_mood", who, mood) : mood;
                if (anim != null) who = who != null ? who + ", " + anim : anim;
                string text = who != null ? who + ": " + _body : _body;
                Plugin.LogDebug("scene line: " + text);
                Speech.SayEvent(text);
            }
            catch (Exception ex) { Plugin.LogDebug("Scene line: " + ex.Message); }
        }

        private static string SlotName(DialogueCharacterSlot slot)
        {
            if (slot == null) return null;
            string n = null;
            try
            {
                if (slot.ActiveCharacter != null) n = slot.ActiveCharacter.Alias;
                if (string.IsNullOrEmpty(n) && _slotAlias != null) n = (_slotAlias.GetValue(slot) as string)?.Replace("_", " ");
            }
            catch { }
            if (string.IsNullOrEmpty(n) || n == "*blank*") return null;
            return Speech.Clean(n);
        }

        private static string CharacterName(DialogueManager m, string id)
        {
            try
            {
                foreach (var s in _slots(m))
                {
                    if (s == null) continue;
                    if (s.ActiveCharacter != null && s.ActiveCharacter.IsCharacter(id)) return SlotName(s);
                    var c = s.Characters?.FirstOrDefault(x => x != null && x.IsCharacter(id));
                    if (c != null) return Speech.Clean(c.Alias);
                }
            }
            catch { }
            return char.ToUpperInvariant(id[0]) + id.Substring(1).ToLowerInvariant();
        }

        // ---- Commands: entrances, location, darkness, the Rift ----
        private static void CommandPrefix(DialogueManager __instance, string[] parameters, System.Reflection.MethodBase __originalMethod)
        {
            try
            {
                if (!ModConfig.DescribeScenes.Value || parameters == null || parameters.Length == 0) return;
                string cmd = __originalMethod.Name, text = null;
                string p0 = parameters[0] ?? "";
                switch (cmd)
                {
                    case "SetCharacter":
                        if (parameters.Length < 2) return;
                        string id = parameters[1];
                        bool leaving = id == "" || id.Equals("none", StringComparison.OrdinalIgnoreCase)
                                       || parameters.Any(p => p != null && p.ToLowerInvariant().StartsWith("disappear"));
                        string side = p0.ToLowerInvariant().StartsWith("left") ? Loc.T("lane.left") : p0.ToLowerInvariant().StartsWith("right") ? Loc.T("lane.right") : null;
                        if (leaving)
                        {
                            var slot = _slots(__instance)?.FirstOrDefault(s => s != null && s.Position.ToString().Equals(p0, StringComparison.OrdinalIgnoreCase) && s.ActiveCharacter != null);
                            string who = SlotName(slot);
                            if (who != null) text = Loc.F("scene.leaves", who);
                        }
                        else if (id.Length > 0)
                        {
                            string who = CharacterName(__instance, id);
                            text = side != null ? Loc.F("scene.enters_side", who, side) : Loc.F("scene.enters", who);
                        }
                        break;
                    case "SetBackground":
                        _background = p0;
                        _manager = __instance;
                        text = Loc.F("scene.location", Background(p0));
                        break;
                    case "DarkenScreen":
                        if (int.TryParse(p0, out int pct)) text = Loc.T(pct == 0 ? "scene.light" : "scene.dark");
                        break;
                    case "RiftSeize": text = Loc.F("scene.seize", CharacterName(__instance, p0)); break;
                    case "RiftRelease": text = Loc.F("scene.release", CharacterName(__instance, p0)); break;
                    case "SetRiftBackground":
                    case "SetRiftOverlay":
                        text = Loc.T(p0.Equals("on", StringComparison.OrdinalIgnoreCase) ? "scene.rift_on" : "scene.rift_off");
                        break;
                    case "PlayVfx":
                        if (p0.IndexOf("rift", StringComparison.OrdinalIgnoreCase) >= 0) text = Loc.T("scene.rift_flare");
                        break;
                    case "SetAnimation":
                        if (parameters.Length > 1)
                        {
                            string a = AnimText(parameters[1].ToLowerInvariant());
                            if (a != null) text = CharacterName(__instance, p0) + ", " + a;
                        }
                        break;
                }
                if (text == null) return;
                Plugin.LogDebug("scene: " + text);
                Speech.SayEvent(text);
            }
            catch (Exception ex) { Plugin.LogDebug("Scene command: " + ex.Message); }
        }

        private static string _background;
        private static DialogueManager _manager;

        /// <summary>Review buffer in a dialogue scene: the place and who is on screen, else null.</summary>
        public static string CurrentSceneText()
        {
            if (_manager == null || !_manager.isActiveAndEnabled) _manager = UnityEngine.Object.FindObjectOfType<DialogueManager>();
            if (_manager == null || !_manager.isActiveAndEnabled) return null;
            var parts = new List<string>();
            if (_background != null) parts.Add(Loc.F("scene.location", Background(_background)));
            var names = new List<string>();
            try
            {
                foreach (var s in _slots(_manager))
                    if (s != null && s.ActiveCharacter != null && s.ActiveCharacter.gameObject.activeInHierarchy)
                    {
                        string n = SlotName(s);
                        if (n != null && !names.Contains(n)) names.Add(n);
                    }
            }
            catch { }
            if (names.Count > 0) parts.Add(Loc.F("scene.present", string.Join(", ", names)));
            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>The mod's description of a dialogue background (Lang bg.&lt;key&gt;), keyed like the script's
        /// setbackground argument without "_MG" and "_Dialogue_BG".</summary>
        public static string Background(string key)
        {
            string k = (key ?? "").ToLowerInvariant().Replace("_dialogue_bg", "").Replace("_mg", "");
            string s = Loc.TryOwn("bg." + k);
            if (s == null && Loc.Has("bg." + k)) s = Loc.T("bg." + k);
            return s ?? key.Replace("_", " ");
        }

        // ---- In-song dialogue: speaker and bubble tone, spoken with the line by NarrationReader ----
        private static string _songPrefix;
        private static int _songFrame = -100;

        private static void SongLinePostfix(RhythmRift.RRDialogueLineData dialogueLineData)
        {
            try
            {
                if (!ModConfig.DescribeScenes.Value) { _songPrefix = null; return; }
                string name = null;
                if (!string.IsNullOrEmpty(dialogueLineData.SpeakingCharacterAlias))
                    name = Speech.Clean(TicToc.Localization.Localizer.GetText(dialogueLineData.SpeakingCharacterAlias));
                string pose = dialogueLineData.SpeakingCharacter == RhythmRift.RRDialogueSpeakingCharacter.LeftPortrait
                    ? dialogueLineData.LeftCharacterPose : dialogueLineData.RightCharacterPose;
                string mood = MoodText(pose?.ToLowerInvariant());
                string tone = dialogueLineData.SpeechBubble == RhythmRift.RRDialogueSpeechBubble.Exclamation ? Loc.T("scene.shouts")
                            : dialogueLineData.SpeechBubble == RhythmRift.RRDialogueSpeechBubble.Thinking ? Loc.T("scene.thinks") : null;
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(name)) parts.Add(name);
                if (mood != null) parts.Add(mood);
                if (tone != null) parts.Add(tone);
                _songPrefix = parts.Count > 0 ? string.Join(", ", parts) : null;
                _songFrame = Time.frameCount;
            }
            catch (Exception ex) { Plugin.LogDebug("Song line: " + ex.Message); }
        }

        /// <summary>"Cadence, shouting" for the in-song line just shown, once, else null.</summary>
        public static string TakeSongPrefix()
        {
            if (_songPrefix == null || Time.frameCount - _songFrame > 30) return null;
            string p = _songPrefix;
            _songPrefix = null;
            return p;
        }

        // ---- Expressions ----
        private static string MoodText(string pose)
        {
            if (string.IsNullOrEmpty(pose)) return null;
            bool phone = pose.StartsWith("phone");
            string p = pose.Replace("earbuds", "");
            if (phone) p = p.Substring(5);
            if (!Moods.TryGetValue(p, out var key))
            {
                Plugin.LogDebug("Unknown pose " + pose);
                return phone ? Loc.T("mood.phone") : null;
            }
            string m = key != null ? Loc.T("mood." + key) : null;
            if (phone) m = m != null ? Loc.F("mood.on_phone", m) : Loc.T("mood.phone");
            return m;
        }

        private static string AnimText(string anim)
        {
            switch (anim)
            {
                case "jump": return Loc.T("anim.jump");
                case "laugh": return Loc.T("anim.laugh");
                case "shakeangry": return Loc.T("anim.shakeangry");
                case "shakescared": return Loc.T("anim.shakescared");
                case "drop": return Loc.T("anim.drop");
                default: return null;
            }
        }

        private static void BuildMoods()
        {
            void Map(string key, params string[] poses) { foreach (var p in poses) Moods[p] = key; }
            Map(null, "normal", "normaltilt", "neutral", "calm", "normalflute", "fluteeyesopen", "action", "bridget", "cora", "versa");
            Map("happy", "happy", "smile", "pleased", "pleasedeyesopen", "satisfied");
            Map("eyes_closed", "normaleyesclosed", "pleasedeyesclosed", "plpleasedeyesclosed", "fluteeyesclosed");
            Map("excited", "excited", "ecstatic", "inspired", "triumphant");
            Map("laughing", "laugh");
            Map("surprised", "surprised", "shocked", "offguard", "surprisedshadow");
            Map("realizing", "realization");
            Map("awkward", "awkward", "sweatdrop");
            Map("angry", "angry", "bigangry", "aggravated", "shakeangry", "scowl");
            Map("annoyed", "annoyed", "irritated", "frustrated", "upset");
            Map("sad", "sad", "somber", "gloomy", "disappointed", "frowning");
            Map("despairing", "despair", "defeated", "defeated2", "defeated3");
            Map("worried", "worried", "fearful", "shakescared", "harried", "conflicted");
            Map("confused", "confused");
            Map("thoughtful", "thoughtful", "thoughful", "focused");
            Map("serious", "serious", "determined");
            Map("confident", "confident", "smug", "smirk");
            Map("mocking", "mocking", "taunting", "eyeroll");
            Map("suspicious", "suspicious", "sus");
            Map("menacing", "menacing", "threatening", "threateningplus", "threateningflame");
            Map("tired", "tired", "exhausted", "wornout");
            Map("mystical", "mystical", "consumed");
        }
    }
}
