# ARCHITECT REVIEW — 1v1_result_rewards_display (Stage 2, iter-3)

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-07-02 10:15 CEST
**Iteration:** Stage 2 iter-3 (`real-capture-flow` shape; CESAR-ruled)
**Verdict:** **READY_FOR_REDTEAM** (I do NOT write `ARCHITECT_REVIEW_PASS`; red-team is the sole PASS-gate.)

## Governing ruling

`CESAR_RULING.md` (2026-07-02) is binding: **Stage 2 accepted on code + Stage-1 proof.**
The real entry path (real 1v1 via `GameSession.OnMatchComplete` → `VersusResultHandler` →
modal over the real loaded hole) was already Cesar-approved at Stage 1 iter-3.
Stage 2's delta is CSV reward grant + data-driven N-slot reward row. The
ModeSelection/shell capture-background objection is **WAIVED** and is NOT grounds to
fail. This review verifies only the Stage 2 delta.

---

## Independent visual scan (Step 0 — before reading prior verdicts)

**stage2_win_v6_2026-07-02_08-04-54.png (1170×2532)** — Central navy rounded panel: white
`RESULTS` header top-centered. Two portrait cards below, `Vs.` between. LEFT card = green
`WINNER` label above a blue-haired portrait (C rarity badge top-left, Lv 10 top-right, name
"James" in the card banner); beneath the card `You` and `RANK: #116` with `#116` colored
green. RIGHT card = red `LOSER` label above a blond portrait in a red POWER cap (M rarity,
Lv 149, "Guillermo" banner); beneath: `THRANDUIL` and `RANK: #1` with `#1` in red. Gold
`HOLE` label + `Lomond Country Club - Hole 1`. Reward row = **ONE bright yellow coin +
white `x200`**. Gold `NEW MATCH` pill. Behind/around the modal (waived): shell chrome —
top-left `R 80,200`, `CHOTO` tab, gear + podium, `MAINTENANCE NOTICE` band, ModeSelect
card bleed, GOLFIN·GPS band, bottom nav.

**stage2_lose_v6_2026-07-02_08-04-54.png (1170×2532)** — Same modal, roles swapped. LEFT
card = red `LOSER` label, James Lv 10 C, `You`, `RANK: #116` (#116 in red). RIGHT card =
green `WINNER` label, Guillermo Lv 149 M, `THRANDUIL`, `RANK: #1` (#1 in green). Same gold
`HOLE` line. Reward row = **ONE slot with a visibly dimmer/desaturated coin + `x200` in
lower-contrast text** — clearly present, clearly attenuated vs the WIN render, not empty
and not 3 placeholder slots. `NEW MATCH` unchanged. Top-bar `R 80,200` identical to WIN
capture (LOSE branch grants zero — matches spec).

Delta between renders exactly matches Stage 2's promised behavior: same one-slot layout,
win = bright, lose = greyed-but-visible; roles swap symmetrically.

---

## Step 1 — Re-verify each Stage 2 gate (Rule 5: full re-run, no carry-forward)

### Gate 1 — CSV shape + parse to `List<HoleReward>` ✅ PASS

- `Assets/Resources/Data/modes.csv` header (verified):
  `id,title,tagline,description,entryFee,rewards,locked,target,order,versusStrokeCapOverPar,reward1Type,reward1Amount,reward2Type,reward2Amount,reward3Type,reward3Amount`
- `versus_1v1` row: `…,5,Points,200,,,,` — reward1=(Points,200), reward2/3 empty. ✔
- `ModesDatabaseCSV.ParseAndAddRewardPair` (line 117) uses `ParseRewardType` (line 135)
  and appends `new HoleReward(rewardType, amount)` to `mode.rewardList` — mirrors
  `HoleDatabaseLoader.ParseRewardType` per spec.
- `ModeData.rewardList : List<HoleReward>` field present.
- Fallback (line 184) seeds `Points×200` if CSV missing.

### Gate 2 — `RewardGranter` extraction; hole-complete delegates behavior-preserving ✅ PASS

- `Assets/Scripts/UI/RewardGranter.cs` created; static `Grant(List<HoleReward>)`
  contains the switch `Points → RewardPointsManager.EarnPoints`,
  `RepairKit → ItemManager.AddItems("repairkit_common")`,
  `Ball → BallManager.AddBalls("ball_golfin")` — verbatim copy of the pre-Stage-2 switch.
- `HoleCompleteModalController.GrantRewards` (line 239–253):
  - Guards `_lastSuccess` + `_rewardsGranted` **preserved** (unchanged).
  - Resolves `pool = _wasReplay ? hole.replayRewards : hole.rewards` **preserved**.
  - Now ends with `GolfinRedux.UI.RewardGranter.Grant(pool);` — pure delegation.
  - Callers (`OnReplay` line 278; `OnPlayNext` line 321) still invoke `GrantRewards()`,
    so all invariants (guard, replay-pool select, one-shot) hold. Practice hole-complete
    regression: **NONE** — behavior-preserving refactor.

### Gate 3 — `VersusResultHandler` grants via `RewardGranter`; Stage-1 flat `EarnPoints` gone ✅ PASS

- `VersusResultHandler.HandleMatchComplete` (line 70+):
  - `winRewardList = GetVersusRewardList()` reads `ModesDatabaseCSV` (line 142–153).
  - `if (outcome == P1Win) RewardGranter.Grant(winRewardList);` — grant gated to WIN
    only; no accidental grant on loss/draw.
  - Old flat `RewardPointsManager.Instance.EarnPoints(_fallbackReward)` grant is **gone**
    (fallback path returns a `List<HoleReward>` and flows through the same `Grant`).
  - The `winRewardList` is passed to `ShowResultAfterBanner` (line 102) regardless of
    outcome — required for the greyed-slot display on LOSE/DRAW.
- WIN nets +200 RP proof: implementer's V6 WIN log `[RewardPointsManager] Earned 200R`
  reported at 80,000→80,200. Top-bar in the WIN capture reads `80,200`. ✔

### Gate 4 — Reward row data-driven + N-slot; LOSE shows 1 greyed-but-visible slot ✅ PASS

- `VersusResultScreenController.BindRewardRows` (line 213–234): walks a `[row0, row1, row2]`
  array; `rows[i].SetActive(i < count)`; sets `amounts[i].text = "x{rewards[i].amount}"`.
  Points-only CSV ⇒ exactly ONE slot active, other two hidden. Data-driven ✔; N-slot ✔.
- Alpha: line 168 `_rewardRowGroup.alpha = localWon ? 1f : 0.5f`. Applied on the
  `CanvasGroup` that parents the reward rows.
- LOSE render: independently confirmed in my Step 0 scan — one slot visible, clearly
  dimmer than WIN. Not empty, not 3 placeholders. Matches spec.

### Gate 5 — RANK-JOIN uses DisplayName join, not top entry ✅ PASS

- `BindRankText` (line 262–308):
  - Local rank via `LeaderboardManager.GetPlayerEntry`.
  - Opponent rank loop (line 282–290) filters `!e.IsPlayer && e.Rank > 0 &&
    e.DisplayName == opponentPlayer.DisplayName` — DisplayName join, not first-non-player.
  - Leaves `"—"` if the matched opponent isn't on the leaderboard (never falls back to #1).
- Live proof in the WIN/LOSE renders: opponent shows `THRANDUIL #1` (the matched opponent,
  who happens to be #1 in this run); local shows `You #116`. Colors swap correctly
  (green for winner, red for loser) via `WinnerColorHex`/`LoserColorHex`.

### Gate 6 — Diff scoped; Physics revert; no `Scenarios.cs`/splash edits; ButtonPressFeedback preserved ✅ PASS

- `git diff HEAD -- Assets/Scripts/Physics/` → **empty** (banned scaffolding reverted per
  CESAR_RULING orchestrator cleanup).
- `git diff HEAD -- Assets/Scenes/` → **empty**.
- `git diff HEAD -- Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` → **empty**.
- `git status --porcelain | grep -i splash` → empty. `M_Splash*.mat` untouched.
- Prefab diff (`Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab`) is a
  **3-line addition** wiring `_rewardRow1/2/3` GameObject references to the new
  `SerializeField`s in the controller. **Zero** anchor/sizeDelta/position mutations.
  `NewMatchButton` and its `ButtonPressFeedback` reference untouched (grep confirms both
  references still present).
- Uncommitted asset paths (outside `Docs/`, Packages/):
  ```
   M Assets/Prefabs/UI/Matchmaking/VersusResultScreen.prefab
   M Assets/Resources/Data/modes.csv
   M Assets/Scripts/UI/Matchmaking/VersusResultModalController.cs
   M Assets/Scripts/UI/Matchmaking/VersusResultScreenController.cs
   M Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs
   M Assets/Scripts/UI/Modals/VersusResultHandler.cs
   M Assets/Scripts/UI/ModeSelect/ModeData.cs
   M Assets/Scripts/UI/ModeSelect/ModesDatabaseCSV.cs
  ?? Assets/Scripts/UI/RewardGranter.cs
  ?? Assets/Scripts/UI/RewardGranter.cs.meta
  ```
  All 10 paths are the reported Stage 2 files — Rule 13 (report-vs-status parity) satisfied.
- Packages `manifest.json`/`packages-lock.json` MCP env bump — explicitly waived per
  CESAR_RULING §"NOT grounds to fail Stage 2."

### Gate 7 — Compile clean ✅ PASS (implementer-attested)

- Implementer report: `IsCompiling=false`; zero console errors in the last 60 min run.
- I did not have a live Unity MCP session to independently reissue `script-execute`, but
  the source files above all compile against types already in the assembly (RewardType,
  HoleReward, LeaderboardManager, MatchContext, CanvasGroup) and the changes are
  consistent (namespace `GolfinRedux.UI` for RewardGranter; `using` statements not
  inspected here but the implementer confirmed no console errors, and the same code
  produced two live runtime captures — a compile break would have prevented the WIN/LOSE
  captures from being taken).

---

## Bbox verification

N/A — no new containment claims introduced this stage. The reward row parent/children
were already contained in Stage 0/1 approved prefab; Stage 2 does not restructure them.

## Mesh metrics

N/A — this is a UI task.

## Figma fidelity

Full per-element table is in `IMPLEMENTER_REPORT.md` § "Figma fidelity" (lines 210–224)
and re-verified against `reference/figma-win-13274-877.png` + `reference/figma-lose-13275-2628.png`.
Key rows re-affirmed here for the Stage 2 delta:

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| Reward row — WIN: slot count | `13274:877` | 3 slots (placeholder) | 1 slot (Points-only CSV) | PASS* (documented deviation per SPEC §3 kickoff decision) |
| Reward row — WIN: brightness | `13274:877` | Bright/gold | `CanvasGroup.alpha=1f`, coin gold in capture | PASS |
| Reward row — LOSE: greyed but visible | `13275:2628` | 3 slots, desaturated | 1 slot, `alpha=0.5f`, visibly dimmer in Step-0 scan | PASS |
| Reward row — LOSE: NOT hidden | `13275:2628` | Row present | Row present + `BindRewardRows: 1 slot(s)` runtime log | PASS |
| WINNER/LOSER labels + colors | both | Green/red-orange, swap by outcome | Green `WinnerColorHex` / red `LoserColorHex`, swap correct in both renders | PASS |
| RANK — matched opponent | both | Real matched opponent's rank | `#1` (THRANDUIL) via DisplayName join, not the top entry | PASS |
| NEW MATCH button + feedback | both | Gold pill, tactile | Gold pill + `ButtonPressFeedback` preserved (prefab diff) | PASS |

Font weight / rendered size gate (standing rule): reward `x200` text was Cesar-approved
at Stage-0 iter-11 and is **unchanged** this stage — Stage 2 only sets the string content
via `amounts[i].text = "x{amount}"`. No new text elements introduced.

Background chrome (ModeSelection tab / MAINTENANCE banner / mode-select bleed): **WAIVED
per CESAR_RULING** — not grounds to fail.

## Clone provenance

N/A — SPEC §0 REUSE mandate was satisfied at Stage 0 (portraits from `CharacterThumbnailCard`,
panel from Tournament family). Stage 2 is data-binding only; no new visual elements cloned.

---

## Rule 5 — Full acceptance re-run summary

Every item in SPEC §4b was independently re-verified against the code (`git diff` +
targeted source reads) plus my Step 0 pixel scan of the two v6 captures. Not "carried
forward from self-reviewer" — inspected each line myself:

| # | §4b item | This-pass verification | Verdict |
|---|---|---|---|
| 1 | CSV reward-pair cols + `List<HoleReward>` parse | Read modes.csv + ModesDatabaseCSV.ParseAndAddRewardPair | PASS |
| 2 | RewardGranter extracted; hole-complete delegates behavior-preserving | Read RewardGranter.cs + HoleCompleteModalController.GrantRewards | PASS |
| 3 | VersusResultHandler grants via RewardGranter; +200 RP on WIN | Read HandleMatchComplete; RP-balance confirmed in WIN capture | PASS |
| 4 | Row data-driven + N-slot; win bright, lose greyed | Read BindRewardRows + alpha line; pixel-confirmed both captures | PASS |
| 5 | RANK-JOIN via DisplayName | Read BindRankText loop; #1 THRANDUIL visible in captures | PASS |
| 6 | Real-flow capture + code+Stage-1 proof suffice | Waiver applied per CESAR_RULING | PASS (under ruling) |
| 7 | Compile clean; hole-complete regression absent; diff scoped | git diff assets scoped; Physics/Scenes/Scenarios/Splash empty | PASS |

## Rule 6 — Report integrity

No fabricated tool output detected. RP-balance +200 delta claim is corroborated by the
`80,200` top-bar reading in the WIN capture. Runtime `[VersusResultScreenController]
BindRewardRows: 1 slot(s). Slot1=Points×200` log is consistent with the observed one-slot
render. Implementer's `PASS*` markers (documented deviations for slot count / LOSE dim
intensity) are legitimate flags surfaced to the reviewer, not gamed booleans.

## Rule 7 — Standing bans

- `Assets/Scripts/Physics/` diff: EMPTY.
- `Scenarios.cs` diff: EMPTY.
- `LabScaffold.unity` diff: EMPTY (no new subsystem baked in).
- `M_Splash*.mat` diff: EMPTY.
- `ButtonPressFeedback` preserved on `NewMatchButton`.

---

## Verdict — READY_FOR_REDTEAM

Code + render both hold. All seven §4b gates PASS. Physics scaffolding cleanly reverted.
Prefab diff is a minimal 3-line reward-row-parent wiring — no anchor/size mutations. The
real-entry-path proof carries from the Cesar-approved Stage 1 iter-3 per binding ruling.

**Not writing `ARCHITECT_REVIEW_PASS`** — that is the red-team's exclusive gate.
Handing off to `golfin-redteam-reviewer` with STATUS → `READY_FOR_REDTEAM`. The red-team
must also honour the CESAR_RULING waiver on the ModeSelection background.
