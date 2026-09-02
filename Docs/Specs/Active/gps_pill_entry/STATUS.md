SPEC_READY

# STATUS — `gps_pill_entry`

**Current:** `SPEC_READY` — art baked and verified; the Unity pass is waiting on a free Editor.

**Opened:** 2026-09-02 (Cesar: move the GPS entry point to the Figma pill, restore the banner)
**Amends:** `punch_it_gps_variants` (Completed)

## Done without Unity

- Node `14060:4638` pulled with `get_design_context`; frame `2098:8490` and a 4× pill export
  saved to `reference/`.
- `Docs/Scripts/make_gps_pill.py` → `Assets/Art/HomeScreen/S_GpsPill.png` (276×184, 2× of
  138×92), verified by sampling: gold `#FCF195`, 1px inner rule, `#133453`→`#091B33`.
- SPEC written with the per-element Figma fidelity table (Rule 18).

## Waiting on a free Editor (another session is driving Unity)

1. Import `S_GpsPill.png` as a Sprite (default texture import = white box).
2. Build the `GpsPill` object on Home, wire the Button + `ButtonPressFeedback` → `GpsHub`.
3. `HOME_GPS_PILL` into the CSV, re-import the table, publish.
4. Revert the `BannerSlotBinder` gated-route hide **in the same change as the pill** — alone it
   would ship a visible-but-dead banner in "punch it" builds.
5. Verify both variants, crop-diff the pill against the node, EditMode + lint.

## History

| Date | State | Note |
|---|---|---|
| 2026-09-02 | `SPEC_READY` | Design pulled, sprite baked and numerically verified, spec written. No Unity touched — another session holds the Editor. |
