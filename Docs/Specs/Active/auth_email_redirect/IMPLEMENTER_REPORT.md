# Implementer report — auth_email_redirect

**Date:** 2026-08-19 · **Run by:** Claude Code (direct, non-pipeline — infra/code task, no Figma node)
**Spec:** `Docs/Specs/Active/auth_email_redirect/SPEC.md`

---

## Summary

**Part 2 (Unity client) is COMPLETE and verified.** The game now sends an explicit
`redirect_to` on all three email-sending auth endpoints, so Supabase no longer falls back to
the Site URL (`admin.golfin.world` → Cloudflare Access block page).

**Part 1 (Worker deploy) is BLOCKED on Cesar** — `wrangler` is not authenticated on this
machine and `wrangler login` is an interactive browser OAuth flow. Spec steps 3 and 4
(Supabase dashboard) were already Cesar-owned. The Worker source is in place and its config
**validates against a real `wrangler deploy --dry-run`**.

**One real gap found and NOT silently shipped** — see § Recovery handler gap. It is worse than
"missing": a password-reset link currently *logs the player in* instead of letting them set a
new password.

---

## Part 2 — what changed

| File | Change |
|---|---|
| `Assets/Scripts/Auth/AuthRedirectUrl.cs` **(new)** | Pure static `Append(path, redirectTo)` — appends `redirect_to=<escaped>` with `?` or `&`. Returns the path untouched when the redirect is empty. |
| `Assets/Scripts/Auth/AuthRedirectUrl.cs.meta` **(new)** | Hand-authored (Unity not running). GUID `c0282b84a5032d923ef2c8efca501fc2`, verified unique; same shape as the other 60-byte 2-line metas in the folder (Lesson R). |
| `Assets/Scripts/Auth/SupabaseConfig.cs` | Added `emailConfirmRedirect` + `passwordResetRedirect` (tooltipped, mirrors `oauthRedirect`). |
| `Assets/Scripts/Auth/SupabaseAuthClient.cs` | `/signup` and `/resend` → `WithEmailRedirect(...)`; `/recover` → `AuthRedirectUrl.Append(..., passwordResetRedirect)`. |
| `Assets/Resources/SupabaseConfig.asset` | Serialized both new values. (Live transport is already ON here: `useMockTransport: 0`.) |
| `Assets/Scripts/Auth/Tests/OAuthTests.cs` | Added `AuthRedirectUrlTests` — 4 tests, 6 assertions. |

### Wire format — verified, NOT assumed

The spec flagged this as "verify the exact wire format, do not assume." For the **raw REST API**
that `SupabaseAuthClient` speaks, GoTrue takes the redirect as a **query parameter**, not a body
field: `POST /auth/v1/signup?redirect_to=...`. The `options.email_redirect_to` shape named in the
spec is the **supabase-js** surface, which lowers onto this same query param. `/resend` was
included too — it sends the identical confirmation email and had the same defect.

### Verification (both ran; output copied from the actual runs)

**1. Compile** — `Assets/Scripts/Auth/*.cs` against Unity 6000.3.9f1's real UnityEngine +
netstandard2.1 reference set, using Unity's own Roslyn (`DotNetSdkRoslyn/csc.dll`):

```
EXIT=0   — zero errors.
```

Only 17 `CS0649` warnings, all pre-existing ("never assigned" on the `JsonUtility` DTO fields,
lines 197–245 — untouched by this change).

**2. Executed the helper for real.** It has no Unity dependency, so it was compiled to an exe and
run against the exact values now serialized in `SupabaseConfig.asset`:

```
PASS POST /signup   -> /signup?redirect_to=https%3A%2F%2Fconfirm.golfin.world%2F
PASS POST /resend   -> /resend?redirect_to=https%3A%2F%2Fconfirm.golfin.world%2F
PASS POST /recover  -> /recover?redirect_to=https%3A%2F%2Fconfirm.golfin.world%2F%3Ftype%3Drecovery
PASS query path uses &          -> /verify?type=recovery&redirect_to=golfin%3A%2F%2Fauth-callback
PASS empty redirect = untouched -> /signup
PASS null  redirect = untouched -> /signup

ALL PASS
```

These six strings are exactly the assertions in the new NUnit tests, so the tests will pass when
Unity runs them.

**Not verified — the test assembly would not compile offline.** No nunit build on this machine
lines up with Unity's reference set (the net35 build wants mscorlib 2.0, the net40 build wants
mscorlib 4.0, and UnityEngine.dll wants netstandard 2.1). Every error landed on `[Test]` attribute
lines **including ones in pre-existing files I never touched**, which is what identifies it as a
harness limitation rather than a defect. Unity composes the correct refs from the asmdef. Re-run
`tests-run` on `Golfin.Auth.Tests` once the Editor is open to close this out.

**Also not verified — Unity has not imported the new file.** The Editor was closed the whole
session (`list_engine_instances` returned "No engine instances are connected"). Open the Editor
once so it picks up `AuthRedirectUrl.cs` and confirms the hand-written `.meta`.

---

## Part 1 — Worker: staged, validated, NOT deployed

Files are at `Tools/golfin-confirm/` as the spec directs (`wrangler.jsonc` + `public/index.html`).

**Config is valid** — real dry-run, mutates nothing:

```
$ npx wrangler deploy --dry-run
✨ Read 1 file from the assets directory ...\Tools\golfin-confirm\public
Total Upload: 0.36 KiB / gzip: 0.26 KiB
--dry-run: exiting now.
```

**Blocker — deploy needs Cesar:**

```
$ npx wrangler whoami
You are not authenticated. Please run `wrangler login`.
```

There is no wrangler credential file and no `CLOUDFLARE_API_TOKEN` on this machine, and
`wrangler login` opens an interactive browser consent flow that cannot be completed from a
non-interactive session. I did not attempt any workaround.

**Current live state (acceptance check #1), measured this session:**

```
confirm.golfin.world -> HTTP 000   (not deployed / DNS not resolving)
admin.golfin.world   -> HTTP 302   (control — confirms the check method is sound)
```

### Remaining steps — all Cesar-side

1. `cd Tools/golfin-confirm && npx wrangler login && npx wrangler deploy`
2. Verify `curl -s -o /dev/null -w "%{http_code}" https://confirm.golfin.world/` → **200**
   (not 302 — this page must stay public; **do not** add a Cloudflare Access policy).
3. Supabase → Authentication → URL Configuration: add `https://confirm.golfin.world` and
   `https://confirm.golfin.world/**`.
4. Swap the two email templates' `redirect_to` from `golfin://auth-callback` to
   `https://confirm.golfin.world/` (signup) and `https://confirm.golfin.world/?type=recovery`
   (recovery).

**Ordering note — do step 3 before shipping a build.** Supabase ignores a `redirect_to` that is
not on the allow list, so a build sending `https://confirm.golfin.world/` before it is allow-listed
falls back to the Site URL — i.e. straight back to the Access block page. If a build has to ship
first, set both `SupabaseConfig` fields back to `golfin://auth-callback` (already allow-listed);
the fields exist precisely so this needs no code change.

Note the current templates hardcode the verify URL with their own `redirect_to`, so they override
what the client sends until step 4. The client-side value still matters for correctness-by-default
and for any template that uses `{{ .ConfirmationURL }}`.

---

## Recovery handler gap — the spec's "must verify, do not assume"

**Verdict: the gap is real, and it is worse than a missing feature.**

`AuthService.OnDeepLink` (`Assets/Scripts/Auth/AuthService.cs:202`) has exactly one branch. It
calls `OAuthCallbackParser.Parse`, which looks only for `access_token` in the fragment — there is
**no `type` / `recovery` handling anywhere in `Assets/Scripts/`** (grepped: zero hits outside the
unrelated character-stat `Recovery`).

So when a player taps a password-reset link today:

1. Supabase verifies the token and redirects to `golfin://auth-callback#access_token=…&type=recovery`.
2. `IsCallback` matches on prefix — the `type=recovery` is never read.
3. The recovery tokens are parsed as a normal session: `Session.ApplyFrom` + `Session.Save`, then
   `RaiseSignedIn()`.
4. **The player is silently logged in and dropped into the game. Their password is unchanged, and
   they are never shown a set-new-password screen.**

There is also no way to fix this purely in the UI: `ISupabaseAuthClient` has **no password-update
method at all** (`UpdateDisplayName` is the only `PUT /user` caller), and no reset/new-password
screen exists (`Assets/Scripts/UI/Account/` contains `LoginScreenController` only).

**Consequence:** correcting the redirects fixes signup confirmation end-to-end, but
password-reset-by-email still will not let anyone reset a password. Acceptance item 4 resolves as
"handler gap documented," not "works."

**What a fix needs** (out of scope here — warrants its own spec):

- Detect `type=recovery` in the callback fragment (Supabase puts it there, so no extra plumbing is
  needed) and route to a set-new-password screen instead of raising signed-in.
- Add `UpdatePassword(accessToken, newPassword)` to `ISupabaseAuthClient` — `PUT /auth/v1/user`
  with `{"password": "..."}` using the recovery session's access token.
- New UI screen plus its localization keys.
- Also worth doing: the landing page forwards only `location.hash`, not `location.search`, so the
  `?type=recovery` marker never reaches the app — the fragment's own `type` is the reliable signal.

---

## Acceptance status

| # | Criterion | Status |
|---|---|---|
| 1 | `curl` on `confirm.golfin.world` → 200, no Access interstitial | **BLOCKED** — not deployed (HTTP 000). Worker validated via dry-run. |
| 2 | Signup → email → link on iPhone confirms + lands in game | **BLOCKED** — needs the deploy plus a device build. |
| 3 | Same link on desktop: confirmed, bilingual page, no Access screen | **BLOCKED** — needs the deploy. |
| 4 | Password reset → new password can be set (or gap documented) | **DONE as "gap documented"** — see above. The feature itself does not work. |
| 5 | Supabase URL Configuration has both confirm.golfin.world entries | **BLOCKED** — Cesar, dashboard-only. |
| — | Part 2: client sends explicit `redirect_to` | **DONE** — compiles clean, output verified. |

## Standing-ban check

Zero edits to `Assets/Scripts/Physics/`. No `*Gate` scenarios added. No scene touched (including
`LabScaffold.unity`). No `M_Splash*.mat` touched. No new `Button` added (no UI work), so rule 11
does not apply.
