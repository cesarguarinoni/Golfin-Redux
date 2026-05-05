// DebugShotPanel — attach to a persistent parent GO that is always active.
// Wire _panelRoot to the child panel container (Image + all sub-widgets).
// CentralBallWidget calls Toggle() when the ball sprite is tapped.
//
// Required hierarchy (Cesar builds in Unity):
//
//   DebugShotPanelController [this script]       (always active, bottom-center anchor)
//   └── DebugPanel [Image, Raycast Target=true]  ← _panelRoot (starts inactive)
//       ├── PowerRow
//       │   ├── PowerLabel  [TextMeshProUGUI]
//       │   └── PowerInput  [TMP_InputField]     ← _powerInput   (default text "100")
//       ├── AccuracyRow
//       │   ├── AccuracyLabel [TextMeshProUGUI]
//       │   ├── GreenBtn   [Button + Image]       ← _greenBtn
//       │   ├── YellowBtn  [Button + Image]       ← _yellowBtn
//       │   └── RedBtn     [Button + Image]       ← _redBtn
//       └── ShootBtn [Button + TextMeshProUGUI]   ← _shootBtn

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class DebugShotPanel : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;
        [SerializeField] private GameObject     _panelRoot;

        [Header("Inputs")]
        [SerializeField] private TMP_InputField _powerInput;

        [Header("Accuracy buttons")]
        [SerializeField] private Button _greenBtn;
        [SerializeField] private Button _yellowBtn;
        [SerializeField] private Button _redBtn;

        [Header("Shoot")]
        [SerializeField] private Button _shootBtn;

        [Header("Button highlight colors")]
        [SerializeField] private Color _selectedColor   = new Color(0.25f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color _unselectedColor = new Color(0.30f, 0.30f, 0.30f, 1f);

        private DebugShotAccuracy _accuracy = DebugShotAccuracy.Green;

        void Start()
        {
            _greenBtn?.onClick.AddListener(() => SelectAccuracy(DebugShotAccuracy.Green));
            _yellowBtn?.onClick.AddListener(() => SelectAccuracy(DebugShotAccuracy.Yellow));
            _redBtn?.onClick.AddListener(() => SelectAccuracy(DebugShotAccuracy.Red));
            _shootBtn?.onClick.AddListener(OnShoot);

            SelectAccuracy(DebugShotAccuracy.Green);
            if (_panelRoot != null) _panelRoot.SetActive(false);
        }

        public void Toggle()
        {
            if (_panelRoot == null) return;
            _panelRoot.SetActive(!_panelRoot.activeSelf);
        }

        private void SelectAccuracy(DebugShotAccuracy acc)
        {
            _accuracy = acc;
            SetBtnColor(_greenBtn,  acc == DebugShotAccuracy.Green);
            SetBtnColor(_yellowBtn, acc == DebugShotAccuracy.Yellow);
            SetBtnColor(_redBtn,    acc == DebugShotAccuracy.Red);
        }

        private void SetBtnColor(Button btn, bool selected)
        {
            if (btn == null) return;
            var img = btn.GetComponent<Image>();
            if (img != null) img.color = selected ? _selectedColor : _unselectedColor;
        }

        private void OnShoot()
        {
            if (_shotController == null) return;

            float power = 1f;
            if (_powerInput != null && float.TryParse(_powerInput.text, out float parsed))
                power = Mathf.Clamp(parsed, 0f, 120f) / 100f;

            _shotController.FireDebugShot(power, _accuracy);
        }
    }
}
