// Assets/Scripts/Localization/LocalizationBootstrap.cs
using UnityEngine;

public class LocalizationBootstrap : MonoBehaviour
{
    [SerializeField] private LocalizationTextTable textTable;
    [SerializeField] private Language defaultLanguage = Language.English;

    private void Awake()
    {
        if (textTable == null)
        {
            Debug.LogError("LocalizationBootstrap: TextTable is not assigned.");
            return;
        }

        LocalizationManager.Initialize(textTable, defaultLanguage);
    }

    // Called only from the editor window
    public void SetDefaultLanguageEditor(Language lang)
    {
        defaultLanguage = lang;
    }
}
