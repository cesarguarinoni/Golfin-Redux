---
name: Water color bug in PhysicsLab (unfixed)
description: Water renders gray in PhysicsLab play mode despite fix attempt — needs further investigation
type: project
originSessionId: 4dbf2d84-8620-4202-96b5-c01ec83d510a
---
URPWater/Standard shader renders gray in PhysicsLab play mode instead of using material colors.
Attempted fix: enabled requiresDepthTexture and requiresColorTexture on ChaseCamera via UniversalAdditionalCameraData in PhysicsLabController.Awake(). Did NOT resolve it.

**Why:** Mobile_RPAsset has depth+opaque textures disabled. URPWater needs them for depth-based color. The camera-level override didn't work — possibly the pipeline asset level setting overrides the camera setting, or the camera used for rendering isn't ChaseCamera.

**How to apply:** Next investigation should check: (1) whether the actual rendering camera is ChaseCamera or Main Camera, (2) whether requiresDepthTexture on camera is respected when the pipeline asset has it off, (3) consider switching water mat to URP/Lit for PhysicsLab only, or enabling depth textures on the pipeline asset used by the lab.
