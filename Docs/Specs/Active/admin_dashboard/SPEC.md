# SPEC — admin_dashboard (GOLFIN Admin Dashboard)

**Status:** SPEC_READY (plan approved in concept by Cesar 2026-08-12; build not started)
**Author:** Claude (Cowork session, auth-integration epic) with Cesar
**Decision of record:** web app living in this repo, talking to Supabase, expandable panel by panel (Cesar, 2026-08-11).
**Companion docs:** `Docs/GPS/GPS_INTEGRATION_REFERENCE.md` (backend/API/DB truth), `Docs/GPS/2026_08_11_sync_display_name_trigger.sql` (applied to prod), `Docs/GPS/GPS_UNITY_PORT_SPEC.md` §2 (points-ledger decision).

---

## 1. Vision

One internal web app where Wonderwall admins can see **every user** and everything the
game knows about them — account, username, Reward Points, club inventory, characters,
activities, purchases — and **edit all of it**, with every admin action audit-logged.
Starts small (auth data only, which is all the server owns today) and grows a panel at
a time as game systems move server-side.

## 2. The hard constraint the whole plan hangs on

**The dashboard can only show/edit what the SERVER owns.** Today the split is:

| Data | Where it lives today | Dashboard-ready? |
|---|---|---|
| Users, emails, providers (Email/Google/Apple) | Supabase `auth.users` | ✅ now |
| Usernames | `auth.users` metadata + `public.profiles.display_name` (sync trigger applied 2026-08-11) | ✅ now |
| PLAYLIFE points (`activity_pts`/`gift_pts`), trust, badges, activities, gifts, IAP, tournaments | Supabase tables (see GPS_INTEGRATION_REFERENCE §5) | ✅ now (read), but the GAME doesn't write them yet |
| **GOLFIN Reward Points** (`RewardPointsManager`) | **client-only** (local save) | ❌ needs sync |
| **Club inventory / bags / balls** | **client-only** (CSV + local save) | ❌ needs sync |
| **Characters / roster** | **client-only** | ❌ needs sync |
| Stamina, gacha history, hole progress | **client-only** | ❌ needs sync |

So the roadmap is two interleaved tracks:

- **Track A — Dashboard app**: build the shell + panels over whatever is server-side.
- **Track B — Game-state sync**: move game data into Supabase (new tables written by
  the game via authenticated client, RLS: user writes own row) so Track A can grow.
  Track B is ALSO the "cloud save" feature players want — one effort, two payoffs.
  NOTE: the §2 points-ledger decision (RewardPointsManager becomes a client of
  `/points/*` — one shared ledger) should be honored here; don't invent a second
  points table if the PLAYLIFE ledger is the destiny.

## 3. Architecture (decided)

```
GolfinRedux/Tools/admin-dashboard/       ← Next.js (App Router) + TypeScript + Tailwind
├─ app/(panels)/users/…                  ← v1
├─ app/(panels)/<future>/…               ← one folder per panel
├─ lib/registry.ts                       ← panel registry: {id,title,icon,route} — sidebar builds itself
├─ lib/supabaseAdmin.ts                  ← service_role client, SERVER-SIDE ONLY (route handlers/server actions)
├─ lib/audit.ts                          ← writeAudit(admin, action, target, before, after)
└─ .env.local                            ← SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY, ADMIN_EMAILS (never committed)
```

- **Why Next.js**: the service_role key must never reach a browser; Next gives a server
  boundary in one deployable. Runs local-first (`npm run dev`); deploy later to Fly.io
  (next to playlife-api) or Vercel when Ken needs access.
- **Admin auth (v1)**: Supabase email/password login + server-side `ADMIN_EMAILS`
  allowlist (Cesar + Ken). Roles table later if the team grows.
- **Audit log (day one)**: new table `public.admin_audit_log`
  `(id, at, admin_email, action, target_user, table_name, before jsonb, after jsonb)`.
  Every mutation goes through it. Non-negotiable: this DB also serves live PLAYLIFE users.
- **Edit guardrails**: destructive actions double-confirm; deletes cascade per FK —
  surface what will be deleted before confirming.

## 4. Panel roadmap

### v1 — Users panel (buildable TODAY, no dependencies)
- List: `auth.admin.listUsers` ⋈ `profiles` — email, username (profiles.display_name),
  provider icons (Email/Google/Apple), confirmed?, created, last sign-in, trust_level,
  total_points. Search, filters (provider / unconfirmed / banned), pagination.
- Detail drawer: full profiles row (points split, avatar level/XP, counters, invite
  codes), auth identities, recent `activities` + `points_transactions` (read-only).
- Actions: edit username (writes profiles + user_metadata, mirrors the sync trigger),
  resend confirmation, send password-reset, manually confirm email, ban/unban
  (`banned_until`), delete user (double-confirm).
- Header stat cards: total users, new 7d, confirmed %, by-provider breakdown.
- Current test population (2026-08-12): 5 users — Apple Reviewer, Cratilo (email),
  WWtest (Google), Apple (Apple), ken.

### v1.5 — PLAYLIFE data panels (server data exists; game doesn't write it yet)
- **Points ledger**: `points_transactions` viewer + manual earn/adjust with reason
  (reason goes to audit log). Becomes THE Reward Points panel once §2 unification lands.
- **Activities / GPS trust**: `activities` incl. gps_* anti-cheat fields; flag/void.

### v2 — Game-state panels (GATED ON TRACK B sync tables)
- **Characters/roster**: owned characters, levels; grant/revoke character.
- **Club inventory**: clubs/bags/balls owned + equipped; grant/revoke item.
- **Progress/stamina**: hole progress, stamina balance; adjust.
- Suggested sync-table shape: `public.player_state(user_id pk/fk, characters jsonb,
  clubs jsonb, wallet jsonb, progress jsonb, updated_at, client_rev)` written by the
  game on meaningful change; start jsonb-loose, normalize columns when panels need
  querying/editing granularity. RLS: `auth.uid() = user_id` for writes; dashboard
  bypasses via service_role.

### v3 — Ops panels
- **Moderation** (`reports`, `user_blocks`), **Tournaments** (entries/prizes),
  **IAP** (`iap_purchases` — refund/grant), **Gacha config** if it moves server-side.

### Suggestions accepted-for-consideration (Cesar asked for ideas)
- Impersonation-free "player view": render a read-only mock of what that player's
  Home screen would show (name, points, roster) — great for support tickets.
- CSV export on any table view (support + finance asks come fast).
- Broadcast tools later (send announcement/compensation grants to all users) — pairs
  naturally with the grant actions once game-state sync exists.
- Env separation flag: the GPS eval notes dev/stage/prod all share one backend — the
  dashboard should show a loud PRODUCTION banner until env separation exists.

## 5. Build order (when picked up)

1. Scaffold `Tools/admin-dashboard` (Next.js, TS, Tailwind), panel registry, admin
   login + allowlist, `admin_audit_log` table + migration file in `Docs/GPS/`.
2. Users panel list + detail (read-only) → verify against the 5 test users.
3. Users panel mutations (username edit, confirm, ban, delete) with audit + confirm UX.
4. Stat cards; then v1.5 points ledger (read-only first).
5. Track B design session: player_state schema + Unity `PlayerStateSync` service
   (piggybacks on AuthService session + the future ApiClient).
6. Deploy decision (Fly.io next to playlife-api vs Vercel) when Ken needs it.

Estimates: step 1–2 ≈ 1 session; 3–4 ≈ 1 session; Track B is its own spec (write
`player_state_sync` SPEC when starting).

## 6. Open questions (answer at build time)

1. Hosting + who besides Cesar/Ken gets access (drives auth complexity).
2. Does §2 points unification happen before or after the dashboard's points panel?
   (Before = one ledger to display; after = temporary "two wallets" view.)
3. JP/EN for the dashboard UI? (Internal tool — propose EN-only until Ken needs JP.)
4. The Supabase project is on the FREE tier and auto-pauses after ~1 week idle —
   upgrade before the dashboard is relied on for daily ops (or before real players).

## 7. Session context for the next Claude session

Auth epic completed 2026-08-11/12: email/password + Google + Apple sign-in all verified
on-device (see commits 2ffe0403f, 122842b8c, 847d7bced and Docs/GPS/*). Cesar has admin
on Supabase (project wmszyghwwkaptgqdunel "playlife"), Google Cloud (GOLFIN project
golfin-505209), and Apple Developer (Next Innovation, team TCUV4A9VTJ). The
service_role key is obtainable from the Supabase dashboard (Cesar pastes secrets
himself — keep that pattern). Apple client-secret renewal reminder is scheduled for
2027-01-26. Start at §5 step 1.
