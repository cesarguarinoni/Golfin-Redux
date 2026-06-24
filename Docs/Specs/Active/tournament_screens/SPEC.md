# Tournament Screens — Code-Proof Implementation Spec

> **Status:** Stage 0 foundation laid 2026-06-24 JST. Two screens: **Tournament Hole Selection** + **Tournament Leaderboard**.
> **Authority:** `Docs/Game Design/Tournaments_GDD.md` (decisions locked 2026-06-22) **+ its §17 Addendum (2026-06-24)** — read §17 first; it overrides earlier sections where named.
> **Figma file:** `5gEAHjl6xAtW8iYY7NMvWd` ("Golfin Game Redux"). Link form: `https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=<ID-with-dash>`.
> Maps to Implementation Plan orders **T9** (leaderboard) **+ a new order** for Hole Selection (insert before T9). Backend T1–T6 are prerequisites for live data (Stage 2), but Stages 0–1 are buildable now against placeholder/mock data.

---

## 0. How to use this spec (rules Code MUST follow)

1. **Clone-and-modify, never rebuild.** Every screen/element below names an existing Unity asset to duplicate and modify. Auto-reject any from-scratch hierarchy.
2. **Prefab-first / placeholder-baked (hard rule).** Every repeating element is a **committed `.prefab` with placeholder data baked in**. Runtime code **only instantiates + fills data** — no purely runtime-built hierarchies. Each prefab must open and render standalone in the editor so it can be hand-edited.
3. **Verbatim node IDs.** Pull live values from the IDs/links here via Figma MCP (`get_metadata` → `get_design_context`); do not eyeball from screenshots.
4. **Conversions:** Figma px ÷ 1.4 = Unity TMP font size. All gaps/paddings are **multiples of 8** (8/16/24/32; list gap 24; side margins 48).
5. **RP-icon rule:** any prize/fee amount uses the RP coin image (hash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`), never the letters "RP". **EXCEPTION:** the leaderboard **STROKES** value is *not* RP — plain number, **no coin** (legacy `RP Container` / `RP Amount` node names from the clone are vestigial; treat as a strokes pill).
6. **Main Buttons:** use real component instances; **swap variants via `instance.swapComponent(comp)`**, never `setProperties` (the set has internal errors). Silver Close = variant **Silver-Small Enabled=Yes `2541:11875`**.
7. **Stage gating:** deliver in the staged order in §3. Each stage = one reviewable Code handoff. Do not run ahead.

---

## 1. Clone-and-modify map (grounded in Unity, verified 2026-06-24)

| New thing | Clone from (Unity) | Notes |
|---|---|---|
| Tournament Hole Selection screen | `Assets/Scripts/UI/HoleSelection/` (screen + hole-card prefab) | Per-hole data via `HoleData.cs` / `HoleDatabase.cs` / `HoleDatabaseLoader.cs`. **Lomond hole text+images exist; other clubs placeholder.** |
| FINISHED hole-card result block | `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` ("Result Screen card") | Reuse much of the per-hole result layout (TEE OFF / STROKES / TIME / RANK). |
| Tournament Leaderboard screen | `Assets/Scripts/UI/Rankings/` (+ `Rankings/Core/`) | **Strip period machinery** (`LeaderboardPeriodKey`, Daily/Weekly/Monthly, `ILeaderboardProvider` period concept). Single board. |
| Top identity-pill row (BOTH screens) | The Rankings/Hole-Selection **filter pill** | Repurpose segments → **sponsor mark · league/tournament name · countdown timer**. Drop the period filter. |
| Close button (BOTH) | Main Buttons → **Silver-Small Enabled=Yes `2541:11875`** | Centered, bottom. |
| Nav | `Assets/Scripts/UI/ScreenManager.cs` (full screens) | Both screens are full screens (own backgrounds). |
| Claim modal (later) | `Assets/Scripts/UI/Modals/ModalController.cs` | §17.6: auto-claim + leaderboard link. Not part of these two screens' Stage 0–2. |
| Backend | `ITournamentBackend` → `LocalTournamentBackend` (bots) now; `RemoteTournamentBackend` later | GDD §8. AI field now, real server players later — same seam. |

---

## 2. Shared tokens (from GDD §16.1, re-confirm per element via `get_design_context`)

- Canvas **1170 × 2532**, side margins **48** → content **1074**.
- Back panel 1074w, radius 40, gradient `#133453→#091B33`. Card 978w, radius 50, same gradient. List gap **24**.
- Fonts: **Rubik** (Bold/SemiBold/Medium/Regular), **Noto Sans JP Bold**.
- Gold `#EBD170` · `#EEDC9A` · `#FAC74D`. Prize green `#73E080`. Link blue `#8CD1FF`. RP-pill bg `#001E39`.
- Rarity color styles (badge fills, bind by name `Rarity Fonts/<tier>`): Common `#454b60` · Uncommon `#2775dd` · Rare `#50c878` · Mythic `#ffc107` · Legendary `#c04000` · Supreme `#7851a9`.
- RP coin imageHash `d7b5d07acf45a459f8117adbc96d7ae0368c95c1`.

---

## 3. Staged delivery (controlled — one Code handoff each)

- **Stage 0 — Prefabs only.** Build every prefab in §4 as a committed `.prefab` with placeholder data baked in. Static, no logic, no screen wiring. Cesar reviews/edits each prefab. *(This stage's detailed measurements are filled in §4 — see TO-EXTRACT markers.)*
- **Stage 1 — Screen scaffolds + nav.** Both full screens (own backgrounds), identity-pill row, scroll containers, podium-icon → Leaderboard, Close buttons back. Static prefab instances placed. Wire `Selection → Hole Selection → Leaderboard` and back via `ScreenManager`. Drop vestigial Arrows.
- **Stage 2 — Bind to `LocalTournamentBackend`** (deterministic bots): hole-card states from entry progress; leaderboard podium + rows from `GetLeaderboard`; empty-state when no finishers; sticky "you" row (live/partial, `--`, LIVE badge); finished-card RANK = overall tournament rank.
- **Stage 3 — State polish:** Provisional/Final labeling, `T`-tie prefixes, finished-card edge cases, and the stamina/locked-character flag (left off v1 per §17.7) revisited.

---

## 4. Stage 0 — Prefab inventory

> Each prefab: Figma source node, clone source, geometry, and styling. Values marked **[TO-EXTRACT]** must be pulled from the named node via `get_metadata` (geometry) + `get_design_context` (fonts/colors/fills) before the prefab is built. Geometry already confirmed is noted inline.

### Screen A — Tournament Hole Selection  ·  root `13414:2936` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2936)
Cards Container `13414:2969`. Podium-icon container (→ Leaderboard) `13414:2979`. Bottom Close `13414:5576`. Drop Arrows `13414:2977`, `13414:2978`.

**A1 · Hole card — FINISHED** (`prefab: TournamentHoleCard_Finished`)
- Figma `13414:5549` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5549) · clone hole-card + Result-card block (`HoleCompleteModalController`).
- Content: "FINISHED" badge · "Club – Hole N – Par X" title · green thumbnail · course-map mini · result stats (TEE OFF / **STROKES: n** with par colour / TIME / **RANK: #N** — overall tournament rank, `T` only if tied). Read-only (arrow hidden).
- Geometry/styling: **[TO-EXTRACT 13414:5549]** (card w/h, badge, thumbnail, stat row fonts+colors).

**A2 · Hole card — NEXT** (`prefab: TournamentHoleCard_Next`)
- Figma `13414:2972` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-2972) · clone hole-card.
- Content: gold "NEXT" badge · hole title · thumbnail/map · strategy-tip text (from `HoleData`; Lomond real, else placeholder) · gold **PLAY** button (Main Buttons gold).
- Geometry: height **~700px** (confirm). Rest **[TO-EXTRACT 13414:2972]**.

**A3 · Hole card — LOCKED** (`prefab: TournamentHoleCard_Locked`)
- Figma `13414:4041` (siblings `13414:4194`, `13414:4185`) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-4041) · clone hole-card, darkened.
- Content: lock icon · "LOCKED" · hole title. Darkened fill.
- Geometry: height **164px** (confirm). Rest **[TO-EXTRACT 13414:4041]**.

**A4 · Close button (silver)** (`prefab: TournamentCloseButton` — shared with Screen B)
- Figma `13414:5576` · clone Main Buttons **Silver-Small Enabled=Yes `2541:11875`** (swapComponent). Centered, bottom. → Hole Selection: back to Tournament Selection.
- Geometry: **[TO-EXTRACT 13414:5576]** (button w/h; expected ~260×54).

### Screen B — Tournament Leaderboard  ·  root `13414:5598` (1170×2532) · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5598)
Own background. Title "TOURNAMENT LEADERBOARD". Identity-pill row (sponsor · league name · timer). TOP3 podium `13414:5632`. Numbers overlay `13414:5914`. Ranked list (#4+) `13414:5703`. Sticky "you" row `13414:5892`. LIVE badge `13414:6122`. Drop Arrows `13414:5911`, `13414:5912`.

**B1 · Podium item (Top 3)** (`prefab: TournamentPodiumItem`)
- Source: inside podium `13414:5632`; rank overlay `13414:5914` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5632) · clone Rankings podium item.
- Content: character portrait (Rarity Background + Characters) · username · rarity tier · level · **STROKES** pill (no RP coin). #1 center/tallest, #2 left, #3 right.
- Geometry/styling: **[TO-EXTRACT 13414:5632 + child item node id]**.

**B2 · Ranking row (#4+)** (`prefab: TournamentRankingRow`)
- Source: a row inside list `13414:5703` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5703) · clone Rankings card row.
- Content: rank number (`T`-prefix only if tied) · portrait · username · rarity·level · **STROKES** pill (no coin).
- Geometry: row height **110.83px** (confirm). Child row node id + styling **[TO-EXTRACT 13414:5703]**.

**B3 · Sticky "you" row** (`prefab: TournamentPlayerStickyRow`)
- Figma `13414:5892`; LIVE badge `13414:6122` · [link](https://www.figma.com/design/5gEAHjl6xAtW8iYY7NMvWd/?node-id=13414-5892) · clone ranking row + add LIVE badge.
- Content: username · rank `--` until finished · **LIVE** badge (red, Legendary `#c04000`) while active · running strokes. Always rendered, pinned bottom.
- Geometry/styling: **[TO-EXTRACT 13414:5892, 13414:6122]**.

**B4 · Empty-state message** (`prefab: TournamentLeaderboardEmptyState`)
- No Figma source yet (Cesar flagged as missing — author it). Shown in the list area when no finishers; sticky "you" row still renders below.
- **Draft copy (EN):** headline "No finishers yet" · body "Be the first to complete every hole and top the board." — localization key `tournament.leaderboard.empty.title` / `.body` (JP via existing localization CSV). *Confirm/adjust copy.*
- Style: center-aligned, panel-on-card tokens (§2), muted text. Build as an editable prefab.

**B5 · Close button (silver)** — same prefab as **A4** (`TournamentCloseButton`). On Leaderboard: placed at the **bottom of the ranking list, centered, NOT after the sticky row**; → back to Hole Selection.

---

## 5. Open flags / to-resolve

- **Empty-state copy** (B4) — draft above; awaiting Cesar's confirm + JP.
- **Stamina / locked-character UI** — intentionally **off v1** (§17.7); revisit Stage 3.
- **Per-hole rank** — resolved: it is the **overall tournament rank** (§17.5), no new backend method.
- **Implementation Plan** — needs a new UI order inserted for Tournament Hole Selection (before T9); leaderboard = T9.
- **Detailed measurements** — §4 TO-EXTRACT markers are the worklist for the next pass (pull each node, fill geometry + tokens, then build prefabs).
