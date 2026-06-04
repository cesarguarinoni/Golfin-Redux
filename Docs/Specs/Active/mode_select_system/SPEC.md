# SPEC — Mode Select System

**Slug:** `mode_select_system`
**Tier:** FULL PIPELINE (new screens + visual fidelity + new data/economy surface).
**Status:** SPEC_READY
**Depends on:** `practice_1v1_matchmaking_split` (per-mode launch behavior). Insufficient-RP UX RESOLVED 2026-06-03 (disable PLAY + red `#C04000` fee + `ToastController`; no new Figma).
**Figma:** Home carousel `13027-5212` (collapsed) / `13027-10471` (expanded); full-screen `13026-1924` (Mode Selection Screen). File key `5gEAHjl6xAtW8iYY7NMvWd`.

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
