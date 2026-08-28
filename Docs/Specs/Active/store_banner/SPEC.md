# SPEC — `store_banner`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

The Store (General Shop) screen still ships a hard-coded banner — `WinterSaleBanner` inside
`Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab`, drawing `Assets/Art/Shop/Banner - Winter Sale.png`
— that the admin dashboard cannot see, swap, schedule or switch off. This task makes it the
**fourth banner placement, `store`**, on the existing `game_banners` pipeline
(`Docs/Specs/Completed/game_banners/`). No new machinery: one more value in the DB CHECK, the
backend `PLACEMENTS` tuple, the dashboard placement tables, and the client enum, plus a
`Button` + `BannerSlotBinder` on the prefab object that is already there.

Behaviour of record from `game_banners` amendment A1 applies unchanged: **no live `store` row ⇒
the slot is hidden and the shop list closes up.** The Winter Sale sprite stays in the prefab as an
authoring placeholder and is never shown to a player. That is what gives Cesar the "turn it off"
control — deactivating (or never creating) a `store` row removes the banner on the next fetch.

## Decisions of record (Cesar, 2026-08-28)

| # | Decision | Consequence |
|---|---|---|
| S1 | Wire key is **`store`**, enum member **`BannerPlacement.Store`** | Cesar's name for the screen. The code calls it "GeneralShop"; the placement does not. |
| S2 | Auto-served, like `home_promo` / `rankings` | `start_at` / `end_at` / `sort_order` all apply. `PLACEMENT_IS_ASSIGNED.store = false`. |
| S3 | Hidden when nothing is live (A1) | The bundled Winter Sale art is a placeholder only. Not a fallback. |
| S4 | Deploy order: migration → backend → dashboard → client build | Old clients ignore an unknown placement (`TryParsePlacement` returns false → row skipped), so the server side can go first with no risk. |

## Reference

No Figma frame — no layout change. The slot keeps its `RectTransform` (978 × 252, centred) and
its `LayoutElement` (`min/preferredHeight 252`). Only components are added.

| Item | Value (measured 2026-08-28) |
|---|---|
| Prefab | `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` |
| Slot object | `ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea/Viewport/GridContent/WinterSaleBanner` |
| Components today | `RectTransform`, `CanvasRenderer`, `Image` (sprite `Banner - Winter Sale.png`, preserveAspect off), `LayoutElement` (min 252 / preferred 252 / ignoreLayout off) |
| Parent | `GridContent` — `VerticalLayoutGroup` (spacing 24, childControlHeight off) + `ContentSizeFitter` |
| Bundled sprite | `Assets/Art/Shop/Banner - Winter Sale.png` — **978 × 252**, aspect 3.88 |
| Controller | `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` — `Awake` finds `_banner = _grid.Find("WinterSaleBanner")`; `Rebuild()` calls `_banner.SetAsFirstSibling()`. Nothing else touches it. `ClearCards()` destroys only `_cards`. |

**Why no `_expandOnHide` / `_shiftDownOnHide`:** the slot sits inside a `VerticalLayoutGroup`
with a `ContentSizeFitter`, so `LayoutElement.ignoreLayout = true` (which `BannerSlotBinder.Hide()`
already sets — it finds the existing `LayoutElement` via `GetComponent`) removes the 252 px + 24 px
spacing and the cards move up on their own. Leave both arrays empty, exactly like Rankings' second
array. Do not add layout adaptation the screen does not need.

⚠️ **Keep the object name `WinterSaleBanner`.** `GeneralShopScreenController.Awake` finds it by
name. Renaming is a gratuitous diff with a runtime failure mode. Rename only if you also change the
`Find` string — don't.

---

## 1. Schema — `playlife/backend/migrations/2026_08_28_store_banner.sql`

> ✅ **APPLIED to prod by Cesar, 2026-08-28** — `pg_constraint` shows `store` in `game_banners_placement_check`. Do NOT re-run it. Still write the `.sql` file (both copies) as the archive.

Migration first, deploy second (`Docs/ADMIN_DASHBOARD_OPS.md` §3.2). Mirror the header /
VERIFICATION style of `2026_08_17_tournament_banners.sql`, which widened this same constraint.

```sql
alter table public.game_banners
  drop constraint if exists game_banners_placement_check;

alter table public.game_banners
  add constraint game_banners_placement_check
  check (placement in ('home_promo', 'rankings', 'tournament_modal', 'store'));

comment on column public.game_banners.placement is
  'Which in-game slot this banner fills. home_promo, rankings and store are auto-served '
  'by GET /api/v1/banners, one live row each. tournament_modal is NEVER served '
  'there — it reaches a player only via tournaments.modal_banner_id, inside '
  'GET /tournaments/golfin. start_at/end_at/sort_order do not apply to it.';
```

Copy the file into `Tools/admin-dashboard/migrations/` as the earlier banner migrations are. Verify
with the `pg_constraint` query from `2026_08_17_tournament_banners.sql`'s header before deploying
anything that writes a `store` row.

## 2. Backend — `playlife/backend/routers/banners.py`

- `PLACEMENTS = ("home_promo", "rankings", "store")`.
- Update the module docstring's slot table and the `list_banners` docstring ("two placements" →
  three; add the prefab path above).
- `NEVER_AUTO_SERVED` unchanged. Selection logic unchanged — it already iterates `PLACEMENTS`.

Deploy: `cd /Users/cesar/Documents/playlife/backend && export PATH="$HOME/.fly/bin:$PATH" && fly deploy`
via `nohup … &` + log poll (`ADMIN_DASHBOARD_OPS.md` §4.6). Verify:

```
curl -s https://playlife-api.fly.dev/api/v1/banners | head -c 400
curl -s -o /dev/null -w "%{http_code}\n" https://playlife-api.fly.dev/api/v1/banners   # 200, not 307
```

## 3. Admin dashboard — `Tools/admin-dashboard`

Everything in the Banners panel is driven off the placement tables in `lib/banner.ts`; the
editor's `<select>` iterates `BANNER_PLACEMENTS`, the list groups by it, upload validates against
it. So the panel work is data, not UI:

| File | Change |
|---|---|
| `lib/types.ts` | `BannerPlacement` union gains `"store"`; update the doc comment (§255–260) that names the auto-served set. |
| `lib/banner.ts` | `BANNER_PLACEMENTS` gains `"store"` (after `rankings`, before `tournament_modal` — list order is display order). `PLACEMENT_IS_ASSIGNED.store = false`. `BANNER_ART_SPEC.placements.store = { screen: "Store", where: "GeneralShopScreen/…/GridContent/WinterSaleBanner", sprite: "Assets/Art/Shop/Banner - Winter Sale.png", width: 978, height: 252, aspect: 978 / 252 }`. `PLACEMENT_LABEL.store = "Store — banner"`. |
| `lib/i18n.ts` | `"ban.placement.store": { en: "Store — banner", ja: "ストア — バナー" }` next to line 488. |
| `lib/mockBanners.ts` | One `store` fixture (OFF, so mock mode starts where prod starts: nothing live). |
| `README.md` | Placement list, if it enumerates them. |

No new routes, no new components. `validateBannerInput` / `isBannerPlacement` /
`uploadBannerArt` pick the new value up from `BANNER_PLACEMENTS`. Confirm by grepping for
`home_promo` across `Tools/admin-dashboard` — every hit that is a *list* of placements must now
include `store`; hits that are examples/comments may stay.

Deploy: `npm run deploy`, then `curl -s -o /dev/null -w "%{http_code}\n" https://admin.golfin.world/` → **302**.

## 4. Unity client

### 4.1 `Assets/Scripts/BannersRuntime/BannerService.cs`

- `BannerPlacement` gains `Store` **appended after `Rankings`** (serialized as int in the prefab —
  never reorder).
- `TryParsePlacement`: `case "store": placement = BannerPlacement.Store; return true;`
- `Signature()` (line ≈345): add `BannerPlacement.Store` to the enumerated array. Grep the file for
  any other `new[] { BannerPlacement.HomePromo, BannerPlacement.Rankings }` and extend each.

### 4.2 `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` — on `WinterSaleBanner`

Mirror `RankingsScreen.prefab`'s `Banner` object component-for-component:

1. `Button` — **`transition = None`** (A2: `ColorTint` + non-interactable paints the art at alpha
   0.502), `interactable = false` (binder flips it), `targetGraphic` = the Image,
   `onClick` → `BannerSlotBinder.OpenLink` on the same object.
2. `ButtonPressFeedback` (`Assets/Scripts/UI/ButtonPressFeedback.cs`) — the press feel.
3. `BannerSlotBinder` — `_placement = Store`, `_image` = the object's Image, `_button` = the new
   Button, `_expandOnHide` and `_shiftDownOnHide` **empty**, `_shiftDownTrim = 0`.

Leave the existing `LayoutElement` as it is. **The prefab diff must be only this object's new
components and their wiring.**

### 4.3 `GeneralShopScreenController.cs`

**No change.** It keeps finding `WinterSaleBanner` and pinning it first; a hidden slot with
`ignoreLayout` is still a valid first sibling. If you find yourself editing this file, stop — the
binder is the whole runtime.

### 4.4 Tests — `Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs`

- Add `Assert.AreEqual((true, "Store"), BannerProd.TryParsePlacement("store"));` beside line 343.
- Add a negative: `"shop"` and `"STORE"` do not parse.
- Every pre-existing test passes unmodified.

---

## 5. Security

Nothing new. `store` art must sit in the `game-banners/` bucket (`BannerPolicy.IsArtAllowed`) and
links go through `BannerPolicy.IsLinkAllowed` — both untouched.

## 6. Acceptance checklist (Implementer fills `IMPLEMENTER_REPORT.md`)

**Backend**

- [ ] Constraint verified via the `pg_constraint` query: the `check` lists `store`.
- [ ] With one active `store` row, `GET /api/v1/banners` returns it under `"placement": "store"`; with `is_active = false` it is absent.
- [ ] Endpoint is 200 (not 307) on the bare path.

**Dashboard**

- [ ] Editor placement dropdown shows "Store — banner"; the list groups a `store` row under its own heading with a 978×252 target and amber drift warning.
- [ ] Upload lands at `game-banners/store-<locale>-<hash>.<ext>`.
- [ ] Scheduling and sort-order fields are visible for `store` (not hidden like `tournament_modal`).
- [ ] Activate / deactivate on a `store` row writes `admin_audit_log` rows — checked in the Audit panel.

**Client — EditMode**

- [ ] `TryParsePlacement("store")` → `Store`; `"shop"` / `"STORE"` refused.
- [ ] All pre-existing `BannerPolicy` / `TournamentArt*` tests pass, unmodified.

**Client — on device (Cesar)**

- [ ] No `store` row live → Store screen opens with **no banner** and the first card row sits at the top of the list, no 252 px gap.
- [ ] Activate a `store` row in the dashboard → reopen the Store screen → the served image appears above the cards; tapping opens the link.
- [ ] Deactivate → reopen (≥60 s later, or relaunch) → gone again, list closed up.
- [ ] Filter chips (ALL / CLUBS / BALLS …) rebuild the list with the banner still first when shown, and still absent when hidden.
- [ ] Airplane-mode cold launch: Store shows no banner, Console warnings only.

**Always**

- [ ] All `[SerializeField]` references wired; the prefab diff contains only §4.2.
- [ ] Console has no errors related to this task.
- [ ] Deviations flagged at the bottom of `IMPLEMENTER_REPORT.md`.

## 7. Smoke evidence

Two screenshots of the Store screen: one with a live `store` banner, one with nothing live
(cards at the top). Plus the `[BannerArt] Cache HIT` Console line on the second launch.

## 8. Files this task touches

**New** — `playlife/backend/migrations/2026_08_28_store_banner.sql` (+ copy in `Tools/admin-dashboard/migrations/`)

**Modified**

- `playlife/backend/routers/banners.py`
- `Tools/admin-dashboard/lib/{types,banner,i18n,mockBanners}.ts` (+ `README.md` if it lists placements)
- `Assets/Scripts/BannersRuntime/BannerService.cs`
- `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab`
- `Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs`
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`

## 9. Out of scope

- Any other hard-coded art on the Store screen, the card templates, or the filter chips.
- The Stamina shop / detail screens.
- Removing `Banner - Winter Sale.png` from the project.
- Everything `game_banners` §10 already excludes (carousels, targeting, analytics, deep links).
