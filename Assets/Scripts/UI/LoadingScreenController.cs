using UnityEngine;
using TMPro;
using GolfinRedux.UI; // for ScreenManager & ScreenId

/// <summary>
/// Controls the Loading Screen:
/// - Drives LoadingBar
/// - Shows percentage text
/// - Applies a minimum display time
/// - After loading, goes to Home screen.
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LoadingBar loadingBar;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI nowLoadingText; // LocalizedText handles this
    [SerializeField] private ScreenManager screenManager;

    [Header("Timing")]
    [SerializeField] private float minLoadingTime = 2.0f;

    private float _realProgress;     // 0..1 data from real loading if provided
    private float _displayProgress;  // 0..1 shown in UI
    private float _timer;
    private bool _useExternalProgress;
    private bool _isLoading;

    private void OnEnable()
    {
        BeginLoading();
        // No localization here; NowLoadingText already has LocalizedText on it
    }

    public void BeginLoading()
    {
        _timer = 0f;
        _realProgress = 0f;
        _displayProgress = 0f;
        _useExternalProgress = false;
        _isLoading = true;

        if (loadingBar != null)
            loadingBar.SetProgressImmediate(0f);

        if (progressText != null)
            progressText.text = "0%";
    }

    /// <summary>
    /// Call this if you have real loading data (0..1).
    /// If never called, controller will just fake a smooth bar using minLoadingTime.
    /// </summary>
    public void SetRealProgress(float progress01)
    {
        _realProgress = Mathf.Clamp01(progress01);
        _useExternalProgress = true;
    }

    private void Update()
    {
        if (!_isLoading)
            return;

        _timer += Time.deltaTime;

        float target = _useExternalProgress
            ? _realProgress
            : Mathf.Clamp01(_timer / minLoadingTime);

        _displayProgress = Mathf.MoveTowards(_displayProgress, target, Time.deltaTime * 0.5f);

        if (loadingBar != null)
            loadingBar.SetProgress(_displayProgress);

        if (progressText != null)
            progressText.text = $"{Mathf.RoundToInt(_displayProgress * 100f)}%";

        bool realDone = _useExternalProgress ? _realProgress >= 0.999f : _timer >= minLoadingTime;
        bool minTimeReached = _timer >= minLoadingTime;
        bool uiDone = _displayProgress >= 0.999f;

        if (realDone && minTimeReached && uiDone)
        {
            FinishLoading();
        }
    }

    private void FinishLoading()
    {
        _isLoading = false;

        if (screenManager != null)
            screenManager.ShowScreen(ScreenId.Home);
        else
            Debug.LogWarning("[LoadingScreenController] ScreenManager not assigned.");
    }
}
