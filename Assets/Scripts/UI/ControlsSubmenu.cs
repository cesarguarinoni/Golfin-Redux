using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI
{
    /// <summary>
    /// Settings › Controls submenu — Flick / Pendulum / Tap Timing / Free Swing
    /// (control_scheme_seam §3.4).
    ///
    /// <para>A 1:1 copy of <see cref="GraphicsSubmenu"/>'s shape, which is itself a copy of
    /// <see cref="LanguageSubmenu"/>: four rows, selection carried entirely by the ROW FILL,
    /// no tick and no radio, and a re-sync on every accordion open (while collapsed this object
    /// is inactive and misses any change fired in the meantime).</para>
    ///
    /// <para>This deliberately departs from the Figma frame, which draws a radio button on the
    /// right of each row. Cesar, 2026-09-05: the Controls rows must match the settings menu they
    /// live in, not the frame — every other submenu in this screen selects with the blue
    /// rectangle, and one row that selected differently would read as a different control.
    /// Language is the reference.</para>
    ///
    /// <para>The value lives in <see cref="Golfin.Gameplay.UI.Controls.ControlSchemeService"/> —
    /// PlayerPrefs, like volume, language and quality tier. This view only reads and writes it.</para>
    /// </summary>
    public class ControlsSubmenu : MonoBehaviour
    {
        [Header("Scheme Buttons")]
        [SerializeField] private Button flickButton;
        [SerializeField] private Button pendulumButton;
        [SerializeField] private Button tapTimingButton;
        [SerializeField] private Button freeSwingButton;

        [Header("Row Colors")]
        [SerializeField] private Color selectedColor   = new Color32(0x33, 0x99, 0xFF, 0xFF);
        [SerializeField] private Color unselectedColor = new Color32(0x26, 0x42, 0x5F, 0xFF);

        private void Awake()
        {
            if (flickButton     != null) flickButton    .onClick.AddListener(() => OnSchemeSelected(Gameplay.UI.Controls.ControlScheme.Flick));
            if (pendulumButton  != null) pendulumButton .onClick.AddListener(() => OnSchemeSelected(Gameplay.UI.Controls.ControlScheme.Pendulum));
            if (tapTimingButton != null) tapTimingButton.onClick.AddListener(() => OnSchemeSelected(Gameplay.UI.Controls.ControlScheme.Needle));
            if (freeSwingButton != null) freeSwingButton.onClick.AddListener(() => OnSchemeSelected(Gameplay.UI.Controls.ControlScheme.FreeSwing));
        }

        private void OnEnable()
        {
            Gameplay.UI.Controls.ControlSchemeService.OnSchemeChanged += OnSchemeChangedExternally;
            UpdateUI();
        }

        private void OnDisable()
        {
            Gameplay.UI.Controls.ControlSchemeService.OnSchemeChanged -= OnSchemeChangedExternally;
        }

        /// <summary>The in-game gear modal writes the same value; this keeps both surfaces
        /// showing one truth.</summary>
        private void OnSchemeChangedExternally(Gameplay.UI.Controls.ControlScheme scheme)
        {
            UpdateUI();
            Debug.Log($"[ControlsSubmenu] Scheme changed externally to: {scheme}");
        }

        private void OnSchemeSelected(Gameplay.UI.Controls.ControlScheme scheme)
        {
            if (Gameplay.UI.Controls.ControlSchemeService.Current == scheme)
            {
                Debug.Log($"[ControlsSubmenu] Scheme already selected: {scheme}");
                return;
            }

            Gameplay.UI.Controls.ControlSchemeService.Set(scheme, "settings");

            UpdateUI();   // Set() is silent when the value did not move; repaint regardless.
            Debug.Log($"[ControlsSubmenu] Scheme set to {scheme}.");
        }

        private void UpdateUI()
        {
            var current = Gameplay.UI.Controls.ControlSchemeService.Current;

            // Selection is carried entirely by the row fill — no tick, no radio (LanguageSubmenu).
            UpdateButtonColor(flickButton,     current == Gameplay.UI.Controls.ControlScheme.Flick);
            UpdateButtonColor(pendulumButton,  current == Gameplay.UI.Controls.ControlScheme.Pendulum);
            UpdateButtonColor(tapTimingButton, current == Gameplay.UI.Controls.ControlScheme.Needle);
            UpdateButtonColor(freeSwingButton, current == Gameplay.UI.Controls.ControlScheme.FreeSwing);
        }

        private void UpdateButtonColor(Button button, bool isSelected)
        {
            if (button == null) return;

            Image buttonImage = button.GetComponent<Image>();
            if (buttonImage != null)
            {
                buttonImage.color = isSelected ? selectedColor : unselectedColor;
            }
        }
    }
}
