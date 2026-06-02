# SPIKE ADDENDUM — Architect research findings (terrain-apron invisibility)

**Added:** 2026-06-02 12:15 CEST / 19:15 JST (Architect, after kicking the spike — web research)
**Re:** `SPIKE_APRON_INVISIBILITY.md` — adds a new test (T1.5) and a device-render caveat. Read alongside the spike. If you (Code) haven't finished the spike, fold these in; if you have, re-run with T1.5 before concluding.

---

## Root cause of the Lit-vs-TerrainLit difference — FOUND (changes the test plan)

**A Unity terrain's lighting normal ALWAYS points straight up (0,1,0).** A terrain is a heightmap on one conceptual plane; for shading, the renderer feeds the BRDF an upward normal regardless of the terrain's actual slope. A real mesh (the apron) uses true per-face normals that follow its surface. So even with IDENTICAL albedo + normal map + tiling + mask, the apron and terrain receive **different brightness** because the geometric normal fed to lighting differs. Worst on H10's sloped proud rim (apron normals tilt with the ramp while the terrain beside it is lit as flat) → a brightness seam exactly at the grazing angle Cesar inspects from.
*Source: qweb.co.uk "Making URP Lit & TerrainLit shaders produce matching renders" — fixing the object's normals to point up made the Lit object render identically to TerrainLit terrain.*

### → NEW TEST T1.5 — up-normal Lit match (test BEFORE T2; cheaper, likely the winner)
Same as T1 (rough albedo + `T_Rough_Normal` + matte mask + tile 8), but ALSO **overwrite the apron mesh vertex normals to world-up (0,1,0)** after building the mesh (a few lines: set `mesh.normals` to an all-(0,1,0) array before assignment), instead of using true geometric normals — mimicking the terrain's flat-up lighting normal. The cited fix reports this makes URP Lit render the SAME as TerrainLit. Capture T1.5 vs terrain at grazing, apron unselected.
- Tradeoff: up-normals flatten the apron's own shading — but the apron is a thin near-flat fringe meant to read as flat rough, so that's acceptable (it's effectively what the terrain itself does). On H10's proud rim, confirm the up-normal apron still reads as rough and doesn't look oddly bright/dark vs the ramp.

### Revised test order
T1 → **T1.5 (up-normals)** → T2 (TerrainLit-on-mesh, heaviest). T1.5 attacks the actual cause and is expected to outperform T1.

## DEVICE-RENDER CAVEAT (critical for the final acceptance gate)
Known Unity bug: on **MOBILE** (Android/iOS) URP, **TerrainLit renders *lighter* than Lit** on many GPUs — does NOT reproduce on Windows/macOS standalone.
*Source: Unity Issue Tracker "[Mobile] URP GameObjects with TerrainLit shader are rendered lighter than Lit".*
GOLFIN ships iOS/Android. **Implication:** an apron that looks invisible in the EDITOR could still seam on DEVICE (editor uses desktop GPU; the bug is mobile-GPU-specific, and it's exactly a Lit-vs-TerrainLit brightness delta). 
- If a mesh approach (T1/T1.5/T2) is chosen, the invisibility gate must EVENTUALLY be confirmed on-device or in a mobile-target player — not editor-only. Flag in findings whether you only verified in-editor.
- **Q2a (drop the carve → no apron mesh at all) sidesteps BOTH the normal mismatch AND the mobile TerrainLit bug.** Weight it heavily — it is the safest path against device-render surprises, not just the cheapest.

## Net effect on the spike's recommendation
- Best outcome: **Q2a passes** (pad covers the small intrusion) on one or both greens → no mesh, no shader, no normal trick, no device risk.
- If a green still needs the carve: **T1.5 (up-normals)** is the most promising mesh path — cheaper than T2 and targets the real cause — but its acceptance is editor + DEVICE, given the mobile bug.
- Report all of T1 / T1.5 / Q2a with grazing captures (apron unselected, native res, frame-extracted, LOOK before captioning).
