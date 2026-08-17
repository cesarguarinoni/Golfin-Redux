# GOLFIN Admin Dashboard — operations

**Read this before touching the admin dashboard in a new session.** It is the
thing that stops you rediscovering, painfully, what already went wrong once.

Live: **https://admin.golfin.world** · Source: `Tools/admin-dashboard` (this repo)
Full detail lives in that folder's `README.md`; this file is the operator's view.

---

## 1. What it is

Next.js 15 (App Router) + TypeScript + Tailwind, deployed to **Cloudflare
Workers** via the OpenNext adapter, reading the PLAYLIFE Supabase project
directly with a `service_role` key. Four panels — Users, Points, Tournaments,
Audit Log — registered in `lib/registry.ts`.

| Thing | Value |
|---|---|
| Worker name | `golfin-admin` |
| Cloudflare account | Next.innovation.komatsu@gmail.com's — `c2c4b9869449639abcc77e5437c28dab` |
| Why that account | `golfin.world` is a zone in it; a Worker can only take a custom domain from a zone in its **own** account. Pinned as `account_id` in `wrangler.jsonc`. |
| `workers.dev` | **Disabled on purpose.** A second unprotected hostname serving the same app would sit outside the Access policy. |
| Access team domain | `late-cake-f2a4.cloudflareaccess.com` (auto-generated; renameable in Zero Trust settings) |
| Access app / policy | "GOLFIN Admin" → `admin.golfin.world` → policy "Admins" (two emails), 24h session |
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

### 3.1 A new panel
Add `app/(panels)/<id>/page.tsx` + a client component, an entry in
`lib/registry.ts`, and API routes under `app/api/`. Every route handler starts
with `checkAdmin()`; every mutation goes through `lib/audit.ts` with
before/after snapshots. Follow the Tournaments panel — it is the most complete.

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

### 3.4 Adding an admin — two places
`ADMIN_EMAILS` (Worker secret) **and** the Cloudflare Access policy. They fail
differently: miss the policy and the person gets a Cloudflare block page; miss
the secret and they clear Access and land on `/not-admin`.

Access policy changes are dashboard work — the wrangler OAuth token can *read*
`/access/apps` but `POST` returns `auth.forbidden`.

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
`GET /tournaments/golfin` is the game's schedule endpoint — it filters
`kind='golfin'` and `is_active=true`, and must stay declared **above**
`GET /{tournament_id}` or that route swallows the literal path.

---

## 6. Still open

- **Banners panel — BUILT, waiting on one SQL paste.** `Docs/Specs/Active/game_banners/`. The
  fifth panel (`/banners`), `GET /api/v1/banners` on playlife-api, and the Unity half are all
  written and locally verified. **Nothing is deployed**, deliberately: `public.game_banners`
  does not exist yet (PostgREST returns `PGRST205`), and per §3.2 code that reads a missing
  column 500s the endpoint. Order to finish it:

  1. Paste `playlife/backend/migrations/2026_08_17_game_banners.sql` (same file is mirrored in
     `Tools/admin-dashboard/migrations/`) into the Supabase SQL editor.
  2. Verify the columns landed — the VERIFICATION block at the bottom of that file, or:
     `curl -s "$SUPABASE_URL/rest/v1/game_banners?limit=1&select=*" -H "apikey: $KEY" -H "Authorization: Bearer $KEY"` → `200 []`.
  3. `cd /Users/cesar/Documents/playlife/backend && export PATH="$HOME/.fly/bin:$PATH" && fly deploy`
     (background it per §4.6), then `curl -s https://playlife-api.fly.dev/api/v1/banners` →
     200 with `{"data":{"fetched_at","banners"}}`. The **bare** path must be 200, not 307.
  4. `npm run deploy` from `Tools/admin-dashboard`, then the §2 check → 302.

  The `game-banners` Storage bucket is created on first upload by `uploadBannerArt`, exactly as
  `tournament-art` is — no manual Supabase step.

- **⚠️ Banner link-host allowlist needs Cesar's confirmation.** `BannerPolicy.AllowedLinkHosts`
  (`Assets/Scripts/BannersRuntime/BannerPolicy.cs`) currently allows `golfin.io`, `www.golfin.io`,
  `golfin.world`, `www.golfin.world` — exact matches, no wildcards. **It ships in the build**, so a
  campaign page on a marketing host, a Notion/Typeform page or a partner domain needs a client
  release; an admin cannot add a host from the dashboard, by design. `ALLOWED_LINK_HOSTS` in
  `lib/banner.ts` must be kept in step: a URL the dashboard accepts but the client refuses is a
  banner that looks fine to the operator and does nothing on the device.

- **`service_role` key rotation.** The key passed through a chat log once.
  Rotating means updating three places together: the Cloudflare secret, the Fly
  secret on `playlife-api`, and `.env.development.local`. Miss the Fly one and
  the game's `/points/*` breaks.
- **Supabase redirect URL.** `https://admin.golfin.world` still needs adding to
  Authentication → URL Configuration, or password-reset links point at
  localhost.
- **`/tournaments/admin/create` and `/admin/weekly-open`** are still guarded only
  by a non-constant-time comparison against a static key, and `admin_create`
  never sets `kind`. Close them before tournaments carry anything a player would
  miss.
