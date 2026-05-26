// iter-4 Fix 1: Shared EditorWindow capture helper.
// Extracted from GreenTopologyEditor.ExecutePendingCapture so the capture path is not
// bespoke to GreenTopologyEditor.  Lives in Golfin.Editor.GreenAuthoring (editor-only asmdef).
//
// Usage (async, poll-based):
//   CaptureEditorWindow.Request(myWin, outputPath);    // call during or after Repaint
//   // wait for CaptureEditorWindow.IsReady(myWin) == true
//   // file is now at outputPath
//
// The capture reads the EditorWindow's IMGUI framebuffer via Texture2D.ReadPixels during the
// next Repaint event (EventType.Repaint).  This is the Unity-native, OS-agnostic path for
// EditorWindow capture — no screencapture, AVFoundation, or other OS tools used.
//
// Capture rect: the FULL window content area (physical pixels = logical × pixelsPerPoint), which
// includes:
//   - Top toolbar band (drawn in OnGUI at y=0 via GUILayout/EditorStyles.toolbar)
//   - Left sidebar (x=0 to SidebarWidth × ppp)
//   - Right sidebar (x = (w-SidebarWidth) × ppp to w × ppp)
//   - Centre green-view panel
//   - Bottom status bar
// The ReadPixels call uses Rect(0, 0, w×ppp, h×ppp) where w/h come from EditorWindow.position
// and ppp = EditorGUIUtility.pixelsPerPoint (1.0 on standard displays, 2.0 on macOS Retina).
// Without the ppp multiplier, ReadPixels on Retina only captures the bottom-left quadrant of
// the physical framebuffer (the left half-width × bottom half-height in logical pixels),
// cutting off everything to the right and below the view centre.
// The resulting PNG is (w×ppp) × (h×ppp) physical pixels, which at the 1400×900 logical
// window size on 2x Retina is 2800×1800 — well above 720p.
//
// Y orientation:
//   macOS Metal: IMGUI framebuffer is top-down at ReadPixels time — toolbar at pixel row 0,
//   status bar at pixel row h-1.  No Y-flip needed.  EncodeToPNG emits as stored.
//   Windows/Linux (OpenGL): framebuffer is bottom-up; a Y-flip would be needed.  The gate
//   currently only runs on macOS; a platform #if guard for the flip is left as a comment
//   in ExecutePendingCapture below for a future Windows CI pass.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Golfin.Editor.GreenAuthoring
{
    /// <summary>
    /// Shared helper for asynchronous EditorWindow framebuffer captures.
    ///
    /// <para>Works by tagging an EditorWindow with a pending capture request, then completing
    /// the ReadPixels inside the EditorWindow's own OnGUI Repaint pass (via
    /// <see cref="ExecutePendingCapture"/>).  The caller polls <see cref="IsReady"/> before
    /// consuming the output file.</para>
    /// </summary>
    public static class CaptureEditorWindow
    {
        // Per-window state keyed by EditorWindow instance.
        private static readonly Dictionary<EditorWindow, PendingCapture> _pending =
            new Dictionary<EditorWindow, PendingCapture>();

        private class PendingCapture
        {
            public string Path;
            public bool   Ready;
        }

        /// <summary>
        /// Schedules a capture of <paramref name="window"/> on its next Repaint pass.
        /// Call <see cref="EditorWindow.Repaint"/> after this to ensure the pass fires promptly.
        /// </summary>
        public static void Request(EditorWindow window, string outputPath)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (string.IsNullOrEmpty(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            _pending[window] = new PendingCapture { Path = outputPath, Ready = false };
        }

        /// <summary>
        /// Returns <c>true</c> once the capture requested via <see cref="Request"/> has been
        /// written to disk.  One-shot — resets to <c>false</c> after reading.
        /// </summary>
        public static bool IsReady(EditorWindow window)
        {
            if (window == null) return false;
            if (!_pending.TryGetValue(window, out var state)) return false;
            if (!state.Ready) return false;

            _pending.Remove(window);
            return true;
        }

        /// <summary>
        /// EditorWindow subclasses call this at the END of their <c>OnGUI</c> method, after all
        /// draw calls, to complete any pending capture request for themselves.
        ///
        /// <para>The method checks whether a capture was requested and, during an
        /// <see cref="EventType.Repaint"/> pass, reads the IMGUI framebuffer and writes a PNG.</para>
        ///
        /// <para>Capture rect = the FULL window area: <c>Rect(0, 0, position.width, position.height)</c>.
        /// This includes the top toolbar, left/right sidebars, centre panel, and status bar.</para>
        /// </summary>
        public static void ExecutePendingCapture(EditorWindow window)
        {
            if (window == null) return;
            if (!_pending.TryGetValue(window, out var state)) return;
            if (state.Ready) return;  // already done for this request
            if (Event.current.type != EventType.Repaint) return;

            string outPath = state.Path;

            try
            {
                // On Retina / HiDPI displays, position.width/height are in LOGICAL pixels,
                // but ReadPixels operates in PHYSICAL pixels.  On a 2x Retina Mac, a 1400×900
                // logical window has a 2800×1800 physical framebuffer.  Without the pixelsPerPoint
                // multiplier, ReadPixels(Rect(0,0,1400,900)) reads only the bottom-left quadrant
                // of the physical framebuffer (700×450 logical pixels), cutting off everything
                // to the right and below the centre.  Multiplying by pixelsPerPoint fixes this.
                float ppp = EditorGUIUtility.pixelsPerPoint;
                int iw = Mathf.Max(1, Mathf.RoundToInt(window.position.width  * ppp));
                int ih = Mathf.Max(1, Mathf.RoundToInt(window.position.height * ppp));

                var tex = new Texture2D(iw, ih, TextureFormat.RGB24, false);

                // ReadPixels reads the current GL/Metal IMGUI framebuffer at PHYSICAL pixel res.
                // Rect(0, 0, iw, ih) = full physical-pixel content area (window.position × ppp),
                // including toolbar (top), sidebars (left/right), and status bar (bottom).
                //
                // NO Y-FLIP: on macOS Metal the IMGUI framebuffer is already top-down
                // (toolbar at pixel row 0, status bar at row h-1). EncodeToPNG emits as stored.
                // On Windows (OpenGL) the framebuffer is bottom-up; a post-ReadPixels flip would
                // be required.  Add a platform check here if this runs on Windows:
                //
                //   #if UNITY_EDITOR_WIN
                //   Color32[] px = tex.GetPixels32(); System.Array.Reverse(px); ...
                //   #endif
                tex.ReadPixels(new Rect(0, 0, iw, ih), 0, 0);
                tex.Apply();

                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                Debug.Log($"[CaptureEditorWindow] Captured {iw}×{ih} px → {outPath}");
                state.Ready = true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureEditorWindow] Capture failed for {outPath}: {ex.Message}");
                _pending.Remove(window);
            }
        }
    }
}
