STAGE_1_DONE

# STATUS — 1v1_result_rewards_display (Order 347)

**State:** Stage 1 DONE (Cesar-approved 2026-07-02, incl. opponent-rank fix folded in) ·
Stage 2 — spec authoring / kickoff pending.
**Priority:** P2
**Spec:** `Docs/Specs/Active/1v1_result_rewards_display/SPEC.md`

## Stage ledger
- [x] **Stage 0** — `VersusResultScreen.prefab` built; win/lose; real MMModal clone + portraits.
      **Approved by Cesar 2026-07-01 after iter-11.** Spawned always-on font-weight + rendered-size gate.
- [x] **Stage 1** — present `VersusResultScreen` as a modal after banner + live binding.
      **Approved by Cesar 2026-07-02** after iter-3 (4 gates: self→reviewer→red-team ARCHITECT_REVIEW_PASS).
      Red-team verified scene diff 226 ins / 0 del, zero out-of-scope prefab GUIDs, all wirings present,
      prefab byte-identical to Stage-0, handler subscribes to GameSession.OnMatchComplete.
      **Post-approval fix folded in (Cesar-directed):** opponent RANK now resolves the *matched*
      opponent by DisplayName join (mirrors BindOpponentCard) instead of the first non-player entry —
      previously every opponent showed the board leader's #1. `—` when opponent absent from the daily
      board. Local rank was already real (GetPlayerEntry). Compile-verified.
- [ ] Stage 2 — CSV-driven multi-reward grant + reward-row binding + NEW MATCH.
      **RANK-JOIN RE-CHECK (Cesar 2026-07-02):** reviewer must re-verify opponent rank resolves the
      actual `MatchContext` opponent (DisplayName join in `VersusResultScreenController.BindRankText`),
      not the top leaderboard entry, when the reward-binding work touches this controller.
- [ ] Stage 3 — polish (win/lose reward brightness, draw variant D2, transitions)

## iter-3 red-team verdict (golfin-redteam-reviewer 2026-07-02 06:26 CEST)
Independently re-measured every attack vector:
- Scene diff: **226 ins / 0 del** (was 5,078 lines / 2,152 del at iter-2).
- 18 anchor/pos/size hits: all belong to (a) new VersusResultModal/VersusResultHandler GOs
  or (b) prefab-instance overrides for THIS task's own VersusResultScreen.prefab
  (guid 15774d8c…, resolved from .prefab.meta).
- Out-of-scope prefab GUIDs (RankingsScreen 8bf3740e, MatchMakingModal 2bd69f22, 8× Tournament):
  **all 0 hits**. Only 4 guids in diff = this task's prefab + 3 this-task scripts.
- MMModal instance fileID 4390230621042469647 appears exactly ONCE — the `_matchmakingModal:`
  wiring reference (not a propertyPath mutation).
- No over-revert (0 deletions), all 4 wirings present, both new GOs m_IsActive:1,
  prefab-instance m_IsActive:0 (hidden), parented to real top-level Canvas.
- No functional regression: prefab byte-identical to Stage-0 (absent from git status),
  handler still subscribes to GameSession.OnMatchComplete, no synthetic entry / *Gate.
- Banned paths (Physics/, Scenarios.cs, M_Splash*.mat) untouched.
- Env dirt (Packages MCP 0.82.2→0.82.3 + review_misses.log) flagged for close-out only.
Iteration shape: `scene-hygiene:out-of-scope-prefab-drift`.
