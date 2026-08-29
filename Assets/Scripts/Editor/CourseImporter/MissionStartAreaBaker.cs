#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Golfin.Course.Runtime;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime.Baked;
using UnityEditor;
using UnityEngine;

namespace Golfin.EditorTools.Missions
{
    /// <summary>
    /// Bakes the SHORT mission start areas — where a mission drops the ball when it does
    /// not start from a tee. Spec: missions_v1 SPEC §B1.
    ///
    /// Menu: <c>Golfin/Missions/Bake Start Areas</c>.
    ///
    /// ⚠️ IT READS THE TRACKED JSON, NOT THE Hole_NN_Geo SCENES, AND THAT IS A DELIBERATE
    /// DEPARTURE FROM THE SPEC'S WORDING. §B1 says "for each Hole_NN_Geo scene derive from
    /// SurfaceMarker GOs". Those scenes are PER-MACHINE — `.gitignore:111` excludes
    /// `Assets/Golf/Courses/*/Generated/*`, and Docs/Pipeline/TREES_AND_GENERATED_SCENES.md
    /// says so out loud. A bake driven off them would produce coordinates, and a `bake_hash`
    /// drift gate over those coordinates, that only mean something on the machine that ran it;
    /// the next person to run `Validate All Holes` would see 18 failures caused by nothing.
    ///
    /// `Assets/Resources/HoleData/&lt;course&gt;/Hole_NN/{zones,green}.json` is the same geometry,
    /// TRACKED, and is what the runtime itself classifies against (`BakedZoneClassifier` is the
    /// sanctioned surface provider — PhysicsLabController loads exactly these two files). So the
    /// bake reads what the GAME reads, and its output is reproducible on any clone.
    ///
    /// THE FIVE DERIVATIONS, and the ways the course fought each one:
    ///
    ///   GREEN    the green centroid walked toward the tee, stopping at the last point still
    ///            ON the green, capped at 9 m (a 10-yard putt). The cap is a target, not a
    ///            promise: a small green stops the walk early and the row records where it
    ///            actually stopped.
    ///   FRINGE   the first point past the green edge along the same bearing, plus 2 m.
    ///   FAIRWAY  the fairway point whose distance to the default pin is closest to 110 m.
    ///            On the par-3s there is barely a fairway, so "closest to 110" lands at 26-44 m
    ///            — which is the honest answer, not a failure.
    ///   ROUGH    8 m lateral from FAIRWAY. §B1 says 8 m; on holes 7 and 10 the fairway is wide
    ///            enough that BOTH sides at 8 m are still fairway, so the search STEPS OUTWARD
    ///            (8, 10, 12 … 40 m) until the probe leaves every zone polygon. Rough is not a
    ///            polygon in zones.json — it is the classifier's DEFAULT surface — so "outside
    ///            everything" is precisely what rough means here.
    ///   SAND     the greenside bunker nearest the green centroid, within
    ///            <see cref="GreensideMaxM"/>. Beyond that a bunker is not greenside and the
    ///            row is left blank, per §B1's "skip kind if the hole has none".
    ///
    /// ⚠️ THE CENTROID OF A BUNKER RING CAN LAND OUTSIDE THE BUNKER. A crescent-shaped
    /// polygon's centroid sits in the hollow, and a mission starting there would begin in the
    /// rough while telling the player they are in sand. Every probe point is therefore
    /// VERIFIED with a point-in-polygon test and pulled to an inset vertex when it fails.
    ///
    /// THE HASH IS THE DRIFT GATE. `bake_hash` is a function of the row's own coordinates and
    /// pin count, so hand-editing a coordinate without re-baking makes the stored hash
    /// disagree with the recomputed one and `Import ▸ Bake Tree Obstacles ▸ Validate All Holes`
    /// fails that hole. That is the §20 tripwire the acceptance list asks for.
    /// </summary>
    public static class MissionStartAreaBaker
    {
        private const string Tag = "[MissionStartAreaBaker]";

        public const string CsvPath = "Assets/Resources/Data/mission_start_areas.csv";
        public const string CourseSlug = "lomond-country-club";
        public const int HoleCount = 18;

        /// <summary>Target putt length from the green centroid, metres (a 10-yard putt).</summary>
        public const float GreenPuttM = 9f;

        /// <summary>How far past the green edge the FRINGE start sits, metres.</summary>
        public const float FringeOutsetM = 2f;

        /// <summary>Target approach distance from the default pin, metres.</summary>
        public const float FairwayApproachM = 110f;

        /// <summary>§B1's lateral offset for ROUGH. The search starts here and steps out.</summary>
        public const float RoughLateralM = 8f;
        private const float RoughLateralMaxM = 40f;
        private const float RoughLateralStepM = 2f;

        /// <summary>
        /// How near the green a bunker must be to count as GREENSIDE, metres.
        ///
        /// Not arbitrary. Measured over all 18 holes: every real greenside bunker on this course
        /// sits 14-33 m from its green centroid, and the next-nearest sand anywhere is 156 m
        /// (hole 13, which has no greenside bunker at all). 50 m separates those two clusters
        /// with a margin nothing on this course comes near.
        /// </summary>
        public const float GreensideMaxM = 50f;

        /// <summary>The five short kinds this tool bakes, in CSV order.</summary>
        public static readonly string[] ShortAreaIds = { "GREEN", "FRINGE", "FAIRWAY", "ROUGH", "SAND" };

        // ── Result model ────────────────────────────────────────────────────────

        public sealed class AreaResult
        {
            public string areaId = "";
            public bool found;
            public Vector3 world;
            public string note = "";
        }

        public sealed class HoleResult
        {
            public int hole;
            public int pinCount;
            public readonly List<AreaResult> areas = new List<AreaResult>();
            public readonly List<string> errors = new List<string>();
            public bool Ok => errors.Count == 0;
        }

        // ── Entry point ─────────────────────────────────────────────────────────

        [MenuItem("Golfin/Missions/Bake Start Areas", false, 100)]
        public static void BakeAllMenu()
        {
            var results = BakeAll(out string report);
            int written = WriteCsv(results, out string csvNote);
            AssetDatabase.Refresh();

            bool anyError = false;
            foreach (var r in results) if (!r.Ok) anyError = true;

            string text = $"{Tag} Bake Start Areas\n{report}\n{csvNote}\n  → {written} short rows written to {CsvPath}";
            if (anyError) Debug.LogWarning(text);
            else Debug.Log(text);
        }

        /// <summary>Derive every short start area on every hole. No file I/O.</summary>
        public static List<HoleResult> BakeAll(out string report)
        {
            var results = new List<HoleResult>();
            var sb = new StringBuilder();
            sb.Append("  hole | pins | GREEN            | FRINGE           | FAIRWAY          | ROUGH            | SAND\n");
            sb.Append("  -----+------+------------------+------------------+------------------+------------------+------------------\n");

            for (int n = 1; n <= HoleCount; n++)
            {
                HoleResult hr = BakeHole(n);
                results.Add(hr);
                sb.Append(string.Format(CultureInfo.InvariantCulture, "  {0:D2}   | {1,4} ", hr.hole, hr.pinCount));
                foreach (var a in hr.areas)
                    sb.Append(a.found
                        ? string.Format(CultureInfo.InvariantCulture, "| {0,7:F1},{1,7:F1} ", a.world.x, a.world.z)
                        : "|        —         ");
                sb.Append('\n');
            }
            foreach (var hr in results)
            {
                foreach (var a in hr.areas)
                    if (!string.IsNullOrEmpty(a.note))
                        sb.Append($"  Hole {hr.hole:D2} {a.areaId}: {a.note}\n");
                foreach (var e in hr.errors)
                    sb.Append($"  Hole {hr.hole:D2}: {e}\n");
            }
            report = sb.ToString();
            return results;
        }

        public static HoleResult BakeHole(int hole)
        {
            var result = new HoleResult { hole = hole };

            string greenPath = $"Assets/Resources/HoleData/{CourseSlug}/Hole_{hole:D2}/green.json";
            string zonesPath = $"Assets/Resources/HoleData/{CourseSlug}/Hole_{hole:D2}/zones.json";

            // LoadFromDisk, not LoadFromResources: the Resources cache is stale straight after
            // an AssetDatabase.Refresh, which is exactly when a bake tends to run.
            GreenTopology green = GreenTopology.LoadFromDisk(greenPath, hole);
            if (green == null)
            {
                result.errors.Add($"no readable green.json at {greenPath}");
                foreach (var id in ShortAreaIds) result.areas.Add(new AreaResult { areaId = id });
                return result;
            }
            result.pinCount = green.GetPinCandidates()?.Count ?? 0;

            if (!File.Exists(zonesPath))
            {
                result.errors.Add($"no zones.json at {zonesPath}");
                foreach (var id in ShortAreaIds) result.areas.Add(new AreaResult { areaId = id });
                return result;
            }

            ZoneData zones;
            try { zones = ZoneData.FromJson(File.ReadAllText(zonesPath)); }
            catch (Exception ex)
            {
                result.errors.Add($"zones.json parse failed — {ex.Message}");
                foreach (var id in ShortAreaIds) result.areas.Add(new AreaResult { areaId = id });
                return result;
            }

            var classifier = new BakedZoneClassifier(zones);
            var rings = new ZoneRings(zones);

            // The TERRAIN height, for probes that are not on a zone overlay at all.
            // ROUGH is exactly that case on every hole: rough has no polygons in zones.json
            // (it is the classifier's DEFAULT surface), so there is no zone mesh under the
            // probe and TrySampleMeshY correctly fails. Without this the whole ROUGH column
            // baked to y = 0 — a ball spawned under the course. Same pairing
            // PhysicsLabController wires at runtime: zones.json + heightmap.bytes.
            BakedHeightProvider? ground = null;
            string hmPath = $"Assets/Resources/HoleData/{CourseSlug}/Hole_{hole:D2}/heightmap.bytes";
            if (File.Exists(hmPath))
            {
                var hm = Golfin.Physics.Runtime.HeightmapLoader.LoadFromBytes(File.ReadAllBytes(hmPath));
                if (hm != null) ground = new BakedHeightProvider(hm, classifier);
                else result.errors.Add($"heightmap.bytes failed to parse at {hmPath}");
            }
            else result.errors.Add($"no heightmap.bytes at {hmPath} — rough starts would bake at y=0");

            Vector2 greenCentroid = new Vector2(green.GeoCentroidX, green.GeoCentroidZ);
            Vector3 pin = green.GetDefaultPin();
            Vector2 pinXZ = new Vector2(pin.x, pin.z);

            // Bearing from the green back toward the tee. The Tee zone's own centroid, not a
            // scene marker: same reason as the file choice above.
            Vector2 teeCentroid = rings.CentroidOf(SurfaceType.Tee);
            Vector2 toTee = (teeCentroid - greenCentroid);
            toTee = toTee.sqrMagnitude > 1e-6f ? toTee.normalized : Vector2.up;

            result.areas.Add(BakeGreen(rings, classifier, ground, greenCentroid, toTee));
            result.areas.Add(BakeFringe(rings, classifier, ground, greenCentroid, toTee));

            AreaResult fairway = BakeFairway(rings, classifier, ground, pinXZ);
            result.areas.Add(fairway);
            result.areas.Add(BakeRough(rings, classifier, ground, fairway, pinXZ));
            result.areas.Add(BakeSand(rings, classifier, ground, greenCentroid));

            return result;
        }

        // ── The five derivations ────────────────────────────────────────────────

        private static AreaResult BakeGreen(ZoneRings rings, BakedZoneClassifier cls,
                                            BakedHeightProvider? ground, Vector2 centroid, Vector2 toTee)
        {
            var r = new AreaResult { areaId = "GREEN" };
            Vector2 best = centroid;
            bool any = false;
            for (float d = 0f; d <= GreenPuttM + 0.001f; d += 0.25f)
            {
                Vector2 p = centroid + toTee * d;
                if (!rings.Contains(SurfaceType.Green, p)) break;
                best = p; any = true;
            }
            if (!any)
            {
                r.note = "green centroid is not inside any Green polygon — green.json and zones.json disagree";
                return r;
            }
            float reached = (best - centroid).magnitude;
            if (reached < GreenPuttM - 0.5f)
                r.note = $"green is only {reached:F1} m deep toward the tee; the {GreenPuttM:F0} m putt was clamped";
            return Finish(r, cls, ground, best, SurfaceType.Green);
        }

        private static AreaResult BakeFringe(ZoneRings rings, BakedZoneClassifier cls,
                                             BakedHeightProvider? ground, Vector2 centroid, Vector2 toTee)
        {
            var r = new AreaResult { areaId = "FRINGE" };
            float e = 0f;
            while (e < 80f && rings.Contains(SurfaceType.Green, centroid + toTee * e)) e += 0.25f;
            if (e >= 80f) { r.note = "never left the green within 80 m"; return r; }
            Vector2 p = centroid + toTee * (e + FringeOutsetM);
            if (rings.Contains(SurfaceType.Green, p))
                r.note = "the +2 m outset landed back on the green (concave edge)";
            return Finish(r, cls, ground, p, SurfaceType.GreenCollar);
        }

        private static AreaResult BakeFairway(ZoneRings rings, BakedZoneClassifier cls,
                                              BakedHeightProvider? ground, Vector2 pinXZ)
        {
            var r = new AreaResult { areaId = "FAIRWAY" };
            Vector2 best = Vector2.zero;
            float bestErr = float.MaxValue;

            foreach (var poly in rings.Of(SurfaceType.Fairway))
            {
                Vector2 c = Ring.Centroid(poly);
                for (int i = 0; i < poly.Length; i++)
                {
                    Vector2 a = poly[i];
                    Vector2 b = poly[(i + 1) % poly.Length];
                    for (float t = 0f; t < 1f; t += 0.25f)
                    {
                        Vector2 p = Vector2.Lerp(a, b, t);
                        // Pull 15 % toward the polygon centre so the probe is INSIDE the
                        // fairway rather than sitting exactly on its boundary, where a
                        // point-in-polygon test is a coin flip.
                        p += (c - p) * 0.15f;
                        if (!rings.Contains(SurfaceType.Fairway, p)) continue;
                        float err = Mathf.Abs(Vector2.Distance(p, pinXZ) - FairwayApproachM);
                        if (err < bestErr) { bestErr = err; best = p; }
                    }
                }
            }

            if (bestErr == float.MaxValue) { r.note = "no fairway polygon on this hole"; return r; }
            float reached = Vector2.Distance(best, pinXZ);
            if (Mathf.Abs(reached - FairwayApproachM) > 20f)
                r.note = $"nearest fairway point to the pin is {reached:F0} m, not {FairwayApproachM:F0} m " +
                         "(short hole — this is the whole fairway, not a bake failure)";
            return Finish(r, cls, ground, best, SurfaceType.Fairway);
        }

        private static AreaResult BakeRough(ZoneRings rings, BakedZoneClassifier cls,
                                            BakedHeightProvider? ground, AreaResult fairway, Vector2 pinXZ)
        {
            var r = new AreaResult { areaId = "ROUGH" };
            if (!fairway.found) { r.note = "no FAIRWAY to offset from"; return r; }

            Vector2 f = new Vector2(fairway.world.x, fairway.world.z);
            Vector2 toPin = (pinXZ - f);
            toPin = toPin.sqrMagnitude > 1e-6f ? toPin.normalized : Vector2.up;
            Vector2 lateral = new Vector2(-toPin.y, toPin.x);

            for (float d = RoughLateralM; d <= RoughLateralMaxM; d += RoughLateralStepM)
            {
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    Vector2 p = f + lateral * (d * sign);
                    // Rough is the classifier's DEFAULT surface — it has no polygons of its own,
                    // so "outside every zone" IS rough. Asking the classifier directly keeps this
                    // honest if a Rough zone is ever authored.
                    if (rings.ContainsAny(p)) continue;
                    if (d > RoughLateralM + 0.01f)
                        r.note = $"8 m lateral was still fairway; stepped out to {d:F0} m";
                    return Finish(r, cls, ground, p, SurfaceType.Rough);
                }
            }
            r.note = $"no rough within {RoughLateralMaxM:F0} m either side of the fairway point";
            return r;
        }

        private static AreaResult BakeSand(ZoneRings rings, BakedZoneClassifier cls,
                                           BakedHeightProvider? ground, Vector2 greenCentroid)
        {
            var r = new AreaResult { areaId = "SAND" };
            Vector2 best = Vector2.zero;
            float bestDist = float.MaxValue;

            foreach (var poly in rings.Of(SurfaceType.Sand))
            {
                if (!Ring.TryInteriorPoint(poly, out Vector2 p)) continue;  // crescent bunker
                float d = Vector2.Distance(p, greenCentroid);
                if (d < bestDist) { bestDist = d; best = p; }
            }

            if (bestDist == float.MaxValue) { r.note = "no bunker polygon on this hole"; return r; }
            if (bestDist > GreensideMaxM)
            {
                // §B1: "skip kind if the hole has none". A 156 m bunker is a fairway bunker, and
                // a greenside-save mission starting in it would be nonsense. Leaving the row
                // blank makes the publish validator refuse SAND on this hole, which is the point.
                r.note = $"nearest bunker is {bestDist:F0} m from the green — not greenside, " +
                         $"so no SAND start is baked (threshold {GreensideMaxM:F0} m)";
                return r;
            }
            return Finish(r, cls, ground, best, SurfaceType.Sand);
        }

        // ── Height + verification ───────────────────────────────────────────────

        /// <summary>
        /// Resolve the world Y and record the point, after checking the classifier agrees the
        /// probe is on the surface we think it is. A disagreement is a NOTE, never a silent
        /// drop: the point is usually still usable and the operator should see why it is odd.
        /// </summary>
        private static AreaResult Finish(AreaResult r, BakedZoneClassifier cls,
                                         BakedHeightProvider? ground, Vector2 xz, SurfaceType expected)
        {
            fp x = fp.FromFloat(xz.x);
            fp z = fp.FromFloat(xz.y);

            SurfaceType actual = cls.Classify(x, z);
            if (actual != expected && !(expected == SurfaceType.GreenCollar && actual != SurfaceType.Green))
            {
                string extra = $"classifier says {actual}, expected {expected}";
                r.note = string.IsNullOrEmpty(r.note) ? extra : r.note + "; " + extra;
            }

            // Height, in the order the runtime resolves it: the zone MESH is exact for a point
            // on an overlay surface (green, fairway, bunker); the baked TERRAIN heightmap
            // covers everything else. ROUGH is always the second case — rough has no overlay
            // mesh at all — so a mission that starts in the rough depends entirely on the
            // heightmap being present. Refusing to bake beats a start at y = 0, which is a
            // ball under the course.
            float y;
            if (!cls.TrySampleMeshY(x, z, out _, out y))
            {
                if (ground == null)
                {
                    string missing = "not on a zone mesh AND no heightmap — refusing to bake a start at y=0";
                    r.note = string.IsNullOrEmpty(r.note) ? missing : r.note + "; " + missing;
                    r.found = false;
                    return r;
                }
                y = ground.SampleHeight(x, z).ToFloat();
            }

            r.world = new Vector3(xz.x, y, xz.y);
            r.found = true;
            return r;
        }

        // ── The hash — the drift tripwire ───────────────────────────────────────

        /// <summary>
        /// A stable 8-hex digest of what a baked row CLAIMS. Recomputed by
        /// <c>TreeBakeValidator</c>; a hand-edited coordinate makes the two disagree.
        ///
        /// FNV-1a over an invariant-culture string, NOT <c>string.GetHashCode()</c>: .NET's
        /// string hash is randomised per process, so a stored value would fail on the very
        /// next Editor launch and the gate would cry wolf forever.
        /// </summary>
        public static string ComputeBakeHash(int hole, string areaId, Vector3 world, int pinCount)
        {
            string payload = string.Format(CultureInfo.InvariantCulture,
                "{0:D2}|{1}|{2:F3}|{3:F3}|{4:F3}|{5}", hole, areaId, world.x, world.y, world.z, pinCount);
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in payload) { h ^= c; h *= 16777619u; }
                return h.ToString("x8", CultureInfo.InvariantCulture);
            }
        }

        // ── CSV write ───────────────────────────────────────────────────────────

        /// <summary>
        /// Rewrite the short rows of mission_start_areas.csv IN PLACE.
        ///
        /// The file is rewritten line by line rather than regenerated, for the reason
        /// Tools/content/export_content.py gives at length: the repo CSV is the authority on
        /// ROW ORDER and LINE LAYOUT (the comment header here is 27 lines of why the short
        /// rows are blank), and the catalog is the authority on values. Tee rows are passed
        /// through untouched — they carry no coordinates by design.
        /// </summary>
        public static int WriteCsv(List<HoleResult> results, out string note)
        {
            string full = Path.GetFullPath(CsvPath);
            if (!File.Exists(full)) { note = $"  !! {CsvPath} not found — nothing written"; return 0; }

            var byKey = new Dictionary<string, AreaResult>();
            var pinByHole = new Dictionary<int, int>();
            foreach (var hr in results)
            {
                pinByHole[hr.hole] = hr.pinCount;
                foreach (var a in hr.areas) byKey[$"{hr.hole}:{a.areaId}"] = a;
            }

            string[] lines = File.ReadAllLines(full);
            var outLines = new List<string>(lines.Length);
            string[] header = Array.Empty<string>();
            int written = 0, cleared = 0;

            foreach (string line in lines)
            {
                if (line.Length == 0 || line.TrimStart().StartsWith("#")) { outLines.Add(line); continue; }
                string[] cells = SplitCsv(line);
                if (header.Length == 0) { header = cells; outLines.Add(line); continue; }

                int iHole = Array.IndexOf(header, "holeId");
                int iArea = Array.IndexOf(header, "areaId");
                int iKind = Array.IndexOf(header, "kind");
                if (iHole < 0 || iArea < 0 || iKind < 0) { outLines.Add(line); continue; }
                if (!int.TryParse(cells[iHole], NumberStyles.Integer, CultureInfo.InvariantCulture, out int hole))
                { outLines.Add(line); continue; }

                // Tee rows resolve to scene markers at runtime and must stay coordinate-less.
                if (!string.Equals(cells[iKind], "short", StringComparison.OrdinalIgnoreCase))
                { outLines.Add(line); continue; }

                if (!byKey.TryGetValue($"{hole}:{cells[iArea]}", out AreaResult? area)) { outLines.Add(line); continue; }

                int iX = Array.IndexOf(header, "x");
                int iY = Array.IndexOf(header, "y");
                int iZ = Array.IndexOf(header, "z");
                int iPin = Array.IndexOf(header, "pin_count");
                int iHash = Array.IndexOf(header, "bake_hash");

                if (area.found)
                {
                    if (iX >= 0) cells[iX] = area.world.x.ToString("F3", CultureInfo.InvariantCulture);
                    if (iY >= 0) cells[iY] = area.world.y.ToString("F3", CultureInfo.InvariantCulture);
                    if (iZ >= 0) cells[iZ] = area.world.z.ToString("F3", CultureInfo.InvariantCulture);
                    if (iPin >= 0) cells[iPin] = pinByHole[hole].ToString(CultureInfo.InvariantCulture);
                    if (iHash >= 0) cells[iHash] = ComputeBakeHash(hole, cells[iArea], area.world, pinByHole[hole]);
                    written++;
                }
                else
                {
                    // A kind this hole does not have. Blank, not stale — a leftover coordinate
                    // from a previous bake would be a mission starting somewhere that is no
                    // longer there.
                    if (iX >= 0) cells[iX] = "";
                    if (iY >= 0) cells[iY] = "";
                    if (iZ >= 0) cells[iZ] = "";
                    if (iPin >= 0) cells[iPin] = pinByHole[hole].ToString(CultureInfo.InvariantCulture);
                    if (iHash >= 0) cells[iHash] = "";
                    cleared++;
                }
                outLines.Add(JoinCsv(cells));
            }

            File.WriteAllText(full, string.Join("\n", outLines) + "\n", new UTF8Encoding(false));
            note = $"  {written} short rows baked, {cleared} left blank (kind absent on that hole)";
            return written;
        }

        // ── Tiny CSV helpers (QUOTE_MINIMAL, matching Tools/content) ────────────

        public static string[] SplitCsv(string line)
        {
            var outCells = new List<string>();
            var sb = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (quoted)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') quoted = false;
                    else sb.Append(c);
                }
                else if (c == '"') quoted = true;
                else if (c == ',') { outCells.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
            outCells.Add(sb.ToString());
            return outCells.ToArray();
        }

        public static string JoinCsv(string[] cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append(',');
                string c = cells[i] ?? "";
                if (c.IndexOfAny(new[] { ',', '"', '\n' }) >= 0)
                    sb.Append('"').Append(c.Replace("\"", "\"\"")).Append('"');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Zone ring access ────────────────────────────────────────────────────

        /// <summary>
        /// The zone polygons as flat XZ rings, by surface type. `BakedZoneClassifier` owns
        /// classification and mesh-Y; this owns the "is this point inside a GREEN polygon
        /// specifically" question, which the classifier's single-answer API cannot express
        /// (it returns the winning surface, not membership of a named one).
        /// </summary>
        private sealed class ZoneRings
        {
            private readonly Dictionary<SurfaceType, List<Vector2[]>> _byType = new();

            public ZoneRings(ZoneData data)
            {
                foreach (var group in data.zones)
                {
                    if (group?.polygons == null) continue;
                    if (!_byType.TryGetValue(group.SurfaceType, out var list))
                        _byType[group.SurfaceType] = list = new List<Vector2[]>();
                    foreach (var poly in group.polygons)
                    {
                        if (poly?.points == null || poly.points.Count < 3) continue;
                        var ring = new Vector2[poly.points.Count];
                        for (int i = 0; i < poly.points.Count; i++)
                            ring[i] = new Vector2(poly.points[i].x, poly.points[i].z);
                        list.Add(ring);
                    }
                }
            }

            public IReadOnlyList<Vector2[]> Of(SurfaceType type)
                => _byType.TryGetValue(type, out var list) ? list : Array.Empty<Vector2[]>();

            public bool Contains(SurfaceType type, Vector2 p)
            {
                foreach (var ring in Of(type)) if (Ring.Contains(ring, p)) return true;
                return false;
            }

            public bool ContainsAny(Vector2 p)
            {
                foreach (var kv in _byType)
                    foreach (var ring in kv.Value)
                        if (Ring.Contains(ring, p)) return true;
                return false;
            }

            public Vector2 CentroidOf(SurfaceType type)
            {
                Vector2 sum = Vector2.zero;
                int n = 0;
                foreach (var ring in Of(type)) foreach (var p in ring) { sum += p; n++; }
                return n > 0 ? sum / n : Vector2.zero;
            }
        }

        private static class Ring
        {
            public static bool Contains(Vector2[] ring, Vector2 p)
            {
                bool inside = false;
                for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
                {
                    if ((ring[i].y > p.y) != (ring[j].y > p.y) &&
                        p.x < (ring[j].x - ring[i].x) * (p.y - ring[i].y) / (ring[j].y - ring[i].y) + ring[i].x)
                        inside = !inside;
                }
                return inside;
            }

            public static Vector2 Centroid(Vector2[] ring)
            {
                Vector2 sum = Vector2.zero;
                foreach (var p in ring) sum += p;
                return sum / ring.Length;
            }

            /// <summary>
            /// A point genuinely INSIDE the ring. The centroid first, then vertices pulled 25 %
            /// toward it — a crescent bunker's centroid sits in the hollow, outside its own
            /// polygon, and a mission starting there would begin in the rough while the card
            /// says sand.
            /// </summary>
            public static bool TryInteriorPoint(Vector2[] ring, out Vector2 point)
            {
                Vector2 c = Centroid(ring);
                if (Contains(ring, c)) { point = c; return true; }
                foreach (var v in ring)
                {
                    Vector2 t = v + (c - v) * 0.25f;
                    if (Contains(ring, t)) { point = t; return true; }
                }
                point = c;
                return false;
            }
        }
    }
}
