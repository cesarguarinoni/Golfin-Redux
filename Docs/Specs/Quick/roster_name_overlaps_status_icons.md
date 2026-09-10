# Quick spec — `roster_name_overlaps_status_icons`

**Filed:** 2026-09-10, from Cesar reading the `asset_loans_offers` canonical frame: *"shows the Level
up arrow over the character name (name too long). This is not your defect but fix it anyway by using
autosize on the name when stepping over the icons."*
**Not an `asset_loans_offers` defect** — it predates that task and shows on any roster view of a
long-named character with a level-up available. Filed separately so attribution stays clean.

## Problem

`Canvas/ScreensRoot/RosterScreen/DetailPanel/RightPanel/CharacterNamePanel/CharacterNameText` is
489 px wide with `enableWordWrapping = false`, `overflowMode = Overflow` and a fixed `fontSize = 45`.
The status icons are **not beside it** — `RightPanel/StatusIconsRow` is anchored to the panel's
top-right and its leftmost edge lands **418 px into the label's own rect**. Measured:

| | px |
|---|---|
| label rect width | 489 |
| headroom before `IconLevelUpBig` | 425 |
| headroom before `IconSelectedBig` | 418 |
| `"CHRISTOFFERSON"` at fontSize 45 | **445** |

So the level-up arrow drew over the final `N` — **30 px of overlap** with the level-up icon alone,
37 px with the selected icon. Nothing is wrong with the icon; the label had no idea it was there.

## Shape audit (PIPELINE_HARDENING rule 15)

Every name-label-under-status-icons site, enumerated rather than sampled — **including the ones that
were fine**:

| Site | fontSize | auto | Verdict |
|---|---|---|---|
| `RosterScreen/DetailPanel/RightPanel` (row under `RightPanel`) | 45 | false | **DEFECT — the reported one**, overflows by 30 px |
| `…/CompareRightPanel/CompareInfoPanel/CharacterNamePanel` (row under the name panel) | 45 | false | **Same defect, latent** — overflows `IconSelectedBig` by 3 px; clears `IconLevelUpBig` by 5. Not a margin worth trusting |
| `MatchMakingModal` ×2 `CharacterThumbnailCardGlowUp/NameLabel` | 25 | **true** | Fine — already auto-sizes |
| `VersusResultModal` ×2, same | 25 | **true** | Fine — already auto-sizes |
| `RightPanel/StatsPanel/…/StatsName` ×4 | 33 | true | Not this shape; matched only on the substring "Name" |
| `LevelUpModal/HeaderSection/CharacterNameText` | 45 | false | No icons row — not this shape |

⚠️ **A first sweep of this shape missed the reported site.** It scoped candidate icons to the text's
*immediate parent*, which finds the Compare copy (row under `CharacterNamePanel`) but not the detail
panel (row under `RightPanel`, one level up). The two instances of the same visual shape are
parented differently. Scope by **ancestor**, not by parent.

## Fix

`Assets/Scripts/UI/Roster/UI/NameIconFitter.cs` (new) — turns on TMP auto-sizing with the design
size as `fontSizeMax`, and reserves the icon strip in `margin.right`, measured from the icons' live
world corners. Called from `CharacterDetailPanel.UpdateDisplay` and
`CompareController` **after** the icons are toggled.

- **margin, not the rect** — the rect, anchors and alignment are untouched, so nothing that lays the
  panel out is disturbed, and a short name still renders at the full 45.
- **only active icons reserve** — a character with no badges gets the full 489 back.
- **measured, never hardcoded** — moving `StatusIconsRow` in the scene needs no code change.
- **code only, no scene edit** — the serialized `enableAutoSizing = false` is left alone and the
  runtime turns it on, so this adds nothing to `ShellScene.unity`'s diff.

### The bug inside the fix, and why the first version looked right

The first version measured the icon rect **immediately after `SetActive`**, before the row's layout
group had rebuilt. With `IconSelectedBig` switched off, the surviving level-up icon still reported
its two-icon position (1074 instead of 1036), the reserve came out **36 instead of 74**, and the
label auto-fitted itself neatly into a gap that no longer existed — the arrow still sat on the last
glyph while every number in the component said it fit. `Fit` now calls
`LayoutRebuilder.ForceRebuildLayoutImmediate` on each icon row first, scoped to those rows rather
than `Canvas.ForceUpdateCanvases()`, which would rebuild every canvas and risk baking anchor churn
into the scene on a later save.

## Verification (real navigation, play mode — no harness)

Boot → Home → real `NavCharactersButton.onClick` → roster → select `char_johan`:

| | before | after |
|---|---|---|
| `margin.right` | 0 | **73.5** |
| `fontSize` | 45 | **42** (auto) |
| glyph right edge (world x) | 1056 | **1026** |
| `IconLevelUpBig` left edge | 1036 | 1036 |
| gap | **−20 px (overlap)** | **+10.0 px clear** |

Short-name regression, `char_james` / "JAMES CARTWRIGHT", both icons active: `fontSize` **45**
(full design size, not shrunk), `margin.right` 90, gap 83.5 px. The fix does not make every name
smaller — only the ones that would collide.

Frames: `Docs/Diagnostics/_capture/screenshot_2026-09-10_14-26-16.png` and the before/after crop
`Docs/Diagnostics/_capture/roster_name_vs_levelup_icon_before_after.png`.

## Attribution warning for the `asset_loans_offers` close-out

`CharacterDetailPanel.cs` now carries **two tasks'** uncommitted changes — `asset_loans_offers`
(OFFERED as a narrowing of lent, RESCIND before LEND) and this fix (two `nameFontSize*` fields plus
the `NameIconFitter.Fit` call). They are contiguous and separable, but the close-out commit for
`asset_loans_offers` must **hunk-split** this file rather than staging it whole. Same hazard as
`project_k10_commit_swept_k11_edits`. `CompareController.cs`, `NameIconFitter.cs` and its `.meta`
belong to this task alone.
