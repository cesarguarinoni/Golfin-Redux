using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage 0–1 controller for the Tournament Selection screen (T7, Figma 13386:1758).
    ///
    /// Scaffold only — no backend. Six static showcase cards (one per TournamentSelectionCard.CardState)
    /// are instantiated from _cardPrefab and populated with literal static data.
    /// Stage 2 replaces static data with ITournamentBackend.GetTournaments() (blocked on T1→T4).
    ///
    /// Navigation:
    ///   SIGN UP / CONTINUE CTA → TournamentHoleSelection (card tap)
    ///   LEADERBOARD CTA         → TournamentLeaderboard
    ///   Filter tabs             → visual only (Stage 1), filter logic in Stage 2
    /// </summary>
    public class TournamentSelectionScreenController : MonoBehaviour
    {
        [Header("Cards List")]
        [SerializeField] private ScrollRect _cardsScrollRect;
        [SerializeField] private RectTransform _cardsContent;
        [SerializeField] private TournamentSelectionCard _cardPrefab;

        [Header("Filter Tabs")]
        [SerializeField] private Button _tabAll;
        [SerializeField] private Button _tabOpen;
        [SerializeField] private Button _tabPlaying;
        [SerializeField] private Button _tabClosed;

        [Header("Tab Underline Images (active = gold, inactive = transparent)")]
        [SerializeField] private Image _tabAllUnderline;
        [SerializeField] private Image _tabOpenUnderline;
        [SerializeField] private Image _tabPlayingUnderline;
        [SerializeField] private Image _tabClosedUnderline;

        [Header("Navigation")]
        [SerializeField] private ScreenId _holeSelectionTarget = ScreenId.TournamentHoleSelection;
        [SerializeField] private ScreenId _leaderboardTarget   = ScreenId.TournamentLeaderboard;

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color TabActiveColor   = new Color32(0xFF, 0xE4, 0x8B, 255); // #ffe48b gold
        private static readonly Color TabInactiveColor = new Color32(0xC7, 0xD6, 0xEB, 255); // #c7d6eb grey-blue
        private static readonly Color UnderlineActive  = new Color32(0xFF, 0xE4, 0x8B, 255);
        private static readonly Color UnderlineHidden  = new Color32(0xFF, 0xFF, 0xFF, 0);

        private enum TabId { All, Open, Playing, Closed }
        private TabId _activeTab = TabId.All;

        // ── Static card data (Stage 0–1, replaced by backend in Stage 2) ─────
        private struct StaticCardData
        {
            public TournamentSelectionCard.CardState State;
            public string Name;
            public string ClubLine;
            public string DateLine;
            public bool   IsFreeEntry;
            public int    EntryRp;
            public int    RewardRp;
            public string CtaText;
        }

        private static readonly StaticCardData[] StaticCards = new StaticCardData[]
        {
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.EnteredActive,
                Name = "Kasumigaseki Open",
                ClubLine = "Kasumigaseki CC - 18 Holes",
                DateLine = "Round in progress — Hole 7 of 18",
                IsFreeEntry = false, EntryRp = 0, RewardRp = 15000,
                CtaText = "CONTINUE"
            },
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.EnteredFinished,
                Name = "Hirono Invitational",
                ClubLine = "Hirono Golf Club - 18 Holes",
                DateLine = "Round complete",
                IsFreeEntry = false, EntryRp = 0, RewardRp = 18000,
                CtaText = "LEADERBOARD"
            },
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.Open,
                Name = "Lomond Championship",
                ClubLine = "Loch Lomond GC - 18 Holes",
                DateLine = "Jun 20 — Jun 27",
                IsFreeEntry = true, EntryRp = 0, RewardRp = 5000,
                CtaText = "SIGN UP"
            },
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.Ending,
                Name = "Gotemba Masters",
                ClubLine = "Gotemba GC - 18 Holes",
                DateLine = "Ends in 3d 04h",
                IsFreeEntry = false, EntryRp = 500, RewardRp = 12000,
                CtaText = "SIGN UP"
            },
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.Upcoming,
                Name = "Kisarazu Cup",
                ClubLine = "Kisarazu CC - 18 Holes",
                DateLine = "Starts in 8d",
                IsFreeEntry = true, EntryRp = 0, RewardRp = 8000,
                CtaText = "UPCOMING"
            },
            new StaticCardData
            {
                State = TournamentSelectionCard.CardState.Ended,
                Name = "Kawana Fuji Open",
                ClubLine = "Kawana Hotel GC - 18 Holes",
                DateLine = "Ended Jun 13",
                IsFreeEntry = true, EntryRp = 0, RewardRp = 20000,
                CtaText = "LEADERBOARD"
            },
        };

        private void Awake()
        {
            WireTab(_tabAll,     TabId.All);
            WireTab(_tabOpen,    TabId.Open);
            WireTab(_tabPlaying, TabId.Playing);
            WireTab(_tabClosed,  TabId.Closed);
        }

        private void WireTab(Button btn, TabId tab)
        {
            if (btn == null) return;
            btn.onClick.AddListener(() => SelectTab(tab));
        }

        private void OnEnable()
        {
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
        }

        private void OnDisable()
        {
            ClearCards();
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            RebuildCards();
            SelectTab(_activeTab);
        }

        private void RebuildCards()
        {
            ClearCards();

            if (_cardPrefab == null)
            {
                Debug.LogError("[TournamentSelectionScreen] _cardPrefab not wired.");
                return;
            }

            if (_cardsContent == null)
            {
                Debug.LogError("[TournamentSelectionScreen] _cardsContent not wired.");
                return;
            }

            foreach (var data in StaticCards)
            {
                var card = Object.Instantiate(_cardPrefab, _cardsContent);
                card.BindStatic(
                    data.State,
                    data.Name,
                    data.ClubLine,
                    data.DateLine,
                    data.IsFreeEntry,
                    data.EntryRp,
                    data.RewardRp,
                    data.CtaText);

                card.OnCtaClicked += HandleCtaClicked;
            }

            if (_cardsScrollRect != null)
                _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ClearCards()
        {
            if (_cardsContent == null) return;
            foreach (Transform child in _cardsContent)
            {
                var card = child.GetComponent<TournamentSelectionCard>();
                if (card != null) card.OnCtaClicked -= HandleCtaClicked;
                Object.Destroy(child.gameObject);
            }
        }

        private void HandleCtaClicked(TournamentSelectionCard card)
        {
            if (card == null) return;

            switch (card.State)
            {
                case TournamentSelectionCard.CardState.Open:
                case TournamentSelectionCard.CardState.Ending:
                case TournamentSelectionCard.CardState.EnteredActive:
                    ScreenManager.Instance?.ShowScreen(_holeSelectionTarget);
                    break;

                case TournamentSelectionCard.CardState.EnteredFinished:
                case TournamentSelectionCard.CardState.Ended:
                    ScreenManager.Instance?.ShowScreen(_leaderboardTarget);
                    break;

                case TournamentSelectionCard.CardState.Upcoming:
                    Debug.Log("[TournamentSelectionScreen] UPCOMING — CTA disabled (Stage 2 will handle Notify).");
                    break;

                default:
                    Debug.LogWarning($"[TournamentSelectionScreen] Unhandled card state: {card.State}");
                    break;
            }
        }

        private void SelectTab(TabId tab)
        {
            _activeTab = tab;
            RefreshTabVisuals();
            // Stage 2: filter card list based on tab selection
        }

        private void RefreshTabVisuals()
        {
            SetTabActive(_tabAll,     _tabAllUnderline,     _activeTab == TabId.All);
            SetTabActive(_tabOpen,    _tabOpenUnderline,    _activeTab == TabId.Open);
            SetTabActive(_tabPlaying, _tabPlayingUnderline, _activeTab == TabId.Playing);
            SetTabActive(_tabClosed,  _tabClosedUnderline,  _activeTab == TabId.Closed);
        }

        private static void SetTabActive(Button btn, Image underline, bool active)
        {
            if (btn != null)
            {
                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.color = active ? TabActiveColor : TabInactiveColor;
            }
            if (underline != null) underline.color = active ? UnderlineActive : UnderlineHidden;
        }
    }
}
