# SPEC — `tap_feedback_fx`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.
> Created 2026-06-06 (Architect). Tier: FULL PIPELINE (visual fidelity + new always-on system + package dep + global input).

## Status

See `STATUS.md`. Currently `SPEC_READY`.

## Goal

Add a global, always-on "your touch registered" micro-feedback: a subtle soft-glow **ring + small sparkle burst** spawned at every finger-down point, on every screen (menus + in-game), rendered **above all UI**. Purely cosmetic and **non-consuming** — it never intercepts, blocks, or alters input. The feel target is the Pokémon TCG Pocket tap shimmer / Material ripple: reassurance and juice, not a gameplay mechanic.

## Reference

- **Figma frame:** none — this is procedural VFX, there is no mockup node. Do not go looking in Figma.
- **Visual target:** a soft expanding ring (Material-style ripple, ~30→90 px, fade out, ~0.30 s, ease-out) + ~6 additive sparkles drifting outward, white / soft-gold, 0.35–0.5 s, **low peak alpha (~0.5)**. Subtle over flashy.
- **Placeholder vs canonical:** n/a.

## Architecture context

- **New package dependency — UIParticle (mob-sakai `ParticleEffectForUGUI`).** Renders a real `ParticleSystem` through `CanvasRenderer`, so it draws **over Screen Space – Overlay** canvases without an extra Camera / RenderTexture, no per-frame GC, sortable. URP + Unity 6 compatible (project is `6000.3.9f1`). Package id `com.coffee.ui-particle`, assembly `Coffee.UIParticle`, runtime namespace `Coffee.UIExtensions`. Install via UPM git URL `https://github.com/mob-sakai/ParticleEffectForUGUI.git` (then **pin the resolved version** in `Packages/manifest.json`) or OpenUPM `com.coffee.ui-particle`. Confirmed **not** currently in `Packages/manifest.json`.
- **Host — standalone self-bootstrapping persistent controller.** Spawn it from a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` bootstrap (same mechanism as `Assets/Scripts/Gameplay/Input/InputSimulationBootstrap.cs`), as its own `DontDestroyOnLoad` object. Do **not** bolt it onto `PersistentUIManager` — that singleton (`Assets/Scripts/UI/PersistentUIManager.cs`, `Golfin.UI`, DontDestroyOnLoad) hides its bars until Home loads, so it's the wrong owner for an effect that must exist from boot on every screen. PersistentUIManager is the *precedent* for cross-scene UI, not the host.
- **Input detection — ⚠️ DO NOT USE EnhancedTouch.** `InputSimulationBootstrap.cs` documents that `EnhancedTouchSupport.Enable()` at `BeforeSceneLoad` was suspected of **breaking UI buttons + raw mouse reads**, and the enable is deliberately left commented out. This task must keep it that way. Detect finger-down by polling the low-level Input System in `Update()`:
  - Primary (mouse + primary touch): `Pointer.current?.press.wasPressedThisFrame` → position `Pointer.current.position.ReadValue()`.
  - Multi-touch: when `Touchscreen.current != null`, iterate `Touchscreen.current.touches`, fire on each `touch.press.wasPressedThisFrame` at `touch.position.ReadValue()`.
  - This is strictly **observational/read-only**. Do not enable EnhancedTouch, do not consume input, do not add a `GraphicRaycaster` to the FX canvas.
- **Complementary existing pattern (do NOT duplicate):** `Assets/Scripts/UI/ButtonPressFeedback.cs` is per-button press feedback. This task is the separate global layer.
- **Optional audio:** `AudioManager.Instance` for a soft tick — **OFF by default** (per-tap audio reads as noise). Expose a serialized `playAudio` bool only.
- **Asmdef:** if `Assets/Scripts/UI/` is governed by an asmdef, add references to `Coffee.UIParticle` and `Unity.InputSystem`. If the dependency can't be resolved cleanly, flag it rather than guessing.

## Implementation

1. **Package.** Add UIParticle (see above). Verify clean import under URP / Unity `6000.3.9f1` — no console errors.

2. **Effect prefab** `TapFeedbackFX.prefab` (under the project's UI prefab folder):
   - Root: `RectTransform` + `UIParticle` (`Coffee.UIExtensions`).
   - Child `ParticleSystem` authored via the Unity MCP particle tools:
     - **Ring** — one soft expanding ring/glow. If a clean ring is awkward as a particle, the ring may instead be a sibling UGUI `Image` (soft radial sprite) scaled+faded by a short code tween; keep sparkles in the PS.
     - **Sparkles** — burst of ~6 small additive points, radial outward velocity ~120 px/s, gravity 0, size ~6–10 px, lifetime 0.35–0.5 s, color white→transparent (soft gold tint optional), **additive** blend. Material: URP `Universal Render Pipeline/Particles/Unlit` (additive) or a UIParticle-friendly `UI/Additive`.
   - All `Image`/graphic `raycastTarget = false`. No Animator needed.
   - Keep peak alpha low — this MUST read as subtle.

3. **`TapFeedbackController`** (`Assets/Scripts/UI/TapFeedbackController.cs`, namespace `Golfin.UI`):
   - `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` bootstrap spawns one `DontDestroyOnLoad` GameObject carrying:
     - a dedicated overlay `Canvas` (`renderMode = ScreenSpaceOverlay`, `sortingOrder` high enough to sit **above all UI including modals + ToastController** — verify against the topmost existing canvas; e.g. `5000`),
     - a `CanvasScaler` matching the project's reference resolution,
     - **no** `GraphicRaycaster`,
     - the controller.
   - **Pool:** pre-instantiate `N` (default 8) `TapFeedbackFX` instances under the canvas. On finger-down: take next free instance, position it with `RectTransformUtility.ScreenPointToLocalPointInRectangle` against the canvas rect, `ParticleSystem.Clear()` + `Play()` (+ ring tween). Recycle when `!ParticleSystem.IsAlive(true)` or after a max-lifetime timer. NOTE: if the project already has a pooling utility, prefer it; otherwise this local fixed pool is fine.
   - One effect **per finger**; mouse fallback for editor testing.
   - **Serialized tuning:** pool size, ring start/end px, ring duration, sparkle count, sparkle speed, lifetimes, peak alpha, tint color, `playAudio` (default false), `audioClip`.
   - Expose `public static bool Suppressed` (default false) so the shot system *could later* mute the effect during active aim-drag. **Wiring that suppression is OUT OF SCOPE** — just expose the flag.
   - Strictly observational: never consume input, never touch EnhancedTouch.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item MUST be `PASS`/`FAIL` with a one-sentence justification citing what was measured.

- [ ] Tapping over empty space, over a button, over a modal, and in-game each spawns the ring+sparkle at the contact point and renders ABOVE that UI.
- [ ] UI buttons, bottom-nav, and in-game shot input all still work exactly as before — the effect never intercepts input.
- [ ] Multi-touch: two simultaneous fingers spawn two independent effects.
- [ ] Effect is subtle — low alpha, ≤0.5 s, no screen darkening, no layout shift.
- [ ] Rapid tapping (10+/s) shows no GC spikes and no instantiate-per-tap (pool reused) in the Profiler.
- [ ] Effect is present from the first interactive screen through in-game and persists across scene loads.
- [ ] `EnhancedTouchSupport` is NOT enabled by this change; `InputSimulationBootstrap.cs` is untouched.
- [ ] No white-box placeholders visible in the screenshot.
- [ ] All `[SerializeField]` references wired in the Inspector.
- [ ] Unity Console has no errors related to this task.
- [ ] Spec deviations (if any) flagged at the bottom of the report with justification.

## Files / hierarchy this task touches

- `Packages/manifest.json` — add `com.coffee.ui-particle`.
- `Assets/Scripts/UI/TapFeedbackController.cs` — NEW.
- `Assets/.../Prefabs/TapFeedbackFX.prefab` — NEW (UIParticle + ParticleSystem).
- `Assets/Scripts/UI/<UI asmdef>.asmdef` — add `Coffee.UIParticle` + `Unity.InputSystem` refs IF an asmdef governs this folder.
- A soft ring/glow + sparkle sprite (small white radial PNG, additive) — NEW art asset only if nothing suitable exists.

## Smoke evidence

Manual/PlayMode pass: load Home → tap empty area + a nav button + open a modal and tap it + enter a hole and tap. Confirm the effect renders above each surface and input still fires in every case.

### Visual-fidelity verification (Lesson O)

This is a visual-fidelity task — dispatch/event captures alone are NOT sufficient. Require **human-in-the-loop play-and-confirm**: the implementer drives the flow (editor or device) and writes a content-sanity description in `IMPLEMENTER_REPORT.md` — what the ring/sparkle looked like, where it appeared relative to the finger, that it sat above UI, and that it read as subtle. Attach a short capture to `videos/` if possible.

## Out of scope (do NOT do these)

- Per-button feedback (already `ButtonPressFeedback.cs`).
- Haptics / vibration (leave the audio hook off; no haptics this task).
- A settings-menu toggle to disable the effect (nice later; not now).
- Wiring shot-drag suppression (only expose the `Suppressed` flag).
- Any change to `InputSimulationBootstrap.cs` or anything that enables EnhancedTouch.

---
