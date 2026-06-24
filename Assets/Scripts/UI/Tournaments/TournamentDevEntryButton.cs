using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// TEMPORARY Stage-1 entry hook. The real upstream "Tournament Selection" screen
    /// (Implementation Plan T7) does not exist yet, so this small button lets the
    /// Selection → Tournament Hole Selection forward navigation be exercised now.
    /// REMOVE once T7 (tournament_selection_screen) lands and wires the real card.
    /// </summary>
    public class TournamentDevEntryButton : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private ScreenId _target = ScreenId.TournamentHoleSelection;

        private void Awake()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null)
                _button.onClick.AddListener(() => ScreenManager.Instance?.ShowScreen(_target));
        }
    }
}
