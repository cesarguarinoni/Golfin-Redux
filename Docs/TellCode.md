# TellCode.md — handoff channel (POINTER + KICKOFF FILE)

> **Spec-sized tasks live in `Docs/Specs/Active/<slug>/SPEC.md` — this file only points at them.**
> **Kickoff-sized tasks (no spec folder) live HERE in full**, in the PENDING KICKOFFS section, so they survive the chat session that produced them. (Rule updated 2026-08-04 by Cesar: chat-only kickoffs die with the session; every kickoff the Architect produces is written here at the time it is produced.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Last updated:** 2026-08-12 later (Architect — SEQUENCING CHANGE, Cesar: **Reward Points move to the backend FIRST; admin dashboard waits.** New active task `reward_points_backend` (`Docs/Specs/Active/reward_points_backend/SPEC.md`) — answers GPS_UNITY_PORT_SPEC §2: unify on the PLAYLIFE `points_transactions` ledger. Decisions: online-required spends + queued earns (idempotency keys); dashboard's points panel will read the one ledger (its open question 2 = BEFORE). Kickoff issued for **Slice 1** (Unity infra behind default-OFF flag — no backend dependency, zero behavior change). Phase A UNBLOCKED same day: repo at **`/Users/cesar/Documents/playlife`** (Cesar; fresh copy, mtimes 2026-08-12) — real `points.py` + `points_atomic.sql` read; ⚠️ key find: `earn_activity_pts` welds every earn to avatar XP/level at 5–50-pt PLAYLIFE scale. Architect proposed a separate `golfin_rp` currency — **Cesar OVERRODE same day: ONE shared RP value** (`total_points`); game prices/rewards get rebalanced to the GPS scale, and per Cesar the **rebalance is folded into this task** — Slice 2 ships rebalance + cutover together, gated on an `RP_REBALANCE.md` table (Architect drafts, Cesar approves every number). Spec §3/§4 rewritten to the one-value design; Phase A kickoff below REVISED accordingly (any earlier `golfin_rp` kickoff is dead — this file's version is current). **REBALANCE APPROVED same day:** `RP_REBALANCE.md` (in the spec folder) is the binding number set — global ÷10, level-up `ceil(level/2)`, stamina global rounding, §3 caps as drafted, and one amendment: **the 50,000 welcome grant is REMOVED** (testing-only per Cesar; new accounts start at 0, test balances admin-set via dashboard/Supabase — no welcome or migration actions in `game_point_actions`, no client migration logic, and Slice 2 deletes `DEFAULT_STARTING_POINTS` from `RewardPointsManager.Awake`). Also of record: **Cesar now runs the GPS app too** — every former "Ken's nod" item is Cesar's own call. **✅ PHASE A SHIPPED TO PROD same day** (Architect + Cesar): migration `2026_08_12_points_spend_idempotency.sql` applied via Supabase SQL editor (verify-first passed; `earn_pts_v2`/`spend_pts` live, service_role-only; idempotency column+index live; catalog = 3 actions, welcome/migration seed rows deleted per decision #6), **gift bug fixed end-to-end** (new migration `2026_08_12_gift_pts_total_points_fix.sql`: trigger now credits total_points, EXECUTE revoked, reconciliation ran — invariant holds on 0 profiles, the 2 affected accounts now 475/680), and **deployed**: flyctl installed (`~/.fly/bin`), Cesar authed (wonderwall acct, one-time high-risk unlock), `fly deploy` green, `/health` ok, `/points/spend` + `/points/earn-game` + `/points/balance` all respond 403-not-404 in prod. **SLICE 1 LANDED same day** (Code report: 46 new tests, suite 1159/0, zero-behavior-change proven two ways; contract notes recorded: /health is root-mounted not under /api/v1, 403 = missing header vs 401 = refresh-and-replay, errors are {detail} not {data}; AuthServiceTokenProvider adapts AuthService.cs:126 refresh — no second auth path). Manual acceptance on Cesar: Editor play mode signed in -> GOLFIN > Points Backend > Enabled -> Log Server Balance Now -> expect the balance log; toggle back OFF after. **Slice 2 kickoff ISSUED — block below** (rebalance + re-point + cutover; catalog-mirror SQL is write-only, Architect applies). `admin_dashboard` pointer below marked ⏸ ON HOLD; its kickoff stays valid, don't start it.)

- **Last updated:** 2026-08-12 (Architect — Cesar gave GO: **"start the admin dashboard."** `admin_dashboard` spec (filed 2026-08-12 by the auth-epic Cowork session, `Docs/Specs/Active/admin_dashboard/SPEC.md`) is now the active task — pointer + kickoff added below (§5 steps 1–2: Next.js scaffold at `Tools/admin-dashboard/` + read-only Users panel over Supabase). STATUS.md created at SPEC_READY. Web app, not Unity — no Assets/ edits.)

- **Last updated:** 2026-08-10 (Architect — close-out sweep per Cesar: "all tasks from yesterday and today are done." CLOSED: `auto_club_selection` (`43d8a34c9`), `power_gauge_target_marker` (Order 357, off the video), `map_view_strict_crop_indicators` (Order 355, off the video), `aim_camera_ball_centering` (moved to Completed in this sweep — pending Architect calls D2/D3 accepted as-is), `putter_aim_blue_line` (approved off the Hole 6 video), plus the `hole1_cup_buried_under_green` repair (`da62daf86`, surgical CupReseatTool — a Hole 1 re-import would have destroyed 1362 trees + the baked sim data; standing rule: shipped holes are repaired in place, never re-imported). All five SPEC_READY pointer+kickoff blocks below pruned to strikethrough one-liners. Notion GOLFIN_Roadmap rows closed 2026-08-10. Repo committed + pushed same day, incl. the previously-uncommitted `Scenarios.cs` in-flight ClubHandle regression guard + smoke-bot log refresh. `Docs/Specs/Active/` now holds no SPEC_READY work (only the historical `ob_boundary_presentation` + `phone_build_smoke_test` folders) — next task comes from Cesar.)

- **Last updated:** 2026-08-05 15:05 JST (Architect — **DEVICE ERA.** Game builds+runs on physical iPhone since 2026-07-27; signing SOLVED (do not re-litigate); on-device smoke found 7 issues. Fixed since: `centralball_device_invisible` (device-verified `1a4ad15ca`), `hole6_tree_collision_profiles` (`c1d38e280`), `camera_drag_touch_origin`/K1 (CLOSED — `bb59d32dd` 08-03, device-verified per commit + Cesar's session; block deleted 2026-08-05), `nav_bar_edge_gaps` (K4) (CLOSED — `49825e867` + ticket-cluster follow-up `26ceeb051`, 08-03 — PRE-DATED the batch write, same drift class as K1; cause was H1: fixed-width 1178px center-anchored bars under a **ConstantPixelSize** canvas, fix = stretch anchors + proportional icon re-anchor; NOT the CanvasScaler — the `loading_bar_inset` (K14) hold on that question is resolved; block deleted 2026-08-05, flagged by Cesar). Shipped: `build_version_stamp` (3 defects → hardening kickoff below). **iOS Simulator three-tier verification loop VALIDATED** — canonical doc `Docs/Pipeline/IOS_SIMULATOR_LOOP.md`; standing rules: never wipe the seeded DerivedData, never `BuildPipeline.BuildPlayer` via MCP script-execute. Full story: `Docs/Reports/2026-08-04_ios_simulator_build_blocker.md` §§10–13 + `Docs/AI_CONTEXT.md` top block. **OPEN = the PENDING KICKOFFS below** (6 smoke issues + build-stamp hardening + housekeeping; K9 `ui_frame_pacing` smoke #8 added 2026-08-05; K10 `ob_recovery_fixes` **CLOSED 2026-08-05** (`90dd574ff` camera+drop rule, `ed65f5726` permanent capture Y-flip fix; CupZoom same-class wedge found+fixed; OB now stops chasing with no aerial cut; ground-level settle built then reverted per Cesar); K1 closed. K11 `club_selection_green_gate` **CLOSED 2026-08-05** (`066df31f2` selector gate + `efa681acb` §2f re-decide after reposition — the item deferred pending K10; ⚠️ K10's close-out swept K11's in-flight lines and briefly broke `main`, repaired forward — see the K11 block). K12 `matchmaking_scan_pacing` added 2026-08-05 — find-opponent animation: decelerating scan + total cut ~5.6s→~3.1s, NO scene edit (new-serialized-field technique), queued AFTER K11 per Cesar — **now NEXT UP**. K13 `boot_loading_screen_removal` **CLOSED 2026-08-05** (`d3bf00026`) — measured first as instructed: zero real progress ever fed (`_useExternalProgress` never true, max `_realProgress` 0.000 across 2 runs), boot init done at t=3.8s vs Splash interactive at t=9.0s, real work behind the transition ~0.23s (Main Theme decode, already under the 0.25s fade) → REMOVED per the <2s rule. **click→Home 2.72s → 0.48s.** HoleLoad path verified byte-identical + live-regression-passed (real bar 0→1 via the real ModeHomeCard PlayButton). ⚠️ Adjacent knob still open: `minLoadingTime` (2s, scene-serialized) is also the hole-load screen's MINIMUM — measured 2.586s with progress already at 1.0; same scene-serialization trap as K12. K14 `loading_bar_inset` **CLOSED 2026-08-05** (`bae5386f3`) — shipped the SCENE route (not the code shim); `LoadingBarRoot` sizeDelta.x 0→-16, isolated one-line diff. Gate lifted when Cesar landed K7 (`d680198b3`) mid-task. **Two kickoff premises proved wrong and are corrected in the sequencing bullet above — read them before reusing that block's reasoning:** (a) the bar was never at zero inset — `Track` already carried `-48` (24 units/side), so the edit went 24→**32**/side; (b) the loading screen's canvas is **ScaleWithScreenSize** (ref 1170×2532, match-width), NOT ConstantPixelSize — that was K4's separate nav-bar canvas, so the kickoff's "8 units = 8 device px, dial ~24 for points" units advice was wrong-canvas and must not be reused. Verified on rendered pixels at 1170×2532: inset 32/32 at both 0% and 100%, fill 1106px across a 1106px track (reaches both ends exactly, the don't-break-functionality gate); `ProgressText` edges match `Track` edges; 16:9 renders the same 32 units as 28px at scaleFactor 0.874. Editing `Track` (the rect that owned the inset) would have desynced `ProgressText`, which carries its own `-48` — the root was the correct single dial. ⚠️ Side-finding for whoever owns matchmaking: opening+saving ShellScene reconciles STALE `MatchMakingModal` prefab overrides (an anchored-position `-564`→`-68` move, plus four `scan*Seconds` fields from `925a25398`). Reverted out of this commit to keep it one-line; it is still unreconciled on disk and will reappear on the next ShellScene save. RECONCILIATION DONE 2026-08-05 per Cesar ("Close them"): `arrow_speed_retune` (K6) CLOSED — `cd0ef6ed4` 08-04 verified against the kickoff shape in the diff: F13 changelog entry (93 lines), BOTH mirrors (controls.csv + ControlsConfig.cs), ShotController floor clamp (`Mathf.Max(arrowHz, MinArrowSpeedHz)`), both test files updated. NOTE: F13 locked at 30 fps, BEFORE `ui_frame_pacing` landed — if arrow feel reads differently at 60 fps on device, retune reopens as a NEW row; F13 stays the record. `ui_frame_pacing` (K9) CLOSED — `7380baf67`, FramePacingBootstrap.cs exactly as specced; device feel signed off via Cesar's own device sessions; in-hole 60 fps knock-on unreported — watch in whole-game perf (940). `b702e1a41` wind→ball-flight ACCEPTED as landed (no kickoff existed; it carries NO F-entry — flag for the next physics-changelog pass). Both blocks deleted. K15 `app_identity` **CLOSED 2026-08-05** (`66ac68575` → `7a63f7c2f`) — **the app is now `Golfin`**: productName RE2 → `Golfin`, companyName → NEXT INNOVATION PTE. LTD., default icon → `Assets/Icons/Golfin-Icon2.png`; bundle id + signing UNTOUCHED as specced. `Golfin: The Invitational` shipped first and was rejected on sight of the springboard — iOS collapses the spaces before truncating, rendering `Golfin:TheI…`. **The built .app is now `Golfin.app`** (executable + process name `Golfin`; `RE2.app` deleted) — `IOS_SIMULATOR_LOOP.md` re-pointed. ⚠️ Two findings for anyone driving the sim loop: (a) an append re-export can leave the pbxproj referencing `lib_burst_generated.cpp`/`.a`, which Burst NEVER generates for the simulator SDK — the tier-2 build dies with "Build input file cannot be found"; this is NOT a DerivedData problem, do not wipe, just strip the 8 lines (fix is PERMANENT — append preserves the pbxproj; the refs were legacy state inherited from an earlier device-SDK export); (b) icon2 carries an opaque alpha channel that Unity strips at icon generation — built icon reports `hasAlpha: no`, store-safe, leave it alone. K16 `hole_scene_leftover` **CLOSED 2026-08-05** (`a6b022642` fix + `1372da34b` residue strip) — capture launchers now snapshot the scene setup before staging and restore it at EnteredEditMode, closing staged hole scenes WITHOUT saving; shipped the Option-B alternative so SmokeRunner2e/2f inject their host at EnteredPlayMode and NEVER serialize it, removing the LabScaffold write entirely (2f's save was also baking 13 unrelated `_disabledAlpha` lines per run). The committed SmokeRunner2fHost residue is gone and cannot recur. 2f's defensive pre-clean stays and now finds nothing. ⚠️ SEPARATE pre-existing bug surfaced while verifying: the 2e OB capture no longer reaches OB (18.95m AtRest vs 131.28m TerminalState=OB in the log from `4f9fd2012`) — untouched host, fixed preset, so physics/terrain drift; it falls back to "capturing current state as evidence", i.e. LOOKS successful while proving nothing. Needs its own task.) plus `putter_aim_blue_line` (413, SPEC_READY in `Specs/Active/`, awaiting Cesar go) and a device pass on `demo_build_slice` (426). Everything below this bullet predates the device era and is historical.)

- **Last updated:** 2026-07-02 (Architect — `1v1_result_rewards_display` (347) DONE. NEXT-at-the-time = `stamina_boost_shop` (517) design pass. STALE — superseded by the device-era bullet above.)
- Older narrative bullets (2026-06-11 → 2026-06-24): preserved in git history of this file — all tasks named in them are closed in `Docs/Specs/Completed/`. Trust `Docs/Specs/Active/` + the AI_CONTEXT headline, not old bullets.

---

## 📋 SPEC_READY POINTERS

- **`reward_points_backend`** (filed 2026-08-12, Architect) — **SPEC_READY, GO from Cesar 2026-08-12 (points-first sequencing).** Unify GOLFIN Reward Points onto the PLAYLIFE Supabase ledger (`points_transactions`), server-authoritative — the §2 fork in GPS_UNITY_PORT_SPEC, resolved. Offline policy: online spends, queued earns. Kickoff below covers **Slice 1 only**: `Golfin.Net` ApiClient + `PointsService` + persistent pending-ops queue behind a default-OFF `PointsBackendEnabled` flag — byte-identical game behavior with the flag off. **One-value design (Cesar): RP == PLAYLIFE `total_points`, no new currency.** Phase A (backend: idempotency + `spend_pts`/`earn_pts_v2` + `/points/spend` + `/points/earn-game` in `/Users/cesar/Documents/playlife/backend`) has its OWN kickoff below — runs in the playlife repo, independent of Slice 1, scale-agnostic. Slice 2 = **economy rebalance to GPS scale + re-point call sites + flag flip, one cutover** — gated on the Cesar-approved `RP_REBALANCE.md` table. Spec: `Docs/Specs/Active/reward_points_backend/SPEC.md`. Kickoffs below.

### Kickoff · reward_points_backend — Slice 1

```
Read Docs/Specs/Active/reward_points_backend/SPEC.md and implement §4 Slice 1
only (Golfin.Net infra + PointsService + pending-ops queue, behind a
default-OFF flag). Phase A and Slice 2 are later kickoffs — do not start them.

Context:
- Goal: infrastructure for moving Reward Points to the PLAYLIFE backend
  (https://playlife-api.fly.dev/api/v1, Bearer Supabase JWT, {data:...}
  envelope). This slice changes NO game behavior: flag PointsBackendEnabled
  defaults OFF and nothing existing calls the new code paths when off.
- New asmdef Golfin.Net: ApiClient singleton (UnityWebRequest, Bearer attach,
  envelope unwrap, retry on 408/connection failure, 401 -> refresh -> retry
  once), static Endpoints (/points/* + /health only), ApiResult<T>.
- Reuse the Supabase session/token from the auth epic (2ffe0403f, 122842b8c,
  847d7bced) — read that code for the exact accessor; if token refresh isn't
  exposed yet, flag it in the report rather than hand-rolling a second auth path.
- PointsService (Golfin.Economy): RefreshBalanceAsync() + cached balance +
  persistent JSON queue in Application.persistentDataPath (idempotency GUID
  per op, FIFO replay on reconnect/login; earn ops only in v1).
- Do NOT touch RewardPointsManager or any of its call sites in this slice.
- EditMode tests: queue round-trip, idempotency-key stability, replay ordering,
  ApiClient envelope/401 paths via mocked transport.
- Out of scope: backend edits, RewardPointsManager re-pointing, spend flows,
  migration grants, admin dashboard.

When done: list changed files with a 1-line summary each, run the EditMode
suite (must stay green — zero behavior change is the acceptance bar), flag the
manual device check (flag ON + logged in -> RefreshBalanceAsync logs the test
account's server balance), update STATUS.md + IMPLEMENTER_REPORT.md in the
spec folder, and update Docs/AI_CONTEXT.md.
```

### Kickoff · reward_points_backend — Phase A (backend, run in the playlife repo) — REVISED (one-value design)

```
Open /Users/cesar/Documents/playlife. Read
/Users/cesar/Documents/GolfinRedux/Docs/Specs/Active/reward_points_backend/SPEC.md
and implement §3 Phase A only (idempotency + spend + earn-game on the EXISTING
balance). Slice 2 is a later kickoff — do not touch the Unity repo.

Context:
- ONE shared RP value (Cesar decision): GOLFIN RP == total_points
  (activity_pts + gift_pts). NO new currency, NO new balance column. Game
  earns post to activity_pts via the existing atomic/avatar-coupled semantics;
  the game-side rebalance to GPS scale happens in Slice 2 and gates when the
  game actually starts writing — your work here is scale-agnostic.
- Migration backend/migrations/2026_08_12_points_spend_idempotency.sql —
  mirror 2026_06_29_points_atomic.sql conventions exactly (CREATE OR REPLACE,
  revoke-from-public/anon/authenticated + grant-to-service_role on EVERY new
  function, staging verification footer). Contents per spec §3.1:
  points_transactions.idempotency_key + partial unique index;
  earn_pts_v2 (same body as earn_activity_pts incl. avatar coupling, plus
  idempotent replay — leave earn_activity_pts itself untouched);
  spend_pts (row-locked, activity_pts first then gift_pts, total_points kept
  consistent, distinct insufficient result, negative ledger row(s), no
  avatar_xp change, idempotent);
  game_point_actions table (pts nullable = fixed server amount vs cap-validated
  client amount; once_per_user) seeded with PLACEHOLDER amounts — comment them
  as placeholders, real values come from the Slice-2 rebalance table.
- backend/routers/points.py additions in the existing style ({data} envelope,
  get_current_user, service client): POST /points/earn-game (resolve amount:
  catalog-fixed else validated client amount -> rpc earn_pts_v2),
  POST /points/spend (-> rpc spend_pts, explicit insufficient payload).
  /balance, /earn, /redeem untouched.
- Add tests if the repo has a test setup; otherwise include the staging
  verification SQL block (single call, idempotent replay, concurrency loop,
  insufficient-funds incl. the activity->gift split boundary) in the migration
  footer per house style.
- Out of scope: Unity/GolfinRedux, applying the migration to prod (Cesar or
  Architect-via-browser does that), fly deploy without Cesar's go, any
  rebalance numbers.

When done: list changed files with a 1-line summary each; state clearly that
the migration is WRITTEN but NOT APPLIED and what the apply+deploy steps are
(Supabase SQL editor → then fly deploy from backend/, app playlife-api — flag
if flyctl isn't authenticated); update STATUS.md + IMPLEMENTER_REPORT.md in
the spec folder and Docs/AI_CONTEXT.md in GolfinRedux.
```

### Kickoff · reward_points_backend — Slice 2 (rebalance + re-point + cutover) — issued 2026-08-12 after Slice 1 landed

```
Read Docs/Specs/Active/reward_points_backend/SPEC.md §4 (Rebalance + Slice 2)
and RP_REBALANCE.md in the same folder (APPROVED — binding numbers), then
implement Slice 2: economy rebalance + re-point RewardPointsManager call
sites + cutover prep. Slice 1 infra (Golfin.Net, PointsService, queue, flag)
is in; Phase A is live in prod.

Context:
- Rebalance FIRST, RP_REBALANCE.md verbatim: HoleDatabase.csv Points rows
  (100->10, hole 6 200->20, replay 50->5); modes.csv (practice entryFee
  100->10, rewards 50->5; versus rewards + reward1Amount 200->20; missions
  200->20); tournaments.csv entryFeeRP (100->10, 500->50);
  tournament_prizes.csv rpReward all /10; LevelUpCosts.csv cost_r =
  ceil(level/2), sp_reward unchanged; gacha_banners.csv (500/4500 -> 50/450,
  750/6750 -> 75/675); shop_catalog.csv rpCost+saleRpCost /10;
  stamina_shop_items.csv rp_cost = round(x/10) min 1;
  RewardPointsDebugPanel deltas ±1000/±10000 -> ±100/±1000, "Set 50k" ->
  "Set 5k". Item amounts (RepairKit/Ball) and SP rewards are NOT RP — leave.
- Remove the client seed: the DEFAULT_STARTING_POINTS (50,000) path in
  RewardPointsManager.Awake goes away entirely (decision of record #6 — new
  accounts start at 0; server balance is authoritative when the flag is ON).
- Earns: EarnPoints call sites gain an action/reason; when flag ON also
  enqueue earn-game with actions hole_complete / hole_replay / versus_win /
  tournament_prize (one idempotency GUID per gameplay event). Local balance,
  leaderboard accumulators, and RP SFX behavior unchanged — the queue
  reconciles the server.
- Spends: PointsService.SpendAsync(amount, reason) — server debit precedes
  the action in the four spend flows (character level-up, club level-up,
  tournament signup via IRewardPointsService, mode entry fee).
  IRewardPointsService gains an async variant for
  LocalTournamentBackend.Register; exact seam adaptation is your call under
  the minimal-diff rule. Offline spend -> existing toast pattern
  ("Connection required"). Use the live contract Slice 1 recorded: 401 =
  refresh+retry; insufficient funds returns 200 with status "insufficient".
- Server catalog mirror — WRITE ONLY, do not apply: new file
  /Users/cesar/Documents/playlife/backend/migrations/2026_08_12_game_point_actions_rebalance.sql
  updating game_point_actions to the approved RP_REBALANCE §3 values:
  hole_complete pts=NULL max_per_event=20 daily_cap=400; hole_replay (NEW
  row) pts=NULL max=5 cap=100; versus_win pts=20 cap=200; tournament_prize
  pts=NULL max=2000 cap=NULL. Cesar/Architect applies it in Supabase.
- Flag: PointsBackendEnabled flips to default ON in this slice, AFTER
  everything compiles and tests pass. SetPoints/ResetToDefault/debug panel
  become flag-OFF-only (guard, don't delete).
- Out of scope: admin dashboard, fly deploy, applying any SQL, player_state
  sync, avatar/leaderboard server-side work.

When done: list changed files with a 1-line summary each, run the EditMode
suite (must stay green), run TournamentLoopCaptureHarness (fees and prizes
changed — signup must still work), list the manual cutover steps for Cesar
(apply the catalog SQL; hand-set the 5 test balances in Supabase; on-device
smoke with flag ON incl. one earn, one spend, one offline-queue replay),
update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

### Kickoff · points_cutover_followups — bot auth bypass + shop spend + sign-in gate (issued 2026-08-12, Cesar-decided)

```
Read Docs/Specs/Active/reward_points_backend/STATUS.md (top entries) for
context. Three bounded follow-ups from the Slice 2 report, all decided by
Cesar 2026-08-12. Minimal diffs; EditMode suite must stay green.

1. BOT AUTH BYPASS (unblocks TournamentLoopCaptureHarness and every
   boot-from-Splash bot). Dev-only BotSessionOverride behind an editor-only
   guard (#if UNITY_EDITOR or a GOLFIN_BOT_HARNESS define — must be
   impossible to ship in a player build): when active, the auth gate treats
   the session as signed-in with a fake local identity AND
   PointsBackendEnabled is forced OFF for the run — bots play the
   deterministic offline economy: no network, no credentials, no prod
   ledger writes. BotDriver.NavigateToHome must get Splash -> Home again
   without touching the Login screen. Acceptance: TournamentLoopCaptureHarness
   reaches === SEQUENCE COMPLETE === from boot.
2. SHOP SERVER SPEND. Route ShopTransaction through
   PointsService.SpendAsync exactly like the other four flows (busy state,
   insufficient branch, offline -> "Connection required" toast). Kills the
   self-refunding flag-ON purchase.
3. HARD SIGN-IN GATE (decision of record: NO guest mode). Close the path
   that reaches mode select without a session — signed-out users cannot get
   past Login, except via the item-1 bot override.

Out of scope: backend edits, admin dashboard, any economy value changes.
When done: changed files with 1-line summaries, EditMode suite green, run
TournamentLoopCaptureHarness end-to-end as item-1 acceptance, flag manual
device checks, update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder
and Docs/AI_CONTEXT.md.
```

- **`admin_dashboard`** (filed 2026-08-12, Architect) — ⏸ **ON HOLD 2026-08-12 (later): Cesar sequenced `reward_points_backend` first — do NOT start this kickoff until that lands.** Original entry: **SPEC_READY, GO from Cesar 2026-08-12 ("start the admin dashboard").** New internal web app at `Tools/admin-dashboard/` — Next.js (App Router) + TypeScript + Tailwind — over the shared Supabase project (`wmszyghwwkaptgqdunel`). This kickoff covers **§5 steps 1–2 only**: scaffold (panel registry, admin login + `ADMIN_EMAILS` allowlist, `admin_audit_log` migration SQL) + Users panel **read-only** (`auth.admin.listUsers` ⋈ `profiles`). NOT Unity — no Assets/ edits. service_role key is server-side only; Cesar pastes secrets into `.env.local` himself. Spec: `Docs/Specs/Active/admin_dashboard/SPEC.md`. Kickoff below.

### Kickoff · admin_dashboard

```
Read Docs/Specs/Active/admin_dashboard/SPEC.md and implement §5 steps 1–2
(scaffold + Users panel read-only). Steps 3+ are a later kickoff.

Context:
- New web app: Tools/admin-dashboard — Next.js (App Router) + TypeScript +
  Tailwind. Not Unity; touch nothing under Assets/. Talks to Supabase project
  wmszyghwwkaptgqdunel via a SERVER-SIDE service_role client only
  (lib/supabaseAdmin.ts — the key must never reach the browser).
- Secrets: scaffold .env.local.example (SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY,
  ADMIN_EMAILS) and gitignore .env.local — Cesar pastes real values himself.
- Admin auth v1: Supabase email/password login + server-side ADMIN_EMAILS
  allowlist. Day-one audit table public.admin_audit_log — write the migration
  SQL to Docs/GPS/ (Cesar applies it in the Supabase dashboard, same pattern
  as 2026_08_11_sync_display_name_trigger.sql).
- Panel registry (lib/registry.ts) so future panels self-register in the
  sidebar. Loud PRODUCTION banner — this DB serves live PLAYLIFE users.
- Users panel per spec §4 v1 (list + detail drawer, READ-ONLY): verify against
  the 5 test users listed there.
- Out of scope: mutations (step 3), stat cards + points ledger (step 4),
  Track B game-state sync, hosting/deploy.

When done: list changed files with a 1-line summary each, run npm run dev and
confirm login + user list + detail drawer render against live data, flag what
needs Cesar's manual steps (env values, migration apply), update STATUS.md +
IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`tournaments_mode_card`** (filed 2026-08-10, Architect) — **SPEC_READY.** Tournament mode is implemented but has no production entry point (only the dev "TOURNAMENTS (TEMP)" button on ModeSelection). Adds a fifth mode card **TOURNAMENTS** to the Home carousel + full-screen Mode Select, pure data + minimal code: new `modes.csv` row (order 3; driving_range→4, missions→5) with a new optional `rewardsTextKey` column ("Varies by tournament" text row, no coin), `case "tournaments"` → `ScreenId.TournamentSelection` in both `HandlePlayClicked` switches, id-based tagline/desc localization fallback in `ModeCardController`, 5 new LocalizationText.csv keys (EN+JP, incl. localizing the hardcoded "NO ENTRY FEE"). No scene/prefab edits. Temp button stays (capture harness clicks it). Spec: `Docs/Specs/Active/tournaments_mode_card/SPEC.md`. Kickoff below.

### Kickoff · tournaments_mode_card

```
Read Docs/Specs/Active/tournaments_mode_card/SPEC.md and implement it.

Context:
- Adds a TOURNAMENTS card to the Home mode carousel + full-screen Mode Select,
  routing PLAY to the existing ScreenId.TournamentSelection (same call the
  TOURNAMENTS (TEMP) dev button already makes). Cards are runtime-instantiated
  from ModesDatabaseCSV — this is a modes.csv row + small code diffs, NO scene
  or prefab edits.
- Touch only: modes.csv, ModeData.cs, ModesDatabaseCSV.cs, ModeCardController.cs,
  ModeCarouselController.cs, ModeSelectScreenController.cs, LocalizationText.csv
  (+ Tools → Localization → Import Text CSV, commit the regenerated table asset).
- Minimal diff. Reuse the existing card pipeline (Bind/SetState/UpdateEconomyRows)
  and the LocalizationManager.Get fallback contract. New CSV column `rewardsTextKey`
  and the localization keys exactly as specced.
- Out of scope: TournamentDevEntryButton (harness clicks it — leave it), tournament
  backend/signup fees, other modes' tagline/desc localization, prefab visuals.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`hole_scene_leftover_v3`** (filed 2026-08-10, Architect) — **SPEC_READY, GO from Cesar 2026-08-10.** 🔴 **THIRD attempt at the `Hole_NN_Geo` hierarchy leak — read the spec's "Read this first" section before touching anything.** Cesar had `Hole_06_Geo` reintroduced and left in the hierarchy **twice on 2026-08-10**, after both the Architect and Code assured him K16 (`hole_scene_leftover_v2`) had closed it. **v1 and v2 scoped the bug to the capture launchers; that was the wrong scope.** The dominant vector is the **EditMode test suite** — `RealHoleTerrainTests` opens all 18 `Hole_NN_Geo` scenes additively into the LIVE hierarchy (`:131`) and its `[OneTimeTearDown]` (`:85-91`) closes only what is still in a plain static `s_HoleCache` (`:60`), which any domain reload wipes while the scenes stay open (a cancelled run skips teardown entirely). Evidence: `LastSceneManagerSetup.txt.bak` records ShellScene + Hole_06_Geo; Editor.log shows holes 01–18 each opened 2× (= the two suite runs Cesar saw) and a `[CaptureSceneSetup] Excluding staged hole scene from snapshot: Hole_06_Geo` line ~280 lines AFTER a sweep, proving the sweep left it open; only 4 of 26 hole-scene stagers call `CaptureSceneSetup` — exactly K16's scope. Fix is two layers: (1) reload-proof, scan-based pre-clean + teardown in the fixtures, (2) an always-on `StagedHoleSceneGuard` with strict authoring protection (closes only a non-active, non-dirty `Hole_NN_Geo` while ShellScene/LabScaffold is open, never saves, EditorPrefs off-switch). Spec: `Docs/Specs/Active/hole_scene_leftover_v3/SPEC.md`. Kickoff below.

### Kickoff · hole_scene_leftover_v3

```
Read Docs/Specs/Active/hole_scene_leftover_v3/SPEC.md and implement it.
Read its "Read this first" section before writing any code.

Context:
- THIRD attempt at this bug. v1/v2 (K16) fixed the capture launchers; the real
  vector is the EditMode TEST SUITE and was never in scope. Do not re-scope to
  launchers — they are already handled and their behaviour must not change.
- Layer 1 (the actual fix): RealHoleTerrainTests opens all 18 hole scenes
  additively at :131 and its OneTimeTearDown (:85-91) only closes what survives
  in the static s_HoleCache (:60) — a domain reload wipes that dict while the
  scenes stay open, so teardown closes nothing and reports success. Replace with
  a scan-based close over SceneManager.sceneCount, AND pre-clean in OneTimeSetUp
  so a previous aborted run self-heals. Same for BakedPivotRegressionTests (:89,
  :111-118). Never save a hole scene.
- Layer 2 (safety net): new editor-only StagedHoleSceneGuard, [InitializeOnLoad],
  hooked to EnteredEditMode + afterAssemblyReload + delayCall. Closes a scene ONLY
  when ALL hold: Hole_NN_Geo, not active, not dirty, ShellScene-or-LabScaffold
  also open, not playing/compiling. Plus a manual sweep menu item and an
  EditorPrefs on/off toggle. Do NOT reference TestRunnerApi (asmdef compile risk).
- One implementation of the "is this a staged hole scene" rule, not four —
  promote CaptureSceneSetup's IsHoleGeoScene/CloseStagedHoleScenes and share them
  (mind the asmdef; delegate rather than duplicate).
- Minimal diff. ANY .unity diff is a failure of this task.
- Out of scope: re-importing holes, touching HoleGeoImporter, shrinking the
  18-hole sweep, changing CaptureSceneSetup behaviour, scene/prefab/CSV edits.

When done: list changed files with a 1-line summary each, then run the acceptance
checklist in the spec IN FULL — it requires TWO back-to-back full EditMode suite
runs each followed by a quoted GetSceneManagerSetup() dump, the mid-run guard
safety check, the interrupted-run recovery, BOTH directions of the authoring
protection test (dirty+active hole survives; clean+non-active hole is closed),
the killed-editor case, and a cat of Library/LastSceneManagerSetup.txt. A report
that claims cleanliness without quoting the dumps will be rejected — this is the
third time this bug has been declared fixed. Flag what needs Cesar on-device,
update STATUS.md + IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```

- ~~**`auto_club_selection`**~~ — **DONE 2026-08-10, Cesar-approved** (shipped `43d8a34c9`, closed `b9225442d`; folder in `Docs/Specs/Completed/auto_club_selection/`). Driver on the tee, never auto-Driver off it (manual stays allowed — K11 gate untouched), elsewhere the shortest bag club that reaches the pin; re-runs every shot; §2f green rule wins; `_autoClubSelectEnabled` toggle default ON. Details: AI_CONTEXT top block. Pointer + kickoff block deleted 2026-08-10.

- ~~**`power_gauge_target_marker`**~~ — **DONE 2026-08-10, Cesar-approved off the video** (Order 357; folder in `Docs/Specs/Completed/power_gauge_target_marker/`). Map-set landing target renders as a white radial notch on the power gauge via new `ShotController.MapTargetCarryM` (metres, re-derives on club change); also fixed the never-wired `PowerGaugeWidget` yards text (now reads `ClubContext.SelectedDistance`). Power system untouched. Details: AI_CONTEXT top block. Pointer + kickoff block deleted 2026-08-10.

- ~~**`map_view_strict_crop_indicators`**~~ — **DONE 2026-08-10, Cesar-approved off the video** (Order 355; folder in `Docs/Specs/Completed/map_view_strict_crop_indicators/`). Strict-crop invariant (viewport ground footprint ⊆ OB rect on open/pan/pinch, editor tripwire) + shared edge-clamped flag/ball indicators with docking. Pinch/two-finger-pan gestures remain on-device checks. Details: AI_CONTEXT top block. Pointer + kickoff block deleted 2026-08-10.

- ~~**`aim_camera_ball_centering`**~~ — **DONE 2026-08-10, Cesar-approved** (folder moved to `Docs/Specs/Completed/aim_camera_ball_centering/` at the 2026-08-10 close-out sweep; deviations D2 — live-widget viewport Y 0.5000, not the mockup 0.4234 — and D3 — tee clamp settles at 6.42 m on Hole 1 — ACCEPTED as-is). Ball projects exactly at the CentralBallWidget point, aim cam 8.54→3.31 m, chase cam 3.0/1.8→2.0/1.2 (ball 1.50× in flight), BotDriver now delegates via the `ApplyAimCameraAt` seam. Details: AI_CONTEXT top block. Pointer + kickoff block deleted 2026-08-10.

- ~~**`putter_aim_blue_line`**~~ (413) — **DONE, Cesar-approved 2026-08-10 off the Hole 6 video** (folder in `Docs/Specs/Completed/putter_aim_blue_line/`). Cyan putter aim line over the green grid; colour `#7AE9FF` / width 0.08 m stay provisional `[SerializeField]`s; spawned the `hole1_cup_buried_under_green` repair (`da62daf86`). Details: AI_CONTEXT top block. Pointer + kickoff block deleted 2026-08-10.

- ~~**`tree_occlusion_fade`** (added 2026-08-07)~~ — **DONE 2026-08-07, Cesar-approved.** Shipped same-day and moved to `Docs/Specs/Completed/tree_occlusion_fade/`. Cone shipped at **45°/60°** (spec's 10°/16° proved too narrow — the gate is angular, so a near trunk fills the screen while sitting outside a narrow cone; Cesar's call). Proven by real-entry-path A/B video on Hole 1: dither renders on **bark AND leaves** (§4.3 retarget confirmed live); the video also caught+fixed a post-terminal focus bug (after `SetTarget(null)`, `CurrentFocus` degraded to the finished shot's origin and aimed the cone backwards — driver now prefers the live ball transform via new `LoopCameraDirector.CurrentBall`). Two spec premises corrected in code: `Camera.main` is NOT the gameplay camera during a hole (resolve by `ChaseCamera` component), and `_shotOrigin` is `(0,0,0)` during aiming. EditMode 1023/1020/0. ⚠️ `Vegetation.shader` is under gitignored `Assets/Packs/` — force-added with its .meta (pins GUID `e80a1e91…` that all 7 retargeted .mats reference). Still open on device (Cesar): dither grain at retina DPI, perf, cone feel. Report-only finding: Spruce is on `Realistic Tree` Shader Graphs (NOT the NoWind pack as assumed) and doesn't fade; absent from Holes 1/6. Notion roadmap row added 2026-08-07. Original pointer + kickoff below kept for history.

Original pointer: SPEC_READY, awaiting Cesar go. Trees blocking the camera→ball sightline fade to a faint dithered see-through window (soft cone, ~15% ghost, 0.25 s ramps), active during aim + flight. Cesar-decided 2026-08-07: window style (not whole-tree), faint ghost (not fully invisible), aim+flight. Spec: `Docs/Specs/Active/tree_occlusion_fade/SPEC.md`. Mechanism: globals-driven dither clip injected into `Custom/Vegetation` (Forward/DepthOnly/DepthNormals/GBuffer/Universal2D; ShadowCaster deliberately untouched) + new no-scene-wiring `TreeOccludeFadeDriver` (TreeWindDriver pattern, `RenderPipelineManager.beginCameraRendering`, focus from a 2-line `ChaseCamera.CurrentFocus` accessor) + one-time retarget of bark/impostor .mats from URP/Lit onto `Custom/Vegetation` (wind off) so trunks fade too. Works for terrain-system AND standalone trees because it is per-fragment, not per-instance. Step 0 inventory reports any tree on the NoWind pack shaders (known separate finding) — those won't fade here.

### Kickoff · tree_occlusion_fade

```
Task: tree_occlusion_fade — awaiting GO from Cesar (spec filed 2026-08-07).

AUTHORITATIVE SPEC: Docs/Specs/Active/tree_occlusion_fade/SPEC.md
Read it in full before touching anything; this kickoff is a pointer, not the
work definition. Update STATUS.md as you move and fill IMPLEMENTER_REPORT.md
with the spec §5 acceptance checklist.

Context:
- Fixes: trees between camera and ball hide the shot. Ship a Genshin/BOTW-style
  dithered see-through window: soft cone from camera to ball, fragments inside
  fade to ~15% dithered ghost, smooth spatial edge + 0.25 s temporal ramp.
- STEP 0 FIRST (spec §3): inventory tree materials/shaders on Hole 1 + 6 and
  verify the premises (leaves = Custom/Vegetation in
  Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader; bark/impostors =
  stock URP/Lit; Spruce likely NoWind pack → report only). Find each patched
  pass's SV_POSITION input before writing shader code.
- Where to look: Vegetation.shader passes at ~196/1337/2518/2943/2139 (patch,
  identical injection, markers) — NOT ShadowCaster/Meta. New
  Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs (TreeWindDriver
  pattern, globals only). ChaseCamera.cs gets a 2-line CurrentFocus accessor.
  Bark/impostor .mat retarget per §4.3 with before/after screenshots — if a
  species shifts visibly, leave it on Lit and flag, don't chase parity.
- Minimal diff. Reuse existing systems (TreeWindDriver pattern, ChaseCamera
  focus, mapView.IsOpen gate). New tunables/toggles as specced
  (TreeOccludeFadeDriver statics incl. Disabled kill switch).
- Out of scope: whole-tree fade, NoWind Spruce shaders, device tree-sway bug,
  LOD impostor popping, non-tree occluders, shadow fading, any scene edit.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification (dither grain
+ perf + cone tuning are Cesar-on-device), update STATUS.md +
IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

- ~~**`shot_ui_translucency_glow`** (added 2026-08-07)~~ — **DONE 2026-08-07, Cesar-approved (live-directed).** Moved to `Docs/Specs/Completed/shot_ui_translucency_glow/`. Landed in 4 iterations: iter-1/2 wiring + sibling-render fix; **iter-3** root-caused "logs pass, no pixels glow" (glow scale didn't multiply ClubHandle's localScale 2.0 — animated inside an occluded rect); **iter-4** Cesar redirect: soft generated radial halo, centre-pivoted (handle's bottom pivot made it grow upward only), `haloPadding` 1.6; #98855B trial reverted to #FFC94A. Video `videos/raw_tee_idle_glow.mp4` covers 5s onset / tap-reset / modal-pause end-to-end. ARCHITECT_REVIEW.md filed (PASS, deviations accepted). Lessons: generated overlays must multiply target scale + need pixel evidence; behind-effects = lower-index siblings + explicit OnDestroy cleanup; no domain reload during capture sessions. Original pointer + kickoff below kept for history.

Original pointer: SPEC_READY, awaiting Cesar go. Shot UI: swap ball/handle translucency (handle → 100%, ball alpha mirrors the cone) + pulsating gold glow on the handle after 5 s idle, TEE SHOTS ONLY (other-button taps reset the timer, modals pause it, re-arms after every swing reset). Spec: `Docs/Specs/Active/shot_ui_translucency_glow/SPEC.md`. Two NEW components (`BallConeAlphaMirror`, `TeeIdleGlowController`), +2-line touch to `ClubHandleDragger`, no hierarchy rebuilds, `ConeAlphaController` stays the single cone-alpha writer.

### Kickoff · shot_ui_translucency_glow

```
Task: shot_ui_translucency_glow — awaiting GO from Cesar (spec filed 2026-08-07).

AUTHORITATIVE SPEC: Docs/Specs/Active/shot_ui_translucency_glow/SPEC.md
Read it in full before touching anything; this kickoff is a pointer, not the
work definition. Update STATUS.md as you move and fill IMPLEMENTER_REPORT.md
with the spec acceptance checklist.

Context:
- Part A: club handle renders 100% opaque (CanvasGroup ignoreParentGroups on
  the handle GO); ball base alpha mirrors the cone root CanvasGroup that
  ConeAlphaController drives (new BallConeAlphaMirror, read-only mirror).
  Diagnose first: confirm the handle's current translucency comes from
  inheriting the cone group's ConeIdleAlpha.
- Part B: new TeeIdleGlowController on the ClubHandle GO — gold pulse after
  idleGlowDelay (5 s, unscaled) when GameSession.TurnCount == 1 && ShotState.Idle
  && !AnyOverlayOpen && !mapView.IsOpen. Other-button pointer-down calls
  NotifyOtherInteraction() (timer reset); modals hold timer at 0 (restart on
  close); re-arms every idle; disarms once the shot fires. Glow raycastTarget
  = false. VERIFY TurnCount increment timing (spec NOTE) before trusting the gate.
- Minimal diff. Reuse existing systems: ConeAlphaController (don't touch),
  ClubHandleDragger (+1 ref, +1 call), OtherButtonsFader.AnyOverlayOpen,
  GameSession. New params/toggles as specced (debugLegacyTranslucency,
  debugDisableIdleGlow).
- Out of scope: flick gate/aim lock/cone sizing, non-tee glow, hierarchy
  rebuilds, putt ghosting.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- ~~**`map_view_playable_area`** (added 2026-08-06)~~ — **DONE 2026-08-07, Cesar-approved.** Shipped and moved to `Docs/Specs/Completed/map_view_playable_area/`. Single file (`MapViewController.cs`) + tests, **zero scene edits**, `MapViewCaptureDriver` unmodified. Landed in four passes, each steered by Cesar on the previous one: **354** diagnose (neither suspected branch fired — the OB rect loads fine; the Order-353b fit filled the width *at the ball's row* with nothing constraining the far side, and the reference screenshot is **Hole 1, not Hole 5**) + camera on the hole axis + show-region fit + mountain-ring hide + pan/zoom clamps; **354b** frame the playable footprint (OB-mask in-bounds hull) instead of the bounding rect; **354c** off-tile ground stays GREEN and the fit becomes ball+flag only, zoomed as tight as they allow; **354d** camera yaw snapped to the playfield axis so the field renders upright (near/far edge Δy 0.148/0.081 → **0.000/0.000**). **K2 `map_view_bottom_anchor` ABSORBED — its block is DELETED from this file.** P-010 stays fixed-by-construction; P-008 closes inverted (the default view IS the zoom-out stop). EditMode 1005/0. Report: `Docs/Specs/Completed/map_view_playable_area/IMPLEMENTER_REPORT.md`. **Two open items handed back, neither blocking:** `_heroTiltDeg` is serialized `70` on the LabScaffold instance (spec asked 80; 90 would also remove the perspective trapezoid on the playfield rectangle — one Inspector field, untouched because the spec bans scene edits), and the on-device pinch / two-finger-pan gestures are unexercised (no Touchscreen in the editor harness — the clamp math is unit-tested and wired, but that gesture is the one path that could still reveal the outside world). Kickoff text below is kept for history; note that §4.2/§4.3 of it were superseded by 354b–d.

### Kickoff · map_view_playable_area — TellCode (historical — see the DONE note above)

```
Task: map_view_playable_area — GO from Cesar 2026-08-06.

AUTHORITATIVE SPEC: Docs/Specs/Active/map_view_playable_area/SPEC.md
Read it in full before touching anything; this kickoff is a pointer, not the
work definition. Reference screenshot in the spec header (map view: hole tile
tiny + rotated, off-course green + mountain ring visible — the defect).
Update STATUS.md as you move and fill IMPLEMENTER_REPORT.md with the spec §5
acceptance checklist.

FILE: Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs (single file;
tests in Assets/Scripts/Gameplay/Tests/MapViewAimingTests.cs). Parallel-safe
with the Hole 6 task — no shared files.

Shape of the work (details + line refs in the spec):
0. DIAGNOSE FIRST (spec §3): open the map on the screenshot hole in the
   editor, read the existing "[MapView v2] Width-fill:" logs, and report which
   branch produced the mess (OB-rect load fail vs degenerate solve) BEFORE
   coding. If TryGetObRect is failing, fixing the loader is in scope.
1. Camera axis = ball→flag hole axis, NOT live aim yaw (§4.1). Aim line keeps
   rotating on screen via AimDirection2D() — iter-33 behavior preserved in the
   aim line, not the camera.
2. Frame the remaining hole: OB rect clipped behind the ball (≤5-vertex
   polygon), generalize the existing bisection solvers, near edge flush at
   screen bottom = K2 verbatim (§4.2). Runs in Open() before frame 1 (P-010).
3. Hard-hide the outside (§4.3): name-hide MountainBackdrop/Backdrop/Ring via
   the existing _hiddenObjects machinery + dynamic far clip. Frame-debug any
   remaining off-tile mesh and add its name — do NOT guess names.
4. Clamp pan focus to the OB rect; cap zoom-out in ALL paths (§4.4).

BANNED-list at the top of the file stays banned (no RT/RawImage/uvRect).
Do not regress: P-006, P-007, P-009; MapViewCaptureDriver compiles unmodified.

VERIFY (spec §5): Holes 1 / screenshot-hole / 6, tee AND green-side lie, aim
±90°, pinch both stops, pan to all four edges — mountain ring / backdrop /
off-tile ground NEVER visible; screenshot each hole in the report. Ball
bottom-anchor flush on long + short hole (K2 check). SHOOT close + aim
write-back + invariant JSON unchanged. Editor-verifiable.
```

- ~~**`landing_surface_banner`** (added 2026-08-06)~~ — **DONE 2026-08-06, Cesar-approved.** Shipped and moved to `Docs/Specs/Completed/landing_surface_banner/`. Landing-surface banner on ball settle as a runtime clone of the 1v1 TurnBanner (Figma 4094:26052), EN+JP `LANDING_*` rows, `VersusMatchController.AwaitShot` sequencing, LabScaffold `[Session]` wiring. Kickoff text below is kept for history.

### Kickoff · landing_surface_banner — TellCode

```
Task: landing_surface_banner — GO from Cesar 2026-08-06.

AUTHORITATIVE SPEC: Docs/Specs/Active/landing_surface_banner/SPEC.md
Read it in full before touching anything; this kickoff is a pointer, not the
work definition. Reference render: the spec folder's reference/ PNG
(Figma 4094:26052). Update STATUS.md as you move through the pipeline and
fill IMPLEMENTER_REPORT.md with the spec's acceptance checklist.

Shape of the work (details + code skeleton in the spec):
1. Assets/Localization/LocalizationText.csv — append the 8 LANDING_* rows
   exactly as specced (EN caps / JP). Importer auto-runs on play mode; commit
   the regenerated LocalizationTextTable.asset.
2. NEW Assets/Scripts/Physics/Viewer/LandingBannerController.cs — subscribes
   BallSM.OnShotComplete, maps EndSurface/OBReason → key, runtime-clones the
   existing TurnBanner (scene fileID 1436714829) and calls Show(). Do NOT
   rebuild the banner or edit TurnBannerWidget.cs — the clone IS the visual
   spec (white text inherited; set no colors in code).
3. Golfin.Physics.Viewer.asmdef — add "Golfin.Localization" reference
   (VERIFIED missing; asmdef refs are not transitive).
4. LabScaffold.unity — add the component to [Session], wire _templateBanner →
   TurnBanner. Scene diff must be ONLY that. ⚠️ Known trap: saving the scene
   may sweep in stale MatchMakingModal prefab-override reconciliation (K14
   side-finding) — check git diff and revert any such drift out of the commit.
5. VersusMatchController.cs — bounded AwaitShot() edit only (wait while
   LandingBannerController.IsBannerVisible, cap 2.5s) so landing banner and
   OPPONENT'S TURN never stack.

Suppression rules (locked): versus + ActiveIndex != 0 → no banner; InCup,
Tee, CartPath → no banner. Sand AND BunkerLip → BUNKER. OB: Water → WATER,
OutOfBounds/ExitedWorldBounds → OB.

VERIFY (editor — Lesson O, visual not just dispatch): land on fairway/green/
rough/bunker + water + boundary OB, describe what the banner visually did;
one landing in JP; 1v1 run — bot shots silent, human shot banner → turn
banner sequential; InCup shows no banner and Hole Complete flow unchanged.
Console clean.
```

---

## 📋 PENDING KICKOFFS — 2026-08-04 batch

Paste any block below into Code as-is. Produced by the Architect during the 2026-08-03/04 sessions; grounded against source at time of writing. Delete a block (and log it in CURRENT STATE) when its task closes.

**Sequencing constraints:**
- ~~`nav_bar_edge_gaps` BEFORE `safe_area_top_bar`~~ — SATISFIED: `nav_bar_edge_gaps` (K4) landed 08-03 (`49825e867`); `safe_area_top_bar` (K7) is in flight on top of it.
- `tree_wind_device` verification is DEVICE-ONLY (sim false-passes it — measured, report §11). `arrow_speed_retune` and `safe_area_top_bar` are editor/sim-verifiable. `ob_recovery_fixes` (K10) is EDITOR-verifiable — state-machine logic; the camera wedge repros in the editor with a mouse.
- ~~`ui_frame_pacing` (K9) before `arrow_speed_retune` (K6) locks~~ — MOOT: both CLOSED 2026-08-05 per Cesar (`7380baf67` / `cd0ef6ed4`). Reality inverted the intended order (F13 locked at 30 fps): if arrow feel reads differently at 60 fps on device, `arrow_speed_retune` reopens as a new row — F13 stays the record.
- ~~`club_selection_green_gate` (K11) may run IN PARALLEL with K10~~ — **K11 CLOSED 2026-08-05** (`066df31f2` gate + `efa681acb` the deferred §2f-after-reposition item, which K10's merge unblocked). Both shipped; see the K11 block below, including the process scar where K10's close-out swept K11's in-flight lines and briefly broke `main`.
- `matchmaking_scan_pacing` (K12): queued AFTER K11 per Cesar. Single file (MatchmakingModalController.cs), no overlap with K10/K11 — technically parallel-safe if the queue frees up. ⚠️ NO ShellScene edit: the modal's tunables are scene-serialized (K7 is mid-flight in that scene); K12 uses new serialized fields so code defaults take effect without touching the scene. EDITOR-verifiable.
- ~~`loading_bar_inset` (K14): ShellScene YAML edit, gated on `safe_area_top_bar` (K7) freeing ShellScene~~ — **CLOSED 2026-08-05** (`bae5386f3`, SCENE route not the code shim). Gate lifted when Cesar landed K7 (`d680198b3`) mid-task. Isolated one-line diff as designed. **Two kickoff premises were wrong — corrected here:** (1) the bar was NOT at zero inset — `Track` already carried `sizeDelta.x -48` (24 units/side), so `-16` on the root took it 24→**32**/side, not 0→8; (2) the UNITS NOTE was wrong about the canvas — the loading screen lives under the **ScaleWithScreenSize** canvas (ref 1170×2532, match-width, `m_UiScaleMode: 1`), NOT ConstantPixelSize. ConstantPixelSize was K4's *nav-bar* canvas — a different Canvas object. Consequence: 1 canvas unit = 1 physical px on a 1170-wide panel = ⅓ point, so the old 24-unit inset was already ≈8 **points**; the "if Cesar meant points, dial ~24" advice was derived from the wrong canvas and should not be reused. Editing `Track` directly (the rect that actually owned the inset) would have desynced `ProgressText`, which carries its own `-48` — the root is the correct single dial.
- ~~`app_identity` (K15): ProjectSettings.asset + one-shot PlayerSettings icon call — NO scene; parallel-safe~~ — **CLOSED 2026-08-05** (`66ac68575` then `7a63f7c2f`). Parallel-safety held: two commits, both scoped to ProjectSettings.asset + docs + the new icon asset; no scene touched, so the K14 side-finding (stale `MatchMakingModal` overrides reappearing on any ShellScene save) was never triggered. The K3 conflict window never opened. **The kickoff's name premise did not survive contact:** `Golfin: The Invitational` shipped first, and the springboard truncated it to `Golfin:TheI…` — iOS collapses the inter-word spaces BEFORE truncating, so it reads as one run-together token, worse than the kickoff's predicted `Golfin: The I…`. Cesar took the documented dial: **productName is now plain `Golfin`**, full title lives on the icon + store listing. Icon also swapped mid-task to `Assets/Icons/Golfin-Icon2.png` (shield + "The Invitational" script) — it DOES carry an alpha channel unlike icon1, but the channel is fully opaque (min=max=255) and Unity strips it generating the icon set (built `AppIcon60x60@2x.png` reports `hasAlpha: no`), so the store no-alpha rule is satisfied; do not "fix" it. Bundle id / signing untouched as specced.
- ~~`hole_scene_leftover` (K16)~~ **CLOSED 2026-08-05** (`a6b022642` + `1372da34b`) — see the K16 block. Shipped Option B (host injected at EnteredPlayMode, never serialized) so no launcher writes LabScaffold at all; setup snapshot/restore closes staged hole scenes unsaved. Extended to SmokeRunner2fMenu beyond the kickoff (flagged there). ⚠️ Found a SEPARATE pre-existing bug: the 2e OB capture no longer reaches OB and fails silently — needs its own task.
- ~~`boot_loading_screen_removal` (K13): parallel-safe with everything open~~ — **CLOSED 2026-08-05** (`d3bf00026`). The parallel-safety prediction held: the commit used an explicit 2-file pathspec and left K7's ShellScene/SafeAreaFitter/PersistentUIManager and K12's MatchmakingModalController drift untouched (the K10→K11 sweep scar did NOT repeat). SHARED LoadingScreenController never edited, as designed.

### K3 · build_stamp_hardening — Surgical (defect B AMENDED 2026-08-04)

```
Task: build_stamp_hardening — three defects in the shipped build_version_stamp.

FILE (all three): Assets/Editor/BuildStampGenerator.cs

The implementation is correct and working; do NOT restructure it. These are
three bounded fixes. Nothing else in the file needs to change.

────────────────────────────────────────────────────────────────
DEFECT A — the dirty check is blind to untracked files
────────────────────────────────────────────────────────────────
ComputeStampString() derives `dirty` and `diffHash` from `git diff HEAD`, which
reports modifications to TRACKED files only.

When a NEW .cs file is added and not committed — routine during implementation
work — the tree reads clean, no "+diffHash" is emitted, and two builds either
side of that addition differ only by timestamp.

FIX: fold untracked files into the hash input. `git status --porcelain`
covers both modifications and untracked files in one call, or add
`git ls-files --others --exclude-standard` alongside the existing diff.
Hash the combined output.

VERIFY A: build, note the stamp. Add a NEW .cs file WITHOUT committing. Build
again. The stamp MUST now carry a +diffHash that was not there before. Then
edit an EXISTING tracked file without committing and confirm the hash changes
again — do not fix additions by breaking modifications.

────────────────────────────────────────────────────────────────
DEFECT B (AMENDED — broader than first specced) — the restore never
persists to disk, on success OR failure
────────────────────────────────────────────────────────────────
Evidence (report 2026-08-04 §12–§13, third observed instance):
ProjectSettings.asset was left dirty with buildNumber hunks after a
SUCCESSFUL build. Assigning PlayerSettings.* updates the in-memory object
only — it does not reach disk. Additionally, OnPostprocessBuild does not fire
at all when a build FAILS.

FIX REQUIREMENTS:
- Restore the two fields (iOS.buildNumber, Android.bundleVersionCode) AND
  call AssetDatabase.SaveAssets().
- VERIFY by re-reading ProjectSettings.asset from disk after the restore and
  asserting the values match the pre-build snapshot — never trust assignment.
- Run the restore on BOTH outcomes (finally / report.summary.result check).
- Keep the narrow-restore discipline: ONLY those two fields. Never revert the
  whole file (it carries other live settings — data-loss bug).

ACCEPTANCE: git status shows ProjectSettings.asset CLEAN after (a) a
successful build AND (b) a deliberately failed build. Both, not either.

────────────────────────────────────────────────────────────────
DEFECT C — the upload guard blocks ordinary iteration builds
────────────────────────────────────────────────────────────────
After GOLFIN/Build/Mark Current Commit As Uploaded runs at commit N, the
`buildNumber <= lastUploaded` check throws for EVERY build at commit N — all
platforms, all profiles, including Dev-iOS.

The guard's purpose is protecting App Store Connect upload slots, which only
store-bound builds can burn. As written, after a TestFlight upload Cesar
cannot rebuild Dev-iOS without inventing a dummy commit.

FIX: scope the guard to store-bound builds only. Skip it for development /
iteration builds — BuildOptions.Development via report.summary.options is the
cheapest discriminator; prefer a more explicit profile check if available.
Keep the guard's failure message as-is when it DOES fire. Non-store builds
still write and bake the build number normally; only the refuse-to-build
check is skipped.

VERIFY C: run Mark Current Commit As Uploaded. Without committing anything, a
Dev-iOS build must SUCCEED. A store-bound build at the same commit must still
FAIL with the existing message.

DO NOT:
- Change the display string format.
- Touch the gitignore entries for build_stamp.txt.
- Move the guard file (Docs/Versioning/last_uploaded_build.txt is deliberately
  outside any Build/ dir — .gitignore's "[Bb]uild/" rule would untrack it).
- Alter the git-executable fallback list or the stderr drain — both correct.
```

### K5 · tree_wind_device (smoke #6) — TellCode (verification AMENDED per report §11)

```
Bug: trees do not sway on a physical iPhone. Wind animation works in the Unity
editor AND in the iOS Simulator build (measured, report §11 — 54–57% canopy
pixel change with bit-identical controls). This is a DEVICE-TARGET issue.

⚠️ VERIFICATION IS DEVICE-ONLY. The sim build targets iphonesimulator, the
device build targets iphoneos, and different SDKs strip different variant
sets. The sim's trees sway, so any sim check of this fix is a guaranteed
false PASS.

⚠️ FORWARD REQUIREMENT — factor into the fix choice:
A quality-tier setting that DISABLES tree wind on low-end devices is a known
upcoming requirement (`9a — Quality settings presets`, Order 900, already
updated with this dependency). Runtime toggling needs BOTH the _WIND-on and
_WIND-off variants present in the shipped build. That constrains the fix.

STEP 0 — IDENTIFY WHICH SHADER THE HOLE TREES USE. Two packs exist:
  Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader
    → "Custom/Vegetation", Amplify-generated, URP, HAS wind.
  Assets/Packs/Mobile_Tree_Bundle/Shaders/Standard/*NoWind.shader
    → literally NoWind, built-in-RP Standard shaders in a URP project.
Select a swaying tree in a hole scene, report exact material + shader. If any
hole trees are on the NoWind shaders, that is a separate finding — report it.

HYPOTHESES (re-ranked by the §11 measurement — the sim IS a real il2cpp iOS
player build off the same scenes/settings, and its wind survives):

H1 — DEVICE-SDK shader variant stripping. FRONT-RUNNER.
  Vegetation.shader gates wind behind:
    [Toggle(_WIND)] _Wind("Wind", Float) = 1
    #pragma shader_feature _WIND        ← shader_feature, NOT multi_compile
  shader_feature variants ship ONLY if a material has the keyword enabled at
  bake time; the editor compiles on demand, so it always works there.
  _WIND is a GLOBAL shader_feature (neighbours are shader_feature_local).
  CHEAP DISCRIMINATOR FIRST: open the tree material's .mat YAML and read
  m_ShaderKeywords. If _WIND is absent, stripping drops the variant.
  FIX OPTIONS — report the tradeoff, do NOT pick unilaterally:
    a) Serialize _WIND enabled on the shipping material.
       → ON variant only. NO runtime toggle. Fails Order 900.
    b) Always Included Shaders / ShaderVariantCollection. → same limitation.
    c) shader_feature → multi_compile _WIND. Both variants ship; a single
       Shader.DisableKeyword("_WIND") then kills tree wind game-wide — the
       exact hook Order 900 needs. Costs build size; edits a third-party pack
       file. MEASURE the build-size delta of (c) vs (a)/(b) and report it as
       a number. If (a)/(b) is chosen, state that Order 900 still needs the
       shader change later.

H2 — static batching on iPhone: WEAKENED HARD by §11 (the same batching
  settings bake into the sim build, which sways). Check only if H1's
  discriminator comes back clean. Lead if needed: an "iPhone static-batching
  entry" was once observed as uncommitted ProjectSettings churn (AI_CONTEXT
  housekeeping 2026-07-29).

H3 — quality tier / LOD bias: WEAKENED but alive pending ONE unverified fact —
  whether the sim build resolves to the same quality tier as device. One
  Debug.Log of QualitySettings.GetQualityLevel() through the tier-2 sim loop
  settles it cheaply before any H3 time is spent. If H3 IS the cause, it
  likely also explains the two dark-green LOD-impostor spheres seen on Hole 1
  — say so, it merges two open issues AND Order 900's LOD-bias tier setting.

CONSTRAINTS:
- Do NOT edit hole scenes or ShellScene (no merge driver; Order 429 queued).
- Do NOT modify Assets/Packs/ third-party files without reporting first
  (includes fix option c).
- Do NOT re-author tree materials or replace the pack.
- Do NOT implement the quality tier here — Order 900 owns it. This task only
  preserves the ability to build it.

VERIFY: physical iPhone, trees visibly sway on Hole 1. Report which
hypothesis was correct and the evidence. If fix (c) landed, also confirm
Shader.DisableKeyword("_WIND") at runtime stops the sway — that proves the
Order 900 hook exists.
```

### K7 · safe_area_top_bar (smoke #2) — TellCode · RUN AFTER K4 · AMENDED 2026-08-04 (scene + PersistentUIManager.cs, Cesar-approved Option A)

```
Task: safe_area_top_bar — tickets counter is eaten by the Dynamic Island on
iPhone 14 Pro Max. Smoke issue #2.

SCOPE (RULING 2026-08-04): scene-only is IMPOSSIBLE — show/hide and chrome
logic in PersistentUIManager.cs couples to the current hierarchy. Approved
plan = Option A: ShellScene.unity + PersistentUIManager.cs, ONE isolated
commit. Two new serialized refs approved: topBarContent, bottomNavContent.

THE COMPONENT ALREADY EXISTS — do not write a new one:
Assets/Scripts/UI/Core/SafeAreaFitter.cs (GolfinRedux.UI.Core).
[ExecuteAlways], polls Screen.safeArea, converts to anchors.

⚠️ THE TRAP — inset the CONTENT, not the bar BACKGROUNDS:
- Bar background art: FULL-BLEED on the existing roots (topBarPanel /
  bottomNavPanel), extending under the Dynamic Island / into the
  home-indicator zone.
- Bar CONTENT: canvas-level "SafeArea" node (stretch anchors, zero offsets,
  SafeAreaFitter attached) containing TopBarContent + BottomNavContent;
  re-parent the content sub-objects into those.

CODE TOUCHPOINTS — four, not two. All in PersistentUIManager.cs:
1. ShowTopBar(bool) / ShowBottomNav(bool): toggle BOTH the root panel AND
   the matching content ref. Content-only inverts the bug (Splash/Loading
   would show floating backgrounds); root-only strands the chrome (the bug
   that forced this amendment). Null-guard the new refs.
2. SetTopBarChromeVisible: retarget the child loop from topBarPanel to
   topBarContent. UsernameText MOVES INTO topBarContent (it is top-bar
   content and must sit inside the safe area — account-screen titles would
   otherwise be under the island). The skip-by-name UsernameText logic
   carries over unchanged, so ShowAccountTitleBar keeps working.
3. ApplyDemoTopBarTrim: currently topBarPanel.transform.Find(
   "RewardPointsBackground") — after the reparent this returns null and
   NO-OPS SILENTLY, regressing demo_build_slice §3.4 (demo would show RP
   chrome). Retarget the Find to topBarContent.
4. EnsureTicketPill: resolves via ticketCountText.transform.parent — it
   survives IF RewardPointsBackground, TicketIcon, ShopPlusButton and the
   count text all move together as SIBLINGS into TopBarContent. Keep that
   cluster intact; verify the pill still spawns (its center-anchor math
   assumes the cluster centers as the bar stretches).

SURVIVES UNTOUCHED (do not modify): HideIfScreenBlocked and every serialized
Button/Text/Image ref — Unity object refs, not paths. The two Find calls
above are the only path-based lookups in the file.

SCENE EDIT RULES: isolated commit (ShellScene.unity + PersistentUIManager.cs
together, nothing else), minimal diff, diff the scene YAML before committing,
revert unrelated drift. No merge driver yet (Order 429 queued).
PersistentUIManager's topBarPanel / bottomNavPanel serialized refs must
survive — re-parent CHILDREN only, never rename/move the panel roots.

SEQUENCING: run AFTER nav_bar_edge_gaps (K4) — same bars, same scene; K4's
outcome determines the bars' final geometry.

SCOPE LIMITS: shell canvas only. In-game HUD (player card / hole info) also
crowds the notch but was NOT the reported issue — CHECK visually and report,
don't fix. Build stamp handles its own inset; leave it alone. Other screens'
notch-kissing content = the deferred full inset pass, its own row.

VERIFY — Simulator VALID (safe-area class; ShellScene ships in build data →
tier-1 data swap covers iteration):
- Sim (iPhone 14): tickets pill fully below the notch; NO blank strip
  between notch and top-bar background; bottom nav icons clear of the
  home-indicator band; backgrounds still reach all screen edges.
- Show/hide matrix — every row, this is where the amendment bites:
  Logo/Splash/Loading → NO bar backgrounds AND no chrome visible.
  Account/login screens → banner + centered title ONLY (chrome stripped,
  title visible and inside the safe area).
  Home → full bars, chrome restored.
  In-hole → shell bars fully hidden.
  Demo define (GOLFIN_DEMO, PointsEnabled=false) → RP chrome hidden
  (touchpoint 3 regression check).
- Editor Game view at 16:9: layout unchanged (safe area is zero there — any
  visible difference is a regression).
- Final confirm on Cesar's iPhone 14 Pro Max (taller Dynamic Island than the
  sim's notch, and it is the reporting device). One launch, Cesar's eyeball.
```

### K8 · housekeeping_batch — Surgical, four bounded items

```
Housekeeping addendum — four bounded items, no investigation:

1. .gitignore: add the recurring iOS-export residue:
   Assets/Resources/PerformanceTestRunInfo.json (+.meta),
   Assets/Resources/PerformanceTestRunSettings.json (+.meta),
   Assets/packages-merged-link/
   Verify a fresh export then leaves git status clean.

2. Orphan hygiene → IOS_SIMULATOR_LOOP.md: il2cpp leaks a hung child on EVERY
   build, successful ones included (34 reaped, report §13 addendum). Add a
   post-session check — pgrep -fl il2cpp; reap by start time (ps -o lstart=),
   NEVER by pid comparison (pids wrap). Also: it is the FIRST check if a
   headless build ever fails again.

3. IOS_SIMULATOR_LOOP.md, two missing rules:
   - The §13 rule verbatim: never BuildPipeline.BuildPlayer via MCP
     script-execute (10× retry = build storm); fire-and-forget via
     EditorApplication.delayCall / menu item + marker file. The doc explains
     tier-2's append re-export but not how to invoke it without the storm.
   - Under the standing rule: "The bootstrap/seed requirement is a local
     workaround, not pipeline design — standard CI runs xcodebuild cold on
     fresh exports. The §§1–7 anomaly must be root-caused before any CI
     adoption (testflight_distribution may eventually want CI)."

4. For the record, no action: the §13 orphan hypothesis is logged in
   AI_CONTEXT as the first cheap check on recurrence. Investigation closed.
```

### K10 · ob_recovery_fixes (smoke #9) — ✅ DONE 2026-08-05 (Cesar-approved)

**Shipped:** `90dd574ff` (camera + drop rule) · `ed65f5726` (capture Y-flip fix + harness).
Folder moved to `Docs/Specs/Completed/ob_recovery_fixes/`; clip in `Docs/Reports/Media/2026-08-05_ob_recovery_fixes.mp4`.

- **Part A (symptoms 2+3):** `OBFreeze`/`CupZoom` are focus-based modes with no null-target
  early-return, so they kept running through the next aim phase and overwrote the pin-facing
  re-aim + orbit drag (both Chase-gated). Director now exits them → `Chase` on entry to `Aiming`.
  **Same-class finding: `CupZoom` was broken identically** (every hole-out wedged the next
  aim phase) — fixed in the same conditional.
- **Part A follow-up (Cesar ruling):** OB no longer cuts to an aerial view — `ModeMap[OB]` = `Chase`,
  pivot teleport + `ComputeOBFreezePivot` deleted; the camera just stops chasing (0.00 m drift, on video).
  A ground-level "horizon settle" variant was built, reviewed and **reverted at Cesar's call** — the
  plain freeze is what ships.
- **Part B (symptom 1, Cesar ruling = real golf):** boundary OB is **stroke and distance**
  (drop at previous origin → first-shot OB re-tees); water keeps last-dry-touch via the untouched
  `OBDropResolver.Resolve`. **Known approximation:** a long carry over land that splashes drops at the
  last *bounce*, which can sit behind the true crossing point — refining that is a separate design row.
- **Tests:** 250 pass / 0 fail, incl. an end-to-end test on the real `ChaseCamera` + Director + SM.

**Spun out of this task — permanent capture fix (`ed65f5726`):** mid-recording `ScreenCapture`
reads were flipping Recorder frames on Metal (proven 1:1 by frame-pts↔capture-log correlation).
`CaptureCore.RecordingActive` now hard-refuses every snap while recording; stills are extracted
from the finished mp4. **Rule for all future capture bots: never snap a still during a recording.**

<details><summary>Original kickoff (historical)</summary>

```
Task: ob_recovery_fixes — three symptoms on the shot AFTER an OB; one camera
root cause + one design-rule change. Smoke #9 (device, Hole 1, first-shot OB
into the right tree line; build 10fc22e+595c, 08-05 09:29).

SYMPTOMS (Cesar, device):
1. Ball not returned to the tee after a first-shot OB (dropped at green edge).
2. Aiming line points BACKWARDS (toward the tee).
3. Camera cannot be dragged sideways during that aim phase. The next shot
   fires → everything recovers.

NOT K1. camera_drag_touch_origin (`bb59d32dd`) is fixed + device-verified;
normal-shot drag works. Do NOT touch InputSystemSource or the orbit input
read.

────────────────────────────────────────────────────────────────
PART A — camera wedge after OB (symptoms 2+3, ONE root cause,
source-verified by the Architect — re-verify the chain, then fix)
────────────────────────────────────────────────────────────────
Chain, all in Assets/Scripts/Physics/Viewer/:

LoopCameraDirector.HandleStateChanged:
  →OB: ResetToOrigin(LastShotOrigin,…) ← _shotOrigin = the TEE on shot 1
       SetOBFreezePivot(pivot)         ← OB crossing point
       ModeMap[OB] = Mode.OBFreeze
       SetTarget(null)                 ← terminal clear; its comment claims
                                         "aim owner takes over via ChaseCamera
                                         LateUpdate null-target early-return"
  →Aiming (from ReArm): ModeMap[Aiming] = null = "leave whatever was set"
       → mode STAYS OBFreeze through the entire next aim phase.

ChaseCamera.RunLateUpdateLogic: the null-target early-return exists ONLY for
Chase/GroundLevel. OBFreeze keeps running every frame with
focus = _target ?? _shotOrigin = THE TEE:
  desiredPos = _obFreezePivot (out at the OB crossing)
  desiredRot = LookRotation(tee − pivot)  ← camera looks BACKWARDS
LateUpdate therefore overwrites, every frame:
  – the pin-facing re-aim (ApplyCameraYaw committed in
    PhysicsLabController.RepositionBallWithLookDir)   → symptom 2
  – the orbit drag written in Update (HandleCameraOrbit) → symptom 3
Why AtRest shots are fine: ModeMap[AtRest] = Chase → null-target early-return
→ aim owner runs. The Aiming=null entry predates OBFreeze (§2b); OBFreeze
broke the invariant that terminal modes are inert during aim.

FIX (director-side, minimal — respect the single-writer rule; do NOT
restructure ChaseCamera):
In HandleStateChanged, on change.Next == BallState.Aiming:
  if (setter.CurrentMode == ChaseCamera.Mode.OBFreeze)
      ApplyMode(ChaseCamera.Mode.Chase);
Chase + null target = dormant → the aim camera owner takes the view back.
⚠️ Do NOT blanket-map Aiming→Chase: the null entry protects putter mode
(EnterPutterMode sets GroundLevel; re-arms happen while putting).

SAME-CLASS CHECK (report; fix only if same-shape): InCup → CupZoom is also a
pivot/focus mode with no null-target early-return. If the NEXT hole's first
aim phase can run with mode still CupZoom (does anything reset it before
SetupAtTee?), it wedges identically. Check and report; if broken, include
CupZoom in the same conditional exit.

────────────────────────────────────────────────────────────────
PART B — drop rule (symptom 1): Cesar RULING 2026-08-05 = REAL GOLF
────────────────────────────────────────────────────────────────
Current behavior: OBDropResolver.Resolve drops at the LAST in-bounds terrain
hit; falls back to _lastShotOrigin only when no safe hit exists. Deliberate
§2e design — now ruled against.

New rule (real golf):
– Boundary OB (result.OBReason != Water): STROKE AND DISTANCE — drop at the
  previous shot origin (_lastShotOrigin). First-shot OB → back on the tee.
– Water: KEEP current behavior (last dry touch ≈ lateral relief near entry,
  never nearer the hole). KNOWN APPROXIMATION: a long carry over land that
  splashes drops at the last BOUNCE, which can sit well behind the real
  crossing point. Accepted for now — note it in the report; refining to the
  actual water-crossing point is a separate design row if Cesar wants it.

Implementation: branch on OBReason at the §2e call site in
PhysicsLabController.HandleShotComplete (BallState.OB case) — water path
keeps OBDropResolver.Resolve; boundary path uses _lastShotOrigin directly.
Leave OBDropResolver itself unchanged (water still uses it). The
aim-toward-pin yaw computation stays as-is — it is correct once the camera
stops fighting it (re-tee drop → ComputeYawTowardPin(tee, pin) = down the
fairway). Penalty/turn arithmetic: DO NOT touch — TURN counting is already
correct (Cesar's TURN 3 after a first-shot OB = shot + penalty + 1).

CONSTRAINTS:
– No changes to ChaseCamera internals beyond (at most) the CupZoom finding;
  no changes to BallStateMachine / ReArm semantics; keep the OB hold beats
  (water 1.2 s, boundary 2.0 s) — shipped behavior.
– Run the Physics test assembly; add a test for the boundary→origin branch
  wherever the OB drop is covered (NextShotHandoffTests neighborhood).

VERIFY — EDITOR-VALID (state-machine logic, not device-only):
1. Editor: fire a deliberate boundary OB (ObBoundaryCaptureBot menu or
   manual). After the drop: ball at the previous origin, camera behind the
   ball facing the pin, aim line forward, mouse orbit drag WORKS. The drag
   check is the wedge regression test — it FAILS on HEAD today.
2. Water OB: ball still drops at last dry touch; camera/aim/drag equally
   healthy afterward.
3. Device: one boundary-OB repro on iPhone for confidence (drag is the
   K1-verified path; expected to just work once LateUpdate stops fighting).
4. Report the CupZoom same-class finding either way.
```
</details>

### K11 · club_selection_green_gate — ✅ DONE 2026-08-05 (Cesar-approved)

**Shipped:** `066df31f2` (selector gate) · `efa681acb` (the deferred §2f-after-reposition item).
TellCode-dispatched — no `Docs/Specs/` folder for this one.

- **Gate at the UI layer, reusing §2f — not a second rule.** `EnterPutterMode`/`ExitPutterMode`
  publish to `ClubSelectionBroadcast` (same static-bus/asmdef-isolation precedent as `Raise`);
  eligibility is one pure `IsSelectable(labClubIndex, putterLabClubIndex, inPutterMode)` shared by
  `Populate` and `Scroll`. Reading the same decision that flipped the club is what stops the gate
  and the auto-switch from fighting the player. Bots / map view / debug stay **ungated**;
  `SetClub`, `PutterModeSurfaceController`, `ClubContext.RequestSelection` untouched.
- **Shipped disabled, not hidden** — alpha 0.5 + non-interactive (ball-selector precedent), with
  `CanvasGroup` added to runtime clones so no prefab is dirtied. Every commit path guarded; `Scroll`
  steps over ineligible clubs and returns `bool` so the hold-scroll coroutine exits instead of spinning.
- **Needed beyond the brief:** `Enter/ExitPutterMode` only fire on a club *change*, so a boot-time
  publish in `Start()` was required or the gate would have been inert for the whole first hole.
  `IsSelectable` fails open on an unpublished index so it can never soft-lock the selector.
- **Deferred item — now done (`efa681acb`):** hooked the §2f re-decide into `RepositionBallWithLookDir`,
  the single seam `PlaceBallAt` + both OB hold coroutines funnel through, classifying the drop point
  with the same baked classifier the sim uses for `EndSurface`.
- 🔵 **Scope correction:** K10's stroke-and-distance rule made **boundary OB self-correcting** (the drop
  returns to the previous origin, where the club was already right). Real exposure is **water relief
  crossing the green boundary**, plus `PlaceBallAt` — narrower than the kickoff implied.
- **Tests:** Gameplay 252 / Physics 257, 0 fail. `RepositionClubReDecideTests` runs against **real baked
  Hole 6 zone data** with points discovered by scanning, so a re-bake that moved the green fails it.

⚠️ **Process scar — parallel close-outs stage whole files.** K10's close-out (`90dd574ff`) staged all of
`PhysicsLabController.cs` and swept in three in-flight K11 lines, leaving `origin/main` calling a
`ClubSelectionBroadcast.SetPutterMode` that did not exist yet (CS0117 on a fresh checkout; invisible
locally because the working tree had both halves). Repaired forward by `066df31f2` — no history rewrite,
`90dd574ff` was already pushed. **Rule: a publisher/consumer pair split across two asmdefs must be
committed together.** CLAUDE.md Rule 12 guards the reverse direction only; nothing guards this one.

<details><summary>Original kickoff (historical)</summary>

```
Task: club_selection_green_gate — the putter is selectable ONLY on the
green, and non-putter clubs are NOT selectable on the green. Player-facing
selection gate (Cesar, 2026-08-05).

CONTEXT — the rule already exists; the UI just doesn't enforce it:
§2f auto-switch (PutterModeSurfaceController.DecideTargetClub, called from
PhysicsLabController.HandleShotComplete AtRest branch) already flips to the
putter when the ball rests on Green and back to _lastNonPutterClubIndex when
it rests elsewhere. The classification is GREEN-STRICT: SurfaceType.Green
only — GreenCollar counts as OFF-green. The gate must reuse THIS
classification — do not invent a second rule; if the gate and §2f disagree
they will fight the player.

WHAT'S UNGATED TODAY (all paths funnel into ClubSelectionBroadcast.Raise →
PhysicsLabController.OnClubBroadcastReceived → SetClub):
1. SelectorOverlayWidget.Populate() Kind.Club — builds a selectable card for
   EVERY club in ClubContext.EquippedBag, no surface awareness.
2. SelectorOverlayWidget.Scroll(±1) — arrow buttons + hold-scroll over the
   full bag.
Both in Assets/Scripts/Gameplay/UI/ShotUI/SelectorOverlayWidget.cs.

DESIGN — gate at the UI layer, NOT inside SetClub:
Bots (BotDriver/VersusBot), map view, and debug paths call SetClub
programmatically and must stay ungated — §2f keeps the player state correct;
the defect is only that the SELECTOR lets the player override it.

IMPLEMENTATION:
a) Putt-mode flag visible to UI: PhysicsLabController.EnterPutterMode /
   ExitPutterMode are the existing single entry/exit (driven by SetClub via
   OnClubIndexChanged). Publish a static flag there that Gameplay.UI can
   read — follow the ClubSelectionBroadcast static-bus precedent (same
   asmdef-isolation reason): e.g. ClubSelectionBroadcast.InPutterMode plus
   PutterLabClubIndex (UI must not hardcode 3 and must not reference
   ShotController directly).
b) Eligibility as a pure static (testable, shared by Populate + Scroll):
   IsSelectable(labClubIndex, putterLabClubIndex, inPutterMode)
     inPutterMode  → only the putter
     !inPutterMode → everything EXCEPT the putter
c) Populate(): ineligible cards render DISABLED — grayed, non-interactive
   (match the ball-selector putter-mode precedent: alpha 0.5, no
   interaction). Guard EVERY commit path for disabled cards: the card's
   selection callback, CommitHighlighted, UpdateHoldHover (no highlight on
   disabled), EvaluateRelease returning OnCard over a disabled card → treat
   as Outside. If SelectorCardWidget makes a disabled state awkward, HIDING
   ineligible cards is an acceptable fallback — report which you shipped.
d) Scroll(): skip ineligible indices (off-green: skip the putter; on-green:
   arrows effectively no-op). Mind ArrowScrollRoutine — the hold-scroll
   coroutine must not spin trying to reach a skipped index.

⚠️ K10 OVERLAP — EXPLICITLY DEFERRED, DO NOT DO IN THIS PASS:
Repositioned balls (OB water drop, PlaceBallAt) never run the §2f decision,
so putter-mode can be stale after a drop onto/off the green. The fix (run
DecideTargetClub after reposition) lands in the SAME PhysicsLabController
region K10 is editing. Keep K10/K11 parallel-safe: SKIP it here; it is a
one-line follow-up AFTER K10 merges. Log it in your report so it isn't
lost.

DO NOT:
- Gate Bags/Inventory screens — out-of-round club management stays free.
- Touch SetClub, bots, the map-view SHOOT repurpose (iter-38 router guard),
  or ClubContext.RequestSelection semantics.
- Change §2f / PutterModeSurfaceController.

TESTS: pure-logic tests for IsSelectable (both modes). Run the Gameplay
test assembly (ShotControllerPuttModeTests neighborhood) — no regressions.

VERIFY — EDITOR-VALID:
- Off green: putter card disabled (or hidden); arrows skip it; hold-drag
  release over it commits nothing.
- On green (§2f flipped to putter): other clubs disabled; arrows no-op;
  putter still commits fine.
- Ball selector (Kind.Ball) untouched in both modes.
- Bot smoke: one BotDriver hole plays through unchanged (bots bypass UI).
- Device pass optional — pure UI logic, editor/sim sufficient.
```

</details>

### K12 · matchmaking_scan_pacing — Surgical · AFTER K11 per Cesar · EDITOR-verifiable

```
Task: matchmaking_scan_pacing — the 1v1 "FINDING OPPONENT" animation should
start FAST and decelerate before landing on the opponent (slot-machine
feel), and the total wait is too long — shorten it. (Cesar, 2026-08-05.)

MEASURED CURRENT BEHAVIOR (source-grounded):
FILE: Assets/Scripts/UI/Matchmaking/MatchmakingModalController.cs
OpponentScanRoutine cycles opponents at a CONSTANT
opponentCycleIntervalSeconds (0.3 s) for searchDurationSeconds (5 s), then
holds 0.6 s on "OPPONENT FOUND" before GameplaySceneLoader.BeginGameplayLoad.
Total wait before the load even starts ≈ 5.6 s. No easing of any kind.

⚠️ SCENE-SERIALIZATION TRAP — read before coding:
The tunables are [SerializeField] and ShellScene.unity SERIALIZES them
(searchDurationSeconds: 5 at ~line 131153). Changing the C# defaults does
NOTHING — the scene values win. And ShellScene is OFF-LIMITS right now (K7
mid-flight, no merge driver). Fix: introduce NEW serialized fields — absent
from the scene YAML, so the script defaults take effect with ZERO scene
edit. Deprecate the old two in a comment; a later housekeeping pass can
remove them + their scene entries when ShellScene is next legally edited.

IMPLEMENTATION (one file, one coroutine):
New fields (script defaults become live immediately):
  scanTotalSeconds       = 2.5f   // was 5 via scene — total cut ≈ 5.6→~3.1 s
  scanStartIntervalSeconds = 0.10f  // fast flicker at start
  scanEndIntervalSeconds   = 0.50f  // slow holds before the find
OpponentScanRoutine: replace the constant wait with a decelerating ramp —
  t = elapsed / scanTotalSeconds  (0→1)
  interval = Mathf.Lerp(scanStartIntervalSeconds, scanEndIntervalSeconds,
                        t * t)    // ease-out: fast early, slow late
  yield WaitForSeconds(interval); elapsed += interval  (accumulate the
  ACTUAL interval used — the old code added the constant).
≈ 9–10 name flips: ~5–6 in the first second, 2–3 slow holds at the end.
The deceleration lands naturally on the final opponent — finalPick already
tracks the last displayed entry; keep that mechanism untouched.

KEEP UNCHANGED:
- The 0.6 s "OPPONENT FOUND" beat (Stage C0 staging — deliberate, the modal
  hides at the FadeController midpoint; do not shorten without Cesar).
- DotCycleRoutine (status dots) — independent, fine at 0.4 s.
- Phase enum transitions (BotDriver test seam reads Phase; loop_v2 bots just
  get a faster wait — no seam change).
- GameSession seeding / MatchContext population order.

VERIFY — EDITOR-VALID:
1. Editor: open 1v1 matchmaking from the mode carousel. Scan visibly starts
   fast and decelerates; last displayed name/card == the opponent the match
   starts against; total scan ≈ 2.5 s (log elapsed at OpponentFound).
2. Cancel mid-scan still works (coroutines stop, home panels restore —
   OnHide path untouched).
3. Bot smoke: loop_v2 matchmaking-dependent bot run passes (Phase seam).
4. Report before/after totals; Cesar tunes the three fields in the
   Inspector afterward if the feel is off — they are serialized for exactly
   that.
```

### K13 · boot_loading_screen_removal — **CLOSED 2026-08-05** (`d3bf00026`, Cesar-approved)

**Outcome: REMOVED** (the branch the kickoff expected). Step-0 measurement, two baseline
runs driving the real `StartButton.onClick`, confirmed the static read exactly:

| Metric | run 1 | run 2 |
|---|---|---|
| boot init complete (AfterSceneLoad) | t=3.75s | t=3.88s |
| Splash interactive | t=9.27s | t=8.99s |
| Loading screen visible | 2.502s | 2.476s |
| click → Home total | 2.749s | 2.723s |
| `_useExternalProgress` ever true | **False** | **False** |
| max `_realProgress` | **0.000** | **0.000** |
| worst frame during Loading | 21.5ms | 17.9ms |

Zero real progress, ever. Boot init finished 5.2 s *before* START was tappable. The only
real cost behind the transition is a ~225 ms `AudioManager.PlayMusic(Main Theme)` decode
running synchronously inside `ApplyScreen(Home)` — shorter than the 0.25 s fade that
already covers it. Real work ≈ 0.23 s ≪ 2 s → remove.

**After: click → Home = 0.468–0.483 s** (was 2.72 s). The music spike is unchanged
(224.7 ms vs 230.3 ms baseline), so removal added no cost — it stopped hiding 2.2 s of
nothing. Changed files: `SplashScreenController.cs` (+ rationale/re-entry comment) and
`HomeScreenController.cs` (PLAY fallback → `Debug.LogError`). `LoadingScreenController`,
`GameplaySceneLoader`, `ScreenManager` verified byte-identical to HEAD.

Verification: (1) Logo→Splash→START→Home direct, bars/chrome correct, no flash ✅
(2) GOLFIN_DEMO — **static only**, the define is build-profile-scoped so it is inactive in
the Editor; Home is on `DemoGate.Allowed` so Loading never opens ✅ (by construction, not
by run) (3) HoleLoad gate via the real `ModeHomeCard(Clone)/PlayButton`: OPPONENT FOUND →
`BeginGameplayLoad`, `_useExternalProgress=True`, `_realProgress` 0.00→0.45→0.95→1.00 over
112 frames, 2.586 s, hands off to gameplay ✅ (4) `CreateUsername → Home` unchanged ✅

⚠️ **Adjacent knob still open (reported, not changed):** `FinishLoadingCoroutine` enforces
`minLoadingTime` as the MINIMUM for the HOLE-LOAD screen too — measured live at **2.586 s
with real progress already at 1.0**. Scene-serialized in ShellScene (~line 111701, value
`2`); changing it later needs K12's new-serialized-field technique or a scene edit.

<details><summary>Original kickoff (kept for reference)</summary>

```
Task: boot_loading_screen_removal — the initial loading screen is a
hardcoded timer. Cesar's rule (2026-08-05): make it reflect REAL loading,
or REMOVE it if the real wait is under 2 seconds.

GROUNDED CURRENT BEHAVIOR:
Boot flow: Splash START/Play → SplashScreenController.OnStartClicked →
ScreenManager.ShowScreen(ScreenId.Loading) → LoadingScreenController in
LegacyBootHome mode → auto-navigates to Home.
LegacyBootHome is 100% FAKE: target = timer / minLoadingTime
(minLoadingTime scene-serialized = 2, ShellScene ~line 111701), display bar
chases at 0.5/s, finish requires timer ≥ 2s AND bar ≥ 0.999 → ~2.0–2.2 s of
pure theater. NOTHING feeds it real progress — the only
SetProgress/SetRealProgress callers in the repo are GameplaySceneLoader's
(HoleLoad path). Heavyweight boot init (CSV singletons, CharacterManager,
save load) runs in Awake/RuntimeInitializeOnLoad — done before the Splash
screen is even interactive.
→ Real remaining work at Loading-show ≈ 0 s → per the <2s rule: REMOVE.

STEP 0 — MEASURE FIRST (cheap, guards against invisible async work):
Log Time.realtimeSinceStartup at ShowScreen(Loading) and log any work still
running at that moment. Expected: nothing but the timer. IF measurement
finds ≥2 s of real async boot work the static read missed, STOP — wire
SetRealProgress from that work instead of removing, and report. Otherwise
proceed with removal.

IMPLEMENTATION (removal branch, expected):
1. SplashScreenController.OnStartClicked: ShowScreen(ScreenId.Loading) →
   ShowScreen(ScreenId.Home). That is the entire boot change.
2. HomeScreenController.OnPlayClicked legacy fallback (~line 454): when
   matchmakingModal is unwired it shows ScreenId.Loading — a fake screen
   that bounces back to Home. Replace the fallback with a Debug.LogError
   (it only fires on a wiring bug; navigating to a fake loader helps
   nobody). Do not touch the modal path above it.
3. DO NOT touch LoadingScreenController, LoadingBar, GameplaySceneLoader,
   or ScreenManager — the HoleLoad path (real progress: host op 0–50%,
   hole op 50–100%, FinishLoadingCoroutine) reuses the same screen and
   must keep working byte-identically. This also keeps K13 conflict-free
   with K12 (in flight in MatchmakingModalController).
4. Keep the Loading screen GameObject + ScreenId — it is the HoleLoad
   surface, and a future real boot dependency (backend login is on the
   roadmap) can re-enter the flow via the existing SetRealProgress
   plumbing. Leave a comment at the OnStartClicked call site saying so.

KNOWN ADJACENT KNOB — report, do not change:
FinishLoadingCoroutine enforces minLoadingTime (2 s) as the MINIMUM for the
HOLE-LOAD screen too. That path is real-progress-driven and the floor is
deliberate anti-flash staging. If Cesar wants the hole-load handoff
snappier later, that scene-serialized field is the knob (same
scene-serialization trap as K12 — flag only).

VERIFY — EDITOR-VALID:
1. Editor, full game: Logo → Splash → START → lands DIRECTLY on Home; bars/
   chrome correct per the ScreenManager show/hide matrix; no Loading flash.
2. Demo define (GOLFIN_DEMO): Splash "Play" → Home works (DemoGate's
   allowed-screens list includes Home; Loading simply never shows).
3. 1v1 matchmaking → OPPONENT FOUND → hole-load Loading screen still
   appears with a REAL progress bar and hands off to gameplay — the
   HoleLoad regression gate.
4. Login/CreateUsername → Home paths unchanged (they never used Loading).
5. Report the Step-0 measurement numbers either way.
```

</details>

### K16 · hole_scene_leftover — **CLOSED 2026-08-05** (`a6b022642` + `1372da34b`)

Hole_06_Geo reappearing in the edit hierarchy: FIXED. Capture launchers now snapshot the
scene setup before staging and restore it at `EnteredEditMode`, closing staged `Hole_*_Geo`
scenes **without saving**. New shared helper `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs`
(`Capture` / `Restore` / `StripSerializedHost<T>`), SessionState-backed (not EditorPrefs).

**Shipped the ALTERNATIVE, not the exit-hook-restore variant:** `SmokeRunner2eMenu` adopted
LoopV2SmokeBotMenu's "Option B" — the host is injected at `EnteredPlayMode` and never
serialized, so the LabScaffold write is removed entirely rather than cleaned up afterwards.
Handlers re-registered via `[InitializeOnLoadMethod]` so a domain reload can't orphan the
cleanup. Staging DURING a run is unchanged (same scenes, additive order, host, sequence) —
exit-path only. `VersusHudCaptureMenu` got the snapshot/restore at all three staging sites.

**Scope note — went beyond the kickoff:** `SmokeRunner2fMenu` had the identical leak with
Hole_01_Geo and got the same Option-B treatment. Its save-before-play was actively harmful,
baking 13 unrelated `_disabledAlpha` serialization lines into LabScaffold on every run. Its
defensive pre-clean STAYS as specced; it now finds nothing. Revert that half if unwanted.

Second residue cleared in `1372da34b`: the serialized `SmokeRunner2fHost` that was already
committed in LabScaffold (line 26561) — split into its own commit so the scene diff is
reviewable (−14 residue / +13 `_disabledAlpha` catch-up, nothing else). No launcher
serializes a host any more, so it cannot recur.

Verified in-Editor: (1) 2e OB run end-to-end → hierarchy back to ShellScene, captures
produced, ZERO git change to LabScaffold or Hole_06_Geo; (2) twice back-to-back, run 2
snapshots ShellScene not a contaminated setup; (3) 2f "Closing stale hole scene" no longer
fires (count 0); (4) VersusHud restore fires, Hole_04_Geo closed unsaved — exercised via its
gate + a play-mode cycle, NOT a full recorded match. A complete 2f run leaves LabScaffold
byte-identical (md5 `fa8ac173…` before and after).

⚠️ **Pre-existing bug found, NOT fixed here — needs its own task.** The 2e OB capture no
longer reaches OB: it lands 18.95m `AtRest` in Rough vs the committed log's 131.28m
`TerminalState=OB` (log from `4f9fd2012`). `SmokeRunner2eHost.cs` is untouched by K16 and the
shot is a fixed `wedge_100_zerospin` preset, so this is physics/terrain/preset drift. The host
silently falls back to "capturing current state as evidence", so **the capture looks
successful while proving nothing about OB.**

---

## How current work is actually tracked

1. **Live queue = `Docs/Specs/Active/`** — spec-sized tasks are folders there; authoritative for what's open at spec scale.
2. **Kickoff-sized tasks = PENDING KICKOFFS section above** — full fenced blocks, written when produced (rule confirmed by Cesar 2026-08-04).
3. **Completed tasks = `Docs/Specs/Completed/<slug>/`.**
4. **Session state headline = `Docs/AI_CONTEXT.md`** — upload at session start.
5. **Pre-2026-05-01 narrative history = `Docs/Archive/TELLCODE_HISTORY.md`** + git history of this file.

## Rules

- Spec-sized tasks (>50 lines): per-task folders under `Docs/Specs/Active/<slug>/SPEC.md`; this file gets a pointer only.
- Kickoff-sized tasks: full fenced kickoff block in PENDING KICKOFFS, at the time it is produced — chat-only kickoffs are forbidden (they die with the session).
- Chat delivery to Cesar = the FULL fenced block as well, not a one-line pointer (rule confirmed by Cesar 2026-08-05: he wants to see the info inline). TellCode is the durable copy; the chat block is the readable one. Both, every kickoff.
- Task references use the TASK NAME first, K-number in brackets: `nav_bar_edge_gaps` (K4). Never a bare K-number — in chat or in new text in this file (Cesar 2026-08-05). Existing headers already carry both; no retro-rewrite needed.
- Refresh the CURRENT STATE bullet whenever touching this file.
- New UI tasks use the multi-agent pipeline at `.claude/agents/` (see `CLAUDE.md` § Multi-Agent Workflow).
- Live course importer is `HoleGeoImporter.cs` (NOT `HoleLiteImporter.cs` — deprecated, banner header, commit 980cc122). Verify via `grep MenuItem` before touching importer internals.
