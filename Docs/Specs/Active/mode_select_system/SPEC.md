# SPEC — Mode Select System

**Slug:** `mode_select_system`
**Tier:** FULL PIPELINE (new screens + visual fidelity + new data/economy surface).
**Status:** SPEC_READY
**Depends on:** `practice_1v1_matchmaking_split` (per-mode launch behavior) · Figma "Insufficient Reward Points" message (to be created + Cesar-approved before the economy gate ships).
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
- **Entry fee** (assumption — confirm): on PLAY press, if (fee>0 and !CanAfford(fee)) -> show **Insufficient Reward Points** message (Figma, to create) and abort; else `SpendPoints(fee)` then route. Deduct-on-launch.
- Carousel rotates **all 4** modes (locked ones show locked, no PLAY) — assumption. The two surfaces are independent launchers (no shared "current mode" state).

---

## Acceptance gates (via loop_v2_smoke_bot framework — reusability contract)

New Scenarios.cs flows:
1. Tee button -> Mode Select renders 4 cards in `order`; Practice/1v1 enabled, Driving Range/Missions locked (dark + lock glyph, PLAY non-interactive).
2. Home carousel: swipe centers each mode; arrow expands centered card (description visible), collapses on slide; banner stays below; only centered card shows PLAY.
3. Practice PLAY -> Hole Select. 1v1 PLAY -> gameplay reached (delegated; see split spec).
4. Fee economy: Practice PLAY with balance < fee -> Insufficient-RP message, no deduction, no launch; balance >= fee -> SpendPoints called once, RP counter decrements, launch proceeds. 1v1 (fee 0) -> no deduction.
5. Existing screens untouched; EditMode green.

Human LOOK pass: both surfaces match Figma; locked treatment reads "coming soon"; expand/collapse clean.

---

## Out of scope (tracked elsewhere)
- Practice/1v1 matchmaking behavior -> `practice_1v1_matchmaking_split`.
- 1v1 in-game UI -> new roadmap item (Cesar's upcoming Figma).
- Designing the Insufficient-RP message -> created in Figma first, approved, then bound here.
