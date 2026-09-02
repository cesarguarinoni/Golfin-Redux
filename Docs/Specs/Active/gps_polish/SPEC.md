# SPEC — `gps_polish`

> **Authoritative spec for this task.** Implementer reads this and ONLY this for the work definition. STATUS.md tracks pipeline state. Reports/reviews go in their own files.

## Status

See `STATUS.md`. (Standard pipeline states — SPEC_READY → IMPLEMENTER_WORKING → … → DONE.)

## Goal

Make the GPS surface **move** like the rest of the game and finish its rough edges: one shared motion helper, a layered push between GPS screens, cross-fades where the screens currently snap, pop-in modals, pending-state CTAs, count-ups on the numbers that change, shimmer placeholders where a fetch leaves a blank panel, and a sweep of the small things (safe area, scroll feel, keyboard). No new feature, no new screen, no new package. Every GPS screen must feel like it belongs to the game after this, and nothing about what a screen *does* may change.

This is the map Cesar approved on 2026-09-02, with his three calls baked in:

- **Fade-to-black stays at the game↔GPS boundary** (Home → GpsHub, and any GPS screen → a non-GPS screen). The game-wide `FadeController` convention is untouched.
- **Layered push INSIDE the GPS surface** — "let's gamble it". Backgrounds cross-fade in place, only the content layer moves. If it looks bad in the review video, the fallback is the plain fade — but build the push first.
- **No haptics.** They land in the game and GPS together, behind a Settings toggle, as their own task (`haptics_option`, Notion 2130). Do not add `Handheld.Vibrate` or any native shim here.

## Reference

- **No Figma nodes.** This is a motion and polish task; the built GPS screens at HEAD are the visual reference and must look pixel-identical at rest (the `fidelity/` side-by-sides in `Completed/gps_profile_pack`, `auth_golf_profile` and `gps_gifts_votes` are the rest-state ground truth — a polish pass that shifts a rest pixel is a regression).
- **Motion already in the project — copy the feel, not the code:** `VersusResultModalController` (Stage 3 pop-in: scale 0.9→1.0, 0.2 s ease-out, independent alpha), `DailyMissionPillController.SlideRoutine` (the eased slide shape `ModeCarouselController.LerpToTargetLayout` uses, plus the glow pulse `SetGlowAlpha`), `GachaRevealModalController` (staggered card pops), `ToastController.Fade` (CanvasGroup fade), `ScreenManager` + `FadeController` (the boundary fade, `_defaultDuration` 0.5 s), `PendingSpend` (`Assets/Scripts/UI/Polish/` — the `…` pending label + disable pattern from `transaction_feedback`), `ButtonPressFeedback` (press scale; already on every GPS button — leave it).
- **Structure to exploit:** every GPS screen prefab already splits into `Background` / `ContentContainer` / `GpsNavBar` at the root (`GpsHubScreen` also has `BackPill`; the two Auth-extras screens have no nav bar). `ScoreUploadScreen` is the exception — its six `Step*_` roots sit beside `ContentContainer` and each carries its own background (see `ScoreUploadScreenBuilder.StepBackgrounds`).
- **Where navigation happens:** `ScreenManager.Navigate` (the one entry point; `ShowScreen`/`GoBack`/`NavigateToPillar` all funnel into it), `GpsGate.IsGpsScreen(id)` (the membership test for "both ends are GPS"), `ScoreUploadFlowController.Show(step)` (the `_stepRoots[i].SetActive` loop, line ~279), `GpsHubScreenController` (`ShowScreen(GpsProfile)`, `ShowScreen(ScoreUpload)`, `GoBack(Home)`), `GpsProfileScreenController` (`GoBack(GpsHub)`, `ShowScreen(GpsBadges|GpsAvatar)`).

## Design

### D1 · `UiMotion` — the one helper (Assembly-CSharp, `Assets/Scripts/UI/Polish/UiMotion.cs`)

Static coroutine helpers, no MonoBehaviour state, no tween package, no `Animator`:

| Call | What | Constants (the ONLY copies) |
|---|---|---|
| `Fade(CanvasGroup, from, to, dur)` | alpha lerp, ease-out cubic | `FadeDur = 0.15f` (cross-fade), `EntryDur = 0.25f` |
| `Pop(RectTransform, CanvasGroup?, dur)` | scale 0.9→1.0 + alpha 0→1, ease-out cubic; sets `localScale = one` on completion or interruption | `PopDur = 0.20f` |
| `Slide(RectTransform, fromX, toX, dur, easeOut)` | anchoredPosition.x lerp, the `SlideRoutine` shape | `PushDur = 0.25f` |
| `Rise(RectTransform, CanvasGroup, dy, dur)` | anchoredPosition.y from −dy→0 with alpha 0→1 | `RiseDy = 16f` |
| `CountUp(TMP_Text, from, to, dur, format)` | integer tween, ease-out, snaps to `to` on completion | `CountDur = 0.40f` |
| `Stagger(IList<…>, perItemDelay)` | fires a per-item routine with a delay | `StaggerDelay = 0.03f`, cap 12 items |
| `Pulse(CanvasGroup glow, min, max, cycles, dur)` | the pill's glow curve, N cycles then rest at `min` | `PulseDur = 0.6f` |

Every routine takes `unscaledDeltaTime` (modals open while `timeScale` may be 0), is interruption-safe (a new call on the same target must `StopCoroutine` the previous via a per-target handle — pattern: `UiMotion.Run(MonoBehaviour host, ref Coroutine handle, IEnumerator)`), and completes to the exact final value when the host is disabled mid-tween (`OnDisable` hooks set final state). Respect a static `UiMotion.Enabled` flag (true; the existing `ReducedMotion`/accessibility setting if one exists — grep first; if none, the flag simply exists for the game_polish task to wire).

**Retrofit rule:** do NOT rewrite the three existing pop/slide implementations to use `UiMotion` — that is `game_polish`. This task only adds the helper and uses it on GPS surfaces.

### D2 · Layered push between GPS screens (`ScreenManager` + `GpsScreenTransition`)

In `ScreenManager.Navigate`, after the gates and history bookkeeping, add ONE branch before the fade:

```
if (!instant && GpsGate.IsGpsScreen(_currentScreen) && GpsGate.IsGpsScreen(screenId)
    && GpsScreenTransition.CanPush(current, target))
    → StartCoroutine(GpsScreenTransition.Push(currentGO, targetGO, direction, ApplyScreen))
else → existing FadeOutThenIn path (unchanged)
```

`GpsScreenTransition` (new, `Assets/Scripts/UI/Gps/GpsScreenTransition.cs`):

1. **Direction.** `Forward` when pushing (`push == true` and target is not the hub); `Back` for `GoBack` and for any move whose target is `GpsHub`; between two hub-nav tabs (ScoreUpload / GpsGift / GpsVote / GpsProfile) direction follows nav-bar slot order (left→right = Forward). Forward: target content enters from `+W`, current content exits to `−W × 0.3` (parallax); Back is mirrored. `W = ContentContainer.rect.width`.
2. **Layers.** For the duration (`PushDur`, 0.25 s): target GO active alongside current; target `Background` (+ `GpsNavBar` + `BackPill` if present) `CanvasGroup` alpha 0→1 IN PLACE while current's fade 1→0 (`FadeDur`); only the two `ContentContainer`s slide. Nav bars are visually identical across GPS screens, so the cross-fade reads as a static bar — verify by pixel (A5).
3. **Completion.** `ApplyScreen(target)` runs at the END (not the midpoint), then every moved/faded rect and CanvasGroup on BOTH screens is reset to rest (position 0, alpha 1). Rest state after the push must be byte-identical to a rest state reached by `instant: true` (A2).
4. **Input.** A `CanvasGroup.blocksRaycasts = false` on both content containers during the push; the Top UI stays interactive. A second `Navigate` during a push completes the running one instantly (snap to rest) and starts the new one — no queue.
5. **`CanPush` returns false** (→ existing fade) when either prefab lacks the `Background`/`ContentContainer` split, when `ScoreUpload` is one end (its step roots own their backgrounds — the fade is right for it), or when `UiMotion.Enabled` is false.
6. **Boundary unchanged:** Home → GpsHub, GpsHub → Home (`GoBack(Home)`), Welcome Skip → Home, any GPS → Login/Loading keep the fade-to-black. `GpsGolfProfile` → `GpsWelcome` → `GpsHub` ARE pushes (both GPS).

### D3 · Screen entry motion

On every GPS screen's `OnEnable` (hub, profile, avatar, badges, gift, vote, golf profile, welcome): `ContentContainer` does `Rise` (`EntryDur`, 16 px) — **only when the screen was reached through the fade path** (boundary entry); after a push the content already animated in, so `GpsScreenTransition` sets a one-shot `SkipEntry` flag the controller consumes. List rows and grid cells that appear from a fetch (`GpsHubRoundRow` rows, `BadgeCellView` cells, gift Popular Golfers rows, vote `VoteCardView` cards, Top Supporters rows) `Stagger`-rise on first paint of a fetch result (30 ms apiece, cap 12), never on a repaint of cached data (paint-cache hits are instant — the hub's paint-cache → subscribe → fetch pattern makes this a one-line check).

### D4 · Sub-navigation cross-fades

- `ScoreUploadFlowController.Show(step)`: replace the `SetActive` loop with a cross-fade — outgoing step root `Fade` 1→0 then `SetActive(false)`, incoming `SetActive(true)` + `Fade` 0→1, overlapping (`FadeDur`). Each step root gets a `CanvasGroup` (builder change). The step-strip active indicator `Slide`s to the active step instead of jumping (builder: the indicator becomes one moving RectTransform under the strip, positions computed from the step pills' anchored X).
- Gift screen: the `BUY GIFT ITEMS` strip and `TOP SUPPORTERS` / `POPULAR GOLFERS` panels fade in with their data (D3 covers rows).
- Vote screen: filter chip change (`PUBLIC`/`MINE`) cross-fades the list (`FadeDur`), cards stagger (D3).

### D5 · Modals and strips

- `ModalController` gains an opt-in `animateShow` (default **false** so no non-GPS modal changes): when true, `Show()` runs `Pop` on `modalPanel` and `Fade` 0→1 on `backdrop`; `Hide()` runs the reverse (`Fade` backdrop 1→0, panel scale 1→0.95 + alpha 1→0, `FadeDur`) and deactivates on completion. Enabled on `VenuePickerModal`, `GiftSendModal`, `VoteCreateModal` (builder sets the flag). `IsVisible()` must be true from the first frame of Show and false from the first frame of Hide (state, not animation, drives `OpenModalCount`).
- Score Upload strips (`_readingSub`, `_foundStrip`, `_votePanel`, `_stepStrip` when it toggles): `Rise` in / reverse out instead of `SetActive` snaps.

### D6 · Button and selection states

- Every network-calling CTA on the GPS surface goes through `PendingSpend.Begin(button, label, …)` while its call is in flight, if it doesn't already: SAVE PROFILE, GET STARTED (no call → skip), Score Upload POST/CONFIRM, Gift `CONFIRM`, Vote `VOTE` and `CREATE`, Venue picker row tap. Audit each call site; report a table (button → call → pending wired before/after).
- Selection pills (avatar colour swatch, experience chips, vote filter chips, gift amount buttons `50/100/500/1000`): on select, `Pop`-style bump (scale 1.0→1.06→1.0 over 0.10 s) on the selected element and a `FadeDur` colour cross-fade between the unselected/selected sprites (two Images, alpha swap — do NOT tint sprites, Build rule 2 from `gps_profile_pack`).

### D7 · Live-value moments

- `CountUp` on: hub points/RP figure, Gift `GIFTS RECEIVED` pts, gift `Your balance`, vote `+10 pts` earned reflected in the Top UI RP (the Top UI is shared — call the same `CountUp` on `PersistentUIManager`'s RP label ONLY when the delta originates from a GPS action; the game's own RP updates are `game_polish`), Score Posted total (`Pop`), badge count on Profile.
- A newly earned badge cell (`BadgeCellView` whose `earned` flips true between two paints) `Pulse`s once (2 cycles).
- Vote bars animate their fill width from the previous value to the new one after a cast (`CountDur`).

### D8 · Loading and empty states

- Shimmer placeholders: a `ShimmerBlock` prefab (rounded rect `ADark(black, 0.35)` with a moving highlight band `A(white, 0.08)`, 1.2 s loop, `Image` + `Mask`, no shader) shown for the first fetch on: hub Rounds list (3 rows), Badges grid (6 cells), Top Supporters (3 rows), Popular Golfers (3 rows), Vote list (2 cards). Replaced by the real rows on paint; never shown when the paint cache has data; hidden on error in favour of the existing error/empty label, which `Fade`s in.
- Empty labels (`_roundsEmpty`, the gift "from 0 supporters", the vote empty state) `Fade` in rather than appear.

### D9 · General sweep (report a per-screen table)

- Safe area: every GPS screen's `ContentContainer` and `GpsNavBar` respect `Screen.safeArea` the way the game shell does (`safe_area_top_bar` — reuse its component, do not re-derive); verify with the Editor's iPhone 15 Pro Max simulator view.
- Scroll rects (hub rounds, badges, gift, vote): `movementType = Elastic`, `elasticity 0.1`, `inertia on`, `decelerationRate 0.135`, `scrollSensitivity` equal to Inventory's — copy the Inventory values, quote them.
- Keyboard: Golf Profile nickname + handicap fields and Vote CREATE fields scroll into view above the iOS keyboard (`TMP_InputField.shouldHideMobileInput` is already true; add the content offset on `onSelect`/`onDeselect` using `TouchScreenKeyboard.area` when available, no-op in Editor).
- Text: any GPS `TMP_Text` still on `Rubik:Medium` renders the variable face (~5 % narrow, backlog row). NOT fixed here — but list every Medium site in the report so the fix is one import later.

## Localization

No new player-facing strings are expected. If any polish needs one (e.g. a "Loading…" fallback), Build rule 7 applies verbatim: `LocalizationText.csv` EN+JA → importer PLAN → APPLY → publish `texts` → `--check` clean. Quote the PLAN verdict either way (`add 0` is the expected line).

## Architecture context

- New files: `Assets/Scripts/UI/Polish/UiMotion.cs`, `Assets/Scripts/UI/Gps/GpsScreenTransition.cs`, `Assets/Prefabs/UI/Gps/ShimmerBlock.prefab` (+ builder function in a new `Assets/Scripts/UI/Gps/Editor/GpsPolishBuilder.cs` that ADDS the CanvasGroups/indicators/shimmers to the existing prefabs by re-running the existing builders' outputs — do not fork the builders; each existing builder stays the prefab source of truth, so the additions go INTO those builders (`GpsProfilePackBuilder`, `GpsAuthExtrasBuilder`, `GpsGiftVoteBuilder`, `ScoreUploadScreenBuilder`, the hub builder) as a shared `GpsPolishBuilder.Apply(root)` call at the end of each).
- Touched: `ScreenManager.Navigate` (one branch), `ModalController` (opt-in animate), `ScoreUploadFlowController.Show`, every `Gps*ScreenController` `OnEnable`/paint path, `GiftSendModalController`, `VoteCreateModalController`, `VoteCardView`, `BadgeCellView`, `GpsHubRoundRow`, `PersistentUIManager` (RP `CountUp` entry point).
- No new asmdef. `UiMotion` lives in Assembly-CSharp beside `PendingSpend`; if `Golfin.Social`/`Golfin.Gps` code needs it, it doesn't — motion is UI-only.
- EditMode: `UiMotionTests` (easing endpoints, interruption completes to final value, `Enabled=false` short-circuits to final state in one frame, `Stagger` cap), `GpsScreenTransitionTests` (direction table — every ordered pair of GPS ids → Forward/Back/Fade, pinned; `CanPush` false for ScoreUpload and for a prefab without the split), `ModalController` animate flag default-false pinned.

## Acceptance checklist (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] **A1 · Motion invariants JSON.** A play-mode `GpsPolishProbe` (Editor run, real navigation through `ShowScreen`/`GoBack`, like `GpsAuthExtrasEditorRun`) drives every transition in the direction table and writes `Docs/Diagnostics/_capture/gps_polish_invariants.json`: per transition — measured duration within ±2 frames of the constant, target content X at t=0 equals ±W, both content X == 0 and both alpha == 1 at completion, `blocksRaycasts` restored, no frame where BOTH backgrounds have alpha < 0.5 (the seam test). `fail == 0` quoted; this JSON is the gate, the video is the artifact.
- [ ] **A2 · Rest-state parity.** For every GPS screen: a capture after an animated arrival vs a capture after `ShowScreen(id, instant: true)` — pixel diff `0` (or quote the non-zero and why). Reuse `figma_diff.py`'s diff path.
- [ ] **A3 · Boundary untouched.** Home → GpsHub and GpsHub → Home log the `[ScreenManager] Fading to` line, not a push; `FadeController` unmodified (`git diff --stat` shows no change).
- [ ] **A4 · Videos** (`videos/`, ≥50 KB each, drawtext-captioned per the Rule 17 idiom): (a) hub → Profile → Badges → back → back, (b) hub nav bar tab sweep ScoreUpload → Gift → Vote → Profile, (c) Score Upload step walk, (d) Venue picker + Gift send + Vote create modals open/close, (e) Golf Profile → Welcome → hub entry, (f) a cast with the bar fill + RP count-up. One still per video in `screenshots/`. **This is what Cesar judges the gamble on** — (a) and (b) are the push.
- [ ] **A5 · Nav-bar seam.** During (b), the row of pixels through the nav-bar icons measured at 3 frames mid-push differs from rest by mean |ΔRGB| ≤ 2 (the bar must read as static).
- [ ] **A6 · UI fidelity lint** `fail=0` on every GPS prefab after the builder changes (they all get re-run); geometry audits of the existing screens unchanged (`N sites 0 FAIL 0 GONE`, same N as their last report).
- [ ] **A7 · Pending-state table** (D6) — every GPS CTA that calls the network, with before/after, and one captured frame of the `…` state.
- [ ] **A8 · Shimmer** — a frame of each of the five shimmer sites during a cold fetch (clear the paint cache first; quote how), and a log line proving the cache-hit path skips it.
- [ ] **A9 · Modals** — `OpenModalCount`/`IsVisible()` timing pinned by test; `animateShow` default false pinned; no non-GPS modal prefab changed (`git status` on `Assets/Prefabs` quoted).
- [ ] **A10 · Sweep table** (D9) — per screen: safe-area component present, scroll values, keyboard offset wired (fields), Rubik Medium sites listed.
- [ ] **A11 · Importer** PLAN verdict quoted (expected `add 0`), `--check` clean.
- [ ] **A12 · EditMode** full sweep green + the new suites executed by name (tripwire, as `auth_golf_profile` did).
- [ ] **A13 · Perf** — Editor profiler over video (b): no GC alloc per frame from the tweens after warm-up (coroutines allocate once at start; quote the frame with the highest alloc), and no frame > 16.7 ms attributable to the push on the iPhone 15 Pro Max simulator view. If the push allocates per frame, fix it (cache `WaitForEndOfFrame`, no closures in the loop).
- [ ] **A14 · Deviations** flagged with justification; `gps_pill_entry` closed (STATUS → DONE, folder → Completed) as the first commit of this task.

## Smoke evidence

Videos (a)–(f) above with one still each; the invariants JSON; the A2 parity diffs; the D6/D9 tables.

## Out of scope (do NOT do these)

- No haptics of any kind (`haptics_option`, later, game + GPS together).
- No retrofit of the Versus / Daily-pill / Gacha motion onto `UiMotion` (`game_polish`).
- No change to `FadeController`, to any non-GPS screen's transition, or to any non-GPS modal.
- No Rubik Medium font import (backlog; list the sites only).
- No new screens, no Rounds tab destination, no Settings edit, no follow UI, no vote NO button.
- No DOTween/LeanTween/Animator/Timeline; no Lottie.
- No Android-specific work.
