# Physics Architecture — Research & Implementation Plan

**Status:** Research / planning — pre-spec, all design questions resolved
**Author:** Claude (Architect)
**Date:** 2026-04-21
**Context:** Before writing physics code, decide architecture. This doc captures the research, considered options, adversarial analysis, and the recommendation. Sources cited at bottom.

---

## TL;DR

- **Build physics first**, before gameplay. Gameplay has not started yet (per AI_CONTEXT). Building gameplay against placeholder physics means tuning two layers against each other.
- **Use a deterministic, custom-integrator ball flight model** in pure C# (not PhysX, not Unity Physics).
- **Skip Photon Quantum for v1.** It solves a problem we don't yet have (real-time predict/rollback) at the cost of a heavyweight ECS rewrite, vendor lock-in, and a soft floor on porting effort. Revisit only if we move from turn-based to real-time.
- **Use `long`-based fixed-point math** (FixFloat or Unity.Mathematics.FixedPoint) for the trajectory integrator. This buys cross-platform determinism without rewriting the rendering layer.
- **Keep PhysX for non-gameplay-critical effects only** — particles, ragdolls, ambient. Never for the ball.
- **5 phases**, each independently testable in a driving-range scene before integration.
- **Realism dial:** middle (sim-honest with assist layer on top). Toggleable for tournament play.
- **Tunability:** all physics knobs CSV-driven, hot-reloadable in Unity, headless-validatable.
- **Stat coupling:** Specialized Roles (Option D). Each stat owns a distinct physics input. See `PHYSICS_TUNING_TARGETS.md` Section 8.
- **Putt model:** reuse `BallSimulation` with a fast-path collapse to 2D rolling. Decouple later if it becomes painful.
- **Heightmap baking:** separate post-import tool (`PhysicsHeightmapBaker`) with per-hole / current-hole / all-holes menu options.
- **Workflow:** Claude Code now drives Unity directly via Unity-MCP — scene/component manipulation, script execution, test running, console reading, screenshots. Specs target the autonomous "implement → test → fix → report" loop. See Section 6.5.

---

## 1. Why physics first

Stronger conviction after reading AI_CONTEXT — gameplay is "Not started." There's nothing to break.

- **Substrate first.** Gameplay mechanics (flick controls, power gauge, club differentiation, spin selection) are *expressions* of the underlying physics. Build them on placeholder physics → tune two layers against each other → every physics iteration breaks gameplay tuning.
- **Confluence backlog is physics debt.** Centered shot drifting right by default, sidespin not working, ball-on-asphalt misclassified, wind disabled since 1.2.0.2, tee shots needing launch-angle modifier. Cleaner to fix in fresh code than inherit.
- **Measurable validation.** Physics has a ground truth — does a 7-iron at power 80 carry 150 yards? Gameplay feel is subjective. Lock down the objective layer first.

Counter-argument considered: build gameplay first with a physics stub to validate control feel. Rejected because flick controls were already validated in older builds (1.1.1.1+). Control scheme is not the unknown — physics fidelity is.

---

## 2. Determinism: required, not optional

Even though multiplayer is "at some point," the cost of retrofitting determinism is enormous. The cost of building it in from day one is small. This is a one-way door.

### Why deterministic for a turn-based golf game

| Concern | Deterministic | Non-deterministic |
|---|---|---|
| **Bandwidth per shot** | ~30–50 bytes (inputs + seed) | ~4 KB (trajectory samples) |
| **Replay storage** | Tiny (re-simulate from seed) | Large (store full trajectory) |
| **Anti-cheat** | Server re-simulates, verifies | Trust client OR run authoritative sim per shot |
| **Tournament integrity** | Replays prove the shot was clean | Disputes are unverifiable |
| **Spectator mode** | Stream inputs only | Stream trajectory data |
| **Networking complexity** | Trivial (turn-based, no lockstep needed) | Same |

Two orders of magnitude bandwidth difference. On JP/SEA mobile data plans, this matters. And golf is turn-based — players don't take simultaneous shots — so we get the determinism win without the brutal real-time lockstep/rollback complexity that makes Photon Quantum worth its weight in shooters.

### Cost of determinism

- **Cannot use Unity PhysX directly for the ball.** PhysX is non-deterministic across platforms (iOS ARM vs Android ARM vs Windows x86 builds give different float results).
- **Cannot use `UnityEngine.Random`.** Use seeded `System.Random` per shot, or better, a deterministic PRNG that's identical across platforms.
- **Cannot use `Mathf.Sin`/`Cos`/`Sqrt` directly.** These call platform math libraries with different implementations. Use a deterministic math lib.
- **Must avoid `HashSet`/`Dictionary` iteration in sim code** (order is implementation-defined).
- **Build flags matter.** IL2CPP with FastMath off, consistent settings across platforms.

Manageable for a 1-ball, 1-shot-at-a-time simulation. It would be hell for a 100-entity RTS, but we have one ball.

### Determinism choice: fixed-point vs soft-floats

Two viable options:

**Option A: Fixed-point math (`long`-backed Q48.16 or Q24.8)**
- Libraries: iShape FixFloat, Unity.Mathematics.FixedPoint, or roll our own (Photon Quantum's FP type is also Q48.16).
- Pros: Bit-perfect identical results on every platform. Mature pattern (used in fighting games for decades). Burst-compatible.
- Cons: Slightly verbose syntax. Need lookup tables for sin/cos. Limited dynamic range — but golf coordinates are bounded (~1km × 1km × 200m max), well within Q48.16.

**Option B: Soft floats**
- Library: unity-deterministic-physics by Kimbatt.
- Pros: Drop-in float replacement, no syntax change.
- Cons: Slower than fixed-point. Designed for full-physics scenes — overkill for single-ball trajectory. Less battle-tested.

**Recommendation: fixed-point.** Single-ball trajectory is small enough that the syntax overhead doesn't compound. Bit-perfect is bit-perfect. Photon Quantum's FP is also Q48.16, so if we ever migrate to Quantum, the math values are directly portable.

### Adversarial: "what if I want non-determinism for visual flair?"

E.g., random ball-trail particle jitter, wind grass animation, crowd cheer randomness. **Fine.** Determinism only applies to the trajectory simulation. The Unity rendering/VFX layer can use whatever non-deterministic Unity APIs it wants. The architectural rule is: simulation outputs `Trajectory { samples[], finalPos, terrainHits[] }`; rendering consumes that. Anything else the renderer does is untracked and that's fine.

---

## 3. The trajectory model

Researched real golf simulator equations and academic papers. Summary of what we need:

### Forces on the ball in flight

```
F_total = F_gravity + F_drag + F_lift + F_wind
```

- **Gravity:** `F_g = m·g` (constant, downward). m = 0.04593 kg (USGA max), g = 9.80665 m/s².
- **Drag:** `F_d = -½·ρ·A·Cd·|v|·v` (opposes velocity). ρ = air density (1.225 kg/m³ at sea level, 15°C), A = ball cross-section (~0.001432 m²), Cd ≈ 0.21–0.30 depending on Reynolds number for a dimpled ball.
- **Lift (Magnus):** `F_l = ½·ρ·A·Cl·|v|² · (ω̂ × v̂)` where ω is spin axis. Backspin → lift up (extends carry). Sidespin → curve. Cl ≈ 0.10–0.25 typically.
- **Wind:** integrated by replacing `v` with `v_relative = v_ball - v_wind` in drag and lift terms.

### Coefficients

For a dimpled golf ball, Cd and Cl are functions of Reynolds number (Re = velocity-dependent). For game purposes, two viable simplifications:

1. **Constant coefficients** (Cd = 0.25, Cl = 0.20). Simplest. Gets ~80% realism.
2. **Lookup tables** indexed by `|v|` and spin rate. Gets ~95% realism. Tables can be derived from published PGA Tour data (Trackman).

**Recommendation: start with constants in Phase 1, swap to LUTs in Phase 2 once we validate the integration loop.**

### Integration

4th-order Runge-Kutta (RK4) at fixed dt = 1/240s (or even 1/480s for safety). RK4 is overkill numerically but cheap (we're integrating one ball for ~6 seconds = 1440 steps), and it's the standard choice in the golf simulation literature.

Symplectic Euler is the cheaper alternative if profiling demands it, but RK4 keeps energy/spin behavior cleanly bounded over long flights.

### Surface interaction (post-flight)

After the ball lands, switch from flight integrator to a surface-interaction model:

1. **Bounce** — coefficient of restitution per surface (green ≈ 0.45, fairway ≈ 0.50, semi-rough ≈ 0.35, rough ≈ 0.25, sand ≈ 0.15, cart path ≈ 0.65). Friction also per surface. All values CSV-tunable.
2. **Roll** — once vertical velocity drops below threshold and ball stays in contact, switch to roll model. Rolling resistance per surface; slope reads from baked deterministic heightmap (NOT `terrain.SampleHeight()`).
3. **Stop condition** — velocity below 0.05 m/s and on a near-flat surface.

The zone overlay system (already built — fairway/green/bunker/water/cart-path meshes with `SurfaceMarker` components) is the surface lookup. Cast a ray downward from the ball, get the surface marker, get the coefficients.

### Putting

**Decision (locked 2026-04-21):** Reuse `BallSimulation` with a fast-path collapse to 2D rolling. Same trajectory data structure, same surface-interaction code, same coefficient tuning surface. The "this is a putt" condition is detected automatically from the input (low velocity + low launch angle + ball already on green) and the integrator skips the airborne aerodynamics block.

**Why reuse:** A putt running off the back of the green is the same physics as a slow chip — having both code paths agree on what happens at the green/fringe boundary is one less thing that can desync. Slope-reading bugs get fixed in one place, not two. One trajectory shape to learn.

**Escape hatch:** if the fast-path inside `BallSimulation` becomes a tangled mess (e.g., putt-only tuning starts polluting flight code), decouple into a separate `PuttSimulation` class at that point. The `Trajectory` output type stays identical so callers don't notice. **Decoupling is reversible; coupling is not.** Start coupled.

---

## 4. Existing implementations / libraries surveyed

| Project | Pros | Cons | Use? |
|---|---|---|---|
| **brogan89/Golf-Mechanics** (GitHub) | Open-source Unity C# golf physics. Driving range + putting scenes. Working web demo. | Built on Unity PhysX (non-deterministic). Older project (~2018). License not stated clearly. | **Reference only** — read the math, don't import the code. |
| **Photon Quantum Golf Sample** | Official deterministic Unity sample, exactly our problem domain. Turn-based. Ball physics in fixed-point. | Locked to Photon Quantum stack. ECS rewrite. Vendor pricing past 100 CCU. | **Read the code** for math/architecture, don't adopt the stack. |
| **iShape FixFloat** | MIT-ish, Burst-compatible, `long`-backed `FixNumber`. | 2D-focused, would need 3D wrappers. | **Strong candidate** for the math library. |
| **Unity.Mathematics.FixedPoint** (danielmansson) | Mirrors Unity.Mathematics API, fp3/fp4/fpquaternion built-in. | Less mature, limited test coverage. | **Strong candidate**, possibly preferred — better API match for Unity devs. |
| **Kimbatt unity-deterministic-physics** | Soft-float Unity DOTS Physics fork. Drop-in solution. | Heavyweight. We don't need DOTS Physics; we need one ball. | **Skip.** Wrong scale. |
| **Unity PhysX (default)** | Free, integrated, well-known. | Non-deterministic across platforms. | **Skip for ball.** Keep for non-gameplay (particles, ambient ragdolls). |
| **Unity Physics (DOTS/ECS)** | Newer, better than PhysX in some ways. | Still non-deterministic without modifications. ECS rewrite. | **Skip.** |

### Why not just use Photon Quantum?

I was tempted. They literally have a published Golf Sample. Their physics is fixed-point Q48.16. Their networking is solved.

But:

- **ECS rewrite.** Quantum is sparse-set ECS with code generation from a custom DSL (.qtn files). Our codebase is MonoBehaviour singletons (CharacterManager, BagManager, etc.) per AI_CONTEXT. Migrating to Quantum's ECS is not "add a package" — it's a fundamental rearchitecture of every system that touches game state.
- **Vendor lock-in.** Free up to 100 CCU. After that, $0.50/CCU/month. For a partner-app integration with potentially large user base, that's a real cost. And there's no portable runtime — the Photon servers are required.
- **Solo-dev complexity.** Quantum is a beast designed for studios shipping competitive multiplayer. We're a solo dev whose multiplayer is "two people taking turns."
- **Solves the wrong problem.** Quantum's headline value is predict/rollback for real-time sub-100ms latency. Turn-based golf doesn't need that — we just need "given inputs, both clients compute the same trajectory."

**The 80/20 play:** roll our own deterministic physics (small, contained, ~1500 LOC), use any commodity networking (Photon Realtime / Unity Netcode / even REST) for turn exchange. Total cost ≈ free, lock-in ≈ none. If we ever pivot to real-time multiplayer (unlikely for golf), revisit Quantum then — the math values port directly because we're using the same Q48.16 representation.

---

## 5. Adversarial considerations

### What could kill this approach?

**"Fixed-point math is too slow for mobile."**
One ball, ~1500 RK4 steps per shot, ~12 fixed-point ops per step. ~18,000 ops per shot. Trivially fast on any modern phone. We compute the entire trajectory in <5ms and then animate it. Profile to confirm but no realistic concern.

**"Determinism breaks the second we touch terrain.SampleHeight()."**
This is the real risk. `terrain.SampleHeight()` is Unity API and likely non-deterministic across platforms. Mitigation: at hole import time, bake the heightmap into a deterministic 2D `fp[,]` array (already exists as `heights` in the importer — same 2049×2049 grid). Sample our deterministic grid in sim code, not Unity's terrain. Renderer can still use Unity terrain for visuals.

**"Wind feels random — won't determinism make it predictable and boring?"**
Wind has a base direction + magnitude (per-hole, per-round) and per-shot gust variation. The gust uses a per-shot seed derived from `(matchSeed, holeNumber, shotNumber, playerId)`. Deterministic but not predictable to the player. Same trick used by Spelunky-likes for "random but verifiable" runs.

**"What about lie-induced shot variance? Hitting from rough should be inconsistent."**
Same trick. Lie variance is a deterministic function of `(seed, lie_type, character_skill)`. The variance is real but reproducible — which is exactly what you want for replays and anti-cheat.

**"What if we want PhysX-driven ragdolls or particles?"**
Fine. Those don't affect the ball trajectory — they're presentation. Renderer can use whatever Unity APIs it wants. Architectural rule: **the ball trajectory and final resting position are computed by the deterministic sim and only by the deterministic sim.** Everything else can be non-deterministic.

**"What if the user disconnects mid-match?"**
Each shot's trajectory is fully captured by `ShotInput { ... }`. Persist the input log per match. On reconnect, replay from the log to reconstruct state. This is a free side benefit of determinism.

**"Solo dev — can I really write a custom physics engine?"**
We're not writing a physics engine. We're writing a 1-ball trajectory integrator + a surface-interaction model. ~1500 LOC total estimate. The math is in the references at the bottom. The integrator is a textbook RK4. The hard part is tuning the coefficients to feel right, which we'd have to do regardless of which physics framework we use.

### Game design considerations

**Realism dial — middle, with assist toggle (locked 2026-04-21).**
Sim-honest physics (the simulation always runs the real model). Assist layer on top: ghost trajectory preview, predicted landing zone, lie effect indicators, the putter "gravity well." Toggleable per round. Tournament/competitive modes force assists OFF. The architectural rule is: **assists are in the rendering/UI layer, never in the sim.** The ball flies the same way whether assists are on or off — the player just sees more or fewer hints.

**Tunability — CSV-driven, hot-reloadable, headless-validatable (locked 2026-04-21).**
All physics constants live in CSVs under `Assets/Resources/Physics/`. A `Window > Physics > Tuning` EditorWindow exposes every knob with sliders, hot-reloads CSVs in Play Mode, and lets designers save/load presets. A headless validation tool fires N shots through the sim and asserts club carry distances stay within tolerance — designers can tune freely without breaking driver yardage.

**Stat coupling — Specialized Roles, Option D (locked 2026-04-21).**
Each stat owns a distinct physics input. Multiplicative stacking only when stats genuinely share a lane (Club Power × Ball Power, Club Accuracy × Character Club Control). No "everything multiplies everything" stacking. Hard caps per stat to keep endgame numbers sane. Full mapping in `PHYSICS_TUNING_TARGETS.md` Section 8.

**The "flick to shoot" control needs to map to physics inputs.**
Per Confluence, the existing control is: pull back power gauge, flick forward. The flick produces:
- A magnitude (0–120% power) → club head speed
- A direction → aim offset (drift left/right)
- A center-tap accuracy → spin axis (off-center = side spin)

These are all clean inputs to the physics sim. No coupling concerns.

**Putt model needs special care.**
Confluence flags many real-feel issues with putting. Reading green slope is a key gameplay loop. If physics is right, putting feels right. If physics is wrong, no UI polish saves it. This is the most physics-sensitive gameplay element and worth budgeting extra polish time for.

**Don't over-engineer Magnus.**
Real golf has a "reverse Magnus" regime at certain Reynolds numbers. Skip it. Players cannot perceive the difference, and it would only manifest on extremely off-axis spin shots no one will hit on purpose.

---

## 6. Implementation plan — 5 phases (+ heightmap baker)

Each phase ends with a testable scene (`Assets/Scenes/Physics/PhaseN_Test.unity`), a console-runnable validation harness, and a checkpoint with sign-off before next phase.

### Phase 0 — Physics heightmap baker (prerequisite for Phase 4)
**Scope:** new editor tool that reads imported terrain heightmaps and writes deterministic `fp[,]` versions to disk. Separate from `HoleGeoImporter` to keep that file from growing further.
**Deliverable:** new file `Assets/Scripts/Editor/CourseImporter/PhysicsHeightmapBaker.cs`. Three menu items:
- `Import > Bake Physics Heightmap > Bake Current Hole`
- `Import > Bake Physics Heightmap > Bake Hole 01..18` (sub-menu, one per hole)
- `Import > Bake Physics Heightmap > Bake All Holes`
Reads the imported `TerrainData.GetHeights(...)` from the active scene (or opens scenes one-by-one for batch mode), converts to fixed-point, writes `<exportPath>/heightmap.bytes` next to the existing per-hole export files.
**Validation:** round-trip read-back matches source heightmap within 1mm tolerance for 100 random sample points. File size predictable (2049 × 2049 × 4 bytes for Q24.8 ≈ 16 MB; consider Q16.16 if size matters).
**Risk:** file size. 16 MB per hole × 18 holes = 290 MB on disk. Mitigations: (1) downsample to 1025×1025 for physics (halves precision but golf doesn't need sub-15cm terrain reads); (2) use Q16.16 instead of Q48.16 for storage (still bit-precise within ±32km range, fine for golf); (3) zlib-compress the bytes file. Pick during implementation based on actual size.
**Out of scope:** runtime loading. The `HeightProvider` MonoBehaviour that reads `heightmap.bytes` at scene load is Phase 4's concern.
**Unity-MCP usage:** Claude Code drives the entire bake loop via `editor-application-set-state` (force EditMode), `script-execute` (invoke menu item directly), `console-get-logs` (verify success), and re-runs on any failure. No human intervention until result is reported. See Section 6.5.

### Phase 1 — Vacuum trajectory + driving range scene
**Scope:** ball flight in vacuum (gravity only). RK4 integrator. Fixed-point math.
**Deliverable:** `Golfin.Physics.BallSimulation` class, pure C#, no MonoBehaviour. Method `Trajectory Simulate(ShotInput input, GroundProvider ground)`. Driving-range test scene that hits a ball with configurable initial velocity + launch angle and renders the trajectory as a line.
**Validation:** trajectory matches `range = v² · sin(2θ) / g` projectile equation within 1% over 1000 random inputs.
**Out of scope:** drag, lift, wind, surfaces, anything else. Pure parabola.
**Unity-MCP usage:** Claude Code builds the driving-range test scene via `scene-create` + `gameobject-create` + `gameobject-component-add` (ground plane, ball spawn, camera, trajectory line renderer). Runs validation tests via `tests-run`. Captures result via `screenshot-game-view` for visual confirmation.

### Phase 2 — Aerodynamics
**Scope:** add drag and Magnus lift. Constant coefficients first, then swap to lookup tables. CSV-driven coefficients.
**Deliverable:** updated `BallSimulation` with `aero.csv` (Cd, Cl, ρ, A, m). Driving range now matches `PHYSICS_TUNING_TARGETS.md` Section 1 carry-distance targets within 5% for typical shots.
**Validation:** validation harness compares simulated trajectories against `PHYSICS_TUNING_TARGETS.md` Section 7 PGA Tour averages. Test cases: 14 club types × 3 power levels × 3 spin rates = 126 trajectories. Headless-runnable.
**Risk:** coefficient tuning. Allocate 1–2 days for tuning before declaring done.
**Unity-MCP usage:** Claude Code runs the tuning loop autonomously — adjust CSV value → trigger validation harness via `script-execute` → read results from `console-get-logs` → adjust → repeat until carry distances are within tolerance. Cesar reviews the final coefficient set, not every iteration.

### Phase 3 — Wind
**Scope:** wind direction + magnitude, altitude variation (optional), per-shot gust variation from seeded PRNG.
**Deliverable:** `WindModel` class. `ShotInput` extended with `windSeed`. Driving range now has a wind indicator and headwind/tailwind/crosswind change ranges measurably.
**Validation:** 10 m/s headwind reduces driver carry by ~25–35yd (matches real golf). Crosswind drift matches expected lateral displacement.
**Unity-MCP usage:** Same tuning loop pattern as Phase 2.

### Phase 4 — Surface interaction (bounce & roll)
**Scope:** ball lands, bounces, rolls, stops. Per-surface coefficients (restitution, friction, rolling resistance) in `surfaces.csv`. Reads zone meshes via `SurfaceMarker` components (already in codebase). Reads slope from baked deterministic heightmap (`HeightProvider` consumes Phase 0's `heightmap.bytes`).
**Deliverable:** `SurfaceInteraction` class + `HeightProvider` MonoBehaviour. Trajectory includes `terrainHits[]` array of all bounces with surface type. Test scene on Hole 1 — hit ball from tee, watch it land, bounce, roll, stop in correct surface zone.
**Validation:** ball lands on green and stays. Ball lands on cart path and bounces ~70% of incoming velocity. Ball lands in water → physics stops, penalty system handles outcome (penalty system is separate, not this phase).
**Coupling note:** depends on Phase 0's heightmap bake. The bake step needs to be re-runnable per hole during physics development — that's why Phase 0 has the per-hole/current-hole/all-holes options.
**Unity-MCP usage:** Claude Code opens Hole 1 scene via `scene-open`, fires test shots via `script-execute`, validates landing zones by reading `SurfaceMarker` components via `gameobject-component-get`. Screenshots both Game and Scene view to verify visually.

### Phase 5 — Putting (fast-path inside `BallSimulation`)
**Scope:** add putt detection + 2D-rolling fast-path inside `BallSimulation`. Slope read via deterministic heightmap gradient. Putt-tuned parameters via `putt.csv`.
**Deliverable:** Putt test scene on Hole 1 green. Slope-aware putts curve correctly. Same `BallSimulation.Simulate()` entry point — no new public API.
**Validation:** flat 3m putt at calibrated power = ball stops at hole within 30cm. Sloped putt curves the right direction by the right amount. Putts that run off the green transition cleanly to the regular flight/roll code path.
**Decoupling escape hatch:** if putt-specific tuning starts polluting the flight integrator beyond ~50 LOC of branched logic, extract `PuttSimulation` class with the same `Trajectory` output shape. Reversible. No callsite changes needed.
**Unity-MCP usage:** Same scene-open + script-execute + screenshot pattern as Phase 4.

### Total estimate (revised — Unity-MCP shortens iteration loops)
- Phase 0: 0.5 day (was 1 — Claude Code can write & run the baker autonomously)
- Phase 1: 1.5 days (was 2)
- Phase 2: 3 days (was 4 — autonomous tuning loop)
- Phase 3: 1 day (was 1.5)
- Phase 4: 3 days (was 4)
- Phase 5: 1.5 days (was 2)

≈ **10–11 working days** for a fully validated, deterministic ball physics layer. Cesar's role shifts from "implement and test" to "review and design-decide" on each phase boundary. After this, gameplay (controls, club selection, shot UI, scorecard) can be built on a stable substrate.

---

## 6.5 Workflow change — Claude Code now drives Unity directly via Unity-MCP

Claude Code has access to Unity-MCP (https://github.com/IvanMurzak/Unity-MCP), a bridge that exposes 50+ Unity Editor tools as MCP functions. This materially changes the implementation workflow.

### Tools relevant to physics development

| Category | Tool | Use in physics work |
|---|---|---|
| **Scripting** | `script-update-or-create` | Write `BallSimulation.cs`, integrator, etc. |
| | `script-execute` | Run arbitrary C# via Roslyn — fire test shots, compute carry, check trajectory math without writing a full test harness |
| | `tests-run` | Execute EditMode/PlayMode tests directly; read pass/fail per test |
| | `console-get-logs` | Read compile errors, runtime exceptions, Debug.Log output — self-correct without round-tripping through Cesar |
| | `reflection-method-call` | Call any C# method (incl. private) — bypass the need for public test hooks |
| **Scene** | `scene-create` / `scene-open` / `scene-save` | Build per-phase test scenes (`Phase1_Test.unity`, etc.) |
| | `gameobject-create` / `gameobject-component-add` / `gameobject-modify` | Programmatically build driving range, place ball, attach scripts |
| | `editor-application-set-state` | Enter/exit Play Mode for runtime validation |
| **Validation** | `screenshot-game-view` / `screenshot-scene-view` / `screenshot-camera` | Visual proof of correct trajectory, ball resting position, slope curvature |
| | `editor-selection-set` / `editor-selection-get` | Inspect specific objects after a shot |
| **Project** | `assets-find` / `assets-create-folder` / `assets-modify` | Manage `Assets/Resources/Physics/*.csv` |
| | `package-add` | Install `Unity.Mathematics.FixedPoint` from git URL — no manual Package Manager step |

### What this means for the workflow loop

**Before Unity-MCP:**
1. Architect Claude writes spec to `Docs/TellCode.md`
2. Claude Code reads spec, writes/edits files
3. Claude Code reports "done"
4. Cesar opens Unity, manually creates the test scene, runs the test, reports results
5. Loop back to step 2 if anything failed

**With Unity-MCP:**
1. Architect Claude writes spec to `Docs/TellCode.md` (unchanged)
2. Claude Code reads spec, writes/edits files
3. **Claude Code creates the test scene, runs the tests, reads the logs, fixes errors, re-runs, screenshots the result**
4. Claude Code reports "done" with screenshot evidence and validation harness output
5. Cesar reviews the final state — design-level approval, not implementation babysitting

The number of human-in-the-loop steps drops dramatically. This compresses every phase by ~25–35% (Section 6 estimates updated accordingly).

### Implications for spec writing (`TellCode.md`)

Specs now include explicit **autonomous validation criteria** Claude Code must satisfy before reporting done:

- "Run `tests-run` on `ProjectileMathTests`, all 1000 cases must pass"
- "Capture `screenshot-game-view` after firing a shot at 30°/30 m/s; attach to status report"
- "Verify `console-get-logs` shows zero errors after `script-execute` of validation harness"
- "If validation fails, `reflection-method-call` to inspect intermediate state before reporting"

Claude Code is expected to iterate autonomously on its own implementation until the autonomous validation passes. Only then does it report back. If it can't make the validation pass after a reasonable number of attempts (define per-spec, e.g. "max 5 iterations"), it reports the failure with diagnostic detail rather than a vague "didn't work."

### What does NOT change

- **Architecture decisions** (deterministic, fixed-point, custom integrator, 5-phase plan, stat coupling, tuning targets). All still apply. Unity-MCP is a workflow tool, not a physics decision.
- **Cesar's role as design authority.** Unity-MCP can run the tests but it can't tell us what "feels right" for putting. Design judgment stays with you.
- **The decision to keep simulation code Unity-independent.** Sim core still imports zero Unity APIs. Unity-MCP doesn't make Unity APIs deterministic — it just lets Claude Code drive them more easily.
- **The handoff dance:** Architect Claude → `TellCode.md` → Claude Code. The spec format is unchanged; Claude Code now has more tools to execute against it.

### Adversarial: "what could go wrong with autonomous Claude Code?"

- **Infinite loops on unfixable bugs.** Mitigation: every spec includes a max-iteration cap and a "report failure with diagnostics" exit condition.
- **Drift from spec intent.** Mitigation: specs include validation criteria explicit enough that "it compiles and tests pass" cannot be interpreted as "task done" if behavior is wrong. Screenshots are part of the report so visual sanity-check is preserved.
- **Side effects in scenes / project.** Mitigation: each phase uses a dedicated test scene under `Assets/Scenes/Physics/`. Any scene Claude Code creates is namespaced. CSV writes go to `Assets/Resources/Physics/` only.
- **Editor crashes during long autonomous runs.** Real risk — Unity is Unity. Specs should checkpoint progress (e.g., commit between holes during Phase 0 batch bake).
- **Scope creep.** Claude Code, given more autonomy, may "improve" things outside the spec. Mitigation: specs include explicit "DO NOT change" lists like the existing TellCode.md tasks already do. Pattern is established.

---

## 7. Architecture sketch

```
Assets/Scripts/Physics/
├── Core/
│   ├── BallSimulation.cs         // pure C#, no MonoBehaviour, no UnityEngine
│   ├── ShotInput.cs              // input DTO
│   ├── Trajectory.cs             // output DTO (samples + bounces + final state)
│   ├── AeroModel.cs              // drag + lift forces
│   ├── WindModel.cs              // wind sampling
│   ├── SurfaceInteraction.cs     // bounce + roll model
│   └── DeterministicPRNG.cs      // seeded, platform-stable RNG
├── Math/
│   └── (fixed-point math lib — package import via Unity-MCP `package-add`)
├── Tuning/
│   ├── PhysicsConfigLoader.cs    // CSV → typed config objects, hot-reloadable
│   ├── PhysicsTuningWindow.cs    // EditorWindow: sliders, presets, hot-reload
│   └── ValidationHarness.cs      // headless N-shot validator (callable via Unity-MCP `script-execute`)
├── Stats/
│   └── StatModifierResolver.cs   // raw stats → effective physics modifiers (Section 8 of TUNING_TARGETS)
├── Runtime/
│   ├── BallView.cs               // MonoBehaviour, animates ball along Trajectory
│   ├── HeightProvider.cs         // loads baked heightmap.bytes at scene load
│   ├── ShotController.cs         // takes UI inputs → builds ShotInput → runs sim → hands Trajectory to BallView
│   └── AssistRenderer.cs         // ghost trajectory, predicted landing zone, etc. — toggleable
└── Tests/
    ├── ProjectileMathTests.cs    // Phase 1 — runnable via Unity-MCP `tests-run`
    ├── TrackmanComparisonTests.cs // Phase 2
    ├── WindEffectsTests.cs        // Phase 3
    ├── SurfaceBounceTests.cs      // Phase 4
    └── PuttCurvatureTests.cs      // Phase 5

Assets/Scripts/Editor/CourseImporter/
└── PhysicsHeightmapBaker.cs       // Phase 0 — per-hole/current/all menu items, invokable via Unity-MCP `script-execute`

Assets/Resources/Physics/
├── aero.csv                       // Cd, Cl LUTs
├── ball.csv                       // mass, area, ball stat coefficients
├── clubs.csv                      // per-club base velocity, loft, etc.
├── surfaces.csv                   // bounce/friction/roll per surface zone
├── stats.csv                      // stat → modifier coefficients + caps
├── wind.csv                       // wind base parameters, gust variance
└── putt.csv                       // putt-specific parameters

Assets/Scenes/Physics/
├── Phase1_VacuumTest.unity        // Built programmatically by Claude Code via Unity-MCP
├── Phase2_AeroTest.unity
├── Phase3_WindTest.unity
├── Phase4_SurfaceTest.unity       // Loads on top of Hole 1 scene
└── Phase5_PuttTest.unity          // Loads on top of Hole 1 scene
```

**Key architectural rule:** `BallSimulation`, `ShotInput`, `Trajectory`, and everything in `Core/`, `Math/`, and `Stats/` reference **zero Unity APIs**. Pure C#. Headlessly testable on a build server. This is the same pattern Photon Quantum uses (their sim is its own .NET project) and it's the right pattern even without Quantum.

---

## 8. What we're NOT doing

- **Not using Photon Quantum** (overkill, lock-in, ECS rewrite required).
- **Not using Unity PhysX for the ball** (non-deterministic).
- **Not using floats anywhere in the simulation core** (non-deterministic across platforms).
- **Not modeling dimples/CFD** (academic, no perceivable game value).
- **Not modeling reverse Magnus** (negligible, only matters at edge-case spins).
- **Not building a physics engine** — building a 1-ball trajectory integrator with surface interaction. Different problem, much smaller.
- **Not networking anything yet.** Determinism is the substrate; multiplayer comes later.
- **Not separating the putt model preemptively.** Reuse `BallSimulation` with a fast-path. Decouple only if it gets messy.
- **Not baking assists into the simulation.** Sim is honest. Assists are rendering layer.
- **Not adding heightmap baking to `HoleGeoImporter`.** Separate tool, separate concern, doesn't bloat an already-large file.
- **Not putting Cesar in the iteration loop for autonomous validations.** Claude Code drives the test/fix/screenshot cycle via Unity-MCP. Cesar reviews phase-completion reports, not every test run.

---

## 9. Open questions for Cesar — all resolved

### Resolved (2026-04-21)
1. ✅ **Realism dial:** Middle, with assist toggle. Sim-honest, assists in the UI layer.
2. ✅ **Tunability:** CSV-driven knobs, hot-reloadable Unity EditorWindow, headless validator.
3. ✅ **Trackman data:** A+B — public Trackman averages as targets, academic papers' coefficients as starting parameters, then tune to feel.
4. ✅ **Stat coupling:** Specialized Roles (Option D). See `PHYSICS_TUNING_TARGETS.md` Section 8.
5. ✅ **Putt model:** Reuse `BallSimulation`, decouple later if needed.
6. ✅ **Heightmap baking:** Separate post-import tool (`PhysicsHeightmapBaker`) with per-hole / current-hole / all-holes menu options. See Phase 0 in Section 6.
7. ✅ **Unity-MCP workflow:** Claude Code uses Unity-MCP for scene building, script execution, test running, screenshot validation. Specs require autonomous validation before "done" reports. See Section 6.5.

**Status:** Ready to write Phase 0 + Phase 1 specs into `Docs/TellCode.md`.

---

## 10. References

### Open-source code referenced
- brogan89/Golf-Mechanics — https://github.com/brogan89/Golf-Mechanics (Unity C# golf physics, PhysX-based, reference only)
- iShape FixFloat — https://github.com/iShapeUnity/FixFloat (deterministic fixed-point math)
- Unity.Mathematics.FixedPoint (danielmansson) — https://github.com/danielmansson/Unity.Mathematics.FixedPoint
- Kimbatt unity-deterministic-physics — https://github.com/Kimbatt/unity-deterministic-physics (DOTS soft-float fork)
- IronWarrior UnityCrossPlatformDeterministicFloats — https://github.com/IronWarrior/UnityCrossPlatformDeterministicFloats (test suite)

### Unity-MCP
- IvanMurzak/Unity-MCP — https://github.com/IvanMurzak/Unity-MCP (Claude Code's Unity Editor bridge)
- Default tools reference — https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/default-mcp-tools.md

### Photon Quantum docs
- Quantum 3 intro — https://doc.photonengine.com/quantum/current/quantum-intro
- Quantum Golf Sample — https://doc.photonengine.com/quantum/v1/demos-and-tutorials/turn-based-framework/golf-sample
- Quantum pricing — https://www.photonengine.com/quantum/pricing

### Physics references
- GSA Golf — physics overview — https://www.golf-simulators.com/physics.htm
- Werner (2007) Flight Model of a Golf Ball — http://www.physics.csbsju.edu/~jcrumley/222_2007/projects/awwerner/project.pdf
- Burglund & Street (2011) Golf Ball Flight Dynamics — https://www.math.union.edu/~wangj/courses/previous/math238w13/Golf%20Ball%20Flight%20Dynamics2.pdf
- IJIMT (2013) Flight Trajectory of a Golf Ball for a Realistic Game — https://www.ijimt.org/papers/419-D0260.pdf (RK4 integrator + dimple effects)
- USPTO 6,186,002 — Method for determining coefficients of lift and drag of a golf ball — https://patents.google.com/patent/US6186002B1/en
- Simulations4All Golf Ball Flight Simulator — https://simulations4all.com/simulations/golf-ball-flight-simulator (Trackman-validated reference data)
- Magnus effect / dimples deep-dive — https://www.engineered-mind.com/fluid-mechanics/the-magnus-effect-ball-design-turbulence/

### Determinism background
- Shaderfun — Understanding Determinism Part 1 — https://shaderfun.com/2020/10/25/understanding-determinism-part-1-intro-and-floating-points/
- Unity Discussions — Soft floats & determinism — https://discussions.unity.com/t/soft-floating-points-calculations-and-determinism/878679
- Unity Discussions — IL2CPP float determinism — https://discussions.unity.com/t/are-floating-point-numbers-deterministic-on-the-same-architecture-il2cpp/892251

### Mobile golf game design references
- WGT Golf — https://en.wikipedia.org/wiki/World_Golf_Tour
- Existing Confluence docs in project (`Golfin - Confluence.txt`) — gameplay mechanics, penalty system, hitting/driving/putting design briefs, build notes documenting historical physics issues

### Tuning targets
- `PHYSICS_TUNING_TARGETS.md` — canonical numbers (carry distances, stat coefficients, RP costs, surface coefficients, stat-stacking model). Companion document.

---

## Recommendation

Build physics first, deterministic, custom integrator with fixed-point math. 5-phase plan + Phase 0 baker, ~10–11 working days with Unity-MCP-accelerated workflow, results in a fully validated and headlessly testable physics layer that gameplay can then be built on with confidence. Skip Photon Quantum for v1. Skip PhysX for the ball. Keep everything multiplayer-ready by construction without paying the multiplayer-framework tax up front. Claude Code drives the implement-test-fix-screenshot cycle via Unity-MCP; Cesar reviews phase-completion reports.

**All design questions resolved.** Ready to write Phase 0 spec into `Docs/TellCode.md` on your go-ahead.
