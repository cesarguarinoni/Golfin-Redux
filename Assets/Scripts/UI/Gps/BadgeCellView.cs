// gps_profile_pack §5.3 — one badge cell in the Badges screen grid.
#nullable enable
using Golfin.Gps;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Binds a <see cref="BadgeProgressDto"/> to a badge cell in the 4-column grid.
    /// States: earned (white-10 bg, rarity border 2px, green checkmark) / unearned (dark bg, muted border 1px).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BadgeCellView : MonoBehaviour
    {
        [SerializeField] private Image?           _background;
        [SerializeField] private Image?           _border;
        [SerializeField] private TextMeshProUGUI? _checkmark;       // "✓" or ""
        [SerializeField] private TextMeshProUGUI? _rarityLabel;
        [SerializeField] private Image?           _iconRing;
        [SerializeField] private TextMeshProUGUI? _nameLabel;
        [SerializeField] private TextMeshProUGUI? _progressLabel;   // "0%" or ""

        // sRGB computed colours (matches SPEC)
        private static readonly Color EarnedBg    = new Color(1f, 1f, 1f, 0.10f);
        private static readonly Color UnearnedBg  = new Color(0f, 0f, 0f, 0.25f);
        private static readonly Color MutedBorder = GpsUiColor.Hex("#4A5A6E");

        public void Bind(BadgeProgressDto dto)
        {
            bool earned = dto.Earned;
            string rarity = dto.Rarity ?? "COMMON";

            // Background
            if (_background != null)
                _background.color = earned ? EarnedBg : UnearnedBg;

            // Border
            if (_border != null)
            {
                _border.color = earned
                    ? GpsUiColor.RarityBorderColor(rarity)
                    : MutedBorder;
            }

            // Checkmark
            SetText(_checkmark, earned ? "✓" : "");
            if (_checkmark != null)
                _checkmark.color = GpsUiColor.Green;

            // Rarity label
            SetText(_rarityLabel, rarity);
            if (_rarityLabel != null)
                _rarityLabel.color = GpsUiColor.RarityBorderColor(rarity);

            // Icon ring opacity
            if (_iconRing != null)
            {
                var c = _iconRing.color;
                _iconRing.color = new Color(c.r, c.g, c.b, earned ? 1f : 0.60f);
            }

            // Name
            string nameKey = dto.NameKey ?? dto.Id ?? "";
            SetText(_nameLabel, LocalizationManager.Get(nameKey));
            if (_nameLabel != null)
                _nameLabel.color = earned ? Color.white : GpsUiColor.Muted;

            // Progress % — shown only when a target exists
            if (dto.Required > 0 && !earned)
            {
                float pct = dto.Required > 0 ? (dto.Progress * 100f / dto.Required) : 0f;
                SetText(_progressLabel, $"{pct:0}%");
            }
            else
            {
                SetText(_progressLabel, "");
            }
        }

        private static void SetText(TextMeshProUGUI? t, string value)
        { if (t != null) t.text = value; }
    }
}
