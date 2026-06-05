using System.Collections.Generic;
using UnityEngine;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Singleton that loads modes from Assets/Resources/Data/modes.csv at runtime.
    /// CSV columns: id, title, tagline, description, entryFee, rewards, locked, target, order
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

                if (!string.IsNullOrEmpty(mode.id))
                    _modes.Add(mode);
            }

            // Sort by order column
            _modes.Sort((a, b) => a.order.CompareTo(b.order));

            Debug.Log($"[ModesDatabaseCSV] Loaded {_modes.Count} modes");
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
            _modes.Add(new ModeData { id = "versus_1v1",   title = "Multiplayer",    tagline = "1v1",                               description = "Face off in fast-paced 1v1 golf matches where every shot matters. Master the course, outplay your opponent, and sink clutch putts to claim victory.", entryFee = 0, rewards = 200, locked = false, target = "matchmaking_1v1", order = 1 });
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
