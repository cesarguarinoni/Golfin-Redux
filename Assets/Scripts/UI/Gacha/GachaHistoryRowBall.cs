// Assets/Scripts/UI/Gacha/GachaHistoryRowBall.cs
// Binds a ball-type GachaHistoryRecord into the GachaHistoryRowBall prefab.
// Stage 1: populates BallCard portrait + stats (BallSegmentedBar per stat), metadata, currency.
// The prefab hierarchy is cloned from Stage 0 — do NOT rebuild, only bind.
#nullable enable
using System.Linq;
using Golfin.Inventory;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Binder for a ball-type GachaHistoryRowBall prefab instance.
    /// Must be attached to the root of GachaHistoryRowBall.prefab.
    /// </summary>
    public class GachaHistoryRowBall : MonoBehaviour
    {
        [Header("Col1 — Ball card")]
        [SerializeField] private RectTransform? _ballCard;          // The BallCard rect
        [SerializeField] private Image?          _portrait;
        [SerializeField] private TMP_Text?       _nameLabel;
        [SerializeField] private TMP_Text?       _amountBadgeText;

        [Header("Col1 — Ball stat bars (BallSegmentedBar will be added at runtime)")]
        [Tooltip("Assign the 5 bar Images in order: power, rebound, windResistance, roll, spin")]
        [SerializeField] private Image[] _statBarImages = System.Array.Empty<Image>();
        [Tooltip("Assign the 5 StatValue TMPs in order: power, rebound, windResistance, roll, spin — shows numeric '+N' / '-N'")]
        [SerializeField] private TMP_Text[] _statLabels  = System.Array.Empty<TMP_Text>();

        [Header("Col2 — Metadata")]
        [Tooltip("Line_0: ball name, Line_1: quantity, Line_2: date, Line_3: time, Line_4: banner, Line_5: pulls")]
        [SerializeField] private TMP_Text[] _metaLines = System.Array.Empty<TMP_Text>();

        [Header("Col3 — Currency")]
        [SerializeField] private TMP_Text? _ticketLabel;
        [SerializeField] private Image?    _ticketIcon;

        private const int BALL_STAT_MAX = 10;

        // ── Bind ─────────────────────────────────────────────────────────────────

        public void Bind(GachaHistoryRecord record)
        {
            if (record.RewardType != GachaRewardType.Ball)
            {
                Debug.LogWarning($"[GachaHistoryRowBall] Bind called with {record.RewardType} — expected Ball. Row: {name}");
                return;
            }

            var template = BallDatabaseCSV.Instance?.GetBall(record.RewardId);
            BindBallCard(record, template);
            BindMetadata(record, template);
            BindCurrency(record);
        }

        // ── Ball card ─────────────────────────────────────────────────────────────

        private void BindBallCard(GachaHistoryRecord record, BallDataRuntime? template)
        {
            if (template == null)
            {
                Debug.LogWarning($"[GachaHistoryRowBall] Ball not found: {record.RewardId}");
                return;
            }

            // Portrait
            if (_portrait != null && template.thumbnailSprite != null)
                _portrait.sprite = template.thumbnailSprite;

            // Name (uppercase to match club card convention)
            if (_nameLabel != null)
                _nameLabel.text = template.name.ToUpper();

            // Amount badge: "x{qty}" right-aligned, inside card
            if (_amountBadgeText != null)
                _amountBadgeText.text = $"x{record.Quantity}";

            // Stats — BallSegmentedBar requires Image component (RequireComponent).
            // The 5 Images are authored in the prefab; we add BallSegmentedBar at runtime.
            int[] statValues = new[]
                { template.power, template.rebound, template.windResistance, template.roll, template.spin };

            for (int i = 0; i < _statBarImages.Length && i < statValues.Length; i++)
            {
                var barImage = _statBarImages[i];
                if (barImage == null) continue;

                var seg = barImage.GetComponent<BallSegmentedBar>();
                if (seg == null) seg = barImage.gameObject.AddComponent<BallSegmentedBar>();
                seg.SetValue(statValues[i], BALL_STAT_MAX);
            }

            // Stat value labels — show numeric "+N" / "-N" (the new layout is [icon][bar][value])
            for (int i = 0; i < _statLabels.Length && i < statValues.Length; i++)
            {
                if (_statLabels[i] != null)
                {
                    int v = statValues[i];
                    _statLabels[i].text = (v >= 0 ? "+" : "") + v.ToString();
                }
            }
        }

        // ── Metadata lines ────────────────────────────────────────────────────────

        private void BindMetadata(GachaHistoryRecord record, BallDataRuntime? template)
        {
            // Line_0: ball name (uppercase, matching club card convention)
            string ballName = template != null ? template.name.ToUpper() : record.RewardId.ToUpper();

            // Line_1: quantity — mirrors club row's rarity-badge line position
            string quantity = $"x{record.Quantity}";

            // Line_2 / Line_3: date & time — SAME format as club row
            string date = "";
            string time = "";
            if (!string.IsNullOrEmpty(record.PulledUtc))
            {
                if (System.DateTime.TryParse(record.PulledUtc,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var dt))
                {
                    date = "PULLED " + dt.ToString("yyyy/MM/dd");
                    time = dt.ToString("hh:mm:ss tt").ToUpper();
                }
            }

            string bannerName = "";
            var bannerEntry = GachaBannerCatalog.Entries.FirstOrDefault(e => e.BannerId == record.BannerId);
            if (bannerEntry != null)
                bannerName = bannerEntry.NameKey;

            // Line_5: pull count — SAME format as club row
            string pulls = "PULLS: " + record.PullCount;

            SetLine(0, ballName);
            SetLine(1, quantity);
            SetLine(2, date);
            SetLine(3, time);
            SetLine(4, bannerName);
            SetLine(5, pulls);
        }

        private void SetLine(int index, string text)
        {
            if (index < _metaLines.Length && _metaLines[index] != null)
                _metaLines[index].text = text;
        }

        // ── Currency ──────────────────────────────────────────────────────────────

        private void BindCurrency(GachaHistoryRecord record)
        {
            // _ticketLabel shows static "TICKET" text set in the prefab; do NOT overwrite it.

            if (_ticketIcon != null)
            {
                var entry = TicketCatalog.Get(record.TicketType);
                if (entry != null && !string.IsNullOrEmpty(entry.IconSprite))
                {
                    var spr = Resources.Load<Sprite>(entry.IconSprite);
                    if (spr != null) _ticketIcon.sprite = spr;
                }
            }
        }
    }
}
