# iter-14 diagnostic — fairway breaking around the green

**Date:** 2026-05-31 · **Committed state captured:** iter-13 (ridge fix), HEAD `102b994d` · **Mode:** read-only capture (no bake / reimport / edit; scenes never saved).

## Method
- Scenes: shipping Geo (`Assets/Golf/Courses/lomond-country-club/Generated/Hole_NN_Geo.unity`). NOT the deprecated Lite/`Hole_NN.unity`.
- Capture path note: `screenshot-isolated` (isolated=false) renders the full scene but **streams base64 only** (no named PNG) and offers only 6 fixed axis views — it cannot express an arbitrary grazing angle. To produce real files at ≥1100 px **and** controllable grazing obliques, shots were rendered through a temporary scene camera using the supported Unity-6/URP off-screen API `RenderPipeline.SubmitRenderRequest` (legacy `cam.Render()` returns black under URP). Full scene visible (all meshes, scene lighting + skybox) = isolated=false-equivalent. Temp camera created + `DestroyImmediate`'d each run; no scene saved (H07 reopened clean afterwards).
- Approach geometry per hole measured by raycasting a height profile from the fairway up across the green's leading edge.

## Objective approach-lip ranking (all 18 holes)
`lip = green-plateau-centre Y − fairway-toe Y` along the approach direction. Matches Cesar's qualitative read (H5 flattest, H9 most contoured), so the metric is trustworthy.

| rank | hole | lip (m) | approach fairway |
|---|---|---|---|
| 1 | **H9** | 1.32 | Fairway_1 |
| 2 | **H14** | 1.28 | Fairway_1 |
| 3 | H6 | 1.15 | Fairway_1 |
| 4 | **H7** | 1.14 | **Fairway_2** |
| 5 | H12 | 1.11 | Fairway_1 |
| … | … | … | … |
| 7 | **H18** | 0.81 | **Fairway_2** |
| 17 | H5 | 0.25 | Fairway_1 |
| 18 | H11 | −0.17 | Fairway_1 |

Two steepest = **H9, H14** (captured as the comparison set). H18 added as a cheap hypothesis-test (also a Fairway_2 approach, like H7).

## H7 — the defect (Green_1 ↔ Fairway_2 junction, leading edge faces West / low-X)
Height profile at Z=−30.4, X 156→172: fairway ramps smoothly 27.48→28.10, then **Green_1 takes over at X≈166 and rises steeply to the 28.81 plateau by X167** — the green sits on a raised pad whose leading edge is a steep ~0.55–0.7 m grass **lip/wall**. RaycastAll shows Fairway_2 (~28.0) stacked over base TerrainRoot (~27.5) with **no green mesh below X165.5** → the fairway and green meshes **butt at the boundary rather than sharing it**.

**Classification: (b) overlap / z-fight at an abrupt, no-collar-blend lip.** NOT (a) see-through gap — every profile raycast hit a solid top surface (no NO-HIT). NOT primarily (c) crease — the fairway ramp itself is smooth.

What each H7 shot shows:
| file | angle | what it shows |
|---|---|---|
| `h07_overhead.png` | ortho top-down (size 24) | green + collar ring + fairway stripes entering from W/SW; from straight down the seam is hidden (this is why overhead alone never caught it). |
| `h07_graze_w_low.png` | ~2° (near-horizontal, W→E) | golfer's-eye up the approach; leading edge reads as a low dark band — seam present but subtle at this flat angle. |
| `h07_graze_w_15.png` | ~15° elevated oblique, W | **money shot.** Green sits on a raised pad; along the leading edge a row of **small bright angular slivers + dark notches** breaks the toe line. |
| `h07_graze_sw.png` / `h07_graze_nw.png` | ~5–7° obliques, SW & NW | same broken/notched toe seen along the S and N portions of the leading arc — defect runs along the perimeter, worst on the W/SW leading edge. |
| `h07_zoom_lip15.png` | tight ~15°, W-centre lip | bright triangular **mesh flaps poke out of the seam** at the base of the lip, plus a hard dark seam line. |
| `h07_zoom_liplow.png` | tight near-horizontal | confirms the leading edge is a near-vertical grass wall. |
| `h07_zoom_sliver.png` | fov-13 magnified | unambiguous: a thin **bright mesh sliver protrudes** through the seam at the toe → fairway/green-pad meshes interpenetrate. |

**Apparent vertical sizes (H7):** pad lip wall ≈ 0.55–0.7 m; local mesh step at the actual boundary ≈ 0.16 m; protruding overlap slivers ≈ 0.1–0.3 m. Worst on the **W / SW leading edge**, best exposed at **~15° elevated oblique** and the magnified lip zoom.

## Comparison holes — is it hole-general or H7-specific?
| hole | lip | junction in zoom_lip15 | slivers? |
|---|---|---|---|
| **H9** (steepest, Fairway_1) | 1.32 | clean collar → fairway slope | **none** |
| **H14** (2nd steepest, Fairway_1) | 1.28 | faint seam line only | **none** |
| **H18** (Fairway_2, like H7) | 0.81 | smooth collar → fairway | **none** |

The two **steepest** approaches (H9, H14) are clean, and **H18** — which shares H7's Fairway_2 approach — is also clean. So the defect tracks **neither approach steepness nor fairway-segment index**.

## Conclusion (for the fix spec)
The overlap/sliver seam reads as **H7-specific** (a localized bad mesh boundary where Green_1's pad meets Fairway_2 on the W/SW leading edge), not a hole-general consequence of steep approaches. The fix should target H7's fairway↔green-pad mesh join (shared boundary / collar blend / overlap trim) rather than a global steep-approach treatment. Worth a quick re-check of the other clearly-steep holes (H6, H12) before committing to "H7-only," but on this evidence a hole-general fix is not warranted.
