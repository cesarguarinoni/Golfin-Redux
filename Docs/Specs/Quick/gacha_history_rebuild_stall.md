# Quick · `gacha_history_rebuild_stall` — Gacha History arrives with a > 1 s stall

**Filed:** 2026-09-04 (Architect, game-polish track). **Found by:** `game_polish_a` A13 perf run — the
first instrument that ever measured a screen change on this screen. **Severity:** player-visible
hitch on every arrival at Gacha History; the layered push finishes before the screen has built
(1–2 rendered frames instead of ~15).

## Measured (`Docs/Diagnostics/_capture/game_polish_a_run.log` § A13, shipped build)

```
GachaPrizes -> GachaHistory    297.7 MB over 2 frame(s), worst frame 1271.04 ms
GachaHistory -> GeneralShop    276.8 MB over 1 frame(s), worst frame 1145.21 ms
```

Every other screen arrival in the same run: 347 KB – 1.2 MB / frame, worst 17–72 ms.

## Cause (read, not guessed)

`GachaHistoryScreenController.OnEnable` → `RebuildList()` destroys every child of `_scrollContent`
and re-instantiates **one full row per prize record** — `GachaHistoryStore.All` is the flattened
`/gacha/history` page (`FetchHistoryAsync(100, …)` = 100 PULLS, ×10 pulls flatten to up to
**1 000 records**) — and every club row's `GachaHistoryRow.BindClubCard` instantiates a complete
`BagClubCard` (`_clubCard.Initialize(playerClub, template, "")`: card art, stat arc, buttons then
disabled). Plus a `_dividerPrefab` between each pair, plus `Resources.Load<Sprite>` per row in
`BindCurrency`. The dev account has thousands of pulls (`gacha_client_real_pull`: ~6 000 tickets
spent through real pulls), so the screen builds ~1 000 club cards in one frame. The store's
`OnChanged` → `RepaintAnimated` → `RebuildList` repeats it when the server answers.

## Fix (minimal)

1. **Page the list.** Show the newest `PageSize = 40` records on first paint; append the next 40
   when the `ScrollRect` reaches the bottom (`onValueChanged`, `verticalNormalizedPosition <= 0.02`),
   until `All.Count` is exhausted. No new string: appending on scroll-end needs no button. Keep
   newest-first order and the divider rule (none after the last visible row).
2. **Repaint = diff, not rebuild.** On `OnChanged`, if the record list only gained rows at the head
   (`Prepend` after a pull — compare the first visible record's identity), insert those rows at the
   top; otherwise rebuild the FIRST PAGE only. Never destroy-and-respawn 1 000 rows.
3. **Cache the ticket sprite** — one `Resources.Load` per `ticketType`, not per row (a static
   `Dictionary<int, Sprite>` on the row class or the `TicketCatalog` entry).
4. Do NOT touch `BagClubCard`, the row prefabs, the store's fetch size, or the server.

## Done when

- A13's `perf` mode (`GamePolishProbe`, `Assets/Scripts/UI/Polish/Editor/`) re-run on
  `GachaPrizes -> GachaHistory` and `GachaHistory -> GeneralShop` with the SAME account: worst
  frame **< 50 ms**, alloc **< 20 MB** per arrival, and the push renders ≥ 10 frames. Quote all
  three before/after.
- Scrolling to the bottom appends the next page (a log line per append; one still after 3 pages).
- A real pull followed by opening History shows the new prize at the top without a full rebuild
  (log line `prepend N` not `rebuild`).
- `A2` rest parity for `GachaHistory` still 0 px vs the `game_polish_a` baseline for the first
  40 rows (the visible viewport is identical — page 1 IS what was on screen before).
- EditMode: `GachaHistoryPagingTests` — page boundaries, prepend-diff vs rebuild decision, ticket
  sprite cache hit. Full sweep green.
- `git status` shows only `GachaHistoryScreenController.cs`, `GachaHistoryRow.cs`, the new test
  file (+ `.meta`s).
