#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Diagnostics.Runtime;

namespace Golfin.EditorTools.BuildSize
{
    /// <summary>
    /// build_size_diet Phase 1 / 2.7 / 3 — the A/B capture rig.
    ///
    /// THE POINT IS THAT THE TWO HALVES ARE THE SAME CAMERA.
    ///   "Before/after, same camera transform" is not something a human eye can guarantee by
    ///   flying a scene view twice. So the transforms are DERIVED from the scene every run —
    ///   from the tee marker, the pin, and (where there is one) the bridge — and the rig is run
    ///   twice with different labels around the import change. Same code, same scene, same
    ///   numbers in, so the only variable left between the two PNGs is the texture import.
    ///   Every camera pose it used is written into the manifest next to the images.
    ///
    /// WHICH HOLES, AND WHY NOT THE ONES THE SPEC NAMED.
    ///   The SPEC asks for holes 1, 6 and 12. Phase 1 turned out to change the PBR Bridge
    ///   textures, and a GUID search of the 18 generated hole scenes says the bridge prefabs
    ///   appear on hole 07 (17 references) and hole 12 (48) and NOWHERE else. So hole 07 is
    ///   added — without it the change's main subject is under-covered — and holes 1 and 6 are
    ///   kept as CONTROLS: they contain no bridge, so their frames must come out identical, and
    ///   a difference there would mean Phase 1 reached something it should not have.
    ///
    /// IT NEVER SAVES A SCENE. Scenes are opened, read, captured and closed; the rig adds one
    /// temporary camera and destroys it. Hole scenes are per-machine generated files
    /// (Docs/Pipeline/TREES_AND_GENERATED_SCENES.md) and a stray save would bake editor state
    /// into one.
    /// </summary>
    public static class BuildSizeCaptureRig
    {
        const string Tag = "[BuildSizeCaptureRig]";
        const string ShotDir = "Docs/Specs/Active/build_size_diet/screenshots";
        const string SceneFmt = "Assets/Golf/Courses/lomond-country-club/Generated/Hole_{0:D2}_Geo.unity";
        const string GreenFmt = "Assets/Resources/HoleData/lomond-country-club/Hole_{0:D2}/green.json";

        // iPhone 14 portrait — the resolution every capture in this project is judged at
        // (memory: feedback_capture_resolution_iphone14).
        const int W = 1170, H = 2532;

        /// <summary>07 and 12 carry the bridge; 01 and 06 are the controls. See the class docs.</summary>
        static readonly int[] Holes = { 1, 6, 7, 12 };

        [MenuItem("Tools/Golfin/Build Size/Capture A-B — label BEFORE", false, 300)]
        public static void CaptureBefore() => Capture("before");

        [MenuItem("Tools/Golfin/Build Size/Capture A-B — label AFTER", false, 301)]
        public static void CaptureAfter() => Capture("after");

        /// <summary>-executeMethod … .BuildSizeCaptureRig.CaptureBeforeBatch</summary>
        public static void CaptureBeforeBatch() => Capture("before");

        /// <summary>-executeMethod … .BuildSizeCaptureRig.CaptureAfterBatch</summary>
        public static void CaptureAfterBatch() => Capture("after");

        public static void Capture(string label)
        {
            Directory.CreateDirectory(ShotDir);
            var manifest = new StringBuilder();
            manifest.AppendLine($"# screenshots/_camera_manifest_{label}.txt — build_size_diet A/B rig");
            manifest.AppendLine($"# {DateTime.Now:yyyy-MM-dd HH:mm:ss}   {W}x{H} (iPhone 14 portrait)");
            manifest.AppendLine("# Poses are DERIVED from the scene, so the before and after runs use identical");
            manifest.AppendLine("# transforms. If a row differs between the two manifests, the pair is not an A/B.");
            manifest.AppendLine();

            string openScenePath = EditorSceneManager.GetActiveScene().path;

            foreach (int hole in Holes)
            {
                string scenePath = string.Format(SceneFmt, hole);
                if (!File.Exists(scenePath))
                {
                    manifest.AppendLine($"hole {hole:D2}: SCENE MISSING at {scenePath} — skipped " +
                                        "(generated hole scenes are per-machine and gitignored).");
                    Debug.LogWarning($"{Tag} hole {hole:D2}: no scene at {scenePath}, skipped.");
                    continue;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                Vector3 tee = FindTee(hole, out string teeSrc);
                Vector3 pin = FindGreenCentre(hole, out string pinSrc);
                var bridges = FindBridgeRenderers();

                var camGo = new GameObject("~BuildSizeCaptureCam") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 55f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 5000f;
                cam.clearFlags = CameraClearFlags.Skybox;

                try
                {
                    manifest.AppendLine($"hole {hole:D2}   tee={V(tee)} ({teeSrc})   green={V(pin)} ({pinSrc})");

                    // 1. From the tee, down the hole — the frame a player opens on.
                    Shoot(cam, tee + Vector3.up * 1.7f, pin, hole, "tee", label, manifest);

                    // 2. 20 m behind the green looking back, per SPEC Phase 1.4.
                    Vector3 back = pin + (pin - tee).normalized * 20f;
                    Shoot(cam, new Vector3(back.x, pin.y + 6f, back.z), pin, hole, "behindgreen", label, manifest);

                    // 3. The bridge, where there is one — 25 m out, roughly eye height. This is the
                    //    frame that actually judges Phase 1; a bridge is a few pixels from a tee.
                    if (bridges.Count > 0)
                    {
                        // ONE FRAME PER BRIDGE. Hole 12 carries two spans ~100 m apart; merging
                        // every bridge renderer into one Bounds put the camera in the woodland
                        // between them and the first pass produced two photographs of a spruce.
                        for (int bi = 0; bi < bridges.Count; bi++)
                        {
                            Bounds b = bridges[bi];
                            Vector3 across = Mathf.Abs(b.size.x) > Mathf.Abs(b.size.z) ? Vector3.forward : Vector3.right;
                            float span = Mathf.Max(b.size.x, b.size.z);
                            string suffix = bridges.Count > 1 ? (bi + 1).ToString() : "";

                            Vector3 eyeFar = b.center + across * (span * 0.9f) + Vector3.up * (b.extents.y + 22f);
                            manifest.AppendLine($"          bridge{suffix} centre={V(b.center)} size={V(b.size)} span={span:F1}m");
                            Shoot(cam, eyeFar, b.center, hole, "bridgeWide" + suffix, label, manifest);

                            // Close read of the deck — where an albedo cap is actually judged.
                            Vector3 eyeNear = b.center + across * (span * 0.30f) + Vector3.up * (b.extents.y * 0.8f + 4f);
                            Shoot(cam, eyeNear, b.center, hole, "bridgeClose" + suffix, label, manifest);
                        }
                    }
                    else
                    {
                        manifest.AppendLine("          no PBR Bridge renderer in this scene — CONTROL hole, " +
                                            "its frames must be identical before/after.");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(camGo);
                }
            }

            // Leave the editor as it was found (feedback_leave_editor_clean). Never saves.
            if (!string.IsNullOrEmpty(openScenePath) && File.Exists(openScenePath))
                EditorSceneManager.OpenScene(openScenePath, OpenSceneMode.Single);

            var manifestPath = $"{ShotDir}/_camera_manifest_{label}.txt";
            File.WriteAllText(manifestPath, manifest.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"{Tag} '{label}' pass complete — manifest at {manifestPath}\n{manifest}");
        }

        static void Shoot(Camera cam, Vector3 eye, Vector3 lookAt, int hole, string view,
                          string label, StringBuilder manifest)
        {
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.LookRotation((lookAt - eye).normalized, Vector3.up);
            string path = $"{ShotDir}/hole{hole:D2}_{view}_{label}.png";
            CaptureCore.SnapCamera(cam, W, H, $"hole{hole:D2}_{view}_{label}", path);
            manifest.AppendLine($"          {view,-12} eye={V(eye)} look={V(lookAt)} " +
                                $"euler={V(cam.transform.rotation.eulerAngles)} -> {Path.GetFileName(path)}");
        }

        /// <summary>
        /// The tee the player actually plays from: the midpoint of the paired
        /// <c>HoleRoot/Anchors/TeeMarker_&lt;set&gt;_L</c> / <c>_R</c> markers, preferring the
        /// regular set. NOT the first object whose name contains "tee" — that is
        /// <c>HoleRoot/Tees</c>, an empty container sitting at the world origin, and the first
        /// pass of this rig duly shot every hole from (0,0,0).
        /// </summary>
        static Vector3 FindTee(int hole, out string source)
        {
            var all = UnityEngine.Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var set in new[] { "regular", "front", "back", "ladies" })
            {
                var l = all.FirstOrDefault(t => t.name.Equals($"TeeMarker_{set}_L", StringComparison.Ordinal));
                var r = all.FirstOrDefault(t => t.name.Equals($"TeeMarker_{set}_R", StringComparison.Ordinal));
                if (l == null || r == null) continue;
                source = $"TeeMarker_{set}_L/R midpoint";
                return (l.position + r.position) * 0.5f;
            }

            var any = all.FirstOrDefault(t => t.name.StartsWith("TeeMarker_", StringComparison.Ordinal));
            if (any != null) { source = "scene object '" + any.name + "'"; return any.position; }

            var numbered = all.Where(t => t.parent != null && t.parent.name == "Tees" &&
                                          t.position.sqrMagnitude > 0.01f)
                              .OrderBy(t => t.name, StringComparer.Ordinal).FirstOrDefault();
            if (numbered != null)
            {
                source = "Tees/" + numbered.name;
                var v = numbered.position;
                return new Vector3(v.x, SampleGround(v.x, v.z), v.z);
            }

            source = "FALLBACK terrain centre — no tee marker found";
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return Vector3.zero;
            var d = terrain.terrainData;
            return terrain.transform.position + new Vector3(d.size.x * 0.5f, 0f, d.size.z * 0.1f);
        }

        /// <summary>
        /// Green centre from green.json's boundsMin/boundsMax — the baked green, not a guess, and
        /// the same numbers the physics reads. Y comes off the terrain so the camera is not
        /// buried or floating.
        /// </summary>
        static Vector3 FindGreenCentre(int hole, out string source)
        {
            // The flag is the thing a player aims at, and it is authored in the scene; green.json
            // bounds are the fallback for a hole whose flag object is named differently.
            var flag = UnityEngine.Object
                .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name.StartsWith("Flag_", StringComparison.Ordinal) && t.parent != null
                                     && t.parent.name == "Greens");
            if (flag != null) { source = "scene object '" + flag.name + "'"; return flag.position; }

            string path = string.Format(GreenFmt, hole);
            if (File.Exists(path))
            {
                try
                {
                    var j = JsonUtility.FromJson<GreenJson>(File.ReadAllText(path));
                    if (j != null && j.boundsMax != null && j.boundsMin != null)
                    {
                        float cx = (j.boundsMin.x + j.boundsMax.x) * 0.5f;
                        float cz = (j.boundsMin.z + j.boundsMax.z) * 0.5f;
                        float y = SampleGround(cx, cz);
                        source = "green.json bounds";
                        return new Vector3(cx, y, cz);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{Tag} hole {hole:D2}: green.json unreadable ({e.Message}).");
                }
            }
            source = "FALLBACK terrain centre — green.json unusable";
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (terrain == null) return Vector3.forward * 100f;
            var d = terrain.terrainData;
            var c = terrain.transform.position + new Vector3(d.size.x * 0.5f, 0f, d.size.z * 0.9f);
            c.y = SampleGround(c.x, c.z);
            return c;
        }

        static float SampleGround(float x, float z)
        {
            var terrain = UnityEngine.Object.FindFirstObjectByType<Terrain>();
            return terrain == null ? 0f : terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        /// <summary>
        /// One <see cref="Bounds"/> per BRIDGE, not one per renderer and not one for the lot.
        /// Renderers are matched by their material coming from the PBR Bridge pack, then grouped
        /// by the highest ancestor named "Bridge*" — a bridge is a deep LOD hierarchy
        /// (Sides/Side_L/Top_L_LOD0/Top_L_LOD1), and two of them on one hole are 100 m apart.
        /// </summary>
        static List<Bounds> FindBridgeRenderers()
        {
            var byRoot = new Dictionary<Transform, Bounds>();
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool hit = false;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    var p = AssetDatabase.GetAssetPath(m);
                    if (!string.IsNullOrEmpty(p) && p.StartsWith(PackTextureBudget.BridgeRoot, StringComparison.Ordinal))
                    { hit = true; break; }
                }
                if (!hit) continue;

                // The prefab-instance root IS the bridge: Bridges/Bridge_withLODs and
                // Bridges/Bridge_withLODs (1) are two instances under one container. Climbing to
                // the highest "Bridge*" ancestor instead lands on the CONTAINER, merges both
                // spans into one 142 m Bounds and aims the camera at the woodland between them —
                // which is exactly what the previous pass photographed.
                // GetOutermostPrefabInstanceRoot is edit-mode-only, which is where this rig runs
                // (memory: reference_playmode_hides_prefab_instance).
                var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(r.gameObject);
                Transform best;
                if (prefabRoot != null)
                {
                    best = prefabRoot.transform;
                }
                else
                {
                    best = r.transform;
                    for (Transform t = r.transform; t != null; t = t.parent)
                        if (t.name.StartsWith("Bridge", StringComparison.OrdinalIgnoreCase) &&
                            !t.name.Equals("Bridges", StringComparison.OrdinalIgnoreCase))
                            best = t;
                }
                if (byRoot.TryGetValue(best, out var b)) { b.Encapsulate(r.bounds); byRoot[best] = b; }
                else byRoot[best] = r.bounds;
            }
            return byRoot.OrderByDescending(kv => kv.Value.size.sqrMagnitude).Select(kv => kv.Value).ToList();
        }

        static string V(Vector3 v) => string.Format(CultureInfo.InvariantCulture, "({0:F2},{1:F2},{2:F2})", v.x, v.y, v.z);

        [Serializable] class GreenJson { public Vec3 boundsMin; public Vec3 boundsMax; }
        [Serializable] class Vec3 { public float x, y, z; }
    }
}
#endif
