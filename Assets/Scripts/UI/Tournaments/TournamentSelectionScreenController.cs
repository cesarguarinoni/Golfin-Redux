using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.Tournaments;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Stage 2 controller for the Tournament Selection screen (T7, Figma 13386:1758).
    /// Binds live data from TournamentService.Instance.Backend (replaces Stage 0-1 static cards).
    ///
    /// iter-2 fixes (CESAR_REJECTION 2026-06-27):
    ///   - Names + venues resolved via LocalizationManager.Get(def.NameKey) / tourn.venue.*
    ///   - Sponsors bound from def.SponsorKey ("{SPONSOR} PRESENTS"), not hardcoded
    ///   - BuildDateLine adds date-range prefix + middot to OPEN/ENDING/UPCOMING
    ///   - LIVE "Hole {N}" uses max(1, count) so it never shows "Hole 0"
    ///   - EnteredFinished shows "Round finished · Hole 18 of 18"
    /// </summary>
    public class TournamentSelectionScreenController : MonoBehaviour
    {
        [Header("Cards List")]
        [SerializeField] private ScrollRect _cardsScrollRect;
        [SerializeField] private RectTransform _cardsContent;
        [SerializeField] private TournamentSelectionCard _cardPrefab;

        [Tooltip("Optional per-tournament bundled art override, keyed by def.Id. Overrides the " +
                 "Resources/TournamentImages/{courseId} convention; the server's banner_url still wins.")]
        [SerializeField] private TournamentImageEntry[] _courseImageMap;

        [Tooltip("Shown when a tournament has no art at any layer. Leave the image hidden if unwired.")]
        [SerializeField] private Sprite _placeholderImage;

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

        [Header("Signup Modal (T6)")]
        [Tooltip("TournamentSignupModalController on the ShellScene canvas. Opened for Open/Ending CTA.")]
        [SerializeField] private TournamentSignupModalController _signupModal;

        // ── Colours ───────────────────────────────────────────────────────────
        private static readonly Color TabActiveColor   = new Color32(0xFF, 0xE4, 0x8B, 255);
        private static readonly Color TabInactiveColor = new Color32(0xC7, 0xD6, 0xEB, 255);
        private static readonly Color UnderlineActive  = new Color32(0xFF, 0xE4, 0x8B, 255);
        private static readonly Color UnderlineHidden  = new Color32(0xFF, 0xFF, 0xFF, 0);

        private enum TabId { All, Open, Playing, Closed }
        private TabId _activeTab = TabId.All;

        private readonly List<TournamentSelectionCard> _cards = new List<TournamentSelectionCard>();

        // ── Course image lookup ────────────────────────────────────────────────
        [Serializable]
        public struct TournamentImageEntry
        {
            public string Id;
            public Sprite Sprite;
        }

        // ── Awake / Enable / Disable ──────────────────────────────────────────

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
            // A server fetch that lands while the player is already on this screen repaints it;
            // entering the screen later is already covered by the rebuild below.
            TournamentService.OnScheduleChanged += HandleScheduleChanged;

            // Settings is an OVERLAY (SettingsController, not a ScreenManager screen) and the
            // top-bar gear that opens it is visible on this screen — so the player can switch
            // language with T7 still active and this OnEnable never re-fires. Every card string
            // is language-dependent (the name ladder's JP rung, the venue line, the date line),
            // so the cards have to be rebuilt in place.
            LocalizationManager.OnLanguageChanged += HandleScheduleChanged;

            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
        }

        private void OnDisable()
        {
            TournamentService.OnScheduleChanged -= HandleScheduleChanged;
            LocalizationManager.OnLanguageChanged -= HandleScheduleChanged;
            ClearCards();
        }

        /// <summary>Repaint in place — the schedule changed under us, or the language did.</summary>
        private void HandleScheduleChanged()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            RebuildCards();
            SelectTab(_activeTab);
        }

        // ── Live RebuildCards ─────────────────────────────────────────────────

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

            if (TournamentService.Instance == null)
            {
                Debug.LogWarning("[TournamentSelectionScreen] TournamentService not yet ready; skipping rebuild.");
                return;
            }

            var backend = TournamentService.Instance.Backend;
            var liveBk  = backend as LocalTournamentBackend;
            var defs    = backend.GetTournaments();
            DateTime now = DateTime.UtcNow;

            for (int i = 0; i < defs.Count; i++)
            {
                var def   = defs[i];
                EntryState entry = backend.GetMyEntry(def.Id);
                TournamentState state = liveBk != null
                    ? liveBk.DeriveState(def, now)
                    : DeriveStateFallback(def, entry, now);

                bool nowPastEnd    = now >= def.EndUtc;
                EntryStatus entryStatus = entry != null ? entry.Status : EntryStatus.NotEntered;
                var cardState = MapCardState(state, entryStatus, nowPastEnd);
                string ctaText = CardCtaText(cardState);

                // ── Field derivations (iter-2) ────────────────────────────────
                // Name: localize(NameKey) → Title → Id. A dashboard-created tournament has no
                // localization key in this build, so without the ladder its raw key would render.
                string name = TournamentDisplayName.Resolve(def);

                // Venue line: localized via tourn.venue.<clubId>; fallback if missing
                string venueLocKey = "tourn.venue." + def.ClubId;
                string clubLine    = LocalizationManager.Get(venueLocKey);
                if (string.IsNullOrEmpty(clubLine) || clubLine == venueLocKey)
                    clubLine = def.ClubId + " · " + def.HoleSet.Count + " Holes";

                string dateLine = BuildDateLine(cardState, def, entry, now);
                bool   isFree   = def.EntryFeeRP == 0;
                int    entryRp  = (int)def.EntryFeeRP;
                int    rewardRp = (int)TournamentService.Instance.GetTopPrizeRP(def.Id);

                // Sponsor: "{SPONSOR} PRESENTS" from CSV, not hardcoded "GOLFIN PRESENTS"
                string sponsorLine = string.IsNullOrEmpty(def.SponsorKey)
                    ? LocalizationManager.Get("TOURN_GOLFIN_PRESENTS")
                    : def.SponsorKey.ToUpperInvariant() + " PRESENTS";

                var card = UnityEngine.Object.Instantiate(_cardPrefab, _cardsContent);
                card.BindStatic(
                    cardState,
                    name,
                    clubLine,
                    dateLine,
                    isFree,
                    entryRp,
                    rewardRp,
                    ctaText,
                    def.Id,
                    sponsorLine);

                ApplyCardArt(card, def);

                card.OnCtaClicked += HandleCtaClicked;
                _cards.Add(card);
            }

            if (_cardsScrollRect != null)
                _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        // ── MapCardState ──────────────────────────────────────────────────────
        public static TournamentSelectionCard.CardState MapCardState(
            TournamentState state,
            EntryStatus entryStatus,
            bool nowPastEnd)
        {
            var mapped = TournamentCardStateMapper.Map(state, entryStatus, nowPastEnd);
            switch (mapped)
            {
                case TournamentCardState.Upcoming:        return TournamentSelectionCard.CardState.Upcoming;
                case TournamentCardState.EnteredActive:   return TournamentSelectionCard.CardState.EnteredActive;
                case TournamentCardState.EnteredFinished: return TournamentSelectionCard.CardState.EnteredFinished;
                case TournamentCardState.Ending:          return TournamentSelectionCard.CardState.Ending;
                case TournamentCardState.Open:            return TournamentSelectionCard.CardState.Open;
                case TournamentCardState.Ended:           return TournamentSelectionCard.CardState.Ended;
                default:                                  return TournamentSelectionCard.CardState.Ended;
            }
        }

        private static string CardCtaText(TournamentSelectionCard.CardState cs)
        {
            switch (cs)
            {
                case TournamentSelectionCard.CardState.Upcoming:        return "UPCOMING";
                case TournamentSelectionCard.CardState.EnteredActive:   return "CONTINUE";
                case TournamentSelectionCard.CardState.EnteredFinished: return "LEADERBOARD";
                case TournamentSelectionCard.CardState.Ending:          return "SIGN UP";
                case TournamentSelectionCard.CardState.Open:            return "SIGN UP";
                case TournamentSelectionCard.CardState.Ended:           return "LEADERBOARD";
                default:                                                return "LEADERBOARD";
            }
        }

        /// <summary>
        /// iter-2: matches CESAR_REJECTION.md per-state table.
        /// Separator = middot (·). Date range prefix on OPEN/ENDING/UPCOMING.
        /// LIVE uses "·" and real hole count (never 0).
        /// </summary>
        private static string BuildDateLine(
            TournamentSelectionCard.CardState cardState,
            TournamentDefinition def,
            EntryState entry,
            DateTime now)
        {
            // "Jun 24 – Jun 27" (en-dash, no year)
            string dateRange = def.StartUtc.ToString("MMM d") + " – " + def.EndUtc.ToString("MMM d");

            switch (cardState)
            {
                case TournamentSelectionCard.CardState.Upcoming:
                {
                    TimeSpan diff = def.StartUtc - now;
                    string countdown;
                    if (diff.TotalDays >= 1)
                        countdown = string.Format("Starts in {0}d", (int)diff.TotalDays);
                    else
                        countdown = string.Format("Starts in {0}h {1:D2}m", (int)diff.TotalHours, diff.Minutes);
                    // "Jul 02 – Jul 05 · Starts in 8d"
                    return dateRange + " · " + countdown;
                }

                case TournamentSelectionCard.CardState.EnteredActive:
                {
                    // Cesar (iter-3): keep "Round in progress" + time remaining, separated by "-".
                    // No hole progression on the LIVE card.
                    TimeSpan diff = def.EndUtc - now;
                    string countdown;
                    if (diff.TotalDays >= 1)
                        countdown = string.Format("Ends in {0}d {1:D2}h", (int)diff.TotalDays, diff.Hours);
                    else
                        countdown = string.Format("Ends in {0:D2}h {1:D2}m", (int)diff.TotalHours, diff.Minutes);
                    // "Round in progress - Ends in 2d 04h"
                    return "Round in progress - " + countdown;
                }

                case TournamentSelectionCard.CardState.EnteredFinished:
                {
                    // "Round finished · Hole 18 of 18"
                    int totalHoles = def.HoleSet.Count;
                    return string.Format("Round finished · Hole {0} of {0}", totalHoles);
                }

                case TournamentSelectionCard.CardState.Ending:
                {
                    TimeSpan diff = def.EndUtc - now;
                    string countdown;
                    if (diff.TotalDays >= 1)
                        countdown = string.Format("Ends in {0}d {1:D2}h", (int)diff.TotalDays, diff.Hours);
                    else
                        countdown = string.Format("Ends in {0:D2}h {1:D2}m", (int)diff.TotalHours, diff.Minutes);
                    // "Jun 21 – Jun 25 · Ends in 06h 40m"
                    return dateRange + " · " + countdown;
                }

                case TournamentSelectionCard.CardState.Open:
                {
                    TimeSpan diff = def.EndUtc - now;
                    string countdown;
                    if (diff.TotalDays >= 1)
                        countdown = string.Format("Ends in {0}d {1:D2}h", (int)diff.TotalDays, diff.Hours);
                    else
                        countdown = string.Format("Ends in {0:D2}h {1:D2}m", (int)diff.TotalHours, diff.Minutes);
                    // "Jun 24 – Jun 27 · Ends in 3d 04h"
                    return dateRange + " · " + countdown;
                }

                case TournamentSelectionCard.CardState.Ended:
                    return string.Format("Ended {0}", def.EndUtc.ToString("MMM d"));

                default:
                    return dateRange;
            }
        }

        private static TournamentState DeriveStateFallback(
            TournamentDefinition def,
            EntryState entry,
            DateTime now)
        {
            if (now < def.StartUtc) return TournamentState.Upcoming;
            if (now >= def.EndUtc)  return entry != null ? TournamentState.Ended : TournamentState.Closed;
            bool ending = (def.EndUtc - now).TotalHours <= 1.0;
            if (ending) return TournamentState.Ending;
            bool inProgress = entry != null && entry.Status == EntryStatus.InProgress;
            return inProgress ? TournamentState.Playing : TournamentState.Open;
        }

        // ── Card artwork ──────────────────────────────────────────────────────
        //
        // Resolution order, first hit wins (SPEC §5.1):
        //   1. def.BannerUrl        — server art, downloaded + disk-cached, host-allowlisted
        //   2. _courseImageMap[id]  — optional bundled per-tournament override
        //   3. Resources/TournamentImages/{ClubId} — the shipped course photo
        //   4. _placeholderImage    — else the image is hidden
        //
        // ⚠️ The old positional fallback (_courseImages[csvIndex]) is DELETED. It was the bug, not a
        // safety net: now that the dashboard can reorder tournaments, indexing art by schedule
        // position silently reshuffles which photograph lands on which card. A card must never show
        // a DIFFERENT course's photo — showing none is strictly better.

        private static readonly Dictionary<string, Sprite> _bundledArtMemo =
            new Dictionary<string, Sprite>(StringComparer.Ordinal);

        private bool _warnedMissingPlaceholder;

        private void ApplyCardArt(TournamentSelectionCard card, TournamentDefinition def)
        {
            bool hasRemote = !string.IsNullOrEmpty(def.BannerUrl);

            // Paint the bundled layer FIRST and unconditionally, so a card is never an empty
            // rectangle while a download is in flight and never jumps layout on arrival.
            // `hasRemote` suppresses the no-art warning: a brand tournament on a course with no
            // bundled photo is not missing art, it is one frame away from it.
            card.SetCourseImage(ResolveBundledSprite(def, suppressMissingWarning: hasRemote));

            if (!hasRemote) return;

            var art = TournamentArtService.Instance;

            if (art.TryGet(def.BannerUrl, out Sprite remote) && remote != null)
            {
                card.SetCourseImage(remote);
                return;
            }

            art.Request(def.BannerUrl, sprite =>
            {
                // The card can be destroyed by a tab switch or a rebuild before the download lands.
                if (card == null || sprite == null) return;
                card.SetCourseImage(sprite);
            });
        }

        /// <summary>Layers 2–4: the art available with no network at all.</summary>
        private Sprite ResolveBundledSprite(TournamentDefinition def, bool suppressMissingWarning)
        {
            // 2. Inspector override — but only when it actually carries a sprite.
            //    The old code returned mapEntry.Sprite even when it was null, so an id-only map row
            //    BLANKED the card instead of falling through to the course photo.
            if (_courseImageMap != null)
            {
                foreach (var mapEntry in _courseImageMap)
                {
                    if (!string.Equals(mapEntry.Id, def.Id, StringComparison.Ordinal)) continue;
                    if (mapEntry.Sprite != null) return mapEntry.Sprite;
                    break;   // matched the id but has no sprite → fall through, do not blank the card
                }
            }

            // 3. Convention: Resources/TournamentImages/{courseId}. Memoised — Resources.Load per
            //    card per rebuild would re-hit the asset database on every tab switch.
            Sprite byCourse = LoadCourseSprite(def.ClubId);
            if (byCourse != null) return byCourse;

            // 4. Placeholder, or nothing.
            if (_placeholderImage == null && !_warnedMissingPlaceholder && !suppressMissingWarning)
            {
                _warnedMissingPlaceholder = true;
                Debug.LogWarning(
                    $"[TournamentSelectionScreen] No art for '{def.Id}' (courseId='{def.ClubId}') and " +
                    "_placeholderImage is unwired; the card image is hidden.");
            }
            return _placeholderImage;
        }

        private static Sprite LoadCourseSprite(string clubId)
        {
            if (string.IsNullOrEmpty(clubId)) return null;
            if (_bundledArtMemo.TryGetValue(clubId, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>("TournamentImages/" + clubId);
            _bundledArtMemo[clubId] = sprite;   // memoise misses too, so a bad id costs one lookup
            return sprite;
        }

        private void ClearCards()
        {
            _cards.Clear();
            if (_cardsContent == null) return;
            foreach (Transform child in _cardsContent)
            {
                var card = child.GetComponent<TournamentSelectionCard>();
                if (card != null) card.OnCtaClicked -= HandleCtaClicked;
                UnityEngine.Object.Destroy(child.gameObject);
            }
        }

        private void HandleCtaClicked(TournamentSelectionCard card)
        {
            if (card == null) return;

            if (TournamentService.Instance != null)
            {
                TournamentService.Instance.SelectedTournamentId = card.TournamentId;
                Debug.Log(string.Format("[TournamentSelectionScreen] SelectedTournamentId = {0}", card.TournamentId));
            }
            else
            {
                Debug.LogWarning("[TournamentSelectionScreen] TournamentService.Instance is null; SelectedTournamentId not set.");
            }

            switch (card.State)
            {
                // T6: Open/Ending → Signup modal (Register → HoleSelection from within modal).
                case TournamentSelectionCard.CardState.Open:
                case TournamentSelectionCard.CardState.Ending:
                    if (_signupModal != null)
                        _signupModal.Open(card.TournamentId);
                    else
                        ScreenManager.Instance?.ShowScreen(_holeSelectionTarget); // fallback if modal not wired
                    break;

                // EnteredActive → HoleSelection directly (already registered).
                case TournamentSelectionCard.CardState.EnteredActive:
                    ScreenManager.Instance?.ShowScreen(_holeSelectionTarget);
                    break;

                case TournamentSelectionCard.CardState.EnteredFinished:
                case TournamentSelectionCard.CardState.Ended:
                    ScreenManager.Instance?.ShowScreen(_leaderboardTarget);
                    break;

                case TournamentSelectionCard.CardState.Upcoming:
                    Debug.Log("[TournamentSelectionScreen] UPCOMING — CTA disabled.");
                    break;

                default:
                    Debug.LogWarning(string.Format("[TournamentSelectionScreen] Unhandled card state: {0}", card.State));
                    break;
            }
        }

        private void SelectTab(TabId tab)
        {
            _activeTab = tab;
            RefreshTabVisuals();
            ApplyFilter();
            if (_cardsScrollRect != null) _cardsScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ApplyFilter()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.gameObject.SetActive(Matches(card.State, _activeTab));
            }
        }

        private static bool Matches(TournamentSelectionCard.CardState state, TabId tab)
        {
            switch (tab)
            {
                case TabId.All:     return true;
                case TabId.Open:    return state == TournamentSelectionCard.CardState.Open
                                        || state == TournamentSelectionCard.CardState.Ending
                                        || state == TournamentSelectionCard.CardState.Upcoming;
                case TabId.Playing: return state == TournamentSelectionCard.CardState.EnteredActive
                                        || state == TournamentSelectionCard.CardState.EnteredFinished;
                case TabId.Closed:  return state == TournamentSelectionCard.CardState.Ended;
                default:            return true;
            }
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
