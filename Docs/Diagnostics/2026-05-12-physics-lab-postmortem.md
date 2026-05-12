# PhysicsLab Hole-Picker Bug Chain — Postmortem

**Date:** 2026-05-12
**Surfaced as:** Single user-visible bug ("ball spawns in wrong place when loading any hole other than Hole 1 via the picker")
**Actually:** Four independent bugs interacting, each introduced by a different commit at a different phase

---

## The four bugs

### Bug 1 — `DivideByZeroException` from stale `AeroCfg`

- **Introduced:** `1dba77d0` — "Phase 7 Part F — putt mode, debug toggles, ball placement dropdown" (2026-04-XX)
- **What changed:** Awake stopped doing eager config loads. Was: `AeroCfg = PhysicsConfigLoader.LoadAeroConfig(); WindCfg = ...; ...` (four direct assignments). Became: `EnsureConfigsLoaded()` (a lazy-load gated by `_configsLoaded` bool).
- **Why it broke:** `AeroCfg` is an auto-property (`public AeroConfig AeroCfg { get; private set; }`). Its compiler-generated backing field gets **zeroed** by Unity's "Reload Domain Only" PlayMode entry — but plain private fields like `_configsLoaded` are NOT zeroed under the same setting. So `_configsLoaded` stays `true` across Edit→Play, the short-circuit fires, `AeroCfg` stays at `default(AeroConfig)` with `SpinRateReference = 0`, and the first shot divides by zero in `AeroModel.ComputeAeroForce`.
- **Masked by:** `AeroConfig.AssertValid` defense-in-depth + a `try { ComputeMaxCarryYards() } catch { LogWarning }` wrapper, both of which absorbed symptoms instead of propagating them. Stack trace pointed at `BallSimulation:741` and `AeroModel:81`, not the actual root in `EnsureConfigsLoaded`.
- **Fix:** `78a48b6e` — short-circuit now validates `AeroCfg.SpinRateReference > 0` before honoring the cached `_configsLoaded=true`. On stale state it logs a warning and reloads.

### Bug 2 + Bug 3 — Ball-clone accumulation

- **Bug 3 (original sin):** `063ff2ff` — "Phase 6 — Physics Viewer lab" (the very first BallAnimator). `DestroyInstance` only cleared the `_instance` field; never swept other children. Harmless under the original assumption: PlayMode-only usage, one ball at a time.
- **Bug 2 (activator):** `1f1c4fce` — "fix: ball spawns underground — notify controller on hole load/unload in edit mode" (2026-04-24). HolePicker started calling `OnHoleLoaded` directly from Edit Mode. That chained through `SetupAtTee → ballAnimator.PlaceAtRest → SpawnInstance → Instantiate(ballPrefab, transform)`. In Edit Mode, the new clone becomes a serialized child of BallAnimator → scene dirty → persisted to `LabScaffold.unity`. Every picker action left a ghost behind. After enough picker actions, 8+ ghosts in the scene file.
- **Why undetected:** When `1f1c4fce` was authored, "Edit Mode notification" was thought of as a notification-only change. The fact that the notification chain executed `Instantiate(..., transform)` as a side effect was invisible from the picker's perspective.
- **Why it surfaced now:** Hole 1 used the same coords as the original LabScaffold defaults, so accumulated ghosts there overlapped the legitimate ball position visually. Any other hole = ghosts at Hole 1 coords, ball at the new hole's tee = visually noticeable.
- **Fix:** `9f4160f4` — two-part. `BallAnimator.Awake` sweeps existing ghost children matching `ballPrefab.name` (self-heals the disk legacy). `SpawnInstance` tags new Edit-Mode clones with `HideFlags.DontSaveInEditor` (stops new ghosts at the source).

### Bug 4 — Camera not repositioned on tee setup

- **Introduced:** `0b64566f` — "controls_h_chase_camera_regression DONE (iter-8 fallback + 3 in-flight fixes)" (2026-05-08).
- **What changed:** Iter-6 of the controls_h arc moved camera writing OUT of `SetupAtTee` into `ChaseCamera.SetAimDirection` (single-writer principle). Iter-8 fell back to "ApplyCameraYaw owns camera position during Aiming" — and restored the call in `HandleCameraOrbit` and `HandleShotResolved(AtRest)`, but NOT at the Aiming entry points (`SetupAtTee`, `PlaceBallAt`).
- **Why it broke:** The comment correctly stated the ownership model. The wiring missed two of the four ownership-handoff points. Camera stayed at LabScaffold's serialized default (near Hole 1) on any hole-load until user click-swiped to trigger HandleCameraOrbit.
- **Why undetected:** All testing involved click-swiping the camera before evaluating. Nobody tested the "load a hole and immediately pull the club without orbiting first" path.
- **Fix:** `02f622df` — invoke `ApplyCameraYaw` at the end of both `SetupAtTee` and `PlaceBallAt`.

---

## Common patterns + prevention rules

### Pattern A — Auto-property + guard bool = Edit→Play zero-init trap

When Unity's Player settings has **Reload Domain Only** enabled (Reload Scene OFF), some MonoBehaviour state is reset on PlayMode entry and some is not. Specifically: **auto-property compiler-generated backing fields can be zeroed** while **plain private fields persist**. If a guard bool flags a config as loaded but the config itself was an auto-property that got zeroed, the next short-circuit serves stale `default(T)`.

**Prevention rules:**
1. Any `if (_loaded) return;` short-circuit MUST be followed by a sentinel check on the cached state itself (`if (_loaded && _cachedThing.IsValid()) return;`).
2. Prefer explicit private fields (`AeroConfig _aeroCfg`) over auto-properties (`public AeroConfig AeroCfg { get; private set; }`) for any state that gets cached across PlayMode boundaries. If you need public access, use a property with an explicit backing field.
3. The same pattern can resurface in any config loaded via `EnsureConfigsLoaded()` — `WindCfg`, `SurfaceCfg`, `PuttCfg`, anything new. The current fix validates `AeroCfg.SpinRateReference > 0` as the sentinel; future configs should add similar sentinels.

**Grep query to find new instances:**

```powershell
Select-String -Path Assets/Scripts -Recurse -Pattern "public \w+ \w+ \{ get; (private )?set; \}" | Where-Object { $_.Line -match "Config|Stats|State" }
```

### Pattern B — Edit-Mode invocation of MonoBehaviour code = scene pollution

Any time an editor tool (HolePicker, custom inspector, etc.) directly invokes runtime MonoBehaviour methods, any `Instantiate(..., transform)` call inside that chain becomes a serialized child of the parent transform. The scene becomes dirty; saving persists the clone. Each tool invocation leaves a ghost.

**Prevention rules:**
1. Any `Instantiate(..., transform)` call in code that can be invoked from Edit Mode MUST tag the result with `HideFlags.DontSaveInEditor` when `!Application.isPlaying`.
2. Any method intended to be called from Edit Mode (via tools) SHOULD have an `Awake` companion that sweeps stale children from prior Edit-Mode invocations. Self-healing on PlayMode entry.
3. When adding a new Edit-Mode tool that drives a MonoBehaviour, audit the full call graph for `Instantiate`, `AddComponent`, and any other state-creating operations. Tag them or guard them.

**Grep query:**

```powershell
Get-ChildItem -Recurse -Filter "*.cs" -Path Assets/Scripts | Select-String -Pattern "Instantiate\(.*,\s*transform\)" | Where-Object { $_.Path -notmatch "Editor|Tests" }
```

### Pattern C — Defense-in-depth masks root cause

`AeroConfig.AssertValid` and the `try/catch` around `ComputeMaxCarryYards` both absorbed symptoms instead of propagating them. They were added to prevent crashes but had the side effect of hiding the zero-init mechanism for days.

**Prevention rules:**
1. Defense-in-depth is allowed but the log message MUST be unambiguous about what it caught and that the root cause is upstream. Bad: `LogWarning("ComputeMaxCarryYards failed")`. Good: `LogWarning("[DEFENSE] AeroConfig was zero-init when ComputeMaxCarryYards ran — root cause is upstream; this catch is a backstop, fix the source")`.
2. Every backstop-style catch deserves a TODO with a timestamp + name of the root-cause investigation owner.
3. When you find a backstop catch firing in production, **don't normalize it** — escalate to fix the root cause.

### Pattern D — Documented ownership models without wiring at every entry point

The controls_h iter-8 comment said "ApplyCameraYaw owns camera position during Aiming". That's a clear ownership model. But the implementation only invoked ApplyCameraYaw at TWO of the FOUR entry points into Aiming (`HandleCameraOrbit`, `HandleShotResolved(AtRest)`). The two missing — `SetupAtTee`, `PlaceBallAt` — are the entry points used when ENTERING Aiming after a hole load, which is exactly when the bug surfaces.

**Prevention rules:**
1. When refactoring writer ownership of any state (camera transform, ball position, UI mode, etc.), enumerate ALL entry points into the state. Comment them in the file. Audit each one explicitly.
2. For state with N entry points, the refactor commit should touch all N. If it touches only 2, ask: "which 3 didn't I touch and why not?"
3. Ownership comments are documentation, not verification. They describe intent; they don't catch wiring gaps.

---

## Process notes

- **`/effort=max` style debugging was correct here.** Each of the four bugs was non-obvious on its own; together they made the symptom look like one bug. Trying to fix the first thing that "could plausibly explain it" would have shipped the wrong fix three times.
- **Multi-hypothesis triage worked.** When the first DivideByZero theory (Code's NullRef-at-BallSimulation:214) didn't match the actual stack trace, we ran multiple inspection passes instead of guessing.
- **Diagnostic instrumentation paid for itself.** The `[AeroDiag]` and `[TeeDiag]` logs were not "wasted work"; they generated the evidence that ruled out hypotheses. Diagnostics-first approach when the failure mode is unclear.
- **Backstop catches concealed the root cause for at least one week** between when defense-in-depth was added and when this postmortem identified the mechanism. Tag aggressively.

## Commit map

| Commit | What | When |
|---|---|---|
| `063ff2ff` | Phase 6 BallAnimator (no child sweep in DestroyInstance) | original sin for Bug 3 |
| `1dba77d0` | Phase 7 Part F (`EnsureConfigsLoaded` short-circuit added) | sets up Bug 1 |
| `1f1c4fce` | Edit-Mode picker → controller notification | activates Bug 2 |
| `0b64566f` | controls_h iter-8 fallback (camera ownership) | introduces Bug 4 |
| `78a48b6e` | FIX Bug 1 — sentinel check on cached config | 2026-05-12 |
| `9f4160f4` | FIX Bug 2+3 — sweep ghosts + DontSaveInEditor | 2026-05-12 |
| `02f622df` | FIX Bug 4 — invoke ApplyCameraYaw on hole-load | 2026-05-12 |