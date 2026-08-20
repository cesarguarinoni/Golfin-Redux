// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Inventory.Tests — ClubInfoTextTests
// The club description ladder: infoJa (JP only) → info → "".
// Mirrors DescriptionLadderTests (TournamentsRuntime/Tests), including its
// language save/restore discipline.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Inventory.Tests
{
    [TestFixture]
    public class ClubInfoTextTests
    {
        private static FieldInfo TextMapField =>
            typeof(LocalizationManager).GetField("_textMap", BindingFlags.NonPublic | BindingFlags.Static)!;

        private object?  _savedTextMap;
        private Language _savedLanguage;

        [SetUp]
        public void SetUp()
        {
            _savedTextMap  = TextMapField.GetValue(null);
            _savedLanguage = LocalizationManager.CurrentLanguage;
        }

        [TearDown]
        public void TearDown()
        {
            // Restore through Initialize, not SetLanguage, so no OnLanguageChanged fires at
            // whatever UI happens to be alive in the editor.
            LocalizationManager.Initialize(
                ScriptableObject.CreateInstance<LocalizationTextTable>(), _savedLanguage);
            TextMapField.SetValue(null, _savedTextMap);
        }

        private static void Use(Language language) =>
            LocalizationManager.Initialize(ScriptableObject.CreateInstance<LocalizationTextTable>(), language);

        private static string Resolve(string? info, string? ja) => ClubRosterProd.ResolveInfo(info, ja);

        // ── The two rungs ─────────────────────────────────────────────────────

        [Test]
        public void JapanesePlayer_GetsTheJapaneseColumn()
        {
            Use(Language.Japanese);
            Assert.AreEqual("和文の説明", Resolve("English blurb", "和文の説明"));
        }

        [Test]
        public void EnglishPlayer_GetsTheEnglishColumn()
        {
            Use(Language.English);
            Assert.AreEqual("English blurb", Resolve("English blurb", "和文の説明"));
        }

        /// <summary>
        /// The JP-only asymmetry, deliberate and shared with TournamentDescription: an English
        /// player must never be shown Japanese copy, even when the English column is empty. They
        /// fall through to "" and the info row collapses.
        /// </summary>
        [Test]
        public void EnglishPlayer_NeverSeesJapanese_EvenWithNoEnglishCopy()
        {
            Use(Language.English);
            Assert.AreEqual(string.Empty, Resolve("", "和文の説明"));
            Assert.AreEqual(string.Empty, Resolve(null, "和文の説明"));
        }

        /// <summary>
        /// The case that matters right now: the 7 legacy rows carry no info_ja. A Japanese player
        /// must fall back to English rather than see a blank panel.
        /// </summary>
        [Test]
        public void JapanesePlayer_FallsBackToEnglish_WhenJapaneseColumnIsBlank()
        {
            Use(Language.Japanese);
            Assert.AreEqual("English blurb", Resolve("English blurb", ""));
            Assert.AreEqual("English blurb", Resolve("English blurb", null));
            Assert.AreEqual("English blurb", Resolve("English blurb", "   "));
        }

        [Test]
        public void BothColumnsBlank_ReturnsEmpty_NeverNull()
        {
            Use(Language.English);
            Assert.AreEqual(string.Empty, Resolve(null, null));
            Use(Language.Japanese);
            Assert.AreEqual(string.Empty, Resolve("", ""));
        }

        [Test]
        public void ResolvedCopy_IsTrimmed()
        {
            Use(Language.Japanese);
            Assert.AreEqual("和文の説明", Resolve("English blurb", "  和文の説明  "));
            Use(Language.English);
            Assert.AreEqual("English blurb", Resolve("  English blurb  ", null));
        }
    }
}
