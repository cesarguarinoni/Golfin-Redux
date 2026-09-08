DONE

# STATUS — `game_polish_c`

**Current:** `DONE` (2026-09-08) — approved by Cesar. Notion 2111, slice c of three — the sweep.
Runs after `game_polish_b` (DONE `9c3ae0daf`).

All four sweeps closed, every gate measured against the committed tree:

| gate | before | after |
|---|---|---|
| C1 player-facing Buttons without `ButtonPressFeedback` | 242 | **0** |
| C2 in-scope ScrollRects off the reference | 11 | **0** |
| C3 surfaces with content in the iPhone 15 Pro Max safe-area bands | 4 | **0** |
| C4 Toast fade, sampled off the running coroutine | — | **ease-out, worst residual 0.0024** (vs 0.2726 linear) |
| A5 rest movement attributable to the task | — | **inside the control envelope on every statistic** |
| EditMode | — | **2886 pass / 0 fail / 3 pre-existing skips** |

| Date | State | Note |
|---|---|---|
| 2026-09-08 | `SPEC_READY` | Button press-feedback backfill (354 vs 102 serialized refs), one scroll feel, safe area per screen, Toast on UiMotion. |
| 2026-09-08 | `READY_FOR_ARCHITECT_REVIEW` | 254 feedback components (119 prefab / 135 scene), 11 ScrollRects to the Elastic reference, 2 safe-area intrusions fixed with `SafeAreaFitter` at baseline 141, Toast on `UiMotion.Tween`. 3 new EditMode suites + §20 tripwire. See `IMPLEMENTER_REPORT.md`; per-site tables in `TABLES.md`. |
| 2026-09-08 | `DONE` | Approved by Cesar. Implementation committed `d2da35695`; folder moved to `Docs/Specs/Completed/`. |
| 2026-09-08 | `DONE` | Follow-up: Cesar chose the eased toast — `ToastController.Fade` is `UiMotion.Fade` now. Deviation D-1 resolved; A6 re-measured off the running coroutine. |
