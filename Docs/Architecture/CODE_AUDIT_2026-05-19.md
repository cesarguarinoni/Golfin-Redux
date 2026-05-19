# Code Audit — 2026-05-19 (Loop v2 prep)

**Scope:** UI/Screens + glue layer feeding Loop v2 (Select Character → Clubs → Hole → play → result → next/menu).
**Method:** Architect read-only pass over `Assets/Scripts/UI/**` + root Managers + `GameSession`. No code edits in this pass.
**Bias:** Find what gates or compounds-mess-in Loop v2. Polish-only items demoted.

Findings ranked **P0** (blocking) → **P1** (compounds if left) → **P2** (cleanup) → **P3** (nice).

---

## P0 — fix before/inside Loop v2

### P0-1. Two parallel bottom-nav bars driving `ScreenManager`
- `HomeScreenController.cs:77-119` declares `navHome/Gacha/Tee/Inventory/Characters` Buttons and wires them via `OnNavClicked → screenManager.ShowScreen(...)`.
- `PersistentUIManager.cs:23-27, 128-141` declares `home/gacha/mainPlay/inventory/characters` Buttons and wires them via `NavigateTo(Screen) → ScreenManager.Instance.ShowScreen(...)`.
- Each controller tracks its **own** "active screen" highlight independently. If both exist in-scene, they race. Loop v2 ships a result screen + next/menu branch — a third caller into `ScreenManager` from a third source-of-truth is the path of least resistance and will make the problem worse.
- **Verdict:** Pick one. PersistentUI is the better home (it already persists across screens). Strip the nav wiring from `HomeScreenController` and delete the duplicate `[SerializeField]` block. Estimated 30–60 min.

### P0-2. Two `SettingsController`s in production
- `SettingsController.cs` (Phase 1, `Golfin.UI`, 226 lines, 40+ SerializeFields, all click handlers are `Debug.Log`).
- `SettingsControllerPhase2.cs` (Phase 2, `Golfin.UI`, 256 lines, accordion items + submenu components + modal hook).
- Both register their own `static Instance`. `PersistentUIManager.OnSettingsButtonClick` (line 200) prefers Phase 2 with Phase 1 fallback. `HomeScreenController.OnSettingsClicked` (line 244) calls Phase 1 only. Different open-routes, same button.
- **Verdict:** Phase 2 is the keeper. Delete `SettingsController.cs` outright, delete the `Phase2` suffix from Phase 2's class name (`SettingsController` again), update the two callers. ~45 min. Does not block Loop v2 functionally but is exactly the kind of "isolation/divergence" the audit was asked to surface.

### P0-3. `HoleProgressionService` has no writer for "completed"
- `HoleSelection/HoleProgressionService.cs` is a POCO singleton with `IsUnlocked/HasPlayed` and override setters. The only callers of the setters are `HoleProgressionDebug` and tests. The Hole Selection screen reads it correctly to pick Replay vs Play and Locked vs Collapsed.
- Loop v2's result screen needs to mark the hole as played and unlock the next on success. Nothing writes that today. The "next hole" state propagation is silently broken end-to-end.
- **Verdict:** Loop v2 must add the writer (single-line `SetPlayedOverride(currentHoleNumber, true)` from the result screen on hole-cleared, plus a follow-on `SetUnlockedOverride(currentHoleNumber + 1, true)`). File the proper save-layer wiring as a Loop v2 sub-task — for now, in-memory persistence inside `HoleProgressionService` is enough to ship the loop.

### P0-4. `GameSession` is half a game session
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` is a static class with `TurnCount` + `ShotHistory` + `ResetForNewHole`. No selected character, no equipped bag, no current hole number.
- Loop v2's central plumbing job is "selected character + bag + hole → gameplay → result." Today, gameplay reads selection state by reaching back into `CharacterManager.Instance.GetSelectedCharacterId()` / `BagManager.Instance.EquippedBagSlot` / hole number passed via parameter. There is no single read-back surface that says "what hole are we on right now."
- **Verdict:** Loop v2 extends `GameSession` (or graduates it to a new `Golfin.Gameplay.Session` namespace) with `CurrentHoleNumber`, `SelectedCharacterId`, `EquippedBagSlot`, captured at session-start. The capture point is the matchmaking modal's "OPPONENT FOUND → start" transition (or `HoleSelectionScreenController.HandleActionClicked` if matchmaking is skipped).

---

## P1 — compounds if left

### P1-1. Namespace split: `Golfin.*` vs `GolfinRedux.*`
- `Golfin.UI`, `Golfin.UI.Modals`, `Golfin.UI.Matchmaking`, `Golfin.Roster`, `Golfin.Gameplay.UI.HUD`, `Golfin.Utilities` — the canonical roots.
- `GolfinRedux.UI`, `GolfinRedux.UI.HoleSelection` — a parallel tree. `ScreenManager`, `FadeController`, `HomeScreenController`, `HoleData`, `HoleDatabase`, `HoleDatabaseLoader`, `HoleSelectionScreenController`, `HoleProgressionService`, `HoleCardController`.
- Custom-instructions canon: "Namespace: `Golfin.Roster` for all roster/character scripts." `Golfin.*` is the project root; `GolfinRedux.*` is legacy from the original project name. Every UI controller has to do `using Golfin.UI;` AND `using GolfinRedux.UI;` to talk to itself.
- **Verdict:** Bulk-rename `GolfinRedux.UI → Golfin.UI.Shell` (or similar) post-Loop-v2. Not a blocker, but every new file landed in the wrong tree compounds the future migration. **Action for Loop v2:** new Loop v2 files land in `Golfin.Gameplay.Session` (gameplay state) or `Golfin.UI.Shell` (screens). Do NOT add to `GolfinRedux.*`.

### P1-2. Singleton init/teardown inconsistency
| Class | DontDestroyOnLoad | OnDestroy cleanup | Init API |
|---|---|---|---|
| `ScreenManager` | no | no | `FindFirstObjectByType` (modern) |
| `FadeController` | no (intentional, lives in Canvas) | no | — |
| `PersistentUIManager` | yes | no | — |
| `SettingsController(Phase2)` | no | no | — |
| `CharacterManager` | yes | yes (`Instance = null!`) | — |
| `BagManager` | yes | yes | — |
| `LogoScreenController` | n/a | n/a | `FindFirstObjectByType` |
| `SplashScreenController` | n/a | n/a | `FindObjectOfType` (deprecated) |

- Mixed `FindObjectOfType` / `FindFirstObjectByType` across siblings.
- Singletons that survive a domain reload without `OnDestroy` cleanup are the exact pattern Lesson Q warns about. Roster Manager pattern (`CharacterManager`, `BagManager`) is the correct one.
- **Verdict:** Loop v2 itself doesn't force this fix, but any new Loop v2 singleton (e.g. `GameSession` if it graduates from static) MUST use the Roster pattern. Existing offenders go on a P1 backlog.

### P1-3. `GameSession` namespace is wrong
- Lives in `Golfin.Gameplay.UI.HUD` because the file was placed next to ShotUI in §2c. It's not UI; it's session state that UI subscribes to.
- **Verdict:** Loop v2's extension is the natural moment to move it to `Golfin.Gameplay.Session`. Renaming the namespace touches ~3-5 subscribers (HUD turn label, history view, result screen).

### P1-4. Modal pattern divergence
- `MatchmakingModalController` is best-in-class: extends `ModalController`, proper coroutine cleanup in `OnHide` + `OnDisable` safety net, `OnShow/OnHide` hooks, fade-in animation built into the base.
- Settings (Phase 1 AND Phase 2) does NOT extend `ModalController` — both reinvent `OpenSettings`/`CloseSettings` with raw `SetActive(true/false)` and no fade.
- `ClubLevelUpModalController`, `BagClubModalController`, `BagSelectionModalController`, `ItemUseModalController` — TBD but the naming smell suggests several may also not extend `ModalController`.
- **Verdict:** When Loop v2 builds the **Result modal**, it MUST extend `ModalController` (Matchmaking is the template). Migrating existing modals is a P2 cleanup pass.

### P1-5. Debug-noisy controllers in production paths
- `ScreenManager.ShowScreen` and `ApplyScreen` log 9× per transition.
- `HomeScreenController.OnNavClicked` logs 4–5× per tap.
- `PersistentUIManager` logs username updates, settings opens, navigation failures.
- Builds will spam logs every screen change. Not a Loop v2 blocker but trivial to gate behind a `#if UNITY_EDITOR` or `Debug.isDebugBuild`.

---

## P2 — cleanup

- **`HoleData.cs` / `HoleDatabase.cs` / `HoleDatabaseLoader.cs` at `Assets/Scripts/UI/` root.** Should live under `UI/HoleSelection/` (consumed only there) or graduate to `Assets/Scripts/Data/`. Loop v2 won't notice — but anyone touching hole flow has to grep across two locations.
- **`LoadingScreenController.cs` has no namespace.** Bare global class. Doesn't conflict with anything yet.
- **12 `.bak` files under `Assets/Scripts/UI/Editor/`.** `AboutSubmenuBuilder.cs.bak`, `DiagnoseLayoutIssue.cs.bak`, `FixSettingsLayout*.cs.bak`, etc. Cesar's old Phase-2 settings work. Safe to delete with `git rm Assets/Scripts/UI/Editor/*.bak` after a final eyeball.
- **`HomeScreenController.cs` hard-coded sprite path** (`$"Characters/Homescreen/{charName}"` + Placeholder fallback) — extract to const + central asset registry post-Loop-v2.
- **Resource lookup duplication:** reward icon sprites (`pointsIcon`, `repairKitIcon`, `ballIcon`) are serialized on **three** different controllers (`HomeScreenController`, `MatchmakingModalController`, `HoleCardController`). When Loop v2 adds a result screen, it'll need them too. Candidate for a `RewardIconRegistry` ScriptableObject (single source) — but that's a P2 follow-up, not a Loop v2 blocker.

---

## P3 — nice to have

- **`#nullable enable` adoption is patchy.** `MatchmakingModalController`, `CharacterManager`, `BagManager`, `BallManager`, `ClubManager`, `ItemManager` enable it. `ScreenManager` uses `?` annotation **without** `#nullable enable` (likely emits a warning per file or relies on project-level setting). UI shell controllers don't enable it. Pick a project-wide stance.
- **TODO buttons wired to placebo screens.** `HomeScreenController:116` wires Gacha to Home. `PersistentUIManager.NavigateTo(Screen.Gacha)` logs a warning. Stub UX is fine, just track it.
- **`HoleSelectionIter4Corrections.cs`** referenced in the opener does NOT exist as a separate file — Iter 4 corrections were merged into `HoleSelectionScreenController.cs`. The opener was stale. No action.

---

## Smooth screen transitions + animated UI (Cesar request)

- `FadeController` already exists, is well-implemented (singleton, coroutine-cancelling, fade-out-then-in with midpoint callback) and is already used by every `ScreenManager.ShowScreen` call.
- `ModalController` already does fade-in/fade-out on a `CanvasGroup` with configurable duration.
- **Verdict:** Transition infra is solid. Loop v2 should **bind to it**, not rebuild. Animated button presses + panel expand/collapse for the result screen → use `LayoutRebuilder.ForceRebuildLayoutImmediate` + `CanvasGroup` fades (the existing pattern in `HoleCardController.SetState`). A new `TransitionController` is NOT needed for this iteration. Shader-based polish + DOTween + Lottie all stay queued for post-Loop-v2 polish pass.

---

## Summary table

| ID | Severity | Item | Loop-v2 cost if ignored |
|---|---|---|---|
| P0-1 | Blocking | Two bottom-nav bars | New screens get a third nav-source; bug surface compounds |
| P0-2 | Blocking | Two Settings controllers | Result-screen settings entry has two paths |
| P0-3 | Blocking | `HoleProgressionService` has no writer | "Next hole" doesn't unlock; Loop v2 has no closure |
| P0-4 | Blocking | `GameSession` missing character/bag/hole | No single read-back surface; controllers re-resolve singletons everywhere |
| P1-1 | Compounding | Namespace split | New Loop v2 files land in wrong tree |
| P1-2 | Compounding | Singleton inconsistency | New singletons drift |
| P1-3 | Compounding | `GameSession` in `UI.HUD` namespace | Subscriber refactor compounds |
| P1-4 | Compounding | Modal pattern drift | Result modal copies wrong template |
| P1-5 | Compounding | Debug.Log spam | Build logs noise |
| P2 | Cleanup | File-root placement, `.bak` files, sprite paths | — |
| P3 | Nice | Nullable, TODOs, stale opener refs | — |
