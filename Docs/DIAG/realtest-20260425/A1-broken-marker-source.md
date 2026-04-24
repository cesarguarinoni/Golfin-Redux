# A1 — Broken Marker Source (Static Analysis)
**Date:** 2026-04-25  
**Method:** grep + manual read of HoleGeoImporter.cs, SyncPhysicsSurfaceMarkers.cs, SceneGroundProvider.cs

---

## AddComponent<Physics.Runtime.SurfaceMarker> call sites in HoleGeoImporter.cs

| Line | Surface Type | Context |
|------|-------------|---------|
| 2031 | Sand | Bunker mesh GO |
| 2623 | Green | Green mesh GO (primary) |
| 2740 | GreenCollar | Collar mesh GO |
| 2818 | Green | Green surface GO (sub-mesh path) |
| 2937 | Water | Water mesh GO |
| 4377 | Fairway | Fairway mesh GO (direct path) |
| 4535 | Tee | Tee mesh (CreateTeeMeshWithSkirt path) |
| 4620 | Tee | Tee mesh (second tee creation path) |
| 4747 | Tee | Tee mesh (third tee creation path, linear-slope skirt) |
| 5022 | CartPath | CartPath mesh (single-section) |
| 5258 | CartPath | CartPath mesh (multi-section loop, body section) |
| 5468 | CartPath | CartPath mesh (multi-section loop, connector section) |

All the above are `AddComponent<Golfin.Physics.Runtime.SurfaceMarker>()` with immediate `.Type = ...` assignment.  No string-based AddComponent. No MonoScript manipulation.

---

## GAP: CreateFlatContourMesh only adds Course marker — no Physics marker

**File:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs:4191`

```csharp
var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
marker.surfaceType = surfaceType;
return go;  // ← NO Physics.Runtime.SurfaceMarker added
```

`CreateEarClipContourMesh` (line 4196) delegates to `CreateFlatContourMesh` and inherits this gap.

**Impact:** Any zone GO created via these helper functions gets only a Course marker. A2 audit must determine which (if any) Hole_01 GOs were built via this path.

---

## GAP: SyncPhysicsSurfaceMarkers only UPDATES existing markers, does NOT create missing ones

**File:** `Assets/Scripts/Editor/SyncPhysicsSurfaceMarkers.cs:95-108`

```csharp
foreach (var physMarker in root.GetComponentsInChildren<SurfaceMarker>(true))
{
    var courseMaker = physMarker.GetComponent(courseSmType);
    if (courseMaker == null) continue;
    // ...updates physMarker.Type
}
```

This iterates over **already-existing** `Physics.Runtime.SurfaceMarker` components and corrects their `.Type` values. It does **not** detect GOs that have a Course marker but no Physics marker, and does **not** add Physics markers.

**Contradiction with done-report claim:** The previous session's done report states "+27 markers added" but the current SyncPhysicsSurfaceMarkers.cs code cannot add markers. Either:
(a) An inline Roslyn script run via MCP script-execute DID add markers (not in current codebase), OR
(b) The "27 added" referred to 27 Type values corrected on pre-existing Physics markers.

If (a): that Roslyn script is no longer on disk. Its behavior and the context it ran in (Assembly-CSharp vs Golfin.Physics.Runtime.asmdef) cannot be verified.

---

## Hypothesis: Source of `Golfin.Physics.Runtime::Golfin.Physics.Runtime.SurfaceMarker` broken reference

Unity's Inspector displays a component as `{asmdef_name}::{fully_qualified_class_name}` when the component's stored `m_Script` GUID cannot be resolved to any known MonoScript asset. The format matches:
- Assembly: `Golfin.Physics.Runtime` (the asmdef name)
- Class: `Golfin.Physics.Runtime.SurfaceMarker` (namespace + class)

**Most likely cause:** A Roslyn inline script run via MCP `script-execute` in the previous session compiled in Assembly-CSharp context. When such a script calls `AddComponent(typeof(Golfin.Physics.Runtime.SurfaceMarker))`, the `Type` object's `GUID` corresponds to the Assembly-CSharp compilation of that type. But the canonical MonoScript asset for `SurfaceMarker` is in `Golfin.Physics.Runtime.asmdef`. Unity stores the wrong GUID in the scene file (`.unity`), producing a component that it cannot resolve on subsequent domain reloads — the "zombie" component.

**Critical uncertainty:** This hypothesis cannot be confirmed from static code alone. A2 will show whether broken-script components are present post-restart and in what quantity. A4 will show whether they affect determinism across cold loads.

---

## Non-determinism hypothesis (pre-A2/A3/A4)

If zombie components from the Roslyn migration ARE partially resolving in some Unity domain reloads (plausible if Assembly-CSharp and Golfin.Physics.Runtime.asmdef compile to the same GUID under some circumstances), then some cold loads would have:
- 1 valid Physics marker per GO (expected)
- 1 zombie component that occasionally resolves to a second valid SurfaceMarker

`GetComponentInParent<SurfaceMarker>()` returns the FIRST matching component. With two valid markers of possibly different Types on the same GO, the return value depends on component slot order — which is not guaranteed stable across domain reloads.

**Alternative:** The non-determinism is simpler. `_useSceneProviders` may be false on some loads due to a timing race in `OnHoleLoaded`. A3 and A4 will check `_useSceneProviders` explicitly.

---

## Files with AddComponent<Physics.Runtime.SurfaceMarker> calls (full list)

- `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs` — 12 call sites (lines listed above)
- `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs` — 8 call sites (lines 2132, 2721, 2838, 2916, 3064, 4286, 4409, 4684) — HoleLite parallel, same pattern, same gaps
- **NOT in** SyncPhysicsSurfaceMarkers.cs — it only iterates, does not add
- **NOT** via string-based `AddComponent(string)` anywhere

---

## Summary

1. **Importer adds both Course + Physics markers correctly** for most zone types. No string-based `AddComponent` calls found.
2. **`CreateFlatContourMesh` is a gap** — only Course marker added. A2 will show if any Hole_01 GOs used this path.
3. **SyncPhysicsSurfaceMarkers cannot create missing Physics markers.** The previous session's "+27 added" claim is inconsistent with the current script.
4. **Zombie component hypothesis:** A Roslyn script (no longer in codebase) may have added Physics markers via Assembly-CSharp type resolution, producing broken-GUID components.
5. **Non-determinism root cause unclear from static analysis alone.** A2 (post-restart count) and A4 (3 cold loads) are the deciding tests.
