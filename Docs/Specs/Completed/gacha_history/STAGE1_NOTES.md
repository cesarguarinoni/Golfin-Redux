# gacha_history — MUST-DO notes for Stage 1 (do not lose these)

Recorded 2026-07-14 during the Stage-0 fix pass. **Read this at Stage 1 kickoff.**

## 0. Port the ball-card alignment fix into `GachaHistoryRowBall.prefab` (REQUIRED)

Stage 0 poses the rows **statically inside `GachaHistoryScreen.prefab`**, which carries its own embedded
copy of the ball card. Edits to `GachaHistoryRowBall.prefab` do **NOT** propagate into that embedded copy
(learned the hard way — the fix double-applied when written to both). So the ball-card fix Cesar asked for
lives **only** on `GachaHistoryScreen.prefab` right now:

| Object (under `BallCard`) | Stage-0 value on `GachaHistoryScreen.prefab` | Was |
|---|---|---|
| `BallCard` `anchoredPosition` | `(0, 33.1)` — top-aligns with the club card (both cards now hang 6px below their row's top) | `(0, 67)` |
| `AmountBadge` `anchoredPosition` | `(0 → x unchanged, -23.1)` — pulls the `x99` pill fully inside the card | `(-5, -41)` |
| `AmountBadge` text | `alignment = Right`, `margin = (0,0,10,0)` — keeps `x99` 10px inside the right edge | centered, spilled 25px past the card's left edge |

`GachaHistoryRowBall.prefab` was deliberately **reverted to its original values** so there is exactly ONE
source of truth for what renders today.

**Stage 1 must:** when `GachaHistoryScreenController` starts instantiating rows from
`GachaHistoryRowBall.prefab` at runtime (instead of the statically-posed copies), apply the three values
above to `GachaHistoryRowBall.prefab`, and delete the statically-posed rows from `GachaHistoryScreen.prefab`
so the embedded copies can't drift. Verify with the same measurement: **club card and ball card must each
sit the same distance below their own row's top edge (6.0px), and the `x99` badge must be fully inside the
card bounds.**

## 1. REMOVE the hardcoded stat-bar colour on the static rows (REQUIRED)

Stage 0 posed the history rows as **static clones** of `BagClubCard` with **no binder running**, so the
stat bars showed the prefab's unbound authoring default (white). To make Stage 0 look right, I hardcoded
the bar colour on the static stand-ins:

- `Assets/Prefabs/UI/Gacha/GachaHistoryRow.prefab` → 5 `Bar` Images set to `#3380E6`
- `Assets/Prefabs/UI/Gacha/GachaHistoryScreen.prefab` → 5 `Bar` Images set to `#3380E6`

**This hardcode MUST be removed in Stage 1**, when the history controller binds real reward data to the
card. Reason: `BagClubCard.Bind()` → `SetBar(...)` sets **both** `fillAmount` **and** `bar.color` from
data (`BagClubCard.cs:121-125`):

```csharp
private static readonly Color StatBarColor       = new Color(0.2f, 0.5f, 0.9f, 1f); // #3380E6
private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f); // red
...
if (bar != null) { bar.fillAmount = cap > 0 ? (float)value / cap : 0f; bar.color = color; }
```

If the hardcoded blue is left in place, it will **mask the red low-durability state** (and any future
stat-based colour rule). The binder must own the colour — not the prefab.

**NOTE (Cesar, 2026-07-14):** the shared `BagClubCard` / `ItemUseClubCard` / `GeneralShopCard` prefabs
are **NOT broken** and were deliberately left untouched — their white bars are the unbound default and
are correctly overwritten at runtime. Do not "fix" them.

## 1b. Rarity letter / rarity background are ALSO runtime-driven (no fix needed, same trap)

Cesar flagged "rarity letter is not coloured by rarity" during the Stage-0 review. It is **not broken** —
same situation as the stat bars. `BagClubCard.Bind()` (`BagClubCard.cs:62-81`) sets all of it from data:

```csharp
var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");   // rarity card background
...
rarityBadgeText.text  = RarityHelper.GetRarityLabel(template.rarity);   // the letter (R/M/L/...)
rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);  // its colour
```

The Stage-0 rows show the authored default because no binder runs. **Do not hardcode the rarity letter,
its colour, or the rarity background on the prefabs** — Stage 1's binder owns all three. Verify after
binding that Rare/Mythic/etc. render with the correct letter + colour + card background.

## 2. Stat-bar blue token drift (see also `Docs/Specs/Queued/statbar_blue_token_drift.md`)

The blue is inconsistent across design and code — flagged, not yet reconciled:

| Source | Value |
|---|---|
| Figma node `4079:18306` (sampled from the render) | `#387FDF` |
| `BagClubCard.StatBarColor` (shipping runtime) | `#3380E6` |
| `ItemUseClubCard.prefab` | `#3380E6` |
| `GeneralShopCard_Club.prefab` | `#3B7DDB` |

Imperceptible in practice, but three different blues are in play. Decide on one token if/when it matters.
