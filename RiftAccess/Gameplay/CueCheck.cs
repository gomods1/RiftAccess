using System;
using System.Collections.Generic;
using RiftAccess.Core;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// Debug measurement of the lane cues (on while Debug logging is on). For every hit the game opens a window
    /// for, it records whether a main cue sounded before it, in the right lane, and how long before the hit
    /// (compared with the cue lead). The totals are logged and spoken at the end of each stage.
    /// </summary>
    internal static class CueCheck
    {
        public static bool Enabled => ModConfig.DebugLogging.Value;

        private static int _hits, _ok, _wrongLane, _missing, _extra;
        private static double _leadErrorSum;
        private static float _leadErrorMax;
        private static readonly Dictionary<string, int> Problems = new Dictionary<string, int>();

        public static void Reset()
        {
            _hits = _ok = _wrongLane = _missing = _extra = 0;
            _leadErrorSum = 0;
            _leadErrorMax = 0;
            Problems.Clear();
        }

        public static void Record(string enemy, float beat, int lane, int cueLane, float leadSeconds, float expectedLead)
        {
            _hits++;
            if (cueLane < 0)
            {
                _missing++;
                Count(enemy + " missing");
                Plugin.LogInfo($"CueCheck MISSING {enemy} beat {beat:0.00} lane {lane}");
                return;
            }
            if (cueLane != lane)
            {
                _wrongLane++;
                Count(enemy + " wrong lane");
                Plugin.LogInfo($"CueCheck WRONG LANE {enemy} beat {beat:0.00} lane {lane} cue {cueLane}");
                return;
            }
            _ok++;
            float err = Math.Abs(leadSeconds - expectedLead);
            _leadErrorSum += err;
            if (err > _leadErrorMax) _leadErrorMax = err;
            if (err > 0.03f) Plugin.LogInfo($"CueCheck late/early {enemy} beat {beat:0.00}: lead {leadSeconds * 1000f:0} ms, expected {expectedLead * 1000f:0} ms");
        }

        /// <summary>A cue the player heard that did not lead to a hit in its lane (besides the counted one).</summary>
        public static void RecordExtra(string enemy, float beat, int lane, string what)
        {
            _extra++;
            Count(what);
            Plugin.LogInfo($"CueCheck EXTRA {enemy} beat {beat:0.00} lane {lane}: {what}");
        }

        private static void Count(string what)
        {
            Problems.TryGetValue(what, out int n);
            Problems[what] = n + 1;
        }

        public static string Summary()
        {
            if (_hits == 0) return null;
            string s = $"Cue check: {_hits} hits, {_ok} cued correctly, {_wrongLane} wrong lane, {_missing} missing, " +
                       $"{_extra} misleading extra cues, lead error average {(_ok > 0 ? _leadErrorSum / _ok * 1000 : 0):0} ms, max {_leadErrorMax * 1000:0} ms";
            foreach (var kv in Problems) s += "; " + kv.Key + " " + kv.Value;
            return s;
        }

        /// <summary>Logs and speaks the totals of the stage that just ended.</summary>
        public static void Report()
        {
            if (!Enabled) return;
            string s = Summary();
            if (s == null) return;
            Plugin.LogInfo(s);
            Speech.Say(s);
            Reset();
        }
    }
}
