#nullable enable
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Inventory
{
    /// <summary>
    /// Manages Compare Mode for the Ball Detail Panel.
    /// Attach to BallDetailPanel (same GameObject as BallDetailPanel component).
    ///
    /// State machine:
    ///   NORMAL → EnterCompareMode(ballId) → COMPARE (empty right)
    ///         → OnCarouselSelection(otherId)  → COMPARE (filled right, with stat diffs)
    ///         → ExitCompareMode()             → NORMAL
    ///         → Right COMPARE button          → NORMAL then COMPARE with right as new left
    ///
    /// Stat bars in right column reuse BallSegmentedBar (get-or-add at runtime).
    /// </summary>
    public class BallCompareController : MonoBehaviour
    {
        // ── Normal Mode ─────────────────────────────────────────────────────────
        [Header("Normal Mode References")]
        [SerializeField] private GameObject    leftPanel  = null!;
        [SerializeField] private RectTransform rightPanel = null!;

        // ── Compare Mode Layout ──────────────────────────────────────────────────
        [Header("Compare Mode References")]
        [SerializeField] private GameObject      compareRightPanel      = null!;
        [SerializeField] private GameObject      comparePlaceholder     = null!;
        [SerializeField] private TextMeshProUGUI comparePlaceholderText = null!;
        [SerializeField] private GameObject      compareInfoPanel       = null!;
        [SerializeField] private GameObject      verticalDivider        = null!;

        // ── Left Column Buttons ──────────────────────────────────────────────────
        [Header("Left Column Buttons")]
        [SerializeField] private Button compareButton      = null!;   // shown in normal mode
        [SerializeField] private Button closeCompareButton = null!;   // shown in compare mode

        // ── Right Column — Ball Info ─────────────────────────────────────────────
        [Header("Right Column — Ball Info")]
        [SerializeField] private TextMeshProUGUI compareNameText     = null!;
        [SerializeField] private TextMeshProUGUI compareQuantityText = null!;

        // ── Right Column — Stat: Power ───────────────────────────────────────────
        [Header("Right Column — Stat: Power")]
        [SerializeField] private TextMeshProUGUI comparePowerName   = null!;
        [SerializeField] private Image           comparePowerBar    = null!;
        [SerializeField] private TextMeshProUGUI comparePowerNumber = null!;
        [SerializeField] private TextMeshProUGUI comparePowerDiff   = null!;

        // ── Right Column — Stat: Rebound ─────────────────────────────────────────
        [Header("Right Column — Stat: Rebound")]
        [SerializeField] private TextMeshProUGUI compareReboundName   = null!;
        [SerializeField] private Image           compareReboundBar    = null!;
        [SerializeField] private TextMeshProUGUI compareReboundNumber = null!;
        [SerializeField] private TextMeshProUGUI compareReboundDiff   = null!;

        // ── Right Column — Stat: Wind Resistance ─────────────────────────────────
        [Header("Right Column — Stat: Wind Resistance")]
        [SerializeField] private TextMeshProUGUI compareWindResistanceName   = null!;
        [SerializeField] private Image           compareWindResistanceBar    = null!;
        [SerializeField] private TextMeshProUGUI compareWindResistanceNumber = null!;
        [SerializeField] private TextMeshProUGUI compareWindResistanceDiff   = null!;

        // ── Right Column — Stat: Roll ────────────────────────────────────────────
        [Header("Right Column — Stat: Roll")]
        [SerializeField] private TextMeshProUGUI compareRollName   = null!;
        [SerializeField] private Image           compareRollBar    = null!;
        [SerializeField] private TextMeshProUGUI compareRollNumber = null!;
        [SerializeField] private TextMeshProUGUI compareRollDiff   = null!;

        // ── Right Column — Stat: Spin ────────────────────────────────────────────
        [Header("Right Column — Stat: Spin")]
        [SerializeField] private TextMeshProUGUI compareSpinName   = null!;
        [SerializeField] private Image           compareSpinBar    = null!;
        [SerializeField] private TextMeshProUGUI compareSpinNumber = null!;
        [SerializeField] private TextMeshProUGUI compareSpinDiff   = null!;

        // ── Right Column — Buttons ───────────────────────────────────────────────
        [Header("Right Column — Buttons")]
        [SerializeField] private Button compareRightCompareButton = null!;

        // ── Carousel ─────────────────────────────────────────────────────────────
        [Header("Carousel")]
        [SerializeField] private BallCarouselController? carousel;

        // ── Animation ────────────────────────────────────────────────────────────
        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.3f;
        [SerializeField] private float fadeDuration  = 0.2f;

        // ── Colors ───────────────────────────────────────────────────────────────
        private static readonly Color DiffPositiveColor = new(0.2f, 0.8f, 0.3f, 1f);
        private static readonly Color DiffNegativeColor = new(0.9f, 0.2f, 0.2f, 1f);

        private const int BALL_STAT_MAX = 10;

        // ── Runtime State ────────────────────────────────────────────────────────
        private bool    _isCompareMode = false;
        private string  _leftBallId    = "";
        private string? _rightBallId   = null;
        private Vector2 _normalRightPos;
        private Vector2 _compareLeftPos;

        public bool IsCompareMode => _isCompareMode;

        // ── Lifecycle ────────────────────────────────────────────────────────────

        private void Awake()
        {
            SafeSetActive(compareRightPanel,              false);
            SafeSetActive(verticalDivider,                false);
            SafeSetActive(closeCompareButton?.gameObject, false);
        }

        private void Start()
        {
            closeCompareButton?.onClick.AddListener(ExitCompareMode);
            compareRightCompareButton?.onClick.AddListener(OnRightCompareClicked);
        }

        private void OnEnable()
        {
            if (carousel != null)
                carousel.OnBallSelected += OnCarouselSelection;
        }

        private void OnDisable()
        {
            if (carousel != null)
                carousel.OnBallSelected -= OnCarouselSelection;
        }

        // ── Enter / Exit ─────────────────────────────────────────────────────────

        /// <summary>Called by BallDetailPanel when COMPARE is tapped.</summary>
        public void EnterCompareMode(string ballId)
        {
            if (_isCompareMode) return;

            _isCompareMode = true;
            _leftBallId    = ballId;
            _rightBallId   = null;

            _normalRightPos = rightPanel.anchoredPosition;

            var detailRect = rightPanel.parent?.GetComponent<RectTransform>();
            float detailW  = detailRect != null ? detailRect.rect.width : 800f;
            _compareLeftPos = new Vector2(-detailW * 0.25f, _normalRightPos.y);

            StopAllCoroutines();

            SafeSetActive(leftPanel, false);
            StartCoroutine(SlidePanel(rightPanel, _compareLeftPos, slideDuration));

            if (comparePlaceholderText != null)
                comparePlaceholderText.text = LocalizationManager.Get("BALL_COMPARE_EMPTY_PROMPT");

            SafeSetActive(comparePlaceholder, true);
            SafeSetActive(compareInfoPanel,   false);
            StartCoroutine(FadeIn(compareRightPanel, fadeDuration, slideDuration));

            SafeSetActive(verticalDivider, true);

            SafeSetActive(compareButton?.gameObject,      false);
            SafeSetActive(closeCompareButton?.gameObject, true);

            Debug.Log($"[BallCompareController] Entered compare mode for {ballId}");
        }

        public void ExitCompareMode()
        {
            if (!_isCompareMode) return;
            Debug.Log("[BallCompareController] Exited compare mode");
            CleanupAndExit();
        }

        // ── Carousel Interception ────────────────────────────────────────────────

        private void OnCarouselSelection(string ballId)
        {
            if (!_isCompareMode) return;

            if (ballId == _leftBallId)
            {
                ExitCompareMode();
                return;
            }

            _rightBallId = ballId;

            StopAllCoroutines();
            SafeSetActive(compareRightPanel, true);
            SetAlpha(compareRightPanel, 1f);

            SafeSetActive(comparePlaceholder, false);
            SafeSetActive(compareInfoPanel,   true);

            RefreshRightColumn(ballId);

            Debug.Log($"[BallCompareController] Comparing {_leftBallId} vs {ballId}");
        }

        // ── Data Binding ─────────────────────────────────────────────────────────

        private void RefreshRightColumn(string ballId)
        {
            if (BallManager.Instance == null || BallDatabaseCSV.Instance == null) return;

            var rightTemplate = BallDatabaseCSV.Instance.GetBall(ballId);
            if (rightTemplate == null) return;

            var leftTemplate = BallDatabaseCSV.Instance.GetBall(_leftBallId);

            // Name + quantity
            if (compareNameText != null)
                compareNameText.text = rightTemplate.name.ToUpper();
            if (compareQuantityText != null)
                compareQuantityText.text = BallManager.Instance.GetQuantityDisplay(ballId);

            // Stat values
            int rPow  = rightTemplate.power;
            int rReb  = rightTemplate.rebound;
            int rWind = rightTemplate.windResistance;
            int rRoll = rightTemplate.roll;
            int rSpin = rightTemplate.spin;

            int lPow  = leftTemplate?.power          ?? 0;
            int lReb  = leftTemplate?.rebound        ?? 0;
            int lWind = leftTemplate?.windResistance ?? 0;
            int lRoll = leftTemplate?.roll           ?? 0;
            int lSpin = leftTemplate?.spin           ?? 0;

            UpdateCompareStatBar(comparePowerName, comparePowerBar, comparePowerNumber, comparePowerDiff,
                LocalizationManager.Get("BALL_POWER"), rPow, lPow);
            UpdateCompareStatBar(compareReboundName, compareReboundBar, compareReboundNumber, compareReboundDiff,
                LocalizationManager.Get("BALL_REBOUND"), rReb, lReb);
            UpdateCompareStatBar(compareWindResistanceName, compareWindResistanceBar, compareWindResistanceNumber, compareWindResistanceDiff,
                LocalizationManager.Get("BALL_WIND_RESISTANCE"), rWind, lWind);
            UpdateCompareStatBar(compareRollName, compareRollBar, compareRollNumber, compareRollDiff,
                LocalizationManager.Get("BALL_ROLL"), rRoll, lRoll);
            UpdateCompareStatBar(compareSpinName, compareSpinBar, compareSpinNumber, compareSpinDiff,
                LocalizationManager.Get("BALL_SPIN"), rSpin, lSpin);

            if (compareRightCompareButton != null)
                compareRightCompareButton.interactable = true;
        }

        // ── Stat Bar Helper ──────────────────────────────────────────────────────

        private void UpdateCompareStatBar(TextMeshProUGUI? nameField, Image? bar,
            TextMeshProUGUI? numberField, TextMeshProUGUI? diffField,
            string label, int value, int leftValue)
        {
            if (nameField != null) nameField.text = label;

            if (numberField != null)
            {
                if (value > 0)      numberField.text = $"+{value}";
                else if (value < 0) numberField.text = $"{value}";
                else                numberField.text = "0";
            }

            if (bar != null)
            {
                var seg = bar.GetComponent<BallSegmentedBar>();
                if (seg == null) seg = bar.gameObject.AddComponent<BallSegmentedBar>();
                seg.SetValue(value, BALL_STAT_MAX);
            }

            ShowStatDifference(diffField, leftValue, value);
        }

        private void ShowStatDifference(TextMeshProUGUI? diffLabel, int leftValue, int rightValue)
        {
            if (diffLabel == null) return;
            int diff = rightValue - leftValue;
            if (diff > 0)
            {
                diffLabel.text  = $"+{diff}";
                diffLabel.color = DiffPositiveColor;
                diffLabel.gameObject.SetActive(true);
            }
            else if (diff < 0)
            {
                diffLabel.text  = $"{diff}";
                diffLabel.color = DiffNegativeColor;
                diffLabel.gameObject.SetActive(true);
            }
            else
            {
                diffLabel.gameObject.SetActive(false);
            }
        }

        // ── Button Handlers ──────────────────────────────────────────────────────

        private void OnRightCompareClicked()
        {
            if (string.IsNullOrEmpty(_rightBallId)) return;
            string newLeft = _rightBallId;
            ForceExitImmediate();
            EnterCompareMode(newLeft);
        }

        // ── Cleanup ──────────────────────────────────────────────────────────────

        private void CleanupAndExit()
        {
            _isCompareMode = false;
            _rightBallId   = null;

            StopAllCoroutines();
            StartCoroutine(FadeOut(compareRightPanel, fadeDuration));
            SafeSetActive(verticalDivider, false);
            StartCoroutine(SlidePanel(rightPanel, _normalRightPos, slideDuration));
            SafeSetActive(leftPanel, true);

            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);

            // Let BallDetailPanel refresh with the left ball
            GetComponent<BallDetailPanel>()?.ShowBall(_leftBallId);
        }

        private void ForceExitImmediate()
        {
            StopAllCoroutines();
            _isCompareMode = false;
            _rightBallId   = null;

            SetAlpha(compareRightPanel, 0f);
            SafeSetActive(compareRightPanel, false);
            SafeSetActive(verticalDivider,   false);

            if (rightPanel != null) rightPanel.anchoredPosition = _normalRightPos;

            SafeSetActive(leftPanel, true);
            SafeSetActive(compareButton?.gameObject,      true);
            SafeSetActive(closeCompareButton?.gameObject, false);
        }

        // ── Animation Coroutines ─────────────────────────────────────────────────

        private IEnumerator FadeOut(GameObject obj, float duration)
        {
            if (obj == null) yield break;
            var cg = GetOrAddCG(obj);
            cg.alpha = 1f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            { cg.alpha = 1f - t / duration; yield return null; }
            cg.alpha = 0f;
            obj.SetActive(false);
        }

        private IEnumerator FadeIn(GameObject obj, float duration, float delay = 0f)
        {
            if (obj == null) yield break;
            if (delay > 0f) yield return new WaitForSeconds(delay);
            obj.SetActive(true);
            var cg = GetOrAddCG(obj);
            cg.alpha = 0f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            { cg.alpha = t / duration; yield return null; }
            cg.alpha = 1f;
        }

        private IEnumerator SlidePanel(RectTransform panel, Vector2 target, float duration)
        {
            if (panel == null) yield break;
            Vector2 start = panel.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float s = Mathf.SmoothStep(0f, 1f, t / duration);
                panel.anchoredPosition = Vector2.Lerp(start, target, s);
                yield return null;
            }
            panel.anchoredPosition = target;
        }

        // ── Utilities ────────────────────────────────────────────────────────────

        private static CanvasGroup GetOrAddCG(GameObject obj)
        {
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg == null) cg = obj.AddComponent<CanvasGroup>();
            return cg;
        }

        private static void SetAlpha(GameObject obj, float alpha)
        {
            if (obj == null) return;
            var cg = obj.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = alpha;
        }

        private static void SafeSetActive(GameObject? obj, bool active)
        {
            if (obj != null) obj.SetActive(active);
        }
    }
}
