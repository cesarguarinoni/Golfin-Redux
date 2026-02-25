using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;

/// <summary>
/// Button with visual press-down state.
/// On press: scales to pressedScale and tints with pressedColor.
/// On release: restores original. Fires onClick event.
/// </summary>
public class PressableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("Press Effect")]
    [SerializeField] private float pressedScale = 0.95f;
    [SerializeField] private Color pressedTint = new Color(0.85f, 0.85f, 0.85f, 1f);
    [SerializeField] private float transitionSpeed = 15f;
    
    [Header("Audio (Optional)")]
    [SerializeField] private AudioClip pressSound;
    
    public UnityEvent onClick;
    
    private RectTransform _rect;
    private Image _image;
    private Vector3 _originalScale;
    private Color _originalColor;
    private bool _isPressed;
    private float _currentLerp;
    
    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _image = GetComponent<Image>();
        _originalScale = _rect.localScale;
        if (_image != null) _originalColor = _image.color;
    }
    
    private void Update()
    {
        // Smooth interpolation toward target state
        float target = _isPressed ? 1f : 0f;
        _currentLerp = Mathf.MoveTowards(_currentLerp, target, Time.deltaTime * transitionSpeed);
        
        // Apply scale
        float scale = Mathf.Lerp(1f, pressedScale, _currentLerp);
        _rect.localScale = _originalScale * scale;
        
        // Apply tint
        if (_image != null)
            _image.color = Color.Lerp(_originalColor, _originalColor * pressedTint, _currentLerp);
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        
        if (pressSound != null)
            AudioSource.PlayClipAtPoint(pressSound, Camera.main.transform.position, 0.5f);
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }
}
