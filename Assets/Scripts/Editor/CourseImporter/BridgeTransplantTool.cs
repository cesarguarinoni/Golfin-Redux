#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Runtime;

namespace Golfin.CourseImport
{
    /// <summary>
    /// Stage A + Stage B7 of <c>Docs/Specs/Active/bridge_transplant/SPEC.md</c>.
    ///
    /// A) Transplants the bridge instances that exist only in the archived capture scenes
    ///    (<c>Generated/Video/Hole_NN_Geo.unity</c>) into the live play scenes
    ///    (<c>Generated/Hole_NN_Geo.unity</c>), under a scene-ROOT container named
    ///    <c>Bridges</c>.
    ///
    /// B7) Generates, per transplanted bridge, a <c>Deck_Collision</c> child carrying a
    ///    MeshFilter (deck walking surface, authored in the bridge root's LOCAL space) and a
    ///    <see cref="SurfaceMarker"/> of <see cref="SurfaceType.Bridge"/>, and NO MeshRenderer.
    ///    <c>BakeZoneJsonTool</c> then bakes it into zones.json as a Bridge group, which
    ///    outranks Water (priority 95 &gt; 80) so a ball on the deck classifies as Bridge and
    ///    <c>BakedHeightProvider.SampleHeight</c> returns the deck Y via barycentric Path β.
    ///
    /// HARD CONSTRAINTS (see SPEC "Architecture context"):
    ///  • <c>Bridges</c> is a scene-ROOT object and NOTHING bridge-related may live under
    ///    <c>StandaloneTrees</c> / <c>PaintedTrees</c> — <c>TreeObstacleBaker.OnSceneSaving</c>
    ///    would harvest it as tree cylinders and corrupt <c>tree_obstacles.csv</c> (Fact 4).
    ///  • The Video scene is opened ADDITIVELY and closed WITHOUT SAVING. This tool never
    ///    mutates it (it copies property modifications; it does not move the GameObject).
    ///  • Never hand-edit the .unity YAML, never re-import a hole, never re-bake heightmap.bytes.
    ///
    /// DECK SOURCE — SPEC DEVIATION, measured 2026-08-31. SPEC B7 says the deck-top plane is
    /// derivable from the <c>Top_L_*</c> / <c>Top_R_*</c> renderer bounds. Measured on
    /// Bridge_withLODs, <c>Top_L_LOD0</c>'s up-facing tris sit at local Y 3.874–4.009 across
    /// X 2.486–2.836 — that is the 35 cm HANDRAIL CAP on one edge, 3.1 m above the walkway.
    /// The walking surface is <c>Main_LOD0</c>: up-facing tris at Y 0.702–0.793 across
    /// X -2.399–2.408, Z -29.968–30.927 (the full 4.8 m × 60.9 m deck). This tool therefore
    /// prefers <c>Main_LOD*</c> and keeps the whole-model-bounds route only as the fallback
    /// the SPEC specifies for models with no identifiable deck mesh.
    /// </summary>
    public static class BridgeTransplantTool
    {
        private const string CourseId         = "lomond-country-club";
        /// <summary>Scene-ROOT container name. Public so BridgeInstanceCatalog agrees with it.</summary>
        public  const string ContainerName    = "Bridges";
        private const string DeckChildName    = "Deck_Collision";
        /// <summary>
        /// Deck meshes live under <c>Data/hole-NN-geo/</c>, NOT under <c>Generated/</c>.
        /// <c>Assets/Golf/Courses/*/Generated/*</c> is gitignored (.gitignore:111) — every machine
        /// builds its own hole scenes — so anything written there evaporates at the repo boundary.
        /// <c>Data/hole-NN-geo/</c> is the tracked sibling that already carries TerrainData and
        /// standalone_trees.csv, and is where every durable per-hole artifact belongs.
        /// </summary>
        private static string DeckMeshDir(int hole) => $"Assets/Golf/Courses/{CourseId}/Data/hole-{hole:D2}-geo";

        /// <summary>The five holes that carry a bridge in the Video scenes (SPEC ground-truth table).</summary>
        public static readonly int[] BridgeHoles = { 7, 8, 9, 12, 17 };

        /// <summary>Target world size of one deck-mesh grid cell, in metres.</summary>
        private const float DeckCellMeters = 1.0f;
        private const int   DeckMaxDiv     = 128;
        private const int   DeckMinDiv     = 2;

        // ── Menu items ────────────────────────────────────────────────────────────

        [MenuItem("Import/Transplant Bridges/Transplant Current Hole", false, 250)]
        public static void TransplantCurrentHole()
        {
            if (!TryResolveActiveHole(out Scene live, out int n, "Transplant Bridges")) return;
            TransplantIntoScene(live, n);
        }

        [MenuItem("Import/Transplant Bridges/Transplant Hole 07", false, 260)] public static void TransplantH07() => TransplantHole(7);
        [MenuItem("Import/Transplant Bridges/Transplant Hole 08", false, 261)] public static void TransplantH08() => TransplantHole(8);
        [MenuItem("Import/Transplant Bridges/Transplant Hole 09", false, 262)] public static void TransplantH09() => TransplantHole(9);
        [MenuItem("Import/Transplant Bridges/Transplant Hole 12", false, 263)] public static void TransplantH12() => TransplantHole(12);
        [MenuItem("Import/Transplant Bridges/Transplant Hole 17", false, 264)] public static void TransplantH17() => TransplantHole(17);

        [MenuItem("Import/Transplant Bridges/Generate Deck Meshes (Current Hole)", false, 300)]
        public static void GenerateDeckMeshesCurrentHole()
        {
            if (!TryResolveActiveHole(out Scene live, out int n, "Generate Deck Meshes")) return;
            GenerateDeckMeshes(live, n);
        }

        // ── Stage A — transplant ──────────────────────────────────────────────────

        /// <summary>Opens the live hole scene Single, then transplants. Mirrors TreeObstacleBaker.BakeHole.</summary>
        public static void TransplantHole(int n)
        {
            string livePath = GetLiveScenePath(n);
            if (livePath == null) return;
            if (AnyOpenSceneDirty("TransplantHole")) return;
            Scene live = EditorSceneManager.OpenScene(livePath, OpenSceneMode.Single);
            TransplantIntoScene(live, n);
        }

        /// <summary>
        /// Copies every scene-root GameObject whose name matches <c>^[Bb]ridge</c> out of the
        /// hole's Video scene into <paramref name="live"/>, under a scene-root
        /// <c>Bridges</c> container. Returns the number of instances created.
        /// </summary>
        public static int TransplantIntoScene(Scene live, int n)
        {
            string videoPath = GetVideoScenePath(n);
            if (videoPath == null)
            {
                Debug.LogError($"[BridgeTransplant] Hole {n:D2}: no Video scene at the expected path — nothing to transplant.");
                return 0;
            }
            if (live.path.Contains("/Video/"))
            {
                Debug.LogError($"[BridgeTransplant] Refusing to transplant INTO a Video scene ('{live.path}').");
                return 0;
            }

            Scene video = EditorSceneManager.OpenScene(videoPath, OpenSceneMode.Additive);
            if (!video.IsValid() || !video.isLoaded)
            {
                Debug.LogError($"[BridgeTransplant] Hole {n:D2}: failed to open '{videoPath}' additively.");
                return 0;
            }

            var log = new StringBuilder();
            int created = 0;
            try
            {
                var sources = new List<GameObject>();
                foreach (var root in video.GetRootGameObjects())
                    if (IsBridgeName(root.name)) sources.Add(root);

                if (sources.Count == 0)
                {
                    Debug.LogWarning($"[BridgeTransplant] Hole {n:D2}: no scene-root GameObject matching ^[Bb]ridge in '{videoPath}'.");
                    return 0;
                }

                GameObject container = GetOrCreateContainer(live);

                // Idempotent: rebuild the container from scratch so a re-run can never
                // double-add. Deck meshes are regenerated by the Stage-B7 menu item.
                for (int i = container.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(container.transform.GetChild(i).gameObject);

                log.Append($"[BridgeTransplant] Hole {n:D2}: {sources.Count} bridge instance(s) from {videoPath}\n");

                foreach (var src in sources)
                {
                    GameObject inst = CloneInstance(src, live, container.transform);
                    if (inst == null) continue;
                    created++;

                    Transform t = inst.transform;
                    Vector3 e = t.rotation.eulerAngles;
                    log.Append($"  · '{inst.name}'  pos=({t.position.x:F4}, {t.position.y:F4}, {t.position.z:F4})")
                       .Append($"  rot=({t.rotation.x:F7}, {t.rotation.y:F7}, {t.rotation.z:F7}, {t.rotation.w:F7})")
                       .Append($"  eulerHint=({e.x:F2}, {e.y:F2}, {e.z:F2})")
                       .Append($"  lossyScale=({t.lossyScale.x:F4}, {t.lossyScale.y:F4}, {t.lossyScale.z:F4})\n");

                    log.Append("    verify: ").Append(CompareHierarchies(src.transform, t)).Append('\n');
                }

                EditorSceneManager.MarkSceneDirty(live);
            }
            finally
            {
                // Close WITHOUT saving — the Video scenes are read-only source of truth.
                EditorSceneManager.CloseScene(video, removeScene: true);
            }

            // Keep the TRACKED catalog in step with the scene, exactly as TreePlacer keeps
            // standalone_trees.csv in step. Generated/*.unity is gitignored, so without this the
            // transplant reaches no other machine while zones.json does — see BridgeInstanceCatalog.
            BridgeInstanceCatalog.ExportSceneQuiet(live);

            Debug.Log(log.ToString());
            return created;
        }

        /// <summary>
        /// Recreates <paramref name="src"/> (a prefab or model-prefab instance living at the
        /// root of the Video scene) inside <paramref name="live"/> under <paramref name="parent"/>.
        ///
        /// Copies the FULL property-modification set, not just the root TRS: the hole-7
        /// instance carries a scale/position override on a CHILD transform
        /// (m_LocalScale.y = 4.09, m_LocalPosition.y = -6.74 on fileID 2560872485283614753)
        /// in addition to the root's, and a root-TRS-only copy would silently drop it.
        /// </summary>
        private static GameObject CloneInstance(GameObject src, Scene live, Transform parent)
        {
            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(src);
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError($"[BridgeTransplant] '{src.name}' is not a prefab/model instance — cannot resolve a source asset. SKIPPED (surface, never hand-rebuild).");
                return null;
            }
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
            {
                Debug.LogError($"[BridgeTransplant] '{src.name}': failed to load source asset '{assetPath}'. SKIPPED.");
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, live);
            if (inst == null)
            {
                Debug.LogError($"[BridgeTransplant] '{src.name}': InstantiatePrefab returned null for '{assetPath}'. SKIPPED.");
                return null;
            }

            // Parent BEFORE applying the modifications: the container sits at identity at the
            // scene origin, so the source's root-local TRS (== its world TRS, it was a scene
            // root) lands verbatim as the clone's local TRS.
            inst.transform.SetParent(parent, worldPositionStays: false);

            var mods = PrefabUtility.GetPropertyModifications(src);
            if (mods != null) PrefabUtility.SetPropertyModifications(inst, mods);

            inst.name = src.name; // preserve the " (1)" suffixes
            EditorUtility.SetDirty(inst);
            return inst;
        }

        /// <summary>
        /// Walks two hierarchies in lockstep by child name and reports the worst world-space
        /// divergence. This is the acceptance evidence for "exact world TRS", not the log line.
        /// </summary>
        private static string CompareHierarchies(Transform a, Transform b)
        {
            float maxPos = 0f, maxRot = 0f, maxScale = 0f;
            int compared = 0, missing = 0;
            string worst = "";
            CompareRec(a, b, ref maxPos, ref maxRot, ref maxScale, ref compared, ref missing, ref worst);
            return $"{compared} transform(s) compared, {missing} unmatched; "
                 + $"max |Δpos|={maxPos:F6} m, max Δangle={maxRot:F6}°, max |Δscale|={maxScale:F6}"
                 + (string.IsNullOrEmpty(worst) ? "" : $" (worst: {worst})");
        }

        private static void CompareRec(Transform a, Transform b,
            ref float maxPos, ref float maxRot, ref float maxScale,
            ref int compared, ref int missing, ref string worst)
        {
            float dp = Vector3.Distance(a.position, b.position);
            float dr = Quaternion.Angle(a.rotation, b.rotation);
            float ds = Vector3.Distance(a.lossyScale, b.lossyScale);
            if (dp > maxPos || dr > maxRot || ds > maxScale)
            {
                if (dp > maxPos) maxPos = dp;
                if (dr > maxRot) maxRot = dr;
                if (ds > maxScale) maxScale = ds;
                worst = a.name;
            }
            compared++;

            for (int i = 0; i < a.childCount; i++)
            {
                Transform ca = a.GetChild(i);
                Transform cb = b.Find(ca.name);
                if (cb == null) { missing++; continue; }
                CompareRec(ca, cb, ref maxPos, ref maxRot, ref maxScale, ref compared, ref missing, ref worst);
            }
        }

        private static GameObject GetOrCreateContainer(Scene live)
        {
            foreach (var root in live.GetRootGameObjects())
                if (root.name == ContainerName) return root;

            var go = new GameObject(ContainerName);
            SceneManager.MoveGameObjectToScene(go, live);
            go.transform.SetParent(null);                    // scene ROOT — never under a tree container (Fact 4)
            go.transform.position   = Vector3.zero;
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        /// <summary>Matches the SPEC's <c>^[Bb]ridge</c> selector — and nothing else.</summary>
        public static bool IsBridgeName(string name)
            => !string.IsNullOrEmpty(name)
               && (name.StartsWith("Bridge", StringComparison.Ordinal)
                || name.StartsWith("bridge", StringComparison.Ordinal));

        // ── Stage B7 — deck meshes ────────────────────────────────────────────────

        /// <summary>Generates a Deck_Collision child + saved mesh asset for every bridge in the scene.</summary>
        public static int GenerateDeckMeshes(Scene live, int n)
        {
            GameObject container = null;
            foreach (var root in live.GetRootGameObjects())
                if (root.name == ContainerName) { container = root; break; }

            if (container == null)
            {
                Debug.LogError($"[BridgeTransplant] Hole {n:D2}: no '{ContainerName}' root object — run Transplant first.");
                return 0;
            }

            Directory.CreateDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "..", DeckMeshDir(n))));

            var log = new StringBuilder();
            log.Append($"[BridgeTransplant] Hole {n:D2}: generating deck meshes for {container.transform.childCount} bridge(s)\n");
            int built = 0;

            for (int i = 0; i < container.transform.childCount; i++)
            {
                Transform bridge = container.transform.GetChild(i);
                if (BuildDeckFor(bridge, n, i, log)) built++;
            }

            EditorSceneManager.MarkSceneDirty(live);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
            return built;
        }

        private static bool BuildDeckFor(Transform bridge, int hole, int index, StringBuilder log)
        {
            // Drop any previous deck so the step is repeatable, and so its triangles never
            // pollute the deck-source search below.
            Transform old = bridge.Find(DeckChildName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);

            var sources = FindDeckSources(bridge, out string sourceDesc);
            if (sources.Count == 0)
            {
                Debug.LogError($"[BridgeTransplant] '{bridge.name}': no deck source mesh found. NOT building a deck — surface, never guess.");
                return false;
            }

            // Collect the up-facing triangles of the deck source, expressed in the bridge
            // root's LOCAL space. Local (not world) up is the right test: the hole-12 / hole-17
            // bridges are x-tilted, and it is the deck's OWN top face we want, tilt included.
            var tv = new List<Vector3>();
            foreach (var mf in sources)
            {
                var mesh = mf.sharedMesh;
                var verts = mesh.vertices;
                var tris  = mesh.triangles;
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 a = bridge.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[t]]));
                    Vector3 b = bridge.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[t + 1]]));
                    Vector3 c = bridge.InverseTransformPoint(mf.transform.TransformPoint(verts[tris[t + 2]]));
                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    if (nrm.sqrMagnitude < 1e-12f) continue;
                    if (nrm.normalized.y <= 0.5f) continue;
                    tv.Add(a); tv.Add(b); tv.Add(c);
                }
            }

            if (tv.Count == 0)
            {
                Debug.LogError($"[BridgeTransplant] '{bridge.name}': deck source ({sourceDesc}) has no up-facing triangles. NOT building a deck.");
                return false;
            }

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in tv)
            {
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }

            // Grid resolution from the WORLD extent, so the non-uniform instance scales
            // (hole 7 is z=1.37, hole 8 is 0.5/0.5/0.14) give consistent cell sizes.
            Vector3 ls = bridge.lossyScale;
            int nx = Mathf.Clamp(Mathf.RoundToInt((maxX - minX) * Mathf.Abs(ls.x) / DeckCellMeters), DeckMinDiv, DeckMaxDiv);
            int nz = Mathf.Clamp(Mathf.RoundToInt((maxZ - minZ) * Mathf.Abs(ls.z) / DeckCellMeters), DeckMinDiv, DeckMaxDiv);

            int vx = nx + 1, vz = nz + 1;
            var ys      = new float[vx * vz];
            var covered = new bool[vx * vz];
            for (int i = 0; i < ys.Length; i++) ys[i] = float.MinValue;

            for (int iz = 0; iz < vz; iz++)
            for (int ix = 0; ix < vx; ix++)
            {
                float x = Mathf.Lerp(minX, maxX, vx == 1 ? 0f : (float)ix / nx);
                float z = Mathf.Lerp(minZ, maxZ, vz == 1 ? 0f : (float)iz / nz);
                if (TryTopY(tv, x, z, out float y)) { ys[iz * vx + ix] = y; covered[iz * vx + ix] = true; }
            }

            int uncovered = FillUncovered(ys, covered, vx, vz, maxY);

            var vertices = new Vector3[vx * vz];
            for (int iz = 0; iz < vz; iz++)
            for (int ix = 0; ix < vx; ix++)
            {
                float x = Mathf.Lerp(minX, maxX, vx == 1 ? 0f : (float)ix / nx);
                float z = Mathf.Lerp(minZ, maxZ, vz == 1 ? 0f : (float)iz / nz);
                vertices[iz * vx + ix] = new Vector3(x, ys[iz * vx + ix], z);
            }

            var indices = new int[nx * nz * 6];
            int k = 0;
            for (int iz = 0; iz < nz; iz++)
            for (int ix = 0; ix < nx; ix++)
            {
                int v00 = iz * vx + ix, v10 = v00 + 1, v01 = v00 + vx, v11 = v01 + 1;
                indices[k++] = v00; indices[k++] = v01; indices[k++] = v11;
                indices[k++] = v00; indices[k++] = v11; indices[k++] = v10;
            }

            var deckMesh = new Mesh { name = DeckMeshAssetName(hole, index, bridge.name) };
            deckMesh.indexFormat = vertices.Length > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            deckMesh.vertices  = vertices;
            deckMesh.triangles = indices;
            deckMesh.RecalculateNormals();
            deckMesh.RecalculateBounds();

            string assetPath = $"{DeckMeshDir(hole)}/{deckMesh.name}.asset";
            var existingAsset = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existingAsset != null)
            {
                existingAsset.Clear();
                existingAsset.indexFormat = deckMesh.indexFormat;
                existingAsset.vertices  = vertices;
                existingAsset.triangles = indices;
                existingAsset.RecalculateNormals();
                existingAsset.RecalculateBounds();
                EditorUtility.SetDirty(existingAsset);
                UnityEngine.Object.DestroyImmediate(deckMesh);
                deckMesh = existingAsset;
            }
            else
            {
                AssetDatabase.CreateAsset(deckMesh, assetPath);
            }

            // The deck child sits at IDENTITY local TRS under the bridge root, so
            // BakeZoneJsonTool's transform.TransformPoint() carries the root-local vertices
            // into world with the full instance TRS applied — non-uniform scale included.
            var deckGO = new GameObject(DeckChildName);
            deckGO.transform.SetParent(bridge, worldPositionStays: false);
            deckGO.transform.localPosition = Vector3.zero;
            deckGO.transform.localRotation = Quaternion.identity;
            deckGO.transform.localScale    = Vector3.one;
            deckGO.AddComponent<MeshFilter>().sharedMesh = deckMesh;   // NO MeshRenderer — nothing new is drawn
            var marker = deckGO.AddComponent<SurfaceMarker>();
            marker.Type = SurfaceType.Bridge;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(deckGO);

            Vector3 wMin = bridge.TransformPoint(new Vector3(minX, minY, minZ));
            Vector3 wMax = bridge.TransformPoint(new Vector3(maxX, maxY, maxZ));
            log.Append($"  · '{bridge.name}' deck from {sourceDesc}: grid {vx}x{vz} ({vertices.Length} verts, {indices.Length / 3} tris), ")
               .Append($"local X[{minX:F3},{maxX:F3}] Z[{minZ:F3},{maxZ:F3}] Y[{minY:F3},{maxY:F3}], ")
               .Append($"{uncovered} grid vert(s) filled from neighbours, ")
               .Append($"world corners ({wMin.x:F2},{wMin.y:F2},{wMin.z:F2})..({wMax.x:F2},{wMax.y:F2},{wMax.z:F2}) → {assetPath}\n");
            return true;
        }

        /// <summary>
        /// Deck-source selection. Prefers the <c>Main_LOD*</c> walkway slab; falls back to the
        /// widest up-facing renderer only when no Main mesh exists (the SPEC's fallback route
        /// for the <c>bridgeLODs</c> FBX). Never returns the Deck_Collision mesh itself.
        /// </summary>
        private static List<MeshFilter> FindDeckSources(Transform bridge, out string desc)
        {
            var exact = new List<MeshFilter>();
            var lod1  = new List<MeshFilter>();
            foreach (var mf in bridge.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                if (mf.name == DeckChildName) continue;
                if (mf.name == "Main_LOD0") exact.Add(mf);
                else if (mf.name == "Main_LOD1") lod1.Add(mf);
            }
            if (exact.Count > 0) { desc = $"Main_LOD0 x{exact.Count}"; return exact; }
            if (lod1.Count  > 0) { desc = $"Main_LOD1 x{lod1.Count}";  return lod1;  }

            // Fallback: the single largest-XZ-footprint mesh that has any up-facing area.
            MeshFilter best = null; float bestArea = 0f;
            foreach (var mf in bridge.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.name == DeckChildName) continue;
                Bounds b = mf.sharedMesh.bounds;
                float area = Mathf.Abs(b.size.x * mf.transform.lossyScale.x)
                           * Mathf.Abs(b.size.z * mf.transform.lossyScale.z);
                if (area > bestArea) { bestArea = area; best = mf; }
            }
            if (best != null) { desc = $"fallback:largest-footprint '{best.name}'"; return new List<MeshFilter> { best }; }
            desc = "none";
            return new List<MeshFilter>();
        }

        /// <summary>Highest triangle-plane Y over (x,z) among the collected up-facing triangles.</summary>
        private static bool TryTopY(List<Vector3> tv, float x, float z, out float y)
        {
            y = float.MinValue;
            bool any = false;
            for (int i = 0; i < tv.Count; i += 3)
            {
                Vector3 a = tv[i], b = tv[i + 1], c = tv[i + 2];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float w0 = ((b.z - c.z) * (x - c.x) + (c.x - b.x) * (z - c.z)) / d;
                float w1 = ((c.z - a.z) * (x - c.x) + (a.x - c.x) * (z - c.z)) / d;
                float w2 = 1f - w0 - w1;
                const float eps = -1e-4f;
                if (w0 < eps || w1 < eps || w2 < eps) continue;
                float py = w0 * a.y + w1 * b.y + w2 * c.y;
                if (!any || py > y) { y = py; any = true; }
            }
            return any;
        }

        /// <summary>
        /// Fills grid vertices no triangle covered (gaps between the L and R deck halves, or
        /// the AABB corners of a non-rectangular deck) by BFS from their covered neighbours,
        /// so the emitted mesh is always a clean rectangular grid with ONE boundary loop —
        /// BakeZoneJsonTool chains boundary edges into polygons and a ragged edge would
        /// produce a nonsense contour. Returns how many vertices were filled.
        /// </summary>
        private static int FillUncovered(float[] ys, bool[] covered, int vx, int vz, float fallbackY)
        {
            int total = 0;
            for (int i = 0; i < covered.Length; i++) if (!covered[i]) total++;
            if (total == 0) return 0;

            var queue = new Queue<int>();
            for (int i = 0; i < covered.Length; i++) if (covered[i]) queue.Enqueue(i);

            if (queue.Count == 0)
            {
                for (int i = 0; i < ys.Length; i++) { ys[i] = fallbackY; covered[i] = true; }
                return total;
            }

            while (queue.Count > 0)
            {
                int i  = queue.Dequeue();
                int ix = i % vx, iz = i / vx;
                for (int d = 0; d < 4; d++)
                {
                    int jx = ix + (d == 0 ? 1 : d == 1 ? -1 : 0);
                    int jz = iz + (d == 2 ? 1 : d == 3 ? -1 : 0);
                    if (jx < 0 || jx >= vx || jz < 0 || jz >= vz) continue;
                    int j = jz * vx + jx;
                    if (covered[j]) continue;
                    ys[j] = ys[i];
                    covered[j] = true;
                    queue.Enqueue(j);
                }
            }
            return total;
        }

        private static string DeckMeshAssetName(int hole, int index, string bridgeName)
        {
            var sb = new StringBuilder();
            foreach (char c in bridgeName)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            return $"MESH_BridgeDeck_Hole{hole:D2}_{index:D2}_{sb}";
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool TryResolveActiveHole(out Scene live, out int n, string title)
        {
            live = EditorSceneManager.GetActiveScene();
            n = TreeObstacleBaker.ExtractHoleNumber(live.name);
            if (n < 1 || n > 18)
            {
                Debug.LogError($"[BridgeTransplant] {title}: cannot detect a hole number from active scene '{live.name}'. "
                             + "Open Generated/Hole_NN_Geo.unity first.");
                return false;
            }
            if (live.path.Contains("/Video/"))
            {
                Debug.LogError($"[BridgeTransplant] {title}: the ACTIVE scene is a Video scene ('{live.path}'). "
                             + "Those are read-only source of truth — open the live Generated/Hole_NN_Geo.unity instead.");
                return false;
            }
            return true;
        }

        private static bool AnyOpenSceneDirty(string what)
        {
            for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                Scene s = EditorSceneManager.GetSceneAt(i);
                if (s.isDirty)
                {
                    Debug.LogError($"[BridgeTransplant] {what}: '{s.path}' has unsaved changes. "
                                 + "Save or discard them first — opening a scene Single would silently drop them.");
                    return true;
                }
            }
            return false;
        }

        public static string GetLiveScenePath(int n) => ExistingPath(
            $"Assets/Golf/Courses/{CourseId}/Generated/Hole_{n:D2}_Geo.unity", n, "live");

        public static string GetVideoScenePath(int n) => ExistingPath(
            $"Assets/Golf/Courses/{CourseId}/Generated/Video/Hole_{n:D2}_Geo.unity", n, "Video");

        private static string ExistingPath(string path, int n, string label)
        {
            if (File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)))) return path;
            Debug.LogError($"[BridgeTransplant] Hole {n:D2}: no {label} scene at '{path}'.");
            return null;
        }
    }
}
#endif
