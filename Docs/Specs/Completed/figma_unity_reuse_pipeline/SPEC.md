# Pipeline hardening — Figma→Unity element-reuse loop

**Status:** DONE — 2026-07-03 (all four durable fixes shipped; moving to Completed). Origin: QUEUED (Cesar directive 2026-07-02 — build AFTER `stamina_boost_shop` ships, using that task as the written-up case study)

## Problem (root cause, from stamina_boost_shop)
The pipeline gates *"did you clone the mandated screen?"* (Rule 19) but has **no step and no gate** for *"when there's no whole-screen clone, reuse the individual elements that already exist, and match the exact Figma node."* Every piece with no 1:1 clone source (menu row, detail inner content) fell through that hole: the implementer fabricated flat-fill `Image` boxes, guessed fonts/sizes, and never searched the app for the real reusable element (RP pill lived in Rankings; tier/entry-fee badge in Tournaments). Three strikes, same hole. Cesar caught each on sight.

Secondary: the reusable palette was rediscovered from scratch (~15 tool calls) each task, and `CaptureHelper.SnapGameView()` returned stale game-view frames, so there was no reliable isolated A/B render.

## The four durable fixes
1. **UI Element Palette catalog** — `Docs/Architecture/UI_ELEMENT_PALETTE.md`. Living dictionary of reusable atoms with exact paths + GUIDs. Seed it from the stamina_boost_shop palette:
   - Navy card panel: `Assets/Art/HoleSelectScreen/Background - Next Hole.png`
   - RP navy pill: `Assets/Art/RankingsScreen/RPContainer.png`
   - Stadium pill (badges): `Assets/Art/Tournaments/S_PillStadium.png` (bb07d102185aa4f1ca51da13de9eeac6)
   - Two-layer tier/entry-fee badge pattern: outer `S_PillStadium` (rim color) + inner `PillFill` (dark) + gradient TMP text — see `TournamentSelectionCard` PaidEntryBadge
   - Gold button: `Assets/Art/HomeScreen/Play Button.png`
   - Silver button: `Assets/Art/RosterScreen/ButtonCancel.png` / `ResultScreen/Button - Replay.png`
   - RP coin icon: `Assets/Art/HomeScreen/Reward Points Icon.png` (aab2dfa34afd9cf4abfe974a164268dc)
   - Stamina icon: `Assets/Art/RosterScreen/IconStaminaSmall.png`
   - Dividers: `Assets/Art/HomeScreen/Divider.png` (h), `Assets/Art/ClubsInventory/DividerVertical.png` (v)
   - Rounding masks: `Assets/Art/Original UI/Common/S_Common_BGCorner20.png` / `BGCorner8.png`
   - Fonts (SDF): `Rubik-SemiBold SDF`, `NotoSansJP-VariableFont_wght SDF`, `Rubik-VariableFont_wght SDF`; shell-canvas font convention (geometry 1:1 Figma px, verify size vs node render).
2. **Enforced Figma→Unity build loop** in `CLAUDE.md` + `golfin-implementer.md`: pull node (`get_design_context`) → **Element Reuse Map** (every element → palette entry OR "pulled from Figma", only if genuinely absent) → build at node-exact geometry → **isolated A/B render vs node** → self-check (every `Image.sprite` non-null unless justified; font weight + rendered size vs node render).
3. **Hook gate** (`enforce_implementer_done.py`): for Figma-node tasks, require the Element Reuse Map, and **HARD-FAIL any flat-fill `Image` (null sprite) where the node shows a sprite/border/gradient** (Cesar 2026-07-02: hard-fail, not warn). This would have auto-failed all three bad iterations in seconds.
4. **Sanctioned isolated-UI-prefab render tool** — editor menu/util that does the WorldSpace-canvas + dedicated-camera + RenderTexture trick (used in stamina_boost_shop to A/B the menu row) so any UI prefab renders clean against Figma without the stale-`SnapGameView` problem. Extends `CaptureCore`.

## Decisions locked
- Build AFTER the shop ships (Cesar). **Superseded 2026-07-02:** Cesar directed the detection tooling be built + wired mid-shop ("Add it to full pipeline pass before we continue").
- Gate strictness: **hard-fail flat-fills** (Cesar).

## SHIPPED 2026-07-02 (detection layer)
- ✅ **Fix #4 (render tool) + detection linters** — `Assets/Editor/UIFidelity/UIFidelityLinter.cs` (render-health + node-spec) and `Docs/Scripts/figma_diff.py` (pixel-diff). Isolated UI render = WorldSpace-canvas + dedicated camera + RenderTexture (SnapGameView returns stale edit-mode frames).
- ✅ **Fix #3 (hard gate)** — **Rule 21** in `enforce_implementer_done.py` (`validate_ui_lint`): Figma-node tasks must carry a `## UI fidelity lint` section citing `_lint.json`(s) with `fail == 0`; missing/failing = hard block. Functionally tested (no-section / no-ref / missing-file / fail>0 all block; fail==0 passes).
- ✅ **Fix #2 (loop wired into agents)** — `golfin-implementer` step 6e (run linter, cite JSON), `golfin-reviewer` step 2d + `golfin-redteam-reviewer` §12 (RE-RUN, never trust), IMPLEMENTER_REPORT template `## UI fidelity lint` section, CLAUDE.md Rule 21.
- ✅ **Fix #1 (Element Palette catalog `UI_ELEMENT_PALETTE.md`)** — DONE 2026-07-03 — `Docs/Architecture/UI_ELEMENT_PALETTE.md` written (12 sprites + 4 fonts + badge pattern + clone bases; paths + GUIDs verified vs repo). Consumer WIRED: **Rule 22** (proactive Element Reuse Map) in `golfin-implementer.md` + pointer in CLAUDE.md Rule 21.
- 📄 **Node-spec auto-parse (SPECCED — follow-up order)** — spec JSONs are currently hand-authored from `get_design_context`; a `get_design_context → spec.json` generator — now SPEC_READY as its own order `figma_node_spec_generator` (`Docs/Specs/Queued/figma_node_spec_generator/`).

## Case-study reference
`Docs/Specs/Active|Completed/stamina_boost_shop/` — the menu-row rebuild (iter after the 3 from-scratch strikes) is the worked example: reused RP pill + gold button + two-layer badge, real Figma photo, node-exact geometry, isolated RT A/B render (`Docs/Diagnostics/_capture/menurow_v2.png`).
