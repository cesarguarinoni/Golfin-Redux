# TellCode.md — Instructions from Claude (Architect) to Claude Code

> Claude Code: Read this file at the start of each task. Execute the latest instruction block.
> After completing, add a status line at the bottom: `✅ DONE: [date] [brief summary]`
> Claude (Architect) will update this file with new instructions as needed.
> Handoff: `Docs/TellCode.md`

---

## Current Task — Drop the Tee Border Ring

The terrain skirt from the last task is working — tees are raised
platforms with a gentle mound. But the dark border ring is still
fighting the terrain on the downhill side (ragged tearing where the
dilated CDT can't resolve the height differential) and floating on the
uphill side. The ring is decorative at this point: the mound itself is
the visual boundary, and the tee material is already distinct from
fairway/rough. Real golf tees rarely have a visible dark collar.

**This task: build tees as a simple single-submesh flat CDT, no
border.** Pattern is identical to the water mesh — straight CDT of the
contour, flatten all vert Y to the platform height, one material.

**Target file:** `Assets/Scripts/Editor/CourseImporter/HoleGeoImporter.cs`
**No changes to:** `FlattenTerrainUnderTees`, `DepressTerrainUnderOverlays`,
`CreateTeeMeshWithBorder` (leave intact — it's dead code for tees but we
keep it in case something else wants it, and for future "inside inset"
option if we change our minds).

---

### Step 1 — Add a new borderless tee mesh builder

Add this new helper next to `CreateTeeMeshWithBorder` in the same class.
Pattern mirrors `CreateWaterMeshes`'s per-body loop (lines 2851–2915).
The key move: we **flatten all verts to a single platform Y** instead
of sampling terrain, so the mesh is guaranteed level regardless of
what `FlattenTerrainUnderTees` did to the heightmap.

```csharp
/// <summary>
/// Flat single-submesh tee mesh. CDTs the contour directly, flattens
/// all verts to platformY, no border ring. Visual boundary comes from
/// the raised terrain mound (built by FlattenTerrainUnderTees) rather
/// than a dark collar mesh.
/// </summary>
private static GameObject CreateTeeMeshFlat(
    int id, ContourPoint[] contour,
    Terrain terrain, float terrainBaseY,
    Material mat, float tileSize,
    Golfin.Course.SurfaceType surfaceType)
{
    int nc = contour.Length;
    if (nc < 3) return null;

    float yOffset = 0.02f; // match CreateTeeMeshWithBorder convention

    System.Func<float, float, Vector2> uvFunc = (wx, wz) =>
        new Vector2(wx / tileSize, wz / tileSize);

    // CDT the tee contour directly — no dilation, no inner constraint.
    var (rawVerts, uvs, tris) = CDTTriangulate(
        contour, terrain, terrainBaseY, yOffset, 1.0f, uvFunc);

    if (rawVerts == null || tris == null || tris.Length < 3)
    {
        Debug.LogWarning($"[HoleGeoImporter] Tee {id}: CDT failed");
        return null;
    }

    // Platform Y = max of sampled verts. FlattenTerrainUnderTees already
    // raised the terrain under the contour to a single height, so all
    // verts should already agree (up to bilinear interpolation noise).
    // Taking max guarantees we never dip below the terrain and avoids
    // any sub-cm sampling waviness on the mesh top.
    float platformY = float.MinValue;
    for (int i = 0; i < rawVerts.Length; i++)
        if (rawVerts[i].y > platformY) platformY = rawVerts[i].y;

    // Flatten all verts to platformY.
    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i].y = platformY;

    // Center mesh at centroid — same pattern as water/fairway.
    float cx = 0f, cz = 0f;
    for (int i = 0; i < rawVerts.Length; i++)
    { cx += rawVerts[i].x; cz += rawVerts[i].z; }
    cx /= rawVerts.Length; cz /= rawVerts.Length;
    Vector3 centroid = new Vector3(cx, 0f, cz);

    for (int i = 0; i < rawVerts.Length; i++)
        rawVerts[i] -= centroid;

    // Winding check — ensure top faces up.
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
    mesh.name = $"Tee_{id}";
    mesh.vertices = rawVerts;
    mesh.uv = uvs;
    mesh.triangles = tris;
    mesh.RecalculateNormals();
    mesh.RecalculateBounds();

    var go = new GameObject($"Tee_{id}");
    go.transform.position = centroid;
    go.AddComponent<MeshFilter>().sharedMesh = mesh;
    go.AddComponent<MeshRenderer>().sharedMaterial = mat;

    AddCleanMeshCollider(go, mesh);

    var marker = go.AddComponent<Golfin.Course.SurfaceMarker>();
    marker.surfaceType = surfaceType;

    return go;
}
```

---

### Step 2 — Replace the tee mesh callsite

In `CreateFlatZoneMeshes`, find this block (around line 3800–3815):

```csharp
foreach (var region in data.zones.tee)
{
    if (region.contour == null || region.contour.Length < 3) continue;
    var meshGO = CreateTeeMeshWithBorder(
        region.id, "Tee", region.contour,
        terrain, terrainBaseY,
        teeMat, 3f,
        teeBorderMat, 0.5f, 3f,
        Golfin.Course.SurfaceType.Tee);
    if (meshGO != null)
        meshGO.transform.SetParent(teeRoot.transform);
}
```

Replace with:

```csharp
foreach (var region in data.zones.tee)
{
    if (region.contour == null || region.contour.Length < 3) continue;
    var meshGO = CreateTeeMeshFlat(
        region.id, region.contour,
        terrain, terrainBaseY,
        teeMat, 3f,
        Golfin.Course.SurfaceType.Tee);
    if (meshGO != null)
        meshGO.transform.SetParent(teeRoot.transform);
}
```

---

### Step 3 — Leave the border material block in place

**Do NOT delete the `teeBorderMat` setup block** above the foreach.
Keep `MAT_TeeBorder.mat` being generated on every import. Rationale:

- If we decide a collar/fringe would improve the look (e.g., switching
  to an "inside-inset" border that lives fully on the flat platform),
  the material is already built and wired, so reintroducing it is a
  one-line change at the mesh-builder callsite.
- The material asset is cheap — a `.mat` file with one albedo and one
  normal, regenerated on import. No runtime cost if nothing references
  it.

The variable `teeBorderMat` will be declared but only used if the
callsite ever passes it. On Unity's side this is harmless — C# doesn't
warn about unused locals in this context, and the material file lives
in `{dataDir}/MAT_TeeBorder.mat` as inert project data.

Add a short comment above the block so future readers know it's
intentional:

```csharp
// Tee border material — kept built & ready even though CreateTeeMeshFlat
// doesn't use it. If we decide to bring back a fringe (e.g., inside-inset
// variant that lives on the flat platform), swap the callsite back to a
// border-using builder and this material is already wired up.
var teeBorderMat = new Material(GetLitShader());
// ...rest of existing block unchanged...
```

(Add the comment; don't touch anything else in the block.)

---

### Step 4 — Verification

Re-import the pancake hole:

- [ ] Flat elliptical top (unchanged from last iteration).
- [ ] **No dark border ring** — the tee surface extends edge-to-edge
      with a single material.
- [ ] Clean edge where tee surface meets the terrain skirt — no tearing,
      no floating arc, no ragged boundary.
- [ ] Skirt mound still looks right (Step 4 of the previous task).

Regression:

- [ ] Hole 1 — 3 tees, check big back tee and both small forward tees.
      All should be borderless, flat, flush with skirt.
- [ ] Hole 18 — 6 small tees. No ring visible on any of them.
- [ ] Hole 7 — water-adjacent tee, border removal shouldn't affect
      water. Skirt still skips water cells per `FlattenTerrainUnderTees`
      (it doesn't — only skips fairway/green — so if there's a problem
      near water, flag it and we'll add water to the skip mask.
      Unlikely on Hole 7 since tees aren't near water.)
- [ ] `Debug.Log` shows `Tee {id}: ... platformY=...` per tee, no CDT
      failure warnings.
- [ ] `MAT_TeeBorder.mat` still generated in each hole's `dataDir` —
      unused but present, ready if we resurrect the fringe.
- [ ] No compiler warnings about unused locals. (If the compiler does
      complain, suppress with `_ = teeBorderMat;` as the last line of
      the block — but this is unlikely for materials assigned to
      `AssetDatabase`.)

---

### Do NOT change

- `CreateTeeMeshWithBorder` — leave it in the file, even though nothing
  calls it. If we want to resurrect the "inside inset" border option
  later, it's our starting point.
- `FlattenTerrainUnderTees` — the skirt ramp is exactly right.
- `DepressTerrainUnderOverlays` — tees still go into the shared
  `depress` mask and still get a 0.40m drop under the mesh (invisible
  z-fight filler, same as fairway).
- Green, fairway, bunker, water, cart path meshes.
- The `T_TeeDark_Albedo` / `T_TeeDark_Normal` texture assets.
- The `teeBorderMat` setup block — kept as-is, material still generated.
- Any other `MAT_TeeBorder.mat` consumers.

---

### Design note

Real golf tees are usually identified visually by two cues: (1) the
raised mound, and (2) the tee box's distinct grass — typically a
tighter-mown area than the surrounding fairway or rough. Our setup
provides both: the skirt mound from `FlattenTerrainUnderTees` and the
`T_Tee_Albedo` texture via `teeMat`. The dark border collar was a
stylistic flourish that's not needed for zone identification, and
removing it from the mesh eliminates the last class of geometric
artifacts from the tee rendering.

We keep the border material wired up because it's cheap insurance. If
we later decide the transition from tee surface to skirt needs a
visual seam — or if a specific hole's mow pattern looks weird without
a collar — we can bring back a fringe by changing one callsite.

---

✅ DONE: 2026-04-17 — FlattenTerrainUnderTees extended with chamfer-distance skirt ramp (2m smoothstep). Green Y fixed by setting yOffset=0.00f in CreateGreenMeshCDT.
