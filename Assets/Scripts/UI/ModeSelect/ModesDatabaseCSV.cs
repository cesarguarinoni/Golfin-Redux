#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Golfin.Content;
using GolfinRedux.UI;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Singleton that loads modes from Assets/Resources/Data/modes.csv at runtime.
    /// CSV columns: id, title, tagline, description, entryFee, rewards, locked, target, order,
    ///              versusStrokeCapOverPar, reward1Type, reward1Amount, reward2Type, reward2Amount,
    ///              reward3Type, reward3Amount, rewardsTextKey
    /// The (type,amount) reward pair columns are parsed into ModeData.rewardList (Stage 2).
    /// rewardsTextKey is optional — when present the card's REWARDS row shows that localized
    /// text instead of "x{rewards}" (tournaments: "Varies by tournament").
    ///
    /// <para>
    /// OVERLAID BY THE <c>modes</c> CONTENT CATALOG since game_modes_admin (§2) — the standard
    /// treatment (bundled row + patch by id, appended rows admitted, <c>is_active=false</c> drops
    /// the card, <c>RequireReady</c> so an EditMode run reads bundled only, next-launch effect I5).
    ///
    /// TWO THINGS HERE ARE NOT STANDARD.
    ///
    /// First, <c>entryFee</c> is a PRICE THE SERVER ENFORCES. A modes publish mirrors it into
    /// <c>golfin_mode_fees</c> and <c>POST /points/spend</c> refuses a <c>mode_entry_fee:&lt;id&gt;</c>
    /// debit that disagrees. So this overlay is what keeps the number on the card and the number the
    /// player is charged the same one; when it cannot (a publish landed mid-session), the server
    /// answers <c>fee_changed</c> and <see cref="ModeCardController"/> re-prices the card.
    ///
    /// Second, THE WITHHOLD RULE. An overlay can APPEND a mode — that is the point of appends — but
    /// a mode is only enterable if this build's <see cref="ModeSelectScreenController"/> knows how to
    /// route its <c>target</c>. A published <c>target</c> this build does not dispatch would render
    /// a card whose PLAY button does nothing, which is the one failure the whole content pipeline
    /// exists to prevent (§2: "a client missing information never shows a broken item"). Such a mode
    /// is WITHHELD with a warning. The routable set is read from
    /// <see cref="ModeSelectScreenController.CanDispatch"/> — the same <c>const</c>s its dispatch
    /// switch uses — so there is exactly one list, not two that drift.
    ///
    /// Note what this is NOT: <c>locked=true</c> still renders, as Coming Soon, exactly as today.
    /// Withheld means "this build cannot enter it at all"; locked means "nobody can enter it yet".
    /// Which makes flipping Missions live a PUBLISH rather than a build.
    /// </para>
    /// </summary>
    public class ModesDatabaseCSV : MonoBehaviour
    {
        public static ModesDatabaseCSV Instance { get; private set; }

        private const string CsvResourcePath = "Data/modes";

        private List<ModeData> _modes = new List<ModeData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadFromCSV();
            ApplyDemoLock();
        }

        /// <summary>
        /// demo_build_slice §3.4: close every mode except Practice, using the SAME 'locked'
        /// Coming-Soon treatment Driving Range / Missions already ship with. The carousel
        /// auto-centers on the first unlocked mode, so this leaves Practice front-and-center as
        /// the only playable mode. Runs after both the CSV and fallback paths. No-op in the full game.
        /// </summary>
        private void ApplyDemoLock()
        {
            if (!GolfinRedux.Demo.DemoGate.IsDemo) return;
            int closed = 0;
            foreach (var m in _modes)
                if (m.id != "practice" && !m.locked) { m.locked = true; closed++; }
            if (closed > 0)
                Debug.Log($"[ModesDatabaseCSV] Demo: closed {closed} non-practice mode(s) — Practice only.");
        }

        private void LoadFromCSV()
        {
            _modes.Clear();

            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(ModesDatabaseCSV))
                ? ContentCatalogStore.Catalog(ContentCatalogs.Modes)
                : null;

            var seen = new HashSet<string>();
            int overlaid = 0, deactivated = 0, withheld = 0;

            TextAsset csv = Resources.Load<TextAsset>(CsvResourcePath);
            if (csv == null)
            {
                Debug.LogError($"[ModesDatabaseCSV] Could not load CSV at Resources/{CsvResourcePath}.csv");
                // Fallback: populate minimal hardcoded data so UI doesn't break in editor
                AddFallbackModes();
                return;
            }

            string[] lines = csv.text.Split('\n');
            if (lines.Length < 2)
            {
                Debug.LogError("[ModesDatabaseCSV] CSV has no data rows");
                AddFallbackModes();
                return;
            }

            // Parse header into the index map ContentFields.Csv reads a bundled column by.
            //
            // This REPLACES the previous run of `int i<Column> = Array.IndexOf(...)` locals — one
            // per column, each re-checked against `cols.Length` at every row. Column names are now
            // declared once, at the point of use in BuildMode, which is also what lets an overlay
            // patch any of them without a matching local being added here (I4). Every column stays
            // OPTIONAL exactly as before: a name the header does not carry reads as "" / 0 / false.
            string[] headers = lines[0].Trim().Split(',');
            var headerIndex = new Dictionary<string, int>();
            for (int h = 0; h < headers.Length; h++) headerIndex[headers[h].Trim()] = h;

            int iId = System.Array.IndexOf(headers, "id");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Quote-aware split so fields (e.g. descriptions) may contain commas
                // by wrapping them in double quotes: id,Title,Tagline,"Desc, with commas",...
                string[] cols = ParseCsvLine(line);

                string id = (iId >= 0 && iId < cols.Length) ? cols[iId].Trim() : string.Empty;
                if (string.IsNullOrEmpty(id)) continue;
                seen.Add(id);

                ContentRow? patch = null;
                if (overlay != null) overlay.ById.TryGetValue(id, out patch);

                var fields = ContentFields.Csv(cols, headerIndex, patch);

                // I6 — a deactivated mode is withdrawn, not shown greyed out. `locked` is the
                // Coming Soon treatment; is_active=false is "this mode no longer exists".
                if (!fields.IsActive) { deactivated++; continue; }

                var mode = BuildMode(id, fields);
                if (mode == null) { withheld++; continue; }

                if (patch != null) overlaid++;
                _modes.Add(mode);
            }

            // APPEND — an overlay row for a mode the bundled CSV does not carry. This is how a new
            // mode reaches an INSTALLED build: as long as its `target` is one this build already
            // dispatches, the card is real and its PLAY button works. If it is not, BuildMode
            // withholds it — which is the whole reason appending a mode is safe to allow at all.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (string.IsNullOrEmpty(row.Id) || seen.Contains(row.Id)) continue;

                    var fields = ContentFields.OverlayOnly(row);
                    if (!fields.IsActive) { deactivated++; continue; }

                    var appended = BuildMode(row.Id, fields);
                    if (appended == null) { withheld++; continue; }

                    _modes.Add(appended);
                    overlaid++;
                }
            }

            // Sort by order column
            _modes.Sort((a, b) => a.order.CompareTo(b.order));

            Debug.Log($"[ModesDatabaseCSV] Loaded {_modes.Count} modes" +
                      (overlay == null
                          ? " — BUNDLED only, no modes overlay this launch."
                          : $" — overlay v{overlay.Version}: {overlaid} row(s) patched/appended, " +
                            $"{deactivated} deactivated, {withheld} withheld (unroutable target)."));
        }

        /// <summary>
        /// One mode from a bundled row, an overlay patch, or an overlay row alone — or NULL when
        /// this build cannot route its <c>target</c>, in which case it has already logged why.
        ///
        /// THE WITHHOLD IS THE ONLY PLACE THIS METHOD CAN RETURN NULL, and it is the invariant the
        /// spec names: a mode whose target this build does not dispatch must never become a card.
        /// The routable set comes from <see cref="ModeSelectScreenController.CanDispatch"/>, which
        /// is built from the same <c>const</c>s its dispatch switch uses — so a target added there
        /// is routable here with no second list to remember.
        /// </summary>
        private static ModeData? BuildMode(string id, ContentFields fields)
        {
            string target = fields.Get("target");

            if (!ModeSelectScreenController.CanDispatch(target))
            {
                Debug.LogWarning(
                    $"[ModesDatabaseCSV] Mode '{id}' has target '{target}', which this build does " +
                    "not dispatch — WITHHELD. A card that taps into nothing is worse than no card; " +
                    "ship a build that routes it, or publish target=none to show it as Coming Soon.");
                return null;
            }

            var mode = new ModeData
            {
                id                     = id,
                title                  = fields.Get("title"),
                tagline                = fields.Get("tagline"),
                description            = fields.Get("description"),
                entryFee               = fields.GetInt("entryFee"),
                rewards                = fields.GetInt("rewards"),
                locked                 = fields.GetBool("locked"),
                target                 = target,
                order                  = fields.GetInt("order"),
                versusStrokeCapOverPar = fields.GetInt("versusStrokeCapOverPar"),
                rewardsTextKey         = fields.Get("rewardsTextKey"),
            };

            // Up to 3 reward pairs (Stage 2). Mirrors HoleDatabaseLoader.ParseRewardType.
            AddRewardPair(fields, "reward1Type", "reward1Amount", mode);
            AddRewardPair(fields, "reward2Type", "reward2Amount", mode);
            AddRewardPair(fields, "reward3Type", "reward3Amount", mode);

            return mode;
        }

        /// <summary>
        /// Appends one (typeColumn, amountColumn) reward pair to mode.rewardList, reading through
        /// the overlay so a published reward pair patches the bundled one. Silently skips when
        /// either half is empty or the amount is not a positive number — an empty pair is how the
        /// CSV says "this mode has fewer than three rewards", not a parse failure.
        /// </summary>
        private static void AddRewardPair(ContentFields fields, string typeColumn, string amountColumn, ModeData mode)
        {
            string typeStr   = fields.Get(typeColumn);
            string amountStr = fields.Get(amountColumn);

            if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(amountStr)) return;
            if (!int.TryParse(amountStr, out int amount) || amount <= 0) return;

            mode.rewardList.Add(new HoleReward(ParseRewardType(typeStr), amount));
        }

        /// <summary>
        /// Parses a reward type string to RewardType enum. Mirrors HoleDatabaseLoader.ParseRewardType.
        /// </summary>
        private static RewardType ParseRewardType(string typeStr)
        {
            switch (typeStr.ToLower())
            {
                case "points":
                    return RewardType.Points;
                case "repairkit":
                case "repair kit":
                case "repair_kit":
                    return RewardType.RepairKit;
                case "ball":
                    return RewardType.Ball;
                default:
                    Debug.LogWarning($"[ModesDatabaseCSV] Unknown reward type: '{typeStr}', defaulting to Points");
                    return RewardType.Points;
            }
        }

        /// <summary>
        /// Splits one CSV line on commas, honoring double-quoted fields so a field
        /// may itself contain commas. A literal quote inside a quoted field is "".
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var cols = new List<string>();
            var sb = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    cols.Add(sb.ToString());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            cols.Add(sb.ToString());
            return cols.ToArray();
        }

        private void AddFallbackModes()
        {
            // RP amounts below MIRROR modes.csv and must move with it — they are what the game runs on
            // when the CSV fails to load, so a stale value here silently reinstates the pre-rebalance
            // economy (RP_REBALANCE.md, applied 2026-08-12: versus 200→20, practice fee 100→10 /
            // rewards 50→5, missions 200→20).
            var versus = new ModeData { id = "versus_1v1",   title = "Multiplayer",    tagline = "1v1",                               description = "Face off in fast-paced 1v1 golf matches where every shot matters. Master the course, outplay your opponent, and sink clutch putts to claim victory.", entryFee = 0, rewards = 20, locked = false, target = "matchmaking_1v1", order = 1, versusStrokeCapOverPar = 5 };
            versus.rewardList.Add(new HoleReward(RewardType.Points, 20));
            _modes.Add(versus);
            _modes.Add(new ModeData { id = "practice",     title = "PRACTICE",      tagline = "Sharpen your skills.",              description = "Practice on any course.",             entryFee = 10, rewards = 5,   locked = false, target = "hole_select",    order = 2 });
            _modes.Add(new ModeData { id = "driving_range",title = "DRIVING RANGE",  tagline = "Coming Soon.",                      description = "Practice long shots.",                entryFee = 0,   rewards = 0,   locked = true,  target = "none",           order = 3 });
            _modes.Add(new ModeData { id = "missions",     title = "MISSIONS",       tagline = "Coming Soon.",                      description = "Complete challenges for rewards.",     entryFee = 0,   rewards = 20,  locked = true,  target = "none",           order = 4 });
        }

        public List<ModeData> GetAllModes()
        {
            return new List<ModeData>(_modes);
        }

        public ModeData GetMode(string id)
        {
            return _modes.Find(m => m.id == id);
        }
    }
}
