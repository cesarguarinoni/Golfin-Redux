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

        /// <summary>Daily card + campaign list, measured once while the daily card is collapsed.</summary>
        private float _columnTotal;

        /// <summary>Floor for the campaign viewport, so an expanded daily can never erase the list.</summary>
        private const float MinCardsViewport = 420f;

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
            // MissionLoadoutResolver installs itself from [RuntimeInitializeOnLoadMethod], which
            // does NOT re-run after a mid-session domain reload — statics come back null and the
            // attribute has already fired for this play session. MissionCatalog then resolves
            // every loadout to zero clubs, and the daily card, which is dropped when its bag is
            // empty, silently disappears. Re-installing here is idempotent (a plain assignment)
            // and costs nothing. Observed 2026-08-29: 'SUP_IRONS resolved to no clubs — no
            // ClubResolver is installed'.
            MissionLoadoutResolver.Install();
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
                if (isActiveAndEnabled) StartCoroutine(RebalanceNextFrame());
                return;
            }
            SetExpanded(card);
            // ScrollTo measures against cardsContent; the daily card is not parented to it, so
            // scrolling "to" it would move the campaign list to a meaningless position.
            if (_cards.Contains(card)) StartCoroutine(ScrollToNextFrame(card));
        }

        /// <summary>
        /// The daily card and the campaign list share one fixed column: `Content` is a plain
        /// VerticalLayoutGroup, not a scroll view, so anything the daily card grows by is pushed
        /// straight off the bottom of the screen. Expanding it more than doubles its height
        /// (374 -> ~878), which would shove the whole list past the nav bar.
        ///
        /// So the column total is held constant and the campaign list absorbs the difference.
        /// The list IS a scroll view, so it loses viewport, not content.
        ///
        /// `Content`'s group has childControlHeight = false, which makes LayoutElement inert
        /// there — sizeDelta is the only lever that moves anything.
        /// </summary>
        private void RebalanceColumn()
        {
            if (dailyCard == null || cardsScrollRect == null) return;
            var daily = dailyCard.rootRect;
            if (daily == null) return;
            var column = daily.parent as RectTransform;
            if (column == null) return;

            // The ScrollRect lives on CardsContainer/CardsScrollView, a GRANDchild of the column
            // -- resizing the ScrollRect's own transform moves the viewport inside the container
            // and leaves the container itself the same height, which is exactly the no-op this
            // first shipped as. Walk up to whichever ancestor is the column's own child.
            var cards = cardsScrollRect.transform as RectTransform;
            while (cards != null && cards.parent != column) cards = cards.parent as RectTransform;
            if (cards == null) return;

            // An INACTIVE daily card occupies no space in the column, but its RectTransform still
            // reports whatever height it last had -- so it has to be read as zero here, and it can
            // never be the thing the budget is measured from.
            bool dailyShowing = dailyCard.gameObject.activeInHierarchy;
            float dailyHeight = dailyShowing ? daily.rect.height : 0f;

            if (_columnTotal <= 0f)
            {
                // The budget is only readable from a daily card that is on screen AND collapsed.
                // Banking it while expanded would freeze the expanded height as the budget;
                // banking it while inactive would bank a stale rect nothing is laying out.
                if (!dailyShowing || dailyCard.State == MissionCardState.Expanded) return;
                _columnTotal = daily.rect.height + cards.rect.height;
                if (_columnTotal <= 0f) return;
            }

            float target = Mathf.Max(MinCardsViewport, _columnTotal - dailyHeight);
            if (!Mathf.Approximately(cards.sizeDelta.y, target))
                cards.sizeDelta = new Vector2(cards.sizeDelta.x, target);
        }

        private IEnumerator RebalanceNextFrame()
        {
            // The card's own height comes from a ContentSizeFitter, which has not run yet on the
            // frame the state changed — measuring now would rebalance against the OLD height.
            yield return null;
            Canvas.ForceUpdateCanvases();
            RebalanceColumn();
        }

        /// <summary>The single-expanded invariant lives HERE, not on the card — a card cannot
        /// know what its siblings are doing.</summary>
        private void SetExpanded(MissionCardController card)
        {
            foreach (var c in AllCards())
            {
                if (c == null) continue;
                if (c == card) c.SetState(MissionCardState.Expanded);
                else if (c.State == MissionCardState.Expanded) c.SetState(MissionCardState.Collapsed);
            }
            if (isActiveAndEnabled) StartCoroutine(RebalanceNextFrame());
        }

        /// <summary>
        /// Every card the invariant governs: the instantiated campaign rows PLUS the daily card,
        /// which lives outside <c>_cards</c> because it is a scene object rather than a row.
        /// Naming it here is what stops "expand a campaign card" from leaving the daily one open
        /// beside it — and vice versa.
        /// </summary>
        private IEnumerable<MissionCardController?> AllCards()
        {
            foreach (var c in _cards) yield return c;
            if (dailyCard != null) yield return dailyCard;
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
        /// Put a marker on the hole thumbnail where the mission starts.
        ///
        /// Only for starts that are NOT the tee: a tee start needs no marker, the words already
        /// name which tee, and every hole draws its tee in the same place anyway.
        ///
        /// The thumbnails are stylised illustrations rather than renders, so the mapping is a
        /// per-hole fit rather than maths — see <see cref="HoleMapCalibration"/> for how it is
        /// derived and, more importantly, for how accurate it is NOT. Holes whose fit does not
        /// survive the bunker check return null and keep the words-only treatment, which is
        /// exact.
        /// </summary>
        private void PlaceStartMarker(MissionCardController card, MissionDefinition m)
        {
            card.HideStartMarker();
            if (m == null || m.StartWorld == null) return;
            if (string.Equals(m.StartKind, "tee", System.StringComparison.OrdinalIgnoreCase)) return;

            Vector2? uv = HoleMapCalibration.Normalised(m.HoleNumber, m.StartWorld.Value);
            if (uv != null) card.SetStartMarkerNormalised(uv.Value);
        }

        // ── Daily ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The daily card.
        ///
        /// ⚠️ IT STARTS HIDDEN AND ONLY APPEARS WITH A REAL RECIPE. A daily card with nothing
        /// in it is the "dead card" the standing invariant exists to prevent — and it is worse
        /// than the campaign version, because a player who taps a blank daily has no idea what
        /// they are being asked to do. So: hidden, fetch, and shown only if the server answers.
        ///
        /// The recipe is composed SERVER-SIDE (`services/daily_mission.py`) and frozen for the
        /// UTC day. §C2 also asks for an OFFLINE fallback that generates the same recipe locally
        /// from the bundled tables — deliberately NOT done here, and flagged: it needs a C#
        /// port of the generator, and a second implementation of a deterministic draw is the
        /// one thing that would break the property the whole design rests on (server, admin
        /// preview and client agreeing about what a date produces). Offline, the card is absent
        /// rather than wrong.
        ///
        /// The CLAIM always goes to the server regardless — the client never pays itself.
        /// </summary>
        private void RefreshDaily()
        {
            if (dailyCard == null) return;
            dailyCard.gameObject.SetActive(false);
            StartCoroutine(FetchDailyRoutine());
        }

        private IEnumerator FetchDailyRoutine()
        {
            yield return Golfin.Economy.MissionsClient.Instance.FetchDailyRoutine(r =>
            {
                if (!r.Success || r.Data?.Recipe == null)
                {
                    Debug.Log($"[MissionSelection] no daily today ({r.ErrorMessage ?? "no recipe"}) — card stays hidden.");
                    return;
                }
                var def = BuildDailyDefinition(r.Data);
                if (def == null || def.ClubIds.Count == 0)
                {
                    Debug.LogWarning("[MissionSelection] the daily recipe could not be resolved — card stays hidden.");
                    return;
                }

                dailyCard!.gameObject.SetActive(true);

                // The daily card is a SERIALIZED SCENE OBJECT, not one of the rows RebuildCards
                // instantiates — so it never passed through the subscribe site there, and its two
                // events had no listeners at all. The card rendered correctly and did nothing:
                // tapping it could not expand it and its PLAY button could not start the round.
                // Subscribe here, where the card is bound, so a real recipe is always wired.
                // `-=` first because OnEnable calls RefreshDaily on every return to the screen;
                // without it a second visit would double-subscribe and one tap would expand and
                // immediately collapse again.
                dailyCard.OnCardTapped -= HandleCardTapped;
                dailyCard.OnCardTapped += HandleCardTapped;
                dailyCard.OnActionButtonClicked -= HandleActionClicked;
                dailyCard.OnActionButtonClicked += HandleActionClicked;

                dailyCard.Bind(def, MissionCardMode.Daily, MissionCardState.Collapsed);

                // Bank the collapsed column budget now, while it is still readable.
                if (isActiveAndEnabled) StartCoroutine(RebalanceNextFrame());

                System.DateTime utc = System.DateTime.UtcNow;
                dailyCard.SetDailyStatus(utc.Date.AddDays(1) - utc, r.Data.Streak, r.Data.Claimed);
            });
        }

        /// <summary>
        /// Turn a server recipe into the same <see cref="MissionDefinition"/> a campaign row
        /// produces, so everything downstream — the card, MissionSession, the evaluator —
        /// cannot tell the two apart. That is the point: the daily is not a second kind of
        /// mission, it is a mission whose row was composed this morning.
        /// </summary>
        private MissionDefinition? BuildDailyDefinition(Golfin.Economy.DailyMissionResult daily)
        {
            var r = daily.Recipe;
            var def = MissionCatalog.BuildFromRecipe(
                id: $"daily:{daily.Date}",
                holeNumber: r.HoleId,
                par: r.Par,
                startAreaId: r.StartAreaId,
                windPresetId: r.WindPresetId,
                loadoutId: r.LoadoutId,
                pinIndex: r.PinIndex,
                staminaDrain: r.StaminaDrain);
            if (def == null) return null;

            if (r.Goals != null)
                foreach (var g in r.Goals)
                {
                    var type = MissionGoal.Parse(g.Type);
                    if (type != MissionGoalType.None) def.Goals.Add(new MissionGoal(type, g.Param ?? ""));
                }

            def.DoubleRp = r.Modifier == "DOUBLE_RP";
            // DOUBLE_RP is the one modifier that changes the payout, so the card must show the
            // doubled number — the server applies it from the stored recipe, not from us.
            if (def.DoubleRp) def.FirstClearRP *= 2;
            def.LowStaminaStart = r.Modifier == "LOW_STAMINA_START";
            def.DifficultyScore = r.DifficultyScore;
            return def;
        }
    }
}
