# SPEC — `screen_hints`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

**Status:** see `STATUS.md` (`SPEC_READY`, 2026-09-10, Architect / Cowork).
**Track:** Polish (Notion GOLFIN_Roadmap row 2238; deferrals 2239–2241).
**Cesar decided (2026-09-10):** first entry into each screen shows that screen's hints from the Loading tips, in a modal, one at a time; a `1/X` counter when a screen has more than one hint; once per screen; the in-game shot view is in scope with all six gameplay tips; persistence is **per device** (PlayerPrefs); modal shows image + text + counter + buttons; **a BACK button left of CONTINUE from hint 2 onwards so the player can re-read the previous tip — BACK is NOT THERE on hint 1 or on a single hint (never a disabled button); the gold button reads CLOSE on the last tip of a group and when the group has a single tip**; `TIP_RP` copy rewritten (§2.3); **backdrop = the standard dark scrim + the OPAQUE navy plate** — a translucent plate over the scrim (the loading card's 60 % fill) and a blurred-screen-instead-of-scrim option were both mocked in Figma and rejected ("looks bad if translucent on scrim"). The screen → hint table in §2.2 is the one he was shown and did not change.
**Architect defaults (Cesar did not decide these — report may flag):** (a) a screen is marked *seen* when its first hint OPENS, not when the last CLOSE is tapped (§3.4); (b) a tip key already shown on another screen is skipped everywhere else, so no tip is ever read twice (§3.3 — e.g. TIP_MISSIONS on ModeSelection then MissionSelection); (c) the modal opens one frame after the screen and only when no other modal is open (§3.5); (d) hardware back is a no-op while a hint is up (§3.6); (e) going BACK does not decrement anything — `seenKeys` stays as is.

## Goal

The 34 Loading tips (`loading_tips`, DONE `a455e0e3c`) are the game's tutorial, but a player only meets them on a loading screen, out of context. This task shows the RELEVANT tips as a modal the first time the player enters each screen — Roster explains rarities / stats / condition / level-up while the roster is on screen behind the scrim, the shot view explains the swing before the first pull. Nothing new is written: the copy, the JA, the 34 authored diagrams and the sprite table already exist; this spec binds them to screens and adds one modal, one CSV, one presenter and one PlayerPrefs blob.

## Reference

- **Figma page `Tutorial`** (new, right after `Loading`; file `5gEAHjl6xAtW8iYY7NMvWd`). Node ids in §4.
- **Renders in `reference/`** (1170×2532, the render call's own `original_width/height`):
  `figma_14263-109304_roster_hint_1of4.png` (Roster, TIP_RARITIES, counter 1/4),
  `figma_14263-109672_home_hint_1of2.png` (Home, TIP_RP, 1/2),
  `figma_14263-109883_ingame_hint_1of6.png` (shot view, TIP_SWING, 1/6),
  `figma_14266-109661_roster_hint_4of4_last.png` (Roster, TIP_LEVELUP, 4/4 — the last-hint state: BACK enabled, CLOSE).
  Hint-1 frames show CONTINUE alone (no BACK); the single-hint state (CLOSE alone, no counter) has no frame — it is the hint-1 row with the CLOSE label.
- **Provenance:** the Figma overlay is a clone of `Pop-Ups › Delete Account Pop-up` (`4081:27957` — scrim, navy plate, gold `Main Buttons`), the content is the Loading page's authored tip components (`Loading Tips — OFFICIAL`, `14218:1220`). In Unity the same provenance: the prefab is a copy of `SchemeConfirmModal.prefab` (itself a copy of `StartingCharacterConfirmModal.prefab`), the content is the loading card's own `TipContent` subtree.
- **Existing code this builds on:** `Assets/Scripts/UI/ProTipCard.cs` (`TipSprite` struct + name-keyed `tipSprites`, `Show(LoadingTip)`, `SwapRoutine`, the eased-height `_cardLayout.preferredHeight` pattern), `LoadingTipCatalog.Load(TextAsset)` / `LoadingTip { key, first, order, sprite, active }`, `LoadingTipStore` (the PlayerPrefs JSON pattern), `Assets/Scripts/UI/Modals/ModalController.cs` (`Show`/`Hide`, `animateShow`, `OpenModalCount`, `ModalStackEmptied`), `SchemeConfirmModalController` (two-scene `Instance` resolver, `SortingOrder = 600`), `TournamentResultPresenter` (the `ScreenManager.ScreenChanged` + `ModalStackEmptied` presenter shape), `GameplaySceneLoader.LoadCoroutine` step 7 (the gameplay reveal), `ControlsSubmenu.OnEnable`.

## 1. What exists today (verified in the repo 2026-09-10)

| Thing | State |
|---|---|
| Tips | `Assets/Resources/Data/LoadingTips.csv` — 34 rows, `TIP_STORE` and `TIP_REPAIR` `active=0`; text keys `TIP_*` EN+JA published (`texts` v50) with `<color=#EEDC9A>` spans; 34 sprites `Assets/Art/LoadingScreen/Tip_<KEY>.png`, wired by name on ShellScene `ProTipCard.tipSprites`. |
| Screens | `ScreenManager.ScreenId` (appended-only enum); `ScreenManager.ScreenChanged` fires at the end of every `ApplyScreen`, including GoBack and nav-bar jumps. The shot view is NOT a ScreenId — `GameplaySceneLoader` additively loads `LabScaffold` + `Hole_NN_Geo` and reveals the tee at `LoadCoroutine` step 7. Settings is an overlay (`SettingsController`), Controls is `ControlsSubmenu` inside it. |
| Modals | `ModalController` base; `ModalScrim.Apply` guarantees a full-screen scrim; `OpenModalCount` / `ModalStackEmptied` for stacking; `SchemeConfirmModal.prefab` has `animateShow: 1` (Pop + backdrop Fade) and lives twice — ShellScene (Settings canvas) and LabScaffold (`ShotUI_Canvas`, above `InGameSettingsModal`). Shot input is entirely UI event handlers (`ClubHandleDragger`, the scheme drivers, `SelectorDragRouter`), so a scrim blocks it. |
| Other first-entry logic | `GpsAuthExtrasFlow` diverts the FIRST GpsHub navigation into the Golf Profile capture (it re-enters `Navigate`, so `ScreenChanged` fires for the screen that actually opens). `TournamentResultPresenter` opens a modal on eligible screens and retries on `ModalStackEmptied`. |

## 2. Content

### 2.1 Behaviour

- On the first entry into a screen that has hints, a modal opens over the screen: scrim, `PRO TIP` title, the tip's diagram, the tip's text, a `n/X` counter top-right (only when X > 1), and a gold **CONTINUE** button centred at the bottom. No X, no backdrop dismiss.
- CONTINUE shows the next hint (counter increments, content cross-fades). On the LAST hint of the group the gold button reads **CLOSE** and closes the modal. A group of ONE hint shows CLOSE alone with no counter.
- From hint 2 onwards a silver **BACK** button appears LEFT of the gold one and shows the previous hint (counter decrements, same cross-fade). On hint 1 and on a single hint BACK is **absent — not disabled, not there** (Cesar 2026-09-10); the gold button re-centres. BACK from hint 2 lands on hint 1 with CONTINUE alone, never CLOSE.
- Button states, for the implementer: `n == 1 && X == 1` → [CLOSE]; `n == 1 && X > 1` → [CONTINUE]; `1 < n < X` → [BACK][CONTINUE]; `n == X > 1` → [BACK][CLOSE]. The gold button is ONE `Button` whose label swaps between `HINT_CONTINUE` and `HINT_CLOSE`; BACK is `SetActive(n > 1)`. Both changes happen on the rebind inside the cross-fade so nothing flips on a visible row.
- The screen is then never hinted again on this device (§3.4). Every other entry into the screen is unchanged.
- Hints whose tip is `active=0` in `LoadingTips.csv` are skipped and never counted (`Inventory` is `1/2` today, not `1/3`; `GeneralShop` shows TIP_GACHA alone with no counter). When `TIP_STORE` / `TIP_REPAIR` flip to active (Notion 2227) the counts grow with no change here.
- A tip key that has already been shown by an earlier screen's hints is skipped (Architect default b). If that leaves a screen with zero hints, no modal opens and the screen is still marked seen.
- Screens with no row in §2.2 never open a modal: Logo, Splash, Loading, the auth gate, StartingCharacterSelection (shares RosterScreen — Roster's hints wait for the first REAL Roster entry), GachaHistory, StoreHistory, ScoreUpload, GpsProfile/Avatar/Badges, GpsGolfProfile/GpsWelcome, StaminaShopDetail, TournamentHoleSelection.

### 2.2 Screen → hints (the order is the `1/X` order)

| screen id | hints (in order) |
|---|---|
| `Home` | TIP_RP, TIP_DAILY |
| `Roster` | TIP_RARITIES, TIP_STATS, TIP_CONDITION, TIP_LEVELUP |
| `Inventory` | TIP_CLUBSTATS, TIP_BALLS, TIP_REPAIR* |
| `ModeSelection` | TIP_MISSIONS, TIP_TOURNAMENT, TIP_VERSUS |
| `HoleSelection` | TIP_AUTOCLUB, TIP_SURFACES |
| `MissionSelection` | TIP_MISSIONS, TIP_DAILY |
| `TournamentSelection` | TIP_TOURNAMENT |
| `Leaderboard` | TIP_LEADERBOARD |
| `TournamentLeaderboard` | TIP_LEADERBOARD |
| `StaminaShopSelection` | TIP_CONDITION |
| `GeneralShop` | TIP_GACHA, TIP_STORE* |
| `GachaPrizes` | TIP_GACHA |
| `GpsHub` | TIP_GPS_WALLET, TIP_GPS_CHECKIN, TIP_GPS_SOCIAL |
| `GpsRounds` | TIP_GPS_CHECKIN |
| `GpsGift` | TIP_GPS_SOCIAL |
| `GpsVote` | TIP_GPS_SOCIAL |
| `Gameplay` (the shot view, first hole of ANY mode) | TIP_SWING, TIP_ACCURACY, TIP_GRADES, TIP_VIEW, TIP_CLUB, TIP_FORECAST |
| `SettingsControls` (Settings › Controls submenu) | TIP_CONTROLS |

\* `active=0` today — skipped, not counted (§2.1). Duplicate keys across rows (LEADERBOARD, GPS_SOCIAL, MISSIONS, DAILY, TOURNAMENT, GACHA, CONDITION, GPS_CHECKIN) are intentional: whichever screen the player reaches first shows the tip, the other skips it (default b).

`Gameplay` and `SettingsControls` are the two ids that are not `ScreenId` members; `ScreenHintCatalog` reserves them as constants.

### 2.3 Strings — through the two-way importer (standing rule 2026-08-28)

| key | EN | JA | note |
|---|---|---|---|
| `HINT_CONTINUE` (new) | CONTINUE | 続ける | gold button, not the last hint. A NEW key, not `TOURN_CTA_CONTINUE` — the tournament CTA can be reworded without touching the tutorial. |
| `HINT_CLOSE` (new) | CLOSE | 閉じる | gold button on the last hint / a single hint. Own key for the same reason (`SETTINGS_CLOSE` is Settings'). |
| `HINT_BACK` (new) | BACK | 戻る | silver button. Own key (`INGAME_BACK`, `GACHA_BACK`, `ROSTER_STARTER_BACK` are their screens'). |
| `TIP_HEADER` (existing) | PRO TIP | プロのヒント | title — reused unchanged |
| `TIP_RP` (existing — **REWRITE**, Cesar 2026-09-10) | `<color=#EEDC9A>REWARD POINTS</color> ARE THE GAME'S CURRENCY. EARN THEM ON EVERY HOLE, MISSION AND ROUND` | `<color=#EEDC9A>リワードポイント</color>がゲームの通貨。ホール・ミッション・ラウンドで稼ごう` | "ONLY" and "THEY ARE NEVER FOR SALE" are gone — RP may be sold later. Same row feeds the loading screen AND this modal, so the loading tip changes with it (Cesar: "change it in Loading screen as well"). Figma already updated on both pages (§4). |
| `TIP_*` ×33 others (existing) | — | — | body — reused unchanged, `<color=#EEDC9A>` spans included |

The counter is `{shown}/{total}` in ASCII digits with no key (numbers are not localised anywhere in the game). **Retired keys: none.** NOTE for the reviewer: `Docs/Economy/ECONOMY_MASTER.md` still says RP are never purchasable — the tip no longer promises it; the doc is Cesar's to amend, not this task's.

Path: add the `HINT_CONTINUE`, `HINT_CLOSE`, `HINT_BACK` rows and rewrite the `TIP_RP` row (EN + JA, `true`) in `Assets/Localization/LocalizationText.csv` → `python3 Tools/content/import_content.py --env-file … --catalogs texts` (PLAN, read the verdicts) → `--apply` → publish `texts` from the admin → `export_content.py --check` clean. CONFLICTS = stop and report. `LocalizationTextTable.asset` regenerates on build. Acceptance line: `--check` clean + zero new hardcoded `.text` literals (grep quoted in the report).

## 3. Design

### 3.1 Data — `Assets/Resources/Data/ScreenHints.csv` (new, bundled, client-only)

```
# screen_hints §3.1 — which Loading tips open as a modal on the FIRST entry into each screen.
# screen: a ScreenManager.ScreenId name, or Gameplay / SettingsControls (ScreenHintCatalog constants).
# order: the n/X order within the screen. key: a LoadingTips.csv key — tip text, sprite and
# active flag all come from there; a key that is active=0 is skipped and not counted.
screen,order,key
Home,1,TIP_RP
Home,2,TIP_DAILY
Roster,1,TIP_RARITIES
Roster,2,TIP_STATS
Roster,3,TIP_CONDITION
Roster,4,TIP_LEVELUP
Inventory,1,TIP_CLUBSTATS
Inventory,2,TIP_BALLS
Inventory,3,TIP_REPAIR
ModeSelection,1,TIP_MISSIONS
ModeSelection,2,TIP_TOURNAMENT
ModeSelection,3,TIP_VERSUS
HoleSelection,1,TIP_AUTOCLUB
HoleSelection,2,TIP_SURFACES
MissionSelection,1,TIP_MISSIONS
MissionSelection,2,TIP_DAILY
TournamentSelection,1,TIP_TOURNAMENT
Leaderboard,1,TIP_LEADERBOARD
TournamentLeaderboard,1,TIP_LEADERBOARD
StaminaShopSelection,1,TIP_CONDITION
GeneralShop,1,TIP_GACHA
GeneralShop,2,TIP_STORE
GachaPrizes,1,TIP_GACHA
GpsHub,1,TIP_GPS_WALLET
GpsHub,2,TIP_GPS_CHECKIN
GpsHub,3,TIP_GPS_SOCIAL
GpsRounds,1,TIP_GPS_CHECKIN
GpsGift,1,TIP_GPS_SOCIAL
GpsVote,1,TIP_GPS_SOCIAL
Gameplay,1,TIP_SWING
Gameplay,2,TIP_ACCURACY
Gameplay,3,TIP_GRADES
Gameplay,4,TIP_VIEW
Gameplay,5,TIP_CLUB
Gameplay,6,TIP_FORECAST
SettingsControls,1,TIP_CONTROLS
```

- Parsed by `ScreenHintCatalog` (`Assets/Scripts/UI/Hints/ScreenHintCatalog.cs`, `Assembly-CSharp`, `namespace GolfinRedux.UI`): `static IReadOnlyList<ScreenHint> Load(TextAsset csv)`; `#` lines skipped exactly as `LoadingTipCatalog` does; a malformed row logs one warning and is dropped. `[Serializable] struct ScreenHint { string screen; int order; string key; }`. Constants `GameplayScreen = "Gameplay"`, `SettingsControlsScreen = "SettingsControls"`; `static string IdFor(ScreenId id) => id.ToString()`.
- Not a content catalog (same reasoning as `LoadingTips.csv` §3.1 of `loading_tips`): the tip text is admin-editable through `texts`; the mapping is bundled. Registering it is the deferred row (Notion 2239).

### 3.2 Resolution — `ScreenHintResolver` (pure, testable)

`Assets/Scripts/UI/Hints/ScreenHintResolver.cs`, no Unity dependencies:

```csharp
public static class ScreenHintResolver
{
    /// The tips to show for `screen`, in order — or empty. Filters: rows for this screen,
    /// sorted by order; drops keys with no LoadingTips row, rows with active=0, and keys in
    /// `seenKeys` (default b). Returns the LoadingTip rows so the modal has sprite + key.
    public static List<LoadingTip> HintsFor(string screen,
        IReadOnlyList<ScreenHint> hints, IReadOnlyList<LoadingTip> tips, ScreenHintState state);
}
```

`state.seenScreens.Contains(screen)` → empty, before anything else.

### 3.3 The modal — `ScreenHintModalController` : `ModalController`

**Prefab (Rule 19 clone provenance).** `Assets/Prefabs/UI/Modals/ScreenHintModal.prefab` = `AssetDatabase.CopyAsset(SchemeConfirmModal.prefab, …)` by an Editor builder `Assets/Scripts/UI/Hints/Editor/ScreenHintModalBuilder.cs` (mirror `SchemeConfirmModalBuilder`'s shape — copy, delete, add, wire, save; re-runnable). Kept from the copy: `DimBackground`, `Panel` (navy plate, 1086 wide), `TitleRow/TitleText`, `SeparatorRow/ModalSeparator`, the button row with **both** buttons — `CancelButton` (silver `ButtonCancel` sprite → becomes BACK) and `ConfirmButton` (gold `Button - Retry` sprite → becomes CONTINUE / CLOSE) — with their `ButtonPressFeedback`, `animateShow: 1`, `SortingOrder 600`. Deleted: `StepsRow`, `HowItWorksRow`, `FooterRow`, and the `SchemeConfirmModalController` component (replaced by the new one). Added:

- `Counter` — TMP, cloned from `TitleText` (same font asset/material, gold), size per §4, right-aligned, anchored top-right of `Panel` (64 px in from the right edge, 36 px down — §4), `ignoreLayout`. Inactive when X == 1.
- `TipContent` — **copy the ShellScene `ProTipCard/TipContent` subtree** (`TipText` + `TipImage`, its `CanvasGroup`, the TMP font/material, `LocalizedText` on `TipText`) into `Panel` between the separator and the button row. This is the loading card's own content, so the two surfaces cannot drift. Width per §4 (text 990 in a 1086 panel), image 806 wide `preserveAspect`, centred, `LayoutElement` so the `VerticalLayoutGroup` + `ContentSizeFitter` on `Panel` size the plate to its content exactly as `ProTipCard` sizes the card.
- `CancelButton` → renamed `BackButton`, label key `HINT_BACK`. `ConfirmButton` → renamed `NextButton`, label a `LocalizedText` whose key is set at rebind to `HINT_CONTINUE` or `HINT_CLOSE` (§2.1 state table). The row keeps the copy's `HorizontalLayoutGroup` (450 + gap + 450, 120 tall, child alignment MiddleCenter); `BackButton` is `SetActive(false)` on hint 1 and on single hints, and the layout then centres `NextButton` alone — no second layout, no disabled state anywhere.
- `[SerializeField] ProTipCard.TipSprite[] tipSprites` on the controller — 34 entries, name = `Tip_<KEY>`, wired on the PREFAB (so both scene instances share one table; `ProTipCard.TipSprite` is already `public` — verified 2026-09-10 — so no `ProTipCard` edit). NOTE: `LoadingTipCatalogTests` already pins `LoadingTips.csv` ↔ the 34 PNGs ↔ ShellScene `ProTipCard` to each other; extend it to the prefab's table (§Acceptance).

**Two instances, one prefab — exactly `SchemeConfirmModal`'s arrangement.** ShellScene: sibling of `SchemeConfirmModal` under the Settings canvas (it must sit above the Settings overlay for `SettingsControls`, and `SortingOrder 600` already does). LabScaffold: under `ShotUI_Canvas`, sibling of the gameplay `SchemeConfirmModal`, above `InGameSettingsModal`. `Instance` = a copy of `SchemeConfirmModalController.Instance` (non-ShellScene wins when both are loaded).

**API.**

```csharp
public void Show(IReadOnlyList<LoadingTip> hints, Action onFinished);   // hints.Count >= 1
```

`Show` binds hint 0 via one `Bind(int n)` (title `TIP_HEADER` via `LocalizedText`, text `LocalizedText.SetKey(tip.key)` with the raw-key fallback `ProTipCard.Show` uses, sprite by name → `TipImage` active/inactive, counter `n/X` or hidden, button states per §2.1), then `base.Show()` — Pop + backdrop Fade come from `animateShow`. `NextButton` → if `n < X` cross-fade to `Bind(n + 1)` (§3.3a); else `Hide()` then `onFinished()`. `BackButton` → cross-fade to `Bind(n − 1)` (never reachable at `n == 1`: the button is inactive). `Hide()` while mid-sequence (force-disable, scene teardown) → `OnDisable` stops motion; `onFinished` is NOT called (the screen is already marked seen, default a — nothing is owed). Every rebind happens inside the fade-out → fade-in seam, buttons included, so a label or BACK's presence never flips on a fully visible row.

### 3.3a Polish atoms (Cesar 2026-09-09/10 — every UI spec names them)

- `ModalController` with `animateShow` — Pop in / Unpop out + backdrop Fade, inherited from the copy. No new open/close motion.
- `Golfin.UI.Polish.ButtonPressFeedback` (`Assets/Scripts/UI/ButtonPressFeedback.cs`) on both `BackButton` and `NextButton` — inherited from the copy's two buttons (`SchemeConfirmModal.prefab` carries two); `PressFeedbackCoverageTests` audits prefabs under `Assets/Prefabs/UI/Modals/` and will see them (say so in the report).
- Hint → hint (either direction) = **`ProTipCard.SwapRoutine`'s shape, not a new one**: one `UiMotion.Fade(tipContentGroup, a, 0)` → rebind text + sprite + counter + buttons → `UiMotion.Fade(…, 0, 1)` as ONE routine held in `Coroutine? _swap` via `UiMotion.Run(this, ref _swap, …)`; the loading card's double-advance fix (`Then` tail re-entering `Run` on the same handle — see `ProTipCard` §"SwapRoutine" comment) applies verbatim: one routine yielding both sweeps, never `Then` into another `Run`. Plate height eases with the same `_cardLayout.preferredHeight` tween `ProTipCard` uses (`UiMotion.Tween(from, to, UiMotion.EntryDur, …)` then `preferredHeight = -1` to hand the height back to the fitter) — copy that code path, quote the before/after heights of one swap in the report.
- Counter change = `UiMotion.Bump(counterRect)` on the counter text when it changes in either direction (Bump is the "just changed" atom; no invented pulse).
- `UiMotion.Stop(this, ref _swap)` / `ref _height` in `OnDisable` (call base first — the `OpenModalCount` leak guard). `UiMotion.Enabled` kill switch honoured for free.
- No `PendingSpend` (no server call), no `ShimmerHost` (nothing is fetched), no `UiSelection` (nothing is selected), no `StaggerRise` (no list). Neither button is ever disabled; BACK is present or absent.
- Taps during a swap: both buttons ignore input while `_swap` is running (a guard bool, not `interactable` — toggling `interactable` for 0.3 s would flash the disabled look); the loading card's one-tap-one-advance lesson (Notion 2229) applies here too — the report proves one tap == one step with the `[Modal]`/bind log.

### 3.4 Persistence — `ScreenHintStore`

`Assets/Scripts/UI/Hints/ScreenHintStore.cs`, PlayerPrefs key `screenhints.state`, `JsonUtility`, mirror of `LoadingTipStore` (`Load` / `Save` / `Clear`, corrupt → default). `[Serializable] struct ScreenHintState { string[] seenScreens; string[] seenKeys; }`. A screen is added to `seenScreens` **when its first hint opens** (default a); each key is added to `seenKeys` when it is shown. A reinstall restarts the tutorial — that is the point of per-device (Cesar 2026-09-10). Editor menu `GOLFIN ▸ Hints ▸ Reset seen` → `ScreenHintStore.Clear()` (next to whatever menu `LoadingTipStore.Clear` has; add both if neither exists).

### 3.5 Presenter — `ScreenHintPresenter`

`Assets/Scripts/UI/Hints/ScreenHintPresenter.cs`, a MonoBehaviour on ShellScene `PersistentUI` (where `TournamentResultPresenter` lives), `[SerializeField] TextAsset hintsCsv` (drag `ScreenHints.csv`) + `[SerializeField] TextAsset tipsCsv` (drag `LoadingTips.csv`). Loads both catalogs and the store once in `Awake`.

- `OnEnable`: `ScreenManager.ScreenChanged += OnScreenChanged`; `OnDisable` unsubscribes. `OnScreenChanged(id)` → `Request(ScreenHintCatalog.IdFor(id))`.
- `public static void NotifyScreenEntered(string screen)` — the two non-ScreenId entry points call this: **`GameplaySceneLoader.LoadCoroutine` step 7**, immediately after `loadingScreen.FinishLoadingCoroutine()` and next to `TeeIdleGlowController.NotifyOtherInteraction()` (so the hint opens over the revealed tee, never behind the loading screen), with `ScreenHintCatalog.GameplayScreen`; and **`ControlsSubmenu.OnEnable`** with `SettingsControlsScreen`. Null-safe static (no presenter → no-op), so the physics-lab / bot launchers that never run the loader are untouched.
- `Request(screen)`: `hints = ScreenHintResolver.HintsFor(...)`; if the screen is unseen, mark it seen + save NOW (default a) even when `hints` is empty; if empty → return. Otherwise start `PresentWhenClear(screen, hints)`: `yield return null` (one frame — anything that opens a modal on the same screen change, e.g. `TournamentResultPresenter`, gets there first), then while `ModalController.OpenModalCount > 0` wait for `ModalStackEmptied` (subscribe, yield, unsubscribe — bounded by nothing on purpose: a modal the player is reading is a modal the player is reading). If a DIFFERENT screen id arrives while waiting, cancel (the screen stays marked seen — it was entered). Then `ScreenHintModalController.Instance?.Show(hints, onFinished: () => { if (screen == GameplayScreen) TeeIdleGlowController.NotifyOtherInteraction(); })` — the tee-idle countdown restarts when the player can actually see the tee. `Instance == null` → log a warning, nothing else (a scene without the prefab must never strand the player).
- Only one presentation at a time; a second `Request` during one is dropped after its seen-marking (its screen was entered; the player will get that screen's hints… never — accepted: it can only happen if two screens change during one modal, which `Navigate` does not do).

### 3.6 Input and stacking

- The scrim (`ModalScrim.Apply`) blocks every UI handler under it, which is all shot input; NOTE: the aim-camera drag — if `camera_drag_touch_origin` reads raw touches rather than UI events, gate it on `ModalController.OpenModalCount == 0` (one line) and say so in the report; if it is a UI handler already, say that instead.
- Hardware / gesture back: `ScreenManager.Update` returns while `OpenModalCount > 0` ("modals own their own dismissal"); this modal has no `ModalBackdropDismiss` and no close button, so back does nothing until CONTINUE / CLOSE (default d).
- A modal opened by another system while a hint is up stacks on top (ModalController `SetAsLastSibling`); the hint resumes underneath. Not prevented.
- 1v1 / tournament: the hint shows on the first hole regardless of mode. NOTE: if the 1v1 turn clock (`1v1_match_flow`) runs during the shot view's idle, it runs under the modal — report whether it does; if it does, pausing it is Notion 2241, not this task.

## 4. Fidelity table (Figma `Tutorial` page → Unity, canvas 1170×2532 origin centre)

| Element | Figma node | Figma value | Unity |
|---|---|---|---|
| Overlay / scrim | `14263:39325` (`Hint Modal — overlay`) | 1170×2532, black 50 % | `DimBackground` from the copy (black α 0.92 in the prefab) — `ModalScrim.Apply` enforces `MinAlpha 0.80` in linear space anyway (its own comment: 0.50 reads undimmed), so the Figma 50 % is a sketch value; keep the copy's |
| Plate | `14263:39326` (`Pop-up`) | x 42, **1086 wide**, r 50, **opaque** navy vertical gradient `#132F53 → #091B33`, silver gradient stroke 3, drop shadow 0/10 blur 20 40 % | the copy's `Background` Image (`Background - HoleCard.png`, sliced, white α 1) under `Panel` — unchanged; NOT the loading card's translucent `Tip Card Background.png` (rejected, see header); width 1086, height = content |
| Plate height / y | Roster 1/4 `14263:109627` · Home · In-game · Roster 4/4 `14266:109664` | 1158 / 900 / 1024 / 1010 tall, vertically centred (y = (2532 − h)/2) | `ContentSizeFitter` vertical; `Panel` anchored centre → centred for free |
| Column | `14263:39327` (`Pop-Up`) | vertical, gap 24, padding-bottom 32 | `VerticalLayoutGroup` on `Panel`: spacing 24, bottom 32 |
| Title | `14263:39330` | `PRO TIP`, Rubik SemiBold 66 / lh 84, `#EEDC9A`, centred, row 120 tall (pad 24 top) | `TitleText` from the copy (gold 66 already); key `TIP_HEADER` |
| Counter | `14263:109274` | `1/4`, Rubik SemiBold 45, `#EEDC9A`, right-aligned; right edge 64 px in from the plate's right, top 36 px from the plate's top; hidden when X = 1 | new `Counter` TMP, anchor top-right, anchoredPosition (−64, −36), pivot (1, 1) |
| Separator | `14263:39331` | 978 wide, stroke 2, at y 120 | `ModalSeparator` from the copy |
| Body text | `14263:39335` | Rubik SemiBold 51 / lh 66, white + `#EEDC9A` spans, centred, **990 wide** (48 px side padding), 12 px top padding | `TipText` copied from the loading card (same style); `LayoutElement.preferredWidth 990` |
| Diagram | `14263:109279` (Roster) · `14263:109863` (Home) · `14263:110032` (In-game) | 806 wide, height per sprite (628 / 370 / 560), centred, 24 above and below | `TipImage` copied from the loading card, `preserveAspect`, width 806 |
| Button row | `14263:39343` (hint 1: CONTINUE alone, centred) · `14266:109702` (last hint: BACK + CLOSE, gap 48) | centred, 120 tall | the copy's button row (`HorizontalLayoutGroup`, both buttons kept; BACK `SetActive(n > 1)`) |
| BACK | `14267:32682` (last-hint frame, `Silver, Enabled=Yes`) | 428×120, label 66; not present on hint-1 frames | `BackButton` = the copy's silver `CancelButton`, **450×120** (the Unity Main Button width — keep it; 428 is Figma's hug), key `HINT_BACK`; inactive on hint 1 and when X = 1 |
| CONTINUE / CLOSE | `14263:39346` (CONTINUE) · `14266:109704` (CLOSE) | `Main Buttons / Gold, Enabled=Yes`, 428×120, label 66 | `NextButton` = the copy's gold `ConfirmButton`, 450×120, key `HINT_CONTINUE` / `HINT_CLOSE` per §2.1 |

Motion is not in Figma: §3.3a.

## Architecture context

- **Asmdef:** `Assembly-CSharp` only (`ScreenManager`, `ModalController`, `ProTipCard`, `LoadingTipCatalog`, `GameplaySceneLoader`, `ControlsSubmenu` all live there). New folder `Assets/Scripts/UI/Hints/` (+ `Editor/`). No new asmdef; no gameplay-assembly change (the loader call is one static line in `Assembly-CSharp`).
- **Polish atoms:** `ModalController` (`animateShow` Pop/Unpop + backdrop Fade), `ButtonPressFeedback` on BACK and CONTINUE/CLOSE, `UiMotion.Fade` swap + `UiMotion.Tween` height (the `ProTipCard` routines, copied), `UiMotion.Bump` on the counter, `UiMotion.Stop` in `OnDisable`. Not used, and why: §3.3a last line.
- **Not touched:** `ScreenManager` (subscribe only), `ProTipCard` (read its public `TipSprite` type and copy its swap — no edit), `LoadingTips.csv`, the 34 PNGs, `SchemeConfirmModal.prefab`, the admin dashboard, `ContentCatalogs`.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] `ScreenHintCatalogTests` (EditMode): the shipped CSV parses to 36 rows / 18 screens; every `screen` is a `ScreenId` name or one of the two constants; every `key` exists in `LoadingTips.csv`; `(screen, order)` unique and contiguous from 1 per screen.
- [ ] `ScreenHintResolverTests`: `Inventory` → 2 hints (TIP_REPAIR inactive dropped); `GeneralShop` → 1; a seen screen → 0; `MissionSelection` after `ModeSelection`'s hints were shown → TIP_DAILY only (default b); flipping `TIP_STORE` active in a fixture → `GeneralShop` → 2.
- [ ] `ScreenHintStoreTests`: save → load round-trips both arrays; corrupt blob → default.
- [ ] `LoadingTipCatalogTests` extended: the prefab's `tipSprites` has 34 entries matching the CSV `sprite` cells (same assertion the ShellScene `ProTipCard` already gets).
- [ ] `PressFeedbackCoverageTests` green with the new prefab; `ModalPopTests` / `GpsScreenTransitionTests` green (`animateShow` default still false on the class; `1` on this prefab like `SchemeConfirmModal`).
- [ ] Editor, cleared `PlayerPrefs` (`GOLFIN ▸ Hints ▸ Reset seen`): boot → Home shows `1/2` TIP_RP with CONTINUE alone (no BACK) → CONTINUE → `2/2` TIP_DAILY with BACK + CLOSE → BACK → `1/2` again (CONTINUE alone, no BACK) → CONTINUE → CLOSE closes; second visit to Home shows nothing; `screenhints.state` quoted after each step.
- [ ] Roster: `1/4 … 4/4`; the plate height eases between RARITIES (628 px image) and STATS (frame strip every 2 frames, before/after heights quoted); the counter bumps in both directions; the gold label only ever changes inside the fade seam (frame strip of the 3/4 → 4/4 swap).
- [ ] Inventory shows `1/2`, `2/2` (not `/3`); GeneralShop shows TIP_GACHA with CLOSE alone, NO BACK, NO counter.
- [ ] One tap == one step in both directions (log quoted); taps during a swap are ignored.
- [ ] MissionSelection entered after ModeSelection shows TIP_DAILY only (or nothing if Home already showed it) — default b in action, screenshot.
- [ ] Hole load (practice, Lomond hole 1): the modal opens over the revealed tee, not over the loading screen; `1/6 … 6/6`; no pull possible while it is up (drag on the cone does nothing); after `6/6` CONTINUE the tee-idle glow countdown starts from zero (`TeeIdleGlowController` timer quoted). Second hole: nothing.
- [ ] Settings › Controls first open: `TIP_CONTROLS`, no counter, above the Settings overlay; second open: nothing.
- [ ] Stacking: with a tournament result pending on Home (`TournamentResultPresenter` fixture), the result modal opens first and the hint opens after it closes — order quoted from the `[Modal]` log lines.
- [ ] JA device language: title, body and CONTINUE render Japanese on two screens (screenshots).
- [ ] `export_content.py --check` clean for `texts`; zero new hardcoded `.text` literals (grep quoted); `HINT_CONTINUE` / `HINT_CLOSE` / `HINT_BACK` published; `TIP_RP` rewritten EN + JA and visible with the new copy on BOTH the loading screen and the Home hint (screenshot each).
- [ ] Unity Console clean through boot → Home → Roster → hole load → Settings › Controls.
- [ ] Spec deviations flagged at the bottom of the report (incl. the §3.6 camera-drag and 1v1-clock NOTEs).

## Out of scope (filed as Notion `Deferred` rows by the Architect)

- `ScreenHints.csv` as a server content catalog + admin panel (Notion 2239).
- A "Replay tutorial" toggle in Settings that clears `screenhints.state` (Notion 2240).
- Per-scheme gameplay hints (Pendulum / Tap Timing / Free Swing players get TIP_SWING for Flick today) and pausing the 1v1 clock under the hint (Notion 2241).
- Hints that mark the loading-tip pools as "seen" (the two systems keep separate state on purpose).

## Files / hierarchy this task touches

- `Assets/Resources/Data/ScreenHints.csv` (new)
- `Assets/Scripts/UI/Hints/ScreenHintCatalog.cs`, `ScreenHintResolver.cs`, `ScreenHintStore.cs`, `ScreenHintPresenter.cs`, `ScreenHintModalController.cs` (new); `Editor/ScreenHintModalBuilder.cs`, `Editor/ScreenHintMenu.cs` (new)
- `Assets/Prefabs/UI/Modals/ScreenHintModal.prefab` (new, copy of `SchemeConfirmModal.prefab`)
- `Assets/Scenes/ShellScene.unity` (prefab instance under the Settings canvas; `ScreenHintPresenter` on `PersistentUI`), `Assets/Scenes/LabScaffold.unity` (prefab instance under `ShotUI_Canvas`)
- `Assets/Scripts/UI/GameplayTransition/GameplaySceneLoader.cs` (one line at step 7), `Assets/Scripts/UI/ControlsSubmenu.cs` (one line in `OnEnable`)
- `Assets/Tests/EditMode/ScreenHintCatalogTests.cs`, `ScreenHintResolverTests.cs`, `ScreenHintStoreTests.cs` (new); `LoadingTipCatalogTests.cs` (extend)
- `Assets/Localization/LocalizationText.csv` (`HINT_CONTINUE`, `HINT_CLOSE`, `HINT_BACK` new; `TIP_RP` rewritten)
