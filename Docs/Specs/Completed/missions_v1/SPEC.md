# SPEC — `missions_v1`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work
> definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
>
> Filed 2026-08-28 (Architect via Cowork). Design of record: `Docs/Game Design/MISSIONS_REDESIGN.md`
> + `Docs/Game Design/GOLFIN_Missions_Redesign.xlsx` (copies in `reference/`). Read the design
> once for intent; THIS file is the work definition. Where they disagree, this file wins and the
> disagreement is a spec bug — flag it, don't pick.
>
> Cesar's decisions of record (2026-08-28): loadout is **supplied OR own-bag-with-mask, per
> mission**; launch campaign **40 missions / 4 tiers** (the workbook `Missions` sheet, verbatim);
> Daily Mission **generated server-side from components**; **no Home-screen daily surface in v1**
> (badge deferred, needs design); Missions mode enters through its existing mode card on Home +
> Mode Select. Standing invariant: a client missing information never shows a broken card and
> never wrongly spends or earns RP.

## Status

See `STATUS.md`. `SPEC_READY`.

## Goal

Ship the Missions mode: 40 data-driven missions composed from components (hole × start area ×
wind preset × loadout × 1–3 goals), a server-generated Daily Mission, admin control over all of
it through the content-catalog machinery, server-authoritative rewards, and a Mission Selection
screen cloned from Hole Selection — entered from the Missions mode card that already exists in
`modes.csv` (`locked=true`, `target=none` today).

## Reference

- **Figma:** file `5gEAHjl6xAtW8iYY7NMvWd` — Missions Screen `4065:7960` (NEXT MISSION state),
  `4065:7961` (REPLAY state). Renders in `reference/MissionsScreen_NextMission_4065-7960.png`,
  `reference/MissionsScreen_Replay_4065-7961.png`.
- **Placeholder vs canonical in the mockup:** course tabs `YAITA - RINDOU` / `YAITA - KIKYOU`
  are placeholder (only `lomond-country-club` exists); `25/25` tier counts are placeholder (data
  is 10/tier); every card title `Risk and Reward - Hole 5` is placeholder; the three goal bullets
  with a duplicated line are filler (duplicate goal types are INVALID — validator rejects them);
  `Lomond Country Club - Hole 5` is the REAL course name; reward strip icons (RP coin, wrench =
  repair kit, ball) are canonical.
- **Data:** `reference/GOLFIN_Missions_Redesign.xlsx` sheets `Missions`, `Tiers`, `StartAreas`,
  `WindPresets`, `Loadouts`, `Goals`, `GoalWeights`, `DailyWeights`, `DailyRewards`,
  `AdminCatalogs`, `Economy`; `reference/missions.csv` = the `Missions` sheet as CSV.

## Figma Fidelity (Rule 18) — Mission Selection screen only

The screen is the Hole Selection prefab family re-bound; every element below maps to an existing
Hole Selection element unless marked NEW.

| Element | Figma node | Property → value |
|---|---|---|
| Top UI (RP pill, title `MISSIONS`, settings) | `4002:6036` | as Hole Selection; title text key `MISSIONS_TITLE` |
| Rankings button | `4003:4576` | 75×75 at content-area top-right (x 1047, y 262); v1 opens the existing "coming soon" modal |
| Filter row 1 — course tabs | `4003:4412` | 1074×56; `{COURSE} {cleared}/{total}` per course; padlock vector `4003:4527` when no active missions; one course at launch |
| Filter row 2 — tier tabs | `4003:4508` | 1074×56; `BEGINNER n/10 · AMATEUR n/10 · 🔒 PRO 0/10 · 🔒 LEGEND 0/10`; padlock until the tier gate opens; default = furthest unlocked tier; persists across nav round-trips |
| Card column | `4002:6161` | 978-wide `Mission Card Container` ×N, 24 px gap, scrollbar `4003:7316` at x 1090 |
| Collapsed card | `4003:5297` | 978×284: header pill (`REPLAY MISSION` / `NEXT MISSION` gold / 🔒 `LOCKED` grey), title `{order} - {name} - Hole {holeId}`, reward strip |
| Expanded card | `4003:5010` | 978×844.5: course line `Lomond Country Club - Hole N`, hole map thumbnail (Hole Selection image for `holeId`, **start marker at the mission's start area — NEW**), 1–3 goal bullets + **wind line + loadout line (NEW, same bullet style)**, reward strip, button |
| Reward strip | in card | icon ×qty: RP · RepairKit/PremiumRepairKit · Ball · **Ticket (NEW icon — reuse `tickets.csv` art)**; uncleared shows first-clear + items, cleared shows replay RP only, locked greyed |
| Button | in card | `PLAY` gold on NEXT (Hole Selection PLAY), `REPLAY` silver on cleared (Hole Selection REPLAY), none on LOCKED |
| Daily card (NEW, no Figma — Cesar-approved interim) | — | `Mission Card Container` instance pinned ABOVE filter row 2 with header `DAILY MISSION · HH:MM:SS` (UTC countdown) and streak chip `🔥 n`; button `PLAY` → `CLEARED ✓` after clear; distinct header tint (use the tournaments gold band colour) — request a frame from Nishikawa; do not invent further chrome |

## Architecture context

**Unity (existing, reuse — do not rebuild):**
- `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab`, `Assets/Scripts/UI/HoleSelection/{HoleSelectionScreenController, HoleCardController, HoleProgressionService, HoleProgressionStoreAdapter}.cs` — clone-and-bind source for the screen.
- `Assets/Scripts/UI/HoleData.cs` (`RewardType {Points, RepairKit, Ball}`, `rewards`, `replayRewards`, `tees`), `HoleDatabaseLoader.cs` (`GetHole(index)`), `Assets/Data/HoleDatabase.csv` (`windSpeedMph`, `windDirectionDegrees`, `courseId`), `Assets/Data/HoleTees.csv`.
- `Assets/Scripts/UI/ModeSelect/{ModeSelectScreenController, ModeCarouselController, ModeCardController, ModesDatabaseCSV}.cs` — target dispatch (`ModeSelectScreenController.TargetHoleSelect…`, `CanDispatch`), the withhold rule, entry-fee spend (`ModeCardController.cs:622` `PointsSpendGate.Spend(_data.entryFee, SpendReasons.ModeEntryFeeFor(_data.id), …)`).
- `ScreenManager` `enum ScreenId` (`HoleSelection`, `ModeSelection`, …).
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — `OnHoleLoaded` tee scan (`TeeMarker_regular_*` → `_RuntimeTeeAnchor` / `_ballSpawnPoint`, ~2160–2240), wind population from `HoleDatabaseLoader.GetHole` into `WindContext` + `WindCfg` (~2336–2357), pin from `GreenTopology`.
- `Assets/Scripts/Course/Runtime/{GreenTopology, GreenTopologyCache, TeeData, HoleTeesCsvParser}.cs` — `GetPinCandidates()`, `GetPinLabels()`, `GetDefaultPin()`; `TeeSet` enum.
- `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — `StrokeCapEnabled`, `StrokeCapOverPar` (documented Missions opt-in, cleared in `ResetSession()`), `IsVersus`; `HoleCompletionBridge.cs` FAILED path.
- `Assets/Scripts/Gameplay/Loop/ShotResult.cs` — `TerminalState (AtRest|InCup|OB)`, `OBReason`, `StartSurface`, `EndSurface` — the per-shot facts goals are evaluated from.
- `Assets/Scripts/BagManager.cs` (`EquippedBagSlot`, `GetClubsInBag`, `EquipBag`, `OnEquippedBagChanged`), `ClubManager` (`PlayerClubData.equippedBagSlot` is the source of truth), `Assets/Resources/Data/Clubs.csv` (`type`, `rarity` columns via `ClubCsvParser`).
- `Assets/Scripts/Core/Stamina/StaminaModel.cs` + `Docs/Design/stamina_economy.csv` (`drain_per_hole` = 8, flat at hole completion).
- `Assets/Scripts/ContentRuntime/` (`ContentCatalogStore.Catalog(name)`, overlay pattern from `content_overlay_catalogs`), `Assets/Scripts/EconomyRuntime/PointsSpendGate.cs`, `InventorySync` grants drain, `Assets/Scripts/Save/SaveData.cs` (`unlockedHoles`, `playedHoles`, `claimed`).
- `Assets/Scripts/Telemetry/` — mission events join the beta telemetry stream.

**Backend (`/Users/cesar/Documents/playlife/backend`):** `routers/points.py` (`POST /earn-game` — catalog-resolved amounts, `pts NULL` = client amount ≤ `max_per_event`; `POST /spend` with the `mode_entry_fee:<id>` validation), `routers/content.py` (catalog publish; `golfin_mode_fees` mirror-in-transaction pattern from `game_modes_admin`), `migrations/`, `main.py` mounts.

**Content tooling:** `Tools/content/catalogs.py` `CATALOGS` (nine entries today, `modes` is #8, `level_up_costs` #9 — number the new ones after whatever is there), `seed_from_csv.py`, `--check`.

**Admin (`Tools/admin-dashboard`):** `lib/registry.ts`, `lib/contentValidate.ts` (`REQUIRED_COLUMNS`, `NUMERIC`, `ID_COLUMN`), `lib/i18n.ts` `DICT` (EN + JA, every string), `lib/audit.ts` `writeAudit()`, existing Rewards panel (`app/(panels)/rewards`) over `game_point_actions`, Modes panel. Deployment proof per `Docs/PIPELINE_HARDENING.md` §23.

## Implementation — five phases, in this order, each with its own evidence

### Phase A — Data + content catalogs + server truth (backend, tooling, admin)

A1. **CSVs** (bundled, `Assets/Resources/Data/`): `missions.csv` (columns exactly as
`reference/missions.csv`, plus `courseId=lomond-country-club`, `pinIndex=0`, `staminaDrain`
(8 for tee starts, 3 for short starts)), `mission_start_areas.csv` (`id, courseId, holeId, kind,
x, y, z, bake_hash` — tee kinds carry NO coordinates, they resolve to scene markers; short kinds
are baked in Phase B), `mission_wind_presets.csv`, `mission_loadouts.csv`, `mission_goal_weights.csv`,
`mission_tiers.csv`, `daily_mission_weights.csv` — each verbatim from the workbook sheet of the
same name. Localization: `mission.<key>.name` EN + JA from the sheet; goal/wind/loadout lines
are TEMPLATED (`goal.SCORE.0 = "Score par or better"` etc.) — no per-mission description keys.

A2. **Catalogs**: add all seven to `CATALOGS`; seed migration `2026_08_29_content_missions_seed.sql`
generated by `seed_from_csv.py --catalogs missions,mission_start_areas,mission_wind_presets,mission_loadouts,mission_goal_weights,mission_tiers,daily_mission_weights`;
export round-trip byte-identical; `--check` clean.

A3. **Server tables** (one migration, RLS on / no policies, verification block, FULL SQL IN CHAT
for Cesar): `golfin_mission_rewards(mission_id pk, tier, first_clear_rp int, replay_rp int,
is_active bool, updated_at)` + `golfin_mission_tier_bonus(tier pk, bonus_rp)` — **written by the
`missions`/`mission_tiers` publish in the same transaction** (publish fails if the mirror write
fails, `golfin_mode_fees` pattern); `mission_progress(user_id, mission_id, clears int,
best_strokes int, first_cleared_at, pk(user_id, mission_id))`; `daily_missions(date pk, recipe
jsonb, pinned bool, generated_at)`; `daily_mission_claims(user_id, date, streak int, rp int,
pk(user_id,date))`. `game_point_actions` rows: `mission_clear (pts NULL, max_per_event 60)`,
`mission_replay (5, daily_cap 50)`, `mission_tier_clear (pts NULL, max 300)`, `daily_mission (30,
once_per_user per UTC day — implement as daily_cap 30 + the claims table)`, `daily_streak (pts
NULL, max 30)`. Router label map gains the five (EN/JA).

A4. **Endpoints** (`routers/missions.py`, mounted `/api/v1/missions`, AUTH):
- `GET /catalog-state` → `{missions: [{mission_id, clears, best_strokes}], tiers_unlocked: [...]}`.
- `POST /claim {mission_id, strokes, goals_met: bool, idempotency_key}` → reads
  `golfin_mission_rewards` (inactive → 200 `{"status":"inactive"}`), decides first-clear vs replay
  from `mission_progress`, credits via the same `earn_pts_v2` path `/earn-game` uses with reason
  `mission_clear:<id>` / `mission_replay:<id>`, upserts progress, pays `mission_tier_clear` when the
  tier's 10th clear lands, returns `{status, awarded, first_clear, tier_bonus}`. `goals_met=false`
  records the attempt (nothing paid).
- `GET /daily` → today's UTC recipe: read `daily_missions[date]`, else generate (A5) and insert.
  Response includes `date`, `recipe`, `recipe_hash`, `claimed` for the caller, `streak`.
- `POST /daily/claim {date, recipe_hash, strokes, idempotency_key}` → refuses a `recipe_hash`
  that does not match the stored row (`{"status":"recipe_mismatch"}`), once per user per date,
  pays `daily_mission` + streak bonus (`daily_streak`: +15 at streak 3, +30 at streak 7 then the
  streak wraps to 0; streak = consecutive UTC dates), returns `{status, awarded, streak}`.
- Tests fake-Supabase style (`test_missions.py`): first clear / replay / inactive / tier bonus
  at 10 / daily generate-cache / mismatch refused / once-per-day / streak 3 and 7 / idempotent
  replay of every POST.

A5. **Daily generator** (`services/daily_mission.py`, pure function of `(date, weights, tables,
history)`): seed = `sha256("golfin-daily:" + date)`; band Amateur (6–9) Mon–Thu, Pro (10–13)
Fri–Sun; draw start kind → loadout (SUPPLIED only, allowed for that start kind) → hole → wind →
primary goal → secondary goal → modifier (`NONE | LOW_STAMINA_START | ALT_PIN | DOUBLE_RP`) by
`daily_mission_weights`; score with `mission_goal_weights`; reroll up to 20× until in band, then
drop the secondary goal; freshness: no hole within 5 days, no primary goal within 2, no loadout
within 2, DOUBLE_RP at most once per 7 days; ALT_PIN only if the hole has ≥ 2 pin candidates
(a `pin_count` column on `mission_start_areas` rows of kind GREEN, baked in Phase B — until it
exists ALT_PIN weight is treated as 0). `recipe_hash = sha256(canonical json)`. Deterministic:
same inputs → same recipe, asserted in tests.

A6. **Admin**: **Missions panel** (`app/(panels)/missions`) — table + row editor whose hole /
start / wind / loadout / goal-type fields are dropdowns fed from the component catalogs;
publish validation in `contentValidate.ts`: start↔loadout compatibility, typed goal params, **no
duplicate goal types on a row**, `difficultyScore` RECOMPUTED from `mission_goal_weights` (stored
value is display), tier band, campaign order non-decreasing (warn), `firstClearRP ≤
mission_clear.max_per_event` (block), supplied loadouts resolve every club type+rarity to ≥ 1
`clubs` row (block). **Components panel** (`app/(panels)/mission-components`) — one tab per
component catalog; the goal-weights tab shows the re-scored mission table before publish.
**Daily panel** (`app/(panels)/daily-missions`) — calendar of `daily_missions`, *Preview next 14
days* (runs A5 without inserting), *Pin recipe* (validates like a mission row; past dates
refused; audited), clear-rate per date from `daily_mission_claims`. **Users drawer → Missions
tab** — `mission_progress` + claims, *Reset mission* through `writeAudit()`. Rewards panel: the
five new actions appear automatically; add drift warnings `missions.firstClearRP >
mission_clear.max_per_event` and `daily_mission.pts ≠ DailyRewards base`. EN + JA for every
string. `npm run deploy` + Cloudflare deployment id quoted (§23).

A7. **Every new string and every edit to an existing catalog CSV goes through the two-way
importer, or the admin flow breaks** (`Tools/content/README.md` §Importing, `content_two_way`).
Concretely:
- New localization keys (`MISSIONS_TITLE`, `mission.<key>.name` ×40, the `goal.*` / `wind.*` /
  `loadout.*` templates, card pills `MISSION_NEXT` / `MISSION_REPLAY` / `MISSION_LOCKED` /
  `MISSION_DAILY`, tier/course tab labels, warnings) are added to
  `Assets/Localization/LocalizationText.csv` with EN **and** JA in the same commit — then
  `python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN), read the
  verdicts, `--apply`, publish `texts` from the admin, and `export_content.py --check` must come
  back clean. Never hand-insert `content_rows`; never add a key only in code or only in a
  migration. If the plan reports CONFLICTS (someone mid-edit in the admin), stop and say so —
  do not `--overwrite-dirty` on your own.
- The `modes.csv` row edit (`target=mission_select`, `rewardsTextKey=MODE_REWARDS_VARY`) is the
  same path: edit the CSV → `import_content.py --catalogs modes` → publish. `min_build` for
  CHANGED rows is untouched (immutable once published).
- The seven NEW catalogs are seeded by the A2 migration (rows + drafts at version 1, day-one
  parity by construction) — the importer is for later edits, not the first load.
- Dashboard UI strings are a different system: `lib/i18n.ts` `DICT` entries with both `en` and
  `ja` (a missing key is a type error) — see `Docs/ADMIN_DASHBOARD_OPS.md` §3.4. Do not put
  player-facing strings there or dashboard strings in `LocalizationText.csv`.
- Acceptance adds: `export_content.py --check` clean for `texts` and `modes` after the publish;
  `LocalizationTextTable.asset` regenerates on build (the build hook) — no manual step.

### Phase B — Start areas bake + gameplay hooks (Unity, editor)

B1. **Bake tool** (`Assets/Scripts/Editor/CourseImporter/MissionStartAreaBaker.cs`, menu
`Golfin/Missions/Bake Start Areas`): for each `Hole_NN_Geo` scene derive from `SurfaceMarker` GOs
(same scan `PhysicsLabController.OnHoleLoaded` does): `GREEN` = green centroid offset 9 m toward
the tee side; `FRINGE` = 2 m outside the green contour on the tee side; `FAIRWAY` = the fairway
marker nearest to 110 m from the default pin; `ROUGH` = rough/semi-rough point 8 m lateral from
FAIRWAY; `SAND` = bunker marker nearest the green (skip kind if the hole has none — the mission
validator must then refuse SAND on that hole); plus `pin_count` from `GreenTopology.GetPinCandidates()`.
Writes `mission_start_areas.csv` rows with `bake_hash`; the `Validate All Holes` drift gate
(`hole02_tree_bake_drift`) gains a start-area check. Sample terrain height for `y`.

B2. **Session seeding** (`Assets/Scripts/Gameplay/Missions/MissionSession.cs`, new asmdef
`Golfin.Gameplay.Missions` referencing Loop/Session, Course.Runtime, Physics.Stats as needed):
`MissionSession.Begin(MissionDefinition)` sets — spawn (`_ballSpawnPoint` override: tee kinds →
that `TeeMarker_<label>_*` midpoint; short kinds → baked coords), pin (`pinIndex` →
`GreenTopology.GetPinCandidates()[i]`, `HoleContext.PinWorld`), wind (`WindContext.SpeedMph` /
`DirectionDegrees` from the preset: absolute = bearing(spawn → pin) + relDir; GUSTY re-rolls
speed in [6,18] on every `OnShotComplete`), stroke cap (`GameSession.StrokeCapEnabled = true`,
`StrokeCapOverPar` = the tightest of SCORE/SHOTS goals), stamina (`staminaDrain` overrides
`drain_per_hole` for this session — add the override seam to `StaminaModel`, default = config),
loadout (B3). `MissionSession.End()` restores everything; `GameSession.ResetSession()` also
clears it. Practice / 1v1 / tournaments never enter `MissionSession` — assert in tests.

B3. **Loadout override**: `BagManager.PushSessionBag(IReadOnlyList<string> clubIds)` /
`PopSessionBag()` — a transient bag that `GetClubsInBag(EquippedBagSlot)` and the in-game
club selector read while pushed; never written to `PlayerClubData` / SaveData; popped on
`MissionSession.End()` and on any session reset. `supplied:` resolves each `Type+Rarity` to the
first `Clubs.csv` row with that type and rarity (deterministic order = CSV order); `own:` =
equipped bag minus `ban:` types. Durability: supplied clubs never wear (they are not owned).

B4. **Goal evaluation** (`MissionGoalEvaluator`): subscribes to the shot stream (`ShotResult`)
and hole completion; evaluates all goals at hole-out (as the old design) with early FAIL for
SCORE/SHOTS via the stroke cap, NO_HAZARD on the first OB/water, AVOID on the first landing on
the surface, AVOID_CLUB on the first banned club use. Result → `MissionResult {goals[], strokes,
cleared}` → `POST /missions/claim` (or `/daily/claim`) through the existing points client with an
idempotency key `mission:<id>:<session guid>`; local progress mirror in SaveData
(`missionProgress` list) updated from the server response only. Hole Complete modal shows goal
ticks/crosses + the awarded amount (reuse the modal; add a goals block).

### Phase C — Mode card wiring + Mission Selection screen (Unity, UI)

C1. `ScreenManager.ScreenId.MissionSelection`; `ModeSelectScreenController.TargetMissionSelect =
"mission_select"` in the dispatch set (`CanDispatch` admits it); `case` added in BOTH
`ModeSelectScreenController` and `ModeCarouselController` switches, and the carousel switch
re-pointed at the constants instead of string literals. `modes.csv` row `missions`: `target=
mission_select`, `rewardsTextKey=MODE_REWARDS_VARY`, `locked` stays `true` in the bundled CSV
(Cesar flips it from the admin). Entry fee stays on the card path unchanged.

C2. **Screen**: `Assets/Prefabs/UI/MissionSelection/MissionSelectionScreen.prefab` + `MissionCard.prefab`
cloned from Hole Selection (`HoleCard.prefab`), controllers `MissionSelectionScreenController` /
`MissionCardController` / `MissionProgressionService` mirroring the Hole Selection trio; bindings
per the Figma Fidelity table. Card order: cleared (desc) … NEXT (expanded by default, scrolled to
top) … locked. Tier gate: tier N+1 unlocks when ≥ 8 of tier N are cleared; within a tier
`unlock=clear:<prev>`. Course/tier tab counts from the catalog. Start marker on the map thumbnail:
project the start area's XZ into the `HoleImages/` thumbnail using the same transform the
MapView uses. Daily card per the table (state from `GET /daily`; countdown to UTC midnight;
offline → deterministic local generation from bundled tables with the same seed, `PLAY`
enabled, claim goes to the server when back online — never local-pay). Back → whichever screen
opened it.

C3. **Warnings**: an `own:` mission whose ban mask empties the bag, or a supplied loadout that
cannot resolve a club, renders the card with the Hole Selection "missing equipment" warning
style and PLAY disabled (invariant: never a dead card).

### Phase D — Telemetry + docs

Events: `mission_start {id|daily, recipe_hash}`, `mission_end {cleared, strokes, goals_met[],
awarded}`, `daily_claim {streak}`. `Docs/AI_CONTEXT.md`, content runbook (catalog list), admin
README (panels), `Docs/Economy/ECONOMY_MASTER.md` §3 marked "live" once Cesar flips the mode.

### Sequencing / gates

A (backend + catalogs + admin, deployable on its own, mode still locked) → B → C → D. The §21
live E2E runs at the end of C: on a device build against prod, clear mission 1 (first-clear 15
RP ledger row `mission_clear:1`), replay it (`mission_replay:1`, 5 RP), clear the daily (`daily_mission`
30 RP, streak 1), and pin tomorrow's recipe from the live admin and see it on the client after
UTC midnight (or with the device clock check the admin allows). Quote the deployment ids (§23).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Seven catalogs round-trip: seed → export byte-identical → `--check` clean; `Tools/content` tests green.
- [ ] Every new player-facing string is in `LocalizationText.csv` (EN + JA) and reached the `texts` catalog via `import_content.py` → publish; `--check` clean for `texts` and `modes`; zero hardcoded `.text` literals added (grep in the report).
- [ ] Migration applied (verification block output quoted); `golfin_mission_rewards` rows = 40 after the first `missions` publish; publish with a failing mirror write rolls back (test).
- [ ] `POST /missions/claim`: first clear pays `firstClearRP`, second pays `replayRP`, inactive pays nothing, 10th clear in a tier adds the tier bonus once; every POST idempotent (tests + one live ledger read).
- [ ] Daily: same date → same recipe (test); `recipe_mismatch` refused; second claim same date refused; streak 3 / 7 bonuses; pinned recipe wins over generated.
- [ ] Admin: Missions, Components, Daily panels + Users Missions tab live at admin.golfin.world — **Cloudflare deployment id quoted** (§23); validator blocks duplicate goal types, incompatible start↔loadout, unresolvable supplied clubs, RP over cap; warns on band/order drift; EN + JA complete.
- [ ] Bake: `mission_start_areas.csv` has GREEN/FRINGE/FAIRWAY/ROUGH for all 18 holes, SAND where a bunker exists, `pin_count` filled; `Validate All Holes` fails on a hand-edited coordinate (tripwire, §20).
- [ ] Gameplay: start at the mission's area (position-trace assertion per start kind on Hole 01), pin index honoured, wind = preset (WindContext values logged), GUSTY changes speed between shots, stroke cap ends the hole FAILED at the right stroke, stamina drain = `staminaDrain`.
- [ ] Loadout: supplied bag shows only the listed clubs in the selector, never persists (SaveData diff empty after the round), pops on session end and on `ResetSession`; `own:` mask removes the banned types; Practice/1v1/tournaments unaffected (tests).
- [ ] Goals: one EditMode test per goal type (pass and fail cases) driven by synthetic `ShotResult`s.
- [ ] Mode card: PLAY on the Missions card from Home carousel AND Mode Select opens `MissionSelection`; a bundled `target=none` row is withheld; flipping `locked=false` from the admin shows the live card next launch without a build (live E2E).
- [ ] Screen: Figma Fidelity table reproduced PASS/FAIL against both renders; tier tabs count from data (10/tier); NEXT expanded by default; 8-of-10 gate; daily card countdown + `CLEARED ✓`; offline daily renders and does not pay locally.
- [ ] §21 live E2E as described in Sequencing, ledger rows quoted.
- [ ] Full EditMode sweep green; backend suite green; dashboard build + deploy green; no Console errors; STATUS/IMPLEMENTER_REPORT/AI_CONTEXT updated.

## Files / hierarchy this task touches

Unity: `Assets/Resources/Data/{missions,mission_start_areas,mission_wind_presets,mission_loadouts,mission_goal_weights,mission_tiers,daily_mission_weights}.csv`, `Assets/Resources/Data/modes.csv` (row `missions`), `Assets/Localization/LocalizationText.csv`, `Assets/Scripts/Gameplay/Missions/*` (new asmdef), `Assets/Scripts/BagManager.cs`, `Assets/Scripts/Core/Stamina/StaminaModel.cs` (override seam), `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (spawn/pin/wind override entry points only), `Assets/Scripts/UI/ModeSelect/{ModeSelectScreenController,ModeCarouselController}.cs`, `Assets/Scripts/UI/ScreenManager.cs`, `Assets/Scripts/UI/MissionSelection/*` + `Assets/Prefabs/UI/MissionSelection/*` (new), `Assets/Scripts/Editor/CourseImporter/MissionStartAreaBaker.cs` (new), `Assets/Scripts/Save/SaveData.cs` (`missionProgress`), `Assets/Scripts/ContentRuntime/*` (seven loaders on the overlay pattern), telemetry events.
Backend: `migrations/2026_08_29_missions.sql`, `migrations/2026_08_29_content_missions_seed.sql`, `routers/missions.py`, `services/daily_mission.py`, `main.py`, `tests/test_missions.py`.
Tooling/admin: `Tools/content/catalogs.py`, `Tools/admin-dashboard/{lib/registry.ts, lib/contentValidate.ts, lib/i18n.ts, app/(panels)/missions, app/(panels)/mission-components, app/(panels)/daily-missions, app/(panels)/users (drawer tab), app/api/missions/*}`.

## Smoke evidence

Per phase: A — backend tests + admin deploy id + a live publish that writes the mirror; B — bake CSV diff + position-trace test + a device/editor round on Hole 01 from each start kind (screenshots in `screenshots/`); C — EN/JA screenshots of the screen in NEXT / REPLAY / LOCKED / daily states vs the two renders, plus the §21 live E2E ledger rows. Visual fidelity for the start marker and card states needs the human play-and-confirm note (Lesson O).

## Out of scope (do NOT do these)

- Home-screen daily badge / any Home surface for the daily (deferred — needs design).
- Mission leaderboards (Rankings button stays "coming soon").
- New pin authoring, new courses, `HoleTees.csv` yardage fix (flagged separately).
- Streak-shield items, daily leaderboard by strokes, sponsor daily overrides beyond *Pin recipe*.
- Flipping `missions.locked` in the bundled CSV — that is Cesar's admin publish.
- Durability wear, ball consumption, gacha pool (unchanged dormant sinks).
