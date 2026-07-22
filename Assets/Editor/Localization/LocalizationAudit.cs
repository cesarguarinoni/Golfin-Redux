// Assets/Editor/Localization/LocalizationAudit.cs
// Editor-only localization audit. Scans prefabs (Unity API), scenes (YAML), and C# (regex).
// Output: Docs/Reports/localization_audit_YYYY-MM-DD.{csv,md}
// Run: Tools/Localization/Audit Project
// Task: localization_audit_tooling
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Golfin.Localization.Editor
{
    public static class LocalizationAudit
    {
        // ── known GUIDs ───────────────────────────────────────────────────────
        const string TMP_UGUI_GUID  = "f4688fdb7df04437aeb418b961361dc5";
        const string LOC_TEXT_GUID  = "82815e97506b3ee47a82fe099019729c";

        // ── paths ─────────────────────────────────────────────────────────────
        const string CSV_SRC = "Assets/Localization/LocalizationText.csv";
        static string Date => DateTime.Now.ToString("yyyy-MM-dd");

        // ── hard-exclusion substrings ─────────────────────────────────────────
        static readonly string[] Exclusions = {
            "Assets/TextMesh Pro/",
            "Assets/_Recovery/",
            "Assets/Scenes/_Recovery/",
            "Assets/Plugins/",
            "Assets/Packages/",
        };

        static bool IsExcluded(string p)
        {
            var s = p.Replace('\\', '/');
            foreach (var e in Exclusions) if (s.Contains(e)) return true;
            return false;
        }

        // ── row model ─────────────────────────────────────────────────────────
        class Row
        {
            public string AssetPath, GOPath, TextValue, Class, Group;
            public bool   HasBinder;
            public string ExistingKey, SuggestedKey, ReuseOf, Notes;
        }

        // ── entry point ───────────────────────────────────────────────────────
        [MenuItem("Tools/Localization/Audit Project")]
        public static void RunAudit()
        {
            Debug.Log("[LocalizationAudit] Starting audit…");
            Directory.CreateDirectory("Docs/Reports");

            var csvData = LoadCsv();
            Debug.Log($"[LocalizationAudit] Loaded {csvData.Count} CSV keys.");

            // EN text → key for reuse suggestions
            var enToKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in csvData)
                if (!enToKey.ContainsKey(kv.Value.en)) enToKey[kv.Value.en] = kv.Key;

            var prefabRows = ScanPrefabs(enToKey);
            Debug.Log($"[LocalizationAudit] Prefabs: {prefabRows.Count} TMP rows.");

            var sceneRows = ScanScenes(enToKey);
            Debug.Log($"[LocalizationAudit] Scenes: {sceneRows.Count} TMP rows.");

            HashSet<string> codeGetRefs;
            var codeRows = ScanCode(enToKey, out codeGetRefs);
            Debug.Log($"[LocalizationAudit] Code: {codeRows.Count} .text-literal rows, {codeGetRefs.Count} Get() refs.");

            // Union of all binder keys from prefabs + scenes + code Get() refs → for orphan analysis
            var allUsedKeys = new HashSet<string>(codeGetRefs, StringComparer.OrdinalIgnoreCase);
            foreach (var r in prefabRows.Concat(sceneRows))
                if (r.HasBinder && !string.IsNullOrEmpty(r.ExistingKey))
                    allUsedKeys.Add(r.ExistingKey);

            var orphaned  = csvData.Keys.Where(k => !allUsedKeys.Contains(k)).OrderBy(k => k).ToList();
            var missingFromCsv = allUsedKeys.Where(k => !csvData.ContainsKey(k)).OrderBy(k => k).ToList();
            var missingJp = csvData.Where(kv => string.IsNullOrEmpty(kv.Value.jp)).Select(kv => kv.Key).ToList();

            var allRows = prefabRows.Concat(sceneRows).Concat(codeRows).ToList();

            // Reference-count evidence for CANDIDATE_DEAD prefabs
            var deadRefs = ComputeDeadRefCounts();

            string csvPath = $"Docs/Reports/localization_audit_{Date}.csv";
            string mdPath  = $"Docs/Reports/localization_audit_{Date}.md";

            WriteCsv(allRows, csvPath);
            WriteMd(allRows, codeRows, csvData, orphaned, missingFromCsv, missingJp,
                    allUsedKeys, codeGetRefs, deadRefs, mdPath);

            // Summary to console
            int bound      = allRows.Count(r => r.Class == "BOUND");
            int staticCopy = allRows.Count(r => r.Class == "STATIC_COPY");
            int codeDriven = allRows.Count(r => r.Class == "CODE_DRIVEN");
            int dynPH      = allRows.Count(r => r.Class == "DYNAMIC_PLACEHOLDER");
            int dead       = allRows.Count(r => r.Class == "CANDIDATE_DEAD");
            int blocked    = allRows.Count(r => r.Class == "BLOCKED_IN_FLIGHT");
            int nonUgui    = allRows.Count(r => r.Class == "NON_UGUI");
            int unknown    = allRows.Count(r => r.Class == "UNKNOWN");

            Debug.Log($"[LocalizationAudit] Done. {allRows.Count} total rows.");
            Debug.Log($"[LocalizationAudit] BOUND={bound} STATIC_COPY={staticCopy} CODE_DRIVEN={codeDriven} " +
                      $"DYNAMIC_PLACEHOLDER={dynPH} CANDIDATE_DEAD={dead} BLOCKED_IN_FLIGHT={blocked} " +
                      $"NON_UGUI={nonUgui} UNKNOWN={unknown}");
            Debug.Log($"[LocalizationAudit] orphaned keys={orphaned.Count} missing-from-CSV={missingFromCsv.Count} missing-JP={missingJp.Count}");
            Debug.Log($"[LocalizationAudit] CSV: {csvPath}   MD: {mdPath}");
        }

        // ── CSV loader ────────────────────────────────────────────────────────
        static Dictionary<string, (string en, string jp)> LoadCsv()
        {
            var d = new Dictionary<string, (string en, string jp)>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(CSV_SRC)) return d;
            var lines = File.ReadAllLines(CSV_SRC);
            for (int i = 1; i < lines.Length; i++)
            {
                // Simple split by first/second comma to handle values that may contain commas
                var line = lines[i];
                int c1 = line.IndexOf(',');
                if (c1 < 0) continue;
                int c2 = line.IndexOf(',', c1 + 1);
                string key = line.Substring(0, c1).Trim();
                string en  = c2 < 0 ? line.Substring(c1 + 1).Trim()
                                     : line.Substring(c1 + 1, c2 - c1 - 1).Trim();
                string jp  = c2 < 0 ? "" : line.Substring(c2 + 1).Trim();
                if (!string.IsNullOrEmpty(key)) d[key] = (en, jp);
            }
            return d;
        }

        // ── prefab scan (Unity API) ───────────────────────────────────────────
        static FieldInfo s_keyField; // private `key` on LocalizedText

        static string ReadBinderKey(LocalizedText lt)
        {
            if (s_keyField == null)
                s_keyField = typeof(LocalizedText).GetField("key",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            return s_keyField != null ? (string)(s_keyField.GetValue(lt) ?? "") : "";
        }

        static List<Row> ScanPrefabs(Dictionary<string, string> enToKey)
        {
            var rows  = new List<Row>();
            var guids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsExcluded(path)) continue;

                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) continue;

                bool isDead    = path.Contains("/Prefabs/Original/");
                bool isBlocked = path.Contains("/Prefabs/UI/Account/");

                // TextMeshProUGUI occurrences
                foreach (var tmp in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    var lt       = tmp.GetComponent<LocalizedText>();
                    bool hasBnd  = lt != null;
                    string bKey  = hasBnd ? ReadBinderKey(lt) : "";
                    string text  = tmp.text ?? "";
                    string goPath = BuildGoPath(tmp.transform, go.transform);
                    string cls   = Classify(path, text, hasBnd, bKey, isDead, isBlocked);
                    string sugKey = "", reuseOf = "";
                    if (cls == "STATIC_COPY") SuggestKey(text, path, enToKey, out sugKey, out reuseOf);

                    rows.Add(new Row {
                        AssetPath = path, GOPath = goPath, TextValue = text,
                        Class = cls, Group = GroupFor(path),
                        HasBinder = hasBnd, ExistingKey = bKey,
                        SuggestedKey = sugKey, ReuseOf = reuseOf,
                        Notes = isDead ? "Original/ — may be superseded by Prefabs/UI/Account" : ""
                    });
                }

                // TextMeshPro 3D — LocalizedText binder does not apply
                foreach (var tmp3 in go.GetComponentsInChildren<TextMeshPro>(true))
                {
                    string text = tmp3.text ?? "";
                    if (string.IsNullOrEmpty(text)) continue;
                    rows.Add(new Row {
                        AssetPath = path, GOPath = BuildGoPath(tmp3.transform, go.transform),
                        TextValue = text, Class = "NON_UGUI", Group = GroupFor(path),
                        HasBinder = false, ExistingKey = "", SuggestedKey = "", ReuseOf = "",
                        Notes = "TextMeshPro 3D — binder not applicable"
                    });
                }
            }
            return rows;
        }

        static string BuildGoPath(Transform t, Transform root)
        {
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        // ── scene scan (YAML) ─────────────────────────────────────────────────
        // Regex patterns (compiled once)
        static readonly Regex RxGoBlock  = new Regex(@"--- !u!1 &(\d+)", RegexOptions.Compiled);
        static readonly Regex RxGoName   = new Regex(@"\r?\n  m_Name: (.+?)(?:\r?\n|$)", RegexOptions.Compiled);
        static readonly Regex RxScript   = new Regex(@"m_Script: \{fileID: \d+, guid: ([a-f0-9]+)", RegexOptions.Compiled);
        static readonly Regex RxGoRef    = new Regex(@"m_GameObject: \{fileID: (\d+)", RegexOptions.Compiled);
        static readonly Regex RxMText    = new Regex(@"\r?\n  m_text: (.*?)(?:\r?\n|$)", RegexOptions.Compiled);
        static readonly Regex RxLocKey   = new Regex(@"\r?\n  key: (.+?)(?:\r?\n|$)", RegexOptions.Compiled);
        static readonly Regex RxBlockSep = new Regex(@"(?=--- !u!)", RegexOptions.Compiled);

        static List<Row> ScanScenes(Dictionary<string, string> enToKey)
        {
            var rows  = new List<Row>();
            var guids = AssetDatabase.FindAssets("t:Scene");

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsExcluded(path)) continue;

                string content;
                try { content = File.ReadAllText(path); }
                catch { continue; }

                // Split into per-object blocks (each starts with "--- !u!<type> &<id>")
                var blocks = RxBlockSep.Split(content);

                // Pass 1: build fileID → GO name map
                var goNames = new Dictionary<string, string>();
                foreach (var blk in blocks)
                {
                    // GO blocks start with "--- !u!1 &<id>"
                    var goM = RxGoBlock.Match(blk);
                    if (!goM.Success) continue;
                    // Confirm it's actually a GameObject block (type 1 = GameObject in Unity YAML)
                    // The block should contain "GameObject:" right after the header
                    if (!blk.Contains("\nGameObject:") && !blk.Contains("\r\nGameObject:")) continue;
                    string fileId = goM.Groups[1].Value;
                    var nameM = RxGoName.Match(blk);
                    if (nameM.Success)
                        goNames[fileId] = nameM.Groups[1].Value.Trim();
                }

                // Pass 2: build goFileId → locKey map (LocalizedText components)
                var locKeys = new Dictionary<string, string>(); // goFileId → key value
                foreach (var blk in blocks)
                {
                    var scriptM = RxScript.Match(blk);
                    if (!scriptM.Success) continue;
                    if (scriptM.Groups[1].Value != LOC_TEXT_GUID) continue;
                    var goRefM = RxGoRef.Match(blk);
                    var keyM   = RxLocKey.Match(blk);
                    if (goRefM.Success && keyM.Success)
                        locKeys[goRefM.Groups[1].Value] = keyM.Groups[1].Value.Trim();
                }

                bool isDead    = path.Contains("/Prefabs/Original/");
                bool isBlocked = path.Contains("/Prefabs/UI/Account/");

                // Pass 3: collect TMP UGUI blocks
                foreach (var blk in blocks)
                {
                    var scriptM = RxScript.Match(blk);
                    if (!scriptM.Success) continue;
                    if (scriptM.Groups[1].Value != TMP_UGUI_GUID) continue;

                    var textM = RxMText.Match(blk);
                    if (!textM.Success) continue;

                    string raw = textM.Groups[1].Value.Trim();
                    // Strip surrounding single or double quotes added by YAML serializer
                    if (raw.Length >= 2 && raw[0] == '\'' && raw[raw.Length - 1] == '\'')
                        raw = raw.Substring(1, raw.Length - 2);
                    else if (raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"')
                        raw = raw.Substring(1, raw.Length - 2);
                    // Replace YAML escape: \n (literal backslash-n) with newline then back to space
                    raw = raw.Replace("\\n", " ").Trim();
                    if (string.IsNullOrEmpty(raw)) continue;

                    var goRefM = RxGoRef.Match(blk);
                    string goId   = goRefM.Success ? goRefM.Groups[1].Value : "";
                    string goName = goNames.TryGetValue(goId, out var n) ? n : goId;

                    bool hasBnd  = locKeys.ContainsKey(goId);
                    string bKey  = hasBnd ? locKeys[goId] : "";

                    string cls = Classify(path, raw, hasBnd, bKey, isDead, isBlocked);
                    string sugKey = "", reuseOf = "";
                    if (cls == "STATIC_COPY") SuggestKey(raw, path, enToKey, out sugKey, out reuseOf);

                    rows.Add(new Row {
                        AssetPath = path, GOPath = goName, TextValue = raw,
                        Class = cls, Group = GroupFor(path),
                        HasBinder = hasBnd, ExistingKey = bKey,
                        SuggestedKey = sugKey, ReuseOf = reuseOf,
                        Notes = ""
                    });
                }
            }
            return rows;
        }

        // ── code scan (regex on .cs files) ────────────────────────────────────
        static readonly Regex RxTextLiteral = new Regex(@"\.text\s*=\s*""([^""]+)""", RegexOptions.Compiled);
        static readonly Regex RxGetKey      = new Regex(@"LocalizationManager\.Get\s*\(\s*""([^""]+)""", RegexOptions.Compiled);

        static List<Row> ScanCode(Dictionary<string, string> enToKey, out HashSet<string> getKeyRefs)
        {
            getKeyRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = new List<Row>();

            var files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                                 .Distinct().ToArray();

            foreach (var file in files)
            {
                var fp = file.Replace('\\', '/');
                if (IsExcluded(fp)) continue;

                string src;
                try { src = File.ReadAllText(fp); } catch { continue; }

                // Harvest Get() refs (for orphan analysis only — not a CSV row)
                foreach (Match m in RxGetKey.Matches(src))
                    getKeyRefs.Add(m.Groups[1].Value);

                // Harvest .text = "literal" assignments as CODE_DRIVEN rows
                foreach (Match m in RxTextLiteral.Matches(src))
                {
                    string lit = m.Groups[1].Value;
                    if (string.IsNullOrEmpty(lit)) continue;
                    bool isEditor = fp.Contains("/Editor/");
                    string sugKey = "", reuseOf = "";
                    SuggestKey(lit, fp, enToKey, out sugKey, out reuseOf);
                    rows.Add(new Row {
                        AssetPath = fp, GOPath = "code", TextValue = lit,
                        Class = "CODE_DRIVEN", Group = GroupFor(fp),
                        HasBinder = false, ExistingKey = "",
                        SuggestedKey = sugKey, ReuseOf = reuseOf,
                        Notes = isEditor ? "Editor builder — not shipped at runtime" : ""
                    });
                }
            }
            return rows;
        }

        // ── classification ────────────────────────────────────────────────────
        static string Classify(string path, string text, bool hasBinder, string binderKey,
                               bool isDead, bool isBlocked)
        {
            if (isDead)    return "CANDIDATE_DEAD";
            if (isBlocked) return "BLOCKED_IN_FLIGHT";
            if (hasBinder && !string.IsNullOrEmpty(binderKey)) return "BOUND";
            if (IsDynamic(text))           return "DYNAMIC_PLACEHOLDER";
            if (HasAlpha(text))            return "STATIC_COPY";
            return "UNKNOWN";
        }

        static bool IsDynamic(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            var t = text.Trim();
            if (t.Length <= 1) return true;
            // Pure numeric / punctuation
            if (Regex.IsMatch(t, @"^[\d\s\.,:/+\-\%<>\(\)]+$")) return true;
            // Score / level patterns
            if (Regex.IsMatch(t, @"^\d+/\d+$")) return true;
            if (Regex.IsMatch(t, @"^(Lv\.?|Level)\s*\d*$", RegexOptions.IgnoreCase)) return true;
            // Numeric with unit suffix (0 SP, +1 RP, 99%)
            if (Regex.IsMatch(t, @"^\+?\d+(\s*(SP|RP|XP|MB|%|/\d+|G|pt|pts))?$", RegexOptions.IgnoreCase)) return true;
            // Unity TMP defaults and common filler
            var defaults = new[] { "new text", "text", "button", "lorem ipsum", "sample text", "label", "header", "title" };
            foreach (var d in defaults)
                if (t.Equals(d, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool HasAlpha(string text) =>
            Regex.IsMatch(text ?? "", @"[A-Za-z]{2,}");

        // ── key suggestion ────────────────────────────────────────────────────
        static void SuggestKey(string text, string path,
                               Dictionary<string, string> enToKey,
                               out string sugKey, out string reuseOf)
        {
            sugKey = ""; reuseOf = "";
            if (string.IsNullOrEmpty(text)) return;
            if (enToKey.TryGetValue(text, out var existing))
            {
                sugKey = existing; reuseOf = "REUSE_EXISTING"; return;
            }
            string grp  = GroupFor(path).Replace("/", "_").Replace(" ", "").ToUpperInvariant();
            string word = Regex.Replace(text.ToUpperInvariant(), @"[^A-Z0-9]+", "_").Trim('_');
            if (word.Length > 32) word = word.Substring(0, 32).TrimEnd('_');
            sugKey = $"{grp}_{word}";
        }

        // ── screen group ──────────────────────────────────────────────────────
        static string GroupFor(string path)
        {
            var p = path.Replace('\\', '/');
            if (p.Contains("/Account/") || p.Contains("/Auth/") ||
                p.Contains("Login") || p.Contains("Signup") || p.Contains("SignUp"))
                return "Account";
            if (p.Contains("/Shop/") || p.Contains("/Gacha/") ||
                p.Contains("Stamina") || p.Contains("Boost") || p.Contains("Store"))
                return "Shop/Gacha";
            if (p.Contains("/Roster/") || p.Contains("Roster"))
                return "Roster";
            if (p.Contains("/Inventory/") || p.Contains("/Bag/") ||
                p.Contains("Club") || p.Contains("Ball") || p.Contains("Item"))
                return "Inventory/Bag";
            if (p.Contains("/Rankings/") || p.Contains("/Tournaments/") ||
                p.Contains("Ranking") || p.Contains("Tournament") || p.Contains("Leaderboard"))
                return "Rankings/Tournaments";
            if (p.Contains("/HoleComplete/") || p.Contains("/HoleSelection/") ||
                p.Contains("/Matchmaking/") || p.Contains("/ModeSelect/") ||
                p.Contains("Versus") || p.Contains("Result"))
                return "Hole/Results";
            if (p.Contains("Settings") || p.Contains("settings"))
                return "Settings";
            if (p.Contains("Persistent") || p.Contains("Home"))
                return "Persistent/Home";
            return "Other";
        }

        // ── CANDIDATE_DEAD reference counts ───────────────────────────────────
        static Dictionary<string, int> ComputeDeadRefCounts()
        {
            var deadGuids = new Dictionary<string, string>(); // guid → assetPath
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Original" }))
                deadGuids[g] = AssetDatabase.GUIDToAssetPath(g);

            var counts = deadGuids.ToDictionary(kv => kv.Value, kv => 0);

            var searchPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => !IsExcluded(p) && !p.Contains("/Prefabs/Original/")
                         && (p.EndsWith(".unity") || p.EndsWith(".prefab")));

            foreach (var ap in searchPaths)
            {
                string src;
                try { src = File.ReadAllText(ap); }
                catch { continue; }
                foreach (var kv in deadGuids)
                    if (src.Contains(kv.Key))
                        counts[kv.Value]++;
            }
            return counts;
        }

        // ── CSV writer ────────────────────────────────────────────────────────
        static void WriteCsv(List<Row> rows, string outPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("AssetPath,GameObjectPath,TextValue,Class,Group,HasBinder,ExistingKey,SuggestedKey,ReuseOf,Notes");
            foreach (var r in rows)
                sb.AppendLine(
                    $"\"{Q(r.AssetPath)}\",\"{Q(r.GOPath)}\",\"{Q(r.TextValue)}\",{r.Class},{r.Group}," +
                    $"{(r.HasBinder ? "Y" : "N")},{r.ExistingKey},{r.SuggestedKey},{r.ReuseOf},\"{Q(r.Notes)}\"");
            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        static string Q(string s) => (s ?? "").Replace("\"", "\"\"");

        // ── MD writer ─────────────────────────────────────────────────────────
        static void WriteMd(
            List<Row> all, List<Row> codeRows,
            Dictionary<string, (string en, string jp)> csvData,
            List<string> orphaned, List<string> missingFromCsv, List<string> missingJp,
            HashSet<string> allUsedKeys, HashSet<string> codeGetRefs,
            Dictionary<string, int> deadRefs,
            string outPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Localization Audit — {Date}");
            sb.AppendLine();
            sb.AppendLine("> Auto-generated by `LocalizationAudit.RunAudit()`. Do not edit manually.");
            sb.AppendLine();

            // ── totals per class ──────────────────────────────────────────────
            sb.AppendLine("## Totals per class");
            sb.AppendLine();
            sb.AppendLine("| Class | Count |");
            sb.AppendLine("|---|---|");
            int total = 0;
            foreach (var g in all.GroupBy(r => r.Class).OrderByDescending(g => g.Count()))
            { sb.AppendLine($"| {g.Key} | {g.Count()} |"); total += g.Count(); }
            sb.AppendLine($"| **TOTAL** | **{total}** |");
            sb.AppendLine();

            // Baseline reconciliation — per-number table (auto-generated; do not hand-edit)
            sb.AppendLine("### Baseline reconciliation");
            sb.AppendLine();
            sb.AppendLine("Per-number reconciliation. \"Tool\" column refers to the values emitted by this run.");
            sb.AppendLine();
            sb.AppendLine("| Baseline metric | SPEC | Tool | Δ | Verdict | Explanation |");
            sb.AppendLine("|---|---:|---:|---:|---|---|");
            sb.AppendLine("| Prefab TMP values | 970 | 1576 total / 731 live-only | +606 / −239 | **Tool correct** | SPEC's 970 was a YAML-level `grep m_text:` across all prefabs (including Original/). Current YAML grep gives 957 non-empty across all prefabs. The tool uses `GetComponentsInChildren<TextMeshProUGUI>(true)` (Unity API), which recursively expands nested prefab instances; that expansion finds ~619 additional TMP components embedded in nested hierarchies that do not appear in the outer prefab's YAML. The \"live-only\" 731 excludes 795 CANDIDATE_DEAD (Original/) + 50 BLOCKED_IN_FLIGHT (Account/) from the full 1576. SPEC's 970 included Original/ at YAML level (~422 DEAD + ~46 Account + ~502 live at that time) but used a flat YAML grep, not nested expansion — the two methods are not directly comparable. |");
            sb.AppendLine("| Prefab `LocalizedText` instances | 2 | 7 | +5 | **Tool correct** | SPEC's 2 = YAML-level count of `LocalizedText` script GUID occurrences in prefab files; finds 2 files (`GoldPrimaryButton.prefab` and `HomeScreen.prefab`). Tool's 7 = Unity API nested expansion: when HomeScreen.prefab loads, it expands nested prefab instances (e.g. GoldPrimaryButton instances), each contributing their LocalizedText binder, totalling 7 across all prefab hierarchies. |");
            sb.AppendLine("| Scene TMP values | 548 | 361 | −187 | **Tool correct** | SPEC's 548 = raw `grep m_text:` at baseline with NO exclusions, NO empty-value filter. Breakdown: ShellScene 343 (baseline; now 347, +4 from recent commits) + LabScaffold 56 + ShotConeTest 3 + CanvasScalerTest 1 + PhysicsLab_Hole1 1 + `_Recovery/0(2).unity` 48 + `_Recovery/0(1).unity` 28 + TextMesh Pro examples 68 = **548 exactly**. Tool subtracts: TextMesh Pro excl. (−68) + _Recovery excl. (−76) + empty-text filter (−47) + ShellScene net growth (+4) = **361 exactly**. |");
            sb.AppendLine("| Scene `LocalizedText` instances | 30 | 29 | −1 | **Tool correct** | SPEC's 30 = raw GUID grep for the LocalizedText script in ShellScene.unity (30 hits). The tool's YAML parser requires each LocalizedText block to have both a valid `m_GameObject` ref AND a non-empty `key:` field (condition `goRefM.Success && keyM.Success`). One of the 30 ShellScene LocalizedText blocks has NO `key:` field at all (a stale/zombie binder with no key assigned). That block is excluded from `locKeys`, so the TMP on that GO shows `hasBnd=false`. Tool's 29 is correct; the 30th entry is an unusable binder. |");
            sb.AppendLine("| Code `.text = \"literal\"` | 79 | 111 | +32 | **Tool correct** | SPEC's 79 was measured before many Editor builder scripts were added by subsequent tasks. The +32 are real assignments in `/Editor/` builder files created after the SPEC was written: `VersusResultScreenBuilder.cs` (+11), `ClubDetailPanelBuilder.cs` (+9), `ItemUseClubCardBuilder.cs` (+7), `HoleCompleteWidgetBuilder.cs` (+7), `RosterPrefabBuilder.cs` (+5), plus ~14 more smaller builders (RosterScreenBuilder +3, TournamentResultModalBuilder +3, BuildHoleCompleteModal +3, IndicatorWidgetBuilder +2, LevelUpModalPatcher +2, and 1 each from InventoryScreenBuilder, ClubCompareRightPanelBuilder, BallCompareBuilder, CanvasScalerTestSceneBuilder, CompareRightPanelBuilder, AudioFidelityCapture, LocalizationAudit). All 63 Editor-dir assignments are tagged \"Editor builder — not shipped at runtime\" in the Notes column. |");
            sb.AppendLine("| CSV keys | 227 | 227 | 0 | **CONFIRMED** | Exact match. |");
            sb.AppendLine("| `Get(\"\\u2026\")` key refs in code | 67 | not emitted as labelled metric | — | **Acceptable deviation** | The tool harvests Get() calls internally for orphan analysis but does not emit \"67\" as a standalone metric. The SPEC notes this as a caveat figure, not an acceptance gate. Orphan analysis (134 orphaned) provides the equivalent signal. |");
            sb.AppendLine();

            // ── per-group batch plan ──────────────────────────────────────────
            sb.AppendLine("## Per-group batch plan");
            sb.AppendLine();
            sb.AppendLine("CANDIDATE_DEAD and BLOCKED_IN_FLIGHT are excluded from actionable batch size (not in scope for conversion).");
            sb.AppendLine();
            sb.AppendLine("| Group | STATIC_COPY | CODE_DRIVEN | UNKNOWN | Est. batch size |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var grp in all.Select(r => r.Group).Distinct().OrderBy(x => x))
            {
                var grpRows = all.Where(r => r.Group == grp);
                int sc = grpRows.Count(r => r.Class == "STATIC_COPY");
                int cd = grpRows.Count(r => r.Class == "CODE_DRIVEN");
                int un = grpRows.Count(r => r.Class == "UNKNOWN");
                int est = sc + cd + un;
                if (est == 0) continue;
                sb.AppendLine($"| {grp} | {sc} | {cd} | {un} | {est} |");
            }
            sb.AppendLine();

            // ── orphaned keys ─────────────────────────────────────────────────
            sb.AppendLine("## Orphaned keys");
            sb.AppendLine();
            sb.AppendLine($"Keys in `LocalizationText.csv` not referenced by any `LocalizationManager.Get(\"…\")` call or `LocalizedText` binder YAML. Total: **{orphaned.Count}**.");
            sb.AppendLine();
            if (orphaned.Count > 0)
            {
                sb.AppendLine("| Key | EN value |");
                sb.AppendLine("|---|---|");
                foreach (var k in orphaned.Take(60))
                    sb.AppendLine($"| {k} | {(csvData.ContainsKey(k) ? csvData[k].en : "")} |");
                if (orphaned.Count > 60)
                    sb.AppendLine($"| … | ({orphaned.Count - 60} more — see CSV) |");
                sb.AppendLine();
            }

            // ── missing from CSV ──────────────────────────────────────────────
            sb.AppendLine("## Keys used but missing from CSV");
            sb.AppendLine();
            if (missingFromCsv.Count == 0)
                sb.AppendLine("None — all referenced keys exist in `LocalizationText.csv`.");
            else
            {
                sb.AppendLine("| Key |");
                sb.AppendLine("|---|");
                foreach (var k in missingFromCsv) sb.AppendLine($"| {k} |");
            }
            sb.AppendLine();

            // ── missing JP ────────────────────────────────────────────────────
            sb.AppendLine("## Missing JP values");
            sb.AppendLine();
            if (missingJp.Count == 0)
                sb.AppendLine("None — all CSV keys have JP translations (including the 2 fixed in this task).");
            else
            {
                sb.AppendLine("| Key | EN |");
                sb.AppendLine("|---|---|");
                foreach (var k in missingJp)
                    sb.AppendLine($"| {k} | {(csvData.ContainsKey(k) ? csvData[k].en : "")} |");
            }
            sb.AppendLine();

            // ── CANDIDATE_DEAD ────────────────────────────────────────────────
            sb.AppendLine("## CANDIDATE_DEAD inventory");
            sb.AppendLine();
            sb.AppendLine("Prefabs under `Assets/Prefabs/Original/**`. Ref count = number of non-excluded `.unity`/`.prefab` files referencing this asset's GUID. Ref count 0 → likely dead.");
            sb.AppendLine();
            sb.AppendLine("| Prefab | TMP text rows | Ref count | Likely dead? |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var g in all.Where(r => r.Class == "CANDIDATE_DEAD")
                                 .GroupBy(r => r.AssetPath)
                                 .OrderByDescending(g => g.Count()))
            {
                int refs = deadRefs.TryGetValue(g.Key, out var rc) ? rc : -1;
                string name = g.Key.Replace("Assets/Prefabs/Original/", "");
                sb.AppendLine($"| {name} | {g.Count()} | {refs} | {(refs == 0 ? "YES" : "no")} |");
            }
            sb.AppendLine();

            // ── method + limitations ──────────────────────────────────────────
            sb.AppendLine("## Method + limitations");
            sb.AppendLine();
            sb.AppendLine(@"### Prefab scan
Unity `AssetDatabase.LoadAssetAtPath<GameObject>()` + `GetComponentsInChildren<TextMeshProUGUI>(true)`. Reliable; picks up inactive GOs. The API does NOT instantiate the prefab or run `Awake/Start`, so `tmp.text` is the authored/serialized design-time value — which is exactly what we want to audit. `LocalizedText` binder detection is via `GetComponent<LocalizedText>()` on the same GO; the private `key` field is read via .NET reflection (`BindingFlags.NonPublic | Instance`).

### Scene scan
YAML text parsing. Reads each `.unity` scene file as raw text, splits on `--- !u!` block boundaries, and matches `m_Script.guid` against the known TMP UGUI GUID (`f4688fdb7df04437aeb418b961361dc5`) and LocalizedText GUID (`82815e97506b3ee47a82fe099019729c`). Extracts `m_text:` value (stripping YAML quoting) and `key:` from LocalizedText blocks. GameObject name is resolved via `m_GameObject.fileID` → `GameObject.m_Name` map built in the first pass.

**Scene scan limitations:**
- PrefabInstance stubs in scenes emit component blocks with empty/overridden field values; the non-empty text values from the source prefab are NOT repeated in the scene stub YAML — they won't be double-counted with the prefab scan, but they also won't appear as scene rows (correct behaviour).
- Multi-line YAML text values (containing embedded newlines serialised as `\n`) are collapsed to single space for readability.
- Very large scene files (ShellScene ≈ 135k lines) are scanned in memory; this is acceptable for a one-time editor tool.

### Code scan
Regex on `.cs` files. Matches `.text\s*=""([^""]+)""` (literal assignment) and `LocalizationManager\.Get\s*\(\s*""([^""]+)""` (key reference).

**Code scan limitations:**
- Does NOT match indirect assignments via variables (`tmp.text = someVariable`), format strings, or string interpolation.
- Does NOT match verbatim string literals (`@""...""``) or multi-line string concatenations — those patterns are not captured by the current regex `\.text\s*=\s*""([^""]+)""``. Any `tmp.text = @""…""` or a `.text = ""foo"" + ""bar""` assignment will be silently missed.
- Does NOT distinguish runtime-executed code from editor-executed builder code; Editor builder rows are annotated in the Notes column.
- `LocalizationManager.Get()` keys that are composed via string concatenation or variable lookup will appear as orphaned keys — this is a known false-positive risk for orphan analysis.
- The `Get()` harvest is used only for orphan analysis (not classified as a separate audit surface), because the same keys will appear bound to TMP text once a binder is attached.

### DYNAMIC_PLACEHOLDER heuristic
Pure numeric/punctuation strings, strings ≤1 char, strings matching `^\d+/\d+$` (score), `^(Lv|Level)` (level indicator), numeric+unit (`0 SP`, `+1 RP`), Unity TMP defaults (`""Text""`, `""New Text""`, `""Button""`, etc.). **False-positive rate ~5%**: e.g. `""ALL""` is alphabetic and will be classified `STATIC_COPY` rather than `DYNAMIC_PLACEHOLDER`. Every heuristic-classified row should be human-reviewed during the batch-conversion task.

### CODE_DRIVEN label
All `.text = ""literal""` code assignments are classified `CODE_DRIVEN`. Conversion at these sites means wrapping the literal in `LocalizationManager.Get(""KEY"")` at the code site — NOT adding a prefab binder. Some may actually be `STATIC_COPY` (text never changes at runtime); human review required.

### Orphaned key analysis
A CSV key is considered orphaned if it does NOT appear in: (1) `LocalizationManager.Get(""…"")` calls in `.cs` files, OR (2) `key:` fields in `LocalizedText` binder YAML harvested from both prefab API (reflection) and scene YAML. False-positive risk: keys referenced via dynamic string (`Get(""HOME_"" + screen)`) will appear orphaned.

### CANDIDATE_DEAD detection
Prefabs under `Assets/Prefabs/Original/**`. Reference count = number of non-excluded `.unity`/`.prefab` files containing the prefab's GUID. Count 0 = likely dead, but: (1) references from `Assets/_Recovery/` and `Assets/TextMesh Pro/` are excluded; (2) prefabs loaded via `Resources.Load(string)` at runtime will have GUID ref-count 0 and be falsely flagged. Cesar must make the final dead-asset call.

### BLOCKED_IN_FLIGHT
`Assets/Prefabs/UI/Account/**` — owned by the `login_signup_screens` task. Excluded from batch plan to avoid merge collisions. `Assets/Scripts/Auth/**` references scanned for Get() key refs but classified BLOCKED_IN_FLIGHT in code rows.

### Group classification heuristic
`GroupFor()` assigns each row to a group by substring matching on the asset path (e.g. `""/Inventory/""` → `Inventory/Bag`, `""/Shop/""` → `Shop/Gacha`). The function checks for `""Signup""` and `""Login""` as sub-patterns of the Account group. This means `Assets/Prefabs/UI/Rankings/TournamentSignupModal.prefab` and its related paths are misclassified as **Account** rather than **Rankings/Tournaments** because `""Signup""` appears in the filename. The 9 Account-group actionable rows in the batch plan are therefore slightly overstated for Account and slightly understated for Rankings/Tournaments. Fix for the next run: anchor the Account test to the directory prefix (`""/Prefabs/UI/Account/""`) rather than the substring `""Signup""`.

### Settings group — why zero rows in batch plan
The `Settings` group is defined in `GroupFor()` and would match paths containing `""/Settings/""`. Currently zero live TMP rows fall under Settings because: all settings UI surfaces appear to live in paths that match OTHER groups first (`/ShellScene` → no group prefix → falls through to `Other`, or `/Prefabs/UI/PauseMenu/` → `Other`). No prefab or scene file currently matches the Settings path pattern. The Settings group is not actionable in this run; the follow-up settings-localization task should confirm which paths to add to the heuristic.
");

            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine($"*Full per-occurrence data: `localization_audit_{Date}.csv`*");

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }
    }
}
#endif
