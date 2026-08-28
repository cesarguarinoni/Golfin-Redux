# Implementer Report — `store_banner`

> Implemented directly by Claude Code (main thread) at Cesar's instruction, not through the
> subagent chain. Every SPEC §6 checklist item is marked PASS/FAIL below with the measurement
> that produced it.

## Implementation summary

The Store screen's hard-coded `WinterSaleBanner` became the fourth `game_banners` placement,
`store`. No new machinery: one more value in the DB CHECK (already applied to prod), one more
entry in the backend `PLACEMENTS` tuple, `store` added to the dashboard's four placement tables,
`BannerPlacement.Store` appended to the client enum, and a `Button` + `ButtonPressFeedback` +
`BannerSlotBinder` added to the prefab object that was already there — mirroring the Rankings
prefab's `Banner` component-for-component. `GeneralShopScreenController.cs` is untouched and the
object keeps the name `WinterSaleBanner`.

Deploy order followed S4: migration (pre-applied) → backend (`fly deploy`, v61 → v62) → dashboard
→ client build (Cesar's, not part of this task).

## Files modified or created

| Path | Change |
|---|---|
| `playlife/backend/migrations/2026_08_28_store_banner.sql` | **created** — the CHECK-widening archive, mirroring `2026_08_17_tournament_banners.sql`'s header/VERIFICATION style. Already applied to prod by Cesar; not re-run. |
| `Tools/admin-dashboard/migrations/2026_08_28_store_banner.sql` | **created** — byte-identical copy, as the earlier banner migrations are kept. |
| `playlife/backend/routers/banners.py` | modified — `PLACEMENTS` gains `"store"`; module docstring's slot table now lists three auto-served slots + the prefab path; `list_banners` docstring corrected from "keeps its bundled sprite" to "is hidden" (A1). `NEVER_AUTO_SERVED` and the selection loop untouched. |
| `Tools/admin-dashboard/lib/types.ts` | modified — `BannerPlacement` union gains `"store"`; doc comment rewritten (three auto-served, none falls back to bundled art). |
| `Tools/admin-dashboard/lib/banner.ts` | modified — `BANNER_PLACEMENTS` gains `"store"` (after `rankings`, before `tournament_modal`); `PLACEMENT_IS_ASSIGNED.store = false`; `BANNER_ART_SPEC.placements.store` (978×252, prefab path, sprite path); `PLACEMENT_LABEL.store = "Store — banner"`. |
| `Tools/admin-dashboard/lib/i18n.ts` | modified — `"ban.placement.store"` EN/JA next to the rankings key. |
| `Tools/admin-dashboard/lib/mockBanners.ts` | modified — one `store` fixture, `isActive: false`, so mock mode starts where prod starts. |
| `Tools/admin-dashboard/app/api/banners/art/route.ts` | modified — the 400 message said "home_promo or rankings"; now derived from `BANNER_PLACEMENTS` so it cannot go stale again. |
| `Tools/admin-dashboard/lib/tournamentMutations.ts` | modified — comment generalised: a dangling `modalBannerId`, or one pointing at an auto-served row (`home_promo` / `rankings` / `store`), is a 400. |
| `Assets/Scripts/BannersRuntime/BannerService.cs` | modified — `BannerPlacement.Store` **appended** (ordinal 2), `TryParsePlacement` case `"store"`, `Signature()`'s enumerated array extended; enum doc now carries the APPEND-only warning (the value is serialized as an int in prefabs). |
| `Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab` | modified — `WinterSaleBanner` gains `Button` (transition None, interactable false, targetGraphic = the Image, onClick → `BannerSlotBinder.OpenLink`), `ButtonPressFeedback` (defaults), `BannerSlotBinder` (`_placement = Store`, `_image`/`_button` wired, both arrays empty, `_shiftDownTrim = 0`). +91 lines, all on that one object. |
| `Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs` | modified — `"store"` → `Store` positive assertion; `"shop"` and `"STORE"` negative assertions. |
| `Docs/AI_CONTEXT.md` | modified — session status. |
| `Docs/TellCode.md` | modified — completion line. |
| `Docs/Specs/Active/store_banner/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT}` | task bookkeeping. |

**Not touched, per spec:** `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs`,
`Assets/Art/Shop/Banner - Winter Sale.png`, `Assets/Scripts/Physics/`, `Scenarios.cs`.

## Screenshot

- **Canonical screenshot:** `screenshots/store_banner_live.png` — 1170 × 2532. The served `store`
  banner drawn above the card list, which is the state the whole task exists to produce.
- Supporting: `screenshots/store_no_banner_live.png` (nothing live, cards at the top),
  `screenshots/store_after_deactivate.png` (the same slot after deactivating the row — gone again,
  CLUBS filter still applied).
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes — booted, real dev sign-in, then the player's own path:
  `PersistentUI/BottomNavBar/NavGachaButton.onClick` → `.../TabBar/WeeklyTab` (label `STORE`)
  `.onClick`. No synthetic entry point, no `ShowScreen` shortcut.
- **Hole loaded:** n/a (UI task).

## Acceptance checklist (SPEC §6)

### Backend

| Item | Result | Justification |
|---|---|---|
| Constraint verified: the `check` lists `store` | PASS | Derived from the live database, not from the file: `POST /rest/v1/game_banners {"placement":"shop"}` → `23514 … violates check constraint "game_banners_placement_check"`, while `{"placement":"store"}` inserted and returned a row. Admits `store`, still rejects nonsense. |
| One active `store` row → served under `"placement": "store"`; `is_active = false` → absent | PASS | Active: `{"banners":[{"placement":"store","image_url_en":"…/game-banners/store-en-smoke9570280000.png","link_url":"https://golfin.io/store","expires_at":null}]}`. After `PATCH {"is_active":false}` (204): `{"banners":[]}`. Both against the deployed `playlife-api.fly.dev`. |
| Endpoint is 200 (not 307) on the bare path | PASS | `curl -o /dev/null -w "%{http_code}" https://playlife-api.fly.dev/api/v1/banners` → `200`, run before and after deploy. |
| Deploy landed | PASS | Not the exit code: `fly status` moved `playlife-api:deployment-01M1497KYMBVE7GA9T7VSS87GZ` / VERSION 61 → `deployment-01M158E482W76Z078TA8HFTMW6` / VERSION 62, both machines `nrt`, plus the live probes above. |

### Dashboard

| Item | Result | Justification |
|---|---|---|
| Editor dropdown shows "Store — banner"; list groups `store` with a 978×252 target and drift warning | PASS (by construction, not by screenshot) | The editor `<select>`, the list grouping, the size target and the amber aspect warning are all driven off `BANNER_PLACEMENTS` / `PLACEMENT_LABEL` / `BANNER_ART_SPEC.placements` — there is no per-placement UI code. All four tables now carry `store` (`aspect: 978 / 252`), and `npx tsc --noEmit` is clean, which is what proves the `Record<BannerPlacement, …>` maps are exhaustive. **Not visually confirmed in a browser** — see § Known gaps. |
| Upload lands at `game-banners/store-<locale>-<hash>.<ext>` | PASS | `uploadBannerArt` builds the key from the placement string it is handed and `isBannerPlacement` now admits `"store"`. Exercised end-to-end against the real bucket at `game-banners/store-en-smoke9570280000.png` (upload 200, public fetch 200, client downloaded 351 KB from it). Also fixed the route's stale 400 copy, which still said "home_promo or rankings". |
| Scheduling and sort-order visible for `store` | PASS | The editor hides those three fields on `isAssignedPlacement(placement)` only; `PLACEMENT_IS_ASSIGNED.store = false`, so they render exactly as for `home_promo` / `rankings`. |
| Activate / deactivate writes `admin_audit_log` rows | PASS (by construction) | Auditing is in `bannerMutations`, keyed off the row not the placement — no placement branch exists. **Not confirmed in the Audit panel** — see § Known gaps. |

### Client — EditMode

| Item | Result | Justification |
|---|---|---|
| `TryParsePlacement("store")` → `Store`; `"shop"` / `"STORE"` refused | PASS | `Golfin.TournamentsRuntime.Tests` 247/247, 0 failed. Proven to actually run, not silently skipped: a deliberate tripwire (`(true,"TRIPWIRE")`) produced exactly one failure — `Expected: (True, TRIPWIRE) But was: (True, Store)` — and was then reverted and re-run green. |
| All pre-existing `BannerPolicy` / `TournamentArt*` tests pass, unmodified | PASS | Same run: 247 passed / 0 failed. The only edits to that file are the three new assertions and one test rename. |

### Client — live play mode (Unity-verified; see § Known gaps for what a device would add)

| Item | Result | Justification |
|---|---|---|
| No `store` row live → no banner, first card at the top, no 252 px gap | PASS | `image.enabled=False`, `raycastTarget=False`, `ignoreLayout=True`, `button.interactable=False`; `GetWorldCorners`: `Card_shop_club_iron9_klyro` top edge is **0.00 px** below `GridContent`'s top. Frame: `screenshots/store_no_banner_live.png`. |
| Activate → reopen → the served image appears above the cards; tapping opens the link | PASS | After the row went live and the tab was re-entered through the real STORE tab: `storeLive=True`, `image.enabled=True`, `ignoreLayout=False`, `sprite` size `(978, 252)` — the **downloaded** sprite, not the bundled one (`[BannerArt] Downloaded and cached (351 KB)`). Banner at `gapFromGridTop = 0.00`, first card pushed to `276.00` (= 252 + the group's 24 spacing). Tap: `button.interactable=True` and `BannerPolicy.IsLinkAllowed("https://golfin.io/store") = True`, and the button's single persistent call is `BannerSlotBinder.OpenLink`. `OpenLink` itself was **not** invoked — it calls `Application.OpenURL`, which would launch a browser on Cesar's machine. |
| Deactivate → reopen (≥60 s later) → gone again, list closed up | PASS | Row set `is_active=false`, waited out the full 60 s refresh cooldown, re-entered via GACHA→STORE tabs: `storeLive=False`, `image.enabled=False`, `ignoreLayout=True`, first card back at `gapFromGridTop = 0.00`. Frame: `screenshots/store_after_deactivate.png`. |
| Filter chips rebuild with the banner still first when shown, absent when hidden | PASS | Clicked the real `CategoryRow/CLUBSChip.onClick` with the banner live: `siblingIndex=0`, `image.enabled=True`, `ignoreLayout=False`, `gapFromGridTop=0.00`, first card `276.00`. With it hidden, the post-deactivate frame shows the same CLUBS filter and four club cards starting at the top. |
| Airplane-mode cold launch: no banner, warnings only | PASS (equivalent path, not literal airplane mode) | The nothing-live launch is the same code path — `TryGet` returns false and `Hide()` runs — and it logged `[Banners] Banner source: DISK CACHE (previous fetch). Placements=0` then `SERVER … Placements=0` with no errors. The transport-failure branch is `game_banners` machinery this task does not touch. |
| `[BannerArt] Cache HIT` on the second launch (§7) | PASS | Second play session, same art URL, verbatim from `~/Library/Logs/Unity/Editor.log`: `[BannerArt] Cache HIT (351 KB), no download: store-en-smoke9570280000.png`. |

### Always

| Item | Result | Justification |
|---|---|---|
| All `[SerializeField]` references wired; prefab diff contains only §4.2 | PASS | `git diff --stat` = **91 insertions, 0 deletions**, all inside the `WinterSaleBanner` block. Read back from the saved asset: `transition=None`, `interactable=False`, `targetGraphic == Image`, `onClick` = 1 call → `OpenLink` in `m_Mode: 0` (EventDefined — matching Rankings exactly; the typed `AddVoidPersistentListener` writes `m_Mode: 1`, so it was re-registered), `_placement=2`, `_image`/`_button` both wired, `_expandOnHide`/`_shiftDownOnHide` empty, `_shiftDownTrim=0`. `LayoutElement` untouched at min/pref 252, `ignoreLayout` false. |
| Console has no errors related to this task | PASS | Both play sessions: 0 Error, 0 Exception. Six warnings, all pre-existing and unrelated — four `[ApiClient] SLOW … (cold start?)` and two `[GachaTab] Path not found: …/PullSection/PullX1Button|PullX10Button`. |
| Deviations flagged | PASS | § Spec deviations below. |

## Console output

```
[Banners] Banner source: DISK CACHE (previous fetch). Placements=0
[Banners] Banner source: SERVER (live fetch). Placements=0          ← nothing live: slot hidden
[Banners] Banner source: SERVER (live fetch). Placements=1
[BannerArt] Downloaded and cached (351 KB): store-en-smoke9570280000.png
[Banners] Banner source: SERVER (live fetch). Placements=0          ← after deactivate: hidden again
[Banners] Banner source: DISK CACHE (previous fetch). Placements=0
[BannerArt] Cache HIT (351 KB), no download: store-en-smoke9570280000.png   ← 2nd launch (§7)
[Banners] Banner source: SERVER (live fetch). Placements=1
```

Two `[Banners] Could not parse …` lines also appear in `Editor.log`. They are the EditMode suite's
own negative-path inputs, not play mode — verified: `BannerPolicyTests.cs:384` asserts
`ParseUtc("whenever")` is null.

## Spec deviations

- **Two files beyond SPEC §8's list were edited**, both places where the placement set was
  hard-coded and would otherwise have gone stale: `app/api/banners/art/route.ts`'s 400 message
  ("placement must be home_promo or rankings") is now derived from `BANNER_PLACEMENTS`, and
  `lib/tournamentMutations.ts`'s comment about which rows are a 400 to assign to a tournament now
  says "auto-served (`home_promo` / `rankings` / `store`)". This is exactly the grep §3 asked for
  ("every hit that is a *list* of placements must now include `store`").
- **The `onClick` listener was registered twice.** `UnityEventTools.AddVoidPersistentListener`
  wrote `m_Mode: 1` (Void); Rankings uses `m_Mode: 0` (EventDefined). Identical behaviour on a
  parameterless `UnityEvent`, but the spec says mirror Rankings, so it was re-registered with
  `AddPersistentListener`. The committed diff carries only `m_Mode: 0`.
- **Prod was written to, three times, and cleaned up each time.** SPEC §6's backend and client
  checks cannot be run without a live `store` row and real art. Created: one `store` row (twice)
  and one object `game-banners/store-en-smoke9570280000.png`. All deleted; verified from primary
  source — `?placement=eq.store` → `[]`, bucket listing shows only the four pre-existing
  `home_promo`/`tournament_modal` objects, and the endpoint is back to `{"banners":[]}`.

## Dashboard panel — verified in a browser (gap closed after the deploy)

Run locally with `MOCK_MODE=1 next dev -p 3111` (fixtures only, no Supabase connection) and driven
through the real login:

- **Editor dropdown**, read off the live `<select name="b-placement">`:
  `home_promo = "Home — promo strip"`, `rankings = "Rankings — banner"`, **`store = "Store — banner"`**,
  `tournament_modal = "Tournament — sign-up modal strip"` — the four values in the specified order.
- **List grouping:** a `Store — banner` heading of its own, annotated
  `store  978×252  GeneralShopScreen/ContentArea/BarsArea/RankingsArea/Modal/Bottom97/ScrollArea/Viewport/GridContent/WinterSaleBanner`,
  with the mock `Store — winter sale (draft)` row showing an `OFF` badge and an **Activate** button.
- **Scheduling and sort are visible** for `store`: the row renders the `Window (UTC)` and `Sort`
  columns exactly as the `home_promo` / `rankings` groups do, unlike `tournament_modal`.

## Known gaps

- **The `admin_audit_log` write on activate/deactivate was not observed in the Audit panel** — mock
  mode does not persist audit rows. Auditing lives in `bannerMutations` and is keyed off the row,
  not the placement; there is no placement branch to get wrong.
- **Airplane mode was not literally simulated** — the nothing-live path was exercised instead
  (identical `Hide()` branch).
- **`OpenLink` was not invoked**, to avoid launching a browser; the wiring and the allowlist result
  were read back instead.

### A1 stale-copy shape audit (found while verifying the Banners panel)

**Shape (mechanically checkable):** *does this text claim that a banner slot with nothing live
falls back to its bundled sprite?* False for EVERY placement since `game_banners` amendment A1
(Cesar, 2026-08-17) — `BannerSlotBinder.Hide()` sets `image.enabled = false` unconditionally.
Confirmed live this session: with nothing served, `HomeScreen/PromoBanner` and
`RankingsScreen/ContentArea/Banner` both read `image.enabled=False`.

**Verdict per site** (grep of `bundled` / `fallback` / `go blank` across the three subsystems;
every candidate listed, including the ones that are fine):

| Site | Verdict |
|---|---|
| `backend/routers/banners.py` docstring + `list_banners` | **FIXED this task** (was editing that block) |
| `admin-dashboard/lib/types.ts:255-260` `BannerPlacement` doc | **FIXED this task** (same) |
| `admin-dashboard/lib/banner.ts:90-92` `store` spec comment | OK — written correct |
| `admin-dashboard/lib/banner.ts:104-107` `tournament_modal` | OK — already says "no bundled fallback" |
| `admin-dashboard/lib/banner.ts:69` home_promo size note | OK — about the sprite's px, not the fallback rule |
| `admin-dashboard/lib/banner.ts:133` `deriveBannerState` doc | **STALE** — "every other state means the slot shows its bundled sprite" |
| `admin-dashboard/lib/banner.ts:291` | **STALE** — "leaves the bundled sprite" |
| `admin-dashboard/lib/bannerMutations.ts:45, 253` | **STALE** ×2 — one is an operator-facing toast |
| `admin-dashboard/lib/i18n.ts:331, 335, 484, 531, 535, 552, 583` | **STALE** ×7 — all player-operator-facing EN+JA copy |
| `admin-dashboard/lib/i18n.ts:570` | **STALE, worst one** — "nothing here can make a slot go blank", which is the exact opposite of the control Cesar asked for |
| `admin-dashboard/lib/i18n.ts:463` (`tournament_modal` empty state) | OK |
| `Assets/.../BannerService.cs:7, 153, 180, 286, 345` | **STALE** ×5 — file header and `TryGet`/`ResolveImageUrl` docs |
| `Assets/.../RemoteBannerSource.cs:20, 85, 98, 116, 119` | **STALE** ×5 |
| `Assets/.../BannerSlotBinder.cs:10` | OK — this file is where A1 is written down correctly |
| every other `bundled`/`fallback` hit (content catalogs, tournaments art, notices, `num(v, fallback)`) | OK — different subsystem, unrelated meaning |

**Not fixed here, deliberately.** All of it predates `store` and is wrong for `home_promo` and
`rankings` too; none of it is *caused* by this task. A ~20-site copy rewrite (including bilingual
operator strings) riding on a wiring commit would be unreviewable. Filed as follow-up.

## Open questions for Architect

- None. The one judgement call — whether to leave a real `store` row + art in prod so the banner
  could be switched on immediately — was resolved as *no*: S3 makes "nothing live" the correct
  starting state, and creating product content is Cesar's call, not the implementer's.
