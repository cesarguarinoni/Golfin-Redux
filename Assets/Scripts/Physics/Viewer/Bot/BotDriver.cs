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
            // Yield one frame so Unity flushes any pending render commands.
            yield return null;

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
        /// Click a button found by FindButton. Logs success or miss. Waits settleSeconds
        /// (realtime) after invoking onClick so the UI can respond before the next step.
        /// </summary>
        public IEnumerator Click(string nameOrText, float settleSeconds = 0.8f)
        {
            LogStep($"Click: '{nameOrText}'");
            var btn = FindButton(nameOrText);
            if (btn != null)
            {
                btn.onClick.Invoke();
                LogStep($"  → clicked {btn.gameObject.name}");
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

            // 1. Switch to putter.
            try
            {
                ctrl.SetClub(PhysicsLabController.PutterIndex);
                LogStep($"  FireShot: SetClub(PutterIndex={PhysicsLabController.PutterIndex})");
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
        public IEnumerator PlayHoleToCup(int par)
        {
            int maxStrokes = par + 3;
            LogStep($"=== PlayHoleToCup: par={par}, stroke cap={maxStrokes} ===");

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

                SelectShot(dist, isFirstStroke, out int club, out float power01, out string label);
                isFirstStroke = false;

                LogStep($"Stroke {strokes}: ball={ball:F1} cup={cup:F1} dist={dist:F1}m — {label} club={club} power={power01:F2}");

                try { ctrl.SetClub(club); } catch (Exception ex) { LogStep($"  SetClub warn: {ex.Message}"); }

                float yaw = Mathf.Atan2(flat.z, flat.x);
                try { ctrl.SetCameraYawRadians(yaw); }
                catch (Exception ex) { LogStep($"  SetCameraYaw warn: {ex.Message}"); }

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
        /// Select club and power for a shot of the given horizontal distance (metres).
        ///
        /// Fix #2 (Cesar 2026-05-20): Driver ONLY on the first stroke. For subsequent strokes:
        ///   dist > 90m   → Wedge  (club 2)  at ~70% power  → aim to land in front of green (~55-65m)
        ///   dist > 40m   → Wedge  (club 2)  at reduced power → ~40-55m carry
        ///   dist > 15m   → Wedge  (club 2)  at low power → 15-40m carry
        ///   dist > 6m    → Putter (club 3)  at scaled power → long putt
        ///   dist <= 6m   → Putter (club 3)  at scaled power → short putt
        ///
        /// Strategy rationale (iter-5, 2026-05-20): After iter-4 playthrough, the ball landed
        /// in sand ~118m from cup. Iron 7 shots from there all went OB because the trajectory
        /// overshot the green (no safe terrain hit → OB drop back to origin → infinite loop).
        /// Root cause: Iron 7 from sand at 84% power carries ~100-120m, overshoots the
        /// green boundary into OB. Fix: use Wedge (max carry ~91m) for all post-driver
        /// approach shots. Power = dist/130 gives conservative carry well short of OB.
        ///
        /// Power tuning reference (StatBundle production path, not preset path):
        ///   Wedge base 42 m/s; carry scales as power^1.8 approximately.
        ///   power=0.70 → ~0.55 * 91m ≈ 50m carry
        ///   power=0.85 → ~0.75 * 91m ≈ 68m carry
        ///   power=1.00 → 91m carry (full)
        ///
        /// Club indices: 0=Driver, 1=Iron 7, 2=Wedge, 3=Putter (PhysicsLabController.PutterIndex)
        /// </summary>
        void SelectShot(float dist, bool isFirstStroke, out int club, out float power01, out string label)
        {
            int putter = PhysicsLabController.PutterIndex;

            // First stroke: always Driver at full power (~250m carry for Par 5).
            if (isFirstStroke)
            {
                club    = 0;
                power01 = 1.0f;
                label   = $"Driver full power (first stroke, dist={dist:F0}m to cup)";
                return;
            }

            // Subsequent strokes: use Wedge for all approach shots (avoids OB overshoot).
            if (dist > 40f)
            {
                // Approach: Wedge at conservative power.
                // Target: carry ~65% of remaining distance, leaving next shot in good range.
                // Clamp to 0.75 to avoid overshooting green into OB.
                // Wedge full power carry ~91m. power=0.75 → ~0.75^1.8 * 91 ≈ 55m carry.
                // dist=118m → power=0.75 (cap) → carry~55m → 63m left → chip + putt.
                // dist=60m  → power=0.60        → carry~35m → 25m left → chip.
                club    = 2;
                power01 = Mathf.Min(Mathf.Clamp01(dist / 130f), 0.75f);
                label   = $"Wedge approach (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 15f)
            {
                // Short approach: Wedge at low power.
                // power = dist / 80 → 40m → 0.50 (≈23m carry), 15m → 0.19 (≈7m carry)
                club    = 2;
                power01 = Mathf.Clamp01(dist / 80f);
                label   = $"Wedge chip (dist={dist:F0}m) power={power01:F2}";
            }
            else if (dist > 6f)
            {
                // Long putt: Putter. Putter base 5 m/s; scale conservatively.
                club    = putter;
                power01 = Mathf.Clamp01(dist / 18f);
                label   = $"Putter long putt (dist={dist:F0}m) power={power01:F2}";
            }
            else
            {
                // Short putt: Putter at moderate power.
                club    = putter;
                power01 = Mathf.Clamp01(dist / 8f);
                label   = $"Putter short putt (dist={dist:F0}m) power={power01:F2}";
            }
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
    }
}
#endif
