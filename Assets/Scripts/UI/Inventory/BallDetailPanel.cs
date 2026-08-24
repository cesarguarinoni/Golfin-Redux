#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Ball detail panel — shows ball image, info text, quantity, and 5 stat bars.
    /// Simplified from ClubDetailPanel: no rarity, no level, no equip, no repair.
    /// Stat bars: blue for positive/zero values, orange-red for negative.
    /// </summary>
    public class BallDetailPanel : MonoBehaviour
    {
        [Header("Left Panel")]
        [SerializeField] private Image           ballImage  = null!;
        [SerializeField] private TextMeshProUGUI infoHeader = null!;
        [SerializeField] private TextMeshProUGUI infoText   = null!;

        [Header("Right Panel — Name & Quantity")]
        [SerializeField] private TextMeshProUGUI ballNameText = null!;
        [SerializeField] private TextMeshProUGUI ownedLabel   = null!;   // "OWNED"
        [SerializeField] private TextMeshProUGUI quantityText = null!;   // "x99" or "∞"

        [Header("Stat — Power")]
        [SerializeField] private TextMeshProUGUI powerName   = null!;
        [SerializeField] private Image           powerBar    = null!;
        [SerializeField] private TextMeshProUGUI powerNumber = null!;

        [Header("Stat — Rebound")]
        [SerializeField] private TextMeshProUGUI reboundName   = null!;
        [SerializeField] private Image           reboundBar    = null!;
        [SerializeField] private TextMeshProUGUI reboundNumber = null!;

        [Header("Stat — Wind Resistance")]
        [SerializeField] private TextMeshProUGUI windResistanceName   = null!;
        [SerializeField] private Image           windResistanceBar    = null!;
        [SerializeField] private TextMeshProUGUI windResistanceNumber = null!;

        [Header("Stat — Roll")]
        [SerializeField] private TextMeshProUGUI rollName   = null!;
        [SerializeField] private Image           rollBar    = null!;
        [SerializeField] private TextMeshProUGUI rollNumber = null!;

        [Header("Stat — Spin")]
        [SerializeField] private TextMeshProUGUI spinName   = null!;
        [SerializeField] private Image           spinBar    = null!;
        [SerializeField] private TextMeshProUGUI spinNumber = null!;

        [Header("Buttons")]
        [SerializeField] private Button compareButton = null!;

        [Header("Carousel")]
        [SerializeField] private BallCarouselController? carousel;

        [Header("Compare")]
        [SerializeField] private BallCompareController? compareController;

        private string currentBallId = "";

        private const int BALL_STAT_MAX = 10;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnBallSelected += OnBallSelected;

            if (compareButton != null)
                compareButton.onClick.AddListener(OnCompareClicked);

            // The Settings overlay leaves the screen underneath enabled, so this panel never
            // re-enables on a language switch and its imperatively-bound labels kept the old
            // language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;

            // Re-bind on enable, not just on selection. The panel binds once in Start() and then
            // only when the carousel fires a selection, so entering the tab after the language
            // changed elsewhere left the previous language's copy on screen — and unlike the
            // repaint bugs, leaving and re-entering did NOT fix it.
            RefreshLocalizedText();
        }


        /// <summary>Get(key), falling back to the raw CSV copy when the row is absent.</summary>
        static string LocalizeBody(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            string v = LocalizationManager.Get(key);
            return string.Equals(v, key, System.StringComparison.Ordinal) ? fallback : v;
        }

        /// <summary>"ball_putt_ace" -> "PUTT_ACE".</summary>
        static string KeySuffix(string id, string prefix)
        {
            if (string.IsNullOrEmpty(id)) return "";
            if (id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                id = id.Substring(prefix.Length);
            return id.ToUpperInvariant();
        }

        /// <summary>Re-resolve the panel against the current language. UpdatePanel is a pure re-bind.</summary>
        private void RefreshLocalizedText()
        {
            if (!string.IsNullOrEmpty(currentBallId)) UpdatePanel(currentBallId);
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnBallSelected -= OnBallSelected;

            if (compareButton != null)
                compareButton.onClick.RemoveListener(OnCompareClicked);

            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        private void Start()
        {
            // Show first ball if none selected yet
            if (string.IsNullOrEmpty(currentBallId))
            {
                var firstId = BallManager.Instance?.GetAllOwnedBallIds();
                if (firstId != null && firstId.Count > 0)
                    UpdatePanel(firstId[0]);
            }
        }

        // ── Event Handlers ─────────────────────────────────────────────────────

        private void OnBallSelected(string ballId)
        {
            UpdatePanel(ballId);
        }

        private void OnCompareClicked()
        {
            if (compareController != null)
                compareController.EnterCompareMode(currentBallId);
            else
                Debug.Log("[BallDetailPanel] compareController not wired — run GOLFIN/Setup/Ball Compare.");
        }

        /// <summary>Called by BallCompareController after exiting compare mode.</summary>
        public void ShowBall(string ballId)
        {
            UpdatePanel(ballId);
        }

        // ── Panel Update ───────────────────────────────────────────────────────

        private void UpdatePanel(string ballId)
        {
            if (compareController != null && compareController.IsCompareMode) return;
            currentBallId = ballId;

            var playerBall = BallManager.Instance?.GetBallData(ballId);
            if (playerBall == null) return;

            var template = BallDatabaseCSV.Instance?.GetBall(ballId);
            if (template == null) return;

            // Ball image
            if (ballImage != null)
            {
                if (template.fullSprite != null)
                    ballImage.sprite = template.fullSprite;
                else if (template.thumbnailSprite != null)
                    ballImage.sprite = template.thumbnailSprite;
            }

            // Name
            if (ballNameText != null) ballNameText.text = template.name.ToUpper();

            // Owned + quantity
            if (ownedLabel != null) ownedLabel.text = LocalizationManager.Get("BALL_OWNED");
            if (quantityText != null)
                quantityText.text = BallManager.Instance?.GetQuantityDisplay(ballId) ?? "x0";

            // Info
            if (infoHeader != null) infoHeader.text = LocalizationManager.Get("BALL_INFO");
            // template.info is the raw English CSV blurb, so the body stayed English even
            // though the 情報 header above it localized. Key by id, exactly as the mode cards
            // and hole cards do; a missing row falls back to the CSV string, so English is
            // byte-identical to before.
            if (infoText != null)
                infoText.text = LocalizeBody("BALL_INFO_" + KeySuffix(template.ballId, "ball_"), template.info);

            // Stat bars
            UpdateBallStatBar(powerName, powerBar, powerNumber,
                LocalizationManager.Get("BALL_POWER"), template.power);
            UpdateBallStatBar(reboundName, reboundBar, reboundNumber,
                LocalizationManager.Get("BALL_REBOUND"), template.rebound);
            UpdateBallStatBar(windResistanceName, windResistanceBar, windResistanceNumber,
                LocalizationManager.Get("BALL_WIND_RESISTANCE"), template.windResistance);
            UpdateBallStatBar(rollName, rollBar, rollNumber,
                LocalizationManager.Get("BALL_ROLL"), template.roll);
            UpdateBallStatBar(spinName, spinBar, spinNumber,
                LocalizationManager.Get("BALL_SPIN"), template.spin);
        }

        // ── Stat Bar Helper ────────────────────────────────────────────────────

        private void UpdateBallStatBar(TextMeshProUGUI? nameField, Image? bar,
            TextMeshProUGUI? numberField, string label, int value)
        {
            if (nameField != null) nameField.text = label;

            // Number shows +/- prefix
            if (numberField != null)
            {
                if (value > 0)      numberField.text = $"+{value}";
                else if (value < 0) numberField.text = $"{value}";
                else                numberField.text = "0";
            }

            // Segmented bar — get-or-add BallSegmentedBar, then set value
            if (bar != null)
            {
                var seg = bar.GetComponent<BallSegmentedBar>();
                if (seg == null) seg = bar.gameObject.AddComponent<BallSegmentedBar>();
                seg.SetValue(value, BALL_STAT_MAX);
            }
        }
    }
}
