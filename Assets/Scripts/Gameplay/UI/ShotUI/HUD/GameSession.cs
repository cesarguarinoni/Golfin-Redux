using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Gameplay.UI.HUD
{
    /// <summary>
    /// Per-hole ephemeral session state. Reset on every hole load.
    /// Not persisted across app restarts (Loop v2 / save state spec handles persistence).
    /// </summary>
    public static class GameSession
    {
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

        // ── Lifecycle ─────────────────────────────────────────────────────────
        /// <summary>
        /// Reset session state for a new hole. Clears history, sets turn back to 1.
        /// Fires both OnTurnChanged and OnHistoryChanged so subscribers re-render cleanly.
        /// </summary>
        public static void ResetForNewHole()
        {
            TurnCount = 1;
            ShotHistory.Clear();
            OnTurnChanged?.Invoke();
            OnHistoryChanged?.Invoke();
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
