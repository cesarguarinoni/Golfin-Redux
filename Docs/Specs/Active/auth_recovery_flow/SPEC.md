# Spec: auth_recovery_flow — make password reset actually work

**Slug:** `auth_recovery_flow` · **Status:** SPEC_READY · **Date:** 2026-08-19
**Repo home (Implementer creates):** `Docs/Specs/Active/auth_recovery_flow/SPEC.md` + `STATUS.md` (`SPEC_READY`)
**Prereq reading:** `Docs/Specs/Active/auth_email_redirect/HANDOFF.md` §"Password reset is still broken — and it fails badly" and `IMPLEMENTER_REPORT.md` (the defect inventory below comes from there — verified 2026-08-19, commit `ac41914b6`).

## Problem (verified, not assumed)

A password-reset link today **silently signs the player in with the password unchanged**:

1. `AuthService.OnDeepLink` (`Assets/Scripts/Auth/AuthService.cs:202`) has ONE branch — every
   callback goes to `OAuthCallbackParser.Parse`, which reads only `access_token` from the fragment.
2. `type=recovery` in the fragment is never read; the recovery tokens are applied as an ordinary
   session, saved, and `RaiseSignedIn()` fires.
3. `ISupabaseAuthClient` has **no password-update method** (`UpdateDisplayName` is the only
   `PUT /user` caller), and `Assets/Scripts/UI/Account/` contains only `LoginScreenController`.

Server side is already in place (2026-08-19): reset emails are branded EN+JA and redirect through
`https://confirm.golfin.world/?type=recovery`, which deep-links `golfin://auth-callback#<fragment>`
into the app on mobile — the fragment carries `type=recovery`. Nothing more is needed outside Unity.

## Scope (Unity only)

1. **Parse `type` (and errors) from the callback fragment.** Extend `OAuthCallbackParser` to expose
   `type` and the error params (`error`, `error_code`, `error_description`) that Supabase puts in the
   fragment when a link is expired/used (e.g. `error_code=otp_expired`). Keep the existing parse
   behavior for everything else — minimal diff.
2. **Branch in `AuthService.OnDeepLink`:**
   - `type == "recovery"` → hold the tokens as a *recovery session* and route to the new
     set-new-password screen. Do **not** persist the session or `RaiseSignedIn()` until the new
     password is set successfully. (If holding an unsaved session doesn't fit `AuthSession`'s current
     shape, flag it in the report rather than force-fitting — an in-memory holder object is fine.)
   - fragment contains `error` → surface a localized failure message (expired/used link), no sign-in.
   - anything else → existing behavior, byte-for-byte.
3. **`ISupabaseAuthClient.UpdatePassword(accessToken, newPassword)`** → `PUT /auth/v1/user` with
   `{"password":"..."}` using the recovery session's access token. Mirror `UpdateDisplayName`'s
   request plumbing. Add to `MockSupabaseAuthClient` too.
4. **Set-new-password UI.** New screen/modal alongside `LoginScreenController` in
   `Assets/Scripts/UI/Account/` — follow whichever pattern LoginScreen uses (NOTE: check whether it
   is ScreenManager-routed or a ModalController overlay and match it; do not invent a new pattern).
   Fields: new password + confirm; submit calls `UpdatePassword`; on success → save session,
   `RaiseSignedIn()`, continue into the game; on failure → localized error, stay on screen.
5. **Localization:** EN + JA keys via `LocalizationText.csv` / LocalizationManager for: screen title,
   field labels, submit, mismatch error, too-short error, expired-link error, success toast.
   (NOTE: password minimum length — read it from the Supabase project settings before hardcoding;
   default is 6 but verify.)
6. **Tests** in `Golfin.Auth.Tests` (`Assets/Scripts/Auth/Tests/`): fragment `type` extraction,
   error-fragment extraction, recovery does-not-persist-before-update behavior (whatever seam makes
   that testable), `UpdatePassword` request shape. Also run the whole `Golfin.Auth.Tests` assembly —
   the previous task's `AuthRedirectUrlTests` have never executed in the Editor (harness limit on the
   PC); this task inherits that verification.

## Out of scope

- Custom SMTP / sender identity (separate, Cesar-driven).
- Admin-dashboard "reset password / set password manually" actions (separate backlog spec).
- Email-change and magic-link flows (same parser touchpoint, different tasks).
- Landing-page changes (`Tools/golfin-confirm`) — fragment already forwards; page copy already
  handles `?type=recovery`.

## Acceptance

- [ ] In-game "forgot password" → email → tap on iPhone → game opens the set-new-password screen
      (NOT a silent sign-in).
- [ ] Setting a new password succeeds; signing out and back in works with the new password; the old
      password is rejected.
- [ ] An expired/reused reset link shows the localized error and does not sign the player in.
- [ ] A plain signup-confirmation deep link still behaves exactly as before (regression guard).
- [ ] `Golfin.Auth.Tests` passes in the Editor, including the pre-existing `AuthRedirectUrlTests`.
- [ ] EN + JA strings render on-device.

## Editor prerequisite (fold into this task's session)

Unity has not yet imported `Assets/Scripts/Auth/AuthRedirectUrl.cs` (hand-written .meta from the
PC session). With the Editor open: let it import/refresh, confirm no compile errors, then run
`Golfin.Auth.Tests` — before starting the new work.
