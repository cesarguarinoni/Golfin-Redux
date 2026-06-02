DONE

SHIPPED 2026-06-02 (commit 8a825477, pushed to main). Cesar approved the carve-inset approach and
said "ship it" after reviewing H10+H18 orbit videos (normal + slope-grid). Task complete.

  - HoleGeoImporter.cs: carve-inset = DilateContour(activeContour, GreenCollarWidth − GreenCarveInset),
    GreenCarveInset=0.20m, gated to terrain-bordered greens only (fairway weld untouched). Apron path
    (CreateGreenTerrainApron + GreenTerrainApronWidth) DELETED. Diff +72/−3 vs HEAD (was +215 apron).
  - H10 + H18 reimported via production code: apron GONE (0 GreenApron refs in scenes), green interior
    fully carved (0 verts over solid terrain), poke-through ≤2.7mm(H10)/0(H18), clean collar→rough seam
    at grazing (no teeth, no band). Captures: screenshots/spike_apron/carveinset_h{10,18}_*.png.
  - EditMode tests: 359 pass / 0 fail / 3 skip (baseline-identical; 3 skips pre-existing HoleComplete C1).
  - reimport_report.txt: terrain-carve boundaryWidth=0.70m carveInset=0.20m (no apron), isTerrainBordered=True.

NOT YET COMMITTED. On Cesar "Done"/"ship": commit importer + H10/H18 Geo scenes/terrain (scoped), push,
move task folder to Completed/. If Cesar wants a tweak (e.g. Δ=0.15 → literally 0 poke-through), re-shoot.

--- prior history (apron path, rejected) ---
CARVE-INSET spike result (2026-06-02, supersedes apron-material path — Cesar killed the apron):
Cesar's idea — drop the apron, inset the terrain carve so the raster teeth tuck UNDER the collar drape — WORKS on H10 (worst case). Findings: SPIKE_FINDINGS_CARVE_INSET.md.

  - Poke-through probe (read-only, existing collar mesh): at inset Δ=0.20m the exposed band has
    ~zero poke-through (1 vert, 2.7mm); global max under collar = 1.76cm vs 16.9cm for no-carve.
  - Reimported H10 with Δ=0.20 + apron dropped: apron GONE, green interior fully carved (0/1563
    green verts over solid terrain), clean collar→rough seam at grazing — NO teeth, NO band, NO
    poke-through. Captures: screenshots/spike_apron/carveinset_h10_*.png.
  - H18 not yet reimported but geometrically strictly safer (0.075m vs 0.19m intrusion).

SCRATCH STATE: HoleGeoImporter.cs has uncommitted SPIKE2 edits (consts SpikeCarveInset=0.20/
SpikeDropApron + inset carveContour + apron gated off). H10 Geo scene/terrain reflect the inset
result — open Hole_10_Geo to inspect live. NOTHING committed.

DECISION PENDING CESAR: (a) approve → promote to real spec (default inset for terrain-bordered greens,
remove SPIKE gating, delete apron path) + confirm H18; or (b) reject → revert scratch + reimport H10
to restore the apron baseline. Spike rule honored: no production commit, no close-out.
