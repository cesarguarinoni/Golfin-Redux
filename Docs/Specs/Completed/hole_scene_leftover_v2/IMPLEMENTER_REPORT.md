# Implementer Report — `hole_scene_leftover_v2`

**Iteration shape:** capture-scene-setup:hole-leftover-resurrection

## Implementation summary

Added `IsHoleGeoScene(string nameOrPath)` static helper to `CaptureSceneSetup` and used it to filter Hole_NN_Geo entries out of `Capture()` (prevents resurrection), `Restore()` (defence against stale pre-fix snapshots), and `CloseStagedHoleScenes()` (de-duplicated inline test). Wired `LoopV2SmokeBotMenu` to `CaptureSceneSetup` for the first time: both `Launch()` and `LaunchDirectLab()` call `Capture(SetupKey)` + arm `CleanupKey` before any OpenScene; the `OnPlayModeStateChanged` handler gains a new `EnteredEditMode` branch gated on its own `CleanupKey` that calls `Restore(SetupKey)` and disarms the flag.

## Files modified or created

| Path | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` | Modified — added `IsHoleGeoScene()` helper, filter in `Capture()` + zero-entries guard, filter in `Restore()`, switched `CloseStagedHoleScenes()` to use the shared helper |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | Modified — added `SetupKey`/`CleanupKey` constants, wired `CaptureSceneSetup.Capture`/`.Restore` in `Launch()`, `LaunchDirectLab()`, and new `EnteredEditMode` branch of `OnPlayModeStateChanged` |
| `Docs/Specs/Active/hole_scene_leftover_v2/HEARTBEAT.log` | Created — iter-1 kickoff baseline + timestamped actions |
| `Docs/Specs/Active/hole_scene_leftover_v2/STATUS.md` | Modified — SPEC_READY → IMPLEMENTER_WORKING |
| `Docs/Specs/Active/hole_scene_leftover_v2/screenshots/gate_test_clean_2026-08-07.png` | Created — 1200×900 scene view, ShellScene only (no hole scenes) |

## Screenshot

- **Canonical screenshot:** `screenshots/gate_test_clean_2026-08-07.png`
- **Captured at:** `screenshots/gate_test_clean_2026-08-07.png` (1200×900)
- **Scene loaded:** `Assets/Scenes/ShellScene.unity` (ShellScene only — Hole_06_Geo absent, confirmed by `scene-get-data` IsDirty=false, RootCount=25, no Hole_NN_Geo root)
- **Play mode:** No (editor tooling task — no visual UI to render)
- **Hole loaded:** N/A

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| **Gate 1 — Resurrection cycle broken:** Hole_06_Geo absent after run 1 and run 2 restores; "Excluding staged hole scene" log on run 1 only | PASS | Console logs from Gate1ResurrectionTest (06:48:21–22): run 1 — `[CaptureSceneSetup] Excluding staged hole scene from snapshot: Assets/.../Hole_06_Geo.unity`; SessionState `{"entries":[{"path":"Assets/Scenes/ShellScene.unity",...}]}` (no hole); `[Gate1] Snapshot excludes Hole_06_Geo: PASS`; `[Gate1] Hole_06_Geo absent after run 1 restore: PASS`. Run 2 — no "Excluding" log (no hole in setup at run-2 Capture); `[Gate1] Run 2 snapshot still excludes Hole_06_Geo: PASS`; `[Gate1] Hole_06_Geo absent after run 2 restore: PASS`. |
| **Gate 2 — LoopV2 hierarchy restore:** user scene (ShellScene) restored after run; cleanup log line present | PASS | Console logs from Gate2Gate3Test (06:50:58–59): `[Gate2] CleanupKey armed: True`; SessionState snapshot = ShellScene only; `[CaptureSceneSetup] Restored pre-run scene setup: ShellScene`; `[LoopV2SmokeBotMenu] Run cleaned up: staged scenes closed, scene setup restored.`; `[Gate2] ShellScene restored alone: PASS`; `[Gate2] No hole scene present: PASS`; `[Gate2] CleanupKey disarmed: PASS`. |
| **Gate 3 — Stale-snapshot defence:** Restore skips a hand-injected Hole_06_Geo entry; no hole reopened | PASS | Console logs from same run: injected stale payload with both ShellScene + Hole_06_Geo entries; `[CaptureSceneSetup] Skipping stale hole scene entry in snapshot: Assets/.../Hole_06_Geo.unity` (CaptureSceneSetup.cs:139); `[CaptureSceneSetup] Restored pre-run scene setup: ShellScene`; `[Gate3] Hole_06_Geo NOT reopened: PASS`; `[Gate3] ShellScene still present: PASS`. |
| **Gate 4 — git status clean:** zero `.unity` diffs | PASS | `git diff --name-only HEAD -- "*.unity"` → empty output. Only dirty paths outside task folder: `M Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` and `M Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` — exactly the spec touch list, no `.unity` files. |
| **Compile clean:** no errors after edits | PASS | `assets-refresh` + `console-get-logs(Error)` → zero errors. Reflection checks: `IsHoleGeoScene found: PASS`, `Launch found: PASS`, `SetupKey/CleanupKey constants found: PASS`. IsCompiling=false confirmed before gate tests. |
| **M_Splash*.mat untouched** | PASS | `git diff --name-only HEAD -- "Assets/Resources/FX/M_Splash*.mat"` → empty output. |
| **No `.unity` scene files written** | PASS | Zero `*.unity` diffs (Gate 4). `ShellScene.IsDirty=false` confirmed via `scene-get-data`. No `EditorSceneManager.SaveScene` called in any modified code path (only the pre-existing `StripSerializedHost` saves, which was untouched). |
| **`LaunchDirectLab` has no callers — compile is its only proof** | PASS | Compile succeeded with zero errors. `LaunchDirectLab` wiring is identical pattern to `Launch()` per SPEC §4.2. Reflection confirmed both methods exist and contain the new constants. |

## Known FAIL items

None.

## Spec deviations

- **`Assets/Scripts/Physics/Viewer/Editor/` edits vs Rule 7 "ZERO edits under `Assets/Scripts/Physics/`":** The SPEC §7 explicitly lists `CaptureSceneSetup.cs` and `LoopV2SmokeBotMenu.cs` as the touch list, and the task dispatch names these same two files as the Tier-2 touch list. The standing ban guards against unauthorized capture/scaffolding additions to Physics runtime code; these are the specific editor-only infrastructure files the spec authorizes. The `git diff HEAD -- Assets/Scripts/Physics/` shows ONLY these two files changed — no runtime files, no Scenarios.cs, no LabScaffold.unity.

## Console output

All gate console log lines are captured in the Acceptance checklist above. Zero errors during the entire session. Zero warnings related to this task (only pre-existing Unity lifecycle warnings unrelated to the change).

```
[CaptureSceneSetup] Excluding staged hole scene from snapshot: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
[CaptureSceneSetup] Snapshot taken (1 scene(s)): ShellScene
[Gate1] Snapshot excludes Hole_06_Geo: PASS
[CaptureSceneSetup] Closing staged hole scene without saving: Hole_06_Geo
[CaptureSceneSetup] Restored pre-run scene setup: ShellScene
[Gate1] Hole_06_Geo absent after run 1 restore: PASS
[Gate1] Run 2 snapshot still excludes Hole_06_Geo: PASS
[Gate1] Hole_06_Geo absent after run 2 restore: PASS
[CaptureSceneSetup] Skipping stale hole scene entry in snapshot: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
[Gate3] Hole_06_Geo NOT reopened: PASS
[LoopV2SmokeBotMenu] Run cleaned up: staged scenes closed, scene setup restored.
```

## Open questions for Architect

None.
