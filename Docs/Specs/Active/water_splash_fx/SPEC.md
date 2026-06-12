# SPEC — `water_splash_fx` (Order 349)

**Tier:** TELLCODE (Tier 2) — additive presentation feature on an established event pattern (mirror of `BallTrailController`); no new architecture, no spatial math, no Figma UI. Architect classification; Cesar may escalate to FULL PIPELINE at kickoff. Visual gate (bot-recorded video) still REQUIRED.
**Priority:** P2 · Phase: Gameplay Polish · Notion: Order 349
**Author:** Architect, 2026-06-12. Scope: splash VFX + audio HOOK only (clip asset lands with Order 350 `sound_effects`).

---

## 1) Problem

A ball entering water terminates the shot (`TerminationReason.HitWater` → OB flow → drop) with ZERO feedback: no splash, no sound, the ball just stops and teleports. Trees now have physical feedback (348); water is the remaining silent hazard.

## 2) Verified ground truth (Architect recon, 2026-06-12 — all live in repo)

- Water is fully mechanical already: `SurfaceType.Water` terminates all sim phases (`BallSimulation.cs` ~lines 245/610/787, `TerminationReason.HitWater`), `BallStateMachine` maps it to OB with `OBReason.Water` + `terminalSurface = SurfaceType.Water` (~line 140), `OBDropResolver` resolves the drop. **349 touches NONE of this.** Presentation only — gameplay/sim impact must be ZERO by construction.
- **Timing hook:** `BallStateMachine.Tick(bool animatorIsPlaying)` is falling-edge driven — `OnStateChanged` fires when the VISUAL playback completes, i.e. exactly when the on-screen ball reaches the water. Same hook `BallTrailController` uses for its OB red-flip. No custom playback tracking needed.
- **Event payload:** `BallStateChange` (readonly struct) carries `Position` (fp3, ball position at transition — the water-entry point), `Surface`, `OBReason?` (populated only when `Next == OB`), `SimTime`.
- **Wiring pattern:** presentation components live on the BallAnimator GameObject and are wired in `PhysicsLabController.Awake()` via `Configure(anim, sm, shot)` — copy `BallTrailController`'s shape exactly, including idempotent re-wire (unsubscribe-before-subscribe for domain reload).
- **Audio:** `AudioManager.Instance.PlaySFXAtPosition(AudioClip clip, Vector3 position, float volumeMultiplier = 1f)` exists (`AudioManager.cs:259`).
- 1v1: bot shots run the same ShotController → BallStateMachine path, so the splash fires for bot water balls with no extra work. Confirm in video.

## 3) Design

### New: `WaterSplashController : MonoBehaviour` (namespace `Golfin.Physics.Viewer`, same folder as `BallTrailController`)

- Lives on the BallAnimator GO. `Configure(BallAnimator anim, BallStateMachine sm, ShotController shot)` — idempotent, mirrors trail.
- Subscribes `sm.OnStateChanged`. Trigger condition: `change.OBReason == OBReason.Water` (do NOT trigger on `Surface == Water` alone; HitWater is terminal and Water-OB is the unambiguous signal).
- On trigger: spawn/replay the splash at `change.Position` (fp3 → Vector3). The sim terminates AT the water surface, so Y needs no correction raycast. NOTE: if a hole's visual water plane Y diverges from sim ground Y, flag it in the report rather than adding correction logic.
- **Single pooled instance:** one ParticleSystem prefab instantiated lazily on first use, reused via `Clear()+Play()` (one ball → never concurrent). Null-safe: prefab slot unassigned = silent no-op (log once).

### Splash prefab (`WaterSplash.prefab`, new, under FX/)

- Root ParticleSystem with 2 children: (a) vertical spray burst (20–35 particles, white-blue, gravity on, 0.6–0.9s), (b) ring/ripple — flat quad or particle with horizontal expansion + fade (~1.2s). Unity PS randomness is FINE here (presentation layer; determinism not required — sim untouched).
- **Two intensity tiers** by incoming speed at the terminal hit: full splash (flight ball) vs small plop (rolled in). Tier = start-size/burst-count multiplier on the same prefab. NOTE: if the terminal incoming velocity isn't accessible without new plumbing, ship single-tier and log the speed for a follow-up — do NOT add new public API to BallStateMachine for this.
- URP mobile budget: ≤ 50 particles total, one shared material, no lights, no collision modules.

### Audio hook (clip deferred to Order 350)

- `[SerializeField] AudioClip _splashClip;` on the controller; on trigger, if non-null → `AudioManager.Instance.PlaySFXAtPosition(_splashClip, worldPos)`. Ship with the slot EMPTY (null-safe). Order 350 supplies the asset.

### Ball visibility (optional polish — include only if cheap)

- Between the splash and the ball's reposition to the drop point, the visual ball sits ON the water looking dry. If the existing OB respawn path exposes a clean seam to toggle the ball Renderer off/on, hide it during that window. NOTE: do not restructure BallAnimator/respawn flow for this — no clean seam, skip and note in the report.

## 4) Files

- NEW `Assets/Scripts/Physics/Viewer/WaterSplashController.cs`
- NEW `Assets/FX/WaterSplash.prefab` (+ material)
- EDIT `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs` — one wiring block in `Awake()` mirroring the trail wiring
- NO edits to: BallSimulation, BallStateMachine, OBDropResolver, surface/CSV data. Zero-gameplay-impact is an acceptance item.

## 5) Acceptance

- [ ] Flight ball into water → splash burst + ripple at the entry point, at the moment the visual ball arrives (not at sim compute time).
- [ ] Rolled-in ball → smaller plop (or single tier shipped with NOTE, see §3).
- [ ] Bot (1v1) water ball → same splash, no extra wiring.
- [ ] Prefab slot empty → no exceptions, no behavior change.
- [ ] Zero gameplay impact: no sim/state-machine/drop files touched (diff-verified); existing test suite green untouched.
- [ ] EditMode test `WaterSplash_TriggersOnlyOnWaterOB`: controller fires exactly once on an `OBReason.Water` transition, never on OOB/normal transitions (pure C# event test, no scene).
- [ ] Bot-recorded full-res video: one flight splash + one roll-in + one OOB (non-water) control showing NO splash.

## 6) Out of scope

Water shader interaction/foam decals, drop-zone FX, camera shake, splash audio assets (Order 350), underwater ball rendering, splash on non-terminal water skips (mechanic doesn't exist).
