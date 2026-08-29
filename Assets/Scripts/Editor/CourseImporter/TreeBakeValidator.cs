#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using Golfin.EditorTools.Missions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Golfin.CourseImport
{
    /// <summary>
    /// DRIFT GATE for hole tree data.
    ///
    /// THE FAILURE THIS CATCHES
    ///   Hole scenes (Assets/Golf/Courses/*/Generated/*.unity) are gitignored and therefore
    ///   per-machine. Physics reads the TRACKED bake (Resources/HoleData/.../tree_obstacles.csv).
    ///   When a machine's scene and the committed bake disagree, the player collides with trees
    ///   that are not rendered (or drives through trees that are). Hole 02 shipped that way:
    ///   1,495 invisible Spruce colliders.
    ///
    /// WHAT IT CHECKS, PER HOLE
    ///   1. BAKE      — re-harvest the live scene with TreeObstacleBaker's own harvest code and
    ///                  diff against the committed tree_obstacles.csv: per-profile counts, and
    ///                  every row matched to a committed row within 1 cm.
    ///   2. STANDALONE— the scene's StandaloneTrees children vs the tracked
    ///                  Data/hole-NN-geo/standalone_trees.csv, order-sensitive.
    ///
    /// Any mismatch is an error. Wired into CIBuild (Dev-iOS and iOS-Full) — see
    /// <see cref="Golfin.EditorTools.CIBuild"/> and the -skipTreeBakeCheck escape hatch.
    /// </summary>
    public static class TreeBakeValidator
    {
        private const string Tag = "[TreeBakeValidator]";

        /// <summary>Position tolerance, metres. 1 cm — the CSV stores 0.1 mm.</summary>
        public const float PositionToleranceM = 0.01f;
        private const float ScaleTolerance = 0.001f;
        private const float YawToleranceDeg = 0.01f;

        // ── Result model ─────────────────────────────────────────────────────────

        public sealed class HoleResult
        {
            public int hole;
            public string bakeStatus = "-";
            public string standaloneStatus = "-";
            /// <summary>missions_v1 §B1 — the baked mission start areas for this hole.</summary>
            public string startAreaStatus = "-";
            public int bakeRows;
            public int sceneRows;
            public int standaloneCsvRows;
            public int standaloneSceneRows;
            public readonly List<string> errors = new List<string>();
            public bool Ok => errors.Count == 0;
        }

        public sealed class Report
        {
            public readonly List<HoleResult> holes = new List<HoleResult>();
            public bool AllPass
            {
                get { foreach (var h in holes) if (!h.Ok) return false; return true; }
            }
            public int FailCount
            {
                get { int n = 0; foreach (var h in holes) if (!h.Ok) n++; return n; }
            }

            public string ToTable()
            {
                var sb = new StringBuilder();
                sb.Append("  hole | bake                         | standalone                    | start areas\n");
                sb.Append("  -----+------------------------------+-------------------------------+-----------------------------\n");
                foreach (var h in holes)
                    sb.Append(string.Format(CultureInfo.InvariantCulture,
                        "  {0:D2}   | {1,-28} | {2,-29} | {3}\n",
                        h.hole, h.bakeStatus, h.standaloneStatus, h.startAreaStatus));
                foreach (var h in holes)
                    foreach (var e in h.errors)
                        sb.Append($"  Hole {h.hole:D2}: {e}\n");
                sb.Append($"  → {holes.Count - FailCount}/{holes.Count} PASS");
                return sb.ToString();
            }
        }

        // ── Entry points ─────────────────────────────────────────────────────────

        [MenuItem("Import/Bake Tree Obstacles/Validate All Holes", false, 360)]
        public static void ValidateAllHolesMenu()
        {
            var report = ValidateAllHoles();
            string text = $"{Tag} Validate All Holes\n{report.ToTable()}";
            if (report.AllPass) Debug.Log(text);
            else Debug.LogError(text);
        }

        /// <summary>
        /// Opens every Hole_NN_Geo scene additively, validates it, and restores the editor's
        /// original scene setup. Safe to call from batchmode.
        /// </summary>
        public static Report ValidateAllHoles()
        {
            var report = new Report();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            bool interactive = !Application.isBatchMode;

            try
            {
                for (int n = 1; n <= 18; n++)
                {
                    if (interactive)
                        EditorUtility.DisplayProgressBar("Validating tree bake", $"Hole {n:D2}/18", (n - 1) / 18f);
                    report.holes.Add(ValidateHole(n));
                }
            }
            finally
            {
                if (interactive) EditorUtility.ClearProgressBar();
                StandaloneTreeCatalog.RestoreSetup(setup);
            }

            return report;
        }

        public static HoleResult ValidateHole(int holeNumber)
        {
            var result = new HoleResult { hole = holeNumber };

            string scenePath = TreeObstacleBaker.GetGeoScenePath(holeNumber);
            if (scenePath == null)
            {
                result.bakeStatus = "FAIL scene missing";
                result.standaloneStatus = "FAIL scene missing";
                // The start-area check reads TRACKED JSON + CSV, so it is meaningful even on a
                // machine that has not generated the scenes. Run it before returning.
                ValidateStartAreas(holeNumber, result);
                result.errors.Add($"no Hole_{holeNumber:D2}_Geo.unity on this machine — the hole cannot render. " +
                                  "Generate it, then Rebuild Current Hole.");
                return result;
            }

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(scenePath);
            if (slug == null)
            {
                result.bakeStatus = "FAIL slug";
                result.errors.Add($"cannot resolve course slug from '{scenePath}'.");
                return result;
            }

            Scene scene = default;
            bool opened = false;
            try
            {
                scene = EditorSceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    opened = true;
                }

                ValidateBake(scene, holeNumber, slug, result);
                ValidateStandalone(scene, holeNumber, slug, result);
                ValidateStartAreas(holeNumber, result);
            }
            catch (Exception e)
            {
                result.bakeStatus = "FAIL exception";
                result.errors.Add($"{e.GetType().Name}: {e.Message}");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }

            return result;
        }

        // ── 1. bake diff ─────────────────────────────────────────────────────────

        private static void ValidateBake(Scene scene, int holeNumber, string slug, HoleResult result)
        {
            string csvPath = TreeObstacleBaker.GetCsvAssetPath(holeNumber, slug);
            var committed = ReadBakeCsv(csvPath, out string readError);
            if (committed == null)
            {
                result.bakeStatus = "FAIL csv";
                result.errors.Add(readError);
                return;
            }

            // The baker's own harvest — never a reimplementation of it.
            var harvestedRaw = TreeObstacleBaker.HarvestScene(scene, holeNumber, out string _)
                               ?? new List<string>();
            var harvested = new List<BakeRow>(harvestedRaw.Count);
            foreach (var raw in harvestedRaw)
            {
                if (TryParseBakeRow(raw, out BakeRow row)) harvested.Add(row);
                else
                {
                    result.bakeStatus = "FAIL harvest";
                    result.errors.Add($"harvest produced an unparseable row: \"{raw}\"");
                    return;
                }
            }

            result.bakeRows = committed.Count;
            result.sceneRows = harvested.Count;

            // Per-profile counts.
            var committedByProfile = GroupByProfile(committed);
            var harvestedByProfile = GroupByProfile(harvested);
            var profiles = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var k in committedByProfile.Keys) profiles.Add(k);
            foreach (var k in harvestedByProfile.Keys) profiles.Add(k);

            bool countMismatch = false;
            foreach (var profile in profiles)
            {
                int c = committedByProfile.TryGetValue(profile, out var cl) ? cl.Count : 0;
                int h = harvestedByProfile.TryGetValue(profile, out var hl) ? hl.Count : 0;
                if (c != h)
                {
                    countMismatch = true;
                    result.errors.Add($"profile '{profile}': committed={c} scene={h} (Δ{h - c:+#;-#;0})");
                }
            }

            // Positions, per profile, within tolerance.
            int unmatched = 0;
            foreach (var profile in profiles)
            {
                committedByProfile.TryGetValue(profile, out var cl);
                harvestedByProfile.TryGetValue(profile, out var hl);
                unmatched += CountUnmatched(cl, hl);
            }

            if (countMismatch || unmatched > 0)
            {
                result.bakeStatus = $"FAIL {result.sceneRows}/{result.bakeRows} rows, {unmatched} unmatched";
                if (unmatched > 0)
                    result.errors.Add($"{unmatched} scene tree(s) have no committed row within " +
                                      $"{PositionToleranceM * 100f:F0} cm.");
            }
            else
            {
                result.bakeStatus = $"PASS {result.bakeRows} rows";
            }
        }

        private struct BakeRow
        {
            public float x, z, y, scale;
            public string profile;
        }

        private static bool TryParseBakeRow(string line, out BakeRow row)
        {
            row = default;
            var p = line.Split(',');
            if (p.Length != 5) return false;
            if (!TryF(p[0], out row.x) || !TryF(p[1], out row.z) ||
                !TryF(p[2], out row.y) || !TryF(p[3], out row.scale)) return false;
            row.profile = p[4];
            return true;
        }

        private static List<BakeRow> ReadBakeCsv(string assetPath, out string error)
        {
            error = null;
            string fullPath = StandaloneTreeCatalog.ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                error = $"{assetPath} does not exist.";
                return null;
            }

            var rows = new List<BakeRow>();
            foreach (string raw in File.ReadAllLines(fullPath))
            {
                string line = raw.Trim().TrimStart('﻿');
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("worldX,", StringComparison.Ordinal)) continue;
                if (!TryParseBakeRow(line, out BakeRow row))
                {
                    error = $"{assetPath}: unparseable row \"{line}\"";
                    return null;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static Dictionary<string, List<BakeRow>> GroupByProfile(List<BakeRow> rows)
        {
            var map = new Dictionary<string, List<BakeRow>>(StringComparer.Ordinal);
            foreach (var r in rows)
            {
                if (!map.TryGetValue(r.profile, out var list))
                    map[r.profile] = list = new List<BakeRow>();
                list.Add(r);
            }
            return map;
        }

        /// <summary>
        /// Greedy spatial match of <paramref name="scene"/> rows against <paramref name="committed"/>
        /// rows of the same profile. Returns the number of scene rows with no free partner within
        /// tolerance (plus any committed rows left over, so a deficit counts too).
        /// </summary>
        private static int CountUnmatched(List<BakeRow> committed, List<BakeRow> scene)
        {
            if (committed == null) return scene?.Count ?? 0;
            if (scene == null) return committed.Count;

            // 0.5 m buckets — comfortably larger than the 1 cm tolerance, small enough that a
            // bucket holds a handful of trees at the 6 m minimum spacing.
            const float Cell = 0.5f;
            var grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < committed.Count; i++)
            {
                long key = CellKey(committed[i].x, committed[i].z, Cell);
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<int>();
                list.Add(i);
            }

            var consumed = new bool[committed.Count];
            int unmatched = 0;

            foreach (var s in scene)
            {
                int cx = Mathf.FloorToInt(s.x / Cell);
                int cz = Mathf.FloorToInt(s.z / Cell);
                int best = -1;
                for (int dx = -1; dx <= 1 && best < 0; dx++)
                for (int dz = -1; dz <= 1 && best < 0; dz++)
                {
                    long key = ((long)(cx + dx) << 32) ^ (uint)(cz + dz);
                    if (!grid.TryGetValue(key, out var candidates)) continue;
                    foreach (int idx in candidates)
                    {
                        if (consumed[idx]) continue;
                        var c = committed[idx];
                        if (Mathf.Abs(c.x - s.x) <= PositionToleranceM &&
                            Mathf.Abs(c.z - s.z) <= PositionToleranceM &&
                            Mathf.Abs(c.y - s.y) <= PositionToleranceM)
                        { best = idx; break; }
                    }
                }

                if (best < 0) unmatched++;
                else consumed[best] = true;
            }

            for (int i = 0; i < consumed.Length; i++) if (!consumed[i]) unmatched++;
            return unmatched;
        }

        private static long CellKey(float x, float z, float cell)
            => ((long)Mathf.FloorToInt(x / cell) << 32) ^ (uint)Mathf.FloorToInt(z / cell);

        // ── 2. standalone catalog diff ───────────────────────────────────────────

        private static void ValidateStandalone(Scene scene, int holeNumber, string slug, HoleResult result)
        {
            string assetPath = StandaloneTreeCatalog.GetCsvAssetPath(holeNumber, slug);
            var committed = StandaloneTreeCatalog.ReadCsv(assetPath, out string readError);
            if (committed == null)
            {
                result.standaloneStatus = "FAIL csv";
                result.errors.Add(readError);
                return;
            }

            var live = StandaloneTreeCatalog.HarvestRows(scene);
            result.standaloneCsvRows = committed.Count;
            result.standaloneSceneRows = live.Count;

            if (committed.Count != live.Count)
            {
                result.standaloneStatus = $"FAIL scene={live.Count} csv={committed.Count}";
                result.errors.Add($"StandaloneTrees differs from {assetPath}: scene has {live.Count} " +
                                  $"child(ren), the tracked file has {committed.Count}. " +
                                  "Run Import/Standalone Trees/Rebuild Current Hole.");
                return;
            }

            int mismatches = 0;
            string firstDetail = null;
            for (int i = 0; i < committed.Count; i++)
            {
                var c = committed[i];
                var l = live[i];
                bool same = string.Equals(c.prefab, l.prefab, StringComparison.Ordinal) &&
                            Mathf.Abs(c.x - l.x) <= PositionToleranceM &&
                            Mathf.Abs(c.y - l.y) <= PositionToleranceM &&
                            Mathf.Abs(c.z - l.z) <= PositionToleranceM &&
                            Mathf.Abs(Mathf.DeltaAngle(c.yawDeg, l.yawDeg)) <= YawToleranceDeg &&
                            Mathf.Abs(c.scale - l.scale) <= ScaleTolerance;
                if (same) continue;

                mismatches++;
                if (firstDetail == null)
                    firstDetail = $"row {i}: csv \"{StandaloneTreeCatalog.FormatRow(c)}\" vs " +
                                  $"scene \"{StandaloneTreeCatalog.FormatRow(l)}\"";
            }

            if (mismatches > 0)
            {
                result.standaloneStatus = $"FAIL {mismatches}/{committed.Count} rows differ";
                result.errors.Add($"StandaloneTrees differs from {assetPath} — {firstDetail}");
            }
            else
            {
                result.standaloneStatus = $"PASS {committed.Count} rows";
            }
        }

        // ── 3. mission start areas (missions_v1 §B1) ─────────────────────────────

        /// <summary>
        /// The drift gate over `mission_start_areas.csv`.
        ///
        /// WHAT IT CATCHES, and why it is a hash rather than a re-bake. Re-deriving the points
        /// here would only prove the baker is deterministic — it would pass just as happily
        /// over a CSV somebody had hand-edited, because the comparison would be against the
        /// freshly-derived value, not against what is committed. `bake_hash` is computed FROM
        /// the row's own coordinates, so the only way for it to agree is for those coordinates
        /// to be the ones the baker wrote. Change an x by a metre in a text editor and this
        /// fails the hole, which is the §20 tripwire the spec asks for.
        ///
        /// It also checks the two invariants the file's own header states: a TEE row never
        /// carries coordinates (it resolves to the scene's TeeMarker at runtime), and a SHORT
        /// row either carries a full coordinate set or is deliberately blank.
        ///
        /// This is the ONE check in this file that does not need the Hole_NN_Geo scene: it
        /// reads the tracked CSV, so it means the same thing on every clone.
        /// </summary>
        private static void ValidateStartAreas(int holeNumber, HoleResult result)
        {
            string csv = MissionStartAreaBaker.CsvPath;
            string full = Path.GetFullPath(csv);
            if (!File.Exists(full))
            {
                result.startAreaStatus = "FAIL csv missing";
                result.errors.Add($"no {csv} — run Golfin ▸ Missions ▸ Bake Start Areas.");
                return;
            }

            string[] lines;
            try { lines = File.ReadAllLines(full); }
            catch (Exception e)
            {
                result.startAreaStatus = "FAIL csv read";
                result.errors.Add($"could not read {csv}: {e.Message}");
                return;
            }

            string[] header = Array.Empty<string>();
            int checkedRows = 0, drifted = 0, blank = 0;
            string firstDetail = null;

            foreach (string line in lines)
            {
                if (line.Length == 0 || line.TrimStart().StartsWith("#")) continue;
                string[] cells = MissionStartAreaBaker.SplitCsv(line);
                if (header.Length == 0) { header = cells; continue; }

                int iHole = Array.IndexOf(header, "holeId");
                int iArea = Array.IndexOf(header, "areaId");
                int iKind = Array.IndexOf(header, "kind");
                int iX = Array.IndexOf(header, "x");
                int iY = Array.IndexOf(header, "y");
                int iZ = Array.IndexOf(header, "z");
                int iPin = Array.IndexOf(header, "pin_count");
                int iHash = Array.IndexOf(header, "bake_hash");
                if (iHole < 0 || iArea < 0 || iKind < 0 || iX < 0 || iY < 0 || iZ < 0 || iPin < 0 || iHash < 0)
                {
                    result.startAreaStatus = "FAIL header";
                    result.errors.Add($"{csv} is missing one of holeId/areaId/kind/x/y/z/pin_count/bake_hash.");
                    return;
                }

                if (!int.TryParse(cells[iHole], NumberStyles.Integer, CultureInfo.InvariantCulture, out int h)
                    || h != holeNumber) continue;

                string areaId = cells[iArea];
                bool isTee = string.Equals(cells[iKind], "tee", StringComparison.OrdinalIgnoreCase);
                bool hasCoords = cells[iX].Length > 0 && cells[iY].Length > 0 && cells[iZ].Length > 0;

                if (isTee)
                {
                    checkedRows++;
                    if (hasCoords || cells[iHash].Length > 0)
                    {
                        drifted++;
                        firstDetail ??= $"{areaId} is a TEE row but carries coordinates — tees resolve " +
                                        "to the scene's TeeMarker_<label>_L/R midpoint and must stay blank";
                    }
                    continue;
                }

                checkedRows++;
                if (!hasCoords)
                {
                    // Deliberately blank: the kind does not exist on this hole (hole 13 has no
                    // greenside bunker). A blank row with a leftover hash would be the real bug.
                    blank++;
                    if (cells[iHash].Length > 0)
                    {
                        drifted++;
                        firstDetail ??= $"{areaId} has no coordinates but still carries a bake_hash";
                    }
                    continue;
                }

                if (!TryF(cells[iX], out float x) || !TryF(cells[iY], out float y) || !TryF(cells[iZ], out float z)
                    || !int.TryParse(cells[iPin], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pins))
                {
                    drifted++;
                    firstDetail ??= $"{areaId} has an unparseable coordinate or pin_count";
                    continue;
                }

                string expected = MissionStartAreaBaker.ComputeBakeHash(
                    holeNumber, areaId, new Vector3(x, y, z), pins);
                if (!string.Equals(expected, cells[iHash], StringComparison.Ordinal))
                {
                    drifted++;
                    firstDetail ??= $"{areaId} bake_hash is {cells[iHash]} but its coordinates hash to " +
                                    $"{expected} — the row was edited without re-baking";
                }
            }

            if (checkedRows == 0)
            {
                result.startAreaStatus = "FAIL no rows";
                result.errors.Add($"{csv} has no rows for hole {holeNumber:D2}.");
            }
            else if (drifted > 0)
            {
                result.startAreaStatus = $"FAIL {drifted}/{checkedRows} drifted";
                result.errors.Add($"mission start areas: {firstDetail}. Re-run Golfin ▸ Missions ▸ Bake Start Areas.");
            }
            else
            {
                result.startAreaStatus = blank > 0
                    ? $"PASS {checkedRows} rows ({blank} blank)"
                    : $"PASS {checkedRows} rows";
            }
        }

        private static bool TryF(string s, out float v)
            => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
#endif
