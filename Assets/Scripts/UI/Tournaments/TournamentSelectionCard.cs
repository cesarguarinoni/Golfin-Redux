using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage 2 — TournamentSelectionCard prefab controller.
    /// iter-2: BindStatic gains optional sponsorLine param; eyebrow renders
    /// "{SPONSOR} PRESENTS" from CSV instead of hardcoded "GOLFIN PRESENTS".
    /// </summary>
    public class TournamentSelectionCard : MonoBehaviour
    {
        // -- Badge ----------------------------------------------------------------
        [Header("Badge")]
        [SerializeField] private Image _badgeBackground;
        [SerializeField] private TextMeshProUGUI _badgeLabel;

        // -- Course image ---------------------------------------------------------
        [Header("Course Image")]
        [SerializeField] private Image _tournamentImage;

        // -- Content header -------------------------------------------------------
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _eyebrowLabel;
        [SerializeField] private TextMeshProUGUI _nameLabel;
        [SerializeField] private TextMeshProUGUI _clubLabel;

        // -- Date / status row ----------------------------------------------------
        [Header("Date / Status")]
        [SerializeField] private TextMeshProUGUI _dateLabel;

        // -- Entry + Rewards block ------------------------------------------------
        [Header("Entry + Rewards")]
        [SerializeField] private GameObject _freeEntryBadge;
        [SerializeField] private GameObject _paidEntryBadge;
        [SerializeField] private TextMeshProUGUI _paidEntryAmount;
        [SerializeField] private Image _rewardRpIcon;
        [SerializeField] private TextMeshProUGUI _rewardAmountLabel;

        // -- CTA buttons ---------------------------------------------------------
        [Header("CTA Buttons")]
        [SerializeField] private GameObject _ctaGoldButtonGO;
        [SerializeField] private TextMeshProUGUI _ctaGoldLabel;
        [SerializeField] private Button _ctaGoldButton;
        [SerializeField] private GameObject _ctaSilverButtonGO;
        [SerializeField] private TextMeshProUGUI _ctaSilverLabel;
        [SerializeField] private Button _ctaSilverButton;

        // -- Chevron (expand affordance - Stage 3 U1) ----------------------------
        [Header("Chevron (Stage 3)")]
        [SerializeField] private GameObject _chevronGO;

        // -- Events --------------------------------------------------------------
        public System.Action<TournamentSelectionCard> OnCtaClicked;

        // -- Badge colour tokens -------------------------------------------------
        private static readonly Color BadgeLive     = new Color32(0xC0, 0x40, 0x00, 255);
        private static readonly Color BadgeOpen     = new Color32(0x50, 0xC8, 0x78, 255);
        private static readonly Color BadgeEnding   = new Color32(0xFF, 0xC1, 0x07, 255);
        private static readonly Color BadgeUpcoming = new Color32(0x27, 0x75, 0xDD, 255);
        private static readonly Color BadgeEnded    = new Color32(0x6E, 0x7B, 0x91, 255);

        private static readonly Color TextDark  = new Color32(0x0A, 0x1A, 0x30, 255);
        private static readonly Color TextWhite = Color.white;

        // -- Eyebrow gradient: white -> #828fa1 (metallic) ----------------------
        private static readonly Color EyebrowTop    = Color.white;
        private static readonly Color EyebrowBottom = new Color32(0x82, 0x8F, 0xA1, 255);

        // -- State enum ---------------------------------------------------------
        public enum CardState { Open, Ending, EnteredActive, EnteredFinished, Upcoming, Ended }

        private CardState _state;
        public CardState State => _state;

        /// <summary>Tournament id set during BindStatic. Read by the screen controller.</summary>
        public string TournamentId { get; private set; } = string.Empty;

        private static bool UseSilverCta(CardState state) =>
            state == CardState.EnteredFinished || state == CardState.Ended;

        private void Awake()
        {
            if (_ctaGoldButton != null)
                _ctaGoldButton.onClick.AddListener(() => OnCtaClicked?.Invoke(this));
            if (_ctaSilverButton != null)
                _ctaSilverButton.onClick.AddListener(() => OnCtaClicked?.Invoke(this));

            if (_chevronGO != null)
                _chevronGO.SetActive(false);

            ApplyEyebrowGradient();
        }

        private void ApplyEyebrowGradient()
        {
            if (_eyebrowLabel == null) return;
            _eyebrowLabel.enableVertexGradient = true;
            _eyebrowLabel.colorGradient = new TMPro.VertexGradient(
                EyebrowTop,    // top-left
                EyebrowTop,    // top-right
                EyebrowBottom, // bottom-left
                EyebrowBottom  // bottom-right
            );
        }

        /// <summary>
        /// Stage 2 bind. iter-2: adds optional <paramref name="sponsorLine"/> so the eyebrow
        /// renders "{SPONSOR} PRESENTS" from CSV instead of hardcoded "GOLFIN PRESENTS".
        /// </summary>
        public void BindStatic(
            CardState state,
            string tournamentName,
            string clubLine,
            string dateLine,
            bool isFreeEntry,
            int entryRpCost,
            int rewardRp,
            string ctaText,
            string? tournamentId = null,
            string? sponsorLine  = null)
        {
            _state = state;
            TournamentId = tournamentId ?? string.Empty;

            // Badge
            ApplyBadge(state);

            // Header
            if (_eyebrowLabel != null)
            {
                // iter-2: use sponsorLine from CSV; default to "GOLFIN PRESENTS" if absent
                _eyebrowLabel.text = string.IsNullOrEmpty(sponsorLine) ? "GOLFIN PRESENTS" : sponsorLine;
                ApplyEyebrowGradient();
            }
            if (_nameLabel != null) _nameLabel.text = tournamentName;
            if (_clubLabel != null) _clubLabel.text = clubLine;

            // Date
            if (_dateLabel != null) _dateLabel.text = dateLine;

            // Entry block
            bool entered = state == CardState.EnteredActive || state == CardState.EnteredFinished;
            if (entered)
            {
                if (_freeEntryBadge != null)
                {
                    _freeEntryBadge.SetActive(true);
                    var lbl = _freeEntryBadge.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (lbl != null) lbl.text = "ENTERED";
                }
                if (_paidEntryBadge != null) _paidEntryBadge.SetActive(false);
            }
            else
            {
                if (_freeEntryBadge != null)
                {
                    _freeEntryBadge.SetActive(isFreeEntry);
                    if (isFreeEntry)
                    {
                        var lbl = _freeEntryBadge.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (lbl != null) lbl.text = "FREE ENTRY";
                    }
                }
                if (_paidEntryBadge != null) _paidEntryBadge.SetActive(!isFreeEntry);
                if (_paidEntryAmount != null && !isFreeEntry)
                    _paidEntryAmount.text = entryRpCost.ToString("N0");
            }

            // Reward
            if (_rewardAmountLabel != null)
                _rewardAmountLabel.text = rewardRp.ToString("N0");

            // CTA
            bool silver = UseSilverCta(state);
            if (_ctaGoldButtonGO   != null) _ctaGoldButtonGO.SetActive(!silver);
            if (_ctaSilverButtonGO != null) _ctaSilverButtonGO.SetActive(silver);

            if (!silver && _ctaGoldLabel   != null) _ctaGoldLabel.text   = ctaText;
            if (silver  && _ctaSilverLabel != null) _ctaSilverLabel.text = ctaText;
        }

        public void SetCourseImage(Sprite sprite)
        {
            if (_tournamentImage != null && sprite != null) _tournamentImage.sprite = sprite;
        }

        private void ApplyBadge(CardState state)
        {
            Color bgColor;
            Color textColor;
            string label;

            switch (state)
            {
                case CardState.EnteredActive:
                    bgColor   = BadgeLive;
                    textColor = TextWhite;
                    label     = "LIVE";
                    break;
                case CardState.EnteredFinished:
                    bgColor   = BadgeLive;
                    textColor = TextWhite;
                    label     = "LIVE";
                    break;
                case CardState.Open:
                    bgColor   = BadgeOpen;
                    textColor = TextDark;
                    label     = "OPEN";
                    break;
                case CardState.Ending:
                    bgColor   = BadgeEnding;
                    textColor = TextDark;
                    label     = "ENDING";
                    break;
                case CardState.Upcoming:
                    bgColor   = BadgeUpcoming;
                    textColor = TextWhite;
                    label     = "UPCOMING";
                    break;
                case CardState.Ended:
                    bgColor   = BadgeEnded;
                    textColor = TextWhite;
                    label     = "ENDED";
                    break;
                default:
                    bgColor   = BadgeEnded;
                    textColor = TextWhite;
                    label     = "";
                    break;
            }

            if (_badgeBackground != null) _badgeBackground.color = bgColor;
            if (_badgeLabel      != null)
            {
                _badgeLabel.text  = label;
                _badgeLabel.color = textColor;
            }
        }
    }
}
