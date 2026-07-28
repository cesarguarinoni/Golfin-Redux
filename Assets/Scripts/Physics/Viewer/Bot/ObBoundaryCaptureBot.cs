#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Standalone bot for OB boundary presentation capture clips on Hole 6.
    ///
    /// Three scenarios (set via SessionState before entering play mode):
    ///   "ob_before"  — Driver aimYaw=0 rad (+X), power=0.85: overshoots right-edge OB band
    ///                  (lands in unmapped terrain past X=+114.45 → Fairway). No OOB terrainHit
    ///                  → clamp does NOT arm → camera follows ball past course edge → void visible.
    ///   "ob_after"   — Same aimYaw=0 (+X), power=0.236: ball LANDS inside the narrow OOB strip
    ///                  (X≈99.7–100.0, just past the green right edge, inside mask).
    ///                  BakedZoneClassifier returns SurfaceType.OOB on first bounce.
    ///                  TryFindFirstOBHit finds hit → clamp arms at OB point; skirt fills void.
    ///   "ob_control" — Driver aimYaw=0.0 rad (toward green, +X), power=0.15 → inbounds.
    ///                  Normal chase camera framing; proves clamp does not fire on normal play.
    ///
    /// ArmDeferred recording: ObBoundaryCaptureMenu calls BotVideoRecorder.ArmDeferred() so
    /// Begin() at EnteredPlayMode is a no-op (DeferredRecord=true, RecordVideo=false). This
    /// bot calls BeginDeferred() via reflection after the hole is loaded and stable — avoiding
    /// the Y-flip that occurs when recording starts before the first frame (Mac/Metal regression).
    ///
    /// BotVideoRecorder.End() is handled unconditionally by LoopV2SmokeBotMenu.ExitingPlayMode.
    ///
    /// Launched exclusively by ObBoundaryCaptureMenu (Editor assembly). Never place in scenes.
    /// </summary>
    public class ObBoundaryCaptureBot : MonoBehaviour
    {
        const string ArmedKey    = "ObBoundaryCapture.Armed";
        const string ScenarioKey = "ObBoundaryCapture.Scenario";
        const float  StartupWait = 5f;

        // ── SessionState properties ────────────────────────────────────────────

        public static bool Armed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        public static string Scenario
        {
            get  => UnityEditor.SessionState.GetString(ScenarioKey, "");
            set  => UnityEditor.SessionState.SetString(ScenarioKey, value);
        }

        // Runtime clamp-suppression for the BEFORE clip: force the chase clamp OFF every
        // frame so the camera behaves byte-identically to HEAD (§5.4 proves active=false ==
        // HEAD). Lets ob_before reuse the EXACT ob_after shot (power=0.27) yet show the
        // pre-fix behaviour — camera follows the ball past the OB boundary instead of holding.
        ChaseCamera _suppressCam;

        IEnumerator SuppressClampForever()
        {
            while (true)
            {
                if (_suppressCam != null) _suppressCam.SetChaseClamp(Vector3.zero, false);
                yield return null;
            }
        }

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[ObBoundaryCaptureBot] Start() — Armed={sessionArmed} Scenario={Scenario}");

            if (!sessionArmed)
            {
                Debug.LogWarning("[ObBoundaryCaptureBot] Not armed — destroying self.");
                Destroy(gameObject);
                return;
            }

            Armed = false; // consume the arm flag

            if (Time.timeScale < 0.01f)
            {
                Debug.LogWarning($"[ObBoundaryCaptureBot] timeScale={Time.timeScale} — forcing to 1.");
                Time.timeScale = 1f;
            }

            StartCoroutine(SafeRun());
        }

        IEnumerator SafeRun()
        {
            yield return new WaitForSecondsRealtime(StartupWait);

            string scenario = Scenario;
            var    driver   = new Bot.BotDriver(
                "Docs/Specs/Active/ob_boundary_presentation/screenshots");

            driver.LogStep($"=== ObBoundaryCaptureBot: scenario='{scenario}' ===");

            // ITER-8 FIX: all scenarios fire aimYaw=0 (+X) toward the right-edge OB boundary.
            //   OB mask (zones.json worldBounds): X∈[-114.45,+114.45], Z∈[-50.3,+50.3], 1024×1024.
            //   Perimeter-frame OB: right edge X≈+108..+113 is solid OB across all Z.
            //   aimYaw=0 → LaunchDir=(+1,0,0) → fires toward +X (right-edge OB).
            //   aimYaw=π (iter-7) was wrong: ball overshot left edge (-114.45) into unmapped Fairway.
            //
            //   Hole 6 tee at X≈82, Z≈-24.54. Green at X≈96-100. OB zone: X≈100..114.45.
            //   (+X direction = toward the green and OB zone beyond.)
            //   ACTUAL Hole 6 geometry (probed iter-8):
            //     power=0.22 → first bounce X=96.73 (Fairway/green area), rolls to X=99.62 (real-time).
            //                  Pre-computed shows TerrainHit OOB at X=99.92; real-time ball stops 0.30
            //                  units short at X=99.62. BakedZoneClassifier: OOB strip is X≈99.7–100.0
            //                  (a narrow ~0.3 unit band at the green right edge).
            //     power=0.28 → first bounce X=109.52 (CartPath! NOT OOB). Ball jumps over the narrow
            //                  OOB strip entirely. All terrain hits: CartPath/Fairway. OBState=False.
            //   The architect's assumption "X=108-113 is solid OB" is WRONG at Z=-24.54 — that range
            //   is CartPath. The only OOB zone in +X direction is the narrow strip at X≈99.7–100.0.
            //
            //   Linear power² fit (two data points): X_carry = 96.73 + 426×(power²−0.22²).
            //   For first bounce X=99.84 (inside OOB strip, target middle≈99.75):
            //     power² = (99.84-96.73)/426 + 0.0484 = 0.05571 → power≈0.236.
            //   At power=0.236, carry lands IN OOB strip → both pre-computed AND real-time OOB hit.
            //   TryFindFirstOBHit arms clamp; OBState fires immediately on first bounce.
            //
            //   ob_control: power=0.15 → first bounce X≈89 (tee area, well before green and OB).
            //   ob_before:  power=0.85 → first bounce X≈349 (past mask → unmapped → Fairway → no OOB hit).
            //   ob_after:   power=0.27 → real-time ball crosses X=105.38 (OOB zone start) → OBState=True → clamp fires.
            //     Iter-8 physics note: power=0.236 pre-computed trajectory showed OOB at X=105.38, but real-time
            //     ball stopped at X=103.27 (2.11 m short, friction divergence). Raised to 0.27 so real-time
            //     ball also crosses X=105.38 and OBState=True fires, confirming the camera-clamp path.
            float aimYaw  = 0.0f; // all scenarios: +X direction toward right-edge OB
            float power01;
            if      (scenario == "ob_control") power01 = 0.15f; // short inbounds
            // OUTER-EDGE demo (Cesar 2026-07-28): terrain mesh ends at X=+114.45; beyond = skybox void.
            // power≈0.50 carries the ball to X≈180 — OVER the outer edge — so the chase camera frames the
            // void region. AFTER = ObGroundSkirt fills it (continuous ground); BEFORE = raw skybox void
            // (skirt destroyed + clamp suppressed → faithful HEAD). This is the empty-screen defect P1 fixes.
            else if (scenario == "ob_before")  power01 = 0.50f;
            else                               power01 = 0.50f;  // ob_after: ball flies past X=114.45 over the void; skirt visible

            bool            done  = false;
            System.Exception caught = null;

            var inner = OBShot(driver, aimYaw: aimYaw, power01: power01);
            yield return StartCoroutine(RunWithCatch(inner,
                () => done = true,
                ex => caught = ex));

            if (caught != null) driver.LogStep($"EXCEPTION: {caught}");
            driver.LogStep(done
                ? "=== OBShot complete ==="
                : "=== OBShot INCOMPLETE — see errors above ===");

            driver.FlushLog($"ob_capture_{scenario}.log");

            Debug.Log("[ObBoundaryCaptureBot] Done. Exiting play mode.");
            Destroy(gameObject);
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        // ── Main shot coroutine ────────────────────────────────────────────────
        // Pattern-matched exactly on Scenarios.AudioWaterSplashSfx (lines 2526-2790).

        IEnumerator OBShot(Bot.BotDriver d, float aimYaw, float power01)
        {
            // 1. Navigate to Home via real ShellScene boot (recording not running yet).
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // 2. Unlock Hole 6 at runtime via HoleProgressionService reflection.
            try
            {
                System.Type svcType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleProgressionService"); if (t != null) { svcType = t; break; } }
                if (svcType == null)
                {
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleProgressionService"); if (t != null) { svcType = t; break; } }
                }
                if (svcType != null)
                {
                    var instanceProp = svcType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    var instance = instanceProp?.GetValue(null);
                    if (instance != null)
                    {
                        var unlockMethod = svcType.GetMethod("SetUnlockedOverride",
                            new[] { typeof(int), typeof(bool) });
                        unlockMethod?.Invoke(instance, new object[] { 6, true });
                        d.LogStep("  HoleProgressionService.SetUnlockedOverride(6, true) — Hole 6 unlocked");
                    }
                    else { d.LogStep("  WARN: HoleProgressionService.Instance is null"); }
                }
                else { d.LogStep("  WARN: HoleProgressionService type not found — Hole 6 may stay locked"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: unlock reflection error: {ex.Message}"); }

            // 3. Practice → HoleSelection.
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f); // HoleCardController expand animation

            // 4. Find Hole 6 card, tap its CardTapButton to expand it.
            bool hole6Tapped = false;
            try
            {
                System.Type cardType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleCardController"); if (t != null) { cardType = t; break; } }
                if (cardType == null)
                {
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleCardController"); if (t != null) { cardType = t; break; } }
                }
                if (cardType != null)
                {
                    var holeNumProp = cardType.GetProperty("HoleNumber",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var cards = UnityEngine.Object.FindObjectsByType(
                        cardType, UnityEngine.FindObjectsSortMode.None);
                    foreach (var card in cards)
                    {
                        int num = (int)(holeNumProp?.GetValue(card) ?? 0);
                        if (num == 6)
                        {
                            var go = ((UnityEngine.Component)card).gameObject;
                            UnityEngine.UI.Button cardTapBtn = null;
                            foreach (var btn in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                            {
                                if (btn.gameObject.name.Contains("CardTapButton") ||
                                    btn.gameObject.name.Contains("TapButton"))
                                { cardTapBtn = btn; break; }
                            }
                            if (cardTapBtn == null) cardTapBtn = go.GetComponentInChildren<UnityEngine.UI.Button>();
                            if (cardTapBtn != null)
                            {
                                cardTapBtn.onClick.Invoke();
                                d.LogStep($"  Tapped Hole 6 card (CardTapButton on '{go.name}')");
                                hole6Tapped = true;
                            }
                            else { d.LogStep("  WARN: Hole 6 card found but no tap button"); }
                            break;
                        }
                    }
                    if (!hole6Tapped) d.LogStep("  WARN: Hole 6 card not found — falling back to ActionButton");
                }
                else { d.LogStep("  WARN: HoleCardController type not found — falling back to ActionButton"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: card tap reflection error: {ex.Message}"); }

            // 5. Seed GameSession and call GameplaySceneLoader.BeginGameplayLoad(6) directly.
            //    Mirrors what HoleSelectionScreenController.HandleActionClicked does.
            yield return new WaitForSecondsRealtime(1.5f); // let card settle after tap

            bool gameplayLoadStarted = false;
            try
            {
                System.Type gsType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Session.GameSession"); if (t != null) { gsType = t; break; } }
                if (gsType == null)
                {
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("GameSession"); if (t != null) { gsType = t; break; } }
                }

                if (gsType != null)
                {
                    var isVersusProp = gsType.GetProperty("IsVersus",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    isVersusProp?.SetValue(null, false);

                    string charId  = string.Empty;
                    int    bagSlot = 0;

                    System.Type cmType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("CharacterManager"); if (t != null) { cmType = t; break; } }
                    if (cmType != null)
                    {
                        var cmInst = cmType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (cmInst != null)
                        {
                            var getSelId = cmType.GetMethod("GetSelectedCharacterId",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            charId = (string)(getSelId?.Invoke(cmInst, null) ?? string.Empty);
                        }
                    }

                    System.Type bmType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("BagManager"); if (t != null) { bmType = t; break; } }
                    if (bmType != null)
                    {
                        var bmInst = bmType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (bmInst != null)
                        {
                            var slotProp = bmType.GetProperty("EquippedBagSlot",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            bagSlot = (int)(slotProp?.GetValue(bmInst) ?? 0);
                        }
                    }

                    var seedMethod = gsType.GetMethod("SeedSession",
                        new[] { typeof(int), typeof(string), typeof(int) });
                    seedMethod?.Invoke(null, new object[] { 6, charId, bagSlot });
                    d.LogStep($"  GameSession.SeedSession(6, '{charId}', {bagSlot}) called");

                    System.Type loaderType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.UI.GameplayTransition.GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }
                    if (loaderType == null)
                    {
                        foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                        { var t = a.GetType("GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }
                    }
                    if (loaderType != null)
                    {
                        var loaderInst = loaderType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (loaderInst != null)
                        {
                            var beginLoad = loaderType.GetMethod("BeginGameplayLoad",
                                                new[] { typeof(int), typeof(object) })
                                         ?? loaderType.GetMethod("BeginGameplayLoad",
                                                new[] { typeof(int) });
                            if (beginLoad == null)
                            {
                                foreach (var m in loaderType.GetMethods(
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                    if (m.Name == "BeginGameplayLoad") { beginLoad = m; break; }
                            }
                            if (beginLoad != null)
                            {
                                var parameters = beginLoad.GetParameters();
                                object[] args = parameters.Length == 1
                                    ? new object[] { 6 }
                                    : new object[] { 6, null };
                                beginLoad.Invoke(loaderInst, args);
                                d.LogStep("  GameplaySceneLoader.BeginGameplayLoad(6) called — loading Hole 6");
                                gameplayLoadStarted = true;
                            }
                            else { d.LogStep("  WARN: BeginGameplayLoad method not found"); }
                        }
                        else { d.LogStep("  WARN: GameplaySceneLoader.Instance is null"); }
                    }
                    else { d.LogStep("  WARN: GameplaySceneLoader type not found"); }
                }
                else { d.LogStep("  WARN: GameSession type not found — falling back to ActionButton click"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: direct load reflection error: {ex.Message}"); }

            if (!gameplayLoadStarted)
            {
                d.LogStep("  Ultimate fallback: clicking first ActionButton");
                yield return d.Click("ActionButton", settleSeconds: 1.5f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.5f);
            }

            // 6. Wait for Hole 6 to load.
            yield return d.WaitForSceneLoaded("LabScaffold",  timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_06_Geo",  timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f); // fade-in + HUD settle; avoids Y-flip

            // 6a. ob_before: destroy the skirt MESH GO so the raw void shows (BEFORE = no skirt).
            //     ObGroundSkirt.Rebuild() creates a child GO named "[ObGroundSkirt]" (see ObGroundSkirt.cs:180).
            //     We find+destroy that child GO, NOT the component's owner (which is LabRoot — where
            //     PhysicsLabController lives). Destroying LabRoot breaks FireViaShotController (iter-9 bug).
            //     Destroying only the "[ObGroundSkirt]" mesh GO removes the visual while leaving
            //     PhysicsLabController intact.
            if (Scenario == "ob_before")
            {
                int destroyed = 0;
                // Strategy 1: find GO by exact name "[ObGroundSkirt]"
                var allGOs = UnityEngine.Object.FindObjectsByType<UnityEngine.GameObject>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                foreach (var go in allGOs)
                {
                    if (go.name == "[ObGroundSkirt]")
                    {
                        d.LogStep($"  ob_before: destroying skirt mesh GO '{go.name}' → void visible");
                        UnityEngine.Object.Destroy(go);
                        destroyed++;
                    }
                }
                // Strategy 2: disable the component so it doesn't re-build
                var skirts = UnityEngine.Object.FindObjectsByType<ObGroundSkirt>(
                    UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);
                foreach (var s in skirts)
                {
                    s.enabled = false;
                    d.LogStep($"  ob_before: disabled ObGroundSkirt component on '{s.gameObject.name}'");
                }
                if (destroyed == 0)
                    d.LogStep("  ob_before: no '[ObGroundSkirt]' mesh GO found (may not have built yet)");
                yield return null; // let Destroy() process this frame
            }

            // 6b. Re-tag chase camera to unique "ChaseCam" tag.
            //     ShellScene + LabScaffold both carry a "MainCamera"-tagged camera, so
            //     CameraInputSettings("MainCamera") is ambiguous → ambiguous-camera warning
            //     + Y-flipped frames (iter-9 scar).  Tag the actual ChaseCamera GO to the
            //     unique "ChaseCam" tag (registered in TagManager.asset) before calling
            //     BotVideoRecorder.Begin(), which reads BotVideoRecorder.CameraTag from
            //     SessionState.  With a unique tag, CameraInputSettings finds exactly one
            //     camera and injects its own RT → no Metal Y-flip.
            try
            {
                var cc = UnityEngine.Object.FindFirstObjectByType<ChaseCamera>();
                if (cc != null)
                {
                    cc.gameObject.tag = "ChaseCam";
                    d.LogStep($"  Re-tagged chase camera GO '{cc.gameObject.name}' → 'ChaseCam'");
                    if (Scenario == "ob_before")
                    {
                        _suppressCam = cc;
                        StartCoroutine(SuppressClampForever());
                        d.LogStep("  ob_before: clamp-suppressor started — camera == HEAD (follows ball past boundary, no hold)");
                    }
                }
                else { d.LogStep("  WARN: ChaseCamera component not found for re-tagging"); }
            }
            catch (System.Exception ex) { d.LogStep($"  WARN: chase camera re-tag error: {ex.Message}"); }

            // 7. START RECORDING — hole stable, no Y-flip risk.
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            try
            {
                System.Type recType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Physics.Viewer.Editor.BotVideoRecorder"); if (t != null) { recType = t; break; } }
                if (recType != null)
                {
                    var beginMethod = recType.GetMethod("Begin",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    beginMethod?.Invoke(null, null);
                    d.LogStep("  BeginDeferred: BotVideoRecorder.Begin() called — recording started");
                }
                else { d.LogStep("  BeginDeferred WARN: BotVideoRecorder type not found"); }
            }
            catch (System.Exception ex) { d.LogStep($"  BeginDeferred ERROR: {ex.Message}"); }
            yield return new WaitForSecondsRealtime(1f); // let first frames settle

            // 8. Capture pre-shot frame for the report.
            // Capture prefix: distinguish BEFORE / CONTROL / AFTER stills in screenshots folder.
            //   ob_after  → "ob"     (matches existing s01-s10 AFTER stills, no re-capture needed)
            //   ob_before → "before" (camera follows ball into void, no skirt)
            //   ob_control→ "ctrl"   (normal inbounds, no clamp fires)
            string cap = Scenario == "ob_before"  ? "before"
                       : Scenario == "ob_control" ? "ctrl"
                       : "ob";

            yield return d.Capture($"{cap}_pre_shot");

            // 9. Fire Driver: aimYaw=0 → launchDir=(+1,0,0) for all scenarios (iter-8 fix).
            //    ob_after:   power=0.236 → first bounce X≈99.84 (inside narrow OOB strip X≈99.7–100.0).
            //                BakedZoneClassifier → SurfaceType.OOB → TryFindFirstOBHit → clamp arms.
            //                NOTE: architect assumed OB at X=108-113; actual zone is CartPath there.
            //                True OOB in +X at Z=-24.54 is only the thin strip X≈99.7–100.0.
            //    ob_before:  power=0.85 → carry≈349 units → past mask (X>114.45) → unmapped Fairway.
            //                No OOB hit → clamp does NOT arm → camera follows ball past course edge.
            //    ob_control: power=0.15 → carry≈8 units → X≈89 (tee area, well inside course).
            var ctrl = UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();
            if (ctrl == null)
            {
                d.LogStep("  FAIL: PhysicsLabController not found after Hole_06_Geo load");
                yield break;
            }

            ctrl.SetClub(0); // Driver = index 0
            ctrl.InjectLabBundleForCurrentClub();
            ctrl.SetCameraYawRadians(aimYaw);
            d.LogStep($"  Firing Driver: aimYaw={aimYaw} rad, power={power01:F2}");
            ctrl.FireViaShotController(power01, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // 9b. Log pre-computed trajectory (HandleShotResolved runs synchronously at fire time).
            //     Wait one frame to let the SM transition (Aiming→Flying) complete before reading.
            yield return null;
            {
                var traj = ctrl.LastTrajectory;
                if (traj?.terrainHits != null && traj.terrainHits.Count > 0)
                {
                    d.LogStep($"  Trajectory: {traj.terrainHits.Count} terrain hit(s)");
                    for (int i = 0; i < traj.terrainHits.Count; i++)
                    {
                        var hit = traj.terrainHits[i];
                        d.LogStep($"  TerrainHit[{i}]: surface={hit.Surface} " +
                                  $"t={hit.Time.ToFloat():F3}s " +
                                  $"pos=({hit.Position.x.ToFloat():F2},{hit.Position.y.ToFloat():F2}" +
                                  $",{hit.Position.z.ToFloat():F2}) isStop={hit.IsStop}");
                    }
                }
                else d.LogStep("  Trajectory: terrainHits null or empty");
                d.LogStep($"  LaunchDir: {ctrl.LastShotLaunchDir}");
            }

            // 10. Timed captures during flight + OB landing window.
            //     Pre-computed trajectory: OOB at t=1.155s pos=(106.96,13.72,-24.54).
            //     The camera clamp fires when real-time ball projected X > 106.96 along LaunchDir.
            //     Must capture DURING the flight window; the OB penalty reset happens within
            //     ~2-4s after landing (making post-poll captures show the re-tee position).
            //
            //     ITER-8 timing analysis (console timestamps):
            //       Shot fires at actual clock +0.000s
            //       s02 captured at actual +0.43s  → BallPos.x≈91 (flying, 16 m before OB)
            //       OB fires + ball RESETS at actual +0.94s  (BallStateMachine.DrainPendingTransitions)
            //       s03 captured at actual +1.23s  → BallPos.x≈80 (already reset — WRONG)
            //
            //     Critical window: +0.43s → +0.94s (0.51 s gap, NO captures previously).
            //     Fix: 5 dense captures at +0.15 s intervals fill the window.
            //       At actual +0.65s ball is at X≈97, camera chasing
            //       At actual +0.80s ball is at X≈102, camera near/at clamp (X≈107)
            //       At actual +0.90s ball is at X≈105, camera AT/past clamp — CANONICAL
            //
            //     The ITER-8 dense series (s03–s07) replaces the old sparse t+1.0s/t+1.3s pair.

            yield return new WaitForSecondsRealtime(0.2f);
            yield return d.Capture($"{cap}_early_flight"); // t+0.2s: ball X≈91, camera chasing
            d.LogStep($"  [+0.20s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");

            // Dense series — 5 captures at 0.15 s intervals covering the critical window.
            yield return new WaitForSecondsRealtime(0.15f);
            d.LogStep($"  [+0.35s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_t035"); // ball X≈95

            yield return new WaitForSecondsRealtime(0.15f);
            d.LogStep($"  [+0.50s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_t050"); // ball X≈98

            yield return new WaitForSecondsRealtime(0.15f);
            d.LogStep($"  [+0.65s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_t065"); // ball X≈101

            yield return new WaitForSecondsRealtime(0.15f);
            d.LogStep($"  [+0.80s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_t080"); // ball X≈104, camera near clamp

            yield return new WaitForSecondsRealtime(0.10f);
            d.LogStep($"  [+0.90s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_approaching"); // ball X≈105–107, camera AT clamp ← CANONICAL

            // t+1.1s: OB has fired by now; ball at drop zone; OBFreeze camera mode active.
            yield return new WaitForSecondsRealtime(0.2f);
            d.LogStep($"  [+1.10s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_freeze_moment"); // OBFreeze: camera at pivot, looking at drop

            // t+2.0s: ball settling / OB penalty showing
            yield return new WaitForSecondsRealtime(0.7f);
            d.LogStep($"  [+2.0s] BallPos={ctrl.BallPosition} BallState={ctrl.BallSM?.State}");
            yield return d.Capture($"{cap}_skirt_settled");

            // 11. Poll for BallState.OB (diagnostic — verifies OB state fires eventually).
            //     Note: poll runs AFTER canonical captures to avoid delaying the capture window.
            float pollStart = UnityEngine.Time.realtimeSinceStartup;
            var   ballSM    = ctrl.BallSM;
            bool  obFired   = false;
            while (UnityEngine.Time.realtimeSinceStartup - pollStart < 5f)
            {
                if (ballSM != null && ballSM.State == Golfin.Gameplay.Loop.BallState.OB)
                { obFired = true; break; }
                // Refresh ballSM reference in case ball was re-instantiated after OB reset.
                ballSM = ctrl.BallSM;
                yield return new WaitForSecondsRealtime(0.033f);
            }
            float elapsed = UnityEngine.Time.realtimeSinceStartup - pollStart;
            d.LogStep($"  OBState detected={obFired} after +{elapsed:F2}s | BallPos={ctrl.BallPosition}");

            yield return new WaitForSecondsRealtime(2.5f);
            yield return d.Capture($"{cap}_final_void");

            d.LogStep($"=== OBShot done: scenario='{Scenario}' ===");
        }

        // ── RunWithCatch helper ────────────────────────────────────────────────
        // Mirrors LoopV2SmokeBot.RunWithCatch exactly. C# cannot use try/catch
        // inside an iterator that yields, so the inner enumerator is driven manually.

        IEnumerator RunWithCatch(IEnumerator inner,
            System.Action onDone, System.Action<System.Exception> onError)
        {
            while (true)
            {
                bool hasNext;
                try { hasNext = inner.MoveNext(); }
                catch (System.Exception ex) { onError(ex); yield break; }
                if (!hasNext) break;
                yield return inner.Current;
            }
            onDone();
        }
    }
}
#endif
