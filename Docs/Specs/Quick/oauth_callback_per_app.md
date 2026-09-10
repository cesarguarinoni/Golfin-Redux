# Quick: OAuth callback lands in the app that launched it (`oauth_callback_per_app`)

**Date:** 2026-09-11 · **Reported by Cesar:** "Logging in with Google (probably also Apple or
manual) with both GPS and Game versions installed seems to default to GPS. You can't log into
Game until you delete GPS from the device. The login should go to the app that launched it."

## Cause — one string, three copies, one of them wrong

iOS hands a custom-scheme URL to whichever installed app claims the scheme; with two claimants the
choice is undefined (in practice the most recently installed). Both apps asked Supabase for the
same redirect, and the shell claimed the game's scheme on top of its own:

| Where | Game (`com.nextinnovation.golfingame`) | GPS shell (`com.nextinnovation.golfingps`) | |
|---|---|---|---|
| Info.plist `CFBundleURLSchemes` | `[golfin]` | `[golfin, golfingps]` | **BUG** — `StandaloneBuildPreprocessor` *added* `golfingps` to the game's list ("registered beside", `gps_standalone_shell` §D6) instead of replacing it |
| OAuth `redirect_to` (`SupabaseConfig.oauthRedirect`) | `golfin://auth-callback` | `golfin://auth-callback` | **BUG** — one shared asset, no per-app value |
| Confirm/recovery landing page hop (`confirm.golfin.world`) | `golfin://auth-callback#…` | `golfin://auth-callback#…` | **BUG** — page hard-coded the game |

So: game opens Safari → Google → Supabase 302s to `golfin://auth-callback#tokens` → iOS opens
the **shell** (it claims `golfin`) → the shell's `AuthService.OnDeepLink` accepts it (`IsCallback`
matched `golfin://auth-callback`) and signs the shell in → the game's 8 s watchdog fires
"Sign-in didn't complete". Email/password login has no redirect and cannot be affected; the
signup-confirmation and password-reset links go through the landing page and were.

## Fix — each app claims ONLY its own scheme and asks for callbacks on it

- `Assets/Scripts/Auth/AppDeepLink.cs` (new) — `Scheme` = `golfin` | `golfingps` from the
  `GOLFIN_STANDALONE` define (mirrors `AppVariantInfo`); `WithScheme` re-schemes a deep link,
  `TagLandingPage` appends `?app=golfingps` for the shell (the game's URLs stay byte-identical).
- `SupabaseConfig.OAuthRedirectForThisApp` / `EmailConfirmRedirectForThisApp` /
  `PasswordResetRedirectForThisApp` — the authored fields, derived for this binary. Consumers read
  these, never the raw fields: `OAuthUrlBuilder.Authorize`, `OAuthCallbackParser.IsCallback`,
  `SupabaseAuthClient` (`/signup`, `/resend`, `/recover`).
- `StandaloneBuildPreprocessor` — stamps `CFBundleURLSchemes = [golfingps]` (`StandaloneUrlSchemes`);
  `WithScheme` (the add-beside helper) deleted. Verified in-editor: stamped `[golfingps]`, restored
  `[golfin]`, `ProjectSettings.asset` byte-identical after.
- `BannerPolicy.InternalScheme` / `StandaloneScheme` now alias `AppDeepLink` so the strings
  cannot drift; behaviour unchanged (both routes still accepted in both apps — in-app routing).
- `Tools/golfin-confirm/public/index.html` — reads `?app=`; `golfingps` → hops to
  `golfingps://auth-callback`, anything else → `golfin://` (fail-closed: the page forwards tokens,
  it never deep-links to a scheme a query string made up). **Deployed** — version
  `ed016f49-d647-493c-b904-6a2eaf4a7dcd`, live bytes == repo file, still public (200).
- Tests: `AppDeepLinkTests` (7, `Golfin.Auth.Tests`), `StandaloneUrlSchemeTests` (2,
  `GolfinRedux.Tests.EditMode`, asmdef gained `Golfin.Auth`) — the plist scheme and the runtime
  scheme are two copies of one string, pinned together. Auth 52/52, EditMode 339/339,
  WireupTests 253/253 (BannerPolicy constants), StandaloneGate 10/10.

## What only Cesar can do

1. **Supabase → Authentication → URL Configuration → Redirect URLs: add `golfingps://auth-callback`.**
   Without it Supabase silently ignores the shell's `redirect_to` and falls back to the Site URL
   (`admin.golfin.world`, behind Access) — the shell's Google/Apple login would then break instead
   of stealing the game's. Do this BEFORE the next "punch it standalone". `https://confirm.golfin.world/**`
   (already listed per `auth_email_redirect`) covers the new `?app=golfingps` query.
2. **Ship both apps.** The stale shell on a phone still claims `golfin`; the game's login is only
   safe once the rebuilt shell (`punch it standalone`) is installed beside it. The game build
   (`punch it` / `punch it GPS`) is unchanged in behaviour but carries the alias/property refactor.

## Not touched

`Assets/Plugins/Android/AndroidManifest.xml` still hard-codes `golfin`/`auth-callback` — there is
no Android shell. `Docs/Specs/Completed/gps_standalone_shell/SPEC.md` §D6 ("registered beside")
is left as the historical record of where this came from.
