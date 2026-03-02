using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI
{
    [RequireComponent(typeof(Button))]
    public class UIButtonClickEffect : MonoBehaviour
    {
        [Header("Scale")]
        [SerializeField] private float _pressedScale = 0.9f;
        [SerializeField] private float _animationDuration = 0.08f; // quick pop

        [Header("Color (optional)")]
        [SerializeField] private Image _targetImage;
        [SerializeField] private Color _pressedColor = Color.gray;
        [SerializeField] private bool _useColorEffect = false;

        private Button _button;
        private Vector3 _originalScale;
        private Color _originalColor;
        private bool _isAnimating;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _originalScale = transform.localScale;

            if (_targetImage == null)
                _targetImage = GetComponent<Image>();

            if (_targetImage != null)
                _originalColor = _targetImage.color;

            _button.onClick.AddListener(PlayClickEffect);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(PlayClickEffect);
        }

        private void PlayClickEffect()
        {
            if (!_isAnimating)
                StartCoroutine(AnimateClick());
        }

        private IEnumerator AnimateClick()
        {
            _isAnimating = true;

            // Scale / color down
            float t = 0f;
            while (t < _animationDuration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = t / _animationDuration;
                transform.localScale = Vector3.Lerp(_originalScale, _originalScale * _pressedScale, lerp);

                if (_useColorEffect && _targetImage != null)
                    _targetImage.color = Color.Lerp(_originalColor, _pressedColor, lerp);

                yield return null;
            }

            // Scale / color back
            t = 0f;
            while (t < _animationDuration)
            {
                t += Time.unscaledDeltaTime;
                float lerp = t / _animationDuration;
                transform.localScale = Vector3.Lerp(_originalScale * _pressedScale, _originalScale, lerp);

                if (_useColorEffect && _targetImage != null)
                    _targetImage.color = Color.Lerp(_pressedColor, _originalColor, lerp);

                yield return null;
            }

            _isAnimating = false;
        }
    }
}
