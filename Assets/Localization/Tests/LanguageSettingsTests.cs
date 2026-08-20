using NUnit.Framework;
using UnityEngine;

namespace Golfin.Localization.Tests
{
    /// <summary>
    /// Startup-language resolution: saved player choice wins, otherwise the device
    /// language decides (Japanese -> Japanese, anything else -> English).
    /// </summary>
    public class LanguageSettingsTests
    {
        private bool _hadSaved;
        private string _saved;

        [SetUp]
        public void SetUp()
        {
            _hadSaved = PlayerPrefs.HasKey(LanguageSettings.PlayerPrefsKey);
            _saved = PlayerPrefs.GetString(LanguageSettings.PlayerPrefsKey, string.Empty);
            LanguageSettings.ClearSavedLanguage();
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadSaved)
                PlayerPrefs.SetString(LanguageSettings.PlayerPrefsKey, _saved);
            else
                PlayerPrefs.DeleteKey(LanguageSettings.PlayerPrefsKey);

            PlayerPrefs.Save();
        }

        [Test]
        public void JapaneseDevice_MapsToJapanese()
        {
            Assert.AreEqual(Language.Japanese, LanguageSettings.FromSystemLanguage(SystemLanguage.Japanese));
        }

        [TestCase(SystemLanguage.English)]
        [TestCase(SystemLanguage.French)]
        [TestCase(SystemLanguage.ChineseSimplified)]
        [TestCase(SystemLanguage.Korean)]
        [TestCase(SystemLanguage.Unknown)]
        public void OtherDevices_MapToEnglish(SystemLanguage systemLanguage)
        {
            Assert.AreEqual(Language.English, LanguageSettings.FromSystemLanguage(systemLanguage));
        }

        [Test]
        public void FreshInstall_HasNoSavedLanguage()
        {
            Assert.IsFalse(LanguageSettings.HasSavedLanguage);
            Assert.IsFalse(LanguageSettings.TryGetSavedLanguage(out _));
        }

        [Test]
        public void SavedChoice_WinsOverDeviceLanguage()
        {
            LanguageSettings.SaveLanguage(Language.English);

            Assert.IsTrue(LanguageSettings.TryGetSavedLanguage(out Language saved));
            Assert.AreEqual(Language.English, saved);
            // Device language is irrelevant once the player has chosen.
            Assert.AreEqual(Language.English, LanguageSettings.ResolveStartupLanguage());

            LanguageSettings.SaveLanguage(Language.Japanese);
            Assert.AreEqual(Language.Japanese, LanguageSettings.ResolveStartupLanguage());
        }

        [Test]
        public void CorruptSavedValue_IsIgnored()
        {
            PlayerPrefs.SetString(LanguageSettings.PlayerPrefsKey, "Klingon");
            PlayerPrefs.Save();

            Assert.IsFalse(LanguageSettings.TryGetSavedLanguage(out _));
            // Falls through to the device language rather than honouring the junk value.
            Assert.AreEqual(
                LanguageSettings.FromSystemLanguage(Application.systemLanguage),
                LanguageSettings.ResolveStartupLanguage());
        }

        [Test]
        public void DeviceLanguageDisabled_UsesFallback()
        {
            Assert.AreEqual(
                Language.English,
                LanguageSettings.ResolveStartupLanguage(Language.English, useDeviceLanguage: false));
        }

        [Test]
        public void ClearSavedLanguage_FallsBackToDeviceLanguage()
        {
            LanguageSettings.SaveLanguage(Language.Japanese);
            LanguageSettings.ClearSavedLanguage();

            Assert.IsFalse(LanguageSettings.HasSavedLanguage);
            Assert.AreEqual(
                LanguageSettings.FromSystemLanguage(Application.systemLanguage),
                LanguageSettings.ResolveStartupLanguage());
        }
    }
}
