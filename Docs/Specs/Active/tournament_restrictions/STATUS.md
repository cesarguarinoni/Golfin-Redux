# STATUS — tournament_restrictions

`AWAITING_DEVICE_PASS` (2026-08-19)

- Server half LIVE in prod 2026-08-18; client half implemented by Code and ARCHITECT_APPROVED
  (see ARCHITECT_REVIEW.md); all three commits landed 2026-08-19.
- A2 dashboard restrictions editor SHIPPED 2026-08-19 (typecheck clean, deployed, no service key
  in bundle, Access 302; gear=supplied blocked for authoring per review Q3).
- Live test tournament AUTHORED via the new editor: `restricted_test_open` (Lomond, OPEN
  2026-08-19 → 08-27, fee 0, category=competitive, max_players=100, per_division=100,
  division=level, char rarity Uncommon–Legendary, char level 10–200, gear=own, club cap
  Legendary). `list_golfin` verified serving all 10 fields verbatim.
- REMAINING (Cesar, device/Editor): RULES block renders the authored values EN+JA + pixel fit;
  ineligible CONFIRM (enter with a Common character — James/Mike — or level <10) → toast, no
  debit; eligible entry succeeds; optionally drop max_players to 1 after one entry to see the
  server's `full` denial. JA native review of the 16 client loc rows still open.
- Delete `restricted_test_open` from the panel when testing is done.
