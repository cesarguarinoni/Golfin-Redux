# ITER-10 execution plan — §6 fidelity on the iter-5 baseline (reuse, don't rebuild)

**Approved by Cesar 2026-06-04.** Binding for this iteration alongside SPEC §6. Baseline: both card prefabs were just **reset to iter-5 (`d6ae486b`)** by the architect (clean, imports without errors). Apply §6 onto these existing cards by **modifying their existing children**, NOT by rebuilding.

## Cesar's locked answers
1. **Border = SPRITE SWAP.** Active vs collapsed border is achieved by swapping the card-background **sprite** per state (white-border sprite when active/expanded; `#3E7CA8`-border sprite when collapsed). Do NOT add an Outline component. If the two bordered sprite variants don't already exist, create them as sprite assets (tinted/edited card-bg) and swap `Image.sprite` in `ModeCardController.SetState`. Keep the bg Image **`m_Type:1` Sliced**.
2. **PLAY = REUSE the existing button.** Use the existing `ActionButton` (full-screen `ModeCard`) / `PlayButton` (home `ModeHomeCard`). Reposition + size to 359×120. **Never replace it.** Keep its existing gold styling + `ButtonPressFeedback`.
3. **The "pill" (`Label` + `Image` GOs):** these exist in iter-5 but were **NOT visible** in the iter-5 captures. Cesar doesn't know what they are. → **Preserve whatever kept them invisible in iter-5; do NOT activate, move, resize, or reparent them.** Do NOT let any §6 change surface them. (If you can positively confirm one is an unused orphan, you may `gameobject-destroy` it — but default to leaving them exactly as iter-5 had them.)
4. **Full-screen back panel:** keep/fix the existing scene `CardsContainer` via MCP — "as long as it works." Don't rebuild it.

## HARD anti-mistake rules (iter-6 failure modes — do NOT repeat)
- **MCP Unity-API ONLY.** Prefab: `assets-prefab-open` → `gameobject-component-modify`/`object-modify`/`gameobject-component-add` → `assets-prefab-save` → `assets-prefab-close(save:true)`. Scene: `gameobject-*`/`object-modify` → `scene-save`. **NEVER** raw-`Edit`/`Write` a `.prefab`/`.unity`, never hand-write YAML/fileIDs, never make a `[MenuItem]` batch script. C# files may use `Edit`.
- **After every prefab/scene save:** `assets-refresh` + `console-get-logs(Error)`. On ANY "overflow internal type" / "Broken text PPtr" / "Problem detected while loading" / "Transform child can't be loaded" → STOP, set `IMPLEMENTER_BLOCKED` with the exact error. Do not push through.
- **Inventory-FIRST.** Before editing a card, list its existing children and fill the reuse-map. No edit begins until the map is filled.
- **MODIFY-IN-PLACE, never recreate.** `gameobject-create` of a child is BANNED if a cloned equivalent exists. The ONLY allowed new objects: a duplicate of an existing `'Divider '` for the 3rd separator, and (if needed) the two border-sprite assets. A new generic `Image`/`Label` GO = the exact failure signature → forbidden.
- **Preserve properties, don't replace objects.** Never swap an Image to restyle it; change the property on the existing object (keep Sliced, keep the gold button sprite/material).
- **Incremental.** One element/area at a time, save+refresh+console-check, then next. No big batches.

## Reuse-map (each §6 element → existing iter-5 child to modify)
| §6 element | Existing GO to modify (do NOT recreate) |
|---|---|
| Card background + state border | root `Image` → keep `m_Type:1` Sliced; swap sprite per state (white vs `#3E7CA8` border) |
| PLAY button | `ActionButton` / `PlayButton` → reposition + 359×120, keep gold visuals |
| ENTRY FEE / REWARDS label | existing `EntryFeeLabel` / `RewardsLabel` → weight 600, ~27.9px, centered cluster |
| coin + value | existing `RewardSlot1`/`Reward1Icon`/`Reward1Amount` (+ Exp variants) |
| separators (3 expanded / 1 collapsed) | existing `'Divider '`, `Divider (1)`, `Divider (2)`; duplicate ONE for 3rd-above-PLAY |
| title (gold active / silver collapsed) | existing `Title`/`TitleExp` → color only |
| chevron / locked | existing `ChevronCollapsed/Expanded`, `LockedOverlay`, `LockIcon*` |
| description | existing `DescriptionText` → 80px inset |
| `Label` + `Image` ("pill") | LEAVE invisible as iter-5 (answer 3) |

## Ordered fix list
0. **Inventory** both cards; fill the reuse-map; confirm the existing button + Sliced-capable bg are present. (no edits)
1. **Typography:** all text → Rubik SemiBold 600 at §6.2 sizes, on existing text GOs.
2. **Fee/reward layout:** centered cluster `[LABEL] gap32 [coin42 gap6 value]`, rows gap-24, by repositioning the existing slots+labels; keep ENTRY FEE/REWARDS/NO ENTRY FEE labels.
3. **PLAY + separators:** reposition the existing button BELOW the content with a separator above it (fixes the PLAY-overlaps-REWARDS defect); 3 separators expanded / 1 collapsed from existing Dividers (+1 dup); description 80px inset; widths 764/677/978.
4. **Borders:** sprite-swap active-white / collapsed-`#3E7CA8`; bg stays Sliced.
5. **Title colors** (gold/silver), **back panel** scene reconcile, **arrows hidden**, **per-card chevron** (home-center only).
6. **Capture all 4 states** (home collapsed/expanded, full-screen one-expanded/all-collapsed) at clean **1170×2532** (verify each PNG is exactly 1170×2532, NOT 2070×1912 editor chrome). HONESTLY verify each §6 item against the capture — read the pixels, don't rubber-stamp; the PLAY must not overlap REWARDS or the banner.

## Guards
- Two distinct prefabs; source GUIDs in report. Do NOT touch singleton/manager files. ButtonPressFeedback on every Button.
- Controllers (`ModeCardController.cs` etc.) are at the iter-9 code state; adjust them (C# `Edit` ok) to match the reset prefabs + sprite-swap border + re-wire SerializeFields via MCP. Verify no null-ref at runtime.
- Report every uncommitted path outside the task folder (Rule 13). Restore playable state.
