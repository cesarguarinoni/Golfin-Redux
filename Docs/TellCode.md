# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Tree Brush Tool (Scene-view cluster painter)

Build an editor tool that lets Cesar paint clusters of trees directly
into a hole scene from the Scene view, on top of whatever `TreePlacer`
already produced from `tree-zones.json`. This is for **detailed
landscaping** — specimen trees, copses near greens, gaps the mask
missed. The `TreePlacer` zone-mask flow stays untouched.

**Target file (new):** `Assets/Scripts/Editor/CourseImporter/TreeBrushTool.cs`
**Reference:** `Assets/Scripts/Editor/CourseImporter/TreePlacer.cs` (read-only — reuse its palette + folder settings, don't duplicate logic)

---

### Design summary

- EditorWindow: **`Window > Trees > Brush Tool`**.
- Reuses `TreePlacer.TreePalette` (same scanned prefabs), so
  enabling/disabling and weights stay in sync with `TreePlacer`.
  No separate prefab list.
- Brush mode = **cluster only** (1 click drops N jittered trees in a
  radius). No single-tree mode, no stamp mode.
- Picks prefabs from the currently-selected folder tab using that
  folder's `FolderPlacementSettings.weight` distribution. **Brush
  settings are independent of `TreePlacer` settings** — the brush has
  its own `scaleMin`/`scaleMax`/`sinkOffset`/`minSpacing` per folder,
  stored separately so artists can tune the brush without touching
  importer behavior.
- Standalone vs terrain-tree decision is per `TreeEntry.standalone`
  (already auto-detected by `TreePlacer.ScanPrefabs`):
    - Standalone → `PrefabUtility.InstantiatePrefab` under a scene
      container `PaintedTrees` (parented to `HoleRoot` if found).
    - Terrain-tree → appended to `terrain.terrainData.treeInstances`
      after registering the prefab as a `TreePrototype` (or reusing the
      existing index if already registered).
- **`PaintedTrees` is a separate container from `StandaloneTrees`** so
  re-running `TreePlacer.PlaceTrees` (which calls
  `CleanupStandaloneTrees`) doesn't wipe brush-painted trees.
- Full undo support — every brush stroke is one undo entry.

Constraint behaviour:
- **Excludes zones via the same overlay-polygon test `TreePlacer` uses.**
  Trees can't be painted over fairway, green, tee, bunker, water, or
  cart path. The brush calls `TreePlacer.IsBlockedByOverlay(worldX,
  worldZ)` (new public helper, see Step 4) per candidate; rejected
  candidates count toward `maxAttempts` but not `placedCount`.
- Honors terrain bounds — clusters that fall partly outside the active
  terrain just skip the out-of-bounds members.
- Sample terrain Y per tree via `terrain.SampleHeight(...)`, apply
  `sinkOffset` from the brush's own per-folder settings.

---

### Step 1 — File scaffold

Create `Assets/Scripts/Editor/CourseImporter/TreeBrushTool.cs` wrapped
in `#if UNITY_EDITOR ... #endif`, namespace `Golfin.CourseImport`.

Class: `public class TreeBrushTool : EditorWindow`.

```csharp
[MenuItem("Window/Trees/Brush Tool")]
public static void ShowWindow()
{
    var w = GetWindow<TreeBrushTool>("Tree Brush");
    w.minSize = new Vector2(280, 360);
}
```

Internal state (serialized so it survives domain reloads via
`[SerializeField]` on private fields, plus `EditorWindow` auto-serializes):

```csharp
[SerializeField] private bool brushEnabled;          // master toggle
[SerializeField] private string activeFolder = "Root"; // tab from palette
[SerializeField] private float brushRadius = 4f;     // meters
[SerializeField] private int treesPerClick = 5;      // cluster size
[SerializeField] private bool alignToTerrainNormal = false; // tilt by slope
[SerializeField] private float maxAlignTiltDeg = 15f;
[SerializeField] private int seed = 0;               // 0 = random per stroke

// Brush-only per-folder settings, fully independent of TreePlacer.PerFolder.
// Persisted as EditorPrefs JSON (key "TreeBrush.FolderSettings") so they
// survive domain reloads, scene switches, and Unity restarts without
// requiring a scene asset.
[System.Serializable]
public class BrushFolderSettings
{
    public string folder;
    public float minSpacingInCluster = 1.5f;
    public float scaleMin = 0.85f;
    public float scaleMax = 1.15f;
    public float sinkOffset = 0.3f;
}
[SerializeField] private List<BrushFolderSettings> brushFolderSettings = new();
```

Helper: `BrushFolderSettings GetBrushFolderSettings(string folder)` —
look up by name, lazy-create on first request seeded from the matching
`TreePlacer.GetFolderSettings(folder)` values (so first-time use of a
folder mirrors importer values, then drifts independently as the
artist tunes). Persistence: in `OnDisable` and after any settings
change, serialize `brushFolderSettings` to `EditorPrefs` under
`"Golfin.TreeBrush.FolderSettings"` via `JsonUtility.ToJson`. In
`OnEnable`, restore from `EditorPrefs` if present.

`OnEnable`/`OnDisable` register/unregister `SceneView.duringSceneGui`:

```csharp
private void OnEnable()
{
    SceneView.duringSceneGui += OnSceneGUI;
    if (TreePlacer.TreePalette.Count == 0) TreePlacer.ScanPrefabs();
}
private void OnDisable()
{
    SceneView.duringSceneGui -= OnSceneGUI;
}
```

---

### Step 2 — Window UI (`OnGUI`)

Top section:
- Big toggle: **"Brush enabled (B)"** — bound to `brushEnabled`. Color
  the window background tinted green when enabled so it's obvious.
- Help box if `brushEnabled` and no terrain in scene: "No active
  terrain in this scene."

Folder picker:
- Dropdown of `TreePlacer.GetFolderTabs()`. If empty, show "Scan
  prefabs in TreePlacer first."
- Right of the dropdown: a small "Refresh Palette" button that calls
  `TreePlacer.ScanPrefabs()`.
- Below the dropdown, a small read-only summary of which prefabs from
  the active folder are currently enabled (from
  `TreePlacer.TreePalette.Where(e => e.enabled && e.folder == activeFolder)`)
  with their weights — so the artist sees what they're about to paint
  without leaving this window. If none are enabled in the folder, show
  a warning: "No enabled prefabs in folder X — enable some in
  TreePlacer."

Brush settings (apply to ALL folders):
- `brushRadius` slider 0.5..20 m.
- `treesPerClick` int slider 1..30.
- `alignToTerrainNormal` toggle. If on, show `maxAlignTiltDeg` slider
  0..45.
- `seed` int field. Tooltip: "0 = random per stroke; non-zero = repeatable."

Per-folder brush settings (editable, independent of TreePlacer):
- Header: "Brush settings — folder: {activeFolder}"
- `minSpacingInCluster` slider 0.3..5 m.
- `scaleMin` / `scaleMax` slider pair 0.3..3.
- `sinkOffset` slider 0..1 m.
- Small button: **"Reset to TreePlacer defaults"** — copies the active
  folder's `TreePlacer.GetFolderSettings(activeFolder)` values into the
  brush's `BrushFolderSettings` for that folder.
- Footer note: "These settings are independent of the importer.
  Changing them here does NOT affect Trees > Import Trees."

Bottom row of buttons:
- **"Clear Painted Trees (this scene)"** → `Undo.RegisterCompleteObjectUndo`
  on the container, then `DestroyImmediate` the `PaintedTrees`
  GameObject. Also remove any terrain-tree instances tagged as
  brush-painted (see Step 4 for tagging).

Tooltip / footer: "Hold **Shift** and click in Scene view to paint a
cluster. Hold **Ctrl** to erase trees within the brush radius."

---

### Step 3 — Scene view interaction (`OnSceneGUI`)

```csharp
private void OnSceneGUI(SceneView sv)
{
    if (!brushEnabled) return;
    var terrain = Terrain.activeTerrain;
    if (terrain == null) return;

    // Keyboard shortcut: B toggles brush
    if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.B)
    {
        brushEnabled = !brushEnabled;
        Repaint();
        Event.current.Use();
        return;
    }

    // Take control of mouse so left-click doesn't deselect
    int controlId = GUIUtility.GetControlID(FocusType.Passive);
    HandleUtility.AddDefaultControl(controlId);

    // Raycast from mouse into the scene
    var mouse = Event.current.mousePosition;
    Ray ray = HandleUtility.GUIPointToWorldRay(mouse);
    if (!Physics.Raycast(ray, out RaycastHit hit, 5000f))
    {
        // Fall back: intersect the terrain plane analytically
        if (!RaycastTerrain(terrain, ray, out hit)) return;
    }

    // Draw brush gizmo
    // Exclusion check at the cursor centre — turn the disc orange-ish
    // if the cursor itself is over an excluded zone, so the artist
    // knows clicks won't place anything there.
    var exclusionPolys = TreePlacer.BuildExclusionPolygonsForActiveScene();
    bool overExcluded = TreePlacer.IsBlockedByOverlay(
        hit.point.x, hit.point.z, exclusionPolys);

    Handles.color =
        overExcluded ? new Color(1f, 0.55f, 0.1f, 0.8f) :
        Event.current.shift ? new Color(0.2f, 1f, 0.2f, 0.8f) :
        Event.current.control ? new Color(1f, 0.3f, 0.3f, 0.8f) :
        new Color(1f, 1f, 1f, 0.5f);
    Handles.DrawWireDisc(hit.point, Vector3.up, brushRadius);

    // Click handling
    if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
    {
        if (Event.current.shift)
        {
            PaintCluster(terrain, hit.point);
            Event.current.Use();
        }
        else if (Event.current.control)
        {
            EraseInRadius(terrain, hit.point);
            Event.current.Use();
        }
    }

    // Force repaint so the gizmo follows the mouse smoothly
    if (Event.current.type == EventType.MouseMove ||
        Event.current.type == EventType.MouseDrag)
        sv.Repaint();
}
```

**`RaycastTerrain` helper** — analytic raycast against the active
terrain's bounds + sample. Use Unity's `terrain.GetComponent<Collider>()`
if present (terrain colliders ARE in `Physics.Raycast`'s world by
default), so the fallback only fires when there's no terrain collider.
Implementation: step along ray from `ray.origin` in fixed increments
of 0.5m up to 5000m, comparing `terrain.SampleHeight(p) + terrain.transform.position.y`
against `p.y`; on first crossing, return that point. Cheap and
sufficient for the brush.

---

### Step 4 — `PaintCluster`

```csharp
private void PaintCluster(Terrain terrain, Vector3 center)
{
    var folderEntries = TreePlacer.TreePalette
        .Where(e => e.enabled && e.weight > 0f &&
                    (e.folder ?? "Root") == activeFolder)
        .ToList();
    if (folderEntries.Count == 0)
    {
        Debug.LogWarning($"[TreeBrush] No enabled prefabs in folder '{activeFolder}'");
        return;
    }

    // Brush-only settings (NOT TreePlacer.PerFolder)
    var bSettings = GetBrushFolderSettings(activeFolder);

    // Build overlay-exclusion polygons for the current scene/hole.
    // We resolve the export folder via the active scene name (same logic
    // as TreePlacer.ImportTreesMenuItem). If we can't resolve it (e.g.,
    // an ad-hoc test scene), polygons stays empty and exclusion is a no-op.
    var exclusionPolys = TreePlacer.BuildExclusionPolygonsForActiveScene();

    var rng = (seed == 0)
        ? new System.Random()
        : new System.Random(seed + Mathf.FloorToInt(center.x * 31f) + Mathf.FloorToInt(center.z * 53f));

    // Cumulative weights for picker
    float total = 0f;
    var cum = new float[folderEntries.Count];
    for (int i = 0; i < folderEntries.Count; i++)
    {
        total += folderEntries[i].weight;
        cum[i] = total;
    }
    if (total <= 0f) return;

    // Get-or-create the PaintedTrees container
    var container = GetOrCreatePaintedContainer();
    Undo.RegisterCompleteObjectUndo(container, "Paint Tree Cluster");

    // Collect placed positions for in-cluster spacing check
    var placed = new List<Vector2>();
    int placedCount = 0;
    int attempts = 0;
    int maxAttempts = treesPerClick * 12; // higher bound — exclusion can reject many

    // For terrain-tree appends, batch then assign once at the end
    var newTerrainTrees = new List<TreeInstance>();

    while (placedCount < treesPerClick && attempts < maxAttempts)
    {
        attempts++;

        // Uniform random in disc
        double u = rng.NextDouble();
        double v = rng.NextDouble();
        float r = brushRadius * Mathf.Sqrt((float)u);
        float a = (float)(v * 2.0 * System.Math.PI);
        float dx = r * Mathf.Cos(a);
        float dz = r * Mathf.Sin(a);
        Vector2 pos2 = new Vector2(center.x + dx, center.z + dz);

        // Spacing check inside this cluster (uses brush setting)
        bool tooClose = false;
        for (int i = 0; i < placed.Count; i++)
        {
            if ((placed[i] - pos2).sqrMagnitude <
                bSettings.minSpacingInCluster * bSettings.minSpacingInCluster)
            { tooClose = true; break; }
        }
        if (tooClose) continue;

        // Ensure point is inside the terrain bounds
        var tPos = terrain.transform.position;
        var tSize = terrain.terrainData.size;
        float lx = pos2.x - tPos.x;
        float lz = pos2.y - tPos.z;
        if (lx < 0 || lx > tSize.x || lz < 0 || lz > tSize.z) continue;

        // Overlay exclusion: skip if this candidate falls inside ANY
        // fairway/green/tee/bunker/water/cart-path polygon.
        if (TreePlacer.IsBlockedByOverlay(pos2.x, pos2.y, exclusionPolys))
            continue;

        float terrainH = terrain.SampleHeight(new Vector3(pos2.x, 0, pos2.y));
        float worldY = tPos.y + terrainH - bSettings.sinkOffset;

        // Pick prefab from folder by weight
        float roll = (float)rng.NextDouble() * total;
        int pickIdx = 0;
        for (int i = 0; i < cum.Length; i++)
            if (roll <= cum[i]) { pickIdx = i; break; }
        var entry = folderEntries[pickIdx];

        float scale = bSettings.scaleMin +
            (float)rng.NextDouble() * (bSettings.scaleMax - bSettings.scaleMin);
        float rotDeg = (float)rng.NextDouble() * 360f;

        if (entry.standalone)
        {
            // Use PrefabUtility to keep the prefab connection
            var go = (GameObject)PrefabUtility.InstantiatePrefab(entry.prefab);
            go.name = $"{entry.name}_brush_{placedCount}";
            go.transform.SetParent(container.transform, true);
            go.transform.position = new Vector3(pos2.x, worldY, pos2.y);

            Vector3 up = Vector3.up;
            if (alignToTerrainNormal)
            {
                Vector3 norm = SampleTerrainNormal(terrain, pos2);
                // Clamp tilt
                float tilt = Vector3.Angle(Vector3.up, norm);
                if (tilt > maxAlignTiltDeg)
                    norm = Vector3.Slerp(Vector3.up, norm, maxAlignTiltDeg / tilt);
                up = norm;
            }
            go.transform.rotation = Quaternion.AngleAxis(rotDeg, Vector3.up) *
                                    Quaternion.FromToRotation(Vector3.up, up);
            go.transform.localScale = Vector3.one * scale;

            Undo.RegisterCreatedObjectUndo(go, "Paint Tree");
        }
        else
        {
            // Terrain tree path
            int protoIdx = EnsureTerrainPrototype(terrain, entry.prefab);
            float ny = Mathf.Max(0f,
                (terrainH - bSettings.sinkOffset) / tSize.y);
            float nx = lx / tSize.x;
            float nz = lz / tSize.z;
            newTerrainTrees.Add(new TreeInstance
            {
                position = new Vector3(nx, ny, nz),
                widthScale = scale,
                heightScale = scale,
                rotation = rotDeg * Mathf.Deg2Rad,
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = protoIdx,
            });
        }

        placed.Add(pos2);
        placedCount++;
    }

    if (placedCount == 0 && attempts >= maxAttempts)
    {
        Debug.Log($"[TreeBrush] No trees placed — entire brush radius " +
                  $"may be over an excluded zone (fairway/green/tee/bunker/water/path).");
    }

    if (newTerrainTrees.Count > 0)
    {
        // Append, don't replace
        var existing = terrain.terrainData.treeInstances;
        Undo.RegisterCompleteObjectUndo(terrain.terrainData,
            "Paint Terrain Trees");
        var combined = new TreeInstance[existing.Length + newTerrainTrees.Count];
        System.Array.Copy(existing, combined, existing.Length);
        for (int i = 0; i < newTerrainTrees.Count; i++)
            combined[existing.Length + i] = newTerrainTrees[i];
        terrain.terrainData.SetTreeInstances(combined, false);
    }

    // Mark scene dirty
    EditorUtility.SetDirty(container);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
        terrain.gameObject.scene);
}
```

**Helpers needed:**

`GetOrCreatePaintedContainer()` — find a GO named `PaintedTrees` in the
active scene; if missing, create one and parent it to `HoleRoot` if
that exists, else leave at scene root. Register with
`Undo.RegisterCreatedObjectUndo` if newly created.

`EnsureTerrainPrototype(Terrain terrain, GameObject prefab)` — scan
`terrain.terrainData.treePrototypes`; if `prefab` already there, return
its index; otherwise append a new `TreePrototype { prefab = prefab }`,
write back via `terrain.terrainData.treePrototypes = newArray`, and
return the new index.

`SampleTerrainNormal(Terrain terrain, Vector2 worldPos)` — use
`terrain.terrainData.GetInterpolatedNormal(nx, nz)` where `nx`/`nz`
are normalized terrain coords.

---

### Step 4.5 — Expose overlay-exclusion helpers in TreePlacer

The brush needs to test whether a candidate point falls inside any
fairway/green/tee/bunker/water/cart-path polygon. `TreePlacer` already
builds these polygons in `BuildExclusionPolygons` and tests them with
`PointInPolygon` / `IsInsideAnyOverlay` — but everything is `private`
and tied to a known `exportPath` + `isGeo` flag. Add a thin public
surface.

Add these to `TreePlacer.cs` (no changes to existing logic — pure
additions):

```csharp
/// <summary>
/// Build overlay-exclusion polygons for the currently-active scene.
/// Resolves Lite-vs-Geo and the export folder from the scene name +
/// path (same logic as ImportTreesMenuItem).
/// Returns an empty list if the scene name doesn't match a hole or
/// the export folder is missing.
/// Used by TreeBrushTool to keep the brush off overlay surfaces.
/// </summary>
public static List<Vector2[]> BuildExclusionPolygonsForActiveScene()
{
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
    string sceneName = activeScene.name;
    string scenePath = activeScene.path ?? "";

    bool isGeo = scenePath.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Geo", System.StringComparison.OrdinalIgnoreCase) >= 0;
    bool isFlat = scenePath.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0
        || sceneName.IndexOf("_Flat", System.StringComparison.OrdinalIgnoreCase) >= 0;

    string baseName = System.Text.RegularExpressions.Regex
        .Replace(sceneName, "(_Geo)?(_Flat)?$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    int holeNumber = -1;
    if (baseName.StartsWith("Hole_") && baseName.Length >= 7)
        int.TryParse(baseName.Substring(5, 2), out holeNumber);
    if (holeNumber < 1 || holeNumber > 18)
        return new List<Vector2[]>();

    string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
    string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat" : $"hole-{holeNumber:D2}";
    string exportPath = Path.Combine(
        Application.dataPath, "..",
        $"Tools/{toolFolder}/output/lomond-country-club/export",
        holeFolder);

    if (!Directory.Exists(exportPath))
        return new List<Vector2[]>();

    return BuildExclusionPolygons(exportPath, isGeo);
}

/// <summary>
/// Test if (worldX, worldZ) falls inside ANY of the supplied
/// overlay-exclusion polygons. Empty list = always returns false.
/// </summary>
public static bool IsBlockedByOverlay(
    float worldX, float worldZ, List<Vector2[]> polygons)
{
    if (polygons == null || polygons.Count == 0) return false;
    return IsInsideAnyOverlay(worldX, worldZ, polygons);
}
```

That's the only structural change to `TreePlacer` for exclusion. The
private `BuildExclusionPolygons` / `IsInsideAnyOverlay` /
`PointInPolygon` stay private — only the two new public wrappers are
added.

---

### Step 5 — `EraseInRadius`

```csharp
private void EraseInRadius(Terrain terrain, Vector3 center)
{
    float r2 = brushRadius * brushRadius;
    int removedGO = 0;
    int removedTerrain = 0;

    // Standalone painted trees
    var container = FindPaintedContainer();
    if (container != null)
    {
        // Collect first to avoid mutating during iteration
        var toRemove = new List<GameObject>();
        for (int i = 0; i < container.transform.childCount; i++)
        {
            var child = container.transform.GetChild(i).gameObject;
            Vector2 d = new Vector2(child.transform.position.x - center.x,
                                    child.transform.position.z - center.z);
            if (d.sqrMagnitude <= r2) toRemove.Add(child);
        }
        foreach (var go in toRemove)
        {
            Undo.DestroyObjectImmediate(go);
            removedGO++;
        }
    }

    // Terrain trees: filter by world distance, rewrite array
    var tPos = terrain.transform.position;
    var tSize = terrain.terrainData.size;
    var instances = terrain.terrainData.treeInstances;
    var kept = new List<TreeInstance>(instances.Length);
    for (int i = 0; i < instances.Length; i++)
    {
        float wx = tPos.x + instances[i].position.x * tSize.x;
        float wz = tPos.z + instances[i].position.z * tSize.z;
        Vector2 d = new Vector2(wx - center.x, wz - center.z);
        if (d.sqrMagnitude <= r2) { removedTerrain++; continue; }
        kept.Add(instances[i]);
    }
    if (removedTerrain > 0)
    {
        Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Erase Terrain Trees");
        terrain.terrainData.SetTreeInstances(kept.ToArray(), false);
    }

    if (removedGO + removedTerrain > 0)
    {
        Debug.Log($"[TreeBrush] Erased {removedGO} GO + {removedTerrain} terrain trees");
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            terrain.gameObject.scene);
    }
}
```

**Note on terrain-tree erase:** this removes **all** terrain-tree
instances in the radius — including ones placed by `TreePlacer`, not
just brush-painted ones. There is no per-instance metadata on
`TreeInstance` to distinguish source. That's acceptable: the artist
explicitly chose to erase the area; if they want to restore the
mask-driven trees, they re-run `Trees > Import Trees Current Hole`.
Document this in the window as a small footer note: "Erase removes any
terrain trees in the radius, including ones placed by TreePlacer.
Re-import to restore."

The standalone container split (`PaintedTrees` vs `StandaloneTrees`)
*does* make the GameObject side robust — re-importing won't kill
brush-painted trees, only the importer's own.

---

### Step 6 — Don't break TreePlacer

`TreePlacer.CleanupStandaloneTrees()` only deletes GameObjects named
`StandaloneTrees`. Our container is `PaintedTrees`, so cleanup is
already isolated — no change needed there.

After painting, the next `Trees > Import Trees Current Hole` run will:
- Wipe `StandaloneTrees` (importer's container) ✓
- Replace ALL terrain-tree instances via
  `terrain.terrainData.SetTreeInstances(...)` ✗ — this WILL erase
  brush-painted terrain trees too

This is a known limitation. Options for the artist:
1. Paint only after the final `TreePlacer` import.
2. For specimens that must survive re-imports, use prefabs marked
   `standalone` (in `PaintedTrees`).

Add this note to the window's help section.

---

### Step 7 — Apply `NormalizeLODGroup`

Standalone painted trees should get the same LOD treatment as importer
trees. `TreePlacer.NormalizeLODGroup` is private — make it
`internal static` (single-word visibility change) so the brush tool
can call it on each instance after creation.

In `TreePlacer.cs`, change:

```csharp
private static void NormalizeLODGroup(LODGroup lodGroup)
```

to:

```csharp
internal static void NormalizeLODGroup(LODGroup lodGroup)
```

Then in `PaintCluster`, after instantiating a standalone tree:

```csharp
foreach (var lg in go.GetComponentsInChildren<LODGroup>(true))
    TreePlacer.NormalizeLODGroup(lg);
```

(For terrain-tree path, the prototype's prefab already has LODGroup
normalized once the importer runs; brush-only sessions can skip this
since unnormalized LODs are still functional.)

---

### Verification

Open `Hole_07_Geo` (or any hole with a baked terrain), then:

- [ ] `Window > Trees > Brush Tool` opens; folder dropdown shows the
      same folders as the TreePlacer window.
- [ ] Toggle "Brush enabled". Move mouse over the terrain — wire disc
      gizmo follows, white by default.
- [ ] Hold Shift — disc turns green. Click — a cluster of N trees
      drops with jitter inside `brushRadius`, none closer than
      `minSpacingInCluster`.
- [ ] Hover the cursor over a fairway, green, tee, bunker, water, or
      cart path — the disc turns **orange**. Shift-click does nothing
      (or only places trees in the non-overlapping part of the disc
      if the brush straddles the edge).
- [ ] Move the brush partially over a fairway edge — only the trees
      outside the fairway polygon get placed; spacing in the placed
      portion still respects `minSpacingInCluster`.
- [ ] Change `scaleMin`/`scaleMax`/`sinkOffset` in the brush window —
      next paint stroke uses the new values. Re-running
      `Trees > Import Trees Current Hole` is **unaffected** — importer
      still uses its own `TreePlacer.GetFolderSettings`.
- [ ] Click "Reset to TreePlacer defaults" — brush settings snap back
      to whatever TreePlacer is currently using for that folder.
- [ ] Standalone prefabs (e.g., `Spruce 1`) appear under
      `HoleRoot/PaintedTrees/`. Terrain-tree prefabs (e.g., the cedars)
      appear via the terrain system, no scene GameObjects.
- [ ] Hold Ctrl — disc turns red. Click — trees inside the radius
      vanish (both standalone and terrain).
- [ ] `Ctrl+Z` undoes a paint stroke as one operation. `Ctrl+Z` undoes
      an erase as one operation.
- [ ] Run `Trees > Import Trees Current Hole`. `PaintedTrees`
      container survives. (Terrain-tree brush placements get wiped —
      this is documented behavior.)
- [ ] Switch to a hole with no `tree-zones.json` painted (e.g., a
      brand-new hole). Brush still works against the bare terrain.
- [ ] Disable the brush toggle — gizmo disappears, scene clicks
      behave normally (selection works).
- [ ] `B` key toggles the brush while the Scene view is focused.
- [ ] Click the "Clear Painted Trees (this scene)" button — the
      `PaintedTrees` container is destroyed. Undo restores it.

Regression:

- [ ] Run `Trees > Import All Trees Geo` on a hole — completes without
      errors, `StandaloneTrees` populated, `PaintedTrees` (if exists
      from a prior session) untouched.

---

### Do NOT change

- `TreePlacer.PlaceTrees`, `TreePlacer.ScanPrefabs`, the menu items,
  session persistence, or any of its `MenuItem` flows.
- `TreePlacer.PerFolder` settings — those stay the importer's source
  of truth. The brush keeps its own `BrushFolderSettings`.
- `tree-zones.json` schema, the UHole pipeline, or any export tools.
- The `StandaloneTrees` container name or `CleanupStandaloneTrees`
  behavior. The brush uses a different container by design.
- `TreePlacer`'s default weights, force-standalone list, or
  `ExcludeZones`.
- The private polygon helpers (`BuildExclusionPolygons`,
  `IsInsideAnyOverlay`, `PointInPolygon`) — leave them private and
  call only via the new `BuildExclusionPolygonsForActiveScene` /
  `IsBlockedByOverlay` wrappers.

Allowed changes to `TreePlacer.cs`:
- `NormalizeLODGroup`: `private` → `internal`
- New public method: `BuildExclusionPolygonsForActiveScene()`
- New public method: `IsBlockedByOverlay(float, float, List<Vector2[]>)`

---



✅ DONE: 2026-04-17 — Tree Brush Tool complete: TreeBrushTool.cs + TreePlacer overlay helpers + NormalizeLODGroup internal

## Previous Task — Fix Voronoi Seesaw in Shore Ramp (Hole 7 Geo)

The 3-pass box blur didn't help. Correct diagnosis: this is a **Voronoi
seesaw pattern**, not per-cell chamfer quantization noise.

**Root cause:** The water mask built by `MarkContourCells` has 1-cell
jaggies along the curved boundary (inherent to rasterizing a smooth
polygon onto a grid — each cell's centre is either inside or outside,
no partial coverage). When the chamfer distance transform walks
outward from this jagged mask, it produces *radial spokes* in the
distance field, not random noise. Each spoke is a multi-cell-long
coherent stripe aligned with a boundary jaggy.

On a 2m slope, the absolute-target lerp turns those spokes into
visible vertical stripes.

A local box blur only averages adjacent cells, so it smooths pixel
noise but can't fix coherent multi-cell spokes. The stripes persist
because each stripe's cells all have similar (wrong) distance values.

**Fix:** smooth the **distance field itself**, not the final heights.
A Gaussian blur on `distToWater` (continuous float values) produces
sub-cell-accurate distances with no jagged boundary spokes. The
downstream lerp then gives smooth ramp heights.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Change: Blur the distance field before the lerp

Remove the 3-pass box blur on `heights` (added in the previous task).
It didn't help and just costs time.

Add a Gaussian blur on `distToWater` immediately after the chamfer
transform completes, before the shore lerp loop.

#### Step 1 — Remove the heights-blur block

Delete the entire 3-pass masked box blur on `heights` that was added
in the previous task. Starts with comment
`// Masked box blur over ramp cells only.` and ends with its closing
brace. Remove the `rampMask` array declaration and the
`rampMask[z, x] = true` line inside the shore lerp loop as well — no
longer needed.

#### Step 2 — Add distance field Gaussian blur

After the joint chamfer transform finishes (after the backward pass,
before the shore lerp loop), add:

```csharp
// Smooth the distance field to eliminate Voronoi seesaw spokes.
// The water mask has 1-cell boundary jaggies from polygon rasterization.
// The chamfer transform propagates those jaggies as coherent radial
// spokes in distToWater. A Gaussian blur on the continuous distance
// values produces sub-cell-accurate distances without jagged spokes.
//
// Blur radius should be comparable to the jaggies scale (~1-2 cells).
// Sigma = 2.0 cells gives a ~5-cell effective kernel — enough to smooth
// spokes without softening the overall distance gradient.
{
    const int blurRadius = 3;
    const float blurSigma = 2.0f;
    int kernelSize = blurRadius * 2 + 1;
    float[] kernel = new float[kernelSize];
    float kernelSum = 0f;
    for (int i = 0; i < kernelSize; i++)
    {
        float d = i - blurRadius;
        kernel[i] = Mathf.Exp(-(d * d) / (2f * blurSigma * blurSigma));
        kernelSum += kernel[i];
    }
    for (int i = 0; i < kernelSize; i++) kernel[i] /= kernelSum;

    // Horizontal pass
    float[,] tmp = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sx = Mathf.Clamp(x + k - blurRadius, 0, hRes - 1);
                sum += distToWater[z, sx] * kernel[k];
            }
            tmp[z, x] = sum;
        }
    }

    // Vertical pass, writing back to distToWater
    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            float sum = 0f;
            for (int k = 0; k < kernelSize; k++)
            {
                int sz = Mathf.Clamp(z + k - blurRadius, 0, hRes - 1);
                sum += tmp[sz, x] * kernel[k];
            }
            distToWater[z, x] = sum;
        }
    }
}
```

**Important:** do NOT blur `nearestSurfaceY` — it's already uniform on
Hole 7 (single water body), and if we had multiple bodies we'd want
it to stay discrete at body boundaries, not blend.

**Important:** do NOT blur water cells' distToWater back upward. The
blur writes over the 0-value boundary cells, making them non-zero,
which would flag them as "ramp candidates" in the lerp loop. Two
safeguards:
1. The `if (waterMask[z, x]) continue;` guard in the lerp loop already
   protects water cells from being touched.
2. But the blur itself reads from water cells (dist=0) into neighbors,
   which is what we want — it pulls the distance field smoothly toward
   0 at the waterline. Keep this behavior.

After the blur, water cells have `distToWater` values > 0 (blurred in
from their non-water neighbors), but the `waterMask` guard skips them
anyway, so this doesn't matter.

---

### Why this works

- The water mask has ~1-cell jaggies → the chamfer propagates them as
  ~1-cell-wide radial spokes in `distToWater`.
- A 5-cell-wide separable Gaussian (σ=2, radius=3) averages each
  distance value with ~25 neighbors → individual spoke contributions
  average out, leaving a smooth gradient.
- The overall distance gradient (smooth change from 0 at shore to
  `shoreRadius` outward) is preserved because the blur's kernel width
  (~5 cells) is much smaller than the ramp width (~10 cells).
- The lerp then runs on smoothed distances → smooth ramp heights →
  no stripes.

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] No stripes on the shore ramp — smooth ramp surface
- [ ] No cliff (regression)
- [ ] Water surface flat (regression)
- [ ] Full water mesh visible (regression)
- [ ] Ramp still lerps smoothly from shore to terrain (not overflat)

Regression check:

- [ ] `Import Hole 01 Geo` (no water) — no errors
- [ ] `Import Hole 12 Geo` (multi-body) — each body still handled

If stripes persist, raise sigma to 3.0 (radius 4). If ramp looks too
flat/soft, drop sigma to 1.5 (radius 2).

---

### Do NOT change

- Everything from the previous three water tasks
- The chamfer distance transform itself
- `nearestSurfaceY` propagation (leave it as-is)
- Shore constants
- Water mesh, floor depression, or shore ramp lerp formula

---

## Previous Task — Smooth Shore Ramp Stripes (Hole 7 Geo)

Absolute-target lerp works — no more cliff. But the shore ramp now shows
vertical comb-like stripes running from the waterline up the slope.

**Root cause:** The chamfer distance transform is a discrete approximation
(1.0 for axial, 1.414 for diagonal). Along a curved water boundary, the
forward+backward chamfer walks produce subtle directional banding in the
distance field — cell-to-cell variations of ~0.05 in `t`. With the old
ramp (0.4m max drop), 0.05 * 0.4m = 2cm — invisible. With the new
absolute lerp (can span 2m+ on slopes), 0.05 * 2m = 10cm — very visible
stripes.

**Fix:** Blur the ramp heights after computing them. A light box-blur
over the ramp zone averages out the chamfer quantization without
damaging the overall ramp shape. Skip water cells and already-depressed
cells during the blur so we don't smear water depth or fairway edges.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Change: Build a rampMask + add masked blur pass

**Step 1 — Build `rampMask` during the shore lerp loop.**

The existing shore lerp loop writes `heights[z, x]` and increments
`shoreCount`. Add a `rampMask` bool[,] that tracks which cells the ramp
touched.

Just before the shore lerp loop (inside the `if (hasWater && ShoreRadius > 0 && ShoreDepthMeters > 0f)` block, after the joint chamfer transform), add:

```csharp
bool[,] rampMask = new bool[hRes, hRes];
```

Inside the existing lerp loop, in the `if (targetH < originalH)` branch
where `heights[z, x]` is assigned, also set `rampMask[z, x] = true`:

```csharp
if (targetH < originalH)
{
    heights[z, x] = Mathf.Max(0f, targetH);
    rampMask[z, x] = true;   // NEW
    shoreCount++;
}
```

**Step 2 — Add a 3-iteration masked box blur after the lerp loop.**

Still inside the `if (hasWater ...)` block, immediately after the lerp
loop closes, add:

```csharp
// Masked box blur over ramp cells only.
// 3 iterations ≈ Gaussian with ~5x5 effective kernel.
// Kills the chamfer quantization stripes (~10cm variation on 2m slopes).
float[,] tmpHeights = new float[hRes, hRes];
for (int pass = 0; pass < 3; pass++)
{
    // Snapshot current heights
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            tmpHeights[z, x] = heights[z, x];

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (!rampMask[z, x]) continue;

            // 3x3 box average; skip water/depressed neighbors so their
            // values don't leak into the ramp zone.
            float sum = 0f;
            int count = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                int nz = z + dz;
                if (nz < 0 || nz >= hRes) continue;
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= hRes) continue;
                    if (waterMask[nz, nx]) continue;
                    if (depress[nz, nx]) continue;
                    if (cartDepress[nz, nx]) continue;
                    sum += tmpHeights[nz, nx];
                    count++;
                }
            }
            if (count > 0)
                heights[z, x] = sum / count;
        }
    }
}
```

---

### Why a blur works

- Chamfer distance has ~1-2% error bands aligned with the forward-pass
  walk direction. Cells in the bands differ from their neighbors by small
  amounts; across a 2m-span ramp that produces visible stripes.
- A box blur averages each cell with its 8 neighbors — exactly the cells
  that sampled the correct distance in one of the two chamfer passes.
  Three iterations widens the effective kernel to ~5×5.
- We only blur ramp cells, so water, fairway, tee, cart path, and
  untouched terrain stay pixel-perfect.
- We skip water/depressed neighbors in the kernel sum so depression
  depth doesn't bleed up into the ramp (which would re-cliff the
  boundary).

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] No vertical stripes on the shore ramp
- [ ] Ramp still smooth from shoreline to terrain
- [ ] No cliff (regression check)
- [ ] Water surface flat (regression check)
- [ ] Full water mesh visible (regression check)

Regression check:

- [ ] `Import Hole 01 Geo` — no errors
- [ ] `Import Hole 12 Geo` — multi-body still clean

If stripes persist with 3 passes, raise to 5. If ramp looks muddy, drop
to 2.

---

### Do NOT change

- Everything from the previous three water tasks
- The chamfer distance transform itself (blur fixes its output, not the
  algorithm)
- Shore constants

---

## Previous Task — Shore Ramp Absolute Target (Hole 7 Geo Cliff)

Water is no longer hidden — but there's now a ~1.6m vertical cliff at
the upslope water boundary. Shore ramp runs its full 10 cells but the
cliff remains because the ramp can only subtract `ShoreDepthMeters`
(0.4m) — not enough on a slope that's 2m higher than the water surface.

**Root cause in one sentence:** `drop = ShoreDepthMeters * smoothstep(t)`
is a fixed-magnitude subtraction. When the terrain is 2m above waterY,
subtracting 0.4m at the boundary still leaves a 1.6m cliff.

**Fix:** Replace the subtractive ramp with a lerp that targets the water
surface height as an absolute value. At the boundary the ramp should
reach `waterSurfaceNorm` (the water mesh Y in normalized terrain units).
At `ShoreRadius` it should reach the original terrain height. Everything
in between is a smoothstep blend between those two absolute heights.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Step 1 — Add per-body water SURFACE Y tracking

The previous task added `waterMask` and `waterFloorY` arrays. We now also
need per-cell `waterSurfaceY` for the shore ramp target.

In the water-mask building block, alongside `waterFloorY`:

```csharp
// Per-cell water SURFACE Y in normalized heightmap units.
// Needed by shore ramp so land can lerp to the correct water level
// per body (holes can have multiple bodies at different elevations).
float[,] waterSurfaceY = new float[hRes, hRes];
```

Inside the `foreach (var w in waterData.water)` loop, compute the surface
norm alongside the floor norm:

```csharp
// Water SURFACE Y in world units, then normalize.
// (Surface is at minTerrainH - 0.05m, same as CreateWaterMeshes.)
float surfaceWorldY = minTerrainH - 0.05f;
float surfaceNorm = Mathf.Clamp01(surfaceWorldY / elevRange);
```

In the `for (int z) for (int x)` loop that writes `waterMask` and
`waterFloorY`, also write `waterSurfaceY`:

```csharp
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
        if (bodyMask[z, x])
        {
            waterMask[z, x] = true;
            waterFloorY[z, x] = floorNorm;
            waterSurfaceY[z, x] = surfaceNorm;
        }
```

---

### Step 2 — Joint chamfer: propagate nearest body's surface Y with distance

The existing chamfer distance transform populates `distToWater[z, x]`.
Extend it to also track the surface Y of the nearest water body.

Replace the chamfer distance block in the shore slope pass with:

```csharp
// Joint chamfer: distToWater + nearest-body surfaceY propagation.
// Water cells start with dist=0 and their own surfaceY.
// Non-water cells inherit both from the nearest water neighbor.
float[,] distToWater = new float[hRes, hRes];
float[,] nearestSurfaceY = new float[hRes, hRes];
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
    {
        distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;
        nearestSurfaceY[z, x] = waterSurfaceY[z, x];
    }

// Forward pass
for (int z = 0; z < hRes; z++)
    for (int x = 0; x < hRes; x++)
    {
        if (x > 0)
        {
            float cand = distToWater[z, x - 1] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z, x - 1]; }
        }
        if (z > 0)
        {
            float cand = distToWater[z - 1, x] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x]; }
        }
        if (x > 0 && z > 0)
        {
            float cand = distToWater[z - 1, x - 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x - 1]; }
        }
        if (x < hRes - 1 && z > 0)
        {
            float cand = distToWater[z - 1, x + 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z - 1, x + 1]; }
        }
    }
// Backward pass
for (int z = hRes - 1; z >= 0; z--)
    for (int x = hRes - 1; x >= 0; x--)
    {
        if (x < hRes - 1)
        {
            float cand = distToWater[z, x + 1] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z, x + 1]; }
        }
        if (z < hRes - 1)
        {
            float cand = distToWater[z + 1, x] + 1f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x]; }
        }
        if (x < hRes - 1 && z < hRes - 1)
        {
            float cand = distToWater[z + 1, x + 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x + 1]; }
        }
        if (x > 0 && z < hRes - 1)
        {
            float cand = distToWater[z + 1, x - 1] + 1.414f;
            if (cand < distToWater[z, x])
            { distToWater[z, x] = cand; nearestSurfaceY[z, x] = nearestSurfaceY[z + 1, x - 1]; }
        }
    }
```

---

### Step 3 — Replace the subtractive ramp with an absolute-target lerp

Replace the shore-cell ramp loop:

```csharp
int shoreRadiusCells = ShoreRadius;

for (int z = 0; z < hRes; z++)
{
    for (int x = 0; x < hRes; x++)
    {
        if (waterMask[z, x]) continue;
        if (depress[z, x]) continue;
        if (cartDepress[z, x]) continue;

        float dist = distToWater[z, x];
        if (dist <= 0f || dist > shoreRadiusCells) continue;

        // t = 0 at the water boundary, 1 at shoreRadius.
        float t = dist / shoreRadiusCells;
        t = t * t * (3f - 2f * t); // smoothstep

        // Absolute target: lerp from water surface Y (at boundary)
        // to original terrain height (at shoreRadius). Works for any
        // slope magnitude because we target an absolute Y, not a
        // fixed-magnitude drop.
        float waterY = nearestSurfaceY[z, x];
        float originalH = heights[z, x];
        float targetH = Mathf.Lerp(waterY, originalH, t);

        // Only lower the terrain — never raise it. If the existing
        // height is already below the interpolated target (e.g., a
        // natural low spot next to water), leave it alone.
        if (targetH < originalH)
        {
            heights[z, x] = Mathf.Max(0f, targetH);
            shoreCount++;
        }
    }
}
```

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] No cliff at water boundary — terrain meets water level smoothly
- [ ] Ramp is gradual over the full ShoreRadius width (~3m at 2049 res)
- [ ] Water surface still flat (no seesaw)
- [ ] Full water mesh still visible (no re-regression to hidden half)

Regression check:

- [ ] `Import Hole 01 Geo` (no water) — no errors
- [ ] `Import Hole 12 Geo` (multi-body) — each body's surrounding ramp
      targets THAT body's surface Y, not some average

---

### Do NOT change

- Water mesh construction (from the first port)
- Water floor depression (from the second port)
- Fairway/tee/cart path behavior
- Shore constants (ShoreRadius=10, ShoreDepthMeters=0.4)
- Shore ramp skip conditions (water/depress/cartDepress)

**Note:** With this fix, `ShoreDepthMeters` becomes less directly meaningful
for the ramp (the ramp now targets water surface absolutely, not a fixed
drop). Keep the constant for now — it still controls water floor depth
via the previous task's floor logic.

---

## Previous Task — Flatten Terrain Under Water (Hole 7 Geo Follow-up)

Water rework applied successfully — seesaw gone, shape clean. But on sloped
contours (Hole 7), half the water mesh ends up below the terrain.

**Root cause:** Water Y is `min(shoreTerrain) − 0.05m`. On a slope, the
highest shore may be 1–2m above the lowest. The current depression drops
terrain by a FIXED 0.4m off its original height — not enough to get the
upslope side below the water plane.

**Fix:** Flatten terrain under water to an ABSOLUTE normalized height
(`waterY − underwaterDepth` in world units), not a relative drop off
original height. This guarantees a flat bed below water regardless of
slope.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No pipeline changes.**

---

### Change: Separate water depression from fairway/tee

The previous water port added water contours to the shared `depress` bool
array and let the standard flat-drop loop handle them. Replace that with
a dedicated absolute-height pass.

**Step 1: Undo water's entry in the shared `depress` mask.**

In `DepressTerrainUnderOverlays`, find the water-contour block that was
added between the tee section and the cart path block:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with shore slope ramp applied afterward.
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }
}
```

**Delete this entire block.** Water needs its own mask + pass, not the
shared one.

**Step 2: Add a dedicated water mask, parallel to the others.**

Just after the `cartDepress` block (and before the depression apply loops),
build a water mask AND compute water Y per body:

```csharp
// Water cells — tracked separately because they get an ABSOLUTE height
// floor (not a relative drop). Necessary for sloped contours where a
// fixed drop off original height leaves the upslope bed above waterY.
bool[,] waterMask = new bool[hRes, hRes];
// Per-cell water Y in normalized heightmap units (height = [0..1])
float[,] waterFloorY = new float[hRes, hRes];
bool hasWater = false;

string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        // We need terrainBaseY for SampleHeight conversion.
        Terrain terrainComp = terrainGO.GetComponent<Terrain>();
        float terrainBaseY = terrainGO.transform.position.y;

        // Underwater floor: 0.3m below water surface.
        // Water surface = terrainBaseY + minShoreTerrainH - 0.05m,
        // so floor = terrainBaseY + minShoreTerrainH - 0.35m.
        const float UnderwaterDepthMeters = 0.3f;

        foreach (var w in waterData.water)
        {
            if (w.contour == null || w.contour.Length < 3) continue;

            // Recompute minTerrainH across contour — same as CreateWaterMeshes.
            // (We can't share it easily because water was built earlier,
            // but this is one float per body, cheap.)
            float minTerrainH = float.MaxValue;
            for (int i = 0; i < w.contour.Length; i++)
            {
                float wx = w.contour[i].x;
                float wz = w.contour[i].z;
                float th = terrainComp.SampleHeight(new Vector3(wx, 0, wz));
                if (th < minTerrainH) minTerrainH = th;
            }
            // Floor Y in world units, then normalized to [0..1] against elevRange.
            float floorWorldY = minTerrainH - 0.05f - UnderwaterDepthMeters;
            // Clamp to ≥ 0 in case terrain Y offset eats the range
            float floorNorm = Mathf.Clamp01(floorWorldY / elevRange);

            // Mark cells inside this water contour with this body's floor Y.
            // Build a local mask for THIS body, then write the Y value to
            // waterFloorY for each cell in the mask.
            bool[,] bodyMask = new bool[hRes, hRes];
            MarkContourCells(w.contour, bodyMask,
                hRes, terrainPos, terrainSize, 0f);

            for (int z = 0; z < hRes; z++)
                for (int x = 0; x < hRes; x++)
                    if (bodyMask[z, x])
                    {
                        waterMask[z, x] = true;
                        waterFloorY[z, x] = floorNorm;
                    }

            hasWater = true;
        }
    }
}
```

**Step 3: Apply the water floor BEFORE the fairway/tee/cart apply loops.**

Immediately after the water mask-building block above (still before the
existing apply loops), add:

```csharp
// Apply water: flatten terrain to an absolute floor (not a relative drop).
// Must run BEFORE fairway/tee/cart apply loops because any fairway that
// overlaps water should keep the fairway drop, not the water floor.
// We'll mask out water cells in the fairway/tee loops below.
int waterFloorCount = 0;
if (hasWater)
{
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            if (waterMask[z, x])
            {
                // Set to absolute floor, not subtract
                heights[z, x] = waterFloorY[z, x];
                waterFloorCount++;
            }
}
```

**Step 4: Skip water cells in the fairway/tee apply loop.**

Find the fairway/tee apply loop:

```csharp
int depressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (depress[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            depressedCount++;
        }
```

Change the condition to skip water cells:

```csharp
int depressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (depress[hz, hx] && !waterMask[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            depressedCount++;
        }
```

Same for the cart path apply loop:

```csharp
int cartDepressedCount = 0;
for (int hz = 0; hz < hRes; hz++)
    for (int hx = 0; hx < hRes; hx++)
        if (cartDepress[hz, hx] && !waterMask[hz, hx])
        {
            heights[hz, hx] = Mathf.Max(0f,
                heights[hz, hx] - dropNormalized);
            cartDepressedCount++;
        }
```

**Step 5: Shore slope pass — use existing water mask.**

The shore slope pass already exists from the previous port. It currently
re-reads water.json and builds its own `waterMask`. Simplify: reuse the
mask built in Step 2.

Find the shore slope section (begins with
`string waterShorePath = Path.Combine(exportPath, "water.json");`).

Replace the entire shore slope block (from `string waterShorePath = ...`
through the closing brace of the outer `if (File.Exists(waterShorePath) ...)`)
with:

```csharp
// ─── Shore slope pass: gradual ramp OUTSIDE water contours ─────────
// Uses waterMask built above. Smooth ramp from shoreline
// (full ShoreDepthMeters drop) to surrounding terrain (no drop)
// over ShoreRadius cells.
int shoreCount = 0;
if (hasWater && ShoreRadius > 0 && ShoreDepthMeters > 0f)
{
    // Chamfer distance transform from water boundary.
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }
    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreRadiusCells = ShoreRadius;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;
            if (depress[z, x]) continue;
            if (cartDepress[z, x]) continue;

            float dist = distToWater[z, x];
            if (dist <= 0f || dist > shoreRadiusCells) continue;

            float t = 1f - (dist / shoreRadiusCells);
            t = t * t * (3f - 2f * t);
            float drop = shoreDropNorm * t;

            heights[z, x] = Mathf.Max(0f, heights[z, x] - drop);
            shoreCount++;
        }
    }
}
```

**Step 6: Update final Debug.Log.**

Replace the current final log (the one updated in the previous port) with:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water floor: {waterFloorCount} cells flattened," +
          $" water shore ramp: {shoreCount} cells)");
```

---

### Execution order

1. Step 1 (remove old water-in-depress block)
2. Step 2 (build waterMask + waterFloorY)
3. Step 3 (apply water floor)
4. Step 4 (skip water in fairway/tee/cart loops)
5. Step 5 (replace shore slope block)
6. Step 6 (log update)

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Entire water mesh visible (no hidden-under-terrain half)
- [ ] Water surface still flat (no seesaw regression)
- [ ] Shore ramp smooth (no cliff)
- [ ] No Z-fighting between water mesh edge and terrain

Regression check:

- [ ] `Import Hole 01 Geo` — no water, no errors, no regression
- [ ] `Import Hole 12 Geo` — multiple water bodies, each gets its own floor

---

### Why this approach

- The previous "drop 0.4m off original" works for flat-land water but not
  sloped water bodies. Absolute floor is slope-independent.
- Per-body floorY handles holes with multiple water levels (e.g., a pond
  and a stream at different elevations) — each body gets its own floor
  from its own min shore height.
- Shore ramp stays on original terrain heights (not the floor) because
  ramp cells are OUTSIDE the water mask.

---

### Do NOT change

- `CreateWaterMeshes` (from previous port — water mesh Y is still
  `terrainBaseY + minTerrainH - 0.05f`, unchanged)
- `CreateWaterMaterial` depth settings
- Shore constants (ShoreRadius=10, ShoreDepthMeters=0.4)
- Fairway/tee/green/bunker/cart path logic

---

## Previous Task — Port Water Rework to HoleGeoImporter

Hole 7 Geo shows a seesaw waterline and water edges that don't match the
terrain edges. Cause: HoleGeoImporter still has the OLD per-vertex
terrain-following water code. The 2026-04-14 water rework was applied to
HoleLiteImporter.cs only; HoleGeoImporter.cs never got the port.

This task ports the rework to HoleGeoImporter.cs. The Lite version is the
working reference — match its behavior.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**Reference file:** `Assets/Scripts/Editor/CourseImporter/HoleLiteImporter.cs`
(only for cross-checking — do not edit)
**No pipeline changes.** `water.json` is fine.

---

### Part 1 — Shore constants at top of class

Current (HoleGeoImporter.cs, around lines 18–21):

```csharp
public static int ShoreRadius = 2;
public static float ShoreDepthMeters = 0.1f;
```

Change to:

```csharp
public static int ShoreRadius = 10;
public static float ShoreDepthMeters = 0.4f;

// ─── Terrain Y offset — headroom below flat terrain for water bed.
// Must be ≥ ShoreDepthMeters + water surface depth (0.05m) + underwater margin (0.3m)
// so heightmap can represent the full water bed without clamping.
private static float TerrainYOffset => ShoreDepthMeters;
```

---

### Part 2 — Use `TerrainYOffset` for terrain placement

In `ImportHoleInternal`, the terrain object is positioned using
`ShoreDepthMeters`. Change it to use `TerrainYOffset`.

Find:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -ShoreDepthMeters, -terrainZ / 2f);
```

Replace with:

```csharp
terrainGO.transform.position = new Vector3(-terrainX / 2f, -TerrainYOffset, -terrainZ / 2f);
```

Do not touch `CreateTerrain`'s use of `ShoreDepthMeters` in
`elevRange`/`normalizedFlat` — those compute headroom, which is correct.

---

### Part 3 — Rewrite `CreateWaterMeshes`

Find the method `CreateWaterMeshes` (signature:
`private static void CreateWaterMeshes(TerrainData terrainData, GameObject terrainGO, Transform parentRoot, string exportPath, string dataDir, string projectRoot, bool[,] holes)`).

The method has TWO sections:
1. The per-water-body mesh-building `foreach (var water in waterFile.water)` loop
2. A trailing "Shore slope pass" section that builds `isWater` mask and depresses terrain

**Delete section 2 entirely.** That work moves to `DepressTerrainUnderOverlays`
(Part 4). Keep only the final `File.Copy` of water.json to Assets and the final
`Debug.Log`.

**Rewrite section 1 (the per-water-body loop):**

Currently each iteration samples terrain height per vertex and sets
`wy = terrainBaseY + terrainH - 0.1f` — this creates uneven water surface that
seesaws along sloped shores.

Replace the loop body with flat-CDT construction. Here is the complete
replacement for the entire `foreach` body:

```csharp
foreach (var water in waterFile.water)
{
    if (water.contour == null || water.contour.Length < 3) continue;

    int n = water.contour.Length;

    // 3A. Flat water Y = min terrain height across contour − 0.05m
    float minTerrainH = float.MaxValue;
    for (int i = 0; i < n; i++)
    {
        float wx = water.contour[i].x;  // Geo: no rotation
        float wz = water.contour[i].z;
        float th = terrain.SampleHeight(new Vector3(wx, 0, wz));
        if (th < minTerrainH) minTerrainH = th;
    }
    float waterY = terrainBaseY + minTerrainH - 0.05f;

    // 3B. CDT triangulation — same pattern as fairway/tee.
    // Water doesn't need fine terrain conformance (flat surface), but CDT
    // needs interior Steiner points for clean triangulation of large
    // concave shapes. 2.0m grid spacing is plenty.
    float tileSize = 10f; // world-UV tiling for URPWater shader
    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(wx / tileSize, wz / tileSize);

    var (rawVerts, uvs, tris) = CDTTriangulate(
        water.contour, terrain, terrainBaseY, 0f, 2.0f, uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
    {
        Debug.LogWarning($"[HoleGeoImporter] Water {water.id}: CDT failed, skipping");
        continue;
    }

    // 3C. Flatten all vertex Y to waterY (CDT sampled terrain heights;
    // overwrite them so the surface is perfectly flat).
    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i].y = waterY;

    // 3D. Center mesh at centroid (Y=0 origin pattern, same as fairway).
    float cx = 0f, cz = 0f;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0f, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // 3E. Winding check — ensure top faces up.
    if (tris.Length >= 3)
    {
        Vector3 a = rawVerts[tris[0]];
        Vector3 b = rawVerts[tris[1]];
        Vector3 c = rawVerts[tris[2]];
        float cross = (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
        if (cross > 0)
        {
            for (int t = 0; t < tris.Length; t += 3)
            { int tmp = tris[t]; tris[t] = tris[t + 2]; tris[t + 2] = tmp; }
        }
    }

    var mesh = new Mesh();
    mesh.name = $"Water_{water.id}";
    mesh.vertices = rawVerts;
    mesh.uv = uvs;
    mesh.triangles = tris;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Water_{water.id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = waterMat;

    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = Golfin.Course.SurfaceType.Water;

    go.transform.SetParent(waterRoot.transform);

    Debug.Log($"[HoleGeoImporter] Water {water.id}: {n} contour verts, " +
              $"{rawVerts.Length} CDT verts, {tris.Length / 3} tris, " +
              $"waterY={waterY:F2}");
}
```

**Notes:**
- Keep the two existing `Debug.Log` lines at method top
  (`terrainBaseY={...}` / `ShoreDepthMeters={...}`). They're useful.
- Keep the `waterRoot`/`terrain`/`terrainBaseY`/`waterMat` setup at top
  of the method — unchanged.
- The old section 2 (shore slope, `isWater` mask, chamfer distance,
  `underwaterDrop`, `terrainData.SetHeights`) is DELETED.

---

### Part 4 — Add water handling to `DepressTerrainUnderOverlays`

`DepressTerrainUnderOverlays` currently handles fairway + tee + cart path.
Add water.

**4A. Add water contours to the `depress` bool[,] array.**

In `DepressTerrainUnderOverlays`, find the tee contour section
(immediately after fairway, uses `zone-contours.json` and the
`data.zones.tee` loop with `MarkContourCells(region.contour, depress, ...)`).

Immediately AFTER the closing brace of that tee block, BEFORE the cart path
section (the `cartDepress` block), insert:

```csharp
// Water contours — use same flat depression as fairway/tee
// but with shore slope ramp applied afterward.
string waterPath = Path.Combine(exportPath, "water.json");
if (File.Exists(waterPath))
{
    var waterData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterPath));
    if (waterData.water != null)
    {
        foreach (var w in waterData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, depress,
                    hRes, terrainPos, terrainSize, 0f);
                    // inset=0 — depress right up to the contour edge
        }
    }
}
```

This makes water cells receive the standard `OverlayDepressionMeters` (0.40m)
flat drop in the existing apply loop. No separate water-depression needed.

**4B. Add shore slope pass after the existing apply loop.**

The existing apply loop ends with `depressedCount += cartDepressedCount;` and
then `terrainData.SetHeights(0, 0, heights);` followed by a Debug.Log.

**BEFORE** `terrainData.SetHeights(0, 0, heights);`, insert the shore slope
pass:

```csharp
// ─── Shore slope pass: gradual ramp outside water contours ─────────
// Creates a smooth transition from shoreline (full ShoreDepthMeters drop)
// to surrounding terrain (no drop) over ShoreRadius cells.
// Without this, water edges would cliff against un-depressed terrain.
string waterShorePath = Path.Combine(exportPath, "water.json");
int shoreCount = 0;
if (File.Exists(waterShorePath) && ShoreRadius > 0 && ShoreDepthMeters > 0f)
{
    // 4B-1. Build water-only mask from water contours.
    bool[,] waterMask = new bool[hRes, hRes];
    var waterShoreData = JsonUtility.FromJson<WaterFileData>(
        File.ReadAllText(waterShorePath));
    if (waterShoreData.water != null)
    {
        foreach (var w in waterShoreData.water)
        {
            if (w.contour != null && w.contour.Length >= 3)
                MarkContourCells(w.contour, waterMask,
                    hRes, terrainPos, terrainSize, 0f);
        }
    }

    // 4B-2. Chamfer distance transform from water boundary (cells not in water).
    float[,] distToWater = new float[hRes, hRes];
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
            distToWater[z, x] = waterMask[z, x] ? 0f : float.MaxValue;

    // Forward pass
    for (int z = 0; z < hRes; z++)
        for (int x = 0; x < hRes; x++)
        {
            if (x > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x - 1] + 1f);
            if (z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x] + 1f);
            if (x > 0 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x - 1] + 1.414f);
            if (x < hRes - 1 && z > 0)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z - 1, x + 1] + 1.414f);
        }
    // Backward pass
    for (int z = hRes - 1; z >= 0; z--)
        for (int x = hRes - 1; x >= 0; x--)
        {
            if (x < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z, x + 1] + 1f);
            if (z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x] + 1f);
            if (x < hRes - 1 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x + 1] + 1.414f);
            if (x > 0 && z < hRes - 1)
                distToWater[z, x] = Mathf.Min(distToWater[z, x],
                    distToWater[z + 1, x - 1] + 1.414f);
        }

    // 4B-3. Apply ramp OUTSIDE water (full drop at boundary,
    //       zero drop at ShoreRadius). Skip water cells (already
    //       depressed in step 4A) and fairway/tee/cart cells
    //       (already fully depressed — another drop would stack).
    float shoreDropNorm = ShoreDepthMeters / elevRange;
    int shoreRadiusCells = ShoreRadius;

    for (int z = 0; z < hRes; z++)
    {
        for (int x = 0; x < hRes; x++)
        {
            if (waterMask[z, x]) continue;           // water cell: skip
            if (depress[z, x]) continue;             // fairway/tee/water: skip
            if (cartDepress[z, x]) continue;         // cart path: skip

            float dist = distToWater[z, x];
            if (dist <= 0f || dist > shoreRadiusCells) continue;

            // smoothstep: 1 at boundary, 0 at shoreRadius
            float t = 1f - (dist / shoreRadiusCells);
            t = t * t * (3f - 2f * t);
            float drop = shoreDropNorm * t;

            heights[z, x] = Mathf.Max(0f, heights[z, x] - drop);
            shoreCount++;
        }
    }
}
```

Then update the final Debug.Log to include shore:

```csharp
Debug.Log($"[HoleGeoImporter] Terrain depression: {depressedCount}" +
          $" cells lowered by {OverlayDepressionMeters:F2}m" +
          $" (cart path: {cartDepressedCount} cells," +
          $" water shore ramp: {shoreCount} cells)");
```

**Important:** The variable `cartDepress` is defined inside
`DepressTerrainUnderOverlays` and is in scope at the insertion point — it's
created above the fairway/tee sections. Verify the variable is accessible
where you insert the shore pass. If for any reason the cart path mask is
scoped differently in Geo, drop the `if (cartDepress[z, x]) continue;` line
(worst case: cart-path grass gets an extra shore drop near water, visually
harmless).

---

### Part 5 — Update water material depth settings

In `CreateWaterMaterial`, find:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.3f);
```

Change to:

```csharp
mat.SetFloat("_DepthStart", 0f);
mat.SetFloat("_DepthEnd", 0.8f);
```

This gives the depth-based color gradient room to work with the new 0.4m
shore depression.

---

### Execution order

1. Part 1 (constants)
2. Part 2 (terrain position)
3. Part 5 (material — trivial, do it while you're near the constants area)
4. Part 3 (CreateWaterMeshes rewrite)
5. Part 4 (DepressTerrainUnderOverlays — 4A then 4B)

---

### Verification

Re-import Hole 07 Geo: `Import > Geo > Normal > Import Hole 07 Geo`

- [ ] Water surface is perfectly flat (single Y per body, no seesaw)
- [ ] Water edges line up with terrain edges (no dark cliff strip)
- [ ] Shore slopes gradually into water
- [ ] Depth-based color: shallower teal near edges, darker blue toward center
- [ ] No z-fighting between water mesh and terrain
- [ ] Fairways, tees, bunkers, greens, cart paths unaffected

Then regression check with a hole without water:

- [ ] `Import Hole 01 Geo` completes without errors (Hole 1 has no water —
      make sure the water file handling degrades cleanly)

And a hole with multiple water bodies:

- [ ] `Import Hole 12 Geo` — waterways + pond, check both look flat

---

### Do NOT change

- `CreateWaterMaterial` shader selection (URPWater/Standard)
- Any other CreateWaterMeshes setup code (waterRoot, terrain, terrainBaseY,
  waterMat vars, File.Copy at end)
- Fairway/tee/green/bunker/cart path logic
- UHoleGeo export pipeline — `water.json` is fine as-is
- The disabled `if (false && loadedRaw)` boundary propagation block

---

## Completed Tasks

✅ DONE: 2026-04-17 — Replaced chamfer distance with exact polygon-edge distance for shore ramp; all blur attempts failed (lesson written)

✅ DONE: 2026-04-17 — Masked 3-pass box blur on rampMask cells to kill chamfer quantization stripes
✅ DONE: 2026-04-17 — Absolute-target shore ramp: waterSurfaceY per body, joint chamfer propagates nearestSurfaceY, lerp replaces fixed-drop
✅ DONE: 2026-04-17 — Absolute water floor for sloped contours: per-body floorNorm, waterMask separate from depress, fairway/cart loops skip water cells, shore reuses waterMask
✅ DONE: 2026-04-17 — Water rework ported to HoleGeoImporter: flat CDT, TerrainYOffset, water depression in DepressTerrainUnderOverlays, shore slope ramp, _DepthEnd 0.8
✅ DONE: 2026-04-16 — Flat inside + 8-cell outward smoothstep ramp implemented
✅ DONE: 2026-04-16 — Green collar CDT complete
✅ DONE: 2026-04-16 — Bunker lip submesh complete
✅ 2026-04-16 — Bunker lip baked as submesh 1
✅ 2026-04-16 — Cart path outward smoothstep ramp (8 cells)
✅ 2026-04-16 — Cart path flat depression
✅ 2026-04-16 — Spline cart path depression footprint
✅ 2026-04-16 — Spline cart path meshes
✅ 2026-04-16 — Fringe/border baked into parent CDT mesh as submesh
✅ 2026-04-14 — Water rework complete (HoleLiteImporter only — Geo ported 2026-04-17)
✅ 2026-04-13 — Cart path flat depression + spine fixes
✅ 2026-04-13 — Natural OB↔Rough transition + Smooth OB
✅ 2026-04-12 — CDT triangulation for fairway/tee/cart path meshes
✅ All earlier tasks
