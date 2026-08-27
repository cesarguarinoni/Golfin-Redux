using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Paints the on-course club handle with the sprite for the club actually in the player's hands.
    ///
    /// KEYED ON CLUB TYPE, NOT THE LAB CLUB INDEX. The lab index has four values — Driver(0),
    /// Iron(1), Wedge(2), Putter(3) — and **Driver and Wood both map to 0**, so a Wood was drawing
    /// the DRIVER handle and there was no way for this binder to tell the difference. That
    /// collision is documented elsewhere in the codebase too: <c>ClubEntry.IsDriver</c> exists as a
    /// separate bool for exactly the same reason. <c>ClubContext.SelectedTypeLabel</c> is the one
    /// signal on this side of the assembly boundary that distinguishes them.
    ///
    /// The lab-index path is KEPT as a fallback, not deleted: the standalone lab rig drives
    /// <see cref="ClubSelectionBroadcast"/> without necessarily populating <see cref="ClubContext"/>,
    /// and a handle that goes blank there would be a worse bug than the one being fixed.
    ///
    /// BRAND IS HARDCODED TO GOLFIN for every club, which is the behaviour this has always had.
    /// Clubs.csv carries a per-club <c>controlSprite</c> and Resources/Clubs/Controls holds all five
    /// types across 15-18 brands, so per-brand handles are possible — but that is a design change,
    /// not this fix, and it needs a Sprite field plumbed onto ClubEntry (this assembly cannot
    /// reference ClubDataRuntime in Assembly-CSharp).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ClubHandleSpriteBinder : MonoBehaviour
    {
        private Image _image;

        // Indexed by ClubHandle, below. Driver and Wood are now DISTINCT entries.
        private static readonly string[] ResourceKeys =
        {
            "Clubs/Controls/S_Controls_Driver_GOLFIN",
            "Clubs/Controls/S_Controls_Wood_GOLFIN",
            "Clubs/Controls/S_Controls_Iron_GOLFIN",
            "Clubs/Controls/S_Controls_Wedge_GOLFIN",
            "Clubs/Controls/S_Controls_Putter_GOLFIN",
        };

        private enum ClubHandle { Driver = 0, Wood = 1, Iron = 2, Wedge = 3, Putter = 4 }

        /// Lab club index (0..3) → handle. Driver and Wood are indistinguishable here — that is the
        /// whole reason the type label is preferred; this only runs when no label is available.
        private static readonly ClubHandle[] ByLabIndex =
        {
            ClubHandle.Driver, ClubHandle.Iron, ClubHandle.Wedge, ClubHandle.Putter,
        };

        private Sprite[] _cached;

        private void Awake()
        {
            _image  = GetComponent<Image>();
            _cached = new Sprite[ResourceKeys.Length];
            for (int i = 0; i < ResourceKeys.Length; i++)
            {
                _cached[i] = Resources.Load<Sprite>(ResourceKeys[i]);
                if (_cached[i] == null)
                    Debug.LogWarning($"[ClubHandleSpriteBinder] Missing sprite: Resources/{ResourceKeys[i]}");
            }
        }

        private void OnEnable()
        {
            ClubSelectionBroadcast.OnClubChanged += HandleClubChanged;
            ClubContext.OnSelectedChanged        += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            ClubSelectionBroadcast.OnClubChanged -= HandleClubChanged;
            ClubContext.OnSelectedChanged        -= Refresh;
        }

        private void HandleClubChanged(int index) => Refresh();

        private void Refresh()
        {
            if (_image == null) return;

            ClubHandle handle;
            if (TryHandleFromTypeLabel(ClubContext.SelectedTypeLabel, out var fromLabel))
            {
                handle = fromLabel;
            }
            else
            {
                int idx = ClubSelectionBroadcast.CurrentIndex;
                if (idx < 0 || idx >= ByLabIndex.Length) idx = 0;
                handle = ByLabIndex[idx];
            }

            var s = _cached[(int)handle];
            if (s != null) _image.sprite = s;
        }

        /// <summary>
        /// "DRIVER" / "WOOD" / "IRON" / "A. WEDGE" / "P. WEDGE" / "S. WEDGE" / "PUTTER"
        /// (ClubData.GetTypeLabel). Matched on a contains-basis so the three wedge labels collapse
        /// to one handle without listing each, and so a label tweak does not silently fall through.
        /// </summary>
        private static bool TryHandleFromTypeLabel(string label, out ClubHandle handle)
        {
            handle = ClubHandle.Driver;
            if (string.IsNullOrEmpty(label)) return false;

            string l = label.ToUpperInvariant();
            if (l.Contains("PUTTER")) { handle = ClubHandle.Putter; return true; }
            if (l.Contains("WEDGE"))  { handle = ClubHandle.Wedge;  return true; }
            if (l.Contains("IRON"))   { handle = ClubHandle.Iron;   return true; }
            if (l.Contains("WOOD"))   { handle = ClubHandle.Wood;   return true; }
            if (l.Contains("DRIVER")) { handle = ClubHandle.Driver; return true; }
            return false;
        }
    }
}
