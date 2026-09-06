# DESIGN_TOKENS.md — the Figma variables the game UI is measured against

**Status: COMPLETED by `design_consistency_audit` Phase 0.1 (2026-09-06).** Seeded by the Architect
2026-09-03. Every row below was read with `get_variable_defs` or extracted from a node SVG — file
`5gEAHjl6xAtW8iYY7NMvWd`. **Nothing here is guessed**; a variable that could not be read is listed
under § Unresolved with the reason and the method to resolve it.

Canvas: **1170×2532 at scale 1 — a Figma px is a Unity px** (`FIGMA_SCREEN_BUILD_PLAYBOOK.md` §2).
Rubik SemiBold runs render 10–12 % oversize at nominal px; the project authors them at
`node_px × 59/66` (playbook §4). Whether the game keeps that convention is audit shape (iii).

## Typography — EN (Rubik)

Read from `13994:1935`, `4065:14998`, `4065:9071`, `4065:16939`, `4079:1726`.

| Variable | Style | Size | Line height | Tracking | Read from |
|---|---|---|---|---|---|
| `EN/Caption_4 (en)` | SemiBold 600 | 20 | 24 | −1.5 | 4065:14998 |
| `EN/Caption_3 (en)` | Medium 500 | 30 | 36 | −0.5 | 4065:14998 |
| `EN/Caption_2 (en)` | Regular 400 | 33 | 39 | +0.18 | 4065:14998 |
| `EN/Caption_2_Medium (en)` | Medium 500 | 33 | 39 | +0.18 | 4065:14998 |
| `EN/Footnote (en)` | SemiBold 600 | 39 | 54 | −0.24 | 13994:1935 |
| `EN/Subhead (en)` | SemiBold 600 | 45 | 60 | −0.69 | 13994:1935 |
| `EN/Callout (en)` | SemiBold 600 | 48 | 63 | −0.93 | 4065:14998 |
| `EN/Headline (en)` | SemiBold 600 | 51 | 66 | −1.29 | 13994:1935 |
| `EN/Title_2 (en)` | SemiBold 600 | 66 | 84 | −0.78 | 13994:1935 |

**The type scale in use is nine styles over EIGHT distinct sizes: 20 · 30 · 33 · 39 · 45 · 48 · 51 · 66.** (`EN/Caption_2` and `EN/Caption_2_Medium` are both 33 px and differ by weight, not size — nine variables, eight sizes.)
`Title_1`, `Large Title`, `Body` and `Caption_1` were named in the seed as expected members but are
**used by no in-scope frame** — see § Unresolved. That absence is itself an audit input: a rendered
size that matches none of the nine steps above cannot be excused as "some other scale step".

Unity font assets: SemiBold → `Assets/Fonts/Rubik-SemiBold SDF.asset` (`39fb7824ee463ab408c7f2e76c362562`);
Medium / Regular → `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (`0e84913c86a5b7f4881cb73d5e80728f`).
`LiberationSans SDF` (`8f586378b4e144a9851e7b34d9b748ee`) is Unity's default and is **never** a token.

## Typography — JP (Noto Sans JP)

| Variable | Style | Size | Line height | Tracking | Read from |
|---|---|---|---|---|---|
| `JP/Footnote (jp)` | Display SemiBold 600 | 39 | 54 | −0.24 | 4079:1726 |

Only ONE `JP/*` variable is referenced by any in-scope frame. Every other JA size therefore has no
node-side token to be measured against, and the audit records the JA font asset actually bound at
runtime per site (A3's JA pass) instead of asserting a size defect. Unity:
`Assets/Fonts/NotoSansJP-VariableFont_wght SDF.asset` (`8f62f163976fae841ad23d559ebdf279`).

## Colours — flat

| Variable | Value | Read from |
|---|---|---|
| `Main (Game)/White`, `White`, `Text Colors/Text White` | `#FFFFFF` | 13994:1935 |
| `Main (Game)/Game_Dark_Blue` | `#001E39` | 4065:14998 |
| `Main (Game)/Red` | `#FF0000` | 4079:1726 |
| `Text Colors/Text Blue` | `#2775DD` | 4065:14998 |
| `Text Colors/Mission Font` | `#EEDC9A` | 13994:1935 |
| `Greys/Grey_30%` | `#B2B2B2` | 13994:1935 |
| `Rarity Fonts/Rare` | `#50C878` | 4065:14998 |
| `Rarity Fonts/Mythic` | `#FFC107` | 4079:1726 |
| `Rarity Fonts/Supreme` | `#7851A9` | 4079:1726 |
| `Inverse Fonts/Common Inverse` | `#7E848A` | 4065:9071 |
| `Inverse Fonts/Uncommon Inverse` | `#ABC9F5` | 4065:9071 |
| `Inverse Fonts/Rare Inverse` | `#C0EAC9` | 4065:9071 |
| `Inverse Fonts/Mythic Inverse` | `#FFF5D3` | 4065:9071 |
| `Inverse Fonts/Legendary Inverse` | `#ECB5A3` | 4065:9071 |
| `Inverse Fonts/Supreme Inverse` | `#C6B8DE` | 4065:9071 |

## Colours — gradients (EMPTY from `get_variable_defs`, resolved from node SVG)

`get_variable_defs` returns `""` for every gradient variable. These were extracted with
`download_assets(nodeId, "svg")` and reading the `<linearGradient>` stops — the CSS layer collapses
a gradient to its first stop (`reference_figma_css_hides_gradient_stops`), so the SVG is the only
honest source.

| Variable | Stops | Read from |
|---|---|---|
| **`Gold`** | `#FCF195` @0 → `#D6AB42` @0.6 → `#BB7F1D` @1 | `13026:2366` export SVG |
| **`Silver`** | `white` @0 → `#D1D5DB` @0.4 → `#818EA1` @1 | `12885:91119` (Main Buttons) |
| **Card family fill** | `#133453` @0 → `#091B33` @1 | `12961:1728` (Mission Card Container) |
| Gold glow (radial) | `#FFEC8F` flat | `13026:2366` |

Verbatim excerpt (A1), from the `13026:2366` export:

```xml
<linearGradient id="paint..._linear_12885_87551" ...>
  <stop stop-color="#FCF195"/>
  <stop offset="0.6" stop-color="#D6AB42"/>
  <stop offset="1" stop-color="#BB7F1D"/>
</linearGradient>
```

This **confirms** `UI_ELEMENT_PALETTE.md`'s gold-stroke claim (`#FCF195 → #D6AB42 @0.6 → #BB7F1D`)
against the file rather than inheriting it.

## Unresolved — listed, not guessed (A1)

| Variable | Why | How to resolve |
|---|---|---|
| `Blue`, `Copper` | EMPTY from the API; no in-scope node exported so far draws one | SVG of a node that uses them — Copper appears on `4079:1726` (Rankings 3rd-place), Blue on `4065:9071` |
| `Rarity Backgrounds/{Common,Uncommon,Rare,Mythic,Legendary,Supreme}` | EMPTY (gradients) | SVG per rarity chip on `4065:14998` / `4065:9071` |
| `Parameters/{Parameter Bar, Parameter Bar Max, Durability Bar Low}` | EMPTY (gradients) | SVG of a StatBar / durability bar instance |
| `Text Colors/Text Silver` | EMPTY (gradient) | SVG of a Settings row label, `4065:16939` |
| `Rarity Fonts/{Common,Uncommon,Legendary}` | Not returned by any frame queried | query a frame showing those rarities |
| `EN/Title_1`, `EN/Large Title`, `EN/Body`, `EN/Caption_1` | Not referenced by any in-scope frame | may not exist in the file; confirm before citing one as an expected step |

## Shared atom geometry

| Atom | Value | Source |
|---|---|---|
| Card family fill | `#133453 → #091B33`, 3 px white border, r50 big / r32 small | playbook §3 + SVG above |
| `Pop-up` panel | `#133453 → #091B33`, silver edge, shadow baked in the sprite margin (L20 R20 T10 B30) | palette |
| `Main Buttons` | 348×120 (HoleSelection `12885:91119`), 359×120 (`12976:1038`), 54-high in Clubs detail | node metadata |
| Nav Bar Container | 1170×263 (`2098:7988`) — render is 273 incl. bleed | node render |
| Top UI | 1170×313 (`12961:1697`) — render is 321 incl. bleed | node render |
