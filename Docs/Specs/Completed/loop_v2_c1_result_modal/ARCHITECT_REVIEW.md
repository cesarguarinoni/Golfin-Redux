# Architect Review — loop_v2_c1_result_modal (Stage C1 — ShellScene Result Modal)

**Reviewer:** golfin-reviewer
**Date:** 2026-05-21 19:32 CEST
**Verdict:** ARCHITECT_REVIEW_PASS
**Iteration:** 8 (single-defect fix iteration — addresses the sole iter-7 FAIL: Toast
Canvas `m_OverrideSorting: 0` made `sortingOrder: 950` inert)

---

## Independent visual scan (written BEFORE reading IMPLEMENTER_REPORT / SELF_REVIEW / prior verdict)

**iter8_s03_hole18_cleared_toast_overridesorted.png (Hole 18 — single card + toast):** One
dark-navy rounded result card centred over a blurred grass-green gameplay backdrop; a small
character HUD strip ("JAMES / LV 10 / TURN 1" left, "LOMOND / HOLE 1 - REGULAR / PAR 5"
right) and a gear icon sit at the top. The card has a green "✓ SUCCESS" header, a white
subhead "Lomond Country Club  - Hole 18 - Par 5", a real green golf-hole map sprite on the
left (S-curve fairway with a green putting surface, no magenta), a left-aligned stats block
"TEE OFF: REGULAR / STROKES: 1 (-4) / BEST: — / TIME: 00:00:00 / BEST: —", a reward row of
three chips reading "x100" (gold-selected) / "x10" / "x5" each on a single line, and a light
gray "REPLAY" button. There is NO second card below — Card 2 is correctly hidden for the
final hole. Near the bottom of the screen, over the club tray, a dark pill-shaped toast
reads "COURSE CLEARED!" — fully legible, not occluded by anything.

The frame matches what the iter-7 reviewer described for `iter7_s03`, with the toast still
clearly visible. No magenta, no clipped text, no text outside any container. My pixel scan
agrees with the implementer's and self-reviewer's visual claims — no disagreement.

---

## Figma side-by-side — no Figma frame; lab `HoleCompleteWidget.prefab` is canonical

Per `CESAR_REJECTION.md` (authoritative) the production modal reuses the FULL lab
`HoleCompleteWidget`. Iteration 8 changed nothing visual — it is a one-property scene-wiring
flip. The iter-7 per-element side-by-side table (all rows MATCH) stands; iter8_s03 is the
re-capture of the one frame the FAIL touched (Hole 18). Per-element confirmation for the
re-captured frame:

| Element | Lab reference | iter8_s03 capture | Match |
|---|---|---|---|
| Single card (Card 2 hidden, Hole 18) | one card, rounded navy panel | one card only, no Card 2 below | Matches SPEC §0 |
| Card 1 SUCCESS header | green "✓ SUCCESS" | green "✓ SUCCESS" | Matches |
| Card 1 subhead | "Lomond Country Club  - Hole N - Par P", single line | "Lomond Country Club  - Hole 18 - Par 5", single line | Matches |
| Card 1 hole-map | green hole-shape graphic | real green Lomond Hole-18 map, no magenta | Matches |
| Card 1 stats block | TEE OFF / STROKES / BEST / TIME / BEST | identical 5-line block, STROKES green | Matches |
| Card 1 rewards row | 3 chips, single-line counts | "x100 / x10 / x5", single-line, gold-selected first | Matches |
| Card 1 button | REPLAY (success) | light-gray "REPLAY" | Matches |
| "COURSE CLEARED!" toast | dark pill, bottom-center | dark pill toast bottom-center, fully legible | Matches SPEC §0 — and now correctly z-ordered (see below) |

No visible divergence introduced by iteration 8.

---

## Confirmation of the single-line fix (task-prompt item 1)

The task is uncommitted in the working tree (no per-iteration commits exist for this task —
`git log` shows only the SPEC-authoring commit `7a6f95f3`), so `git diff` against HEAD shows
the CUMULATIVE diff of the entire C1 task, not the iter7→iter8 delta. The iteration boundary
cannot be diffed via a commit. Instead I verified the fix the deterministic way — by
inspecting the on-disk YAML and cross-checking against the exact defect the iter-7 FAIL
named:

- **iter-7 FAIL-1 was:** `ShellScene.unity` Toast PrefabInstance's scene-added Canvas
  (`!u!223 &1838651179`) had `m_OverrideSorting: 0`, making `m_SortingOrder: 950` inert.
- **iter-8 on-disk state** (`grep` of `ShellScene.unity`, Canvas block `&1838651179`,
  lines 86011–86029):

```
m_RenderMode: 2
m_OverrideSorting: 1      <-- FIXED (was 0)
m_SortingOrder: 950       <-- unchanged
m_TargetDisplay: 0
```

- **`git diff -- ShellScene.unity` removal count: 0.** The full ShellScene diff is purely
  additive (377 insertions, 0 deletions). No `m_IsActive: 0`, no `sizeDelta` mutation, no
  position change on any pre-existing GameObject. The iter-7 scene-mutation audit (additive
  only) still holds; the one-property flip from 0→1 is an in-place value change inside the
  already-additive Toast Canvas block, not a new mutation of pre-existing scene content.

The iter-7 architect review verified every other line item PASS. The `git diff` confirms
nothing outside the Toast Canvas property changed since then — no other scene mutation,
nothing else touched. **Single-fix confirmed.**

---

## Z-order verification (task-prompt item 3)

`script-execute` Unity MCP is not in this session's toolset; z-order was verified
deterministically from the scene/prefab YAML — an equivalent reproducible procedure (the
same method the iter-7 review used for its bbox check). The YAML on disk is authoritative
for what loads at runtime.

| Canvas | File | overrideSorting | sortingOrder | Source |
|---|---|---|---|---|
| Toast | `ShellScene.unity` `&1838651179` (line 86018/86025) | `1` (true) | `950` | scene-added Canvas |
| HoleCompleteModal | `HoleCompleteModal.prefab` Canvas (line 2384/2391) | `1` (true) | `900` | prefab (untracked new C1 file, unchanged) |

Both Canvases are overriding (`m_OverrideSorting: 1`). An overriding child Canvas
establishes its own sorting context against its parent; with both overriding, the higher
`sortingOrder` draws on top. Toast `950 > 900` Modal → **the Toast renders ABOVE the
modal**, exactly per the SPEC §5 locked decision ("Modal canvas sortingOrder = 900. Toast
canvas sortingOrder = 950. LoadingScreen stays 1000"). The implementer's report additionally
documents a runtime `script-execute` confirmation (`overrideSorting=True sortingOrder=950`),
consistent with the YAML.

**Modal Canvas unchanged:** `HoleCompleteModal.prefab` is an untracked new file (entirely
C1's own content); its Canvas still reads `m_OverrideSorting: 1, m_SortingOrder: 900`. The
ShellScene modal PrefabInstance carries no Canvas-property override in its modification
list (confirmed in the diff). Modal z-config is untouched by iteration 8.

iter-7 FAIL-1's worst case — "if the modal card were taller or the toast repositioned, the
toast would be occluded behind the 900-order modal" — is now structurally resolved: the
toast's 950 overriding Canvas draws above the modal's 900 regardless of geometric overlap.

---

## Scene-mutation audit — `git diff` (Lesson 2026-05-13)

`git diff -- Assets/Scenes/ShellScene.unity`: 377 insertions, **0 deletions**. Grep for
removed `m_IsActive: 0` / `m_SizeDelta` / `m_AnchoredPosition` on pre-existing GameObjects:
**none** — every such line is `+`-prefixed inside the new `HoleCompleteModal` / `Toast`
PrefabInstance blocks. `LabScaffold.unity` and `HoleCompleteWidget.prefab` are unchanged
from iter-7 (iteration 8 modified only `ShellScene.unity`, per IMPLEMENTER_REPORT, and the
diff bears that out). No capture-driven scene corruption. **PASS.**

---

## Production-flow capture verification

`iter8_s03_hole18_cleared_toast_overridesorted.png` is a FRESH capture (timestamp
2026-05-21 19:24:11) produced by `LoopV2SmokeBot` scenario `hole18_course_cleared` driving
the real production path (`ForceShotComplete` → `GameSession.OnHoleComplete` →
`HoleCompleteModalController.HandleHoleComplete` → `_widget.Show` + `ToastController.Show`),
captured via `CaptureCore.SnapPlayModeSafe` (sanctioned path; console line
`[BotDriver] Capture: s02_result_modal_h18_cleared`). This is a production-flow capture, not
a smoke-runner-only state injection. **PASS.**

---

## IMPLEMENTER_REPORT internal consistency (task-prompt item 4)

- The Hole-18 toast checklist item is now graded **PASS** with the fix documented (line 79):
  `m_OverrideSorting` 0→1, runtime-verified, modal Canvas unchanged at 900. Consistent with
  the on-disk YAML I inspected.
- The iter-7 PNG count inaccuracy (17→18) is **corrected** in the iter-8 report (line 16 and
  the files table) — Hole_01.png was also swapped to the Lomond art set; one consistent art
  style across all 18 holes. Matches `git diff --stat` and the iter-7 self-review note.
- No FAIL items declared; no open questions. EditMode tests: iteration 8 changed only
  `ShellScene.unity` YAML, no C# recompilation, so the iter-7 gate of 314 pass / 0 fail /
  3 skip (3 disclosed `[Ignore]`s) carries forward legitimately — the report says exactly
  this and the reasoning is sound (a scene-YAML edit cannot change EditMode test results).
- One small report wording note (non-blocking, no action): the console block (lines
  150–154) shows `[Fix] Before: overrideSorting=True` because Unity had already reloaded the
  edited scene from disk before the runtime verification script ran; the report explains
  this honestly. Both Before and After confirm the correct final state. Not a defect.

Report is internally consistent.

---

## Definition of Done — re-confirmation

Every iter-7 PASS row stands (nothing outside the Toast Canvas property changed per the
`git diff`). The single FAIL row is now resolved:

| Criterion | iter-7 | iter-8 |
|---|---|---|
| Two-card lab widget, subscriptions, scene-swap survival | PASS | PASS (unchanged) |
| Card 1 SUCCESS/FAILED full layout | PASS | PASS (unchanged) |
| Card 2 NEXT / LOCKED states | PASS | PASS (unchanged) |
| Hole 18: Card 2 hidden + "COURSE CLEARED!" toast | PASS *visually* | PASS — toast now correctly z-ordered above modal |
| Magenta hole-maps eliminated | PASS | PASS (unchanged) |
| Reward "x100" single-line | PASS | PASS (unchanged) |
| Action handlers (REPLAY/RETRY/PLAY) + progression writes | PASS | PASS (unchanged) |
| Reward grant on SUCCESS | PASS | PASS (unchanged) |
| Verified-GOOD C# intact; double-fire strip | PASS | PASS (unchanged) |
| All `[SerializeField]`s wired | PASS | PASS (unchanged) |
| EditMode tests 314 / 0 / 3 | PASS | PASS (no C# change, gate carries) |
| **Toast Canvas `sortingOrder = 950` effective (SPEC §5)** | **FAIL** | **PASS — `overrideSorting = 1`, `sortingOrder = 950`, draws above modal 900** |

---

## Verdict rationale

Iteration 8 addresses exactly the one defect the iter-7 review raised and nothing else. The
Toast PrefabInstance's scene-added Canvas in `ShellScene.unity` now reads
`m_OverrideSorting: 1` with `m_SortingOrder: 950` unchanged — verified directly in the
on-disk YAML (line 86018/86025). With both the Toast Canvas (950) and the modal Canvas (900)
overriding, the toast draws above the modal regardless of geometric overlap, satisfying the
SPEC §5 locked decision and the `ToastController` class's own documented contract. The
modal Canvas is untouched. The `ShellScene.unity` diff is purely additive (0 deletions), so
nothing else in the scene was mutated since iter-7. The fresh production-flow Hole-18
capture confirms the SUCCESS modal renders correctly (single card, green hole-map,
single-line rewards) with the "COURSE CLEARED!" toast clearly visible. The IMPLEMENTER_REPORT
grades the toast item PASS, corrects the 17→18 PNG count, and is internally consistent.

Every other line item was verified PASS in iteration 7 and the git diff shows none of it
changed. No defect remains.

Route to Cesar for final approval.

---

## STATUS

`ARCHITECT_REVIEW_PASS` — single iter-7 FAIL resolved (Toast Canvas `overrideSorting = 1`,
`sortingOrder = 950`, draws above the modal's 900); ShellScene diff purely additive, nothing
else regressed. Ready for Cesar's final approval.
