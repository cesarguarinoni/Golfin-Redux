// Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs
// gacha_prizes Stage 1 — Controller for the Gacha Prizes screen.
// Dual mode (x1 / x10) parameterised by PendingPullCount (set before ShowScreen).
// x10: shows 4/4/2 card grid with all 10 mock prizes.
// x1:  hides the three prize rows, shows the x1CardSlot with ONE centred card.
// BACK → ScreenId.GeneralShop. PULL stub (no-op / log).
#nullable enable
using System.Collections.Generic;
using Golfin.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Attached to the root of GachaPrizesScreen.prefab (scene instance).
    /// Binds 10 mock prize cards via BagClubCard.Initialize(), mirrors the
    /// GachaHistoryRow binding pattern (display-only, all buttons non-interactable).
    /// </summary>
    public class GachaPrizesScreenController : MonoBehaviour
    {
        // ── Static pending context (set by caller before ShowScreen) ───────────

        private static int s_pendingPullCount = 10;

        /// <summary>
        /// Set this BEFORE calling ScreenManager.ShowScreen(ScreenId.GachaPrizes).
        /// 1 = x1 mode (single centred card), 10 = x10 mode (full 4/4/2 grid).
        /// Resets to 10 after each OnEnable so the default is always x10.
        /// </summary>
        public static void SetPendingPullCount(int n) => s_pendingPullCount = n;

        // ── Inspector refs ─────────────────────────────────────────────────────

        [Header("Grid rows (x10 mode)")]
        [SerializeField] private GameObject? _prizeRow1;   // 4 cards
        [SerializeField] private GameObject? _prizeRow2;   // 4 cards
        [SerializeField] private GameObject? _prizeRow3;   // 2 cards

        [Header("x1 mode — centred single card")]
        [SerializeField] private GameObject? _x1CardSlot;  // container with LayoutElement.preferredHeight=1170
        [SerializeField] private BagClubCard? _x1Card;     // the single centred BagClubCard

        [Header("Labels")]
        [SerializeField] private TMP_Text? _costMultiLabel;   // "x10" / "x1"  (CostRow > x10Label)
        [SerializeField] private TMP_Text? _pullButtonLabel;  // "PULL x10" / "PULL x1" (PullButton > PlayLable)

        [Header("Buttons")]
        [SerializeField] private Button? _pullButton;
        [SerializeField] private Button? _backButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        private BagClubCard[] _gridCards = System.Array.Empty<BagClubCard>();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _gridCards = CollectGridCards();

            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBack);
            }

            if (_pullButton != null)
            {
                _pullButton.onClick.RemoveAllListeners();
                _pullButton.onClick.AddListener(OnPull);
            }
        }

        private void OnEnable()
        {
            int pullCount = s_pendingPullCount;
            s_pendingPullCount = 10; // reset default

            _pullCount = pullCount;
            ApplyMode(pullCount);

            // The PULL label is resolved imperatively here, so — unlike a LocalizedText label —
            // nothing repaints it when the language changes. The toggle lives in the Settings
            // OVERLAY, which leaves this screen enabled, so OnEnable never re-ran and the label
            // kept the old language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;
        }

        // The pull count OnEnable consumed. s_pendingPullCount is reset to 10 as it is read, so it
        // cannot be re-read later — a refresh has to use this instead or an x1 pull would relabel
        // itself as x10.
        private int _pullCount = 10;

        private void RefreshLocalizedText()
        {
            if (_pullButtonLabel != null)
                _pullButtonLabel.text = LocalizationManager.Get(_pullCount != 1 ? "GACHA_PULL_X10" : "GACHA_PULL_X1");
        }

        // ── Mode logic ─────────────────────────────────────────────────────────

        private void ApplyMode(int pullCount)
        {
            bool isX10 = pullCount != 1;

            // Toggle row visibility
            SetActive(_prizeRow1, isX10);
            SetActive(_prizeRow2, isX10);
            SetActive(_prizeRow3, isX10);
            SetActive(_x1CardSlot, !isX10);

            // Update labels
            string multiplier = isX10 ? "x10" : "x1";
            if (_costMultiLabel != null) _costMultiLabel.text = multiplier;
            if (_pullButtonLabel != null)
                _pullButtonLabel.text = LocalizationManager.Get(isX10 ? "GACHA_PULL_X10" : "GACHA_PULL_X1");

            // Bind cards
            if (isX10)
                BindGridCards();
            else
                BindX1Card();
        }

        // ── Card binding ───────────────────────────────────────────────────────

        private void BindGridCards()
        {
            var pool = GachaMockPrizePool.GetMockPrizes();
            int len = Mathf.Min(_gridCards.Length, pool.Length);
            for (int i = 0; i < len; i++)
                BindCard(_gridCards[i], pool[i]);
        }

        private void BindX1Card()
        {
            if (_x1Card == null) return;
            BindCard(_x1Card, GachaMockPrizePool.GetX1Prize());
        }

        private static void BindCard(BagClubCard card, PrizeRecord record)
        {
            if (card == null) return;

            var template = ClubDatabaseCSV.Instance?.GetClub(record.ClubId);
            if (template == null)
            {
                Debug.LogWarning($"[GachaPrizesScreenController] Club not found: {record.ClubId}");
                return;
            }

            var playerClub = new PlayerClubData
            {
                clubId            = record.ClubId,
                currentLevel      = 1,
                currentDurability = template.maxDurability,
                maxDurability     = template.maxDurability,
            };

            card.Initialize(playerClub, template, "");

            // Display-only — disable all interactive buttons on the card.
            foreach (var btn in card.GetComponentsInChildren<Button>(includeInactive: true))
                btn.interactable = false;
        }

        // ── Button handlers ────────────────────────────────────────────────────

        private void OnBack()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.ShowScreen(GolfinRedux.UI.ScreenId.GeneralShop);
            else
                Debug.LogWarning("[GachaPrizesScreenController] ScreenManager not found — cannot go back.");
        }

        private void OnPull()
        {
            // Stub — real pull logic deferred to gacha_pull_animation order.
            Debug.Log("[GachaPrizesScreenController] Pull tapped — stub (deferred).");
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Collects all BagClubCard components from the direct children of each prize row.
        /// Iterates Row1 (4 cards), Row2 (4 cards), Row3 (2 cards) in order.
        /// </summary>
        private BagClubCard[] CollectGridCards()
        {
            var list = new List<BagClubCard>();
            AppendChildCards(_prizeRow1, list);
            AppendChildCards(_prizeRow2, list);
            AppendChildCards(_prizeRow3, list);
            if (list.Count != 10)
                Debug.LogWarning($"[GachaPrizesScreenController] Expected 10 grid cards, found {list.Count}.");
            return list.ToArray();
        }

        private static void AppendChildCards(GameObject? row, List<BagClubCard> list)
        {
            if (row == null) return;
            foreach (Transform child in row.transform)
            {
                var card = child.GetComponent<BagClubCard>();
                if (card != null) list.Add(card);
            }
        }

        private static void SetActive(GameObject? go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
