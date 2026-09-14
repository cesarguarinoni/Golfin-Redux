// ─────────────────────────────────────────────────────────────────────────────
// TournamentVenueClubNameTests — finished_tournament_leaderboard_route F3 (EditMode)
//
// TournamentVenueLine.ClubName is the club half of the venue row, for the tournament hole
// cards ("{Club} - Hole 3 - Par 4"). It rides the same ladder as Resolve — the localized
// tourn.venue.* row in the current language, else the club id — and cuts at the row's own
// separator. Reached by REFLECTION through the shared `Prod` helper, exactly like
// TournamentDescriptionTests; the localization table is installed and restored the same way.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.WireupTests
{
    public class VenueClubNameTests
    {
        private static readonly Type VenueType = Prod.Find("Golfin.Tournaments.TournamentVenueLine");

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
            // whatever UI is alive in the editor (DescriptionLadderTests' discipline).
            LocalizationManager.Initialize(
                ScriptableObject.CreateInstance<LocalizationTextTable>(), _savedLanguage);
            TextMapField.SetValue(null, _savedTextMap);
        }

        private static string ClubName(string? clubId, int holeCount)
        {
            var m = VenueType.GetMethod("ClubName", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(int) }, null)!;
            return (string)m.Invoke(null, new object?[] { clubId, holeCount })!;
        }

        private static string Resolve(string? clubId, int holeCount)
        {
            var m = VenueType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(int) }, null)!;
            return (string)m.Invoke(null, new object?[] { clubId, holeCount })!;
        }

        private static void Install(Language language,
                                    params (string Key, string English, string Japanese)[] rows)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            foreach (var (key, english, japanese) in rows)
                table.rows.Add(new LocalizedTextRow { key = key, english = english, japanese = japanese });
            LocalizationManager.Initialize(table, language);
        }

        private static readonly (string, string, string) KisarazuRow =
            ("tourn.venue.kisarazu", "Kisarazu Higashi CC · 18 Holes", "木更津東カントリークラブ · 18ホール");

        [Test]
        public void Localized_row_yields_the_club_before_the_separator_in_English()
        {
            Install(Language.English, KisarazuRow);
            Assert.AreEqual("Kisarazu Higashi CC", ClubName("kisarazu", 18));
        }

        [Test]
        public void Localized_row_yields_the_club_before_the_separator_in_Japanese()
        {
            Install(Language.Japanese, KisarazuRow);
            Assert.AreEqual("木更津東カントリークラブ", ClubName("kisarazu", 18));
        }

        [Test]
        public void Unlocalized_club_falls_back_to_the_id_without_the_count()
        {
            Install(Language.English);   // no venue rows at all
            // Resolve's fallback is "{id}  -  18 Holes"; the club half is the id.
            StringAssert.StartsWith("new_course", Resolve("new_course", 18));
            Assert.AreEqual("new_course", ClubName("new_course", 18));
        }

        [Test]
        public void Row_without_a_separator_is_returned_whole()
        {
            Install(Language.English, ("tourn.venue.plain", "Plain Links", "プレーン"));
            Assert.AreEqual("Plain Links", ClubName("plain", 9));
        }

        [Test]
        public void Empty_club_is_empty()
        {
            Install(Language.English, KisarazuRow);
            Assert.AreEqual(string.Empty, ClubName("", 18));
            Assert.AreEqual(string.Empty, ClubName(null, 18));
        }
    }
}
