using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading bar: white track (background) with blue fill that grows left-to-right.
/// Fill works by scaling the fill RectTransform width, NOT Image.fillAmount.
/// This works with or without sprites assigned.
/// </summary>
public class LoadingBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform fillRect;   // The blue fill bar
    [SerializeField] private Image fillImage;           // For color control
    [SerializeField] private Image glowImage;           // Optional glow at fill edge

    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 3f;

    [Header("Colors")]
    [SerializeField] private Color fillColor = new Color(0.13f, 0.50f, 0.88f); // #2080E0

    private float _targetProgress;
    private float _currentProgress;
    private RectTransform _barRect;

    private void Awake()
    {
        _barRect = GetComponent<RectTransform>();

        // If fillRect not assigned, try to find child
        if (fillRect == null && fillImage != null)
            fillRect = fillImage.GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Set initial color
        if (fillImage != null)
            fillImage.color = fillColor;

        // Ensure fill starts at 0
        UpdateFillVisual(0f);
    }

    private void Update()
    {
        _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.deltaTime * smoothSpeed);
        UpdateFillVisual(_currentProgress);
    }

    private void UpdateFillVisual(float progress)
    {
        if (fillRect == null || _barRect == null) return;

        // Scale fill width as percentage of track width
        // Anchor fill to left side, stretch vertically
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(progress, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Glow follows the right edge of the fill
        if (glowImage != null)
        {
            var glowRT = glowImage.GetComponent<RectTransform>();
            if (glowRT != null)
            {
                float barWidth = _barRect.rect.width;
                float xPos = barWidth * progress - barWidth * 0.5f;
                glowRT.anchoredPosition = new Vector2(xPos, 0f);
            }
            glowImage.gameObject.SetActive(progress > 0.02f && progress < 0.98f);
        }
    }

    public void SetProgress(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
    }

    public void SetProgressImmediate(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
        _currentProgress = _targetProgress;
        UpdateFillVisual(_currentProgress);
    }
}
