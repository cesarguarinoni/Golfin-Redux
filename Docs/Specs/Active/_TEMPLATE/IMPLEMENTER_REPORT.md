# Implementer Report — `<task name>`

> **MANDATORY:** Every checklist item from `SPEC.md` must be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. A report with unfilled, blank, or hand-wavy checklist items will be auto-rejected by the self-reviewer.

## Implementation summary

<2-3 sentences on what was built, in plain prose>

## Files modified or created

| Path | Change |
|---|---|
| `<path>` | <created / modified / deleted, with one-line description> |

## Screenshot

- **Canonical screenshot:** `screenshots/<file>.png`  ← REQUIRED (Rule 14). Long edge ≥ 900px. Name the SINGLE frame the reviewer should judge; pick the angle that REVEALS the feature, not a flattering thumbnail/overhead.
- **Captured at:** `screenshots/<timestamp>.png` (or `.jpg`)
- **Scene loaded:** `<scene path>`
- **Play mode:** Yes / No
- **Hole loaded (if applicable):** `<hole id>`

## Rejection follow-up

Only required when `CESAR_REJECTION.md` exists (Rule 15 — hook-enforced; delete this section otherwise). For EACH defect Cesar flagged, re-shoot the exact angle and give a verdict + full-res citation.

| Rejected defect | Verdict | Evidence (same-angle, full-res) |
|---|---|---|
| <defect Cesar flagged> | GONE / RESOLVED / STILL PRESENT | `screenshots/<file>.png` |

## Figma fidelity

Required when `SPEC.md` references a Figma node (Rule 18 — hook-enforced; delete this section otherwise). One row per UI element the task touches — enumerate **every border/outline** and every **relocated/derived** element. A/B each against the pulled node render in `reference/` (or live `mcp__figma__get_screenshot`), NOT the spec's prose. "matches" is not acceptable — cite the measured value. Flagged-but-accepted deviation = `PASS*` (note it under § Spec deviations).

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| <e.g. Banner top/bottom border> | `<node>` | 3px solid #818EA1 | <pixel-sampled value> | PASS / FAIL |
| <e.g. Relocated map> | `<node>` | above Fade/Draw, image-only | <built value> | PASS / FAIL |
| <...> | `<node>` | <...> | <...> | PASS / FAIL |

## UI fidelity lint

Required when `SPEC.md` references a Figma node (Rule 21 — hook-enforced; delete this section otherwise). Run `Golfin.EditorTools.UIFidelity.UIFidelityLinter.LintPrefab("Assets/…/X.prefab", "reference/nodes/X_spec.json")` (via `mcp__unity__script-execute`) on EVERY new/changed prefab. It writes `Docs/Diagnostics/_capture/<prefab>_lint.json`. Fix every FAIL until `fail == 0`, then cite each JSON below. Render-health catches oval pills / distorted radii / flat-fills with no reference; node-spec catches size/gap/radius/sprite/color/font off the node. Any `fail > 0` or an uncited/missing JSON blocks the transition.

| Prefab | Lint JSON | fail | warn |
|---|---|---|---|
| <e.g. StaminaMenuRow.prefab> | `Docs/Diagnostics/_capture/StaminaMenuRow_lint.json` | 0 | <n> |

## Acceptance checklist (copy from SPEC.md, fill every line)

| Item | Result | Justification |
|---|---|---|
| <check 1 from spec> | PASS / FAIL | <one sentence citing what was measured, e.g. "Portrait RectTransform sizeDelta = (180, 180) verified in Inspector"> |
| <check 2> | PASS / FAIL | <...> |
| ... | ... | ... |

## Known FAIL items

If any items above are `FAIL`, list them here with what's blocking and what would unblock. **Do NOT mark the task done; surface to architect-review instead.**

- <fail 1>
- <fail 2>

## Spec deviations

If you deviated from the spec for any reason, list each deviation with justification. If none, write "None."

- <deviation>: <reason>

## Console output

Any warnings/errors related to this task that appeared during play mode. Paste verbatim.

```
<console output>
```

## Open questions for Architect

If anything in the spec was ambiguous or seemed wrong, list it here. **Do NOT silently invent a resolution; surface it.**

- <question>
