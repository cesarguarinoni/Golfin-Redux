# Quick task — `mode_carousel_play_charges_and_goes_nowhere`

**Reported by Cesar, 2026-09-09:** on the Home mode carousel, a card's PLAY button could charge
the entry fee and navigate nowhere. Verified twice in play mode:

```
[PointsService] Spent 10 (mode_entry_fee:practice) → ok spent=10 (activity=10, gift=0) → RP=6488
```

…followed by 25 s with `ScreenManager.Instance.CurrentScreen` still `Home`.

## Root cause

`ModeCarouselController.RebuildCards()` loops the carousel by instantiating the mode list three
times (`for (int pass = 0; pass < 3; pass++)`) and sliding a window over the middle copy. Inside
that loop it wired the whole-card tap on every pass but PLAY on one:

```csharp
if (pass == 1) card.OnPlayClicked += HandlePlayClicked;
card.OnCardTapped += HandleCardTapped;   // every pass
```

The reasoning was that the centred card is always a pass-1 instance. That is true of a **settled**
carousel — `NormalizeCenterInstant()` folds the centre index back into the middle third after every
snap — and false of a **moving** one. `HandleCardTapped` centres the instance the player actually
tapped, which is a pass-0 or pass-2 clone, and `ApplyCardStates()` → `SetCenter(true)` →
`RefreshCenterVisuals()` turns that clone's PLAY button on immediately, for the length of the
0.22 s slide.

Press it in that window and `HandlePlayButtonClicked` ran the entire spend path —
`PointsSpendGate.Spend(entryFee, …)` succeeded and the fee left the balance server-side — and then
`OnPlayClicked?.Invoke(this)` reached no subscriber, so `ScreenManager.ShowScreen(…)` was never
called. Nothing happened on screen.

The window is wider than the slide looks, because the spend is a server round-trip: the click is
committed the moment it lands, and the answer arrives long after the snap has settled.

## What changed

**1. `ModeCarouselController.RebuildCards` — PLAY is wired on every pass.**
`HandlePlayClicked` resolves the route from `card.ModeId` and never cared which clone the click
came from, so this is the wiring the code always meant to have. Each card gets exactly one
subscriber; `UnwireCards()` already removed one per card.

**2. `ModeCardController.HandlePlayButtonClicked` — a card with no listener refuses to spend.**
The wiring bug is fixed, but the *class* of bug is the one worth closing: everything below that
line is irreversible from the player's side, and the only thing that redeems it is the `Invoke` at
the bottom. An unsubscribed `OnPlayClicked` can therefore only end one way — money out, no round.
It now logs an error and returns before the gate, so the next surface that shows a card with PLAY
live and forgets to subscribe gets a red console line instead of a player's points.

**3. `ModeCarouselController.OnEnable` — the animation latches are cleared.**
`OnDisable` calls `StopAllCoroutines()`, which kills a snap without running the line that clears
`_isSnapping`. `OnBeginDrag` and `HandleCardTapped` both read a latched `_isSnapping` /
`_layoutAnim` as "an animation owns the carousel", and nothing ever handed it back — so a carousel
hidden mid-slide came back from the next Home visit permanently unswipeable and untappable. Proven
in play mode: hiding `HomeScreen` mid-snap left `_isSnapping=True` while hidden. Nothing is in
flight at enable time, since `OnEnable` rebuilds from scratch.

## Coverage

`Assets/Tests/EditMode/ModeCarouselPlayWiringTests.cs` (3 tests, reflection into Assembly-CSharp,
built in a preview scene from the real `ModeHomeCard.prefab` through the real `RebuildCards`):

- `EveryCloneShowingPlayHasALivePlayClickedSubscriber` — the invariant, swept over **every** centre
  index the carousel can hold, not just pass 1. **Tripwired:** reverted to `if (pass == 1)` and it
  fails, naming all 8 offending clones (the four playable modes × passes 0 and 2; `driving_range`
  is locked so it never shows PLAY).
- `PlayWithNoSubscriberRefusesToSpend` — drives the real `playButton.onClick` (after invoking the
  real `Awake`, which EditMode does not run on a clone) on a zero-fee mode, so the affordability
  guard cannot be what returns.
- `EnablingTheCarouselClearsStaleAnimationLatches`.

## Verification (Unity play mode, ShellScene, real widgets only)

| Step | Evidence |
|---|---|
| Boot → Home | real Splash `StartButton` tapped by `DevAutoSignIn` |
| Tap a **pass-2** side card | its own `cardTapButton.onClick` |
| Mid-snap state | `centre=11 (pass 2) targetIsCentre=True PLAY active=True subscribers=1` |
| Press PLAY mid-snap | real `playButton.onClick` |
| Result | `RP 6308 → 6298 (delta -10); screen = HoleSelection` |
| Settled path (pass-1 centre) | `RP 6298 → 6288; screen=HoleSelection; ShowScreen(HoleSelection) calls=1` — no double navigation |
| Latch fix | mid-snap hide → `while hidden: _isSnapping=True` → after re-show `_isSnapping=False`, tap moves centre `6 → 5` (not frozen) |

Full EditMode suite after the change: **2994 passed, 0 failed** (3 pre-existing intentional skips).

## Out of scope

`Assets/Scripts/UI/HoleSelection/HoleSelectionScreenController.cs` — a separate first-card-gap fix
is in flight there and was not touched.
