# DESIGN_TOKENS.md — the Figma variables the game UI is measured against

**Status: SEED (Architect, 2026-09-03).** Read with `get_variable_defs` on Home `2098:8490` and
Rankings `4079:1726` in file `5gEAHjl6xAtW8iYY7NMvWd`. `design_consistency_audit` Phase 0.1
completes it (every `EN/*`, `JP/*`, colour and rarity variable; EMPTY values resolved from the SVG
gradient stops). Until then treat every row below as verified and every gap as unknown — never guess.

Canvas: **1170×2532 at scale 1 — a Figma px is a Unity px** (`FIGMA_SCREEN_BUILD_PLAYBOOK.md` §2).
Rubik SemiBold runs render 10–12 % oversize at nominal px; the project authors them at
`node_px × 59/66` (playbook §4). Whether the game screens keep that convention is a
`design_consistency_audit` recommendation (shape iii).

## Typography — EN (Rubik)

| Variable | Style | Size | Line height | Tracking | Source node |
|---|---|---|---|---|---|
| `EN/Caption_3 (en)` | Medium 500 | 30 | 36 | −0.5 | 4079:1726 |
| `EN/Caption_2_Medium (en)` | Medium 500 | 33 | 39 | +0.18 | 4079:1726 |
| `EN/Footnote (en)` | SemiBold 600 | 39 | 54 | −0.24 | 2098:8490 |
| `EN/Subhead (en)` | SemiBold 600 | 45 | 60 | −0.69 | 2098:8490 |
| `EN/Headline (en)` | SemiBold 600 | 51 | 66 | −1.29 | 2098:8490 |
| `EN/Title_2 (en)` | SemiBold 600 | 66 | 84 | −0.78 | 2098:8490 |
| `EN/…` (Title_1, Large Title, Caption_1, Body, Callout …) | — | — | — | — | **unresolved — Phase 0.1** |

Unity font assets: SemiBold → `Assets/Fonts/Rubik-SemiBold SDF.asset` (`39fb7824ee463ab408c7f2e76c362562`);
Medium / Regular → `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (`0e84913c86a5b7f4881cb73d5e80728f`)
(the variable face renders Medium ~5 % narrow — `POLISH_BACKLOG.md`, Rubik Medium import).
`LiberationSans SDF` (`8f586378b4e144a9851e7b34d9b748ee`) is Unity's default and is **never** a design token.

## Typography — JP (Noto Sans JP)

| Variable | Style | Size | Line height | Tracking | Source node |
|---|---|---|---|---|---|
| `JP/Footnote (jp)` | Display SemiBold 600 | 39 | 54 | −0.24 | 4079:1726 |
| `JP/…` | — | — | — | — | **unresolved — Phase 0.1** |

Unity: `Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` (`8f62f163976fae841ad23d559ebdf279`), swapped by `LocalizedText` at runtime (audit A3 confirms per site).

## Colours

| Variable | Value | Source node |
|---|---|---|
| `Main (Game)/White`, `White`, `Text Colors/Text White` | `#FFFFFF` | 2098:8490 |
| `Main (Game)/Game_Dark_Blue` | `#001E39` | 4079:1726 |
| `Main (Game)/Red` | `#FF0000` | 4079:1726 |
| `Text Colors/Mission Font` | `#EEDC9A` | 2098:8490 |
| `Greys/Grey_30%` | `#B2B2B2` | 2098:8490 |
| `Rarity Fonts/Mythic` | `#FFC107` | 4079:1726 |
| `Rarity Fonts/Supreme` | `#7851A9` | 4079:1726 |
| `Rarity Fonts/Common|Uncommon|Rare|Legendary` | **unresolved** | — |
| `Gold`, `Silver`, `Blue`, `Copper` | **EMPTY from the API — gradients; read the SVG stops** (known: the `Pop-Up` / `Mission Card Container` gold stroke is `#FCF195 → #D6AB42 @0.6 → #BB7F1D`, `UI_ELEMENT_PALETTE.md`) | — |
| `Rarity Backgrounds/*` | **EMPTY from the API — gradients; read the SVG stops** | — |

## Shared atom geometry (from the build playbook / palette — confirm per node in Phase 0.1)

| Atom | Value | Source |
|---|---|---|
| Card family fill | `rgba(19,52,83,.6) → rgba(9,27,51,.6)` gradient, **3 px white border**, r50 big cards / r32 small tiles | playbook §3 |
| `Pop-up` panel | `#133453 → #091B33`, silver edge, shadow baked in the sprite margin (L20 R20 T10 B30 sprite-px) | palette "Pop-up panel" |
| `Main Buttons` | height 120 (Home `4192:31376`), 54 (Clubs detail `12772:3285`) — per-context sizes, list all in Phase 0.1 | Home page / Clubs page metadata |
