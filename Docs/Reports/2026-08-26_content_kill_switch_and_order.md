# `content_kill_switch_and_order` — architect report

**Date:** 2026-08-26 · **Spec:** `Docs/Specs/Active/content_kill_switch_and_order/`
**Status:** shipped to prod and verified; awaiting Cesar's sign-off.
**Implemented by:** Claude Code (main thread, direct — not the subagent pipeline; SPEC_KIND backend,
no Figma node, no screenshot deliverable).

Two small pre-existing fixes. The first gated the Phase-2 device pass.

---

## 1. The headline: the bug was in the PLAN, not the implementation

`CONTENT_PIPELINE_PLAN.md` §7.4, verbatim and unchanged:

> **Kill switch.** `content_catalogs.is_enabled=false` → the manifest reports `enabled:false` →
> clients ignore all remote content and run bundled until it is flipped back. One flag, no deploy.

That sentence describes **one** switch doing **two** jobs: a per-catalog column driving a global
client behaviour. `routers/content.py` implemented it faithfully — `enabled` was ANDed across the
requested catalogs — and `ContentService` implemented the client half faithfully too: it requests
all seven catalogs and drops **every** cache on `enabled:false`. Each half is correct against the
plan; together they mean **disabling one catalog reverted all seven to bundled on every client**.

This matters beyond the fix: the spec's framing ("the endpoint promises a per-catalog kill and
delivers a global one") locates the defect in the endpoint. It is actually in the plan of record,
which is why review never caught it — the code matched the document it was reviewed against.

**Ask #1: §7.4 needs rewriting** to describe two switches. Suggested replacement:

> **Kill switches, two of them.** `content_catalogs.is_enabled=false` kills ONE catalog: it
> vanishes from `catalogs` and is named in top-level `disabled`; that catalog reverts to bundled and
> no other is touched. `content_settings.content_enabled=false` is the GLOBAL kill: top-level
> `enabled` goes false and clients ignore all remote content until it is flipped back. Top-level
> `enabled` is NEVER a function of which catalogs the client requested. One flag each, no deploy.

Until §7.4 is amended, the shipped behaviour and the plan of record disagree.

---

## 2. What shipped

### §1 — per-catalog vs global kill

**Server** (`playlife/backend/routers/content.py`, deployed v51):

- Top-level `enabled` = `content_settings.content_enabled` **AND NOT** (every catalog in the
  registry disabled). Derived from the **whole registry**, never from `catalogs=` — so the same
  server state produces the same top-level answer for every client, whatever subset it asked for.
- New top-level `disabled: [names]` — every killed catalog in the registry, again regardless of
  what was requested.
- Each **served** catalog object carries `"enabled": true`.
- **A disabled catalog stays ABSENT from `catalogs`** — unchanged, per spec, because Phase 2's
  WITHDRAWN handling depends on it (cursor parity is present-and-empty, so absent already means
  "not served").

**Schema** (`backend/migrations/2026_08_26_content_global_kill_switch.sql`, applied by Cesar):
a `content_settings(key, value, updated_at)` table with the `content_enabled` row. RLS on, zero
policies — same posture as the four content tables.

Why a new table rather than a column: `content_rows.catalog` has a FK onto `content_catalogs` and
the endpoint iterates that table as the catalog list, so a synthetic `__global__` row there would
need special-casing in three code paths. Why a DB row rather than an env var: §7.4 promises "one
flag, no deploy" — an operator flips a row, not a fly secret.

`_global_enabled()` **fails OPEN** — missing table, missing row, or any exception reads as enabled.
`enabled:false` makes clients drop every cache, so failing closed would turn a transient PostgREST
blip into a global cache wipe for everyone fetching during it, with recovery only on the launch
after that. It also makes deploy order irrelevant: the API was safe to ship before or after the
migration.

**Client** (`Golfin.Content`): `RemoteContentDto.Disabled`, `RemoteCatalogDto.Enabled`,
`ContentPayload.Disabled` + `IsDisabled(name)`, `ContentCatalog.Enabled`. The per-catalog refresh
branch was extracted to `ContentService.DecideCatalogAction(payload, catalog, hasSlice)` —
`Write` / `DropDisabled` / `DropWithdrawn` — so the **branch order is a thing a test drives rather
than a thing a reviewer reads**. Disabled is asked first, which is load-bearing: a server that
served a killed catalog present-and-flagged would otherwise take the Write branch and cache content
an operator had just switched off.

A named kill now routes to `DropDisabledCatalog` (one cache, logged as a kill).
`DropWithdrawnCatalog` still handles the genuinely unexplained absence, and its log no longer lists
three possibilities as though equally likely.

### §2 — the −100 tie

`CharacterManager` −100 → **−95**, written by `MonoImporter.SetExecutionOrder` (not a hand-edited
`.meta`): strictly after `SaveDataHost` (−100), still ahead of `ClubDatabaseCSV` (−90).

New `SaveDataHost.IsLoaded`. This is the substantive half — `Instance` is assigned **before**
`LoadData()`, so `Instance != null` never proved the save had been read, and no existing signal did.
`LoadData` is now a wrapper over `LoadDataCore` that raises the flag, which also means
`ReloadFromDisk()` marks a hand-built host loaded. `CharacterManager` asserts on it in the same
shape as the three Phase-2 asserts.

---

## 3. Evidence

### Prod, before and after — the spec's own measurement, re-run

`bags` disabled through the service key, measured, restored in a `finally`; registry read back
all-enabled afterwards.

| request | SPEC §1 measured (before) | measured now |
|---|---|---|
| `catalogs=bags,items` | `enabled` **False** | `enabled` **True**, `disabled ['bags']`, served `['items']` |
| `catalogs=items` | `enabled` True | `enabled` **True**, `disabled ['bags']`, served `['items']` |
| all seven | `enabled` **False** | `enabled` **True**, `disabled ['bags']`, served the other six |

`disabled` is **identical across all three subsets**. That invariance — not the `True` values — is
the property the fix turns on, and it is what a future regression would break first.

### The global kill actually fires

Worth stating explicitly because fail-open hides it: `_global_enabled()` returns True on a good read
of `true`, on a **failed** read, and on a non-bool truthy value. "Prod says `enabled: true`" is
therefore evidence of nothing. Flipped the row:

```
flag set false -> {'key': 'content_enabled', 'value': False}
GLOBAL KILL: enabled = False   disabled = []   catalogs served = 7
RESTORED     -> {'key': 'content_enabled', 'value': True}
```

`disabled` stays `[]` — no catalog individually killed, which is exactly the separation this task
created. The value arrives as a real `bool`, not the string `"true"` (a string would have made
`is not False` true forever). `flyctl logs` shows no `content_settings` warning, confirming the row
is genuinely read rather than silently defaulting.

### Tests

- **EditMode, full unfiltered sweep: 1706 / 1703 passed / 0 failed / 3 skipped.** Baseline was
  1692 / 1689 / 0 / 3; +14 is exactly the new tests (10 `ContentPerCatalogKillTests`,
  4 `BootExecutionOrderTests`), same 3 pre-existing skips.
- **Backend: 10 passed**, driving the real `get_content` coroutine against an in-memory Supabase
  fake — no network, no creds. **Tripwired**: run against `git show HEAD:backend/routers/content.py`
  the same 10 give **8 failed / 2 passed**, and `test_per_catalog_kill_is_not_global` fails
  reproducing the prod measurement exactly. The suite bites.
- **Clamp determinism:** 5 consecutive `ShellScene` play-mode boots, each
  `[SaveDataHost] Loaded save` → `[CharacterManager] Loaded 12 characters from CSV` →
  `[Content] Clamp (characters)`, in that order, zero `EXECUTION ORDER BROKEN`, zero
  `SaveDataHost.Instance is null`.
- **Backward compatibility, observed rather than argued:** those five boots ran against the
  *pre-deploy* server with no `disabled` field — `Refresh complete: 7/7 catalog cache(s) written`,
  nothing dropped. Pinned by `NoDisabledField_ReadsAsNothingKilled`.
- **Deploy verified by `flyctl status` image version + live probes, never the exit code**
  (v50 → v51, `deployment-01M0XVQSXZJVQQG2T71ZAR40DR`). `/health`, `/notices`, `/banners`,
  `/tournaments/golfin` → 200 / 200 / 200 / 200.

Repro:

```bash
cd /Users/cesar/Documents/playlife/backend && python -m pytest tests/test_content_kill_switch.py -q
```

---

## 4. The decision the architect needs to make

**Ask #2: the SPEC's wire shape is self-contradictory. Both readings are implemented; pick one or
keep both.**

SPEC §1 asks for two things that cannot both hold for a disabled catalog:

- *"Per-catalog `"enabled": false` in each catalog object"*
- *"A disabled catalog stays **absent** from `catalogs`"*

An absent object carries no fields. The acceptance line — "that catalog is absent **and** reports
disabled" — needs the report to live somewhere other than inside the absent object.

The spec also says the client "is already written to consume this — confirm before changing the
shape". **Confirmed: it was not.** `RemoteCatalogDto` had no `enabled` field and `ContentCatalog`
had no `Enabled`; `ContentService.DropWithdrawnCatalog`'s own doc named the gap as needing "an API
change this spec is explicitly not allowed to make: a per-catalog `"enabled": false` inside the
catalog object, **or** a top-level `"disabled": ["clubs"]` list."

Resolution taken — implement **both**, which satisfies every line of the spec literally:

- served catalogs carry `"enabled": true` (the per-catalog field exists and is uniformly readable);
- disabled catalogs are absent and named in top-level `"disabled"` (the report the acceptance line
  requires, and the shape the client's own TODO proposed).

`IsDisabled(name)` honours both, so a later server-side choice between the shapes cannot wrong-foot
a shipped build. **Dropping either is a two-line change**, and no shipped build contains
`ContentService` yet, so the window to simplify is open now and closes at first release. My
recommendation: keep both — the cost is one always-`true` field, and the uniform
`catalogs[name].enabled` read is what makes the client shape-agnostic.

---

## 5. Findings that belong to other specs

1. **The `.cs.meta` execution-order fragility is now partly pinned, which should re-scope its own
   task.** It remains true that this project has no `ProjectSettings/MonoManager.asset`, that every
   order lives in a committed `.cs.meta`, and that only `SaveDataHost`'s is re-asserted on reload
   (`SaveDataHostExecutionOrder`, `[InitializeOnLoad]`). `CharacterManager` at −95 inherits exactly
   the same fragility. **But** `BootExecutionOrderTests` now reads the orders back through
   `MonoImporter` and fails if the chain stops being monotonic — so a regenerated `.meta` is caught
   by the test suite rather than by a player with no clubs. That is detection, not prevention; the
   separate task can now be scoped as "re-assert on reload" rather than "make it detectable".

2. **The admin dashboard has no control for the global flag.**
   `Tools/admin-dashboard/lib/contentMutations.ts` toggles per-catalog `is_enabled` only. §7.4's
   "one flag, no deploy" currently has no button behind it for the global kill — it needs a SQL
   `update`. Worth a Quick task.

3. **Three EditMode harnesses fake a `SaveDataHost` boot, and the pattern will keep biting.**
   `TournamentServiceWireupTests` (×2) and `TournamentRestrictionsClientTests` `AddComponent` the
   host, force-set `Instance` through its backing field, and hand it a `NullPersister` — EditMode
   never calls `Awake`. The new assert failed all five of their tests on first run because nothing
   had read a save. **Fixed by completing the fake boot (`ReloadFromDisk()`), not by
   `LogAssert.Expect`-ing the error away** — silencing it would have left the harnesses unfaithful
   stand-ins and the assert permanently expected-to-fail there. Any future boot-order assert will
   hit the same three harnesses; a shared `TestBoot.SaveDataHost()` helper would stop this
   recurring.

---

## 6. Residual risk and ops notes

- **A kill is not instant.** 60 s response cache, and the client applies caches at boot (I5, no live
  swap) — so a per-catalog kill takes up to 60 s to reach a client and lands at its **next** launch.
  Re-enabling costs another launch to refetch. Set expectations accordingly during the device pass.
- **One extra Supabase round trip per content request** (`_global_enabled`). A single indexed PK
  lookup against a one-row table; unmeasured but structurally negligible next to the catalog reads.
  Flagging it rather than defending it.
- **Fail-open is a deliberate asymmetry.** An unreadable flag serves content. If the intent is ever
  "a kill switch must win even when the DB is unreachable", this is the line to revisit — but that
  would mean a DB blip wipes every player's caches, which I do not think is what §7.4 wants.
- **Not covered by any test:** an operator disabling a catalog and re-enabling it *within* one
  client's cache window. Behaviour is benign (the client either sees the kill or does not) but it is
  untested.
- **Out of scope and untouched, per spec:** boot cost, player inventory, Addressables, art URLs, the
  general `.cs.meta` fragility.

---

## 7. State

- `STATUS.md` = `READY_FOR_ARCHITECT_REVIEW`; every acceptance item PASS, nothing outstanding.
- Prod verified clean after both flips: `enabled True`, `disabled []`, 7 catalogs served, all
  enabled.
- **Nothing is committed.** `Assets/Scripts/ContentRuntime/` is entirely untracked in-flight work
  from another session, so staging the edited files there would sweep that feature into this
  commit. The `playlife` repo is clean apart from this task and can be committed on its own. Awaiting
  Cesar's call.
- The Phase-2 device pass is **unblocked** — flipping the per-catalog kill on a phone will now
  revert one catalog, not seven.
