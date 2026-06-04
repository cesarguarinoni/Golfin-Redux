# Brief — Reusable elements (`mode_select_system`)

For Cesar's review. Nothing here is built/changed yet — this is the inventory of what we **clone/bind** instead of rebuilding. Clone bases per Cesar: **NextHolePanel** (home carousel) + **HoleSelectionScreen** (full-screen vertical).

---

## Surface 1 — Home carousel  (clone base: `NextHolePanel`)

**Source:** `Assets/Prefabs/UI/HomeScreen.prefab` › `NextHolePanel` (also in `ShellScene.unity`). Host: `HomeScreenController.cs`.

`NextHolePanel` already contains most of a mode card — duplicate it, rename, resize children to Figma `13027-5212` (collapsed) / `13027-10471` (expanded):

| Child in NextHolePanel | Reuse as |
|---|---|
| `NextHoleTitleText ` | mode title (collapsed + expanded) |
| `CourseNameText` | tagline / subtitle |
| `Reward Row1 / Row3` + `Reward1Icon/2Icon/3Icon`, `Reward2Amount` | REWARDS row (mode card needs a single coin row — keep one, hide extras) |
| `PlayButton` + `PlayLable` | gold PLAY button (**prefab-wins**: keep size/typography; only enabled/greyed state + route change; add `ButtonPressFeedback`) |
| `PageDots` + `Dot1/Dot2/Dot3` | carousel pagination dots — **already present**, reuse for the 4-mode carousel |
| `Divider` | section divider |
| Background art `Art/HomeScreen/Next Hole Panel.png`, `Play Button.png` | card chrome |

**Banner:** `HomeScreen.prefab` › `PromoBanner` (art-backed). **Prefab-wins** — keep size/typography, only reposition **under** the carousel. (SPEC §"Prefab-wins exceptions".)

**Tee entry point:** `HomeScreen.prefab` › `BottomNavBar` › `NavTeeButton` → routes to `ScreenId.ModeSelection` (wiring already added in the salvageable `PersistentUIManager` edit).

**RP display:** `TopBar` › `RewardPointsText` / `RewardPointsIcon` — already bound to `RewardPointsManager.OnPointsChanged`. No work; reuse for fee affordability.

---

## Surface 2 — Full-screen vertical Mode Select  (clone base: `HoleSelectionScreen`)

**Clone `HoleSelectionScreenController.cs` → `ModeSelectScreenController.cs`.** Reusable surface verified:
- `ScrollRect cardsScrollRect` + `RectTransform cardsContent` + `HoleCardController cardPrefab` — the vertical scroll-list.
- `RebuildCards()` — `Instantiate(cardPrefab, cardsContent)` one per row; **single-expanded invariant**; auto-expand the first eligible card after layout settles; `HandleCardTapped` toggles Collapsed↔Expanded and ignores `Locked`.
- `[SerializeField] MatchmakingModalController matchmakingModal` — already the hook for the 1v1 launch path.
- Maps cleanly to modes: `HoleData`→mode row, `HoleProgressionService.IsUnlocked`→static `locked` CSV flag (no progression service for modes).

**Clone `HoleCardController.cs` → `ModeCardController.cs`** (backed by `Assets/Prefabs/UI/HoleSelection/HoleCard.prefab`). This is the **locked/accordion treatment the SPEC says to clone verbatim**:
- `enum HoleCardState { Collapsed, Expanded, Locked }`
- `Bind(...)` — Locked ⇒ title "LOCKED" + silver gradient.
- `SetState(...)` — `lockedOverlay.SetActive`, `chevronCollapsed/chevronExpanded`, `lockIconCollapsed`, `cardTapButton.interactable=!isLocked`, dim reward icons/amounts to **alpha 0.4**.
- `HoleCard.prefab` hierarchy: `CollapsedContainer`, `TitleArea`, `SubtitleExp`, `RewardSlot1`, `Reward1AmountExp`, `ChevronCollapsed`, `LockIconCollapsed/Expanded`, `ActionButton`, `Divider`.

> Visual note: where `HoleCard`/`NextHolePanel` chrome disagrees with the Figma Mode-card frames (`13026-1924`, `13027-*`), **Figma wins** — adjust RectTransform/typography on the clone. Don't inherit prefab metrics on faith (SPEC fidelity gate).

---

## Shared systems — bind, don't rebuild

| System | Path | Use |
|---|---|---|
| `RewardPointsManager` | `Scripts/UI/Roster/Managers/RewardPointsManager.cs` | `CanAfford(fee)` / `SpendPoints(fee)` / `GetPoints()` / `OnPointsChanged` — entry-fee economy, no new currency |
| `ToastController` | `Scripts/UI/Toast/ToastController.cs` | insufficient-RP toast ("Not enough Reward Points"); already animated |
| `ModalController` | `Scripts/UI/Modals/ModalController.cs` | `CanvasGroup` FadeIn/FadeOut idiom — the coroutine-Lerp pattern for carousel expand/collapse + matchmaking modal |
| `FadeController` | `Scripts/UI/FadeController.cs` | screen-swap fades via `ShowScreen(id, instant:false)` — never `instant=true` |
| `ScreenManager` | `Scripts/UI/ScreenManager.cs` | `ScreenId.ModeSelection` (**already added — salvageable edit**) + `_modeSelectionScreen` slot |
| `PersistentUIManager` | `Scripts/UI/PersistentUIManager.cs` | tee-button `MainPlay → ModeSelection` (**already added — salvageable**); bars shown on ModeSelection |
| `ButtonPressFeedback` | `Scripts/UI/ButtonPressFeedback.cs` (ns `Golfin.UI.Polish`) | sibling on **every** new Button (Hard Rule 11) |
| `MatchmakingModalController` | `Scripts/UI/Matchmaking/MatchmakingModalController.cs` | 1v1 PLAY launch path (already referenced by HoleSelection) |
| CSV-first loader idiom | `CharacterDatabaseCSV.cs` / `ClubDatabaseCSV.cs` | model for `ModesDatabaseCSV` reading `Resources/Data/modes.csv` |

---

## Lingering prior-session artifacts — disposition (pending Cesar)

| Artifact | Verdict |
|---|---|
| `Prefabs/UI/ModeSelect/ModeCard.prefab` | **DISCARD** — from-scratch empty boxes; rebuild from NextHolePanel/HoleCard |
| `Scripts/UI/ModeSelect/ModeCardController.cs`, `ModeCarouselController.cs`, `ModeSelectScreenController.cs`, `ModeData.cs` | **REVIEW → likely rewrite** — written against the scratch prefab, not the clone bases |
| `Scripts/UI/ModeSelect/ModesDatabaseCSV.cs` + `Resources/Data/modes.csv` | **REVIEW → likely keep** — verify columns match SPEC (id/title/tagline/description/entryFee/rewards/locked/target/order) |
| `Scripts/UI/ScreenManager.cs` edit | **KEEP** — enum + slot + bar visibility, clean |
| `Scripts/UI/PersistentUIManager.cs` edit | **KEEP** — confirm tee-button repurpose is intended |

**Open question for Cesar:** confirm DISCARD of `ModeCard.prefab` + rewrite of the 4 controllers, and whether `modes.csv`/`ModesDatabaseCSV` are kept as-is or regenerated.
