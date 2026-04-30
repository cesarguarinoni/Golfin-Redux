# 8.5.B — Lab Inventory Seeder

> **Tier 2 — TellCode-style established pattern.** Single new MonoBehaviour + scene wiring.
> **Created:** 2026-04-30 14:42 JST
> **Owner:** golfin-implementer
> **Depends on:** `8_5_a_csv_consolidation` MUST BE DONE FIRST (this spec uses the consolidated CSV's club IDs).
> **Blocks:** the selector redesign in `8_5_c_selector_redesign` (selector needs real entries to test against).

---

## Why

LabScaffold has no `BagManager` / `ClubManager` / `BallManager` GameObjects. As a result:

- `ClubContextPopulator.Refresh()` early-returns at `if (bag == null || db == null)` → `ClubContext.Reset()` → `EquippedBag` is empty.
- `BallContextPopulator.Refresh()` early-returns at `if (bm == null || db == null)` → `BallContext.Reset()` → `OwnedBalls` is empty.

When the user taps DRIVER or GOLFIN, the selector overlay opens with **zero cards** (or one stale card from a leftover broadcast). There's nothing to test the selector logic against.

We need 4 lab-test clubs (1 Driver, 1 Wood, 1 Iron, 1 Putter) and 2 balls (Golfin + Putt Ace) in the contexts whenever LabScaffold runs in standalone mode (no manager scenes loaded).

## Decision

Add a **`LabInventoryStub` MonoBehaviour** to `LabRoot` in LabScaffold. It runs in `Start()` after the existing populators have early-exited, and:

1. Detects "lab mode" (`BagManager.Instance == null` AND `BallManager.Instance == null`).
2. Loads the `ClubDatabaseCSV` and `BallDatabaseCSV` singletons (these need to be present in LabScaffold for art lookups — see "Scene wiring" below).
3. Builds 4 `ClubEntry` records from `ClubDatabaseCSV` and pushes them into `ClubContext.EquippedBag`.
4. Builds 2 `BallEntry` records from `BallDatabaseCSV` and pushes them into `BallContext.OwnedBalls`.
5. Selects index 0 in each (Driver / Golfin) and raises the change events.

This is intentionally **lab-only** — it never runs in real gameplay because in real gameplay the managers exist and the populators do the right thing.

---

## File 1 — new MonoBehaviour

**Path:** `Assets/Scripts/Physics/Viewer/LabInventoryStub.cs`

**Asmdef:** `Golfin.Physics.Viewer` (existing, includes `Assembly-CSharp` ref so it can see `ClubDatabaseCSV` / `BallDatabaseCSV` / `ClubType` / `BallContext` / `ClubContext`).

```csharp
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Seeds ClubContext and BallContext with test entries when running in LabScaffold
    /// without real managers (BagManager / BallManager absent). Allows the action button
    /// selectors to be tested against real catalog entries from Clubs.csv / Balls.csv.
    ///
    /// Runs in Start() so the existing ClubContextPopulator / BallContextPopulator
    /// have already fired (and no-op'd) by the time we push our lab data.
    ///
    /// Activates only if BagManager.Instance == null AND BallManager.Instance == null.
    /// In any scene where the real managers are present, this MonoBehaviour does nothing.
    /// </summary>
    public class LabInventoryStub : MonoBehaviour
    {
        // Fixed lab-test set. IDs MUST match Assets/Resources/Data/Clubs.csv (post-consolidation).
        // Order in this list defines the order in the selector card stack
        // (index 0 = bottom = selected by default).
        static readonly string[] s_TestClubIds =
        {
            "club_driver_gf",       // 0 — Driver
            "club_wood_gf",         // 1 — Wood
            "club_iron7_mireo",     // 2 — Iron
            "club_putter_golfinx",  // 3 — Putter
        };

        // Balls — pull all rows from Balls.csv (currently 2: ball_golfin, ball_putt_ace).
        // No fixed ID list — whatever's in the CSV gets surfaced. ball_golfin should be index 0.

        void Start()
        {
            // Skip if real managers are present — let the real populators do their thing.
            // (We use reflection-free type lookups via Assembly-CSharp ref since this
            // asmdef already references Assembly-CSharp.)
            bool hasBag  = TryGetSingleton("BagManager", out _);
            bool hasBall = TryGetSingleton("BallManager", out _);
            if (hasBag || hasBall)
            {
                Debug.Log("[LabInventoryStub] Real managers present — stub disabled.");
                return;
            }

            SeedClubs();
            SeedBalls();
        }

        void SeedClubs()
        {
            var db = ClubDatabaseCSV.Instance;
            if (db == null)
            {
                Debug.LogWarning("[LabInventoryStub] ClubDatabaseCSV.Instance is null — cannot seed clubs. " +
                                 "Add a ClubDatabaseCSV GameObject to LabScaffold (see spec § Scene wiring).");
                return;
            }

            var entries = new List<ClubEntry>(s_TestClubIds.Length);
            for (int i = 0; i < s_TestClubIds.Length; i++)
            {
                string id = s_TestClubIds[i];
                var rt = db.GetClub(id);
                if (rt == null)
                {
                    Debug.LogWarning($"[LabInventoryStub] Club '{id}' not found in Clubs.csv — skipped.");
                    continue;
                }

                entries.Add(new ClubEntry
                {
                    ClubId       = id,
                    TypeLabel    = rt.GetTypeLabel(),  // "DRIVER" / "WOOD" / "IRON" / "PUTTER"
                    Distance     = rt.baseDistance,
                    Portrait     = rt.portraitSprite,  // action-button card art (Portraits/ folder); controlSprite is for the swing-handle, not the card
                    LabClubIndex = MapClubTypeToLabIndex(rt.type),
                });
            }

            ClubContext.EquippedBag = entries;

            // Select Driver (index 0) by default.
            if (entries.Count > 0)
            {
                var e = entries[0];
                ClubContext.SelectedClubId    = e.ClubId;
                ClubContext.SelectedTypeLabel = e.TypeLabel;
                ClubContext.SelectedDistance  = e.Distance;
                ClubContext.SelectedPortrait  = e.Portrait;
                ClubContext.SelectedIndex     = 0;
                ClubContext.RaiseSelectedChanged();
            }
            ClubContext.RaiseBagChanged();

            Debug.Log($"[LabInventoryStub] Seeded {entries.Count} clubs into ClubContext.");
        }

        void SeedBalls()
        {
            var db = BallDatabaseCSV.Instance;
            if (db == null)
            {
                Debug.LogWarning("[LabInventoryStub] BallDatabaseCSV.Instance is null — cannot seed balls. " +
                                 "Add a BallDatabaseCSV GameObject to LabScaffold (see spec § Scene wiring).");
                return;
            }

            var allBalls = db.GetAllBalls();  // confirm method name — see API verification below
            var entries = new List<BallEntry>(allBalls.Count);
            foreach (var rt in allBalls)
            {
                if (rt == null) continue;
                entries.Add(new BallEntry
                {
                    BallId          = rt.ballId,             // confirm field name
                    NameLabel       = rt.name.ToUpper(),
                    QuantityDisplay = "∞",                   // lab mode: infinite supply
                    Thumbnail       = rt.thumbnailSprite,    // confirm field name
                    FullSprite      = rt.fullSprite,         // confirm field name
                });
            }

            BallContext.OwnedBalls = entries;

            // Select Golfin (or whatever is index 0) by default.
            if (entries.Count > 0)
            {
                var e = entries[0];
                BallContext.SelectedBallId          = e.BallId;
                BallContext.SelectedNameLabel       = e.NameLabel;
                BallContext.SelectedQuantityDisplay = e.QuantityDisplay;
                BallContext.SelectedThumbnail       = e.Thumbnail;
                BallContext.SelectedFullSprite      = e.FullSprite;
                BallContext.SelectedIndex           = 0;
                BallContext.RaiseSelectedChanged();
            }
            BallContext.RaiseBagChanged();

            Debug.Log($"[LabInventoryStub] Seeded {entries.Count} balls into BallContext.");
        }

        // Maps ClubType to the 4-slot LabClubs array index in PhysicsLabController.
        // Mirrors the same logic in ClubContextPopulator.MapClubTypeToLabIndex.
        static int MapClubTypeToLabIndex(ClubType type) => type switch
        {
            ClubType.Driver  => 0,
            ClubType.Wood    => 0,  // Wood uses Driver slot (no separate Wood entry in LabClubs[])
            ClubType.Iron    => 1,
            ClubType.A_Wedge => 2,
            ClubType.P_Wedge => 2,
            ClubType.S_Wedge => 2,
            ClubType.Putter  => 3,
            _                => 0,
        };

        // Generic singleton check via reflection — avoids hardcoding type references that
        // would require their assemblies to be loaded. Returns true if a MonoBehaviour
        // with a non-null `Instance` static property exists in the scene.
        static bool TryGetSingleton(string typeName, out object instance)
        {
            instance = null;
            var t = System.Type.GetType($"{typeName}, Assembly-CSharp")
                 ?? System.Type.GetType($"Golfin.Inventory.{typeName}, Assembly-CSharp");
            if (t == null) return false;
            var prop = t.GetProperty("Instance",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop == null) return false;
            instance = prop.GetValue(null);
            return instance != null;
        }
    }
}
```

### API verification needed (Code: confirm before writing the file)

These are the questionable APIs. Confirm by inspecting the source files; if any differ, adjust the stub accordingly.

- `ClubDataRuntime.controlSprite` — added by the 8.5.A consolidation spec. **Must exist after that spec ships.** Note: this field exists for the swing-handle sprite swap (`ClubHandleSpriteBinder`), NOT for action-button card art. The seeder uses `portraitSprite` for cards. If 8.5.A is incomplete and `controlSprite` doesn't exist on `ClubDataRuntime`, that's only a problem if you also need to verify the field elsewhere — the seeder code itself doesn't reference it.
- `ClubDataRuntime.GetTypeLabel()` — confirm method exists. Search: `grep -r "GetTypeLabel" Assets/Scripts/UI/Inventory/`. Likely returns the uppercase string ("DRIVER" / "IRON" / etc). If it doesn't exist, derive the label from `rt.type.ToString().ToUpper()` instead.
- `BallDatabaseCSV.GetAllBalls()` — confirm method name. Search: `grep -r "GetAllBalls\|public.*BallDataRuntime" Assets/Scripts/`. If the method is named differently (e.g. `AllBalls`, `Balls`), adjust the call.
- `BallDataRuntime.ballId` / `name` / `thumbnailSprite` / `fullSprite` — confirm field names. Search the type definition. Adjust if names differ (e.g. could be `id` instead of `ballId`).

If any API differs from spec, **fix the field/method name in the stub code without changing the structure**, and note the deviation in the done report. Do NOT change the structure (e.g. by inventing new fields on `BallEntry`).

---

## Scene wiring — `LabScaffold.unity`

`LabInventoryStub` needs `ClubDatabaseCSV.Instance` and `BallDatabaseCSV.Instance` to be non-null at `Start()`. These are MonoBehaviour singletons that must exist in the scene.

### Required GameObjects in LabScaffold

1. **ClubDatabaseCSV** — create a child GameObject under `LabRoot` named `ClubDatabaseCSV`. Add the `ClubDatabaseCSV` component. Drag `Assets/Resources/Data/Clubs.csv` into the `clubsCSV` field. (After 8.5.A is done; before that spec lands, this file is at `Assets/Data/Clubs.csv`.)
   - **Alternative:** there's an existing editor menu `GOLFIN/Setup/Club Managers` (in `ClubManagerSetup.cs`) which creates this GameObject + ClubManager. Run it once in LabScaffold. **Heads up:** that script creates BOTH `ClubDatabaseCSV` AND `ClubManager`. We DON'T want `ClubManager` (its presence would un-trip our `BagManager`/`BallManager`-null check — actually no, `BagManager` is a separate manager from `ClubManager`. Confirm this. If `ClubManager` doesn't have a `BagManager`-like singleton, we're fine.)
   - **Decide before scene-edit:** does running `GOLFIN/Setup/Club Managers` add anything that would defeat the lab-mode detection in `LabInventoryStub`? Check `BagManager.Instance` is set by `BagManager.Awake()`, NOT by `ClubManager.Awake()`. If the two are independent, the menu is safe to use. If they're coupled, manually create just the `ClubDatabaseCSV` GO instead.

2. **BallDatabaseCSV** — same pattern. Create `BallDatabaseCSV` GO under `LabRoot`. Add `BallDatabaseCSV` component. Wire `Assets/Data/Balls.csv` into the inspector field. Search for an existing `BallManagerSetup` editor script — if it exists, same caveat as above.

3. **LabInventoryStub** — add `LabInventoryStub` component to `LabRoot` itself (same GameObject that has `PhysicsLabController` and the existing populators).

### Hierarchy after this spec ships

```
LabRoot
├── PhysicsLabController                  (existing)
├── ClubContextPopulator                  (existing — added by ActionButtonsBuilder)
├── BallContextPopulator                  (existing — added by ActionButtonsBuilder)
└── LabInventoryStub                      (NEW)

LabRoot/ClubDatabaseCSV (or root sibling) (NEW)
LabRoot/BallDatabaseCSV (or root sibling) (NEW)
```

Either nested under LabRoot or as scene root siblings — Unity doesn't care for singleton pattern. Nested keeps the hierarchy tidier.

---

## Wood club mapping

The 4-slot `PhysicsLabController.LabClubs[]` array currently has indices: `0=Driver, 1=Iron 7, 2=Wedge, 3=Putter`. **There's no Wood slot.**

For lab-test purposes:
- `club_wood_gf` → `LabClubIndex = 0` (uses Driver physics — Wood and Driver are tee-shot clubs with similar carry profiles)
- This is fine for v1. The Wood is in the selector for **UI testing**, not for **physics fidelity testing**.
- A future spec can extend `LabClubs[]` to 5 slots and add real Wood physics. **Out of scope for 8.5.B.**

---

## Acceptance criteria

### Code

- [ ] `LabInventoryStub.cs` compiles without errors.
- [ ] All API verifications resolved — either matched spec or adjusted with a note in the done report.

### Scene

- [ ] LabScaffold contains `ClubDatabaseCSV` GO with `Clubs.csv` wired.
- [ ] LabScaffold contains `BallDatabaseCSV` GO with `Balls.csv` wired.
- [ ] LabScaffold's `LabRoot` GO has the `LabInventoryStub` component.
- [ ] LabScaffold does NOT contain a `BagManager` or `BallManager` GO (confirm by Hierarchy search). If either accidentally got added, remove them.

### Runtime (play mode)

- [ ] Enter play mode in LabScaffold.
- [ ] Console shows: `[ClubDatabaseCSV] Loaded 7 clubs.` (or similar — the menu CSV load path).
- [ ] Console shows: `[BallDatabaseCSV] Loaded 2 balls.` (or similar).
- [ ] Console shows: `[LabInventoryStub] Seeded 4 clubs into ClubContext.`
- [ ] Console shows: `[LabInventoryStub] Seeded 2 balls into BallContext.`
- [ ] Console does NOT show: `Real managers present — stub disabled.`
- [ ] Console does NOT show any `Club 'xxx' not found in Clubs.csv — skipped` warnings.

### Visual (play mode)

- [ ] DRIVER button (bottom-right action button) shows "DRIVER" text and a Driver portrait sprite (not a white box).
- [ ] GOLFIN button (bottom-left action button) shows "GOLFIN" text and the Golfin ball sprite.
- [ ] Tap DRIVER → selector overlay opens. Card stack shows **4 cards**: Driver, Wood, Iron, Putter (order top-to-bottom, with Driver at the bottom matching the selected state). Each card has its sprite and label.
- [ ] Tap GOLFIN → selector overlay opens. Card stack shows **2 cards**: Golfin, Putt Ace.

> **NOTE on selector visuals:** This spec only seeds the data. The selector is still using the OLD (broken/stacked) layout from the original 8.5 implementation. The selector layout fix is `8_5_c_selector_redesign` — that spec will redesign the overlay per the new Figma. Don't try to fix the selector layout in this spec; just verify that 4 cards (or 2 for balls) appear in some form, even if visually janky.

### Lab integration

- [ ] In a fresh play session: tap DRIVER, pick the Iron card (3rd from bottom). Console should show some indication of `LabClubIndex = 1` or similar (whatever ClubSelectionBroadcast and PhysicsLabController.OnClubBroadcastReceived log). Fire a shot — confirm the ball reacts as if hit by an Iron (steeper trajectory, less carry than Driver).
- [ ] Tap DRIVER, pick the Wood card. Fire a shot. The ball should fly like a Driver shot (since Wood maps to LabClubs[0]). This confirms the LabClubIndex routing works through Wood.
- [ ] Tap GOLFIN, pick Putt Ace. The button updates to show "PUTT ACE". (No physics differentiation between balls in current sim — visual change is enough for v1.)

---

## Done report

In `Docs/Specs/Active/8_5_b_lab_inventory_seeder/IMPLEMENTER_REPORT.md`:

- Files created / scene-modified list.
- API verifications: which fields/methods existed as specced, which differed (and how the stub was adjusted).
- Console output dump from a play session (the 4 expected log lines).
- Screenshot of LabScaffold play mode showing the action button cluster with real Driver portrait + GOLFIN ball sprite (no white boxes).
- Screenshot of selector overlay open with 4 club cards.
- Confirmation of the lab integration smoke tests (Driver vs Iron vs Wood firing produces different/expected trajectories).
- Any deviations from spec.

Then set `STATUS.md` to `IMPLEMENTER_DONE` and run the self-reviewer.
