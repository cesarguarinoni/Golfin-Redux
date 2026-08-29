# Quick — `missions_locked_on_device`

**Reported by Cesar, 2026-08-29. BOTH original bugs are closed.** Kept for the third one it
uncovered, and so nobody re-investigates the first two.

## Bug 2 — "Missions locked on device" — NOT A BUG (Cesar, 2026-08-29)

> *"Second one was a false issue, it works in the build."*

Nothing to fix. The published `modes` row genuinely did carry `locked = 'true'` until it was
flipped and published as **modes v8** the same evening (see the commit "missions: unlock the mode
and retire its Coming Soon copy"). The Editor/device split the file theorised was never confirmed,
and the `min_build` investigation it proposed was never needed — `min_build` on that row is 0.

Do not re-open this on the strength of the notes below; they describe a hypothesis, not a finding.

## Bug 3 — the daily card expanded into a fixed-height box (OPEN → fixed, see below)

> *"Card still not expanding right. First one expands like shit."*

Cesar's device screenshot shows the expanded daily card with its goal list and hole map cut off at
the card's bottom edge — "Ladies tee" sliced in half.

**Cause, and it was mine.** Fixing the earlier bottom-gap complaint I pinned the card:
`LayoutElement.preferredHeight = minHeight = 374`. The card carries a `ContentSizeFitter`
(PreferredSize) and the campaign prefab it was cloned from has **no LayoutElement at all** — so
the pin overrode what the fitter would have computed. Collapsed that is correct (374 IS the
container's measured height); expanded, `ExpandedContainer` needs **826** and the card stayed 374.

Measured before the fix, with the card expanded in play mode:

    DailyMissionCard  rect=374   LE pref=374 min=374
      ExpandedContainer active=True  h=878     <- clipped by 504px

**Fix:** unpin (`pref = min = -1`) and let the fitter decide, exactly as the prefab does. Verified:
the card grew 374 → 826 and `daily >= expanded` — it fits its own content.

### Two things the fix uncovered

1. **An expanded daily shoves the campaign list off the bottom.** `Content` is a plain
   VerticalLayoutGroup, not a scroll view, so the 452px the card gains goes straight past the nav
   bar. `MissionSelectionScreenController.RebalanceColumn()` now holds the column total constant
   and lets the campaign list — which IS a scroll view — absorb the difference.
2. **The daily card can vanish with no explanation.** `MissionCatalog.BuildFromRecipe` called the
   club resolver with `out _`, and the caller drops the whole card when `ClubIds` is empty. So an
   unresolvable bag produced a missing card and a silent log. The warning now names the loadout
   and the reason, which immediately produced two real causes:
   - `MissionCatalog.ClubResolver` null — `MissionLoadoutResolver.Install()` is
     `[RuntimeInitializeOnLoadMethod]`, which does not re-run after a mid-session domain reload.
     `OnEnable` now re-installs it (idempotent assignment).
   - **`ClubDatabaseCSV.Instance` null while the component is alive, active and in
     DontDestroyOnLoad.** Same shape: `Awake()` sets the static and does not re-run for an object
     that is already alive. `MissionLoadoutResolver` now falls back to the live object.

## Bug 4 — CLOSED, NOT A BUG (2026-08-29, same evening)

Filed here earlier as "`ClubDatabaseCSV.Instance` goes null while the component lives", on the
strength of a probe that reported one live, active, enabled component in DontDestroyOnLoad
alongside a null static — with `LocalizationManager` simultaneously returning raw keys.

**It does not reproduce, and the observation was worthless.** Two separate artefacts produced it:

1. The first sighting came while the project had an unnoticed `CS0104` compile error, so Unity was
   serving the last good assemblies (see the Lesson below).
2. The "clean Editor" re-test that seemed to confirm it was taken after a hard kill of the Editor,
   which **reopened an untitled empty scene** — `scene-list-opened` returned one scene with an
   empty name and empty path. Nothing was running, so every static was legitimately null and
   `ScreenManager` was null too. That should have been the first thing checked and was not.

Opening `Assets/Scenes/ShellScene.unity` and entering play gave, immediately:

    MISSION_PILL_NEXT -> 'NEXT MISSION'   ClubDatabaseCSV.Instance=present

Do not re-open this. **The two robustness fixes in `MissionLoadoutResolver` and
`MissionSelectionScreenController.OnEnable` stay** — they are cheap, idempotent, and correct
regardless; they simply are not fixing the bug this section claimed.

**The check that would have saved the whole detour:** before concluding anything from null statics
in play mode, confirm a scene is actually loaded. `scene-list-opened` returning an empty `Name`
and `path` means there is no app to reason about.

## Lesson — "the type exists" is not "it compiled"

The compile error above went unnoticed for several verification rounds because the check used was
"does the type resolve by reflection?", which passes happily against a stale assembly. `tests-run`
refuses to run at all on compile errors and is the honest gate. See `tasks/lessons.md`.


---

# Bug 1, FIXED — the daily mission card could not be expanded or played

Recorded here because it was reported in the same breath as the lock bug, and because the next
session should not re-investigate it.

## Symptom

In the mission selector the DAILY card rendered correctly but was inert: tapping it did not
expand it, and its action button did not start the round. **Reproduced in the Editor**, so nothing
server-side was involved.

## Cause

`MissionSelectionScreenController.RebuildCards()` subscribes `OnCardTapped` and
`OnActionButtonClicked` on each campaign card as it instantiates it into `cardsContent`. The daily
card is a **serialized scene object** (`dailyCard`, ShellScene fileID `772096315`), bound
separately in `FetchDailyRoutine`, so it never passed that subscribe site and had **zero listeners
on both events**. There was no Inspector path either — both buttons in
`Assets/Prefabs/UI/MissionSelection/MissionCard.prefab` have `m_Calls: []`. The clicks fired into
nothing.

Note the handlers themselves were already generic: `HandleCardTapped` / `HandleActionClicked` work
off `card.Mission` and `card.IsPlayable` and needed no daily-specific branch.

## Fix — all in `Assets/Scripts/UI/MissionSelection/MissionSelectionScreenController.cs`

1. **Subscribe the daily card** in `FetchDailyRoutine`, where it is bound, with `-=` before `+=`.
   `OnEnable` calls `RefreshDaily` on every return to the screen; without the `-=` a second visit
   double-subscribes and a single tap expands then immediately collapses.
2. **`SetExpanded` now iterates a new `AllCards()`** — `_cards` plus `dailyCard` — so the
   single-expanded invariant spans both. Otherwise expanding a campaign card leaves the daily one
   open beside it.
3. **Scroll is restricted to campaign rows** (`if (_cards.Contains(card))`). `ScrollTo` measures
   against `cardsContent`, which the daily card is not parented to, so scrolling "to" it would
   move the campaign list to a meaningless position.

## State — READ THIS BEFORE TOUCHING IT

- **Compiles.** Verified by reflecting against the loaded `Assembly-CSharp` after an asset refresh:
  `AllCards` present (returns `IEnumerable\`1`), `SetExpanded` present, `isCompiling=False`, no
  `CS` errors in the Console.
- **NOT verified behaviourally.** Nobody has tapped the card since the change. The compile proof
  says the wiring exists, not that the tap expands and the button starts the round. One play-mode
  pass through the real mission selector closes this out.
- **NOT committed** as of 2026-08-29.
