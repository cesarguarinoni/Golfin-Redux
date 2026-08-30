DONE

Approved by Cesar 2026-08-30 after iteration 3.

Iteration 1 — the pill itself: slide-in from the left, pulsing gold halo, y that
follows the maintenance notice, flame + auto-sized streak, tap to Mission
Selection, and one shared `DailyMissionState` so the pill and the daily card can
never disagree.
Iteration 2 — Cesar's three follow-ups: the pill hugs its content when there is
no streak (549 -> 481), the streak badge moved beside the DAILY MISSION title in
both card states, and the tap lands with the daily already expanded.
Iteration 3 — the slide is an announcement, not a transition: it plays the first
time a given day's pill appears and again when midnight brings a new one;
returning from any other menu finds the pill already there, at rest.

Invariant gate: 18 assertions, 0 FAIL (`pill_invariants.json`).
UI fidelity lint: 0 FAIL on DailyMissionPill, StreakFlame and MissionCard.
EditMode: 1939 passed / 0 failed / 3 skipped (pre-existing).
`texts` published v17, `export_content.py --check` clean.

Canonical screenshot: `screenshots/home_notice_streak5_en.png`
Canonical video: `videos/daily_mission_home_pill_demo.mp4`
Both copied to `Docs/Reports/Media/` for the next daily report.

CARRIED FORWARD (surfaced, not patched — see IMPLEMENTER_REPORT § Open questions):
1. Offline shows no pill: SPEC §2 assumes a local daily recipe that `missions_v1`
   deliberately never shipped.
2. The mandated importer run published 15 rows belonging to `missions_v1` whose
   importer step had never been run, so `texts` v17 covers more than this task.
