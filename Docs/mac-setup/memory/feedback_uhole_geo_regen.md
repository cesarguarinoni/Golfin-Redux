---
name: Always remind user to regenerate UHole Geo heightmap after terrain script changes
description: When modifying Tools/UHoleGeo/scripts/generate-terrain.mjs, always instruct the user to run the UHole Geo regeneration workflow (not just Unity import) so the heightmap.raw is rebuilt.
type: feedback
originSessionId: a9cb905a-a370-40b9-8285-3c38e8f47e00
---
After modifying `Tools/UHoleGeo/scripts/generate-terrain.mjs`, always include an explicit step telling Cesar to regenerate the heightmap via the UHole Geo tool before doing the Unity import. Running the node script from bash is fine for verification, but Cesar's normal workflow is through the UHole Geo GUI.

**Why:** Cesar pointed out I forgot this step on 2026-04-17 after I shipped the ravine-carving change. Running `node generate-terrain.mjs` from bash DID produce a new heightmap.raw, but Cesar's workflow uses the UHole Geo regenerate button, and the import-into-Unity step is useless until the heightmap is regenerated there.

**How to apply:** After any edit to the terrain generation script, my next-steps bullet list must include "Regenerate in UHole Geo" BEFORE "Import in Unity."
