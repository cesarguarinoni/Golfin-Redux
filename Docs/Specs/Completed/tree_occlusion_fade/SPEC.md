# SPEC — tree_occlusion_fade

**Added:** 2026-08-07 (Architect). **Status:** SPEC_READY, awaiting Cesar go.
**Goal:** When a tree sits between the camera and the ball during gameplay (aiming AND ball flight), the part of the tree blocking the sightline fades to a faint dithered ghost (~15% remaining) with a soft edge — never a hard pop, never a violent disappear. Cesar-decided (2026-08-07): see-through window style, faint ghost, active for aim + flight.

---

## 1. The defect

On tree-lined lies (ball near/under trees, doglegs, low chase-cam angles) the canopy or trunk fully hides the ball and the aim context. Nothing in the project fades occluders today.

## 2. Prior art — how other games handle this (researched 2026-08-07)

- **Dithered see-through window (chosen):** Breath of the Wild, Genshin Impact, Fortnite (UE "capsule fade") fade only the *fragments* inside a soft cone/capsule between camera and focus point, using screen-space dither on an opaque cutout material. No transparent-queue swap, no sorting problems, no per-instance bookkeeping — which is why it is THE approach that works with batched/terrain-rendered foliage. Standard Unity URP recipe: Bayer-matrix dither driven into the alpha-clip threshold (danielilett.com "Transparency Dithering in Shader Graph and URP").
- **Whole-tree translucency:** Everybody's Golf / Mario Golf fade the entire occluding tree. Rejected here: our trees are a MIX of terrain-system trees and standalone GameObjects (`TreePlacer` mixed mode — prefabs with LODGroup go into the terrain tree system), and terrain-painted trees share materials and are batch-rendered by the terrain — per-instance renderer fades (Unity-Object-Fade-style MaterialPropertyBlock per occluder) are impossible for them without converting occluders to GameObjects at runtime. Out of scope; revisit only if the window style reads badly on device.
- **True alpha blending:** rejected — leaf materials are alpha-clip cutout; moving them to the transparent queue breaks leaf self-sorting and multiplies overdraw on mobile.

## 3. STEP 0 — DIAGNOSE / INVENTORY FIRST (report before coding)

Same discipline as the map_view and tree-wind tasks: verify the premises in the running project, then report.

1. **Tree material inventory on a real hole (Hole 1 + Hole 6):** enumerate every material+shader on (a) `Terrain.activeTerrain.terrainData.treePrototypes[*].prefab` renderers, (b) standalone tree GameObjects under the hole root. Premises from the Architect's repo read, verify each:
   - Leaf materials (`MAT_*Leaf*`) → `Custom/Vegetation` (`Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`, Amplify-generated URP shader, opaque + alpha clip, `_WIND` keyword, `WindSpeedFloat1` — the shader `TreeWindDriver` drives).
   - Bark + impostor materials (`MAT_*Bark*`, `MAT_*Imposter*`) → **stock URP/Lit** (guid `933532a4fcc9baf4fa0491de14d08ed7` in the .mat files). Confirmed on JapaneseBlackPine; check the other species.
   - Spruce 1 / Spruce 3 (forced-standalone in `TreePlacer`) — likely on `Mobile_Tree_Bundle` NoWind built-in-RP Standard shaders (known separate finding in TellCode). If so: **report, do not fix here** — they simply won't fade until that finding is resolved.
2. **Confirm SV_POSITION pixel coords are available in each pass to patch** (§4.2) — the Amplify frag signatures differ per pass; find the `float4 clipPos : SV_POSITION` (or equivalent) input in Forward / DepthOnly / DepthNormals / GBuffer before writing the injection.
3. Report the inventory + any premise that fails in IMPLEMENTER_REPORT.md before proceeding.

## 4. Design

Two globals-driven pieces. **No scene edits, no per-instance state, no material writes at runtime.**

### 4.1 New C# driver — `Assets/Scripts/Physics/Viewer/TreeOccludeFadeDriver.cs`

Namespace `Golfin.Physics.Viewer` (sits next to `ChaseCamera`/`LoopCameraDirector`). Follow the `TreeWindDriver` pattern: static class, `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` init, **zero scene wiring**. It only calls `Shader.SetGlobalVector/Float` — nothing to restore on play-exit, but DO reset statics in Init (domain-reload rule) and push strength 0 on init so a stale global from a previous editor run can never leak.

Per-frame hook: `RenderPipelineManager.beginCameraRendering += OnBeginCamera` — guaranteed to run after every `LateUpdate` (i.e. after `ChaseCamera.RunLateUpdateLogic` has moved the camera), which a plain LateUpdate script-order gamble is not. In the handler, only act for the gameplay camera (`cam == Camera.main`; skip scene-view/preview cameras).

Focus point = what the camera looks at:
- Add a 2-line accessor to `ChaseCamera`: `public Vector3 CurrentFocus => _target != null ? _target.position : _shotOrigin;` (both fields exist). The driver finds the ChaseCamera on the rendering camera; if absent → strength target 0.
- This automatically covers aiming (resting ball at `_shotOrigin`/target), flight (live ball transform via `SetTarget`), and every camera mode.
- Smooth the *published* focus toward the raw focus (exp lerp, ~10/s) so teleports (drop rule, next-hole reset) never snap the window across the screen in one frame.

Strength state machine (temporal fade — this is the "not violently" guarantee at activation boundaries):
- Target 1 when: a hole is loaded, ChaseCamera focus is valid, and the map view is NOT open (`mapView.IsOpen` — same gate the tee-idle glow uses; NOTE: resolve the MapViewController reference the same way `TeeIdleGlowController` does, or via a static `MapViewController.IsAnyOpen` if that's what exists — check, don't guess).
- Target 0 otherwise (menus/Home have no ChaseCamera → naturally 0).
- Current strength moves toward target at `1/RampSeconds` per second (`RampSeconds = 0.25f`, scaled time is fine).

Globals published each frame (names prefixed to never collide):
- `_GolfinOccFadeBall` (Vector4: smoothed focus xyz, w unused)
- `_GolfinOccFadeStrength` (float 0..1)
- `_GolfinOccFadeParams` (Vector4: x = cos(OuterHalfAngleDeg), y = cos(InnerHalfAngleDeg), z = MaxOpacityCut, w = DepthFeatherM)
- `_GolfinOccFadeBias` (float: BallDistBiasM)

Tunables — public statics with doc comments (TreeWindDriver precedent), defaults:
- `InnerHalfAngleDeg = 10f` — full-fade cone around the camera→ball ray
- `OuterHalfAngleDeg = 16f` — fade reaches 0 here (soft spatial edge)
- `MaxOpacityCut = 0.85f` — faint ghost: ~15% of fragments survive at full fade (Cesar's pick)
- `DepthFeatherM = 1.5f` — fade-vs-depth softening band in metres
- `BallDistBiasM = 0.5f` — only fragments at least this much nearer than the ball fade (the ball itself, the green behind it, and everything past the ball never fade)
- `RampSeconds = 0.25f`
- `public static bool Disabled` — debug kill switch; when true publish strength 0 every frame (exact pre-change rendering, since the shader path is a no-op at strength 0).

Why no physics Linecast gating: canopy has no colliders (`TreeObstacleBaker` bakes trunk obstacles for the ball, not leaves), so a raycast gate would miss exactly the thing that blocks the view most. The cone is purely spatial — when nothing is inside it, zero pixels change, so "always on during gameplay" costs nothing visually and ~a dozen ALU per vegetation fragment.

### 4.2 Shader injection — `Assets/Packs/BSP Trees Package/Shaders/Vegetation.shader`

⚠️ This file is Amplify-generated (190 KB). Treat the .shader text as the source of truth from now on: wrap every edit in `// ── GOLFIN OCCLUDE FADE ──` … `// ── END GOLFIN OCCLUDE FADE ──` markers and note in the file header that an ASE regen would wipe them. Edits are small and repeated per pass — keep them IDENTICAL.

Add once to the shared `HLSLINCLUDE` block (top of file, after the Filtering.hlsl include):

```hlsl
// ── GOLFIN OCCLUDE FADE ── (tree_occlusion_fade spec)
float4 _GolfinOccFadeBall;    // xyz = focus (ball) world pos
float  _GolfinOccFadeStrength;
float4 _GolfinOccFadeParams;  // x=cosOuter y=cosInner z=maxCut w=depthFeatherM
float  _GolfinOccFadeBias;    // metres nearer-than-ball required

float GolfinOccFadeAmount(float3 worldPos)
{
    float s = _GolfinOccFadeStrength;
    if (s <= 0.001) return 0.0;
    float3 toBall = _GolfinOccFadeBall.xyz - _WorldSpaceCameraPos.xyz;
    float3 toFrag = worldPos            - _WorldSpaceCameraPos.xyz;
    float ballDist = max(length(toBall), 0.01);
    float fragDist = max(length(toFrag), 0.01);
    // depth gate: only fragments in front of the ball, feathered
    float depthT = saturate((ballDist - _GolfinOccFadeBias - fragDist) / _GolfinOccFadeParams.w);
    // cone gate: angular distance from the camera→ball ray, feathered
    float cosAng = dot(toFrag / fragDist, toBall / ballDist);
    float coneT  = smoothstep(_GolfinOccFadeParams.x, _GolfinOccFadeParams.y, cosAng);
    return s * depthT * coneT;
}

float GolfinBayer4(uint2 pixel) // 4x4 Bayer, 1/32..31/32, no texture fetch
{
    const float bayer[16] = { 1,17,5,21, 25,9,29,13, 7,23,3,19, 31,15,27,11 };
    return bayer[(pixel.y % 4u) * 4u + (pixel.x % 4u)] / 32.0;
}
// ── END GOLFIN OCCLUDE FADE ──
```

In the fragment of each patched pass, immediately after the existing `clip(Alpha - AlphaClipThreshold);` (all passes already have `WorldPosition` in scope — Forward builds it from tSpace0..2.w, the others carry `IN.worldPos`):

```hlsl
// ── GOLFIN OCCLUDE FADE ──
float golfinFade = GolfinOccFadeAmount(WorldPosition);
if (golfinFade > 0.001)
    clip((1.0 - golfinFade * _GolfinOccFadeParams.z) - GolfinBayer4(uint2(<SV_POSITION>.xy)));
// ── END GOLFIN OCCLUDE FADE ──
```

`<SV_POSITION>` = that pass's pixel-position input, found in Step 0.2. NOTE: the Forward pass wraps its clip in `#ifdef ASE_DEPTH_WRITE_ON` variants — place the injection so it runs in BOTH variants (after the #endif, unconditionally).

Patch these passes, identically: **Forward** (~line 196), **DepthOnly** (~1337), **DepthNormals** (~2518), **GBuffer** (~2943), **Universal2D** (~2139). DepthOnly/DepthNormals MUST match Forward or the depth prepass diverges from the color pass (SSAO/depth-texture artifacts around the window).
**Do NOT patch:** **ShadowCaster** (~924) — a faded tree keeps casting its shadow. That grounds the ghost, avoids shadow-pop, and matches Genshin/BOTW behaviour. **Meta** — bake-only, irrelevant.

Interaction notes: composes fine with `LOD_FADE_CROSSFADE` (also a dither clip); `AlphaToMask` is Off in this shader; the `_WIND` keyword is orthogonal.

### 4.3 Bark + impostors — bring them onto the shader we own

Trunks must fade too (a trunk dead in front of the aim camera is the worst offender). Stock URP/Lit can't take the injection, so **retarget the bark and impostor .mat assets to `Custom/Vegetation` with the `_Wind` toggle OFF** (one-time asset change, editor-side):

- Slot mapping: `_BaseMap`→`_Albedo`, `_BumpMap`→`_NormalMap`, `_BaseColor`→`_Color`, metallic/smoothness values across, `_Cutoff`→`_AlphaCutoff` (impostors have `_ALPHATEST_ON`; bark is opaque — cutoff 0). Do it via a small editor utility or by hand; either way commit the .mat diffs and NOTHING else.
- `TreeWindDriver.Apply()` will start writing `WindSpeedFloat1` on these materials (it filters by shader name) — harmless with the `_WIND` keyword off; confirm the editor guard restores them like the leaves.
- **Gate: before/after screenshots of one tree of each species (same camera, same hole) in IMPLEMENTER_REPORT.md.** Vegetation is a full URP PBR shader (albedo/normal/metallic/smoothness/shadow-tint) so bark should read near-identical — if a species shifts visibly, STOP, leave that species on Lit (its trunk won't fade), and flag for Cesar's call rather than chasing lighting parity.

### 4.4 Tests — `Assets/Scripts/Physics/Tests/TreeOccludeFadeDriverTests.cs` (EditMode)

Cover the pure logic (extract it static-testable, same pattern as the map-view solvers): strength ramps 0→1 in RampSeconds and back; target 0 when map open / no focus / `Disabled`; params vector packs the configured angles as cosines; focus smoothing converges and never overshoots; globals actually written (read back via `Shader.GetGlobal*`).

## 5. Acceptance tests

1. **Window works (editor, Hole 1):** put the ball so trees stand between camera and ball (tee shot aimed into the tree line, or a lie under canopy). Ball + immediate green context clearly visible through a faint dithered ghost; tree stays SOLID outside the cone. Screenshot.
2. **No pop:** orbit the aim camera so a tree sweeps into/out of the corridor; capture consecutive frames — the window edge is a gradient (smoothstep cone), never a single-frame cut. The activation ramp (0.25 s) likewise on hole load.
3. **Zero-diff when clear:** same frame with no tree in the corridor, `Disabled` true vs false → pixel-identical screenshots.
4. **Flight:** hit a shot through/past trees — the window tracks the live ball under the chase cam; no flicker at the `SetTarget(null)` terminal transitions (focus falls back to `_shotOrigin`, strength never pops).
5. **Map view:** open the map on a treed hole — no fade window in the top-down view (strength ramped to 0).
6. **Shadows:** faded tree's ground shadow unchanged (ShadowCaster untouched).
7. **No depth artifacts:** with the window active, no SSAO halo / depth-texture shimmer around the faded region (DepthOnly+DepthNormals patched identically to Forward).
8. **Kill switch:** `TreeOccludeFadeDriver.Disabled = true` restores pre-change rendering exactly (screenshot diff on an occluded frame).
9. **EditMode suite green** (§4.4) plus the full existing suite — zero regressions.
10. **⚠️ Device (Cesar, manual):** dither grain acceptable at retina DPI on the real phone; frame time on Hole 1 unchanged; tune `InnerHalfAngleDeg`/`OuterHalfAngleDeg`/`MaxOpacityCut` on device if the window feels too tight/too ghosty.

## 6. Out of scope

- Whole-tree fade mode (rejected §2; revisit only on Cesar's call after seeing the window on device).
- Spruce/NoWind-shader trees (report in Step 0; separate TellCode finding owns that shader mess).
- Tree sway missing on device (smoke issue #6) and LOD impostor popping — separate tasks; do not entangle.
- Non-tree occluders (rocks, backdrop, buildings) — trees only, per Cesar's ask.
- Fading shadows of faded trees.
- Hierarchy rebuilds, scene edits, prefab restructures — none are needed and none are allowed.
