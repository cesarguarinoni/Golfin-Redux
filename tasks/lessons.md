# Lessons Learned

## Physics — IGroundProvider and Zone Mesh Height (PhysicsLab Hole1)

### HeightmapData only knows terrain — use SceneGroundProvider for scene with zone meshes
`HeightmapData.SampleHeight` returns the baked terrain heightmap Y value. Zone overlay meshes (greens, tees, bunkers, cart paths) sit 0.3–0.5m above the terrain — `HeightmapData` is unaware of them. If the ball simulation uses `HeightmapData` as its ground provider in a scene that has physical zone mesh colliders, the ball spawns and rolls at terrain height, visually below the green mesh surface.

**Fix:** `SceneGroundProvider : IGroundProvider` — raycasts from Y=500 downward, returns `hit.point.y` (the first physical surface). Hits the Green_1 MeshCollider (Y≈10.0m) before the terrain collider (Y≈9.6m).

**Rule:** For any PhysicsLab scene that has zone mesh colliders, use `SceneGroundProvider`. Reserve `HeightmapData` for headless/test scenarios or when you need slope normals for out-of-green simulation.

### SceneGroundProvider disables terrain slope — intentionally correct for greens
`BallSimulation.cs` uses `ground is HeightmapData hm` to get slope normals for the gravity-tangent term in `RunPuttPhase`. With `SceneGroundProvider` (not HeightmapData), `BallSimulation` uses flat normal (0,1,0) — no slope-gravity, no downhill pull. This is correct for the green surface (which should be effectively flat for putting). The putt stops naturally via rolling resistance.

### MeshRenderer changes in Play mode don't persist — use edit-mode script-execute
Enabling/disabling `Renderer.enabled` in Play mode (even via script-execute) reverts when Play mode exits. To persistently enable renderers on baked zone meshes: run the enable script in **Edit mode** with `EditorUtility.SetDirty(r)` on each modified component and `EditorSceneManager.SaveScene(scene)` after the loop.

### PhysicsLab camera reset: use trajectory.samples[0] not preset.Origin
`ShotPreset.Origin.y` may be 0 (preset-defined) even when the actual terrain/green is at Y≈10. After simulation runs, `trajectory.samples[0].position` is already terrain-snapped (ball starts at `groundHeight + ballRadius`). Always use the first sample position for the camera origin in `FireInternal`.

---

## Physics — Fixed-Point Precision (Phase 4)

### Use Dot(v, v) for stop detection, not Sqrt(|v|)
`fpMath.Sqrt` underestimates for small inputs. Newton's method initial guess (computed via bit-shift) can land BELOW the true square root; the first Newton step from below jumps above, triggering the `if (r >= prev) break` guard and returning the underestimate. Two consecutive `Sqrt` calls on slightly different small values can return the same raw integer, making `speed <= prevSpeed` fire spuriously and ending the roll phase before the ball has moved.
**Rule:** For stop detection, compare `fpMath.Dot(vel, vel)` (speed²) against `StopSpeed * StopSpeed`. Dot is pure multiply+add — no Sqrt, no precision loss at small magnitudes.
**Do not fix `fpMath.Sqrt` globally** — it is used throughout the aero model for velocity normalization, and changing its output shifts carry distances for all clubs, breaking previously-tuned tests.

### HeightmapData.SampleNormal — use one-sided differences at boundaries
Central differences at a grid boundary clamp the out-of-bounds sample to the boundary value (e.g. `SampleHeight(-cellX, z)` clamps to `SampleHeight(0, z)`), which halves the effective gradient. A 10° slope computes as only 5°, making rolling resistance win over gravity for the first few steps.
**Rule:** In `SampleNormal`, check `worldX <= OriginX` / `worldX >= OriginX + SizeX` (and same for Z) and use forward or backward differences at the boundary. Only use central differences for interior points.

### Assembly boundary: namespace collision with Golfin.Physics
Inside `namespace Golfin.Physics.Runtime`, the bare name `Physics` resolves to the `Golfin.Physics` namespace, not `UnityEngine.Physics`. Any call to `Physics.Raycast(...)` fails with `CS0234`.
**Rule:** Inside `Golfin.Physics.Runtime`, always qualify as `UnityEngine.Physics.Raycast(...)`. The same applies to any UnityEngine type whose name collides with a Golfin namespace segment.

### SurfaceConfig.Default must have per-surface values, not flat defaults
A flat default (e.g., Cr=0.40 for every surface) makes tests that use surface-specific properties (CartPath Cr=0.70, Sand Cr=0.15) meaningless — they all behave identically. Tests using `SurfaceConfig.Default` must be testing the real surface coefficients.
**Rule:** `SurfaceConfig.Default` must encode the canonical per-surface values from `surfaces.csv`. If the CSV changes, update `Default` to match. Water and OOB still need explicit overrides since they're terminal/special cases.

### Putt calibration: proportional rolling resistance model (Phase 5)
With `a = -k*v` (proportional rolling resistance), the stop distance is `d = v0/k * (1 - v_stop/v0)`, NOT `v0/k` (which ignores the stop threshold). For Green (k=0.10, v_stop=0.04), a 3m putt needs v0 ≈ 0.35 m/s. The spec's suggested 1.85 m/s was from a different (constant deceleration) model and would roll ~18.5m — not 3m.
**Rule:** When calibrating putt velocity for a target distance, compute `v0 = k*d / (1 - v_stop/(k*d + v_stop))` or solve iteratively. Read the model in `RunPuttPhase` before accepting spec velocity values at face value.

### Unity MCP: use scene-create / gameobject-create directly, not editor scripts
**Mistake:** When asked to create Unity scenes with GameObjects and components wired, wrote a `PhysicsLabSceneBuilder.cs` MonoBehaviour/editor script and had Cesar run it — then deleted the script after the user pointed out the Unity MCP has `scene-create`, `gameobject-create`, `gameobject-component-add`, `gameobject-component-modify`, `gameobject-set-parent`, and `scene-save` skills.
**Rule:** For any task that creates or modifies Unity scenes, GameObjects, or components, drive it directly via Unity MCP tools. Do NOT write an editor script just to call Unity APIs — that is extra indirection that requires Cesar to run it. The MCP tools ARE the Unity Editor.
**Sequence:** `scene-create` → `gameobject-create` → `gameobject-component-add` → `gameobject-component-modify` → `gameobject-set-parent` → `scene-save`. Use `script-execute` only for logic that cannot be expressed as a sequence of those calls (e.g., complex Roslyn one-shots).

### fp3 is a readonly struct — fields cannot be mutated in-place
`fp3.x`, `fp3.y`, `fp3.z` are `readonly` fields. You cannot write `v.x = fp.FromFloat(...)`. You must construct a new instance: `new fp3(newX, v.y, v.z)`.
**Rule:** Whenever modifying a single component of an `fp3` (e.g., inside a lambda or loop), always construct a full `new fp3(...)` replacing all three components.

### Assembly reference: TMP in asmdef with overrideReferences
When an asmdef uses `"overrideReferences": true`, TextMeshPro is NOT auto-referenced — it must be added as a GUID reference: `"GUID:6055be8ebefd69e48b49212b09b47b2f"` (path: `Packages/com.unity.ugui/Runtime/TMP/Unity.TextMeshPro.asmdef`).
**Rule:** If a Viewer/UI asmdef has `overrideReferences: true` and uses TMP types, always add the TMP GUID reference explicitly.

---

## UHoleGeo Pipeline

### Topology-critical chain rescue in skeleton extraction
**Mistake:** Using a blanket endpoint-frequency check across ALL raw chains to identify branch nodes. Junction clusters in the downsampled skeleton produce many tiny 2-3px chains between adjacent branch pixels, all of which pass the "both endpoints are branch nodes" filter. Keeping them causes cascading 2-way merges that collapse the entire network into one chain.
**Rule:** Rescue short chains using the LONG chains set as the reference: compute 2-way junctions from longChains (len≥minSpinePixels) only. A short chain that touches a 2-way junction upgrades it to 3-way. Add minimum length floor (`dsFactor*2`) to exclude single-pixel intra-cluster fragments. Never compute branch nodes from all raw chains.

### "Both endpoints are branch nodes" filter is too broad
**Symptom:** Adding a "keep junction bridges" rule increased chain count from 8 to 29 and merged everything into 1 path — worse than before.
**Root cause:** The downsampled skeleton has junction clusters (several adjacent branch pixels), so EVERY pixel in a cluster is a branch node. Tiny 2-3px chains within the cluster all appear as bridges between branch nodes.
**Correct approach:** Check specifically whether the chain's endpoint is a 2-way junction in the long-chain set, not whether it's a branch node across all chains.

## Git / Version Control

### ALWAYS push after changes
User requested: push to GitHub after every change, not just on request.
Pattern: `git add <files> && git commit -m "..." && git push`

### git checkout reverts too much
**Mistake:** Used `git checkout -- <file>` to undo a specific change, but it reverted the file
to the last commit — erasing other unrelated fixes in the same file.
**Rule:** Before reverting, read the file carefully and do a targeted Edit instead.
If you must revert, cherry-pick only the specific lines that need to change.

### Revert removes multiple fixes at once
When `CharacterDetailPanel.cs` was reverted, it lost:
- `selectButton.interactable = !isSelected`
- Level Up / Boost button disabled state logic
**Rule:** Never use `git checkout` on a file that has multiple accumulated fixes.
Use `Edit` to surgically restore just the broken part.

## Unity Transform / Hierarchy

### SetParent worldPositionStays cancels parent Y offsets
**Mistake:** Set a parent GO's `localPosition.y = -0.03f` to lower all children,
but children were positioned in world space BEFORE parenting. Unity's
`SetParent(t, worldPositionStays: true)` auto-adjusts `localPosition` to
preserve the world position, so localPosition.y becomes +0.03f and the offset
is cancelled entirely.
**Rule:** Never apply a Y correction on a parent container to fix child mesh
positions. Bake the correction into the vertex Y values or into the child GO's
world position AFTER parenting (set localPosition explicitly).

## Physics LUT Tuning

### CSV values in test helper must exactly match the CSV files being tested
**Mistake:** `MakeLutConfig()` in the test file had stale drag LUT values (Cd=0.50 at 5-55 m/s) while the "finalized" values from script-execute diagnostics (Cd=0.16 at low speeds) were never applied to either the CSV or the test helper. Test 8 showed all clubs at ~50% of expected carry because of the stale high-Cd values.
**Rule:** Whenever you tune LUT values via script-execute, immediately update both (a) the CSV file and (b) the inline `MakeLutConfig()` equivalent in the test. They must stay in sync or tests become misleading.

### S-monotonicity: spin parameter only increases during a golf ball's flight
The spin parameter S = r·ω/|v| increases as the ball decelerates (v decreases). This means a club starting at S₀ will never sample LUT values at S < S₀ after launch. Safe to tune S > S₀ breakpoints in isolation without affecting that club's early flight.

### SpinDragFactor differentiates clubs with the same speed but different spin
A single 1D drag LUT on speed alone cannot distinguish Iron3 (65 m/s, 461 rad/s) from a hypothetical club at 65 m/s with higher spin. Adding `SpinDragFactor × S²` to Cd gives clubs at the same speed different effective drag based on their spin rate.

### Iron3 model limitation — 1D drag LUT cannot fix a speed-boundary club
Iron3 launches exactly at 65 m/s, the boundary between the low-Cd and high-Cd LUT zones. It spends almost no time in the high-Cd zone before decelerating into the low-Cd zone. Its low spin (S≈0.15) gives negligible spin-induced drag. Fixing Iron3 requires either a 2D drag LUT (speed × spin) or per-club drag parameters — the current 1D model cannot get Iron3 within 5%.

### Spin decay moves clubs toward higher Cl, not lower
Exponential spin decay (ω → ω×(1-k×dt)) reduces ω, which reduces S. Lower S means clubs spend more time on the rising/peak portion of the Cl curve, increasing lift and carry. This is the opposite of what's needed when trying to reduce carry for over-shooting clubs. Spin decay is useful for modeling reality but not for carry reduction tuning.

## Unity Package Manager

### Always commit manifest.json when a package is required by code
**Mistake:** `com.unity.recorder` was installed locally in Unity but never added to `Packages/manifest.json`. A package resolve wiped it, breaking compilation.
**Rule:** ANY time code has a `using UnityEditor.Recorder` (or any package namespace), verify the package is in `manifest.json` BEFORE writing that code. If it's missing, add it and commit `manifest.json` alongside the script — never let them diverge.
**Check:** `grep -r "com.unity.recorder" Packages/manifest.json` — must return a result if the Recorder API is used anywhere in the project.

## Unity / C# Patterns

### CS0136 — duplicate local variable in same scope
If a variable is declared at the top of a method (e.g., `int maxLevel`), don't redeclare it
in an inner block. Use the existing variable or rename.

### Singleton null guard in OnEnable/OnDisable
Always wrap event subscriptions in `if (SomeSingleton.Instance != null)` — singletons may not
be initialized when OnEnable fires during scene load.

### ContentSizeFitter required for HorizontalLayoutGroup to size content
Without `ContentSizeFitter.horizontalFit = PreferredSize`, content width collapses and cards
compress. Always add it to the Content object of a ScrollRect.

### CSV-first pattern for character data
`CharacterDatabaseCSV.Instance?.GetCharacter(id)` returns runtime data for all 12 characters.
`CharacterManager.Instance.GetCharacterTemplate(id)` returns ScriptableObject data (may only
have a subset of characters). Always try CSV first, SO as fallback.

### Viewport is the clipping boundary, not the layout group
Expanding layout group padding does NOT fix card clipping on scale-up.
The `ScrollRect.viewport` RectTransform is what clips. Expand it via `offsetMin`/`offsetMax`.
Guard with a bool (`viewportExpanded`) to prevent cumulative expansion on repeated calls.

### Image.enabled = false works for background hiding
Setting `rarityBadgeImage.enabled = false` correctly hides the background Image.
If it appears not to work, check that the SerializeField is wired to the correct Image component
in the Unity Inspector.

## UI / Design

### Gold color for selected state
Use `new Color(1f, 0.8f, 0.2f, 1f)` as gold for selected button tint.
Apply via `selectButton.GetComponent<Image>().color = goldColor`.

## Editor Fix Scripts — Use Generic Component Search, Not Hardcoded Paths

**Mistake:** FixBarImageTypes.cs used hardcoded Transform.Find() paths. When the user had reorganised the hierarchy for layout fixes, the script missed bars that had moved.

**Rule:** One-shot fix/patch editor scripts that target a component type across the scene should use `Object.FindObjectsOfType<T>()` or recursive search by component, filtered by name if needed — never hardcoded full paths. Hardcoded paths are brittle and break silently when the user adjusts the hierarchy.

**Pattern to use:**
```csharp
// Find ALL Image components in scene, filter by GameObject name
foreach (var img in Object.FindObjectsOfType<Image>())
{
    if (img.gameObject.name == "Bar" || img.gameObject.name == "BarPending")
    {
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = 0;
        EditorUtility.SetDirty(img);
    }
}
```
This survives any hierarchy reorganisation the user makes.

## ScreenManager Must Drive PersistentUIManager Bar Visibility

**Mistake:** `PersistentUIManager` had `ShowBars()`/`HideBars()` but nothing called them.
`Awake()` hides bars; there was no code to show them when navigating to Home or Roster.

**Rule:** Any screen manager that controls screen transitions MUST also call
`PersistentUIManager.Instance?.ShowBars()` / `HideBars()` in the same `ApplyScreen()` method.
Never leave bar visibility untriggered — it will silently stay hidden.

**Pattern:**
```csharp
bool showBars = screenId == ScreenId.Home || screenId == ScreenId.Roster;
if (Golfin.UI.PersistentUIManager.Instance != null)
{
    if (showBars) Golfin.UI.PersistentUIManager.Instance.ShowBars();
    else          Golfin.UI.PersistentUIManager.Instance.HideBars();
}
```

## UHole Geo — CLI regen must sync to export folder for Unity to pick it up

**Background:** CLI writes to `output/{courseId}/holes/{nn}/` but Unity's `HoleGeoImporter`
reads from `output/{courseId}/export/hole-{nn}/`. These are different paths.

**Fix applied (2026-04-17):** `generate-terrain.mjs` now copies `heightmap.raw` to the export
folder and patches `hole-manifest.json`'s terrain block (width/length/min/max/resolution) after
each regen. Unity import works directly after CLI regen — no UHole Geo GUI step needed.

**If export dir doesn't exist yet:** The script logs "Export dir not found — skipping sync".
In that case the user does need to run a full export from UHole Geo GUI first to create it.

---

## Course Importer — Shore Ramp Artifacts (2026-04-17)

### Never use chamfer distance for terrain ramps — use exact polygon-edge distance

**Problem:** The shore ramp computes a t-value from `distToWater` (chamfer distance transform
from the rasterized water mask) and lerps terrain height from `waterY` to `originalH`. The result
showed persistent vertical stripe/spike artifacts along the waterline.

**Root cause:** Any chamfer distance field computed from a rasterized polygon boundary has
**Voronoi boundaries** — where adjacent cells are "owned" by different boundary pixels, their
distances differ discontinuously. These discontinuities in t propagate directly into height
discontinuities in the lerp. The more the terrain rises above water (larger `originalH - waterY`),
the more visible the stripes become. **This cannot be fixed by blurring** — Voronoi edges are
real discontinuities, not noise. Blurring the distance field softens them but doesn't remove them.
Blurring the heights after the lerp creates new artifacts: the hard mask boundary turns into
visible stairs where blurred ramp cells meet restored non-ramp cells.

**Fix:** Compute the exact Euclidean distance from each terrain cell to the nearest **polygon
edge** of the water contour, not the chamfer distance from the rasterized water mask.

```csharp
// For each candidate cell (pre-culled by coarse chamfer):
float wx = terrainPos.x + x * cellW;
float wz = terrainPos.z + z * cellH;

float minDistM = float.MaxValue;
foreach (var (pts, surfNorm) in waterContours)
{
    int n = pts.Length;
    for (int i = 0; i < n; i++)
    {
        int j = (i + 1) % n;
        float ax = pts[i].x, az = pts[i].z;
        float bx = pts[j].x, bz = pts[j].z;
        float edx = bx - ax, edz = bz - az;
        float len2 = edx * edx + edz * edz;
        float t2 = len2 > 1e-10f
            ? Mathf.Clamp01(((wx - ax) * edx + (wz - az) * edz) / len2)
            : 0f;
        float px = ax + t2 * edx - wx;
        float pz = az + t2 * edz - wz;
        float d = Mathf.Sqrt(px * px + pz * pz);
        if (d < minDistM) { minDistM = d; nearSurfY = surfNorm; }
    }
}
float t = minDistM / shoreRadiusM;
t = t * t * (3f - 2f * t); // smoothstep
```

**Why it works:** Polygon edges are smooth geometry. The distance from a point to a smooth
polygon boundary is a smooth function — no Voronoi artifacts, no stripes. Use a coarse chamfer
pass first to cull distant cells (performance), then exact distance only for the ramp zone.

---

## EditMode Physics Tests — Use BoxCollider, Not MeshCollider (Quad)

**Problem:** `CreatePrimitive(PrimitiveType.Quad)` adds a `MeshCollider`, which requires async mesh cooking. After `yield return null`, the collider is not yet registered in PhysX. `Physics.RaycastAll` returns 0 hits → all snap tests return `defaultY` → tests fail silently.

**Fix:** Create a bare `new GameObject()` and add `BoxCollider` directly:
```csharp
var go = new GameObject("FlatCollider");
go.transform.position = new Vector3(x, y, z);
go.AddComponent<BoxCollider>().size = new Vector3(size, 0.02f, size);
```
`BoxCollider` registers synchronously in PhysX. One `yield return null` is enough for it to appear in raycasts. The top face lands at `center.y + halfExtents.y = y + 0.01`, so assertions must account for this offset.

**Rule:** In EditMode tests that need physics raycasts, always use `BoxCollider` (or `SphereCollider`). Never use `MeshCollider` or `CreatePrimitive` variants (Quad, Plane, Cube) — they all add `MeshCollider` internally.

---

## NUnit Float Tolerance — Use Assert.That, Not Assert.AreEqual

**Problem:** `Assert.AreEqual(float expected, float actual, float delta, string msg)` causes `error CS1503` — NUnit's overload has `(object, object, string)` as the 3-arg form; the 4th arg expects `object` not `string`. Also `Assert.AreNotEqual` has no float-delta overload.

**Fix:** Always use `Assert.That` with constraint syntax:
```csharp
Assert.That(result, Is.EqualTo(10.15f).Within(0.05f), "message");
Assert.That(result, Is.GreaterThan(0.5f), "message");
Assert.That(result, Is.LessThan(10.17f), "message");
Assert.That(result, Is.LessThanOrEqualTo(0.5f), "message");
Assert.That(a, Is.Not.EqualTo(b).Within(0.05f), "message");
```
**Rule:** Never use `Assert.AreEqual(float, float, float)` — always `Assert.That(..., Is.EqualTo(...).Within(...))`.

---

## BallAnimator.DestroyInstance — DestroyImmediate in EditMode

**Problem:** `BallAnimator.DestroyInstance()` calls `Destroy(_instance)`. In EditMode tests (NUnit + UnityTest runner), Unity logs `[Error] Destroy may not be called from edit mode!` — the test runner treats unhandled error logs as test failures.

**Fix:** Guard with `#if UNITY_EDITOR`:
```csharp
#if UNITY_EDITOR
    DestroyImmediate(_instance);
#else
    Destroy(_instance);
#endif
```
**Rule:** Any production code that destroys GameObjects and may run in EditMode tests must use this pattern. `Destroy` is runtime-only; `DestroyImmediate` is the editor equivalent.

**What NOT to do (confirmed failures):**
- Blurring `distToWater` (Gaussian, any sigma) — reduces stripes but can't eliminate Voronoi edges
- Blurring ramp heights (separable Gaussian + restore non-ramp) — creates stair artifacts at mask boundary
- Masked 2D Gaussian on ramp cells only — also creates stair artifacts where ramp meets terrain
- Multiple blur passes — same failure modes, just slower

---

## Course Importer — Spline Cart Paths (2026-04-16)

### Spline cart paths: use `com.unity.splines` (v2.8.4)

`SplineUtility.CalculateLength<T>(T, float4x4)` requires a transform as second argument.
Always pass `float4x4.identity` when the spline is already in world/local space:
```csharp
float len = SplineUtility.CalculateLength(spline, float4x4.identity);
```

### `sed` corrupts C# comment lines starting with `//`

When using `sed -i 's/old/new/'` on Windows (Git bash `sed`), comment lines can get their
`//` replaced with `\`. Always use `Edit` tool for C# file changes — never `sed`.
A corrupted `\` on a line causes a compile error that Unity silently ignores by running the
last cached compiled version, making it look like the code ran but did nothing.

### Splatmap painting of cart path texture causes a visible border around the mesh

The old splatmap code painted asphalt texture on the terrain using `BuildSpinePolygon()`,
which was wider than the spline mesh on curves. The painted asphalt texture showed up as a
dark border in the grass beyond the road edge.
**Rule:** When a road/path is a mesh overlay, remove all splatmap painting for that surface.
The mesh material handles the visual. Painting the terrain underneath is redundant and creates
visible artifacts at the edges.

### Cart path terrain depression: flat drop, not gradient ramp

**Wrong:** Original depression used a smoothstep gradient ramp (center=100%, edge=0%).
This left terrain at the mesh edge barely depressed — terrain poked through on concave slopes.

**Also wrong:** Outward ramp (full drop inside, taper outside) — depresses grass beyond the
road boundary, creating a visible dark ledge around the road.

**Correct:** Flat drop exactly under the mesh footprint. The mesh itself covers the edge so
no ramp or gradient is needed. Terrain outside the road stays at natural height.
```csharp
// Flat drop only
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (cartDepress[hz, hx])
            heights[hz, hx] = Mathf.Max(0f, heights[hz, hx] - dropNormalized);
```

### Depression polygon must be INSET from mesh edge, not flush or extended

Building the depression polygon at exactly the mesh edge width still marks some cells outside
the mesh (floating point boundary effects + cell-center sampling). Building it wider makes it
visibly bleed into the grass.
**Rule:** For overlay meshes with no fringe, inset the depression polygon by ~0.3m from the
mesh edge: `depHalfWidth = halfWidth - 0.3f`. The mesh covers the inset gap invisibly.

### Depression polygon: use spline right-vector offsets, not mesh edge vert positions

Building the polygon from `leftVerts`/`rightVerts` (actual mesh verts) seems exact but those
verts include terrain height variation (Y) and the XZ positions can drift from the spline
centerline on curves. Using `pos ± right * depHalfWidth` from `SplineUtility.Evaluate` is
cleaner and more predictable for a 2D polygon.

### Static field for cross-method polygon passing

When `CreateSplineCartPaths()` needs to pass depression polygons to `DepressTerrainUnderOverlays()`,
use a `private static List<Vector2[]> _splineCartPathPolygons` field. Reset it at the start of
`CreateSplineCartPaths()` and check for null/empty in `DepressTerrainUnderOverlays()` with a
fallback to the old approach.

### `pos.y` from spline evaluation is NOT reliable for terrain conformance

If spine points are sparse (e.g., one knot every 5-10m), the AutoSmooth Bézier Y between
knots can deviate significantly from actual terrain height — causing the mesh to float or sink.
Fixing this by subdividing the spine to 1m knots and using `pos.y` made the mesh worse (the
Bézier Y overshoots/undershoots between dense knots). Per-sample `terrain.SampleHeight()` at
the centerline is the correct approach for terrain-conforming paths.

### Spline tangent degenerate case

When `tangentFlat = new float3(tangent.x, 0, tangent.z)` has near-zero length (vertical
segment or path doubling back), `math.normalize` produces NaN. Always guard:
```csharp
if (math.lengthsq(tangentFlat) < 0.001f)
    tangentFlat = new float3(1, 0, 0); // fallback to X axis
else
    tangentFlat = math.normalize(tangentFlat);
```

### `MarkWorldContourCells` vs `MarkContourCells`

- `MarkContourCells` — takes `ContourPoint[]` in local meter coords, applies `DepressionInsetMeters` inset automatically
- `MarkWorldContourCells` — takes `Vector2[]` in world XZ coords, NO inset applied
Always use `MarkWorldContourCells` for polygons already in world space (e.g. built from spline verts).

## Never Use ?? With Unity Objects — Use == null Instead

**Mistake:** Used `GetComponent<CanvasGroup>() ?? AddComponent<CanvasGroup>()` in `GetOrAddCG`.
C#'s `??` operator uses reference equality (`ReferenceEquals`), NOT Unity's overloaded `==`.
A destroyed/missing Unity component passes `??` but throws `MissingComponentException` on access.

**Rule:** Always use `== null` / `!= null` when checking Unity `UnityEngine.Object` references.
Never use `??` or `?.` for the null-coalescing/null-conditional part of Unity object checks.

**Pattern:**
```csharp
// WRONG — ?? can miss Unity-null objects:
var cg = obj.GetComponent<CanvasGroup>() ?? obj.AddComponent<CanvasGroup>();

// CORRECT — == null respects Unity's overloaded equality:
var cg = obj.GetComponent<CanvasGroup>();
if (cg == null) cg = obj.AddComponent<CanvasGroup>();
```

## Pre-Add CanvasGroup in Builder — Never Rely on Runtime AddComponent for Fades

**Rule:** If a GameObject will be faded (FadeIn/FadeOut via CanvasGroup), add the CanvasGroup
in the Editor builder script at build time, not lazily at runtime. Runtime AddComponent on objects
that may be inactive or mid-animation can produce stale references.

**Pattern (in builder):**
```csharp
var cg = clone.GetComponent<CanvasGroup>() ?? clone.AddComponent<CanvasGroup>();
cg.alpha = 0f;  // start transparent; FadeIn animates to 1
```

## Clone RightPanel for Compare Panel — Never Build From Scratch

**Rule:** When a compare/secondary panel must visually match an existing panel, clone it with
`Object.Instantiate(rightPanel.gameObject, parent, false)` rather than building from scratch.
Building from scratch requires duplicating every font/color/size the user set manually.
Cloning preserves all those settings automatically.

**After cloning:**
- Override the clone's RectTransform anchors to position it correctly
- Wrap all cloned children in a new empty container (CompareInfoPanel) for show/hide control
- Add ComparePlaceholder as a full-stretch overlay on top
- Strip any left-column-specific buttons from the clone's ButtonsPanel

## AutoWire Paths Must Be Verified Against Scene YAML — Don't Assume Names

**Mistake:** Assumed child names (RarityLabel, Text) without checking the actual scene YAML.
4 paths failed because real names were: RarityText, LevelPanel/LevelText, LevelPanel/LevelTextMax,
and "Text (TMP)" (not "Text").

**Rule:** For any AutoWire paths that aren't directly from CLAUDE.md documentation, grep the
ShellScene.unity for the actual `m_Name:` values. Use `m_Father` fileID cross-references to
confirm parent-child relationships before coding the paths.

## After Compare Swap — Explicitly Push New Character Into Detail Panel

**Problem:** CharacterDetailPanel.OnSelectionChanged refreshes the CURRENTLY DISPLAYED character's
button state, but never switches currentCharacterId to the newly selected character.
After a swap from compare mode, the panel kept showing the old character.

**Fix:** Add a public `ShowCharacter(string id)` method to CharacterDetailPanel that sets
`currentCharacterId` and calls `UpdatePanel`. Call it from CompareController after any swap,
AFTER CleanupAndExit() (which sets _isCompareMode = false so UpdatePanel doesn't early-return).

```csharp
// In CompareController:
private void CommitSwapAndExit(string characterId)
{
    CharacterManager.Instance.SelectCharacter(characterId);
    CleanupAndExit();  // sets _isCompareMode = false first
    GetComponent<CharacterDetailPanel>()?.ShowCharacter(characterId);
}
```

## HorizontalLayoutGroup Overrides LayoutElement Preferred Sizes for Thin Dividers

**Mistake:** Used `LayoutElement.preferredWidth = 1f` for thin divider Images inside a HLG. The HLG auto-sizes children based on `childForceExpand` and available space, overriding the preferred width entirely.

**Rule:** For absolutely-positioned overlays (dividers, indicators) inside a layout group, use `LayoutElement.ignoreLayout = true` and position manually via RectTransform anchors/sizeDelta.

**Pattern:**
```csharp
var le = divGO.AddComponent<LayoutElement>();
le.ignoreLayout = true;

var rt = divGO.GetComponent<RectTransform>();
float xPos = (float)(i + 1) / buttonCount; // normalized position between buttons
rt.anchorMin        = new Vector2(xPos, 0.15f);
rt.anchorMax        = new Vector2(xPos, 0.85f);
rt.sizeDelta        = new Vector2(1f, 0f);   // 1px wide, height from anchors
rt.anchoredPosition = Vector2.zero;
```

## FadeController GameObject May Be Inactive in Editor — Causes Missing Screen Transitions

**Mistake:** FadeController is left inactive in the scene during editing. Because `Awake()` never runs, `FadeController.Instance` stays null. ScreenManager's `FadeOutThenIn` call is skipped, and the Inventory screen either appears instantly or not at all depending on timing.

**Rule:** In `ScreenManager.Awake()`, find FadeController including inactive GameObjects and activate it before any screen transitions are attempted.

**Pattern:**
```csharp
if (FadeController.Instance == null)
{
    var fc = FindObjectOfType<FadeController>(includeInactive: true);
    if (fc != null) fc.gameObject.SetActive(true);
}
```

## Always Use New Input System — Never UnityEngine.Input

**Mistake:** Used `Input.GetKeyDown(KeyCode)` in a debug script. Project uses the New Input System package, so the legacy `UnityEngine.Input` class throws InvalidOperationException at runtime.

**Rule:** ALWAYS use `UnityEngine.InputSystem` in this project. Never use `UnityEngine.Input`.

**Pattern:**
```csharp
using UnityEngine.InputSystem;

// Key check (replaces Input.GetKeyDown):
if (Keyboard.current != null && Keyboard.current[Key.Backquote].wasPressedThisFrame) { }

// Mouse button (replaces Input.GetMouseButtonDown(0)):
if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) { }
```

## Never Use Namespace `Golfin.Debug` — Shadows UnityEngine.Debug

**Mistake:** Created `WalkCamera.cs` in `namespace Golfin.Debug`. Every file with `using UnityEngine;` that calls `Debug.Log` now resolves `Debug` to `Golfin.Debug` instead of `UnityEngine.Debug`, causing 100+ compile errors across the entire project.

**Rule:** Never create a namespace called `Golfin.Debug` (or any `*.Debug` namespace). The name collides with `UnityEngine.Debug` which is used everywhere. Put debug utilities in the global namespace or a non-colliding namespace like `Golfin.DebugTools`.

## Raycast Target on Decorative Images Blocks Button Clicks

**Repeat offender.** This has happened on both the Roster DetailPanel and the Club DetailPanel.

**Symptom:** Buttons exist, look correct, but don't respond to taps/clicks.

**Cause:** A non-interactive Image component (background, rim, portrait, decorative element) has `Raycast Target = true` and sits on top of or overlaps the button in the hierarchy. It eats the click before the button receives it.

**Fix:** Disable `Raycast Target` on ALL non-interactive Image components — backgrounds, rims, portraits, dividers, icons (unless the icon IS a button). Only Buttons and interactive elements should have Raycast Target enabled.

**Prevention:** When creating any new Image component in code or Inspector, immediately set `raycastTarget = false` unless it's intentionally interactive.

## ModalController — Root Must Stay Active

**Rule:** `ModalController` expects the **root GameObject to always be active**. It only toggles the `modalPanel` child via `Show()`/`Hide()`. If the root is inactive, `Show()` still runs (called directly in code, not via Unity events) and calls `modalPanel.SetActive(true)`, but nothing renders because the parent is inactive.

**Symptom:** Console shows `[Modal] X shown` but nothing appears in the hierarchy as active.

**Fix:** Ensure the modal root GameObject is enabled in the scene. Save the scene in that state so Play mode doesn't revert it.

## GameObject.Find() Misses Inactive Objects — Use FindObjectOfType in AutoWire Scripts

**Rule:** `GameObject.Find("Name")` silently returns null for inactive GameObjects. Since modals start hidden (`ModalController.Awake()` deactivates `modalPanel`), the root may be active but if it was ever saved inactive it won't be found.

**Pattern for all AutoWire scripts:**
```csharp
// WRONG — misses inactive objects:
var go = GameObject.Find("MyModal");

// CORRECT — finds inactive too:
var controller = Object.FindObjectOfType<MyModalController>(includeInactive: true);
var go = controller?.gameObject;
```

## Modal Anchor Repositioning Only Works at Canvas Root

**Mistake:** Copied anchor-repositioning logic (world→local coord math) from `LevelUpModalController` into `ClubLevelUpModalController`. The character modal lives at the Canvas root so the math works. The club modal lives inside `InventoryScreen/ContentArea`, which has its own transform offsets — the math lands in the wrong spot and overwrites the correct inspector position every `Open()` call.

**Rule:** If a modal is parented inside a screen hierarchy (not at Canvas root), remove all runtime repositioning code. Set position in the editor; it will hold at runtime.

## Rarity Color Switch — Match Project Canonical Colors, Not Intuition

**Mistake:** In `ItemDetailPanel.GetRarityColor()`, wrote Uncommon as green and Rare as blue —
the opposite of the project standard. The project canonical colors are:
- Common    → grey-blue  `~#BFBFCC`
- Uncommon  → blue       `new Color(0.29f, 0.56f, 0.89f)`  (matches RarityHelper)
- Rare      → green      `#50C878`  `new Color(0.314f, 0.784f, 0.471f)`
- Mythic    → amber      `#FFC107`  `new Color(1.00f, 0.757f, 0.027f)`
- Legendary → orange     `new Color(1.00f, 0.65f, 0.10f)`
- Supreme   → red        `new Color(1.00f, 0.30f, 0.30f)`

**Rule:** When writing a local rarity color switch (for string-based rarities that can't use
`RarityHelper` enum), always cross-check against `RarityHelper.GetRarityColor()` in
`CharacterDatabase.cs` before writing the values. Don't assume which color maps to which rarity.

---

## Editor Scripts — Always Search Including Inactive Objects

**Mistake (repeated):** Used `GameObject.Find("DetailPanel")` and `Object.FindObjectOfType<T>()` in editor scripts. Both silently return null for inactive GameObjects, which is the normal state for screens and modals in this project.

**Rules:**
- For finding by name: use `Resources.FindObjectsOfTypeAll<GameObject>()` filtered by `go.name == "X" && go.scene.isLoaded`
- For finding by type: use `Object.FindObjectOfType<T>(true)` (the `true` = includeInactive)

**Pattern:**
```csharp
// By name (finds inactive):
foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
    if (go.name == "DetailPanel" && go.scene.isLoaded) return go.transform;

// By type (finds inactive):
var modal = Object.FindObjectOfType<LevelUpModalController>(true);
```

---

## Terrain Distance Fields: Chamfer vs Exact Polygon-Edge Distance

**Mistake (repeated twice — water shore ramp and tee skirt):** Used a chamfer distance transform as the distance input to a smoothstep lerp that was driving a height ramp. Produced visible banding/stripes on the resulting slope.

**Two distinct failure modes, both from chamfer, but requiring different fixes:**

1. **1-cell Voronoi noise.** Chamfer from a rasterized polygon mask has ~1-cell-wide radial "spokes" of equal distance. If the polygon contour has very fine vertex spacing (< 1 cell), these are the only artifacts — fixable with a Gaussian blur on the distance field.

2. **N-cell polygon-edge plateaus.** If the contour has vertices spaced ~Ncell apart (tees on this project: ~1.5m spacing = ~13 cells), each polygon edge rasterizes into a row of cells sharing identical chamfer distance. The "spokes" are N cells wide. A Gaussian blur of kernel width < N does NOTHING — it just averages identical values together.

**Diagnostic signature:** If you blur the distance field with progressively larger kernels and the banding doesn't move, it's N-cell edge-plateau banding, not 1-cell Voronoi noise.

**Fix for case 2:** Replace chamfer with exact perpendicular distance to the polygon edge. Use the chamfer as a cheap cull (coarse ring of cells), then iterate polygon edges per cell and take the min perpendicular distance. Exact distance is a continuous function of world position — no plateaus, no stripes.

**Reference implementation:** `HoleGeoImporter.cs::FlattenTerrainUnderTees` (line ~3189, exact-distance pass) and the water shore ramp at line ~3453.

---

## Serrated Grass Texture = Ramp Is Too Steep (Not a Boundary Discontinuity)

**Mistake:** Spent three rounds chasing a "C1 gradient discontinuity at the skirt outer boundary" hypothesis for a tee-mound rendering artifact. Specified fade-out write weights, median platform heights, and dual cut-and-fill merges. None worked.

**The actual cause:** Unity's terrain grass shader stretches grass texture vertically when a triangle face is steeper than ~45°. The tee's smoothstep ramp was trying to drop 7.93m over 2m horizontal — a 76° slope — rendered as a vertical cliff face with stretched-grass artifact.

**Diagnostic signatures:**

- **Serrated/streaked grass texture in a band** = Unity rendering a slope steeper than ~45°. It's a rendered ramp face, not a boundary crease.
- **Smooth, visible crease in a thin line** = lighting discontinuity from mismatched triangle normals at a C1-discontinuous boundary.

The two look superficially similar but have completely different fixes.

**Fix:** If the ramp is too steep, the ramp WIDTH must increase. Per-cell adaptive skirt radius based on `|platformY - baseline| / maxSlope` is how real courses handle this — flat sides get a small skirt, steep sides get a long gentle grade-merge that stays below ~19°.

**Key data point that unlocked this:** sampled the natural DEM and found the surrounding hillside naturally drops 8m over ~13m (avg 32°). Our 2m skirt was compressing the first 8m of that drop into a 2m-wide band, artificially 4× steeper than nature. Widening the skirt to match the natural slope ADDS ≤ 1m of lift in the adaptive region — visually invisible, but enough to restore a walkable ramp face.

**Reference implementation:** `HoleGeoImporter.cs::FlattenTerrainUnderTees`, `TeeMaxRampSlope = 0.35f`, per-cell `adaptiveM = clamp(1.5 × dropAbs / maxSlope, base, cap)`.

---

## When a Fix Fails 2–3 Times, Stop Iterating — Do an Adversarial Review

**Mistake (a meta-lesson reinforcing what `Rules.md` already says):** When the tee-mound fix didn't work after three attempts, the fourth attempt was another variation of the same shape ("reduce the height differential" → median platform → dual cut/fill). It also didn't work, AND it broke something else.

**The pattern:** after each failure I was specifying the "next natural step" along the same solution-shape — narrower, wider, re-center, cut, fill. Each variation felt small and justified. But they were all rationalizing the same underlying (wrong) hypothesis.

**Signal that you're in this failure mode:** Your fourth spec would also be "another small variation of the same idea."

**What broke the loop:** An explicit adversarial review. Attack your own hypothesis with "but why would this cause X?" questions. For the tee issue, the attack that killed the hypothesis was: *"If this is a C1 boundary discontinuity, why is it serrated instead of a smooth crease?"* — which forced investigation of what Unity's terrain shader actually renders under various conditions, and uncovered that the "serration" was a steep-slope rendering artifact, not a C1 kink.

**Rule:** After 2 failed attempts at the same conceptual fix, write a spec for an adversarial review instead of another variation. Attack:
1. **The visual signature** — does the actual appearance match what your hypothesis would produce?
2. **The symmetry** — if the bug is in code X, why does it appear in some places and not others?
3. **The math** — simulate it with sampled data. Numbers catch wrong assumptions that prose doesn't.
4. **The sampled reality** — measure the actual DEM/data. Your assumed values are often wrong by an order of magnitude.

For tees: simulation of `dR = drop / maxSlope` showed my first adaptive formulation didn't bound the ramp slope (attack 3, math). Data sampling of Hole 4 showed the real drop was 7.93m, not my assumed 2m (attack 4, data). Both caught before writing a spec.

**The adversarial review cost ~30 min of thinking. The three wrong specs before it cost a day of implementation and rework.**

---

## Water Shore — Inner Collar Fixes Boundary Cliff (2026-04-20)

### Depression polygon boundaries always need a matching inner ramp

**Problem:** `DepressTerrainUnderOverlays` set all cells inside the water polygon to bed level (`surfaceNorm - 0.3m`). The shore ramp on the OUTSIDE set boundary cells (distance=0) to `surfaceNorm`. This created a 0.3m cliff at every polygon-edge cell → per-cell vertical pillars → stretched grass shader → serration artifact.

**Attempted wrong fix:** Moving `CreateWaterMeshes` to run after depression. This caused `terrain.SampleHeight()` at contour vertices to return depressed bed values → `waterY` sank the entire water mesh underground.

**Correct fix:** Inner collar ramp. For cells inside the polygon, compute chamfer distance from the boundary inward. Cells within `ShoreRadius` smoothstep-lerp from `surfaceNorm` (at the edge) to `waterFloorY` (at ShoreRadius cells in). Both sides of the boundary are now co-planar at `surfaceNorm` → no cliff → no serrations.

**Rule:** Any time terrain is abruptly depressed inside a polygon, the cells just inside the boundary must ramp back up to meet whatever surface the outside is transitioning from. The outside shore ramp (surfaceNorm → originalH outward) must be mirrored by an inner collar (surfaceNorm → floorY inward). Both ramps use the same width (`ShoreRadius`) for symmetric transitions.

**CreateWaterMeshes must always sample original (undepressed) terrain** for `waterY` computation. Keep it before `DepressTerrainUnderOverlays`.

---

## Unity Error Pause Kills Input — Debug.LogError in Awake Pauses Play Mode

**Symptom:** ALL input dead in a scene — mouse reads (0,0), leftButton=False, UI buttons completely unresponsive. The same Input System code works perfectly in a different scene. `InputSystemSourceDebugLog` logs `action.pressed=False` every 0.25s with no change even when clicking.

**Root cause:** A MonoBehaviour's `Awake()` fires `Debug.LogError()`. Unity's Console has "Error Pause" enabled by default. Any `LogError` causes Unity to pause play mode after the current frame. In paused state, each Game View click only steps ONE frame — so buttons never complete their click cycle and mouse position is frozen from the previous (pre-click) frame. Input appears completely dead.

**Diagnosis:** Disable all root GameObjects one at a time in the broken scene. When disabling `HeightProvider` made buttons work instantly, that was the culprit. The `HeightProvider.Awake()` called `Debug.LogError` because its `heightmapAsset` field referenced a deleted `.bytes` file.

**Fix:** Remove the offending GameObject. If the component is unused (as `PhysicsLabController._heightProvider` was — a serialized field never read in code), delete the GO entirely from the scene YAML.

**Rule:** If input appears dead in a scene but works elsewhere, check Console for any `LogError` firing in `Awake()`/`Start()`. The Error Pause feature is the most likely culprit. Toggle Error Pause off temporarily to confirm (red stop-button icon in Console toolbar).

---

## Unity Additive Scene Lighting — CopyHoleLighting Pattern

### RenderSettings are per-active-scene; additive loads don't inherit environment

When a hole scene is loaded additively (`LoadSceneMode.Additive`), `RenderSettings` are still driven by the **active scene** (LabScaffold). Renderers in the hole scene (e.g. URPWater with `_REFLECTIONMODE_PROBES`) sample the active scene's environment — which may be a default skybox with no probes, causing the water to render gray.

**Fix — `CopyHoleLighting(Scene holeScene)`:**
1. Temporarily call `SceneManager.SetActiveScene(holeScene)` — this makes `RenderSettings` read from the hole.
2. Snapshot every field: `skybox`, `ambientMode`, `ambientSkyColor/Equator/Ground`, `ambientLight`, `ambientIntensity`, `fog*`, `defaultReflectionMode`, `reflectionIntensity/Bounces`, `customReflectionTexture`, `sun`.
3. Restore LabScaffold as active: `SceneManager.SetActiveScene(scaffoldScene)`.
4. Write all snapshotted values into the now-active LabScaffold's `RenderSettings`.
5. Call `DynamicGI.UpdateEnvironment()` to regenerate the ambient probe and env cubemap.

**Call site:** at the end of `OnHoleLoaded`, BEFORE `SetupAtTee`.
**Also restore active scene** in `OnHoleUnloaded` (set LabScaffold active again).

### ReflectionProbeClearFlags — use `.Skybox` not `.Sky`

`ReflectionProbeClearFlags.Sky` does not exist — it's `.Skybox`. CS0117 compile error otherwise.

---

## MCP script-execute Runs in Editor Context — Cannot Test Runtime Material Changes

`script-execute` always executes in the Unity Editor (not play mode). `renderer.material` in a script-execute creates an **edit-mode material instance**, not the runtime play-mode instance. Any keyword changes made there will NOT be visible during play mode — the runtime creates its own instance.

**Rule:** Do not use `script-execute` to verify or patch runtime material keywords. To confirm runtime material state, check `Debug.Log` output via Unity Console in play mode, or check the scene screenshot after entering play mode.

---

## Struct Fields Cannot Be Null-Checked — Use a Bool Flag Instead

`AeroConfig`, `WindConfig`, `SurfaceConfig`, `PuttConfig` are **value types (structs)**. The compiler will reject `if (AeroCfg == null)` with CS0019 ("operator == cannot be applied to struct").

**Pattern — `EnsureConfigsLoaded()` with bool guard:**
```csharp
bool _configsLoaded;
void EnsureConfigsLoaded()
{
    if (_configsLoaded) return;
    AeroCfg    = PhysicsConfigLoader.LoadAeroConfig();
    WindCfg    = PhysicsConfigLoader.LoadWindConfig();
    SurfaceCfg = PhysicsConfigLoader.LoadSurfaceConfig();
    PuttCfg    = PhysicsConfigLoader.LoadPuttConfig();
    _configsLoaded = true;
}
```
Call from both `Awake()` and any method that needs configs (e.g. `ComputeMaxCarryYards`) for edit-mode safety.

---

## MCP script-execute — Use Skill/stdin, Not tmp JSON Files

Use the `script-execute` MCP skill directly via `Skill` tool or stdin pipe, never intermediate JSON files:
```bash
npx unity-mcp-cli run-tool script-execute --input-file - <<'EOF'
{"csharpCode": "...", "className": "Script", "methodName": "Main"}
EOF
```
JSON files are no faster, add repo noise, and get left behind in the project root.

**Rule:** For complex multi-line code, use a heredoc. Only write to a temp file if the shell escaping is genuinely unresolvable. Never leave `tmp_*.json` files in the project root.

---

## Session Conventions (Cesar's standing rules)

### "See you space cowboy" — end of session only
Only say "See you space cowboy" when Cesar explicitly signals the session is over. Never use it after completing a task mid-session.

### Always end task reports with a file summary
After completing any task, end the report with a table listing every file written/modified and its status (done, pending, etc.). Example:

| File | Status |
|---|---|
| `Assets/Scripts/Physics/Tests/StatResolverTests.cs` | ✅ done |
| `Docs/AI_CONTEXT.md` | ✅ done |

### Always use Unity MCP to interact with Unity
Use Unity MCP tools (`tests-run`, `script-execute`, `gameobject-create`, etc.) for all Unity Editor interactions. If Unity MCP is unavailable (not connected, Unity not open), say so explicitly — do NOT fall back to batch-mode CLI, editor scripts, or other workarounds without telling Cesar first.

---

## Physics — Surface-Aware Ground Sampling (Terrain Fallthrough Fix, 2026-04-24)

### Two separate SurfaceType enums and SurfaceMarker components exist — don't conflate them
`Golfin.Physics.SurfaceType` and `Golfin.Physics.Runtime.SurfaceMarker` live in the Physics assembly. `Golfin.Course.SurfaceType` and `Golfin.Course.SurfaceMarker` live in Assembly-CSharp. The migration (`SyncPhysicsSurfaceMarkers`) must iterate `Golfin.Course.SurfaceMarker` and ADD `Golfin.Physics.Runtime.SurfaceMarker` where missing — NOT the reverse. The original design iterated Physics markers and tried `GetComponent<Course.SurfaceMarker>()` on the same GO, finding zero results because most GOs had only Course markers.
**Rule:** Migration direction is Course → Physics (Course markers are the source of truth from UHole import; Physics markers are what BallSimulation consumes).

### `Golfin.Physics.Core` has `noEngineReferences: true` — use callback pattern for logging
`BallSimulation.cs` is in a Core assembly with `noEngineReferences: true`. `UnityEngine.Debug.LogError` is unavailable. Use a static `Action<string>` callback (`DiagErrorLogger`) that callers in Runtime assemblies wire to `Debug.LogError`. Wrap all calls in `#if UNITY_EDITOR` to zero cost at runtime.

### Overlapping BoxCollider geometry is the right approach for deterministic physics tests
Real hole scenes cause unpredictable ball trajectories (ball flies off large colliders, minimum-Y gap becomes −1000m). Synthetic BoxCollider geometry (CreateFlat helper: `box.size = new(w, 0.02f, d)`, top face at exact worldY) is fully deterministic. The fallthrough scenario requires TWO overlapping colliders: higher-Y surface (fringe/fairway) covering a larger area PLUS lower-Y surface (green/bunker) covering a smaller area. `SceneGroundProvider.SampleHeight(x, z, preferred)` must pick the lower Y for balls on the lower-marked surface.

### `overrideReferences: true` asmdef requires explicit Physics.Runtime reference
`Golfin.Gameplay.Tests.asmdef` has `overrideReferences: true`. Auto-referenced assemblies are excluded. If tests reference `SceneGroundProvider` or `SurfaceMarker`, add `"Golfin.Physics.Runtime"` to the asmdef references array explicitly.

### MCP tests-run has ~60s timeout — run stress tests separately
Full EditMode suite with stress tests (~45s) + other tests risks timeout. Run stress tests (`testClass: "TerrainStressTests"`) and non-stress tests in separate `tests-run` calls. Both pass individually; combined run may time out the MCP tool.

---
