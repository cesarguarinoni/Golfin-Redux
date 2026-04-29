using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Golfin.Gameplay.UI.HUD;

namespace Golfin.Gameplay.UI.ShotUI
{
    public class SpinPanelWidget : MonoBehaviour
    {
        [SerializeField] private Image         _ballImage;
        [SerializeField] private RectTransform _spinDot;
        [SerializeField] private OutsideClickCatcher _dimBackground;
        [SerializeField] private Sprite        _defaultBallSprite;

        readonly Vector2[] _positions = {
            new Vector2(   0f,    0f), // 0 center
            new Vector2(   0f,  220f), // 1 top
            new Vector2(   0f, -220f), // 2 bottom
            new Vector2(-220f,    0f), // 3 left
            new Vector2( 220f,    0f), // 4 right
        };
        readonly Vector2[] _values = {
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, -1f),
            new Vector2(-1f, 0f), new Vector2(1f, 0f)
        };

        void OnEnable()
        {
            if (_dimBackground != null) _dimBackground.OnOutsideClick = Close;
        }

        public void Open()
        {
            if (_dimBackground != null) _dimBackground.gameObject.SetActive(true);
            gameObject.SetActive(true);
            if (_ballImage != null)
                _ballImage.sprite = BallContext.SelectedThumbnail != null
                    ? BallContext.SelectedThumbnail
                    : _defaultBallSprite;
            SnapDotToCurrent();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (_dimBackground != null) _dimBackground.gameObject.SetActive(false);
        }

        void SnapDotToCurrent()
        {
            int idx = 0;
            for (int i = 0; i < _values.Length; i++)
            {
                if (Mathf.Approximately(_values[i].x, SpinContext.Spin.x) &&
                    Mathf.Approximately(_values[i].y, SpinContext.Spin.y))
                {
                    idx = i;
                    break;
                }
            }
            if (_spinDot != null) _spinDot.anchoredPosition = _positions[idx];
        }

        // Builder wires 5 invisible buttons over the ball, each calling SelectPosition(i).
        public void SelectPosition(int idx)
        {
            idx = Mathf.Clamp(idx, 0, _positions.Length - 1);
            if (_spinDot != null) _spinDot.anchoredPosition = _positions[idx];
            SpinContext.SetSpin(_values[idx]);
        }
    }
}
