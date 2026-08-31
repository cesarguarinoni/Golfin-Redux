# TellCode.md — handoff channel (POINTER + KICKOFF FILE)

> **Spec-sized tasks live in `Docs/Specs/Active/<slug>/SPEC.md` — this file only points at them.**
> **Kickoff-sized tasks (no spec folder) live HERE in full**, in the PENDING KICKOFFS section, so they survive the chat session that produced them. (Rule updated 2026-08-04 by Cesar: chat-only kickoffs die with the session; every kickoff the Architect produces is written here at the time it is produced.)

---

## ▶ CURRENT STATE — update this block at every session boundary

- **Gacha → admin: PLAN filed + spec A SPEC_READY (Architect, 2026-08-31)** — `Docs/GACHA_ADMIN_PLAN.md` §9 decisions taken by Cesar the same day (rates = rarity bp table + weights; pity per banner, may be none; dupes → RP; ticket ledger from 0; 5b overlay + gacha-only re-apply; banner text UI-authored, never in the artwork). **`Docs/Specs/Active/gacha_admin_catalogs/` is SPEC_READY** — kickoff below.
- **Missions track CLOSED (Cesar, 2026-08-30).** `missions_v1` DONE (`8a4275064`, mode OPEN on
  prod — `modes` v8, `texts` v16, 40 component-built missions on Lomond, server-generated Daily
  Mission, seven catalogs + Missions/Components/Daily panels) and `daily_mission_home_pill` DONE
  (`e86edd10a`, `texts` v17 — Home pill, `StreakFlame` on the daily card, shared
  `DailyMissionState`). Design of record `Docs/Game Design/MISSIONS_REDESIGN.md`; economy in
  `Docs/Economy/ECONOMY_MASTER.md` §3. Open follow-ups, not blocking: `HoleTees.csv` yardages
  disagree with pars on 10 holes (data only — `HoleData.tees` is loaded but nothing renders it
  yet). Rankings button on Mission Selection: REMOVE for now — Quick task
  `Docs/Specs/Quick/missions_rankings_button_removal.md`.
- **`game_modes_admin` is DONE** (Cesar, 2026-08-28) — folder in `Docs/Specs/Completed/`.
  `modes` is the TENTH content catalog and mode entry fees are SERVER-PRICED:
  `POST /points/spend` refuses a `mode_entry_fee:<id>` debit that does not match
  `golfin_mode_fees`, the mirror a publish writes in the same request. New Modes
  panel + a new Rewards panel over `game_point_actions` (LIVE ON SAVE, no draft).
  API **v59**; dashboard **`d35c8706-576f-4bec-ba62-cc9946b77a14`** stamp `3143fd639`.
  ⚠️ **The bare `mode_entry_fee` reason was CLOSED the same day** (Cesar's word,
  after approval) — the last self-priced door. **No build carrying
  `SpendReasons.ModeEntryFeeFor` has shipped yet**, so mode entry 400s for every
  client in the field until one does. Chosen deliberately; the fix is the build,
  not a revert. API **v60**.
  ⚠️ **New standing rule:** anything that changes what a content catalog SERVES
  must go through `mirrorForCatalog` (`lib/contentMutations.ts`) — rollback was
  the path that did not, and it stranded the charged price behind the served one.
  The dashboard now has a vitest suite and `cf-deploy.sh` runs it.

- **`progress_server_side` is DONE** (Cesar, 2026-08-28) — folder in `Docs/Specs/Completed/`.
  Level-ups are server-authoritative: `POST /api/v1/progress/level-up`, cost summed from the
  PUBLISHED `level_up_costs` catalog (the ninth, with its own admin panel), debited through
  `spend_pts` with reason `progress:<kind>:<ref>:L<to>`, and the level RECORDED — one transaction.
  §5 folded in on Cesar's call: `/points/spend` now refuses `character_level_up` and
  `club_level_up`. API on **v57** (`playlife-api:deployment-01M13MS0R4MDNNNGK94RNFAX04`).
  ⚠️ **The next build to ship MUST carry `ProgressService`** — the legacy door is shut, so any
  client older than that gets a clean 400 on LEVEL UP. That was the accepted trade
  ("they are testers, not real users"), not an oversight.
  Next up per Cesar: `game_modes_admin` (SPEC_READY, unblocked).

- **LIVE ADMIN: `admin.golfin.world` is at `6ccd4a8a2`** (Cloudflare deployment
  `96e5ad86-8466-466b-a3a4-8d9356ccf694`, 2026-08-28). Three deploys today, in order:
  `577be843-…` (the outstanding backlog, stamped `3df55d58f`), `c927bde9-…` (the Level Costs panel,
  `0c26421b8`), `96e5ad86-…` (the sidebar-label fix, `6ccd4a8a2`).
  **§23 update:** the footer stamp IS readable — it renders bottom-left in the sidebar and reads
  `6ccd4a8a2`, confirmed in a browser against the live site. The `/api/version` CURL still cannot
  work (Access 302s it, no service token exists), so the shell substitute remains the bundle grep
  plus `wrangler deployments list`.
  **NOT A BLOCKER, and do not carry it as one.** The stamp IS checkable against the running site:
  it renders in the sidebar footer, and Cesar's Chrome normally holds an Access session, so
  `mcp__claude-in-chrome__*` reads it directly — that is how `6ccd4a8a2` was confirmed on
  2026-08-28. An Access service token (or a bypass policy on `/api/version`) buys exactly one thing:
  the check becomes scriptable, i.e. usable from a cron/CI/headless run where no browser session
  exists. Nothing today does that, so it is a nice-to-have, not a debt. Updated 2026-08-28 (Claude Code, progress_server_side).

- (superseded) LIVE ADMIN was at `41076c6a3` (Cloudflare deployment
  `dc5097b7-b57b-40da-ac8c-baa181381dd5`, 2026-08-28T05:2xZ). ⚠️ Deploying was NOT enough: the row
  editor rendered art fields only when the STORED row already had the key, so no seeded row showed
  `portraitUrl` and art-by-URL was unreachable from the panel — see PIPELINE_HARDENING §2b. Updated 2026-08-28 (Claude Code).
  It had been stuck at the 2026-08-27T07:47Z deploy with **four** dashboard commits local-only —
  `1f3450c53` (content_two_way row-editor/panel/i18n), `15f2553f1` (catalog-art upload UI),
  `c15998c30` (WebP-only), `541864b38` (URL-only badge). The architect brief named three; the
  fourth surfaced from the deployment records, and its `b4aa4467` reference does not exist in this
  repo. All four are now live. No migration was pending (the `catalog-art` bucket already existed),
  so ADMIN_DASHBOARD_OPS §2's migration-first rule was vacuously satisfied.
  **The dashboard now stamps its own commit** (PIPELINE_HARDENING §23 companion): sidebar footer +
  `GET /api/version`, baked by `cf-deploy.sh` from `git rev-parse --short HEAD`; a dirty tree
  stamps `<hash>-DIRTY`, a build that skipped the script stamps `unstamped`.
  ⚠️ **§23 says "is it deployed?" is a curl — it is not, and cannot be.** Cloudflare Access 302s
  every request including `/api/version` and the static assets, and ADMIN_DASHBOARD_OPS prescribes
  no authenticated-curl path. Until an Access service token or a bypass policy on `/api/version`
  exists, the shell check is `grep` on the built bundle
  (`.open-next/server-functions/default/.next/server/app/api/version/route.js`) plus a browser.

- **Last updated:** 2026-08-27 (Claude Code — **`hole02_tree_bake_drift` DONE.** Hole 02 collided with 1,495 Spruce the local scene never drew: `tree_obstacles.csv` (committed `4b0054069`) held 2,983 rows — 1,488 terrain + **1,495 standalone** — while the Mac's `Hole_02_Geo.unity` (2026-06-01, gitignored, predating that placement pass) had **no `StandaloneTrees` container at all**. Terrain trees survived because `TerrainData` is tracked; standalone trees lived only in the per-machine scene. **Fix: standalone placement is now TRACKED** — `Assets/Golf/Courses/<course>/Data/hole-NN-geo/standalone_trees.csv` (`prefab,worldX,worldY,worldZ,yawDeg,scale`, sibling order), committed for all 18 holes (16 with trees; 01 and 06 header-only by design so "file absent" always means "never exported"). New `StandaloneTreeCatalog` adds `Import/Standalone Trees/{Export Current Hole, Export All Holes, Rebuild Current Hole}`; `TreePlacer.PlaceTrees` and both `TreeBrushTool` write paths re-export automatically. Hole 02's catalog was seeded FROM the bake and the scene rebuilt (1,495 prefab instances under `HoleRoot/StandaloneTrees`). **New drift gate:** `Import/Bake Tree Obstacles/Validate All Holes` re-harvests every hole with the baker's own harvest fn and diffs vs the committed CSV (per-profile counts + 1 cm positions) plus scene-vs-catalog — **18/18 PASS**, tripwire-verified (a 5 cm move, a deleted row, and 3 removed trees each FAIL, and it returns to PASS on restore). Wired into `CIBuild.BuildIOS` and `BuildIOSDev`; `-skipTreeBakeCheck` disarms it loudly. ⚠️ **Byte-identity to `0519c2f0` was NOT achievable and this is not a tool defect:** the baker does not store `baseY`, it re-derives it via `terrain.SampleHeight(x,z)`, and the CSV only preserves X/Z to 4 decimals — re-sampling at the rounded X/Z flips the 4th decimal of `baseY` on the 73 of 2,983 rows whose true height sits within ~2.5e-5 m of a rounding boundary (0.1 mm each; every X, Z, scale, profile, count and row order identical). All 73 were solvable only by nudging trees to the corner of their rounding cell to steer the printed digit — fitting data to a hash, so it was NOT done. Re-baked to **`687cd578`** and proved `rebuild → save → bake` is now a **fixed point** (twice, byte-identical). Also corrected a spec slip: seeding at `worldY = baseY` would have floated all 1,495 trees 30 cm — every healthy hole has `worldY = baseY − 0.30` (TreePlacer's sink offset), measured. Doc: `Docs/Pipeline/TREES_AND_GENERATED_SCENES.md`. **Two findings worth carrying:** (a) `PlaceBallAt`'s ground-snap raycast lands on TREE capsule colliders — it put a ball 23 m up in foliage; assert the snapped Y against `Terrain.SampleHeight` when scripting placement near trees; (b) **1,365 of Hole 02's 1,495 standalone Spruce (91%) are OB** per the baked mask in `zones.json` — a ball hit into those tree lines terminates `HitOOB` and the loop resets it, so only ~130 trees are actually strikeable in play. **No device pass — not needed** (Cesar, same day: *"If it works in Unity there is no reason for it not to work on device."* Standing rule now, see memory `feedback_no_device_pass_by_default`). Editor evidence: stills + an invariants JSON + a render↔collider overlay projecting the committed collider positions onto the live render. Of the four scripted strikes only 2 landed as verified trunk hits (11 and 10 trunk crossings, tested along the real trajectory through the sim's own provider), both on the right line — see the 91%-OB finding above for why the left line cannot produce an at-rest ball. The one recorded clip is unusable (camera sat in a top-down aim state, no trees in frame) and the recorder allows one clip per Editor launch.)

- **Last updated:** 2026-08-18 evening (Architect — **`tournament_async_board` (Phase 4 of `tournaments_server_side`) server half BUILT; Unity spec SPEC_READY, kickoff GATED on deploy.** Cesar's calls: board first / payout (Phase 5) after; bots retire ONE-WAY at 10 human entries and are REMOVED from the ranking (`tournaments.bots_retired_at` latch); leaderboard sends BOTH ranks — display (blended) + `prize_rank` (human-only, bots never paid). Written into playlife (UNCOMMITTED): `migrations/2026_08_18_tournament_async_phase4.sql` (bot latch, `tournament_entries.display_level`, `golfin_bot_fields` + `golfin_bot_brackets` server mirrors of the CSVs), NEW `routers/tournaments_golfin.py` mounted at the same `/api/v1/tournaments` prefix — `POST /golfin/{slug}/enter` (server-side fee debit via `spend_pts` with deterministic uuid5(user:slug) key — self-heals, never double-charges), `POST /golfin/{slug}/submit-hole` (§6b.3 plausibility: hole-in-set, strokes 1–15, window = end+resolve_delay grace, 20s/hole pace tripwire, idempotent replay for the offline queue), `GET /golfin/{slug}/entry` (cross-device resume), `GET /golfin/{slug}/leaderboard` (server bot generation ONCE per tournament — seeded via bot_seed latch, persisted into entries+hole_results with organic-reveal timestamps; ranking is a faithful port of LocalTournamentBackend: provisional score-to-par/thru/earlier-submit no partial ties, final strokes+T-ties+earlier submitted_at; DNF & thru-0 hidden; caller row always in `player`). Logic proven end-to-end against a fake Supabase (enter/replay/pace/retire/final). GPS endpoints in `tournaments.py` untouched. ✅ SHIPPED TO PROD same evening: migration APPLIED (verification 2 new cols / 3 bot fields / 6 brackets / RLS true), `fly deploy` green, smoke: `/health` 200, all four `/golfin/{slug}/…` endpoints **403-not-404** (mounted, auth-gated), the public `/golfin` schedule still 200, garbage routes 404. **The tournament_async_board kickoff below is pasteable NOW.** Phase-4 NOTE: `GetResults`/`ClaimPrize` keep the existing client-side earn-game `tournament_prize` payment path (unchanged behavior) — Phase 5 moves payment into the resolver and re-points ClaimPrize (decision of record #5).)
- **Last updated:** 2026-08-18 later (Architect — **`leaderboard_backend` server half BUILT; Unity spec SPEC_READY.** Cesar's decisions of record: server-side fake pool (everyone sees the same board), character id+level sync to profiles, Architect builds/deploys backend + Code does Unity. Written into playlife (UNCOMMITTED — Code or Cesar commits): `migrations/2026_08_18_golfin_leaderboards.sql` (profiles.golfin_character_id/level, `golfin_fake_players` seeded with the 120 fake_players.csv rows RLS-on-no-policies, `golfin_leaderboard(p_start,p_end)` aggregation fn — filters by the `game_point_actions` catalog so GPS RP + admin grants never rank, service_role-only), `routers/leaderboards.py` (`GET /api/v1/leaderboards/{daily|weekly|monthly|historic}`, AUTH, ranks+T-ties server-side 1,2,2,4, deterministic fake scores per period key — participation 35/70/90/100%, ranges sized post-÷10 ⚠️ NOTE constants need tuning once real beta scores exist), `user.py` `PUT /golfin-character`, `main.py` mount. ✅ SHIPPED TO PROD same day: migration APPLIED via Supabase SQL editor (Cesar; self-contained verification query returned 2 golfin profile cols / 120 fakes active / RLS true / 0 fn-vs-ledger mismatches), `fly deploy` green (both machines good, via MacOS-MCP shell + nohup — flyctl is NOT in the device_bash VM, it lives on the Mac at ~/.fly/bin), smoke: `/health` 200, all four `/api/v1/leaderboards/{period}` + `PUT /user/golfin-character` respond **403-not-404** (mounted, auth-gated), garbage route 404s. **The Unity kickoff below is pasteable NOW.** Ledger-derived scores mean past periods are queryable for free — the v1.0 "previous period results" popup needs no snapshots when it's wanted. Fake-pool dashboard panel = deliberate follow-up.)
- **Last updated:** 2026-08-13 (final) (Architect — **THE DASHBOARD IS LIVE AND THE LOOP IS CLOSED.** `admin_dashboard` v1 scope complete: running against production, real Supabase password auth verified, and Cesar performed the first live admin RP adjustments through the UI (+100 then −50 on Cratilo → balance 123→223→**173**). Prod SQL confirms both rpc paths and the audit trail: ledger rows `manual_admin_grant +100` / `spend -50`, plus two `rp_adjust` rows in `admin_audit_log` carrying before/after `total_points` and the admin's email. Two days after "start the admin dashboard", the chain runs end to end: **dashboard → `earn_pts_v2`/`spend_pts` → the one shared ledger → the game's RP.** ⚠️ SECURITY STANDING ITEM: the service_role key was pasted into a chat transcript — rotate it, and when you do, update BOTH `Tools/admin-dashboard/.env.local` and the playlife-api Fly secret in the same sitting or `/points/*` breaks. Still open elsewhere: `points_device_checks` (Cesar-only, 3 checks); dashboard hosting + Google OAuth for admin login; spec §4 v2/v3 panels pending Track B.)

- **Last updated:** 2026-08-13 later (Architect, back on the Mac — **BOTH admin-dashboard pre-prod flags CLEARED; the dashboard is now one env-file away from live.** (1) `Tools/admin-dashboard/migrations/2026_08_13_admin_audit_log.sql` **APPLIED to prod** via the Supabase SQL editor (project `wmszyghwwkaptgqdunel`) — post-apply verification: `admin_audit_log` exists, RLS enabled, `authenticated` SELECT = **false**. `writeAudit()` now has a real table and the Audit panel is backed, not silently empty. Migration header stamped APPLIED. (2) **RPC signatures verified live** with `pg_get_function_arguments`: `earn_pts_v2(p_user_id uuid, p_action text, p_pts integer, p_description text, p_key uuid)` and `spend_pts(p_user_id uuid, p_amount integer, p_reason text, p_key uuid)` — **exactly what `lib/mutations.ts` calls**, so the code was right and the README caveat was stale text predating the fix; the caveat is deleted and replaced with the verified signatures. Code's PC-side import (`f5594562f` v1, `91e466684` v2) was clean — v1 a strict subset of v2, 12 added / 11 modified / 0 deleted, no node_modules, no secrets. **REMAINING to go live: Cesar pastes the 4 Supabase values into `Tools/admin-dashboard/.env.local`, then `npm install && npm run dev`.** ⚠️ Standing caveat worth repeating: mock mode accepts ANY credentials, so "it let me in" proves nothing about live auth — the first real email/password sign-in IS the auth test (use the gmail/Cratilo account; Google OAuth is not wired into the dashboard). `points_device_checks` remains Cesar-only and untouched.)

- **Last updated:** 2026-08-17 (Architect — **the admin dashboard is LIVE at https://admin.golfin.world** (Cloudflare Workers, Next Innovation account, behind Cloudflare Access + the app's own Supabase login; no workers.dev by design). **Read `Docs/ADMIN_DASHBOARD_OPS.md` before touching it** — deploy loop, the two-places rule for adding an admin, and the traps that have already cost time: never `next build` against a live `next dev` (shared `.next/`, every chunk 404s, log stays clean); tooling shells inherit `NODE_ENV=production` which prunes devDependencies and breaks middleware; the service_role key must not enter the bundle but `NEXT_PUBLIC_*` must; the missing-key guard fails closed everywhere rather than inferring from `NODE_ENV`; migration always before deploy, and DDL needs Cesar in the Supabase SQL editor. Also shipped today: tournaments gained `title_ja` (a Japanese display name for dashboard-created tournaments) and `is_active` (Activate/Deactivate — deactivated tournaments are simply absent from `/tournaments/golfin`), both applied to prod and verified end to end. Two Unity-side specs are SPEC_READY for these: `Docs/Specs/Quick/tournament_title_ja/` and `Docs/Specs/Quick/tournament_schedule_refresh/`. ⚠️ Those two features interact — a deactivate switch plus a refetch on every screen entry turns 'the server no longer sends this tournament' from rare into routine, so `MergePreservingEntered` (the B1 bug from the Phase-3 review) needs a test at the screen level.)

- **Last updated:** 2026-08-13 (Architect — **`reward_points_backend` is COMPLETED; `admin_dashboard` hold is LIFTED.** (1) **`reward_points_backend` → `Docs/Specs/Completed/`.** The whole RP cutover shipped 2026-08-12 (Phase A in prod → Slice 1 → Slice 2 rebalance+flag-ON → the three follow-ups); EditMode 1172 passed / 0 failed / 3 pre-existing skips of 1175. The only thing left was three checks the Editor structurally cannot run, so per Cesar they are **split out** into a new task rather than holding a finished spec open. (2) **NEW: `points_device_checks`** (`Docs/Specs/Active/points_device_checks/`, STATUS `AWAITING_DEVICE_PASS`) — the 3 device checks verbatim from `reward_points_backend` IMPLEMENTER_REPORT Part 3 §8: signed-out launch cannot pass Login *tapping around the art, not just the buttons* (guards `AuthGate` + the deleted `DevBypassCatcher_TEMP`); flag-ON shop purchase stays debited after a refresh + an airplane-mode purchase grants nothing (guards `PointsSpendGate`, kills the self-refund); double-tap BUY is a no-op on a slow connection (guards the in-flight latch). **Cesar-only** — needs a device build with the `GOLFIN_POINTS_BACKEND` define and a live signed-in account with a hand-set balance (new accounts start at 0 by design). Nothing is known to be broken; no code is pending; this task runs NO subagent pipeline. (3) **`admin_dashboard` — hold LIFTED, and the implementer is NOT Claude Code.** Per Cesar the dashboard is being built in the **Architect / Cowork session**, and **v1 + v2 are already built**: scaffold (panel registry, admin login + `ADMIN_EMAILS` allowlist, `admin_audit_log` migration), Users panel **+ mutations**, RP grant/adjust, and the Points + Audit panels. What remains is **live verification**, not construction. ⚠️ **The old `### Kickoff · admin_dashboard` block below is SUPERSEDED — do not paste it into Claude Code**; doing so would re-scaffold work that already exists. The pointer at §SPEC_READY POINTERS is marked accordingly. (4) **Dashboard source landed in the repo** at `Tools/admin-dashboard/` — **v1 then v2, both imported 2026-08-13**; `node_modules` is not committed and `npm install` was deliberately NOT run inside the repo. v1 proved to be a **strict subset** of v2 (39 of 51 files, zero orphans), so the overlay was clean: 12 new files (`app/(panels)/points/`, `app/(panels)/audit/`, `users/action-modals.tsx`, `api/points`, `api/audit`, `api/users/[id]/rp`, `api/users/[id]/actions`, `lib/mutations.ts`, `lib/format.ts`, `lib/mockStore.ts`) + 11 modified, no deletions. Only `.env.local.example` ships — no secrets, `.env.local` and `node_modules` both gitignored. **Two things to settle before first LIVE use** (both are Cowork-session items, flagged not fixed): (a) `migrations/2026_08_13_admin_audit_log.sql` **has not been applied** — until it is, `writeAudit()` has no table and the Audit panel stays empty; the README says staging-first, run the footer verification queries, then prod (the script is idempotent); (b) the README warns the RP rpc parameter names are "assumed … verify against the deployed functions before first prod use", but `lib/mutations.ts` now cites the **deployed** signatures from `2026_08_12_points_spend_idempotency.sql` (`earn_pts_v2(p_user_id, p_action, p_pts, p_description, p_key)`, `spend_pts(p_user_id, p_amount, p_reason, p_key)`) — the README caveat is stale relative to the code, and the code could not be checked from Windows because the playlife repo lives on the Mac. Confirm against the deployed functions once, then drop the README caveat. Note the default posture: **no service key ⇒ MOCK MODE**, where login accepts any email/password (allowlist still enforced after sign-in).)

- **Last updated:** 2026-08-12 EOD (Architect — **THE LEDGER DAY, closed.** `reward_points_backend` went spec→production in one day: Phase A (idempotent earn + atomic spend + catalog) applied to prod Supabase + deployed to Fly ×3; gift_pts/total_points invariant bug found, fixed, reconciled; Slice 1 (Golfin.Net/PointsService, flag OFF, accepted vs live RP=123); ÷10 rebalance approved (welcome grant KILLED — admin grants replace it; ceil(level/2) sums 14,520 not 14,460, prose corrected); Slice 2 (rebalance + re-point + flag ON) landed; follow-ups landed same evening (BotSessionOverride dev bypass — harness SEQUENCE COMPLETE from boot again; shop via PointsSpendGate; hard sign-in gate — which also killed DevBypassCatcher_TEMP, an invisible ship-in-build tap-to-Home auth hole). Commits: GolfinRedux 510c433ad/f30787e71/25292f73d + EOD follow-ups; playlife c4829af/37f27d9 — all pushed. **NEXT SESSION: 3 device checks (IMPLEMENTER_REPORT) → move spec to Completed → lift `admin_dashboard` hold (kickoff below still valid).** Standing facts: Cesar runs GPS too; RP == total_points; new accounts start at 0; bots never log in for real; Google/Apple auth can't complete in Editor — email/password is the Editor path; filtered EditMode runs mask failures — sweep per assembly.)

- **Last updated:** 2026-08-12 later (Architect — SEQUENCING CHANGE, Cesar: **Reward Points move to the backend FIRST; admin dashboard waits.** New active task `reward_points_backend` (`Docs/Specs/Active/reward_points_backend/SPEC.md`) — answers GPS_UNITY_PORT_SPEC §2: unify on the PLAYLIFE `points_transactions` ledger. Decisions: online-required spends + queued earns (idempotency keys); dashboard's points panel will read the one ledger (its open question 2 = BEFORE). Kickoff issued for **Slice 1** (Unity infra behind default-OFF flag — no backend dependency, zero behavior change). Phase A UNBLOCKED same day: repo at **`/Users/cesar/Documents/playlife`** (Cesar; fresh copy, mtimes 2026-08-12) — real `points.py` + `points_atomic.sql` read; ⚠️ key find: `earn_activity_pts` welds every earn to avatar XP/level at 5–50-pt PLAYLIFE scale. Architect proposed a separate `golfin_rp` currency — **Cesar OVERRODE same day: ONE shared RP value** (`total_points`); game prices/rewards get rebalanced to the GPS scale, and per Cesar the **rebalance is folded into this task** — Slice 2 ships rebalance + cutover together, gated on an `RP_REBALANCE.md` table (Architect drafts, Cesar approves every number). Spec §3/§4 rewritten to the one-value design; Phase A kickoff below REVISED accordingly (any earlier `golfin_rp` kickoff is dead — this file's version is current). **REBALANCE APPROVED same day:** `RP_REBALANCE.md` (in the spec folder) is the binding number set — global ÷10, level-up `ceil(level/2)`, stamina global rounding, §3 caps as drafted, and one amendment: **the 50,000 welcome grant is REMOVED** (testing-only per Cesar; new accounts start at 0, test balances admin-set via dashboard/Supabase — no welcome or migration actions in `game_point_actions`, no client migration logic, and Slice 2 deletes `DEFAULT_STARTING_POINTS` from `RewardPointsManager.Awake`). Also of record: **Cesar now runs the GPS app too** — every former "Ken's nod" item is Cesar's own call. **✅ PHASE A SHIPPED TO PROD same day** (Architect + Cesar): migration `2026_08_12_points_spend_idempotency.sql` applied via Supabase SQL editor (verify-first passed; `earn_pts_v2`/`spend_pts` live, service_role-only; idempotency column+index live; catalog = 3 actions, welcome/migration seed rows deleted per decision #6), **gift bug fixed end-to-end** (new migration `2026_08_12_gift_pts_total_points_fix.sql`: trigger now credits total_points, EXECUTE revoked, reconciliation ran — invariant holds on 0 profiles, the 2 affected accounts now 475/680), and **deployed**: flyctl installed (`~/.fly/bin`), Cesar authed (wonderwall acct, one-time high-risk unlock), `fly deploy` green, `/health` ok, `/points/spend` + `/points/earn-game` + `/points/balance` all respond 403-not-404 in prod. **SLICE 1 LANDED same day** (Code report: 46 new tests, suite 1159/0, zero-behavior-change proven two ways; contract notes recorded: /health is root-mounted not under /api/v1, 403 = missing header vs 401 = refresh-and-replay, errors are {detail} not {data}; AuthServiceTokenProvider adapts AuthService.cs:126 refresh — no second auth path). Manual acceptance on Cesar: Editor play mode signed in -> GOLFIN > Points Backend > Enabled -> Log Server Balance Now -> expect the balance log; toggle back OFF after. **Slice 2 kickoff ISSUED — block below** (rebalance + re-point + cutover; catalog-mirror SQL is write-only, Architect applies). `admin_dashboard` pointer below marked ⏸ ON HOLD; its kickoff stays valid, don't start it.)

- **Last updated:** 2026-08-12 (Architect — Cesar gave GO: **"start the admin dashboard."** `admin_dashboard` spec (filed 2026-08-12 by the auth-epic Cowork session, `Docs/Specs/Active/admin_dashboard/SPEC.md`) is now the active task — pointer + kickoff added below (§5 steps 1–2: Next.js scaffold at `Tools/admin-dashboard/` + read-only Users panel over Supabase). STATUS.md created at SPEC_READY. Web app, not Unity — no Assets/ edits.)

- **Last updated:** 2026-08-10 (Architect — close-out sweep per Cesar: "all tasks from yesterday and today are done." CLOSED: `auto_club_selection` (`43d8a34c9`), `power_gauge_target_marker` (Order 357, off the video), `map_view_strict_crop_indicators` (Order 355, off the video), `aim_camera_ball_centering` (moved to Completed in this sweep — pending Architect calls D2/D3 accepted as-is), `putter_aim_blue_line` (approved off the Hole 6 video), plus the `hole1_cup_buried_under_green` repair (`da62daf86`, surgical CupReseatTool — a Hole 1 re-import would have destroyed 1362 trees + the baked sim data; standing rule: shipped holes are repaired in place, never re-imported). All five SPEC_READY pointer+kickoff blocks below pruned to strikethrough one-liners. Notion GOLFIN_Roadmap rows closed 2026-08-10. Repo committed + pushed same day, incl. the previously-uncommitted `Scenarios.cs` in-flight ClubHandle regression guard + smoke-bot log refresh. `Docs/Specs/Active/` now holds no SPEC_READY work (only the historical `ob_boundary_presentation` + `phone_build_smoke_test` folders) — next task comes from Cesar.)

- **Last updated:** 2026-08-05 15:05 JST (Architect — **DEVICE ERA.** Game builds+runs on physical iPhone since 2026-07-27; signing SOLVED (do not re-litigate); on-device smoke found 7 issues. Fixed since: `centralball_device_invisible` (device-verified `1a4ad15ca`), `hole6_tree_collision_profiles` (`c1d38e280`), `camera_drag_touch_origin`/K1 (CLOSED — `bb59d32dd` 08-03, device-verified per commit + Cesar's session; block deleted 2026-08-05), `nav_bar_edge_gaps` (K4) (CLOSED — `49825e867` + ticket-cluster follow-up `26ceeb051`, 08-03 — PRE-DATED the batch write, same drift class as K1; cause was H1: fixed-width 1178px center-anchored bars under a **ConstantPixelSize** canvas, fix = stretch anchors + proportional icon re-anchor; NOT the CanvasScaler — the `loading_bar_inset` (K14) hold on that question is resolved; block deleted 2026-08-05, flagged by Cesar). Shipped: `build_version_stamp` (3 defects → hardening kickoff below). **iOS Simulator three-tier verification loop VALIDATED** — canonical doc `Docs/Pipeline/IOS_SIMULATOR_LOOP.md`; standing rules: never wipe the seeded DerivedData, never `BuildPipeline.BuildPlayer` via MCP script-execute. Full story: `Docs/Reports/2026-08-04_ios_simulator_build_blocker.md` §§10–13 + `Docs/AI_CONTEXT.md` top block. **OPEN = the PENDING KICKOFFS below** (6 smoke issues + build-stamp hardening + housekeeping; K9 `ui_frame_pacing` smoke #8 added 2026-08-05; K10 `ob_recovery_fixes` **CLOSED 2026-08-05** (`90dd574ff` camera+drop rule, `ed65f5726` permanent capture Y-flip fix; CupZoom same-class wedge found+fixed; OB now stops chasing with no aerial cut; ground-level settle built then reverted per Cesar); K1 closed. K11 `club_selection_green_gate` **CLOSED 2026-08-05** (`066df31f2` selector gate + `efa681acb` §2f re-decide after reposition — the item deferred pending K10; ⚠️ K10's close-out swept K11's in-flight lines and briefly broke `main`, repaired forward — see the K11 block). K12 `matchmaking_scan_pacing` added 2026-08-05 — find-opponent animation: decelerating scan + total cut ~5.6s→~3.1s, NO scene edit (new-serialized-field technique), queued AFTER K11 per Cesar — **now NEXT UP**. K13 `boot_loading_screen_removal` **CLOSED 2026-08-05** (`d3bf00026`) — measured first as instructed: zero real progress ever fed (`_useExternalProgress` never true, max `_realProgress` 0.000 across 2 runs), boot init done at t=3.8s vs Splash interactive at t=9.0s, real work behind the transition ~0.23s (Main Theme decode, already under the 0.25s fade) → REMOVED per the <2s rule. **click→Home 2.72s → 0.48s.** HoleLoad path verified byte-identical + live-regression-passed (real bar 0→1 via the real ModeHomeCard PlayButton). ⚠️ Adjacent knob still open: `minLoadingTime` (2s, scene-serialized) is also the hole-load screen's MINIMUM — measured 2.586s with progress already at 1.0; same scene-serialization trap as K12. K14 `loading_bar_inset` **CLOSED 2026-08-05** (`bae5386f3`) — shipped the SCENE route (not the code shim); `LoadingBarRoot` sizeDelta.x 0→-16, isolated one-line diff. Gate lifted when Cesar landed K7 (`d680198b3`) mid-task. **Two kickoff premises proved wrong and are corrected in the sequencing bullet above — read them before reusing that block's reasoning:** (a) the bar was never at zero inset — `Track` already carried `-48` (24 units/side), so the edit went 24→**32**/side; (b) the loading screen's canvas is **ScaleWithScreenSize** (ref 1170×2532, match-width), NOT ConstantPixelSize — that was K4's separate nav-bar canvas, so the kickoff's "8 units = 8 device px, dial ~24 for points" units advice was wrong-canvas and must not be reused. Verified on rendered pixels at 1170×2532: inset 32/32 at both 0% and 100%, fill 1106px across a 1106px track (reaches both ends exactly, the don't-break-functionality gate); `ProgressText` edges match `Track` edges; 16:9 renders the same 32 units as 28px at scaleFactor 0.874. Editing `Track` (the rect that owned the inset) would have desynced `ProgressText`, which carries its own `-48` — the root was the correct single dial. ⚠️ Side-finding for whoever owns matchmaking: opening+saving ShellScene reconciles STALE `MatchMakingModal` prefab overrides (an anchored-position `-564`→`-68` move, plus four `scan*Seconds` fields from `925a25398`). Reverted out of this commit to keep it one-line; it is still unreconciled on disk and will reappear on the next ShellScene save. RECONCILIATION DONE 2026-08-05 per Cesar ("Close them"): `arrow_speed_retune` (K6) CLOSED — `cd0ef6ed4` 08-04 verified against the kickoff shape in the diff: F13 changelog entry (93 lines), BOTH mirrors (controls.csv + ControlsConfig.cs), ShotController floor clamp (`Mathf.Max(arrowHz, MinArrowSpeedHz)`), both test files updated. NOTE: F13 locked at 30 fps, BEFORE `ui_frame_pacing` landed — if arrow feel reads differently at 60 fps on device, retune reopens as a NEW row; F13 stays the record. `ui_frame_pacing` (K9) CLOSED — `7380baf67`, FramePacingBootstrap.cs exactly as specced; device feel signed off via Cesar's own device sessions; in-hole 60 fps knock-on unreported — watch in whole-game perf (940). `b702e1a41` wind→ball-flight ACCEPTED as landed (no kickoff existed; it carries NO F-entry — flag for the next physics-changelog pass). Both blocks deleted. K15 `app_identity` **CLOSED 2026-08-05** (`66ac68575` → `7a63f7c2f`) — **the app is now `Golfin`**: productName RE2 → `Golfin`, companyName → NEXT INNOVATION PTE. LTD., default icon → `Assets/Icons/Golfin-Icon2.png`; bundle id + signing UNTOUCHED as specced. `Golfin: The Invitational` shipped first and was rejected on sight of the springboard — iOS collapses the spaces before truncating, rendering `Golfin:TheI…`. **The built .app is now `Golfin.app`** (executable + process name `Golfin`; `RE2.app` deleted) — `IOS_SIMULATOR_LOOP.md` re-pointed. ⚠️ Two findings for anyone driving the sim loop: (a) an append re-export can leave the pbxproj referencing `lib_burst_generated.cpp`/`.a`, which Burst NEVER generates for the simulator SDK — the tier-2 build dies with "Build input file cannot be found"; this is NOT a DerivedData problem, do not wipe, just strip the 8 lines (fix is PERMANENT — append preserves the pbxproj; the refs were legacy state inherited from an earlier device-SDK export); (b) icon2 carries an opaque alpha channel that Unity strips at icon generation — built icon reports `hasAlpha: no`, store-safe, leave it alone. K16 `hole_scene_leftover` **CLOSED 2026-08-05** (`a6b022642` fix + `1372da34b` residue strip) — capture launchers now snapshot the scene setup before staging and restore it at EnteredEditMode, closing staged hole scenes WITHOUT saving; shipped the Option-B alternative so SmokeRunner2e/2f inject their host at EnteredPlayMode and NEVER serialize it, removing the LabScaffold write entirely (2f's save was also baking 13 unrelated `_disabledAlpha` lines per run). The committed SmokeRunner2fHost residue is gone and cannot recur. 2f's defensive pre-clean stays and now finds nothing. ⚠️ SEPARATE pre-existing bug surfaced while verifying: the 2e OB capture no longer reaches OB (18.95m AtRest vs 131.28m TerminalState=OB in the log from `4f9fd2012`) — untouched host, fixed preset, so physics/terrain drift; it falls back to "capturing current state as evidence", i.e. LOOKS successful while proving nothing. Needs its own task.) plus `putter_aim_blue_line` (413, SPEC_READY in `Specs/Active/`, awaiting Cesar go) and a device pass on `demo_build_slice` (426). Everything below this bullet predates the device era and is historical.)

- **Last updated:** 2026-07-02 (Architect — `1v1_result_rewards_display` (347) DONE. NEXT-at-the-time = `stamina_boost_shop` (517) design pass. STALE — superseded by the device-era bullet above.)
- Older narrative bullets (2026-06-11 → 2026-06-24): preserved in git history of this file — all tasks named in them are closed in `Docs/Specs/Completed/`. Trust `Docs/Specs/Active/` + the AI_CONTEXT headline, not old bullets.

---

## 📋 SPEC_READY POINTERS

- **`gacha_admin_catalogs`** (filed 2026-08-31, Architect via Cowork) — `SPEC_READY`.
  `Docs/Specs/Active/gacha_admin_catalogs/SPEC.md`. Spec A of `Docs/GACHA_ADMIN_PLAN.md`: four
  content catalogs (`gacha_banners` extended, `gacha_rates`, `gacha_pools`, `ticket_types`), seed,
  round trip, three admin panels + validation + art upload, one client parser rail. Game behaviour
  unchanged. Specs B (`gacha_server_pull`), C (`gacha_client_real_pull`), D follow.

### Kickoff · gacha_admin_catalogs (issued 2026-08-31)

```
Read Docs/Specs/Active/gacha_admin_catalogs/SPEC.md and implement it.

Context:
- Spec A of Docs/GACHA_ADMIN_PLAN.md (read §2-§4, §9 for the why). Makes gacha_banners
  (extend Assets/Resources/Data/gacha_banners.csv in place, 13 new columns), gacha_rates,
  gacha_pools and ticket_types content catalogs #17-#20: Tools/content/catalogs.py, seed via
  seed_from_csv.py -> playlife/backend/migrations/2026_08_31_content_gacha_seed.sql (FULL SQL
  in chat for Cesar), export byte-identical + --check clean, three panels
  (gacha-banners / gacha-pools with Pools|Rates tabs / ticket-types) on the shared
  CatalogPanel, validation rules 1-20 in lib/contentValidate.ts with vitest, artUrl upload
  via contentArtMutations.ts (bucket catalog-art, 882x1448 target), lib/gachaOdds.ts
  (effectiveOdds + seeded simulate) - this is the reference the server roll is checked
  against in spec B.
- Look at: components-panel.tsx (tabs pattern), shop-panel.tsx + ref-picker (RefPicker,
  resolved preview, rowId prefill), contentValidate.ts (REQUIRED_COLUMNS / NUMERIC /
  ID_COLUMN / warn), contentArtMutations.ts (ALLOWED_CATALOGS / ALLOWED_COLUMNS),
  ModesDatabaseCSV.ParseCsvLine (quote-aware splitter for the one client rail in
  GachaBannerModel.ParseCsv - header-indexed, the 15 GachaStage2Tests pass UNMODIFIED).
- Game behaviour does NOT change: no overlay, no pull, no ticket changes - specs B/C.
- Minimal diff. Reuse existing systems (named above). Dashboard strings via lib/i18n.ts DICT
  en + ja; no player strings in this task.
- Out of scope: everything under the spec's Out of scope list.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec (both prod round trips pasted, vitest run quoted, deployment id +
footer stamp quoted, Access curl 302), flag which need manual verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`gacha_reveal_animation`** (filed 2026-08-31, Architect via Cowork) — ✅ **DONE, approved by Cesar 2026-08-31** (`6514fd8a6`).
  `Docs/Specs/Completed/gacha_reveal_animation/SPEC.md`. PULL x1/x10 on a banner card (and PULL-again on
  the Prizes screen) now opens a reveal modal (Figma `13997:4298`, bag art `Assets/Art/Gacha/Bag.png`):
  scrim over everything incl. the persistent bars, bag alone → shakes → each prize card pops out one
  at a time with rarity-scaled particle FX (UIParticle, tint from `RarityHelper`), auto-play + SKIP,
  then the Prizes screen enters with a staggered card entrance. New `GachaPullFlow` is the single
  seam for the real pull later (still mock). 12 new `SfxId`s wired through `SfxBus`/`sfx.csv`/
  `SfxLibrary.asset`, mapped to the 12 CC0 placeholder clips the Architect committed to
  `Assets/Sounds/Gacha/` (`CREDITS.md` there). One string `GACHA_SKIP` (EN+JA, importer path).

### Kickoff · gacha_reveal_animation (issued 2026-08-31)

```
Read Docs/Specs/Active/gacha_reveal_animation/SPEC.md and implement it.

Context:
- Adds the gacha reveal: PULL x1/x10 (GachaBannerCard.OnPullX1/OnPullX10) and the
  Prizes screen's PULL (pull again) go through a new GachaPullFlow.Pull(count) ->
  GachaRevealModalController (new, : ModalController, scene instance on the
  ShellScene canvas, static Instance) -> GachaPrizesScreenController.SetPendingResult
  + ShowScreen(GachaPrizes). Bag alone, shake, cards pop one at a time (x10: each
  replaces the last), rarity FX tiers, auto-play + SKIP, tap-to-fast-forward the hold,
  Prizes screen staggered entrance.
- Look at: GachaBannerCard.cs, GachaPrizesScreenController.cs (BindCard becomes the
  shared binder), GachaMockPrizePool.cs, ModalController.cs + ModalScrim.cs (scrim
  covers the bars for free), TapFeedbackFX.cs + TapFeedbackFX.prefab (the UIParticle
  precedent), SfxId.cs / sfx.csv / SfxLibrary.asset, QualityTierService.Current,
  RarityHelper.GetRarityColor. Coroutines + unscaled time — no tween library.
- Minimal diff. Reuse BagClubCard.prefab for the reveal card (no rebuild), the
  TapSparkle_Additive.mat, the Prizes PULL button atom for SKIP. Bag.png -> Sprite import.
- Audio: add the 12 SfxIds + sfx.csv rows and map each to its clip in
  Assets/Sounds/Gacha/ (already in the repo, Gacha_<Id>.ogg; import settings like
  Assets/Sounds/Hit/). Zero "No clip mapped" warnings for Gacha* ids. Do NOT download
  or generate other clips.
- Strings: GACHA_SKIP EN+JA via LocalizationText.csv -> import_content.py (plan -> apply
  -> publish texts -> export --check clean). Never code-only.
- Out of scope: ticket spend / server pull / history / pity, music ducking, a progress
  counter or any UI not in the Figma, GachaTabController's dead PullSection paths.

When done: list changed files with a 1-line summary each, run the acceptance
checklist in the spec, produce the smoke evidence (5 screenshots + x10 recording WITH
audio + step A-G prose + the SFX order excerpt), flag which need manual on-device
verification, update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```


- **`nav_back_memory`** (filed 2026-08-30, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  `Docs/Specs/Active/nav_back_memory/SPEC.md`. Sweep of every back/close and every OnEnable reset in the
  shell. Bugs: Missions BACK always → Home (`OpenFrom` never called); Tournament Hole Selection →
  Leaderboard → CLOSE skips to Tournament Selection; Rewards Center resets to GACHA and Leaderboard to
  DAILY on every entry; Android back does nothing. Fix: history stack + pillar memory in
  `ScreenManager` (`GoBack(fallback)`, `NavigateToPillar`), every back/close routed through it with the
  serialized target as fallback, nav slots reopen the pillar's last screen (same-pillar tap → root, D1),
  remembered tabs, compare mode exits on leave (D3), Android back → `GoBack`. In-game QUIT stays Home (D2).
  No strings, no prefab/scene edits.

### Kickoff · nav_back_memory (issued 2026-08-30)

```
Read Docs/Specs/Active/nav_back_memory/SPEC.md and implement it.

Context:
- Back/close buttons and the nav bar must return to where the player actually was.
  Add a same-pillar history stack + per-pillar "last screen" memory to ScreenManager
  (GoBack(fallback), NavigateToPillar, PillarOf/RootOf) and route every back/close
  through GoBack with the screen's existing serialized target as the fallback.
  Look at: ScreenManager.ShowScreen/ApplyScreen, PersistentUIManager.NavigateTo +
  HighlightScreen (move its ScreenId->Screen switch into ScreenManager.PillarOf),
  GachaTabController.ApplyPendingOrDefaultTab, RankingsScreenController.OnEnable,
  TournamentLeaderboard/TournamentHoleSelection.Close, MissionSelection.OnBackClicked,
  the three *CompareController.OnDisable (ForceExitImmediate), SettingsController.
- Decisions already taken (do not reopen): same-pillar nav tap -> pillar root; in-game
  QUIT keeps landing on Home (ExitToScreen callers untouched); compare mode exits on leave.
- Minimal diff. Reuse ModalController.OpenModalCount, GachaTabController.RequestStoreTab
  (add the symmetric RequestGachaTab), the existing _backScreen/_returnTarget/_backTarget
  fields as fallbacks. No new strings, no prefab or scene edits.
- Out of scope: gameplay exit routing, cross-launch persistence, scroll memory,
  Settings accordion memory.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec (A1-A16; only A10 needs a device), flag which need manual on-device
verification, quote the Mission Selection BACK control you found and the input path chosen
for Android back, update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```


- **`daily_mission_home_pill`** (filed 2026-08-30, Architect via Cowork) — ✅ **DONE 2026-08-30 (`e86edd10a`, Completed; texts v17). Kickoff below is SPENT.**
  `Docs/Specs/Active/daily_mission_home_pill/SPEC.md`. The Home-screen Daily Mission pill that
  `missions_v1` deferred: Figma `2098:8490` (with notice) / `13994:1935` (without), flame art in
  `Assets/Art/HomeScreen/`. Enters from the left, pulsing glow, y follows the notice panel, flame +
  auto-sized streak number only at streak ≥ 1, leaves on claim, old-out/new-in at UTC rollover,
  tap → Mission Selection. Same `StreakFlame` prefab replaces the text streak on the Mission
  Selection daily card. ⚠️ The Home mockups are the OLD Home layout — pill only, carousel untouched.

### Kickoff · daily_mission_home_pill (issued 2026-08-30) — ✅ SPENT, task DONE 2026-08-30. Kept for history.

```
Read Docs/Specs/Active/daily_mission_home_pill/SPEC.md and implement it.

Context:
- Home-screen "NEW DAILY MISSION!" pill (Figma 2098:8490 with notice / 13994:1935 without,
  renders in reference/). Slides in from the left, pulsing glow, y follows newsPanelRoot,
  flame + auto-sized streak number only when streak >= 1, leaves when the daily is claimed,
  old-out/new-in at UTC rollover, tap opens MissionSelection. Flame art:
  Assets/Art/HomeScreen/Flame.svg + flame.png.
- Reuse: MissionsClient.FetchDailyRoutine (DailyMissionResult: Date/RecipeHash/Claimed/Streak),
  MissionSelectionScreenController.RefreshDaily + MissionCardController.SetDailyStatus (the
  card's text streak becomes the shared StreakFlame prefab), HomeScreenController.newsPanelRoot,
  the SnapAndExpandCoroutine eased-coroutine pattern (no tween lib). Introduce one shared
  DailyMissionState so pill and card never disagree. Minimal diff.
- The Home mockups are the OLD Home layout: take only the pill and its position relative to
  the notice. Do not touch the mode carousel, promo banner or notice logic.
- Strings: HOME_DAILY_PILL (EN + JA) in LocalizationText.csv -> Tools/content/import_content.py
  (plan -> --apply -> publish -> --check clean), PIPELINE_HARDENING §24. No hardcoded .text.
- Out of scope: daily rewards/streak rules/server changes, leaderboards.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- ~~`shot_aim_parity`~~ **DONE 2026-08-28** (`b3d0c5d95` → `2b0cd5cb2`, Completed/). One `AimYawFor()` for line + shot; `AimNudgeRangeRad` removed; latch unlatches on a new low; F14.
- ~~`shot_timing_power`~~ **DONE 2026-08-29** (`4210c0891`, Completed/). Slab progress sampled at the aim latch → power ×0.70/0.90/1.0; band edges in `controls.csv`; sampleless drivers ×1.0; F15. Open for Cesar on feel: D5 putts pay the penalty (one `IsPutt` guard to exempt), band/multiplier values, HUD `× 0.xx` keep/drop. Deviation accepted: HUD branch fires in `Timing` (no `Flicking` state is ever published).
- ~~`shot_timing_telemetry`~~ **DONE 2026-08-29** (`c77c7732b` → `135442309`, Completed/). `shot_taken` carries `timing01`/`timing_mul`/`timing_band`; dashboard Flick-timing card deployed (Cloudflare version `cc9b9dd3`, stamp `c77c7732`); prod rows verified by query. Cesar step: one look at admin.golfin.world → Telemetry → Shot quality (Cloudflare Access — Code cannot sign in). Shot-controls track is closed; D5 putt + F15 tuning now decided by data.

- Parked: `Docs/Specs/Queued/flick_vector_aim_DESIGN_NOTE.md` — scheme C (aim = flick vector), Cesar leaning, revisit after the two above are felt on device.


- **`starter_restore_gate`** (filed 2026-08-29, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  `Docs/Specs/Active/starter_restore_gate/SPEC.md`. Cesar: the starter pick must survive a TestFlight
  update AND delete+reinstall. Diagnosis: the starter IS already in the `golfin_inventory` blob
  (`starter` key, `content_player_inventory`); the client races it — Login/Splash/CreateUsername read
  `NeedsStarter` from the empty local save before `InventorySyncService.Boot()` answers, and
  `CharacterManager`/`ClubManager` never re-hydrate after a restore. Fix: `StarterGate` (route only
  after the boot restore answered), `ReloadFromSave`/`RehydrateFromSave`, starter screen exits on a
  late answer. D1 (Cesar): a FAILED fetch never shows the picker — `AUTH_ERR_OFFLINE` + retry.
  No playlife change, no new strings, no prefab edits. Closes the open
  `content_player_inventory` restore-after-reinstall device pass.

### Kickoff · starter_restore_gate (issued 2026-08-29)

```
Read Docs/Specs/Active/starter_restore_gate/SPEC.md and implement it.

Context:
- Bug: after delete+reinstall a signed-in tester is asked to pick a starter again although the
  server blob already carries it. Read the Diagnosis section first — the server side is correct;
  the three post-auth routers (LoginScreenController :137/:185, CreateUsernameScreenController :85,
  SplashScreenController.RouteAuthenticated) read CharacterManager.NeedsStarter before
  InventorySyncService.Boot() has answered, and CharacterManager.LoadRoster / ClubManager.HydrateFrom
  are private one-shots that never re-run after RestoreFrom.
- Build: InventorySyncService.LastBootOutcome + OnBootFinished + OnRestored; StarterGate.Resolve
  (Assets/Scripts/UI/Account/StarterGate.cs); the three call sites go through it;
  CharacterManager.ReloadFromSave + ClubManager.RehydrateFromSave wired from InventoryCatalogAdapter;
  RosterScreenController leaves starter mode on a late answer.
- D1: a failed fetch NEVER shows the picker — reuse AUTH_ERR_OFFLINE (EN+JA exist) + retry via
  RetryBoot(). No safety timeout that could resolve Ready on an empty save.
- Minimal diff. Reuse Boot(done), BootCompleted, OnRosterChanged, OnInventoryChanged, the
  Login SetBusy/SetError pattern. Do not re-run InitializeClubs (one-shot seeding). No prefab,
  scene, CSV or playlife edits. Bot harness + demo paths must stay byte-identical.
- Out of scope: blob authority/anti-cheat, the fill-if-empty starter rule, grants mid-session.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification (the delete+reinstall
run on Cesar's iPhone is the world-check), update STATUS.md + IMPLEMENTER_REPORT.md in
the spec folder, and update Docs/AI_CONTEXT.md (also close the content_player_inventory
"restore-after-reinstall device pass" item there).
```

- **`missions_v1`** (filed 2026-08-28, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  `Docs/Specs/Active/missions_v1/SPEC.md`. Missions mode end to end: 40 component-built missions
  (Lomond, 18 holes, 4 tiers, curve verified non-decreasing), server-generated Daily Mission,
  seven content catalogs + Missions/Components/Daily admin panels, `golfin_mission_rewards`
  mirror + `/missions/claim`, start-area bake, per-session loadout override, Mission Selection
  screen cloned from Hole Selection, mode card wired on Home + Mode Select (`target=mission_select`).
  Design of record: `Docs/Game Design/MISSIONS_REDESIGN.md` + workbook. Decisions: no Home daily
  surface in v1; `missions.locked` flipped by Cesar from the admin, never in the bundled CSV.
  ✅ **DONE 2026-08-30 — moved to `Docs/Specs/Completed/missions_v1/`. Kickoff below is SPENT.** Follow-up: `daily_mission_home_pill`.
  Phases A→D in the spec; §21 live E2E at the end of C; §23 deploy ids required.

### Kickoff · missions_v1 (issued 2026-08-28)

```
Read Docs/Specs/Active/missions_v1/SPEC.md and implement it, Phase A first.

Context:
- Missions mode, end to end: 7 content catalogs (missions + 6 component tables, CSVs verbatim
  from reference/), server tables + /api/v1/missions endpoints + daily generator in playlife,
  Missions/Components/Daily admin panels, then Unity: start-area bake, MissionSession
  (spawn/pin/wind/stroke-cap/stamina override), BagManager session bag, goal evaluator,
  Mission Selection screen cloned from Hole Selection, mode card target `mission_select`
  in BOTH ModeSelectScreenController and ModeCarouselController.
- Reuse: content catalog machinery (catalogs.py, seed_from_csv.py, ContentCatalogStore overlay),
  golfin_mode_fees mirror-in-transaction pattern, /earn-game catalog-resolved amounts,
  GameSession.StrokeCapEnabled (already a Missions opt-in), GreenTopology pin candidates,
  HoleSelection prefab/controllers, Rewards panel. Minimal diff; no new UI hierarchies where
  Hole Selection already has one.
- Phase A is deployable alone with the mode still locked. Full SQL for every migration in
  chat for Cesar. Dashboard work is not done until deployed (PIPELINE_HARDENING §23 —
  quote the Cloudflare deployment id). Run the §21 live E2E at the end of Phase C.
- Strings: every new player-facing string goes into LocalizationText.csv (EN + JA) and
  reaches the `texts` catalog via Tools/content/import_content.py (plan → apply → publish →
  --check clean) — SPEC §A7. Same importer path for the modes.csv row edit. Dashboard
  strings go in lib/i18n.ts DICT (en + ja). No hardcoded .text literals.
- Out of scope: Home-screen daily badge, mission leaderboards, HoleTees.csv yardage fix,
  flipping missions.locked in the bundled CSV.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`store_banner`** — `Docs/Specs/Active/store_banner/SPEC.md` (SPEC_READY, 2026-08-28). The Store screen's hard-coded `WinterSaleBanner` becomes the fourth `game_banners` placement `store`: DB CHECK widened, backend `PLACEMENTS`, dashboard placement tables, client enum + `Button`/`BannerSlotBinder` on the existing prefab object. No live row ⇒ hidden, list closes up (A1). Deploy order: migration → backend → dashboard → client.

### Kickoff · store_banner

```
Read Docs/Specs/Active/store_banner/SPEC.md and implement it.

Context:
- Adds a 4th game_banners placement `store` for the Store screen's WinterSaleBanner
  (Assets/Prefabs/UI/Shop/GeneralShopScreen.prefab, under GridContent).
- Reuse everything from game_banners: BannerSlotBinder, BannerService, banners.py,
  the dashboard's BANNER_PLACEMENTS / BANNER_ART_SPEC tables. This is data + wiring,
  not new machinery. Mirror the Rankings prefab's Banner object component-for-component
  (Button transition None, ButtonPressFeedback, BannerSlotBinder with empty arrays).
- GeneralShopScreenController.cs stays untouched; keep the object name WinterSaleBanner.
- Migration SQL is in SPEC §1 — Cesar runs it in Supabase before backend deploy.
- Out of scope: other Store art, stamina shop, removing the Winter Sale PNG.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- ~~**`transaction_feedback`** — spend round-trips are visible (`PendingSpend` on 6 sites) and the Fly cold start is gone (`auto_stop_machines = "suspend"`, 5.20 s → 1.18 s); `[ApiClient]` now logs one timed line per request. Warm purchase 246 ms → §8 keep-alive follow-up closed.~~ **DONE 2026-08-29** — `Docs/Specs/Completed/transaction_feedback/`.

- **`progress_server_side`** (filed 2026-08-28, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. RUNS BEFORE game_modes_admin (Cesar).** Level-ups become server-authoritative on the
  shop's shape: `POST /api/v1/progress/level-up` → plpgsql `golfin_level_up()` prices from the
  PUBLISHED `level_up_costs` (LevelUpCosts.csv becomes the NINTH catalog — admin-tunable costs,
  plan §9.2 answered), debits via spend_pts, records `golfin_progress` + an idempotent event, one
  transaction. Grandfathering (Cesar decision): first level-up per (player, ref) trusts the
  claimed from_level once (`grandfathered_from` stamped, blob cross-check logged); after that,
  `level_conflict` guards the ladder. Client: ProgressService mirrors ShopPurchaseService; both
  modals get cost_changed/level_conflict UX; flag OFF byte-identical. Holes + SP allocation OUT
  (free/gameplay-derived — reasons in the spec). Legacy `character_level_up`/`club_level_up`
  reasons close later on Cesar's word. §21 live E2E + §23 dashboard deploy id both bind.
  Spec: `Docs/Specs/Active/progress_server_side/SPEC.md`.
- **`game_modes_admin`** (filed 2026-08-28, Architect via Cowork) — **✅ DONE 2026-08-28.
  Moved to `Docs/Specs/Completed/`. Shipped as the TENTH catalog, not the eighth.**
  Cesar: game-mode entry prices and rewards handled from the admin.** Two truths, two treatments:
  `modes` becomes the EIGHTH content catalog (fees, card copy, `locked`, reward display —
  `modes.csv` has never had an overlay; ModesDatabaseCSV joins the machinery; a mode whose
  `target` this build doesn't know is WITHHELD); rewards stay `game_point_actions` (already
  server-authoritative) and get a Rewards panel with audit — live-on-save, no publish cycle.
  Card reward numbers are DECOUPLED by decision (they are AVERAGES over a later selection,
  except multiplayer) — the drift warning covers exactly one pair, versus_1v1 ↔ versus_win,
  and nothing else. Cesar chose
  server-validated fees NOW: `golfin_mode_fees` mirror on publish (fail-the-publish pattern),
  spend reason becomes `mode_entry_fee:<modeId>`, /points/spend answers `fee_changed`/
  `mode_locked`/`unknown_mode` as 200 payloads, client handles FeeChanged like the shop's
  price_changed. Legacy bare reason stays until Cesar closes it (separate commit).
  Independent of content_art_bundling. Spec: `Docs/Specs/Active/game_modes_admin/SPEC.md`.
- **`content_two_way`** (filed 2026-08-27, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  Cesar's second content requirement: admin-created characters/clubs/items inform the next build,
  and CSV edits made in Unity inform the admin. One truth = published Supabase; a CSV edit is a
  PROPOSAL → `Tools/content/import_content.py` (**already exists**, `0e4fedcaa` — accepted as
  built: refuses the whole run on a dirty draft, `--overwrite-dirty`; spec adds its TESTS) →
  publish → export canonicalises. `--check` gains the value-level direction message (id-level
  already exists). Runtime rail: bundled characters/items/balls whose primary sprite is null get
  `renderable=false` and leave every visible list (`GetAvailable…`), stay in the save/blob;
  **clubs keep Placeholder (Cesar decision)**. `Validate Catalog Art` editor gate = report,
  never fail. Art-by-URL is the NEXT spec (`content_art_urls`), not this one.
  Spec: `Docs/Specs/Active/content_two_way/SPEC.md`.
- ~~**`shop_stocking`**~~ / ~~**`shop_server_purchase`**~~ — ✅ **DONE 2026-08-27** (strict build 2350,
  legacy `/points/spend` shop reason closed, fastlane freshness gate in). Move Active/ → Completed/
  if not already.
- ~~**`shop_stocking`**~~ — ✅ **DONE 2026-08-27** (`cd97bdeaa` + follow-ups). All of §8 shipped
  the same day: `+ New row` on every catalog panel, `lib/buildGates.ts`
  `SHOP_CATEGORY_STRICT_BUILD` (set to **2350**, read from `last_uploaded_build.txt` — the old
  2334 guess was 16 commits off) with validator rules G1/G2, the release lane's
  `export_content.py --check` gate, and the client withholding any row whose ref or sprite it
  cannot resolve. Build **2350 (1.5.7)** archived and on TestFlight; `shop_catalog` **v4** sells
  its first character and item rows; the admin is live at Cloudflare `b4aa4467`. `import_content.py`
  (the `content_two_way` half that made CSV-ahead drift fixable) landed alongside. Cesar bought
  Mike on 2350 — the endpoint's first-ever sale, verified in the ledger — and §2.6 closed the
  legacy `/points/spend` shop path (playlife-api v55). Spec moved to
  `Docs/Specs/Completed/shop_stocking/`.
- **`hole02_tree_bake_drift`** (filed 2026-08-27, Architect via Cowork; **REVISED same day — re-import is
  destructive, do not re-import**) — **kickoff-sized, pasteable (this block is the spec). Hole 02
  collides with 1,495 invisible Spruce.** Root cause verified: the 07-29 re-import (`4b0054069`) ran on
  another machine; its regenerated scene got Spruce (mixed-mode `TreePlacer`) and the committed bake
  (`0519c2f0`) carries them, but generated scenes are gitignored (`.gitignore:111`) so the Mac still has
  the 2026-06-01 `Hole_02_Geo.unity` with zero Spruce. Terrain trees (1,488) are safe — they live in the
  tracked `TerrainData_Hole02Geo.asset`. Cesar's decision: **rebuild the standalone Spruce from the bake**
  (position/scale/profile are in the CSV; yaw is not and does not affect collision), make standalone
  placement a **tracked file** so any machine can rebuild a scene, and add a **build-time drift gate**.

### Kickoff · hole02_tree_bake_drift (issued 2026-08-27, REVISED — supersedes the re-import version)

```
Task: hole02_tree_bake_drift — Hole 02 collides with 1,495 invisible Spruce. Rebuild them from
the bake, make standalone tree placement tracked, add a drift gate. This block is the spec.

DO NOT re-import Hole 02 (or any hole): the importer regenerates TerrainData + scene and wipes
authored tree placement. Do NOT touch TreePlacer weights or TerrainData.

FACTS (verified 2026-08-27):
- Resources/HoleData/lomond-country-club/Hole_02/tree_obstacles.csv (bake_hash 0519c2f0,
  committed 4b0054069 on 07-29) = 2,983 rows: 1,495 Spruce_1/Spruce_3 (standalone) + 1,488
  terrain trees. Columns: worldX,worldZ,baseY,scale,profileName (4 decimals).
- The Mac's Assets/Golf/Courses/lomond-country-club/Generated/Hole_02_Geo.unity (2026-06-01,
  gitignored) has no StandaloneTrees container. Its terrain trees match the CSV's 1,488
  (TerrainData is tracked). All 17 other holes' scenes match their CSVs.
- TreeObstacleBaker harvests StandaloneTrees children named {prefabName}_{n}, profile =
  prefab name with spaces→underscores ("Spruce 1" → Spruce_1). Prefabs:
  Assets/Art/3D/Trees(2025)/Trees2025_Prefabs/European/Spruce/Spruce 1.prefab, Spruce 3.prefab
  (these are what TreePlacer instantiates — TreePlacer.cs:68-72 ForceStandaloneNames).

PART 1 — tracked standalone placement + rebuild tool:
1. New tracked file per hole: Assets/Golf/Courses/lomond-country-club/Data/hole-NN-geo/
   standalone_trees.csv — columns prefab,worldX,worldY,worldZ,yawDeg,scale (one row per
   StandaloneTrees child). Add "Import/Standalone Trees/Export Current Hole" (writes it from
   the open scene) and "Import/Standalone Trees/Rebuild Current Hole" (deletes the
   StandaloneTrees container and re-instantiates every row as a plain prefab instance named
   {prefab}_{index}, in file order, under a new StandaloneTrees container parented like
   TreePlacer does). Hook TreePlacer and TreeBrushTool so every placement/brush write also
   re-exports the file (one call at the end of their apply paths — no other behaviour change).
2. Export standalone_trees.csv for all 17 holes that HAVE standalone trees from their current
   scenes (03–05, 07–18). Commit them. This makes every scene reproducible on any machine.
3. Hole 02: generate its standalone_trees.csv FROM THE BAKE — filter tree_obstacles.csv rows
   whose profileName is Spruce_1/Spruce_3, map back to prefab "Spruce 1"/"Spruce 3",
   worldY = baseY, yawDeg = deterministic per row (System.Random(20260827 + rowIndex) × 360),
   scale = scale, preserving CSV row order. Then Rebuild Current Hole on Hole 02.
4. ACCEPTANCE (the whole point): Import/Bake Tree Obstacles/Bake Hole 02 → the CSV must be
   BYTE-IDENTICAL to HEAD, bake_hash 0519c2f0. If the harvest order or 4-decimal round-trip
   breaks identity, fix the tool (not the CSV) until it is identical; if only ordering
   differs, sort inside the baker deterministically (by profile, then X, then Z) and re-bake
   ALL 18 holes so every hash is regenerated consistently — say so in the report.
5. Visual: Hole 02 tee + two shots into each tree line in the Editor (ball visibly strikes a
   tree where it stops; frames saved), then a Dev-iOS build and the same on device.

PART 2 — drift gate:
6. "Import/Bake Tree Obstacles/Validate All Holes": for each Hole_NN_Geo, open additively,
   re-harvest with the baker's harvest code (refactored into a function, no behaviour change),
   diff against the committed CSV — count per profile + positions within 1 cm. Table; any
   mismatch = error. Also flag any hole whose StandaloneTrees differ from standalone_trees.csv.
7. Wire it into CIBuild (Dev-iOS and iOS-Full): mismatch FAILS the build with hole + counts,
   like the build-stamp guard. -skipTreeBakeCheck escape hatch, logged loudly.
8. Docs (Docs/Pipeline or the Course Importer README): "Generated scenes are per-machine.
   Trees live in TerrainData (tracked) + Data/hole-NN-geo/standalone_trees.csv (tracked).
   After pulling Data/ or HoleData/ changes: Rebuild Current Hole, then Validate All Holes,
   before building. Never re-import a hole to fix trees."

Out of scope: any placement change on any hole, TreePlacer weights, Spruce rendering,
H06 heightmap, re-importing anything.

When done: list changed files; the Hole 02 bake result (hash + byte-identical yes/no); the
validator table for all 18 holes (expect 18/18 PASS); the standalone_trees.csv row counts
per hole vs scene LODGroup counts; the Editor + device frames; update Docs/AI_CONTEXT.md and
this TellCode pointer.
```


- **`shop_server_purchase`** (filed 2026-08-27, Architect via Cowork) — **SPEC_READY, kickoff pasteable.
  CONTENT_PIPELINE_PLAN §6 step 4d / §11.5: the shop price becomes AUTHORITATIVE.** New
  `POST /api/v1/shop/purchase` → plpgsql `golfin_shop_purchase()` reads the PUBLISHED `shop_catalog`
  row, prices it on the server clock (listing + sale windows, same three rules as
  `ContentShopWindow`), debits via `spend_pts` and inserts the item into `golfin_pending_grants`
  in ONE transaction; the client applies that grant through the managers, records the id in
  `appliedGrantIds`, acks. Also: `character` + `item` categories render (club-card hierarchy, no
  Figma) and buy; `ParseCategory` strict (today anything ≠ ball is a Club — latent bug);
  CHARACTERS/ITEMS chips; admin banner copy flips to "enforced for builds ≥ N". Cesar's decisions:
  Code does BOTH repos; Roster-card BUY later; legacy `/points/spend reason=shop_purchase` closed
  in a SEPARATE commit on Cesar's word. Out: stamina shop, level-ups, stockLimit, bags.
  Spec: `Docs/Specs/Active/shop_server_purchase/SPEC.md`.
- **`quality_tiers`** (roadmap `9a`, Order 900; filed 2026-08-27, Architect via Cowork) — **SPEC_READY,
  kickoff pasteable. Phase 2 of `Docs/PERF_OPTIMIZATION_PLAN.md`.** Low / Mid / High resolved from a
  device table in code (iOS `deviceModel` generation, Android GPU name + RAM + GLES3 caps, unknown → Mid),
  Settings → Graphics override (Auto/Low/Mid/High, PlayerPrefs like volume/language), `tier` +
  `tier_source` on `session_start`. Tiers = presentation only: render scale 0.6/0.7/0.8, fps 30/60/60,
  shadows 1×512×15 m / 1×1024×40 m / **2×1024×60 m** (High trimmed from 4/100 for thermal headroom —
  Cesar-judged), `maximumLODLevel` 1/0/0, tree wind off on Low (the approved `Vegetation.shader`
  `multi_compile _WIND` + per-material keyword via `TreeWindDriver`; Spruce Wind Speed → 0), Home
  bloom/HDR High only. **Never:** terrain, tree placement, cull distance, `lodBias`, basemap,
  `drawInstanced` (Option C is dead per Phase 1). Decisions of record 2026-08-27: **no thermal
  governor / no Adaptive Performance** (static tiers; telemetry decides later); **H06 heightmap density
  = separate task** `hole_heightmap_density` (Queued, spec written). Acceptance includes the fairness
  A/B (tree silhouettes identical Low vs High), per-tier cooled tables on H08/H06/H01, and a **5-minute
  H06 endurance curve per tier** — the number Phase 1 showed the static 60 cannot hold.
  Spec: `Docs/Specs/Active/quality_tiers/SPEC.md`. Move Code's `Docs/Specs/Queued/9a_quality_tiers/
  ARCHITECT_BRIEF.md` into the Active folder (git mv) — it is the Phase 1 hand-off the spec cites.
- ~~**`perf_phase1_free_wins`**~~ — ✅ **DONE 2026-08-27** (`cca3cfd1a`; every pose 60 fps cold; Option C
  dropped after measurement; the 2314 "flat terrain" proven pre-existing). Move Active/ → Completed/.

### Kickoff · progress_server_side (issued 2026-08-28) — runs BEFORE game_modes_admin

```
Read Docs/Specs/Active/progress_server_side/SPEC.md and implement it, in the spec's
§6 order (backend, content catalog, admin, Unity, live E2E).

Context:
- Level-ups become server-authoritative on the golfin_shop_purchase shape. Today
  both modals debit a CLIENT-computed totalRPCost (LevelUpModalController ~:467,
  ClubLevelUpModalController ~:445) and the level itself is client-asserted.
- Backend: migration 2026_08_28_golfin_progress.sql (golfin_progress with
  grandfathered_from + golfin_progress_events unique(user,key), RLS on/no
  policies — FULL SQL in chat for Cesar, wait for his verification) + plpgsql
  golfin_level_up(): replay → kill switches → ref active/min_build/maxLevel/
  step-cap-50 → cost summed from PUBLISHED level_up_costs rows (gap →
  costs_missing; expected mismatch → cost_changed) → level guard (row exists:
  from_level must match else level_conflict; absent: GRANDFATHER — seed at the
  claimed level, stamp grandfathered_from, best-effort blob cross-check in an
  exception block, log + include blob_level, never block) → spend_pts with reason
  progress:<kind>:<ref>:L<to> → record, one transaction. routers/progress.py at
  /api/v1/progress, all business outcomes 200, no _missing_relation courtesy.
- Content: LevelUpCosts.csv becomes the NINTH catalog ("level_up_costs", id
  column "level") — catalogs.py row, seed via seed_from_csv --catalogs, Level
  Costs panel on the shared CatalogPanel (240 rows, pagination), validation:
  cost_r/sp_reward ≥ 0, level unique, CONTIGUOUS coverage to max(maxLevel) —
  blocking. CharacterLevelUpDatabase gains the standard overlay.
- Unity: ProgressService in Golfin.Economy mirroring ShopPurchaseService (flag
  gate inside the routine, own latch, fold via ApplySpendResult in finally AFTER
  onDone). Both modals: flag ON → LevelUpAsync(kind, ref, from, to, totalRPCost,
  ContentBuildNumber.Current); Ok → existing commit body unchanged; CostChanged →
  reload cost DB + rebuild preview + price-updated toast, second CONFIRM pays;
  LevelConflict → toast + close + InventorySyncService.MarkDirty; Insufficient/
  Unavailable → PointsSpendGate's two toast consts. Flag OFF byte-identical.
- §21: the live E2E is acceptance item 1 (real level-up on prod: ledger row +
  grandfathered progress row + event verified by SQL; then publish a cost change,
  stale client gets cost_changed, second tap pays the new sum) — it must RUN.
- DEPLOYMENT IS PART OF THE TASK (spec §6, three proofs required in the report,
  each an automatic architect FAIL if missing): STEP 0 = deploy the outstanding
  dashboard backlog (15f2553f1 upload UI, c15998c30 WebP-only, 541864b38 badge)
  PLUS the /api/version + footer commit stamp, id quoted, Access curl 302;
  STEP 1 ends with flyctl deploy + image id via flyctl status; STEP 3 ends with
  npm run deploy again for the Level Costs panel, id quoted, /api/version == HEAD
  by curl. Not done without all three.
- Minimal diff. Reuse: golfin_shop_purchase's function structure and refusal
  style, spend_pts, the shared CatalogPanel, ApplySpendResult, ContentBuildNumber,
  PointsSpendGate's toast consts, test_shop_purchase's fake-Supabase style.
- Out of scope: hole unlocks + SP allocation (reasons in the spec), closing the
  legacy character_level_up/club_level_up reasons (separate commit on Cesar's
  word), game_modes_admin, stamina shop, blob/merge changes.

When done: list changed files (both repos) with a 1-line summary each, run the
acceptance tests incl. the live E2E, quote the Cloudflare deployment id, update
STATUS.md + IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```

### Kickoff · game_modes_admin (issued 2026-08-28) — ✅ DONE 2026-08-28

```
Read Docs/Specs/Active/game_modes_admin/SPEC.md and implement it, in the spec's §5 order
(backend fee validation first, then catalog, admin, Unity).

Context:
- modes.csv becomes the eighth content catalog: Catalog("modes", ...) in
  Tools/content/catalogs.py (export/import/--check pick it up from the table),
  seed migration for content_catalogs + 5 rows at v1, Modes panel via the shared
  CatalogPanel (+ New row works automatically), ModesDatabaseCSV gains the overlay
  exactly as content_overlay_catalogs did the others. Withhold rule: a mode whose
  `target` the build doesn't dispatch is withheld with a warning — read the real
  target set from ModeSelectScreenController's dispatch, don't hard-code it twice.
- Rewards: NEW Rewards panel editing game_point_actions directly (checkAdmin +
  writeAudit, before/after; live on save, the panel says so). pts blank = NULL =
  client-supplied-under-caps — hint EN+JA. No new/deleted actions. Card reward
  numbers are DECOUPLED card copy (averages over a later selection) EXCEPT
  versus_1v1: the drift warning covers only versus_1v1 ↔ versus_win — do not
  generalise it into a mapping table.
- Fees, server-validated: migration 2026_08_28_golfin_mode_fees.sql (RLS on/no
  policies, seeded, verification block — FULL SQL in chat for Cesar, wait for his
  output). Modes publish upserts the mirror in the same transaction and FAILS if
  the mirror write fails (golfin_characters pattern). /points/spend: reason prefix
  mode_entry_fee: → parse mode id; unknown_mode / mode_locked / fee_changed(+fee)
  as 200 payloads, nothing debited; matching amount falls through to spend_pts
  unchanged; BARE mode_entry_fee stays accepted (closing it = separate commit on
  Cesar's word). Client: reason becomes "mode_entry_fee:" + _data.id at
  ModeCardController.cs:604; new SpendOutcome verdict FeeChanged → update the
  card's fee, toast, second tap pays; unknown/locked → generic refusal + refresh.
- PIPELINE_HARDENING §21: the live E2E is acceptance item 1 (publish a fee change,
  stale client gets fee_changed, second tap debits the new fee) — it must RUN.
- Minimal diff. Reuse: the shared CatalogPanel/RowEditor, contentValidate patterns,
  golfin_characters mirror shape, shop price_changed UX, test_golfin_inventory
  fake-Supabase style, seed_from_csv --catalogs.
- Out of scope: tournament fees/prizes, new earn actions, stamina/gacha prices,
  closing the bare reason, LevelUpCosts.

When done: list changed files (both repos) with a 1-line summary each, run the
acceptance tests incl. the live fee E2E, update STATUS.md + IMPLEMENTER_REPORT.md,
and update Docs/AI_CONTEXT.md.
```

### Kickoff · content_art_bundling (issued 2026-08-28) — run after Cesar approves content_art_urls DONE

```
First: on Cesar's word, move content_art_urls to Docs/Specs/Completed/ (STATUS DONE)
and git mv Docs/Specs/Queued/content_art_bundling Docs/Specs/Active/content_art_bundling.

Then read Docs/Specs/Active/content_art_bundling/SPEC.md and implement it. The
Architect's corrections are FOLDED INTO the body (§10 is the record) — the spec as
written is current.

Context:
- Editor tool (GOLFIN/Content/Fetch URL Art + a MenuItem-free static entry) that
  pulls URL-only art into Resources/ as a reviewable git diff. NOT in the build
  lane; no Supabase credentials (public HTTPS + repo CSVs only).
- Naming: match the RESOURCES convention per folder (Zoe / BigRosterZoe /
  {Pascal(name)}-{rarity} for items+balls / {Type}-{Brand} for clubs) — NOT the
  S_* source-art patterns; add the items/balls rule to ASSET_NAMING_CONVENTION.md
  in the same commit. De-dup shared club art by derived name, fetch once.
- Refusals: allowlist via CatalogArtPolicy.IsArtAllowed (never re-implemented);
  500 KB upload cap (not the 1 MB client backstop); WebP by content type AND
  extension; collision never overwrites; empty reference folder never guesses.
- Import settings copied from a sibling in the SAME folder, re-read after import,
  format + maxTextureSize asserted non-default.
- CSV gains the sprite name; closing instruction = import_content.py --apply →
  publish → export. Size summary appended to Docs/Reports/content_art.txt.
- Admin: "URL-only · not bundled" badge (row list + editor, EN+JA) on any row with
  a URL and an empty name.
- Acceptance includes: re-run is a no-op; OLD-build half (strip the bundled file,
  keep name+URL → renders via HasRemote + cached URL — the case most likely to
  regress); six club rarity rows → one download; the ladder hands over to rule 2
  with the sprite identity logged, per PIPELINE_HARDENING §21 this E2E must RUN.
- Minimal diff. Out of scope: retiring bundled art, homeUrl (filed follow-up),
  3D/hole content, Addressables, clearing URLs after bundling.

When done: list changed files with a 1-line summary each, run the acceptance tests
(Editor only — no device pass by default), paste the size summary, update STATUS.md
+ IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```

### Kickoff · content_two_way (issued 2026-08-27)

```
Read Docs/Specs/Active/content_two_way/SPEC.md and implement it, in the spec's §8 order
(Unity + admin first, then the tooling).

Context:
- Two-way content loop with ONE truth: published Supabase. Admin → build already
  works for data (+ New row, publish, export, fastlane gate). CSV → admin EXISTS:
  Tools/content/import_content.py (0e4fedcaa) is accepted as built — refuse-the-run
  on a dirty draft, --overwrite-dirty, --min-build default count+1 — do NOT redesign
  it. What is missing: Tools/content/tests/test_import_content.py (stdlib unittest,
  fake PostgrestClient shared with export) incl. the round-trip property.
- export_content.py --check: id-level direction already exists; add the VALUE-level
  half — "imported, not yet published" when a draft equals the CSV, else "if you
  edited the CSV run import; if not, export". Exit code unchanged. Update
  Tools/content/README.md + Docs/TESTFLIGHT_RUNBOOK.md.
- Client rail (the invariant game-wide): CharacterDataRuntime / ItemDataRuntime /
  BallDataRuntime gain `renderable` = primary sprite resolved (portraitSprite /
  thumbnailSprite / thumbnailSprite) using the resolution the loaders already do;
  GetAvailable… = isActive && renderable; GetAll… untouched (owned-but-unrenderable
  rows must survive the save and InventoryCodec). Switch VISIBLE-list consumers
  (CharacterManager.cs:82/99 roster seed, MatchmakingModalController.cs:256,
  ItemManager.cs:56 — grep every GetAll* call site first) to GetAvailable….
  GeneralShopCatalog.Admit reads `renderable` instead of re-resolving. CLUBS KEEP
  PLACEHOLDER — do not touch ClubDatabaseCSV's fallback.
- Editor gate Assets/Editor/ContentArtValidator.cs + CIBuild step beside
  ValidateTreeBake(): report per catalog of rows with missing sprite columns, written
  to Docs/Reports/content_art_<build>.txt. WARNING ONLY, never fails the build.
- Admin: sprite-field hints (EN+JA) in RowEditor for characters/items/balls/clubs
  naming the Resources folder; amber banner on the Characters panel. No new control.
- Tests: the importer tests above; EditMode tests for renderable / GetAvailable /
  InventoryCodec survival of an owned-but-unrenderable character.
- Minimal diff. Reuse: catalogs.py, rest.py, export_content.drift_report,
  ContentSpriteGuard, the club loader's summary-log shape, the shop banner component.
- Out of scope: art by URL / admin art upload (next spec content_art_urls),
  LevelUpCosts / gacha / bot CSVs as catalogs, any endpoint or blob change.

When done: list changed files with a 1-line summary each, run the acceptance tests in
the spec (Editor only — no device pass by default), run the import DRY-RUN against
prod and paste its output (expected: no drift), update STATUS.md +
IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

### Kickoff · shop_stocking (issued 2026-08-27) — ✅ SPENT, task DONE 2026-08-27. Kept for history.

```
FIRST: commit the shop_server_purchase Unity half (Assets/Scripts/Economy/
ShopPurchaseService.cs and the rest are untracked in the working tree) — its own
commit, its own message. Then:

Read Docs/Specs/Active/shop_stocking/SPEC.md and implement it, in the spec's §8 order.

Context:
- Stocking the shop from the admin, end to end. Three verified gaps: no "+ New row"
  control in CatalogPanel/RowEditor (backend upsert + publish already handle a new
  rowId); the testflight_build lane never runs export_content.py --check; the client
  admits shop rows whose ref or sprite it cannot resolve and renders a blank card.
- Admin: "+ New row" on the shared CatalogPanel (all catalogs), rowId editable only
  while exists === false, validated server-side in upsertDraftRow (regex, ≤80,
  unique across drafts AND published → 409), ID column written from rowId, shop
  prefill rowId = "shop_" + refId after a RefPicker pick, audit action
  content_row_create, EN+JA DICT, mock store parity. New lib/buildGates.ts with
  SHOP_CATEGORY_STRICT_BUILD = 0 (move the panel's SERVER_PRICE_ENFORCED_FROM_BUILD
  there; the 2334 guess is wrong — build number is commit count). Validator rules
  G1 (non-club/ball rows need minBuild ≥ the constant; constant 0 → error) and G2
  (shop minBuild ≥ referenced row's min_build). Banner copy for both states.
- Backend: NEW migration 2026_08_28_shop_purchase_ref_min_build.sql that
  create-or-replaces golfin_shop_purchase() with the added ref_min_build refusal.
  Never edit the applied migration. Print the FULL SQL in chat for Cesar; wait for
  his verification output; deploy with ~/.fly/bin/flyctl; confirm via flyctl status;
  §2.5 smoke from shop_server_purchase.
- fastlane: sh("python3 ../Tools/content/export_content.py --env-file
  ../Tools/admin-dashboard/.env.development.local --check") right after
  assert-unity-closed.sh; non-zero aborts; NO auto-export. Update
  Docs/TESTFLIGHT_RUNBOOK.md with the export → commit → rerun loop.
- Client: GeneralShopCatalog.Admit resolves the ref in the matching DB
  (Club/Ball/Character/ItemDatabaseCSV) and requires a non-Placeholder primary
  sprite; unresolvable → withheld + LogWarning + counted; null DB singleton → skip
  with one log (EditMode must not become withhold-everything). GeneralShopCard.Bind*
  null-row branch → SetActive(false) + LogError.
- Minimal diff. Reuse: upsertDraftRow, writeAudit, RefPicker, ctx.otherCatalogs,
  export_content.py --check, RequireReady pattern, the existing summary log line.
- Out of scope: art upload / art-by-URL, import_content.py, golfin_characters
  mirror, stockLimit/minPlayerLevel, Roster card, stamina shop, closing the legacy
  /points/spend reason (still on Cesar's word only).

When done: list changed files (both repos) with a 1-line summary each, run the
acceptance tests in the spec (Editor with a seeded overlay cache — no device pass by
default), update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

### Kickoff · shop_server_purchase (issued 2026-08-27)

```
Read Docs/Specs/Active/shop_server_purchase/SPEC.md and implement it. Two repos, in the
spec's §5 order: playlife FIRST (/Users/cesar/Documents/playlife), then GolfinRedux.

Context:
- Makes the shop price authoritative. Today ShopTransaction.TryPurchaseCatalogEntry
  debits the client-computed EffectiveRpCost through PointsSpendGate and grants
  locally. New POST /api/v1/shop/purchase (routers/shop.py) → plpgsql
  golfin_shop_purchase(): published content_rows shop_catalog row, server-clock
  windows (mirror ContentShopWindow's three rules), spend_pts debit + a
  golfin_pending_grants insert in ONE transaction, idempotent by key via a new
  golfin_shop_purchases table. Every business outcome is HTTP 200 (ok /
  insufficient / price_changed / not_listed / already_owned).
- Client: new Golfin.Economy ShopPurchaseService (mirror PointsService: flag gate
  inside the routine, own latch, fold total_points AFTER onDone via a new public
  PointsService.ApplySpendResult). Flag ON = server call only, no local price;
  flag OFF = existing path byte-identical. Apply the returned grant through the
  managers (ClubManager.GrantClub / new CharacterManager.UnlockCharacter /
  ItemManager.AddItems / GrantBall), record grant.Id in SaveData.appliedGrantIds,
  InventorySyncService.MarkDirty, ack. Never InventoryGrants.Apply mid-session.
- ShopCategory += Character, Item; GeneralShopCatalog.ParseCategory becomes strict
  (drop + warn, never default to Club). GeneralShopCard.BindCharacter/BindItem on the
  GeneralShopCard_Club hierarchy per SPEC §3.4 (RarityStatCaps for bar scale, no
  hard-coded 60). CHARACTERSChip + ITEMSChip duplicated from BALLSChip.
- Admin: only lib/i18n.ts sh.notice.* copy (EN+JA) + amber style + build constant.
- Minimal diff. Reuse: spend_pts, golfin_pending_grants, InventoryGrants id ledger,
  ContentBuildNumber.Current, PointsSpendGate's two toast consts, RarityStatCaps.
- SQL RULE: print the FULL migration SQL in your message for Cesar to paste into the
  Supabase editor; wait for his verification output before deploying. Deploy with
  ~/.fly/bin/flyctl from the Mac; confirm the image via flyctl status; smoke per §2.5.
- Out of scope: stamina shop, level-ups/hole unlocks, Roster-card BUY, stockLimit /
  minPlayerLevel, bags, IAP. Do NOT close the legacy /points/spend shop_purchase
  reason in this task (§2.6 is a separate commit on Cesar's word).

When done: list changed files (both repos) with a 1-line summary each, run the
acceptance tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

### Kickoff · quality_tiers (issued 2026-08-27)

```
Read Docs/Specs/Active/quality_tiers/SPEC.md and implement it.

Context:
- Phase 2 of the perf plan: Low/Mid/High tiers. Three URP assets (Mobile_RPAsset becomes
  High in place — keep its GUID; Low/Mid are duplicates), three Quality levels (Low=0,
  Mid=1, High=2, PC=3; platform default Mid), QualityTierService + QualityTierResolver in
  Golfin.Gameplay.UI (Assets/Scripts/Gameplay/UI/ShotUI/Quality/ — verified: Physics.Viewer
  references Gameplay.UI, and Gameplay.UI already references URP runtime; Assembly-CSharp
  is NOT reachable from asmdefs). Boot at AfterSceneLoad, after FramePacingBootstrap.
- Tree wind off on Low: edit the 5 `#pragma shader_feature _WIND` in
  Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader to `multi_compile _ _WIND`
  (approved), then toggle PER MATERIAL through TreeWindDriver (a global
  Shader.DisableKeyword cannot override a material-enabled keyword). Spruce: set
  Vector1_b0ddedae341d4c7ba1d429299f3078ea (Wind Speed) to 0 on Spruce_1/Spruce_2.mat.
- Settings UI: duplicate the Language submenu (LanguageSubmenu.cs + prefab) into a Graphics
  item with Auto/Low/Mid/High; new SETTINGS_GRAPHICS / SETTINGS_QUALITY_* keys EN+JP.
- Fairness rule is hard: tiers never change terrain, tree placement, cull distance, lodBias.
  Option C (basemap/drawInstanced) is dead — do not touch terrain settings.
- Minimal diff. Reuse PerfBaselineBot (jobs from index 14, `tier=` in job.txt, 5-minute H06
  endurance jobs). Pinned sky + yaw + 3 runs; fps/frameMs are the verdict.
- Out of scope: thermal governor / Adaptive Performance, H06 heightmap (own task), Spruce
  conversion, in-game gear control, audio/textures, Hole 02 trees.

When done: list changed files with a 1-line summary each, run the acceptance checklist
(resolver tests, override round-trip, fairness A/B frames Low vs High, per-tier cooled
tables H08/H06/H01, 5-min H06 endurance per tier, wind on/off proof, Home bloom, build-size
delta, telemetry fields), flag the three Cesar-judged items (High shadows 2/60 look, fairness
A/B, aim-arrow feel at 30 fps on Low), append §12 to Docs/Reports/perf_baseline_2026-08-26.md,
update STATUS.md + IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```


- **`perf_phase1_free_wins`** (filed 2026-08-26, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. Phase 1 of `Docs/PERF_OPTIMIZATION_PLAN.md`.** Phase 0b (`Docs/Reports/
  perf_baseline_2026-08-26.md` §10) measured, cooled + pinned + 3-run + frame-verified, on Hole 08:
  (a) ShellScene camera off **26.11 → 14.48 ms render thread, 30.1 → 59.8 fps**; (d) DecalRendererFeature
  removed via the asset **→ 15.05 ms**; (a+d) **→ 14.09 ms, 7,375 → 2,430 batches, 5.0 M → 1.4 M tris**;
  (c) terrain basemap 100 + instanced **−6.31 ms**. (a) also holds 60 fps at thermal Serious. This spec
  ships a + d + c, normalises the 5000/50 tree draw distance on holes 01/02/06 to 150/80 at runtime
  (fairness, plan §2), guards the two MapView `ReadPixels`, and chases the Development Console spam
  visible in `exp_ad_CORRECT.png`. **No scene edits** — terrain values are set at hole load.
  ⚠️ Two traps carried into the spec: runtime-disabling a renderer feature renders the terrain black
  (§10.3 — asset edit only), and `OnHoleUnloaded()` never fires in a player build (`LabHoleBinder` is
  editor-only) so the restore goes in `OnDestroy` — which also fixes the shell light never coming back.
  Spec: `Docs/Specs/Completed/perf_phase1_free_wins/SPEC.md`.
- ~~**`perf_baseline_experiments`**~~ — ✅ **DONE 2026-08-26 for everything that gates Phase 1** (report
  §10). Leftovers (e) mid-flight, Instruments trace, Memory Profiler top-10, GC call stack roll into
  `perf_phase1_free_wins` §5 (GC/console) and Phase 4 (memory). Superseded pointer kept below.

### Kickoff · perf_phase1_free_wins (issued 2026-08-26)

```
Read Docs/Specs/Completed/perf_phase1_free_wins/SPEC.md and implement it.

Context:
- Ships the Phase 0b wins: ShellScene camera disabled during a hole (mirror
  PhysicsLabController.DisableShellDirectionalLight, :2475, called at :2196), the
  DecalRendererFeature REMOVED from Assets/Settings/Mobile_Renderer.asset (asset edit +
  rebuild ONLY — runtime SetActive(false) renders the terrain black, report §10.3), and
  terrain basemapDistance 100 / drawInstanced / treeDistance 150-80-20 applied at hole load
  via Terrain.activeTerrain (no scene edits — this also fixes holes 01/02/06's 5000/50).
- Restore camera AND light from PhysicsLabController.OnDestroy(): LabHoleBinder only calls
  OnHoleUnloaded() under UNITY_EDITOR, so in a player build nothing restores today.
- Guard MapViewController's two DoFrameReadbackAndDump calls (:525, :2318) UNITY_EDITOR.
- Water: URPWater _EDGEFADE_ON loses _CameraDepthTexture when the decal CopyDepth goes;
  check Hole 08/13 shorelines, decide per spec §2 NOTE, report the delta.
- Minimal diff. Reuse PerfBaselineBot for the acceptance captures (cooled, pinned yaw,
  3 runs, median + raws, frame PNG each). Expect Mobile_RPAsset m_PrefilterDBufferMRT3 to
  churn — commit it; diff ALL of Assets/Settings/ and nothing else may change.
- Out of scope: tier system (9a), shadow cascades, LOD level, Vegetation.shader, Spruce,
  audio/textures/HoleData, Hole 02 invisible trees, Hole 06 heightmap density, any .unity edit.

When done: list changed files with a 1-line summary each, run the acceptance
checklist in the spec (device tables before/after for Holes 08/01/06 + mid-flight,
Frame Debugger one-camera/no-prepass proof, teardown paths i–iv, water decision,
Hole 01 tree-distance before/after), append §11 to
Docs/Reports/perf_baseline_2026-08-26.md, flag which items need Cesar's on-device
eyes, update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```


- **`perf_baseline_experiments` / Phase 0b** — ✅ **EXPERIMENT SWEEP DONE 2026-08-26**, §10 of
  [`Docs/Reports/perf_baseline_2026-08-26.md`](Docs/Reports/perf_baseline_2026-08-26.md).
  Protocol built and used: cooled device, **pinned camera yaw**, 3 runs + median with all raws, iOS
  `NSProcessInfo.thermalState` logged per run, on-device `ProfilerRecorder` counters (no Editor in the
  loop), and **a PNG saved with every measurement**.
  **ANSWER — biggest win in render-thread ms: (a) ShellScene camera off, 26.11 → 14.48 ms (−11.63),
  Hole 08 30.1 → 59.8 fps — and fps stops depending on temperature (59.8/60.2/59.7 at
  Nominal/Fair/Serious).** (d) decal feature off, done via the asset: −11.06 ms. **(a+d) together:
  −12.02 ms, batches 7,375 → 2,430, triangles 5.03 M → 1.41 M, 59.8 fps** — the Phase 1 first commit,
  now evidenced. (c) −6.31 ms. (b) −3.69 ms.
  ⚠️ **A renderer feature CANNOT be A/B'd by `SetActive(false)` at runtime** — (d) that way rendered a
  fully BLACK terrain, logged no error, and read as a 2× win; caught only because Cesar looked at the
  phone. Re-tested via `m_Active: 0` in the asset + rebuild, then reverted — and reverting needs TWO
  files, because the build rewrites `m_PrefilterDBufferMRT3` into `Mobile_RPAsset.asset`.
  H06's 6.3 M tris CONFIRMED as heightmap density (2049² on 229×101 m ≈ 7× the samples/m² of H08) →
  fix at import. §9's H06 "20.0 fps" was throttling; cooled it is 35.2 fps.
  Harness to keep: `Assets/Scripts/Dev/PerfBaselineBot.cs` + `Assets/Plugins/iOS/GolfinThermal.m`.
  **STILL OWED:** (e) `maximumLODLevel=1` mid-flight, H08 mid-flight baseline, Instruments Metal
  System Trace, Memory Profiler top-10 + load-spike GC column, GC call stack behind ~29 KB/frame.
  *(Original Phase 0b brief follows.)*
- ~~**`perf_baseline_experiments`**~~ (superseded pointer, see DONE note above) — **Phase 0b. Finishes what
  `perf_baseline_capture` owed. Measurement only. Kickoff pasteable.** Phase 0 (`Docs/Reports/
  perf_baseline_2026-08-26.md`) confirmed all five plan items on device — 31 % of render events belong
  to the ShellScene camera, DepthNormals prepass on both cameras with zero decals, 48.8 fps cold on the
  EASIEST hole on an A17 Pro, Home 778 MB → Hole 08 1,370 MB — but stopped before the five A/B
  experiments, the thermal-state proof, the Memory Profiler top-10 and any real GPU timing (Unity
  cannot report GPU ms on Metal). Architect verdict + plan corrections: `PERF_OPTIMIZATION_PLAN.md` §8.

### Kickoff · perf_baseline_experiments (issued 2026-08-26) — Phase 0b, measurement only

```
Read Docs/PERF_OPTIMIZATION_PLAN.md §8 and Docs/Reports/perf_baseline_2026-08-26.md §9,
then finish Phase 0. Measurement only — no fix is committed; every experiment is reverted.

FIRST, make captures comparable (Phase 0 §9.4 showed single captures are not evidence):
1. PerfBaselineBot: add a pinned camera yaw at POSE_READY (serialize the yaw it lands on
   once, reuse it every run) and log ProcessInfo.thermalState (iOS native, tiny
   [DllImport("__Internal")] in the DevHarness assembly, GOLFIN_TESTBUILD-gated like the bot)
   in the same [PerfBot] line as the frame stats. Also log the last 60-frame median of
   Profiler render-thread ms so the number lands in the device log without the Editor.
2. Protocol for EVERY number below: device cooled (screen off ≥ 8 min, thermalState must
   read Nominal), same pinned pose, 3 runs, report the median AND the three raw values.

THEN:
A. Re-take the baseline on Hole 08 tee and Hole 06 tee under that protocol (Phase 0's H08
   and H06 numbers were throttled). Add H08 mid-flight (driver) under the same protocol.
B. Experiments, each alone, each reverted before the next, Hole 08 tee, 3 runs:
     a) ShellScene Main Camera disabled during the hole
     b) Mobile_RPAsset cascades 4→1, shadow distance 100→40
     c) Terrain basemapDistance 1000→100 + drawInstanced ON
     d) DecalRendererFeature removed from Mobile_Renderer
     e) QualitySettings.maximumLODLevel = 1 — measure mid-flight, not at the tee
     a+d) together (they are the Phase 1 first commit; measure the pair)
   Report per experiment: fps, wall ms, render-thread ms, batches, tris, shadow casters,
   before/after, medians + raws.
C. GPU timing: one Xcode Instruments "Metal System Trace" (or GPU counters) capture on the
   Hole 08 tee baseline and on a+d. Report GPU frame time and the top 3 encoders by time.
   If Instruments cannot attach, say so and report render-thread ms as the proxy.
D. Memory Profiler snapshot after H01 → H08 → H06 (cooled protocol not required): Texture2D /
   Mesh / AudioClip / Managed totals, top 10 objects by size, and the GC Alloc column across
   the Hole 08 load frames (expected ~32.8 MB managed spike). State what the +590 MB
   Home→Hole 08 is made of.
E. GC: Profiler CPU module, Hole 08 tee, one frame's GC Alloc call stack — name the
   allocator(s) behind the ~29 KB/frame.
F. Hole 06 6.3 M tris: read TerrainData_Hole06Geo size + heightmapResolution and the terrain
   Profiler "Terrain.Render" tri count; confirm or refute the heightmap-density hypothesis.

Out of scope: any fix, any tier code, any committed scene/asset change, Vegetation.shader,
Spruce conversion, the Hole 02 invisible-tree bug (§5.1 — own task).

When done: append §10 to Docs/Reports/perf_baseline_2026-08-26.md with every table above,
state which of a–e (and a+d) is the biggest win in render-thread ms, update Docs/AI_CONTEXT.md,
and update this TellCode pointer. Bot changes are the only code that may be committed.
```


- **`content_cleanup_quick`** (filed 2026-08-26, updated after Phase 4) — ✅ **DONE 2026-08-26,
  awaiting Cesar's sign-off.** All five items implemented directly by Claude Code. Report:
  `Docs/Specs/Active/content_cleanup_quick/IMPLEMENTER_REPORT.md`. Unity EditMode 1765/1768 (0
  failed, 3 pre-existing skips), playlife backend 26 passed, dashboard build clean; every new
  suite proven with a tripwire. ✅ **DEPLOYED** — `playlife-api` v53, `golfin-admin` `cf90ee8a…`;
  the per-catalog `enabled` is measurably gone from the live response. Item 3 turned out to be
  FOUR harnesses, not three. Original brief below.

  **FIVE small items, no spec folder. The last thing before the batched device pass.**
  1. **Drop the per-catalog `"enabled"` field** from the payload and its client DTOs. Disabled ⇒
     absent, so it can only ever be `true`, and a tautologically-true boolean gets misread as a
     guard. Keep top-level `disabled[]`; `IsDisabled(name)` already reads it. Architect override of
     the keep-both recommendation — the window closes at first release.
  2. **Global kill switch has no dashboard control.** `lib/contentMutations.ts` toggles per-catalog
     `is_enabled` only, so `content_settings.content_enabled` needs a SQL update — which fails
     §7.4's "one flag, no deploy". Add the button.
  3. **Shared `TestBoot.SaveDataHost()` helper** — three EditMode harnesses fake a host boot and any
     future boot-order assert hits all three again.
  4. **Revoke an UNAPPLIED grant** from the Users drawer (`CONTENT_PIPELINE_PLAN.md` §6.5 decision
     3). Grants are additive-only with no subtraction, so a fat-fingered grant is PERMANENT once
     drained, fixable only in SQL. Revoking before it drains is the cheap half. No separate panel.
  5. **Log every merge that RAISES a quantity**, with player + item (§6.5 decision 1). The additive
     merge can refund a consumed item on a rev mismatch; that is accepted for the beta, but beta
     consumption numbers are what tune the economy, so the refund path must be counted rather than
     assumed rare. Near-zero keeps §6 step 4d a launch-gate; anything else moves it up.

- **`content_player_inventory`** (filed 2026-08-26, Architect) — **SPEC_READY. PHASE 4, the last
  piece of Cesar's original ask.** Collapsed into ONE spec (Cesar 2026-08-26: testers only, no real
  players, so the 4a→4b→4c ladder is ceremony). `profiles.golfin_inventory` JSONB +
  `golfin_inventory_rev`; ⚠️ **NOT `user_inventory`, which already exists and is the partner app's
  gift inventory** (`routers/gifts.py`). One blob per user, **deltas-from-default only** (Cesar's
  cost constraint). Write-behind ≤1 PUT/30 s + pause. **Additive merge stays** even though tester
  inventories are expendable — it is what keeps loss DIAGNOSTIC (missing item = bug; under
  last-write-wins loss is sometimes correct and you cannot tell), and it is the hardest thing to
  change once real players exist. Plus a `golfin_pending_grants` queue (idempotent by grant id,
  additive-only) and an admin inventory tab. **Not anti-cheat** — say so on the panel, same as
  prices. Spec: `Docs/Specs/Active/content_player_inventory/SPEC.md`.

### Kickoff · content_player_inventory

```
Read Docs/Specs/Active/content_player_inventory/SPEC.md and implement it.

Context:
- PHASE 4, the last piece. ONE spec, not the 4a/4b/4c ladder — Cesar confirmed
  testers only, no real players, so the phasing that bounded blast radius on
  real inventories is ceremony.
- profiles.golfin_inventory JSONB + golfin_inventory_rev. ⚠️ Do NOT reuse
  user_inventory — it exists and is the PARTNER APP's gift inventory
  (routers/gifts.py). Different concern.
- One blob per user, deltas-from-default only. A default-state club is just its
  id. That is Cesar's cost constraint from day one, and it also means catalog
  rebalances propagate to untouched instances for free.
- Do NOT duplicate what the server already owns: RP balance, the leaderboard
  accumulators, tournament entries.
- Additive merge (union ids, max levels/quantities, never subtract) STAYS. Not
  because tester inventories are precious — because it keeps loss diagnostic. A
  missing item is then unambiguously a bug; under last-write-wins loss is
  sometimes correct and you cannot tell which. Hardest thing to change later.
- Write-behind: at most one PUT per 30s plus one on pause/quit. Never per
  mutation.
- golfin_pending_grants: drain at boot, ack, idempotent by grant id,
  additive-only.
- This is sync and backup, NOT anti-cheat. A modified client can still grant
  itself anything. Print that on the admin panel, same as the shop's price
  notice.
- Out of scope: server-authoritative purchases, Addressables, art URLs,
  LevelUpCosts, any content-endpoint or catalog change.

When done: list changed files with a 1-line summary each, run the acceptance
tests, update STATUS.md + IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```

- **`content_kill_switch_and_order`** (filed 2026-08-26, Architect) — **SPEC_READY. GATES THE
  PHASE-2 DEVICE PASS.** Two small pre-existing fixes, both verified against prod.
  (1) Top-level `enabled` is an AND across the REQUESTED catalogs, so disabling ONE catalog
  reverts EVERY catalog to bundled on every client — a per-catalog kill with global blast radius.
  Fix: per-catalog `"enabled": false`, top-level reserved for a real global kill. Disabled ⇒ absent
  is CORRECT and Phase 2 depends on it — do not change that.
  (2) `CharacterManager` and `SaveDataHost` are both −100; the tie means the Phase-2 clamp silently
  may not run. Move `CharacterManager` to −95 + assert.
  Spec: `Docs/Specs/Active/content_kill_switch_and_order/SPEC.md`.

### Kickoff · content_kill_switch_and_order

```
Read Docs/Specs/Active/content_kill_switch_and_order/SPEC.md and implement it.

Two small fixes, both pre-existing, both verified against prod:

1. Top-level `enabled` is an AND across the REQUESTED catalogs. ContentService
   requests all six and drops the cache on enabled:false, so disabling ONE
   catalog reverts EVERY catalog to bundled. Make `enabled` a real global flag
   and add per-catalog `"enabled": false` (your client is already written for
   it). Disabled-means-absent is CORRECT — Phase 2's WITHDRAWN handling depends
   on it, verified on prod. Do not change that.
2. CharacterManager and SaveDataHost are both at -100. CharacterManager reads
   the save, so the tie means the clamp silently may not run. Move
   CharacterManager to -95 and assert SaveDataHost ran first.

This gates the Phase-2 device pass — testing the per-catalog kill before this
lands would pass while doing something much larger than intended.

Out of scope: the .cs.meta-only execution-order fragility in general (its own
task), boot cost, inventory, Addressables, art URLs.

When done: list changed files with a 1-line summary each, run the acceptance
tests, update STATUS.md + IMPLEMENTER_REPORT.md, and update Docs/AI_CONTEXT.md.
```

- **`content_overlay_catalogs`** (filed 2026-08-26, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. PHASE 2 — clubs / characters / items / bags / balls / shop_catalog.** Phase 1
  (`content_overlay_texts`) is DONE: `Golfin.Content` exists, `min_build` resolved to the
  `build_stamp.txt` number (2302, `git rev-list --count HEAD`, guarded by the generator's
  refuse-to-build check against `last_uploaded_build.txt`) — do NOT rebuild any of it.
  **Why this is a separate spec and not a widening:** a wrong string is harmless, a wrong
  `maxDurability` leaves a saved `PersistedClub` above a ceiling that no longer exists.
  **Un-clamped application is the single most likely way this feature corrupts a save.**
  Execution order is already right — `ContentService` −900, `SaveDataHost` −100, DBs+managers 0 —
  so at 0 BOTH overlay and save are available, which is exactly what clamping needs.
  ⚠️ But `ClubDatabaseCSV` and `ClubManager` are BOTH at default 0 and the "runs before" guarantee
  is only a code comment — find what actually enforces it and ASSERT it, the way Phase 1 asserted
  `LocalizationManager.IsInitialized`. Also folded in: `is_active=false` stays renderable and
  equipped (I6); an overlay row whose sprite does not resolve keeps the BUNDLED row rather than
  showing `Placeholder`; shop windows honoured fail-closed; tournaments already safe via
  `PersistedCharacterSnapshot` (pin it with a test, don't "fix" it); **and a kill-switch semantics
  gap found reviewing Phase 1** — global `enabled:false` drops the cache, but a per-catalog
  `is_enabled=false` only makes the catalog ABSENT, which this client reads as "no update", so the
  last good overlay applies forever. §7.4 promises otherwise. Fix or report what the payload cannot
  express. Finally: the 17 pre-existing test failures are STALE ASSERTIONS
  (`GachaTicketTests.CurrentSchemaVersion_Is9` asserts 9, `SaveSchemaMigrator` ships 10) — fix the
  literals here so the suite is a real signal again.
  Spec: `Docs/Specs/Active/content_overlay_catalogs/SPEC.md`.

### Kickoff · content_overlay_catalogs

```
Read Docs/Specs/Active/content_overlay_catalogs/SPEC.md and implement it.

Context:
- PHASE 2 of Docs/CONTENT_PIPELINE_PLAN.md — clubs, characters, items, bags,
  balls, shop_catalog. Phase 1 is done and deployed; Golfin.Content,
  ContentService (-900), RemoteContentSource, per-catalog cursors and the
  min_build resolution all EXIST. Extend them, do not rebuild them.
- The heart of this task is CLAMPING, not plumbing. A wrong string was harmless;
  a published maxDurability below an owned club's currentDurability is not.
  Clamp once, in an explicit step after the overlay is applied and the save is
  loaded — never at each read site — and log every clamp with id/field/old/new.
  A silent clamp is indistinguishable from a bug report six weeks later.
- Execution order is already correct: ContentService -900, SaveDataHost -100,
  DBs and managers at 0, so at 0 both the overlay and the save are ready.
  ⚠️ But ClubDatabaseCSV and ClubManager are BOTH default 0 and "runs before
  ClubManager" is only a comment. Find what actually enforces it and ASSERT it
  at runtime — same class of invisible failure as Phase 1's IsInitialized.
- is_active=false means deactivated, never deleted: gone from shop and pools,
  still fully renderable in the bag, still equipped if it was.
- If an overlay row's sprite does not resolve, KEEP THE BUNDLED ROW. A silently
  wrong club beats a grid of Placeholder.
- Tournaments are already safe (PersistedCharacterSnapshot freezes stats at
  sign-up). Do not "fix" it — pin it with a test.
- Kill-switch gap I found reviewing Phase 1: global enabled:false drops the
  cache, but a per-catalog is_enabled=false only makes the catalog ABSENT, which
  this client treats as "no update" — so the last good overlay applies forever.
  Make them agree, or REPORT what the payload cannot express.
- Fix the 17 pre-existing failures while you are here: they are stale assertions
  (CurrentSchemaVersion 9 vs the shipped 10), not a code bug. A red suite masks
  regressions and we have leaned on "sweep green" as evidence repeatedly.
- Out of scope: live mid-session swap, player inventory, Addressables, art URLs,
  SP refunds on rarity downgrade (clamp and log — refunding is its own
  decision), LevelUpCosts, and any endpoint/panel/schema change.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`content_overlay_texts`** (filed 2026-08-26, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. PHASE 1 — the first time the content pipeline reaches the GAME.** Everything shipped
  so far (catalogs, publish/rollback, six panels, the per-catalog delta endpoint) is a system the
  client has never read. **Texts only, deliberately** — if the mechanism is wrong, find out on a
  string, not on 799 clubs. New `Golfin.Content` asmdef: `RemoteContentSource` (near-copy of
  `RemoteNoticeSource` — raw-body disk cache, atomic tmp+File.Replace, null on ANY failure),
  `ContentService` MonoBehaviour in ShellScene beside `NoticeService`, and
  `LocalizationManager.ApplyOverlay` (~15 lines, no call-site changes).
  ⚠️ **Two things the spec makes the Implementer resolve rather than guess:** (1) execution order —
  `ContentService` must be `-900`, AFTER `LocalizationBootstrap`'s `-1000` which builds `_textMap`;
  backwards means the overlay is applied then wiped by `Initialize`. (2) **`min_build` has no
  cross-platform Unity runtime API and the two on-disk sources DISAGREE** — `ProjectSettings`
  `buildNumber: iPhone` = **2113**, `Resources/Data/build_stamp.txt` = **`v1.5.7 (2297)`**. Pick
  one, bake an integer at build time (`BuildStamp.cs` compiles out unless `GOLFIN_TESTBUILD`, so
  reusing it directly will not work in release), and NAME the choice in the report. Parse failure
  ⇒ send 0, the safe end.
  Fetch writes the cache and does NOT re-apply mid-session (§2 I5 — next launch); live text swap
  is explicitly deferred so the first Unity spec has as few moving parts as possible.
  Spec: `Docs/Specs/Active/content_overlay_texts/SPEC.md`.

### Kickoff · content_overlay_texts

```
Read Docs/Specs/Active/content_overlay_texts/SPEC.md and implement it.

Context:
- PHASE 1 of Docs/CONTENT_PIPELINE_PLAN.md — read §2 (the six invariants) first.
  This is the first time the content pipeline reaches the game; everything built
  so far is a system the client has never read.
- TEXTS ONLY. Clubs/characters/items/shop overlays are the next spec and need the
  §5 clamping rules texts does not. If the mechanism is wrong, better to find out
  on a string than on 799 clubs.
- Copy RemoteNoticeSource/NoticeService shape exactly — raw-body disk cache,
  atomic .tmp + File.Replace, null on ANY failure, fetch off the critical path,
  MonoBehaviour in ShellScene. Do not invent new networking.
- ⚠️ Execution order: ContentService is -900, AFTER LocalizationBootstrap's
  -1000. Bootstrap builds _textMap in Initialize(); apply the overlay before it
  and it gets wiped. Log both and paste the order in the report.
- ⚠️ min_build: there is NO cross-platform Unity runtime API for the build
  number, and the two on-disk sources disagree — ProjectSettings buildNumber
  iPhone = 2113, build_stamp.txt = "v1.5.7 (2297)". RESOLVE it, bake an integer
  at build time (BuildStamp.cs compiles out unless GOLFIN_TESTBUILD, so it will
  not work in release as-is), and name your choice in the report. Parse failure
  sends 0 — the safe end.
- The fetch writes the cache and does NOT re-apply this session (invariant I5:
  next launch). Live text swap is deliberately deferred — do not add it.
- Airplane mode, a corrupt cache, a missing content_version.txt and
  enabled:false must ALL fall back to bundled strings with a warning and no
  exception. Those are designed paths, not malfunctions.
- Out of scope: every other catalog, live swap, player inventory, Addressables,
  art URLs, and any change to the endpoint/panels/schema. If the client needs
  something the API cannot serve, REPORT it — that has caught four real gaps.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- ~~**`perf_baseline_capture`**~~ — ✅ **DONE 2026-08-26** (device half ran later the same day — report §9; Architect review + plan corrections in `Docs/PERF_OPTIMIZATION_PLAN.md` §8; the owed items are now `perf_baseline_experiments` above). Superseded pointer kept for history: — ⏳ **STATIC HALF DONE
  2026-08-26; DEVICE HALF PENDING A PHONE.** Report:
  [`Docs/Reports/perf_baseline_2026-08-26.md`](Docs/Reports/perf_baseline_2026-08-26.md).
  **All five §0 items came back CONFIRMED from the shipping assets, scenes and URP package source —
  none refuted.** Highlights: two enabled **Base** cameras during a hole (shell one has
  post ON, shadows ON, cull Everything) and nothing in `Assets/Scripts` disables it — **but it sits
  at `(0,1,-10)`, which the baked heightmaps put ~25 m UNDER the Hole 08 surface, so the plan's
  "up to ~2× GPU" is too generous**: the certain cost is 4 cascade passes + the prepass set + a full
  Bloom/Uber chain, and the opaque draw is what the Frame Debugger has to size; Hole 08 = 27,468 GO / 23,538 MeshRenderers
  / 1,958 LODGroups with **23,538 of 23,538 casting shadows**, 12 renderers per tree, no billboard
  LOD, animated crossfade on all 1,958; 9 TerrainLayers on every hole → 3 TerrainLit passes,
  `m_SplatMapDistance: 1000` + `m_DrawInstanced: 0` on all 18; no `LightingDataAsset` anywhere →
  shadows fully realtime. **#5 is worse than filed:** `technique: 1` is DBuffer, which
  unconditionally `ConfigureInput(Depth|Normal)`s a DepthNormals prepass **per camera** with zero
  decals in the project *and* returns `false` from `SupportsNativeRenderPass()` — one unused feature
  is vetoing Native Render Pass on a TBDR GPU. **New:** Hole 02's 1,495 invisible tree collisions are
  CONFIRMED and structural (plan §7 — needs its own task, not fixed here); holes 01/02/06 carry
  `m_TreeDistance: 5000`/billboard 50 vs 150/80 on the other 15, which confounds the 01↔08 baseline
  comparison and breaks plan §2's own fairness rule between holes.
  **Still needs a phone on USB** (both iPhones read `unavailable` on 2026-08-26): all Profiler ms /
  batches / SetPass / tris / verts / shadow-caster / culling numbers, the Frame Debugger event list,
  the Memory Profiler snapshot after 01→08→06, the 10-minute Hole 08 thermal state, and experiments
  a–e. **§6 of the report is a turnkey procedure with the empty tables to paste into** — re-run the
  kickoff below as-is once a device is attached; nothing in it needs re-deriving.
  *(Original pointer text follows.)* **Phase 0 of
  `Docs/PERF_OPTIMIZATION_PLAN.md`. Measurement only, NO code changes. Kickoff pasteable.**
  Inspection found five suspected per-frame costs that must be confirmed on the iPhone before the
  tier system (`9a`, Order 900) is specced: (1) `ShellScene` `Main Camera` (post-processing ON, cull
  Everything) is never disabled during a hole → the hole may render twice + a bloom chain;
  (2) 130–1,958 standalone Spruce GameObjects per hole (Hole 08 = 23,538 MeshRenderers, all shadow
  casters); (3) 4 shadow cascades / 100 m on the Mobile URP asset; (4) terrain basemap distance 1000
  (9-layer splat everywhere) + instancing off; (5) an unused `DecalRendererFeature` (DBuffer) that may
  add a DepthNormals prepass. Output = `Docs/Reports/perf_baseline_<date>.md` with the numbers below.
  Plan + tier table + options: `Docs/PERF_OPTIMIZATION_PLAN.md` (decisions for Cesar in §6).

### Kickoff · perf_baseline_capture (issued 2026-08-26) — measurement only

```
Read Docs/PERF_OPTIMIZATION_PLAN.md §0–§1 and produce the Phase 0 baseline report.
NO gameplay/rendering code changes in this task — measure and report.

Setup:
- Dev-iOS build profile (Development Build + Autoconnect Profiler + Deep Profiling OFF),
  physical iPhone over USB. Frame Debugger attached to the player.
- Holes to capture: 06 (no standalone Spruce), 01 (terrain trees only), 08 (worst case,
  1,958 standalone Spruce). Same pose each time: tee, default aim camera, after the
  tee-idle glow settles; then one capture mid-flight of a driver shot on Hole 08.

Capture per hole (write every number into the report):
1. Profiler: CPU main thread ms, Render thread ms, GPU ms (GPU module), frame rate,
   Batches / SetPass calls / Tris / Verts, shadow casters count, culling time.
2. Frame Debugger event list, saved as text: COUNT THE CAMERAS that render
   (expected suspects: ShellScene "Main Camera" AND LabScaffold "Main Camera"),
   count the shadow cascade passes, and state whether a DepthNormals prepass or
   DBuffer pass exists. This confirms or kills plan items #1 and #5.
3. Memory Profiler snapshot after playing Holes 01 → 08 → 06 consecutively:
   Texture2D / Mesh / AudioClip / Managed heap totals, top 10 objects by size,
   and the managed allocation spike during hole load (Profiler Memory module,
   GC Alloc column in the load frames).
4. Thermal: note Xcode Energy/Thermal state after 10 minutes on Hole 08.

Verification experiments (each one toggled in the Editor/Inspector only, no commits,
revert after measuring; report before/after GPU ms + batches on Hole 08 tee pose):
  a) ShellScene Main Camera disabled during the hole.
  b) Mobile_RPAsset shadow cascades 4 → 1, shadow distance 100 → 40.
  c) Terrain basemapDistance 1000 → 100 and drawInstanced ON (Terrain inspector).
  d) DecalRendererFeature removed from Mobile_Renderer.
  e) QualitySettings.maximumLODLevel = 1 (skip LOD0).

Out of scope: any fix, any tier code, any scene or importer edit that gets committed,
Vegetation.shader, Spruce conversion. If Hole 02 shows invisible tree collisions
(plan §7), note it — do not fix it here.

When done: write Docs/Reports/perf_baseline_<date>.md (numbers, Frame Debugger event
list attached, screenshots), list which of plan §0 items #1–#5 were CONFIRMED / REFUTED
with the evidence, update Docs/AI_CONTEXT.md, and update the TellCode pointer.
```


- **`content_panels_gaps`** (filed 2026-08-25, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable.** Closes the four gaps `content_admin_panels` reported instead of working around
  (panels are DONE + deployed, Worker `3361ddfe-8132-4596-b306-2d5f89d33064`, 14/15 PASS).
  ⚠️ **The escalated Clubs-rarity FAIL is real but its stated cause is NOT** — Architect checked
  prod: all **799/799** club rows carry `rarity` in `data` (`data->>rarity=eq.Common` → 133;
  distribution 133/133/133/133/134/133). `Clubs.csv` has had a `rarity` column since the roster
  shipped. What misled it: the GENERATED ids also encode rarity (`club_awedge_bogeyb_common`)
  while the 7 hand-authored ids do not — true of the ids, irrelevant to the facet, which should
  read `data`. So rarity is the SAME 3-line filter as brand/type, not a special case, and the
  coverage caveat comes OUT of the UI once all three are complete queries.
  The other three: (a) **version history must read `content_versions`, not the audit log** — the
  audit log caps at 200 actions and never saw the SQL-seeded v1, so rollback (the §7.3 safety rail)
  silently loses its tail; needs a `versions` route and v1 must be selectable. (b) **`shop_catalog`
  scheduling columns were specced in `CONTENT_PIPELINE_PLAN.md` §11.2 and never built — an
  ARCHITECT gap**; add `startAt/endAt/saleStartAt/saleEndAt` empty on all 5 rows, fail-closed
  parsing like `notices.py`. (c) **art thumbnails are NOT a gap** — sprite names are what the game
  resolves, the monogram tile is right, and a URL column would pre-empt §10.2. Explicitly a no-op.
  Spec: `Docs/Specs/Active/content_panels_gaps/SPEC.md`.

### Kickoff · content_panels_gaps

```
Read Docs/Specs/Active/content_panels_gaps/SPEC.md and implement it.

Context:
- These are the four things you reported from content_admin_panels rather than
  working around. Reporting them was right. Three are real; one is a no-op.
- §1 Clubs rarity: your FAIL grade was correct, the stated cause was not. All
  799/799 club rows carry rarity in `data` (verified on prod:
  data->>rarity=eq.Common returns 133). The generated ids ALSO encode rarity,
  which is what misled it — the facet should read `data`, not the id. Implement
  rarity identically to brand and type, and REMOVE the per-facet coverage
  caveat from the UI once all three are complete server queries.
- §2 is the consequential one: version history currently comes from the audit
  log, which caps at 200 actions and never saw the SQL-seeded v1. content_versions
  already holds every snapshot and nothing reads it. Rollback is the §7.3 safety
  rail — a target list that loses its tail is a rail that stops reaching. v1 must
  be selectable.
- §3 shop scheduling columns were specced in CONTENT_PIPELINE_PLAN.md §11.2 and
  never built — my gap, not yours. Add them EMPTY on all 5 rows so the round-trip
  stays clean, and parse fail-closed like routers/notices.py _parse.
- §4 art thumbnails: deliberately DO NOTHING. Sprite names are what the game
  resolves; a URL column would pre-empt §10.2.
- Out of scope: art URLs / remote art, any Unity or Assets/Scripts change (the
  only Assets/ edit is four empty CSV columns), player inventory, Addressables.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`content_admin_panels`** (filed 2026-08-25, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. This is where admin-managed content becomes VISIBLE** — everything shipped so far is
  API-only, editable by curl and nothing else. Five panels (Clubs / Characters / Items+Bags+Balls
  as tabs / Texts / Shop) plus ONE shared publish drawer: diff preview → confirm → publish, version
  history with rollback, per-catalog kill switch. **Adds no server logic** — all six routes exist
  and are live (`/api/content`, `[catalog]/rows|diff|publish|rollback|enabled`, every one
  `checkAdmin()` + `writeAudit()`). If a panel needs something the routes cannot serve, that is a
  finding to REPORT, not a licence to add an endpoint. Live counts: clubs 799 · texts 501 ·
  characters 12 · bags 10 · shop 5 · items 3 · balls 2 — **clubs needs server-side pagination and
  filtering, not a `<table>`**. Shop specifics in `CONTENT_PIPELINE_PLAN.md` §11: `refId` typeahead
  against the live catalog (makes a dangling ref impossible rather than merely rejected), resolved
  preview with name/rarity/thumbnail, and a printed notice that **prices are NOT server-enforced**
  (`PointsSpendGate` still debits client-side). Rollback UI must say it moves FORWARD. EN+JA on
  every string (`DictKey` makes a missing key a type error); never name a row-map param `t`.
  Spec: `Docs/Specs/Active/content_admin_panels/SPEC.md`. Predecessors `content_catalog` and
  `content_cursor_per_catalog` are both DONE and deployed.

### Kickoff · content_admin_panels

```
Read Docs/Specs/Active/content_admin_panels/SPEC.md and implement it.

Context:
- The content backend is DONE and live. This task adds NO server logic: all six
  route handlers already exist, are deployed and are auth-gated. Build the UI on
  top of them. If a panel needs something they cannot serve, REPORT it — do not
  add an endpoint.
- Five panels + ONE shared publish drawer (diff preview -> confirm -> publish,
  version history with rollback, per-catalog kill switch). Follow the Tournaments
  panel; ADMIN_DASHBOARD_OPS.md §3.1 calls it the most complete.
- Clubs is 799 rows: server-side pagination and filtering, never a full table.
- Read ADMIN_DASHBOARD_OPS.md §3.4 and §4 BEFORE touching the dashboard. The
  ones that have already cost time: never `next build` against a running
  `next dev` (shared .next/, every chunk 404s, server log stays clean);
  NODE_ENV=development for both `npm run dev` and `npm install --include=dev`;
  every new string needs BOTH en and ja in lib/i18n.ts; never name a row-map
  parameter `t` (it shadows the translator and has bitten that file twice).
- Shop panel: refId typeahead against the live catalog, resolved preview
  (name/rarity/thumbnail), and PRINT ON THE PANEL that prices are not
  server-enforced — purchases still debit RP client-side via PointsSpendGate.
- Rollback moves FORWARD (republishes an old snapshot as a higher version). Say
  so in the UI or an operator will misread the version numbers.
- Out of scope: new API routes or schema, any Assets/ or Unity change, player
  inventory, Addressables, art-URL columns, LevelUpCosts.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`content_cursor_per_catalog`** (filed 2026-08-25, Architect via Cowork) — **SPEC_READY, kickoff
  pasteable. Must land BEFORE the Phase-1 Unity overlay spec** — once a build ships with a scalar
  cursor this is a migration, not an edit. Phase 0 (`content_catalog`) is otherwise DONE: A2 seeded
  1,332 rows, A3 round-trip clean, B deployed (`/api/v1/content` live; `/health` `/notices`
  `/banners` `/tournaments/golfin` all still 200), C exporter + `content_version.txt`, D six
  dashboard route handlers with `checkAdmin()` + `writeAudit()`. **Code's D-2 call was right and my
  spec was wrong**: §B1 said top-level `version = max(published_version)`, which both replays
  610 KB every boot AND silently drops a catalog that publishes while sitting below the max. Code
  shipped `min`, which is safe — but `min` is pinned by whichever catalog changes least, so every
  row past v1 re-sends forever and the delta degenerates to a full replay. (Its "164 bytes"
  measurement is real but only because the v2–v9 publishes were validation tests that changed no
  rows — the `IS DISTINCT FROM` guard left everything at v1. The first real edit starts the
  ratchet.) **A single scalar cursor cannot describe seven independently-versioned catalogs.**
  Fix: per-catalog `since=clubs:1,texts:9`, delete the top-level `version`, keep `latest_version`
  as informational-only. `content_version.txt` is already per-catalog, so the plumbing exists.
  Also folded in: ~~a live texts drift~~ — **CORRECTED 2026-08-25: there was no drift.** The
  Architect counted a mid-file `#` comment as a data row (`csv.DictReader` does not skip comments;
  the shipping `LocalizationTextImporter` does). 501 = 501, identical id sets. The `--check` drift
  guard was built anyway and gates on **id sets, not counts** — two files can hold 501 rows each
  and disagree about which 501; the `shop_club_pwedge_royal`
  600/600 data fix; and widening the STATUS hook's `BACKEND_TASK_RE` (it matches "No `Assets/`
  changes", the spec said "edits" — one word forced four Figma/screenshot gates onto a backend
  task). **Code left those four gates failing rather than fabricating evidence — that was the right
  call and the spec says so.**
  **Phase 0's last open item is CLOSED**: the dashboard had never been redeployed (`.open-next`
  from 08-19 vs handlers from 08-25, so `/api/content` was a live 404 while `/api/audit` was 200
  on the same cookie — not an auth problem). Architect deployed 2026-08-25, Version ID
  `5f6548cd-c93b-4a19-a86f-ef93e93cdc72`; root now 302s to cloudflareaccess and `/api/content`
  returns 200 with a real admin session, `"mock": false`. **Skip §8 of the follow-up spec.**
  Spec: `Docs/Specs/Active/content_cursor_per_catalog/SPEC.md`.

### Kickoff · content_cursor_per_catalog

```
Read Docs/Specs/Active/content_cursor_per_catalog/SPEC.md and implement it.

Context:
- Follow-up to content_catalog (Phase 0, DONE and deployed). This must land
  BEFORE the Phase 1 Unity overlay spec — after a build ships with a scalar
  cursor it becomes a migration instead of an edit.
- The core change is small: `since` becomes per-catalog
  (since=clubs:1,texts:9), the top-level `version` field is DELETED, and
  `latest_version` stays as informational-only. Each catalog already returns its
  own version, and Tools/content/export_content.py already writes
  content_version.txt as one <catalog>=<version> line per catalog — the plumbing
  exists, only the endpoint and cursor are scalar.
- Your D-2 analysis was correct and my spec was wrong. Do not re-litigate min vs
  max: both are wrong, per-catalog is the answer. §Background explains why min
  degrades even though your 164-byte measurement was accurate.
- §8 (deploy the dashboard) is ALREADY DONE — the Architect deployed it
  2026-08-25, Version ID 5f6548cd-c93b-4a19-a86f-ef93e93cdc72. Verified: root
  302 to cloudflareaccess, /api/content 200 with a real admin session,
  "mock": false, all 7 catalogs, dirtyCount 0. Phase 0 is fully closed. Skip §8.
- Also in scope, all small: fix the live texts drift (catalog 501 rows vs CSV
  502) and add a drift check to `--check`; blank shop_club_pwedge_royal's
  saleRpCost and restore the strict rule (§6 — Cesar delegated the call, it is
  recorded in the spec, do not re-ask); widen BACKEND_TASK_RE and add an explicit
  SPEC_KIND: backend declaration so prose drift stops breaking the hook.
- You were RIGHT to leave the four inapplicable STATUS gates failing rather than
  fabricate a screenshot. Keep that posture. Fix the hook, not the evidence.
- Out of scope: ContentService / RemoteContentSource / any *DatabaseCSV.cs edit
  (all Phase 1), admin panels, player inventory, Addressables, art URLs.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`bridge_transplant`** (filed 2026-08-25, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  **SPLIT 2026-08-25 (Cesar: "Split, bridges first, trees later"). This spec is BRIDGES ONLY,
  5 holes.** The scenery half is now `Docs/Specs/Queued/scenery_transplant/` and the wind gap
  found while auditing it is `Docs/Specs/Queued/tree_wind_coverage/` — both QUEUED, pointers below.
  Copy the 7 bridges that exist only in `Generated/Video/Hole_NN_Geo.unity` into the live
  `Generated/Hole_NN_Geo.unity` scenes (holes 7, 8×2, 9, 12×2, 17), give them deck + railing/pier
  collision in the fixed-point sim, and re-bake only what changed. Spec:
  `Docs/Specs/Active/bridge_transplant/SPEC.md` — it carries the exact world TRS of all 7 instances
  as ground truth (both scene sets share TerrainData guid `f024468aa2c3f9c42ac9cc410c8576d0`, so
  transforms transplant verbatim).
  **Cesar's decisions of record:** (1) deck AND railings/piers, not deck-only; (2) bots AVOID
  bridges — no `VersusBot` change; (3) all 7 instances; (4) split — scenery/trees deferred.
  **Four findings that are load-bearing — do not "simplify" past them:**
  (a) Unity colliders are dead weight — the ball runs on `BallSimulation` fixed-point, so a bridge
  in the scene is physically invisible until something bakes it. The prefabs' 140 / 38 `BoxCollider`s
  are *authoring data* for the obstacle bake, not runtime collision.
  (b) `PhysicsHeightmapBaker` reads terrain only, so `heightmap.bytes` must come out BYTE-IDENTICAL —
  do not re-bake it. The deck's height reaches the sim through the zone path instead
  (`BakedHeightProvider.SampleHeight` calls `TrySampleMeshY` FIRST and it beats the heightmap).
  (c) **A CartPath deck over water is silently masked** — `BakedZoneClassifier.Priority` puts
  Water at 80 and CartPath at 50, and the first containing polygon wins, so the ball standing on
  the bridge would classify as Water and take a penalty. Hence a NEW `SurfaceType.Bridge` at
  priority 95. That also delivers decision (2) for free: `Bridge` is absent from
  `VersusBot.IsPlayableSurface`, so bots decline to aim at decks with ZERO bot-code change and zero
  blast radius onto the real cart paths on the other 13 holes.
  (d) `TreeObstacleBaker.OnSceneSaving` re-harvests `StandaloneTrees`/`PaintedTrees` on EVERY save —
  the `Bridges` container must be scene-root or bridge parts get baked as tree cylinders. A changed
  `tree_obstacles.csv` hash is a BUG, never an expected diff. ⚠️ Hole 17's is `79f0eae4` / 1663 rows
  as of 2026-08-25 04:04 — Cesar planted 829 spruces there that morning, so that file is FRESH live
  data this task must leave untouched.
  ⚠️ Two traps: `SurfaceConfig.cs:24` is a hardcoded `new SurfaceCoefficients[11]` (must become 12 or
  the first bridge classification throws), and `bridgeLODs.fbx` (holes 8×2, 9) is an FBX with NO
  colliders — those three either need a prefab variant with railing boxes or ship deck-only in v1,
  flagged either way.
  ⚠️ Known limitation, accepted: the height field is 2.5D, so nothing can pass UNDER a bridge.
  Confirm how bad it looks on hole 12 (two bridges) and report.
  **Do NOT confuse this with the pre-existing `BridgeAnchor` / `BridgeExporter` / `bridges.json`** —
  that is cart-path spline snapping for UHoleGeo, a different feature that shares the word.
  Stage A+B on hole 7 alone is a complete, reviewable increment — get it reviewed before batching
  the other four holes.

  ### Kickoff · bridge_transplant

  ```
  Read Docs/Specs/Active/bridge_transplant/SPEC.md and implement it.

  Context:
  - Copies the 7 bridges from Generated/Video/Hole_NN_Geo.unity into the live
    Generated/Hole_NN_Geo.unity scenes (holes 7, 8x2, 9, 12x2, 17) and gives them real
    collision in the fixed-point sim: deck via a new SurfaceType.Bridge zone mesh,
    railings/piers via a new BridgeObstacleProvider mirroring TreeObstacleProvider.
  - Read SPEC "Architecture context" FIRST — Facts 1-4 explain why the obvious approach
    (Unity colliders / CartPath deck / re-bake the heightmap) is wrong in each case.
  - BRIDGES ONLY. Do not move grass, rocks, signs or any tree, and do not touch any
    material or shader. Those are two separate queued specs.
  - Minimal diff. Mirror the existing tree pipeline exactly: TreeObstacleData /
    TreeObstacleProvider (XZ grid, sorted candidates, fp containment guard) /
    TreeObstacleLoader / TreeObstacleBaker (bake-hash header + sceneSaving hook).
    Reuse SurfaceMarker, BakeZoneJsonTool, CourseSlugResolver, BakedZoneClassifier.
  - Do Stage A+B on HOLE 7 ONLY and stop for review before batching holes 8/9/12/17.
  - Never hand-edit .unity YAML. Never re-import a hole. Never re-bake heightmap.bytes.
    tree_obstacles.csv must be unchanged on all 5 holes — hole 17's especially, it was
    freshly baked 2026-08-25 when Cesar planted that hole.
  - Out of scope: VersusBot / BotTreeProbe changes, BridgeAnchor / BridgeExporter /
    bridges.json, cart-path behaviour on the other 13 holes, ball-under-bridge 3D
    collision, and everything in scenery_transplant / tree_wind_coverage.

  When done: list changed files with a 1-line summary each, run the acceptance
  tests in the spec, flag which need manual on-device verification, update
  STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
  Docs/AI_CONTEXT.md.
  ```

- **`scenery_transplant`** (filed 2026-08-25, Architect via Cowork) — **QUEUED. Runs AFTER
  `bridge_transplant` is DONE** (both edit the same five hole scenes and share the same
  `tree_obstacles.csv` hash invariant). Spec: `Docs/Specs/Queued/scenery_transplant/SPEC.md`.
  Moves the rest of the Video-only content: **five trees** (Pine 03 ×2 + Poplar 01 on H02,
  Old 03 on H03, Ash 02 on H14, Fir 04 on H16), 1 841 grass tufts, 6 rocks, 14 wooden signs,
  across 8 more holes (01, 02, 03, 05, 06, 14, 16, 18 — note H18 has 380 grass and no bridge).
  **The audit's headline: the move is Video → live and the LIVE scenes are the RICHER set.**
  13 702 hand-placed `Spruce 1`/`Spruce 3` are already in 15 live holes under `StandaloneTrees`
  and already baked (hole 7: 677 standalone + 666 terrain = its 1343 CSV rows exactly). There
  was never a pile of hand-placed trees waiting in the Video scenes — there are five.
  ⚠️ **`PaintedTrees` is a phantom-obstacle trap.** `TreeObstacleBaker.HarvestContainer` bakes
  EVERY child of `PaintedTrees` as a tree, and `tree_collision_profiles.csv` has no grass rows —
  so grass transplanted into that container becomes one `default` cylinder each (0.25 m trunk × 3 m,
  3 m canopy radius to 9 m), 840 of them on H08 alone. Grass goes to a NEW `PaintedGrass` container,
  rocks/signs to `Props`; only the 5 real trees go to `StandaloneTrees`, each needing a MEASURED
  profile row (Fir_04 already has one). Expected tree-hash changes: H02/H03/H14/H16 ONLY.
  ✅ Resolved 2026-08-25: hole 17 previously had NO `tree_obstacles.csv` at all — the only hole
  shipping with zero tree collision. Cesar planted it that morning (829 spruces + 834 terrain);
  the save hook auto-baked `79f0eae4` / 1663 rows at 04:04. **All 18 holes now have tree collision.**

- **`tree_wind_coverage`** (filed 2026-08-25, Architect via Cowork) — **QUEUED, BLOCKED on a
  Cesar decision.** Spec: `Docs/Specs/Queued/tree_wind_coverage/SPEC.md`. Do not start.
  `TreeWindDriver` maps `WindContext.SpeedMph` onto `WindSpeedFloat1` but walks
  `terrain.terrainData.treePrototypes` ONLY and skips non-`Custom/Vegetation` materials. The
  13 702 hand-placed spruces are on `Leaves_URP.shadergraph` ("Wind Speed" =
  `Vector1_b0ddedae341d4c7ba1d429299f3078ea`, authored **0.4**) — wrong shader AND not terrain
  prototypes, so nothing drives them. **0.4 is exactly `MaxTreeWindSpeed`, so every hand-placed
  spruce is pinned at MAXIMUM sway on 15 holes while the terrain trees correctly scale with hole
  wind.** Worst on a calm hole; hole 17 is the one hole where the two populations happen to agree
  (windiest hole, terrain trees also reach 0.4), so do NOT eyeball the bug there.
  **Two routes, Cesar picks:** (A) extend the driver to walk the containers and write both property
  names — must also extend `TreeWindDriverEditorGuard`'s authored-value restore or values bake into
  the `.mat` on disk, and the two shaders' sway curves differ so the mapping needs a feel pass;
  (B) re-material the spruces onto `Custom/Vegetation` — simpler at runtime, art change with
  15-hole regression risk. Acceptance either way: at 0 mph every tree is static, and at the hole's
  authored wind both populations agree.

- **`content_catalog`** (filed 2026-08-24, Architect via Cowork) — **SPEC_READY, kickoff pasteable.**
  Phase 0 of admin-managed game content (`Docs/CONTENT_PIPELINE_PLAN.md`): Supabase tables +
  seed from the seven CSVs the game ships today + atomic draft→publish→rollback + the public
  `GET /api/v1/content` delta endpoint + the build-time exporter that rewrites the repo CSVs
  from the published catalogs. **Zero Unity behaviour change** — `Endpoints.cs` gains one
  property nothing calls; no `*DatabaseCSV.cs`, no `LocalizationManager`, no admin UI (panels
  are the follow-up spec `content_admin_panels`). Four stages, each independently verifiable:
  A schema+seed (⚠️ the round-trip seed→export→`diff` must come back EMPTY — that is Stage A's
  real acceptance), B FastAPI read endpoint, C exporter, D dashboard publish/validate/rollback
  route handlers (no pages). SQL for Cesar: `playlife/backend/migrations/2026_08_24_content_catalog.sql`
  **✅ APPLIED TO PROD 2026-08-24 (Cesar).** Verification returned all 7 rows (tables 4 /
  functions 2 / rls_enabled 4 / policies 0 / catalogs 7 / both exec-privilege checks 0),
  re-confirmed live over PostgREST: `content_catalogs` = 7 rows all v0+enabled, the other three
  tables HTTP 200 with the service key. `playlife-api` deliberately NOT redeployed —
  `/api/v1/content` is still 404, and `/health` + `/tournaments/golfin` + `/notices` + `/banners`
  are all still 200. **So the Implementer starts at A2 (seed generator), not A1.**
  **✅ `2026_08_24_golfin_characters_rarity_fix.sql` ALSO APPLIED 2026-08-24 (Cesar).** Verified
  over PostgREST: all 12 `golfin_characters` rarities now match `Assets/Data/Characters.csv`,
  0 mismatches (`char_olivia` Uncommon → Common). The rarity-restricted-tournament bug is closed.
  Spec §A4 still stands: seed the `characters` catalog from the CSV, and make publishing it
  upsert the mirror in the same request so this cannot silently recur once panels exist.
  **Three design points that are load-bearing and must not be "simplified":** (1) the
  `ON CONFLICT … WHERE … IS DISTINCT FROM` guard in `content_publish` — without it every publish
  stamps every row and the delta becomes a full download; (2) `content_rollback` moves FORWARD
  (restores a snapshot as a higher version) — rewinding the counter strands every client that
  already holds the bad version; (3) `min_build` is filtered SERVER-side so an old build never
  receives a row whose art it does not have.
  ⚠️ **Found while collision-checking the migration: `golfin_characters` (the server mirror
  `tournaments_golfin.py:373` reads for `char_rarity_min/max`) is STALE — it says `char_olivia`
  = Uncommon, `Characters.csv` says Common since 2026-08-21. On a rarity-RESTRICTED tournament
  only, Olivia is wrongly rejected from a Common-only event and wrongly accepted into an
  Uncommon-minimum one; the other 11 rows agree. Fix written, NOT YET APPLIED:
  `playlife/backend/migrations/2026_08_24_golfin_characters_rarity_fix.sql` — apply it with the
  content-catalog migration. Spec §A4 also makes publishing the `characters` catalog upsert the
  mirror in the same request, because an admin editing rarity in a panel will never know the
  mirror exists.**
  Open question the Implementer must ask, not guess:
  whether `Bags.csv`/`Balls.csv` are in scope (seeded on the assumption they are).
  Spec: `Docs/Specs/Active/content_catalog/SPEC.md`.

### Kickoff · content_catalog

```
Read Docs/Specs/Active/content_catalog/SPEC.md and implement it.

Context:
- Phase 0 of Docs/CONTENT_PIPELINE_PLAN.md — read §2 (the six invariants) first;
  every design choice in the spec follows from them. Backend + tooling only.
- TWO repos: GolfinRedux (Tools/content/, Tools/admin-dashboard/) and
  /Users/cesar/Documents/playlife (backend/migrations/, backend/routers/,
  backend/main.py). No Assets/ edits except ONE new property in
  Assets/Scripts/Net/Endpoints.cs that nothing calls yet.
- ⚠️ STAGE A1 IS ALREADY DONE — DO NOT RE-APPLY IT. Both migrations were
  applied to prod by Cesar on 2026-08-24 and verified live:
  2026_08_24_content_catalog.sql (4 tables + content_publish/content_rollback;
  content_catalogs = 7 rows, all v0 + enabled; RLS on, 0 policies, EXECUTE
  revoked from anon/authenticated) and
  2026_08_24_golfin_characters_rarity_fix.sql (all 12 mirror rarities now match
  Characters.csv). Both files are stamped APPLIED. START AT STAGE A2.
- Do the remaining stages IN ORDER and report at each: A2 seed generator +
  A3 round-trip, B GET /api/v1/content, C export_content.py, D dashboard route
  handlers. Stage A is not done until seed -> export -> diff against the seven
  repo CSVs comes back EMPTY.
- playlife-api has NOT been redeployed: /api/v1/content is 404 while /health,
  /tournaments/golfin, /notices and /banners are all 200. Stage B's fly deploy
  is the first step with real blast radius on the live build — treat it as such.
- Copy the existing patterns, do not invent: routers/notices.py for the envelope
  and fail-closed windows; lib/audit.ts + lib/auth.ts checkAdmin() + lib/mode.ts
  for the dashboard routes. Read ADMIN_DASHBOARD_OPS.md §4 before touching
  Tools/admin-dashboard — NODE_ENV, next dev vs next build, and the env-file
  traps have all cost time already.
- Out of scope: ContentService / RemoteContentSource / any *DatabaseCSV.cs edit,
  admin panels or pages, player inventory, Addressables, art URLs on rows,
  LevelUpCosts (deliberately unseeded — open question), PointsSpendGate.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`auth_recovery_flow`** (filed 2026-08-19, Architect via Cowork) — **IMPLEMENTED — Code closed the loop 2026-08-19 (commits `0e4381fc6` + `96227b057`; STATUS `READY_FOR_SELF_REVIEW`). Architect spot-check PASSED same day** (commit scope, scene wiring `_resetPasswordScreen`→`883282913`, 2-block scene diff claim, screenshots present per gitignore policy, and the sweep's one failure — pre-existing telemetry edit-mode bootstrap — fixed separately in `15d805a47`, GameSessionTests re-run 5/5). ⚠️ ONE SCOPE NOTE: the auth commit's LocalizationText.csv hunk swept in the 16 `tourn.*` rows belonging to `tournament_restrictions` (still uncommitted) — benign, but that task's close-out must NOT re-add them. Remaining: Cesar's 3 device items (report §Needs-manual), Supabase min-length dashboard check, JA native review; JA-renders-lighter-than-EN wants its own spec. Password-reset links today **silently sign the player in with the password unchanged**: `AuthService.OnDeepLink` (AuthService.cs:202) has one branch, `OAuthCallbackParser` ignores `type=recovery`, and `ISupabaseAuthClient` has no password-update method. Server side done 2026-08-19 — reset emails land on `confirm.golfin.world` and deep-link back with `type` in the fragment. Task: parse `type`+error params, branch OnDeepLink (recovery session held un-persisted, no `RaiseSignedIn()` until the new password is set), `UpdatePassword` → `PUT /auth/v1/user`, set-new-password screen in `Assets/Scripts/UI/Account/`, EN+JA loc, tests in `Golfin.Auth.Tests`. Pre-step folded in: import `AuthRedirectUrl.cs` + first-ever Editor run of `AuthRedirectUrlTests`. Out of scope: SMTP, admin-dashboard password actions, email-change/magic-link, `Tools/golfin-confirm`. Spec: `Docs/Specs/Active/auth_recovery_flow/SPEC.md`.

### Kickoff · auth_recovery_flow (RE-ISSUED 2026-08-19 after partial Cowork implementation — supersedes the original Cowork kickoff)

```
Read Docs/Specs/Active/auth_recovery_flow/SPEC.md, then HANDOFF.md in the same
folder, and FINISH the implementation.

Context:
- The Unity code + tests are ALREADY WRITTEN and green (Cowork 2026-08-19,
  uncommitted): parser CallbackInfo, AuthService recovery branch (tokens held,
  no persist/SignedIn until update), UpdatePassword on all three clients,
  ResetPasswordScreenController, AUTH_RESET_* CSV rows, 14 new tests —
  Golfin.Auth.Tests 45/45 filtered. Do NOT rewrite; HANDOFF.md §2 lists every
  file. The Editor pre-step (AuthRedirectUrl.cs import + first AuthRedirectUrl
  test run) is DONE — 31/31 before the new code.
- Remaining (HANDOFF.md §4): ShellScene wiring — duplicate LoginScreen →
  ResetPasswordScreen, strip to title / new+confirm password fields / eye
  toggle / submit / back / error label, add + wire the controller, wire
  ScreenManager._resetPasswordScreen, LocalizedText keys incl. placeholders;
  then FULL unfiltered EditMode sweep (Assembly-CSharp was touched), SPEC
  acceptance run, docs.
- ⚠️ The working tree carries UNRELATED in-flight work (tournament_restrictions,
  KLYRO club art, AI_CONTEXT/lessons edits). Commit ONLY HANDOFF.md §2 files.
- Out of scope: SMTP, admin-dashboard password actions, email-change/magic-link,
  Tools/golfin-confirm changes.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`auth_email_redirect`** (filed 2026-08-19, Architect via Cowork) — **SPEC_READY, kickoff pasteable.** Player signup-confirmation + password-reset emails were landing on `admin.golfin.world` → the Cloudflare Access block page (client sends no `redirect_to`, Site URL fallback — `ADMIN_DASHBOARD_OPS.md` §6). **Already done server-side 2026-08-19 (Supabase Studio, no code):** both email templates rewritten — GOLFIN-branded, EN+JA, verify link hardcodes `redirect_to=golfin://auth-callback` — so builds already in the field are fixed. Remaining (this task): deploy `Tools/golfin-confirm` (assets-only Worker, `confirm.golfin.world`, same CF account as golfin-admin, **NO Access policy** — verify 200 not 302), make the client pass `redirect_to` on signup/recover, then swap the two templates to the hosted page (+ Supabase URL-allowlist entries, Cesar). ⚠️ MUST VERIFY, not assume: does the `golfin://auth-callback` handler process `type=recovery` (set-new-password flow)? If not, document the gap in the report. Spec: `Docs/Specs/Active/auth_email_redirect/SPEC.md`. Worker files already in `Tools/golfin-confirm/`.

### Kickoff · auth_email_redirect (issued 2026-08-19)

```
Read Docs/Specs/Active/auth_email_redirect/SPEC.md and implement it.

Context:
- Fixes player auth emails landing on admin.golfin.world / Cloudflare Access.
- Templates already fixed server-side (deep link) — do NOT redo them. Your
  parts: (1) deploy Tools/golfin-confirm (assets-only Worker, account pinned
  in wrangler.jsonc, custom domain confirm.golfin.world, NO Access policy —
  verify curl returns 200, not a 302 to cloudflareaccess.com),
  (2) ISupabaseAuthClient signup/recover must pass redirect_to (mirror how
  OAuthUrlBuilder passes golfin://auth-callback today).
- Minimal diff. Out of scope: custom SMTP sender, admin-dashboard password
  actions (separate backlog spec).
- MUST VERIFY, not assume: does the golfin://auth-callback handler process
  type=recovery (set-new-password flow)? If not, document the gap.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
- **`tournament_restrictions`** (filed 2026-08-19, Architect) — **SPEC_READY, kickoff pasteable. Server half LIVE in prod 2026-08-18.** Tournaments carry category (`sponsor`|`competitive`) + entry restrictions (max players, character rarity/level bands, gear rule, club rarity cap), authored in the dashboard, served by `list_golfin` (10 new nullable fields), and enforced server-side at `POST /golfin/{slug}/enter` BEFORE the fee debit (denials 200-shaped: `full` / `ineligible` like `insufficient`; rarity truth = new `golfin_characters` mirror). Client half: DTO→`TournamentDefinition` plumbing (appended optional, CSV fallback = today's behaviour), the signup modal's RULES block goes data-driven (`ApplyRules()` currently joins 5 hardcoded loc strings; ⚠️ the "GEAR: Supplied by GOLFIN" default was display fiction and becomes "Own clubs" per backfilled data), and CONFIRM is gated client-side on character rarity/level + equipped-bag club rarity — ineligible = toast, no debit, no navigation. Standard-spec stat normalization explicitly deferred. Spec: `Docs/Specs/Active/tournament_restrictions/SPEC.md`.

### Kickoff · tournament_restrictions (issued 2026-08-19)

```
Read Docs/Specs/Active/tournament_restrictions/SPEC.md and implement it.

Context:
- Server half is LIVE: list_golfin emits 10 nullable restriction fields
  (playlife routers/tournaments.py); /golfin/{slug}/enter denies pre-debit
  with 200-shaped {status:"full"|"ineligible"} (routers/tournaments_golfin.py).
  Read them for the contract; do not change playlife.
- Client: RemoteTournamentDtos + TournamentDefinition (appended optional,
  Title/BannerUrl pattern) + TournamentScheduleMapper pass-through;
  TournamentSignupModalController.ApplyRules() becomes data-driven with the
  existing 5 loc keys as null fallbacks; OnConfirm gates eligibility BEFORE
  the payment path (CharacterManager rarity/level, BagManager equipped bag
  vs club_rarity_max; gear_rule=supplied skips the club check).
- Minimal diff. Reuse RarityHelper/CharacterRarity ordering, the modal's
  existing refusal-toast pattern, LocalizationManager. New loc rows EN+JA
  (JA flagged for native review). No prefab surgery — if the category tag
  needs layout work, SKIP it and note it.
- Out of scope: standard-spec normalization, dashboard, server changes,
  division/bracket logic.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec (incl. the widget-click ineligible-CONFIRM test), flag which
need manual on-device verification, update STATUS.md + IMPLEMENTER_REPORT.md
in the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`ingame_settings_modal`** (filed 2026-08-18, Architect) — **SPEC_READY, kickoff pasteable.** The in-game gear (`ShotUI_Canvas/SettingsButton` in LabScaffold — gameplay HUD only, menu gear untouched) gets its real function: settings overlay with SFX+Music sliders (AudioManager reuse) and a PLAYING card (live HoleContext/HoleData bind) with BACK / QUIT; QUIT is solo-only, confirm-gated ("no rewards"), tears down via `GameplaySceneLoader.UnloadGameplay()`. Same change REMOVES the cheat on that gear: `GreenTuningPanel.toggleButton` unwired in LabScaffold (class + lab usage stay). Everything reuses existing card/button/slider assets — zero new art. Figma `13873:33610` + `13905:6678`; renders in `reference/`. ⚠️ Flagged for later, NOT in this task: app-kill mid-tournament-round = abandoned-round handling needs its own spec. Spec: `Docs/Specs/Active/ingame_settings_modal/SPEC.md`.

### Kickoff · ingame_settings_modal (issued 2026-08-18)

```
Read Docs/Specs/Active/ingame_settings_modal/SPEC.md and implement it.

Context:
- In-game gear (ShotUI_Canvas/SettingsButton in LabScaffold) opens a new
  settings modal: sound sliders + PLAYING card + solo-only confirm-gated
  QUIT. Also REMOVES the cheat on that gear (GreenTuningPanel.toggleButton
  — unwire in scene, keep the class; check SmokeRunner2fHost S3 capture).
- Minimal diff. Reuse existing systems: ModalController base,
  AudioManager Get/Set volumes, HoleContext + HoleData bind,
  GoldPrimaryButton + signup-modal silver button + SettingsScreen sliders
  + existing card backgrounds, ButtonPressFeedback, LocalizationManager
  (6 new CSV keys, table in spec §5).
- Quit = VersusResultModalController.NewMatchRoutine() pattern
  (coroutine + UnloadGameplay). QUIT hidden when GameSession.IsVersus ||
  TournamentRoundContext.IsActive.
- Out of scope: tournament/versus forfeit rules, menu settings screen,
  blur, timeScale, GreenTuningPanel feature changes, any reward/RP/stamina
  grant or refund on quit.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`tournament_async_board`** (filed 2026-08-18, Architect) — **SPEC_READY — endpoints LIVE in prod 2026-08-18, kickoff pasteable.** Phase 4 of `tournaments_server_side` §6b: tournament entry, per-hole submission and the leaderboard move to the backend — every player sees the same board, server-generated bot field with organic reveal, bots retire one-way at 10 humans, sticky row shows `#rank · PRIZE #prize_rank` while bots pad. Unity-only: new `RemoteTournamentBackend` behind the existing `ITournamentBackend` seam, submissions ride a pending-ops queue, `LocalTournamentBackend` stays for bot/demo/signed-out. Spec: `Docs/Specs/Active/tournament_async_board/SPEC.md`.

### Kickoff · tournament_async_board (issued 2026-08-18)

```
Read Docs/Specs/Active/tournament_async_board/SPEC.md and implement it.

Context:
- Tournaments become real async multiplayer: enter/submit/leaderboard now
  live on the backend (endpoints are live; spec §1 is the contract,
  verbatim). Every player must see the same board.
- ITournamentBackend was built for this swap ("Later: RemoteTournamentBackend
  (REST). UI code never changes."). New code goes in
  Assets/Scripts/TournamentsRuntime/ — do NOT add Golfin.Net to
  Golfin.Tournaments.asmdef (hard-won rule).
- Register: the server debits the entry fee (deterministic spend key). Do
  NOT also debit via IRewardPointsService — that double-charges. Trigger the
  rp_balance_sync refresh instead. Insufficient/offline use the existing UX.
- SubmitHoleResult: local persist first, then enqueue — mirror
  Economy/PendingOpsStore (FIFO, idempotency GUID per hole, drop on
  replayed:true and on 400). Newtonsoft: DateParseHandling.None, both
  reader and serializer, or schedules shift by timezone.
- Leaderboard: map the payload verbatim, do NOT re-rank. Sticky row shows
  "#rank · PRIZE #prize_rank" while bots_active and they differ.
- Provider selection: BotSessionOverride / signed-out / DemoGate keep
  LocalTournamentBackend — bots are offline by design, never hit prod.
- GetResults/ClaimPrize: final rank from the server board (prize_rank);
  the award keeps the existing earn-game tournament_prize path, with a
  NOTE where Phase 5 re-points it.
- Out of scope: Phase 5 resolver/payout, dashboard editors, GPS endpoints,
  tournament banners, the Rankings screen.

When done: list changed files with a 1-line summary each, run the FULL
per-assembly EditMode sweep + the new tests in spec §5, flag which §5 manual
items need Cesar's device pass, update STATUS.md + IMPLEMENTER_REPORT.md in
the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`leaderboard_backend`** (filed 2026-08-18, Architect) — **SPEC_READY — endpoint LIVE in prod 2026-08-18, kickoff pasteable.** The Rankings screen moves off `LocalFakeLeaderboardProvider` onto `GET /api/v1/leaderboards/{period}` — same board for every player, ranks + ties computed server-side, fakes served from the server pool, character portrait/level synced via `PUT /user/golfin-character`. Unity-only: new `BackendLeaderboardProvider` behind the existing `ILeaderboardProvider` seam (built for this exact swap), disk-cached last payload, refresh driven from `RankingsScreenController.OnEnable`/tab taps, `LocalFakeLeaderboardProvider` retired to the bot/signed-out path only. Spec: `Docs/Specs/Active/leaderboard_backend/SPEC.md`.

### Kickoff · leaderboard_backend (issued 2026-08-18)

```
Read Docs/Specs/Active/leaderboard_backend/SPEC.md and implement it.

Context:
- The Rankings screen currently runs on LocalFakeLeaderboardProvider (client
  fakes + local SaveData accumulators). The backend endpoint is live and is
  now the single source of truth; spec §1 is the contract, verbatim.
- ILeaderboardProvider was built for this swap — UI code stays untouched
  except the refresh hooks in RankingsScreenController.OnEnable/OnTabClicked
  (§4). No prefab or scene edits.
- Reuse, do not rebuild: ApiClient (Get<T> :67; add a one-line Put<T> —
  §1 note), the RemoteBannerSource atomic disk-cache discipline, Endpoints
  (+2 lines), PlayerIdentity.DisplayNameOr for the player's own row.
- Server already computes ranks + T-ties (1,2,2,4) — do NOT re-rank
  client-side. player row is always present; character_id can be null.
- Provider selection (§4): BotSessionOverride / signed-out keeps LocalFake —
  bots are offline by design and must never hit prod.
- Character sync (§5): OnCharacterSelected + OnCharacterLeveledUp +
  sign-in, fire-and-forget, throttled, silent failure. For the sign-in hook
  reuse whatever rp_balance_sync lands — do not invent a second auth event.
- Out of scope: previous-period popup, leagues, SNS share, fake-pool
  dashboard panel, tournament leaderboards, backend edits.

When done: list changed files with a 1-line summary each, run the FULL
per-assembly EditMode sweep + the new tests in spec §7, flag which §7 manual
items need Cesar's device pass, update STATUS.md + IMPLEMENTER_REPORT.md in
the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`home_notices`** (filed 2026-08-18, Architect) — **SPEC_READY. The Home screen's notice panel becomes admin-controlled — title + body, EN + JA, scheduled, no client build.** Server side is DONE and deployed by the Architect: table `public.home_notices` (migration `2026_08_18_home_notices.sql`, **APPLIED to prod 2026-08-18** — service key reads 200 `[]`, anon 401, and the endpoint was smoke-tested with four rows proving live/expired/future/draft filtering before they were deleted), `GET /api/v1/notices` (`backend/routers/notices.py`, no auth, server-side scheduling, `expires_at` echoed for the on-device cache), and a Notices panel in the admin dashboard (live at https://admin.golfin.world). **The Unity client is the only outstanding half.** New `Assets/Scripts/NoticesRuntime/` (`Golfin.Notices`, no asmdef) mirroring `BannersRuntime` file-for-file — `RemoteNoticeDtos` / `RemoteNoticeSource` (cache `home_notices.json`, atomic write, null on any failure) / `NoticeService` (singleton, sync cache read in `Awake`, throttled `Refresh()` on `ScheduleRefreshThrottle`, `OnNoticesChanged`, `LocalizationManager.OnLanguageChanged`) — plus one `Endpoints.Notices` line and a rewrite of `HomeScreenController.UpdateNewsContent()` to page the live notices through the dots that already exist. ⚠️ Two behaviour changes to be aware of: **with nothing live the panel HIDES** (it is an announcement surface, not a fixture — banners have a bundled sprite behind them, an unwritten announcement has nothing), and the bundled `HOME_MAINTENANCE_*` strings are **retired, not kept as an offline fallback** — they currently tell every player the servers go down on **2025/12/31**, a date eight months past, which is the bug this feature exists to fix. Demo build (`DemoGate.IsDemo`) path is unchanged. Needs one scene wiring step (`newsPanelRoot` on HomeScreenController in ShellScene). Spec: `Docs/Specs/Active/home_notices/SPEC.md`.

### Kickoff · home_notices (issued 2026-08-18)

```
Read Docs/Specs/Active/home_notices/SPEC.md and implement it.

Context:
- The Home notice panel ("MAINTENANCE NOTICE" + body) currently reads two
  hardcoded LocalizationText.csv keys and tells every player the servers go
  down on 2025/12/31. This puts the copy under admin control instead.
- Server side is already live — GET /api/v1/notices returns the scheduled,
  ordered, EN+JA notices. Client only. Spec §2 is the contract.
- Mirror Assets/Scripts/BannersRuntime/ file-for-file: same singleton shape,
  same disk-cache discipline (raw body, atomic .tmp+replace, null on failure),
  same ScheduleRefreshThrottle, same silent-failure posture. Read BannerService
  and RemoteBannerSource before writing anything.
- Minimal diff elsewhere: one Endpoints.Notices line, one new serialized field
  on HomeScreenController (newsPanelRoot), and UpdateNewsContent rewritten.
  Do NOT touch the DemoGate path.
- An empty notice list is a NORMAL state and means hide the panel. Do not keep
  HOME_MAINTENANCE_* as an offline fallback — spec §4.3 explains why.
- Out of scope: rich text, targeting, push, a third language.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification (§5 items 1–7
mostly need a device + the dashboard), update STATUS.md +
IMPLEMENTER_REPORT.md in the spec folder, and update Docs/AI_CONTEXT.md.
```

- **`beta_telemetry`** (filed 2026-08-18, Architect) — **SPEC_READY. Telemetry for next week's 20-tester live beta — Unity client + backend half.** A batching `TelemetryService` (new `Assets/Scripts/Telemetry/`, Assembly-CSharp) subscribes to EXISTING events — `ScreenManager.ScreenChanged`, `GameSession.OnHistoryChanged`/`OnHoleComplete` (ShotRecord already carries club/distance/OB/surface — the shot telemetry is free), `RewardPointsManager.OnPointsChanged`, `CharacterManager.OnCharacterLeveledUp`, `Application.logMessageReceived` — plus ~4 one-line insertions (`GameSession.SeedSession`, the two ShotController cancel/reject branches — raised as static events and relayed through a tiny `ShotTelemetryRelay` in `Golfin.Gameplay.UI`, because `Golfin.Gameplay.Input` is autoReferenced:false and Assembly-CSharp can't see `ShotController`; verified, see spec — and `Endpoints.cs`). Ships through the EXISTING `ApiClient` (Bearer/envelope/retry/401-replay — nothing re-implemented) to a new authed `POST /api/v1/telemetry/events` (`backend/routers/telemetry.py`) writing one `telemetry_events` table (migration `2026_08_18_telemetry_events.sql`, client-GUID `event_id` unique = idempotent retries, RLS on / no policies = service_role only). 13 events: sessions, screen funnel, round/shot/hole (incl. `flick_rejected` — THE control-feel number), abandons, capped client exceptions, FPS avg/low per hole, points/level-up/SP. Editor sends OFF unless `GOLFIN_TELEMETRY_DEBUG`. **Migration first, deploy second** (ops doc §3.2): Cesar pastes SQL, REST-probe verify, then `fly deploy`. Spec: `Docs/Specs/Active/beta_telemetry/SPEC.md`.

### Kickoff · beta_telemetry (issued 2026-08-18)

```
Read Docs/Specs/Active/beta_telemetry/SPEC.md and implement it.

Context:
- 20 external TestFlight testers play live next week; this captures controls
  quality (flick rejects/cancels/OB), the drop-off funnel, crashes, and economy
  events from their devices. Two halves: Unity client + playlife-api endpoint.
- Almost everything hooks EXISTING events (spec lists file:line for each). Only
  ~4 one-line insertions into existing files; everything else is new files under
  Assets/Scripts/Telemetry/ and backend/routers/telemetry.py.
- Reuse ApiClient.Instance + Endpoints (add one URL). Do NOT add retry/auth
  logic — ApiClient already does Bearer, envelope unwrap, transient retry, 401
  refresh-replay. Follow points.py for the router shape.
- MIGRATION FIRST: write migrations/2026_08_18_telemetry_events.sql, hand Cesar
  the SQL for the Supabase SQL editor, REST-probe that the table exists, THEN
  fly deploy (ADMIN_DASHBOARD_OPS §3.2). Deploying first 500s the endpoint.
- Telemetry must never break gameplay: every hook wrapped, queue capped at 500,
  one re-enqueue then drop. EditMode tests per spec §5 (fake-transport style of
  ApiClientTests.cs).
- Spec has NOTE flags (par accessor, SP call site, JSON serializer choice, boot
  choke point; the asmdef/relay question is already resolved in the spec) — resolve against the codebase and record
  what you found; if a NOTE has no clean answer, skip that item and flag it.
- Out of scope: the admin panel (separate spec telemetry_admin_panel), offline
  queue persistence, remote kill switch, retention jobs, anything GPS/PII.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`telemetry_admin_panel`** (filed 2026-08-18, Architect) — **SPEC_READY. Sixth dashboard panel: read the beta telemetry.** New read-only **Telemetry** panel in `Tools/admin-dashboard` (registry entry uses the already-defined-but-unused `"chart"` icon): KPI cards, session funnel (Home → hole select → round_start → hole_complete), per-hole table (strokes/OB%/abandons/fps_low), shot-quality cards (**flick reject rate is the headline number**), per-tester rollup, raw event explorer with filters + real pagination. Reads `telemetry_events` directly via service_role like every panel; aggregates in TS (20 testers = trivial volume; hard 10k row cap with a visible `truncated` badge, never silent). `checkAdmin()` on all three new API routes; NO audit writes (read-only). No chart lib — div bars. **Buildable NOW in mock mode** (`lib/mockTelemetry.ts`, deterministic fixture) — does not block on the `beta_telemetry` migration; acceptance includes the empty-table live state (no NaN/div-zero) and the §2 post-deploy 302 Access check. Spec: `Docs/Specs/Active/telemetry_admin_panel/SPEC.md`.

### Kickoff · telemetry_admin_panel (issued 2026-08-18)

```
Read Docs/Specs/Active/telemetry_admin_panel/SPEC.md and implement it.

Context:
- Read-only Telemetry panel for admin.golfin.world so Cesar + Ken can read next
  week's 20-tester beta: funnel, per-hole difficulty, flick-reject rate, crashes,
  per-tester table, raw event explorer.
- Copy the Tournaments panel STRUCTURE (page.tsx + client component + api
  routes) but none of its mutation/editor parts. checkAdmin() first line of
  every route. No lib/audit.ts — nothing mutates. No new npm deps; bars are
  plain divs.
- Event names/payloads come from Docs/Specs/Active/beta_telemetry/SPEC.md §1-§2
  ONLY — do not invent fields. Table may not exist yet: build + verify in mock
  mode (deterministic fixture, no Date.now()), and make the live empty-table
  state render clean zero states.
- Aggregate in TypeScript in lib/telemetryData.ts (mirror tournamentData.ts
  naming); 10k row cap with a visible truncated badge. Explorer route paginates
  server-side with .range(), 100/page.
- Reuse the Users panel's user_id -> email/name lookup — do not write a second
  profiles query pattern.
- Deploy loop per ADMIN_DASHBOARD_OPS: NEVER next build while next dev runs;
  NODE_ENV=development npm run dev; npm run deploy; then verify the 302 curl
  check. A 200 on / means Access broke - stop and investigate.
- Out of scope: mutations, CSV export, chart libs, realtime refresh, retention,
  the Unity/backend half.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual verification (live smoke needs real
rows), update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```


- **`fastlane_testflight_pipeline`** (filed 2026-08-17, Architect) — **SPEC_READY. One command replaces the whole manual TestFlight loop.** `bundle exec fastlane ios testflight_build` → Unity batchmode build → archive → upload. **Does NOT make it faster** — Unity + IL2CPP still dominate; it makes it *unattended* and removes the four GUI steps a human currently gets wrong. ⚠️ **Read the interaction section first:** scheme post-actions do not reliably fire under `xcodebuild`, so the Xcode-post-action half of `upload_guard_automation` silently stops working under this pipeline. Resolution is in the spec and is strictly better — call `Tools/mark-uploaded.sh` from the Fastfile after upload succeeds, which fires on real *upload* rather than *archive* and removes that spec's known over-strictness. Keep both; the script is idempotent. ⚠️ **Ruby is the real prerequisite:** this Mac has system Ruby **2.6.10**, EOL and Apple-deprecated — `brew install fastlane` (vendors its own Ruby), never `gem install` against system Ruby. **Blocked on Cesar for the end-to-end run only:** the App Store Connect API key must be generated by hand (Users and Access → Integrations; the `.p8` downloads once, store it outside the repo). Code builds and verifies everything up to `build_app` without it. Decisions already taken, do not re-litigate: no `match` (automatic signing works on one machine), no `groups:` (external-only in fastlane; `In-House Testers` auto-distributes), `skip_waiting_for_build_processing: true` (costs changelog support, worth it). Highest-risk item in the whole task: **a batchmode Unity build that fails but exits 0** — that is how a stale binary reaches TestFlight; the spec makes proving the non-zero exit an explicit acceptance line. Spec: `Docs/Specs/Active/fastlane_testflight_pipeline/SPEC.md`.

### Kickoff · fastlane_testflight_pipeline (issued 2026-08-17)

```
Read Docs/Specs/Active/fastlane_testflight_pipeline/SPEC.md and implement it.

Context:
- Goal is one command: Unity batchmode build -> archive -> TestFlight upload. The
  manual path was proven 2026-08-17 with 1.5.7 (2192); this automates it.
- Ruby FIRST: system Ruby is 2.6.10 (EOL, Apple-deprecated). Use `brew install
  fastlane`, which vendors its own Ruby. Do NOT gem install against system Ruby,
  and do not modify it. Record the exact commands you used in the report.
- New: Assets/Editor/CIBuild.cs (BuildIOS entry point), Tools/unity-build-ios.sh,
  Tools/assert-unity-closed.sh, fastlane/Fastfile + Appfile + .env.example.
- CIBuild must activate the iOS-Full profile via the BuildProfile API, NOT the
  -activeBuildProfile CLI flag (Unity 6 batchmode bug, see DEMO_BUILD_PLAN §3.1).
- MOST IMPORTANT: a Unity batchmode build that fails must exit NON-ZERO. Prove it
  with a deliberately broken build. A silent zero-exit uploads stale binaries.
- Read the "Interaction with upload_guard_automation" section before touching the
  guard. Short version: call Tools/mark-uploaded.sh from the Fastfile after upload,
  keep the scheme post-action for manual GUI archives.
- Do NOT create the App Store Connect API key — Cesar does that by hand. Everything
  through build_app is verifiable without it; upload_to_testflight is not. Flag it
  as awaiting Cesar rather than marking it PASS.
- Out of scope: match, Android, CI runners, changelog automation, external groups.

When done: list changed files with a 1-line summary each, run the acceptance tests in
the spec, flag which need manual verification (the end-to-end run is Cesar-only),
update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`upload_guard_automation`** (filed 2026-08-17, Architect) — **SPEC_READY. Small, self-contained build-tooling task; fell out of Order 424.** `BuildStampGenerator` has a regression guard that refuses a store build whose number is ≤ the last uploaded one, and that value lives in `Docs/Versioning/last_uploaded_build.txt` — written **only** by the menu item `GOLFIN → Build → Mark Current Commit As Uploaded`. Nobody ran it after the 2026-08-17 upload of `1.5.7 (2192)`, so **the file still reads `0` and the guard has been inert since it was written.** Cesar's call: automate it via an **Xcode Archive post-action**. ⚠️ **The post-action cannot be added through Xcode's Edit Scheme UI** — Unity regenerates `Unity-iPhone.xcodeproj` including schemes on every Replace build and would wipe it; it has to be injected from Unity on every iOS build, the same way `iOSPostProcess.cs` already injects `ITSAppUsesNonExemptEncryption` into `Info.plist`. Two deliverables: `Tools/mark-uploaded.sh` (never regresses, always exits 0, logs to a gitignored file — a failing post-action is invisible in Xcode) and scheme-injection in the existing iOS post-process. Two paths need **verifying, not assuming**: where Unity 6000.3.9f1 actually writes the `.xcscheme` (`xcshareddata` vs `xcuserdata`), and the `$PROJECT_DIR` relative depth to the repo root. Known and accepted trade-off: it fires on **archive**, not upload, so a discarded archive still advances the guard — over-strict is fine here because the build number is `git rev-list --count HEAD` and Cesar commits between store builds anyway. **ASC API integration was considered and explicitly rejected** — key management for a problem whose worst case is one wasted archive. Do not delete the menu item; it stays as a manual escape hatch. Spec: `Docs/Specs/Active/upload_guard_automation/SPEC.md`.

### Kickoff · upload_guard_automation (issued 2026-08-17)

```
Read Docs/Specs/Active/upload_guard_automation/SPEC.md and implement it.

Context:
- BuildStampGenerator's regression guard reads Docs/Versioning/last_uploaded_build.txt,
  which today is only written by a menu item a human has to remember. It was missed
  after the 1.5.7 (2192) upload, so the file reads 0 and the guard is inert.
- Two deliverables: Tools/mark-uploaded.sh (git rev-list --count HEAD, write only if
  greater, always exit 0, log to a gitignored file), and injection of an Xcode Archive
  post-action into the generated .xcscheme from a [PostProcessBuild] callback.
- The post-action MUST be injected from Unity, not added in Xcode's UI — Unity
  regenerates the scheme on every Replace build. Follow the pattern already in
  Assets/Editor/iOSPostProcess.cs and do not disturb its Info.plist behaviour.
- Two things to VERIFY rather than assume, per the spec's NOTE markers: the actual
  .xcscheme path Unity 6000.3.9f1 emits, and the $PROJECT_DIR depth to the repo root.
  Flag with a NOTE comment if either can't be confirmed.
- Minimal diff. Do not touch BuildStampGenerator's numbering logic, the menu item,
  or anything Android.
- Out of scope: App Store Connect API integration, auto-committing the guard file.

When done: list changed files with a 1-line summary each, run the acceptance tests in
the spec, paste the generated <PostActions> XML fragment into the report, flag which
need manual on-device verification (the real Product -> Archive run is Cesar-only),
update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`tournament_banners`** (filed 2026-08-17, Architect) — **SPEC_READY. THE feature the banner epic was for, and the one part that never got built.** Home-promo and Rankings banners are live end to end; **tournament banners do not exist in the admin at all.** This was `game_banners` **§9**, amended in after Code had already started, not implemented, and then carried into `Docs/Specs/Completed/` when that spec closed — so it went invisible. Refiled standalone; the §9 text in the Completed spec is superseded. **Cesar's decision:** the artwork is managed in the **Banners** panel like every other banner, but **which** banner a tournament shows is chosen **per tournament in the Tournaments panel** — upload once, assign many, switch off in one place. **Four gaps, verified in the tree 2026-08-17:** (1) the `game_banners.placement` CHECK allows only `('home_promo','rankings')`; (2) there is no `tournaments.modal_banner_id`; (3) the tournament editor has no picker; (4) `GET /tournaments/golfin` does not join it and the client has no DTO. ⚠️ **The consuming side is already built and tested** — `ApplyBanner`, `ApplyBannerState`, the 1411 ↔ 1167 padding switch, the strip, the button and the link handler all shipped with `tournament_signup_modal`; `TryResolveModalBanner` (`TournamentSignupModalController.cs:532`) is a 3-line `return false` stub. **Landing this is that one resolver plus the data feeding it — not the prefab, not the layout.** Also pinned: do NOT add `TournamentModal` to `BannerService.BannerPlacement` (a tournament banner never comes through `/api/v1/banners`, and adding it there builds a second unreachable path that looks like it works); `is_active` is checked server-side so the client never learns that column exists; `modal_banner_id` must not appear in the payload. Spec: `Docs/Specs/Active/tournament_banners/SPEC.md`.

### Kickoff · tournament_banners (issued 2026-08-17)

```
Read Docs/Specs/Active/tournament_banners/SPEC.md and implement it.

Context:
- Tournament banners are the one banner placement that was never built. Home
  promo and Rankings are live; the tournament sign-up modal's cross-promotion
  strip has no admin at all. This was game_banners section 9, amended in late,
  skipped, and then filed under Completed with the rest of that spec.
- The consuming side is DONE. ApplyBanner, ApplyBannerState, the 1411/1167
  padding switch, _bannerRoot/_bannerImage/_bannerButton and the link handler
  all shipped and are tested. TryResolveModalBanner at
  TournamentSignupModalController.cs:532 is a three-line stub returning false.
  You are implementing that resolver and the data that feeds it. Do NOT touch
  ApplyBanner, ApplyBannerState, the padding switch, or the prefab.
- Build in this order: migration -> verify the columns over PostgREST ->
  backend + fly deploy -> verify with curl -> dashboard + npm run deploy ->
  Unity. Deploying a .select() naming a missing column 500s the WHOLE schedule
  endpoint for every player (Docs/ADMIN_DASHBOARD_OPS.md 3.2).
- Find the real name of the placement CHECK constraint with
  pg_get_constraintdef before dropping it. Do not trust the name in the spec.
- Do NOT add TournamentModal to BannerService.BannerPlacement. A tournament
  banner never arrives through /api/v1/banners; adding it there creates a
  second unreachable code path that looks like it works.
- The banner's is_active check happens server-side, and modal_banner_id must
  not appear in the payload.
- Regression bar: Home promo and Rankings banners must still render, the
  no-banner modal must still measure exactly 1167, and the schedule endpoint
  must still return all 6 tournaments with its 19 base fields.
- Out of scope: the result/CLAIM modal 13894:3628, scheduling or rotation for
  tournament banners, tournaments.banner_url and the tournament-art bucket
  (that is the 260x360 card art, a different image in a different bucket), and
  analytics.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`tournament_signup_modal`** (filed 2026-08-17, Architect) — **SPEC_READY. The sign-up confirmation modal goes from a four-line card to the full pre-entry briefing.** Figma `13480:2479` (978×531) → **`13498:2067` "INFO + Banner"** (978×1411): cross-promotion banner, existing header + date line, tournament card art beside a description blurb, a RULES block, then entry/prize and BACK · CONFIRM. **Cesar's decisions (2026-08-17):** **both** layouts ship from **one prefab** — `13498:2067` when the tournament has a banner, `13892:3454` when it does not; the banner is **created in the Banners panel and assigned per tournament in the Tournaments panel** (`game_banners` SPEC §9); **RULES stays hardcoded but localized**; the blurb gets **new per-tournament `description_en` / `description_ja` columns plus a `description_key` that overrides them when it resolves — the same ladder shape as the tournament title**. ⚠️ Notes the spec pins: **the title is Rubik Bold 42, overriding the Figma's Noto Sans JP (Cesar, 2026-08-17)** — Rubik has no CJK glyphs, so the title MUST use `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`, the only Rubik asset that declares `NotoSansJP` in its `m_FallbackFontAssetTable`, or every Japanese tournament name renders as tofu; **the two states are not one layout with a hidden row** — the content container's top padding switches 0 ↔ 32 with the banner (1411 vs **1167**, not 1379), and the banner is 970 wide with 4px margins so it must NOT inherit the 48px side padding; the 📍 pin `13498:2079` is `hidden="true"` in the design and must NOT be built; CANCEL becomes **BACK** as a label change only — `_cancelButton` keeps its field name so the prefab reference survives; buttons are **359 / 391**, no longer symmetric; and `OnConfirm` / `CompleteSignup` / `TrySpendAsync` / the RP pre-check are **untouched — this task is presentation over a live payment path**. Deliberately does NOT reuse the GPS-owned `tournaments.description` column. The banner half is sequenced last: §5.1 collapses `_bannerRoot` when there is none, so the modal ships complete before `game_banners` lands. Japanese for the six `tourn.rules.*` keys is **written into the spec** (Architect, full-width `：` per the table's existing convention) — unreviewed by a native speaker, flag it in the report. Renders in `reference/`. Spec: `Docs/Specs/Active/tournament_signup_modal/SPEC.md`.

### Kickoff · tournament_signup_modal (issued 2026-08-17)

```
Read Docs/Specs/Active/tournament_signup_modal/SPEC.md and implement it.

Context:
- Rebuilds the tournament sign-up confirmation modal from Figma 13480:2479 to
  13498:2067 "INFO + Banner". Renders for both are already in the spec folder's
  reference/ - A/B against reference/target_13498-2067_info_banner.png at
  1170x2532. Node ids are in the fidelity table; pull anything else you need
  with get_design_context, file key 5gEAHjl6xAtW8iYY7NMvWd.
- Every value except RULES comes from the tournament admin. RULES is hardcoded
  this pass but must go through LocalizationManager - six new rows in
  Assets/Localization/LocalizationText.csv, then Tools > Localization >
  Import Text CSV. A CSV row without the re-import does nothing.
- Minimal diff. Extend TournamentSignupModalController.Populate, do not rewrite
  it: every existing binding (sponsor, title, venue, date line, entry pill,
  reward) stays byte-for-byte, apart from the title's font asset. Reuse
  TournamentArtService for the 260x360
  thumbnail and TournamentDisplayName as the shape for the new
  TournamentDescription ladder.
- DO NOT TOUCH OnConfirm, CompleteSignup, Register, TrySpendAsync, the RP
  pre-check, the GetMyEntry short-circuit or the navigation target. This is a
  presentation change sitting on top of a live payment path, and the acceptance
  list has regression items that will catch it if you do.
- BOTH states ship from ONE prefab. 13498:2067 with a banner, 13892:3454
  without. They are NOT the same layout minus one row: the content container's
  top padding switches 0 (banner) to 32 (no banner), so the heights are 1411 and
  1167. Toggling only the banner gives you 1379 with a gap at the top. Let a
  layout group drive height; do not hard-code either number.
- Four traps the spec pins: (1) the title is Rubik Bold 42, NOT the Figma's Noto
  Sans JP - Cesar's override - and because Rubik has no CJK glyphs it must use
  Assets/Fonts/Rubik-VariableFont_wght SDF.asset, the one Rubik asset with
  NotoSansJP in its fallback table, or Japanese tournament names render as tofu;
  (2) the banner is 970 wide with 4px side margins and must not inherit the
  container's 48px padding; (3) the pin glyph 13498:2079 is hidden in the design
  and must not be built; (4) BACK is a LABEL change only - keep the
  _cancelButton field name so the prefab reference survives.
- The six tourn.rules.* rows including Japanese are written out in SPEC section
  4. Paste them verbatim, full-width colons included.
- Build sections 1-6 first and ship. The banner half depends on
  Docs/Specs/Active/game_banners/ section 9; section 5.1 collapses _bannerRoot
  when there is no banner, so the modal is complete without it. Do not block.
- Out of scope: the result/CLAIM modal 13894:3628 and
  TournamentResultModalController, making RULES admin-editable, reusing the
  GPS-owned tournaments.description column, uploading banner art from the
  tournament editor, and adding description columns to the shipped
  tournaments.csv.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`game_banners`** (filed 2026-08-17, Architect) — **SPEC_READY. The two banner images baked into the build get an admin and a live fetch.** `Assets/Art/HomeScreen/GPS Banner.png` (`Canvas/ScreensRoot/HomeScreen/PromoBanner`) and `Assets/Art/RankingsScreen/Banner.png` (`RankingsScreen/ContentArea/Banner`, in the Rankings prefab) can only change today by shipping a build. This adds a `public.game_banners` table, a no-auth `GET /api/v1/banners` on playlife-api mirroring `/tournaments/golfin`, a **Banners** panel at `admin.golfin.world/banners`, and a client that swaps the bundled sprite for the served image and opens an allowlisted external URL on tap. **Cesar's decisions (2026-08-17):** two placements only (`home_promo`, `rankings`) — the Home news carousel is OUT; **image per locale, no text fields**; **one live banner per placement**, no rotation; tap opens an **external URL** behind a **client-side host allowlist**; delivery via **playlife-api**, not Supabase-direct. ⚠️ Two verified traps the spec turns on: (1) `HomeScreenController.promoBannerButton` / `promoBannerText` / `gpsIcon` are all `{fileID: 0}` in `ShellScene.unity` — **unassigned**, so `OnPromoBannerClicked` has never run in a build and the strip is a dead `Image` with no `Button`; (2) `TournamentArtService` must be **parameterized, not forked** — it is the only image-download path in the project and carries the pre-buffer size cap, `redirectLimit = 0` and the LRU sweep. Bundled sprites stay in the build and remain the fallback: no network, expired window, or nothing scheduled all render exactly what players see today. **OPEN — needs Cesar:** confirm the link-host allowlist (spec seeds it with `golfin.io` + `golfin.world` from `SettingsController.cs:188-209`); an admin cannot add a host from the dashboard, by design. Spec: `Docs/Specs/Active/game_banners/SPEC.md`. **AMENDED 2026-08-17 (Cesar): a third placement `tournament_modal` — see SPEC §9.** Tournament banners are created and managed in the Banners panel but **assigned per tournament** from the Tournaments panel (`tournaments.modal_banner_id` → a `game_banners` row), so one GPS promo serves every tournament and is swapped in one edit. `tournament_modal` rows are NOT served by `GET /api/v1/banners` — they ride on `GET /tournaments/golfin` as a `modal_banner` object, with the `is_active` check done server-side. Art spec 970×252, the same as the `rankings` slot. Consumer: `Docs/Specs/Active/tournament_signup_modal/`.

### Kickoff · game_banners (issued 2026-08-17)

```
Read Docs/Specs/Active/game_banners/SPEC.md and implement it.

Context:
- Two banner images are baked into the build and can only change with a store
  release: the Home promo strip (Canvas/ScreensRoot/HomeScreen/PromoBanner in
  Assets/Scenes/ShellScene.unity) and the Rankings banner
  (RankingsScreen/ContentArea/Banner in
  Assets/Prefabs/UI/Rankings/RankingsScreen.prefab). This adds a game_banners
  table, GET /api/v1/banners on playlife-api, a Banners panel in
  Tools/admin-dashboard, and the client half that swaps the sprite and opens an
  allowlisted external URL on tap.
- Build it in the order the spec lists: migration -> verify the columns landed
  over PostgREST -> FastAPI + fly deploy -> verify with curl -> dashboard panel
  -> Unity. Deploying code that reads a column that does not exist yet 500s the
  endpoint (Docs/ADMIN_DASHBOARD_OPS.md 3.2).
- Minimal diff. Reuse, by name: TournamentArtService + TournamentArtPolicy
  (parameterize per SPEC 4.1 - do NOT fork the downloader),
  RemoteTournamentSource as the shape for RemoteBannerSource,
  ScheduleRefreshThrottle verbatim, ApiClient.Get<string> + Endpoints,
  checkAdmin + writeAudit + isMockMode + getSupabaseAdmin on the dashboard side,
  and uploadTournamentArt / ArtworkTab as the upload template.
- VERIFIED TRAP: HomeScreenController.promoBannerButton, promoBannerText and
  gpsIcon are all {fileID: 0} in ShellScene.unity. The promo strip is an Image
  with no Button and no wiring; OnPromoBannerClicked has never run. You are
  adding that wiring, not reusing it. Leave promoBannerText and gpsIcon
  unassigned - the content model is image-only.
- Do NOT add the new runtime code to Golfin.Tournaments.asmdef. It goes in
  Assets/Scripts/BannersRuntime/ with no asmdef, i.e. Assembly-CSharp, the same
  arrangement as Assets/Scripts/TournamentsRuntime/.
- Every pre-existing TournamentArtPolicy / TournamentArtService EditMode test
  must still pass unmodified. If one needs editing, the extraction changed
  behaviour - stop and re-read.
- Out of scope: the Home news/announcement carousel (NoticePanel, PageDots,
  HOME_MAINTENANCE_*), the TournamentLeaderboardScreen banner and its sponsor
  pills, any banner text, rotation/carousels, targeting, analytics, in-game
  deep links, and tournaments.banner_url / the tournament-art bucket.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`tournament_card_art_mask`** (filed 2026-08-14, Architect) — **SPEC_READY, quick.** The remote-art path works end to end (Cesar's screenshot: uploaded art rendering on the Lomond card), with one presentation defect — `tournament_image` is a 260×360 rect sitting flush over the card's two rounded LEFT corners, so every image renders square corners past the ~44 px radius. Cesar's call: fix it with a **mask**, not by pre-rounding each file before upload — right, now that art comes from the dashboard. Everything needed already exists: `Assets/Art/Original UI/Common/S_Common_BGCorner20Left.png` (guid `a007e88d378a6d04da972c3519543ec4`, border `25,25,0,25`) rounds the left two corners and leaves the right two square, and is referenced by **zero** prefabs — authored for exactly this and never used; and `StaminaShopCard.prefab` (same 978×360 archetype, same background sprite) already implements the pattern — a `Mask` carrying a sliced `S_Common_BGCorner20*` Image with `ShowMaskGraphic: 0`, photo as its child. One prefab, no C# change: drop the useless `RectMask2D` off `tournament_image`, make its Image the sliced left-rounded sprite at `pixelsPerUnitMultiplier ≈ 0.36` (16 ÷ ppu = radius; the card frame is ~44), add `Mask` with `ShowMaskGraphic: 0`, add a stretched `Photo` child, and re-point `_tournamentImage` at it. Spec: `Docs/Specs/Quick/tournament_card_art_mask/SPEC.md`. Kickoff below.

### Kickoff · tournament_card_art_mask (issued 2026-08-14)

```
Read Docs/Specs/Quick/tournament_card_art_mask/SPEC.md and implement it.

Context:
- The remote tournament art now renders, but tournament_image is a plain
  260x360 rect over the card's two rounded LEFT corners, so every image shows
  square corners past the card's ~44px radius. Art comes from the dashboard
  now, so this has to be a mask, not per-file editing before upload.
- Do NOT write a shader. The project has no UI shaders and no soft-mask
  package. Follow the pattern already in
  Assets/Prefabs/UI/Shop/StaminaShopCard.prefab (same 978x360 card archetype,
  same d162244f background sprite): a Mask carrying a sliced
  S_Common_BGCorner20* Image with ShowMaskGraphic: 0, photo as its child.
  StaminaShopHeroCard -> HeroMask and StaminaMenuRow -> Thumbnail/PhotoMask are
  the same thing.
- Use Assets/Art/Original UI/Common/S_Common_BGCorner20Left.png (guid
  a007e88d378a6d04da972c3519543ec4, spriteBorder 25,25,0,25). It rounds the
  left two corners only, which is the shape this card needs — the image's right
  edge is interior, not a card edge. It is currently referenced by nothing.
- Only Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab changes. No
  C# change: re-pointing _tournamentImage at the new Photo child is a
  serialized reference, and SetCourseImage keeps working untouched.
- Radius has to MATCH, not merely exist. Start at pixelsPerUnitMultiplier 0.36
  and confirm against CardBackground's corner by eye at 1170x2532. Close-but-
  wrong reads worse than square.
- Also delete the RectMask2D on tournament_image — it is axis-aligned clipping,
  it can never round a corner, it clips nothing today, and RectMask2D next to a
  stencil Mask on one object is a trap.

When done: screenshot at 1170x2532 showing a remote-art card, a bundled-art
card and a no-art card, run the acceptance list in the spec, confirm the
EditMode suite is unchanged, and update STATUS.md.
```

- **`tournaments_unity_wiring`** (filed 2026-08-14, Architect) — **SPEC_READY. Phase 3 of the tournaments epic: the game finally reads the schedule from the server, artwork and all.** Phases 1 (schema, prod) and 2 (dashboard panel) shipped 2026-08-13/14, but the client still loads `Assets/Resources/Data/tournaments.csv`, so every dashboard edit needs an export + a build. **Cesar's 2026-08-14 decision reshaped this phase:** *"Tournaments names/images are not necessarily tied to a country club. Can be brands as well."* — so a tournament is brand-led as often as venue-led, which (a) promotes remote art from a deferred "3b" into this phase (a bundled course photo cannot express a brand) and (b) exposes a real gap: the card name is `LocalizationManager.Get(def.NameKey)` with **no fallback** (`TournamentSelectionScreenController.cs:153`) and localization keys ship in the build, so a dashboard-created tournament would render its raw key. The server now sends `title` and the client falls back to it. `course_id` keeps one job: which venue is played. Art order becomes `banner_url` → `Resources/TournamentImages/{course_id}` → placeholder, and the positional `_courseImages[csvIndex]` fallback is **deleted** (it silently reshuffles photos the moment the dashboard can reorder). Also inside: `Golfin.Net` must NOT be added to `Golfin.Tournaments.asmdef` — the fetch lives in `TournamentsRuntime/` (Assembly-CSharp, which already sees it); CSV stays the offline fallback; state stays client-derived; one new no-auth endpoint `GET /api/v1/tournaments/golfin` plus a `kind` filter on `/active` and `auto_enter_score` so GPS and game rows stop bleeding into each other. Spec: `Docs/Specs/Active/tournaments_unity_wiring/SPEC.md`. Kickoff below.

### Kickoff · tournaments_unity_wiring (issued 2026-08-14)

```
Read Docs/Specs/Active/tournaments_unity_wiring/SPEC.md and implement it.

Context:
- Phases 1+2 shipped: the tournament schedule, prize bands and per-tournament
  artwork now live in Supabase and have an admin panel. The game still reads
  Assets/Resources/Data/tournaments.csv, so nothing the dashboard does reaches
  players. This phase wires the client to the server.
- Decision of record (Cesar, 2026-08-14): a tournament's NAME and ARTWORK are
  independent of the course it is played on — they can be brand-led. So remote
  art ships in this phase, not later, and the display name must fall back
  localize(name_key) -> title -> slug, because a dashboard-created tournament
  has no localization key in the build.
- The single client seam is TournamentService.Compose()
  (Assets/Scripts/TournamentsRuntime/TournamentService.cs:145) — definitions are
  injected into LocalTournamentBackend, so only where they come from changes.
  ITournamentBackend, LocalTournamentBackend and DeriveState keep working
  unchanged.
- DO NOT add Golfin.Net to Golfin.Tournaments.asmdef. The fetch, the JSON
  mapping and the art service go in Assets/Scripts/TournamentsRuntime/, which
  compiles into Assembly-CSharp and already sees Golfin.Net and Golfin.Economy
  (both autoReferenced). The tournaments core keeps taking plain DTOs.
- Reuse, do not rebuild: ApiClient (Assets/Scripts/Net/ApiClient.cs:67 Get<T>,
  401-refresh-and-replay already handled), TournamentCsvLoader.ExpandHoleSet
  (:250) and CheckReferentialIntegrity (:341), and the atomic file-write
  pattern in Economy/PendingOpsStore.cs:57-68.
- Delete the positional _courseImages[csvIndex] fallback
  (TournamentSelectionScreenController.cs:31, :333-334) and fix the
  null-shadowing bug at :330 where a map entry with a null Sprite blanks the
  card instead of falling through.
- The server half is in /Users/cesar/Documents/playlife: add
  GET /api/v1/tournaments/golfin (no auth, kind='golfin', prize bands joined in
  one payload), and add a kind filter to /tournaments/active (tournaments.py:61)
  and auto_enter_score's two selects (:239, :248).
- Minimal diff. Out of scope: entries, per-hole submission, leaderboards,
  server-side bot generation, the prize resolver (Phases 4-5), sponsor logo
  images, and any new playable course.

When done: list changed files with a 1-line summary each, run the acceptance
tests in the spec, flag which need manual on-device verification, update
STATUS.md + IMPLEMENTER_REPORT.md in the spec folder, and update
Docs/AI_CONTEXT.md.
```

- **`rp_balance_sync`** (filed 2026-08-13, Architect) — **SPEC_READY, from Cesar's find: the nav-bar RP counter doesn't show the backend balance.** The inbound half of the RP cutover was never built — Slice 2 made the game write to the server (earns queue, spends debit server-first) but nothing ever reads back: `PointsService.OnBalanceChanged` has **no subscribers**, `RefreshBalanceAsync`'s only non-test caller is the **editor menu**, and `RewardPointsManager.SetPoints` is flag-OFF-only, so with the flag ON there is no legal path for a server balance to reach the UI. Fix = `ApplyServerBalance` + subscription + refresh on sign-in/resume/Home/after-mutations, with the pending-queue rule (displayed = server + queued earns) so fresh earns don't visibly vanish. Spec: `Docs/Specs/Active/rp_balance_sync/SPEC.md`. Kickoff below.

### Kickoff · rp_balance_sync (issued 2026-08-13)

```
Read Docs/Specs/Active/rp_balance_sync/SPEC.md and implement it.

Context:
- Slice 2 wired the game to WRITE to the server but never to READ: the nav-bar
  RP counter shows a stale local number while the flag is ON. Verified in the
  code — PointsService.OnBalanceChanged has zero subscribers,
  RefreshBalanceAsync's only non-test caller is Economy/Editor/
  PointsBackendMenu.cs, and RewardPointsManager.SetPoints is flag-OFF-only, so
  no server balance can legally reach the UI today.
- Fix per spec section 3: add RewardPointsManager.ApplyServerBalance(int) (NOT
  gated by AllowLocalOverride — the server is not a local override), subscribe
  it to PointsService.OnBalanceChanged without creating an asmdef cycle
  (EconomyRuntime bridge like PointsSpendGate, or the tournament-adapter seam
  pattern), and refresh after sign-in / on app resume / on entering Home /
  after every successful earn+spend.
- Section 3.4 is the subtle one: displayed = server balance + pending queued
  earns, or a fresh earn visibly vanishes until the queue flushes. Don't skip.
- Section 3.5: never render 0 for "unknown" — HasBalance distinguishes them.
- No auth sign-in event was found in AuthService.cs during diagnosis; find the
  real hook or add a minimal one, and flag what you chose. Don't poll.
- Every RP consumer already listens to OnPointsChanged, so no UI rewrites.
- Out of scope: backend, dashboard, queue retry logic, leaderboard
  accumulators, economy values.

When done: list changed files with a 1-line summary each, run the FULL EditMode
sweep (per-assembly — filtered runs report FailedTests only for the filter),
add the tests in section 5.2, and state clearly which acceptance items need
Cesar's manual pass (5.3 is the real proof: live account
cesar.guarinoni@gmail.com sits at 173 RP right now — the nav bar should read
173, then 198 after a +25 dashboard grant and a foreground, no restart).
Update STATUS.md + IMPLEMENTER_REPORT.md in the spec folder and
Docs/AI_CONTEXT.md.
```


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

- **`admin_dashboard`** (filed 2026-08-12, Architect) — ▶ **HOLD LIFTED 2026-08-13** (`reward_points_backend` is COMPLETED; its residual device checks live in `points_device_checks`). **⚠️ IMPLEMENTER IS THE ARCHITECT / COWORK SESSION, NOT CLAUDE CODE — per Cesar.** **v1 + v2 are ALREADY BUILT** there: scaffold (panel registry, admin login + `ADMIN_EMAILS` allowlist, `admin_audit_log` migration), Users panel **+ mutations**, RP grant/adjust, and the **Points + Audit panels**. What remains is **live verification**, not construction. Source now lives in the repo at `Tools/admin-dashboard/` — **v1 then v2, both imported 2026-08-13** (v1 was a strict subset of v2, so the overlay added 12 files and orphaned none); `node_modules` deliberately not committed and `npm install` not run inside the repo. **✅ Both pre-live flags CLEARED 2026-08-13 (Architect):** `migrations/2026_08_13_admin_audit_log.sql` is APPLIED to prod (table + RLS verified, `authenticated` SELECT false) and the `earn_pts_v2` / `spend_pts` signatures are VERIFIED live against `pg_get_function_arguments` — they match `lib/mutations.ts`; the stale README caveat is gone. **Only remaining step: Cesar fills `.env.local` (4 Supabase values), `npm install && npm run dev`.** Web app over the shared Supabase project (`wmszyghwwkaptgqdunel`); NOT Unity — no `Assets/` edits. service_role key is server-side only; Cesar pastes secrets into `.env.local` himself. Spec: `Docs/Specs/Active/admin_dashboard/SPEC.md`.

### ~~Kickoff · admin_dashboard~~ — ⛔ SUPERSEDED 2026-08-13, DO NOT PASTE

> This block told Claude Code to scaffold §5 steps 1–2 from nothing. That work is **done** (and
> then some — v2 added mutations, RP grant/adjust, and the Points + Audit panels), and it was
> done in the **Architect / Cowork session**, which per Cesar remains the implementer for this
> task. Pasting this into Claude Code would re-scaffold an app that already exists. Kept below
> only as the historical record of the original scope.

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

---

## 2026-08-17 · `game_banners` — CODE COMPLETE, blocked on one SQL paste

All three halves built and locally verified: `public.game_banners` migration, `GET /api/v1/banners`
on playlife-api, the Banners panel in `Tools/admin-dashboard`, and the Unity half
(`Assets/Scripts/BannersRuntime/`, no asmdef → Assembly-CSharp). The bundled sprites stay in the
build and remain the fallback on every failure path.

**⛔ Cesar's one step:** paste `playlife/backend/migrations/2026_08_17_game_banners.sql` into the
Supabase SQL editor. `public.game_banners` does not exist yet (probed over PostgREST with the
service key → `PGRST205`), so **neither `fly deploy` nor `npm run deploy` has been run** —
deploying code that reads a missing column 500s the endpoint (`ADMIN_DASHBOARD_OPS.md` §3.2, which
now carries the four-step finish-it runbook).

**⚠️ Also needs Cesar:** confirm the client-side link-host allowlist (SPEC §5.2) — it ships in the
build, so a campaign page on a non-`golfin.io` host needs a client release.

Spec + report: `Docs/Specs/Active/game_banners/`. STATUS = `IMPLEMENTER_BLOCKED`.
EditMode `Golfin.Tournaments.WireupTests`: 115 passed / 0 failed, every pre-existing
`TournamentArtPolicy`/`TournamentArtService` test unmodified.

---

## 2026-08-29 · `store_banner` — DONE (awaiting Cesar's approval)

The Store screen's hard-coded `WinterSaleBanner` is now the fourth `game_banners` placement,
`store` — auto-served, schedulable, and switchable off from the dashboard. Data + wiring only, no
new machinery: DB CHECK (migration pre-applied by Cesar, archived both sides), backend
`PLACEMENTS`, the dashboard's four placement tables, `BannerPlacement.Store` **appended** to the
client enum, and `Button` + `ButtonPressFeedback` + `BannerSlotBinder` on the prefab object that was
already there. `GeneralShopScreenController.cs` untouched; the object keeps its name.

`playlife-api` deployed **v61 → v62**. `Golfin.TournamentsRuntime.Tests` 247/247, tripwire-proven.
Verified in live play mode through the player's own entry point across the full
nothing-live → activate → deactivate round trip; smoke row + art removed from prod afterwards.

⚠️ `BannerPlacement` is serialized as an int in prefabs — `Store` is ordinal 2 because it was
APPENDED. Never reorder that enum.

Dashboard deployed too — `golfin-admin` version `1c1d5564-dd98-4e6d-815a-bfd48b5972a7`, Banners
panel browser-verified (editor dropdown reads `Store — banner`; the list renders a Store group at
978×252 with Window and Sort columns).

⚠️ **Follow-up, deliberately not fixed here:** the Banners panel explainer still promises the pre-A1
behaviour ("nothing here can make a slot go blank"). Audited as a SHAPE, not patched in place —
~20 stale `bundled`/`fallback` sites across the dashboard, the backend and `BannersRuntime`, each
with a verdict in the report's § *A1 stale-copy shape audit*. All of it predates `store`.

**Approved by Cesar 2026-08-29.** Spec + report: `Docs/Specs/Completed/store_banner/`. STATUS = `DONE`.
