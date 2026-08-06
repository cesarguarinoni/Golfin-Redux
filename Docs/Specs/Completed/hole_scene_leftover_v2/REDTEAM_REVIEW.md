# Red-Team Review — `hole_scene_leftover_v2`

**Reviewer:** golfin-redteam-reviewer
**Timestamp:** 2026-08-07 07:25 JST
**Verdict:** `ARCHITECT_REVIEW_PASS`

Tier-2 editor-tooling task (C# editor scripts only). No Figma/video/mesh/scene surface —
Rules 16/17/18/19/21 N/A. Evidence is textual (code diff + live reflection + git), which I
re-derived myself rather than carrying forward the reviewer's PASS.

## Independent capture / probe angle

No visual angle exists for a wiring fix. My independent evidence is a **read-only reflection
probe** run in the live editor (`RT_Probe`, 07:24:27 JST) that (a) proves the editor assembly
compiled with the new members and (b) exercises `IsHoleGeoScene` directly. The probe compiling
against `Assembly-CSharp-Editor` and executing is itself the compile-clean proof.

`IsHoleGeoScene` live results (re-derived, not read from report):
- `"Hole_06_Geo"` → True · full `.../Hole_06_Geo.unity` path → True
- `"ShellScene"` → False · `Assets/Scenes/Physics/LabScaffold.unity` → False
- `null` → False · `""` → False · `"MyHole_01_Geo"` → False
Correctly scoped and null-safe. LoopV2 `SetupKey=LoopV2SmokeBotMenu.SceneSetup`,
`CleanupKey=LoopV2SmokeBotMenu.Cleanup`; `LaunchDirectLab` present (compiled).

## Attack 1 — "forced EnterPlaymode invalidated Gate 1" → REFUTED

The concern: the reviewer forced `EnterPlaymode()` because `delayCall` stalled under MCP, so
maybe only the inner methods (not the wiring) were proven. Re-derived from the actual source:

- `SmokeRunner2fMenu.Run()`: `CaptureSceneSetup.Capture(SetupKey)` is **line 36** (synchronous),
  `SessionState.SetBool(CleanupKey,true)` line 39, `OpenLabAndHole()` line 41, `Arm()` line 42 —
  ALL synchronous and BEFORE `EditorApplication.delayCall += EnterPlayMode` at line 43.
- The deferred callback `EnterPlayMode()` (lines 106-116) contains **only** an `isPlaying` guard +
  `EditorApplication.EnterPlaymode()`. Nothing else.
- `Restore(SetupKey)` fires at **line 156** in the `EnteredEditMode` branch — triggered by the
  play-EXIT transition, independent of how play mode was ENTERED.

So the Capture/Restore sequencing does NOT live in the deferred step; a forced `EnterPlaymode()`
is byte-equivalent to what the wrapper would have called and gates nothing on the evidence. For
**LoopV2 (the actually-new wiring) there is no delayCall at all** — `Launch()` calls
`EnterPlaymode()` synchronously at line 561, and the reviewer's Gate 2 ran the full bot scenario
to self-exit with the new `EnteredEditMode` branch firing (`LoopV2SmokeBotMenu.cs:690`). Wiring
proven, not just inner methods.

## Attack 2 — "run 2 must be fresh with the leftover re-staged" → SATISFIED (asymmetry is the proof)

SPEC §6 Gate 1 specifies the "Excluding staged hole scene" line on **run 1 only**. That asymmetry
IS the resurrection-cycle proof: run 1 had `Hole_06_Geo` staged (→ Excluding logged → snapshot
excludes it → CloseStagedHoleScenes removes it); after run 1 the leftover is GONE, so run 2's
`BEFORE setup` is `LabScaffold` alone and its Capture has nothing to exclude (no log). Under the
OLD bug, run 1's Restore would have re-opened the hole and run 2 would show it again. Reviewer's
log matches exactly: Run 1 Excluding + clean; Run 2 BEFORE=LabScaffold-alone, no Excluding, clean.
Run 2 was a separate `ExecuteMenuItem` call. Gate met.

## Attack 3 — over/under-match, double-restore, stale resurrection → REFUTED

- **Filter scope:** live `IsHoleGeoScene` results above show no false-positive on real user scenes.
- **Three independent cycle-breakers:** Capture excludes hole entries (CaptureSceneSetup.cs:67-71);
  `CloseStagedHoleScenes` actively closes any open hole scene on every Restore (177-188); Restore
  filters stale hole entries from pre-fix snapshots (137-141). Any one breaks the cycle; all three present.
- **Double-restore:** all four launcher key pairs distinct — `SmokeRunner2eMenu.*`,
  `SmokeRunner2fMenu.*`, `VersusHudCaptureMenu.*`, `LoopV2SmokeBotMenu.SceneSetup`/`.Cleanup`
  (verified in all 4 source files + live reflection). Each handler early-returns unless its own
  CleanupKey is set; only one launcher arms per run. No collision.
- **Degenerate zero-entry:** CaptureSceneSetup.cs:91-97 erases key + logs.

## SPEC §6 acceptance — re-run independently (no carry-forward)

| Gate | How I verified | Result |
|---|---|---|
| 1 Resurrection cycle broken | Code: 3 cycle-breakers + delayCall analysis (Attack 1/2). Filter behavior via live reflection. | PASS |
| 2 LoopV2 hierarchy restore | Code: Capture+arm L526-527, Restore in new `EnteredEditMode` branch L682-692 gated on own key; no delayCall in path. | PASS |
| 3 Stale-snapshot defence | Code: `IsHoleGeoScene(e.path)` skip L137-141; helper verified correct via live reflection. | PASS |
| 4 Zero `.unity` diffs | `git diff --name-only HEAD -- '*.unity'` → empty (ran myself). | PASS |

## SPEC §5 traps — re-derived from the actual diff

Never-save (CloseScene(s,true) only; diff touches 0 `SaveScene` lines; pre-existing
`StripSerializedHost` untouched) · own-CleanupKey (4 distinct keys) · untitled refusal AFTER hole
filter (L67-71 then L73) · exactly one `[DidReloadScripts]` (L628; other grep hits are comments) ·
`IsHoleGeoScene` shared 3 ways (L67/L137/L183) · degenerate path (L91-97) · `LaunchDirectLab`
compiles + mirrors `Launch()`. All PASS.

## Working-tree attribution & cleanliness

`git status` outside the task folder: only the 2 authorized `.cs` files, plus the parallel
Architect work `Docs/TellCode.md` + `Docs/Specs/Active/map_view_playable_area/SPEC.md` — correctly
NOT attributed to this task. Zero `.unity` diffs. `M_Splash{Droplet,Foam,Ring}.mat` all at
`m_CustomRenderQueue: 3100`. No font-SDF-atlas or bot `history.log` residue (reviewer's reverts
stuck). Editor: `IsPlaying=false, IsCompiling=false`, ShellScene alone (not dirty), no staged
`Hole_NN_Geo`. My probe was read-only and left state unchanged.

## Screenshot posture

Canonical `screenshots/gate_test_clean_2026-08-07.png` is a two-tone blur that substantiates
nothing; it clears Rule 14's 900px floor mechanically only and carried ZERO weight. For a
code-only editor-tooling task the substantive gate is textual, and that gate is met by stronger
evidence (diff + live reflection + git). A readable Hierarchy/Console capture requirement for this
task class is a worthwhile PIPELINE_HARDENING addendum but is not a blocker here.

## Verdict

`ARCHITECT_REVIEW_PASS` — genuinely attempted to break across three vectors and could not. Every
gate and trap re-derived from primary source. Advancing to Cesar.
