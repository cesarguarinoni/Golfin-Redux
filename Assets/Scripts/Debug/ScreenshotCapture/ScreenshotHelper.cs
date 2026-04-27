using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Golfin.Gameplay.Input;

namespace Golfin.Dev
{
    /// <summary>
    /// Play-mode screenshot helper. Add to any active GO in LabScaffold, then call
    /// Capture() via script-execute / reflection-method-call.
    /// Waits for the render to stabilise before firing the GOLFIN screenshot menu.
    /// Auto-destroys after each capture.
    /// </summary>
    public class ScreenshotHelper : MonoBehaviour
    {
        [Tooltip("Frames to hold the state before capturing.")]
        public int stabiliseFrames = 4;

        /// <summary>Capture at <paramref name="power01"/> (0=tip, 1=full pull).</summary>
        public void Capture(float power01 = 1f)
        {
            StartCoroutine(DoCaptureCoroutine(power01));
        }

        private IEnumerator DoCaptureCoroutine(float power01)
        {
            var controller = FindFirstObjectByType<ShotController>(FindObjectsInactive.Include);
            if (controller == null)
            {
                UnityEngine.Debug.LogError("[ScreenshotHelper] ShotController not found.");
                Destroy(this);
                yield break;
            }

            // 1. Full reset to Idle
            if (controller.State != ShotState.Idle)
                controller.CancelExternalDrag();

            yield return null; // let Idle propagate

            // 2. Enter pulling state at requested power
            controller.BeginExternalDrag();
            controller.SetExternalPower(Mathf.Clamp01(power01), 0f);

            // 3. Hold stable: re-assert power each frame so TickArrow
            //    cannot cycle all the way to MaxTotalPasses mid-capture.
            for (int i = 0; i < stabiliseFrames; i++)
            {
                if (controller.State == ShotState.Idle)
                {
                    // TickArrow reset us — re-enter immediately
                    controller.BeginExternalDrag();
                    controller.SetExternalPower(Mathf.Clamp01(power01), 0f);
                }
                yield return null;
            }

            // 4. Capture
#if UNITY_EDITOR
            EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View");
#endif
            UnityEngine.Debug.Log($"[ScreenshotHelper] Captured power={power01 * 100f:F0}%");

            Destroy(this);
        }
    }
}
