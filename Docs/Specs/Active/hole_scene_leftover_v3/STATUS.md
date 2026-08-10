SPEC_READY

Task: hole_scene_leftover_v3
Filed: 2026-08-10 (Architect)

Third attempt at the Hole_NN_Geo hierarchy leak. v1/v2 (K16) scoped it to the capture launchers;
the dominant vector is the EditMode test suite (RealHoleTerrainTests opens all 18 hole scenes
additively, cleanup depends on a domain-reload-fragile static). Evidence in SPEC.md § "Read this
first". Do not re-scope to launchers.
