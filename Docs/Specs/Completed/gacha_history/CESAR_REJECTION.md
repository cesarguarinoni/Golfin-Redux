# gacha_history Stage 1 — Cesar rejection after ARCHITECT_REVIEW_PASS (2026-07-15)

The pipeline passed all gates (self-review → reviewer → red-team), but Cesar rejected at final approval
on ONE issue, now precisely measured by the orchestrator:

**Separator gaps are still asymmetric on CLUB rows.** Runtime measurement (card-edge → divider, canvas px):
- CLUB rows: ~42px above the divider / ~6px below (club card sits ~18px too high in its row).
- BALL rows: 24px / 24px (symmetric — correct).

The prior "24/24 symmetric" reports measured the invisible row-container box, not the visible card. Cesar has
flagged separator asymmetry twice; this is the same defect, finally measured correctly.

**Required fix:** see `CESAR_STAGE1_NOTES.md` item 12 — center the club card in `GachaHistoryRow.prefab`
vertically to match the ball row (which is already 24/24). Verify every divider's gapAbove ≈ gapBelow at runtime.
Only the club row changes; the ball row and shared BagClubCard are untouched.

Everything else in iter-7 is Cesar-approved (ball card family match, power icon, outline, formats, etc.).
