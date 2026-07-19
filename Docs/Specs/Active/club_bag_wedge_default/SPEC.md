# club_bag_wedge_default

> **Status:** SPEC_READY
> **Order:** 761 (Notion GOLFIN_Roadmap) — Phase "Loop v2", P2 — Medium
> **Tier:** 3 — FULL PIPELINE (save-schema migration + gameplay + bot behaviour + Hole 1 completability gate)
> **Filed:** 2026-07-17 11:30 JST (Architect)
> **Handoff file:** `Docs/Specs/Active/club_bag_wedge_default/SPEC.md` (this file)
> **Pairs with:** Order 762 `versus_bot_club_resolution_audit` (the multiplayer-bag half — separate order).

---

## One-line

Give every player a wedge in their default equipped bag (fresh **and** existing saves), teach the solo bot to use it, and retire the pile of "the bag has no wedge" workarounds that pattern created — which also de-risks the Hole 1 solo-completion endgame.

---

## Cesar rulings (2026-07-17)

1. **Starter bag must contain a club of each type** — including a wedge. Not a progression unlock.
2. **Wedges are not gated.**
3. **Add the wedge to EXISTING players**, not just fresh saves.
4. Wedge to seed = **`club_pwedge_royal`** (P.Wedge Royal Swing). Rationale: a pitching wedge is the
   natural approach club, and its "reliable distance control around the green" (CSV bio) is exactly what
   Hole 1's sunken green needs for a soft drop.
5. Give the solo bot a wedge; **audit all shot-firing bots for a working bag** (the versus-bot half is
   Order 762).

---

## Context — what is and isn't already true

The June "club_bag_population_concern" doc is **largely stale**. Order 610 Phase A (commit `29c8e8279`,
save schema v5→v6) already made the equipped bag load from `SaveDataHost`, seed once, hydrate, and persist.
The "no save-state bag exists" premise is dead. Two of the three concern legs are resolved:

- **Bag-from-save-state** → RESOLVED by 610 Phase A.
- **`SelectedDistance` uses CSV `baseDistance`** → non-issue; that's the correct per-club spec value and the
  map rings already use real carry independently (`ShotConeView.MaxCarryYardsForMap`). Comment-cleanup only.
- **No wedge in the bag** → the one real thing. This order.

The existing default set (`ClubManager.DefaultBagIds`) is Driver / Wood / Iron7 / Putter. It is already
**mixed-rarity** (Common driver/wood, **Rare** iron, **Supreme** putter), so seeding the **Legendary** P.Wedge
fits the established pattern — **no new low-rarity wedge needs authoring.** Two wedges exist in `Clubs.csv`
(`club_awedge_fyloe` Mythic, `club_pwedge_royal` Legendary); we use the P.Wedge.

---

## The three save cohorts (this is the crux)

"Add to existing" is NOT one code path. `HasPlayableBag` + the current A4 repair only *re-equip clubs the
player already owns* — they never grant. Cohorts:

| Cohort | Owns `club_pwedge_royal`? | What A4 re-equip alone does | Needs |
|---|---|---|---|
| **Grandfathered** (pre-610) | **Yes**, slot 0 — `SeedGrandfather` seeded the full catalog | re-equips it ✓ | re-equip (works) |
| **Fresh-seeded post-610** | **No** — `SeedStarter` seeded only the 4 starter clubs | `TryGetValue` misses → **no-op → still no wedge forever** ✗ | **grant, then equip** |
| **Fresh post-this-change** | Yes — seeded by new `DefaultBagIds` | n/a | seed (works) |

The middle cohort is why a **grant-then-equip migration** is mandatory. A pure re-equip cannot reach them.

---

## Scope

### Change 1 — add the wedge to the default set

`ClubManager.DefaultBagIds`:

```csharp
private static readonly string[] DefaultBagIds =
    { "club_driver_gf", "club_wood_gf", "club_iron7_mireo", "club_pwedge_royal", "club_putter_golfinx" };
```

This fixes **fresh post-change** saves (both `SeedStarter` and `SeedGrandfather` read `DefaultBagIds`).

### Change 2 — bag-safety treats "wedge" as one role

`ClubType` has **three** wedge values: `A_Wedge`, `P_Wedge`, `S_Wedge`. Adding a single one to
`RequiredBagTypes` would make bag-safety demand *that exact sub-type*, so a player who later equips an
A_Wedge instead of the P_Wedge would trip a false "unplayable bag" repair. Model wedge as one **role**:

- Add a required **Wedge role** satisfied by ANY of `A_Wedge` / `P_Wedge` / `S_Wedge`.
- Minimal-diff approach: extend `ClubOwnershipService.HasPlayableBag` to accept role-groups (an array of
  acceptable-alternative arrays), OR keep `RequiredBagTypes` as exact names for Driver/Wood/Iron/Putter and
  add a separate `RequiredBagTypeGroups` for the wedge role. Implementer picks the cleaner of the two; the
  **behaviour** is fixed: a bag is playable iff it has Driver AND Wood AND Iron AND (any Wedge) AND Putter.
- This is a pure-layer change → add an EditMode test.

### Change 3 — v8→v9 migration: signal the wedge backfill

Follow the **exact v5→v6 pattern** (pure `Migrate` sets a flag; the catalog-dependent work happens later in
`ClubManager`, which has `ClubDatabaseCSV`). In `SaveSchemaMigrator.Migrate`:

```csharp
// v8 → v9: backfill the default-bag wedge for existing players (Order 761).
// Pure signal only — ClubManager grants+equips on next load (it owns the catalog).
if (data.schemaVersion < 9)
{
    if (data.clubOwnershipSeeded)   // an already-seeded save = an existing player
        data.wedgeBackfillPending = true;
    data.schemaVersion = 9;
    Debug.Log($"[SaveSchemaMigrator] Migrated v8 → v9 (wedgeBackfillPending={data.wedgeBackfillPending}).");
}
```

- Bump `CurrentSchemaVersion` 8 → 9.
- Add `public bool wedgeBackfillPending;` to `SaveData` (defaults false; a brand-new save never runs
  `Migrate()`, so fresh saves never set it — they get the wedge via `DefaultBagIds` seeding instead).

### Change 4 — ClubManager performs the backfill (grant-then-equip, run-once)

In `InitializeClubs()`, after `HydrateFrom(save)` and gated on `save.wedgeBackfillPending`:

1. If `club_pwedge_royal` is **not owned** → grant it (`BuildSpec` + `ClubOwnershipService.Grant` /
   `GrantClub`). Covers the fresh-seeded-post-610 cohort.
2. Equip it to bag slot 1 (`EquipClub("club_pwedge_royal", 1)` or set `equippedBagSlot = 1` before hydrate
   write). Covers grandfathered (owned, slot 0) **and** the just-granted cohort uniformly.
3. Clear `save.wedgeBackfillPending = false`; `host.MarkDirty()`.

Idempotent: grant is a no-op if owned; the flag gate makes it run exactly once. Order it BEFORE the existing
A4 `HasPlayableBag` check so the bag is already wedge-complete when A4 runs (A4 then confirms, no double work).

### Change 5 — BotDriver uses the wedge (unwinds workarounds)

`Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs`. The class is riddled with no-wedge workarounds keyed off
the old bag. With a wedge now in the equipped bag (`LabClubIndex 2` resolvable via `ClubContext.EquippedBag`):

- **`SelectShot`** — add a wedge band for short approaches (~20–80m) so approaches drop onto/into the green
  instead of being laid up with Iron7. Use the existing `bot_clubs.csv` **wedge** carry curve (21 rows,
  already present) via `InterpolateClubPower("wedge", …)` — the same table VersusBot uses. Keep the driver /
  iron7 / putter bands; insert wedge between iron7-short and putter.
- **Off-green putter guard** — chip with the **Wedge**, not Iron7. This is the Hole 1 endgame fix: the Iron7
  chip could not drop soft into the sunken green; the wedge can. Update the guard's club + power (interpolate
  from the wedge curve) and the log string.
- **The "nearest available equipped club" resolver** (the `bestBelow/bestAbove` block, ~lines 756–800) —
  now that the bag carries a wedge, the desired club will usually be present. **Do not delete the resolver**
  (it's still a correct safety net for any bag missing a lab club), but its comments that assert "the default
  loadout has no wedge / LabClubIndex 2" are now false — update them so the next reader isn't misled.
- Update the stale `// the default bag has NO wedge` / `// there is no wedge` comments throughout.

**Do NOT** change `BotDriver`'s LIVE-path `ClubContext.SelectedClubId` sync — that mechanism is correct and
is what makes the bot play its real bag. We are giving the bag a wedge, not changing how the bag is read.

---

## Hard gates

1. **Hole 1 completability** — ≤7 strokes, default character, **bot-recorded video** (the default visual
   gate; never manual play). This order's whole point is that the wedge lets the approach hold the green so a
   legal putt finishes the hole. If the bot still can't complete in ≤7, the wedge integration failed — do not
   paper over it with the `ForceShotComplete("InCup")` par+3 safety net and call it done. The safety net
   existing is fine; **relying on it to pass this gate is a FAIL.**
2. **All three cohorts verified.** Bot/EditMode rigs must prove: (a) fresh post-change save seeds the wedge
   equipped; (b) a simulated grandfathered save (owns wedge slot 0, `wedgeBackfillPending`) ends with it at
   slot 1; (c) a simulated fresh-seeded-post-610 save (does NOT own the wedge, `wedgeBackfillPending`) ends
   owning + equipping it. Cohort (c) is the one the old A4 path could not reach — it is the load-bearing test.
3. **Migration runs exactly once.** Second load must not re-grant, re-equip, or duplicate. Assert
   `wedgeBackfillPending` is false after first load and `ownedClubs` has no dup.
4. **No regression to the seed gate.** A brand-new `SaveData()` must still never run `Migrate()` and must get
   the wedge via `DefaultBagIds` seeding, not the backfill flag.
5. Tests at or above baseline. Add EditMode tests for Changes 2, 3, 4 (pure/service layer) and the cohort
   matrix. `ClubOwnershipService` tests are EditMode (no Assembly-CSharp ref) — keep the pure split intact.

---

## Traps

- **Lesson AA** — verify the implementation actually landed in git before any close-out. Save-schema changes
  especially: the migrator, the `SaveData` field, and the ClubManager backfill must all be in the same
  shipped commit or a partial migration corrupts saves.
- **Lesson W** — do not solve a cross-asmdef need by adding a reference. `ClubOwnershipService` is pure
  (`Golfin.Save`, no Unity); the catalog stays on the Assembly-CSharp side (`ClubManager`). Keep that split —
  it's why Phase A is EditMode-testable.
- **Schema Q-LOCK** — `SaveSchemaMigrator` fails hard if a file's version > code version. Bumping to v9 is
  forward-only and safe; do NOT renumber existing migrations.
- **The gacha test-grant TODOs (v6→v7, v7→v8)** live right above where v8→v9 goes. Do not disturb them; do
  not "helpfully" revert them (that's a separate ship decision).
- **`RequiredBagTypes` is ClubType enum *names* as strings** (`nameof(ClubType.Driver)` …). The wedge role
  must match the same convention.

---

## Expected outcomes (predict, then verify)

- Fresh player: 5-club bag Driver/Wood/Iron7/**P.Wedge**/Putter, all slot 1.
- Existing players (both cohorts): same, after one migrated load.
- Solo bot: approaches inside ~80m use the wedge and hold the green; Hole 1 completes in ≤7 **real** strokes
  (no seam) on video.
- Net code: BotDriver's no-wedge special-casing shrinks; no new architecture.

---

## Out of scope

- **VersusBot / 1v1 bag resolution** → Order 762. (VersusBot already *selects* a wedge in its logic but may
  not *fire* one on the live path — that's the 762 measurement.)
- Tournament field bots — `BotFieldGenerator` generates scores statistically from `bot_score_brackets.csv`;
  no shots, no bag. Correctly excluded.
- Authoring new low-rarity wedges, or any `Clubs.csv` content change (we reuse `club_pwedge_royal`).
- The `SelectedDistance` comment cleanup (cosmetic; fold into a later polish pass if desired).
- Widening `RarityStatCaps`.

---

## Definition of done

1. Changes 1–5 landed in a single coherent commit set (schema + field + migration + ClubManager + BotDriver).
2. Cohort matrix (fresh / grandfathered / fresh-seeded-post-610) all verified; run-once proven.
3. Hole 1 ≤7 **real** strokes on bot video (seam not relied upon).
4. Tests green at or above baseline, incl. new pure-layer + cohort tests.
5. Stale BotDriver no-wedge comments corrected.
6. Cesar-approved → Active → Completed; Notion 761 Done + Closed.
