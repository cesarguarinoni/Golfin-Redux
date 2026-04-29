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
        [SerializeField] private Sprite        _defaultBallSprite;
        [SerializeField] private GameObject    _aimingCone;

        // Kept for legacy wiring; actual close is driven by a runtime Button (see Open()).
        [SerializeField] private OutsideClickCatcher _dimBackground;

        Button   _dimCloseBtn;
        GameObject _dimGo;

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

        public void Open()
        {
            if (_aimingCone != null) _aimingCone.SetActive(false);
            ActivateDim();
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
            if (_dimCloseBtn != null) _dimCloseBtn.onClick.RemoveListener(Close);
            if (_dimGo != null) _dimGo.SetActive(false);
            if (_aimingCone != null) _aimingCone.SetActive(true);
        }

        void ActivateDim()
        {
            // Resolve the dim GO: prefer serialized _dimBackground.gameObject, else find by name.
            if (_dimGo == null)
            {
                if (_dimBackground != null)
                    _dimGo = _dimBackground.gameObject;
                else if (transform.parent != null)
                {
                    var t = transform.parent.Find("OutsideClickCatcher_Spin");
                    if (t != null) _dimGo = t.gameObject;
                }
            }
            if (_dimGo == null) return;

            // Add a Button at runtime — guaranteed to survive play-mode serialization.
            _dimCloseBtn = _dimGo.GetComponent<Button>();
            if (_dimCloseBtn == null)
            {
                _dimCloseBtn = _dimGo.AddComponent<Button>();
                var img = _dimGo.GetComponent<Image>();
                if (img != null) _dimCloseBtn.targetGraphic = img;
            }
            _dimCloseBtn.onClick.RemoveListener(Close);
            _dimCloseBtn.onClick.AddListener(Close);

            _dimGo.SetActive(true);
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
