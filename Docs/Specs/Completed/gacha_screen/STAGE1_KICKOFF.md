# Stage 1 kickoff (2026-07-09)

**Stage 0 APPROVED** — Cesar hand-tuned the Main card in Live; the tuned layout was copied to
both side peeks and committed (`2159f7956`). Do NOT re-touch the Stage-0 card layout (banner/pity/
cost/buttons geometry). The live scene cards under
`Canvas/ScreensRoot/GeneralShopScreen/ContentArea/GachaTabContent` are the source of truth.

**Fork #2 RESOLVED (Cesar 2026-07-09): starter Gacha Ticket grant = 10** (test grant; fresh AND
migrated saves start with 10 so the counter/pull buttons are exercisable in dev — revert before ship).

## Stage 1 deliverables (SPEC §4 Stage 1 + §3b/§3a/§3d/§3e)
- `GachaTabController` (namespace GolfinRedux.UI.Gacha): wire the TabBar — GACHA → show GachaTabContent,
  hide STORE content + FilterGroup chip row; STORE → inverse; GIFTS grayed/inert. Active-tab gold styling.
  Default tab on Rewards Center open = GACHA.
- `GachaTicketManager` (mirror RewardPointsManager: Instance, GetTickets, SpendTickets, AddTickets,
  event OnTicketsChanged) + `SaveData.gachaTickets` (int, additive migration; starter grant = 10).
- `PersistentUIManager`: bind top-bar 999 ticket counter + Shop+ to GachaTicketManager.OnTicketsChanged
  (RP-pill double-subscribe guard); ticket counter visible on Home/Roster/Inventory/Rewards Center.
- Stubs: PULL x1/x10 -> ToastController "Coming soon" + log; History -> log; Shop+ -> log. No ticket spend.
- EditMode tests: ticket add/spend/insufficient/persist-roundtrip/migration-adds-field-without-loss.

Red-team focus = save-schema migration (SPEC §9).
