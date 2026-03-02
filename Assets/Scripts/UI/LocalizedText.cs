using TMPro;
using UnityEngine;

namespace GolfinRedux.UI
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string _key;

        private TextMeshProUGUI _text;

        private void Awake()
        {
            _text = GetComponent<TextMeshProUGUI>();
            Refresh();
        }

        public void Refresh()
        {
            if (LocalizationManager.Instance == null)
                return;

            _text.text = LocalizationManager.Instance.GetText(_key);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                if (_text == null)
                    _text = GetComponent<TextMeshProUGUI>();

                if (LocalizationManager.Instance != null)
                    _text.text = LocalizationManager.Instance.GetText(_key);
            }
        }
#endif
    }
}
