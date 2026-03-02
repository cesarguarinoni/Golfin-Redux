using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages screen transitions with fade effects. 
/// Singleton — persists across scenes.
/// Creates a persistent black background behind all screens so
/// the empty scene never shows during transitions.
/// </summary>
public class ScreenManager : MonoBehaviour
{
    public static ScreenManager Instance { get; private set; }
    
    [Header("Transition Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private Color backgroundColor = Color.black;
    
    private ScreenBase _currentScreen;
    private bool _transitioning;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        
        EnsurePersistentBackground();
    }
    
    /// <summary>
    /// Creates a black background as the first child of Canvas,
    /// behind all screens. Ensures no empty scene is ever visible.
    /// </summary>
    private void EnsurePersistentBackground()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        
        // Check if it already exists
        Transform existing = canvas.transform.Find("_PersistentBackground");
        if (existing != null) return;
        
        GameObject bg = new GameObject("_PersistentBackground");
        bg.transform.SetParent(canvas.transform, false);
        bg.transform.SetAsFirstSibling(); // Behind everything
        
        RectTransform rt = bg.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        
        Image img = bg.AddComponent<Image>();
        img.color = backgroundColor;
        img.raycastTarget = false; // Don't block clicks
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
        
        // Fade out current screen
        if (_currentScreen != null)
            yield return StartCoroutine(_currentScreen.FadeOut(fadeDuration));
        
        // Fade in new screen
        _currentScreen = target;
        yield return StartCoroutine(_currentScreen.FadeIn(fadeDuration));
        
        _transitioning = false;
    }
}
