using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    [Tooltip("Optional Japanese font-size scale, relative to this label's design (English) size. " +
             "0 = no override (English and Japanese share the design size). e.g. 0.85 renders Japanese " +
             "at 85% so longer CJK strings fit fixed-width buttons WITHOUT changing the English size.")]
    [SerializeField] private float japaneseFontScale = 0f;

    private TextMeshProUGUI _label;
    private float _baseFontSize;
    private bool _baseCaptured;

    private void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
        CaptureBaseFontSize();
    }

    // Capture the authored (English/design) size once, before we ever scale it.
    private void CaptureBaseFontSize()
    {
        if (!_baseCaptured && _label != null)
        {
            _baseFontSize = _label.fontSize;
            _baseCaptured = true;
        }
    }

    private void OnEnable()
    {
        LocalizationManager.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalizationManager.OnLanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        // Resolve lazily: Refresh can be called before Awake has run — via the public SetKey, or
        // from editor tooling previewing a language — and _label would still be null, so the call
        // silently did nothing.
        if (_label == null) _label = GetComponent<TextMeshProUGUI>();

        if (_label == null || string.IsNullOrEmpty(key)) return;
        _label.text = LocalizationManager.Get(key);
        ApplyPerLanguageSize();
    }

    // Per-language font size: English always renders at the design size; Japanese renders at
    // designSize * japaneseFontScale when a scale is set. No effect when japaneseFontScale <= 0.
    private void ApplyPerLanguageSize()
    {
        if (japaneseFontScale <= 0f || _label == null) return;
        CaptureBaseFontSize();
        bool isJapanese = LocalizationManager.CurrentLanguage == Language.Japanese;
        _label.fontSize = isJapanese ? _baseFontSize * japaneseFontScale : _baseFontSize;
    }

    public void SetKey(string newKey)
    {
        key = newKey;
        Refresh();
    }
}
