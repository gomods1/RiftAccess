using System;
using System.Collections.Generic;
using System.IO;
using FMODUnity;
using RiftAccess.Core;

namespace RiftAccess.Audio
{
    /// <summary>
    /// Plays the mod's cue sounds through FMOD's core API (the game uses FMOD; Unity audio is not part of its mix).
    /// Sounds are WAV files in the plugin's Sounds folder. A cue can start at an exact time in the future: it is
    /// scheduled on FMOD's DSP clock, so it lands sample-accurately however late in the frame it was requested.
    /// </summary>
    internal static class CuePlayer
    {
        private static readonly Dictionary<string, FMOD.Sound> Sounds = new Dictionary<string, FMOD.Sound>();
        private static readonly List<Scheduled> Pending = new List<Scheduled>();
        private static FMOD.ChannelGroup _group;
        private static int _sampleRate = 48000;
        private static bool _ready;
        private static float _nextAttempt;

        public sealed class Scheduled
        {
            public FMOD.Channel Channel;
            public ulong StartClock;
            public bool Started;
        }

        public static bool TryInitialize()
        {
            if (_ready) return true;
            if (UnityEngine.Time.unscaledTime < _nextAttempt) return false;
            _nextAttempt = UnityEngine.Time.unscaledTime + 1f;
            try
            {
                if (!RuntimeManager.IsInitialized) return false;
                var core = RuntimeManager.CoreSystem;
                core.getSoftwareFormat(out _sampleRate, out _, out _);
                Check(core.createChannelGroup("RiftAccessCues", out _group), "createChannelGroup");
                Check(core.getMasterChannelGroup(out var master), "getMasterChannelGroup");
                Check(master.addGroup(_group), "addGroup");

                string dir = Path.Combine(Plugin.PluginDir, "Sounds");
                if (Directory.Exists(dir))
                {
                    foreach (string file in Directory.GetFiles(dir, "*.wav"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                        var r = core.createSound(file, FMOD.MODE.DEFAULT | FMOD.MODE.CREATESAMPLE | FMOD.MODE.LOOP_OFF | FMOD.MODE._2D, out var sound);
                        if (r == FMOD.RESULT.OK) Sounds[name] = sound;
                        else Plugin.LogWarning("Could not load cue " + file + ": " + r);
                    }
                }
                else Plugin.LogWarning("Sounds folder missing: " + dir);
                _ready = true;
                Plugin.LogInfo("Cue player ready: " + Sounds.Count + " sounds, " + _sampleRate + " Hz.");
            }
            catch (Exception ex)
            {
                Plugin.LogError("Cue player init failed: " + ex.Message);
            }
            return _ready;
        }

        private static void Check(FMOD.RESULT r, string what)
        {
            if (r != FMOD.RESULT.OK) throw new Exception(what + " returned " + r);
        }

        public static bool Has(string name) => Sounds.ContainsKey(name);

        /// <summary>
        /// Plays a cue after delaySeconds (0 = now). pan -1..1, volume 0..2, pitch as a playback-rate multiplier.
        /// Returns a handle that can be cancelled until the cue starts, or null if nothing was played.
        /// </summary>
        public static Scheduled Play(string name, float delaySeconds = 0f, float pan = 0f, float volume = 1f, float pitch = 1f)
        {
            if (!_ready || !Sounds.TryGetValue(name, out var sound)) return null;
            try
            {
                var core = RuntimeManager.CoreSystem;
                if (core.playSound(sound, _group, true, out var ch) != FMOD.RESULT.OK) return null;
                ch.setPan(Math.Max(-1f, Math.Min(1f, pan)));
                ch.setVolume(volume);
                if (Math.Abs(pitch - 1f) > 0.001f) ch.setPitch(pitch);
                var s = new Scheduled { Channel = ch };
                if (delaySeconds > 0.001f)
                {
                    _group.getDSPClock(out _, out ulong parentClock);
                    s.StartClock = parentClock + (ulong)(delaySeconds * _sampleRate);
                    ch.setDelay(s.StartClock, 0, false);
                    Pending.Add(s);
                }
                else s.Started = true;
                ch.setPaused(false);
                return s;
            }
            catch (Exception ex)
            {
                Plugin.LogDebug("Play " + name + " failed: " + ex.Message);
                return null;
            }
        }

        /// <summary>Stops a scheduled cue if it has not started yet. Returns true if it was cancelled.</summary>
        public static bool Cancel(Scheduled s)
        {
            if (s == null || s.Started) return false;
            UpdateStarted(s);
            if (s.Started) return false;
            try { s.Channel.stop(); } catch { }
            Pending.Remove(s);
            return true;
        }

        /// <summary>Stops every cue that has not started yet (pause, stage end).</summary>
        public static void CancelAllPending()
        {
            foreach (var s in Pending)
            {
                UpdateStarted(s);
                if (!s.Started) { try { s.Channel.stop(); } catch { } }
            }
            Pending.Clear();
        }

        public static void StopAll()
        {
            Pending.Clear();
            if (_ready) { try { _group.stop(); } catch { } }
        }

        private static void UpdateStarted(Scheduled s)
        {
            if (s.Started) return;
            try
            {
                _group.getDSPClock(out _, out ulong parentClock);
                if (parentClock >= s.StartClock) s.Started = true;
            }
            catch { s.Started = true; }
        }

        public static void Update()
        {
            if (!_ready || Pending.Count == 0) return;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                UpdateStarted(Pending[i]);
                if (Pending[i].Started) Pending.RemoveAt(i);
            }
        }

        public static void Shutdown()
        {
            try
            {
                foreach (var s in Sounds.Values) s.release();
                Sounds.Clear();
                if (_ready) _group.release();
            }
            catch { }
            _ready = false;
        }
    }
}
