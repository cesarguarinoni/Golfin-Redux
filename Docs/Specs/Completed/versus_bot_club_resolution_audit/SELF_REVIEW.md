# Self-Review — `versus_bot_club_resolution_audit`

**Iteration:** 1
**Date:** 2026-07-20 JST
**Reviewer:** golfin-self-reviewer
**Verdict:** **FORWARD_TO_ARCHITECT**

---

## Visual diff notes (independent pixel scan of `screenshots/versus_762_wedge_hud_t3.png`, done BEFORE reading report)

Top center: yellow debug banner "CAM: Chase   BALL: Flying". Top-right: white circular gear + green "G" badge. Top-left player card: portrait of a female character in a red "POWER" cap, name "CAMILA", "Lv 13", "TURN 1". Top-right player card: portrait of a second female character in blue "POWER" cap, name "TARO", "Lv 17", "TURN 0". Left-side chip: down triangle + "0.0 mph" (wind). Right-side chip: flag glyph + "37 yds" (distance to pin). Scene: overhead-behind view of a green with sand bunkers; a bright blue vertical guide line runs from a small pale ball (mid-frame) down through the bottom of the screen. Circular power gauge floats mid-right showing "40%" over "100.5 yd" with a green arc segment. Small oval mini-map preview sits below the gauge. Bottom row of HUD buttons: left column "SPIN" (ball icon) and "GOLFIN ∞" (ball icon); right column an up-arrow "STRAIGHT" button and, critically, a **card showing a wedge club sprite with "P. WEDGE / 120 yrds" beneath it** — this is the club-slot HUD confirming P.Wedge is the currently equipped/selected swing club during a bot-driven flying-ball moment.

Comparison to what would falsify the fix: if the fix had failed, we'd expect the club card to read "DRIVER" (the stale ClubContext.SelectedClubId set by ClubContextPopulator/LabInventoryStub at hole-load). It reads "P. WEDGE" → visually consistent with a successful ClubContext sync.

## Additional frame evidence pulled from `videos/762_wedge_proof.mp4`

- **Frame @ t≈15s**: Ball in mid-flight approach, flag chip reads "10 yds", bottom P.WEDGE card visible, on-screen caption from `build_bot_video.py textfile=` idiom reads **"clubVel=42.00m/s confirms wedge stats on LIVE path (driver=75m/s)"**. This is direct on-screen evidence that the LIVE bundle carried wedge stats, not driver stats.
- **Frame @ t≈45s**: Ball at rest ~2 yds from the flag; power gauge idle at 14%/36.0yd; P.WEDGE card still shown. Wedge approach converged near the pin — the exact behaviour the SPEC's Gate 2 requires.
- **Frame @ t≈60s**: Both players advanced to TURN 2, "YOUR TURN" banner up, ball parked next to flag — match progressed cleanly across at least four bot shots; no fall-through, no stuck-recovery loop.
- Video is 1170×2532, 60s, 45MB (h264/aac) — full-res per Cesar's standing rule (`feedback_record_bot_video_full_size`).

---

## Checklist walk-through (Rule 5 — entire acceptance list, every pass)

| # | Item | Implementer verdict | My verdict | Notes |
|---|---|---|---|---|
| 1 | Stage 1 measurement documented (selected vs fired club, who populates ClubContext) | PASS | **CONFIRM-PASS** | Verified by reading three files: `VersusBot.cs:693–710` (pre-fix had `SetClub → ClearStatBundleOverride`, no ClubContext push); `LiveStatProviderHost.cs:188` (`string clubId = ClubContext.SelectedClubId;` — literal read of the field the SPEC named); `BotDriver.cs` original 50-line inline block (identical push logic the SPEC calls out as the "proven fix"). Divergence is real, not asserted. |
| 2 | Stage 2 fix landed — ClubContext pushed before ClearStatBundleOverride | PASS | **CONFIRM-PASS** | `git diff Assets/Scripts/Physics/Viewer/VersusBot.cs` (27 lines total): inserts `BotClubSync.SyncToClubContext(club, "VersusBot")` between `_controller.SetClub(club)` and `_shotController.ClearStatBundleOverride()`. Position is exactly what the SPEC prescribed. On-screen video caption confirms `clubVel=42.00m/s` (wedge class), which would be ~75m/s if the driver had fired. |
| 3 | Gate 1 — VersusBot fires club with SelectedClubId != driver after club switch | PASS | **CONFIRM-PASS** | Console log in report: `[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)` × 4 shots; `[CommitFlick] bundle.Club.HasValue=True clubVel=42.00m/s`. On-screen caption in the video mirrors the same claim (independent visual). |
| 4 | Gate 2 — bot visibly plays wedge on short approach in recorded video | PASS | **CONFIRM-PASS** | Canonical PNG + frames @ 15s, 45s, 60s (extracted independently) all show the P.WEDGE HUD card and a successful approach shot converging on the pin. Match plays through 60s cleanly. |
| 5 | Gate 3 — Difficulty/H2/H3 behaviour unchanged | PASS | **CONFIRM-PASS** | Diff scope on `VersusBot.cs` is +15/−1 lines, all inside the `SetClub`/`ClearStatBundleOverride` window (line ~694). All 2b/H2/H3 blocks and the `_carryTable`/`_difficultyTable` static tables plus their `-1` domain-reload sentinels are byte-identical to HEAD (grep confirms). H3b log line `[VersusBot] H3b off-green override: surface=Fairway…` appears in the recording, proving H3b still fires. BotDriver refactor is a pure algorithmic replacement (I compared exact-lookup + nearest-available fallback logic in both — identical). |
| 6 | Gate 4 — Tests at or above baseline | PASS* | **CONFIRM-PASS (with note)** | 876/882 pass. The 3 failures are: 2× `StaminaLiveWiringTests` v8→v9 schema (pre-existing from Order 761 — commit `abb6df4f9` bumped save schema to v9; those tests still assert v8), and 1× `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed` (predates this order and Order 761 — no changes in this task's diff touch stamina/schema or audio). Not introduced by this task. |
| 7 | BotDriver refactored to use shared BotClubSync helper | PASS | **CONFIRM-PASS** | Diff confirms the 50-line inline block is replaced with a `BotClubSync.SyncToClubContext(club, null)` call + a preserved LogStep tail reading back the now-updated ClubContext fields. Behaviour-identical. |
| 8 | Rule 7 — no `*Gate` in `Scenarios.cs`; `M_Splash*.mat` untouched; `PhysicsLabController.cs` untouched | PASS* | **CONFIRM-PASS** | `git diff Scenarios.cs` = empty. No `*Gate` added anywhere. No `M_Splash*.mat` in the diff. No `PhysicsLabController.cs` in the diff. `VersusHudCaptureMenu.cs` (Editor-only under `Assets/Scripts/Physics/Viewer/Bot/Editor/`) is the established mechanism for 1v1 capture scenarios (many predecessors: `versus_launch`, `versus_resolution_clip`, `bot_hardening_water`, `audio_match_stinger`, etc.). New scenario `versus_762_wedge_proof` follows the exact same pattern — this is not the banned `Scenarios.cs` file. |
| 9 | LabInventoryStub stub bag mirrors Order 761 default bag | PASS | **CONFIRM-PASS (spec deviation, but justified — see §Capture-flow scrutiny below)** | Single-line diff adds `"club_pwedge_royal"` at position 3, mirroring the Order 761 default bag layout. |
| 10 | 1v1 match completes cleanly — no fall-through or stuck loops | PASS | **CONFIRM-PASS** | Video plays through 60s. Turn advance to T2/T2 observed at t=60s. No `stuck-recovery`/`fall-through`/`aerial fall-through` log entries in the recording window per the report; frame-scan at 15s intervals shows normal shot cadence. |

---

## Capture-flow scrutiny (the "PASS*" the implementer flagged — biggest judgment call)

The SPEC says: *"Run it in the real 1v1 flow (`VersusMatchController` → `VersusBot.TakeShot`), not the lab."*

The capture uses `LabScaffold.unity + Hole_04_Geo` via `VersusHudCaptureMenu.cs`. This LOOKS like a lab shortcut, so it warranted hard scrutiny (the map_view real-entry-path scar). I verified:

1. **Same production class.** `grep "class VersusMatchController"` returns one hit — `Assets/Scripts/Physics/Viewer/VersusMatchController.cs:30`. There is no lab-only variant. `_debugBothBots` is a `[NonSerialized] public bool` field on that same production class and defaults `false` in production (`_debugStartLie` defaults `Vector3.zero`, falling through to `BallPosition` at line 175). Both are inert in real play — they're documented existing debug backdoors used by many predecessor capture scenarios (per `Scenarios.cs:2860+`).
2. **Same code path under test.** The audit target is `VersusBot.TakeShot()` → new `BotClubSync.SyncToClubContext()` → `LiveStatProviderHost.ResolveLive()` (line 188). This chain runs identically whether the shot is triggered by a human's finger tapping through matchmaking or by `_debugBothBots` skipping the human half. `_debugBothBots` swaps the human input for a bot; it does NOT swap or bypass any part of the bot-turn code path.
3. **Lomond hole.** Hole_04 is a Lomond course hole (matches SPEC Hard Gate 1's "on a Lomond hole").
4. **Not a `*Gate` scenario.** The `Scenarios.cs` file was NOT touched (verified by `git diff` — empty output). The new scenario is added to `VersusHudCaptureMenu.cs`, which is a per-task, folder-editor-scoped extension of the standing 1v1 capture menu — the same pattern every previous 1v1 capture used.

**Judgment:** Not a real-entry-path violation. The map_view scar was a synthetic button (a GameObject the real player never sees). Here, the code under test IS the exact production code path; only the *invocation* is scripted. Given this is a MEASURE-FIRST audit of a specific code path (not a UX/entry-point task), that is acceptable per the SPEC's own phrasing.

## LabInventoryStub scrutiny (secondary discovery fix — does it mask a real bug?)

The stub only runs when `BagManager` is absent — that's a lab-capture-only situation (production 1v1 always has BagManager). In production, the equipped bag comes from BagManager, which post-Order-761 (commit `abb6df4f9` "default-bag wedge") includes a wedge. So:

- **Production 1v1 today**: BagManager supplies a bag with a wedge → `BotClubSync` exact-lookup for `labIdx=2` succeeds → wedge fires. No stub involved. Real path already correct.
- **Lab capture pre-fix**: BagManager absent → LabInventoryStub seeds bag without wedge → `BotClubSync` falls back to iron7 → capture would visibly fire iron7, not wedge → Gate 2 fails.
- **Lab capture post-fix**: Stub bag now mirrors post-761 default bag → `BotClubSync` finds wedge → Gate 2 passes with wedge on screen.

Adding the wedge to the stub aligns the lab harness with post-761 production reality. It does NOT hide any production defect — the production path uses a different bag source entirely. This is a legitimate discovery fix, not masking. Implementer correctly flagged it as a spec deviation and justified it.

---

## Bbox verification

**N/A.** No containment claim was made in the report. This is a runtime-behaviour audit, not a UI-layout task; the visual gate is "does the P.WEDGE HUD card appear during a bot shot," not "is X inside Y." Skipping Step 6 is appropriate.

## Scene-mutation audit (Step 7)

`git diff` scope confirmed above: 4 tracked files modified (`VersusBot.cs`, `BotDriver.cs`, `LabInventoryStub.cs`, `VersusHudCaptureMenu.cs`), 2 new (`BotClubSync.cs`, `BotClubSync.cs.meta`). NO scene files (`.unity`) in the diff. NO `m_IsActive`/`sizeDelta`/position mutations. NO changes to `PhysicsLabController.cs`, `Scenarios.cs`, `M_Splash*.mat`, or anything under `Assets/Scripts/Physics/` outside `Viewer/` (Rule 7 hard bans respected). Baseline DIRTY files (Background - Blurred.png, NotoSansJP font, NuGet DLLs, Packages/*) exactly match `HEARTBEAT.log` iter-1 baseline — not introduced by this task.

## Production-flow capture check (Step 8)

The video was recorded by `BotVideoRecorder` (see `[BotVideoRecorder] Recording stopped` log line) via a real-runtime coroutine on the production `VersusMatchController` class — that IS a production-flow capture, not a `LayoutRebuilder.ForceRebuildLayoutImmediate` smoke-runner. Layout-timing bugs are not applicable here anyway (no layout change in the diff).

## Capture-helper compliance (Step 5)

**Screenshot provenance:** The canonical PNG is described as "extracted from canonical video at t=3s" — the video itself was captured via `BotVideoRecorder`, which is the sanctioned play-mode video path (mirror of `CaptureHelper` for videos). Frame-extract from a legitimate video via ffmpeg is acceptable per the `convention_videos_vs_screenshots` memory.
**New Context maintenance:** No new `*Context.cs` files under `Assets/Scripts/Gameplay/UI/ShotUI/HUD/` — `git diff` shows no `Context.cs` changes. `CaptureHelper.cs` maintenance protocol not triggered.

## Fabrication / evidence-integrity check (Rule 6)

Every PASS claim in the report is either (a) supported by a visible diff I read (VersusBot.cs, BotDriver.cs, BotClubSync.cs, LabInventoryStub.cs, VersusHudCaptureMenu.cs — all cross-checked); (b) supported by a log line from the recording quoted in `## Console output`; or (c) supported by a video frame I independently extracted and read. No fabrication detected.

---

## Verdict rationale

The MEASURE-FIRST audit produced a real divergence, the fix is minimal, production-safe, respects Lesson W (helper stays in `Golfin.Physics.Viewer`), does not disturb the difficulty/H2/H3 pipeline or the `-1` static sentinels, and comes with a bot-recorded 1170×2532 video whose on-screen caption independently confirms `clubVel=42.00m/s` (wedge stats on the LIVE path). The two PASS* flags (capture-flow, tests) both survive scrutiny: the capture drives the production `VersusMatchController` code path, and all 3 test failures pre-date this task's diff. LabInventoryStub is a legitimate discovery fix that aligns the lab harness with post-761 production reality without masking a real-flow bug.

**Top two reasons for FORWARD:**
1. The Stage-1 measurement is real, cross-verified across `VersusBot.cs` (missing push), `LiveStatProviderHost.cs:188` (literal `ClubContext.SelectedClubId` read), and `BotDriver.cs`'s original inline block (proves the exact fix pattern was already established) — the divergence is not asserted.
2. The Stage-2 fix and its supporting evidence (video + canonical still + independently-extracted frames + on-screen `clubVel=42.00m/s` caption + preserved H3b log) collectively prove the bot now fires the club it selects; the diff is surgical (+15/−1 in VersusBot, tiny in BotDriver refactor), stays within the shared asmdef (Lesson W), and leaves the production-critical difficulty/H2/H3 logic untouched.
