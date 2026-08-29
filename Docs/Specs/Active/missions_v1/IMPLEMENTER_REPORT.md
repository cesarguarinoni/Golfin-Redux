# Implementer Report — `missions_v1` (Phases A + B)

> Phases A and B. C (the Mission Selection screen) and D (telemetry + docs) are not started —
> except C1, the mode-card target wiring, which was pulled forward into B because Phase A's
> `modes.csv` edit had made the bundled `missions` row unroutable and broke four
> ModesOverlayTests. The mode is still locked and still has no screen: nothing built so far is
> reachable by a player.

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


---

# Phase B — start-area bake, session, session bag, goal evaluator

## Implementation summary

The layer under the screen: where a mission puts the ball, what it changes while it runs, and
how it decides whether you cleared it. A new leaf assembly `Golfin.Gameplay.Missions` owns the
state; the Viewer applies it in one call; the Hole Complete modal settles it. Full Unity
EditMode sweep green — **2021 tests, 2018 passed, 0 failed, 3 pre-existing skips**.

## Files (Phase B)

| Path | Change |
|---|---|
| `Assets/Scripts/Editor/CourseImporter/MissionStartAreaBaker.cs` | created — `Golfin ▸ Missions ▸ Bake Start Areas` |
| `Assets/Scripts/Editor/CourseImporter/TreeBakeValidator.cs` | modified — third status column: the start-area drift gate |
| `Assets/Resources/Data/mission_start_areas.csv` | baked — 89/90 short rows + `pin_count` + `bake_hash`; header rewritten |
| `Assets/Scripts/Gameplay/Missions/*` | created — asmdef, `MissionDefinition`, `MissionGoal`, `MissionSession`, `MissionSessionBag`, `MissionGoalEvaluator`, `MissionResult` |
| `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` | modified — `OnSessionReset` event (the inverted dependency) |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | modified — `ApplyMissionOverrides` + `AdvanceMissionGust` |
| `Assets/Scripts/BagManager.cs` | modified — `GetClubsInBag` reads the session bag |
| `Assets/Scripts/StaminaRuntimeService.cs` | modified — the mission's own drain |
| `Assets/Scripts/Save/SaveData.cs`, `SaveSchemaMigrator.cs` | modified — `missionProgress`, schema v12 |
| `Assets/Scripts/Economy/MissionsClient.cs`, `Net/Endpoints.cs` | created/modified — the claim path |
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | modified — settle, claim, mirror, and suppress the hole's own rewards |
| `Assets/Scripts/UI/ScreenManager.cs`, `ModeSelect/*` | modified — C1: `ScreenId.MissionSelection`, `TargetMissionSelect`, both switches |
| `Assets/Scripts/Gameplay/Tests/MissionGoalEvaluatorTests.cs`, `MissionSessionTests.cs` | created — 40 tests |

## Acceptance checklist (Phase B items)

| Item | Result | Justification |
|---|---|---|
| Bake: GREEN/FRINGE/FAIRWAY/ROUGH on all 18, SAND where a bunker exists, `pin_count` filled | **PASS** | 89 of 90 short rows baked, y range 5.75–40.28 m with **zero** at 0, all 72 tee rows coordinate-less, `pin_count` and `bake_hash` on every row. The one blank is hole 13's SAND — see below. |
| `Validate All Holes` fails on a hand-edited coordinate (tripwire, §20) | **PASS** | 18/18 PASS baked. Nudging hole 5's FAIRWAY x by 1 m and leaving the hash produced `05 FAIL 1/9 drifted — FAIRWAY bake_hash is 82e9be72 but its coordinates hash to 906b2291`. Restored; 18/18 again. |
| Gameplay: start at the mission's area, pin index honoured, wind = preset, GUSTY changes between shots, stroke cap, stamina drain | **PARTIAL** | Implemented and unit-tested (cap = the tightest goal; GUSTY stays in 6–18 and is deterministic per (mission, shot); `DrainOverride` returns the config value with no mission). The position-trace assertion on Hole 01 per start kind needs a **play-mode round**, which is Phase C work — there is no screen to start a mission from yet. |
| Loadout: supplied bag shows only the listed clubs, never persists, pops on end and on `ResetSession`; `own:` mask; Practice/1v1/tournaments unaffected | **PASS** | `MissionSessionBag` is a stack in front of `PlayerClubData`, which is never written. Push/pop balance, the double-Begin case, `ResetSession` clearing a running mission, and "1v1 and tournaments are never in a mission" each have a test. |
| Goals: one EditMode test per goal type (pass and fail) | **PASS** | 24 tests over all 14 types, both directions each, driven through `GameSession.RecordShot` — the same path the game writes. Plus the yard/metre conversion, the two vocabulary mappings (Bunker→Sand, `SW`→"Sand Wedge"), progressive-fail, and "progressive never marks a goal MET". |
| Mode card: PLAY opens `MissionSelection` from Home carousel AND Mode Select | **PARTIAL** | Both switches route `mission_select` and both use the shared constants. Cannot be exercised end-to-end until the screen exists (Phase C) — `ShowScreen(ScreenId.MissionSelection)` currently has nothing to show. |
| Full EditMode sweep green; no Console errors | **PASS** | 2021 / 2018 passed / 0 failed / 3 pre-existing skips. Both new suites proven live with a tripwire: 2021 → 2023 with both named, then removed. |

## What Phase B found

**Hole 13 has no greenside bunker.** Its nearest sand is 156 m from the green; every real
greenside bunker on this course is 14–33 m. The bake leaves its SAND row blank, which is §B1's
own "skip kind if the hole has none" — and it makes **mission 37 (`l_sand_up_down`, "Sand Save",
hole 13, `startAreaId=SAND`) unpublishable** until it is re-sited or a bunker is authored. A
design call, not mine.

**The entire ROUGH column was baking to y = 0** — a ball under the course. Rough has no polygons
in `zones.json` at all (it is the classifier's DEFAULT surface), so there is no zone mesh under
the probe and `TrySampleMeshY` correctly failed. Fixed by wiring the baked `heightmap.bytes` as
the height source for probes that are not on an overlay, which is the same pairing
`PhysicsLabController` wires at runtime.

**A test caught a real fragility in the teardown wiring.** The first version armed
`GameSession.OnSessionReset` from a `[RuntimeInitializeOnLoadMethod]` — which does not run in
EditMode at all, and in a player runs once at load, so the subscription's existence had nothing
to do with whether a mission was in progress. Armed from `Begin()` instead, which ties the
guarantee to the state it guards.

**ALT_PIN is near-inert, and the data says so.** Only hole 1 has more than one pin candidate (3);
every other hole has exactly 1. The daily generator already gates ALT_PIN on `pin_count >= 2`,
so it will almost never draw — correct behaviour, but worth knowing before anyone tunes its
weight. New pin authoring is explicitly out of scope.

## Phase B deviations

7. **The bake reads the TRACKED JSON, not the `Hole_NN_Geo` scenes.** §B1 says scenes; those are
   gitignored and per-machine (`.gitignore:111`), so a scene-driven bake would produce
   coordinates and a `bake_hash` drift gate that only mean something on the machine that ran it —
   the next person to run `Validate All Holes` would see 18 failures caused by nothing.
   `Resources/HoleData/<course>/Hole_NN/{zones,green}.json` is the same geometry, tracked, and is
   what the runtime itself classifies against.
8. **ROUGH steps outward past 8 m when it has to.** §B1 says 8 m lateral; on holes 2, 7, 10, 14
   and 18 both sides at 8 m are still fairway. The search steps out (8 → 40 m) until the probe
   leaves every zone polygon, and the row's note records the distance actually used. A missing
   ROUGH row would have been worse than an 12 m one.
9. **A greenside radius (50 m) gates SAND.** §B1 says "the bunker nearest the green"; taken
   literally that gives hole 13 a bunker 156 m away. The threshold separates two clusters that
   are 123 m apart on this course.
10. **CARRY is evaluated as total distance.** `ShotRecord` has `DistanceXZMeters` and nothing
    separating flight from roll, so a true carry cannot be read from it. No campaign mission uses
    CARRY and the daily never draws it, so nothing ships on the approximation — but a CARRY goal
    authored in the admin would be easier than it reads. Flagged in the evaluator itself.
11. **The mission claim is online-only — no offline queue**, which is the opposite call from
    `/points/earn-game`. That queues earns because the amount is already known and replaying it
    is exact; a mission claim's amount is decided BY THE SERVER at claim time, so a queued claim
    is a deferred DECISION made against whatever the catalog says days later. The idempotency key
    makes retrying safe, which is the property that matters.

## Phase B — not done

* **The Hole Complete modal's goal strip.** The data path is complete and `LastMissionResult`
  is exposed on the controller, but the widget does not yet DRAW the ticks and crosses — that is
  prefab work on the mission card family, which is what Phase C builds. Called out rather than
  quietly folded into "done".
* **The position-trace assertion per start kind on Hole 01** (§B acceptance). It needs a play-mode
  round, and there is no screen to start a mission from until Phase C.

## Phase B open questions

4. **Mission 37 needs a decision** — re-site `l_sand_up_down` to a hole with a greenside bunker,
   or author one on 13. It blocks the `missions` publish either way.

---

# Phase C — the Mission Selection screen

## What it is

`MissionSelectionScreen` is a clone of `HoleSelectionScreen`, and `MissionCard.prefab` a clone of
`HoleCard.prefab` (guid `6717663c8484640909c58d78cd02f8c2`) — §1's reuse mandate taken literally.
The screen shows, top to bottom: a course-progress line (`Lomond Country Club 0/40`), a tier strip
with per-tier counts and locks, the **daily mission** card, and the campaign list with the first
unlocked mission expanded and everything after it collapsed and locked.

`ScreenId.MissionSelection` was added to `ScreenManager`, the persistent bars show on it
(`MISSIONS_TITLE`, MainPlay nav slot), and both `ModeSelectScreenController` and
`ModeCarouselController` route the Missions card at `mission_select` — the two of them are the
only entry points a player has, and the carousel keeps three copies of every card, so the wiring
was proven by invoking the real `ModeCardController.playButton.onClick`, not by calling
`ShowScreen` directly.

## The daily card

`GET /api/v1/missions/daily` returns the day's generated recipe; `MissionCatalog.BuildFromRecipe`
resolves it against the seven catalogs into the same `MissionDefinition` shape a campaign row
produces, so one card controller draws both. The card shows the hole, the live reset countdown,
and the reward the server actually decided — today's draw included the `DOUBLE_RP` modifier, which
is why the capture reads **x60** against a campaign card's x15.

## Cesar's three rounds of feedback, and what each one actually was

1. **"mission title goes outside the panel. Goals are not shown, neither map."** Three separate
   causes, not one: `CourseLine` had been cloned into `SubtitleRowExp`, which is a *horizontal*
   group; `Tutorial` is also horizontal, so the map and five text lines laid out ~3250px across an
   830px row; and the map stayed a sliver even after that because the group's `childControlWidth`
   was `False`, so the `LayoutElement` was ignored outright (trap C4).
2. **"Daily mission card does not have appropiate gap with the bottom of the pannel."** The daily
   card has four content rows where a campaign card has three (it adds the reset countdown), so its
   `CollapsedContainer` measured 374px inside a 340px card and overflowed its own 24px bottom
   padding. Fixed by sizing the card to exactly what the container measures. The lever is
   `sizeDelta`, **not** `LayoutElement` — `Content`'s VerticalLayoutGroup has
   `childControlHeight = false`, so preferredHeight is inert (trap C3/C4 again). `CardsContainer`
   gives back the same 34px, so its bottom edge stays at worldY 344 — byte-identical to
   `HoleSelectionScreen`'s, which is the design reference.
3. **"make outline for Daily mission golden."** The first attempt overlaid the card's own
   `Background - Next Hole` sprite tinted `#EEDC9A` with `fillCenter=false`. That does not work and
   the frame proves why: the sprite's 48px 9-slice border is *solid navy art*, so the ring painted
   gold-tinted navy over navy — a muddy band, not an outline. A red-tint probe confirmed the ring
   was rendering and covering x=48..96 with dark art. The fix is a real stroke-on-transparency atom:
   `Assets/Art/Gacha/S_GachaCardBorder3.png` (48×48, 3px white stroke, transparent centre, already
   9-sliced at 23px), tinted `#EEDC9A`, `pixelsPerUnitMultiplier = 0.5` so its corner radius matches
   the card's. Measured on the shipped frame: `(238,220,154)` at the card edge — exactly `#EEDC9A`.

   `pixelsPerUnitMultiplier` was swept rather than guessed. Blue peek-through of the card's own
   corner arc, counted in a 32×34px box at the top-left corner: ppu 0.7 → 88px, 0.6 → 85, 0.55 → 75,
   **0.5 → 25**, 0.45 → 0. 0.45 rendered a visibly stepped arc at 4× zoom; 0.5 is the knee.

## Gates

  Unity EditMode   2035 tests / 2032 passed / 0 failed / 3 pre-existing skips
  Scene guardrail  ShellScene vs HEAD: 0 fileIDs lost, 0 active-state flips, 5 added
                   (the GoldOutline GameObject and its three components)

## Phase C deviations

12. **The start marker is not drawn on the card thumbnail.** There is no world→thumbnail
    calibration in the project — `MapViewController` is a live 3D camera, not a projected still —
    so a marker can only be placed by eye. It rendered as a white box in the wrong place and was
    removed; the start area is conveyed in words instead (`START_AREA_*`). Restoring it needs a
    calibration decision, not more code.

## Phase C — not done

* **The Figma fidelity table (Rule 18) and the UI fidelity lint (Rule 21)** against nodes
  `4065:7960` / `4065:7961`.
* **EN + JA screenshots.** Only EN has been captured.
* **The Hole Complete modal's goal strip** — still carried from Phase B.
* **§21's live E2E** — play a mission end to end, clear it, watch the claim settle.
* **Offline daily generation (§C2)** — deliberately not attempted. It would mean a second C#
  implementation of the deterministic draw, and two implementations of one seeded algorithm drift
  silently. The card shows a retry state instead when `/daily` is unreachable.
