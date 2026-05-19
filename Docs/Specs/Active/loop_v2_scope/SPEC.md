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

## Open questions for Cesar (answer before Stage B kicks off)

1. **Stage A scope.** Do we land BOTH P0-1 (nav bar consolidation) AND P0-2 (settings consolidation) in Stage A, or split them? My take: bundle, both are surgical and same risk profile.
2. **Sub-spec firing cadence.** Stage A → B → C as separate full-pipeline runs (implementer → self-reviewer → reviewer → Cesar)? Or roll A+B as one task since they're both small and don't overlap files? My take: separate. A is "delete + rename"; B is "extend + namespace move." Different review surfaces.
3. **Result modal placement.** Lives in the **gameplay scene's UI canvas** (alongside `HoleCompleteWidget`'s current home), or as part of the **ShellScene** that survives scene loads? My take: gameplay scene for now — keeps the modal close to the signal it listens for (`BallStateMachine.OnShotComplete`). Promote to ShellScene only if Stage D's scene-load work creates a transition flicker.
4. **Reward grant on REPLAY.** Replay rewards are usually smaller (per `HoleData.replayRewards`). Confirm: should they grant **every** replay clear, or only the first replay clear after the original first-completion? My take: every clear grants `replayRewards`. Daily caps live in a future progression system.
5. **PLAY NEXT vs MENU defaults.** On hole 18 (last hole), PLAY NEXT becomes invalid. Show only MENU? Or replace PLAY NEXT with "MAIN MENU" and keep two buttons for muscle memory? My take: show only MENU, prominent. Add a "you cleared the course" toast.

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
