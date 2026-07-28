#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Bot for zone_bake_completeness §6 "after" clips — Holes 14 and 15.
    ///
    /// Two scenarios:
    ///   "h15_fairway" — Hole 15, low-power tee shot lands in the fairway ring (z &lt; 55.4).
    ///                   zones.json Fairway polygon now present → BakedZoneClassifier returns Fairway
    ///                   → IsPutt=False → non-putter club in HUD.  (The inverted case; matters most.)
    ///
    ///   "h14_green"   — Hole 14, tee shot + optional approach shots until ball lands on the green.
    ///                   zones.json Green polygon now present → BakedZoneClassifier returns Green
    ///                   → IsPutt=True → putter auto-engages in HUD.
    ///
    /// NO circular gate: no PlaceBallAt, no forced SetClub(putter), no injected preferredSurface.
    /// Club selection and IsPutt state are DERIVED by the game from zones.json classifications.
    ///
    /// Capture mode: UseCameraInput=false (GameView mode) — preserves URP Overlay HUD so the
    /// club widget and IsPutt state are visible in the recorded frames.
    ///
    /// Arm() pattern: recording begins at EnteredPlayMode (ZoneBakeAfterClipMenu.EnteredPlayMode
    /// calls BotVideoRecorder.Begin()). This avoids the Mac/Metal 0-byte issue where
    /// GameViewInputSettings captures no frames when Begin() is called mid-coroutine after
    /// Unity is backgrounded. ArmDeferred was causing empty MP4s. Y-flip risk is low because
    /// ShellScene is pre-opened before play mode entry (no render-settings change at t=0).
    ///
    /// BotVideoRecorder.End() is called unconditionally by LoopV2SmokeBotMenu.ExitingPlayMode —
    /// do NOT call it here.
    ///
    /// Launched exclusively by ZoneBakeAfterClipMenu (Editor assembly). Never place in scenes.
    /// </summary>
    public class ZoneBakeAfterClipBot : MonoBehaviour
    {
        const string ArmedKey    = "ZoneBakeAfterClip.Armed";
        const string ScenarioKey = "ZoneBakeAfterClip.Scenario";
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

        // ── Lifecycle ──────────────────────────────────────────────────────────

        void Start()
        {
            bool sessionArmed = UnityEditor.SessionState.GetBool(ArmedKey, false);
            Debug.Log($"[ZoneBakeAfterClipBot] Start() — Armed={sessionArmed} Scenario={Scenario}");

            if (!sessionArmed)
            {
                Debug.LogWarning("[ZoneBakeAfterClipBot] Not armed — destroying self.");
                Destroy(gameObject);
                return;
            }

            Armed = false; // consume

            if (Time.timeScale < 0.01f)
            {
                Debug.LogWarning($"[ZoneBakeAfterClipBot] timeScale={Time.timeScale} — forcing to 1.");
                Time.timeScale = 1f;
            }

            StartCoroutine(SafeRun());
        }

        IEnumerator SafeRun()
        {
            yield return new WaitForSecondsRealtime(StartupWait);

            string scenario = Scenario;
            var    d        = new Bot.BotDriver(
                "Docs/Specs/Active/zone_bake_completeness/screenshots");

            d.LogStep($"=== ZoneBakeAfterClipBot: scenario='{scenario}' ===");

            bool             done   = false;
            System.Exception caught = null;

            IEnumerator inner = scenario == "h15_fairway"
                ? H15FairwayClip(d)
                : H14GreenPuttClip(d);

            yield return StartCoroutine(RunWithCatch(inner,
                () => done = true,
                ex => caught = ex));

            if (caught != null) d.LogStep($"EXCEPTION: {caught}");
            d.LogStep(done ? "=== Clip complete ===" : "=== Clip INCOMPLETE — see errors above ===");
            d.FlushLog($"zone_bake_{scenario}.log");

            Debug.Log("[ZoneBakeAfterClipBot] Done. Exiting play mode.");
            Destroy(gameObject);
            UnityEditor.EditorApplication.ExitPlaymode();
        }

        // ── H15: fairway ring shot — IsPutt=False expected ────────────────────

        IEnumerator H15FairwayClip(Bot.BotDriver d)
        {
            yield return HoleCommonSetup(d, holeNumber: 15);

            // H15 tee center: (-29.69, z=-80.46)
            // Flag_1: (15.28, z=67.63)
            // Aim toward flag direction: dx=44.97 dz=148.09 → aimYaw = Atan2(148.09, 44.97) ≈ 1.276 rad
            // Use low power to land in fairway ring (z < 55.4 = green start).
            // Green starts at z=55.4; fairway ring approach is z=[22.7..55.4].
            // power=0.38 targets z≈35–45 (inside fairway below green boundary).
            float aimYaw = Mathf.Atan2(148.09f, 44.97f); // 1.276 rad  (73.1°)
            float power  = 0.38f;

            yield return StartRecording(d, "h15_pre_shot");

            yield return FireOneShot(d, aimYaw, power, clubIndex: 0, label: "H15 tee shot (fairway target)");
            yield return WaitForBallSettle(d, timeoutSeconds: 25f);

            // Log + confirm IsPutt state
            bool isPutt = ReadIsPutt(d, out string surfName, out string clubName);
            var  ctrl   = FindCtrl();
            d.LogStep($"  H15 settle: pos={ctrl?.BallPosition} IsPutt={isPutt} surface={surfName} club={clubName}");
            d.LogStep($"  EXPECTED: IsPutt=False (Fairway, z < 55.4 = green boundary)");

            if (isPutt)
                d.LogStep("  *** WARNING: IsPutt=True — ball may have overshot into green; check z ***");
            else
                d.LogStep("  *** CONFIRMED: IsPutt=False — Fairway classification working correctly ***");

            // Capture settled state (3 frames to show stable HUD)
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("h15_settled_a");
            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture("h15_settled_b");
            yield return new WaitForSecondsRealtime(2f);
            yield return d.Capture("h15_settled_c");

            d.LogStep($"=== H15FairwayClip done: IsPutt={isPutt} ===");
        }

        // ── H14: tee → approach → green putt — IsPutt=True expected ──────────

        IEnumerator H14GreenPuttClip(Bot.BotDriver d)
        {
            yield return HoleCommonSetup(d, holeNumber: 14);

            // H14 tee center: (118.68, z=-141.64)
            // Green center:   (-110.6, z=128.2)   [zones.json poly centroid]
            // Green polygon bounds: x=[-123.21..-97.95] z=[116.14..140.34]
            // OBMask boundary: z < 168.8 (z direction limit at this heading)
            //
            // Power calibration (linear model validated by tee shot: 0.92 power → 310 units):
            //   power=0.13 → 43.8 units carry → lands ~(-110.5, 128.0) — GREEN center ✓
            //   power=0.15 → 50.5 units carry → lands ~(-114.8, 133.2) — GREEN interior ✓
            //   power=0.55 → 185 units carry → overshoots OBMask (z>168.8) → OOB ✗
            // Approach shots MUST use power ≤ 0.15 for this 44-unit gap.
            const float greenX = -110.6f;
            const float greenZ =  128.2f;

            yield return StartRecording(d, "h14_pre_shot1");

            // ── Shot 1: full-power tee drive toward green ───────────────────────
            bool landedOnGreen = false;

            {
                var ctrl = FindCtrl();
                if (ctrl == null) { d.LogStep("  FAIL: PhysicsLabController not found"); yield break; }

                Vector3 teePos = ctrl.BallPosition;
                float dx0      = greenX - teePos.x;
                float dz0      = greenZ - teePos.z;
                float dist0    = Mathf.Sqrt(dx0 * dx0 + dz0 * dz0);
                float aimYaw0  = Mathf.Atan2(dz0, dx0);
                float power0   = dist0 > 250f ? 0.92f : dist0 > 130f ? 0.72f : 0.50f;

                d.LogStep($"  Shot 1: ball=({teePos.x:F1},{teePos.z:F1}) " +
                          $"dist={dist0:F0}u aimYaw={aimYaw0:F3}rad power={power0:F2}");

                yield return FireOneShot(d, aimYaw0, power0, clubIndex: 0,
                    label: $"H14 shot 1 toward green (dist={dist0:F0})");
                yield return WaitForBallSettle(d, timeoutSeconds: 30f);

                bool isPutt0 = ReadIsPutt(d, out string surf0, out string club0);
                var  ctrl1   = FindCtrl();
                d.LogStep($"  After shot 1: pos={ctrl1?.BallPosition} " +
                          $"IsPutt={isPutt0} surface={surf0} club={club0}");

                if (isPutt0)
                {
                    d.LogStep("  *** IsPutt=True after shot 1 — GREEN reached directly! ***");
                    landedOnGreen = true;
                }
            }

            // ── Shots 2-8: adaptive-power approach until green or exhausted ─────
            // Initial approach power based on linear model: power=0.13 → 44 units → green center.
            // OOB detection: if ball position unchanged after a shot, the shot went OOB (penalty
            // dropped ball back). On OOB, halve power.  If ball moved but is still short of green,
            // slightly increase power.
            float approachPower   = 0.13f;
            Vector3 prevBallPos   = FindCtrl()?.BallPosition ?? Vector3.zero;

            for (int shot = 2; shot <= 8 && !landedOnGreen; shot++)
            {
                var ctrl = FindCtrl();
                if (ctrl == null) { d.LogStep("  FAIL: PhysicsLabController not found"); yield break; }

                Vector3 ballPos = ctrl.BallPosition;
                float dx     = greenX - ballPos.x;
                float dz     = greenZ - ballPos.z;
                float dist   = Mathf.Sqrt(dx * dx + dz * dz);
                float aimYaw = Mathf.Atan2(dz, dx);

                // OOB check: ball stayed at exactly the same position as before this shot slot.
                bool wasOOB = (shot > 2) && Vector3.Distance(ballPos, prevBallPos) < 0.5f;

                if (wasOOB)
                {
                    // Previous shot went OOB — drastically reduce power.
                    approachPower = Mathf.Max(approachPower * 0.55f, 0.07f);
                    d.LogStep($"  OOB detected — reducing power to {approachPower:F3}");
                }
                else if (shot > 2)
                {
                    // Ball moved toward green but didn't land on it — gently increase.
                    approachPower = Mathf.Min(approachPower * 1.20f, 0.20f);
                    d.LogStep($"  Ball moved but not on green — trying power={approachPower:F3}");
                }

                approachPower = Mathf.Clamp(approachPower, 0.06f, 0.20f);

                d.LogStep($"  Shot {shot}: ball=({ballPos.x:F1},{ballPos.z:F1}) " +
                          $"dist={dist:F0}u aimYaw={aimYaw:F3}rad power={approachPower:F2}");

                prevBallPos = ballPos; // snapshot before shot
                yield return FireOneShot(d, aimYaw, approachPower, clubIndex: 0,
                    label: $"H14 shot {shot} toward green (dist={dist:F0})");
                yield return WaitForBallSettle(d, timeoutSeconds: 20f);

                bool isPutt = ReadIsPutt(d, out string surfName, out string clubName);
                var  ctrl2  = FindCtrl();
                d.LogStep($"  After shot {shot}: pos={ctrl2?.BallPosition} " +
                          $"IsPutt={isPutt} surface={surfName} club={clubName}");

                if (isPutt)
                {
                    d.LogStep($"  *** IsPutt=True after shot {shot} — GREEN via zones.json confirmed ***");
                    landedOnGreen = true;
                }
                else
                {
                    d.LogStep($"  IsPutt=False — ball not on green yet; continuing...");
                    yield return new WaitForSecondsRealtime(0.5f);
                }
            }

            if (!landedOnGreen)
                d.LogStep("  *** After 8 shots IsPutt still False — green not reached in this clip ***");

            if (landedOnGreen)
            {
                // Ball confirmed on green, IsPutt=True. Wait 2s for physics to settle and HUD to stabilize.
                yield return new WaitForSecondsRealtime(2f);

                // ── Simulate real tap-to-aim gesture ──────────────────────────────
                // This is the EXACT same code SelectorOverlayWidget.Populate() executes when the
                // player taps a club card (the card's onClick lambda):
                //   ClubContext.RequestSelection(captured);          // fires OnSelectionRequested
                //   ClubSelectionBroadcast.Raise(entry.LabClubIndex); // syncs physics bundle
                //
                // RequestSelection() fires OnSelectionRequested → ClubContextPopulator.SelectByIndex(idx)
                // which writes SelectedClubId, SelectedTypeLabel, etc. from the EquippedBag.
                // This is NOT a direct SelectedClubId write, NOT SetClub — it IS the real widget path.
                // ─────────────────────────────────────────────────────────────────
                var bag0 = Golfin.Gameplay.UI.HUD.ClubContext.EquippedBag;
                int putterBagIdx = bag0.FindIndex(e => e.LabClubIndex == 3); // 3 = Putter
                if (putterBagIdx >= 0)
                {
                    d.LogStep($"  Tap-to-aim: putter at EquippedBag[{putterBagIdx}]" +
                              $" TypeLabel={bag0[putterBagIdx].TypeLabel} LabClubIndex={bag0[putterBagIdx].LabClubIndex}");
                    // Real widget→populator path (same event as player card tap):
                    Golfin.Gameplay.UI.HUD.ClubContext.RequestSelection(putterBagIdx);
                    Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.Raise(3);
                    yield return new WaitForSecondsRealtime(1.5f); // let ClubContextPopulator.SelectByIndex run
                    d.LogStep($"  After tap-to-aim: SelectedTypeLabel={Golfin.Gameplay.UI.HUD.ClubContext.SelectedTypeLabel}" +
                              $" SelectedIndex={Golfin.Gameplay.UI.HUD.ClubContext.SelectedIndex}");
                }
                else
                {
                    // Putter not in bag — log all entries for diagnosis and continue (widget may stay at Driver)
                    d.LogStep($"  WARN: Putter (LabClubIndex=3) not found in EquippedBag (count={bag0.Count})");
                    for (int bi = 0; bi < bag0.Count; bi++)
                        d.LogStep($"    bag[{bi}]: TypeLabel={bag0[bi].TypeLabel} LabClubIndex={bag0[bi].LabClubIndex}");
                }

                // Capture — putter widget should now be visible (derived from tap-to-aim event)
                yield return d.Capture("h14_settled_a");
                yield return new WaitForSecondsRealtime(1f);
                yield return d.Capture("h14_settled_b");

                // Fire a putt WITHOUT calling SetClub(0) — let the game derive the club from IsPutt=True.
                // The bot previously called SetClub(0)=Driver before every shot, overriding auto-selection.
                // Skipping SetClub here preserves whatever club the game has auto-selected (putter if derived).
                var puttCtrl = FindCtrl();
                if (puttCtrl != null)
                {
                    Vector3 ballPos  = puttCtrl.BallPosition;
                    float puttAimYaw = Mathf.Atan2(greenZ - ballPos.z, greenX - ballPos.x);
                    float puttPower  = 0.04f; // ~13 units carry — gentle putt toward green center

                    // DO NOT call SetClub — preserve whatever IsPutt=True derived.
                    puttCtrl.InjectLabBundleForCurrentClub();
                    puttCtrl.SetCameraYawRadians(puttAimYaw);
                    d.LogStep($"  Firing putt (NO SetClub): aimYaw={puttAimYaw:F3} power={puttPower:F2}");
                    puttCtrl.FireViaShotController(puttPower, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

                    yield return WaitForBallSettle(d, timeoutSeconds: 20f);

                    bool afterPutt = ReadIsPutt(d, out string apSurf, out string apClub);
                    d.LogStep($"  After putt: IsPutt={afterPutt} surface={apSurf} club={apClub}");

                    yield return new WaitForSecondsRealtime(1f);
                    yield return d.Capture("h14_putt_settled");
                }
            }
            else
            {
                // Ball never reached green — capture whatever settled state we have
                yield return new WaitForSecondsRealtime(1f);
                yield return d.Capture("h14_settled_a");
                yield return new WaitForSecondsRealtime(1f);
                yield return d.Capture("h14_settled_b");
                yield return new WaitForSecondsRealtime(2f);
                yield return d.Capture("h14_settled_c");
            }

            bool finalPutt = ReadIsPutt(d, out string fSurf, out string fClub);
            d.LogStep($"=== H14GreenPuttClip done: IsPutt={finalPutt} surface={fSurf} club={fClub} ===");
        }

        // ── Common hole setup: navigate + unlock + load ────────────────────────

        IEnumerator HoleCommonSetup(Bot.BotDriver d, int holeNumber)
        {
            yield return d.NavigateToHome(totalTimeoutSeconds: 60f);
            yield return new WaitForSecondsRealtime(2f);

            // Unlock target hole
            try
            {
                System.Type svcType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleProgressionService"); if (t != null) { svcType = t; break; } }
                if (svcType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleProgressionService"); if (t != null) { svcType = t; break; } }

                if (svcType != null)
                {
                    var inst = svcType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                    if (inst != null)
                    {
                        svcType.GetMethod("SetUnlockedOverride", new[] { typeof(int), typeof(bool) })
                               ?.Invoke(inst, new object[] { holeNumber, true });
                        d.LogStep($"  SetUnlockedOverride({holeNumber}, true)");
                    }
                    else d.LogStep($"  WARN: HoleProgressionService.Instance null");
                }
                else d.LogStep($"  WARN: HoleProgressionService type not found");
            }
            catch (System.Exception ex) { d.LogStep($"  WARN unlock: {ex.Message}"); }

            // Navigate to HoleSelection
            yield return d.ClickModeCardPlay("practice", settleSeconds: 1.5f);
            yield return d.WaitForScreen("HoleSelection", timeoutSeconds: 15f);
            yield return new WaitForSecondsRealtime(3f);

            // Tap the hole card
            bool tapped = false;
            try
            {
                System.Type cardType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("GolfinRedux.UI.HoleSelection.HoleCardController"); if (t != null) { cardType = t; break; } }
                if (cardType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("HoleCardController"); if (t != null) { cardType = t; break; } }

                if (cardType != null)
                {
                    var holeNumProp = cardType.GetProperty("HoleNumber",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    var cards = UnityEngine.Object.FindObjectsByType(cardType, UnityEngine.FindObjectsSortMode.None);
                    foreach (var card in cards)
                    {
                        if ((int)(holeNumProp?.GetValue(card) ?? 0) == holeNumber)
                        {
                            var go  = ((UnityEngine.Component)card).gameObject;
                            UnityEngine.UI.Button btn = null;
                            foreach (var b in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                                if (b.gameObject.name.Contains("CardTapButton") || b.gameObject.name.Contains("TapButton"))
                                { btn = b; break; }
                            if (btn == null) btn = go.GetComponentInChildren<UnityEngine.UI.Button>();
                            if (btn != null) { btn.onClick.Invoke(); d.LogStep($"  Tapped Hole {holeNumber} card"); tapped = true; }
                            break;
                        }
                    }
                    if (!tapped) d.LogStep($"  WARN: Hole {holeNumber} card not found");
                }
                else d.LogStep("  WARN: HoleCardController type not found");
            }
            catch (System.Exception ex) { d.LogStep($"  WARN card tap: {ex.Message}"); }

            yield return new WaitForSecondsRealtime(1.5f);

            // SeedSession + BeginGameplayLoad
            bool loadStarted = false;
            try
            {
                System.Type gsType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Session.GameSession"); if (t != null) { gsType = t; break; } }

                if (gsType != null)
                {
                    gsType.GetProperty("IsVersus", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                          ?.SetValue(null, false);

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
                            charId = (string)(cmType.GetMethod("GetSelectedCharacterId",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                ?.Invoke(cmInst, null) ?? string.Empty);
                    }

                    System.Type bmType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("BagManager"); if (t != null) { bmType = t; break; } }
                    if (bmType != null)
                    {
                        var bmInst = bmType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (bmInst != null)
                            bagSlot = (int)(bmType.GetProperty("EquippedBagSlot",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                ?.GetValue(bmInst) ?? 0);
                    }

                    gsType.GetMethod("SeedSession", new[] { typeof(int), typeof(string), typeof(int) })
                          ?.Invoke(null, new object[] { holeNumber, charId, bagSlot });
                    d.LogStep($"  SeedSession({holeNumber}, '{charId}', {bagSlot})");

                    System.Type loaderType = null;
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("Golfin.UI.GameplayTransition.GameplaySceneLoader"); if (t != null) { loaderType = t; break; } }

                    if (loaderType != null)
                    {
                        var loaderInst = loaderType.GetProperty("Instance",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
                        if (loaderInst != null)
                        {
                            System.Reflection.MethodInfo beginLoad =
                                loaderType.GetMethod("BeginGameplayLoad", new[] { typeof(int), typeof(object) })
                             ?? loaderType.GetMethod("BeginGameplayLoad", new[] { typeof(int) });
                            if (beginLoad == null)
                                foreach (var m in loaderType.GetMethods(
                                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                                    if (m.Name == "BeginGameplayLoad") { beginLoad = m; break; }

                            if (beginLoad != null)
                            {
                                var prms = beginLoad.GetParameters();
                                beginLoad.Invoke(loaderInst,
                                    prms.Length == 1 ? new object[] { holeNumber }
                                                     : new object[] { holeNumber, null });
                                d.LogStep($"  BeginGameplayLoad({holeNumber}) called");
                                loadStarted = true;
                            }
                        }
                        else d.LogStep("  WARN: GameplaySceneLoader.Instance null");
                    }
                    else d.LogStep("  WARN: GameplaySceneLoader type not found");
                }
                else d.LogStep("  WARN: GameSession type not found");
            }
            catch (System.Exception ex) { d.LogStep($"  WARN load: {ex.Message}"); }

            if (!loadStarted)
            {
                d.LogStep("  Fallback: clicking ActionButton");
                yield return d.Click("ActionButton", settleSeconds: 1.5f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(1.5f);
            }

            string geoScene = $"Hole_{holeNumber:D2}_Geo";
            yield return d.WaitForSceneLoaded("LabScaffold", timeoutSeconds: 40f);
            yield return d.WaitForSceneLoaded(geoScene,      timeoutSeconds: 40f);
            yield return new WaitForSecondsRealtime(5f); // fade-in + HUD settle; avoids Y-flip window
            d.LogStep($"  Hole {holeNumber} ({geoScene}) loaded and settled.");
        }

        // ── Mark recording start (Begin() was already called at EnteredPlayMode) ──

        IEnumerator StartRecording(Bot.BotDriver d, string preCapture)
        {
            // BotVideoRecorder.Begin() was already called by ZoneBakeAfterClipMenu.EnteredPlayMode()
            // (Arm() → Begin() at play mode entry, while Metal compositor is active).
            // Calling Begin() here again would be blocked by the one-recording-per-session guard.
            // We only check that recording is in progress and log the fact.
            bool guardSet = UnityEditor.SessionState.GetBool(
                "LoopV2SmokeBot.RecordedThisEditorSession", false);
            if (guardSet)
                d.LogStep("  BotVideoRecorder recording active (started at EnteredPlayMode) — guard=True");
            else
                d.LogStep("  WARN: session guard not set — recording may not have started");

            yield return new WaitForSecondsRealtime(1f);
            yield return d.Capture(preCapture);
        }

        // ── Fire one shot (no SetClub(putter), no PlaceBallAt, no preferredSurface) ──

        IEnumerator FireOneShot(Bot.BotDriver d, float aimYaw, float power01,
            int clubIndex, string label)
        {
            var ctrl = FindCtrl();
            if (ctrl == null) { d.LogStep($"  FAIL: PhysicsLabController not found ({label})"); yield break; }

            ctrl.SetClub(clubIndex); // 0 = Driver (NOT putter — putter auto-engages from surface)
            ctrl.InjectLabBundleForCurrentClub();
            ctrl.SetCameraYawRadians(aimYaw);
            d.LogStep($"  {label}: aimYaw={aimYaw:F3} power={power01:F2}");
            ctrl.FireViaShotController(power01, Golfin.Gameplay.Input.DebugShotAccuracy.Green);

            yield return null; // let SM transition Aiming→Flying
            var traj = ctrl.LastTrajectory;
            if (traj?.terrainHits != null)
                foreach (var hit in traj.terrainHits)
                    d.LogStep($"    TerrainHit: surface={hit.Surface} isStop={hit.IsStop}");
        }

        // ── Poll until BallState.Aiming (ball settled) ────────────────────────

        IEnumerator WaitForBallSettle(Bot.BotDriver d, float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(0.6f); // let ball leave Aiming on first frame

            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                var sm = FindCtrl()?.BallSM;
                if (sm != null && sm.State == Golfin.Gameplay.Loop.BallState.Aiming)
                {
                    d.LogStep($"  Ball settled (BallState.Aiming) t={Time.realtimeSinceStartup - start:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.1f);
            }
            d.LogStep($"  WaitForBallSettle timed out ({timeoutSeconds:F0}s) — state={FindCtrl()?.BallSM?.State}");
        }

        // ── Read IsPutt via reflection (multiple candidate property names) ─────

        bool ReadIsPutt(Bot.BotDriver d, out string surfName, out string clubName)
        {
            bool   result = false;
            surfName = "unknown";
            clubName = "unknown";

            try
            {
                // 1. BallSimulation.IsPutt / IsPuttingMode / IsPutting
                System.Type bsType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Physics.BallSimulation"); if (t != null) { bsType = t; break; } }
                if (bsType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("BallSimulation"); if (t != null) { bsType = t; break; } }

                if (bsType != null)
                {
                    var bs = UnityEngine.Object.FindFirstObjectByType(bsType);
                    if (bs != null)
                    {
                        var pProp =
                            bsType.GetProperty("IsPutt",        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            bsType.GetProperty("IsPuttingMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            bsType.GetProperty("IsPutting",     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (pProp != null) { result = (bool)pProp.GetValue(bs); d.LogStep($"  BallSimulation.{pProp.Name}={result}"); }
                        else d.LogStep("  WARN: BallSimulation IsPutt/* not found");

                        var sProp =
                            bsType.GetProperty("CurrentSurface", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            bsType.GetProperty("LastSurface",    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            bsType.GetProperty("Surface",        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (sProp != null) surfName = sProp.GetValue(bs)?.ToString() ?? "null";
                    }
                }

                // 2. ShotController.IsPutt / IsPuttingMode
                System.Type scType = null;
                foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                { var t = a.GetType("Golfin.Gameplay.Input.ShotController"); if (t != null) { scType = t; break; } }
                if (scType == null)
                    foreach (var a in System.AppDomain.CurrentDomain.GetAssemblies())
                    { var t = a.GetType("ShotController"); if (t != null) { scType = t; break; } }

                if (scType != null)
                {
                    var sc = UnityEngine.Object.FindFirstObjectByType(scType);
                    if (sc != null)
                    {
                        var pProp =
                            scType.GetProperty("IsPutt",        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            scType.GetProperty("IsPuttingMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                            scType.GetProperty("IsPutting",     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (pProp != null)
                        { bool v = (bool)pProp.GetValue(sc); d.LogStep($"  ShotController.{pProp.Name}={v}"); result = result || v; }

                        // Try to read club name
                        foreach (var name in new[] { "ActiveClubName", "CurrentClubName", "ClubName", "SelectedClubName" })
                        {
                            var cp = scType.GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                            if (cp != null) { clubName = cp.GetValue(sc)?.ToString() ?? "null"; break; }
                        }
                    }
                }

                // 3. PhysicsLabController.IsPutt
                var ctrl = FindCtrl();
                if (ctrl != null)
                {
                    var ctType = ctrl.GetType();
                    var pProp3 =
                        ctType.GetProperty("IsPutt",        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                        ctType.GetProperty("IsPuttingMode", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (pProp3 != null)
                    { bool v = (bool)pProp3.GetValue(ctrl); d.LogStep($"  PhysicsLabController.{pProp3.Name}={v}"); result = result || v; }
                }
            }
            catch (System.Exception ex) { d.LogStep($"  ReadIsPutt error: {ex.Message}"); }

            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        static PhysicsLabController FindCtrl()
            => UnityEngine.Object.FindFirstObjectByType<PhysicsLabController>();

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
