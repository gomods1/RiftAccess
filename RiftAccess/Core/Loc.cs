using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace RiftAccess.Core
{
    /// <summary>
    /// The mod's own strings. One table per language in Lang/&lt;code&gt;.txt (embedded in the DLL; a file of the same
    /// name in the plugin's Lang folder overrides it). Lines are "key = value", # starts a comment, \n is a line
    /// break, {0} {1} are format arguments. The language follows the game's (TicToc Localizer); a missing key
    /// falls back to English and is logged once.
    /// </summary>
    internal static class Loc
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Tables = new Dictionary<string, Dictionary<string, string>>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static string _lang = "en";
        private static float _nextCheck;

        public static string Language => _lang;

        /// <summary>Re-reads the game's language now and then (it can change in the options).</summary>
        public static void Refresh() => _nextCheck = 0f;

        public static void Update()
        {
            float now = UnityEngine.Time.unscaledTime;
            if (now < _nextCheck) return;
            _nextCheck = now + 1f;
            string lang = GameLanguage();
            if (lang != _lang)
            {
                _lang = lang;
                Plugin.LogInfo("Mod language: " + lang);
            }
        }

        private static string GameLanguage()
        {
            try
            {
                switch (TicToc.Localization.Localizer.GetLanguageEnum())
                {
                    case TicToc.Localization.Localizer.Language.Spanish_Spain:
                    case TicToc.Localization.Localizer.Language.Spanish_LATAM: return "es";
                    case TicToc.Localization.Localizer.Language.French: return "fr";
                    case TicToc.Localization.Localizer.Language.Italian: return "it";
                    case TicToc.Localization.Localizer.Language.German: return "de";
                    case TicToc.Localization.Localizer.Language.Dutch: return "nl";
                    case TicToc.Localization.Localizer.Language.Portuguese_Brazil: return "pt";
                    case TicToc.Localization.Localizer.Language.Russian: return "ru";
                    case TicToc.Localization.Localizer.Language.Japanese: return "ja";
                    case TicToc.Localization.Localizer.Language.Korean: return "ko";
                    case TicToc.Localization.Localizer.Language.Chinese_Simplified: return "zh-Hans";
                    case TicToc.Localization.Localizer.Language.Chinese_Traditional: return "zh-Hant";
                    default: return "en";
                }
            }
            catch { return _lang; }
        }

        private static Dictionary<string, string> Table(string lang)
        {
            if (Tables.TryGetValue(lang, out var t)) return t;
            t = new Dictionary<string, string>();
            Tables[lang] = t;
            string text = null;
            try
            {
                string file = Path.Combine(Path.Combine(Plugin.PluginDir ?? "", "Lang"), lang + ".txt");
                if (File.Exists(file)) text = File.ReadAllText(file);
                else
                {
                    using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("RiftAccess.Lang." + lang + ".txt"))
                        if (s != null) using (var r = new StreamReader(s)) text = r.ReadToEnd();
                }
            }
            catch (Exception ex) { Plugin.LogWarning("Language table " + lang + ": " + ex.Message); }
            if (text == null) return t;
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.TrimStart().StartsWith("#")) continue;
                int eq = line.IndexOf(" = ", StringComparison.Ordinal);
                if (eq <= 0) continue;
                t[line.Substring(0, eq).Trim()] = line.Substring(eq + 3).Replace("\\n", "\n");
            }
            return t;
        }

        public static bool Has(string key) => Table(_lang).ContainsKey(key) || Table("en").ContainsKey(key);

        /// <summary>The string for key in the game's language.</summary>
        public static string T(string key)
        {
            if (Table(_lang).TryGetValue(key, out var v)) return v;
            if (Missing.Add(_lang + ":" + key)) Plugin.LogWarning("Missing string " + key + " for " + _lang);
            return Table("en").TryGetValue(key, out v) ? v : key;
        }

        /// <summary>The string for key with {0}, {1}... filled in.</summary>
        public static string F(string key, params object[] args)
        {
            try { return string.Format(T(key), args); }
            catch { return T(key); }
        }

        /// <summary>The string only if this language has it (no English fallback), else null.</summary>
        public static string TryOwn(string key)
        {
            if (Table(_lang).TryGetValue(key, out var v)) return v;
            return _lang != "en" ? null : (Table("en").TryGetValue(key, out v) ? v : null);
        }
    }
}
