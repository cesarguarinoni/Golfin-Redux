using System;
using Golfin.Gameplay.Loop;

namespace Golfin.Gameplay.Session
{
    /// <summary>
    /// Payload for GameSession.OnHoleComplete. Lightweight — only session-level
    /// data. UI consumers (Stage C Result Modal) assemble the richer
    /// HoleCompleteData (UI payload) by combining this + HoleContext + HoleData CSV.
    /// </summary>
    public readonly struct HoleCompletionData
    {
        public readonly BallState TerminalState;   // InCup (SUCCESS) or AtRest-stroke-cap (FAILED)
        public readonly int       Strokes;          // total strokes inc. penalties
        public readonly int       PenaltyStrokes;
        public readonly int       HoleNumber;
        public readonly DateTime  CompletedAtUtc;

        public HoleCompletionData(BallState terminalState, int strokes, int penaltyStrokes, int holeNumber)
        {
            TerminalState  = terminalState;
            Strokes        = strokes;
            PenaltyStrokes = penaltyStrokes;
            HoleNumber     = holeNumber;
            CompletedAtUtc = DateTime.UtcNow;
        }
    }
}
