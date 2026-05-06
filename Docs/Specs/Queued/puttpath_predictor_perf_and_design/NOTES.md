# PuttPathPredictor — Perf Measurement + Design Redesign — Architect NOTES

**Status:** PRE_SPEC — created as spinoff from §2b NOTES round 2 (2026-05-07).
**Architect (claude.ai), 2026-05-07 JST**

Spun out of §2b camera transitions because Cesar flagged: "PathPredictor needs work. We shipped but it eats a lot of processor and might be too much. We need to check what other games do."

Two threads, can ship together or separately.

---

## Thread 1 — Perf measurement (already a Putter P1 follow-up)

Per `Docs/TellCode.md` B-followups: "Profiler session on `BallSimulation.Simulate` over 60 frames of active aiming. If p95 > 5 ms on editor target, throttle."

Status: queued, never actioned. PuttPathPredictor calls into the full bit-exact sim every aim frame to compute the predicted ball trajectory on the green. p95 budget is 5ms; if exceeded, throttle to N Hz instead of every frame.

**Likely true:** sim is hot. Bearman-Harvey aero LUT lookups + RK4 airborne + per-step roll integration over a typical putter shot (= a few hundred sim steps at 240 Hz internal dt for a 5–15m putt) is non-trivial. Easily 1–5ms per frame, probably more.

**If throttle needed:** options range from "recompute at 30Hz instead of 60Hz" (cheap, halves cost, still smooth) to "recompute only when aim yaw delta exceeds threshold" (smart, eliminates redundant recomputes during static aim).

---

## Thread 2 — Design redesign (sim convention research)

Cesar's instinct ("might be too much") aligns with industry practice:

| Game | Putt aim assist UX |
|---|---|
| **PGA Tour 2K23/25** | Grid + slope arrows on green; target dots (not full curve) |
| **EA Sports PGA Tour** | Grid + slope arrows |
| **WGT Golf** | Grid + colored arrows + slope severity (color-coded thickness) |
| **Mario Golf** | Grid lines, no predicted trail |
| **Everybody's Golf** | Slope arrows on the line |
| **Arccos / AimPoint apps** (real-world tools) | Heat map + arrows; no predicted trail |
| **Most arcade mobile games** | Full predicted curve |

Pattern: **sim convention is grid + slope arrows**. Predicted-line UX is **arcade territory**. Cesar's PuttPathPredictor is currently in arcade mode.

### Design options for redesign

| Option | UX | Perf impact | Sim feel |
|---|---|---|---|
| **(a) Status quo + throttle** | Full predicted curve, recomputed at 30Hz | Moderate (50% reduction) | Arcade |
| **(b) Replace with grid + slope arrows** | Static green grid, arrows show slope direction + severity | Low (one-time bake per green region) | Sim |
| **(c) Target marker at apex** | Single dot at predicted ball-stopping position | Very low (one sim per aim, throttle-able) | Hybrid |
| **(d) Hybrid: short predicted segment + arrows farther** | First 2m of trajectory drawn live; arrows beyond | Moderate (only short segment computed live) | Hybrid |
| **(e) Aim-line target + power feedback only** | Static line in aim direction + power gauge tells player rest | Trivial | Sim, light |

### Architect lean

**(b) or (d)** for the GOLFIN aesthetic. (b) is the cleanest sim convention — leans into the established putt-reading UX players already know from PGA 2K + EA. (d) keeps a small predictive element near the ball while delegating long-distance break to slope arrows; gives novices a footing without feeling like cheating.

Open question: which sim feel does Cesar want? GOLFIN's positioning vs PGA 2K (full sim) vs Everybody's Golf (arcade-ish) is the call.

---

## Open questions for Cesar

1. **Sim vs arcade positioning.** Where does GOLFIN sit on the assistance axis? Closer to PGA 2K (player reads the green) or closer to arcade (game shows full predicted line)?
2. **Ship perf-only fix first, redesign later?** Or bundle both into one spec?
3. **Slope-arrow source.** If we go grid+arrows route: bake slope vectors per-green-region from heightmap once on hole-load, OR compute live per-cell on aim? Bake is faster + still deterministic; live recompute is more responsive to aim changes (matters less for arrows than for predicted lines).

---

## Sequencing

Not on Loop v1 critical path. Hides behind §2b's gameplay scaffold default (predictor disabled in gameplay) until this spec lands. Lab keeps current behavior for now.

Estimate: 0.5 day for perf-only throttle, 1–2 days for full redesign.

---

## Pointers

- `Assets/Scripts/UI/HUD/PuttPathPredictor.cs` — current MonoBehaviour
- `Assets/Scripts/Physics/Viewer/PhysicsLabController.cs:118` — `_puttPathPredictor` SerializeField + setup
- Putter P1 spec: `Docs/Specs/Completed/putter_p1_ui/`
- B-followups list: `Docs/TellCode.md` § "B-followups"
