using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using RiftAccess.Core.Output;
using UnityEngine;

namespace RiftAccess.Core
{
    /// <summary>
    /// The only speech entry point. Say = focus echoes and on-demand readouts; SayEvent = unsolicited
    /// announcements, also written to the event log next to the plugin. Every spoken line goes to the speech log.
    /// </summary>
    internal static class Speech
    {
        private static PrismSpeech _prism;
        private static StreamWriter _speechLog;
        private static StreamWriter _eventLog;
        private static int _mainThreadId;
        private static readonly ConcurrentQueue<string> Pending = new ConcurrentQueue<string>();
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);
        private static string _lastEvent;
        private static float _lastEventTime;

        public static string LastSpoken { get; private set; } = "";

        public static void Initialize(string pluginDir)
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _prism = new PrismSpeech();
            _prism.Initialize(pluginDir, ModConfig.SpeechBackend?.Value);
            try
            {
                _speechLog = new StreamWriter(Path.Combine(pluginDir, "RiftAccess_speech.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                _eventLog = new StreamWriter(Path.Combine(pluginDir, "RiftAccess_events.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch (Exception ex) { Plugin.LogWarning("Could not open speech logs: " + ex.Message); }
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = Tags.Replace(text, " ");
            s = s.Replace(" ", " ").Replace("​", "");
            return Spaces.Replace(s, " ").Trim();
        }

        public static void Say(string text, bool interrupt = false)
        {
            string clean = Clean(text);
            if (clean.Length == 0) return;
            LastSpoken = clean;
            Recent.Add(new System.Collections.Generic.KeyValuePair<float, string>(Time.unscaledTime, clean));
            try { _speechLog?.WriteLine(Time.realtimeSinceStartup.ToString("0.00") + (interrupt ? " ! " : " | ") + clean); } catch { }
            if (_prism == null) return;
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                Pending.Enqueue((interrupt ? "!" : " ") + clean);
                return;
            }
            _prism.Speak(clean, interrupt, ModConfig.Braille == null || ModConfig.Braille.Value);
        }

        private static string _lastFocus = "";

        /// <summary>The last focus echo, for the review buffer's line 0.</summary>
        public static string LastFocus => _lastFocus;
        private static float _lastFocusTime = -10f;

        /// <summary>Focus echo while navigating menus: interrupts only if the player chose that.</summary>
        public static void SayFocus(string text)
        {
            _lastFocus = Clean(text);
            _lastFocusTime = Time.unscaledTime;
            Say(text, ModConfig.InterruptOnNavigation.Value);
        }

        /// <summary>True if this text was part of a focus announcement in the last two seconds.</summary>
        public static bool RecentlyFocused(string text)
        {
            return Time.unscaledTime - _lastFocusTime < 2f && text.Length > 0 && _lastFocus.IndexOf(text, StringComparison.Ordinal) >= 0;
        }

        private static readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>> Recent =
            new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<float, string>>();

        /// <summary>True if this text was already said in the last few seconds (by any reader).</summary>
        public static bool RecentlySaid(string text)
        {
            if (text.Length == 0) return false;
            float now = Time.unscaledTime;
            Recent.RemoveAll(kv => now - kv.Key > 4f);
            foreach (var kv in Recent) if (kv.Value.IndexOf(text, StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Unsolicited announcement. Duplicates within a short window are dropped.</summary>
        public static void SayEvent(string text)
        {
            string clean = Clean(text);
            if (clean.Length == 0) return;
            float now = Time.unscaledTime;
            if (clean == _lastEvent && now - _lastEventTime < 0.75f) return;
            _lastEvent = clean;
            _lastEventTime = now;
            try { _eventLog?.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + clean); } catch { }
            RiftAccess.Menus.ReviewBuffers.AddEvent(clean);
            Say(clean);
        }

        public static void Stop() => _prism?.Stop();

        public static void Update()
        {
            if (_prism == null) return;
            while (Pending.TryDequeue(out var line))
                _prism.Speak(line.Substring(1), line[0] == '!', ModConfig.Braille.Value);
            _prism.Poll();
        }

        public static void Shutdown()
        {
            try { _prism?.Shutdown(); } catch { }
            _prism = null;
            try { _speechLog?.Dispose(); _eventLog?.Dispose(); } catch { }
        }
    }
}
