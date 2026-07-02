# RED-TEAM REVIEW — 1v1_result_rewards_display (Stage 2, iter-3)

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-07-02 (JST)
**Verdict:** **ARCHITECT_REVIEW_PASS**
**Governing ruling:** `CESAR_RULING.md` (2026-07-02) — Stage 2 accepted on code + Stage-1 proof;
ModeSelection/shell capture-background objection is WAIVED and was NOT used as grounds for any
finding here. Attacked the CODE and the reward-row RENDER per the ruling.

I tried to break this along all seven attack vectors and came up empty. Every claim below is
re-derived from source/diff/render I inspected myself, not carried from the reviewer's PASS.

---

## Attack 1 — RewardGranter extraction behavior-preserving (Practice-hole regression risk) → HOLDS

Re-derived from `git diff HEAD -- HoleCompleteModalController.cs`. The ENTIRE diff is:
- REMOVED: only the inner `foreach (var r in pool) { switch(r.type){Points/RepairKit/Ball} }` loop.
- ADDED: `GolfinRedux.UI.RewardGranter.Grant(pool);`

Everything guarding the grant is UNTOUCHED (verified by line-read, not trust):
- `GrantRewards` line 241: `if (!_lastSuccess || _rewardsGranted) return;` — double-grant guard PRESENT.
- line 242: `_rewardsGranted = true;` — one-shot PRESENT.
- line 247: `var pool = _wasReplay ? hole.replayRewards : hole.rewards;` — replay-pool select PRESENT.
- Guard fields `_lastSuccess/_wasReplay/_rewardsGranted/_lastSessionData` (lines 52–55) PRESENT.
- Callers `OnReplay` (278) and `OnPlayNext` (321) still invoke `GrantRewards()`.

`RewardGranter.Grant` switch is a verbatim copy: `Points→EarnPoints`, `RepairKit→AddItems`,
`Ball→AddBalls`, with default IDs `repairkit_common`/`ball_golfin` matching the old
`REPAIR_KIT_DEFAULT_ID`/`BALL_DEFAULT_ID` byte-for-byte. Practice hole-complete cannot double-grant
nor grant the wrong pool. **Regression: NONE.**

## Attack 2 — Versus grant correctness (P1Win-only, no RP leak) → HOLDS

`VersusResultHandler.HandleMatchComplete` (line 88–96): `RewardGranter.Grant(winRewardList)` is
inside `if (outcome == P1Win)`; the `else` branch grants nothing (log only). Lose/draw = 0 grant.
Confirmed against the live render: top-bar `R 80,200` is IDENTICAL in the WIN and LOSE captures,
i.e. the LOSE branch added zero RP. No RP leak.

## Attack 3 — Stage-1 flat EarnPoints fully removed (no double-grant) → HOLDS

`git diff` shows the exact deletion of the Stage-1 `RewardPointsManager.Instance.EarnPoints(reward)`
block; it is replaced by the single `RewardGranter.Grant` call. `grep -rn "EarnPoints|AddBalls|AddItems"`
across `Assets/Scripts/UI/Matchmaking/` + `VersusResultHandler.cs` returns ZERO hits — all versus
grants funnel through one `RewardGranter` call. No flat-plus-granter double-grant possible.

## Attack 4 — LOSE reward row: greyed-but-visible, exactly the win slots → HOLDS

`ShowResult` always calls `BindRewardRows(rewardList)` (the WIN list) regardless of outcome
(line 171); dimming is `_rewardRowGroup.alpha = localWon ? 1f : 0.5f` (168) PLUS direct child
tint `SetRewardChildrenColor(...Dim)` (169) so it survives all capture contexts. Children stay
active. **Render A/B confirms:** WIN = ONE bright gold coin + white `x200`; LOSE = ONE grey/brown
coin + grey `x200`, clearly present and clearly attenuated — not empty, not 3 placeholder slots.
The implementer flagged the perceptual match as "unclear"; on my own read the greying is
unambiguously visible. Legitimate PASS.

## Attack 5 — N-slot hide logic (no index-out-of-range) → HOLDS

`BindRewardRows` iterates a fixed `for (i=0; i<3; i++)`, reads `rewards![i]` ONLY when `i < count`
(guarded), and `rows[i].SetActive(i < count)`. `count = rewards?.Count ?? 0`. Empty/null list ⇒ all
3 rows hidden, no throw. 1-item list ⇒ row1 shows, rows 2&3 hidden. A list longer than 3 simply
fills 3 and ignores the rest — no overflow. `ParseAndAddRewardPair` (bounds-checks col indices,
skips empty/≤0 amounts) means the empty reward2/3 CSV columns add ZERO spurious slots — exactly
the one-slot render observed.

## Attack 6 — RANK-join resolves matched opponent, never top entry → HOLDS

`BindRankText` (262–308): opponent loop filters `!e.IsPlayer && e.Rank>0 &&
e.DisplayName == opponentPlayer.DisplayName` (284), leaves `"—"` if unmatched — no first-non-player
/ #1 fallback. Live proof in both renders: `You #116` (local) and `THRANDUIL #1` (matched
opponent) are distinct entries; #1 is the matched opponent's real rank, not a hardcoded top slot.

## Attack 7 — Scene/prefab/ban integrity → HOLDS

Re-ran every ban check myself:
- `git diff HEAD -- Assets/Scripts/Physics/` → EMPTY (capture scaffolding reverted per ruling).
- `Scenarios.cs` diff → EMPTY. No `*Gate` scenario.
- `Assets/Scenes/` diff → EMPTY. `M_Splash*.mat` → not in porcelain.
- Prefab diff = `+3` lines only: `_rewardRow1/2/3` fileID wiring. ZERO `m_AnchorMin/Max`,
  `m_SizeDelta`, `m_AnchoredPosition`, `m_LocalPosition`, `m_IsActive` mutations.
- NotoSansJP atlas dirt reverted (clean in porcelain).
- Uncommitted assets = exactly the 10 reported Stage-2 files; Packages MCP bump waived.
- `NewMatchButton` gold pill present in both renders; `ButtonPressFeedback` untouched by prefab diff.

---

## Prior rejections (CESAR_REJECTION iter-history) replayed

- **iter-1 "capture over title/splash":** GONE — v6 renders show the modal over course + shell
  chrome, no PLAY/Create-Account title splash. (Background itself waived for Stage 2.)
- **iter (self-review) "LOSE reward row EMPTY":** GONE — LOSE render shows one greyed-but-visible
  coin `x200`; `BindRewardRows` binds the WIN list on all outcomes; alpha+child-tint applied.
- **RANK `—` synthetic:** GONE — real `#116`/`#1` DisplayName-joined entries.

## Three break-attempts, why each failed

1. **Visual:** hunted the reward row for an empty/placeholder LOSE slot or a WIN slot that looked
   wrong — LOSE is a genuinely dimmed single slot, WIN is a bright single slot; symmetric role
   swap correct. No seam/mismatch found.
2. **Geometric/logic:** tried to force an IndexOutOfRange (empty list, >3 list) and a wrong-pool /
   double grant in hole-complete — both are structurally impossible given the preserved guards and
   the `i<count` read guard.
3. **Spec-intent:** checked for RP leak on loss (top-bar identical WIN vs LOSE = no leak) and for a
   surviving flat EarnPoints double-grant (grep = zero). Intent satisfied, not just the letter.

## Report integrity (Rule 6)

No fabrication found. The `80,200` top-bar in the WIN render corroborates the +200 grant claim;
the identical LOSE top-bar corroborates the zero-grant-on-loss claim. The implementer's `PASS*`
flags (slot-count deviation, LOSE dim intensity) are honest surfaced caveats, not gamed booleans.

## Verdict

All seven vectors held under adversarial scrutiny; code diffs are minimal and behavior-preserving;
the reward-row render is correct in both states. Advancing to **ARCHITECT_REVIEW_PASS** for Cesar's
final approval.
