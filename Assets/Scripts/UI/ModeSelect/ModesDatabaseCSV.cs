using System.Collections.Generic;
using UnityEngine;
using GolfinRedux.UI;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Singleton that loads modes from Assets/Resources/Data/modes.csv at runtime.
    /// CSV columns: id, title, tagline, description, entryFee, rewards, locked, target, order,
    ///              versusStrokeCapOverPar, reward1Type, reward1Amount, reward2Type, reward2Amount,
    ///              reward3Type, reward3Amount
    /// The (type,amount) reward pair columns are parsed into ModeData.rewardList (Stage 2).
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

            // Parse header
            string[] headers = lines[0].Trim().Split(',');
            int iId = System.Array.IndexOf(headers, "id");
            int iTitle = System.Array.IndexOf(headers, "title");
            int iTagline = System.Array.IndexOf(headers, "tagline");
            int iDesc = System.Array.IndexOf(headers, "description");
            int iFee = System.Array.IndexOf(headers, "entryFee");
            int iRewards = System.Array.IndexOf(headers, "rewards");
            int iLocked = System.Array.IndexOf(headers, "locked");
            int iTarget = System.Array.IndexOf(headers, "target");
            int iOrder = System.Array.IndexOf(headers, "order");
            int iCapOverPar = System.Array.IndexOf(headers, "versusStrokeCapOverPar");
            // Stage 2 reward-pair columns (up to 3 pairs, matching HoleDatabaseLoader precedent)
            int iReward1Type   = System.Array.IndexOf(headers, "reward1Type");
            int iReward1Amount = System.Array.IndexOf(headers, "reward1Amount");
            int iReward2Type   = System.Array.IndexOf(headers, "reward2Type");
            int iReward2Amount = System.Array.IndexOf(headers, "reward2Amount");
            int iReward3Type   = System.Array.IndexOf(headers, "reward3Type");
            int iReward3Amount = System.Array.IndexOf(headers, "reward3Amount");

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Quote-aware split so fields (e.g. descriptions) may contain commas
                // by wrapping them in double quotes: id,Title,Tagline,"Desc, with commas",...
                string[] cols = ParseCsvLine(line);

                var mode = new ModeData();
                if (iId >= 0 && iId < cols.Length)     mode.id       = cols[iId].Trim();
                if (iTitle >= 0 && iTitle < cols.Length) mode.title   = cols[iTitle].Trim();
                if (iTagline >= 0 && iTagline < cols.Length) mode.tagline = cols[iTagline].Trim();
                if (iDesc >= 0 && iDesc < cols.Length)  mode.description = cols[iDesc].Trim();
                if (iFee >= 0 && iFee < cols.Length)    int.TryParse(cols[iFee].Trim(), out mode.entryFee);
                if (iRewards >= 0 && iRewards < cols.Length) int.TryParse(cols[iRewards].Trim(), out mode.rewards);
                if (iLocked >= 0 && iLocked < cols.Length) bool.TryParse(cols[iLocked].Trim(), out mode.locked);
                if (iTarget >= 0 && iTarget < cols.Length)  mode.target = cols[iTarget].Trim();
                if (iOrder >= 0 && iOrder < cols.Length)    int.TryParse(cols[iOrder].Trim(), out mode.order);
                if (iCapOverPar >= 0 && iCapOverPar < cols.Length) int.TryParse(cols[iCapOverPar].Trim(), out mode.versusStrokeCapOverPar);

                // Parse up to 3 reward pairs (Stage 2). Mirror HoleDatabaseLoader.ParseRewardType pattern.
                ParseAndAddRewardPair(cols, iReward1Type, iReward1Amount, mode);
                ParseAndAddRewardPair(cols, iReward2Type, iReward2Amount, mode);
                ParseAndAddRewardPair(cols, iReward3Type, iReward3Amount, mode);

                if (!string.IsNullOrEmpty(mode.id))
                    _modes.Add(mode);
            }

            // Sort by order column
            _modes.Sort((a, b) => a.order.CompareTo(b.order));

            Debug.Log($"[ModesDatabaseCSV] Loaded {_modes.Count} modes");
        }

        /// <summary>
        /// Parses one (typeCol, amountCol) reward pair from the CSV columns and appends to mode.rewardList.
        /// Silently skips if the columns are absent or the values are empty/invalid.
        /// </summary>
        private static void ParseAndAddRewardPair(string[] cols, int typeColIdx, int amountColIdx, ModeData mode)
        {
            if (typeColIdx < 0 || amountColIdx < 0) return;
            if (typeColIdx >= cols.Length || amountColIdx >= cols.Length) return;

            string typeStr   = cols[typeColIdx].Trim();
            string amountStr = cols[amountColIdx].Trim();

            if (string.IsNullOrEmpty(typeStr) || string.IsNullOrEmpty(amountStr)) return;
            if (!int.TryParse(amountStr, out int amount) || amount <= 0) return;

            RewardType rewardType = ParseRewardType(typeStr);
            mode.rewardList.Add(new HoleReward(rewardType, amount));
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
            var versus = new ModeData { id = "versus_1v1",   title = "Multiplayer",    tagline = "1v1",                               description = "Face off in fast-paced 1v1 golf matches where every shot matters. Master the course, outplay your opponent, and sink clutch putts to claim victory.", entryFee = 0, rewards = 200, locked = false, target = "matchmaking_1v1", order = 1, versusStrokeCapOverPar = 5 };
            versus.rewardList.Add(new HoleReward(RewardType.Points, 200));
            _modes.Add(versus);
            _modes.Add(new ModeData { id = "practice",     title = "PRACTICE",      tagline = "Sharpen your skills.",              description = "Practice on any course.",             entryFee = 100, rewards = 50,  locked = false, target = "hole_select",    order = 2 });
            _modes.Add(new ModeData { id = "driving_range",title = "DRIVING RANGE",  tagline = "Coming Soon.",                      description = "Practice long shots.",                entryFee = 0,   rewards = 0,   locked = true,  target = "none",           order = 3 });
            _modes.Add(new ModeData { id = "missions",     title = "MISSIONS",       tagline = "Coming Soon.",                      description = "Complete challenges for rewards.",     entryFee = 0,   rewards = 200, locked = true,  target = "none",           order = 4 });
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
