# ARCHITECT_REVIEW — `gacha_client_real_pull`

**Verdict: PASS** (post-approval verification — Cesar approved 2026-08-31 after three rounds of
card corrections; this review checks the repo state the approval rests on and records what it
hands to spec D). Architect via Cowork, 2026-08-31.

## Verified in the repo (`3964cab3d` → `18d035cfb`)

- `GachaMockPrizePool.cs` gone; `DEFAULT_STARTING_TICKETS` and `SpendTickets(` have zero live
  references outside tests.
- `ContentCatalogs`: the four gacha consts, in `All`/`RequestList`.
- `ContentService.TryReinstallFromCache` (:570) refuses anything but the four gacha catalogs.
- `ShopTransaction.ApplyPurchaseGrant` has the `KindTicket` case (:507) that refreshes the ticket
  manager — the B-review gap is closed.
- `InventoryProjector`: tickets are not projected into the blob (both directions), with the
  reasoning comment.
- `GachaPullFlow.ApplyOk` applies in the spec's order — tickets → RP fold → forced drain →
  history (:203–218) — behind an injectable seam.
- `content_version.txt`: `texts=19` (the nine `GACHA_*` keys through the importer).
- Screenshots: `01_banner_card_numeric_costs.png` — title, `COST x50 / x450`, both guarantee
  lines bound to the row ("Guaranteed LEGENDARY or higher within [50 pulls]", "Every 10-pull
  includes at least one RARE"); `06_prizes_x10_mixed_kinds.png` — ten prizes on the club card at
  one size, dupe pills, repair-kit description, ball with `x3`.

## Deviations — all accepted

1. `GachaPullService` applies nothing; `GachaPullFlow.ApplyOk` owns the order — correct
   (asmdef boundary, same as `IServerBalanceSink`).
2. `x50` rather than `50` after the ticket icon — reads better; Cesar saw it.
3. Prefab cost labels re-pointed to `CountLabel` and widened — necessary.
4. `DrainGrants(force:)` — necessary for the Prizes screen to read a bag the grant reached.
5. Non-club prizes on the CLUB card via `BagClubCard.InitializePrize` — Cesar's call, replacing
   my §4.3 shop-card reuse. The `GachaHistoryRowBall` precedent makes it a named pattern rather
   than a new design. `BindTicket` / `BindForDisplay` stay for the shop's ticket rows.
6. Stale `LocalizedText` on the pity labels removed — right.
7–9. The three rebuild bugs (inactive clone, stale card on slot reuse, double name / wrapped
   label) — found by reading the render; fixed and re-verified.

§7 step 5 (publish a brand-new banner on prod) skipped on I6 grounds — accepted: the reinstall
path is proven live (v4 landed mid-session, card re-priced) and the append path has a test.

## Carried into spec D (amended now, pre-kickoff)

1. **5b is only half there.** `ContentService` fetches once at `Awake`; nothing re-fetches while
   the app is foregrounded, so a publish AFTER launch lands at the next launch, not the next
   Rewards Center open. The gacha-only reinstall is correct; the missing piece is a throttled
   foreground refresh. D §4c.
2. Gacha Banners panel banner copy still says pulls run on the mock (`i18n.ts:1460`). D.
3. `simulate()` x10-guarantee slot — already D §4b.
4. `ball_golfin` has 0 for all five stats in `Balls.csv` — a data fact, not a bug in the card.
   Cesar can fix it from the Balls panel (no build). Noted, not scheduled.

## Notes

- 8 of the 10 prizes in the mixed x10 were dupes: the seed pool has seven clubs. Before beta the
  pool wants filling from the 799-club roster — an admin task on the Pools panel, no build, and
  the economy's dupe-RP rates (`dupeRp`) should be set with that in mind (`ECONOMY_MASTER`
  §5 item 4 is now answerable).
- The prod footprint on `cesar.guarinoni@gmail.com` (2 500 tickets granted, 6 pulls, ~+600 RP
  from dupes) stays — these are the first real rows, as the report says.
- `TICKET_SHOP_BUILD` still `0`: set from `last_uploaded_build.txt` after the archive that
  carries this build (D §5).
