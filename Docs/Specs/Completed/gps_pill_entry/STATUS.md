DONE

# STATUS — `gps_pill_entry`

**Current:** `DONE` — built, verified both ways, shipped in `e2db982c0`; approved by Cesar
2026-09-02 and closed as the first commit of `gps_polish`.

**Opened:** 2026-09-02 (Cesar: move the GPS entry point to the Figma pill, restore the banner)
**Amends:** `punch_it_gps_variants` (Completed)

## Done without Unity

- Node `14060:4638` pulled with `get_design_context`; frame `2098:8490` and a 4× pill export
  saved to `reference/`.
- `Docs/Scripts/make_gps_pill.py` → `Assets/Art/HomeScreen/S_GpsPill.png` (276×184, 2× of
  138×92), verified by sampling: gold `#FCF195`, 1px inner rule, `#133453`→`#091B33`.
- SPEC written with the per-element Figma fidelity table (Rule 18).

## Built and verified (2026-09-02)

All five steps done in one change (`e2db982c0`):

1. `S_GpsPill.png` imported as a Sprite, PPU 200 → native 138×92.
2. `GpsPill` built on Home at the node's (1008, 251), 138×92, `Image.Type.Simple`, Button +
   `ButtonPressFeedback` → `GpsHub`. Measured built rect x 1008..1146, y 251..343.
3. `HOME_GPS_PILL` in the CSV, table re-imported, **seeded to the published catalog** (905 rows);
   content `--check` clean.
4. `BannerSlotBinder` gated-route hide reverted in the SAME commit as the pill.
5. Both variants verified in play mode; pill crop-diffed against the node.

**GPS ON:** pill visible, label "GPS" at 45.0, tap reaches GpsHub, banner visible.
**GPS OFF:** pill hidden (`activeSelf=false`), five GPS screens still refuse, banner enabled +
raycasting + interactable. EditMode 2239 / 2236 passed / 0 failed.

### Two things worth carrying forward

- **The podium collided with the pill** — 75×75 fully inside the pill's box. The Figma frame has
  no leaderboard element, so the design never accounted for it. Found by crop-diffing, not by
  reading numbers. Cesar: move it left; it now mirrors to x 48..123 (pivot mirrored too, or it
  hung off-screen at x −27..48).
- **The GPS-off check needed the PROFILE switched, not just the const flipped.** The earlier
  batchmode GPS build had left the Editor on `iOS-Full-GPS`, whose profile-level `GOLFIN_GPS`
  kept the gate on — the first "off" run was a false pass until `Enabled=False` was confirmed in
  the loaded assembly.

## Known deviation — typography, measured

The node's glyph ink for "GPS" is 84.5px wide, 31.1px cap height. At the node's 45pt our Rubik
SDF gives cap height 31.5 (match) but width 99.1 (~17% wide). It is the font asset, not the
setup: the shipped daily-mission pill's label renders "GPS" at 45pt as 98.4 with the same font
and material. Font SIZE was kept (cap height is what reads as "same size text"), and the label
rect is inset 12 rather than the node's 24 so 45pt fits. Capsule fidelity is otherwise
essentially exact — comparing only the node's opaque pixels, **median |ΔRGB| = 1**, deltas
confined to the glyph rows.

**Cesar's call if he wants the other trade:** constrain the label to the node's 24px padding and
the text renders at 40.9 with width 89.3 — closer to Figma's ink, but letters smaller than the
design. One line.

## History

| Date | State | Note |
|---|---|---|
| 2026-09-02 | `SPEC_READY` | Design pulled, sprite baked and numerically verified, spec written. No Unity touched — another session holds the Editor. |
| 2026-09-02 | `READY_FOR_SELF_REVIEW` | Built and shipped in `e2db982c0`. Podium moved left per Cesar after the crop-diff exposed a 100% overlap. One measured deviation: glyph width, inherent to the project's Rubik SDF. |
| 2026-09-02 | `DONE` | Approved by Cesar. Folder moved to `Docs/Specs/Completed/`; closed as the first commit of `gps_polish`. |
