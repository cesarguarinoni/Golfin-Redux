# UI Element Palette — reusable atoms for Figma→Unity builds

**Purpose.** A living catalog of the reusable UI atoms that already exist in the project, with
exact asset paths + GUIDs. When a Figma node has **no whole-screen clone source** (Rule 19), the
implementer reuses these individual atoms instead of fabricating flat-fill `Image` boxes. This is
**Fix #1** of the Figma→Unity element-reuse pipeline (`Docs/Specs/.../figma_unity_reuse_pipeline`);
Fixes #2–#4 (`UIFidelityLinter`, Rule 21 hard-gate, agent wiring) *enforce* the reuse this doc *enables*.

**How to use (implementer).** For every element in a Figma node, the **Element Reuse Map** must map
it to a row below, OR justify "pulled from Figma" only if the atom is genuinely absent here. Build at
node-exact geometry (1:1 Figma px on the shell canvas). Never ship a null-sprite `Image` where the node
shows a sprite/border/gradient — **Rule 21 hard-fails it**.

**How to maintain.** When a task discovers or creates a new reusable atom, add its row here **in the
same commit**. Paths + GUIDs below verified against the repo on **2026-07-03**.

---

## Panels & backgrounds
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| Navy card panel | `Assets/Art/HoleSelectScreen/Background - Next Hole.png` | `d162244f2dd5e8646afef2518d902a8e` | dark rounded card / list-item / panel background | Hole Select cards, stamina-shop cards + menu rows |

## Pills & containers
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| RP value pill (navy) | `Assets/Art/RankingsScreen/RPContainer.png` | `9106f5ea13a81ca4c8dc7b2671c853bf` | dark rounded RP-cost/amount container (coin + number) | Rankings, stamina-shop RP chips |
| Stadium pill (badge base) | `Assets/Art/Tournaments/S_PillStadium.png` | `bb07d102185aa4f1ca51da13de9eeac6` | outer rim of tier / entry-fee / status badges | Tournaments `PaidEntryBadge`, shop tier badges |

## Buttons
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| Gold button | `Assets/Art/HomeScreen/Play Button.png` | `cff37a7f9ed6d134696ab92626c9a747` | primary / confirm / BUY action | Home PLAY, shop BUY |
| Silver button | `Assets/Art/RosterScreen/ButtonCancel.png` | `6021c639e9c124b44a06c8ccd977896f` | secondary / cancel / back | Roster cancel, shop CANCEL |
| Silver button (alt) | `Assets/Art/ResultScreen/Button - Replay.png` | `d7b1c62bfcb4e844ab498b958b38aede` | secondary action (replay-style) | Result screen |

## Icons
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| RP coin icon | `Assets/Art/HomeScreen/Reward Points Icon.png` | `aab2dfa34afd9cf4abfe974a164268dc` | RP currency coin (pairs with RPContainer) | RP chips everywhere |
| Stamina icon | `Assets/Art/RosterScreen/IconStaminaSmall.png` | `e9df8622e360a894abb5d5b361930161` | stamina / energy glyph | Roster, shop `+STA` values |

## Dividers
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| Divider (horizontal) | `Assets/Art/HomeScreen/Divider.png` | `36b5ccd887d78864b9d3f0b36a18f339` | thin horizontal separator | Home, cards |
| Divider (vertical) | `Assets/Art/ClubsInventory/DividerVertical.png` | `c9234f1f0e5cd6f48bd406ff0995d2cf` | thin vertical separator (3-col info card) | Clubs inventory, shop detail info card |

## Rounding masks
| Atom | Path | GUID | Use for | Seen in |
|---|---|---|---|---|
| Corner mask 20px | `Assets/Art/Original UI/Common/S_Common_BGCorner20.png` | `dd96a2f1280ec46459c4e10fbaf32c92` | rounded-corner mask, 20px radius | rounded panels / cards |
| Corner mask 8px | `Assets/Art/Original UI/Common/S_Common_BGCorner8.png` | `b2ae6196bf901b54eaf57aea53472a8c` | rounded-corner mask, 8px radius | smaller rounded elements |

## Fonts (TMP SDF)
| Atom | Path | GUID | Use for |
|---|---|---|---|
| Rubik-SemiBold SDF | `Assets/Fonts/Rubik-SemiBold SDF.asset` | `39fb7824ee463ab408c7f2e76c362562` | headings / titles / emphasis (Latin) |
| Rubik-VariableFont_wght SDF | `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` | `0e84913c86a5b7f4881cb73d5e80728f` | body / regular (Latin) |
| NotoSansJP-VariableFont_wght SDF | `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` | `8f62f163976fae841ad23d559ebdf279` | Japanese + mixed JP/Latin |
| Rubik-Italic SDF | `Assets/Fonts/Rubik-Italic-VariableFont_wght SDF.asset` | `db4138b73e3ac5b47b93d242d972e386` | italic Latin (rare) |

> **Font sizing (Lesson AK):** geometry is 1:1 with Figma px on the shell canvas, but ALWAYS verify
> rendered size against the node render — the Figma→TMP divisor is **per-task** (tournament buttons
> were ÷1.3, default ÷1.4). Don't assume.

## Composite patterns
- **Two-layer badge** (tier / entry-fee / status): outer `S_PillStadium` tinted to the rim color +
  **inner dark rounded fill** + gradient TMP text. Canonical example →
  `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab` › `PaidEntryBadge`. Reused for the
  shop's HIGH/MEDIUM/LIGHT tier badges. **NOTE:** there is no standalone `PillFill.png` — the inner
  fill is a dark-tinted `Image` on the prefab node, not a separate asset (corrects the original seed list).

## Clone bases (whole-screen / card — per the Reuse mandate)
- **List screen** (scroll list of selection cards): `Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab`
  + `TournamentSelectionScreenController.cs` — the mandated clone base for list-style screens
  (used by the stamina-shop Selection screen). Register in `ScreenManager` like `ScreenId.TournamentSelection`.
- **Selection card:** `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab` + `TournamentSelectionCard.cs`
  — clone base for list cards (and the source of the two-layer badge pattern above).

---

## Related pipeline pieces
- **Detection/enforcement:** `Assets/Editor/UIFidelity/UIFidelityLinter.cs` (render-health + node-spec),
  `Docs/Scripts/figma_diff.py` (pixel diff), **Rule 21** in `.claude/hooks/enforce_implementer_done.py`.
- **Memory:** `reference_ui_fidelity_linter`, `feedback_figma_unity_reuse_elements_not_clone_screens`.
- **Worked example:** `Docs/Specs/Completed/stamina_boost_shop/` — the menu-row rebuild (reused RP pill +
  gold button + two-layer badge at node-exact geometry, isolated RT A/B render) after three from-scratch strikes.
