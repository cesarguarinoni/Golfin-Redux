# Quick · `gps_navbar_bottom_anchor`

**Reported by Cesar, 2026-09-03, from the first device pass:**
> "The bottom nav bar is not anchored to the bottom of the screen in GPS."

He was right, and it was mine — `gps_polish` §D9.

## The bug

`GpsPolishBuilder.EnsureNavBarSafeArea` wrapped the bar in a full-screen `NavSafeArea` carrying a
`SafeAreaFitter` with `_baselineInsetPixels = 0`, i.e. **the full safe area on every edge**.
`GpsNavBar` is anchored (0.5, 0), pivot (0.5, 0), position (0, 0) — flush to its *parent's* bottom.
So on a phone with a home indicator the wrapper's bottom edge lifted by `Screen.safeArea.y`
(34 pt = **102 px** @3x) and the whole bar rode up with it, leaving 102 px of screen background
showing underneath.

I inset the **whole bar** when the correct pattern is: the background reaches the physical bottom
edge and only the **content** lifts clear of the indicator. The precedent was already in the repo
and I did not follow it — `safe_area_top_bar` gives the TOP bar `_baselineInsetPixels = 141`
precisely so it does *not* move on an iPhone 14.

## Why every gate missed it

At the 1170×2532 Editor reference `Screen.safeArea` **is** the whole screen, so the inset is zero
and nothing moves. `gps_polish`'s own A10 note said exactly that in writing — *"nothing moves —
which the A2 0-px result confirms"* — and flagged D9 as device-only. Seven gate passes could not
have seen it. The device pass is what it was flagged for, and it found it on the first run.

## The fix

`Assets/Scripts/UI/Gps/GpsNavBarSafeArea.cs` — new, runtime-only.

Because the bar's anchor and pivot are both y = 0, growing its **height** extends it upward and
leaves its bottom welded to the screen edge. So:

- the bar absorbs the inset as height — background still covers the indicator;
- its four icon buttons are TOP-anchored (0, 1), so they ride the rising top edge for free;
- the one BOTTOM-anchored child (the centre camera button) gets the inset added to its `y`.

The geometry is a pure function, `GpsNavBarSafeArea.For(height, bottomChildY, inset)`, so the part
that can be pinned without a phone is pinned without a phone.

**Runtime only — deliberately NOT `[ExecuteAlways]`.** An edit-mode version would rewrite the
height in the open prefab, a later save would serialise the GROWN value as the authored one, and
the next run would grow it again from there. Cumulative asset drift. A test pins the attribute's
absence.

The `NavSafeArea` wrapper stays (now an inert full-screen pass-through) so the
`NavSafeArea/GpsNavBar` path every caller, recorder and probe already resolves keeps working; only
its `SafeAreaFitter` is removed.

## Verification

| check | result |
|---|---|
| EditMode sweep | **2325 total · 2322 passed · 0 failed · 3 pre-existing skips** |
| new tests | 6 in `GpsNavBarSafeAreaTests` — inset 0 is a no-op; negative clamped; the bar GROWS rather than MOVES; the camera button clears the indicator (257 − 119 = 138 > 102); the icon row goes from 20 px above the bottom (inside a 102 px indicator — the original defect) to 122 px; the component is not `[ExecuteAlways]` |
| prefabs | 7 of 7 nav-bar screens: fitter removed, component added, bar still `NavSafeArea/GpsNavBar` at 1178×196 pos (0,0) — **authored geometry unchanged, no drift** |
| scene | `ShellScene.unity` **not modified** — the screens are prefab instances, so it propagated to 7/7 live instances with a zero scene diff (`isDirty=False`) |
| scope | `SafeAreaFitter` gone from all GPS prefabs; still present in `ShellScene` for the shell's own top bar, untouched |
| Editor rest state | unchanged: the wrapper's authored anchors are exactly what the fitter used to write at full-screen safe area ((0,0)–(1,1), offsets 0), and the component is a proven no-op at inset 0 |

## One unrelated thing this shook loose

`UiMotionTests.Rise_ReturnsToTheRestYItWasGivenAndFullAlpha` began failing — expected −361 ±1e-4,
got −361.000122. **Not a regression, and not flaky-and-ignored:** the routine's last line writes
`restY` exactly, but `anchoredPosition` on a parentless `RectTransform` round-trips through
`localPosition`, and at magnitude 361 one float ulp is already 6.1e-5. The residual is one to
three ulp and its size moves with the frame timing that decides the step count, so the old 1e-4
bound sat on the noise floor and passed by luck until a fresh Editor changed the timing.

Confirmed as the round trip by reproducing the same drift with the production routine driven
directly, no test harness (`final y = −361.000153`), and `UiMotion.cs` is unchanged since the suite
was last green. Tolerance is now 0.01 px — a hundredth of a pixel, far below anything visible and
far above the storage noise — with the mechanism written into the test.

## Still needs the device

Whether iOS reports the bottom inset we expect. Everything downstream of that number is pinned
here. Same standing item as the `gps_polish` keyboard offset — both want the same device pass.

## Not done (Cesar's call)

The **CHECK-IN tile goes nowhere because there is no check-in feature.** Only plumbing exists:
`Endpoints.ActivityCheckin` (`POST /activity/checkin`, never called) and `ActivityDto.CheckInAt`,
whose comment says *"No service method consumes this yet; the type exists so `gps_checkin_screen`
does not have to reopen this file."* `gps_checkin_screen` is roadmap in `Docs/GPS/GPS_BACKLOG.md`
— *"needs a design round (no Ken mockup)"* — with no spec in `Active/` or `Queued/`.

The tile is deliberately inert from `gps_hub_entry` (`interactable = false`, logs its name). The
real problem is that it looks **identical to SCREENSHOT / VOTE / GIFT, which all work**, so nothing
tells a player it is not ready. Dim it, badge it, or leave it as the promise it was meant to be —
a design call, not a bug fix.
