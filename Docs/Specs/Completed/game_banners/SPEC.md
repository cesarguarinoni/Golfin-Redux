# SPEC — `game_banners`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. `STATUS.md` tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. Current: `SPEC_READY`.

## Goal

Two banner images ship inside the build today and cannot be changed without a store release:
the **Home promo strip** (`Assets/Art/HomeScreen/GPS Banner.png`) and the **Rankings screen
banner** (`Assets/Art/RankingsScreen/Banner.png`). This task puts both under admin control —
a new **Banners** panel at `admin.golfin.world/banners` writes a `public.game_banners` row,
a new no-auth `GET /api/v1/banners` on playlife-api serves what is live, and the client swaps
the bundled sprite for the served image and opens an allowlisted external URL on tap.

The bundled sprites stay in the build and remain the fallback. **A player with no network, an
expired banner, or nothing scheduled sees exactly what they see today.** Nothing here can make
a slot go blank.

## ⚠️ AMENDED after first look on device (Cesar, 2026-08-17, post-deploy)

Two changes to what is written below. They supersede the original text wherever they conflict.

| # | Amendment | Supersedes |
|---|---|---|
| A1 | **No live banner ⇒ the slot is HIDDEN and the UI closes up.** The bundled sprite is NOT a runtime fallback; it stays in the scene purely as an authoring placeholder and is never shown to a player. The *cached* banner still serves an offline player — only "nothing to show" hides the slot. | The Goal paragraph and §4.2's *"No banner always means the bundled sprite stays on screen. There is no empty state."* |
| A2 | The added `Button` must use **`transition = None`**. `ColorTint` + `interactable = false` paints the target graphic with `disabledColor` (grey, **alpha 0.502**) — the translucent banner Cesar reported. `ButtonPressFeedback` already supplies the press feel. | §4.3 (implicit — the spec did not name a transition) |

Reference frames for A1: Figma `13027-5212` (Home, no banner) and `4079-1727` (Rankings, no banner).

**What "the UI adapts" means per screen**, measured rather than assumed:

- **Home** — `PromoBanner` is bottom-anchored with no parent layout group and nothing positioned
  relative to it, so `Image.enabled = false` is the entire adaptation.
- **Rankings** — `ContentArea` has a `VerticalLayoutGroup` (spacing 24), so `LayoutElement.ignoreLayout`
  reclaims **276px** (252 banner + 24 spacing). That is not sufficient on its own: `RankingsArea` is a
  fixed 1285px panel, and `Modal` inside it runs its own `VerticalLayoutGroup` whose content
  (457 + 776 + 176 + 48 spacing + 16 padding = **1473**) deliberately **overflows** the 1273px panel by
  200px — and that overflow is precisely what holds the pinned `RankingsCardUser` *below* the panel.
  Growing only the panel absorbs the overflow and swallows the card. So **both** `RankingsArea` and
  `Bottom97` (the list) grow by the same 276px, which preserves the 200px overflow exactly, keeps the
  YOU card motionless, and gives the reclaimed height to the scroll list (≈2 more rows).

## Decisions of record (Cesar, 2026-08-17)

| # | Decision | Consequence |
|---|---|---|
| D1 | **Two auto-served placements:** `home_promo`, `rankings` (plus `tournament_modal`, assigned — see D7) | The Home news/announcement carousel (`NoticePanel`, `PageDots`) is explicitly OUT of scope. |
| D2 | **Image per locale, no text fields** | Row carries `image_url_en` + `image_url_ja`. Copy is baked into the artwork. The Home slot's `promoBannerText` / `gpsIcon` fields stay unassigned. |
| D3 | **One live banner per placement** | The endpoint returns at most one row per placement. No carousel, no dots, no rotation timer on either screen. |
| D4 | **Tap opens an external URL** | `Application.OpenURL`, gated by a client-side host allowlist (D5). |
| D5 | **Host allowlist in the client** | Mirrors `TournamentArtPolicy`: parse first, then compare scheme/host/path on the normalized `Uri`. Never a raw `StartsWith`. |
| D6 | **The game reads playlife-api, not Supabase** | New router `backend/routers/banners.py`, mounted `/api/v1/banners`. Needs a `fly deploy` (app `playlife-api`, region nrt). |
| D7 | **Tournament banners are created here, assigned in Tournaments** (Cesar, 2026-08-17, later) | A third placement `tournament_modal`. Rows with that placement are **not** served by `GET /api/v1/banners` — a tournament points at one via `tournaments.modal_banner_id`, picked from a dropdown in the tournament editor, and `GET /tournaments/golfin` joins it. See §9. |

## Reference

No Figma frame — this task changes no layout. Both slots keep their existing
`RectTransform`, size and position; only the `Sprite` on the existing `Image` changes,
plus a `Button` added to each so the slot can be tapped.

| Slot | Scene / prefab path | Component today | Bundled sprite |
|---|---|---|---|
| `home_promo` | `Canvas/ScreensRoot/HomeScreen/PromoBanner` (`Assets/Scenes/ShellScene.unity`) | `Image` only — **no `Button`, no children** | `Assets/Art/HomeScreen/GPS Banner.png` |
| `rankings` | `RankingsScreen/ContentArea/Banner` (`Assets/Prefabs/UI/Rankings/RankingsScreen.prefab`) | `Image` only, toggled by `RankingsScreenController._banner` / `_showBanner` | `Assets/Art/RankingsScreen/Banner.png` |

⚠️ **Verified trap.** `HomeScreenController`'s `promoBannerButton`, `promoBannerText` and
`gpsIcon` are all `{fileID: 0}` in `ShellScene.unity` — **unassigned**. `OnPromoBannerClicked`
has therefore never run in a build; the strip is a dead image. Do not assume the wiring exists.

⚠️ `Canvas/ScreensRoot/TournamentLeaderboardScreen/ContentArea/Banner` uses the *same* sprite
but is a different object with sponsor/name pills bound by
`TournamentLeaderboardScreenController` (`SponsorLabelPath`, `TournNameLabelPath`). **Out of
scope. Do not touch it.**

## Architecture context

- **Asmdef boundaries:** all new runtime code lands in **Assembly-CSharp** (new folder
  `Assets/Scripts/BannersRuntime/`, no `.asmdef` — same arrangement as
  `Assets/Scripts/TournamentsRuntime/`). It may reference `Golfin.Net` (`autoReferenced: true`)
  and Newtonsoft. It must **not** be added to the `Golfin.Tournaments` asmdef
  (`Assets/Scripts/Tournaments/`), which is deliberately dependency-light and must never learn
  a network exists.
- **Existing code reused (do not duplicate):**
  - `Golfin.Tournaments.TournamentArtService` — `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs`. The size-capped, redirect-refusing, LRU-swept image downloader. **Parameterize it (§4.1); do not fork it.**
  - `Golfin.Tournaments.TournamentArtPolicy` — same folder. URL normalization + cache-key derivation.
  - `Golfin.Tournaments.RemoteTournamentSource` — the fetch + raw-body-to-disk pattern `RemoteBannerSource` copies in shape.
  - `Golfin.Tournaments.ScheduleRefreshThrottle` (`public sealed`, ctor `(double cooldownSeconds = 60.0)`) — reuse verbatim for the banner refetch cooldown.
  - `Golfin.Net.ApiClient.Instance.Get<string>(url, cb)` / `ApiResult<T>.RawBody` / `Golfin.Net.Endpoints`.
  - `LocalizationManager.CurrentLanguage` (`Assets/Localization/LocalizationManager.cs`, `Language.English` / `Language.Japanese`) and `LocalizationManager.OnLanguageChanged`.
- **Dashboard code reused:** `lib/auth.ts::checkAdmin`, `lib/audit.ts::writeAudit`,
  `lib/mode.ts::isMockMode`, `lib/supabaseAdmin.ts::getSupabaseAdmin`, `lib/registry.ts`,
  `components/PanelIcon.tsx`. The Tournaments panel's `ArtworkTab`
  (`app/(panels)/tournaments/tournament-editor.tsx:819`) and
  `uploadTournamentArt` (`lib/tournamentMutations.ts:458`) are the templates for the upload flow.
- **Backend reused:** `backend/routers/tournaments.py::list_golfin` is the shape to copy —
  service-key Supabase client, `{"data": {...}}` envelope written by hand, no auth.

---

## 1. Schema — `playlife/backend/migrations/2026_08_17_game_banners.sql`

**Migration first, deploy second. Always** (`Docs/ADMIN_DASHBOARD_OPS.md` §3.2). You cannot run
DDL yourself: write the file, hand Cesar the SQL for the Supabase SQL editor, and verify the
columns landed via PostgREST before deploying anything that reads them.

```sql
create table if not exists public.game_banners (
  id           uuid primary key default gen_random_uuid(),
  placement    text not null check (placement in ('home_promo', 'rankings', 'tournament_modal')),
  label        text not null,
  image_url_en text,
  image_url_ja text,
  link_url     text,
  start_at     timestamptz,
  end_at       timestamptz,
  sort_order   integer not null default 0,
  is_active    boolean not null default false,
  created_at   timestamptz not null default now(),
  updated_at   timestamptz not null default now()
);

create index if not exists game_banners_live_idx
  on public.game_banners (placement, is_active, sort_order desc, created_at desc);

alter table public.game_banners enable row level security;
revoke all on table public.game_banners from anon;
revoke all on table public.game_banners from authenticated;
grant select, insert, update, delete on table public.game_banners to service_role;
```

Follow the header/comment/VERIFICATION style of
`Tools/admin-dashboard/migrations/2026_08_13_admin_audit_log.sql` and
`playlife/backend/migrations/2026_08_17_tournaments_is_active.sql`. Add a
`comment on column` for at least `placement`, `is_active` and the two image columns.

Notes that belong in the migration's own comments:

- `label` is **admin-only** — a name so the row is findable in the panel. It is never sent to
  the client and never rendered to a player.
- `is_active` defaults to **false**, unlike `tournaments.is_active`. Tournaments defaulted true
  because rows already existed and had to stay visible; a banner row is new, and saving a draft
  must not publish it to every player mid-edit.
- RLS on with no policies: only `service_role` reads it, and both readers (the dashboard and
  the FastAPI app) use the service key.

Drop the same file into `Tools/admin-dashboard/migrations/` so it sits next to the dashboard
that writes the table, matching how `admin_audit_log` is filed.

## 2. Backend — `GET /api/v1/banners`

New file `playlife/backend/routers/banners.py`. Mount it in `backend/main.py`:

```python
from routers import ..., banners
app.include_router(banners.router, prefix="/api/v1/banners", tags=["Banners"])
```

Single route, `@router.get("")`, no auth (same posture as `/tournaments/golfin` and
`/tournaments/active` — the schedule is public and must warm at boot before any token work).

**Selection is server-side, and covers the two auto-served placements only.** Rows with
`placement = 'tournament_modal'` are **excluded from this endpoint entirely** — they reach the
client through `GET /tournaments/golfin`, joined onto the tournament that points at them (§9).
For each of `home_promo` and `rankings`, take rows where

```
is_active = true
and (start_at is null or start_at <= now())
and (end_at   is null or end_at   >  now())
```

ordered `sort_order desc, created_at desc`, and keep the **first**. Placements with nothing
live are simply absent from the array.

Response — one entry per live placement, at most one per placement:

```json
{"data": {
  "fetched_at": "2026-08-17T04:00:00+00:00",
  "banners": [
    {"placement": "home_promo",
     "image_url_en": "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/home_promo-en-a1b2c3d4e5f6.jpg",
     "image_url_ja": null,
     "link_url": "https://golfin.io/campaign/august",
     "expires_at": null}
  ]
}}
```

`expires_at` is the chosen row's `end_at`, verbatim (may be null). It exists for one reason:
the client mirrors this body to disk, so a cached banner whose window has closed while the
player was offline must be dropped client-side. Without it the client would need the whole
scheduling rule.

Do the placement filter in Python over one `select` of the candidate rows rather than one query
per placement — there are two placements and the table is tiny.

Deploy: `cd /Users/cesar/Documents/playlife/backend && export PATH="$HOME/.fly/bin:$PATH" && fly deploy`.
⚠️ `fly deploy` runs longer than the 60s tool timeout — launch it with `nohup … &`, sleep, then
poll the log (`Docs/ADMIN_DASHBOARD_OPS.md` §4.6).

Verify before touching the client:

```
curl -s https://playlife-api.fly.dev/api/v1/banners  | head -c 400
curl -s -o /dev/null -w "%{http_code}\n" https://playlife-api.fly.dev/api/v1/banners
curl -s -o /dev/null -w "%{http_code}\n" https://playlife-api.fly.dev/api/v1/banners/
```

Both must be 200, **not 307** — an empty-path route under a prefix is the one place FastAPI can
surprise you with a redirect, and `RemoteBannerSource` must not depend on redirect following.
If the trailing-slash form redirects, that is acceptable; the bare form redirecting is not.

## 3. Admin dashboard — the Banners panel

Read `Docs/ADMIN_DASHBOARD_OPS.md` §2 and §4 before running anything in
`Tools/admin-dashboard`. In particular: never `next build` while `next dev` is running (§4.1),
and start dev with `NODE_ENV=development npm run dev` (§4.2).

### 3.1 Files

| File | Change |
|---|---|
| `lib/registry.ts` | Add `"image"` to the `PanelIcon` union; append `{ id: "banners", title: "Banners", icon: "image", route: "/banners" }` **after** Tournaments. |
| `components/PanelIcon.tsx` | Add an `image` entry to `PATHS` (a 24×24 stroke icon in the existing style — rect + circle + polyline is fine). |
| `lib/types.ts` | NEW `BannerPlacement`, `BannerRow`, `BannerInput`, `BannersResponse`. Follow the `TournamentRow` / `TournamentInput` split exactly: `Row` is what the panel renders, `Input` is what create/update accept over the wire. |
| `lib/banner.ts` | NEW. Pure helpers: `BANNER_PLACEMENTS`, `deriveBannerState()`, `validateBannerInput()`, `validateBannerArtUrl()`, `validateBannerLinkUrl()`, `BANNER_ART_SPEC`. |
| `lib/bannerData.ts` | NEW, `import "server-only"`. Read side, mock ↔ live branch, mirroring `lib/tournamentData.ts`. |
| `lib/bannerMutations.ts` | NEW, `import "server-only"`. `createBanner`, `updateBanner`, `deleteBanner`, `setBannerActive`, `uploadBannerArt`. Every one audited. |
| `lib/mockBanners.ts` + `lib/mockStore.ts` | NEW fixtures — one row per placement so the panel is exercisable with `MOCK_MODE=1`. |
| `app/(panels)/banners/page.tsx` | Server component, `export const dynamic = "force-dynamic"`, `metadata.title = "Banners — GOLFIN Admin"`. Mirrors `app/(panels)/tournaments/page.tsx` verbatim in shape. |
| `app/(panels)/banners/banners-panel.tsx` | `"use client"`. List grouped by placement, LIVE / SCHEDULED / EXPIRED / OFF badge, thumbnail, Create / Edit / Activate / Delete. |
| `app/(panels)/banners/banner-editor.tsx` | Modal editor: label, placement, EN art, JA art, link URL, start/end (UTC, same `toLocalInput` / `fromLocalInput` helpers the tournament editor uses), sort order, active toggle. |
| `app/api/banners/route.ts` | `GET` list, `POST` create. |
| `app/api/banners/[id]/route.ts` | `PATCH` update, `DELETE`. |
| `app/api/banners/art/route.ts` | `POST` multipart upload. |

### 3.2 Non-negotiables (these are what the existing panels already do)

- **Every** route handler opens with `const check = await checkAdmin(); if (!check.ok) …`.
- **Every** mutation calls `writeAudit(check.email, action, null, "game_banners", before, after)`
  on its success path. Actions: `banner_create`, `banner_update`, `banner_delete`,
  `banner_activate`, `banner_deactivate`, `banner_art_upload`.
- Mock branch on every read and write, so the whole panel works on fixtures with no secrets.
- `export const dynamic = "force-dynamic"` on every route file.

### 3.3 Storage

Bucket **`game-banners`** — public, created on first use exactly as `uploadTournamentArt` does
(`listBuckets` → `createBucket` if absent), `fileSizeLimit` = the cap below,
`allowedMimeTypes` = the list below.

```
BANNER_ART_SPEC = {
  mimeTypes: ["image/jpeg", "image/png", "image/webp"],
  maxBytes: 500 * 1024,        // same ceiling as ART_SPEC — every mobile player downloads this
}
```

Object name: `` `${placement}-${locale}-${sha256(bytes).slice(0,12)}.${ext}` ``, `upsert: true`,
`cacheControl: "31536000"`. **Content-hashed and immutable, like tournament art** — replacing an
image produces a new URL, so the client's URL-keyed disk cache needs no invalidation story at
all. Do not reuse a stable name.

After `getPublicUrl`, run `validateBannerArtUrl` on the result and fail with 500 if Storage
returned something off-host.

### 3.4 Validation (server side, in `validateBannerInput` — one gate both create and update use)

- `label`: 1–80 chars after trim.
- `placement` ∈ `BANNER_PLACEMENTS`.
- At least one of `image_url_en` / `image_url_ja` is set **when `is_active` is true**. A draft may
  have neither; a live banner with no art is a slot that silently does nothing.
- Any non-null image URL passes `validateBannerArtUrl` (this project's Storage host, inside the
  `game-banners/` bucket).
- `link_url`, when set, passes `validateBannerLinkUrl` (§5.2 allowlist). Null is valid — a banner
  with no link is informational and the client leaves the button non-interactable.
- `end_at > start_at` when both are set.
- `sort_order` is an integer in −999…999.
- Flipping `is_active` true→false on a row that is currently LIVE requires a typed confirmation
  in the editor, the same way editing an Open tournament requires `confirmSlug`. It is player-facing
  and instant.

### 3.5 Aspect guidance in the editor

The editor shows the bundled sprite's real dimensions as the per-placement target and warns on
drift like `ArtworkTab` does (`drift > tolerance` → amber, **never a block**), and states the byte
cap in the help text. Measured from the shipped PNGs, 2026-08-17:

| Placement | Bundled sprite | Pixels | Aspect |
|---|---|---|---|
| `home_promo` | `Assets/Art/HomeScreen/GPS Banner.png` | **1010 × 292** | 3.46 |
| `rankings` | `Assets/Art/RankingsScreen/Banner.png` | **970 × 252** | 3.85 |

Put these in `BANNER_ART_SPEC` keyed by placement. Re-measure rather than trusting this table if
either PNG has been replaced since.

Deploy: `npm run deploy`, then the §2 verification from `ADMIN_DASHBOARD_OPS.md`:

```
curl -s -o /dev/null -w "%{http_code}\n" https://admin.golfin.world/     # expect 302
```

A 200 there means Access is not protecting it — stop and investigate.

---

## 4. Unity client

New folder `Assets/Scripts/BannersRuntime/`, namespace `Golfin.Banners`, no `.asmdef`.

### 4.1 Reuse the image downloader — parameterize, do not fork

`TournamentArtService` is the only image-download and texture-cache code in the project, and it
carries a lot of hard-won behaviour: a `DownloadHandlerScript` that refuses on
`Content-Length` **before buffering**, `redirectLimit = 0`, atomic `.tmp`+replace cache writes,
in-flight coalescing, a failed-URL set, and a background LRU sweep. **Copying it for banners
would fork a security-critical download path.** Do not.

Two small, behaviour-preserving edits:

**`TournamentArtPolicy.cs`** — extract the body of `IsAllowed` into

```csharp
internal static bool IsAllowedUnder(string? url, Uri allowedRoot)
```

and leave `public static bool IsAllowed(string? url) => IsAllowedUnder(url, AllowedRoot);`.
Every existing check (scheme, host, userinfo, default port, path prefix, `..`, `%2e`, bucket
root) moves across unchanged. Also lift `CacheFileName` / `ExtensionOf` usage as-is — they are
already URL-generic.

**`TournamentArtService.cs`** — the type stays where it is and keeps its name. Add:

- a private constructor `TournamentArtService(string tag, string cacheDirName, Func<string, bool> isAllowed)`;
- fields for those three, replacing the three current hard-coded references (`Tag`,
  `TournamentArtPolicy.CacheDirName` in `CacheDir`, `TournamentArtPolicy.IsAllowed` in `Request`);
- the existing `Instance` keeps today's values verbatim — `"[TournamentArt]"`,
  `TournamentArtPolicy.CacheDirName`, `TournamentArtPolicy.IsAllowed`;
- `public static TournamentArtService Banners { get; } = new TournamentArtService("[BannerArt]", BannerPolicy.CacheDirName, BannerPolicy.IsArtAllowed);`
- an overload `public void Prefetch(IEnumerable<string?> urls)` alongside the existing
  `Prefetch(IEnumerable<TournamentDefinition>?)`.

`MaxDownloadBytes` (1 MB), `MaxCacheBytes` (50 MB) and `EndedRetention` stay as they are and now
apply per instance. The banner instance's cache directory is `<persistentDataPath>/game-banners`,
separate from `tournament-art`, so the two budgets do not evict each other.

⚠️ `Golfin.TournamentsRuntime.Tests` covers this file and `TournamentArtPolicy`. **Every existing
test must still pass, unmodified.** If a test needs editing to accommodate this change, that is
a signal the extraction changed behaviour — stop and re-read.

### 4.2 New files

**`BannerPolicy.cs`** — the whole security surface of this feature, and the only file a reviewer
must read line by line.

```csharp
public const string AllowedArtPrefix =
    "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/game-banners/";
public const string CacheDirName = "game-banners";

public static bool IsArtAllowed(string? url);    // → TournamentArtPolicy.IsAllowedUnder(url, root)
public static bool IsLinkAllowed(string? url);   // → §5.2
```

**`RemoteBannerDtos.cs`** — Newtonsoft DTOs matching §2 exactly. `expires_at` is a **string**,
parsed explicitly with `DateTimeStyles.AdjustToUniversal | AssumeUniversal`, for the same reason
`RemoteTournamentDto` keeps `start_at` / `end_at` as strings: a `DateTime` field lets Newtonsoft
hand back local time and give two players in different zones different behaviour.

**`RemoteBannerSource.cs`** — shape-for-shape copy of `RemoteTournamentSource`:
`CacheFileName = "banners.json"`, `CachePath`, `ReadCache`, `WriteCache` (atomic `.tmp` +
`File.Replace`), `ClearCache`, and `FetchRoutine(Action<string?> onDone)` over
`ApiClient.Instance.Get<string>` caching `result.RawBody`. On any failure `onDone(null)` and the
caller keeps what it has.

**`BannerService.cs`** — `MonoBehaviour` singleton, `DontDestroyOnLoad`, added to the same
GameObject that already carries `TournamentService` in `ShellScene`.

- `Awake`: read the disk cache synchronously and publish it, then start one fetch. Boot must
  never block on the network.
- `public bool TryGet(BannerPlacement placement, out BannerDefinition banner)` — returns false
  when nothing is live, the cached row has expired, or no art URL resolves for the current
  language.
- `public static event Action OnBannersChanged` — raised on the main thread only when a fetch
  **replaced** the set, so a screen already open repaints.
- `public void Refresh()` — guarded by `new ScheduleRefreshThrottle(60.0)`, exactly as
  `TournamentService` guards `RefreshSchedule`. Called from both screens' `OnEnable`.
- After publishing, `TournamentArtService.Banners.Prefetch(...)` the resolved URLs and issue one
  `SweepCacheAsync`.

Resolution order inside `TryGet`, and this is the whole fallback ladder:

1. `expires_at` is set and `now >= expires_at` → **no banner** (the cached row outlived its window).
2. `LocalizationManager.CurrentLanguage == Language.Japanese` → `image_url_ja`, else `image_url_en`.
3. That one is null/empty → the other one.
4. Still nothing → **no banner**.

"No banner" always means *the bundled sprite stays on screen*. There is no empty state.

**`BannerSlotBinder.cs`** — one small `MonoBehaviour` both screens use, so neither controller
learns about the network.

```csharp
[SerializeField] private BannerPlacement _placement;
[SerializeField] private Image  _image;      // the existing Image, bundled sprite already set
[SerializeField] private Button _button;     // added by this task
```

- `OnEnable`: cache `_image.sprite` as the bundled fallback on first run; subscribe to
  `BannerService.OnBannersChanged` and `LocalizationManager.OnLanguageChanged`; call
  `BannerService.Instance?.Refresh()`; apply.
- `OnDisable`: unsubscribe. (Project convention: subscribe in `OnEnable`, unsubscribe in
  `OnDisable`.)
- Apply: `TryGet` → on hit, `TournamentArtService.Banners.Request(url, s => _image.sprite = s)`;
  on miss, restore the bundled sprite. `Request` never calls back with null, so a failed download
  simply leaves whatever is drawn — bundled art — in place.
- Button: `interactable` only when a live banner has a `link_url` that passes
  `BannerPolicy.IsLinkAllowed`. On click, `Application.OpenURL(link)` — re-checking
  `IsLinkAllowed` at the call site, not trusting the flag set earlier.

### 4.3 Screen edits (minimal diffs)

**`Assets/Scripts/UI/HomeScreenController.cs`**

- Add `[SerializeField] private Image promoBannerImage;` under the existing
  `Promo Banner (GPS)` header.
- Replace the body of `OnPromoBannerClicked()` — the `Debug.Log` — with a delegation to the
  binder on the same GameObject. Keep the method (it is the `onClick` target) and keep the
  existing null-guarded `AddListener` in `Awake`.
- Leave `promoBannerText` and `gpsIcon` unassigned. Per D2 there is no text.
- Do **not** touch the News panel (`UpdateNewsContent`, `NextNewsPage`, `dotsContainer`,
  `totalNewsPages`, `newsAutoCycleInterval`). Out of scope.

**`Assets/Scripts/UI/Rankings/RankingsScreenController.cs`**

- Add `[SerializeField] private Image? _bannerImage;` and `[SerializeField] private Button? _bannerButton;`
  under the existing `Banner` header.
- `ApplyBanner()` keeps its current job — `_banner.SetActive(_showBanner)` — unchanged. Art and
  tap are the binder's, not the controller's.

**`Assets/Scenes/ShellScene.unity`** — on `Canvas/ScreensRoot/HomeScreen/PromoBanner`: add a
`Button`, add `BannerSlotBinder` (`_placement = HomePromo`, `_image` = that object's `Image`,
`_button` = the new Button), and wire `HomeScreenController.promoBannerButton` +
`promoBannerImage`. Also add `BannerService` to the GameObject already carrying
`TournamentService`. **The scene diff must be only those changes** — see the
`landing_surface_banner` kickoff note in `Docs/TellCode.md` for the scene-save trap.

**`Assets/Prefabs/UI/Rankings/RankingsScreen.prefab`** — on `RankingsScreen/ContentArea/Banner`:
add a `Button` and `BannerSlotBinder` (`_placement = Rankings`), wire
`RankingsScreenController._bannerImage` / `_bannerButton`.

---

## 5. Security

### 5.1 Art host

Identical posture to tournament art, for identical reasons: `image_url_*` is a free-text column,
and the client fetches it unattended at boot. `BannerPolicy.IsArtAllowed` pins scheme, host and
the `game-banners/` path prefix **after `Uri` normalization**, refuses userinfo, refuses a
non-default port, and refuses surviving `..` / `%2e`. Read
`TournamentArtPolicy.IsAllowed`'s doc comment before writing this — it explains precisely why a
`StartsWith` check is exploitable here.

### 5.2 Link host allowlist

```csharp
// BannerPolicy
private static readonly string[] AllowedLinkHosts = {
    "golfin.io", "www.golfin.io",
    "golfin.world", "www.golfin.world",
};
```

`IsLinkAllowed` requires: parses as an absolute `Uri`; `Scheme == "https"` (ordinal); `UserInfo`
empty; `IsDefaultPort`; `Host` matches an entry **exactly**, ordinal, after `Uri` lower-casing.
No suffix matching — `evil-golfin.io` and `golfin.io.attacker.net` must both fail, and a
`*.golfin.io` wildcard is what would let them through.

`golfin.io` is taken from the four URLs `SettingsController.cs` already opens
(`/terms-of-use`, `/privacy-policy`, `/faq`, `/contact`, lines 188–209) — it is the live
player-facing domain. `golfin.world` is the domain the admin dashboard runs on.

> **NOTE — needs Cesar before this ships.** Confirm the final list. If campaign pages will live
> on a marketing host, a Notion/Typeform page, or a partner app's domain, they must be added
> here **in the build** — an admin cannot add a host from the dashboard, by design. The dashboard's
> `validateBannerLinkUrl` must be kept byte-identical in spirit to this list; a URL the dashboard
> accepts but the client refuses is a banner that looks fine to the operator and does nothing on
> the device.

The dashboard-side check is a usability guard. **The client check is the control** — it is the
only one that still holds if a row is written by something other than the dashboard.

---

## 6. Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item `PASS` or `FAIL` with a one-sentence justification citing what was measured.

**Backend**

- [ ] Migration applied to prod and verified by dumping the column list over PostgREST (not "the SQL ran").
- [ ] `GET https://playlife-api.fly.dev/api/v1/banners` returns 200 with the `{"data":{"fetched_at","banners"}}` envelope.
- [ ] With two active `home_promo` rows at different `sort_order`, the response contains exactly one, the higher one.
- [ ] A row with `start_at` in the future is absent; a row with `end_at` in the past is absent.
- [ ] `is_active = false` → absent.

**Dashboard**

- [ ] Banners appears in the sidebar after Tournaments and the panel loads with `MOCK_MODE=1`.
- [ ] Upload writes to the `game-banners` bucket under a content-hashed name; re-uploading the same bytes yields the same URL.
- [ ] A >500 KB file and a `.gif` are both rejected client-side with a readable message.
- [ ] A `link_url` on an off-allowlist host is rejected on save.
- [ ] Create / update / delete / activate each write one `admin_audit_log` row with before/after — checked in the Audit Log panel, not assumed.
- [ ] Deactivating a LIVE banner requires the typed confirmation.
- [ ] Post-deploy: `curl -s -o /dev/null -w "%{http_code}" https://admin.golfin.world/` → 302.

**Tournament banner assignment (§9)**

- [ ] `tournaments.modal_banner_id` applied and verified over PostgREST.
- [ ] The tournament editor's Artwork tab lists only `is_active` `tournament_modal` banners, plus "None".
- [ ] Saving a tournament with a banner selected round-trips: reload the panel and it is still selected.
- [ ] A `modal_banner_id` naming a `home_promo` row is rejected with 400, not written.
- [ ] Deleting an assigned banner warns with the tournament count and needs a typed confirm; after deletion the tournament survives with `modal_banner_id = null`.
- [ ] `GET /tournaments/golfin` returns `modal_banner` for an assigned tournament, `null` for an unassigned one, and `null` once the banner row is switched `is_active = false`.
- [ ] The join is ONE extra query for the whole payload, not one per tournament — confirmed by reading the code, not guessed.

**Client — EditMode tests (new, in `Golfin.TournamentsRuntime.Tests` or a sibling)**

- [ ] `BannerPolicy.IsArtAllowed` refuses: http, wrong host, `user@host`, explicit port, the bucket root itself, a `..` traversal that normalizes out of the bucket, and a `%2e%2e` form. Accepts a well-formed object URL.
- [ ] `BannerPolicy.IsLinkAllowed` refuses `http://golfin.io`, `https://evil-golfin.io`, `https://golfin.io.attacker.net`, `https://golfin.io:8443`, `https://a@golfin.io`. Accepts `https://golfin.io/x` and `https://www.golfin.world/y`.
- [ ] Resolution ladder: JP + `image_url_ja` null falls back to `image_url_en`; both null → no banner; `expires_at` in the past → no banner.
- [ ] **Every pre-existing `TournamentArtPolicy` / `TournamentArtService` test still passes, unmodified.**

**Client — on device (manual, human-in-the-loop)**

- [ ] Home shows the uploaded EN promo image in place of `GPS Banner.png`; tapping opens the browser at the link.
- [ ] Switching the language to Japanese swaps to the JA image without leaving the screen.
- [ ] Deactivate in the dashboard → relaunch → the bundled GPS sprite is back, no gap, no error.
- [ ] Cold launch in airplane mode → both slots show bundled art, Console has warnings only (no errors).
- [ ] Second launch with the same banner logs `Cache HIT … no download` from `[BannerArt]`.
- [ ] Rankings banner behaves the same for its placement.

**Always**

- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] `ShellScene.unity` diff contains only the changes named in §4.3.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations flagged at the bottom of `IMPLEMENTER_REPORT.md` with justification.

## 7. Smoke evidence

Per-device verification is required for the four manual items above; a dispatch log is not
visual evidence (`Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson O). Attach: one screenshot of Home
with a remote banner, one with the bundled fallback after deactivation, and a Console excerpt
showing the `[BannerArt] Cache HIT` line on the second launch.

## 8. Files this task touches

**New**

- `playlife/backend/migrations/2026_08_17_game_banners.sql` (+ copy in `Tools/admin-dashboard/migrations/`)
- `playlife/backend/routers/banners.py`
- `Tools/admin-dashboard/lib/banner.ts`, `bannerData.ts`, `bannerMutations.ts`, `mockBanners.ts`
- `Tools/admin-dashboard/app/(panels)/banners/{page,banners-panel,banner-editor}.tsx`
- `Tools/admin-dashboard/app/api/banners/{route.ts,[id]/route.ts,art/route.ts}`
- `Assets/Scripts/BannersRuntime/{BannerPolicy,RemoteBannerDtos,RemoteBannerSource,BannerService,BannerSlotBinder}.cs`
- EditMode tests for `BannerPolicy` and the resolution ladder

**Modified**

- `playlife/backend/main.py` — one import, one `include_router`
- `playlife/backend/routers/tournaments.py` — `list_golfin` joins `modal_banner` (§9.4)
- `Tools/admin-dashboard/lib/tournamentData.ts`, `lib/tournamentMutations.ts`,
  `app/(panels)/tournaments/tournament-editor.tsx` — the §9.3 banner picker
- `Tools/admin-dashboard/lib/{registry,types,mockStore}.ts`, `components/PanelIcon.tsx`
- `Assets/Scripts/TournamentsRuntime/TournamentArtPolicy.cs` — extract `IsAllowedUnder`
- `Assets/Scripts/TournamentsRuntime/TournamentArtService.cs` — parameterize; add `Banners` instance + `Prefetch(IEnumerable<string?>)`
- `Assets/Scripts/UI/HomeScreenController.cs` — one field, one method body
- `Assets/Scripts/UI/Rankings/RankingsScreenController.cs` — two fields
- `Assets/Scenes/ShellScene.unity`, `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab`
- `Docs/AI_CONTEXT.md`, `Docs/TellCode.md`, `Docs/ADMIN_DASHBOARD_OPS.md`, `Tools/admin-dashboard/README.md`

## 9. `tournament_modal` — created here, assigned in Tournaments (D7)

The tournament sign-up and result modals carry a **Cross Promotion Banner** at the top
(Figma `13892:3435`, **970 × 252**, corner radius 20 — the same pixel spec as the `rankings`
slot). Cesar's call: the artwork is **managed in this Banners panel** so one GPS promo can serve
every tournament and be swapped in one edit, but **which** banner a tournament shows is set in
the **Tournaments** panel, per tournament.

The consuming client work is specced separately in
`Docs/Specs/Active/tournament_signup_modal/`. This section defines only the half that lives here.

### 9.1 Schema

Add to §1's migration:

```sql
alter table public.tournaments
  add column if not exists modal_banner_id uuid
    references public.game_banners(id) on delete set null;
```

`on delete set null`, not `cascade`: deleting a banner must never delete a tournament. The
tournament simply loses its banner and the modal renders without the strip.

### 9.2 What changes in this spec's own scope

- **`placement` gains `tournament_modal`** (already in §1's check constraint).
- **`GET /api/v1/banners` ignores it** (already in §2). A `tournament_modal` row is never
  auto-served; it only ever reaches a player attached to a tournament.
- **`start_at` / `end_at` do not apply** to a `tournament_modal` row — the tournament's own
  window governs when it is on screen. The editor **hides both fields** when the placement is
  `tournament_modal` rather than showing controls that do nothing. `is_active` still applies and
  is still the kill switch: switching a banner off drops it from every tournament using it, at
  once, which is the point of managing it in one place.
- **`sort_order` does not apply** either — there is no "pick the top one" for an assigned banner.
  Hide it too.
- **Art spec** for this placement: **970 × 252**, same `BANNER_ART_SPEC` byte cap and MIME list.
- **`link_url` still applies.** Tapping the strip in the modal opens it, through the same
  `BannerPolicy.IsLinkAllowed` gate as every other placement.
- **Deleting a row that is assigned** must warn with the count and list of tournaments pointing at
  it, and require a typed confirmation — the same posture §3.4 takes for deactivating a LIVE
  banner. Read the count off `tournaments.modal_banner_id` before showing the dialog.
- The list view shows an **"Assigned to N tournaments"** line on each `tournament_modal` row, so an
  operator can see the blast radius without opening the Tournaments panel.

### 9.3 What the Tournaments panel gains

Specced here for completeness; implement it alongside the rest of §3, since it is the same
codebase and the same deploy.

- `lib/types.ts`: `TournamentRow.modalBannerId: string | null` and the same on `TournamentInput`.
- `lib/tournamentData.ts`: map `modal_banner_id`, tolerating the column being absent on an
  un-migrated DB (`str(r.modal_banner_id)`), exactly as `isActive` tolerates its own column today.
- `lib/tournamentMutations.ts`: persist it on create and update; validate that the id, when
  non-null, names an existing `game_banners` row whose `placement = 'tournament_modal'`. A
  dangling or wrong-placement id is a 400, not a silent write.
- `app/(panels)/tournaments/tournament-editor.tsx`: a **Banner** picker on the **Artwork** tab —
  a dropdown of active `tournament_modal` banners by `label`, each with a thumbnail preview, plus
  a "None" entry. Do **not** add a second upload control here; the tab already owns card art, and
  the whole point of D7 is that banner bytes are uploaded once, in the Banners panel. Link out to
  `/banners` with a short line saying where new ones come from.
- Audit: an assignment change rides in the existing `tournament_update` before/after snapshot. No
  new action name.

### 9.4 What `GET /tournaments/golfin` gains

One extra join, in `backend/routers/tournaments.py::list_golfin`. Select `modal_banner_id`
alongside the existing columns, batch-fetch the referenced `game_banners` rows in one query
(the same one-extra-round-trip shape the prize-bands fetch already uses — **not** one query per
tournament), and emit per tournament:

```json
"modal_banner": {"image_url_en": "…", "image_url_ja": null, "link_url": "https://…"}
```

`null` when the tournament has no banner, when the referenced row is missing, or when that row is
`is_active = false`. The **inactive check happens server-side** — the client must not have to know
about `is_active`.

⚠️ Do not add `modal_banner_id` itself to the payload. The client has no use for an internal uuid,
and shipping it invites someone to fetch by it later.

## 10. Out of scope (do NOT do these)

- The Home **news/announcement carousel** (`NoticePanel`, `PageDots`, `HOME_MAINTENANCE_*`). It is
  still hard-coded and still shows the same string on all three pages. Separate task.
- `Canvas/ScreensRoot/TournamentLeaderboardScreen/ContentArea/Banner` and its sponsor pills.
- Any text on a banner — no title, no body, no localized strings, no `promoBannerText`.
- Rotation, carousels, dots, or more than one live banner per placement.
- Targeting by platform, app version, player segment, or A/B bucket.
- Impression or click analytics.
- In-game deep links from a banner. External URL only, this pass.
- Touching `tournaments.banner_url` or the `tournament-art` bucket. The Tournaments panel is
  touched **only** for the §9.3 banner picker and the §4.1 shared-policy extraction — nothing else.
- Rotating the `service_role` key (tracked separately in `ADMIN_DASHBOARD_OPS.md` §6).
