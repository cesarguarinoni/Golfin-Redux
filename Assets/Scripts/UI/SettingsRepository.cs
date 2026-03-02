using System.IO;
using UnityEngine;

namespace GolfinRedux.UI
{
    [System.Serializable]
    public class UserSettings
    {
        public string language = "en";
        public string username = "Player";
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
    }

    public static class SettingsRepository
    {
        private const string FileName = "user_settings.json";
        private static UserSettings _cache;

        public static UserSettings Load()
        {
            if (_cache != null) return _cache;

            var path = Path.Combine(Application.persistentDataPath, FileName);
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _cache = JsonUtility.FromJson<UserSettings>(json) ?? new UserSettings();
            }
            else
            {
                _cache = new UserSettings();
            }

            return _cache;
        }

        public static void Save(UserSettings settings)
        {
            _cache = settings;
            var path = Path.Combine(Application.persistentDataPath, FileName);
            var json = JsonUtility.ToJson(settings, true);
            File.WriteAllText(path, json);
        }
    }
}
