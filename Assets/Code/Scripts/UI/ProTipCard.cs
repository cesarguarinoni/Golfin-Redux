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
    [SerializeField] private Image dividerImage;            // Gold divider line

    [Header("Tip Images (optional — one per tip, matched by index)")]
    [Tooltip("Assign sprites here. Index 0 = first tip key, Index 1 = second, etc. Leave empty slots for text-only tips.")]
    [SerializeField] private Sprite[] tipSprites;           // Drag sprites here in Inspector
    [SerializeField] private Image tipImageDisplay;         // Single Image component to show current tip's sprite
    
    [Header("Tip Keys (from localization CSV)")]
    [SerializeField] private string[] tipKeys = new string[]
    {
        "tip_club_bag", "tip_forecast", "tip_rarities", "tip_swing",
        "tip_accuracy", "tip_leaderboard", "tip_timing", "tip_view_switch"
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
        // Only auto-initialize if not already initialized by LoadingScreen.OnScreenEnter()
        // Use a frame delay to let external Initialize() calls happen first
        if (_tipKeys == null || _tipKeys.Length == 0)
            StartCoroutine(DelayedAutoInit());
    }

    private IEnumerator DelayedAutoInit()
    {
        yield return null; // wait one frame for external Initialize() calls
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
        
        // Show tip image if a sprite is assigned for this index
        if (tipImageDisplay != null)
        {
            Sprite sprite = null;
            if (tipSprites != null && _currentTipIndex < tipSprites.Length)
                sprite = tipSprites[_currentTipIndex];
            
            tipImageDisplay.sprite = sprite;
            tipImageDisplay.gameObject.SetActive(sprite != null);
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
