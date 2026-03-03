using System;
using System.Collections.Generic;
using UnityEngine;

public static class LocalizationManager
{
    private static LocalizationTextTable _textTable;
    private static Dictionary<string, LocalizedTextRow> _textMap;

    public static Language CurrentLanguage { get; private set; } = Language.English;

    // Fired when language changes (for runtime refresh)
    public static event Action OnLanguageChanged;

    public static void Initialize(LocalizationTextTable table, Language defaultLanguage)
    {
        _textTable = table;
        CurrentLanguage = defaultLanguage;

        _textMap = new Dictionary<string, LocalizedTextRow>();
        foreach (var row in _textTable.rows)
        {
            if (!string.IsNullOrEmpty(row.key))
                _textMap[row.key] = row;
        }
    }

    public static void SetLanguage(Language language)
    {
        if (CurrentLanguage == language)
            return;

        CurrentLanguage = language;
        OnLanguageChanged?.Invoke();
    }

    public static string Get(string key)
    {
        if (_textMap == null || !_textMap.TryGetValue(key, out var row))
            return key; // fallback: key

        return CurrentLanguage switch
        {
            Language.Japanese => string.IsNullOrEmpty(row.japanese) ? row.english : row.japanese,
            _ => row.english
        };
    }
}
