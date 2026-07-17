# club_control_arrow_range_calibration

> **Status: DONE (2026-07-17) — Cesar-approved.** Measured, rescaled, felt-gate video captured. See § Implemented.
> **Order:** 732 (Notion GOLFIN_Roadmap) — Phase "Loop v2", P2 — Medium
> **Tier:** 3 — FULL PIPELINE (gameplay-feel math + measurement gate)
> **Filed:** 2026-07-16 17:05 JST (Architect)
> **Replaces:** the never-written, now-VOID `club_control_aim_arrow_speed`. See `Docs/Physics/STAT_LANE_AUDIT.md` § "Findings 2a/2b VOID".

---

## Implemented (2026-07-17)

Measured the arrow ladder (arithmetic on `TickArrow`'s two formulas across CC 0/25/30/35/40/50) — premise confirmed: at −0.025/0.04 the reachable Common-cap→Supreme spread was a flat **1.36×** (2.375→1.75 Hz) and 2→3 clean passes. Cesar approved the **full rescale** (restore the designed endpoints on the reachable 0–50 range):

| Key | Old | New | Reachable effect |
|---|---|---|---|
| `ArrowSpeedHzPerCC` | −0.025 | **−0.05** | arrow Common cap 1.75 → Supreme 0.5 Hz (**3.5×**) |
| `CleanPassesPerCC` | 0.04 | **0.08** | clean passes Common cap 3 → Supreme 5 |
| `PuttArrowSpeedMultiplier` | 0.5 | **0.8** | Supreme putt 4.0 s → 2.5 s (kept < 1.0; putts still slower than swing) |
| `MaxTotalPasses` | 10 | 10 (Cesar) | Supreme degradation window 7 → 5 passes |

Changed in **both** `Assets/Resources/Gameplay/controls.csv` (runtime) and the mirrored `ControlsConfig.Default` (fallback/tests). CSV-only for logic — no `ShotController` change. **F11** in `PHYSICS_TUNING_CHANGELOG.md`.

- **Caveat (documented):** `arrowHz` has no floor, so −0.05 goes negative above CC=60; safe only because RarityStatCaps enforces CC ≤ 50. A floor would be a `ShotController` change → separate order.
- **Tests:** Golfin.Gameplay.Tests 243/0, Golfin.Physics.Tests 234/0. `Test11` moved CC=100→CC=50 (100 is unreachable and now yields negative arrowHz). Hole-1 completability unaffected (bot fires via `FireDebugShot`, bypassing `TickArrow`).
- **Felt gate (video):** built a reusable rig `Assets/Scripts/UI/Editor/ClubControlArrowDemoRecorder.cs` (menu `GOLFIN > Physics > Record ClubControl Arrow Range Demo`) that drives the REAL Timing loop at CC 25 vs 50 and records. Side-by-side at `videos/club_control_arrow_range_sidebyside.mp4` — the Supreme (CC=50) arrow visibly sweeps ~3.5× slower. Resolves the "physics bot bypasses TickArrow, needs a different rig" gap the spec flagged.

Open forks resolved by Cesar: (1) full linear rescale; (3) putt retuned to 0.8; (2) MaxTotalPasses unchanged.

---

## One-line

Rescale the ClubControl→arrow coefficients in `controls.csv` from their unreachable 0–100 design range to the real 0–50 rarity cap, so the designed arrow-speed / clean-pass ladder actually materialises across Common→Supreme.

---

## Why

The mechanic is **live and correct in shape** — `ShotController.TickArrow()`:

```csharp
float arrowHz   = _config.BaseArrowSpeedHzAtCC0 + ccClamped * _config.ArrowSpeedHzPerCC;
int cleanPasses = Mathf.RoundToInt(_config.MaxCleanPassesAtCC0 + cc * _config.CleanPassesPerCC);
```

The coefficients are calibrated for **CC 0–100**. `RarityStatCaps.cs` caps ClubControl at **50** (Supreme);
Common caps at 25. CC=100 is unreachable, so the designed dynamic range collapses:

| | Designed (0→100) | Actually reachable (Common 25 → Supreme 50) |
|---|---|---|
| Arrow speed | 3.0 → 0.5 Hz (**6×**) | 2.375 → 1.75 Hz (**1.36×**) |
| Clean passes | 1 → 5 | **2 → 3** |

The entire roster ladder buys a 26% slower arrow and one extra clean pass. That is why ClubControl feels
dead — **not** the aim cone (that lane is off-design and is being deleted by Order 731).

---

## Hypothesis (measure before shipping)

Rescaling to the real 0–50 range restores the designed endpoints:

| Key | Current | Hypothesis | Yields at CC=50 |
|---|---|---|---|
| `ArrowSpeedHzPerCC` | −0.025 | **−0.05** | 3.0 − 50×0.05 = **0.5 Hz** ✅ designed |
| `CleanPassesPerCC` | 0.04 | **0.08** | 1 + 50×0.08 = **5 passes** ✅ designed |

`BaseArrowSpeedHzAtCC0 = 3.0` unchanged (CC=0 is the FALLBACK/neutral floor).

**Do not ship these off this table.** They are a starting hypothesis. See Measurement.

---

## Blocked by

**Order 731 (`stat_lane_offdesign_retirement`) must land first.** Until it does, `bundle.Character.ClubControl`
is contaminated below 100% condition by the resolver's double stamina scaling, so any CC-vs-arrow-rate
measurement is unreliable.

---

## Measurement — NOT a carry-delta test

This is an **input-layer timing mechanic**, not a physics lane. The ≥10m carry perceptibility bar **does not
apply** and must not be invoked.

- The physics bot fires via `ShotController.FireDebugShot()`, which **bypasses `TickArrow()` entirely**. The
  existing physics bot rig cannot measure this. A different rig is required.
- Measure `arrowHz` and `cleanPasses` directly as functions of CC across the reachable ladder
  (CC = 25 Common cap, 30 Rare, 35 Mythic, 40 Legendary, 50 Supreme), plus CC=0 FALLBACK.
- The felt gate is a **bot-recorded video** of the arrow at Common-cap vs Supreme-cap CC, side by side. If the
  difference isn't obvious on video, the retune failed regardless of what the numbers say.

---

## Open forks for Cesar (resolve before implementing)

1. **Ladder shape.** Restoring the endpoints puts Common's cap (CC=25) at 1.75 Hz — the *midpoint* of the
   range, i.e. today's Supreme feel. Is a linear 0–50 map right, or should the curve be weighted so Common
   still feels meaningfully worse than Supreme? (Note starting stats sit well below the caps, so the
   *played* range is narrower than cap-to-cap.)
2. **Does `MaxTotalPasses = 10` need to move** once clean passes reach 5? The degradation window between
   clean-out and auto-cancel halves for a Supreme character.
3. **Putt path.** `PuttArrowSpeedMultiplier = 0.5` compounds on the rescaled value. At CC=50 putts would go
   0.5 × 0.5 = **0.25 Hz** — a 4-second arrow cycle. Probably too slow; likely needs its own retune in the
   same pass.

---

## Hard rules

- **Hole 1 completability** (≤7 strokes, default character) — standing gate.
- New **F-entry** in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`.
- CSV-only change if at all possible. If `ShotController` logic must change, re-classify and re-scope.
- Tests at or above baseline.

---

## Out of scope

- The aim-cone lanes (Order 731 owns both).
- Strength lanes (Order 415).
- Ball lanes (Order 417).
- Widening `RarityStatCaps` — retune the coefficients to the caps, not the caps to the coefficients.
