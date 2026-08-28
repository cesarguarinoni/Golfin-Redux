using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.UI;
using Golfin.UI.Matchmaking;
using Golfin.Gameplay.Session;

namespace GolfinRedux.UI.ModeSelect
{
    /// <summary>
    /// Full-screen vertical Mode Select controller.
    /// Cloned from HoleSelectionScreenController; adapted for ModeData.
    ///
    /// ITER-6 FIDELITY CHANGES (§6.3):
    ///   Item 11: CardsContainer back panel 1074w, gradient, 3px white/0.9 border, rounded-20, pad-24, gap-24.
    ///   Item 12: Cards 978 wide, inset 48 inside the 1074 panel.
    ///   Item 16: Per-card chevron HIDDEN on full-screen list cards (SetShowChevron(false)).
    ///   Item 17: ENTRY FEE/REWARDS labels kept on all cards.
    /// </summary>
    public class ModeSelectScreenController : MonoBehaviour
    {
        // ── The targets THIS BUILD dispatches (game_modes_admin §2) ──────────────────
        //
        // These are `const`, so the `switch` in HandlePlayClicked can use them as case
        // labels and `DispatchableTargets` can be built from the SAME symbols. That is
        // the whole trick: the withhold rule in ModesDatabaseCSV needs to know which
        // targets are routable, and the alternative — a second literal list somewhere —
        // is a list that silently goes stale the first time a target is added here.
        // With this, adding a case without adding it to the array does not compile past
        // the array, and adding it to the array without a case is the one direction that
        // is safe (a mode that renders and logs "no route" rather than never rendering).
        //
        // `TargetNone` is IN the set on purpose. It is not a route — it is the explicit
        // "deliberately not enterable", and its cards are the Coming Soon ones the game
        // has always shipped. Withholding it would make Driving Range and Missions
        // vanish, which is not what "this build cannot enter it" is supposed to mean.

        /// <summary>PLAY opens hole selection (Practice).</summary>
        public const string TargetHoleSelect = "hole_select";

        /// <summary>PLAY opens the 1v1 matchmaking modal.</summary>
        public const string TargetMatchmaking1v1 = "matchmaking_1v1";

        /// <summary>PLAY opens the tournament browse screen.</summary>
        public const string TargetTournaments = "tournaments";

        /// <summary>Deliberately not enterable — the Coming Soon cards.</summary>
        public const string TargetNone = "none";

        /// <summary>
        /// Every <c>modes.target</c> this build understands. Read by
        /// <see cref="ModesDatabaseCSV"/> to withhold a published mode whose target this
        /// build cannot route — an overlay can add a mode at any time, and a card that
        /// taps into nothing is worse than no card at all.
        /// </summary>
        public static readonly string[] DispatchableTargets =
        {
            TargetHoleSelect, TargetMatchmaking1v1, TargetTournaments, TargetNone,
        };

        /// <summary>Case-insensitive membership in <see cref="DispatchableTargets"/>.</summary>
        public static bool CanDispatch(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;
            string t = target.Trim();
            foreach (string known in DispatchableTargets)
                if (string.Equals(known, t, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        [Header("Cards List")]
        [SerializeField] private ScrollRect cardsScrollRect;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private ModeCardController cardPrefab;

        [Header("Back Panel (§6.2 Cards Container — 1074w, gradient, 3px border, rounded-20)")]
        [SerializeField] private RectTransform cardsContainerPanel;

        [Header("Screen Manager (optional — falls back to singleton)")]
        [SerializeField] private ScreenManager screenManager;

        [Header("1v1 Matchmaking Modal")]
        [SerializeField] private MatchmakingModalController matchmakingModal1v1;

        [Header("Initial state")]
        [Tooltip("Mode id to show expanded when the screen opens. Empty = all collapsed.")]
        [SerializeField] private string _initialExpandedModeId = "practice";

        private readonly List<ModeCardController> _cards = new List<ModeCardController>();
        // NOTE: _savedUsernameText and the SetUsername("MODE SELECTION") call were removed in
        // iter-10 (leaderboard_wiring). The "MODE SELECTION" top-bar center text is now driven
        // centrally by PersistentUIManager.HighlightScreen(ScreenId.ModeSelection) — the same
        // mechanism used for "LEADERBOARD" on the Rankings screen. This avoids corrupting the
        // cached _username field via transient SetUsername calls (BLOCKER 1 in REDTEAM_REVIEW).

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildCardsNextFrame());
        }

        private IEnumerator RebuildCardsNextFrame()
        {
            yield return null;
            RebuildCards();
        }

        private void OnDisable()
        {
            UnwireCards();
            _cards.Clear();
        }

        private void RebuildCards()
        {
            UnwireCards();
            if (cardsContent != null)
                foreach (Transform child in cardsContent) Destroy(child.gameObject);
            _cards.Clear();

            var db = ModesDatabaseCSV.Instance;
            if (db == null)
            {
                Debug.LogWarning("[ModeSelectScreen] ModesDatabaseCSV.Instance is null.");
                return;
            }

            if (cardPrefab == null)
            {
                Debug.LogError("[ModeSelectScreen] cardPrefab is null.");
                return;
            }

            var modes = db.GetAllModes();

            foreach (var mode in modes)
            {
                var card = Instantiate(cardPrefab, cardsContent);
                ModeCardState state = mode.locked
                    ? ModeCardState.Locked
                    : (!string.IsNullOrEmpty(_initialExpandedModeId) && mode.id == _initialExpandedModeId
                        ? ModeCardState.Expanded
                        : ModeCardState.Collapsed);
                // §6.3 item 16: NO expand chevron on full-screen list cards
                card.SetShowChevron(false);
                card.Bind(mode, state);
                card.OnCardTapped  += HandleCardTapped;
                card.OnPlayClicked += HandlePlayClicked;
                _cards.Add(card);
            }

            if (cardsScrollRect != null)
                cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void UnwireCards()
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardTapped  -= HandleCardTapped;
                c.OnPlayClicked -= HandlePlayClicked;
            }
        }

        private void HandleCardTapped(ModeCardController card)
        {
            if (card == null || card.State == ModeCardState.Locked) return;

            if (card.State == ModeCardState.Expanded)
            {
                card.SetState(ModeCardState.Collapsed);
                return;
            }

            foreach (var c in _cards)
            {
                if (c != null && c != card && c.State == ModeCardState.Expanded)
                    c.SetState(ModeCardState.Collapsed);
            }

            card.SetState(ModeCardState.Expanded);
            StartCoroutine(CentreCardNextFrame(card));
        }

        private void HandlePlayClicked(ModeCardController card)
        {
            if (card == null || card.State == ModeCardState.Locked) return;

            var db = ModesDatabaseCSV.Instance;
            if (db == null) return;

            var mode = db.GetMode(card.ModeId);
            if (mode == null) return;

            ScreenManager sm = screenManager != null ? screenManager : ScreenManager.Instance;

            switch (mode.target)
            {
                case TargetHoleSelect:
                    if (sm != null)
                        sm.ShowScreen(ScreenId.HoleSelection);
                    else
                        Debug.LogWarning("[ModeSelectScreen] ScreenManager not found.");
                    break;

                case TargetTournaments:
                    // Tournament browse screen (T7). No entry-fee spend here — per-tournament
                    // fees are owned by the signup flow (TournamentSignupModalController).
                    if (sm != null)
                        sm.ShowScreen(ScreenId.TournamentSelection);
                    else
                        Debug.LogWarning("[ModeSelectScreen] Tournaments PLAY — ScreenManager not found.");
                    break;

                case TargetMatchmaking1v1:
                    // 1v1 path: flag the session as versus BEFORE opening matchmaking.
                    GameSession.IsVersus = true;
                    // Pick a random hole (1-18), then open matchmaking modal.
                    // MatchmakingModalController.Open expects a 0-based index.
                    if (matchmakingModal1v1 != null)
                    {
                        int randomHoleIndex = UnityEngine.Random.Range(0, 18); // 0-based → hole numbers 1-18
                        matchmakingModal1v1.Open(randomHoleIndex);
                    }
                    else
                    {
                        Debug.LogWarning("[ModeSelectScreen] 1v1 PLAY — matchmakingModal1v1 not wired in Inspector.");
                    }
                    break;

                case TargetNone:
                default:
                    Debug.LogWarning($"[ModeSelectScreen] PLAY on mode '{card.ModeId}' has no route.");
                    break;
            }
        }

        private IEnumerator CentreCardNextFrame(ModeCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (cardsScrollRect == null || card == null || card.rootRect == null) yield break;

            var content  = cardsScrollRect.content;
            var viewport = cardsScrollRect.viewport;
            var cardRt   = card.rootRect;

            float cardCentreFromTop  = -cardRt.anchoredPosition.y + cardRt.rect.height * 0.5f;
            float scrollableHeight   = content.rect.height - viewport.rect.height;
            if (scrollableHeight <= 0f) yield break;

            float targetCentreFromTop = cardCentreFromTop - viewport.rect.height * 0.5f;
            float normalized = Mathf.Clamp01(1f - targetCentreFromTop / scrollableHeight);
            cardsScrollRect.verticalNormalizedPosition = normalized;
        }
    }
}
