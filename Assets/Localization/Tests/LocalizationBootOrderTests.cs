using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Golfin.Localization.Tests
{
    /// <summary>
    /// Boot-order guarantees. A label that woke up before LocalizationBootstrap ran must still
    /// end up in the startup language — the failure these cover is a screen that silently stays
    /// English (or shows raw keys) because it refreshed too early and no event ever reached it.
    /// </summary>
    public class LocalizationBootOrderTests
    {
        private LocalizationTextTable _table;
        private Language _saved;

        [SetUp]
        public void SetUp()
        {
            _saved = LocalizationManager.CurrentLanguage;
            _table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            _table.rows.Add(new LocalizedTextRow { key = "TEST_HELLO", english = "Hello", japanese = "こんにちは" });
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationManager.Initialize(_table, _saved);
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void Initialize_FiresLanguageChanged_SoEarlyLabelsCatchUp()
        {
            int fired = 0;
            void Handler() => fired++;

            LocalizationManager.OnLanguageChanged += Handler;
            try
            {
                LocalizationManager.Initialize(_table, Language.Japanese);
            }
            finally
            {
                LocalizationManager.OnLanguageChanged -= Handler;
            }

            Assert.AreEqual(1, fired, "Initialize must notify subscribers, or a label whose OnEnable " +
                                      "already ran keeps whatever it resolved before the table existed.");
        }

        [Test]
        public void SetLanguage_ToStartupLanguage_DoesNotFire_WhichIsWhyInitializeMust()
        {
            LocalizationManager.Initialize(_table, Language.Japanese);

            int fired = 0;
            void Handler() => fired++;

            LocalizationManager.OnLanguageChanged += Handler;
            try
            {
                LocalizationManager.SetLanguage(Language.Japanese); // already Japanese — early-returns
            }
            finally
            {
                LocalizationManager.OnLanguageChanged -= Handler;
            }

            Assert.AreEqual(0, fired, "SetLanguage short-circuits on an unchanged language, so it can " +
                                     "never be the mechanism that rescues an early label.");
        }

        [Test]
        public void LocalizedText_Refresh_WorksBeforeAwakeHasRun()
        {
            LocalizationManager.Initialize(_table, Language.Japanese);

            // Instantiated inactive: Awake never runs, so the cached label reference is null.
            var go = new GameObject("label", typeof(TextMeshProUGUI));
            go.SetActive(false);
            var loc = go.AddComponent<LocalizedText>();
            try
            {
                loc.SetKey("TEST_HELLO");
                Assert.AreEqual("こんにちは", go.GetComponent<TextMeshProUGUI>().text,
                    "SetKey/Refresh must resolve the label lazily; otherwise it silently no-ops " +
                    "whenever it is called before Awake.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
