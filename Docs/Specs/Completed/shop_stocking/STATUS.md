DONE

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

  §8 STEPS 4-5 DONE 2026-08-27. Lane run end to end: all four gates passed
      (clean tree, Unity closed, the NEW content gate green on its first real
      lane run, then Unity batchmode + xcodebuild), **build 2350 (1.5.7)
      uploaded to TestFlight** — Apple's own transporter confirmed the upload,
      not an exit code. `last_uploaded_build.txt` 2333 -> 2350.
      `SHOP_CATEGORY_STRICT_BUILD` set to **2350**, READ from that file (the old
      2334 guess was 16 commits off). Dashboard redeployed twice — Cloudflare
      version `b4aa4467-f9f6-4b8e-8282-2992c7b98bd2` at 100%. First non-club
      rows published as **shop_catalog v4**: `shop_char_mike` (150 RP) and
      `shop_item_repairkit_common` (75 RP), both at min_build 2350, both inside
      their economy band; exported and committed so build 2351+ bundles them.

  §8 STEP 6 DONE 2026-08-27 — the legacy `/points/spend` `shop_purchase` reason
      is CLOSED (playlife 357ce7f, playlife-api v55). The gate was "once testers
      are on the build carrying §3"; 2350 was on TestFlight and the ledger showed
      ZERO `shop_purchase` debits across its entire 128-row history, so the door
      had never sold anything and closing it broke no flow that had ever run.

  THE -1 "UNLIMITED" SWALLOW, flagged at close-out as the one known residual, is
      CLOSED 2026-08-27 — server migration
      `2026_08_29_shop_purchase_unlimited_refusal.sql` (applied, 11/11 verification
      rows) plus the client refusing it in `ShopTransaction.HoldsUnlimited` and
      never rendering a BUY for it. Two locks, neither relying on the other.

AWAITING CESAR
  2. ~~The endpoint has never sold anything.~~ **CLOSED 2026-08-27** — Cesar ran
     2350 and bought `shop_char_mike` for 150 RP. Purchase row, grant and RP debit
     all landed with the identical microsecond timestamp, the grant applied 148 ms
     later, and `char_mike` is in the inventory blob. The end-to-end chain this
     task built — admin row -> published catalog -> min_build gate -> client card
     -> server price -> debit + grant in one transaction -> delivery — is proven
     on a real device. Remaining §6 edges (sale window, replay, kill switch,
     already-owned, delivery-survives-death) are still unexercised.
  3. Approval + moving this folder to `Docs/Specs/Completed/` — Cesar's, per
     CLAUDE.md rule 6. No subagent may write DONE.
