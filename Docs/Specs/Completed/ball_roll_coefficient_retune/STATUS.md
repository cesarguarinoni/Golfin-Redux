DONE

SHIPPED 2026-06-02 (commit 04613e67, pushed to main). Architect-approved after independent main-thread Unity MCP
verification: live ball_roll_per_point=0.02, rollMul cap-saturated 1.20/0.80, Fairway running shot
(2deg/45 m/s) Roll-10 -> 73.32m vs Roll+10 -> 90.47m = 17.15m delta (>= 10m bar). 362/362 tests pass,
caps unchanged, Roll=0 neutral (Hole 1 no-op). Effect is shot-dependent by design (roll only matters
for rolling shots). Implementer 27.65m figure (1deg/60 m/s) did not reproduce under real aero+backspin
(actual ~7.27m, optimistic/non-load-bearing); 2deg/45 figure reproduced solidly so conclusion stands.
Files: StatCoefficients.cs + stats.csv (CSV-first source of truth, defeats override-revert) +
StatResolverTests.cs (Test 9 at-cap 0.90->0.80) + PHYSICS_TUNING_CHANGELOG.md (F8).
