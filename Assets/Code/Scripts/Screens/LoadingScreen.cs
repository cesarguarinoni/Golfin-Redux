using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Loading screen with progress bar and rotating Pro Tips.
/// Simulates loading progress. Tips cycle on timer or tap.
/// </summary>
public class LoadingScreen : ScreenBase
{
    [Header("References")]
    [SerializeField] private LoadingBar loadingBar;
    [SerializeField] private ProTipCard proTipCard;
    [SerializeField] private TextMeshProUGUI nowLoadingText;
    [SerializeField] private TextMeshProUGUI downloadProgressText;
    
    [Header("Loading Settings")]
    [SerializeField] private float simulatedLoadTime = 8f;
    [SerializeField] private float totalSizeMB = 267f;
    
    [Header("Tip Keys (from localization CSV)")]
    [SerializeField] private string[] tipKeys = new string[]
    {
        "tip_club_bag",
        "tip_forecast",
        "tip_rarities",
        "tip_swing",
        "tip_accuracy",
        "tip_leaderboard",
        "tip_timing",
        "tip_view_switch"
    };
    
    /// <summary>True when the simulated loading is done</summary>
    public bool IsLoadingComplete { get; private set; }
    
    private float _loadProgress;
    
    public override void OnScreenEnter()
    {
        IsLoadingComplete = false;
        _loadProgress = 0f;
        
        if (proTipCard != null)
            proTipCard.Initialize(tipKeys);
        
        StartCoroutine(SimulateLoading());
    }
    
    public override void OnScreenExit()
    {
        StopAllCoroutines();
    }
    
    private IEnumerator SimulateLoading()
    {
        float elapsed = 0f;
        
        while (elapsed < simulatedLoadTime)
        {
            elapsed += Time.deltaTime;
            _loadProgress = Mathf.Clamp01(elapsed / simulatedLoadTime);
            
            // Ease-out curve for more natural feel
            float displayProgress = 1f - Mathf.Pow(1f - _loadProgress, 2f);
            
            if (loadingBar != null)
                loadingBar.SetProgress(displayProgress);
            
            // Update download text
            if (downloadProgressText != null)
            {
                float currentMB = displayProgress * totalSizeMB;
                downloadProgressText.text = $"{currentMB:F2} / {totalSizeMB:F0} MB";
            }
            
            yield return null;
        }
        
        if (loadingBar != null)
            loadingBar.SetProgress(1f);
        
        // Brief pause at 100%
        yield return new WaitForSeconds(0.5f);
        
        IsLoadingComplete = true;
    }
}
