using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages screen transitions with fade effects. 
/// Singleton — persists across scenes.
/// </summary>
public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }
    
    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    
    private ScreenBase _currentScreen;
    private bool _transitioning;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }
    
    /// <summary>Transition to target screen with crossfade</summary>
    public void TransitionTo(ScreenBase target)
    {
        if (_transitioning || target == _currentScreen) return;
        StartCoroutine(DoTransition(target));
    }
    
    /// <summary>Show a screen immediately without transition</summary>
    public void ShowImmediate(ScreenBase target)
    {
        if (_currentScreen != null) _currentScreen.Hide();
        _currentScreen = target;
        _currentScreen.Show();
        _currentScreen.OnScreenEnter();
    }
    
    private IEnumerator DoTransition(ScreenBase target)
    {
        _transitioning = true;
        
        // Fade out current
        if (_currentScreen != null)
            yield return StartCoroutine(_currentScreen.FadeOut(fadeDuration));
        
        // Fade in target
        _currentScreen = target;
        yield return StartCoroutine(_currentScreen.FadeIn(fadeDuration));
        
        _transitioning = false;
    }
}
