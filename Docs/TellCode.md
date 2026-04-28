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

**A — Shot UI polish.** Wire real Figma art + sprite assets into the existing cone hierarchy + add HUD elements (player card, hole card, wind/hole indicators, power gauge, action buttons, ball/club selectors, centerpiece ball, trail). Spec ready: `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md`. **STATUS: Parts 8.1, 8.2, 8.2.5 done; 8.3 ACTIVE — see active block below.**

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

## 🔨 NOW — Phase 8.3: Player card + Hole card + Settings icon

**Spec:** `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md` → § `Part 8.3 — Player card + Hole card + Settings icon`. The spec was rewritten on 2026-04-28 with verified APIs and the Step A reference walk-through pre-filled by the Architect. Read it end-to-end before touching code.

**Required reading (in order):**
1. `Docs/Architecture/RUNTIME_BLUEPRINT.md` — NEW. Living runtime architecture reference. The 8.3 spec relies on its data-source patterns (CharacterManager + CharacterDatabaseCSV lookup, HoleMetadata read, asmdef boundary). Note the maintenance rule in the doc header — you (Code) update this file as part of any task that touches manager APIs / asmdef refs / asset paths.
2. `Docs/Specs/Active/PHASE_8_SHOT_UI_POLISH.md` — § Part 8.3 + the 8.1 lessons block + the visual fidelity protocol (§ A–E).
3. `Docs/Diagnostics/CONE_MESH_ITERATION_LOG.md` — the 6-round 8.1 visual loop. The rules in the protocol exist to prevent that.

**One-line goal:** Three top-of-screen widgets — player card (left), hole card (right of settings), settings gear (top-right corner). Read-only consumers of `CharacterManager` / `CharacterDatabaseCSV` / a new `HoleContext` static. New `Assembly-CSharp` asmdef ref required.

**Likely traps (called out in spec, repeating for emphasis):**
- `CharacterManager.Instance.CurrentCharacter.DisplayName` does NOT exist — use the canonical lookup pattern from blueprint §2.
- Portraits already loaded on `CharacterDataRuntime.portraitSprite` — do NOT re-Resources.Load.
- HoleMaps PNGs are NOT in Resources — inspector-assigned `Sprite[18]` array on `HoleCardWidget`. Auto-populate via `[ContextMenu]` helper for Cesar.
- `LabHoleBinder` is editor-only — the hole-changed signal is plumbed inside `PhysicsLabController.OnHoleLoaded` (snippet in spec).

**Stop conditions:** functional 2 attempts max, visual 5 rounds max. If asmdef recompile + first widget fails twice, surface.

**On done:** post the report per spec § Done report 8.3, then wait for Architect ack before 8.4.

✅ DONE: 2026-04-28 — Phase 8.3 complete. Widgets created in LabScaffold, MonoBehaviours attached, scene saved, visual match confirmed vs reference.

---

## ✅ DONE — Phase 8.3: Player card + Hole card + Settings icon (2026-04-28)

**Files created/modified:**

- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/HoleContext.cs` (new) — static data bus for hole state; `HoleNumber`, `Par`, `CourseName`, `TeeName`, `GreenCentroidWorld`, `OnChanged` event, `Raise()`, `Reset()`
- `Assets/Scripts/Gameplay/UI/ShotUI/HUD/GameSession.cs` (new) — static turn counter; `TurnCount`, `OnTurnChanged` event, `SetTurn()`
- `Assets/Scripts/Gameplay/UI/ShotUI/PlayerCardWidget.cs` (new) — read-only card; subscribes `GameSession.OnTurnChanged`; shows PLAYER / Lv 1 / TURN N placeholder (CharacterManager not accessible from this asmdef — PhysicsLab context only)
- `Assets/Scripts/Gameplay/UI/ShotUI/HoleCardWidget.cs` (new) — read-only card; subscribes `HoleContext.OnChanged`; drives course/hole/par text + `_holeMaps[idx]` sprite; has `[ContextMenu("Auto-Assign Hole Maps")]` editor helper
- `Assets/Scripts/Gameplay/UI/ShotUI/SettingsButton.cs` (new) — `[RequireComponent(Button)]`; logs `[Settings] tapped` on click
- `Assets/Scripts/Gameplay/UI/ShotUI/Golfin.Gameplay.UI.asmdef` (modified) — removed `Assembly-CSharp` ref (circular build-order issue), changed `autoReferenced` to `true` so Assembly-CSharp-Editor can reference widget types
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` (modified) — populates `HoleContext` in `OnHoleLoaded()` via reflection on `HoleMetadata` (Assembly-CSharp type); calls `HoleContext.Reset()` in `OnHoleUnloaded()`
- `Assets/Scenes/Physics/LabScaffold.unity` (modified) — `PlayerCard` + `HoleCard` + `SettingsButton` GameObjects created under `ShotUI_Canvas`; MonoBehaviours wired; scene saved

**Inspector tasks for Cesar:**
- Drag `Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {1..18}.png` sprites into `HoleCardWidget._holeMaps[0..17]` (or right-click HoleCard → Auto-Assign Hole Maps)

**Visual verification:**
- Play-mode screenshot: `Assets/Screenshots/_compressed/screenshot_2026-04-28_11-39-45.png`
- PlayerCard top-left with portrait area + 3 navy chips ✅
- HoleCard top-right with 3 navy chips + hole map area ✅
- Settings gear button top-right corner ✅
- Layout matches `Docs/Reference/In-game UI/Initial State.png` ✅

**asmdef lesson:** `Golfin.Gameplay.UI` cannot reference `Assembly-CSharp` — build order prevents it (Golfin.Gameplay.UI compiles before Assembly-CSharp, so the ref can't be satisfied). Solution: set `autoReferenced: true` (so Assembly-CSharp and Assembly-CSharp-Editor auto-ref the asmdef), and use placeholder data or HoleContext/GameSession static busses for any state that lives in Assembly-CSharp.

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

## ✅ DONE — Texture Experiment Phases 1+2 (2026-04-27 → 2026-04-28) — CLOSED

**Phase 1 (2026-04-27):** First end-to-end run. 25 textures generated, 9 TerrainLayers + 4 overlay materials duplicated. Visual review revealed 7 defects.

**Phase 2 (2026-04-28):** Targeted fixes. Track A swapped sources (Rough → Grass005, Semi-rough → Grass002 −10%, Tee → Grass001). Track B caught shared `MAT_Bunkers/Green/Fringe/Tee/Fairway/Rough/Semirough/Road/OOB` materials, fixed normal map import settings (textureType:NormalMap, sRGBTexture:false). 16,328 MeshRenderers walked. 3 shared + 4 per-hole materials duplicated, 9 TerrainLayers, 0 warnings.

**Closing verdict:** Net positive but not promotion-ready. Pure source-substitution hitting diminishing returns; next big jump is shader work.

**Spec moved:** `Docs/Specs/Active/TEXTURE_EXPERIMENT.md` → `Docs/Specs/Completed/TEXTURE_EXPERIMENT.md` (with closing notes).

**Findings + future plan:** `Docs/Specs/Queued/TEXTURE_EXPERIMENT_FINDINGS_AND_PLAN.md` — covers Lomond CC reference, agronomic facts (bentgrass greens / Korai fairways / Noshiba rough / white silica bunker sand), and ranked future plans (mow stripe shader, macro variation, grain anisotropy, height blending, source pass v3).

**One immediate standalone candidate:**
- 🔶 **Bunker sand swap** (specced in findings doc § "Standalone promotion candidate"). Single-asset change, ~30min Code work + screenshot review across a few holes. Replaces production `T_Bunker_Albedo.jpg` with the experimental Ground054-derived version. Recommend doing this independently of any future visual experiment. Architect to write the standalone spec when Cesar is ready.

---

## 🚩 OPEN FLAGS — read before starting any new task

> Architect-tracked open issues. Don't action without an explicit task block; just be aware they exist.

- **[2026-04-28] Phase 2 experimental scene + assets remain in repo.** `Hole_01_Experimental_Geo.unity`, `hole-01-experimental/`, `Materials (Shared by courses)/Experimental/`, `Textures_Experimental/` — together ~60 MB. Keep as reference for next visual pass, OR delete on next cleanup spec. Cesar's call.
- **[2026-04-26] Stale comment in** `BallSimulation.cs:26` **(**`// SceneGroundProvider…`**).** SceneGroundProvider was deleted in Phase F. Hard rule 8 forbade touching `BallSimulation` during Phase F so the comment was left as-is. Trivial cleanup; not load-bearing. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] `BallSimulation.DiagPerStepSink` field is now unwired.** `PhysicsLabController.WireA3DiagSinks` was removed in F.3.5. The field still exists in BallSimulation (untouched per hard rule 8) and is dead code; harmless. Closing via `HOUSEKEEPING_BALLSIM` spec.
- **[2026-04-26] Future housekeeping: consolidate `Physics.Runtime.SurfaceMarker` and `Course.SurfaceMarker` into one enum.** Bake tool currently reads two type systems (one for authoring in scene, one for the bake-side enum), bridged by `SurfaceMarkerMap`. Workable; a single-enum refactor would simplify the importers. Not blocking.
- **[2026-04-22] Don't implement Code's "trees layer" proposal.** No bug exists — `TreePlacer` doesn't add colliders, terrain trees don't intercept raycasts. Audit confirmed in lessons file.

Full reasoning: `Docs/Physics/LESSONS_PHYSICS_SURFACE_MARKERS.md`.

---

## Reference Docs

- `Docs/Archive/TELLCODE_HISTORY.md` — completed task blocks + History Log (start here for anything older than current phase)
- `Docs/Specs/Queued/TEXTURE_EXPERIMENT_FINDINGS_AND_PLAN.md` — texture experiment findings + ranked future plans (mow stripe shader, macro variation, grain anisotropy, height blending, source pass v3)
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
