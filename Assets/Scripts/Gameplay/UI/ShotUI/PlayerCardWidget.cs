using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class PlayerCardWidget : MonoBehaviour
    {
        [Header("Portrait")]
        [SerializeField] Image _portrait;
        [SerializeField] Sprite _defaultPortrait;

        [Header("Chip rows")]
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _turnText;

        void OnEnable()
        {
            GameSession.OnTurnChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            GameSession.OnTurnChanged -= Refresh;
        }

        void Refresh()
        {
            if (_portrait != null && _defaultPortrait != null)
                _portrait.sprite = _defaultPortrait;
            if (_nameText != null)  _nameText.text  = "PLAYER";
            if (_levelText != null) _levelText.text = "Lv 1";
            if (_turnText != null)  _turnText.text  = $"TURN {GameSession.TurnCount}";
        }
    }
}
