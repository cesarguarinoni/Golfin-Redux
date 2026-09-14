# Quick spec — `scroll_lists_drag_anywhere`

**Filed:** 2026-09-14, Cesar, after `rankings_list_drag_anywhere` surfaced the shape in thirteen
more lists: *"Do the other 13."* (The audit table had fourteen rows — the prose count in the
rankings spec was one short; all fourteen are here.)

## The shape

A `ScrollRect` only receives a drag when the EventSystem raycast lands on a graphic **inside its
subtree** — `GetEventHandler<IDragHandler>` bubbles up from the top hit. Every list below had no
enabled raycast graphic on the scroll rect or its viewport, so a press on empty list space (the gap
between rows, a row's dark body, the space under the last row) hit the panel *behind* the list and
nothing scrolled. Four are literal clones of the rankings block (viewport `Image` present but
**disabled**); ten have a `RectMask2D`-only viewport with no `Image` at all.

## Fix — one rule, fourteen sites (PATTERNS §13)

The viewport carries an enabled `Image`, `raycastTarget = 1`, colour `(1,1,1,0)`. With
`CanvasRenderer.cullTransparentMesh` (the default, and confirmed `true` on every site) nothing is
drawn; `Graphic.Raycast` ignores colour. Children — rows, buttons, input fields, scrollbars — draw
after the viewport, so nothing that was tappable lost its tap; an `InputField` still owns its own
drag (it is an `IDragHandler` closer to the press).

| Site | Change |
|---|---|
| `GeneralShopScreen`, `StaminaShopSelectionScreen`, `TournamentSelectionScreen` prefabs — `…/Modal/Bottom97/ScrollArea/Viewport` | existing `Image`: `m_Enabled 0→1`, alpha `1→0` (2 lines each) |
| `ShellScene` — `TournamentLeaderboardScreen/…/Bottom97/ScrollArea/Viewport` | same 2 lines |
| `LoginScreen`, `SignUpScreen`, `CreateUsernameScreen` prefabs — `CardBorder/CardBody/ScrollView/Viewport` | `Image` + `CanvasRenderer` added (40 lines each) |
| `ShellScene` — `ResetPasswordScreen/…/ScrollView/Viewport`, `ItemUseModal/…/ScrollArea/Viewport`, `BagsClubModal/…/ScrollArea/Viewport` | `Image` + `CanvasRenderer` added |
| `GachaRatesModal` prefab — `Panel/Content/BodyScroll/Viewport` | `Image` + `CanvasRenderer` added |
| `GpsVoteScreen` — `ContentContainer/VoteList`; `VenuePickerModal` — `ModalPanel/List`; `LoanModal` — `ModalPanel/RecipientScroll` | the `ScrollRect` object IS its own viewport: `Image` + `CanvasRenderer` added there |

All through `SerializedObject` on `PrefabUtility.LoadPrefabContents` / `Undo.AddComponent` (trap C1).
The scene half took two attempts: the first `SaveScene` on the day's play-worn Editor baked 2 756
lines of anchor churn (`m_AnchorMin.y 1→0`, `m_AnchoredPosition.x 520.5→0` on prefab-instance
overrides — the known [scene-save churn]); a freshly `OpenScene`d copy did the same, so the eight
hunks that are this change were isolated from the diff and applied to HEAD (`+122 −2`), and the
Editor's copy reloaded from disk. `TournamentSelectionScreen.prefab` also carries another session's
uncommitted `_holeSelectionTarget` / `_leaderboardTarget` renumbering — only the two viewport lines
are staged from it.

## Evidence — `ScrollDragAnywhereVerify` sweep (JSON is the gate)

One real boot per run → title `StartButton` → each list through its real entry (column 2; where a
widget needs state the run cannot reach, the `ScreenManager` / controller call that widget itself
makes, and the JSON says so) → first-visit hints closed through their real CONTINUE / CLOSE → the
finder proves an empty spot inside the viewport and inside no child → `EventSystem.RaycastAll`
there → drag dispatched only to what the raycast resolved → `content.anchoredPosition` measured.

`media/scroll_lists_drag_anywhere/drag_anywhere_baseline*.json` (HEAD, three runs while the
openers were being taught the shop's STORE tab, the nav bar's pillar memory and the bag modal's
empty-slot card) vs `drag_anywhere_after.json` + `drag_anywhere_after_loan.json` (fixed):

| List | Reached through | Empty spot pressed | HEAD: top hit → drag handler | AFTER: top hit → handler | Δcontent (px) |
|---|---|---|---|---|---|
| Rankings | Home ▸ LeaderboardButton.onClick | gap between the first two rows | fixed in `53baff298` — HEAD evidence in `rankings_list_drag_anywhere` | `…/Bottom97/ScrollArea/Viewport` → ScrollRect ✓ | 968 |
| General Shop (STORE tab) | Rewards Center ▸ STORE tab.onClick | gap between the first two rows | `GeneralShopScreen/ContentArea/BarsArea/RankingsArea` → **none** | `…/Bottom97/ScrollArea/Viewport` → ScrollRect ✓ | 966 |
| Stamina shop | Roster ▸ BoostButton.onClick | gap between the first two rows | `StaminaShopSelectionScreen/SafeArea/CardsPanel` → **none** | `…/Bottom97/ScrollArea/Viewport` → ScrollRect ✓ | 968 |
| Tournament selection | `ShowScreen(TournamentSelection)` — the mode-carousel card's call | gap between the first two rows | `TournamentSelectionScreen/…/RankingsArea` → **none** | `…/Bottom97/ScrollArea/Viewport` → ScrollRect ✓ | 0 — 3 cards, too short to scroll |
| Tournament leaderboard | `ShowScreen(TournamentLeaderboard)` | gap between the first two rows | `…/ContentArea/BarsArea/RankingsArea` → **none** | `…/Bottom97/ScrollArea/Viewport` → ScrollRect ✓ | 553 |
| Gacha rates modal | GachaBannerCard ▸ RULES button.onClick | empty list, viewport centre | `GachaRatesModal/Panel/Background` → **none** | `…/BodyScroll/Viewport` → ScrollRect ✓ | 0 — body shorter than the viewport |
| Loan modal (recipients) | Roster ▸ LendButton.onClick | gap between the first two rows | `RosterScreen/LoanModal/ModalPanel` → **none** | `…/LoanModal/ModalPanel/RecipientScroll` → ScrollRect ✓ | 0 — two rows, too short to scroll |
| Item use modal | Inventory ▸ Items ▸ UseButton.onClick | gap between the first two rows | `…/ItemUseModal/ModalPanel` → **none** | `…/ModalContainer/ScrollArea/Viewport` → ScrollRect ✓ | 769 |
| Bag club modal | `BagClubModalController.Open(slot 0, Equip)` — the slot card's call | gap between the first two rows | `…/BagsClubModal/ModalPanel` → **none** | `…/ModalContainer/ScrollArea/Viewport` → ScrollRect ✓ | 335 |
| GPS vote | Home ▸ GpsPill ▸ GpsHub ▸ VOTE tile.onClick | gap between the first two rows | **nothing at all** → **none** | `GpsVoteScreen/ContentContainer/VoteList` → ScrollRect ✓ | 205 |
| Venue picker modal | `VenuePickerModalController.Open` — CHOOSE MANUALLY's call | gap between the first two rows | `…/VenuePickerModal/Backdrop` → **none** (the press was dismissing the modal's backdrop) | `…/VenuePickerModal/ModalPanel/List` → ScrollRect ✓ | 969 |
| Login form | `ShowScreen(Login)` | gap between the first two rows | `LoginScreen/CardBorder/CardBody` → **none** | `…/ScrollView/Viewport` → ScrollRect ✓ | 0 — form fits the 2532 px viewport |
| Reset-password form | `ShowScreen(ResetPassword)` — the recovery deep-link door's call (the login button only sends an e-mail) | gap between the first two rows | `ResetPasswordScreen/CardBorder/CardBody` → **none** | `…/ScrollView/Viewport` → ScrollRect ✓ | 0 — fits |
| Sign-up form | Login ▸ CreateAccount.onClick | gap between the first two rows | `SignUpScreen/CardBorder/CardBody` → **none** | `…/ScrollView/Viewport` → ScrollRect ✓ | 0 — fits |
| Create-username form | `ShowScreen(CreateUsername)` | gap between the first two rows | `CreateUsernameScreen/CardBorder/CardBody` → **none** | `…/ScrollView/Viewport` → ScrollRect ✓ | 0 — fits |

Control presses (a plain label in the first row — the press that always worked) resolved the
ScrollRect at HEAD and after on every list that has such a label; the four forms and the rankings
row have none the finder accepts (their first child is a header or a card with a Button, which is
fine — a Button does not own drags — but the finder wants a bare label).

**Eight lists scroll, seven are too short to scroll in the Editor's data state** (forms fit the
viewport at 1170×2532; three cards, two rows, a short rates body). For those seven the gate is the
handler resolution — the exact link that was broken — and the sheets show a frame identical
before and after, which is what a list that cannot scroll should do (the `SnapPlayModeSafe`
md5 check flagged them as identical on purpose).

- `media/scroll_lists_drag_anywhere/after_<list>_sheet.jpg` — fifteen sheets, sanctioned
  `CaptureCore.SnapPlayModeSafe` stills: at rest with the press marker | after release.
- `media/scroll_lists_drag_anywhere/scroll_lists_drag_anywhere_clip.mp4` — 45 s, three of the
  scrolling lists (STORE grid, SELECT CLUB modal, venue picker) at the real boot → real taps;
  marker burned in post from the JSON; every caption window checked against a decoded frame;
  header-band flip scan clean (max jump 28, all at screen changes).

## The run that locked the Editor, and what changed because of it

The first after-run recorded all fifteen lists in one take — four minutes of play mode with
`runInBackground` and **no frame cap**, encoding 1170×2532 on top — and pegged the CPU until Cesar
had to kill Unity (another session's recompile had already killed the recorder mid-run: 0-byte
mp4). `ScrollDragAnywhereVerify` now applies `BotVideoRecorder`'s guardrail — `vSyncCount = 0`,
`targetFrameRate = 30` before `StartRecording`, restored on exit — for every session, recorded or
not (measured: ~1.2 of 8 cores during the sweep, 0.5–1 core while recording); the sweep is
JSON + stills by design, and a clip is recorded only for a named subset
(`Launch(label, record: true, sweep: true, only: "a,b,c")`); the verdict JSON is written after every
target so an interrupted run keeps what it finished. Memory:
`feedback_cap_play_mode_frame_rate_never_record_long_sweeps`.

## Files

| File | Change |
|---|---|
| 10 prefabs (table above) | viewport raycast image enabled or added |
| `Assets/Scenes/ShellScene.unity` | 4 viewports, `+122 −2` isolated from the churned save |
| `Assets/Scripts/UI/Polish/Editor/ScrollDragAnywhereVerify.cs` | target table (15 lists), real openers, empty-spot finder (visual row order, grids, forms, empty lists), frame cap, resumable verdict, sanctioned stills |
| `Docs/Specs/Quick/media/scroll_lists_drag_anywhere/` | baseline + after verdict JSON/logs, 15 sheets, 3 clip sheets, captioned clip |
