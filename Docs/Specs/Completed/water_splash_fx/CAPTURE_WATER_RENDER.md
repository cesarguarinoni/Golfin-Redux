# CAPTURE_WATER_RENDER — `water_splash_fx` Problem A (water grey in capture)

**Author:** Architect (claude.ai) · **Date:** 2026-06-13
> **RESOLVED 2026-06-13 (Cesar).** Actual root cause: **two directional lights active at once** — ShellScene's persistent light + the additive hole's light double-lit the water surface, washing it flat grey. NOT a reflection-probe issue. The architect hypotheses below were wrong-shaped (reasoned from symptom without a live scene-graph dump). Code is fixing it (cull/disable the duplicate light on additive hole load). **Lesson:** with ShellScene→additive-hole, check for duplicated persistent objects (lights, audio listeners, cameras) FIRST. The original diagnosis is kept below for the record only.

---

**This was scoped as a Code task** — needs the Unity MCP bridge (`localhost:21573`), which the Architect doesn't have. Architect supplied the (incorrect) diagnosis + fix plan; Code ran it in-engine and found the real cause.

## Symptom
Hole 6 water renders flat **grey/sandy** in every frame of the bot/capture flow. It renders **correctly (deep blue, rippled, reflective) in normal manual play.** Cesar confirmed: not a camera-angle artifact, not a known bug. Both flows call `SeedSession(6,…)` + `GameplaySceneLoader.BeginGameplayLoad(6)` and both host on `LabScaffold` + additive `Hole_06_Geo`. Normal play boots from `ShellScene` first; the capture rig (`WaterSplashCaptureRig.cs`) also boots ShellScene, so that alone isn't the difference.

## Hypotheses (ranked)
1. **Reflection environment not populated in the scripted flow (MOST LIKELY).** Grey = reflection absent → the water shader shows only the refracted grey lakebed, no sky/reflection. A reflection probe (or planar/SSR reflection) that normally refreshes during the Home→ModeSelect→HoleSelection→play navigation never refreshes when the script jumps straight to `BeginGameplayLoad`. Evidence-for: grey is exactly "reflection missing"; manual nav has extra frames/transitions a script skips. Evidence-against: none yet — confirm by dump.
2. **Capture records before the probe finishes rendering.** Realtime probe with time-slicing needs several frames; `WaterSplashCaptureRig` may start the camera-hold/record too early. Evidence-for: timing-sensitive, matches "works when a human dawdles." Test: insert a multi-frame settle before record.
3. **`PhysicsLabController.CopyHoleLighting()` drops the reflection env.** It copies only a SUBSET of `RenderSettings` + `DynamicGI.UpdateEnvironment()`; may omit `defaultReflectionMode` / `customReflectionTexture` / `reflectionIntensity` / probe data. Evidence-against: if this were the whole story normal play would also break (it shares the path) — UNLESS normal play's probe refresh masks the gap. Plausible as a contributing cause.
4. **(outside-the-box)** A water-manager / time-of-day / animated-water component is enabled by a UI transition the script skips, so the surface never initialises its reflection sampler.

## Diagnostic (do this first — don't fix blind)
Dump the same state at the identical moment (ball-over-water / `IsHoleReady` + 1s) in **both** flows and diff:
- `RenderSettings`: `skybox`, `ambientMode`, `ambientIntensity`, `defaultReflectionMode`, `customReflectionTexture` (or `.customReflection`), `reflectionIntensity`.
- All active `ReflectionProbe`s on Hole 6: `mode` (Baked/Realtime), `refreshMode`, `bakedTexture`/`realtimeTexture` non-null?, `texture` non-null?, `boxProjection`.
- Water material: shader name, reflection-related properties + enabled shader keywords (e.g. `_ENVIRONMENTREFLECTIONS`, planar reflection texture slot non-null?).
- URP: is SSR / planar-reflection in the active Renderer feature list, and is the reflection RT assigned at capture time?

The grey side will reveal the null/zero (probe texture null, customReflection null, intensity 0, or keyword off).

## Fix — prefer capture-only (keeps zero gameplay diff)
Fix locus #1 = `WaterSplashCaptureRig.cs` (editor/bot-only — same pattern as the existing camera-hold; zero gameplay-file diff). After `IsHoleReady`, before camera-hold + record:
- `DynamicGI.UpdateEnvironment();`
- Force the hole's reflection probe(s): set `refreshMode` appropriately and call `probe.RenderProbe()` (capture the returned render-id and `yield` until done), OR if Realtime time-sliced, set `timeSlicingMode = NoTimeSlicing` for the capture and refresh once.
- Then **wait N frames** (start ~5–8) for the RT to populate before `BeginRecord`.

Only if the env genuinely isn't built in the scripted path (hypothesis 3 confirmed by the dump) extend `PhysicsLabController.CopyHoleLighting()` to also carry the reflection fields above. **Caution:** that method is on the production path shared with normal play (which currently works) — additions must be no-ops where it already works; verify normal play still renders correctly after any change, and keep the gameplay-behaviour diff null.

## Acceptance
- Water reads deep-blue/reflective in the capture video, matching normal play.
- No diff to gameplay behaviour; `BallSimulation.cs`, `BallStateMachine.cs`, `OBDropResolver.cs`, `LabScaffold.unity`, `ChaseCamera.cs`, `LoopCameraDirector.cs` stay zero-diff (per SPEC).
- Fix lives in the capture rig unless the dump proves a `CopyHoleLighting` gap.
