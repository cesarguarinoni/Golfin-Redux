using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// Singleton that loads localization data from a CSV in Resources.
/// CSV format: key,en,ja,es (first row = headers with language codes)
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }
    
    [Header("Settings")]
    [SerializeField] private string csvFileName = "Data/localization";
    [SerializeField] private string defaultLanguage = "en";
    
    public string CurrentLanguage { get; private set; }
    public event Action OnLanguageChanged;
    
    // key -> (lang -> value)
    private Dictionary<string, Dictionary<string, string>> _data = new();
    private string[] _languages;
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        CurrentLanguage = defaultLanguage;
        LoadCSV();
    }
    
    private void LoadCSV()
    {
        var asset = Resources.Load<TextAsset>(csvFileName);
        if (asset == null) { Debug.LogError($"[Localization] CSV '{csvFileName}' not found in Resources!"); return; }
        
        var lines = asset.text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return;
        
        // Parse header row
        _languages = ParseCSVLine(lines[0]);
        
        // Parse data rows
        for (int i = 1; i < lines.Length; i++)
        {
            var cols = ParseCSVLine(lines[i]);
            if (cols.Length < 2) continue;
            
            string key = cols[0].Trim();
            var translations = new Dictionary<string, string>();
            
            for (int j = 1; j < cols.Length && j < _languages.Length; j++)
                translations[_languages[j].Trim()] = cols[j].Trim();
            
            _data[key] = translations;
        }
        
        Debug.Log($"[Localization] Loaded {_data.Count} keys, {_languages.Length - 1} languages");
    }
    
    /// <summary>Parse a CSV line respecting quoted fields</summary>
    private string[] ParseCSVLine(string line)
    {
        var result = new List<string>();
        bool inQuotes = false;
        string current = "";
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ',' && !inQuotes) { result.Add(current); current = ""; continue; }
            current += c;
        }
        result.Add(current);
        return result.ToArray();
    }
    
    /// <summary>Get localized text for a key in current language</summary>
    public string GetText(string key)
    {
        if (_data.TryGetValue(key, out var translations))
        {
            if (translations.TryGetValue(CurrentLanguage, out var text)) return text;
            if (translations.TryGetValue(defaultLanguage, out var fallback)) return fallback;
        }
        return $"[{key}]";
    }
    
    /// <summary>Get localized text with string.Format arguments</summary>
    public string GetText(string key, params object[] args)
    {
        return string.Format(GetText(key), args);
    }
    
    /// <summary>Change the active language and notify listeners</summary>
    public void SetLanguage(string langCode)
    {
        CurrentLanguage = langCode;
        OnLanguageChanged?.Invoke();
    }
    
    /// <summary>Get available language codes</summary>
    public string[] GetAvailableLanguages() => _languages?.Skip(1).ToArray() ?? Array.Empty<string>();
}
