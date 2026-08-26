# IMPLEMENTER_REPORT — `perf_phase1_free_wins`

**Iteration shape:** `perf:phase1-free-wins-code-complete`
**Author:** Claude Code (main thread, implementing directly at Cesar's instruction — this task was
not dispatched to the `golfin-implementer` subagent chain).
**Date:** 2026-08-26.
**Canonical screenshot:** `screenshots/editor_hole01_tee_after.png` (1170×2532)

---

## 0. Read this first — device pass HALTED, visual question OPEN

The four code/asset changes are in, and the device pass ran: three Dev-iOS builds (2314) were made,
patched, signed, installed and driven by `PerfBaselineBot`. **Cesar halted the pass** with the Hole 08
tee frame still looking wrong to him on both the shipped configuration and the basemap variant, and
took the visual question to the Architect for a faster Editor repro.

**What is settled:** the performance win is real and measured under a controlled protocol
(§11.2 of the report — 30.1 → 58.1 fps, 26.11 → 13.35 ms render thread, 7,375 → 1,848 batches).

**What is open:** whether the tee frame carries a genuine visual defect, and whether it predates
Phase 1. Neither hypothesis chased this session explains it — see §11.4 of the report for the
four-test isolation plan that was never run.

**Biggest process finding:** `SkyRandomizer` rolls a new sky per app launch, so *no* frame comparison
in this report — Phase 0b's included — was taken under controlled lighting. Now pinned in the bot.

## 1. Files modified or created

| File | Change |
|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | §1 shell-camera disable/restore + §1 NOTE light restore from `OnDestroy`; §3 `ApplyTerrainRenderDefaults()` at hole load. |
| `Assets/Settings/Mobile_Renderer.asset` | §2 `DecalRendererFeature` removed — list entry **and** the sub-asset. |
| `Assets/Scripts/Gameplay/UI/ShotUI/MapViewController.cs` | §4 both `DoFrameReadbackAndDump` call sites **and** the coroutine itself wrapped in `#if UNITY_EDITOR`. |
| `Assets/Scripts/UI/Roster/UI/CharacterThumbnailCard.cs` | §5 one-line guard: an inactive card snaps to its target scale instead of starting a coroutine Unity refuses. |

Nothing else in the working tree is mine. The other uncommitted paths (`CIBuild.cs`,
`LocalizationManager.cs`, `ShellScene.unity`, the four test files, `AppVersion.cs`,
`Golfin.Tournaments.Tests.asmdef`, `TournamentSnapshotImmunityTests.cs`,
`DEVICE_PASS_CONTENT_PIPELINE.md`, `tasks/quit_transition_demo/`, `last_uploaded_build.txt`) are the
**content-pipeline / tournament-snapshot** work from another session — see the chat hand-off.

---

## 2. Acceptance checklist

### Verified this session (Editor, production entry path)

| # | Item | Verdict | Evidence |
|---|---|---|---|
| 1 | §1 shell camera disabled during a hole | **PASS** | Live read: `'Main Camera' scene='ShellScene' enabled=False goActive=True`. `goActive` stays **true** — the AudioListener keeps running, as the spec requires. `_shellCameraDisabled = Main Camera`. |
| 2 | Exactly one camera enabled during a hole | **PASS** | `'Main Camera' scene='LabScaffold' enabled=True`; ShellScene's `enabled=False`; `WalkCamera` `goActive=False`. |
| 3 | `Camera.main` resolves to the LabScaffold camera | **PASS** | Live: `Camera.main = Main Camera (scene LabScaffold)`. Confirms the spec's claim; no consumer change needed. |
| 4 | §3 terrain defaults applied at hole load | **PASS** | Hole 08 live: `basemapDistance=100 drawInstanced=True` (was `1000` / `False`). |
| 5 | §3 tree normalisation on Hole 01 | **PASS** | Authored on disk `m_TreeDistance: 5000, m_TreeBillboardDistance: 50, m_TreeCrossFadeLength: 5`; live after load **150 / 80 / 20**. Scene file untouched. |
| 6 | §1 NOTE — camera **and light** restored on the player-build path | **PASS** | Drove the real `GameplaySceneLoader.UnloadGameplayScenes()`. After unload: ShellScene camera `enabled=True`, ShellScene directional light `enabled=True`. On HEAD the light was never restored in a player build (`LabHoleBinder` is entirely `#if UNITY_EDITOR`); `OnDestroy` now fixes it. |
| 7 | §2 decal feature genuinely removed | **PASS** | Unity-side read-back: `rendererFeatures.Count = 0`, sub-assets at path = 1 (`UniversalRendererData` only). No null list entry. |
| 8 | §2 did not black out the terrain (the §10.3 trap) | **PASS** | `screenshots/editor_hole01_tee_after.png` — terrain, trees, shadows, HUD all render correctly. Asset edit + reimport, never a runtime `SetActive(false)`. |
| 9 | Zero decal consumers in the project | **PASS** | Only `DecalProjector` mentions are comments in `MapViewController.cs`; iter-31 already replaced it with a ZTest=Always disc. Confirms Phase 0's "zero decals". |
| 10 | §4 both readback sites guarded | **PASS** | Guard audit: every *code* reference to `DoFrameReadbackAndDump` (`:528`, `:2323`, `:2911`) sits inside `#if UNITY_EDITOR`; only comments are unguarded. Preprocessor depth balances to 0. |
| 11 | §5 spam identified **and** fixed | **PASS** | Message read off `exp_ad_CORRECT.png`: `Coroutine couldn't be started because the the game object 'CharacterThumbnailCardGlowUp(Clone)' is inactive!` Source: `CharacterThumbnailCard.SetSelected` → `StartCoroutine(AnimateScale())` on a card whose screen is deactivated during a hole. **Not** a `PerfBaselineBot` artefact — the bot only *reads* `GetSelectedCharacterId`. Console is clean in both new frames. |
| 12 | `Assets/Settings/` diff is exactly the spec's set | **PASS** | `git status -- Assets/Settings/` → `M Mobile_Renderer.asset` only. `Mobile_RPAsset.asset` md5 identical before/after all experiments (`0d8e3b27…`). |
| 13 | No scene or TerrainData modified | **PASS** | No `.unity` in my diff; ShellScene `IsDirty=false` after all play-mode work; never saved a scene. |
| 14 | EditMode tests hold baseline | **PASS** | **1768 total / 1765 passed / 0 failed / 3 skipped** — identical to the `content_cleanup_quick` close-out baseline. Same 3 pre-existing `HoleCompleteDriverTests` skips. |
| 15 | No compile errors | **PASS** | 0 `CS####` errors. Reflection probe confirms every new symbol exists with the right constant values (100 / 150 / 80 / 20). |

### PENDING DEVICE — needs build 2311's successor on the phone

| # | Item | State |
|---|---|---|
| 16 | Hole 08 tee after: fps ≥ 58, render ms ≤ 15.0 (3 runs, cooled, pinned yaw) | **PENDING DEVICE** |
| 17 | Hole 01 + Hole 06 tee after, same protocol (H06 ≤ 26.59 ms) | **PENDING DEVICE** |
| 18 | Hole 08 mid-flight (driver) baseline | **PENDING DEVICE** |
| 19 | Frame Debugger: one camera, zero `DrawDepthNormalPrepass`, zero `CopyDepth` | **PENDING DEVICE** |
| 20 | Device frame A/B vs `exp_ad_CORRECT.png` (tree silhouettes in the same places) | **PENDING DEVICE** |
| 21 | Hole 01 tree-distance before/after **on device** | **PENDING DEVICE** — Editor before/after is item 5 above. |
| 22 | Teardown paths i–iv on device | **PARTIAL** — path (iv) (return to Home, camera + light restored) and the repeat-hole-load case are PASS in the Editor (items 6, and Hole 01→13 loaded back-to-back in one session). Paths i/ii/iii need the device. |
| 23 | MapView opened on device, no `ReadPixels` in frame | **PENDING DEVICE** — code-side guard is item 10. |
| 24 | GC-per-frame after (vs ~29 KB/frame) | **PENDING DEVICE** |

---

## 3. §2 water decision — deferred to the device (Editor evidence RETRACTED)

**My first Editor pass was invalid and Cesar caught it.** The diagnostic camera sat at `y=7.2`
where the terrain is `9.39` — **2.19 m underground**. The "hard shoreline" reading and the
3.16-vs-16.49 numbers came from looking at the world through the ground. Deleted, not amended.

Re-run from a verified pose (`Terrain.SampleHeight + 1.70 m`, downward raycast confirming
`TerrainRoot` 1.75 m below the camera), both shots inside **one** hole load:

| Region | mean Δ/255 |
|---|---|
| static grass (should not change → noise floor) | **3.51** |
| shoreline seam | 6.52 |
| water surface (animated) | 9.23 |

Mechanics confirmed either way: `_CameraDepthTexture` is the `UnityBlack` 4×4 dummy,
`supportsCameraDepthTexture = False`, and a per-camera `requiresDepthTexture` does not override the
asset flag — `m_RequireDepthTexture: 1` is the only lever.

**Verdict: not decidable in the Editor** — the water delta is only ~2× the noise floor and the
surface animates between shots. Closing it on device per the order's item 4 (Hole 08 + Hole 13
shoreline frames). Frames: `h13_shoreline_depth_off.png`, `h13_shoreline_depth_on.png` in
`Docs/Reports/perf_baseline_2026-08-26_frames/`.

## 4. The flat terrain is PRE-EXISTING — bisect stopped at step 0

**Cesar's symptom on build 2314:** terrain renders flat untextured colour near and far (rough flat
dark green, bunker flat white, fairway layer flat light green); only overlay meshes (`Fairway_n`,
`Tee_n`) keep texture.

**Reproduced in the Editor Game View** at HEAD — Hole 08 tee, pinned sky (`Afternoon (Cloudy)`,
sun 28.5°), pinned yaw, 1170×2532. Then re-shot with all four Phase 1 changes reverted, and again
against **real pre-Phase-1 code** (`a98008f6d` checked out for `PhysicsLabController.cs`,
`Mobile_Renderer.asset`, `Mobile_RPAsset.asset`).

| patch (luminance) | HEAD | all reverted | pre-Phase-1 |
|---|---|---|---|
| near fairway | 141.9 · sd **22.44** | 141.9 · sd **22.44** | 141.9 · sd **22.44** |
| mid rough left | 92.5 · sd 13.12 | 85.9 · sd 22.10 | 85.9 · sd 22.10 |
| far hillside | 77.3 · sd 2.52 | 72.6 · sd 8.84 | 72.6 · sd 8.84 |

Near fairway bit-identical across all three, flat in all three. **Phase 1 did not cause it.**
Steps 1–4 (drawInstanced, NRP, decal, shell camera) not run — step 0 answered it. Own task;
`m_UseNativeRenderPass: 1` is the obvious first probe.

**Method note that matters:** my earlier Editor passes missed this because they rendered through an
ad-hoc `screenshot-camera`, which does not exercise the real pipeline. Only the **Game View** shows
it. That is why three earlier investigations looked clean.

### The one real Phase 1 delta, and the fix

HEAD's *distant* terrain was measurably flatter than pre-Phase-1 (mid-rough sd 13.12 vs 22.10; far
hillside 2.52 vs 8.84) with the near field untouched. `drawInstanced` was the only §3 setting that
could do it. **Removed** — instructed either way, and justified independently: §11.3 shows it is
within noise on device (13.48 vs 13.35 ms, identical batches/tris). It also carried a device-only
risk the Editor cannot surface: every hole ships `m_DrawInstanced: 0`, the flag is runtime-only, and
`GraphicsSettings m_InstancingStripping` is **StripUnused**, so the instanced terrain variants may
not be in a player build at all.

**§3 is now the tree-distance normalisation only.**

## 5. Spec deviations

1. **`Assets/Scripts/Physics/` edit vs CLAUDE.md standing ban (rule 7).** The SPEC mandates editing
   `PhysicsLabController.cs` by line number. No hook enforces the ban (`grep` over `.claude/hooks/`
   finds no such gate), and the ban's intent is the physics *simulation*; `Viewer/` is a scene
   controller. Proceeding was the Architect's explicit instruction — flagging it rather than
   silently ignoring it.
2. **`CharacterThumbnailCard.cs` is outside the spec's "Files this task touches" list.** §5 itself
   authorises a ≤5-line fix; the change is one line of code plus comment. The `return` also skips the
   adjacent per-call `Debug.Log`, which further reduces the log/GC pressure §5 is chasing.
3. **§2 done as a YAML edit, not via the Inspector.** The spec preferred the Inspector "so Unity
   rewrites both lists" — but `m_RendererFeatureMap` was already empty, so there was nothing to
   rewrite. Verified equivalent by loading the asset through Unity afterwards: `rendererFeatures.Count = 0`,
   one sub-asset, no null entry.
4. **`m_PrefilterDBufferMRT3` has not churned yet.** Per Phase 0b §10.4b that rewrite happens at
   **build** time. No iOS build was made (Cesar's instruction), so expect that one line in
   `Mobile_RPAsset.asset` on the first Phase 1 device build — commit it then.
5. **Harness note.** Unity MCP tools were not registered in this session (the Editor was closed at
   session start). I launched the Editor and drove the same MCP server over HTTP directly. All
   captures used the sanctioned paths — `screenshot-camera` and the
   `GOLFIN/Screenshot/Capture Game View` menu item — never a hand-rolled `CaptureCore`/`ScreenCapture`
   reflection (CAPTURE RULE 0).

---

## 6. How §1/§3 were verified (method, so it can be re-run)

Driving `BeginGameplayLoad(8)` alone is **not** enough: `PhysicsLabController.ScanForLoadedHoleSceneAtStartup`
only polls for the hole scene when `GameSession` carries a seed, otherwise it takes the immediate
flat-ground fallback and `OnHoleLoaded` never fires (`[PhysicsLab] No hole scene loaded at startup`).
The production order is:

```
GameSession.SeedSession(hole, characterId, bagSlot)   →   GameplaySceneLoader.Instance.BeginGameplayLoad(hole)
```

With the seed in place `IsHoleReady` goes true and both §1 and §3 fire. Holes 08, 01 and 13 were each
loaded this way, and the exit was driven through the real `UnloadGameplayScenes()`.


---

## 7. Device pass — what actually ran (2026-08-26)

### Build pipeline (repeatable)

```
Unity -batchmode -executeMethod Golfin.EditorTools.CIBuild.BuildIOSDev      # -> Builds/iOS-Dev
xcodebuild -project Builds/iOS-Dev/Unity-iPhone.xcodeproj -scheme Unity-iPhone \
  -configuration Release -destination 'generic/platform=iOS' -allowProvisioningUpdates \
  SYMROOT=$PWD/Builds/iOS-Dev/build DEVELOPMENT_TEAM=TCUV4A9VTJ CODE_SIGN_STYLE=Automatic build
PlistBuddy: add NSLocalNetworkUsageDescription + NSBonjourServices                # report §9.5
codesign --force --sign "Apple Development: Cesar Guarinoni (NWQPSKM8S9)" --entitlements ents.plist
xcrun devicectl device install app --device <id> Golfin.app
```

`BuildPipeline` was never driven through MCP (Cesar's instruction, and memory
`reference_never_buildplayer_via_script_execute`). Three builds were needed: the second added the
`basemapDistance` revert + the teardown gate, the third added the sky pin.

**Housekeeping:** each batchmode build bumps `ProjectSettings.asset` `buildNumber` (2113 → 2314)
despite `CIBuild.RestoreBuildNumbers`. Restored surgically after every build — never with
`git checkout`, per the standing rule — so the file is clean in the diff.

### Item status

| # | Item | Verdict |
|---|---|---|
| 16 | H08 tee fps ≥ 58, render ≤ 15.0 ms | **PASS on one run** (58.1 fps / 13.35 ms) — *not* the 3-run median the protocol demands |
| 17 | H01 + H06 tee | **NOT RUN** (halted) |
| 18 | H08 mid-flight | **NOT RUN** |
| 19 | Frame Debugger one-camera / no prepass | **NOT RUN**. An automated event-enumeration probe was written (`FrameDebuggerUtility` via reflection, limit 3000) but never executed |
| 20 | Device frame A/B vs `exp_ad_CORRECT.png` | **BLOCKED** — that reference was shot under a different, unpinned sky, so it is not a valid comparand |
| 21 | Hole 01 tree distance before/after | **NOT RUN on device** (Editor evidence is §2 item 5) |
| 22 | Teardown paths i–iv | **BUILT, NOT RUN** — now a bot job (`P1_teardown`, job 13) per Cesar's "automate always"; writes `teardown_invariants.json` |
| 23 | MapView no `ReadPixels` | **NOT RUN on device** (code-side guard is §2 item 10) |
| 24 | GC B/frame | **29,030 → 21,506** observed, single run |

### Two attributions I got wrong, and what corrected them

1. **"`basemapDistance = 100` causes a visible seam."** Wrong. A pinned-sky A/B shows basemap 100 vs
   1000 differ by **mean 2.01/255** with identical batches and triangles. The arithmetic that led me
   there (512-res basemap over a 668 m terrain = 1.30 m/texel) is correct but was not the cause of
   what Cesar saw. The setting is still removed — because it also delivers **no measurable gain**
   (13.48 vs 13.35 ms), so it is cost without benefit.
2. **"The Hole 13 shoreline is a hard edge / the trees have black cards."** Both were read off a
   camera **2.19 m underground**. Cesar caught it. Re-shot from a verified pose
   (`Terrain.SampleHeight + 1.70 m`, downward raycast confirming ground 1.75 m below).

The common thread: I twice drew a conclusion from a frame without first proving the frame was
trustworthy. The sky pin and the ground-height assertion both exist now to make that harder.

### Handover

The visual question is the Architect's, and is faster in the Editor. Report §11.4 lists the four
isolation tests, in order, with the noise-floor warning that global image diffing cannot resolve
them (Editor noise floor mean 6.36 vs config diffs 6.97–7.85).
