# Quick · `push_arrival_hitch` — same-backdrop pushes are janky; the arriver is drawn UNDER the leaver

**Filed:** 2026-09-09 (Architect), **revised 2026-09-09** after Cesar's Gacha Prizes report. **Run after
`polish_regressions_0909`.** Cesar: "Mode Selection → Mission Selection feels clunky … janky and not
smooth" (suspecting the shared background); "the Prizes screen transitions from the right but the prize
seems to be there before and not moving with the screen"; "the back button from Prizes transitions to the
right and looks bad (empty screen on the left)"; "if you pull from the Prizes screen, the animation does
not end up in a new Prizes screen but the old one".

## P0 — the root cause: sibling order (found 2026-09-09, supersedes the "three things" below as the FIRST fix)

`LayeredPush.Push` only re-orders the arriver when the chrome cross-fades:
`if (crossFadeChrome) toGo.transform.SetAsLastSibling();` (`LayeredPush.cs` ~397). On a same-backdrop pair
it does NOT, so the two screens keep their `ScreensRoot` order (ShellScene: … `HoleSelection` 7,
`MissionSelection` 8, `ModeSelection` 10, `TournamentHoleSelection` 11 … `GeneralShop` 16, `GachaHistory` 17,
`GachaPrizes` 18). Each screen's `Background` is an opaque full-screen Image. So whenever the ARRIVER is an
earlier sibling than the LEAVER, the arriver — background AND content — is drawn underneath the leaver's
opaque background for the entire 250 ms. What the player sees is only the leaver's content drifting
`ParallaxFactor 0.3·W` over its own static backdrop, then a hard cut to the arriver at `Settle`. That is:

- `ModeSelection (10) → MissionSelection (8)`: Mode's cards drift left 30 %, cut. "Janky, not smooth."
  (The reverse, `Mission → Mode`, arrives on top and looks right — which is why it reads as a
  direction-specific clunk.)
- `GachaPrizes (18) → GeneralShop (16)` on BACK: the Prizes content drifts right 30 %, exposing its own
  background on the left — "empty screen on the left" — then cut to the carousel.
- Rule: a push is occluded iff the arriver's `ScreensRoot` index is LOWER than the leaver's. Same shape:
  `ModeSelection (10) → HoleSelection (7)`, `ModeSelection (10) → MissionSelection (8)`,
  `TournamentHoleSelection (11) → HoleSelection/MissionSelection/ModeSelection` back,
  `GachaHistory (17) → GeneralShop (16)` back, `GachaPrizes (18) → GachaHistory (17) / GeneralShop (16)` back.
  Enumerate every push pair from `LayerMap` × `ScreensRoot` order in the report, both directions
  (§22 shape audit — list the on-top ones too).

a's invariants never caught it because `chromeAlphaMin`/`seamWorstCover` sample CanvasGroup alphas, not
what is on top; the parity diffs compare REST frames; and the a4 strips Cesar was shown are cross-backdrop
pairs (which DO re-order).

**Fix (P0):**
1. `SetAsLastSibling()` on the arriver on EVERY push (restore at `Settle` — `ToSiblingIndex` already does).
2. With the arriver on top and its backdrop identical to the leaver's, hold the arriver's chrome at alpha 0
   for the whole push (`ChromeGroups[i].alpha = 0` before activation, as the cross-fade path already does)
   so the leaver's content stays visible under the incoming content — the seam invariant holds because
   the LEAVER's chrome is at 1 throughout; `Rest()` puts the arriver's chrome back to 1. Without this
   step the arriver's opaque background would cut the leaver's content away on frame 1, which is the
   same defect mirrored.
3. `Dir.Back` is the same code path; verify both directions on every pair.
4. New invariant per record: `arriverOnTop = true` (sibling index of `ToGo` == last during the tween) and
   `arriverChromeAlphaMax` during the tween == 0 on same-backdrop pairs. `LayeredPushTests`: one test that
   an earlier-sibling arriver is last-sibling during the push and restored after.

## P1 — Gacha Prizes (three reports, two causes)

**Pull again from the Prizes screen shows the OLD prizes.** `GachaPullFlow.ShowPrizes` →
`ScreenManager.ShowScreen(ScreenId.GachaPrizes)` while `_currentScreen == GachaPrizes` hits
`if (_currentScreen == screenId && !instant) { "Already on … ignoring"; return; }` (`ScreenManager.cs` 341), so
`GachaPrizesScreenController.OnEnable` — the only place `s_result` is bound (`ApplyMode`) and
`s_pendingEntrance` consumed — never runs. Nothing on this path changed since `f11079114`; say in the report
whether it reproduces on that commit (it should — it is a latent bug, not a polish regression).
Fix: `GachaPrizesScreenController` exposes `public void Rebind()` = the OnEnable body (derive count, `ApplyMode`,
start `PlayEntrance` when pending); `ShowPrizes` calls `Rebind()` on the live instance when
`ScreenManager.Instance.CurrentScreen == ScreenId.GachaPrizes`, else `ShowScreen`. `OnEnable` calls `Rebind()`
so there is one binding path.

**Prize "already there, not moving with the screen" on the first arrival.** Two motions on one object again:
`GeneralShop → GachaPrizes` is a same-backdrop push (both `5ec22d10…`), so `GameScreenContent` slides in from
the right, while `PlayEntrance` (armed by `SetPendingResult`, consumed in `OnEnable` — which runs INSIDE
`Push`'s `SetActive(true)`) pops every card from scale 0 / alpha 0 in place during the same 250 ms. The cards
therefore bloom in the middle of the slide and the eye reads them as spawning after the panel. And the push
is not wanted here at all: `ShowPrizes`'s own comment says the screen "binds and activates UNDER the
still-opaque scrim and is revealed by the modal's fade". Fix: `ShowPrizes` navigates with `instant: true`
(the reveal modal's scrim is up; the fade-out of the modal is the transition), keep the card pop entrance
(it now plays under the modal's fade, as designed). The BACK from Prizes stays a push (P0 makes it correct).

**Back from Prizes "empty screen on the left"** — P0, `GachaPrizes (18) → GeneralShop (16)`. No separate fix;
prove it with the P0 before/after strip on this exact pair.

## What it is (measured, not the background) — the remaining three, still valid, applied AFTER P0


The shared background is innocent: on a same-backdrop pair `LayeredPush` animates NO chrome at
all (`chromeAlphaMin = 1` on every frame of every Play-pillar record in
`game_polish_a_invariants.json`). The jank is three things stacked on the first frames of a 15-frame
slide:

1. **The arriving screen is built INSIDE the first tween frame.** `LayeredPush.Push` (`LayeredPush.cs`
   ~line 410) does `toGo.SetActive(true)` — which runs `MissionSelectionScreenController.OnEnable`:
   `MissionCatalog.EnsureLoaded`, the card instantiation, `RefreshDaily` — and then starts the
   `while (elapsed < PushDur)` loop in the same frame. a's own perf run has the number:
   `ModeSelection->MissionSelection: alloc=10.4 MB over 12 frames, worst frame 68.75 ms`
   (second run 51.8 ms). The first `elapsed += unscaledDeltaTime` after a 50–70 ms frame jumps
   the ease-out 20–28 % of the way in one step, so the content appears to *teleport* a fifth of
   the distance, then glide. That is the "clunk".
2. **b's front-door stagger runs DURING the slide.** `MissionSelectionScreenController` staggers
   the cards (`GpsPaintMotion.StaggerRise`) on the first paint of every entry, and the daily
   card's shimmer/fade fires too (R2 of `polish_regressions_0909` removes the shimmer). The
   `EnteringViaPush` flag is consumed only by `ScreenEntryMotion` (the 16 px rise), not by the
   stagger — so the panel slides while every card inside it rises. Two motions on one thing.
3. **Parallax on a static backdrop.** The leaver exits at `ParallaxFactor 0.3` while the arriver
   enters at 1.0. With a background that does not move, the eye has a fixed reference and the
   two speeds read as a stutter rather than depth. GPS gets away with it because its chrome
   cross-fades; the Play pillar's does not.

## Fix

1. **Build before you move.** In `LayeredPush.Push`: activate the target, `Canvas.ForceUpdateCanvases()`,
   then `yield return null` ONCE (the heavy frame), and only then start the slide clock with
   `elapsed = 0`. Content is parked at `RestX + enterOffset` before the activation (it already
   is), so the one held frame shows nothing move. The invariants gate defines t0 as the first
   SLID frame; `measuredDur` stays within ±2 frames of `PushDur` and the record gains
   `arrivalFrameMs` (the held frame's cost) so the number is visible per pair. Cap the first
   tween step: `elapsed` advances by `min(unscaledDeltaTime, 1/30)` so a late hitch can never
   jump more than 2 frames' worth (state this in the invariants as `maxStepFrac`).
2. **No stagger under a push.** `GpsPaintMotion.StaggerRise` (and `PanelReveal`) consult
   `LayeredPush.EnteringViaPush` the way `ScreenEntryMotion` does: when the screen is arriving by
   push, rows land in place (instant) — the slide IS the entrance. The front-door stagger (Cesar's
   rule) stays on FADE-path arrivals and pillar resets. `MissionSelection`, `ModeSelection`,
   `HoleSelection`, `TournamentSelection`, `GeneralShop`, `GachaHistory`, `Rankings` all route
   through the same check; log line per site `paint(local) — instant (push)`.
3. **Parallax 0 on same-backdrop pushes.** `LayeredPush.Push` uses `ParallaxFactor` only when the
   backdrops differ (the chrome cross-fade case); on a same-backdrop pair the leaver exits at the
   full width so the two contents move as ONE strip over the fixed background. Record it as a
   5 s A/B clip (0.3 vs 0) on `ModeSelection → MissionSelection → back` so Cesar can overrule —
   ship 0 unless he does.
4. **Cheapen the arrival where it is cheap to.** `MissionSelectionScreenController` — build the
   cards once per session and rebind on later entries (the catalog is local and does not change
   under the player); same for `ModeSelectScreenController` if it rebuilds. Quote before/after
   `arrivalFrameMs` for the four Play-pillar screens.

## Done when

- **P0:** per-frame strip (5 frames) of `ModeSelection → MissionSelection` and `GachaPrizes → GeneralShop`
  (back) before/after — the arriver's content visibly sliding over the leaver's; the enumerated pair × order
  table with occluded-before / on-top-after for every same-backdrop pair in both directions; the new
  invariant on every record; the sibling test.
- **P1:** pull-again on the Prizes screen re-binds to the new result (log: `[GachaPrizesScreenController] Rebind x10`
  with a different first prize id than before); `ShowPrizes` logs `instant` on the first arrival; a strip of the
  Prizes arrival under the modal fade; BACK from Prizes covered by P0.

- `game_polish_a_invariants.json` regenerated (`GamePolishProbe` push mode): `fail = 0`, every
  record carries `arrivalFrameMs` + `maxStepFrac ≤ 2/PushDur·(1/60)`; `ModeSelection →
  MissionSelection` first slid step ≤ 2 frames' worth of travel, quoted.
- Per-frame content-X log for that pair, before/after, showing no step larger than the next
  (monotone ease-out, no initial jump).
- The A/B parallax clip + Cesar's pick; the four arrival costs before/after.
- Stagger-under-push log lines; rest parity unchanged (0 px vs the b baselines on the Play
  pillar); `game_polish_a`'s `LayeredPushTests` + b's `GpsPolishMotionTests` green, one new test
  per fix (held frame, capped step, parallax 0 on same-backdrop, stagger suppressed under push).
- `git status`: `LayeredPush.cs`, `PaintMotion.cs` (the check), the screen controllers, the
  probe, tests. Nothing under `Gps/`.
