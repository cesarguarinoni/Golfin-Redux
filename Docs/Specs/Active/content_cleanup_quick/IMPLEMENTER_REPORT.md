# content_cleanup_quick — Implementer report

**Iteration shape:** `content-pipeline:five-small-cleanups`
**Date:** 2026-08-26
**Spec:** `Docs/TellCode.md` § `content_cleanup_quick`; decisions of record for items 4 and 5 in
`Docs/CONTENT_PIPELINE_PLAN.md` §6.5. No spec folder was authored — the items are small and the
pointer is the spec.

---

## Preflight baseline

Captured before the first edit:

```
GolfinRedux HEAD  dc9281bc4a8bc30d98f67c2c7e0d6c2ef198a33d
playlife    HEAD  4bd745b49f9293e78986689264f98f9d50784a2c   (working tree CLEAN)
```

GolfinRedux DIRTY at baseline (none of it mine, none of it touched):

```
 M Assets/Localization/LocalizationManager.cs
 M Assets/Scenes/ShellScene.unity
 M Assets/Scripts/Gameplay/Tests/StaminaLiveWiringTests.cs
 M Assets/Scripts/Save/Tests/ClubOwnershipTests.cs
 M Assets/Scripts/Save/Tests/GachaTicketTests.cs
 M Assets/Scripts/Save/Tests/SaveLayerTests.cs
 M Assets/Scripts/Tournaments/Tests/Golfin.Tournaments.Tests.asmdef
 M Assets/Scripts/UI/BuildInfo/AppVersion.cs
 M Docs/TellCode.md
 M Docs/Versioning/last_uploaded_build.txt
 D _to_delete/…                                (13 paths)
?? Assets/Scripts/Tournaments/Tests/TournamentSnapshotImmunityTests.cs(+.meta)
?? Docs/DEVICE_PASS_CONTENT_PIPELINE.md
?? Docs/Reports/perf_baseline_2026-08-26.md
?? tasks/quit_transition_demo/
```

Every path in that block is still exactly as it was. `Assets/Scenes/ShellScene.unity` in particular
was already modified at baseline and was NOT written to by this task — `scene-list-opened` reported
`IsDirty: false` throughout, and no scene-save was performed.

---

## Item 1 — drop the per-catalog `enabled` field

**PASS.** A disabled catalog is ABSENT from `catalogs` (named in the top-level `disabled` list), so
a catalog that reaches the serialiser could only ever carry `enabled: true`. A boolean that is true
by construction reads as a guard, and the guard would never fire — so it is gone from the wire and
from both client types. Top-level `disabled[]` is untouched and `IsDisabled(name)` already read it,
so no correctness moved.

| Layer | Change |
|---|---|
| playlife `routers/content.py` | the `"enabled": True` line dropped from the per-catalog object; module + endpoint docstrings corrected |
| `RemoteContentDtos.cs` | `RemoteCatalogDto.Enabled` removed, replaced by a comment saying why it must not come back |
| `ContentCatalogMapper.cs` | `ContentCatalog.Enabled` and its ctor param removed; `IsDisabled` is now `Disabled.Contains(name)` alone |
| `ContentService.cs` | two docstrings that described the "present-and-flagged" wire shape corrected |

A stray `enabled` still arriving on the wire is IGNORED like any other unknown field (I4) — never a
parse failure and never a second, quieter kill switch. That is pinned by
`AStrayPerCatalogEnabledField_IsIgnored_NotAKill`, which replaces the old
`ACatalogServedWithEnabledFalse_IsAlsoReadAsDisabled` (that test asserted exactly the behaviour this
item removes).

**Evidence — backend, tripwired.** `test_a_served_catalog_carries_no_enabled_field` was added and
proven load-bearing by re-adding the field:

```
E   AssertionError: balls still ships a per-catalog `enabled`
FAILED tests/test_content_kill_switch.py::test_a_served_catalog_carries_no_enabled_field
1 failed, 10 passed
```
Field removed again → `11 passed`.

**Evidence — client, tripwired.** See § Tripwire run below: forcing `IsDisabled` to `false` turns
4 `ContentPerCatalogKillTests` red, so the suite really is driving the production decision.

## Item 2 — dashboard control for the GLOBAL kill switch

**PASS.** `content_settings.content_enabled` now has a button. It previously needed a hand-written
SQL `update`, which does not meet §7.4's "one flag, no deploy" in any sense an operator at 2am
would recognise.

| Layer | Change |
|---|---|
| `lib/contentData.ts` | `fetchGlobalContentEnabled()`, folded into `fetchCatalogs()` as `globalEnabled`. FAILS OPEN on a missing table/row, matching the endpoint's `_global_enabled()` — a panel that showed OFF on an unreadable flag would send an operator to fix a switch that is already on |
| `lib/contentMutations.ts` | `setGlobalContentEnabled()` — upsert (not update: on a project where the migration has not run, the flip must CREATE the row), audited under its own action `content.global_enabled`, distinct from the per-catalog `content.enabled:<catalog>` |
| `app/api/content/enabled/route.ts` | new — POST, admin-only. No catalog segment, because the flag is not a property of any catalog |
| `publish-drawer.tsx` | a second card in the existing Kill switch tab, tagged `ALL CATALOGS`, red-bordered when off, with a confirm on the way OFF and one click on the way back ON |
| `catalog-panel.tsx` | `GlobalKillBanner` above everything else when the global switch is off |
| `lib/i18n.ts` | 10 new keys, EN + JA |

Both switches are shown together and never merged, each stating its own blast radius. That is
deliberate: `content_kill_switch_and_order` exists because a per-catalog column was quietly doing a
global job, and "kill switch" as a bare phrase is what allowed it.

Also corrected two stale doc comments that still claimed the per-catalog kill drops the top-level
`enabled` flag (`app/api/content/[catalog]/enabled/route.ts`, `lib/contentMutations.ts`) — that
wording *was* the bug.

**Evidence — routes, mock mode, live server:**

```
POST /api/content/enabled {"enabled":false} → 200, globalEnabled=false
        per-catalog isEnabled UNCHANGED (balls stayed false, the other six stayed true)
POST /api/content/enabled {}                → 400 "enabled (boolean) is required."
POST /api/content/enabled (no cookie)       → 401 "Not signed in."
POST /api/content/enabled {"enabled":true}  → 200, globalEnabled=true
GET  /api/audit                             → "content.global_enabled" rows present
```

**Evidence — the real UI.** Signed in through the real mock-login form and drove the real widgets:

* `/clubs` ▸ Review & publish ▸ Kill switch shows BOTH cards — "Serve this catalog to the game"
  (ON, Stop serving) and "Serve remote content at all" `ALL CATALOGS` (ON, Kill remote content).
* Clicking the global button fires the confirm with the full text
  ("Kill remote content for EVERY catalog and EVERY player? …").
* After confirming: the global card goes red and reads OFF, its button becomes "Resume remote
  content", the per-catalog card is untouched and still reads ON, and the red banner
  "⚠ Remote content is OFF for every player" appears at the top of the Clubs panel.
* Switched to 日本語 and re-checked: the banner and both cards render in JA.
* Pressed "Resume remote content" — card back to ON, banner gone.

## Item 3 — shared `TestBoot.SaveDataHost()` helper

**PASS.** New assembly `Golfin.TestSupport` (Editor-only, `UNITY_INCLUDE_TESTS`) holding
`TestBoot.SaveDataHost()` and `NullPersister`. It returns an `IDisposable` lease, so a fixture's
TearDown is one call and a mid-test failure still cannot leak a destroyed host into the next
fixture.

⚠️ **There were FOUR harnesses, not three.** The task named three; `TournamentRestrictionsClientTests`
(`IneligibleConfirmWidgetTests`) is a fourth copy of the same reflection dance, added since. It is
converted too — the item's whole rationale is "any future boot-order assert hits all of them again",
and leaving the one nobody had counted would have defeated it.

| Fixture | Was |
|---|---|
| `TournamentServiceWireupTests` | hand-rolled clear + AddComponent + persister + ReloadFromDisk + force-set + restore |
| `RealItemRewardAdapterTests` | the same, duplicated |
| `ApplyServerBalanceTests` | the same **minus `ReloadFromDisk()`** — it was booting a host that had never read a save |
| `TournamentRestrictionsClientTests` | the same, plus its own `SetSaveDataHost` helper (now deleted) |

That divergence is not hypothetical: `content_kill_switch_and_order` §2 added a
`SaveDataHost.IsLoaded` assert to `CharacterManager`, two of the four copies grew a
`ReloadFromDisk()` call, and two did not. There is now one copy.

**Evidence — tripwired.** Deleting `host.ReloadFromDisk()` from `TestBoot` turns 5 tests across
**3 different fixtures** red with the production assert:

```
Golfin.Tournaments.WireupTests.TournamentServiceWireupTests.Compose_ReturnsNonNull_With6Tournaments
Golfin.Tournaments.WireupTests.TournamentServiceWireupTests.Compose_Register_SnapshotHasCorrectStats
Golfin.Tournaments.WireupTests.IneligibleConfirmWidgetTests.CONFIRM_on_a_level_ineligible_tournament_registers_nothing
Golfin.Tournaments.WireupTests.IneligibleConfirmWidgetTests.CONFIRM_on_a_rarity_ineligible_tournament_registers_nothing
Golfin.Tournaments.WireupTests.IneligibleConfirmWidgetTests.CONFIRM_on_an_unrestricted_tournament_still_registers

  SetUp : Unhandled log message: '[Error] [CharacterManager] EXECUTION ORDER BROKEN: SaveDataHost
  exists but has not finished loading …'
```

That is the proof the helper is the shared boot AND that its load step is load-bearing.

## Item 4 — revoke an UNAPPLIED grant from the Users drawer

**PASS.** Per §6.5 decision 3: no separate panel, a Revoke action on the existing drawer.

| Layer | Change |
|---|---|
| `lib/inventoryMutations.ts` | `revokeInventoryGrant()` — read-then-delete, scoped by `user_id` AND `applied_at is null`, audited as `inventory_grant_revoke` with the full row in the audit's `before` |
| `app/api/users/[id]/inventory/route.ts` | new `DELETE` handler, admin-only |
| `inventory-tab.tsx` | a Revoke button on PENDING grant rows only |
| `user-drawer.tsx` | a `revokeGrant` pending-modal kind reusing the existing `ConfirmActionModal` |
| `lib/mockInventory.ts` | the fixture grant id made a VALID uuid (it was `…00000000mock`, which the route rejects before reaching the mock branch); a second, ALREADY-APPLIED fixture grant added so both states are visible |

Two design points worth stating because they are easy to get wrong later:

* **It is a DELETE, not a `revoked_at` column.** The queue's contract on both the client and the API
  is `applied_at is null` ⇒ pending; a third state would need special-casing in the drain AND the
  ack for a row nobody reads again. No history is lost — `admin_audit_log` keeps the row.
* **`applied_at is null` is repeated on the delete after the read.** They are two statements, and a
  boot in between is exactly the window where a tester drains the grant. Without the repeat, the
  delete would win that race and the operator would be told "revoked" while the player held it.

**Evidence — all four outcomes, mock mode, live server:**

```
DELETE {"grantId":"…dead"}  (pending) → 200 "Revoked 9999× item_MOCK_NOT_REAL (item) before it was applied."
                                        grants list goes from 1 pending → 0
DELETE {"grantId":"…dead"}  (again)   → 404 "That grant no longer exists."
DELETE {"grantId":"…beef"}  (applied) → 409 "That grant has already been applied — the player has it…"
DELETE {"grantId":"nope"}             → 400 "grantId (uuid) is required."
DELETE (no cookie)                    → 401 "Not signed in."
GET /api/audit                        → "inventory_grant_revoke" row present
```

**Evidence — the real UI.** Users ▸ ken ▸ Inventory ▸ GRANTS shows the PENDING row with a Revoke
button and the APPLIED row **without one**. Clicking Revoke opens "Revoke this grant?" naming
`9999× item_MOCK_NOT_REAL (item)`; confirming removes it from the list, leaving only the applied
row.

## Item 5 — log every merge that RAISES a quantity

**PASS.** §6.5 decision 1: the refund is an accepted trade for the beta; not knowing how often it
fires is not, because beta consumption figures are what tune the economy.

| Layer | Change |
|---|---|
| `InventoryRaise.cs` | new — `{Kind, Id, From, To}`, `Item`/`Ball`/`Ticket` |
| `InventoryProjector.Apply` | optional `List<InventoryRaise>? raises` collector; `RaiseQuantity` and the ticket branch append to it |
| `InventorySyncService` | `ApplyAndCount()` wraps BOTH merge sites; `Debug.LogWarning` per raise plus an `OnQuantitiesRaised` seam |
| `TelemetryConfig.cs` | `TelemetryEventNames.InventoryMergeRaise = "inventory_merge_raise"` |
| `TelemetryHooks.cs` | one telemetry row per raised stack: `kind`, `item`, `from`, `to`, `delta` |

Four decisions, each of which changes what the number means:

* **Only a key the save ALREADY held counts.** A quantity arriving on a key this device does not
  have is a RESTORE — a fresh install pulling its inventory back — which is the feature working.
  Counting it would bury the refund signal under every reinstall.
* **Levels and SP are excluded.** Nothing consumes them, so a raise there can never be a refund.
* **BOTH merge sites are counted**, not just the obvious one. The stale-PUT retry is where a refund
  is most likely, but the boot restore can do it too (a reinstall restoring a blob written before
  this device's last spend), and counting only the obvious site would undercount by exactly the
  cases nobody expected.
* **The player is NOT in the payload.** `/telemetry/events` stamps `user_id` from the bearer token
  and ignores any id in the body, so a copy here would be a second, lower-trust source for a column
  the server already fills correctly. "With player" is satisfied by the row's own `user_id`.

The telemetry wiring is a seam assigned from `TelemetryHooks` (the one place every telemetry event
is wired), not a direct call — `Golfin.InventorySync` stays constructible in an EditMode test with
no play mode and no network. It is `=` and not `+=` on purpose: a domain reload between edit and
play can leave a stale delegate, and a double-subscribe would double the very number this exists to
read. A handler that throws is swallowed with a warning, because the merge is already applied to the
save by then.

**Evidence — 7 new EditMode tests** (`InventoryProjectorTests` ×4, `InventorySyncServiceTests` ×3),
driving the production `InventorySyncService.Boot` / `Push` through the existing `FakeTransport` —
not a reimplementation. Tripwired: deleting the `raises?.Add` in `RaiseQuantity` turns 4 of them red,
including the two service-level ones:

```
InventoryProjectorTests.A_raised_quantity_on_a_key_we_already_held_is_reported
    the refund path must produce exactly one row · Expected: 1 But was: 0
InventoryProjectorTests.Balls_and_tickets_are_counted_too_and_a_no_op_merge_counts_nothing
    Expected: equivalent to <"Ball:ball_pro 1->4", "Ticket:0 10->12">  But was: <"Ticket:0 10->12">
InventorySyncServiceTests.A_stale_merge_that_refunds_a_consumed_item_is_reported
    the stale-merge refund must be reported exactly once · Expected: 1 But was: 0
InventorySyncServiceTests.A_boot_merge_that_raises_a_held_quantity_is_reported_too
    Expected: 1 But was: 0
```

(The `Ticket` row surviving is correct — the ticket branch has its own `raises?.Add`, which the
tripwire did not touch. It is the tripwire being precise, not a gap.)

---

## Tripwire run — the whole suite, deliberately broken then restored

Three breaks armed at once, one EditMode run, then reverted:

| Break | Fired |
|---|---|
| `IsDisabled(name) => false` | 4 × `ContentPerCatalogKillTests` (item 1) |
| `raises?.Add` deleted from `RaiseQuantity` | 4 × inventory raise tests (item 5) |
| `host.ReloadFromDisk()` deleted from `TestBoot` | 5 × tests in 3 fixtures (item 3) |

```
Tripwired:  1768 total · 1752 passed · 13 FAILED · 3 skipped
Restored:   1768 total · 1765 passed ·  0 failed · 3 skipped
```

The 3 skips are pre-existing `HoleCompleteDriverTests` Stage-C1 skips and are unrelated to this
task.

## Acceptance tests — final state

| Suite | Command | Result |
|---|---|---|
| Unity EditMode | `tests-run EditMode` | **1768 total · 1765 passed · 0 failed · 3 skipped** |
| playlife backend | `python -m pytest tests/ -q` | **26 passed** |
| dashboard types | `npx tsc --noEmit` | clean, no output |
| dashboard build | `npm run build` | clean; `ƒ /api/content/enabled` present in the route manifest |

`tests-run` reports only failures and skips, never the passing names, so every new suite above was
proven by the tripwire rather than by the summary line.

## Files modified or created

### GolfinRedux — Unity

| File | 1-line summary |
|---|---|
| `Assets/Scripts/ContentRuntime/RemoteContentDtos.cs` | dropped `RemoteCatalogDto.Enabled`; left a comment saying why it must not return |
| `Assets/Scripts/ContentRuntime/ContentCatalogMapper.cs` | dropped `ContentCatalog.Enabled` + ctor param; `IsDisabled` now reads the top-level `disabled` list alone |
| `Assets/Scripts/ContentRuntime/ContentService.cs` | corrected two docstrings that described the removed "present-and-flagged" wire shape |
| `Assets/Scripts/ContentRuntime/Tests/ContentPerCatalogKillTests.cs` | fixtures no longer carry per-catalog `enabled`; the honour-a-false-flag test replaced by an ignore-a-stray-field test |
| `Assets/Scripts/TestSupport/Golfin.TestSupport.asmdef` | **new** — Editor-only, `UNITY_INCLUDE_TESTS`, references `Golfin.Save` |
| `Assets/Scripts/TestSupport/TestBoot.cs` | **new** — `TestBoot.SaveDataHost()` + `NullPersister`; the one fake host boot, leased and disposable |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentServiceWireupTests.cs` | two fixtures moved onto `TestBoot`; local `NullPersister` deleted |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs` | the fourth harness moved onto `TestBoot`; its `SetSaveDataHost` helper deleted |
| `Assets/Scripts/TournamentsRuntime/Tests/Golfin.TournamentsRuntime.Tests.asmdef` | + `Golfin.TestSupport` reference |
| `Assets/Scripts/EconomyRuntime/Tests/ApplyServerBalanceTests.cs` | moved onto `TestBoot`; local `NullPersister` deleted; **gains the missing `ReloadFromDisk()`** |
| `Assets/Scripts/EconomyRuntime/Tests/Golfin.EconomyRuntime.Tests.asmdef` | + `Golfin.TestSupport` reference |
| `Assets/Scripts/InventorySync/InventoryRaise.cs` | **new** — the raise record, and the essay on why a new key is not one |
| `Assets/Scripts/InventorySync/InventoryProjector.cs` | `Apply` takes an optional raise collector; quantity + ticket raises append to it |
| `Assets/Scripts/InventorySync/InventorySyncService.cs` | `ApplyAndCount()` on both merge sites; warning per raise + `OnQuantitiesRaised` seam |
| `Assets/Scripts/InventorySync/Tests/InventoryProjectorTests.cs` | +4 tests: the refund, the restore-is-not-a-refund case, balls/tickets, and collector-changes-nothing |
| `Assets/Scripts/InventorySync/Tests/InventorySyncServiceTests.cs` | +3 tests: stale-merge refund, boot-merge raise, throwing handler |
| `Assets/Scripts/Telemetry/TelemetryConfig.cs` | + `TelemetryEventNames.InventoryMergeRaise` |
| `Assets/Scripts/TelemetryRuntime/TelemetryHooks.cs` | wires `OnQuantitiesRaised` → one `inventory_merge_raise` row per raised stack |

### GolfinRedux — admin dashboard (`Tools/admin-dashboard`)

| File | 1-line summary |
|---|---|
| `app/api/content/enabled/route.ts` | **new** — POST the GLOBAL kill switch, admin-only, no catalog segment |
| `app/api/content/[catalog]/enabled/route.ts` | doc comment corrected — the per-catalog kill does NOT drop the top-level flag |
| `app/api/users/[id]/inventory/route.ts` | + `DELETE` — revoke an unapplied grant |
| `lib/contentData.ts` | + `fetchGlobalContentEnabled()` (fails open); `fetchCatalogs()` returns `globalEnabled` |
| `lib/contentMutations.ts` | + `setGlobalContentEnabled()`, audited as `content.global_enabled`; per-catalog doc corrected |
| `lib/inventoryMutations.ts` | + `revokeInventoryGrant()` — scoped, race-safe, audited |
| `lib/types.ts` | + `ContentCatalogsResponse.globalEnabled` |
| `lib/mockStore.ts` | + `contentGlobalEnabled`, seeded ON |
| `lib/mockInventory.ts` | fixture grant id made a valid uuid; + an already-applied fixture grant |
| `lib/i18n.ts` | +13 keys (EN + JA): `cp.global.*`, `c.globalKill.*`, `uinv.revoke*`, `urevoke.*` |
| `app/(panels)/_content/client.ts` | + `setGlobalContentEnabled()`; header route list corrected |
| `app/(panels)/_content/publish-drawer.tsx` | Kill switch tab now shows both switches, each naming its blast radius; confirm on the global kill |
| `app/(panels)/_content/catalog-panel.tsx` | reads `globalEnabled`, renders `GlobalKillBanner`, passes it to the drawer |
| `app/(panels)/users/inventory-tab.tsx` | Revoke button on PENDING grants only |
| `app/(panels)/users/user-drawer.tsx` | `revokeGrant` modal kind wired to the DELETE |

### playlife (separate repo, `~/Documents/playlife`)

| File | 1-line summary |
|---|---|
| `backend/routers/content.py` | per-catalog `"enabled": True` removed from the response; docstrings corrected |
| `backend/tests/test_content_kill_switch.py` | + `test_a_served_catalog_carries_no_enabled_field`; two stale `enabled` assertions dropped |

**No file outside these tables was written.** Everything else in `git status` is the baseline dirt
listed at the top of this report, unchanged.

## Notes for Cesar

1. **Four harnesses, not three** (item 3). `TournamentRestrictionsClientTests` was a fourth copy;
   it is converted. Flagging it because the task said three.
2. **Both halves are DEPLOYED** (2026-08-26) — see STATUS.md for the version ids and the
   before/after prod probe. `playlife-api` v52 → v53; `golfin-admin` version `cf90ee8a…`. No
   migration was needed; nothing here touched the schema.
3. **What was and was not verified against prod.** The API half WAS: the per-catalog `enabled`
   field is measurably gone from the live response, and the subset-invariance of the top-level
   flags still holds. The dashboard half was NOT — `admin.golfin.world` is behind Cloudflare
   Access, which 302s an unauthenticated probe, so items 2 and 4 rest on the mock-mode UI run
   above plus the active version id. No kill switch was flipped on prod and no live Supabase row
   was written by this task.
4. **Item 5 has no real-play evidence, by nature.** Reproducing a refund needs two devices with
   divergent revs. The EditMode tests drive the production `InventorySyncService` end to end through
   the existing `FakeTransport`, which is the honest gate here; the first real number will come out
   of the device pass telemetry.
