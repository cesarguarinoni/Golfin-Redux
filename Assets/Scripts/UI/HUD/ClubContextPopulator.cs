#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
{
    public class ClubContextPopulator : MonoBehaviour
    {
        void OnEnable()
        {
            var bag = BagManager.Instance;
            var clb = ClubManager.Instance;
            if (bag != null) { bag.OnBagChanged += OnBagChangedHandler; bag.OnEquippedBagChanged += OnEquippedBagChangedHandler; }
            if (clb != null) { clb.OnClubEquipped += OnClubEquippedHandler; clb.OnInventoryChanged += Refresh; }
            ClubContext.OnSelectionRequested += SelectByIndex;
            Refresh();
        }

        void OnDisable()
        {
            var bag = BagManager.Instance;
            var clb = ClubManager.Instance;
            if (bag != null) { bag.OnBagChanged -= OnBagChangedHandler; bag.OnEquippedBagChanged -= OnEquippedBagChangedHandler; }
            if (clb != null) { clb.OnClubEquipped -= OnClubEquippedHandler; clb.OnInventoryChanged -= Refresh; }
            ClubContext.OnSelectionRequested -= SelectByIndex;
        }

        void OnBagChangedHandler(int _) => Refresh();
        void OnEquippedBagChangedHandler(int _) => Refresh();
        void OnClubEquippedHandler(string _) => Refresh();

        void Refresh()
        {
            if (FakeStateLock.IsLocked) return;

            var bag = BagManager.Instance;
            var db  = ClubDatabaseCSV.Instance;
            if (bag == null || db == null) { ClubContext.Reset(); return; }

            int slot = bag.EquippedBagSlot;
            if (slot <= 0) { ClubContext.Reset(); return; }

            var clubs = bag.GetClubsInBag(slot) ?? new List<PlayerClubData>();
            var entries = new List<ClubEntry>(clubs.Count);
            foreach (var pc in clubs)
            {
                var t = db.GetClub(pc.clubId);
                if (t == null) continue;
                entries.Add(new ClubEntry
                {
                    ClubId       = pc.clubId,
                    TypeLabel    = t.GetTypeLabel(),
                    Distance     = t.baseDistance,
                    Portrait     = t.portraitSprite,
                    LabClubIndex = MapClubTypeToLabIndex(t.type),
                });
            }
            ClubContext.EquippedBag = entries;

            int newIdx = 0;
            if (!string.IsNullOrEmpty(ClubContext.SelectedClubId))
            {
                int found = entries.FindIndex(e => e.ClubId == ClubContext.SelectedClubId);
                if (found >= 0) newIdx = found;
            }
            SelectByIndex(newIdx);
            ClubContext.RaiseBagChanged();
        }

        void SelectByIndex(int idx)
        {
            if (ClubContext.EquippedBag.Count == 0)
            {
                ClubContext.SelectedClubId    = "";
                ClubContext.SelectedTypeLabel = "DRIVER";
                ClubContext.SelectedDistance  = 0;
                ClubContext.SelectedPortrait  = null;
                ClubContext.SelectedIndex     = 0;
                ClubContext.RaiseSelectedChanged();
                return;
            }
            idx = Mathf.Clamp(idx, 0, ClubContext.EquippedBag.Count - 1);
            var e = ClubContext.EquippedBag[idx];
            ClubContext.SelectedClubId    = e.ClubId;
            ClubContext.SelectedTypeLabel = e.TypeLabel;
            ClubContext.SelectedDistance  = e.Distance;
            ClubContext.SelectedPortrait  = e.Portrait;
            ClubContext.SelectedIndex     = idx;
            ClubContext.RaiseSelectedChanged();
        }

        static int MapClubTypeToLabIndex(ClubType type) => type switch
        {
            ClubType.Driver  => 0,
            ClubType.Wood    => 0,
            ClubType.Iron    => 1,
            ClubType.A_Wedge => 2,
            ClubType.P_Wedge => 2,
            ClubType.S_Wedge => 2,
            ClubType.Putter  => 3,
            _                => 0,
        };
    }
}
