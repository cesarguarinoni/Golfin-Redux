using UnityEngine;
using TMPro;

/// <summary>
/// Attach to any TextMeshProUGUI to auto-localize it from a key.
/// Supports gold highlight tags: wrap words with {gold}word{/gold} in CSV.
/// </summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public class LocalizedText : MonoBehaviour
{
    [Header("Localization")]
    [SerializeField] private string localizationKey;
    [SerializeField] private Color highlightColor = new Color(0.78f, 0.72f, 0.19f); // Gold #C8B830
    
    private TextMeshProUGUI _text;
    
    private void Awake() => _text = GetComponent<TextMeshProUGUI>();
    
    private void OnEnable()
    {
        UpdateText();
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += UpdateText;
    }
    
    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= UpdateText;
    }
    
    /// <summary>Set a new key and refresh</summary>
    public void SetKey(string key)
    {
        localizationKey = key;
        UpdateText();
    }
    
    /// <summary>Set key with format arguments</summary>
    public void SetKey(string key, params object[] args)
    {
        localizationKey = key;
        if (LocalizationManager.Instance == null) return;
        string raw = LocalizationManager.Instance.GetText(key, args);
        _text.text = ProcessHighlights(raw);
    }
    
    private void UpdateText()
    {
        if (string.IsNullOrEmpty(localizationKey) || LocalizationManager.Instance == null) return;
        string raw = LocalizationManager.Instance.GetText(localizationKey);
        _text.text = ProcessHighlights(raw);
    }
    
    /// <summary>Replace {gold}text{/gold} with TMP rich text color tags</summary>
    private string ProcessHighlights(string input)
    {
        string hex = ColorUtility.ToHtmlStringRGB(highlightColor);
        return input
            .Replace("{gold}", $"<color=#{hex}>")
            .Replace("{/gold}", "</color>");
    }
}
