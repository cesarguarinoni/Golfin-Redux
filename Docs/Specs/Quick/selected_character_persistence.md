# selected_character_persistence

**Reported by Cesar (2026-08-30):** "My selected character should survive between sessions."

## Symptom

Pick a character in the Roster (SELECT), quit, relaunch → the game is back on the first
character in the roster. Home shows the wrong portrait; gameplay uses the wrong stats.

## Root cause

The save layer was never the problem — `SaveData.selectedCharacterId` is written by
`CharacterManager.SelectCharacter` and restored in `LoadRoster`, and it round-trips
(`SaveLayerTests`). The selection was being **overwritten after it was restored**:

`RosterScreenController.InitializeScreen()` (called from `Start`, i.e. the first time the
Roster screen activates) ran unconditionally:

```csharp
currentCharacterId = characters[0].characterId;
CharacterManager.Instance.SelectCharacter(currentCharacterId);
```

`SelectCharacter` persists + `MarkDirty()`s, so merely *opening the Roster screen* rewrote the
save to "first owned character" and that is what the next launch restored. RosterScreen is
`m_IsActive: 1` in `ShellScene`, so this fires during boot on the first run of the session.

Confirmed live: with `char_johan` selected, running that one line flips manager **and** save to
`char_james`.

## Fix

1. **`RosterScreenController.InitializeScreen`** — keep the restored selection; only pick
   `characters[0]` (and persist it) when there is no valid saved selection or the saved one is
   no longer owned.
2. **`CarouselController`** — open on the selected character instead of card 0
   (`ResolveInitialCardId`), and snap to the page that card lives on (`SnapToPageOf`, deferred
   one frame so the ContentSizeFitter has sized Content). Without this the fix would leave the
   screen incoherent: card 0 highlighted while someone else is the active character. Starter
   mode still opens on card 0 — it is a picker, not a restore.
3. **`CharacterManager.LoadRoster`** — reconcile `isSelected` flags to `selectedCharacterId`
   after restore. The id is authoritative; the flags are what the SELECT button and the
   selected-icon read, and the F8 starter repair a few lines above can backfill the id without
   touching any flag.

## Verification (play mode, ShellScene, 2026-08-30)

Save backed up and restored afterwards; only `char_james` is owned in the real save, so the test
unlocked a second character to create the repro condition.

| Step | Result |
|---|---|
| Own `char_james` + `char_johan`, select `char_johan` | mgr/save = `char_johan` |
| Invoke `RosterScreenController.Start` (what first activation does) | mgr/save = `char_johan` — **no clobber** |
| Invoke `CarouselController.Start` | focused card = `char_johan`, page 1/2, scrollX 1.00 |
| save.json on disk | `selectedCharacterId: char_johan` |
| Exit play → re-enter play (new session) | restored `char_johan`, `isSelected` true on it and false on `char_james` |
| Old line `SelectCharacter(GetAllOwnedCharacters()[0])` | `char_johan` → `char_james` (the regression, reproduced) |
