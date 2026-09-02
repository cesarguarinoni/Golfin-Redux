IMPLEMENTER_WORKING

# STATUS — `gps_polish`

**Current:** `IMPLEMENTER_WORKING` — iteration 1 landed the layered push and its gates; several
§D polish items remain. See `IMPLEMENTER_REPORT.md` § Not done for the exact remainder.

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

## Needs Cesar

- **Deviation D-5** — the GPS nav bar was wired on non-hub screens. It is a one-line revert. The
  probe found that at HEAD a player who reaches Profile, Badges or Avatar has **no way back**, and
  two acceptance items depend on the bar working. Veto if you would rather ship the dead bar.
- **The push itself** — `videos/gps_polish_a_push_walkthrough.mp4` is the gamble. If it reads
  badly, the fallback is the plain fade: `UiMotion.Enabled = false` turns every push back into the
  boundary fade with no other change.

## History

| Date | State | Note |
|---|---|---|
| 2026-09-02 | `SPEC_READY` | Motion/polish spec, no Figma nodes. |
| 2026-09-02 | `IMPLEMENTER_WORKING` | Push built and measured (`a7902da27`); D1/D2/D3/D5 done, D6 wired, D4 and D7–D9 partial. Two pre-existing defects surfaced: the nav bar is decorative off the hub, and the profile-pack back buttons are unwired. |
