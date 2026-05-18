using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Viewer;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.HUD;

/// <summary>
/// One-shot smoke test for cup_speed_gated_capture task.
/// Placed in LabScaffold scene, auto-runs on Start in play mode.
/// Uses BallSimulation.Simulate directly to bypass camera-yaw rotation in
/// PhysicsLabController.RunSimForCamera (which ignores preset origin).
///
/// Shot 1 — slow putt at 0.8 m/s: ball placed 0.5 m from pin, fired toward pin.
///   Expected: speed at cup &lt; 1.5 m/s threshold → InCup capture.
///
/// Shot 2 — fast putt at 3.5 m/s: same setup.
///   Expected: speed at cup &gt; 1.5 m/s threshold → AtRest past cup (flyover).
///
/// Both captured via CaptureCore.SnapPlayModeSafe and copied to task screenshots/.
/// </summary>
[DefaultExecutionOrder(9999)]
public class SmokeCaptureCupSpeedGate : MonoBehaviour
{
    public string OutputFolder = "Docs/Specs/Active/cup_speed_gated_capture/screenshots";

    IEnumerator Start()
    {
        // Wait for PhysicsLabController.Start() + ScanForLoadedHoleSceneAtStartup + OnHoleLoaded
        yield return new WaitForSeconds(5f);

        var ctrl = FindFirstObjectByType<PhysicsLabController>();
        if (ctrl == null)
        {
            Debug.LogError("[SmokeCapture] PhysicsLabController not found — aborting");
            Destroy(gameObject);
            yield break;
        }

        Vector3 pinW = HoleContext.PinWorld;
        Debug.Log($"[SmokeCapture] PinWorld={pinW:F3}");

        if (pinW == Vector3.zero)
        {
            Debug.LogError("[SmokeCapture] PinWorld is Vector3.zero — RealCupDetector not installed. Aborting.");
            Destroy(gameObject);
            yield break;
        }

        // Place the ball 0.5 m from pin in the -X direction (standard Hole_01 green is flat-ish)
        Vector3 ballPos = pinW + new Vector3(-0.5f, 0f, 0f);
        ctrl.PlaceBallAt(ballPos, preferredSurfaceTypeValue: (int)Golfin.Physics.SurfaceType.Green);

        // Wait for placement to settle
        yield return new WaitForSeconds(1f);

        // Set camera yaw to point ball → pin (+X direction, angle=0)
        // Use reflection since _cameraYaw is private
        SetCameraYaw(ctrl, 0f);  // 0 rad = +X direction

        // ── Shot 1: FAST putt 3.5 m/s (first — no modal) ─────────────────────
        // Fire fast first so the InCup from slow putt doesn't block the fast capture.
        // Fast: speed 3.5 > 1.5 m/s threshold → speed-gate rejects capture → AtRest past cup.
        var fastPreset = MakePuttPreset("fast_putt_3.5mps", speedMps: 3.5f);
        Debug.Log($"[SmokeCapture] Firing fast putt 3.5 m/s (speed > 1.5 m/s, should fly over)");
        ctrl.Fire(fastPreset);

        yield return new WaitForSeconds(10f);  // ball rolls past cup, stops on green
        yield return null;

        string fastPath = CaptureCore.SnapPlayModeSafe("fast_putt_3p5mps_flyover");
        CopyToOutput(fastPath, "fast_putt_3p5mps_flyover.png");
        Debug.Log($"[SmokeCapture] Fast putt captured: {fastPath}");

        // ── Shot 2: SLOW putt 0.8 m/s (second — will show InCup modal) ────────
        // Reposition ball again for slow putt
        ctrl.PlaceBallAt(ballPos, preferredSurfaceTypeValue: (int)Golfin.Physics.SurfaceType.Green);
        yield return new WaitForSeconds(1f);
        SetCameraYaw(ctrl, 0f);

        var slowPreset = MakePuttPreset("slow_putt_0.8mps", speedMps: 0.8f);
        Debug.Log($"[SmokeCapture] Firing slow putt 0.8 m/s (speed < 1.5 m/s, should enter cup)");
        ctrl.Fire(slowPreset);

        yield return new WaitForSeconds(8f);  // short putt, InCup triggers SUCCESS modal
        yield return null;

        string slowPath = CaptureCore.SnapPlayModeSafe("slow_putt_0p8mps_InCup");
        CopyToOutput(slowPath, "slow_putt_0p8mps_InCup.png");
        Debug.Log($"[SmokeCapture] Slow putt captured: {slowPath}");

        Debug.Log("[SmokeCapture] DONE. Both captures complete. Destroying.");
        Destroy(gameObject);
    }

    // Makes a preset with velocity entirely in +X at the given speed.
    // RunSimForCamera rotates xz-speed * (cos(yaw), sin(yaw)) — with yaw=0, stays +X.
    static ShotPreset MakePuttPreset(string id, float speedMps)
    {
        return new ShotPreset(
            id: id,
            name: id,
            scene: PresetScene.Hole1,
            origin: new fp3(fp.Zero, fp.Zero, fp.Zero),  // ignored by RunSimForCamera (uses current ball pos)
            velocity: new fp3(fp.FromFloat(speedMps), fp.Zero, fp.Zero),  // +X, rotated by _cameraYaw
            spin: SpinState.None,
            wind: WindConfig.Calm,
            notes: $"Smoke test: {speedMps} m/s putt toward cup"
        );
    }

    static void SetCameraYaw(PhysicsLabController ctrl, float yawRadians)
    {
        var fi = typeof(PhysicsLabController).GetField("_cameraYaw",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (fi == null)
        {
            Debug.LogWarning("[SmokeCapture] Could not find _cameraYaw via reflection — firing in default direction");
            return;
        }
        fi.SetValue(ctrl, yawRadians);

        // Also update the shot controller heading if accessible
        var shotCtrlField = typeof(PhysicsLabController).GetField("_shotController",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (shotCtrlField?.GetValue(ctrl) is UnityEngine.Object sc && sc != null)
        {
            var headingProp = sc.GetType().GetProperty("CameraHeadingRadians",
                BindingFlags.Public | BindingFlags.Instance);
            headingProp?.SetValue(sc, yawRadians);
        }

        Debug.Log($"[SmokeCapture] Set _cameraYaw={yawRadians:F3} rad");
    }

    void CopyToOutput(string srcAbsolute, string fileName)
    {
        if (string.IsNullOrEmpty(srcAbsolute) || !File.Exists(srcAbsolute))
        {
            Debug.LogWarning($"[SmokeCapture] Source not found: {srcAbsolute}");
            return;
        }
        string projectRoot = Application.dataPath.Replace("/Assets", "");
        string destDir = Path.Combine(projectRoot, OutputFolder);
        Directory.CreateDirectory(destDir);
        string dest = Path.Combine(destDir, fileName);
        File.Copy(srcAbsolute, dest, overwrite: true);
        Debug.Log($"[SmokeCapture] Copied to {dest}");
    }
}
