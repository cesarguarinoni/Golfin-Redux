# Implementer Report — `missions_v1` (Phase A)

> Phase A only. B (Unity bake + gameplay hooks), C (mode card + Mission Selection screen) and
> D (telemetry + docs) are not started. Phase A is deployable on its own with the mode still
> locked, which is exactly the state it is in now.

## Implementation summary

Seven content catalogs, the server tables and endpoints that decide and pay what a mission clear
is worth, the deterministic daily-mission generator, and three admin panels plus a Users-drawer
tab to tune all of it. **Nothing here is reachable by a player** — `modes.missions.locked` is
still `true` in the bundled CSV and Cesar opens the mode with a publish, not a build. That is the
point of doing it in this order: the shop, the level-up and the mode entry fee each had to be
made server-authoritative *after* players were already walking through them (routers/points.py's
three `LEGACY_*` constants record what that cost); this one is authoritative before the door opens.

The load-bearing invariant is that a mission's reward is never a number the client sends. The
client says "I cleared mission 7 in 4 strokes"; `golfin_mission_claim()` reads what mission 7 pays
out of the `golfin_mission_rewards` mirror, decides first-clear vs replay from `mission_progress`,
credits through the same `earn_pts_v2` path `/earn-game` uses, and records the clear — one
transaction, one idempotency key.

## Files modified or created

### GolfinRedux — data

| Path | Change |
|---|---|
| `Assets/Resources/Data/missions.csv` | created — the 40-mission campaign, columns verbatim from `reference/missions.csv` plus `courseId` / `pinIndex` / `staminaDrain` (8 tee, 3 short) |
| `Assets/Resources/Data/mission_start_areas.csv` | created — 162 rows (18 holes × 9 areas). Short-area coordinates are BLANK; the Phase B bake fills them |
| `Assets/Resources/Data/mission_wind_presets.csv` | created — 9 presets, `relDirDeg` relative to the tee→pin bearing |
| `Assets/Resources/Data/mission_loadouts.csv` | created — 13 supplied/own bags with `allowedStartKinds` |
| `Assets/Resources/Data/mission_goal_weights.csv` | created — the difficulty curve, the workbook's prose expanded into machine-scorable rows |
| `Assets/Resources/Data/mission_tiers.csv` | created — 4 bands + the 10-of-10 completion bonus |
| `Assets/Resources/Data/daily_mission_weights.csv` | created — the daily draw table + the freshness `rule` rows |
| `Assets/Resources/Data/*.csv.meta` | created — 7 `TextScriptImporter` metas, fresh GUIDs checked against all 10 219 in the project |
| `Assets/Resources/Data/modes.csv` | modified — `missions` row `target=none` → `mission_select`, `rewardsTextKey=MODE_REWARDS_VARY`. `locked` stays `true` |
| `Assets/Localization/LocalizationText.csv` | modified — 131 new keys, EN + JA (screen chrome, goal/wind/loadout/start-area templates, the 40 mission names) |

### GolfinRedux — tooling

| Path | Change |
|---|---|
| `Tools/content/catalogs.py` | modified — the seven catalogs registered as #11–#17 |

### GolfinRedux — admin dashboard

| Path | Change |
|---|---|
| `Tools/admin-dashboard/lib/missionScore.ts` | created — the difficulty scorer (pure, client-safe) |
| `Tools/admin-dashboard/lib/dailyMissionData.ts` | created — daily calendar + per-player mission reads |
| `Tools/admin-dashboard/lib/missionMutations.ts` | created — pin a recipe, reset a mission; both audited |
| `Tools/admin-dashboard/lib/contentValidate.ts` | modified — seven catalogs' required/numeric/id tables + publish rules 11–17 |
| `Tools/admin-dashboard/lib/contentMutations.ts` | modified — `mirrorMissionRewards` / `mirrorMissionTierBonus`, dispatcher, component loading |
| `Tools/admin-dashboard/lib/contentView.ts` | modified — seven `CatalogView`s + six new facets |
| `Tools/admin-dashboard/lib/contentData.ts` | modified — `FILTERABLE` fields for the new catalogs |
| `Tools/admin-dashboard/lib/rewardsData.ts` | modified — the two cross-surface drift checks |
| `Tools/admin-dashboard/lib/registry.ts`, `components/PanelIcon.tsx`, `lib/i18n.ts`, `lib/types.ts` | modified — three panels registered, three icons, 47 dictionary entries (EN + JA) |
| `Tools/admin-dashboard/app/(panels)/missions/*` | created — Missions panel + the component-dropdown row editor |
| `Tools/admin-dashboard/app/(panels)/mission-components/*` | created — five-tab components panel + the re-score preview |
| `Tools/admin-dashboard/app/(panels)/daily-missions/*` | created — calendar, preview, pin, clear rate |
| `Tools/admin-dashboard/app/(panels)/users/missions-tab.tsx`, `user-drawer.tsx` | created/modified — the Missions tab + its reset confirm |
| `Tools/admin-dashboard/app/(panels)/rewards/rewards-panel.tsx` | modified — renders the drift warnings |
| `Tools/admin-dashboard/app/api/missions/daily/route.ts`, `preview/route.ts`, `app/api/users/[id]/missions/route.ts` | created |
| `Tools/admin-dashboard/lib/__tests__/missionScore.test.ts`, `missionValidate.test.ts`, `mirrorRowMapping.test.ts` | created/extended — 88 new tests |

### playlife (backend)

| Path | Change |
|---|---|
| `backend/migrations/2026_08_29_missions.sql` | created — 6 tables, 2 functions, 5 earn actions, RLS, verification block |
| `backend/migrations/2026_08_29_content_missions_seed.sql` | created — 307 rows, generated by `seed_from_csv.py` |
| `backend/routers/missions.py` | created — `catalog-state` / `claim` / `daily` / `daily/claim` + the admin daily-preview |
| `backend/services/daily_mission.py`, `services/__init__.py` | created — the pure generator |
| `backend/main.py` | modified — mounts `/api/v1/missions` |
| `backend/tests/test_missions.py` | created — 51 tests |

## Screenshot

**None, and none is required for this phase.** Phase A has no Unity UI: the deliverables are CSVs,
SQL, a FastAPI router and web panels. The screenshot / Figma-fidelity / UI-lint gates (Rules 14, 18,
21) attach to the Mission Selection screen, which is Phase C. Phase A's evidence is the test suites,
the round-trip proof and the deployment id, all quoted below.

## Acceptance checklist

Phase A items only; B/C/D items are marked NOT STARTED and are not claimed either way.

| Item | Result | Justification |
|---|---|---|
| Seven catalogs round-trip: seed → export byte-identical → `--check` clean; `Tools/content` tests green | **PARTIAL** | Round-trip PROVEN offline: the generated seed SQL was parsed back into rows and re-rendered through the real `export_content.render_csv` — all 7 byte-identical (script + output in the § Evidence). `Tools/content` tests 26 passed. `--check` cannot be clean until Cesar applies the seed migration — it currently reports exactly that (`content_rows is EMPTY but <csv> has rows` for all seven), which is the correct reading of the current state, not a defect. |
| Every new player-facing string in `LocalizationText.csv` (EN + JA), reaches `texts` via `import_content.py` → publish; `--check` clean for `texts` and `modes`; zero hardcoded `.text` literals | **PARTIAL** | 131 keys added with BOTH locales (the script refuses a blank either side). `import_content.py --apply --catalogs texts,modes` written 132 drafts, **0 conflicts**. The PUBLISH is deliberately left to Cesar — see § Blocked on Cesar. Zero hardcoded literals: Phase A adds no C# and no `.text =` assignment (`git diff --stat` shows no `.cs` file touched). |
| Migration applied; `golfin_mission_rewards` = 40 after the first publish; failing mirror rolls back | **BLOCKED** | DDL is Cesar's to apply. The migration carries its own verification block whose expected output is written out. The rollback property is structural, not hoped-for: `mirrorForCatalog` is called BEFORE `content_publish` and a non-null error returns 502 with nothing published (`lib/contentMutations.ts`). |
| `POST /missions/claim`: first clear / replay / inactive / tier bonus at 10; every POST idempotent | **PARTIAL** | Implemented in `golfin_mission_claim()` and pinned at the router boundary (every status is a 200 payload; the five rpc params; a missing result is a loud 500). The plpgsql arithmetic itself is NOT re-implemented in Python — the same call `test_progress_level_up.py` and `test_shop_purchase.py` each made, since porting it would test the port. It is proven by the verification block + the §21 live E2E at the end of Phase C. |
| Daily: same date → same recipe; `recipe_mismatch` refused; second claim refused; streak 3/7; pinned wins | **PARTIAL** | Determinism, band, freshness, GUSTY-is-Pro-only, supplied-only, no-duplicate-goal-type, ALT_PIN gating and the hash's key-order stability are all tested for real against the SHIPPED catalogs. `recipe_mismatch` / `already_claimed` / `no_recipe` pinned at the router. Pinned-wins is structural (`generate_and_store` upserts with `ignore_duplicates` then RE-READS). Streak 3/7 arithmetic is plpgsql — same caveat as above. |
| Admin: three panels + Users tab live at admin.golfin.world, **deployment id quoted**; validator blocks duplicate goal types / incompatible start↔loadout / unresolvable supplied clubs / RP over cap; warns on band + order drift; EN + JA complete | **PASS** | **Cloudflare Version ID `4ccabd61-e47c-402b-a9b8-1ac49f890088`**, deployed from clean commit `0ef3bd912` (the deploy script prints a DIRTY warning if the tree is not clean; it did not), `https://admin.golfin.world/missions` → HTTP 302 (Cloudflare Access, i.e. the Worker is serving). All four validator blocks and both warnings have a dedicated test in `missionValidate.test.ts`. Every new string has `en` and `ja` — a missing one is a TypeScript error by construction, and `tsc --noEmit` is clean. |
| Bake: `mission_start_areas.csv` coordinates + `pin_count`; `Validate All Holes` tripwire | **NOT STARTED** | Phase B. The 162 slot rows exist and are blank by design; the validator WARNS (never blocks) on an unbaked short area and the generator refuses to draw one. |
| Gameplay: spawn / pin / wind / GUSTY / stroke cap / stamina | **NOT STARTED** | Phase B. |
| Loadout: supplied bag, never persists, pops on reset; `own:` mask; other modes unaffected | **NOT STARTED** | Phase B. |
| Goals: one EditMode test per goal type | **NOT STARTED** | Phase B. |
| Mode card: PLAY opens `MissionSelection` from both entry points | **PARTIAL** | The DATA half is done: `modes.csv` `target=mission_select` and the draft is staged. The dispatch half (`ScreenId.MissionSelection`, both switches) is Phase C. Until then an unrecognised target is withheld by `ModesDatabaseCSV` — and the card is `locked` regardless. |
| Screen: Figma fidelity, tier tabs, NEXT expanded, 8-of-10 gate, daily countdown, offline daily | **NOT STARTED** | Phase C. |
| §21 live E2E, ledger rows quoted | **NOT STARTED** | Phase C, as the spec sequences it. |
| Full EditMode sweep green; backend suite green; dashboard build + deploy green; no Console errors | **PASS** | Backend **172 passed** (51 new). Dashboard **126 passed** (88 new), `tsc --noEmit` clean, `next build` green, deployed. `Tools/content` **26 passed**. EditMode sweep not run and not applicable: Phase A touches no `.cs` file, and the Unity Editor was not running. |

## Evidence

**Round-trip, offline** (`seed SQL → rows → render_csv → byte-compare`):

```
  missions                 40 rows  BYTE-IDENTICAL
  mission_start_areas     162 rows  BYTE-IDENTICAL
  mission_wind_presets      9 rows  BYTE-IDENTICAL
  mission_loadouts         13 rows  BYTE-IDENTICAL
  mission_goal_weights     36 rows  BYTE-IDENTICAL
  mission_tiers             4 rows  BYTE-IDENTICAL
  daily_mission_weights    43 rows  BYTE-IDENTICAL
ROUND-TRIP: all 7 catalogs byte-identical
```

**The strongest single check in the phase** — `mission_goal_weights.csv` is an *expansion* of the
workbook's prose (`"≤150→0, ≤200→1, else 2"` became three rows with a `match` column), so the only
honest proof the expansion is faithful is that scoring the 40 shipped missions with it reproduces
the 40 numbers the designer wrote. It does, **in both languages** — `test_the_scorer_reproduces_
all_forty_shipped_difficulty_scores` (Python) and 40 parameterised cases in `missionScore.test.ts`
(TypeScript). That fixed point is also what keeps the two implementations from drifting.

**Importer plan, before apply** (0 conflicts):

```
catalog         add  change   same  conflict  csv
  texts         131       0    508         0  Assets/Localization/LocalizationText.csv
  modes           0       1      4         0  Assets/Resources/Data/modes.csv
```

**One real bug the tests found, in the generator.** With the short start areas unbaked — the actual
Phase A state — `_draw` drew `startKind=short` on ~45 % of days and then raised `GenerationError`,
taking the daily down for half of every week. Fixed by drawing the start KIND only from kinds that
have a usable area. Found by `test_an_unbaked_short_start_area_is_never_drawn`, which existed
because the spec says short areas are baked in Phase B.

## Blocked on Cesar

Three steps, in this order. Nothing in Phase A is finished without them, and none of them is
something I should do unilaterally — two are DDL and one is outward-facing.

1. **Apply `2026_08_29_missions.sql`**, then `2026_08_29_content_missions_seed.sql` (Supabase SQL
   editor). Both carry verification blocks; the second's expected counts are in its header.
2. **Publish `texts`** from the admin (131 additive keys at `min_build 2442`; nothing renders them
   until Phase C ships).
3. **Publish `missions` and `mission_tiers`** — this is what writes the two mirrors, and it cannot
   run before step 1 because the mirror tables would not exist. `golfin_mission_rewards` must read
   40 rows and `golfin_mission_tier_bonus` 4 afterwards. **Publish `modes` last, or hold it for
   Phase C** — it is the only one with any player-visible effect, and none is needed yet.

After 1–3, `python3 Tools/content/export_content.py --env-file … --check` should come back clean.

## Spec deviations

Every one of these is flagged rather than silently taken.

1. **Localization keys are `UPPER_SNAKE`, not the spec's dotted `mission.<key>.name` / `goal.SCORE.0`.**
   `texts` row ids are constrained to `/^[A-Za-z0-9_]+$/` in `contentValidate.ts`, so a dotted key
   could never be created or edited from the admin — the exact surface §A7 requires every string to
   pass through. The keys are `MISSION_NAME_<KEY>`, `GOAL_SCORE_0`, `WIND_CALM`, `LOADOUT_SUP_FULL`,
   which is also the `{SCREEN}_{ELEMENT}` convention in CLAUDE.md. **This looks like a spec bug.**
2. **`mission_start_areas.csv` is NOT "verbatim from the workbook sheet of the same name".** The
   spec's own column list (`id, courseId, holeId, kind, x, y, z, bake_hash`) describes a per-hole
   BAKED table; the `StartAreas` sheet is nine per-kind definitions with no coordinates. The two
   sentences cannot both hold. I followed the column list, which is the more specific statement and
   the one Phase B's baker writes, and carried the sheet's `label`/`weight` per row (the validator
   warns when rows sharing an `areaId` disagree, so the denormalisation cannot drift silently).
3. **`daily_mission` caps are 60, not the spec's `daily_cap 30`.** The generator can draw
   `DOUBLE_RP`, which pays 2× base; a cap of 30 would make a DOUBLE_RP day silently unpayable —
   the "wrongly earn nothing" half of the standing invariant. `pts` stays 30 so the Rewards panel
   shows the base and the drift warning stays meaningful. Once-per-day is enforced by
   `daily_mission_claims`' primary key, which is where §A3 puts it.
4. **Two tables beyond §A3's list.** `mission_claims` (the idempotency ledger — `earn_pts_v2`'s
   replay guard is keyed on the action type, so it cannot make a *failed* attempt idempotent, and
   §A3 asks for "idempotent replay of every POST"). Same class of deliberate addition as
   `content_versions` in `seed_from_csv.py`.
5. **The admin's *Preview next 14 days* is a proxy, not a port.** The generator stays the single
   Python implementation; the dashboard forwards to a new admin-key-gated
   `GET /api/v1/missions/admin/daily-preview`. It needs `PLAYLIFE_API_URL` and `PLAYLIFE_ADMIN_KEY`
   on the Cloudflare deployment; **until they are set, Preview says so in amber and the panel's
   other three controls work normally.** A TypeScript port would have been a second implementation
   of the draw, which is the one thing a deterministic recipe cannot survive.
6. **Spec §A5 vs the workbook, resolved in the spec's favour** (as instructed) and recorded in
   `daily_mission_weights.csv`'s header: the modifier is `ALT_PIN` (sheet said `MIRROR_PIN`), hole
   freshness is 5 days (sheet said 3), Pro band is Fri–Sun.

## Open questions for Architect

1. **Deviation 1 (dotted vs `UPPER_SNAKE` loc keys)** — confirm `UPPER_SNAKE`. Phase C reads these
   by name, so it is cheapest to settle now.
2. **Deviation 2 (`mission_start_areas` shape)** — confirm the baked per-hole table is what Phase
   B's `MissionStartAreaBaker` should write, and that per-hole `weight` tuning is wanted at all
   (the alternative is a per-kind weight column somewhere else).
3. **A pre-existing drift, not mine:** `playlife/backend/migrations/2026_08_24_content_seed.sql` has
   been overwritten in the working tree with a `texts`-only seed (file mtime **2026-08-28 21:47**,
   before this session — a `seed_from_csv.py --catalogs texts` run without `--out`). The day-one
   migration is the applied record of that day. Left untouched and uncommitted per the close-out
   rule. Restore with:
   `git -C ~/Documents/playlife checkout -- backend/migrations/2026_08_24_content_seed.sql`
