READY_FOR_REDTEAM

# STATUS — `design_consistency_audit`

**Current:** `READY_FOR_REDTEAM` (round 5 — confirmation pass on a post-PASS delta).

Round 4's red-team wrote `ARCHITECT_REVIEW_PASS` on commit `666c198a0`: it re-derived every headline
with its own extractor (all exact), confirmed the tracked evidence byte-identical to the tool output,
verified A9 empirically, and could not break the deliverable. **It also handed over two non-blocking
nits, and chasing the second found a real defect** — so the artifact changed after the gate passed
it, and the PASS no longer covers what is on disk. Re-submitting rather than carrying a stale PASS.

**What changed since `666c198a0`:**

1. **Nit 1 (prose):** "nine scale steps" listed `33` twice. It is **nine type STYLES over eight
   distinct SIZES** — `EN/Caption_2` and `EN/Caption_2_Medium` are both 33 px, differing by weight.
   The matcher always used the eight; only the prose was wrong. Fixed in the report (3 places) and
   `DESIGN_TOKENS.md`. **No count affected.**
2. **Nit 2 was a symptom of a real defect.** `HomeScreen__en.json` carried
   `reachedVia:"harness ShowScreen (Tier 2)"` — the Tier-2 loop dumps `CurrentScreenRoot()` without
   verifying the re-seat landed, so an id that resolves nowhere makes it dump whatever IS active
   (HomeScreen) under that screen's own filename. Measurements were identical, so no count moved and
   every gate passed; only the provenance was corrupted. Fixed both halves: **verify-then-dump**, and
   a **`TIER2_` prefix** (as modals carry `MODAL_`), which is why the corpus filter now excludes by
   prefix instead of a hand-kept name list — closing nit 2 at the cause.
3. **Third finding from the same thread:** `DumpCurrent` never passed a `via`, so every TAPPED screen
   recorded `"unspecified"` — read literally, 11 of 17 corpus dumps claim no provenance. Navigation
   was always real; only the record was missing. Runner now records it; **the committed dumps
   deliberately predate that fix and are labelled in the report rather than regenerated**, because
   re-running a pass to improve a label is how the Tier-2 contamination got in.
4. **New instrument caveat (report §7.6):** the corpus reproduces in its MEASUREMENTS, not its BYTES
   — countdowns tick, an animating glow alpha samples at a different phase, the build number
   increments. Compare corpora by measurement, never by md5.

**Numbers unchanged and re-verified after the re-runs:** 17/17 screens, LiberationSans 36 (+5 = 41),
Outline 20, Shadow 0, Filled 225 {Bar 182 / BarContainer 33 / BarPending 8 / GhostBar 2}, visible 701
/ panel 291, CJK 660 / 7, buckets 1389 / 209 / 139 / 46 / 274 over n=2057. Gate reports no
contradictions. EditMode **2709 / 2706 passed / 0 failed / 3 pre-existing skips**. A10 clean.

| Date | State | Note |
|---|---|---|
| 2026-09-06 (r5) | `READY_FOR_REDTEAM` | Post-PASS delta: nit 1 prose; nit 2 traced to a Tier-2 pass overwriting HomeScreen's dump (verify-then-dump + `TIER2_` prefix); provenance recording fixed; byte-vs-measurement caveat. No count changed. |
| 2026-09-06 (r4) | `ARCHITECT_REVIEW_PASS` | Red-team #4: every headline re-derived exact by its own extractor; tracked evidence identical; A9 empirical; 2 non-blocking nits. |
| 2026-09-06 (r4) | `READY_FOR_REDTEAM` | Red-team #3's 4 blockers closed (incl. a FABRICATED table row). Gate's 2 blind spots closed + 6 tripwires. Modal evidence restored; Tier-2 corpus contamination guarded; 61 dumps now tracked. |
| 2026-09-06 (r3) | `ARCHITECT_REVIEW_FAIL` | Red-team #3: §3.5 summed 228 w/ a row matching no data; Q7b 144; Q8 275; §3.8 prose 275. Gate blind to all four. |
| 2026-09-06 (r3) | `READY_FOR_REDTEAM` | Red-team #2's 4 blockers closed + 2 self-found. Checker rewritten and tripwired. |
| 2026-09-06 10:58 | `ARCHITECT_REVIEW_FAIL` | Red-team #2: §3.5 table (447≠225), Q5 (226≠225), §3.6 header (442/26 vs 701/291), JA F-row on wrong corpus. |
| 2026-09-06 | `READY_FOR_REDTEAM` | Red-team #1's two blockers fixed: JA S1→S3; corpus contamination fixed at source. |
| 2026-09-06 10:15 | `ARCHITECT_REVIEW_FAIL` | Red-team #1: JA finding visually refuted; shape counts contradictory. |
| 2026-09-06 | `READY_FOR_REDTEAM` | Cesar sent it straight to red-team, skipping `golfin-reviewer`. |
| 2026-09-06 09:55 | `SELF_REVIEW_PASS` | Self-reviewer verified ÷1.4, LiberationSans 41, node-table, A2/A9/A10/A12/A13. |
| 2026-09-06 | `READY_FOR_SELF_REVIEW` | 17 screens + 13 modals dumped, 15 crop sheets, 74 prefabs + 5 live roots linted. |
| 2026-09-03 | `SPEC_READY` | Audit-only task: findings report + per-screen fix list; no production change. |
