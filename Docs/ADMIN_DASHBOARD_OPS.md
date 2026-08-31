# GOLFIN Admin Dashboard — operations

**Read this before touching the admin dashboard in a new session.** It is the
thing that stops you rediscovering, painfully, what already went wrong once.

Live: **https://admin.golfin.world** · Source: `Tools/admin-dashboard` (this repo)
Full detail lives in that folder's `README.md`; this file is the operator's view.

---

## 1. What it is

Next.js 15 (App Router) + TypeScript + Tailwind, deployed to **Cloudflare
Workers** via the OpenNext adapter, reading the PLAYLIFE Supabase project
directly with a `service_role` key. Panels — Audit Log, Banners, Characters,
Clubs, Daily Missions, Gacha Banners, Gacha Pools, Items, Level Costs, Mission
Components, Missions, Modes, Notices, Points, Rewards, Shop, Telemetry, Texts,
Ticket Types, Tournaments, Users — registered in `lib/registry.ts`. The
sidebar renders them **sorted by their translated title**, so the order follows
whichever language is showing and the array order in the registry is not
load-bearing.

| Thing | Value |
|---|---|
| Worker name | `golfin-admin` |
| Cloudflare account | Next.innovation.komatsu@gmail.com's — `c2c4b9869449639abcc77e5437c28dab` |
| Why that account | `golfin.world` is a zone in it; a Worker can only take a custom domain from a zone in its **own** account. Pinned as `account_id` in `wrangler.jsonc`. |
| `workers.dev` | **Disabled on purpose.** A second unprotected hostname serving the same app would sit outside the Access policy. |
| Access team domain | `late-cake-f2a4.cloudflareaccess.com` (auto-generated; renameable in Zero Trust settings) |
| Access app / policy | "GOLFIN Admin" → `admin.golfin.world` → policy "Admins" (three emails — see §3.5), 24h session |
| Supabase project | `wmszyghwwkaptgqdunel` |
| Local dev | `npm run dev` → http://localhost:3000 |
| Local env file | `.env.development.local` — **not** `.env.local`, see §4.4 |

**Two independent gates:** Cloudflare Access at the edge, then the app's own
Supabase login + `ADMIN_EMAILS` allowlist. Behind both sits a `service_role` key
with unrestricted write access to production. Treat any change that weakens
either gate as a production security change.

---

## 2. The normal loop

```
cd Tools/admin-dashboard
# 1. stop the dev server first — see §4.1, this has taken the dashboard down
npm run dev          # local work on :3000
npm run deploy       # build + ship to admin.golfin.world
```

`npm run deploy` runs `scripts/cf-deploy.sh`, which is not a thin wrapper — read
§4.4 and §4.5 before changing it.

**Verify after every deploy** (Access makes a browser check awkward, so check
from the shell):

```
curl -s -o /dev/null -w "%{http_code}\n" https://admin.golfin.world/   # expect 302 → cloudflareaccess.com
```

A **200** there means Access is not protecting it — stop and investigate.

---

## 3. Changing things

### 2.x The deploy runs the tests

`npm run deploy` runs `npm test` **before** the build and aborts on failure —
seconds wasted rather than a full opennextjs build. `SKIP_TESTS=1 npm run deploy`
disarms it loudly, the same posture as `CIBuild`'s `-skipTreeBakeCheck`: it exists
because "I cannot ship a hotfix, an unrelated test is flaky" is a real problem,
and a gate with no escape hatch is a gate somebody deletes. Using it is a
decision on the record, not a default.

The suite (`Tools/admin-dashboard/lib/__tests__/`, vitest) covers the PURE
modules only — `contentValidate` (which stops a bad publish), the Rewards number
guards, and the `golfin_mode_fees` row mapping. It deliberately does not touch
the React tree or the Supabase-backed mutations: those need a DOM or a database,
and a suite that needs either is a suite that rots.

⚠️ Two of the three files RESTATE private `server-only` logic rather than
importing it, and say so in their own docstrings. They pin the rules, not the
implementation — if `checkNumber` or `mirrorModeFees` changes, change the test
copy in the same commit or the suite is quietly lying.

### 3.0 Two kinds of panel, and they are not interchangeable

**Content panels** (Characters, Clubs, Items, Level Costs, Modes, Shop, Texts)
edit `content_drafts` and take effect on **Publish**, then at the player's next
launch. They are all the shared `CatalogPanel` plus a descriptor in
`lib/contentView.ts`; their rules live in `lib/contentValidate.ts`.

Two of them mirror into a server table AS PART OF THE PUBLISH REQUEST, and the
publish FAILS if the mirror write fails (`lib/contentMutations.ts`):

* **Characters** → `golfin_characters` (tournament rarity gates)
* **Modes** → `golfin_mode_fees` (`/points/spend` prices a mode entry from it)

⚠️ **A ROLLBACK MOVES THE MIRROR TOO.** A rollback is a publish carrying old
content — it produces a new, client-visible version — so `rollbackCatalog`
re-mirrors from the rolled-to snapshot and aborts if that write fails, exactly as
publish does. It did not until 2026-08-28: rolling back a bad `modes` fee publish
restored the card to the old price while `golfin_mode_fees` kept the new one, so
every player was answered `fee_changed` at the fee the operator had just undone.
Anything added later that changes what a catalog SERVES must go through
`mirrorForCatalog` (`lib/contentMutations.ts`); `MIRRORED_CATALOGS` is the list.

The per-catalog **kill switch** deliberately does NOT touch the mirror — see the
`setCatalogEnabled` comment for the three options and why leaving it is the only
one that is safe in both directions. The residual is bounded by the `fee_changed`
UX: the player is always shown the price before it is charged.

**Live panels** (Banners, Notices, Points, Rewards, Tournaments, Users) write the
table the server reads per request. There is no draft, no publish and no version
— a save is in effect on the next request.

**Rewards is the one to be careful with.** It edits `game_point_actions`, i.e.
what every earn PAYS, and it is live on save. The panel says so in a banner at
the top and again in the editor; do not soften that copy. Its `pts` column being
BLANK is a mode, not a missing value — blank means the client supplies the amount
bounded by the caps, which is how hole scores and tournament prizes work. Typing
a number into a blank `pts` silently converts a variable payout into a flat one.
Actions cannot be created or deleted from the panel (a shipped client refers to
them by name); `lib/rewardsMutations.ts` enforces that server-side, not just by
omitting the buttons.

### 3.1 A new panel
Add `app/(panels)/<id>/page.tsx` + a client component, an entry in
`lib/registry.ts`, and API routes under `app/api/`. Every route handler starts
with `checkAdmin()`; every mutation goes through `lib/audit.ts` with
before/after snapshots. Follow the Tournaments panel — it is the most complete.

For a **read-only** panel, follow **Telemetry** instead: same page.tsx → client
component → `app/api/<id>/*` shape, but no editor, no mutation route, and no
`lib/audit.ts` (there is nothing to audit). It is also the worked example of
aggregating in TypeScript behind a row cap, of a deterministic mock fixture you
can build a whole panel against before any real rows exist, and of tolerating a
table that is not there yet instead of 500ing — see
`Docs/Specs/Completed/telemetry_admin_panel/`.

### 3.2 A new DB column
**Migration first, deploy second. Always.** Deploying code that references a
column that does not exist yet 500s the endpoint.

You cannot run DDL yourself: Supabase's REST API has no DDL path and there is no
Postgres connection string on the machine. Write the migration into
`playlife/backend/migrations/`, hand Cesar the SQL to paste into the Supabase SQL
editor, and **verify it landed before deploying**:

```
curl -s "$SUPABASE_URL/rest/v1/tournaments?limit=1&select=*" -H "apikey: $KEY" -H "Authorization: Bearer $KEY"
```

Dump the column list from that and check by name. When a migration seems to have
half-landed, this is the diagnostic that settles it — it distinguishes "the
statement never ran" from a stale PostgREST schema cache.

### 3.3 Secrets
Five, all runtime: `SUPABASE_SERVICE_ROLE_KEY`, `SUPABASE_URL`,
`NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY`, `ADMIN_EMAILS`.

```
npx wrangler secret bulk .env.development.local   # all five at once
npx wrangler secret list
```

The Worker must exist before secrets can be set, so a first-ever deploy runs
without them.

### 3.4 Adding UI text — it must be bilingual
The dashboard ships EN + JA behind a switcher at the top right (added
2026-08-18). No i18n library: `lib/i18n.ts` holds one flat `DICT` of
`{ en, ja }` entries, `translate()` does `{var}` interpolation, and the chosen
language lives in the **cookie** `golfin_admin_lang` so the *server* renders the
right language on first paint — localStorage would flash English then flip.

- Client components: `const t = useT()` from `components/I18nProvider`, then
  `t("key")` / `t("key", { count: 3 })`.
- Server components: read the cookie yourself — `app/not-admin/page.tsx` is the
  three-line pattern.
- Adding a string means adding a `DICT` entry with **both** languages; `DictKey`
  is derived from `DICT`, so a missing key is a type error, not a runtime blank.
- `t` is a short name and gets shadowed easily — `rows.map((t) => …)` has bitten
  this file twice. Name row params `row`.
- Japanese is longer in some places and unbreakable in others: badges and table
  headers need `whitespace-nowrap`, and drop `tracking-wider` on JA badges.
- The switcher is `z-30` on purpose — drawers and editors are `z-40` and must
  cover it.

Untranslated by design: Unity object paths, bucket names, slugs, DB column
names, `<title>` metadata, and the LIVE / SCHEDULED / OFF state badges.

### 3.5 Adding an admin — THREE places
1. **`ADMIN_EMAILS`** (Worker secret). Edit `.env.development.local`, then
   `npx wrangler secret bulk .env.development.local` — it pushes all five and
   takes effect immediately, no redeploy.
2. **The Cloudflare Access policy** "Admins" (Zero Trust → Access controls →
   Policies → Admins → Configure). Type the address and press **Enter** so it
   becomes its own chip; reload the edit page afterwards and count the chips.
   Two addresses once merged into one string here and it was only caught on a
   screenshot. Dashboard work only — the wrangler OAuth token can *read*
   `/access/apps` but `POST` returns `auth.forbidden`.
3. **A Supabase account with a PASSWORD.** The dashboard's own login is
   Supabase email/password; Google sign-in is not wired into it, so someone
   whose account is Google-only cannot sign in even with 1 and 2 done. Check
   before assuming — the live answer is one call:

   ```
   curl -s "$SUPABASE_URL/auth/v1/admin/users?per_page=200" \
     -H "apikey: $KEY" -H "Authorization: Bearer $KEY" \
     | python3 -c "import json,sys; [print(u['email'], u['app_metadata'].get('providers')) for u in json.load(sys.stdin)['users']]"
   ```

   `providers: ['email', ...]` means they already have a password. If it is
   Google-only, set one in Supabase → Authentication → Users → the user →
   Reset password, and send it out of band.

   ⚠️ **Do not read identity facts off the dashboard in MOCK mode.** The Users
   panel and drawer render `lib/mock.ts` fixtures there, provider badges
   included, and they are invented. This produced a confidently wrong claim
   about Ken's account on 2026-08-18 — the fixture said Google, production says
   `email` + `google`. Mock mode is for exercising the UI, never for answering
   a question about a real user.

They fail differently, which is how you tell which one you missed: no policy →
Cloudflare block page; no `ADMIN_EMAILS` → they clear Access and land on
`/not-admin`; no password → the login form rejects them at the dashboard's own
sign-in.

Current admins (2026-08-18): `cesar.guarinoni@wonderwall-g.com`,
`cesar.guarinoni@gmail.com`, `greedisland.k.k@gmail.com` (Ken — verified
`providers: ['email','google']`, so he signs in with his existing password;
nothing to set).

**No role tiers.** The allowlist is all-or-nothing: every admin can adjust RP,
ban, and delete users. Every action is attributed by email in
`admin_audit_log`, but nothing is *prevented*. A read-only role would be a real
feature, not a config switch.

---

## 4. Traps — each of these has already cost time

### 4.1 Never `next build` while `next dev` is running
They share `.next/`. A build against a live dev server leaves it serving HTML
that references chunks the build deleted: every `/_next/static/chunks/*.js` and
the stylesheet return **404**, so the page loads unstyled and never hydrates.
**The server log stays clean**, which is what makes it read as "the app is
broken". Recovery: stop the server, `rm -rf .next`, restart.

### 4.2 `NODE_ENV=production` leaks into tooling shells
The Desktop Commander shell inherits `NODE_ENV=production` (this is the tooling,
not Cesar's profile). Two consequences:

- `npm install` **prunes all devDependencies** — typescript, tailwind, wrangler.
  Symptom: the build fails with `Can't resolve '@/lib/...'` for every path
  alias, because TypeScript is simply gone. Fix:
  `NODE_ENV=development npm install --include=dev`.
- `next dev` compiles middleware in a mode the Edge sandbox rejects: every page
  500s with `EvalError: Code generation from strings disallowed`. The startup
  banner warns about a "non-standard NODE_ENV" one line earlier. Start with
  `NODE_ENV=development npm run dev`.

### 4.3 The service key must not enter the bundle
Next loads env files at build time and OpenNext writes what it finds into
`.open-next/cloudflare/next-env.mjs`, which is uploaded with the Worker.
`cf-deploy.sh` moves the env file aside for the build, greps the output for the
key and **aborts** if it finds it, then restores the file on any exit path.

### 4.4 …but `NEXT_PUBLIC_*` must
They are *inlined into the client bundle at compile time*, so a Worker secret
cannot supply them — the browser code is already built. Hiding the env file
takes them with it, and the app then deploys fine, passes Access, and dies on
`NEXT_PUBLIC_SUPABASE_URL / NEXT_PUBLIC_SUPABASE_ANON_KEY are required in live
mode`. `cf-deploy.sh` extracts only the `NEXT_PUBLIC_*` lines and passes those
to the build. This is why the local env file is `.env.development.local` (loaded
only by `next dev`) rather than `.env.local` (loaded by every build).

### 4.5 The missing-key guard fails closed, everywhere
`lib/mode.ts` **throws** if `SUPABASE_SERVICE_ROLE_KEY` is absent rather than
falling back to mock mode — mock mode's login accepts **any password**. Mock
mode is opt-in via `MOCK_MODE=1` only. The one exception is
`NEXT_PHASE=phase-production-build`, since the build deliberately runs with no
secrets and Next still prerenders `/_not-found`.

An earlier version only threw when `NODE_ENV === "production"`. That was wrong:
`NODE_ENV` is not reliably set on Workers, so it would not have fired on a real
deploy. Do not reintroduce environment-name inference.

### 4.6 Shell gotchas on this Mac
- `pkill -f "next dev"` from a tooling shell **matches and kills its own shell**.
  Use `pgrep -f "[n]ext dev"` and `kill <pid>`.
- macOS has no `setsid`. Background with `( … & )` + `nohup`.
- `tar --overwrite` is unsupported; plain `tar -xzf` overwrites anyway.
- `fly deploy` runs longer than the 60s tool timeout. Launch it with `nohup … &`,
  sleep, then poll the log — it completes even when the tool call times out.

---

## 5. Backend half

The dashboard reads Supabase directly, but the **game** reads
`playlife-api.fly.dev`. Anything the game must see (e.g. the tournament schedule)
needs the FastAPI change too:

```
cd /Users/cesar/Documents/playlife/backend
export PATH="$HOME/.fly/bin:$PATH"
fly deploy                      # app playlife-api, region nrt
```

Routes live in `backend/routers/`, mounted under `/api/v1`. Envelope is
`{"data": …}` written by hand per route; errors are FastAPI `{"detail": …}`.
`GET /notices` (`routers/notices.py`) is the Home notice panel's copy — every
live `home_notices` row, ordered, EN+JA, with `expires_at` echoed so a cached
notice dies on time offline. An empty list is normal and means "hide the panel".

`GET /tournaments/golfin` is the game's schedule endpoint — it filters
`kind='golfin'` and `is_active=true`, and must stay declared **above**
`GET /{tournament_id}` or that route swallows the literal path.

---

## 6. Still open

- **`service_role` key rotation.** The key passed through a chat log once.
  Rotating means updating three places together: the Cloudflare secret, the Fly
  secret on `playlife-api`, and `.env.development.local`. Miss the Fly one and
  the game's `/points/*` breaks.
- ~~**Supabase redirect URL.**~~ DONE 2026-08-18 — `https://admin.golfin.world`
  and `https://admin.golfin.world/**` are both in Authentication → URL
  Configuration (4 entries total). The bare host covers the root callback, the
  `/**` form covers password-reset links that land on a path. **Site URL was
  also flipped to `https://admin.golfin.world`** (was `https://playlife-app.web.app/`;
  Cesar, 2026-08-18: the GPS app is deprecated).

  ⚠️ **Knock-on, still open.** Site URL is the fallback for any auth mail sent
  with no explicit `redirect_to`, and the GAME sends two of those:
  `/auth/v1/signup` (confirmation) and `/auth/v1/recover` (reset) — see
  `Assets/Scripts/Auth/ISupabaseAuthClient.cs`. Only the OAuth path passes one
  (`OAuthUrlBuilder` → `golfin://auth-callback`). So a player clicking a
  confirmation or reset link now lands on `admin.golfin.world` and hits the
  **Cloudflare Access block page**. Before the beta, the client should pass
  `redirect_to=golfin://auth-callback` on both calls; that deep link is already
  in the allow list. Until then, treat player email confirmation as broken on
  the redirect leg (the account is still confirmed — the landing page is what
  fails).
- **`/tournaments/admin/create` and `/admin/weekly-open`** are still guarded only
  by a non-constant-time comparison against a static key, and `admin_create`
  never sets `kind`. Close them before tournaments carry anything a player would
  miss.
