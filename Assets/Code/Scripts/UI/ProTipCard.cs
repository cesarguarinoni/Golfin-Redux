using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Glassmorphism Pro Tip card that displays localized tips.
/// Auto-cycles through tips on a timer. Tap to advance immediately.
/// Uses VerticalLayoutGroup + ContentSizeFitter to auto-resize with content.
/// </summary>
public class ProTipCard : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI headerText;   // "PRO TIP"
    [SerializeField] private TextMeshProUGUI tipText;       // Main tip content
    [SerializeField] private TextMeshProUGUI tapNextText;   // "TAP FOR NEXT TIP"
    [SerializeField] private Image[] tipImages;             // Optional tip illustrations
    [SerializeField] private Image dividerImage;            // Gold divider line
    
    [Header("Tip Keys (from localization CSV)")]
    [SerializeField] private string[] tipKeys = new string[]
    {
        "tip_first", "tip_flick", "tip_forecast", "tip_leaderboards",
        "tip_map", "tip_accuracy", "tip_perfect", "tip_swap", "tip_wind"
    };
    
    [Header("Settings")]
    [SerializeField] private float autoCycleInterval = 8f;
    [SerializeField] private float textFadeDuration = 0.3f;
    
    [Header("Highlight Color")]
    [SerializeField] private Color goldColor = new Color(0.78f, 0.72f, 0.19f);
    
    private string[] _tipKeys;
    private int _currentTipIndex;
    private Coroutine _autoCycleCoroutine;
    private CanvasGroup _tipTextCanvasGroup;
    
    private void Awake()
    {
        // Add CanvasGroup for fading tip text
        if (tipText != null)
        {
            _tipTextCanvasGroup = tipText.gameObject.GetComponent<CanvasGroup>();
            if (_tipTextCanvasGroup == null)
                _tipTextCanvasGroup = tipText.gameObject.AddComponent<CanvasGroup>();
        }
    }
    
    private void Start()
    {
        // Auto-initialize with Inspector keys if not initialized externally
        if (_tipKeys == null || _tipKeys.Length == 0)
            Initialize(tipKeys);
    }
    
    /// <summary>Initialize with an array of localization keys (or uses Inspector defaults)</summary>
    public void Initialize(string[] keys)
    {
        _tipKeys = keys;
        _currentTipIndex = 0;
        
        // Localize static elements
        if (headerText != null)
        {
            var locHeader = headerText.GetComponent<LocalizedText>();
            if (locHeader != null) locHeader.SetKey("tip_header");
        }
        if (tapNextText != null)
        {
            var locTap = tapNextText.GetComponent<LocalizedText>();
            if (locTap != null) locTap.SetKey("tip_next");
        }
        
        ShowTip(0);
        RestartAutoCycle();
    }
    
    /// <summary>Display a specific tip by index</summary>
    public void ShowTip(int index)
    {
        if (_tipKeys == null || _tipKeys.Length == 0) return;
        
        _currentTipIndex = index % _tipKeys.Length;
        
        if (tipText != null && LocalizationManager.Instance != null)
        {
            string raw = LocalizationManager.Instance.GetText(_tipKeys[_currentTipIndex]);
            tipText.text = ProcessGoldTags(raw);
        }
        
        // Show/hide tip images if assigned
        if (tipImages != null)
        {
            for (int i = 0; i < tipImages.Length; i++)
            {
                if (tipImages[i] != null)
                    tipImages[i].gameObject.SetActive(i == _currentTipIndex);
            }
        }
    }
    
    /// <summary>Advance to the next tip with a crossfade</summary>
    public void NextTip()
    {
        StartCoroutine(CrossfadeToTip((_currentTipIndex + 1) % _tipKeys.Length));
    }
    
    private IEnumerator CrossfadeToTip(int index)
    {
        // Fade out
        if (_tipTextCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < textFadeDuration)
            {
                elapsed += Time.deltaTime;
                _tipTextCanvasGroup.alpha = 1f - (elapsed / textFadeDuration);
                yield return null;
            }
        }
        
        ShowTip(index);
        
        // Fade in
        if (_tipTextCanvasGroup != null)
        {
            float elapsed = 0f;
            while (elapsed < textFadeDuration)
            {
                elapsed += Time.deltaTime;
                _tipTextCanvasGroup.alpha = elapsed / textFadeDuration;
                yield return null;
            }
            _tipTextCanvasGroup.alpha = 1f;
        }
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        NextTip();
        RestartAutoCycle();
    }
    
    private void RestartAutoCycle()
    {
        if (_autoCycleCoroutine != null) StopCoroutine(_autoCycleCoroutine);
        _autoCycleCoroutine = StartCoroutine(AutoCycleRoutine());
    }
    
    private IEnumerator AutoCycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(autoCycleInterval);
            NextTip();
        }
    }
    
    /// <summary>Replace {gold}text{/gold} with TMP color tags</summary>
    private string ProcessGoldTags(string input)
    {
        string hex = ColorUtility.ToHtmlStringRGB(goldColor);
        return input
            .Replace("{gold}", $"<color=#{hex}>")
            .Replace("{/gold}", "</color>");
    }
    
    private void OnDisable()
    {
        if (_autoCycleCoroutine != null) StopCoroutine(_autoCycleCoroutine);
    }
}
