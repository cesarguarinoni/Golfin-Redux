# SPEC — `content_panels_gaps`

SPEC_KIND: backend

> Closes the four gaps the `content_admin_panels` Implementer reported instead of working around.
> Every one was correctly escalated rather than self-graded — including the one whose premise turns
> out to be wrong. Reporting them was right; three of the four are real.
>
> Depends on: `content_catalog`, `content_cursor_per_catalog`, `content_admin_panels` — all DONE
> and deployed.

## Status

`SPEC_READY`.

## Goal

Add the small amount of **server logic** the panels genuinely need, and the one schema addition
that was specced in `CONTENT_PIPELINE_PLAN.md` §11.2 but never built. Four items, all small, no
Unity change.

---

## 1. Clubs rarity facet — the FAIL is real, the stated cause is not

**Corrected by the Architect against prod, 2026-08-25.** The report says *"rarity only appears in
the row id — which the 7 originally-shipped clubs predate."* That is not what the data shows:

```
club_awedge_bogeyb_common    -> data.rarity = 'Common'    brand='BogeyB'  type='A.Wedge'
rarity non-null: 799 / 799   brand non-null: 799 / 799    type non-null: 799 / 799
data->>rarity=eq.Common      -> 133 rows
distribution: Common 133 · Uncommon 133 · Rare 133 · Mythic 133 · Legendary 133 · Supreme 134
```

`Clubs.csv` has carried a `rarity` column since the roster shipped (column 4), so every seeded row
has it in `data`. What misled the diagnosis is that the **generated ids also encode rarity**
(`club_awedge_bogeyb_common`) while the 7 hand-authored ids do not (`club_iron9_klyro`) — true of
the ids, and irrelevant, because the facet should never have read the id.

**So rarity is not a special case.** It is the same `data->>'<field>'` filter as brand and type,
with complete 799/799 coverage. Implement all three facets identically.

- Add a filter parameter to `/api/content/[catalog]/rows` and `fetchDraftRows` — the ~3 lines the
  report already scoped, applied to `rarity` as well.
- **Drop the per-facet coverage caveat from the Clubs UI once all three are complete queries.** It
  was the honest thing to show while a facet was partial; leaving it up after the fact would state
  something untrue.
- Keep the facet values server-derived (`select distinct data->>'<field>'`), not hard-coded — a new
  brand must appear without a deploy.

## 2. Version history must read `content_versions`, not the audit log

The most consequential of the four. History is currently reconstructed from `admin_audit_log`,
which **caps at 200 actions and never saw the SQL-seeded v1**. Rollback is §7.3 of the plan — the
answer to "an update broke installed games" — and a rollback target list that silently loses its
tail is a safety rail that quietly stops reaching.

`content_versions` already holds every snapshot, written inside `content_publish` since Phase 0.
Nothing reads it.

- New route `GET /api/content/[catalog]/versions` → `version`, `published_by`, `published_at`,
  `note`, `row_count` (from the snapshot length), newest first, paginated. `checkAdmin()` as usual.
- Point the drawer's history and rollback picker at it.
- **v1 must be selectable** — it is the seeded baseline and the most likely rollback target in an
  emergency.
- The audit log stays what it is: the record of *who did what*, not the source of truth for *what
  versions exist*.

## 3. `shop_catalog` scheduling columns — specced in §11.2, never built

The report is right that the badge cannot compute LIVE/SCHEDULED/ENDED because the columns do not
exist. Falling back to `is_active` rather than inventing a schedule was the correct call.
`CONTENT_PIPELINE_PLAN.md` §11.2 specified these and Phase 0 seeded only the existing CSV shape —
**an Architect gap, not an Implementer one.**

Add to `shop_catalog` rows (additive, per §2 I4 — new columns, nothing renamed):

| Column | Meaning |
|---|---|
| `startAt` / `endAt` | listing window, UTC, `endAt` EXCLUSIVE — same semantics as `home_notices` |
| `saleStartAt` / `saleEndAt` | sale window, independent of the listing window; outside it `saleRpCost` is ignored |

- Add the columns to `Assets/Resources/Data/shop_catalog.csv` **empty for all 5 rows** (empty = no
  window = always live), so the round-trip stays clean and nothing changes behaviour.
- Extend `lib/contentValidate.ts`: windows well-ordered; sale window inside the listing window.
- Badge then computes properly; keep the `is_active` fallback for rows with no window.
- Parse windows fail-closed, exactly like `routers/notices.py` `_parse` — a present-but-unreadable
  bound drops the row rather than publishing it forever.

## 4. Art thumbnails — NOT a gap. Accept as built.

The catalogs store Unity sprite names because that is what the game resolves; there are no URLs to
show. The monogram tile beside the sprite name is the right answer, and inventing a URL column here
would pre-empt `CONTENT_PIPELINE_PLAN.md` §10.2, which is a deliberate, separate decision tied to
serving club art remotely.

**Do nothing.** Recorded so the next session does not re-open it.

---

## Acceptance checklist

- [ ] All three Clubs facets (brand / type / rarity) narrow the SERVER query; verify rarity=Common returns 133 and the count comes from the API, not a client filter
- [ ] Facet values are server-derived, not hard-coded — adding a brand in drafts makes it appear
- [ ] The per-facet coverage caveat is REMOVED from the Clubs UI, since all three are now complete
- [ ] `GET /api/content/[catalog]/versions` returns every version including **v1**, newest first, paginated, `checkAdmin()`-gated
- [ ] Rollback picker offers v1 and a version older than the 200-action audit horizon; rolling back to it produces a HIGHER version
- [ ] The four schedule columns exist on all 5 `shop_catalog` rows (empty), and `export --check` is clean — round-trip unaffected
- [ ] A row with a future `startAt` reads SCHEDULED; a past `endAt` reads ENDED; no window reads from `is_active`
- [ ] Validation rejects an inverted window and a sale window outside the listing window
- [ ] A present-but-unparseable bound drops the row (fail closed), matching `notices.py`
- [ ] Art thumbnails unchanged — §4 is deliberately a no-op
- [ ] Deployed; root still 302s to cloudflareaccess (NOT 200)
- [ ] Spec deviations flagged at the bottom of the report

## Out of scope

- Art URL columns / remote art (`CONTENT_PIPELINE_PLAN.md` §10.2 — its own decision).
- Any Unity or `Assets/Scripts` change. The only `Assets/` edit is four empty CSV columns.
- Player inventory, Addressables, `LevelUpCosts`.
- Re-opening §4.
