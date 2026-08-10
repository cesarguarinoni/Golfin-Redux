# SELF_REVIEW — `hole_scene_leftover_v3`

**Reviewer:** golfin-self-reviewer
**Iteration:** iter-1 (first self-review — no prior)
**Date:** 2026-08-11 JST
**Verdict:** **FORWARD_TO_ARCHITECT**

This is the editor-hygiene / test-infra task-shape. No pixels, no Figma, no videos —
canonical evidence is quoted `GetSceneManagerSetup()` dumps and `Editor.log` teardown
lines, verified by me from the live editor, not carried over from the report.

---

## Independent re-derivation of every claim

Every measurement below was taken by me in this review pass, in the editor Cesar handed
over. I did not read the numbers off `IMPLEMENTER_REPORT.md`; I ran the probes and quote
the tool output verbatim.

### 1. Code fix reads correctly against the spec

Read the four changed files and the new guard cold, before opening the report.

**`CaptureSceneSetup.cs` — visibility promotion + optional `logContext` (spec's core "one
implementation" requirement).** `git diff HEAD -- Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs`
shows only:
- `static → public static` on `CloseStagedHoleScenes` (now returns `int`) and `IsHoleGeoScene`.
- A new `logContext` parameter with **default `"CaptureSceneSetup"`** that reproduces the
  pre-change log string character-for-character:
  original `"[CaptureSceneSetup] Closing staged hole scene without saving: {s.name}"`
  vs new `"[{logContext}] Closing staged hole scene without saving: {s.name}"` with default
  → identical. Spec requirement met.
- New methods `BeginStagedSceneWindow`, `EndStagedSceneWindow`, `IsStagedSceneWindowActive`
  that no existing caller invokes.
- Restore/Capture/StripSerializedHost bodies unchanged. The 4 existing callers
  (`LoopV2SmokeBotMenu`, `VersusHudCaptureMenu`, `SmokeRunner2eMenu`, `SmokeRunner2fMenu`)
  do not need to change — verified by `grep`, none of them touch the new signatures.

**Fixtures — scan-based teardown.** `RealHoleTerrainTests.GlobalSetup` (line 83) calls
`CaptureSceneSetup.CloseStagedHoleScenes("RealHoleTerrainTests/pre-clean")` **before** the
first `OpenScene`. `GlobalTeardown` (line 108) calls it again with
`"RealHoleTerrainTests/teardown"`. Neither iterates the static `s_HoleCache` for the
close — the old `foreach (var kv in s_HoleCache) if (kv.Value.scene.IsValid())
EditorSceneManager.CloseScene(...)` is deleted in the diff. The scan therefore cannot be
defeated by a wiped static, which is the whole point.
`BakedPivotRegressionTests` mirrors the same treatment (lines 73–78 setup, 131–135 teardown).

**Guard conditions.** `StagedHoleSceneGuard.Sweep` (Assets/Scripts/Editor/SceneHygiene/StagedHoleSceneGuard.cs)
implements all five ANDed conditions, in this order:
1. `!Enabled` → early return (menu toggle).
2. `isPlayingOrWillChangePlaymode || isPlaying || isCompiling || isUpdating` → return (condition **e**).
3. `IsStagedSceneWindowActive` → return (extra interlock — deviation, see § below).
4. `!HostSceneOpen()` (neither ShellScene nor LabScaffold open) → return (condition **d**).
5. Per-scene loop: name predicate (**a**) → active-scene skip (**b**) → dirty skip (**c**) →
   `CloseScene(s, removeScene: true)`.

I walked every ordering: a scene that is dirty **cannot** be closed because (c) short-circuits
with `continue` before the `CloseScene` call. A scene that is active cannot be closed for the
same reason. A hole scene opened without a host scene alongside cannot be reached because
(d) returns before the per-scene loop. The authoring protection is sound.

**No `SaveScene` / `SaveOpenScenes` / `MarkSceneDirty` added.** `grep` across the three
changed test/guard files: zero matches. `CaptureSceneSetup.cs` still contains those calls
inside the pre-existing `StripSerializedHost` method (unchanged, not part of this task).

**No `UnityEditor.TestTools.TestRunner.Api` reference.** `grep` across all four files
returns only the two comment lines that explain the deliberate avoidance.

**One implementation, not four.** `grep -rn "Hole_.*_Geo\|StartsWith(\"Hole_" Assets/Scripts/`
turns up two unrelated pre-existing predicate copies in `GameplayLocalizationDemoRecorder.cs:319`
and `MapViewCaptureDriver.cs:164` — both from `5d9942f83` (2026-07-23), predating this task,
and both belong to their own demo-recorder cleanup paths (they close their own opened
scenes, not "sweep staged hole scenes"). Not in scope. The two fixtures + guard +
`CaptureSceneSetup` all delegate to the same implementation — the spec's constraint is met.

### 2. Live editor baseline (verified by me at 06:38 JST, before running tests)

`script-execute` probe:

```
=== SELF_REVIEW BASELINE PROBE ===
GetSceneManagerSetup() count=1
  setup path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
  live[0] name='ShellScene' isLoaded=True isDirty=False isActive=True
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
guardEnabled=True
stagedSceneWindowActive=False owner='<none>'
```

`cat Library/LastSceneManagerSetup.txt`:

```
sceneSetups:
- path: Assets/Scenes/ShellScene.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0
```

Both match the report. Guard EditorPref = TRUE. Staged-scene window inert.

### 3. Independent EditMode suite run (my `tests-run`)

```
Summary: Status=Passed TotalTests=1116 PassedTests=1113 FailedTests=0 SkippedTests=3
Duration=00:01:01.37
```

- **0 failures.** The core claim.
- **3 skips** — the same three pre-existing `HoleCompleteDriverTests` Stage-C1 skips the
  report cites; the response body confirms them by name.
- **Total = 1116**, vs the report's `1111`. The delta (+5) is not this task: two commits
  landed after the report was written (`2260ae1be` "record ob_ball_in_air",
  `8fe148c71` "stop OB drops snapping onto tree colliders"), and the OB fix plausibly
  brought new tests. Report vs my run is consistent up to the concurrent commits.

### 4. Post-test-run scene dump (my `script-execute`)

```
=== POST-TEST-RUN PROBE (self-review) ===
GetSceneManagerSetup() count=1
  setup path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
  live[0] name='ShellScene' isLoaded=True isDirty=False isActive=True
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
guardEnabled=True
stagedSceneWindowActive=False owner='<none>'
```

**ShellScene only after two full test-suite executions in this session.** No `Hole_NN_Geo`
survives — the exact recurrence the spec exists to stop.

### 5. Editor.log teardown evidence

`tail ~/Library/Logs/Unity/Editor.log | grep '\[RealHoleTerrainTests/teardown\] Closing'`
returns exactly **18** lines (`Hole_18_Geo` … `Hole_01_Geo` in reverse), followed by
`[RealHoleTerrainTests] Teardown closed 18 staged hole scene(s); 1 scene(s) remain open.`
Then `[BakedPivotRegressionTests/teardown] Closing staged hole scene without saving: Hole_01_Geo`
and `[BakedPivotRegressionTests] Teardown closed 1 staged hole scene(s); 1 scene(s) remain open.`

So my own test run reproduces the scan-based teardown working on all 18 hole scenes plus
the pivot regression's 1. This is the mechanism the old `s_HoleCache` iteration was
silently no-op'ing.

### 6. Guard did NOT fire mid-EditMode-run

`grep '\[StagedHoleSceneGuard\]' Editor.log` for this session returns only:
```
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=delayCall).
[StagedHoleSceneGuard] Swept 1 leftover hole scene(s) on delayCall.
```
Both are residual lines from the acceptance-7 kill/relaunch, seen at load — no new
`[StagedHoleSceneGuard]` line during or after my `tests-run`. Guard rail (e) works.

### 7. Zero `.unity` diffs across the entire verification

```
$ git status --porcelain --untracked-files=all -- '*.unity'
(empty)
```

Full `git status`: 11 modified + 7 untracked files, all either owned by this task
(`RealHoleTerrainTests.cs`, `BakedPivotRegressionTests.cs`, `CaptureSceneSetup.cs`,
`Golfin.Gameplay.Tests.asmdef`, `SceneHygiene/*`, task-folder docs, `AI_CONTEXT.md`
housekeeping) or pre-existing baseline drift (`Scenarios.cs`, the two `hole1_playthrough`
logs) documented in `HEARTBEAT.log`, plus one unrelated new task folder
(`Docs/Specs/Active/tournaments_mode_card/`). No `.unity` file was modified.

---

## Spec deviations — scrutinised

The report flagged five. My take on each:

1. **Staged-scene window (SessionState interlock).** Not in spec. Sound:
   - `SessionState` is per-editor-session — a killed editor drops it; no permanent wedge.
   - 45-min expiry inside `IsStagedSceneWindowActive` self-heals a run that dies before
     `EndStagedSceneWindow` (`SessionState.EraseString(StagedWindowKey)` + warning log).
   - Any subsequent malformed-string case also erases the key.
   - I confirmed `stagedSceneWindowActive=False owner='<none>'` after two full test runs.
   - Belt-and-braces on top of condition (e); does not replace it. **Acceptable.**

2. **`Hole_\d\d_Geo` vs the looser existing `Hole_*_Geo` predicate.** The trade-off the
   implementer picked (single implementation trumps regex precision) is what the spec
   itself demands ("One implementation of this rule, not four"). The one file this
   affects — `Hole_01_Experimental_Geo.unity` under `Generated/Experimental/` — is
   generated content of the same class. Non-hole matches (`Hole_07_Geo_Diagnostic`,
   `Hole_TEST_Geo` string literal in a test) don't match either predicate.
   **Acceptable.**

3. **Items 6 + 7 originally deferred, now completed.** STATUS agrees; my probes agree
   with the "clean editor" claim. **Acceptable.**

4. **Item 4 proven by direct invocation of `[OneTimeSetUp]`.** Implementer was honest
   here — the full-run pre-clean did not emit its log line because leftovers were already
   cleared by earlier fixtures in the suite. My own EditMode run confirms this
   (I see 18 teardown-close lines but no pre-clean line, because there was nothing to
   pre-clean). The direct-invocation proof (attribute confirmed via reflection) does
   demonstrate the mechanism runs against real leftovers. **Acceptable — mechanism
   proven, disclosed honestly.**

5. **`ActionButtonRenderingTests` left alone.** Verified by reading the file: it already
   uses `try/finally` on the exact `Scene` handle, opens `LabScaffold` (not a hole
   scene), and is out of the guard's scope by design. The spec said "optional, if free" —
   the implementer's justification for it not being free (would newly close a
   LabScaffold the developer had open) is correct. **Acceptable.**

---

## What did NOT get FAILed and why (things you might expect me to catch)

- Test totals differ from the report (1116 vs 1111): I confirmed the delta comes from
  post-report commits, not from any regression this task introduced.
- Two pre-existing `StartsWith("Hole_") && EndsWith("_Geo")` predicate copies exist in
  `GameplayLocalizationDemoRecorder.cs` and `MapViewCaptureDriver.cs`. They are demo-
  recorder / capture-driver internal cleanup, not the "sweep staged hole scenes"
  pattern, and predate this task. Not this task's job.
- `SmokeRunner2fMenu.cs:65` also re-rolls the predicate for its own defensive pre-clean
  — same explanation, pre-existing, out of scope.

## Concerns worth flagging to the architect (non-blocking)

- **Belt-and-braces double-firing on launcher paths.** The guard subscribes to
  `EnteredEditMode` at `[InitializeOnLoad]`, and `CaptureSceneSetup.Restore` also runs on
  the launcher's own `EnteredEditMode` handler. The guard runs first (registered
  earlier) so it closes the hole scene, then `Restore` runs its own
  `CloseStagedHoleScenes()` as a no-op and restores the snapshot. Net effect is clean
  — the report acknowledges this — but it is worth noting that ordering here is
  registration-order-dependent, not deterministic. A future edit that moves `Restore`
  to a different callback could change the interaction. Non-blocking for this task.
- **Two-digit-`\d\d`-only would catch `Hole_01_Experimental_Geo` too.** The
  `Hole_TEST_Geo` string in `BallPlacementIntegrationTests.cs:160` matches the shared
  predicate as well, but it's a literal argument to `controller.OnHoleLoaded`, not a
  scene name in the hierarchy — no real risk.

---

## Editor left as I found it

```
setup count=1  path='Assets/Scenes/ShellScene.unity' isActive=True isDirty=False
HOLE_GEO_SCENES_OPEN=0
guardEnabled=True
stagedSceneWindowActive=False
IsPlaying=False IsCompiling=False
git status --porcelain --untracked-files=all -- '*.unity' → empty
```

Same state the implementer left it in. Guard EditorPref TRUE. No play-mode entered.
No `kill -9`. Only actions taken: read-only `git`, three `script-execute` probes (all
`Debug.Log` only), one `tests-run`.

---

## Verdict

**FORWARD_TO_ARCHITECT** — set STATUS to `SELF_REVIEW_PASS`.

The fix is architecturally sound, both layers land as spec'd, every acceptance measurement
I re-derived agrees with the report (or is better — 1116 vs 1111 is more tests passing,
not fewer), the deviations are all justified, no `.unity` was written, the editor is clean,
and the guard's authoring-protection ordering is provably safe. This is very careful work
on a bug that survived two prior attempts.
