#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Roster;
using Golfin.Save;
using UnityEngine;

namespace Golfin.UI.Rankings
{
    /// <summary>
    /// Phase 1 implementation of ILeaderboardProvider.
    /// Loads fake_players.csv from Resources/Data, generates deterministic seeded scores,
    /// merges the real player's SaveData accumulators, and returns the ranked list.
    ///
    /// Score formula:
    ///   score(fake, period) = seededBase(fake.id, periodKey) + drift(fake.id, elapsedFraction)
    ///
    /// All computation is in-memory and deterministic — no persistence needed.
    /// </summary>
    public class LocalFakeLeaderboardProvider : ILeaderboardProvider
    {
        // ── Score tuning constants ────────────────────────────────────────────
        // Distribution: base scores cluster around 2,000–40,000 RP.
        // Historic uses a larger fixed base (50,000–500,000) to look like an all-time board.
        private const int BaseScoreMin       = 500;
        private const int BaseScoreMax       = 40_000;
        private const int HistoricBaseMin    = 50_000;
        private const int HistoricBaseMax    = 500_000;
        private const int DriftMaxPerPeriod  = 5_000; // max drift added across full period

        // ── Fake player data ──────────────────────────────────────────────────
        private struct FakePlayer
        {
            public string Id;
            public string Username;
            public string CharacterId;
            public int    Level;
        }

        private readonly List<FakePlayer> _fakePlayers = new List<FakePlayer>();
        private bool _loaded;

        // ── ITimeProvider ─────────────────────────────────────────────────────
        private readonly ITimeProvider _time;

        public LocalFakeLeaderboardProvider(ITimeProvider? time = null)
        {
            _time = time ?? NetworkTimeProvider.Instance;
        }

        // ── ILeaderboardProvider ──────────────────────────────────────────────

        public IReadOnlyList<LeaderboardEntry> GetRanking(LeaderboardPeriod period)
        {
            EnsureLoaded();
            var all = BuildAllEntries(period);
            return ApplyRanksWithTies(all);
        }

        public LeaderboardEntry GetPlayerEntry(LeaderboardPeriod period)
        {
            var ranking = GetRanking(period);
            foreach (var e in ranking)
            {
                if (e.IsPlayer) return e;
            }
            // Fallback (shouldn't happen if player is always merged)
            return new LeaderboardEntry
            {
                Rank = ranking.Count + 1,
                IsTie = false,
                DisplayName = "YOU",
                CharacterId = CharacterManager.Instance != null
                              ? CharacterManager.Instance.GetSelectedCharacterId()
                              : string.Empty,
                Level = 1,
                Score = GetPlayerScore(period),
                IsPlayer = true
            };
        }

        public DateTime GetPeriodEndUtc(LeaderboardPeriod period)
        {
            if (period == LeaderboardPeriod.Historic) return DateTime.MaxValue;
            DateTime utcNow = _time.UtcNow;
            return period switch
            {
                LeaderboardPeriod.Daily   => LeaderboardPeriodKey.DailyPeriodEnd(utcNow),
                LeaderboardPeriod.Weekly  => LeaderboardPeriodKey.WeeklyPeriodEnd(utcNow),
                LeaderboardPeriod.Monthly => LeaderboardPeriodKey.MonthlyPeriodEnd(utcNow),
                _                        => DateTime.MaxValue
            };
        }

        // ── Internals ─────────────────────────────────────────────────────────

        private void EnsureLoaded()
        {
            if (_loaded) return;
            LoadFakePlayers();
            _loaded = true;
        }

        private void LoadFakePlayers()
        {
            var asset = Resources.Load<TextAsset>("Data/fake_players");
            if (asset == null)
            {
                Debug.LogError("[LocalFakeLeaderboardProvider] Resources/Data/fake_players.csv not found!");
                return;
            }

            string[] lines = asset.text.Split('\n');
            bool first = true;
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
                if (first) { first = false; continue; } // skip header

                string[] parts = line.Split(',');
                if (parts.Length < 4) continue;
                _fakePlayers.Add(new FakePlayer
                {
                    Id          = parts[0].Trim(),
                    Username    = parts[1].Trim(),
                    CharacterId = parts[2].Trim(),
                    Level       = int.TryParse(parts[3].Trim(), out int lv) ? lv : 1
                });
            }

            Debug.Log($"[LocalFakeLeaderboardProvider] Loaded {_fakePlayers.Count} fake players.");
        }

        private List<LeaderboardEntry> BuildAllEntries(LeaderboardPeriod period)
        {
            DateTime utcNow = _time.UtcNow;
            long periodKey = GetPeriodKey(period, utcNow);
            float elapsedFraction = GetPeriodElapsedFraction(period, utcNow);

            var list = new List<LeaderboardEntry>(_fakePlayers.Count + 1);

            // Add fakes
            foreach (var fp in _fakePlayers)
            {
                long score = ComputeFakeScore(fp.Id, period, periodKey, elapsedFraction);
                list.Add(new LeaderboardEntry
                {
                    DisplayName = fp.Username,
                    CharacterId = fp.CharacterId,
                    Level       = fp.Level,
                    Score       = score,
                    IsPlayer    = false
                });
            }

            // Add real player
            long playerScore = GetPlayerScore(period);
            // Also run lazy rollover on the save data period keys when the leaderboard is opened
            RolloverStalePeriods(utcNow);

            string playerCharId = CharacterManager.Instance != null
                                  ? CharacterManager.Instance.GetSelectedCharacterId()
                                  : string.Empty;
            int playerLevel = 1;
            if (CharacterManager.Instance != null && !string.IsNullOrEmpty(playerCharId))
            {
                var pcd = CharacterManager.Instance.GetPlayerCharacter(playerCharId);
                if (pcd != null) playerLevel = pcd.currentLevel;
            }

            list.Add(new LeaderboardEntry
            {
                DisplayName = "YOU",
                CharacterId = playerCharId,
                Level       = playerLevel,
                Score       = playerScore,
                IsPlayer    = true
            });

            // Sort descending by score
            list.Sort((a, b) => b.Score.CompareTo(a.Score));
            return list;
        }

        private static IReadOnlyList<LeaderboardEntry> ApplyRanksWithTies(List<LeaderboardEntry> sorted)
        {
            // Count score frequency to detect ties
            var scoreCount = new Dictionary<long, int>();
            foreach (var e in sorted)
            {
                scoreCount.TryGetValue(e.Score, out int cnt);
                scoreCount[e.Score] = cnt + 1;
            }

            // Assign ranks (dense: equal scores get same rank; next distinct score skips)
            // Per spec: tie shown as T11, T11, then 14 (not 13).
            // This is "standard competition ranking" (1,2,2,4,…)
            int rank = 1;
            for (int i = 0; i < sorted.Count; i++)
            {
                var e = sorted[i];
                bool isTie = scoreCount[e.Score] > 1;
                sorted[i] = new LeaderboardEntry
                {
                    Rank        = rank,
                    IsTie       = isTie,
                    DisplayName = e.DisplayName,
                    CharacterId = e.CharacterId,
                    Level       = e.Level,
                    Score       = e.Score,
                    IsPlayer    = e.IsPlayer
                };

                // Advance rank by 1 for the next entry, but if the next entry has a different score
                // we already moved past all ties by the offset.
                if (i + 1 < sorted.Count && sorted[i + 1].Score != e.Score)
                {
                    rank = i + 2; // 1-based: next distinct is at position i+2
                }
                // If the next entry has the same score, rank stays unchanged for them.
            }

            return sorted;
        }

        // ── Score computation ─────────────────────────────────────────────────

        private long ComputeFakeScore(string fakeId, LeaderboardPeriod period, long periodKey, float elapsedFraction)
        {
            if (period == LeaderboardPeriod.Historic)
            {
                // Use a fixed large base for the all-time board
                int seed = HashToSeed(fakeId + "_historic");
                var rng = new System.Random(seed);
                return rng.Next(HistoricBaseMin, HistoricBaseMax);
            }

            // Seeded base for the current period
            int baseSeed = HashToSeed(fakeId + "_" + periodKey);
            var baseRng = new System.Random(baseSeed);
            long baseScore = baseRng.Next(BaseScoreMin, BaseScoreMax);

            // Drift: slowly increases as the period elapses
            int driftSeed = HashToSeed(fakeId + "_drift_" + periodKey);
            var driftRng = new System.Random(driftSeed);
            long maxDrift = driftRng.Next(0, DriftMaxPerPeriod);
            long drift = (long)(maxDrift * elapsedFraction);

            return baseScore + drift;
        }

        private static int HashToSeed(string input)
        {
            // FNV-1a hash — deterministic, doesn't depend on string.GetHashCode stability
            unchecked
            {
                int hash = (int)2166136261u;
                foreach (char c in input)
                    hash = (hash ^ c) * 16777619;
                return hash;
            }
        }

        // ── Period helpers ────────────────────────────────────────────────────

        private long GetPeriodKey(LeaderboardPeriod period, DateTime utcNow)
        {
            return period switch
            {
                LeaderboardPeriod.Daily   => LeaderboardPeriodKey.Daily(utcNow),
                LeaderboardPeriod.Weekly  => LeaderboardPeriodKey.Weekly(utcNow),
                LeaderboardPeriod.Monthly => LeaderboardPeriodKey.Monthly(utcNow),
                _                        => 0 // Historic: key unused
            };
        }

        private float GetPeriodElapsedFraction(LeaderboardPeriod period, DateTime utcNow)
        {
            if (period == LeaderboardPeriod.Historic) return 1f;
            DateTime start = GetPeriodStart(period, utcNow);
            DateTime end   = GetPeriodEndUtc(period);
            double total   = (end - start).TotalSeconds;
            if (total <= 0) return 0f;
            double elapsed = (utcNow - start).TotalSeconds;
            return Mathf.Clamp01((float)(elapsed / total));
        }

        private DateTime GetPeriodStart(LeaderboardPeriod period, DateTime utcNow)
        {
            return period switch
            {
                LeaderboardPeriod.Daily   => utcNow.Date,
                LeaderboardPeriod.Weekly  => LeaderboardPeriodKey.WeeklyPeriodEnd(utcNow).AddDays(-7),
                LeaderboardPeriod.Monthly => new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _                        => DateTime.MinValue
            };
        }

        private long GetPlayerScore(LeaderboardPeriod period)
        {
            if (SaveDataHost.Instance == null) return 0;
            var data = SaveDataHost.Instance.Data;
            return period switch
            {
                LeaderboardPeriod.Daily   => data.rpDaily,
                LeaderboardPeriod.Weekly  => data.rpWeekly,
                LeaderboardPeriod.Monthly => data.rpMonthly,
                _                        => data.lifetimeRpEarned
            };
        }

        /// <summary>
        /// Lazy rollover: when the leaderboard is opened, reset stale accumulators so
        /// a player who hasn't earned RP since the boundary shows 0 for the new period.
        /// </summary>
        private void RolloverStalePeriods(DateTime utcNow)
        {
            if (SaveDataHost.Instance == null) return;
            var data = SaveDataHost.Instance.Data;
            bool dirty = false;

            long dailyKey = LeaderboardPeriodKey.Daily(utcNow);
            if (data.dailyPeriodKey != dailyKey && data.dailyPeriodKey != 0)
            {
                data.rpDaily = 0;
                data.dailyPeriodKey = dailyKey;
                dirty = true;
            }

            long weeklyKey = LeaderboardPeriodKey.Weekly(utcNow);
            if (data.weeklyPeriodKey != weeklyKey && data.weeklyPeriodKey != 0)
            {
                data.rpWeekly = 0;
                data.weeklyPeriodKey = weeklyKey;
                dirty = true;
            }

            long monthlyKey = LeaderboardPeriodKey.Monthly(utcNow);
            if (data.monthlyPeriodKey != monthlyKey && data.monthlyPeriodKey != 0)
            {
                data.rpMonthly = 0;
                data.monthlyPeriodKey = monthlyKey;
                dirty = true;
            }

            if (dirty) SaveDataHost.Instance.MarkDirty();
        }
    }
}
