// § controls_h iter-4 gate: fire driver_hole1 preset and capture mid-flight Chase + Downrange screenshots.
// Usage: GOLFIN > Tests > Capture Iter4 Shot Sequences
// Must be in Play mode when invoked. The coroutine fires the shot, waits at 3 different timings,
// captures, and pauses. Check Docs/Diagnostics/_capture/ for the output files.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Golfin.Physics.Viewer;
using Golfin.Diagnostics.Runtime;

namespace Golfin.Physics.Tests.Editor
{
    public static class Iter4ShotCapture
    {
        [MenuItem("GOLFIN/Tests/Capture Iter4 Shot Sequences")]
        public static void RunCaptures()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[Iter4ShotCapture] Must be in Play mode first.");
                return;
            }
            var ctrl = Object.FindAnyObjectByType<PhysicsLabController>();
            if (ctrl == null)
            {
                Debug.LogError("[Iter4ShotCapture] PhysicsLabController not found.");
                return;
            }

            // Create runner GO and start coroutine
            var go = new GameObject("_Iter4ShotCaptureRunner");
            var runner = go.AddComponent<CaptureRunner>();
            runner.controller = ctrl;
            runner.StartCoroutine(runner.RunSequence());
        }

        public class CaptureRunner : MonoBehaviour
        {
            public PhysicsLabController controller;

            public IEnumerator RunSequence()
            {
                IReadOnlyList<ShotPreset> presets = ShotPresetCatalog.All;
                int idx = -1;
                for (int i = 0; i < presets.Count; i++)
                    if (presets[i].Id == "driver_hole1") { idx = i; break; }

                if (idx < 0)
                {
                    Debug.LogError("[Iter4ShotCapture] driver_hole1 preset not found.");
                    Destroy(gameObject);
                    yield break;
                }

                // === Shot 1: Capture Chase mode at ~1s (before Downrange cut at ~1.5s) ===
                Debug.Log("[Iter4ShotCapture] Firing shot 1 for Chase-mode capture...");
                controller.Fire(presets[idx]);
                yield return new WaitForSeconds(1.0f);  // ~40-50% carry, still in Chase mode

                // Use SnapAtEndOfFrameAndPause (coroutine) to capture at end-of-frame before pausing
                yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause("controls_h_iter4_chase_midflight", skipPause: true));
                Debug.Log("[Iter4ShotCapture] Chase snap complete");

                // Wait for shot 1 to complete
                yield return new WaitForSeconds(3.0f);

                // === Shot 2: Capture Downrange mode at ~1.7s (just after 65% carry cut fires) ===
                Debug.Log("[Iter4ShotCapture] Firing shot 2 for Downrange-mode capture...");
                controller.Fire(presets[idx]);
                yield return new WaitForSeconds(1.7f);  // ~65-70% carry, Downrange cut should have fired

                yield return StartCoroutine(CaptureCore.SnapAtEndOfFrameAndPause("controls_h_iter4_downrange", skipPause: true));
                Debug.Log("[Iter4ShotCapture] Downrange snap complete");

                // Wait for shot 2 to complete
                yield return new WaitForSeconds(3.0f);

                Debug.Log("[Iter4ShotCapture] DONE. Check Docs/Diagnostics/_capture/ for controls_h_iter4_* files.");
                Debug.Log("[Iter4ShotCapture] Check Docs/Diagnostics/_capture/ for output files.");
                Destroy(gameObject);
            }
        }
    }
}
