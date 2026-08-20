// Assets/Localization/LanguageSettings.cs
using UnityEngine;

/// <summary>
/// Single source of truth for which <see cref="Language"/> the game starts in.
///
/// Resolution order:
///   1. The player's saved choice (PlayerPrefs "Settings_Language").
///   2. The device language — Japanese devices start in Japanese.
///   3. The supplied fallback (English).
/// </summary>
public static class LanguageSettings
{
    /// <summary>PlayerPrefs key holding the player's explicit language choice.</summary>
    public const string PlayerPrefsKey = "Settings_Language";

    /// <summary>True when the player has explicitly picked a language at least once.</summary>
    public static bool HasSavedLanguage => TryGetSavedLanguage(out _);

    /// <summary>
    /// Read the player's saved language choice. Returns false on a fresh install or if
    /// the stored value is not a valid <see cref="Language"/>.
    /// </summary>
    public static bool TryGetSavedLanguage(out Language language)
    {
        language = Language.English;

        string saved = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
        if (string.IsNullOrEmpty(saved))
            return false;

        return System.Enum.TryParse(saved, out language) && System.Enum.IsDefined(typeof(Language), language);
    }

    /// <summary>Persist the player's explicit language choice.</summary>
    public static void SaveLanguage(Language language)
    {
        PlayerPrefs.SetString(PlayerPrefsKey, language.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>Forget the saved choice — the next startup falls back to the device language.</summary>
    public static void ClearSavedLanguage()
    {
        PlayerPrefs.DeleteKey(PlayerPrefsKey);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Map a device/system language onto a supported <see cref="Language"/>.
    /// Only <see cref="SystemLanguage.Japanese"/> maps to Japanese; every other device
    /// language falls back (English).
    /// </summary>
    public static Language FromSystemLanguage(SystemLanguage systemLanguage, Language fallback = Language.English)
    {
        return systemLanguage == SystemLanguage.Japanese ? Language.Japanese : fallback;
    }

    /// <summary>
    /// The language the game should boot in: saved choice first, then device language,
    /// then <paramref name="fallback"/>.
    /// </summary>
    public static Language ResolveStartupLanguage(Language fallback = Language.English, bool useDeviceLanguage = true)
    {
        if (TryGetSavedLanguage(out Language saved))
            return saved;

        return useDeviceLanguage ? FromSystemLanguage(Application.systemLanguage, fallback) : fallback;
    }
}
