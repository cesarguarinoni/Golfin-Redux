# MISSIONS REDESIGN — component-built missions + Daily Mission

**2026-08-28 · Architect · companion workbook: `GOLFIN_Missions_Redesign.xlsx` · Figma: Missions Screen `4065:7960` / `4065:7961` (file `5gEAHjl6xAtW8iYY7NMvWd`)** (Missions, Tiers, StartAreas, WindPresets, Loadouts, Goals, GoalWeights, DailyWeights, DailyRewards, OldSheetAudit). Status: DESIGN, **verified against the repo 2026-08-28 (§8)** — not a spec yet. Decisions of record from Cesar today: loadout is **supplied OR restricted, declared per mission**; launch campaign **≈40 missions / 4 tiers**; Daily Mission **generated from components**, with enough components to stay fresh.

## 1. What was wrong with the old sheet (RC Mission Master Table, 100 missions)

| Finding | Evidence |
|---|---|
| It was 5 tutorials + **11 templates × 9 holes**. The player played the same idea nine times before anything changed. | #6–14 "Bogey - Ladies - Hole 1..9", #15–23 the same from the Tourney tee, #24–32 "No S.Rough" ×9, … |
| Difficulty went **backwards between tiers** | Beginner #15–32 play from the Tourney (hardest) tee; Amateur #33–50 drop back to Regular |
| Wind was random per mission, in absolute degrees | #13 speed 0.02 → #14 speed 0.93; a "45°" wind is a headwind on one hole and a tailwind on the next |
| Mission 1 asked for a 150-yd shot before any chipping or putting | "The First 150" precedes "Land on the fairway" |
| Only 5 of 10 goal types used; goal slot 3 never used | Near Pin / Shots / Carry never appear; Goal3Type = 0 on all rows |
| Start areas existed in the data but were never used | 'Starting Coordinates' has Green / Sub-Green / Fairway / Rough / Sand Trap per hole; every mission starts on a tee |
| Replay reward 0 → no reason to replay | ReQuantity = 0 everywhere (was 5 on 2025-04-24) |
| Copy ≠ rule | #6–14 say "Double Bogey or better", parameter is Bogey |
| Reward scale is pre-÷10 | 100/200/300/400 RP per tier vs live hole-complete 10, ~300 RP/day reference |
| Legend skipped par-3 holes instead of changing the goal | holes 4 and 8 missing from #87–100 |

## 2. The new model in one line

**Mission = Hole × StartArea × WindPreset × Loadout × Goals(1–3)**, every component carries a difficulty weight, **mission difficulty = sum**, tier = score band, campaign order = ascending score. No template×9 grids: each of the 40 missions is a different idea, holes rotate underneath.

### 2.1 Components (full tables in the workbook)

- **Hole** (18, **Lomond Country Club** — the only course in the build; the old sheet's Yaita is gone): base weight par3 0 / par4 1 / par5 2. Pars verified against `Assets/Data/HoleDatabase.csv` and `HoleMetadata.par` in every `Generated/Hole_NN_Geo.unity`: P3 = 4, 6, 11, 15 · P5 = 1, 8, 13, 18 · the rest P4. ⚠️ `Assets/Data/HoleTees.csv` yardages disagree with those pars on holes 3, 4, 5, 6, 8, 9, 11, 12, 13, 14 (e.g. hole 8 par 5 at 198 yd, hole 11 par 3 at 463 yd) — display-only today, but fix before the card shows yards.
- **StartArea** (9): GREEN 0 · FRINGE 1 · FAIRWAY 1 · ROUGH 2 · SAND 2 · TEE_LADIES 1 · TEE_FRONT 2 · TEE_REGULAR 3 · TEE_BACK 5. The build has **four** tee sets, not the old five: the importer places `TeeMarker_{back|regular|front|ladies}_{L,R}` props per hole (`HoleGeoImporter.cs:1138–1192`), and gameplay today always spawns at the **regular** midpoint (`PhysicsLabController.OnHoleLoaded` → `_RuntimeTeeAnchor` / `_ballSpawnPoint`). A tee start = pick a different marker label; a **short start needs authored world positions that do not exist yet** — derive them at bake time from the scene's `SurfaceMarker` GOs (bunker nearest the green → SAND, green centroid + offset → GREEN/FRINGE, fairway point ~120 yd from the pin → FAIRWAY, rough beside it → ROUGH) into a tracked `mission_start_areas.csv`, the `standalone_trees.csv` pattern, with the drift gate. The old sheet's coordinates are Yaita and useless here. Short starts are the cheap variety: a 2-shot mission fits a 90-second session and is what makes the daily playable on a train (stamina: see §3 `staminaDrain`).
- **WindPreset** (9): CALM 0 · TAIL_L 0 · CROSS_L 1 · HEAD_L 1 · TAIL_S 1 · CROSS_S 3 · HEAD_S 3 · QUARTER_S 3 · GUSTY 4. Units are settled: the game reads `windSpeedMph` / `windDirectionDegrees` per hole from `HoleDatabase.csv` (defaults 5–11 mph) into `WindContext` and `WindCfg` at hole load (`PhysicsLabController.cs:2336–2357`), so a mission just writes those two values instead. Presets: light 6 mph, strong 12 mph, GUSTY 6–18 re-rolled per shot (needs a per-shot hook — today wind is set once per hole). **Direction is stored relative to the start→pin bearing** (0 = tail, 180 = head) and resolved to absolute degrees from the spawn point and `HoleContext.PinWorld`; same preset = same feel on every hole.
- **Loadout** (13): two kinds in one table.
  - `supplied:` a preset bag by club TYPE + rarity (SUP_FULL Common 0, SUP_FULL_RARE −1, SUP_NO_DRIVER 1, SUP_IRONS 2, SUP_WEDGE_PUTTER 1, SUP_PUTTER 0, SUP_DRIVER_PUTTER 3, SUP_ONE_IRON 4, SUP_WOOD_ONLY 3). Resolved to concrete club ids from `Clubs.csv` at load (brand-agnostic; pick the first row of that type+rarity, or a designated `missionDefault` brand). Deterministic difficulty and no "missing equipment" warnings. ⚠️ **This is new mechanics, not reuse:** tournaments have a `gear_rule='supplied'` enum (`TournamentRestrictions.cs:123`) but `TournamentRulesText.cs:15` records that no gear was ever supplied — the string was display fiction and the server backfilled every tournament to `own`. A per-session bag override in `BagManager` (restore on session end, never persisted) is the piece to build; tournaments get it for free afterwards.
  - `own:` the player's current bag with a ban mask (OWN 0, OWN_NO_WOODS 1, OWN_NO_IRONS 2, OWN_NO_WEDGES 2). This is where gear progression pays off — Legend is mostly own-bag on purpose.
  - Each loadout declares which start kinds it is valid for (putter-only ⇒ green start, driver+putter ⇒ tee start). The build script asserts it; the admin validator should too.
- **Goals** (14 types, up to 3 per mission, all validated at hole-out as before): SCORE(rel. par) · SHOTS(≤N) · PUTTS(≤N) · NO_HAZARD · AVOID(surface) · LAND_TEE(surface) · LAND_ANY(surface) · GIR · DIST(≥yd) · CARRY(≥yd) · NEAR_PIN(≤yd) · USE_CLUB · AVOID_CLUB · UP_DOWN. Weights in `GoalWeights` (bogey 1, par 2, birdie 4, eagle 6, GIR 3, near-pin ≤5 yd 3, …). The old unused types (Near Pin, Shots, Carry) become the short-start goals.

### 2.2 Tiers and the curve

| Tier | Score band | First clear | Replay | Tier-clear bonus | Unlock |
|---|---|---|---|---|---|
| Beginner | 0–5 | 15 RP | 5 | 50 | start |
| Amateur | 6–9 | 25 RP | 5 | 100 | clear 8 of 10 Beginner |
| Pro | 10–13 | 40 RP | 5 | 200 | clear 8 of 10 Amateur |
| Legend | 14+ | 60 RP | 5 | 300 | clear 8 of 10 Pro |

The 40 missions in the workbook are **verified non-decreasing in score across the whole campaign** (build script check), 10 per tier, every one of the 18 holes used 1–3 times, no hole twice in a row. Within a tier the next mission unlocks by clearing the previous one (as before), but the **tier gate is 8 of 10**, so one mission that doesn't suit a player's gear never hard-blocks progression — the single biggest churn point in a linear list.

Arc: Beginner is the tutorial (putt → chip → tee shot → par 3 → 150 yd → bunker → first full holes), all supplied bags. Amateur introduces the player's own bag, regular tees, light wind, short-start recovery missions. Pro is back tees, strong wind, restricted own bags, par required, one single-club mission. Legend is back tees with strong wind, birdies, gusts, an eagle, and a two-club par. Every tier ends in a named "final" carrying a Gold Ticket.

Economy check: campaign first-clears total **1,400 RP + 650 RP tier bonuses = 2,050 RP**, roughly a week of the ~300 RP/day reference earn — meaningful but nowhere near a Legendary character unlock (3,000). Replay 5 RP mirrors `hole_replay` and sits under its daily cap. Items: Repair Kits on early tiers, Premium Repair Kits on Pro/Legend, Gold Tickets only on tier finals (gacha pool is still a mock — tickets are cheap to promise, keep them rare anyway).

## 3. Daily Mission (retention)

- **One global recipe per UTC day**, generated server-side from the component tables with weights in `DailyWeights`, seeded by the date so every player sees the same mission (daily leaderboard by strokes is then fair — the Confluence "Daily Leaderboard" idea, v2).
- **Always a supplied loadout.** A day-one player can play it; a Legend player is on the same gear as everyone else; the difficulty score is honest.
- **Band by weekday:** Amateur band (6–9) Mon–Thu, Pro band (10–13) allowed Fri–Sun. Generator picks components, scores, rerolls up to 20× until the score lands in band, then drops the secondary goal if it still can't.
- **Freshness rules:** no hole repeat within 5 days, no primary-goal repeat within 2 days, no loadout repeat within 2 days. Space is 18 holes × 9 starts × 9 winds × 9 supplied loadouts × 7 primary × 6 secondary goals ≈ 300k recipes before modifiers.
- **Daily-only modifiers** (30 % of days): LOW_STAMINA_START (character begins at 50 % stamina — reuses `StaminaModel` debuff thresholds; NOTE verify a per-round stamina override hook exists), ALT_PIN (alternate pin — **feasible today**: `GreenTopology.GetPinCandidates()` / `GetPinLabels()` hold authored candidates per hole, index 0 = default; a `pinIndex` column selects one — verify every hole has ≥ 2 candidates before enabling), DOUBLE_RP (max one per week).
- **Stamina:** live model is a flat **8 Condition per hole completion** (`Docs/Design/stamina_economy.csv`, not per shot), so a 2-shot chip mission would cost the same as a full hole — add a `staminaDrain` column (short starts 3, full holes 8) and let the session read it instead of the global `drain_per_hole`.
- **Rewards:** 30 RP on first clear (earn action `daily_mission`, `once_per_user` per UTC day, server-validated like every other earn); streak +15 on day 3, +30 and a Gold Ticket on day 7, then wraps. Streak resets on a miss (v1; a "streak shield" item is an obvious later sink). Retries until cleared are free; no reward on replay after clear.
- **Admin override:** `daily_missions(date, recipe json)` table — ops can pin a specific recipe to a date (sponsor days, events). Otherwise the generator's output for that date is cached the first time it's requested.
- **Client fallback (the standing invariant):** if the recipe can't be fetched, the client runs the same deterministic generator over the bundled tables with the same seed, so the card never breaks; the reward claim still goes through the server, which recomputes the recipe from its tables and refuses a mismatch. Missing info never spends RP and never shows a dead card.

## 4. Data & pipeline shape (for the spec that follows)

CSV-first, and these become content catalogs beside the existing eight so the admin can edit and publish them without a build:

- `Assets/Resources/Data/missions.csv` — the `Missions` sheet columns: `id, order, tier, key, name_en, name_ja, courseId, holeId, startAreaId, pinIndex, windPresetId, loadoutId, goal1Type, goal1Param … goal3Param, difficultyScore, staminaDrain, firstClearRP, replayRP, itemRewards, dailyEligible, unlock`.
- **Hooks that already exist for this:** `GameSession.StrokeCapEnabled` + `StrokeCapOverPar` are an explicit "Missions opt-in" (`GameSession.cs:60–82`, `HoleCompletionBridge.cs:28–156`) — a hole can end FAILED early, which is how SHOTS/SCORE goals fail without playing out; `HoleData.RewardType {Points, RepairKit, Ball}` + `replayRewards` on the hole card is the reward-strip model to extend with `Ticket`; `HoleProgressionService` (SaveData `unlockedHoles`/`playedHoles`) is the progression facade to mirror for missions; `modes.csv` row `missions` is `locked=true, target=none` — it needs a real `target` (`mission_select`) the withhold rule accepts.
- `mission_start_areas.csv` (id, courseId, holeId, kind, x, y, z, bake_hash) — baked from scene markers (see §2.1), not hand-typed.
- `mission_wind_presets.csv`, `mission_loadouts.csv`, `mission_goal_weights.csv` (the last one lets the admin retune the curve; publish validation recomputes `difficultyScore` and warns if a mission's score leaves its tier band or the campaign order stops being non-decreasing).
- Progress: `mission_progress` per user (missionId, clears, bestStrokes) — belongs on the server with the rest of `progress_server_side`; first-clear vs replay reward is decided server-side from that row.
- Earn actions to add (client code + catalog rows): `mission_clear` (pts NULL, max_per_event 60), `mission_replay` (5, daily cap shared with hole_replay), `mission_tier_clear` (pts NULL, max 300), `daily_mission` (30, once_per_user/day), `daily_streak` (pts NULL, max 30).
- `modes.csv` `missions` row flips `locked=false` from the admin when the mode ships (already a publish, not a build, per `game_modes_admin`).
- Localization: `mission.<key>.name` / `.desc` EN + JA — names for all 40 are in the workbook; goal descriptions are generated from `(type, param)` templates, so copy can never disagree with the rule again.

## 5. Admin control (sheet `AdminCatalogs`)

Same split `game_modes_admin` established: **content = catalogs (draft → publish → next launch), rewards = server tables (live on save, audited)**. Nothing about missions needs a build once this ships.

**Content catalogs #9–#15**, all through the existing `Tools/content/catalogs.py` `CATALOGS` table, seed migrations via `seed_from_csv.py`, `registry.ts` panels, `REQUIRED_COLUMNS` / `contentValidate.ts`, `ContentCatalogStore` overlay on the client — mechanical additions beside the eight:

| Catalog | Panel | Publish validation (block unless noted) |
|---|---|---|
| `missions` | **Missions panel** — table + row editor where hole / start / wind / loadout / goals are dropdowns fed by the component catalogs; `+ New row`; `is_active` withholds a mission (list renumbers, unlock chain skips it) | start↔loadout compatibility; goal params typed; `difficultyScore` **recomputed on publish** from `mission_goal_weights` (the stored value is display only); tier band; campaign order non-decreasing (warn); `firstClearRP ≤ mission_clear.max_per_event` |
| `mission_start_areas` / `mission_wind_presets` / `mission_loadouts` / `mission_goal_weights` / `mission_tiers` | **Components panel**, one tab each; goal-weights tab shows the re-scored 40-row tier table before you publish | loadout `supplied` rows must resolve every club type+rarity to ≥ 1 `Clubs.csv` row; tier bands contiguous, non-overlapping; unlock N ≤ missions in tier |
| `daily_mission_weights` | **Daily panel**, weights tab | weights ≥ 0, every group sums > 0, band per weekday set |

**Server mirror on publish** (the `golfin_mode_fees` pattern, same transaction, publish fails if the mirror write fails): `golfin_mission_rewards(mission_id pk, tier, first_clear_rp, replay_rp, is_active)` + tier-bonus rows. The claim endpoint reads the mirror, never the client's number.

**Live tables** (like tournaments — edited on save, audited, no publish cycle):
- `daily_missions(date pk, recipe jsonb, pinned bool, generated_at)` — **Daily panel** calendar: shows the generated recipe for each date, *Preview next 14 days*, *Pin recipe* to override a date (sponsor days), cannot pin a past date. `GET /missions/daily` returns today's row, generating and caching it on first request.
- `mission_progress(user_id, mission_id, clears, best_strokes, first_cleared_at)` and `daily_mission_claims(user_id, date, streak, rp)` — read in the **Users drawer → Missions tab**, with a *reset mission* action through `writeAudit()`. Daily panel shows clear-rate per date so a badly generated day is visible the same day.
- `game_point_actions` rows `mission_clear` (pts NULL, max 60), `mission_replay` (5, daily cap 50), `mission_tier_clear` (pts NULL, max 300), `daily_mission` (30, once_per_user/day), `daily_streak` (pts NULL, max 30) — edited on the **Rewards panel** already specced; drift warnings: `missions.firstClearRP` vs `mission_clear.max_per_event`, `daily_mission.pts` vs the Daily panel's base.

**Claim path:** `POST /missions/claim {mission_id | date, strokes, goals_met, idempotency_key}` → server checks `golfin_mission_rewards.is_active`, decides first-clear vs replay from `mission_progress`, credits through `earn_pts_v2` with the mirrored amount, upserts progress. For the daily it recomputes the recipe for that date and refuses a claim whose recipe hash doesn't match (covers the offline-fallback client). `modes.missions.locked` stays the on/off switch and is already a publish.

Deployment proof per PIPELINE_HARDENING §23 applies to every panel above (`npm run deploy` + Cloudflare deployment id quoted).

## 6. Economy (sheet `Economy` — formulas, editable)

Missions are a **source**, and the only mode with no entry fee (recommendation: keep it 0 — it's the retention loop). Stamina is the throttle: a full-hole mission costs 5–7, a short-start mission 2–3, against the 25-point safe budget + 30/h regen, so ≈ 4 missions/day for the reference player.

| | RP |
|---|---|
| Campaign first clears 150 + 250 + 400 + 600 | 1,400 |
| Tier-clear bonuses 50 + 100 + 200 + 300 | 650 |
| **Campaign total (one-off per account)** | **2,050** + 4 Repair Kits, 3 Premium Repair Kits, 6 Gold Tickets |
| Daily mission 30 + streak (45 per 7-day cycle ≈ 6.4/day) | 36.4/day + 1 Gold Ticket/week |
| Mission replays 5 RP, daily cap 50, assume 5 | 25/day |
| **Recurring from missions** | **≈ 61/day** |
| Campaign pace 4 missions/day → ≈ 10 days | ≈ +205/day while it runs |

Reference player (ECONOMY_MASTER §1): earn 300 → **≈ 361/day post-campaign** (≈ 566 during the campaign), spend unchanged at 50 → **net ≈ 311/day (was 250)**. Level cap 14,520 RP in **~47 days (was ~58)**; full character roster 18,800 RP in **~60 days (was ~74)**. Theoretical daily ceiling rises by 110 recurring (replay cap 50 + daily 60); campaign actions are one-off and account-bounded, not daily. This is the direction the economy doc already wanted — more recurring earn is fine because the recurring sinks (repairs, balls, gacha) are still dormant; when durability wear ships, missions are also the natural place to spend repair kits, which is why the item rewards are kits rather than RP.

## 7. Mission Selection screen (Figma `4065:7960` next-mission state, `4065:7961` replay state)

Same skeleton as Hole Selection — Top UI, two filter rows, scrolling card column, nav bar, side arrows, `Rankings Container` (4003:4576) top-right of the content area. Cesar's brief: mimic Hole Selection, bind different data. Reuse the Hole Selection prefab hierarchy and bind; do not rebuild.

**Filter row 1 — course** (mockup `YAITA - RINDOU 35/100` · 🔒 `YAITA - KIKYOU` — placeholder names; the build has only `lomond-country-club`, with Kisarazu and Taiheiyo source folders waiting in `Docs/Golf Courses/`): course tabs with `cleared/total`, padlock on a course with no active missions. Missions carry `courseId` + `holeId`, so a second course is more rows. Locked = no active missions on that course, or course itself locked.

**Filter row 2 — tier** (`BEGINNER 25/25` · `AMATEUR 10/25` · 🔒 `PRO 0/25` · 🔒 `LEGEND 0/25`): tier tabs with `cleared/total`, padlock until the 8-of-10 gate opens. Counts are **read from the catalog**, never hard-coded — the mockup's 25/tier, 100 total is placeholder; launch data is 10/tier, 40 total. Default-selected tab = furthest unlocked tier (Confluence rule), persisted across nav-bar round trips.

**Card column** (`Mission Card Container` ×6, one expanded): the mockup's 2 collapsed replay cards above, 1 expanded, 3 collapsed locked below. Mapping to data:

| Card element | Binding |
|---|---|
| Header pill | `REPLAY MISSION` (cleared) / `NEXT MISSION` (first uncleared, gold) / 🔒 `LOCKED` |
| Title | `{order} - {name_en\|ja} - Hole {holeId}` — NOTE: mockup repeats "Risk and Reward - Hole 5" on every card; ours is `order - mission.<key>.name - Hole N` |
| Expanded: course line | `{courseName} - Hole {holeId}` — the mockup's "Lomond Country Club - Hole 5" is the real course (`HOLE_LOMOND_N` keys in `HoleDatabase.csv`) |
| Expanded: hole map | the Hole Selection map thumbnail for `holeId`, with the **start marker moved to `startAreaId`** so a bunker/fairway start is visible before playing |
| Expanded: goal bullets ×1–3 | generated from `(goalType, param)` templates; **plus** two auto-lines the mockup doesn't have yet: wind (`Strong headwind`) and loadout (`Supplied: Driver + Putter` / `Your bag — no woods`) — the player must see the two things that set difficulty |
| Reward strip | icons ×qty: RP · items (repair kit icon = the mockup's wrench) · ball / ticket; cleared cards show the **replay** amount (5 RP), uncleared show first-clear + items; locked cards greyed |
| Button | `PLAY` (gold) on NEXT; `REPLAY` (silver) on cleared; none on locked |

Only one card is expanded at a time; tapping a collapsed card expands it and collapses the previous (Hole Selection behaviour). The expanded card by default is the NEXT mission; the list scrolls so it sits at the top with cleared cards above (mockup shows exactly this).

**Daily Mission — not in the mockup, needed for §3:** one pinned card **above the tier filter**, full width, distinct header `DAILY MISSION · 12:34:56` (countdown to UTC midnight) and a streak chip (`🔥 3`); expanded layout is the same card with the goal/wind/loadout lines; button `PLAY` → `CLEARED ✓` for the rest of the day. No Home-screen surface in v1 (deferred, see Entry below). Needs a Figma frame — request from Nishikawa/Cesar; until then reuse `Mission Card Container` with a different header colour.

**Entry — tie the mode card to the screen (both surfaces):**

- `modes.csv` row `missions` today: `locked=true, target=none, entryFee=0, rewards=20`. It needs `target=mission_select` and, when Cesar flips it, `locked=false` — both are a `modes` publish from the admin, no build. Card copy (`tagline` "Coming Soon — complete challenges.", `rewardsTextKey`) is also catalog-edited; recommend `rewardsTextKey=MODE_REWARDS_VARY` like tournaments, since mission rewards are per-mission.
- **Home carousel** (`ModeHomeCard` prefab → `ModeCarouselController.HandlePlayClicked`, `ModeCarouselController.cs:495–528`) and **Mode Select list** (`ModeSelectScreenController.cs:199–235`) each carry their own `switch (mode.target)`; add `case TargetMissionSelect: sm.ShowScreen(ScreenId.MissionSelection)` to **both**, and register the constant in `ModeSelectScreenController` (`TargetMissionSelect = "mission_select"`) so `CanDispatch` — the withhold rule `ModesDatabaseCSV` reads — admits the target. Note the carousel switch uses string literals (`"hole_select"`) instead of the constants; the missions case is the moment to point it at the constants so the two lists can't drift.
- **Entry fee** goes through the card exactly as the other modes (`ModeCardController.HandlePlayButtonClicked` → `PointsSpendGate.Spend(_data.entryFee, SpendReasons.ModeEntryFeeFor("missions"))`, server-validated against `golfin_mode_fees`); at fee 0 the gate short-circuits, so nothing new is needed.
- **New `ScreenId.MissionSelection`** in `ScreenManager` (`enum ScreenId`, after `HoleSelection`), screen prefab built from the Hole Selection prefab. Back returns to whichever surface opened it (Home or Mode Select) — same as HoleSelection today.
- **Daily on Home — DEFERRED (Cesar 2026-08-28): needs design, not in this version.** v1 has no Home-screen surface for the daily; PLAY on the Missions card lands on the Mission Selection screen with the daily card pinned at the top (§7). A badge on the Missions home card is the candidate for the next version once Figma has it.

**Rankings Container** → mission leaderboards (Confluence: daily / weekly / monthly / all-time RP). Out of scope for v1; the button can open a "coming soon" modal so the layout stays.

## 8. Repo verification (2026-08-28, folders granted) — what changed from the first draft

| Assumed in the first draft | Found in the repo | Effect |
|---|---|---|
| Yaita, 9 holes, pars from the old sheet | **Lomond Country Club, 18 holes** in `EditorBuildSettings` (`Hole_01–18_Geo`); pars in `HoleDatabase.csv` = scene `HoleMetadata.par` | Campaign re-holed across all 18 (each hole 1–3×), still 10/tier and non-decreasing |
| 5 tee sets incl. Silver/Tourney with old coordinates | 4 tee marker sets (`back/regular/front/ladies`) placed by the importer; spawn = regular midpoint | Silver → FRONT, Tourney → BACK (weight 5); Legend/Pro both on BACK |
| Start-area coordinates reusable from the old sheet | None exist for Lomond | Bake `mission_start_areas.csv` from `SurfaceMarker` GOs + drift gate |
| Wind units unknown | `windSpeedMph` + `windDirectionDegrees` per hole → `WindContext`/`WindCfg` at hole load | Presets in mph (6 / 12 / gusty 6–18); gust re-roll needs a per-shot hook |
| Supplied gear = reuse tournaments' mechanism | `gear_rule='supplied'` exists but was never implemented ("display fiction") | Per-session bag override in `BagManager` is a new piece; tournaments inherit it |
| Alternate pin needs a new table | `GreenTopology.GetPinCandidates()` already authored per hole | `pinIndex` column; ALT_PIN daily modifier is cheap |
| Stamina 1/shot, 25-point budget (Confluence) | Flat 8 per hole completion, tank 60+, regen 12/h, penalty < 70 % | `staminaDrain` per mission; missions/day is a play-time assumption, not a stamina cap |
| Missions mode a blank slate | `GameSession.StrokeCapEnabled` is already a Missions opt-in; `modes.csv` `missions` row exists (`locked=true`, `target=none`) | Goal-fail-early path exists; add a `mission_select` target |
| Admin gaps | `game_modes_admin` implemented today (READY_FOR_SELF_REVIEW): `modes` is catalog #8, Rewards panel over `game_point_actions` live, `golfin_mode_fees` mirror pattern deployed | §5 builds on shipped machinery; catalogs #9–15 numbering holds |
| `Docs/Economy/ECONOMY_MASTER.md` in the repo | Identical to the project copy before today's edit | Updated copy goes into the repo with this doc (Code commits) |

Still open before the spec:

1. `HoleTees.csv` yardages vs pars (10 holes disagree) — which file is wrong?
2. Bake start areas for all 18 holes and eyeball them once in the editor (bunker choice per hole matters).
3. Confirm every hole has ≥ 2 pin candidates before enabling ALT_PIN.
4. Entry fee 0 for Missions (recommendation) — Cesar's call; it's a `modes` publish either way.
5. `mission_progress` lands server-side with the `progress_server_side` pattern (grandfather nothing — missions are new).
6. Figma frame for the Daily Mission card on the Mission Selection screen. (Home-screen daily badge: deferred to a later version, needs design.)
