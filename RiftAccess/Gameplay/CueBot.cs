using System.Collections.Generic;
using RiftAccess.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Debug test player (Ctrl+F11 while Debug logging is on): plays using nothing but the mod's cues, like a blind
    /// player. Each cue gives it a lane and the moment the sound starts; it presses that lane's key one cue lead
    /// later, holds hold notes until the release cue, and forgets cues the mod cancels. If it clears a song, the
    /// cues carried everything needed to play it. Keys go through the Input System like a real keyboard.
    /// </summary>
    internal static class CueBot
    {
        public static bool Active { get; private set; }

        private sealed class Press
        {
            public Key Key;
            public float At;
            public bool Hold;
            public float ReleaseAt = float.MaxValue;
            public bool Down;
        }

        private static readonly Dictionary<string, Press> Pending = new Dictionary<string, Press>();
        private static readonly List<Press> Down = new List<Press>();
        private static readonly HashSet<Key> Held = new HashSet<Key>();
        private static readonly List<string> Done = new List<string>();

        public static void Toggle()
        {
            Active = !Active;
            Pending.Clear();
            ReleaseAll();
            Speech.Say(Active ? "Cue bot on" : "Cue bot off", true);
            Plugin.LogInfo("Cue bot " + (Active ? "on" : "off"));
        }

        private static Key LaneKey(int lane, int columns)
        {
            if (columns != 3) return Key.None;
            return lane == 0 ? Key.LeftArrow : lane == 1 ? Key.UpArrow : lane == 2 ? Key.RightArrow : Key.None;
        }

        /// <summary>A cue has been scheduled: lane, kind and the moment its key is due.</summary>
        public static void OnCue(string id, int lane, int columns, string kind, float pressAt)
        {
            if (!Active) return;
            var key = LaneKey(lane, columns);
            if (key == Key.None) return;
            if (kind == "release")
            {
                foreach (var p in Down) if (p.Key == key && p.Hold) p.ReleaseAt = pressAt;
                foreach (var p in Pending.Values) if (p.Key == key && p.Hold) p.ReleaseAt = pressAt;
                return;
            }
            Pending[id] = new Press { Key = key, At = pressAt, Hold = kind == "hold" };
        }

        /// <summary>The vibe ready chime played: press Space shortly after, as a player would.</summary>
        public static void OnVibeReady()
        {
            if (Active) Pending["vibe" + Time.frameCount] = new Press { Key = Key.Space, At = Time.unscaledTime + 0.3f };
        }

        public static void OnCancel(string id)
        {
            if (Active) Pending.Remove(id);
        }

        /// <summary>Boss battles and minigames: a direction is due at pressAt.</summary>
        public static void OnDirection(string id, Key key, float pressAt, float holdSeconds)
        {
            if (!Active || key == Key.None) return;
            var p = new Press { Key = key, At = pressAt, Hold = holdSeconds > 0f };
            if (p.Hold) p.ReleaseAt = pressAt + holdSeconds;
            Pending[id] = p;
        }

        public static void Update()
        {
            if (!Active) return;
            float now = Time.unscaledTime;
            bool changed = false;
            Done.Clear();
            // All three lanes at the same moment: one press of Down (the game's "hit all three" key), as the
            // mod's tutorial text tells players.
            Press left = null, mid = null, right = null;
            foreach (var p in Pending.Values)
            {
                if (now < p.At || p.Hold) continue;
                if (p.Key == Key.LeftArrow) left = p; else if (p.Key == Key.UpArrow) mid = p; else if (p.Key == Key.RightArrow) right = p;
            }
            if (left != null && mid != null && right != null && Mathf.Abs(left.At - mid.At) < 0.03f && Mathf.Abs(right.At - mid.At) < 0.03f)
            {
                left.Key = Key.DownArrow;
                foreach (var kv in Pending) if (kv.Value == mid || kv.Value == right) Done.Add(kv.Key);
            }
            foreach (var kv in Pending)
            {
                var p = kv.Value;
                if (now < p.At || Done.Contains(kv.Key)) continue;
                Done.Add(kv.Key);
                if (now - p.At > 0.15f) continue; // missed its moment (pause, hitch): skip like a player would
                p.Down = true;
                if (!p.Hold) p.ReleaseAt = now + 0.07f;
                else if (p.ReleaseAt == float.MaxValue) p.ReleaseAt = now + 12f;
                Down.Add(p);
                Held.Add(p.Key);
                changed = true;
            }
            foreach (var k in Done) Pending.Remove(k);
            for (int i = Down.Count - 1; i >= 0; i--)
            {
                if (now < Down[i].ReleaseAt) continue;
                var key = Down[i].Key;
                Down.RemoveAt(i);
                if (!Down.Exists(d => d.Key == key)) { Held.Remove(key); changed = true; }
            }
            if (changed) Send();
        }

        private static void ReleaseAll()
        {
            Down.Clear();
            if (Held.Count == 0) return;
            Held.Clear();
            Send();
        }

        private static void Send()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            var keys = new Key[Held.Count];
            Held.CopyTo(keys);
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
        }
    }
}
