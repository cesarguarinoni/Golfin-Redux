#nullable enable
using System.Collections.Generic;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// What a mission attempt came to. Spec: missions_v1 §B4.
    ///
    /// ⚠️ `Cleared` IS A CLAIM, NOT A PAYOUT. It is what the client believes and what it sends
    /// to `POST /api/v1/missions/claim` as `goals_met`; the server decides what that is worth
    /// by reading `golfin_mission_rewards`. Nothing in this struct is money.
    /// </summary>
    public sealed class MissionResult
    {
        public string MissionId = "";
        public int Strokes;
        public int Putts;

        /// <summary>Every goal on the card, in order, each with its verdict.</summary>
        public readonly List<MissionGoal> Goals = new List<MissionGoal>();

        /// <summary>True only when EVERY goal was met. One missed goal is a failed mission —
        /// there is no partial credit, by design.</summary>
        public bool Cleared;

        /// <summary>True when the hole ended because the stroke cap ran out rather than the
        /// ball going in. The modal says so; the server does not care.</summary>
        public bool FailedOnStrokeCap;

        /// <summary>
        /// The idempotency key for the claim: `mission:&lt;id&gt;:&lt;session guid&gt;`, per §B4.
        /// Generated ONCE, at hole completion, so an offline replay of the same attempt is the
        /// same key and the server replies with the original award rather than paying twice.
        /// </summary>
        public string IdempotencyKey = "";
    }
}
