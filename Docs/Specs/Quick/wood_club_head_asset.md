# Quick spec — Wood club head asset uses Driver model

> **RESOLVED 2026-08-27.** Re-reported by Cesar the same day ("the Golfin wood handle is showing
> the driver handle instead of `S_Controls_Wood_GOLFIN` in the game"). **All three root causes
> hypothesised below were wrong** — see § Resolution at the bottom. The data was correct the whole
> time; the binder was discarding it.

## Problem
Selecting the Wood preset/club in the lab (and likely anywhere the Wood
club-head is referenced) shows the Driver club-head mesh/sprite instead of a
distinct Wood mesh.

## Source
Spotted by Cesar during play verification of
`controls_h_chase_camera_regression` on 2026-05-08. Filed as Quick to keep
that task scoped to the chase-camera regression.

## Likely root cause
Asset wiring in one of:
- `Assets/Data/Clubs.csv` — Wood row points at the Driver sprite/mesh path.
- The `Wood` ClubData / `ClubDataRuntime` field references the wrong sprite.
- The Wood preset prefab in the physics lab references the Driver model.

## What to do
1. Pick a Wood preset and reproduce the bug in lab playmode.
2. Inspect the Wood club row in `Clubs.csv` and compare its sprite/mesh column
   against the Driver row. They should not be identical.
3. If a distinct Wood asset exists in `Assets/Resources/`, fix the CSV / prefab
   reference. If no distinct asset exists, file a follow-up note that an asset
   needs to be authored.
4. Re-verify by selecting Wood in the lab — the club head should look visually
   distinct from Driver.

## Out of scope
Driver / Iron / Wedge / Putter wiring (only check Wood).


---

# Resolution (2026-08-27)

## None of the three suspected causes was it

Checked each, in order:

| Hypothesis (2026-05-08) | Reality |
|---|---|
| `Clubs.csv` Wood row points at the Driver sprite | **Wrong.** Every GOLFIN Wood row carries `controlSprite=S_Controls_Wood_GOLFIN`; every Driver row carries `S_Controls_Driver_GOLFIN`. The CSV has been correct all along. |
| The `ClubData` / `ClubDataRuntime` field references the wrong sprite | **Wrong.** `ClubDatabaseCSV` loads `controlSprite` per club from `Clubs/Controls` and resolves it correctly. |
| The Wood preset prefab references the Driver model | **Wrong.** Nothing prefab-side is involved. |

## The actual cause

`ClubHandleSpriteBinder` **never read the per-club `controlSprite` at all**. It painted the handle
from a hardcoded four-entry array keyed by the LAB CLUB INDEX:

```
0 → S_Controls_Driver_GOLFIN
1 → S_Controls_Iron_GOLFIN
2 → S_Controls_Wedge_GOLFIN
3 → S_Controls_Putter_GOLFIN
```

`MapClubTypeToLabIndex` maps **both Driver and Wood to 0**. There was no wood entry, and the lab
index physically cannot distinguish the two — a collision the codebase already documents elsewhere,
where `ClubEntry.IsDriver` exists as a separate bool "because LabClubIndex cannot express this —
Driver and Wood BOTH map to lab index 0".

So the correct sprite was sitting in the CSV, loaded and resolved, and the binder threw it away and
substituted the driver.

## The fix

`ClubHandleSpriteBinder` now keys on **club type** via `ClubContext.SelectedTypeLabel`
(`"DRIVER"` / `"WOOD"` / `"IRON"` / `"A. WEDGE"` / `"P. WEDGE"` / `"S. WEDGE"` / `"PUTTER"` from
`ClubData.GetTypeLabel`), against a five-entry table that separates Driver and Wood. Wedges match
on a contains-basis so all three wedge labels collapse to one handle.

The lab-index path is **kept as a fallback**, not deleted: the standalone lab rig drives
`ClubSelectionBroadcast` without necessarily populating `ClubContext`, and a handle that went blank
there would be a worse bug than the one being fixed. The binder now also subscribes to
`ClubContext.OnSelectedChanged`, not only `ClubSelectionBroadcast.OnClubChanged`.

Verified: all five GOLFIN control sprites load; `Driver` and `Wood` are genuinely distinct texture
assets rather than two names for one file; all seven real `GetTypeLabel()` outputs map correctly and
unknown labels fall through to the lab index.

## Open design question for Cesar — deliberately NOT fixed here

**The handle brand is hardcoded to GOLFIN for every club**, which is what this component has always
done. But the CSV carries a per-club `controlSprite` and `Assets/Resources/Clubs/Controls/` holds
all five types across **15–18 brands** (`_ROYAL`, `_EAGLEZ`, `_FAIRWAY`, `_KLYRO`, …) — so a ROYAL
driver still draws the GOLFIN driver handle.

Now that the data is known-good, brand-accurate handles are a small change: plumb a `Sprite` field
onto `ClubEntry` and have the two populators fill it from `controlSprite` (this assembly cannot
reference `ClubDataRuntime`, which lives in Assembly-CSharp). Flagged rather than silently expanded
into a bug fix.

## Files

- `Assets/Scripts/Gameplay/UI/ShotUI/ClubHandleSpriteBinder.cs` — rewritten.

## Verification still owed

On-device confirmation from Cesar that a Golfin wood now draws the wood handle.
