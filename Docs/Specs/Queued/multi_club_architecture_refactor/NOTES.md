# multi_club_architecture_refactor — NOTES

> **Status:** QUEUED. Architect-side design captured 2026-05-17 (Cesar's pick = Path A, after Loop v1 closes).
> **Architect SPEC.md will be written when this task is next-up (after §2f).** This NOTES file is the design lock; SPEC just operationalises it.
> **Notion:** TBD (Order 290 — between Loop v1 200s and Loop v2 300s). Slot reserved on Cesar greenlight.
> **Tier:** 3 (multi-file, asmdef-adjacent, scene-adjacent — full pipeline).

---

## Why

Today's repo is single-course (Lomond). Adding Taiheiyo Club Gotenba — or any second course — collides at the sim-data path because `Assets/Resources/HoleData/Hole_XX/` is flat across courses: Taiheiyo's Hole 1 would overwrite Lomond's Hole 1.

Path A from the planning convo: properly namespace the sim-data path **before** authoring any new content. Same architectural decision as Phase F's surface-marker consolidation — pay the refactor cost once instead of carrying a dual-path workaround forever.

Cesar's locked picks (2026-05-17):
1. **Path A** — proper refactor, no carve-out.
2. **After Loop v1 §2f closes** — does not interleave with Loop v1 work.
3. **6-tee schema with 2 empty slots for Lomond** — `HoleData.tees` grows to a `Dictionary<TeeColor, TeeData>` or equivalent, Lomond keeps its 4, Taiheiyo carries 6 (Tournament / Back / Regular / Middle / Front / Ladies).

---

## Scope

### File path layout (target)

```
Assets/Resources/HoleData/
  ├─ lomond-country-club/
  │   ├─ Hole_01/ heightmap.bytes + zones.json
  │   ├─ Hole_02/ ...
  │   └─ Hole_18/ ...
  └─ taiheiyo-club-gotenba/        ← drops in via Phase 2
      └─ Hole_01/ ...
```

Today: `Assets/Resources/HoleData/Hole_01/...` (flat, Lomond-only).

### Code surface area (verified 2026-05-17)

**Single runtime load site** — `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:1185-1186`:
```csharp
var zonesAsset = Resources.Load<TextAsset>($"HoleData/{holeId}/zones");
var hmAsset    = Resources.Load<TextAsset>($"HoleData/{holeId}/heightmap");
```
becomes `$"HoleData/{course}/{holeId}/zones"` etc. `course` comes from `ActiveCourseContext.CurrentCourseSlug` (new static bus).

**Two bake sites** (write):
- `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` — `ResourcesRoot = "Assets/Resources/HoleData"` (line ~67); writes to `Path.Combine(ResourcesRoot, holeId)`. Refactor: derive course slug from the loaded scene's path (`Assets/Golf/Courses/<course>/Generated/Hole_XX_Geo.unity` -> `<course>`), write to `Path.Combine(ResourcesRoot, course, holeId)`.
- `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` — same pattern, same fix.

**One test site:** `Assets/Scripts/Gameplay/Tests/BakedPivotRegressionTests.cs:42-45` references `HoleData/Hole_` strings; update to use the new path under `lomond-country-club/`.

**UI / database** — `HoleData.cs` already has `courseNameKey` (good). `HoleSelectionScreenController.cs:202` literally comments `// Lomond filter: all 18 holes` — multi-course awareness already anticipated. `HoleDatabase` becomes course-keyed; loader filters by `ActiveCourseContext.CurrentCourseSlug`.

**6-tee schema** — `HoleData.cs` currently has only `holeNumber` + `par`; tee distances aren't stored on it yet (they live in `Tools/UHoleGeo/output/<course>/course.json`). Question for SPEC: do we need tee distances on the runtime `HoleData` for the picker UI today? If yes, add `tees: Dictionary<TeeColor, int>` with TeeColor enum (Tournament, Back, Regular, Middle, Front, Ladies). If no, defer schema bump to Loop v2 §3b when picker actually needs them.

### New code

- **`Assets/Scripts/Gameplay/Loop/ActiveCourseContext.cs`** — static-bus per project pattern. Fields: `string CurrentCourseSlug` + `string CurrentCourseDisplayName`. Methods: `Set(string slug, string displayName)`, `Reset()`, `event OnCourseChanged`. Default slug = `"lomond-country-club"` (cold-boot fallback so no caller has to special-case).
- **`Assets/Scripts/Editor/CourseImporter/CourseSlugResolver.cs`** — helper that extracts course slug from a hole-scene path. Single point of truth so BakeZoneJsonTool + PhysicsHeightmapBaker + HoleGeoImporter all agree. Path regex: `Assets/Golf/Courses/(?<slug>[^/]+)/Generated/Hole_\\d{2}_Geo\\.unity`.

### Migration step

One-time mechanical move of existing Lomond data:
```
Assets/Resources/HoleData/Hole_01/  ->  Assets/Resources/HoleData/lomond-country-club/Hole_01/
... x 18
```
Use `AssetDatabase.MoveAsset` to preserve `.meta` GUIDs (zones.json and heightmap.bytes don't have script references but their GUIDs may be referenced by editor tools — preserve to be safe).

The bake tools then write fresh data to the new path on next bake. Migration script lives in `Assets/Scripts/Editor/CourseImporter/MigrateHoleDataToCourseNamespaced.cs`, gated behind menu item `GOLFIN > Tools > Migrate HoleData to course-namespaced paths` so it can only fire deliberately.

### Importer menus

`HoleGeoImporter.Geo01..Geo18` + `Geo07Flat..Geo18Flat` menus are all 36 lines of one-liners hardcoded to `"lomond-country-club"`. Options:
- **(a)** Add a `Geo01..Geo18` block per course (36 lines x N courses — boring but mechanical, easy to grep for course-specific issues).
- **(b)** Replace all menus with one `GOLFIN > Course Importer` EditorWindow that has a course dropdown + hole list + import buttons (one-time UI work, scales better, slight friction to the muscle-memory keystroke flow).

SPEC will pick (a) initially — keeps Cesar's import muscle-memory; (b) can land later if there's appetite.

---

## Test gate

- All existing EditMode tests still pass (current baseline: 248/248 or higher per controls_h's landing).
- `BakedPivotRegressionTests` 24/24 still PASS on Lomond Hole 1 after migration.
- New `ActiveCourseContextTests.cs`: ~4 tests for Set / Reset / event fires / default slug.
- New `CourseSlugResolverTests.cs`: ~3 tests (lomond path resolves, taiheiyo path resolves, malformed path returns null).
- Manual smoke: load Lomond Hole 1, fire driver from tee, ball settles correctly. Load Lomond Hole 7 (ravine repro) — still classifies right.

---

## Hard rules

1. **Do not break the bit-exact test gate.** Migration must be a path change only; sim outputs unchanged.
2. **Do not touch `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, any sim physics, the BallStateMachine asmdef, or LoopCameraDirector.** This is pure data-path plumbing.
3. **Migration menu item is a one-shot.** After the 18 Lomond folders are moved, it gets `[MenuItem(..., validate = true)]` returning false if the migration has already happened (detect via existence of `HoleData/lomond-country-club/Hole_01/heightmap.bytes`). Re-running is harmless but the menu greys out.
4. **No raw-YAML scene edits.** The Hole_XX_Geo scenes don't need to change for this — but if something does, Unity Editor MCP only.
5. **`Resources.Load` path strings are the canonical interface.** No file-path-based load; everything goes through `Resources.Load<TextAsset>($"HoleData/{course}/{holeId}/...")`.

---

## Out of scope (defer)

- **HoleSelectionScreen course-tab UI.** Single-course UI works (filter is already there — just hardcoded to Lomond). When Taiheiyo content actually ships, add a course tab/dropdown then. Spec'd separately as part of Loop v2 §3b.
- **Per-course course-info card** (in-game splash with course story, designer credit, photos). Pure content — drops in alongside Phase 2.
- **CharacterContext / club-roster course-awareness.** Clubs are course-agnostic today; no refactor needed unless a future club is "Taiheiyo edition" cosmetic.
- **Save state per-course.** Loop v2 §3e save-state work owns the course-keyed progression schema; that spec subsumes it.

---

## Followup hook for Phase 2

Phase 2 (Taiheiyo content) becomes a pure "follow `Docs/Pipeline/ADD_HOLE.md` 18 times under the new course slug" exercise. No code, only content + UHoleGeo configs + Unity import.

---

## Estimate

1 day implementer-time (Tier 3 pipeline run). Mechanical; risk is concentrated in the migration step (GUID preservation) and the importer-menu refactor (option a vs b).
