# FINDINGS — `zone_bake_completeness`

**Raised:** 2026-07-28, Architect, after Cesar challenged the `surface_coverage_audit` conclusion.
**Status:** **CONFIRMED 2026-07-28 by read-only probe.** §5 middle branch. Mechanism (§4) still hypothesis-ranked, not proven.

---

## 0. What triggered this

I claimed "Hole 14 has no fairway polygons." Cesar replied that he can see `Fairway_1` in Unity. **He was right and my framing was wrong** — I made a claim about the hole after looking at only one of two artifacts. Correcting that opened a real defect.

---

## 1. Two artifacts, not one

| Artifact | Hole 14 contents | Read by |
|---|---|---|
| Build scene `Generated/Hole_14_Geo.unity` | `Fairways`/`Fairway_1`, `Greens`/`Green_1`, 15 `MeshCollider`s | rendering |
| `Assets/Resources/HoleData/lomond-country-club/Hole_14/zones.json` | **only** Tee, Sand, CartPath, Water | `BakedZoneClassifier` (via `PhysicsLabController.cs:1458`) |

The visual and the physics data **disagree**. The scene has fairway and green; the classifier's data has neither.

**Not a stale-bake gradient:** all 18 runtime `zones.json` were baked in one commit — `b44c22bc0`, 2026-07-27, `multi_club_architecture_refactor`.

### 1.1 The "duplicate name" is a false alarm — separate from the real duplication

`grep m_Name: Fairway` returning 3 hits on Hole 14 is **not** duplication:

| Line | Name | Unity type |
|---|---|---|
| 241817 | `Fairway_1` | `!u!43` **Mesh** |
| 1588687 | `Fairway_1` | `!u!1` **GameObject** |
| 367583 | `Green_1` | `!u!1` **GameObject** |
| 954410 | `Green_1` | `!u!43` **Mesh** |

One GameObject + its embedded procedural mesh sharing a name. Normal.

---

## 2. The real duplication — two parallel `SurfaceMarker` systems

Both are attached to **every** zone mesh, in both importers, by design:

| Class | Field | Refs in Hole 14 scene |
|---|---|---|
| `Golfin.Course.SurfaceMarker` (`Assets/Scripts/Course/SurfaceMarker.cs`) | `surfaceType` | 15 |
| `Golfin.Physics.Runtime.SurfaceMarker` (`Assets/Scripts/Physics/Runtime/SurfaceMarker.cs`) | `Type` | 15 |

`HoleGeoImporter.cs` (the **live** importer) contains **27** `SurfaceMarker` references, stamping both at every site — e.g. `:3113-3115`, `:3230-3232`, `:3427-3429`. `HoleLiteImporter.cs` (deprecated) does the same.

### 2.1 The two enums diverge from index 2

```
Golfin.Course.SurfaceType     Golfin.Physics.SurfaceType (byte)
0 Fairway                     0 Fairway   <- default for unmarked terrain
1 Green                       1 Green
2 SemiRough                   2 GreenCollar     X
3 Rough                       3 Semirough       X
4 Bunker                      4 Rough           X
5 Water                       5 Tee             X
6 Tee                         6 Sand            X
7 CartPath                    7 BunkerLip       X
8 Fringe                      8 CartPath        X
                              9 Water
                             10 OOB
```

Unity serialises enums as integers. These are bridged by hand via `SurfaceMarkerMap.MapCourseToPhysics`, which on an unrecognised value **logs a warning and defaults to `Fairway`** (`SurfaceMarkerMap.cs:24`). `SurfaceCoverageAudit.cs:32-34` independently flagged a related mismatch (`MapCourseToPhysics(8)` returns `GreenCollar`).

**Assessment:** only `Golfin.Physics.Runtime.SurfaceMarker` is consumed by the bake (§3). `Golfin.Course.SurfaceMarker` looks vestigial — 15 redundant components per hole across 18 holes, plus a hand-maintained mapping between two enums that must stay in lockstep across ~27 stamp sites in the live importer alone. Every stamp site is an opportunity to set one correctly and the other wrong, silently.

*(Vestigial-ness is inferred from "nothing in the bake path reads it," not from an exhaustive reference search. Confirm before deleting.)*

---

## 3. How the bake builds `zones.json`

`Assets/Scripts/Editor/CourseImporter/BakeZoneJsonTool.cs`:

- `:94-95` — skips `Video/` scenes, requires `_Geo.unity`. So Hole 14 baked from `Generated/Hole_14_Geo.unity`, the same scene that visibly has fairway/green.
- `:175` — object must have **both** the Physics `SurfaceMarker` **and** a `MeshFilter`
- `:182` — `var marker = t.GetComponent<SurfaceMarker>();`
- `:186` — `SurfaceType type = marker.Type;` (group key)
- `:142` — `foreach (var kv in groups) data.zones.Add(kv.Value);`

A surface type reaches `zones.json` **only** if at least one mesh survives the whole chain.

---

## 4. Ranked hypotheses for the drop

**H1 — silent boundary-loop rejection.** `:278` and `:284` both read `if (loopVerts.Count < 3) continue;`. Outline extraction yielding <3 vertices drops the mesh **with no warning**. If every Fairway mesh on Hole 14 fails extraction, the Fairway group is never created and `zones.json` is written without it — no error, valid-looking file. **Best fit:** explains total absence rather than partial loss, and explains per-hole variation. *Evidence: code path read. Not yet observed firing.*

**H2 — missing `MeshFilter`.** `:175` requires one. If Hole 14's fairway renders via a different component arrangement, it is skipped at the gate. *Discriminator: inspect `Fairway_1`'s components in the scene.*

**H3 — marker set to the wrong type.** Given the diverging enums in §2.1 and the default-to-`Fairway` fallback, a mis-stamped marker could fold Fairway meshes into another group. *Weaker: produces mis-typing, not disappearance — and Hole 14 lost two distinct types.*

**Not yet ruled out:** that the missing entries are harmless because some path I have not found feeds scene meshes into runtime surface resolution. §5 settles this.

---

## 5. Probe — RUN, CONFIRMED (2026-07-28, read-only, wrote nothing)

| Mesh | World (x, z) | `Classify` returns | Provenance |
|---|---|---|---|
| `Greens/Green_1` | (−111.506, 127.607) | **`Fairway`** | **`Default`** (fallthrough) |
| `Fairways/Fairway_1` | (−42.815, 62.402) | **`Fairway`** | **`Default`** (fallthrough) |

Both centroids verified inside the classifier's XZ frame — scene-mesh world space and classifier world space are the same frame, no alignment caveat. Both matched **no polygon of any type** and **no OB mask**, falling through to `DefaultSurface = Fairway`.

**Concavity caveat is moot:** `Provenance = Default` means nothing matched, so any interior point on those meshes returns the same answer. No secondary sample needed.

**Outcome: §5 middle branch. My model of the classifier was correct; the bug is real.**

### 5.1 Severity — what the greens actually play like

`SurfaceConfig.Default` (`Assets/Scripts/Physics/Core/SurfaceConfig.cs`):

| Coefficient | `Green` (intended) | `Fairway` (actual) | Effect on a putt |
|---|---|---|---|
| `RollingResistance` | 0.12 | **0.18** | decelerates **50% faster** |
| `StopSpeed` | 0.05 | **0.10** | halts at **2×** the speed threshold |
| `TangentFriction` | 0.75 | 0.55 | — |
| `Restitution` | 0.40 | 0.50 | — |

Putts on holes 02 / 12 / 14 come up **short and stop abruptly** — a ball that should trickle into the cup dies near it while still visibly moving.

**Scope of direct confirmation:** the probe covered **Hole 14 only**. Holes 02 and 12 are inferred from the same data signature (Fairway present, Green absent in their `zones.json`) and have **not** been probed. Hole 15 (Fairway absent, Green present) is likewise inferred.

---

## 6. Consequences — now CONFIRMED, not conditional

1. **A live gameplay bug**, independent of any pending decision: greens on holes 02 / 12 / 14 classify as `Fairway`.
2. **The cheap path in `surface_classification_ob_rough` is dead.** `DefaultSurface = Rough` would turn Hole 14's fairway *and* green to rough, Hole 15's fairway to rough, and holes 02/12's greens to rough. It only looked safe because missing data was hiding behind a fallback that happened to be `Fairway`.
3. **Option 2 gains a second justification** — a per-cell surface grid baked from the source raster bypasses polygonisation entirely and would fix the missing zones as a side effect. The source raster has fairway and green on all 18 holes (Hole 14: 257,120 fairway / 20,208 green cells).
4. **The bake needs a completeness gate.** Any hole whose source raster contains a surface type that does not survive into `zones.json` should fail the bake loudly rather than write a valid-looking file.

---

## 7. Corrections to my own earlier work, for the record

- **`surface_coverage_audit` SPEC §3.2 was structurally invalid** — it sourced authored intent from the terrain alphamap, which `HoleGeoImporter.ZoneToLayer` (`:1614-1630`) collapses to rough/semirough. Fairway cannot appear there. My error; the red-team caught it correctly.
- **The escalation's proposed fix location was also wrong** — the pre-collapse raster is *not* in the runtime `zones.json` (keys: `holeId`, `zones`, `obMask` only). It lives in `Tools/UHoleGeo/output/.../export/hole-NN/zones.json`, a different tree with a different schema.
- **The escalation's claim that `terrain_grid` under-reports fairway is false** — on Hole 6, `grid` and `terrain_grid` have identical fairway (38,265) and green (43,951) counts; they differ only in overlay features.
- **My first presence/absence script had a missing `z.get('type')` fallback** and flagged all 18 holes as broken. False positives from my own bug, caught and rerun. The corrected result is 4 holes: 02, 12, 14, 15.
