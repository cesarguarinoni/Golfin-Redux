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

        private string currentBallId = "";

        private const int BALL_STAT_MAX = 10;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnBallSelected += OnBallSelected;

            if (compareButton != null)
                compareButton.onClick.AddListener(OnCompareClicked);
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnBallSelected -= OnBallSelected;

            if (compareButton != null)
                compareButton.onClick.RemoveListener(OnCompareClicked);
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
            Debug.Log("[BallDetailPanel] Compare coming soon.");
        }

        // ── Panel Update ───────────────────────────────────────────────────────

        private void UpdatePanel(string ballId)
        {
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
            if (infoText != null) infoText.text = template.info;

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
