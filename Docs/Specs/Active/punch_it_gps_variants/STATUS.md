READY_FOR_SELF_REVIEW

# STATUS — `punch_it_gps_variants`

**Current:** `READY_FOR_SELF_REVIEW`

**Spec written:** 2026-08-31 (Architect)
**Implemented:** 2026-08-31 (Claude Code, main thread — build tooling + a compile-time gate, no UI
authoring, so the subagent chain does not apply)

## Built and verified

- `GpsGate` (deny-list, Editor always on), gated at all three reachability points in
  `ScreenManager`, plus the Home banner hidden in `BannerSlotBinder` when its internal route
  targets a gated screen.
- `iOS-Full-GPS.asset` created **through the Editor**; its only delta from `iOS-Full` is the name
  and `GOLFIN_GPS`. **`iOS-Full.asset` is byte-identical** — verified clean in git.
- `CIBuild.BuildIOSGps()` + `AssertGpsDefine()`, which refuses to build a GPS variant whose profile
  lost the define (that build would be indistinguishable from an ordinary one).
- `unity-build-ios.sh gps`; Fastfile split into one shared body with `testflight_build` /
  `testflight_build_gps`.
- **EditMode: 2234 tests, 2231 passed, 0 failed** — and the new tests were proven to actually run
  via a tripwire that turned the suite red by name, then was reverted.

## ✅ Completed once the Editor was free (2026-09-02)

The three previously-blocked items all ran:

- **GPS ON, play mode** — Home shows the `GOLFIN·GPS` banner, tapping it reaches GpsHub, and all
  five GPS screens navigate. Screenshot in `screenshots/`.
- **GPS OFF** (const temporarily flipped, reverted after) — all five screens refuse to navigate,
  the banner is hidden and non-tappable while `BannerService` still holds the live row, and the
  slot **collapses by a measured 236 px** (banner 214 + 22 gap): `ModeCarouselSection` worldY
  536→300. Screenshot in `screenshots/`.
- **`./Tools/unity-build-ios.sh gps`** — exit 0 in 2 min 40 s, log carrying
  `[CIBuild] GPS variant — GOLFIN_GPS defined on iOS-Full-GPS.` and
  `active build profile → iOS-Full-GPS`, build 2567.

**All 11 acceptance items now PASS.** Remaining: the device pass, which is Cesar's punch-it runs.

## History

| Date | State | Note |
|---|---|---|
| 2026-08-31 | `SPEC_READY` | Spec authored. |
| 2026-08-31 | `READY_FOR_SELF_REVIEW` | Implemented. 8 of 11 acceptance items PASS, 1 PARTIAL, 2 BLOCKED on exclusive Editor access. One flake seen and not reproduced: `RealHoleTerrainTests` on Hole_05/13 during a run concurrent with the other session's asset imports — no terrain file is touched by this task. |
| 2026-09-02 | `READY_FOR_SELF_REVIEW` | Editor freed; the three blocked items completed. **11 of 11 PASS.** Caught a capture trap en route: the GOLFIN menu item writes to `Docs/Diagnostics/_capture/`, not `Assets/Screenshots/`, so the first before/after pair were the SAME file — found by md5-comparing them rather than trusting filenames. |
