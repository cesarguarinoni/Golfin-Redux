# [TellCode.md](http://TellCode.md) — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block. After completing, add a status line at the bottom of your task section: `✅ DONE: [date] [brief summary]`. Claude (Architect) will update this file with new instructions as needed. Handoff: `Docs/TellCode.md`.
>
> **Note (2026-04-25):** `Docs/` was reorganized. Historical entries in this file or in `Docs/Archive/TELLCODE_HISTORY.md` may reference old paths:
>
> - `Docs/DIAG/...` → now `Docs/Diagnostics/...`
> - `Docs/BACKUPS/...` → now `Docs/Backups/...`
> - `Docs/PHYSICS_RESEARCH.md`, `PHYSICS_TUNING_TARGETS.md`, `LESSONS_PHYSICS_*.md` → now under `Docs/Physics/`
> - `Docs/INVENTORY_REFERENCE.md`, `UI_HIERARCHY.md`, `PATTERNS.md`, `ARCHITECTURE_AUDIT.md` → now under `Docs/Architecture/`
> - `Docs/LESSONS_FRINGE_BORDER_MESHES.md`, `BUNKER_*`, `TEE_SKIRT_*`, `ADD_HOLE.md` → now under `Docs/Pipeline/`
> - `Docs/SURFACE_MARKER_FIX_REPORT.md`, `PHASE6_STAT_COUPLING_REPORT.md`, `SPEC_PHASE6_STAT_COUPLING.md` → now under `Docs/Physics/`
> - `Docs/generate_audit.*`, `compress_screenshots.*`, `daily_report.py`, etc. → now under `Docs/Scripts/`
>
> See `Docs/README.md` for the full index map.
>
> **History:** Completed task blocks and the long History Log live in `Docs/Archive/TELLCODE_HISTORY.md`. If you need detail on something old, check there first.

---

## 📅 ROADMAP — upcoming deliverables (planned 2026-04-26)

> Architect-tracked roadmap for the next gameplay-loop closure. Order locked: A → B → C.

**A — Shot UI polish.** Wire real Figma art + sprite assets into the existing cone hierarchy + add HUD elements (player card, hole card, wind/hole indicators, power gauge, action buttons, ball/club selectors, centerpiece ball, trail). Spec ready: `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md`. **STATUS: Parts 8.1, 8.2, 8.2.5 done; awaiting ack on 8.3.**

**B — Controls finetuning.** Two sub-tasks, sequenced:

- **B.1** Putter velocity bug — putter shoots ~100yd instead of putt-range. Likely a stat-coupling/wiring issue (StatBundle not swapping, or `PuttBaseVelocityMps` override not respected, or power scaling math wrong for putt mode). Diagnosis-first: log what `ShotInputBuilder.Build` actually returns in putt mode.
- **B.2** Surface roll resistance — ball rolls forever regardless of surface. Either `surfaces.csv` rolling-resistance values are too low across the board, or there's a units/application bug. Diagnosis-first: fire test shots on each surface, log deceleration profiles, then re-tune CSV.
- Spec for B written after Phase 8 lands.

**C — Menu → gameplay integration (superficial spec; deep dive when we get there).** Wire the existing main menu to a new Hole Picker screen, then to a runtime version of LabScaffold so pressing Play actually starts a hole. Scope:

1. **Hole Picker UI** — new scene/screen accessed from main menu's Play button. Lists 18 holes with thumbnails (probably greyed-out for unimported). Selects one → loads it.
2. **Runtime hole-load equivalent of** `LabScaffold` **+** `PhysicsLabHolePicker` — today's hole-load flow is editor-only via the picker EditorWindow. Need a runtime equivalent: a `GameplayScaffold` scene (lighter than LabScaffold — no debug UI/preset Fire button) that additively loads `Hole_XX_Geo.unity`, wires `ShotController`, `BallAnimator`, `ChaseCamera`, baked providers.
3. **Hole flow** — ball-in-cup detection (Z proximity to pin GO + speed threshold), shot counter, par tracking from hole metadata, hole-end summary panel (par/strokes/score), Next-Hole or Back-to-Menu buttons.
4. **Camera/UI flow** — ball-settled → next-shot transition (camera reframes, controller resets to Aiming, shot count increments).

- Scope deliberately stops at single-hole play — no full 18-hole round, no save state, no scoring leaderboard.
- Existing assets to leverage: `Mainmenu` prefab, `ShellScene.unity`, `LabScaffold.unity` (template for `GameplayScaffold`), `PhysicsLabHolePicker` (template for runtime hole picker logic).
- Deep-dive spec when A and B are settled.

---

## ✅ Architectural state (as of 2026-04-26)

**Pivot to baked-data sim:** merged to main 2026-04-25. All tests pass (BakedPivot 24/24, Phase 1–6 physics, RealHoleTerrainTests). Cesar's "ball into void" repro eliminated by construction. Sim reads `Assets/Resources/HoleData/Hole_XX/zones.json` + `heightmap.bytes`. Scene providers demoted to editor-only placement helpers.

**Phase F cleanup:** deleted `SceneGroundProvider`, `SceneSurfaceProvider`, `PhysicsMarkerRepairTool`, `MarkerAuditTool`, 8 pre-pivot diag/agreement test files, the Phase-A `WireA3DiagSinks` harness in `PhysicsLabController`, and the stale `TERRAIN_REALTEST_FIX` Active spec. **Mid-step fix:** `Physics.Runtime.SurfaceMarker` was defined inline in the deleted `SceneSurfaceProvider.cs` — extracted to its own file (`Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`) to satisfy hard rule 5 + restore importer compilation. Lesson filed (`tasks/lessons.md`: grep ALL types in a file before deleting). Test gate: **198/198 EditMode PASS, 0 failed, 0 skipped, 43.5s**. Per-step commits `phase-f.{1,1b,2,3,3.5,4,4-fix,4b,5,6}` on `main` (commits `32c73935..03744859` + lessons `8b2c82fc`).

Full history in `Docs/Archive/TELLCODE_HISTORY.md`.

---

## 📌 ACTIVE — Phase 8 Shot UI Polish

**Spec:** `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md` — read end-to-end before starting any new part.

**One-line summary:** Wire real Figma art into the shot UI. 8 parts (8.1–8.8), each with its own done report. Per-part 2-attempt budget. Branch: `phase-8-shot-ui`. Pre-merge tag: `pre-phase-8`. Total estimated ~10–11h of Code time.

**Architecture decisions are LOCKED in the spec.** Each visible element has a bucket (procedural / sprite / TMP) assigned by the Architect. If an assignment looks wrong during impl, surface to Architect; do NOT swap silently.

**Hard rules:** No `BallSimulation.cs` edits. No `Physics/Core/` edits. No third-party tween libs. Reuse `Golfin.Gameplay.UI` asmdef. Per-part commits with `phase-8.{N}: {summary}`.

**Order:** 8.1 cone restyle → 8.2 power gauge → 8.2.5 club handle sprite → 8.3 player+hole card+settings → 8.4 wind+hole indicators → 8.5 action button row → 8.6 ball+club selectors → 8.7 centerpiece ball+trail → 8.8 polish/tests/smoke.

**Stop after each part. Wait for Architect ack before next.**

---

## ✅ DONE — Phase 8.1: Cone restyle (2026-04-27)

**Files created/modified:**

- `ConeBandPalette.cs` (new) — shared constants: band Y positions, `BandHalfHeightPx=2f`, fill color, band-line colors (dark), slab colors (pastel/translucent: salmon, cream, mint)
- `ConeMeshGraphic.cs` (rewritten) — filled grey triangle + 3 horizontal band-line quads; reads `ConeBandPalette` for all palette values; no serialized `_bandHalfHeightPx` (uses palette constant directly)
- `TimingSlabGraphic.cs` (new) — trapezoidal slab travelling up the cone; width narrows toward apex; `_slabHalfHeightPx=30f` (60px total ≈ 10% of 600px cone); `SetConeParams()` + `CurrentY01` property
- `ShotConeView.cs` (rewritten) — `SetupSlab()` replaces `SetupArrows()`; `UpdateSlab()` drives slab position/color; `SlabColorFromProgress()` lerps `SlabColorRed→SlabColorGold→SlabColorGreen` using palette breakpoints

**Verified visually (autonomous screenshot):**

- Grey semi-transparent cone fill ✅
- 4px band lines at 0%, 45%, 85% (dark maroon/amber/olive) ✅
- Salmon trapezoidal slab at Y01=0.3 correct shape + color ✅
- Compared against `Docs/Reference/In-game UI/Timing Arrows.png` ✅

---

## ✅ DONE — Phase 8.2: Power gauge widget (2026-04-27)

**Files created/modified:**

- `PowerGaugeGraphic.cs` (new) — `MaskableGraphic` subclass; procedural arc ring (outer=100px, inner=80px, ring=20px on 200×200 widget); vertex-colored triangle fan; gradient baked into vertex colors: green(0°)→yellow(180°)→red(360°)→maroon(overpower); `Progress01` property drives `SetVerticesDirty()`
- `PowerGaugeWidget.cs` (new) — coordinator MonoBehaviour; subscribes `ShotController.OnStateChanged`; `CanvasGroup.alpha` for show/hide (preserves event subscription); drives `Progress01`, `{pct}%` text (Rubik Medium 50), `{yards} yd` text (Rubik Medium 23)
- `ShotConeTest.unity` (modified) — `PowerGaugeWidget` GO added under `ShotCanvas`; RT 200×200, anchor top-right, pos −180/−460; `Background` child with `Indicator - Power.png` sprite; `GaugeArc` child with `PowerGaugeGraphic`; `PctText` + `YardsText` TMP children; `ShotController` reference wired
- `Assets/Art/In-Game UI/` (11 assets) — fixed `TextureType` from `Default` → `Sprite` for all in-game UI PNGs (needed by Parts 8.3–8.7 too)

**Visually confirmed by Cesar (ShotConeTest) + autonomous screenshot (LabScaffold):** gauge renders at 50% — green→yellow arc, navy background circle, "50%" + yards text correct. Screenshot: `Assets/Screenshots/_compressed/screenshot_gauge_lab_50pct.png`.

**Font note:** PctText + YardsText should use `Rubik-VariableFont_wght SDF`, not `Rubik-SemiBold SDF`. Cesar corrected this manually; future build scripts must use `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`.

**Post-ack bug fixes (2026-04-27):**
- `ClubHandleDragger._coneHeightPx` was stale at 600px (old value) while `ShotConeView._coneHeightPx` = 1009px. Fixed: `ShotConeView.Awake()` now calls `_clubHandle.GetComponent<ClubHandleDragger>()?.SetConeHeight(_coneHeightPx)` to keep both in sync.
- `ConeMesh` base Y was 120px → cone tip sat 70px above screen center. Fixed: base Y moved to 50px so tip aligns with canvas center (2118/2 = 1059). ClubHandle moved automatically as a child.

---

## ✅ DONE — Phase 8.2.5: Club Handle sprite swap + scale-with-pull (2026-04-27)

**Files created/modified:**
- `ClubSelectionBroadcast.cs` (new) — static event bus in `Golfin.Gameplay.UI.ShotUI`; avoids circular asmdef dep (Viewer already refs Gameplay.UI, so Viewer calls Raise() and UI subscribes)
- `ClubHandleSpriteBinder.cs` (new) — caches 4 GOLFIN sprites in Awake, subscribes to `ClubSelectionBroadcast`; no direct Viewer reference
- `PhysicsLabController.cs` (modified) — added `CurrentClubIndex` property, `OnClubChanged` event, fires `ClubSelectionBroadcast.Raise(index)` in `SetClub`
- `ShotConeView.cs` (modified) — `UpdateClubHandle` now applies `localScale = Vector3.one * Lerp(_minHandleScale, _maxHandleScale, PowerNormalized)` (inspector-tunable, defaults 1.0→1.3); `sizeDelta=(178,100)` applied in Awake from inspector fields
- `ClubHandleDragger.cs` (modified) — removed hardcoded `_coneHeightPx = 600f`; reads live from `[SerializeField] ConeMeshGraphic _coneGraphic` via computed property `ConeHeightPx`
- `LabScaffold.unity` (modified) — `ClubHandleSpriteBinder` added to `ClubHandle` GO; `ClubHandleDragger._coneGraphic` wired to `ConeMesh` GO

**Play-mode verification (2026-04-27):**
- 0% power: `anchoredPos=(0, 1009)`, `scale=(1.0, 1.0, 1.0)` ✅
- 100% power (Timing state): `anchoredPos=(0, 0)`, `scale=(1.3, 1.3, 1.3)` ✅
- Timing slab visible at 100% power, absent at 0% ✅
- No compile errors ✅

**Cesar confirmed (2026-04-27):** Handle moves to full-pull position and correct scale when unpaused. Code verified working.

**Remaining smoke test for Cesar:** cycle lab club picker (Driver→Iron→Wedge→Putter) → verify handle sprite swaps. Scale and position ranges are tunable via `ShotConeView` inspector fields `_minHandleScale` / `_maxHandleScale` / `_handleWidth` / `_handleHeight`.

**Awaiting Architect ack before Part 8.3.**

---

## ✅ DONE — Housekeeping: BallSimulation A3 plumbing cleanup (2026-04-27)

Deleted `DiagPerStepSink`, `DiagPerStepEnabled`, `DiagStepFrame` fields + their two consumer blocks in `RunRollPhase` and `RunPuttPhase` (-21 lines). `DiagErrorLogger` and both `CheckTerrainInvariant` calls retained. 198/198 PASS. Commit: `238a8f67`.

---

## ✅ DONE — Texture Experiment Phase 1 (2026-04-27, superseded by Phase 2)

- Step 1: 25 textures generated, 12 MB total, 0 failed sources. Output: `Assets/Courses/Textures_Experimental/`
- Step 2: 9 TerrainLayers duplicated, 4 overlay materials duplicated, 0 warnings. Scene: `Hole_01_Experimental_Geo.unity`. Report: `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md`.
- Visual review by Cesar (2026-04-28) revealed 7 defects: rough brown, semi-rough too vivid, greens/bunkers/tee-borders unchanged, tees identical to fairway, everything flat. Root causes: wrong sources for rough/semi-rough/tee + clone script missed shared `MAT_*` materials + JPG normals imported as Default-type sRGB instead of NormalMap-type linear.
- Phase 2 fixes all 7. Phase 1 outputs will be deleted in Phase 2 Step 0.

---

## ➡️ ACTIVE — Texture Experiment Phase 2 (revision)

**Spec:** `Docs/Specs/Active/TEXTURE_EXPERIMENT.md`
**Branch:** any (this is non-load-bearing — no production files touched)
**Replaces:** Phase 1 outputs (which had visible defects).

**One-line summary:** Two parallel tracks. Track A swaps source images for Rough (→ ambientCG Grass005), Semi-rough (→ Grass002 darkened), and Tee (→ Grass001). Track B fixes the clone script to (i) walk ALL MeshRenderers, (ii) catch shared MAT_* materials in addition to per-hole MAT_T_* ones, (iii) duplicate them to `Materials (Shared by courses)/Experimental/`, (iv) set `textureType: NormalMap` + `sRGBTexture: false` on every `_Normal.jpg`. Then tear down Phase 1 outputs and re-run end-to-end.

**Steps for Code:**

**Step 0 — clean up Phase 1:**
- Delete `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/` (folder + meta)
- Delete `Assets/Golf/Courses/lomond-country-club/Data/hole-01-experimental/` (folder + meta)
- Delete `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md`

**Track A — Texture sources:**
1. Edit `Tools/TextureExperiment/manifest.json` per spec section A.1 (rough → Grass005, semi-rough → Grass002 −10%, tee variants → Grass001)
2. `cd Tools/TextureExperiment && node prepare-textures.mjs`
3. Verify all 25 textures present, T_Rough_Albedo is visibly green wild grass (not brown/rocky)

**Track B — Clone script:**
4. Update `Assets/Scripts/Editor/CourseImporter/BuildExperimentalHole01.cs` per spec sections B.1–B.6:
   - **B.1:** After Track A, set `textureType=NormalMap`, `sRGBTexture=false` on all `Textures_Experimental/*_Normal.jpg`. (CRITICAL — fixes the flat look.)
   - **B.2:** Walk EVERY MeshRenderer in the duplicated scene; iterate ALL `sharedMaterials`; catch BOTH `MAT_T_*` (per-hole) AND `MAT_(Bunkers|Green|Fringe|Tee|Fairway|Rough|Semirough|Road|OOB)(_Dark)?` (shared)
   - **B.3:** Use a filename → experimental-texture lookup dict; repoint `_BaseMap` + `_MainTex` + `_BumpMap`; preserve `m_Scale`, `_BaseColor`, all floats, all colors
   - **B.4:** TerrainLayer duplicates must preserve `m_NormalScale: 0.4`, `m_SmoothnessSource: 1`, `m_MaskMapTexture` GUID, `m_TileSize`
   - B.5 covered by Step 0 above
   - **B.6:** HOLE01_CLONE_REPORT.md must list Bunkers, Green, Fringe, Tee duplications by name (acceptance gate)
5. Run `GOLFIN > Tools > Build Hole_01 Experimental Clone`
6. Verify production scene + production materials (excluding new `Experimental/` subfolders) are unmodified — `git status` shows only additions

**Hard rules:**
- No edits to production scene, production TerrainLayers, or production materials in `Materials (Shared by courses)/` outside the new `Experimental/` subfolder
- No edits to `HoleGeoImporter.cs` or any other importer code
- No splatmap or mask map regeneration
- Preserve `_BaseColor` tints on duplicated materials (e.g. `MAT_Bunkers` warm cream tint `(1, 0.894, 0.703)` must survive)
- Preserve `m_Scale` on duplicated materials (e.g. `MAT_Tee` scale of (14, 14) must stay (14, 14) on the experimental copy)
- If Grass005 delivers wrong-looking output (brown/yellow/blue), surface to Architect WITH delivered images, do NOT swap to a third source silently
- Iteration budget: 1 attempt for Track A, 2 attempts for Track B

✅ DONE: 2026-04-28
- Track A: 25 textures regenerated. Sources: grass001 (tee/green), grass002 (fairway/semirough), grass003 (fringe), grass005 (rough — wild meadow), ground054 (bunker), asphalt012 (road), polyhaven_sparse_grass (OOB). Brightness: semirough ×0.90, fairway_light ×1.08, fairway_dark ×0.92, bunker_dark ×0.85, tee_dark ×0.90. 0 failed sources.
- Track B: 16328 MeshRenderers walked, 18 unique material names encountered. Duplicated: 3 shared (BunkerSand, GreenSurface, MAT_Fringe), 4 per-hole (MAT_T_Fairway_Mix, MAT_T_Semirough_Albedo, MAT_T_Tee_Albedo, MAT_T_RoadAsphalt_Albedo). 9 TerrainLayers duplicated. Normals: all 11 already correctly set as NormalMap/linear (0 reimported this run). 0 warnings. Report: `Docs/Diagnostics/texture-experiment/HOLE01_CLONE_REPORT.md`. Scene: `Assets/Golf/Courses/lomond-country-club/Generated/Experimental/Hole_01_Experimental_Geo.unity`.
- Key fix applied to BuildExperimentalHole01.cs: open SOURCE scene directly (not copy+open-experimental) to bypass stale Unity artifact cache; save to tracked temp path then File.Copy to gitignored Generated/ directory.

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- **[2026-04-26] Stale comment in** `BallSimulation.cs:26` **(**`// SceneGroundProvider…`**).** SceneGroundProvider was deleted in Phase F. Hard rule 8 forbade touching `BallSimulation` during Phase F so the comment was left as-is. Trivial cleanup; not load-bearing. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] `BallSimulation.DiagPerStepSink` field is now unwired.** `PhysicsLabController.WireA3DiagSinks` was removed in F.3.5. The field still exists in BallSimulation (untouched per hard rule 8) and is dead code; harmless. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] Future housekeeping: consolidate `Physics.Runtime.SurfaceMarker` and `Course.SurfaceMarker` into one enum.** Bake tool currently reads two type systems (one for authoring in scene, one for the bake-side enum), bridged by `SurfaceMarkerMap`. Workable; a single-enum refactor would simplify the importers. Not blocking.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.

Full reasoning: `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## Reference Docs

- `Docs/Archive/TELLCODE_HISTORY.md` — completed task blocks + History Log (start here for anything older than current phase)
- `Docs/README.md` — index map of what lives where in `Docs/`
- `Docs/AI_CONTEXT.md` — project state, pipeline overview, session changelog
- `Docs/Physics/PHYSICS_RESEARCH.md` — physics architecture, 5+1 phase plan
- `Docs/Physics/PHYSICS_TUNING_TARGETS.md` — canonical physics numbers
- `Docs/Physics/LESSONS_PHYSICS_AERO.md` — aero remediation lessons + future tightening options (read before touching aero LUTs)
- `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md` — surface-marker / heightmap rationale
- `Docs/Architecture/INVENTORY_REFERENCE.md` — inventory system patterns
- `Docs/Architecture/UI_HIERARCHY.md` — scene UI paths reference
- `Docs/Architecture/PATTERNS.md` — recurring patterns across the codebase
- `Docs/Pipeline/ADD_HOLE.md` — end-to-end procedure for adding a new hole
- `Docs/Pipeline/LESSONS_FRINGE_BORDER_MESHES.md` — canonical submesh recipe
- `Docs/Game Design/SHOT_CONTROLS_DESIGN.md` — shot control v1 design (authoritative for Phase 7)
- `CLAUDE.md` — Claude Code session rules
- Unity-MCP — https://github.com/IvanMurzak/Unity-MCP
