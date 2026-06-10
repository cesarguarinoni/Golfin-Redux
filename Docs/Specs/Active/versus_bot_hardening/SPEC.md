# SPEC — `versus_bot_hardening`

**Notion:** Order 345 (P2, Loop v2) · **Precedes:** 1v1 Phase 2b (difficulty model) · **Follows:** `1v1_match_flow` Phase 2a (Order 344, shipped `ec9ee885`, in `Docs/Specs/Completed/1v1_match_flow/`)
**Tier:** FULL PIPELINE (runtime spatial math + failure-prone + touches shipped gameplay)
**Prepared:** 2026-06-10 20:20 JST (Architect)

---

## 1. Why

Phase 2a shipped a working 1v1 turn-flow + resolution, but the bot (`VersusBot`) is a **straight-line, distance-only shooter**: it aims dead at the pin every shot, has no hazard/OB awareness, no recovery, no green read, and a crude power model (it took ~5 shots on a ~107m hole, and was only ever exercised on Hole 4). Matches pick a **random hole 1–18**; on doglegs / water / OB-flanked holes the current bot fires straight into trouble and grinds to the par+5 safety cap or loses outright.

**Harden the bot to play a competent, robust hole on ANY of the 18 holes** before Phase 2b layers a difficulty model on top — otherwise 2b's error bands sit on a loose, hole-fragile baseline and "higher level = tighter" has no real floor to scale from.

This is a **bot-only** hardening pass. It does **not** touch the turn-flow state machine, resolution logic, HUD, RP bridge, or solo play — all of those shipped correctly in 2a.

---

## 2. Scope — three workstreams (all bot-side)

### H1 — Calibrated club/power (target distance → club + power)

**Problem:** `VersusBot.SelectShot` uses linear power ramps (`dist/180`, `dist/130` capped 0.75…) that don't map to real carry, so the bot under-clubs and over-shoots inconsistently.

**Build:**
1. **Calibration harness (editor-only, generates a shipped CSV):** run headless `BallSimulation.Simulate(ShotInput, IGroundProvider, …)` probe sims per club across `power ∈ [0..1]` (e.g. 0.05 steps) using the **production stat path** (the same `StatProviderBus`/bundle a real shot uses), on flat ground, measuring flat XZ carry. Emit a per-club power→carry table to `Assets/Resources/Data/bot_clubs.csv` (CSV-first). Use `BallStateMachine.Headless` for synchronous probing. Harness is `#if UNITY_EDITOR` and committed under the bot's editor folder; its OUTPUT (the CSV) ships.
2. **Rewrite `VersusBot.SelectShot`** to read `bot_clubs.csv`: given remaining (safe-target) distance, pick the longest club whose calibrated max carry does not overshoot the target, then set `power01 = inverseCarry(targetDistance)` from the table. Putt range → Putter with calibrated distance→power.

**Acceptance:** on a straight hole the bot holes a ~par-3 in ~3 and plays a par-4/par-5 at or near par (no more 5-shots-on-107m).

> NOTE (elevation): the carry table is flat-ground; Lomond has elevation. H1 is a flat-carry baseline — a simple Δelevation power nudge MAY be added if cheap, else elevation compensation is deferred to 2b. Flag the approximation; do not block on it.

### H2 — Landing-safety / layup + OB recovery (anti-self-destruct — the priority)

**Problem:** the bot fires the full straight-line shot regardless of what's in the way, so it repeatedly lands in water/OB on non-straight holes.

**Build:**
1. **Proactive landing probe:** before committing, compute the landing XZ along the aim line at the chosen carry; `_controller.GetSurfaces().Classify(x, z)`. If the landing surface is in the **avoid set `{Water}`** (and `Bunker` is "discouraged, not forbidden"), **lay up**: walk the target distance down in steps and re-probe until the landing falls on a playable surface (`Fairway/Green/Fringe/Rough/SemiRough/Tee`), then re-pick club+power via H1 for that shorter target.
2. **Retarget fallback:** if no safe landing exists on the straight line within reach, rotate the aim a bounded set of offsets (e.g. ±10°, ±20°) and choose the line whose landing is playable and closest to the pin.
3. **Reactive recovery:** if this player's **previous** shot returned `ShotResult.OBReason` (Water / OutOfBounds / ExitedWorldBounds), bias the next aim away from that line (the proactive `Classify` probe does not reliably catch world-bounds OB — this is the robust backstop).

**Acceptance:** on a hole whose straight pin line crosses water/OB, the bot lays up or retargets onto a playable surface instead of repeatedly going OB; it no longer caps out (par+5) on non-straight holes.

> NOTE: confirm what `BakedZoneClassifier.Classify` returns for positions OUTSIDE all zone markers (true world-bounds OB). If it returns a benign fallback (e.g. Rough), proactive OB detection is unreliable there — rely on the reactive `OBReason` path (#3) for world-bounds OB and keep proactive `Classify` for Water/Bunker. Flag the actual behavior in the report.
> NOTE: `Classify` takes `fp worldX, fp worldZ` — use the existing `float→fp` converter (same one `BotDriver` / sim code uses).

### H3 — Basic green-slope read

**Problem:** putts are `dist/18` / `dist/8` straight at the cup, ignoring authored green slope → the bot 3-putts sloped greens.

**Build:**
1. **Additive accessor on `PutterGreenReader`:** `public bool TryGetSlopeAt(float worldX, float worldZ, out float slopeX, out float slopeZ, out float magnitude)` — nearest-cell lookup over the already-baked `_cells` (no re-bake). Do not change existing PutterGreenReader behavior.
2. **In `VersusBot`, on putts:** query slope at the ball; offset the aim yaw to play the break (aim uphill of the fall line by an amount ∝ `slope × distance`) and/or nudge power for uphill/downhill. Keep it basic — a single proportional correction, CSV-tunable gain.

**Acceptance:** on a sloped green the bot's putt curves toward the cup rather than rolling straight past; measurably fewer 3-putts than the 2a straight-line baseline.

---

## 3. Constraints (carry over from 2a)

- `VersusBot` stays **shippable**: no `#if UNITY_EDITOR`, no `ForceShotCompleteForBot`, drives the production `ShotController` external-drag path. (The calibration harness in H1 is the only editor-only piece; it ships a CSV, not behavior.)
- Bot lives in `Golfin.Physics.Viewer` (internal `BallSM`/`SetCameraYawRadians` access) — unchanged.
- All tunables CSV-first (`bot_clubs.csv`, plus any green-read gain / layup-step constants in a bot CSV).
- No changes to `VersusMatchController`, resolution, HUD, RP bridge, or solo play.

---

## 4. Code anchors (verified 2026-06-10)

| Need | Anchor |
|---|---|
| Surface at a world XZ | `PhysicsLabController.GetSurfaces()` → `ISurfaceProvider.Classify(fp x, fp z)` → `SurfaceType` — `Physics/Core/ISurfaceProvider.cs`; impl `BakedZoneClassifier` |
| Surface values | `SurfaceType { Fairway, Green, SemiRough, Rough, Bunker, Water, Tee, CartPath, Fringe }` — `Physics/Core/SurfaceType.cs` |
| OB reason (reactive) | `ShotResult.OBReason` (Water / OutOfBounds / ExitedWorldBounds) — `Gameplay/Loop/ShotResult.cs` |
| Green slope cells | `PutterGreenReader._cells` (`SlopeCell{cx,cz,meshY,slopeX,slopeZ,magnitude}`, 0.5m grid) — `Physics/Viewer/PutterGreenReader.cs:50`; ADD `TryGetSlopeAt` |
| Carry probe | `BallSimulation.Simulate(ShotInput, IGroundProvider, …)` static — `Physics/Core/BallSimulation.cs:60+`; `BallStateMachine.Headless` synchronous |
| Cup / par | `HoleContext.PinWorld`, `HoleContext.Par` |
| Current bot | `VersusBot.cs` (`TakeShot`, `SelectShot`) — `Physics/Viewer/VersusBot.cs` |
| Shot commit path | `ShotController.BeginExternalDrag/SetExternalPower/EndExternalDrag` (unchanged) |

---

## 5. Out of scope

- The difficulty/error model (aim/power error bands, club noise, level→band CSV) — that is **Phase 2b**, written next, on top of this hardened baseline.
- Per-turn **pacing** trim (the slow 1.5/0.5/2.0s waits + many-shots). H1 indirectly speeds matches (fewer shots); explicit cadence tuning is a separate polish item.
- Full dogleg path-planning / multi-shot route optimization — H2 is a per-shot safe-landing heuristic, not a planner.
- Spin / shot-shape use (gated on `spin_and_shot_shape_wiring`; the bot can adopt draw/fade for doglegs once that lands).
- Any change to turn-flow, resolution, HUD, RP, or solo play.

---

## 6. Acceptance checklist (implementer fills `IMPLEMENTER_REPORT.md`)

- [ ] **H1:** `bot_clubs.csv` generated by the calibration harness from headless production-path sims; `VersusBot.SelectShot` reads it; bot holes a straight ~par-3 in ~3 and plays a par-4/par-5 near par.
- [ ] **H2:** on a hole whose straight pin line crosses Water/OB, the bot lays up / retargets onto a playable surface (proactive `Classify` for Water; reactive `OBReason` for world-bounds OB); it no longer caps out on a non-straight hole.
- [ ] **H3:** `PutterGreenReader.TryGetSlopeAt` added (additive); bot putts curve with the slope; fewer 3-putts than the 2a baseline on a sloped green.
- [ ] `VersusBot` remains shippable (no `#if UNITY_EDITOR`, no `ForceShotCompleteForBot`).
- [ ] **Multi-hole coverage:** verified on at least three holes — a straight hole, a water/OB hole (the H2 case), and a sloped-green hole — NOT just Hole 4.
- [ ] No change to `VersusMatchController` / resolution / HUD / RP bridge / solo play (diff confined to `VersusBot`, the additive `PutterGreenReader` accessor, the editor harness, and CSVs).

---

## 7. Visual gate

Per `feedback_prefer_bot_videos` + `feedback_record_bot_video_full_size`: bot-recorded videos at full 1170×2532 on the three coverage holes (§6) showing (a) the bot reaching the green near par on a straight hole, (b) the bot laying up / retargeting around water/OB instead of going OB, and (c) a putt curving with the slope. The two-clip approach from 2a (tee-to-cup duration) applies if needed.

---

## 8. Tier & kickoff

**FULL PIPELINE** — runtime spatial math (surface probing, carry inversion, slope read) + failure-prone + edits shipped gameplay code.

Kickoff (Cesar pastes into Claude Code):
```
Use the implementer subagent on "versus_bot_hardening"
```
