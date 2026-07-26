# multi_club_architecture_refactor — SPEC

> **Status:** ACTIVE. **Tier 3** (multi-file, editor-tool, migration — full pipeline).
> **Notion:** Order 290 (slot reserved in NOTES; confirm/create on kickoff).
> **Design lock:** `NOTES.md` (2026-05-17) in this folder. **This SPEC supersedes NOTES wherever they conflict — see §0.**
> **Surface re-verified against HEAD `5d9942f83` on 2026-07-24.**
> **Naming wart:** the slug says "club" but this is a **multi-COURSE** refactor. Slug kept for continuity with Notion + KICKOFF; do not rename mid-flight.

---

## 0. Corrections to NOTES.md — READ FIRST

NOTES verified the code surface on **2026-05-17**. `tree_collisions` (Order 348) and the green-authoring track landed *after* that and both write into this exact path. NOTES is now materially under-scoped.

| NOTES said | Verified at HEAD 2026-07-24 |
|---|---|
| 2 artifacts per hole (`zones.json`, `heightmap.bytes`) | **4** — adds `green.json`, `tree_obstacles.csv` |
| 1 runtime load site | **3** files |
| 2 bake/write sites | **5** |
| 1 test site | **3 test files** |
| ~4 files total | **34 references across 14 files** |
| `PhysicsLabController.cs:1185-1186` | drifted to **`:1453/:1454`**, plus a third load at **`:1484`** |
| `tees` as `Dictionary<TeeColor, TeeData>` | **ILLEGAL** — see §3 |
| Importer menus: pick option (a) | **Cesar overrode to (b)** 2026-07-24 |
| Only `Resources/HoleData/` collides | **`Resources/HoleImages/` collides too** — see §1.7. Fails silently via a `Missing` sprite fallback. |

**Do not work from NOTES' call-site list. Use §1.2 below.**

---

## 1. PHASE 1 — course-namespaced sim-data path

**This is the only phase on the critical path for a second course.** Everything else can slip without blocking Taiheiyo content.

### 1.1 Target layout

```
Assets/Resources/HoleData/
  ├─ lomond-country-club/
  │   ├─ Hole_01/  green.json + heightmap.bytes + tree_obstacles.csv + zones.json
  │   └─ ... Hole_18/
  ├─ taiheiyo-club-gotenba/     ← drops in later, content-only
  └─ _test/                     ← non-course fixtures (see 1.5)
      ├─ TestGreen/zones.json
      └─ Hole_99/               (created + destroyed by tests at runtime)
```

**Invariant to preserve and document:** every entry at `HoleData/` root is a course slug, with exactly one reserved exception — `_test`.

### 1.2 Verified call sites — ALL must be updated

**Runtime loads (3):**
| File | Line | Artifact |
|---|---|---|
| `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` | 1453 | zones |
| `" "` | 1454 | heightmap |
| `" "` | 1484 | tree_obstacles |
| `Assets/Scripts/Course/Runtime/GreenTopology.cs` | 148 | green |

Also update the log strings at `PhysicsLabController.cs:1457, :1462` and the comment at `:1449` so warnings print the real path.

**Bake / write sites (5):**
| File | Line | Note |
|---|---|---|
| `Assets/Scripts/Editor/CourseImporter/TreeObstacleBaker.cs` | 356 | literal |
| `Assets/Scripts/Editor/GreenAuthoring/GreenJsonWriter.cs` | 106 | literal |
| `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` | 2498 | literal |
| `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs` | 163 | **variable** — `Path.Combine(exportPath, "heightmap.bytes")` |
| `Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs` | ~67 | **variable** — `ResourcesRoot` const |

**Editor / authoring reads (3 files):**
`GreenTopologyEditor.cs:216, :256` · `GreenAuthoringVisualGate.cs:100, :462` · `Debug/GreenVariantDiagnostic.cs:91`

**Tests (3 files):**
`BakedPivotRegressionTests.cs:43, :44` · `RealHoleTerrainTests.cs:48, :49` · `GreenTopologyTests.cs:47, :61, :272, :277`

**Log string only (no behaviour):** `Physics/Viewer/Bot/Scenarios.cs:3381`

**Doc comments to correct:** `GreenTopology.cs:15, :140` · `TreeObstacleBaker.cs:13` · `GreenJsonWriter.cs:16, :31` · `GreenTopologyEditor.cs:24` · `BakeZoneJsonTool.cs:16` · `TestGreenLabSetup.cs:18`

### 1.3 TRAP — a literal grep under-counts the surface

`PhysicsHeightmapBaker.cs` and `BakeZoneJsonTool.cs` compose their output path from **variables**, so they never appear in a `grep 'HoleData/'`. **Trace the variables (`exportPath`, `ResourcesRoot`), do not string-match only.** Before declaring Phase 1 complete, re-run the grep AND manually confirm both variable-composed writers land under the course slug.

### 1.4 New code

**`Assets/Scripts/Gameplay/Loop/ActiveCourseContext.cs`** — static bus.
- `string CurrentCourseSlug` (default `"lomond-country-club"` — cold-boot fallback so no caller special-cases)
- `string CurrentCourseDisplayName`
- `Set(string slug, string displayName)`, `Reset()`, `event OnCourseChanged`

**`Assets/Scripts/Editor/CourseImporter/CourseSlugResolver.cs`** — single source of truth for slug-from-scene-path, shared by both bakers + the importer.
- Regex: `Assets/Golf/Courses/(?<slug>[^/]+)/Generated/Hole_\\d{2}_Geo\\.unity`
- Returns `null` on malformed input — callers must fail loudly, never silently fall back to Lomond.

### 1.5 Fixture relocation (Cesar delegated this call 2026-07-24 → `_test/`)

- `HoleData/TestGreen/` → `HoleData/_test/TestGreen/`. Update `TestGreenLabSetup.cs:55`.
- `Hole_99` synthetic fixture in `GreenTopologyTests.cs` (:47, :61, :272, :277) → writes under `HoleData/_test/`. **The test must pass the fixture root explicitly** — if it inherits the resolver default it will write into `lomond-country-club/Hole_99` and pollute the course.
- **Rejected:** under-lomond (phantom Hole 99 breaks any future "enumerate holes for course X"), and root (destroys the one-entry-per-course invariant).
- **NOTE for implementer:** everything under `Assets/Resources/` ships in the player build regardless of references, so `_test/` fixtures ship too. That is already true today — **no regression, do not fix here.** File a follow-up if it matters.

### 1.6 Migration

One-shot mechanical move of 18 Lomond folders (all 4 artifacts each):
- Script: `Assets/Scripts/Editor/CourseImporter/MigrateHoleDataToCourseNamespaced.cs`
- Menu: `GOLFIN > Tools > Migrate HoleData to course-namespaced paths`
- **Use `AssetDatabase.MoveAsset`** to preserve `.meta` GUIDs.
- `[MenuItem(..., validate = true)]` returns false once `HoleData/lomond-country-club/Hole_01/heightmap.bytes` exists, so the menu greys out after it has run.

### 1.7 HoleImages — second collision path (added 2026-07-24 after Cesar asked "does this cover every per-hole detail?")

**`Assets/Resources/HoleImages/Hole_01.png … Hole_18.png` is flat and un-namespaced.** Taiheiyo's `Hole_01.png` collides with Lomond's exactly like the sim data. NOT in the original NOTES scope.

Load sites (2, both `Resources.Load<Sprite>($"HoleImages/{hole.holeImageName}")`):
| File | Line | Fallback |
|---|---|---|
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | 376 | `HoleImages/Missing` at :379 |
| `Assets/Scripts/UI/HoleSelection/HoleCardController.cs` | 157 | `HoleImages/Missing` at :160 |

**TRAP — this one fails SILENTLY.** Both sites fall back to a `Missing` sprite on a null load, so a collision does not throw. Taiheiyo would ship rendering placeholder art on its hole cards and result modals with no error in the log. Any verification MUST be visual, not log-based.

**Fix is data-only — no code change.** `holeImageName` is CSV column 4 and `Resources.Load` accepts a relative path:
1. Move PNGs → `Assets/Resources/HoleImages/lomond-country-club/Hole_NN.png` (use `AssetDatabase.MoveAsset` — GUID preservation, same as §1.6).
2. Rewrite `HoleDatabase.csv` col 4 from `Hole_01` → `lomond-country-club/Hole_01` (18 rows).
3. Leave `HoleImages/Missing` at the root — it is course-agnostic and both fallbacks reference it directly.
4. Update the comment on `HoleData.cs:44`, which documents the old flat convention.

**Gate:** open Hole Selection and complete a hole — cards and result modal must show real hole art, not the `Missing` placeholder.

### 1.8 Verified as NOT needing work

Audited 2026-07-24; do not spend time re-checking these:
- **Baked geometry** (materials, mask textures) — already at `Golf/Courses/<slug>/Data/hole-NN[-geo][-flat][-experimental]/`. Safe.
- **Hole scenes** — already at `Golf/Courses/<slug>/Generated/Hole_NN_Geo.unity`. Safe.
- **Baked lightmaps / GI** — none exist (no `.exr` or `*Lightmap*` anywhere under `Assets/Golf`).
- **Per-hole prefabs** — none live. `Scenes/Original~/` is Unity-ignored (`~` suffix); `Prefabs/Original/OldHole/` is the known dead-asset pile.
- **Localization keys** — `HOLE_LOMOND_N` / `HOLE_LOMOND_N_DESC` already encode the course.
- **Non-shipping, out of scope:** `Assets/Screenshots/Hole_NN_*` (dev capture output), `Scenes/Debug/Hole_07_Geo_Diagnostic.unity`.

---

## 2. PHASE 2 — Course Importer EditorWindow

**Cesar's call 2026-07-24: option (b), overriding NOTES' pre-pick of (a).**

Replace the 36 hardcoded one-liner menu items in `HoleGeoImporter.cs` (`Geo01..Geo18` + `Geo07Flat..Geo18Flat`) with a single window.

- Menu: `GOLFIN > Course Importer`
- **Course dropdown** populated by enumerating `Assets/Golf/Courses/*` directories (today: `lomond-country-club` only).
- Hole list 1–18 with per-hole Import button, plus the Flat variant toggle where one exists.
- Sets `ActiveCourseContext` on selection so bakes and imports agree on the slug.

**Muscle-memory mitigation (required, not optional).** Cesar loses a keystroke flow he uses constantly. The window **must** persist last-selected course + last-selected hole via `EditorPrefs` and restore them on open, so the common path is open → click Import. Keep one `[MenuItem]` shortcut that re-runs the last import directly.

**Do not delete the old menu items until the window is verified working** on at least 2 holes incl. a Flat variant.

---

## 3. PHASE 3 — 6-tee schema

**Cesar's call 2026-07-24: add tees now (not deferred).**

### 3.1 NOTES' model is wrong — corrected model

Ground truth from `Tools/UHoleGeo/output/<course>/course.json` (both files verified to exist):
- **Lomond:** 4 tees — `back`/blue, `regular`/green, `front`/white, `ladies`/red
- **Taiheiyo:** 6 tees — `tournament, back, regular, middle, front, ladies`; `tee_set_count: 6`; **every `color` is `null`**, with an explicit `_notes_tee_colors` field saying Cesar fills them from an on-site visit or brochure

Two corrections:
1. **`Dictionary` will not serialize.** `HoleData` (`Assets/Scripts/UI/HoleData.cs`) is a plain `[Serializable]` class. Use `List<TeeData>` — mirrors the existing `List<HoleReward>` pattern in the same file — plus a `TryGetTee(TeeSet, out TeeData)` helper.
2. **Tier is not colour.** `tournament/back/regular/...` are tee *tiers*; colour is a separate, frequently-unknown field. Naming the enum `TeeColor` would bake Taiheiyo's nulls into a type that claims to be colour.

```csharp
public enum TeeSet { Tournament, Back, Regular, Middle, Front, Ladies }

[Serializable]
public class TeeData
{
    public TeeSet set;
    public int    yards;
    public string color;   // may be empty/null — Taiheiyo colours are unknown
}
```

Add to `HoleData`: `public List<TeeData> tees = new();`

Lomond's "2 empty slots" from NOTES falls out naturally — **absent from the list means the course does not offer that tee.** Do not pad with empty entries.

### 3.2 CSV changes

`Assets/Data/HoleDatabase.csv` is **19 positional columns, 18 data rows, no `courseId`**, and is an inspector-assigned `TextAsset` on `HoleDatabaseLoader` (not `Resources.Load`).

**New file `Assets/Data/HoleTees.csv`** — normalized, not inline:
```
courseId,holeNumber,teeSet,yards,color
lomond-country-club,1,back,531,blue
lomond-country-club,1,regular,509,green
...
```
72 Lomond rows (18 × 4). Absorbs Taiheiyo's 108 later without touching the main CSV.

**Why not inline:** 6 tees inline = +6 to +12 positional columns on a 19-column positional parser, putting the existing reward indices (7–12, 13–18) at risk. A separate file leaves every existing index untouched → zero regression surface on reward parsing.

**Main CSV:** add `courseId` **appended at index 19**, not inserted. Indices 0–18 must not move. `HoleDatabaseLoader` filters rows by `ActiveCourseContext.CurrentCourseSlug`; a missing/blank `courseId` defaults to `lomond-country-club` for backward compatibility.

**Also:** `MatchmakingModalController.cs:257` hard-codes `new HoleData("HOLE_LOMOND_5", 5)`. Leave the behaviour, but add a `// TODO(multi-course)` marker — it will need the active course once a second course ships.

---

## 4. Test gate

- **Re-baseline EditMode tests before starting.** NOTES cited 248/248 as of May; that number is unverified at HEAD — capture the real count first, then require no regressions.
- `BakedPivotRegressionTests` still PASS on Lomond Hole 1 after migration.
- `RealHoleTerrainTests` + `GreenTopologyTests` still PASS with relocated fixtures.
- New `ActiveCourseContextTests.cs` — Set / Reset / event fires / default slug (~4).
- New `CourseSlugResolverTests.cs` — lomond resolves, taiheiyo resolves, malformed returns null (~3).
- New `TeeDataTests.cs` — Lomond hole parses 4 tees, `TryGetTee` miss returns false, null colour survives round-trip.
- **Manual smoke:** Lomond Hole 1 — load, drive from tee, ball settles. Hole 7 (ravine repro) still classifies correctly. Hole 8 — tree collisions still fire (proves `tree_obstacles.csv` resolved at the new path).
- **Manual smoke, VISUAL (§1.7):** Hole Selection cards + Hole Complete modal show real hole art, **not** the `Missing` placeholder. Must be checked by eye — the `Missing` fallback means a broken path produces no log error.

---

## 5. Hard rules

1. **Bit-exact gate is sacred.** Migration is a *path change only*. Sim outputs must be byte-identical before and after.
2. **Do not touch** `BallSimulation.cs`, `Trajectory.cs`, `AeroModel.cs`, any aero CSV, any sim physics, the BallStateMachine asmdef, or `LoopCameraDirector`. This is data-path plumbing.
3. **No raw-YAML scene edits.** If a scene must change, Unity Editor MCP only.
4. `Resources.Load` path strings are the canonical interface — no file-path-based loading.
5. **Migration menu is one-shot** and self-disabling (§1.6).
6. **`CourseSlugResolver` returning null must fail loudly.** Never silently fall back to Lomond at a *bake* site — that is how Taiheiyo data ends up overwriting Lomond, which is the exact bug this task exists to prevent. (The *runtime* default in §1.4 is fine; the bake-side default is not.)

---

## 6. Out of scope (defer)

- HoleSelectionScreen course-tab UI — `HoleSelectionScreenController.cs:202` already has the filter, hardcoded to Lomond. Ships with Taiheiyo content.
- Per-course course-info card (splash, story, designer credit). Pure content.
- Club-roster course-awareness — clubs are course-agnostic.
- Save state per-course — owned by the Loop v2 save-state spec.
- Moving test fixtures out of `Resources/` entirely (see §1.5 NOTE).

---

## 7. Phase 2 follow-on

Taiheiyo content becomes "follow `Docs/Pipeline/ADD_HOLE.md` 18 times under the new slug" — no code. Queued spec already exists at `Docs/Specs/Queued/taiheiyo_club_gotenba_content`.
