# Implementer Report — `game_banners`

## Implementation summary

Both banner images that ship inside the build — the Home promo strip and the Rankings banner —
are now admin-controllable end to end: a `public.game_banners` table, a no-auth
`GET /api/v1/banners` on playlife-api that does the whole `is_active` + schedule-window + sort
selection server-side, a Banners panel in `Tools/admin-dashboard`, and a Unity half that swaps the
sprite and opens an allowlisted external URL on tap.

**⚠️ Behaviour amended after Cesar's first look on device** (see SPEC's `⚠️ AMENDED` block and the
`## Post-deploy iteration` section below): with **no live banner the slot is HIDDEN and the UI
closes up**, rather than falling back to the bundled sprite. The bundled art stays in the scene as
an authoring placeholder only. A cached banner still serves an offline player; only "nothing to
show" hides the slot.

Nothing new was built where something existed. `TournamentArtService` was **parameterized, not
forked** (a private `(tag, cacheDirName, isAllowed)` ctor plus a second `Banners` instance), and
`TournamentArtPolicy.IsAllowed` was split into `IsAllowedUnder(url, allowedRoot)` with every check
moved across unchanged, so the size-capped downloader and the URL-normalizing allowlist are shared
rather than duplicated into a second security-critical path.

**Status: SHIPPED.** Cesar applied the migration 2026-08-17; both halves are deployed and verified
(`playlife-api` → `/api/v1/banners` 200 with the envelope on the bare path; `golfin-admin` version
`4cab1bbb`, `admin.golfin.world` 302 behind Access). A real banner has been created through the
panel and observed rendering in the Editor from the live endpoint.

## Files modified or created

| Path | Change |
|---|---|
| `playlife/backend/migrations/2026_08_17_game_banners.sql` | **created** — `game_banners` table, live index, per-column comments, RLS on with no policies, `service_role`-only grants, VERIFICATION block. **APPLIED to prod 2026-08-17 by Cesar.** |
| `Tools/admin-dashboard/migrations/2026_08_17_game_banners.sql` | **created** — byte-identical copy, filed next to the dashboard that writes the table (same convention as `admin_audit_log`). |
| `playlife/backend/routers/banners.py` | **created** — `GET ""`, no auth, one select + Python placement filter, `end_at` exclusive, unparseable bounds fail closed. |
| `playlife/backend/main.py` | modified — one import, one `include_router` at `/api/v1/banners`. |
| `Tools/admin-dashboard/lib/registry.ts` | modified — `"image"` added to the `PanelIcon` union; `banners` panel appended after Tournaments. |
| `Tools/admin-dashboard/components/PanelIcon.tsx` | modified — `image` icon (rect + circle + polyline, 24×24 stroke, existing style). |
| `Tools/admin-dashboard/lib/types.ts` | modified — `BannerPlacement`, `BannerState`, `BannerRow`, `BannerInput`, `BannersResponse`, following the `TournamentRow`/`TournamentInput` split. |
| `Tools/admin-dashboard/lib/banner.ts` | **created** — client-safe rules: `BANNER_PLACEMENTS`, `BANNER_ART_SPEC` (per-placement pixel targets), `deriveBannerState`, `validateBannerArtUrl`, `validateBannerLinkUrl`, `validateBannerInput`, `ALLOWED_LINK_HOSTS`. |
| `Tools/admin-dashboard/lib/bannerData.ts` | **created** — `server-only` read side, mock ↔ live branch, panel sort = the endpoint's resolution order. |
| `Tools/admin-dashboard/lib/bannerMutations.ts` | **created** — `server-only` create/update/delete/setActive/uploadBannerArt, every one audited, LIVE-deactivation typed-confirm guard. |
| `Tools/admin-dashboard/lib/mockBanners.ts` | **created** — three fixtures: one live row per placement plus an art-less draft. |
| `Tools/admin-dashboard/lib/mockStore.ts` | modified — `banners` added to `MockDb` and its seed. |
| `Tools/admin-dashboard/app/(panels)/banners/page.tsx` | **created** — server component, `force-dynamic`, `metadata.title`. |
| `Tools/admin-dashboard/app/(panels)/banners/banners-panel.tsx` | **created** — grouped by placement, LIVE/SCHEDULED/EXPIRED/OFF badge, thumbnail, one-click Activate/Deactivate. |
| `Tools/admin-dashboard/app/(panels)/banners/banner-editor.tsx` | **created** — modal editor: label, placement, EN + JA art with aspect warning, link URL with live validation, UTC window, sort order, active switch, delete. |
| `Tools/admin-dashboard/app/api/banners/route.ts` | **created** — `GET` list, `POST` create. |
| `Tools/admin-dashboard/app/api/banners/[id]/route.ts` | **created** — `PATCH` (edit or `setActive`), `DELETE`. |
| `Tools/admin-dashboard/app/api/banners/art/route.ts` | **created** — `POST` multipart upload. |
| `Assets/Scripts/BannersRuntime/BannerPolicy.cs` | **created** — art prefix, cache dir name, `IsArtAllowed` (delegates to the shared check), `IsLinkAllowed` + `AllowedLinkHosts`. |
| `Assets/Scripts/BannersRuntime/RemoteBannerDtos.cs` | **created** — Newtonsoft DTOs; `expires_at` kept a string. |
| `Assets/Scripts/BannersRuntime/RemoteBannerSource.cs` | **created** — fetch + atomic raw-body mirror to `game_banners.json`; null on any failure. |
| `Assets/Scripts/BannersRuntime/BannerService.cs` | **created** — `BannerPlacement`, `BannerDefinition`, the singleton, `TryGet`, `ResolveImageUrl`, `OnBannersChanged`, throttled `Refresh`, art warm + sweep. |
| `Assets/Scripts/BannersRuntime/BannerSlotBinder.cs` | **created, then reworked** — hides the slot when nothing is live (`Image.enabled` + `raycastTarget` + `LayoutElement.ignoreLayout`), grows every `_expandOnHide` target by the reclaimed height, defers revealing until the artwork decodes, gates the Button, `OpenLink` re-checks the allowlist. |
| `Assets/Scripts/TournamentsRuntime/TournamentArtPolicy.cs` | modified — `IsAllowed` body extracted to `internal static IsAllowedUnder(url, allowedRoot)`; every check moved across unchanged. |
| `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs` | modified — private `(tag, cacheDirName, isAllowed)` ctor, `Banners` instance, `Prefetch(IEnumerable<string?>)`; `SweepCore` takes the tag, `WriteCacheFile` became an instance method (it logs with the tag). |
| `Assets/Scripts/Net/Endpoints.cs` | modified — `Endpoints.Banners`. |
| `Assets/Scripts/UI/HomeScreenController.cs` | modified — `promoBannerImage` field; `OnPromoBannerClicked` delegates to the binder. |
| `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` | modified — `_bannerImage` + `_bannerButton` fields. `ApplyBanner()` untouched. |
| `Assets/Scenes/ShellScene.unity` | modified — Button + `ButtonPressFeedback` + `BannerSlotBinder` on `PromoBanner`, controller fields wired, `BannerService` on the `TournamentService` GameObject, `_bannerImage`/`_bannerButton` wired on the Rankings instance. |
| `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` | modified — Button + `ButtonPressFeedback` + `BannerSlotBinder` on `ContentArea/Banner`, persistent `onClick → BannerSlotBinder.OpenLink`. |
| `Assets/Scripts/TournamentsRuntime/Tests/BannerPolicyTests.cs` | **created** — 24 EditMode tests: art allowlist, link allowlist, resolution ladder, wire parsing. |
| `Docs/AI_CONTEXT.md`, `Docs/ADMIN_DASHBOARD_OPS.md`, `Tools/admin-dashboard/README.md` | modified — status, the finish-it runbook, the panel list. |

## Screenshot

- **Canonical screenshot:** `screenshots/home_banner_live_opaque.png` (1170×2532) — Home showing
  Cesar's real uploaded banner, fetched from the live endpoint, at **full opacity**. This is the
  frame that matters: it is the same slot that shipped translucent, and it proves both the
  `ColorTint` fix and the end-to-end server→device path in one image.
- **Also:** `screenshots/rankings_no_banner_fixed.png` (1170×2532) — Rankings with no live banner:
  tabs directly under the title, the panel grown into the reclaimed space (rows 4–11 instead of
  4–9), and the pinned YOU card still below the panel.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes — driven through the REAL entry points (`SplashScreen/StartButton.onClick`,
  then `HomeScreen/LeaderboardButton.onClick` / `NavHomeButton.onClick`), never a direct `ShowScreen`.

The two earlier `*_bundled_fallback.png` frames were deleted: they recorded the superseded
bundled-sprite behaviour and would now read as the expected result.

## Acceptance checklist

### Backend

| Item | Result | Justification |
|---|---|---|
| Migration applied to prod and verified by dumping the column list over PostgREST | **PASS** | Cesar applied it 2026-08-17; verified over PostgREST with the service key: table returns `200 []`, all 12 columns resolve BY NAME, anon key `401`, and a `placement='home_banner'` insert is rejected `23514`. Original blocker (I cannot run DDL — no PostgREST DDL path, no connection string, no `psql` on this Mac. Probed with the service key: `GET /rest/v1/game_banners?limit=1&select=*` → **404 `PGRST205` "Could not find the table 'public.game_banners'"**. The migration file is written and ready to paste. |
| `GET /api/v1/banners` returns 200 with the `{"data":{"fetched_at","banners"}}` envelope | **PASS** | Deployed. Bare path **200** with the envelope, trailing slash **307** (the acceptable direction), `/health` 200; a real row came back and rendered on device. Formerly blocked: Deploying a router that selects from a non-existent table would 500 the endpoint (§3.2), so `fly deploy` was deliberately not run. Live play-mode boot confirms the client's designed reaction: `[Banners] Banner fetch failed (NotFound, HTTP 404) … Keeping the cached/bundled banners.` |
| Two active `home_promo` rows at different `sort_order` → exactly one returned, the higher | **PARTIAL** | The endpoint is live and returns exactly one `home_promo` entry, but only ONE active row exists, so the tie-break has not been exercised against real data. Ordering is `.order("sort_order", desc=True).order("created_at", desc=True)` with `break` on the first live row per placement; the dashboard's own listing uses the same order and was observed returning `sort_order` 20 → 10 → 0. |
| Future `start_at` absent; past `end_at` absent | PASS (logic), **FAIL (endpoint)** | `_is_live` was unit-checked against the real `routers/banners.py` (imported with stubbed `fastapi`/`supabase`/`config`, so the production function ran): 9/9 cases correct, including `end_at` exclusive at the instant, a `+09:00` offset resolving to the right UTC instant, and unparseable bounds failing closed. Not yet observable over HTTP. |
| `is_active = false` → absent | PASS (logic), **FAIL (endpoint)** | `.eq("is_active", True)` is in the select itself. Same blocker. |

### Dashboard

Verified against an **isolated** `MOCK_MODE=1` dev server (the app rsynced to the scratchpad with
its own `.next`, port 3055) so the `next dev` already running on Cesar's copy was never touched —
§4.1 says a second process against a shared `.next` is how the dashboard gets broken.

| Item | Result | Justification |
|---|---|---|
| Banners appears in the sidebar after Tournaments; panel loads with `MOCK_MODE=1` | PASS | `PANELS` order is Users, Points, Tournaments, **Banners**, Audit Log. `GET /banners` → **200**, page title "Banners"; `GET /api/banners` returned all three fixtures. |
| Upload writes a content-hashed name; re-uploading the same bytes yields the same URL | PASS | Same 11 813-byte PNG uploaded twice → `home_promo-en-a63239b29b3d.png` **both times**; the same bytes under `locale=ja` → `home_promo-ja-a63239b29b3d.png`, i.e. the name is `{placement}-{locale}-{sha256[:12]}.{ext}` as specified. |
| A >500 KB file and a `.gif` are both rejected client-side with a readable message | PASS | 1 960 918-byte PNG → 400 `"Image is 1915 KB — the cap is 500 KB. Every mobile player downloads this."`; `image/gif` → 400 `"Unsupported type \"image/gif\". Use JPG, PNG or WebP."` The editor applies the identical checks before the request, so the message appears without a round trip. |
| A `link_url` on an off-allowlist host is rejected on save | PASS | `https://evil-golfin.io/x` → 400 naming the four allowed hosts. The pure helper was also exercised directly: it refuses `http://golfin.io`, `https://evil-golfin.io`, `https://golfin.io.attacker.net`, `https://golfin.io:8443`, `https://a@golfin.io`, and accepts `https://golfin.io/x` and `https://www.golfin.world/y`. |
| Create / update / delete / activate each write one `admin_audit_log` row with before/after | PASS | Read back from the Audit Log API, not assumed: `banner_create` 1, `banner_update` 1, `banner_delete` 1, `banner_activate` 1, `banner_deactivate` 1, `banner_art_upload` 3. Update/delete/activate/deactivate all carry a non-null `before`; create carries `before=null, after=snapshot`. |
| Deactivating a LIVE banner requires the typed confirmation | PASS | On the LIVE fixture: no `confirmLabel` → **409**; wrong label → **409**; `"August GPS campaign"` → **200**. Re-activating needs no confirmation (reversible in the same click). Activating the art-less draft → **400**. |
| Post-deploy `curl https://admin.golfin.world/` → 302 | **PASS** | Deployed, version `4cab1bbb`. `/` and `/banners` both **302**; `cf-deploy.sh` logged *"bundle carries no service_role key"* and restored the env file. |

### Client — EditMode tests

| Item | Result | Justification |
|---|---|---|
| `BannerPolicy.IsArtAllowed` refuses http / wrong host / `user@host` / explicit port / bucket root / `..` traversal / `%2e%2e`; accepts a well-formed object URL | PASS | `BannerArtAllowlistTests` — a 10-row reject table plus 4 traversal forms plus null, and three accepted object URLs including a nested path. Also asserts the cache dir differs from `tournament-art` (shared dirs would let the two 50 MB budgets evict each other) and that `IsAllowedUnder` still exists, so a future copy-paste of the check fails the suite. |
| `BannerPolicy.IsLinkAllowed` refuses the five named forms; accepts `https://golfin.io/x` and `https://www.golfin.world/y` | PASS | `BannerLinkAllowlistTests` — all four allowed hosts accepted, all five named rejects plus `sub.golfin.io`, a scheme-less string, `javascript:`, empty and null refused. |
| Resolution ladder: JP + `image_url_ja` null → `image_url_en`; both null → no banner; `expires_at` past → no banner | PASS | `BannerResolutionLadderTests` — 9 tests over the production `BannerService.ResolveImageUrl`, the exact function `TryGet` calls: both language directions, both cross-locale fallbacks, empty-string-as-absent, expiry past / at the instant (exclusive) / future / absent. |
| **Every pre-existing `TournamentArtPolicy` / `TournamentArtService` test still passes, unmodified** | PASS | `RemoteScheduleTests.cs`, `ScheduleRefreshTests.cs`, `TournamentServiceWireupTests.cs` are byte-unchanged (`git status` shows them untracked-clean). EditMode namespace `Golfin.Tournaments.WireupTests`: **115 passed / 0 failed / 0 skipped**. |

**Proof the new suite actually ran** (per the standing "tests-run hides passes" lesson): a
`BannerTripwireTests` with a bare `Assert.Fail` was added → the run went **1289 total, 1 failed**,
naming the tripwire → the file was deleted → back to **1288 total, 115 passed, 0 failed**.

### Client — on device (manual, human-in-the-loop)

Both halves are deployed and a real banner now renders from the live endpoint, so most of these are
closed. What remains genuinely needs a device build.

| Item | Result | Justification |
|---|---|---|
| Home shows the uploaded EN promo image; tapping opens the browser at the link | **PARTIAL** | Image half **PASS** — Cesar's uploaded banner renders from the live endpoint at full opacity (`screenshots/home_banner_live_opaque.png`). Tap half UNPROVEN: his row has `link_url = null`, so the Button is correctly non-interactable and no tap path has fired. The wiring is verified: `promoBannerButton` → `OnPromoBannerClicked` → `BannerSlotBinder.OpenLink`, which re-checks `IsLinkAllowed` before `Application.OpenURL`. |
| Switching the language to Japanese swaps to the JA image without leaving the screen | **UNPROVEN** | His row has `image_url_ja = null`, so there is nothing to swap TO. The binder subscribes to `LocalizationManager.OnLanguageChanged` in `OnEnable` and both locales are prefetched (not only the current one), so the swap needs no download. Unverifiable without a live row. |
| Deactivate in the dashboard → relaunch → the bundled GPS sprite is back, no gap, no error | **PASS in the equivalent state** | Today's build IS the "nothing live" state, and it was observed in play mode: `BannerService.Source=None`, `TryGet(HomePromo)=False`, `TryGet(Rankings)=False`, `PromoBanner.sprite = "GPS Banner"`, `Button.interactable=False`, Rankings `sprite = "Banner"`. Still needs the live→deactivated transition once the endpoint exists. |
| Cold launch in airplane mode → both slots show bundled art, Console has warnings only | **PASS in the equivalent state** | An unreachable endpoint is the same code path as no network. Console for the session: **0 errors / 0 exceptions**, one warning — `[Banners] Banner fetch failed (NotFound, HTTP 404): Not Found. Keeping the cached/bundled banners.` |
| Second launch logs `Cache HIT … no download` from `[BannerArt]` | **UNPROVEN** | A real object is now in the bucket and was downloaded, so the next cold launch should log it — not yet observed across two launches. The log line is shared with the tournament path, which has already been observed doing exactly this in production. |
| Rankings banner behaves the same for its placement | **PASS for the fallback half** | `screenshots/rankings_bundled_fallback.png`, reached through the real Home → Leaderboard button. Remote half blocked. |

### Always

| Item | Result | Justification |
|---|---|---|
| All `[SerializeField]` references wired in the Inspector | PASS | Read back after a scene reload, not assumed. Home: binder `_placement=0 (HomePromo)`, `_image=PromoBanner (Image)`, `_button=PromoBanner (Button)`; `promoBannerButton` + `promoBannerImage` set; `promoBannerText` + `gpsIcon` **unassigned** per D2. Rankings: binder `_placement=1`, `_image`/`_button` set, `onClick` persistent call count 1 → `Banner (BannerSlotBinder).OpenLink`; controller `_bannerImage`/`_bannerButton` set. `BannerService` present on the `TournamentService` GameObject. |
| `ShellScene.unity` diff contains only the changes named in §4.3 | PASS | `git diff --stat` → **116 insertions, 1 deletion** (the deletion is `promoBannerButton: {fileID: 0}` becoming the real reference). Filtering the diff for anything outside the new component blocks leaves only the four `--- !u!114 &…` anchors. No `m_IsActive`, no `sizeDelta`, no position changes. See § Spec deviations for the four TMP auto-size lines the save baked in and how they were removed. |
| Unity Console has no errors related to this task | PASS | Play-mode session log: 100 entries, **0 Error / 0 Exception / 0 Assert**. The only banner line is the expected 404 warning. |
| Spec deviations flagged | PASS | Below. |

## Post-deploy iteration — 2026-08-17 (Cesar's first look on device)

Two defects reported against the deployed build. Both fixed and measured; SPEC now carries an
`⚠️ AMENDED` block recording the behaviour change.

### 1. Banner rendered translucent — REGRESSION I INTRODUCED

**Cause:** I added a `Button` to each slot and set `interactable = false` so an unlinked banner
would not be tappable, but left the default **`ColorTint`** transition. Unity's `Selectable` then
paints the target graphic with `disabledColor` = `RGBA(0.784, 0.784, 0.784, 0.502)` — grey at 50%
alpha. Measured on both slots; `Image.color` itself was still `a=1`, which is why it read as an
asset problem.

**Fix:** `transition = None` on both Buttons. `ButtonPressFeedback` already supplies press feedback,
so the tint was buying nothing. Verified live: `Image.color = RGBA(1,1,1,1)`, `transition = None`,
served artwork fully opaque — `screenshots/home_banner_live_opaque.png`.

### 2. No live banner should hide the slot and let the UI close up

Supersedes SPEC §4.2's bundled-sprite fallback (see the AMENDED block). Cesar, on the offline case:
*"Last cached banner. If no banner present, same as no banner."* — the disk cache still serves an
offline player (that was already `Awake`'s behaviour); only "nothing to show" hides the slot.

The binder now hides via `Image.enabled = false` + `raycastTarget = false` +
`LayoutElement.ignoreLayout = true`, and defers revealing until the artwork has actually **decoded**
so the authoring placeholder never flashes.

**Two wrong turns worth recording:**

- I first grew only `RankingsArea`. Cesar caught both symptoms immediately: *"ranking panel did not
  grow to occupy the banner space and the YOU card is now over the rankings panel instead of below
  it."* Cause: `Modal` runs its own `VerticalLayoutGroup` whose content (1473) deliberately
  **overflows** the 1273 panel by 200px, and that overflow is what holds the pinned card below the
  panel. Growing the panel alone absorbed the overflow. Fix: grow `Bottom97` by the same amount, so
  the overflow is preserved exactly.
- I then tried to re-anchor `Bottom97` / `RankingsCardUser` in the prefab and wrote bad offsets —
  the values I had measured were **layout-group-driven at runtime**, not the authored ones, so the
  conversion math was against the wrong baseline. Caught immediately, restored from the values
  logged in the same call, and confirmed with `git diff`: **zero** `m_Anchor*` / `m_SizeDelta` /
  `m_Pivot` lines in the prefab diff. Lesson: never derive an anchor conversion from play-mode
  values when a `LayoutGroup` is driving the child.

**Measured result** (play mode, real navigation, `GetWorldCorners`):

| State | Panel | List | YOU card worldY | Card vs panel bottom |
|---|---|---|---|---|
| Banner shown | 1285 | 776 | 305..481 | 24px below — outside |
| Banner hidden | 1561 (+276) | 1052 (+276) | 305..481 | 24px below — outside |
| `Apply()` again | 1561 | 1052 | 305..481 | 24px below — outside |

The YOU card does not move between states, the list gains ~2 rows, and repeated `Apply()` is
idempotent. Evidence: `screenshots/rankings_no_banner_fixed.png`.

**Diff hygiene:** `ShellScene.unity` remains 116 insertions / 1 deletion, only the banner component
blocks. `RankingsScreen.prefab` carries **no** RectTransform layout changes — the growth is
runtime-only, so the authored state stays the with-banner layout.

**One bug found in my own new code and fixed:** `SetExpanded` cached the base heights on a null
check alone, so a length change between the cache and the serialized array threw
`IndexOutOfRangeException`. Now guarded on length as well.

## Spec deviations

1. **`SweepCore` gained a `tag` parameter and `WriteCacheFile` became an instance method.**
   §4.1 named three hard-coded references to replace; these two are static methods that also
   logged with `Tag`, which stops compiling the moment `Tag` becomes per-instance. Nothing else
   in the project calls either (`grep` over `Assets/Scripts`: only `TournamentService` touches
   `Prefetch`/`SweepCacheAsync`), and behaviour is unchanged — the log line now carries
   `[BannerArt]` or `[TournamentArt]` instead of a constant.

2. **The resolution ladder was extracted to `internal static BannerService.ResolveImageUrl`.**
   §4.2 puts the ladder in `TryGet`. `TryGet` is a MonoBehaviour method reading the live
   `LocalizationManager` and clock, so testing it in EditMode would mean instantiating the
   component and reaching into private state. `TryGet` now supplies language + clock and calls
   the pure function; the ladder is identical and the tests exercise the production code rather
   than a copy of it. Same reasoning that put `ScheduleRefreshThrottle` in its own class.

3. **`Endpoints.Banners` added to `Assets/Scripts/Net/Endpoints.cs`.** Not in §8's file list, but
   §4.1 names `Golfin.Net.Endpoints` as a reuse target and `RemoteBannerSource` needs the URL from
   somewhere. One property, matching `TournamentsGolfin` in shape and doc style.

4. **The Rankings tap is a persistent `onClick` on the Button; the Home tap goes through the
   controller.** §4.2 says the binder handles the click and §4.3 says `OnPromoBannerClicked` stays
   the `onClick` target and delegates to the binder. If the binder ALSO added a runtime listener,
   one tap on Home would open the browser twice. So the binder never adds a listener: it exposes
   `OpenLink()`, Home reaches it through the controller, and Rankings reaches it through an
   inspector-visible persistent call.

5. **`ButtonPressFeedback` added to both new Buttons.** Not in the spec; `CLAUDE.md` hard rule 11
   requires it on every new player-facing Button, at stock defaults (`_pressedScale 0.95`,
   `_duration 0.12`).

6. **Four TMP auto-size lines were reverted by hand after the scene save.** Saving `ShellScene`
   baked `m_fontSize: 19.85 → 19.95` on four unrelated text objects — the known scene-save layout
   churn, nothing to do with this task. The four lines were patched back at their exact line
   numbers and the scene reloaded in the Editor (clean, not dirty), leaving the diff at
   116 insertions / 1 deletion.

7. **`BannerService` uses `DateTime.UtcNow` for the expiry check**, not `NetworkTimeProvider` (the
   seam `TournamentService` uses for its clock). The spec says `now >= expires_at` without naming a
   source. Flagging it because a device with a badly skewed clock could show or hide a banner an
   hour early; the consequence is cosmetic, and the alternative would pull a tournament dependency
   into `BannersRuntime` for that.

## Console output

```
[Banners] Banner fetch failed (NotFound, HTTP 404): Not Found. Keeping the cached/bundled banners.
```

That is the entire banner-related output for the play-mode session: one warning, zero errors —
the designed degradation while the endpoint does not exist yet.

## Open questions for Architect

1. **The link-host allowlist needs confirming before a real campaign ships** (SPEC §5.2 already
   flags this). Currently `golfin.io`, `www.golfin.io`, `golfin.world`, `www.golfin.world`. It is
   compiled into the build, so a marketing host, a Notion/Typeform page or a partner domain is a
   client release, not a dashboard change. If campaign pages will not live on `golfin.io`, this
   list is wrong today and every banner link will silently do nothing on device.

2. **`label` uniqueness is not enforced.** The LIVE-deactivation and delete guards ask the operator
   to re-type the label, which is a weaker key than the tournament panel's `slug` (unique by
   index). Two banners sharing a label would make the typed confirmation ambiguous. Deliberate —
   §1's schema has no unique constraint on `label` and adding one was not in scope — but worth a
   decision if the panel grows past a handful of rows.
