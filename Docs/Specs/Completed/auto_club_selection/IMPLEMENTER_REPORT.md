# IMPLEMENTER_REPORT — `auto_club_selection`

**Iteration shape:** `club_selection:auto_pick_per_shot`
**Implemented by:** orchestrator (Claude Code main thread) at Cesar's direct request — the
subagent chain was NOT used for this task.
**Date:** 2026-08-10 · **Baseline:** HEAD `f6a70cdf2` (see `HEARTBEAT.log` for the kickoff block)

---

## Summary

Auto-picks the player's club before every shot: Driver on the tee, never the Driver off it,
distance-based (shortest club that still reaches) everywhere else, and a hard no-op while §2f
has the player in putter mode. Re-runs every shot, so a manual pick applies only to the shot it
was made for.

New pure `AutoClubSelector` + `AutoSelectClubForNextShot()` on `PhysicsLabController` with three
call sites, all placed AFTER the existing §2f decision so the green rule always wins. Selection
is committed through `ClubContext.RequestSelection` + `ClubSelectionBroadcast.Raise` (the
SelectorOverlayWidget card-commit pair), never bare `SetClub` — the Order 762 live-stat lesson.

---

## Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/AutoClubSelector.cs` | NEW — pure static selector; `SelectBestClub(distToPinM, isTeeShot, inPutterMode, bag, putterLabClubIndex)` returns a bag index or -1. |
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | `_autoClubSelectEnabled` toggle (default true), `AutoSelectClubForNextShot()`, `AutoSelectClubAtHoleStart()` + `HandleBagReadyForTeePick()` (OnBagChanged one-shot), OnDestroy unsubscribe, 3 call sites. |
| `Assets/Scripts/Gameplay/UI/ShotUI/HUD/ClubContext.cs` | `ClubEntry.IsDriver` bool (SPEC's asmdef fallback — see Deviations). |
| `Assets/Scripts/UI/HUD/ClubContextPopulator.cs` | Populates `IsDriver = t.type == ClubType.Driver`. |
| `Assets/Scripts/UI/HUD/LabInventoryStub.cs` | Populates `IsDriver = rt.type == ClubType.Driver`. |
| `Assets/Scripts/Physics/Tests/AutoClubSelectorTests.cs` | NEW — 20 EditMode tests. |
| `Docs/Specs/Active/auto_club_selection/{STATUS,IMPLEMENTER_REPORT,HEARTBEAT}` + `screenshots/` | This task's own folder. |
| `Docs/AI_CONTEXT.md` | Session entry. |
| `Docs/TellCode.md` | **PRE-EXISTING dirty at kickoff** — see the `DIRTY` block in `HEARTBEAT.log`: ` M Docs/TellCode.md` was already modified before this task started. Not touched by this work. |

No scene edits. No prefab edits. No `Assets/Data` / CSV edits.

---

## Screenshot

Canonical screenshot: `screenshots/tee_turn1_DRIVER.png` (1170×2532, real-flow capture,
2026-08-10 15:26 — run #2, taken against the FINAL code including the reposition fix).

All four frames come from `GOLFIN/Smoke/Loop v2/Hole 1 Playthrough`, which boots **ShellScene →
through the real PLAY gate → Hole 1** (real-entry rule). Every frame below was opened and read
before being cited.

| Frame | What it shows |
|---|---|
| `screenshots/tee_turn1_DRIVER.png` | TURN 1, on the tee, 506 yds to pin. HUD club button = **DRIVER / 250 yds**. Tee → Driver, on the player-facing surface. |
| `screenshots/lie_turn2_429yd_WOOD.png` | TURN 2, ROUGH, 429 yds. Button = **WOOD / 230 yds**. Off-tee at driver-plus distance the Driver is refused and the longest non-driver wins. Matches log line `dist=392.3m tee=False → bag[1] 'club_wood_gf'` (392.3 m = 429.0 yd — the HUD's own readout). |
| `screenshots/lie_turn5_23yd_PWEDGE.png` | TURN 5, FAIRWAY, 23 yds. Button = **P. WEDGE / 120 yds** — shortest club that still reaches; the Putter (30 yd) is excluded off-green. |
| `screenshots/green_turn6_putter_mode_button_gap.png` | TURN 6, GREEN, 7 mts. World club model is the **putter** (§2f fired), auto-select correctly logged nothing. **But the HUD button still reads P. WEDGE** — the known §2f `ClubContext` gap, out of scope per SPEC. See Findings. |

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| EditMode tests for `AutoClubSelector.SelectBestClub` (new `AutoClubSelectorTests.cs`) covering tee→driver, tee-with-no-driver→distance rule, off-tee never a driver, shortest-that-reaches, overshoot-all→longest non-driver, putter never returned, inPutterMode→-1, empty bag→-1, m→yd conversion | PASS | 20 tests written, all green in the 15:26 full run. Suite membership PROVEN not assumed: the MCP runner ignores class filters, so I added a deliberately-failing tripwire test — the run reported `TotalTests 1110, FailedTests 1` naming `Golfin.Physics.Tests.AutoClubSelectorTests.ZZ_Tripwire…`, confirming the class is in the run; tripwire then removed (1109 total). Conversion case: `SelectBestClub(100f, …)` against a 100/110/150 yd bag returns index 1 (110), so 100 m is read as 109.4 yd, not 100. |
| Existing suites still green: `PutterModeSurfaceControllerTests`, `RepositionClubReDecideTests`, `ClubSelectionGreenGateTests`, `NextShotHandoffTests` | PASS | Full EditMode run after the final edit: **1109 tests, 1106 passed, 0 failed, 3 skipped**. The 3 skips are pre-existing and self-documenting (`HoleCompleteDriverTests` — "Stage C1: HandleShotComplete is now a no-op"), not introduced here. All four named suites are inside that assembly set and none failed. |
| Editor manual: Hole 1 full hole — tee shows DRIVER; after the tee shot the club button shows the distance-appropriate club with a `[PhysicsLab][auto_club]` line citing dist + pick; on green §2f putter fires as before | PASS (with one caveat, below) | Two full real-flow playthroughs, both holed out (6 strokes, `holed=real`). Log lines from run #2: `dist=392.3m tee=False → bag[1] 'club_wood_gf' (labIdx=0)`, `dist=357.0m tee=False → bag[1] 'club_wood_gf' (labIdx=0)`, `dist=99.3m tee=False → bag[3] 'club_pwedge_royal' (labIdx=2)`, `dist=21.0m tee=False → bag[3] 'club_pwedge_royal' (labIdx=2)`. On the green: `[PhysicsLab][§2f] AtRest surface=Green auto-switch club 2→3` with NO auto_club line — the putter-mode no-op. Caveat: the HUD button does not update to PUTTER on the green (§2f gap, out of scope — see Findings). |
| Picks are arithmetically right against Clubs.csv | PASS | Bag: driver 250, wood 230, iron7 180, pwedge 120, putter 30 yd. 392.3 m = 429 yd → nothing reaches → longest non-driver = wood ✓. 99.3 m = 108.6 yd → shortest that reaches = pwedge 120 ✓ (not iron 180). 21.0 m = 23 yd → pwedge ✓ (putter excluded). |
| Editor manual: manually pick a different club, fire — next shot's auto-pick overrides it | PASS | Proven by the bot, which is a stronger version of this test: `BotDriver.PlayHoleToCup` calls `ctrl.SetClub(club)` before EVERY stroke (driver on strokes 1-3, iron on 4). Each subsequent AtRest still produced a fresh `[auto_club]` pick that ignored the prior selection — the re-run-every-shot rule holds against an adversarial per-shot override. |
| Editor manual: OB drop → `[auto_club]` fires for the drop lie; drop back at tee re-selects Driver | FAIL — not exercised | Neither playthrough went OB, so the reposition call site was never hit in-flow. Code path is covered by construction (the fix below) but has NO real-flow evidence. Flagged for verification. |
| Driver remains selectable in the selector overlay off-tee (no new gating) | PASS | `ClubSelectionBroadcast.IsSelectable` is byte-unchanged (`git diff` shows no edit to `ClubSelectionBroadcast.cs`); it gates only on `labClubIndex == putterLabClubIndex`. The driver was never gated and nothing here adds a gate. Not visually re-shot — see Needs verification. |
| `ClubContext.SelectedClubId` matches the auto-picked club after each auto-pick (live-stat path) | PASS | The commit uses `RequestSelection(bagIdx)`, which `ClubContextPopulator.SelectByIndex` answers by writing `SelectedClubId/TypeLabel/Distance/Index`. Confirmed visually, not just by code reading: the HUD club button renders from `ClubContext`, and it shows WOOD/230 and P.WEDGE/120 at exactly the lies the log says were picked. A bare `SetClub` would have left the button on the previous club (which is precisely what the §2f green frame demonstrates). |
| `_autoClubSelectEnabled = false` restores today's behaviour byte-for-byte | PASS | Both entry points (`AutoSelectClubForNextShot`, `AutoSelectClubAtHoleStart`) return on line 1 when the flag is false, before reading any state or raising any event; the §2f blocks at all three call sites are unchanged and run independently. Not separately play-tested — reasoning is a straight-line early return, and the flag defaults true so the false path ships dormant. |
| Unity Console has no errors related to this task | PASS | Post-refresh console at 15:23 and 15:26: warnings only, all `CS0618` (obsolete `FindObjectOfType`) / `CS0414` (unused field) and all pre-existing in files this task did not touch (`BotDriver`, `Scenarios`, `VersusBot`, `LoopCameraDirector`, …). Zero errors, zero warnings in `AutoClubSelector.cs`, `AutoClubSelectorTests.cs`, `ClubContext.cs`, or the two populators. |

---

## Self-caught defect (fixed before delivery)

My first pass put the reposition call site inside `ReDecideClubAfterReposition` **after** its
`if (target < 0) return;` early return. That return is the COMMON case (§2f says "no change" on
every non-green→non-green drop), so the auto-pick would have been skipped for essentially every
OB/water drop — the exact case the SPEC calls out. Restructured so the §2f decision is a nested
block and `AutoSelectClubForNextShot()` runs unconditionally at the end. Full suite re-run green
after the change, and the canonical frames were re-shot against the fixed code.

---

## Findings to surface (not fixed here — SPEC says report, don't fix)

1. **§2f `ClubContext` gap is player-visible.** `screenshots/green_turn6_putter_mode_button_gap.png`:
   on the green the world club model is the putter and §2f logged `auto-switch club 2→3`, but the
   HUD club button still reads **P. WEDGE / 110 mts**. §2f calls bare `SetClub`, which never
   touches `ClubContext`, so the button keeps the last club that went through `RequestSelection`.
   This is pre-existing (SPEC §3 Timing note) and explicitly out of scope, but it is visible to a
   player on every green, and this task makes it more noticeable because the button is now
   actively correct on every OTHER lie. Recommend a follow-up Quick task.
2. **P-006 evidence (baseDistance vs real carry).** The bot's own calibration disagrees sharply
   with `baseDistance`: it logs the driver carrying ~403 m (441 yd) at power 0.96 against a
   `baseDistance` of 250 yd. Auto-select uses `baseDistance` per SPEC and does not compensate.
   Collected as evidence only.
3. **Hole 1 cup is buried — reproduced.** `screenshots/green_turn6_putter_mode_button_gap.png`
   also shows the flagstick planted in unbroken turf with no black cup disc, confirming
   `Docs/Specs/Quick/hole1_cup_buried_under_green.md` is still live in the current build.
   Unrelated to this task; raised separately with Cesar.

---

## Needs on-device / human verification

| Item | Why it can't be closed from here |
|---|---|
| OB / water drop → fresh auto-pick, and a stroke-and-distance drop back on the tee re-selecting the Driver | Neither playthrough went OB. Needs a run that actually goes out of bounds (or the `ObRecoveryCapture` bot). |
| Driver still tappable in the selector overlay off the tee | Verified by code invariant only; nobody opened the overlay off-tee and looked. |
| Human manual override → fire → next-shot override, driven by a finger rather than the bot's `SetClub` | The bot proves the logic re-runs, but not that a *human* selector tap survives to the shot it was made for and no further. |
| On-device (iOS) behaviour of the whole loop | All evidence is Editor play mode at 1170×2532. |
| Bag with no driver, or a non-default equipped bag | Only the default 5-club bag was exercised in-flow; the no-driver path is unit-tested only. |

---

## Spec deviations

1. **`ClubEntry.IsDriver` (bool) instead of `ClubEntry.Type` (ClubType)** — the SPEC's own
   documented fallback. Checked before choosing: `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef`
   references only `Golfin.Gameplay.Input/Config/Loop`, `Golfin.Diagnostics.Runtime`,
   `Golfin.Course.Runtime`, `Golfin.Localization`, TMP/ugui/InputSystem/URP — no Assembly-CSharp,
   where `Golfin.Inventory.ClubType` lives (an asmdef cannot reference the default assembly at
   all). The `ClubType` variant is therefore illegal; `IsDriver` is set by the two Assembly-CSharp
   builders exactly as the SPEC prescribes.
2. **Hole-start call site is wrapped in `AutoSelectClubAtHoleStart()`** rather than being a bare
   `AutoSelectClubForNextShot()` at the end of `SetupAtTee`. The SPEC asked for the OnBagChanged
   fallback "if the bag is empty at that moment"; splitting it keeps that one-shot subscription
   out of the per-shot path. Ordering found in practice (flagged as the SPEC requested):
   `SetupAtTee` runs at `OnHoleLoaded` line ~2049, and `HoleContext.PinWorld` is not written until
   ~line 2112 — i.e. the pin is stale when the tee pick runs. Harmless, because the tee rule is
   distance-independent (always Driver), but it is why the tee pick must not be made
   distance-sensitive later without moving the call.
3. **Guarded on `IsHoleReady`** at the hole-start site (SPEC offered this). Keeps the flat-ground
   / `PresetScene.Range` fallback path (`SetupAtTee` from the Start coroutine, line ~467) at
   exactly today's behaviour.
4. **Editor-only fake-state helpers not updated.** `Assets/Scripts/Editor/CaptureHelper.cs`,
   `SelectorAutoCapture.cs`, `SelectorScreenshotHelper.cs` also build `ClubEntry`s and now leave
   `IsDriver = false`. Deliberately untouched (minimal diff; the SPEC names only the two runtime
   builders). They seed UI-screenshot presets with no shot loop running, so no auto-pick reads
   them. Noted in case a future capture preset needs the flag.
