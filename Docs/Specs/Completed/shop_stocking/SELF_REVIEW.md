# Self-Review — `<task name>`

> Written by `golfin-self-reviewer` subagent. Reads `SPEC.md`, `IMPLEMENTER_REPORT.md`, the screenshot, and the Figma reference. Catches obvious failures before they reach the architect.

## Verdict

`PASS` / `FAIL` / `ESCALATE`

- **PASS** — All checklist items genuinely PASS in the screenshot. Routes to `golfin-reviewer` for final review.
- **FAIL** — Implementer's report contained false PASSes, OR obvious failures visible in screenshot, OR mandatory items unfilled. Routes back to `golfin-implementer` with fail list.
- **ESCALATE** — Spec ambiguity, missing information, or judgment call beyond self-reviewer's scope. Routes to `golfin-reviewer`.

## Checklist verification

For each item in `IMPLEMENTER_REPORT.md`, the self-reviewer either confirms or overrides:

| Item | Implementer said | Self-reviewer says | Notes |
|---|---|---|---|
| <check 1> | PASS | CONFIRMED / OVERRIDE-FAIL | <what the screenshot actually shows> |
| <check 2> | FAIL | CONFIRMED-FAIL / OVERRIDE-PASS | <...> |
| ... | ... | ... | ... |

## Specific failures (if any)

For every OVERRIDE-FAIL or CONFIRMED-FAIL, write a concrete fix instruction the Implementer can act on without re-reading the entire spec.

1. **<failed item>** — <what's wrong in the screenshot, what the spec said, what to change in code/scene to fix>
2. **<...>**

## Visual diff notes

Free-form observations comparing the screenshot to the Figma reference. Things that aren't on the checklist but are clearly off.

- <observation>

## Figma fidelity

Required when `SPEC.md` references a Figma node (Rule 18). Do NOT accept the implementer's blanket "matches Figma" — build your own per-element table diffed against the pulled node renders (`reference/` or live `mcp__figma__get_screenshot`). Enumerate **every border/outline** + every **relocated/derived** element. Any element you can't confirm against the actual node render → FAIL the row → `BACK_TO_IMPLEMENTER`.

| Element | Figma node | Figma value | Built value | Result |
|---|---|---|---|---|
| <element> | `<node>` | <figma value> | <built> | PASS / FAIL |

## Routing

Final routing decision (one of):

- `BACK_TO_IMPLEMENTER` with fail list above
- `FORWARD_TO_ARCHITECT` for final review (routes to `golfin-reviewer`)
- `ESCALATE_TO_ARCHITECT` with question(s) for Cesar (routes to `golfin-reviewer`)

(Routing labels retain the `_ARCHITECT` suffix to match the existing STATUS values — `READY_FOR_ARCHITECT_REVIEW` etc. — which are still used throughout the pipeline. The agent they route to is now `golfin-reviewer`.)

## Iteration count

This is iteration **<N>** of self-review for this task. If N ≥ 3, escalate regardless of verdict — three rounds of FAIL means the spec or the approach has a deeper problem only the reviewer can resolve.
