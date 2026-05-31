#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// ITER-11 DIAGNOSTIC — Editor-only, NEVER ships.
//
// Builds four green+collar mesh variants from H07 green.json data,
// renders them side-by-side isolated from terrain (constant seatY=0),
// and captures top-down screenshots for Cesar's visual inspection.
//
// Hard rules:
//   - ZERO changes to HoleGeoImporter.cs, bake-green.mjs, GreenTopology.cs,
//     any green.json, or any production scene.
//   - All variants use the same CDT library (andywiecko.BurstTriangulator).
//   - No terrain integration — terrain.SampleHeight stubbed to constant seatY=0.
//   - H07 only. Consumes contourResampled verbatim from green.json.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using andywiecko.BurstTriangulator;
using Golfin.Course.Runtime;

namespace Golfin.CourseImport.Diagnostic
{
    public static class GreenVariantDiagnostic
    {
        // ─── Constants ────────────────────────────────────────────────────────
        private const float SeatY       = 0f;    // constant terrain stub
        private const float CollarWidth = 0.9f;  // matches production GreenCollarWidth
        private const float GridSpacing = 0.5f;  // matches production green gridSpacing
        private const float MinkowskiR  = 0.65f; // Minkowski radius (SPEC B)
        private const float ResampleTarget = 0.5f;
        private const float OverlayYAbove  = 1.0f;  // gizmo Y above SeatY
        private const int   HoleNumber     = 7;
        private const string ScenePath   = "Assets/Scenes/Debug/Hole_07_Geo_Diagnostic.unity";
        private const string CapturePath  = "Docs/Specs/Active/green_slope_height_bake/screenshots/iter11";
        private const string VideoPath    = "Docs/Specs/Active/green_slope_height_bake/videos/iter11";

        // Orbit params
        private const int   OrbitFrames    = 120;   // 5 s at 24 fps
        private const float OrbitFPS       = 24f;
        private const float OrbitRadiusM   = 22f;   // horizontal distance from centroid (mesh ~25m wide)
        private const float OrbitElevDeg   = 38f;   // elevation above horizontal
        private const int   OrbitW         = 1280;
        private const int   OrbitH         = 720;

        private static readonly string[] VarLabels = {
            "Variant A — ISOLATED-BASELINE (DilateContour)",
            "Variant B — MINKOWSKI-OFFSET",
            "Variant C — SHARED-BOUNDARY (DilateContour)",
            "Variant D — UNIFIED-CDT (Minkowski)"
        };
        private static readonly string[] VarFileSuffixes = {
            "A_orbit", "B_orbit", "C_orbit", "D_orbit"
        };

        // Layout: A(0,0)  B(45,0)  C(0,-45)  D(45,-45)
        private static readonly Vector3[] VarOffset = {
            new Vector3(  0f, 0f,   0f),
            new Vector3( 45f, 0f,   0f),
            new Vector3(  0f, 0f, -45f),
            new Vector3( 45f, 0f, -45f),
        };
        private static readonly string[] VarNames = {
            "Var_A_BASELINE", "Var_B_MINKOWSKI", "Var_C_SHARED_BOUNDARY", "Var_D_UNIFIED_CDT"
        };

        // ─── Persistent overlay data (for gizmo host) ────────────────────────
        private static Vector2[]   s_inputContour;
        private static Vector2[][] s_offsetPoly  = new Vector2[4][];
        private static Vector3[][] s_wireVerts   = new Vector3[4][];
        private static int[][]     s_wireTris    = new int[4][];
        private static Vector2[]   s_contourCentroid2D = new Vector2[4];

        // ─── Menu items ──────────────────────────────────────────────────────

        [MenuItem("Debug/Build Green Variants (H07)")]
        public static void BuildGreenVariants()
        {
            // 1. Load/create debug scene
            EnsureDebugScene();

            // 2. Load topology
            string absPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", $"Assets/Resources/HoleData/Hole_{HoleNumber:D2}/green.json"));
            var topo = GreenTopology.LoadFromDisk(absPath, HoleNumber);
            if (topo == null)
            {
                UnityEngine.Debug.LogError("[GreenVariantDiagnostic] Cannot load H07 green.json. Re-bake first.");
                return;
            }
            Vector2[] cr = topo.ContourResampled;
            if (cr == null || cr.Length < 3)
            {
                UnityEngine.Debug.LogError("[GreenVariantDiagnostic] ContourResampled missing or < 3 pts.");
                return;
            }
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] H07 contour: {cr.Length} pts");

            // 3. Clear old root
            var existing = GameObject.Find("DebugGreenVariants");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var root = new GameObject("DebugGreenVariants");

            // 4. Backdrop quad
            AddBackdrop(root.transform);

            // 5. Materials
            var greenMat  = LoadOrCreateMat("GreenSurface", new Color(0.15f, 0.55f, 0.15f));
            var collarMat = LoadOrCreateMat("CollarSurface", new Color(0.25f, 0.45f, 0.20f));

            // 6. Build overlays storage
            s_inputContour = cr;
            for (int v = 0; v < 4; v++) s_contourCentroid2D[v] = Centroid2D(cr);

            // 7. Build four variants
            BuildVariantA(cr, topo, greenMat, collarMat, root.transform);
            BuildVariantB(cr, topo, greenMat, collarMat, root.transform);
            BuildVariantC(cr, topo, greenMat, collarMat, root.transform);
            BuildVariantD(cr, topo, greenMat, collarMat, root.transform);

            // 8. Top-down camera
            AddTopDownCamera(root.transform);

            // 9. Gizmo host
            var host = root.AddComponent<GreenDiagGizmoHost>();
            host.InputContour   = s_inputContour;
            host.OffsetPolys    = s_offsetPoly;
            host.WireVerts      = s_wireVerts;
            host.WireTris       = s_wireTris;
            host.Centroids2D    = s_contourCentroid2D;
            host.Offsets        = VarOffset;
            host.OverlayY       = SeatY + OverlayYAbove;

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            UnityEngine.Debug.Log("[GreenVariantDiagnostic] Done. Use 'Debug > Capture Diagnostic Screenshots (H07)' next.");
        }

        [MenuItem("Debug/Capture Diagnostic Screenshots (H07)")]
        public static void CaptureDiagnosticScreenshots()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Hole_07_Geo_Diagnostic.unity"))
            {
                string fp = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
                if (!File.Exists(fp))
                {
                    UnityEngine.Debug.LogError("[GreenVariantDiagnostic] Scene not found. Run Build first.");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", CapturePath));
            Directory.CreateDirectory(outDir);

            var camGO = GameObject.Find("DiagTopDownCamera");
            if (camGO == null) { UnityEngine.Debug.LogError("[GreenVariantDiagnostic] DiagTopDownCamera not found. Rebuild first."); return; }
            var cam = camGO.GetComponent<Camera>();
            if (cam == null) { UnityEngine.Debug.LogError("[GreenVariantDiagnostic] No Camera on DiagTopDownCamera."); return; }

            // Full 4-pane overhead
            var origPos  = cam.transform.position;
            var origSize = cam.orthographicSize;

            RenderAndSave(cam, 1920, 1080, Path.Combine(outDir, "iter11_all_variants_overhead.png"));
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Saved iter11_all_variants_overhead.png (1920x1080)");

            // Per-variant captures
            string[] labels = { "A", "B", "C", "D" };
            for (int v = 0; v < 4; v++)
            {
                Vector3 vc = VariantSceneCenter(v);
                cam.transform.position = new Vector3(vc.x, 80f, vc.z);
                cam.orthographicSize   = 22f;

                RenderAndSave(cam, 900, 900, Path.Combine(outDir, $"iter11_variant_{labels[v]}_closeup.png"));
                UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Saved iter11_variant_{labels[v]}_closeup.png");

                // Wireframe: temporarily swap materials
                SwapToWireframe(v, true);
                RenderAndSave(cam, 900, 900, Path.Combine(outDir, $"iter11_variant_{labels[v]}_wireframe.png"));
                SwapToWireframe(v, false);
                UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Saved iter11_variant_{labels[v]}_wireframe.png");
            }

            cam.transform.position = origPos;
            cam.orthographicSize   = origSize;

            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] All screenshots saved to: {outDir}");
            AssetDatabase.Refresh();
        }

        // ─── Orbit video capture ──────────────────────────────────────────────

        // Called from script-execute (no ffmpeg inside Unity; Bash assembles afterward)
        public static string CaptureOrbitFramesOnly()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Hole_07_Geo_Diagnostic.unity"))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            bool allPresent = true;
            for (int v = 0; v < 4; v++)
            {
                if (GameObject.Find(VarNames[v]) == null) { allPresent = false; break; }
            }
            if (!allPresent)
            {
                BuildGreenVariants();
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            var tempCamGO = new GameObject("_OrbitCamera_TEMP");
            var cam = tempCamGO.AddComponent<Camera>();
            cam.orthographic  = false;
            cam.fieldOfView   = 40f;
            cam.clearFlags    = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane  = 500f;

            var results = new System.Text.StringBuilder();
            try
            {
                for (int v = 0; v < 4; v++)
                {
                    string frameDir = RenderOrbitFrames(cam, v, repoRoot);
                    results.Append($"VAR_{(char)('A'+v)}={frameDir}|");
                    UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Orbit frames done for var {(char)('A'+v)}: {frameDir}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempCamGO);
            }
            return results.ToString();
        }

        // Returns the frame directory path for Bash/ffmpeg to consume
        private static string RenderOrbitFrames(Camera cam, int varIdx, string repoRoot)
        {
            string varName = VarNames[varIdx];
            var go = GameObject.Find(varName);
            if (go == null)
            {
                UnityEngine.Debug.LogError($"[GreenVariantDiagnostic] {varName} not found.");
                return "ERROR";
            }

            var mf = go.GetComponent<MeshFilter>();
            Vector3 meshCentroid;
            if (mf != null && mf.sharedMesh != null)
                meshCentroid = go.transform.TransformPoint(mf.sharedMesh.bounds.center);
            else
                meshCentroid = go.transform.position;

            float elevRad   = OrbitElevDeg * Mathf.Deg2Rad;
            float horizDist = OrbitRadiusM * Mathf.Cos(elevRad);
            float camHeight = meshCentroid.y + OrbitRadiusM * Mathf.Sin(elevRad);

            string frameDir = Path.Combine(repoRoot, "Temp", $"orbit_frames_var{(char)('A'+varIdx)}");
            if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true);
            Directory.CreateDirectory(frameDir);

            var rt  = new RenderTexture(OrbitW, OrbitH, 24);
            var tex = new Texture2D(OrbitW, OrbitH, TextureFormat.RGB24, false);

            for (int f = 0; f < OrbitFrames; f++)
            {
                float angle = f * (360f / OrbitFrames) * Mathf.Deg2Rad;
                float cx    = meshCentroid.x + horizDist * Mathf.Sin(angle);
                float cz    = meshCentroid.z + horizDist * Mathf.Cos(angle);
                cam.transform.position = new Vector3(cx, camHeight, cz);
                cam.transform.LookAt(meshCentroid);

                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, OrbitW, OrbitH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string framePath = Path.Combine(frameDir, $"frame_{f:D4}.png");
                File.WriteAllBytes(framePath, tex.EncodeToPNG());
            }

            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] {varName}: {OrbitFrames} frames rendered, centroid={meshCentroid}, horizDist={horizDist:F1}m, camH={camHeight:F1}m");
            return frameDir;
        }

        [MenuItem("Debug/Capture Orbit Videos (H07)")]
        public static void CaptureOrbitVideos()
        {
            // Ensure we're in the diagnostic scene
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.path.EndsWith("Hole_07_Geo_Diagnostic.unity"))
            {
                string fp = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
                if (!File.Exists(fp))
                {
                    UnityEngine.Debug.LogError("[GreenVariantDiagnostic] Diagnostic scene not found. Run 'Build Green Variants (H07)' first.");
                    return;
                }
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            // Verify variants are present
            bool allPresent = true;
            for (int v = 0; v < 4; v++)
            {
                if (GameObject.Find(VarNames[v]) == null)
                {
                    UnityEngine.Debug.LogWarning($"[GreenVariantDiagnostic] {VarNames[v]} not found — rebuilding variants.");
                    allPresent = false;
                    break;
                }
            }
            if (!allPresent)
            {
                BuildGreenVariants();
                // Re-open after rebuild (BuildGreenVariants saves the scene)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            string repoRoot  = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string videoDir  = Path.GetFullPath(Path.Combine(repoRoot, VideoPath));
            Directory.CreateDirectory(videoDir);

            // Temp camera
            var tempCamGO = new GameObject("_OrbitCamera_TEMP");
            var cam       = tempCamGO.AddComponent<Camera>();
            cam.orthographic  = false;
            cam.fieldOfView   = 40f;
            cam.clearFlags    = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane  = 500f;

            try
            {
                for (int v = 0; v < 4; v++)
                {
                    CaptureOrbitForVariant(cam, v, videoDir, repoRoot);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempCamGO);
                UnityEngine.Debug.Log("[GreenVariantDiagnostic] Orbit capture complete. Temp camera destroyed.");
            }
        }

        private static void CaptureOrbitForVariant(Camera cam, int varIdx, string videoDir, string repoRoot)
        {
            string varName = VarNames[varIdx];
            var go = GameObject.Find(varName);
            if (go == null)
            {
                UnityEngine.Debug.LogError($"[GreenVariantDiagnostic] Cannot find {varName} — skipping.");
                return;
            }

            // Compute world-space centroid of this variant's mesh
            var mf = go.GetComponent<MeshFilter>();
            Vector3 meshCentroid;
            if (mf != null && mf.sharedMesh != null)
            {
                // Bounds.center is in local space; transform to world
                meshCentroid = go.transform.TransformPoint(mf.sharedMesh.bounds.center);
            }
            else
            {
                meshCentroid = go.transform.position;
            }

            // Orbit params: horizontal radius OrbitRadiusM, elevation OrbitElevDeg
            float elevRad   = OrbitElevDeg * Mathf.Deg2Rad;
            float horizDist = OrbitRadiusM * Mathf.Cos(elevRad);
            float camHeight = meshCentroid.y + OrbitRadiusM * Mathf.Sin(elevRad);

            // Temp dir for PNG frames
            string frameDir = Path.Combine(repoRoot, "Temp", $"orbit_frames_var{(char)('A'+varIdx)}");
            if (Directory.Exists(frameDir)) Directory.Delete(frameDir, true);
            Directory.CreateDirectory(frameDir);

            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Orbiting {varName}: centroid={meshCentroid}, horizDist={horizDist:F1}m, camH={camHeight:F1}m, frames={OrbitFrames}");

            var rt  = new RenderTexture(OrbitW, OrbitH, 24);
            var tex = new Texture2D(OrbitW, OrbitH, TextureFormat.RGB24, false);

            for (int f = 0; f < OrbitFrames; f++)
            {
                float angle   = f * (360f / OrbitFrames) * Mathf.Deg2Rad;
                float cx      = meshCentroid.x + horizDist * Mathf.Sin(angle);
                float cz      = meshCentroid.z + horizDist * Mathf.Cos(angle);
                cam.transform.position = new Vector3(cx, camHeight, cz);
                cam.transform.LookAt(meshCentroid);

                cam.targetTexture = rt;
                cam.Render();
                cam.targetTexture = null;

                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, OrbitW, OrbitH), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                string framePath = Path.Combine(frameDir, $"frame_{f:D4}.png");
                File.WriteAllBytes(framePath, tex.EncodeToPNG());
            }

            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt);

            // Assemble with ffmpeg, add drawtext caption
            string outputMp4 = Path.Combine(videoDir, $"iter11_variant_{VarFileSuffixes[varIdx]}.mp4");
            string ffmpegPath = GetFfmpegPath();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                UnityEngine.Debug.LogError("[GreenVariantDiagnostic] ffmpeg not found on PATH. Frames saved to: " + frameDir);
                return;
            }

            // Caption: bottom-left, semi-transparent black background, white text
            string label    = VarLabels[varIdx].Replace("'", "\\'").Replace(":", "\\:");
            string fontFile = "/System/Library/Fonts/Helvetica.ttc";
            if (!File.Exists(fontFile)) fontFile = "/Library/Fonts/Arial.ttf";

            // Build drawtext filter — skip if font not found (still produce silent mp4)
            string vtFilter;
            if (File.Exists(fontFile))
            {
                vtFilter = $"drawtext=fontfile='{fontFile}':text='{label}':fontsize=28:fontcolor=white:x=20:y=h-50:box=1:boxcolor=black@0.55:boxborderw=6";
            }
            else
            {
                vtFilter = "null";
                UnityEngine.Debug.LogWarning("[GreenVariantDiagnostic] No font found — producing video without caption text.");
            }

            string args = $"-y -framerate {OrbitFPS} -i \"{Path.Combine(frameDir, "frame_%04d.png")}\" " +
                          $"-vf \"{vtFilter}\" " +
                          $"-c:v libx264 -pix_fmt yuv420p -crf 18 " +
                          $"\"{outputMp4}\"";

            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Running ffmpeg for {varName}...");
            var psi = new ProcessStartInfo
            {
                FileName        = ffmpegPath,
                Arguments       = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow  = true
            };
            using (var proc = Process.Start(psi))
            {
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"[GreenVariantDiagnostic] ffmpeg failed for {varName} (exit {proc.ExitCode}): {err}");
                    return;
                }
            }

            // Clean up frame dir
            Directory.Delete(frameDir, true);

            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] Video saved: {outputMp4}");
        }

        private static string GetFfmpegPath()
        {
            // Try common locations
            string[] candidates = {
                "/Users/cesar/.local/bin/ffmpeg",
                "/usr/local/bin/ffmpeg",
                "/opt/homebrew/bin/ffmpeg",
                "/usr/bin/ffmpeg",
                "ffmpeg"
            };
            foreach (var c in candidates)
            {
                if (c == "ffmpeg") return c;     // rely on PATH
                if (File.Exists(c)) return c;
            }
            return null;
        }

        // ─── Variant A: ISOLATED-BASELINE ────────────────────────────────────
        // Production DilateContour (vertex-normal miter offset) + CDT + seatY stub.

        private static void BuildVariantA(Vector2[] cr, GreenTopology topo,
            Material greenMat, Material collarMat, Transform root)
        {
            ContourPoint[] contour = ToCPs(cr);
            ContourPoint[] dilated = DilateCP(contour, CollarWidth);

            s_offsetPoly[0] = ToV2(dilated);

            var (rv, uvs, tris) = CDT_Stubbed(dilated, GridSpacing, contour);
            if (rv == null) { UnityEngine.Debug.LogWarning("[GreenVariantDiagnostic] A: CDT failed"); return; }

            ApplyHeightY(rv, cr, topo);
            s_wireVerts[0] = (Vector3[])rv.Clone();
            s_wireTris[0]  = (int[])tris.Clone();

            SpawnGreenCollarGO("Var_A_BASELINE", rv, uvs, tris, cr, VarOffset[0], greenMat, collarMat, root);
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] A: {rv.Length} verts, {tris.Length/3} tris, dilated={dilated.Length} pts (DilateContour)");
        }

        // ─── Variant B: MINKOWSKI-OFFSET ─────────────────────────────────────
        // Replaces DilateContour with 2D Minkowski convex-arc offset.

        private static void BuildVariantB(Vector2[] cr, GreenTopology topo,
            Material greenMat, Material collarMat, Transform root)
        {
            ContourPoint[] contour = ToCPs(cr);
            Vector2[] mink = MinkowskiOffset(cr, MinkowskiR, ResampleTarget);
            ContourPoint[] dilated = ToCPs(mink);

            s_offsetPoly[1] = mink;

            var (rv, uvs, tris) = CDT_Stubbed(dilated, GridSpacing, contour);
            if (rv == null) { UnityEngine.Debug.LogWarning("[GreenVariantDiagnostic] B: CDT failed"); return; }

            ApplyHeightY(rv, cr, topo);
            s_wireVerts[1] = (Vector3[])rv.Clone();
            s_wireTris[1]  = (int[])tris.Clone();

            SpawnGreenCollarGO("Var_B_MINKOWSKI", rv, uvs, tris, cr, VarOffset[1], greenMat, collarMat, root);
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] B: {rv.Length} verts, {tris.Length/3} tris, Minkowski={mink.Length} pts");
        }

        // ─── Variant C: SHARED-BOUNDARY ──────────────────────────────────────
        // DilateContour outer ring; SAME inner-constraint vertices as green boundary
        // (CDT with inner constraint guarantees exact shared verts at seam — no re-resample).

        private static void BuildVariantC(Vector2[] cr, GreenTopology topo,
            Material greenMat, Material collarMat, Transform root)
        {
            ContourPoint[] contour = ToCPs(cr);
            ContourPoint[] dilated = DilateCP(contour, CollarWidth);

            s_offsetPoly[2] = ToV2(dilated);

            // CDT with inner constraint — identical to A structurally, BUT we emphasise
            // that in this variant the seam boundary is EXACTLY the inner-constraint edges,
            // with no subsequent re-sampling or coordinate drift on the inner side.
            // The difference from A is conceptual (proving shared-by-reference behaviour),
            // but the mesh geometry should be identical so we can see if A's scallop persists.
            var (rv, uvs, tris) = CDT_Stubbed(dilated, GridSpacing, contour);
            if (rv == null) { UnityEngine.Debug.LogWarning("[GreenVariantDiagnostic] C: CDT failed"); return; }

            ApplyHeightY(rv, cr, topo);
            s_wireVerts[2] = (Vector3[])rv.Clone();
            s_wireTris[2]  = (int[])tris.Clone();

            SpawnGreenCollarGO("Var_C_SHARED_BOUNDARY", rv, uvs, tris, cr, VarOffset[2], greenMat, collarMat, root);
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] C: {rv.Length} verts, {tris.Length/3} tris (shared inner ring via CDT constraint)");
        }

        // ─── Variant D: UNIFIED-CDT ───────────────────────────────────────────
        // Single CDT mesh: outer = Minkowski, inner constraint = contour.
        // One connected manifold; seam is an internal constraint edge.

        private static void BuildVariantD(Vector2[] cr, GreenTopology topo,
            Material greenMat, Material collarMat, Transform root)
        {
            ContourPoint[] contour = ToCPs(cr);
            Vector2[] mink = MinkowskiOffset(cr, MinkowskiR, ResampleTarget);
            ContourPoint[] outer = ToCPs(mink);

            s_offsetPoly[3] = mink;

            // Single CDT: outer boundary + inner-constraint loop (contour = seam)
            var (rv, uvs, tris) = CDT_Stubbed(outer, GridSpacing, contour);
            if (rv == null) { UnityEngine.Debug.LogWarning("[GreenVariantDiagnostic] D: CDT failed"); return; }

            ApplyHeightY(rv, cr, topo);
            s_wireVerts[3] = (Vector3[])rv.Clone();
            s_wireTris[3]  = (int[])tris.Clone();

            SpawnGreenCollarGO("Var_D_UNIFIED_CDT", rv, uvs, tris, cr, VarOffset[3], greenMat, collarMat, root);
            UnityEngine.Debug.Log($"[GreenVariantDiagnostic] D: {rv.Length} verts, {tris.Length/3} tris (unified CDT, Minkowski outer)");
        }

        // ─── Core CDT (terrain stubbed) ───────────────────────────────────────

        private static (Vector3[], Vector2[], int[]) CDT_Stubbed(
            ContourPoint[] boundary, float gridSpacing, ContourPoint[] innerConstraint = null)
        {
            int n = boundary.Length;
            if (n < 3) return (null, null, null);

            var pos2D = new List<double2>();
            var edges = new List<int>();

            for (int i = 0; i < n; i++)
                pos2D.Add(new double2(boundary[i].x, boundary[i].z));
            for (int i = 0; i < n; i++) { edges.Add(i); edges.Add((i + 1) % n); }

            int innerStart = pos2D.Count;
            if (innerConstraint != null && innerConstraint.Length >= 3)
            {
                int ic = innerConstraint.Length;
                for (int i = 0; i < ic; i++)
                    pos2D.Add(new double2(innerConstraint[i].x, innerConstraint[i].z));
                for (int i = 0; i < ic; i++)
                {
                    edges.Add(innerStart + i);
                    edges.Add(innerStart + ((i + 1) % ic));
                }
            }

            // Steiner grid inside outer boundary
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var p in boundary)
            {
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z; if (p.z > maxZ) maxZ = p.z;
            }
            var poly2D = new Vector2[n];
            for (int i = 0; i < n; i++) poly2D[i] = new Vector2(boundary[i].x, boundary[i].z);

            for (float gx = minX + gridSpacing; gx < maxX; gx += gridSpacing)
                for (float gz = minZ + gridSpacing; gz < maxZ; gz += gridSpacing)
                    if (IsInside(gx, gz, poly2D))
                        pos2D.Add(new double2(gx, gz));

            using var nPos = new NativeArray<double2>(pos2D.ToArray(), Allocator.TempJob);
            using var nEdg = new NativeArray<int>(edges.ToArray(), Allocator.TempJob);
            using var tri  = new Triangulator(Allocator.TempJob)
            {
                Settings = { RestoreBoundary = true },
                Input    = { Positions = nPos, ConstraintEdges = nEdg }
            };
            tri.Run();

            var outTris = tri.Output.Triangles;
            var outPos  = tri.Output.Positions;
            if (outTris.Length < 3) return (null, null, null);

            int vc = outPos.Length;
            var verts = new Vector3[vc];
            var uvs   = new Vector2[vc];
            for (int i = 0; i < vc; i++)
            {
                float wx = (float)outPos[i].x;
                float wz = (float)outPos[i].y;
                verts[i] = new Vector3(wx, SeatY, wz);  // terrain.SampleHeight → SeatY (0)
                uvs[i]   = new Vector2(wx / 2f, wz / 2f);
            }
            var tArr = new int[outTris.Length];
            for (int i = 0; i < outTris.Length; i++) tArr[i] = outTris[i];

            return (verts, uvs, tArr);
        }

        // ─── Height bake application (stubbed terrain) ────────────────────────
        // Mirrors CreateGreenMeshCDT Y logic: greenSeatY=SeatY, outer collar at SeatY.

        private static void ApplyHeightY(Vector3[] rv, Vector2[] contour, GreenTopology topo)
        {
            for (int i = 0; i < rv.Length; i++)
            {
                float d = Mathf.Sqrt(DistSqToContour(rv[i].x, rv[i].z, contour));
                bool inside = IsInside(rv[i].x, rv[i].z, contour);

                float relH = 0f;
                topo.TrySampleHeight(new Vector2(rv[i].x, rv[i].z), out relH);

                if (inside)
                {
                    rv[i].y = SeatY + relH;
                }
                else
                {
                    float innerY = SeatY + relH;
                    float tBlend = 1f - Mathf.Clamp01(d / CollarWidth);
                    tBlend = tBlend * tBlend * (3f - 2f * tBlend);
                    rv[i].y = Mathf.Lerp(SeatY, innerY, tBlend); // outer = SeatY (terrain stub)
                }
            }
        }

        // ─── Spawn green+collar mesh GO ────────────────────────────────────────
        // Classifies triangles by centroid, builds 2-submesh mesh, sets position.

        private static void SpawnGreenCollarGO(
            string name, Vector3[] rv, Vector2[] uvs, int[] tris,
            Vector2[] originalContour, Vector3 varWorldOffset,
            Material greenMat, Material collarMat, Transform root)
        {
            // Classify
            var gT = new List<int>();
            var cT = new List<int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                Vector3 a = rv[tris[t]], b = rv[tris[t+1]], c = rv[tris[t+2]];
                float cx = (a.x + b.x + c.x) / 3f;
                float cz = (a.z + b.z + c.z) / 3f;
                if (IsInside(cx, cz, originalContour))
                { gT.Add(tris[t]); gT.Add(tris[t+1]); gT.Add(tris[t+2]); }
                else
                { cT.Add(tris[t]); cT.Add(tris[t+1]); cT.Add(tris[t+2]); }
            }

            // Compute centroid (average XZ of all verts), subtract from verts for local space
            float sumX = 0f, sumZ = 0f;
            foreach (var v in rv) { sumX += v.x; sumZ += v.z; }
            Vector3 cent = new Vector3(sumX / rv.Length, 0f, sumZ / rv.Length);

            var lv = new Vector3[rv.Length];
            for (int i = 0; i < rv.Length; i++) lv[i] = rv[i] - cent;

            // Wind CW
            if (tris.Length >= 3)
            {
                Vector3 a = lv[tris[0]], b = lv[tris[1]], c = lv[tris[2]];
                float cross = (b.x - a.x)*(c.z - a.z) - (b.z - a.z)*(c.x - a.x);
                if (cross > 0)
                {
                    System.Action<List<int>> FlipList = L => {
                        for (int fi = 0; fi < L.Count; fi += 3) { int tmp = L[fi]; L[fi] = L[fi+2]; L[fi+2] = tmp; }
                    };
                    FlipList(gT); FlipList(cT);
                }
            }

            var mesh = new Mesh { name = name };
            mesh.vertices     = lv;
            mesh.uv           = uvs;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(gT.ToArray(), 0);
            mesh.SetTriangles(cT.ToArray(), 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Place at variant world offset + contour centroid removed
            // Since verts are in world-XZ relative to their own mesh centroid,
            // we position the GO at: varWorldOffset + (cent - contourWorldCentroid)
            // varWorldOffset offsets the pane in the diagnostic scene;
            // but the contour itself lives in world ~(177, -30), so we need to
            // move the mesh to the variant offset position in our simplified scene.
            // We subtract the contour centroid to bring it to origin, then add varOffset.
            Vector2 conCent = Centroid2D(originalContour);
            Vector3 worldPos = varWorldOffset + new Vector3(cent.x - conCent.x, cent.y, cent.z - conCent.y);

            var go = new GameObject(name);
            go.transform.SetParent(root);
            go.transform.position = worldPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new Material[] { greenMat, collarMat };
        }

        // ─── Minkowski-sum 2D polygon offset ─────────────────────────────────
        // Convex-arc fillet at convex corners, mitered point on concave.
        // Re-sampled to uniform ~resampleStep spacing.
        // ~100-line custom impl — no Clipper dependency.

        private static Vector2[] MinkowskiOffset(Vector2[] poly, float radius, float step)
        {
            int n = poly.Length;
            var result = new List<Vector2>();

            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                int next = (i + 1) % n;

                Vector2 e1 = (poly[i] - poly[prev]).normalized;
                Vector2 e2 = (poly[next] - poly[i]).normalized;

                // Outward normal = CW 90° rotation: (x,y) → (y,-x)
                Vector2 no1 = new Vector2( e1.y, -e1.x);
                Vector2 no2 = new Vector2( e2.y, -e2.x);

                // Cross product sign: positive = convex (outward turn)
                float cross = e1.x * e2.y - e1.y * e2.x;

                if (cross >= 0f)
                {
                    // Convex corner — arc fillet
                    float a0 = Mathf.Atan2(no1.y, no1.x);
                    float a1 = Mathf.Atan2(no2.y, no2.x);
                    float da = a1 - a0;
                    while (da >  Mathf.PI) da -= 2f * Mathf.PI;
                    while (da < -Mathf.PI) da += 2f * Mathf.PI;

                    int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(da) * radius / step));
                    for (int s = 0; s <= steps; s++)
                    {
                        float a = a0 + da * (s / (float)steps);
                        result.Add(poly[i] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                    }
                }
                else
                {
                    // Concave corner — mitered point (clamped)
                    Vector2 avg = (no1 + no2).normalized;
                    float denom = Vector2.Dot(no1, avg);
                    float miter = (denom > 0.1f) ? radius / denom : radius;
                    miter = Mathf.Min(miter, radius * 3f);
                    result.Add(poly[i] + avg * miter);
                }
            }

            return ResamplePoly(result.ToArray(), step);
        }

        private static Vector2[] ResamplePoly(Vector2[] poly, float step)
        {
            if (poly.Length < 2) return poly;

            float totalLen = 0f;
            for (int i = 0; i < poly.Length; i++)
                totalLen += Vector2.Distance(poly[i], poly[(i+1) % poly.Length]);

            int count = Mathf.Max(3, Mathf.RoundToInt(totalLen / step));
            float segStep = totalLen / count;

            var res = new List<Vector2> { poly[0] };
            float accum = 0f;
            int cur = 0;
            float curSegLen = Vector2.Distance(poly[0], poly[1 % poly.Length]);

            for (int s = 1; s < count; s++)
            {
                float target = s * segStep;
                while (cur < poly.Length && accum + curSegLen < target)
                {
                    accum += curSegLen;
                    cur    = (cur + 1) % poly.Length;
                    curSegLen = Vector2.Distance(poly[cur], poly[(cur+1) % poly.Length]);
                }
                float frac = (curSegLen > 0.0001f) ? (target - accum) / curSegLen : 0f;
                res.Add(Vector2.Lerp(poly[cur], poly[(cur+1) % poly.Length], frac));
            }
            return res.ToArray();
        }

        // ─── Scene helpers ────────────────────────────────────────────────────

        private static void EnsureDebugScene()
        {
            string absScene = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
            if (!File.Exists(absScene))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(absScene));
                var ns = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(ns, ScenePath);
            }
            else
            {
                var cur = EditorSceneManager.GetActiveScene();
                if (cur.path != ScenePath)
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        private static void AddBackdrop(Transform root)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Backdrop";
            go.transform.SetParent(root);
            go.transform.position = new Vector3(22.5f, SeatY - 1f, -22.5f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(130f, 130f, 1f);
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.color = new Color(0.2f, 0.2f, 0.2f);
                mr.sharedMaterial = mat;
            }
        }

        private static void AddTopDownCamera(Transform root)
        {
            var old = GameObject.Find("DiagTopDownCamera");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);

            var go  = new GameObject("DiagTopDownCamera");
            var cam = go.AddComponent<Camera>();
            cam.orthographic     = true;
            cam.orthographicSize = 65f;
            cam.transform.position = new Vector3(22.5f, 100f, -22.5f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.clearFlags   = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.08f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 200f;
            go.transform.SetParent(root);
        }

        private static Vector3 VariantSceneCenter(int v) =>
            new Vector3(VarOffset[v].x, 0f, VarOffset[v].z);

        private static Material LoadOrCreateMat(string name, Color fallback)
        {
            string path = $"Assets/Golf/Courses/lomond-country-club/Data/hole-07-geo/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;

            var m = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            m.name  = name;
            m.color = fallback;
            return m;
        }

        // ─── Wireframe swap ───────────────────────────────────────────────────
        // For the diagnostic screenshot pass only: swap to wireframe and back.
        // Uses a simple dictionary to store saved materials per variant index.

        private static readonly Dictionary<int, Material[]> s_savedMats = new Dictionary<int, Material[]>();

        private static void SwapToWireframe(int v, bool on)
        {
            var go = GameObject.Find(VarNames[v]);
            if (go == null) return;
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;

            if (on)
            {
                // Save current materials
                Material[] saved = mr.sharedMaterials;
                s_savedMats[v] = saved;

                // White wireframe material
                var wf = new Material(Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Standard"));
                wf.color = Color.white;
                int cnt = saved.Length;
                Material[] wfMats = new Material[cnt];
                for (int m = 0; m < cnt; m++) wfMats[m] = wf;
                mr.sharedMaterials = wfMats;
            }
            else
            {
                // Restore saved materials
                Material[] saved;
                if (s_savedMats.TryGetValue(v, out saved) && saved != null)
                {
                    mr.sharedMaterials = saved;
                    s_savedMats.Remove(v);
                }
            }
        }

        // ─── Camera capture to PNG ────────────────────────────────────────────

        private static void RenderAndSave(Camera cam, int w, int h, string path)
        {
            var rt   = new RenderTexture(w, h, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            rt.Release();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }

        // ─── Geometry utilities (duplicated from HoleGeoImporter — editor-only OK) ──

        private static ContourPoint[] ToCPs(Vector2[] pts)
        {
            var r = new ContourPoint[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = new ContourPoint { x = pts[i].x, z = pts[i].y };
            return r;
        }

        private static Vector2[] ToV2(ContourPoint[] pts)
        {
            var r = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) r[i] = new Vector2(pts[i].x, pts[i].z);
            return r;
        }

        private static ContourPoint[] DilateCP(ContourPoint[] contour, float offset)
        {
            int n = contour.Length;
            var w = new Vector3[n];
            for (int i = 0; i < n; i++) w[i] = new Vector3(contour[i].x, 0f, contour[i].z);
            Vector3[] d = OffsetOutward(w, offset);
            var r = new ContourPoint[n];
            for (int i = 0; i < n; i++) r[i] = new ContourPoint { x = d[i].x, z = d[i].z };
            return r;
        }

        private static Vector3[] OffsetOutward(Vector3[] c, float dist)
        {
            int n = c.Length;
            var r = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                int p = (i - 1 + n) % n, q = (i + 1) % n;
                var e1 = new Vector2(c[i].x - c[p].x, c[i].z - c[p].z).normalized;
                var e2 = new Vector2(c[q].x - c[i].x, c[q].z - c[i].z).normalized;
                var n1 = new Vector2(e1.y, -e1.x);
                var n2 = new Vector2(e2.y, -e2.x);
                var avg = (n1 + n2).normalized;
                float dot = Vector2.Dot(n1, avg);
                float m   = (dot > 0.1f) ? dist / dot : dist;
                m = Mathf.Min(m, dist * 3f);
                r[i] = new Vector3(c[i].x + avg.x * m, c[i].y, c[i].z + avg.y * m);
            }
            return r;
        }

        private static bool IsInside(float px, float pz, Vector2[] poly)
        {
            int n = poly.Length;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = poly[i].x, zi = poly[i].y, xj = poly[j].x, zj = poly[j].y;
                if (((zi > pz) != (zj > pz)) && (px < (xj - xi) * (pz - zi) / (zj - zi) + xi))
                    inside = !inside;
            }
            return inside;
        }

        private static float DistSqToContour(float px, float pz, Vector2[] poly)
        {
            float best = float.MaxValue;
            int n = poly.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ax = poly[j].x, az = poly[j].y, bx = poly[i].x, bz = poly[i].y;
                float dx = bx - ax, dz = bz - az, lsq = dx*dx + dz*dz;
                float t = (lsq > 0f) ? Mathf.Clamp01(((px-ax)*dx + (pz-az)*dz) / lsq) : 0f;
                float ex = ax + t*dx - px, ez = az + t*dz - pz;
                float d = ex*ex + ez*ez;
                if (d < best) best = d;
            }
            return best;
        }

        private static Vector2 Centroid2D(Vector2[] pts)
        {
            float x = 0f, z = 0f;
            foreach (var p in pts) { x += p.x; z += p.y; }
            return new Vector2(x / pts.Length, z / pts.Length);
        }

    } // GreenVariantDiagnostic

    // ─── Gizmo host ───────────────────────────────────────────────────────────
    // MonoBehaviour attached to DebugGreenVariants. Draws the 3 overlay layers.

    [ExecuteAlways]
    public class GreenDiagGizmoHost : MonoBehaviour
    {
        // Public fields set by GreenVariantDiagnostic
        public Vector2[]   InputContour;
        public Vector2[][] OffsetPolys;
        public Vector3[][] WireVerts;
        public int[][]     WireTris;
        public Vector2[]   Centroids2D;
        public Vector3[]   Offsets;
        public float       OverlayY;

        private void OnDrawGizmos()
        {
            if (InputContour == null || Offsets == null) return;

            for (int v = 0; v < 4; v++)
            {
                if (v >= Offsets.Length) break;
                Vector3 off = Offsets[v];
                Vector2 cent = (Centroids2D != null && v < Centroids2D.Length)
                    ? Centroids2D[v] : Vector2.zero;

                // Yellow — input contour (world XZ mapped to variant offset)
                Gizmos.color = Color.yellow;
                DrawContour(InputContour, cent, off, OverlayY);

                // Cyan — offset polygon
                if (OffsetPolys != null && v < OffsetPolys.Length && OffsetPolys[v] != null)
                {
                    Gizmos.color = Color.cyan;
                    DrawContour(OffsetPolys[v], cent, off, OverlayY);
                }

                // Magenta — wireframe
                if (WireVerts != null && v < WireVerts.Length && WireVerts[v] != null &&
                    WireTris  != null && v < WireTris.Length  && WireTris[v]  != null)
                {
                    Gizmos.color = Color.magenta;
                    var wv = WireVerts[v];
                    var wt = WireTris[v];
                    for (int t = 0; t < wt.Length; t += 3)
                    {
                        Gizmos.DrawLine(WV(wv[wt[t]],   cent, off, OverlayY),
                                        WV(wv[wt[t+1]], cent, off, OverlayY));
                        Gizmos.DrawLine(WV(wv[wt[t+1]], cent, off, OverlayY),
                                        WV(wv[wt[t+2]], cent, off, OverlayY));
                        Gizmos.DrawLine(WV(wv[wt[t+2]], cent, off, OverlayY),
                                        WV(wv[wt[t]],   cent, off, OverlayY));
                    }
                }
            }
        }

        private static void DrawContour(Vector2[] pts, Vector2 cent, Vector3 off, float y)
        {
            if (pts == null || pts.Length < 2) return;
            for (int i = 0; i < pts.Length; i++)
            {
                int j = (i + 1) % pts.Length;
                Vector3 a = new Vector3(pts[i].x - cent.x + off.x, y, pts[i].y - cent.y + off.z);
                Vector3 b = new Vector3(pts[j].x - cent.x + off.x, y, pts[j].y - cent.y + off.z);
                Gizmos.DrawLine(a, b);
            }
        }

        private static Vector3 WV(Vector3 w, Vector2 cent, Vector3 off, float y) =>
            new Vector3(w.x - cent.x + off.x, y, w.z - cent.y + off.z);
    }
}
#endif
