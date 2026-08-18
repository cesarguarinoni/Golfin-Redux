# SPEC — Home notices from the admin dashboard

**Status:** ready to implement (client side)
**Author:** Architect session, 2026-08-18
**Server side:** already written and deployed by the Architect — see §2. The client is the only outstanding work.

The Home screen's notice panel — the one that today reads `MAINTENANCE NOTICE` /
"Scheduled server maintenance: 2025/12/31" from `LocalizationText.csv` — becomes
operator-controlled. Title and body, EN + JA, scheduled, switchable, with no
client build.

---

## 0. Why the shipped behaviour has to change

`HomeScreenController.UpdateNewsContent()` currently does this:

```csharp
bool demo = GolfinRedux.Demo.DemoGate.IsDemo;
string titleKey = demo ? "HOME_DEMO_WELCOME_TITLE" : "HOME_MAINTENANCE_TITLE";
string bodyKey  = demo ? "HOME_DEMO_WELCOME_BODY"  : "HOME_MAINTENANCE_BODY";
newsTitleText.text = LocalizationManager.Get(titleKey);
newsBodyText.text  = LocalizationManager.Get(bodyKey);
```

Two problems, and the second is the reason this is worth doing now:

1. `_currentNewsIndex` and `totalNewsPages = 3` drive dots that page between
   three copies of the same text. The paging exists; the content does not.
2. **Every player is currently being told the servers go down on 2025/12/31** —
   a date eight months in the past, baked into the build. There is no way to
   correct it without shipping a release.

---

## 1. Schema

`playlife/backend/migrations/2026_08_18_home_notices.sql`, table
`public.home_notices`. Applied to prod by Cesar in the Supabase SQL editor.
Column comments in that file are the reference; the shape is:

| column | notes |
|---|---|
| `label` | admin-only, never sent to the client |
| `title_en`, `body_en` | the base locale; an active row must have one of them |
| `title_ja`, `body_ja` | nullable — null means "fall back to English", not "hide" |
| `start_at`, `end_at` | optional UTC window, `end_at` EXCLUSIVE |
| `sort_order` | page order, highest first, then newest |
| `is_active` | the switch, defaults **false** |

RLS on, no policies, service_role only — identical posture to `game_banners`.

---

## 2. Endpoint (done — this is the contract to code against)

`GET https://playlife-api.fly.dev/api/v1/notices` — **no auth**, same posture as
`/banners` and `/tournaments/golfin`, so it can warm at boot before any token
work.

```json
{"data": {
  "fetched_at": "2026-08-18T04:10:00+00:00",
  "notices": [
    {"title_en": "MAINTENANCE NOTICE",
     "title_ja": "メンテナンス情報",
     "body_en": "Scheduled server maintenance: 2026/08/28\nThe game will not be available…",
     "body_ja": "定期サーバーメンテナンス: 2026/08/28…",
     "expires_at": "2026-08-29T00:00:00+00:00"}
  ]
}}
```

Contract notes that matter to the client:

- The server applies the **whole** scheduling rule (`is_active` + window). The
  client never re-derives it. The one thing the client must honour is
  `expires_at`, because a body cached on disk can outlive its window while the
  player is offline — and a maintenance notice that outlives the maintenance is
  worse than no notice at all.
- `notices` is ordered. Index 0 is page 1.
- **An empty array is a normal, healthy response** meaning *hide the panel*.
  This is the one place the notices differ in kind from the banners: banners
  always have a bundled sprite behind them, an unwritten announcement has
  nothing behind it.
- At most 5 rows (`MAX_NOTICES` in `backend/routers/notices.py`).
- Both locales are always sent; the client picks.

---

## 3. `NoticesRuntime` — mirror `BannersRuntime`

New folder `Assets/Scripts/NoticesRuntime/`, namespace `Golfin.Notices`, no
asmdef (same as `BannersRuntime`). Three files, each the direct analogue of the
banner one — **read the banner file first and follow it**, including the failure
posture and the comment discipline:

| new file | model it on |
|---|---|
| `RemoteNoticeDtos.cs` | `RemoteBannerDtos.cs` |
| `RemoteNoticeSource.cs` | `RemoteBannerSource.cs` — cache file `home_notices.json`, atomic `.tmp` + replace, raw body cached BEFORE mapping, null on any failure |
| `NoticeService.cs` | `BannerService.cs` — singleton, `DontDestroyOnLoad`, disk cache read synchronously in `Awake`, then `Refresh()` off the critical path |

Add to `Golfin.Net.Endpoints`:

```csharp
/// <summary>GET → {data:{fetched_at, notices:[…]}} — the Home notice panel's copy.
/// No auth, same posture as <see cref="Banners"/>. No trailing slash.</summary>
public static string Notices => BaseUrl + "/notices";
```

### 3.1 What the service exposes

```csharp
public readonly struct NoticePage
{
    public readonly string Title;          // already resolved for the current language
    public readonly string Body;
    public readonly DateTime? ExpiresAtUtc;
}

public sealed class NoticeService : MonoBehaviour
{
    public static NoticeService? Instance { get; private set; }
    public static event Action? OnNoticesChanged;   // fetch REPLACED the set; same rules as banners
    public NoticeSource Source { get; }             // None | DiskCache | Server
    public const double RefreshCooldownSeconds = ScheduleRefreshThrottle.DefaultCooldownSeconds;

    /// <summary>Live pages in order, already language-resolved and expiry-filtered.
    /// Empty is a normal state and means "hide the panel".</summary>
    public IReadOnlyList<NoticePage> Pages { get; }

    public bool Refresh();                          // throttled; never awaited by the caller
}
```

`Pages` is rebuilt (not cached across calls) when the set changes **and** when
the language changes — see §3.3.

### 3.2 The resolution ladder — pure and unit-tested

Exactly as `BannerService.ResolveImageUrl` is: a `static internal` pure function
with no MonoBehaviour, clock or socket, so the EditMode tests can drive it.

```csharp
internal static bool TryResolve(
    string? titleEn, string? titleJa,
    string? bodyEn,  string? bodyJa,
    bool japanese, DateTime? expiresAtUtc, DateTime nowUtc,
    out string title, out string body)
```

1. `expiresAtUtc` set and `nowUtc >= expiresAtUtc` → **drop the page**. (The
   server already filtered; this covers a cached body with no network to learn
   from.)
2. Japanese player: `title_ja` if non-blank, else `title_en`; same for body,
   **independently** — a row with a Japanese title and no Japanese body shows a
   Japanese heading over English copy, which is correct and better than
   dropping either.
3. English player: `title_en` / `body_en` **only**. An English player must never
   fall into Japanese copy — the same rule `TournamentDisplayName` enforces, for
   the same reason.
4. Both title and body empty after all that → drop the page. (The dashboard
   refuses to activate such a row; this is the belt.)

### 3.3 Language switching

`LocalizationManager.OnLanguageChanged` — subscribe in `OnEnable`, unsubscribe in
`OnDisable`, exactly as `BannerSlotBinder` does. A language switch must re-resolve
and repaint without leaving the screen.

### 3.4 Refresh

`Refresh()` is throttled by `ScheduleRefreshThrottle` (the same 60 s cooldown and
in-flight guard the schedule and the banners use). It is called:

- once in `Awake`, after the synchronous cache read;
- from `HomeScreenController.OnEnable`, every time Home comes up.

Failure is silent by construction: every failure path leaves the current set
untouched and logs one warning. No toast, no retry, no empty state on a network
error.

### 3.5 Scene wiring

`NoticeService` needs a GameObject in `ShellScene` alongside `BannerService`.
Follow whatever pattern `BannerService` uses there — if it is on a bootstrap
object, put this on the same one.

---

## 4. `HomeScreenController`

### 4.1 New serialized field

```csharp
[SerializeField] private GameObject newsPanelRoot;   // the panel to hide when nothing is live
```

Wire it in `ShellScene` to the news panel's root (the object that owns
`newsTitleText`, `newsBodyText` and `dotsContainer`). **Leaving it unassigned
must not crash and must not hide anything** — null-check and fall back to
leaving the panel visible, which is the pre-change behaviour.

### 4.2 `UpdateNewsContent` becomes

```
if (DemoGate.IsDemo)                       → HOME_DEMO_WELCOME_TITLE / _BODY, one page, dots hidden.
                                             UNCHANGED — the demo build has no server.
else if (NoticeService has ≥1 page)        → page _currentNewsIndex, dots = page count.
else                                       → newsPanelRoot.SetActive(false).
```

`totalNewsPages` stops being a serialized constant and becomes the live page
count. Keep the field (scene serialization) but treat it as a **cap** if it is
> 0, or simply ignore it — your call, but say which in a comment.

### 4.3 The bundled `HOME_MAINTENANCE_*` strings are retired

The non-demo path no longer reads them. **Do not** keep them as an offline
fallback: a cold launch in airplane mode with no cache would then show a
maintenance notice from a date that has already passed, which is exactly the bug
this feature exists to fix. Hiding the panel is the correct offline state.

Leave the two CSV rows in place (harmless, and `LocalizationText.csv` churn is
not worth a merge conflict) but add a `# unused since home_notices` marker if
the CSV format tolerates one. If it does not, leave them untouched and note it
in the PR.

### 4.4 Dots

`dotsContainer` has a fixed number of children in the scene. For a page count
`n`:

- `n <= 1` → hide `dotsContainer` entirely (one page needs no dots).
- otherwise → activate the first `min(n, childCount)` children, deactivate the
  rest, and clamp `_currentNewsIndex` into range.

If `n > childCount`, the extra pages are still reachable by auto-cycle; the dots
just under-represent them. Log one warning naming both counts — the endpoint
caps at 5, so this means the scene needs more dot children.

### 4.5 Auto-cycle

Unchanged (`newsAutoCycleInterval`, 5 s), except that it must stop when the page
count drops to 1 or 0, and `_currentNewsIndex` must be re-clamped whenever the
set changes underneath it — a refresh that removes pages while the player is
looking at page 3 must not index out of range.

---

## 5. Acceptance

1. Dashboard → Notices → new notice, EN + JA, Activate. Relaunch the game: the
   Home panel shows it, in the device's language.
2. With the game already running on another screen, activate a second notice;
   return to Home. The panel repaints with two pages and two dots, without a
   relaunch (screen-entry refresh, ≤1 request per minute).
3. Deactivate everything. Return to Home → the panel is **gone**, not blank, not
   showing the old maintenance text.
4. Switch language in Settings with a notice on screen → title and body swap in
   place. A notice with no Japanese shows the English to a JP player.
5. Airplane mode, cache present → the cached notices show. Airplane mode, cache
   cleared → the panel is hidden and one warning is logged.
6. Set `end_at` a minute out, let it pass with the app backgrounded, come back →
   the page is gone client-side even with no network.
7. Demo build (`DemoGate.IsDemo`) → unchanged welcome message.

## 6. Tests (EditMode, alongside `Assets/Scripts/TournamentsRuntime/Tests/`)

Pure-function coverage of `TryResolve`, table-driven, mirroring
`RemoteScheduleTests`' language handling (restore `LocalizationManager` state in
`TearDown` — language is global static state):

- JA player, both languages present → JA.
- JA player, `title_ja` null → EN title, JA body (independent fallback).
- EN player, only JA present → page dropped.
- `expires_at` in the past → dropped, regardless of language.
- Empty title AND body → dropped.
- Deserialization: enveloped `{"data":…}` (cache) and bare payload (live fetch)
  both parse; malformed JSON returns null and changes nothing.

---

## 7. Out of scope

- Rich text / images in notices. Plain text, `\n` breaks, TMP renders it.
- Per-player or per-segment targeting.
- Push notification on publish.
- A third language — that waits for localisation to move into the editor
  (Cesar, 2026-08-17).
