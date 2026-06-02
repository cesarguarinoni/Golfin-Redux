# club_control_arrow_polarity_fix

> **Status:** SPEC_READY (Architect, 2026-06-02). Tier-Tune / polarity bug. FULL PIPELINE.
> **Spun out of:** `club_control_aim_arrow_speed` (closed already-implemented) — investigation found the existing CC→arrow-speed coupling has **inverted polarity**.

## One-line

Invert the ClubControl→aim-arrow-speed mapping so **higher ClubControl gives a SLOWER arrow** (easier to time), instead of the current faster-at-high-CC behavior.

## Why (verified against live code)

`ShotController.TickArrow()` computes:

```csharp
float arrowHz = _config.BaseArrowSpeedHzAtCC0 + cc * _config.ArrowSpeedHzPerCC;
```

with live values (both `ControlsConfig.Default` and the canonical `Assets/Resources/Gameplay/controls.csv`):
`BaseArrowSpeedHzAtCC0 = 0.5`, `ArrowSpeedHzPerCC = 0.025`.

ClubControl range is **0–100** (per `controls.csv` note on `CleanPassesPerCC`: "CC=100 gives 5 passes"). So today:
- **CC=0 (worst player) → 0.5 Hz** (slow arrow = EASY to time)
- **CC=100 (best player) → 3.0 Hz** (fast arrow = HARD to time)

This is backwards. The faster oscillation is harder to stop in the target zone, so high ClubControl currently *punishes* the player. Cesar confirmed intent: **high CC must give slower arrows.** Note the clean-pass coupling on the next line (`cleanPasses = MaxCleanPassesAtCC0 + cc * CleanPassesPerCC`, +passes with CC) already has the correct "high CC = more forgiving" polarity — only arrow speed is wrong-signed, so the two currently fight each other.

## The fix — mirror the existing band, flipped

Preserve the exact 0.5–3.0 Hz difficulty envelope; only reorient it to be monotonically **decreasing** in CC.

| Key | Old | New |
|---|---|---|
| `BaseArrowSpeedHzAtCC0` | 0.5 | **3.0** (CC0 is now the fast/hard end) |
| `ArrowSpeedHzPerCC` | 0.025 | **-0.025** (subtractive) |

Result: `arrowHz = 3.0 - cc*0.025` → **CC0 = 3.0 Hz (hard), CC100 = 0.5 Hz (easy)**. Same spread as today, correct orientation.

**Positivity guard:** clamp `cc` to `[0,100]` in the arrow-Hz line only, so `arrowHz` stays within `[0.5, 3.0]` even if a buff pushes CC above 100:

```csharp
float ccClamped = Mathf.Clamp(cc, 0f, 100f);
float arrowHz   = _config.BaseArrowSpeedHzAtCC0 + ccClamped * _config.ArrowSpeedHzPerCC;
if (IsPutt) arrowHz *= _config.PuttArrowSpeedMultiplier;
```

Do **NOT** clamp the `cleanPasses` line — its polarity is already correct and extra passes above CC100 are harmless.

## Scope (files)

1. `Assets/Scripts/Gameplay/Config/ControlsConfig.cs` — `Default`: `BaseArrowSpeedHzAtCC0` 0.5->3.0, `ArrowSpeedHzPerCC` 0.025->-0.025.
2. `Assets/Resources/Gameplay/controls.csv` — **canonical source of truth** (a `.cs`-only change reverts; see ball_roll lesson). Same two values; update the two `notes` columns to describe the inverted mapping ("arrow cycles/sec at ClubControl=0 — fastest/hardest" and "additive cycles/sec per CC point — negative: higher CC = slower").
3. `Assets/Scripts/Gameplay/Input/ShotController.cs` `TickArrow()` — add the `Mathf.Clamp(cc, 0f, 100f)` guard shown above (1–2 lines). Formula structure otherwise unchanged (the negative coefficient does the inversion).
4. `Assets/Scripts/Gameplay/Tests/ShotControllerTests.cs` — `Test09` and `Test10` still **pass** (they count 1 pass/tick, unchanged), but their comments hard-code "arrowHz=0.5" — update comments to the new CC0=3.0 value so they don't mislead.
5. `Assets/Scripts/Gameplay/Tests/ShotControllerPuttModeTests.cs` — the putt test (~L134) asserts putt `ArrowProgress01 < 1` after 2s; that holds only while CC0 yields a slow arrow, so it **will fail** after the fix (putt arrowHz at CC0 = 3.0*0.5 = 1.5 Hz). Fix it to assert the **polarity-independent invariant** instead: at equal CC, putt arrowHz < non-putt arrowHz (i.e. `PuttArrowSpeedMultiplier` still slows putts). If keeping the "progress<1 after Ns" shape, inject a high-CC bundle so the arrow is genuinely slow, and recompute the window.
6. **New regression test** (in `ShotControllerTests.cs`): assert monotonic-decreasing + positive — `arrowHz(CC=0) > arrowHz(CC=100)` and both `> 0`. Drive two ticks with injected CC=0 vs CC=100 bundles and compare elapsed arrow progress over a fixed dt (CC=0 must advance faster). This is the decisive automatable gate for the polarity.

## Hard rules

- Keep `arrowHz > 0` across the full CC in [0,100] range (the clamp guarantees the floor of 0.5 Hz).
- Do not change the clean-pass coupling, cone geometry, or any other ControlsConfig field.
- `.cs` Default and `controls.csv` must agree (both carry the new values).
- Existing test count must stay at or above baseline (Test09/Test10 keep passing; putt test fixed; +1 new test).

## Verification

- **Primary (automatable):** the new monotonic-decreasing test passes; full EditMode suite green.
- **Secondary (optional, nice-to-have):** bot or manual confirm that a high-CC character's arrow visibly sweeps slower than a low-CC character's. Numeric test is decisive, so this is confirmation, not a gate.

## Out of scope

- Putt arrow *tuning* (whether 1.5 Hz at CC0 putt is too fast is a separate feel question — this spec only fixes polarity + keeps the multiplier relationship).
- The separate observation that cone half-angle widens with Accuracy (`ConeHalfAngleAtAcc0Deg=5` -> `AtAcc100Deg=20`), which may or may not be intended — flagged to Architect, not addressed here.
- The vestigial `AimConeReductionFraction` resolver output (filed POLISH_BACKLOG P-003).

## Handoff

Spec path: `Docs/Specs/Active/club_control_arrow_polarity_fix/SPEC.md`. Route via FULL PIPELINE (implementer → self-review → reviewer → architect).
