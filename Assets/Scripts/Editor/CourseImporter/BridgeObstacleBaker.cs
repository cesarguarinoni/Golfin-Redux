#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Bakes per-hole bridge railing/pier collision to
    /// Assets/Resources/HoleData/&lt;courseSlug&gt;/Hole_NN/bridge_obstacles.csv.
    /// Beat-for-beat mirror of <see cref="TreeObstacleBaker"/>: same menu shape, same
    /// <c>#&#160;bake_hash=&lt;hex8&gt;</c> FNV-1a header, same <c>sceneSaving</c> auto-rebake hook,
    /// same Resources output root.
    ///
    /// SOURCE. The bridge prefabs ship real BoxColliders (134 on Bridge_withLODs, 36 on
    /// Bridge_part_1) on children named Collider_N / Beam_Collider. Those colliders are dead
    /// weight at runtime — BallSimulation is a fixed-point integrator that never touches PhysX —
    /// but they are excellent AUTHORING data, so this baker reads them and emits fixed-point boxes.
    ///
    /// CLASSIFICATION, relative to the deck plane sampled AT EACH BOX'S OWN XZ (not a single
    /// deck Y — the hole 12 and 17 bridges are x-tilted and the hole 7 deck is cambered):
    ///   box top ABOVE  deckY + 0.02 → `railing`  (this includes the KERBS — see below)
    ///   box top BELOW  deckY − 0.15 → `pier`
    ///   box top at the deck plane   → EXCLUDED — that is the deck slab itself, already owned by
    ///                                 the Stage-B zone mesh. Double-representing it would fight
    ///                                 the ground solver.
    ///
    /// THE 0.02 UPPER BAND IS LOAD-BEARING, measured on hole 7. An earlier ±0.15 m "straddling"
    /// band swallowed the two KERB colliders — the raised lips at the deck edges, top 24.007
    /// against a deck surface of 23.947, so only 0.060 m proud. Excluding them left a ball free
    /// to roll off the deck edge at |perp| 2.404 while the nearest railing box did not begin
    /// until 2.499: the ball fell 23.7 m into the water through a 0.095 m gap, having visibly
    /// passed THROUGH the railing art. The kerbs are exactly the containment the deck needs, and
    /// they are authored collision — not something to invent. The deck slab's own box tops at
    /// 23.900 (BELOW the surface) and the abutment blocks at 23.907, so a 0.02 m tolerance
    /// separates "is the deck" from "sits on the deck" cleanly.
    ///
    /// NON-UNIFORM SCALE. Hole 7 is (1, 1, 1.37) with a 4.09× stretched child; hole 8 is
    /// (0.5, 0.5, 0.14). TreeObstacleBaker's <c>child.localScale.x // uniform scale assumed</c>
    /// shortcut is WRONG here, so every box goes through the full local-to-world matrix: the
    /// eight world corners are computed and reduced to a yaw-rotated AABB.
    /// </summary>
    public static class BridgeObstacleBaker
    {
        private const string CourseId  = "lomond-country-club";
        private const string DeckChild = "Deck_Collision";

        /// <summary>
        /// A box whose top rises more than this above the deck surface is something a ball can
        /// hit — a railing member or a kerb. Tight on purpose: the hole-7 kerbs clear the deck by
        /// only 0.060 m and are the difference between a ball being contained and falling 23.7 m.
        /// </summary>
        private const float AboveDeckM = 0.02f;

        /// <summary>A box whose top is this far BELOW the deck surface is a pier.</summary>
        private const float BelowDeckM = 0.15f;

        // ── Menu items ───────────────────────────────────────────────────────────

        [MenuItem("Import/Bake Bridge Obstacles/Bake Current Hole", false, 250)]
        public static void BakeCurrentHole()
        {
            var scene = EditorSceneManager.GetActiveScene();
            int n = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (n < 1 || n > 18)
            {
                Debug.LogError($"[BridgeObstacleBaker] Cannot detect hole number from scene '{scene.name}'.");
                return;
            }
            if (scene.path.Contains("/Video/"))
            {
                Debug.LogError($"[BridgeObstacleBaker] '{scene.path}' is a Video scene — read-only source of truth.");
                return;
            }
            BakeActiveScene(scene, n);
        }

        [MenuItem("Import/Bake Bridge Obstacles/Bake Hole 07", false, 260)] public static void BakeH07() => BakeHole(7);
        [MenuItem("Import/Bake Bridge Obstacles/Bake Hole 08", false, 261)] public static void BakeH08() => BakeHole(8);
        [MenuItem("Import/Bake Bridge Obstacles/Bake Hole 09", false, 262)] public static void BakeH09() => BakeHole(9);
        [MenuItem("Import/Bake Bridge Obstacles/Bake Hole 12", false, 263)] public static void BakeH12() => BakeHole(12);
        [MenuItem("Import/Bake Bridge Obstacles/Bake Hole 17", false, 264)] public static void BakeH17() => BakeHole(17);

        [MenuItem("Import/Bake Bridge Obstacles/Bake All Bridge Holes", false, 350)]
        public static void BakeAllBridgeHoles()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (int n in BridgeTransplantTool.BridgeHoles)
                {
                    EditorUtility.DisplayProgressBar("Baking bridge obstacles", $"Hole {n:D2}", 0f);
                    BakeHole(n);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                StandaloneTreeCatalog.RestoreSetup(setup);
            }
        }

        private static void BakeHole(int n)
        {
            string path = BridgeTransplantTool.GetLiveScenePath(n);
            if (path == null) return;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            BakeActiveScene(scene, n);
        }

        // ── Save hook ────────────────────────────────────────────────────────────

        [InitializeOnLoadMethod]
        private static void RegisterSaveHook()
        {
            EditorSceneManager.sceneSaving -= OnSceneSaving;
            EditorSceneManager.sceneSaving += OnSceneSaving;
        }

        private static void OnSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            int n = TreeObstacleBaker.ExtractHoleNumber(scene.name);
            if (n < 1 || n > 18) return;
            if (path.Contains("/Video/")) return;

            // No Bridges container → nothing to bake, and crucially nothing to CLEAR: a hole
            // that never had a bridge must not get an empty CSV written on every save.
            if (BridgeInstanceCatalog.FindContainer(scene) == null) return;

            string slug = Golfin.Course.Runtime.CourseSlugResolver.Resolve(path);
            if (slug == null)
            {
                Debug.LogWarning($"[BridgeObstacleBaker] OnSceneSaving: could not resolve course slug from '{path}' — skipping auto re-bake.");
                return;
            }

            var rows = HarvestScene(scene, out string _);
            if (rows == null) return;

            string newHash = ComputeHash(rows);
            string csvPath = GetCsvAssetPath(n, slug);
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", csvPath));

            if (File.Exists(fullPath))
            {
                using var reader = new StreamReader(fullPath);
                string firstLine = reader.ReadLine() ?? "";
                if (firstLine.StartsWith("# bake_hash="))
                {
                    string existing = firstLine.Substring("# bake_hash=".Length).Trim();
                    if (existing == newHash)
                    {
                        Debug.Log($"[BridgeObstacleBaker] Hole {n:D2}: bridge hash unchanged, skip re-bake.");
                        return;
                    }
                }
            }

            Debug.Log($"[BridgeObstacleBaker] Hole {n:D2}: bridge state changed, auto re-baking...");
            WriteCsv(csvPath, rows, newHash);
        }

        // ── Core ─────────────────────────────────────────────────────────────────

        private static void BakeActiveScene(UnityEngine.SceneManagement.Scene scene, int n)
        {
            string slug = Golfin.Course.Runtime.CourseSlugResolver.ResolveOrThrow(scene.path, "BridgeObstacleBaker.BakeActiveScene");
            var rows = HarvestScene(scene, out string breakdown);
            if (rows == null)
            {
                Debug.LogWarning($"[BridgeObstacleBaker] Hole {n:D2}: no bridge parts harvested. CSV not written.\n{breakdown}");
                return;
            }

            string hash = ComputeHash(rows);
            string csvPath = GetCsvAssetPath(n, slug);
            WriteCsv(csvPath, rows, hash);
            Debug.Log($"[BridgeObstacleBaker] Hole {n:D2}: baked {rows.Count} bridge part(s) → {csvPath}\n{breakdown}");
        }

        /// <summary>
        /// Harvest every BoxCollider under the scene's <c>Bridges</c> container, classified
        /// against that bridge's own deck plane. Returns CSV row strings, or null when there is
        /// nothing to write. <paramref name="breakdown"/> is the per-bridge audit line.
        /// </summary>
        public static List<string> HarvestScene(UnityEngine.SceneManagement.Scene scene, out string breakdown)
        {
            var sb = new StringBuilder();
            breakdown = "";
            var rows = new List<string>();

            var container = BridgeInstanceCatalog.FindContainer(scene);
            if (container == null) { breakdown = "no 'Bridges' container in this scene"; return null; }

            foreach (Transform bridge in container.transform)
            {
                string sourceLabel = "?";
                var deckTris = CollectDeckTriangles(bridge, out float deckMeanY, out bool hasDeck);
                if (!hasDeck)
                {
                    Debug.LogWarning(
                        $"[BridgeObstacleBaker] '{bridge.name}' has no {DeckChild} mesh — cannot classify its boxes " +
                        "against a deck plane, so NOTHING is baked for it. Run " +
                        "Import/Transplant Bridges/Generate Deck Meshes (Current Hole) first.");
                    continue;
                }

                // ── The ONE yaw frame every box on this bridge is reduced into ─────────
                // Derived from the bridge's own across axis, and derived the SAME WAY the runtime
                // reads it back: BridgeBox.ToLocalXZ treats (cos, sin) as its local +X, so the
                // baked yaw must be atan2(acrossZ, acrossX) — NOT the transform's
                // Quaternion.eulerAngles.y, whose sign convention is the opposite and which is
                // ill-defined on the x-tilted bridges.
                Vector3 across = bridge.rotation * Vector3.right;
                float bridgeYawRad = Mathf.Atan2(across.z, across.x);
                float bridgeYawDeg = bridgeYawRad * Mathf.Rad2Deg;
                float cs = Mathf.Cos(bridgeYawRad), sn = Mathf.Sin(bridgeYawRad);

                int railing = 0, pier = 0, deck = 0, duplicates = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);

                // Candidate oriented boxes, from EITHER source (see CollectCandidates).
                var candidates = CollectCandidates(bridge, out string sourceKind);
                sourceLabel = sourceKind;

                foreach (var cand in candidates)
                {
                    // Full local-to-world was already applied by CollectCandidates.
                    // (TreeObstacleBaker's `localScale.x // uniform scale assumed` shortcut is
                    // wrong here — hole 7 is (1,1,1.37) with a 4.09x stretched child.)
                    Vector3 c  = cand.centre;
                    Vector3 ex = cand.ex;
                    Vector3 ey = cand.ey;
                    Vector3 ez = cand.ez;

                    // Reduce to an AABB in the BRIDGE's frame — not in each collider's own yaw.
                    //
                    // WHY THIS MATTERS (measured on hole 7, 2026-08-31). Using each collider's
                    // own eulerAngles.y and taking max|component| about its centre is a valid
                    // bound but a badly inflating one: a truss brace is a long thin member lying
                    // DIAGONALLY, so projecting it into its own yaw frame smears its length
                    // across the perpendicular axis. The railing meshes are symmetric about the
                    // deck at ±2.26 m; that bake put the blocking faces at +1.82 and −2.48 —
                    // 0.44 m too far inboard on one side and 0.22 m too far outboard on the
                    // other, so a ball rolling one way bounced off thin air and rolling the
                    // other way fell past the railing into the water.
                    //
                    // Projecting onto the bridge's own across/along axes instead is exact in the
                    // direction that decides containment: min/max along `across` IS the member's
                    // true perpendicular extent, whatever angle it lies at.
                    float minY = float.MaxValue, maxY = float.MinValue;
                    float minLx = float.MaxValue, maxLx = float.MinValue;
                    float minLz = float.MaxValue, maxLz = float.MinValue;

                    for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 p = c + ex * sx + ey * sy + ez * sz;
                        if (p.y < minY) minY = p.y;
                        if (p.y > maxY) maxY = p.y;
                        float lx =  p.x * cs + p.z * sn;   // same basis as BridgeBox.ToLocalXZ
                        float lz = -p.x * sn + p.z * cs;
                        if (lx < minLx) minLx = lx; if (lx > maxLx) maxLx = lx;
                        if (lz < minLz) minLz = lz; if (lz > maxLz) maxLz = lz;
                    }

                    float halfX  = (maxLx - minLx) * 0.5f;
                    float halfZ  = (maxLz - minLz) * 0.5f;
                    float midLx  = (maxLx + minLx) * 0.5f;
                    float midLz  = (maxLz + minLz) * 0.5f;
                    // Frame centre back to world XZ: (cs,sn) and (−sn,cs) are an orthonormal basis.
                    float cX = midLx * cs - midLz * sn;
                    float cZ = midLx * sn + midLz * cs;

                    float deckY = SampleDeckY(deckTris, cX, cZ, deckMeanY);

                    string profile;
                    if (maxY > deckY + AboveDeckM)      { profile = "railing"; railing++; }
                    else if (maxY < deckY - BelowDeckM) { profile = "pier";    pier++;    }
                    else                                { deck++; continue; }  // the deck slab — Stage B owns it

                    string row = FormatRow(cX, cZ, minY, maxY, halfX, halfZ, NormalizeYaw(bridgeYawDeg), profile);

                    // DEDUPE. These prefabs carry colliders on BOTH LOD levels — Main_LOD0 and
                    // Main_LOD1 are byte-identical boxes, likewise End_1_LOD0/LOD1 — so a naive
                    // harvest bakes every such part twice (124 rows for 93 distinct boxes on
                    // hole 7). Duplicates are not a correctness bug (the provider returns the
                    // earliest hit either way) but they inflate the grid and the tracked CSV, and
                    // an identical row carries no information. Dedupe on the formatted row so the
                    // test is exactly "same box", at full CSV precision.
                    if (!seen.Add(row)) { duplicates++; continue; }
                    rows.Add(row);
                }

                sb.Append($"  '{bridge.name}' [{sourceLabel}]: railing={railing} pier={pier} "
                        + $"deck-excluded={deck} lod-duplicates-dropped={duplicates} "
                        + $"kept={rows.Count} deckMeanY={deckMeanY:F3}\n");
            }

            breakdown = sb.ToString().TrimEnd();
            return rows.Count > 0 ? rows : null;
        }

        // ── Candidate boxes ──────────────────────────────────────────────────────

        /// <summary>An oriented box in world space, before reduction into the bridge's yaw frame.</summary>
        private struct Candidate
        {
            public Vector3 centre, ex, ey, ez;   // ex/ey/ez are HALF-extent vectors
        }

        /// <summary>
        /// Meshes that become collision when a model ships no BoxColliders. Matched on the LOD0
        /// renderer name; the deck-relative rule below still decides railing vs pier, so this list
        /// only answers "does this part collide at all", never "what is it".
        ///
        /// Deliberately NOT everything: a single AABB is a good stand-in for a slab-like member
        /// (a railing, a pier) and a terrible one for sparse geometry. <c>StreetLight_Poles_LOD0</c>
        /// is 0.97 x 8.21 x 46.49 m of mostly empty air — one box there would be a phantom wall
        /// down the whole bridge. <c>Line_LOD0</c> (the overhead wire) is the same. <c>Fence_*</c>
        /// is a 1 mm plane already inside the railing box, and <c>Main_*</c> is the deck, which the
        /// Stage-B zone mesh owns.
        /// </summary>
        private static bool IsCollidingPartName(string n)
            => n.StartsWith("Railing_", StringComparison.Ordinal)
            || n.StartsWith("Top_",     StringComparison.Ordinal)
            || n.StartsWith("Beams_L_", StringComparison.Ordinal)
            || n.StartsWith("Beams_R_", StringComparison.Ordinal)
            || n.StartsWith("Pier_",    StringComparison.Ordinal)
            || n.StartsWith("End_",     StringComparison.Ordinal)
            || n.StartsWith("Bottom_",  StringComparison.Ordinal);

        /// <summary>
        /// Where a bridge's collision geometry comes from.
        ///
        /// PREFERRED — the model's own BoxColliders. <c>Bridge_withLODs</c> ships 134 and
        /// <c>Bridge_part_1</c> 36, authored per member. They are dead weight to PhysX at runtime
        /// (BallSimulation never touches it) but they are exactly the authoring data this bake wants.
        ///
        /// FALLBACK — renderer AABBs, for <c>bridgeLODs.fbx</c>, which ships ZERO colliders and is
        /// the source for three of the seven instances (hole 8 x2, hole 9). SPEC Risk 2 offered
        /// "author a prefab variant with railing/pier boxes" or "ship those three deck-only";
        /// Cesar chose to author them (2026-08-31). Authoring them as a prefab variant is the one
        /// route that CANNOT work: <c>Assets/Packs/</c> is gitignored (.gitignore:107), so a
        /// hand-built variant next to the FBX would never leave this machine — the same class of
        /// bug as the gitignored hole scenes. Deriving them from the model's own meshes puts the
        /// result in the TRACKED <c>bridge_obstacles.csv</c> instead, needs no new asset, and stays
        /// correct if the art is ever re-imported.
        ///
        /// One AABB per member is coarser than the per-member colliders: a lattice railing becomes
        /// a solid wall rather than a set of braces. For containment that is the safer error — and
        /// on hole 7 the authored truss colliders only contained the ball because of the two kerb
        /// boxes, which this model has no equivalent of.
        /// </summary>
        private static List<Candidate> CollectCandidates(Transform bridge, out string sourceKind)
        {
            var list = new List<Candidate>();

            var colliders = bridge.GetComponentsInChildren<BoxCollider>(true);
            if (colliders.Length > 0)
            {
                sourceKind = $"colliders x{colliders.Length}";
                foreach (var bc in colliders)
                {
                    var t = bc.transform;
                    list.Add(new Candidate
                    {
                        centre = t.TransformPoint(bc.center),
                        ex = t.TransformVector(new Vector3(bc.size.x * 0.5f, 0f, 0f)),
                        ey = t.TransformVector(new Vector3(0f, bc.size.y * 0.5f, 0f)),
                        ez = t.TransformVector(new Vector3(0f, 0f, bc.size.z * 0.5f)),
                    });
                }
                return list;
            }

            int considered = 0;
            foreach (var mf in bridge.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.name == DeckChild) continue;                    // our own generated deck
                if (mf.name.EndsWith("_LOD1", StringComparison.Ordinal)) continue;
                if (mf.name.EndsWith("_LOD2", StringComparison.Ordinal)) continue;
                if (!IsCollidingPartName(mf.name)) continue;
                considered++;

                var t = mf.transform;
                Bounds b = mf.sharedMesh.bounds;
                list.Add(new Candidate
                {
                    centre = t.TransformPoint(b.center),
                    ex = t.TransformVector(new Vector3(b.extents.x, 0f, 0f)),
                    ey = t.TransformVector(new Vector3(0f, b.extents.y, 0f)),
                    ez = t.TransformVector(new Vector3(0f, 0f, b.extents.z)),
                });
            }
            sourceKind = $"renderer-AABB fallback (no colliders on this model) x{considered}";
            return list;
        }

        // ── Deck plane sampling ──────────────────────────────────────────────────

        /// <summary>World-space triangle soup of this bridge's Deck_Collision mesh, in flat triples.</summary>
        private static List<Vector3> CollectDeckTriangles(Transform bridge, out float meanY, out bool hasDeck)
        {
            meanY = 0f;
            hasDeck = false;
            var deck = bridge.Find(DeckChild);
            var tris = new List<Vector3>();
            if (deck == null) return tris;
            var mf = deck.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return tris;

            var verts = mf.sharedMesh.vertices;
            var idx   = mf.sharedMesh.triangles;
            double sum = 0;
            for (int i = 0; i < idx.Length; i++)
            {
                Vector3 w = deck.TransformPoint(verts[idx[i]]);
                tris.Add(w);
                sum += w.y;
            }
            if (tris.Count == 0) return tris;
            meanY = (float)(sum / tris.Count);
            hasDeck = true;
            return tris;
        }

        /// <summary>
        /// Deck Y directly above/below (x,z), by point-in-triangle over the deck soup. Falls back
        /// to the deck's mean Y outside the footprint — piers and railing ends overhang the deck
        /// rectangle, and a fallback of 0 would classify every one of them as "railing".
        /// </summary>
        private static float SampleDeckY(List<Vector3> tris, float x, float z, float fallback)
        {
            float best = float.MinValue;
            bool any = false;
            for (int i = 0; i + 2 < tris.Count; i += 3)
            {
                Vector3 a = tris[i], b = tris[i + 1], c = tris[i + 2];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float w0 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float w1 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                float w2 = 1f - w0 - w1;
                const float eps = -1e-4f;
                if (w0 < eps || w1 < eps || w2 < eps) continue;
                float py = w0 * a.y + w1 * b.y + w2 * c.y;
                if (!any || py > best) { best = py; any = true; }
            }
            return any ? best : fallback;
        }

        // ── Helpers (mirrors of TreeObstacleBaker) ───────────────────────────────

        private static string FormatRow(float cx, float cz, float baseY, float topY,
                                        float halfX, float halfZ, float yawDeg, string profile)
            => string.Format(CultureInfo.InvariantCulture,
                             "{0:F4},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4},{7}",
                             cx, cz, baseY, topY, halfX, halfZ, yawDeg, profile);

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            return yaw;
        }

        private static string ComputeHash(List<string> rows)
        {
            var sorted = new List<string>(rows);
            sorted.Sort(StringComparer.Ordinal);

            var sb = new StringBuilder();
            foreach (var r in sorted) sb.Append(r).Append('\n');

            byte[] data = Encoding.UTF8.GetBytes(sb.ToString());
            uint hash = 2166136261u; // FNV-1a 32-bit
            foreach (byte b in data) { hash ^= b; hash *= 16777619u; }
            return hash.ToString("x8");
        }

        private static void WriteCsv(string csvAssetPath, List<string> rows, string hash)
        {
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", csvAssetPath));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // Explicit '\n', never AppendLine — a tracked CSV must not churn wholesale just
            // because it was written on Windows instead of macOS.
            var sb = new StringBuilder();
            sb.Append($"# bake_hash={hash}").Append('\n');
            sb.Append("centerX,centerZ,baseY,topY,halfX,halfZ,yawDeg,profileName").Append('\n');
            foreach (var row in rows) sb.Append(row).Append('\n');

            File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(csvAssetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[BridgeObstacleBaker] Wrote {rows.Count} rows to {csvAssetPath} (hash={hash})");
        }

        public static string GetCsvAssetPath(int holeNumber, string courseSlug)
            => $"Assets/Resources/HoleData/{courseSlug}/Hole_{holeNumber:D2}/bridge_obstacles.csv";
    }
}
#endif
