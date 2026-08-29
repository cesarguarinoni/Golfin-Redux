#nullable enable
using System;
using Golfin.Course.Runtime;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Session;
using UnityEngine;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// The one place that knows a mission is being played. Spec: missions_v1 §B2.
    ///
    /// WHAT IT DOES AND DOES NOT DO. It holds the resolved <see cref="MissionDefinition"/> and
    /// derives the four things a hole load has to be told — where the ball starts, which pin,
    /// what the wind is doing, and how many strokes the goals allow. It does NOT apply them.
    /// `PhysicsLabController.OnHoleLoaded` already owns `_ballSpawnPoint`, `HoleContext.PinWorld`
    /// and `WindContext`, and it reads this class at the end of that scan.
    ///
    /// That split is not fussiness — it is what keeps this assembly a LEAF. `WindContext` and
    /// `HoleContext` live in `Golfin.Gameplay.UI`; if Missions referenced UI, the Hole Complete
    /// modal could never reference Missions to draw its goal ticks, because that would close a
    /// cycle. So the flow is one-directional: Missions computes, the Viewer applies, the UI
    /// reads. The spec asks for exactly this — PhysicsLabController gets "spawn/pin/wind
    /// override entry points only".
    ///
    /// ⚠️ PRACTICE, 1v1 AND TOURNAMENTS MUST NEVER ENTER A MISSION SESSION. Nothing here runs
    /// unless <see cref="Begin"/> was called, and only the Mission Selection screen calls it.
    /// <see cref="IsActive"/> is false in every other mode, so every override below is inert —
    /// asserted in MissionSessionTests rather than assumed.
    /// </summary>
    public static class MissionSession
    {
        public static MissionDefinition? Active { get; private set; }
        public static bool IsActive => Active != null;

        /// <summary>Raised on Begin and on End, so HUD widgets can show/hide the goal strip.</summary>
        public static event Action? OnChanged;

        /// <summary>Set by Begin, re-rolled per shot when the preset is GUSTY.</summary>
        public static float WindSpeedMph { get; private set; }

        /// <summary>ABSOLUTE bearing in degrees, already folded with the spawn→pin bearing.</summary>
        public static float WindDirectionDegrees { get; private set; }

        /// <summary>The pin this mission plays to, resolved from the hole's candidates.</summary>
        public static Vector3 PinWorld { get; private set; }

        /// <summary>Where the ball starts, for a SHORT start. Null for a tee start.</summary>
        public static Vector3? SpawnWorld { get; private set; }

        /// <summary>The goal evaluator for the run in progress. Null when no mission is active.</summary>
        public static MissionGoalEvaluator? Evaluator { get; private set; }

        private static int _gustSeed;

        /// <summary>
        /// Subscribe to the teardown signal.
        ///
        /// `GameSession.ResetSession()` is the back-to-Home path, and a mission left standing
        /// across it would put the player into their next Practice round holding a mission's
        /// supplied clubs. Loop cannot call into Missions (Missions references Loop), so the
        /// arrow is inverted: Loop raises, this listens.
        ///
        /// ⚠️ SUBSCRIBED FROM <see cref="Begin"/>, NOT FROM A
        /// `[RuntimeInitializeOnLoadMethod]`. That is what the first version did, and it was
        /// wrong in a way that only a test could show: a runtime hook does not run in EditMode
        /// at all, and in a player it runs once at load — so the subscription's existence
        /// depended on something entirely unrelated to whether a mission was in progress.
        /// Arming it where the state is created ties the guarantee to the thing it guards, and
        /// makes it true in every context including a test that never enters play mode.
        ///
        /// The unsubscribe-then-subscribe is what keeps it single: `Begin` can be called any
        /// number of times, and `event +=` does not deduplicate.
        /// </summary>
        private static void HookSessionReset()
        {
            GameSession.OnSessionReset -= Clear;
            GameSession.OnSessionReset += Clear;
        }

        // ── Begin / End ─────────────────────────────────────────────────────────

        /// <summary>
        /// Enter a mission. Call BEFORE the hole scene loads: `OnHoleLoaded` reads the
        /// overrides while it is placing the ball, and a mission begun after that would have
        /// the player teeing off from the wrong place for one shot.
        ///
        /// Returns false and changes NOTHING when the definition cannot be honoured — an empty
        /// bag, or a short start with no baked coordinates. A half-applied mission is the one
        /// outcome worth avoiding: the player would be somewhere the card did not promise, with
        /// clubs it did not list, and no way to tell.
        /// </summary>
        public static bool Begin(MissionDefinition mission)
        {
            if (mission == null) return false;
            if (mission.ClubIds.Count == 0)
            {
                Debug.LogError($"[MissionSession] {mission.Id}: loadout resolved to an EMPTY bag — refusing to start.");
                return false;
            }
            if (mission.StartKind == "short" && !mission.StartWorld.HasValue)
            {
                Debug.LogError($"[MissionSession] {mission.Id}: short start '{mission.StartAreaId}' has no baked " +
                               "coordinates — run Golfin ▸ Missions ▸ Bake Start Areas. Refusing to start.");
                return false;
            }

            End();   // a mission left standing from a previous run is not a base to build on

            HookSessionReset();

            Active = mission;
            SpawnWorld = mission.StartWorld;
            _gustSeed = mission.Id.GetHashCode() ^ mission.HoleNumber;

            ResolvePin(mission);
            ResolveWind(mission);

            // The documented Missions opt-in on GameSession, in use for the first time.
            int? cap = mission.StrokeCapOverPar;
            GameSession.StrokeCapEnabled = cap.HasValue;
            GameSession.StrokeCapOverPar = cap ?? 0;

            MissionSessionBag.Push(mission.ClubIds);

            Evaluator = new MissionGoalEvaluator(mission, PinWorld);
            Evaluator.Attach();

            OnChanged?.Invoke();
            Debug.Log($"[MissionSession] BEGIN {mission.Id} hole {mission.HoleNumber} " +
                      $"start={mission.StartAreaId} pin={mission.PinIndex} " +
                      $"wind={WindSpeedMph:F0}mph@{WindDirectionDegrees:F0}° " +
                      $"cap={(cap.HasValue ? cap.Value.ToString() : "none")} clubs={mission.ClubIds.Count}");
            return true;
        }

        /// <summary>
        /// Leave the mission and put everything back. Safe to call when nothing is active, and
        /// safe to call twice — a teardown path that has to be careful about being run twice is
        /// a teardown path that gets skipped.
        /// </summary>
        public static void End()
        {
            if (Active == null) return;
            string id = Active.Id;

            Evaluator?.Detach();
            Evaluator = null;

            MissionSessionBag.Pop();

            GameSession.StrokeCapEnabled = false;
            GameSession.StrokeCapOverPar = 0;

            Active = null;
            SpawnWorld = null;
            WindSpeedMph = 0f;
            WindDirectionDegrees = 0f;

            OnChanged?.Invoke();
            Debug.Log($"[MissionSession] END {id}");
        }

        /// <summary>
        /// Hard reset — what <c>GameSession.ResetSession()</c> calls. Unlike <see cref="End"/>
        /// this does not care whether the stack is balanced; it only cares that nothing
        /// survives into the next mode.
        /// </summary>
        public static void Clear()
        {
            Evaluator?.Detach();
            Evaluator = null;
            Active = null;
            SpawnWorld = null;
            WindSpeedMph = 0f;
            WindDirectionDegrees = 0f;
            MissionSessionBag.Clear();
            GameSession.StrokeCapEnabled = false;
            GameSession.StrokeCapOverPar = 0;
        }

        // ── Pin + wind ──────────────────────────────────────────────────────────

        private static void ResolvePin(MissionDefinition mission)
        {
            GreenTopology topo = GreenTopology.LoadFromResources(mission.HoleNumber);
            if (topo == null)
            {
                Debug.LogWarning($"[MissionSession] no green.json for hole {mission.HoleNumber:D2}; " +
                                 "pin falls back to the scene's Flag.");
                PinWorld = Vector3.zero;
                return;
            }
            var pins = topo.GetPinCandidates();
            if (pins == null || pins.Count == 0) { PinWorld = Vector3.zero; return; }

            // Clamp rather than throw. A mission asking for pin 1 on a hole that has only one
            // is a data problem the validator should have caught, but at THIS point the player
            // has already tapped PLAY — the default pin is a playable hole, an exception is not.
            int index = Mathf.Clamp(mission.PinIndex, 0, pins.Count - 1);
            if (index != mission.PinIndex)
                Debug.LogWarning($"[MissionSession] {mission.Id}: pinIndex {mission.PinIndex} but hole " +
                                 $"{mission.HoleNumber:D2} has {pins.Count} candidate(s); using {index}.");
            PinWorld = pins[index];
        }

        private static void ResolveWind(MissionDefinition mission)
        {
            // The preset's direction is RELATIVE to the shot the player is about to face, which
            // is what makes "strong headwind" mean the same thing on every hole. Absolute
            // bearing = the spawn→pin bearing, plus the preset's offset.
            Vector3 spawn = SpawnWorld ?? Vector3.zero;
            float bearing = 0f;
            if (PinWorld != Vector3.zero && spawn != Vector3.zero)
            {
                Vector3 toPin = PinWorld - spawn;
                bearing = Mathf.Atan2(toPin.x, toPin.z) * Mathf.Rad2Deg;
            }
            WindDirectionDegrees = Mathf.Repeat(bearing + mission.WindRelDirDeg, 360f);
            WindSpeedMph = mission.WindGusty ? RollGust(0) : mission.WindSpeedMph;
        }

        /// <summary>
        /// GUSTY: a fresh speed in [6, 18] mph for every shot. Deterministic in
        /// (mission, shot number) so a replay of the same mission is the same round — a
        /// wind that re-rolls from `UnityEngine.Random` would make the same mission a
        /// different difficulty every attempt, which is not what "hard" should mean.
        /// </summary>
        public static float RollGust(int shotNumber)
        {
            unchecked
            {
                int h = _gustSeed * 486187739 + shotNumber * 31;
                h ^= h >> 13; h *= unchecked((int)0x5bd1e995); h ^= h >> 15;
                float t = (uint)h / (float)uint.MaxValue;
                return 6f + t * 12f;
            }
        }

        /// <summary>
        /// Called by the Viewer after each completed shot. Only a GUSTY mission moves; every
        /// other preset holds its speed for the whole hole.
        /// </summary>
        public static bool TryAdvanceGust(int shotNumber, out float newSpeedMph)
        {
            newSpeedMph = WindSpeedMph;
            if (Active == null || !Active.WindGusty) return false;
            WindSpeedMph = RollGust(shotNumber);
            newSpeedMph = WindSpeedMph;
            return true;
        }

        /// <summary>
        /// The Condition this mission costs at hole completion. `StaminaRuntimeService` asks
        /// this instead of `StaminaModel.DrainForHole()` when a mission is active — a 3-point
        /// short-game mission must not cost the same as a full hole.
        /// </summary>
        public static float DrainOverride(float configuredDrain)
            => Active != null ? Active.StaminaDrain : configuredDrain;
    }
}
