#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Golfin.Diagnostics.Runtime;

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
        /// Fire a shot via PhysicsLabController.Fire(). Builds a minimal ShotPreset
        /// aimed at worldTarget using the controller's current tee position as origin.
        /// power01 scales the default putt velocity. Waits until BallState reaches
        /// a terminal state (AtRest, InCup, OB) or timeoutSeconds elapses.
        /// </summary>
        public IEnumerator FireShot(Vector3 worldTarget, float power01 = 1f, float timeoutSeconds = 30f)
        {
            LogStep($"FireShot: target={worldTarget} power={power01:F2}");

            var ctrl = UnityEngine.Object.FindObjectOfType<PhysicsLabController>();
            if (ctrl == null)
            {
                LogStep("  FireShot FAIL: PhysicsLabController not found in scene");
                yield break;
            }

            // Build a direction vector from current ball position toward the target.
            var ballAnimator = UnityEngine.Object.FindObjectOfType<BallAnimator>();
            Vector3 origin = ballAnimator != null
                ? ballAnimator.transform.position
                : ctrl.transform.position;

            Vector3 dir = (worldTarget - origin).normalized;
            // Putter speed range ~1-5 m/s; scale by power.
            float speed = Mathf.Lerp(1f, 5f, power01);
            var velocity = new Golfin.Physics.Math.fp3(
                Golfin.Physics.Math.fp.FromFloat(dir.x * speed),
                Golfin.Physics.Math.fp.Zero,
                Golfin.Physics.Math.fp.FromFloat(dir.z * speed));

            var preset = new ShotPreset(
                id: "bot_shot",
                name: "Bot Shot",
                scene: PresetScene.Hole1,
                origin: new Golfin.Physics.Math.fp3(
                    Golfin.Physics.Math.fp.FromFloat(origin.x),
                    Golfin.Physics.Math.fp.FromFloat(origin.y),
                    Golfin.Physics.Math.fp.FromFloat(origin.z)),
                velocity: velocity,
                spin: default,
                wind: default,
                notes: "BotDriver auto-shot");

            ctrl.Fire(preset);
            LogStep($"  FireShot fired: origin={origin} dir={dir} speed={speed:F2}");

            // Wait for terminal state.
            yield return WaitForBallState("terminal", timeoutSeconds);
        }

        /// <summary>
        /// Wait until the BallStateMachine reaches any terminal state (AtRest, InCup, OB),
        /// or specifically the named state. stateName="terminal" matches any of the three.
        /// Reads BallSM via PhysicsLabController.BallSM (internal property) using reflection.
        /// </summary>
        public IEnumerator WaitForBallState(string stateName, float timeoutSeconds = 30f)
        {
            LogStep($"WaitForBallState: {stateName} (timeout={timeoutSeconds}s)");
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                string current = GetBallStateName();
                bool matched = stateName.Equals("terminal", StringComparison.OrdinalIgnoreCase)
                    ? (current == "AtRest" || current == "InCup" || current == "OB")
                    : (current != null && current.Equals(stateName, StringComparison.OrdinalIgnoreCase));

                if (matched)
                {
                    LogStep($"  WaitForBallState OK: state='{current}' after {elapsed:F1}s");
                    yield break;
                }
                yield return new WaitForSecondsRealtime(0.5f);
                elapsed += 0.5f;
            }
            LogStep($"  WaitForBallState TIMEOUT: '{stateName}' not reached after {timeoutSeconds}s. Current={GetBallStateName()}");
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
        /// Find the pin/cup position in the active hole scene.
        /// Searches for common names: Pin, Flag, Cup, FlagGO, PinTransform, CupMarker.
        /// Returns Vector3.zero if nothing found.
        /// </summary>
        public Vector3 FindCupPosition()
        {
            string[] candidates = { "Pin", "Flag", "Cup", "FlagGO", "PinTransform",
                                    "CupMarker", "CupCenter", "HolePin" };
            foreach (var name in candidates)
            {
                var go = GameObject.Find(name);
                if (go != null)
                {
                    LogStep($"FindCupPosition: found '{name}' at {go.transform.position}");
                    return go.transform.position;
                }
            }

            // Fallback: any GO with "pin" or "flag" or "cup" in its name.
            var allGos = UnityEngine.Object.FindObjectsOfType<GameObject>(includeInactive: false);
            foreach (var go in allGos)
            {
                string n = go.name.ToLowerInvariant();
                if (n.Contains("pin") || n.Contains("flag") || n.Contains("cup"))
                {
                    LogStep($"FindCupPosition: fuzzy match '{go.name}' at {go.transform.position}");
                    return go.transform.position;
                }
            }

            LogStep("FindCupPosition WARN: no pin/flag/cup GO found — using Vector3.zero");
            return Vector3.zero;
        }
    }
}
#endif
