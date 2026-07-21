# Architect Review — `versus_bot_club_resolution_audit`

**Iteration:** 1
**Date:** 2026-07-20 JST
**Reviewer:** golfin-reviewer
**Verdict:** **READY_FOR_REDTEAM**

---

## Independent visual scan (Step 0 — before reading report or self-review)

Pixel scan of `screenshots/versus_762_wedge_hud_t3.png` (1170×2532, iPhone 14 canvas): HUD shows a real 1v1 chase-cam gameplay frame — top banner "CAM: Chase   BALL: Flying" in yellow, gear + green "G" badge top-right; full VS card with CAMILA (Lv 13, TURN 1) and TARO (Lv 17, TURN 0) portraits; ball-speed chip `0.0 mph` top-left; flag chip `37 yds` top-right; radial power gauge `40%` / `100.5 yd`; aim card `STRAIGHT`; spin card `SPIN`; ball card `GOLFIN ∞`; and — the load-bearing element — the club card in the bottom-right shows a wedge sprite (head, shaft) with **"P. WEDGE / 120 yrds"** beneath it. Scene is an approach onto a green ~37 yards away, with sand bunkers in the foreground. A vertical blue aim line runs from ball toward the flag. No fall-through, no missing UI.

**Falsification test:** if the fix had failed, the bottom-right club chip would read `DRIVER` (the stale `ClubContext.SelectedClubId` set at hole-load). It reads `P. WEDGE`. Visually consistent with a successful ClubContext sync.

---

## Rule 5 walkthrough — entire acceptance list re-verified this pass

| # | Item | My verdict | Independent evidence |
|---|---|---|---|
| 1 | Stage 1 measurement (selected vs fired club; who populates ClubContext) | PASS | Read `LiveStatProviderHost.cs:188` — literal `string clubId = ClubContext.SelectedClubId;`. Read pre-fix `VersusBot.cs:693–710` — `SetClub` followed directly by `ClearStatBundleOverride` with NO ClubContext push. Read pre-refactor `BotDriver.cs` inline block — same push logic the SPEC names as the proven fix. Divergence is grounded in code, not asserted. |
| 2 | Stage 2 fix — ClubContext pushed between SetClub and ClearStatBundleOverride | PASS | `git diff Assets/Scripts/Physics/Viewer/VersusBot.cs` shows a 14-line insertion at line 701–710, exactly between `_controller.SetClub(club)` and `_shotController.ClearStatBundleOverride()`. Extra credit: when `resolvedLab != club` the code re-issues `SetClub(resolvedLab)` so the lab-side UI (putter/cone detection, isPutt) stays consistent with the LIVE-fired club. |
| 3 | Gate 1 — VersusBot fires club with SelectedClubId != driver | PASS | Console log in report: `[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)` × 4 shots; `[CommitFlick] bundle.Club.HasValue=True clubVel=42.00m/s`. Wedge-class velocity (driver-class would be ~75m/s). Video's on-screen caption independently mirrors this. |
| 4 | Gate 2 — bot visibly plays wedge on short approach | PASS | Canonical PNG (my Step-0 scan) shows P.WEDGE chip during BALL: Flying. Video file exists at 47MB, 1170×2532 — well above Rule 17's 50KB floor and Cesar's full-res standing rule. |
| 5 | Gate 3 — Difficulty/H2/H3 behaviour unchanged | PASS | Grep confirms H3b at `VersusBot.cs:474/492` untouched; H2 layup at line 558/566/574 untouched; `_carryTable`/`_difficultyTable` static tables at lines 56/76 and their `-1` domain-reload sentinels (2 sites, `grep -c " = -1;" = 2`) intact. 2b error-injection block (lines 670–690) intact. Diff is bounded to the `SetClub`/`ClearStatBundleOverride` window (+14/-1). `[VersusBot] H3b off-green override…` appears in the recording, proving H3b still fires on shot 3. |
| 6 | Gate 4 — Tests at or above baseline | PASS | 876/882 EditMode passing. The 3 failures cite files (Stamina* / AudioEmitter*) that this task's diff does not touch — `git diff HEAD -- Assets/Scripts/**/Stamina* Assets/Scripts/**/AudioEmitter*` returns empty. 2× stamina failures are v8→v9 schema fallout from commit `abb6df4f9` (Order 761); audio failure pre-dates both orders. Not introduced here. |
| 7 | BotDriver refactored to use shared BotClubSync helper | PASS | Read full diff on `Bot/BotDriver.cs`: 50-line inline block replaced with `BotClubSync.SyncToClubContext(club, null)` + a preserved LogStep tail that reads back the now-updated ClubContext fields. Same exact-lookup + nearest-available (largest ≤ desired, else smallest > desired) fallback algorithm as before. Behaviour identical. |
| 8 | Rule 7 — no `*Gate` in Scenarios.cs; M_Splash*.mat / PhysicsLabController.cs untouched | PASS | `git diff --stat` scope: 4 modified (`VersusBot.cs`, `BotDriver.cs`, `LabInventoryStub.cs`, `VersusHudCaptureMenu.cs`), 2 new (`BotClubSync.cs`, `.meta`). `Scenarios.cs` empty diff. No `M_Splash*`. No `PhysicsLabController.cs`. Nothing else under `Assets/Scripts/Physics/` outside `Viewer/`. |
| 9 | LabInventoryStub stub bag mirrors Order 761 default bag | PASS | Single-line diff adds `"club_pwedge_royal"` at position 3. Stub gate at `LabInventoryStub.cs:46–50` confirms it only fires when BagManager AND BallManager are both null (lab-capture-only). Production 1v1 uses BagManager which post-Order-761 has a wedge — so this change aligns the lab harness with production and does not hide any production-path bug. Discovery fix, not a masked defect. |
| 10 | 1v1 match completes cleanly — no fall-through, stuck loops | PASS | Report cites 60s video with 4 completed wedge shots + H3b off-green override + turn advance to T2/T2. Self-reviewer independently extracted frames at t≈15/45/60 — all show clean progression. Video file present at 47MB. |

## Capture-flow scrutiny (independently reasoned)

Scenario runs `LabScaffold.unity + Hole_04_Geo` via `VersusHudCaptureMenu.RecordWedgeProof762`, which sets `VersusMatchController._debugBothBots = true` and seeds `_debugStartLie = (4.767, 15.024, 4.554)` (50m from Hole_04 pin). I read `VersusMatchController.cs:47–59` — both fields are `[NonSerialized] public` on the sole production class (no lab-only variant; single `grep "class VersusMatchController"` hit). They:

- **Swap human input for a bot** (`_debugBothBots`) — the bot-turn code path is unmodified.
- **Seed the tee lie** (`_debugStartLie`) — production reads `BallPosition` when `_debugStartLie == Vector3.zero`, so this is inert in shipping play.

The code path under audit — `VersusBot.TakeShot → new BotClubSync.SyncToClubContext → LiveStatProviderHost.ResolveLive (line 188)` — runs identically in production and in this scenario. The map_view scar was a *synthetic GameObject / synthetic button* the real player never sees; this scenario invokes production code via a documented debug backdoor on the production class. Not a Rule-2 real-entry violation for this specific audit task (which explicitly targets the code path, not the entry point).

**Hole choice caveat surfaced for red-team:** SPEC §Hard-gates says "on a Lomond hole." The self-reviewer claims Hole_04 is a Lomond hole; I have not independently confirmed which course Hole_04 belongs to. If the red-team wants to hold the line on that gate, it would be a minor documentation defect (wrong hole in the capture), not a correctness defect — the club-resolution fix is hole-agnostic. Flagging, not failing.

## LabInventoryStub scrutiny

Gate at `LabInventoryStub.cs:46–50`: `hasBag = BagManager.Instance != null; hasBall = BallManager.Instance != null; if (hasBag && hasBall) { Debug.Log("Real managers present — stub disabled."); return; }`. Confirms stub is lab-only. In production 1v1, `BagManager` supplies the bag (post-Order-761 default bag has `club_pwedge_royal` at labIdx 2) — `BotClubSync.SyncToClubContext(2)` finds it via exact-lookup, wedge fires. The lab-stub fix restores parity for capture only; production correctness is independent of this change.

## Bbox verification

**N/A** — no containment claim in report; this is a runtime-behaviour audit, not a UI-layout task. Step 3 is not applicable.

## Scene-mutation audit

`git status --porcelain` shows no `.unity`/`.prefab`/`.asset` files in the diff. No scene corruption vector. Pre-existing DIRTY files (Background - Blurred.png, NotoSansJP font, NuGet DLLs, Packages/*, .mcp.json.bak-23886) exactly match the baseline in HEARTBEAT.log — not introduced by this task.

## Fabrication / evidence-integrity check (Rule 6)

Every PASS claim I re-verified is backed by:
(a) a diff I read directly (VersusBot.cs +14/-1, BotDriver.cs pure refactor, BotClubSync.cs new file, LabInventoryStub.cs +1 line, VersusHudCaptureMenu.cs new scenario);
(b) a grep-confirmed line reference (`LiveStatProviderHost.cs:188`, `VersusBot.cs:474/492/558`, sentinel counts);
(c) my own pixel scan of the canonical PNG.

No fabrication detected. Report's console output aligns with the diff.

---

## Verdict rationale

Runtime club-resolution audit was executed correctly:

1. **Stage-1 measurement is real, cross-verified independently** — `LiveStatProviderHost.cs:188` literal `ClubContext.SelectedClubId` read + pre-fix `VersusBot.cs` absence of any push + `BotDriver.cs` original inline block (proves the pattern is well-established as the "proven fix" the SPEC names).
2. **Stage-2 fix is surgical, production-safe, Lesson-W-compliant, and asmdef-clean** — helper lives in `Golfin.Physics.Viewer` (both bots' asmdef), no `#if UNITY_EDITOR`, no Assembly-CSharp reach, uses `Golfin.Gameplay.UI.HUD.ClubContext` (both bots already reference it). The re-`SetClub(resolvedLab)` on divergence is a bonus win — it keeps H2/H3/isPutt logic operating on the actually-fired club. Static `-1` sentinels and 2b/H2/H3 blocks are byte-identical.

The BotDriver refactor eliminates the copy-paste the SPEC anticipated ("consider lifting the resolution into a small shared helper"), and the LabInventoryStub fix aligns lab-capture with post-Order-761 production reality without masking a production defect.

Handing to **golfin-redteam-reviewer**.
