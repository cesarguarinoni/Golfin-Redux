using UnityEngine;
using System.Collections;

/// <summary>
/// Abstract base class for all screens. Each screen has a CanvasGroup for fading.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class ScreenBase : MonoBehaviour
{
    protected CanvasGroup canvasGroup;
    
    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }
    
    /// <summary>Called when this screen becomes the active screen</summary>
    public virtual void OnScreenEnter() { }
    
    /// <summary>Called when this screen is about to be hidden</summary>
    public virtual void OnScreenExit() { }
    
    /// <summary>Show this screen instantly</summary>
    public void Show()
    {
        gameObject.SetActive(true);
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
    }
    
    /// <summary>Hide this screen instantly</summary>
    public void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }
    
    /// <summary>Fade in over duration</summary>
    public IEnumerator FadeIn(float duration)
    {
        gameObject.SetActive(true);
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        OnScreenEnter();
    }
    
    /// <summary>Fade out over duration</summary>
    public IEnumerator FadeOut(float duration)
    {
        OnScreenExit();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        
        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }
}
