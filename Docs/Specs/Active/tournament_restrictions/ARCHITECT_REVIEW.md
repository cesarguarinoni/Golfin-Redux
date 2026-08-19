# ARCHITECT_REVIEW — tournament_restrictions (client half)

**Verdict: APPROVED.** 2026-08-19, Architect. Spot-checked against the working tree, not just the
report: gate placement in `OnConfirm` (after the already-entered short-circuit, before both payment
paths), the CSV-first rarity ladder in the modal adapter (`CharacterDatabaseCSV.Instance` →
`GetCharacterTemplate` fallback, `(int)rarity + 1` pinned to the server's 1-based `RARITY_RANK`),
`TournamentEligibility` as a pure rank-taking evaluator, `TournamentScheduleMapper` pass-through
with `NullIfBlank` normalisation, and the two new register statuses — all verified present and as
described. 1478/1475/0 suite state accepted; the whole-mode-sweep caveat is fine (a superset run
cannot mask what a filtered run can).

The adapter bug write-up is the valuable kind — the evaluator was never wrong, the feed was — and
finding it by driving the real CONFIRM is exactly why widget-click tests are in the acceptance list.

## Rulings on the open questions

**Q1 — level-check asymmetry: INTENDED, recorded.** The server's `golfin_character_level` check is a
plausibility backstop by design (its own comment says so); the client checks the character actually
entering. They can disagree; the failure is soft both ways (toast, no debit). Do NOT converge now.
If it ever matters, the additive fix is optional `character_rarity`/`character_level` fields in the
enter body — same trust level as the sync, one server-side comparison change.

**Q2 — `club_rarity_max` client-only: CONFIRMED as a design knob, not an integrity rule.** The
server cannot see the bag until any bag-sync exists. A modified client bypassing it affects that
player's own gear flavour, not scores or payouts. Stated here so nobody later mistakes it for a
security control.

**Q3 — `gear_rule = supplied`: agreed, and the dashboard will enforce the warning.** The A2
restrictions editor (Architect, in progress) ships with `supplied` DISABLED for authoring — visible,
greyed, tooltip "requires the standard-spec task" — so an unplayable promise cannot be authored by
accident. Unblocks when standard-spec lands.

**Q4 — inert `category`: ACCEPTED.** Carried because the contract mandates it; the sponsor line
carries the player-facing signal (Cesar, 2026-08-19). Standing note for any future category surface:
`category` and `sponsor_name` are independent columns — do not treat them as synonyms.

## What remains before this closes

1. **Commit** — Claude Code commits its 13 files (list in ARCHITECT_HANDOFF.md); the Klyro art +
   spec-folder drift in the tree is the Architect's and is handed over separately.
2. **A2 dashboard restrictions editor** (Architect) — after it ships, author ONE restricted
   tournament (rarity band + level band + max_players; NOT gear=supplied per Q3) and run the live
   round trip: RULES render from `list_golfin`, ineligible CONFIRM toast, server `ineligible` on a
   forced mismatch, `full` at cap.
3. **JA native review** — the 16 new rows, same handling as `tournament_signup_modal`.
4. **RULES visual fit on device** — pixel check of the worst line in the real body box.
