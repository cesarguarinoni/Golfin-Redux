# Architect Review — `<task name>`

> Written by `golfin-architect` subagent (final review pass). Reads `SPEC.md`, `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, the screenshot, the Figma reference, and the broader project context. Final gatekeeper before Cesar sees the work.

## Verdict

`PASS` / `FAIL` / `ESCALATE_TO_CESAR`

- **PASS** — Work matches spec; ready for Cesar's final approval.
- **FAIL** — Issues remain that the architect can describe concretely. Routes back to Implementer.
- **ESCALATE_TO_CESAR** — Spec was wrong, or there's a judgment call only Cesar can make (e.g., "Figma frame says X but design intent is Y; which wins?"). Cesar must respond before progress.

## Architectural / cross-cutting checks

Things only the architect can verify (beyond what the self-reviewer caught):

- [ ] Does this work fit the asmdef boundaries cleanly? (No backdoor refs, no autoref violations.)
- [ ] Does this respect existing patterns from `Docs/Architecture/PATTERNS.md`?
- [ ] Does this introduce duplicated logic that should reuse existing utilities?
- [ ] Does the implementation match the *intent* of the spec, not just the letter?
- [ ] Does this break anything else? (Cross-feature implications.)
- [ ] Are there latent bugs the screenshot doesn't show? (Edge cases, null refs, asset loading order.)

| Check | Result | Notes |
|---|---|---|
| Asmdef boundaries | PASS / FAIL | <...> |
| Pattern adherence | PASS / FAIL | <...> |
| ... | ... | ... |

## Visual fidelity verdict

Per-element comparison against Figma reference.

| Element | Spec value | Screenshot shows | Match? |
|---|---|---|---|
| <element 1> | <expected> | <actual> | YES / NO |
| ... | ... | ... | ... |

## Specific FAIL items (if any)

Concrete fix instructions for the Implementer. Cite the spec line or Figma node that defines the correct behavior.

1. **<failed item>** — Spec § <section> says <X>; screenshot shows <Y>. Fix: <concrete change>.

## Open questions for Cesar (only if ESCALATE)

- <question 1>
- <question 2>

## Lessons captured

If this task surfaced a pattern worth remembering, add a one-liner that goes into `tasks/lessons.md` after Cesar approves.

- <lesson>

## Cesar's final approval

Cesar fills this section after eyeballing the screenshot one last time.

- [ ] Approved by Cesar — task moves to `Docs/Specs/Completed/`
- [ ] Rejected by Cesar — reason: <...>
