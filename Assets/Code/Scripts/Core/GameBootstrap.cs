using UnityEngine;
using System.Collections;

/// <summary>
/// Entry point. Manages the startup flow: Logo → Loading → Splash → Home.
/// Attach to a root GameObject in the scene.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Screen References")]
    [SerializeField] private LogoScreen logoScreen;
    [SerializeField] private LoadingScreen loadingScreen;
    [SerializeField] private SplashScreen splashScreen;
    [SerializeField] private HomeScreen homeScreen;
    
    [Header("Timing")]
    [SerializeField] private float logoDisplayTime = 3f;
    
    private void Start()
    {
        // Hide all screens
        logoScreen.Hide();
        loadingScreen.Hide();
        splashScreen.Hide();
        homeScreen.Hide();
        
        StartCoroutine(StartupSequence());
    }
    
    private IEnumerator StartupSequence()
    {
        // Phase 1: Logo
        ScreenManager.Instance.ShowImmediate(logoScreen);
        yield return new WaitForSeconds(logoDisplayTime);
        
        // Phase 2: Loading with tips
        ScreenManager.Instance.TransitionTo(loadingScreen);
        
        // Wait for loading to complete
        yield return new WaitUntil(() => loadingScreen.IsLoadingComplete);
        
        // Phase 3: Splash with Start button
        ScreenManager.Instance.TransitionTo(splashScreen);
        
        // Wire Start button → Home screen
        splashScreen.OnStartPressed.RemoveAllListeners();
        splashScreen.OnStartPressed.AddListener(GoToHome);
    }
    
    private void GoToHome()
    {
        ScreenManager.Instance.TransitionTo(homeScreen);
    }
}
