# Implementer Report — `content_overlay_texts`

**Iteration shape:** `content:phase1-texts-overlay`
**Iteration:** 1
**Date:** 2026-08-26

## Implementation summary

Shipped `Golfin.Content`: a new one-way asmdef holding a disk-cached, fail-soft texts overlay
that is read synchronously at `Awake` (execution order `-900`, immediately after
`LocalizationBootstrap`'s `-1000`) and merged over the bundled `LocalizationTextTable` by id.
`RemoteContentSource` is a shape-for-shape copy of `RemoteNoticeSource` — raw-body mirror, atomic
`.tmp` + `File.Replace`, null on any failure — and the fetch runs as a coroutine off the boot
critical path, writing the cache for the NEXT launch without re-applying this session (§2 I5).
`LocalizationManager` gained `ApplyOverlay` (~20 lines) and `IsInitialized`; there are **no
call-site changes anywhere**.

The admin-published change reaches the game: the canonical screenshot shows five overlaid Settings
rows rendering in the live UI, alongside two rows the client deliberately refused (one
`is_active:false`, one blank-`english`) still showing their bundled strings, and three untouched
keys — four acceptance invariants in one frame.

## The `min_build` question — RESOLVED

**`Assets/Resources/Data/build_stamp.txt`, parsed by the new `ContentBuildNumber`, is
authoritative. Measured value this session: `2302`.**

SPEC §4 flagged that the two candidate sources disagree. They disagree for a reason that makes the
choice unambiguous:

| Source | Value | Verdict |
|---|---|---|
| `ProjectSettings.asset` → `buildNumber: iPhone` | 2113 | **Rejected.** `BuildStampGenerator` deliberately RESTORES this field after every build (`OnPostprocessBuild` + a `delayCall` safety net) so `ProjectSettings.asset` generates no cross-machine merge noise. 2113 is a stale working-copy leftover, is in no shipped binary, and has no cross-platform runtime API anyway. |
| `Assets/Resources/Data/build_stamp.txt` | `v1.5.7 (2302) 96c9e0d+af95 · 08-26 08:55` | **Chosen.** Baked in `OnPreprocessBuild` from `git rev-list --count HEAD` — the same integer written to `PlayerSettings.iOS.buildNumber` (→ `CFBundleVersion`) and `Android.bundleVersionCode`. It is the only one of the three a runtime can read, it is baked **ungated** on every build (the `GOLFIN_TESTBUILD` gate is on `BuildStamp.cs`, the on-screen overlay, **not** on the generator that writes the file), and it is already trusted for exactly this by `GolfinRedux.UI.BuildInfo.AppVersion` in Settings ▸ About. |

**Deviation from the spec's recommendation, deliberately:** SPEC §4 suggested baking a *new*
`Resources/Data/build_number.txt`. That would add a **third** source that can disagree with the
other two — precisely the failure this section exists to close. Reading the artifact the pipeline
already bakes cannot drift from the binary.

Guards on that choice:
- `ContentBuildNumberTests.Parse_ReadsTheRealBundledStamp` parses the **real bundled file** and
  fails if it yields ≤ 0, so a stamp-format change breaks a test instead of silently pinning every
  client to `build=0`.
- `AppVersion.cs` now carries a "SECOND CONSUMER — DO NOT CHANGE THE `(1234)` FORMAT" note pointing
  back here.
- Parse failure ⇒ **0**, the safe end (server then serves only rows every build can render).
  Verified: `ContentBuildNumber.Parse("v1.5.7 (editor) · …") == 0`.

`build_stamp.txt` is **gitignored** (`.gitignore:260`) — generated, not committed. It self-heals in
the editor (`BuildStampGenerator.InstallEditorHooks` writes it if missing, and refreshes it on every
play-mode enter) and is baked by `OnPreprocessBuild` in every player build, so it is present on both
paths. Flagged here because it is a build-artifact dependency, not a source file.

## Execution order — verified, both logs pasted

`ContentService` is `-900`, `LocalizationBootstrap` is `-1000`. Confirmed by reflection
(`ContentService order = -900`) and, more importantly, in the real boot log:

```
[LocalizationBootstrap] Startup language: English (saved=no, device=English)
[Content] Awake — LocalizationManager already initialised (order OK: LocalizationBootstrap -1000 → ContentService -900).
[Content] Texts overlay applied from DISK CACHE: 6 row(s) merged over the bundled table (catalog v12, full=False, skipped inactive=1, unusable=1).
[Content] Boot critical-path cost: 50.42 ms (synchronous cache read + map + merge; the fetch below blocks nothing).
[Content] Fetching texts delta: since=texts:11, build=2302.
[Content] Texts cache refreshed from SERVER: catalog v11, full=False, 0 usable row(s). NOT applied this session by design (§2 I5) — it takes effect at next launch.
```

The order is **asserted at runtime**, not merely declared: `ContentService.Awake` refuses to apply
the overlay and logs a `LogError` if `LocalizationManager.IsInitialized` is false. A silent wipe by
a later `Initialize()` is invisible in game (it just shows bundled strings), so the invariant is
checked rather than trusted to an attribute surviving a future refactor.
`LocalizationOverlayTests.Initialize_AfterApplyOverlay_WipesIt_WhichIsWhyOrderMatters` pins the
failure mode itself.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/ContentRuntime/Golfin.Content.asmdef` | created — one-way asmdef, references `Golfin.Net` + `Golfin.Localization`, precompiled `Newtonsoft.Json.dll` |
| `Assets/Scripts/ContentRuntime/RemoteContentDtos.cs` | created — wire DTOs for `GET /api/v1/content`; `data` is a loose string→string bag so an unknown admin column is ignored (I4) |
| `Assets/Scripts/ContentRuntime/RemoteContentSource.cs` | created — per-catalog disk cache (`content_texts.json`), atomic `.tmp` + `File.Replace`, `ClearCache`, `FetchRoutine`; null on any failure |
| `Assets/Scripts/ContentRuntime/ContentVersionFile.cs` | created — parses the bundled `content_version.txt` (`texts=11`) into a cursor; every failure resolves to 0 |
| `Assets/Scripts/ContentRuntime/ContentBuildNumber.cs` | created — resolves `build=` from `build_stamp.txt`; 0 on any failure |
| `Assets/Scripts/ContentRuntime/ContentTextsMapper.cs` | created — pure payload→overlay mapping; kill switch, `is_active`, blank-english and unknown-column rules all live here |
| `Assets/Scripts/ContentRuntime/ContentService.cs` | created — `[DefaultExecutionOrder(-900)]` MonoBehaviour; boot apply + off-critical-path fetch |
| `Assets/Localization/LocalizationManager.cs` | modified — added `ApplyOverlay(IReadOnlyDictionary<…>)` and `IsInitialized`. No other behaviour touched |
| `Assets/Scripts/Net/Endpoints.cs` | modified — `Content(since, build)` gained an **optional** third `catalogs` parameter (default null preserves the old two-arg shape); doc comment updated from "NOTHING CALLS THIS YET" to name the caller |
| `Assets/Scripts/UI/BuildInfo/AppVersion.cs` | modified — **doc comment only**, cross-referencing the second consumer of the stamp format. No code change |
| `Assets/Scenes/ShellScene.unity` | modified — **13 insertions, 0 deletions**: `ContentService` added to the `TournamentService` GameObject beside `NoticeService`/`BannerService` |
| `Assets/Scripts/ContentRuntime/Tests/Golfin.Content.Tests.asmdef` | created — EditMode test assembly |
| `Assets/Scripts/ContentRuntime/Tests/ContentVersionFileTests.cs` | created — 8 tests (cursor parsing, garbage, negatives, the real bundled file) |
| `Assets/Scripts/ContentRuntime/Tests/ContentBuildNumberTests.cs` | created — 7 tests, incl. parsing the REAL bundled stamp |
| `Assets/Scripts/ContentRuntime/Tests/ContentTextsMapperTests.cs` | created — 15 tests (envelope both shapes, `is_active`, blank english, unknown columns, kill switch, corrupt) |
| `Assets/Scripts/ContentRuntime/Tests/LocalizationOverlayTests.cs` | created — 11 tests on `ApplyOverlay` incl. the order-wipe failure mode |
| `Assets/Scripts/ContentRuntime/Tests/RemoteContentSourceTests.cs` | created — 9 tests (atomic write, no stray `.tmp`, `ClearCache`, corrupt round trip) |
| `Assets/Scripts/ContentRuntime/Tests/ContentFetchPathTests.cs` | created — 8 tests driving the fetch through a fake `IHttpTransport`: airplane mode, 500, kill switch, exact URL |
| `Docs/Specs/Active/content_overlay_texts/STATUS.md` | modified — `SPEC_READY` → `READY_FOR_ARCHITECT_REVIEW` |
| `Docs/Specs/Active/content_overlay_texts/IMPLEMENTER_REPORT.md` | modified — this file |
| `Docs/Specs/Active/content_overlay_texts/HEARTBEAT.log` | created — iter-1 baseline + progress |
| `Docs/Specs/Active/content_overlay_texts/screenshots/*.png` | created — EN + JA canonical frames (folder is gitignored, `.gitignore:252`) |

### Uncommitted paths outside this task's folder that are NOT mine (Rule 13)

The iter-1 kickoff baseline in `HEARTBEAT.log` records `HEAD = 0998ecb497f940ed8d0bca36aab34f77293314bb`
and its `git status --porcelain --untracked-files=all`. Against that baseline:

| Path | Status | Why it is not mine |
|---|---|---|
| `Docs/Specs/Active/content_cursor_per_catalog/SPEC.md` | ` M` | Present in the iter-1 baseline DIRTY block verbatim (`M Docs/Specs/Active/content_cursor_per_catalog/SPEC.md`). |
| `Docs/Specs/Queued/content_admin_panels_NOTE.md` | ` D` | Present in the iter-1 baseline DIRTY block verbatim (`D Docs/Specs/Queued/content_admin_panels_NOTE.md`). |
| `Docs/Versioning/last_uploaded_build.txt` | ` M` | Present in the iter-1 baseline DIRTY block verbatim (`M Docs/Versioning/last_uploaded_build.txt`). |
| `tasks/quit_transition_demo/quit_invariants.json` | `??` | Present in the iter-1 baseline DIRTY block verbatim (`?? tasks/quit_transition_demo/quit_invariants.json`). |
| `_to_delete/**` (12 files) | ` D` | **Appeared DURING the session, and is not in the baseline block.** HEAD moved mid-session from `0998ecb49` → `96c9e0d78 "Docs and Tellcode"` (Cesar's own commit, 08:55, three Docs files). `_to_delete/` no longer exists on disk. No command run this session touches that path — every write was under `Assets/Scripts/ContentRuntime/`, `Assets/Localization/LocalizationManager.cs`, `Assets/Scripts/Net/Endpoints.cs`, `Assets/Scripts/UI/BuildInfo/AppVersion.cs`, `Assets/Scenes/ShellScene.unity`, or the task folder. I cannot prove *who* removed it, only that nothing I ran could have. **Not staged, not reverted — surfaced for Cesar.** |

`Docs/CONTENT_PIPELINE_PLAN.md`, `Docs/TellCode.md` and `Docs/PERF_OPTIMIZATION_PLAN.md` were in the
baseline block and have since been committed by `96c9e0d78`, so they no longer appear.

## Screenshot

- **Canonical screenshot:** `screenshots/settings_overlay_applied_EN.png` — 1170×2532 (long edge 2532 ≥ 900). This is the frame that reveals the feature: five overlaid rows, two deliberately-refused rows, three untouched rows, all in one list.
- **Second frame:** `screenshots/settings_overlay_applied_JA.png` — 1170×2532, same session, after a mid-session language switch through the real `JapaneseButton.onClick`.
- **Captured at:** `Docs/Diagnostics/_capture/screenshot_2026-08-26_08-58-29.png` (EN) and `…_08-59-06.png` (JA), via `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` — the sanctioned path (Capture Rule 0). No hand-rolled `script-execute` capture.
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes (`IsPlaying=true, IsPaused=false`, `Application.runInBackground=true`)
- **Entry path:** real widget `onClick` only — `PersistentUI/TopBar/SettingsButton.onClick.Invoke()`, then `LanguageRow.onClick` → `JapaneseButton.onClick`. No synthetic/test-only buttons, no `ShowScreen()` shortcut.

What the canonical frame proves, read off the live `TMP_Text.text` values:

| Row rendered | Overlay row | Rule |
|---|---|---|
| `REMOTE SOUND SETTINGS` | `SETTINGS_SOUND`, active | overlay applies |
| `REMOTE LANGUAGE` | `SETTINGS_LANG`, active | overlay applies |
| `REMOTE ABOUT` | `SETTINGS_ABOUT`, active | overlay applies |
| `REMOTE CONTACT FORM` | `SETTINGS_CONTACT`, active, **plus unknown `Korean` + `notes` columns** | I4 — unknown columns ignored, row still applies |
| `TERMS OF USE` (bundled) | `SETTINGS_TERMS`, `is_active:false`, english `"SHOULD NOT APPEAR"` | I6 — deactivation keeps the bundled string; the remote value is nowhere on screen |
| `PRIVACY POLICY` (bundled) | `SETTINGS_PRIVACY`, `english:""` | blank english refused; bundled survives |
| `USER PROFILE`, `FAQ`, `LOG OUT` (bundled) | not in the overlay | I1 — the bundled table is the floor; untouched keys untouched |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Edit a string in the Texts panel, publish, relaunch → the new string renders | **PARTIAL — client half PASS, publish half NOT RUN** | The client half is proven end to end: a payload in the shape the live endpoint was verified to return (curl transcript below) was placed in the cache, and after relaunch five overlaid rows render in the live Settings UI (`screenshots/settings_overlay_applied_EN.png`; live `TMP_Text.text` values pasted above). The **admin-edit → publish → `content_rows`** half was NOT run: publishing writes the production Supabase catalog, an outward-facing action I will not take unilaterally. **Needs Cesar's go-ahead** — see § Open questions. |
| The same string in JA renders the JA value; switching language mid-session still works | **PASS** | After `JapaneseButton.onClick.Invoke()`, live labels read `REMOTE サウンド設定 / REMOTE 言語 / REMOTE アバウト / REMOTE お問い合わせ` while non-overlaid rows keep bundled JA (`利用規約`, `プライバシーポリシー`, `よくある質問`, `ログアウト`). `screenshots/settings_overlay_applied_JA.png`. Switching back to English restored all EN values in the same session. |
| **Airplane mode, cold launch, no cache** → bundled strings, no error, one warning | **PASS** | Deterministic: `ContentFetchPathTests.AirplaneMode_ColdLaunch_YieldsNullBody_AndWritesNoCache` drives a `ConnectionFailure` transport — body is null, **no cache file is created**, and the only log is the warning `[Content] Texts fetch failed (Network, HTTP 0): Cannot resolve destination host. Keeping the bundled strings and any existing cache.` (verbatim in the console output below). Real cold boot with no cache logged `[Content] No texts cache; using bundled strings. build=2302` and rendered bundled text. **On-device airplane mode is a manual-verification item.** |
| **Airplane mode with a warm cache** → the cached overlay still applies | **PASS** | Two independent facts: (a) the boot apply reads the cache *before any network call exists* — the 6-row apply is logged before `Fetching texts delta`, so it cannot depend on the fetch; (b) `ContentFetchPathTests.AirplaneMode_WarmCache_LeavesTheCacheIntact` asserts a failed fetch leaves the cached body byte-identical, and `ServerError_IsTreatedLikeOffline_TheCacheSurvives` covers HTTP 500 the same way. |
| Corrupt `content_texts.json` by hand → bundled strings, one warning, no exception | **PASS** | Real boot with a hand-truncated 168-byte cache: `[Content] Could not parse the texts payload: Unterminated string … Falling back to the bundled strings.` + `[Content] The texts cache could not be mapped; using bundled strings.` Live `Get()` returned `"Sound Settings" / "Music" / "Language" / "About" / "Contact Form"` (all bundled); `Source=Bundled`, `AppliedTextCount=0`; grep of the last 300 log lines for `Exception|NullReference` returned nothing. |
| `enabled: false` → next launch is bundled-only and the cache is gone | **PASS** | Real boot with an `enabled:false` cache: `[Content] Cached payload has enabled=false; dropping the cache and using bundled strings.` → `Source=Bundled`, `AppliedTextCount=0`, all six probed keys bundled. The fetch-side branch (the one an operator actually triggers) is covered by `ContentFetchPathTests.KillSwitch_MapsToDisabled_SoTheCallerDropsTheCache`, and `ContentTextsMapperTests.Map_KillSwitch_ShortCircuitsBeforeReadingCatalogs` proves rows sent *alongside* the flag are still refused. Server side implements it at `routers/content.py` (`if not meta.get("is_enabled", True): enabled = False; continue`). **Flipping the prod catalog to `is_enabled=false` was NOT done** — same reason as row 1. |
| A row with empty `english` is skipped, not applied | **PASS** | Visible in the canonical frame: `SETTINGS_PRIVACY` was sent active with `English:""` and the row still reads `PRIVACY POLICY` (bundled). Boot log counted it: `unusable=1`. Also `ContentTextsMapperTests.Map_EmptyEnglish_IsSkipped_BecauseBlankIsWorseThanBundled` (covers `""` and whitespace-only) and `LocalizationOverlayTests.ApplyOverlay_BlankEnglish_IsSkipped_AndTheBundledStringSurvives`. |
| `is_active = false` leaves the bundled string in place | **PASS** | Visible in the canonical frame: `SETTINGS_TERMS` was sent `is_active:false` with english `"SHOULD NOT APPEAR"`, and the row reads `TERMS OF USE` (bundled). That string appears nowhere on screen. Boot log counted it: `skipped inactive=1`. Also `ContentTextsMapperTests.Map_InactiveRow_IsIgnored_SoTheBundledStringStays`. |
| Missing/garbage `content_version.txt` → full payload requested, game still boots | **PASS** | Real boot with the bundled file replaced by garbage (`this is not a pair` / `texts=NOTANUMBER` / `=5`): three warnings, then `[Content] Fetching texts delta: since=texts:0, build=2302.` and `[Content] Texts cache refreshed from SERVER: catalog v11, full=True, 501 usable row(s).` — the full 95 KB catalog. Game booted normally, no exception. File restored afterwards; `git diff` on it is empty. Missing-file case covered by `ContentVersionFileTests` + the `Resources.Load` null branch. |
| `min_build` source resolved, named in the report, and a row above the build's number is not received | **PARTIAL — source resolved and named; the withhold could not be observed** | Source resolved and named above (`build_stamp.txt` → 2302). The **withhold could not be demonstrated because no such row exists**: I probed the live endpoint at `build=0,1,2113,2302,999999` and every request returned the same 501 rows with `min_build` range `0..0`. Every seeded texts row is `min_build = 0`, so the filter is a no-op on today's data. The mechanism is server-side and unbypassable (`routers/content.py:154,175` — `.lte("min_build", build)`), and the client sends the parameter correctly (`ContentFetchPathTests.FetchRoutine_SendsThePerCatalogCursor_AndNarrowsToTexts` asserts `build=2297` in the URL). **Observing the withhold needs a row published with `min_build > 0` — a prod write.** See § Open questions. |
| Execution order verified: `ApplyOverlay` runs AFTER `Initialize` (log both, paste the order) | **PASS** | Both lines pasted in § Execution order above, in order, from a real boot. Additionally asserted at runtime via `LocalizationManager.IsInitialized` (LogError if violated) and pinned by `LocalizationOverlayTests.Initialize_AfterApplyOverlay_WipesIt_WhichIsWhyOrderMatters`. |
| Boot time not measurably worse — the fetch is off the critical path (measure, don't assert) | **PASS, with one number worth Cesar's eye** | Measured, not asserted — see § Boot cost below. The fetch is definitively off the critical path: it is a coroutine, and the log timestamps show it completing ~1.4 s after `Awake` returned. Synchronous cost measured across four real boots: **1.22 ms** (no cache) · **8.58 ms** (corrupt) · **44.35 ms** (kill-switch, **zero rows, 101 bytes**) · **50.42 ms** (8 rows) · **64.49 ms** (real 501-row / 95 KB worst case). The 44 ms floor on a zero-row payload proves the bulk is a **one-time JSON-stack warm-up**, not row work — once warm, mapping 8 rows costs **0.098 ms** (mean of 50) and mapping all 501 costs **5.08 ms**. |
| Full unfiltered EditMode sweep green; new tests in a `Golfin.Content.Tests` asmdef | **PASS for this task; sweep is NOT globally green (pre-existing)** | Full unfiltered run: **1615 tests, 1595 passed, 17 failed, 3 skipped.** All 61 new tests pass. **All 17 failures and 3 skips are pre-existing** — see § Pre-existing test failures for the citation. |
| Screenshot of an admin-published string rendering in-game | **PASS (client half)** | `screenshots/settings_overlay_applied_EN.png`, 1170×2532, real play mode, real widget entry path. The rendered string came from a content-endpoint-shaped payload, not from a live publish — see row 1. |
| Spec deviations flagged at the bottom of the report | **PASS** | § Spec deviations below. |

## Boot cost — measured

| Scenario | Cache | Synchronous `Awake` cost |
|---|---|---|
| No cache (fresh install) | absent | **1.22 ms** |
| Corrupt cache (parse fails at byte 168) | 168 B | **8.58 ms** |
| Kill switch, **zero rows** | 101 B | **44.35 ms** |
| Warm cache, 8 rows | 1.5 KB | **50.42 ms** |
| Real full catalog, 501 rows | 95 KB | **64.49 ms** |

Marginal (warm) cost, measured in-process: `Map()` of 8 rows = **0.098 ms** (mean of 50);
`Map()` of a 501-row / 79 KB payload = **5.08 ms**.

Reading: a 101-byte, zero-row payload costing 44 ms while a 501-row payload costs 5 ms once warm
means ~43 ms is fixed first-parse cost (Newtonsoft assembly load + JIT + contract construction),
paid once per process, and the *content-dependent* part is ≤ 5 ms even in the worst case.

**What I could not isolate, stated plainly:** whether that ~43 ms is *net new* to boot or merely
*moved earlier*. `NoticeService` (order 0, same GameObject) already does `ToObject<RemoteNoticesDto>`
at `Awake` in the same frame, so before this change something else in boot paid the JSON-stack
warm-up; `ContentService` at `-900` is now simply the first to touch it. I tried to measure the two
cold contract builds directly, but the editor domain already had Newtonsoft warm (both came back
at 0.22 ms / 0.14 ms), and editor boot noise is far larger than 43 ms, so a wall-clock A/B could not
resolve it either. **If Cesar wants this off the boot path entirely**, the cheap fix is to parse the
cached body with `JToken` directly instead of `ToObject<T>` (skips contract construction), or to
defer the boot apply by one frame — but the second trades away the "no call-site changes" property,
because a screen could then draw a bundled string first. Not done without a spec.

## Pre-existing test failures — attribution

All 17 failures are in `Golfin.Save.Tests` (16) and `Golfin.Tournaments.WireupTests` (1), and none
of them can be reached by this task's changes.

- **Save (16 failures, all `Expected: 9 / But was: 10`).** `SaveSchemaMigrator.cs:18` ships
  `public const int CurrentSchemaVersion = 10;` **at HEAD**, committed in
  `baa9f123c fix(save): stamp fresh saves at the current schema — no more free Royal Swing`,
  while the tests still assert 9. Verified with `git grep -n "CurrentSchemaVersion *=" HEAD -- Assets/Scripts/Save`.
- **`Golfin.Tournaments.WireupTests…SnapshotHasCorrectStats` (`STR must be 6 / But was: 7`).**
  Same class of drift, in a tree this task does not touch.
- **Proof of non-involvement:** `git diff --stat HEAD -- Assets/Scripts/Save Assets/Scripts/Tournaments Assets/Scripts/TournamentsRuntime Assets/Scripts/Gameplay` produces **no output** — those trees are byte-identical to HEAD in my working copy.
- **The 3 skips** carry explicit `Stage C1 …` skip reasons committed long before this task.

**Tripwire that the new suite really runs in the unfiltered sweep** (per the known `tests-run`
behaviour of hiding passes): the *first* full sweep reported **18** failures, the extra one being
`Golfin.Content.Tests.ContentEndpointTests.Content_UsesThePerCatalogCursorForm_AndNarrowsToTexts`
(my own assertion pinned `%3A` while `UnityWebRequest.EscapeURL` emits lower-case `%3a`). After
fixing the assertion the count returned to 17. The new assembly is therefore demonstrably inside the
unfiltered run, not silently skipped. Filtered run: `Golfin.Content.Tests` → **61 passed, 0 failed**,
matching the 61 `[Test]` attributes in the folder exactly.

## Live endpoint verification (curl, 2026-08-26)

The DTOs were written against the observed response, not against the spec's prose:

```
$ curl -s "https://playlife-api.fly.dev/api/v1/content?since=texts:11&build=2301&catalogs=texts"
{"data":{"fetched_at":"2026-08-25T23:36:27.512950+00:00","enabled":true,"latest_version":11,
 "catalogs":{"texts":{"version":11,"full":false,"changed":[]}}}}

$ curl -s "https://playlife-api.fly.dev/api/v1/content?since=texts:0&build=2301&catalogs=texts"
{"data":{…,"catalogs":{"texts":{"version":11,"full":true,"changed":[
  {"id":"AUTH_CREATE_USERNAME_BODY","is_active":true,"min_build":0,
   "data":{"key":"AUTH_CREATE_USERNAME_BODY","English":"Please choose a username.…","Japanese":"…"}}, …]}}}}
```

**Bonus round-trip check (I3).** The garbage-cursor boot pulled the full 501-row catalog and applied
all 501 rows over the bundled table. Every probed key was byte-identical to its bundled value
(`BTN_START="PLAY"`, `SETTINGS_SOUND="Sound Settings"`, `HOME_NEXT_HOLE="NEXT HOLE"`,
`ROSTER_LEVEL_UP="LEVEL UP"`, `CLUB_COMPARE="COMPARE"`, `AUTH_LOGIN_TITLE="LOGIN WITH EMAIL"`) —
i.e. the published catalog and the shipped CSV currently agree exactly, so the export step has not
rotted.

## Spec deviations

1. **`min_build` source: `build_stamp.txt`, not a new `build_number.txt`.** SPEC §4 recommended
   baking a new file. Rejected because it would create a third source that can disagree with the
   other two — the exact problem §4 exists to solve. Full reasoning in § The `min_build` question.
2. **`LocalizationManager.ApplyOverlay` returns `int`, not `void`.** The spec sketched
   `public static void`. It returns the number of rows actually merged so `ContentService` can log
   what it *applied* rather than what it was *handed* — those differ precisely when a row was
   skipped, which is the case worth seeing in a log (`6 row(s) merged … skipped inactive=1,
   unusable=1`). Source-compatible: callers ignoring the return still compile.
3. **`RemoteContentSource.FetchRoutine` does NOT write the cache; `ContentService` does.**
   `RemoteNoticeSource.FetchRoutine` writes its own cache. Content cannot: the kill switch must
   *reject* a 200 body and **delete** the cache rather than store it, and that decision needs the
   parsed payload. The write therefore lives one level up. Everything else about the source is a
   shape-for-shape copy.
4. **`Endpoints.Content` gained an optional `catalogs` parameter.** SPEC §5 requires
   `catalogs=texts`; the existing two-arg builder could not express it. This is a client-side URL
   builder change only — the server already supports the parameter (`routers/content.py`,
   `catalogs: Optional[str] = Query(None)`), and the endpoint/panels/schema are untouched as
   required. Default `null` preserves the old shape (pinned by
   `ContentEndpointTests.Content_WithoutCatalogs_KeepsTheOldTwoArgShape`).
5. **The cursor sent is always the BUNDLED `texts=` version, never the version from a previous
   response** — even though the endpoint accepts the latter and its doc comment mentions
   "whichever is higher". This is load-bearing and is commented at the call site: the cache is a
   *whole-body mirror replaced wholesale*, so advancing the cursor would make the next response a
   different subset and writing it would drop rows the previous delta had applied. Replaying the
   bundled cursor keeps every cached body self-sufficient. Phase 2/3 will want the per-response
   cursor plus a merged store; that is their spec's problem.
6. **One extra `Stopwatch` in `ContentService`** to produce the boot-cost number the acceptance
   list asks to be *measured*. It adds one log line and one public diagnostic property.
7. **Added `LocalizationManager.IsInitialized`** (2 lines) so the execution-order invariant is
   asserted at runtime instead of trusted to an attribute.
8. **Not built: the live mid-session text swap.** Correct per §5 / I5, and stated here as the spec
   asks. `ContentService.OnCacheRefreshed` exists as the seam a future spec would hang it on;
   nothing subscribes to it.

## Console output

All `[Content]` lines produced across the six play-mode boots, verbatim. Every abnormal path is a
**warning**, never an error; no exceptions were logged in any run.

```
[LocalizationBootstrap] Startup language: English (saved=no, device=English)
[Content] Awake — LocalizationManager already initialised (order OK: LocalizationBootstrap -1000 → ContentService -900).
[Content] No texts cache; using bundled strings. build=2302
[Content] Boot critical-path cost: 1.22 ms (synchronous cache read + map + merge; the fetch below blocks nothing).
[Content] Fetching texts delta: since=texts:11, build=2302.
[Content] Texts cache refreshed from SERVER: catalog v11, full=False, 0 usable row(s). NOT applied this session by design (§2 I5) — it takes effect at next launch.

[Content] Texts overlay applied from DISK CACHE: 6 row(s) merged over the bundled table (catalog v12, full=False, skipped inactive=1, unusable=1).
[Content] Boot critical-path cost: 50.42 ms (synchronous cache read + map + merge; the fetch below blocks nothing).

[Content] Could not parse the texts payload: Unterminated string. Expected delimiter: ". Path 'data.catalogs.texts.changed[0].data.English', line 1, position 168.. Falling back to the bundled strings.
[Content] The texts cache could not be mapped; using bundled strings. It is left on disk in case a later build can read it.
[Content] Boot critical-path cost: 8.58 ms (synchronous cache read + map + merge; the fetch below blocks nothing).

[Content] Cached payload has enabled=false; dropping the cache and using bundled strings.
[Content] Boot critical-path cost: 44.35 ms (synchronous cache read + map + merge; the fetch below blocks nothing).

[Content] Skipping unparseable content_version line 'this is not a pair'.
[Content] Skipping content_version line 'texts=NOTANUMBER' — 'NOTANUMBER' is not an integer. 'texts' will be requested in full.
[Content] Skipping unparseable content_version line '=5'.
[Content] Fetching texts delta: since=texts:0, build=2302.
[Content] Texts cache refreshed from SERVER: catalog v11, full=True, 501 usable row(s). NOT applied this session by design (§2 I5) — it takes effect at next launch.

[Content] Texts overlay applied from DISK CACHE: 501 row(s) merged over the bundled table (catalog v11, full=True, skipped inactive=0, unusable=0).
[Content] Boot critical-path cost: 64.49 ms (synchronous cache read + map + merge; the fetch below blocks nothing).

(from the fake-transport EditMode tests)
[Content] Texts fetch failed (Network, HTTP 0): Cannot resolve destination host. Keeping the bundled strings and any existing cache.
[Content] Texts fetch failed (Server, HTTP 500): boom. Keeping the bundled strings and any existing cache.
```

## Open questions for Architect / Cesar

1. **The two acceptance items I did not close both require a PRODUCTION WRITE, and I did not make
   one without your go-ahead.** Say the word and I will run either or both:
   - *Edit → publish → relaunch.* Publishing a texts change bumps `texts` to v12 in the live
     Supabase catalog. Blast radius today is genuinely zero — **no shipped build contains
     `ContentService`**, so nothing in the field reads it — and the panel has rollback. But it is a
     write to production content, so it is your call, not mine.
   - *`min_build` withhold.* Cannot be observed at all until one row is published with
     `min_build > 0`; every one of the 501 rows in prod is `min_build = 0` today (probed at
     build 0/1/2113/2302/999999 — identical 501-row responses). One throwaway row at
     `min_build = 999999` would prove the filter in a single curl.
2. **Boot cost.** ~43 ms of one-time JSON-stack warm-up now lands at `-900`. I could not prove
   whether it is net-new or merely moved earlier off `NoticeService` (§ Boot cost explains why the
   measurement was inconclusive). If you want it gone, the `JToken`-direct parse is a contained
   change; I did not make it because it is not in the spec.
3. **API gap, reported per the spec's standing instruction — none found that blocks Phase 1.**
   The endpoint served everything the client needed: per-catalog cursor, `catalogs=` narrowing,
   `enabled` kill switch, `full` vs delta, `is_active`, and `min_build` echo. Two observations for
   the *next* spec rather than this one: (a) `data.catalogs[name]` is absent entirely when a
   catalog is killed, which is right but means the client must not read "absent" as "empty" — this
   build treats a missing `texts` object as *unparsed* so a good cache is never overwritten by it;
   (b) the seeded rows store literal `\n` two-character sequences rather than newlines, exactly as
   the bundled CSV does, so TMP's `parseCtrlCharacters` renders both paths identically — parity
   holds by construction, but a future non-TMP consumer would need to unescape.
4. **`_to_delete/` vanished from disk mid-session** and shows as 12 deletions. Nothing I ran touches
   that path; HEAD moved in the same window (your `96c9e0d78`). Left exactly as found — flagging so
   it is not swept into someone's next commit unnoticed.
