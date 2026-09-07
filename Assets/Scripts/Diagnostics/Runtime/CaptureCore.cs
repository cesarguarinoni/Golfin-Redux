using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Golfin.Diagnostics.Runtime
{
    /// <summary>
    /// Synchronous / coroutine-safe screenshot helper. Runtime-side core, consumable from
    /// both editor and gameplay assemblies.
    ///
    /// Factored out of Golfin.EditorTools.CaptureHelper in §2b to close the §2a OPEN FLAG
    /// (CaptureHelper consolidation). Editor-side CaptureHelper.cs becomes a thin wrapper.
    ///
    /// BANNED: ScreenCapture.CaptureScreenshot(path) — async, fails silently when paused.
    /// </summary>
    public static class CaptureCore
    {
        public const string OutDir = "Docs/Diagnostics/_capture";

        // ── Recording lock (K10 ob_recovery_fixes, 2026-08-05) ─────────────────
        //
        // ROOT CAUSE (proven by 1:1 frame-time correlation on ob_recovery_after_hud.mp4):
        // any backbuffer / GameView-RT read DURING an active Unity Recorder GameView
        // recording (ScreenCapture.CaptureScreenshotAsTexture, GameView RT ReadPixels)
        // disturbs the Metal swapchain and the Recorder captures 1–7 vertically FLIPPED
        // frames at that instant. The 2026-06-16 fix locked vSync/targetFrameRate at
        // Begin(); this lock extends the same principle to the ENTIRE recorded window:
        // while BotVideoRecorder is recording, every CaptureCore snap is refused (no-op)
        // so the trigger cannot fire at all. Stills are extracted from the finished mp4
        // instead — identical pixels, zero interference.
        //
        // Set/cleared exclusively by BotVideoRecorder.Begin()/End().
        public static bool RecordingActive;

        static bool BlockDuringRecording(string what, string label)
        {
            if (!RecordingActive) return false;
            Debug.Log($"[CaptureCore] {what} SKIPPED (video recording active): '{label}' — " +
                      "backbuffer reads during recording flip Recorder frames on Metal. " +
                      "Extract this still from the finished video instead.");
            return true;
        }

        // ── RT reflection (shared between SnapGameView and SnapAtEndOfFrameAndPause) ──

        public static Texture2D GrabGameViewRT()
        {
            if (BlockDuringRecording("GrabGameViewRT", "-")) return null;
#if UNITY_EDITOR
            var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null) return null;

            var gv = EditorWindow.GetWindow(gameViewType, false, null, false);
            if (gv == null) return null;

            gv.Focus();
            gv.Repaint();

            string[] candidates = { "m_RenderTexture", "m_TargetTexture", "m_RenderTarget" };
            RenderTexture rt = null;
            foreach (var name in candidates)
            {
                var f = gameViewType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                rt = f?.GetValue(gv) as RenderTexture;
                if (rt != null && rt.IsCreated()) break;
            }

            if (rt == null) return null;

            int w = rt.width, h = rt.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // Flip Y (OpenGL bottom-left origin)
            var pixels  = tex.GetPixels();
            var flipped = new UnityEngine.Color[pixels.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    flipped[y * w + x] = pixels[(h - 1 - y) * w + x];
            tex.SetPixels(flipped);
            tex.Apply();

            return tex;
#else
            return null;
#endif
        }

        // ── Provenance: a frame must not be able to lie about what it is ──────
        //
        // selector_carousel (2026-09-07): an edit-mode capture of LabScaffold, driven by injected
        // FakeState, was surfaced as if it were the running game. It was not obviously wrong — it
        // had real art, real cards, a real halo — but the ball sat at the pre-shot_view_layout
        // centre because ShotLayoutController only runs in play mode, so the whole framing was the
        // OLD layout. Cesar spotted it in seconds; nothing in the pipeline did.
        //
        // Three overlapping defences, cheapest first:
        //   1. FILENAME. A frame that is not real play is written as "NOT-REAL_<label>_...png".
        //      That prefix travels into every path anyone pastes, including chat.
        //   2. PIXELS. Red diagonal hatching is burned into the frame itself, so it cannot be
        //      laundered by copying or renaming the file.
        //   3. SIDECAR. Every capture gets a <file>.json stating play state, fake-state lock,
        //      whether the shot layout had been applied, the ball's viewport Y and the loaded
        //      scenes. `enforce_implementer_done.py` reads it and blocks the review transition on
        //      a canonical screenshot that is not real play.
        //
        // Reflection rather than asmdef references on purpose: Golfin.Gameplay.UI already
        // references this assembly, so naming ShotLayoutController here would be circular, and
        // FakeStateLock lives in Golfin.Gameplay.UI.HUD (NOT EditorTools, despite being a
        // capture-harness concept) and is a public static FIELD, not a property — hence
        // ReadStaticBool checking both.

        public struct CaptureProvenance
        {
            public bool   RealPlay;
            public bool   Playing;
            public bool   FakeStateLocked;
            public bool   ShotLayoutApplied;
            public float  BallViewportY;
            public string Reason;
        }

        public static CaptureProvenance InspectProvenance()
        {
            var p = new CaptureProvenance
            {
                Playing           = Application.isPlaying,
                FakeStateLocked   = ReadStaticBool("Golfin.Gameplay.UI.HUD.FakeStateLock", "IsLocked"),
                ShotLayoutApplied = ReadStaticBool("Golfin.Gameplay.UI.ShotUI.ShotLayoutController", "LayoutApplied"),
                BallViewportY     = ReadStaticFloat("Golfin.Gameplay.UI.ShotUI.ShotLayoutController", "LastAppliedBallY", float.NaN),
            };

            var reasons = new System.Collections.Generic.List<string>();
            if (!p.Playing)          reasons.Add("EDIT MODE - NOT REAL PLAY");
            if (p.FakeStateLocked)   reasons.Add("FAKE STATE INJECTED");
            if (!p.ShotLayoutApplied) reasons.Add("SHOT LAYOUT NOT APPLIED - STALE AUTHORED FRAMING");

            p.RealPlay = reasons.Count == 0;
            p.Reason   = reasons.Count == 0 ? "real play" : string.Join(" | ", reasons);
            return p;
        }

        static System.Type FindType(string fullName)
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type t = null;
                try { t = a.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        static object ReadStaticObject(string typeName, string member)
        {
            var t = FindType(typeName);
            if (t == null) return null;
            var pi = t.GetProperty(member, BindingFlags.Public | BindingFlags.Static);
            if (pi != null) { try { return pi.GetValue(null); } catch { return null; } }
            var fi = t.GetField(member, BindingFlags.Public | BindingFlags.Static);
            if (fi != null) { try { return fi.GetValue(null); } catch { return null; } }
            return null;
        }

        static bool  ReadStaticBool(string t, string m)  { var v = ReadStaticObject(t, m); return v is bool b && b; }
        static float ReadStaticFloat(string t, string m, float dflt) { var v = ReadStaticObject(t, m); return v is float f ? f : dflt; }

        /// <summary>
        /// Rewrites <paramref name="path"/> to carry a NOT-REAL prefix, hatches the texture, and
        /// writes the sidecar. Returns the path the caller should actually write to.
        /// </summary>
        static string ApplyProvenance(Texture2D tex, string path, out CaptureProvenance prov)
        {
            prov = InspectProvenance();
            if (!prov.RealPlay)
            {
                string dir  = Path.GetDirectoryName(path);
                string name = Path.GetFileName(path);
                if (!name.StartsWith("NOT-REAL_")) name = "NOT-REAL_" + name;
                path = string.IsNullOrEmpty(dir) ? name : $"{dir}/{name}";
                HatchTexture(tex);
                Debug.LogWarning($"[CaptureCore] PROVENANCE: {prov.Reason}. Frame hatched and renamed -> {path}");
            }
            return path;
        }

        /// <summary>Burn red diagonal hatching across the frame so it cannot pass as a real one.</summary>
        static void HatchTexture(Texture2D tex)
        {
            if (tex == null) return;
            try
            {
                int w = tex.width, h = tex.height;
                var px = tex.GetPixels32();
                const int period = 96, thickness = 22;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        if (((x + y) % period) >= thickness) continue;
                        int i = y * w + x;
                        var c = px[i];
                        px[i] = new Color32((byte)Mathf.Min(255, c.r / 2 + 190), (byte)(c.g / 3), (byte)(c.b / 3), c.a);
                    }
                }
                tex.SetPixels32(px);
                tex.Apply(false);
            }
            catch (System.Exception e) { Debug.LogWarning($"[CaptureCore] hatching failed: {e.Message}"); }
        }

        static void WriteSidecar(string pngPath, CaptureProvenance p)
        {
            try
            {
                var scenes = new System.Text.StringBuilder();
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (i > 0) scenes.Append(", ");
                    scenes.Append('"').Append(string.IsNullOrEmpty(sc.path) ? sc.name : sc.path).Append('"');
                }
                string json =
                    "{\n" +
                    $"  \"capturedAt\": \"{DateTime.Now:o}\",\n" +
                    $"  \"realPlay\": {(p.RealPlay ? "true" : "false")},\n" +
                    $"  \"playing\": {(p.Playing ? "true" : "false")},\n" +
                    $"  \"fakeStateLocked\": {(p.FakeStateLocked ? "true" : "false")},\n" +
                    $"  \"shotLayoutApplied\": {(p.ShotLayoutApplied ? "true" : "false")},\n" +
                    $"  \"ballViewportY\": {(float.IsNaN(p.BallViewportY) ? "null" : p.BallViewportY.ToString("F4"))},\n" +
                    $"  \"loadedScenes\": [{scenes}],\n" +
                    $"  \"reason\": \"{p.Reason}\"\n" +
                    "}\n";
                File.WriteAllText(pngPath + ".json", json);
            }
            catch (System.Exception e) { Debug.LogWarning($"[CaptureCore] sidecar failed: {e.Message}"); }
        }

        // ── SnapGameView ───────────────────────────────────────────────────────

        public static string SnapGameViewWithLabel(string label)
        {
            if (BlockDuringRecording("SnapGameViewWithLabel", label)) return string.Empty;
            Directory.CreateDirectory(OutDir);
            string path = $"{OutDir}/{label}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

            Texture2D tex = GrabGameViewRT();

            if (tex != null)
            {
                Debug.Log("[CaptureCore] Using RT reflection path (GameView RenderTexture)");
            }
            else
            {
                Debug.LogWarning("[CaptureCore] GameView RT reflection failed — falling back to ScreenCapture.CaptureScreenshotAsTexture(). Result may be black in editor.");
                tex = ScreenCapture.CaptureScreenshotAsTexture();
            }

            path = ApplyProvenance(tex, path, out var prov);
            path = ApplyProvenance(tex, path, out var prov0);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            WriteSidecar(path, prov0);
            WriteSidecar(path, prov);
            UnityEngine.Object.DestroyImmediate(tex);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            Debug.Log($"[CaptureCore] Wrote {path} ({prov.Reason})");
            return Path.GetFullPath(path);
        }

        // ── SnapCamera ─────────────────────────────────────────────────────────

        /// <summary>
        /// Renders <paramref name="cam"/> to a PNG at an exact pixel size, in EDIT MODE or play
        /// mode, and returns the absolute path.
        ///
        /// WHY THIS EXISTS (build_size_diet Phase 1, 2026-09-04).
        ///   Every other entry point here captures the GAME VIEW, which is the right instrument
        ///   for anything a player navigates to. It is the wrong one for an A/B on a texture's
        ///   resolution: that needs the SAME camera transform in two different states of the
        ///   project, on a scene opened in the editor, at a fixed device resolution — and the
        ///   Game View gives none of those. The alternative was a per-task hand-rolled
        ///   camera+RT+ReadPixels, which CLAUDE.md § Screenshots rule 6 bans by name after the
        ///   loop_v1 iter-12 scene corruption; so the case is added here instead.
        ///
        ///   The camera is rendered exactly as configured. Nothing is toggled, no GameObject is
        ///   activated or deactivated, no scene state is touched — the whole failure mode rule 6
        ///   exists to prevent.
        ///
        /// The caller owns <paramref name="cam"/> and must destroy it; that is deliberate, so a
        /// capture rig can position one camera once and take several frames through it.
        /// </summary>
        /// <param name="label">File stem; the timestamp is appended as everywhere else here.</param>
        /// <param name="outputPath">Optional exact path. When given, no timestamp is added — an
        /// A/B pair wants stable, comparable file names, not two timestamps.</param>
        public static string SnapCamera(Camera cam, int width, int height, string label,
                                        string outputPath = null)
        {
            if (cam == null) { Debug.LogError("[CaptureCore] SnapCamera: camera is null."); return string.Empty; }
            if (BlockDuringRecording("SnapCamera", label)) return string.Empty;

            // REFUSE RATHER THAN RETURN FLAT GREY. Under `-batchmode -nographics` there is no
            // graphics device, cam.Render() draws nothing, and ReadPixels hands back a uniform
            // fill — a PNG of the right size, at the right path, with no scene in it. That is the
            // worst possible failure for an A/B rig, because both halves look like data. Observed
            // for real on 2026-09-04: a whole 14-frame capture pass came back flat grey and the
            // pixel diff read 130-165/255 across 100% of pixels before anyone opened one.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogError("[CaptureCore] SnapCamera REFUSED: no graphics device " +
                               "(-nographics). A camera cannot be rendered here; run batchmode " +
                               "WITHOUT -nographics, or capture from an open Editor.");
                return string.Empty;
            }
            if (width <= 0 || height <= 0)
            {
                Debug.LogError($"[CaptureCore] SnapCamera: bad size {width}x{height}.");
                return string.Empty;
            }

            string path = outputPath ?? $"{OutDir}/{label}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                useMipMap = false,
            };
            var prevTarget = cam.targetTexture;
            var prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply(false);
                path = ApplyProvenance(tex, path, out var prov1);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                WriteSidecar(path, prov1);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
            }

            Debug.Log($"[CaptureCore] SnapCamera wrote {path} ({width}x{height})");
            return Path.GetFullPath(path);
        }

        // ── SnapPlayModeSafe ───────────────────────────────────────────────────

        /// <summary>
        /// Synchronous play-mode-safe snapshot. Use from inside a play-mode coroutine
        /// when you need a captured frame and the file path back, but you must NOT
        /// call <c>AssetDatabase.Refresh()</c> (which forces a domain reload and kills
        /// the coroutine) or pause the editor.
        ///
        /// Difference from <see cref="SnapGameViewWithLabel"/>: skips
        /// <c>AssetDatabase.Refresh()</c>. The PNG is on disk; Unity picks it up
        /// automatically on next edit-mode entry.
        ///
        /// Difference from <see cref="SnapAtEndOfFrameAndPause"/>: synchronous (no
        /// end-of-frame yield), returns the absolute path string, and does not
        /// pause the editor regardless of <c>skipPause</c>. Caller is responsible for
        /// any frame-timing yields before invoking.
        /// </summary>
        public static string SnapPlayModeSafe(string label)
        {
            if (BlockDuringRecording("SnapPlayModeSafe", label)) return string.Empty;
            Directory.CreateDirectory(OutDir);
            string path = $"{OutDir}/{label}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png";

            // In play mode, Screen Space Overlay canvases are composited into the final
            // backbuffer AFTER the camera RT is written. GrabGameViewRT() reads only the
            // camera RT and misses UI overlays (modals, toasts, HUD). Use
            // CaptureScreenshotAsTexture() in play mode so overlays are captured.
            // GrabGameViewRT() is the right path only in edit mode (no play-mode compositing).
            Texture2D tex;
            if (Application.isPlaying)
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
                Debug.Log("[CaptureCore] SnapPlayModeSafe: play-mode — using CaptureScreenshotAsTexture (captures UI overlays)");
            }
            else
            {
                tex = GrabGameViewRT();
                if (tex == null)
                    tex = ScreenCapture.CaptureScreenshotAsTexture();
            }

            if (tex != null)
            {
                path = ApplyProvenance(tex, path, out var prov2);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                WriteSidecar(path, prov2);
                // Use Destroy (deferred) in play mode to avoid GC paths that can
                // collect MCP plugin background threads. DestroyImmediate is only safe
                // in edit mode; SnapPlayModeSafe is intended for play-mode callers.
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(tex);
                else
                    UnityEngine.Object.DestroyImmediate(tex);
            }
            else
            {
                Debug.LogWarning($"[CaptureCore] SnapPlayModeSafe: texture was null for label={label}");
            }

            // Intentionally NO AssetDatabase.Refresh() — would force domain reload
            // and exit play mode, killing the calling coroutine.
            Debug.Log($"[CaptureCore] Wrote {path} (play-mode safe — no AssetDatabase.Refresh)");
            return Path.GetFullPath(path);
        }

        // ── SnapAtEndOfFrameAndPause ───────────────────────────────────────────

        /// <summary>
        /// For coroutines: captures at end-of-frame, then pauses the editor.
        /// CRITICAL: capture-then-pause, never pause-then-capture.
        /// </summary>
        /// <param name="skipPause">
        /// When true, captures but does NOT pause the editor. Use this in automated smoke
        /// runners where an external un-pause would be required (pausing causes MCP-thread
        /// finalization issues on macOS Unity 6). Default is false (human-review mode).
        /// </param>
        public static IEnumerator SnapAtEndOfFrameAndPause(string label, string outputPath = null,
            bool skipPause = false)
        {
            if (BlockDuringRecording("SnapAtEndOfFrameAndPause", label)) yield break;
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(OutDir);
            string path = outputPath ?? $"{OutDir}/{label}_f{Time.frameCount}.png";

            Texture2D tex = GrabGameViewRT();
            if (tex == null)
                tex = ScreenCapture.CaptureScreenshotAsTexture();

            path = ApplyProvenance(tex, path, out var prov3);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            WriteSidecar(path, prov3);
            // Use Destroy (deferred) in play mode to avoid triggering GC that collects MCP
            // plugin background threads. DestroyImmediate is only safe/needed in edit mode.
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(tex);
            else
                UnityEngine.Object.DestroyImmediate(tex);

#if UNITY_EDITOR
            if (!skipPause)
            {
                EditorApplication.isPaused = true;
                // Do NOT call AssetDatabase.Refresh() while in play mode — it triggers a domain
                // reload which forces Unity to exit play mode before the coroutine finishes.
                // The PNG is already written to disk; refresh happens automatically on next edit-mode entry.
                if (!EditorApplication.isPlaying)
                    AssetDatabase.Refresh();
            }
#endif
            Debug.Log($"[CaptureCore] Wrote {path}{(skipPause ? "" : " and paused")}");
        }

        // ── SM-gated capture API (§2b OPEN FLAG closure) ──────────────────────

        /// <summary>
        /// Subscribes to the SM's OnStateChanged event, snaps a frame the moment the
        /// target state is entered, then unsubscribes. Uses SnapAtEndOfFrameAndPause
        /// for the actual snap so the capture is at-rest-deterministic.
        ///
        /// Requires a MonoBehaviour to start the coroutine. Typically the caller passes
        /// their own component (e.g. SmokeTestRunner2a).
        /// </summary>
        public static void SnapWhenStateReached(
            MonoBehaviour owner,
            Golfin.Gameplay.Loop.BallStateMachine sm,
            Golfin.Gameplay.Loop.BallState target,
            string label,
            string outputPath = null,
            bool skipPause = false)
        {
            if (sm    == null) throw new ArgumentNullException(nameof(sm));
            if (owner == null) throw new ArgumentNullException(nameof(owner));

            Action<Golfin.Gameplay.Loop.BallStateChange> handler = null;
            handler = (change) =>
            {
                if (change.Next != target) return;
                sm.OnStateChanged -= handler;
                owner.StartCoroutine(SnapAtEndOfFrameAndPause(label, outputPath, skipPause));
            };
            sm.OnStateChanged += handler;
        }

        // ── Director-mode-gated capture API (§controls_g_smoke_followup) ────────

        /// <summary>
        /// Late-bound overload for snapping when a Director mode is reached.
        /// Uses <c>int</c> instead of <c>ChaseCamera.Mode</c> to avoid a circular
        /// asmdef dependency (Golfin.Diagnostics.Runtime → Golfin.Physics.Viewer would
        /// be circular since Viewer already references Diagnostics.Runtime).
        ///
        /// Callers in Golfin.Physics.Viewer cast ChaseCamera.Mode to int and subscribe
        /// to director.OnModeChanged inline:
        /// <code>
        ///   CaptureCore.SnapWhenModeReached(this,
        ///       subscribe: h => director.OnModeChanged += m => h((int)m),
        ///       targetModeAsInt: (int)ChaseCamera.Mode.Downrange,
        ///       label: "capture_label");
        /// </code>
        ///
        /// One-shot: uses a bool flag to fire exactly once and ignore subsequent events.
        /// The subscriber lambda remains registered but is a no-op after first fire
        /// (unsubscription is not possible because the wrapper lambda identity is lost).
        /// </summary>
        public static void SnapWhenModeReached(
            MonoBehaviour owner,
            Action<Action<int>> subscribe,
            int targetModeAsInt,
            string label,
            string outputPath = null,
            bool skipPause = false)
        {
            if (owner     == null) throw new ArgumentNullException(nameof(owner));
            if (subscribe == null) throw new ArgumentNullException(nameof(subscribe));

            bool fired = false;
            subscribe(modeInt =>
            {
                if (fired || modeInt != targetModeAsInt) return;
                fired = true;
                owner.StartCoroutine(SnapAtEndOfFrameAndPause(label, outputPath, skipPause));
            });
        }
    }
}
