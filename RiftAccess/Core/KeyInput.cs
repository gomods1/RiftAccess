using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace RiftAccess.Core
{
    /// <summary>
    /// Key edges for the mod's own keys, computed once per frame from Keyboard.current's held state.
    /// The game's Input System update does not line up with MonoBehaviour.Update, so wasPressedThisFrame misses
    /// presses; comparing isPressed with the previous frame does not.
    /// </summary>
    internal static class KeyInput
    {
        private static readonly Key[] Watched =
        {
            Key.UpArrow, Key.DownArrow, Key.LeftArrow, Key.RightArrow, Key.Home, Key.End, Key.Tab,
            Key.Enter, Key.NumpadEnter, Key.Space, Key.Escape, Key.F1, Key.F2, Key.F10, Key.F11, Key.F12, Key.H,
        };
        private static readonly HashSet<Key> Down = new HashSet<Key>();
        private static readonly HashSet<Key> Pressed = new HashSet<Key>();

        public static bool Ctrl { get; private set; }
        public static bool Shift { get; private set; }
        public static bool Alt { get; private set; }

        public static void Update()
        {
            Pressed.Clear();
            var kb = Keyboard.current;
            if (kb == null) return;
            Ctrl = kb.ctrlKey.isPressed;
            Shift = kb.shiftKey.isPressed;
            Alt = kb.altKey.isPressed;
            foreach (var k in Watched)
            {
                bool now = kb[k].isPressed;
                if (now && !Down.Contains(k)) { Pressed.Add(k); Plugin.LogDebug("key " + k + " frame " + UnityEngine.Time.frameCount); }
                if (now) Down.Add(k); else Down.Remove(k);
            }
        }

        /// <summary>True on the frame the key went down.</summary>
        public static bool WasPressed(Key k) => Pressed.Contains(k);
    }
}
