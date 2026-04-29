using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class PlayerCardWidget : MonoBehaviour
    {
        [Header("Portrait")]
        [SerializeField] Image  _portrait;
        [SerializeField] Sprite _defaultPortrait;
        [SerializeField] Image  _rarityBackground;
        [SerializeField] Sprite _defaultRarityBackground;

        [Header("Chip rows")]
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _levelText;
        [SerializeField] TMP_Text _turnText;

        void OnEnable()
        {
            PlayerContext.OnChanged   += Refresh;
            GameSession.OnTurnChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            PlayerContext.OnChanged   -= Refresh;
            GameSession.OnTurnChanged -= Refresh;
        }

        void Refresh()
        {
            if (_portrait != null)
                _portrait.sprite = PlayerContext.Portrait != null ? PlayerContext.Portrait : _defaultPortrait;

            if (_rarityBackground != null)
                _rarityBackground.sprite = PlayerContext.RarityBackground != null
                    ? PlayerContext.RarityBackground
                    : _defaultRarityBackground;

            if (_nameText  != null) _nameText.text  = PlayerContext.DisplayName;
            if (_levelText != null) _levelText.text = $"Lv {PlayerContext.Level}";
            if (_turnText  != null) _turnText.text  = $"TURN {GameSession.TurnCount}";
        }
    }
}
