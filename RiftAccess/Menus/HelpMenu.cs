using System;
using System.Collections.Generic;
using RiftAccess.Audio;
using RiftAccess.Core;
using RiftAccess.Gameplay;
using UnityEngine.InputSystem;

namespace RiftAccess.Menus
{
    /// <summary>F1: a browsable list of the game's and the mod's keys, then a sound guide whose lines play the cue
    /// they describe. Up/Down read, Escape or F1 close.</summary>
    internal static class HelpMenu
    {
        public static bool IsOpen { get; private set; }
        private static readonly List<string> Lines = new List<string>();
        private static readonly List<Action> Sounds = new List<Action>();
        private static int _index;
        private static readonly List<CuePlayer.Scheduled> Playing = new List<CuePlayer.Scheduled>();
        private static FMOD.Channel? _loop;

        private static void Cue(string name, int lane, float delay = 0f, float pitch = 1f)
        {
            float pan = (lane - 1) * ModConfig.PanWidth.Value / 100f;
            var h = CuePlayer.Play(name, delay, pan, ModConfig.CueVolume.Value / 100f, pitch);
            if (h != null) Playing.Add(h);
        }

        /// <summary>Stops the guide's own sounds that have not started (never the song's cues).</summary>
        private static void StopGuide()
        {
            foreach (var h in Playing) CuePlayer.Cancel(h);
            Playing.Clear();
            CuePlayer.Stop(_loop);
            _loop = null;
        }

        private static void AddSoundGuide()
        {
            Lines.Add(Loc.T("snd.title"));
            Sounds.Add(null);
            void Add(string key, Action play) { Lines.Add(Loc.T(key)); Sounds.Add(play); }
            string L(int lane) => !ModConfig.LanePitch.Value ? "mid" : lane == 0 ? "left" : lane == 2 ? "right" : "mid";
            Add("snd.left", () => Cue("tap_" + L(0), 0));
            Add("snd.mid", () => Cue("tap_" + L(1), 1));
            Add("snd.right", () => Cue("tap_" + L(2), 2));
            Add("snd.all", () => { Cue("tap_" + L(0), 0); Cue("tap_" + L(1), 1); Cue("tap_" + L(2), 2); });
            Add("snd.hold", () =>
            {
                Cue("hold_" + L(1), 1);
                if (ModConfig.HoldTone.Value)
                    _loop = CuePlayer.PlayLoop("hold_loop", 0.5f, 2.0f, 0f, ModConfig.CueVolume.Value / 100f * 0.7f);
                if (ModConfig.HoldReleaseCue.Value) Cue("release_" + L(1), 1, 2.0f);
            });
            Add("snd.heart", () => Cue((ModConfig.HealthItemCue.Value ? "heart_" : "tap_") + L(2), 2));
            Add("snd.dodge", () => { Cue("dodge_left", 0); Cue("dodge_right", 2, 0.5f); });
            Add("snd.up", () => Cue("tap_up", 1));
            Add("snd.down", () => Cue("tap_down", 1));
            Add("snd.vibe", () => Cue("vibe_ready", 1));
            Add("snd.warning", () => Cue("warning", 1));
            Add("snd.click", () => Cue("click", 1));
            Add("snd.locked", () => { Cue("locked", 1); Cue("unlocked", 1, 1.2f); });
        }

        private static void Read(bool atEnd)
        {
            StopGuide();
            Speech.Say((atEnd ? Loc.T("help.end") + " " : "") + Lines[_index], true);
            try { Sounds[_index]?.Invoke(); } catch (Exception ex) { Plugin.LogDebug("Sound guide: " + ex.Message); }
        }

        public static void Open()
        {
            Lines.Clear();
            Sounds.Clear();
            Lines.Add(Loc.T("help.title"));
            if (StageWatcher.InSong)
            {
                Lines.Add(Loc.T("help.song1"));
                Lines.Add(Loc.T("help.song2"));
                Lines.Add(Loc.F("help.song3", ModConfig.LeadText(ModConfig.LeadBeats.Value)));
                Lines.Add(Loc.T("help.song4"));
                Lines.Add(Loc.T("help.song5"));
                Lines.Add(Loc.T("help.song6"));
                Lines.Add(Loc.T("help.song7"));
                Lines.Add(Loc.T("help.song8"));
            }
            else
            {
                Lines.Add(Loc.T("help.menu1"));
                Lines.Add(Loc.T("help.menu2"));
            }
            Lines.Add(Loc.T("help.f1"));
            Lines.Add(Loc.T("help.f2"));
            Lines.Add(Loc.T("help.buffers"));
            Lines.Add(Loc.T("help.h"));
            Lines.Add(Loc.T("help.h2"));
            Lines.Add(Loc.T("help.f10"));
            while (Sounds.Count < Lines.Count) Sounds.Add(null);
            AddSoundGuide();
            IsOpen = true;
            _index = 0;
            InputBlocker.Block();
            Speech.Say(Lines[0], true);
        }

        public static void Update()
        {
            if (KeyInput.WasPressed(Key.Escape) || KeyInput.WasPressed(Key.F1) || KeyInput.WasPressed(Key.Enter))
            {
                IsOpen = false;
                StopGuide();
                InputBlocker.Release();
                Speech.Say(Loc.T("help.closed"), true);
                return;
            }
            if (KeyInput.WasPressed(Key.DownArrow))
            {
                if (_index < Lines.Count - 1) _index++;
                Read(_index == Lines.Count - 1);
            }
            else if (KeyInput.WasPressed(Key.UpArrow))
            {
                if (_index > 0) _index--;
                Read(false);
            }
        }
    }

    /// <summary>Mod hotkeys outside its menus. None of them is bound by the game.</summary>
    internal static class Hotkeys
    {
        public static void Update()
        {
            if (KeyInput.Ctrl && KeyInput.WasPressed(Key.F12)) { ScreenTextWatcher.Dump(); return; }
            if (KeyInput.Ctrl && KeyInput.WasPressed(Key.F11) && ModConfig.DebugLogging.Value) { CueBot.Toggle(); return; }
            if (KeyInput.Ctrl || KeyInput.Alt) return;
            if (KeyInput.WasPressed(Key.F10)) ModSettingsMenu.Open();
            else if (KeyInput.WasPressed(Key.F1)) HelpMenu.Open();
            else if (KeyInput.WasPressed(Key.F2)) MenuReader.RepeatFocus();
            else if (KeyInput.WasPressed(Key.H))
            {
                string status = StageWatcher.StatusText();
                Speech.Say(status ?? Loc.T("not_in_song"), true);
            }
        }
    }
}
