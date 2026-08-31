// Assets/Tests/EditMode/GachaRatesTextTests.cs
// gacha_ops_polish §2 — EditMode tests for the RATES & RULES body.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef). GachaRatesText, GachaBannerEntry, GachaRateEntry
// and GachaPoolEntry all live in Assembly-CSharp, which an asmdef cannot reference, so the
// production call goes through System.Reflection — the same pattern as GachaClientRealPullTests,
// and for the same reason: the seam under test must be the SHIPPING one, not a copy of it
// (feedback_tests_must_target_production_type).
//
// The name resolver is a DELEGATE for exactly this reason — a test in an asmdef could not
// implement an interface declared in Assembly-CSharp, so the seam takes Func<string,string,string>
// and this file hands it a lambda.
//
// LOCALIZATION IS INITIALISED, not stubbed: the assertions are on the FORMATTED English lines
// ("Guaranteed Rare or higher within 50 pulls"), which is the thing that can actually be wrong.
// A test that asserted on raw keys would pass while every {0} was in the wrong place.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaRatesTextTests
    {
        private const BindingFlags Any =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static readonly Type TextType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaRatesText, Assembly-CSharp");
        private static readonly Type EntryType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerEntry, Assembly-CSharp");
        private static readonly Type RateType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaRateEntry, Assembly-CSharp");
        private static readonly Type PoolType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPoolEntry, Assembly-CSharp");
        private static readonly Type RarityType =
            Type.GetType("Golfin.Roster.CharacterRarity, Assembly-CSharp");

        // ── Localization: the seven keys under test, in English ───────────────

        private static readonly (string key, string en)[] Rows =
        {
            ("GACHA_RATES_FEATURED",      "FEATURED"),
            ("GACHA_RATES_PITY",          "Guaranteed {0} or higher within {1} pulls"),
            ("GACHA_RATES_GUARANTEE_X10", "Every 10-pull includes at least one {0}"),
            ("GACHA_RATES_DUPE",          "Duplicate clubs and characters are converted to Reward Points"),
            ("GACHA_RATES_FOOTER",        "Rates apply to every pull on this banner."),
            ("RARITY_COMMON",             "COMMON"),
            ("RARITY_UNCOMMON",           "UNCOMMON"),
            ("RARITY_RARE",               "RARE"),
            ("RARITY_MYTHIC",             "MYTHIC"),
            ("RARITY_LEGENDARY",          "LEGENDARY"),
            ("RARITY_SUPREME",            "SUPREME"),
        };

        private object _savedTextMap;

        [SetUp]
        public void SetUp()
        {
            var mapField = typeof(LocalizationManager).GetField("_textMap", Any);
            _savedTextMap = mapField.GetValue(null);

            var table = ScriptableObject.CreateInstance<LocalizationTextTable>();
            foreach (var (key, en) in Rows)
                table.rows.Add(new LocalizedTextRow { key = key, english = en, japanese = en });

            LocalizationManager.Initialize(table, Language.English);
        }

        [TearDown]
        public void TearDown()
        {
            // Restore through Initialize + the saved map, not SetLanguage, so no OnLanguageChanged
            // fires at whatever UI happens to be alive in the editor.
            LocalizationManager.Initialize(ScriptableObject.CreateInstance<LocalizationTextTable>(),
                                           Language.English);
            typeof(LocalizationManager).GetField("_textMap", Any).SetValue(null, _savedTextMap);
        }

        // ── Fixture builders ──────────────────────────────────────────────────

        private static object Rarity(string name) => Enum.Parse(RarityType, name);

        private static void Set(object target, string member, object value)
        {
            var p = target.GetType().GetProperty(member, Any);
            if (p != null) { p.SetValue(target, value); return; }
            var f = target.GetType().GetField(member, Any);
            Assert.NotNull(f, $"{target.GetType().Name} has no member '{member}'");
            f.SetValue(target, value);
        }

        private static object Banner(string id, int pity = 0, string pityRarity = "Common",
                                     bool hasGuarantee = false, string guaranteeRarity = "Common",
                                     string[] featured = null)
        {
            object e = Activator.CreateInstance(EntryType);
            Set(e, "BannerId", id);
            Set(e, "PoolId", "pool_test");
            Set(e, "PityThreshold", pity);
            Set(e, "PityMinRarity", Rarity(pityRarity));
            Set(e, "HasGuaranteeX10", hasGuarantee);
            Set(e, "GuaranteeMinRarityX10", Rarity(guaranteeRarity));
            Set(e, "FeaturedRefIds", featured ?? Array.Empty<string>());
            return e;
        }

        private static object Rate(string rarity, int bp)
        {
            object r = Activator.CreateInstance(RateType);
            Set(r, "PoolId", "pool_test");
            Set(r, "Rarity", Rarity(rarity));
            Set(r, "RateBp", bp);
            return r;
        }

        private static object Prize(string refId, string rarity, int weight, int dupeRp = 0)
        {
            object p = Activator.CreateInstance(PoolType);
            Set(p, "PoolId", "pool_test");
            Set(p, "Kind", "club");
            Set(p, "RefId", refId);
            Set(p, "Rarity", Rarity(rarity));
            Set(p, "Weight", weight);
            Set(p, "DupeRp", dupeRp);
            return p;
        }

        /// <summary>The production seam, called reflectively; the resolver names every refId
        /// "NAME-&lt;refId&gt;" unless <paramref name="unknown"/> lists it.</summary>
        private static List<string> Build(object entry, IEnumerable<object> rates,
                                          IEnumerable<object> pool, params string[] unknown)
        {
            var method = TextType.GetMethod("Build", Any);
            Assert.NotNull(method, "GachaRatesText.Build not found");

            var rateList = ToTypedList(RateType, rates);
            var poolList = ToTypedList(PoolType, pool);

            var unknownSet = new HashSet<string>(unknown ?? Array.Empty<string>());
            Func<string, string, string> resolve = (kind, refId) =>
                unknownSet.Contains(refId) ? null : "NAME-" + refId;

            var result = method.Invoke(null, new object[] { entry, rateList, poolList, resolve });
            return ((System.Collections.IEnumerable)result).Cast<string>().ToList();
        }

        private static object ToTypedList(Type elementType, IEnumerable<object> items)
        {
            var listType = typeof(List<>).MakeGenericType(elementType);
            var list = (System.Collections.IList)Activator.CreateInstance(listType);
            foreach (var i in items) list.Add(i);
            return list;
        }

        /// <summary>The rendered line with the rich-text colour tags stripped, so an assertion is
        /// about the WORDS and not about a hex value that lives in RarityHelper.</summary>
        private static string Plain(string line)
            => System.Text.RegularExpressions.Regex.Replace(line ?? string.Empty, "<[^>]+>", string.Empty);

        private static List<string> Plain(IEnumerable<string> lines) => lines.Select(Plain).ToList();

        // ── §2.2/§2.3 — formatting and ordering ───────────────────────────────

        [Test]
        public void RarityTiers_AreListedRarestFirst_WithTwoDecimalPercentages()
        {
            var rates = new[] { Rate("Common", 9800), Rate("Legendary", 200) };
            var pool  = new[] { Prize("a", "Common", 100), Prize("b", "Legendary", 100) };

            var lines = Plain(Build(Banner("b1"), rates, pool));

            int legendary = lines.FindIndex(l => l.StartsWith("LEGENDARY"));
            int common    = lines.FindIndex(l => l.StartsWith("COMMON"));

            Assert.Greater(legendary, -1, "no LEGENDARY heading:\n" + string.Join("\n", lines));
            Assert.Greater(common, -1, "no COMMON heading:\n" + string.Join("\n", lines));
            Assert.Less(legendary, common, "rarest tier must come first:\n" + string.Join("\n", lines));

            Assert.AreEqual("LEGENDARY  2.00%", lines[legendary]);
            Assert.AreEqual("COMMON  98.00%", lines[common]);
        }

        [Test]
        public void PerItemOdds_AreRateTimesWeightShare_MatchingTheAdminEffectiveOdds()
        {
            // Legendary at 300bp split 3:1 → 2.25% and 0.75%; the admin's effectiveOdds computes
            // rate/10000 × weight/Σweight(tier) and must agree to the second decimal.
            var rates = new[] { Rate("Legendary", 300), Rate("Common", 9700) };
            var pool  = new[]
            {
                Prize("big",   "Legendary", 300),
                Prize("small", "Legendary", 100),
                Prize("junk",  "Common",    100),
            };

            var lines = Plain(Build(Banner("b1"), rates, pool));

            CollectionAssert.Contains(lines, "   NAME-big  2.25%");
            CollectionAssert.Contains(lines, "   NAME-small  0.75%");
            CollectionAssert.Contains(lines, "   NAME-junk  97.00%");
        }

        [Test]
        public void ATierRatedAtZero_IsNotListedAtAll()
        {
            var rates = new[] { Rate("Common", 10000), Rate("Supreme", 0) };
            var pool  = new[] { Prize("a", "Common", 100), Prize("s", "Supreme", 100) };

            var lines = Plain(Build(Banner("b1"), rates, pool));

            Assert.IsFalse(lines.Any(l => l.Contains("SUPREME")),
                           "a 0bp tier is never rolled and must not be listed:\n" + string.Join("\n", lines));
            Assert.IsFalse(lines.Any(l => l.Contains("NAME-s")));
        }

        [Test]
        public void TwoRateRowsForOneRarity_AreSummed_NotListedTwice()
        {
            var rates = new[] { Rate("Common", 4000), Rate("Common", 6000) };
            var pool  = new[] { Prize("a", "Common", 100) };

            var lines = Plain(Build(Banner("b1"), rates, pool));

            Assert.AreEqual(1, lines.Count(l => l.StartsWith("COMMON")),
                            "one heading per tier:\n" + string.Join("\n", lines));
            CollectionAssert.Contains(lines, "COMMON  100.00%");
        }

        // ── §2.1 — featured ───────────────────────────────────────────────────

        [Test]
        public void FeaturedIds_RenderNameAndRarity_InTheOrderTheOperatorWroteThem()
        {
            var rates = new[] { Rate("Common", 5000), Rate("Supreme", 5000) };
            var pool  = new[] { Prize("plain", "Common", 100), Prize("star", "Supreme", 100) };

            var lines = Plain(Build(Banner("b1", featured: new[] { "star", "plain" }), rates, pool));

            int head = lines.IndexOf("FEATURED");
            Assert.AreEqual(0, head, "FEATURED is the first section:\n" + string.Join("\n", lines));
            Assert.AreEqual("NAME-star  SUPREME", lines[1]);
            Assert.AreEqual("NAME-plain  COMMON", lines[2]);
        }

        [Test]
        public void AnUnresolvableFeaturedId_IsSkipped_AndTheRestStillRender()
        {
            var rates = new[] { Rate("Common", 10000) };
            var pool  = new[] { Prize("known", "Common", 100) };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("cannot resolve"));

            var lines = Plain(Build(Banner("b1", featured: new[] { "ghost", "known" }), rates, pool));

            CollectionAssert.Contains(lines, "FEATURED");
            CollectionAssert.Contains(lines, "NAME-known  COMMON");
            Assert.IsFalse(lines.Any(l => l.Contains("ghost")),
                           "an id no pool row carries must be skipped:\n" + string.Join("\n", lines));
        }

        [Test]
        public void AFeaturedIdInThePoolButUnnameable_IsAlsoSkipped()
        {
            var rates = new[] { Rate("Common", 10000) };
            var pool  = new[] { Prize("nameless", "Common", 100), Prize("known", "Common", 100) };

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
                new System.Text.RegularExpressions.Regex("cannot resolve"));

            var lines = Plain(Build(Banner("b1", featured: new[] { "nameless", "known" }),
                                    rates, pool, unknown: "nameless"));

            CollectionAssert.Contains(lines, "NAME-known  COMMON");
            Assert.IsFalse(lines.Any(l => l.Contains("nameless")));
        }

        [Test]
        public void NoFeaturedIds_MeansNoFeaturedHeading()
        {
            var lines = Plain(Build(Banner("b1"), new[] { Rate("Common", 10000) },
                                    new[] { Prize("a", "Common", 100) }));
            Assert.IsFalse(lines.Contains("FEATURED"));
        }

        // ── §2.4 — the three conditionals ─────────────────────────────────────

        [Test]
        public void PityLine_AppearsWithBothSubstitutions_OnlyWhenThresholdIsSet()
        {
            var rates = new[] { Rate("Common", 10000) };
            var pool  = new[] { Prize("a", "Common", 100) };

            var withPity = Plain(Build(Banner("b1", pity: 50, pityRarity: "Rare"), rates, pool));
            CollectionAssert.Contains(withPity, "Guaranteed RARE or higher within 50 pulls");

            var without = Plain(Build(Banner("b2"), rates, pool));
            Assert.IsFalse(without.Any(l => l.StartsWith("Guaranteed")),
                           "pityThreshold 0 means NO pity line:\n" + string.Join("\n", without));
        }

        [Test]
        public void GuaranteeLine_AppearsOnlyWhenTheBannerDeclaresOne()
        {
            var rates = new[] { Rate("Common", 10000) };
            var pool  = new[] { Prize("a", "Common", 100) };

            var with = Plain(Build(Banner("b1", hasGuarantee: true, guaranteeRarity: "Uncommon"),
                                   rates, pool));
            CollectionAssert.Contains(with, "Every 10-pull includes at least one UNCOMMON");

            // A BLANK guaranteeMinRarityX10 is "no guarantee", NOT "guarantee Common" — the
            // distinction GachaBannerEntry keeps as a separate bool, and the reason a banner with
            // the default rarity and no flag must print nothing.
            var without = Plain(Build(Banner("b2"), rates, pool));
            Assert.IsFalse(without.Any(l => l.StartsWith("Every 10-pull")),
                           string.Join("\n", without));
        }

        [Test]
        public void DupeLine_TracksThePool_NotTheBanner()
        {
            var rates = new[] { Rate("Common", 10000) };

            var withDupe = Plain(Build(Banner("b1"), rates, new[] { Prize("a", "Common", 100, dupeRp: 20) }));
            CollectionAssert.Contains(withDupe,
                "Duplicate clubs and characters are converted to Reward Points");

            var noDupe = Plain(Build(Banner("b1"), rates, new[] { Prize("a", "Common", 100) }));
            Assert.IsFalse(noDupe.Any(l => l.StartsWith("Duplicate")),
                           "a pool with no dupeRp has no duplicate rule to state:\n" + string.Join("\n", noDupe));
        }

        // ── §2.5 — the footer ─────────────────────────────────────────────────

        [Test]
        public void TheFooterIsAlwaysTheLastLine()
        {
            var lines = Plain(Build(Banner("b1", pity: 10, pityRarity: "Rare"),
                                    new[] { Rate("Common", 10000) },
                                    new[] { Prize("a", "Common", 100, dupeRp: 5) }));

            Assert.AreEqual("Rates apply to every pull on this banner.", lines[lines.Count - 1]);
        }

        [Test]
        public void ANullBanner_ProducesNothingRatherThanThrowing()
        {
            var method = TextType.GetMethod("Build", Any);
            var result = method.Invoke(null, new object[] { null, null, null, null });
            Assert.AreEqual(0, ((System.Collections.IEnumerable)result).Cast<string>().Count());
        }

        // ── The rarity tint is the roster's, not a second palette ─────────────

        [Test]
        public void RarityHeadings_CarryTheRosterRarityColour()
        {
            var lines = Build(Banner("b1"), new[] { Rate("Legendary", 10000) },
                              new[] { Prize("a", "Legendary", 100) });

            // RarityHelper is Assembly-CSharp too, so the EXPECTED colour is read from the same
            // shipping function the text uses — not hard-coded here, which would let the two
            // palettes drift apart while this test kept passing.
            var helper = Type.GetType("Golfin.Roster.RarityHelper, Assembly-CSharp");
            var color  = (Color)helper.GetMethod("GetRarityColor", Any)
                                      .Invoke(null, new[] { Rarity("Legendary") });
            string expected = "#" + ColorUtility.ToHtmlStringRGB(color);

            Assert.IsTrue(lines.Any(l => l.Contains("<color=" + expected + ">")),
                          "the heading must be tinted with RarityHelper's colour, got:\n" +
                          string.Join("\n", lines));
        }
    }
}
