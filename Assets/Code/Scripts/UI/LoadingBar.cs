using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Animated loading bar with pill shape and glow effect.
/// Uses a fill Image (Image Type = Filled, Fill Method = Horizontal).
/// </summary>
public class LoadingBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private Image glowImage; // Optional glow that follows fill edge
    
    [Header("Settings")]
    [SerializeField] private float smoothSpeed = 3f;
    
    [Header("Colors")]
    [SerializeField] private Color fillColorStart = new Color(0.13f, 0.50f, 0.88f); // #2080E0
    [SerializeField] private Color fillColorEnd = new Color(0.25f, 0.63f, 1f);       // #40A0FF
    
    private float _targetProgress;
    private float _currentProgress;
    private RectTransform _barRect;
    private RectTransform _glowRect;
    
    private void Awake()
    {
        _barRect = fillImage?.GetComponent<RectTransform>();
        _glowRect = glowImage?.GetComponent<RectTransform>();
    }
    
    private void Update()
    {
        // Smooth progress toward target
        _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.deltaTime * smoothSpeed);
        
        if (fillImage != null)
        {
            fillImage.fillAmount = _currentProgress;
            // Gradient effect via color lerp
            fillImage.color = Color.Lerp(fillColorStart, fillColorEnd, _currentProgress);
        }
        
        // Move glow to the fill edge
        if (glowImage != null && _barRect != null && _glowRect != null)
        {
            float barWidth = _barRect.rect.width;
            float xPos = barWidth * _currentProgress - (barWidth * 0.5f);
            _glowRect.anchoredPosition = new Vector2(xPos, 0f);
            glowImage.gameObject.SetActive(_currentProgress > 0.01f && _currentProgress < 0.99f);
        }
    }
    
    /// <summary>Set the target progress (0-1)</summary>
    public void SetProgress(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
    }
    
    /// <summary>Set progress immediately without smoothing</summary>
    public void SetProgressImmediate(float progress)
    {
        _targetProgress = Mathf.Clamp01(progress);
        _currentProgress = _targetProgress;
    }
}
