using UnityEngine;
using UnityEngine.UI;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2f: compact in-loop green-tuning widget. Two sliders + a reset button.
    /// Toggled via a gear-icon button in the HUD. Independent of DashboardUI
    /// (which stays as the full lab debug pane).
    /// </summary>
    public class GreenTuningPanel : MonoBehaviour
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] GameObject panelRoot;        // The collapsible content panel
        // Wired to the in-game HUD's SettingsButton wheel (ShotUI_Canvas/SettingsButton).
        // This is deliberately the REAL settings wheel, not a dedicated debug icon — the old
        // flat-green "G" square used to sit on top of the wheel and has been removed.
        // Scope: gameplay HUD only. The shell/menu + nav-bar settings buttons are untouched.
        [SerializeField] Button     toggleButton;     // Shows/hides panelRoot
        [SerializeField] Slider     rollingResistanceSlider;
        [SerializeField] Slider     stopSpeedSlider;
        [SerializeField] Text       rollingResistanceLabel;
        [SerializeField] Text       stopSpeedLabel;
        [SerializeField] Button     resetButton;

        // Editor-visible so Cesar can pre-set initial values without entering play mode.
        // L8: NOT persisted across play-mode exit (this is the bool that confirms it).
        [SerializeField] bool _persistEdits = false;

        const float kRollingResistanceMin = 0f;
        const float kRollingResistanceMax = 0.5f;
        const float kStopSpeedMin         = 0f;
        const float kStopSpeedMax         = 0.2f;

        void Awake()
        {
            if (controller == null) controller = FindObjectOfType<PhysicsLabController>();

            if (panelRoot != null) panelRoot.SetActive(false);
            if (toggleButton != null) toggleButton.onClick.AddListener(TogglePanel);
            if (resetButton != null)  resetButton.onClick.AddListener(ResetToDefault);
        }

        void OnEnable()
        {
            // Initialize slider values from current config.
            // Guard: controller may not have initialized SurfaceCfg.Coefficients yet
            // (if GreenTuningPanel.OnEnable fires before PhysicsLabController.Awake).
            if (controller == null) return;
            if (controller.SurfaceCfg.Coefficients == null) return;
            var greenCoef = controller.SurfaceCfg[SurfaceType.Green];
            if (rollingResistanceSlider != null)
            {
                rollingResistanceSlider.minValue = kRollingResistanceMin;
                rollingResistanceSlider.maxValue = kRollingResistanceMax;
                rollingResistanceSlider.SetValueWithoutNotify(greenCoef.RollingResistance.ToFloat());
                rollingResistanceSlider.onValueChanged.AddListener(OnRollingResistanceChanged);
            }
            if (stopSpeedSlider != null)
            {
                stopSpeedSlider.minValue = kStopSpeedMin;
                stopSpeedSlider.maxValue = kStopSpeedMax;
                stopSpeedSlider.SetValueWithoutNotify(greenCoef.StopSpeed.ToFloat());
                stopSpeedSlider.onValueChanged.AddListener(OnStopSpeedChanged);
            }
            UpdateLabels();
        }

        void OnDisable()
        {
            if (rollingResistanceSlider != null) rollingResistanceSlider.onValueChanged.RemoveListener(OnRollingResistanceChanged);
            if (stopSpeedSlider != null)         stopSpeedSlider.onValueChanged.RemoveListener(OnStopSpeedChanged);
        }

        void OnDestroy()
        {
            if (toggleButton != null) toggleButton.onClick.RemoveListener(TogglePanel);
            if (resetButton != null)  resetButton.onClick.RemoveListener(ResetToDefault);
        }

        void TogglePanel()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(!panelRoot.activeSelf);
        }

        void OnRollingResistanceChanged(float value)
        {
            if (controller == null) return;
            // L9 (amended Option B): mirror to both SurfaceConfig[Green] AND PuttConfig[Green].
            // BallSimulation.RunPuttPhase reads PuttConfig[Green] for putts on Green surfaces,
            // so without this mirror the slider has no effect on putt rolling distance.
            var surfCfg  = controller.SurfaceCfg;
            var surfCoef = surfCfg[SurfaceType.Green];
            surfCoef.RollingResistance = fp.FromFloat(value);
            surfCfg.Coefficients[(int)SurfaceType.Green] = surfCoef;
            controller.SetSurfaceConfig(surfCfg);

            var puttCfg  = controller.PuttCfg;
            var puttCoef = puttCfg[SurfaceType.Green];
            puttCoef.RollingResistance = fp.FromFloat(value);
            puttCfg.Coefficients[(int)SurfaceType.Green] = puttCoef;
            controller.SetPuttConfig(puttCfg);

            UpdateLabels();
        }

        void OnStopSpeedChanged(float value)
        {
            if (controller == null) return;
            // L9 (amended Option B): mirror to both SurfaceConfig[Green] AND PuttConfig[Green].
            var surfCfg  = controller.SurfaceCfg;
            var surfCoef = surfCfg[SurfaceType.Green];
            surfCoef.StopSpeed = fp.FromFloat(value);
            surfCfg.Coefficients[(int)SurfaceType.Green] = surfCoef;
            controller.SetSurfaceConfig(surfCfg);

            var puttCfg  = controller.PuttCfg;
            var puttCoef = puttCfg[SurfaceType.Green];
            puttCoef.StopSpeed = fp.FromFloat(value);
            puttCfg.Coefficients[(int)SurfaceType.Green] = puttCoef;
            controller.SetPuttConfig(puttCfg);

            UpdateLabels();
        }

        void ResetToDefault()
        {
            if (controller == null) return;
            // L6: only reset the Green entry; preserve user edits to other surfaces.
            // L9 (amended Option B): reset both SurfaceConfig[Green] and PuttConfig[Green].
            var surfCfg = controller.SurfaceCfg;
            var defaultSurfCfg = SurfaceConfig.Default;
            surfCfg.Coefficients[(int)SurfaceType.Green] = defaultSurfCfg.Coefficients[(int)SurfaceType.Green];
            controller.SetSurfaceConfig(surfCfg);

            var puttCfg = controller.PuttCfg;
            var defaultPuttCfg = PuttConfig.Default;
            puttCfg.Coefficients[(int)SurfaceType.Green] = defaultPuttCfg.Coefficients[(int)SurfaceType.Green];
            controller.SetPuttConfig(puttCfg);

            // Refresh sliders to match (use SurfaceConfig as source of truth for display).
            var greenCoef = surfCfg[SurfaceType.Green];
            if (rollingResistanceSlider != null) rollingResistanceSlider.SetValueWithoutNotify(greenCoef.RollingResistance.ToFloat());
            if (stopSpeedSlider != null)         stopSpeedSlider.SetValueWithoutNotify(greenCoef.StopSpeed.ToFloat());
            UpdateLabels();
        }

        void UpdateLabels()
        {
            if (controller == null) return;
            var greenCoef = controller.SurfaceCfg[SurfaceType.Green];
            if (rollingResistanceLabel != null) rollingResistanceLabel.text = $"Roll Resist: {greenCoef.RollingResistance.ToFloat():F3}";
            if (stopSpeedLabel != null)         stopSpeedLabel.text         = $"Stop Speed: {greenCoef.StopSpeed.ToFloat():F3} m/s";
        }

        // §2f hotkey: 'G' toggles the panel in debug builds.
        void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame)
            {
                TogglePanel();
            }
#endif
        }
    }
}
