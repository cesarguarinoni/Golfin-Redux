#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// K10 ob_recovery_fixes — real-play video repro of the shot AFTER a boundary OB.
    ///
    /// Drives the REAL entry path (ShellScene → Practice → HoleSelection → BeginGameplayLoad(6))
    /// then fires a Driver boundary OB (aimYaw=0 / +X, power=0.50 → ball flies past the mask edge
    /// X=+114.45 → ExitedWorldBounds → OBReason != Water → boundary OB). Records continuously
    /// through the whole recovery so all three device symptoms are visible in one clip:
    ///
    ///   1. Drop rule (Part B): boundary OB is stroke-and-distance → ball re-tees (drops back at
    ///      the previous shot origin), NOT at the last in-bounds hit.
    ///   2. Aim direction (Part A): the aim phase after the drop frames the ball from behind,
    ///      looking DOWN the fairway toward the pin — not backwards toward the tee.
    ///   3. Draggable (Part A): SimulateOrbitDragDegrees drives the PRODUCTION orbit write
    ///      (incl. the Chase-mode gate) — it applies (camera pans) only because the mode exited
    ///      OBFreeze → Chase on re-arm. On HEAD it would be gated out (mode stuck OBFreeze) and
    ///      the camera would stay wedged looking back at the tee.
    ///
    /// Hole 6 is used because its boundary-OB geometry is already tuned (ObBoundaryCaptureBot
    /// ob_after). The fix is hole-agnostic (camera lifecycle + drop rule), so Hole 6 exercises
    /// the exact same code path as the Hole 1 device smoke.
    ///
    /// Launched exclusively by ObRecoveryCaptureMenu. Recording start is deferred until the hole
    /// is stable (Y-flip-safe, ChaseCam TaggedCamera) exactly like ObBoundaryCaptureBot.
    /// BotVideoRecorder.End() is handled unconditionally by LoopV2SmokeBotMenu.ExitingPlayMode.
    /// </summary>
    public class ObRecoveryCaptureBot : MonoBehaviour
    {
        const string ArmedKey    = "ObRecoveryCapture.Armed";
        const float  StartupWait = 5f;

        public static bool Armed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        void Start()
        {
            if (!Armed)
            {
                Debug.LogWarning("[ObRecoveryCaptureBot] Not armed — destroying self.");
                Destroy(gameObject);
                return;
            }
            Armed = false; // consume

            if (Time.timeScale < 0.01f) Time.timeScale = 1f;
            StartCoroutine(SafeRun());
        }

        IEnumerator SafeRun()
        {
            yield return new WaitForSecondsRealtime(StartupWait);

            var driver = new Bot.BotDriver(
                "Docs/Specs/Active/ob_recovery_fixes/screenshots");
            driver.LogStep("=== ObRecoveryCaptureBot: boundary-OB recovery (Hole 6) ===");

            bool             done   = false;
            System.Exception caught = null;
            var inner = OBRecoveryShot(driver);
            yield return StartCoroutine(RunWithCatch(inner, () => done = true, ex => caught = ex));

            if (caught != null) driver.LogStep($"EXCEPTION: {caught}");
            driver.LogStep(done ? "=== recovery shot complete ===" : "=== recovery shot INCOMPLETE ===");
            driver.FlushLog("ob_recovery_capture.log");

            Debug.Log("[ObRecoveryCaptureBot] Done. Exiting play mode.");
            Destroy(gameObject);
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        IEnumerator OBRecoveryShot(Bot.BotDriver d)
        {
            // ── 1. Real boot → Home ────────────────────────────────────────────
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // ── 2. Unlock Hole 6 (reflection) ──────────────────────────────────
            TryReflect(() =>
            {
                var svcType = FindType("GolfinRedux.UI.HoleSelection.HoleProgressionService")
                           ?? FindType("HoleProgressionService");
                if (svcType == null) { d.LogStep("  WARN: HoleProgressionService not found"); return; }
                var inst = svcType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                svcType.GetMethod("SetUnlockedOverride", new[] { typeof(int), typeof(bool) })
                       ?.Invoke(inst, new object[] { 6, true });
                d.LogStep("  Hole 6 unlocked");
            }, d, "unlock");

            // ── 3. Practice → HoleSelection ────────────────────────────────────
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f);

            // ── 4. Tap Hole 6 card ─────────────────────────────────────────────
            TryReflect(() =>
            {
                var cardType = FindType("GolfinRedux.UI.HoleSelection.HoleCardController")
                            ?? FindType("HoleCardController");
                if (cardType == null) { d.LogStep("  WARN: HoleCardController not found"); return; }
                var holeNumProp = cardType.GetProperty("HoleNumber");
                foreach (var card in UnityEngine.Object.FindObjectsByType(cardType, UnityEngine.FindObjectsSortMode.None))
                {
                    if ((int)(holeNumProp?.GetValue(card) ?? 0) != 6) continue;
                    var go = ((UnityEngine.Component)card).gameObject;
                    UnityEngine.UI.Button tap = null;
                    foreach (var b in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        if (b.gameObject.name.Contains("CardTapButton") || b.gameObject.name.Contains("TapButton")) { tap = b; break; }
                    if (tap == null) tap = go.GetComponentInChildren<UnityEngine.UI.Button>();
                    if (tap != null) { tap.onClick.Invoke(); d.LogStep("  Tapped Hole 6 card"); }
                    break;
                }
            }, d, "card tap");
            yield return new WaitForSecondsRealtime(1.5f);

            // ── 5. Seed + BeginGameplayLoad(6) ─────────────────────────────────
            bool loadStarted = false;
            TryReflect(() =>
            {
                var gsType = FindType("Golfin.Gameplay.Session.GameSession") ?? FindType("GameSession");
                if (gsType == null) { d.LogStep("  WARN: GameSession not found"); return; }
                gsType.GetProperty("IsVersus",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.SetValue(null, false);

                string charId = "";
                var cmType = FindType("CharacterManager");
                if (cmType != null)
                {
                    var cmInst = cmType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (cmInst != null)
                        charId = (string)(cmType.GetMethod("GetSelectedCharacterId")?.Invoke(cmInst, null) ?? "");
                }
                int bagSlot = 0;
                var bmType = FindType("BagManager");
                if (bmType != null)
                {
                    var bmInst = bmType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (bmInst != null)
                        bagSlot = (int)(bmType.GetProperty("EquippedBagSlot")?.GetValue(bmInst) ?? 0);
                }
                gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                      ?.Invoke(null, new object[] { 6, charId, bagSlot });

                var loaderType = FindType("Golfin.UI.GameplayTransition.GameplaySceneLoader") ?? FindType("GameplaySceneLoader");
                if (loaderType == null) { d.LogStep("  WARN: GameplaySceneLoader not found"); return; }
                var loaderInst = loaderType.GetProperty("Instance",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                System.Reflection.MethodInfo begin = null;
                foreach (var m in loaderType.GetMethods()) if (m.Name == "BeginGameplayLoad") { begin = m; break; }
                if (begin != null && loaderInst != null)
                {
                    var pars = begin.GetParameters();
                    begin.Invoke(loaderInst, pars.Length == 1 ? new object[] { 6 } : new object[] { 6, null });
                    d.LogStep("  BeginGameplayLoad(6) called");
                    loadStarted = true;
                }
            }, d, "seed+load");

            if (!loadStarted) { yield return d.Click("ActionButton", settleSeconds: 1.5f); }
            else              { yield return new WaitForSecondsRealtime(1.5f); }

            // ── 6. Wait for Hole 6 loaded + settle (avoid Y-flip) ──────────────
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded("Hole_06_Geo", timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(4f);

            // ── 6b. Re-tag ChaseCamera → "ChaseCam" (unique, Y-flip-safe) ──────
            ChaseCamera cc  = UnityEngine.Object.FindFirstObjectByType<ChaseCamera>();
            Camera      cam = cc != null ? cc.GetComponent<Camera>() : null;
            if (cc != null) { cc.gameObject.tag = "ChaseCam"; d.LogStep("  Re-tagged ChaseCamera → 'ChaseCam'"); }
            else d.LogStep("  WARN: ChaseCamera not found for retag");

            // ── 7. START RECORDING (hole stable) ───────────────────────────────
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.RecordVideo", true);
            UnityEditor.SessionState.SetBool("LoopV2SmokeBot.DeferredRecord", false);
            TryReflect(() =>
            {
                var recType = FindType("Golfin.Physics.Viewer.Editor.BotVideoRecorder");
                recType?.GetMethod("Begin",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
                d.LogStep("  BotVideoRecorder.Begin() — recording started");
            }, d, "record begin");
            yield return new WaitForSecondsRealtime(1f);

            var ctrl = UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();
            if (ctrl == null) { d.LogStep("  FAIL: PhysicsLabController not found"); yield break; }

            yield return d.Capture("recov_00_pre_shot");

            // ── 8. Fire the boundary OB (aimYaw=0 / +X, power=0.50) ────────────
            ctrl.SetClub(0);                       // Driver
            ctrl.InjectLabBundleForCurrentClub();
            ctrl.SetCameraYawRadians(0f);          // +X toward the mask edge
            d.LogStep("  Firing Driver: aimYaw=0, power=0.50 (boundary OB → ExitedWorldBounds)");
            ctrl.FireViaShotController(0.50f, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            // Flight window.
            yield return new WaitForSecondsRealtime(0.4f);
            yield return d.Capture("recov_01_flight");

            // ── 9. Poll for OB ─────────────────────────────────────────────────
            float t0 = Time.realtimeSinceStartup;
            bool obFired = false;
            while (Time.realtimeSinceStartup - t0 < 6f)
            {
                var sm = ctrl.BallSM;
                if (sm != null && sm.State == Golfin.Gameplay.Loop.BallState.OB) { obFired = true; break; }
                yield return new WaitForSecondsRealtime(0.033f);
            }
            Vector3 camAtOB = cam != null ? cam.transform.position : Vector3.zero;
            d.LogStep($"  OB fired={obFired} at +{Time.realtimeSinceStartup - t0:F2}s | mode={(cc!=null?cc.CurrentMode.ToString():"?")} camPos={camAtOB:F1}");
            d.LogStep($"    (K10 follow-up proof: mode must be Chase — NOT OBFreeze — and the camera must" +
                      $" hold this position instead of cutting to an aerial pivot)");
            yield return d.Capture("recov_02_ob_freeze");

            // Hold through the OB dwell and log camera drift: "stops chasing" means the
            // transform must stay put (no top-down teleport) for the whole OB beat.
            for (int k = 1; k <= 4; k++)
            {
                yield return new WaitForSecondsRealtime(0.4f);
                Vector3 now = cam != null ? cam.transform.position : Vector3.zero;
                d.LogStep($"    [OB dwell +{k * 0.4f:F1}s] mode={(cc != null ? cc.CurrentMode.ToString() : "?")} " +
                          $"camPos={now:F1} driftFromOB={Vector3.Distance(now, camAtOB):F2}m");
            }
            yield return d.Capture("recov_02b_ob_hold");

            // ── 10. Wait for the re-tee (BoundaryOBHold 2.0s → Reposition → ReArm → Aiming) ──
            float t1 = Time.realtimeSinceStartup;
            bool reAimed = false;
            while (Time.realtimeSinceStartup - t1 < 8f)
            {
                var sm = ctrl.BallSM;
                if (sm != null && sm.State == Golfin.Gameplay.Loop.BallState.Aiming) { reAimed = true; break; }
                yield return new WaitForSecondsRealtime(0.05f);
            }
            yield return new WaitForSecondsRealtime(0.3f); // let RepositionBallWithLookDir settle the camera
            Vector3 ballPos = ctrl.BallPosition;
            d.LogStep($"  RE-TEE: reachedAiming={reAimed} ballPos={ballPos:F2} " +
                      $"mode={(cc!=null?cc.CurrentMode.ToString():"?")} camPos={(cam!=null?cam.transform.position.ToString("F1"):"?")}");
            d.LogStep($"    (Part A proof: mode == Chase here means OBFreeze was exited on re-arm; on HEAD it stays OBFreeze)");
            yield return d.Capture("recov_03_reteed_aim");

            // ── 11. DRAG PROOF — pan the camera through the production orbit path ──
            // SimulateOrbitDragDegrees applies only when mode == Chase (the exact gate
            // HandleCameraOrbit uses), so a visible pan = symptom 3 fixed.
            bool everApplied = false;
            float dur = 0f;
            // Sweep: right 1.2s, left 2.4s, right 1.2s → ends near where it started.
            while (dur < 4.8f)
            {
                float dir = (dur < 1.2f) ? +1f : (dur < 3.6f) ? -1f : +1f;
                bool applied = ctrl.SimulateOrbitDragDegrees(dir * 0.9f);
                everApplied |= applied;
                dur += Time.unscaledDeltaTime;
                if (Mathf.FloorToInt(dur / 0.6f) != Mathf.FloorToInt((dur - Time.unscaledDeltaTime) / 0.6f))
                {
                    d.LogStep($"    [drag +{dur:F2}s] applied={applied} mode={(cc!=null?cc.CurrentMode.ToString():"?")} camPos={(cam!=null?cam.transform.position.ToString("F1"):"?")}");
                    if (dur > 1.0f && dur < 1.7f)  yield return d.Capture("recov_04_drag_right");
                    if (dur > 2.6f && dur < 3.3f)  yield return d.Capture("recov_05_drag_left");
                }
                yield return null;
            }
            d.LogStep($"  DRAG PROOF: everApplied={everApplied}  (true = aim phase was DRAGGABLE = symptoms 2+3 fixed; false = wedged/HEAD)");
            yield return d.Capture("recov_06_final_aim");
            yield return new WaitForSecondsRealtime(1f);

            d.LogStep("=== ObRecoveryCaptureBot done ===");
        }

        // ── helpers ────────────────────────────────────────────────────────────

        static System.Type FindType(string fullName)
        {
            foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(fullName); if (t != null) return t; }
            return null;
        }

        static void TryReflect(System.Action body, Bot.BotDriver d, string label)
        {
            try { body(); }
            catch (System.Exception ex) { d.LogStep($"  WARN: {label} reflection error: {ex.Message}"); }
        }

        IEnumerator RunWithCatch(IEnumerator inner, System.Action onDone, System.Action<System.Exception> onError)
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
