using UnityEngine;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SpinButtonWidget : ActionButtonWidget
    {
        [SerializeField] private SpinPanelWidget _spinPanel;

        protected override void Refresh() { /* static label, no dynamic update needed */ }

        protected override void OnClick()
        {
            if (_spinPanel != null) _spinPanel.Open();
        }
    }
}
