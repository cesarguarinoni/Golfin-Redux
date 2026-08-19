# Handoff — auth_email_redirect

**Date:** 2026-08-19 · **STATUS:** `IMPLEMENTER_BLOCKED` · **Commit:** `ac9f92a56` (pushed to `main`)
**Companion doc:** `IMPLEMENTER_REPORT.md` (verification evidence) · **Spec:** `SPEC.md`

This is the action doc: what shipped, what you have to do, and what to watch out for.
Everything remaining is Cesar-side by the spec's own design.

---

## TL;DR

| | |
|---|---|
| **Shipped** | Part 2 — the Unity client now sends an explicit `redirect_to` on `/signup`, `/resend`, `/recover`. Compiles clean, URL output verified by execution. |
| **Blocked** | Part 1 — the `golfin-confirm` Worker is staged and its config validates, but `wrangler` is not logged in on this machine and `wrangler login` needs an interactive browser. |
| **Found** | Password reset is broken client-side in a way that is worse than "missing" — a reset link silently **logs the player in** instead of letting them set a new password. Documented, not shipped over. |
| **Watch out** | Do the Supabase allow-list step **before** shipping a build, or the new redirect is silently ignored and you land right back on the Access block page. |

---

## What you need to do

### 1. Deploy the Worker

```bash
cd Tools/golfin-confirm && npx wrangler login && npx wrangler deploy
```

`wrangler.jsonc` already pins the account (`c2c4b98…`) and claims `confirm.golfin.world` as a
custom domain on the golfin.world zone. Nothing in it needs editing — it passed a real
`--dry-run` this session.

### 2. Verify it is public

```bash
curl -s -o /dev/null -w "%{http_code}\n" https://confirm.golfin.world/
```

Expect **200**. This is the opposite of the admin dashboard, which correctly returns 302 —
**do not add a Cloudflare Access policy to this Worker.** It is where player confirmation
emails land; gating it recreates the exact bug this task fixes.

### 3. Supabase → Authentication → URL Configuration

Add both entries to the redirect allow list:

- `https://confirm.golfin.world`
- `https://confirm.golfin.world/**`

**This is the step that gates shipping a build — see § The ordering trap.**

### 4. Swap the two email templates' `redirect_to`

In Supabase Studio, change the hardcoded `redirect_to` in the verify links from
`golfin://auth-callback` to:

| Template | New value |
|---|---|
| Confirm signup | `https://confirm.golfin.world/` |
| Reset password | `https://confirm.golfin.world/?type=recovery` |

---

## The ordering trap

**Supabase does not error on a `redirect_to` that isn't allow-listed — it silently ignores it and
falls back to the Site URL.** The Site URL is `admin.golfin.world`, which is behind Cloudflare
Access. So a build that ships before step 3 lands players on the block page again, and it will
look exactly like the fix didn't work.

Two safe orders:

- **Preferred:** do step 3, then ship the build.
- **If a build has to go out first:** set both `SupabaseConfig` fields back to
  `golfin://auth-callback` (already allow-listed). They're serialized fields, so this is an
  Inspector change — no code edit, no recompile.

---

## Password reset is still broken — and it fails badly

The spec asked me to verify this rather than assume it. The answer is worse than the spec
anticipated.

`AuthService.OnDeepLink` (`Assets/Scripts/Auth/AuthService.cs:202`) has **one** branch. It hands
the URL to `OAuthCallbackParser.Parse`, which looks only for `access_token` in the fragment. There
is no `type` / `recovery` handling anywhere in `Assets/Scripts/`.

So today, when a player taps a password-reset link:

1. Supabase verifies the token → redirects to `golfin://auth-callback#access_token=…&type=recovery`
2. `IsCallback` matches on prefix; `type=recovery` is never read
3. The recovery tokens are applied as an ordinary session, saved, and `RaiseSignedIn()` fires
4. **The player is silently signed in and dropped into the game — password unchanged, no
   set-new-password screen ever shown**

It also cannot be patched in UI alone: `ISupabaseAuthClient` has **no password-update method at
all** (`UpdateDisplayName` is the only `PUT /user` caller), and `Assets/Scripts/UI/Account/`
contains just `LoginScreenController`.

**Net effect:** steps 1–4 above fix signup confirmation end-to-end. They do **not** make password
reset work. Acceptance item 4 resolves as "gap documented."

### What a fix needs (its own spec)

- Branch on `type=recovery` in the callback fragment — Supabase already puts it there, so no new
  plumbing — and route to a set-new-password screen instead of raising signed-in.
- Add `UpdatePassword(accessToken, newPassword)` to `ISupabaseAuthClient` → `PUT /auth/v1/user`
  with `{"password": "..."}`, using the recovery session's token.
- New UI screen plus EN/JA localization keys.
- Minor: the landing page forwards only `location.hash`, not `location.search`, so `?type=recovery`
  never reaches the app. The fragment's own `type` is the reliable signal — build on that.

---

## What shipped in `ac9f92a56`

The client sends the redirect as a **query parameter**, which is what GoTrue's raw REST API takes.
The `options.email_redirect_to` shape named in the spec is the supabase-js surface; it lowers onto
this same param. `/resend` was included as well — it sends the identical confirmation email and had
the same defect, which the spec didn't call out.

| File | Change |
|---|---|
| `Assets/Scripts/Auth/AuthRedirectUrl.cs` *(new)* | Pure `Append(path, redirectTo)`; empty redirect → path untouched |
| `Assets/Scripts/Auth/AuthRedirectUrl.cs.meta` *(new)* | Hand-authored (Editor was closed); GUID verified unique |
| `Assets/Scripts/Auth/SupabaseAuthClient.cs` | `redirect_to` on `/signup`, `/resend`, `/recover` |
| `Assets/Scripts/Auth/SupabaseConfig.cs` | `emailConfirmRedirect`, `passwordResetRedirect` |
| `Assets/Resources/SupabaseConfig.asset` | Both values serialized |
| `Assets/Scripts/Auth/Tests/OAuthTests.cs` | `AuthRedirectUrlTests` — 4 tests, 6 assertions |

Verified by compiling `Assets/Scripts/Auth/*.cs` against Unity 6000.3.9f1's real reference set with
Unity's own Roslyn (**zero errors**; only the 17 pre-existing `CS0649` DTO warnings), and by
compiling the helper to an exe and **running** it against the values now in the asset — all six
emitted URLs match the new test assertions.

### Two loose ends for when the Editor is next open

- Unity has not imported `AuthRedirectUrl.cs` yet (Editor was closed all session). Open it once so
  the new file and its hand-written `.meta` are picked up.
- Run `tests-run` on `Golfin.Auth.Tests`. The test assembly wouldn't compile offline — no nunit
  build here lines up with Unity's reference set, and the errors landed on `[Test]` lines in
  pre-existing untouched files too, which is what marks it a harness limit rather than a defect.

---

## Acceptance scorecard

| # | Criterion | Status |
|---|---|---|
| 1 | `curl` on `confirm.golfin.world` → 200, no Access interstitial | **BLOCKED** — HTTP 000 (not deployed); config dry-run passes |
| 2 | Signup → email → link on iPhone confirms + lands in game | **BLOCKED** — needs deploy + device build |
| 3 | Same link on desktop: confirmed, bilingual page, no Access screen | **BLOCKED** — needs deploy |
| 4 | Password reset → new password can be set *(or gap documented)* | **DONE as documented** — the feature itself does not work |
| 5 | Supabase URL Configuration has both entries | **BLOCKED** — dashboard-only |
| — | Part 2: client sends explicit `redirect_to` | **DONE** — verified |

Measured this session: `confirm.golfin.world` → **000** · `admin.golfin.world` → **302** (control,
confirms the check method is sound).
