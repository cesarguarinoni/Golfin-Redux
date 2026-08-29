#nullable enable
using System.Collections.Generic;
using Golfin.Save;

namespace Golfin.Gameplay.Missions
{
    /// <summary>
    /// Which missions a player has cleared, and which tiers are open to them.
    /// Spec: missions_v1 §C2. Mirrors <c>HoleProgressionService</c>.
    ///
    /// ⚠️ IT IS A READ-ONLY VIEW OF SERVER TRUTH, AND THAT IS THE WHOLE DIFFERENCE FROM
    /// <c>HoleProgressionService</c>. Hole progression is written locally — the client decides
    /// a hole is unlocked and saves it. Mission progress is decided by the SERVER: it comes
    /// back on a claim response and is mirrored into <c>SaveData.missionProgress</c> by the
    /// Hole Complete modal. Nothing here writes it. A client that incremented its own clears
    /// would show a first-clear reward the server was never going to pay again, which is the
    /// class of bug this whole feature was built to avoid.
    ///
    /// The two RULES it does own are pure functions of that data, and they live here rather
    /// than on the screen so the card list and the tier tabs cannot disagree:
    ///
    ///   WITHIN a tier — `unlock=clear:&lt;id&gt;` chains one mission to the previous.
    ///   BETWEEN tiers — tier N+1 opens when `unlockClears` of tier N are cleared (8 of 10).
    ///
    /// Falls back to an empty picture when SaveDataHost is absent (EditMode tests), which is
    /// the correct state for a player who has cleared nothing.
    /// </summary>
    public class MissionProgressionService
    {
        private static MissionProgressionService? _instance;
        public static MissionProgressionService Instance => _instance ??= new MissionProgressionService();

        /// <summary>EditMode tests seed this instead of a save file.</summary>
        public static void ResetForTests() => _instance = null;

        private readonly Dictionary<string, PersistedMissionProgress> _memory =
            new Dictionary<string, PersistedMissionProgress>();

        // ── Read ────────────────────────────────────────────────────────────────

        private IEnumerable<PersistedMissionProgress> Rows
        {
            get
            {
                var host = SaveDataHost.Instance;
                if (host?.Data?.missionProgress != null) return host.Data.missionProgress;
                return _memory.Values;
            }
        }

        public PersistedMissionProgress? Find(string missionId)
        {
            foreach (var r in Rows) if (r.missionId == missionId) return r;
            return null;
        }

        public int Clears(string missionId) => Find(missionId)?.clears ?? 0;
        public int Attempts(string missionId) => Find(missionId)?.attempts ?? 0;
        public int BestStrokes(string missionId) => Find(missionId)?.bestStrokes ?? 0;

        public bool HasCleared(string missionId) => Clears(missionId) > 0;

        /// <summary>Tried and NOT cleared. A real state the card renders differently from
        /// "never opened" — usually the one a support question is about.</summary>
        public bool HasFailed(string missionId) => Clears(missionId) == 0 && Attempts(missionId) > 0;

        // ── The two unlock rules ────────────────────────────────────────────────

        /// <summary>Cleared missions in a tier, counted over the CATALOG rather than over the
        /// save — a save row for a mission that has since been withdrawn must not keep a tier
        /// open that its remaining missions cannot.</summary>
        public int ClearedInTier(string tier)
        {
            int n = 0;
            foreach (var m in MissionCatalog.All)
                if (m.Tier == tier && HasCleared(m.Id)) n++;
            return n;
        }

        /// <summary>
        /// Is this tier open? The FIRST tier always is (`unlockClears = 0`); every other needs
        /// `unlockClears` clears of the tier before it.
        /// </summary>
        public bool IsTierUnlocked(string tier)
        {
            var tiers = MissionCatalog.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Tier != tier) continue;
                if (i == 0 || tiers[i].UnlockClears <= 0) return true;
                return ClearedInTier(tiers[i - 1].Tier) >= tiers[i].UnlockClears;
            }
            return false;   // a tier the catalog does not carry is not open
        }

        /// <summary>How many more clears of the previous tier this one needs. 0 = open.</summary>
        public int ClearsNeededFor(string tier)
        {
            var tiers = MissionCatalog.Tiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Tier != tier) continue;
                if (i == 0 || tiers[i].UnlockClears <= 0) return 0;
                int have = ClearedInTier(tiers[i - 1].Tier);
                return have >= tiers[i].UnlockClears ? 0 : tiers[i].UnlockClears - have;
            }
            return int.MaxValue;
        }

        /// <summary>
        /// Can this mission be played? Its tier must be open AND its own `unlock` satisfied.
        ///
        /// `unlock` is `start` (always) or `clear:&lt;id&gt;`. An unlock naming a mission the
        /// catalog does not carry returns FALSE — the publish validator blocks that, and if one
        /// ever reached a client a locked card is a far better outcome than a card that opens
        /// a mission nothing gates.
        /// </summary>
        public bool IsUnlocked(MissionDefinition mission)
        {
            if (mission == null) return false;
            if (!IsTierUnlocked(mission.Tier)) return false;

            string unlock = (mission.Unlock ?? "").Trim();
            if (unlock.Length == 0 || unlock == "start") return true;
            if (!unlock.StartsWith("clear:", System.StringComparison.Ordinal)) return false;

            string prereq = unlock.Substring("clear:".Length).Trim();
            foreach (var m in MissionCatalog.All)
                if (m.Id == prereq) return HasCleared(prereq);
            return false;
        }

        /// <summary>The NEXT mission — the first unlocked one not yet cleared. The screen
        /// expands it by default and scrolls to it. Null once everything is cleared.</summary>
        public MissionDefinition? NextMission()
        {
            foreach (var m in MissionCatalog.All)
                if (IsUnlocked(m) && !HasCleared(m.Id)) return m;
            return null;
        }

        // ── Test seam ───────────────────────────────────────────────────────────

        /// <summary>Seed progress WITHOUT a save file. EditMode tests only — the production
        /// path writes this from a claim response and nothing else.</summary>
        public void SeedForTests(string missionId, int clears, int attempts = 0, int bestStrokes = 0)
        {
            var host = SaveDataHost.Instance;
            if (host?.Data?.missionProgress != null)
            {
                var row = host.Data.missionProgress.Find(m => m.missionId == missionId);
                if (row == null)
                {
                    row = new PersistedMissionProgress { missionId = missionId };
                    host.Data.missionProgress.Add(row);
                }
                row.clears = clears; row.attempts = attempts; row.bestStrokes = bestStrokes;
                return;
            }
            _memory[missionId] = new PersistedMissionProgress
            {
                missionId = missionId, clears = clears, attempts = attempts, bestStrokes = bestStrokes,
            };
        }

        public void ClearForTests() => _memory.Clear();
    }
}
