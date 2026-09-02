// ─────────────────────────────────────────────────────────────────────────────
// gps_polish — deviation D-5. Read this before deciding whether it belongs here.
//
// WHAT THE REAL-NAVIGATION PROBE FOUND. The GPS nav bar is CLONED onto every GPS
// screen (GpsProfilePackBuilder, GpsGiftVoteBuilder, ScoreUploadScreenBuilder all
// copy it from the hub) but only GpsHubScreenController ever wires it. On every
// other GPS screen the five slots are decoration. And `_backButton` is NULL on
// all three profile-pack prefabs, which carry no other button. Net effect at
// HEAD: a player who reaches Profile, Badges or Avatar has NO WAY OUT of it.
//
// WHY IT IS FIXED IN A POLISH TASK, and not deferred. Two of this task's own
// acceptance items are unreachable without it:
//   · A4 (b) is "hub nav bar tab sweep ScoreUpload -> Gift -> Vote -> Profile" —
//     a sweep of nav slots that, at HEAD, do nothing once you leave the hub.
//   · D2's direction table is specified as "between two hub-nav tabs … direction
//     follows nav-bar slot order", which only has meaning if a nav slot can be
//     tapped from somewhere other than the hub.
// Nothing here is new: no screen, no art, no layout, no localized string. It
// makes an already-shipped, already-drawn widget do the single thing it visibly
// promises. If Cesar would rather ship the dead bar, deleting this component
// from GpsPolishBuilder is a one-line revert.
//
// THE HUB IS EXCLUDED. GpsHubScreenController already wires its own bar (and
// deliberately leaves HOME as a lit no-op and ROUNDS inert). Binding it twice
// would add a second listener to every slot.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using GolfinRedux.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Wires the cloned GPS nav bar on a NON-hub GPS screen. Added by
    /// <c>GpsPolishBuilder</c>; takes no serialized references so it survives a builder re-run.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsNavBarBinder : MonoBehaviour
    {
        private const string Tag = "[GpsNav]";

        private bool _wired;

        private void OnEnable() => WireOnce();

        /// <summary>
        /// Once, not per enable: <c>onClick</c> is additive, so re-wiring on every screen entry
        /// would fire one tap N times after N visits — the same trap
        /// <see cref="GpsHubScreenController"/> documents on its own <c>WireOnce</c>.
        /// </summary>
        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            Transform? bar = GpsScreenTransition.FindLayer(gameObject, "GpsNavBar");
            if (bar == null) { Debug.LogWarning($"{Tag} {name}: no GpsNavBar to bind"); return; }

            Bind(bar, "NavHomeButton",    ScreenId.GpsHub);
            Bind(bar, "NavCameraButton",  ScreenId.ScoreUpload);
            Bind(bar, "NavGiftButton",    ScreenId.GpsGift);
            Bind(bar, "NavProfileButton", ScreenId.GpsProfile);

            // ROUNDS stays inert, exactly as the hub leaves it: the Rounds screen was never
            // designed (GPS_BACKLOG § "Rounds tab destination"). A slot that navigated nowhere
            // would be worse than one that is honestly dead.
            Transform? rounds = bar.Find("NavRoundsButton");
            if (rounds != null)
            {
                var rb = rounds.GetComponent<Button>();
                if (rb != null) rb.interactable = false;
            }
        }

        private void Bind(Transform bar, string child, ScreenId target)
        {
            Transform? t = bar.Find(child);
            var b = t != null ? t.GetComponent<Button>() : null;
            if (b == null) return;

            b.interactable = true;
            b.onClick.AddListener(() =>
            {
                // The slot for the screen you are already standing on is a lit no-op — the hub's
                // HOME slot behaves the same way, and Navigate would ignore it regardless.
                var sm = ScreenManager.Instance;
                if (sm == null || sm.CurrentScreen == target) return;
                Debug.Log($"{Tag} {name}: {child} -> {target}");
                sm.ShowScreen(target);
            });
        }
    }
}
