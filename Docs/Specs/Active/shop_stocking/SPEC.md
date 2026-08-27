# SPEC — `shop_stocking`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-27 (Architect via Cowork), from Cesar's requirement the same day:
> *"add items, characters, etc. to the shop in the admin, and have that inform the next
> build — and a client missing any information must never show a broken item and must never
> wrongly spend RP."* Shop first; creating characters/clubs in the admin and CSV → admin
> import are the next spec (§7), not this one.
>
> Depends on `shop_server_purchase` (Unity half must be COMMITTED first — it is untracked in
> the working tree at filing time). Builds on it; does not change its endpoint.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Three gaps, verified in the repo 2026-08-27, stand between "the admin can publish a shop row"
and "the shop is stocked from the admin":

1. **There is no way to add a row in the admin.** `CatalogPanel` offers two interactions —
   click an existing row to edit, and Review & publish — and `RowEditor` renders `rowId` as a
   read-only `<code>`. Every row that exists came from the seed migration. The backend is
   already able: `PUT /api/content/:catalog/rows` upserts a draft by `rowId`
   (`lib/contentMutations.ts::upsertDraftRow`), and `content_publish` inserts
   `on conflict (catalog, row_id) do update`. Nothing calls it with a new id.
2. **Nothing makes the next build carry what was published.** `Tools/content/export_content.py`
   exists and `--check` exits 1 on a stale repo, but the release lane
   (`fastlane/Fastfile` `testflight_build`) never runs it. A build can ship a bundled floor that
   disagrees with the server.
3. **The client renders rows it cannot resolve.** `GeneralShopCatalog.Admit` checks
   `is_active` and the windows, then admits the row; `GeneralShopCard.BindClub/BindBall`
   early-return on a missing DB row *after the card is instantiated* — a blank card with a BUY
   button. With server pricing this row is also refused by the server (`not_listed /
   ref_inactive`), so the player sees a card that can only fail.

This task closes all three and encodes the two build-gate decisions (D2, D4 from the
`shop_server_purchase` report) as validator rules rather than things to remember.

---

## 1. Invariant, stated once

**A shop row is shown only when the client can fully render it, and charged only by the
server.** Rendering needs: the shop row itself (served or bundled), the referenced catalog row
in the client's DB (bundled or overlaid), and its primary sprite. If any is missing the row is
**withheld**, logged, and counted — never rendered with holes. Charging is
`golfin_shop_purchase()` only (`shop_server_purchase` §2), which independently refuses
inactive, gated, or unpublished references. The client withholding and the server refusing are
two locks; neither is allowed to rely on the other.

---

## 2. Admin — `+ New row` on every catalog panel

`Tools/admin-dashboard/app/(panels)/_content/catalog-panel.tsx` +
`row-editor.tsx`. Registered once in the shared panel, so Clubs / Characters / Items / Bags /
Balls / Texts / Shop all get it; Shop is the use case that needs it today.

- A **`+ New row`** button beside Review & publish. Opens `RowEditor` with a blank draft:
  `data = {}`, `minBuild = 0`, `isActive = true`, `exists = false`.
- **`rowId` is an input while `exists === false`** and the read-only `<code>` it is today
  otherwise (the same flag that already locks `min_build`, §D1.7). Validation on save, in
  `upsertDraftRow` (server side, not only the form): `^[a-z0-9_]+$`, ≤ 80 chars, **unique
  across `content_drafts` AND `content_rows`** for the catalog (409 with a message naming
  the clash). The id column of the row (`ID_COLUMN[catalog]` — `entryId` for the shop) is
  **written from `rowId` automatically** and never shown as an editable field, so the two
  cannot disagree.
- Shop convenience, in `shop-panel.tsx`'s `editorExtras`: after a `RefPicker` pick on a new
  row whose `rowId` is still blank, prefill `rowId = "shop_" + refId` (editable). Nothing
  else about the shop editor changes — category picker, `RefPicker`, the four window fields
  are already there.
- Audit: `writeAudit()` with action `content_row_create` (distinct from the existing edit
  action so the Audit panel shows creation as creation). EN + JA `DICT` entries for the
  button, the id field hint, and the two error messages (`ADMIN_DASHBOARD_OPS.md` §3.4).
- Mock mode (`lib/mockContent.ts`): the mock store must accept a new rowId the same way, or
  the control is untestable without a service key.

Out: bulk import, duplicating a row, deleting a draft (deactivate is the delete — I6).

## 3. Admin — the build gates as validator rules (`lib/contentValidate.ts`, `shop_catalog` only, blocking)

One constant, one place: **`lib/buildGates.ts`** exporting
`SHOP_CATEGORY_STRICT_BUILD` — the first uploaded build that parses shop categories strictly
and prices on the server (the `shop_server_purchase` client half). `shop-panel.tsx`'s
`SERVER_PRICE_ENFORCED_FROM_BUILD` moves here and is deleted from the panel.

**Value: `0` until the archive lands, then the number from
`Docs/Versioning/last_uploaded_build.txt` — read from the file, never inferred.** The build
number is `git rev-list --count HEAD` at archive time (`Tools/mark-uploaded.sh`), so "last
upload + 1" is wrong by construction (HEAD was 2338 when the panel said 2334). Setting it is a
one-line commit after the first archive that carries the client half.

Rules, run on publish:

- **G1.** `category ∉ {club, ball}` ⇒ `minBuild ≥ SHOP_CATEGORY_STRICT_BUILD`, and if the
  constant is `0`: **error** — *"The client build that renders `<category>` rows has not been
  uploaded yet; set `SHOP_CATEGORY_STRICT_BUILD` after the archive."* Older builds parse any
  non-ball category as a club (`GeneralShopCatalog.ParseCategory` before the strict fix); the
  server-side `min_build` filter is the only thing that keeps such a row away from them, and
  `min_build` is immutable once published — so it has to be right the first time, and this rule
  is what makes that automatic.
- **G2.** `minBuild ≥` the referenced row's `min_build` (`ctx.otherCatalogs` already carries
  the target row; add `minBuild` to what it carries if it does not). A shop row must never be
  visible on a build that cannot see the thing it sells. Plan §11.4.6, never implemented.
- Banner copy in `shop-panel.tsx`: when the constant is `0`, the amber banner reads *"Server
  pricing is live; the client build is pending upload — character and item rows cannot be
  published yet."* Otherwise the existing "enforced for builds ≥ N" copy. Both EN + JA.

## 4. Backend — one added refusal (`playlife/backend/migrations/2026_08_27_golfin_shop_purchase.sql` → new migration `2026_08_28_shop_purchase_ref_min_build.sql`)

`golfin_shop_purchase()` step 7 (`shop_server_purchase` §2.1): after resolving the referenced
row, also refuse when **the referenced row's `min_build > p_build`** →
`{"status":"not_listed","reason":"ref_min_build"}`. G2 makes this unreachable through the admin;
the function does not get to assume the admin was used. `create or replace` the function in a new
migration (the house rule: migrations are append-only, applied ones are never edited); full SQL
pasted in chat for Cesar; verification block re-runs the bound-parser matrix plus this case.
Deploy + §2.5 smoke as before.

## 5. Build gate — the release lane refuses a stale repo

`fastlane/Fastfile` `testflight_build`, immediately after `sh("../Tools/assert-unity-closed.sh")`:

```ruby
# Content freshness (CONTENT_PIPELINE_PLAN.md I3). The bundled CSVs are the floor every
# install runs on; a build whose CSVs are behind the published catalogs ships a floor that
# disagrees with the server. --check writes nothing and exits 1 when stale OR drifted.
sh("python3 ../Tools/content/export_content.py --env-file ../Tools/admin-dashboard/.env.development.local --check")
```

Non-zero **aborts the lane** with the exporter's own message (it names the catalog and the
sample ids). It does **not** auto-export: an export inside the lane would bake CSV changes into a
build whose commit does not contain them, and the build number is the commit count — the
archive would then lie about what it carries. The human loop is: `export_content.py` → commit
→ rerun the lane. Add that sentence to the lane's header comment and to
`Docs/TESTFLIGHT_RUNBOOK.md` (a new step before "run the lane").

The lane is the only place for this gate. Editor dev builds are not store-bound and the
remote overlay covers them (I1); `BuildStampGenerator.OnPreprocessBuild` must not grow a
network + service-key dependency.

`Assets/Resources/Data/content_version.txt` is already rewritten by the exporter and already
read by `ContentService` as the per-catalog `since` — nothing to add there.

## 6. Client — withhold what cannot be rendered

`GeneralShopModel.cs` `GeneralShopCatalog.Admit`, after the window verdict and before
`_entries.Add`:

```
resolve(entry) :=
  Club      → ClubDatabaseCSV.Instance?.GetClub(refId)          + (portraitSprite ?? portraitFull) != null
  Ball      → BallDatabaseCSV.Instance?.GetBall(refId)          + (thumbnailSprite ?? fullSprite) != null
  Character → CharacterDatabaseCSV.Instance?.GetCharacter(refId) + isActive + (portraitSprite ?? portraitFullSprite) != null
  Item      → ItemDatabaseCSV.Instance?.GetItem(refId)          + isActive + thumbnail sprite != null
```

- Unresolvable → **withheld**, `LogWarning` naming `entryId`, `refId`, category and *which*
  part is missing (row vs sprite), new counter `unresolvable` in the existing summary log line.
- **DB singleton null** (EditMode, no scene) → skip resolution for that row and log ONCE per
  load ("no <X>DatabaseCSV — resolution not checked"). This is a runtime rail, and the same
  shape `RequireReady` already uses for a missing `ContentService`; it must not turn every
  existing `GeneralShopCatalog` EditMode test into a withheld-everything test.
- A **`Placeholder`** sprite is not "resolved": if the DB resolved the sprite name to the
  shared placeholder asset, treat it as missing. NOTE: check how `ClubDatabaseCSV` /
  `ItemDatabaseCSV` fall back (`SpriteRef` / `SpritePath`, ~lines 87/113 in the item DB) — if
  they substitute `Placeholder` silently, compare by sprite name.
- `GeneralShopCard.BindClub/BindBall/BindCharacter/BindItem`: the existing early return on a
  null DB row now also `gameObject.SetActive(false)` and logs an error — it should be
  unreachable after `Admit`, and an unreachable branch that still leaves a blank card is the
  bug this spec exists for.

Tests (EditMode): `Admit` withholds a row whose ref is absent from an injected fake DB; keeps
one whose ref resolves; null-DB path admits and logs once; `ParseCategory` strictness test
from `shop_server_purchase` still green.

## 7. What this deliberately leaves for the next spec (`content_two_way`)

- **Creating characters / clubs / items in the admin** — the `+ New row` control from §2
  already covers the data row; what is missing is art (plan §10.2: `portraitUrl` etc. served
  through a `TournamentArtService` clone) and the `golfin_characters` mirror on publish
  (`content_catalog` §A4). Until art-by-URL exists, a character created in the admin renders
  only on a build that bundles its sprites, which is what `min_build` + G2 + §6 protect.
- **CSV edits in Unity informing the admin** — an `import_content.py`: repo CSV → `content_drafts`
  upsert of the rows that differ from published, then the existing publish drawer shows the
  diff and Cesar publishes. Published Supabase stays the single truth; a CSV edit is a
  *proposal* until published. `export_content.py --check` already detects drift in both
  directions and becomes the pre-commit signal.

## 8. Sequencing

1. Code commits the `shop_server_purchase` Unity half (untracked at filing).
2. §2 + §3 (constant at `0`) + §6 + §5 — one Unity/admin/fastlane commit set.
3. §4 migration → SQL in chat → Cesar applies → deploy → smoke.
4. Run the lane → archive → `last_uploaded_build.txt` → set `SHOP_CATEGORY_STRICT_BUILD` →
   redeploy the dashboard.
5. Publish the first character + item shop rows from the admin (G1 now passes) →
   `export_content.py` → commit → next build carries them bundled.
6. `shop_server_purchase` §2.6 (close the legacy spend reason) — on Cesar's word after the
   device pass on that build.

## 9. Acceptance

- [ ] `+ New row` on the Shop panel: pick category `character`, pick a ref, `rowId` prefilled
      `shop_<refId>`, save → draft exists; publish with the constant at `0` → **G1 error**;
      set the constant → publish succeeds; `content_rows` has the row at the new version.
- [ ] `+ New row` with a `rowId` that exists in drafts or published → 409 with the clash named.
- [ ] `+ New row` on Clubs (any non-shop panel) works the same way — one control, all catalogs.
- [ ] G2: a shop row whose `minBuild` is below its referenced row's `min_build` is refused.
- [ ] Audit panel shows `content_row_create` with the admin's email.
- [ ] Lane: with a published row not yet exported, `testflight_build` aborts before Unity runs,
      naming the catalog; after `export_content.py` + commit it proceeds.
- [ ] Client: a published shop row whose ref is not in the client's DB (simulate by publishing
      a row pointing at a deactivated character, or by an overlay-only ref) is **withheld** with
      the warning; nothing blank renders; the summary log counts it. *(Editor with a seeded
      overlay cache, no device needed — Cesar's standing rule.)*
- [ ] Client: a ref whose sprite resolves to `Placeholder` is withheld.
- [ ] Server: `POST /shop/purchase` for a row whose referenced character has
      `min_build > build` → `not_listed / ref_min_build`.
- [ ] Banner copy switches between the two states with the constant; EN + JA.
- [ ] `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy.
- [ ] Full unfiltered EditMode sweep green; dashboard `npm run build` green; backend suite green.

## Out of scope

- Art upload / art-by-URL; creating characters or clubs with new art; `import_content.py`;
  `golfin_characters` mirror on publish; `stockLimit` / `minPlayerLevel`; the locked Roster
  card; the stamina shop.
