# SPEC — `<task name>`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md` for current pipeline state. Possible values:

- `SPEC_READY` — Architect wrote the spec, Implementer can start
- `IMPLEMENTER_WORKING` — Code is mid-implementation
- `READY_FOR_SELF_REVIEW` — Code finished and filled `IMPLEMENTER_REPORT.md`; self-reviewer fires next
- `SELF_REVIEW_PASS` — Self-reviewer approved; architect-review fires next
- `SELF_REVIEW_FAIL` — Self-reviewer found issues; routed back to Implementer
- `READY_FOR_ARCHITECT_REVIEW` — Self-reviewer escalated; architect-review fires next
- `ARCHITECT_REVIEW_PASS` — Architect approved; ready for Cesar's final approval
- `ARCHITECT_REVIEW_FAIL` — Architect rejected; routed back to Implementer with fail list
- `ARCHITECT_REVIEW_ESCALATE` — Architect can't decide alone; needs Cesar
- `DONE` — Cesar approved; spec moves to `Docs/Specs/Completed/`

## Goal

<one-paragraph statement of what this task accomplishes and why>

## Reference

- **Figma frame:** <page name> / <frame name> / id `<node id>` in file `<file key>`
- **Reference PNG:** `<path/to/Reference.png>` (companion for visual diff)
- **Placeholder vs canonical content notes:** <which text/data in the Figma is mockup>

## Architecture context

- **Asmdef boundaries affected:** <list>
- **Existing code referenced:** <list of class names + paths>
- **Existing assets referenced:** <list of paths>
- **Manager APIs used:** <list with method signatures>

## Implementation

<step-by-step build instructions, RectTransform values, hierarchy, code skeletons, etc>

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

Each item below MUST be marked `PASS` or `FAIL` with a one-sentence justification citing what was measured. The Implementer cannot mark the task done without filling every line. The self-reviewer will reject any report with unfilled or unjustified checklist items.

- [ ] <visual or functional check 1>
- [ ] <check 2>
- [ ] <...>
- [ ] No white-box placeholders visible in the screenshot
- [ ] All `[SerializeField]` references wired in the Inspector
- [ ] Unity Console has no errors related to this task
- [ ] Spec deviations (if any) are flagged at the bottom of the report with justification

## Files / hierarchy this task touches

- <path 1> — <what changes>
- <path 2> — <what changes>

## Smoke evidence

Describe how the implementation will be verified before marking IMPLEMENTER_REPORT done.

### Visual-fidelity verification (Lesson O)

When the spec involves visual fidelity — camera tracking, animation timing, ball/ribbon rendering, mode transitions, SmoothDamp targets, or any deliverable where player-perceived behavior is the success criterion — runtime event-dispatch captures (e.g., `OnModeChanged`, `OnStateChanged`, `OnShotComplete`) are NECESSARY but NOT SUFFICIENT.

Visual fidelity REQUIRES one of:
- **Human-in-the-loop play-and-confirm.** Implementer loads the scene, drives the flow manually, and writes a content-sanity description in IMPLEMENTER_REPORT.md describing what the camera/animation/ball visually did. Auditable by Cesar and reviewer.
- **Position-trace assertion.** EditMode or PlayMode test reads actual Transform positions over multiple frames and asserts tracking against the expected reference.

Mode-history captures + screenshot files alone are dispatch evidence, not visual evidence. See `Docs/Diagnostics/PIPELINE_LESSONS.md` Lesson O for the full failure analysis.

## Out of scope (do NOT do these)

- <explicit non-goals so Implementer doesn't scope-creep>
