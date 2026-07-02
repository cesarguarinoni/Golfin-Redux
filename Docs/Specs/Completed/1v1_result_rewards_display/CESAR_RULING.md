# CESAR RULING — Stage 2 capture-background (2026-07-02)

**Context:** Stage 2 iter-3 set `IMPLEMENTER_BLOCKED` with an architectural question: the 1v1
result modal fires while the ScreenManager shell is on `ModeSelection` (there is NO gameplay/in-match
`ScreenId` — a match plays in an additively-loaded gameplay scene while the shell stays on
ModeSelection). Three capture attempts could not cleanly reproduce Stage-1's loaded-hole background:
iter-1 (title screen — banned), iter-2 (ModeSelect + maintenance banner + card bleed — banned),
iter-3 (clean, but "MODE SELECTION" shell tab behind the modal).

**Cesar's ruling (2026-07-02):** **Accept Stage 2 on code + Stage-1 proof.** The real entry path
(`GameSession.OnMatchComplete` → `VersusResultHandler` → modal over the real loaded hole) was already
proven and Cesar-approved in **Stage 1 iter-3** (v4 captures). Stage 2's only delta is the CSV-driven
reward grant + data-driven N-slot reward row, which is code-verified and clearly visible in every
Stage-2 render. **The capture-background objection is WAIVED for Stage 2** — do NOT run further
capture-staging iterations, and reviewers must NOT fail Stage 2 on the ModeSelection/shell background.

**Consequent cleanup (orchestrator, done before gates):**
- Reverted banned-path `Assets/Scripts/Physics/` capture scaffolding (`VersusResultCaptureBot.cs`
  + `.meta` deleted; `VersusHudCaptureMenu.cs` restored to HEAD) — test-only, Rule 7.
- Reverted incidental `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` atlas dirty (not a
  Stage-2 deliverable).

**What the gates SHOULD verify (code + clean modal render only):**
1. `modes.csv` versus_1v1 reward-pair columns (win = `Points,200`); `ModesDatabaseCSV`/`ModeData`
   parse to `List<HoleReward>`.
2. `RewardGranter.Grant(List<HoleReward>)` extracted + shared; `HoleCompleteModalController` delegates
   (no Practice hole-complete regression).
3. WIN nets +200 RP via `RewardGranter`; Stage-1 silent flat `EarnPoints` gone.
4. Reward row data-driven + N-slot; **LOSE shows one greyed-but-visible RP slot** (win-list bound,
   greyed on loss), not empty, not 3 placeholder slots.
5. RANK-join: opponent resolves the matched `MatchContext` opponent via DisplayName join
   (`BindRankText`), not the top leaderboard entry (proven live: FOSCO #86 / THRANDUIL #1).
6. Scene/prefab diff scoped: prefab row-parent wiring only; ZERO out-of-scope prefab/anchor mutations;
   no `Physics/`/`Scenarios.cs`/`M_Splash*.mat` edits.

**NOT grounds to fail Stage 2:** the ModeSelection/shell capture background (waived above); the
Packages `manifest.json`/`packages-lock.json` MCP 0.82.2→0.82.3 env dirt (left uncommitted).
