# loop_v2_smoke_bot — Production-flow playthrough bot framework

**Parent scope:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (inserted between Stage C0 and C1)
**Task type:** TELLCODE — pattern matches existing `SmokeRunner2fHost` template; but scope is broader (reusable framework, not single-scenario script)
**Notion:** GOLFIN_Roadmap — Order 335
**Status:** SPEC_READY (revised 2026-05-19 per Cesar: bot must drive ANY UI like a real player, not just play-through-to-cup)

---

## Goal

A **reusable bot framework** that drives the production app like a real player would. Two layers:

1. **Driver layer** (the framework) — primitives for: clicking buttons by name/path, waiting for screens/modals to appear, reading UI state, firing shots, capturing screenshots. Reusable, no scenario logic baked in.
2. **Scenario layer** — a thin script that composes the primitives into a specific test (e.g. "Hole 1 playthrough to cup", "open Settings from Roster, navigate accordion, close", "select Hole 3 in Hole Selection grid then back out"). Each Loop v2 stage's visual gate gets its own scenario.

After this ships:
- `LoopV2SmokeBot` host MonoBehaviour runs the **Driver**.
- One menu item per scenario, e.g. `GOLFIN/Smoke/Loop v2/Hole 1 Playthrough`, `GOLFIN/Smoke/Loop v2/Settings Round Trip`.
- Each scenario reads as a flat sequence of high-level steps: `Click("PLAY")`, `WaitForScreen(ScreenId.Loading)`, `Capture("s02_loading")`, etc.
- New scenarios for Stage C1/D/E/F are 30-50 lines each, not 250.

---

## Pre-flight (implementer logs in IMPLEMENTER_REPORT.md)

1. **Confirm CaptureCore canonical path.** Bot runs in PlayMode — `CaptureCore.SnapPlayModeSafe` sanctioned. Verify:
   ```
   grep -n 'public static.*SnapPlayModeSafe\|public static.*SnapAtEndOfFrameAndPause' Assets/Scripts/Capture/CaptureCore.cs
   ```

2. **Map the production UI surface.** The Driver needs to find UI elements. Two strategies, pick one based on what's available:
   - **(a) Name-based** — `Click("PLAY")` finds a `Button` whose GameObject is named "PlayButton" or whose child TMP_Text contains "PLAY". Cheap, fragile to renames.
   - **(b) Tag-based** — add `[BotIdentifier("play_button")]` attribute to known buttons, Driver finds via reflection or a registry.
   My take: **(a) name-based with TMP_Text fallback** for v1. (b) is over-engineered until we have 20+ buttons.

3. **Identify fire-to-cup test seam.** Reuse §2f's `BallAnimator.PlayRate=Instant` lesson + whatever public putt-fire method exists. Grep:
   ```
   grep -n 'public.*Fire\|public.*ForSmokeRunner\|public.*SetAim' Assets/Scripts/Physics/Viewer/PhysicsLabController.cs | head -20
   ```

4. **Confirm `MatchmakingModalController` exposes state.** Driver needs to wait for OPPONENT_FOUND. Check:
   ```
   grep -n 'public.*State\|public.*IsFound\|private.*_state' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs
   ```
   If only private, add minimal public `MatchmakingState State { get; }` getter (flag in report).

---

## Architecture

**Three files**, all `#if UNITY_EDITOR` guarded, all in `Golfin.Physics.Viewer` asmdef (matches §2c-§2f).

### File 1: `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` (the framework)

A reusable instance, not a singleton. Held by `LoopV2SmokeBot`. Pure primitives, no scenario logic.

```csharp
#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Golfin.Physics.Viewer.Bot
{
    /// <summary>
    /// Reusable bot primitives. Scenarios compose these into specific test flows.
    /// All waits use realtime to survive Unity-MCP frozen-time sessions.
    /// All captures go through CaptureCore.SnapPlayModeSafe (no Lesson K).
    /// </summary>
    public class BotDriver
    {
        readonly string _captureDir; // tasks/<scenario>/screenshots/
        readonly System.Text.StringBuilder _log = new();
        int _captureCounter = 1;

        public BotDriver(string captureDir) { _captureDir = captureDir; }
        public string Log => _log.ToString();

        // ── UI primitives ──────────────────────────────────────────────────

        /// <summary>Find a Button by GameObject name (case-insensitive) or by TMP_Text child contents.</summary>
        public Button FindButton(string nameOrText) { /* implementation */ }

        /// <summary>Click a button found by FindButton. Logs success/miss.</summary>
        public IEnumerator Click(string nameOrText, float settleSeconds = 0.5f) { /* invoke onClick, wait settle */ }

        /// <summary>Poll until a screen (by ScreenId or GameObject-name root) is the active screen.</summary>
        public IEnumerator WaitForScreen(string screenNameOrId, float timeoutSeconds = 10f) { /* poll */ }

        /// <summary>Poll until a modal (by name) is visible (CanvasGroup.alpha > 0.9 or GameObject active).</summary>
        public IEnumerator WaitForModalVisible(string modalName, float timeoutSeconds = 10f) { /* poll */ }

        /// <summary>Poll until a modal closes (CanvasGroup invisible or GameObject inactive).</summary>
        public IEnumerator WaitForModalHidden(string modalName, float timeoutSeconds = 10f) { /* poll */ }

        /// <summary>Poll until a scene by name is loaded (additive).</summary>
        public IEnumerator WaitForSceneLoaded(string sceneName, float timeoutSeconds = 15f) { /* poll */ }

        /// <summary>Poll until any GameObject by name appears in the active scene.</summary>
        public IEnumerator WaitForGameObject(string goName, float timeoutSeconds = 10f) { /* poll */ }

        /// <summary>Poll a custom predicate.</summary>
        public IEnumerator WaitFor(System.Func<bool> predicate, string description, float timeoutSeconds = 10f) { /* poll */ }

        /// <summary>Type into a TMP_InputField found by name.</summary>
        public IEnumerator TypeInto(string inputFieldName, string text) { /* implementation */ }

        /// <summary>Read TMP_Text contents by name.</summary>
        public string ReadText(string textName) { /* implementation */ }

        /// <summary>Drag a slider to value. For settings sliders, audio sliders, etc.</summary>
        public IEnumerator SetSliderValue(string sliderName, float value) { /* implementation */ }

        /// <summary>Toggle a Toggle by name.</summary>
        public IEnumerator SetToggle(string toggleName, bool on) { /* implementation */ }

        // ── Gameplay primitives ────────────────────────────────────────────

        /// <summary>Fire a putt/shot via PhysicsLabController test seam, towards a world target.</summary>
        public IEnumerator FireShot(Vector3 worldTarget, float power01 = 1f, float timeoutSeconds = 25f) { /* implementation */ }

        /// <summary>Wait until ball state reaches a target BallState.</summary>
        public IEnumerator WaitForBallState(string stateName, float timeoutSeconds = 25f) { /* implementation */ }

        // ── Capture + log ──────────────────────────────────────────────────

        /// <summary>Capture a screenshot via the canonical path. Auto-prefixes counter (s01, s02, ...).</summary>
        public IEnumerator Capture(string label) { /* CaptureCore.SnapPlayModeSafe */ }

        /// <summary>Log a step to the history buffer (timestamped).</summary>
        public void LogStep(string message) { /* append with realtime timestamp */ }

        /// <summary>Flush the log buffer to disk at scenario end.</summary>
        public void FlushLog(string filename = "history.log") { /* write _log to _captureDir/filename */ }
    }
}
#endif
```

### File 2: `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` (host MonoBehaviour)

Mirrors `SmokeRunner2fHost`'s lifecycle pattern. Holds a `BotDriver` instance + a `ScenarioKey` from SessionState, dispatches to the right scenario coroutine.

```csharp
#if UNITY_EDITOR
using System.Collections;
using UnityEngine;
using Golfin.Physics.Viewer.Bot;

namespace Golfin.Physics.Viewer
{
    public class LoopV2SmokeBot : MonoBehaviour
    {
        const string ArmedKey = "LoopV2SmokeBot.Armed";
        const string ScenarioKey = "LoopV2SmokeBot.Scenario";
        const float StartupWait = 5f;

        public static bool Armed
        {
            get => UnityEditor.SessionState.GetBool(ArmedKey, false);
            set => UnityEditor.SessionState.SetBool(ArmedKey, value);
        }

        public static string Scenario
        {
            get => UnityEditor.SessionState.GetString(ScenarioKey, "");
            set => UnityEditor.SessionState.SetString(ScenarioKey, value);
        }

        void Start()
        {
            if (!Armed) { Debug.LogWarning("[LoopV2SmokeBot] Not armed — destroying self."); Destroy(this); return; }
            Armed = false;
            if (Time.timeScale < 0.01f) Time.timeScale = 1f;
            StartCoroutine(SafeRun());
        }

        IEnumerator SafeRun()
        {
            yield return new WaitForSecondsRealtime(StartupWait);
            var captureDir = $"tasks/loop_v2_smoke_bot/{Scenario}/screenshots";
            var driver = new BotDriver(captureDir);

            System.Exception caught = null;
            bool completed = false;
            try {
                switch (Scenario) {
                    case "hole1_playthrough":      yield return Scenarios.Hole1Playthrough(driver); break;
                    case "settings_round_trip":    yield return Scenarios.SettingsRoundTrip(driver); break;
                    case "hole_selection_browse":  yield return Scenarios.HoleSelectionBrowse(driver); break;
                    // future: result_modal_play_next, menu_exit, etc.
                    default: Debug.LogError($"[LoopV2SmokeBot] Unknown scenario: {Scenario}"); break;
                }
                completed = true;
            } catch (System.Exception ex) { caught = ex; }

            if (caught != null) driver.LogStep($"EXCEPTION: {caught}");
            if (!completed) driver.LogStep("INCOMPLETE — scenario aborted");
            driver.FlushLog();
            Destroy(this);
        }
    }
}
#endif
```

### File 3: `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` (scenario library)

Static class. One coroutine per scenario. **Three scenarios at ship**:

```csharp
#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

namespace Golfin.Physics.Viewer.Bot
{
    public static class Scenarios
    {
        /// <summary>
        /// SCENARIO 1: Cold launch → PLAY → matchmaking → loading → Hole 1 → fire putt → InCup → result modal.
        /// The default visual gate for Stage C1.
        /// </summary>
        public static IEnumerator Hole1Playthrough(BotDriver d)
        {
            d.LogStep("=== Hole 1 Playthrough ===");
            yield return d.WaitForScreen("Home");
            yield return d.Capture("home");

            yield return d.Click("PLAY");
            yield return d.WaitForModalVisible("MatchMakingModal");
            yield return d.Capture("matchmaking_searching");

            yield return d.WaitFor(() => MatchmakingState() == "OpponentFound", "opponent_found", 15f);
            yield return d.Capture("opponent_found");

            yield return d.WaitForSceneLoaded("LabScaffold");
            yield return d.WaitForSceneLoaded("Hole_01_Geo");
            yield return new WaitForSecondsRealtime(2f); // settle
            yield return d.Capture("gameplay_armed");

            yield return d.FireShot(FindCupPosition(), power01: 0.65f);
            yield return d.WaitForBallState("InCup", 25f);
            yield return d.Capture("ball_in_cup");

            yield return new WaitForSecondsRealtime(2f); // result modal animate-in
            yield return d.Capture("result_modal");
        }

        /// <summary>
        /// SCENARIO 2: Home → Settings → expand accordion → close. Smoke for Stage A's surviving settings flow.
        /// </summary>
        public static IEnumerator SettingsRoundTrip(BotDriver d)
        {
            d.LogStep("=== Settings Round Trip ===");
            yield return d.WaitForScreen("Home");
            yield return d.Capture("home");
            yield return d.Click("settings"); // top-bar settings icon
            yield return d.WaitForGameObject("SettingsScreen");
            yield return d.Capture("settings_open");
            yield return d.Click("Audio"); // expand the accordion section
            yield return new WaitForSecondsRealtime(0.5f);
            yield return d.Capture("settings_audio_expanded");
            yield return d.Click("Close");
            yield return d.WaitForScreen("Home");
            yield return d.Capture("home_returned");
        }

        /// <summary>
        /// SCENARIO 3: Home → Hole Selection bottom-nav → expand card 3 → back to Home.
        /// Smoke for Stage E's hole-selection entry point.
        /// </summary>
        public static IEnumerator HoleSelectionBrowse(BotDriver d)
        {
            d.LogStep("=== Hole Selection Browse ===");
            yield return d.WaitForScreen("Home");
            yield return d.Click("TeeButton"); // bottom-nav tee/hole-selection
            yield return d.WaitForScreen("HoleSelection");
            yield return d.Capture("hole_selection_grid");
            yield return d.Click("HoleCard_03");
            yield return new WaitForSecondsRealtime(0.8f); // expand animation
            yield return d.Capture("hole_03_expanded");
            yield return d.Click("HomeButton");
            yield return d.WaitForScreen("Home");
            yield return d.Capture("home_returned");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        static string MatchmakingState() { /* read MatchmakingModalController.Instance.State.ToString() */ }
        static Vector3 FindCupPosition() { /* find FlagGO or PinTransform in active hole scene */ }
    }
}
#endif
```

### File 4: `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` (launcher)

One menu item per scenario. Each menu method just sets `LoopV2SmokeBot.Scenario`, opens `ShellScene.unity`, attaches the host, arms, enters play mode.

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Golfin.Physics.Viewer.Editor
{
    public static class LoopV2SmokeBotMenu
    {
        const string ShellScenePath = "Assets/Scenes/ShellScene.unity";

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole 1 Playthrough")]
        public static void RunHole1Playthrough() => Launch("hole1_playthrough");

        [MenuItem("GOLFIN/Smoke/Loop v2/Settings Round Trip")]
        public static void RunSettingsRoundTrip() => Launch("settings_round_trip");

        [MenuItem("GOLFIN/Smoke/Loop v2/Hole Selection Browse")]
        public static void RunHoleSelectionBrowse() => Launch("hole_selection_browse");

        static void Launch(string scenarioKey)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[LoopV2SmokeBotMenu] Stop play first."); return; }

            var shell = EditorSceneManager.OpenScene(ShellScenePath, OpenSceneMode.Single);
            if (!shell.IsValid()) { Debug.LogError("[LoopV2SmokeBotMenu] Failed to open ShellScene."); return; }

            var go = new GameObject("[LoopV2SmokeBot]");
            go.AddComponent<Viewer.LoopV2SmokeBot>();

            Viewer.LoopV2SmokeBot.Scenario = scenarioKey;
            Viewer.LoopV2SmokeBot.Armed = true;

            EditorApplication.delayCall += () =>
            {
                EditorSceneManager.SaveScene(shell);
                EditorApplication.EnterPlaymode();
            };
        }
    }
}
#endif
```

---

## Why a framework, not a hard-coded sequence

- **Loop v2 has 4 more stages** after C1. Each carries a Cesar visual gate. If each gate is its own ~250-line bespoke bot, that's 1000+ lines of throwaway. With the framework, each scenario is 30-50 lines.
- **Real player simulation needs real UI primitives.** Hard-coded "click PlayButton, wait 5s, click another button" doesn't generalize. `Click(label)` + `WaitForScreen` + `WaitForModalVisible` does.
- **Future-proofs UI changes.** If Stage F's polish renames buttons, only `FindButton`'s lookup table changes — not every scenario.
- **Lets us write smoke for non-gameplay flows** — Settings round-trips, Hole Selection browsing, future Gacha/Shop screens. Manual play burden drops across the entire app, not just Loop v2.

---

## Scope

### Files CREATED

- `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` (~350 lines, the framework)
- `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` (~80 lines, host MonoBehaviour)
- `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` (~150 lines, 3 scenarios)
- `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` (~60 lines, 3 menu items)

### Files POTENTIALLY EDITED (only if test seam missing — minimal)

- `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` — public `State` getter if not already exposed.
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — public putt-fire seam if not already exposed.

Everything else MUST go through existing public APIs. If the implementer finds itself adding test seams beyond these two, **escalate** before continuing.

### Files DELETED

None.

---

## Implementation steps

1. **Pre-flight checks** (above), log results in IMPLEMENTER_REPORT §1.
2. **Create `BotDriver.cs`** — all primitives. Stub-out gameplay primitives if test seams are missing; flag and continue.
3. **Create `LoopV2SmokeBot.cs`** — host with armed-flag dispatch.
4. **Create `Scenarios.cs`** — three scenarios at ship.
5. **Create `LoopV2SmokeBotMenu.cs`** — three menu items.
6. **Test seam audit** — only if pre-flight found missing API, add the minimal seam.
7. **Compile clean.**
8. **Run each of the three scenarios locally** via the new menu. Each scenario produces ~5-7 MD5-distinct PNGs + a `history.log`.
9. **Commit captures** under `tasks/loop_v2_smoke_bot/<scenario>/screenshots/`.
10. **Commit + push.** Message: `loop_v2_smoke_bot: bot framework + 3 scenarios`

---

## Definition of Done

**Audit grep:**
- [ ] `ls Assets/Scripts/Physics/Viewer/Bot/` → BotDriver.cs, LoopV2SmokeBot.cs, Scenarios.cs, Editor/LoopV2SmokeBotMenu.cs
- [ ] `grep -c '#if UNITY_EDITOR' Assets/Scripts/Physics/Viewer/Bot/*.cs Assets/Scripts/Physics/Viewer/Bot/Editor/*.cs` → all four guarded
- [ ] `grep -c 'CaptureCore.SnapPlayModeSafe\|SnapAtEndOfFrameAndPause' Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` → at least one hit (Capture method)
- [ ] `grep -c '\[MenuItem' Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` → 3 hits
- [ ] Project compiles clean
- [ ] EditMode test gate **305/305 PASS** unchanged

**Self-evidence** (one capture set per scenario):
- [ ] `tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/` — 6 MD5-distinct PNGs + history.log
- [ ] `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/` — 4 MD5-distinct PNGs + history.log
- [ ] `tasks/loop_v2_smoke_bot/hole_selection_browse/screenshots/` — 3 MD5-distinct PNGs + history.log
- [ ] Each `history.log` ends with `=== Scenario complete ===` (not `INCOMPLETE`)

_Note (iter-2 edit): PNG counts corrected from 7/5/5 to 6/4/4 to match scenario code verbatim. HoleSelection scenario reworked to drive CardTapButton collapse (Hole 1 auto-expands; no collapsed→expanded drive possible with only Hole 1 unlocked). ARCHITECT_REVIEW.md §PNG-count calls this a spec-bookkeeping edit, not a functional change._

_Note (iter-3 edit): hole_selection_browse count further corrected from 4 to 3. CardTapButton is ambiguous (18 matches across all HoleCard prefab instances); collapse attempt produced byte-identical s02/s03 (iter-2 ARCHITECT_REVIEW_FAIL §4). Honest fix: 3-capture flow (home → hole_selection_grid → home_returned). TODO: extend to 4+ captures when Stage E unlocks Hole 2+._

**Cesar visual gate:** light. Review the three capture sets + logs. If each scenario captures look right, approve. No manual play required.

---

## Handoff

**Implementer:** Claude Code (TELLCODE).
**Spec:** `Docs/Specs/Active/loop_v2_smoke_bot/SPEC.md`
**Architect-side close:** STATUS.md → DONE, move to `Docs/Specs/Completed/`. Flip Notion entry to Done.

**Reusability contract:** From now on, every Loop v2 stage's visual gate includes "add a scenario to `Scenarios.cs` covering the new UI surface; bot must pass before Cesar visual gate." Bot framework is the default acceptance evidence path. Cesar plays manually only when the bot can't reach a flow (rare).

---

## Out of scope (deferred)

- **CI integration** — `-batchmode -executeMethod` runner is its own future spec when CI exists.
- **Performance / fps captures** — bot captures PNGs, not perf traces. Profiler integration is its own future spec.
- **Crash diagnostics in bot** — if the bot crashes mid-scenario, it logs and self-destructs. No crash-dump capture.
- **Production builds** — `#if UNITY_EDITOR` guarded; never ships in player builds.
- **Random / fuzz scenarios** — every scenario is deterministic. A fuzz harness is its own future spec.
- **Multi-hole runs in `Hole1Playthrough`** — Stage E may parameterize the scenario to `HoleNPlaythrough(int)`.
- **PLAY NEXT / MENU / RETRY button drives** — added to Scenarios.cs in Stage D when those buttons exist.
- **FAILED-state path** — add a `Hole1FailedPlaythrough` scenario when needed.

---

## Risk register

| # | Risk | Mitigation |
|---|---|---|
| 1 | `FindButton("PLAY")` returns the wrong button (multiple matches) | Driver logs all matches; throws if >1 match; scenario uses more specific name (e.g. "PlayButton_Home") |
| 2 | TMP_Text contents change with localization | Pre-flight verifies button GameObject names exist as a fallback; scenarios prefer name over text where possible |
| 3 | Realtime wait timeouts too short on slow machines | All timeouts ≥10s. `StartupWait=5s` from §2f. Implementer can extend per scenario if needed |
| 4 | `MatchmakingModalController.State` private | Add minimal public getter; flag in IMPLEMENTER_REPORT §2 |
| 5 | Bot Test seam additions creep | If >2 production files touched for seams, escalate to architect |
| 6 | Self-destruct races with capture write | Driver's `FlushLog` is sync; `Capture` coroutine yields after write completes |
| 7 | Bot can't drive a UI element (e.g. drag-to-aim shot) | Scenario falls back to test-seam direct fire; flag the missing primitive for a future spec |
