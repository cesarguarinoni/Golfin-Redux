# SPEC — `gps_profile_prompt_on_entry` (Quick)

> Amends `auth_golf_profile` (Completed). Device-pass finding #2, Cesar 2026-09-03: **first run must
> default to the game.** The Golf Profile capture + Welcome tutorial move from "first Home entry" to
> "first entry into GPS" — the pill tap in the game, or the first launch of the standalone shell.

## Status

See `STATUS.md`.

## Goal

A fresh install lands on Home and stays there. The first time the player enters the GPS surface
(`ShowScreen(GpsHub)` from anywhere in the game — the Home pill, the promo banner deep link, a
`golfin://gps` URL), the flow intercepts ONCE: Golf Profile → Welcome → GpsHub. Second entry goes
straight to the hub. The standalone shell (`gps_standalone_shell`, later) reuses the same intercept
on its boot path, so nothing here is Home-specific.

## Implementation

1. **Remove the Home trigger.** `HomeScreenController` no longer calls `GpsAuthExtrasFlow.ShouldOffer()`
   (both call sites, the deferred coroutine too). Delete the `[HomeScreen] auth_golf_profile —` log line.
2. **Intercept in `ScreenManager.Navigate`.** After the gates and BEFORE history bookkeeping: if
   `screenId == ScreenId.GpsHub && GpsAuthExtrasFlow.ShouldOffer()` → set a one-shot
   `GpsAuthExtrasFlow.PendingHubEntry = true` and navigate to `ScreenId.GpsGolfProfile` instead (same
   `instant`/`push` args; the boundary fade still plays, so the player sees Home → Golf Profile with
   the usual fade). `ShouldOffer()` keeps its three inputs (gate on, signed in, not prompted).
3. **Exits unchanged.** SAVE and Skip → `MarkPrompted()` → Welcome; GET STARTED → `GpsHub` (now a push,
   both GPS); Welcome Skip → `Home`. `PendingHubEntry` is cleared on either Welcome exit.
4. **Deep link / banner path.** `golfin://gps` resolves to `ShowScreen(GpsHub)` today — it goes through
   the same `Navigate`, so it is covered with no extra code. Prove it in the report (log line).
5. **Standalone seam.** Expose `GpsAuthExtrasFlow.InterceptHubEntry(ScreenId requested) → ScreenId`
   (pure, testable): returns `GpsGolfProfile` when the offer applies, else the requested id.
   `Navigate` calls it; the shell will call the same function on boot.
6. **No strings, no prefabs, no backend.** Loc PLAN verdict expected `add 0`.

## Acceptance (Implementer fills in `IMPLEMENTER_REPORT.md`)

- [ ] Fresh Editor run with `gps_profile_prompted` cleared: Home comes up and STAYS (quote the screen log —
      no `GpsGolfProfile` before the pill tap).
- [ ] Pill tap → Golf Profile → SAVE → Welcome → GET STARTED → hub (real navigation, log quoted); relaunch
      + pill → hub directly.
- [ ] Skip path: Golf Profile Skip → Welcome → Skip → Home; flag set; next pill tap → hub directly.
- [ ] `golfin://gps` (or `ShowScreen(GpsHub)` from the banner binder) with the flag cleared → Golf Profile
      first (log quoted).
- [ ] "Punch it" build (gate off): `InterceptHubEntry(GpsHub)` returns `GpsHub`; Home never offers
      (EditMode via the three-arg core, all four combinations as before).
- [ ] EditMode: `GpsAuthExtrasFlowTests` updated (Home no longer offers; intercept table pinned);
      full sweep green, suites executed by name.
- [ ] Rest-state parity untouched (no prefab change — `git status Assets/Prefabs` empty).
- [ ] `Docs/GPS/GPS_DEVICE_PASS.md` §1 rows 1.3 / 1.8 updated to the new trigger (pill tap, not Home).

## Out of scope

Settings edit screen, avatar photo, any change to the Golf Profile / Welcome prefabs, the nav-bar
height fix (separate quick fix already with Code), the standalone shell itself.
