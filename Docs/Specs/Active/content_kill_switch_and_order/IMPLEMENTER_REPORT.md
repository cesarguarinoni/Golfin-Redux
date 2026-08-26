# Implementer Report — `content_kill_switch_and_order`

> SPEC_KIND: **backend**. No Figma node, no prefab, no screenshot deliverable — the § Screenshot,
> § Figma fidelity and § UI fidelity lint sections are deleted per the template's own instruction.
> The gate here is tests: a backend suite with a tripwire against the pre-fix router, an EditMode
> suite driving the production client types, and five consecutive play-mode boots.

## Implementation summary

Split the one kill switch that was doing two jobs, and broke the −100 tie.

**§1.** `GET /api/v1/content` derived top-level `enabled` by ANDing `content_catalogs.is_enabled`
across the catalogs the CLIENT HAD REQUESTED. `ContentService` requests all seven and drops every
cache on `enabled:false`, so disabling one catalog reverted all seven to bundled on every client.
`enabled` is now computed from the whole registry plus a new global flag row and never from the
requested subset; the per-catalog kill arrives as a top-level `disabled` list (a disabled catalog
stays ABSENT from `catalogs`, unchanged, because Phase 2's WITHDRAWN handling depends on it), and
every served catalog object carries `enabled: true`. The client reads both shapes and drops only
that catalog's cache.

**§2.** `CharacterManager` moved from −100 to −95 through `MonoImporter.SetExecutionOrder`, so it
is strictly after `SaveDataHost` (−100) and still ahead of `ClubDatabaseCSV` (−90). `SaveDataHost`
gained `IsLoaded` — `Instance` is assigned BEFORE `LoadData()`, so it never proved the save had
been read — and `CharacterManager` asserts on it in the same shape as the three Phase-2 asserts.

## Files modified or created

### `playlife` (backend repo, `~/Documents/playlife`)

| Path | Change |
|---|---|
| `backend/routers/content.py` | modified — `enabled` is now global-only (registry + `content_settings.content_enabled`, never the requested subset); new top-level `disabled` list; `enabled: true` on each served catalog; `_global_enabled()` reads the flag and fails OPEN |
| `backend/migrations/2026_08_26_content_global_kill_switch.sql` | created — `content_settings` table + the `content_enabled` row, RLS on, zero policies; idempotent; **APPLIED to prod by Cesar 2026-08-26** |
| `backend/tests/test_content_kill_switch.py` | created — 10 tests driving the real `get_content` against an in-memory Supabase fake; no network |

### `GolfinRedux` (Unity)

| Path | Change |
|---|---|
| `Assets/Scripts/ContentRuntime/RemoteContentDtos.cs` | modified — `RemoteContentDto.Disabled` (per-catalog kill) and `RemoteCatalogDto.Enabled`; the `Enabled` doc now says GLOBAL and why |
| `Assets/Scripts/ContentRuntime/ContentCatalogMapper.cs` | modified — `ContentPayload.Disabled` + `IsDisabled(name)`; `ContentCatalog.Enabled`; `Map` reads both fields |
| `Assets/Scripts/ContentRuntime/ContentService.cs` | modified — `DecideCatalogAction` (the per-catalog refresh branch, extracted so a test can drive it), `DropDisabledCatalog`, boot-path guard for a killed cached catalog, order header corrected to −95 |
| `Assets/Scripts/ContentRuntime/Tests/ContentPerCatalogKillTests.cs` | created — 10 EditMode tests over the production mapper / payload / decision |
| `Assets/Scripts/Save/SaveDataHost.cs` | modified — `IsLoaded`; `LoadData` is now a wrapper over `LoadDataCore` that raises it, so `ReloadFromDisk` marks a hand-built host loaded too |
| `Assets/Scripts/CharacterManager.cs` | modified — asserts `SaveDataHost.Instance.IsLoaded` before the clamp + overlay; clamp comment updated to the new order |
| `Assets/Scripts/CharacterManager.cs.meta` | modified — `executionOrder: -100` → `-95`, written by `MonoImporter.SetExecutionOrder` (not hand-edited) |
| `Assets/Scripts/Save/Tests/BootExecutionOrderTests.cs` | created — 4 EditMode tests pinning the boot chain and the `IsLoaded` member |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs` | modified — the two hand-built `SaveDataHost` harnesses now call `ReloadFromDisk()`, completing the boot EditMode's missing `Awake` never performs |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs` | modified — same one-line harness completion |

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Disable one catalog: top-level `enabled` stays true, that catalog is absent and reports disabled, others unaffected | PASS (unit + **live prod**) | `test_one_disabled_catalog_leaves_the_global_flag_true` — `enabled is True`, `disabled == ["bags"]`, `"bags" not in catalogs`, and `catalogs["clubs"]["changed"]` still carries its rows |
| Disable every catalog: top-level `enabled` is false | PASS | `test_every_catalog_disabled_is_a_global_kill` — all seven disabled → `enabled is False`, `catalogs == {}`. `test_empty_registry_…` guards the `all([])` degenerate case |
| Client drops only the disabled catalog's cache; the others keep applying | PASS | `Decision_KilledCatalog_DropsThatCacheOnly` — `DecideCatalogAction` (the method `RefreshRoutine` switches on) returns `DropDisabled` for `bags` and `Write` for `clubs`/`texts`; `ClearingOneCatalogsCacheLeavesTheOthersOnDisk` proves the file-level drop |
| `CharacterManager` at −95; assert fires if `SaveDataHost` has not run | PASS | `MonoImporter.GetExecutionOrder` reads back −95 (before=−100); `.cs.meta` on disk is `executionOrder: -95`. The assert is not theoretical — it FIRED on first run, failing 5 tournament tests whose harnesses had force-set `Instance` without ever loading a save (see § Console output) |
| Clamp runs deterministically across 5 consecutive play-mode boots | PASS | 5 boots of `ShellScene`, each showing `[SaveDataHost] Loaded save (schema v10, rp=123, holes=2)` → `[CharacterManager] Loaded 12 characters from CSV` → `[Content] Clamp (characters): …`, in that order, with zero `EXECUTION ORDER BROKEN` and zero `SaveDataHost.Instance is null` |
| `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy | PASS | Deployed 2026-08-26 (v50 → v51, image `deployment-01M0XVQSXZJVQQG2T71ZAR40DR` — verified by `flyctl status`, not by the exit code). All four probed live: **200 / 200 / 200 / 200** |
| Full unfiltered EditMode sweep green (baseline 1692 / 1689 / 0 / 3) | PASS | **1706 / 1703 / 0 / 3.** +14 = 10 `ContentPerCatalogKillTests` + 4 `BootExecutionOrderTests`; 0 failures, same 3 pre-existing skips |

### Backend suite (tripwired, not just green)

`cd backend && python -m pytest tests/test_content_kill_switch.py -q` → **10 passed in 0.15s**.

Run against `git show HEAD:backend/routers/content.py` (the pre-fix router) the same 10 tests give
**8 failed, 2 passed**, and `test_per_catalog_kill_is_not_global` fails exactly the way prod
measured on 2026-08-26 — `catalogs=bags,items` returns `enabled False` while `catalogs=items`
returns `True`. The suite bites; it is not a green rubber stamp.

## Known FAIL items

**None.** Both items that were outstanding at first write are now closed:

1. **Migration applied** by Cesar. Confirmed live over PostgREST:
   `content_settings.content_enabled = {"key": "content_enabled", "value": True}`, and the value
   arrives as a Python **bool**, not the string `"true"` — which matters, because
   `rows[0].get("value") is not False` would have read a string as enabled forever.
2. **Deployed and verified.** `playlife-api` v50 → **v51**, image
   `deployment-01M0XVQSXZJVQQG2T71ZAR40DR`, confirmed by `flyctl status` plus live probes rather
   than the deploy exit code.

## Live prod verification (2026-08-26, after deploy)

**The original measurement, re-run.** `bags` disabled through the service key, measured, restored
in a `finally` — the registry was read back all-enabled afterwards, and the API re-checked.

| request | SPEC §1 measured (before) | measured now |
|---|---|---|
| `catalogs=bags,items` | `enabled` **False** | `enabled` **True**, `disabled ['bags']`, served `['items']` |
| `catalogs=items` | `enabled` True | `enabled` **True**, `disabled ['bags']`, served `['items']` |
| all seven | `enabled` **False** | `enabled` **True**, `disabled ['bags']`, served the other six |

`disabled` is **identical across all three subsets** — the field describes the server, not the
request, which is the property the whole fix turns on. `bags` is absent from `catalogs` (unchanged,
as §1 requires) and the other six still serve.

**The global kill actually fires — and this is the check fail-open hides.** `_global_enabled()`
returns True on a successful read of `true`, on a failed read, AND on a non-bool truthy value, so
"enabled: true in prod" proves nothing on its own. Flipped `content_enabled` to false:

```
flag set false  -> {'key': 'content_enabled', 'value': False}
GLOBAL KILL: enabled = False   disabled = []   catalogs served = 7
RESTORED:      {'key': 'content_enabled', 'value': True}
post-restore:  enabled = True
```

`disabled` stays `[]` — no catalog is individually killed, which is exactly the separation this
task created. The catalogs still serialise under a global kill; the client short-circuits on
`enabled:false` before reading any of them (`ContentCatalogMapper.Map`, pinned by
`GlobalKill_StillShortCircuitsEverything`).

**Fail-open is not masking a broken read.** `flyctl logs` shows the content requests with **no**
`could not read content_enabled from content_settings` warning — so the row is genuinely being
read, not silently defaulting.

**Backward compatibility, observed rather than argued.** During the five play-mode boots the client
was talking to the PRE-deploy server, which had no `disabled` field: `Refresh complete: 7/7 catalog
cache(s) written`, nothing dropped. Covered as a test by `NoDisabledField_ReadsAsNothingKilled`.

## Spec deviations

**One, and it is a shape decision the spec left contradictory — please confirm.**

SPEC §1 asks for `"enabled": false` **in each catalog object** AND for a disabled catalog to stay
**absent** from `catalogs`. Those cannot both hold for a disabled catalog: an absent object carries
no fields. (The acceptance line — "that catalog is absent **and** reports disabled" — needs the
report to live somewhere other than inside the absent object.)

The spec also says the Phase-2 client "is already written to consume this — confirm before changing
the shape". **Confirmed: it is not.** `RemoteCatalogDto` had no `enabled` field, `ContentCatalog`
had no `Enabled`, and `ContentService.DropWithdrawnCatalog`'s own doc named the gap as needing "an
API change this spec is explicitly not allowed to make: a per-catalog `"enabled": false` inside the
catalog object, **or** a top-level `"disabled": ["clubs"]` list."

Resolution — implement both halves, which satisfies every line of the spec literally and leaves no
work wasted whichever the architect prefers:

* every **served** catalog object carries `"enabled": true` (the per-catalog field exists, uniform
  and readable unconditionally);
* every **disabled** catalog is absent from `catalogs` and named in a new top-level `"disabled"`
  list (the report the acceptance line asks for, and the shape the client's own TODO proposed).

The client honours BOTH: `ContentPayload.IsDisabled(name)` is true if the name is in `disabled` OR
if a served catalog carries `enabled:false`, so a later server-side choice between the two shapes
cannot wrong-foot a shipped build. Dropping one of the two fields later is a two-line change.

**Second, smaller:** the "new global flag" needed somewhere to live. It is a `content_settings`
row rather than an env var because §7.4 promises "one flag, no deploy" — an operator flips a row,
not a fly secret. It is deliberately not a `content_catalogs` column: `content_rows.catalog` has a
foreign key onto that table and the endpoint iterates it as the catalog list, so a synthetic
`__global__` row would need special-casing in three code paths.

## Console output

The §2 assert fired on its first run — 5 tests in `Golfin.Tournaments.WireupTests` failed with:

```
SetUp : Unhandled log message: '[Error] [CharacterManager] EXECUTION ORDER BROKEN: SaveDataHost
exists but has not finished loading, so this roster is about to be overlaid from save data that is
not the player's — and the clamp below would run against it. SaveDataHost must stay ahead of
CharacterManager (-100 vs -95, from their .cs.meta executionOrder fields).'
```

That is the assert working, not a false positive. Those harnesses build the singletons by hand
because EditMode never calls `Awake`: they `AddComponent<SaveDataHost>()`, force-set `Instance`
through its backing field, and hand it a `NullPersister` — but nothing ever read a save, so
`IsLoaded` was correctly false. Fixed by completing the fake boot (`host.ReloadFromDisk()`, the
public "load through the current persister" call), NOT by `LogAssert.Expect`-ing the error away —
silencing it would have left the harness an unfaithful stand-in for a boot and the assert
permanently expected-to-fail there.

After the fix the sweep is 0 failures, and no `EXECUTION ORDER BROKEN` line appears in any of the
five play-mode boots.

## Open questions for Architect

1. **Wire shape, per § Spec deviations** — keep both `disabled` (top-level) and `enabled` (per
   served catalog), or drop one? Both are implemented and both are consumed; I kept both because
   the spec's two bullets each demand one of them.
2. ~~**Who applies the migration and deploys.**~~ **DONE 2026-08-26** — Cesar ran the SQL, I
   deployed and ran the live acceptance (see § Live prod verification).
3. **Admin panel.** `Tools/admin-dashboard/lib/contentMutations.ts` can flip per-catalog
   `is_enabled` but has no control for the new global row. Out of this spec's scope; worth a Quick
   task so the "one flag, no deploy" promise has a button behind it.
