IMPLEMENTER_WORKING

# STATUS — `gps_polish`

**Current:** `IMPLEMENTER_WORKING` — **Cesar approved the push on 2026-09-02** ("Done") and it is
in that evening's daily report. STATUS deliberately stays open: several §D polish items are still
undone and archiving the folder now would throw away the remainder list. See
`IMPLEMENTER_REPORT.md` § Not done. One word moves it to `DONE` if the rest is being dropped.

**Not `READY_FOR_SELF_REVIEW`:** the acceptance list is not fully satisfied. A1 (fail=0), A2
(0 px), A3, A5, A6 (0 new findings), A9, A11 and A12 all pass; A4 is 1 video of 6, and A7/A8/A10
are partial. Advancing the state would be claiming a checklist that is not filled.

**Opened:** 2026-09-02. First commit closed `gps_pill_entry` (`96d60fab4`).

## What is green

| gate | result |
|---|---|
| A1 invariants | `fail=0` over 10 pushes; durations 0.251–0.267 s vs 0.25; t0 ±1170; seam 1.000 |
| A2 rest parity | HEAD vs final build, **0 differing px** on all 7 GPS screens |
| A3 boundary | `FadeController` byte-identical; non-GPS ends pinned by test |
| A5 nav-bar seam | worst mid-push mean \|ΔRGB\| = 0.92 (budget 2) |
| A6 lint | identical prefab-for-prefab vs HEAD — 0 new findings |
| A11 importer | `--check` clean, texts v31, no new strings |
| A12 EditMode | 2296 / 2293 passed / 0 failed / 3 pre-existing skips |

## Cesar's call, 2026-09-02

- **The push is approved** — "Done". The fallback (`UiMotion.Enabled = false`, which turns every
  push back into the boundary fade with nothing else changing) was not needed.
- **Deviation D-5 not vetoed** — the GPS nav bar stays wired on non-hub screens. It remains a
  one-line revert from `GpsPolishBuilder.Apply`.
- **Shipped to Ken** — the before/after pair went out with the 2026-09-02 daily report:
  `gps_surface.mp4` (fade between every screen) against `gps_polish_layered_push.mp4` (the push).

## History

| Date | State | Note |
|---|---|---|
| 2026-09-02 | `SPEC_READY` | Motion/polish spec, no Figma nodes. |
| 2026-09-02 | `IMPLEMENTER_WORKING` | Push built and measured (`a7902da27`); D1/D2/D3/D5 done, D6 wired, D4 and D7–D9 partial. Two pre-existing defects surfaced: the nav bar is decorative off the hub, and the profile-pack back buttons are unwired. |
| 2026-09-02 | `IMPLEMENTER_WORKING` | Cesar approved the push and it went into the daily report. Folder stays in `Active/` — the §D remainder is real work, not bookkeeping. |
