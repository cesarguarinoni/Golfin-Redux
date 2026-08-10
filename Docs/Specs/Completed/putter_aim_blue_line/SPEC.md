# SPEC — `putter_aim_blue_line`

**Status:** SPEC_READY · **Tier:** 2 (TellCode) · **Author:** Claude (Architect) · **Rev 2, 2026-08-07 JST** (Rev 1: 2026-08-03 13:10 JST)
**Rev 2 adds:** §8 mobile performance budget (device era — the line must be free at 60 fps) and §9 competitive check confirming the Rev 1 straight-line design. §§1–7 unchanged except the two DoD lines noted in §8.4.
**Notion:** `putter_aim_blue_line` (Order 413, P1 — High, Loop v2)
**Supersedes:** `Docs/Specs/Queued/putter_aim_blue_line/NOTES.md` (filed 2026-05-25) — **its architecture sketch is wrong, see §1.**
**Estimate:** 2–4 h Code time + pipeline.

---

## 1. Pre-flight result — **Scenario C**, and the NOTES' sketch does not survive it

NOTES §Pre-flight asked which of three scenarios holds. **Answer: C — but not for the reason NOTES anticipated, and both A and B are impossible rather than merely wrong.**

NOTES told the implementer to read `Assets/Scripts/Gameplay/UI/ShotUI/PutterTrack.cs`. **That file does not exist.** The real file is `PutterTrackGraphic.cs`, same folder, and reading it settles everything:

```csharp
public class PutterTrackGraphic : MaskableGraphic   // ← screen-space Canvas UI
```

`PutterTrackGraphic` is a **`MaskableGraphic`** — a procedural Canvas mesh built in `OnPopulateMesh(VertexHelper)`, in **screen space**. It draws a vertical power lane (width 140 **px**, height 1000 **px**) with a dark centre-to-edge gradient and three horizontal band lines — green `#627352`, amber `#8F7240`, red `#7A3E3E`. It is the **putt power meter**, not an aim line, and it has no world-space existence whatsoever.

Consequences, each of which invalidates part of NOTES:

- ❌ **Scenario A is impossible.** It is not "a straight thin line drowned by the grid." It is a different widget in a different coordinate system, on a Canvas.
- ❌ **Scenario B is impossible as written.** NOTES says *"new child GO `PutterAimLine` under PutterTrack's parent."* `PutterTrackGraphic`'s parent is a **Canvas**. A world-space aim line parented there would be laid out in UI pixels and would not track the ball.
- ❌ **NOTES Q5 is a category error against `PutterTrackGraphic`** — you cannot Z-offset a screen-space `MaskableGraphic` above a world-space mesh. (Q5 *is* still meaningful against the grid; see §4.)
- ⚠️ **Do not touch `PutterTrackGraphic`.** It is the power meter. Changing its colour/width to "make the aim line brighter" would silently restyle putt power feedback. This is the single most likely wrong turn on this task.

**The aim line is new, and it belongs beside the grid, not beside the power meter.**

### Where it goes, and why not `Golfin.Gameplay.UI.ShotUI`

NOTES §Scenario C proposes `PutterAimLineWidget` in `Golfin.Gameplay.UI.ShotUI`. **Put it in `Golfin.Physics.Viewer` instead**, next to `PutterGreenReader`.

The repo already documents why, in a 5-line stub file left as a signpost at `Assets/Scripts/UI/HUD/PuttPathPredictor.cs`:

> `PuttPathPredictor` lives in `Assets/Scripts/Physics/Viewer/PuttPathPredictor.cs` (`Golfin.Physics.Viewer` asmdef) because `Golfin.Physics.Math` (`fp` type) and `Golfin.Gameplay.Input` (`ShotController`) are `autoReferenced:false` and are not accessible from Assembly-CSharp.

The aim line needs the same two things — `ShotController` for state + aim heading, and the ball position. `Golfin.Physics.Viewer.asmdef` already references `Golfin.Gameplay.Input`, `Golfin.Physics.Math`, `Golfin.Gameplay.UI` and `Golfin.Gameplay.Loop`. Placing it there needs **zero asmdef edits**. Placing it in `ShotUI` repeats a problem this repo has already solved once and left a note about.

---

## 2. What to build

`PutterAimLine` — a world-space straight line from the ball along the current aim heading, visible only during putter aim, rendered above the green grid.

**New file:** `Assets/Scripts/Physics/Viewer/PutterAimLine.cs`, namespace `Golfin.Physics.Viewer`.

Mirror `PutterGreenReader`'s shape rather than inventing a second pattern — it is the component this thing lives next to, and its lifecycle is already correct.

---

## 3. Lifecycle — copy `PutterGreenReader` exactly

`PutterGreenReader` (`Assets/Scripts/Physics/Viewer/PutterGreenReader.cs`, 596 lines) already solves this, and Lesson Q (no putter-specific divergence) says match it rather than invent:

| Concern | `PutterGreenReader` | `PutterAimLine` |
|---|---|---|
| Aim source | `[SerializeField] private ShotController _shotController;` (`:45`) | same |
| Subscribe | `_shotController.OnStateChanged += OnShotStateChanged;` in `OnEnable` (`:162`) | same |
| Unsubscribe | `-=` in `OnDisable`, then `_aimActive = false` (`:173-174`) | same |
| Visibility gate | `_aimActive = isPutterAim;` (`:214`) → `_gridMeshGO.SetActive(_aimActive)` (`:218`) | same flag, own child GO |
| Ball position | `_ballPositionOverride ?? <live ball>` (`:232-234`) | **must honour the same override** |
| Per-frame | early-out `if (!_aimActive …) return;` (`:228`) | same |

🔴 **`SetBallPositionOverride(Vector3?)` (`:144`) is not optional.** Visual-gate capture scripts set it when no ball is spawned. If `PutterAimLine` reads only the live ball, it renders at the origin — or not at all — in exactly the captures that are supposed to prove it works, and the task fails its own visual gate. Expose the same public setter with the same priority order.

---

## 4. Rendering — the Z-offset is the one real trap

`PutterGreenReader` lifts its grid off the terrain by `_surfaceYOffset = 0.02f` (`:77`), applied per-vertex as `c.meshY + _surfaceYOffset` (`:442`). That 2 cm exists because of a **z-fight fix shipped in iter-4** — the file's line 1 is literally `// iter-4: _surfaceYOffset z-fight fix (2026-05-25)`. Do not re-litigate it; clear it.

| Setting | Value | Note |
|---|---|---|
| Y offset | **0.04 m** above terrain surface | 2 cm above the grid mesh. Same per-vertex approach; sample terrain height along the line, don't lay a flat quad — the green is not flat, and a flat line will sink into any slope. |
| Width | **0.08 m** world | NOTES Q2 lean, accepted. |
| Length | **15 m fixed** | NOTES Q3 lean, accepted. Cup-aware trimming is future polish, explicitly out of scope. |
| Colour | **`#7AE9FF`** | NOTES Q4 lean. **Provisional — Cesar locks from the first capture.** |
| Shadows | `ShadowCastingMode.Off`, `receiveShadows = false` | Matches `PutterGreenReader:508-509`. |
| Sorting | Above the grid material | The 2 cm depth gap should do it; if the grid material writes depth oddly, bump render queue rather than raising the offset further — a visibly floating line is worse than a sorting tweak. |

Follow the grid's construction pattern: a child GO holding `MeshFilter` + `MeshRenderer` (`:490-509`), `SetActive(_aimActive)`, cleaned up on disable (`:526`).

---

## 5. Aim heading — confirm the convention before writing math

The heading must be the **player's current aim yaw**, not ball→cup.

There is a documented convention elsewhere in the codebase — `MapViewController.AimDirection2D()` (`:1026`), described at `:1313` as `(cos θ, 0, sin θ)`, with `aimYaw = 0 → +X` and increasing θ rotating counterclockwise in XZ. `MapViewController:789-791` carries an explicit warning that using flag direction instead of aim yaw is a bug that has already been hit once.

⚠️ `MapViewController` is in `Golfin.Gameplay.UI.ShotUI`, a different assembly. **Do not copy the formula on faith and do not duplicate it.** Read how `PutterGreenReader` obtains its own heading from `_shotController` and use that same accessor — one source of truth, inside the assembly you are already in. If the two conventions disagree, stop and report rather than picking one.

---

## 6. Out of scope

Per NOTES, unchanged: no aim line for iron/driver (the cone already covers those); no distance tick marks; no putt-strength integration; **no curve prediction** — grid + straight line is the complete feedback set per the `puttpath_predictor` L1 design lock. A curved line is the "Sim positioning" anti-feature and is a hard no.

---

## 7. Definition of done

1. `Assets/Scripts/Physics/Viewer/PutterAimLine.cs` exists; **zero asmdef edits**; **`PutterTrackGraphic.cs` untouched** (verify by diff).
2. Line appears on entering putter aim, hides on shot start and on leaving putter mode.
3. Line stays anchored to ball + aim heading while the camera rotates.
4. No z-fight with the grid or terrain on a sloped green.
5. `SetBallPositionOverride` honoured — captures work with no live ball.
6. EditMode tests still green at the current baseline; report the number, don't assert a target.

**Visual gate (Cesar):** production Hole 1 putter aim — line clearly readable over the grid, anchored through a full camera rotation, hidden after the putt. Plus the bot video `PutterAimWarpedGridOnTestGreen` extended to show it.

**Open for Cesar at gate time:** colour `#7AE9FF` and width `0.08 m` are eyeballed from a reference screenshot, not locked. Expect one tuning round.

---

## 8. Mobile performance budget (Rev 2)

Context: `ui_frame_pacing` (K9) shipped — the game now runs 60 fps on the physical iPhone. Anything living in putter-aim `Update()` runs every frame of the longest-held state in the game. The line itself is tiny; the budget below is about making its *steady-state cost zero*, the same way `PutterGreenReader` already behaves.

### 8.1 Rebuild-on-dirty, not per-frame

Cache last-built `(ballPos, aimYaw)`. Rebuild the mesh only when yaw moved > ~0.05° or ball moved > 1 cm; otherwise `Update()` is the existing `if (!_aimActive) return;` early-out plus two comparisons. While the player is *holding* an aim, cost is ~zero. (The grid gets this for free because its warp happens in the shader; the line's geometry depends on aim yaw, so the dirty check is where that saving lives.)

### 8.2 Zero steady-state allocations

- One `Mesh` created on first build, `MarkDynamic()`, reused for life; never `new Mesh` per rebuild.
- Pre-allocated `Vector3[]` / `List<Vector3>` at fixed capacity (§8.3); refill + `SetVertices`/`SetUVs`; no LINQ, no `ToArray()` per rebuild.
- Triangles + UVs set once (topology never changes); only vertex positions refresh on rebuild.
- Gate: zero GC alloc per frame in putter aim after the first build (Profiler window on the aim state is sufficient evidence; a device capture is a bonus, not required).

### 8.3 Geometry + draw cost

15 m line sampled every 0.5 m (same pitch as the slope bake) = 31 samples → 62 verts / 60 tris / **one draw call**. Unlit URP shader (or the simplest transparent unlit variant of the grid's material setup), `ShadowCastingMode.Off`, `receiveShadows = false`, per-instance params via `MaterialPropertyBlock` — the exact pattern `PutterGreenReader` iter-3 established. Overdraw from a 0.08 m strip is negligible.

### 8.4 Terrain height source — bake, not raycasts

Sample line-vertex Y from the **same baked slope data the grid uses** (`BakedZoneClassifier` 0.5 m bake / `SlopeCell.meshY`), not `Physics.Raycast` per vertex per rebuild. Two reasons:

1. **Perf:** raycasts × 31 × every aim-drag frame is the only way this feature could ever show up in a profile. Bake lookups are array reads.
2. **Correctness (Rejection-3 lesson):** if the line and grid sample Y differently, the 2 cm gap between them is not guaranteed everywhere and the z-fight defense from `puttpath_predictor` iter-4 silently degrades. Same source ⇒ the gap is real at every vertex by construction.

Off-bake fallback (line extends past the green polygon): clamp to last baked sample's Y, or a single raycast per rebuild for the overhang tail — implementer's choice, NOTE it in the report.

Why not `LineRenderer`: camera-facing billboarding recomputes every frame whether or not aim changed, gives no control over per-vertex Y sampling against the bake, and would introduce a second line-drawing pattern beside the repo's established procedural-mesh + MPB one. Consistency wins (Lesson Q).

### DoD additions (extend §7)

7. Zero per-frame GC allocations in putter aim after first mesh build; mesh rebuilt only on aim/ball change (dirty check).
8. One draw call for the line; unlit, no shadows; height sampled from the slope bake per §8.4.

---

## 9. What other games do (Rev 2 — competitive check)

Surveyed before locking the plan. Result: **the Rev 1 design (straight intent line + separate slope grid) is the genre standard; no design change.**

| Game | Putt-aim visual | Break shown how |
|---|---|---|
| Winning Putt (Cesar's original ref) | thin straight cyan line over grid | grid |
| Golf Clash | straight "path of light" from ball + needle timing | separate break arrows / hole glow, not bent into the line |
| PGA Tour 2K (2K23) | straight target/distance markers | grid with moving beads (our grid's own reference); ALSO has a curved putt-preview line — but even 2K treats it as a limited-use assist (once per stroke, difficulty-gated), i.e. the industry's own flag that live curve prediction removes the skill. This is exactly the "Sim positioning" anti-feature the `puttpath_predictor` L1 lock already excludes. |
| WGT Golf | aim marker | grid + moving dots (Ken's original grid reference) |
| Ultimate Golf | aim arc showing intent — explicitly does NOT fold wind into the arc | player compensates |
| Mini Golf King | straight guideline; longer guideline is a *paid/boost* aid | — |

Takeaways applied: (a) straight-line-plus-grid is what every comp title ships as the default read — the L1 "no curve prediction" lock is not just our preference, it is the norm, and where curve exists it is monetised/limited, which is a live-ops option for later, not a rendering feature now; (b) every comp title renders the aim line *uniformly bright, full length* — no fade-out gradient needed for v1; (c) Winning Putt's per-meter tick marks and Golf Clash's hole-glow-on-line remain the two obvious future-polish items, both already out of scope per §6.

