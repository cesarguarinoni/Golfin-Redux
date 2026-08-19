# HANDOFF — auth_recovery_flow (Cowork → Claude Code, 2026-08-19)

Cesar kicked this spec off in the Cowork session; partway through he redirected it to Code.
**The Unity code and the tests are already written and verified compiling + green (filtered).
The ShellScene UI wiring is NOT done.** Everything below is uncommitted working-tree state.

⚠️ **The working tree carries OTHER in-flight, uncommitted work that is NOT part of this task**
(`tournament_restrictions` client files, KLYRO club art under `Assets/Resources/Clubs/`,
`Docs/AI_CONTEXT.md` edits, `tasks/lessons.md`, `Docs/Versioning/last_uploaded_build.txt`,
staged hunks in `Docs/TellCode.md`). Commit ONLY the files in §2.

## 1 · Editor prerequisite (SPEC last section) — DONE, result verified
- `assets-refresh` ran clean; `AuthRedirectUrl.cs` imported off its hand-written .meta; 0 compile errors.
- `Golfin.Auth.Tests` EditMode **31/31 passed** pre-change, including all 4 `AuthRedirectUrlTests`
  BY NAME — their first-ever Editor execution (the PC harness never ran them).

## 2 · Files this task touched (commit exactly these)
Modified:
- `Assets/Scripts/Auth/OAuthCallbackParser.cs` — added `CallbackInfo` struct + `GetCallbackInfo(url)`
  (type / error / error_code / error_description); `Parse` behavior untouched.
- `Assets/Scripts/Auth/AuthService.cs` — `OnDeepLink` branches: `type=recovery` → tokens HELD in
  `PendingRecovery` (nothing persisted, no `RaiseSignedIn`), error-with-no-pending-OAuth → failure
  surfaced via static `PasswordRecovery` event + `ConsumeRecoveryFailure()` cold-start seam; new
  `UpdatePasswordWithRecovery` (persist + `SignedIn` ONLY after server accepts), `CancelPasswordRecovery`,
  public `HandleAuthCallback` test seam; `ConfigureForTest` now guarantees a `Config` in edit mode
  (edit mode never runs `Awake`, so `IsCallback` rejected every test deep link without it).
- `Assets/Scripts/Auth/ISupabaseAuthClient.cs` — `UpdatePassword(accessToken, newPassword, cb)`.
- `Assets/Scripts/Auth/SupabaseAuthClient.cs` — `UpdatePassword` → `PUT /user` mirroring
  `UpdateDisplayName`; `public static PasswordBody()` so the wire shape is unit-testable.
- `Assets/Scripts/Auth/MockSupabaseAuthClient.cs` — `UpdatePassword` (token→account, 8-char floor
  mirroring mock SignUp, mutates stored password).
- `Assets/Scripts/UI/ScreenManager.cs` — `ScreenId.ResetPassword` + `_resetPasswordScreen` field +
  `ApplyScreen` line + account-title-bar inclusion; subscribes `PasswordRecovery` in `Start()`
  (AFTER initial ApplyScreen — see routing note in §4) for warm-path routing; `OnDestroy` unsubscribes.
- `Assets/Scripts/UI/AuthGate.cs` — `ResetPassword` added to the pre-auth allowlist.
- `Assets/Scripts/UI/Account/LoginScreenController.cs` — cold-start hooks in `OnEnable`:
  `PendingRecovery` held → route to ResetPassword; `ConsumeRecoveryFailure()` → localized expired-link
  error; also subscribes `PasswordRecovery` while open (expired link tapped ON the login screen would
  otherwise be a silent no-op because `ShowScreen(Login)` dedupes).
- `Assets/Localization/LocalizationText.csv` — 9 rows appended after `AUTH_USERNAME_TAKEN`:
  `AUTH_RESET_TITLE / _NEW_PLACEHOLDER / _CONFIRM_PLACEHOLDER / _BUTTON / _MISMATCH / _TOO_SHORT /
  _LINK_EXPIRED / _SUCCESS / _BACK` (EN+JA; **JA flagged for native review**).
- `Docs/TellCode.md` — pointer + kickoff under SPEC_READY POINTERS.

New:
- `Assets/Scripts/UI/Account/ResetPasswordScreenController.cs` (+.meta) — ScreenManager-routed like
  LoginScreen (NOT a modal — the account flow has no ModalController overlays; SPEC §4's "check the
  pattern" resolved to ScreenManager routing). Validates via existing `PasswordRequirements.AllMet`
  then mismatch, calls `UpdatePasswordWithRecovery`, on success `AccountUiBridge.SyncUsername()` +
  route `HasDisplayName ? Home : CreateUsername` (mirrors LoginScreen); Back = `CancelPasswordRecovery()`
  then Login (MUST clear first — Login.OnEnable re-routes whenever tokens are held).
- `Assets/Scripts/Auth/Tests/RecoveryFlowTests.cs` (+.meta) — 14 tests: CallbackInfo extraction (4),
  PasswordBody shape + mock UpdatePassword (4), and `RecoveryDeepLinkTests` (6) driving the real
  AuthService component through `HandleAuthCallback`: hold-don't-persist, update-then-persist+SignedIn,
  no-link failure, expired-link surfacing, cancel, and the signup-confirmation regression guard.
- `Docs/Specs/Active/auth_recovery_flow/` — SPEC.md (verbatim from Architect), STATUS.md, this file.

## 3 · Test state at handoff
- Filtered `Golfin.Auth.Tests` EditMode: **45 passed / 0 failed / 0 skipped** (31 pre-existing + 14 new).
  House caveat applies: a filtered green proves only the filter — **run the full unfiltered EditMode
  sweep** before closing; `ScreenManager` / `AuthGate` / `LoginScreenController` live in
  Assembly-CSharp and are outside the auth filter (they compile clean; behavior unswept).
- `tests-run` returned its known transient "No tests found" twice this session — retry, per AI_CONTEXT.

## 4 · Remaining work
1. **ShellScene wiring** (the whole UI): duplicate the Login screen GameObject → `ResetPasswordScreen`
   (clone-provenance: cite the source object path), strip to title / new-password + confirm
   `TMP_InputField`s / one eye toggle / submit / back-to-login / error label; add
   `ResetPasswordScreenController`, wire every `[SerializeField]` (no white-box placeholders), reuse
   Login's eye sprites; `LocalizedText` components with the `AUTH_RESET_*` keys (incl. both input
   placeholders); wire `ScreenManager._resetPasswordScreen`; new screen left INACTIVE like its
   siblings. Scene-save guardrail: diff active-state vs HEAD before saving (home_notices scar).
2. Localization table: CSV→asset regenerates on play mode/build (`LocalizationPlaymodeHook`); note
   `LocalizationTextTable.asset` already shows modified in git — I did not edit it directly.
3. Full unfiltered EditMode sweep (§3).
4. SPEC §Acceptance: run what the Editor can (regression deep link, expired link, recovery flow via
   `HandleAuthCallback` + real widget clicks); flag the device-only items (iPhone mail-tap → screen,
   EN+JA on device, real Supabase round-trip with old/new password).
5. STATUS.md + IMPLEMENTER_REPORT.md + Docs/AI_CONTEXT.md at close (AI_CONTEXT deliberately not
   touched here — it has in-flight edits from another session).

## 5 · Flags / NOTEs (SPEC asked for these rather than guesses)
- **Password minimum length (SPEC §5 NOTE): UNVERIFIED against Supabase project settings** — needs the
  dashboard (Auth → Providers → Email). Client enforces the existing `PasswordRequirements` (8+ with
  character classes, same as Sign Up) which is stricter than Supabase's default 6, so the client can't
  submit something the server refuses on length; if the project setting is ever raised above 8 the
  server error surfaces via the `WeakPassword` branch. Cesar: verify once, then this NOTE dies.
- **Recovery session holder**: `AuthSession`'s shape was NOT force-fitted — the held tokens live as an
  in-memory `AuthResult` (`PendingRecovery`), exactly the spec's sanctioned fallback.
- **Routing model**: warm path (app already running) = `ScreenManager.OnPasswordRecovery`; cold path
  (launched BY the link) = `LoginScreenController.OnEnable` reading `PendingRecovery`, because the
  cold-start event fires during `AfterSceneLoad`, before `ScreenManager.Start`'s subscription, and the
  boot flow lands on Login via AuthGate anyway. Subscribing later than Start would miss warm links;
  earlier would be stomped by `ApplyScreen(_initialScreen)`.
- **Error branch scope**: an error fragment WITH a pending OAuth attempt keeps today's behavior
  byte-for-byte; only the no-pending case (email links) surfaces the new localized failure.
- **DemoGate** not extended — GOLFIN_DEMO builds have no auth; ResetPassword is simply unreachable there.
- **Eye toggle**: one button unmasks BOTH fields (they hold the same secret); confirm-field has no
  separate toggle. Trivial to split if Cesar wants parity with Login.
