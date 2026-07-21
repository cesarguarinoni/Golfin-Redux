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
same commit**. Paths + GUIDs below verified against the repo on **2026-07-06** (shop-card atoms added
from the Order-610 card rebuild — see `Docs/Reports/POSTMORTEM_general_shop_ui_fabricated_provenance.md` Part 2).

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
| Corner mask 8px LEFT | `Assets/Art/Original UI/Common/S_Common_BGCorner8Left.png` | `48e7cea5350380b42bceb9a78035d7b7` | left-side-only rounding (e.g. rarity tile on a card's left edge) | shop card rarity tile |
| Corner mask 8px BOTTOM | `Assets/Art/Original UI/Common/S_Common_BGCorner8Bottom.png` | `555dbbd195fecb0459818bc9066e6621` | bottom-only rounding (lower half of two-tone boxes) | shop card price box |

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
- **Stat bar (continuous, clubs)** — added 2026-07-06, Order 610 card rebuild: `S_PillStadium.png`
  (`bb07d102…`, 176×176, border 88 = half) as BOTH track and fill, with a tuned
  `pixelsPerUnitMultiplier` (~13 for a 14px-tall bar) so the rounded caps stay full semicircles.
  Do **NOT** 9-slice `LevelUpBlueFill` for proportional-width fills — its border (8,3,8,3) is smaller
  than its ~10px cap radius, so the leading cap kinks to a point (PIPELINE_HARDENING C10). Verify by
  zooming the fill's **leading edge**, not the whole bar.
- **Two-tone price box (RP)** — added 2026-07-06, Order 610: upper tone `S_Common_BGCorner8` +
  lower tone `S_Common_BGCorner8Bottom`; RP coin icon (`Reward Points Icon.png`, NOT an "R" text
  prefix) tight to the number, **non-bold**, as a **centred group inside the box**. The Figma's
  real-money `$` strike-through block re-tokens to this (D2, general_shop_ui).
- **Rarity tile (card left edge)** — added 2026-07-06, Order 610: `Resources/Rarities/<Rarity>.png`
  gradient masked to rounded-left via `S_Common_BGCorner8Left`.

## Item art (Resources — path patterns, not single GUIDs)
| Art | Path pattern | Use for |
|---|---|---|
| Club portraits | `Assets/Resources/Clubs/Portraits/<Club>.png` | shop/inventory club card image (same art as Club Selection) |
| Rarity gradients | `Assets/Resources/Rarities/<Rarity>.png` | rarity tile backgrounds |

## Type-specific stat displays (balls ≠ clubs — added 2026-07-06, Order 610)
Clubs use a **continuous** fill bar (stat-bar composite above); balls use a **segmented bidirectional**
bar — `Golfin.Inventory.BallSegmentedBar` (`Assets/Scripts/UI/Inventory/BallSegmentedBar.cs`): 20
segments, centre divider, blue right / orange-red left / grey empty, value −10..+10. Ball stats are
Power/Rebound/WindCut/Roll/Spin, NOT the club set. **Before building any item-type stat UI, open the
real inventory/detail surface for that type** (`BagClubCard.prefab`, `BallDetailPanel.cs`) — never
assume one stat display fits all. NOTE: `BallSegmentedBar` builds its segments via a runtime
`HorizontalLayoutGroup` — it does NOT bake in edit mode (PIPELINE_HARDENING C11); static prefabs need
explicitly built segment children.

## Clone bases (whole-screen / card — per the Reuse mandate)
- **List screen** (scroll list of selection cards): `Assets/Prefabs/UI/Tournaments/TournamentSelectionScreen.prefab`
  (GUID `93756886e6c93413a815700517bd4b54`) + `TournamentSelectionScreenController.cs` — the mandated
  clone base for list-style screens (used by the stamina-shop Selection screen). Carries the real
  `TabBar` tab strip, `ScrollArea`/`ScrollRect`, `Scrollbar`+Handle, and `BG`. Register in
  `ScreenManager` like `ScreenId.TournamentSelection`.
- **Selection card:** `Assets/Prefabs/UI/Tournaments/TournamentSelectionCard.prefab`
  (GUID `baac145d1783f41758376281a61c83e0`) + `TournamentSelectionCard.cs` — clone base for list cards
  (source of the two-layer badge pattern above; already carries the gold `Play Button` CTA sprite +
  `ButtonPressFeedback`).
- **Segmented-filter list screen:** `Assets/Prefabs/UI/Shop/StaminaShopSelectionScreen.prefab`
  (GUID `ff5fc45710513468fab1149f4aeaa252`) — two segmented filter pills + scroll list + scrollbar.
  **SHIPPED Order-517 deliverable — clone from it, never edit it in place** (a task silently modified
  it +68 lines during the 610 fabrication incident; see the shipped-asset guard, Order 611).
- **Shop card:** `Assets/Prefabs/UI/Shop/StaminaShopCard.prefab` (GUID `717d118c7be214838ab65e0bd65731f2`).
- **Club inventory card (stat rows):** `Assets/Prefabs/UI/Inventory/BagClubCard.prefab`
  (GUID `5e39901a81c074c4aacbe5d27d1309fd`) — the real club stat-row display; source for club-card stat UI.

---

## Account / Auth atoms (login_signup_screens — Order 2026-07-21)

New atoms exported from Figma file `5gEAHjl6xAtW8iYY7NMvWd` and imported as sprites under `Assets/Art/UI/Account/`.

| Atom name | Asset path | GUID | Notes |
|---|---|---|---|
| Top-band banner (navy) | `Assets/Art/UI/Account/S_Login_TopBG_Navy.png` | `b23e2030b37249c8b75ed25d702cf7f8` | 9-sliced (L334 R47); notched gold-edged banner used on all 4 auth screens |
| Splash background (login) | `Assets/Art/UI/Account/S_Login_SplashBG.png` | — | Full-screen course/sky photo for auth-screen BG |
| Splash BG variant 2 | `Assets/Art/UI/Account/S_Login_TopBG2.png` | — | Alternative top-band graphic |
| Sign-up BG | `Assets/Art/UI/Account/S_SignUp_BG.png` | — | Background for sign-up screen |
| Sign-up BG variant 2 | `Assets/Art/UI/Account/S_SignUp_BG2.png` | — | Alternative sign-up background |
| Password-rule cross icon | `Assets/Art/UI/Account/ICO_RuleCross.png` | — | Red X for unmet password rules |
| Password-rule tick icon | `Assets/Art/UI/Account/ICO_RuleTick.png` | `66e339915cfd7491d830afe99ef11b7b` | Green checkmark for met password rules |
| Google social icon | `Assets/Art/Original UI/LoginScreen/S_Login_Google_Icon.png` | `bb94c73e3c83e5145b77f3d7ab423fde` | Google G logo for social-auth pill (Login + SignUp) |
| Apple social icon | `Assets/Art/Original UI/LoginScreen/S_Login_Apple_Icon.png` | `9cf6f483eef9f374989e51301871daec` | Apple logo for social-auth pill (Login + SignUp) |
| Password eye-show | `Assets/Art/Original UI/SettingsScreen/S_Settings_Icon_EyeOn.png` | `985195deea614f14ca3fe265203c529d` | Eye open icon; password contentType=Standard |
| Password eye-hide | `Assets/Art/Original UI/SettingsScreen/S_Settings_Icon_EyeOff.png` | `5b0184341b55e7e4b80b8f668b5c8757` | Eye closed icon; password contentType=Password (default) |
| Green GPS primary button | `Assets/Art/SplashScreen/Green Button.png` | `091a45d11621e7745b879424b7b278a5` | Green gradient pill sprite for primary action buttons |
| Text input field | `Assets/Art/Original UI/Common/S_Common_TextField_882.png` | `4f9a7fe719e942548a538f7891172652` | White rounded input field background (882px wide) |
| Social auth pill bg | **REUSED** `Assets/Art/Tournaments/S_PillStadium.png` | `bb07d102185aa4f1ca51da13de9eeac6` | White pill stadium sprite for Google/Apple social buttons |
| CANCEL silver button | **REUSED** `Assets/Art/RosterScreen/ButtonCancel.png` | `6021c639e9c124b44a06c8ccd977896f` | Existing silver gradient; reused on all auth screens |
| Divider / separator | **REUSED** `Assets/Art/HomeScreen/Divider.png` | `36b5ccd887…` | Horizontal rule between sections |
| Rubik SemiBold SDF | **REUSED** `Rubik-SemiBold SDF.asset` | `39fb7824…` | Primary heading font |
| Rubik Variable SDF | **REUSED** `Rubik-VariableFont_wght SDF.asset` | `0e84913c…` | Body / label font |

**Prefabs (4 screen prefabs):**
- `Assets/Prefabs/UI/Account/LoginScreen.prefab`
- `Assets/Prefabs/UI/Account/SignUpScreen.prefab`
- `Assets/Prefabs/UI/Account/CreateUsernameScreen.prefab`
- `Assets/Prefabs/UI/Account/EmailConfirmationScreen.prefab`

Each screen: white-pill social buttons (3px black border radius-90), green GPS primary button (gradient `#22B800→#20A80C→#179005` + inner `#B2FFA1` border), white input fields (radius 20), navy card (20px radius, `#133453→#091B33`), `S_Common_BGCorner20` sprite for CardBorder.

---

## Related pipeline pieces
- **Detection/enforcement:** `Assets/Editor/UIFidelity/UIFidelityLinter.cs` (render-health + node-spec),
  `Docs/Scripts/figma_diff.py` (pixel diff), **Rule 21** in `.claude/hooks/enforce_implementer_done.py`.
- **Memory:** `reference_ui_fidelity_linter`, `feedback_figma_unity_reuse_elements_not_clone_screens`.
- **Worked example:** `Docs/Specs/Completed/stamina_boost_shop/` — the menu-row rebuild (reused RP pill +
  gold button + two-layer badge at node-exact geometry, isolated RT A/B render) after three from-scratch strikes.
