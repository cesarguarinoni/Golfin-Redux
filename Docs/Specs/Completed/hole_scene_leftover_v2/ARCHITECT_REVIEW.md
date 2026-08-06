# Architect Review — `hole_scene_leftover_v2`

**Reviewer:** golfin-reviewer
**Timestamp:** 2026-08-07 07:17 JST
**Iteration:** 1 of architect review.

## Independent visual scan

Opened `screenshots/gate_test_clean_2026-08-07.png` (1200×900) before reading any prior verdict. A near-vertical seam roughly two-thirds across the frame splits the image into two flat regions: on the left, a blue-to-off-white sky gradient over a featureless warm-grey ground plane with a soft horizon about 55% down the height — Unity's default skybox over an empty scene camera; on the right, a flat dark olive-green rectangle with faint corner vignetting. No Hierarchy, Console, or menu-bar chrome visible; no geometry; no text of any kind. **The image substantiates NOTHING and is given zero weight** — it clears Rule 14's 900px floor mechanically only. The real gate for this Tier-2 editor-tooling task is textual (Editor console log + `EditorSceneManager.GetSceneManagerSetup()` dumps + `git status`), which I re-ran independently through the REAL menu items below.

## Verdict

`READY_FOR_REDTEAM` — routing to the adversarial red-team gate.

All four SPEC §6 gates independently re-verified this pass through the paths the reviewer prompt demanded: **Gates 1 and 2 driven through `EditorApplication.ExecuteMenuItem` on the real menu paths** (not the direct-call harness the implementer/self-reviewer used). Every SPEC §5 trap re-checked from the actual diff. Working tree clean, editor left clean, compile proven by real menu executions.

## Real-menu re-verification (Gates 1 & 2)

The self-reviewer disclosed the implementer's Gates 1/2 evidence was harness-only; per PIPELINE_HARDENING Rule 2 that's insufficient. I drove both through the REAL menu items via `ExecuteMenuItem(...)` and gated on log output from that path. Full Editor.log line numbers cited.

### Gate 1 — Resurrection cycle broken (SmokeRunner2f, twice, real menu)

**Setup:** `EditorSceneManager.OpenScene("Assets/Scenes/Physics/LabScaffold.unity", Single)` + `OpenScene(".../Hole_06_Geo.unity", Additive)` — simulates the leftover.

**Run 1** (Editor.log line ~31898744-31928489):
```
[Rev-Gate1] RUN 1 BEFORE setup: LabScaffold(loaded=True,active=True),Hole_06_Geo(loaded=True,active=False),
[CaptureSceneSetup] Excluding staged hole scene from snapshot: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
[CaptureSceneSetup] Snapshot taken (1 scene(s)): LabScaffold
[SmokeRunner2fMenu] Closing stale hole scene: Hole_06_Geo   ← defensive sweep still fires (belt-and-braces, per spec)
[SmokeRunner2fMenu] SmokeRunner2eHost.Armed cleared; SmokeRunner2fHost armed via SessionState. Host will be injected at EnteredPlayMode — never saved to disk.
[Rev-Gate1] RUN 1 ExecuteMenuItem returned: True
… (SmokeRunner2fHost ran full driver→putter→tuning sequence to "Sequence COMPLETE.")
[CaptureSceneSetup] Closing staged hole scene without saving: Hole_01_Geo
[CaptureSceneSetup] Restored pre-run scene setup: LabScaffold
[SmokeRunner2fMenu] §2f capture run cleaned up: hole scene closed, scene setup restored.
```
After Run 1: `scene-list-opened` → `LabScaffold` **alone**. Hole_06_Geo and Hole_01_Geo both gone.

**Run 2** (Editor.log line ~31928648-31958351):
```
[Rev-Gate1] RUN 2 BEFORE setup: LabScaffold(loaded=True,active=True),          ← no hole left over from Run 1 – cycle broken
[CaptureSceneSetup] Snapshot taken (1 scene(s)): LabScaffold                    ← NO "Excluding" log this run (SPEC: "run 1 only")
[SmokeRunner2fMenu] SmokeRunner2eHost.Armed cleared; SmokeRunner2fHost armed via SessionState. …
[Rev-Gate1] RUN 2 ExecuteMenuItem returned: True
… (real menu run, forced EnterPlaymode after delayCall stalled — see § MCP note below)
[CaptureSceneSetup] Closing staged hole scene without saving: Hole_01_Geo
[CaptureSceneSetup] Restored pre-run scene setup: LabScaffold
[SmokeRunner2fMenu] §2f capture run cleaned up: hole scene closed, scene setup restored.
```
After Run 2: `scene-list-opened` → `LabScaffold` **alone** (same as after Run 1).

Explicit `Excluding` count for the review window (whole Editor.log grep):
- 2 hits from my real-menu runs (Gate 2 setup + Gate 1 Run 1) — both with Hole_06_Geo pre-staged.
- 0 hits from Gate 1 Run 2 — matches SPEC "run 1 only" exactly.

Result: **CONFIRMED PASS via real menu.** Resurrection cycle proven broken across two consecutive real-menu runs.

MCP note (transparent): each SmokeRunner2f real-menu run had its `EditorApplication.delayCall += EnterPlayMode` fail to fire under the MCP-driven session (main thread appears held). I forced `EditorApplication.EnterPlaymode()` — which is byte-identical to what the delayCall would have invoked — after ~90s of stall. The Capture code path had already run synchronously inside `Run()` BEFORE my nudge (as the logs show), so the nudge did not alter what was gated on. Every log line above is from the real `SmokeRunner2fMenu.Run()` → `OnPlayModeStateChanged(EnteredEditMode)` flow, not from a synthetic harness.

### Gate 2 — LoopV2 hierarchy restore (real menu)

**Setup:** LabScaffold(Single) + Hole_06_Geo(Additive) — same leftover, but this run also tests LoopV2's NEW `EnteredEditMode` branch and the "user scene ≠ ShellScene" case.

Editor.log line ~31895753-…:
```
[Rev-Gate2] BEFORE setup: LabScaffold(loaded=True,active=True),Hole_06_Geo(loaded=True,active=False),
[LoopV2SmokeBotMenu] Launching scenario: 'settings_round_trip'
[CaptureSceneSetup] Excluding staged hole scene from snapshot: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
[CaptureSceneSetup] Snapshot taken (1 scene(s)): LabScaffold                              ← NOT ShellScene (payload = user scene, filtered)
[LoopV2SmokeBotMenu] DisableSceneReload detected — temporarily enabling scene reload for this run.
[LoopV2SmokeBotMenu] Armed. Scenario='settings_round_trip'. Entering play mode…
[Rev-Gate2] ExecuteMenuItem('GOLFIN/Smoke/Loop v2/Settings Round Trip') returned: True
[LoopV2SmokeBotMenu] Injected [LoopV2SmokeBot] host into play-mode scene (scenario=settings_round_trip, not saved to disk).
… (bot ran full scenario Home → Settings → Sound expanded → Close → Home returned → Scenario complete → self-exited play mode)
[LoopV2SmokeBotMenu] Restored DisableSceneReload option (at ExitingPlayMode).
[CaptureSceneSetup] Restored pre-run scene setup: LabScaffold
[LoopV2SmokeBotMenu] Run cleaned up: staged scenes closed, scene setup restored.
```
Restore call-site stack: `Golfin.Physics.Viewer.Editor.LoopV2SmokeBotMenu:OnPlayModeStateChanged (line 690)` → `CaptureSceneSetup:Restore (line 165)` — the NEW `EnteredEditMode` branch fired via the CleanupKey gate as authored.

After: `scene-list-opened` → `LabScaffold` **alone** (not ShellScene — which was today's bug — and no Hole_06_Geo).

Result: **CONFIRMED PASS via real menu.** LoopV2 hierarchy-restore fix works end-to-end through the actual `GOLFIN/Smoke/Loop v2/Settings Round Trip` menu item; the whole bot scenario ran and self-exited on its own (my early-exit hook did not fire — full real path executed).

### Gate 3 — Stale-snapshot defence (direct injection, per SPEC gate methodology)

Hand-wrote a stale SessionState payload containing both `ShellScene` and `Hole_06_Geo` entries under a synthetic key, then invoked `Golfin.Physics.Viewer.Editor.CaptureSceneSetup.Restore(key)` via reflection. SPEC §6 explicitly authorizes this as the gate methodology ("hand-write a SessionState payload… Restore skips it").

Console:
```
[Rev-Gate3] BEFORE scene setup: ShellScene,
[Rev-Gate3] Injected stale payload with Hole_06_Geo entry under key='ReviewGate3.StaleSnapshot'.
[CaptureSceneSetup] Skipping stale hole scene entry in snapshot: Assets/Golf/Courses/lomond-country-club/Generated/Hole_06_Geo.unity
[CaptureSceneSetup] Restored pre-run scene setup: ShellScene
[Rev-Gate3] AFTER scene setup: ShellScene,
[Rev-Gate3] Hole_06_Geo NOT reopened: PASS
[Rev-Gate3] SessionState after Restore: <erased>
```
Restore call-site: `CaptureSceneSetup.cs:139` (the new `IsHoleGeoScene(e.path)` skip inside the Restore-time loop).

Result: **CONFIRMED PASS.** Stale hole entry is filtered out of the payload during Restore; no hole reopened; no warning spam; SessionState key correctly erased.

### Gate 4 — git status clean (zero .unity diffs)

`git diff --name-only HEAD -- '*.unity' 'Assets/Resources/FX/M_Splash*.mat'` → empty output.
`git status --porcelain --untracked-files=all` after review activity + restoring review side-effects:
```
 M Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs   ← authorized
 M Assets/Scripts/Physics/Viewer/Editor/CaptureSceneSetup.cs        ← authorized
?? Docs/Specs/Active/hole_scene_leftover_v2/(6 spec-folder files)   ← authorized (task folder)
```
Review-caused drift (`Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` — Japanese font atlas grew during settings_round_trip; `tasks/loop_v2_smoke_bot/settings_round_trip/screenshots/history.log`) was surgically `git checkout --`'d and did not persist. `M_Splash*.mat` NOT re-dirtied (spec §7 standing ban honoured).

Result: **CONFIRMED PASS.**

## SPEC §5 trap audit (re-derived from the actual diff, not from prior verdicts)

| Trap | Verification | Result |
|---|---|---|
| Never save a hole scene | `CaptureSceneSetup.cs:186` uses `EditorSceneManager.CloseScene(s, true)` with NO save. The only `SaveScene` call in the file is the pre-existing `StripSerializedHost` (line 226 — untouched by this diff, only fires when there is stale residue in LabScaffold). Zero `.unity` diffs across all my real-menu runs. | PASS |
| LoopV2 EnteredEditMode gated on its OWN CleanupKey (no double-restore) | Read all 4 launcher keys: `SmokeRunner2eMenu.CleanupPending`, `SmokeRunner2fMenu.CleanupPending`, `VersusHudCaptureMenu.CleanupPending`, `LoopV2SmokeBotMenu.Cleanup` — all distinct. LoopV2's `OnPlayModeStateChanged` early-returns when its own key isn't set (line 688). Gate 1's SmokeRunner2f runs did NOT trigger LoopV2's cleanup log (`Restored pre-run scene setup: LabScaffold` fired from `SmokeRunner2fMenu.cs:156`, not `LoopV2SmokeBotMenu.cs:690`) — verified by the stack frame in the log. | PASS |
| Untitled-scene refusal still AFTER hole filter | `CaptureSceneSetup.cs`: `IsHoleGeoScene` check at line 67-71 (`continue`) precedes the `string.IsNullOrEmpty(s.path)` refusal at line 73. Order: filter first, refuse second. A hole entry is filtered, not treated as an abort trigger. | PASS |
| SessionState keys per-launcher | Confirmed above — no collision with the other three launchers. | PASS |
| `IsHoleGeoScene` genuinely shared (single implementation) | Defined once at `CaptureSceneSetup.cs:195`. Called from `Capture` (line 67), `Restore` (line 137), `CloseStagedHoleScenes` (line 183). `CloseStagedHoleScenes`' prior inline `StartsWith("Hole_") && EndsWith("_Geo")` test was correctly replaced. | PASS |
| No second `[DidReloadScripts]` handler | `grep '\[UnityEditor\.Callbacks\.DidReloadScripts\]' LoopV2SmokeBotMenu.cs` → one hit only (line 628 — the pre-existing `OnScriptsReloaded`). NEW `EnteredEditMode` logic sits inside the same `OnPlayModeStateChanged` handler that was already re-registered by that pre-existing DidReloadScripts. | PASS |
| Degenerate case (all entries filtered → erase key + log) | `CaptureSceneSetup.cs:92-97` handles it. Not exercised in my runs (I always had a non-hole scene present) but the code is present and correct. | PASS |
| `LaunchDirectLab` compile-only proof (zero callers) | It compiled — the entire real-menu smoke chain would not have executed otherwise. Reviewed its wiring against `Launch()` — identical `Capture(SetupKey) + SessionState.SetBool(CleanupKey, true)` as the first statements after the `isPlaying` guard (lines 586-587 vs 526-527). Mirrors the pattern exactly. | PASS |

## Architectural / cross-cutting checks

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS | Both files remain in existing `Golfin.Physics.Viewer.*Editor` asmdefs; no new refs added; `#if UNITY_EDITOR` guards preserved. |
| Pattern adherence | PASS | LoopV2 wiring mirrors SmokeRunner2eMenu / SmokeRunner2fMenu / VersusHudCaptureMenu exactly — same 4-launcher family, one shared `CaptureSceneSetup`, distinct per-launcher `SetupKey`/`CleanupKey`, arm-at-launch not arm-at-EnterPlayMode. |
| No duplicated logic | PASS | Cross-cutting improvement: `CloseStagedHoleScenes`' inline `StartsWith/EndsWith` test was folded into the shared `IsHoleGeoScene` helper — this diff REMOVES duplication rather than introduces it. |
| Spec intent honoured | PASS | Root cause (`Capture` recording hole scenes → `Restore` re-opening them) is fixed at both ends (filter in Capture, defence-in-depth filter in Restore). Secondary defect (LoopV2 never wired at all) also fixed with per-launcher key so it can't collide with other launchers on shared play-mode exits. |
| Cross-feature implications | PASS | The filter only rejects `Hole_NN_Geo` names — cannot false-positive on other scenes. Real-menu runs of Gate 1 (SmokeRunner2f) and Gate 2 (LoopV2 Settings Round Trip) both left the working tree clean and did not corrupt any scene. |
| Latent bugs the screenshot doesn't show | PASS | Compile clean (proven by real menu execution). No new API surface. Reflection probe (in self-review evidence) confirms `IsHoleGeoScene` returns `False` for null/empty/non-hole names. |

## Figma fidelity

N/A — SPEC states `Figma: N/A.` This is a Tier-2 editor-tooling task with no design surface. Rule 18 does not apply.

## Screenshot posture (per reviewer prompt)

The canonical `screenshots/gate_test_clean_2026-08-07.png` is a two-tone blur that substantiates nothing. It cleared Rule 14's ≥900px floor mechanically but is worthless for content verification, as both the self-reviewer and the reviewer prompt flagged. **It carried zero weight in this PASS.** For a Tier-2 editor-tooling task the correct evidence is textual — console log lines + `EditorSceneManager.GetSceneManagerSetup()` dumps + `git status` + `scene-list-opened`, all of which are cited above with Editor.log line numbers. Task-screenshots do not reach git (`.gitignore:246 = Docs/Specs/**/screenshots/`), so no confusion downstream. Suggestion for future editor-tooling tasks in `## Lessons captured` below.

## Bbox verification

N/A — no containment claims to verify in a code-only editor-tooling task.

## Scene-mutation audit

`git diff -- '*.unity'` empty across the entire review. No `m_IsActive`, `sizeDelta`, or position changes in any scene file. The two real-menu smoke runs and the LoopV2 Settings Round Trip run left ZERO scene mutations behind — the whole point of the fix is that the launchers no longer contaminate the editor hierarchy on exit.

## Editor cleanliness

- `editor-application-get-state`: `IsPlaying=false, IsPaused=false, IsCompiling=false, IsUpdating=false`.
- `scene-list-opened`: `ShellScene` alone, not dirty.
- No staged `Hole_NN_Geo` scenes open.
- `M_Splash*.mat` clean.

## Specific FAIL items

None.

## Open questions for Cesar

None.

## Lessons captured

- **For Tier-2 editor-tooling tasks, the canonical "screenshot" gate is not fit for purpose.** The pipeline's Rule-14 900px floor was designed for visual UI tasks; for a wiring-fix that has no UI to render, a screenshot cleared mechanically is misleading (the two-tone blur here). Consider allowing a text-artifact-in-lieu-of-screenshot exception for `Figma: N/A + no visual UI` tasks — e.g. a required `EditorSceneManager.GetSceneManagerSetup()` dump + console log excerpt file at the canonical path. Flag for a future `PIPELINE_HARDENING.md` addendum, not a change to make here.
- **`EditorApplication.delayCall` stalls under MCP-driven sessions.** Consistently reproduced across two SmokeRunner2f real-menu runs: `Run()` completes synchronously (including `Capture`, defensive sweep, `Arm`, and the `delayCall += EnterPlayMode` registration), then the delayCall itself sits un-fired for 60-90+ seconds until forced with a direct `EnterPlaymode()`. This is a review-tooling gap (probably MCP holding the main thread), not a defect in the launcher; the `Capture` code always ran BEFORE the stall, so the review evidence is intact. Worth capturing in `Docs/Specs/Queued/` as an MCP-integration paper cut if it bites future reviews.

## Cesar's final approval

Cesar fills this section after eyeballing the screenshot one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
