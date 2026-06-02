# Red-Team Review — `club_control_arrow_polarity_fix`

**Reviewer:** golfin-redteam-reviewer (adversarial gate)
**Timestamp:** 2026-06-02 13:16 CEST
**Verdict:** ARCHITECT_REVIEW_PASS

---

## Task type & applicable gates

Physics/config + test polarity fix. NOT UI/Figma (no Figma ref → no bbox gate),
NOT mesh/terrain (Rule-16 mesh-metrics and Rule-17 mesh-bake-video do NOT apply,
videos/ correctly empty). Decisive objective gate: (a) code+config diff is exactly
the inversion + clamp and nothing else, and (b) automated tests prove the polarity
flipped. I re-derived the polarity math by hand from the LIVE coefficients to
cross-check the test assertions, since I cannot call `tests-run` myself.

## Evidence I generated/verified myself (not re-used)

- **Canonical screenshot:** `screenshots/cc_polarity_fix_2026-06-02_13-04-18.png` —
  verified via `sips`: 1920×1080, 3.4 MB, genuine in-game frame (fairway, conifers,
  OB stripe, sky). Not a fabricated flat-color frame. Per SPEC it is supporting
  evidence only; there is NO pixel-level acceptance surface for an arrow-Hz flip,
  so no flattering-angle attack is possible here.
- **`git diff HEAD`** on all 5 files — re-ran myself (see § Diff audit).
- **`git status --porcelain --untracked-files=all`** — re-ran; verified Rule-13.
- **Read myself, in full:** `ControlsConfig.cs`, `controls.csv`, `ShotController.cs`
  (`Tick`, `TickArrow`, `PublishState`, transitions, inject API), both test files,
  `SyntheticInputSource.cs`, `ShotDebugFlags.cs`, `StatProviderBus.cs`,
  `DefaultStatProvider.cs`, `CharacterStats.cs`.

## Prior rejections (Step 1)

No `CESAR_REJECTION.md` exists. This task has never been rejected (first iteration,
spun out of the closed `club_control_aim_arrow_speed`). Nothing to replay.

## Hand-derived arrowHz (decisive numbers)

Live coefficients confirmed byte-for-byte identical in BOTH sources:
- `.cs` (`ControlsConfig.cs` L67-68): `BaseArrowSpeedHzAtCC0=3.0f`, `ArrowSpeedHzPerCC=-0.025f`
- `.csv` (`controls.csv` L15-16): `BaseArrowSpeedHzAtCC0,3.0` / `ArrowSpeedHzPerCC,-0.025`
- → no runtime-revert risk (the ball_roll-lesson failure mode is absent).

Formula in `TickArrow` L296-298: `arrowHz = 3.0 + Clamp(cc,0,100)*(-0.025)`, then `*0.5` if putt.

| Case | CC | arrowHz | progress over dt=0.1 |
|---|---|---|---|
| Worst player (CC0) | 0 | **3.0 Hz** (hard) | 0.30 |
| Best player (CC100) | 100 | **0.5 Hz** (easy) | 0.05 |
| Over-cap CC120 (clamped→100) | 120 | **0.5 Hz** | 0.05 |
| Putt @ CC0 | 0 | **1.5 Hz** | 0.15 |
| Putt @ CC100 | 100 | **0.25 Hz** | 0.025 |

- Monotonic DECREASING in CC (3.0 → 0.5): higher CC = slower = easier. Matches Cesar's intent.
- Positivity: min over the REAL 0..120 stat range is 0.5 Hz (non-putt) / 0.25 Hz (putt). No 0, no negative, no NaN.
- **Test11** asserts `progressCC0 (0.30) > progressCC100 (0.05)` → TRUE. Under OLD polarity it would be `0.05 > 0.30` = FALSE → genuine regression gate, not a tautology.
- **Putt test** asserts `progressNonPutt (0.30) > progressPutt (0.15)` → TRUE, strict, exercises the 0.5× multiplier; CC equal (both bundles use `CharacterStats.Neutral`, CC=0).

My hand-derivation AGREES with the test assertions and with `ARCHITECT_TEST_RERUN.md`
(13/13 + 14/14, 0 failed/0 skipped). No disagreement → no measurement-conflict FAIL.

## Diff audit (surgical scope confirmed)

- `ControlsConfig.cs`: exactly 2 lines (the two values). All other Default fields at baseline (cone 5/20, MaxCleanPassesAtCC0=1, CleanPassesPerCC=0.04, MaxTotalPasses=10, PuttArrowSpeedMultiplier=0.5, …) UNCHANGED.
- `controls.csv`: exactly 2 rows (values + notes). Clean-pass / cone rows UNCHANGED.
- `ShotController.cs`: +1 clamp line (296) + `cc`→`ccClamped` on the arrow-Hz line (297) ONLY. Clean-pass line (309) and preview path (370-371) keep raw `cc` per SPEC. Putt multiplier (298) untouched.
- Test files: comment updates + Test11 add + putt-test rewrite. No `[Ignore]`/`[Explicit]`.
- ZERO `.unity`/`.asset`/prefab mutations. No `m_IsActive`/sizeDelta/position changes. No new Button (Rule 11 N/A).

## Rule 13 (files outside spec folder)

The 5 `M` code files = SPEC scope. The `?? Assets/Courses/Maps/Taiheyo/**` entries are
ALL `.meta` import sidecars — I verified the underlying `.png` assets are tracked in
git (pre-existing) and that ZERO non-`.meta` files are untracked under Taiheyo
(`git status | grep -vE '\.meta$'` → empty). Disclosed in IMPLEMENTER_REPORT table.
The `Docs/Diag` M's, `h07_iter8_*.jpg`, and `capture-all-holes.mjs` appear in the
HEARTBEAT iter-1 baseline DIRTY block → predate this task. Rule 13 satisfied.

## Three break-attempts (all failed)

1. **Visual:** No pixel-level acceptance surface exists for an arrow-Hz polarity flip;
   a still frame cannot show oscillation speed, and SPEC designates the test rollup as
   decisive. Verified the screenshot is a real 1920×1080 frame (not fabricated). No
   flattering-angle exploit possible. FAILED to break.
2. **Geometric/numeric margin:** The only fail-boundary is `arrowHz ≤ 0`. The clamp
   floors non-putt at 0.5 Hz and putt at 0.25 Hz across the entire real 0..120 stat
   range — the worst over-cap buff can't approach zero. The clamp converts a would-be
   fragile near-zero edge into a hard floor. No value sits within 20% of a bad
   threshold. FAILED to break.
3. **Spec-intent + test soundness (hardest):** Pursued the CC 0..120 vs SPEC's assumed
   0..100 gap. (a) A CC=120 character clamps to 100 → same 0.5 Hz as CC=100, so the top
   20 CC points give no further benefit — but this is exactly the SPEC-specified
   `Mathf.Clamp(cc,0,100)` positivity guard; un-clamped it would freeze (CC120→0Hz) or
   reverse (CC>120→negative) the arrow, a worse bug. The implementation is MORE correct
   than naive subtraction; the flat top-end is an architect-chosen design point, not a
   defect. (b) Confirmed the putt test's "equal CC" premise holds (both default bundles
   use `CharacterStats.Neutral`, CC=0; the differing club/putter doesn't touch CC). (c)
   Traced Test11's reset between CC0/CC100: `IsTouching=false; Tick(0.016)` with
   `CancelOnSlowFlick=true` (verified struct default) + zero velocity (verified
   `SyntheticInputSource` default) → `validFlick=false` → `TransitionToIdle()` zeroes
   `_arrowProgress`/`_passIndex`; fresh `ShotController` per `[SetUp]` so DebugFlags
   can't leak. The second measurement is clean. FAILED to break.

## The single hardest thing I tried

Break-attempt 3(a): whether the SPEC's `Clamp(cc,0,100)` is a hidden polarity/benefit
bug because the real stat ceiling is 120, not 100. It survived: the clamp is a
deliberate positivity guard that keeps arrowHz monotonic-non-increasing and strictly
positive (≥0.5 Hz) across the FULL 0..120 range — un-clamped, CC≥120 would zero/reverse
the arrow. Flat top-end above CC100 is an architect design choice already encoded in
the SPEC, not an implementation defect.

## Verdict

I genuinely attacked the diff, the numbers, the test soundness, and the spec intent,
and could not produce a concrete blocker. The fix is a surgical, correctly-clamped
polarity inversion; `.cs` and `.csv` agree; both new/modified tests are real gates that
fail under the old polarity; no scope creep, no scene mutation, Rule 13 clean.

**ARCHITECT_REVIEW_PASS.**
