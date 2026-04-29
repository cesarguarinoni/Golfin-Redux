#nullable enable
using System.Collections.Generic;
using UnityEngine;
using Golfin.Inventory;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.UI.HUD
{
    public class BallContextPopulator : MonoBehaviour
    {
        void OnEnable()
        {
            var bm = BallManager.Instance;
            if (bm != null) bm.OnInventoryChanged += Refresh;
            BallContext.OnSelectionRequested += SelectByIndex;
            Refresh();
        }

        void OnDisable()
        {
            var bm = BallManager.Instance;
            if (bm != null) bm.OnInventoryChanged -= Refresh;
            BallContext.OnSelectionRequested -= SelectByIndex;
        }

        void Refresh()
        {
            var bm = BallManager.Instance;
            var db = BallDatabaseCSV.Instance;
            if (bm == null || db == null) { BallContext.Reset(); return; }

            var ids = bm.GetAllOwnedBallIds() ?? new List<string>();
            var entries = new List<BallEntry>(ids.Count);
            foreach (var id in ids)
            {
                var t = db.GetBall(id);
                if (t == null) continue;
                entries.Add(new BallEntry
                {
                    BallId          = id,
                    NameLabel       = t.name.ToUpper(),
                    QuantityDisplay = bm.GetQuantityDisplay(id),
                    Thumbnail       = t.thumbnailSprite,
                    FullSprite      = t.fullSprite,
                });
            }
            BallContext.OwnedBalls = entries;

            int newIdx = 0;
            if (!string.IsNullOrEmpty(BallContext.SelectedBallId))
            {
                int found = entries.FindIndex(e => e.BallId == BallContext.SelectedBallId);
                if (found >= 0) newIdx = found;
            }
            SelectByIndex(newIdx);
            BallContext.RaiseBagChanged();
        }

        void SelectByIndex(int idx)
        {
            if (BallContext.OwnedBalls.Count == 0)
            {
                BallContext.SelectedBallId          = "";
                BallContext.SelectedNameLabel       = "GOLFIN";
                BallContext.SelectedQuantityDisplay = "∞";
                BallContext.SelectedThumbnail       = null;
                BallContext.SelectedFullSprite      = null;
                BallContext.SelectedIndex           = 0;
                BallContext.RaiseSelectedChanged();
                return;
            }
            idx = Mathf.Clamp(idx, 0, BallContext.OwnedBalls.Count - 1);
            var e = BallContext.OwnedBalls[idx];
            BallContext.SelectedBallId          = e.BallId;
            BallContext.SelectedNameLabel       = e.NameLabel;
            BallContext.SelectedQuantityDisplay = e.QuantityDisplay;
            BallContext.SelectedThumbnail       = e.Thumbnail;
            BallContext.SelectedFullSprite      = e.FullSprite;
            BallContext.SelectedIndex           = idx;
            BallContext.RaiseSelectedChanged();
        }
    }
}
