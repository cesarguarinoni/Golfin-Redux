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

  §4 APPLIED to prod by Cesar 2026-08-27. All 10 verification rows exact —
      including `bound_zoneless_reads_as_utc`, which is what proves
      `set timezone = 'UTC'` survived the `create or replace`, and
      `fn_still_reads_ref_is_active`, which proves the replace did not drop the
      older refusal. No deploy rode with it (no API source changed). §2.5 smoke
      re-run against prod after the apply, all eight probes identical to the
      shop_server_purchase baseline: /health /notices /banners
      /tournaments/golfin /content?build=9999 all 200, POST /shop/purchase 403
      unauth and 401 on a bad token, garbage route 404.

  CONTENT GATE NOW GREEN. The five `SETTINGS_QUALITY_*` / `SETTINGS_GRAPHICS`
      keys quality_tiers shipped in the CSV but never created in the admin were
      created as drafts and published as `texts` **v12** on 2026-08-27 (drafts
      confirmed byte-identical to published first, so the publish shipped those
      five rows and nothing else; the full 506-row draft set validated through the
      same `validateCatalog` the publish button runs — 0 errors, 0 warnings; audit
      rows written for the five creates and the publish). `content_version.txt`
      re-exported and committed. `export_content.py --check` now exits **0**, so
      `testflight_build` proceeds.

  A PostToolUse guard against that drift was written, wired and then REMOVED the
      same day on Cesar's call — `import_content.py` (`content_two_way` §7) lands
      today and removes the failure mode rather than warning about it. In the
      history at `fd85327c0` if that slips. Lesson BN carries the reasoning.

AWAITING CESAR
  1. §8 steps 4-6 — archive, read `last_uploaded_build.txt`, set
     `SHOP_CATEGORY_STRICT_BUILD`, redeploy the dashboard, then publish the first
     character/item rows.
