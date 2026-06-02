# Architect independent test re-run — `club_control_arrow_polarity_fix`

The self-reviewer and reviewer both flagged a soft note: pipeline subagents cannot call `mcp__ai-game-developer__tests-run` (implementer-scoped), so the "suite green" claim rested on the implementer's rollup. The architect main thread (which DOES have Unity MCP) re-ran the two affected test classes independently to close that gap.

**Run date:** 2026-06-02 (architect main thread, via `mcp__ai-game-developer__tests-run`, EditMode, `includePassingTests=true`).

## ShotControllerTests — Status: Passed (13/13, 0 failed, 0 skipped)

All Test01–Test10 passed, plus the new polarity gate:

- `Test11_ArrowSpeed_MonotonicDecreasingWithCC` — **Passed** ✅ (decisive polarity regression: CC=0 arrow advances faster than CC=100; both > 0)

## ShotControllerPuttModeTests — Status: Passed (14/14, 0 failed, 0 skipped)

All F1/F3/F6 putt tests passed, including the rewritten one:

- `F1_IsPutt_ArrowsSlowedByMultiplier` — **Passed** ✅ (re-targeted to polarity-independent invariant: putt arrowHz < non-putt arrowHz at equal CC)

## Conclusion

Both the new regression test and the rewritten putt test are confirmed PASSED by direct re-run, not inferred from the rollup. The reviewer's open soft note is resolved. No tests skipped or ignored in either class.
