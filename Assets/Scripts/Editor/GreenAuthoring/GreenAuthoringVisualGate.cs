// Phase 2 of Docs/Specs/Queued/green_topology_and_pin_authoring/SPEC.md
// Bot-driven visual gate harness for the Green Authoring editor tool.
// Drives the 10-step sequence, records a video, and cleans up.
// Lives in Golfin.Editor.GreenAuthoring (editor-only). NOT reachable from any non-Editor build.
//
// ITER-3 FIXES (carried forward):
// Fix A — Y-flip removed. On macOS Metal, ReadPixels during OnGUI Repaint is already top-down.
// Fix B — Arrow visibility: ArrowScale 0.6, threshold 3px, gate zooms in for arrow frames.
// Fix C — MP4 8 key frames × ~6s = ~48s at 30 fps.
// Fix D — ShellScene: record clean scenes at gate start; reload any dirtied by ImportAsset.
// Fix E — Orientation sanity check: top-strip mean-green [30, 120] range.
//
// ITER-4 FIXES:
// Fix 1 — CaptureEditorWindow helper: extracted capture path to shared helper (CaptureEditorWindow.cs).
//   RequestCapture/IsCaptureReady now delegate to CaptureEditorWindow.Request/IsReady.
//   CaptureEditorWindow.ExecutePendingCapture called at end of GreenTopologyEditor.OnGUI.
// Fix 2 — Step8 fabrication DROPPED. Fabricated dark-gray synthetic PNG removed entirely.
//   ScheduleStep8Close removed. Step8 now just closes the editor and goes to step9.
//   Step9 caption updated: "After Close + Reopen — Hole 01 Loaded".
// Fix 3 — Zoom timing: step1 now waits 3.0s (was 2.5s) for the window to layout at the new
//   position before ZoomToWorldRegion reads position.width/height.
// Fix 4 — Paint arrows distinguishable: SetGateMode(true) before PaintCell calls in step5.
//   Gate-painted cells render in orange (vs. yellow for gradient). DrawArrow accepts isGatePainted.
// Fix 5 — Pin marker size: cross arm length = max(14, cellPx*0.4) ≥ 14px per arm, 3px wide.
// Fix 6 — ShellScene contamination root fix: preserve ShellScene.unity bytes at gate start;
//   restore them at step10 after AssetDatabase.ImportAsset may have triggered TMP rehashing.
//   This ensures git diff Assets/Scenes/ShellScene.unity is empty after the gate.

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Course.Runtime;
using SysProcess = System.Diagnostics.Process;
using SysProcStartInfo = System.Diagnostics.ProcessStartInfo;

namespace Golfin.Editor.GreenAuthoring
{
    /// <summary>
    /// Bot-driven visual gate for the Green Authoring editor tool.
    /// Menu: <c>GOLFIN/Smoke/Green Authoring Visual Gate</c>.
    ///
    /// <para>Drives a 10-step sequence (open, select hole, procedural fill, paint, pin, save,
    /// close, reopen, verify, cleanup) while recording the editor window using the Unity-native
    /// EditorWindow capture path (Texture2D.ReadPixels during OnGUI Repaint).</para>
    ///
    /// <para>Output: <c>Docs/Specs/Active/green_authoring_editor_tool/videos/green_authoring_visual_gate.mp4</c>.</para>
    /// </summary>
    public static class GreenAuthoringVisualGate
    {
        private const string GateCourseSlug  = "lomond-country-club"; // visual gate is hardcoded to hole 01 of this course
        private const string OutputVideoDir  = "Docs/Specs/Active/green_authoring_editor_tool/videos";
        private const string OutputVideoPath = "Docs/Specs/Active/green_authoring_editor_tool/videos/green_authoring_visual_gate.mp4";
        private const string ScreenshotsDir  = "Docs/Specs/Active/green_authoring_editor_tool/screenshots";

        // Minimum file size (bytes) for a non-blank frame check.
        // A 1400×900 all-gray PNG compresses to ~2-4KB (PNG run-length encoding of uniform areas).
        // Real content frames (green cells, polygon, arrows) are 25-50KB.
        // Threshold of 10000 bytes (10KB) reliably separates blank from real captures.
        // Previous variance-based check (BlankVarianceThreshold=8) failed because the sampled
        // region often fell in the uniform sidebar or background, even for valid frames.
        private const int BlankFileSizeThreshold = 10000;

        // Expected toolbar brightness range (macOS dark-theme editor toolbar is dark gray ~50-80).
        // The top strip of a correctly-oriented capture should match this range.
        // An upside-down capture would show the STATUS BAR at the top, which is dark with bright
        // green/yellow text → mean brightness in the green channel would be much higher.
        private const float ToolbarStripMinBrightness = 30f;  // below = fully black (wrong)
        private const float ToolbarStripMaxBrightness = 120f; // above = too bright (upside down = status bar showing)

        private static byte[]             _originalGreenJsonBytes;
        private static string             _greenJsonPath;
        private static string             _originalSha256;
        private static readonly List<string> _frameCaptures = new List<string>();
        // Fix D (iter-3): track which scenes were clean at gate start so we can reload them if dirty at end.
        private static readonly List<string> _cleanScenePathsAtStart = new List<string>();
        // Fix 6 (iter-4): preserve ShellScene.unity bytes before gate so they can be restored if
        // AssetDatabase.ImportAsset triggers TMP m_TextStyleHashCode rehashing.
        private static byte[]             _shellSceneBytes;
        private static string             _shellScenePath;

        [MenuItem("GOLFIN/Smoke/Green Authoring Visual Gate")]
        public static void RunVisualGate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[GreenAuthoringVisualGate] Stop play mode before running the visual gate.");
                return;
            }

            Debug.Log("[GreenAuthoringVisualGate] Starting 10-step visual gate sequence…");

            Directory.CreateDirectory(OutputVideoDir);
            Directory.CreateDirectory(ScreenshotsDir);

            _greenJsonPath = Path.GetFullPath($"Assets/Resources/HoleData/{GateCourseSlug}/Hole_01/green.json");
            if (File.Exists(_greenJsonPath))
            {
                _originalGreenJsonBytes = File.ReadAllBytes(_greenJsonPath);
                _originalSha256 = ComputeSha256(_originalGreenJsonBytes);
                Debug.Log($"[GreenAuthoringVisualGate] Pre-gate Hole_01 green.json SHA-256: {_originalSha256}");
            }
            else
            {
                Debug.LogWarning("[GreenAuthoringVisualGate] Hole_01/green.json not found before gate — cannot verify SHA-256 round-trip.");
                _originalGreenJsonBytes = null;
                _originalSha256 = "(missing)";
            }

            _frameCaptures.Clear();

            // Fix 6: preserve ShellScene.unity bytes so we can restore them if
            // AssetDatabase.ImportAsset(green.json) causes TMP m_TextStyleHashCode rehashing.
            _shellScenePath = Path.GetFullPath("Assets/Scenes/ShellScene.unity");
            if (File.Exists(_shellScenePath))
            {
                _shellSceneBytes = File.ReadAllBytes(_shellScenePath);
                Debug.Log($"[GreenAuthoringVisualGate] Fix 6: Preserved ShellScene.unity ({_shellSceneBytes.Length} bytes) before gate.");
            }
            else
            {
                _shellSceneBytes = null;
                Debug.LogWarning("[GreenAuthoringVisualGate] Fix 6: ShellScene.unity not found — cannot preserve/restore.");
            }


            // Fix D: record which scenes are currently clean so we can restore them at gate end.
            _cleanScenePathsAtStart.Clear();
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var sc = SceneManager.GetSceneAt(si);
                if (!sc.isDirty && !string.IsNullOrEmpty(sc.path))
                    _cleanScenePathsAtStart.Add(sc.path);
            }
            Debug.Log($"[GreenAuthoringVisualGate] Fix D: {_cleanScenePathsAtStart.Count} clean scene(s) recorded at gate start.");

            Debug.Log("[GreenAuthoringVisualGate] Recording initialised (iter-4: CaptureEditorWindow helper, no fabricated frames).");

            double nextDelay = EditorApplication.timeSinceStartup + 1.5;
            ScheduleStep1(nextDelay);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Step sequencer
        // ──────────────────────────────────────────────────────────────────────

        private static GreenTopologyEditor _editor;

        private static void ScheduleStep1(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 1: Opening GreenTopologyEditor…");
                _editor = GreenTopologyEditor.Open();
                // Set a large window so toolbar, sidebars, and centre panel are all visible.
                // 1400 × 900 gives centre panel ≈ 1000 × 852px — arrows will be visible even at
                // whole-polygon zoom (~28px/m for a 22m-wide green in a 600px centre panel).
                _editor.SetWindowRect(new Rect(100, 50, 1400, 900));
                _editor.Focus();
                // iter-4 Fix 3: give the window 3 full frames (≥3.0s total) to layout at the new
                // position before ZoomToWorldRegion reads position.width/height.  If we zoom
                // immediately, position still reports the previous/default window size and the
                // zoom/pan calculation lands the polygon off-screen.
                ScheduleStep2(EditorApplication.timeSinceStartup + 3.0);
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep2(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 2: Selecting Hole 1…");
                _editor.SelectHole(1);
                _editor.Repaint();

                ScheduleStep3(EditorApplication.timeSinceStartup + 2.5);
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep3(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 3: Verifying polygon loaded…");
                if (_editor.GreenPolygon == null || _editor.GreenPolygon.Count == 0)
                    Debug.LogWarning("[GreenAuthoringVisualGate] Step 3: green polygon vertex count = 0; zones.json may be missing for Hole 1. Continuing anyway.");
                else
                    Debug.Log($"[GreenAuthoringVisualGate] Step 3 PASS: polygon has {_editor.GreenPolygon.Count} vertices.");

                // Zoom to show the polygon (it may be far from the default view origin).
                // ZoomToWorldRegion only changes _zoom and _pan — does NOT touch the slope grid.
                if (_editor.GreenPolygon != null && _editor.GreenPolygon.Count > 0)
                {
                    _editor.ZoomToWorldRegion(_editor.PolygonCentroid, 14f);  // 28m half-extent shows whole polygon
                    _editor.Repaint();
                }

                _editor.Repaint();

                // Frame: post-load (shows toolbar + polygon outline + empty grid at correct zoom)
                string capPath = BuildCapturePath("step3_polygon");
                ScheduleCapture(_editor, capPath, "step3_polygon",
                    afterCapture: () => ScheduleStep4(EditorApplication.timeSinceStartup + 2.0));
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep4(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                // Reset bounds from polygon AABB before fill.
                Debug.Log("[GreenAuthoringVisualGate] Step 4: Resetting bounds from polygon AABB…");
                bool boundsReset = _editor.ResetBoundsFromPolygon();
                if (!boundsReset)
                    Debug.LogWarning("[GreenAuthoringVisualGate] Step 4: ResetBoundsFromPolygon returned false — no polygon loaded.");
                else
                    Debug.Log($"[GreenAuthoringVisualGate] Step 4: Bounds reset. min={_editor.BoundsMin}, max={_editor.BoundsMax}, cells={_editor.SlopeGrid?.Length / 3}.");

                _editor.ClearPins();
                Debug.Log("[GreenAuthoringVisualGate] Step 4: Cleared skeleton pins.");

                Debug.Log("[GreenAuthoringVisualGate] Step 4: Running Procedural Fill (synthetic gradient)…");
                _editor.RunProceduralFillWithSampler(v => 0.002f * v.x + 0.0015f * v.y + 50f);
                _editor.Repaint();

                int nonZero = _editor.NonZeroCellCount();
                Debug.Log($"[GreenAuthoringVisualGate] Step 4: non-zero cells after fill = {nonZero}.");
                if (nonZero < 10)
                    Debug.LogError($"[GreenAuthoringVisualGate] Step 4 FAIL: fewer than 10 non-zero cells ({nonZero}).");
                else
                    Debug.Log($"[GreenAuthoringVisualGate] Step 4 PASS: {nonZero} non-zero cells.");

                // Frame A: post-fill at whole-polygon zoom (shows polygon outline + fill coverage)
                string capPathA = BuildCapturePath("step4_post_fill");
                ScheduleCapture(_editor, capPathA, "step4_post_fill",
                    afterCapture: () =>
                    {
                        // After whole-polygon capture, zoom into the centre 6m×6m sub-region
                        // so slope arrows are clearly visible (Fix B).
                        Vector2 c = _editor.PolygonCentroid;
                        _editor.ZoomToWorldRegion(c, 3f);  // 6×6m, arrows ~18px at 0.6 scale
                        _editor.Repaint();
                        string capPathB = BuildCapturePath("step4_arrows_zoom");
                        ScheduleCapture(_editor, capPathB, "step4_arrows_zoom",
                            afterCapture: () => ScheduleStep5(EditorApplication.timeSinceStartup + 1.5));
                    });
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep5(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                // Paint cells near the centroid (visible in the zoomed-in view) with a distinctive
                // 4% magnitude +X direction stroke. We compute centroid-relative cell indices so the
                // painted arrows appear in the zoomed-in view (centred on polygon centroid).
                Debug.Log("[GreenAuthoringVisualGate] Step 5: Painting 3 cells near centroid in Paint mode…");
                Vector2 centroid5 = _editor.PolygonCentroid;
                Vector2 bMin5     = _editor.BoundsMin;
                const float cellSz = 0.5f; // DefaultCellSize
                int cCol = Mathf.Max(0, Mathf.FloorToInt((centroid5.x - bMin5.x) / cellSz));
                int cRow = Mathf.Max(0, Mathf.FloorToInt((centroid5.y - bMin5.y) / cellSz));

                // iter-4 Fix 4: enable gate mode so PaintCell tracks these cells in
                // _gatePaintedCells → they render in orange (vs. yellow for gradient cells).
                _editor.SetGateMode(true);
                // Paint a 3-cell horizontal strip centred on the centroid cell.
                _editor.PaintCell(cCol,     cRow, 1f, 0f, 4.0f);
                _editor.PaintCell(cCol + 1, cRow, 1f, 0f, 4.0f);
                _editor.PaintCell(cCol + 2, cRow, 1f, 0f, 4.0f);
                _editor.SetGateMode(false);  // disable after painting

                _editor.Repaint();
                Debug.Log($"[GreenAuthoringVisualGate] Step 5: Painted cells ({cCol},{cRow}), ({cCol+1},{cRow}), ({cCol+2},{cRow}) with dir=(1,0), mag=4% [orange arrows — gate mode].");

                // Frame: post-paint, still at zoomed-in view so painted arrows are clearly visible.
                string capPath = BuildCapturePath("step5_post_paint");
                ScheduleCapture(_editor, capPath, "step5_post_paint",
                    afterCapture: () =>
                    {
                        // Return to whole-polygon zoom for pin / save / reopen frames.
                        // IMPORTANT: use ZoomToWorldRegion (NOT ResetBoundsFromPolygon) so we
                        // don't reinitialize the slope grid to zeros — we want to KEEP the
                        // painted cells and procedural fill data for the pin/save/reopen steps.
                        Vector2 centroid = _editor.PolygonCentroid;
                        Vector2 bMin = _editor.BoundsMin;
                        Vector2 bMax = _editor.BoundsMax;
                        float halfExtent = Mathf.Max((bMax.x - bMin.x), (bMax.y - bMin.y)) * 0.55f;
                        _editor.ZoomToWorldRegion(centroid, halfExtent);
                        _editor.Repaint();
                        ScheduleStep6(EditorApplication.timeSinceStartup + 2.0);
                    });
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep6(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 6: Adding pin at polygon centroid…");
                Vector2 centroid2D = _editor.PolygonCentroid;
                Vector3 centroid = new Vector3(centroid2D.x, 0f, centroid2D.y);
                _editor.AddPin(centroid, "visual-gate-test");
                _editor.Repaint();
                Debug.Log($"[GreenAuthoringVisualGate] Step 6: Added pin 'visual-gate-test' at ({centroid.x:F2}, {centroid.z:F2}).");

                // Frame: post-pin (shows yellow cross pin marker at centroid).
                string capPath = BuildCapturePath("step6_post_pin");
                ScheduleCapture(_editor, capPath, "step6_post_pin",
                    afterCapture: () => ScheduleStep7(EditorApplication.timeSinceStartup + 2.0));
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep7(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 7: Saving…");
                bool saved = _editor.SaveCurrentState();
                _editor.Repaint();

                if (!saved)
                {
                    Debug.LogError("[GreenAuthoringVisualGate] Step 7 FAIL: SaveCurrentState() returned false. See console.");
                }
                else
                {
                    Debug.Log("[GreenAuthoringVisualGate] Step 7 PASS: Saved Hole_01 green.json.");
                    GreenTopologyCache.Invalidate(1);
                    var topo = GreenTopology.LoadFromResources(1);
                    if (topo == null)
                        Debug.LogError("[GreenAuthoringVisualGate] Step 7: GreenTopology.LoadFromResources(1) returned null after save — round-trip FAILED.");
                    else
                        Debug.Log($"[GreenAuthoringVisualGate] Step 7: Round-trip PASS — grid {topo.GridWidth}x{topo.GridHeight}, pins={topo.GetPinCandidates().Count}, sourceTag='{topo.SourceTag}'.");
                }

                // Frame: post-save (shows "Saved Hole_01 green.json" in status bar at BOTTOM of window).
                string capPath = BuildCapturePath("step7_post_save");
                ScheduleCapture(_editor, capPath, "step7_post_save",
                    afterCapture: () => ScheduleStep8(EditorApplication.timeSinceStartup + 2.0));
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep8(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                // iter-4 Fix 2: step8 (fabricated dark-gray PNG) is DROPPED entirely.
                // Cesar's decision: "drop step8 from the slideshow entirely and have step9
                // visually demonstrate 'closed then reopened' via a single annotated capture."
                // The fabricated Texture2D block that existed in iter-3 here has been removed.
                // Step9's caption will read "After Close + Reopen — Hole 01 loaded" to make
                // the close+reopen transition clear without needing a separate frame.
                Debug.Log("[GreenAuthoringVisualGate] Step 8: Closing editor window (no fabricated frame)…");
                if (_editor != null)
                {
                    _editor.Close();
                    _editor = null;
                }

                // Go directly to step9 after a short delay for Unity to process the close event.
                ScheduleStep9(EditorApplication.timeSinceStartup + 1.5);
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep9(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 9: Reopening editor and verifying persistence…");
                GreenTopologyCache.Invalidate(1);
                _editor = GreenTopologyEditor.Open();
                _editor.SetWindowRect(new Rect(100, 50, 1400, 900));
                _editor.SelectHole(1);
                _editor.Focus();
                _editor.Repaint();

                int nonZero = _editor.NonZeroCellCount();
                int pinCount = _editor.PinCount;
                Debug.Log($"[GreenAuthoringVisualGate] Step 9: Reopened. Non-zero cells = {nonZero}. Pins = {pinCount}.");

                if (nonZero < 10)
                    Debug.LogError($"[GreenAuthoringVisualGate] Step 9 FAIL: fewer than 10 non-zero cells after reopen ({nonZero}) — authored data may not have persisted.");
                else
                    Debug.Log($"[GreenAuthoringVisualGate] Step 9 PASS: {nonZero} non-zero cells survived close+reopen.");

                if (pinCount == 0)
                    Debug.LogWarning("[GreenAuthoringVisualGate] Step 9 WARNING: no pins after reopen — pin data may not have persisted.");
                else
                    Debug.Log($"[GreenAuthoringVisualGate] Step 9 PASS: {pinCount} pin(s) survived close+reopen.");

                // iter-4 Fix 2: step9 caption explains the full close+reopen cycle since step8
                // was dropped. Caption reads "After Close + Reopen — Hole 01 loaded".
                string capPath = BuildCapturePath("step9_post_reopen");
                ScheduleCapture(_editor, capPath, "step9_post_reopen",
                    afterCapture: () => ScheduleStep10(EditorApplication.timeSinceStartup + 1.0));
            };
            EditorApplication.update += step;
        }

        private static void ScheduleStep10(double afterTime)
        {
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (EditorApplication.timeSinceStartup < afterTime) return;
                EditorApplication.update -= step;

                Debug.Log("[GreenAuthoringVisualGate] Step 10: Cleaning up — restoring original Hole_01/green.json…");

                if (_originalGreenJsonBytes != null && _greenJsonPath != null)
                {
                    File.WriteAllBytes(_greenJsonPath, _originalGreenJsonBytes);
                    AssetDatabase.ImportAsset($"Assets/Resources/HoleData/{GateCourseSlug}/Hole_01/green.json");
                    GreenTopologyCache.Invalidate(1);

                    string restoredSha256 = ComputeSha256(File.ReadAllBytes(_greenJsonPath));
                    bool roundTripOk = restoredSha256 == _originalSha256;
                    Debug.Log($"[GreenAuthoringVisualGate] Step 10: SHA-256 original={_originalSha256}");
                    Debug.Log($"[GreenAuthoringVisualGate] Step 10: SHA-256 restored={restoredSha256}");
                    Debug.Log($"[GreenAuthoringVisualGate] Step 10: SHA-256 round-trip {(roundTripOk ? "PASS" : "FAIL")}");

                    if (!roundTripOk)
                        Debug.LogError("[GreenAuthoringVisualGate] SHA-256 mismatch after restore — file was not cleanly restored!");
                }
                else
                    Debug.LogWarning("[GreenAuthoringVisualGate] Step 10: No original bytes saved — cannot verify SHA-256 round-trip.");

                if (_editor != null)
                {
                    _editor.Close();
                    _editor = null;
                }

                // Fix D (iter-3): reload any scenes that were clean at gate start but became dirty.
                ReloadDirtiedCleanScenes();

                // Fix 6 (iter-4): restore ShellScene.unity bytes to prevent the
                // m_TextStyleHashCode TMP rehashing from persisting in the working tree.
                // Root cause: AssetDatabase.ImportAsset(green.json) triggers a partial
                // domain reload that causes Unity to recompute TMP style hashes for all
                // open scenes, dirtying ShellScene.unity with a new PrefabInstance override.
                // We saved the original bytes at gate start; write them back now.
                RestoreShellScene();

                StitchVideoAndFinish();
            };
            EditorApplication.update += step;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Fix D: reload scenes that were dirtied as a side-effect
        // ──────────────────────────────────────────────────────────────────────

        private static void ReloadDirtiedCleanScenes()
        {
            if (_cleanScenePathsAtStart.Count == 0) return;

            var scenesToReload = new List<Scene>();
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                var sc = SceneManager.GetSceneAt(si);
                if (sc.isDirty && _cleanScenePathsAtStart.Contains(sc.path))
                {
                    scenesToReload.Add(sc);
                    Debug.Log($"[GreenAuthoringVisualGate] Fix D: scene '{sc.name}' was clean at gate start, became dirty — will reload to discard side-effect changes.");
                }
            }

            foreach (var sc in scenesToReload)
            {
                try
                {
                    // Unity 6 has no EditorSceneManager.ReloadScene.
                    // Discard in-memory dirty state by closing and re-opening additively.
                    string scenePath = sc.path;
                    string sceneName = sc.name;
                    EditorSceneManager.CloseScene(sc, removeScene: true);
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    Debug.Log($"[GreenAuthoringVisualGate] Fix D: Reloaded scene '{sceneName}' — TMP style hash side-effect discarded.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[GreenAuthoringVisualGate] Fix D: Could not reload scene '{ex.Message}'");
                }
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Fix 6: restore ShellScene.unity to prevent TMP rehashing contamination
        // ──────────────────────────────────────────────────────────────────────

        private static void RestoreShellScene()
        {
            if (_shellSceneBytes == null || string.IsNullOrEmpty(_shellScenePath))
            {
                Debug.Log("[GreenAuthoringVisualGate] Fix 6: No ShellScene backup available — skipping restore.");
                return;
            }

            try
            {
                byte[] current = File.ReadAllBytes(_shellScenePath);
                if (current.Length == _shellSceneBytes.Length)
                {
                    bool identical = true;
                    for (int bi = 0; bi < current.Length; bi++)
                    {
                        if (current[bi] != _shellSceneBytes[bi]) { identical = false; break; }
                    }
                    if (identical)
                    {
                        Debug.Log("[GreenAuthoringVisualGate] Fix 6: ShellScene.unity unchanged — no restore needed.");
                        return;
                    }
                }

                // Bytes differ: the gate's AssetDatabase.ImportAsset triggered TMP rehashing.
                // Restore the original bytes.
                File.WriteAllBytes(_shellScenePath, _shellSceneBytes);
                Debug.Log($"[GreenAuthoringVisualGate] Fix 6: ShellScene.unity restored to pre-gate bytes ({_shellSceneBytes.Length} bytes) — TMP rehashing side-effect discarded.");

                // Notify Unity the asset file has changed on disk.
                // Use the project-relative path for AssetDatabase.
                AssetDatabase.ImportAsset("Assets/Scenes/ShellScene.unity",
                    ImportAssetOptions.ForceUpdate);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GreenAuthoringVisualGate] Fix 6: Could not restore ShellScene.unity: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Unity-native EditorWindow capture (iter-4: delegates to CaptureEditorWindow)
        // ──────────────────────────────────────────────────────────────────────

        // iter-4 Fix 1: ScheduleCapture now uses RequestCapture/IsCaptureReady which internally
        // delegate to CaptureEditorWindow helper (shared capture path, not bespoke to gate).
        private static void ScheduleCapture(
            GreenTopologyEditor editor,
            string capturePath,
            string label,
            Action afterCapture)
        {
            editor.Focus();
            editor.RequestCapture(capturePath);  // → CaptureEditorWindow.Request(editor, capturePath)

            double startTime = EditorApplication.timeSinceStartup;
            const double TimeoutSec = 10.0;

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                if (EditorApplication.timeSinceStartup - startTime > TimeoutSec)
                {
                    EditorApplication.update -= poll;
                    Debug.LogError($"[GreenAuthoringVisualGate] Capture timed out after {TimeoutSec}s for label='{label}'.");
                    afterCapture?.Invoke();
                    return;
                }

                if (editor == null || !editor.IsCaptureReady()) return;  // → CaptureEditorWindow.IsReady
                EditorApplication.update -= poll;

                if (File.Exists(capturePath))
                {
                    bool valid = ValidateFrameNonBlank(capturePath, label);
                    if (valid)
                    {
                        _frameCaptures.Add(capturePath);
                        Debug.Log($"[GreenAuthoringVisualGate] Frame '{label}' captured and validated → {capturePath}");
                    }
                    else
                    {
                        Debug.LogError($"[GreenAuthoringVisualGate] Frame '{label}' FAILED blank/orientation check — not added to video.");
                    }
                }
                else
                {
                    Debug.LogError($"[GreenAuthoringVisualGate] Capture file not found after IsCaptureReady for label='{label}': {capturePath}");
                }

                afterCapture?.Invoke();
            };
            EditorApplication.update += poll;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Non-blank + orientation check (Fix E)
        // ──────────────────────────────────────────────────────────────────────

        private static bool ValidateFrameNonBlank(string path, string label)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
                if (!tex.LoadImage(bytes))
                {
                    Debug.LogError($"[GreenAuthoringVisualGate] ValidateFrameNonBlank: failed to decode PNG at {path}");
                    UnityEngine.Object.DestroyImmediate(tex);
                    return false;
                }

                int w = tex.width;
                int h = tex.height;

                // ── Non-blank check (file-size based) ───────────────────────
                // A 1400×900 solid-color PNG (all-gray blank) compresses to ~2-4KB due to
                // PNG run-length encoding of uniform rows. Real content frames (green cells,
                // polygon outline, arrows, text) compress to 25-50KB.
                // File-size check is layout-independent: no need to know which pixel region
                // contains "interesting" content. Previous variance-based check failed because
                // the sampled region (center or upper-right crop) often landed in the uniform
                // dark-gray sidebar, producing variance≈0 even for valid content frames.
                bool nonBlankOk = bytes.Length >= BlankFileSizeThreshold;
                Debug.Log($"[GreenAuthoringVisualGate] Frame '{label}' blank-check: fileSize={bytes.Length} bytes (threshold={BlankFileSizeThreshold}) → {(nonBlankOk ? "PASS" : "FAIL")}");

                if (!nonBlankOk)
                {
                    Debug.LogError($"[GreenAuthoringVisualGate] Frame '{label}' BLANK CHECK FAIL: fileSize={bytes.Length} < {BlankFileSizeThreshold} bytes. " +
                                   "EditorWindow may not have rendered into framebuffer before ReadPixels.");
                    UnityEngine.Object.DestroyImmediate(tex);
                    return false;
                }

                // ── Orientation sanity check (Fix E) ────────────────────────
                // In a correctly-oriented capture (no Y-flip), the top 26 rows are the
                // Unity Editor toolbar — dark gray background (RGB ~50-80 in macOS dark theme).
                // In an upside-down capture (iter-2 bug), the STATUS BAR appears at the top
                // (bright green text on dark background — the green channel is significantly
                // higher than expected toolbar gray).
                //
                // Check: sample a 200×14 strip from the top of the image (rows 0..13).
                // Compute mean GREEN channel value.
                //   - Correct orientation (toolbar): mean green ≈ 50-100 (Unity dark gray toolbar)
                //   - Upside-down (status bar at top): mean green > 120 (status text is lime green)
                //
                // Note: this check may fire false positives if the toolbar is very dark or the
                // status bar is empty. The check is advisory — a WARNING, not a hard FAIL —
                // because the non-blank variance check already caught the iter-1 desktop bug.
                int toolbarSampleW = Mathf.Min(200, w);
                int toolbarSampleH = Mathf.Min(14,  h);
                int toolbarX0 = Mathf.Max(0, w / 2 - toolbarSampleW / 2);
                Color[] topStrip = tex.GetPixels(toolbarX0, h - toolbarSampleH, toolbarSampleW, toolbarSampleH);
                // Note: Texture2D pixel y=0 is BOTTOM of image after LoadImage (PNG flips to GL convention).
                // So "top of PNG" = y = h-1, not y=0. We sample y=(h-toolbarSampleH)..h which is the
                // top of the PNG (first rows of the toolbar in a correctly-oriented image).

                float greenSum = 0f;
                foreach (Color c in topStrip) greenSum += c.g * 255f;
                float meanGreen = greenSum / topStrip.Length;

                bool orientationOk = meanGreen >= ToolbarStripMinBrightness && meanGreen <= ToolbarStripMaxBrightness;
                Debug.Log($"[GreenAuthoringVisualGate] Frame '{label}' orientation-check: top-strip mean-green={meanGreen:F1} " +
                          $"(expected {ToolbarStripMinBrightness}-{ToolbarStripMaxBrightness}) → {(orientationOk ? "PASS" : "WARN")}");

                if (!orientationOk)
                {
                    Debug.LogWarning($"[GreenAuthoringVisualGate] Frame '{label}' ORIENTATION WARN: top-strip mean-green={meanGreen:F1} " +
                                     $"outside [{ToolbarStripMinBrightness},{ToolbarStripMaxBrightness}]. " +
                                     "Image may be upside-down or toolbar is non-standard. " +
                                     "Check GreenTopologyEditor.ExecutePendingCapture Y-flip logic.");
                    // Treat orientation mismatch as a FAIL to prevent silent upside-down captures.
                    UnityEngine.Object.DestroyImmediate(tex);
                    return false;
                }

                UnityEngine.Object.DestroyImmediate(tex);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenAuthoringVisualGate] ValidateFrameNonBlank exception for {label}: {ex.Message}");
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // MP4 stitching (Fix C — 7 frames × ~6s each = ~42s at 30fps)
        // ──────────────────────────────────────────────────────────────────────

        private static void StitchVideoAndFinish()
        {
            if (_frameCaptures.Count == 0)
            {
                Debug.LogError("[GreenAuthoringVisualGate] StitchVideoAndFinish: no validated frame captures — cannot produce video.");
                LogGateSummary();
                return;
            }

            Debug.Log($"[GreenAuthoringVisualGate] Stitching video from {_frameCaptures.Count} validated frames…");

            try
            {
                string ffmpegPath = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                    ".local/bin/ffmpeg");
                if (!File.Exists(ffmpegPath)) ffmpegPath = "ffmpeg";

                // Each frame held for (totalDuration / frameCount) seconds.
                // Target: 40-60s. With 7+ frames at 6s each = 42s minimum.
                // We use ~6s per frame so total duration hits 40-60s range.
                // With fewer frames (e.g. 7), duration = 7×6 = 42s.
                // With more frames (e.g. 9), duration = 9×6 = 54s.
                double frameDuration = 6.0;
                double totalDuration = _frameCaptures.Count * frameDuration;
                // Clamp to 40-60s target: if too short, extend durations; if too long, shorten.
                if (totalDuration < 40.0 && _frameCaptures.Count > 0)
                    frameDuration = 40.0 / _frameCaptures.Count;
                else if (totalDuration > 60.0)
                    frameDuration = 60.0 / _frameCaptures.Count;

                // Write concat list.
                string concatList = Path.GetTempFileName() + ".txt";
                var sb = new StringBuilder();
                foreach (string frame in _frameCaptures)
                    sb.AppendLine($"file '{frame.Replace("'", "\\'")}'\nduration {frameDuration:F3}");
                File.WriteAllText(concatList, sb.ToString());

                string absOut = Path.GetFullPath(OutputVideoPath);
                Directory.CreateDirectory(Path.GetDirectoryName(absOut));

                // Build per-frame caption filter.
                var captionSb = new StringBuilder();
                for (int i = 0; i < _frameCaptures.Count; i++)
                {
                    string rawLabel = Path.GetFileNameWithoutExtension(_frameCaptures[i]);
                    // Strip timestamp suffix (format: label_YYYY-MM-DD_HH-mm-ss).
                    // Find last segment that looks like a date.
                    int tsIdx = rawLabel.LastIndexOf('_');
                    if (tsIdx > 0)
                    {
                        string suffix = rawLabel.Substring(tsIdx + 1);
                        if (suffix.Length >= 4 && char.IsDigit(suffix[0]))
                        {
                            // Also strip HH-mm-ss
                            string remaining = rawLabel.Substring(0, tsIdx);
                            int tsIdx2 = remaining.LastIndexOf('_');
                            if (tsIdx2 > 0)
                            {
                                string suffix2 = remaining.Substring(tsIdx2 + 1);
                                if (suffix2.Length >= 4 && char.IsDigit(suffix2[0]))
                                    remaining = remaining.Substring(0, tsIdx2);
                            }
                            rawLabel = remaining;
                        }
                    }
                    string caption = rawLabel.Replace("_", " ");
                    double tStart = i * frameDuration;
                    double tEnd   = (i + 1) * frameDuration;

                    // Step-label map for readable captions.
                    string readableCaption = MapLabelToCaption(rawLabel);
                    string safeCaption = readableCaption.Replace("'", "\\'").Replace(":", "\\:");

                    if (captionSb.Length > 0) captionSb.Append(",");
                    captionSb.Append(
                        $"drawtext=text='{safeCaption}'" +
                        $":enable='between(t,{tStart:F3},{tEnd:F3})'" +
                        $":fontsize=32:fontcolor=white:box=1:boxcolor=0x000000BB:boxborderw=8:x=20:y=20");
                }

                string vf = $"scale=1280:-2{(captionSb.Length > 0 ? "," + captionSb : "")}";

                string ffmpegArgs = $"-y -f concat -safe 0 -i \"{concatList}\" " +
                                    $"-vf \"{vf}\" " +
                                    $"-vcodec libx264 -preset medium -crf 23 -pix_fmt yuv420p " +
                                    $"-r 30 " +
                                    $"\"{absOut}\"";

                var psi = new SysProcStartInfo(ffmpegPath, ffmpegArgs)
                {
                    UseShellExecute  = false,
                    CreateNoWindow   = true,
                    RedirectStandardError = true,
                };
                var proc = SysProcess.Start(psi);
                string ffmpegStderr = proc?.StandardError.ReadToEnd() ?? "";
                bool exited = proc != null && proc.WaitForExit(120000);

                if (exited && File.Exists(absOut))
                {
                    var fileInfo = new FileInfo(absOut);
                    Debug.Log($"[GreenAuthoringVisualGate] Video stitched from {_frameCaptures.Count} frames " +
                              $"({frameDuration:F1}s/frame = {_frameCaptures.Count * frameDuration:F0}s total) → {absOut} ({fileInfo.Length / 1024}KB)");
                    Debug.Log("[GreenAuthoringVisualGate] Expected ffprobe: r_frame_rate=30/1, " +
                              $"duration≈{_frameCaptures.Count * frameDuration:F0}s");
                }
                else
                {
                    proc?.Kill();
                    Debug.LogError($"[GreenAuthoringVisualGate] ffmpeg concat failed. stderr: {ffmpegStderr}");
                }

                try { File.Delete(concatList); } catch { }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GreenAuthoringVisualGate] StitchVideoAndFinish: {ex.Message}");
            }

            LogGateSummary();
        }

        private static string MapLabelToCaption(string label)
        {
            // Map internal step labels to human-readable captions for the video.
            if (label.Contains("step3")) return "Step 3: Green Polygon Loaded";
            if (label.Contains("step4") && label.Contains("arrows")) return "Step 4: Slope Arrows (Zoomed In)";
            if (label.Contains("step4")) return "Step 4: Procedural Fill";
            // iter-4 Fix 4: step5 caption notes the orange arrows are the painted stroke.
            if (label.Contains("step5")) return "Step 5: Paint Stroke — Orange Arrows (+X dir)";
            if (label.Contains("step6")) return "Step 6: Pin Added";
            if (label.Contains("step7")) return "Step 7: Saved";
            // iter-4 Fix 2: step8 is dropped; step9 caption explains close+reopen cycle.
            if (label.Contains("step9")) return "Step 9: After Close + Reopen — Hole 01 Loaded";
            return label.Replace("_", " ");
        }

        private static void LogGateSummary()
        {
            Debug.Log($"[GreenAuthoringVisualGate] Visual gate complete. Video → {OutputVideoPath}");
            Debug.Log("[GreenAuthoringVisualGate] ============================================================");
            Debug.Log("[GreenAuthoringVisualGate] GATE SUMMARY: Check console for PASS/FAIL markers above.");
            Debug.Log("[GreenAuthoringVisualGate]   Step 3: polygon vertex count assertion");
            Debug.Log("[GreenAuthoringVisualGate]   Step 4: procedural fill cell count (synthetic gradient)");
            Debug.Log("[GreenAuthoringVisualGate]   Step 5: 3 orange paint-stroke cells (distinguishable from yellow gradient)");
            Debug.Log("[GreenAuthoringVisualGate]   Step 6: pin marker at polygon centroid (≥20px cross)");
            Debug.Log("[GreenAuthoringVisualGate]   Step 7: save + round-trip assertion");
            Debug.Log("[GreenAuthoringVisualGate]   Step 8: editor closed (no fabricated frame — step8 DROPPED, iter-4 Fix 2)");
            Debug.Log("[GreenAuthoringVisualGate]   Step 9: persistence after close+reopen ('After Close + Reopen' caption)");
            Debug.Log("[GreenAuthoringVisualGate]   Step 10: SHA-256 round-trip assertion + ShellScene.unity restore");
            Debug.Log("[GreenAuthoringVisualGate]   Capture: non-blank + orientation check on each frame (CaptureEditorWindow helper)");
            Debug.Log($"[GreenAuthoringVisualGate]   Validated frames: {_frameCaptures.Count}");
            Debug.Log("[GreenAuthoringVisualGate] ============================================================");
        }

        // ──────────────────────────────────────────────────────────────────────
        // Path helpers
        // ──────────────────────────────────────────────────────────────────────

        private static string BuildCapturePath(string label)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            return Path.GetFullPath(
                Path.Combine(ScreenshotsDir, $"{label}_{timestamp}.png"));
        }

        // ──────────────────────────────────────────────────────────────────────
        // SHA-256 helper
        // ──────────────────────────────────────────────────────────────────────

        private static string ComputeSha256(byte[] data)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                var sb = new StringBuilder(64);
                foreach (byte b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }
    }
}
