// ─────────────────────────────────────────────────────────────────────────────
// The lit slot on the GPS nav bar — the same way the Game's bar does it.
//
// Cesar, 2026-09-03: "The selected tab in the bottom nav bar should be coloured
// in the same way the one in Game is."
//
// THE GAME'S MECHANISM, read off PersistentUIManager rather than guessed: each
// nav button is a single Image holding a bare glyph, and HighlightScreen tints
// that Image `iconActiveColor` for the active pillar and `iconNormalColor` for
// the rest. Nothing swaps sprites and nothing toggles a "Selected" child.
//
// IT PORTS EXACTLY, and the prefabs say so rather than the eye:
//   · the GPS nav buttons are also single Images with no children;
//   · two of the five use the SAME sprite assets the Game bar tints —
//     NavHome is Art/HomeScreen/Home.png, NavProfile is Character.png;
//   · both bars are Button transition=ColorTint with normalColor white, so the
//     Image.color set here MULTIPLIES with the Selectable's CanvasRenderer tint
//     instead of being overwritten by it. That is why it works in the Game.
//
// THE COLOURS ARE READ FROM PersistentUIManager, not copied. One source of
// truth: retune the Game's highlight and GPS follows on the next frame it
// paints. The white/cyan fallback only matters if the shell manager is missing.
//
// WHICH SLOT LIGHTS IS DECIDED BY THE SCREEN'S OWN NAME, not by asking
// ScreenManager what is current. The bar is cloned onto every GPS screen, so
// each copy already knows which screen it is on, and reading CurrentScreen in
// OnEnable would race the push (which activates the target before the swap has
// settled). It also keeps the component free of serialized references, so a
// builder re-run cannot break it — the same property GpsNavBarBinder documents.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>Lights the nav slot belonging to the GPS screen this bar is drawn on.</summary>
    [DisallowMultipleComponent]
    public sealed class GpsNavBarHighlight : MonoBehaviour
    {
        /// <summary>Every slot on the bar, so the four that are NOT current get reset.</summary>
        private static readonly string[] Slots =
        {
            "NavHomeButton", "NavRoundsButton", "NavCameraButton",
            "NavGiftButton", "NavProfileButton",
        };

        /// <summary>
        /// Screen root name → the slot that should be lit, or null for "light nothing".
        ///
        /// <para>Badges and Avatar light PROFILE because they are reached from it and have no slot
        /// of their own — the same call the Game bar makes when it keeps Characters lit for the
        /// Shop screens entered from Roster.</para>
        ///
        /// <para>Vote lights NOTHING: the bar has no vote slot (Vote is reached from a hub tile),
        /// and lighting an unrelated slot would be a lie about where the player is.</para>
        ///
        /// <para>gps_checkin: Rounds USED to be the other "never lit" case, because its screen
        /// did not exist. It does now, and its slot behaves like every other one.</para>
        /// </summary>
        public static string? SlotFor(string screenName) => screenName switch
        {
            "GpsHubScreen"      => "NavHomeButton",
            // gps_checkin — the Rounds tab exists now, so its slot lights like any other.
            "GpsRoundsScreen"   => "NavRoundsButton",
            "ScoreUploadScreen" => "NavCameraButton",
            "GpsGiftScreen"     => "NavGiftButton",
            "GpsProfileScreen"  => "NavProfileButton",
            "GpsBadgesScreen"   => "NavProfileButton",
            "GpsAvatarScreen"   => "NavProfileButton",
            _                   => null,
        };

        private void OnEnable() => Apply();

        private void Apply()
        {
            Transform? bar = GpsScreenTransition.FindLayer(gameObject, "GpsNavBar");
            if (bar == null) return;

            Color normal = Color.white, active = Color.cyan;   // only if the shell is absent
            var shell = Golfin.UI.PersistentUIManager.Instance;
            if (shell != null)
            {
                normal = shell.iconNormalColor;
                active = shell.iconActiveColor;
            }

            string? lit = SlotFor(gameObject.name);
            foreach (string slot in Slots)
            {
                Transform? t = bar.Find(slot);
                var img = t != null ? t.GetComponent<Image>() : null;
                if (img == null) continue;
                img.color = slot == lit ? active : normal;
            }
        }
    }
}
