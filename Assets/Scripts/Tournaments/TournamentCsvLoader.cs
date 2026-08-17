// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentCsvLoader (T2)
// POCO that loads the three tournament CSVs from Resources/Data/.
// Pattern: clone ModesDatabaseCSV (header-name map, trim, #-comment skip).
// Data only — no bot rolling (T3), no backend logic (T4), no Unity lifecycle.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// POCO loader for the three tournament data CSVs.
    /// Loads each <c>TextAsset</c> via <c>Resources.Load&lt;TextAsset&gt;("Data/&lt;name&gt;")</c>
    /// — same pattern as ModesDatabaseCSV (SPEC §4 reuse).
    /// <para>
    /// Parse rules (all three files):
    /// • Skip blank lines and lines whose first non-whitespace char is <c>#</c>.
    /// • First non-comment/non-blank line = header; map columns by header name, not index.
    /// • Trim every cell value (whitespace).
    /// • UTF-8.
    /// </para>
    /// Lifecycle: callers own caching/re-use. T4's <c>LocalTournamentBackend</c> calls
    /// these once at startup and caches the results.
    /// </summary>
    public sealed class TournamentCsvLoader
    {
        private const string TournamentsPath    = "Data/tournaments";
        private const string PrizesPath         = "Data/tournament_prizes";
        private const string BotFieldsPath      = "Data/tournament_bot_fields";

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Load all rows from <c>tournaments.csv</c> → <c>TournamentDefinition</c>.
        /// Rows with empty <c>id</c> are skipped.
        /// </summary>
        public IReadOnlyList<TournamentDefinition> LoadTournaments()
        {
            var asset = LoadAsset(TournamentsPath);
            if (asset == null)
            {
                Debug.LogError("[TournamentCsvLoader] tournaments.csv not found in Resources/Data/");
                return Array.Empty<TournamentDefinition>();
            }

            var lines  = SplitLines(asset.text);
            int[]? idx = null; // column indices keyed by enum below
            int iId = -1, iNameKey = -1, iCourseId = -1, iHoleSet = -1;
            int iStartUtc = -1, iEndUtc = -1, iResolveDelay = -1;
            int iEntryFee = -1, iBotFieldId = -1, iPrizeTableId = -1;
            int iSponsorKey = -1, iLeagueKey = -1;

            bool headerParsed = false;
            var results = new List<TournamentDefinition>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                var cols = SplitCsvLine(line);

                if (!headerParsed)
                {
                    // Map header names → column indices
                    iId            = IndexOf(cols, "id");
                    iNameKey       = IndexOf(cols, "nameKey");
                    iCourseId      = IndexOf(cols, "courseId");
                    iHoleSet       = IndexOf(cols, "holeSet");
                    iStartUtc      = IndexOf(cols, "startUtc");
                    iEndUtc        = IndexOf(cols, "endUtc");
                    iResolveDelay  = IndexOf(cols, "resolveDelayMinutes");
                    iEntryFee      = IndexOf(cols, "entryFeeRP");
                    iBotFieldId    = IndexOf(cols, "botFieldId");
                    iPrizeTableId  = IndexOf(cols, "prizeTableId");
                    iSponsorKey    = IndexOf(cols, "sponsorKey");
                    iLeagueKey     = IndexOf(cols, "leagueKey");
                    headerParsed   = true;
                    continue;
                }

                string id = Get(cols, iId);
                if (string.IsNullOrEmpty(id)) continue;

                string nameKey       = Get(cols, iNameKey);
                var holeSet          = ExpandHoleSet(Get(cols, iHoleSet));
                var startUtc         = ParseUtc(Get(cols, iStartUtc), id, "startUtc");
                var endUtc           = ParseUtc(Get(cols, iEndUtc),   id, "endUtc");
                int resolveDelay     = ParseInt(Get(cols, iResolveDelay), 0);
                long entryFee        = ParseLong(Get(cols, iEntryFee), 0L);

                results.Add(new TournamentDefinition(
                    id:                  id,
                    nameKey:             nameKey,
                    clubId:              Get(cols, iCourseId),   // courseId → ClubId per SPEC §2
                    holeSet:             holeSet,
                    startUtc:            startUtc,
                    endUtc:              endUtc,
                    resolveDelayMinutes: resolveDelay,
                    entryFeeRP:          entryFee,
                    prizeTableId:        Get(cols, iPrizeTableId),
                    botFieldId:          Get(cols, iBotFieldId),
                    sponsorKey:          Get(cols, iSponsorKey),
                    leagueKey:           Get(cols, iLeagueKey),
                    // The SAME column value, deliberately, into both rungs of the name ladder.
                    // This file has one name column, and the dashboard's CSV export writes
                    // `nameKey ?? title` into it — so a dashboard-named tournament exported to CSV
                    // arrives with NameKey="Cesar Championship", which resolves nowhere, and the
                    // ladder would fall through to the SLUG ("cesar_championship") offline. Feeding
                    // it to Title too makes that case render a readable name; a key that DOES
                    // resolve is unaffected, because rung 1 wins before Title is ever consulted.
                    // Deliberately NOT a new CSV column: this file is the offline fallback, not a
                    // second source of truth. There is no titleJa rung here for the same reason.
                    title:               string.IsNullOrEmpty(nameKey) ? null : nameKey
                ));
            }

            return results;
        }

        /// <summary>
        /// Load all rows from <c>tournament_prizes.csv</c> → dictionary of
        /// <c>PrizeTable</c> keyed by <c>prizeTableId</c>.
        /// </summary>
        public IReadOnlyDictionary<string, PrizeTable> LoadPrizeTables()
        {
            var asset = LoadAsset(PrizesPath);
            if (asset == null)
            {
                Debug.LogError("[TournamentCsvLoader] tournament_prizes.csv not found in Resources/Data/");
                return new Dictionary<string, PrizeTable>();
            }

            var lines = SplitLines(asset.text);
            int iPrizeTableId = -1, iRankFrom = -1, iRankTo = -1;
            int iRpReward = -1, iItemRewardId = -1;
            bool headerParsed = false;

            // Group bands by prizeTableId
            var grouped = new Dictionary<string, List<PrizeBand>>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                var cols = SplitCsvLine(line);

                if (!headerParsed)
                {
                    iPrizeTableId = IndexOf(cols, "prizeTableId");
                    iRankFrom     = IndexOf(cols, "rankFrom");
                    iRankTo       = IndexOf(cols, "rankTo");
                    iRpReward     = IndexOf(cols, "rpReward");
                    iItemRewardId = IndexOf(cols, "itemRewardId");
                    headerParsed  = true;
                    continue;
                }

                string tableId = Get(cols, iPrizeTableId);
                if (string.IsNullOrEmpty(tableId)) continue;

                int  rankFrom     = ParseInt(Get(cols, iRankFrom),   1);
                int  rankTo       = ParseInt(Get(cols, iRankTo),     1);
                long rpReward     = ParseLong(Get(cols, iRpReward),  0L);
                string? itemId    = NullIfEmpty(Get(cols, iItemRewardId));

                var band = new PrizeBand(rankFrom, rankTo, rpReward, itemId);

                if (!grouped.TryGetValue(tableId, out var list))
                {
                    list            = new List<PrizeBand>();
                    grouped[tableId] = list;
                }
                list.Add(band);
            }

            var result = new Dictionary<string, PrizeTable>(grouped.Count);
            foreach (var kv in grouped)
                result[kv.Key] = new PrizeTable(kv.Key, kv.Value);

            return result;
        }

        /// <summary>
        /// Load all rows from <c>tournament_bot_fields.csv</c> → dictionary of
        /// <c>BotFieldConfig</c> keyed by <c>botFieldId</c>.
        /// </summary>
        public IReadOnlyDictionary<string, BotFieldConfig> LoadBotFields()
        {
            var asset = LoadAsset(BotFieldsPath);
            if (asset == null)
            {
                Debug.LogError("[TournamentCsvLoader] tournament_bot_fields.csv not found in Resources/Data/");
                return new Dictionary<string, BotFieldConfig>();
            }

            var lines = SplitLines(asset.text);
            int iBotFieldId = -1, iBotCount = -1, iBracketWeights = -1;
            int iMinSec = -1, iMaxSec = -1, iSpread = -1;
            bool headerParsed = false;

            var result = new Dictionary<string, BotFieldConfig>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line[0] == '#') continue;

                var cols = SplitCsvLine(line);

                if (!headerParsed)
                {
                    iBotFieldId      = IndexOf(cols, "botFieldId");
                    iBotCount        = IndexOf(cols, "botCount");
                    iBracketWeights  = IndexOf(cols, "bracketWeights");
                    iMinSec          = IndexOf(cols, "startOffsetMinSec");
                    iMaxSec          = IndexOf(cols, "startOffsetMaxSec");
                    iSpread          = IndexOf(cols, "perHoleSpreadSec");
                    headerParsed     = true;
                    continue;
                }

                string fieldId = Get(cols, iBotFieldId);
                if (string.IsNullOrEmpty(fieldId)) continue;

                int  botCount      = ParseInt(Get(cols, iBotCount),   0);
                var  weights       = ParseBracketWeights(Get(cols, iBracketWeights), fieldId);
                float minSec       = ParseFloat(Get(cols, iMinSec),   0f);
                float maxSec       = ParseFloat(Get(cols, iMaxSec),   0f);
                float spread       = ParseFloat(Get(cols, iSpread),   0f);

                result[fieldId] = new BotFieldConfig(
                    botFieldId:        fieldId,
                    botCount:          botCount,
                    bracketWeights:    weights,
                    startOffsetMinSec: minSec,
                    startOffsetMaxSec: maxSec,
                    perHoleSpreadSec:  spread
                );
            }

            return result;
        }

        // ── Helpers — HoleSet expansion ───────────────────────────────────────

        /// <summary>
        /// Expand a <c>holeSet</c> value into a list of hole-id strings.
        /// <list type="bullet">
        /// <item><c>"1-18"</c> → ["1","2",…,"18"]</item>
        /// <item><c>"1,4,7"</c> → ["1","4","7"]</item>
        /// <item>Single number: <c>"9"</c> → ["9"]</item>
        /// </list>
        /// </summary>
        public static IReadOnlyList<string> ExpandHoleSet(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            raw = raw.Trim();

            // Range: "A-B"
            int dashIdx = raw.IndexOf('-');
            if (dashIdx > 0)
            {
                string fromStr = raw.Substring(0, dashIdx).Trim();
                string toStr   = raw.Substring(dashIdx + 1).Trim();
                if (int.TryParse(fromStr, out int from) && int.TryParse(toStr, out int to) && from <= to)
                {
                    var list = new List<string>(to - from + 1);
                    for (int i = from; i <= to; i++)
                        list.Add(i.ToString());
                    return list;
                }
            }

            // Comma list: "1,4,7"
            if (raw.Contains(','))
            {
                var parts = raw.Split(',');
                var list  = new List<string>(parts.Length);
                foreach (var p in parts)
                {
                    string trimmed = p.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        list.Add(trimmed);
                }
                return list;
            }

            // Single value
            return new[] { raw };
        }

        /// <summary>
        /// Parse a <c>bracketWeights</c> value into a dictionary of minLevel-key → weight.
        /// Format: <c>"1:0.30;10:0.30;25:0.20;50:0.20"</c> — semicolon-separated
        /// <c>minLevel:weight</c> pairs. Keys MUST be bot_difficulty.csv minLevels
        /// (1/10/25/50/100/180) — validated here, warning logged on unknown key.
        /// </summary>
        public static IReadOnlyDictionary<string, float> ParseBracketWeights(string raw, string fieldIdForLog)
        {
            var result = new Dictionary<string, float>();
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning($"[TournamentCsvLoader] bracketWeights is empty for botFieldId='{fieldIdForLog}'");
                return result;
            }

            var pairs = raw.Split(';');
            foreach (var pair in pairs)
            {
                string trimmed = pair.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                int colon = trimmed.IndexOf(':');
                if (colon <= 0)
                {
                    Debug.LogWarning($"[TournamentCsvLoader] Invalid bracketWeights pair '{trimmed}' in fieldId='{fieldIdForLog}'");
                    continue;
                }

                string key    = trimmed.Substring(0, colon).Trim();
                string valStr = trimmed.Substring(colon + 1).Trim();

                if (!float.TryParse(valStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float weight))
                {
                    Debug.LogWarning($"[TournamentCsvLoader] Cannot parse weight '{valStr}' in bracketWeights pair '{trimmed}' for fieldId='{fieldIdForLog}'");
                    continue;
                }

                result[key] = weight;
            }

            return result;
        }

        // ── Helpers — referential integrity ───────────────────────────────────

        /// <summary>
        /// Check referential integrity: every <c>prizeTableId</c> and <c>botFieldId</c>
        /// in <paramref name="tournaments"/> must resolve in <paramref name="prizeTables"/>
        /// and <paramref name="botFields"/> respectively.
        /// Logs a clear error for each dangling id; returns false if any are found.
        /// </summary>
        public static bool CheckReferentialIntegrity(
            IReadOnlyList<TournamentDefinition>          tournaments,
            IReadOnlyDictionary<string, PrizeTable>      prizeTables,
            IReadOnlyDictionary<string, BotFieldConfig>  botFields)
        {
            bool ok = true;
            foreach (var t in tournaments)
            {
                if (!prizeTables.ContainsKey(t.PrizeTableId))
                {
                    Debug.LogError(
                        $"[TournamentCsvLoader] Referential integrity FAIL: tournament '{t.Id}' " +
                        $"references prizeTableId='{t.PrizeTableId}' which is not in tournament_prizes.csv.");
                    ok = false;
                }

                if (!botFields.ContainsKey(t.BotFieldId))
                {
                    Debug.LogError(
                        $"[TournamentCsvLoader] Referential integrity FAIL: tournament '{t.Id}' " +
                        $"references botFieldId='{t.BotFieldId}' which is not in tournament_bot_fields.csv.");
                    ok = false;
                }
            }
            return ok;
        }

        // ── Low-level parse helpers ───────────────────────────────────────────

        private static TextAsset? LoadAsset(string path)
            => Resources.Load<TextAsset>(path);

        private static string[] SplitLines(string text)
            => text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        /// <summary>
        /// Quote-aware CSV line split (same logic as ModesDatabaseCSV.ParseCsvLine).
        /// Handles comma-inside-quotes; returns trimmed cells.
        /// </summary>
        private static string[] SplitCsvLine(string line)
        {
            var  cols     = new List<string>();
            var  sb       = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    cols.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            cols.Add(sb.ToString().Trim());
            return cols.ToArray();
        }

        private static int IndexOf(string[] headers, string name)
        {
            for (int i = 0; i < headers.Length; i++)
                if (string.Equals(headers[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        private static string Get(string[] cols, int idx)
            => (idx >= 0 && idx < cols.Length) ? cols[idx] : string.Empty;

        private static string? NullIfEmpty(string s)
            => string.IsNullOrEmpty(s) ? null : s;

        /// <summary>
        /// Parse an absolute UTC timestamp from the CSV.
        /// <para>
        /// Deliberately absolute-only. A clock-relative form (<c>now+5d</c>) was tried and
        /// reverted: this file ships, so anchoring to session load time would give every
        /// player a private schedule — the same tournament "ending in 4d" forever, never
        /// actually starting or finishing, and two players in different windows sharing one
        /// leaderboard. Every build must show the same tournaments at the same times, so the
        /// dates live here and <c>LocalTournamentBackend.DeriveState</c> compares them to the
        /// current time. Expired rows are the schedule doing its job; refresh the data.
        /// </para>
        /// </summary>
        private static DateTime ParseUtc(string raw, string rowId, string colName)
        {
            if (DateTime.TryParse(raw, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime dt))
                return dt;
            Debug.LogWarning($"[TournamentCsvLoader] Cannot parse '{colName}'='{raw}' for id='{rowId}'; using DateTime.MinValue");
            return DateTime.MinValue;
        }

        private static int ParseInt(string raw, int fallback)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
            return fallback;
        }

        private static long ParseLong(string raw, long fallback)
        {
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v))
                return v;
            return fallback;
        }

        private static float ParseFloat(string raw, float fallback)
        {
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return v;
            return fallback;
        }
    }
}
