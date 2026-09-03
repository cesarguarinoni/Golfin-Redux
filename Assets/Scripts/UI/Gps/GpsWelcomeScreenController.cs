// auth_golf_profile §4 — the one-page Welcome tutorial (Figma 14029:33929).
#nullable enable
using GolfinRedux.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// The last screen of the post-signup chain. Entirely STATIC content — four feature tiles and
    /// a headline, all localized text with no data binding, so there is no fetch, no cache and no
    /// subscription here.
    ///
    /// <para>
    /// THE DOTS ARE DECORATIVE. The node draws a four-dot pager (14029:34198) but the tutorial is
    /// one page: SPEC §4 pins them as static, so there is no carousel behind them and none is
    /// implied. They are built as plain Images by the builder and this controller does not touch
    /// them.
    /// </para>
    /// <para>
    /// TWO EXITS, TWO DESTINATIONS. GET STARTED is the intended one and lands in the GPS hub —
    /// the whole point of the tutorial is to hand the player to the surface it just described.
    /// SKIP means "not now", so it returns to Home. Neither touches the once-per-device flag
    /// <see cref="GpsAuthExtrasFlow.PromptedKey"/>: it was already set by whichever exit of the
    /// Golf Profile screen led here, so this screen cannot be reached with it unset.
    /// </para>
    /// <para>
    /// gps_profile_prompt_on_entry §3 — both exits DO clear
    /// <see cref="GpsAuthExtrasFlow.PendingHubEntry"/>. This screen is the end of the chain either
    /// way, so leaving the marker set would say "a hub entry is still in flight" for the rest of
    /// the session. GET STARTED clears it before navigating rather than after: the hub entry it
    /// then makes is an ordinary one, not the intercepted one.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsWelcomeScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsWelcome]";

        [Header("Actions")]
        [SerializeField] private Button? _getStartedButton;
        [SerializeField] private Button? _skipButton;

        private bool _wiredOnce;

        private void Awake() => WireOnce();

        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            if (_getStartedButton != null)
                _getStartedButton.onClick.AddListener(() =>
                {
                    Debug.Log($"{Tag} GET STARTED -> GpsHub");
                    GpsAuthExtrasFlow.PendingHubEntry = false;
                    ScreenManager.Instance?.ShowScreen(ScreenId.GpsHub);
                });

            if (_skipButton != null)
                _skipButton.onClick.AddListener(() =>
                {
                    Debug.Log($"{Tag} SKIP -> Home");
                    GpsAuthExtrasFlow.PendingHubEntry = false;
                    ScreenManager.Instance?.ShowScreen(ScreenId.Home);
                });
        }
    }
}
