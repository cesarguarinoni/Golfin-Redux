# Stage C0 — Matchmaking → Gameplay Transition

**Parent scope:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (Stage C splits into C0 + C1)
**Root cause ref:** `Docs/Specs/Queued/ARCHITECT_NOTE_matchmaking_to_gameplay_transition.md`
**Audit refs:** `Docs/Architecture/CODE_AUDIT_2026-05-19.md` (Stage B finding)
**Task type:** TELLCODE — established patterns (additive scene load already exists in `PhysicsLabController.LoadHole`); but Cesar-visual gate is heavier than typical TELLCODE (first end-to-end production playthrough)
**Notion:** GOLFIN_Roadmap — new entry (Stage C0 sub-item of Loop v2)
**Status:** SPEC_READY

---

## Goal

Close the OPPONENT FOUND → ball-at-tee gap. After this stage:
- Player taps PLAY on Home or Hole Selection → matchmaking modal → OPPONENT FOUND seeds `GameSession` (Stage B) → **loading screen with tips appears** → gameplay scene loads additively → ShellScene UI hides → ball spawns at the seeded hole's tee → player can play the hole.
- After ball reaches `InCup`, the existing lab `HoleCompleteWidget` fires (no change from Stage B); the ShellScene Result modal is Stage C1's job.

This is the **first end-to-end production playthrough** in the project's history. Cesar's visual gate is unavoidably heavy.

---

## Architecture decisions (locked, with rationale)

These are the three real choices C0 is making. Cesar may course-correct, but I'm picking defaults to keep momentum:

### D1. Gameplay host scene: **`LabScaffold.unity`**
- It's already the canonical host scene used by every smoke runner and lab capture system.
- `PhysicsLabController.LoadHole(n)` already does the additive `Hole_NN_Geo.unity` load. Production gets the same path the lab uses today.
- Renaming `LabScaffold.unity` → `GameplayScene.unity` is **deferred polish** (post-Loop-v2). The name "lab" is internal-facing; the user never sees it.
- The existing empty `GameplayScene.unity` stub from March 2026 is **left alone** (might be deleted in a later cleanup stage; not C0's job).

**Alternative considered:** `PhysicsLab_Hole1.unity` (older, tied to Hole 1 only — would block holes 2-18). Rejected.

### D2. Scene load mode: **Additive** (`LoadSceneMode.Additive`)
- ShellScene survives the load. PersistentUIManager + ScreenManager + SettingsController + Matchmaking modal all stay alive (no DontDestroyOnLoad scramble).
- Aligns with Loop v2 scoping Q3 (locked Option B — ShellScene-resident Result modal). Stage C1 will subscribe to `GameSession.OnHoleComplete` from the ShellScene-resident modal; additive load is what makes that possible.
- On hole exit (PLAY NEXT or MENU in Stage D), `SceneManager.UnloadSceneAsync(LabScaffold)` tears down gameplay; ShellScene continues.

**Alternative considered:** `Single` mode with `DontDestroyOnLoad` markers. Rejected — fragile, every ShellScene singleton needs a DDoL audit.

### D3. Holes 2-18 strategy: **Wire all 18 via existing `LoadHole(n)`**
- `PhysicsLabController.LoadHole(n)` already takes a hole number and loads `Hole_NN_Geo.unity` from `Assets/Golf/Courses/lomond-country-club/Generated/`.
- C0 calls it with `GameSession.CurrentHoleNumber`. If a hole produces a broken playthrough (missing pin, broken collision, etc.), that's a content/data bug to fix on the hole, not a scene-architecture issue.
- Cesar's visual gate covers Hole 1 only. Holes 2-18 are "should work, file bugs if not" — content QA is deferred to a later content stage.

**Alternative considered:** C0 ships Hole 1 only, other holes refuse on PLAY. Rejected — `LoadHole(n)` already supports all 18, gating them is more code than letting them work.

---

## Pre-flight (implementer logs in IMPLEMENTER_REPORT.md)

1. **Verify `LabScaffold.unity` is loadable from runtime** (not just editor):
   ```
   grep -n 'GUID' Assets/Scenes/Physics/LabScaffold.unity.meta
   ```
   Capture the GUID. It must be added to `ProjectSettings/EditorBuildSettings.asset` — if it isn't, runtime `SceneManager.LoadSceneAsync` will fail with "scene not in build settings."

2. **Verify the missing `Hole_01.unity` in build settings is safe to remove**:
   ```
   grep -B1 -A2 'Hole_01.unity' ProjectSettings/EditorBuildSettings.asset
   ```
   The entry has `guid: 00000000000000000000000000000000` — ghost reference. Confirm zero references in code:
   ```
   grep -rn '"Hole_01"' Assets/Scripts/ | grep -v 'Hole_01_Geo'
   ```
   Should be zero or only test/editor refs. Remove the ghost build-settings entry as part of this stage.

3. **Verify ShellScene UI elements that must hide during gameplay**:
   - Bottom nav bar (PersistentUIManager) — should be hidden while gameplay scene is active.
   - Top bar (player name, RP counter on HomeScreen) — N/A, Home isn't the active screen during gameplay.
   - Matchmaking modal — already closes itself after OPPONENT FOUND fade-out.
   List in IMPLEMENTER_REPORT.md what's in scene and what gets hidden.

4. **Verify `PhysicsLabController` startup hole-number source**:
   ```
   grep -nE 'currentHole|_holeNumber|HoleContext.HoleNumber\\s*=' Assets/Scripts/Physics/Viewer/PhysicsLabController.cs | head -10
   ```
   `HoleContext.HoleNumber` gets set inside `LoadHole(n)` (line ~1441 per the recon grep). C0 needs to ensure `LoadHole(GameSession.CurrentHoleNumber)` fires on scene-load, not the hardcoded default.

---

## Scope

### Build settings change

`ProjectSettings/EditorBuildSettings.asset`:
- **Add:** `Assets/Scenes/Physics/LabScaffold.unity` (use GUID from its `.meta`).
- **Remove:** `Assets/Golf/Courses/lomond-country-club/Generated/Hole_01.unity` (ghost entry, zero GUID).
- **Keep:** `ShellScene.unity`, `Hole_01_Geo.unity`, `Hole_06_Geo.unity` (already in).
- **Add:** `Hole_02_Geo.unity` through `Hole_18_Geo.unity` (need to be in build settings for runtime additive load via `PhysicsLabController.LoadHole(n)`). 17 new entries. Grab GUIDs from their `.meta` files.

### Files CREATED

**`Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs`** — new MonoBehaviour, lives in ShellScene:

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Golfin.Gameplay.Session;

namespace Golfin.UI.GameplayTransition
{
    /// <summary>
    /// Owns the OPPONENT FOUND → gameplay-scene-loaded transition.
    /// Called by MatchmakingModalController.OpponentScanRoutine end (after GameSession.SeedSession).
    /// Shows LoadingScreenController during the load. Hides ShellScene UI on completion.
    /// Stage C0.
    /// </summary>
    public class GameplaySceneLoader : MonoBehaviour
    {
        public static GameplaySceneLoader Instance { get; private set; }

        const string GAMEPLAY_SCENE_NAME = "LabScaffold";

        [SerializeField] LoadingScreenController loadingScreen;
        [SerializeField] PersistentUIManager persistentUI;  // for hiding bottom nav

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// Entry point. MatchmakingModalController calls this after seeding GameSession.
        /// </summary>
        public void BeginGameplayLoad(int holeNumber)
        {
            StartCoroutine(LoadCoroutine(holeNumber));
        }

        IEnumerator LoadCoroutine(int holeNumber)
        {
            // 1. Show loading screen with tips
            if (loadingScreen != null) loadingScreen.PrepareForHoleLoad(holeNumber);
            ScreenManager.Instance?.ShowScreen(ScreenId.Loading);

            // 2. Hide bottom nav during gameplay
            persistentUI?.SetBottomNavVisible(false);

            // 3. Additive load gameplay scene
            var op = SceneManager.LoadSceneAsync(GAMEPLAY_SCENE_NAME, LoadSceneMode.Additive);
            op.allowSceneActivation = true;
            while (!op.isDone)
            {
                if (loadingScreen != null) loadingScreen.SetProgress(op.progress);
                yield return null;
            }

            // 4. Set the gameplay scene as active so newly-instantiated GameObjects land there
            var gameplayScene = SceneManager.GetSceneByName(GAMEPLAY_SCENE_NAME);
            if (gameplayScene.IsValid()) SceneManager.SetActiveScene(gameplayScene);

            // 5. Loading screen finishes — hands off to gameplay
            if (loadingScreen != null) yield return loadingScreen.FinishLoadingCoroutine();
        }

        /// <summary>
        /// Tears down the gameplay scene. Called by Stage D's MENU button handler.
        /// </summary>
        public IEnumerator UnloadGameplay()
        {
            persistentUI?.SetBottomNavVisible(true);
            var op = SceneManager.UnloadSceneAsync(GAMEPLAY_SCENE_NAME);
            while (op != null && !op.isDone) yield return null;
        }
    }
}
```

### Files EDITED

**`Assets/Scripts/UI/LoadingScreenController.cs`** — generalize (Stage D Part 1 work folded in):

Today `FinishLoading` hardcodes `ScreenManager.ShowScreen(ScreenId.Home)`. Generalize:

```csharp
public enum LoadTarget { LegacyBootHome, HoleLoad }

LoadTarget _target = LoadTarget.LegacyBootHome;
int _targetHoleNumber = 0;
float _externalProgress = -1f;  // -1 = use internal fake bar; ≥0 = use external load progress

/// <summary>Stage C0: prepare for a real hole load (gameplay scene transition).</summary>
public void PrepareForHoleLoad(int holeNumber)
{
    _target = LoadTarget.HoleLoad;
    _targetHoleNumber = holeNumber;
    _externalProgress = 0f;
}

/// <summary>Reset to legacy boot path. Called on Home → Loading legacy transitions.</summary>
public void ClearTarget()
{
    _target = LoadTarget.LegacyBootHome;
    _targetHoleNumber = 0;
    _externalProgress = -1f;
}

/// <summary>Stage C0: external progress driver (GameplaySceneLoader feeds SceneManager.LoadSceneAsync progress).</summary>
public void SetProgress(float p) => _externalProgress = Mathf.Clamp01(p);

/// <summary>Stage C0: coroutine variant of FinishLoading, awaitable by GameplaySceneLoader.</summary>
public IEnumerator FinishLoadingCoroutine()
{
    // (Existing fade-out timing; yields until loading bar reaches 1.0 and the fade completes.)
    yield return ...;
    _externalProgress = -1f;
    _target = LoadTarget.LegacyBootHome;  // reset for next show
}
```

`OnEnable` is unchanged for the legacy boot path (target = LegacyBootHome → finish to Home). When `_target == HoleLoad`, the loading bar is driven by `_externalProgress` instead of the internal fake timer, and `FinishLoadingCoroutine` is invoked by `GameplaySceneLoader` rather than auto-firing.

**`Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs`** — hand off to `GameplaySceneLoader` at OPPONENT FOUND end:

After `GameSession.SeedSession(...)` (Stage B), and after the modal's existing fade-out, call:
```csharp
// Stage C0: hand off to gameplay scene loader
GameplaySceneLoader.Instance?.BeginGameplayLoad(GameSession.CurrentHoleNumber);
```

Insert this as the last action in `OpponentScanRoutine` after the fade-out completes (the modal goes invisible, then the loading screen takes over).

**`Assets/Scripts/UI/PersistentUIManager.cs`** — add bottom-nav visibility toggle:

```csharp
[SerializeField] GameObject bottomNavBar;  // assign in scene to the nav container

public void SetBottomNavVisible(bool visible)
{
    if (bottomNavBar != null) bottomNavBar.SetActive(visible);
}
```

**`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs`** — startup hole-number source:

The controller currently has a hardcoded/Inspector-default starting hole. Add a `Start()` or `OnEnable()` hook that checks `GameSession.CurrentHoleNumber`:

```csharp
void Start()
{
    int holeFromSession = Golfin.Gameplay.Session.GameSession.CurrentHoleNumber;
    if (holeFromSession > 0)
    {
        Debug.Log($"[PhysicsLabController] Loading hole {holeFromSession} from GameSession seed (Stage C0).");
        LoadHole(holeFromSession);
    }
    else
    {
        // Lab/editor default path — keep existing behaviour for smoke runners and dashboard testing.
        // (No change to current default-hole load path.)
    }
}
```

**Critical:** verify no existing `Start()` is already in `PhysicsLabController` that would conflict. If there is, merge the new logic into it.

### Files DELETED

**None.** `GameplayScene.unity` stub is left alone (might delete in a future cleanup, but not C0's job — the deletion ripple could destabilize tests).

---

## Scene wiring (manual, paste-for-Cesar after code lands)

After implementer commits, before visual verify:

1. Open `ShellScene.unity`.
2. Find the GameObject hosting `PersistentUIManager`.
3. Add `GameplaySceneLoader` component to the same GameObject (or a sibling).
4. Wire its `loadingScreen` SerializeField to the `LoadingScreenController` instance in the scene.
5. Wire its `persistentUI` SerializeField to `PersistentUIManager`.
6. On `PersistentUIManager`, wire the new `bottomNavBar` SerializeField to the bottom-nav GameObject root.
7. Save the scene.

If any of these references can be auto-wired via `[RequireComponent]` or `FindFirstObjectByType` in `Awake`, implementer should prefer that and document. SerializeField is only needed where auto-wiring would be fragile.

---

## Implementation steps (recommended order)

1. **Pre-flight checks** (above), log results.
2. **Build settings edit**: add LabScaffold + 17 Hole_NN_Geo entries, remove ghost Hole_01.unity entry.
3. **Create** `GameplaySceneLoader.cs` in `Assets/Scripts/UI/GameplayTransition/` (new directory).
4. **Generalize** `LoadingScreenController.cs` (target enum + PrepareForHoleLoad + SetProgress + FinishLoadingCoroutine).
5. **Edit** `MatchmakingModalController.cs` — call `GameplaySceneLoader.Instance.BeginGameplayLoad` after seed+fade.
6. **Edit** `PersistentUIManager.cs` — `SetBottomNavVisible(bool)`.
7. **Edit** `PhysicsLabController.cs` — `Start()` reads `GameSession.CurrentHoleNumber`, calls `LoadHole(n)` if seeded.
8. **Compile clean.**
9. **Scene wiring** (manual, after code commits).
10. **Run EditMode test gate.** Existing 300/300 must remain. New tests (5, below) add to 305/305.
11. **Cesar visual gate** — first production end-to-end playthrough.
12. **Commit + push.** Message: `loop_v2_c0_matchmaking_to_gameplay_transition: end-to-end PLAY flow`

---

## Tests (5 new EditMode tests)

New file: `Assets/Scripts/UI/Tests/GameplaySceneLoaderTests.cs`.

1. **`BeginGameplayLoad_HidesBottomNav`** — mock `PersistentUIManager`, call `BeginGameplayLoad(1)`, assert `SetBottomNavVisible(false)` invoked.
2. **`BeginGameplayLoad_PreparesLoadingScreenWithHoleNumber`** — mock `LoadingScreenController`, call `BeginGameplayLoad(7)`, assert `PrepareForHoleLoad(7)` invoked.
3. **`UnloadGameplay_RestoresBottomNav`** — call `UnloadGameplay`, assert `SetBottomNavVisible(true)` invoked.
4. **`LoadingScreenController_PrepareForHoleLoad_SetsTarget`** — call `PrepareForHoleLoad(5)`, assert internal target = HoleLoad, holeNumber = 5.
5. **`LoadingScreenController_ClearTarget_ResetsToLegacy`** — call `PrepareForHoleLoad(5)` then `ClearTarget`, assert target = LegacyBootHome.

PlayMode integration tests for the actual scene load are deferred — Cesar's visual gate is the load-correctness proof.

---

## Definition of Done

**Audit grep:**
- [ ] `grep -n 'LabScaffold.unity' ProjectSettings/EditorBuildSettings.asset` → one hit (added)
- [ ] `grep -n 'Hole_01.unity' ProjectSettings/EditorBuildSettings.asset | grep -v _Geo` → zero hits (ghost removed)
- [ ] `grep -c 'Hole_.._Geo.unity' ProjectSettings/EditorBuildSettings.asset` → 18 hits (all 18 holes)
- [ ] `ls Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` → file exists
- [ ] `grep -n 'GameplaySceneLoader.Instance' Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` → one hit
- [ ] `grep -n 'PrepareForHoleLoad' Assets/Scripts/UI/LoadingScreenController.cs` → one hit
- [ ] `grep -n 'GameSession.CurrentHoleNumber' Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` → at least one hit (in the new Start hook)
- [ ] Project compiles clean
- [ ] EditMode test gate: **305/305 PASS** (300 prior + 5 new)

**Cesar visual gate (the big one — first end-to-end production playthrough):**
- [ ] Launch from cold (Logo → Splash → Loading → Home)
- [ ] Tap PLAY on Home → matchmaking modal opens → OPPONENT FOUND fires → loading screen with tips appears
- [ ] Gameplay scene loads (Hole 1) → loading screen fades out → ball at tee, can be aimed
- [ ] Hit a putt or two; ball physics works
- [ ] Sink the ball into the cup → lab `HoleCompleteWidget` appears (existing lab behavior; Stage C1 will migrate this)
- [ ] Bottom nav bar is HIDDEN during gameplay
- [ ] Tap CANCEL on matchmaking before OPPONENT FOUND → returns cleanly to Home, no scene-load fires
- [ ] Repeat from Hole Selection: select Hole 2 → PLAY → loads Hole 2 (verify hole number shows on HUD)

**Known-acceptable visual gaps** (Cesar may flag, Stage C1+ will address):
- Bottom nav re-appears via PersistentUI after lab modal closes (Stage D handles MENU button)
- ShellScene Result modal does not yet appear (Stage C1's job)
- Dashboard / dev UI may be visible in the gameplay scene (Stage E or polish stage will strip)

---

## Handoff

**Implementer:** Claude Code (TELLCODE).
**Spec:** `Docs/Specs/Active/loop_v2_c0_matchmaking_to_gameplay_transition/SPEC.md`
**Architect-side close:** STATUS.md → DONE, move folder to `Docs/Specs/Completed/`, flip Notion entry to Done, set Closed date.

---

## Out of scope (deferred to other stages)

- **ShellScene Result modal** — Stage C1's job. Subscribes to `GameSession.OnHoleComplete`. Existing lab `HoleCompleteWidget` continues to fire in the gameplay scene (no conflict, both can exist briefly; C1 retires the lab one or hides it in production builds).
- **Dashboard / dev UI hiding** in production gameplay scene — polish stage. The dashboard is just GameObjects in LabScaffold; can be SetActive(false) via a "production mode" flag, but that's Stage E or polish work.
- **MENU / RETRY / PLAY NEXT button handlers** — Stage D.
- **Holes 2-18 content QA** — content stage. C0 wires the load path; content bugs (missing pin, weird terrain) get fixed per-hole.
- **`LabScaffold.unity` rename → `GameplayScene.unity`** — polish. C0 commits to the name "LabScaffold" living on as the production gameplay scene; rename happens in a dedicated naming stage with full grep across smoke runners.
- **Modal pattern migration** for `SettingsController` — audit P1-4 follow-up.
- **`GolfinRedux.* → Golfin.*` namespace migration** — audit P1-1 follow-up.

---

## Risk register

| # | Risk | Mitigation |
|---|---|---|
| 1 | `LabScaffold.unity` references editor-only scripts that fail at runtime | Pre-flight verifies; if real, Stage C0 strips them or fails fast with clear error |
| 2 | Bottom-nav visibility toggle breaks the highlight logic (Stage A regression risk) | New `SetBottomNavVisible` only touches `bottomNavBar.SetActive`; highlight code is internal to PersistentUI and unaffected |
| 3 | Cesar visual gate fails on a hole that has broken content | C0 ships Hole 1 as the canonical gate; other holes are "best effort" — file content bugs as separate tasks |
| 4 | Additive load leaves orphan GameObjects in ShellScene from prior runs | `UnloadSceneAsync` in Stage D's MENU handler is the cleanup; until D ships, only single-hole playthroughs work cleanly per launch |
| 5 | `PhysicsLabController.Start` already exists with conflicting logic | Implementer must merge, not duplicate. Pre-flight grep flags this |
| 6 | 17 new build-settings entries cause project-wide rebuild | Expected; one-time cost |
