using System;
using System.Collections.Generic;
using HarmonyLib;
using RhythmRift;
using RhythmRift.Enemies;
using RiftAccess.Audio;
using RiftAccess.Core;
using Shared.Audio;
using Shared.RhythmEngine;

namespace RiftAccess.Gameplay
{
    /// <summary>
    /// The lane cues: for every enemy the player must hit, a sound in that enemy's lane (pan and pitch) played
    /// LeadBeats before the hit, so the player hears the pattern and answers it on the beat.
    ///
    /// Source of truth is the game's own prediction, the same one it uses to pre-queue its hit sounds
    /// (RREnemyController.TryQueueActionRowSounds): RREnemy.NextActionRowTrueBeatNumber for the next hit,
    /// ExpectedFollowUpActionTrueBeatNumber for the next hit of a multi-hit enemy, TargetGridPosition.x for the lane,
    /// ShouldSpoofInputForEnemy for "needs an input" (what autoplay uses). The beat-to-seconds conversion is the game's
    /// ComputeAudioQueueDelay (tempo changes, song speed), with the same latency correction AudioManager applies.
    /// Runs as a postfix of RREnemyController.UpdateSystem, which is the frame's authoritative time capsule.
    /// </summary>
    internal static class LaneCues
    {
        /// <summary>Cues are handed to FMOD this long before they must sound (they are then sample-accurate).</summary>
        private const float ScheduleHorizon = 0.2f;

        private sealed class PendingCue
        {
            public CuePlayer.Scheduled Handle;
            public float TargetBeat;
            public int Lane;
            public string Key;
            public float StartTime;
        }

        private sealed class EnemyState
        {
            public string Id;
            public readonly HashSet<string> Done = new HashSet<string>();
            public readonly List<PendingCue> Pending = new List<PendingCue>();
            /// <summary>Main cues handed to FMOD and not cancelled: what the player heard (for the cue check).</summary>
            public readonly List<PendingCue> Played = new List<PendingCue>();
            public readonly HashSet<int> Checked = new HashSet<int>();
            public int Frame;
            /// <summary>The hold tone, playing while the player keeps a hold note's key down.</summary>
            public FMOD.Channel? HoldTone;

            public void StopHoldTone()
            {
                if (HoldTone == null) return;
                CuePlayer.Stop(HoldTone);
                HoldTone = null;
            }
        }

        private static AccessTools.FieldRef<RREnemyController, List<RREnemy>> _activeEnemies;
        private static AccessTools.FieldRef<RREnemyController, IRRGridDataAccessor> _grid;
        private static Func<RREnemyController, FmodTimeCapsule, float, float> _queueDelay;
        private static Func<RREnemy, int> _enemyLength;

        private static readonly Dictionary<string, EnemyState> States = new Dictionary<string, EnemyState>();
        private static readonly HashSet<int> SeenTypes = new HashSet<int>();
        private static readonly List<string> Gone = new List<string>();
        private static RREnemyController _controller;
        private static float _lastBeat = float.NegativeInfinity;
        private static int _frame;

        public static RREnemyController Controller => _controller;
        public static FmodTimeCapsule LastCapsule { get; private set; }
        public static float LastUpdateTime { get; private set; } = -100f;

        public static void TryPatch(Harmony harmony)
        {
            try
            {
                _activeEnemies = AccessTools.FieldRefAccess<RREnemyController, List<RREnemy>>("_activeEnemies");
                _grid = AccessTools.FieldRefAccess<RREnemyController, IRRGridDataAccessor>("_tileGridAccessor");
                var delay = AccessTools.Method(typeof(RREnemyController), "ComputeAudioQueueDelay");
                if (delay != null) _queueDelay = AccessTools.MethodDelegate<Func<RREnemyController, FmodTimeCapsule, float, float>>(delay);
                var len = AccessTools.PropertyGetter(typeof(RREnemy), "EnemyLength");
                if (len != null) _enemyLength = AccessTools.MethodDelegate<Func<RREnemy, int>>(len);

                var update = AccessTools.Method(typeof(RREnemyController), nameof(RREnemyController.UpdateSystem));
                harmony.Patch(update, postfix: new HarmonyMethod(typeof(LaneCues), nameof(UpdateSystemPostfix)));
                var pause = AccessTools.Method(typeof(AudioManager), nameof(AudioManager.SetGamePaused));
                harmony.Patch(pause, postfix: new HarmonyMethod(typeof(LaneCues), nameof(OnPaused)));
                Plugin.LogInfo("Patched RREnemyController.UpdateSystem and AudioManager.SetGamePaused (lane cues)");
            }
            catch (Exception ex) { Plugin.LogError("Lane cues not available: " + ex); }
        }

        public static void Update()
        {
            // The controller stops updating when a stage ends or the scene changes: forget it.
            if (!ReferenceEquals(_controller, null) && _controller == null)
            {
                Reset();
                _controller = null;
            }
        }

        private static void Reset()
        {
            CuePlayer.CancelAllPending();
            foreach (var st in States.Values) st.StopHoldTone();
            States.Clear();
            SeenTypes.Clear();
            _lastBeat = float.NegativeInfinity;
        }

        private static void OnPaused()
        {
            try
            {
                // Unstarted cues would sound over the pause menu; drop them and let them be scheduled again later.
                foreach (var st in States.Values)
                {
                    st.StopHoldTone();
                    foreach (var p in st.Pending)
                        if (CuePlayer.Cancel(p.Handle)) st.Done.Remove(p.Key);
                    st.Pending.Clear();
                }
            }
            catch (Exception ex) { Plugin.LogError("Lane cue pause: " + ex.Message); }
        }

        private static void UpdateSystemPostfix(RREnemyController __instance, FmodTimeCapsule fmodTimeCapsule)
        {
            try { Run(__instance, fmodTimeCapsule); }
            catch (Exception ex) { Plugin.LogError("Lane cues: " + ex); }
        }

        private static void Run(RREnemyController controller, FmodTimeCapsule ftc)
        {
            float now = ftc.TrueBeatNumber;
            if (controller != _controller || now < _lastBeat - 1f)
            {
                Reset();
                _controller = controller;
                StageWatcher.OnStageStarted();
            }
            _lastBeat = now;
            LastCapsule = ftc;
            LastUpdateTime = UnityEngine.Time.unscaledTime;
            _frame++;

            var enemies = _activeEnemies(controller);
            int columns = 3;
            try { var g = _grid(controller); if (g != null && g.NumColumns > 0) columns = g.NumColumns; } catch { }

            for (int i = 0; i < enemies.Count; i++)
            {
                RREnemy e = enemies[i];
                if (e == null) continue;
                string id = e.EnemyId;
                if (!States.TryGetValue(id, out var st)) { st = new EnemyState { Id = id }; States[id] = st; }
                st.Frame = _frame;

                if (e.IsDying || !e.ShouldSpoofInputForEnemy() || !e.ShouldQueueSounds()) { st.StopHoldTone(); Targets.Clear(); CancelStale(st, Targets, -1); continue; }
                if (CueCheck.Enabled) CheckHitWindow(controller, ftc, e, st);

                if (SeenTypes.Add(e.EnemyTypeId) && StageWatcher.SpeechAllowed)
                {
                    if (ModConfig.AnnounceNewEnemies.Value) Speech.SayEvent(RuleWatcher.EnemyName(e.DisplayName));
                    // The first hold note of the session gets a reminder of how holds sound.
                    if (e.IsHoldNote && ModConfig.CuesEnabled.Value && !_holdHintGiven)
                    {
                        _holdHintGiven = true;
                        Speech.SayEvent(Loc.T("hint.hold"));
                    }
                }

                if (!ModConfig.CuesEnabled.Value) continue;
                int lane = PredictLane(e, columns);

                if (e.IsHoldNote && e.IsBeingHeld)
                {
                    Targets.Clear();
                    CancelStale(st, Targets, lane);
                    float end = e.GetLastHoldBeatNumber();
                    if (st.HoldTone == null && ModConfig.HoldTone.Value && IsFinite(end) && end > now)
                    {
                        // From the press until the release beat (or until the key comes up early).
                        Place(lane, columns, out float pan, out _, out float pitch);
                        float stop = SecondsUntil(controller, ftc, end) + ModConfig.TimingOffsetMs.Value / 1000f
                                     - (LatencyManager.AudioLatencyOffset - LatencyManager.VideoLatencyOffset);
                        if (stop > 0.05f)
                        {
                            st.HoldTone = CuePlayer.PlayLoop("hold_loop", 0f, stop, pan, ModConfig.CueVolume.Value / 100f * 0.7f, pitch * LoopPitch(lane, columns));
                            Plugin.LogDebug($"hold tone lane={lane} for {stop:0.000}s ({st.Id})");
                        }
                    }
                    if (ModConfig.HoldReleaseCue.Value && end > now)
                        Consider(controller, ftc, st, "rel", end, 0f, lane, columns, "release", 1f);
                    continue;
                }
                st.StopHoldTone();

                // This hit and every further hit the enemy still needs: the game gives the spacing of the next one
                // (ExpectedFollowUpActionTrueBeatNumber); the rest follow at the same spacing, one per remaining health.
                // Under a "Bane" modifier the game itself hits a damaged monster's follow-ups: cue only the first hit.
                bool autoFollow = RuleWatcher.AutoHitsFollowUps(e);
                float p = e.NextActionRowTrueBeatNumber;
                Targets.Clear();
                if (IsFinite(p) && !(autoFollow && e.CurrentHealthValue < e.MaxHealthValue)) Targets.Add(new KeyValuePair<float, int>(p, lane));
                if (IsFinite(p) && !autoFollow && ModConfig.FollowUpCues.Value && e.IsExpectingFollowUpAction && !e.IsHoldNote)
                {
                    float spacing = e.ExpectedFollowUpActionTrueBeatNumber - p;
                    if (IsFinite(spacing) && spacing > 0.05f)
                    {
                        int more = Math.Min(Math.Max(1, e.CurrentHealthValue - 1), 8);
                        for (int k = 1; k <= more; k++)
                            Targets.Add(new KeyValuePair<float, int>(p + k * spacing, PredictFollowLane(e, columns, k)));
                    }
                }
                CancelStale(st, Targets, lane);

                string kind = e.IsHoldNote ? "hold" : (e.IsHealthItem && ModConfig.HealthItemCue.Value ? "heart" : "tap");
                foreach (var t in Targets)
                    if (t.Key > now) CueHit(controller, ftc, st, t.Key, t.Value, columns, kind);
            }

            // Forget enemies that left the board.
            Gone.Clear();
            foreach (var kv in States) if (kv.Value.Frame != _frame) Gone.Add(kv.Key);
            foreach (var id in Gone)
            {
                States[id].StopHoldTone();
                if (CueCheck.Enabled) CheckPhantoms(States[id], float.PositiveInfinity);
                States.Remove(id);
            }
            if (CueCheck.Enabled) foreach (var st in States.Values) CheckPhantoms(st, now);
        }

        private static void CueHit(RREnemyController c, FmodTimeCapsule ftc, EnemyState st, float hitBeat, int lane, int columns, string kind)
        {
            float lead = Math.Max(0f, ModConfig.LeadBeats.Value);
            Consider(c, ftc, st, "main", hitBeat, lead, lane, columns, kind, 1f);
            int count = ModConfig.CountIn.Value;
            for (int k = 1; k <= count; k++)
                Consider(c, ftc, st, "count" + k, hitBeat, lead + k, lane, columns, "tap", 0.5f);
            if (ModConfig.HitClick.Value)
                Consider(c, ftc, st, "click", hitBeat, 0f, lane, columns, "click", 0.6f);
        }

        /// <summary>Schedules one cue for (enemy, target beat, role) once, when it comes within the horizon.</summary>
        private static void Consider(RREnemyController c, FmodTimeCapsule ftc, EnemyState st, string role, float targetBeat,
                                     float leadBeats, int lane, int columns, string kind, float volumeScale)
        {
            string key = role + "|" + Math.Round(targetBeat * 24f);
            if (st.Done.Contains(key)) return;

            float cueBeat = targetBeat - leadBeats;
            float delay = SecondsUntil(c, ftc, cueBeat) + ModConfig.TimingOffsetMs.Value / 1000f
                          - (LatencyManager.AudioLatencyOffset - LatencyManager.VideoLatencyOffset);
            if (delay > ScheduleHorizon) return;
            st.Done.Add(key);

            if (delay < -0.04f)
            {
                // Too late for its slot (enemy appeared or changed course close by). Only the main cue is still
                // worth playing, and only if it comes clearly before the hit.
                if (role != "main" || SecondsUntil(c, ftc, targetBeat) < 0.18f) return;
                delay = 0f;
            }

            Place(lane, columns, out float pan, out string side, out float pitch);
            string sound = kind == "click" ? "click" : kind + "_" + side;
            float vol = ModConfig.CueVolume.Value / 100f * volumeScale;

            var handle = CuePlayer.Play(sound, Math.Max(0f, delay), pan, vol, pitch);
            float start = UnityEngine.Time.unscaledTime + Math.Max(0f, delay);
            var pc = new PendingCue { Handle = handle, TargetBeat = targetBeat, Lane = lane, Key = key, StartTime = start };
            if (handle != null && !handle.Started) st.Pending.Add(pc);
            if (role == "main" || role == "rel")
            {
                if (role == "main") st.Played.Add(pc);
                // What a player knows from this sound: the lane, and that the key is due one lead after it starts.
                CueBot.OnCue(st.Id + key, lane, columns, kind, start + (SecondsUntil(c, ftc, targetBeat) - Math.Max(0f, delay)));
            }
            Plugin.LogDebug($"cue {sound} {role} hit={targetBeat:0.00} lane={lane} in {delay:0.000}s ({st.Id})");
        }

        /// <summary>Where a lane sounds: pan, which lane file (left, mid, right) and an extra pitch for boards wider
        /// than three lanes (outer lanes two semitones beyond the side sounds).</summary>
        private static void Place(int lane, int columns, out float pan, out string side, out float pitch)
        {
            float rel = lane - (columns - 1) / 2f;
            float half = Math.Max(0.5f, (columns - 1) / 2f);
            pan = rel / half * (ModConfig.PanWidth.Value / 100f);
            side = !ModConfig.LanePitch.Value ? "mid" : rel < -0.01f ? "left" : rel > 0.01f ? "right" : "mid";
            pitch = 1f;
            if (ModConfig.LanePitch.Value && Math.Abs(rel) > 1.01f) pitch = (float)Math.Pow(2, Math.Sign(rel) * 2 / 12.0);
        }

        /// <summary>The hold tone is one file (the middle lane's note): a fifth down on the left, a fifth up on the right.</summary>
        internal static float LoopPitch(int lane, int columns)
        {
            if (!ModConfig.LanePitch.Value) return 1f;
            float rel = lane - (columns - 1) / 2f;
            return rel < -0.01f ? (float)Math.Pow(2, -7 / 12.0) : rel > 0.01f ? (float)Math.Pow(2, 7 / 12.0) : 1f;
        }

        private static bool _holdHintGiven;

        private static readonly List<KeyValuePair<float, int>> Targets = new List<KeyValuePair<float, int>>();

        /// <summary>Cancels unstarted cues whose enemy changed lane or no longer needs a hit at that beat.</summary>
        private static void CancelStale(EnemyState st, List<KeyValuePair<float, int>> targets, int lane)
        {
            for (int i = st.Pending.Count - 1; i >= 0; i--)
            {
                var pc = st.Pending[i];
                if (pc.Handle.Started) { st.Pending.RemoveAt(i); continue; }
                bool ok = pc.Key.StartsWith("rel") && pc.Lane == lane;
                foreach (var t in targets) if (Near(pc.TargetBeat, t.Key) && pc.Lane == t.Value) ok = true;
                if (ok) continue;
                if (CuePlayer.Cancel(pc.Handle))
                {
                    Plugin.LogDebug($"cancel {pc.Key} lane={pc.Lane} ({st.Id})");
                    st.Done.Remove(pc.Key);
                    st.Played.Remove(pc);
                    CueBot.OnCancel(st.Id + pc.Key);
                }
                st.Pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// Cue check (debug): when an enemy's hit window opens, a main cue for that hit must already have sounded,
        /// in the lane the enemy is really in. Counts go to CueCheck and the log.
        /// </summary>
        private static void CheckHitWindow(RREnemyController c, FmodTimeCapsule ftc, RREnemy e, EnemyState st)
        {
            if (!e.IsHitWindowActive) return;
            float target = e.TargetHitBeatNumber;
            if (!IsFinite(target)) return;
            int q = (int)Math.Round(target * 24f);
            if (!st.Checked.Add(q)) return;
            int lane = e.TargetGridPosition.x;
            PendingCue heard = null;
            int wrong = 0, count = 0;
            for (int i = st.Played.Count - 1; i >= 0; i--)
            {
                var pc = st.Played[i];
                if (!Near(pc.TargetBeat, target) || pc.StartTime > UnityEngine.Time.unscaledTime + 0.02f) continue;
                if (heard == null || pc.StartTime > heard.StartTime) heard = pc;
                count++;
                if (pc.Lane != lane) wrong++;
                st.Played.RemoveAt(i);
            }
            // A corrected cue still leaves the earlier one in the player's ears.
            if (heard != null && heard.Lane == lane && wrong > 0) CueCheck.RecordExtra(e.DisplayName, target, lane, "earlier cue in another lane");
            else if (count > 1 && wrong == 0) CueCheck.RecordExtra(e.DisplayName, target, lane, "cued " + count + " times");
            float hitTime = UnityEngine.Time.unscaledTime + SecondsUntil(c, ftc, target);
            float expectedLead = SecondsUntil(c, ftc, target) - SecondsUntil(c, ftc, target - Math.Max(0f, ModConfig.LeadBeats.Value));
            CueCheck.Record(e.DisplayName, target, lane, heard?.Lane ?? -1, heard != null ? hitTime - heard.StartTime : -1f, expectedLead);
        }

        /// <summary>Cue check: main cues that sounded for a hit whose window never opened (the enemy came at
        /// another beat, or left). A hit window takes its cues out of Played, so what is left after the beat is extra.</summary>
        private static void CheckPhantoms(EnemyState st, float now)
        {
            float t = UnityEngine.Time.unscaledTime;
            for (int i = st.Played.Count - 1; i >= 0; i--)
            {
                var pc = st.Played[i];
                if (pc.StartTime > t + 0.02f) { if (float.IsPositiveInfinity(now)) st.Played.RemoveAt(i); continue; }
                if (pc.TargetBeat > now - 0.5f) continue;
                CueCheck.RecordExtra(st.Id, pc.TargetBeat, pc.Lane, "cue for a hit that never came");
                st.Played.RemoveAt(i);
            }
        }

        private static readonly System.Reflection.FieldInfo SkullSideways = AccessTools.Field(typeof(RRSkullEnemy), "_moveSidewaysWhenHit");

        /// <summary>
        /// The lane the enemy will be in when it reaches the action row. The game decides each move one beat ahead
        /// (TargetGridPosition); for the moves after that the enemy's own rules are applied: zombies step one lane
        /// diagonally per row, bouncing off the edges (or wrapping). Further hits: PredictFollowLane. Collisions can
        /// still change a lane at the last moment; a cue that has not started yet is then moved.
        /// </summary>
        private static int PredictLane(RREnemy e, int columns)
        {
            int x = e.TargetGridPosition.x;
            bool wrap = e.ShouldWrapAroundGrid;
            int dir = e.IsFacingLeft ? -1 : 1;
            if (HitMoveUndecided(e) && MovesSidewaysWhenHit(e, out _)) return Step(x, ref dir, columns, wrap);
            if (e is RRZombieEnemy && !e.IsBeingPushed && !e.IsBeingTeleported && !e.IsHitWindowActive && !e.IsPerformingHitMovement)
            {
                int rows = e.TargetGridPosition.y - RRGridView.HOME_ROW_COORD.y;
                for (int i = 0; i < rows && i < 16; i++) x = Step(x, ref dir, columns, wrap);
            }
            return x;
        }

        /// <summary>
        /// Hit before its beat, an enemy first snaps onto the action row for a few frames and only then gets its hit
        /// move (bats and sideways skulls: one lane over, one row back). Until then TargetGridPosition is still the
        /// lane it was hit in, which is not where its next hit comes. After the move is decided the target is a row
        /// behind the action row.
        /// </summary>
        private static bool HitMoveUndecided(RREnemy e) =>
            e.IsPerformingHitMovement && e.TargetGridPosition.y == RRGridView.HOME_ROW_COORD.y;

        private static readonly System.Reflection.FieldInfo BatFlips = AccessTools.Field(typeof(RRBatEnemy), "_changeDirectionOnHit");

        /// <summary>Bats and sideways skulls move one lane per hit; red bats (flips) turn around after each.</summary>
        private static bool MovesSidewaysWhenHit(RREnemy e, out bool flips)
        {
            flips = false;
            if (e is RRBatEnemy)
            {
                if (BatFlips != null) { try { flips = (bool)BatFlips.GetValue(e); } catch { } }
                return true;
            }
            if (e is RRSkullEnemy && SkullSideways != null) { try { return (bool)SkullSideways.GetValue(e); } catch { } }
            return false;
        }

        /// <summary>The lane of the k-th hit after the next one: sideways movers step one lane per hit, everything
        /// else stays in its lane.</summary>
        private static int PredictFollowLane(RREnemy e, int columns, int k)
        {
            int x = e.TargetGridPosition.x;
            if (!MovesSidewaysWhenHit(e, out bool flips)) return x;
            int dir = e.IsFacingLeft ? -1 : 1;
            if (e.IsPerformingHitMovement)
            {
                // The move of the hit just taken: maybe still to be decided; a red bat turns around once it ends.
                if (HitMoveUndecided(e)) x = Step(x, ref dir, columns, e.ShouldWrapAroundGrid);
                if (flips) dir = -dir;
            }
            for (int i = 0; i < k; i++)
            {
                x = Step(x, ref dir, columns, e.ShouldWrapAroundGrid);
                if (flips) dir = -dir;
            }
            return x;
        }

        private static int Step(int x, ref int dir, int columns, bool wrap)
        {
            int next = x + dir;
            if (next >= 0 && next < columns) return next;
            if (wrap) return (next + columns) % columns;
            dir = -dir;
            next = x + dir;
            return next >= 0 && next < columns ? next : x;
        }

        private static bool Near(float a, float b) => IsFinite(b) && Math.Abs(a - b) < 0.05f;
        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private static float SecondsUntil(RREnemyController c, FmodTimeCapsule ftc, float beat)
        {
            if (_queueDelay != null)
            {
                try { return _queueDelay(c, ftc, beat); } catch { _queueDelay = null; }
            }
            return (beat - ftc.TrueBeatNumber) * ftc.BeatLengthInSeconds;
        }

        public static int HoldLength(RREnemy e)
        {
            try { return _enemyLength != null ? _enemyLength(e) : 0; } catch { return 0; }
        }
    }
}
