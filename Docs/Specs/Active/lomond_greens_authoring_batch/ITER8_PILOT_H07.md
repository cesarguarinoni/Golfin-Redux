# ITER-8 PILOT — H07 PDF-frame readable gate + direction re-author

**STATUS:** SPEC_READY (pilot — H07 only)
**PARENT:** `Docs/Specs/Active/lomond_greens_authoring_batch/SPEC.md`
**WORKFLOW TIER:** FULL PIPELINE
**WRITTEN:** 2026-05-28 (time TBD — set HH:MM JST at kickoff)

---

## Why iter-8 (what iter-7 got wrong)

The visual gate never compared the authored slope to the PDF. Confirmed in `Assets/Scripts/Editor/GreenAuthoring/LomondVisualGate.cs`:

- `ComputeHoleResult` (lines ~1262–1300) averages the **whole green to one mean vector**, then cosines it against one hand-typed number per hole in `DominantPdfDirs` (lines ~279–298). It is mathematically incapable of detecting tier structure or per-region direction. "16/18 PASS" only means "average slope ≈ the typed number."
- `DominantPdfDirs` is hand-entered, NOT read from the PDF. The PDF thumbnail is eyeball-only and never enters any computation.
- The RED(grid) vs BLUE(YAML) dual-arrow overlay only proves grid ≈ YAML — two copies of the same interpretation.
- `RegionCount`/`RidgePresent` IS computed (variance > 0.5 → 2, line ~1309) but written to CSV only; it never gates.

Net: per-region directions are unverified on all 18. Even H07 (the one with visible tier structure) has wrong directions — its hard-coded regions **diverge** (`DirX=-0.84` left / `+0.84` right, both `DirZ=+0.54`) while the PDF shows both tiers draining toward the front (left ↓, right ↘, ~40° apart, not ~120° divergent). The inter-tier angle is rotation-invariant, so this is a genuine direction error, not a frame-rotation artifact.

## The fix, in one sentence

The gate renders in the **PDF panel's own frame** with the authored arrows overlaid directly on the PDF, so correctness is read at a glance — and the cosine is computed per-region against the PDF's own arrows in that same frame.

## Pilot scope

**H07 only.** Prove the readable artifact + correct directions + the panel↔world transform. On Cesar approval, the identical method rolls to the other 17 as a batch.

---

## Deliverable 1 — the readable gate artifact (the point of this iter)

One composite PNG, two panels, **identical crop + scale + orientation = the PDF panel frame**. No world-XZ North-up frame anywhere.

- **LEFT:** the H07 PDF green panel crop, untouched — reference, printed black arrows visible.
- **RIGHT:** the same crop, dimmed ~40%, with the authored slope arrows overlaid as bright magenta at the grid sample points, transformed world→panel. Ridge polyline drawn as a magenta dashed line.
- Thin caption strip: per-region cosine + `regionCount` + `ridgePresent`.

Eyeball test: do the magenta arrows (right) sit on and point the same way as the black arrows (left), tier-for-tier? Ridge match the dashed line?

Save to `tasks/lomond_greens_authoring_batch/Hole_07/pilot_gate.png` via `CaptureCore` (sanctioned path only).

## Deliverable 2 — the panel↔world transform

Establish a per-hole similarity transform `T` (rotation + uniform scale + translation) between the PDF panel frame and world-XZ, from 3–4 correspondence points shared by BOTH the `greens.json` polygon (world) and the PDF panel: polygon long-axis endpoints, a bunker notch, a distinctive lobe. Store as `editorBackdrop` correspondence points in `green.json` (the Phase 2 Q3 mechanism — runtime ignores unknown fields). `T` is used to: read PDF arrows → world (`T⁻¹`) for authoring, and sample world slope → panel (`T`) for the overlay. A wrong `T` shows up as misaligned arrows in the overlay, so the gate is now sensitive to it.

## Deliverable 3 — re-authored H07 green.json

Read H07's printed arrows in the panel (ground truth, panel frame):
- Left/upper tier: ↓ (drains toward front).
- Right tier: ↘.
- Far-right edge: one ↙ back inward toward the ridge.
- Ridge: dashed, flat-Z, center of green, runs upper-right → lower-left. 2 regions.

Author `green.json` (world-XZ, schema v1) so that sampling each region and applying `T` reproduces those panel-frame directions. Two regions + ridge polyline matching the dashed line. **Do not hand-type a dominant direction** — derive region slopes from the PDF arrow reading through `T⁻¹`.

---

## Acceptance (H07 pilot)

1. **Visual:** overlaid magenta arrows align with the PDF's printed black arrows per tier (direction within eyeball tolerance); the L/R 2-tier split + ridge match the dashed line; readable at a glance. Cesar gate.
2. **Numeric:** per-arrow cosine in the **panel frame** ≥ 0.85 against the PDF-read directions (now meaningful — same frame, reference is the actual PDF reading).
3. **Structure gates:** `regionCount == 2`, `ridgePresent == true`, and these must **block** status (a known-tier hole reporting <2 regions fails). Replace the average-vs-typed cosine in `ComputeHoleResult`.
4. No scene mutation; `CaptureCore` only; `GreenTopologyEditor` drives authoring.

On approval → roll identical method to H01–H06, H08–H18 as the batch (tier holes H03/H11/H18 get the same 2-region treatment; genuinely flat holes like H05 stay single-region but still verified in-frame).

---

## Kickoff (paste into Claude Code)

```
Use the golfin-implementer subagent on "lomond_greens_authoring_batch" — iter-8 pilot, H07 only, per Docs/Specs/Active/lomond_greens_authoring_batch/ITER8_PILOT_H07.md
```
