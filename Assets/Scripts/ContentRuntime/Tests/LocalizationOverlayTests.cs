using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// <c>LocalizationManager.ApplyOverlay</c> — the ~15 lines that make an admin-published string
    /// reach the game. The invariants under test are the ones that decide whether this feature can
    /// BREAK an installed build: the bundled table is the floor (I1), untouched keys stay untouched,
    /// and a blank remote value never wins over a good bundled one.
    /// </summary>
    public class LocalizationOverlayTests
    {
        private LocalizationTextTable _table;
        private Language _savedLanguage;

        [SetUp]
        public void SetUp()
        {
            _savedLanguage = LocalizationManager.CurrentLanguage;

            _table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            _table.rows.Add(new LocalizedTextRow { key = "BTN_START", english = "PLAY",  japanese = "プレイ" });
            _table.rows.Add(new LocalizedTextRow { key = "BTN_QUIT",  english = "QUIT",  japanese = "終了" });

            LocalizationManager.Initialize(_table, Language.English);
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationManager.Initialize(_table, _savedLanguage);
            Object.DestroyImmediate(_table);
        }

        private static Dictionary<string, LocalizedTextRow> Overlay(params LocalizedTextRow[] rows)
        {
            var map = new Dictionary<string, LocalizedTextRow>();
            foreach (var r in rows) map[r.key] = r;
            return map;
        }

        [Test]
        public void ApplyOverlay_ReplacesAnExistingKey()
        {
            int applied = LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF", japanese = "ティーオフ" }));

            Assert.AreEqual(1, applied);
            Assert.AreEqual("TEE OFF", LocalizationManager.Get("BTN_START"));
        }

        [Test]
        public void ApplyOverlay_LeavesKeysItDoesNotName_Untouched()
        {
            LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF" }));

            Assert.AreEqual("QUIT", LocalizationManager.Get("BTN_QUIT"),
                "I1 — the bundled table is the FLOOR, never replaced. An overlay touches only the " +
                "ids it carries.");
        }

        [Test]
        public void ApplyOverlay_AddsAnUnknownKey_Harmlessly()
        {
            LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_FUTURE", english = "SOON" }));

            Assert.AreEqual("SOON", LocalizationManager.Get("BTN_FUTURE"),
                "An id no call site reads is inert; refusing it would block copy landing ahead of code.");
        }

        [Test]
        public void ApplyOverlay_JapaneseValue_RendersInJapanese()
        {
            LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF", japanese = "ティーオフ" }));

            LocalizationManager.SetLanguage(Language.Japanese);
            Assert.AreEqual("ティーオフ", LocalizationManager.Get("BTN_START"));

            LocalizationManager.SetLanguage(Language.English);
            Assert.AreEqual("TEE OFF", LocalizationManager.Get("BTN_START"),
                "Switching language mid-session must keep working over an overlaid row.");
        }

        [Test]
        public void ApplyOverlay_RowWithNoJapanese_FallsBackToEnglish()
        {
            LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF", japanese = "" }));

            LocalizationManager.SetLanguage(Language.Japanese);
            Assert.AreEqual("TEE OFF", LocalizationManager.Get("BTN_START"),
                "Get()'s JA→EN fallback is exactly why an empty english is refused.");
        }

        [Test]
        public void ApplyOverlay_BlankEnglish_IsSkipped_AndTheBundledStringSurvives()
        {
            int applied = LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "", japanese = "プレイ" }));

            Assert.AreEqual(0, applied);
            Assert.AreEqual("PLAY", LocalizationManager.Get("BTN_START"),
                "A blank remote string is worse than the bundled one — a player would see an empty " +
                "button with no way to recover until the next publish.");
        }

        [Test]
        public void ApplyOverlay_NullOrEmpty_IsANoOp_AndFiresNothing()
        {
            int fired = 0;
            void Handler() => fired++;
            LocalizationManager.OnLanguageChanged += Handler;
            try
            {
                Assert.AreEqual(0, LocalizationManager.ApplyOverlay(null));
                Assert.AreEqual(0, LocalizationManager.ApplyOverlay(new Dictionary<string, LocalizedTextRow>()));
            }
            finally { LocalizationManager.OnLanguageChanged -= Handler; }

            Assert.AreEqual(0, fired, "An empty overlay repainting every open screen would be churn for nothing.");
            Assert.AreEqual("PLAY", LocalizationManager.Get("BTN_START"));
        }

        [Test]
        public void ApplyOverlay_FiresLanguageChanged_SoOpenScreensRepaint()
        {
            int fired = 0;
            void Handler() => fired++;
            LocalizationManager.OnLanguageChanged += Handler;
            try
            {
                LocalizationManager.ApplyOverlay(
                    Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF" }));
            }
            finally { LocalizationManager.OnLanguageChanged -= Handler; }

            Assert.AreEqual(1, fired,
                "A LocalizedText whose OnEnable already ran holds a resolved string; without this " +
                "event it keeps the bundled copy for the life of the screen.");
        }

        [Test]
        public void ApplyOverlay_SkipsNullRowsAndEmptyKeys_WithoutThrowing()
        {
            var overlay = new Dictionary<string, LocalizedTextRow>
            {
                { "",          new LocalizedTextRow { key = "", english = "E" } },
                { "NULL_ROW",  null },
                { "GOOD",      new LocalizedTextRow { key = "GOOD", english = "G" } },
            };

            Assert.AreEqual(1, LocalizationManager.ApplyOverlay(overlay));
            Assert.AreEqual("G", LocalizationManager.Get("GOOD"));
        }

        [Test]
        public void Initialize_AfterApplyOverlay_WipesIt_WhichIsWhyOrderMatters()
        {
            LocalizationManager.ApplyOverlay(
                Overlay(new LocalizedTextRow { key = "BTN_START", english = "TEE OFF" }));
            Assert.AreEqual("TEE OFF", LocalizationManager.Get("BTN_START"));

            LocalizationManager.Initialize(_table, Language.English);

            Assert.AreEqual("PLAY", LocalizationManager.Get("BTN_START"),
                "THE failure mode this task's execution order exists to prevent: Initialize rebuilds " +
                "_textMap from scratch, so an overlay applied BEFORE it is silently wiped and the " +
                "game just shows bundled strings. ContentService (-900) must stay after " +
                "LocalizationBootstrap (-1000), and asserts LocalizationManager.IsInitialized to prove it.");
        }

        [Test]
        public void IsInitialized_IsTheOrderSignalContentServiceAssertsOn()
        {
            Assert.IsTrue(LocalizationManager.IsInitialized,
                "SetUp called Initialize; if this were false ContentService would refuse to apply " +
                "the overlay and log an execution-order error rather than let it be wiped.");
        }
    }
}
