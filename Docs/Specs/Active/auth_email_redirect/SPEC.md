# Spec: Auth email redirect fix — deep link now, hosted page next

**Slug:** `auth_email_redirect` · **Status:** SPEC_READY · **Date:** 2026-08-19
**Problem:** Player signup-confirmation and password-reset emails redirected to the Supabase
Site URL fallback (`admin.golfin.world`) → Cloudflare Access block page. Root cause per
`claude/ADMIN_DASHBOARD_OPS.md` §6: the game client sends `/auth/v1/signup` and
`/auth/v1/recover` with **no `redirect_to`**.

## What is ALREADY DONE (2026-08-19, via Supabase Studio — no code involved)

- **Confirm-signup and Reset-password email templates rewritten**: GOLFIN-branded,
  bilingual EN+JA, and the verify link now hardcodes
  `redirect_to=golfin://auth-callback` via
  `https://wmszyghwwkaptgqdunel.supabase.co/auth/v1/verify?token={{ .TokenHash }}&type=<signup|recovery>&redirect_to=golfin://auth-callback`.
  This fixes builds already in the field. `golfin://auth-callback` is already in the
  Supabase redirect allow list.

## Part 1 — Deploy the hosted landing page (Worker `golfin-confirm`)

Files provided: `golfin-confirm/wrangler.jsonc` + `golfin-confirm/public/index.html`.
Place at `Tools/golfin-confirm/` in the repo.

1. `cd Tools/golfin-confirm && npx wrangler deploy` (same wrangler auth as golfin-admin;
   account pinned in wrangler.jsonc). Creates `confirm.golfin.world` as a custom domain
   on the golfin.world zone.
2. **Do NOT add a Cloudflare Access policy** — the page must be public. Verify:
   `curl -s -o /dev/null -w "%{http_code}\n" https://confirm.golfin.world/` → expect **200**
   (the admin dashboard expects 302; this one is the opposite, on purpose).
3. **Supabase → Authentication → URL Configuration**: add `https://confirm.golfin.world`
   and `https://confirm.golfin.world/**` to the redirect allow list (SQL-free, dashboard
   only — Cesar).
4. **Swap the two email templates' `redirect_to`** from `golfin://auth-callback` to:
   - Confirm signup → `https://confirm.golfin.world/`
   - Reset password → `https://confirm.golfin.world/?type=recovery`
   The page auto-deep-links to `golfin://auth-callback` on mobile (forwarding
   `location.hash`, where the verify redirect puts the tokens) and shows a bilingual
   "use your phone" note on desktop.

## Part 2 — Client passes redirect_to explicitly (Unity)

**File:** `Assets/Scripts/Auth/ISupabaseAuthClient.cs` (and its implementation).

- `/auth/v1/signup` request body: add `"options": {"email_redirect_to": "https://confirm.golfin.world/"}`
  — NOTE: verify the exact wire format the implementation uses; for raw REST it is a
  top-level `email_redirect_to`-style param carried as `redirect_to` query/body depending
  on the client. Mirror how `OAuthUrlBuilder` passes `golfin://auth-callback` today.
- `/auth/v1/recover` body: add `"redirect_to": "https://confirm.golfin.world/?type=recovery"`.
- Until the Worker is deployed, use `golfin://auth-callback` as the value (matches the
  templates' current hardcode); switch to the hosted URL in the same commit as Part 1
  if both land together.

**NOTE (must verify, do not assume):** confirm the game's `golfin://auth-callback`
handler processes a `type=recovery` callback (i.e. shows a set-new-password screen and
calls the password-update endpoint with the recovery session). If it only handles OAuth,
password-reset-by-email remains broken client-side even with correct redirects — file the
gap in the IMPLEMENTER_REPORT rather than silently shipping.

## Out of scope

- Custom SMTP / "from GOLFIN" sender (separate checklist, Cesar-driven, no code).
- Admin-dashboard "reset password / set password" user actions (separate spec, backlog —
  agreed 2026-08-19).

## Acceptance

- [ ] `curl` check on `https://confirm.golfin.world/` returns 200 with the GOLFIN page, no Access interstitial.
- [ ] New in-game signup → confirmation email → link on iPhone confirms the account and lands in the game (or on the confirm page which deep-links in).
- [ ] Same link opened on a desktop browser: account confirmed, friendly bilingual page, no Cloudflare Access screen.
- [ ] Password reset email → link → new password can actually be set (or the handler gap is documented).
- [ ] Supabase URL Configuration contains both confirm.golfin.world entries.
