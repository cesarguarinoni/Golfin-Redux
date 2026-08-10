# SPEC — `hole_scene_leftover_v3`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md` (starts at `SPEC_READY`).

## Read this first — why v1 and v2 did not fix it

Cesar reported on 2026-08-10 that `Hole_06_Geo` was reintroduced into the editor hierarchy **twice
that day, unprompted, and left there** — after both the Architect and Claude Code had assured him
`hole_scene_leftover_v2` (K16, `a6b022642` + `1372da34b`) had closed this out.

**Both previous attempts scoped the problem to the capture launchers. That was the wrong scope.**
The dominant resurrection vector is the **EditMode test suite**, which no version of this task has
ever touched — and which runs far more often than any capture launcher, because every task's
acceptance gate runs the full ~1100-test suite.

Do not re-scope this task to launchers. The launchers are already handled; the tests are not.

### Evidence gathered 2026-08-10 (Architect, from the live machine)

1. **`Library/LastSceneManagerSetup.txt.bak`** (Aug 3) records the leak verbatim:
   `ShellScene.unity` (active) **+ `Hole_06_Geo.unity`** (loaded, not active). That file is what Unity
   reopens on launch, so once the leak lands there it survives editor restarts.
   The live `LastSceneManagerSetup.txt` (Aug 10 16:21) is **clean** — ShellScene only — so the
   hierarchy is currently fine; this task is about stopping the recurrence, not repairing a leftover.

2. **`~/Library/Logs/Unity/Editor.log`, last 700k lines**: every hole `Hole_01_Geo` … `Hole_18_Geo`
   was opened **additively exactly 2×**. That is two full EditMode suite runs — matching Cesar's
   "2 times today". `Hole_06_Geo` appears **3×**: the two sweeps plus one extra.

3. The **extra** `Hole_06_Geo` open (log line ~9276054) is immediately preceded by
   `Loaded scene 'Assets/Scenes/Physics/LabScaffold.unity'` — the capture-launcher signature
   (LabScaffold single + hole additive).

4. **The proof that the test sweep does not clean up.** Log line ~9296637 is a sweep opening
   `Hole_05_Geo` then `Hole_06_Geo`. ~280 lines later, at ~9296917:
   `[CaptureSceneSetup] Excluding staged hole scene from snapshot: …/Hole_06_Geo.unity`
   — i.e. when a *protected* launcher subsequently called `Capture()`, **`Hole_06_Geo` was still
   open in the hierarchy from the test sweep.** `CaptureSceneSetup` behaved correctly; it was
   cleaning up after a mess it did not make.

5. **Coverage audit.** 26 files reference a hole-geo scene path. Exactly **4** call
   `CaptureSceneSetup`: `LoopV2SmokeBotMenu`, `VersusHudCaptureMenu`, `SmokeRunner2eMenu`,
   `SmokeRunner2fMenu`. Those are precisely K16's scope. Everything else is unprotected, including
   every test fixture and every `*DemoRecorder`.

## Goal

`Hole_NN_Geo` never survives into the editor hierarchy unless a human deliberately opened it to
author it. Two layers: fix the actual leaking fixtures, and add a guard that catches every present
and future vector without ever touching authoring work.

## Root cause (Layer 1 target)

`Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs`:

- 18 `[TestCase("Hole_01")]`…`[TestCase("Hole_18")]` on
  `AllImportedHoles_Smoke_TeeShot_DoesNotFallThrough` (:419-437) each call `EnsureHoleLoaded(holeId)`.
- `EnsureHoleLoaded` (:100-145) calls
  `EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)` at **:131** — into the LIVE
  editor hierarchy — and caches the `Scene` in
  `static readonly Dictionary<string, HoleProviders> s_HoleCache` (:60-61).
- Cleanup is `[OneTimeTearDown] static GlobalTeardown()` (:85-91), which closes **only what is still
  in `s_HoleCache`**.

**The defect:** `s_HoleCache` is a plain static with no reload-durable backing. Any domain reload
between an `OpenScene` and the teardown (script compile, assembly reload) clears the dictionary
**while the opened scenes remain in the hierarchy** — the teardown then iterates an empty dictionary,
closes nothing, and reports success. A cancelled or crashed run skips `OneTimeTearDown` entirely,
with the same result. In both cases the staged scenes silently *become* the editor hierarchy, and
Unity persists them to `LastSceneManagerSetup.txt`.

Same pattern, same risk, smaller blast radius:
- `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` — `s_HoleScene` static, opens
  `Hole_01_Geo` additively at **:89**, teardown at **:111-118**.
- `Assets/Scripts/Gameplay/Tests/ActionButtonRenderingTests.cs` — opens `LabScaffold` additively at
  **:36**, closes at **:63**. Not a hole scene, so out of scope for the guard, but fix the same way
  if it is free to do so.

## Implementation

### Layer 1 — make test-fixture cleanup reload-proof (the actual fix)

Do **not** delete the 18-hole sweep; that coverage is valuable. Make its cleanup not depend on a
static surviving a domain reload.

In `RealHoleTerrainTests`:

1. **Pre-clean in `[OneTimeSetUp]`.** Before the first `OpenScene`, close every already-open
   `Hole_NN_Geo` scene (without saving). This retroactively sweeps a leftover from a previous
   aborted run, so the leak self-heals on the next suite run rather than persisting for days.
2. **Scan-based `[OneTimeTearDown]`.** Replace the `s_HoleCache` iteration with a scan over
   `SceneManager.sceneCount` / `GetSceneAt(i)` closing every `Hole_NN_Geo`, then clear the cache.
   Scanning is idempotent and cannot be defeated by a wiped static. Keep the existing summary-file
   write after the close loop.
3. Close with `EditorSceneManager.CloseScene(scene, removeScene: true)` and **never** save —
   `Hole_NN_Geo` is generated content with no merge driver.

Apply the same scan-based pre-clean + teardown to `BakedPivotRegressionTests`.

Factor the shared "close every open `Hole_NN_Geo`" helper into one place rather than copy-pasting it
a third time. `CaptureSceneSetup` already has a private `CloseStagedHoleScenes()` and a private
`IsHoleGeoScene()` — promote those to a small shared editor utility (or make them `public static` on
`CaptureSceneSetup`) and have the fixtures and the guard call it. **One implementation of this rule,
not four.** Watch the asmdef: `CaptureSceneSetup` lives in `Golfin.Physics.Viewer.Editor`; if the
test asmdefs cannot reach it, put the helper somewhere both can and have `CaptureSceneSetup`
delegate to it — do not duplicate the predicate.

### Layer 2 — always-on staged-hole guard (the safety net)

New file, editor-only, e.g. `Assets/Scripts/Editor/SceneHygiene/StagedHoleSceneGuard.cs`.

`[InitializeOnLoad]`, hooked to:
- `EditorApplication.playModeStateChanged` → `PlayModeStateChange.EnteredEditMode`
  (covers every capture launcher, protected or not, including ones that die before their own restore),
- `AssemblyReloadEvents.afterAssemblyReload` (covers the post-test-run compile and editor restart),
- `EditorApplication.delayCall` once on load.

A scene is closed **only when ALL of these hold** — this is the authoring protection, and it is the
load-bearing part of the design:

| # | Condition | Why |
|---|---|---|
| a | name matches `Hole_\d\d_Geo` | only generated hole geometry |
| b | it is **not** the active scene | authoring a hole makes it active |
| c | `!scene.isDirty` | never destroy unsaved work |
| d | `ShellScene` **or** `LabScaffold` is also open | the staged signature; authoring alone won't match |
| e | `!isPlaying && !isCompiling && !isUpdatingOrCompiling` | never fire mid-run |

Never saves. Logs every close with the scene name and which hook fired.

Also ship:
- `GOLFIN > Scene Hygiene > Close Staged Hole Scenes Now` — manual sweep.
- `GOLFIN > Scene Hygiene > Guard Enabled` — checked menu item backed by `EditorPrefs`, default ON,
  so a deliberate authoring session can switch it off.

**Do NOT reference `UnityEditor.TestTools.TestRunner.Api`.** Hooking `TestRunnerApi` would look
tidier but drags a test-framework asmdef reference into an editor assembly that does not have one —
a compile break for a marginal gain. The `afterAssemblyReload` hook covers the test vector, and
Layer 1 is the real fix for it.

**Guard rail:** the guard must never fire while an EditMode run is in progress, or it will close the
sweep's scenes mid-run and fail the tests. Condition (e) plus the choice of hooks is what prevents
that — verify it explicitly (acceptance test 2).

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Mark each `PASS`/`FAIL` with the measurement quoted. **This is the third attempt at this bug; a
report that asserts cleanliness without quoting a `GetSceneManagerSetup()` dump will be rejected.**

- [ ] **Full EditMode suite run #1** completes; immediately after, dump `EditorSceneManager.GetSceneManagerSetup()` and quote it — contains **no** `Hole_NN_Geo`. Quote the test totals too (baseline is 1109 / 1106 pass / 0 fail / 3 pre-existing skips).
- [ ] **Full EditMode suite run #2, back to back**, same dump, same result. Two runs is the reproduction Cesar actually saw.
- [ ] **Mid-run guard safety:** the 18-hole sweep passes with the guard enabled — the guard did not close a scene the sweep was still using. Cite the sweep's per-hole results, not just the total.
- [ ] **Interrupted-run recovery:** leave a `Hole_NN_Geo` open by hand (or abort a run mid-sweep), then start a suite run — the `[OneTimeSetUp]` pre-clean removes it. Quote the log line.
- [ ] **🔴 Authoring protection (the one that must not regress):** open `Hole_06_Geo` **alone as the active scene**, dirty it with a trivial edit, force a domain reload — the guard does **NOT** close it and does **NOT** save it. Then repeat with it clean and non-active alongside ShellScene — the guard **does** close it. Both directions reported.
- [ ] **Launcher path still clean:** run one capture launcher (VersusHud or LoopV2 direct-lab), exit play mode, dump the setup — clean, and `CaptureSceneSetup`'s existing behaviour is unchanged.
- [ ] **Killed-editor case:** stage a launcher run, kill Unity mid-run (`kill -9`), relaunch — the guard sweeps the leftover at load. Quote the log line.
- [ ] `Library/LastSceneManagerSetup.txt` after all of the above contains **ShellScene only**. `cat` it into the report.
- [ ] Zero `.unity` diffs in `git status` across the entire verification — no hole scene was ever saved.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations flagged at the bottom of the report with justification.

## Files this task touches

- `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` — pre-clean + scan-based teardown.
- `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` — same treatment.
- `Assets/Scripts/Editor/SceneHygiene/StagedHoleSceneGuard.cs` — NEW, the guard + menu items.
- `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` — expose/delegate the shared
  `IsHoleGeoScene` + `CloseStagedHoleScenes` helper; **behaviour unchanged**.
- (optional, if free) `Assets/Scripts/Gameplay/Tests/ActionButtonRenderingTests.cs`.

## Out of scope (do NOT do these)

- Re-importing any hole, or touching `HoleGeoImporter` — a shipped hole is repaired in place, never
  re-imported (Hole 1 / 1362 trees lesson, `da62daf86`).
- Deleting or shrinking the 18-hole sweep to dodge the problem. The coverage stays.
- Changing what `CaptureSceneSetup` does for the 4 launchers that already call it — it works; this
  task only extends its helpers.
- Wiring `TestRunnerApi` (see Layer 2 rationale).
- Scene, prefab or CSV edits. Any `.unity` diff is a failure of this task.
- The unrelated pre-existing finding that the 2e OB capture no longer reaches OB.
