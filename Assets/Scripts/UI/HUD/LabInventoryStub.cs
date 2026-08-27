#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
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
    ///
    /// NOTE: Placed in Assets/Scripts/UI/HUD/ (Assembly-CSharp) rather than
    /// Assets/Scripts/Physics/Viewer/ (Golfin.Physics.Viewer asmdef) because the
    /// Viewer asmdef cannot reference Assembly-CSharp types (Golfin.Inventory,
    /// BagManager, BallManager) without a circular dependency. This matches the
    /// same pattern used by ClubContextPopulator and BallContextPopulator.
    /// </summary>
    public class LabInventoryStub : MonoBehaviour
    {
        // Fixed lab-test set. IDs match Assets/Resources/Data/Clubs.csv.
        // Order in this list defines the order in the selector card stack
        // (index 0 = first = selected by default).
        static readonly string[] s_TestClubIds =
        {
            "club_driver_gf",       // 0 — Driver
            "club_wood_gf",         // 1 — Wood
            "club_iron7_mireo",     // 2 — Iron
            "club_pwedge_royal",    // 3 — P.Wedge (Order 761 default-bag addition; labIdx=2)
            "club_putter_golfinx",  // 4 — Putter
        };

        // True once stub is running in manager-absent mode; used to guard event handlers.
        bool _active;

        void Start()
        {
            // Skip if real managers are present — let the real populators do their thing.
            bool hasBag  = BagManager.Instance != null;
            bool hasBall = BallManager.Instance != null;
            if (hasBag || hasBall)
            {
                Debug.Log("[LabInventoryStub] Real managers present — stub disabled.");
                return;
            }

            _active = true;
            SeedClubs();
            SeedBalls();

            // Own selection-request events so overlay card taps update the contexts.
            // BallContextPopulator / ClubContextPopulator no-op in stub mode (no managers),
            // so without these handlers the RequestSelection calls go unanswered.
            BallContext.OnSelectionRequested  += SelectBallByIndex;
            ClubContext.OnSelectionRequested  += SelectClubByIndex;

            // §2f: also mirror ClubSelectionBroadcast → ClubContext so auto-switch
            // (PhysicsLabController.SetClub from HandleShotComplete) refreshes the
            // ClubButtonWidget. SetClub fires ClubSelectionBroadcast.Raise(index) but
            // does NOT call ClubContext.RequestSelection — bridging that gap here.
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.OnClubChanged += SelectClubByIndex;
        }

        void OnDestroy()
        {
            if (!_active) return;
            BallContext.OnSelectionRequested  -= SelectBallByIndex;
            ClubContext.OnSelectionRequested  -= SelectClubByIndex;
            // §2f: unsubscribe from ClubSelectionBroadcast mirror.
            Golfin.Gameplay.UI.ShotUI.ClubSelectionBroadcast.OnClubChanged -= SelectClubByIndex;
        }

        void SelectBallByIndex(int idx)
        {
            var balls = BallContext.OwnedBalls;
            if (balls.Count == 0) return;
            idx = Mathf.Clamp(idx, 0, balls.Count - 1);
            var e = balls[idx];
            BallContext.SelectedBallId          = e.BallId;
            BallContext.SelectedNameLabel       = e.NameLabel;
            BallContext.SelectedQuantityDisplay = e.QuantityDisplay;
            BallContext.SelectedThumbnail       = e.Thumbnail;
            BallContext.SelectedFullSprite      = e.FullSprite;
            BallContext.SelectedIndex           = idx;
            // Populate spin stat from template so disc radius reflects equipped ball.
            var db = BallDatabaseCSV.Instance;
            var template = db != null ? db.GetBall(e.BallId) : null;
            BallContext.SelectedSpinStat = template != null ? template.spin : 0;
            BallContext.RaiseSelectedChanged();
            Debug.Log($"[LabInventoryStub] Ball selected: {e.NameLabel} (idx={idx}) spin={BallContext.SelectedSpinStat}");
        }

        void SelectClubByIndex(int idx)
        {
            var bag = ClubContext.EquippedBag;
            if (bag.Count == 0) return;
            idx = Mathf.Clamp(idx, 0, bag.Count - 1);
            var e = bag[idx];
            ClubContext.SelectedClubId    = e.ClubId;
            ClubContext.SelectedTypeLabel = e.TypeLabel;
            ClubContext.SelectedDistance  = e.Distance; // iter-37: show the club's distance (see ClubContextPopulator)
            ClubContext.SelectedPortrait  = e.Portrait;
            ClubContext.SelectedControlSprite = e.ControlSprite;
            ClubContext.SelectedIndex     = idx;
            ClubContext.RaiseSelectedChanged();
            Debug.Log($"[LabInventoryStub] Club selected: {e.TypeLabel} (idx={idx})");
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
                    TypeLabel    = rt.GetTypeLabel(),
                    Distance     = rt.baseDistance,
                    Portrait     = rt.portraitSprite,
                    ControlSprite = rt.controlSprite,   // brand+type handle; mirrors ClubContextPopulator
                    LabClubIndex = MapClubTypeToLabIndex(rt.type),
                    // auto_club_selection: mirrors ClubContextPopulator — Driver and Wood both map
                    // to lab index 0, so the auto-selector keys off this flag, not the lab index.
                    IsDriver     = rt.type == ClubType.Driver,
                });
            }

            ClubContext.EquippedBag = entries;

            // Select Driver (index 0) by default.
            if (entries.Count > 0)
            {
                var e = entries[0];
                ClubContext.SelectedClubId    = e.ClubId;
                ClubContext.SelectedTypeLabel = e.TypeLabel;
                ClubContext.SelectedDistance  = e.Distance; // iter-37: show the club's distance
                ClubContext.SelectedPortrait  = e.Portrait;
                ClubContext.SelectedControlSprite = e.ControlSprite;
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

            var allBalls = db.GetAllBalls();
            var entries = new List<BallEntry>(allBalls.Count);
            foreach (var rt in allBalls)
            {
                if (rt == null) continue;
                entries.Add(new BallEntry
                {
                    BallId          = rt.ballId,
                    NameLabel       = rt.name.ToUpper(),
                    QuantityDisplay = "∞",   // ∞ — lab mode: infinite supply
                    Thumbnail       = rt.thumbnailSprite,
                    FullSprite      = rt.fullSprite,
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
                // Populate spin stat from template.
                var firstTemplate = db.GetBall(e.BallId);
                BallContext.SelectedSpinStat = firstTemplate != null ? firstTemplate.spin : 0;
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
            ClubType.Wood    => 0,  // Wood uses Driver slot (no separate Wood slot in LabClubs[])
            ClubType.Iron    => 1,
            ClubType.A_Wedge => 2,
            ClubType.P_Wedge => 2,
            ClubType.S_Wedge => 2,
            ClubType.Putter  => 3,
            _                => 0,
        };
    }
}
