// ─────────────────────────────────────────────────────────────────────────────
// TournamentDescriptionTests — the blurb ladder (tournament_signup_modal §3.2, §9)
//
// ASSEMBLY: Golfin.TournamentsRuntime.Tests — the SAME asmdef as the name-ladder
// tests in RemoteScheduleTests.cs, as the spec requires. TournamentDescription
// lives in Assembly-CSharp, which an asmdef cannot reference, so it is reached by
// REFLECTION through the shared `Prod` helper, exactly like TournamentDisplayName.
//
// COVERAGE
//   §1  All four rungs of localize(key) → ja (JP only) → en → ""
//   §2  The JP-only asymmetry — an EN player NEVER sees the Japanese blurb
//   §3  No raw key ever renders, and there is no id rung
//   §4  Mapper pass-through — the three columns reach TournamentDefinition
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using Golfin.Tournaments;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Tournaments.WireupTests
{
    public class DescriptionLadderTests
    {
        private static readonly Type DescriptionType =
            Prod.Find("Golfin.Tournaments.TournamentDescription");

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
            // Same discipline as DisplayNameLadderTests: restore through Initialize, not
            // SetLanguage, so no OnLanguageChanged fires at whatever UI is alive in the editor.
            LocalizationManager.Initialize(
                ScriptableObject.CreateInstance<LocalizationTextTable>(), _savedLanguage);
            TextMapField.SetValue(null, _savedTextMap);
        }

        // ── Reflection wrappers ───────────────────────────────────────────────

        private static string Resolve(string? key, string? en, string? ja)
        {
            var m = DescriptionType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(string), typeof(string), typeof(string) }, null)!;
            return (string)m.Invoke(null, new object?[] { key, en, ja })!;
        }

        private static string Resolve(TournamentDefinition? def)
        {
            var m = DescriptionType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(TournamentDefinition) }, null)!;
            return (string)m.Invoke(null, new object?[] { def })!;
        }

        private static void Install(Language language,
                                    params (string Key, string English, string Japanese)[] rows)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            foreach (var (key, english, japanese) in rows)
                table.rows.Add(new LocalizedTextRow { key = key, english = english, japanese = japanese });
            LocalizationManager.Initialize(table, language);
        }

        private static TournamentDefinition Def(string? key, string? en, string? ja) =>
            new TournamentDefinition(
                id: "lomond_championship", nameKey: "tourn.lomond", clubId: "lomond",
                holeSet: new[] { "1" }, startUtc: DateTime.UtcNow, endUtc: DateTime.UtcNow.AddDays(3),
                resolveDelayMinutes: 30, entryFeeRP: 500, prizeTableId: "lomond_championship",
                botFieldId: "field_major", sponsorKey: "GOLFIN", leagueKey: "DIAMOND",
                descriptionEn: en, descriptionJa: ja, descriptionKey: key);

        // ── §1 The four rungs ─────────────────────────────────────────────────

        [Test]
        public void Rung1_ResolvingKeyWinsInBothLanguages()
        {
            // A shipped key is a real translation PAIR, so it outranks operator copy in BOTH
            // languages — not only the one that happens to be missing.
            Install(Language.English, ("tourn.desc.lomond", "Shipped EN blurb", "出荷済みJA紹介文"));
            Assert.AreEqual("Shipped EN blurb",
                Resolve("tourn.desc.lomond", "Operator EN", "運営JA"));

            Install(Language.Japanese, ("tourn.desc.lomond", "Shipped EN blurb", "出荷済みJA紹介文"));
            Assert.AreEqual("出荷済みJA紹介文",
                Resolve("tourn.desc.lomond", "Operator EN", "運営JA"));
        }

        [Test]
        public void Rung2_JapanesePlayerGetsTheJapaneseColumn()
        {
            Install(Language.Japanese);
            Assert.AreEqual("運営JA", Resolve(null, "Operator EN", "運営JA"));
        }

        [Test]
        public void Rung3_EnglishPlayerGetsTheEnglishColumn()
        {
            Install(Language.English);
            Assert.AreEqual("Operator EN", Resolve(null, "Operator EN", "運営JA"));
        }

        [Test]
        public void Rung3_JapanesePlayerFallsToEnglishWhenThereIsNoJapanese()
        {
            // A JP player with no JA blurb gets the EN one. That is intended, not a gap —
            // it mirrors TitleJa's behaviour and is strictly better than an empty row.
            Install(Language.Japanese);
            Assert.AreEqual("Operator EN", Resolve(null, "Operator EN", null));
            Assert.AreEqual("Operator EN", Resolve(null, "Operator EN", "   "));
        }

        [Test]
        public void Rung4_EmptyWhenThereIsNothingToSay()
        {
            Install(Language.English);
            Assert.AreEqual(string.Empty, Resolve(null, null, null));
            Assert.AreEqual(string.Empty, Resolve("", "", ""));
            Assert.AreEqual(string.Empty, Resolve("   ", "   ", "   "));
        }

        // ── §2 The JP-only asymmetry — the case the rule exists for ───────────

        [Test]
        public void EnglishPlayerNeverSeesTheJapaneseBlurb_EvenWithNoEnglish()
        {
            // THE headline case. With `en` empty, the English ladder must SKIP rung 2 entirely
            // and land on rung 4, collapsing the row. A rung that merely *preferred* en would
            // pass a naive test and still leak Japanese copy to an EN player.
            Install(Language.English);

            foreach (string? emptyEn in new string?[] { null, "", "   " })
            {
                string rendered = Resolve(null, emptyEn, "運営JA");
                Assert.AreEqual(string.Empty, rendered,
                    $"With en='{emptyEn ?? "null"}' an EN player must get the empty string.");
                Assert.AreNotEqual("運営JA", rendered,
                    "The Japanese blurb must NEVER reach an English player.");
            }
        }

        // ── §3 No raw key, no id rung ─────────────────────────────────────────

        [Test]
        public void NonResolvingKeyFallsThroughAndNeverRendersRaw()
        {
            Install(Language.English, ("tourn.desc.other", "Other", "その他"));

            string rendered = Resolve("tourn.desc.missing", "Operator EN", "運営JA");
            Assert.AreEqual("Operator EN", rendered, "A key that echoes back must fall through.");
            Assert.AreNotEqual("tourn.desc.missing", rendered, "The raw key must never reach a player.");

            // And with nothing beneath it, the fall-through is empty — not the key, not the slug.
            Assert.AreEqual(string.Empty, Resolve("tourn.desc.missing", null, null));
        }

        [Test]
        public void NullKeyDoesNotThrow()
        {
            // LocalizationManager.Get(null) would hit Dictionary.TryGetValue(null) → ArgumentNullException.
            Install(Language.English, ("tourn.desc.x", "X", "X"));
            Assert.DoesNotThrow(() => Resolve(null, "EN", "JA"));
        }

        [Test]
        public void NullDefinitionIsEmpty()
        {
            Install(Language.English);
            Assert.AreEqual(string.Empty, Resolve((TournamentDefinition?)null));
        }

        [Test]
        public void ValuesAreTrimmed()
        {
            Install(Language.English);
            Assert.AreEqual("Operator EN", Resolve(null, "  Operator EN  ", null));

            Install(Language.Japanese);
            Assert.AreEqual("運営JA", Resolve(null, null, "  運営JA  "));
        }

        // ── The definition overload — proves the ladder is fed off a real def ─

        [Test]
        public void DefinitionOverloadWalksTheSameLadder()
        {
            Install(Language.Japanese);
            Assert.AreEqual("運営JA", Resolve(Def(null, "Operator EN", "運営JA")));

            Install(Language.English);
            Assert.AreEqual("Operator EN", Resolve(Def(null, "Operator EN", "運営JA")));

            Install(Language.English, ("tourn.desc.lomond", "Shipped EN blurb", "出荷済みJA紹介文"));
            Assert.AreEqual("Shipped EN blurb", Resolve(Def("tourn.desc.lomond", "Operator EN", "運営JA")));
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // §4 Mapper pass-through — the three columns survive the wire → definition hop
    // ═════════════════════════════════════════════════════════════════════════
    public class DescriptionMapperTests
    {
        [Test]
        public void DescriptionColumnsReachTheDefinition()
        {
            string json = Fixtures.Envelope(
                DescribedTournament("Compete at Lomond.", "ロモンドで競え。", "tourn.desc.lomond"));

            var mapped = Prod.MapJson(json, Fixtures.BotFields("field_major"));
            Assert.IsNotNull(mapped, "The payload should map cleanly.");

            var def = mapped!.Value.Defs[0];
            Assert.AreEqual("Compete at Lomond.", def.DescriptionEn);
            Assert.AreEqual("ロモンドで競え。",     def.DescriptionJa);
            Assert.AreEqual("tourn.desc.lomond",  def.DescriptionKey);
        }

        [Test]
        public void AbsentOrBlankDescriptionColumnsBecomeNullAndDropNoRow()
        {
            // An un-migrated server sends no description keys at all; a migrated one can send "".
            // Neither is an error — the modal simply hides its info row.
            var mapped = Prod.MapJson(Fixtures.Envelope(Fixtures.Tournament()),
                                      Fixtures.BotFields("field_major"));
            Assert.IsNotNull(mapped);
            Assert.AreEqual(1, mapped!.Value.Defs.Count, "A row with no blurb must not be dropped.");
            Assert.IsNull(mapped.Value.Defs[0].DescriptionEn);
            Assert.IsNull(mapped.Value.Defs[0].DescriptionJa);
            Assert.IsNull(mapped.Value.Defs[0].DescriptionKey);

            var blank = Prod.MapJson(Fixtures.Envelope(DescribedTournament("   ", "", "")),
                                     Fixtures.BotFields("field_major"));
            Assert.IsNotNull(blank);
            Assert.AreEqual(1, blank!.Value.Defs.Count);
            Assert.IsNull(blank.Value.Defs[0].DescriptionEn, "Whitespace-only collapses to null.");
            Assert.IsNull(blank.Value.Defs[0].DescriptionJa);
            Assert.IsNull(blank.Value.Defs[0].DescriptionKey);
        }

        /// <summary>Fixtures.Tournament plus the three description columns.</summary>
        private static string DescribedTournament(string? en, string? ja, string? key)
        {
            string J(string? s) => s == null ? "null" : "\"" + s + "\"";
            string one = Fixtures.Tournament();
            return one.Substring(0, one.Length - 1)
                 + $",\"description_en\":{J(en)},\"description_ja\":{J(ja)},\"description_key\":{J(key)}"
                 + "}";
        }
    }
}
