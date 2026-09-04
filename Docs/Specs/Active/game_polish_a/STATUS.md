ARCHITECT_REVIEW_ESCALATE

# STATUS — `game_polish_a`

**Current:** `READY_FOR_REDTEAM` — the red-team's four report-only fixes are done and it is re-submitted
to the same gate. **No code, scene, prefab, test or JSON has changed since `golfin-reviewer` passed**,
which is why this returns straight to red-team rather than re-running the reviewer on identical code.
Citation check: `python3 Docs/Scripts/check_report_citations.py …/IMPLEMENTER_REPORT.md` → **78 cited,
0 unresolved**.

<details><summary>The FAIL this replaces</summary>

**Was:** `ARCHITECT_REVIEW_FAIL` — red-team gate. The **work is verified correct** (all
functional gates re-derived clean by the red-team; see `ARCHITECT_REVIEW.md` § RED-TEAM REVIEW) and
must NOT be re-implemented. The blocker is a **report-integrity defect only** (Shape C / defect #2,
third recurrence): `IMPLEMENTER_REPORT.md` § A4's lower table cites two files absent from disk
(`videos/game_polish_a_f_option_b.mp4`, `screenshots/a4_f_option_b.png` — renamed to
`…_f_cross_backdrop.mp4` when the flag was removed) with a dead "flag ON" column, and § A1's body
still quotes the stale pre-option-b run (`measured=48 / flag=false`) contradicting the on-disk JSON
(`measured=87 / optionBShipped=true`). Both sit in sections the implementer's own Shape C table
certifies as fixed, so the completeness claim is falsified.

**Fix was report-only** (no code/scene/prefab/test/JSON changes): correct the § A4 lower table to the
shipped clip + an existing still and drop the dead flag column/prose; replace or `(superseded)`-mark
the § A1 stale numbers with the on-disk 87/0/optionB values; re-run the Shape C heading sweep for
real and correct its § A4 / § A1 verdict rows. Then re-submit.

**All four done:** § A4's body table rebuilt from the six clips on disk (dead `flag` column and the
renamed `…_f_option_b` row gone, `raw.mp4` re-measured at 34.0 s / 1033 frames); § A1's summary and
all 87 rows GENERATED from the invariants JSON rather than transcribed; Shape C's verdict rows
corrected to record that the heading sweep MISSED both — the shape is stale *content*, not stale
headings, and a narrow check was reported as a complete one; and the sweep is now a script
(`Docs/Scripts/check_report_citations.py`) rather than another reading.

</details>

| Date | State | Note |
|---|---|---|
| 2026-09-04 | `READY_FOR_REDTEAM` | **Round-3 report fix, re-submitted.** Red-team's four stale-count sites corrected (§ A1 footnote 4/48→5/87, § A5 48→55 same-bg + the 32 cross-bg `seamWorstCover` invariant it never named, § A10 21→37 / 24→40, § A13 labelled). Grep for the old run's fingerprint found two MORE it did not name (§ A2's "all 24 pairs" / "48/48 records", and the push-map clause "every other move fades"). § A13 could not be closed — no artifact exists and a `perf` re-run produced 0 bytes — so it is labelled pre-option-(b) rather than quoted as current. New `check_report_counts.py` reconciles counts against the JSON: **0 stale**; citations **78/0**. No code changed since the reviewer's PASS. |
| 2026-09-03 | `SPEC_READY` | Map approved by Cesar (G1 = fade + option-(b) video behind an OFF flag). |
| 2026-09-04 | `IMPLEMENTER_WORKING` | Kicked off by Cesar directly. |
| 2026-09-04 | `READY_FOR_SELF_REVIEW` | Code + gates done. |
| 2026-09-04 | **OPTION (b) SHIPPED** | Cesar approved the clip; flag REMOVED. Re-measured 87 pushes, fail=0. |
| 2026-09-04 | `SELF_REVIEW_PASS` | Full acceptance re-walked (Rule 5). |
| 2026-09-04 | `READY_FOR_SELF_REVIEW` | **iter-2.** Centre-title dissolve fix (had shipped broken once via `??` fake-null). |
| 2026-09-04 | `SELF_REVIEW_PASS` (iter-2) | Iter-2 fix re-walked; dissolve confirmed in pixels; sweep 2430/0/3. |
| 2026-09-04 | `READY_FOR_REDTEAM` | golfin-reviewer PASS. |
| 2026-09-04 | `ARCHITECT_REVIEW_FAIL` | **Red-team.** Work verified correct; report-integrity blocker (dead file citations in § A4 + stale § A1 numbers, both mis-certified by the Shape C sweep). Report-only fix. |
| 2026-09-04 | `READY_FOR_REDTEAM` | **Report-only fix, re-submitted.** § A4's body table rebuilt from disk; § A1 regenerated from the invariants JSON; Shape C corrected to record that the heading sweep missed both, because the shape is stale content rather than stale headings. Replaced by `check_report_citations.py` → **78 cited, 0 unresolved**. No code changed since the reviewer's PASS. |

| 2026-09-04 | `ARCHITECT_REVIEW_FAIL` | **Red-team re-submission.** Work still correct; SAME report-integrity shape recurs — stale pre-option-b counts (4-of-48 frame-starved, 48 records, 24/21 pairs, perf 48/44) survive in live PASS sections § A1-footnote, § A5, § A10, § A13. Checker sees file paths only, not numeric drift. Shape-level count audit required; a 3rd same-shape FAIL must escalate. |
| 2026-09-04 | `ARCHITECT_REVIEW_ESCALATE` | **Red-team, THIRD pass → escalate (Rule 1).** Six named sites fixed & A13 honestly labelled (gate is a green ≤32B test, not the quote). But the SAME report-integrity shape recurs a 5th time: `## Option (b) shipped — re-measured` L88–97 still carries the stale 84-sweep run (`measured=84 / 52 same-bg / 4 starved / 9–16 frames / 0.293s`) vs on-disk JSON `87 / 55 / 5 / 10–16 / 0.268s` — a line the report ITSELF flags at L13 ("an earlier line here said 84") and left unfixed. Both new scripts pass it (count-script suspect set is a closed `{48,44,24,21,12}`; skip-regex is gameable). Work verified correct 3 rounds running; instance-chasing has not converged. Cesar decides: accept-with-caveat or mandate a structural regenerate-from-JSON fix.
