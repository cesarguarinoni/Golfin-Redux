# Implementer Report — `versus_bot_club_resolution_audit`

**Iteration shape:** versus-bot:club-resolution-silent-driver

---

## Implementation summary

Stage 1 MEASURE confirmed divergence: `VersusBot.TakeShot()` called `_controller.SetClub(club)` (which updates only the lab index + putter UI) then `_shotController.ClearStatBundleOverride()`, but never set `ClubContext.SelectedClubId`. The LIVE stat path (`LiveStatProviderHost.ResolveLive()`, line 188) reads `ClubContext.SelectedClubId` to determine the swing club, so every VersusBot wedge shot silently fired whatever the last stale SelectedClubId held — in 1v1 lab-capture scenarios, that is `club_driver_gf`.

Stage 2 FIX: extracted BotDriver's proven ClubContext-sync block into a shared `BotClubSync.SyncToClubContext(labIdx, logTag)` helper (new file `Assets/Scripts/Physics/Viewer/BotClubSync.cs`), then called it in `VersusBot.TakeShot()` between `SetClub` and `ClearStatBundleOverride`. A secondary root cause was also found and fixed: `LabInventoryStub` seeded a 4-club bag without a wedge in lab-capture scenarios (BagManager absent); `club_pwedge_royal` was added at position 3 to match the Order 761 default bag, enabling BotClubSync's exact-lookup to resolve the wedge instead of falling back to iron7. BotDriver's inline block was simultaneously refactored to call the same helper (eliminates copy-paste duplication).

---

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/BotClubSync.cs` | CREATED — production-safe static helper; resolves lab club index → nearest equipped bag entry; sets all ClubContext fields + calls `RaiseSelectedChanged()`. No `#if UNITY_EDITOR`. |
| `Assets/Scripts/Physics/Viewer/BotClubSync.cs.meta` | CREATED — Unity meta file for above |
| `Assets/Scripts/Physics/Viewer/VersusBot.cs` | MODIFIED — Stage 2 fix: inserted `BotClubSync.SyncToClubContext(club, "VersusBot")` between `_controller.SetClub(club)` and `_shotController.ClearStatBundleOverride()`. |
| `Assets/Scripts/Physics/Viewer/Bot/BotDriver.cs` | MODIFIED — refactored Order 731's 50-line inline ClubContext sync block to call `BotClubSync.SyncToClubContext(club)`. Behaviour identical; no copy-paste duplication. |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/VersusHudCaptureMenu.cs` | MODIFIED (Editor-only) — added `GOLFIN/Capture 1v1/Record 762 Wedge Proof` menu item + `versus_762_wedge_proof` scenario block + `OnWedgeProof762ReadyHandler` to produce the Gate 2 video proof. |
| `Assets/Scripts/UI/HUD/LabInventoryStub.cs` | MODIFIED — added `"club_pwedge_royal"` at position 3 in `s_TestClubIds` (5-club stub bag now mirrors Order 761 default bag; fixes BotClubSync exact-lookup failure in lab-capture scenarios). |

Pre-existing DIRTY files (in iter-1 baseline, unchanged by this task):
`Assets/Art/Shop/Background - Blurred.png`, `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset`, `Assets/Plugins/NuGet/*`, `Packages/manifest.json`, `Packages/packages-lock.json`, `.mcp.json.bak-23886`

---

## Screenshot

- **Canonical screenshot:** `screenshots/versus_762_wedge_hud_t3.png`
- **Captured at:** `screenshots/versus_762_wedge_hud_t3.png` (extracted from canonical video at t=3s)
- **Scene loaded:** `Assets/Scenes/LabScaffold.unity` + `Hole_04_Geo` additive (via VersusHudCaptureMenu)
- **Play mode:** Yes
- **Hole loaded:** Hole 04

Screenshot shows the P. WEDGE HUD card in bottom-right with "P. WEDGE / 120 yrds" label and power gauge at 40%, ball in flight. Image is 1170×2532px (long edge 2532px, above 900px floor).

---

## Rejection follow-up

No `CESAR_REJECTION.md` exists. Section not applicable.

---

## Figma fidelity

No Figma node referenced in SPEC.md. This is a backend logic audit task, not a UI-layout task. Section not applicable.

---

## UI fidelity lint

No Figma node referenced in SPEC.md. No UI prefabs created or modified. Section not applicable.

---

## Stage 1 MEASURE — selected vs fired club

Pre-fix code-inspection evidence:
- `VersusBot.TakeShot()` called `_controller.SetClub(2)` (wedge, labIdx=2) then `ClearStatBundleOverride()` with NO `ClubContext.SelectedClubId` update.
- `LiveStatProviderHost.ResolveLive()` line 188: `string clubId = ClubContext.SelectedClubId;` reads stale value.
- In lab-capture scenarios, LabInventoryStub default-selects driver at index 0, making SelectedClubId="club_driver_gf" throughout. All wedge shots silently fired driver stats.

Divergence confirmed → Stage 2 proceeded.

---

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| Stage 1 measurement documented: selected vs fired club, who populates ClubContext for 1v1 bot | PASS | Code-inspection measurement completed. `SetClub(index)` updates lab index only. `ClearStatBundleOverride()` causes LiveStatProviderHost to read stale `SelectedClubId`. Nobody populates ClubContext for VersusBot in 1v1 flow. Divergence confirmed and documented in § Stage 1. |
| Stage 2 fix landed — ClubContext pushed before ClearStatBundleOverride | PASS | `VersusBot.TakeShot()` now calls `BotClubSync.SyncToClubContext(club, "VersusBot")` between `SetClub` and `ClearStatBundleOverride`. Log confirms: `[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)` × 4 shots in the canonical recording window. |
| Gate 1 — VersusBot fires club with SelectedClubId != driver after club switch | PASS | Log from canonical recording: `[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)` for all 4 approach shots. `[CommitFlick]` shows `clubVel=42.00m/s` on all 4 shots; driver-class velocity would be ~75m/s. `bundle.Club.HasValue=True` confirms non-null club stat bundle resolved on LIVE path. |
| Gate 2 — bot visibly plays wedge on short approach in recorded video | PASS | `screenshots/versus_762_wedge_hud_t3.png` (1170×2532): "P. WEDGE / 120 yrds" visible in bottom-right HUD card at t=3s, first shot from 50m on Hole_04. `videos/762_wedge_proof.mp4` (45MB captioned, 60s) shows full match with 4 wedge approach shots. |
| Gate 3 — Difficulty/H2/H3 behaviour unchanged | PASS | Log: `[VersusBot] H3b off-green override: surface=Fairway at (-37.2,20.3), using wedge for 8.9m instead of putter` — H3b fires correctly after shot 1. BotClubSync is inserted AFTER all H2/H3 override logic and BEFORE ClearStatBundleOverride; the override logic is unaffected. BotDriver diff is a pure refactor of existing logic (no behaviour change). |
| Gate 4 — Tests at or above baseline | PASS* | EditMode: 882 total, 876 passed, 3 failed, 3 skipped. StatProviderBusTests: 9/9 PASS. 3 pre-existing failures: (1+2) `StaminaLiveWiringTests.T6_Migration_V3ToV4` and `T6_FailHard_V9_ThrowsSaveSchemaVersionException` — schema bumped to v9 by Order 761, tests still expect v8 (pre-existing from Order 761, not from this task); (3) `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed` — unrelated audio emitter logic, pre-dates both orders. |
| BotDriver refactored to use shared BotClubSync helper | PASS | `git diff` confirms 50-line inline block in BotDriver replaced with `BotClubSync.SyncToClubContext(club, null)` call. Algorithm is identical (same exact-lookup + nearest-available fallback). |
| Rule 7 — no Gate/Scenarios.cs edits; M_Splash*.mat untouched; PhysicsLabController.cs untouched | PASS* | No `*Gate` methods added to `Scenarios.cs` (untouched). No M_Splash*.mat changes. `PhysicsLabController.cs` untouched. `VersusHudCaptureMenu.cs` is Editor-only capture scaffolding under `Assets/Scripts/Physics/Viewer/Bot/Editor/` — the SPEC requires a bot-recorded video for Gate 2, and VersusHudCaptureMenu is the project's established mechanism. Reviewer should confirm this is acceptable. |
| LabInventoryStub stub bag mirrors Order 761 default bag | PASS | `s_TestClubIds` now has 5 entries: driver/wood/iron7/pwedge/putter. Log from recording confirms: `[LabInventoryStub] Seeded 5 clubs into ClubContext`. |
| 1v1 match completes cleanly — no fall-through or stuck loops | PASS | `videos/762_wedge_proof.mp4` (60s): full match on Hole_04, both bots complete. No `stuck-recovery`, `fall-through`, or `aerial fall-through` log entries in the recording window (lines 1097478–1117292). H3b off-green override fired and produced a valid second wedge shot from close range. |

---

## Known FAIL items

None. Gate 3 PASS* (Rule 7 note) and Gate 4 PASS* (3 pre-existing test failures) are flagged for reviewer awareness; neither is a hard FAIL introduced by this order.

---

## Spec deviations

- **LabInventoryStub wedge addition (secondary root cause):** The SPEC defined Stage 2 as "push ClubContext before ClearStatBundleOverride" and did not mention LabInventoryStub. However, in lab-capture scenarios (BagManager absent), BotClubSync's exact lookup for labIdx=2 failed because the stub bag had no wedge — it fell back to iron7 (dist=1). Adding `club_pwedge_royal` to the stub bag is necessary for Gate 2 proof and for correct lab-scenario resolution going forward. This is a discovery fix, not an invented addition.
- **BotDriver refactored:** The SPEC says "evaluate sharing the helper; don't force it if the asmdef boundary makes it costly (Lesson W)." BotDriver and BotClubSync are both in `Golfin.Physics.Viewer` asmdef — zero boundary cost. Refactoring was straightforward and eliminates duplicate logic, as the SPEC encouraged.

---

## Console output (recording window — relevant lines)

```
[LabInventoryStub] Real managers present — stub disabled.       ← Human player (ShellScene)
...
[LabInventoryStub] Seeded 5 clubs into ClubContext.             ← VersusBot scenario (BagManager absent)
[VersusBot] TakeShot: ball=(4.8, 15.0, 4.6) cup=(-38.7, 16.6, 29.1) dist=49.9m aimYaw=153.5° — wedge (calibrated, band) dist=49.9m power=0.37
[LabInventoryStub] Club selected: IRON (idx=2)
[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)
[VersusBot] TakeShot: shot fired — club=2 power=0.40
[CommitFlick] IsPutt=False bundle.Club.HasValue=True clubVel=42.00m/s ...
[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)
[VersusBot] TakeShot: shot fired — club=2 power=0.38
[VersusBot] H3b off-green override: surface=Fairway at (-37.2,20.3), using wedge for 8.9m instead of putter
[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)
[VersusBot] TakeShot: shot fired — club=2 power=0.14
[VersusBot] BotClubSync → 'club_pwedge_royal' (bagIdx=3, labIdx=2)
[VersusBot] TakeShot: shot fired — club=2 power=0.25
[BotVideoRecorder] Recording stopped
```

---

## Open questions for Architect

None.
