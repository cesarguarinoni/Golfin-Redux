# Implementer Report — `putter_p1_ui`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

_(2-3 sentences on what was built, in plain prose)_

## Files modified or created

| Path | Change |
|---|---|
| _(fill in)_ | _(fill in)_ |

## Screenshot

- **Captured at:** `screenshots/putter-mode-diff-v1.png`
- **Scene loaded:** `Assets/Scenes/Physics/LabScaffold.unity`
- **Play mode:** Yes
- **Hole loaded:** _(fill in — Hole 6 recommended for visible slope)_

## Acceptance checklist (copy from SPEC.md, fill every line)

| Item | Result | Justification |
|---|---|---|
| Top bar identical to standard mode | PASS / FAIL | _(...)_ |
| HoleIndicator distance reads `mts` | PASS / FAIL | _(...)_ |
| Cone graphic hidden when putter mode active | PASS / FAIL | _(...)_ |
| Putter track 140 × 1000 anchored center | PASS / FAIL | _(...)_ |
| Track gradient lighter edges, darker center | PASS / FAIL | _(...)_ |
| Three band lines at 200/500/1000 in green/amber/red | PASS / FAIL | _(...)_ |
| Putter handle sprite shows correctly | PASS / FAIL | _(...)_ |
| Handle Y slides with power | PASS / FAIL | _(...)_ |
| Handle X locked at 0 in putter mode | PASS / FAIL | _(...)_ |
| Central ball renders at 150×150 in putter mode | PASS / FAIL | _(...)_ |
| Power gauge text shows `mts` suffix | PASS / FAIL | _(...)_ |
| Power gauge max ≈ ComputeMaxPuttRangeMeters | PASS / FAIL | _(...)_ |
| Predicted-path line is a polyline (multiple segments) | PASS / FAIL | _(...)_ |
| Predicted-path line curves on slope | PASS / FAIL | _(...)_ |
| Predicted-path line terminates at predicted stop | PASS / FAIL | _(...)_ |
| Default mode: blue gradient line, alpha 1.0 → 0.2 | PASS / FAIL | _(...)_ |
| Heatmap mode: green→yellow→red speed-coded line | PASS / FAIL | _(...)_ |
| Power=0 case: line hides | PASS / FAIL | _(...)_ |
| Top action button row hidden | PASS / FAIL | _(...)_ |
| Bottom action button row visible | PASS / FAIL | _(...)_ |
| Ball selector at 50% alpha, non-interactable | PASS / FAIL | _(...)_ |
| Putter selector fully opaque, fully interactable | PASS / FAIL | _(...)_ |
| Switching to non-putter exits putter mode | PASS / FAIL | _(...)_ |
| No white-box placeholders | PASS / FAIL | _(...)_ |
| All `[SerializeField]` refs wired | PASS / FAIL | _(...)_ |
| Unity Console clean | PASS / FAIL | _(...)_ |
| Performance: prediction mean < 2ms | PASS / FAIL | _(measured: mean __ ms / p95 __ ms / max __ ms over 60 frames)_ |
| Spec deviations flagged below | PASS / FAIL | _(...)_ |

## Known FAIL items

_(If any items above are FAIL, list them here with what's blocking and what would unblock. Do NOT mark the task done; surface to architect-review instead.)_

## Spec deviations

_(If you deviated from the spec for any reason, list each deviation with justification. If none, write "None.")_

## Console output

```
_(paste verbatim)_
```

## Open questions for Architect

- `putt_mode_club_lock_decision` — should the PUTTER selector be tappable in putter mode, allowing switch back? Default v1: yes (switching out exits putter mode). Confirm with Cesar.
- `putt_slab_visual_decision` — does the existing Timing slab look reasonable when traversing the new putter track? If it feels visually off, gate it off in putter mode.
- `putt_predictor_perf` — measured cost (mean/p95/max above). If > 5ms p95, propose mitigation.
- _(any new questions surfaced during implementation)_
