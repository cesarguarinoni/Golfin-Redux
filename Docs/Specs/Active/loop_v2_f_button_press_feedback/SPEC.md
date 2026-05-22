# SPEC — `loop_v2_f_button_press_feedback`

**Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state.

## Status

See `STATUS.md`. Initial: **PART_A_SHIPPED_BY_ARCHITECT / PART_B_SPEC_READY** — Part A (component file + transition audit) shipped surgically by Architect. Part B (MCP attach to ~10 button surfaces) is the Implementer's slice.

## Goal

Add tactile press-feedback (1.0 → 0.95 → 1.0 over 0.12s) to every user-facing button in Loop v2 surfaces, and verify all user-driven `ScreenManager.ShowScreen(...)` calls go through `FadeController.FadeOutThenIn` (no `instant: true` shortcuts crept in). Both deliver the closing polish for Loop v2.

## Pre-flight findings (locked 2026-05-22)

| Check | Result |
|---|---|
| Repo HEAD before this stage | `c6f937b4` (Stage E Part A) |
| `instant: true` callers across user-driven paths | ✓ Only one in the codebase: `GameplaySceneLoader.cs:74` — the legit C0 caller (the outer FadeController owns the visible fade; `instant: true` on the inner ShowScreen call is correct). **Zero offenders. Audit complete with zero code changes required.** |
| Existing animation deps in project (DOTween, etc.) | None — hand-rolled coroutine is the right call. |

## Part A — Component file + audit (SHIPPED BY ARCHITECT)

**Pipeline:** SURGICAL (1 new file, 1 audit pass). Already committed by Architect before this SPEC landed.

### New file

`Assets/Scripts/UI/ButtonPressFeedback.cs` — namespace `Golfin.UI.Polish`, ~110 lines, single class.

- `[DisallowMultipleComponent]` + `[RequireComponent(typeof(RectTransform))]` guards.
- Implements `IPointerDownHandler` so it fires on the press, not the click (immediate tactile response).
- Coroutine uses `Time.unscaledDeltaTime` so the pulse still plays during a paused timeScale (e.g. while a modal is fading in over a frozen background).
- `OnDisable` restores the original `localScale` if the pulse is interrupted mid-press (button hidden during animation) — never leaves the button "stuck small".
- Respects the host `Button.interactable` flag: non-interactable buttons emit no feedback.
- Re-entrant press handling: a fast double-tap cancels the running pulse and starts fresh.
- Tunables (SerializeFields) with `[Range]`:
  - `_pressedScale` (default 0.95, range 0.5–1.0)
  - `_duration` (default 0.12s, range 0.04–0.5)

### Audit pass

Grep result (Architect): `grep -rn 'ShowScreen.*instant' Assets/Scripts --include='*.cs'` returns exactly two lines, both in `ScreenManager.cs` itself (the definition + a debug log), plus one caller `GameplaySceneLoader.cs:74` which is the legit C0 instant call. **No fix needed.**

### Acceptance for Part A

- [x] `ButtonPressFeedback.cs` created at `Assets/Scripts/UI/ButtonPressFeedback.cs`
- [x] Namespace, attributes, and coroutine pattern match the spec above
- [x] FadeController audit done; zero offenders confirmed
- [x] No asmdef changes
- [x] No other files modified

## Part B — Attach component to ~10 button surfaces (TELLCODE, MCP)

**Pipeline:** TELLCODE via Unity MCP. Pure scene/prefab edit operations — no code changes, no SerializeField wiring needed (the component has internal-only state). Each attach is one MCP call.

### Buttons to attach

Implementer should use Unity MCP `find` / `add_component` to attach `Golfin.UI.Polish.ButtonPressFeedback` to each of the following GameObjects. If any button's GO name differs from what's listed, Implementer should find by Button-component traversal and report the actual name.

| # | Location | GameObject name | Notes |
|---|---|---|---|
| 1 | HomeScreen | `PLAY` | The primary PLAY shortcut on the Home screen |
| 2 | HoleCard prefab | `ActionButton` (SerializeField is `actionButton`) | The PLAY/REPLAY button on the expanded hole card. Attach on the **prefab**, not a scene instance, so all 18 cards pick it up. |
| 3 | HoleCompleteWidget — Card 1 | `ReplayButton` | REPLAY on SUCCESS state |
| 4 | HoleCompleteWidget — Card 1 | `RetryButton` | RETRY on FAILED state |
| 5 | HoleCompleteWidget — Card 2 | `PlayButton` | PLAY NEXT on SUCCESS state |
| 6 | PersistentUI bottom-nav | `NavHomeButton` | |
| 7 | PersistentUI bottom-nav | `NavTeeButton` | |
| 8 | PersistentUI bottom-nav | `NavRosterButton` (verify name) | Skip if not present |
| 9 | PersistentUI bottom-nav | `NavInventoryButton` (verify name) | Skip if not present |
| 10 | PersistentUI top-bar | `SettingsButton` | Settings open |
| 11 | SettingsPanel | `CloseButton` | Settings close |

Skip the Matchmaking modal — its only button is a cancel/close action that's part of the modal's own dismiss flow; adding pulse there is low-value and risks interfering with the auto-scan timing.

### Procedure

For each button:

1. `find` the GameObject by name (or component traversal if name unknown).
2. Confirm it has a `UnityEngine.UI.Button` component.
3. `add_component` of type `Golfin.UI.Polish.ButtonPressFeedback`.
4. Leave SerializeField defaults (`_pressedScale = 0.95`, `_duration = 0.12`) untouched unless Cesar pushes back on the feel.

### Acceptance for Part B

Implementer fills `IMPLEMENTER_REPORT.md`:

- [ ] All buttons in the table above have `ButtonPressFeedback` attached (or are explicitly marked SKIPPED with reason — e.g. "NavRosterButton not present in current PersistentUI")
- [ ] HoleCard prefab edit done at prefab level, not on a scene instance, so all 18 cards inherit it
- [ ] Project compiles clean (no missing-script warnings)
- [ ] Visual gate: at least three buttons (suggest PLAY, NavHomeButton, ReplayButton) verified visually to scale-pulse on tap, either via the Stage E Part B smoke-bot recording OR via manual play
- [ ] No baked references to obsolete buttons (deleted prefab nodes etc.)
- [ ] No changes to `ButtonPressFeedback.cs` itself unless Cesar explicitly asks for a feel tweak

### Out of scope

- Adding any new animation library (DOTween, LeanTween, etc.)
- Touching the `FadeController` or `ScreenManager` transition logic
- Polishing the modal fade animations (those use `ModalController.Show/Hide` already)
- Sound effects on press (separate future polish pass)

## Definition of done (Stage F overall)

- Part A shipped, audit pass logged here
- Part B attaches confirmed across the listed buttons
- Visual gate passes (Cesar sees the press-pulse on at least the primary buttons during the next bot run OR a manual session)
- Notion Order flipped to Done, Closed date set
- Loop v2 milestone is now feature-complete — all six stages (A, B, C0, C1, D-absorbed, E, F) shipped
