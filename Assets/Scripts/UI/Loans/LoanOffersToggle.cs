// asset_loans_offers §3.4 — Settings ▸ User Profile ▸ LOAN OFFERS (Figma 14261:109878,
// row 14261:109994, toggle 14261:109998).
//
// The one control that decides whether other players can offer to lend you anything. Off removes
// you from every recipient list in the game (the server filters `/user/search` and
// `/social/{id}/following` on `for_loans=1`) and refuses a direct offer with `not_accepting`.
#nullable enable
using System.Collections;
using Golfin.EconomyRuntime;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// A 112×60 pill toggle bound to <c>profiles.golfin_loan_offers</c>.
    ///
    /// <para>
    /// THE SERVER IS THE TRUTH AND THE KNOB IS OPTIMISTIC — in that order. The knob moves the
    /// instant it is tapped, because a switch that waits 200 ms for a round trip before moving
    /// reads as broken; but the WRITE is what decides, and a failure slides the knob back and says
    /// so. The alternative (wait, then move) makes the common case feel dead to save a state
    /// nobody sees in the uncommon one.
    /// </para>
    /// <para>
    /// THERE IS NO LOCAL FALLBACK, deliberately. This setting has no meaning on this device: it
    /// governs what OTHER players' clients are shown, and every one of those reads it from the
    /// server. A PlayerPrefs mirror would be a value that could disagree with the only copy that
    /// matters, and the disagreement would be invisible — the player would see OFF and keep
    /// getting offers.
    /// </para>
    /// </summary>
    public sealed class LoanOffersToggle : MonoBehaviour
    {
        [Header("Row")]
        [SerializeField] private TMPro.TextMeshProUGUI? titleText;
        [SerializeField] private TMPro.TextMeshProUGUI? subtitleText;

        [Header("Toggle (112×60 pill, 48px knob, 6px inset)")]
        [SerializeField] private Button? toggleButton;
        [SerializeField] private Image? pillImage;
        [SerializeField] private RectTransform? knobRect;

        [Header("Colours (Figma 14261:109998)")]
        [SerializeField] private Color onFill  = new Color(0.1529f, 0.4588f, 0.8667f, 1f);  // #2775DD
        [SerializeField] private Color offFill = new Color(0.2196f, 0.3490f, 0.4980f, 1f);  // #38597F

        [Header("Knob travel (anchoredPosition.x)")]
        [SerializeField] private float knobOffX = 6f;
        [SerializeField] private float knobOnX  = 58f;   // 112 − 48 − 6
        [SerializeField] private float slideDuration = 0.15f;

        private bool _isOn = true;
        private bool _pending;
        private Coroutine? _slide;

        /// <summary>Exposed for review / tests: what the knob currently says.</summary>
        public bool IsOn => _isOn;

        private void Awake()
        {
            if (toggleButton != null) toggleButton.onClick.AddListener(OnTapped);
        }

        private void OnEnable()
        {
            if (titleText != null)
                titleText.text = LocalizationManager.Get("LOAN_SETTING_TITLE");
            if (subtitleText != null)
                subtitleText.text = LocalizationManager.Get("LOAN_SETTING_SUB");

            UserService.Instance.OnDetailChanged += OnDetailChanged;

            // Paint from the cache immediately, then let a fetch correct it. `EnsureDetail`
            // calls back on the SAME frame when the row is already in hand, so the common case
            // is one paint and no flicker; a cold Settings entry paints the default ON and
            // settles to the truth when the row lands.
            ApplyState(CurrentSetting(), animate: false);
            UserService.Instance.EnsureDetail(_ => ApplyState(CurrentSetting(), animate: false));
        }

        private void OnDisable()
        {
            UserService.Instance.OnDetailChanged -= OnDetailChanged;
            if (_slide != null) { StopCoroutine(_slide); _slide = null; }
        }

        private void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(OnTapped);
        }

        private void OnDetailChanged(UserDetailDto detail)
        {
            // A write in flight owns the knob. Update's own success callback replaces LastDetail
            // and fires this, and re-applying from it mid-slide would fight the animation the tap
            // already started with the identical value.
            if (_pending) return;
            ApplyState(detail != null && detail.AcceptsLoanOffers, animate: true);
        }

        /// <summary>Null (never fetched, or a server that predates the column) reads as ON — see
        /// <c>UserDetailDto.AcceptsLoanOffers</c>.</summary>
        private static bool CurrentSetting()
        {
            UserDetailDto? detail = UserService.Instance.LastDetail;
            return detail == null || detail.AcceptsLoanOffers;
        }

        private void OnTapped()
        {
            if (_pending) return;

            bool wanted = !_isOn;
            ApplyState(wanted, animate: true);          // optimistic — see the class remarks
            StartCoroutine(WriteRoutine(wanted));
        }

        private IEnumerator WriteRoutine(bool wanted)
        {
            // `display_name` is REQUIRED by the deployed UpdateProfileRequest, so the current one
            // has to ride along or the PUT is a 422. Reading it from the cached row rather than
            // from a field on this component is what stops this toggle from ever RENAMING anybody
            // — it echoes back exactly what the server last said the name was.
            UserDetailDto? detail = UserService.Instance.LastDetail;
            string? displayName = detail != null ? detail.DisplayName : null;

            if (string.IsNullOrEmpty(displayName))
            {
                // No profile row in hand: the PUT would be refused and the player would watch the
                // knob snap back for no visible reason. Say the offline sentence instead — which
                // is what "we could not reach your profile" actually is.
                ApplyState(!wanted, animate: true);
                Toast(PointsSpendGate.OfflineMessage);
                yield break;
            }

            ApiResult<UserDetailDto>? result = null;

            using (PendingSpend.BeginOn(toggleButton))
            {
                _pending = true;

                IEnumerator call = UserService.Instance.Update(
                    displayName!, null, null, null, null, r => result = r,
                    golfinLoanOffers: wanted);
                while (call.MoveNext()) yield return call.Current;
            }

            _pending = false;

            if (result == null || !result.Success)
            {
                // REVERT. The knob told the player something that turned out not to be true, and
                // leaving it where they put it would mean believing offers were off while every
                // other player could still send them.
                ApplyState(!wanted, animate: true);
                Toast(PointsSpendGate.OfflineMessage);
                yield break;
            }

            // Settle on what the SERVER says the row now is, not on what was asked for. They
            // agree in every ordinary case; when they do not, the server is right.
            ApplyState(CurrentSetting(), animate: true);

            TelemetryService.Instance?.RecordSafe("loan_offers_setting",
                () => new Dictionary<string, object> { { "on", _isOn } });
        }

        /// <summary>Paint the knob and the fill. <paramref name="animate"/> false snaps, which is
        /// what a first paint from the cache wants — a knob that slides in on every Settings entry
        /// would read as the player having just changed it.</summary>
        private void ApplyState(bool on, bool animate)
        {
            _isOn = on;

            if (pillImage != null) pillImage.color = on ? onFill : offFill;
            if (knobRect == null) return;

            float target = on ? knobOnX : knobOffX;
            float from = knobRect.anchoredPosition.x;

            if (!animate || !isActiveAndEnabled || Mathf.Approximately(from, target))
            {
                SetKnobX(target);
                return;
            }

            UiMotion.Run(this, ref _slide,
                UiMotion.Tween(from, target, slideDuration, SetKnobX));
        }

        private void SetKnobX(float x)
        {
            if (knobRect == null) return;
            var ap = knobRect.anchoredPosition;
            knobRect.anchoredPosition = new Vector2(x, ap.y);
        }

        private static void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message)) ToastController.Instance?.Show(message);
        }
    }
}
