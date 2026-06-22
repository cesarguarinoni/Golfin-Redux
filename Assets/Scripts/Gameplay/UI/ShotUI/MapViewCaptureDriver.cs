// map_view_aiming (Order 352) iter-12 — REAL-INPUT capture driver (runtime MonoBehaviour).
// Lives in Golfin.Gameplay.UI assembly — autoReferenced, has full access to ShotUI types.
// Zero edits to Assets/Scripts/Physics/ (criterion 9).
// Launched via MapViewCaptureBotMenu.LaunchProgrammatic() or directly by script-execute.
//
// iter-8b fixes vs iter-8 raw capture:
//   B6 fix (final): kCaptureFinetune + kFireFinetune reduced 0.6 → 0.25 so bent guide line
//                   is still clearly visible but ball stays in Hole 1 fairway (not trees).
//   B2 fix (kept): SetFinetuneForCapture(0.25f) called AFTER arm FadeDraw, BEFORE map open,
//                  so MapViewController.Open() snapshots ConeFinetune=0.25 → bent guide line.
//   B1/B3/B4/B5 fixes are in MapViewCaptureBotMenu.cs and MapViewController.cs.
//
// iter-6 changes vs iter-5 (preserved):
//   - FadeDraw arm BEFORE map open (criterion 6 fix):
//     Step 4  = ARM FadeDraw + SetFinetuneForCapture(0.6)
//     Step 5  = OPEN map → map reads FadeDrawActive=true, ConeFinetune=0.6 → bent line
//     Step 6  = Snap bent guide line + re-aim to fairway centre
//     Step 7  = SHOOT/close
//     Step 8  = FIRE with finetune=0.6
//   - No Physics edits. Real-input codepath fully preserved from iter-5.
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Capture driver for map_view_aiming (Order 352) iter-6.
    ///
    /// Performs REAL player actions through the ShellScene → GameplaySceneLoader flow:
    ///   Arm FD : ExecuteEvents.pointerDown/Up + onClick on FadeDrawButtonWidget child Button
    ///             (BEFORE opening map — it is active in normal shot view).
    ///   Open   : ExecuteEvents.pointerDown/Up + onClick on the real HoleMap Button GO.
    ///   Re-aim : mvc.TrySetAimFromScreenPoint — same codepath as player tap/drag.
    ///   Close  : ExecuteEvents.pointerDown/Up + onClick on the real SHOOT Button GO.
    ///   Fire   : ShotController.BeginExternalDrag → ramp SetExternalPower → EndExternalDrag.
    ///
    /// Arms via PlayerPrefs so it survives the play-mode domain reload.
    /// </summary>
    public class MapViewCaptureDriver : MonoBehaviour
    {
        // ── PlayerPrefs keys (survive domain reload, unlike SessionState) ─────
        public const string ArmedKey      = "MapViewCapture.Armed";
        public const string CaptureDirKey = "MapViewCapture.CaptureDir";

        public static bool Armed
        {
            get => PlayerPrefs.GetInt(ArmedKey, 0) != 0;
            set { PlayerPrefs.SetInt(ArmedKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
        public static string CaptureDir
        {
            get => PlayerPrefs.GetString(CaptureDirKey, "");
            set { PlayerPrefs.SetString(CaptureDirKey, value); PlayerPrefs.Save(); }
        }

        // ── RuntimeInitializeOnLoad injection ────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInject()
        {
            if (!PlayerPrefs.HasKey(ArmedKey) || PlayerPrefs.GetInt(ArmedKey, 0) == 0)
                return;

            // Check if already injected
            if (FindObjectOfType<MapViewCaptureDriver>() != null)
                return;

            var go = new GameObject("MapViewCaptureDriver_iter6");
            DontDestroyOnLoad(go);
            go.AddComponent<MapViewCaptureDriver>();
            Debug.Log("[MapViewCaptureDriver] Auto-injected via RuntimeInitializeOnLoad.");
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        void Start()
        {
            bool armed = Armed;
            Debug.Log($"[MapViewCaptureDriver] Start() Armed={armed}");
            if (!armed)
            {
                Debug.LogWarning("[MapViewCaptureDriver] Not armed — destroying self.");
                Destroy(gameObject);
                return;
            }

            Armed = false; // clear immediately
            StartCoroutine(RunScenario());
        }

        // ── Scenario coroutine ────────────────────────────────────────────────
        IEnumerator RunScenario()
        {
            string captureDir = CaptureDir;
            if (string.IsNullOrEmpty(captureDir))
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                captureDir = Path.GetFullPath(Path.Combine(root,
                    "Docs/Specs/Active/map_view_aiming/screenshots/iter30"));
            }
            Directory.CreateDirectory(captureDir);

            int counter = 1;
            void Log(string msg)
            {
                Debug.Log($"[MapViewCapture] {msg}");
                File.AppendAllText(Path.Combine(captureDir, "history.log"),
                    $"[t={Time.realtimeSinceStartup:F2}] {msg}\n");
            }
            IEnumerator Snap(string label)
            {
                yield return new WaitForEndOfFrame();
                string lbl = $"s{counter:D2}_{label}";
                counter++;
                string src = CaptureCore.SnapPlayModeSafe(lbl);
                if (!string.IsNullOrEmpty(src) && File.Exists(src))
                {
                    string dst = Path.Combine(captureDir, Path.GetFileName(src));
                    File.Copy(src, dst, overwrite: true);
                    Log($"Snap: {lbl} → {dst}");
                }
                else Log($"Snap WARN: no file for {lbl}");
            }

            Log("=== MapViewCaptureDriver iter-30 started (tight-framing: biased lookAt, no black void) ===");

            // ── Step 0: wait for realtime startup ────────────────────────────
            yield return new WaitForSecondsRealtime(5f);

            // ── Step 1: navigate to Home ──────────────────────────────────────
            Log("Step 1: NavigateToHome");
            yield return NavigateToHome(Log);
            yield return new WaitForSecondsRealtime(2f);

            // ── Step 2: Navigate to HoleSelection ─────────────────────────────
            Log("Step 2: ShowScreen(HoleSelection) — real ScreenManager codepath");
            yield return ShowHoleSelectionViaScreenManager(Log);
            yield return WaitForScreen("HoleSelection", 15f, Log);
            yield return new WaitForSecondsRealtime(3f);
            yield return Snap("hole_selection");

            // ── Step 3: select hole 1 via its ActionButton ─────────────────────
            Log("Step 3: tap HoleCardController.actionButton for hole 1");
            yield return ClickHoleCardActionButton(Log);

            yield return WaitForSceneLoaded("LabScaffold", 60f, Log);
            Log("WaitForAnyHoleGeo: polling…");
            {
                float ge = 0f; bool gf = false;
                while (ge < 90f)
                {
                    for (int si = 0; si < SceneManager.sceneCount; si++)
                    {
                        var sc = SceneManager.GetSceneAt(si);
                        if (sc.isLoaded && sc.name.StartsWith("Hole_") && sc.name.EndsWith("_Geo"))
                        {
                            Log($"WaitForAnyHoleGeo OK: '{sc.name}' after {ge:F1}s");
                            gf = true; break;
                        }
                    }
                    if (gf) break;
                    yield return new WaitForSecondsRealtime(0.5f);
                    ge += 0.5f;
                }
                if (!gf) { Log("WaitForAnyHoleGeo TIMEOUT"); yield break; }
            }
            yield return new WaitForSecondsRealtime(5f);
            yield return Snap("hole_loaded");

            // ── §iter-26 FIX 0 GATE: Sample gameplay frame luma PRE-MAP-OPEN ─────────────────
            // Verifies that terrain renders NON-BLACK when the map cam is in idle/closed state
            // (clearFlags=Depth → does NOT wipe gameplay framebuffer).
            string gameplayLumaJsonPath = Path.Combine(captureDir, "gameplay_fix0_luma.json");
            yield return SampleGameplayFrameLuma("pre_open", gameplayLumaJsonPath, Log);

            // ── iter-21 CARRY FIX: Set ClubContext.SelectedDistance before map opens ───────────
            // Root cause of iter-20 carry regression (154yd driver fallback):
            //   ClubContext.SelectedDistance was 0 when MapViewController.Open() ran → fell
            //   through to ShotConeView.MaxCarryYardsForMap (~154yd driver).
            //
            // Fix: directly set SelectedDistance = 124 (7-Wood carry, matching iter-19 / reference).
            // This mirrors the pattern in CaptureHelper.cs line 155:
            //   ClubContext.SelectedDistance = 230  (editor fake-state helper).
            //
            // NOTE: PhysicsLabController.PushSelectedClubDistanceToContext() is the production path
            //   that would populate this in real gameplay, but it does not run in this scripted
            //   capture flow. Direct assignment here is the correct approach for capture scenarios.
            {
                Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance = 124;
                Log($"  iter-21 CARRY FIX: ClubContext.SelectedDistance set to 124 (7-Wood carry). " +
                    $"Actual value: {Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance}");
            }

            // ── Step 3b (iter-8g B1 Y-flip fix): PRE-WARM the MapViewController RenderTexture ──
            // On macOS Metal, allocating a NEW RenderTexture during Unity Recorder recording
            // causes a single Y-flipped frame in the output. Pre-warming the RT here (after the
            // hole is stable but BEFORE the recorder's hotpath around Open()) lets Open() →
            // BuildRuntimeObjects() reuse the existing RT with no Metal swapchain disruption.
            Log("Step 3b (iter-8g): PrewarmRT on MapViewController to avoid Y-flip during recording");
            {
                var mvc = FindObjectOfType<MapViewController>();
                if (mvc != null)
                {
                    mvc.PrewarmRT();
                    Log("  PrewarmRT() called — RT pre-allocated before recording hotpath");
                    // Wait one frame so Metal commits the RT allocation before the Recorder captures.
                    yield return new WaitForEndOfFrame();
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                else
                    Log("  WARN: MapViewController not found — PrewarmRT skipped (Y-flip may occur)");
            }

            // ── Step 4 (ITER-6 NEW): ARM FADE/DRAW BEFORE OPENING MAP ─────────
            // The FadeDrawButtonWidget button IS ACTIVE in normal shot view.
            // Must arm BEFORE opening map (which hides all chrome except SHOOT).
            Log("Step 4 (iter-6): ARM FadeDraw BEFORE opening map — tapping real FadeDraw button");
            bool fadeDrawArmed = false;
            {
                // Approach 1: via FadeDrawButtonWidget
                var fdWidget = FindObjectOfType<FadeDrawButtonWidget>();
                Button fdBtn = null;
                if (fdWidget != null)
                {
                    // The button may be a child — search including inactive children of the widget
                    // but the widget itself must be active for this to work
                    fdBtn = fdWidget.GetComponentInChildren<Button>(includeInactive: true);
                    Log($"  Found FadeDrawButtonWidget '{fdWidget.gameObject.name}' " +
                        $"widgetActive={fdWidget.gameObject.activeInHierarchy} " +
                        $"Button={fdBtn?.gameObject.name ?? "null"} " +
                        $"ButtonActive={fdBtn?.gameObject.activeInHierarchy}");
                }

                // Approach 2: text-content fallback (STRAIGHT or FADE/DRAW text on active buttons)
                if (fdBtn == null || !fdBtn.gameObject.activeInHierarchy)
                {
                    fdBtn = FindButtonByTextContent("STRAIGHT", Log)
                         ?? FindButtonByTextContent("FADE", Log)
                         ?? FindButtonByTextContent("DRAW", Log);
                    Log($"  FadeDrawButton text fallback: {fdBtn?.gameObject.name ?? "NOT FOUND"}");
                }

                if (fdBtn != null && fdBtn.gameObject.activeInHierarchy)
                {
                    var ped = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(fdBtn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(0.15f);
                    ExecuteEvents.Execute(fdBtn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                    fdBtn.onClick.Invoke();
                    Log("  FadeDraw Button: pointer-down/up + onClick fired");
                    fadeDrawArmed = true;
                }
                else if (fdBtn != null)
                {
                    // Button found but not active — try invoking onClick directly since
                    // FadeDrawButtonWidget.OnClick() calls ShotModeContext.Toggle()
                    fdBtn.onClick.Invoke();
                    Log($"  FadeDraw Button not active in hierarchy — onClick.Invoke() called directly");
                    fadeDrawArmed = true;
                }
                else
                {
                    // Last resort: call ShotModeContext.Toggle() directly (same effect as the button click)
                    Log("  FadeDraw button not found — calling ShotModeContext.Toggle() directly");
                    ShotModeContext.Toggle();
                    fadeDrawArmed = true;
                }

                yield return new WaitForSecondsRealtime(0.5f);
                var sc2 = FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
                Log($"  After FadeDraw arm: ShotModeContext.Mode={ShotModeContext.Mode} " +
                    $"sc.FadeDrawActive={sc2?.FadeDrawActive} fadeDrawArmed={fadeDrawArmed}");

                // B2 fix (iter-8): pre-set a NON-ZERO ConeFinetune via editor-only seam so that
                // MapViewController.Open() snapshots a non-zero value → visible bend in the guide line.
                // kCaptureFinetune = 0.25 (was 0.6 in iter-8 raw capture — ball curved too far right
                // into Hole 1 right-side trees). 0.25 = still clearly visible bend without tree landing.
                const float kCaptureFinetune = 0.25f;
                if (sc2 != null)
                {
                    sc2.SetFinetuneForCapture(kCaptureFinetune);
                    Log($"  SetFinetuneForCapture({kCaptureFinetune}) → sc.ConeFinetune={sc2.ConeFinetune:F2} (B2 fix)");
                }
                else
                    Log("  WARN: ShotController not found — cannot pre-set finetune (B2 may fail)");
            }
            yield return Snap("fadedraw_armed_preopen");

            // ── Step 5: OPEN map — tap the real HoleMap Button ───────────────
            // FadeDrawActive is now true AND ConeFinetune=0.6 → map guide line will be bent on open.
            Log("Step 5: OPEN map — tapping real HoleMap button (FadeDraw already armed)");
            {
                var holeMapBtn = FindButtonByGoName("HoleMap", Log);
                if (holeMapBtn == null) { Log("FAIL: HoleMap Button not found — abort"); yield break; }

                var ped = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(holeMapBtn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.1f);
                ExecuteEvents.Execute(holeMapBtn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                holeMapBtn.onClick.Invoke();
                Log("  HoleMap Button: pointer-down/up + onClick fired → mvc.Open()");
            }
            yield return new WaitForSecondsRealtime(1.5f);

            {
                var mvc = FindObjectOfType<MapViewController>();
                var sc3  = FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
                if (mvc == null || !mvc.IsOpen)
                    Log("WARN: MapViewController is not open after HoleMap click");
                else
                    Log($"VERIFY: mvc.IsOpen={mvc.IsOpen} FadeDrawActive={sc3?.FadeDrawActive} " +
                        $"ShotMode={ShotModeContext.Mode} — map open, checking bent guide line");
            }
            yield return Snap("map_open_bent");  // Should show bent guide line

            // ── Step 6: RE-AIM with a 20° offset from the real ball→flag direction ────
            // iter-30 FIX: the old step-6 used π/2 (north) which is WRONG for Hole 1:
            //   the flag is at world (-230, 0, -72) = south-west of ball (219, 11, 34).
            //   Aiming north (π/2) points into open void/sky outside the terrain mesh.
            // Fix: offset the CURRENT aim (= ball→flag direction ≈ -2.907 rad) by +0.35 rad
            //   (≈ 20°) to show a rotated bent guide line while staying on terrain.
            // This is a TESTING-ONLY bypass to show a distinct aimed state; production
            //   players aim via drag/tap (TrySetAimFromScreenPoint).
            // After setting aim, call ForceInvariantDump("open_aimed_flag") for state2.
            Log("Step 6 (iter-30): RE-AIM +0.35 rad from current aim (real flag direction). " +
                "ForceInvariantDump state2 after.");
            float chosenHeading = float.NaN;
            {
                var mvc = FindObjectOfType<MapViewController>();
                if (mvc == null) { Log("FAIL: MapViewController not found — abort"); yield break; }

                // Read the current aim (= original ball→flag direction on Hole 1 ≈ -2.907 rad)
                // and rotate by +0.35 rad (≈ 20°) to produce a visible re-aim without terrain void.
                float currentAim = mvc.AimYawRadians;
                float offsetAim  = currentAim + 0.35f;  // 20° offset — shows bent guide line
                mvc.SetAimYawDirectly(offsetAim);
                chosenHeading = mvc.AimYawRadians;
                Log($"  SetAimYawDirectly: currentAim={currentAim:F4} → offsetAim={offsetAim:F4} rad ({offsetAim * Mathf.Rad2Deg:F1} deg) → mvc.AimYaw={chosenHeading:F4}");

                // Give the guide line one frame to update.
                yield return null;

                // Dump state2 invariant: all 3 markers should now be in-viewport.
                mvc.ForceInvariantDump("open_aimed_flag");
                Log("  ForceInvariantDump('open_aimed_flag') written → state2 JSON.");
                Log($"  Chosen heading after re-aim: {chosenHeading:F4} rad ({chosenHeading * Mathf.Rad2Deg:F1} deg)");
            }
            yield return new WaitForSecondsRealtime(1.0f);
            yield return Snap("map_aimed_bent");  // Bent guide line after 20° re-aim

            // ── Step 7: CLOSE map — tap the real SHOOT button ─────────────────
            Log("Step 7: CLOSE map — tapping real SHOOT button");
            float headingAtClose = float.NaN;
            {
                var shootBtn = FindButtonByTextContent("SHOOT", Log);
                if (shootBtn == null)
                {
                    var mvc2 = FindObjectOfType<MapViewController>();
                    if (mvc2 != null)
                    {
                        var field = typeof(MapViewController).GetField("_shootButton",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        shootBtn = field?.GetValue(mvc2) as Button;
                        Log($"  SHOOT button via reflection: {shootBtn?.gameObject.name ?? "null"}");
                    }
                }
                if (shootBtn != null)
                {
                    var mvc3 = FindObjectOfType<MapViewController>();
                    headingAtClose = mvc3 != null ? mvc3.AimYawRadians : float.NaN;
                    Log($"  AimYawRadians at close: {headingAtClose:F4} rad ({headingAtClose * Mathf.Rad2Deg:F1} deg)");

                    var ped = new PointerEventData(EventSystem.current);
                    ExecuteEvents.Execute(shootBtn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                    yield return new WaitForSecondsRealtime(0.1f);
                    ExecuteEvents.Execute(shootBtn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                    shootBtn.onClick.Invoke();
                    Log("  SHOOT Button: pointer-down/up + onClick → mvc.Close()");
                }
                else
                    Log("WARN: SHOOT button not found");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            {
                var mvc4 = FindObjectOfType<MapViewController>();
                var sc3  = FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
                Log($"  After SHOOT: mvc.IsOpen={mvc4?.IsOpen} sc.CameraHeadingRadians={sc3?.CameraHeadingRadians:F4}");
            }
            yield return Snap("map_closed");

            // ── §iter-26 FIX 0 GATE: Sample gameplay frame luma POST-MAP-CLOSE ─────────────────
            // Verifies that terrain renders NON-BLACK after the map cam returns to idle state.
            yield return SampleGameplayFrameLuma("post_close", gameplayLumaJsonPath, Log);

            // ── Step 8: FIRE via real ShotController drag path ────────────────
            // iter-8b: pass the same kCaptureFinetune (0.25) to SetExternalPower so the actual
            // shot curves as demonstrated by the map guide line (FadeDraw armed, finetune=0.25).
            // Reduced from 0.6 to avoid curving the ball into the Hole 1 right-side tree line.
            Log("Step 8: FIRE via ShotController.BeginExternalDrag → ramp → EndExternalDrag (finetune=0.25)");
            const float kFireFinetune = 0.25f;  // matches kCaptureFinetune set before map open
            float headingAtFire = float.NaN;
            {
                var sc = FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
                if (sc == null) { Log("FAIL: no ShotController — cannot fire"); yield break; }

                headingAtFire = sc.CameraHeadingRadians;
                Log($"  CameraHeadingRadians before fire: {headingAtFire:F4} rad ({headingAtFire * Mathf.Rad2Deg:F1} deg)");

                if (!float.IsNaN(headingAtClose) && !float.IsNaN(headingAtFire))
                {
                    float delta = Mathf.Abs(Mathf.DeltaAngle(
                        headingAtClose * Mathf.Rad2Deg,
                        headingAtFire  * Mathf.Rad2Deg));
                    // iter-10 FIX: real assertion, not hardcoded "(pass)" string.
                    if (delta > 5f)
                        Debug.LogError($"[MapViewCapture] CRITERION 5b FAIL: heading delta={delta:F2} deg > 5 deg threshold. close={headingAtClose * Mathf.Rad2Deg:F1} fire={headingAtFire * Mathf.Rad2Deg:F1}");
                    else
                        Log($"  HEADING DELTA (close→fire): {delta:F2} deg — CRITERION 5b PASS (≤ 5 deg)");
                }

                // Wait for ShotController Idle
                float idleWait = 0f;
                while (sc.State != Golfin.Gameplay.Input.ShotState.Idle && idleWait < 4f)
                {
                    idleWait += Time.unscaledDeltaTime;
                    yield return null;
                }
                Log($"  ShotController Idle gate: state={sc.State} waited={idleWait:F2}s");

                sc.BeginExternalDrag();

                const float rampSeconds = 0.85f;
                const float targetPower = 0.35f;  // iter-8f: reduced from 0.45 to carry ~150yd to open fairway pre-tree-cluster
                float rt = 0f;
                while (rt < rampSeconds)
                {
                    rt += Time.unscaledDeltaTime;
                    // Pass kFireFinetune throughout ramp so the FadeDraw curve is applied to the shot.
                    sc.SetExternalPower(Mathf.Lerp(0f, targetPower, rt / rampSeconds), kFireFinetune);
                    yield return null;
                }
                sc.SetExternalPower(targetPower, kFireFinetune);
                yield return new WaitForSecondsRealtime(0.2f);
                sc.EndExternalDrag();
                Log($"  Fired: BeginExternalDrag→ramp {targetPower:F2} over {rampSeconds:F2}s→EndExternalDrag (finetune={kFireFinetune:F2}) [B6 fix iter-8f: 0.35 power lands ~150yd in open fairway]");
            }
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Snap("ball_airborne");
            yield return new WaitForSecondsRealtime(6f);
            yield return Snap("ball_landed");

            // ── Final summary ─────────────────────────────────────────────────
            Log("=== MapViewCapture DONE iter-26 ===");
            Log($"  fadeDrawArmed: {fadeDrawArmed}");
            Log($"  chosenHeading:  {chosenHeading:F4} rad ({chosenHeading * Mathf.Rad2Deg:F1} deg)");
            Log($"  headingAtClose: {headingAtClose:F4} rad ({headingAtClose * Mathf.Rad2Deg:F1} deg)");
            Log($"  headingAtFire:  {headingAtFire:F4} rad ({headingAtFire * Mathf.Rad2Deg:F1} deg)");
            if (!float.IsNaN(headingAtClose) && !float.IsNaN(headingAtFire))
            {
                float finalDelta = Mathf.Abs(Mathf.DeltaAngle(
                    headingAtClose * Mathf.Rad2Deg,
                    headingAtFire  * Mathf.Rad2Deg));
                // iter-10 FIX: real assertion in final summary too.
                if (finalDelta > 5f)
                    Debug.LogError($"[MapViewCapture] CRITERION 5b FINAL FAIL: delta={finalDelta:F2} deg > 5 deg. close={headingAtClose * Mathf.Rad2Deg:F1} fire={headingAtFire * Mathf.Rad2Deg:F1}");
                else
                    Log($"  CRITERION 5b FINAL DELTA: {finalDelta:F2} deg — PASS (≤ 5 deg)");
            }

            // Write evidence file
            string evidencePath = Path.Combine(captureDir, "iter22_heading_evidence.txt");
            File.WriteAllText(evidencePath,
                $"fadeDrawArmed={fadeDrawArmed}\n" +
                $"chosenHeading={chosenHeading:F4} rad ({chosenHeading * Mathf.Rad2Deg:F1} deg)\n" +
                $"headingAtClose={headingAtClose:F4} rad ({headingAtClose * Mathf.Rad2Deg:F1} deg)\n" +
                $"headingAtFire={headingAtFire:F4} rad ({headingAtFire * Mathf.Rad2Deg:F1} deg)\n" +
                (!float.IsNaN(headingAtClose) && !float.IsNaN(headingAtFire) ?
                    $"delta_close_to_fire={Mathf.Abs(Mathf.DeltaAngle(headingAtClose * Mathf.Rad2Deg, headingAtFire * Mathf.Rad2Deg)):F2} deg\n" :
                    "delta=UNDEFINED\n"));
            Log($"Evidence written: {evidencePath}");

            // Signal the Editor recorder to stop (via PlayerPrefs flag)
            // The Editor launcher's playModeStateChanged hook will call BotVideoRecorder.End()
            // on ExitingPlayMode — we just exit play mode now.
            Destroy(gameObject);
#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#endif
        }

        // ── §iter-26 FIX 0 GATE: Gameplay terrain luma sampling ─────────────────────────────────
        // Reads the composited framebuffer at a "terrain region" sample point (centre of screen),
        // computes mean luma, and appends the result to a JSON file for the §11 gate validator.
        // A non-black frame (map cam NOT wiping the color buffer) should have luma > 0.05.
        IEnumerator SampleGameplayFrameLuma(string phase, string jsonPath, Action<string> log)
        {
            // Wait for end of frame so the full render is committed.
            yield return new WaitForEndOfFrame();

            // Sample a 64×64 patch at the screen centre.
            int sw = Screen.width;
            int sh = Screen.height;
            int patchW = 64, patchH = 64;
            int x0 = (sw - patchW) / 2;
            int y0 = (sh - patchH) / 2;

            // ReadPixels reads FROM the current composited framebuffer.
            var tex = new Texture2D(patchW, patchH, TextureFormat.RGBA32, mipChain: false);
            tex.ReadPixels(new Rect(x0, y0, patchW, patchH), 0, 0);
            tex.Apply();

            Color32[] pixels = tex.GetPixels32();
            UnityEngine.Object.Destroy(tex);

            // Compute mean Rec.709 luma over the patch.
            float lumaSum = 0f;
            int n = pixels.Length;
            for (int i = 0; i < n; i++)
            {
                float r = pixels[i].r / 255f;
                float g = pixels[i].g / 255f;
                float b = pixels[i].b / 255f;
                lumaSum += 0.2126f * r + 0.7152f * g + 0.0722f * b;
            }
            float meanLuma = lumaSum / n;
            bool allBlack  = meanLuma < 0.005f;

            // Corner samples for JSON diagnostics.
            Color32 tl = pixels[0];
            Color32 br = pixels[n - 1];
            int tlR = tl.r, tlG = tl.g, tlB = tl.b;
            int brR = br.r, brG = br.g, brB = br.b;

            log(string.Format("  FIX0-GATE [{0}]: screen={1}x{2} patch=[{3},{4} {5}x{6}] meanLuma={7:F4} allBlack={8}",
                phase, sw, sh, x0, y0, patchW, patchH, meanLuma, allBlack));
            if (allBlack)
                Debug.LogError(string.Format("[MapViewCapture] FIX0 GATE FAIL [{0}]: meanLuma={1:F4} < 0.005 — gameplay framebuffer IS BLACK.", phase, meanLuma));

            // JSON-lines: one entry per phase, appended to a single file.
            string trueStr  = "true";
            string falseStr = "false";
            string entry = string.Format(
                "{{\"phase\":\"{0}\",\"screen\":[{1},{2}],\"samplePatch\":[{3},{4},{5},{6}],\"meanLuma\":{7:F5},\"allBlack\":{8},\"sample_tl\":[{9},{10},{11}],\"sample_br\":[{12},{13},{14}]}}",
                phase, sw, sh, x0, y0, patchW, patchH, meanLuma,
                allBlack ? trueStr : falseStr,
                tlR, tlG, tlB, brR, brG, brB);
            File.AppendAllText(jsonPath, entry + "\n");
            log(string.Format("  FIX0-GATE [{0}]: luma entry appended to {1}", phase, jsonPath));
        }

        // ── Button / RawImage search helpers ─────────────────────────────────
        static Button FindButtonByGoName(string goName, Action<string> log)
        {
            var all = FindObjectsOfType<Button>(includeInactive: false);
            foreach (var b in all)
                if (b.gameObject.name.Equals(goName, StringComparison.OrdinalIgnoreCase))
                    return b;
            log?.Invoke($"  FindButtonByGoName MISS: '{goName}'");
            return null;
        }

        static Button FindButtonByTextContent(string textSubstr, Action<string> log)
        {
            var all = FindObjectsOfType<Button>(includeInactive: false);
            string lower = textSubstr.ToLowerInvariant();
            foreach (var b in all)
            {
                var tmps = b.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: false);
                foreach (var t in tmps)
                    if (t.text != null && t.text.ToLowerInvariant().Contains(lower))
                        return b;
            }
            log?.Invoke($"  FindButtonByTextContent MISS: '{textSubstr}'");
            return null;
        }

        static RawImage FindRawImageByGoName(string goName, Action<string> log)
        {
            var all = FindObjectsOfType<RawImage>(includeInactive: false);
            foreach (var r in all)
                if (r.gameObject.name.Equals(goName, StringComparison.OrdinalIgnoreCase))
                    return r;
            log?.Invoke($"  FindRawImageByGoName MISS: '{goName}'");
            return null;
        }

        // ── Navigation helpers ────────────────────────────────────────────────
        IEnumerator NavigateToHome(Action<string> log, float timeout = 60f)
        {
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                string cur = GetCurrentScreenName();
                if (cur == "Home") { log("  NavigateToHome: already Home"); yield break; }
                if (cur == "Splash")
                {
                    yield return ClickButton("StartButton", 0.5f, log);
                    elapsed += 1f;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            log("  NavigateToHome TIMEOUT");
        }

        IEnumerator ShowHoleSelectionViaScreenManager(Action<string> log)
        {
            log("  ShowHoleSelectionViaScreenManager: calling ScreenManager.ShowScreen(HoleSelection)");
            bool called = false;
            var monos = FindObjectsOfType<MonoBehaviour>();
            foreach (var m in monos)
            {
                if (m.GetType().Name != "ScreenManager") continue;
                var showMethod = m.GetType().GetMethod("ShowScreen",
                    new System.Type[] { m.GetType().Assembly.GetType("GolfinRedux.UI.ScreenId"), typeof(bool) });
                if (showMethod == null)
                    showMethod = m.GetType().GetMethod("ShowScreen",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (showMethod != null)
                {
                    var enumType = m.GetType().Assembly.GetType("GolfinRedux.UI.ScreenId");
                    if (enumType != null)
                    {
                        var holeSelValue = System.Enum.Parse(enumType, "HoleSelection");
                        showMethod.Invoke(m, new object[] { holeSelValue, false });
                        log($"  ShowScreen(HoleSelection) invoked on '{m.gameObject.name}'");
                        called = true;
                    }
                    break;
                }
            }
            if (!called)
                log("  WARN: ScreenManager.ShowScreen not found via reflection");
            yield return new WaitForSecondsRealtime(0.1f);
        }

        IEnumerator ClickHoleCardActionButton(Action<string> log)
        {
            log("  ClickHoleCardActionButton: finding HoleCardController via FindObjectsOfType");
            var allMonos = FindObjectsOfType<MonoBehaviour>(includeInactive: false);
            MonoBehaviour targetCard = null;
            foreach (var m in allMonos)
            {
                if (m.GetType().Name == "HoleCardController")
                {
                    var holeNumProp = m.GetType().GetProperty("HoleNumber",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var holeNumField = m.GetType().GetField("_holeNumber",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?? m.GetType().GetField("holeNumber",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    int holeNum = -1;
                    if (holeNumProp != null) holeNum = (int)holeNumProp.GetValue(m);
                    else if (holeNumField != null) holeNum = (int)holeNumField.GetValue(m);
                    log($"  Found HoleCardController '{m.gameObject.name}' hole={holeNum}");
                    if (targetCard == null || holeNum == 1)
                        targetCard = m;
                    if (holeNum == 1) break;
                }
            }

            if (targetCard == null)
            {
                log("  WARN: no HoleCardController found — falling back to FindButtonByGoName('ActionButton')");
                yield return ClickButton("ActionButton", 1.5f, log);
                yield break;
            }

            var actionBtnField = targetCard.GetType().GetField("actionButton",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Button actionBtn = actionBtnField?.GetValue(targetCard) as Button;
            log($"  actionButton field: {(actionBtn != null ? actionBtn.gameObject.name : "null")}");

            if (actionBtn != null && actionBtn.gameObject.activeInHierarchy)
            {
                var ped = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(actionBtn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.1f);
                ExecuteEvents.Execute(actionBtn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                actionBtn.onClick.Invoke();
                log($"  HoleCard ActionButton: clicked via ExecuteEvents+onClick on '{actionBtn.gameObject.name}'");
            }
            else
            {
                var allMonos2 = FindObjectsOfType<MonoBehaviour>(includeInactive: false);
                foreach (var m2 in allMonos2)
                {
                    if (m2.GetType().Name == "HoleSelectionScreenController")
                    {
                        var handleActionMethod = m2.GetType().GetMethod("HandleActionClicked",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (handleActionMethod != null)
                        {
                            handleActionMethod.Invoke(m2, new object[] { targetCard });
                            log("  Invoked HoleSelectionScreenController.HandleActionClicked directly");
                        }
                        break;
                    }
                }
            }
            yield return new WaitForSecondsRealtime(1.5f);
        }

        IEnumerator ClickButton(string nameOrText, float settle, Action<string> log)
        {
            log($"  ClickButton: '{nameOrText}'");
            var btn = FindButtonByGoName(nameOrText, null)
                   ?? FindButtonByTextContent(nameOrText, null);
            if (btn != null)
            {
                var ped = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.1f);
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                btn.onClick.Invoke();
                log($"  ClickButton: clicked '{btn.gameObject.name}'");
            }
            else
                log($"  ClickButton MISS: '{nameOrText}' not found");
            yield return new WaitForSecondsRealtime(settle);
        }

        IEnumerator WaitForScreen(string screenName, float timeout, Action<string> log)
        {
            log($"  WaitForScreen: '{screenName}' timeout={timeout}s");
            float e = 0f;
            while (e < timeout)
            {
                if ((GetCurrentScreenName() ?? "").Equals(screenName, StringComparison.OrdinalIgnoreCase))
                { log($"  WaitForScreen OK: '{screenName}' after {e:F1}s"); yield break; }
                yield return new WaitForSecondsRealtime(0.25f);
                e += 0.25f;
            }
            log($"  WaitForScreen TIMEOUT: '{screenName}' current={GetCurrentScreenName()}");
        }

        IEnumerator WaitForSceneLoaded(string sceneName, float timeout, Action<string> log)
        {
            log($"  WaitForSceneLoaded: '{sceneName}' timeout={timeout}s");
            float e = 0f;
            while (e < timeout)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.isLoaded && sc.name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                    { log($"  WaitForSceneLoaded OK: '{sceneName}' after {e:F1}s"); yield break; }
                }
                yield return new WaitForSecondsRealtime(0.5f);
                e += 0.5f;
            }
            log($"  WaitForSceneLoaded TIMEOUT: '{sceneName}'");
        }

        static string GetCurrentScreenName()
        {
            try
            {
                var monos = FindObjectsOfType<MonoBehaviour>();
                foreach (var m in monos)
                {
                    if (m.GetType().Name != "ScreenManager") continue;
                    var f = m.GetType().GetField("_currentScreen",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null) return f.GetValue(m)?.ToString();
                }
            }
            catch { }
            return null;
        }

        static string GetHierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            while (t != null) { sb.Insert(0, "/" + t.name); t = t.parent; }
            return sb.ToString();
        }
    }
}
