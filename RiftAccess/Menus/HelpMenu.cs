using System.Collections.Generic;
using RiftAccess.Core;
using RiftAccess.Gameplay;
using UnityEngine.InputSystem;

namespace RiftAccess.Menus
{
    /// <summary>F1: a browsable list of the game's and the mod's keys. Up/Down read, Escape or F1 close.</summary>
    internal static class HelpMenu
    {
        public static bool IsOpen { get; private set; }
        private static readonly List<string> Lines = new List<string>();
        private static int _index;

        public static void Open()
        {
            Lines.Clear();
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
            }
            else
            {
                Lines.Add(Loc.T("help.menu1"));
                Lines.Add(Loc.T("help.menu2"));
            }
            Lines.Add(Loc.T("help.f1"));
            Lines.Add(Loc.T("help.f2"));
            Lines.Add(Loc.T("help.h"));
            Lines.Add(Loc.T("help.f10"));
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
                InputBlocker.Release();
                Speech.Say(Loc.T("help.closed"), true);
                return;
            }
            if (KeyInput.WasPressed(Key.DownArrow))
            {
                if (_index < Lines.Count - 1) _index++;
                Speech.Say((_index == Lines.Count - 1 ? Loc.T("help.end") + " " : "") + Lines[_index], true);
            }
            else if (KeyInput.WasPressed(Key.UpArrow))
            {
                if (_index > 0) _index--;
                Speech.Say(Lines[_index], true);
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
