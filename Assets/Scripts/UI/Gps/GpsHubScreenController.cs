// ─────────────────────────────────────────────────────────────────────────────
// gps_hub_entry §5 — the GPS / PLAYLIFE hub screen (Figma 14011:32819).
//
// The hub is a FRONT DOOR, not a feature. Everything on it that is not a real
// number is deliberately inert in v1: the four action tiles and four of the five
// hub nav buttons log and do nothing, because their screens do not exist yet and
// a "coming soon" modal was explicitly NOT asked for. What IS live is the hero
// panel (from /user/detail) and MY RECENT ROUNDS (from /score/history) — the two
// places where a player sees their own PLAYLIFE data inside the game for the
// first time.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Binds the GPS hub prefab to live data and wires its inert affordances.
    ///
    /// <para>
    /// Event-driven, not polled: <see cref="PointsService.OnDisplayBalanceChanged"/> and
    /// <see cref="UserService.OnDetailChanged"/> are subscribed in <c>OnEnable</c> and dropped in
    /// <c>OnDisable</c>, so re-entering the screen repaints from cache on the first frame and the
    /// network answer only ever moves numbers forward.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsHubScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsHub]";

        /// <summary>What a stat shows before the server has answered. Never <c>0</c> — see
        /// <see cref="UserDetailDto"/>'s note on why a null is not a zero.</summary>
        private const string Unknown = "—";

        // ── Hero panel ────────────────────────────────────────────────────────
        [Header("Hero panel")]
        [SerializeField] private TextMeshProUGUI? _avatarInitial;
        [SerializeField] private TextMeshProUGUI? _playerName;
        [SerializeField] private TextMeshProUGUI? _playerSub;
        [SerializeField] private TextMeshProUGUI? _statPoints;
        [SerializeField] private TextMeshProUGUI? _statBest;
        [SerializeField] private TextMeshProUGUI? _statTrust;
        [SerializeField] private TextMeshProUGUI? _statAvatar;

        // ── Recent rounds ─────────────────────────────────────────────────────
        [Header("My recent rounds")]
        [Tooltip("The whole panel. Hidden outright when the player has no posted scores, or when " +
                 "/score/history fails — v1 ships no empty state (SPEC § Figma Fidelity).")]
        [SerializeField] private GameObject? _roundsPanel;

        /// <summary>The one-line "no rounds yet" label inside <see cref="_roundsPanel"/>.</summary>
        [SerializeField] private TextMeshProUGUI? _roundsEmpty;

        [Tooltip("Exactly three authored rows, top to bottom. A row with no data is deactivated.")]
        [SerializeField] private GpsHubRoundRow[] _roundRows = new GpsHubRoundRow[0];

        // ── Inert affordances (v1) ────────────────────────────────────────────
        [Header("Inert in v1")]
        [Tooltip("The four action tiles. Made non-interactable on enable; each logs its name on tap.")]
        [SerializeField] private Button[] _tileButtons = new Button[0];

        [Tooltip("Names parallel to _tileButtons, used only in the log line.")]
        [SerializeField] private string[] _tileNames = new string[0];

        [Tooltip("The four non-Home hub nav buttons. Each logs its name on tap.")]
        [SerializeField] private Button[] _navButtons = new Button[0];

        [Tooltip("Names parallel to _navButtons, used only in the log line.")]
        [SerializeField] private string[] _navNames = new string[0];

        [Tooltip("The hub's own HOME nav button — interactable, and a deliberate no-op: it is the " +
                 "screen you are already on.")]
        [SerializeField] private Button? _navHomeButton;

        // ── Live affordances (score_upload_flow) ──────────────────────────────
        [Header("Live")]
        [Tooltip("The camera centre button of the hub nav bar. The FIRST live nav slot: it opens " +
                 "the score upload flow. Must NOT also appear in _navButtons, which makes its " +
                 "entries non-interactable.")]
        [SerializeField] private Button? _navCameraButton;

        [Tooltip("Hub Profile nav slot — wired outside the inert _navButtons loop (gps_profile_pack).")]
        [SerializeField] private Button? _navProfileButton;

        [Tooltip("The SCREENSHOT action tile — the same destination as the camera button, and the " +
                 "reason the tile row is no longer entirely inert. Must NOT also appear in " +
                 "_tileButtons.")]
        [SerializeField] private Button? _tileScreenshotButton;

        [Tooltip("gps_gifts_votes — the hub's GIFT nav slot. Lifted out of the inert _navButtons " +
                 "loop, which sets interactable = false.")]
        [SerializeField] private Button? _navGiftButton;

        [Tooltip("gps_gifts_votes — the GIFT action tile. Same destination as the nav slot. Must " +
                 "NOT also appear in _tileButtons.")]
        [SerializeField] private Button? _tileGiftButton;

        [Tooltip("gps_gifts_votes — the VOTE action tile (the frame's LIVE VOTES affordance). " +
                 "Must NOT also appear in _tileButtons.")]
        [SerializeField] private Button? _tileVoteButton;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button? _backButton;

        private bool _wiredOnce;

        /// <summary>
        /// The last rows MY RECENT ROUNDS was bound from, kept so a language change can re-bind
        /// them. The row copy ("today", "● Trust 80%", "(18 holes)") is resolved IMPERATIVELY at
        /// bind time, and the Settings language toggle is an OVERLAY that never disables this
        /// screen — so without this the panel keeps rendering the previous language until the
        /// player leaves and comes back. Same scar as the settings-overlay stale-text bug.
        /// </summary>
        private List<ActivityDto>? _lastRows;

        // ═════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            WireOnce();

            // Paint what is already known BEFORE any request, so re-entering the screen never
            // flashes "—" over numbers that were correct a moment ago.
            ApplyIdentityFallback();
            ApplyDetail(UserService.Instance.LastDetail);

            PointsService.Instance.OnDisplayBalanceChanged += OnDisplayBalanceChanged;
            UserService.Instance.OnDetailChanged += ApplyDetail;
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            var client = ApiClient.Instance;
            client.Run(UserService.Instance.Detail(OnDetailResult));
            client.Run(ScoreHistoryService.Instance.History(0, 3, OnHistoryResult));

            TelemetryService.Instance.RecordSafe("gps_hub_open",
                () => new Dictionary<string, object>
                {
                    ["source"] = "home_banner",
                });
        }

        private void OnDisable()
        {
            PointsService.Instance.OnDisplayBalanceChanged -= OnDisplayBalanceChanged;
            UserService.Instance.OnDetailChanged -= ApplyDetail;
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        /// <summary>
        /// Listeners are added ONCE, not per enable: <c>onClick</c> is additive, so re-adding on
        /// every screen entry would fire a tap N times after N visits.
        /// </summary>
        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            if (_backButton != null)
                _backButton.onClick.AddListener(OnBackClicked);

            // The tiles ARE the promise of the feature, so they keep full opacity and are simply
            // not interactable (SPEC § Figma Fidelity, Action tiles). The listener is still added
            // so that turning one on later is a one-line change rather than a re-wire.
            for (int i = 0; i < _tileButtons.Length; i++)
            {
                Button? b = _tileButtons[i];
                if (b == null) continue;
                string label = i < _tileNames.Length ? _tileNames[i] : b.name;
                b.interactable = false;
                b.onClick.AddListener(() => Debug.Log($"{Tag} tile {label} — not wired yet"));
            }

            // Same posture as the tiles, and for the same reason: the slot has to LOOK like the
            // finished nav bar. The prefab's disabledColor is opaque white, so `interactable=false`
            // gates the tap without greying the ring.
            for (int i = 0; i < _navButtons.Length; i++)
            {
                Button? b = _navButtons[i];
                if (b == null) continue;
                string label = i < _navNames.Length ? _navNames[i] : b.name;
                b.interactable = false;
                b.onClick.AddListener(() => Debug.Log($"{Tag} nav {label} — not wired yet"));
            }

            // HOME is the only interactable nav slot and it goes nowhere on purpose: it is the
            // screen the player is standing on, and a lit slot that does nothing reads correctly.
            if (_navHomeButton != null)
                _navHomeButton.onClick.AddListener(() => Debug.Log($"{Tag} nav HOME — already here"));

            // score_upload_flow §2 — the two entry points that were inert at gps_hub_entry. Both go
            // to the same screen: the camera button is the affordance a player reaches for, the
            // tile is the one the "how it works" strip has been promising since the hub shipped.
            // Wired here rather than through _navButtons/_tileButtons because those two loops
            // set interactable = false, which is exactly what these must not be.
            if (_navCameraButton != null)
            {
                _navCameraButton.interactable = true;
                _navCameraButton.onClick.AddListener(OpenScoreUpload);
            }

            // gps_profile_pack — Profile nav slot (lifted out of the inert _navButtons loop)
            if (_navProfileButton != null)
            {
                _navProfileButton.interactable = true;
                _navProfileButton.onClick.AddListener(() =>
                    GolfinRedux.UI.ScreenManager.Instance?.ShowScreen(GolfinRedux.UI.ScreenId.GpsProfile));
            }
            if (_tileScreenshotButton != null)
            {
                _tileScreenshotButton.interactable = true;
                _tileScreenshotButton.onClick.AddListener(OpenScoreUpload);
            }

            // gps_gifts_votes — the last two inert affordances on the hub. Both the nav slot and
            // the tile go to the same screen, for the same reason the camera button and the
            // SCREENSHOT tile do: the nav slot is what a player reaches for, the tile is what the
            // frame has been promising since the hub shipped.
            if (_navGiftButton != null)
            {
                _navGiftButton.interactable = true;
                _navGiftButton.onClick.AddListener(() => Open(ScreenId.GpsGift, "nav GIFT"));
            }
            if (_tileGiftButton != null)
            {
                _tileGiftButton.interactable = true;
                _tileGiftButton.onClick.AddListener(() => Open(ScreenId.GpsGift, "tile GIFT"));
            }
            if (_tileVoteButton != null)
            {
                _tileVoteButton.interactable = true;
                _tileVoteButton.onClick.AddListener(() => Open(ScreenId.GpsVote, "tile VOTE"));
            }
        }

        private void Open(ScreenId id, string source)
        {
            Debug.Log($"{Tag} {source} -> {id}.");
            ScreenManager.Instance?.ShowScreen(id);
        }

        private void OpenScoreUpload()
        {
            Debug.Log($"{Tag} opening the score upload flow.");
            ScreenManager.Instance?.ShowScreen(ScreenId.ScoreUpload);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Hero panel
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The name and initial come from the SESSION, which is known before any request — so the
        /// player sees their own name on frame one and only the golf numbers wait on the server.
        /// </summary>
        private void ApplyIdentityFallback()
        {
            string name = PlayerIdentity.DisplayNameOr("PLAYER");
            SetName(name);

            if (_playerSub != null && string.IsNullOrEmpty(_playerSub.text))
                _playerSub.text = FormatSub(null, null);

            SetStatsUnknownIfEmpty();
        }

        private void SetStatsUnknownIfEmpty()
        {
            if (_statPoints != null && string.IsNullOrEmpty(_statPoints.text)) _statPoints.text = Unknown;
            if (_statBest   != null && string.IsNullOrEmpty(_statBest.text))   _statBest.text   = Unknown;
            if (_statTrust  != null && string.IsNullOrEmpty(_statTrust.text))  _statTrust.text  = Unknown;
            if (_statAvatar != null && string.IsNullOrEmpty(_statAvatar.text)) _statAvatar.text = Unknown;
        }

        private void SetName(string name)
        {
            string upper = (name ?? string.Empty).ToUpperInvariant();
            if (_playerName != null) _playerName.text = upper;
            if (_avatarInitial != null)
                _avatarInitial.text = upper.Length > 0 ? upper.Substring(0, 1) : "?";
        }

        /// <summary>
        /// Bind the hero panel from a profile row. Null leaves the panel exactly as it is (see
        /// <see cref="UserService.Detail"/>) rather than blanking numbers that were already right.
        /// </summary>
        private void ApplyDetail(UserDetailDto? d)
        {
            if (d == null) return;

            if (!string.IsNullOrWhiteSpace(d.DisplayName)) SetName(d.DisplayName);

            if (_playerSub != null) _playerSub.text = FormatSub(d.Handicap, d.FollowersCount);

            // POINTS is the PLAYLIFE profile's own total, which is the same column the RP balance
            // reads — but it is rendered from PointsService, because that number also folds earns
            // the server has not accepted yet and the two must not disagree on one screen.
            if (_statPoints != null)
                _statPoints.text = PointsService.Instance.HasBalance
                    ? PointsService.Instance.DisplayBalance.ToString("N0", CultureInfo.InvariantCulture)
                    : (d.TotalPoints.HasValue
                        ? d.TotalPoints.Value.ToString("N0", CultureInfo.InvariantCulture)
                        : Unknown);

            if (_statBest != null)
                _statBest.text = d.BestScore.HasValue
                    ? d.BestScore.Value.ToString(CultureInfo.InvariantCulture)
                    : Unknown;

            if (_statTrust != null)
                _statTrust.text = d.TrustLevel.HasValue
                    ? d.TrustLevel.Value.ToString(CultureInfo.InvariantCulture) + "%"
                    : Unknown;

            if (_statAvatar != null)
                _statAvatar.text = d.AvatarLevel.HasValue
                    ? "Lv." + d.AvatarLevel.Value.ToString(CultureInfo.InvariantCulture)
                    : Unknown;
        }

        /// <summary>"HC 22.1 · 1,240 followers", with an em dash for either half that is unknown.</summary>
        private static string FormatSub(double? handicap, int? followers)
        {
            string hc = handicap.HasValue
                ? handicap.Value.ToString("0.0", CultureInfo.InvariantCulture)
                : Unknown;
            string f = followers.HasValue
                ? followers.Value.ToString("N0", CultureInfo.InvariantCulture)
                : Unknown;
            return string.Format(LocalizationManager.Get("GPS_HUB_SUB_FORMAT"), hc, f);
        }

        /// <summary>
        /// Re-resolve every string this screen formats itself. The static labels ride
        /// <see cref="LocalizedText"/>, which has its own subscription; these are the ones built
        /// with <c>string.Format(LocalizationManager.Get(...))</c> at bind time and would otherwise
        /// stay in the language they were bound in.
        /// </summary>
        private void OnLanguageChanged()
        {
            ApplyDetail(UserService.Instance.LastDetail);
            if (_playerSub != null && UserService.Instance.LastDetail == null)
                _playerSub.text = FormatSub(null, null);
            ShowRounds(_lastRows);
        }

        private void OnDisplayBalanceChanged(int display)
        {
            if (_statPoints != null)
                _statPoints.text = display.ToString("N0", CultureInfo.InvariantCulture);
        }

        private void OnDetailResult(ApiResult<UserDetailDto> result)
        {
            // The success path already ran through UserService.OnDetailChanged. Only the failure
            // is this callback's business, and it is a single Warning with no toast: a hub whose
            // hero shows "—" is degraded, not broken, and a modal over it would be worse.
            if (result == null || result.Success) return;
            Debug.LogWarning($"{Tag} /user/detail failed ({result.ErrorKind}) — hero stats stay '{Unknown}'.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // My recent rounds
        // ═════════════════════════════════════════════════════════════════════

        private void OnHistoryResult(ApiResult<List<ActivityDto>> result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                    Debug.LogWarning($"{Tag} /score/history failed ({result.ErrorKind}) — rounds panel hidden.");
                ShowRounds(null);
                return;
            }

            ShowRounds(result.Data);
        }

        /// <summary>
        /// Bind up to three rows. With no rounds the panel STAYS UP and shows a one-line empty
        /// state: hiding it made a brand-new player's hub look broken — a headline with nothing
        /// under it is better than a hole in the layout where a panel should be.
        /// </summary>
        private void ShowRounds(List<ActivityDto>? rows)
        {
            _lastRows = rows;
            int count = 0;
            if (rows != null)
            {
                foreach (ActivityDto r in rows)
                {
                    if (r == null) continue;
                    if (count >= _roundRows.Length) break;
                    GpsHubRoundRow row = _roundRows[count];
                    if (row == null) continue;
                    row.gameObject.SetActive(true);
                    row.Bind(r, IsBest(r));
                    count++;
                }
            }

            for (int i = count; i < _roundRows.Length; i++)
                if (_roundRows[i] != null) _roundRows[i].gameObject.SetActive(false);

            if (_roundsEmpty != null)
            {
                _roundsEmpty.gameObject.SetActive(count == 0);
                if (count == 0) _roundsEmpty.text = LocalizationManager.Get("GPS_HUB_NO_ROUNDS");
            }
            if (_roundsPanel != null) _roundsPanel.SetActive(true);
        }

        /// <summary>
        /// The BEST tag marks the row whose score EQUALS the profile's <c>best_score</c> — the
        /// server's own number, never a max recomputed over the three rows on screen, which would
        /// tag a mediocre round "BEST" whenever the real best fell off the page.
        /// </summary>
        private static bool IsBest(ActivityDto row)
        {
            UserDetailDto? d = UserService.Instance.LastDetail;
            return d != null && d.BestScore.HasValue && row.Score.HasValue &&
                   row.Score.Value == d.BestScore.Value;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Navigation
        // ═════════════════════════════════════════════════════════════════════

        private void OnBackClicked()
        {
            // GoBack pops the same-pillar history first; Home is the fallback for the case where
            // the hub was opened from a screen that cleared it (ShowScreen from Home does).
            ScreenManager.Instance?.GoBack(ScreenId.Home);
        }
    }
}
