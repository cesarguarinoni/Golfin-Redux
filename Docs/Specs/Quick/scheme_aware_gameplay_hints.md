# Quick — `scheme_aware_gameplay_hints`

**Asked by Cesar, 2026-09-14:** *"Hints for control scheme (not loading tips) should show depending on
what control scheme is selected. We are currently shipping with Flicker but that might change in the
future."*

**Track:** Polish. Closes the "per-scheme gameplay hints" deferral of `screen_hints` (Notion 2241).

## What is wrong today

The first entry into the shot view opens the `Gameplay` group of `Assets/Resources/Data/ScreenHints.csv`:
`TIP_SWING, TIP_ACCURACY, TIP_GRADES, TIP_VIEW, TIP_CLUB, TIP_FORECAST`. The first two are **Flick's**
instructions — "PULL THE CLUB BACK DOWN THE CONE … FLICK UP" and "SLIDE LEFT OR RIGHT INSIDE THE CONE …
GREEN ARROWS = PURE" — and they open for every player, whatever `ControlSchemeService.Current` says.
A Pendulum / Tap Timing / Free Swing player is taught a cone their screen does not draw
(`Docs/CONTROL_SCHEMES_PLAN.md` §5: aim input is "map only" on those three; the timing widget is a bar,
a needle, or nothing). The three per-scheme tips (`TIP_PENDULUM`, `TIP_TAPTIMING`, `TIP_FREESWING`,
EN+JA published, diagrams authored) exist only in the loading-screen general pool.

Flick is the shipping default (`CONTROL_SCHEMES_PLAN.md` §6, "until §5 metrics say otherwise"). Nothing
here may assume it: the hint set is read off the SELECTED scheme, so a default change is a one-line
`ControlScheme` default change and the tutorial follows.

## Change

### Data — `ScreenHints.csv` grows an optional 4th column, `scheme`

`screen,order,key,scheme`. Blank = the row shows for every scheme; a `ControlScheme` enum NAME
(`Flick`, `Pendulum`, `Needle`, `FreeSwing` — the internal names, exactly as `ControlScheme.cs` spells
them, so a renamed member fails `ScreenHintCatalogTests`) = the row shows ONLY while that scheme is the
player's current one. `order` stays one contiguous sequence per screen over the WHOLE file (the shipped
invariant `OrdersAreUniqueAndContiguousFromOne_PerScreen` is unchanged); each scheme sees a subsequence.

```
Gameplay,1,TIP_SWING,Flick
Gameplay,2,TIP_ACCURACY,Flick
Gameplay,3,TIP_PENDULUM,Pendulum
Gameplay,4,TIP_TAPTIMING,Needle
Gameplay,5,TIP_FREESWING,FreeSwing
Gameplay,6,TIP_GRADES,
Gameplay,7,TIP_VIEW,
Gameplay,8,TIP_CLUB,
Gameplay,9,TIP_FORECAST,
```

Resolved per scheme: **Flick** `1/6 … 6/6` exactly as today; **Pendulum** `TIP_PENDULUM, GRADES, VIEW,
CLUB, FORECAST` (`1/5`); **Needle** `TIP_TAPTIMING, …` (`1/5`); **FreeSwing** `TIP_FREESWING, …` (`1/5`).
`TIP_ACCURACY` is Flick-only on purpose: it is the cone's aim slide and the cone's green/red bands.
`TIP_GRADES` stays shared — every scheme's grade pop draws from the same `SHOT_GRADE_*` vocabulary.
Every other screen's rows carry a blank scheme and resolve exactly as before.

### Code (all in `Assembly-CSharp`, `Assets/Scripts/UI/Hints/`)

- `ScreenHintCatalog`: `ScreenHint.scheme` (string, "" = any) + `ScreenHint.AppliesTo(ControlScheme)`.
  `Parse` reads column 4 when present; a non-blank value that is not a `ControlScheme` name drops the
  row with one warning (a typo must never show a Pendulum tip to a Flick player — dropping is the safe
  failure, same policy as the other malformed-row cases).
- `ScreenHintResolver.HintsFor(screen, hints, tips, state, ControlScheme scheme)` — the scheme is a
  required parameter (no default: a default IS the hard-wiring being removed). Rows that do not
  `AppliesTo(scheme)` are filtered with the screen match, before the seen/active filters.
- `ScreenHintPresenter.Request` passes `ControlSchemeService.Current` (read at the entry the hint is
  resolved for — `GameplaySceneLoader` step 7 — where the scheme is stable) and logs it.
- `ScreenHintVerifyBot`: the gameplay walk reads `Modal.Count` instead of the literal 6 and logs the
  scheme; new menu items `GOLFIN ▸ Hints ▸ Run verify bot — gameplay (<scheme>)` do the fresh-install
  boot → PLAY → Home → mode card → HoleSelection → hole card → shot-view hint walk for one scheme,
  restoring the player's previous scheme pref on exit.

### Not changed, on purpose

- **Loading tips** (`LoadingTips.csv`, `ProTipCard`, `LoadingTipSequencer`, the `first` pool with
  `TIP_SWING`/`TIP_ACCURACY`): Cesar scoped them out explicitly.
- **Seen-state model.** `Gameplay` is still seen once per device. A player who later switches scheme
  gets the new scheme explained by `SchemeConfirmModal` (three tiles + three "how it works" lines) at
  the moment of switching, which is richer than one tip; re-showing the swing tip on the next shot view
  would be a second explanation of the same thing. If Cesar wants it anyway it is a follow-up on the
  store shape (per-scheme seen key), not on this change.
- `TIP_CONTROLS` copy ("NOT A FLICK FAN? …") still names Flick as the scheme the player is on. It is
  the Settings › Controls hint, where all four options are on screen; rewording it is a texts publish,
  flagged, not done.

## Acceptance

- `ScreenHintCatalogTests`: 39 rows / 18 screens; every `scheme` cell blank or a `ControlScheme` name;
  Gameplay rows are the nine above in order; a `Flik` row drops with a warning; orders still 1..N.
- `ScreenHintResolverTests`: Gameplay per scheme as listed; EVERY `ControlScheme` value gets exactly one
  swing tip, first (a 5th scheme added without a row fails here); a scheme row is invisible to the other
  schemes and a blank row visible to all; `Roster` resolves identically under all four schemes.
- Play mode, real navigation, fresh install, per scheme: the shot-view modal opens with that scheme's
  tip at `1/6` (Flick) or `1/5` (the other three); screenshots in
  `Docs/Specs/Quick/media/scheme_aware_gameplay_hints/`.
- Every other EditMode namespace that touches hints green; Console clean.
