using System.Collections.Generic;
using RiftAccess.Core;
using UnityEngine.InputSystem;

namespace RiftAccess.Menus
{
    /// <summary>
    /// Review buffers (Alt+arrows; Ctrl is the game's tab/difficulty key). Two buffers:
    ///   - Item: the focused thing only. Line 0 is what was just spoken for it, then its details: the description of
    ///     its picture (album cover, monster, profile item...), or in a story scene the place and who is there, in a
    ///     boss battle the boss, in a song its background. Alt+Up goes deeper, Alt+Down back to the name.
    ///   - Events: recent announcements, newest first.
    /// Alt+Left/Right switch buffers. While Alt is held outside songs the game sees no input, so its arrows do not
    /// also move the menu. Inactive during songs, where Alt+arrow would also hit lanes, except while a tutorial box
    /// shows a demo video (the song waits for the box).
    /// </summary>
    internal static class ReviewBuffers
    {
        private static readonly List<string> Item = new List<string>();
        private static readonly List<string> Events = new List<string>();
        private static string _itemKey;
        private static int _itemCursor, _eventCursor = -1, _buffer;
        private static bool _gating, _videoBox;

        /// <summary>True while Alt is held where the buffers are live (the input blocker then hides keys from the game).</summary>
        public static bool AltGate
        {
            get
            {
                var kb = Keyboard.current;
                return kb != null && kb.altKey.isPressed && (!Gameplay.StageWatcher.InSong || _videoBox) && !ModSettingsMenu.IsOpen && !HelpMenu.IsOpen;
            }
        }

        public static void AddEvent(string text)
        {
            Events.Insert(0, text);
            if (_eventCursor >= 0) _eventCursor++;
            if (Events.Count > 100) Events.RemoveAt(Events.Count - 1);
        }

        public static void Update()
        {
            PictureDescriber.PollProfileCard();
            // A tutorial box with a demo video holds the song: the buffers work there too, to describe the video.
            var kb = Keyboard.current;
            _videoBox = kb != null && kb.altKey.isPressed && Gameplay.StageWatcher.InSong && TutorialVideos.Current() != null;
            bool gate = AltGate;
            if (gate && !_gating) { InputBlocker.Block(); _gating = true; }
            else if (!gate && _gating) { InputBlocker.Release(); _gating = false; }
            if (!gate) return;
            if (KeyInput.WasPressed(Key.UpArrow)) Move(+1);
            else if (KeyInput.WasPressed(Key.DownArrow)) Move(-1);
            else if (KeyInput.WasPressed(Key.LeftArrow) || KeyInput.WasPressed(Key.RightArrow)) Switch();
        }

        private static void Switch()
        {
            _buffer = 1 - _buffer;
            if (_buffer == 0) RefreshItem();
            else _eventCursor = -1;
            var list = _buffer == 0 ? Item : Events;
            Speech.Say(Loc.F("buf.count", Loc.T(_buffer == 0 ? "buf.item" : "buf.events"), list.Count), true);
        }

        private static void Move(int dir)
        {
            if (_buffer == 0)
            {
                RefreshItem();
                if (Item.Count == 0) { Speech.Say(Loc.T("buf.empty"), true); return; }
                int next = _itemCursor + dir;
                if (next < 0) { Speech.Say(Loc.T("buf.top") + ". " + Item[0], true); return; }
                if (next >= Item.Count) { Speech.Say(Loc.T("buf.end") + ". " + Item[_itemCursor], true); return; }
                _itemCursor = next;
                Speech.Say(Item[_itemCursor], true);
            }
            else
            {
                if (Events.Count == 0) { Speech.Say(Loc.T("buf.empty"), true); return; }
                int next = _eventCursor + dir;
                if (next < 0) { _eventCursor = 0; Speech.Say(Loc.T("buf.top") + ". " + Events[0], true); return; }
                if (next >= Events.Count) { Speech.Say(Loc.T("buf.end") + ". " + Events[_eventCursor], true); return; }
                _eventCursor = next;
                Speech.Say(Events[_eventCursor], true);
            }
        }

        /// <summary>Rebuilds the item buffer when the focused thing (or scene) changed; keeps the cursor otherwise.</summary>
        private static void RefreshItem()
        {
            var details = PictureDescriber.DetailLines();
            string head = Speech.LastFocus;
            bool scene = SceneDescriber.CurrentSceneText() != null || Gameplay.StageWatcher.InStage;
            var lines = new List<string>();
            if (!scene && !string.IsNullOrEmpty(head)) lines.Add(head);
            foreach (var d in details) if (!lines.Contains(d)) lines.Add(d);
            string key = string.Join("\n", lines);
            if (key == _itemKey) return;
            _itemKey = key;
            Item.Clear();
            Item.AddRange(lines);
            _itemCursor = 0;
            // A scene or stage has no focus line: start before the first detail so Alt+Up reads it.
            if (scene || string.IsNullOrEmpty(head)) _itemCursor = -1;
        }
    }
}
