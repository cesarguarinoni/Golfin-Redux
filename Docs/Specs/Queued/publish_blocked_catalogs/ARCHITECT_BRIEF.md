# ARCHITECT BRIEF — two catalogs cannot be published from the admin

**Diagnosed:** 2026-09-01, by Claude Code, while re-checking the web admin after
`club_full_art_repoint`. **Not fixed** — both need a decision that is yours.

## Headline

`mission_loadouts` (17 errors) and `gacha_pools` (1 error) both fail
`validateCatalog`, so **neither can be published from the admin**. Both are
**validator false positives with ZERO runtime impact.** Neither is a data problem,
and in one case "fixing the data" would actively break the game.

Provenance: both appear **identically in published rows AND drafts**, and both
cross-reference columns no recent session wrote. They are long-standing.

| | errors | runtime impact | root cause |
|---|---|---|---|
| `mission_loadouts` | 17 | **none** — all 9 supplied loadouts + all 3 ban masks resolve | validator uses a different club-type vocabulary than the runtime |
| `gacha_pools` | 1 | **none** — the row is already `is_active=false` and skipped when rolling | rule has no deactivated-row carve-out |

---

## Issue 1 — `mission_loadouts`: two club-type vocabularies

### What fires

`SUP_FULL`, `SUP_FULL_RARE`, `SUP_IRONS`, `SUP_NO_DRIVER`, `SUP_ONE_IRON`,
`SUP_WEDGE_PUTTER` — 17 errors of the form:

> `No active clubs row is type "Iron7" at rarity "Common", so this supplied bag would be missing…`

Unresolvable tokens: `Iron7` ×5, `Iron9` ×4, `AW` ×4, `PW` ×4.

### Root cause

The loadout `clubs` mask is written in a **design vocabulary** that is deliberately
NOT `ClubType`:

| mask token | `clubs.type` |
|---|---|
| `AW` / `PW` / `SW` | `A.Wedge` / `P.Wedge` / `S.Wedge` |
| `Iron7` / `Iron9` | `Iron` (the enum does not distinguish) |

The runtime knows this and translates —
`Assets/Scripts/UI/MissionSelection/MissionLoadoutResolver.cs:163` `ClubTypeName()`,
whose own comment says: *"The enum and the design vocabulary differ on the wedges
(`A.Wedge` vs `AW`), so the mapping is explicit rather than a ToString."*
`IronName()` (`:180`) recovers the 7/9 from the club's `clubId + name`.

The validator does **not** translate. `lib/contentValidate.ts:1217-1231` compares the
mask token straight against `text(club.data.type)`. So every wedge and every
numbered iron is reported unreachable.

### Runtime truth — verified, not assumed

I replayed the resolver's own mapping over the published catalogs:

```
Resolver token -> rarities available (ACTIVE clubs)
   AW, PW, SW, Iron, Iron7, Iron9, Driver, Wood, Putter
     -> all six rarities present for every token

supplied loadouts that CANNOT build a bag at runtime: 0   (9 of 9 OK)
OWN ban masks that ban nothing:                       0   (3 of 3 OK)
```

`IronName` does work: of 114 iron rows, 12 resolve to `Iron7` and 6 to `Iron9`,
because names read `Iron 7 FAIRLOFT`, `Iron 9 …`.

### Options

- **A — teach the validator the mapping (recommended).** Mirror `ClubTypeName()` in
  `contentValidate.ts` before comparing. Smallest change, makes the rule true.
  **Cost:** the mapping then exists twice, in C# and TS, and can drift.
- **B — rewrite the data into the `clubs` vocabulary. Reject.** The runtime resolver
  matches on `AW`/`Iron7`; changing the masks to `A.Wedge`/`Iron` would make
  `ResolveSupplied` find nothing and every supplied mission would hand out an empty
  bag. This is the option that looks like "fixing the data" and is the dangerous one.
- **C — drop the reachability rule.** Cheapest; loses a real guard (it would have
  caught a genuinely unreachable bag).

### Two things worth deciding while you are here

1. **The validator never checks `ban:` masks at all** — `contentValidate.ts:1200`
   only asserts the shape (`*` or `ban:…`), never that the banned tokens exist. A
   typo (`ban:Irons`) would silently ban nothing and no gate would notice. All three
   shipped ban masks happen to be correct today.
2. **`IronName` is positional-fragile.** A club's mask token is derived from a digit
   in its `clubId + name`. Renaming `Iron 7 FAIRLOFT` → `Iron FAIRLOFT`, or adding a
   club whose brand contains a 7, silently changes which loadouts can see it. Any
   validator mirror inherits that fragility. The durable fix is an explicit column on
   `clubs` (e.g. `loadoutType`) rather than a probe — bigger, and your call.

---

## Issue 2 — `gacha_pools`: a rule that fires on a deactivated row

### What fires

> `psc1_ball_golfin / refId`: `"ball_golfin"` is the DEFAULT ball — every player already owns one, so a slot that pays it pays nothing.

The rule itself is correct and worth keeping — it is the `gacha_ops_polish §4e`
guard, written after `psc1_ball_golfin` sat in the standard pool at weight 60 (11 %
of every Common pull was a no-op) until an operator noticed.

### Root cause

**The operator already fixed it, and the rule will not let go.** The row is
`is_active=false` — in the shipped CSV
(`Assets/Resources/Data/gacha_pools.csv:4`, trailing field `false`) and in the
published catalog. But the `gacha_pools` block
(`lib/contentValidate.ts:1559`) iterates `for (const row of rows)` with **no
`row.isActive` guard**, so every rule in it fires on deactivated rows.

Runtime already ignores the row entirely:

| site | guard |
|---|---|
| `GachaBannerModel.cs:262` | `if (!p.IsActive) continue;` |
| `GachaBannerModel.cs:416` | `if (!row.IsActive) continue;` |
| `GachaRatesModalController.cs:165` | `if (!p.IsActive) continue;` |

So the error protects against nothing and blocks the catalog.

This is the **exact failure mode the sibling shop rules were written to avoid**.
`lib/__tests__/contentValidate.test.ts`, *"leaves a DEACTIVATED ticket row alone"*:

> *"no client renders a deactivated row, and min_build is immutable once published — so gating one would make a catalog permanently unpublishable with deactivation as the only way out."*

That is precisely what has happened to `gacha_pools`.

### Options

- **A — skip deactivated rows for the ref/default-ball rules (recommended).** Mirrors
  the shop carve-out exactly; one condition. Deactivation goes back to being a real
  remedy.
- **B — guard the whole `gacha_pools` block on `row.isActive`.** Broader. Decide
  first whether any rule in that block *should* still fire on a deactivated row
  (rarity-format checks arguably should, so the row is sane if reactivated).
- **C — repoint the row or clear `isDefault`. Reject.** Changes live economy data to
  satisfy a lint, on a row that is already switched off.

---

## Cross-cutting note

This is the **third** "two systems spell the same thing differently" defect found in
one session:

1. ball thumbnails — `S_Controls_Ball_*` vs the PascalCase `fullSprite` names;
2. club full art — `portraitFull` generated from the brand NAME (`FairwayThreads`)
   while art and the two working sibling columns use the brand TOKEN (`Fairway`);
3. this one — loadout masks (`AW`, `Iron7`) vs `clubs.type` (`A.Wedge`, `Iron`).

Per `CLAUDE.md` PIPELINE_HARDENING rule 15 (*second defect of a shape ⇒ audit the
shape*), the shape is worth naming rather than fixing case by case: **an identifier
is minted in one system and re-derived, by convention, in another, with no shared
registry and no gate proving the two agree.** A cheap mechanical check — "every
cross-catalog name reference resolves against what the build actually bundles" — is
what caught #1 and #2 here and could run in CI.

## How to reproduce any of this

```
python3 Tools/content/export_content.py --env-file Tools/admin-dashboard/.env.development.local --check
```
and, for the validator sweep, run `validateCatalog` over every catalog's published
rows and drafts with a full `otherCatalogs` context (the publish route's own path).
