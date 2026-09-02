// gps_profile_pack §5.3 — one badge cell in the Badges screen grid.
#nullable enable
using Golfin.Gps;
using Golfin.UI.Polish;
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

        // ═════════════════════════════════════════════════════════════════════
        // gps_polish §D7 — the newly-earned pulse
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>The runtime glow overlay's name.</summary>
        private const string GlowName = "EarnedGlow";

        private Coroutine? _pulse;

        /// <summary>
        /// Two glow cycles on a badge that flipped to earned between two paints (§D7).
        ///
        /// <para>WHICH badge is new is not a question this cell can answer: the section view
        /// destroys and re-instantiates every cell on each paint, so a cell has no memory of a
        /// previous one. <see cref="GpsBadgesScreenController"/> keeps the earned-id set across
        /// paints and calls this on exactly the cells that changed.</para>
        ///
        /// <para>The glow is a runtime child that rests at alpha 0 — nothing is authored, and a
        /// cell that never earns anything is pixel-identical to HEAD.</para>
        /// </summary>
        public void PlayEarnedPulse()
        {
            Image? glow = EnsureGlow();
            if (glow == null) return;

            var cg = glow.GetComponent<CanvasGroup>();
            if (cg == null) cg = glow.gameObject.AddComponent<CanvasGroup>();
            glow.gameObject.SetActive(true);
            UiMotion.Run(this, ref _pulse, UiMotion.Pulse(cg, 0f, 1f, cycles: 2));
        }

        private Image? EnsureGlow()
        {
            Transform? t = transform.Find(GlowName);
            if (t != null) return t.GetComponent<Image>();

            var go = new GameObject(GlowName, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            if (_border != null)
            {
                img.sprite                 = _border.sprite;
                img.type                   = _border.type;
                img.pixelsPerUnitMultiplier = _border.pixelsPerUnitMultiplier;
            }
            img.color         = GpsUiColor.Gold;
            img.raycastTarget = false;
            go.GetComponent<CanvasGroup>().alpha = 0f;
            return img;
        }

        private void OnDisable()
        {
            UiMotion.Stop(this, ref _pulse);
        }

        private static void SetText(TextMeshProUGUI? t, string value)
        { if (t != null) t.text = value; }
    }
}
