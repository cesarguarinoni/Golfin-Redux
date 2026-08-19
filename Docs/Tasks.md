# Current Priorities (Updated 2026-04-08)

## ⏰ Time-boxed — beta week (2026-08-24 → 2026-08-31)

- [ ] **Telemetry panel §5.6 live smoke.** Compare the numbers on
  https://admin.golfin.world/telemetry against hand-run SQL for the same date
  range. The queries are pre-written, all five sections, in
  `Docs/Specs/Completed/telemetry_admin_panel/live_smoke_5.6.sql` — set the
  twelve date literals to the range the panel is showing and run them in the
  Supabase SQL editor.
  **Why it matters:** the panel aggregates in TypeScript
  (`Tools/admin-dashboard/lib/telemetryData.ts`). This is the only check that
  catches a wrong date boundary or a miscounted `distinct session_id` — the
  mock fixture cannot, because fixture and panel would be wrong together and
  agree all the way to the wrong answer. Everything else in that spec passed;
  this one waited on real rows.
  **Do it early in the week**, not at the end: if the panel is misreporting,
  you want to know while there is still beta left to re-measure.
  A scheduled reminder also fires 2026-08-26 10:00 JST
  (`~/.claude/scheduled-tasks/telemetry-panel-live-smoke-5-6/`).


1. ~~Tee Areas~~ ✅ Splatmap texture + FBX tee markers (2 per tee)
2. ~~Flag/hole marker~~ ✅ Flag.fbx + black circle at green centroid. Future: green editor in UHole Lite GUI for pin placement.
3. **Texture & lighting cleanup** — IN PROGRESS
   - ✅ Plastic sheen fixed (mask map with A=0 smoothness)
   - ✅ Fairway/fringe textures swapped (T_Fringe_Albedo = fairway, T_Fairway_Dark = fringe)
   - ✅ Fairway fringe ring added (semi-rough border via dilation)
   - ✅ Blur removed (was causing fairway bleed)
   - ✅ Alphamap resolution bumped 256 → 1024
   - ✅ Zone grid resolution bumped to ~2048 (lanczos3 upscale before classification)
   - ✅ PNG + SVG zone import in Hole Viewer
   - ✅ Morphological close in classify-zones.mjs
   - **TODO: Mow stripes** — alternate light/dark fairway textures
4. **Geometry extraction for UHole Lite** — pull real elevation data to inform slopes and big terrain shifts
