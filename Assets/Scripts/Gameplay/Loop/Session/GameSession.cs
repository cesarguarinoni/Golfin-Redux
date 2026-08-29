using System.Collections.Generic;
using Golfin.Gameplay.Config;
using UnityEngine;

namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Session-wide state for the active gameplay run.
    /// Holds (a) the session seed set at Matchmaking "OPPONENT FOUND"
    /// (character / bag / hole pointer — survives across holes within the same
    /// session), and (b) per-hole ephemeral state (turn counter, shot history —
    /// cleared on each hole start). Not persisted across app restarts.
    /// </summary>
    public static class GameSession
    {
        // ── Session seed (set at Matchmaking "OPPONENT FOUND"; persists between holes) ──
        public static int    CurrentHoleNumber;
        public static string SelectedCharacterId = string.Empty;
        public static int    EquippedBagSlot;

        // ── Versus flag (gates all 1v1 HUD elements) ─────────────────────────
        /// <summary>
        /// True when a 1v1 match is active. False for all solo/Practice sessions.
        /// Set to true by ModeSelectScreenController / ModeCarouselController before
        /// opening the 1v1 matchmaking modal. Set to false by HoleSelectionScreenController
        /// on the Practice path. Cleared to false in ResetSession().
        /// </summary>
        public static bool IsVersus;

        // ── Tournament flag (gates tournament result handling) ────────────────
        /// <summary>
        /// True when a tournament hole is active. Set before BeginGameplayLoad on the
        /// tournament path. Cleared to false in ResetSession().
        /// HoleCompletionBridge (Golfin.Physics.Viewer) fires OnTournamentHoleComplete
        /// instead of OnHoleComplete when this is true.
        /// </summary>
        public static bool IsTournament;

        /// <summary>
        /// Active tournament id for the current session. Set alongside IsTournament.
        /// Cleared to null in ResetSession().
        /// </summary>
        public static string? TournamentId;

        /// <summary>
        /// Fired by HoleCompletionBridge (Golfin.Physics.Viewer) when a tournament hole
        /// completes. Carries holeNumber (1-indexed) and total stroke count.
        /// ShellScene-resident TournamentRoundHandler subscribes here to submit HoleResult.
        /// </summary>
        public static event System.Action<int, int>? OnTournamentHoleComplete;

        // ── Versus safety stroke cap (Phase 2a, CSV-keyed) ───────────────────
        /// <summary>
        /// Strokes above par at which a 1v1 match is forced to draw if neither player has holed.
        /// Read from modes.csv "versus_1v1.versusStrokeCapOverPar" by VersusResultHandler (Assembly-CSharp)
        /// and written here so VersusMatchController (Golfin.Physics.Viewer) can read it without
        /// crossing the asmdef boundary to ModesDatabaseCSV. Default = 5.
        /// </summary>
        public static int VersusStrokeCapOverPar = 5;

        // ── Solo stroke-cap FAILED state (Missions opt-in) ────────────────────
        /// <summary>
        /// Master gate for the solo stroke-cap FAILED end-condition read by
        /// <c>HoleCompletionBridge</c> (Golfin.Physics.Viewer).
        ///
        /// <b>Default false — no shipping mode has a fail condition.</b> Practice,
        /// Tournament and 1v1 all run to the cup; only the mode that explicitly opts in
        /// can end a hole as FAILED. Missions is the intended consumer: it sets this
        /// true (plus <see cref="StrokeCapOverPar"/>) when seeding a mission that carries
        /// a stroke limit, and the machinery below stays intact for it.
        ///
        /// Cleared to false in ResetSession() so a mission never leaks into the next
        /// Practice session. NOT cleared by ResetForNewHole() — a multi-hole mission
        /// keeps its limit across holes.
        ///
        /// Note: this does NOT gate <see cref="VersusStrokeCapOverPar"/>, which is a
        /// separate 1v1 match-termination safety net (forced draw), not a FAILED screen.
        /// </summary>
        public static bool StrokeCapEnabled;

        /// <summary>
        /// Strokes above par at which the hole ends as FAILED, when
        /// <see cref="StrokeCapEnabled"/> is true. Missions writes its own limit here.
        /// <c>0</c> (the default) means "use the value serialized on HoleCompletionBridge".
        /// Cleared to 0 in ResetSession().
        /// </summary>
        public static int StrokeCapOverPar;

        // ── Turn counter ──────────────────────────────────────────────────────
        public static int TurnCount = 1;
        public static event System.Action OnTurnChanged;
        public static void SetTurn(int n) { TurnCount = n; OnTurnChanged?.Invoke(); }

        // ── Shot history ──────────────────────────────────────────────────────
        public static readonly List<ShotRecord> ShotHistory = new List<ShotRecord>();
        public static event System.Action OnHistoryChanged;
        public static void RecordShot(ShotRecord record)
        {
            ShotHistory.Add(record);
            OnHistoryChanged?.Invoke();
        }

        // ── Cross-scene completion signal (Stage B) ───────────────────────────
        public static event System.Action<HoleCompletionData> OnHoleComplete;

        // ── Versus match completion signal (Phase 2a) ─────────────────────────
        /// <summary>
        /// Outcome of a completed 1v1 match.
        /// </summary>
        public enum MatchOutcome { P1Win, P2Win, Draw }

        /// <summary>
        /// Fired by VersusMatchController via MarkMatchComplete when the match ends.
        /// Carries outcome, P1 stroke count, P2 stroke count.
        /// ShellScene-resident VersusResultHandler subscribes here to grant RP and return home.
        /// VersusMatchController (in Golfin.Physics.Viewer) must NOT call RewardPointsManager
        /// directly — only fire this event.
        /// </summary>
        public static event System.Action<MatchOutcome, int, int> OnMatchComplete;

        /// <summary>Fire OnMatchComplete. Called by VersusMatchController at match end.</summary>
        public static void MarkMatchComplete(MatchOutcome outcome, int p1Strokes, int p2Strokes)
            => OnMatchComplete?.Invoke(outcome, p1Strokes, p2Strokes);

        /// <summary>
        /// Fire OnTournamentHoleComplete. Called by HoleCompletionBridge (Golfin.Physics.Viewer)
        /// at tournament hole completion, so callers outside this class can trigger the event.
        /// </summary>
        public static void FireTournamentHoleComplete(int holeNumber, int totalStrokes)
            => OnTournamentHoleComplete?.Invoke(holeNumber, totalStrokes);

        // ── Lifecycle ─────────────────────────────────────────────────────────
        /// <summary>
        /// Per-hole reset: clears history, sets turn back to 1.
        /// Preserves seed fields (CurrentHoleNumber / SelectedCharacterId / EquippedBagSlot).
        /// Fires both OnTurnChanged and OnHistoryChanged so subscribers re-render cleanly.
        /// </summary>
        public static void ResetForNewHole()
        {
            TurnCount = 1;
            ShotHistory.Clear();
            OnTurnChanged?.Invoke();
            OnHistoryChanged?.Invoke();
        }

        /// <summary>
        /// Initial seed at Matchmaking "OPPONENT FOUND".
        /// Sets all three seed fields and also runs ResetForNewHole.
        /// </summary>
        public static void SeedSession(int holeNumber, string characterId, int bagSlot)
        {
            CurrentHoleNumber   = holeNumber;
            SelectedCharacterId = characterId ?? string.Empty;
            EquippedBagSlot     = bagSlot;
            ResetForNewHole();
            RaiseRoundStarted();
        }

        /// <summary>
        /// PLAY NEXT path: re-points to a new hole, resets per-hole state, keeps seed.
        /// </summary>
        public static void SetCurrentHole(int holeNumber)
        {
            CurrentHoleNumber = holeNumber;
            ResetForNewHole();
            RaiseRoundStarted();
        }

        /// <summary>
        /// Full session clear — used on MENU / back-to-Home path (Stage D).
        /// </summary>
        public static void ResetSession()
        {
            CurrentHoleNumber   = 0;
            SelectedCharacterId = string.Empty;
            EquippedBagSlot     = 0;
            IsVersus            = false;
            IsTournament        = false;
            TournamentId        = null;
            StrokeCapEnabled    = false;
            StrokeCapOverPar    = 0;
            TournamentRoundContext.EndRound();
            ResetForNewHole();
        }

        // ── Round-start signal (beta_telemetry SPEC §1 #4) ────────────────────
        /// <summary>
        /// Fired whenever a hole actually begins: the initial <see cref="SeedSession"/> AND
        /// the PLAY NEXT path through <see cref="SetCurrentHole"/>. Both are needed —
        /// SeedSession alone would miss every hole after the first in a session.
        ///
        /// Deliberately NOT raised from <see cref="ResetForNewHole"/>, which
        /// <see cref="ResetSession"/> also calls: a teardown is not a round start.
        /// </summary>
        public static event System.Action OnRoundStarted;

        private static void RaiseRoundStarted()
        {
            // A telemetry subscriber must never be able to break a hole start.
            try { OnRoundStarted?.Invoke(); }
            catch (System.Exception ex) { Debug.LogWarning($"[GameSession] OnRoundStarted subscriber threw: {ex.Message}"); }
        }

        /// <summary>
        /// Fire OnHoleComplete with the given payload. Called by HoleCompleteDriver
        /// when the ball state machine reaches BallState.InCup.
        /// </summary>
        public static void MarkHoleComplete(HoleCompletionData data) => OnHoleComplete?.Invoke(data);

        // ── shot_timing_telemetry: the flick-timing keys every shot_taken carries ──

        /// <summary>
        /// The band the player's flick landed in, named with the SAME edges the shot was
        /// judged with (<see cref="ControlsConfig.Default"/>) so no consumer — dashboard
        /// included — has to know or re-derive the tuning numbers.
        ///
        /// Returns null for a sampleless swing (NaN): bots, capture drivers, FireDebugShot
        /// and EditMode tests never latch a timing sample. Null, never "red" and never 0 —
        /// a fake 0 would read as a botched flick in the aggregate.
        /// </summary>
        public static string TimingBand(float timing01)
        {
            if (float.IsNaN(timing01)) return null;
            var cfg = ControlsConfig.Default;
            if (timing01 >= cfg.TimingBandGreenY01) return "green";
            if (timing01 >= cfg.TimingBandGoldY01)  return "gold";
            return "red";
        }

        /// <summary>
        /// Writes the three timing keys of a <c>shot_taken</c> payload. Lives here rather than
        /// inline in TelemetryHooks for two reasons: Golfin.Gameplay.Config is not
        /// auto-referenced (Assembly-CSharp cannot see <c>ControlsConfig</c>), and the keys are
        /// then reachable from Golfin.Gameplay.Tests — the production shaping is what the tests
        /// assert, not a copy of it.
        /// </summary>
        public static void AppendShotTimingKeys(IDictionary<string, object> payload, in ShotRecord shot)
        {
            if (payload == null) return;
            payload["timing01"]   = float.IsNaN(shot.Timing01)
                                        ? null
                                        : (object)System.Math.Round(shot.Timing01, 2);
            payload["timing_mul"] = System.Math.Round(shot.TimingPowerMul, 2);
            payload["timing_band"] = TimingBand(shot.Timing01);
        }
    }

    /// <summary>
    /// Append-only record of one completed shot. Built from ShotResult on each
    /// BallStateMachine.OnShotComplete fire.
    /// </summary>
    public readonly struct ShotRecord
    {
        public readonly int    ShotNumber;          // 1-indexed within the hole
        public readonly string ClubLabel;           // "Driver", "Iron 7", "Wedge", "Putter"
        public readonly Vector3 OriginPosition;
        public readonly Vector3 FinalPosition;
        public readonly float   DistanceXZMeters;   // origin -> final XZ
        public readonly string  TerminalState;      // "AtRest" / "InCup" / "OB"
        public readonly string  OBReason;           // "Water" / "OutOfBounds" / "ExitedWorldBounds" / null
        public readonly string  FinalSurface;       // best-effort; "Unknown" if not derivable
        public readonly int     PenaltyStrokes;     // §2e: 0 normally, 1 on OB

        /// <summary>Slab progress (0..1) the committed flick was judged on, or NaN when the
        /// swing pushed no touch sample (bot / capture / debug shot). shot_timing_telemetry D2.</summary>
        public readonly float   Timing01;
        /// <summary>Power multiplier that timing cost the shot (F15). 1.0 = no penalty.</summary>
        public readonly float   TimingPowerMul;

        // shot_timing_telemetry: 11-arg constructor with the flick-timing pair.
        public ShotRecord(
            int shotNumber, string clubLabel,
            Vector3 originPosition, Vector3 finalPosition,
            float distanceXZMeters,
            string terminalState, string obReason, string finalSurface,
            int penaltyStrokes,
            float timing01, float timingPowerMul)
        {
            ShotNumber       = shotNumber;
            ClubLabel        = clubLabel;
            OriginPosition   = originPosition;
            FinalPosition    = finalPosition;
            DistanceXZMeters = distanceXZMeters;
            TerminalState    = terminalState;
            OBReason         = obReason;
            FinalSurface     = finalSurface;
            PenaltyStrokes   = penaltyStrokes;
            Timing01         = timing01;
            TimingPowerMul   = timingPowerMul;
        }

        // §2e: 9-arg constructor preserved — forwards with "no timing sample, no penalty".
        public ShotRecord(
            int shotNumber, string clubLabel,
            Vector3 originPosition, Vector3 finalPosition,
            float distanceXZMeters,
            string terminalState, string obReason, string finalSurface,
            int penaltyStrokes)
            : this(shotNumber, clubLabel, originPosition, finalPosition,
                   distanceXZMeters, terminalState, obReason, finalSurface, penaltyStrokes,
                   float.NaN, 1f)
        { }

        // §2c: existing 8-arg constructor preserved — forwards to new ctor with PenaltyStrokes=0.
        public ShotRecord(
            int shotNumber, string clubLabel,
            Vector3 originPosition, Vector3 finalPosition,
            float distanceXZMeters,
            string terminalState, string obReason, string finalSurface)
            : this(shotNumber, clubLabel, originPosition, finalPosition,
                   distanceXZMeters, terminalState, obReason, finalSurface, 0)
        { }
    }
}
