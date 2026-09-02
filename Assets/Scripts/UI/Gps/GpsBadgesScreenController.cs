// gps_profile_pack §5.3 — GPS Badges screen (Figma 14027:33298).
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Gps;
using Golfin.Net;
using Golfin.UI.Polish;
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

        /// <summary>§D3/§D8 — cache-vs-fetch memory for the badge grid.</summary>
        private readonly PaintGate _gate = new PaintGate(Tag, "badges");

        /// <summary>
        /// The ids that were EARNED at the previous paint, so §D7's "flips true between two
        /// paints" is a real comparison and not a guess. Null until the first paint: the very
        /// first list a player ever sees must not pulse every badge they already had.
        /// </summary>
        private HashSet<string>? _earnedBefore;

        /// <summary>Every cell created by the last paint, in SECTION order — which is not the
        /// server's order, so the row it was bound from is kept alongside it rather than looked
        /// up by index into the raw list.</summary>
        private readonly List<BadgeCellView>     _painted     = new List<BadgeCellView>();
        private readonly List<BadgeProgressDto>  _paintedDtos = new List<BadgeProgressDto>();

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

            _gate.Rearm();
            if (BadgeService.Instance.HasData)
                BindBadges(BadgeService.Instance.LastBadges, PaintKind.Cache);
            else
            {
                ShowPlaceholders();
                _gate.Should(PaintKind.Cache, 0);
                GpsPaintMotion.Shimmer(gameObject, ShimmerHost.Badges, _gate.IsCold);
            }

            // Always fire live fetch (copy GpsHubScreenController:128-136 pattern).
            //
            // WITH A CALLBACK, and that is the fix for a real defect this task's own placeholder
            // exposed. `FetchBadges()` fires OnBadgesChanged only on SUCCESS, so a failed or
            // empty answer repainted nothing — and once §D8 put a shimmer over the grid, "nothing"
            // stopped being invisible: the placeholder swept forever over a screen that was never
            // going to fill. Every other GPS fetch site already routes its FAILURE arm back into
            // the paint (the hub's /score/history, discover, supporters, /vote/list); this was the
            // one that did not.
            var client = ApiClient.Instance;
            client.Run(BadgeService.Instance.FetchBadges(OnBadgesFetched));
        }

        private void OnDisable()
        {
            BadgeService.Instance.OnBadgesChanged -= OnBadgesChanged;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Data binding
        // ═══════════════════════════════════════════════════════════════════

        private void OnBadgesChanged() => BindBadges(BadgeService.Instance.LastBadges, PaintKind.Fetch);

        /// <summary>
        /// The badge fetch ANSWERED — successfully or not. A success has already repainted through
        /// <see cref="OnBadgesChanged"/>; every other outcome has to spend the gate here, or the
        /// screen keeps a loading state it can never leave.
        /// </summary>
        private void OnBadgesFetched(ApiResult<List<BadgeProgressDto>> result)
        {
            if (result != null && result.Success && result.Data != null) return;

            Debug.LogWarning($"{Tag} /badges/progress did not answer with a list " +
                             $"({(result != null ? result.ErrorKind.ToString() : "no result")}) — " +
                             "placeholder cleared, grid left as it stands.");
            BindBadges(BadgeService.Instance.LastBadges, PaintKind.Fetch);
        }

        private void BindBadges(List<BadgeProgressDto>? badges, PaintKind kind)
        {
            if (badges == null)
            {
                ShowPlaceholders();
                _gate.Should(kind, 0);
                GpsPaintMotion.Shimmer(gameObject, ShimmerHost.Badges, _gate.IsCold);
                return;
            }

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

            _painted.Clear();
            _paintedDtos.Clear();
            PopulateSection(_sectionGolf,    bySection["GOLF"]);
            PopulateSection(_sectionSocial,  bySection["SOCIAL"]);
            PopulateSection(_sectionTrust,   bySection["TRUST"]);
            PopulateSection(_sectionSpecial, bySection["SPECIAL"]);

            bool stagger = _gate.Should(kind, _painted.Count);
            GpsPaintMotion.Shimmer(gameObject, ShimmerHost.Badges, _gate.IsCold);

            PulseNewlyEarned(badges);

            if (stagger)
            {
                var rows = new List<Transform>(_painted.Count);
                foreach (BadgeCellView c in _painted) if (c != null) rows.Add(c.transform);
                GpsPaintMotion.StaggerRise(this, rows);
            }
        }

        /// <summary>
        /// §D7 — pulse the cells whose <c>earned</c> flipped true since the previous paint.
        ///
        /// <para>Skipped on the FIRST paint of a session (<see cref="_earnedBefore"/> null): with
        /// no previous set every earned badge looks new, and a player opening the screen would
        /// watch their whole collection light up as if they had just won it.</para>
        ///
        /// <para>It therefore never coincides with the stagger: the stagger runs on the first
        /// cold fetch paint, which is exactly the paint this returns early from. A cell cannot be
        /// at alpha 0 and pulsing at once.</para>
        /// </summary>
        private void PulseNewlyEarned(List<BadgeProgressDto> badges)
        {
            var now = new HashSet<string>(StringComparer.Ordinal);
            foreach (var b in badges)
            {
                string id = b.Id ?? b.NameKey ?? string.Empty;
                if (b.Earned && id.Length > 0) now.Add(id);
            }

            HashSet<string>? before = _earnedBefore;
            _earnedBefore = now;
            if (before == null) return;

            int pulsed = 0;
            for (int i = 0; i < _painted.Count && i < _paintedDtos.Count; i++)
            {
                BadgeCellView cell = _painted[i];
                BadgeProgressDto b = _paintedDtos[i];
                string id = b.Id ?? b.NameKey ?? string.Empty;
                if (cell == null || !b.Earned || id.Length == 0 || before.Contains(id)) continue;
                cell.PlayEarnedPulse();
                pulsed++;
            }
            if (pulsed > 0) Debug.Log($"{Tag} {pulsed} newly earned badge(s) pulsed.");
        }

        private void PopulateSection(GpsBadgeSectionView? section, List<BadgeProgressDto> items)
        {
            if (section == null) return;
            section.Populate(items, _badgeCellPrefab, _painted, _paintedDtos);
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

        public void Populate(List<BadgeProgressDto> items, BadgeCellView? prefab,
                             List<BadgeCellView>? painted = null,
                             List<BadgeProgressDto>? paintedDtos = null)
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
                painted?.Add(cell);
                paintedDtos?.Add(item);
            }
        }
    }
}
