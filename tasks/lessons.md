# Lessons Learned

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
