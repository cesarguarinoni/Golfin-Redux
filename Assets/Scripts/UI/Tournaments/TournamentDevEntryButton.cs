using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// ModeSelection TOURNAMENTS button entry hook. Routes to the T7 TournamentSelection
    /// browse screen (Figma 13386:1758). Previously routed to TournamentHoleSelection
    /// (Stage-1 temp); updated when T7 landed.
    /// </summary>
    public class TournamentDevEntryButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ScreenId _target = ScreenId.TournamentSelection;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(() => ScreenManager.Instance?.ShowScreen(_target));
        }
    }
}
