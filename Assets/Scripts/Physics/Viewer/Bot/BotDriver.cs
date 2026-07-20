#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Viewer.Bot
{
    /// <summary>
    /// Reusable bot primitives. Scenarios compose these into specific test flows.
    ///
    /// All waits use WaitForSecondsRealtime to survive Unity-MCP frozen-time sessions
    /// (timeScale may be 0). All captures go through CaptureCore.SnapPlayModeSafe
    /// (no async-capture Lesson K traps). Assembly boundary: this assembly
    /// (Golfin.Physics.Viewer) cannot statically reference Assembly-CSharp types such as
    /// GolfinRedux.UI.ScreenManager or Golfin.UI.Matchmaking.MatchmakingModalController —
    /// access is via FindObjectsOfType + System.Reflection.
    /// </summary>
    public class BotDriver
    {
        // ── State ─────────────────────────────────────────────────────────────

        readonly string _captureDir;
        readonly StringBuilder _log = new StringBuilder();
        int _captureCounter = 1;

        public string Log => _log.ToString();

        public BotDriver(string captureDir)
        {
            // If captureDir is relative, resolve it against the project root
            // (Application.dataPath = <project>/Assets; project root is one level up).
            if (!Path.IsPathRooted(captureDir))
            {
                string projectRoot = Path.GetDirectoryName(Application.dataPath);
                captureDir = Path.GetFullPath(Path.Combine(projectRoot, captureDir));
            }
            _captureDir = captureDir;
            Directory.CreateDirectory(captureDir);
        }

        // ── Logging ───────────────────────────────────────────────────────────

        /// <summary>Log a step to the history buffer with a realtime timestamp.</summary>
        public void LogStep(string message)
        {
            string line = $"[t={Time.realtimeSinceStartup:F2}] {message}";
            _log.AppendLine(line);
            Debug.Log($"[BotDriver] {message}");
        }

        /// <summary>Flush the log buffer to disk at scenario end.</summary>
        public void FlushLog(string filename = "history.log")
        {
            Directory.CreateDirectory(_captureDir);
            string path = Path.Combine(_captureDir, filename);
            File.WriteAllText(path, _log.ToString());
            Debug.Log($"[BotDriver] Log flushed to {path}");
        }

        // ── Capture ───────────────────────────────────────────────────────────

        /// <summary>
        /// Capture a screenshot via CaptureCore.SnapPlayModeSafe (the canonical path).
        /// Auto-prefixes counter (s01_, s02_, …). Copies result to captureDir.
        /// Caller should yield one frame before calling to ensure render is fresh.
        /// </summary>
        public IEnumerator Capture(string label)
        {
            // WaitForEndOfFrame ensures the full render cycle completes (including
            // Screen Space Overlay UI canvases). ScreenCapture.CaptureScreenshotAsTexture()
            // must be called at end-of-frame; a plain "yield return null" is insufficient.
            yield return new WaitForEndOfFrame();

            string counterLabel = $"s{_captureCounter:D2}_{label}";
            _captureCounter++;

            // SnapPlayModeSafe is synchronous, returns the absolute path.
            string srcPath = CaptureCore.SnapPlayModeSafe(counterLabel);

            // Copy into the per-scenario screenshots folder.
            if (!string.IsNullOrEmpty(srcPath) && File.Exists(srcPath))
            {
                Directory.CreateDirectory(_captureDir);
                string destPath = Path.Combine(_captureDir, Path.GetFileName(srcPath));
                File.Copy(srcPath, destPath, overwrite: true);
                LogStep($"Capture: {counterLabel} → {destPath}");
            }
            else
            {
                LogStep($"Capture WARN: SnapPlayModeSafe returned empty or non-existent path for label={counterLabel}");
            }
        }

        // ── UI button primitives ──────────────────────────────────────────────

        /// <summary>
        /// Find a Button by: exact GO name, case-insensitive GO name, or child TMP_Text
        /// containing the label as a substring. Searches all loaded scenes. Returns the first
        /// match; logs a warning if zero or more than one Button matches.
        /// </summary>
        public Button FindButton(string nameOrText)
        {
            var allButtons = UnityEngine.Object.FindObjectsOfType<Button>(includeInactive: false);
            var matches = new List<Button>();

            string lower = nameOrText.ToLowerInvariant();

            foreach (var btn in allButtons)
            {
                // Exact or case-insensitive GameObject name match.
                if (btn.gameObject.name.Equals(nameOrText, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(btn);
                    continue;
                }

                // Child TMP_Text substring match.
                var tmps = btn.GetComponentsInChildren<TMPro.TMP_Text>(includeInactive: false);
                foreach (var tmp in tmps)
                {
                    if (tmp.text != null && tmp.text.ToLowerInvariant().Contains(lower))
                    {
                        matches.Add(btn);
                        break;
                    }
                }
            }

            if (matches.Count == 0)
            {
                LogStep($"FindButton MISS: no active Button found for '{nameOrText}'");
                return null;
            }
            if (matches.Count > 1)
            {
                LogStep($"FindButton AMBIGUOUS: {matches.Count} buttons match '{nameOrText}' — using first. Consider a more specific name.");
            }
            return matches[0];
        }

        /// <summary>
        /// Click a button found by FindButton. Fires a real pointer-down → up sequence
        /// through ExecuteEvents so IPointerDownHandler components react exactly as they
        /// would to a human tap — notably the Stage F ButtonPressFeedback press-pulse,
        /// which onClick.Invoke() alone bypasses (it never raises a pointer event). The
        /// explicit onClick.Invoke() afterwards guarantees the action fires regardless of
        /// EventSystem / raycast state. Waits settleSeconds (realtime) after.
        /// </summary>
        public IEnumerator Click(string nameOrText, float settleSeconds = 0.8f)
        {
            LogStep($"Click: '{nameOrText}'");
            var btn = FindButton(nameOrText);
            if (btn != null)
            {
                // Real pointer-down/up so press-feedback (and the Button's own tint
                // transition) animate like a genuine tap. ExecuteEvents.Execute targets
                // the GameObject directly, so no raycast/EventSystem wiring is required.
                var ped = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.10f);   // brief press hold
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerUpHandler);

                // Guarantee the action fires (down+up alone does not synthesize a click).
                btn.onClick.Invoke();
                LogStep($"  → clicked {btn.gameObject.name} (pointer-down/up + onClick)");
            }
            else
            {
                LogStep($"  → CLICK FAILED: button '{nameOrText}' not found");
            }
            yield return new WaitForSecondsRealtime(settleSeconds);
        }

        // ── Wait primitives ───────────────────────────────────────────────────

        /// <summary>
        /// Poll a custom predicate until it returns true or timeout expires.
        /// Logs whether it timed out or succeeded.
        /// </summary>
        public IEnumerator WaitFor(Func<bool> predicate, string description, float timeoutSeconds = 10f)
        {
            LogStep($"WaitFor: {description} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (!predicate() && elapsed < timeoutSeconds)
            {
                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }
            if (elapsed >= timeoutSeconds)
                LogStep($"  WaitFor TIMEOUT: {description} not satisfied after {timeoutSeconds}s");
            else
                LogStep($"  WaitFor OK: {description} after {elapsed:F1}s");
        }

        /// <summary>
        /// Poll until the active screen name (read via ScreenManager reflection)
        /// equals screenNameOrId (case-insensitive). ScreenManager._currentScreen is a
        /// GolfinRedux.UI.ScreenId enum; we compare its ToString() to the argument.
        /// </summary>
        public IEnumerator WaitForScreen(string screenNameOrId, float timeoutSeconds = 15f)
        {
            LogStep($"WaitForScreen: {screenNameOrId} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                string current = GetCurrentScreenName();
                if (current != null &&
                    current.Equals(screenNameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    LogStep($"  WaitForScreen OK: on '{current}' after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }
            LogStep($"  WaitForScreen TIMEOUT: '{screenNameOrId}' not reached after {timeoutSeconds}s. Current={GetCurrentScreenName()}");
        }

        /// <summary>
        /// Poll until a modal (found by GameObject name, case-insensitive) is visible.
        /// Uses ModalController.IsVisible() via reflection.
        /// </summary>
        public IEnumerator WaitForModalVisible(string modalName, float timeoutSeconds = 15f)
        {
            LogStep($"WaitForModalVisible: {modalName} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (IsModalVisible(modalName))
                {
                    LogStep($"  WaitForModalVisible OK: '{modalName}' visible after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }
            LogStep($"  WaitForModalVisible TIMEOUT: '{modalName}' not visible after {timeoutSeconds}s");
        }

        /// <summary>
        /// Poll until a modal (by name) becomes invisible/closed.
        /// </summary>
        public IEnumerator WaitForModalHidden(string modalName, float timeoutSeconds = 15f)
        {
            LogStep($"WaitForModalHidden: {modalName} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (!IsModalVisible(modalName))
                {
                    LogStep($"  WaitForModalHidden OK: '{modalName}' hidden after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }
            LogStep($"  WaitForModalHidden TIMEOUT: '{modalName}' still visible after {timeoutSeconds}s");
        }

        /// <summary>
        /// Drives the app from cold start through the splash/loading sequence to the Home screen.
        /// Flow: Logo (auto-fades) → Splash (click StartButton) → Loading (auto) → Home.
        /// Call this at the start of any scenario that begins from cold launch.
        /// </summary>
        public IEnumerator NavigateToHome(float totalTimeoutSeconds = 60f)
        {
            LogStep("NavigateToHome: waiting for app startup sequence…");
            float elapsed = 0f;

            // Step 1: Wait for Logo or Splash (Logo auto-fades after ~3s).
            while (elapsed < totalTimeoutSeconds)
            {
                string current = GetCurrentScreenName();
                if (current == "Home") { LogStep($"  NavigateToHome: already on Home after {elapsed:F1}s"); yield break; }
                if (current == "Splash" || current == "Logo") break;
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }

            // Step 2: If on Logo, wait for it to auto-transition to Splash (~3s).
            while (elapsed < totalTimeoutSeconds && GetCurrentScreenName() == "Logo")
            {
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }

            // Step 3: If on Splash, click StartButton to proceed to Loading.
            if (GetCurrentScreenName() == "Splash")
            {
                LogStep($"  NavigateToHome: on Splash after {elapsed:F1}s — clicking StartButton");
                yield return new WaitForSecondsRealtime(0.5f); // let splash settle
                yield return Click("StartButton", settleSeconds: 0.5f);
                elapsed += 1.0f;
            }

            // Step 4: Wait for Loading to finish and Home to appear.
            while (elapsed < totalTimeoutSeconds)
            {
                string current = GetCurrentScreenName();
                if (current == "Home")
                {
                    LogStep($"  NavigateToHome: reached Home after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            LogStep($"  NavigateToHome TIMEOUT: did not reach Home after {totalTimeoutSeconds}s. Current={GetCurrentScreenName()}");
        }

        /// <summary>
        /// Poll until a Unity scene by name is in the loaded scene list.
        /// </summary>
        public IEnumerator WaitForSceneLoaded(string sceneName, float timeoutSeconds = 20f)
        {
            LogStep($"WaitForSceneLoaded: {sceneName} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded &&
                        scene.name.Equals(sceneName, StringComparison.OrdinalIgnoreCase))
                    {
                        LogStep($"  WaitForSceneLoaded OK: '{sceneName}' loaded after {elapsed:F1}s");
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            LogStep($"  WaitForSceneLoaded TIMEOUT: '{sceneName}' not loaded after {timeoutSeconds}s");
        }

        /// <summary>
        /// Poll until any active GameObject with the given name (case-insensitive) appears.
        /// </summary>
        public IEnumerator WaitForGameObject(string goName, float timeoutSeconds = 10f)
        {
            LogStep($"WaitForGameObject: {goName} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                var go = GameObject.Find(goName);
                if (go != null && go.activeInHierarchy)
                {
                    LogStep($"  WaitForGameObject OK: '{goName}' found after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.25f);
                elapsed += 0.25f;
            }
            LogStep($"  WaitForGameObject TIMEOUT: '{goName}' not found after {timeoutSeconds}s");
        }

        // ── Settings primitives ───────────────────────────────────────────────

        /// <summary>Type text into a TMP_InputField found by name.</summary>
        public IEnumerator TypeInto(string inputFieldName, string text)
        {
            LogStep($"TypeInto: '{inputFieldName}' = '{text}'");
            var fields = UnityEngine.Object.FindObjectsOfType<TMPro.TMP_InputField>(includeInactive: false);
            foreach (var f in fields)
            {
                if (f.gameObject.name.Equals(inputFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    f.text = text;
                    LogStep($"  TypeInto OK: set '{f.gameObject.name}' to '{text}'");
                    yield break;
                }
            }
            LogStep($"  TypeInto MISS: TMP_InputField '{inputFieldName}' not found");
            yield return null;
        }

        /// <summary>Read TMP_Text contents by GameObject name.</summary>
        public string ReadText(string textName)
        {
            var tmps = UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>(includeInactive: false);
            foreach (var t in tmps)
            {
                if (t.gameObject.name.Equals(textName, StringComparison.OrdinalIgnoreCase))
                    return t.text;
            }
            return null;
        }

        /// <summary>Drag a Slider to a given value (0..1 or range). Invokes onValueChanged.</summary>
        public IEnumerator SetSliderValue(string sliderName, float value)
        {
            LogStep($"SetSliderValue: '{sliderName}' = {value}");
            var sliders = UnityEngine.Object.FindObjectsOfType<Slider>(includeInactive: false);
            foreach (var s in sliders)
            {
                if (s.gameObject.name.Equals(sliderName, StringComparison.OrdinalIgnoreCase))
                {
                    s.value = value;
                    LogStep($"  SetSliderValue OK: '{s.gameObject.name}' = {value}");
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield break;
                }
            }
            LogStep($"  SetSliderValue MISS: Slider '{sliderName}' not found");
            yield return null;
        }

        /// <summary>Toggle a Toggle component found by name.</summary>
        public IEnumerator SetToggle(string toggleName, bool on)
        {
            LogStep($"SetToggle: '{toggleName}' = {on}");
            var toggles = UnityEngine.Object.FindObjectsOfType<Toggle>(includeInactive: false);
            foreach (var t in toggles)
            {
                if (t.gameObject.name.Equals(toggleName, StringComparison.OrdinalIgnoreCase))
                {
                    t.isOn = on;
                    LogStep($"  SetToggle OK: '{t.gameObject.name}' = {on}");
                    yield return new WaitForSecondsRealtime(0.2f);
                    yield break;
                }
            }
            LogStep($"  SetToggle MISS: Toggle '{toggleName}' not found");
            yield return null;
        }

        // ── Gameplay primitives ───────────────────────────────────────────────

        /// <summary>
        /// Fire a putt toward worldTarget using the §2f scaffolding pattern from SmokeRunner2fHost.
        ///
        /// Pattern (mirrors SmokeRunner2fHost.cs:454-565):
        ///   1. SetClub(PutterIndex) — select putter so IsPutt=true
        ///   2. SetBallAnimatorPlayRate(float.MaxValue) — Instant mode: SnapToEnd fires inside
        ///      Play(), the SM drain runs on the very next Tick (~1 frame), deterministic.
        ///   3. PlaceBallAt(nearCup, 1) — 3m from cup on green surface type 1 (green).
        ///   4. SetCameraYawRadians(yaw) — orient toward cup so RunSimForCamera doesn't
        ///      redirect the shot to +X (RunSimForCamera:1131 uses _cameraYaw, not the
        ///      preset velocity direction).
        ///   5. Gate on State==Aiming before firing.
        ///   6. Subscribe sm.OnShotComplete BEFORE ctrl.Fire() — sets a bool flag.
        ///   7. ctrl.Fire(puttPreset) — "putt_flat_3m" from ShotPresetCatalog.
        ///   8. Poll flag (frame-by-frame) up to timeoutSeconds.
        ///   9. Restore original PlayRate.
        ///
        /// worldTarget is used only to compute the yaw angle; actual ball placement is 3m
        /// from cup so a calibrated putt preset can reach InCup reliably.
        /// </summary>
        public IEnumerator FireShot(Vector3 worldTarget, float power01 = 1f, float timeoutSeconds = 30f)
        {
            LogStep($"FireShot: target={worldTarget} power={power01:F2} (§2f pattern)");

            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("  FireShot FAIL: PhysicsLabController not found in scene");
                yield break;
            }

            // 1. Switch to putter (LAB path — inject neutral bundle so physics is club-only).
            try
            {
                ctrl.SetClub(PhysicsLabController.PutterIndex);
                ctrl.InjectLabBundleForCurrentClub(); // LAB path
                LogStep($"  FireShot: SetClub(PutterIndex={PhysicsLabController.PutterIndex}) + InjectLabBundle");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: SetClub failed: {ex.Message}");
            }

            // 2. Save and set Instant PlayRate.
            float savedPlayRate = 1f;
            try
            {
                savedPlayRate = ctrl.GetBallAnimatorPlayRate();
                ctrl.SetBallAnimatorPlayRate(float.MaxValue);
                LogStep($"  FireShot: PlayRate saved={savedPlayRate:F2} → Instant (float.MaxValue)");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: SetBallAnimatorPlayRate failed: {ex.Message}");
            }

            // 3. Place ball 3m from target along the approach direction so a putt_flat_3m
            //    preset can reach InCup in one shot.
            Vector3 nearCup = worldTarget; // default: place AT cup if offset fails
            try
            {
                // 3m offset away from cup along the line from cup to ball spawn origin.
                // If ball already at a valid pos, offset from there; else use arbitrary offset.
                Vector3 ballPos = ctrl.BallPosition;
                Vector3 approachDir = (ballPos != Vector3.zero)
                    ? (ballPos - worldTarget).normalized
                    : new Vector3(1f, 0f, 0f); // fallback +X offset
                nearCup = worldTarget + approachDir * 3f;
                ctrl.PlaceBallAt(nearCup, preferredSurfaceTypeValue: 1);
                LogStep($"  FireShot: PlaceBallAt({nearCup}) — 3m from cup");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: PlaceBallAt failed: {ex.Message}");
            }

            // 4. Set camera yaw toward the cup so RunSimForCamera uses the correct direction.
            try
            {
                Vector3 towardCup = worldTarget - nearCup;
                float yaw = Mathf.Atan2(towardCup.z, towardCup.x);
                ctrl.SetCameraYawRadians(yaw);
                LogStep($"  FireShot: SetCameraYawRadians({yaw:F3} rad) toward cup");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: SetCameraYawRadians failed: {ex.Message}");
            }

            // 5. Gate on Aiming state before firing (state guard, not fixed wait).
            var sm = ctrl.BallSM;
            if (sm != null)
            {
                float gateElapsed = 0f;
                while (sm.State != BallState.Aiming && gateElapsed < 3f)
                {
                    gateElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                LogStep($"  FireShot: pre-fire Aiming gate done: State={sm.State} gateElapsed={gateElapsed:F3}s");
            }

            // 6. Subscribe OnShotComplete BEFORE firing.
            bool shotComplete = false;
            ShotResult shotResult = default;
            Action<ShotResult> onComplete = r =>
            {
                shotComplete = true;
                shotResult = r;
                Debug.Log($"[BotDriver] OnShotComplete fired: terminal={r.TerminalState} endSurface={r.EndSurface}");
            };
            if (sm != null)
                sm.OnShotComplete += onComplete;

            // 7. Select putt preset and fire.
            ShotPreset puttPreset = default;
            try
            {
                puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id == "putt_flat_3m");
                if (puttPreset.Id == null)
                    puttPreset = ShotPresetCatalog.All.FirstOrDefault(p => p.Id != null && p.Id.StartsWith("putt"));
                if (puttPreset.Id == null)
                    LogStep("  FireShot WARN: no putt preset found in ShotPresetCatalog");
                else
                    LogStep($"  FireShot: using preset '{puttPreset.Id}'");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: preset lookup failed: {ex.Message}");
            }

            try
            {
                if (puttPreset.Id != null)
                {
                    ctrl.Fire(puttPreset);
                    LogStep("  FireShot: ctrl.Fire(puttPreset) called — Instant mode, shot completes in ~1 frame");
                }
                else
                {
                    LogStep("  FireShot FAIL: no valid preset — cannot fire");
                    if (sm != null) sm.OnShotComplete -= onComplete;
                    // Restore PlayRate before bailing.
                    try { ctrl.SetBallAnimatorPlayRate(savedPlayRate); } catch { /* best-effort */ }
                    yield break;
                }
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot FAIL: ctrl.Fire threw: {ex.Message}");
                if (sm != null) sm.OnShotComplete -= onComplete;
                try { ctrl.SetBallAnimatorPlayRate(savedPlayRate); } catch { /* best-effort */ }
                yield break;
            }

            // 8. Poll flag frame-by-frame (Instant mode: should complete in <0.1s realtime).
            const float InstantShotWait = 5f;
            float elapsed = 0f;
            while (!shotComplete && elapsed < InstantShotWait)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (sm != null) sm.OnShotComplete -= onComplete;

            if (shotComplete)
                LogStep($"  FireShot OK: OnShotComplete fired after {elapsed:F3}s — terminal={shotResult.TerminalState}");
            else
                LogStep($"  FireShot TIMEOUT: OnShotComplete not fired within {InstantShotWait}s (Instant mode)");

            // 9. Restore PlayRate.
            try
            {
                ctrl.SetBallAnimatorPlayRate(savedPlayRate);
                LogStep($"  FireShot: PlayRate restored to {savedPlayRate:F2}");
            }
            catch (Exception ex)
            {
                LogStep($"  FireShot WARN: PlayRate restore failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Drives the ball-state machine directly to a terminal state, skipping physics.
        /// Use ONLY for scenarios whose gate is downstream of terminal-state observation
        /// (modal wiring, scene unload, reward grant, progression write). For scenarios
        /// that genuinely test shot mechanics, use FireShot instead.
        ///
        /// Delegates to BallStateMachine.ForceShotCompleteForBot (a #if UNITY_EDITOR seam
        /// that fires the same OnShotComplete event production uses). Modal sees no difference.
        /// </summary>
        public IEnumerator ForceShotComplete(string terminalStateName, float settleSeconds = 0.5f)
        {
            LogStep($"ForceShotComplete: driving terminal={terminalStateName}");
            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("ForceShotComplete FAIL: no PhysicsLabController in scene");
                yield break;
            }

            if (!System.Enum.TryParse<BallState>(terminalStateName, out var target))
            {
                LogStep($"ForceShotComplete FAIL: unknown BallState '{terminalStateName}'");
                yield break;
            }

            ctrl.BallSM.ForceShotCompleteForBot(target);
            LogStep($"ForceShotComplete OK: terminal={target}");
            yield return new WaitForSecondsRealtime(settleSeconds);
        }

        /// <summary>
        /// Plays the current hole with REAL physics shots. Each stroke aims the camera at
        /// the cup and fires via the PRODUCTION ShotController path (FireViaShotController)
        /// so the shot UI (cone, ball widget, club handle) hides during ball flight exactly
        /// as it does for a real player. Club selection follows Cesar's fix #2: Driver only
        /// on stroke 1; subsequent strokes use Iron 7, Wedge, or Putter by distance.
        ///
        /// Loops until the ball is InCup or the stroke count reaches par+3 (safety net seam).
        /// Normal PlayRate is left untouched so each shot animates its full trajectory.
        /// </summary>
        /// <param name="firstStrokePowerOverride">When &gt; 0, overrides the first-stroke power
        /// (default 1.0 = Driver full). Use values like 0.55 on par-3 holes to keep
        /// the ball in play — Driver at full power carries ~250m which overshoots short
        /// holes into OB.</param>
        public IEnumerator PlayHoleToCup(int par, float firstStrokePowerOverride = 0f)
        {
            int maxStrokes = par + 3;
            LogStep($"=== PlayHoleToCup: par={par}, stroke cap={maxStrokes}, firstStrokePowerOverride={firstStrokePowerOverride:F2} ===");

            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null) { LogStep("PlayHoleToCup FAIL: no PhysicsLabController"); yield break; }
            var sm = ctrl.BallSM;

            // ShotController drives the production drag path so the club handle visibly
            // pulls down for each shot (Cesar fix #3).
            var shotCtl = UnityEngine.Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
            if (shotCtl == null)
                LogStep("PlayHoleToCup WARN: no ShotController found — club-handle animation unavailable, will fall back to instant fire");

            Vector3 cup = FindCupPosition();
            int strokes = 0;
            bool inCup = false;
            bool isFirstStroke = true;

            while (!inCup && strokes < maxStrokes)
            {
                strokes++;
                Vector3 ball = ctrl.BallPosition;
                Vector3 flat = new Vector3(cup.x - ball.x, 0f, cup.z - ball.z);
                float dist = flat.magnitude;

                SelectShot(dist, isFirstStroke, out int club, out float power01, out string label,
                    firstStrokePowerOverride);
                isFirstStroke = false;

                // ── Off-green putter guard (Order 731, 2026-07-16; updated Order 761, 2026-07-19) ──
                // Mirrors VersusBot.cs H3b and the real game's PutterModeSurfaceController,
                // which reverts putter→last club whenever the ball is off-green. A putt fired
                // from a non-putt surface (Fairway/Rough) fails IsPutt's surface gate and is
                // misrouted to the AIRBORNE integrator; its origin sits on/below the sim
                // ground, so the airborne ground-crossing guard never fires and the ball
                // free-falls under gravity (y≈-1582m) while OnShotComplete never returns.
                // Real players cannot putt off-green (the club UI auto-reverts it) and
                // VersusBot already guards this — BotDriver was the lone harness that didn't,
                // making the capture bot behave unlike a real player.
                // Order 761: chip with the Wedge (club 2, now in default bag) for a higher-loft
                // approach that clears roughs and holds the green better than the Iron7.
                if (club == PhysicsLabController.PutterIndex)
                {
                    var surfaces    = ctrl.GetSurfaces();
                    var ballSurface = surfaces != null
                        ? surfaces.Classify(Golfin.Physics.Math.fp.FromFloat(ball.x),
                                            Golfin.Physics.Math.fp.FromFloat(ball.z))
                        : SurfaceType.Green;
                    if (ballSurface != SurfaceType.Green && ballSurface != SurfaceType.GreenCollar)
                    {
                        // Chip with the Wedge (club 2 — club_pwedge_royal, now in default bag, Order 761).
                        // High loft clears rough and lands softly on the green. Calibrated to the
                        // remaining distance via the "wedge" carry curve so the chip does not overshoot.
                        EnsureCarryTable();
                        club    = 2; // Wedge chip back toward the green.
                        power01 = (_carryTable != null && _carryTable.Count > 0)
                            ? InterpolateClubPower("wedge", Mathf.Max(dist, 5f))
                            : Mathf.Clamp(dist / 90f, 0.05f, 0.70f);
                        label   = $"Wedge (off-green putter guard, surface={ballSurface}) dist={dist:F1}m power={power01:F2}";
                        LogStep($"  Off-green putter guard: surface={ballSurface} at ({ball.x:F1},{ball.z:F1}) → Wedge chip instead of Putter (prevents airborne fall-through)");
                    }
                }

                LogStep($"Stroke {strokes}: ball={ball:F1} cup={cup:F1} dist={dist:F1}m — {label} club={club} power={power01:F2}");

                // PROD path: SetClub only (no lab bundle inject); clear any prior override so bus resolves live stats.
                try
                {
                    ctrl.SetClub(club);
                    shotCtl?.ClearStatBundleOverride();
                }
                catch (Exception ex) { LogStep($"  SetClub warn: {ex.Message}"); }

                // LIVE-path club switch (Order 731, 2026-07-17).
                // SetClub only updates the LAB club index + cone/putter UI. On the LIVE stat
                // path the provider resolves the SWING club from ClubContext.SelectedClubId
                // (LiveStatProviderHost.cs:188) — which SetClub never touches. Without this the
                // equipped driver is fired for EVERY stroke (clubVel=75m/s, loft=10.9°) → every
                // shot overshoots ~2× and the hole never converges. Drive the same selection the
                // real club-selector widget uses (mirrors LabInventoryStub / ClubContextPopulator)
                // so the chosen club is actually equipped — this is what makes the bot switch
                // clubs like a real player. (Putter, LabClubIndex 3, is resolved from EquippedBag
                // directly by the provider, but we keep the selection in sync for consistency.)
                try
                {
                    var bag = Golfin.Gameplay.UI.HUD.ClubContext.EquippedBag;
                    // Resolve the desired lab-club index to the NEAREST AVAILABLE equipped club.
                    // The player's bag may not contain every lab club — a real player then reaches
                    // for the closest club they actually carry (a shorter club: prefer the largest
                    // index <= desired; else the smallest index > desired). This is what makes the
                    // bot play its real bag. (Order 761: the default bag now includes a wedge at
                    // LabClubIndex 2, so club=2 resolves directly. The fallback logic handles any
                    // non-default bag that might be missing a slot.)
                    int bagIdx = -1;
                    if (bag != null && bag.Count > 0)
                    {
                        bagIdx = bag.FindIndex(e => e.LabClubIndex == club);
                        if (bagIdx < 0)
                        {
                            int bestBelow = -1, bestBelowIdx = -1, bestAbove = int.MaxValue, bestAboveIdx = -1;
                            for (int i = 0; i < bag.Count; i++)
                            {
                                int li = bag[i].LabClubIndex;
                                if (li <= club && li > bestBelow) { bestBelow = li; bestBelowIdx = i; }
                                if (li >  club && li < bestAbove) { bestAbove = li; bestAboveIdx = i; }
                            }
                            bagIdx = bestBelowIdx >= 0 ? bestBelowIdx : bestAboveIdx;
                        }
                    }
                    if (bagIdx >= 0)
                    {
                        var entry = bag[bagIdx];
                        Golfin.Gameplay.UI.HUD.ClubContext.SelectedClubId    = entry.ClubId;
                        Golfin.Gameplay.UI.HUD.ClubContext.SelectedIndex     = bagIdx;
                        Golfin.Gameplay.UI.HUD.ClubContext.SelectedTypeLabel = entry.TypeLabel;
                        Golfin.Gameplay.UI.HUD.ClubContext.SelectedPortrait  = entry.Portrait;  // Order 761 fix: mirror SelectByIndex — was leaving driver portrait on wedge
                        Golfin.Gameplay.UI.HUD.ClubContext.SelectedDistance  = entry.Distance;  // Order 761 fix: mirror SelectByIndex — was leaving "250 yrds" on wedge
                        Golfin.Gameplay.UI.HUD.ClubContext.RaiseSelectedChanged();
                        // Keep the lab club index in sync with the club actually equipped so the
                        // putter-mode / cone UI and isPutt detection match what the LIVE provider fires.
                        if (entry.LabClubIndex != club)
                        {
                            try { ctrl.SetClub(entry.LabClubIndex); } catch { }
                            LogStep($"  ClubContext → '{entry.ClubId}' (bagIdx={bagIdx}, labIdx {club}→{entry.LabClubIndex}, no exact club in bag) so the LIVE provider fires an available club");
                            club = entry.LabClubIndex;
                        }
                        else
                        {
                            LogStep($"  ClubContext → '{entry.ClubId}' (bagIdx={bagIdx}, labIdx={club}) so the LIVE provider fires the selected club");
                        }
                    }
                    else
                    {
                        LogStep($"  ClubContext WARN: empty EquippedBag (count={(bag?.Count ?? 0)}) — LIVE club unchanged, may overshoot");
                    }
                }
                catch (Exception ex) { LogStep($"  ClubContext switch warn: {ex.Message}"); }

                float yaw = Mathf.Atan2(flat.z, flat.x);
                try { ctrl.SetCameraYawRadians(yaw); }
                catch (Exception ex) { LogStep($"  SetCameraYaw warn: {ex.Message}"); }

                // Blocker A fix (2026-07-17): SetCameraYawRadians only sets the internal
                // _cameraYaw float + ShotController heading — it does NOT rotate the chase
                // camera transform (that happens in PhysicsLabController.ApplyCameraYaw, which
                // the production auto-aim calls at AtRest but the bot seam bypasses). Rotate
                // the chase camera to face the cup so the VIDEO shows the bot aiming at the pin
                // and the HoleIndicatorWidget flag-tail (which reads chaseCamera.forward) points
                // at the hole, instead of the stale "shoots straight" forward.
                try { AimChaseCameraAtCup(ball, cup); }
                catch (Exception ex) { LogStep($"  AimChaseCamera warn: {ex.Message}"); }

                // Gate on BallSM in Aiming state before firing.
                if (sm != null)
                {
                    float g = 0f;
                    while (sm.State != BallState.Aiming && g < 3f)
                    {
                        g += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    LogStep($"  Pre-fire state: SM={sm.State} (gated {g:F2}s)");
                }

                // Subscribe OnShotComplete BEFORE firing.
                bool done = false;
                ShotResult res = default;
                Action<ShotResult> handler = r => { done = true; res = r; };
                if (sm != null) sm.OnShotComplete += handler;

                // Fire via the production ShotController DRAG path — mirrors ClubHandleDragger
                // (a real player): BeginExternalDrag → ramped SetExternalPower → EndExternalDrag.
                // Ramping the power over ~0.85s makes the club handle visibly pull DOWN before
                // the shot releases (Cesar fix #3 — the handle must move like a player taking
                // the shot). The release → CommitFlick → Resolving still hides the cone / handle
                // / ball widget during flight (fix #1).
                bool fired = false;
                if (shotCtl != null)
                {
                    // Wait for ShotController to return to Idle (previous shot fully cleared).
                    float si = 0f;
                    while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
                    {
                        si += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    shotCtl.BeginExternalDrag(); // Idle → Aiming; the handle starts at rest

                    const float rampSeconds = 0.85f; // visible handle pull-down
                    float rt = 0f;
                    while (rt < rampSeconds)
                    {
                        rt += Time.unscaledDeltaTime;
                        shotCtl.SetExternalPower(Mathf.Lerp(0f, power01, rt / rampSeconds), 0f);
                        yield return null;
                    }
                    shotCtl.SetExternalPower(power01, 0f);
                    yield return new WaitForSecondsRealtime(0.18f); // brief hold at full pull
                    shotCtl.EndExternalDrag(); // Timing → CommitFlick → Resolving → shot fires
                    fired = true;
                    LogStep($"  Fired via ShotController drag path — handle ramped to power={power01:F2}");
                }
                else
                {
                    // Fallback: no ShotController — instant fire (handle will not animate).
                    try
                    {
                        ctrl.FireViaShotController(power01, Golfin.Gameplay.Input.DebugShotAccuracy.Green);
                        fired = true;
                        LogStep($"  FireViaShotController(power={power01:F2}) — instant fallback (no handle anim)");
                    }
                    catch (Exception ex)
                    {
                        LogStep($"  FireViaShotController threw: {ex.Message}");
                    }
                }

                if (fired)
                {
                    // Wait up to 30s realtime for OnShotComplete (flight + roll + terminal state).
                    float e = 0f;
                    while (!done && e < 30f)
                    {
                        e += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    LogStep($"  Shot wait: done={done} elapsed={e:F1}s");
                }
                if (sm != null) sm.OnShotComplete -= handler;

                if (done)
                {
                    inCup = res.TerminalState == BallState.InCup;
                    LogStep($"  Stroke {strokes} terminal={res.TerminalState} endSurface={res.EndSurface} ball={ctrl.BallPosition:F1}");
                    // Diagnostic: first/last terrain contact so a truncated carry (early ground
                    // strike / obstacle) is distinguishable from a full-carry-then-roll shot.
                    var traj = ctrl.LastTrajectory;
                    if (traj != null && traj.terrainHits != null && traj.terrainHits.Count > 0)
                    {
                        var fh = traj.terrainHits[0];
                        var lh = traj.terrainHits[traj.terrainHits.Count - 1];
                        LogStep($"    traj: bounces={traj.terrainHits.Count} firstHit=({fh.Position.x.ToFloat():F0},{fh.Position.y.ToFloat():F0},{fh.Position.z.ToFloat():F0}) surf={fh.Surface} lastHit=({lh.Position.x.ToFloat():F0},{lh.Position.y.ToFloat():F0},{lh.Position.z.ToFloat():F0}) surf={lh.Surface}");
                    }
                }
                else
                {
                    LogStep($"  Stroke {strokes} -> OnShotComplete not observed (timeout or fire failed)");
                }

                // Short pause so the settled ball frame is visible in the video.
                yield return new WaitForSecondsRealtime(1.0f);
                yield return Capture($"stroke{strokes}");
            }

            if (!inCup)
            {
                LogStep($"PlayHoleToCup: not holed in {strokes} real strokes — ForceShotComplete safety net (Cesar spec: par+3 cap).");
                yield return ForceShotComplete("InCup");
            }
            LogStep($"=== PlayHoleToCup done: {strokes} strokes, holed={(inCup ? "real" : "seam")} ===");
        }

        /// <summary>
        /// Rotate the gameplay chase camera to look from behind the ball toward the cup —
        /// mirrors PhysicsLabController.ApplyCameraYaw (orbitCenter = ball). The bot's
        /// SetCameraYawRadians seam sets only the internal _cameraYaw + ShotController heading
        /// (so the SHOT fires at the cup) but never moves the camera, so the visible camera and
        /// the HoleIndicatorWidget flag-tail (bound to chaseCamera.forward) stayed pointed the
        /// old way — the "shoots straight / flag not at the pin" symptom. Idempotent (same angle
        /// as the production AtRest auto-aim). Only acts in Chase mode with the ball at rest so
        /// it never fights ChaseCamera's in-flight follow.
        /// </summary>
        void AimChaseCameraAtCup(Vector3 ball, Vector3 cup)
        {
            var chase = UnityEngine.Object.FindObjectOfType<Golfin.Physics.Viewer.ChaseCamera>();
            if (chase == null) return;
            if (chase.CurrentMode != Golfin.Physics.Viewer.ChaseCamera.Mode.Chase) return;
            var cam = chase.GetComponent<Camera>();
            if (cam == null) return;
            float yaw = Mathf.Atan2(cup.z - ball.z, cup.x - ball.x);
            Vector3 lookDir = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
            cam.transform.position = ball - lookDir * 8f + Vector3.up * 3f;
            cam.transform.LookAt(ball + lookDir * 3f + Vector3.up * 0.5f);
        }

        /// <summary>
        /// Select club and power for a shot of the given horizontal distance (metres),
        /// CALIBRATED against the LIVE default bag's measured carry curves (bot_clubs.csv).
        ///
        /// Why this was originally rewritten (2026-07-17, bot-completion rehab): the old table
        /// was tuned for a LAB wedge (~91m full carry). The default bag at that time had NO wedge
        /// (Driver / Wood / Iron7 / Putter, see ClubManager.DefaultBagIds pre-Order-761). The LIVE
        /// stat path mapped the old "Wedge" (club 2) onto the Iron7, whose real carry at power 0.75
        /// is ~235m, not ~55m — every approach overshot ~4× and never converged. Fix was to select
        /// ONLY clubs the bag actually carried and interpolate power from that club's measured curve.
        ///
        /// Order 761 (2026-07-19): the default bag NOW carries P.Wedge Royal Swing (club_pwedge_royal).
        /// The wedge band is restored for short approaches (20–80m), using the "wedge" curve from
        /// bot_clubs.csv (the same table VersusBot uses via InterpolateClubPower). The Iron7 band is
        /// retained for medium approaches (80–130m) and layups.
        ///
        /// Bands (remaining distance → cup, metres):
        ///   dist &lt;= 18   : Putter  — carry = dist (on/near green).
        ///   dist &lt;= 80   : Wedge   — carry = dist (soft drop onto / into the green).
        ///   dist &lt;= 130  : Iron7   — carry = dist (land near the cup).
        ///   dist &lt;= 230  : Iron7   — layup: carry = dist − 55 (leave a wedge approach).
        ///   dist &gt;  230  : Driver  — layup: carry = min(dist − 60, driverMax).
        ///
        /// Club indices: 0=Driver, 1=Iron 7, 2=Wedge, 3=Putter (PhysicsLabController.PutterIndex).
        /// </summary>
        void SelectShot(float dist, bool isFirstStroke, out int club, out float power01, out string label,
            float firstStrokePowerOverride = 0f)
        {
            EnsureCarryTable();
            int putter = PhysicsLabController.PutterIndex;

            // Explicit first-stroke override hook (e.g. par-3 tuning). Driver at the given power.
            if (isFirstStroke && firstStrokePowerOverride > 0f)
            {
                club    = 0;
                power01 = Mathf.Clamp01(firstStrokePowerOverride);
                label   = $"Driver (first-stroke override, dist={dist:F0}m, power={power01:F2})";
                return;
            }

            // Carry table unavailable → fall back to the (bag-correct) legacy heuristic so the
            // bot still plays if bot_clubs.csv is missing.
            if (_carryTable == null || _carryTable.Count == 0)
            {
                SelectShotLegacy(dist, out club, out power01, out label);
                return;
            }

            // On / near the green: putt. NOTE the bot_clubs.csv putter curve (0.20→9m) is
            // calibrated for a different, stronger character on flat ground; the LIVE solo
            // default (low-level char_james) on Hole 1's green rolls ~5× shorter — measured
            // 0.20→1.9m, 0.17→1.5m, 0.13→1.0m ⇒ carry ≈ 9·power. So target the distance with
            // power = dist/9 (green-response calibrated) instead of the flat-ground table, or
            // putts crawl and never reach the cup. Slight-short bias + the per-stroke re-aim
            // loop converge onto the 5.4cm cup without lip-out (arrive < 1.5 m/s speed gate).
            if (dist <= 18f)
            {
                club    = putter;
                power01 = Mathf.Clamp(dist / 9f, 0.12f, 1f);
                label   = $"Putter (green-calibrated, dist={dist:F1}m) power={power01:F2}";
                return;
            }

            string name;
            float  targetCarry;
            if (dist > 230f)
            {
                name        = "driver";
                targetCarry = Mathf.Min(dist - 60f, GetMaxCarry("driver"));
            }
            else if (dist > 130f)
            {
                name        = "iron7";
                targetCarry = dist - 55f;   // layup, leave a wedge approach
            }
            else if (dist > 80f)
            {
                name        = "iron7";
                targetCarry = dist;         // land near the cup
            }
            else
            {
                // Short approach (20–80m): use the wedge (bag now has club_pwedge_royal, Order 761).
                // Higher loft + backspin holds the sunken green better than Iron7 chips.
                name        = "wedge";
                targetCarry = dist;
            }

            if (name == "driver")       club = 0;
            else if (name == "iron7")   club = 1;
            else if (name == "wedge")   club = 2;
            else                        club = 1;   // fallback

            power01 = InterpolateClubPower(name, targetCarry);
            label   = $"{name} (calibrated, dist={dist:F1}m carry~{targetCarry:F0}m) power={power01:F2}";
        }

        /// <summary>
        /// Bag-correct legacy fallback used only when bot_clubs.csv fails to load.
        /// Driver for long, Iron7 for approaches, Putter near the green — conservative powers.
        /// </summary>
        void SelectShotLegacy(float dist, out int club, out float power01, out string label)
        {
            int putter = PhysicsLabController.PutterIndex;
            if (dist <= 18f)      { club = putter; power01 = Mathf.Clamp01(dist / 40f);  label = $"Putter legacy dist={dist:F0}m power={power01:F2}"; }
            else if (dist > 200f) { club = 0;      power01 = 0.55f;                        label = $"Driver legacy dist={dist:F0}m power={power01:F2}"; }
            else if (dist > 60f)  { club = 1;      power01 = Mathf.Clamp01(dist / 260f);   label = $"Iron7 legacy dist={dist:F0}m power={power01:F2}"; }
            else                  { club = 1;      power01 = Mathf.Clamp01(dist / 170f);   label = $"Iron7 legacy chip dist={dist:F0}m power={power01:F2}"; }
        }

        // ── Carry-table calibration (bot_clubs.csv — LIVE production carries) ──────
        // Reuses VersusBot's H1 calibration DATA (the same Resources/Data/bot_clubs.csv the 1v1
        // bot is tuned on). Loaded once; interpolation mirrors VersusBot.InterpolateClubPower.
        struct CarryRow { public string club; public float power01; public float carry; }
        static List<CarryRow> _carryTable;
        static bool           _carryTableLoaded;

        static void EnsureCarryTable()
        {
            if (_carryTableLoaded) return;
            _carryTableLoaded = true;
            _carryTable = new List<CarryRow>(96);
            var csv = Resources.Load<TextAsset>("Data/bot_clubs");
            if (csv == null) { Debug.LogWarning("[BotDriver] bot_clubs.csv not found in Resources/Data — SelectShot will use legacy fallback."); return; }
            foreach (var raw in csv.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("club,")) continue;
                var p = line.Split(',');
                if (p.Length < 3) continue;
                if (!float.TryParse(p[1].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float pow)) continue;
                if (!float.TryParse(p[2].Trim(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out float carry)) continue;
                _carryTable.Add(new CarryRow { club = p[0].Trim(), power01 = pow, carry = carry });
            }
        }

        static float GetMaxCarry(string clubName)
        {
            float max = 0f;
            if (_carryTable != null)
                foreach (var r in _carryTable)
                    if (r.club == clubName && r.carry > max) max = r.carry;
            return max;
        }

        /// <summary>Linear-interpolate power01 for a target carry (metres) from the club's curve.</summary>
        static float InterpolateClubPower(string clubName, float targetDist)
        {
            CarryRow below = default, above = default;
            bool foundBelow = false, foundAbove = false;
            foreach (var r in _carryTable)
            {
                if (r.club != clubName) continue;
                if (r.carry <= targetDist) { if (!foundBelow || r.carry > below.carry) { below = r; foundBelow = true; } }
                else                       { if (!foundAbove || r.carry < above.carry) { above = r; foundAbove = true; } }
            }
            if (!foundBelow && foundAbove) return above.power01;
            if (foundBelow && !foundAbove) return below.power01; // clamp at the club's max
            if (!foundBelow)               return 0f;
            float span = above.carry - below.carry;
            if (span < 0.01f) return below.power01;
            float t = (targetDist - below.carry) / span;
            return Mathf.Clamp01(Mathf.Lerp(below.power01, above.power01, t));
        }

        /// <summary>
        /// Wait until the BallStateMachine reaches any terminal state (AtRest, InCup, OB),
        /// or specifically the named state. stateName="terminal" matches any of the three.
        /// Reads BallSM via PhysicsLabController.BallSM (internal property, same asmdef).
        /// NOTE: With §2f Instant-mode FireShot, this is typically called AFTER FireShot has
        /// already confirmed OnShotComplete fired. This method is kept for scenarios that need
        /// to verify the terminal state independently.
        /// </summary>
        public IEnumerator WaitForBallState(string stateName, float timeoutSeconds = 30f)
        {
            LogStep($"WaitForBallState: {stateName} (timeout={timeoutSeconds}s)");
            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("  WaitForBallState SKIP: PhysicsLabController not found");
                yield break;
            }
            var sm = ctrl.BallSM;
            if (sm == null)
            {
                LogStep("  WaitForBallState SKIP: BallSM is null");
                yield break;
            }

            // Subscribe to OnShotComplete to eliminate the polling race.
            bool reached = false;
            BallState reachedState = BallState.Aiming;
            Action<ShotResult> handler = r =>
            {
                bool isTerminal = r.TerminalState == BallState.AtRest
                    || r.TerminalState == BallState.InCup
                    || r.TerminalState == BallState.OB;
                bool matches = stateName.Equals("terminal", StringComparison.OrdinalIgnoreCase)
                    ? isTerminal
                    : r.TerminalState.ToString().Equals(stateName, StringComparison.OrdinalIgnoreCase);
                if (matches) { reached = true; reachedState = r.TerminalState; }
            };
            sm.OnShotComplete += handler;

            float elapsed = 0f;
            while (!reached && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            sm.OnShotComplete -= handler;

            if (reached)
                LogStep($"  WaitForBallState OK: '{reachedState}' reached after {elapsed:F3}s");
            else
                LogStep($"  WaitForBallState TIMEOUT: '{stateName}' not reached after {timeoutSeconds}s. Current={sm.State}");
        }

        // ── Reflection helpers (Assembly-CSharp bridge) ───────────────────────

        /// <summary>
        /// Read GolfinRedux.UI.ScreenManager._currentScreen via reflection.
        /// Returns the ScreenId enum value as a string, or null if unavailable.
        /// </summary>
        public string GetCurrentScreenName()
        {
            // ScreenManager is in Assembly-CSharp; access via Type.GetType with assembly qualification.
            Type smType = Type.GetType("GolfinRedux.UI.ScreenManager, Assembly-CSharp");
            if (smType == null) return null;

            // Find the instance via static Instance property.
            var instanceProp = smType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            if (instanceProp == null) return null;

            object instance = instanceProp.GetValue(null);
            if (instance == null) return null;

            // Read the private _currentScreen field.
            var field = smType.GetField("_currentScreen",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return null;

            object val = field.GetValue(instance);
            return val?.ToString();
        }

        /// <summary>
        /// Read MatchmakingModalController.Phase via reflection (it's in Assembly-CSharp).
        /// Returns the MatchmakingPhase enum value as a string, or "Unknown".
        /// </summary>
        public string GetMatchmakingPhase()
        {
            Type mmType = Type.GetType(
                "Golfin.UI.Matchmaking.MatchmakingModalController, Assembly-CSharp");
            if (mmType == null) return "Unknown";

            var instances = UnityEngine.Object.FindObjectsOfType(mmType);
            if (instances == null || instances.Length == 0) return "Unknown";

            var phaseProp = mmType.GetProperty("Phase",
                BindingFlags.Public | BindingFlags.Instance);
            if (phaseProp == null) return "Unknown";

            object val = phaseProp.GetValue(instances[0]);
            return val?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// Check whether a modal (by GO name, case-insensitive) is visible.
        /// Uses ModalController.IsVisible() if available; falls back to GO active check.
        /// </summary>
        bool IsModalVisible(string modalName)
        {
            var allMonos = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            foreach (var mono in allMonos)
            {
                if (!mono.gameObject.name.Equals(modalName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Try ModalController.IsVisible() via reflection.
                var isVisibleMethod = mono.GetType().GetMethod("IsVisible",
                    BindingFlags.Public | BindingFlags.Instance);
                if (isVisibleMethod != null)
                {
                    object result = isVisibleMethod.Invoke(mono, null);
                    if (result is bool b) return b;
                }

                // Fallback: check if the GO itself is active.
                return mono.gameObject.activeInHierarchy;
            }
            return false;
        }

        /// <summary>
        /// Read BallStateMachine.State via PhysicsLabController.BallSM (internal property).
        /// Returns state name string, or null if not available.
        /// </summary>
        string GetBallStateName()
        {
            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null) return null;

            // BallSM is an internal property — accessible via reflection within the same assembly.
            var prop = typeof(PhysicsLabController).GetProperty("BallSM",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (prop == null) return null;

            object sm = prop.GetValue(ctrl);
            if (sm == null) return null;

            var stateProp = sm.GetType().GetProperty("State",
                BindingFlags.Public | BindingFlags.Instance);
            if (stateProp == null) return null;

            object val = stateProp.GetValue(sm);
            return val?.ToString();
        }

        /// <summary>
        /// Find the pin/cup world position in the active hole scene.
        ///
        /// Primary source: Golfin.Gameplay.UI.HUD.HoleContext.PinWorld — the canonical
        /// static-bus value set by PhysicsLabController when the hole scene loads (line 1492).
        /// Read via reflection to avoid a cross-assembly static reference.
        ///
        /// Fallback: recursive descendant-by-name walk for "Flag" in all scene roots,
        /// mirroring PhysicsLabController's own flag-search logic.
        /// </summary>
        public Vector3 FindCupPosition()
        {
            // Primary: read HoleContext.PinWorld via reflection.
            // HoleContext lives in Golfin.Gameplay.UI.asmdef (not Assembly-CSharp).
            Type holeCtxType = Type.GetType("Golfin.Gameplay.UI.HUD.HoleContext, Golfin.Gameplay.UI");
            if (holeCtxType != null)
            {
                FieldInfo pinField = holeCtxType.GetField("PinWorld",
                    BindingFlags.Public | BindingFlags.Static);
                if (pinField != null)
                {
                    var pw = (Vector3)pinField.GetValue(null);
                    if (pw != Vector3.zero)
                    {
                        LogStep($"FindCupPosition: HoleContext.PinWorld = {pw}");
                        return pw;
                    }
                }
            }

            // Fallback: recursive descendant walk for "Flag" (same search PhysicsLabController uses).
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    Transform flag = FindDescendantByName(root.transform, "Flag");
                    if (flag != null)
                    {
                        LogStep($"FindCupPosition: fallback 'Flag' descendant at {flag.position}");
                        return flag.position;
                    }
                }
            }

            LogStep("FindCupPosition WARN: HoleContext.PinWorld is zero and no 'Flag' descendant found — using Vector3.zero");
            return Vector3.zero;
        }

        /// <summary>Recursive depth-first search for the first descendant Transform with the given name.</summary>
        Transform FindDescendantByName(Transform root, string name)
        {
            if (root.name.Equals(name, StringComparison.OrdinalIgnoreCase)) return root;
            foreach (Transform child in root)
            {
                var found = FindDescendantByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // ── Mode-card primitives (practice_1v1_matchmaking_split) ────────────

        /// <summary>
        /// Snaps the mode carousel to centre the card with the given modeId, bypassing the
        /// swipe-drag animation so the bot doesn't have to simulate pointer events.
        ///
        /// Uses reflection to:
        ///   1. Find ModeCarouselController in the scene.
        ///   2. Read its _allCards list and _centeredVirtualIndex field.
        ///   3. Write _centeredVirtualIndex to the virtual slot whose ModeId matches.
        ///   4. Call private ApplyCardStates() to activate PLAY on the new centre card.
        ///
        /// Safe to call even if the carousel is already centred on modeId.
        /// Returns true if the carousel was found and the mode was set; false otherwise.
        /// </summary>
        public bool SnapCarouselToMode(string modeId)
        {
            LogStep($"SnapCarouselToMode: target='{modeId}'");

            Type carouselType = Type.GetType(
                "GolfinRedux.UI.ModeSelect.ModeCarouselController, Assembly-CSharp");
            if (carouselType == null)
            {
                LogStep("SnapCarouselToMode WARN: ModeCarouselController type not found via reflection");
                return false;
            }

            var carousel = UnityEngine.Object.FindObjectOfType(carouselType) as MonoBehaviour;
            if (carousel == null)
            {
                LogStep("SnapCarouselToMode WARN: no ModeCarouselController instance in scene");
                return false;
            }

            // Get the _allCards list.
            FieldInfo cardsField = carouselType.GetField("_allCards",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (cardsField == null)
            {
                LogStep("SnapCarouselToMode WARN: _allCards field not found");
                return false;
            }
            var allCards = cardsField.GetValue(carousel) as System.Collections.IList;
            if (allCards == null || allCards.Count == 0)
            {
                LogStep("SnapCarouselToMode WARN: _allCards is null or empty");
                return false;
            }

            // Find the virtual index with matching modeId (prefer the middle pass = pass 1).
            Type cardType = Type.GetType(
                "GolfinRedux.UI.ModeSelect.ModeCardController, Assembly-CSharp");
            PropertyInfo modeIdProp = cardType?.GetProperty("ModeId",
                BindingFlags.Public | BindingFlags.Instance);

            FieldInfo centeredField = carouselType.GetField("_centeredVirtualIndex",
                BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo dataCountField = carouselType.GetField("_dataCount",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (centeredField == null || dataCountField == null)
            {
                LogStep("SnapCarouselToMode WARN: _centeredVirtualIndex or _dataCount field not found");
                return false;
            }
            int dataCount = (int)dataCountField.GetValue(carousel);

            // Find first match in the middle pass (indices [dataCount .. 2*dataCount-1]).
            int targetVirtual = -1;
            for (int i = 0; i < allCards.Count; i++)
            {
                var card = allCards[i] as MonoBehaviour;
                if (card == null) continue;
                string id = modeIdProp?.GetValue(card) as string;
                if (id != null && id.Equals(modeId, StringComparison.OrdinalIgnoreCase))
                {
                    // Prefer middle pass slot.
                    if (i >= dataCount && i < 2 * dataCount)
                    {
                        targetVirtual = i;
                        break;
                    }
                    if (targetVirtual < 0) targetVirtual = i; // fallback to first match
                }
            }

            if (targetVirtual < 0)
            {
                LogStep($"SnapCarouselToMode MISS: no card with modeId='{modeId}' in _allCards");
                return false;
            }

            centeredField.SetValue(carousel, targetVirtual);
            LogStep($"SnapCarouselToMode: set _centeredVirtualIndex={targetVirtual} (total cards={allCards.Count}, dataCount={dataCount})");

            // Call private ApplyCardStates() to update SetCenter / PLAY visibility.
            MethodInfo applyMethod = carouselType.GetMethod("ApplyCardStates",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (applyMethod != null)
            {
                applyMethod.Invoke(carousel, null);
                LogStep("SnapCarouselToMode: ApplyCardStates() called — PLAY button should now be active on center card");
            }
            else
            {
                LogStep("SnapCarouselToMode WARN: ApplyCardStates() method not found; card states may be stale");
            }
            return true;
        }

        /// <summary>
        /// Find the PLAY button on the mode card whose ModeId matches <paramref name="modeId"/>
        /// (case-insensitive). ModeCardController is in Assembly-CSharp — accessed via reflection
        /// to avoid a cross-assembly static reference.
        ///
        /// Returns the Button component if found, null otherwise.
        /// </summary>
        public Button FindModeCardPlayButton(string modeId)
        {
            // ModeCardController lives in Assembly-CSharp (GolfinRedux.UI.ModeSelect namespace).
            Type cardType = Type.GetType(
                "GolfinRedux.UI.ModeSelect.ModeCardController, Assembly-CSharp");
            if (cardType == null)
            {
                LogStep($"FindModeCardPlayButton WARN: ModeCardController type not found via reflection");
                return null;
            }

            var cards = UnityEngine.Object.FindObjectsOfType(cardType) as MonoBehaviour[];
            if (cards == null || cards.Length == 0)
            {
                LogStep($"FindModeCardPlayButton WARN: no ModeCardController instances active");
                return null;
            }

            PropertyInfo modeIdProp = cardType.GetProperty("ModeId",
                BindingFlags.Public | BindingFlags.Instance);

            // The carousel uses a 3× virtual array — there may be multiple ModeCardController
            // instances with the same modeId. Only the center-card copy has its playButton active.
            // Scan all matching cards and return the first one whose playButton is active.
            FieldInfo playField = cardType.GetField("playButton",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (playField == null)
            {
                LogStep($"FindModeCardPlayButton WARN: 'playButton' field not found on {cardType.Name}");
                return null;
            }

            Button inactiveCandidate = null; // keep for debug log
            foreach (var card in cards)
            {
                string id = modeIdProp?.GetValue(card) as string;
                if (id == null || !id.Equals(modeId, StringComparison.OrdinalIgnoreCase))
                    continue;

                var btn = playField.GetValue(card) as Button;
                if (btn == null) continue;

                if (btn.gameObject.activeInHierarchy && btn.isActiveAndEnabled)
                {
                    LogStep($"FindModeCardPlayButton OK: modeId='{modeId}' → {btn.gameObject.name} (active)");
                    return btn;
                }
                // Record a found-but-inactive candidate for diagnostics.
                inactiveCandidate = btn;
            }

            if (inactiveCandidate != null)
                LogStep($"FindModeCardPlayButton WARN: all '{modeId}' cards found but playButton inactive/null — center card may not have settled yet");
            else
                LogStep($"FindModeCardPlayButton MISS: no active ModeCardController with modeId='{modeId}'");
            return null;
        }

        /// <summary>
        /// Click the PLAY button on the mode card whose ModeId == <paramref name="modeId"/>.
        /// Uses the same pointer-down/up + onClick.Invoke() pattern as <see cref="Click"/>.
        /// Waits settleSeconds (realtime) after clicking.
        /// </summary>
        public IEnumerator ClickModeCardPlay(string modeId, float settleSeconds = 1.5f)
        {
            LogStep($"ClickModeCardPlay: modeId='{modeId}'");

            // Ensure the carousel is centred on the requested mode so its PLAY button is active.
            // SnapCarouselToMode is instantaneous (no animation); wait one frame for Unity to
            // process the layout changes before reading the button state.
            SnapCarouselToMode(modeId);
            yield return null; // one-frame settle

            var btn = FindModeCardPlayButton(modeId);
            if (btn != null)
            {
                var ped = new PointerEventData(EventSystem.current);
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerDownHandler);
                yield return new WaitForSecondsRealtime(0.10f);
                ExecuteEvents.Execute(btn.gameObject, ped, ExecuteEvents.pointerUpHandler);
                btn.onClick.Invoke();
                LogStep($"  → clicked mode card PLAY for modeId='{modeId}'");
            }
            else
            {
                LogStep($"  → ClickModeCardPlay FAILED: no active PLAY button for modeId='{modeId}'");
            }
            yield return new WaitForSecondsRealtime(settleSeconds);
        }

        /// <summary>
        /// Returns true if the MatchMakingModal is currently visible.
        /// Delegates to the private IsModalVisible helper.
        /// </summary>
        public bool IsMatchMakingModalVisible()
            => IsModalVisible("MatchMakingModal");

        /// <summary>
        /// Returns true if the legacy NextHolePanel GameObject is currently active in the hierarchy.
        /// Used by the Cancel gate to verify the panel is NOT resurrected after matchmaking Cancel.
        /// Searches all transforms (includeInactive: true) so we can detect even if the parent is active.
        /// </summary>
        public bool IsNextHolePanelActive()
        {
            // Walk all MonoBehaviours (active+inactive) to find a GO named "NextHolePanel"
            var allMonos = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(includeInactive: true);
            foreach (var mono in allMonos)
            {
                if (mono.gameObject.name.Equals("NextHolePanel", StringComparison.OrdinalIgnoreCase))
                    return mono.gameObject.activeInHierarchy;
            }
            // Not found (normal in gameplay scenes where HomeScreen is not loaded)
            return false;
        }

        /// <summary>
        /// Poll until any loaded scene whose name starts with "Hole_" and ends with "_Geo"
        /// is found. Used by 1v1 scenarios where the exact hole number is random.
        /// </summary>
        public IEnumerator WaitForAnyHoleGeoScene(float timeoutSeconds = 40f)
        {
            LogStep($"WaitForAnyHoleGeoScene (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.isLoaded
                        && scene.name.StartsWith("Hole_", StringComparison.OrdinalIgnoreCase)
                        && scene.name.EndsWith("_Geo", StringComparison.OrdinalIgnoreCase))
                    {
                        LogStep($"  WaitForAnyHoleGeoScene OK: '{scene.name}' loaded after {elapsed:F1}s");
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            LogStep($"  WaitForAnyHoleGeoScene TIMEOUT: no Hole_*_Geo scene loaded after {timeoutSeconds}s");
        }

        // ── Spin-scenario primitives (spin_and_shot_shape_wiring) ─────────────

        /// <summary>
        /// Reset PhysicsLabController to tee position between strokes.
        /// Calls PhysicsLabController.ResetToTee() (public method, same asmdef — direct call).
        /// Logs [TeeDiag] prefix so LiveStatLogTee can capture tee-reset confirmations.
        /// </summary>
        public void ResetLabToTee()
        {
            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("[TeeDiag] ResetLabToTee FAIL: PhysicsLabController not found");
                return;
            }
            ctrl.ResetToTee();
            LogStep("[TeeDiag] ResetLabToTee OK: tee reset via PhysicsLabController.ResetToTee()");
        }

        /// <summary>
        /// Fire a driver shot (club index 0) at the given power via the production ShotController
        /// drag path (BeginExternalDrag → ramp SetExternalPower → EndExternalDrag → CommitFlick).
        /// This is the PRODUCTION path — SpinContext.Spin flows through CommitFlick to Build.
        ///
        /// Camera yaw is set toward the cup (HoleContext.PinWorld) so the shot launches
        /// in the correct direction.
        ///
        /// Waits for OnShotComplete (terminal=AtRest, InCup, or OB) up to timeoutSeconds.
        /// Returns the ShotResult via out-parameter for logging.
        /// </summary>
        /// <param name="spinInput">Explicit spin input (x=draw/fade, y=topspin/backspin).
        /// When non-zero, this overrides SpinContext.Spin so the spin value survives any
        /// state-transition resets that clear SpinContext between SetSpin and CommitFlick.</param>
        public IEnumerator FireDriverShot(float power01 = 1.0f, float timeoutSeconds = 20f,
            Vector2 spinInput = default)
        {
            LogStep($"FireDriverShot: power={power01:F2} (ShotController drag path)");

            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("  FireDriverShot FAIL: PhysicsLabController not found");
                yield break;
            }
            var shotCtl = UnityEngine.Object.FindObjectOfType<Golfin.Gameplay.Input.ShotController>();
            if (shotCtl == null)
            {
                LogStep("  FireDriverShot FAIL: ShotController not found — cannot use production path");
                yield break;
            }
            var sm = ctrl.BallSM;

            // 1. Select Driver (club 0) via production path (no lab bundle override).
            try
            {
                ctrl.SetClub(0);
                shotCtl.ClearStatBundleOverride();
                LogStep("  FireDriverShot: SetClub(0=Driver) + ClearStatBundleOverride");
            }
            catch (Exception ex)
            {
                LogStep($"  FireDriverShot WARN: SetClub failed: {ex.Message}");
            }

            // 2. Orient camera toward pin.
            Vector3 cup = FindCupPosition();
            Vector3 ball = ctrl.BallPosition;
            Vector3 flat = new Vector3(cup.x - ball.x, 0f, cup.z - ball.z);
            float yaw = Mathf.Atan2(flat.z, flat.x);
            try
            {
                ctrl.SetCameraYawRadians(yaw);
                LogStep($"  FireDriverShot: SetCameraYawRadians({yaw:F3}) toward cup={cup}");
            }
            catch (Exception ex)
            {
                LogStep($"  FireDriverShot WARN: SetCameraYawRadians failed: {ex.Message}");
            }

            // 3. Wait for ShotController to return to Idle.
            float si = 0f;
            while (shotCtl.State != Golfin.Gameplay.Input.ShotState.Idle && si < 4f)
            {
                si += Time.unscaledDeltaTime;
                yield return null;
            }
            LogStep($"  FireDriverShot: Idle gate done (waited {si:F2}s), State={shotCtl.State}");

            // 4. Gate on Aiming before firing.
            if (sm != null)
            {
                float g = 0f;
                while (sm.State != BallState.Aiming && g < 3f)
                {
                    g += Time.unscaledDeltaTime;
                    yield return null;
                }
                LogStep($"  FireDriverShot: Aiming gate done (waited {g:F2}s), SM={sm.State}");
            }

            // 5. Subscribe OnShotComplete BEFORE firing.
            bool done = false;
            ShotResult shotRes = default;
            Action<ShotResult> handler = r => { done = true; shotRes = r; };
            if (sm != null) sm.OnShotComplete += handler;

            // 6. Fire via production drag path (ramp power over ~0.85s).
            // Push spin to ShotController.PendingSpinInput before BeginExternalDrag.
            // Use the explicit spinInput arg if non-zero (avoids SpinContext reset race);
            // fall back to SpinContext.Spin for callers that don't supply explicit spin.
            Vector2 resolvedSpin = (spinInput != Vector2.zero)
                ? spinInput
                : Golfin.Gameplay.UI.HUD.SpinContext.Spin;
            shotCtl.PendingSpinInput = resolvedSpin;
            LogStep($"  FireDriverShot: PendingSpinInput set to {resolvedSpin} (spinInput={spinInput})");
            shotCtl.BeginExternalDrag();
            const float rampSeconds = 0.85f;
            float rt = 0f;
            while (rt < rampSeconds)
            {
                rt += Time.unscaledDeltaTime;
                shotCtl.SetExternalPower(Mathf.Lerp(0f, power01, rt / rampSeconds), 0f);
                yield return null;
            }
            shotCtl.SetExternalPower(power01, 0f);
            yield return new WaitForSecondsRealtime(0.18f);
            shotCtl.EndExternalDrag(); // → CommitFlick → ShotInputBuilder.Build reads PendingSpinInput
            LogStep($"  FireDriverShot: EndExternalDrag called (CommitFlick fires, PendingSpinInput flows to Build)");

            // 7. Wait for terminal state.
            float elapsed = 0f;
            while (!done && elapsed < timeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            if (sm != null) sm.OnShotComplete -= handler;

            if (done)
            {
                LogStep($"  FireDriverShot OK: terminal={shotRes.TerminalState} ball={ctrl.BallPosition:F1} after {elapsed:F1}s");

                // Log first-bounce (land) and final-rest positions separately from trajectory hits.
                // This lets parse_spinshape_captions (and manual analysis) separate carry from rollout.
                var traj = ctrl.LastTrajectory;
                if (traj != null && traj.terrainHits != null && traj.terrainHits.Count > 0)
                {
                    var firstHit = traj.terrainHits[0];
                    var lastHit  = traj.terrainHits[traj.terrainHits.Count - 1];
                    Vector3 landPos = new Vector3(
                        firstHit.Position.x.ToFloat(),
                        firstHit.Position.y.ToFloat(),
                        firstHit.Position.z.ToFloat());
                    Vector3 restPos = new Vector3(
                        lastHit.Position.x.ToFloat(),
                        lastHit.Position.y.ToFloat(),
                        lastHit.Position.z.ToFloat());
                    LogStep($"  [Land] ball=({landPos.x:F1}, {landPos.y:F1}, {landPos.z:F1}) surface={firstHit.Surface} bounceCount={traj.terrainHits.Count}");
                    if (traj.terrainHits.Count > 1)
                        LogStep($"  [Rest] ball=({restPos.x:F1}, {restPos.y:F1}, {restPos.z:F1}) surface={lastHit.Surface}");
                }
            }
            else
                LogStep($"  FireDriverShot TIMEOUT: OnShotComplete not fired in {timeoutSeconds}s");
        }
    }
}
#endif
