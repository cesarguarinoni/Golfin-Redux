using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI; // for ScreenManager & ScreenId

/// <summary>
/// Controls the Loading Screen:
/// - Drives LoadingBar
/// - Shows percentage text
/// - Applies a minimum display time
/// - For LegacyBootHome target (default), navigates to Home when done.
/// - For HoleLoad target (Stage C0), driven by external progress and finishes via
///   FinishLoadingCoroutine (no auto-navigate; GameplaySceneLoader owns the transition).
/// </summary>
public class LoadingScreenController : MonoBehaviour
{
    public enum LoadTarget { LegacyBootHome, HoleLoad }

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

    // Stage C0: target + hole-number plumbing for the gameplay-scene transition.
    private LoadTarget _target = LoadTarget.LegacyBootHome;
    private int        _targetHoleNumber;

    public LoadTarget Target => _target;
    public int TargetHoleNumber => _targetHoleNumber;

    private void Awake()
    {
        // Stage C0: ensure the LoadingScreen renders ON TOP of any additively-loaded
        // gameplay canvases (LabScaffold's ShotUI_canvas at sortOrder 0). The ShellScene
        // root Canvas runs at sortOrder=-1, so without this override the loaded gameplay
        // UI would punch through the loading screen.
        var canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
    }

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
        // HoleLoad uses external progress; LegacyBootHome uses internal fake bar.
        _useExternalProgress = (_target == LoadTarget.HoleLoad);
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

    /// <summary>
    /// Stage C0: configure this loading session as a hole-load transition.
    /// Must be called BEFORE the loading screen GameObject is activated (OnEnable runs BeginLoading).
    /// </summary>
    public void PrepareForHoleLoad(int holeNumber)
    {
        _target = LoadTarget.HoleLoad;
        _targetHoleNumber = holeNumber;
        // External progress driver flag is also set here so BeginLoading (running on
        // the same frame the loader is activated) picks up the right mode.
        _useExternalProgress = true;
    }

    /// <summary>
    /// Stage C0: reset to the legacy boot path. Call before showing the loading screen
    /// from any non-gameplay flow.
    /// </summary>
    public void ClearTarget()
    {
        _target = LoadTarget.LegacyBootHome;
        _targetHoleNumber = 0;
        _useExternalProgress = false;
    }

    /// <summary>
    /// Stage C0: external progress driver (GameplaySceneLoader feeds SceneManager.LoadSceneAsync progress).
    /// </summary>
    public void SetProgress(float p)
    {
        _realProgress = Mathf.Clamp01(p);
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

        // Stage C0: HoleLoad target is driven by FinishLoadingCoroutine; do not auto-finish here.
        if (_target == LoadTarget.LegacyBootHome && realDone && minTimeReached && uiDone)
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

    /// <summary>
    /// Stage C0: awaitable finish for the HoleLoad target.
    /// Waits until the display bar catches up to the external progress and the minimum
    /// display time has elapsed, then hides the loading screen GameObject (no navigation).
    /// </summary>
    public IEnumerator FinishLoadingCoroutine()
    {
        // Force the external progress to 1.0 so the bar drives to completion.
        _realProgress = 1f;
        _useExternalProgress = true;

        while (_isLoading && (_displayProgress < 0.999f || _timer < minLoadingTime))
            yield return null;

        _isLoading = false;

        // Hide the loading screen GameObject so the additively-loaded gameplay scene
        // becomes visible. Do NOT call screenManager.ShowScreen(Home) — gameplay is taking over.
        gameObject.SetActive(false);

        // Reset for the next show.
        _target = LoadTarget.LegacyBootHome;
        _targetHoleNumber = 0;
        _useExternalProgress = false;
    }
}
