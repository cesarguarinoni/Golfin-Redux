// Assets/Tests/EditMode/GachaStage2Tests.cs
// gacha_screen Stage 2 — §6 EditMode tests
// Catalog parsing, GetLiveBanners filter, countdown formatter.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, overrideReferences:false)
// GachaBannerEntry, GachaBannerCatalog and GachaCarouselController live in Assembly-CSharp.
// Cannot reference Assembly-CSharp types at compile time — all production calls use System.Reflection
// (same pattern as StaminaShopAddEnergyTests.cs / Golfin.UI.Shop.Tests).
//
// stage2 iter-3 fix (feedback_tests_must_target_production_type):
//   The 3 CsvParse_* tests now call GachaBannerCatalog.ParseCsv(string) via reflection.
//   The 4 GetLiveBanners_* tests now call GachaBannerCatalog.GetLiveBanners(entries,nowUtc) via reflection.
//   Both are REAL production methods (internal seams added to GachaBannerModel.cs in this iteration).
//   The local EntryRow / ParseCsvDirect / FilterLive mirrors have been DELETED — no copy remains.
//   The 8 FormatCountdown_* tests call GachaCarouselController.FormatCountdown via reflection (unchanged).
//
// gacha_admin_catalogs §3: three tests added (CsvParse_QuotedFieldWithComma_DoesNotShiftColumns,
//   CsvParse_WideHeader_ReadsTheNineShippedFieldsUnchanged, CsvParse_RowWithNoBannerId_Skipped).
//   The 15 pre-existing tests are UNMODIFIED — the header-indexed parser must read the 9-column
//   form byte-for-byte the way the positional one did.

using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class GachaStage2Tests
    {
        // ─────────────────────────────────────────────────────────────────────────
        // Reflection: production types in Assembly-CSharp (cannot reference directly)
        // ─────────────────────────────────────────────────────────────────────────

        // GachaBannerCatalog — static class with ParseCsv and GetLiveBanners seams
        private static readonly Type _catalogType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerCatalog, Assembly-CSharp");

        // GachaBannerEntry — data class returned by ParseCsv / GetLiveBanners
        private static readonly Type _entryType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaBannerEntry, Assembly-CSharp");

        // GachaCarouselController — for FormatCountdown (unchanged from prior iters)
        private static readonly Type _carouselType =
            Type.GetType("GolfinRedux.UI.Gacha.GachaCarouselController, Assembly-CSharp");

        // GachaBannerCatalog.ParseCsv(string csvText) — internal seam
        private static readonly MethodInfo _parseCsvMethod =
            _catalogType?.GetMethod("ParseCsv",
                BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string) }, null);

        // GachaBannerCatalog.GetLiveBanners(IEnumerable<GachaBannerEntry>, DateTime) — internal seam
        // Distinguished from the public no-arg overload by parameter count.
        private static readonly MethodInfo _getLiveOverloadMethod = FindLiveOverload();

        // GachaCarouselController.FormatCountdown(TimeSpan) — public static (unchanged)
        private static readonly MethodInfo _formatMethod =
            _carouselType?.GetMethod("FormatCountdown",
                BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(TimeSpan) }, null);

        // MethodInfo finder for the 2-parameter GetLiveBanners overload
        private static MethodInfo FindLiveOverload()
        {
            if (_catalogType == null) return null;
            var methods = _catalogType.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var m in methods)
                if (m.Name == "GetLiveBanners" && m.GetParameters().Length == 2)
                    return m;
            return null;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Reflection helpers
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Calls PRODUCTION GachaBannerCatalog.ParseCsv(csvText) via reflection.
        /// Returns a List&lt;GachaBannerEntry&gt; as IList for iteration.
        /// </summary>
        private static IList ParseCsvViaProd(string csv)
        {
            Assert.IsNotNull(_catalogType,    "GachaBannerCatalog not found in Assembly-CSharp");
            Assert.IsNotNull(_parseCsvMethod, "GachaBannerCatalog.ParseCsv(string) not found — seam missing?");
            return (IList)_parseCsvMethod.Invoke(null, new object[] { csv });
        }

        /// <summary>
        /// Calls PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, DateTime.UtcNow) via reflection.
        /// entries must be a List&lt;GachaBannerEntry&gt; created by MakeEntryList().
        /// </summary>
        private static IList GetLiveViaProd(IList entries)
        {
            Assert.IsNotNull(_catalogType,          "GachaBannerCatalog not found in Assembly-CSharp");
            Assert.IsNotNull(_getLiveOverloadMethod, "GachaBannerCatalog.GetLiveBanners(entries,nowUtc) not found — seam missing?");
            return (IList)_getLiveOverloadMethod.Invoke(null, new object[] { entries, DateTime.UtcNow });
        }

        /// <summary>
        /// Creates a GachaBannerEntry (production type) with the specified key fields.
        /// Other fields use production defaults (empty strings, CostX1/X10=0).
        /// </summary>
        private static object CreateEntry(string bannerId, bool active, DateTime endUtc, int sortOrder)
        {
            Assert.IsNotNull(_entryType, "GachaBannerEntry not found in Assembly-CSharp");
            var entry = Activator.CreateInstance(_entryType);
            _entryType.GetProperty("BannerId" ).SetValue(entry, bannerId);
            _entryType.GetProperty("Active"   ).SetValue(entry, active);
            _entryType.GetProperty("EndUtc"   ).SetValue(entry, endUtc);
            _entryType.GetProperty("SortOrder").SetValue(entry, sortOrder);
            return entry;
        }

        /// <summary>
        /// Creates a List&lt;GachaBannerEntry&gt; (production generic type) at runtime so its
        /// runtime type satisfies IEnumerable&lt;GachaBannerEntry&gt; when passed to GetLiveViaProd.
        /// </summary>
        private static IList MakeEntryList(params object[] entries)
        {
            Assert.IsNotNull(_entryType, "GachaBannerEntry not found in Assembly-CSharp");
            var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(_entryType);
            var list     = (IList)Activator.CreateInstance(listType);
            foreach (var e in entries) list.Add(e);
            return list;
        }

        /// <summary>Reads a named property value from a GachaBannerEntry returned by production methods.</summary>
        private static T GetProp<T>(object entry, string propName)
        {
            Assert.IsNotNull(_entryType, "GachaBannerEntry not found in Assembly-CSharp");
            return (T)_entryType.GetProperty(propName).GetValue(entry);
        }

        /// <summary>Calls PRODUCTION GachaCarouselController.FormatCountdown(ts) via reflection.</summary>
        private static string FormatCountdown(TimeSpan ts)
        {
            Assert.IsNotNull(_carouselType, "GachaCarouselController not found in Assembly-CSharp");
            Assert.IsNotNull(_formatMethod, "FormatCountdown(TimeSpan) not found on GachaCarouselController");
            return (string)_formatMethod.Invoke(null, new object[] { ts });
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 1 — CSV parsing
        // These 3 tests call PRODUCTION GachaBannerCatalog.ParseCsv(string).
        // A regression in the shipped parser (wrong column index, missing header-skip,
        // bad date fallback) will cause these tests to FAIL.
        // ─────────────────────────────────────────────────────────────────────────

        private const string SampleCsv =
            "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active\n" +
            "banner_a,KEY_A,Sprite_A,500,4500,2027-01-01T00:00:00Z,https://rules/a,1,true\n" +
            "banner_b,KEY_B,Sprite_B,750,6750,2026-06-01T00:00:00Z,https://rules/b,2,false\n" +
            "banner_short,K,S,100,900,2099-12-31T23:59:59Z,https://rules/c,3,true\n";

        [Test]
        public void CsvParse_LockedColumns_AllFieldsCorrect()
        {
            // Calls PRODUCTION GachaBannerCatalog.ParseCsv — circular gate is closed.
            var entries = ParseCsvViaProd(SampleCsv);

            Assert.AreEqual(3, entries.Count, "Expected 3 rows");

            var a = entries[0];
            Assert.AreEqual("banner_a",        GetProp<string>  (a, "BannerId"));
            Assert.AreEqual("KEY_A",           GetProp<string>  (a, "NameKey"));
            Assert.AreEqual("Sprite_A",        GetProp<string>  (a, "ArtSprite"));
            Assert.AreEqual(500,               GetProp<int>     (a, "CostX1"));
            Assert.AreEqual(4500,              GetProp<int>     (a, "CostX10"));
            Assert.AreEqual("https://rules/a", GetProp<string>  (a, "RulesUrl"));
            Assert.AreEqual(1,                 GetProp<int>     (a, "SortOrder"));
            Assert.IsTrue(                     GetProp<bool>    (a, "Active"));
            Assert.AreEqual(DateTimeKind.Utc,  GetProp<DateTime>(a, "EndUtc").Kind);

            var b = entries[1];
            Assert.IsFalse(GetProp<bool>(b, "Active"));
        }

        [Test]
        public void CsvParse_MalformedRow_Skipped()
        {
            const string csv =
                "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active\n" +
                "tooshort,only,3cols\n" +
                "banner_ok,KEY,Sprite,100,900,2027-01-01T00:00:00Z,url,1,true\n";

            // Calls PRODUCTION GachaBannerCatalog.ParseCsv — circular gate is closed.
            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(1, entries.Count, "Malformed row must be skipped");
        }

        [Test]
        public void CsvParse_BadEndUtcDate_DefaultsToMaxValue()
        {
            const string csv =
                "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active\n" +
                "banner_bad,KEY,Sprite,100,900,NOT_A_DATE,url,1,true\n";

            // Calls PRODUCTION GachaBannerCatalog.ParseCsv — circular gate is closed.
            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(DateTime.MaxValue, GetProp<DateTime>(entries[0], "EndUtc"),
                "Bad date must default to DateTime.MaxValue");
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 1b — the header-indexed, quote-aware parser (gacha_admin_catalogs §3)
        //
        // The three tests above pin the 9-column form the game shipped with; these three pin
        // what changed. `export_content.py` writes QUOTE_MINIMAL canonical form and the
        // bundled floor of the next build is whatever it wrote, so the parser has to survive
        // (a) a quoted field containing a comma and (b) the 22-column header the admin
        // catalog added — reading the same nine values out of both.
        // ─────────────────────────────────────────────────────────────────────────

        /// The 22-column header the `gacha_banners` catalog publishes (SPEC §2.1).
        private const string WideHeader =
            "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active," +
            "startUtc,poolId,ticketType,pityThreshold,pityMinRarity,guaranteeMinRarityX10," +
            "maxPullsPerPlayer,artUrl,nameEn,nameJa,taglineEn,taglineJa,featuredRefIds\n";

        [Test]
        public void CsvParse_QuotedFieldWithComma_DoesNotShiftColumns()
        {
            // A tagline with a comma in it is ONE quoted field to the exporter and TWO columns
            // to Split(','). Under the old parser `active` would have been read out of the
            // column after it; the entry below must come out identical to the unquoted case.
            const string csv = WideHeader +
                "banner_q,KEY,Sprite,50,450,2027-01-01T00:00:00Z,https://rules/q,7,true," +
                "2026-01-01T00:00:00Z,pool_standard_club1,0,50,Legendary,Rare,,," +
                "QUOTED,クオート,\"Pull now, win big\",\"引いて、勝つ\",club_pwedge_royal\n";

            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(1, entries.Count, "The quoted comma must not produce a ragged row");

            var e = entries[0];
            Assert.AreEqual("banner_q",        GetProp<string>(e, "BannerId"));
            Assert.AreEqual("KEY",             GetProp<string>(e, "NameKey"));
            Assert.AreEqual("Sprite",          GetProp<string>(e, "ArtSprite"));
            Assert.AreEqual(50,                GetProp<int>   (e, "CostX1"));
            Assert.AreEqual(450,               GetProp<int>   (e, "CostX10"));
            Assert.AreEqual("https://rules/q", GetProp<string>(e, "RulesUrl"));
            Assert.AreEqual(7,                 GetProp<int>   (e, "SortOrder"));
            Assert.IsTrue(GetProp<bool>(e, "Active"),
                "active read out of the wrong column is exactly what quote-awareness prevents");
        }

        [Test]
        public void CsvParse_WideHeader_ReadsTheNineShippedFieldsUnchanged()
        {
            // The four seed rows of the extended gacha_banners.csv, verbatim.
            const string csv = WideHeader +
                "banner_standard_club1,STANDARD CLUB 1,GachaBanner_StandardClub1,50,450,2027-01-01T00:00:00Z," +
                "https://golfin.example.com/gacha-rules/standard-club1,1,true,2026-01-01T00:00:00Z," +
                "pool_standard_club1,0,50,Legendary,Rare,,,STANDARD CLUB 1,X,,,club_pwedge_royal;club_putter_golfinx\n" +
                "banner_test_a,TEST BANNER A,GachaBanner_StandardClub1,50,450,2026-12-31T23:59:00Z," +
                "https://golfin.example.com/gacha-rules/test-a,2,true,2026-01-01T00:00:00Z," +
                "pool_standard_club1,0,,,,,,TEST BANNER A,X,,,\n" +
                "banner_test_b,TEST BANNER B,GachaBanner_StandardClub1,75,675,2026-11-30T00:00:00Z," +
                "https://golfin.example.com/gacha-rules/test-b,3,true,2026-01-01T00:00:00Z," +
                "pool_standard_club1,0,30,Rare,Uncommon,,,TEST BANNER B,X,,,club_awedge_fyloe\n" +
                "banner_inactive,INACTIVE BANNER,GachaBanner_StandardClub1,50,450,2027-06-01T00:00:00Z," +
                "https://golfin.example.com/gacha-rules/inactive,10,false,2026-01-01T00:00:00Z," +
                "pool_standard_club1,0,,,,,,INACTIVE BANNER,X,,,\n";

            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(4, entries.Count, "All four seed rows must parse under the 22-column header");

            var expectedIds   = new[] { "banner_standard_club1", "banner_test_a", "banner_test_b", "banner_inactive" };
            var expectedX1    = new[] { 50, 50, 75, 50 };
            var expectedX10   = new[] { 450, 450, 675, 450 };
            var expectedEnd   = new[] { "2027-01-01T00:00:00Z", "2026-12-31T23:59:00Z",
                                        "2026-11-30T00:00:00Z", "2027-06-01T00:00:00Z" };
            var expectedLive  = new[] { true, true, true, false };

            for (int i = 0; i < 4; i++)
            {
                Assert.AreEqual(expectedIds[i],  GetProp<string>(entries[i], "BannerId"));
                Assert.AreEqual(expectedX1[i],   GetProp<int>   (entries[i], "CostX1"));
                Assert.AreEqual(expectedX10[i],  GetProp<int>   (entries[i], "CostX10"));
                Assert.AreEqual(expectedLive[i], GetProp<bool>  (entries[i], "Active"));
                Assert.AreEqual(
                    DateTime.Parse(expectedEnd[i], System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime(),
                    GetProp<DateTime>(entries[i], "EndUtc"));
            }
        }

        [Test]
        public void CsvParse_RowWithNoBannerId_Skipped()
        {
            const string csv =
                "bannerId,nameKey,artSprite,costX1,costX10,endUtc,rulesUrl,sortOrder,active\n" +
                ",KEY,Sprite,100,900,2027-01-01T00:00:00Z,url,1,true\n" +
                "banner_ok,KEY,Sprite,100,900,2027-01-01T00:00:00Z,url,2,true\n";

            var entries = ParseCsvViaProd(csv);
            Assert.AreEqual(1, entries.Count, "A row with a blank bannerId is not a banner");
            Assert.AreEqual("banner_ok", GetProp<string>(entries[0], "BannerId"));
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 2 — GetLiveBanners filter
        // These 4 tests call PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, nowUtc).
        // A regression in the shipped filter (>= vs > on expiry, missing sort, wrong active check)
        // will cause these tests to FAIL.
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void GetLiveBanners_ExcludesInactive()
        {
            var future  = DateTime.UtcNow.AddDays(10);
            var entries = MakeEntryList(
                CreateEntry("a", true,  future, 1),
                CreateEntry("b", false, future, 2));

            // Calls PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, nowUtc) — circular gate is closed.
            var live = GetLiveViaProd(entries);
            Assert.AreEqual(1, live.Count);
            Assert.AreEqual("a", GetProp<string>(live[0], "BannerId"));
        }

        [Test]
        public void GetLiveBanners_ExcludesPastEndUtc()
        {
            var past    = DateTime.UtcNow.AddSeconds(-1);
            var future  = DateTime.UtcNow.AddDays(10);
            var entries = MakeEntryList(
                CreateEntry("expired", true, past,   1),
                CreateEntry("live",    true, future, 2));

            // Calls PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, nowUtc) — circular gate is closed.
            var live = GetLiveViaProd(entries);
            Assert.AreEqual(1, live.Count);
            Assert.AreEqual("live", GetProp<string>(live[0], "BannerId"));
        }

        [Test]
        public void GetLiveBanners_SortsBySortOrder()
        {
            var future  = DateTime.UtcNow.AddDays(10);
            var entries = MakeEntryList(
                CreateEntry("c", true, future, 30),
                CreateEntry("a", true, future, 10),
                CreateEntry("b", true, future, 20));

            // Calls PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, nowUtc) — circular gate is closed.
            var live = GetLiveViaProd(entries);
            Assert.AreEqual(3, live.Count);
            Assert.AreEqual("a", GetProp<string>(live[0], "BannerId"));
            Assert.AreEqual("b", GetProp<string>(live[1], "BannerId"));
            Assert.AreEqual("c", GetProp<string>(live[2], "BannerId"));
        }

        [Test]
        public void GetLiveBanners_AllExpired_ReturnsEmpty()
        {
            var past    = DateTime.UtcNow.AddSeconds(-1);
            var entries = MakeEntryList(
                CreateEntry("x", true, past, 1),
                CreateEntry("y", true, past, 2));

            // Calls PRODUCTION GachaBannerCatalog.GetLiveBanners(entries, nowUtc) — circular gate is closed.
            var live = GetLiveViaProd(entries);
            Assert.AreEqual(0, live.Count);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Test 3 — Countdown formatter
        // These 8 tests already targeted production code (reflection on GachaCarouselController).
        // UNCHANGED from iter-2.
        // ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void FormatCountdown_MultiDay()
        {
            var ts = TimeSpan.FromSeconds(2 * 86400 + 3 * 3600 + 45 * 60 + 7);
            Assert.AreEqual("ENDS IN: 2d 3h 45m 07s", FormatCountdown(ts));
        }

        [Test]
        public void FormatCountdown_LessThanOneDay()
        {
            var ts = TimeSpan.FromSeconds(5 * 3600 + 12 * 60 + 33);
            Assert.AreEqual("ENDS IN: 5h 12m 33s", FormatCountdown(ts));
        }

        [Test]
        public void FormatCountdown_LessThanOneHour()
        {
            var ts = TimeSpan.FromSeconds(23 * 60 + 9);
            Assert.AreEqual("ENDS IN: 23m 09s", FormatCountdown(ts));
        }

        [Test]
        public void FormatCountdown_LessThanOneMinute()
        {
            var ts = TimeSpan.FromSeconds(47);
            Assert.AreEqual("ENDS IN: 47s", FormatCountdown(ts));
        }

        [Test]
        public void FormatCountdown_Zero_ReturnsZeroS()
        {
            Assert.AreEqual("ENDS IN: 0s", FormatCountdown(TimeSpan.Zero));
        }

        [Test]
        public void FormatCountdown_Negative_ReturnsZeroS()
        {
            Assert.AreEqual("ENDS IN: 0s", FormatCountdown(TimeSpan.FromSeconds(-10)));
        }

        [Test]
        public void FormatCountdown_ExactOneDay()
        {
            var ts = TimeSpan.FromSeconds(86400);
            Assert.AreEqual("ENDS IN: 1d 0h 0m 00s", FormatCountdown(ts));
        }

        [Test]
        public void FormatCountdown_59Seconds()
        {
            var ts = TimeSpan.FromSeconds(59);
            Assert.AreEqual("ENDS IN: 59s", FormatCountdown(ts));
        }
    }
}
