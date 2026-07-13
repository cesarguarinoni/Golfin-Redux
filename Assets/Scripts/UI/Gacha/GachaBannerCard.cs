// Assets/Scripts/UI/Gacha/GachaBannerCard.cs
// gacha_screen Stage 2 — §3c Bind script for GachaBannerCard.prefab
// Binds one GachaBannerEntry to all visual slots on the card.
// Pull buttons are stub (Stage 2 — ToastController "Coming soon").
// Rules button → Application.OpenURL(rulesUrl).
// CountdownLabel is updated each frame by GachaCarouselController (no coroutine here).

using Golfin.UI.Toast;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Bind script attached to GachaBannerCard.prefab.
    /// Bind() must be called once after spawn with the catalog entry.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GachaBannerCard : MonoBehaviour
    {
        // ── SerializeField refs — wired by GachaCarouselController.SetupCardRefs() ──

        [SerializeField] private Image            _artImage;
        [SerializeField] private TextMeshProUGUI  _titleText;
        [SerializeField] private TextMeshProUGUI  _countdownLabel;
        [SerializeField] private TextMeshProUGUI  _costX1Text;
        [SerializeField] private TextMeshProUGUI  _costX10Text;
        [SerializeField] private Button           _pullX1Button;
        [SerializeField] private Button           _pullX10Button;
        [SerializeField] private Button           _rulesButton;

        // ── Runtime state ──────────────────────────────────────────────────────

        private GachaBannerEntry _entry;

        /// <summary>The catalog entry this card is bound to (read by GachaCarouselController).</summary>
        public GachaBannerEntry Entry => _entry;

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Bind the card to a catalog entry. Resolves the art sprite from Resources,
        /// sets title + cost labels, wires pull stubs and rules button.
        /// </summary>
        public void Bind(GachaBannerEntry entry)
        {
            _entry = entry;

            // --- Title ---
            if (_titleText != null)
                // TODO: swap nameKey to loc-keys when localization_audit 353 wires LocalizationManager
                // v1: nameKey column carries display text (e.g. "STANDARD CLUB 1"); no loc lookup yet.
                _titleText.text = entry.NameKey;

            // --- Art ---
            if (_artImage != null)
            {
                var spr = Resources.Load<Sprite>("Art/Gacha/Banners/" + entry.ArtSprite);
                if (spr != null)
                    _artImage.sprite = spr;
                else
                    Debug.LogWarning($"[GachaBannerCard] Art sprite not found: Art/Gacha/Banners/{entry.ArtSprite}");
            }

            // --- Costs ---
            // Design (Figma 4065:6730 + Cesar tuning) shows the literal label "COST" next to the
            // ticket icon and "x1"/"x10" — the numeric CostX1/CostX10 are NOT surfaced on the card in v1
            // (reserved for the real-pull spend flow later). Leave the authored "COST" label untouched.
            // (fields kept wired for a future numeric-cost variant.)

            // --- Pull stubs ---
            if (_pullX1Button != null)
            {
                _pullX1Button.onClick.RemoveAllListeners();
                _pullX1Button.onClick.AddListener(OnPullX1);
            }
            if (_pullX10Button != null)
            {
                _pullX10Button.onClick.RemoveAllListeners();
                _pullX10Button.onClick.AddListener(OnPullX10);
            }

            // --- Rules button ---
            if (_rulesButton != null)
            {
                _rulesButton.onClick.RemoveAllListeners();
                _rulesButton.onClick.AddListener(OnRules);
            }
        }

        /// <summary>Called each frame by GachaCarouselController to update the countdown display.</summary>
        public void SetCountdownText(string text)
        {
            if (_countdownLabel != null)
                _countdownLabel.text = text;
        }

        // ── Private handlers ───────────────────────────────────────────────────

        private void OnPullX1()
        {
            Debug.Log($"[GachaBannerCard] Pull x1 tapped on {_entry?.BannerId} — stub (Stage 2)");
            ToastController.Instance?.Show("Coming soon");
        }

        private void OnPullX10()
        {
            Debug.Log($"[GachaBannerCard] Pull x10 tapped on {_entry?.BannerId} — stub (Stage 2)");
            ToastController.Instance?.Show("Coming soon");
        }

        private void OnRules()
        {
            if (_entry != null && !string.IsNullOrEmpty(_entry.RulesUrl))
            {
                Debug.Log($"[GachaBannerCard] Opening rules URL: {_entry.RulesUrl}");
                Application.OpenURL(_entry.RulesUrl);
            }
        }
    }
}
