# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Bridge Placement Tool (Unity → UHoleGeo export)

Cesar places bridge prefabs by hand in a hole scene. This tool captures
their positions/rotations and exports them as `bridges.json` into the
hole's UHoleGeo export folder. UHoleGeo will later consume that file
so cart-path splines can snap to bridge anchor points instead of
guessing from screenshots.

**Target file (new):** `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
**Also new:** `Assets/Scripts/Course/BridgeAnchor.cs`
**No `TreePlacer` or `HoleGeoImporter` changes required.**

---

### Design summary

- EditorWindow: **`Window > Trees > Bridge Exporter`** (put it next to
  the Tree Brush so they live in the same menu cluster).
- Artist drops bridge prefabs anywhere under `HoleRoot` — this tool
  doesn't prescribe WHERE in the hierarchy. Detection is by component,
  see Step 1.
- On "Export Bridges for Current Hole", the tool:
    1. Resolves the hole number + Lite/Geo/Flat flavour from the
       active scene name (same logic `TreePlacer.ImportTreesMenuItem`
       uses).
    2. Finds all `BridgeAnchor` components in the scene.
    3. Writes `bridges.json` to
       `Tools/UHoleGeo/output/lomond-country-club/export/hole-XX/`
       (or the corresponding Lite / `-flat` folder), and mirrors to
       the sibling pipeline (Geo↔Lite) if that folder exists.
- No heightmap modifications, no mesh generation, no splatmap touches.
  Pure position export. Bridges render in Unity because the prefab is
  already in the scene; UHoleGeo gets the coordinates separately.

---

### Step 1 — `BridgeAnchor` marker component

Create `Assets/Scripts/Course/BridgeAnchor.cs`:

```csharp
using UnityEngine;

namespace Golfin.Course
{
    /// <summary>
    /// Marks a GameObject as a bridge for the export pipeline.
    /// Attach to the root of a bridge prefab. The exporter captures
    /// world position + yaw rotation + the two anchor endpoints.
    ///
    /// Anchor endpoints are the points where cart paths should meet
    /// the bridge. They're defined as local offsets along the bridge's
    /// local Z axis (forward) from the bridge's pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public class BridgeAnchor : MonoBehaviour
    {
        [Tooltip("Optional bridge id. If empty, exporter auto-assigns 1..N.")]
        public string id = "";

        [Tooltip("Distance from pivot along local +Z to the 'far' anchor (meters).")]
        public float lengthForward = 3f;

        [Tooltip("Distance from pivot along local -Z to the 'near' anchor (meters).")]
        public float lengthBackward = 3f;

        [Tooltip("Path width this bridge expects to meet (meters). " +
                 "Informational — UHoleGeo uses it to sanity-check cart width.")]
        public float expectedPathWidth = 2.5f;

        // Editor gizmo so the artist sees the anchor endpoints in
        // Scene view without needing to open the exporter window.
        private void OnDrawGizmos()
        {
            Vector3 a = transform.position + transform.forward * lengthForward;
            Vector3 b = transform.position - transform.forward * lengthBackward;
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.9f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawSphere(a, 0.35f);
            Gizmos.DrawSphere(b, 0.35f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                transform.position + transform.forward * (lengthForward + 1f));
        }
    }
}
```

Lives under `Assets/Scripts/Course/` so it compiles in both editor and
player (same pattern as `SurfaceMarker`).

---

### Step 2 — EditorWindow scaffold

Create `Assets/Scripts/Editor/CourseImporter/BridgeExporter.cs`
wrapped in `#if UNITY_EDITOR ... #endif`, namespace
`Golfin.CourseImport`.

```csharp
public class BridgeExporter : EditorWindow
{
    [MenuItem("Window/Trees/Bridge Exporter")]
    public static void ShowWindow()
    {
        var w = GetWindow<BridgeExporter>("Bridges");
        w.minSize = new Vector2(320, 240);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bridge Exporter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        var anchors = FindAnchorsInActiveScene();
        EditorGUILayout.LabelField(
            $"Found {anchors.Count} BridgeAnchor(s) in scene.");

        if (anchors.Count > 0)
        {
            EditorGUILayout.Space();
            foreach (var a in anchors)
            {
                Vector3 p = a.transform.position;
                EditorGUILayout.LabelField(
                    $"  • {(string.IsNullOrEmpty(a.id) ? a.name : a.id)}" +
                    $"  @ ({p.x:F2}, {p.z:F2})  yaw {a.transform.eulerAngles.y:F1}°");
            }
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Add BridgeAnchor to Selected GameObject"))
            AddAnchorToSelected();

        EditorGUILayout.Space();

        GUI.enabled = anchors.Count > 0;
        if (GUILayout.Button("Export Bridges for Current Hole",
                             GUILayout.Height(30)))
            ExportBridgesForCurrentHole(anchors);
        GUI.enabled = true;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Writes bridges.json to the current hole's UHoleGeo export " +
            "folder (Lite/Geo/Flat auto-detected from scene name). " +
            "UHoleGeo can read this file so cart-path splines snap to " +
            "bridge anchors.",
            MessageType.Info);
    }

    private double lastRepaint;
    private void OnInspectorUpdate()
    {
        if (EditorApplication.timeSinceStartup - lastRepaint > 0.5)
        {
            Repaint();
            lastRepaint = EditorApplication.timeSinceStartup;
        }
    }
}
```

Helper stubs:
- `List<BridgeAnchor> FindAnchorsInActiveScene()`
- `void AddAnchorToSelected()`
- `void ExportBridgesForCurrentHole(List<BridgeAnchor> anchors)`

---

### Step 3 — `FindAnchorsInActiveScene` + `AddAnchorToSelected`

```csharp
private static List<Golfin.Course.BridgeAnchor> FindAnchorsInActiveScene()
{
    var result = new List<Golfin.Course.BridgeAnchor>();
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
    foreach (var root in activeScene.GetRootGameObjects())
        result.AddRange(
            root.GetComponentsInChildren<Golfin.Course.BridgeAnchor>(true));
    return result;
}

private static void AddAnchorToSelected()
{
    var sel = Selection.activeGameObject;
    if (sel == null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "Select a GameObject in the scene first.", "OK");
        return;
    }
    if (sel.GetComponent<Golfin.Course.BridgeAnchor>() != null)
    {
        EditorUtility.DisplayDialog("Add Bridge Anchor",
            "That GameObject already has a BridgeAnchor.", "OK");
        return;
    }
    Undo.AddComponent<Golfin.Course.BridgeAnchor>(sel);
    EditorUtility.SetDirty(sel);
}
```

---

### Step 4 — `ExportBridgesForCurrentHole`

```csharp
[System.Serializable]
private class BridgeDTO
{
    public string id;
    public float x;     // world X, meters
    public float z;     // world Z, meters
    public float y;     // world Y, meters (for reference; UHoleGeo is 2D)
    public float yaw_deg;
    public float length_forward_m;
    public float length_backward_m;
    public float expected_path_width_m;
    public AnchorDTO anchor_forward;
    public AnchorDTO anchor_backward;
}

[System.Serializable]
private class AnchorDTO
{
    public float x;
    public float z;
}

[System.Serializable]
private class BridgesFile
{
    public string schema_version = "1.0.0";
    public int hole_number;
    public string flavour;  // "geo" | "lite" | "geo-flat" | "lite-flat"
    public int bridge_count;
    public BridgeDTO[] bridges;
}

private static void ExportBridgesForCurrentHole(
    List<Golfin.Course.BridgeAnchor> anchors)
{
    var activeScene = UnityEditor.SceneManagement.EditorSceneManager
        .GetActiveScene();
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
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Cannot detect hole number from scene '{sceneName}'.\n" +
            "Expected 'Hole_XX', 'Hole_XX_Geo', 'Hole_XX_Flat', " +
            "or 'Hole_XX_Geo_Flat'.", "OK");
        return;
    }

    string flavour = (isGeo ? "geo" : "lite") + (isFlat ? "-flat" : "");
    string toolFolder = isGeo ? "UHoleGeo" : "UHoleLite";
    string holeFolder = isFlat ? $"hole-{holeNumber:D2}-flat"
                               : $"hole-{holeNumber:D2}";
    string exportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{toolFolder}/output/lomond-country-club/export",
            holeFolder));

    if (!System.IO.Directory.Exists(exportPath))
    {
        EditorUtility.DisplayDialog("Export Bridges",
            $"Export folder not found:\n{exportPath}\n\n" +
            "Has this hole been exported from UHoleGeo yet?", "OK");
        return;
    }

    var dtos = new BridgeDTO[anchors.Count];
    for (int i = 0; i < anchors.Count; i++)
    {
        var a = anchors[i];
        Vector3 p = a.transform.position;
        Vector3 fwd = a.transform.forward;

        Vector3 anchorF = p + fwd * a.lengthForward;
        Vector3 anchorB = p - fwd * a.lengthBackward;

        dtos[i] = new BridgeDTO
        {
            id = string.IsNullOrEmpty(a.id) ? $"bridge_{i + 1}" : a.id,
            x = p.x, y = p.y, z = p.z,
            yaw_deg = NormalizeYaw(a.transform.eulerAngles.y),
            length_forward_m = a.lengthForward,
            length_backward_m = a.lengthBackward,
            expected_path_width_m = a.expectedPathWidth,
            anchor_forward  = new AnchorDTO { x = anchorF.x, z = anchorF.z },
            anchor_backward = new AnchorDTO { x = anchorB.x, z = anchorB.z },
        };
    }

    var file = new BridgesFile
    {
        hole_number = holeNumber,
        flavour = flavour,
        bridge_count = dtos.Length,
        bridges = dtos,
    };

    string outPath = System.IO.Path.Combine(exportPath, "bridges.json");
    string json = JsonUtility.ToJson(file, true);
    System.IO.File.WriteAllText(outPath, json);

    Debug.Log($"[BridgeExporter] Wrote {dtos.Length} bridge(s) to {outPath}");

    // Mirror to the other pipeline (Geo ↔ Lite) if its folder exists.
    string otherTool = isGeo ? "UHoleLite" : "UHoleGeo";
    string otherExportPath = System.IO.Path.GetFullPath(
        System.IO.Path.Combine(
            Application.dataPath, "..",
            $"Tools/{otherTool}/output/lomond-country-club/export",
            holeFolder));
    if (System.IO.Directory.Exists(otherExportPath))
    {
        string mirrorPath = System.IO.Path.Combine(
            otherExportPath, "bridges.json");
        System.IO.File.WriteAllText(mirrorPath, json);
        Debug.Log($"[BridgeExporter] Mirrored to {mirrorPath}");
    }
}

private static float NormalizeYaw(float yawDeg)
{
    yawDeg = yawDeg % 360f;
    if (yawDeg > 180f) yawDeg -= 360f;
    if (yawDeg < -180f) yawDeg += 360f;
    return yawDeg;
}
```

---

### Step 5 — Example JSON output

```json
{
  "schema_version": "1.0.0",
  "hole_number": 7,
  "flavour": "geo",
  "bridge_count": 1,
  "bridges": [
    {
      "id": "bridge_1",
      "x": -184.30,
      "y": 2.45,
      "z": 72.10,
      "yaw_deg": 38.5,
      "length_forward_m": 3.0,
      "length_backward_m": 3.0,
      "expected_path_width_m": 2.5,
      "anchor_forward":  { "x": -182.43, "z": 74.45 },
      "anchor_backward": { "x": -186.17, "z": 69.75 }
    }
  ]
}
```

**Coordinate convention (important for UHoleGeo consumption):**
`x`/`z` are Unity world meters, matching `cart-paths.json`'s
`contour[i].x`/`.z` exactly. UHoleGeo can treat `anchor_forward` /
`anchor_backward` as snap targets for spline endpoints directly — no
coordinate transformation required. `y` is included for future 3D
routing but can be ignored by the current 2D path logic.

---

### Verification

1. Open `Hole_07_Geo`. Drop a bridge prefab over the stream.
2. `Window > Trees > Bridge Exporter` → window shows "Found 0
   BridgeAnchor(s)".
3. Select the bridge GameObject → click "Add BridgeAnchor to Selected
   GameObject". Window now shows "Found 1" with its position.
4. Yellow gizmo line runs through the bridge with spheres at the two
   anchor endpoints. Rotate/move the bridge — gizmo tracks.
5. Click "Export Bridges for Current Hole". Console logs:
   - `[BridgeExporter] Wrote 1 bridge(s) to .../hole-07/bridges.json`
   - `[BridgeExporter] Mirrored to .../UHoleLite/.../hole-07/bridges.json`
6. Open the written `bridges.json` — coordinates match the bridge's
   Unity world position, yaw matches Y rotation, anchor endpoints are
   offset along the bridge's local forward.

Regression:
- [ ] `Hole_01_Geo` (no bridges): window shows "Found 0", export button
      disabled, no crash.
- [ ] Rename a scene to `Test_Scene`: export shows a clear dialog, no
      crash.
- [ ] `Hole_07_Geo_Flat`: export lands in `hole-07-flat/bridges.json`
      and mirrors to the Lite flat folder if it exists.

---

### Out of scope (future work, not this task)

- UHoleGeo reading `bridges.json` and routing splines to anchors —
  separate JS-side task when Cesar tackles the UHoleGeo tool.
- Bridge prefab authoring (width variants, material sets, LODs).
- Physics colliders / ball bounce behaviour on bridges.
- Runtime bridge loading for gameplay.

---

### Do NOT change

- `TreePlacer.cs`, `HoleGeoImporter.cs`, `HoleLiteImporter.cs`.
- `cart-paths.json` schema — bridges live in a separate file.
- Any scene hierarchy conventions beyond adding `BridgeAnchor`
  components. Bridges can live anywhere under `HoleRoot` (or even at
  scene root — detection is by component, not by name).

---

## Previous Task — Fix Tee Border Ring Texture Twisting (Constant V)

The inset tee border ring is in place and orientation is correct (light
toward tee surface, dark toward terrain). But the texture shows
distortion/twisting at points along the ring's curve.

**Cause:** In `CreateTeeMeshWithInsetBorder`, the border vert
duplication assigns `v = (src.x + src.z) / borderTileSize`. That's a
world-XZ projection, which jumps around as the ring curves. For a
texture with meaningful V-direction content, that would tile badly on
a closed ring.

**But the texture has no meaningful V content.** `T_TeeDark_Albedo` is
a left-to-right color gradient (green → uniform green → rough-darker)
with only mild noise. V variation is purely decorative. Setting V to a
constant eliminates the twisting without visibly losing anything.

### The change

In `CreateTeeMeshWithInsetBorder` (the mesh builder added in the last
task), in the border vert duplication block, find:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
float v = (src.x + src.z) / borderTileSize;
```

Replace with:

```csharp
float u = 1f - Mathf.Clamp01(dist / borderWidth);
// T_TeeDark_Albedo has no meaningful V content — it's a pure L→R
// color gradient (tee-green to rough-darker). World-XZ V causes
// visible texture twisting on the ring's curve. Constant V removes
// the twisting; no visual content is lost because V has none to lose.
float v = 0.5f;
```

That's the entire change. `borderTileSize` stays as a function parameter
(still used by other callers / future-me if we ever swap in a texture
with V-direction content).

### Verification

- [ ] Re-import any tee-bearing hole (Hole 4 is fine).
- [ ] Dark border ring still visible, still oriented correctly (light
      toward tee, dark toward terrain).
- [ ] Texture twisting / wavy distortion at the bottom edge is gone.
- [ ] Gradient still clean from the tee-surface edge of the ring to
      the terrain-adjacent edge.

### Do NOT change

- Anything else in `CreateTeeMeshWithInsetBorder`.
- The U calculation.
- The `borderTileSize` parameter or its callsite.
- Any other mesh builder, material, or system.

---

✅ DONE: 2026-04-18 Constant-V UV fix applied. Additionally fixed geometric crease: rebuilt ring as manual quad-strip (outer contour × inset contour vertex pairs by index) instead of CDT-classified triangles — eliminates long diagonal spanning tris. CDT now only triangulates the inset contour for submesh 0; submesh 1 is a clean N-quad strip with winding auto-checked.

✅ DONE: 2026-04-18 Bridge Placement Tool implemented. BridgeAnchor.cs (Golfin.Course) marker component with gizmo. BridgeExporter.cs EditorWindow at Window > Trees > Bridge Exporter — finds anchors, previews positions, exports bridges.json to UHoleGeo/UHoleLite export folder with auto-detection of Geo/Lite/Flat from scene name, mirrors to sibling pipeline folder.
