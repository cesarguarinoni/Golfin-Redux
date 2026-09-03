// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C4 — the Rounds tab (Figma 14076:33800 list / 14077:100447
// active).
//
// PLAYLIFE'S ROUNDS TAB WAS A FACADE. `rounds_map_tab.dart` never called
// /activity/checkin: CHECK IN set local state and showed a fake "+50 pts", the
// ranges and restaurants were nine hardcoded `_Spot` literals, and the map was a
// stylised drawing. This screen is the same LAYOUT with the mechanic actually
// wired — which is why almost everything here is about the two states being
// honest rather than about drawing them.
//
// ONE SCREEN, TWO STATES, AND THE FLIP IS DATA. There is no "checked-in screen":
// `RoundSession.Active` decides whether the chips or the live card occupy slot 1
// and whether the list is the chosen category or FOOD-first, and every path that
// can change it — entry, resume, check-in, check-out, a score post that closed
// the round — goes through ApplyState(). A second layout would be a second place
// for the two to disagree.
//
// THE BUTTON IS NEVER DEAD (D1, Cesar 2026-09-03). Outside the radius CHECK IN
// stays tappable, reads the distance, and TOASTS why. With no fix it toasts that
// too. The refusal that matters is enforced on the SERVER (the RPC awards 0
// outside the radius regardless of what the client sends), so the client's job
// here is explanation, not enforcement.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Telemetry;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>Binds the Rounds prefab to live venues, the map tile and the round lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class GpsRoundsScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsRounds]";

        /// <summary>The three chips, in the node's order, and the API's `category` values.</summary>
        private static readonly string[] Categories = { "golf", "range", "food" };

        /// <summary>Map surface size in the RawImage's own pixels (node 14077:33884 Map Surface).</summary>
        public const int MapW = 918;
        public const int MapH = 420;

        /// <summary>Static Maps `scale`. The proxy asks for a half-size image at 2×, so one
        /// projection pixel is two RawImage pixels — see <see cref="MapProjection.Offset"/>.</summary>
        public const float MapScale = 2f;

        /// <summary>Where the map centres when there is no fix at all: Tokyo Station, the same
        /// fallback the Flutter tab used. A blank panel would be worse than a map of somewhere.</summary>
        public const double FallbackLat = 35.681236;
        public const double FallbackLon = 139.767125;

        /// <summary>Pan re-fetch debounce (§C4). A drag emits a fix every frame; re-fetching the
        /// tile per frame would be 60 proxy calls a second against a 60/min rate limit.</summary>
        public const float PanDebounceSeconds = 0.25f;

        // ── Status row ────────────────────────────────────────────────────────
        [Header("Status row")]
        [SerializeField] private TextMeshProUGUI? _statusLeft;
        [SerializeField] private TextMeshProUGUI? _statusPillLabel;
        [SerializeField] private Image? _statusPillFill;
        [SerializeField] private Image? _statusPillStroke;

        // ── Chips ─────────────────────────────────────────────────────────────
        [Header("Category chips")]
        [SerializeField] private GameObject? _chipsRow;
        [SerializeField] private Button[] _chipButtons = new Button[0];
        [SerializeField] private Image[] _chipFills = new Image[0];
        [SerializeField] private TextMeshProUGUI[] _chipLabels = new TextMeshProUGUI[0];
        [SerializeField] private Sprite? _chipSelectedSprite;
        [SerializeField] private Sprite? _chipUnselectedSprite;

        // ── Map ───────────────────────────────────────────────────────────────
        [Header("Map panel")]
        [SerializeField] private RawImage? _mapSurface;
        [SerializeField] private Image? _mapFallback;
        [SerializeField] private RectTransform? _pinLayer;
        [SerializeField] private RectTransform? _playerDot;
        [SerializeField] private GameObject? _pinTemplate;
        [SerializeField] private Button? _recenterButton;
        [SerializeField] private GameObject? _mapAttribution;

        // ── Sort bar ──────────────────────────────────────────────────────────
        [Header("Sort bar")]
        [SerializeField] private TextMeshProUGUI? _sortLeft;
        [SerializeField] private Button? _sortToggle;
        [SerializeField] private TextMeshProUGUI? _sortToggleLabel;

        // ── Spot list ─────────────────────────────────────────────────────────
        [Header("Spot list")]
        [SerializeField] private GameObject? _spotPanel;
        [SerializeField] private CanvasGroup? _spotPanelGroup;
        [SerializeField] private TextMeshProUGUI? _spotPanelTitle;
        [SerializeField] private RoundSpotRowView[] _spotRows = new RoundSpotRowView[0];
        [SerializeField] private TextMeshProUGUI? _spotEmpty;

        // ── Active round card ─────────────────────────────────────────────────
        [Header("Active round card")]
        [SerializeField] private GameObject? _activeCard;
        [SerializeField] private TextMeshProUGUI? _cardVenue;
        [SerializeField] private TextMeshProUGUI? _cardVenueSub;
        [SerializeField] private TextMeshProUGUI? _cardSince;
        [SerializeField] private TextMeshProUGUI? _cardElapsed;
        [SerializeField] private TextMeshProUGUI? _cardPts;
        [SerializeField] private TextMeshProUGUI? _cardGps;
        [SerializeField] private TextMeshProUGUI? _cardFixes;
        [SerializeField] private Button? _scoreUploadButton;
        [SerializeField] private Button? _checkOutButton;

        // ── My recent rounds ──────────────────────────────────────────────────
        [Header("My recent rounds")]
        [SerializeField] private GameObject? _historyPanel;
        [SerializeField] private GpsHubRoundRow[] _historyRows = new GpsHubRoundRow[0];
        [SerializeField] private TextMeshProUGUI? _historyEmpty;
        [SerializeField] private GameObject? _historySeeAll;

        // ── Modals ────────────────────────────────────────────────────────────
        [Header("Modals")]
        [SerializeField] private CheckInConfirmModalController? _confirmModal;
        [SerializeField] private RoundCompleteModalController? _completeModal;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button? _backButton;

        // ═════════════════════════════════════════════════════════════════════
        // Runtime state
        // ═════════════════════════════════════════════════════════════════════

        private int _category;                 // index into Categories
        private bool _sortByName;
        private List<VenueDto> _spots = new List<VenueDto>();

        /// <summary>The active round's address line, REMEMBERED once resolved.
        ///
        /// <para><see cref="SpotSubtitleFor"/> can only answer from <see cref="_spots"/>,
        /// which is the list currently on screen — so the answer disappears whenever that
        /// list stops containing the round's venue. Two ways that happens, both seen:
        /// the card paints on entry BEFORE /venue/nearby has answered (a resumed round
        /// then shows no address at all), and opening a round flips the list to FOOD &amp;
        /// DRINK, which does not contain the golf course being played.</para>
        ///
        /// <para>The round itself is the durable thing, so the last non-empty answer is
        /// kept against its venue id and reused. Cleared when the round changes.</para>
        private int? _cardSubVenueId;
        private string _cardSubCached = string.Empty;
        private bool _cardSubFetched;

        /// <summary>Whether the list currently on screen was fetched FOR a live round.
        ///
        /// <para>The category depends on the round — `Session.HasActive ? "food" : chip` — so the
        /// list goes stale the moment that answer changes, and it changes on its own: the mirror
        /// paints a round on frame one, then <c>/activity/active</c> says it is gone (checked out
        /// on another device, or expired), and the FOOD list stays on screen under a GOLF COURSES
        /// chip. Null until the first fetch lands.</para></summary>
        private bool? _listBuiltForActive;
        private readonly List<GameObject> _pins = new List<GameObject>();
        private List<ActivityDto>? _history;

        private LocationFix? _fix;
        private double _mapLat = FallbackLat;
        private double _mapLon = FallbackLon;
        private int _mapZoom = MapProjection.DefaultZoom;

        private bool _wired;
        private bool _fetchInFlight;
        private Coroutine? _tick;
        private Coroutine? _mapFetch;

        /// <summary>§D3/§D8 — the cache-vs-fetch memory for the spot list.</summary>
        private readonly PaintGate _spotsGate = new PaintGate(Tag, "spots");
        private readonly PaintGate _historyGate = new PaintGate(Tag, "history");

        private RoundSession Session => RoundSession.Instance;

        // ═════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            WireOnce();

            _spotsGate.Rearm();
            _historyGate.Rearm();

            // Paint what is already known BEFORE any request (§D3): the mirrored round makes the
            // live card appear on frame one instead of after a round trip, and the previous
            // visit's spots keep the list from flashing empty.
            ApplyState(PaintKind.Cache);
            ShowSpots(_spots, PaintKind.Cache);
            ShowHistory(_history, PaintKind.Cache);

            Session.OnActiveChanged += OnActiveRoundChanged;
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            // The server is the source of truth for BOTH: which round is open, and what is nearby.
            ApiClient.Instance.Run(Session.Refresh(ActivityService.Instance, _ => ApplyState(PaintKind.Fetch)));
            ApiClient.Instance.Run(ActivityService.Instance.History(0, 3, OnHistoryResult));
            StartCoroutine(LocateThenFetch());

            _tick = StartCoroutine(TickElapsed());

            TelemetryService.Instance.RecordSafe("gps_rounds_open",
                () => new Dictionary<string, object>
                {
                    ["category"] = Categories[_category],
                    ["active"] = Session.HasActive,
                });
        }

        private void OnDisable()
        {
            Session.OnActiveChanged -= OnActiveRoundChanged;
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
            if (_tick != null) { StopCoroutine(_tick); _tick = null; }
            if (_mapFetch != null) { StopCoroutine(_mapFetch); _mapFetch = null; }
        }

        /// <summary>
        /// D3 — the foreground GPS trail. Unity raises this when the app comes back, which is the
        /// ONLY resume signal available without a background-location entitlement, and it is what
        /// gets a long round past K4's 3-fix threshold.
        /// </summary>
        private void OnApplicationFocus(bool focused)
        {
            if (!focused || !isActiveAndEnabled) return;
            ApiClient.Instance.Run(Session.Refresh(ActivityService.Instance, _ => ApplyState(PaintKind.Fetch)));
            StartCoroutine(LocateThenFetch());
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < _chipButtons.Length; i++)
            {
                int index = i;
                if (_chipButtons[i] == null) continue;
                _chipButtons[i].onClick.AddListener(() => SelectCategory(index));
            }

            if (_sortToggle != null) _sortToggle.onClick.AddListener(ToggleSort);
            if (_recenterButton != null) _recenterButton.onClick.AddListener(Recenter);
            if (_backButton != null)
                _backButton.onClick.AddListener(() => ScreenManager.Instance?.GoBack(ScreenId.GpsHub));

            if (_scoreUploadButton != null) _scoreUploadButton.onClick.AddListener(OpenScoreUpload);
            if (_checkOutButton != null) _checkOutButton.onClick.AddListener(OpenCheckOut);

            foreach (RoundSpotRowView row in _spotRows)
                if (row != null) row.OnAction += OnRowAction;

            if (_pinTemplate != null) _pinTemplate.SetActive(false);
        }

        private void OnLanguageChanged()
        {
            // The static labels ride LocalizedText; these are the ones this controller formats
            // itself with string.Format at bind time, and the Settings language toggle is an
            // OVERLAY that never disables this screen — so without a repaint they stay in the
            // language they were bound in.
            ApplyState(PaintKind.Repaint);
            ShowSpots(_spots, PaintKind.Repaint);
            ShowHistory(_history, PaintKind.Repaint);
        }

        // ═════════════════════════════════════════════════════════════════════
        // State: list vs active
        // ═════════════════════════════════════════════════════════════════════

        private void OnActiveRoundChanged(ActivityDto? row)
        {
            ApplyState(PaintKind.Fetch);
            // The list itself changes meaning across the flip (chosen category ⇄ FOOD first), so
            // it is re-fetched rather than re-filtered.
            StartCoroutine(FetchSpots());
        }

        /// <summary>
        /// The ONE place the two states are applied. Everything that can change which one is
        /// current calls this; nothing toggles the card or the chips directly.
        /// </summary>
        private void ApplyState(PaintKind kind)
        {
            bool active = Session.HasActive;
            bool animate = kind == PaintKind.Fetch && UiMotion.Enabled;

            if (_chipsRow != null && _chipsRow.activeSelf != !active)
            {
                _chipsRow.SetActive(!active);
                // The chips leaving and the card arriving is the flip the player reads, so it
                // cross-fades rather than cuts (§ motion). Only on a FETCH paint: a cache paint is
                // the screen appearing, and animating it would be a second entrance.
                if (animate && !active) GpsPaintMotion.FadeInPanel(this, _chipsRow, true);
            }

            if (_activeCard != null && _activeCard.activeSelf != active)
            {
                _activeCard.SetActive(active);
                if (active && animate)
                {
                    var rt = _activeCard.transform as RectTransform;
                    if (rt != null) UiMotion.Run(this, UiMotion.Pop(rt, EnsureGroup(_activeCard)));
                }
            }

            // MY RECENT ROUNDS is a LIST-state panel (§ Figma Fidelity): while a round is live the
            // card above it is the round the player cares about, and a history panel under it
            // would put the same venue on screen twice.
            if (_historyPanel != null) _historyPanel.SetActive(!active);

            PaintStatusRow();
            PaintActiveCard();
            PaintSortBar();

            // The list's CATEGORY is a function of the round (food while one is live), so a round
            // appearing or ending makes what is on screen wrong — a GOLF COURSES chip over a food
            // list, or the reverse.
            //
            // If a fetch is already running this call is a no-op (FetchSpots returns early), which
            // is exactly the case that has to be caught: the fetch in flight was started for the
            // OLD round state and its answer will be stale on arrival. FetchSpots re-checks when
            // it lands, so the correction is never dropped — see its tail.
            if (_listBuiltForActive.HasValue && _listBuiltForActive.Value != active)
                ApiClient.Instance.Run(FetchSpots());
        }

        private void PaintStatusRow()
        {
            bool active = Session.HasActive;

            if (_statusLeft != null)
            {
                _statusLeft.text = active
                    ? string.Format(LocalizationManager.Get("GPS_ROUNDS_CHECKED_IN_SINCE"),
                                    RoundSession.FormatClock(Session.CheckInAt))
                    : string.Format(LocalizationManager.Get("GPS_ROUNDS_NEARBY_COUNT"),
                                    _spots.Count.ToString(CultureInfo.InvariantCulture));
            }

            // Three states, three colours, and the OFF one is Muted rather than red: no GPS is a
            // condition to fix, not an error to alarm about.
            Color accent = active ? GpsUiColor.Gold
                         : _fix != null ? GpsUiColor.Green
                         : GpsUiColor.Muted;
            string key = active ? "GPS_ROUNDS_LIVE"
                       : _fix != null ? "GPS_ROUNDS_GPS_ON"
                       : "GPS_ROUNDS_GPS_OFF";

            if (_statusPillLabel != null)
            {
                _statusPillLabel.text = LocalizationManager.Get(key);
                _statusPillLabel.color = accent;
            }
            if (_statusPillFill != null)
                _statusPillFill.color = GpsUiColor.A(accent, active ? 0.18f : 0.16f);
            if (_statusPillStroke != null) _statusPillStroke.color = accent;
        }

        private void PaintActiveCard()
        {
            ActivityDto? row = Session.Active;
            if (row == null) return;

            if (_cardVenue != null) _cardVenue.text = row.VenueName ?? string.Empty;
            if (_cardVenueSub != null)
                _cardVenueSub.text = Session.IsExpired
                    ? LocalizationManager.Get("GPS_ROUNDS_EXPIRED")
                    : CardSubtitleFor(row.VenueId);
            if (_cardSince != null)
                _cardSince.text = string.Format(LocalizationManager.Get("GPS_ROUNDS_SINCE"),
                                                RoundSession.FormatClock(Session.CheckInAt));

            PaintElapsed();

            if (_cardPts != null)
                _cardPts.text = "+" + (row.Points.GetValueOrDefault(0) > 0
                    ? row.Points!.Value.ToString(CultureInfo.InvariantCulture)
                    : (row.GpsVerified == true ? "30" : "0"));

            if (_cardGps != null)
            {
                GpsQuality q = Session.Quality;
                _cardGps.text = CheckInConfirmModalController.QualityLabel(q);
                _cardGps.color = q == GpsQuality.High ? GpsUiColor.Green
                               : q == GpsQuality.Medium ? GpsUiColor.Gold
                               : GpsUiColor.Muted;
            }

            if (_cardFixes != null)
                _cardFixes.text = Math.Max(Session.FixCount, row.GpsCheckCount ?? 1)
                                      .ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The elapsed digits, once a second. Fixed-width by construction
        /// (<see cref="RoundSession.FormatElapsed"/> pads the minutes), so the stat cannot jitter
        /// as the value crosses 9→10.</summary>
        private void PaintElapsed()
        {
            if (_cardElapsed == null) return;
            _cardElapsed.text = RoundSession.FormatElapsed(Session.Elapsed);
        }

        private IEnumerator TickElapsed()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                if (Session.HasActive) PaintElapsed();
                yield return wait;
            }
        }

        private void PaintSortBar()
        {
            if (_sortLeft != null)
                _sortLeft.text = LocalizationManager.Get(_sortByName ? "GPS_ROUNDS_SORT_NAME"
                                                                     : "GPS_ROUNDS_SORT_NEAREST");
            if (_sortToggleLabel != null)
                _sortToggleLabel.text = LocalizationManager.Get(_sortByName ? "GPS_ROUNDS_SORT_NAME_TOGGLE"
                                                                            : "GPS_ROUNDS_SORT_DISTANCE");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Chips and sort
        // ═════════════════════════════════════════════════════════════════════

        private void SelectCategory(int index)
        {
            if (index < 0 || index >= Categories.Length || index == _category) return;
            _category = index;
            PaintChips();
            // The list is a different set of places, so it cross-fades rather than swapping
            // in place (§ motion: cross-fade on chip change).
            if (_spotPanelGroup != null && UiMotion.Enabled)
                UiMotion.Run(this, UiMotion.Fade(_spotPanelGroup, 0.2f, 1f));
            StartCoroutine(FetchSpots());
        }

        private void PaintChips()
        {
            for (int i = 0; i < _chipFills.Length; i++)
            {
                bool on = i == _category;
                if (_chipFills[i] != null)
                {
                    Sprite? want = on ? _chipSelectedSprite : _chipUnselectedSprite;
                    if (want != null) _chipFills[i].sprite = want;
                    _chipFills[i].color = Color.white;
                }
                if (i < _chipLabels.Length && _chipLabels[i] != null)
                    _chipLabels[i].color = on ? GpsUiColor.Hex("#2A1A00") : Color.white;
            }
        }

        /// <summary>
        /// The "DISTANCE ▾" toggle flips to NAME order and back.
        ///
        /// <para>It does NOT re-derive distance: the server already sorted by it (§A2), and a
        /// client-side re-sort would be a second opinion about a number the client cannot compute
        /// as well. Name order is a genuinely different question, which is why it is the only
        /// alternative offered.</para>
        /// </summary>
        private void ToggleSort()
        {
            _sortByName = !_sortByName;
            PaintSortBar();
            ShowSpots(_spots, PaintKind.Repaint);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Fetching
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// One fix, then one nearby fetch, then the map tile.
        ///
        /// <para>Sequential on purpose: the fetch's geohash prefixes and its <c>lat/lon</c> both
        /// come from the fix, and the map centres on it. Firing them in parallel would send the
        /// first request from the LAST known position, which after a flight is the wrong
        /// prefecture.</para>
        /// </summary>
        private IEnumerator LocateThenFetch()
        {
            IEnumerator locate = LocationProviderFactory().Fetch(
                UnityLocationProvider.DefaultTimeoutSeconds, OnLocation);
            while (locate.MoveNext()) yield return locate.Current;

            IEnumerator fetch = FetchSpots();
            while (fetch.MoveNext()) yield return fetch.Current;
        }

        private void OnLocation(LocationResult result)
        {
            if (result != null && result.Ok && result.Fix != null)
            {
                _fix = result.Fix;
                _mapLat = _fix.Lat;
                _mapLon = _fix.Lon;
                // The trail only advances while a round is live — a fix taken while browsing is
                // not evidence of playing anything.
                // The accuracy label is shown before a round exists (confirm modal), so the
                // fix is always NOTED; only an ACTIVE round feeds the paid trail.
                Session.NoteFix(_fix);
                if (Session.HasActive) Session.RecordFix(_fix);
                Debug.Log($"{Tag} fix {_fix}");
            }
            else
            {
                Debug.LogWarning($"{Tag} no fix ({result?.Reason}) — listing from " +
                                 "the last known centre, CHECK IN disabled.");
            }
            PaintStatusRow();
            RequestMapTile();
        }

        /// <summary>
        /// The location seam.
        ///
        /// <para>⚠️ EDITOR EVIDENCE (SPEC § Acceptance). <c>UnityLocationProvider</c> cannot run in
        /// the Editor — <c>Input.location</c> is a no-op there and it says so by returning
        /// <c>Unknown</c>. <see cref="EditorFixOverride"/> is what makes the whole flow testable
        /// from a play-mode session: set it to a fix and every entry uses it. It is
        /// <c>UNITY_EDITOR</c>-only, so no player build can carry a mocked position.</para>
        /// </summary>
        private ILocationProvider LocationProviderFactory()
        {
#if UNITY_EDITOR
            if (EditorFixOverride != null) return new FixedLocationProvider(EditorFixOverride);
#endif
            return new UnityLocationProvider();
        }

#if UNITY_EDITOR
        /// <summary>Editor-only mocked position. Set from a menu item or a test to stand at a
        /// venue (1993 TEST Office) without leaving the desk.</summary>
        public static LocationFix? EditorFixOverride;

        /// <summary>An <see cref="ILocationProvider"/> that always answers with one fix.</summary>
        private sealed class FixedLocationProvider : ILocationProvider
        {
            private readonly LocationFix _fix;
            public FixedLocationProvider(LocationFix fix) { _fix = fix; }
            public IEnumerator Fetch(float timeoutSeconds, Action<LocationResult> onResult)
            {
                onResult?.Invoke(LocationResult.Success(_fix));
                yield break;
            }
        }
#endif

        private IEnumerator FetchSpots()
        {
            if (_fetchInFlight) yield break;
            _fetchInFlight = true;

            // While a round is live the list is FOOD first, exactly as the Flutter tab had it —
            // the player is at a course and what is useful next is where to eat.
            bool builtForActive = Session.HasActive;
            string category = builtForActive ? "food" : Categories[_category];

            double centreLat = _fix?.Lat ?? _mapLat;
            double centreLon = _fix?.Lon ?? _mapLon;
            string prefixes = Geohash.NearbyPrefixes(centreLat, centreLon);

            ApiResult<List<VenueDto>>? result = null;
            IEnumerator call = VenueService.Instance.Nearby(
                prefixes, category,
                _fix != null ? (double?)_fix.Lat : null,
                _fix != null ? (double?)_fix.Lon : null,
                LanguageCode(), r => result = r);
            while (call.MoveNext()) yield return call.Current;

            _fetchInFlight = false;

            if (result == null || !result.Success)
            {
                Debug.LogWarning($"{Tag} /venue/nearby failed ({result?.ErrorKind}) — keeping the " +
                                 "previous list.");
                ShowSpots(_spots, PaintKind.Fetch);
                yield break;
            }

            _spots = result.Data ?? new List<VenueDto>();
            _listBuiltForActive = builtForActive;
            ShowSpots(_spots, PaintKind.Fetch);
            PaintStatusRow();
            PaintPins();

            // The round may have started or ended WHILE this request was in flight — which is the
            // common case on entry, where the round refresh and this fetch race. The answer just
            // painted was built for the other state, so fetch once more for the state that now
            // holds. This converges: the re-run reads the CURRENT value, so it repeats only while
            // the round is genuinely still changing.
            if (builtForActive != Session.HasActive)
            {
                ApiClient.Instance.Run(FetchSpots());
                yield break;
            }
            // The card may have painted before this answer arrived, with no list to resolve the
            // address from. It gets a second chance now rather than staying blank until the
            // player navigates away and back.
            PaintActiveCard();
        }

        private void OnHistoryResult(ApiResult<List<ActivityDto>> result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                    Debug.LogWarning($"{Tag} /activity/history failed ({result.ErrorKind}).");
                ShowHistory(null, PaintKind.Fetch);
                return;
            }
            ShowHistory(result.Data, PaintKind.Fetch);
        }

        private static string LanguageCode()
            => LocalizationManager.CurrentLanguage == Language.Japanese ? "ja" : "en";

        // ═════════════════════════════════════════════════════════════════════
        // Painting the list
        // ═════════════════════════════════════════════════════════════════════

        private void ShowSpots(List<VenueDto>? rows, PaintKind kind)
        {
            _spots = rows ?? new List<VenueDto>();

            List<VenueDto> ordered = _sortByName
                ? SortedByName(_spots)
                : _spots;                     // the server's own nearest-first order

            int count = 0;
            var painted = new List<Transform>(_spotRows.Length);
            for (int i = 0; i < _spotRows.Length; i++)
            {
                RoundSpotRowView row = _spotRows[i];
                if (row == null) continue;
                if (i >= ordered.Count) { row.gameObject.SetActive(false); continue; }

                VenueDto v = ordered[i];
                row.gameObject.SetActive(true);
                row.Bind(v, StateFor(v), RingColourFor(v), DistanceColourFor(v));
                painted.Add(row.transform);
                count++;
            }

            if (_spotPanelTitle != null)
                _spotPanelTitle.text = LocalizationManager.Get(
                    Session.HasActive ? "GPS_ROUNDS_NEARBY_FOOD" : "GPS_ROUNDS_NEAR_YOU");

            bool stagger = _spotsGate.Should(kind, count);
            bool cold = _spotsGate.IsCold;
            GpsPaintMotion.Shimmer(gameObject, ShimmerHost.RoundsSpots, cold);

            if (_spotEmpty != null)
            {
                bool showEmpty = count == 0 && !cold;
                _spotEmpty.gameObject.SetActive(showEmpty);
                if (showEmpty) _spotEmpty.text = LocalizationManager.Get("GPS_ROUNDS_EMPTY");
            }
            if (_spotPanel != null) _spotPanel.SetActive(true);

            if (stagger) GpsPaintMotion.StaggerRise(this, painted);
        }

        private void ShowHistory(List<ActivityDto>? rows, PaintKind kind)
        {
            _history = rows;
            int count = 0;
            var painted = new List<Transform>(_historyRows.Length);
            if (rows != null)
            {
                foreach (ActivityDto r in rows)
                {
                    if (r == null || count >= _historyRows.Length) break;
                    GpsHubRoundRow row = _historyRows[count];
                    if (row == null) continue;
                    row.gameObject.SetActive(true);
                    row.Bind(r, false);
                    painted.Add(row.transform);
                    count++;
                }
            }
            for (int i = count; i < _historyRows.Length; i++)
                if (_historyRows[i] != null) _historyRows[i].gameObject.SetActive(false);

            bool stagger = _historyGate.Should(kind, count);
            GpsPaintMotion.Shimmer(gameObject, ShimmerHost.RoundsHistory, _historyGate.IsCold);

            if (_historyEmpty != null)
            {
                bool showEmpty = count == 0 && !_historyGate.IsCold;
                _historyEmpty.gameObject.SetActive(showEmpty);
                if (showEmpty) _historyEmpty.text = LocalizationManager.Get("GPS_ROUNDS_NO_ROUNDS");
            }

            // "ALL ROUNDS ›" is authored and HIDDEN: there is no full-history screen to open, and
            // a link that goes nowhere is worse than no link (SPEC § Out of scope names this
            // explicitly, and the backlog row stays either way).
            if (_historySeeAll != null) _historySeeAll.SetActive(false);

            if (stagger) GpsPaintMotion.StaggerRise(this, painted);
        }

        private static List<VenueDto> SortedByName(List<VenueDto> rows)
        {
            var copy = new List<VenueDto>(rows);
            copy.Sort((a, b) => string.Compare(a?.Name, b?.Name, StringComparison.CurrentCulture));
            return copy;
        }

        /// <summary>
        /// Which action the row offers. The FOUR-way answer is what keeps a dead button off the
        /// screen: a spot is either checkable-in, too far (and says how far), unlocatable (and
        /// says so), or already-superseded by a live round.
        /// </summary>
        private RoundSpotRowView.ActionState StateFor(VenueDto v)
        {
            if (Session.HasActive) return RoundSpotRowView.ActionState.Details;
            if (_fix == null || !v.DistanceM.HasValue) return RoundSpotRowView.ActionState.NoGps;
            double radius = v.GpsRadiusM ?? 500.0;
            return v.DistanceM.Value <= radius
                ? RoundSpotRowView.ActionState.CheckIn
                : RoundSpotRowView.ActionState.TooFar;
        }

        private static Color RingColourFor(VenueDto v)
            => v.Category == "food" ? GpsUiColor.Food
             : v.IsPartner ? GpsUiColor.Green
             : GpsUiColor.GoldSoft;

        private static Color DistanceColourFor(VenueDto v)
            => v.Category == "food" ? GpsUiColor.Food : GpsUiColor.Green;

        /// <summary>The card's address line: the live lookup when the list can answer, the
        /// remembered one when it cannot. See <see cref="_cardSubCached"/>.</summary>
        private string CardSubtitleFor(int? venueId)
        {
            if (venueId == null) return string.Empty;

            if (_cardSubVenueId != venueId)          // a different round — forget the old address
            {
                _cardSubVenueId = venueId;
                _cardSubCached = string.Empty;
                _cardSubFetched = false;
            }

            string live = SpotSubtitleFor(venueId);
            if (!string.IsNullOrEmpty(live)) _cardSubCached = live;

            // A RESUMED round can never be resolved from the list: opening a round flips the list
            // to FOOD & DRINK, which by definition does not contain the golf course being played,
            // so there is nothing on screen to read the address off and nothing was cached this
            // process. The round knows its venue id, so ask the server for that ONE venue. Once
            // per round, and only when the list genuinely could not answer.
            if (string.IsNullOrEmpty(_cardSubCached) && !_cardSubFetched && isActiveAndEnabled)
            {
                _cardSubFetched = true;
                StartCoroutine(FetchCardSubtitle(venueId.Value));
            }

            return _cardSubCached;
        }

        /// <summary>One <c>/venue/{id}</c> for the active round's address line. A failure is
        /// silent: an empty sub-line is a missing detail, never a reason to interrupt a round.</summary>
        private IEnumerator FetchCardSubtitle(int venueId)
        {
            ApiResult<VenueDto>? result = null;
            yield return VenueService.Instance.ById(venueId, LanguageCode(), r => result = r);

            if (result == null || !result.Success || result.Data == null)
            {
                Debug.LogWarning($"{Tag} /venue/{venueId} failed — the card keeps an empty address.");
                yield break;
            }

            string sub = RoundSpotRowView.SubtitleOf(result.Data);
            if (string.IsNullOrEmpty(sub) || _cardSubVenueId != venueId) yield break;

            _cardSubCached = sub;
            PaintActiveCard();
        }

        private string SpotSubtitleFor(int? venueId)
        {
            if (venueId == null) return string.Empty;
            foreach (VenueDto v in _spots)
                if (v != null && v.Id == venueId.Value) return RoundSpotRowView.SubtitleOf(v);
            return string.Empty;
        }

        // ═════════════════════════════════════════════════════════════════════
        // The map
        // ═════════════════════════════════════════════════════════════════════

        private void Recenter()
        {
            if (_fix == null)
            {
                Toast("GPS_ROUNDS_NO_GPS_TOAST");
                return;
            }
            _mapLat = _fix.Lat;
            _mapLon = _fix.Lon;
            RequestMapTile();
        }

        private void RequestMapTile()
        {
            if (_mapFetch != null) StopCoroutine(_mapFetch);
            _mapFetch = StartCoroutine(FetchMapTile());
        }

        private IEnumerator FetchMapTile()
        {
            yield return new WaitForSecondsRealtime(PanDebounceSeconds);

            Texture2D? tex = null;
            IEnumerator call = VenueService.Instance.MapTile(_mapLat, _mapLon, _mapZoom, MapW, MapH,
                                                             t => tex = t);
            while (call.MoveNext()) yield return call.Current;

            bool ok = tex != null;
            if (_mapSurface != null)
            {
                // The pins FADE with the tile rather than snapping to their new positions (§
                // motion: pin placement animates on tile re-fetch), so a pan reads as one movement
                // instead of a picture change plus a marker jump.
                if (ok) _mapSurface.texture = tex;
                _mapSurface.gameObject.SetActive(ok);
                if (ok && UiMotion.Enabled)
                    UiMotion.Run(this, UiMotion.Fade(EnsureGroup(_mapSurface.gameObject), 0.4f, 1f));
            }
            if (_mapFallback != null) _mapFallback.gameObject.SetActive(!ok);
            if (_mapAttribution != null) _mapAttribution.SetActive(ok);

            PaintPins();
            _mapFetch = null;
        }

        /// <summary>
        /// Place a pin per visible spot, plus the player dot, by Web-Mercator projection against
        /// the tile's own centre and zoom (<see cref="MapProjection"/>).
        ///
        /// <para>Pins are POOLED from one authored template rather than rebuilt: a category switch
        /// repaints them up to 50 at a time, and instantiating a marker prefab per paint is the
        /// kind of per-frame allocation the A13 GC gate exists to catch.</para>
        /// </summary>
        private void PaintPins()
        {
            if (_pinLayer == null || _pinTemplate == null) return;

            int used = 0;
            foreach (VenueDto v in _spots)
            {
                if (v?.Latitude == null || v.Longitude == null) continue;
                Vector2 offset = MapProjection.Offset(_mapLat, _mapLon,
                                                      v.Latitude.Value, v.Longitude.Value,
                                                      _mapZoom, MapScale);
                if (!MapProjection.IsVisible(offset, MapW, MapH, 22f)) continue;

                GameObject pin = PinAt(used++);
                var rt = pin.transform as RectTransform;
                if (rt != null) rt.anchoredPosition = offset;
                var img = pin.GetComponent<Image>();
                if (img != null) img.color = PinColourFor(v);
                pin.SetActive(true);
            }

            for (int i = used; i < _pins.Count; i++)
                if (_pins[i] != null) _pins[i].SetActive(false);

            if (_playerDot != null)
            {
                bool show = _fix != null;
                _playerDot.gameObject.SetActive(show);
                if (show)
                    _playerDot.anchoredPosition = MapProjection.Offset(
                        _mapLat, _mapLon, _fix!.Lat, _fix.Lon, _mapZoom, MapScale);
            }
        }

        private static Color PinColourFor(VenueDto v)
            => v.Category == "food" ? GpsUiColor.Food
             : v.IsPartner ? GpsUiColor.Green
             : GpsUiColor.Registered;

        private GameObject PinAt(int index)
        {
            while (_pins.Count <= index)
            {
                GameObject clone = Instantiate(_pinTemplate!, _pinLayer);
                clone.name = "Pin" + _pins.Count;
                _pins.Add(clone);
            }
            return _pins[index];
        }

        private static CanvasGroup EnsureGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Check in
        // ═════════════════════════════════════════════════════════════════════

        private void OnRowAction(RoundSpotRowView row)
        {
            VenueDto? v = row?.Venue;
            if (v == null) return;

            switch (row!.State)
            {
                case RoundSpotRowView.ActionState.CheckIn:
                    if (_confirmModal == null)
                    {
                        Debug.LogWarning($"{Tag} no confirm modal wired.");
                        return;
                    }
                    _confirmModal.Open(v, Session.Quality, StartCheckIn);
                    break;

                case RoundSpotRowView.ActionState.TooFar:
                    // D1: the button is alive precisely so this can happen.
                    Toast(string.Format(LocalizationManager.Get("GPS_ROUNDS_TOO_FAR_TOAST"),
                                        v.Name, RoundSpotRowView.Km(v.DistanceM)));
                    break;

                case RoundSpotRowView.ActionState.NoGps:
                    Toast("GPS_ROUNDS_NO_GPS_TOAST");
                    break;

                case RoundSpotRowView.ActionState.Details:
                    ShowDetails(v);
                    break;
            }
        }

        /// <summary>
        /// The DETAILS affordance while a round is live.
        ///
        /// <para>NOTED AS A DEVIATION (SPEC §C4 asks for "the existing Venue detail treatment if
        /// one exists; else a read-only modal — NOTE which"). There is NO venue detail screen in
        /// the project: <c>VenuePickerModalController</c> is a picker, not a detail view. Rather
        /// than build a screen this task did not scope, DETAILS raises a toast carrying the row's
        /// own offer/price — the information a read-only modal would have shown, in the one
        /// surface that already exists.</para>
        /// </summary>
        private void ShowDetails(VenueDto v)
        {
            string detail = !string.IsNullOrWhiteSpace(v.PartnerOffer) ? v.PartnerOffer
                          : !string.IsNullOrWhiteSpace(v.PriceLabel) ? v.PriceLabel
                          : RoundSpotRowView.SubtitleOf(v);
            Toast(string.IsNullOrWhiteSpace(detail) ? v.Name : v.Name + " — " + detail);
        }

        private void StartCheckIn(VenueDto venue) => StartCoroutine(CheckInRoutine(venue));

        private IEnumerator CheckInRoutine(VenueDto venue)
        {
            _confirmModal?.BeginPending();

            // The key is minted and PERSISTED BEFORE the request leaves (§C3). This is the whole
            // force-quit story: the retry carries the SAME key and replays into the same row.
            string key = Session.BeginCheckInKey();

            ApiResult<CheckInResult>? result = null;
            IEnumerator call = ActivityService.Instance.CheckIn(venue.Id, _fix, key, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            if (result == null)
            {
                _confirmModal?.Finish(false);
                Toast("GPS_ROUNDS_CHECKIN_FAILED");
                yield break;
            }

            if (!result.Success)
            {
                // A REFUSAL IS AN ANSWER, so the key is spent — but a NETWORK failure is not, and
                // keeping the key is what makes the retry safe.
                string? reason = ActivityService.ReasonOf(result.RawBody);
                bool answered = result.StatusCode >= 400 && result.StatusCode < 500;
                if (answered) Session.ClearCheckInKey();

                if (reason == "already_active")
                {
                    Debug.Log($"{Tag} already_active — adopting the open round instead.");
                    Toast("GPS_ROUNDS_ALREADY_ACTIVE");
                    _confirmModal?.Finish(true);
                    ApiClient.Instance.Run(Session.Refresh(ActivityService.Instance,
                                                           _ => ApplyState(PaintKind.Fetch)));
                    yield break;
                }

                Debug.LogWarning($"{Tag} check-in failed ({result.ErrorKind} {result.StatusCode} " +
                                 $"reason={reason}).");
                _confirmModal?.Finish(false);
                Toast("GPS_ROUNDS_CHECKIN_FAILED");
                yield break;
            }

            Session.ClearCheckInKey();
            CheckInResult data = result.Data;
            if (data?.Activity != null) Session.SetActive(data.Activity);
            Session.RecordFix(_fix);

            _confirmModal?.Finish(true);

            // The Top UI counts the award up rather than swapping to it (§ motion). A replay
            // awards 0, and counting up by nothing would tell the player they earned twice.
            if (data != null && data.Awarded > 0)
            {
                PointsService.Instance.RefreshBalanceAsync();
                Toast(string.Format(LocalizationManager.Get("GPS_ROUNDS_CHECKED_IN"),
                                    venue.Name, data.Awarded.ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                // Verified-but-unpaid is the replay case; unverified is the "you were not close
                // enough for the server" case. Both are honest and neither is an error.
                Toast(string.Format(LocalizationManager.Get("GPS_ROUNDS_CHECKED_IN_NO_PTS"),
                                    venue.Name));
            }

            TelemetryService.Instance.RecordSafe("gps_round_checkin",
                () => new Dictionary<string, object>
                {
                    ["venue_id"] = venue.Id,
                    ["awarded"] = data?.Awarded ?? 0,
                    ["verified"] = data?.GpsVerified ?? false,
                    ["replayed"] = data?.Replayed ?? false,
                });

            ApplyState(PaintKind.Fetch);
            yield return FetchSpots();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Check out
        // ═════════════════════════════════════════════════════════════════════

        private void OpenCheckOut()
        {
            ActivityDto? row = Session.Active;
            if (row == null || _completeModal == null) return;

            _completeModal.OpenConfirm(
                row.VenueName ?? string.Empty,
                string.Format(LocalizationManager.Get("GPS_ROUNDS_SINCE"),
                              RoundSession.FormatClock(Session.CheckInAt)),
                Session.Elapsed,
                Math.Max(Session.FixCount, row.GpsCheckCount ?? 1),
                Session.IsExpired,
                () => StartCoroutine(CheckOutRoutine(row)),
                OpenScoreUpload);
        }

        private IEnumerator CheckOutRoutine(ActivityDto row)
        {
            _completeModal?.BeginPending();
            string key = Session.BeginCheckOutKey();

            ApiResult<CheckOutResult>? result = null;
            IEnumerator call = ActivityService.Instance.CheckOut(
                row.Id, _fix, Math.Max(Session.FixCount, row.GpsCheckCount ?? 1), key,
                r => result = r);
            while (call.MoveNext()) yield return call.Current;

            if (result == null || !result.Success)
            {
                bool answered = result != null && result.StatusCode >= 400 && result.StatusCode < 500;
                if (answered) Session.ClearCheckOutKey();
                Debug.LogWarning($"{Tag} check-out failed ({result?.ErrorKind}).");
                _completeModal?.FailPending();
                Toast("GPS_ROUNDS_CHECKOUT_FAILED");
                yield break;
            }

            Session.ClearCheckOutKey();
            CheckOutResult data = result.Data;

            DateTimeOffset? start = Session.CheckInAt;
            // The END time is the SERVER's check_out_at, not this device's clock: the receipt is a
            // record of what the server recorded, and a device whose clock drifts must not print a
            // different round than the one the backend paid for. Falls back to now only if the
            // response carried no timestamp.
            DateTimeOffset? end = RoundSession.ParseTimestamp(data?.Activity?.CheckOutAt)
                                  ?? DateTimeOffset.UtcNow;
            _completeModal?.ShowReceipt(
                data, Session.Elapsed, Math.Max(Session.FixCount, row.GpsCheckCount ?? 1),
                row.VenueName ?? string.Empty,
                RoundCompleteModalController.ReceiptSub(start, end,
                                                        data?.GpsVerified ?? false));

            // The round is closed the moment the server says so, NOT when the modal is dismissed:
            // leaving the card up behind an open receipt would show a live round that is over.
            Session.SetActive(null);

            if (data != null && data.Awarded > 0) PointsService.Instance.RefreshBalanceAsync();

            TelemetryService.Instance.RecordSafe("gps_round_checkout",
                () => new Dictionary<string, object>
                {
                    ["activity_id"] = row.Id,
                    ["awarded"] = data?.Awarded ?? 0,
                    ["expired"] = data?.Expired ?? false,
                });

            ApplyState(PaintKind.Fetch);
            ApiClient.Instance.Run(ActivityService.Instance.History(0, 3, OnHistoryResult));
            yield return FetchSpots();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Score upload
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// SCORE UPLOAD, from the card or from the check-out receipt.
        ///
        /// <para>The draft is armed with the round's venue AND its id, so the GPS step opens with
        /// the course already chosen and the post CLOSES the round rather than opening a second
        /// history row beside it (§A5 / D6). From the receipt the round is already closed, and
        /// arming only the venue is the right call — which is what a null Active gives.</para>
        /// </summary>
        private void OpenScoreUpload()
        {
            ActivityDto? row = Session.Active;
            VenueDto? venue = row?.VenueId != null ? FindSpot(row.VenueId.Value) : null;

            ScoreUploadDraft.Arm(new ScoreUploadDraft.Prefill
            {
                ActivityId = row?.Id,
                VenueId = row?.VenueId,
                VenueName = row?.VenueName,
                DistanceM = venue?.DistanceM,
            });

            Debug.Log($"{Tag} SCORE UPLOAD -> round #{row?.Id} at {row?.VenueName}");
            ScreenManager.Instance?.ShowScreen(ScreenId.ScoreUpload);
        }

        private VenueDto? FindSpot(int id)
        {
            foreach (VenueDto v in _spots)
                if (v != null && v.Id == id) return v;
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Toast
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>A localization KEY, or an already-formatted string. Keys never contain a
        /// space, which is what tells the two apart without a second parameter.</summary>
        private static void Toast(string keyOrMessage)
        {
            if (ToastController.Instance == null) return;
            string message = keyOrMessage != null && keyOrMessage.IndexOf(' ') < 0
                ? LocalizationManager.Get(keyOrMessage)
                : keyOrMessage ?? string.Empty;
            ToastController.Instance.Show(message);
        }
    }
}
