using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loading bar where the Fill image grows from left to right.
/// Uses Image.fillAmount for simplicity.
/// </summary>
public class LoadingBar : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    [SerializeField] private float smoothSpeed = 3f;

    private float _targetProgress;
    private float _currentProgress;

    private void Start()
    {
        SetProgressImmediate(0f);
    }

    private void Update()
    {
        _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.deltaTime * smoothSpeed);
        if (fillImage != null)
        {
            fillImage.fillAmount = _currentProgress;
        }
    }

    public void SetProgress(float progress01)
    {
        _targetProgress = Mathf.Clamp01(progress01);
    }

    public void SetProgressImmediate(float progress01)
    {
        _targetProgress = Mathf.Clamp01(progress01);
        _currentProgress = _targetProgress;
        if (fillImage != null)
            fillImage.fillAmount = _currentProgress;
    }
}
