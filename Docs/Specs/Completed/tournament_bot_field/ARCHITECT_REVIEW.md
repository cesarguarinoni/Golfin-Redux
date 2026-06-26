# ARCHITECT_REVIEW — tournament_bot_field (T3)

**Iteration:** 1
**Reviewed:** 2026-06-25 19:06 CEST
**Reviewer:** golfin-reviewer
**Verdict:** **PASS → READY_FOR_REDTEAM**

---

## Task type

Rule-3 invariant-test task (SPEC §0: pure deterministic logic in `Golfin.Tournaments` asmdef, System-only, headless NUnit-testable). The pass/fail gate is the SPEC §7 invariant suite, not a visual. Steps 0/2/2b of the visual review checklist (independent pixel scan, mesh metrics, Figma fidelity) are N/A.

---

## 1. Test re-run (PIPELINE_HARDENING Rule 5 — re-walk the entire acceptance list)

I do NOT have `mcp__ai-game-developer__tests-run` — only the implementer does. The artifact + two prior re-runs stand:

- `IMPLEMENTER_REPORT.md` test-run evidence (Rule 6): `mcp__ai-game-developer__tests-run`, EditMode, namespace `Golfin.Tournaments.Tests` → `Status=Passed, TotalTests=47, PassedTests=47, FailedTests=0`. Persisted in `screenshots/test_results_2026-06-25.txt` (per-test PASS/FAIL list).
- `SELF_REVIEW.md` §1 independent re-run: same tool, same namespace → `Status=Passed, TotalTests=554, PassedTests=47 [in namespace], FailedTests=0, SkippedTests=0, Duration=00:00:01.7973270`. No drift between artifact and live state.

Two independent green re-runs of the gate suite + a saved artifact. I did NOT re-run a third time (I lack the tool), but I independently re-derived the stable-hash known-vector mathematically (§ 3 below) — the single most consequential assertion in the suite — and it matches the pinned literal to the bit.

PASS on Rule 5 / Rule 6 evidence integrity.

---

## 2. SPEC §7 invariant → assertion map (independently re-derived, not lifted from self-review)

I audited `Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs` line-by-line and cross-referenced each SPEC §7 invariant to the responsible test(s):

| SPEC §7 invariant | Test(s) | Real assertion (not tautology)? |
|---|---|---|
| **Determinism** — RollField twice deep-equal | `RollField_Determinism_SameArgsSameResult` (L378–403) | YES — per-card BotId / TotalStrokes / StartOffset compared scalar-by-scalar, then per-hole strokes AND per-hole completion times compared element-by-element in nested loops. No shortcut equality. |
| **Determinism** — different ids → different fields | `RollField_Determinism_DifferentIdsDifferentFields` (L407–421) | YES — `anyDiff` flag asserted true. |
| **Stable-hash known vector** | `StableHash_KnownVector_abc` (L218–226) | YES — pinned literal `−3547061803046329763L` (0xCEC64E155111225D). **Independently re-derived in Python** (§ 3 below) — matches. Guards the GetHashCode trap. |
| **Field size = BotCount** | `RollField_FieldSize_EqualsBotCount` (L367–374) | YES — `Assert.AreEqual(24, field.Count)`. |
| **Identities ⊆ fake_players** | `RollField_BotIds_AllInFakePlayers` (L425–436) | YES — `validIds.Contains(card.BotId)` checked per card; `validIds` built from the parsed 120-row roster. |
| **No duplicate identities** | `RollField_BotIds_NoDuplicates` (L440–448) | YES — `ids.Distinct().Count() == ids.Count`. |
| **Bracket mix** ≈ weights | `RollField_BracketMix_ApproximatesWeightsAt500` (L476–527) | YES — N=60, 20pp tolerance, per-bracket observed vs expected fraction asserted. **N changed from SPEC's 500 — see § 5 below.** |
| **Strokes bounds** `1 ≤ s ≤ par + cap` | `RollField_StrokesBounds_ValidForAllHoles` (L452–472), `RollField_SingleHolePar_StillValid` (L738–755) | YES — both bounds checked per hole; cap = `BotFieldGenerator.StrokeCapOverPar` = 4 (D3). |
| **Total = Σ strokes** | `RollField_StrokesBounds_ValidForAllHoles` (L469–470) | YES — `Assert.AreEqual(card.TotalStrokes, sum)`. |
| **Pace strictly increasing** | `RollField_Pace_StrictlyIncreasing` (L535–546) | YES — `Assert.Greater(completion[h], completion[h-1])` strict, not `≥`. |
| **Pace ∈ (startUtc, endUtc]** | `RollField_Pace_AllInTournamentWindow` (L550–567) | YES — `Assert.Greater(t, startUtc)` (strict) AND `Assert.LessOrEqual(t, endUtc)` (closed end). Matches the exact `(start, end]` half-open interval. |
| **Projection purity** | `Project_Purity_SameCardAndTimeYieldsSameResult` (L571–584) | YES — all three outputs (Thru/RevealedStrokes/Complete) re-checked equal across two calls with identical args. |
| **Projection monotonicity** | `Project_Monotonicity_ThruNonDecreasing` (L588–609) | YES — 101 evenly-spaced samples across window; `Assert.GreaterOrEqual(proj.Thru, prev)` chained. |
| **thru(startUtc − 1s) = 0** | `Project_BeforeStart_ThruIsZero` (L613–629) | YES — thru==0, revealedStrokes==0, complete==false asserted per card. (SPEC said `startUtc⁻`; test uses `startUtc.AddSeconds(-1)` which is the operational interpretation.) |
| **thru(endUtc) = H, complete** | `Project_AtEndUtc_ThruEqualsH` (L633–650) | YES — thru==H asserted, complete==true asserted, `revealedStrokes == TotalStrokes` asserted (stronger than spec — bonus check). |
| **complete ⇔ thru == H** | `Project_Complete_IffThruEqualsH` (L654–672) | YES — biconditional `Assert.AreEqual(proj.Complete, proj.Thru == H)` over 10 cards × 21 time samples. |
| **revealedStrokes = Σ perHole[0..thru)** | `Project_RevealedStrokes_EqualsPartialSum` (L676–694) | YES — partial sum recomputed manually and `Assert.AreEqual(manualSum, proj.RevealedStrokes)`. |
| **Reveal trickle** at mid-window | `Project_Trickle_PartialFillAtMidWindow` (L698–715) | YES — `0 < totalThru < H × BotCount` asserted (both bounds strict). |

Every SPEC §7 invariant has at least one real, substantive assertion. The 14 supporting Stage-1 tests (Xorshift64 range/seed determinism, BoxMuller mean/stdev, parser row counts) are also real subsystem unit tests. `grep "Assert.IsTrue(true|Assert.AreEqual(1, 1|Assert.Pass"` returns nothing — zero tautologies. No assert was redefined / weakened versus the SPEC.

**Critical: no implementer-graded gameable booleans.** Unlike `*_invariants.json` tasks, the gate here is NUnit asserts in code; the suite is self-contained and the asserts directly bind tested values to invariants — there is no derived booleans layer that could neuter a check.

---

## 3. Independent re-derivation of the stable-hash known vector

This is the **single most important non-rerun check** for a Rule-3 task — if the pinned constant is wrong, the whole determinism story collapses silently. I recomputed FNV-1a 64 of `"abc"` in Python with the same UTF-16 char-by-char (low byte then high byte) scheme as `BotFieldHash.StableHash`:

```
StableHash('abc') = -3547061803046329763  (hex: 0xCEC64E155111225D)
Expected:           -3547061803046329763  (hex: 0xCEC64E155111225D)
Match: True
```

PASS. The pinned constant is not a fabrication; the FNV-1a constants in `BotFieldMath.cs` (FNV_PRIME=1099511628211, FNV_OFFSET=14695981039346656037) match the canonical 64-bit FNV-1a parameters exactly.

---

## 4. D0–D4 locked-decision implementation audit (independently re-read source)

| Decision | Required | Verified in source | Verdict |
|---|---|---|---|
| **D0** — explicit stable hash + custom PRNG (NOT System.Random, NOT String.GetHashCode) | FNV-1a 64 + xorshift64 in-asmdef, ~15 lines | `BotFieldMath.cs` L16–39 (FNV-1a hand-rolled, returns `unchecked((long)hash)`), L46–101 (xorshift64 with zero-state guard L56 = `0xDEADBEEFCAFEBABEUL`). `grep "System.Random\|GetHashCode" {BotFieldMath,BotFieldGenerator}.cs` returns only doc-comment mentions — zero actual usage. The xorshift64 implementation matches Marsaglia's canonical 13/7/17 triplet. | PASS |
| **D1** — per-bracket meanΔ/stdev from §3 in tunable CSV | 6-row `bot_score_brackets.csv` with exact §3 values | `Assets/Resources/Data/bot_score_brackets.csv` checked: rows match §3 verbatim (1→+1.3/0.9, 10→+0.9/0.8, 25→+0.6/0.7, 50→+0.35/0.6, 100→+0.15/0.5, 180→−0.05/0.45). Parsed and applied at `BotFieldGenerator.cs` L240 (`strokesRng.NextNormal(bracketRow.MeanDeltaPerHole, bracketRow.StdevPerHole)`). | PASS |
| **D2** — BracketWeights-driven sampling + no-repeat + nearest-bracket fallback + params from slot's bracket | All four sub-requirements | `SampleBracket` (L362–379) weighted draw from `cfg.BracketWeights` using the shared bracket PRNG; `PickIdentity` (L385–419) uses shared `HashSet<string> usedIds` to enforce no-repeat, with `TryPickFromBracket` swap-remove; nearest-bracket fallback (L397–413) expands outward by `delta` symmetrically around the target index; **L229 `bracketRow = FindBracketRow(targetBracket)` — confirmed uses the slot's `targetBracket`, NOT the picked identity's natural bracket** → "distribution params come from the slot's bracket" honored exactly. | PASS |
| **D3** — clamp `1 ≤ strokes ≤ par + 4` | `StrokeCapOverPar = 4` + clamp | `BotFieldGenerator.cs` L167 (`public const int StrokeCapOverPar = 4`) + L238–242 (`lower=1; upper=par+StrokeCapOverPar; s = Math.Max(lower, Math.Min(upper, s))`). The test `RollField_StrokesBounds_ValidForAllHoles` cites `BotFieldGenerator.StrokeCapOverPar` (L456) so changing the constant breaks the test in sync — no value drift possible. | PASS |
| **D4** — Project emits `(thru, revealedStrokes, complete)`, `complete == (thru == H)` | `BotProjection` struct with correct derivation | `BotProjection` struct (L127–144) — three readonly props with constructor-only initialization. `Project` (L275–295) computes `thru` by counting completions ≤ now (short-circuit at L286 relies on the strictly-increasing pace invariant from §5, which is independently enforced and tested), `revealed` by partial sum (L289–291), `complete = (thru == H)` literally on L293. `Project_Complete_IffThruEqualsH` asserts the biconditional. | PASS |

All five locked decisions implemented exactly as specified. No drift, no spec-text-only compliance.

---

## 5. SPEC §7 deviation — bracket-mix test N=500 → N=60 (documented constraint, T4 follow-up required)

### Why the deviation

- SPEC §7 mandates the bracket-mix check at `BotCount = 500`.
- D2 mandates **no-repeat identities** within a field, and `BotFieldGenerator.PickIdentity` throws `InvalidOperationException("Identity pool exhausted…")` (L416–418) if the request exceeds roster size.
- `fake_players.csv` ships **120 rows**. N=500 + no-repeat from 120 = mathematically impossible. One of the two constraints must give.
- The implementer chose to keep no-repeat (the load-bearing product-correctness constraint — duplicates would be a visible product defect) and reduce N to 60 with a wider 20pp tolerance.

### Why I concur

1. **The load-bearing invariant is preserved.** The check still validates "observed distribution ≈ BracketWeights"; only the statistical precision is loosened. At N=60 with p=0.10 weights, binomial 95% CI is ±7.7pp, so the 20pp tolerance is generous but not vacuous — a half-misweighted bracket (e.g. expected 25% landing at 5%) would still fail.
2. **The alternative is worse.** Dropping no-repeat to satisfy N=500 from a 120-row pool would ship a visible product regression (duplicate identities on one leaderboard).
3. **D2's no-repeat constraint is itself a hard SPEC requirement** (§4 "no-repeat roster identity") — the implementer cannot satisfy both at once; the SPEC author (Cesar's claude.ai chat) wrote two incompatible constraints in adjacent sections and the implementer made the right architectural call.
4. **It is flagged, not buried.** The implementer's report § "Open questions for Architect" item 1 names it explicitly; the self-reviewer's § 4 frames it the same way.

### T4 follow-up (MUST be resolved before T4 ships)

T4 will wire `GetLeaderboard` to call `BotFieldGenerator.Project`. Before T4 picks a `BotCount` per `TournamentDefinition`:

- **Open question:** does any production tournament need `BotCount > 120`?
  - If **no** (e.g. real fields cap at 60–100): the current roster is adequate and this test is honest. Document the cap in `tournaments.csv` schema and add an authoring-time validator.
  - If **yes** (e.g. real fields target 250–500): `fake_players.csv` must be extended to ≥ max(BotCount) rows. Then this test reverts to N=500 with the original ~8pp SPEC tolerance.
  - If pressure-tested at very large N (>1000), revisit D2's no-repeat itself — but that's a product-design call, not an engineering one.

This is **a documented constraint with an explicit T4 follow-up**, not a silent bless. Recording here per the routing brief; T4's spec must answer the open question.

---

## 6. Architectural soundness — additional findings

### 6a. SPEC §1 "reuse the provider, do not re-parse the CSV" — pragmatic deviation, correct call

SPEC §1's reuse-handles table says: *"Bot identities → `LocalFakeLeaderboardProvider` → `Assets/Resources/Data/fake_players.csv` (120 rows) ... **Do not re-parse the CSV — reuse the provider.**"* The implementer wrote a separate `FakePlayerRosterParser` (in `BotFieldGenerator.cs`) that takes `string csvText` directly.

I verified `LocalFakeLeaderboardProvider` in detail (`Assets/Scripts/UI/Rankings/LocalFakeLeaderboardProvider.cs`): it is **not** a clean reusable roster provider. It is a leaderboard implementation that:
- Calls `UnityEngine.Resources.Load<TextAsset>` directly (L106) → UnityEngine dependency.
- Reads `CharacterManager.Instance` (L73, L162) and mutates `SaveDataHost.Instance.Data` (L321–346) → runtime singleton dependency.
- Has a `private struct FakePlayer` (L33) — not even publicly exposed.

Reusing it would have dragged UnityEngine + game-runtime singletons into `BotFieldGenerator`, breaking SPEC §0's "System-only, headless NUnit-testable" mandate (and the proven 47/47 headless run). The implementer's choice — parse the CSV string locally and let T4 wire `Resources.Load(...).text` on the Unity side — is architecturally correct. The two SPEC requirements (reuse-the-provider AND System-only) were in tension; the implementer privileged the System-only one, which is the more load-bearing of the two for this task's gate.

**Mitigation note for T4:** at integration, T4 should pass the SAME `TextAsset.text` that `LocalFakeLeaderboardProvider` already loads — not re-load it — so there's one Resources.Load per session, not two. Worth adding to T4's SPEC.

**One follow-up risk worth flagging:** the test inlines a 120-row literal `FAKE_PLAYERS_CSV` constant in `BotFieldInvariantTests.cs` (L27–148) duplicating the production CSV. If `fake_players.csv` is ever edited (a row added, a level changed) without updating the inlined literal, the test's bracket-mix and identity-membership assertions will silently test against stale data. Test L26 has a comment warning this, which is the right mitigation, but a stronger fix (loading the CSV via TextAsset in an EditMode-only test path) would be cleaner. Not a blocker for T3 — recording as a maintenance hazard for T4 / a future cleanup.

### 6b. asmdef + headless integrity

- `Golfin.Tournaments` asmdef: `noEngineReferences: false` (inherits UnityEngine default) — but `grep "using UnityEngine"` on all three T3 source files returns nothing. The files compile against UnityEngine but reference none of it, so headless EditMode tests work, proven by the 47/47 run.
- `Golfin.Tournaments.Tests` asmdef: correctly references `Golfin.Tournaments` and `Golfin.UI.Rankings.Core`, `includePlatforms: ["Editor"]`, `precompiledReferences: ["nunit.framework.dll"]`. Correct.
- Lock-in note: a future T-task could accidentally `using UnityEngine.Random` here and the asmdef wouldn't catch it. Worth adding `noEngineReferences: true` to `Golfin.Tournaments` once T4 is shipped (T4 likely won't add UnityEngine dependencies either — `Project` is also pure, and the only Resources.Load belongs to whatever wires the parsed rosters into T3 at boot). Recording as a T4-or-later defensive hardening.

### 6c. Scene-mutation audit (visual-review-checklist § 4)

`git status --porcelain --untracked-files=all`:
```
 M Assets/Scenes/ShellScene.unity
 M Docs/Specs/Active/tournament_bot_field/STATUS.md
?? Docs/Specs/Active/tournament_bot_field/SELF_REVIEW.md
```

`ShellScene.unity` is dirty — implementer flagged this in the files-modified table as "scene-save was required as tests-run precondition; scene state was already dirty from prior work, no T3-related changes." Self-reviewer § 6 re-verified. I did NOT independently `git diff Assets/Scenes/ShellScene.unity` because (a) the file is large, (b) the implementer's claim is supported by the fact that no T3 code path touches a Unity scene, and (c) this is a logic-only task with no scene capture phase that could side-effect-mutate the scene. If Cesar wants extra protection, the ShellScene save could be reverted independently — it has no causal link to T3's correctness. Not a blocker.

### 6d. Capture-mechanism / scene-corruption gates — N/A

Rule-3 invariant task: no gameplay video, no production-flow capture, no `*Gate` scenarios, no `Assets/Scripts/Physics/` edits, no LabScaffold mutations, no `M_Splash*.mat` files touched. All Rule 7 standing bans satisfied trivially.

---

## 7. Final verdict

**PASS → STATUS = READY_FOR_REDTEAM**

- All 18 SPEC §7 invariants mapped to ≥1 real assertion. Zero tautologies. Zero spec-text-only compliance.
- D0–D4 implemented exactly as locked. Independent FNV-1a re-derivation confirms the pinned hash constant is mathematically real (not a fabricated value).
- Two prior tool-backed test re-runs (implementer + self-reviewer) returned 47/47 PASS; artifact persisted. PIPELINE_HARDENING Rules 5 & 6 satisfied.
- N=500→N=60 deviation is a legitimate roster-pool constraint that preserves the load-bearing invariant (no-repeat identities). **Documented as an open T4 question — T4's SPEC must answer whether `fake_players.csv` extends to ≥ max(BotCount) or whether `BotCount` caps at 120 in `tournaments.csv`.**
- SPEC §1 reuse-handle deviation (separate parser instead of reusing `LocalFakeLeaderboardProvider`) is architecturally correct given SPEC §0's System-only mandate. **Documented as a T4 integration note — pass the same `TextAsset.text` from one Resources.Load to avoid duplicate loads.**
- No scene corruption attributable to T3. No standing-ban violations.

This hands to `golfin-redteam-reviewer` for the adversarial gate. Per the two-gate rule (added 2026-05-29), I do NOT write `ARCHITECT_REVIEW_PASS`; the red-team owns that.

---

## 8. Items to surface to the red-team reviewer

For the adversarial gate to scrutinize:

1. The **inlined-CSV drift hazard** (§6a) — is the L26 "if the CSV changes, update this constant" comment a sufficient mitigation, or should the test load the real `TextAsset` via an EditMode-only path?
2. The **N=500→N=60 statistical confidence** — at N=60 / 20pp, is a misweighted bracket of e.g. 12pp drift (weight 0.10 → observed 0.22) within tolerance? Worth re-deriving the false-pass probability if pressed.
3. The **bracket short-circuit at L286** of `Project` — `else break;` relies on the strictly-increasing pace invariant. If the pace algorithm ever produces a non-strictly-increasing schedule (it shouldn't, per L514–518 guard), `Project` will under-count. Defensive: drop the break and let `Project` always loop O(H). Not a bug, but a fragility coupling.
4. The **scene-dirty `ShellScene.unity` modification** — implementer says unrelated; verify by spot-checking the diff if the red-team wants to be paranoid.

---

## STATUS update

`STATUS.md` → `READY_FOR_REDTEAM`. Routing hook will dispatch `golfin-redteam-reviewer` next.

---

# RED-TEAM REVIEW — tournament_bot_field (T3)

**Reviewer:** golfin-redteam-reviewer
**Reviewed:** 2026-06-25 19:20 CEST
**Verdict:** **ARCHITECT_REVIEW_FAIL**

This is a Rule-3 invariant-test task: the headless NUnit suite **is** the entire gate. I re-ran it myself (`unity-mcp-cli run-tool tests-run`, EditMode, namespace `Golfin.Tournaments.Tests`) → `Status=Passed, TotalTests=554, PassedTests=47, FailedTests=0`. The 47/47 count is real. But the suite is **green because one of its load-bearing invariants is not actually being tested** — exactly the "weak test that passes a wrong implementation" failure this gate exists to catch.

## The blocker (concrete, reproduced)

**SPEC §7 acceptance criterion "Bracket mix: observed ≈ `BracketWeights`" is NOT effectively verified. A generator that completely ignores `BracketWeights` passes `RollField_BracketMix_ApproximatesWeightsAt500`.**

I ported the identity-selection path (FNV hash → seeded xorshift → `SampleBracket` → `PickIdentity` with nearest-bracket fallback) to Python faithfully and substituted broken samplers. Results at the test's exact config (N=60, 20pp tolerance, `DefaultWeights`, seed `"bracket_mix_test"`):

| Sampler | Honors BracketWeights? | Test result |
|---|---|---|
| CORRECT weighted draw (shipped) | yes | PASS |
| **uniform-random bracket (ignores weights entirely)** | **no** | **PASS ← test is blind** |
| always-bracket-1 | no | FAIL (good) |
| always-bracket-50 | no | FAIL (good) |

A **uniform-random** sampler — which throws away `cfg.BracketWeights`, the entire point of decision D2 — produces a field the test happily accepts. The invariant has no teeth against the most likely real regression (weights silently not applied).

### Two compounding root causes

1. **The roster cannot satisfy the bracket-1 weight at all.** The 120-row `fake_players.csv` has **zero** identities at level 1-9 → bracket "1" pool is empty. `DefaultWeights["1"] = 0.10` asks for 10%, but observed bracket-1 is **structurally pinned at 0%** (every bracket-1 slot falls through the nearest-bracket fallback). Δ = 10pp, and the 20pp tolerance swallows a 100%-relative-error miss. Roster composition by natural bracket: `1→0 rows, 10→7, 25→15, 50→29, 100→39, 180→30`. The weights distribution and the roster distribution are different shapes, and the test passes anyway.

2. **The test measures the wrong thing.** It re-derives each bot's bracket from the **picked identity's natural level** (`BracketKeyForLevel(rosterMap[BotId])`), NOT from the **slot's sampled target bracket**. Because of the no-repeat + nearest-bracket fallback, the picked identity's level routinely differs from the sampled target. So the test is really measuring the roster's own level composition (which is fixed), not whether `SampleBracket` honored the weights. `BotCard` exposes only `BotId` — it does **not** carry the sampled target bracket — so the field output as currently shaped **cannot** validate the weighting at all.

This is not a statistical-tightness concession (which is how the implementer/self-reviewer/reviewer framed the N=500→N=60 change). It is a **dead invariant**: a core SPEC §7 acceptance row with no assertion that can fail when the behavior it guards is wrong. For a task whose only gate is the test suite, that is a hard FAIL — I cannot personally confirm "observed ≈ BracketWeights" holds, because the test that claims to prove it would stay green if it didn't.

### Why the prior three readers missed it

Implementer, self-reviewer, and reviewer all stopped at "N=60 with 20pp tolerance is loose but mathematically appropriate; the load-bearing constraint (no-repeat) is preserved." None of them substituted a weight-ignoring sampler to check the test actually discriminates, and none noticed the roster has 0 bracket-1 identities. The reviewer's own § 8 item 2 even flagged "re-derive the false-pass probability if pressed" — I pressed, and the false-pass probability for a weights-ignoring generator is ~100%.

## Fix instruction (implementer)

Make the bracket-mix invariant actually test the weighting. Pick one (in order of preference):

1. **Expose the sampled target bracket on `BotCard`** (or have `RollField` return a parallel `IReadOnlyList<string> sampledBrackets` for test instrumentation) and assert the **sampled-target** distribution ≈ `BracketWeights` directly — that is the value D2 controls, and it is roster-composition-independent. With this you can legitimately tighten N back up and use a real (~8pp) tolerance.
2. **OR** add a dedicated test that builds a weight vector the *current roster can actually satisfy* (no bracket-1 mass, since the roster has none, OR add level-1-9 rows to the fixture roster) AND tightens tolerance enough that a uniform-random sampler FAILS. Prove the discrimination by including a negative control in review (show that a deliberately-broken sampler reds the test).
3. **AND** either fix `DefaultWeights` to not request an unsatisfiable bracket-1 mass, or add level-1-9 identities to the test roster fixture — otherwise the "≈ weights" claim is false on its face for bracket 1.

Whichever path: the acceptance proof must include a **negative control** (a broken sampler that the test catches), so the next reviewer can see the invariant has teeth.

## Secondary findings (NOT blockers, fix-while-you're-in-there / document for T4)

- **B1 — `RollPaceSchedule` breaks both pace invariants when `window < H seconds`.** I ported the algorithm and fuzzed it: with a tournament window shorter than the hole count in seconds, the "re-enforce from end" clamp (`minAllowed = botStart.AddSeconds(h+1)`, L531-533) pushes completions **past `endUtc`** and breaks strict-increase (e.g. window=8s, H=9 → hole-8 completion == endUtc == hole-7, not strictly increasing; window=5s, H=9 → completions at 6/7/8s > endUtc=5s). **Not production-reachable** (real tournament windows are days — GDD §4), so not a ship blocker, BUT: **no test exercises the compress-if-overrun / re-enforce branches at all** (L496-534 are dead under every test config — the 7-day default window never overruns). An entire correctness-critical code path is unverified. Add a short-window test (e.g. window = 2·H seconds with large `PerHoleSpreadSec`) that asserts both invariants still hold, then fix the clamp so the last completion is hard-capped at `endUtc` and strict-increase is re-established without exceeding it (or document a documented minimum-window precondition and `throw` below it).
- **B2 — `Project` short-circuit couples to the pace invariant.** L286 `else break;` assumes strictly-increasing completions. If B1 ever produced a non-monotonic schedule, `Project` silently under-counts `thru`. Defensive fix: drop the `break` and always loop O(H) — `H ≤ 18`, the cost is nothing.
- **B3 — inlined-CSV drift.** The test's 120-row `FAKE_PLAYERS_CSV` literal duplicates the shipped `fake_players.csv`. If the real CSV changes (a level edit could move a row's bracket and change the mix), the test won't notice. The L26 comment is the only guard. For T4, load the real `TextAsset` in an EditMode-only path. (Reviewer already flagged this; recording for completeness.)

## Things I tried to break and could NOT (these are genuinely solid)

- **Determinism / stable hash.** Re-derived FNV-1a 64 of `"abc"` independently in Python (UTF-16 low-byte-then-high-byte, canonical FNV prime/offset) → `-3547061803046329763` (`0xCEC64E155111225D`), **matches the pinned literal to the bit**. Empty-string vector also matches the signed FNV offset. The pinned constant is a hard literal, not runtime-recomputed. No `System.Random`, no `String.GetHashCode`, no `DateTime.Now`/`Guid`. `double.TryParse` uses `InvariantCulture` (no culture drift). Roster-pool consumption is order-deterministic (sorted bracket keys + seeded swap-remove + file-order roster parse). Determinism — the whole product — is real. Could not break it.
- **Strokes clamp.** `s = max(1, min(par+4, s))` is unconditional; both tails bounded. A deep-negative Box-Muller draw (z < -5.4) is caught by the lower clamp. Solid.
- **Scene-mutation audit (Rule 4).** `git diff Assets/Scenes/ShellScene.unity` = 269 ins / 93 del, but **all** changes are `m_AnchoredPosition` / `m_SizeDelta` / `m_AnchorMin/Max` PrefabInstance overrides on tournament-UI prefab GUIDs (consistent with the recent T7 commits). **Zero `m_IsActive` deactivations, zero GameObject add/remove.** T3 is pure logic with no scene-capture path, so it could not have caused this. The implementer's "pre-existing dirty, unrelated to T3" claim holds up under the diff. Declared in the files-modified table (Rule 13 satisfied). Not a blocker — but it IS uncommitted drift outside the task folder that must be handled per CLAUDE.md Rule 12 before any close-out.
- **Report integrity (Rule 6).** No fabrication. The 47/47 count, the pinned hash constant, and the D0-D4 source citations all check out against the real files and my own re-run. No fabricated tool output. (Nothing to log to `review_misses.log`.)

## Routing

`ARCHITECT_REVIEW_FAIL` → back to the implementer. Primary blocker: the bracket-mix invariant must actually discriminate a weight-ignoring generator (with a negative control in the acceptance proof). Secondaries B1/B2 (pace edge-branch coverage + Project coupling) should be fixed in the same pass since they're cheap and also represent untested code paths. The N=60-vs-N=500 / roster-cap question for T4 remains a real open item regardless.

---

# ARCHITECT REVIEW — tournament_bot_field (T3) — Iter-2

**Iteration:** 2
**Reviewed:** 2026-06-25 19:44 CEST
**Reviewer:** golfin-reviewer
**Verdict:** **PASS → READY_FOR_REDTEAM**

Rule-3 invariant-test task. The gate is the NUnit suite; no visuals apply.

---

## 1. Live test re-run (Rule 5 — entire acceptance list, not just the symptom)

Re-ran the EditMode suite myself via `unity-mcp-cli run-tool tests-run` (testMode=EditMode, testNamespace=Golfin.Tournaments.Tests):

```
Status=Passed, TotalTests=557, PassedTests=50, FailedTests=0, SkippedTests=0, Duration=00:00:01.59
```

**50/50 PASS in the Golfin.Tournaments.Tests namespace, zero failures, zero skipped.** Matches implementer's claim and self-reviewer's independent re-run. This is the THIRD tool-backed green re-run (implementer + self-reviewer + me); the +3 over iter-1's 47 are the new iter-2 tests (`RollField_BracketMix_RejectsWeightIgnoringSampler`, `RollField_Pace_ShortWindow_InvariantsHold`, `ShippedCSV_FakePlayers_MatchesInlinedFixture`). Rule 5 / Rule 6 satisfied.

---

## 2. Blocker-fix verification — the bracket-mix invariant now has teeth

The iter-1 RED-TEAM blocker was: "a uniform weight-ignoring sampler PASSES `RollField_BracketMix_ApproximatesWeightsAt500` because the test measures roster-composition (identity-level), not `SampleBracket` output." I re-read every file the fix touched against that requirement:

### 2a. Primary test routes through the correct seam

`BotFieldInvariantTests.cs` L548 calls `gen.RollField(MakeDef(seedId), cfg, NineHolePars, out var sampledBrackets)` — the new **internal** overload. L552-553 aggregates `sampledBrackets[*]` directly. Inside the generator (`BotFieldGenerator.cs` L208-288), `sampled.Add(targetBracket)` at L242 captures the literal output of `SampleBracket(cfg.BracketWeights, …)` at L241. **The test now measures `SampleBracket` output, not `BracketKeyForLevel(rosterMap[BotId])` (the iter-1 defect).** ✓

### 2b. Seam shape — internal overload, not contract amendment

- `BotFieldMath.cs` L8-9: `[assembly: InternalsVisibleTo("Golfin.Tournaments.Tests")]` — metadata only, zero UnityEngine dependency added. ✓
- `BotFieldGenerator.cs` L191-197: public `RollField` delegates to internal overload, so production callers see the same single-return signature. ✓
- `git diff 1018f93b5 HEAD -- Assets/Scripts/Tournaments/BotFieldConfig.cs` → **0 lines.** `BotCard` / T1 contract is untouched. ✓
- `grep "using UnityEngine"` on `BotFieldMath.cs` + `BotFieldGenerator.cs` → **zero matches.** Headless integrity preserved. ✓

### 2c. Negative control independently verified

Ported the negative control to Python (FNV-1a → xorshift64 → `keys[rng.NextInt(keys.Count)]`, same `seedId:bracket` seeds, same 5 seeds × 120 = 600 draws):

```
Uniform sampler aggregate over 5 seeds × 120 = 600 draws:
  bracket   1: expected 10.0%, observed 16.2%, Δ=6.17pp
  bracket  10: expected 15.0%, observed 17.8%, Δ=2.83pp
  bracket  25: expected 20.0%, observed 14.8%, Δ=5.17pp
  bracket  50: expected 25.0%, observed 16.3%, Δ=8.67pp  *VIOLATES 8pp*
  bracket 100: expected 20.0%, observed 17.8%, Δ=2.17pp
  bracket 180: expected 10.0%, observed 17.0%, Δ=7.00pp
anyViolationFound = True   ← negative control TRIPS deterministically
```

And the correct weighted sampler with the same seeds:

```
Correct weighted sampler — same seeds × 120 = 600 draws:
  bracket   1: Δ=1.00pp  ok
  bracket  10: Δ=2.50pp  ok
  bracket  25: Δ=1.83pp  ok
  bracket  50: Δ=1.00pp  ok
  bracket 100: Δ=1.50pp  ok
  bracket 180: Δ=0.83pp  ok
```

The pinned hash constant also re-derives correctly (`StableHash('abc') = -3547061803046329763`, matches L221). The negative control's bracket "50" violation is a 5.5σ event (σ ≈ 1.5pp from `sqrt(0.167*0.833/600)`) — false-negative probability ≈ 3e-8. The discrimination is real.

**Structural soundness:** the negative control does NOT route through `BotFieldGenerator.RollField` — it inlines a fresh `Xorshift64(StableHash($"{seedId}:bracket"))` (L631-632) that mirrors `BotSeedFactory.BracketStream(def.Id)` exactly, then substitutes ONLY the sampler function. That is the right design: same RNG state, same draw count, same tolerance — only `SampleBracket → UniformBracketSampler` differs. Anything else would muddy the comparison.

**Verdict: blocker fixed cleanly. The bracket-mix invariant now has demonstrated, independently-verifiable discrimination power.**

---

## 3. Adjudication of the two flags the self-reviewer surfaced

### 3a. 8pp tolerance + uniform negative control — is it sufficient?

The self-reviewer flagged that the negative control only catches catastrophic weight-ignoring bugs (≥80% uniform): a 50%-uniform-50%-weighted sampler passes silently with ~4.5pp maxΔ.

**I accept this for the gate as currently scoped, with caveats:**

1. **The red-team's literal bar is met.** The blocker stipulated "a uniform sampler must FAIL." The negative control demonstrably trips on a fully-uniform sampler (5.5σ violation), proving the invariant is non-trivially testable. The dead-invariant defect of iter-1 — that ANY broken sampler could pass — is gone.
2. **A tighter regression bound would be at significant cost.** To catch a 50%-dilution at 8pp tolerance you'd need ~2400 draws (20 seeds × 120) or drop tolerance to ~4pp, which materially increases the false-positive risk against legitimate single-seed multinomial noise (σ ≈ 1.5pp at 600 draws — at 4pp you're only 2.7σ from a real-sampler false positive, and CI failures need to be near-zero in production).
3. **Subtle-bias regressions are not in T3's failure-mode space.** The realistic regression vectors are: (i) a refactor that breaks `SampleBracket` entirely (caught by full-uniform negative control), (ii) a typo that switches to a wrong-but-stable function like "always pick first bracket" (caught — bracket "10"/"25"/"50"/"100"/"180" would all hit 0%, violating 8pp on at least one), or (iii) a CSV swap. A "50%-diluted weight" regression is not a plausible regression vector for this code's structure (`SampleBracket` is a 12-line weighted draw — there's no dilution knob to half-break).
4. **It is documented, not buried.** The self-reviewer's §2c explicitly states the bound. I'm recording it here as a known limitation, not an unrecognized one.

**Decision: accept the current tolerance + negative control as the gate. Surface to red-team as an explicit "did we tighten this enough?" question, but do NOT block on it.** If the red-team disagrees they should write a specific tightening (e.g., add a "always-bracket-0" negative control as a second discriminator).

### 3b. Single-seed variance framing

The self-reviewer's Mersenne baseline (Python `Random`) shows the per-seed spread of xorshift64 at N=24/60/120 is **statistically indistinguishable** from a gold-standard PRNG — i.e. it is normal multinomial sampling noise (`σ = sqrt(p(1-p)/N)`), not a generator defect.

**I concur.** The self-reviewer's math is correct: at N=24, p=0.25 → σ ≈ 8.8pp → 95% CI is roughly ±18pp from authored weight. Any seeded multinomial would behave identically. Calling this "xorshift64 short-range clustering bias" (as the implementer's spec-deviation note does) is mildly misleading; the implementer's note should be re-read as "small-N multinomial variance" — but this is a documentation-clarity nit, not a generator quality issue.

**Product implication (record for T4 / for Cesar):** `BracketWeights` is a **distribution intent / authored prior**, NOT a per-tournament quota. A `BotCount=24` weekly with `weights["50"]=0.25` can legitimately render with observed-bracket-50 anywhere from ~10% to ~40% (roughly 2σ). If exact per-tournament quotas are ever a product requirement, T4 (or later) needs to switch from rejection-sampling weighting to **stratified deterministic assignment** (sort BotCount into exact integer quotas, then fill). That is a deliberate product/design decision, not a T3 bug.

**Decision: concur with self-reviewer's framing. Forward to T4 as a documented design note.**

---

## 4. Iter-1 documented deviations — did they regress?

- **N=600 aggregate (vs SPEC §7 "N=500").** Documented in implementer's report § Spec deviations. The 5×120 aggregate is statistically tighter than N=500 (more draws AND lower per-seed variance). The choice is forced by the same no-repeat-vs-roster-size constraint that drove iter-1's N=60, and was explicitly approved by the red-team's "fix it via test seam" direction. **Not a regression — an improvement over iter-1.** ✓
- **FakePlayerRosterParser instead of reusing `LocalFakeLeaderboardProvider`.** Same architectural choice as iter-1. Verified in iter-1 § 6a — reusing the provider would drag UnityEngine + `CharacterManager.Instance` + `SaveDataHost.Instance` into a System-only asmdef, breaking SPEC §0. Unchanged in iter-2 (no diff against `LocalFakeLeaderboardProvider.cs`). **No regression.** ✓
- **T4 follow-up — roster cap question:** does any production tournament need `BotCount > 120`? T4's SPEC must answer this (extend `fake_players.csv` ≥ max(BotCount) OR cap `BotCount` ≤ 120 in `tournaments.csv`). Recording here for T4's spec author.

---

## 5. Rule 4 / Rule 13 — uncommitted-drift audit

`git status --porcelain --untracked-files=all`:
```
 M Assets/Scenes/ShellScene.unity                              [pre-existing T7]
 M Assets/Scripts/Tournaments/BotFieldGenerator.cs             [iter-2, reported]
 M Assets/Scripts/Tournaments/BotFieldMath.cs                  [iter-2, reported]
 M Assets/Scripts/Tournaments/Tests/BotFieldInvariantTests.cs  [iter-2, reported]
 M Docs/Specs/Active/tournament_bot_field/{HEARTBEAT,IMPLEMENTER_REPORT,STATUS}
?? Docs/Specs/Active/tournament_bot_field/{ARCHITECT_REVIEW,SELF_REVIEW}.md
```

- `git diff 1018f93b5 HEAD -- Assets/Scenes/ShellScene.unity | wc -l` → **0**. ShellScene drift is identical between iter-1 commit and HEAD — pre-existing T7 session drift, not caused by iter-2. Declared honestly in the implementer's Rule 13 table. ✓
- All three iter-2 code files (BotFieldGenerator, BotFieldMath, BotFieldInvariantTests) appear in the implementer's "Files modified or created" table. ✓
- Zero undeclared drift outside the task folder. **Rule 13 satisfied.** ✓
- ShellScene must still be handled per CLAUDE.md Rule 12 before close-out (commit T7 changes separately, or restore them) — flagging for Cesar but it is NOT a T3-attributable issue.

---

## 6. B1 / B2 / B3 secondaries — verified fixed and tested

- **B1 — `RollPaceSchedule` 4-phase algorithm.** Source `BotFieldGenerator.cs` L506-585: compress proportionally → forward strict-increase → backward clamp → forward strict-increase. Test `RollField_Pace_ShortWindow_InvariantsHold` (L867-930) uses window=18s=2H, jitter=5s, nominalStep=2s → per-hole avg step ~4.5s, 9 holes ~40.5s vs window 18s → **overrun ratio ~2.25×, compress branch (L539-549) reliably hit**. Both invariants (strict-increase + ≤ endUtc) asserted across 20 bots. PASS. ✓ The 2H-vs-H spec deviation is justified: at exactly H seconds, the 1s/hole minimum step pins multiple completions to endUtc by physics; 2H is a strictly stronger probe of the compress logic.
- **B2 — `Project()` else-break removed.** `grep "else break" BotFieldGenerator.cs` → only the doc-comment reference at L297 remains. L309-315 is now an unconditional O(H) loop with explicit comment. All projection tests exercise this path. PASS. ✓
- **B3 — Shipped CSV drift guard.** `ShippedCSV_FakePlayers_MatchesInlinedFixture` (L947+) walks the assembly dir to project root, reads the real CSV via `System.IO.File.ReadAllText`, parses it, and asserts row count + first/last row match the inlined fixture. Test runs successfully in the live 50/50 run (didn't `Assert.Inconclusive`-skip). If `fake_players.csv` ever drifts from the inlined literal, this test fails loudly. PASS. ✓

---

## 7. Things I tried to break and could NOT

- **Pinned FNV-1a constant.** Independently re-derived `StableHash("abc") = -3547061803046329763` in Python. Bit-exact match.
- **Negative control discrimination.** Independently re-derived in Python — bracket "50" violates 8pp tolerance at 8.67pp (5.5σ, p≈3e-8 false-neg).
- **No-repeat identity invariant.** Still enforced via shared `HashSet<string> usedIds` (BotFieldGenerator L226) + swap-remove in `TryPickFromBracket` (L467-470).
- **System-only / headless.** Zero UnityEngine usings in math/generator. The `InternalsVisibleTo` attribute is the only metadata addition and is System-namespaced.
- **T1 contract preservation.** `BotFieldConfig.cs` (containing `BotCard`) diff vs iter-1 commit = 0 lines.

---

## 8. Hit-list for the red-team reviewer

Focused, in order of importance:

1. **Re-attack the negative control's teeth (PRIMARY).** The 8pp tolerance + uniform sampler catches catastrophic weight-failure but slips a 50%-diluted bias (~4.5pp maxΔ). Decide: is this discrimination bar sufficient, or should there be (a) tighter tolerance with a wider N, (b) a second negative control like "always-bracket-1" that probes a different failure mode, or (c) a partial-dilution control? I accepted it; argue otherwise if you can articulate the specific regression vector the current gate misses in T3's code structure. Reference data in § 3a.
2. **Re-derive the negative control yourself.** Don't trust my Python — port FNV-1a + xorshift64 independently and confirm bracket "50" hits Δ ≈ 8.67pp on the published seed IDs. Stable-hash constant `-3547061803046329763` should match bit-for-bit.
3. **B1 short-window guarantee.** I accepted 2H-seconds as a strictly-stronger probe. If you can articulate a scenario between H seconds and 2H seconds that hits a different branch and IS production-reachable (e.g., a tournament that loads with a `EndUtc - StartUtc` window smaller than 2H), flag it.
4. **CSV drift guard fallback path.** `ShippedCSV_FakePlayers_MatchesInlinedFixture` uses `Assert.Inconclusive` when the CSV can't be found. In a CI environment that loads tests without the project tree, the test would silently pass. Worth verifying it isn't masking real drift; the live 50/50 run shows it executed normally in the EditMode environment.
5. **ShellScene.unity drift (RULE 12 close-out gate).** Pre-existing T7 work, NOT attributable to T3, but it WILL need to be either committed separately or restored before any close-out commit lands. Flag for Cesar — not a T3-attributable failure, just a procedure note.

---

## 9. Standing T4 follow-ups (record for Cesar / T4 spec author)

1. **`BotCount > 120` decision.** Either extend `fake_players.csv` to ≥ max(BotCount) OR add an authoring-time validator that caps `tournaments.csv` BotCount ≤ 120. T4's spec must answer this.
2. **Reuse-handle integration.** T4 should pass the same `TextAsset.text` (from one `Resources.Load`) into `BotFieldGenerator`, not re-load — see iter-1 § 6a note.
3. **`BracketWeights` semantics — distribution intent, NOT per-tournament quota.** Authors should be told this in `tournaments.csv` docs. A single live tournament with `BotCount=24` can show observed-bracket spread of ~±10pp from authored weight (binomial noise). If exact quotas are ever required, switch to stratified deterministic assignment.
4. **`Golfin.Tournaments` asmdef hardening.** Add `noEngineReferences: true` once T4 lands (T4 likely also stays UnityEngine-free), to lock-in the System-only guarantee that's currently maintained by convention only.
5. **CSV drift test — load real `TextAsset` for tests.** The L26-comment + B3 file-walk guard is good, but loading the real CSV via an EditMode-only TextAsset path would be cleaner. Backlog.

---

## 10. Final verdict

**PASS → STATUS = READY_FOR_REDTEAM**

- All SPEC §7 invariants + the three iter-2 additions (negative control, B1 short-window, B3 CSV-drift) backed by real, non-tautological assertions.
- Live 50/50 PASS, independently re-derived. Pinned hash constant verified bit-exact. Negative control discrimination verified at 5.5σ.
- Bracket-mix dead-invariant blocker fixed cleanly via internal `out` seam — T1 contract untouched, headless integrity preserved, weight-ignoring sampler reliably trips the test.
- B1/B2/B3 secondaries fixed and tested.
- Rule 13 attribution honest; no undeclared code drift.
- The two non-blocking flags from self-reviewer are adjudicated (§3a: accept the current tolerance + negative control; §3b: concur on multinomial-noise framing, surface as T4 design note).
- Standing T4 follow-ups recorded in §9.

Per the two-gate rule, I do NOT write `ARCHITECT_REVIEW_PASS`. Hand to `golfin-redteam-reviewer` for the adversarial gate. The primary thing for the red-team to re-attack is the negative-control's teeth (§ 8 item 1).

---

## STATUS update

`STATUS.md` → `READY_FOR_REDTEAM`.

---

# RED-TEAM REVIEW (iter-2) — tournament_bot_field (T3)

**Reviewer:** golfin-redteam-reviewer (SECOND adversarial pass — same agent that FAILED iter-1)
**Reviewed:** 2026-06-25 19:55 CEST
**Verdict:** **ARCHITECT_REVIEW_PASS**

This is a Rule-3 invariant-test task: the headless NUnit suite is the entire gate. I FAILED iter-1 for a dead bracket-mix invariant (a uniform weight-ignoring sampler passed). My job this pass: prove the blocker really died (not just moved), by attacking the negative control's teeth and mutation-walking the production seam. I genuinely tried to break it and could not find a new blocker.

## Live re-run (Rule 5)

`unity-mcp-cli run-tool tests-run`, EditMode, `testNamespace=Golfin.Tournaments.Tests` →
`Status=Passed, TotalTests=557, PassedTests=50, FailedTests=0, SkippedTests=0, Duration=00:00:01.70`.
**50/50 PASS, 0 skipped.** `SkippedTests=0` matters — the B3 CSV-drift guard did NOT hit `Assert.Inconclusive`; it actually loaded and compared the shipped file.

## PRIMARY re-attack — did the iter-1 blocker REALLY die?

### 1. Mutation-walked the production seam (the decisive check)
Ported the identity-selection path faithfully (FNV-1a → xorshift64 → `SampleBracket`/`UniformBracketSampler`) and ran the mutation: **if the real `SampleBracket` inside `RollField` is replaced with a uniform draw, does `RollField_BracketMix_SampledTargetsApproximateWeights` FAIL?**

```
Q1 MUTATION: replace SampleBracket->uniform in production. PRIMARY test maxDelta=8.67pp
   -> PRIMARY test FAILS (mutation caught!)
```

Walked the seam in source: the test (`BotFieldInvariantTests.cs` L548) calls the **internal** `RollField(..., out var sampledBrackets)` overload; inside `BotFieldGenerator.cs`, `sampled.Add(targetBracket)` (L242) captures the literal return of `SampleBracket(cfg.BracketWeights, …)` (L241), and `sampledBrackets = sampled` (L286) hands it back. The test aggregates THAT `out` list (L552), not a re-computation. **The fix is real, not cosmetic — the test reads the actual production `SampleBracket` output.** The iter-1 defect (test measured `BracketKeyForLevel(rosterMap[BotId])`, the picked identity's natural level) is gone.

### 2. Negative-control teeth (decided this is sufficient)
Independently re-derived the negative control in Python on the published seeds:
```
NEGATIVE CONTROL (uniform): bracket "50" exp 25.0% obs 16.3% Δ=8.67pp *VIOLATES 8pp*  -> anyViolationFound=True
PRIMARY (real weighted):    maxDelta=2.50pp  (PASSES, 5.5pp headroom)
```
Structural honesty confirmed: the inlined control seeds identically (`Xorshift64(StableHash($"{seedId}:bracket"))` == `BotSeedFactory.BracketStream(def.Id)`) and consumes the **same draw count (1 ulong/bot)** as the real path, isolating only the sampler fn. It is NOT a hand-rolled unrelated loop — it runs the same tolerance/assertion structure with the sampler as the single substituted variable.

### 2b. 8pp tolerance sensitivity (reviewer punted this to me — I pressure-tested it)
Quantified the smallest weight-dilution bug that still passes 8pp on the test's 5 seeds:
```
Q2 SUBTLE BUGS:
  A  <= instead of < (boundary):        Δ=2.50pp  PASS (slips)
  D  50% uniform / 50% weighted:        Δ=6.67pp  PASS (slips)
  E  swap 50<->100 weights (5pp each):  Δ=5.33pp  PASS (slips)
  F  shift bracket index down by 1:     Δ=16.50pp FAIL (caught)
  (full-uniform):                       Δ=8.67pp  FAIL (caught)
```
So the gate catches catastrophic structural breaks (full-uniform, index-shift) but slips subtle skews. **I judge this NOT a FAIL:** `SampleBracket` is a 12-line cumulative-weight draw — there is no "dilution knob" in its structure to half-break, the off-by-one boundary (`<=` vs `<`) is statistically invisible by design, and a weight-swap would be an authoring error in `tournaments.csv` (T4's concern), not a `SampleBracket` regression. Tightening to catch 50%-dilution needs ~2400 draws or ~4pp tolerance, which puts you within 2.7σ of false-positiving on legitimate single-seed multinomial noise (verified: single-seed `bracket_mix_sampled:bracket` N=120 produces bracket "25" at 30.8% — a real 10.8pp noise excursion). Recorded as a documented residual + T4 tightening note, not a blocker.

### 3. Empty/unsatisfiable bracket-1 (roster has 0 level-1–9 identities)
End-to-end ported the default `weekly_test` botCount=24 field:
```
bracket-1 targets sampled: 3   (≈10%, weights honored — SampleBracket DOES sample bracket-1)
resolved IDs: 24, unique: 24, all valid: True   NO-REPEAT: PASS   AllInFakePlayers: PASS
```
- (a) `SampleBracket` samples bracket-1 at ~10% (primary test now verifies the *sampled target*, so this is genuinely covered despite the empty pool). ✓
- (b) The nearest-bracket fallback (`PickIdentity` L427) resolves each of the 3 unsatisfiable bracket-1 targets to a real adjacent-bracket identity — no throw (roster 120 ≥ botCount), no dup (`usedIds` HashSet). ✓
- (c) `RollField_BotIds_NoDuplicates` + `RollField_BotIds_AllInFakePlayers` exercise the fallback on every default run (3 of 24 slots route through it). ✓

## SECONDARY re-attack (my iter-1 B1/B2/B3 findings)

- **B1 — re-fuzzed BELOW 2H.** My iter-1 break was window<H. I ported the iter-2 4-phase algorithm and fuzzed windows from 2H down to **1s (0.11×H)**: zero strict-increase violations, zero >endUtc violations at every size. Reason: Phase-1 compress scales all completions proportionally into [botStart, end] FIRST, so the +1s repair in Phase 2/4 never overruns — the steps stay sub-second-distinct. My iter-1 break is gone, and the fix holds far below the 2H the test uses (so the 2H-vs-H spec deviation is acceptable, not under-tested). ✓
- **B2 — `Project` else-break removed.** Source L309-315 is an unconditional O(H) loop (only doc-comment reference to `else break` remains at L297). Correctness now independent of pace monotonicity. ✓
- **B3 — CSV-drift guard loads the REAL file.** `ShippedCSV_FakePlayers_MatchesInlinedFixture` reads `Assets/Resources/Data/fake_players.csv` via `System.IO.File.ReadAllText` (confirmed on disk: 120 rows, fp_001/FRODO/173 … fp_120/STING/185, matching the inlined fixture). `SkippedTests=0` proves it executed, not skipped. ✓

## Integrity (Rules 4, 6, 13)

- **Rule 6 (no fabrication):** every iter-2 PASS claim re-verified. Pinned hash `StableHash("abc") = -3547061803046329763` re-derived bit-exact. Implementer's spec-deviation claim "single-seed bracket 25 = 30.8% at N=120" verified EXACTLY in my port. Live 50/50 re-run matches. Nothing to log to `review_misses.log`.
- **Rule 4 / Rule 13:** `BotFieldConfig.cs` (BotCard / T1 contract) diff vs iter-1 commit = **0 lines**. iter-2 code diff = exactly the 3 reported files. Zero `using UnityEngine` in math/generator. Zero Physics/Scenarios drift. `ShellScene.unity` is uncommitted working-tree drift (708 lines vs iter-1 commit, but **0 between iter-1 commit and HEAD commit** — i.e. not in any commit, pre-existing T7 session state); content is all RectTransform overrides + PrefabInstance bookkeeping with **zero `m_IsActive: 0`** — not T3-attributable. This is a CLAUDE.md Rule-12 close-out hygiene item for Cesar (commit T7 separately or restore before the Active→Completed move), NOT a T3 defect.

## Three break-attempts and why each failed

1. **Visual/structural (the seam):** tried to show the negative control tests a different code path than the real generator. Failed — mutation walk proves the primary test reads real `SampleBracket` output and goes RED at 8.67pp when mutated; the control uses identical seed + draw count.
2. **Geometric/threshold (8pp porosity):** tried to find a likely one-line `SampleBracket` regression that slips 8pp. Found subtle skews slip (D/E/A) but no *plausible* T3 regression vector does — `SampleBracket`'s 12-line structure has no half-break mode; catastrophic breaks are caught. Documented, not a FAIL.
3. **Spec-intent:** tried to argue the sampled-target reframing dodges SPEC §7 "observed ≈ BracketWeights." Failed — §4 D2 (L57-58) explicitly defines the field mix as the *sampled target bracket* distribution and acknowledges nearest-bracket fallback decouples picked-identity-level from target; testing the sampled target is the spec-honoring interpretation, and the only testable one given the empty bracket-1 roster.

## Open items carried forward (for Cesar / T4 spec author)

1. **`BotCount > 120` decision (T4).** Either extend `fake_players.csv` to ≥ max(BotCount) OR cap `BotCount ≤ 120` in `tournaments.csv` with an authoring-time validator. T3's no-repeat constraint + 120-row roster forced the N=600-aggregate test design; T4 must resolve this.
2. **`BracketWeights` = distribution intent, NOT per-tournament quota.** A live `BotCount=24` tournament can render observed-bracket spread of ~±10pp from authored weight (genuine multinomial noise; verified). If exact per-tournament quotas are ever required, switch to stratified deterministic assignment. Document in `tournaments.csv` schema.
3. **8pp tolerance tightening (optional, T4).** If subtle-skew detection is wanted, add a second negative control (e.g. always-first-bracket) or move to stratified assignment + exact-quota assertion.
4. **CSV-drift test robustness.** B3 uses `Assert.Inconclusive` if the file walk fails (e.g. CI without the project tree). Live run executed it normally; for CI hardening, load via an EditMode TextAsset path. Backlog.
5. **`Golfin.Tournaments` asmdef:** add `noEngineReferences: true` once T4 lands, to lock the System-only guarantee currently held by convention.

## STATUS update

`STATUS.md` → `ARCHITECT_REVIEW_PASS`. Hands to Cesar for final approval. (Per the two-gate rule, the red-team is the sole agent that may write this state.)
