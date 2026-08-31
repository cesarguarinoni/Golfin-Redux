// Assets/Tests/EditMode/GachaClientRealPullTests.cs
// gacha_client_real_pull §6 — EditMode tests for the client half of the server pull.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, overrideReferences:false).
// GachaBannerCatalog, GachaBannerEntry, PrizeRecord, GachaPullFlow and the three gacha catalogs
// all live in Assembly-CSharp, which an asmdef cannot reference, so every production call here
// goes through System.Reflection — the same pattern as GachaStage2Tests and the reason it exists
// (feedback_tests_must_target_production_type: the seam under test must be the SHIPPING one, not
// a copy of it living in the test file).
//
// Golfin.Content and Golfin.Economy ARE referenced directly by the asmdef, so ContentCatalog /
// ContentRow / GachaPullService / GachaPullResult are used as normal types.

using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Content;
using Golfin.Economy;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaClientRealPullTests
    {
        // ── Reflection handles ────────────────────────────────────────────────

        private static readonly Type CatalogType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCatalog, Assembly-CSharp");
        private static readonly Type EntryType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerEntry, Assembly-CSharp");
        private static readonly Type ResolverType =
            Type.GetType("GolfinRedux.UI.Gacha.IRefResolver, Assembly-CSharp");
        private static readonly Type PrizeRecordType =
            Type.GetType("GolfinRedux.UI.Gacha.PrizeRecord, Assembly-CSharp");
        private static readonly Type RatesType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaRatesCatalog, Assembly-CSharp");
        private static readonly Type PoolType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPoolCatalog, Assembly-CSharp");
        private static readonly Type TicketTypesType =
            Type.GetType("GolfinRedux.UI.Gacha.TicketTypeCatalog, Assembly-CSharp");
        private static readonly Type FlowType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaPullFlow, Assembly-CSharp");
        private static readonly Type RarityType =
            Type.GetType("Golfin.Roster.CharacterRarity, Assembly-CSharp");

        private const BindingFlags Any =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        private static MethodInfo Method(Type t, string name, int argCount)
        {
            foreach (var m in t.GetMethods(Any))
                if (m.Name == name && m.GetParameters().Length == argCount) return m;
            return null;
        }

        // ── Bundled CSV fixtures (the shipped headers, verbatim) ──────────────

        private const string BannerHeader =
            "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active,startUtc,poolId," +
            "ticketType,pityThreshold,pityMinRarity,guaranteeMinRarityX10,maxPullsPerPlayer,artUrl," +
            "nameEn,nameJa,taglineEn,taglineJa,featuredRefIds";

        private static string BannerRow(string id, string costX1 = "50", string active = "true",
                                        string pool = "pool_test", string extra = "")
            => $"{id},NAME KEY,Art_{id},{costX1},450,2099-01-01T00:00:00Z,https://x/y,1,{active}," +
               $"2020-01-01T00:00:00Z,{pool},0,50,Legendary,Rare,,,{id.ToUpperInvariant()},名前,,,{extra}";

        private static string BannerCsv(params string[] rows)
            => BannerHeader + "\n" + string.Join("\n", rows) + "\n";

        // ── Overlay helpers ───────────────────────────────────────────────────

        private static ContentRow Row(string id, bool active, Dictionary<string, string> data, int minBuild = 0)
        {
            var bag = new Dictionary<string, string>();
            foreach (var kv in data) bag[kv.Key] = kv.Value;
            return new ContentRow(id, active, minBuild, bag);
        }

        private static ContentCatalog Catalog(string name, params ContentRow[] rows)
            => new ContentCatalog(name, 1, false, new List<ContentRow>(rows));

        private static List<object> ParseBanners(string csv, ContentCatalog overlay)
        {
            var m = Method(CatalogType, "ParseCsv", 2);
            Assert.IsNotNull(m, "GachaBannerCatalog.ParseCsv(string, ContentCatalog) not found");
            var list = (System.Collections.IEnumerable)m.Invoke(null, new object[] { csv, overlay });
            var result = new List<object>();
            foreach (var e in list) result.Add(e);
            return result;
        }

        private static object Prop(object o, string name) => o.GetType().GetProperty(name).GetValue(o);

        private static object FindBanner(List<object> banners, string id)
        {
            foreach (var b in banners) if ((string)Prop(b, "BannerId") == id) return b;
            return null;
        }

        // ═════════════════════════════════════════════════════════════════════
        // §2 — the overlay
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Overlay_PatchesOneColumnAndLeavesTheRest()
        {
            var overlay = Catalog("gacha_banners",
                Row("banner_a", true, new Dictionary<string, string> { { "costX1", "77" } }));

            var banners = ParseBanners(BannerCsv(BannerRow("banner_a")), overlay);
            var a = FindBanner(banners, "banner_a");

            Assert.IsNotNull(a, "the bundled row must survive the merge");
            Assert.AreEqual(77, (int)Prop(a, "CostX1"), "the overlay's costX1 must win");
            Assert.AreEqual(450, (int)Prop(a, "CostX10"),
                "a column the overlay does NOT name must keep its bundled value — the patch is sparse");
            Assert.AreEqual("Art_banner_a", (string)Prop(a, "ArtSprite"),
                "artSprite must not be blanked by omission");
        }

        [Test]
        public void Overlay_AppendsARowTheBundledCsvHasNeverCarried()
        {
            var overlay = Catalog("gacha_banners",
                Row("banner_new", true, new Dictionary<string, string>
                {
                    { "bannerId", "banner_new" }, { "costX1", "10" }, { "costX10", "90" },
                    { "active", "true" }, { "poolId", "pool_test" }, { "nameEn", "NEW" },
                }));

            var banners = ParseBanners(BannerCsv(BannerRow("banner_a")), overlay);

            Assert.AreEqual(2, banners.Count, "the appended banner must be admitted alongside the bundled one");
            var n = FindBanner(banners, "banner_new");
            Assert.IsNotNull(n, "a banner authored entirely in the admin must appear");
            Assert.AreEqual(10, (int)Prop(n, "CostX1"));
            Assert.AreEqual("NEW", (string)Prop(n, "NameEn"));
        }

        [Test]
        public void Overlay_IsActiveFalseDropsTheRow()
        {
            var overlay = Catalog("gacha_banners",
                Row("banner_a", false, new Dictionary<string, string> { { "costX1", "77" } }));

            var banners = ParseBanners(BannerCsv(BannerRow("banner_a"), BannerRow("banner_b")), overlay);

            Assert.IsNull(FindBanner(banners, "banner_a"),
                "a deactivated banner is DROPPED — unlike a club, nobody owns a banner");
            Assert.IsNotNull(FindBanner(banners, "banner_b"), "every other row is untouched");
        }

        [Test]
        public void Overlay_NullOverlayKeepsTheBundledTableExactly()
        {
            var banners = ParseBanners(BannerCsv(BannerRow("banner_a", costX1: "50")), null);
            var a = FindBanner(banners, "banner_a");

            Assert.IsNotNull(a, "with no store installed (EditMode, a lab scene) the bundled floor stands");
            Assert.AreEqual(50, (int)Prop(a, "CostX1"));
        }

        [Test]
        public void NewColumns_AreParsed()
        {
            var banners = ParseBanners(BannerCsv(BannerRow("banner_a")), null);
            var a = FindBanner(banners, "banner_a");

            Assert.AreEqual("pool_test", (string)Prop(a, "PoolId"));
            Assert.AreEqual(0, (int)Prop(a, "TicketType"));
            Assert.AreEqual(50, (int)Prop(a, "PityThreshold"));
            Assert.AreEqual("Legendary", Prop(a, "PityMinRarity").ToString());
            Assert.IsTrue((bool)Prop(a, "HasGuaranteeX10"));
            Assert.AreEqual("Rare", Prop(a, "GuaranteeMinRarityX10").ToString());
            Assert.IsNull(Prop(a, "MaxPullsPerPlayer"), "a blank cap is UNCAPPED, not a cap of 0");
            Assert.AreEqual("BANNER_A", (string)Prop(a, "NameEn"));
            Assert.AreEqual("名前", (string)Prop(a, "NameJa"));
            Assert.AreNotEqual(DateTime.MinValue, (DateTime)Prop(a, "StartUtc"),
                "startUtc must be parsed, not left unbounded");
        }

        [Test]
        public void BlankGuaranteeIsNoGuarantee_NotAGuaranteeOfCommon()
        {
            // The distinction the card's second line is bound to: a banner with no x10 floor shows
            // NO line, and a "Common floor" would be a line promising nothing.
            string row = "banner_x,K,Art,50,450,2099-01-01T00:00:00Z,,1,true,2020-01-01T00:00:00Z," +
                         "pool_test,0,,,,,,X,,,,";
            var banners = ParseBanners(BannerCsv(row), null);
            var x = FindBanner(banners, "banner_x");

            Assert.IsFalse((bool)Prop(x, "HasGuaranteeX10"),
                "a blank guaranteeMinRarityX10 means NO x10 guarantee");
            Assert.AreEqual(0, (int)Prop(x, "PityThreshold"),
                "a blank pityThreshold means NO pity (plan §9 — pity may be none)");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §3 — the title ladder
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void TitleLadder_PicksTheLanguageThenFallsBackToTheOther()
        {
            var pick = Method(Type.GetType("GolfinRedux.UI.Gacha.GachaCsvMerge, Assembly-CSharp"),
                              "PickLocalised", 2);
            Assert.IsNotNull(pick, "GachaCsvMerge.PickLocalised not found");

            // The EditMode default language is English (LocalizationManager.CurrentLanguage).
            Assert.AreEqual("EN", (string)pick.Invoke(null, new object[] { "EN", "JA" }),
                "English selected → the EN title");
            Assert.AreEqual("JA", (string)pick.Invoke(null, new object[] { "", "JA" }),
                "a blank preferred side falls back to the OTHER language, never to empty");
            Assert.AreEqual("", (string)pick.Invoke(null, new object[] { "", "" }),
                "both blank → empty, so the caller can fall through to nameKey");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §3.1 — the withhold rule
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>A fixture <see cref="GolfinRedux.UI.Gacha.IRefResolver"/> built at runtime, so
        /// the SHIPPING IsRollable is what runs.</summary>
        // PUBLIC, with an explicit public ctor: DispatchProxy generates the proxy in ANOTHER
        // assembly, so a private nested type's constructor is inaccessible to it
        // (MethodAccessException at Create time, not at compile time).
        public class FakeResolver : System.Reflection.DispatchProxy
        {
            public static HashSet<string> Resolvable = new HashSet<string>();
            public static HashSet<int> TicketTypes = new HashSet<int>();
            public static bool Art = true;

            public FakeResolver() { }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                switch (targetMethod.Name)
                {
                    case "Resolves":          return Resolvable.Contains((string)args[0] + ":" + (string)args[1]);
                    case "TicketTypeExists":  return TicketTypes.Contains((int)args[0]);
                    case "ArtResolves":       return Art;
                    default:                  return null;
                }
            }
        }

        private static object MakeResolver()
        {
            var m = typeof(DispatchProxy).GetMethod("Create").MakeGenericMethod(ResolverType, typeof(FakeResolver));
            return m.Invoke(null, null);
        }

        private static bool IsRollable(object entry, out string reason)
        {
            var m = Method(CatalogType, "IsRollable", 3);
            Assert.IsNotNull(m, "GachaBannerCatalog.IsRollable(entry, resolver, out reason) not found");
            var args = new object[] { entry, MakeResolver(), null };
            bool ok = (bool)m.Invoke(null, args);
            reason = (string)args[2];
            return ok;
        }

        /// <summary>Install rates + pool tables for `pool_test` by driving the catalogs' own pure
        /// Parse seams and stuffing the result into their private index.</summary>
        private static void InstallPool(string ratesCsv, string poolCsv)
        {
            SetPrivateStatic(RatesType, "_byPool", IndexByPool(RatesType, ratesCsv));
            SetPrivateStatic(PoolType,  "_byPool", IndexByPool(PoolType, poolCsv));
        }

        private static object IndexByPool(Type catalogType, string csv)
        {
            var parse = Method(catalogType, "Parse", 2);
            var rows = (System.Collections.IEnumerable)parse.Invoke(null, new object[] { csv, null });

            Type rowType = catalogType == RatesType
                ? Type.GetType("GolfinRedux.UI.Gacha.GachaRateEntry, Assembly-CSharp")
                : Type.GetType("GolfinRedux.UI.Gacha.GachaPoolEntry, Assembly-CSharp");

            Type listType = typeof(List<>).MakeGenericType(rowType);
            Type mapType  = typeof(Dictionary<,>).MakeGenericType(typeof(string), listType);
            var map = Activator.CreateInstance(mapType);

            var tryGet = mapType.GetMethod("TryGetValue");
            var add    = mapType.GetMethod("Add");
            foreach (var r in rows)
            {
                string poolId = (string)rowType.GetField("PoolId").GetValue(r);
                object[] a = { poolId, null };
                if (!(bool)tryGet.Invoke(map, a))
                {
                    a[1] = Activator.CreateInstance(listType);
                    add.Invoke(map, new[] { poolId, a[1] });
                }
                listType.GetMethod("Add").Invoke(a[1], new[] { r });
            }
            return map;
        }

        private static void SetPrivateStatic(Type t, string field, object value)
            => t.GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, value);

        private const string RatesCsv =
            "id,poolId,rarity,rateBp\n" +
            "r_common,pool_test,Common,9000\n" +
            "r_legendary,pool_test,Legendary,1000\n";

        private const string PoolCsv =
            "id,poolId,kind,refId,rarity,weight,quantity,dupeRp,featured\n" +
            "p_c,pool_test,club,club_common,Common,100,1,20,false\n" +
            "p_l,pool_test,club,club_legend,Legendary,100,1,300,true\n";

        [SetUp]
        public void ResetFixtures()
        {
            FakeResolver.Resolvable = new HashSet<string> { "club:club_common", "club:club_legend" };
            FakeResolver.TicketTypes = new HashSet<int> { 0 };
            FakeResolver.Art = true;
            InstallPool(RatesCsv, PoolCsv);
            SetPrivateStatic(CatalogType, "BuildForWithhold", 0);
        }

        [TearDown]
        public void ClearFixtures()
        {
            Method(RatesType, "Reload", 0).Invoke(null, null);
            Method(PoolType, "Reload", 0).Invoke(null, null);
            Method(TicketTypesType, "Reload", 0).Invoke(null, null);
            SetPrivateStatic(CatalogType, "BuildForWithhold", -1);
        }

        private static object SeedBanner() => FindBanner(ParseBanners(BannerCsv(BannerRow("banner_a")), null), "banner_a");

        [Test]
        public void Rollable_TheSeedBannerIsAdmitted()
        {
            Assert.IsTrue(IsRollable(SeedBanner(), out string reason),
                $"a fully-resolvable banner must be rollable; withheld for: {reason}");
            Assert.IsEmpty(reason);
        }

        [Test]
        public void Withheld_WhenTheRateTableDoesNotSumTo10000()
        {
            InstallPool("id,poolId,rarity,rateBp\nr_c,pool_test,Common,9000\n", PoolCsv);
            Assert.IsFalse(IsRollable(SeedBanner(), out string reason));
            StringAssert.Contains("9000", reason, "the reason must name the sum it actually found");
        }

        [Test]
        public void Withheld_WhenARatedTierHasNoResolvableEntry()
        {
            // Legendary is rated at 1000bp but its only entry does not resolve in this build.
            FakeResolver.Resolvable = new HashSet<string> { "club:club_common" };
            Assert.IsFalse(IsRollable(SeedBanner(), out string reason));
            StringAssert.Contains("Legendary", reason);
        }

        [Test]
        public void NotWithheld_WhenAnUnresolvableTierIsRatedAtZero()
        {
            // A tier at 0bp is never rolled, so it needs no payable entry — withholding on it would
            // hide a banner the server would happily complete.
            InstallPool("id,poolId,rarity,rateBp\nr_c,pool_test,Common,10000\n" +
                        "r_l,pool_test,Legendary,0\n", PoolCsv);
            FakeResolver.Resolvable = new HashSet<string> { "club:club_common" };

            Assert.IsTrue(IsRollable(SeedBanner(), out string reason),
                $"a 0bp tier must not withhold the banner; got: {reason}");
        }

        [Test]
        public void Withheld_WhenThePoolEntryIsAboveThisBuild()
        {
            // The server's step-8 build lock, evaluated locally: an entry whose min_build is above
            // this build cannot pay, so the tier it is the only member of is unpayable.
            var overlay = Catalog("gacha_pools",
                Row("p_l", true, new Dictionary<string, string> { { "weight", "100" } }, minBuild: 9999));
            var parse = Method(PoolType, "Parse", 2);
            var rows = (System.Collections.IEnumerable)parse.Invoke(null, new object[] { PoolCsv, overlay });

            Type rowType = Type.GetType("GolfinRedux.UI.Gacha.GachaPoolEntry, Assembly-CSharp");
            int locked = 0;
            foreach (var r in rows)
                if ((int)rowType.GetField("MinBuild").GetValue(r) == 9999) locked++;

            Assert.AreEqual(1, locked,
                "the overlay row's min_build must land on the pool entry — it is the field the " +
                "withhold rule compares against ContentBuildNumber.Current");
        }

        [Test]
        public void Withheld_WhenTheTicketTypeIsNotPublished()
        {
            FakeResolver.TicketTypes = new HashSet<int>();
            Assert.IsFalse(IsRollable(SeedBanner(), out string reason));
            StringAssert.Contains("ticket type", reason);
        }

        [Test]
        public void Withheld_WhenTheArtDoesNotResolve()
        {
            FakeResolver.Art = false;
            Assert.IsFalse(IsRollable(SeedBanner(), out string reason));
            StringAssert.Contains("art", reason);
        }

        [Test]
        public void WindowFilter_ExcludesABannerThatHasNotStartedYet()
        {
            var m = Method(CatalogType, "GetLiveBanners", 2);
            Assert.IsNotNull(m, "the 2-arg GetLiveBanners window seam must still exist");

            string scheduled =
                "banner_soon,K,Art,50,450,2099-01-01T00:00:00Z,,1,true,2098-01-01T00:00:00Z," +
                "pool_test,0,,,,,,SOON,,,,";
            var banners = ParseBanners(BannerCsv(scheduled), null);

            var listType = typeof(List<>).MakeGenericType(EntryType);
            var typed = Activator.CreateInstance(listType);
            foreach (var b in banners) listType.GetMethod("Add").Invoke(typed, new[] { b });

            var live = (System.Collections.ICollection)m.Invoke(null,
                new object[] { typed, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

            Assert.AreEqual(0, live.Count,
                "a SCHEDULED banner is withheld: the countdown label ticks endUtc, so showing it " +
                "before startUtc would display the wrong clock entirely");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §2 — TryReinstallFromCache
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void TryReinstallFromCache_RefusesANonGachaCatalog()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("only the gacha catalogs"));

            Assert.IsFalse(ContentService.TryReinstallFromCache(ContentCatalogs.Clubs),
                "I5 — every catalog with owned state takes effect at the NEXT launch, and clubs is " +
                "the reason the rule exists");
        }

        [Test]
        public void TryReinstallFromCache_InstallsAGachaCatalogFromAWrittenCache()
        {
            const string catalog = ContentCatalogs.GachaRates;
            ContentCatalogStore.ConfigureForTest();   // Declared + Ready, nothing installed

            // The envelope RemoteContentSource writes, and the slice shape RemoteContentRowDto
            // deserialises: rows live under "changed" with an "id"/"data" envelope. Built through
            // Envelope() rather than hand-spelled so a change to the wrapper cannot leave this
            // test asserting a shape nothing produces.
            string slice = "{\"version\":7,\"full\":true,\"changed\":[" +
                           "{\"id\":\"r_probe\",\"is_active\":true,\"min_build\":0," +
                           "\"data\":{\"id\":\"r_probe\",\"poolId\":\"pool_probe\"," +
                           "\"rarity\":\"Common\",\"rateBp\":\"10000\"}}]}";

            RemoteContentSource.WriteCache(catalog, RemoteContentSource.Envelope(catalog, slice));
            try
            {
                Assert.IsTrue(ContentService.TryReinstallFromCache(catalog),
                    "a gacha catalog with a good cache on disk must install mid-session (5b)");

                var installed = ContentCatalogStore.Catalog(catalog);
                Assert.IsNotNull(installed, "the store must now hold it");
                Assert.AreEqual(1, installed.Rows.Count);
                Assert.AreEqual("r_probe", installed.Rows[0].Id);
            }
            finally
            {
                RemoteContentSource.ClearCache(catalog);
                ContentCatalogStore.Clear();
            }
        }

        [Test]
        public void TryReinstallFromCache_WithNoCacheOnDiskIsFalseAndHarmless()
        {
            const string catalog = ContentCatalogs.GachaPools;
            RemoteContentSource.ClearCache(catalog);
            ContentCatalogStore.ConfigureForTest();
            try
            {
                Assert.IsFalse(ContentService.TryReinstallFromCache(catalog),
                    "no cache is the normal state of a fresh install — false, and the store keeps " +
                    "whatever it already held");
                Assert.IsNull(ContentCatalogStore.Catalog(catalog));
            }
            finally { ContentCatalogStore.Clear(); }
        }

        // ═════════════════════════════════════════════════════════════════════
        // §4.1 — the pull service
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void BuildPullJson_HasTheDeployedWireShape()
        {
            string json = GachaPullService.BuildPullJson("banner_standard_club1", 10, 450, 2519, "KEY");

            StringAssert.Contains("\"banner_id\":\"banner_standard_club1\"", json);
            StringAssert.Contains("\"count\":10", json);
            StringAssert.Contains("\"idempotency_key\":\"KEY\"", json);
            StringAssert.Contains("\"build\":2519", json);
            StringAssert.Contains("\"expected_cost\":450", json);
        }

        [Test]
        public void BuildPullJson_SendsNullNotZeroWhenTheGuardIsSkipped()
        {
            // 0 would mean "I expect this to be free" and would be refused with cost_changed on
            // every priced banner. Null means "do not guard".
            string json = GachaPullService.BuildPullJson("b", 1, 0, 0, "K");
            StringAssert.Contains("\"expected_cost\":null", json);
        }

        [Test]
        public void PullRoutine_WithTheFlagOffMakesNoRequestAndAnswersUnavailable()
        {
            // The gate is INSIDE the routine, so neither entry point can reach the network with the
            // flag off — and Unavailable, not Disabled, because there is no local roll to fall back
            // to: the mock pool is deleted.
            bool wasEnabled = PointsBackendFlag.Enabled;
            PointsBackendFlag.Enabled = false;
            try
            {
                GachaPullOutcome outcome = null;
                var service = new GachaPullService(null);   // a null client PROVES no transport was used
                var routine = service.PullRoutine("banner_a", 1, 50, 0, o => outcome = o);
                while (routine.MoveNext()) { }

                Assert.IsNotNull(outcome, "onDone must be invoked exactly once, even with the flag off");
                Assert.AreEqual(GachaPullVerdict.Unavailable, outcome.Verdict);
            }
            finally { PointsBackendFlag.Enabled = wasEnabled; }
        }

        [Test]
        public void PullRoutine_RefusesACountThatIsNotOneOrTen_WithoutARequest()
        {
            bool wasEnabled = PointsBackendFlag.Enabled;
            PointsBackendFlag.Enabled = true;
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("only 1 and 10 are pullable"));

                GachaPullOutcome outcome = null;
                var service = new GachaPullService(null);
                var routine = service.PullRoutine("banner_a", 3, 50, 0, o => outcome = o);
                while (routine.MoveNext()) { }

                Assert.AreEqual(GachaPullVerdict.Unknown, outcome.Verdict,
                    "the pull ledger's CHECK accepts 1 and 10 only; a client bug must not reach it");
            }
            finally { PointsBackendFlag.Enabled = wasEnabled; }
        }

        // ═════════════════════════════════════════════════════════════════════
        // §4.1 — the ORDER a successful pull is applied in
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void ApplyOk_RunsTicketsThenRpThenDrainThenHistory_AndFinishesAfterTheDrain()
        {
            var apply = Method(FlowType, "ApplyOk", 6);
            Assert.IsNotNull(apply, "GachaPullFlow.ApplyOk(result, setTickets, foldRp, drain, history, done) not found");

            var order = new List<string>();

            var result = new GachaPullResult
            {
                Status = "ok",
                TicketType = 0,
                TicketBalance = 450,
                Rp = new GachaRpDto { Earned = 20, ActivityPts = 5, GiftPts = 6, TotalPoints = 11 },
                Prizes = Array.Empty<GachaPrizeDto>(),
            };

            Action<int, int> setTickets = (t, b) => order.Add($"tickets:{t}:{b}");
            Action<GachaRpDto> foldRp = rp => order.Add($"rp:{rp.TotalPoints}");
            // The drain defers its callback, which is the ordering that matters: everything after it
            // must wait, or the Prizes screen reads a bag the grant has not reached yet.
            Action<Action> drain = null;
            Action deferred = null;
            drain = done => { order.Add("drain"); deferred = done; };
            Action<GachaPullResult> history = r => order.Add("history");
            Action doneCb = () => order.Add("done");

            apply.Invoke(null, new object[] { result, setTickets, foldRp, drain, history, doneCb });

            CollectionAssert.AreEqual(new[] { "tickets:0:450", "rp:11", "drain" }, order,
                "tickets → RP → drain, and NOTHING after the drain until it calls back");

            deferred();
            CollectionAssert.AreEqual(
                new[] { "tickets:0:450", "rp:11", "drain", "history", "done" }, order,
                "history and the reveal continuation both wait on the drain's callback");
        }

        [Test]
        public void ApplyOk_DoesNotFoldRpWhenThePayloadCarriesNoRpBlock()
        {
            // A pull with no duplicate has no `rp` block at all, and folding its zeros in would
            // wipe the displayed balance.
            var apply = Method(FlowType, "ApplyOk", 6);
            bool folded = false;

            var result = new GachaPullResult
            {
                Status = "ok", TicketType = 0, TicketBalance = 500, Rp = null,
                Prizes = Array.Empty<GachaPrizeDto>(),
            };

            apply.Invoke(null, new object[]
            {
                result,
                (Action<int, int>)((t, b) => { }),
                (Action<GachaRpDto>)(rp => folded = true),
                (Action<Action>)(d => d()),
                (Action<GachaPullResult>)(r => { }),
                (Action)(() => { }),
            });

            Assert.IsFalse(folded, "no rp block ⇒ no balance fold");
        }

        // ═════════════════════════════════════════════════════════════════════
        // §4.3 — PrizeRecord from the server DTO
        // ═════════════════════════════════════════════════════════════════════

        private static object FromDto(GachaPrizeDto dto)
            => Method(PrizeRecordType, "FromDto", 1).Invoke(null, new object[] { dto });

        private static object Field(object o, string name) => o.GetType().GetField(name).GetValue(o);

        [Test]
        public void PrizeRecord_CarriesTheServersRarityVerbatim()
        {
            var r = FromDto(new GachaPrizeDto
            {
                Kind = "club", RefId = "club_x", Quantity = 1, Rarity = "Legendary",
            });

            Assert.AreEqual("Legendary", Field(r, "Rarity").ToString(),
                "the rarity is the SERVER's word — never a lookup in this build's database");
            Assert.AreEqual("club", (string)Field(r, "Kind"));
        }

        [Test]
        public void PrizeRecord_UnknownRarityBecomesCommonWithAWarning()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("Unknown rarity"));

            var r = FromDto(new GachaPrizeDto { Kind = "ball", RefId = "b", Quantity = 3, Rarity = "Mythical" });

            Assert.AreEqual("Common", Field(r, "Rarity").ToString(),
                "Common is the safe direction: an unparseable tier must not light the Legendary fanfare");
            Assert.AreEqual(3, (int)Field(r, "Quantity"));
        }

        [Test]
        public void PrizeRecord_CarriesTheDuplicateAndItsRp()
        {
            var r = FromDto(new GachaPrizeDto
            {
                Kind = "club", RefId = "club_x", Quantity = 1, Rarity = "Rare",
                IsDupe = true, DupeRp = 80,
            });

            Assert.IsTrue((bool)Field(r, "IsDupe"));
            Assert.AreEqual(80, (int)Field(r, "DupeRp"), "the pill reads '+80 RP' off this");
        }

        [Test]
        public void PrizeRecord_KindIsLowerCased()
        {
            // The kind is compared against the KindClub/KindBall constants and against
            // InventoryGrants' spellings; a "Club" that missed would render the wrong card.
            var r = FromDto(new GachaPrizeDto { Kind = "Club", RefId = "c", Quantity = 1, Rarity = "Common" });
            Assert.AreEqual("club", (string)Field(r, "Kind"));
        }

        // ═════════════════════════════════════════════════════════════════════
        // §4.4 — the blob no longer carries tickets
        // ═════════════════════════════════════════════════════════════════════

        [Test]
        public void Projector_DoesNotPutTicketsInTheBlob()
        {
            var save = new Golfin.Save.SaveData();
            save.ticketBalances.Add(new Golfin.Save.PersistedTicketBalance { ticketTypeInt = 0, balance = 500 });

            var snap = Golfin.InventorySync.InventoryProjector.Project(save);

            Assert.AreEqual(0, snap.Tickets.Count,
                "tickets are server-owned like RP: projecting one would upload a number the client " +
                "does not own, and the additive max-merge would then resurrect a pre-spend balance");
        }

        [Test]
        public void Apply_IgnoresAnIncomingTicketBalance()
        {
            var save = new Golfin.Save.SaveData();
            save.ticketBalances.Add(new Golfin.Save.PersistedTicketBalance { ticketTypeInt = 0, balance = 450 });

            var snap = new Golfin.InventorySync.InventorySnapshot();
            snap.Tickets[0] = 500;   // a stale blob, from before the pull that spent 50

            Golfin.InventorySync.InventoryProjector.Apply(snap, save);

            Assert.AreEqual(450, save.ticketBalances[0].balance,
                "the max-merge must NOT raise a ticket balance back to a pre-spend number");
        }
    }
}
