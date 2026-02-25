using UnityEngine;
using System.Collections;

/// <summary>
/// Entry point. Manages the startup flow: Logo → Loading → Splash.
/// Attach to a root GameObject in the scene.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Screen References")]
    [SerializeField] private LogoScreen logoScreen;
    [SerializeField] private LoadingScreen loadingScreen;
    [SerializeField] private SplashScreen splashScreen;
    
    [Header("Timing")]
    [SerializeField] private float logoDisplayTime = 3f;
    
    private void Start()
    {
        // Hide all screens
        logoScreen.Hide();
        loadingScreen.Hide();
        splashScreen.Hide();
        
        StartCoroutine(StartupSequence());
    }
    
    private IEnumerator StartupSequence()
    {
        // Phase 1: Logo
        ScreenManager.Instance.ShowImmediate(logoScreen);
        yield return new WaitForSeconds(logoDisplayTime);
        
        // Phase 2: Loading with tips
        ScreenManager.Instance.TransitionTo(loadingScreen);
        
        // Wait for loading to complete (LoadingScreen signals when done)
        yield return new WaitUntil(() => loadingScreen.IsLoadingComplete);
        
        // Phase 3: Splash with Start button
        ScreenManager.Instance.TransitionTo(splashScreen);
    }
}
