// Build rule 5 — machine-check a built prefab against its node geometry sheet.
//
// The sheet (reference/nodes/<Screen>_geometry.json) carries every RectTransform the Figma frame
// declares, in FIGMA coordinates: x right and y DOWN from the parent's top-left. The builders'
// Rect() helper anchors top-left with a top-left pivot and stores y negated, so the comparison is
// exact arithmetic, not a tolerance on eyeballed numbers. A site the prefab does not have at all
// is GONE, which is a different failure from being in the wrong place and is reported separately.
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Golfin.Gps.UI.Editor
{
    public static class GpsGeometryAudit
    {
        const string Dir = "Docs/Specs/Completed/auth_golf_profile/reference/nodes";
        const string GiftVoteDir = "Docs/Specs/Active/gps_gifts_votes/reference/nodes";
        const string OutDir = "Docs/Diagnostics/_capture";

        /// <summary>Half a pixel: the sheet carries the node's own fractional widths
        /// (284.6667), so an exact-equality test would fail on float round-trip alone.</summary>
        const float Tol = 0.5f;

        [MenuItem("GOLFIN/Gps/Audit Auth Extras Geometry", priority = 214)]
        public static void Run()
            => Audit(Dir, "auth_golf_profile_geometry_audit.txt",
                     "GpsGolfProfileScreen_geometry.json", "GpsWelcomeScreen_geometry.json");

        /// <summary>gps_gifts_votes — the same audit over the two new sheets. A second menu item
        /// rather than a second copy of the routine: the sheet format is identical, so only the
        /// directory and the file list differ.</summary>
        [MenuItem("GOLFIN/Gps/Audit Gift + Vote Geometry", priority = 223)]
        public static void RunGiftVote()
            => Audit(GiftVoteDir, "gps_gifts_votes_geometry_audit.txt",
                     "GpsGiftScreen_geometry.json", "GpsVoteScreen_geometry.json");

        static void Audit(string dir, string outFile, params string[] sheets)
        {
            var lines = new StringBuilder();
            int totalSites = 0, totalFail = 0, totalGone = 0;

            foreach (string sheet in sheets)
            {
                string path = Path.Combine(dir, sheet);
                if (!File.Exists(path)) { Debug.LogError("[GeometryAudit] missing " + path); continue; }

                var doc = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
                string prefabPath = (string)doc!["prefab"];
                var sites = (List<object>)doc["sites"];

                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    lines.Append("\n=== ").Append(prefabPath).Append(" ===\n");
                    foreach (Dictionary<string, object> site in sites)
                    {
                        totalSites++;
                        string p = (string)site["path"];
                        var f = (List<object>)site["figma"];
                        float nx = Num(f[0]), ny = Num(f[1]), nw = Num(f[2]), nh = Num(f[3]);

                        var t = root.transform.Find(p) as RectTransform;
                        if (t == null)
                        {
                            totalGone++;
                            lines.Append("GONE  ").Append(p).Append('\n');
                            continue;
                        }

                        // ContentContainer's own rect is quoted in CANVAS coords in the sheet;
                        // every other site is relative to its parent, which is what anchoredPosition
                        // already is. Both read the same way here because ContentContainer's parent
                        // IS the full-screen root at (0,0).
                        float bx = t.anchoredPosition.x;
                        float by = -t.anchoredPosition.y;
                        float bw = t.rect.width, bh = t.rect.height;

                        bool ok = Near(bx, nx) && Near(by, ny) && Near(bw, nw) && Near(bh, nh);
                        if (!ok) totalFail++;
                        lines.Append(ok ? "PASS  " : "FAIL  ").Append(p)
                             .Append("  node[").Append(F(nx)).Append(',').Append(F(ny)).Append(',')
                             .Append(F(nw)).Append(',').Append(F(nh)).Append("]  built[")
                             .Append(F(bx)).Append(',').Append(F(by)).Append(',')
                             .Append(F(bw)).Append(',').Append(F(bh)).Append(']');
                        if (site.TryGetValue("state", out var st))
                            lines.Append("   state=").Append(st);
                        lines.Append('\n');
                    }
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }

            string verdict = $"{totalSites} sites {totalFail} FAIL {totalGone} GONE";
            lines.Append("\n").Append(verdict).Append('\n');

            Directory.CreateDirectory(OutDir);
            string outPath = Path.Combine(OutDir, outFile);
            File.WriteAllText(outPath, lines.ToString());
            Debug.Log("[GeometryAudit] " + verdict + "\n" + lines);
        }

        static bool Near(float a, float b) => Mathf.Abs(a - b) <= Tol;
        static float Num(object o) => System.Convert.ToSingle(o, CultureInfo.InvariantCulture);
        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>
        /// A 60-line JSON reader. Unity's JsonUtility cannot read a heterogeneous array like
        /// <c>"figma": [40, 355, 878, 80]</c> into anything useful, and the project has no
        /// Newtonsoft reference in Assembly-CSharp-Editor — so the sheet is parsed here rather
        /// than reshaped to suit a serializer.
        /// </summary>
        static class MiniJson
        {
            public static object? Parse(string s) { int i = 0; return Value(s, ref i); }

            static object? Value(string s, ref int i)
            {
                Ws(s, ref i);
                switch (s[i])
                {
                    case '{': return Obj(s, ref i);
                    case '[': return Arr(s, ref i);
                    case '"': return Str(s, ref i);
                    case 't': i += 4; return true;
                    case 'f': i += 5; return false;
                    case 'n': i += 4; return null;
                    default: return Number(s, ref i);
                }
            }

            static Dictionary<string, object> Obj(string s, ref int i)
            {
                var d = new Dictionary<string, object>();
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i); i++;               // ':'
                    d[k] = Value(s, ref i)!;
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return d;                    // '}'
                }
            }

            static List<object> Arr(string s, ref int i)
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)!);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    i++; return l;                    // ']'
                }
            }

            static string Str(string s, ref int i)
            {
                var sb = new StringBuilder();
                i++;                                  // opening quote
                while (s[i] != '"')
                {
                    if (s[i] == '\\') { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                    else sb.Append(s[i]);
                    i++;
                }
                i++;
                return sb.ToString();
            }

            static double Number(string s, ref int i)
            {
                int start = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+'
                                        || s[i] == '.' || s[i] == 'e' || s[i] == 'E')) i++;
                return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
            }

            static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
        }
    }
}
