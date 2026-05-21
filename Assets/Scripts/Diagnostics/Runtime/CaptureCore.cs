using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

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

        // ── RT reflection (shared between SnapGameView and SnapAtEndOfFrameAndPause) ──

        public static Texture2D GrabGameViewRT()
        {
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

        // ── SnapGameView ───────────────────────────────────────────────────────

        public static string SnapGameViewWithLabel(string label)
        {
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

            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

#if UNITY_EDITOR
            AssetDatabase.Refresh();
#endif
            Debug.Log($"[CaptureCore] Wrote {path}");
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
                File.WriteAllBytes(path, tex.EncodeToPNG());
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
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(OutDir);
            string path = outputPath ?? $"{OutDir}/{label}_f{Time.frameCount}.png";

            Texture2D tex = GrabGameViewRT();
            if (tex == null)
                tex = ScreenCapture.CaptureScreenshotAsTexture();

            File.WriteAllBytes(path, tex.EncodeToPNG());
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
