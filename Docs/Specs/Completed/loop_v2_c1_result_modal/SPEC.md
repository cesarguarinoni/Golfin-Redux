# Stage C1 — ShellScene Result Modal

**Status:** SPEC_READY — Architect scoping pass
**Authored:** 2026-05-20 ~15:00 CEST
**Pipeline:** **FULL PIPELINE** (per scoping SPEC line 251 — visual fidelity + new architecture surface)
**Parent spec:** `Docs/Specs/Active/loop_v2_scope/SPEC.md` lines 117–250
**Companion docs:** `Docs/Architecture/BOT_FRAMEWORK.md`, `Docs/Architecture/CODE_AUDIT_2026-05-19.md` (P0-3)
**Notion:** GOLFIN_Roadmap (UUID `364b3e97-02b7-819b-a734-dfe5a3a087a9`) — Order 310 (C1)

---

## 0. ITERATION-6 REDIRECTION (Cesar override — 2026-05-21)

Iterations 1–5 are rejected. **See `CESAR_REJECTION.md` — it is authoritative and
overrides every conflicting clause below, including the "single card / no Card 2" wording
in §1, §2, §6, §7.** Summary:
- The production modal reuses the **FULL lab widget**
  `Assets/Prefabs/UI/HoleComplete/HoleCompleteWidget.prefab` — **BOTH cards**: Card 1
  (current hole) + Card 2 (next hole), including Card 2's NEXT and LOCKED states. The lab
  design in LabScaffold is correct and must be replicated verbatim. Do NOT author new
  layout, do NOT reduce to one card.
- Card 2 LOCKED appears when the hole was FAILED and the next hole was never unlocked.
- The "scrollable shot history list" (§2) stays DROPPED — the lab uses a stats block
  (TEE OFF / STROKES / BEST / TIME) + a next-hole description. `ShotHistoryRowView.cs` /
  `ShotHistoryRow.prefab` remain deleted.
- Buttons = the lab set: REPLAY/RETRY on Card 1, PLAY on Card 2. No standalone MENU button.
- All verified-GOOD C1 behavior C# (HoleCompletionBridge, BallManager.AddBalls,
  reward/progression, double-fire strip) stays.
- Hole 18 success: no Hole 19 → hide Card 2, fire the "COURSE CLEARED!" toast.

---

## 1. Goal

Build the production end-of-hole Result modal as a **ShellScene-resident** `ModalController` subscriber to `GameSession.OnHoleComplete`. Replace the lab `HoleCompleteWidget` for production runs. On SUCCESS, write hole progression (close audit P0-3) and grant rewards. Three actions: **PLAY NEXT** (load next hole via the C0 path), **MENU** (unload gameplay, return Home), **RETRY** (FAILED state only — reload same hole). Hole 18 SUCCESS hides PLAY NEXT and fires a "course cleared" toast.

The modal must **survive the additive-loaded gameplay scene swap** — it lives on the ShellScene canvas, listens to the cross-scene `GameSession.OnHoleComplete` event from Stage B, and is invisible to gameplay scene lifecycle.

---

## 2. Definition of Done (testable)

### Modal lifecycle
- `HoleCompleteModalController` extends `ModalController`, lives on **ShellScene Canvas** (sibling of `LoadingScreen`, not in `LabScaffold`).
- Subscribes to `GameSession.OnHoleComplete` in `OnEnable`, unsubscribes in `OnDisable`. Modal `gameObject` is active across scene loads (it's on ShellScene which is persistent).
- On event: routes to **SUCCESS** (terminal=`InCup`) or **FAILED** (terminal=`AtRest` with `Strokes >= cap`) render. Calls `Show()`.
- Modal canvas `sortingOrder = 900` (above gameplay canvases at 0, below `LoadingScreen` at 1000 — verified in `LoadingScreenController.Awake`).

### SUCCESS card content
- Strokes (`HoleCompletionData.Strokes`, includes penalty strokes), par (looked up from `HoleData`), score-vs-par badge (Eagle/Birdie/Par/Bogey/etc. — reuse `HoleCompleteDriver.ScoreLabelFor`).
- Course + hole title (`HoleData.courseNameKey` localized + `holeNumber`).
- Scrollable shot history list — one row per `ShotRecord` in `GameSession.ShotHistory`. Row shows: shot #, club label, distance (XZ meters), terminal state, OB reason (if any), final surface, penalty stroke flag.
- Rewards row: 3 slots (Points / RepairKit / Ball) bound from `HoleData.rewards` (first clear) OR `HoleData.replayRewards` (if `HoleProgressionService.HasPlayed(currentHole)` was already true at completion).
- Buttons: **PLAY NEXT** + **MENU** (PLAY NEXT hidden on Hole 18).

### FAILED card content
- "FAILED" badge instead of score badge. Strokes and shot history shown. **No rewards row.**
- Buttons: **RETRY** + **MENU**. PLAY NEXT hidden.

### Hole progression (closes audit P0-3)
- New interface `IHoleProgressionStore` + adapter wraps `HoleProgressionService`. Adds `MarkHolePlayed(int)` + `UnlockHole(int)`. Read methods (`IsUnlocked`, `HasPlayed`) delegate.
- SUCCESS PLAY NEXT and SUCCESS MENU both write: `MarkHolePlayed(current)` + `UnlockHole(current + 1)` if `current < 18`.
- FAILED RETRY and FAILED MENU write nothing.

### Reward grant (SUCCESS only)
- Choose pool: `replayRewards` if `HasPlayed(current)` was true before C1 wrote `MarkHolePlayed`; else `rewards`.
- For each `HoleReward` in the chosen pool, call:
  - `RewardType.Points`    → `RewardPointsManager.Instance.EarnPoints(amount)`  *(NOT `AddPoints` — scoping SPEC line was inaccurate; verified actual method = `EarnPoints`)*
  - `RewardType.RepairKit` → `ItemManager.Instance.AddItems(REPAIR_KIT_DEFAULT_ID, amount)`
  - `RewardType.Ball`      → `BallManager.Instance.AddBalls(BALL_DEFAULT_ID, amount)`  *(new mutator — see §6)*

### Action handlers
- **PLAY NEXT** (visible iff `current < 18`): writes progression, calls `LoadingScreenController.PrepareForHoleLoad(current + 1)` → `GameplaySceneLoader.Instance.BeginGameplayLoad(current + 1)`. Modal `Hide()` is fired by `GameplaySceneLoader` at FadeController midpoint (same mechanism as MatchmakingModalController — pass `modalToHideOnMidpoint: this`). `GameSession.SetCurrentHole(current + 1)` is called before triggering load.
- **MENU**: writes progression (if SUCCESS), `Hide()` modal, starts `GameplaySceneLoader.Instance.UnloadGameplay()` coroutine, then `ScreenManager.Instance.ShowScreen(Home)`. `GameSession.ResetSession()` after unload completes.
- **RETRY** (FAILED only): no progression write, modal `Hide()`, `GameSession.ResetForNewHole()` (keeps seed), `GameplaySceneLoader.Instance.BeginGameplayLoad(current)` — same hole reload via existing C0 path.

### Hole 18 SUCCESS special case
- PLAY NEXT button `SetActive(false)`.
- MENU button styled prominent (use the existing PLAY NEXT styling — swap MENU's Image sprite and TMP color to PLAY NEXT's gold gradient. Pattern: serialize a `_menuProminentSprite` + `_menuProminentTextColor`, apply when `current == 18 && success`).
- New `ToastController.Show("COURSE CLEARED!", 3f)` fires at modal show. Lives on ShellScene Canvas, sortingOrder = 950 (above modal, below LoadingScreen).

### Lab `HoleCompleteWidget` retirement
- `HoleCompleteDriver.HandleShotComplete` — **strip the `widget.Show(...)` call** and **strip the `GameSession.MarkHoleComplete(...)` call** (the new `HoleCompletionBridge` owns that).
- `HoleCompleteDriver.ShowForDebug()` — keep as editor-only lab debug helper (used by `DebugShotPanel` "Hole Out" button).
- Lab `HoleCompleteWidget` GameObject in `LabScaffold.unity`: leave in place (Cesar / Code can decide to delete later via MCP scene wiring), but its `widget.Show()` call site is now dormant in production.

### FAILED state detection
- New `HoleCompletionBridge.cs` MonoBehaviour (production, attached in `LabScaffold.unity`) subscribes to `BallStateMachine.OnShotComplete`. On each fire:
  - `terminal == InCup` → fire `GameSession.MarkHoleComplete(...)` with terminal=InCup.
  - `terminal == AtRest && GameSession.TurnCount >= STROKE_CAP` → fire `GameSession.MarkHoleComplete(...)` with terminal=AtRest (FAILED proxy).
  - `terminal == OB` or `terminal == AtRest && < cap` → no-op (continue play, OB adds penalty stroke elsewhere).
- `STROKE_CAP = HoleData.par + 5` (lock value — see open question Q1 if Cesar wants a different cap).

### Visual gate — bot scenarios
- `Scenarios.Hole1Playthrough` — update final capture description: result modal now lives on ShellScene canvas, expect `HoleCompleteModal` GO name (not `HoleCompleteWidget`).
- **NEW** `Scenarios.Hole1PlayNext` — clears Hole 1 via `ForceShotComplete("InCup")`, taps `PlayNextButton`, captures LoadingScreen + Hole 2 armed.
- **NEW** `Scenarios.Hole1Menu` — clears Hole 1, taps `MenuButton`, captures Home returned + nav bar visible.
- **NEW** `Scenarios.Hole1RetryAfterFail` — bumps `GameSession.TurnCount` to STROKE_CAP via `GameSession.SetTurn(cap)`, fires `ForceShotComplete("AtRest")`, captures FAILED modal, taps `RetryButton`, captures Hole 1 re-armed.
- **NEW** `Scenarios.Hole18CourseCleared` — seeds `GameSession.SetCurrentHole(18)`, fires `ForceShotComplete("InCup")`, captures SUCCESS modal with **no** PLAY NEXT button + "course cleared" toast visible.

### EditMode tests (target: 12 new)
1. `Modal_SubscribesOnEnable_UnsubscribesOnDisable` — toggle enable, fire `OnHoleComplete`, assert call count.
2. `Modal_SuccessTerminal_RoutesSuccessRender` — fire with `InCup`, assert SUCCESS render path called.
3. `Modal_FailedTerminal_RoutesFailedRender` — fire with `AtRest + Strokes>=cap`, assert FAILED render.
4. `Modal_PlayNextWritesProgression` — SUCCESS, click PLAY NEXT, assert `MarkHolePlayed(current) + UnlockHole(current+1)`.
5. `Modal_MenuOnFailedDoesNotWriteProgression` — FAILED, click MENU, assert no progression writes.
6. `Modal_Hole18HidesPlayNext` — fire SUCCESS at hole=18, assert PLAY NEXT not active + ToastController.Show was called.
7. `Modal_RetryReloadsSameHole` — FAILED, click RETRY, assert `BeginGameplayLoad(current)` called with same hole.
8. `Bridge_InCupTriggersMarkHoleComplete` — fire `OnShotComplete(InCup)`, assert MarkHoleComplete fired once.
9. `Bridge_StrokeCapTriggersFailed` — set TurnCount=cap, fire `OnShotComplete(AtRest)`, assert MarkHoleComplete fired with AtRest.
10. `Bridge_AtRestBelowCapNoFire` — set TurnCount<cap, fire `OnShotComplete(AtRest)`, assert no fire.
11. `RewardGrant_FirstClearUsesRewardsPool` — `HasPlayed=false`, SUCCESS, assert `EarnPoints`/`AddItems`/`AddBalls` calls match `HoleData.rewards`.
12. `RewardGrant_ReplayUsesReplayRewardsPool` — `HasPlayed=true`, SUCCESS, assert calls match `HoleData.replayRewards`.

Compile clean. EditMode test gate green.

---

## 3. Pre-flight resolutions (verified in repo at `25cd7fd2`)

| # | Item | Verified | Resolution |
|---|---|---|---|
| 1 | `HoleCompletionData` payload sufficiency | `Assets/Scripts/Gameplay/Loop/Session/HoleCompletionData.cs` — TerminalState/Strokes/PenaltyStrokes/HoleNumber/CompletedAtUtc | Lean payload sufficient. Score-vs-par badge and par lookup computed in modal view (par read from `HoleDatabaseLoader.GetHole(holeNumber)`). |
| 2 | `ShotRecord` shape for history list | `Assets/Scripts/Gameplay/Loop/Session/GameSession.cs` — readonly struct with ShotNumber/ClubLabel/Origin/Final/DistanceXZ/Terminal/OBReason/Surface/PenaltyStrokes | All required fields present. **No extension needed.** History row component reads these directly. |
| 3 | FAILED state trigger | `BallStateMachine` fires `OnShotComplete` with terminal in {AtRest, InCup, OB}. No native stroke-cap. | New `HoleCompletionBridge.cs` ships in C1 — subscribes to `OnShotComplete`, fires `MarkHoleComplete(AtRest)` when `TurnCount >= par + 5`. Lab `HoleCompleteDriver` is stripped of its `MarkHoleComplete` + `widget.Show` calls (lab Debug `ShowForDebug` preserved). |
| 4 | Reward Type enum coverage vs manager APIs | `RewardPointsManager.EarnPoints` ✅; `ItemManager.AddItems(itemId, count)` ✅; **`BallManager.AddBall` does not exist**. | (a) Scoping SPEC's `AddPoints` was inaccurate — use `EarnPoints`. (b) `ItemManager.AddItems` requires an itemId — `HoleData.HoleReward` has no tier. Default to `repairkit_common` (lock; see Q2). (c) **New mutator `BallManager.AddBalls(string ballId, int count)`** ships in C1 (mirrors `ItemManager.AddItems`). Default ball id `ball_golfin` (lock; see Q3). |
| 5 | Toast infrastructure | `grep ToastController/ToastManager` returns 0 hits. | Ship a minimal `ToastController.cs` in C1. Singleton on ShellScene Canvas, sortingOrder=950, one `TextMeshProUGUI` + `CanvasGroup` fade-in/hold/fade-out. ~60 lines. API: `Toast.Show(string text, float seconds = 3f)`. |
| 6 | Modal z-order vs FadeController | `LoadingScreenController.Awake` forces canvas overrideSorting=true, sortingOrder=1000. `FadeController` on its own Canvas (ShellScene root, child position controls draw order — default no overrideSorting). | Modal canvas `overrideSorting=true, sortingOrder=900`. **Below** LoadingScreen + FadeController, **above** gameplay scene canvases (LabScaffold ShotUI at 0). No collision. |
| 7 | ShellScene + Hole scene coexistence | `GameplaySceneLoader` confirms additive load (LabScaffold + Hole_NN_Geo on top of ShellScene). | Additive load is the production pattern. Modal lives on ShellScene Canvas, naturally persists across hole loads/unloads. No `DontDestroyOnLoad` needed. |
| 8 | Lab widget retirement | `HoleCompleteDriver.HandleShotComplete` currently calls BOTH `MarkHoleComplete` (the bridge role) AND `widget.Show` (the lab UI). | Strip both calls from `HandleShotComplete`. Keep `ShowForDebug` (lab debug button) — editor-only. New `HoleCompletionBridge.cs` owns `MarkHoleComplete`. |
| 9 | LoopV2SmokeBot scenario coverage | `Scenarios.cs` ships 3 scenarios. C1 needs PLAY NEXT, MENU, RETRY-after-FAIL, Hole 18 cleared. | Extend `Scenarios.cs` with 4 new scenarios + dispatch cases + menu items (per BOT_FRAMEWORK.md §7). |

---

## 4. Open questions for Cesar (3 only — pick a default or override)

| # | Question | Options | Recommended |
|---|---|---|---|
| Q1 | **Stroke cap for FAILED state.** What triple-bogey-or-worse threshold ends the hole as FAILED? | (a) `par + 3` (triple bogey) / (b) `par + 5` / (c) `par + 7` (double par-ish) / (d) "no cap — InCup is the only end-condition; FAILED is unreachable in C1" | **(b) par + 5.** Tight enough to surface FAILED state for testing; lenient enough that real play rarely hits it. |
| Q2 | **Default RepairKit item id for `RewardType.RepairKit` rewards.** `HoleReward` has no tier field. | (a) `repairkit_common` / (b) `repairkit_rare` / (c) extend `HoleReward` schema to carry an `itemId` field (CSV migration) | **(a) `repairkit_common`.** Default reward = common tier; CSV schema change is its own Roadmap item. |
| Q3 | **Default Ball id for `RewardType.Ball` rewards.** Same schema gap. | (a) `ball_golfin` (the starter, only unlimited ball) / (b) random non-starter (e.g. `ball_putt_ace`) / (c) extend `HoleReward` schema | **(a) `ball_golfin`.** Safe default — Golfin is unlimited so AddBalls is a no-op for the player. Lets C1 ship the reward path without picking ball economy. |

Default if Cesar says "go": (b)(a)(a).

---

## 5. Locked decisions (do not re-litigate)

From scoping SPEC (locked 2026-05-19):
- Q3: Modal lives on **ShellScene UI canvas** (Option B).
- Cross-scene signal = `GameSession.OnHoleComplete` (Stage B shipped).
- Reward grant = direct manager calls (no event bus yet).
- Every replay clear grants `replayRewards`, no daily caps.
- Hole 18 SUCCESS: PLAY NEXT hidden, MENU prominent, "course cleared" toast.
- Modal extends `ModalController` (fade-in/fade-out + backdrop).

From this SPEC's pre-flight (locked here):
- New `HoleCompletionBridge.cs` is the production InCup/FAILED → MarkHoleComplete bridge. `HoleCompleteDriver` lab role becomes editor-debug-only.
- `IHoleProgressionStore` interface + adapter ships in C1 (Foundation #1).
- `BallManager.AddBalls(string ballId, int count)` ships in C1 (small additive mutator).
- Modal canvas `sortingOrder = 900`. Toast canvas `sortingOrder = 950`. LoadingScreen stays 1000.
- `ToastController` is a singleton on ShellScene Canvas.
- Modal hides via `Hide()` triggered by `GameplaySceneLoader` FadeController midpoint (passed via `modalToHideOnMidpoint: this`) — same pattern as `MatchmakingModalController`.

---

## 6. Files (CREATED | EDITED | DELETED)

### CREATED
| Path | Purpose |
|---|---|
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalController.cs` | The modal controller. ~250 lines. |
| `Assets/Scripts/UI/Modals/Result/HoleCompleteModalView.cs` | Optional split: pure data-binding (subhead/stats/history list/rewards row/button visibility). Keep in same file if Controller stays under ~300 lines. |
| `Assets/Scripts/UI/Modals/Result/ShotHistoryRowView.cs` | One-row prefab binder for the scrollable shot history list. |
| `Assets/Scripts/UI/Toast/ToastController.cs` | Minimal singleton toast. ~60 lines. |
| `Assets/Scripts/Gameplay/Loop/Session/HoleCompletionBridge.cs` | Production bridge: subscribes to `BallStateMachine.OnShotComplete`, fires `GameSession.MarkHoleComplete` on InCup OR stroke-cap. ~80 lines. |
| `Assets/Scripts/Gameplay/Loop/Session/IHoleProgressionStore.cs` | Read+write interface. ~30 lines. |
| `Assets/Scripts/Gameplay/Loop/Session/HoleProgressionStoreAdapter.cs` | Adapter wrapping `HoleProgressionService.Instance`. ~50 lines. |
| `Assets/Scenes/ShellScene.unity` *(scene-wire only)* | Add `HoleCompleteModal` GO + `ToastController` GO as children of ShellScene Canvas. **Code via Unity MCP — no paste-for-Cesar.** |
| `Assets/Scenes/LabScaffold.unity` *(scene-wire only)* | Add `HoleCompletionBridge` MonoBehaviour to a new `[Session]` GO. **Code via Unity MCP.** |
| `Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab` | Modal prefab (rebuild from `HoleCompleteWidget` lab variant — single-card layout, no Card 2 stacking). Cesar approves visual fidelity per gate. |
| `Assets/Prefabs/UI/Modals/ShotHistoryRow.prefab` | One row of the scrollable history list. |
| `Assets/Prefabs/UI/Modals/Toast.prefab` | Toast root with TMP + CanvasGroup. |
| `Assets/Scripts/Gameplay/Tests/HoleCompleteModalControllerTests.cs` | EditMode tests 1–7. |
| `Assets/Scripts/Gameplay/Tests/HoleCompletionBridgeTests.cs` | EditMode tests 8–10. |
| `Assets/Scripts/Gameplay/Tests/RewardGrantTests.cs` | EditMode tests 11–12. |

### EDITED
| Path | Change |
|---|---|
| `Assets/Scripts/BallManager.cs` | **+ public `void AddBalls(string ballId, int count)`** — mirrors `ItemManager.AddItems`. Fires `OnInventoryChanged`. ~12 lines. |
| `Assets/Scripts/Physics/Viewer/HoleCompleteDriver.cs` | Strip `GameSession.MarkHoleComplete(...)` AND `widget.Show(data, OnModalClose)` from `HandleShotComplete`. Keep `ShowForDebug` intact. Add `[Header("DEPRECATED")]` + summary note that production routes through `HoleCompletionBridge`. |
| `Assets/Scripts/Physics/Viewer/Bot/Scenarios.cs` | + 4 new scenarios (PlayNext, Menu, RetryAfterFail, Hole18CourseCleared). |
| `Assets/Scripts/Physics/Viewer/Bot/LoopV2SmokeBot.cs` | + dispatch cases for 4 new scenario keys. |
| `Assets/Scripts/Physics/Viewer/Bot/Editor/LoopV2SmokeBotMenu.cs` | + 4 MenuItems under `GOLFIN/Smoke/Loop v2/`. |

### DELETED
None.

---

## 7. Architecture sketch

### `HoleCompleteModalController` (skeleton)

```csharp
namespace Golfin.UI.Modals.Result
{
    public class HoleCompleteModalController : ModalController
    {
        [Header("Header")]
        [SerializeField] GameObject _successHeader;
        [SerializeField] GameObject _failedHeader;
        [SerializeField] TMP_Text   _scoreBadge;     // "Birdie" / "Par" / "+5"
        [SerializeField] TMP_Text   _subheadText;    // "Lomond - Hole 3 - Par 4"

        [Header("Stats")]
        [SerializeField] TMP_Text   _strokesText;
        [SerializeField] TMP_Text   _parText;

        [Header("Shot history")]
        [SerializeField] RectTransform _historyContent;
        [SerializeField] ShotHistoryRowView _historyRowPrefab;

        [Header("Rewards row")]
        [SerializeField] CanvasGroup _rewardsCanvasGroup;
        [SerializeField] TMP_Text    _rewardCoinText;
        [SerializeField] TMP_Text    _rewardRepairText;
        [SerializeField] TMP_Text    _rewardBallText;

        [Header("Buttons")]
        [SerializeField] Button _playNextButton;
        [SerializeField] Button _menuButton;
        [SerializeField] Button _retryButton;
        [SerializeField] Sprite _menuProminentSprite;       // hole 18 styling
        [SerializeField] Image  _menuButtonImage;

        // Defaults locked in SPEC §4
        const string REPAIR_KIT_DEFAULT_ID = "repairkit_common";
        const string BALL_DEFAULT_ID       = "ball_golfin";

        IHoleProgressionStore _progression;
        HoleCompletionData    _lastData;
        bool _wasReplay;

        protected override void Awake()
        {
            base.Awake();
            _progression = HoleProgressionStoreAdapter.Default;
            _playNextButton.onClick.AddListener(OnPlayNext);
            _menuButton.onClick.AddListener(OnMenu);
            _retryButton.onClick.AddListener(OnRetry);
        }

        void OnEnable()  => GameSession.OnHoleComplete += HandleHoleComplete;
        void OnDisable() => GameSession.OnHoleComplete -= HandleHoleComplete;

        void HandleHoleComplete(HoleCompletionData data)
        {
            _lastData  = data;
            _wasReplay = _progression.HasPlayed(data.HoleNumber);
            bool success = data.TerminalState == BallState.InCup;
            if (success) RenderSuccess(data);
            else         RenderFailed(data);
            Show();
        }

        void RenderSuccess(HoleCompletionData data) { /* ... */ }
        void RenderFailed (HoleCompletionData data) { /* ... */ }

        void OnPlayNext() { /* writes progression + GameplaySceneLoader.BeginGameplayLoad(next) */ }
        void OnMenu()     { /* if success: writes progression; UnloadGameplay; Home */ }
        void OnRetry()    { /* GameSession.ResetForNewHole; BeginGameplayLoad(current) */ }
    }
}
```

### `HoleCompletionBridge` (skeleton)

```csharp
namespace Golfin.Gameplay.Session
{
    public class HoleCompletionBridge : MonoBehaviour
    {
        [SerializeField] PhysicsLabController _controller;   // auto-resolves if null
        [Tooltip("Stroke cap above par that triggers FAILED. Default: par + 5.")]
        [SerializeField] int _strokeCapOverPar = 5;

        BallStateMachine _sm;

        void Awake()
        {
            if (_controller == null) _controller = FindObjectOfType<PhysicsLabController>();
            _sm = _controller != null ? _controller.BallSM : null;
            if (_sm != null) _sm.OnShotComplete += HandleShot;
        }

        void OnDestroy() { if (_sm != null) _sm.OnShotComplete -= HandleShot; }

        void HandleShot(ShotResult result)
        {
            int strokes = GameSession.TurnCount;
            int par     = HoleContext.Par;
            int cap     = par + _strokeCapOverPar;

            if (result.TerminalState == BallState.InCup)
            {
                Fire(BallState.InCup, strokes);
            }
            else if (result.TerminalState == BallState.AtRest && strokes >= cap)
            {
                Fire(BallState.AtRest, strokes);
            }
            // OB or AtRest-below-cap: no-op (play continues)
        }

        void Fire(BallState terminal, int strokes)
        {
            int penalties = 0;
            foreach (var rec in GameSession.ShotHistory) penalties += rec.PenaltyStrokes;
            int holeNumber = GameSession.CurrentHoleNumber > 0
                                ? GameSession.CurrentHoleNumber
                                : HoleContext.HoleNumber;
            GameSession.MarkHoleComplete(new HoleCompletionData(terminal, strokes, penalties, holeNumber));
        }
    }
}
```

### `IHoleProgressionStore` + adapter

```csharp
namespace Golfin.Gameplay.Session
{
    public interface IHoleProgressionStore
    {
        bool IsUnlocked(int holeNumber);
        bool HasPlayed (int holeNumber);
        void MarkHolePlayed(int holeNumber);
        void UnlockHole(int holeNumber);
    }

    public class HoleProgressionStoreAdapter : IHoleProgressionStore
    {
        public static readonly HoleProgressionStoreAdapter Default = new();
        readonly HoleProgressionService _svc = HoleProgressionService.Instance;

        public bool IsUnlocked(int n)       => _svc.IsUnlocked(n);
        public bool HasPlayed (int n)       => _svc.HasPlayed(n);
        public void MarkHolePlayed(int n)   => _svc.SetPlayedOverride(n, true);
        public void UnlockHole(int n)       => _svc.SetUnlockedOverride(n, true);
    }
}
```

### `ToastController` (skeleton)

```csharp
namespace Golfin.UI.Toast
{
    public class ToastController : MonoBehaviour
    {
        public static ToastController Instance { get; private set; }

        [SerializeField] CanvasGroup _canvasGroup;
        [SerializeField] TMP_Text    _text;
        [SerializeField] float _fadeIn = 0.3f, _fadeOut = 0.5f;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        public void Show(string message, float holdSeconds = 3f)
        {
            StopAllCoroutines();
            _text.text = message;
            gameObject.SetActive(true);
            StartCoroutine(Run(holdSeconds));
        }

        IEnumerator Run(float hold)
        {
            yield return Fade(0f, 1f, _fadeIn);
            yield return new WaitForSeconds(hold);
            yield return Fade(1f, 0f, _fadeOut);
            gameObject.SetActive(false);
        }

        IEnumerator Fade(float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
```

---

## 8. Visual gate (smoke bot)

Existing `Hole1Playthrough` updates: capture `result_modal` now expects ShellScene-resident `HoleCompleteModal` (not `HoleCompleteWidget`). Update wait predicate from `WaitForGameObject("HoleCompleteWidget")` (if used) to `WaitForGameObject("HoleCompleteModal")`.

New scenarios in `Scenarios.cs`:

1. **`Hole1PlayNext`** — composes existing primitives. After `result_modal` capture, `d.Click("PlayNextButton")`, `d.WaitForGameObject("LoadingScreen")`, `d.WaitForSceneLoaded("Hole_02_Geo")`, capture `hole2_armed`.
2. **`Hole1Menu`** — after `result_modal`, `d.Click("MenuButton")`, `d.WaitForScreen("Home")`, capture `home_returned_from_menu`. Verify bottom nav visible.
3. **`Hole1RetryAfterFail`** — drives FAILED state via test seam: `GameSession.SetTurn(par + 5)` (need a small seam — see §10) + `ForceShotComplete("AtRest")`. Wait for FAILED modal, capture, `d.Click("RetryButton")`, wait for `Hole_01_Geo` re-load.
4. **`Hole18CourseCleared`** — `GameSession.SetCurrentHole(18)`, then `ForceShotComplete("InCup")`, wait for modal, assert PLAY NEXT button inactive + `Toast` GO visible, capture.

Add 4 menu items + 4 dispatch cases per BOT_FRAMEWORK.md §7.

**Cesar's eyeballs gate** (per Loop v1 §2d lessons N–O): no text floats outside its BG; rewards row aligns; shot history row spacing matches Figma; FAILED badge color #D16A47, SUCCESS badge #50C878 (consistent with `HoleCompleteCardWidget.BuildStatsBlock`).

---

## 9. Risks / Rollback

### Risk 1 — Double-fire of `MarkHoleComplete`
After C1 ships, if Code forgets to strip the call from `HoleCompleteDriver.HandleShotComplete`, both the driver and the bridge will fire `MarkHoleComplete` → modal sees the event twice. Mitigation: the strip is in §6 "EDITED" with explicit lines to remove; reviewer must verify on diff. EditMode test `Bridge_InCupTriggersMarkHoleComplete` would also catch a double-fire if it counts handler invocations.

### Risk 2 — Modal z-order regression
If the modal canvas isn't given `overrideSorting=true, sortingOrder=900`, gameplay HUD elements on `LabScaffold` (ShotUI_canvas at order 0) might punch through. Mitigation: set in `Awake` of `HoleCompleteModalController` like `LoadingScreenController` already does.

### Risk 3 — `BallManager.AddBalls` semantics for the `ball_golfin` default
`ball_golfin` is initialized with quantity = -1 (unlimited). Adding to unlimited should be a no-op. Mitigation: in `AddBalls`, mirror `ItemManager.AddItems` pattern — `if (data.IsUnlimited) { OnInventoryChanged?.Invoke(); return; }`.

### Risk 4 — FAILED modal during real-play stroke spiral
A player who genuinely cannot get the ball in the cup within par+5 hits the FAILED modal. This is intended behavior for C1 (and matches game-design intent of "give up gracefully"), but Cesar should verify the cap value (Q1) is right for current putter physics.

### Rollback
- `git revert <c1-merge-sha>` restores Stage C0 state.
- Lab `HoleCompleteWidget` path was only stripped (not deleted) — restoring the two `HoleCompleteDriver.HandleShotComplete` lines + the deleted modal files returns the system to C0.
- `BallManager.AddBalls` is additive — leaving it on a revert is harmless (zero callers).

---

## 10. Implementer notes

- **Unity scene wiring** (ShellScene + LabScaffold edits): per user memories, **Claude Code uses Unity MCP**. Architect does not paste scene .unity diffs.
- **Test seams**: `Scenarios.Hole1RetryAfterFail` needs a way for the bot to bump `GameSession.TurnCount`. `GameSession.SetTurn(int)` already exists (public static method) — no seam needed. Similarly `GameSession.SetCurrentHole(int)` exists for `Hole18CourseCleared`.
- **HoleData lookup**: modal needs par for the current hole. Use `HoleDatabaseLoader.GetHole(GameSession.CurrentHoleNumber)`. Cache on `RenderSuccess`/`RenderFailed`.
- **Localization**: course name + hole title use the same `LocalizationManager.Get(courseNameKey)` pattern as `MatchmakingModalController.ApplyHole`.
- **Score-vs-par badge**: reuse `HoleCompleteDriver.ScoreLabelFor(int score)` (already public static). Make sure the C1 modal's asmdef (or default Assembly-CSharp) can reach it — `HoleCompleteDriver` is in `Golfin.Physics.Viewer` asmdef. If reaching out is awkward, **copy the 12-line method into the modal** (it's pure stateless utility — no DRY violation that matters here).
- **Modal canvas**: best to add a dedicated child `Canvas` component on the `HoleCompleteModal` GO under ShellScene Canvas, with `overrideSorting=true`, `sortingOrder=900`. Add a `GraphicRaycaster` so buttons receive input.
- **Shot history scroll**: standard `ScrollRect` + `VerticalLayoutGroup` + `ContentSizeFitter` pattern. Cap row count at `ShotHistory.Count` (no pagination — typical hole is < 10 strokes).

---

## 11. Out of scope (parked, not forgotten)

- **Real save layer for HoleProgressionService** — still in-memory. Save-layer milestone post-Loop-v2.
- **HoleReward CSV schema extension** to carry tier/id for RepairKit and Ball rewards. Default-id resolution in §4 is the C1 ship.
- **DOTween/animation polish** beyond `ModalController` default fade — Stage F.
- **Reward bus / event bus** — Stage F or later. Direct manager calls only in C1.
- **Lab `HoleCompleteWidget` actual deletion** — left in `LabScaffold` scene; cleanup in a future cleanup pass once Cesar confirms production never wants it.
- **Hole 18 "course completed" persistent flag** — toast only in C1. Persistent "you've cleared the course" achievement state is post-Loop-v2.

---

## Pipeline routing

**FULL PIPELINE.** Sub-spec folder: this folder.
Visual gate: smoke bot scenarios (`Scenarios.Hole1Playthrough` updated + 4 new).
Demo videos drop at `Docs/Videos/loop_v2_c1_result_modal/`.

Next step: Cesar locks Q1/Q2/Q3 (or says "go" to accept (b)(a)(a)), then I fire `Use the golfin-implementer subagent on "loop_v2_c1_result_modal"`.
