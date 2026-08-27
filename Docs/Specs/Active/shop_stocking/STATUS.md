READY_FOR_SELF_REVIEW

Filed 2026-08-27 (Architect via Cowork). Kickoff in Docs/TellCode.md.
Implemented directly by Claude Code across both repos (no subagent chain — same
shape as `shop_server_purchase`, which Cesar asked for directly).

DONE AND VERIFIED
  §2 `+ New row` on the shared CatalogPanel — driven live in the browser against a
      mock-mode dev server: created a `character` shop row (rowId prefilled
      `shop_mock_char` from the RefPicker pick), created a `clubs` row from the
      Clubs panel, and got the 409 on a duplicate id and the localised format
      error on `Shop BAD-Id`. Audit shows `content.draft.create:<catalog>` with
      the admin's email for both creations, and NO audit row for the refused one.
  §3 `lib/buildGates.ts` `SHOP_CATEGORY_STRICT_BUILD = 0`; G1 refused the
      character row at publish with the exact spec message ("1 validation
      error(s); nothing was published"). G1-with-the-constant-set and G2 both
      exercised through the compiled validator. Banner copy switches on the
      constant, EN + JA.
  §5 `testflight_build` now runs `export_content.py --check` right after
      assert-unity-closed. RUNBOOK updated with the export → commit → rerun loop.
  §6 `GeneralShopCatalog.Admit` withholds what this build cannot render;
      `GeneralShopCard.Bind*` hides + LogErrors instead of leaving a blank card.
      Full unfiltered EditMode sweep: 1849 tests, 1846 passed, 0 failed, 3
      pre-existing skips. The new suite was tripwire-proven to actually run.
  Dashboard `npm run build` green; `tsc --noEmit` clean.

AWAITING CESAR (blocking, in this order)
  1. §4 — apply `playlife/backend/migrations/2026_08_28_shop_purchase_ref_min_build.sql`
     (full SQL printed in chat) and paste the verification output. Nothing is
     deployed for it: the change is DB-side only, no API source changed, so the
     live Fly image (v54) is already the right one.
  2. The content gate is RED TODAY, and it is not this task's doing:
     `export_content.py --check` exits 1 because five `SETTINGS_QUALITY_*` /
     `SETTINGS_GRAPHICS` text keys are in `Assets/Localization/LocalizationText.csv`
     and NOT in the published `texts` catalog. The next `testflight_build` will
     abort until those five rows are created in the admin (the new `+ New row`
     control is exactly the remedy) and published.
  3. §8 steps 4-6 — archive, read `last_uploaded_build.txt`, set
     `SHOP_CATEGORY_STRICT_BUILD`, redeploy the dashboard, then publish the first
     character/item rows.
