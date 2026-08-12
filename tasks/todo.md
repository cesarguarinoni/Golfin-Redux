# points_cutover_followups — 2026-08-12

Three bounded follow-ups from reward_points_backend Slice 2 (Cesar-decided 2026-08-12).

## Baseline
- HEAD `25292f73d`
- Pre-existing DIRTY (another session's uncommitted auth-flow work — NOT mine, do not touch):
  `Assets/Scenes/ShellScene.unity`, `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs`,
  `Assets/Scripts/UI/Account/SignUpScreenController.cs`, `Assets/Scripts/UI/SplashScreenController.cs`,
  `Docs/Architecture/UI_HIERARCHY.md`, `tasks/loop_v2_smoke_bot/**`, `_to_delete/*.stale`

## Plan

### 1. Bot auth bypass
- [x] `Assets/Scripts/Dev/BotSessionOverride.cs` — whole-file `#if UNITY_EDITOR || GOLFIN_BOT_HARNESS`.
      Fake local session into AuthService + PointsBackendFlag forced OFF (session-only, non-persisted).
      Armed explicitly (`Arm`) or auto-detected from a live `Golfin.Physics.Viewer.Bot` host.
- [x] `PointsBackendFlag.SessionForcedOff` — non-persisting force-off (must NOT clobber Cesar's PlayerPref).
- [x] `SplashScreenController.OnStartClicked` — override short-circuits BEFORE RefreshSession (no network).
- [x] `TournamentLoopCaptureHarness` — explicit `Arm()` at EnteredPlayMode.
- ZERO edits to `Assets/Scripts/Physics/` (standing ban) — legacy bots covered by auto-detect.

### 2. Shop server spend
- [x] `ShopTransaction.TryPurchase` + `TryPurchaseCatalogEntry` → callback form through `PointsSpendGate.Spend`.
- [x] New `SpendReasons.StaminaBoost` / `ShopPurchase` (spend reasons are free-form — no backend edit).
- [x] `StaminaShopDetailScreenController` / `GeneralShopScreenController` — busy state + SpendDenied (no double toast).

### 3. Hard sign-in gate
- [x] Delete `DevBypassCatcher_TEMP` from SplashScreenController.
- [x] `AuthGate` in ScreenManager.ShowScreen, mirroring the existing DemoGate seam.

## Verify
- [x] Compile clean
- [x] EditMode suite green — FULL unfiltered run (1175 total), not a filtered one, since filtered
      runs report FailedTests only for the filter. Run twice: once mid-task, once on the final code.
- [x] TournamentLoopCaptureHarness reaches `=== SEQUENCE COMPLETE ===` from boot

## Review (2026-08-12)

All three items landed; EditMode 1172/0/3 of 1175 (re-run on final code);
`TournamentLoopCaptureHarness` reached `=== SEQUENCE COMPLETE ===` twice from boot.

**What the work actually turned on.** Two constraints shaped item 1 more than the feature itself:
`Assets/Scripts/Physics/` is a zero-edit zone (so every legacy bot host had to be covered by
namespace auto-detection rather than an `Arm()` call), and an asmdef cannot reference
Assembly-CSharp (so the override needed its own `Golfin.DevHarness` assembly for the Editor
harness to reach it — named around the existing `Golfin.Dev` in `Debug/ScreenshotCapture/`).

**The bug worth remembering** is Lesson AW: with domain reload disabled, a "non-persisting" static
is not self-cleaning. The first acceptance run passed and still left the Editor with the points
backend silently forced off. Verifying cleanup by *reading state back after play-mode exit* — not
by reasoning about lifetimes — is what caught it, and it also surfaced a latent `session.Clear()`
that would have deleted Cesar's real persisted session at the end of every bot run.

**Left for Cesar (device):** the three manual checks listed in IMPLEMENTER_REPORT Part 3.
**Not mine to commit:** another session's uncommitted auth-flow work shares
`SplashScreenController.cs` — see the report's drift table before staging.
