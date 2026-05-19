using System.Collections.Generic;
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
        }

        /// <summary>
        /// PLAY NEXT path: re-points to a new hole, resets per-hole state, keeps seed.
        /// </summary>
        public static void SetCurrentHole(int holeNumber)
        {
            CurrentHoleNumber = holeNumber;
            ResetForNewHole();
        }

        /// <summary>
        /// Full session clear — used on MENU / back-to-Home path (Stage D).
        /// </summary>
        public static void ResetSession()
        {
            CurrentHoleNumber   = 0;
            SelectedCharacterId = string.Empty;
            EquippedBagSlot     = 0;
            ResetForNewHole();
        }

        /// <summary>
        /// Fire OnHoleComplete with the given payload. Called by HoleCompleteDriver
        /// when the ball state machine reaches BallState.InCup.
        /// </summary>
        public static void MarkHoleComplete(HoleCompletionData data) => OnHoleComplete?.Invoke(data);
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

        // §2e: 9-arg constructor with PenaltyStrokes.
        public ShotRecord(
            int shotNumber, string clubLabel,
            Vector3 originPosition, Vector3 finalPosition,
            float distanceXZMeters,
            string terminalState, string obReason, string finalSurface,
            int penaltyStrokes)
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
        }

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
