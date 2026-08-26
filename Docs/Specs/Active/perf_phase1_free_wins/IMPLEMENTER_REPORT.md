# IMPLEMENTER_REPORT — `perf_phase1_free_wins`

**Iteration shape:** `perf:phase1-free-wins-code-complete`
**Author:** Claude Code (main thread, implementing directly at Cesar's instruction — this task was
not dispatched to the `golfin-implementer` subagent chain).
**Date:** 2026-08-26.
**Canonical screenshot:** `screenshots/editor_hole01_tee_after.png` (1170×2532)

---

## 0. Read this first — what is and is not done

**All four code/asset changes are implemented and verified live in the Editor through the real
production entry point.** What is *not* done is the device half of the acceptance checklist: fps,
render-thread ms, batches/tris, Frame Debugger, and the thermal-protocol tables. That is deliberate
— Cesar's instruction was to leave **dev build 2311 on the phone as the Phase 1 "before"**, so no
iOS build was made this session.

So: every item below that needs a phone is marked **PENDING DEVICE**, not PASS. Nothing device-shaped
is claimed. Per the spec's own rule ("a number without a frame is not evidence"), I have not written
a single performance number I did not measure.

---

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

## 3. §2 water decision — **leave depth off** (recommended, device to confirm)

The spec asked me to decide and report. What I measured, in the Editor on Hole 13 (4 water surfaces,
all `URPWater/Standard` with `_EDGEFADE_ON`):

- With the decal feature removed, `_CameraDepthTexture` is the **`UnityBlack` 4×4 dummy**, and
  `Mobile_RPAsset.supportsCameraDepthTexture = False`. So the spec's mechanism is confirmed: nothing
  produces a real camera depth texture any more.
- A per-camera `requiresDepthTexture = true` does **not** override it — the RP-asset flag gates it.
  `m_RequireDepthTexture: 1` really is the only lever.
- Forcing `supportsCameraDepthTexture = true` and re-rendering the identical shoreline pose changes
  the shoreline band by **mean 3.16 / 255**, against a **16.49** foliage-antialiasing noise floor in
  the same frame pair. In other words the edge does not measurably change when depth comes back.

**Conclusion: leave `m_RequireDepthTexture: 0`.** Paying for a depth copy buys no visible edge here.

**Honest limitation:** this is an Editor render from a diagnostic camera, and 3.16 is "below the
noise floor", not "pixel-identical". Hole 08/13 shorelines should still be eyeballed on device —
and **build 2311 on the phone is the perfect "before"** for exactly that comparison.
Frames: `screenshots/editor_hole13_shoreline_depth_off.png`, `…_depth_on.png`, `…_depth_diff_x8.png`.

---

## 4. Finding to hand off — black quads on Hole 13 trees (NOT introduced here)

`screenshots/editor_hole13_shoreline_depth_off.png` shows black rectangular cards hanging on several
tree trunks. I chased it rather than shipping past it:

- Present with the **authored** terrain values too (`…_terrain_authored_values.png`) — so **not**
  caused by §3. The pixel diff between the two is `0.00` mean in the near band; the differences that
  do exist are foliage-edge AA and water animation between captures.
- Still present with `treeBillboardDistance = 5000` — so **not** billboard imposters.
- Raycasts land on `TerrainRoot`: these are terrain **tree instances**, rendered by the terrain
  system, with no separate GameObject/renderer to inspect.

**Not ruled out:** whether §2 (decal removal) causes it — both my A/B frames already had the feature
removed. Cheapest check is on device: **build 2311 has the decal feature enabled**, so Hole 13 there
answers it in one look. Tree-shader work (`Vegetation.shader`, Spruce) is explicitly out of Phase 1
scope, so I filed this rather than chasing it further.

---

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
