using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// §2d: immutable data payload passed from HoleCompleteDriver to HoleCompleteWidget.
    /// All placeholders noted inline.
    ///
    /// Iter-6: added HoleMap / NextHoleMap sprites (real hole-map art) and
    /// NextHoleDescription / NextHolePar (from HoleDatabase CSV) for the Card 2 info block.
    /// </summary>
    public readonly struct HoleCompleteData
    {
        // ── Card 1 (current hole, real data) ──────────────────────────────
        public readonly int    Strokes;          // GameSession.TurnCount
        public readonly int    Par;              // HoleContext.Par
        public readonly int    Score;            // strokes - par
        public readonly string ScoreLabel;       // "Birdie" / "Par" / "Bogey" / etc
        public readonly bool   IsFailed;         // score > 0
        public readonly bool   HasPersonalBest;  // Q8: false in §2d
        public readonly string CourseName;       // HoleContext.CourseName ("LOMOND")
        public readonly int    HoleNumber;       // HoleContext.HoleNumber
        public readonly string TeeName;          // HoleContext.TeeName ("REGULAR")

        // ── Card 1 placeholders (Q8: no PB / no time tracking) ────────────
        public readonly string BestStrokes;       // "—"
        public readonly string BestStrokesLabel;  // ""
        public readonly string TimeStr;           // "00:00:00"
        public readonly string BestTimeStr;       // "—"

        // ── Rewards row (placeholder hardcoded x10) ───────────────────────
        public readonly int RewardCoinX;
        public readonly int RewardRepairX;
        public readonly int RewardBallX;

        // ── Card 2 (next hole) ─────────────────────────────────────────────
        public readonly int    NextHoleNumber;
        public readonly int    NextHolePar;          // 0 = unknown; populated from HoleDatabase CSV in iter-6
        public readonly string NextHoleTipText;      // placeholder / description from localization

        // ── Hole map sprites (iter-6) ──────────────────────────────────────
        // Loaded by HoleCompleteDriver from Assets/Art/In-Game UI/HoleMaps/Lomond - Hole N.png.
        // Null if the map asset isn't found (e.g. Hole 19) — widget shows a solid-color fallback.
        public readonly Sprite HoleMap;         // Card 1 map (current hole)
        public readonly Sprite NextHoleMap;     // Card 2 map (next hole, may be null)

        public HoleCompleteData(
            int strokes, int par, int score, string scoreLabel,
            bool isFailed, bool hasPersonalBest,
            string courseName, int holeNumber, string teeName,
            string bestStrokes, string bestStrokesLabel,
            string timeStr, string bestTimeStr,
            int rewardCoinX, int rewardRepairX, int rewardBallX,
            int nextHoleNumber, int nextHolePar, string nextHoleTipText,
            Sprite holeMap = null, Sprite nextHoleMap = null)
        {
            Strokes          = strokes;
            Par              = par;
            Score            = score;
            ScoreLabel       = scoreLabel;
            IsFailed         = isFailed;
            HasPersonalBest  = hasPersonalBest;
            CourseName       = courseName;
            HoleNumber       = holeNumber;
            TeeName          = teeName;
            BestStrokes      = bestStrokes;
            BestStrokesLabel = bestStrokesLabel;
            TimeStr          = timeStr;
            BestTimeStr      = bestTimeStr;
            RewardCoinX      = rewardCoinX;
            RewardRepairX    = rewardRepairX;
            RewardBallX      = rewardBallX;
            NextHoleNumber   = nextHoleNumber;
            NextHolePar      = nextHolePar;
            NextHoleTipText  = nextHoleTipText;
            HoleMap          = holeMap;
            NextHoleMap      = nextHoleMap;
        }
    }
}
