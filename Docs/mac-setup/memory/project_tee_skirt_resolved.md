---
name: Tee skirt resolution
description: Linear-slope ramp fixed the tee cliff after 4 failed attempts — approach confirmed working
type: project
originSessionId: 511c0b3f-40bf-4f47-b431-ea1ed5654573
---
The tee skirt cliff problem (visible 1m vertical drop at the outer edge of tee platforms) was resolved on 2026-04-20 with a linear-slope ramp approach after 4 prior failures.

**What worked:** `rampH_m = maxH_m - minDistM * TeeMaxRampSlope` — descend at constant slope until ramp meets baseline. No fixed radius. Self-terminating by construction.

**Why:** No outer boundary to produce a cliff. Ramp and terrain join wherever they meet, always C¹-continuous.

**Why previous attempts failed:**
1. Unit fix alone → 30m uniform skirt raised cart paths → 0.4m wall after depression pass
2. Mesh skirt ring → winding failures, concentric ring artifacts
3. Per-edge adaptive radii → stair-step boundaries everywhere
4. Unit fix + TeeMaxSkirtMeters cap → cap became dominant clamp (60m), runaway skirt

**How to apply:** If tee skirt issues ever resurface, do NOT go back to radius-based approaches. The linear-slope self-terminating ramp is the correct architecture.
