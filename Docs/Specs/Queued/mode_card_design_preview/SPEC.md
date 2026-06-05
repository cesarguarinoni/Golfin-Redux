# SPEC — mode_card_design_preview

**Type:** Full pipeline (editor tooling, careful non-persistence requirement)
**Effort:** ~half a day
**Restore point:** `restore/mode_select_working_2026-06-05` (tag) + `Docs/Backups/mode_select_working_2026-06-05/`
**Surfaces:** `ModeCard.prefab` (full-screen) + `ModeHomeCard.prefab` (home carousel)

---

## ✅ IMPLEMENTED 2026-06-05 — design DIVERGED from the snapshot approach below

Shipped as `Assets/Scripts/UI/ModeSelect/Editor/ModeCardPreview.cs`. **The in-Prefab-stage
snapshot/restore design described further down was abandoned** — it leaks. Applying a state in
the Prefab stage runs the layout groups, which **bake driven RectTransform sizes** into the
asset (plus `_showChevron` and a `CanvasGroup` that `RefreshFeeColor` adds to PLAY); a property
snapshot can't reliably revert all of that, so a save could persist preview junk. Verified the
leak by md5 (a capture→apply→restore→save cycle changed the prefab in 48/30 lines).

**Final design — non-persistence BY CONSTRUCTION:** the prefab asset is only ever *read*. The
preview instantiates a **linked prefab instance** into a throwaway additive scene
(`__ModeCardPreview`), binds sample data + applies the chosen state to THAT instance, and frames
it in the Scene view (World-Space canvas). Editing the real prefab in the Prefab stage updates
the live instance. `Clear Preview` closes the scene. The prefab file is never written.

Menu: `GOLFIN/Mode Cards/Preview/{Home — Collapsed+PLAY / Home — Expanded / Home — Side /
Home — Locked / Full-screen — Collapsed / Full-screen — Expanded / Full-screen — Locked}` and
`GOLFIN/Mode Cards/Clear Preview`. Sample data = "PREVIEW MODE" + a multi-line description, fee
100 / rewards 200. State applied via the real `Bind/SetState/SetCenter/SetShowChevron` API
(first-show guard reset so it's instant — coroutines don't tick in edit mode).

**Verified:** preview builds a correct linked instance ("PREVIEW MODE", Expanded,
`IsPartOfPrefabInstance=true`); **both prefab files are byte-identical (md5) before and after
previewing + clearing** — the hard non-persistence gate. (The Scene-view frame works
interactively; the MCP scene-view screenshot grabs a different view so it couldn't be
auto-captured — Cesar to eyeball the framing in-editor.)

Everything below is the ORIGINAL spec, kept for context.

---

## Problem

The cards are real prefabs, but their **final appearance is finalized at runtime**
by the controllers — `Bind(data)` fills text, `SetState(state)` toggles the
collapsed/expanded containers + separators + lock, `RefreshCenterVisuals()` sets
PLAY/chevron/border/title, and (home) `ModeCarouselController` sets the card's
width/height/anchors/pivot. So when Cesar opens `ModeCard`/`ModeHomeCard` in the
**Prefab stage**, it shows a **non-representative default**: placeholder/empty text,
an ambiguous container active-state (collapsed + expanded may both be on), no
carousel sizing. He can't WYSIWYG-edit "the expanded card" or "the locked card."

## Goal

Let Cesar open either prefab and, with one click, render it as a **representative,
correctly-laid-out card in a chosen state** (with sample data), edit any aspect
visually, then reset — **without ever baking the preview into the prefab asset.**

## Approach (preferred: in-stage, non-persistent)

A new **editor-only** helper (no runtime/gameplay code, no asmdef changes that pull
editor types into player builds — put it under an `Editor/` folder).

Provide a small toolbar/menu (e.g. `ModeCardController` component context-menu items,
or a tiny `EditorWindow`, or `[MenuItem("GOLFIN/Mode Cards/Preview …")]`) with presets:

- **Home — Collapsed + PLAY (center)**  → `_showChevron=true`, state `Collapsed`,
  `SetCenter(true)`, size 556×424
- **Home — Expanded (center)**          → state `Expanded`, `SetCenter(true)`, size 764×630
- **Home — Side (no PLAY)**             → state `CollapsedNoPlay`, `SetCenter(false)`, size 556×~280
- **Home — Locked**                     → state `Locked`, `SetCenter(false/true)`, dim + lock
- **Full-screen — Collapsed**          → `_showChevron=false`, state `Collapsed`
- **Full-screen — Expanded**           → state `Expanded`
- **Full-screen — Locked**             → state `Locked`
- **Reset preview**                     → return to a clean authoring state

Each preset must, on the prefab-stage root:
1. Bind **sample data** — a hardcoded `ModeData` (title `PREVIEW MODE`, tagline,
   a 2–3 line description, fee `100`, rewards `200`) OR the first row of
   `Assets/Resources/Data/modes.csv`.
2. Apply the state via the existing `Bind`/`SetState`/`SetCenter`/`SetShowChevron`
   API (reuse the real code — don't reimplement layout).
3. For home, set the root size (the carousel's job at runtime).
4. Force a layout rebuild so it renders immediately in the stage.

## HARD requirement — preview must NOT persist into the asset

This is the main failure mode. The preview applies sample text / active-state /
sizes; if those get saved into the `.prefab`, the runtime cards break (placeholder
text, wrong default state). The implementation MUST guarantee non-persistence by
one of:

- Operating on a **throwaway instance** in a temporary preview scene (instantiate,
  preview, discard) — safest; OR
- Applying in the open Prefab stage but providing **"Reset preview"** that restores
  the clean authoring state, AND hooking `PrefabStage.prefabSaving` (or equivalent)
  to auto-reset before save so a stray save can't bake preview data.

**Acceptance gate:** after Preview → Reset → Save, `git diff` of the prefab is
**empty** (no preview text/state/size leakage). This must be tested explicitly.

## Acceptance criteria

- [ ] Opening `ModeHomeCard` + running "Home — Expanded" renders a gold-title
      expanded card with the sample description + PLAY, correctly sized/laid out.
- [ ] Each preset above renders its state representatively (verify locked = dim +
      lock left-of-title; side = no PLAY; full-screen expanded = description + PLAY).
- [ ] "Reset preview" returns the prefab to a clean authoring state.
- [ ] **Non-persistence:** Preview → Reset → Save → `git diff` on the prefab is empty.
      Also: Preview → Save-without-reset must NOT corrupt the asset (auto-reset hook),
      re-verified with an empty/near-empty diff and a clean play-mode run afterwards.
- [ ] No runtime behaviour change; the helper is editor-only and never executes in
      a player build (guarded by `#if UNITY_EDITOR` / `Editor/` asmdef).
- [ ] `script-execute` compile check passes; no new console errors; a normal
      play-mode run of Home + ModeSelection still matches the approved screenshots.

## Out of scope

- Changing the runtime layout/animation or the carousel sizing.
- Persisting any "default preview" into the shipping prefab.

## Notes for the implementer

- Reuse the public-ish entry points already on `ModeCardController`
  (`Bind`, `SetState`, `SetCenter`, `SetShowChevron`, `SetHeights`) — they already
  do all the layout/visibility/colour work; the helper only drives them with sample
  data + a state, in the editor.
- Sample sizes from the working build: home collapsed `556×424`, expanded `764×630`,
  side `556×~280`; full-screen heights are ContentSizeFitter-driven.
- If you add an asmdef for the editor helper, scope it `Editor`-only platform so it
  doesn't leak into the player build (Lesson W — asmdef build order).
