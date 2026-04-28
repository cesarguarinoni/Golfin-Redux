# Quick Tasks (lightweight workflow)

## When to use this folder

Small, well-scoped tasks where the full multi-agent pipeline would be overkill:

- Bug fixes with an obvious solution
- Small UI tweaks (margin adjustments, color changes, font swaps)
- Adding a single field to a CSV
- Refactors with clear scope and no architectural questions
- Anything Cesar can eyeball in 30 seconds

If the task involves any of these, use the full pipeline at `Docs/Specs/Active/` instead:

- New manager classes or asmdef-touching architecture
- Anything Cesar wants to QA visually against Figma
- Tasks that need cross-cutting review (asmdef boundaries, pattern adherence, latent bugs)
- Tasks where a hallucinated PASS would burn a session

## How to use

1. **Architect** writes a one-page spec into `Docs/Specs/Quick/<task_slug>.md`. No template required; freeform is fine. Just enough for Code to do the work.
2. **Cesar** tells Code: `Read Docs/Specs/Quick/<task_slug>.md and implement.`
3. **Code** implements directly. No subagent chain. No STATUS file. No checklist.
4. **Cesar** eyeballs the result. Approves or asks for tweaks in the same chat thread.
5. **Done** -> Architect moves the file to `Docs/Specs/Quick/Completed/`.

## Why two workflows?

The full pipeline at `Docs/Specs/Active/` is for tasks where the visual-verification + architectural-review gate genuinely catches bugs Cesar can't easily catch in 30 seconds. For a 30-second task, that overhead is pure cost. Quick tasks pay for themselves by skipping it.

If a quick task turns out to be more complex than expected (e.g., it ships a regression Cesar misses), that's a signal to migrate it into the full pipeline. The reverse rarely happens.

## Convention

- Filename: `<task_slug>.md` where slug uses lowercase + underscores
- Examples: `fix_chip_padding.md`, `add_strength_stat_to_csv.md`, `swap_settings_icon.md`
- One task per file. If it grows beyond a page, promote to a per-task folder under `Docs/Specs/Active/`.

## What lives here

This `Docs/Specs/Quick/` folder. Completed quick tasks get moved to `Docs/Specs/Quick/Completed/`. (Created on-demand.)
