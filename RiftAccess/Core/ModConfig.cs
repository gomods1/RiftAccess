using System;
using System.Collections.Generic;
using System.Globalization;
using BepInEx.Configuration;

namespace RiftAccess.Core
{
    /// <summary>
    /// Every mod setting. Stored by BepInEx (BepInEx\config\gomods.riftaccess.cfg), changed by players in the
    /// in-game Rift Access settings menu (F10), which is built from <see cref="All"/>.
    /// </summary>
    internal static class ModConfig
    {
        // Lane cues
        public static ConfigEntry<bool> CuesEnabled;
        public static ConfigEntry<float> LeadBeats;
        public static ConfigEntry<int> CountIn;
        public static ConfigEntry<bool> HitClick;
        public static ConfigEntry<int> CueVolume;
        public static ConfigEntry<int> PanWidth;
        public static ConfigEntry<bool> LanePitch;
        public static ConfigEntry<int> TimingOffsetMs;
        public static ConfigEntry<bool> FollowUpCues;
        public static ConfigEntry<bool> HoldReleaseCue;
        public static ConfigEntry<bool> HoldTone;
        public static ConfigEntry<bool> HealthItemCue;
        public static ConfigEntry<int> MusicVolume;

        // Gameplay announcements
        public static ConfigEntry<bool> SpeakDuringSongs;
        public static ConfigEntry<bool> AnnounceDamage;
        public static ConfigEntry<bool> LowHealthSound;
        public static ConfigEntry<int> LowHealthThreshold;
        public static ConfigEntry<bool> VibeReadyCue;
        public static ConfigEntry<bool> VibeReadySpeech;
        public static ConfigEntry<int> ComboMilestone;
        public static ConfigEntry<bool> AnnounceComboLost;
        public static ConfigEntry<bool> AnnounceNewEnemies;
        public static ConfigEntry<bool> AnnounceResults;
        public static ConfigEntry<bool> LockoutCue;
        public static ConfigEntry<bool> AnnounceVibeChains;
        public static ConfigEntry<bool> AnnounceTraps;
        public static ConfigEntry<bool> DescribeScenes;

        public static ConfigEntry<bool> TutorialGuide;
        public static ConfigEntry<bool> InstantDialogue;

        // Speech and menus
        public static ConfigEntry<bool> InterruptOnNavigation;
        public static ConfigEntry<bool> ReadPosition;
        public static ConfigEntry<bool> Braille;
        public static ConfigEntry<string> SpeechBackend;
        public static ConfigEntry<bool> DebugLogging;
        public static ConfigEntry<bool> DevFastBoot;

        public static readonly List<Setting> All = new List<Setting>();

        public static void Bind(ConfigFile cfg)
        {
            const string cues = "Lane cues";
            CuesEnabled = cfg.Bind(cues, "Enabled", true, "Play a sound for every enemy you must hit, in its lane.");
            LeadBeats = cfg.Bind(cues, "LeadBeats", 1f, "How many beats before the hit the lane cue plays.");
            CountIn = cfg.Bind(cues, "CountIn", 0, "Extra quieter cues, one beat apart, before the main cue.");
            HitClick = cfg.Bind(cues, "HitClick", false, "Also play a soft click exactly on the beat you must hit.");
            CueVolume = cfg.Bind(cues, "Volume", 100, "Volume of the lane cues, in percent.");
            PanWidth = cfg.Bind(cues, "StereoWidth", 100, "How far left and right the side lanes are placed, in percent.");
            LanePitch = cfg.Bind(cues, "LanePitch", true, "Give each lane its own pitch: low left, middle centre, high right.");
            TimingOffsetMs = cfg.Bind(cues, "TimingOffsetMs", 0, "Moves every cue earlier (negative) or later (positive), in milliseconds.");
            FollowUpCues = cfg.Bind(cues, "FollowUpCues", true, "Cue every hit of enemies that take several hits.");
            HoldReleaseCue = cfg.Bind(cues, "HoldReleaseCue", true, "For hold notes, play a release sound when you may let go.");
            HoldTone = cfg.Bind(cues, "HoldTone", true, "For hold notes, play a humming tone while you keep the key down.");
            HealthItemCue = cfg.Bind(cues, "HealthItemCue", true, "Use a sparkle sound for hearts (hit them to heal).");
            MusicVolume = cfg.Bind(cues, "MusicDuck", 75, "Volume of the game's music while a song plays, in percent, to hear cues better.");

            const string ann = "Announcements";
            SpeakDuringSongs = cfg.Bind(ann, "SpeakDuringSongs", true, "Allow speech while a song is playing.");
            AnnounceDamage = cfg.Bind(ann, "AnnounceDamage", true, "Say your health when you are hurt or healed.");
            LowHealthSound = cfg.Bind(ann, "LowHealthSound", true, "Play a warning sound when your health is low.");
            LowHealthThreshold = cfg.Bind(ann, "LowHealthThreshold", 3, "Health at or below which the low health warning plays.");
            VibeReadyCue = cfg.Bind(ann, "VibeReadyCue", true, "Play a sound when vibe power can be activated (Space).");
            VibeReadySpeech = cfg.Bind(ann, "VibeReadySpeech", false, "Also say \"vibe ready\".");
            ComboMilestone = cfg.Bind(ann, "ComboMilestone", 0, "Say your combo every this many hits. 0 turns it off.");
            AnnounceComboLost = cfg.Bind(ann, "AnnounceComboLost", false, "Say when you lose a combo.");
            AnnounceNewEnemies = cfg.Bind(ann, "AnnounceNewEnemies", false, "Say the name of each enemy type the first time it appears in a song.");
            AnnounceResults = cfg.Bind(ann, "AnnounceResults", true, "Read the results when a song ends.");
            AnnounceVibeChains = cfg.Bind(ann, "AnnounceVibeChains", true, "Say when a vibe chain starts (and how many monsters it has), breaks or is completed.");
            AnnounceTraps = cfg.Bind(ann, "AnnounceTraps", true, "Say each trap that appears on the board: its kind, lane and row, and what it does.");
            DescribeScenes = cfg.Bind(ann, "DescribeScenes", true, "In story dialogue, say who speaks and their expression, who appears or leaves, the place and what happens on screen.");
            LockoutCue = cfg.Bind(ann, "LockoutCue", true, "Play a sound and speak when the game locks your keys after stray presses, and when a Down press misses because a lane was empty.");
            TutorialGuide = cfg.Bind(ann, "TutorialGuide", true, "In tutorials, replace hints about what is on screen with how to play by ear.");
            InstantDialogue = cfg.Bind(ann, "InstantDialogue", true, "Show dialogue lines at once, so each Enter moves to the next line.");

            const string sp = "Speech";
            InterruptOnNavigation = cfg.Bind(sp, "InterruptOnNavigation", false, "Menu navigation interrupts what is being spoken.");
            ReadPosition = cfg.Bind(sp, "ReadPosition", false, "Say the position of the menu item, such as 2 of 5.");
            Braille = cfg.Bind(sp, "Braille", true, "Send speech to a braille display too.");
            SpeechBackend = cfg.Bind(sp, "Backend", "Auto", "Prism speech backend: Auto, or a backend name such as NVDA or SAPI.");
            DebugLogging = cfg.Bind(sp, "DebugLogging", false, "Write detailed diagnostics to the BepInEx log.");
            DevFastBoot = cfg.Bind(sp, "DevFastBoot", false, "Developers: skip the intro and title screen at startup (not shown in the F10 menu).");

            All.Clear();
            const string c = "section.cues", a = "section.announce", p = "section.speech";
            All.Add(new BoolSetting(c, "set.cues", CuesEnabled));
            All.Add(new FloatChoiceSetting(c, "set.lead", LeadBeats, new[] { 0.5f, 0.75f, 1f, 1.5f, 2f, 3f, 4f }, v => Loc.F("fmt.lead_early", LeadText(v))));
            All.Add(new IntSetting(c, "set.countin", CountIn, 0, 3, 1, v => v == 0 ? Loc.T("off") : v.ToString()));
            All.Add(new BoolSetting(c, "set.click", HitClick));
            All.Add(new IntSetting(c, "set.volume", CueVolume, 0, 150, 5, v => Loc.F("fmt.percent", v)));
            All.Add(new IntSetting(c, "set.width", PanWidth, 0, 100, 10, v => Loc.F("fmt.percent", v)));
            All.Add(new BoolSetting(c, "set.pitch", LanePitch));
            All.Add(new IntSetting(c, "set.offset", TimingOffsetMs, -300, 300, 10, v => v == 0 ? Loc.T("fmt.none") : Loc.F("fmt.ms", v)));
            All.Add(new BoolSetting(c, "set.followup", FollowUpCues));
            All.Add(new BoolSetting(c, "set.holdtone", HoldTone));
            All.Add(new BoolSetting(c, "set.release", HoldReleaseCue));
            All.Add(new BoolSetting(c, "set.heart", HealthItemCue));
            All.Add(new IntSetting(c, "set.music", MusicVolume, 0, 100, 10, v => Loc.F("fmt.percent", v)));

            All.Add(new BoolSetting(a, "set.speaksongs", SpeakDuringSongs));
            All.Add(new BoolSetting(a, "set.damage", AnnounceDamage));
            All.Add(new BoolSetting(a, "set.lowsound", LowHealthSound));
            All.Add(new IntSetting(a, "set.lowthreshold", LowHealthThreshold, 1, 10, 1, v => v.ToString()));
            All.Add(new BoolSetting(a, "set.vibesound", VibeReadyCue));
            All.Add(new BoolSetting(a, "set.vibespeech", VibeReadySpeech));
            All.Add(new IntChoiceSetting(a, "set.combo", ComboMilestone, new[] { 0, 10, 25, 50, 100 }, v => v == 0 ? Loc.T("off") : Loc.F("fmt.every", v)));
            All.Add(new BoolSetting(a, "set.combolost", AnnounceComboLost));
            All.Add(new BoolSetting(a, "set.enemies", AnnounceNewEnemies));
            All.Add(new BoolSetting(a, "set.lockout", LockoutCue));
            All.Add(new BoolSetting(a, "set.chains", AnnounceVibeChains));
            All.Add(new BoolSetting(a, "set.traps", AnnounceTraps));
            All.Add(new BoolSetting(a, "set.scenes", DescribeScenes));
            All.Add(new BoolSetting(a, "set.results", AnnounceResults));
            All.Add(new BoolSetting(a, "set.tutorial", TutorialGuide));
            All.Add(new BoolSetting(a, "set.instant", InstantDialogue));

            All.Add(new BoolSetting(p, "set.interrupt", InterruptOnNavigation));
            All.Add(new BoolSetting(p, "set.position", ReadPosition));
            All.Add(new BoolSetting(p, "set.braille", Braille));
            All.Add(new BoolSetting(p, "set.debug", DebugLogging));
        }

        /// <summary>"1 beat", "1.5 beats" in the player's language.</summary>
        public static string LeadText(float v)
        {
            if (Math.Abs(v - 1f) < 0.001f) return Loc.T("fmt.lead1");
            return Loc.F("fmt.lead", v.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>One line of the settings menu: a label, its current value, and how to change it.</summary>
    internal abstract class Setting
    {
        public string SectionKey { get; }
        public string LabelKey { get; }
        public string Section => Loc.T(SectionKey);
        public string Label => Loc.T(LabelKey);
        public virtual string Help => Loc.T(LabelKey + ".help");
        public abstract string ValueText { get; }
        public abstract string Role { get; }
        /// <summary>Change the value one step (dir = +1 / -1). Returns false at the end of the range.</summary>
        public abstract bool Step(int dir);
        public virtual void Activate() => Step(1);

        protected Setting(string section, string label) { SectionKey = section; LabelKey = label; }
    }

    internal sealed class BoolSetting : Setting
    {
        private readonly ConfigEntry<bool> _e;
        public BoolSetting(string s, string l, ConfigEntry<bool> e) : base(s, l) { _e = e; }
        public override string ValueText => Loc.T(_e.Value ? "on" : "off");
        public override string Role => Loc.T("role.checkbox");
        public override bool Step(int dir) { _e.Value = !_e.Value; return true; }
    }

    internal sealed class IntSetting : Setting
    {
        private readonly ConfigEntry<int> _e;
        private readonly int _min, _max, _step;
        private readonly Func<int, string> _fmt;
        public IntSetting(string s, string l, ConfigEntry<int> e, int min, int max, int step, Func<int, string> fmt) : base(s, l)
        { _e = e; _min = min; _max = max; _step = step; _fmt = fmt; }
        public override string ValueText => _fmt(_e.Value);
        public override string Role => Loc.T("role.slider");
        public override bool Step(int dir)
        {
            int v = Math.Max(_min, Math.Min(_max, _e.Value + dir * _step));
            if (v == _e.Value) return false;
            _e.Value = v;
            return true;
        }
        public override void Activate() { }
    }

    internal sealed class IntChoiceSetting : Setting
    {
        private readonly ConfigEntry<int> _e;
        private readonly int[] _choices;
        private readonly Func<int, string> _fmt;
        public IntChoiceSetting(string s, string l, ConfigEntry<int> e, int[] choices, Func<int, string> fmt) : base(s, l)
        { _e = e; _choices = choices; _fmt = fmt; }
        public override string ValueText => _fmt(_e.Value);
        public override string Role => Loc.T("role.choice");
        public override bool Step(int dir)
        {
            int i = Array.IndexOf(_choices, _e.Value);
            if (i < 0) i = 0;
            int n = Math.Max(0, Math.Min(_choices.Length - 1, i + dir));
            if (n == i && _choices[i] == _e.Value) return false;
            _e.Value = _choices[n];
            return true;
        }
        public override void Activate() { }
    }

    internal sealed class FloatChoiceSetting : Setting
    {
        private readonly ConfigEntry<float> _e;
        private readonly float[] _choices;
        private readonly Func<float, string> _fmt;
        public FloatChoiceSetting(string s, string l, ConfigEntry<float> e, float[] choices, Func<float, string> fmt) : base(s, l)
        { _e = e; _choices = choices; _fmt = fmt; }
        public override string ValueText => _fmt(_e.Value);
        public override string Role => Loc.T("role.choice");
        public override bool Step(int dir)
        {
            int i = 0;
            float best = float.MaxValue;
            for (int k = 0; k < _choices.Length; k++)
            {
                float d = Math.Abs(_choices[k] - _e.Value);
                if (d < best) { best = d; i = k; }
            }
            int n = Math.Max(0, Math.Min(_choices.Length - 1, i + dir));
            if (n == i && best < 0.001f) return false;
            _e.Value = _choices[n];
            return true;
        }
        public override void Activate() { }
    }
}
