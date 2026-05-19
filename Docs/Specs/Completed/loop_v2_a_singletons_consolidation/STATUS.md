# STATUS — loop_v2_a_singletons_consolidation

**Status:** DONE (Cesar approved 2026-05-19)
**Type:** TELLCODE
**Pipeline routing:** Architect writes SPEC → Code implements → Cesar visual verify → close.
**Parent:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` (Stage A)
**Notion:** Loop v2 Order 300

## History
- 2026-05-19 — Architect SPEC.md written. Two parts: bottom nav consolidation + Settings consolidation.
- 2026-05-19 — Code: Part 1 + Part 2 landed clean (294/294 EditMode tests). Handoff to Cesar.
- 2026-05-19 — Cesar v1 visual: bottom-nav highlight gone; Settings only opens after manually activating `SettingsScreen`. Two regressions surfaced by the consolidation.
- 2026-05-19 — Code root-cause + fix: (a) `PersistentUIManager.*Highlight` Image refs were never wired in scene (vestigial dead path); old HomeScreenController was the only thing color-tinting the icons. (b) `SettingsScreen` root saved inactive → Awake never fired → no Instance; Phase 1 fallback used to mask it. Refactored PUM to icon-color-tint + added `HighlightScreen(ScreenId)` driven from `ScreenManager.ApplyScreen`. Wired the 5 icon Images in scene. Activated `SettingsScreen` root with inner panels deactivated.
- 2026-05-19 — Cesar v2 visual: "Done". Closed.
