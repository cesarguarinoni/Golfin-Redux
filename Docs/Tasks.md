# Current Priorities (Updated 2026-04-08)

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
