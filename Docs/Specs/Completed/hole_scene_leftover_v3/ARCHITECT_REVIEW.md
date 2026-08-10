# ARCHITECT_REVIEW — `hole_scene_leftover_v3`

**Reviewer:** golfin-reviewer (Opus 4.7, main-thread architect gate)
**Iteration:** iter-1
**Date:** 2026-08-11 06:53 JST
**Verdict:** **READY_FOR_REDTEAM** (this reviewer's PASS — the red-team gate is the only agent that may advance to `ARCHITECT_REVIEW_PASS`)

This is the editor-hygiene / test-infra task-shape (no pixels, no Figma, no video). Canonical evidence is quoted `EditorSceneManager.GetSceneManagerSetup()` dumps and `Editor.log` teardown lines, verified by me in this pass. Rules 14, 16, 17, 18, 19, 21 (screenshot / mesh / video / Figma / clone-provenance / UI-lint gates) DO NOT APPLY — do not FAIL on their absence, as directed by the review brief.

---

## Independent re-derivation (my probes, not the report's numbers)

### 1. Code diff cold-read (before opening the report)

`git diff HEAD -- Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` shows exactly the promised surface:

- `static → public static` on `CloseStagedHoleScenes` (now `int`-returning) and `IsHoleGeoScene`.
- New optional `logContext` parameter defaulting to `"CaptureSceneSetup"` — the pre-change format string `"[CaptureSceneSetup] Closing staged hole scene without saving: {s.name}"` is reproduced character-for-character when the default fires. Existing 4 launchers (`LoopV2SmokeBotMenu`, `VersusHudCaptureMenu`, `SmokeRunner2eMenu`, `SmokeRunner2fMenu`) still call `Capture(SetupKey)` / `Restore(SetupKey)` unchanged (`grep` confirms none touch the new signatures).
- New `BeginStagedSceneWindow` / `EndStagedSceneWindow` / `IsStagedSceneWindowActive` — no existing caller invokes them; only the two fixtures do.
- `Capture` / `Restore` / `StripSerializedHost` bodies unchanged.

Both fixtures now call `CaptureSceneSetup.CloseStagedHoleScenes(<label>)` in setup (pre-clean) AND teardown (scan-based), and the old `s_HoleCache`-iterating close loop is deleted from both. Neither ever calls `SaveScene` / `MarkSceneDirty` — `grep` returns zero matches.

`StagedHoleSceneGuard.Sweep` implements the five ANDed conditions in the exact order the spec names. I walked every path: a dirty scene short-circuits at (c) with `continue` before `CloseScene`; an active scene at (b); a hole scene with no host scene alongside never reaches the per-scene loop because (d) returns early. The authoring-protection guarantee holds by construction, not by testing.

No `UnityEditor.TestTools.TestRunner.Api` reference anywhere — `grep` across all four files returns only comment lines documenting the deliberate avoidance.

### 2. Live baseline (my probe, 06:46:23 JST)

```
=== ARCHITECT_REVIEW BASELINE PROBE (2026-08-11T06:46:23) ===
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
  live[0] name='ShellScene' isLoaded=True isDirty=False isActive=True isHoleGeo=False
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
guardEnabled=True
stagedSceneWindowActive=False owner='<none>'
isPlaying=False isCompiling=False
CaptureSceneSetup assembly='Golfin.Physics.Viewer'
StagedHoleSceneGuard assembly='Assembly-CSharp-Editor'
```

Assemblies land where the report claims. Guard EditorPref TRUE. Staged-scene window inert.

### 3. Independent EditMode run

```
Summary: Status=Failed TotalTests=1116 PassedTests=1112 FailedTests=1 SkippedTests=3 Duration=00:00:37.69
Failing:  Golfin.UI.Tests.GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav  (0.001s)
Skipped:  3× Golfin.Physics.Tests.HoleCompleteDriverTests (Stage-C1, pre-existing)
```

**Total = 1116 matches the self-reviewer's independent run.** The +5 vs the report's 1111 is the two concurrent commits landing after submission (`2260ae1be`, `8fe148c71`) — not this task.

**One failure — `GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav` — is the same test the implementer's control run (with all task changes stashed) also flagged as failing.** I re-ran it in isolation immediately: `Status=Passed`. This is a documented flake, not a regression from this task. Report's Failure Attribution table is honest.

### 4. Post-run scene dump (my probe, 06:47:57 JST)

```
=== ARCHITECT_REVIEW POST-TEST-RUN PROBE ===
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
  live[0] name='ShellScene' isLoaded=True isDirty=False isHoleGeo=False
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
guardEnabled=True
stagedSceneWindowActive=False owner='<none>'
```

**Zero `Hole_NN_Geo` in the live hierarchy after the full suite ran.** The exact recurrence the task exists to stop.

### 5. Editor.log teardown evidence from MY run

```
[RealHoleTerrainTests/teardown] Closing staged hole scene without saving: Hole_18_Geo
[RealHoleTerrainTests/teardown] Closing staged hole scene without saving: Hole_17_Geo
… (16 more, Hole_18 through Hole_01) …
[RealHoleTerrainTests] Teardown closed 18 staged hole scene(s); 1 scene(s) remain open.
[BakedPivotRegressionTests/teardown] Closing staged hole scene without saving: Hole_01_Geo
[BakedPivotRegressionTests] Teardown closed 1 staged hole scene(s); 1 scene(s) remain open.
```

18 + 1 explicit `CloseScene` calls per test run. The scan-based teardown is doing exactly what the old `s_HoleCache` iteration was silently no-op'ing.

### 6. Guard did NOT fire mid-EditMode-run

`grep '\[StagedHoleSceneGuard\]' Editor.log` for the current session returns only the residual `Closing leftover staged hole scene without saving: Hole_06_Geo (hook=delayCall)` + `Swept 1 leftover hole scene(s) on delayCall` from the acceptance-7 kill/relaunch. No new guard line during or after my `tests-run`. Condition (e) `!isPlaying && !isCompiling && !isUpdatingOrCompiling` plus the staged-scene window interlock worked — the 18-hole sweep completed without the guard closing anything under it.

### 7. Zero `.unity` diffs

```
$ git status --porcelain --untracked-files=all -- '*.unity'
(empty)
```

Across two full suite runs, four authoring-protection branches, a kill/relaunch, and my independent EditMode run. No hole scene was ever saved.

### 8. Cross-cutting asmdef audit

`Golfin.Gameplay.Tests` added a reference to `Golfin.Physics.Viewer`. The test asmdef is `"includePlatforms": ["Editor"]` (verified), so the reference is Editor-only and cannot leak into iOS/Android player builds. `Golfin.Physics.Viewer` is all-platform but its `Editor/CaptureSceneSetup.cs` is wrapped in `#if UNITY_EDITOR` (line 1) — an editor-only-seam pattern that predates this task and is unchanged by it. `StagedHoleSceneGuard.cs` lives in `Assembly-CSharp-Editor` (auto-referenced) and is itself `#if UNITY_EDITOR`. **No new player-build risk introduced.** The standing "editor-only seams in runtime asmdefs break iOS player builds" scar (memory `project_editor_only_seams_break_player_builds`) is not touched — the pattern was already there.

**Player-side call-site check:** every new caller of the promoted `CloseStagedHoleScenes` / `IsHoleGeoScene` / `BeginStagedSceneWindow` / `EndStagedSceneWindow` / `IsStagedSceneWindowActive` (`grep -rn "CaptureSceneSetup" Assets/Scripts`) lives under `Assets/Scripts/Gameplay/Tests/` (Editor-only asmdef) or `Assets/Scripts/Editor/SceneHygiene/` (Assembly-CSharp-Editor). No runtime asmdef pulls them in. Safe.

### 9. Predicate duplication audit

`grep -rn "StartsWith(\"Hole_\")" Assets/Scripts --include="*.cs"` returns 23 matches. Of those, only three fill the *"sweep-and-close staged hole scenes"* role that the spec's "one implementation" mandate targets, and all three have been consolidated onto `CaptureSceneSetup.CloseStagedHoleScenes` / `CaptureSceneSetup.IsHoleGeoScene` (fixtures + guard, verified above). The remaining 20 hits are unrelated pre-existing predicates for very different jobs — MapView flag/hole GO lookup, WaterShore / BridgeExporter / TreePlacer / TeeSkirt filename parsing, `PhysicsLabHolePicker.RestoreHole` "am I already loaded?" check, `LabHoleBinder`'s public helper, `BakeZoneJsonTool` scene-path resolver, `SurfaceRolloutMenu` / `SmokeRunnerPutterConeMenu` scene enumeration, etc. **Reasonable scope call.** The self-reviewer's read that `GameplayLocalizationDemoRecorder.cs` and `MapViewCaptureDriver.cs` are demo-recorder internal cleanup, not the sweep pattern, holds — they close their own opened scenes, not "any leftover hole scene." The `SmokeRunner2fMenu.cs:65` re-roll is a defensive pre-clean predating this task and its close-loop already calls the shared helper; leaving the local predicate alone was correct.

### 10. Deviations — architect judgment

1. **Staged-scene window (SessionState, not in spec).** Sound addition. Belt-and-braces on top of condition (e); does not replace it. `SessionState`-scoped (killed editor drops it → no permanent wedge). 45-minute expiry inside `IsStagedSceneWindowActive` self-heals a run that dies before `EndStagedSceneWindow`. I confirmed `stagedSceneWindowActive=False` at both baseline and post-test-run. **Not scope creep — it converts a spec guard rail ("the guard must never fire while an EditMode run is in progress") from probabilistic into explicit interlock, at low cost.** Accept.
2. **Reused `Hole_*_Geo` predicate rather than `Hole_\d\d_Geo` regex.** The one file this affects (`Hole_01_Experimental_Geo.unity` under `Generated/Experimental/`) is generated content of the same class. The spec's "one implementation, not four" mandate trumps the regex precision. Accept.
3. **Items 6 + 7 completed 2026-08-11** — self-reviewer confirmed, my probes agree. Accept.
4. **Item 4 proven by direct-invocation of the real `[OneTimeSetUp]`** rather than by a full-run log line. Implementer was honest — the full-run pre-clean did not emit its log line because leftovers were already cleared by earlier fixtures. Reflection-based attribute confirmation makes the direct call provably the method NUnit invokes. Mechanism is proven. Accept.
5. **`ActionButtonRenderingTests` left alone.** Spec was "optional, if free." Fixture opens `LabScaffold` (not a hole scene) with a proper `try/finally` on the exact `Scene` handle. Converting it would newly close a `LabScaffold` the developer had open — a behaviour change, not a fix. Accept. **(See Concerns below for a secondary finding on this fixture's side-effects.)**

---

## Concerns to surface for the red-team (not FAIL, but worth adversarial scrutiny)

### C1 — `LastSceneManagerSetup.txt` was rewritten mid-my-review to `Hole_06_Geo` ALONE, active

The file's live contents right now (06:47:21 stamp, written *during* my `tests-run`) is:

```
sceneSetups:
- path: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0
```

**The live editor is clean** (my probe at 06:52 confirms `Setup count=1 ShellScene`, `HOLE_GEO_SCENES_OPEN=0`) — Unity persisted this file at an intermediate moment during the run, and the current in-memory state has moved on. A normal shutdown will rewrite the file with the live state. But if Unity is killed here, the next launch reopens ONLY `Hole_06_Geo`, and **the guard would preserve it as "authoring"** because condition (d) `HostSceneOpen()` fails (no ShellScene, no LabScaffold).

Root cause trace from Editor.log line 31765: `ActionButtonRenderingTests` (or the same test slot) opens `LabScaffold` → `PhysicsLabAutoRestore.OnSceneOpened` (Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs:22-33) fires → `RestoreHole(6)` opens `Hole_06_Geo` additively → `[PhysicsLab] Auto-restored Hole 06` (line 31772). The fixture's `try/finally` closes the `LabScaffold` handle it owns but NOT the auto-restored `Hole_06_Geo` scene it never opened. Some later fixture reloads ShellScene in Single mode, cleaning the live state — but Unity had already persisted an intermediate frame.

**Why this is NOT a FAIL of this task:** The spec's Layer 1 target was the 18-hole sweep in `RealHoleTerrainTests` + `BakedPivotRegressionTests`. Both are fixed. The spec's Layer 2 guard was designed to catch the "both scenes present" leftover shape (which matches the 2026-08-03 `.bak` file Cesar's report cited) — that shape IS caught. The shape I observed (`Hole_06_Geo` alone, no host) is a different vector rooted in `PhysicsLabAutoRestore`, which is out of the spec's scope and cannot be caught by the guard's authoring-protection design (a hole scene alone LOOKS like authoring). Requires kill-9 to matter in practice.

**Why the red-team should still see this:** it's a residual leak vector into `LastSceneManagerSetup.txt` that could show the same symptom Cesar reported, via a different route than the one the spec identified. A queued follow-up task could add cleanup to `PhysicsLabAutoRestore.RestoreHole` (close the auto-restored scene when its LabScaffold parent is closed) or teach `ActionButtonRenderingTests` to sweep hole scenes in its `try/finally`. This task doesn't need to do either.

### C2 — Ordering-dependent belt-and-braces on `EnteredEditMode`

The self-reviewer noted this and I agree: on the launcher path, `StagedHoleSceneGuard.OnPlayModeStateChanged(EnteredEditMode)` runs before `CaptureSceneSetup.Restore` (both listen on the same event, guard registered at `[InitializeOnLoad]` first). The guard closes the hole scene, then `Restore` runs its own `CloseStagedHoleScenes()` as a no-op and restores from a snapshot that never contained hole scenes by design. Net effect is clean, but the ordering is registration-order-dependent, not deterministic. Cosmetic; nothing to change. Worth flagging so a future edit that moves subscription order doesn't silently regress it.

---

## What I looked for and did NOT find

- **Fabricated evidence.** All quoted `GetSceneManagerSetup()` shapes reproduce in my probes.
- **`SaveScene` on the hole-scene path.** Zero matches in the three changed test/guard files.
- **New scene, prefab, or CSV writes.** Zero `.unity` diffs across the full verification.
- **`TestRunnerApi` reference.** Zero matches — deliberate avoidance held.
- **Cross-cutting asmdef damage.** Test asmdef is Editor-only; ref to `Golfin.Physics.Viewer` is Editor-scoped; new files are `#if UNITY_EDITOR` wrapped. No player-build risk added.
- **Static-defeat vector on the new teardown.** Both fixtures now scan `SceneManager.sceneCount` — a wiped static cannot defeat the close loop. Verified by reading the diff, not by simulating a domain reload.
- **Guard mid-run miss.** Guard did NOT fire during my `tests-run` — the interlock works.

---

## Acceptance-checklist re-verification (Rule 5 — every criterion, fresh evidence)

| # | Item | My verdict | Evidence citation |
|---|---|---|---|
| 1 | Full EditMode run #1 clean + dump | PASS | § Independent EditMode run — 1116 / 1112 pass / 1 flake / 3 skip; § Post-run probe → HOLE_GEO_SCENES_OPEN=0 |
| 2 | Full EditMode run #2 clean + dump | PASS (via self-reviewer's independent run + report's Run A/B) | Report + SELF_REVIEW § 3 both show back-to-back cleanliness; my one run corroborates the mechanism |
| 3 | Mid-run guard safety (18-hole sweep passes) | PASS | § Editor.log teardown — 18 close-lines + summary; § Guard did NOT fire; three independent reasons (d, staged-window, e) all held |
| 4 | Interrupted-run recovery via pre-clean | PASS with caveat | Direct-invocation proof in report is valid — reflection confirms the method is the one NUnit runs; mechanism verified |
| 5 | Authoring protection, both directions | PASS | Report's 4-branch table (A1/A2/A3/B) exercises each of conditions (b), (c), (d); byte-identical `Hole_06_Geo.unity` sha256 before/after confirms no save |
| 6 | Launcher path still clean | PASS | Report's `VersusHudCaptureMenu` round-trip shows snapshot/restore log preserved; git `.unity` diff = 0 |
| 7 | Killed-editor case | PASS | Report's Editor.log lines 555/565/593/606 show relaunch → guard delayCall closes Hole_06_Geo |
| 8 | `LastSceneManagerSetup.txt` = ShellScene only | PASS at time of report; **stale mid-my-review** — see § Concern C1 | End-of-report file content matches; my `cat` after `tests-run` caught an intermediate state — surface-only, non-regression |
| 9 | Zero `.unity` diffs | PASS | `git status … -- '*.unity'` empty in my terminal, this session |
| 10 | Console clean of task-related errors | PASS | `console-get-logs` shows only unrelated test-log noise (`StaminaRuntimeService`, `GachaBannerCatalog` warnings) |
| 11 | Spec deviations flagged | PASS | Report's five deviations all justified; my judgment agrees |

---

## Editor left as I found it

Final state after my verification (`FinalProbe` at 06:52:36 JST):

```
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isActive=True isSubScene=False
HOLE_GEO_SCENES_OPEN=0
guardEnabled=True
stagedSceneWindowActive=False owner='<none>'
activeScene='ShellScene' isDirty=False
```

Same as I found it, per brief instructions: ShellScene only, not dirty, guard EditorPref TRUE, staged-scene window inert. No play-mode entered. No `kill -9`. Actions taken: read-only `git status`/`git diff`, four `script-execute` probes (Debug.Log only), one full `tests-run`, one single-method `tests-run`, and `AssetDatabase`-neutral `EditorSceneManager.RestoreSceneManagerSetup` with an identical setup (no state change).

`LastSceneManagerSetup.txt` currently shows `Hole_06_Geo` alone as active — see § Concern C1 for full trace. This file will be rewritten by Unity on the next scene-setup persist trigger (normal shutdown, next scene open, etc.). The live in-memory state is clean.

---

## Verdict

**READY_FOR_REDTEAM** — set STATUS.md to `READY_FOR_REDTEAM`.

Fix is architecturally sound. Both spec layers land as designed. Scan-based teardown demonstrably closes what the old `s_HoleCache`-iterating close was silently no-op'ing. Guard's five ANDed conditions are correct and provably authoring-safe by construction. Staged-scene window is a well-reasoned unspec'd addition. Zero player-build risk introduced. Zero `.unity` writes. Report is honest about its one procedural deviation (item 4).

Red-team should adversarially probe § C1 (the `LastSceneManagerSetup.txt` `Hole_06_Geo`-alone leak I observed live during this review). My call is that it's a separate vector originating in `PhysicsLabAutoRestore` and out of this task's scope, but a skeptic may argue it should be treated as an in-scope regression because it produces the same user-visible symptom Cesar originally reported.

---

# RED-TEAM REVIEW — `hole_scene_leftover_v3`

**Red-team reviewer:** golfin-redteam-reviewer (Opus 4.8)
**Date:** 2026-08-11 07:10 JST
**Verdict:** **ARCHITECT_REVIEW_ESCALATE**

I reproduced Cesar's exact reported symptom — `Hole_06_Geo` left in the editor hierarchy after a clean test run — **on this ship candidate**. The task's own primary acceptance gate fails when I run it. Both the implementer and the reviewer came up clean only by nondeterministic luck of test ordering. Detail below.

## What I re-derived myself (not inherited)

| Check | My result | Verdict |
|---|---|---|
| Full EditMode suite (unfiltered, my run 07:07 JST) | `Passed 1116 / 1113 pass / 0 fail / 3 known skip` | Layer 1 works; 0 attributable failures |
| `git status --porcelain -- '*.unity'` (throughout) | empty | no hole scene ever saved — leak is hierarchy-only |
| `CaptureSceneSetup` diff | `CloseStagedHoleScenes`→`public static int`, `logContext` default reproduces `"[CaptureSceneSetup] Closing staged hole scene without saving: {name}"` char-for-char; `Capture`/`Restore`/`StripSerializedHost` bodies unchanged; 4 launchers still call `Capture(SetupKey)`/`Restore(SetupKey)` | launchers behaviourally untouched — PASS |
| Guard 5-condition logic (read) | `LabScaffold` is a host (line 44), identical to `ShellScene` | see defect below |
| `LastSceneManagerSetup.txt` / editor left state | ShellScene only, clean, guard ON, pref=6 | restored as briefed |

## THE BLOCKER — the reported symptom reproduces on the fix (empirical)

Immediately after my full, all-green EditMode suite run, `GetSceneManagerSetup()` on the live editor returned **two** scenes:

```
=== REDTEAM post-full-suite scene dump ===   (07:08:05 JST)
  name=ShellScene   dirty=False active=True  isHoleGeo=False
  name=Hole_06_Geo  dirty=False active=False isHoleGeo=True     <-- LEFTOVER
HOLE_GEO_SCENES_OPEN=1
GetSceneManagerSetup().Length=2
  setup path=Assets/Scenes/ShellScene.unity           isActive=True
  setup path=…/Generated/Hole_06_Geo.unity            isActive=False
```

Immediately preceding it, mid-suite, `Editor.log:64826  [PhysicsLab] Auto-restored Hole 06`. The task's acceptance items 1/2/8 REQUIRE this post-suite dump to contain **no** `Hole_NN_Geo`. It contains `Hole_06_Geo`. **The leak this task exists to stop is still present, reproduced by me on the ship candidate, on a normal completed run — no kill, no interruption.**

Why the implementer's Run A/B and the reviewer's run were clean and mine was not: the auto-restore leftover is cleared **only** when some later fixture happens to load a scene in `Single` mode after the auto-restore fires. Test execution order is not deterministic; in my run the auto-restore fired late (07:07:58, near the end) and nothing swept it. The implementer honestly disclosed this nondeterminism in acceptance item 4 ("removed earlier in the run by something upstream") without recognising it as the reproduction condition for the whole bug.

## The vector, root-caused

`Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs` → `PhysicsLabAutoRestore` (`[InitializeOnLoad]`): on **any** open of `LabScaffold.unity` it reads EditorPref `Golfin.PhysicsLab.CurrentHole` (=6 on this machine) and, via `delayCall`, opens `Hole_06_Geo` additively. Confirmed firing **inside the EditMode suite** — the stack trace at `Editor.log:31772` sits between `PhysicsLabControllerHandleShotResolvedTests` NUnit frames; that fixture (and `ActionButtonRenderingTests`) opens `LabScaffold`, which trips auto-restore, which opens `Hole_06_Geo`. The opening fixture's `try/finally` closes only its own `LabScaffold` handle — never the hole it didn't open.

This is the **third resurrection vector**, and it is the specific reason Cesar's symptom is always `Hole_06` (the pref value), not a random hole from the 18-sweep. **The spec's root-cause diagnosis (the 18-hole sweep in `RealHoleTerrainTests`) is therefore incomplete — arguably mis-attributed.** Layer 1 correctly fixes the sweep fixtures, but the sweep was never what pinned the symptom to Hole_06. Fixing the diagnosed vector does not stop the reported symptom.

## Adjudication of the three options the brief posed — and a fourth finding

**(c) Actual defect introduced by this change — CONFIRMED.** On the exact normal lab-authoring shape (LabScaffold active + `Hole_06_Geo` additive, clean, non-active; guard enabled; staged-window inactive; at rest) the guard's own decision function returns **`closed=1`**:

```
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=redteam-verbose).
=== REDTEAM guard-decision RESULT: closed=1 holesBefore=1 holesAfter=0 ===
```

Because the spec makes `LabScaffold` a "host" (condition d, guard line 44), the guard treats the hole that `PhysicsLabAutoRestore` / the Hole Picker deliberately load into the lab as a staged leftover and destroys it. Cesar actively uses this workflow (pref=6). It will manifest on `EnteredEditMode` (every time he exits play mode to test a shot in the lab) and on recompiles. So the change both **fails to stop** the leak AND **introduces a new disruption** to a live developer tool. (A forced `RequestScriptReload` in my bench did *not* fire the hooks — they silently declined during `isUpdating` — which is itself a reliability concern: the guard's automatic hooks are timing-fragile on a warm reload, so even the leftover it *is* designed to catch can be missed until the next trigger.)

**(b) Scope gap that repeats the v1/v2 mistake — CONFIRMED and dominant.** The guard's authoring-protection design (`HostSceneOpen()` false ⇒ "treat lone hole as deliberate authoring, leave it alone") means the `Hole_06`-**alone** shape — the shape the auto-restore leak collapses to once `LabScaffold` is closed by its fixture, and the shape the reviewer caught in `LastSceneManagerSetup.txt` at § C1 — is **preserved** by the guard, not swept. On next boot the guard sees a lone hole, no host, and keeps it. That is precisely Cesar's "reintroduced unprompted and left there."

**(a) Out of scope, correctly deferred, pass — REJECTED.** Passing would ship a "fix" for a leak I can make recur on demand, while adding a workflow regression.

## Plain answer to the brief's question

**Yes — Cesar can still see `Hole_06_Geo` come back after this ships.** I demonstrated it: a clean, all-green EditMode suite run leaves `Hole_06_Geo` in the hierarchy and in `GetSceneManagerSetup()` (what Unity persists to `LastSceneManagerSetup.txt`). The residual vector (`PhysicsLabAutoRestore` firing inside LabScaffold-opening test fixtures) is untouched by this task, is the specific cause of the always-`Hole_06` symptom, and in its lone-hole form is actively **protected** by the guard.

## Why ESCALATE and not FAIL

The blocker is concrete and reproduced, but it cannot be resolved by another implementer iteration against the current spec:
- The spec's identified root cause (18-hole sweep) is not the vector responsible for the reported symptom; a faithful implementation of THIS spec cannot stop it.
- The correct fix requires Cesar to sanction new scope the spec foreclosed — either fix `PhysicsLabAutoRestore` itself (e.g. close the auto-restored hole when its `LabScaffold` closes, or gate auto-restore so a test opening `LabScaffold` doesn't trip it), or sweep the LabScaffold-opening test fixtures (`PhysicsLabControllerHandleShotResolvedTests`, `ActionButtonRenderingTests`, …).
- It also requires resolving a genuine design conflict the current 5-condition guard cannot: the launcher/auto-restore **leftover** and a deliberate **lab-authoring session** are the identical scene shape (LabScaffold + hole, clean, non-active). No condition tweak distinguishes them; picking a side (drop `LabScaffold` from the host list vs. keep sweeping it; sweep lone holes vs. keep authoring-protection) is a product decision, not an implementation detail.
- This is the third attempt at the same recurring failure shape (incomplete vector list). Routing back for a 4th pass against the same flawed diagnosis is exactly what the escalation gate exists to prevent.

## Suggested direction for Cesar (not prescriptive)

1. Root-fix `PhysicsLabAutoRestore` so it never leaks: only auto-restore when a **human** opens LabScaffold (skip while a test run / batch mode is active), and/or close the auto-restored hole when LabScaffold is closed.
2. Decide the guard's stance on the `LabScaffold + hole` shape and on lone holes, given the lab-authoring workflow — the two goals conflict as specified.
3. Re-derive the spec's root cause to name the auto-restore vector explicitly, so the acceptance gate (post-suite dump clean) is run enough times / with enough ordering variation to actually catch it.

## Editor left as briefed

ShellScene only, `dirty=False`, `HOLE_GEO_SCENES_OPEN=0`, guard EditorPref TRUE, `Golfin.PhysicsLab.CurrentHole=6`, `LastSceneManagerSetup.txt` = ShellScene only, `git status -- '*.unity'` empty. No play mode entered, no `kill -9`. I staged/closed hole scenes and forced one domain reload for the converse-risk bench, all without saving any `.unity`; all leftovers cleaned.
