# SPEC — `content_kill_switch_and_order`

SPEC_KIND: backend

> Two small fixes. Both are pre-existing, both verified against prod 2026-08-26, and the first
> **gates the Phase-2 device pass** — testing the per-catalog kill before this lands would "pass"
> while doing something far larger than intended.

## Status

`SPEC_READY`.

## 1. Per-catalog kill is global in effect

Measured on `playlife-api` with `bags` disabled, then restored:

| request | top-level `enabled` |
|---|---|
| `catalogs=bags,items` | **False** |
| `catalogs=items` | True |
| all seven | **False** |

Top-level `enabled` is an AND across the **requested** catalogs. `ContentService` requests all six
and Phase 1 drops the cache on `enabled:false`, so **disabling one catalog reverts every catalog to
bundled on every client**. `CONTENT_PIPELINE_PLAN.md` §7.4 promises a per-catalog kill; this is a
global one. (Degrades to the bundled floor, so it is wrong rather than dangerous, and no shipped
build contains `ContentService` yet.)

**Fix — `playlife/backend/routers/content.py`:**

- Per-catalog `"enabled": false` in each catalog object (the Phase-2 client is already written to
  consume this — confirm before changing the shape).
- Top-level `enabled` becomes a **genuine global kill only**: true unless every catalog is
  disabled, or a new global flag says otherwise. Never derived from the requested subset.
- A disabled catalog stays **absent** from `catalogs` — that behaviour is correct and Phase 2's
  WITHDRAWN handling depends on it. Verified: disabled ⇒ absent, cursor-parity ⇒ present-and-empty.
  Do not change it.
- Client: a catalog reported disabled drops **that catalog's** cache only.

## 2. `CharacterManager` and `SaveDataHost` are both at −100

A tie. `CharacterManager` reads `SaveDataHost.Instance.Data` behind a null guard, so losing the tie
means the Phase-2 clamp **silently does not run** — the save keeps out-of-range values until a
launch where the tie falls the other way. Non-deterministic clamping is harder to diagnose than a
crash.

Order today (all of it in `.cs.meta` `executionOrder:` — this project has **no**
`ProjectSettings/MonoManager.asset`):

```
CharacterDatabaseCSV -200 · CharacterManager -100 · SaveDataHost -100 · ClubDatabaseCSV -90 · ClubManager -80
```

**Fix:** move `CharacterManager` to **−95** — strictly after `SaveDataHost`, still before the club
pair. Add a runtime assert that `SaveDataHost` ran first, matching the three asserts Phase 2 added.

## Acceptance

- [ ] Disable one catalog: top-level `enabled` stays **true**, that catalog is absent and reports disabled, others unaffected
- [ ] Disable every catalog: top-level `enabled` is false
- [ ] Client drops only the disabled catalog's cache; the others keep applying
- [ ] `CharacterManager` at −95; assert fires if `SaveDataHost` has not run
- [ ] Clamp runs deterministically across 5 consecutive play-mode boots
- [ ] `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy
- [ ] Full unfiltered EditMode sweep green (baseline 1692 / 1689 / 0 / 3)

## Out of scope

- The `.cs.meta`-only execution-order fragility in general (a regenerated `.meta` silently gives a
  player no clubs). Real, pre-existing, **its own task** — fix only the −100 tie here.
- Boot cost (40 ms fresh-install clubs parse), player inventory, Addressables, art URLs.
