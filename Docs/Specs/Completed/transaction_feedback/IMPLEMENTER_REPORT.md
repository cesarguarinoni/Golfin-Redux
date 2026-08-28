# Implementer Report — `transaction_feedback`

Implemented 2026-08-28 by Claude Code, directly at Cesar's request (not via the
`golfin-implementer` subagent, so the `enforce_implementer_done.py` hook gates were not the
route here — the sections it looks for are filled anyway).

## Implementation summary

Three changes, in the shape the spec set out. **Part A**: `playlife/backend/fly.toml` now says
`auto_stop_machines = "suspend"` (was `"stop"`); `min_machines_running` stays `0`. Deployed;
after 7 min idle the Machine reports `suspended`, and the resume costs **1.18 s** where a boot
from `stopped` cost **5.20 s** on the same endpoint minutes earlier. **Part B**: a new
`Golfin.UI.Polish.PendingSpend` disposable disables the tapped control and swaps its label for `…`
from tap to callback, wired at all six spend call sites; every existing `_purchaseInFlight` latch
is untouched. **Part C**: `ApiClient.SendRoutine` logs one line per completed logical call with the
elapsed ms, warning above 1500 ms — which is what produced the cold-vs-warm numbers quoted below.

## Files modified or created

| Path | Change |
|---|---|
| `playlife/backend/fly.toml` | modified — `auto_stop_machines` `"stop"` → `"suspend"`; nothing else touched |
| `Assets/Scripts/UI/Polish/PendingSpend.cs` | **created** — the shared pending-spend scope (`Begin` / `Dispose`), Assembly-CSharp, same assembly as `ButtonPressFeedback` |
| `Assets/Scripts/UI/Polish/Tests/Golfin.UI.Polish.Tests.asmdef` | **created** — EditMode test assembly (references `Unity.TextMeshPro`, `UnityEngine.UI`) |
| `Assets/Scripts/UI/Polish/Tests/PendingSpendTests.cs` | **created** — 9 EditMode tests reaching `PendingSpend` by reflection (the `ApplyServerBalanceTests` pattern) |
| `Assets/Scripts/Net/ApiClient.cs` | modified — `SlowRequestMs` tunable + `LogCompleted` / `PathOf`; one log line per completed call, at all 4 exits |
| `Assets/Scripts/EconomyRuntime/PointsSpendGate.cs` | modified — added read-only `IsSpendInFlight`; `Spend` itself byte-identical |
| `Assets/Scripts/UI/Shop/GeneralShopCard.cs` | modified — exposed `BuyButton` / `BuyLabel`; `WireBuy` now reads them instead of re-resolving the same two paths |
| `Assets/Scripts/UI/Shop/GeneralShopScreenController.cs` | modified — `HandleBuy` wraps the round-trip; disposes first in `onResult` |
| `Assets/Scripts/UI/Shop/StaminaMenuRow.cs` | modified — exposed `BuyButton` / `BuyLabel` |
| `Assets/Scripts/UI/Shop/StaminaShopDetailScreenController.cs` | modified — `HandleBuyClicked` wraps; re-asserts `UpdateBuyButtonStates` after the restore |
| `Assets/Scripts/UI/Roster/UI/LevelUpModalController.cs` | modified — CONFIRM + CANCEL wrapped around `ProgressService.LevelUpAsync` |
| `Assets/Scripts/UI/Inventory/ClubLevelUpModalController.cs` | modified — same, mirrored |
| `Assets/Scripts/UI/ModeSelect/ModeCardController.cs` | modified — PLAY + card-tap + tagline wrapped around `PointsSpendGate.Spend` |
| `Assets/Scripts/UI/Tournaments/TournamentSignupModalController.cs` | modified — CONFIRM + CANCEL wrapped around both round-trip paths (sixth call site) |

## Screenshot

- **Canonical screenshot:** `screenshots/01_generalshop_buy_pending.png` (1170×2532) — the general-shop
  card mid-purchase, exactly the frame §7 names. Crop: `screenshots/01b_generalshop_buy_pending_crop.png`.
- **Paired settled frame:** `screenshots/02_generalshop_buy_settled.png` (+ `02b_..._crop.png`)
- **Captured at:** `Docs/Diagnostics/_capture/screenshot_2026-08-28_22-49-36.png` / `..._22-49-37.png`
- **Scene loaded:** `Assets/Scenes/ShellScene.unity`
- **Play mode:** Yes (real boot; navigated Home → shop via `ShopPlusButton.onClick`, the player's own entry point)
- **Flag:** `PointsBackendFlag.Enabled = True`, live API, signed-in session

In the pending frame the PUTT ACE card's BUY is the Button's own Disabled tint (dull brown-gold) and
reads `…`; the MIKE MILLAR card directly below it is bright gold and reads `BUY`. In the settled frame
PUTT ACE is bright gold `BUY` again. Both frames were read before being cited.

## §3.1 call sites — all six wired

| # | Surface | Wrapped control(s) | Verified |
|---|---|---|---|
| 1 | General shop card BUY | card `BuyButton` + `BuyLabel` | **Live** — screenshot + frame probe + `[ApiClient]` line |
| 2 | Stamina shop row BUY | row `BuyButton` + `BuyLabel` | Code + unit tests (no live capture — see Known gaps) |
| 3 | Character level-up CONFIRM | `confirmButton`, `confirmButtonLabel`, also `cancelButton` | Code + unit tests |
| 4 | Club level-up CONFIRM | `confirmButton`, `confirmButtonLabel`, also `cancelButton` | Code + unit tests |
| 5 | Mode card PLAY | `playButton`, `playButtonLabel`, also `cardTapButton`, `taglineButton` | Code + unit tests |
| 6 | Tournament signup CONFIRM | `_confirmButton`, also `_cancelButton` | Code + unit tests |

**Sixth row confirmed in scope.** The spec asked me to verify whether the tournament spend is a
round-trip in the flag-ON build. It is, on **both** of `OnConfirm`'s paths: the async-board path posts
`/enter` via `remote.RegisterAsync`, and `TrySpendAsync` →
`RewardPointsServiceAdapter.TrySpendAsync` → `PointsSpendGate.Spend`. Both are wrapped.

## Acceptance checklist

| Item | Result | Justification |
|---|---|---|
| `fly.toml` carries `auto_stop_machines = "suspend"`; deploy green; `/health` 200; `fly status` shows `suspended` after ≥5 min idle | **PASS** | `flyctl deploy` exit 0, image version 60 → 61. Idle from 13:35:27Z; at **13:42:47Z** (7 m 20 s) `flyctl status` reported `app │ 148e03defe4d38 │ 61 │ nrt │ suspended`. `/health` 200 on every probe. `suspend` was accepted — no fallback to `min_machines_running = 1` was needed. |
| Cold-vs-warm timing quoted from the new `[ApiClient]` lines, plus a pre-change baseline | **PASS** | See § Timing evidence. Pre-change baseline `5.201 s`; post-change resume `1.185 s`; warm `0.039 s`. In-app: cold `2819 ms`, warm `220 ms`. |
| Each call site: screenshot of the pending state mid-round-trip | **PARTIAL — 1 of 6 captured live** | Site 1 captured live (canonical screenshot + frame probe). Sites 2–6 are the *same* helper on the same code path but have no live frame. See § Known gaps. |
| Refusal paths restore the button (`insufficient`, `price_changed`, offline) | **PARTIAL** | **Offline: live PASS** — with `ApiClient.Instance.Transport` nulled, `[ShopTransaction] 'shop_ball_putt_ace' not purchased: Unavailable.` and the button ended `interactable=True label='BUY'` with RP unchanged at 788. `insufficient` and `price_changed` are covered by test (`Dispose_RestoresInteractableAndLabel`, `DoubleDispose_IsANoOp`) but NOT live — both are unreachable from a client I can drive: `insufficient` is pre-empted by `ShopTransaction`'s synchronous affordability pre-check, and `price_changed` needs the server catalog moved under a live client. |
| EditMode `PendingSpendTests`: Begin disables + relabels, Dispose restores, double-Dispose no-op, exception still restores | **PASS** | 9/9 green. Also covers: restore-to-disabled, `alsoDisable`, null button, destroyed button, and same-button-passed-twice. |
| Existing `PointsSpendTests` unchanged/green | **PASS** | 13/13 green, file untouched. `ApiClientTests` 18/18 green, file untouched. Whole EditMode suite: **1964 tests, 1961 passed, 0 failed, 3 skipped** (the 3 are pre-existing `[Ignore]`s in `HoleCompleteDriverTests`). |
| Flag-OFF harness sequence byte-identical | **PASS** *(by construction + suite)* | Flag-OFF never reaches a round-trip: `PointsSpendGate.Spend` short-circuits before the latch, `ShopTransaction` takes `PurchaseLocally`, and both level-up modals return on the `!PointsBackendFlag.Enabled` branch *before* the wrapped call. Where a wrap does run flag-OFF (shop, stamina, mode card, tournament) the callback is on the same stack frame, so `Begin`+`Dispose` complete in the same frame — observed exactly that on the live offline-refusal probe (`SAME-FRAME interactable=True label='BUY'`). `Spend` itself is unchanged; `IsSpendInFlight` is a read-only getter. Whole suite green. |
| Rule 11: no new `Button` components added | **PASS** | `git diff` adds zero `AddComponent<Button>` / zero new Button serialization; the task only sets `interactable` on buttons that already exist. No prefab or scene was edited at all. |
| Rule 13: uncommitted paths outside the spec folder all listed | **PASS** | `git status --porcelain -uall` outside the folder = the 14 files in the table above, plus three files that were **already dirty at kickoff and are not mine**: `Docs/Reports/content_art.txt`, `Docs/TellCode.md`, `Docs/Versioning/last_uploaded_build.txt` (see the `=== iter-1 kickoff baseline ===` block in `HEARTBEAT.log`). In the `playlife` repo, `backend/migrations/2026_08_24_content_seed.sql` was likewise already dirty at kickoff; only `backend/fly.toml` is mine. |

## Timing evidence (§7)

**Backend, same endpoint, before and after the config change:**

```
BEFORE  auto_stop_machines = "stop",    machines 'stopped'   → HTTP 200  total=5.201887s
AFTER   auto_stop_machines = "suspend", machine 'suspended'  → HTTP 200  total=1.184502s
WARM    (either config)                                       → HTTP 200  total=0.038505s
```

**In-app `[ApiClient]` lines, verbatim from `~/Library/Logs/Unity/Editor.log`** — boot, then a purchase:

```
[ApiClient] SLOW: GET /api/v1/content → 200 in 2819 ms (cold start?)
[ApiClient] GET /api/v1/points/balance → 200 in 220 ms
[ApiClient] POST /api/v1/shop/purchase → 200 in 1383 ms
[ApiClient] POST /api/v1/shop/purchase → 200 in 1080 ms
[ApiClient] POST /api/v1/user/golfin-grants/ack → 200 in 207 ms
```

Read that top-to-bottom and it is the whole diagnosis: the boot's first call paid the wake
(2819 ms, and the new `SLOW` branch says so out loud), the next call on the woken machine was
220 ms, and the two purchases at 1383/1080 ms each paid a *resume* rather than a *boot* — the ack
immediately after the second purchase, on a machine that was now certainly awake, came back in
207 ms. The line is doing exactly the job §4 asked of it: it separates "the backend was asleep"
from "the UI was slow" without anyone having to guess from a video.

## Warm purchase (ARCHITECT_REVIEW § Open item, measured 2026-08-28)

Two PUTT ACE purchases back-to-back on a hot machine, same route as before (Home →
`ShopPlusButton.onClick` → general shop, flag ON, signed in). The second tap was fired from inside
Unity on **the same `EditorApplication.update` tick** that the first purchase answered — no capture,
no MCP round trip and no pause between them, so the Machine could not suspend in the gap.

```
[ApiClient] POST /api/v1/shop/purchase → 200 in 586 ms
[ApiClient] POST /api/v1/user/golfin-grants/ack → 200 in 140 ms
[ApiClient] PUT /api/v1/user/golfin-inventory → 200 in 186 ms
[ApiClient] POST /api/v1/shop/purchase → 200 in 246 ms
[ApiClient] POST /api/v1/user/golfin-grants/ack → 200 in 100 ms
```

The probe that drove it, showing the gap was zero:

```
[WARM] tap#1 at t=0.000s  RP=788  interactable=True
[WARM] tap#1 same-frame: interactable=False label='…'
[WARM] purchase#1 answered at t=0.610s  RP=753
[WARM] tap#2 at t=0.610s (same tick, no pause)
[WARM] tap#2 same-frame: interactable=False label='…'
[WARM] purchase#2 answered at t=0.860s  RP=718  interactable=True label='BUY'
```

**Second purchase: 246 ms → `keep-alive follow-up CLOSED`.** The §8 threshold is 400 ms and the warm
number is well inside it, so the UnityWebRequest keep-alive investigation does not open.

This also settles the § Open question above. The 1383 / 1080 ms figures were resumes, exactly as
suspected: purchase #1 here still carried some warm-up at 586 ms, and purchase #2 — issued while the
connection was genuinely hot — came back at 246 ms, in line with the 140 / 100 ms acks either side of
it. A warm purchase is a quarter-second round trip; everything above that was the machine waking up,
which is what Part A shortened from 5.2 s to 1.2 s.

Side effect: two more stacking PUTT ACE balls, 70 RP (788 → 718).

## Pending-state proof (frame probe, site 1)

Programmatic, not a reading of pixels — logged from play mode around the real `onClick`:

```
[SCROLL-PROBE] viewport y[295.0,1969.0] buyBtn y[941.0,983.0] insideVertically=True
[PENDING2] BEFORE      interactable=True  label='BUY'
[PENDING2] SAME-FRAME  interactable=False label='…'
[PENDING2] AT-CAPTURE  interactable=False label='…'
[ApiClient] POST /api/v1/shop/purchase → 200 in 1080 ms
[PENDING2] SETTLED     interactable=True  label='BUY'
```

The scroll probe is there because the first attempt captured a frame in which the tapped card's BUY
was scrolled out of the viewport — the state was right and the picture proved nothing. The card was
scrolled into view and the geometry asserted before the frame was cited.

Refusal, same probe, transport nulled to simulate offline:

```
[REFUSAL] BEFORE      interactable=True label='BUY'
[ShopTransaction] 'shop_ball_putt_ace' not purchased: Unavailable.
[REFUSAL] SAME-FRAME  interactable=True label='BUY'
[REFUSAL] SETTLED     interactable=True label='BUY'  RP=788  transport restored
```

## Known gaps

- **Sites 2–6 have no live pending-state screenshot.** Each one would need a real spend on Cesar's
  live account with a permanent effect: a character or club level-up (irreversible progression), a
  mode entry (drops into a round), a tournament sign-up (enters the field), or a stamina boost on
  the selected character. Site 1 was chosen precisely because a ball *stacks*, so it could be bought
  twice for 35 RP each and left the account no worse. The wiring for 2–6 is the same helper on the
  same code path, is visible in the diff, and the helper's behaviour is covered by 9 green tests —
  but a reviewer should treat the *pictures* for those five as absent, not as taken.
- **`insufficient` / `price_changed` refusals not exercised live** — see the checklist row.
- **No device pass.** Unity-verified against the live API is the bar here.

## Spec deviations

1. **`PointsSpendGate.IsSpendInFlight` added** (spec did not ask for it). The gate's `_inFlight` latch
   *silently drops* a second concurrent `Spend` — neither `onApproved` nor `onDenied` fires. A pending
   scope opened for a spend that is then dropped is never disposed, so the button would stay disabled
   forever: the affordance would create a worse bug than the one it fixes. The three gate-routed sites
   therefore skip `Begin` when a spend is already in flight. The property is read-only and `Spend` is
   otherwise unchanged.
2. **`UpdateBuyButtonStates()` re-asserted after `Dispose` in the stamina controller.** `onGranted` (and
   `OnRpChanged`, fired by the debit) both run *inside* the pending scope, so the restore would hand
   back "enabled" over a stamina-full disable. One idempotent extra call, not a behaviour change.
3. **`PendingSpend` caches all states before writing any** (two passes). A prefab variant can wire the
   same component as both `playButton` and `cardTapButton`; a one-pass read-then-write would cache the
   second occurrence as "already disabled" and restore it disabled. Covered by
   `TheSameButtonPassedTwice_IsStillRestored`.
4. **Level-up modals lock CANCEL only, not RESET.** The spec says "the modal's close/cancel button(s)";
   RESET was in an earlier draft of this change and was removed to stay inside that wording.

## Console output

No errors or warnings attributable to this task. The only new warnings are the intended ones:

```
[ApiClient] SLOW: GET /api/v1/content → 200 in 2819 ms (cold start?)
[ApiClient] SLOW: GET /api/v1/tournaments/golfin → 200 in 2543 ms (cold start?)
[ApiClient] SLOW: GET /api/v1/notices → 200 in 2544 ms (cold start?)
[ApiClient] SLOW: GET /api/v1/banners → 200 in 2548 ms (cold start?)
```

Two compile errors were found and fixed during the work, before anything was reported green:
`HttpResponse.StatusCode` is a `long` (not `int`), and the new test asmdef needed explicit
`Unity.TextMeshPro` + `UnityEngine.UI` references.

## Side effects on Cesar's live account

Two PUTT ACE balls bought for 35 RP each (they stack; RP 858 → 788) and one purchase refused with
nothing charged. This is the minimum that produces a real round-trip to photograph.

## Open questions for Architect

- ~~Nobody had measured two purchases back-to-back on a hot machine.~~ **ANSWERED 2026-08-28** — see
  § Warm purchase. Warm purchase = 246 ms, `keep-alive follow-up CLOSED`. No open questions remain.

## Notes on how this was verified

The Unity Editor was **closed** at session start (no process; port refused), so it was launched here.
The `ai-game-developer` MCP tool surface is not registered in this session, so Unity was driven through
`unity-mcp-cli@0.90.0 run-tool` over the same HTTP API. The compile check and the full EditMode suite
were run in `-batchmode` (the interactive Editor would not auto-refresh on focus). Captures went
through `EditorApplication.ExecuteMenuItem("GOLFIN/Screenshot/Capture Game View")` — the path Capture
Rule 0 explicitly permits — fired from an `EditorApplication.update` tick two frames after the real
`onClick`, because the round trip is far shorter than a round trip through the MCP transport. Play mode
was exited and the working tree left with no scene, prefab or settings drift.
