# gacha_prizes — STAGE 1 SPEC (controller + dual x1/x10 mode + wiring)

Cesar decisions (2026-07-16). Read with SPEC.md + CESAR_STAGE0_NOTES.md.

## Dual mode — ONE screen, two variants (Cesar)
The Prizes screen is opened from the gacha banner's pull buttons, in TWO modes:

- **PULL x10** → prizes screen showing the **10-card 4/4/2 grid** (the Stage-0 layout). COST = `x10`, button = `PULL x10`.
- **PULL x1** → the SAME screen but with **ONE card, centered at the GRID CENTER** (horizontally + vertically centered in the grid region where the 4/4/2 grid normally sits). COST = `x1`, button = `PULL x1`.

Implement as ONE `GachaPrizesScreen` prefab + a controller parameterized by `pullCount` (1 or 10):
- Controller spawns `pullCount` cards. For 10 → 4/4/2 grid. For 1 → a single card centered in the grid area.
- The COST `x{n}` label and the PULL button text (`PULL x{n}`) reflect the mode.
- Do NOT build a second prefab — one prefab, controller adapts the card container.

## Entry wiring
- `GachaTabController.OnPullX10` (currently a stub) → set pullCount=10, `ScreenManager.ShowScreen(ScreenId.GachaPrizes)`.
- `GachaTabController.OnPullX1` (stub) → set pullCount=1, show GachaPrizes in x1 mode.
- (Also wire `GachaBannerCard.OnPullX1/OnPullX10` if those are the live per-banner buttons — check which is the real entry.)
- Pass the mode via a controller method / pending-context field the screen reads OnEnable (mirror how other screens take context; no new ScreenId per mode).

## Data — MOCK pool (Cesar: "Mock Pull")
- Mock prize pool of real clubs across VARIED rarities (Common/Rare/Mythic/Legendary → silver/green/blue/gold frames) to match the node variety — bound via `BagClubCard.Initialize`. Locate the real club source (ClubDatabaseCSV / the actual Clubs data file — NOT Assets/Data/Clubs.csv, which is absent).
- x1 mode shows ONE card from the mock (a single mock reward).
- **PULL x10 / PULL x1 buttons on the prizes screen = STUB** (no real ticket spend; mock). Tapping = no-op / "coming soon" log.

## Register + BACK
- `ScreenId.GachaPrizes` in ScreenManager + inactive instance in ShellScene.
- BACK → `ScreenManager.ShowScreen(ScreenId.GeneralShop)` (gacha main).

## Also fold in
- **Exact 42.0 gaps** (Stage-0 was 42.9 top; tighten top padding so visible top gap == 42.0 == bottom).
- Keep everything Cesar approved in Stage 0 (grid, separator, gold PULL clone, silver BACK, no scroll).

## Gates
- EditMode tests: mock pool build; controller spawns correct count (1 vs 10); x1 card centered.
- Rule 21 lint fail==0; real-flow capture of BOTH modes (x10 grid + x1 single centered) via the real PULL x1 / PULL x10 entry, using the `screenshot-game-view` MCP tool (hand-rolled captures are hook-blocked), driving past the PLAY gate as a real user.
- Measure gaps + x1-centering precisely (geometric bounds, not pixel-color scans).
