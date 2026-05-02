# SYMPHONY_PATTERNS — three patterns borrowed from OpenAI Symphony

> **Status:** QUEUED. Not scheduled. Source: read 2026-04-30 from <https://openai.com/index/open-source-codex-orchestration-symphony/> + SPEC.md outline + InfoWorld critique.
> **Decision context:** Full Symphony adoption rejected for solo-dev scale (Cesar is the only reviewer; higher PR throughput just moves the bottleneck to him). These are the three patterns whose value survives the solo-dev scale-down.
> **Order is rough priority, but each is independent — pick any subset.**

---

## 1. STATUS dashboard command

**Problem.** State of the pipeline lives in `Docs/Specs/Active/*/STATUS.md`. To know what's blocked, idle, or waiting on review, Cesar has to either remember (fails across days/machines) or `cat` each STATUS.md by hand. After a context switch (closing the laptop, opening on Mac, returning from a break) there's no quick "where am I?"

**What to build.** A single script — PowerShell on Windows, optionally Python for cross-platform — that:

- Walks `Docs/Specs/Active/*/`
- For each task folder reads `STATUS.md`, file-mtime of all four canonical files (`SPEC.md`, `IMPLEMENTER_REPORT.md`, `SELF_REVIEW.md`, `ARCHITECT_REVIEW.md`), and `HEARTBEAT.log` if present
- Outputs a single table: task slug | STATUS | last activity (Δt) | report exists? | review exists? | flag
- Flags: `IDLE_>24h` (no STATUS change in 24h), `MISSING_REPORT` (STATUS=PASS-or-DONE but report missing), `MISSING_REVIEW` (report exists, review state but no SELF_REVIEW.md), `STUCK_BLOCKED` (BLOCKED state >12h)
- Optional flag `--all` to include `Completed/`

**Out of scope.** No auto-actions. No notifications. No state mutation. Read-only summary, run on demand.

**Effort.** Small. Single script, ~150 lines PowerShell or Python. No dependencies beyond standard lib.

**Dependency on others below.** None. Standalone.

---

## 2. Proof-of-work bar in `IMPLEMENTER_REPORT.md`

**Problem.** IMPLEMENTER_REPORT.md quality is uneven across tasks. 8.3 needed 4 iterations partly because early reports didn't include the visual side-by-side, so review couldn't catch fidelity gaps until Cesar eyeballed it. 8.5_action_buttons SELF_REVIEW.md was never filled in (Cesar bypassed). Reports vary from "all PASS" with no evidence to thorough breakdowns. The implementer agent definition should make evidence non-optional.

**What to change.** Update `.claude/agents/golfin-implementer.md` to require — as a procedural-reject criterion — every IMPLEMENTER_REPORT.md include:

- **For visual changes:** screenshot diff vs the named reference frame, attached at `Docs/Diagnostics/<phase>/<task-slug>/<element>-attempt-<n>.png`. (Already required by Phase 8 lessons; codify across all visual tasks.)
- **For code changes:** test output snippet showing pass/fail counts. If task spec didn't include tests, an explicit "no tests applicable because <reason>" line.
- **For all changes:** `git diff --stat` block showing files touched + line counts. Single source of truth for "what got changed."
- **Decision log:** any place the implementer made a non-trivial call not specified in SPEC.md, listed with one-line rationale.

Procedural rejection on missing evidence — no content review until evidence is there. Mirrors Phase 8 Rule 1 ("no first-attempt commit without side-by-side") but applies it to all tasks, not just visual ones.

**Effort.** Tiny. Edit `golfin-implementer.md` system prompt + update `Docs/Specs/Active/_TEMPLATE/IMPLEMENTER_REPORT.md` with the four required sections.

**Dependency.** None. But the dashboard (#1) becomes more useful if reports are reliably structured.

---

## 3. `depends_on:` field in spec front-matter

**Problem.** Architect already splits tasks by dependency (8.5 a→b→c→d was a→b→c→d because b consumed a's CSV consolidation, c needed b's LabScaffold inventory, d needed c's selectors). But the dependency is implicit — lives in Architect's head and the spec narrative. Means: dashboard can't show "X is ready, waiting on Y"; Cesar can't quickly answer "what could I work on now?"; if 8.5.B's spec changes, no machine-readable way to know 8.5.C might need updating.

**What to add.** Single line in spec front-matter:

```markdown
**Depends-on:** [8_5_a_csv_consolidation, 8_5_b_lab_inventory_seeder]  <!-- Empty list means no deps. -->
```

Default empty. Architect fills it during spec authoring. Dashboard (#1) reads it and shows: "8_5_c_selector_redesign — STATUS=SPEC_READY but blocked-on: 8_5_b_lab_inventory_seeder (still IN_PROGRESS)."

**Combine with parallel-safe.** A task with `depends_on: []` AND no file overlap with another `depends_on: []` task is a parallel-fan-out candidate. Architect can flag it explicitly. Cesar approves. (Note: I had this idea earlier today as "Parallel-Safe: yes" template field. Subsume it under depends_on — empty deps + no file overlap = parallelizable. One field, not two.)

**Effort.** Tiny. Update `_TEMPLATE/SPEC.md`. Tiny addition to dashboard parser if/when #1 is built.

**Dependency.** Becomes most useful with #1, but adds value alone (machine-readable spec metadata for future tooling).

---

## What we're explicitly NOT borrowing from Symphony

- **Polling loop / continuous orchestrator.** Symphony watches a board and auto-spawns agents. We don't want auto-spawn — Cesar's manual paste IS the safety gate.
- **Linear or other issue-tracker integration.** Markdown files are fine. Adding Linear adds complexity and a paid tool dependency for a solo dev.
- **Auto-rebase, auto-PR, CI shepherding.** Cesar reviews everything before merge. The bottleneck is review, not PR creation.
- **The "500% more PRs" target.** Output volume is not the constraint. Validation is. Symphony's headline metric doesn't apply.

---

## When to revisit

- If/when there's a second human reviewer (e.g., contractor or full-time hire), Symphony's full pattern becomes interesting again — decoupling implementation from supervision pays when supervision can scale.
- If/when CI for GolfinRedux gets robust enough that automated tests can stand in for visual review on a meaningful fraction of tasks.
- If/when active task count regularly exceeds 5+ concurrent (today peaks around 4–5; cognitive load isn't the bottleneck yet).
