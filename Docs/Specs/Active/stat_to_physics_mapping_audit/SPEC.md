# stat_to_physics_mapping_audit — Audit the full stat-to-physics lane mapping

> **STATUS:** Queued (drafted 2026-05-25 from `live_stat_provider_wiring` Phase 3 postmortem). Tier 3. Surfaced when the visual gate could not show a HIGH vs LOW carry delta even after the LIVE bus correctly routed character stats end-to-end.

## One-line

Audit the `StatModifierResolver` single-source lane assignments to confirm each character / club / ball stat has a perceptible gameplay effect, document the design intent, and either (a) justify each weakly-felt stat as intentional or (b) propose mapping changes that surface the differences gameplay-side. Output is an Architecture doc + (if needed) a tuned `StatCoefficients` set.

## Why

`live_stat_provider_wiring` (Phase 3) wired the live stat provider into the production codepath correctly — every committed shot now resolves `CharacterStats`, `ClubStats`, `BallStats` from the player's actual selection. The bot-recorded visual gate, however, showed identical 3-stroke EAGLE on Hole 1 with a maxed `Strength` vs default `Strength` build using the same club + ball. Root cause: `StatModifierResolver.cs:22-25` only routes `Club.Power × Ball.Power` into `velocityMultiplier`. `Character.Strength` only feeds `overpower forgiveness` (line 53), which has no observable effect at `power = 1.0` (no overpower applied). So a strength-maxed player and a default player produce identical carry distance whenever they aim cleanly at `power = 1.0`.

This is design-intentional per the "single-source per lane" comment in the resolver — but it surfaces a UX problem: players cannot feel the value of leveling a character if their carry distance is unchanged. A patch was applied in `live_stat_provider_wiring` Phase 4 (F7) adding a small Strength-velocity coupling to unblock the visual gate. That patch is intentional one-off, NOT a design replacement; the full audit lives here.

## Scope

1. **Lane audit:** for each of the 8 lanes in `StatModifierResolver` (velocity, aim cone, spin, lie resist, overpower, putter off-center, gravity well radius, aim cycles), confirm:
   - Which stats feed it (currently single-source per design)
   - The min/max impact at the realistic stat-range extremes (Common 25 vs Supreme 50 stat caps)
   - Whether that impact is *gameplay-perceptible* — minimum bar: shows up in a smoke-bot side-by-side as ≥1 stroke delta, ≥10m carry delta, or ≥0.5° aim spread delta on a representative hole
2. **`BallPhysicsModifiers` audit:** the rebound / roll / wind-cut lanes that flow into `BallSimulation` — same perceptibility bar.
3. **Tuning proposal:** for each weakly-felt lane, either:
   - (a) Justify keeping it weak as design intent (with a doc paragraph explaining why and what the player *can* feel that scales with the stat — e.g. forgiveness only manifests when the player overpowers, which Common-rarity characters do more)
   - (b) Propose a coefficient change that brings it above the perceptibility bar without breaking existing physics tests or hole-completion stats
4. **Cross-cutting questions to answer in writing:**
   - Should `Character.Strength` directly affect velocity (currently no, post-patch only weak coupling)?
   - Should `Character.Recovery` (currently does it affect anything?) feed back into stamina regen between shots?
   - Should `Character.Stamina` be more than a soft scalar (currently `staminaMultiplier` clamps to 1.0 max so it never amplifies, only attenuates)?
   - Should Ball.Power and Ball.Spin compete or stack (currently multiplicative-positive on velocity, additive on spin)?
5. **Test gate:** any coefficient changes must preserve the existing `Golfin.Physics.Tests` baseline (today: **342/339/0/3 after Phase 4 F7 of `live_stat_provider_wiring`**). Document a new physics regression test set if proposed changes go in.

## Methodology — bot harness design

**Primary harness:** the existing `Hole 1 Playthrough` smoke-bot scenario (par-5, full-club journey: driver → wedge → putter). Reuse with character-build variation.

**Stat-build profiles measured:**
- **LOW** — Common-rarity max stats (~5–10 across STR / CTRL / REC / STAM)
- **MID** — Rare-rarity max (~20–25)
- **HIGH** — Supreme-rarity max (~45–50)

**Per-lane sweep:** for each lane in §Scope.1, vary the dominant stat across LOW→MID→HIGH while keeping the other three stats fixed at MID. Same club and same ball as controls. Measure: stroke count, stroke-1 carry (m), terminal surface, aim spread (eyeballed from caption video; bot fires "perfect" aim so spread is theoretical — the audit may need to instrument per-shot RNG-seed jitter to surface aim-cone deltas).

**`BallPhysicsModifiers` sweep:** vary ball stats with character fixed at MID. One new bot scenario `stat_lane_surface_roll`: fires the same club + power onto a Fairway lie, a Rough lie, and a Sand lie at a known position; measures roll-out terminal position with LOW vs HIGH ball stats. Adds ~1 scenario + ~6 bot runs.

**Total harness work:** 1 new bot scenario + reuse of `Hole 1 Playthrough` + ~10–12 bot runs to permute. Output is a perceptibility matrix in `STAT_LANE_AUDIT.md` (rows = lanes, columns = LOW/MID/HIGH, cells = measured delta + PASS/FAIL vs the perceptibility bar).

**OB avoidance:** bot scenarios in this audit MUST avoid OB shots by default. Aim targets must bias toward fairway-safe areas. If a HIGH stat build (e.g., Supreme Strength + driver) would push a default-aim shot into OB on Hole 1, the scenario must use a reduced-power flick or pick a different club. OB shots have currently-shoddy camera framing that degrades the audit's visual evidence. **OB-specific behavior is NOT in this audit's scope.**

This OB-avoidance rule is ALSO codified as a durable bot convention in `Docs/Architecture/BOT_FRAMEWORK.md` §6 (added 2026-05-25) so future bot work inherits it.

**Putter lanes:** out of scope unless a swing-lane finding implicates them (per §Out of scope below).

## Q-LOCKS

| # | Question | Architect lean | Lock |
|---|---|---|---|
| Q1 | Measurement methodology — what harness, what stat profiles, how is data generated? | See §Methodology above. Hole 1 par-5 reuse + 1 new surface-roll scenario, 3 stat profiles (LOW/MID/HIGH), per-lane stat sweep, OB avoidance baked in. | **LOCKED 2026-05-25 (Cesar):** Methodology approved as proposed in §Methodology. Additional durable rule confirmed: bots avoid OB shots by default; OB-specific testing is a separate concern. The rule is codified in `Docs/Architecture/BOT_FRAMEWORK.md` §6. |
| Q2 | F7 baseline — keep F7's Strength→velocity coupling in place during the audit, or revert F7 first and run the audit on a cleaner pre-F7 baseline? | **LOCKED 2026-05-25 (Cesar):** Option A — keep F7 in place. F7's coefficient (`CharStrengthVelocityPerPoint = 0.004f`) and the raised cap (`VelocityMultiplierMax = 2.6`) are treated as the current production baseline. Audit findings on the Strength→velocity lane fall into three buckets: (1) **validate** — perceptibility OK, coefficient right, lock as final; (2) **retune** — perceptible but too strong/weak, propose new coefficient with regression test; (3) **retire** — Strength shouldn't feed velocity directly, propose a different lane (e.g., spin or stamina coupling) and roll back F7 as part of the audit's coefficient PR. The `live_stat_provider_wiring` Phase 4 v3 videos (Δ=26m HIGH vs LOW on identical club+ball) are cited as the F7 calibration data point — not re-collected. |
| Q3 | `DefaultStatProvider.BuildSwingBundle` seam (always returns `DefaultDriver` regardless of club; root cause of Hole 1 default-character 8-stroke seam in `live_stat_provider_wiring` Phase 4) — in-scope as a fix in this audit, or surface as audit finding + file a separate spec? | **LOCKED 2026-05-25 (Cesar):** Option A — in-scope as a fix. Audit ships a club-aware FALLBACK in the same PR. Rationale: avoid tech-debt accumulation; the seam is architecturally inside the resolver's input handling, so the audit is the right place to fix it. **Design pattern:** (1) Extend `StatProviderBus.Resolve(bool isPutt)` → `StatProviderBus.Resolve(bool isPutt, int labClubIndex)`. The new parameter is the lab club index (`0 Driver / 1 Iron7 / 2 Wedge / 3 Putter`) already tracked by `PhysicsLabController.CurrentClubIndex`. (2) `ShotController.GetStatBundle()` passes `PhysicsLabController.Instance.CurrentClubIndex` (or the equivalent accessor; verify exact API during pre-flight) to the bus. (3) `DefaultStatProvider.BuildSwingBundle(int clubIndex)` picks from a per-club static table; new statics added: `ClubStats.DefaultIron7`, `ClubStats.DefaultWedge` (the existing `ClubStats.DefaultDriver` stays). Putter FALLBACK keeps using `PutterStats.DefaultPutter`. (4) The LIVE path is unchanged — `LiveStatProviderHost.ResolveLive` already resolves the real club via `ClubContext.SelectedClubId`. **Default values for new statics:** copy from `PhysicsLabController.LabClubs[1]` (Iron7) and `LabClubs[2]` (Wedge) so FALLBACK matches lab behavior. Verify the values at pre-flight (lab uses canonical "default" stats anyway). **Hard-rule rewrite for this audit:** the existing "≤ 7 strokes on Hole 1 par-5 by default-stat character" hard rule is verifiable on BOTH paths after the fix: LIVE-path (seeded character + clubs + ball) AND FALLBACK-path (no-armed bot, post-fix). Both must pass. The pre-fix 8-stroke seam in `Hole 1 Playthrough` bot must be gone after the fix — re-run + document. **New tests required:** `DefaultStatProvider_BuildSwingBundle_ReturnsClubSpecificStatsForIndex0to3` (4 cases); `StatProviderBus_Resolve_PassesClubIndexToDefaultProvider`; `ShotController_GetStatBundle_ForwardsCurrentClubIndex`. **Implementer's choice:** if the bus + DefaultProvider chain doesn't carry an index cleanly today and refactoring is heavier than expected, surface as IMPLEMENTER_BLOCKED for architect re-scope rather than half-ship. |
| Q4 | Coefficient-PR ceiling — DoD says "if changes proposed, ship them." Tier the proposed changes (safe-ship subset lands in this PR, larger retunings filed as follow-up specs) or all-or-nothing? | **LOCKED 2026-05-25 (Cesar):** Option B — tiered, with a concrete classification rule so the auditor can self-classify rather than escalate per finding. **Tier-Safe (ships in this PR):** single coefficient change OR single cap raise, has a unit test asserting the before/after invariant, doesn't add a lane, doesn't remove a lane, doesn't change a clamp polarity. **Tier-Tune (follow-up spec):** changes that introduce a new lane (e.g., new stat→outcome coupling), change a clamp polarity (`Max`↔`Min`), or require playtest validation beyond unit tests. **Tier-Redesign (follow-up spec):** changes that imply removing or fundamentally re-routing a lane (e.g., "Strength should feed spin not velocity"). **Hard rule — every Tier-Tune and Tier-Redesign proposal MUST result in a filed follow-up SPEC** in `Docs/Specs/Queued/<slug>/SPEC.md`, not a murmur in the audit doc. If the audit surfaces N Tier-Tune findings, the implementer files N follow-up SPECs (stubs are fine; full design pass happens when each is picked up). This preserves Q3's "no tech-debt drift" intent while keeping the audit PR reviewable. **Audit doc deliverable:** `STAT_LANE_AUDIT.md` includes a final "Findings classification" table with rows = findings, columns = Tier-Safe / Tier-Tune / Tier-Redesign / Justified-as-is, plus a "Filed follow-up specs" section listing every spec the audit created. |

## Out of scope

- The `live_stat_provider_wiring` Phase 4 F7 patch itself. That patch is shipping; this audit may revisit it but is not blocked on it.
- Renaming `PhysicsLabController` to a non-lab-confusing name — separate task `physics_lab_controller_rename`.
- The Putter lane stat sources (Putter.Control, Putter.Accuracy, Putter.Weight) — those are 1:1 with putter inputs and don't have the "single-source per character lane" issue. Audit them only if a side-by-side test reveals an issue.
- `DefaultStatProvider.BuildSwingBundle` club-aware FALLBACK is **now IN scope** per Q3 lock (was previously a separate-spec candidate).

## Hard rules

1. The "single-source per lane" pattern is the design baseline. Any departure must be explicitly documented with a one-paragraph design justification in the output doc.
2. Coefficient changes must not regress hole-completion stats on the current Hole 1 par-5 (must still be completable in ≤7 strokes by a default-stat character with a default driver, default ball).
3. Tests stay green at or above baseline.

## Definition of done

- `Docs/Physics/STAT_LANE_AUDIT.md` written: one section per lane, perceptibility number, design justification, proposed change (if any).
- If coefficient changes are proposed: PR ships them with new physics regression tests + the Coefficients diff documented in `Docs/Physics/PHYSICS_TUNING_CHANGELOG.md`.
- `live_stat_provider_wiring` Phase 4 F7 patch revisited in the doc: kept as-is, rolled back, or refined.
- A line in `Docs/AI_CONTEXT.md` updated noting the audit completed and where the result lives.
