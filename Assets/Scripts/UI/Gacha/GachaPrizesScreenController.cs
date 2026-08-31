// Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs
// gacha_prizes Stage 1 — Controller for the Gacha Prizes screen.
// Dual mode (x1 / x10) parameterised by the pending pull RESULT (set before ShowScreen).
// gacha_reveal_animation §1/§3 — the screen now binds the exact prize list the reveal modal
// just showed (SetPendingResult), PULL means "pull again" through GachaPullFlow, and the cards
// stagger in when the screen was opened by a pull (never on a BACK-navigation return).
// x10: shows 4/4/2 card grid with all 10 prizes of the result.
// x1:  hides the three prize rows, shows the x1CardSlot with ONE centred card.
// BACK → ScreenId.GeneralShop (history-aware). PULL → pull again via GachaPullFlow.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.Inventory;
using Golfin.Roster;
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

        // The last pull's prizes. NOT cleared on read: a BACK-navigation return to this screen
        // must re-bind the SAME result it showed before, not a freshly rolled one.
        private static IReadOnlyList<PrizeRecord> s_result = GachaPullFlow.BuildResult(10);

        // Consumed once by the next OnEnable — only a real pull earns the entrance animation.
        private static bool s_pendingEntrance;

        /// <summary>
        /// Set this BEFORE calling ScreenManager.ShowScreen(ScreenId.GachaPrizes).
        /// The list length picks the mode: 1 → x1 (single centred card), otherwise x10 (4/4/2 grid).
        /// Also arms the staggered card entrance for the next open.
        /// </summary>
        public static void SetPendingResult(IReadOnlyList<PrizeRecord> result)
        {
            if (result == null || result.Count == 0)
            {
                Debug.LogWarning("[GachaPrizesScreenController] SetPendingResult got an empty result — keeping the previous one.");
                return;
            }

            s_result = result;
            s_pendingEntrance = true;
        }

        /// <summary>
        /// Thin wrapper kept for GachaTabController's (dead) x1/x10 entry points — it rolls a
        /// result of the requested size and defers to <see cref="SetPendingResult"/>.
        /// </summary>
        public static void SetPendingPullCount(int n) => SetPendingResult(GachaPullFlow.BuildResult(n));

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
            // The mode is DERIVED from the result rather than carried alongside it, so the
            // labels and the grid can never disagree about what was pulled.
            int pullCount = s_result.Count == 1 ? 1 : 10;
            bool playEntrance = s_pendingEntrance;
            s_pendingEntrance = false;

            _pullCount = pullCount;
            ApplyMode(pullCount);

            if (playEntrance) _entranceRoutine = StartCoroutine(PlayEntrance());

            // The PULL label is resolved imperatively here, so — unlike a LocalizedText label —
            // nothing repaints it when the language changes. The toggle lives in the Settings
            // OVERLAY, which leaves this screen enabled, so OnEnable never re-ran and the label
            // kept the old language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

            // Leaving mid-entrance would strand cards at scale 0 / alpha 0; ApplyMode resets
            // them on the next open, but stop the routine so it cannot fight that reset.
            if (_entranceRoutine != null) { StopCoroutine(_entranceRoutine); _entranceRoutine = null; }
        }

        // Runs the staggered card entrance; tracked so a screen change can stop it.
        private Coroutine? _entranceRoutine;

        // The pull count OnEnable derived from the result. RefreshLocalizedText (a language
        // change while the screen is already open) needs it, and it must not be re-derived from
        // a result that a "pull again" may have replaced in the meantime.
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

            // Always start from fully-visible cards: PlayEntrance primes them to 0 and a
            // mid-animation screen change would otherwise leave them invisible forever.
            ResetCardVisuals();

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
            for (int i = 0; i < _gridCards.Length; i++)
            {
                var card = _gridCards[i];
                if (card == null) continue;

                // A result shorter than the grid (never today, but the real pull may vary)
                // hides the surplus slots rather than leaving stale prizes on screen.
                bool hasPrize = i < s_result.Count;
                card.gameObject.SetActive(hasPrize);
                if (hasPrize) BindCard(card, s_result[i]);
            }
        }

        private void BindX1Card()
        {
            if (_x1Card == null || s_result.Count == 0) return;
            _x1Card.gameObject.SetActive(true);
            BindCard(_x1Card, s_result[0]);
        }

        /// <summary>
        /// The one binder for a prize card — shared with the reveal modal so the card the player
        /// sees pop out of the bag and the card on this screen are built by the same code.
        /// </summary>
        internal static void BindCard(BagClubCard card, PrizeRecord record)
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

            // Display-only. Hiding the action row is not cosmetic tidying: the Prizes screen's
            // grid cards ship with LevelUpBtn / RepairBtn / SwapBtn DEACTIVATED in the prefab,
            // while a fresh BagClubCard instance (which is what the reveal modal spawns) has
            // them active — so the same prize rendered two different ways depending on where it
            // was shown. Doing it here, in the one shared binder, is what makes them agree, and
            // it matches the Figma reveal card (13997:4503), which has no action row.
            foreach (var n in ActionButtonPaths)
            {
                var t = card.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            foreach (var btn in card.GetComponentsInChildren<Button>(includeInactive: true))
                btn.interactable = false;
        }

        // Paths inside BagClubCard.prefab, verified against the prefab hierarchy.
        private static readonly string[] ActionButtonPaths =
        {
            "Mask/Background/ButtonRow/LevelUpBtn",
            "Mask/Background/ButtonRow/RepairBtn",
            "SwapBtn",
        };

        /// <summary>
        /// The rarity of a prize, resolved through the club template. Used by the reveal modal
        /// to pick its FX tier and tint.
        /// </summary>
        internal static CharacterRarity ResolveRarity(PrizeRecord record)
        {
            var template = ClubDatabaseCSV.Instance?.GetClub(record.ClubId);
            return template != null ? template.rarity : CharacterRarity.Common;
        }

        // ── Entrance animation (gacha_reveal_animation §3) ─────────────────────

        private const float EntranceStaggerSec = 0.045f;

        private IEnumerator PlayEntrance()
        {
            bool isX10 = _pullCount != 1;

            var cards = new List<BagClubCard>();
            if (isX10)
            {
                foreach (var c in _gridCards)
                    if (c != null && c.gameObject.activeSelf) cards.Add(c);
            }
            else if (_x1Card != null)
            {
                cards.Add(_x1Card);
            }

            // Prime every card BEFORE the first pop, otherwise card 10 would be visible at full
            // size for 400 ms while the stagger walks down to it.
            var groups = new CanvasGroup[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                groups[i] = EnsureGroup(cards[i]);
                groups[i].alpha = 0f;
                cards[i].transform.localScale = Vector3.zero;
            }

            float duration = isX10 ? 0.25f : 0.3f;

            for (int i = 0; i < cards.Count; i++)
            {
                StartCoroutine(PopIn(cards[i].transform, groups[i], duration));
                if (i < cards.Count - 1)
                    yield return new WaitForSecondsRealtime(EntranceStaggerSec);
            }

            yield return new WaitForSecondsRealtime(duration);
            _entranceRoutine = null;
        }

        private static IEnumerator PopIn(Transform t, CanvasGroup group, float duration)
        {
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / duration);
                float s = Mathf.LerpUnclamped(0f, 1f, EaseOutBack(k));
                if (t != null) t.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = k;
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }

        private void ResetCardVisuals()
        {
            foreach (var c in _gridCards) ResetCardVisual(c);
            ResetCardVisual(_x1Card);
        }

        private static void ResetCardVisual(BagClubCard? card)
        {
            if (card == null) return;
            card.transform.localScale = Vector3.one;
            EnsureGroup(card).alpha = 1f;
        }

        // The prize cards ship without a CanvasGroup; adding it at runtime keeps the prefab
        // untouched (SPEC §3).
        private static CanvasGroup EnsureGroup(BagClubCard card)
        {
            var g = card.GetComponent<CanvasGroup>();
            if (g == null) g = card.gameObject.AddComponent<CanvasGroup>();
            return g;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // ── Button handlers ────────────────────────────────────────────────────

        // nav_back_memory §3 — history first, the Rewards Center as the fallback.
        private void OnBack()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.GoBack(GolfinRedux.UI.ScreenId.GeneralShop);
            else
                Debug.LogWarning("[GachaPrizesScreenController] ScreenManager not found — cannot go back.");
        }

        // PULL on the RESULT screen means "pull again": same count, full reveal, then this
        // screen re-binds to the new result. No ticket spend yet (blocked on content).
        private void OnPull()
        {
            Debug.Log($"[GachaPrizesScreenController] Pull again tapped (x{_pullCount}).");
            GachaPullFlow.Pull(_pullCount);
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
