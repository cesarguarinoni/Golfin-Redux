# ARCHITECT_REVIEW — `gacha_admin_catalogs`

**Verdict: PASS** (Architect via Cowork, 2026-08-31). Ready for Cesar's approval.

Reviewed commit `b42c8bff7` (+ `44a4ce261` docs) against the SPEC, checking the repo and the
live admin — not the report.

## Verified in the repo

- `git show --stat b42c8bff7`: 35 files, the set the spec lists; no scene, no physics, no
  `LocalizationText.csv`.
- The four CSVs match SPEC §2 byte-for-byte in content (22-column `gacha_banners` header, the
  four rows with the specced values incl. `banner_test_a` with no pity; 6 rate rows summing to
  10 000; 11 pool rows with the rarities I read off `Clubs.csv`/`Items.csv`; `ticket_types` 0/1).
- `catalogs.py` lines 169–172: the four `Catalog(...)` entries, id columns as specced.
- `GachaStage2Tests.cs`: zero deleted lines in the diff (`grep -c "^-[^-]"` → 0) — the 15
  existing tests are untouched; three added.
- `GachaBannerModel.ParseCsv`: header-indexed (`Field("bannerId")`), quote-aware
  `ParseCsvLine`, blank-id skip, truncated-row skip preserved.
- `contentValidate.ts`: rules present where claimed — sum = 10 000 (both publishes), rarity with
  rate and no entry ("would resolve to nothing"), `pityThreshold` 0/blank → warn on
  `pityMinRarity`, both-locales on active banners, `artSprite`/`artUrl` on active banners,
  `costX10 > 10 × costX1` warn.
- `contentArtMutations.ts`: `gacha_banners` + `artUrl` registered; `registry.ts`: three
  panels, `ticket` icon; seed migration present in both repos.

## Verified live (Cesar's Chrome session, 2026-08-31)

- Sidebar footer stamp reads **`b42c8bff7`** — the §23 check the report could not do.
- `/gacha-banners`: four rows, `banner_inactive` OFF, three LIVE, pity column
  `50 → Legendary` / `(none)` / `30 → Rare`; row editor shows pool + ticket pickers, pity block,
  per-locale title fields, the amber "no text in the artwork" hint, `featuredRefIds`.
- `/gacha-pools`: effective-odds table totals 100.00 % (Common 18.33/18.33/11.00/7.33 — the
  55 % × 100/300 etc. split is right); **Simulate 10 000** rendered the same table as the report
  (max Δ −1.00 pt, pity hits 89, guarantee hits 142); Rates tab shows `Σ = 10000 ✓`, published v3.
- `/ticket-types`: two rows, EN + JA names.
- `screenshots/rewards_center_after.png`: three live banners, countdown, dots — unchanged
  behaviour through the new parser.

## Deviations — all accepted

1. Seed applied over PostgREST by Code rather than SQL pasted for Cesar: data-only, idempotent,
   `.sql` archived in both repos. Fine. (DDL migrations in spec B still go through Cesar.)
2. Publish via the `content_publish` RPC with `validateCatalog` run by hand first — acceptable
   since the drawer rendering was then checked live above.
3. README catalog list rebuilt to 20 (it was stale at nine) — better than the spec asked.
4. Sibling kind→catalog map instead of extending the shop's — correct reasoning (`bag` vs
   `ticket`).
5. Two-switch badge (`active` + `is_active`) — matches the spec's intent.

## Notes (no action needed for approval)

- The editor still shows `taglineEn/Ja` fields. Cesar decided title-only AFTER this spec was
  filed; the columns stay (I4) and spec C ignores them. Cosmetic; leave.
- Found-in-passing: `ALLOWED_COLUMNS` in `contentArtMutations.ts` is flat, not per-catalog —
  a `portraitUrl` upload against `gacha_banners` returns 200. Pre-existing, admin-only, audited.
  Filed as a follow-up quick task, not blocking.
- The Rewards Center screenshot shows the current bundled banner art has copy baked in ("GET
  Drivers, Woods, Irons", "CHANCE TO GET LEGENDARY GEAR!") and the card carries authored pity
  lines with placeholder "99 pulls" pills (`GACHA_PITY_A_RANK` / `GACHA_PITY_S_RANK`). Both are
  exactly what Cesar's "text is UI-authored" decision replaces: the next uploaded art is
  text-free, and **spec C is amended (pre-kickoff, so no amendment-loss risk) so the pity /
  guarantee lines bind to the row** (`pityThreshold`, `pityMinRarity`, `guaranteeMinRarityX10`)
  and hide when a banner has none.

## For Cesar

Nothing left to check by hand — both items in the report's §3 are closed above. Approve → Code
moves the folder to `Completed/`, and spec B (`gacha_server_pull`) can start now that the seed is
on prod.
