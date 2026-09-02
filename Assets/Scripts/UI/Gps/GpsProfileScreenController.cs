// gps_profile_pack §5.1 — GPS Profile screen (Figma 14025:33087).
#nullable enable
using System;
using System.Collections;
using Golfin.Economy;
using Golfin.Gps;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class GpsProfileScreenController : MonoBehaviour
    {
        private const string Tag     = "[GpsProfile]";
        private const string Unknown = "—";

        // ── Hero panel ────────────────────────────────────────────────────────
        [Header("Hero panel")]
        [SerializeField] private TextMeshProUGUI? _avatarInitial;
        [SerializeField] private TextMeshProUGUI? _playerName;
        [SerializeField] private TextMeshProUGUI? _playerSub;
        [SerializeField] private TextMeshProUGUI? _statFollowers;
        [SerializeField] private TextMeshProUGUI? _statRounds;
        [SerializeField] private TextMeshProUGUI? _statAvatar;
        [SerializeField] private TextMeshProUGUI? _statPoints;

        // ── Trust panel ────────────────────────────────────────────────────────
        [Header("Trust panel")]
        [SerializeField] private TextMeshProUGUI? _trustLevel;
        [SerializeField] private Image?           _trustTrackFill;

        // ── Quick stats ────────────────────────────────────────────────────────
        [Header("Quick stats")]
        [SerializeField] private TextMeshProUGUI? _statBest;
        [SerializeField] private TextMeshProUGUI? _statAvgScore;
        // AVG PUTTS is always "—" (no putts data) — deviation documented

        // ── Gift totals ────────────────────────────────────────────────────────
        [Header("Gift totals")]
        [SerializeField] private TextMeshProUGUI? _giftsReceived;
        // GIFTS SENT is always "—" (deviation)

        // ── Shortcuts ─────────────────────────────────────────────────────────
        [Header("Shortcuts")]
        [SerializeField] private TextMeshProUGUI? _badgesShortcutSub;
        [SerializeField] private TextMeshProUGUI? _avatarShortcutSub;
        [SerializeField] private Button?          _badgesShortcutButton;
        [SerializeField] private Button?          _avatarShortcutButton;
        [SerializeField] private Button?          _giftShopButton;     // inert v1

        // ── Recent rounds ─────────────────────────────────────────────────────
        [Header("Recent rounds")]
        [SerializeField] private GameObject?      _roundsPanel;
        [SerializeField] private GpsHubRoundRow[] _roundRows = new GpsHubRoundRow[0];
        /// <summary>One-line empty state INSIDE the rounds panel. Cesar, 2026-09-02: the panel
        /// stays up with "no rounds yet" rather than vanishing, exactly as the hub does — an
        /// absent panel reads as a broken screen, not as an empty one.</summary>
        [SerializeField] private TextMeshProUGUI? _roundsEmpty;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button? _backButton;
        [SerializeField] private Button? _editProfileButton;  // inert v1

        private bool _wiredOnce;

        // ═══════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            WireOnce();
        }

        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            if (_backButton != null)
                _backButton.onClick.AddListener(() =>
                    ScreenManager.Instance?.GoBack(ScreenId.GpsHub));

            if (_badgesShortcutButton != null)
                _badgesShortcutButton.onClick.AddListener(() =>
                    ScreenManager.Instance?.ShowScreen(ScreenId.GpsBadges));

            if (_avatarShortcutButton != null)
                _avatarShortcutButton.onClick.AddListener(() =>
                    ScreenManager.Instance?.ShowScreen(ScreenId.GpsAvatar));

            if (_giftShopButton != null)
                _giftShopButton.onClick.AddListener(() =>
                    Debug.Log($"{Tag} Gift shop tapped — inert v1"));

            if (_editProfileButton != null)
                _editProfileButton.interactable = false;
        }

        private void OnEnable()
        {
            TelemetryService.Instance.RecordSafe("gps_profile_open", () => null);

            UserService.Instance.OnDetailChanged         += OnDetailChanged;
            PointsService.Instance.OnDisplayBalanceChanged += OnPointsChanged;

            // Paint from cache immediately
            BindDetail(UserService.Instance.LastDetail);

            // Fire live fetch (copy GpsHubScreenController:128-136 pattern)
            var client = ApiClient.Instance;
            client.Run(UserService.Instance.Detail(r => { if (r.Success) OnDetailChanged(r.Data); }));
        }

        private void OnDisable()
        {
            UserService.Instance.OnDetailChanged         -= OnDetailChanged;
            PointsService.Instance.OnDisplayBalanceChanged -= OnPointsChanged;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Data binding
        // ═══════════════════════════════════════════════════════════════════

        private void OnDetailChanged(UserDetailDto d) => BindDetail(d);
        private void OnPointsChanged(int _) =>
            SetText(_statPoints, PointsService.Instance.DisplayBalance.ToString());

        private void BindDetail(UserDetailDto? d)
        {
            if (d == null) { ShowPlaceholders(); return; }

            SetText(_avatarInitial, string.IsNullOrEmpty(d.DisplayName) ? "?" :
                d.DisplayName.Substring(0, 1).ToUpperInvariant());
            SetText(_playerName, (d.DisplayName ?? Unknown).ToUpperInvariant());

            // v1 sub-line: "HC {handicap}" + "{activities_count} rounds" (no handle/home-course)
            string hc = d.Handicap.HasValue ? $"HC {d.Handicap:0.0}" : Unknown;
            SetText(_playerSub, $"{hc} · {d.ActivitiesCount ?? 0} rounds");

            SetText(_statFollowers, (d.FollowersCount ?? 0).ToString());
            SetText(_statRounds,    (d.ActivitiesCount ?? 0).ToString());
            SetText(_statAvatar,    $"Lv.{d.AvatarLevel ?? 1}");
            SetText(_statPoints,    PointsService.Instance.DisplayBalance.ToString());

            SetText(_trustLevel, d.TrustLevel.HasValue ? $"{d.TrustLevel:0}%" : Unknown);
            if (_trustTrackFill != null)
                GpsUiColor.SetBarFill(_trustTrackFill, d.TrustLevel.HasValue
                    ? Mathf.Clamp01(d.TrustLevel.Value / 100f) : 0f);

            SetText(_giftsReceived, d.GiftPts.HasValue ? $"{d.GiftPts} pts" : Unknown);

            // Avatar shortcut sub-line
            int lv   = d.AvatarLevel ?? 1;
            int xp   = d.AvatarXp   ?? 0;
            int next = 500 * lv;
            SetText(_avatarShortcutSub, $"Lv.{lv} · {xp}/{next} XP");

            StartCoroutine(FetchLiveData());
        }

        private IEnumerator FetchLiveData()
        {
            yield return ScoreStatsService.Instance.FetchStats(r =>
            {
                if (!r.Success) { Debug.LogWarning($"{Tag} /score/stats failed: {r.ErrorMessage}"); return; }
                var s = r.Data;
                SetText(_statBest,     s.BestScore.HasValue ? s.BestScore.Value.ToString("+0;-#;E") : Unknown);
                SetText(_statAvgScore, s.AvgScore.HasValue  ? s.AvgScore.Value.ToString("0.0")      : Unknown);
            });

            yield return BadgeService.Instance.FetchBadges(r =>
            {
                if (!r.Success || r.Data == null) return;
                int earned = 0;
                foreach (var b in r.Data) if (b.Earned) earned++;
                SetText(_badgesShortcutSub, $"{earned} / {r.Data.Count} earned");
            });

            yield return ScoreHistoryService.Instance.History(0, 2, r =>
            {
                int count = (r.Success && r.Data != null) ? r.Data.Count : 0;
                // The panel STAYS UP at zero rows and shows the hub's empty line instead.
                if (_roundsPanel != null) _roundsPanel.SetActive(true);
                if (_roundsEmpty != null)
                {
                    _roundsEmpty.gameObject.SetActive(count == 0);
                    if (count == 0) _roundsEmpty.text = LocalizationManager.Get("GPS_HUB_NO_ROUNDS");
                }
                for (int i = 0; i < _roundRows.Length; i++)
                {
                    bool active = i < count;
                    _roundRows[i].gameObject.SetActive(active);
                    if (active) _roundRows[i].Bind(r.Data![i], false);
                }
            });
        }

        private void ShowPlaceholders()
        {
            SetText(_avatarInitial,    "?");
            SetText(_playerName,       Unknown);
            SetText(_playerSub,        Unknown);
            SetText(_statFollowers,    Unknown);
            SetText(_statRounds,       Unknown);
            SetText(_statAvatar,       Unknown);
            SetText(_statPoints,       Unknown);
            SetText(_trustLevel,       Unknown);
            SetText(_statBest,         Unknown);
            SetText(_statAvgScore,     Unknown);
            SetText(_giftsReceived,    Unknown);
            SetText(_badgesShortcutSub, Unknown);
            SetText(_avatarShortcutSub, Unknown);
            if (_trustTrackFill != null) GpsUiColor.SetBarFill(_trustTrackFill, 0f);
            if (_roundsPanel != null) _roundsPanel.SetActive(true);
            if (_roundsEmpty != null)
            {
                _roundsEmpty.gameObject.SetActive(true);
                _roundsEmpty.text = LocalizationManager.Get("GPS_HUB_NO_ROUNDS");
            }
            for (int i = 0; i < _roundRows.Length; i++)
                if (_roundRows[i] != null) _roundRows[i].gameObject.SetActive(false);
        }

        private static void SetText(TextMeshProUGUI? t, string value)
        { if (t != null) t.text = value; }
    }
}
