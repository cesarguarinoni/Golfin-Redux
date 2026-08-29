READY_FOR_SELF_REVIEW

Phases A and B complete. Phase C (Mission Selection screen) not started — C1 (the mode-card
target wiring) was pulled forward into B because Phase A's modes.csv edit had made the bundled
`missions` row unroutable and broke four ModesOverlayTests.

The mode is still locked and still has no screen. Nothing built so far is reachable by a player.

Gates: Unity EditMode 2021 tests / 2018 passed / 0 failed / 3 pre-existing skips; both new
mission suites proven live with a tripwire. Backend 172 passed. Dashboard 126 passed, tsc clean,
deployed (Cloudflare 4ccabd61-e47c-402b-a9b8-1ac49f890088).

Blocked on Cesar — see § Blocked on Cesar in IMPLEMENTER_REPORT.md:
  1. apply 2026_08_29_missions.sql, then 2026_08_29_content_missions_seed.sql
  2. publish `texts`
  3. publish `missions` + `mission_tiers` (writes the two server mirrors; needs step 1 first)

Needs a design decision before the campaign can publish:
  * mission 37 (`l_sand_up_down`, Sand Save) is on hole 13, which has NO greenside bunker —
    its nearest sand is 156 m from the green. Re-site the mission, or author a bunker.
