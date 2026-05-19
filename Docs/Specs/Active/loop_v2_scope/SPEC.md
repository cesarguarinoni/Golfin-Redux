# Loop v2 — Scoping SPEC

**Status:** SPEC_READY — Architect scoping pass (2026-05-19)
**Task type:** **FULL PIPELINE**, multi-stage. Stages fire as separate sub-specs.
**Companion doc:** `Docs/Architecture/CODE_AUDIT_2026-05-19.md` (read first; P0 items are referenced here)
**Notion:** GOLFIN_Roadmap (UUID `364b3e97-02b7-819b-a734-dfe5a3a087a9`) Order 300s

---

## What Loop v2 is

The user flow **Select Character → Clubs → Hole → play → result → next/menu**.

Most of the screens already exist. The work is **glue**:

1. Session state plumbing (selected character, equipped bag, current hole) read once at hole-start, available to all consumers without re-resolving singletons.
2. A **Result screen** that shows: score, shot history, rewards earned. Two outcomes: hole **cleared** vs hole **failed**.
3. Branching after Result: **Next Hole** (auto-advance, kicks Loading → next hole) vs **Menu** (return to Home).

---

## What already exists (bind, don't rebuild)

| System | File | Status |
|---|---|---|
| Logo / Splash / Loading / Home / Roster screens | `Assets/Scripts/UI/*ScreenController.cs` | Done |
| Inventory (Clubs / Balls / Items / Bags) | `Assets/Scripts/UI/Inventory/**` | Done |
| Hole Selection (filters, cards, expand/collapse, action button) | `Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` | Done |
| Matchmaking modal (fake opponent search → "OPPONENT FOUND") | `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` | Done; gold-standard modal pattern |
| `ScreenManager` + `FadeController` (transitions) | `Assets/Scripts/UI/ScreenManager.cs`, `FadeController.cs` | Done; bind, do not extend |
| `ModalController` base class (fade-in / fade-out, backdrop) | `Assets/Scripts/UI/Modals/ModalController.cs` | Done; Result modal must extend this |
| `GameSession` (turn count, shot history, reset-on-hole-load) | `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` | Partial (see P0-4 in audit) |
| `HoleProgressionService` (unlock/played state) | `Assets/Scripts/UI/HoleSelection/HoleProgressionService.cs` | Read-only (see P0-3) |
| `CharacterManager.GetSelectedCharacterId()`, `BagManager.EquippedBagSlot` | root Managers | Done |
| End-of-hole detection | Loop v1 §2a–§2f: `BallStateMachine.OnShotComplete` + `HoleCompleteWidget` (already in lab) | Done — Loop v2 hooks into the same signal |
| `HoleCompleteWidget` (SUCCESS/FAILED card + LOCKED next card) | `Assets/Prefabs/...` (built in §2d) | Done — Loop v2 elevates this from the lab into the production result screen |

---

## What Loop v2 actually builds (gap map)

### G1. Session state (`Golfin.Gameplay.Session.GameSession`)
- Extend the existing static `GameSession` (or graduate it; see P1-3 in audit) with three fields, captured at session-start: `CurrentHoleNumber`, `SelectedCharacterId`, `EquippedBagSlot`.
- Capture point: when `MatchmakingModalController` finishes its "OPPONENT FOUND" countdown AND `ScreenManager.ShowScreen(Loading)` fires, write the three values from `CharacterManager.Instance.GetSelectedCharacterId()` + `BagManager.Instance.EquippedBagSlot` + the hole index the modal was opened with.
- Reset on hole-load (existing `ResetForNewHole` extended).
- Subscribers stop reaching into singletons directly; they read `GameSession.CurrentHoleNumber` etc.

### G2. Loading → Gameplay scene handoff
- Today, `LoadingScreenController.FinishLoading` jumps straight to `ScreenId.Home`. For Loop v2, the matchmaking → loading path needs to instead load the **Hole_NN** scene (or set up the gameplay state if scene already loaded).
- Hole scene loading is **out of scope** for the first sub-spec; instead, Loop v2 first lands inside the existing PhysicsLab scene with the `HoleSelection → Matchmaking → GameSession seeded → PhysicsLab.OnHoleLoaded(GameSession.CurrentHoleNumber)` path. The proper scene-loader lands in a follow-up.

### G3. Result screen / Result modal (production, not lab)
- The lab already has `HoleCompleteWidget` (a modal-style result card). Loop v2 elevates it:
  - **Extends `ModalController`** (Settings does not — see P0-2/P1-4). Free fade-in/fade-out + backdrop.
  - Shows: hole number, strokes (`GameSession.ShotHistory.Count + penalties`), par, score-vs-par badge, shot history list, rewards earned (from `HoleData.rewards` or `replayRewards` depending on whether this was a replay).
  - Two action buttons:
    - **PLAY NEXT** (visible if next hole unlocked OR became unlocked by clearing this one) → mark current hole played, unlock next, `ScreenManager.ShowScreen(Loading)` with next hole seeded into session.
    - **MENU** (always visible) → mark current hole played (if SUCCESS), `ScreenManager.ShowScreen(Home)`.
- Failed state: same modal, "FAILED" badge instead of "SUCCESS", no rewards, only **RETRY** + **MENU** buttons.

### G4. Hole progression writer
- The Result modal's **SUCCESS path** writes:
  - `HoleProgressionService.Instance.SetPlayedOverride(GameSession.CurrentHoleNumber, true)`
  - `HoleProgressionService.Instance.SetUnlockedOverride(GameSession.CurrentHoleNumber + 1, true)` if `CurrentHoleNumber < 18`
- These two lines close the audit P0-3 gap with zero new save layer required.

### G5. Reward grant
- On SUCCESS, iterate `HoleData.rewards` (or `replayRewards` if this is a replay) and call:
  - `RewardPointsManager.Instance.AddPoints(amount)` for `RewardType.Points`
  - `ItemManager.Instance.AddItem("item_repairkit_common", amount)` (or whichever rarity the hole grants — TBD) for `RewardType.RepairKit`
  - `BallManager.Instance.AddBall(...)` for `RewardType.Ball` — verify API exists.
- Loop v2 does **not** add the reward bus (Foundation #4) yet. Direct calls are fine for first cut; the bus lands when there's a second consumer (rankings, daily missions, etc.).

### G6. "Next Hole" auto-flow
- After SUCCESS, PLAY NEXT button triggers:
  - `GameSession.ResetForNewHole()` followed by `GameSession.CurrentHoleNumber = next`
  - `ScreenManager.ShowScreen(Loading)` → loading bar → on finish, gameplay scene seeds at the new hole's tee.

---

## Foundations chosen for this milestone

From the five candidates in memory:

✅ **#1 Interface-first services** — but ONLY for **new** Loop v2 surfaces. Specifically:
- `ISessionStore` (read interface over `GameSession`'s new fields) — enables headless / replay later.
- `IHoleProgressionStore` (read+write over `HoleProgressionService`) — same contract for in-memory and the eventual save layer.

We do NOT retrofit interfaces over `CharacterManager`, `BagManager`, etc. in this milestone. That's a P1 architectural pass deferred.

🟡 **#4 Event bus for rewards** — deferred. Only one consumer (the Result screen itself); a bus with one subscriber is just method calls in a hat. Revisit when Rankings (Roadmap 400s) needs a second subscriber.

❌ **#2 Reactive save layer** — deferred. In-memory `HoleProgressionService` is enough to ship the loop. Save layer lands in its own milestone after Loop v2 ships.

❌ **#3 Replay determinism via fixed-point physics** — gameplay already uses fixed-point determinism. The "replay" feature itself (record + playback) is post-Loop-v2.

❌ **#5 Headless mode for bots** — bot tests come after Loop v2 ships. Foundation #1 (interfaces) is what enables it later.

---

## Stage breakdown

### Stage A — Audit P0-1 + P0-2 cleanup
**Why first:** Single source of truth before adding more callers.
- Pick PersistentUI as the nav bar. Strip `HomeScreenController`'s duplicate nav wiring + SerializeFields. Verify no scene-level wiring relies on it.
- Delete `SettingsController.cs` (Phase 1). Rename `SettingsControllerPhase2.cs` → `SettingsController.cs`, class rename `SettingsControllerPhase2` → `SettingsController`. Update both call sites (`PersistentUIManager:200`, `HomeScreenController:244`).
- **Est:** 1.5–2 hr surgical + Cesar visual.
- **Spec folder:** `Docs/Specs/Active/loop_v2_a_singletons_consolidation/` (created when fired).

### Stage B — Session state plumbing (G1)
- Extend `GameSession` with `CurrentHoleNumber`, `SelectedCharacterId`, `EquippedBagSlot`. Capture in `MatchmakingModalController` at OPPONENT_FOUND. Move namespace to `Golfin.Gameplay.Session` (audit P1-3).
- Refactor subscribers (HUD turn label, shot history view) for the new namespace.
- Add `ISessionStore` read interface over `GameSession`.
- 4 new EditMode tests: capture at modal-found, reset-on-new-hole preserves nothing, character/bag/hole getters return seeded values, ResetForNewHole clears all session fields not just turn/history.
- **Est:** 3–4 hr. Single SPEC, single implementer pass likely.

### Stage C — Result modal (G3 + G4 + G5)
- Elevate `HoleCompleteWidget` from lab into production: extends `ModalController`, lives on the gameplay scene's UI canvas, listens for `BallStateMachine.OnShotComplete` terminal-state-InCup.
- Buttons: PLAY NEXT (gated on next-hole-exists), MENU, RETRY (FAILED state).
- On SUCCESS: write progression (P0-3), grant rewards (G5).
- **Est:** half-day. Visual review against `loop_v1_2d_hole_complete_and_result_screen` lessons — Cesar will catch text-floating-outside-BG within seconds if it regresses (lessons N–O still apply).

### Stage D — "Next Hole" auto-flow (G6)
- PLAY NEXT button → `GameSession.ResetForNewHole()` → seed next hole number → `ScreenManager.ShowScreen(Loading)` → on-loaded, gameplay re-arms at the new hole's tee.
- For first cut: stay inside PhysicsLab scene, swap holes via existing `PhysicsLabController.LoadHole(n)` path. Real scene-load deferred.
- **Est:** 2–3 hr.

### Stage E — Hole Selection → Loop v2 entry path
- Wire `HoleSelectionScreenController.HandleActionClicked` → `MatchmakingModalController.Open(holeIndex)` → on found, seed GameSession + show Loading → gameplay.
- Today, Action clicked already opens the matchmaking modal; just ensure the GameSession seed lands at the right moment.
- **Est:** 1–2 hr (mostly wiring + tests).

### Stage F — Animated UI polish (Cesar request)
- Bind to existing `FadeController` + `ModalController` fades. **Do not** add `TransitionController` (audit recommendation).
- Result modal panel uses `CanvasGroup` fade + scale tween (DOTween already in project? Verify; if not, hand-roll the scale tween — 8 lines).
- Button press feedback: 1.0 → 0.95 → 1.0 scale on tap (universal helper component, drop on any Button).
- **Est:** 2–3 hr. Lands at the end of Loop v2 once everything else is functional.

---

## Out of scope (parked, not forgotten)

- **Phase B Stage 3** (total horizontal carry physics) — queued at `Docs/Specs/Queued/phase_b_stage3_total_horizontal_carry/`. Picked up post-Loop-v2.
- **Save layer** (real persistence) — Loop v2 ships with in-memory progression. Save-layer milestone is its own thing.
- **Real scene loading** (per-hole scene instead of in-PhysicsLab swap) — Stage D ships in-lab; proper scene loader is its own follow-up.
- **Reward event bus** — deferred until second consumer exists.
- **Modal pattern migration** (Settings + LevelUp + Bag modals → ModalController) — P2 cleanup, post-Loop-v2.
- **`GolfinRedux.* → Golfin.*` namespace migration** — P1, post-Loop-v2.

---

## Open questions for Cesar (LOCKED 2026-05-19)

1. **Stage A scope.** ✅ Bundle P0-1 + P0-2 into one Stage A.
2. **Sub-spec firing cadence.** ✅ Separate. A → B → C as their own pipeline runs.
3. **Result modal placement.** ✅ Option A — gameplay scene UI canvas (where `HoleCompleteWidget` already lives). Stage D stays in-lab (scene-swap via `PhysicsLabController.LoadHole`), so cross-scene isn't a problem yet. Promote to ShellScene later if a real scene loader makes A flicker.
4. **Reward grant on REPLAY.** ✅ Every replay clear grants `replayRewards`. Daily caps live in a future progression system.
5. **Hole 18 PLAY NEXT.** ✅ Hide PLAY NEXT, MENU styled prominent, fire a "course cleared" toast.

---

## Per-stage goals + Definition of Done (acceptance criteria)

### Stage A — Singletons consolidation
**Goal:** One bottom-nav controller. One SettingsController.
**DoD (testable):**
- `HomeScreenController` has zero nav button SerializeFields and zero `OnNavClicked` references. `PersistentUIManager` is the only writer to `ScreenManager.ShowScreen` from a nav-bar context.
- `SettingsController.cs` (Phase 1) is deleted. `SettingsControllerPhase2.cs` is renamed to `SettingsController.cs`, class renamed to `SettingsController`, `Instance` type updated. Both call sites (`PersistentUIManager:OnSettingsButtonClick`, `HomeScreenController:OnSettingsClicked`) point at the single controller.
- Visual: tap settings from Home → opens. Tap settings from Roster → opens. Bottom nav highlights correctly when switching screens. No double-fire / double-highlight.
- Compile clean, test gate still green.

### Stage B — Session state plumbing
**Goal:** One read-back surface for "what character / bag / hole am I on right now." No subscriber re-resolves singletons.
**DoD:**
- `GameSession` lives in `Golfin.Gameplay.Session` (moved from `Golfin.Gameplay.UI.HUD`). All subscribers updated.
- New fields: `CurrentHoleNumber`, `SelectedCharacterId`, `EquippedBagSlot`. `ResetForNewHole` clears them.
- Capture point: `MatchmakingModalController.OpponentScanRoutine` end (the "OPPONENT FOUND" moment) writes the three values before fading the modal out.
- New `ISessionStore` interface exposes the three fields as read-only.
- 4 new EditMode tests: capture-at-found-event, reset-clears-all, getter-returns-seeded-values, reset-fires-OnTurnChanged-and-OnHistoryChanged.
- Compile clean. Subscribers (HUD turn label, shot history view) still render correctly.

### Stage C — Result modal (production)
**Goal:** End-of-hole shows score, history, rewards, and a clear next-action. Failed and SUCCESS share one modal, different visuals.
**DoD:**
- `HoleCompleteWidget` extends `ModalController` (free fade-in/fade-out + backdrop). Lives on the gameplay scene's UI canvas.
- Listens to `BallStateMachine.OnShotComplete` for terminal-state-InCup (SUCCESS) or stroke-cap-reached (FAILED).
- SUCCESS card shows: hole number, strokes (`ShotHistory.Count + penalties`), par, score-vs-par badge (Eagle/Birdie/Par/Bogey/etc.), shot history scrollable, rewards earned.
- FAILED card shows: "FAILED" badge, no rewards, RETRY + MENU only.
- SUCCESS PLAY NEXT writes `HoleProgressionService.SetPlayedOverride(current, true)` + `SetUnlockedOverride(current + 1, true)` if `current < 18`.
- SUCCESS grants rewards: iterate `HoleData.rewards` (or `replayRewards` if already played), call `RewardPointsManager.AddPoints`, `ItemManager.AddItem`, `BallManager.AddBall`. Every clear grants `replayRewards` on replays (no daily cap).
- Hole 18 SUCCESS: PLAY NEXT button hidden, MENU button styled prominent, "course cleared" toast fires.
- Visual review pass per `loop_v1_2d_hole_complete_and_result_screen` lessons N–O — Cesar's eyeballs gate the close.

### Stage D — PLAY NEXT auto-flow
**Goal:** Tapping PLAY NEXT lands in the next hole's tee, ready to fire, no menu round-trip.
**DoD:**
- PLAY NEXT handler: `GameSession.ResetForNewHole()` → `GameSession.CurrentHoleNumber = next` → `ScreenManager.ShowScreen(Loading)` → on Loading finish, `PhysicsLabController.LoadHole(GameSession.CurrentHoleNumber)` → ball arms at next tee.
- MENU handler: `GameSession.ResetForNewHole()` → `ScreenManager.ShowScreen(Home)`.
- Loading screen shows the right hole's loading hint / image if `HoleData` provides one.
- No flicker between modal-close and Loading-screen-show (FadeController bridges).
- Visual confirmation: clear hole → PLAY NEXT → next hole tee visible within ~2–3s.

### Stage E — Hole Selection entry path
**Goal:** Tapping a hole card's PLAY/REPLAY button reaches gameplay with the correct hole seeded.
**DoD:**
- `HoleSelectionScreenController.HandleActionClicked` → `MatchmakingModalController.Open(holeIndex)` (already wired; verify).
- Matchmaking found → GameSession seeded (Stage B) → Loading → gameplay at the correct hole's tee.
- Locked holes refuse entry (`HoleProgressionService.IsUnlocked` already gates the card).
- Replay holes use `replayRewards` (Stage C consumes this correctly).
- Visual: tap REPLAY on a played hole → matchmaking → gameplay at that hole's tee. Complete it → Result shows replay rewards.

### Stage F — Animated UI polish
**Goal:** No instant cuts on user-driven transitions; buttons feel responsive.
**DoD:**
- All `ScreenManager.ShowScreen` calls in user-driven paths go through `FadeController.FadeOutThenIn` (already the default; verify no `instant: true` shortcuts crept in).
- Result modal uses `ModalController`'s built-in fade (free from Stage C).
- New `ButtonPressFeedback` component (~30 lines): drop on any `Button`, scales 1.0 → 0.95 → 1.0 over 0.12s on tap. Hand-rolled coroutine, no DOTween dependency.
- Applied to: PLAY (Home, hole card, matchmaking), PLAY NEXT, MENU, RETRY, all bottom-nav buttons, Settings open/close.
- No new dependencies. No shader work. No Lottie. (All deferred to a future polish pass.)

---

## Definition of done (Loop v2 overall)

- Fresh launch → Splash → tap → Loading → Home → tap PLAY (or TEE bottom-nav → hole card → PLAY) → Matchmaking → gameplay → make it into the cup → Result modal appears with score, history, rewards → tap PLAY NEXT → loop with next hole → repeat indefinitely up to hole 18 → final completion screen.
- All transitions go through `FadeController` (no instant cuts except inside lab/debug).
- `HoleProgressionService` correctly tracks `HasPlayed` for completed holes after the session ends (in-memory; persistence later).
- Bottom nav works from every screen, with single source of truth (PersistentUI).
- Settings opens from any screen via single controller.
- No new `Debug.LogWarning` floods on screen transitions.

---

## Pipeline routing

- Stages A, B, D, E: **TELLCODE** if scope holds (multi-file, established patterns, no asmdef/scene changes). Architect writes pointer + folder, Code implements.
- Stage C (Result modal): **FULL PIPELINE** — visual fidelity, new arch surface, failure-prone tasks gate. Subagent chain.
- Stage F: **TELLCODE** unless the scale tween needs DOTween (then verify project has it; if not, **SURGICAL** to add an 8-line tween helper).

Next concrete action: Cesar answers the 5 open questions above. Stage A SPEC.md written immediately after.
