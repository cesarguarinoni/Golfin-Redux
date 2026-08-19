# IMPLEMENTER_REPORT — auth_recovery_flow

**Iteration:** 1
**Iteration shape:** auth-recovery-ui:shellscene-wiring
**Date:** 2026-08-19
**Baseline:** `HEARTBEAT.log` iter-1 block — HEAD `ac41914b6e0affb25d95c8efcc0855e48bbd8d88`

Two sessions produced this task. Cowork wrote the C# + tests (HANDOFF.md §2) and left the
ShellScene UI unbuilt; this session built the screen, wired it, fixed one layout defect it
surfaced, and ran the full sweep. Nothing from §2 was rewritten.

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Recovery deep link opens the set-new-password screen (NOT a silent sign-in) | PASS | Play mode, real entry path: title-gate `StartButton.onClick` → Login, then `AuthService.HandleAuthCallback("golfin://auth-callback#access_token=…&type=recovery")` — the same method `Application.deepLinkActivated` invokes. Result: `CurrentScreen=ResetPassword`, `PendingRecovery=HELD`, `Session.IsAuthenticated=False`, `Session.AccessToken=<empty>`. Captured in `screenshots/reset_password_en_clean.png`. |
| Tokens are held, never persisted, until the password update succeeds | PASS | Measured before and after the deep link: `Session.AccessToken` stayed `<empty>` while `PendingRecovery` went `null → HELD`. After a rejected update the session was still `IsAuthenticated=False`. |
| Client-side validation: password too weak | PASS | Real `SubmitButton.onClick.Invoke()` with `abc`/`abc` → MessageLabel active, text `Password does not meet the requirements.` (`AUTH_RESET_TOO_SHORT`), no network call. |
| Client-side validation: passwords do not match | PASS | Real `SubmitButton.onClick.Invoke()` with `Str0ng!Pass1`/`Str0ng!Pass2` → MessageLabel active, text `Passwords do not match.` (`AUTH_RESET_MISMATCH`). Frame: `screenshots/reset_password_en_error_mismatch.png`. |
| Expired / rejected reset token shows the localized error and does not sign the player in | PASS | Editor runs the LIVE transport (`SupabaseConfig.useMockTransport: 0`, real anon key), so a valid+matching submit with a fabricated recovery token went to the real `PUT /auth/v1/user` and was refused. Result: `This reset link has expired or was already used. Please request a new one from the login screen.` in `#E5484D`, `CurrentScreen` still `ResetPassword`, `Session.IsAuthenticated=False`, submit button re-enabled. |
| Expired-link *fragment* (`error_code=otp_expired`) surfaces on the Login screen | PASS | With Login open, fired `golfin://auth-callback#error=access_denied&error_code=otp_expired&error_description=…`. Login's MessageLabel became active with `AUTH_RESET_LINK_EXPIRED`; `CurrentScreen=Login`, `PendingRecovery=null`, `IsAuthenticated=False`. |
| Back to Login clears the held tokens first (no bounce-back loop) | PASS | Real `BackButton.onClick.Invoke()` → `PendingRecovery=null` immediately, then `CurrentScreen=Login` after the fade. Order matters: `LoginScreenController.OnEnable` re-routes to ResetPassword whenever tokens are held, so a clear-after-navigate would ping-pong. |
| Regression guard: a plain signup-confirmation deep link behaves exactly as before | PASS | Fired the same URL shape without `type=recovery`: `PendingRecovery` stayed `null` and `Session.AccessToken` was SET — i.e. the pre-existing sign-in path ran untouched. The fabricated session was cleared with `SignOut()` before leaving play mode. |
| Eye toggle unmasks BOTH fields | PASS | `EyeButton.onClick.Invoke()` moved both `NewPasswordInput` and `ConfirmPasswordField` `Password → Standard`, and back on the second press. One button by design (both fields hold the same secret). |
| EN strings render | PASS | `screenshots/reset_password_en_clean.png` — title `Set New Password`, labels + both placeholders, `SET PASSWORD`, `Back to Login`, all from `AUTH_RESET_*`. |
| JA strings render | PASS | `LocalizationManager.SetLanguage(Japanese)` live: 新しいパスワードを設定 / 新しいパスワード / 新しいパスワード（確認） / パスワードを設定 / ログインに戻る, plus a fresh JA error パスワードが一致しません. Frame: `screenshots/reset_password_ja_error_mismatch.png`. |
| No white-box placeholders — every `[SerializeField]` wired | PASS | After reopening the saved scene, iterated every `ObjectReference` property on the live `ResetPasswordScreenController` via `SerializedObject`: **0 null refs** across all 18. `ScreenManager._resetPasswordScreen = ResetPasswordScreen`. |
| Submit label sits inside its button (no text-outside-container) | PASS | First render FAILED this: `LayoutElement.preferredWidth=388` (baked for the word LOGIN) overrode the button's own ContentSizeFitter, so `SET PASSWORD` (467px preferred) rendered outside the green fill. Fixed by setting `preferredWidth = -1`. Re-measured with `GetWorldCorners`: button `w=658.6`, label `w=466.6`, `LABEL_INSIDE_BUTTON=True`. |
| JA copy fits its containers | PASS | Per-element `TMP.preferredWidth` vs measured slot, in play mode: SubmitButton label EN 467 / JA 440 vs slot 467; BackButton label EN 401 / JA 385 vs slot 445; both password labels well under the 978 slot. SectionHeader's slot is text-driven (no `preferredWidth` override) so JA 605 ≤ 978 available. |
| New screen left INACTIVE like its siblings | PASS | `ResetPasswordScreen.activeSelf = False` on disk and after scene reopen; `MessageLabel.activeSelf = False`, matching LoginScreen. |
| Scene-mutation guardrail: nothing outside the new screen changed | PASS | Block-level YAML diff HEAD vs saved scene: 121 new blocks, **0 removed**, and exactly 2 pre-existing blocks differ — `!u!224 &677848798` (ScreensRoot gained the new child) and `!u!114 &825584067` (ScreenManager gained `_resetPasswordScreen`). 0 dangling scene-local `fileID` refs (same as HEAD). See § Scene-save churn below. |
| Full unfiltered EditMode sweep | PASS | 1496 tests: **1492 passed, 1 failed, 3 skipped**. The single failure is pre-existing and unrelated — see § Tests. |

---

## Screenshot

Canonical screenshot: `screenshots/reset_password_en_clean.png` (1170×2532, iPhone 14, play mode,
reached through the real title-gate → Login → recovery deep link path).

| File | State |
|---|---|
| `screenshots/reset_password_en_clean.png` | EN, fresh screen, both placeholders, submit + back |
| `screenshots/reset_password_en_error_mismatch.png` | EN, mismatch error, both fields masked |
| `screenshots/reset_password_ja_error_mismatch.png` | JA, all copy localized, JA error string |

---

## Clone provenance

Every visual element is a clone of the LoginScreen subtree — no element was hand-built, and no
flat-fill stand-ins were authored.

| Element | Source |
|---|---|
| `ResetPasswordScreen` (whole subtree: BG, Scrim, CardBorder, CardBody, ScrollView, Viewport, Content) | `Object.Instantiate` of `/Canvas/ScreensRoot/LoginScreen` |
| `SectionHeader` | LoginScreen `Content/SectionHeader` (retitled) |
| `NewPasswordLabel` | LoginScreen `Content/EmailLabel` (renamed + retitled) |
| `NewPasswordRow` / `NewPasswordInput` / `EyeButton` / `EyeIconImg` | LoginScreen `Content/PasswordRow` (renamed; eye button and icon carried over as-is) |
| `ConfirmPasswordLabel` | LoginScreen `Content/PasswordLabel` (renamed + retitled) |
| `ConfirmPasswordField` | LoginScreen `Content/EmailField` (renamed; `ContentType` switched to `Password`) |
| `SubmitButton` + `Label` | LoginScreen `Content/LoginButton` |
| `BackButton` + `Label` | LoginScreen `Content/CancelButton` |
| `MessageLabel` | LoginScreen `Content/MessageLabel` |
| `_eyeShowSprite` | `S_Settings_Icon_EyeOn`, guid `985195deea614f14ca3fe265203c529d` (copied off the LoginScreen controller) |
| `_eyeHideSprite` | `S_Settings_Icon_EyeOff`, guid `5b0184341b55e7e4b80b8f668b5c8757` (copied off the LoginScreen controller) |

Deleted from the clone (login-only): `ForgotPassword`, `Separator1`, `ServiceHeader`, `GooglePill`,
`ApplePill`, `ServiceSpacer`, `Separator2`, `Footer`.

`ButtonPressFeedback` (CLAUDE.md rule 11) rides along on all three cloned buttons — no new bare
`Button` was authored.

---

## Tests

Full unfiltered EditMode sweep (Assembly-CSharp was touched, so the auth-only filter is not
sufficient):

```
TotalTests 1496 · Passed 1492 · Failed 1 · Skipped 3 · 00:01:14
```

The one failure is **pre-existing and unrelated to this task**:

```
Golfin.Gameplay.Tests.GameSessionTests.OnHoleComplete_FiresOnMarkHoleComplete_WithCorrectPayload
System.InvalidOperationException : The following game object is invoking the DontDestroyOnLoad
method: [Golfin.Telemetry]. Notice that DontDestroyOnLoad can only be used in play mode...
```

Attribution, derived rather than asserted:
- The test file `Assets/Scripts/Gameplay/Tests/GameSessionTests.cs` and the source that throws
  (`Assets/Scripts/Telemetry/TelemetryBehaviour.cs:29`, a self-bootstrapping `DontDestroyOnLoad`
  host) are both **committed and clean** — neither appears in the iter-1 baseline `DIRTY:` list in
  `HEARTBEAT.log`, whose auth-related entries are `Assets/Scripts/Auth/*`,
  `Assets/Scripts/UI/Account/*`, `Assets/Scripts/UI/ScreenManager.cs`,
  `Assets/Scripts/UI/AuthGate.cs`.
- Grep confirms no file this task touched references `Telemetry` at all.

The 3 skips are the documented Stage-C1 `HoleCompleteDriverTests` retirements, unchanged.

The 14 new `RecoveryFlowTests` and the 4 `AuthRedirectUrlTests` are inside the 1492 passes.

---

## Scene-save churn — caught and repaired

Saving `ShellScene.unity` **after a play-mode session** baked layout churn into 154 pre-existing
`RectTransform` blocks plus 3 `PrefabInstance` blocks (`m_AnchorMin/Max` reset to `(0,0)`,
`m_AnchoredPosition` zeroed, prefab override values dropped) — the known
`project_scene_save_bakes_layout_churn` scar.

Repair: every YAML block present in HEAD was restored to its HEAD content, except the two
intentionally-changed blocks (`&677848798` ScreensRoot child list, `&825584067` ScreenManager);
new blocks were kept. Verified afterwards — only those 2 pre-existing blocks differ, 0 removed,
0 dangling refs — then the scene was reopened in the Editor (`dirty=False`) and re-inspected: all
18 controller refs wired, `_resetPasswordScreen` set, `_mainThemeClip` intact, `SubmitButton`
`preferredWidth=-1`. The verification play-mode run that followed was exited **without saving**.

---

## Files modified or created

This task's files (HANDOFF.md §2 + this session's additions):

| File | What |
|---|---|
| `Assets/Scripts/Auth/OAuthCallbackParser.cs` | `CallbackInfo` + `GetCallbackInfo(url)` — exposes `type` / `error` / `error_code` / `error_description`; `Parse` untouched (Cowork) |
| `Assets/Scripts/Auth/AuthService.cs` | `OnDeepLink` recovery + error branches, `PendingRecovery`, `UpdatePasswordWithRecovery`, `CancelPasswordRecovery`, `ConsumeRecoveryFailure`, `HandleAuthCallback` seam (Cowork) |
| `Assets/Scripts/Auth/ISupabaseAuthClient.cs` | `UpdatePassword(accessToken, newPassword, cb)` (Cowork) |
| `Assets/Scripts/Auth/SupabaseAuthClient.cs` | `UpdatePassword` → `PUT /user`; `PasswordBody()` made public for wire-shape tests (Cowork) |
| `Assets/Scripts/Auth/MockSupabaseAuthClient.cs` | `UpdatePassword` mock (token→account, 8-char floor) (Cowork) |
| `Assets/Scripts/Auth/Tests/RecoveryFlowTests.cs` (+ `.meta`) | 14 new tests (Cowork) |
| `Assets/Scripts/UI/ScreenManager.cs` | `ScreenId.ResetPassword`, `_resetPasswordScreen`, `ApplyScreen` line, account-title-bar inclusion, `PasswordRecovery` subscription (Cowork) |
| `Assets/Scripts/UI/AuthGate.cs` | `ResetPassword` added to the pre-auth allowlist (Cowork) |
| `Assets/Scripts/UI/Account/LoginScreenController.cs` | Cold-start recovery hooks in `OnEnable`; expired-link error while open (Cowork) |
| `Assets/Scripts/UI/Account/ResetPasswordScreenController.cs` (+ `.meta`) | New screen controller (Cowork) **+ this session:** 7 localized-label `[SerializeField]`s and `ApplyLocalization()` wired to `LocalizationManager.OnLanguageChanged` |
| `Assets/Localization/LocalizationText.csv` | 9 `AUTH_RESET_*` rows, EN+JA (Cowork) — **JA still flagged for native review** |
| `Assets/Localization/LocalizationTextTable.asset` | Regenerated from the CSV by `LocalizationPlaymodeHook` on entering play mode; not hand-edited |
| `Assets/Scenes/ShellScene.unity` | **This session:** `ResetPasswordScreen` built (27 GameObjects) + `ScreenManager._resetPasswordScreen` wired |
| `Docs/TellCode.md` | Pointer + kickoff under SPEC_READY POINTERS (Cowork) |
| `Docs/Specs/Active/auth_recovery_flow/*` | SPEC, HANDOFF, STATUS, this report, HEARTBEAT, screenshots |

Uncommitted paths in the working tree that are **NOT this task** — reported per Rule 13, left
untouched, and deliberately excluded from this task's commit (in-flight work from other sessions):

| File | Owner |
|---|---|
| `Assets/Scripts/Tournaments/TournamentDefinition.cs` | `tournament_restrictions` |
| `Assets/Scripts/Tournaments/TournamentEligibility.cs` (+ `.meta`) | `tournament_restrictions` |
| `Assets/Scripts/Tournaments/TournamentRestrictions.cs` (+ `.meta`) | `tournament_restrictions` |
| `Assets/Scripts/Tournaments/Tests/TournamentEligibilityTests.cs` (+ `.meta`) | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/RemoteTournamentBackend.cs` | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/RemoteTournamentDtos.cs` | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/TournamentNetDtos.cs` | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/TournamentScheduleMapper.cs` | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/TournamentRulesText.cs` (+ `.meta`) | `tournament_restrictions` |
| `Assets/Scripts/TournamentsRuntime/Tests/TournamentRestrictionsClientTests.cs` (+ `.meta`) | `tournament_restrictions` |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | `tournament_restrictions` |
| `Docs/Specs/Active/tournament_restrictions/{SPEC,STATUS,IMPLEMENTER_REPORT,ARCHITECT_REVIEW,ARCHITECT_HANDOFF}.md` | `tournament_restrictions` |
| `Assets/Resources/Clubs/Controls/S_Controls_{Driver,Putter}_KLYRO.png` (+ `.meta`) | KLYRO club art |
| `Assets/Resources/Clubs/Full/{Driver,Putter,Wedge,Wood}-Klyro.png` (+ `.meta`) | KLYRO club art |
| `Assets/Resources/Clubs/Portraits/S_Menu_{Driver,Putter}_KLYRO.png` (+ `.meta`) | KLYRO club art |
| `Docs/AI_CONTEXT.md` | carries another session's in-flight edits; updated here but NOT committed |
| `tasks/lessons.md` | another session |
| `Docs/Versioning/last_uploaded_build.txt` | build lane |

---

## Needs manual on-device verification (cannot be closed in the Editor)

1. **Real end-to-end reset on iPhone** — in-game "forgot password" → email → tap the link →
   the game opens the set-new-password screen. The Editor drove the same `OnDeepLink` entry point
   with a synthetic URL; iOS URL-scheme delivery itself is untested here.
2. **A real recovery token succeeding** — the Editor's live transport correctly *rejected* a
   fabricated token, so only the failure branch is proven end-to-end. Signing out and back in with
   the new password, and the old password being refused, needs a real emailed link.
3. **EN + JA on device** — verified in the Editor Game View at 1170×2532; device font rendering and
   the iOS keyboard's interaction with the masked fields are unverified.

## Open NOTEs carried from HANDOFF §5

- **Supabase password minimum length: still UNVERIFIED** against the project settings
  (Auth → Providers → Email). The client enforces `PasswordRequirements` (8+ with character
  classes), stricter than Supabase's default 6, so the client cannot submit something the server
  refuses on length; a server-side floor above 8 would surface through the `WeakPassword` branch.
  One dashboard check retires this NOTE.
- **JA copy is machine-authored** and flagged for native review.
- The empty lower half of the card is the account-screen family style, not a defect introduced
  here: `LoginScreen`, `SignUpScreen`, `CreateUsernameScreen` and `EmailConfirmationScreen` all use
  the same full-height stretched card (1080×2123, `sizeDelta -90/-409`), and the two sparse siblings
  look the same. Worth a design pass across all four, not a one-screen fix.
