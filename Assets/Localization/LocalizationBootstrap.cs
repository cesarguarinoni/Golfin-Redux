// Assets/Scripts/Localization/LocalizationBootstrap.cs
using UnityEngine;

public class LocalizationBootstrap : MonoBehaviour
{
    [SerializeField] private LocalizationTextTable textTable;

    [Tooltip("Language used when the player has no saved choice and the device language is not supported.")]
    [SerializeField] private Language defaultLanguage = Language.English;

    [Tooltip("On a fresh install, start in the device's language (Japanese device -> Japanese, anything else -> the default above).")]
    [SerializeField] private bool useDeviceLanguage = true;

    private void Awake()
    {
        if (textTable == null)
        {
            Debug.LogError("LocalizationBootstrap: TextTable is not assigned.");
            return;
        }

        Language startupLanguage = LanguageSettings.ResolveStartupLanguage(defaultLanguage, useDeviceLanguage);

        LocalizationManager.Initialize(textTable, startupLanguage);

        Debug.Log($"[LocalizationBootstrap] Startup language: {startupLanguage} " +
                  $"(saved={(LanguageSettings.HasSavedLanguage ? "yes" : "no")}, device={Application.systemLanguage})");
    }

    // Called only from the editor window
    public void SetDefaultLanguageEditor(Language lang)
    {
        defaultLanguage = lang;
    }
}
