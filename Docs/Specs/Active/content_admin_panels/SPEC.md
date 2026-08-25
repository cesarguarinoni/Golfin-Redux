# SPEC — `content_admin_panels`

SPEC_KIND: backend

> Declared for `.claude/hooks/enforce_implementer_done.py`. This is a **Next.js dashboard** task:
> no Unity, no scene, no prefab, no `Assets/` edits, and therefore no Unity Game View to
> screenshot. It DOES have a UI surface — a browser one — so evidence is browser screenshots of
> `localhost:3000`, listed under §Smoke evidence. If the hook's `SPEC_KIND: backend` branch skips a
> gate that should apply to a web UI, say so in the report rather than working around it.
>
> Plan: `Docs/CONTENT_PIPELINE_PLAN.md` §3 (panels), §7 (rails), §11 (shop).
> Depends on: `content_catalog` Stage D (DONE, deployed) — every route this task needs already
> exists and is live. **This task adds no server logic.**

## Status

`SPEC_READY`.

## Goal

Six panels on `admin.golfin.world` so clubs, characters, items, texts and shop offers can actually
be edited by a person. Everything shipped so far is API-only: the catalogs are live, validated,
publishable and rollback-able, and there is no way to see or touch any of it without curl.

**No new API routes, no schema change, no Unity change.** If a panel needs something the existing
routes cannot serve, that is a finding to report — not a licence to add an endpoint.

## What already exists (verified live 2026-08-25 — do not rebuild)

| Route | Method | Serves |
|---|---|---|
| `/api/content` | GET | catalog list + `publishedVersion` / `isEnabled` / `publishedCount` / `draftCount` / `dirtyCount` |
| `/api/content/[catalog]/rows` | GET / PUT | paginated draft rows (`?page=&q=&limit=`); upsert one draft row |
| `/api/content/[catalog]/diff` | GET | drafts vs published — added / changed (field-level) / deactivated |
| `/api/content/[catalog]/publish` | POST | validate → `content_publish` → audit |
| `/api/content/[catalog]/rollback` | POST | `content_rollback` → audit |
| `/api/content/[catalog]/enabled` | POST | the kill switch → audit |

All six start with `checkAdmin()` and end with `writeAudit()`. Live counts today:
clubs 799 · texts 501 · characters 12 · bags 10 · shop_catalog 5 · items 3 · balls 2.

## Implementation

### 1. Panels

Add to `lib/registry.ts` (it sorts by TRANSLATED title, so registration order does not matter):

| Panel | id | Notes |
|---|---|---|
| Clubs | `clubs` | **799 rows — server-side pagination + filter (brand / type / rarity) is mandatory**, never a full `<table>` |
| Characters | `characters` | 12 rows, simple grid |
| Items | `items` | Items / Bags / Balls as three tabs in ONE panel — 15 rows between them does not justify three sidebar entries |
| Texts | `texts` | 501 keys; EN and JA side by side, filter by key prefix |
| Shop | `shop` | §3 below |

Follow the Tournaments panel — `ADMIN_DASHBOARD_OPS.md` §3.1 calls it the most complete.

### 2. The Publish drawer — shared by every panel

One component, not five. Diff preview (counts + field-level rows, straight from `/diff`) → confirm
→ publish. Plus version history with one-click rollback, and the per-catalog enable switch.

- **The diff preview is the highest-value guard in the whole system** (`CONTENT_PIPELINE_PLAN.md`
  §7.2). It must be impossible to publish without seeing it.
- **Rollback must state, in the UI, that it moves FORWARD** — it republishes an old snapshot as a
  new, higher version. An operator who thinks it rewinds will misread the version numbers.
- `z-40` on the drawer: the language switcher is `z-30` and must be covered (§3.4).

### 3. Shop panel — `CONTENT_PIPELINE_PLAN.md` §11

- `category` picker → **`refId` typeahead against the live catalog for that category**. This is
  what makes a dangling reference impossible rather than merely rejected at publish.
- **Resolved preview**: name, rarity and art thumbnail of the referenced entity, so the operator
  sees the club rather than `club_iron9_klyro`.
- LIVE / SCHEDULED / ENDED badge derived from the windows, untranslated, exactly like Tournaments.
- ⚠️ **Print on the panel that prices are NOT server-enforced.** Purchases still debit RP
  client-side through `PointsSpendGate` (§11.5). Making the shop admin-driven makes it very easy
  to assume otherwise.

### 4. Bilingual — every string, both languages

`lib/i18n.ts` holds one flat `DICT` of `{ en, ja }`; `DictKey` is derived from `DICT`, so a missing
key is a **type error, not a runtime blank**. Hard-won specifics from §3.4:

- **Never name a row-map parameter `t`** — it shadows the translator and has bitten that file twice.
  Use `row`.
- JA needs `whitespace-nowrap` on badges and table headers, and `tracking-wider` dropped on JA badges.
- Untranslated by design: catalog names, column names, row ids, slugs, and the state badges.

### 5. Mock mode

Respect `isMockMode()` and back the panels with `lib/mockStore.ts` like the others. **Fixtures must
be obviously fake** (prices like `9999`) — §3.5 records a real incident where mock fixtures were
read as production facts about a user.

## Acceptance checklist

- [ ] All five panels registered and reachable; sidebar sorts correctly in BOTH languages
- [ ] Clubs panel pages through 799 rows server-side; filter by brand/type/rarity narrows the query, not the rendered array
- [ ] Editing a draft row shows a non-zero `dirtyCount` on `/api/content` before publish
- [ ] Publish is impossible without the diff preview being shown
- [ ] Diff preview matches `/api/content/[catalog]/diff` exactly for an added, a changed and a deactivated row
- [ ] Publish blocked on an invalid row shows the FULL problem list, and `publishedVersion` did not move
- [ ] Rollback works from the version history and the UI states that it moves forward; the resulting version is HIGHER
- [ ] Kill switch flips `isEnabled` and the game endpoint stops serving that catalog
- [ ] Shop `refId` typeahead only offers `is_active` rows of the chosen category; resolved preview renders name + rarity + thumbnail
- [ ] The "prices are not server-enforced" notice is visible on the Shop panel
- [ ] Every new string has BOTH `en` and `ja`; screenshots of each panel in JA
- [ ] No row-map parameter named `t` anywhere in the diff
- [ ] Every mutation wrote an `admin_audit_log` row
- [ ] Mock mode renders all panels with obviously-fake fixtures
- [ ] Deployed; root still 302s to cloudflareaccess (NOT 200) — `ADMIN_DASHBOARD_OPS.md` §2
- [ ] Spec deviations flagged at the bottom of the report with justification

## Smoke evidence

Browser screenshots of each panel at `localhost:3000` in EN and JA, plus one publish → diff →
confirm → audit-row round trip captured end to end. Run dev with
`NODE_ENV=development npm run dev` (§4.2), and **never `next build` against a running `next dev`**
(§4.1 — shared `.next/`, every chunk 404s, and the server log stays clean, which is what makes it
read as "the app is broken").

## Out of scope

- **Any new API route or schema change.** The six routes are enough; if they are not, report it.
- Unity: no `ContentService`, no `*DatabaseCSV.cs`, no `Assets/` edits. Phase 1 is a separate spec.
- Player inventory, Addressables, art-URL columns, `LevelUpCosts`.
- Server-authoritative purchases — §11.5 stays true, and the panel says so.
