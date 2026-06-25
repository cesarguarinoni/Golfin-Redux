using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage 0 — TournamentSelectionCard prefab controller.
    /// A single card in the Tournament Selection scroll list. Displays a tournament's
    /// badge (state pill), name, club/hole count, entry block, and CTA button.
    ///
    /// All tokens from Figma node 13386:1780 (Lomond OPEN, §3 of SPEC.md).
    /// Stage 2 will call Bind(TournamentDefinition) once ITournamentBackend.GetTournaments()
    /// is wired (Stages blocked on T1→T4). For Stage 0–1 we use BindStatic with literal strings.
    ///
    /// CTA routing:
    ///   Gold button (GoldPrimaryButton instance) = SIGN UP / CONTINUE / NOTIFY ME
    ///   Silver button (TournamentCloseButton instance) = LEADERBOARD / RESULTS
    /// </summary>
    public class TournamentSelectionCard : MonoBehaviour
    {
        // -- Badge ----------------------------------------------------------------
        [Header("Badge")]
        [SerializeField] private Image _badgeBackground;
        [SerializeField] private TextMeshProUGUI _badgeLabel;

        // -- Content header -------------------------------------------------------
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI _eyebrowLabel;    // "GOLFIN PRESENTS"
        [SerializeField] private TextMeshProUGUI _nameLabel;       // tournament name
        [SerializeField] private TextMeshProUGUI _clubLabel;       // "Club Name - 18 Holes"

        // -- Date / status row ----------------------------------------------------
        [Header("Date / Status")]
        [SerializeField] private TextMeshProUGUI _dateLabel;       // "Jun 20 - Jun 27" or countdown

        // -- Entry + Rewards block ------------------------------------------------
        [Header("Entry + Rewards")]
        [SerializeField] private GameObject _freeEntryBadge;       // "FREE ENTRY" pill (visible when free)
        [SerializeField] private GameObject _paidEntryBadge;       // "ENTRY rp500" inline row (visible when paid)
        [SerializeField] private TextMeshProUGUI _paidEntryAmount; // e.g. "500"
        [SerializeField] private Image _rewardRpIcon;              // RP coin icon 30x30, before reward amount
        [SerializeField] private TextMeshProUGUI _rewardAmountLabel; // RP reward, e.g. "5,000"

        // -- CTA buttons ---------------------------------------------------------
        [Header("CTA Buttons")]
        [SerializeField] private GameObject _ctaGoldButtonGO;     // GoldPrimaryButton prefab instance (SIGN UP / CONTINUE / NOTIFY ME)
        [SerializeField] private TextMeshProUGUI _ctaGoldLabel;   // PlayLable on GoldPrimaryButton
        [SerializeField] private Button _ctaGoldButton;           // Button component on GoldPrimaryButton
        [SerializeField] private GameObject _ctaSilverButtonGO;   // TournamentCloseButton prefab instance (LEADERBOARD / RESULTS)
        [SerializeField] private TextMeshProUGUI _ctaSilverLabel; // Text child on TournamentCloseButton
        [SerializeField] private Button _ctaSilverButton;         // Button component on TournamentCloseButton

        // -- Chevron (expand affordance - Stage 3 U1) ----------------------------
        [Header("Chevron (Stage 3)")]
        [SerializeField] private GameObject _chevronGO;           // right-edge chevron (hidden Stage 0-1)

        // -- Events --------------------------------------------------------------
        public System.Action<TournamentSelectionCard> OnCtaClicked;

        // -- Badge colour tokens (extracted from Figma per-state badge nodes) ---
        // LIVE:     #c04000 text white  (13389:1887, 13405:1861)
        // OPEN:     #50c878 text #0a1a30 (13386:1783 - from SPEC §3)
        // ENDING:   #ffc107 text #0a1a30 (13386:1807)
        // UPCOMING: #2775dd text white  (13386:1831)
        // ENDED:    #6e7b91 text white  (13389:1852)
        private static readonly Color BadgeLive     = new Color32(0xC0, 0x40, 0x00, 255);
        private static readonly Color BadgeOpen     = new Color32(0x50, 0xC8, 0x78, 255);
        private static readonly Color BadgeEnding   = new Color32(0xFF, 0xC1, 0x07, 255);
        private static readonly Color BadgeUpcoming = new Color32(0x27, 0x75, 0xDD, 255);
        private static readonly Color BadgeEnded    = new Color32(0x6E, 0x7B, 0x91, 255);

        private static readonly Color TextDark  = new Color32(0x0A, 0x1A, 0x30, 255);
        private static readonly Color TextWhite = Color.white;

        // -- Eyebrow gradient: white -> #d1d6e0 -> #828fa1 (metallic) ----------
        // Figma 13386:1788: white (top) -> #d1d6e0 (mid) -> #828fa1 (bottom)
        // Approximated as TMP vertex gradient: top = white, bottom = #828fa1
        private static readonly Color EyebrowTop    = Color.white;
        private static readonly Color EyebrowBottom = new Color32(0x82, 0x8F, 0xA1, 255);

        // -- State enum (mirrors TournamentState in T1 contracts) ---------------
        public enum CardState { Open, Ending, EnteredActive, EnteredFinished, Upcoming, Ended }

        private CardState _state;
        public CardState State => _state;

        // Which states use the silver (LEADERBOARD/RESULTS) CTA vs gold
        private static bool UseSilverCta(CardState state) =>
            state == CardState.EnteredFinished || state == CardState.Ended;

        private void Awake()
        {
            // Wire CTA click handlers
            if (_ctaGoldButton != null)
                _ctaGoldButton.onClick.AddListener(() => OnCtaClicked?.Invoke(this));
            if (_ctaSilverButton != null)
                _ctaSilverButton.onClick.AddListener(() => OnCtaClicked?.Invoke(this));

            // Stage 3: chevron hidden in Stage 0-1
            if (_chevronGO != null)
                _chevronGO.SetActive(false);

            // Apply metallic gradient to eyebrow (Figma 13386:1788)
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
        /// Stage 0-1 static bind. Called manually by TournamentSelectionScreenController
        /// for each of the 6 static showcase cards.
        /// </summary>
        public void BindStatic(
            CardState state,
            string tournamentName,
            string clubLine,
            string dateLine,
            bool isFreeEntry,
            int entryRpCost,
            int rewardRp,
            string ctaText)
        {
            _state = state;

            // Badge
            ApplyBadge(state);

            // Header
            if (_eyebrowLabel != null)
            {
                _eyebrowLabel.text = "GOLFIN PRESENTS";
                ApplyEyebrowGradient(); // re-apply after text set
            }
            if (_nameLabel    != null) _nameLabel.text    = tournamentName;
            if (_clubLabel    != null) _clubLabel.text    = clubLine;

            // Date
            if (_dateLabel != null) _dateLabel.text = dateLine;

            // Entry block — entered tournaments show an "ENTERED" pill (Figma 13389:1905);
            // otherwise the FREE ENTRY pill or the paid ENTRY+fee variant.
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

            // CTA — route to gold or silver based on state
            bool silver = UseSilverCta(state);
            if (_ctaGoldButtonGO   != null) _ctaGoldButtonGO.SetActive(!silver);
            if (_ctaSilverButtonGO != null) _ctaSilverButtonGO.SetActive(silver);

            if (!silver && _ctaGoldLabel   != null) _ctaGoldLabel.text   = ctaText;
            if (silver  && _ctaSilverLabel != null) _ctaSilverLabel.text = ctaText;
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
