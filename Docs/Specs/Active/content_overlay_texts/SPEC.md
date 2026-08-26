# SPEC — `content_overlay_texts`

> **Phase 1. The first time any of the content pipeline reaches the game.**
> Everything built so far — catalogs, publish/rollback, panels, the delta endpoint — is a system
> the client has never read. This spec closes that, on the safest catalog.
>
> Plan: `Docs/CONTENT_PIPELINE_PLAN.md` §2 (the six invariants — read them first), §4 (this task).
> Depends on: `content_catalog`, `content_cursor_per_catalog`, `content_admin_panels`,
> `content_panels_gaps` — all DONE and deployed.
>
> NOT `SPEC_KIND: backend` — this is a Unity task with a real Game View. The screenshot and
> EditMode gates apply.

## Status

`SPEC_READY`.

## Goal

Ship `Golfin.Content`: a disk-cached, fail-soft overlay that lets an admin-published text change
appear in the game. **Texts only.** Clubs, characters, items and shop are deliberately excluded —
if the mechanism is wrong, find out on a string, not on 799 clubs.

## Why texts first

- `LocalizationManager` is a static `Dictionary<string, LocalizedTextRow>` with an existing
  `OnLanguageChanged` refresh — the smallest possible surface.
- A wrong string is visible and harmless. A wrong `basePower` is neither.
- It exercises every part of the path — endpoint, cursor, cache, fail-soft, kill switch — end to end.

## Architecture context

**Existing code this builds on (read before writing):**

- `Assets/Scripts/NoticesRuntime/RemoteNoticeSource.cs` — **the template.** Copy its shape:
  raw-body disk cache, atomic `.tmp` + `File.Replace`, null on ANY failure.
- `Assets/Scripts/NoticesRuntime/NoticeService.cs` — MonoBehaviour singleton, `DontDestroyOnLoad`,
  reads cache synchronously at `Awake`, fetches off the critical path. Lives in `ShellScene`.
- `Assets/Localization/LocalizationManager.cs` — `Initialize(table, lang)` builds `_textMap`;
  `Get(key)` falls back to the key; `OnLanguageChanged` is the refresh signal.
- `Assets/Localization/LocalizationBootstrap.cs` — `[DefaultExecutionOrder(-1000)]`, calls
  `Initialize`.
- `Assets/Scripts/Net/` — `ApiClient.Instance.Get<string>(url, cb)` (plain C# singleton, not a
  MonoBehaviour), `Endpoints.Content(since, build)`.
- `Assets/Resources/Data/content_version.txt` — written by the exporter, one `<catalog>=<version>`
  line per catalog. Currently `texts=11`.

## Implementation

### 1. `Golfin.Content` asmdef

New `Assets/Scripts/ContentRuntime/`, referencing `Golfin.Net` and `Golfin.Localization`. One-way,
like every other runtime asmdef.

### 2. `RemoteContentSource` — a near-copy of `RemoteNoticeSource`

- Cache at `<persistentDataPath>/content_texts.json`. **Mirror the RAW body before mapping**, so a
  payload this build cannot map is still available to a later build that can.
- Atomic write: `.tmp` then `File.Replace`. A kill mid-write must leave the previous good cache,
  not a truncated file.
- `ReadCache()` returns null on any exception. `FetchRoutine(Action<string?>)` returns null on any
  failure — **warning, not error**: a cold launch in airplane mode is a designed path.

### 3. `ContentVersionFile` — read the bundled cursor

Parse `Resources.Load<TextAsset>("Data/content_version")` into `catalog → version`. A missing file
or unparseable line means **0 for that catalog** (→ full payload), never an exception. The
endpoint wants `texts:11`; the file writes `texts=11`. Convert; do not change the file format.

### 4. ⚠️ `min_build` — RESOLVE, do not guess

The client must send an integer build number so the server can withhold rows the build cannot
render (§2 I4). **There is no cross-platform Unity runtime API for the store build number**, and
the two candidate sources on disk **disagree**:

| Source | Value |
|---|---|
| `ProjectSettings.asset` → `buildNumber: iPhone` | **2113** |
| `Assets/Resources/Data/build_stamp.txt` | `v1.5.7 (2297) 02c1678+da58 · 08-26 06:56` |

Pick one and make it authoritative, then say which in the report. Recommended: **bake an integer
at build time** into `Resources/Data/build_number.txt` via the existing `BuildStampGenerator`
mechanism, because `BuildStamp.cs` itself compiles out unless `GOLFIN_TESTBUILD` and `min_build`
must work in a release build. Parse failure ⇒ send `0`, which is the safe end (the server then
sends only rows every build can render).

Do NOT invent a mapping from `Application.version`.

### 5. `ContentService` — MonoBehaviour, `[DefaultExecutionOrder(-900)]`

`-900` is **after** `LocalizationBootstrap` (`-1000`, which builds `_textMap`) and **before**
`SaveDataHost` (`-100`). Getting this backwards means the overlay is applied and then wiped by
`Initialize`.

At `Awake`: read the cache synchronously, map it, call `LocalizationManager.ApplyOverlay`. Then
start the fetch off the critical path. Add to `ShellScene` beside `NoticeService`.

**The fetch writes the cache and does NOT re-apply this session.** §2 I5 — changes take effect at
next launch. Texts *could* swap live safely, and that is a reasonable follow-up, but the first
Unity spec should have as few moving parts as possible: prove the boot path, then decide.
Say so in the report rather than adding it.

Request: `Endpoints.Content("texts:" + v, build)` with `catalogs=texts`.

### 6. `LocalizationManager.ApplyOverlay`

```csharp
/// <summary>Merge admin-published rows over the bundled table. Keys not in the overlay are
/// untouched; unknown keys are added and harmlessly unused. No-op on null/empty.</summary>
public static void ApplyOverlay(IReadOnlyDictionary<string, LocalizedTextRow> overlay)
```

- Merge into `_textMap`, then fire `OnLanguageChanged`.
- **Skip any row whose `english` is empty** — a blank string is worse than the bundled one, and
  `Get()`'s existing JA→EN fallback depends on `english` being present.
- Rows with `is_active = false` are **ignored**, not deleted: the bundled string stays. §2 I6.
- ~15 lines. **No call-site changes anywhere.**

### 7. Kill switch

`enabled: false` in the payload ⇒ ignore the response entirely AND drop the cache, so the next
launch is bundled-only. One flag must fully undo remote text.

## Acceptance checklist

- [ ] Edit a string in the Texts panel, publish, relaunch the game → the new string renders
- [ ] The same string in JA renders the JA value; switching language mid-session still works
- [ ] **Airplane mode, cold launch, no cache** → bundled strings, no error, one warning
- [ ] **Airplane mode with a warm cache** → the cached overlay still applies
- [ ] Corrupt `content_texts.json` by hand → bundled strings, one warning, no exception
- [ ] `enabled: false` → next launch is bundled-only and the cache is gone
- [ ] A row with empty `english` is skipped, not applied
- [ ] `is_active = false` leaves the bundled string in place
- [ ] Missing/garbage `content_version.txt` → full payload requested, game still boots
- [ ] `min_build` source resolved, named in the report, and a row above the build's number is not received
- [ ] Execution order verified: `ApplyOverlay` runs AFTER `Initialize` (log both, paste the order)
- [ ] Boot time not measurably worse — the fetch is off the critical path (measure, don't assert)
- [ ] Full unfiltered EditMode sweep green; new tests in a `Golfin.Content.Tests` asmdef
- [ ] Screenshot of an admin-published string rendering in-game
- [ ] Spec deviations flagged at the bottom of the report

## Out of scope

- **Every other catalog.** Clubs/characters/items/shop overlays are the next spec, and they need
  the clamping rules in `CONTENT_PIPELINE_PLAN.md` §5 that texts does not.
- Live mid-session text swap (§5 above — deliberate, revisit after the boot path is proven).
- Player inventory, Addressables, art URLs.
- Any change to the endpoint, the panels, or the schema. If the client needs something the API
  cannot serve, **report it** — that has now caught four real gaps.
