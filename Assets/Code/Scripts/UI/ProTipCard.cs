using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;

/// <summary>
/// Pro Tip card for the Loading Screen.
/// Cycles through localized tips with optional images.
/// Card auto-resizes via VerticalLayoutGroup + ContentSizeFitter.
/// </summary>
public class ProTipCard : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private TextMeshProUGUI tipText;
    [SerializeField] private TextMeshProUGUI tapNextText;
    [SerializeField] private Image dividerImage;

    [Header("Tip Images")]
    [Tooltip("One sprite per tip key (matched by index). Empty = text-only tip.")]
    [SerializeField] private Sprite[] tipSprites;
    [SerializeField] private Image tipImageDisplay;

    [Header("Tip Keys (localization CSV keys)")]
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

    private int _currentTipIndex;
    private bool _initialized;
    private Coroutine _autoCycleCoroutine;
    private CanvasGroup _tipTextCanvasGroup;
    private LayoutElement _imageLayoutElement;

    private void Awake()
    {
        if (tipText != null)
        {
            _tipTextCanvasGroup = tipText.gameObject.GetComponent<CanvasGroup>();
            if (_tipTextCanvasGroup == null)
                _tipTextCanvasGroup = tipText.gameObject.AddComponent<CanvasGroup>();
        }

        if (tipImageDisplay != null)
        {
            _imageLayoutElement = tipImageDisplay.GetComponent<LayoutElement>();
            if (_imageLayoutElement == null)
                _imageLayoutElement = tipImageDisplay.gameObject.AddComponent<LayoutElement>();
        }
    }

    private void Start()
    {
        if (!_initialized)
            Initialize(tipKeys);
    }

    /// <summary>
    /// Initialize with tip keys. If null/empty, uses the Inspector tipKeys.
    /// Call from LoadingScreen to override keys at runtime.
    /// </summary>
    public void Initialize(string[] keys = null)
    {
        if (keys != null && keys.Length > 0)
            tipKeys = keys;

        _currentTipIndex = 0;
        _initialized = true;

        // Localize static text
        if (headerText != null)
        {
            var loc = headerText.GetComponent<LocalizedText>();
            if (loc != null) loc.SetKey("tip_header");
        }
        if (tapNextText != null)
        {
            var loc = tapNextText.GetComponent<LocalizedText>();
            if (loc != null) loc.SetKey("tip_next");
        }

        ShowTip(0);
        RestartAutoCycle();
    }

    /// <summary>Display a specific tip by index.</summary>
    public void ShowTip(int index)
    {
        if (tipKeys == null || tipKeys.Length == 0) return;

        _currentTipIndex = index % tipKeys.Length;

        // Set tip text from localization
        if (tipText != null)
        {
            string text;
            if (LocalizationManager.Instance != null)
                text = LocalizationManager.Instance.GetText(tipKeys[_currentTipIndex]);
            else
                text = tipKeys[_currentTipIndex]; // fallback: show the key itself

            tipText.text = ProcessGoldTags(text);
        }

        // Set tip image
        if (tipImageDisplay != null)
        {
            Sprite sprite = null;
            if (tipSprites != null && _currentTipIndex < tipSprites.Length)
                sprite = tipSprites[_currentTipIndex];

            if (sprite != null)
            {
                tipImageDisplay.sprite = sprite;
                tipImageDisplay.gameObject.SetActive(true);

                // Use native aspect ratio but cap height so card doesn't overflow
                float nativeW = sprite.rect.width;
                float nativeH = sprite.rect.height;
                float maxW = 882f;  // card content width (978 - 48*2 padding)
                float maxH = 400f;  // cap to prevent card from covering bottom UI

                // Scale down to fit within max bounds while preserving aspect ratio
                float scale = Mathf.Min(maxW / nativeW, maxH / nativeH, 1f);
                float finalW = nativeW * scale;
                float finalH = nativeH * scale;

                if (_imageLayoutElement != null)
                {
                    _imageLayoutElement.preferredWidth = finalW;
                    _imageLayoutElement.preferredHeight = finalH;
                }

                var imgRT = tipImageDisplay.GetComponent<RectTransform>();
                if (imgRT != null)
                    imgRT.sizeDelta = new Vector2(finalW, finalH);

                tipImageDisplay.preserveAspect = true;
            }
            else
            {
                tipImageDisplay.gameObject.SetActive(false);
            }
        }

        // Force layout rebuild so card resizes to fit content
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }

    public void NextTip()
    {
        if (tipKeys == null || tipKeys.Length == 0) return;
        StartCoroutine(CrossfadeToTip((_currentTipIndex + 1) % tipKeys.Length));
    }

    private IEnumerator CrossfadeToTip(int index)
    {
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
