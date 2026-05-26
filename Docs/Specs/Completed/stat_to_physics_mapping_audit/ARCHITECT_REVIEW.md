# Architect Review — `stat_to_physics_mapping_audit`

**Reviewer:** golfin-reviewer
**Date:** 2026-05-25 20:02 CEST
**Iteration:** post-iter-2 (self-reviewer FORWARD_TO_ARCHITECT)
**Verdict:** `ARCHITECT_REVIEW_PASS`

---

## Independent visual scan (Step 0 — pre-narrative)

The captioned MP4 frame extracts show three distinct phases of a same-start LOW-vs-HIGH Ball Roll comparison: the title card (t02s) on black GOLFIN logo with bottom-left subtitle "Stat Lane Surface Roll / Ball.Roll LOW vs HIGH (same-start comparison)"; the LOW shot mid-flight (t22s) with bottom-left caption "Shot 1: LOW Ball.Roll=-10 (more friction) / fired from tee"; and the HIGH shot mid-flight (t40s) with caption "Shot 2: HIGH Ball.Roll=+10 (less friction) / fired from tee (reset)". The terminal stills (`roll_low_terminal.png` / `roll_high_terminal.png`) are nearly identical visually — both show the ball stopped just past the tee with virtually the same camera framing; the only differentiator is the bottom caption text ("LOW Ball.Roll = -10 / (more friction) / Terminal: (106.79, 10.15, 27.88) / Delta vs HIGH: 0.1m (WEAK)" vs the HIGH variant). The Hole 1 result screen (`hole1_result_3strokes.png`) shows SUCCESS for Lomond Country Club Hole 1, Par 5, 3 strokes (EAGLE) — confirming the FALLBACK 3-stroke playthrough completed in-bounds. The Hole 1 stroke screenshots (`hole1_stroke1_driver.png`, `hole1_stroke2_wedge.png`) show clean fairway/green/bunker positioning — no OB visible.

Caption positioning: all captions sit at the bottom-left on a dark-translucent strip; readable against bright fairway and dark trees; wrap is clean (3 lines max); no overlap with HUD elements. The LOW/HIGH terminal stills are self-identifying via caption text only — visually the ball positions look identical to my eye, which is consistent with the 0.1m measured delta — i.e., the captions are doing the work the pixels can't, which is correct for a perceptibility audit where the WHOLE POINT is that LOW vs HIGH look the same.

---

## Architectural soundness

### A1. Q3 bus-state design deviation — JUSTIFIED

The SPEC Q3 lock specified parameter-pass via `StatProviderBus.Resolve(bool isPutt, int labClubIndex)` with `ShotController.GetStatBundle()` passing `PhysicsLabController.Instance.CurrentClubIndex`. The shipped design uses bus-state: `StatProviderBus.CurrentLabClubIndex` (static, set by `PhysicsLabController.SetClub()`).

The implementer's pre-flight findings (IMPLEMENTER_REPORT.md lines 11-17) document the architectural constraint: `Golfin.Gameplay.Input` (ShotController) does not reference `Golfin.Physics.Viewer` (PhysicsLabController) — and the dependency direction is the reverse. Adding the reverse reference would create a circular asmdef dependency. The bus-state pattern in `Golfin.Gameplay.Defaults` (`autoReferenced=true`) is the canonical workaround per the HoleContext static-bus precedent (mentioned in StatProviderBus.cs:10).

**Verified safety properties:**
- `SetCurrentLabClubIndex` callsites grep-confirmed to be EXACTLY two: `PhysicsLabController.SetClub()` line 558 (lab flow) and `StatProviderBusTests.cs` (test setup/teardown). Production `LiveStatProviderHost.ResolveLive` does NOT set or read it.
- The bus state is only consulted on the FALLBACK path (when `Resolver?.Invoke(isPutt)` returns null). In production with a registered Resolver returning a real bundle, `CurrentLabClubIndex` is never read. So the bus state is effectively a lab-only routing hint, isolated from production codepaths.
- `BuildSwingBundle(int clubIndex = 0)` defaults to 0 (Driver) — same as pre-Q3 behavior. Any non-lab callsite that calls this without setting the bus gets Driver, which is the existing safe default and doesn't regress.
- `Iron7` (51 m/s, 25.5°, 6500 RPM) and `Wedge` (42 m/s, 41.2°, 9000 RPM) statics verified verbatim match against `PhysicsLabController.LabClubs[1]` and `[2]` (file line 524-525).
- Asmdef direction: Viewer → Defaults (lower-level). Defaults references only `Golfin.Physics.Stats` and `Golfin.Physics.Math` — no upward reference, no circularity.

**Conclusion:** the bus-state design is the simpler, cleaner solution, not heavier than the SPEC's parameter-pass design. The SPEC explicitly allowed implementer's choice when the parameter approach was found heavier ("Implementer's choice: ... surface as IMPLEMENTER_BLOCKED for architect re-scope rather than half-ship"). The implementer chose a smaller-footprint path; the architectural reasoning is correct.

### A2. Audit doc quality — STRONG

`Docs/Physics/STAT_LANE_AUDIT.md` (490 lines) delivers per-lane sections for all 13 lanes (8 StatModifierResolver + 5 BallPhysicsModifiers), each with:
- Source-stat identification
- Coefficient + cap math at HEAD
- Min/max impact table at realistic stat-range extremes
- Perceptibility classification (PASS / WEAK)
- Tier classification (Justified-as-is / Tier-Safe / Tier-Tune / Tier-Redesign)
- Filed follow-up SPEC link where applicable

**Perceptibility matrix at line 416:** complete (rows = 13 lanes, columns = LOW/MID/HIGH + Meets Bar + Tier). Internally consistent.

**Findings classification table at line 469:** clean separation of tiers, every Tier-Tune and Tier-Redesign row has a Follow-up Spec column populated.

**Filed follow-up specs inventory at line 483:** 5 rows, each links a Queued slug.

**B2 (Ball.Roll) honest reporting verified:** the measured 0.1m delta (corrected iter-2) is reported alongside the 4–8m theoretical estimate; the audit doc explicitly explains the gap ("Wedge approach steepness + backspin at power=0.55 means the ball barely rolls"); the methodology defect retraction is in writing at line 335. This is the right level of transparency.

**Sub-lane 2a Tier consistency:** body line 145 says "Tier-Tune"; matrix line 421 says "Tier-Tune"; findings table line 472 says "Tier-Tune". All three locations agree (iter-2 fix verified).

### A3. Follow-up specs are real specs — VERIFIED

Each of the 5 follow-up SPECs read individually:

| Slug | Tier | Problem statement | Scope | Hard rules | DoD elements |
|---|---|---|---|---|---|
| `strength_velocity_short_game_scaling` (33 lines) | Tier-Tune | YES (driver-vs-wedge coefficient scale mismatch) | 4 steps | YES (≥10m driver delta preserved, tests stay green) | Implicit DoD via scope + hard rules |
| `club_control_aim_arrow_speed` (30 lines) | Tier-Tune | YES (sub-perceptible stat in isolation) | 3 steps | YES (no aimConeReduction change; no new coefficient) | Implicit DoD via scope |
| `ball_rebound_perceptibility` (27 lines) | Tier-Tune | YES (±20% restitution swing below 10m bar) | 4 steps | YES (cap polarity preserved, Hole 1 completability) | Implicit DoD |
| `ball_roll_coefficient_retune` (31 lines) | Tier-Tune | YES (BallRollPerPoint=0.01 produces 0.1m measured delta) | 4 steps | YES (cap polarity, Hole 1 completability) | Implicit DoD via concrete coefficient proposal |
| `character_recovery_stamina_regen` (28 lines) | Tier-Redesign | YES (Recovery is a no-op stat) | 4 steps | YES (no per-shot stamina mult change; tests cover zero-vs-max Recovery) | Implicit DoD |

None are stubs. Each has a problem statement, scope, hard rules, and at least an implicit DoD via the scope steps. The Tier-Tune specs propose concrete coefficient values where applicable.

**Filing balance verified:** the audit's Findings Classification table has 6 non-Justified entries (F-LANA-1c, F-LANA-2a, F-LANA-2b, F-LANA-B1, F-LANA-B2, F-LANA-REC). F-LANA-2a and F-LANA-2b are both routed to `club_control_aim_arrow_speed` (one spec covers both since they're the same aim-cone lane). So 5 specs cover 6 findings — no over-filing, no under-filing.

### A4. Tier-Safe coefficient changes shipped — ZERO INLINE

STAT_LANE_AUDIT.md §"Tier-Safe Changes" (line 403) explicitly says: "No Tier-Safe coefficient changes are recommended in this audit iteration." The accuracy coefficient bump (0.0042 → 0.006) was considered and then reclassified to Tier-Tune because it would break existing `StatResolverTests.cs` assertions (a polarity/scope change that needs more than a unit test). That reclassification is documented at line 403-410. **No silent omission.**

The Q3 club-aware FALLBACK fix IS shipped inline — but Q3 is a data-routing fix (no coefficient change, no cap change) per the SPEC Q3 scope. It is correctly tracked at line 477 (`Q3-FALLBACK` row in findings table, "Tier-Safe (SHIPPED)").

### A5. PHYSICS_TUNING_CHANGELOG.md entry quality — STRONG

The Q3 section (line 54-101) covers:
- Task name + reason
- ClubStats changes table (DefaultIron7 + DefaultWedge before/after)
- DefaultStatProvider changes (signature + index mapping)
- StatProviderBus changes (bus-state pattern documented)
- StatCoefficients/StatCaps changes: explicit "None. This is purely a data-routing fix."
- Expected behavior table (4 stroke-type rows)
- Completability verification reference
- Tests added (5 explicit test names)

Not a stub. The "Why bus-state, not Resolve parameter" justification is in STAT_LANE_AUDIT.md (line 455-463) — would be slightly stronger if the changelog itself also carried that justification, but the cross-link is sufficient.

---

## Visual fidelity

### V1. Captions render correctly and unobtrusively — PASS

Frame extracts at t=2, 22, 33, 40s confirm captions are:
- Bottom-left positioned on semi-transparent dark strip
- Wrapped to fit portrait 250×540 aspect (3 lines max)
- Self-identifying (LOW/HIGH label, Ball.Roll value, terminal pos / delta)
- Do not obstruct ball/HUD action
- Document the methodology event ("reset" parenthetical at t=40s explicitly cites the ResetToTee call)

Quality meets `feedback_caption_videos_unobtrusively` standing rule. The iter-1 caption gap is fully addressed.

### V2. Terminal stills carry captions — PASS

Both `roll_low_terminal.png` and `roll_high_terminal.png` now carry captions identifying segment + Ball.Roll value + terminal coords + measured delta. The iter-1 "indistinguishable by pixels alone" defect is fixed: a reviewer can identify which still is which by reading the caption.

### V3. OB-avoidance verified — PASS

Per the bot log captured in IMPLEMENTER_REPORT.md lines 105-108:
- LOW terminal: (106.25, 10.15, 27.68) — on fairway per visible pixels
- HIGH terminal: (106.19, 10.15, 27.68) — on fairway per visible pixels

Both shots stay in-bounds with the Wedge at power=0.55 aimed yaw=π (fairway center). The Hole 1 FALLBACK 3-stroke playthrough screenshots also show fairway/bunker/green positioning, no OB indicators. BOT_FRAMEWORK §6 confirmed extant at line 163-189 with OB-avoidance content.

---

## Scene-mutation audit (Visual review checklist Step 4)

`git diff --stat HEAD -- '*.unity' '*.asset' '*.prefab'` → empty.

`git status --short` filtered for the change surface:
- Asmdef change: `Golfin.Physics.Viewer.asmdef` — single line added (`Golfin.Gameplay.Defaults` reference), correct direction, no removal.
- C# changes: `DefaultStatProvider.cs`, `StatProviderBus.cs`, `ClubStats.cs`, `PhysicsLabController.cs`, `StatProviderBusTests.cs`, `Scenarios.cs`, `LoopV2SmokeBot.cs`, `LoopV2SmokeBotMenu.cs` — all in the documented Files-Modified table.
- Doc changes: `AI_CONTEXT.md`, `PHYSICS_TUNING_CHANGELOG.md`, `STAT_LANE_AUDIT.md` (new), 5 follow-up SPECs (new), task-folder files.

**Incidental noise — non-blocking:** `Docs/Specs/Completed/puttpath_predictor_perf_and_design/STATUS.md` is shown modified from `ARCHITECT_REVIEW_PASS` → `DONE`. This is a status update on a separately Completed task; unrelated to this audit. Flagged for awareness only — not a fail.

**No scene, asset, or prefab mutations. CLEAN.**

---

## Capture-helper compliance

Captures here are bot-produced via `BotDriver.Capture()` routing through `CaptureCore.SnapPlayModeSafe` — the sanctioned path for play-mode coroutine captures. Video recorded via `BotVideoRecorder` (Unity Recorder pipeline) with post-process captioning via ffmpeg drawtext (filesystem-only operation, no scene side effects). No banned `ScreenCapture.CaptureScreenshot` use. **PASS.**

---

## Bbox verification

Not applicable — no containment claims in this content/code audit.

---

## Cross-cutting

### Test gate

`Docs/Diagnostics/all_editmode_test_results.txt` shows:
```
TOTAL  : 347
PASSED : 344
FAILED : 0
SKIPPED: 3
GATE: PASS
```
Above the 342/339/0/3 baseline. 5 new Q3 tests verified by source-grep (StatProviderBusTests.cs lines 130-212). Test gate confirmed.

### AI_CONTEXT.md update

Line 12 updated with iter-2 status: "**IMPLEMENTER ITER-2 COMPLETE 2026-05-25**" and a substantive summary of Q3 fix, F7 validation, follow-up specs, and iter-2 fix list. Note: line 12 still says "STATUS: READY_FOR_SELF_REVIEW" while STATUS.md is now `READY_FOR_ARCHITECT_REVIEW` — minor staleness in an in-flight document, not a blocker (will be updated again at DONE).

### Carry-forward from iter-1 self-review

All iter-1 PASS items (#1-#9, #11-#15) were re-verified or trusted-but-verified by the self-reviewer iter-1. I did not re-litigate them but spot-checked:
- Q3 fix code reads cleanly in all 5 files
- 5 follow-up SPECs are substantive
- PHYSICS_TUNING_CHANGELOG.md Q3 entry is detailed
- AI_CONTEXT.md line 12 carries the audit-complete update

No regressions or re-emergent issues.

---

## DONE-with-followups (non-blocking notes for Cesar)

These do NOT block PASS but should be captured for future hygiene:

1. **lessons.md not appended.** The kickoff brief flagged two candidate lessons that emerged from this task:
   - **Methodology lesson:** "any LOW-vs-HIGH same-start comparison MUST reset state between samples" — the iter-1 106.5m phantom delta because `ResetToTee()` was missing between shots. Worth codifying so future stat-perceptibility audits don't repeat this trap.
   - **Architectural lesson:** "asmdef build order can veto a SPEC's parameter-pass design; static-bus state is the canonical workaround when the dependency direction is wrong." The HoleContext precedent makes this a recurring pattern.
   
   Neither is in `tasks/lessons.md`. Per the kickoff brief this is "DONE-with-followups, not a FAIL." Suggest a follow-up Quick spec to append the lessons after Cesar's DONE.

2. **AI_CONTEXT.md line 12 still says STATUS=READY_FOR_SELF_REVIEW.** Stale at this moment in flight (should be READY_FOR_ARCHITECT_REVIEW). Will be naturally fixed at the Cesar-DONE step when the line is rewritten to closure language.

3. **B2 perceptibility measurement caveat.** The 0.1m corrected delta on a Wedge approach at power=0.55 doesn't isolate the BallRoll lane cleanly — the audit doc notes this and recommends the `ball_roll_coefficient_retune` follow-up spec instrument with a low-spin driver approach for a more diagnostic measurement. This is in writing in both the audit doc (line 339) and the follow-up SPEC (scope §3). Adequate.

4. **Incidental puttpath status diff.** `Docs/Specs/Completed/puttpath_predictor_perf_and_design/STATUS.md` modified to `DONE` from `ARCHITECT_REVIEW_PASS`. Unrelated to this task. Cesar may want to verify this is intentional or revert.

---

## Verdict

**`ARCHITECT_REVIEW_PASS`**

The audit is complete, internally consistent, architecturally sound, and visually verified. Q3 fix is mechanically correct with a justified design deviation. The bus-state pattern is safe (callsite-scoped to lab/tests, never read in production). The audit doc is substantive (490 lines, 13 lanes covered, per-lane perceptibility math, internally consistent matrices). All 5 follow-up SPECs are real with problem statement + scope + hard rules. The 0.1m B2 measurement is honest data and is reconciled with the WEAK / Tier-Tune classification. Test gate clean (347/344/0/3). No scene/asset/prefab mutations. No banned capture paths. OB-avoidance verified. Captions render correctly on video and stills.

STATUS → `ARCHITECT_REVIEW_PASS`. Hands off to Cesar's approval gate.

---

## Confidence

- **High** on Q3 architectural soundness (code traced, callsites bounded, asmdef direction correct, no thread/callsite collision).
- **High** on audit doc quality (per-lane math, perceptibility matrix, findings classification all internally consistent across iter-2 fixes).
- **High** on follow-up specs (read each individually; all substantive).
- **High** on visual fidelity (captions render, OB-avoidance held, terminal stills self-identify).
- **High** on regression safety (test gate above baseline, scene diff empty).
