#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.Gameplay.Missions;
using Golfin.Gameplay.Session;
using Golfin.UI.GameplayTransition;
using Golfin.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// The Mission Selection screen. Spec: missions_v1 §C2/§C3.
    ///
    /// CLONED FROM <c>HoleSelectionScreenController</c>: same filter-pill model, same
    /// instantiate-one-card-per-row loop, same single-expanded invariant, same centre-the-
    /// expanded-card scroll. The differences are what a mission IS rather than how a list works.
    ///
    /// CARD ORDER IS NOT CATALOG ORDER (§C2): cleared missions first (most recent at the top),
    /// then NEXT expanded and scrolled to, then everything still locked. A campaign list sorted
    /// by id would bury the one card the player came here to press.
    ///
    /// THE TIER TABS COUNT FROM DATA, never from a constant. `n/10` is
    /// `ClearedInTier / MissionsInTier`, both read from the catalog, so re-tiering the campaign
    /// from the admin moves the tabs with it. The Figma mockup says `25/25`; that is placeholder
    /// and the spec says so.
    ///
    /// ⚠️ THE ENTRY FEE IS NOT SPENT HERE. `ModeCardController` already debits
    /// `mode_entry_fee:missions` when the player taps PLAY on the mode card, before this screen
    /// opens. Charging again on the mission card would bill twice for one entry.
    /// </summary>
    public class MissionSelectionScreenController : MonoBehaviour
    {
        [System.Serializable]
        public class TierPill
        {
            public Button? button;
            public TextMeshProUGUI? label;
            public GameObject? lockIcon;
            /// <summary>Matches `mission_tiers.tier` — "Beginner", "Amateur", "Pro", "Legend".</summary>
            public string tier = "";
        }

        [Header("Filters")]
        [SerializeField] private TextMeshProUGUI? courseTabLabel;
        [SerializeField] private List<TierPill> tierPills = new List<TierPill>();

        [Header("Cards List")]
        [SerializeField] private ScrollRect? cardsScrollRect;
        [SerializeField] private RectTransform? cardsContent;
        [SerializeField] private MissionCardController? cardPrefab;

        [Header("Daily")]
        [SerializeField] private MissionCardController? dailyCard;

        [Header("Rankings")]
        [SerializeField] private Button? rankingsButton;

        private readonly List<MissionCardController> _cards = new List<MissionCardController>();

        /// <summary>
        /// The tier tab in view. Defaults to the FURTHEST UNLOCKED tier and PERSISTS across
        /// navigation round-trips (§C2) — a player working through Legend should not be dropped
        /// back into Beginner every time they come back from a round.
        /// </summary>
        private static string _activeTier = "";

        /// <summary>Where PLAY came from, so BACK returns there rather than always to Home.</summary>
        private static ScreenId _openedFrom = ScreenId.Home;

        public static void OpenFrom(ScreenId from)
        {
            _openedFrom = from;
            ScreenManager.Instance?.ShowScreen(ScreenId.MissionSelection);
        }

        private MissionProgressionService P => MissionProgressionService.Instance;

        private void Awake()
        {
            if (rankingsButton != null) rankingsButton.onClick.AddListener(OnRankingsClicked);
        }

        private void OnEnable()
        {
            MissionCatalog.EnsureLoaded();
            if (string.IsNullOrEmpty(_activeTier)) _activeTier = FurthestUnlockedTier();
            BuildTierPillListeners();
            RebuildCards();
            RefreshDaily();
        }

        private void OnRankingsClicked()
        {
            // v1: the same "coming soon" the Hole Selection leaderboard button opens. Mission
            // leaderboards are explicitly out of scope.
            var ctrl = FindObjectOfType<Golfin.UI.Rankings.RankingsScreenController>();
            if (ctrl != null) ctrl.OpenFrom(ScreenId.MissionSelection);
            else ScreenManager.Instance?.ShowScreen(ScreenId.Leaderboard);
        }

        /// <summary>BACK goes where the player came from (§C2).</summary>
        public void OnBackClicked() => ScreenManager.Instance?.ShowScreen(_openedFrom);

        // ── Tier tabs ───────────────────────────────────────────────────────────

        private string FurthestUnlockedTier()
        {
            string best = "";
            foreach (var t in MissionCatalog.Tiers)
                if (P.IsTierUnlocked(t.Tier)) best = t.Tier;
            return best;
        }

        private void BuildTierPillListeners()
        {
            foreach (var p in tierPills)
            {
                if (p?.button == null) continue;
                p.button.onClick.RemoveAllListeners();
                bool unlocked = P.IsTierUnlocked(p.tier);
                if (unlocked)
                {
                    var captured = p;
                    p.button.onClick.AddListener(() => OnTierPillClicked(captured));
                }
                p.button.interactable = unlocked;
            }
            UpdateTierPills();
        }

        private void OnTierPillClicked(TierPill pill)
        {
            if (pill == null || !P.IsTierUnlocked(pill.tier)) return;
            _activeTier = pill.tier;
            UpdateTierPills();
            RebuildCards();
        }

        /// <summary>
        /// Label, count and lock state per tab. The count is `cleared/size` from the catalog;
        /// a LOCKED tier shows `0/size` and a padlock, because telling a player how many of a
        /// tier they have cleared before they can reach it is noise.
        /// </summary>
        private void UpdateTierPills()
        {
            foreach (var p in tierPills)
            {
                if (p?.label == null) continue;
                bool unlocked = P.IsTierUnlocked(p.tier);
                int size = SizeOf(p.tier);
                int cleared = unlocked ? P.ClearedInTier(p.tier) : 0;

                p.label.text = $"{LocalizationManager.Get("MISSION_TIER_" + p.tier.ToUpperInvariant())} {cleared}/{size}";
                if (p.lockIcon != null) p.lockIcon.SetActive(!unlocked);

                // Same convention as the Hole Selection pills: active keeps the prefab's gold,
                // everything else is restyled silver.
                if (!unlocked || p.tier != _activeTier) TextGradients.ApplySilver(p.label);
            }

            if (courseTabLabel != null)
            {
                int cleared = 0, total = 0;
                foreach (var m in MissionCatalog.All) { total++; if (P.HasCleared(m.Id)) cleared++; }
                courseTabLabel.text = $"{LocalizationManager.Get("MISSION_COURSE_LOMOND")} {cleared}/{total}";
            }
        }

        private static int SizeOf(string tier)
        {
            foreach (var t in MissionCatalog.Tiers) if (t.Tier == tier) return t.MissionsInTier;
            return 10;
        }

        // ── Cards ───────────────────────────────────────────────────────────────

        private void RebuildCards()
        {
            if (cardsContent == null || cardPrefab == null)
            {
                Debug.LogError("[MissionSelection] cardsContent / cardPrefab not wired — screen will be empty.");
                return;
            }

            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardTapped -= HandleCardTapped;
                c.OnActionButtonClicked -= HandleActionClicked;
            }
            foreach (Transform child in cardsContent) Destroy(child.gameObject);
            _cards.Clear();

            var inTier = new List<MissionDefinition>();
            foreach (var m in MissionCatalog.All)
                if (m.Tier == _activeTier) inTier.Add(m);

            // §C2's order: cleared (most recent first) … NEXT … locked.
            var cleared = new List<MissionDefinition>();
            var open = new List<MissionDefinition>();
            var locked = new List<MissionDefinition>();
            foreach (var m in inTier)
            {
                if (P.HasCleared(m.Id)) cleared.Add(m);
                else if (P.IsUnlocked(m)) open.Add(m);
                else locked.Add(m);
            }
            cleared.Reverse();   // most recently cleared reads first

            MissionCardController? nextCard = null;
            foreach (var list in new[] { cleared, open, locked })
            {
                foreach (var m in list)
                {
                    var card = Instantiate(cardPrefab, cardsContent);

                    bool isCleared = P.HasCleared(m.Id);
                    bool isUnlocked = P.IsUnlocked(m);
                    var mode = isCleared ? MissionCardMode.Replay : MissionCardMode.Play;
                    var state = !isUnlocked ? MissionCardState.Locked : MissionCardState.Collapsed;

                    MissionCatalog.Warnings.TryGetValue(m.Id, out string warning);
                    card.Bind(m, mode, state, warning ?? "");
                    PlaceStartMarker(card, m);

                    card.OnCardTapped += HandleCardTapped;
                    card.OnActionButtonClicked += HandleActionClicked;
                    _cards.Add(card);

                    if (nextCard == null && !isCleared && isUnlocked) nextCard = card;
                }
            }

            // NEXT is expanded by default and scrolled to (§C2). After the layout settles —
            // expanding before the content rect has a height scrolls to the wrong place.
            if (nextCard != null) StartCoroutine(ExpandNextAfterLayout(nextCard));
        }

        private IEnumerator ExpandNextAfterLayout(MissionCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            SetExpanded(card);
            yield return null;
            Canvas.ForceUpdateCanvases();
            ScrollTo(card);
        }

        private void HandleCardTapped(MissionCardController card)
        {
            if (card == null || card.State == MissionCardState.Locked) return;
            if (card.State == MissionCardState.Expanded)
            {
                card.SetState(MissionCardState.Collapsed);
                return;
            }
            SetExpanded(card);
            StartCoroutine(ScrollToNextFrame(card));
        }

        /// <summary>The single-expanded invariant lives HERE, not on the card — a card cannot
        /// know what its siblings are doing.</summary>
        private void SetExpanded(MissionCardController card)
        {
            foreach (var c in _cards)
            {
                if (c == null) continue;
                if (c == card) c.SetState(MissionCardState.Expanded);
                else if (c.State == MissionCardState.Expanded) c.SetState(MissionCardState.Collapsed);
            }
        }

        private IEnumerator ScrollToNextFrame(MissionCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            ScrollTo(card);
        }

        private void ScrollTo(MissionCardController card)
        {
            if (cardsScrollRect == null || cardsContent == null || card == null || card.rootRect == null) return;
            float contentH = cardsContent.rect.height;
            float viewH = cardsScrollRect.viewport != null ? cardsScrollRect.viewport.rect.height : 0f;
            if (contentH <= viewH) { cardsScrollRect.verticalNormalizedPosition = 1f; return; }

            float cardTop = -card.rootRect.anchoredPosition.y;
            float target = Mathf.Clamp01(1f - (cardTop / (contentH - viewH)));
            cardsScrollRect.verticalNormalizedPosition = target;
        }

        // ── PLAY ────────────────────────────────────────────────────────────────

        private void HandleActionClicked(MissionCardController card)
        {
            var m = card?.Mission;
            if (m == null) return;

            // §C3 — a card that could not assemble its bag never starts a round. The button is
            // already non-interactable; this is the second lock, because a disabled button is a
            // UI state and this is a correctness rule.
            if (!card!.IsPlayable)
            {
                Debug.LogWarning($"[MissionSelection] mission {m.Id} is not playable: " +
                                 $"{(MissionCatalog.Warnings.TryGetValue(m.Id, out var w) ? w : "unknown")}");
                return;
            }

            if (!MissionSession.Begin(m))
            {
                // Begin refuses an empty bag or an unbaked short start and changes nothing.
                Debug.LogError($"[MissionSelection] MissionSession refused mission {m.Id}.");
                return;
            }

            GameSession.SeedSession(m.HoleNumber, GameSession.SelectedCharacterId, GameSession.EquippedBagSlot);

            var loading = FindObjectOfType<LoadingScreenController>(includeInactive: true);
            if (loading != null) loading.PrepareForHoleLoad(m.HoleNumber);

            var loader = GameplaySceneLoader.Instance;
            if (loader != null) loader.BeginGameplayLoad(m.HoleNumber);
            else Debug.LogError("[MissionSelection] GameplaySceneLoader not found.");
        }

        // ── The start marker ────────────────────────────────────────────────────

        /// <summary>
        /// The start marker on the hole thumbnail — DISABLED, and the reason is a missing input
        /// rather than a missing implementation.
        ///
        /// §C2 says to project the start point "using the same transform the MapView uses".
        /// There isn't one. `MapViewController` frames a live 3D CAMERA over the real hole
        /// (bounds-fit around ball + flag + landing zone); the card shows a PRE-RENDERED PNG
        /// from `HoleImages/`. Nothing anywhere records what world rectangle each of those 18
        /// PNGs covers, so there is no honest way to turn a world XZ into a pixel on them.
        ///
        /// The first attempt derived a box from the hole's five baked start areas. It produced
        /// a marker, and the marker was WRONG — those five points are a small cluster near the
        /// green, not the extent of the hole the PNG draws, so every start projected to a
        /// corner. A marker in the wrong place is worse than no marker: it tells the player
        /// something false about where they will tee off.
        ///
        /// So the start is conveyed in WORDS on the card (`START_AREA_*`, e.g. "Greenside
        /// bunker"), which is exact, and the marker waits for a per-hole thumbnail calibration
        /// — the world rect each PNG covers — that has to be authored once alongside the art.
        /// </summary>
        private void PlaceStartMarker(MissionCardController card, MissionDefinition m)
        {
            card.HideStartMarker();
        }

        // ── Daily ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The daily card. v1 renders it from the LOCAL bundled tables so it is never blank;
        /// the server's copy replaces it when `GET /missions/daily` answers, and the CLAIM
        /// always goes to the server — the client never pays itself (§C2).
        /// </summary>
        private void RefreshDaily()
        {
            if (dailyCard == null) return;
            System.DateTime utc = System.DateTime.UtcNow;
            System.TimeSpan untilReset = utc.Date.AddDays(1) - utc;
            dailyCard.SetDailyStatus(untilReset, streak: 0, claimed: false);
        }
    }
}
