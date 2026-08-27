# Implementer Report — `shop_stocking`

Implemented directly by Claude Code (main thread) across both repos, 2026-08-27 —
no subagent chain, the same way `shop_server_purchase` was run.

## Implementation summary

Three gaps closed so a row published in the admin actually reaches a player, and a
row that cannot reach one never renders. The admin gained the `+ New row` control it
never had (on the SHARED panel, so all seven catalogs get it), with the row id
validated server-side and the catalog's id column written from it so the two cannot
disagree. The two build-gate decisions became validator rules behind one constant in
a new `lib/buildGates.ts`. The release lane now refuses to build a repo whose bundled
CSVs disagree with the published catalogs. And the client withholds any shop row
whose referenced row or art it cannot resolve, instead of instantiating a blank card
with a live BUY button.

## Files modified or created

### GolfinRedux

| Path | Change |
|---|---|
| `Tools/admin-dashboard/lib/buildGates.ts` | **created** — `SHOP_CATEGORY_STRICT_BUILD = 0` plus `shopCategoryBuildPending()`. Pure/client-safe so the validator AND the panel read one constant. Carries the "read it from `last_uploaded_build.txt`, never infer it" instruction and why "last upload + 1" is wrong (build number = commit count). |
| `Tools/admin-dashboard/lib/contentValidate.ts` | modified — rules **G1** (a non-club/ball row needs `minBuild ≥` the constant; constant `0` ⇒ error) and **G2** (shop `minBuild ≥` the referenced row's `min_build`), both on ACTIVE rows only. Also now the home of the row-id shape rules (`ROW_ID_MAX`, `rowIdPattern`, `isValidNewRowId`) so the form and the route share one definition. The `referenced` lookup was hoisted, unchanged, so the gates can see it. |
| `Tools/admin-dashboard/lib/contentMutations.ts` | modified — `upsertDraftRow` validates the row id **on creation only** (shape + length, 409 on a draft or published clash) and writes `data[ID_COLUMN[catalog]]` from the row id. |
| `Tools/admin-dashboard/lib/types.ts` | modified — `ContentRowInput.expectNew?: boolean`, the caller's intent that makes a draft collision detectable at all. |
| `Tools/admin-dashboard/lib/contentView.ts` | modified — re-exports `isValidNewRowId` / `ROW_ID_MAX` alongside `ID_COLUMN`, so client components import shapes from one module. |
| `Tools/admin-dashboard/app/(panels)/_content/catalog-panel.tsx` | modified — the `+ New row` button beside Review & publish, a `creating` flag, and the `rowIdCtx` third argument threaded into `editorExtras`. |
| `Tools/admin-dashboard/app/(panels)/_content/row-editor.tsx` | modified — `isNew` create mode: the row id is an input (with live shape feedback), the id column is dropped from the raw field list in EVERY mode, `expectNew` is sent, and a 409 renders the localised "already taken" message plus the server's own text. |
| `Tools/admin-dashboard/app/(panels)/shop/shop-panel.tsx` | modified — `SERVER_PRICE_ENFORCED_FROM_BUILD` deleted, replaced by the shared constant; two banner states; `rowId` prefilled `shop_<refId>` after a RefPicker pick on a new row. |
| `Tools/admin-dashboard/lib/i18n.ts` | modified — EN + JA for `c.newRow`, `c.edit.newTitle`, `c.edit.rowIdHint`, `c.edit.rowIdInvalid`, `c.edit.rowIdTaken`, `sh.notice.pendingHeadline`, `sh.notice.pendingBody`. |
| `fastlane/Fastfile` | modified — `export_content.py --check` runs right after `assert-unity-closed.sh`; non-zero aborts. Comment states why the lane must never auto-export (the build number is the commit count). |
| `Docs/TESTFLIGHT_RUNBOOK.md` | modified — step table renumbered to 7 steps; new § "Step 3: the content gate" with the export → commit → rerun loop, the two drift directions, and the currently-RED `texts` state. |
| `Assets/Scripts/UI/Shop/GeneralShopModel.cs` | modified — `Admit` now calls `UnrenderableReason(entry)` after the window verdict: resolves the ref in the matching DB, requires `isActive` and a non-`Placeholder` primary sprite, withholds + `LogWarning`s + counts (`unresolvable` in the summary line). A null DB singleton logs ONCE per database per load and admits. `_resolverOverride` is the reflection-only test seam; `Reload()` clears it. |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs` | modified — the four `Bind*` null-row early returns became `HideUnbindable(entry, kind)`: `gameObject.SetActive(false)` + `LogError`, because the branch should now be unreachable and an unreachable branch that still renders the bug is not a safety net. |
| `Assets/Scripts/UI/Shop/Tests/GeneralShopAdmitResolutionTests.cs` (+ `.meta`) | **created** — 5 EditMode tests over the SHIPPING `Admit` (reflection, like `GeneralShopCategoryTests`): withholds an unresolvable ref, withholds a placeholder sprite, keeps a resolvable row, and the two null-database cases. |

### playlife

| Path | Change |
|---|---|
| `backend/migrations/2026_08_28_shop_purchase_ref_min_build.sql` | **created, NOT YET APPLIED** — `create or replace` of `golfin_shop_purchase()` carrying the applied body forward with ONE change: step 7 also reads the referenced row's `min_build` and refuses `{"status":"not_listed","reason":"ref_min_build"}`. The 08-27 migration is untouched. Verification block re-proves the security posture, the whole bound-parser matrix (including zoneless-reads-as-UTC, which is what would show a lost `set timezone`), and both refusals by source. |

Committed alongside, as their own commits, because they were deployed but never
committed: the `shop_server_purchase` Unity half, its admin banner half, and its
backend half (`playlife`).

## Acceptance (SPEC §9)

| # | Item | Result | Evidence |
|---|---|---|---|
| 1 | `+ New row` on Shop: pick `character`, pick a ref, `rowId` prefilled `shop_<refId>`, save → draft exists; publish with the constant at `0` → **G1 error**; set the constant → publish succeeds | **PASS** | Driven live in the browser (mock-mode dev server on :3111). Picking `mock_char` prefilled `shop_mock_char`; save produced the row (`category character · refId mock_char · rpCost 300 · sortOrder 10`, ID column `shop_mock_char`) and the "1 unpublished" badge. Publish returned *"1 validation error(s); nothing was published"* with `shop_mock_char/min_build: The client build that renders "character" rows has not been uploaded yet; set SHOP_CATEGORY_STRICT_BUILD (lib/buildGates.ts) after the archive, from Docs/Versioning/last_uploaded_build.txt.` The set-constant half was proven against the compiled validator (constant 2400: `minBuild 2399` errors, `2400` passes) rather than by editing the shipped constant. |
| 2 | `+ New row` with a `rowId` that exists in drafts or published → 409 naming the clash | **PASS** (drafts) / **PASS by inspection** (published) | Live: re-entering `shop_mock_char` returned *"Row id "shop_mock_char" is already taken in this catalog — Row id "shop_mock_char" already exists as a DRAFT row in shop_catalog. Edit that row instead."* and wrote no audit row. The published-clash branch cannot be reached in mock mode (the fixtures' drafts mirror published exactly, so the draft check always fires first); it is the same `fetchAllRows("content_rows", …)` read, verified by code. |
| 3 | `+ New row` on Clubs works the same way — one control, all catalogs | **PASS** | Live on `/clubs`: the drawer opened with `row_id → data.id`, the full required-column field list (name/type/rarity/brand/basePower/…) and **no `id` field**; saved `club_driver_test` and it appeared in the table with the dirty count at 2. |
| 4 | G2: a shop row whose `minBuild` is below its referenced row's `min_build` is refused | **PASS** | Compiled-validator harness: shop row `minBuild 10` against a character row with `min_build 2400` → `min_build 10 is below the min_build of "char_hi" in characters (2400). The shop row would be visible on a build that cannot see the row it sells.` |
| 5 | Audit shows the creation with the admin's email | **PASS**, with a deviation | Two rows in the Audit panel: `content.draft.create:clubs` and `content.draft.create:shop_catalog`, both `cesar.guarinoni@wonderwall-g.com`, table `content_drafts`, before `—`. **The action name is the existing `content.draft.create:<catalog>`, not `content_row_create`** — see § Spec deviations. |
| 6 | Lane: with a published row not yet exported, `testflight_build` aborts before Unity runs, naming the catalog | **PASS** | `python3 Tools/content/export_content.py --env-file … --check` exits **1** today against prod and names the catalog and ids (`texts: DRIFT — 506 rows in LocalizationText.csv vs 501 in the catalog … SETTINGS_GRAPHICS, SETTINGS_QUALITY_*`). The lane calls exactly that command, and fastlane's `sh` raises on non-zero, so the abort happens before `unity-build-ios.sh`. Not run through fastlane itself — fastlane is not installed on this Mac (RUNBOOK § one-time setup). |
| 7 | Client: a published shop row whose ref is not in the client's DB is **withheld** with the warning; nothing blank renders; the summary counts it | **PASS** | `GeneralShopAdmitResolutionTests.Admit_withholds_a_row_whose_reference_this_build_cannot_resolve` — the shipping `Admit`, with the DB lookup faked, admits 0 entries and logs `'shop_char_ghost' WITHHELD …`. The counter is folded into the load summary line (`{_unresolvable} withheld as unrenderable`). |
| 8 | Client: a ref whose sprite resolves to `Placeholder` is withheld | **PASS** | `Admit_withholds_a_row_whose_art_resolved_to_the_placeholder`; the production comparison is by sprite NAME (`Usable()`), because `ClubDatabaseCSV.LoadSprite` substitutes the shared `Placeholder` asset rather than returning null. |
| 9 | Server: `POST /shop/purchase` for a row whose referenced character has `min_build > build` → `not_listed / ref_min_build` | **NOT RUN — blocked on Cesar** | The migration is written and the diff against the applied function is exactly the intended change (declared `v_ref_min_build`, `select is_active, min_build`, the new refusal). It has not been applied, so nothing has exercised it. |
| 10 | Banner copy switches between the two states with the constant; EN + JA | **PASS** | Live, both languages: EN *"Server pricing is live; the client build is pending upload — character and item rows cannot be published yet."*, JA *「サーバー価格は有効です。ただしクライアントビルドが未アップロードのため、キャラクター行とアイテム行はまだ公開できません。」*. The non-pending state is the pre-existing `sh.notice.headline/body` with `{build}`, unchanged except for its source constant. |
| 11 | `/health`, `/notices`, `/banners`, `/tournaments/golfin` still 200 after deploy | **N/A this pass** | No deploy happened: §4 changes nothing in the API source, so the running image is already correct. To be re-run after the migration is applied, together with the §2.5 smoke. |
| 12 | Full unfiltered EditMode sweep green; dashboard `npm run build` green; backend suite green | **PASS** (Unity, dashboard) / **NOT RUN** (backend) | EditMode: **1849 total, 1846 passed, 0 failed, 3 skipped** (the three pre-existing `HoleCompleteDriverTests` skips). Tripwire-proven: an `Assert.Fail` in the new fixture produced exactly 1 failure in the same 1849, then was removed and the suite went green again. Dashboard: `npx tsc --noEmit` clean and `npm run build` green. The backend suite was not run — this task adds no Python; `test_shop_purchase.py` is router-level and cannot see a plpgsql body. |

## Spec deviations (all deliberate, all flagged)

1. **Audit action stays `content.draft.create:<catalog>`, not `content_row_create`.**
   The spec asked for a distinct creation action "so the Audit panel shows creation
   as creation". That distinction already existed — `upsertDraftRow` has always
   branched `create` vs `update` on whether the draft was there before — and every
   other action in that module is dot-namespaced with the catalog appended
   (`content.publish:shop_catalog`, `content.rollback:…`). Renaming this one to a
   bare `content_row_create` would break the family AND drop the catalog from the
   action string. The intent is met; the name is not the one the spec wrote.
2. **The row-id regex is per-catalog: `^[a-z0-9_]+$`, except `texts` at `^[A-Za-z0-9_]+$`.**
   A single lower-case rule would make it impossible to create a localisation key
   (`SETTINGS_QUALITY_LOW`) — UPPER_SNAKE is the convention for every one of the 501
   existing text keys. It applies to CREATION only; existing ids are never re-checked.
3. **G1 and G2 apply to ACTIVE rows only.** `min_build` is immutable once published
   (rule 7), so a row published before these rules existed could never satisfy them
   again and the whole catalog would become unpublishable with no way out.
   Deactivating is the way out (§I6, "deactivate is the delete"), and a deactivated
   row is dropped by `Admit` on `is_active` before category parsing, so it cannot
   reach the failure G1 prevents.
4. **The client resolver requires `isActive` on all four categories, not just
   character and item.** The spec's `resolve()` listed `isActive` for characters and
   items only. A deactivated club or ball is refused by the server the same way
   (`ref_inactive`), so admitting it would render precisely the "card that can only
   fail" the spec's §1 invariant forbids. Checking all four is the invariant; the
   asymmetry looked like an oversight rather than an intent.
5. **`expectNew` was added to `ContentRowInput`.** Without a statement of intent, the
   PUT is an upsert and "create a row whose id is taken" is indistinguishable from
   "edit that row" — the create silently wins, which is the exact hazard the 409 is
   for. One optional boolean, defaulted off, so every existing caller is unchanged.

## Not done, and why

- **§4 is written but NOT applied or smoked.** The SQL is printed in chat for Cesar
  (house rule: migrations are applied by him, and the verification output is what
  proves it took). No deploy is pending with it — `backend/routers/shop.py` is
  unchanged, so the live image (v54) already serves the right code and the function
  body is DB-side.
- **§8 steps 4-6** (archive → read `last_uploaded_build.txt` → set
  `SHOP_CATEGORY_STRICT_BUILD` → redeploy the dashboard → publish the first
  character/item rows) are Cesar's, by construction: the constant must be READ from
  an archive that does not exist yet.
- **The dashboard was not redeployed.** The admin half of this task is committed but
  not live; deploying is a separate, outward-facing action and the constant it would
  ship is about to change anyway (step 4 above).
- **Out of scope, untouched:** art upload / art-by-URL, `import_content.py`, the
  `golfin_characters` mirror on publish, `stockLimit` / `minPlayerLevel`, the locked
  Roster card, the stamina shop, and closing the legacy `/points/spend` shop reason.

## Found while working — the content gate is RED today

`export_content.py --check` exits 1 right now, and the cause predates this task: five
keys added in Unity by the `quality_tiers` work — `SETTINGS_GRAPHICS`,
`SETTINGS_QUALITY_AUTO`, `SETTINGS_QUALITY_LOW`, `SETTINGS_QUALITY_MID`,
`SETTINGS_QUALITY_HIGH` — are in `Assets/Localization/LocalizationText.csv` and not in
the published `texts` catalog. That is the CSV-ahead-of-catalog direction, which the
exporter cannot fix: it never deletes (I6), so it keeps the extra lines verbatim and
the drift persists. **The next `testflight_build` will abort until those five rows are
created in the admin and published** — which the `+ New row` control this task adds is
exactly what makes possible. Documented in the RUNBOOK's new § "Step 3".
