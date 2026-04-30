---
name: SceneGroundProvider — physics ground for zone-mesh scenes
description: When PhysicsLab has zone mesh colliders, use SceneGroundProvider not HeightmapData
type: project
originSessionId: da349021-0eca-4118-9ca5-486258800871
---
`SceneGroundProvider : IGroundProvider` (`Assets/Scripts/Physics/Runtime/SceneGroundProvider.cs`) raycasts from Y=500 downward to return the top physical surface height. Use this as the ground provider in `PhysicsLabController.BuildGroundProvider()` whenever the scene has zone mesh colliders (greens, tees, cart paths sitting above terrain).

**Why:** `HeightmapData.SampleHeight` only knows the baked terrain heightmap. Zone meshes sit 0.3–0.5m above terrain — ball would spawn below the visible green surface if HeightmapData is used.

**Side effect:** Without `HeightmapData`, `BallSimulation` falls back to flat normal (0,1,0) — no slope-gravity in RunPuttPhase. This is intentionally correct for greens (flat putting surface). Roll-off onto rough/fairway uses airborne→bounce→roll, which is also correct.

**How to apply:** For any Hole1 or multi-hole PhysicsLab scene with MeshColliders on zone meshes, return `new SceneGroundProvider()` from `BuildGroundProvider()`. Only use `HeightmapData` for headless test simulations that need slope normals outside the green.
