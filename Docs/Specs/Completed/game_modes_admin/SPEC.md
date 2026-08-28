# SPEC — `game_modes_admin`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-28 (Architect via Cowork). Cesar's requirement, same day: *"game mode entry
> prices and rewards should also be handled from the admin"* — the last piece of the content
> loop. Decisions of record 2026-08-28: **rewards are edited on a new Earn-actions panel over
> `game_point_actions` (the server truth)**; **mode entry fees are server-validated in this
> task**, not deferred to `progress_server_side`. Amended same day (Cesar): **card reward
> numbers are DECOUPLED from the paying actions** — except for multiplayer, the cards show
> AVERAGES over a later selection, so they are card copy, not a mirror of any action. The
> drift warning exists for exactly one pair: `versus_1v1` ↔ `versus_win`.
>
> **Do not kick off until `content_art_urls` (awaiting DONE) and `content_art_bundling` are
> finished** (Cesar, 2026-08-28).
>
> Standing invariant unchanged: a client missing information never shows a broken item and
> never wrongly spends RP.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Two different truths, two different treatments — naming that split is most of this spec:

- **Reward payments are already server-authoritative** (`game_point_actions`: earns enter ONLY
  through the catalog — `versus_win` pts 20, `hole_complete` max 20/event + 400/day,
  `hole_replay` max 5 + 100/day, `tournament_prize` cap 2000). What is missing is an admin
  surface: today a reward change is a Cesar-in-Supabase edit, unaudited.
- **Mode entry fees are client-asserted.** `ModeCardController.cs:604` debits
  `_data.entryFee` (from bundled `Assets/Resources/Data/modes.csv`, `ModesDatabaseCSV`, NO
  content overlay) through `PointsSpendGate` with reason `mode_entry_fee` — the amount is
  whatever the client says, the same class as the pre-`shop_server_purchase` shop. And the
  fee value itself is un-editable without a build.

After this task: `modes` is the **eighth content catalog** (fees, card copy, `locked`, reward
*display* — admin-edited, published, exported, imported like the other seven); a new
**Earn-actions panel** edits `game_point_actions` with audit; and `/points/spend` **refuses a
mode-entry debit that does not match the published fee**, with the shop's `price_changed` UX on
the client.

## 1. What is true today (verified 2026-08-28)

| Piece | State |
|---|---|
| `Assets/Resources/Data/modes.csv` | 5 rows (`practice`, `versus_1v1`, `tournaments`, `driving_range`, `missions`); columns incl. `entryFee`, `rewards`, `locked`, `target`, `order`, `reward1..3Type/Amount`, `rewardsTextKey` |
| `ModesDatabaseCSV` (`Assets/Scripts/UI/ModeSelect/`, 227 lines) | plain `Resources.Load`, no overlay, no ContentCatalogStore |
| Fee debit | `ModeCardController.cs:604` → `PointsSpendGate.Spend(_data.entryFee, SpendReasons.ModeEntryFee, …)`; fee 0 short-circuits in the gate and never reaches the server |
| Rewards display | card shows `x{mode.rewards}` or `LocalizationManager.Get(rewardsTextKey)` (`ModeCardController.cs:506–513`); the PAID amount comes from `game_point_actions` at earn time |
| `game_point_actions` | seeded by `2026_08_12_game_point_actions_rebalance.sql`; `pts` NULL = client-supplied amount bounded by `max_per_event`; no admin panel edits it |
| Tournament entry fees | already admin-set per tournament and debited server-side pre-entry (`tournaments_golfin.py`) — NOT this task |
| `/points/spend` | reason free text ≤ `MAX_REASON_LEN` 200; already refuses `reason == "shop_purchase"` (the §2.6 closure) — the pattern this task extends |

## 2. `modes` — the eighth content catalog

Mechanical application of the §3 machinery; every list below is "add `modes` beside the seven":

- `Tools/content/catalogs.py` `CATALOGS` += `Catalog("modes", "Assets/Resources/Data/modes.csv", "id")` — exporter, importer and `--check` pick it up from the table with no further code.
- Seed migration `2026_08_28_content_modes_seed.sql`: `content_catalogs` row + the 5 rows at
  version 1 into `content_rows` AND `content_drafts` (generate via `seed_from_csv.py
  --catalogs modes`; day-one parity by construction, first export must be byte-identical).
- Admin: **Modes panel** registered in `lib/registry.ts`; `REQUIRED_COLUMNS['modes'] =
  [id, title, entryFee, order]`, numeric = `[entryFee, rewards, order, reward1..3Amount,
  versusStrokeCapOverPar]`, `ID_COLUMN['modes'] = 'id'`. `+ New row` works automatically
  (shared control). EN + JA `DICT` entries.
- Client: `ModesDatabaseCSV` gains the overlay exactly as the other loaders did in
  `content_overlay_catalogs` — bundled row + `ContentCatalogStore.Catalog("modes")` patch by
  `id`, appended rows admitted, `is_active=false` drops the card, `RequireReady` for EditMode.
  Next-launch effect (I5), like everything else.
- **Withhold rule (the invariant):** a mode whose `target` is not one the running build knows
  (`hole_select`, `matchmaking_1v1`, `tournaments`, `none`) is **withheld with a warning** —
  an overlay-appended mode this build cannot enter must not render a card that taps into
  nothing. Read the real target set from wherever `ModeSelectScreenController` dispatches it;
  do not hard-code the list twice. `locked=true` rows render as Coming Soon exactly as today —
  which, note, makes "flip Missions live from the admin" a publish, not a build.
- Publish validation (`contentValidate.ts`): `entryFee ≥ 0`; `order` unique; `target` non-empty;
  `locked` parses as bool. **Card reward numbers are DECOUPLED from `game_point_actions` by
  decision** — except multiplayer, they are averages over a later selection (which hole, how
  played), i.e. card copy the operator words freely. The one place a card claims the exact paid
  amount is `versus_1v1` (fixed `versus_win` payout), so the drift warning covers exactly that
  pair and nothing else: WARN when `versus_1v1`'s `rewards`/`reward1Amount` ≠ `versus_win.pts`.
  No other mode is checked; do not generalise this into a mapping table.

## 3. Earn-actions panel — `game_point_actions`, edited with audit

NOT part of the content machinery — it is a live server table the earn path reads per request,
like tournaments. A **Rewards panel** in the dashboard:

- Lists `action, pts, max_per_event, daily_cap, once_per_user`; edits go through a
  `checkAdmin()` route + `writeAudit()` (`points_action_update`, before/after in the audit row)
  — the same posture as RP grant/adjust. No draft/publish cycle: an edit is live for the NEXT
  earn request, and the panel says so.
- Validation: `pts` ≥ 0 or empty (empty = NULL = client-supplied-under-caps; the panel explains
  this in a hint, EN + JA, because "pts is blank" looks like a bug otherwise); `max_per_event`
  and `daily_cap` ≥ 0 or empty; refuse deleting a row (actions are referenced by name from
  shipped clients — deactivation semantics do not exist here and are not being invented today).
- **No new earn actions from the panel** in this task: inserting an action the client never
  sends is harmless but pointless; adding one the client DOES send requires client code anyway.
  The panel edits the existing rows.

## 4. Server-validated entry fees

- Reason string gains the mode id: `SpendReasons.ModeEntryFee` stays the PREFIX, the call site
  sends `"mode_entry_fee:" + _data.id` (`ModeCardController.cs:604`). Ledger rows become
  per-mode legible in the Points panel for free.
- Publish of `modes` **upserts a `golfin_mode_fees` mirror in the same transaction**
  (`golfin_characters` pattern — the publish FAILS if the mirror write fails):
  `golfin_mode_fees(mode_id pk, entry_fee int ≥ 0, is_locked bool, updated_at)`. Migration
  `2026_08_28_golfin_mode_fees.sql`, RLS on / no policies, seeded from modes.csv, verification
  block, full SQL in chat for Cesar.
- `routers/points.py /spend`: when `reason` starts with `mode_entry_fee:`, parse the mode id;
  unknown mode → 200 `{"status":"unknown_mode"}`; `is_locked` → 200 `{"status":"mode_locked"}`;
  `amount != entry_fee` → 200 **`{"status":"fee_changed","fee":<published>}`** — nothing
  debited in any of these. A matching amount falls through to `spend_pts` unchanged. Business
  outcomes are 200-payloads, exactly like `insufficient` (a legitimate client hits
  `fee_changed` whenever a publish lands mid-session — it is a normal outcome, not an attack).
- Client (`PointsSpendGate` / `SpendOutcome`): new verdict `FeeChanged` carrying the server
  fee. `ModeCardController` handles it the way `GeneralShopScreenController` handles
  `PriceChanged`: update the card's fee display to the server's number, toast the existing
  "price updated"-style copy (reuse/localise), do NOT auto-debit — the second tap pays the
  shown fee. `unknown_mode` / `mode_locked` → the gate's generic refusal toast + card refresh.
- **Legacy bare `mode_entry_fee` reason stays accepted** until the build carrying the suffixed
  reason is what testers run; closing it is a separate one-line commit on Cesar's word — the
  `shop_purchase` §2.6 pattern, verbatim.

## 5. Sequencing

1. Backend: `golfin_mode_fees` migration → SQL in chat → Cesar applies → `/spend` validation +
   tests (`test_mode_entry_fee.py`, fake-Supabase style: unknown/locked/fee_changed/match/legacy
   bare reason passes) → deploy → smoke (`/health` + friends 200; a suffixed-reason spend with a
   wrong amount → `fee_changed` — this is the §21 live E2E, run it).
2. Content: `catalogs.py` + seed migration → Cesar applies → export round-trip byte-identical.
3. Admin: Modes panel + Rewards panel + validation + mirror-on-publish; `npm run build`.
4. Unity: overlay in `ModesDatabaseCSV` + withhold rule + suffixed reason + `FeeChanged`
   handling; EditMode sweep.
5. Runbook/README: `modes` joins the catalog list; Rewards panel noted as live-on-save.

## 6. Acceptance

- [ ] Edit practice's `entryFee` 10 → 15 in the admin, publish: client at next launch shows and
      is charged 15; a client still on 10 taps ENTER and gets `fee_changed`, card updates to 15,
      second tap debits 15 (ledger row `mode_entry_fee:practice`, −15). *(live E2E, §21)*
- [ ] Wrong-amount suffixed spend → `fee_changed`, nothing debited (ledger row count unchanged).
- [ ] Bare `mode_entry_fee` reason (old build) still debits.
- [ ] `is_locked` published → entry refused server-side AND the card shows Coming Soon next
      launch; flip Missions `locked=false` → card goes live with no build.
- [ ] Rewards panel: edit `versus_win.pts` 20 → 25 → audit row with before/after; next 1v1 win
      credits 25; Modes publish now WARNS that the 1v1 card still displays 20.
- [ ] Editing `practice`'s displayed reward to any number publishes with NO drift warning —
      cards other than versus_1v1 are decoupled copy by decision.
- [ ] `pts`-NULL actions show the explanatory hint on the Rewards panel.
- [ ] A published mode with an unknown `target` is withheld with a warning, never a dead card.
- [ ] `modes` round-trips: seed → export byte-identical → `--check` clean; import path works on
      a hand-edit to modes.csv; `Tools/content` tests green including the new catalog.
- [ ] Full EditMode sweep green; backend suite green; dashboard build green; EN + JA strings.

## Out of scope

- Tournament entry fees (already server-side per tournament) and tournament prize bands.
- New earn actions; deleting actions; stamina shop prices; gacha prices.
- Closing the legacy bare `mode_entry_fee` reason (separate commit, Cesar's word).
- `LevelUpCosts` as a catalog (still the open §9.2 decision in the plan).
