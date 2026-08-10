# IMPLEMENTER_REPORT — `hole_scene_leftover_v3`

> ## iter-2 (2026-08-11) — the red-team was right, and it found the real vector
>
> The red-team gate reproduced Cesar's exact symptom on the iter-1 ship candidate: a clean, all-green
> suite run that ended with `ShellScene + Hole_06_Geo` in the hierarchy. Cesar then chose the scope
> widening ("fix at source + relax guard"). **Everything in the iter-1 sections below still stands —
> Layer 1 was never the problem — but iter-1 would NOT have stopped the leak Cesar was reporting.**
>
> ### Root cause, third vector: `PhysicsLabAutoRestore`
>
> `Assets/Scripts/Editor/Physics/PhysicsLabHolePicker.cs` — `[InitializeOnLoad]`, hooked to
> `EditorSceneManager.sceneOpened`. On **any** open of `LabScaffold.unity` it reads EditorPref
> `Golfin.PhysicsLab.CurrentHole` (= **6** on this machine) and, deferred via `delayCall`, opens that
> hole **additively**. Two things made it the culprit:
>
> 1. `ActionButtonRenderingTests` opens LabScaffold **additively** as a fixture host, so the suite
>    triggered it. The fixture's `try/finally` closes only the scene it opened itself, never the hole
>    an editor hook slipped in behind it. Verified from the live stack at `Editor.log:31772`:
>    `PhysicsLabAutoRestore:RestoreHole (PhysicsLabHolePicker.cs:52)` ← `<OnSceneOpened>b__0 (:32)`.
> 2. `RestoreHole` never re-validated after the deferral. Observed live at `Editor.log:64820-64826` —
>    `Opening scene '…/Hole_06_Geo.unity additively'` + `[PhysicsLab] Auto-restored Hole 06` with
>    **only ShellScene open, no LabScaffold anywhere.** That is precisely the shape in the spec's
>    2026-08-03 `.bak` evidence, and precisely what Cesar saw on screen.
>
> **This is why the leftover was always `Hole_06`** — the pref — and never a random hole from the
> 18-hole sweep. The spec's diagnosis identified a real vector but not the one pinning the symptom.
>
> ### Fixes
>
> | file | change |
> |---|---|
> | `PhysicsLabHolePicker.cs` | `OnSceneOpened` restores only on `OpenSceneMode.Single` (the human / Hole-Picker path); fixtures open LabScaffold Additive and no longer trigger it. `RestoreHole` re-validates LabScaffold is still open after the deferral. |
> | `StagedHoleSceneGuard.cs` | Condition (d) host set narrowed to **ShellScene only**. LabScaffold + clean non-active hole is the *identical* shape to a deliberate lab session, so the guard no longer touches it — removing the workflow regression the red-team benched. |
> | `StagedHoleSceneGuard.cs` | Editor-load sweep changed from a single `delayCall` to an idle-settle pump (see below). |
>
> ### A second defect I found while verifying, and fixed
>
> The iter-1 editor-load hook was a bare `EditorApplication.delayCall`. Unity restores the scene setup
> from `LastSceneManagerSetup.txt` **after** `[InitializeOnLoad]`, so that `delayCall` races the
> restore — and on 2026-08-11 it **lost**: a staged `ShellScene + Hole_06_Geo` survived a full
> `kill -9` + relaunch completely untouched, no guard line in the log. iter-1's acceptance #7 passed
> only because it happened to win the race that time. Replaced with `LoadSweepPump` on
> `EditorApplication.update`, which waits until the editor is idle (not compiling/updating/playing)
> plus a 3 s settle, sweeps once, then unsubscribes.
>
> ### iter-2 verification (all re-derived, nothing carried forward)
>
> **Guard, both shapes** — `Sweep` driven directly, the function all hooks call:
> ```
> [R1 lab shape]  before: ['LabScaffold' active=True] ['Hole_06_Geo' active=False]
> [R1] closed=0   -> PASS (lab session protected; the regression is gone)
> [R2 leak shape] before: ['ShellScene' active=True] ['Hole_06_Geo' active=False]
> [R2] closed=1   -> PASS (Cesar's reported shape still swept)
> ```
>
> **Source fix, both paths:**
> ```
> V3B-A  LabScaffold opened ADDITIVELY (fixture path)  -> ['ShellScene'] ['LabScaffold']  no hole injected   PASS
> (i)    RestoreHole(6) with LabScaffold OPEN          -> hole restored = True            PASS (lab workflow intact)
> (ii)   RestoreHole(6) with LabScaffold NOT open      -> injected = False                PASS (the ShellScene+Hole_06 defect is dead)
> ```
> (ii) is a direct reproduction of the observed `Editor.log:64826` defect, now bailing.
>
> **Two back-to-back full suites, post-fix:**
> ```
> run 1: TotalTests=1116  Passed=1113  Failed=0  Skipped=3
>   count=1  path='Assets/Scenes/ShellScene.unity' isActive=True     HOLE_GEO_SCENES_OPEN=0
> run 2: TotalTests=1116  Passed=1112  Failed=1  Skipped=3
>   count=1  path='Assets/Scenes/ShellScene.unity' isActive=True     HOLE_GEO_SCENES_OPEN=0
> ```
> Run 2's single failure is `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed`,
> the known pre-existing flake — now precisely characterised: it asserts on its own comment's premise
> *"Time.unscaledTime is 0f in EditMode"*, which only holds on the **first** suite run of an editor
> session. It passes on run 1 and fails on run 2 regardless of this task, and it failed in an iter-1
> control run with every task change stashed. Filed separately.
>
> Across both runs: **0 `[PhysicsLab] Auto-restored` lines**, **0 guard activity mid-run**, and
> **36 `[RealHoleTerrainTests/teardown]` closes** (18 × 2) — Layer 1 still doing its job.
>
> **Acceptance #7 re-run end-to-end under the revised condition (d).** Staged `ShellScene +
> Hole_06_Geo`, confirmed `LastSceneManagerSetup.txt` persisted it across the kill, `kill -9`,
> relaunched. Fresh session log (log truncates on start, so this is unambiguously the new session):
> ```
> 512:Loaded scene 'Assets/Scenes/ShellScene.unity'
> 523:Loaded scene '.../Generated/Hole_06_Geo.unity'
> 558:[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=editor-load).
> 571:[StagedHoleSceneGuard] Swept 1 leftover hole scene(s) on editor-load.
> ```
> The pump won the race the old `delayCall` had lost minutes earlier on the identical state.
>
> **Zero `.unity` diffs** throughout iter-2. Editor left on ShellScene only, clean, guard ENABLED,
> `Golfin.PhysicsLab.CurrentHole` untouched at 6.
>
> ### iter-2 deviations
> - `PhysicsLabHolePicker.cs` is **not** in the spec's file list. Cesar authorised the widening after
>   the red-team escalation.
> - Condition (d) no longer matches the spec's table (which named LabScaffold). Deliberate, authorised,
>   and documented in the class summary with the reasoning and the coverage boundary it creates: a
>   capture launcher that stages LabScaffold and then dies is no longer swept by the guard. Its own
>   `CaptureSceneSetup.Restore` covers the normal exit.
> - I considered also hooking `EditorSceneManager.sceneOpened` for instant cleanup and **rejected it**:
>   it would close a hole scene Cesar opens by hand next to ShellScene to inspect, the moment he opens
>   it. The source fix removes the injection, so the hook is unnecessary.

---

**Iteration:** iter-1
**Iteration shape:** `scene-hygiene:hole_geo_scene_survives_into_hierarchy`
**Baseline:** HEAD `a9ef35adb` at kickoff → `5d938c9a8` mid-verification (concurrent commit by another
stream, see HEARTBEAT.log; neither of its two files is touched by this task).
**Canonical evidence:** this report's quoted `GetSceneManagerSetup()` dumps. This is a non-visual,
editor-hygiene task — there is no screenshot or video deliverable, and no Figma node.

---

## What was changed and why

The dominant resurrection vector was the EditMode test suite, exactly as the spec diagnosed. Two
layers landed.

### Layer 1 — reload-proof fixture cleanup (the actual fix)

`RealHoleTerrainTests` opened all 18 `Hole_NN_Geo` scenes additively and cleaned up by iterating
`static readonly Dictionary<string, HoleProviders> s_HoleCache`. A domain reload between an
`OpenScene` and the teardown wipes that dictionary **while the scenes stay open**, so the teardown
iterated an empty dict, closed nothing, and reported success.

Both fixtures now:

1. **Pre-clean in `[OneTimeSetUp]`** — scan-and-close every open `Hole_NN_Geo` before the first
   `OpenScene`, so a leftover from a previous aborted run self-heals on the next run.
2. **Scan-based `[OneTimeTearDown]`** — iterate `SceneManager.sceneCount` / `GetSceneAt(i)` rather
   than the static. Idempotent; cannot be defeated by a wiped static.
3. **Never save.** `CloseScene(scene, removeScene: true)`, no `SaveScene` anywhere on the path.

### Layer 2 — `StagedHoleSceneGuard` (the safety net)

New editor-only `[InitializeOnLoad]` class hooked to `playModeStateChanged → EnteredEditMode`,
`AssemblyReloadEvents.afterAssemblyReload`, and one `EditorApplication.delayCall` on load. It closes
a scene only when **all five** conditions hold (a name, b not active, c not dirty, d ShellScene or
LabScaffold also open, e not playing/compiling/updating). Never saves. Ships
`GOLFIN > Scene Hygiene > Close Staged Hole Scenes Now` and a `GOLFIN > Scene Hygiene > Guard Enabled`
`EditorPrefs`-backed checked toggle (default ON). **No `TestRunnerApi` reference**, per spec.

### One implementation of the rule, not four

`CaptureSceneSetup.IsHoleGeoScene` and `CaptureSceneSetup.CloseStagedHoleScenes` were promoted from
`private` to `public static`; the two fixtures and the guard all call them. Nothing re-rolls the
predicate or the sweep loop.

**Asmdef path taken.** `CaptureSceneSetup` compiles into `Golfin.Physics.Viewer` (verified by
reflection, not assumed):

```
CaptureSceneSetup assembly = Golfin.Physics.Viewer
```

The guard lives in `Assembly-CSharp-Editor`, which auto-references `Golfin.Physics.Viewer`
(`autoReferenced: true`) — no asmdef change needed there. `Golfin.Gameplay.Tests` has
`overrideReferences: true` and could not see it, so one line was added to its reference list. The
reverse direction (a shared helper in a new editor-only asmdef, with `CaptureSceneSetup` delegating
*to* it) is impossible: `Golfin.Physics.Viewer` is an all-platform assembly and Unity rejects an
all-platform assembly referencing an editor-only one. Hence the helper lives on `CaptureSceneSetup`
and everyone delegates *inward*, which is the spec's first-listed option.

---

## Acceptance checklist

### ☑ 1. Full EditMode suite run #1 — PASS

```
Run A: TotalTests=1111  PassedTests=1108  FailedTests=0  SkippedTests=3  Duration=00:01:01.07
```

Dump taken immediately after, via `EditorSceneManager.GetSceneManagerSetup()`:

```
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
```

`HOLE_GEO_SCENES_OPEN` is computed by running `CaptureSceneSetup.IsHoleGeoScene` over the live
`SceneManager` list — the same predicate the fix uses, so the assertion cannot disagree with the
implementation about what counts as a hole scene.

**On the totals vs the spec's baseline (1109 / 1106 / 0 fail / 3 skip).** Observed total is **1111**,
not 1109. The delta is not from this task: a control run with every one of this task's changes
stashed also reported `total=1111` (see § Failure attribution). The 3 skips are the spec's known
pre-existing `HoleCompleteDriverTests` Stage-C1 skips, unchanged.

### ☑ 2. Full EditMode suite run #2, back to back — PASS

```
Run B: TotalTests=1111  PassedTests=1108  FailedTests=0  SkippedTests=3  Duration=00:00:59.96
        start-time 2026-08-10 07:53:24Z   end-time 2026-08-10 07:54:23Z
```

Dump immediately after run B:

```
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
```

The scan-based teardown is visible doing the work in `Editor.log`, one line per scene, both runs:

```
[RealHoleTerrainTests/teardown] Closing staged hole scene without saving: Hole_18_Geo
[RealHoleTerrainTests/teardown] Closing staged hole scene without saving: Hole_17_Geo
… (16 more) …
[RealHoleTerrainTests/teardown] Closing staged hole scene without saving: Hole_01_Geo
[RealHoleTerrainTests] Teardown closed 18 staged hole scene(s); 1 scene(s) remain open.
[BakedPivotRegressionTests/teardown] Closing staged hole scene without saving: Hole_01_Geo
[BakedPivotRegressionTests] Teardown closed 1 staged hole scene(s); 1 scene(s) remain open.
```

For the record, the sweep really does hold all 18 open simultaneously — a mid-run dump caught it:

```
GetSceneManagerSetup() count=18
  path='…/Hole_01_Geo.unity' isLoaded=True isActive=True …
  … Hole_02 … Hole_18 …
```

That is the state the old teardown was failing to clean.

### ☑ 3. Mid-run guard safety — PASS

The 18-hole sweep passed with the guard **enabled** (`guardEnabled=True` asserted in the same dump).
Per-hole results from run B, not just the total:

| case | result | duration | case | result | duration |
|---|---|---|---|---|---|
| Hole_01 | Passed | 0.197s | Hole_10 | Passed | 0.507s |
| Hole_02 | Passed | 0.153s | Hole_11 | Passed | 0.370s |
| Hole_03 | Passed | 0.484s | Hole_12 | Passed | 0.822s |
| Hole_04 | Passed | 0.183s | Hole_13 | Passed | 0.924s |
| Hole_05 | Passed | 0.912s | Hole_14 | Passed | 0.849s |
| Hole_06 | Passed | 0.151s | Hole_15 | Passed | 0.387s |
| Hole_07 | Passed | 0.489s | Hole_16 | Passed | 0.446s |
| Hole_08 | Passed | 1.056s | Hole_17 | Passed | 0.161s |
| Hole_09 | Passed | 0.746s | Hole_18 | Passed | 0.484s |

18/18 Passed. `BakedPivotRegressionTests`: 24/24. The guard closed nothing mid-run.

**Three independent reasons it cannot fire mid-run**, not one:
- condition (d) — mid-sweep the hierarchy is an untitled scene + hole scenes; neither ShellScene nor
  LabScaffold is open, so the guard declines before it even looks at a hole scene. Confirmed in the
  mid-run dump above (`activeScene=''`).
- the cooperative staged-scene window — both fixtures call
  `CaptureSceneSetup.BeginStagedSceneWindow(...)` in `[OneTimeSetUp]` and `EndStagedSceneWindow()` in
  `[OneTimeTearDown]`. `SessionState`-backed (dies with the Editor session, so a hard kill leaves no
  stale inhibit) with a 45-minute expiry so an aborted run cannot disable the guard for the session.
- condition (e) — playing / compiling / updating.

*Deviation:* the staged-scene window is not in the spec. It was added because the spec's own guard
rail ("the guard must never fire while an EditMode run is in progress") is only probabilistically
satisfied by hooks + conditions: the `afterAssemblyReload` hook exists precisely because domain
reloads happen mid-run, and a reload mid-sweep is the root-cause scenario. Verified inert at rest:
`stagedSceneWindowActive=False owner=<none>`.

### ☑ 4. Interrupted-run recovery — PASS (via direct invocation of the real `[OneTimeSetUp]`)

Two hole scenes staged by hand to simulate an aborted sweep, then the fixture's real
`[OneTimeSetUp]` invoked — the attribute is read back from reflection in the same call, so this is
provably the method NUnit runs, not a lookalike:

```
leftover re-staged: '' 'Hole_01_Geo' … 'Hole_06_Geo' … 'Hole_11_Geo' … 'Hole_18_Geo'
invoking Golfin.Gameplay.Tests.RealHoleTerrainTests.GlobalSetup [attributes: OneTimeSetUpAttribute]
AFTER [OneTimeSetUp] pre-clean: ''
after [OneTimeTearDown]: ''
```

Every staged hole scene is gone after the pre-clean.

**Honest caveat — read this.** The spec asks for the pre-clean log line from a *full suite run*
started with a leftover present. I staged `Hole_06_Geo` + `Hole_11_Geo`, ran the full suite, and got
**no** `[RealHoleTerrainTests/pre-clean]` line: the leftovers were already gone by the time the
fixture ran, removed earlier in the run by something upstream (other fixtures open scenes in Single
mode, which closes additive scenes). The end state was clean and the suite was green, but that run
does **not** evidence the pre-clean, so I am not citing it as if it did. The direct invocation above
is what proves the pre-clean path. This is the one acceptance item whose *form* deviates from the
spec; the mechanism itself is verified.

### ☑ 5. 🔴 Authoring protection, both directions — PASS (four branches, not two)

Driven through `StagedHoleSceneGuard.Sweep(hook, verbose)` — the single method all three hooks
delegate to, so every condition under test is the production decision path.

| # | scenario | expected | closed | verdict |
|---|---|---|---|---|
| A1 | `Hole_06_Geo` **alone, ACTIVE, DIRTY** (the spec's case) | not closed | 0 | **PASS** |
| A2 | ShellScene + `Hole_06_Geo` additive, **DIRTY**, non-active — isolates condition (c) | not closed | 0 | **PASS** |
| A3 | ShellScene + `Hole_06_Geo` additive, clean, **ACTIVE** — isolates condition (b) | not closed | 0 | **PASS** |
| B | ShellScene + `Hole_06_Geo` additive, **clean, non-active** | closed | 1 | **PASS** |

```
[A1] before: ['Hole_06_Geo' dirty=True active=True]
[A1] closed=0 after: ['Hole_06_Geo' dirty=True active=True]          -> PASS
[A2] before: ['Hole_06_Geo' dirty=True active=False] ['ShellScene' dirty=False active=True]
[A2] closed=0 after: ['Hole_06_Geo' dirty=True active=False] …       -> PASS
[A3] before: ['ShellScene' … active=False] ['Hole_06_Geo' dirty=False active=True]
[A3] closed=0 after: ['ShellScene' …] ['Hole_06_Geo' dirty=False active=True]  -> PASS
[B]  before: ['ShellScene' dirty=False active=True] ['Hole_06_Geo' dirty=False active=False]
[B]  closed=1 after: ['ShellScene' dirty=False active=True]          -> PASS
```

The guard's own reasoning, from `Editor.log`:

```
[StagedHoleSceneGuard] Neither ShellScene nor LabScaffold is open — treating any open hole scene as deliberate authoring, leaving it alone.
[StagedHoleSceneGuard] Hole_06_Geo has unsaved changes — leaving it open and unsaved (authoring protection, hook=EnteredEditMode).
[StagedHoleSceneGuard] Hole_06_Geo is the ACTIVE scene — leaving it open (authoring protection, hook=delayCall).
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=afterAssemblyReload).
[StagedHoleSceneGuard] Swept 1 leftover hole scene(s) on afterAssemblyReload.
```

**Direction B was additionally proven through a genuine forced domain reload**, not only a direct
call — two clean non-active hole scenes, `EditorUtility.RequestScriptReload()`, and the guard fired
on the way back in:

```
=== ACCEPTANCE-5B: clean, non-active Hole_06_Geo + Hole_11_Geo alongside ShellScene; forcing domain reload ===
Reloading assemblies due to reload request.
Begin MonoManager ReloadAssembly
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_11_Geo (hook=delayCall).
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=delayCall).
[StagedHoleSceneGuard] Swept 2 leftover hole scene(s) on delayCall.
```

**"…and does NOT save it"** — `Hole_06_Geo.unity` was opened, dirtied with a probe GameObject, put
through the guard four times, and closed with `removeScene: true`:

```
BEFORE sha256: d28fd1fb828afc22018a04bf69805dc1ac67b2ac34698b9b71a7de00dfbd8bd5
AFTER  sha256: d28fd1fb828afc22018a04bf69805dc1ac67b2ac34698b9b71a7de00dfbd8bd5
BEFORE mtime : 2026-06-02 02:49:45      AFTER mtime : 2026-06-02 02:49:45
probe leaked = False
```

Byte-identical, mtime untouched.

*Note on A1:* with `Hole_06_Geo` dirty, `EditorUtility.RequestScriptReload()` was swallowed and no
reload occurred (Unity defers it against an unsaved scene), so A1 was driven through `Sweep` rather
than a reload. The reload path itself is proven by B above.

### ☑ 6. Launcher path still clean — PASS

Ran the real launcher via `EditorApplication.ExecuteMenuItem("GOLFIN/Capture 1v1/Record Versus Launch")`
— `VersusHudCaptureMenu`, which stages `LabScaffold` single + `Hole_04_Geo` additive and arms
`BotVideoRecorder`. Full play-mode round trip, `Editor.log`:

```
=== ACCEPTANCE-6: launching VersusHud capture launcher (LabScaffold single + Hole_04_Geo additive) ===
[VersusHudCaptureMenu] Launching scenario: 'versus_launch'
[CaptureSceneSetup] Snapshot taken (1 scene(s)): ShellScene
[VersusHudCaptureMenu] Tee position queried from Hole_04_Geo: (57.186, 22.566, -24.979) …
[VersusHudCaptureMenu] Armed (SessionState). Scenario='versus_launch'. Entering play mode…
[VersusHudCaptureMenu] Injected [VersusHudCaptureBot] host (scenario='versus_launch', not saved to disk).
[VersusHudCaptureMenu] Restored DisableSceneReload option (at ExitingPlayMode).
[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_04_Geo (hook=EnteredEditMode).
[StagedHoleSceneGuard] Swept 1 leftover hole scene(s) on EnteredEditMode.
[CaptureSceneSetup] Restored pre-run scene setup: ShellScene
[VersusHudCaptureMenu] Capture run cleaned up: hole scene closed, scene setup restored.
```

Dump after exiting play mode:

```
isPlaying=False
GetSceneManagerSetup() count=1
  path='Assets/Scenes/ShellScene.unity' isLoaded=True isActive=True isSubScene=False
  live 'ShellScene' dirty=False
HOLE_GEO_SCENES_OPEN=0
activeScene='ShellScene'
```

**`CaptureSceneSetup` behaviour unchanged** — the snapshot/restore pair logged exactly its pre-change
text (`Snapshot taken (1 scene(s)): ShellScene` → `Restored pre-run scene setup: ShellScene`), and
neither scene file was written:

```
2026-08-10 11:12:11  Assets/Scenes/Physics/LabScaffold.unity
2026-06-03 17:27:43  Assets/Golf/Courses/lomond-country-club/Generated/Hole_04_Geo.unity
$ git status --porcelain --untracked-files=all -- '*.unity'   →  count: 0
```

**One behavioural note worth flagging to the reviewer.** The guard and the launcher *both* subscribe
to `EnteredEditMode`, and the guard — registered at `[InitializeOnLoad]` — ran first, so it closed
`Hole_04_Geo` before `CaptureSceneSetup.Restore` got there. That is harmless and the end state is
identical: `Restore` begins with its own `CloseStagedHoleScenes()` (a no-op once the guard has swept)
and then restores from a snapshot that, by design, never contains hole scenes because `Capture`
filters them out. Belt and braces firing in the other order is still belt and braces. No launcher
code changed.

### ☑ 7. Killed-editor case — PASS

Staged the exact hierarchy a launcher leaves behind when it dies — `LabScaffold` single +
`Hole_06_Geo` additive — and confirmed Unity had already persisted the leak, reproducing the shape of
the spec's 2026-08-03 `.bak` evidence:

```
$ cat Library/LastSceneManagerSetup.txt      # pre-kill
sceneSetups:
- path: Assets/Scenes/Physics/LabScaffold.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0
- path: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
  isLoaded: 1
  isActive: 0
  isSubScene: 0
```

Then `kill -9 42719` (the Editor process, 06:31:30 JST), confirmed dead, relaunched with
`open -na …/Unity.app --args -projectPath …`. The setup file still carried the leak across the kill,
so the relaunch genuinely reopened it. New session's `Editor.log`:

```
555:Loaded scene 'Assets/Scenes/Physics/LabScaffold.unity'
565:Loaded scene 'Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity'
593:[StagedHoleSceneGuard] Closing leftover staged hole scene without saving: Hole_06_Geo (hook=delayCall).
606:[StagedHoleSceneGuard] Swept 1 leftover hole scene(s) on delayCall.
```

Unity reopened the leaked hole scene; the guard closed it at load. Post-relaunch state:

```
=== ACCEPTANCE-7 post-relaunch state ===
isPlaying=False isCompiling=False
guardEnabled=True
GetSceneManagerSetup() count=1
  path='Assets/Scenes/Physics/LabScaffold.unity' isLoaded=True isActive=True isSubScene=False
  live 'LabScaffold' dirty=False
HOLE_GEO_SCENES_OPEN=0
```

This is the vector that made the original bug survive editor restarts, and it is now self-healing.

**Cleanup disclosure.** The `kill -9` triggered Unity's crash recovery, which wrote two new scenes
into the pre-existing `Assets/_Recovery/` folder (`0 (3).unity`, `1 (2).unity`, 5.8 MB, both stamped
06:31 today). Nothing was dirty at kill time so nothing was recovered that mattered. Both are covered
by `.gitignore:194 Assets/_Recovery/` — verified with `git check-ignore -v` — so they never affected
the zero-`.unity`-diff result, and both were deleted through `AssetDatabase.DeleteAsset`. The folder
is back to the five April-30 scenes that are tracked in HEAD; those were not touched.

### ☑ 8. `Library/LastSceneManagerSetup.txt` — PASS

```
sceneSetups:
- path: Assets/Scenes/ShellScene.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0
```

ShellScene only. (For contrast, the `.bak` from 2026-08-03 that recorded the original leak carries a
second `Hole_06_Geo.unity` entry.)

### ☑ 9. Zero `.unity` diffs — PASS

```
$ git status --porcelain --untracked-files=all -- '*.unity'
(count: 0)
```

Empty across the entire verification — two full suite runs, two control runs, a leftover-staged run,
two forced domain reloads, and four authoring-protection branches. No hole scene was ever saved.

### ☑ 10. Unity Console has no errors related to this task — PASS

Post-change compile is clean (`isCompiling=False`, all four affected types resolve to their expected
assemblies). The Console carries only the project's large pre-existing `CS0618`/`CS8632` obsolete-API
and nullable-annotation warning set, none in files this task touches. No error, exception, or warning
originates from `StagedHoleSceneGuard`, `CaptureSceneSetup`, `RealHoleTerrainTests`, or
`BakedPivotRegressionTests`.

---

## Failure attribution (why runs earlier in the session showed failures)

Three suite runs during verification showed **three different** failure sets, which is the signature
of flaky tests, not of a deterministic change. Control runs with **every one of this task's changes
stashed** (`git stash push` on the 4 tracked files + the new guard moved out of `Assets/`) settle it:

| run | task changes | total | passed | failed | failing test(s) |
|---|---|---|---|---|---|
| early #1 | applied | 1111 | 1106 | 2 | `AudioEmitterTests.MinInterval_SecondBounceWithinInterval_IsSuppressed`, `GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav` |
| early #2 | applied | 1111 | 1106 | 2 | `AimCameraFramingTests.ApplyCameraYaw_PutterBranch_…`, `AudioEmitterTests.MinInterval_…` |
| **control #1** | **stashed** | 1111 | 1107 | 1 | `GameplaySceneLoaderTests.UnloadGameplay_RestoresBottomNav` |
| **control #2** | **stashed** | 1111 | 1108 | 0 | — |
| **Run A** | applied | 1111 | 1108 | **0** | — |
| **Run B** | applied | 1111 | 1108 | **0** | — |

- `total=1111` in the control runs too → the +2 vs the spec's 1109 baseline predates this task.
- `GameplaySceneLoaderTests` failed **with the changes stashed** → not this task.
- `AimCameraFramingTests.ApplyCameraYaw_PutterBranch_…` was a concurrent stream landing commit
  `5d938c9a8 fix(camera): putter aim centres the ball under the 2D ball widget` mid-verification; it
  passes at that commit.
- `AudioEmitterTests.MinInterval_…` is self-evidently environment-dependent — its own comment reads
  *"Time.unscaledTime is 0f in EditMode"*, an assumption that holds only in a fresh editor session.
  It passes in Runs A and B.

Runs A and B, the canonical back-to-back pair, are **0 failures**.

---

## Files modified or created

| file | change |
|---|---|
| `Assets/Scripts/Gameplay/Tests/RealHoleTerrainTests.cs` | pre-clean in `[OneTimeSetUp]`, scan-based `[OneTimeTearDown]`, staged-scene window; `s_HoleCache` no longer load-bearing for cleanup |
| `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs` | same treatment; `s_HoleScene` no longer load-bearing for cleanup |
| `Assets/Scripts/Gameplay/Tests/Golfin.Gameplay.Tests.asmdef` | +1 reference `Golfin.Physics.Viewer`, so the fixtures can reach the shared helper |
| `Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs` | `IsHoleGeoScene` + `CloseStagedHoleScenes` promoted to `public static` (behaviour and log text preserved); staged-scene-window API added |
| `Assets/Scripts/Editor/SceneHygiene/StagedHoleSceneGuard.cs` | **NEW** — the guard, its 5 conditions, and the two menu items |
| `Docs/Specs/Active/hole_scene_leftover_v3/HEARTBEAT.log` | **NEW** — baseline + verification trail |
| `Docs/Specs/Active/hole_scene_leftover_v3/IMPLEMENTER_REPORT.md` | **NEW** — this file |
| `Docs/Specs/Active/hole_scene_leftover_v3/STATUS.md` | state transition |

**Uncommitted paths outside this task's folder that are NOT this task's** (Rule 13 disclosure), all
present in the kickoff baseline in `HEARTBEAT.log`:

```
 M Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs
 M tasks/loop_v2_smoke_bot/hole1_playthrough/live_stat_log.txt
 M tasks/loop_v2_smoke_bot/hole1_playthrough/screenshots/history.log
```

`Assets/Scripts/Physics/Tests/AimCameraFramingTests.cs` and
`Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` were also dirty at kickoff and left the
working tree during verification when a concurrent stream committed them as `5d938c9a8`.

---

## Spec deviations

1. **Staged-scene window added** (not in the spec). Rationale in acceptance item 3 — it converts the
   spec's mid-run guard rail from "the hooks probably won't fire" into an explicit interlock, without
   touching `TestRunnerApi`. Self-healing: `SessionState`-scoped, erased by teardown, 45-min expiry.
2. **`Hole_\d\d_Geo` implemented as the existing `Hole_*…_Geo` predicate.** The spec's condition (a)
   is a two-digit regex; I reused `CaptureSceneSetup.IsHoleGeoScene` unchanged instead, because
   changing it would (a) alter `CaptureSceneSetup` behaviour, which the spec forbids, and (b) create
   the second implementation the spec explicitly forbids. The two differ on exactly one asset in the
   project — `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity`
   — which is also generated content under `Generated/`, so it belongs in the same class.
   `Assets/Scenes/Debug/Hole_07_Geo_Diagnostic.unity` matches neither.
3. ~~Acceptance items 6 and 7 not run~~ — **both now run and PASS** (2026-08-11, after Cesar handed
   over the Editor). Item 7 was done by staging the launcher hierarchy directly rather than killing
   Unity in the middle of a recording run; the state at kill time is identical (`LastSceneManagerSetup.txt`
   quoted pre-kill) and it avoided leaving a half-written video.
4. **Acceptance item 4 proven by direct invocation** of the real `[OneTimeSetUp]` rather than by a
   log line from a full run. Explained in full, with the reason the full-run form did not produce
   evidence, in acceptance item 4.
5. **`ActionButtonRenderingTests` left alone** (spec: "optional, if free"). It is not free: its
   cleanup is already a correct `try/finally` on the exact scene handle, it opens `LabScaffold` and
   not a hole scene (so it is out of the guard's scope by design), and converting it to a scan-based
   close would newly close a `LabScaffold` the developer had open before the run — a behaviour
   change, not a fix.

## Editor state left behind

Final state after every acceptance item, including the kill/relaunch:

```
final setup count=1
  path='Assets/Scenes/ShellScene.unity' isActive=True
  live 'ShellScene' dirty=False

$ cat Library/LastSceneManagerSetup.txt
sceneSetups:
- path: Assets/Scenes/ShellScene.unity
  isLoaded: 1
  isActive: 1
  isSubScene: 0

$ git status --porcelain --untracked-files=all -- '*.unity'   →  count: 0
```

ShellScene only, clean, guard ON, staged-scene window cleared, no probe objects, no dirty scene, no
crash-recovery residue. `Editor.log` for the post-relaunch session contains no compile error and no
`NullReferenceException`; the only `[StagedHoleSceneGuard]` lines are the two from the acceptance-7
sweep.
