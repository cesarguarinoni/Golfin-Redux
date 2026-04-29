using TMPro;
using UnityEngine;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class WindIndicatorWidget : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] RectTransform _arrow;      // child of the navy half; rotates around its own center
        [SerializeField] TMP_Text      _speedText;  // "1.5 mph"

        void OnEnable()
        {
            WindContext.OnChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            WindContext.OnChanged -= Refresh;
        }

        void Refresh()
        {
            if (_speedText != null) _speedText.text = $"{WindContext.SpeedMph:F1} mph";
            if (_arrow != null)
            {
                // Compass: 0=North=up. Figma chevron points right by default (East = 90 deg).
                // To rotate UI: -DirectionDegrees because Unity Z rotation is counter-clockwise.
                // 0 deg (N) -> arrow points up -> -90 deg from default-east-pointing chevron.
                float zRot = -WindContext.DirectionDegrees - 90f;
                _arrow.localRotation = Quaternion.Euler(0f, 0f, zRot);
            }
        }
    }
}
