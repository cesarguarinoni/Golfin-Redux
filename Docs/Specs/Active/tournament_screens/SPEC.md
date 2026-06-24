# Tournament Screens — Code-Proof Implementation Spec

> **Status:** Stage 0 fully specced (geometry + tokens + reuse map) 2026-06-24 JST. Two screens: **Tournament Hole Selection** + **Tournament Leaderboard**.
> **Authority:** `Docs/Game Design/Tournaments_GDD.md` (locked 2026-06-22) **+ §17 Addendum (2026-06-24)** — read §17 first; it overrides earlier sections where named.
> **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`. Link form: `https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=<ID-with-dash>`.
> Maps to Implementation Plan **T9** (leaderboard) **+ a new order** for Hole Selection (insert before T9). Backend T1–T6 gate live data (Stage 2); Stages 0–1 build against placeholder data.

---

## 0. Rules Code MUST follow

1. **REUSE, don't recreate (HARD RULE).** Every element below names an existing Unity prefab/component. Code **duplicates that prefab and modifies only the diff** — e.g. the podium: duplicate `RankingsScreen.prefab`'s podium, swap the RP pill → STROKES, drop the coin. **Never rebuild a hierarchy that already exists.** The **only** element created from scratch is the empty-state message (B4) — nothing equivalent exists. If unsure whether something exists, search the repo before creating.
2. **Prefab-first / placeholder-baked.** Every repeating element is a committed `.prefab` with placeholder data baked in. Runtime **only instantiates + fills data** — no purely runtime-built hierarchies. Each prefab must render standalone in the editor for hand-editing.
3. **Conversions:** Figma px ÷ 1.4 = Unity TMP font size. Gaps/paddings = multiples of 8 (8/16/24/32; list gap 24; side margins 48).
4. **RP-icon rule:** prize/fee amounts use the RP coin (hash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`), never letters "RP". **EXCEPTION — leaderboard STROKES:** plain `"{n} STROKES"` text, **no coin** (Figma confirms; nodes keep the vestigial `RP Container`/`RP Amount` names — strip any coin child).
5. **Main Buttons:** real component instances; swap variants via `instance.swapComponent(comp)`, never `setProperties`. Silver Close = **Silver-Small Enabled=Yes `2541:11875`**.
6. **Stage gating:** deliver in §3 order; one reviewable handoff per stage.

---

## 1. Reuse map — duplicate these existing Unity assets (verified 2026-06-24)

| New thing | DUPLICATE this Unity asset | Modify (the only diff) |
|---|---|---|
| Hole card — Finished / Next / Locked | `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab` | per-state badge + content (see A1–A3) |
| FINISHED result stat block | `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` | graft TEE OFF / STROKES / TIME / RANK into the finished card |
| Leaderboard screen **+ Top-3 podium** | `Assets/Prefabs/UI/Rankings/RankingsScreen.prefab` (podium is **inside** it) | strip period tabs; RP pill → STROKES |
| Ranking row (#4+) | `Assets/Prefabs/UI/Rankings/RankingsCards.prefab` | RP pill → STROKES (no coin) |
| Sticky "you" row | `Assets/Prefabs/UI/Rankings/RankingsCardUser.prefab` | RP → STROKES, rank `--`, add LIVE badge |
| Row/screen logic | `RankingsScreenController.cs`, `RankingsCardWidget.cs` (`Assets/Scripts/UI/Rankings/`) | strip period machinery (`LeaderboardPeriodKey`); bind strokes |
| Hole list / screen scaffold | `Assets/Scripts/UI/HoleSelection/` + hole data `HoleData.cs`/`HoleDatabase.cs` | Lomond data exists; other clubs placeholder |
| Close button (both) | Main Buttons component → **Silver-Small `2541:11875`** | label "CLOSE", centered |
| Claim modal (later) | `Assets/Scripts/UI/Modals/ModalController.cs` | §17.6 auto-claim + leaderboard link |
| Nav | `Assets/Scripts/UI/ScreenManager.cs` | both are full screens (own backgrounds) |
| **Empty-state message (B4)** | **NONE — create new** (only from-scratch element) | author copy + simple panel |
| Backend | `ITournamentBackend` → `LocalTournamentBackend` (bots) now; `RemoteTournamentBackend` later | GDD §8 |

*Ignore* `Assets/Prefabs/Original/Mainmenu/Screens/RankingScreen.prefab` (pre-Redux legacy).

---

## 2. Shared tokens

**Canvas/panels** (GDD §16.1): canvas 1170×2532, margins 48 → content 1074. Card gradient `#133453→#091B33`, border 3px (white on hole cards; gold `#FCF195` on podium #1 + sticky container), drop-shadow `0 10 10 rgba(0,0,0,.4)` (podium `0 8 8 rgba(0,0,0,.25)`). Card radius 50; podium/sticky panel radius 20; pill radius 50. List gap 24.

**Text styles** (Figma px → **TMP = ÷1.4**), font **Rubik** unless noted:
| Style | Figma | TMP | Used for |
|---|---|---|---|
| Subhead | SemiBold 45 / lh60 / -0.69 | **32** | FINISHED / LOCKED badge text |
| Footnote | SemiBold 39 / lh54 / -0.24 | **28** | hole-card title, FINISHED result stats, rank number |
| Caption_2_Medium | Medium 33 / lh39 / +0.18 | **24** | username, **STROKES** pill text |
| Caption_3 | Medium 30 / lh36 / -0.5 | **21** | rarity·level line; podium name/tier/level |
| LIVE | **Bold** 20 | **14** | LIVE badge |

**Recurring components:**
- **STROKES pill** (replaces the RP pill in podium, row, sticky): bg `#001E39`, radius 50, padding 16; text `"{n} STROKES"` Caption_2_Medium white, centered; **no coin**.
- **LIVE badge:** bg `#C04000` (Legendary red), radius 22, padding-x 8; "LIVE" Bold 20 white.
- **Rarity colors** (bind `Rarity Fonts/<tier>`): Common `#454b60` · Uncommon `#2775dd` · Rare `#50c878` · Mythic `#ffc107` · Legendary `#c04000` · Supreme gradient `#2e0f4f→#6f2dbd`.
- **Rarity·level line** format: `"{RARITY in its color} - Lv {n}"` (rarity word colored/gradient, rest white).

---

## 3. Staged delivery (one Code handoff each)

- **Stage 0 — Prefabs only.** Duplicate each source prefab (§1), apply the per-prefab diff (§4), bake placeholder data, commit. Static, no logic, no wiring. Cesar reviews/edits each.
- **Stage 1 — Screen scaffolds + nav.** Both full screens (own backgrounds), identity-pill row, scroll containers, podium-icon→Leaderboard, Close buttons. Wire `Selection → Hole Selection → Leaderboard` and back via `ScreenManager`. Drop vestigial Arrows.
- **Stage 2 — Bind to `LocalTournamentBackend`** (bots): hole-card states from entry progress; podium + rows from `GetLeaderboard`; empty-state when no finishers; sticky "you" row (live/partial, `--`, LIVE); finished-card RANK = overall tournament rank.
- **Stage 3 — Polish:** Provisional/Final labeling, `T`-tie prefixes, edge cases, stamina/locked-character flag (off v1 per §17.7) revisited.

---

## 4. Stage 0 — Prefab inventory (geometry `x,y,w,h` Figma px; tokens per §2)

### SCREEN A — Tournament Hole Selection
Root `13414:2936` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2936). Cards Container `13414:2969`. Podium-icon → Leaderboard `13414:2979`. **Drop Arrows** `13414:2977`, `13414:2978`. Top identity-pill row (sponsor · league/tournament name · countdown) — shared with Screen B.

**A1 · `TournamentHoleCard_Finished`** — dup `HoleCard.prefab` + graft `HoleCompleteWidget.prefab` stat block. Figma `13414:5549` **978×542.5** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5549)
- Pop-Up `13414:5550` (border 1px `#0a1d35`, r50, pt24)
  - Mission Title `13414:5551` 978×150 (gap10, px16, pb16)
    - Badge `13414:5552` 206×60 @386,0 → **FINISHED** `13414:5553` Subhead **green `#50c878`**
    - Title row `13414:5554` 946×64 @16,70 (gap32) → title `13414:5555` 812×54 Footnote white · Arrow `13414:5556` 46×54 **hidden** (read-only)
  - Separator line `13414:5559` @0,174 w978
  - Mission Content `13414:5560` 978×368.5 (px32 py24)
    - Tutorial `13414:5561` 749.6×320.5 @114.2,24 (py16, r20)
      - Green thumb `13414:5562` 94×94.9 (border 3px white, r20)
      - Map group `13414:5563` 155.6×288.5 @94,16 → map `13414:5564` 155.6×288.5 + shot-path dots `13414:5565‑5568` 7.93
      - Goals `13414:5569` 500×240 @249.6,16 (gap24, pt12, px48) → stats `13414:5571` Footnote white; **"TEE OFF: … / STROKES: n (PAR) / TIME: 00:02:34 / RANK: #N"** — `(PAR)`+strokes value **green `#50c878`**; **RANK = overall tournament rank**, drop `T` unless tied
      - `13414:5572` "DOWNLOAD SIZE" hidden — **delete** (clone artifact)

**A2 · `TournamentHoleCard_Next`** — dup `HoleCard.prefab` (NEXT state, the card's native state). Figma `13414:2972` instance **978×700.5** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2972)
- Gold **NEXT** badge · hole title (Footnote white) · thumbnail/map · **strategy-tip** text (from `HoleData`; Lomond real, else placeholder) · gold **PLAY** (Main Buttons gold). Native HoleCard styling — no changes beyond badge=NEXT + PLAY.

**A3 · `TournamentHoleCard_Locked`** — dup `HoleCard.prefab`, darkened. Figma `13414:4041` **978×164** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-4041)
- Darken SVG overlay `13414:4183` 978×164 (covers card incl. border)
- Mission Title `13414:4043` 978×164 → Badge `13414:4044` 225×60 @376.5,24 (gap10): lock vector `13414:4045` 40×50 + **LOCKED** `13414:4046` Subhead **grey `#c8c8c8`** · title `13414:4047` 946×54 @16,94 Footnote white

**A4 · `TournamentCloseButton`** (shared A+B) — dup Main Buttons → **Silver-Small Enabled=Yes `2541:11875`** (swapComponent). Figma `13414:5576` **308×120** @383 (centered in 978 column) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5576). On Hole Selection → back to Tournament Selection.

### SCREEN B — Tournament Leaderboard
Root `13414:5598` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5598). Own background. Title "TOURNAMENT LEADERBOARD". Identity-pill row shared with A. **Drop Arrows** `13414:5911`, `13414:5912`.

**B1 · Podium (Top-3)** — **inside `RankingsScreen.prefab` — reuse, swap RP→STROKES only.** TOP3 `13414:5632` 894×463 @90,24 · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5632)
- **#2 left** `13414:5633` 282×389 @12,62 · **#1 center/tallest** `13414:5653` 282×439 @306,12 (gold `#FCF195` border, r20) · **#3 right** `13414:5673` 282×389 @600,62
- Item (ref #1): Sheen gold-gradient bg · base-glow ellipse · Portrait Frame 250 wide → Portrait Image Container border 3px `#FCF195` r8, **#1 200 tall / #2,#3 150 tall** → Rarity Background instance 192×250 + Characters · User Data gradient panel r8 250w → Name (Caption_3 white) · rarity tier (Caption_3, rarity-colored) · `Lv n` (Caption_3 white)
- **STROKES pill** `13414:6074` (already reads `"69 STROKES"` in Figma) — keep, ensure no coin
- Rank number 1/2/3 from overlay "Numbers" `13414:5914` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5914)

**B2 · Ranking row (#4+)** — dup `RankingsCards.prefab`, RP→STROKES. Row `13414:5705` **978×110.83** (p24, r50, justify-between) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5705)
- Rank-num col `13414:5706` 101w (px15) → text `13414:5707` Footnote white (`T`-prefix only if tied)
- Portrait `13414:5708` 100×100 (border 1px `#FCF195`, r8) → Rarity Background + Characters 100×133
- User Details `13414:5712` flex-1 (px16) → Username `13414:5714` Caption_2_Medium white · level line `13414:5716` Caption_3 `"{RARITY colored} - Lv n"`
- **STROKES pill** `13414:5717` 229×71 @725 (already `"72 STROKES"`) — no coin
- Pitch 110.83 + separator line between rows. List parent `13414:5703`.
- **Delete** the in-list "Player" row `13414:5874` (clone leftover; pill `13414:5888` has a real RP coin 48×48) — superseded by B3.

**B3 · Sticky "you" row** — dup `RankingsCardUser.prefab`, RP→STROKES + rank `--` + LIVE badge. "Cards Container" `13414:5892` **1074×166.16** @y1731.84 (pinned screen bottom; gradient panel, **gold `#FCF195` border**, r20, py24) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5892)
- Inner row `13414:5894` 978×118.16 (p24, r50): rank `13414:5896` Footnote white **`--` until finished** · portrait 100×100 · Username `13414:5903` Caption_2_Medium white · level line `13414:5905` · **STROKES pill** `13414:6116` (already `"80 STROKES"`, no coin)
- **LIVE badge** `13414:6122` 62×24 @996,16.2: bg `#C04000`, r22, px8 → "LIVE" `13414:6123` Bold 20 white (shown while active)
- Always rendered (even unranked).

**B4 · `TournamentLeaderboardEmptyState`** — **CREATE NEW** (nothing to clone). Shown in list area when **no finishers**; sticky row still renders below.
- **Copy (approved):** title **"No finishers yet"** · body **"Be the first to complete every hole and top the board."** Keys `tournament.leaderboard.empty.title` / `.body`. **JP: TODO**.
- Style: centered, card/panel tokens (§2), muted text. Editable prefab.

**B5 · Close button** — same prefab as **A4**. **TO-ADD** on Leaderboard (not yet in Figma). Place after the last ranked row, centered, **ABOVE** the pinned sticky container `13414:5892`. → back to Hole Selection.

---

## 5. Flags / to-resolve
- **Clone artifacts to remove:** FINISHED "DOWNLOAD SIZE" `13414:5572`; in-list "Player" row `13414:5874` (+RP coin); left/right Arrows on both screens.
- **Close button** TO-ADD on the Leaderboard (dup A4).
- **Empty-state JP** localization TODO.
- **Implementation Plan** — insert a new UI order for Tournament Hole Selection (before T9).
- **Stamina / locked-character UI** — off v1 (§17.7); revisit Stage 3.
- **Hole Selection screen scaffold** is not a standalone prefab (only `HoleCard.prefab` exists) — Stage 1 builds the screen by reusing the HoleSelection screen structure/scene; HoleCard is the repeating prefab.
