# Tournament Screens — Code-Proof Implementation Spec

> **Status:** Stage 0 geometry extracted 2026-06-24 JST. Two screens: **Tournament Hole Selection** + **Tournament Leaderboard**.
> **Authority:** `Docs/Game Design/Tournaments_GDD.md` (locked 2026-06-22) **+ §17 Addendum (2026-06-24)** — read §17 first; it overrides earlier sections where named.
> **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd`. Link form: `https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=<ID-with-dash>`.
> Maps to Implementation Plan **T9** (leaderboard) **+ a new order** for Hole Selection (insert before T9). Backend T1–T6 gate live data (Stage 2); Stages 0–1 build against placeholder data.
>
> **Token note:** geometry below is fully extracted via Figma `get_metadata`. Exact per-element **font sizes / fills** were NOT pulled — the local Figma `get_design_context` endpoint was unresponsive during authoring (needs MCP restart). Each prefab carries its **node-id link** so Code can pull `get_design_context` directly; combined with the cloned Unity prefab's baked styling + the shared tokens in §2, layout is fully determined.

---

## 0. Rules Code MUST follow

1. **Clone-and-modify, never rebuild.** Each element names an existing Unity asset to duplicate. Auto-reject from-scratch hierarchies.
2. **Prefab-first / placeholder-baked (hard rule).** Every repeating element is a committed `.prefab` with placeholder data baked in. Runtime **only instantiates + fills data** — no purely runtime-built hierarchies. Each prefab must render standalone in the editor for hand-editing.
3. **Pull live values** from the node links via `get_metadata` → `get_design_context`. Do not eyeball screenshots.
4. **Conversions:** Figma px ÷ 1.4 = Unity TMP font size. Gaps/paddings = multiples of 8 (8/16/24/32; list gap 24; side margins 48).
5. **RP-icon rule:** prize/fee amounts use the RP coin (hash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`), never letters "RP". **EXCEPTION — leaderboard STROKES:** the strokes value is *not* RP — plain number, **no coin**. (Cloned nodes are named `RP Container` / `RP Amount`; that naming is vestigial — treat as a strokes pill and strip any coin child.)
6. **Main Buttons:** real component instances; swap variants via `instance.swapComponent(comp)`, never `setProperties`. Silver Close = variant **Silver-Small Enabled=Yes `2541:11875`**.
7. **Stage gating:** deliver in §3 order; one reviewable handoff per stage.

---

## 1. Clone-and-modify map (grounded in Unity, verified 2026-06-24)

| New thing | Clone from (Unity) | Notes |
|---|---|---|
| Tournament Hole Selection screen | `Assets/Scripts/UI/HoleSelection/` (screen + hole-card prefab) | Per-hole data via `HoleData.cs` / `HoleDatabase.cs` / `HoleDatabaseLoader.cs`. **Lomond hole text+images exist; other clubs placeholder.** |
| FINISHED hole-card result block | `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | Reuse the per-hole result layout (TEE OFF / STROKES / TIME / RANK). |
| Tournament Leaderboard screen | `Assets/Scripts/UI/Rankings/` (+ `Rankings/Core/`) | **Strip period machinery** (`LeaderboardPeriodKey`, `ILeaderboardProvider` period concept, Daily/Weekly/Monthly). Single board. |
| Top identity-pill row (BOTH) | Rankings/Hole-Selection **filter pill** | Repurpose segments → **sponsor mark · league/tournament name · countdown timer**. Drop period filter. |
| Close button (BOTH) | Main Buttons → **Silver-Small Enabled=Yes `2541:11875`** | Centered. |
| Nav | `Assets/Scripts/UI/ScreenManager.cs` | Both are full screens (own backgrounds). |
| Claim modal (later) | `Assets/Scripts/UI/Modals/ModalController.cs` | §17.6 auto-claim + leaderboard link. Not in these screens' Stage 0–2. |
| Backend | `ITournamentBackend` → `LocalTournamentBackend` (bots) now; `RemoteTournamentBackend` later | GDD §8. AI field now, real server players later — same seam. |

---

## 2. Shared tokens (GDD §16.1 — re-confirm per element via `get_design_context`)

- Canvas **1170 × 2532**, side margins **48** → content **1074**.
- Back panel 1074w r40, gradient `#133453→#091B33`. Card 978w r50, same gradient. List gap **24**.
- Fonts: **Rubik** (Bold/SemiBold/Medium/Regular), **Noto Sans JP Bold**.
- Gold `#EBD170` · `#EEDC9A` · `#FAC74D`. Prize green `#73E080`. Link blue `#8CD1FF`. RP-pill bg `#001E39`.
- Rarity styles (bind by name `Rarity Fonts/<tier>`): Common `#454b60` · Uncommon `#2775dd` · Rare `#50c878` · Mythic `#ffc107` · Legendary `#c04000` · Supreme `#7851a9`.
- RP coin imageHash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`.

---

## 3. Staged delivery (one Code handoff each)

- **Stage 0 — Prefabs only.** Build every §4 prefab as a committed `.prefab`, placeholder data baked in. Static, no logic, no wiring. Cesar reviews/edits each.
- **Stage 1 — Screen scaffolds + nav.** Both full screens (own backgrounds), identity-pill row, scroll containers, podium-icon→Leaderboard, Close buttons back. Static prefab instances placed. Wire `Selection → Hole Selection → Leaderboard` and back via `ScreenManager`. Drop vestigial Arrows.
- **Stage 2 — Bind to `LocalTournamentBackend`** (bots): hole-card states from entry progress; podium + rows from `GetLeaderboard`; empty-state when no finishers; sticky "you" row (live/partial, `--`, LIVE); finished-card RANK = overall tournament rank.
- **Stage 3 — State polish:** Provisional/Final labeling, `T`-tie prefixes, edge cases, stamina/locked-character flag (off v1 per §17.7) revisited.

---

## 4. Stage 0 — Prefab inventory (geometry extracted; tokens via links)

> Coordinates are `x,y,w,h` in Figma px, relative to the parent. `[ctx <node>]` = pull `get_design_context` on that node for font px + fills.

### SCREEN A — Tournament Hole Selection
Root `13414:2936` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2936). Cards Container `13414:2969`. Podium-icon → Leaderboard `13414:2979`. Bottom Close `13414:5576`. **Drop Arrows** `13414:2977`, `13414:2978`. Top identity-pill row (sponsor · league/tournament name · countdown) — shared with Screen B; reuse base filter pill.

**A1 · `TournamentHoleCard_Finished`** — Figma `13414:5549` "Tournament Hole Card Container" **978×542.5** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5549) · clone hole-card + Result block (`HoleCompleteModalController`). [ctx 13414:5549]
- Pop-Up `13414:5550` 978×542.5
  - Mission Title `13414:5551` 978×150 @0,24
    - Badge `13414:5552` 206×60 @386,0 (centered) → text **FINISHED** `13414:5553` 206×60
    - Title row `13414:5554` 946×64 @16,70 → title text `13414:5555` "Club – Hole N – Par X" 812×54 @67,0 · Arrow Container `13414:5556` 46×54 @817.5 **hidden** (read-only)
  - Separator line `13414:5559` @0,174 w978
  - Mission Content `13414:5560` 978×368.5 @0,174
    - Tutorial `13414:5561` 749.6×320.5 @114.2,24
      - Green thumbnail `13414:5562` 94×94.9 @0,16
      - Map group `13414:5563` 155.6×288.5 @94,16 → course-map `13414:5564` 155.6×288.5 + shot-path dots `13414:5565‑5568` ~7.9×7.9
      - Goals `13414:5569` 500×240 @249.6,16
        - Stats `13414:5570` 404×228 @48,12 → result text `13414:5571` 404×216 — **"TEE OFF: … / STROKES: n (PAR) / TIME: 00:02:34 / RANK: #N"** (STROKES par-colored; **RANK = overall tournament rank**, drop `T` unless tied)
        - `13414:5572` "DOWNLOAD SIZE" **hidden — delete** (clone artifact)
- Fill: badge=FINISHED · title=club+hole+par · green thumb=hole image · map=hole map + path dots · stats=tee/strokes(+par color)/time/rank.

**A2 · `TournamentHoleCard_Next`** — Figma `13414:2972` instance "Mission Card Container" **978×700.5** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2972) · clone existing **Mission Card** prefab (NEXT state). [ctx 13414:2972 — instance is collapsed in metadata; pull context or open the main component]
- Content: gold **NEXT** badge · hole title · thumbnail/map · **strategy-tip** text (from `HoleData`; Lomond real, else placeholder) · gold **PLAY** button (Main Buttons gold).

**A3 · `TournamentHoleCard_Locked`** — Figma `13414:4041` "Mission Card Container" **978×164** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-4041) · clone hole-card, darkened. [ctx 13414:4041]
- Darken vector `13414:4183` 978×164 (overlay)
- Pop-Up `13414:4042` 978×164 → Mission Title `13414:4043` 978×164
  - Badge `13414:4044` 225×60 @376.5,24 → lock icon `13414:4045` 40×50 @0 · text **LOCKED** `13414:4046` 175×60 @50
  - Title text `13414:4047` "Club – Hole N – Par X" 946×54 @16,94

**A4 · `TournamentCloseButton`** (shared A+B) — Figma `13414:5576` instance "Main Buttons" **308×120** @383 (centered in 978 column) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5576) · clone Main Buttons → **Silver-Small Enabled=Yes `2541:11875`** (swapComponent). On Hole Selection → back to Tournament Selection.

### SCREEN B — Tournament Leaderboard
Root `13414:5598` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5598). Own background. Title "TOURNAMENT LEADERBOARD" [ctx — pull title node from root]. Identity-pill row shared with A. **Drop Arrows** `13414:5911`, `13414:5912`.

**B1 · `TournamentPodiumItem`** (variant: first vs second/third) — TOP3 `13414:5632` 894×463 @90,24 · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5632) · clone Rankings podium. [ctx 13414:5653]
- **#2 left** `13414:5633` 282×389 @12,62 · **#1 center/tallest** `13414:5653` 282×439 @306,12 · **#3 right** `13414:5673` 282×389 @600,62
- Item (ref #1 `13414:5653`): Sheen bg `13414:5654` 280×187 (#2/#3: 280×166) · base glow ellipse · Frame `13414:5656` 250×332 (#2/#3 282) → Portrait Frame 250×**200** (#2/#3 250×**150**) → Portrait Image Container: **Rarity Background** instance 192×250 + **Characters** instance · User Data 250×120: Name (username) `13414:5664` · rarity tier `13414:5666` · `Lv` `13414:5668`
- **STROKES pill** "RP Container" `13414:6074` ~231×71 → "RP Amount" text (**no coin**)
- **Rank number** from overlay "Numbers" `13414:5914` (1/2/3 over podium) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5914)
- #1 has the bigger portrait (200 tall) + taller card; #2/#3 sit lower.

**B2 · `TournamentRankingRow`** — canonical row "Rankings Card" `13414:5705` (first in list `13414:5703`) **978×110.83** · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5705) · clone Rankings card row. [ctx 13414:5705]
- Rank-num col `13414:5706` 101×62.83 @24,24 → text `13414:5707` 71×54 @15 (rank; `T`-prefix only if tied)
- Portrait `13414:5708` 100×100 @125,5.4 → Rarity Background 100×100 + Characters 100×133
- User Details `13414:5712` 500×62.83 @225,24 → Username `13414:5713` 184×39 · rarity/level line `13414:5715` (text 180×36)
- **STROKES pill** "RP Container" `13414:5717` 229×71 @725,19.9 → "RP Amount" 197×39 (**no coin**)
- Pitch = 110.83 + separator line between rows. List parent `13414:5703`.
- **Delete** the in-list "Player" row `13414:5874` (clone leftover; its pill `13414:5888` has a real RP coin 48×48) — superseded by the sticky row (B3).

**B3 · `TournamentPlayerStickyRow`** — "Cards Container" `13414:5892` **1074×166.16** @ y1731.84 (pinned screen bottom, full content width) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5892) · clone row + add LIVE badge. [ctx 13414:5892]
- Inner row `13414:5894` 978×118.16 @48,24
  - Rank-num `13414:5895` 101×70.16 → text `13414:5896` 71×54 (**`--` until finished**)
  - Portrait `13414:5897` 100×100
  - User Details `13414:5901` 495×70.16 → Username `13414:5903` 304×39 · level line `13414:5905`
  - **STROKES pill** "RP Container" `13414:6116` 234×71 → "RP Amount" 202×39 (**no coin**)
- **LIVE badge** `13414:6122` 62×24 @996,16.2 → text "LIVE" `13414:6123` 46×24 (red, Legendary `#c04000`; shown while active)
- Always rendered (even when unranked). Content: username · rank (`--` until finished) · running strokes · LIVE while active.

**B4 · `TournamentLeaderboardEmptyState`** — no Figma source (authored). Shown in the list area when **no finishers**; sticky row still renders below.
- **Copy (approved EN):** title **"No finishers yet"** · body **"Be the first to complete every hole and top the board."** Keys `tournament.leaderboard.empty.title` / `.body`. **JP: TODO** via localization CSV.
- Style: centered, card/panel tokens (§2), muted text. Build as an editable prefab.

**B5 · Close button** — same prefab as **A4** (`TournamentCloseButton`). **TO-ADD** on Leaderboard (not yet in Figma). Place **after the last ranked row, centered, ABOVE the pinned sticky container `13414:5892`**. → back to Hole Selection.

---

## 5. Flags / to-resolve

- **Token pass pending** — `get_design_context` endpoint hung during authoring (needs MCP restart). Geometry complete; font px + exact fills to be pulled per `[ctx <node>]` marker (or by Code via the node links).
- **Clone artifacts to remove:** FINISHED "DOWNLOAD SIZE" `13414:5572` (hidden→delete); in-list "Player" row `13414:5874` (+ RP coin); left/right Arrows on both screens.
- **STROKES pills** keep legacy `RP Container`/`RP Amount` node names but render a plain strokes number with **no coin**.
- **Close button** is TO-ADD on the Leaderboard (clone A4).
- **Empty-state JP** localization TODO.
- **Implementation Plan** — insert a new UI order for Tournament Hole Selection (before T9); leaderboard = T9.
- **Stamina / locked-character UI** — off v1 (§17.7); revisit Stage 3.
