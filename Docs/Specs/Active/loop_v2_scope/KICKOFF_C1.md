# \ud83c\udfcc\u200d\u2642\ufe0f Kickoff for fresh chat \u2014 Stage C1: ShellScene Result Modal

```
Stage C1 \u2014 ShellScene Result modal. Architect-led, output is a SPEC. Pipeline = FULL PIPELINE (visual fidelity + new architecture surface per scoping SPEC line 251).

# Context \u2014 2026-05-20 (continuing from prior chat)

**Loop v2 progress:**
- \u2705 Stage A `loop_v2_a_singletons_consolidation` (commit 8ee5c1d2) \u2014 nav-bar + SettingsController duplication collapsed
- \u2705 Stage B `loop_v2_b_session_state_plumbing` (commit 0e61d497) \u2014 GameSession namespace move + OnHoleComplete event + Matchmaking seed
- \u2705 Stage C0 `loop_v2_c0_matchmaking_to_gameplay_transition` (commit ace9e1ec) \u2014 first end-to-end production playthrough
- \u2705 `loop_v2_smoke_bot` (commit a8901d99) \u2014 reusable bot framework, becomes default visual gate for C1/D/E/F
- \u23ed Stage C1 \u2014 this is the one

**Quick state check:** clean repo, last commit `92f183ec` (AI_CONTEXT update), tree clean, on main.

---

## What Stage C1 is

The ShellScene-resident Result modal. Subscribes to `GameSession.OnHoleComplete` (Stage B's event), shows SUCCESS or FAILED card with strokes / par / score-vs-par / shot history / rewards. Two action paths: PLAY NEXT (next hole load) and MENU (return to Home), plus RETRY on FAILED.

Per scoping SPEC `Docs/Specs/Active/loop_v2_scope/SPEC.md` lines 117\u2013157 + 185\u2013202.

**Decisions already locked (do not re-litigate):**
- Q3 = Option B \u2014 modal lives on **ShellScene UI canvas**, not gameplay scene
- Cross-scene signal = `GameSession.OnHoleComplete` (Stage B shipped this)
- Reward grant = direct calls (`RewardPointsManager.AddPoints`, `ItemManager.AddItem`, `BallManager.AddBall`) \u2014 no event bus yet (foundation #4 deferred until second consumer exists)
- Every replay clear grants `replayRewards`, no daily caps in this milestone
- Hole 18 SUCCESS hides PLAY NEXT, prominent MENU, plus a "course cleared" toast
- Modal extends `ModalController` (free fade-in/fade-out + backdrop)

---

## What exists (don't rebuild \u2014 bind to it)

- `GameSession.OnHoleComplete` event with `HoleCompletionData` payload \u2014 fires when ball state reaches `InCup` (Stage B + C0 wired this; smoke bot proves the path via `ForceShotComplete("InCup")` seam)
- `HoleProgressionService` (read API, see audit P0-3) \u2014 needs writer in Stage C1
- `ModalController` base class \u2014 `MatchmakingModalController` is the gold-standard template to copy
- Lab `HoleCompleteWidget` at `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` \u2014 the design reference; production modal is its ShellScene-resident successor
- `HoleData` (with `rewards` + `replayRewards` arrays)
- `LoadingScreenController.PrepareForHoleLoad(int)` (from C0) \u2014 PLAY NEXT path uses this
- `GameplaySceneLoader.UnloadGameplay()` (from C0) \u2014 MENU path uses this
- Smoke bot `Hole1Playthrough` scenario with `ForceShotComplete("InCup")` \u2014 default visual gate for C1; will need a new scenario extension or DoD updates to capture PLAY NEXT and MENU paths

---

## What Stage C1 actually builds

1. **`HoleCompleteModalController`** \u2014 new ShellScene-resident modal, extends `ModalController`. Subscribes to `GameSession.OnHoleComplete`.
2. **`IHoleProgressionStore`** \u2014 new interface (Foundation #1, the one we deferred from Stage B). Wraps existing `HoleProgressionService` read + adds writer methods `MarkHolePlayed(int)` + `UnlockHole(int)`. Audit P0-3 finally closed.
3. **Reward grant on SUCCESS** \u2014 iterate `HoleData.rewards` (first clear) or `replayRewards` (replay), call the three managers directly. Defensive null-checks throughout.
4. **PLAY NEXT button** \u2014 writes progression, calls `LoadingScreenController.PrepareForHoleLoad(next)` then `GameplaySceneLoader.BeginGameplayLoad(next)`. Reuses C0 infrastructure.
5. **MENU button** \u2014 writes progression (if SUCCESS), calls `GameplaySceneLoader.UnloadGameplay()`, navigates to Home.
6. **RETRY button** (FAILED only) \u2014 reloads same hole via the C0 path with current `GameSession.CurrentHoleNumber`.
7. **Hole 18 special case** \u2014 PLAY NEXT hidden, MENU prominent, "course cleared" toast.
8. **Lab `HoleCompleteWidget` retirement decision** \u2014 keep as debug fallback or delete? My take: keep behind `#if UNITY_EDITOR` for lab smoke runners (\u00a72d/\u00a72f still need it), production never sees it. Lock in SPEC.

---

## Architectural pre-flight (verify in repo before SPEC commit)

1. **`HoleCompletionData` payload sufficiency** \u2014 today carries `TerminalState`, `Strokes`, `PenaltyStrokes`, `HoleNumber`, `CompletedAtUtc`. Stage C1 needs: score-vs-par badge (Eagle/Birdie/Par/Bogey/etc) computed from Strokes vs `HoleData.par`. SHOULD score-vs-par live in `HoleCompletionData` (computed at fire time) or in the modal (computed on display)? My take: in the modal \u2014 `HoleCompletionData` stays lean, view computes its own display state from raw numbers.
2. **Shot history scrolling** \u2014 `GameSession.ShotHistory` is `List<ShotRecord>`. Verify what each `ShotRecord` carries (club, distance, surface, terminal?) so the modal's history list has the right fields. May need a `ShotRecord` extension or richer payload \u2014 flag in SPEC if so.
3. **FAILED state detection** \u2014 Stage B fires `OnHoleComplete` only on `BallState.InCup`. FAILED state (stroke-cap, time-out, OOB-cap) needs a separate fire path. Stage C1 SPEC must specify the FAILED trigger \u2014 likely `HoleCompletionBridge.cs` (or extension thereof) detects stroke-cap and calls `GameSession.MarkHoleComplete(FAILED_payload)`. Check what stroke-cap logic exists today.
4. **Reward Type enum coverage** \u2014 `HoleData.rewards` is `List<HoleReward>` with a `RewardType` enum. Verify what enum values exist (Points / RepairKit / Ball / ???) and that all have a corresponding manager method.
5. **Toast infrastructure** \u2014 Hole 18 needs a "course cleared" toast. Does any toast system exist? If not, Stage C1 builds the minimum (could be a temporary modal extension, or a brand-new tiny `ToastController`). Flag.
6. **Modal Z-order vs C0 black-fade** \u2014 the modal lives on ShellScene UI canvas. C0's FadeController runs at a high sortOrder for the black fade. Verify the Result modal's z-order doesn't collide; lock the layering pattern in SPEC.
7. **`LoopV2SmokeBot` scenario coverage** \u2014 existing `Hole1Playthrough` captures s06 (modal visible). C1 needs new scenario(s) for PLAY NEXT, MENU, RETRY, and FAILED-state. Lock scenario list in SPEC.

---

## Pre-flight (do these FIRST in the new chat)

1. `conversation_search` for "Stage C1" / "Result modal" / "HoleCompleteModalController" \u2014 pick up any context not in this opener
2. `recent_chats` last 1 chat \u2014 read the bot framework session for full context on the seam principle and reusability contract
3. Verify clean state: should be at commit `92f183ec`, tree clean
4. Read `Docs/Specs/Active/loop_v2_scope/SPEC.md` lines 117\u2013250 (the full Stage C section)
5. Read `Docs/Architecture/BOT_FRAMEWORK.md` \u2014 understand the bot framework before specifying its new scenarios for C1
6. Read `Assets/Scripts/Gameplay/UI/ShotUI/HoleCompleteWidget.cs` + `HoleCompleteData.cs` \u2014 the design reference
7. Read `Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs` \u2014 the gold-standard ModalController template

---

## Pipeline routing

**FULL PIPELINE** per scoping SPEC line 251 \u2014 visual fidelity + new architecture surface + failure-prone task gate.

Sub-spec folder: `Docs/Specs/Active/loop_v2_c1_result_modal/`
Per-task convention: SPEC.md + STATUS.md + IMPLEMENTER_REPORT.md + SELF_REVIEW.md + ARCHITECT_REVIEW.md + screenshots/

Visual gate: smoke bot Hole1Playthrough s06 already captures the modal; SPEC adds new scenarios for PLAY NEXT and MENU paths (and FAILED state if scoped).

---

## Output expectations

**Single deliverable at end of this session:**
- `Docs/Specs/Active/loop_v2_c1_result_modal/SPEC.md` \u2014 full SPEC.md with:
  - Goal / DoD / pre-flight checklist resolutions
  - Files CREATED / EDITED / DELETED (concrete paths)
  - Architectural decisions locked (with rationale)
  - New EditMode tests list (target 305+N PASS)
  - Visual gate via smoke bot scenarios
  - Risk register
  - Out-of-scope list (defer Stage D handlers, defer save-layer persistence, etc.)

Plus the boilerplate STATUS.md + Notion entry creation.

---

## Open paranoia carried forward

- Phase B Stage 3 (real physics calibration) still deferred until after Loop v2 ships
- `IHoleProgressionStore` interface is Foundation #1 \u2014 only `IHoleProgressionStore` and `ISessionStore` get interface treatment in Loop v2; do NOT retrofit interfaces over CharacterManager/BagManager/etc.
- `MarkHoleComplete` for FAILED state is a Stage C1 SPEC decision \u2014 if it needs new code in `HoleCompletionBridge` or BallStateMachine, that's scope here
- Lab `HoleCompleteWidget` retirement \u2014 lock decision in SPEC, don't punt
- Demo videos for Stage C1 should drop in `Docs/Videos/loop_v2_c1_result_modal/`
- Defunct Notion UUID `400b667c...` \u2014 never use; current is `364b3e97-02b7-819b-a734-dfe5a3a087a9`
- Use Mac paths (`/Users/cesar/Documents/GolfinRedux`); Code uses MCP scene wiring (never paste-for-Cesar)

`Begin with the pre-flight, then SPEC.md.`
```

---

Paste that into the new chat. Stage C1 is where Loop v2 starts to feel like a real product loop \u2014 the modal is the moment of payoff after every hole.
