using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Player-card chip strip displayed on the in-game HUD.
    ///
    /// Solo path (IsVersus == false, _playerIndex == 0):
    ///   Reads PlayerContext (unchanged from before 1v1 spec).
    ///
    /// Versus path (IsVersus == true):
    ///   _playerIndex 0 → reads MatchContext.Players[0] (P1 data mirrored from PlayerContext).
    ///   _playerIndex 1 → reads MatchContext.Players[1] (opponent data from matchmaking).
    ///   CanvasGroup alpha is driven by VersusHudController to indicate whose turn it is.
    ///
    /// The solo path is BYTE-IDENTICAL to the pre-spec implementation when IsVersus == false
    /// and _playerIndex == 0 — no branching occurs.
    /// </summary>
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

        [Header("1v1 settings (leave 0 for P1 / solo)")]
        [SerializeField] int _playerIndex = 0;

        // CanvasGroup is optional — added to support VersusHudController alpha fades.
        // Fetched from the same GameObject in Awake; not a SerializeField to avoid
        // requiring it on the existing solo P1 card.
        CanvasGroup _canvasGroup;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        void OnEnable()
        {
            if (GameSession.IsVersus && _playerIndex >= 0)
            {
                // Versus: subscribe to MatchContext, plus OnActiveChanged for turn-indicator alpha.
                MatchContext.OnChanged       += Refresh;
                MatchContext.OnActiveChanged += RefreshAlpha;
            }
            else
            {
                // Solo path: subscribe to PlayerContext exactly as before.
                PlayerContext.OnChanged   += Refresh;
                GameSession.OnTurnChanged += Refresh;
            }
            Refresh();
        }

        void OnDisable()
        {
            // Always unsubscribe from both buses to avoid stale-listener leaks across
            // play-mode transitions (IsVersus may flip between Enable/Disable).
            PlayerContext.OnChanged   -= Refresh;
            GameSession.OnTurnChanged -= Refresh;
            MatchContext.OnChanged       -= Refresh;
            MatchContext.OnActiveChanged -= RefreshAlpha;
        }

        void Refresh()
        {
            if (GameSession.IsVersus && _playerIndex >= 0)
            {
                // Versus branch: read from MatchContext.Players[_playerIndex].
                var p = MatchContext.Players[_playerIndex];

                if (_portrait != null)
                    _portrait.sprite = p.Portrait != null ? p.Portrait : _defaultPortrait;

                if (_rarityBackground != null)
                    _rarityBackground.sprite = p.RarityBackground != null
                        ? p.RarityBackground
                        : _defaultRarityBackground;

                if (_nameText  != null) _nameText.text  = p.DisplayName;
                if (_levelText != null) _levelText.text = $"Lv {p.Level}";
                if (_turnText  != null) _turnText.text  = $"TURN {p.TurnCount}";

                RefreshAlpha();
            }
            else
            {
                // Solo path: identical to the pre-spec implementation.
                if (_portrait != null)
                    _portrait.sprite = PlayerContext.Portrait != null ? PlayerContext.Portrait : _defaultPortrait;

                if (_rarityBackground != null)
                    _rarityBackground.sprite = PlayerContext.RarityBackground != null
                        ? PlayerContext.RarityBackground
                        : _defaultRarityBackground;

                if (_nameText  != null) _nameText.text  = PlayerContext.DisplayName;
                if (_levelText != null) _levelText.text = $"Lv {PlayerContext.Level}";
                if (_turnText  != null) _turnText.text  = $"TURN {GameSession.TurnCount}";

                // Solo path: no turn-based dimming — always fully opaque.
                if (_canvasGroup != null) _canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// Driven by MatchContext.OnActiveChanged — dims the card when it is not the active player's turn.
        /// Only runs in versus mode. No-op if no CanvasGroup is attached.
        /// </summary>
        void RefreshAlpha()
        {
            if (_canvasGroup == null) return;
            bool isActive = MatchContext.ActiveIndex == _playerIndex;
            _canvasGroup.alpha = isActive ? 1f : 0.50f;
        }
    }
}
