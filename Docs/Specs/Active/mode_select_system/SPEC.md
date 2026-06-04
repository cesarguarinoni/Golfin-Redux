# SPEC — Mode Select System

**Slug:** `mode_select_system`
**Tier:** FULL PIPELINE (new screens + visual fidelity + new data/economy surface).
**Status:** SPEC_READY
**Depends on:** `practice_1v1_matchmaking_split` (per-mode launch behavior). Insufficient-RP UX RESOLVED 2026-06-03 (disable PLAY + red `#C04000` fee + `ToastController`; no new Figma).
**Figma:** Home carousel `13027-5212` (collapsed) / `13027-10471` (expanded); full-screen `13026-1924` (Mode Selection Screen). File key `5gEAHjl6xAtW8iYY7NMvWd`.

---

## Step 0 — CLONE, do not author (HARD GATE, reviewer-reject on violation)

A prior session built `ModeCard.prefab` from scratch (empty `Image` boxes, no art) and was **discarded 2026-06-04**. Do not recreate it. **No new card or screen GameObject may be authored from scratch.** Every visual element is produced by **duplicating a named existing prefab** and rebinding/resizing its children:

- **Home carousel card** → `Duplicate` `HomeScreen.prefab › NextHolePanel`.
- **Full-screen vertical list** → clone `HoleSelectionScreenController.cs` → `ModeSelectScreenController.cs`; card = `HoleSelection/HoleCard.prefab` + clone `HoleCardController.cs` → `ModeCardController.cs` (locked/accordion treatment verbatim).
- Full element-by-element inventory + reuse table: **`BRIEF_REUSABLE_ELEMENTS.md`** (in this folder) is binding, read it first.

**IMPLEMENTER_REPORT must list the source asset GUID each new prefab was duplicated from.** A new card/screen GameObject with no clone source = automatic reject. Salvaged-and-kept from the prior session (do NOT rebuild): `ScreenManager.cs` (enum `ModeSelection` + slot), `PersistentUIManager.cs` (tee → ModeSelection), `Resources/Data/modes.csv`, `ModeSelect/ModesDatabaseCSV.cs`.

---

## Step 0.1 — Decisions locked 2026-06-04 (Cesar; supersede any conflicting line below)

1. **Two distinct card prefabs, not one.** Home carousel card = `Duplicate(HomeScreen.prefab › NextHolePanel)`, driven by `ModeCarouselController`. Full-screen card = `Duplicate(HoleSelection/HoleCard.prefab)` → `ModeCard`, driven by `ModeSelectScreenController` + cloned `ModeCardController`. The two surfaces share only **data** (`modes.csv` via `ModesDatabaseCSV`) and the **fee/route logic**, NOT a prefab. This supersedes the Reuse-map line that says both surfaces use "the same ModeCardController card prefab."
2. **"Mission Card" is the Figma layer name, not a Unity clone base.** The Figma frames draw the cards as "Mission Card Container", but the Unity reuse bases are NextHolePanel (home) and HoleCard (full-screen), re-skinned to the Figma metrics in `FIGMA_METRICS.md`. Do NOT duplicate `Prefabs/Original/Missions/MissionCard.prefab`.
3. **Fonts are uniform weight in Figma (`Rubik SemiBold`); only sizes differ** (45/39/66 Figma px → 32.14/27.86/47.14 Unity as a STARTING point). This supersedes the §Visual-fidelity "some bold, some regular" wording — the live Figma data has no weight mix. BUT: **Figma "SemiBold" does NOT render 1:1 in Unity TMP** (Cesar 2026-06-04). Use the **`Rubik-VariableFont_wght SDF` variable font and tune the weight axis to visually match the Figma reference screenshots** — do not assume a fixed weight 600 / a baked SemiBold face matches. The ÷1.4 sizes are the starting point; verify each against `screenshots/figma_*.png` and adjust if the rendered weight/size reads heavier or lighter than Figma. Measure each element's **size** individually.
6. **Explanation text = one auto-sizing element for now (Cesar 2026-06-04).** The card's explanation/subtitle text conceptually has a **collapsed** (short tagline) and **expanded** (full description) version. For v1, implement it as a **single TMP element with auto-size enabled** that swaps its string on expand/collapse and auto-fits the available space (cap max size near the Figma-converted value; let it shrink for the long expanded copy). Do NOT build two separate text objects yet — a future task splits them.
7. **Carousel widths confirmed (Cesar 2026-06-04):** the collapsed center card is intentionally **smaller width than expanded** (Figma: collapsed center 556 → expanded center 764). Use the Figma widths as-is; the earlier "narrower than side peeks" flag is resolved as intended.
4. **Figma metrics are pre-pulled.** `FIGMA_METRICS.md` (this folder) + the 3 `screenshots/figma_*.png` are the binding fidelity source, pulled live by the architect main thread because the implementer subagent context cannot reach the Figma MCP. Bind to that file; if a sub-element is missing, ask the architect to pull the node live — do not guess.
5. **First step is a compile-unblock:** the kept `ModesDatabaseCSV.cs` references the deleted `ModeData` type → project does not compile. Re-add a plain `ModeData` DTO (`id/title/tagline/description/entryFee/rewards/locked/target/order`) as `ModeData.cs`. It's a data holder, not an authored card — does not violate Step 0.

---

## Goal

Replace the single home PLAY->Hole-Select entry with a **mode selection layer** offering four modes, across two surfaces that share one data source and one card component:

1. **Home carousel** (`13027`) — horizontal swipe; centered card expands in place (arrow toggle), collapses on slide; banner sits beneath it.
2. **Full-screen Mode Select** (`13026`, "based on Hole Select") — vertical accordion list; reached from the bottom-nav **tee button**.

Modes: **Practice** (fee, playable), **1v1** (fee 0, playable), **Driving Range** (locked), **Missions** (locked).

---

## Modes data (CSV-first, per project convention)

New `Assets/Resources/Data/modes.csv` (Code confirms canonical Resources path):

- `id` — practice / versus_1v1 / driving_range / missions
- `title` (+ JP/EN per nameKey pattern) — display title
- `tagline` — one-liner under title
- `description` — expanded-card body (1v1: "Face off in fast-paced 1v1 golf matches...")
- `entryFee` (int RP) — **0 renders "NO ENTRY FEE"** (the Missions-card treatment). Practice 100; 1v1 = 0
- `rewards` (int RP) — REWARDS row. Sensible defaults: Practice 50, 1v1 200, DrivingRange 0, Missions 200
- `locked` (bool) — true -> locked card (Driving Range, Missions)
- `target` — launch route: hole_select (Practice) / matchmaking_1v1 (1v1) / none (locked)
- `order` (int) — display order in both surfaces

Fee + rewards are examples; values live in CSV so Cesar tunes which modes charge without code changes (answers #1/#6/#7).

---

## Reuse map (verified anchors)

- **Full-screen Mode Select** -> clone `Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` -> `ModeSelectScreenController.cs`; clone `HoleCardController.cs` -> `ModeCardController.cs`. Same scroll-list + expand/collapse accordion; cards bind to modes.csv rows.
- **Card visual** -> the existing **Mission Card** component (Figma layers are literally "Mission Card Container / Pop-Up / Mission Title / Mission Content"). Bind data, do not rebuild.
- **Locked card** -> reuse the **live locked-hole treatment** in `Scripts/UI/HoleSelection/HoleCardController.cs` (verified shipping — confirmed in a Unity capture): `enum HoleCardState { Collapsed, Expanded, Locked }`; `SetState(Locked)` activates `lockedOverlay`, shows `lockIconCollapsed` (lock glyph in title), `Bind` sets title "LOCKED" + silver gradient, dims reward icons/amounts to alpha 0.4, hides the expand chevron, and sets `cardTapButton.interactable = false`. `ModeCardController` clones this verbatim. The `locked` column in `modes.csv` drives it as a **static "coming soon" flag** — no progression service (unlike holes, which gate on `HoleProgressionService.IsUnlocked`). Driving Range + Missions = Locked.
- **Reward row** -> NOTE-R: hole cards render a `List<HoleReward>` (star/wrench/ball icons via `hole.rewards`/`replayRewards`). Mode cards are simpler — a single RP reward (coin icon, per Figma) from `modes.csv.rewards`. `ModeCardController` binds the single-coin row, not the HoleReward list.
- **PLAY button** -> existing gold button component; add `ButtonPressFeedback` sibling (Hard Rule 11).
- **Home carousel** -> new `ModeCarouselController.cs` on Home; reuses the same `ModeCardController` card prefab; horizontal layout + snap + the existing side-arrow assets (13027:10222/10223). Only the centered/expanded card shows PLAY.
- **Top UI / nav / banner** -> `PersistentUIManager` + `HomeScreenController`. Banner = existing `promoBannerButton` / "Cross Promotion Banner" — reposition under the carousel on Home; absent on the full screen.
- **Economy** -> `Scripts/UI/Roster/Managers/RewardPointsManager.cs`: `CanAfford(fee)`, `SpendPoints(fee)`, `GetPoints()`, `OnPointsChanged`. No new currency (answer #8).
- **Navigation** -> add `ModeSelection` to `Scripts/UI/ScreenManager.cs` `enum ScreenId`; route the bottom-nav tee button -> `ShowScreen(ScreenId.ModeSelection)` (replaces its old ->Hole-Select / ->play behavior, answer #4).

---

## Flows

- **Tee button** -> full-screen Mode Select (`ScreenId.ModeSelection`).
- **PLAY routing** (identical on both surfaces, only on the centered/expanded card):
  - Practice -> target=hole_select -> existing Hole Select screen.
  - 1v1 -> target=matchmaking_1v1 -> launch directly (random hole + random opponent -> matchmaking -> gameplay). Behavior owned by `practice_1v1_matchmaking_split`.
  - Driving Range / Missions -> locked -> PLAY disabled, no route.
- **Entry fee (DECIDED 2026-06-03):** card binds fee from `modes.csv`. **Affordable** (`fee==0 || CanAfford(fee)`): fee renders normal, PLAY enabled; on press `SpendPoints(fee)` then route (deduct-on-launch). **Unaffordable** (`fee>0 && !CanAfford(fee)`): (a) ENTRY FEE amount renders **red `#C04000`** (the project's `spDepletedColor`, from `LevelUpModalController`); (b) PLAY renders **greyed/disabled** but stays *technically* interactable — deliberate divergence from level-up's hard `interactable=false`, so the tap can be caught; (c) tapping PLAY fires `ToastController.Instance.Show("Not enough Reward Points", ...)` — no launch, no deduction. Reuses the existing toast (powers "COURSE CLEARED!"); **no new Figma**. 1v1 (fee 0) is never blocked.
- Carousel rotates **all 4** modes (locked ones show locked, no PLAY) — assumption. The two surfaces are independent launchers (no shared "current mode" state).

---

## Visual fidelity gate (Figma-exact — HARD, blocks acceptance)

Every position, size, and font on the mode-select surfaces must match the named Figma frames exactly (Unity-converted). **Reuse means structure + logic, not stale layout** — where a cloned/reused component (Mission Card, locked-hole treatment, mode card text) disagrees with the Figma frame, **Figma wins**; Code adjusts the clone's RectTransform/typography to the frame. Do not inherit prefab metrics on faith.

**Prefab-wins exceptions (Cesar 2026-06-04 — do NOT re-measure to Figma):**
- **Cross-Promotion banner** — ships exactly as the existing `promoBannerButton` prefab; only repositioned under the carousel. Keep its current size/typography.
- **Gold PLAY button** — ships as the existing component as-is (incl. `ButtonPressFeedback`). Keep its current size/typography; only its enabled/greyed state and route change.

**Source frames** (file key `5gEAHjl6xAtW8iYY7NMvWd`): home carousel `13027-5212` (collapsed) / `13027-10471` (expanded); full-screen Mode Select `13026-1924`. Pull metrics **live from Figma MCP** (`use_figma` `getNodeByIdAsync` walk, or `get_design_context` with `excludeScreenshot:false` for full child geometry) — never guess (RUNTIME_BLUEPRINT §8 standing rule).

**Conversion (RUNTIME_BLUEPRINT §1/§7):**
- Canvas `1170×2532, Match=0`. At 1170-wide, **1 Figma px = 1 Unity unit** → x/y offsets and w/h copy directly as canvas units.
- **Unity TMP fontSize = Figma fontSize ÷ 1.4** (record BOTH numbers as a `// NOTE` beside each text element).
- **Fonts are NOT uniform** — measure every text element individually. Sizes differ per element (title vs tagline vs description vs ENTRY FEE vs REWARDS), and weights differ: some are **bold** (use the bold/SemiBold weight of the variable font, or `fontStyle` Bold) where Figma shows bold, regular elsewhere. Read each layer's size AND weight from Figma; never copy one element's font to another.
- Font asset: `Assets/Fonts/Rubik-VariableFont_wght SDF.asset` (NOT `Rubik-SemiBold SDF`). Apply the per-layer weight from Figma.
- Anchors per the frame's layout (corner-anchored against the 1170×2532 ref, per §7 convention).

**Deliverables (in IMPLEMENTER_REPORT):**
- A per-element fidelity table: element → Figma node id → x,y / w,h (Figma px = Unity units) → font family + **weight (regular/bold)** + Figma size + Unity size.
- **Collapsed vs Expanded are measured separately** — the card's size and child layout differ between `13027-5212` (collapsed) and `13027-10471` (expanded). Capture metrics for BOTH states; the fidelity table has a row set per state.
- Save a screenshot of each of the three frames into `Docs/Specs/Active/mode_select_system/screenshots/` as the §Visual reference (mandatory for visual-fidelity tasks).
- Anything unreadable from Figma → `// NOTE`, flag it, do not guess.

## Transitions & animation (smooth, reuse existing — no new tween lib)

The project has **no tween library** (DOTween/LeanTween absent). Reuse the shipped patterns; animate the gaps with coroutine-Lerp (the `ModalController` idiom).

- **Screen swaps** (tee → Mode Select; Mode Select → back; Practice PLAY → Hole Select) — go through `ScreenManager.ShowScreen(id)` with the **default** `instant=false` so `FadeController.FadeOutThenIn` runs (fade-to-black → swap → fade-in). **Never pass `instant=true`** for these.
- **Matchmaking / toast** — reuse `ModalController` `CanvasGroup` `FadeIn`/`FadeOut` (already animated) and the existing `ToastController` animation. No change.
- **Carousel card expand/collapse** — must NOT be an instant `expandedContainer` SetActive pop. Animate height + `CanvasGroup` alpha over a short ease-out (~0.15–0.20s) via coroutine-Lerp; chevron rotates/swaps in step. Collapse reverses.
- **Carousel swipe-snap** — the centered-card snap eases into place (coroutine-Lerp), not an instant jump; expand only fires after snap settles.
- Durations live as serialized fields (Cesar-tunable in inspector), with the ~0.15–0.20s ease-out as defaults. No frame-rate-dependent steps — Lerp on `unscaledDeltaTime` so a paused timescale doesn't freeze menu motion.

## Acceptance gates (via loop_v2_smoke_bot framework — reusability contract)

New Scenarios.cs flows:
1. Tee button -> Mode Select renders 4 cards in `order`; Practice/1v1 enabled, Driving Range/Missions locked (dark + lock glyph, PLAY non-interactive).
2. Home carousel: swipe centers each mode; arrow expands centered card (description visible), collapses on slide; banner stays below; only centered card shows PLAY.
3. Practice PLAY -> Hole Select. 1v1 PLAY -> gameplay reached (delegated; see split spec).
4. Fee economy: Practice PLAY when balance < fee -> ENTRY FEE text is red `#C04000`, PLAY greyed; tapping PLAY -> toast shown, no `SpendPoints`, no launch. Balance >= fee -> fee normal, `SpendPoints` called once, RP counter decrements, launch proceeds. 1v1 (fee 0) -> never blocked, no deduction.
5. Existing screens untouched; EditMode green.
6. Transitions: screen swaps fade (no hard cuts, no `instant=true`); carousel expand/collapse + swipe-snap animate smoothly (no SetActive pop); modals/toast fade as today.

Human LOOK pass: both surfaces match Figma **to the Visual fidelity gate above** — position/size/font mismatches are **blockers, not polish**, measured against `13027`/`13026`. Locked treatment reads "coming soon"; expand/collapse clean.

---

## Out of scope (tracked elsewhere)
- Practice/1v1 matchmaking behavior -> `practice_1v1_matchmaking_split`.
- 1v1 in-game UI -> new roadmap item (Cesar's upcoming Figma).

---

# ITERATION 6 — Figma-exact fidelity pass (canonical tokens)

> Authored by Architect 2026-06-04 ~22:30 JST after iter-5 review (4 captures: home collapsed, full-screen expanded + full-screen collapsed, vs Figma 13027-5212 / 13027-10471 / 13026-1924). All values below are read LIVE from Figma MCP and are LITERAL — Code transcribes them, does not re-measure or interpret. Where iter-5 disagreed with Figma, **Figma wins**. fp note: Figma px = Unity canvas units at 1170 width; Unity TMP fontSize = Figma px / 1.4.

## 6.1 State model (4 states, 2 surfaces) — bot MUST capture all four

| State | Surface | Figma ref | Card structure |
|---|---|---|---|
| Home collapsed | Home carousel | `13027-5212` | centered card: title + tagline (+ expand chevron on centered card) + ENTRY FEE + REWARDS + PLAY. **No description.** |
| Home expanded | Home carousel | `13027-10471` | same as collapsed **plus** description paragraph between title-separator and fee block. |
| Full-screen, one expanded | Mode Select | `13026-1924` | expanded card = title + subtitle + description + ENTRY FEE + REWARDS + PLAY (3 separators). Other cards collapsed. |
| Full-screen, all collapsed | Mode Select | derive from `13026-1924` collapsed cards | render EVERY card in its collapsed form (title + tagline + fee row(s), no description, **no PLAY**). No separate frame needed. |

**PLAY presence rule:** Home = PLAY always on the centered card (both states). Full-screen = PLAY only on the **expanded** card; collapsed list cards have no PLAY.

**Card width is per-surface (NOT uniform):** Home centered/active = **764**, Home side/collapsed = **677**, Full-screen = **978**. The card is one component; width is a surface parameter.

## 6.2 Canonical card tokens (single source of truth)

**Card body** — gradient `#133453`(top)->`#091B33`(bottom), rounded **50**, drop-shadow `0 10 10 rgba(0,0,0,0.4)`. Inner Pop-Up border `#0A1D35` 1px, rounded 50, top-pad 24.
- **Active/expanded border = 3px WHITE.** **Collapsed/inactive border = 3px `#3E7CA8` (blue).** This is how Figma signals active vs inactive — iter-5 does not do this.

**Back panel (full-screen only) = the `Cards Container`** — gradient `#133453`->`#091B33`, border **3px `rgba(255,255,255,0.9)`**, rounded **20**, padding **24**, **gap 24** between cards, vertical scroll. Width **1074** (48px screen margin each side). Cards sit **978 wide, inset 48** inside it. iter-5 dropped this panel entirely.

**Typography — all text is Rubik SemiBold, weight 600** (iter-5 renders a thinner weight — primary defect). Font asset `Assets/Fonts/Rubik-VariableFont_wght SDF.asset`, apply SemiBold weight.
| Element | Figma px / lineHeight / tracking | Color | Unity TMP (px/1.4) |
|---|---|---|---|
| Title | 45 / 60 / -0.69 | active `#EEDC9A`; collapsed silver gradient `#FFFFFF`->`#D1D5DB`(40%)->`#818EA1` | 32.1 |
| Tagline | 39 / 54 / -0.24 | white | 27.9 |
| Description (expanded) | 45 / 60 / -0.69 | white | 32.1 |
| ENTRY FEE / REWARDS label | 39 / 54 / -0.24 | white | 27.9 |
| Fee/reward value (x100 etc) | 39 / 54 / -0.24 | white | 27.9 |
| PLAY label | 66 / 84 / -0.78 | `#321506` | 47.1 |

**Fee/reward row layout = CENTERED CLUSTER (not corner-justified).** Each row is one horizontal group, centered in the card: `[LABEL]  <gap 32>  [coin 42px  <gap 6>  value]`. Rows stacked with **gap 24**. iter-5's left-label / right-value full-width spread is WRONG on both screens.
- Coin icon 42x42. Keep the live **RP "R" coin** asset (Figma placeholder is a plain coin; the R coin is correct — NOT a defect).
- Single-line collapsed cards (Practice/Driving Range) show one centered cluster. Two-row cards (1v1, Missions) stack ENTRY FEE/NO ENTRY FEE + REWARDS.

**Separators** = 2px line, full card width. **Active/expanded card has THREE:** under title, under description, **above PLAY**. **Collapsed cards have ONE:** under title only. (Home cards use the narrower 492-wide fee-block separators per `13027-10471`; full-screen uses 978-wide per `13026-1924` — keep per-frame, do not conflate.)

**PLAY button** — **359 x 120, centered.** Gradient `#FCF195`(0.5%)->`#D6AB42`(60%)->`#BB7F1D`(99.5%), inner border 2px `#FFE48B` rounded 20, outer Buttons Container rounded 100 border `#422100` shadow `0 4 4 rgba(0,0,0,0.25)`, top-half sheen overlay. Wrapper is **144 tall vs 120 button -> 24px bottom pad**; sits below the third separator with the content container's py-24 above it. iter-5: button too wide + zero vertical padding + jammed against REWARDS.

**Description inset (expanded)** — text column is `px-48` inside a `px-32` container => **~80px inset each side**, centered. iter-5: ~0 inset, touching card edges.

## 6.3 iter-5 -> iter-6 fix list (every item is a blocker, not polish)

**Shared (both surfaces):**
1. Thin font -> Rubik **SemiBold 600** everywhere (sizes per 6.2 table).
2. Fee/reward corner-spread -> **centered cluster** (6.2).
3. Flat title color -> active gold `#EEDC9A`, collapsed **silver gradient**.
4. Uniform border -> active **white** 3px, collapsed **`#3E7CA8`** 3px.

**Home:**
5. Collapsed card missing PLAY -> PLAY on centered card in **both** states (359x120, centered).
6. Card too tall / loose -> content-hug height; fee rows gap-24; separator above PLAY.
7. Description touching sides -> 80px inset.
8. Centered card not horizontally centered -> center card is **764** wide (sides 677); snap centers it. (Likely caused by uniform-width assumption.)
9. **Carousel scroll arrows REMOVED** (Cesar 2026-06-04 — they are set hidden in Figma deliberately; do not render them on either surface).
10. Subtitle/chevron: `1 vs 1 Match` is the MULTIPLAYER **subtitle**, NOT a selectable variant (no mode has variants). The chevron is the expand/collapse affordance only — shown on the home centered card (per `13027-10471`), hidden on the full-screen list (see item 16).

**Full-screen:**
11. No back panel -> add `Cards Container` panel (6.2): 1074w, gradient, 3px `rgba(255,255,255,0.9)`, rounded-20, pad-24, gap-24.
12. Card width wrong -> **978**, 48px inset inside the 1074 panel.
13. Locked mask oversized -> clip overlay to the **978 rounded-50** card rect (not panel, not screen).
14. PLAY no vertical padding -> separator -> py-24 -> PLAY -> 24px bottom pad (6.2).
15. Missing separator above PLAY -> add third 978-wide 2px separator.
16. Per-card side-arrow chevron shown -> **hidden** per Figma (`Arrow Container` hidden=true on list cards).
17. Fee labels dropped (Practice showed bare `Rx100`) -> keep `ENTRY FEE` / `REWARDS` labels on all cards.

## 6.4 Acceptance gates (iter-6) — per state, against the mapped frame

- Bot captures **all four states** in 6.1 (home collapsed, home expanded, full-screen one-expanded, full-screen all-collapsed). Per `feedback_prefer_bot_videos.md` — bot, not manual.
- Each capture diffed against its mapped Figma frame: title/tagline/desc/label/value/PLAY positions, sizes, weights match the 6.2 tables. Mismatch = reject.
- Active card has white border + gold title + 3 separators + PLAY; collapsed cards have blue border + silver title + 1 separator + no PLAY.
- Full-screen: back panel present (1074w), cards 978 inset-48, locked overlay clipped to card.
- No scroll arrows anywhere. No expand chevron on full-screen list cards. Home expand chevron on the centered card only.
- EditMode green; existing screens untouched.
- IMPLEMENTER_REPORT carries the per-element fidelity table (element -> node id -> x,y/w,h -> font weight + Figma px + Unity px) for BOTH states of BOTH surfaces, plus the source GUID each prefab was duplicated from (Step 0 gate still in force).

## 6.5 Resolved (Cesar 2026-06-04)
- **No mode variants exist.** `1 vs 1 Match` is the MULTIPLAYER subtitle; the chevron is the expand/collapse affordance only.
- **All-collapsed full-screen needs no canonical frame** — render every card in its collapsed form; collapsed-card metrics derive from the collapsed cards in `13026-1924`.
