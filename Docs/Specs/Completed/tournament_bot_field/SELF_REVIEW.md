# SELF_REVIEW — tournament_bot_field (T3)

**Iteration:** 2
**Reviewed:** 2026-06-25 19:42 CEST
**Reviewer:** golfin-self-reviewer
**Verdict:** **FORWARD_TO_ARCHITECT** (PASS)

---

## Iter-1 history (context)

- Iter-1 self-review: PASS → architect-reviewer PASS → **red-team FAIL** for "bracket-mix invariant is dead — a uniform-random sampler passes the test." Three secondary findings (B1 RollPaceSchedule dead branches, B2 Project else-break coupling, B3 inlined-CSV drift).
- Iter-2 was instructed to fix all four. This review verifies iter-2.

## Task type

PIPELINE_HARDENING Rule 3 — pure deterministic logic, headless NUnit suite IS the gate. Vision/Figma/bbox checks N/A. The pixel-scan steps are replaced by:
(a) live re-run of the test suite,
(b) line-by-line audit of the NEW assertions added in iter-2,
(c) independent re-derivation of the bracket-mix invariant's discrimination power.

---

## 1. Live test re-run (Rule 5)

Re-ran the EditMode suite via `unity-mcp-cli run-tool tests-run` with `testNamespace: "Golfin.Tournaments.Tests"`:

```
Status      = Passed
TotalTests  = 557  (557 discovered project-wide; 50 in Golfin.Tournaments.Tests namespace)
PassedTests = 50
FailedTests = 0
SkippedTests= 0
Duration    = 00:00:01.7954860
```

**50/50 PASS, 0 fail, 0 skip.** Matches the implementer's claim exactly. The +3 over iter-1's 47 = the three new tests (`RollField_BracketMix_RejectsWeightIgnoringSampler`, `RollField_Pace_ShortWindow_InvariantsHold`, `ShippedCSV_FakePlayers_MatchesInlinedFixture`).

---

## 2. Bracket-mix invariant — DID IT GAIN TEETH? (the primary blocker)

This is the only question that matters for iter-2. I checked the source line-by-line, then verified discrimination independently in Python.

### 2a. Did the primary test stop measuring the wrong thing?

Source: `Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs` L509–571 (`RollField_BracketMix_SampledTargetsApproximateWeights`).

- L548: `gen.RollField(MakeDef(seedId), cfg, NineHolePars, out var sampledBrackets);`
- L552: `foreach (var bk in sampledBrackets) aggregateCounts[bk] = ...`

This consumes the `internal` overload's `out IReadOnlyList<string> sampledBrackets` — populated at `BotFieldGenerator.cs` L242 (`sampled.Add(targetBracket)`), which is the literal return value of `SampleBracket(cfg.BracketWeights, ...)` at L241. **The test now measures the raw output of `SampleBracket`, NOT `BracketKeyForLevel(rosterMap[BotId])` (the iter-1 mistake).** This is the exact fix the red-team prescribed (option 1 of the three).

The test seam is correctly an `internal` overload + `out` param (not a new public field on `BotCard`):
- `BotCard.cs` (`Assets/Scripts/Tournaments/BotFieldConfig.cs` L82–120): `git diff 1018f93b5 HEAD -- Assets/Scripts/Tournaments/BotFieldConfig.cs` returns 0 lines. **T1 contract is untouched.** ✓
- `BotFieldMath.cs` L8–9: `[assembly: InternalsVisibleTo("Golfin.Tournaments.Tests")]` — metadata only, no UnityEngine drag-in. ✓
- `BotFieldGenerator.cs` L191–197: public `RollField` delegates to internal overload (L208–288) so the production API is unchanged. ✓
- `grep "using UnityEngine"` on `BotFieldMath.cs` and `BotFieldGenerator.cs` → **zero matches**. Headless integrity preserved. ✓

### 2b. Does the negative control actually discriminate?

Source: L594–662 (`RollField_BracketMix_RejectsWeightIgnoringSampler`).

- L631–632: `var rng = new Xorshift64(BotFieldHash.StableHash($"{seedId}:bracket"));` — mirrors `BotSeedFactory.BracketStream(def.Id)` exactly.
- L635: `string bracket = UniformBracketSampler(keys, rng);` (defined L481–482 as `keys[rng.NextInt(keys.Count)]` — strictly weight-ignoring).
- L644–656: aggregates counts, asserts `anyViolationFound == true` against the same 8pp tolerance as the primary test.

This is the structural inverse of the primary test — same tolerance, same N, same seeds, opposite expectation. Exactly the discrimination proof the red-team demanded.

**Independent verification (Python port of FNV-1a + xorshift64 + the exact seed IDs):**

```
Uniform sampler aggregate over 5 seeds × 120 = 600 draws:
  bracket   1: expected 10.0%, observed 16.2%, Δ=6.17pp
  bracket  10: expected 15.0%, observed 17.8%, Δ=2.83pp
  bracket  25: expected 20.0%, observed 14.8%, Δ=5.17pp
  bracket  50: expected 25.0%, observed 16.3%, Δ=8.67pp  *VIOLATES 8pp*
  bracket 100: expected 20.0%, observed 17.8%, Δ=2.17pp
  bracket 180: expected 10.0%, observed 17.0%, Δ=7.00pp
anyViolationFound = True (bracket "50" — 8.67pp > 8pp tolerance)
```

**The negative control will pass** (i.e. uniform sampler reliably triggers `anyViolationFound`). And for the primary test:

```
Correct weighted sampler same seeds × 120 = 600 draws:
  bracket   1: expected 10.0%, observed  9.0%, Δ=1.00pp  ok
  bracket  10: expected 15.0%, observed 17.5%, Δ=2.50pp  ok
  bracket  25: expected 20.0%, observed 18.2%, Δ=1.83pp  ok
  bracket  50: expected 25.0%, observed 26.0%, Δ=1.00pp  ok
  bracket 100: expected 20.0%, observed 18.5%, Δ=1.50pp  ok
  bracket 180: expected 10.0%, observed 10.8%, Δ=0.83pp  ok
```

The 8pp tolerance has ~0.67pp of headroom above the violation line — TIGHT but adequate. A fully-uniform sampler reliably fails; a correct sampler comfortably passes. **The invariant now has real, demonstrated discrimination power.**

### 2c. Discrimination sensitivity (extra rigor — not required, but worth noting)

I also tested an intermediate "partially-weight-ignoring" sampler (linear blend of weighted and uniform with mix fractions 0.25/0.5/0.75/1.0):

| mix | maxΔ | violates 8pp? |
|---|---|---|
| 0.00 (correct) | 2.5pp | no |
| 0.25 (subtle bias) | 2.7pp | no |
| 0.50 (half-uniform) | 4.5pp | no |
| 0.75 (mostly uniform) | 6.8pp | no |
| 1.00 (fully uniform) | 8.83pp | **yes** |

The negative control only catches a NEAR-FULLY uniform sampler. A 50%-uniform-50%-weighted bug would pass silently with 4.5pp maxΔ. **Documented weakness — not a blocker** (the red-team's stated requirement was "uniform-random sampler fails," which is met), but worth surfacing to the architect: the test catches catastrophic weight-failure, not subtle weight-dilution. Could be tightened by widening N or tightening tolerance if subtler regressions become a concern.

**Bracket-mix verdict: PASS.** Real teeth. Negative control real. T1 contract preserved.

---

## 3. Question 3.5 — single-seed clustering: is it a real product concern?

This is the prompt's stand-out question. I tested the actual production scenario: single-seed bracket sampling at `BotCount=24/60/120` across 10 realistic tournament IDs (`weekly_lomond_2026_07_01`, `daily_lomond_2026_06_25`, etc.).

| N | xorshift64 meanMaxΔ | xorshift64 worstMaxΔ | Python Random (Mersenne) meanMaxΔ | Python Random worstMaxΔ |
|---|---|---|---|---|
| 24 | 10.25pp | 17.50pp | 12.42pp | 30.00pp |
| 60 | 7.17pp | 13.33pp | 7.67pp | 16.67pp |
| 120 | 5.67pp | 9.17pp | 5.43pp | 11.67pp |
| 500 | n/a | n/a | 2.66pp | 6.00pp |

**Conclusion: the "xorshift64 short-range clustering bias" the implementer noted is NOT a generator-quality defect — it is normal multinomial sampling variance.** At N=24, the variance is statistically indistinguishable from a high-quality Mersenne Twister (the gold-standard PRNG). The implementer's framing "xorshift64 short-range clustering bias" in the spec-deviation note is slightly misleading — it implies a generator issue, but it's actually just `σ = sqrt(p(1-p)/N)` binomial noise, which would affect ANY PRNG identically.

**Product implication:** a single live tournament with `BotCount=24` and an authored weight of 25% for one bracket can legitimately show observed-representation anywhere from ~10% to ~35% (roughly 2σ binomial range). At `BotCount=120`, the range tightens to ~15%–35% (1σ ≈ 4pp). **This is acceptable organic variance for any seeded multinomial sample** — fixing it would require either dropping no-repeat-identity (a worse trade) or implementing stratified sampling (a substantively different algorithm).

**Recommendation to architect/Cesar:** the single-seed variance is a product property, not a defect. Authors should treat `BracketWeights` as a *distribution intent*, not a per-tournament quota. If exact per-tournament quotas are required (T4-era question), the algorithm needs to switch to stratified deterministic assignment (sort the BotCount into exact integer quotas, then fill each quota), which is a T4 SPEC question, not a T3 fix. The aggregate 5-seed test is the right way to gate it.

**Flag (not a blocker):** the implementer's spec-deviation note should be corrected to say "normal multinomial sampling variance at small N, not a generator-quality defect." Currently reads as if xorshift64 is somehow worse than alternatives, which my Mersenne comparison disproves.

---

## 4. Secondary fixes (B1/B2/B3)

### B1 — `RollPaceSchedule` short-window pace test

Source: `BotFieldGenerator.cs` L506–585 (4-phase algorithm: compress → forward strict-increase → backward clamp → forward strict-increase). Test: `BotFieldInvariantTests.cs` L868–930 (window = 2×H seconds = 18s, perHoleSpreadSec = 5s, jitter ≫ nominalStep of 2s).

Analysis:
- nominalStep = 18/9 = 2s. Per-hole step = 2s + jitter[0..5s] = avg ~4.5s/hole × 9 holes ≈ 40.5s nominal. Window = 18s. **Overrun ratio ~2.25× → Phase 1 compress branch is reliably hit.**
- Test asserts both strict-increase AND `≤ endUtc` for every completion across 20 bots. The red-team's adversarial case (window=8s, H=9 → completions past endUtc, equal-time bugs) is now caught.
- The compress branch (L539–549) and backward-clamp branch (L564–568) are now exercised, where iter-1 had them as dead code in the test suite.

**Spec deviation flag (already in implementer's report):** B1 uses 2×H seconds (18s), not exactly H seconds. The implementer's reasoning is sound — at exactly H seconds (9s window, 1s/hole minimum step), the algorithm correctly pins multiple completions to `endUtc` (i.e. strict-increase becomes impossible by physics, not by bug). 2×H seconds is a strictly stronger probe of the compress logic. PASS.

### B2 — `Project()` else-break removed

Source: `BotFieldGenerator.cs` L303–323. L309–315 shows the explicit loop without `else break`; comment at L313–314 documents the B2 fix. **`Project` is now O(H) always but correct even if pace ever produced a non-monotonic schedule.** H ≤ 18, so the cost is trivial. Coupling to the pace invariant is removed.

All existing projection tests exercise this path (they use real RollField output, which goes through the loop). PASS.

### B3 — Shipped CSV drift guard

Source: `BotFieldInvariantTests.cs` L947–1002. Walks the assembly directory up to find `Assets/Resources/Data/fake_players.csv`, parses it, and compares row count + first/last row id/username/level against the inlined `FAKE_PLAYERS_CSV` fixture.

The test uses `Assert.Inconclusive` if the CSV file isn't found (e.g. in a packaged-only environment), which is the correct way to avoid false failures in non-development builds. In the EditMode run (live, 50/50 PASS), the test ran successfully — meaning it found the CSV and matched the fixture. If the shipped CSV ever drifts from the inlined literal, this test will fail loudly. PASS.

---

## 5. Rule 13 / files-modified integrity

Implementer report claims `Assets/Scenes/ShellScene.unity` is pre-existing T7 drift, identical between iter-1 commit `1018f93b5` and HEAD. Verified:

```
$ git diff 1018f93b5 HEAD -- Assets/Scenes/ShellScene.unity | wc -l
0
```

**Zero diff.** Iter-2 did NOT modify ShellScene. The dirty state was inherited from a prior session (T7 ShellScene work). Heartbeat baseline at iter-2 kickoff confirms ShellScene was already dirty before iter-2 began. Rule 13 attribution is honest.

`git status --porcelain --untracked-files=all`:
```
 M Assets/Scenes/ShellScene.unity                              [pre-existing T7 drift]
 M Assets/Scripts/Tournaments/BotFieldGenerator.cs             [iter-2 fix]
 M Assets/Scripts/Tournaments/BotFieldMath.cs                  [iter-2 InternalsVisibleTo]
 M Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs  [iter-2 tests]
 M Docs/Specs/Active/tournament_bot_field/HEARTBEAT.log
 M Docs/Specs/Active/tournament_bot_field/IMPLEMENTER_REPORT.md
 M Docs/Specs/Active/tournament_bot_field/STATUS.md
?? Docs/Specs/Active/tournament_bot_field/ARCHITECT_REVIEW.md
?? Docs/Specs/Active/tournament_bot_field/SELF_REVIEW.md
```

No undeclared code drift. All iter-2 code changes are in the report's files-modified table.

---

## 6. Full SPEC §7 invariant re-walk (Rule 5 — entire list, every iteration)

| SPEC §7 invariant | Test(s) | iter-2 verdict |
|---|---|---|
| Determinism: RollField twice deep-equal | `RollField_Determinism_SameArgsSameResult` (L378) | PASS (still real per-card / per-hole compare; unchanged from iter-1) |
| Stable-hash known vector | `StableHash_KnownVector_abc` (L218) | PASS (pinned constant verified by iter-1 architect-review via FNV-1a re-derivation, no source change in iter-2) |
| Field size = BotCount | `RollField_FieldSize_EqualsBotCount` (L367) | PASS |
| Identities ⊆ fake_players | `RollField_BotIds_AllInFakePlayers` (L425) | PASS |
| No duplicate identities | `RollField_BotIds_NoDuplicates` (L440) | PASS |
| **Bracket mix ≈ weights** | `RollField_BracketMix_SampledTargetsApproximateWeights` (L509) + negative control (L594) | **PASS — invariant gained teeth in iter-2 (the primary blocker fix); verified by independent Python re-derivation** |
| Strokes bounds 1 ≤ s ≤ par+4; Total = Σ | `RollField_StrokesBounds_ValidForAllHoles` (L452), `RollField_SingleHolePar_StillValid` (L1026) | PASS |
| Pace strictly increasing | `RollField_Pace_StrictlyIncreasing` (L669) + short-window B1 (L868) | PASS — B1 fix now exercises compress branch |
| Pace ∈ (startUtc, endUtc] | `RollField_Pace_AllInTournamentWindow` (L684) + B1 | PASS |
| Projection purity | `Project_Purity_SameCardAndTimeYieldsSameResult` (L706) | PASS |
| Projection monotonicity | `Project_Monotonicity_ThruNonDecreasing` (L722) | PASS |
| thru(startUtc − 1s) = 0 | `Project_BeforeStart_ThruIsZero` (L747) | PASS |
| thru(endUtc) = H | `Project_AtEndUtc_ThruEqualsH` (L768) | PASS |
| Reveal trickle 0 < Σthru < H·BotCount | `Project_Trickle_PartialFillAtMidWindow` (L833) | PASS |
| (Bonus) complete ⇔ thru == H | `Project_Complete_IffThruEqualsH` (L789) | PASS |
| (Bonus) revealedStrokes = Σ partial | `Project_RevealedStrokes_EqualsPartialSum` (L811) | PASS |
| **(B2)** Project no `else break` | source L309–315 + comment; all existing tests exercise the path | PASS |
| **(B3)** Shipped CSV drift guard | `ShippedCSV_FakePlayers_MatchesInlinedFixture` (L947) | PASS — guards a real maintenance hazard |

Every §7 row + the three iter-2 additions = real assertion or verified source change. **Zero tautologies.** Zero spec-text-only compliance.

---

## 7. Issues to surface (non-blocking, for architect awareness)

1. **Subtle weight-dilution bugs slip past the 8pp tolerance** (§2c above). The negative control catches ≥80%-uniform samplers; a 50%-uniform-50%-weighted sampler passes silently with 4.5pp maxΔ. Could be tightened with more seeds or stricter tolerance if subtler regressions become a concern. For now, the red-team's discrimination requirement (uniform sampler fails) is met.

2. **Single-seed variance is normal multinomial noise, NOT generator quality.** The implementer's spec-deviation note frames it as "xorshift64 short-range clustering bias," which my Mersenne baseline disproves — Python's Random produces the same variance at N=120. Worth correcting the note's framing for accuracy, but it doesn't change any test behavior. (Question 3.5 detail in §3 above.)

3. **Product property — authors should treat `BracketWeights` as distribution intent, not per-tournament quota.** A live tournament with `BotCount=24` will routinely show 10pp+ deviations from authored weights. If exact per-tournament quotas are needed, T4 should switch to stratified deterministic assignment. Not a T3 fix.

4. **The N=120 + roster cap is still an open T4 question** (architect's iter-1 § 5 — does any production tournament need `BotCount > 120`?). T4's SPEC must answer this.

5. **Inlined `FAKE_PLAYERS_CSV` literal in tests is now drift-guarded by B3** — good. The architect's iter-1 § 6a flag is addressed.

---

## 8. Iter-2 verdict

**FORWARD_TO_ARCHITECT** → STATUS `SELF_REVIEW_PASS`.

The single hard blocker (bracket-mix dead invariant) is fixed correctly and verifiably:
- The test measures `SampleBracket` output via an internal `out` seam, NOT roster composition. ✓
- A negative control proves the test discriminates a weight-ignoring sampler at the chosen tolerance. ✓
- T1 contract (`BotCard`) is untouched — the seam is an internal overload, not a public field. ✓
- Headless integrity preserved — no UnityEngine imports in math/generator. ✓

The three secondaries (B1/B2/B3) are fixed and tested:
- B1: 4-phase pace algorithm with short-window test exercising the compress branch. ✓
- B2: `Project()` else-break removed; comment documents the fix. ✓
- B3: real shipped CSV cross-checked against inlined fixture. ✓

Live test re-run: 50/50 PASS, matching the implementer's claim. Rule 13 attribution honest (ShellScene drift verified pre-existing).

The architect should be aware of the non-blocking issues in § 7, especially the subtle-bias gap in § 2c and the single-seed variance framing in § 3, but none of them are grounds for rejection.

---

## Iter-2 history

- 2026-06-25: SELF_REVIEW_PASS (iter-2). Bracket-mix invariant now has real teeth — internal `out` seam exposes `SampleBracket` output, negative control proves discrimination at the chosen 8pp tolerance against a uniform sampler (verified independently in Python: bracket "50" produces 8.67pp Δ, 0.67pp above tolerance). B1/B2/B3 secondaries fixed and tested. 50/50 PASS confirmed live. T1 contract preserved. Hands to architect-reviewer.
