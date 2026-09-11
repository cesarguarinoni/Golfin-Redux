# home_carousel_and_daily_timing — Quick task (2026-09-11)

Reported by Cesar in chat, with a device screenshot:

1. *Mode selection in Main Screen is overlapping the middle button of the bottom nav bar (seems to
   have gotten lower at some point).*
2. *The daily mission in Mission Select pops up with weird timing when entering the screen. It
   should appear at the same time as the mission list and not suddenly appear and displace it.*

## 1 · Home mode cards on the Tee button

**Reproduced in the Editor only at 1290×2796** (the screenshot was from a Dynamic-Island phone —
its ticket cluster sits 36 px below the R-pill, which is the `SafeAreaFitter` baseline-141 nudge).
At 1170×2532 the layout is exactly the design (cards' bottom 24 px above the Tee button), which is
why neither the Editor nor an iPhone 14 ever showed it.

**Cause.** `BannerSlotBinder.SetShiftedDown` (the Home strip's "drop into the hidden banner's
place") measured its drop **once** and cached it — and the first call is the scene-load one:
`HomeScreen` is authored active, so the binder's `OnEnable` runs on frame 0, **before
`CanvasScaler` has scaled the canvas**. On that raw canvas `ModeCarouselSection`'s proportional
anchors (0.2275 → 0.8764 of the screen height) put it 60 px higher than on the scaled one, while
the pixel-anchored banner does not move: the drop measured **296** instead of **237** and the cards
landed 35 px *under* the Tee button's top. Live numbers, Editor at 1290×2796: `dist=296.06`,
section y `−336.06`, cards' bottom 241 canvas px vs Tee top 274.

**Second defect of the same rect (found on the way).** `ScreenEntryMotion` rises the very same
`ModeCarouselSection` (and `NoticePanel`, `PromoBanner`, `DailyMissionPill`) on every Home entry
— in the same `OnEnable` frame the binder writes its drop and the pills seat themselves under the
notice. `UiMotion.Rise` froze its rest Y at the call and wrote it back on its last line, so a
placement written during the 250 ms rise was undone, and a placement *read* from a rising rect
was 16 px low.

**Fix.**
- `UiMotion.Rise` — the rest is no longer frozen: any external write to `anchoredPosition.y`
  during the rise becomes the new rest (the rise finishes its remaining offset from there);
  `UiMotion.RestY(rect)` exposes the rest for readers. Per-frame allocation still 0 (guarded).
- `BannerSlotBinder.SetShiftedDown` — re-measures on **every** hide from the live geometry (both
  rects read at rest); the base is captured at rest. The frame-0 number is now irrelevant: the
  first real Home entry measures on the scaled canvas.
- `DailyMissionPillController.ComputeTargetY` — reads the notice's rest (`UiMotion.RestY`), which
  also fixes `LoanOfferPillController` (it derives from it).

Verified live at 1290×2796 and 1170×2532: section y `−276.9` / `−276.0`, cards' bottom at 300
canvas px, 24 px clear of the Tee; Home → Roster → Home re-entry settles on the same value; pill at
its computed y (−737). Before/after crop and the clip are in `media/home_carousel_and_daily_timing/`.

## 2 · Mission Select daily card

**Cause.** `RefreshDaily()` deactivated the daily card for the round trip; the campaign list
painted at the top of the column and, 0.2–1.6 s later (measured: 1.6 s in the Editor), the card
was activated on top of it and shoved the list 439 px down. `polish_regressions_0909 R2` removed
the §D4 shimmer block that had been holding that space — the shimmer went, and so did the
reservation.

**Fix** (`MissionSelectionScreenController`):
- The answer is usually already known — the Home pill fetches on every Home entry — so
  `MissionsClient.LastDaily` (the last successful `GET /missions/daily`, not a cache with a
  policy) lets the card paint **in the frame the screen opens**, rising with the campaign rows
  as the first row of the same stagger (instant under a push, like the rows).
- Cold (no known answer, or a rollover): the card **holds its collapsed slot** — active, alpha 0,
  untappable — so the column keeps its shape, and the fetch fades it in where the slot is.
- The fetch stays the source of truth: same recipe ⇒ status-only repaint in place (no re-bind,
  no second arrival — also used after a claim); new recipe ⇒ re-bind; no daily ⇒ the slot goes
  (the one path that still moves the list, and the rare one).

Verified by frame trace: warm entry — daily active + lit and the list at its final position from
the first frame; cold entry — slot held from the first frame, fade-in ~0.3 s later with the list
top unchanged (1819 → 1819); Home-pill entry — expanded on arrival.

## Shape audit (PIPELINE_HARDENING §15)

- *Geometry cached on first use where first use is scene-load frame 0*: `_shiftDistances`
  (DEFECT, removed); `_expandBaseHeights` (fine — both Rankings targets are fixed-size, height
  is canvas-independent); `CharacterDetailPanel` bio base (serialized offsets — fine).
- *Rects in a `ScreenEntryMotion` list that a placement reads or writes*: Home's four (fixed
  above); every other screen's list holds content roots (`Content`, `ContentArea`, `DetailPanel`,
  `CardsContainer`, a button) that nothing places against.

## Tests
`UiMotionTests` +4 (rest adoption, finalizer, `RestY`, noise floor), `BannerSlotShiftTests` +4
(scaled canvas, raw→scaled re-measure, show restores, both rects rising),
`MissionsClientDailyTests` +3, `DailyMissionSessionResetTests` +4. Namespaces green:
`Golfin.UI.Polish.Tests` 170, `GolfinRedux.Tests.EditMode` 345, `Golfin.Economy.Tests` 118,
`Golfin.Net.Tests` 18.

## Account switch (added on Cesar's word, same day)

`DailyMissionSessionReset` (`Assets/Scripts/UI/Home/`, `[RuntimeInitializeOnLoadMethod]`) subscribes
to `AuthService.SignedIn` and forgets both daily facts — `MissionsClient.ForgetDaily()` and
`DailyMissionState.Clear()` — **only when the session's `UserId` differs from the one the facts
were fetched for**. `SignedIn` also fires on a token refresh (its own doc says so, and every other
subscriber re-fetches for that reason); a blind clear would have blanked the Home pill mid-session.
Pinned by `DailyMissionSessionResetTests` (first session remembers, refresh forgets nothing,
another user forgets both, a session without a user id is ignored). Verified at boot: hook
subscribed, nothing forgotten on the first session.
