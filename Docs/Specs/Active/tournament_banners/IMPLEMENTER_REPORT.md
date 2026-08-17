# Implementer Report — `tournament_banners`

**Iteration shape:** `tournament-banners:resolver-and-data`
Iteration 1. Built and deployed in the mandated order; verified against production at each gate.

## Implementation summary

The one placement that was never built now exists end to end. `game_banners.placement` admits
`tournament_modal`, `tournaments.modal_banner_id` assigns one per tournament, `GET /tournaments/golfin`
joins it in **one** extra round trip and emits a `modal_banner` object, the Banners panel is
placement-aware and shows each banner's blast radius, the Tournaments panel has a picker, and
`TryResolveModalBanner` — the three-line stub — is now the real resolver.

**No consuming-side C# was touched.** `ApplyBanner`, `ApplyBannerState`, the 0 ↔ 32 padding switch,
the `_bannerRoot`/`_bannerImage`/`_bannerButton` fields and the link handler are byte-for-byte
unchanged; `git diff` on the controller is confined to the `TryResolveModalBanner` body and its doc
comment. `BannerService.BannerPlacement` was **not** extended (§4.3). The prefab *was* changed, but
only for Cesar's corner/rim correction — see § Cesar corrections.

The whole promise was demonstrated live: a banner assigned in the database appeared in the sign-up
modal **with no client rebuild**, and un-assigning it returned the modal to exactly 1167.

## Build order actually followed

Migration → PostgREST verify → backend + `fly deploy` → curl verify → dashboard + `npm run deploy` →
Unity. No `.select()` naming `modal_banner_id` was deployed before the column was confirmed
reachable by name, which is the failure mode that 500s the whole schedule for every player.

## Cesar corrections applied mid-iteration

| # | Correction | What changed |
|---|---|---|
| 1 | *"Banner should have a mask on it so the top corners curve like the container and not go outside of it. Also, the panel's top outline should be seen above the banner."* | Both were real: the strip's own radius-20 corners poked outside the panel's radius-50 curve, and it covered the 3px rim. The Figma root carries `overflow-clip rounded-[50px] border-3`, i.e. the design clips the banner to the rounded interior — so this is fidelity, not preference. `BannerRoot` keeps its 970×252 layout rect and its `Button` (a transparent `Image` is all the Button needs to raycast), and now holds a **`BannerClip`** child: 3px off the top only, masking with a new **`S_Common_BGCornerTop47Bottom20`** sprite — a 9-sliced atom whose four corner patches are independent, so the top arcs match the container's 50 minus the 3px rim while the bottom keeps the node's 20. Measured on the capture: **3px of white rim above the strip, 4px side inset, clip 970×249**, and the corner crop shows the arc following the container. |

⚠️ **That change is to `TournamentSignupModal.prefab`, which this spec's §8 lists as out of scope.**
Done on Cesar's direct instruction. It is recorded here for traceability but belongs to
`tournament_signup_modal` — the prefab's own task, still awaiting his sign-off — and is logged in
that report too. No C# in the consuming side was touched: `ApplyBanner` / `ApplyBannerState` / the
padding switch are still byte-for-byte unchanged, and the serialized `_bannerRoot` / `_bannerImage` /
`_bannerButton` references all survived (verified by reading them back off the live scene instance).

## Files modified or created

Every uncommitted path outside this task's spec folder is listed (Rule 13). **NOT THIS TASK** rows
are pre-existing or third-party drift — see § Working-tree drift.

### `playlife` repo (separate checkout at `~/Documents/playlife`)

| Path | Change |
|---|---|
| `backend/migrations/2026_08_17_tournament_banners.sql` | **created** — widens the placement CHECK, adds `tournaments.modal_banner_id` with `on delete set null`, column comments, and a VERIFICATION block |
| `backend/routers/tournaments.py` | modified — `modal_banner_id` in `list_golfin`'s `.select()`, ONE `.in_()` join against `game_banners` filtered `is_active`, `modal_banner` emitted per tournament, and the id popped so it never ships |
| `backend/routers/banners.py` | modified — `NEVER_AUTO_SERVED` and an explicit filter so a `tournament_modal` row can never be auto-served |
| `backend/routers/tournaments.py` (pre-existing) | **NOT THIS TASK** — the repo already carried your uncommitted `description_*` select edit from the previous task. Left untouched; it deployed alongside mine, which is the same code already live |

### `GolfinRedux` repo

| Path | Change |
|---|---|
| `Tools/admin-dashboard/migrations/2026_08_17_tournament_banners.sql` | **created** — the dashboard-side copy |
| `Tools/admin-dashboard/migrations/2026_08_17_tournament_description.sql` | **created** — the previous task's migration, copied across; the Architect never did |
| `Tools/admin-dashboard/lib/types.ts` | modified — `BannerPlacement` gains `tournament_modal`; `modalBannerId` on `TournamentRow`/`TournamentInput`; `assignedTournaments` on `BannersResponse` |
| `Tools/admin-dashboard/lib/banner.ts` | modified — `BANNER_PLACEMENTS`, a new `PLACEMENT_IS_ASSIGNED` / `isAssignedPlacement`, the 970 × 252 art spec, the label |
| `Tools/admin-dashboard/lib/bannerData.ts` | modified — reads `tournaments.modal_banner_id` and returns banner-id → tournament-slug lists, tolerating the column being absent |
| `Tools/admin-dashboard/lib/tournamentData.ts` | modified — maps `modal_banner_id` through `str()` |
| `Tools/admin-dashboard/lib/tournamentMutations.ts` | modified — new `validateModalBannerId` (400 on dangling or wrong-placement), called by create and update; persisted on create/update/duplicate and in the audit snapshot |
| `Tools/admin-dashboard/lib/mockTournaments.ts` | modified — `modalBannerId: null` so the fixtures still satisfy `TournamentRow` |
| `Tools/admin-dashboard/app/(panels)/banners/banner-editor.tsx` | modified — hides `start_at`/`end_at`/`sort_order` for `tournament_modal`, explains what governs instead, and names the assigned tournaments in the delete confirmation |
| `Tools/admin-dashboard/app/(panels)/banners/banners-panel.tsx` | modified — "Assigned to N tournaments" per row, and passes `assignedTo` to the editor |
| `Tools/admin-dashboard/app/(panels)/tournaments/tournament-editor.tsx` | modified — `ModalBannerPicker` on the Artwork tab: active `tournament_modal` banners + None, thumbnail, link readout, orphan warning, link out to `/banners`, **no upload control** |
| `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs` | modified — `RemoteModalBannerDto` + `[JsonProperty("modal_banner")]` |
| `Assets/Scripts/Tournaments/TournamentDefinition.cs` | modified — `ModalBannerImageUrlEn/Ja/LinkUrl`, null-defaulted at the end of the constructor |
| `Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs` | modified — passes the three through; absent/null `modal_banner` → three nulls |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | modified — **`TryResolveModalBanner` only** |
| `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab` | modified — **Cesar correction 1 only**: `BannerClip` child added under `BannerRoot`. Formally `tournament_signup_modal`'s file; see the note above |
| `Assets/Art/Original UI/Common/S_Common_BGCornerTop47Bottom20.png` (+ `.meta`) | **created** — the two-radius mask atom for the strip (top 47 / bottom 20, border 52) |
| `Docs/AI_CONTEXT.md` | modified — this task's entry |
| everything under `Docs/Specs/Active/tournament_signup_modal/` | **NOT THIS TASK** — the previous task, still awaiting your sign-off |
| `Assets/Localization/*`, `Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab`, `Assets/Scenes/ShellScene.unity`, `Assets/Art/Original UI/Common/S_Common_BGCorner50.png`, `Assets/Scripts/TournamentsRuntime/TournamentDescription.cs`, `…/Tests/TournamentDescriptionTests.cs`, `Assets/Art/…/S_Common_BGCorner20.png.meta` | **NOT THIS TASK** — the previous task's files |
| `Assets/Plugins/NuGet/*`, `Packages/manifest.json`, `Packages/packages-lock.json` | **NOT THIS TASK** — in the iter-1 kickoff baseline in `HEARTBEAT.log` |
| `ProjectSettings/ProjectSettings.asset`, `Assets/Editor/iOSPostProcess.cs`, `Docs/TESTFLIGHT_RUNBOOK.md`, `Assets/Resources/Characters/Homescreen/Resol Golf Banner.png` | **NOT THIS TASK** — the iOS TestFlight work |
| `Assets/Localization/Editor/LocalizationBuildHook.cs.meta`, `_to_delete/stale-git-locks/**` (both repos) | **NOT THIS TASK** — see § Working-tree drift |

## Working-tree drift (not introduced by this task)

- **`Assets/Localization/Editor/LocalizationBuildHook.cs.meta` is untracked while its `.cs` is tracked.**
  That is the Lesson R failure mode (a `.cs` committed without its `.meta`), and it is not mine — the
  file appeared during this session from other work. Worth committing the `.meta` alongside.
- **`_to_delete/stale-git-locks/**` in both repos** — appeared mid-session, not written by me.
- The iOS TestFlight files and the whole `tournament_signup_modal` change set are from other work
  and must not be swept into this task's commit.

## Screenshot

- **Canonical screenshot:** `screenshots/assigned_banner_1411.png` — 1170 × 2532. The strip rendering
  real 970 × 252 art fetched from Storage, reached through the real player path.
- `screenshots/unassigned_no_banner_1167.png` — the same modal on an unassigned tournament: no strip,
  no gap, 1167.
- **Captured at:** `Docs/Diagnostics/_capture/screenshot_2026-08-17_18-12-53.png` / `…_18-13-19.png`
  (existence asserted after each write, not trusted from a returned path)
- **Scene / play mode:** `Assets/Scenes/ShellScene.unity`, play mode Yes
- **Entry path:** boot → `NavigateToHome` → `NavTeeButton.onClick` → ModeSelection →
  `TOURNAMENTS (TEMP).onClick` → TournamentSelection → the kasumigaseki card's `SIGN UP` `onClick`.
  Real widgets throughout, via `BotDriver.Click`.

## Acceptance checklist

### Schema

| Item | Result | Justification |
|---|---|---|
| The real CHECK constraint name was read from the DB before dropping it | PASS | Probed with a deliberately failing insert over PostgREST (`placement:"__probe_nonsense__"`), which returned `23514 … violates check constraint "game_banners_placement_check"`. It matched the spec's guess, but it was **read, not trusted**. Nothing was written — the insert failed by design. The migration file records the probe and also gives the `pg_get_constraintdef` query. |
| Inserting a `tournament_modal` row succeeds; `nonsense` still fails | PASS | Post-migration: `tournament_modal` inserted (id `1a6f820f…`); `placement:"__nope__"` still `23514`. Both proven before any deploy. |
| `tournaments.modal_banner_id` verified over PostgREST by name before any deploy | PASS | `?select=slug,modal_banner_id` returned rows with `modal_banner_id: null`. Before the migration the identical call returned `42703 column … does not exist`, so this distinguishes "landed" from "stale schema cache". |
| Deleting an assigned banner leaves the tournament alive with `modal_banner_id = null` | PASS | With kasumigaseki assigned, deleted the banner row → `HTTP 204`; the tournament still exists (`is_active: true`) with `modal_banner_id: null`, and the endpoint still returned all 6 tournaments. |

### Backend

| Item | Result | Justification |
|---|---|---|
| `GET /api/v1/banners` does **not** return `tournament_modal` rows, proven with one live | PASS | With the smoke banner `is_active = true`, the deployed endpoint returned `placements served: ['home_promo']`. |
| `GET /tournaments/golfin` returns `modal_banner` for an assigned tournament and `null` for an unassigned one | PASS | `kasumigaseki_open` → the full object (`image_url_en`, `image_url_ja: null`, `link_url`); the other five → `null`. |
| Switching the banner row `is_active = false` makes it `null` on the wire **without** touching the tournament | PASS | Toggled false → wire `null`, DB `modal_banner_id` unchanged (`1a6f820f…`); toggled true → `PRESENT`, DB unchanged. Server-side, so the client never learns `is_active` exists. |
| The join is ONE extra query for the whole payload — confirmed by reading the code | PASS | `routers/tournaments.py`: one `.in_("id", list(set(banner_ids)))` outside the loop, then a dict lookup per tournament. Same shape as the prize-bands fetch above it. No query inside `for t in tournaments`. |
| `modal_banner_id` does **not** appear in the payload | PASS | `t.pop("modal_banner_id", None)` before the row is appended; the live payload's key set contains `modal_banner` and not `modal_banner_id`. |
| The schedule endpoint still returns all 6 tournaments and 19 base fields | PASS | Diffed the live key set against the 19 keys the payload carried before this task: **dropped: none**, **added: `['modal_banner']`**. Count 6 both before and after. |

### Dashboard

| Item | Result | Justification |
|---|---|---|
| `start_at`/`end_at`/`sort_order` hidden for `tournament_modal`, visible for the other two | PASS (code) / **manual** (visual) | Each of the three wrappers is `className={assignedPlacement ? "hidden" : "col-span-1"}` where `assignedPlacement = isAssignedPlacement(draft.placement)`, plus an explanatory panel that only renders for assigned placements. `tsc` and `next build` clean, deployed. Not clicked. |
| The picker lists only active `tournament_modal` banners, plus None | PASS (code) / **manual** | `options = banners.filter(b => b.placement === "tournament_modal" && b.isActive)`, with a literal `None` option first. Also handles the orphan case (an assignment whose row went inactive stays visible with an amber warning) so a vanished strip is explicable. |
| A `modal_banner_id` naming a `home_promo` row is rejected with 400 | PASS (logic proven against live data) / **manual** (round-trip) | `validateModalBannerId` looks the id up and rejects any `placement !== "tournament_modal"`. Ran that exact lookup against the real `home_promo` id `386114c5…` → `{placement: "home_promo", label: "Default"}`, i.e. the 400 branch. A dangling id returns 0 rows → `maybeSingle()` null → the "does not exist" 400. It is wired into both `createTournament` and `updateTournament` (one call each, verified by line number). |
| Assignment round-trips: save, reload, still selected | **manual** | Persisted in `toDbRow` and both optimistic rows, and read back by `tournamentData.mapTournament`. Needs a click-through. |
| "Assigned to N tournaments" correct after assigning to two | PASS (code) / **manual** | Counted server-side in `fetchBanners` off `tournaments.modal_banner_id`; the row renders the count with the slugs in a `title` tooltip. Verified the underlying query works (it is how the delete test read the assignment). Two-tournament case not clicked. |
| Deleting an assigned banner requires the typed confirm and names the tournaments | PASS (code) / **manual** | The typed-label confirm already existed for every delete; added a red block above it listing the count and slugs and stating the tournaments survive. |
| The assignment appears in the Audit Log inside a `tournament_update` before/after | PASS (code) / **manual** | `modal_banner_id` added to `snapshot()`, which is what the existing `tournament_update` audit writes. No new action name. |

### Client

| Item | Result | Justification |
|---|---|---|
| Assigned + active → the strip renders at 970 × 252 and the modal measures **1411** | PASS | Live: `panel=(978,1411) padTop=0 bannerRoot active=True rect=(970,252)`, sprite a runtime-decoded 970 × 252 texture, side margins measured **4 / 4**. After Cesar correction 1 the drawn clip is 970 × 249 with 3px of panel rim above it and its top corners on the container's curve. `screenshots/assigned_banner_1411.png`. |
| Unassigned → no strip, modal measures **1167**, no gap | PASS | Live on `lomond_championship`: `panel=(978,1167) padTop=32 bannerActive=False`, button non-interactable. `screenshots/unassigned_no_banner_1167.png`. The state-B regression holds. |
| JP player with `image_url_ja` set gets JA; with it null, falls back to EN | PASS | With `image_url_ja: null`, a JP player rendered the EN art (1411, banner active). The JA-set direction is covered by the shared ladder plus a direct branch test: JP + both → JA, JP + EN only → EN, EN + JA only → JA. |
| An `image_url_*` outside the `game-banners` prefix is refused and renders state B | PASS | Injected `https://evil.example.com/banner.png` → `panel=(978,1167) bannerActive=False`, one warning logged, nothing downloaded. Also unit-exercised through the resolver directly. |
| The strip's link opens for an allowlisted host; the button is non-interactable with no link | PASS (interactable) / **manual** (the actual tap) | With `link_url = https://golfin.io/campaign/august` the button was `interactable=True`; unassigned it was `False`. `ApplyBanner` and `OnBannerTapped` both gate through `BannerPolicy.IsLinkAllowed` and are unchanged by this task. Opening an external URL needs a device. |
| Home promo and Rankings banners are **unaffected** | PASS | `Canvas/ScreensRoot/HomeScreen/PromoBanner` found, active, enabled, drawing a runtime-downloaded sprite (i.e. the admin banner, not the bundled one). `GET /api/v1/banners` still serves `home_promo`. Rankings has no row in the DB — it keeps its bundled sprite, which is the unchanged baseline, not a regression. |
| EditMode suite still green, unmodified | PASS | `128 passed, 0 failed`. No test file was touched by this task. |

## Known FAIL items

None. Everything specced is built and verified except the seven dashboard-UI items below, which are
verifiable only by clicking the deployed panel.

## Manual verification still required

1. **The dashboard flow end to end (§6 — "the thing to demonstrate").** In `admin.golfin.world`:
   Banners → **New** → placement `Tournament — sign-up modal strip` → confirm schedule and sort
   order are hidden and the explanatory block appears → upload 970 × 252 art → **Active** → save.
   Then Tournaments → a tournament → **Artwork** tab → pick it → save → open that tournament's
   sign-up modal in game. It should appear **with no client rebuild** — that is the whole feature.
   *(I proved the same path at the data layer and captured the result, but by writing the row
   directly rather than clicking. My smoke row and its Storage object were deleted afterwards, so
   production carries no test data.)*
2. **The 400s**, by trying to save a tournament pointing at a `home_promo` banner.
3. **"Assigned to N tournaments"** after assigning one banner to two tournaments.
4. **The delete confirmation** naming the tournaments, on an assigned banner.
5. **The Audit Log** entry showing `modal_banner_id` in a `tournament_update` before/after.
6. **The tap-through** on device, to an allowlisted host.
7. **`[BannerArt] Cache HIT` on a second launch** (§6) — needs two cold launches on device.
8. **The corner/rim fix on device** at real DPI — verified in the editor at 1170 × 2532 by pixel
   measurement and a 3× corner crop, not on hardware.

## Test data left in production, deliberately

One `game_banners` row labelled **"SMOKE — tournament banner (safe to delete)"**
(`33cdcd47-6bd5-4a79-8c05-96fd95ac2529`) is active and assigned to `kasumigaseki_open`, with a
generated 970 × 252 placeholder in the `game-banners` bucket. It is there so the corner/rim fix is
visible when you open the modal. **Delete it from the Banners panel whenever you like** — the
tournament survives with `modal_banner_id = null` (that is the on-delete-set-null test, already
proven once and cleaned up). An earlier identical row and its Storage object were removed after
testing; this is the only test artefact remaining.

## Spec deviations

- **§2.1's exclusion is a Python-side filter, not a PostgREST `not.in` filter.** The spec says "add
  the exclusion explicitly"; it is explicit (`NEVER_AUTO_SERVED`, applied to `candidates` and again
  in the placement loop), just not in the query. Reason: `supabase==2.10.0` is not installed locally
  so I could not confirm the `.not_.in_()` builder syntax, and a filter-syntax mistake there would
  take the whole live banners endpoint down. The table is tiny, so the query-level filter bought
  nothing but risk. Exercised standalone: `tournament_modal` excluded, the other two unaffected.
- **The resolver reuses `BannerService.ResolveImageUrl` rather than restating the ladder.** §4.2 says
  "read it and match it, do not invent a second one" — calling it is strictly better than matching
  it, since the three placements now cannot drift. `expiresAtUtc` is passed `null` because a
  `tournament_modal` row has no window of its own. It is `internal static` in the same assembly, so
  no visibility change was needed.
- **`tournaments.py` deployed alongside your uncommitted `description_*` edit** from the previous
  task, because `fly deploy` ships the working tree. That code was already live, so nothing changed.

## Console output

```
No errors attributable to this task. The one warning path this task can produce is
deliberate and was exercised on purpose:

  [TournamentSignupModal] Refusing a modal banner URL outside the allowlisted Storage
  prefix for 'kasumigaseki_open'. Rendering the no-banner state.

EditMode: 128 passed, 0 failed (the 15 LogErrors in that run are the bad-data suites
that log on purpose, unchanged from the previous task).
```

## Open questions for Architect

1. **`rankings` has no banner row at all**, so that slot has only ever shown its bundled sprite. The
   regression bar says "Home promo and Rankings banners must still render" — Home does, from the
   admin; Rankings renders its bundled fallback. Worth creating one to prove the path, or is the
   fallback the intended steady state?
2. **The result / CLAIM modal `13894:3628`** shares this strip and is explicitly out of scope. The
   resolver is `static` and takes a `TournamentDefinition`, so it is directly reusable there — worth
   noting when that task is written.
