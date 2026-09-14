# Quick — `scheme_aware_gameplay_hints`

**Asked by Cesar, 2026-09-14:** *"Hints for control scheme (not loading tips) should show depending on
what control scheme is selected. We are currently shipping with Flicker but that might change in the
future."*

**Track:** Polish. Closes the "per-scheme gameplay hints" deferral of `screen_hints` (Notion 2241).
**Status:** DONE — approved by Cesar 2026-09-14 on the four per-scheme play-mode frames. Commits `7a88aafbf` (feature), `d8fdab6ac` (texts v56, `TIP_CONTROLS`), `ceba430ae` (proof + text table).

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
- ~~`TIP_CONTROLS` copy ("NOT A FLICK FAN? …")~~ — **done on Cesar's word (2026-09-14, "Go for scheme
  neutral text")**: EN `FOUR WAYS TO SWING: FLICK, PENDULUM, TAP TIMING OR FREE SWING. PICK YOURS IN
  <color=#EEDC9A>SETTINGS › CONTROLS</color>. THE SHOT PHYSICS NEVER CHANGE`; JA `スイングは4通り：フリック・
  振り子・タップタイミング・フリースイング。<color=#EEDC9A>設定 › 操作方法</color>で選べる。ショットの物理は変わらない`.
  Same row feeds the loading screen (`first` pool, order 8) and the Settings › Controls hint, so the
  location stays named rather than "here". Importer plan `texts 0 add / 1 change / 1204 same / 0
  conflict` → `--apply` → `content_publish` → **texts v56** → `export_content.py --check` clean;
  `content_version.txt` `texts=56`. The bundled `LocalizationTextTable.asset` regenerates on the next
  play-mode entry / build (its two hooks). Figma's `Loading Tips — OFFICIAL` still carries the old
  sentence — copy lives in `texts`, the frame is the Architect's to sync.

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

## Result (2026-09-14)

| Gate | Result |
|---|---|
| Offline Roslyn (Assembly-CSharp 323 files, -Editor 229, Tests.EditMode 33) | 0 errors; the Editor's own recompile agreed |
| `ScreenHintResolverTests` | **19/19** (incl. `("Flick")`/`("Pendulum")`/`("Needle")`/`("FreeSwing")` cases and the enum walk) |
| `ScreenHintCatalogTests` | **8/8** (39 rows, scheme cells, `Flik` fixture) |
| `ScreenHintStoreTests` | untouched code; `SaveThenLoad_RoundTripsBothArrays` probed green |
| `LoadingTipCatalogTests` | deliberately not run in the shared Editor (it opens ShellScene additively); nothing it checks changed |
| Play mode, `GOLFIN ▸ Hints ▸ Run verify bot — gameplay (<scheme>)`, fresh install, real StartButton → Home (CLOSE ×2) → PlayButton → HoleSelection (CLOSE ×2) → ActionButton → Lomond hole 2 | **Pendulum** `hints=5 (expect 5)`, `1/5 TIP_PENDULUM` → GRADES → VIEW → CLUB → `5/5 TIP_FORECAST` CLOSE · **Needle** `1/5 TIP_TAPTIMING` … · **FreeSwing** `1/5 TIP_FREESWING` … · **Flick** `hints=6 (expect 6)`, `1/6 TIP_SWING`, `2/6 TIP_ACCURACY`, … `6/6 TIP_FORECAST` — the shipped six. Loading screen inactive at reveal on all four; BACK absent on hint 1, present after; every frame 1170×2532 with a distinct md5; sidecars `realPlay: true` (ShellScene + LabScaffold + Hole_02_Geo). The player's scheme pref (Pendulum) was restored after each run; the Editor profile's seen-state was put back afterwards. Frames: `gameplay_<scheme>_hint_1ofN.jpg` (canonical, with sidecar) + `_NofN.jpg`; logs `verify_gameplay_<Scheme>.log`. |
| Console (`console-get-logs` Error + Exception, the runs' window) | empty |
| `LocalizationTextTable.asset` | regenerated by the play-mode hook; diff = the `TIP_CONTROLS` row only, committed |

## Daily-report clip (2026-09-14)

`media/scheme_aware_gameplay_hints/scheme_hint_demo_flick_to_pendulum.mp4` — 61 s, 1170×2532: Settings ›
Controls with the reworded tip → Flick's first hole on the cone tips (1/6, 2/6) → the in-game switch to
Pendulum through the confirm pop-up → Pendulum's first hole on `TIP_PENDULUM` (1/5), then the shared four.
Tap Timing and Free Swing ride along as the two stills. Captions in `captions.json` are on the VIDEO's
timeline (windows found by a frame classifier, one frame checked inside each); `captions_run_realtime.json`
is what the run itself stamped, kept because it is 1.4–2.8 s ahead of the picture — Lesson CD.

The recording was the failure of the day: one chained take through all four schemes (four hole loads,
three quit/unload cycles) degraded until it locked the Mac and Cesar stopped it; the raw held the first
98 s of a 270 s run, which is where the cut comes from. `ScreenHintSchemeDemoRecorder` is now one
single-hole take per scheme (`GOLFIN ▸ Hints ▸ Record scheme demo take — <n> <scheme>`), captions
stamped on `Time.time`; not re-run — Cesar chose the salvaged cut plus stills.
