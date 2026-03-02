using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GolfinRedux.UI
{
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        [SerializeField] private TextAsset _csvFile;
        [SerializeField] private string _defaultLanguage = "en";

        private readonly Dictionary<string, Dictionary<string, string>> _table = new();
        private string _currentLanguage;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_csvFile != null)
            {
                LoadCsv(_csvFile.text);
            }

            _currentLanguage = _defaultLanguage;
        }

        public void SetLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
        }

        public string GetText(string key)
        {
            if (_table.TryGetValue(key, out var perLang))
            {
                if (perLang.TryGetValue(_currentLanguage, out var value))
                    return value;

                if (perLang.TryGetValue(_defaultLanguage, out var fallback))
                    return fallback;
            }

            return key;
        }

        private void LoadCsv(string csvText)
        {
            using var reader = new StringReader(csvText);
            var header = reader.ReadLine();
            if (string.IsNullOrEmpty(header)) return;

            var columns = header.Split(',');
            // columns[0] = key, others = language codes

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                var key = parts[0].Trim();
                if (string.IsNullOrEmpty(key)) continue;

                if (!_table.TryGetValue(key, out var perLang))
                {
                    perLang = new Dictionary<string, string>();
                    _table[key] = perLang;
                }

                for (int i = 1; i < parts.Length && i < columns.Length; i++)
                {
                    var lang = columns[i].Trim();
                    if (string.IsNullOrEmpty(lang)) continue;
                    perLang[lang] = parts[i].Trim();
                }
            }
        }
    }
}
