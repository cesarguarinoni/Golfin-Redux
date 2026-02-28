using UnityEngine;  
using UnityEngine.UI;  
using TMPro;  
using System.Collections;  
using System.Collections.Generic;  
  
/// <summary>  
/// Settings screen with accordion-style expandable sections.  
/// Categories: User Profile, Language, Sound, Terms, Privacy, FAQ, About, Contact, Log Out  
/// </summary>  
public class SettingsScreen : ScreenBase
{
    [SerializeField] private CanvasGroup canvasGroup;  
    [SerializeField] private ScrollRect scrollRect;  
    [SerializeField] private Transform contentContainer;  
    [SerializeField] private PressableButton closeButton;  
  
    [Header("Accordion Sections")] 
    [SerializeField] private SettingsSection userProfileSection;  
    [SerializeField] private SettingsSection languageSection;  
    [SerializeField] private SettingsSection soundSection;  
    [SerializeField] private SettingsSection termsSection;  
    [SerializeField] private SettingsSection privacySection;  
    [SerializeField] private SettingsSection faqSection;  
    [SerializeField] private SettingsSection aboutSection;  
    [SerializeField] private SettingsSection contactSection;  
    [SerializeField] private PressableButton logoutButton;  
 
  
    private SettingsSection currentOpenSection;  
    private List<SettingsSection> allSections;  
    private Coroutine fadeCoroutine;  
  
    public override void OnScreenEnter()  
        InitializeSettings();  
        StartFadeInAnimation();  
        SetupButtons();  
    } 
  
    private void InitializeSettings()  
        allSections = new List<SettingsSection>()  
            userProfileSection, languageSection, soundSection,  
            termsSection, privacySection, faqSection,  
            aboutSection, contactSection  
        };  
  
        // Initialize all sections as collapsed  
        foreach (var section in allSections)  
            if (section != null)  
                section.SetCollapsed(true);  
                section.OnSectionClicked += OnSectionClicked;  
            }  
        }  
    } 
  
    private void StartFadeInAnimation()  
        if (canvasGroup != null)  
            canvasGroup.alpha = 0f;  
            fadeCoroutine = StartCoroutine(FadeIn());  
        }  
    }  
  
    private IEnumerator FadeIn()  
        float duration = 0.3f;  
        float elapsed = 0f;  
        while (elapsed < duration)  
            elapsed += Time.deltaTime;  
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);  
            yield return null;  
        }  
        canvasGroup.alpha = 1f;  
    } 
  
    private void SetupButtons()  
        if (closeButton != null)  
            closeButton.onClick.AddListener(OnCloseButtonClicked);  
        }  
  
        if (logoutButton != null)  
            logoutButton.onClick.AddListener(OnLogoutButtonClicked);  
        }  
    }  
  
    private void OnSectionClicked(SettingsSection section)  
        // Close current open section if different  
        if (currentOpenSection != null && currentOpenSection != section)  
            currentOpenSection.SetCollapsed(true);  
        }  
  
        // Toggle clicked section  
        bool wasCollapsed = section.IsCollapsed();  
        section.SetCollapsed(!wasCollapsed);  
        currentOpenSection = wasCollapsed ? section : null;  
    } 
  
    private void OnCloseButtonClicked()  
        // Save current settings  
        SaveSettings();  
  
        // Return to HomeScreen  
        var screenManager = FindObjectOfType<ScreenManager>();  
        if (screenManager != null)  
            screenManager.ShowScreen<HomeScreen>();  
        }  
    }  
  
    private void OnLogoutButtonClicked()  
        // Clear save data and return to login/splash  
        SaveManager.ClearAllData();  
  
        var screenManager = FindObjectOfType<ScreenManager>();  
        if (screenManager != null)  
            // TODO: Go to login screen when available  
            screenManager.ShowScreen<HomeScreen>();  
        }  
    } 
  
    private void SaveSettings()  
        // Save settings to PlayerPrefs (local save for now)  
        Debug.Log("[GOLFIN] Settings saved locally");  
        PlayerPrefs.Save();  
    }  
  
    public override void OnScreenExit()  
        if (fadeCoroutine != null)  
            StopCoroutine(fadeCoroutine);  
        }  
  
        // Clean up event listeners  
        foreach (var section in allSections)  
            if (section != null)  
                section.OnSectionClicked -= OnSectionClicked;  
            }  
        }  
    }  
} 
