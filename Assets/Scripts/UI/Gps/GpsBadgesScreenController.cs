// gps_profile_pack §5.3 — GPS Badges screen (Figma 14027:33298).
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Gps;
using Golfin.Net;
using Golfin.Telemetry;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class GpsBadgesScreenController : MonoBehaviour
    {
        private const string Tag     = "[GpsBadges]";
        private const string Unknown = "—";

        // ── Collection panel ──────────────────────────────────────────────────
        [Header("Collection panel")]
        [SerializeField] private TextMeshProUGUI? _collectionPct;
        [SerializeField] private Image?           _collectionTrackFill;
        [SerializeField] private TextMeshProUGUI? _collectionEarned;

        // ── Sections ──────────────────────────────────────────────────────────
        [Header("Sections")]
        [SerializeField] private GpsBadgeSectionView? _sectionGolf;
        [SerializeField] private GpsBadgeSectionView? _sectionSocial;
        [SerializeField] private GpsBadgeSectionView? _sectionTrust;
        [SerializeField] private GpsBadgeSectionView? _sectionSpecial;

        [Header("Badge cell prefab")]
        [SerializeField] private BadgeCellView? _badgeCellPrefab;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private Button? _backButton;

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
                    ScreenManager.Instance?.GoBack(ScreenId.GpsProfile));
        }

        private void OnEnable()
        {
            TelemetryService.Instance.RecordSafe("gps_badges_open", () => null);
            BadgeService.Instance.OnBadgesChanged += OnBadgesChanged;

            if (BadgeService.Instance.HasData)
                BindBadges(BadgeService.Instance.LastBadges);
            else
                ShowPlaceholders();

            // Always fire live fetch (copy GpsHubScreenController:128-136 pattern)
            var client = ApiClient.Instance;
            client.Run(BadgeService.Instance.FetchBadges());
        }

        private void OnDisable()
        {
            BadgeService.Instance.OnBadgesChanged -= OnBadgesChanged;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Data binding
        // ═══════════════════════════════════════════════════════════════════

        private void OnBadgesChanged() => BindBadges(BadgeService.Instance.LastBadges);

        private void BindBadges(List<BadgeProgressDto>? badges)
        {
            if (badges == null) { ShowPlaceholders(); return; }

            int earned = 0;
            foreach (var b in badges) if (b.Earned) earned++;

            int total    = badges.Count;
            float pct    = total > 0 ? (earned * 100f / total) : 0f;

            SetText(_collectionPct,    $"{pct:0}%");
            SetText(_collectionEarned, $"{earned} / {total} badges earned");
            if (_collectionTrackFill != null)
                GpsUiColor.SetBarFill(_collectionTrackFill, Mathf.Clamp01(pct / 100f));

            var bySection = new Dictionary<string, List<BadgeProgressDto>>(StringComparer.OrdinalIgnoreCase)
            {
                ["GOLF"]    = new List<BadgeProgressDto>(),
                ["SOCIAL"]  = new List<BadgeProgressDto>(),
                ["TRUST"]   = new List<BadgeProgressDto>(),
                ["SPECIAL"] = new List<BadgeProgressDto>(),
            };
            foreach (var b in badges)
            {
                string sec = (b.Section ?? "GOLF").ToUpperInvariant();
                if (!bySection.ContainsKey(sec))
                    bySection[sec] = new List<BadgeProgressDto>();
                bySection[sec].Add(b);
            }

            PopulateSection(_sectionGolf,    bySection["GOLF"]);
            PopulateSection(_sectionSocial,  bySection["SOCIAL"]);
            PopulateSection(_sectionTrust,   bySection["TRUST"]);
            PopulateSection(_sectionSpecial, bySection["SPECIAL"]);
        }

        private void PopulateSection(GpsBadgeSectionView? section, List<BadgeProgressDto> items)
        {
            if (section == null) return;
            section.Populate(items, _badgeCellPrefab);
        }

        private void ShowPlaceholders()
        {
            SetText(_collectionPct,    Unknown);
            SetText(_collectionEarned, Unknown);
            if (_collectionTrackFill != null) GpsUiColor.SetBarFill(_collectionTrackFill, 0f);
        }

        private static void SetText(TextMeshProUGUI? t, string value)
        { if (t != null) t.text = value; }
    }

    // ── Section container view ────────────────────────────────────────────────

    /// <summary>
    /// One section panel (GOLF / SOCIAL / TRUST / SPECIAL) in the Badges screen.
    /// Holds a <see cref="GridLayoutGroup"/> or <see cref="HorizontalLayoutGroup"/> container
    /// that is populated at runtime by instantiating <see cref="BadgeCellView"/> cells.
    /// </summary>
    [Serializable]
    public sealed class GpsBadgeSectionView
    {
        [SerializeField] public Transform? CellContainer;

        private readonly List<BadgeCellView> _live = new List<BadgeCellView>();

        public void Populate(List<BadgeProgressDto> items, BadgeCellView? prefab)
        {
            if (CellContainer == null || prefab == null) return;

            // Nothing to show: leave the builder's seeded grid standing. An empty container reads
            // as a broken screen, and the seeded cells are the design's own placeholder layout.
            if (items == null || items.Count == 0) return;

            // Clear EVERY child, not just the ones this method made. The builder seeds a full grid
            // into this container for the fidelity pass, and destroying only `_live` left those
            // seeded cells on screen underneath the real ones — which is why the live screen showed
            // raw BADGE_*_NAME keys and a seeded "first two earned" state.
            for (int i = CellContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(CellContainer.GetChild(i).gameObject);
            _live.Clear();

            foreach (var item in items)
            {
                var cell = UnityEngine.Object.Instantiate(prefab, CellContainer);
                cell.Bind(item);
                _live.Add(cell);
            }
        }
    }
}
