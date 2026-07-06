# DESIGN BRIEF — general_shop_ui (Order 610, Shop pillar)

**Status:** QUEUED — DESIGN PASS. Resolve D1–D6 WITH Cesar + confirm the Figma frames, THEN write the SPEC. **Do not spec before decisions are locked** (scope-before-spec).

## What this is
The **general commerce shop** — clubs / balls / cosmetics / currency — distinct from the stamina
restaurant shop (`stamina_boost_shop`, Order 517, shipped). 517 was deliberately built standalone with a
reusable `ShopTransaction` seam precisely so 610 reuses the purchase guts rather than a bespoke rebuild.

## Verified grounding (already exists — REUSE it, don't reinvent)
- **`Assets/Scripts/UI/Shop/ShopTransaction.cs`** — the purchase transaction (pre-check RP → `SpendPoints`
  → grant callback → success/deny toast). The intended 610 hook. 610's *grant* differs (equip a club /
  add a ball / unlock a cosmetic) but the transaction flow is shared.
- **`RewardPointsManager.SpendPoints(int) → bool`** — RP spend (517's currency).
- **`Docs/Architecture/UI_ELEMENT_PALETTE.md`** — reusable UI atoms (RP pill, gold/silver buttons, badges,
  dividers, fonts) with verified paths + GUIDs. **Rule 22** requires an Element Reuse Map against it.
- **Tournament Selection** (`TournamentSelectionScreen`/`Card`) — clone base for list-style screens
  (per the reuse mandate). If 610 needs a grid, that's a NEW layout — flag it.
- **`Docs/Scripts/figma_node_to_spec.py`** — auto-generates the linter spec.json from Figma (wired into
  implementer step 6e; use it, don't hand-author).
- **517's data pattern** — `Assets/Resources/Data/*.csv` + a `ShopCatalog`-style loader — reuse if 610 is data-driven.

## Open decisions (resolve with Cesar — surface the fork, recommend, lock)
- **D1 — What's sold in v1?** Clubs / balls / cosmetics / RP-or-premium-currency packs — which subset ships first?
- **D2 — Currency.** RP only (like 517), or a premium currency too? A premium currency = a new currency
  system (real-money adjacency, bigger scope) — flag it explicitly if it comes up.
- **D3 — Entry point.** Where is the general shop launched from? Home screen? a dedicated Shop nav button? roster?
- **D4 — IA / layout.** Tabbed catalog (Clubs | Balls | Cosmetics) vs single grid; item grid vs card list.
  Clone Tournament Selection (list) or does the design need a grid?
- **D5 — Grant behavior.** Does buying a club/cosmetic also equip/preview it, or just grant to inventory?
  Where does the granted item land (Clubs inventory, etc.)?
- **D6 — Relationship to 517.** Shared shop shell/nav with the stamina shop, or fully separate screen?
  (517 shipped standalone; 610 could share a nav host — or not.)

## Figma
**[Cesar to provide 610 Figma node link(s) here.]** Run `get_metadata` + `get_design_context` on each
BEFORE any fidelity spec (Lesson AK / Rule 9). Drop node renders into `reference/` at spec time.

## Process
Resolve D1–D6 with Cesar → confirm Figma frames → write `SPEC.md` here → `reference/` node renders →
move Queued→Active → give the kickoff line as a **fenced code block**. Tier likely 3 (new screen(s) +
catalog data + nav entry) — confirm at spec time.
