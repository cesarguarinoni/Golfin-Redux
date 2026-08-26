using NUnit.Framework;

namespace Golfin.Content.Tests
{
    /// <summary>
    /// The payload → overlay mapping. This is the one file that can turn a good response into
    /// WRONG strings, so every rule in SPEC §2/§6/§7 gets a case here, driven by hand-written JSON
    /// in the shape the live endpoint was verified to return on 2026-08-26.
    /// </summary>
    public class ContentTextsMapperTests
    {
        // The live response, trimmed to one row. Enveloped, as the disk cache stores it.
        private const string Enveloped =
            @"{""data"":{""fetched_at"":""2026-08-26T00:00:00+00:00"",""enabled"":true,
               ""latest_version"":11,
               ""catalogs"":{""texts"":{""version"":11,""full"":false,""changed"":[
                 {""id"":""BTN_START"",""is_active"":true,""min_build"":0,
                  ""data"":{""key"":""BTN_START"",""English"":""TEE OFF"",""Japanese"":""ティーオフ""}}
               ]}}}}";

        [Test]
        public void Map_EnvelopedBody_ReadsThroughTheDataWrapper()
        {
            var overlay = ContentTextsMapper.Map(Enveloped);

            Assert.IsTrue(overlay.Parsed);
            Assert.IsTrue(overlay.Enabled);
            Assert.AreEqual(11, overlay.Version);
            Assert.IsFalse(overlay.Full);
            Assert.AreEqual("TEE OFF", overlay.Rows["BTN_START"].english);
            Assert.AreEqual("ティーオフ", overlay.Rows["BTN_START"].japanese);
        }

        [Test]
        public void Map_UnwrappedBody_AlsoWorks_BecauseApiEnvelopeStripsTheWrapper()
        {
            // A live fetch arrives already unwrapped; the cache holds the raw enveloped body.
            // Both shapes reach this mapper, so both must map identically.
            const string unwrapped =
                @"{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""full"":true,""changed"":[
                   {""id"":""BTN_START"",""is_active"":true,""data"":{""English"":""TEE OFF""}}]}}}";

            var overlay = ContentTextsMapper.Map(unwrapped);

            Assert.IsTrue(overlay.Parsed);
            Assert.IsTrue(overlay.Full);
            Assert.AreEqual("TEE OFF", overlay.Rows["BTN_START"].english);
        }

        [Test]
        public void Map_InactiveRow_IsIgnored_SoTheBundledStringStays()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""BTN_START"",""is_active"":false,""data"":{""English"":""GONE""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsTrue(overlay.Parsed);
            Assert.IsFalse(overlay.Rows.ContainsKey("BTN_START"),
                "I6 — nothing is ever deleted, only deactivated. A deactivated text row means the " +
                "BUNDLED string stays, never a blank label.");
            Assert.AreEqual(1, overlay.SkippedInactive);
        }

        [Test]
        public void Map_EmptyEnglish_IsSkipped_BecauseBlankIsWorseThanBundled()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""BLANK_EN"",""is_active"":true,""data"":{""English"":"""",""Japanese"":""ある""}},
                   {""id"":""WS_EN"",""is_active"":true,""data"":{""English"":""   "",""Japanese"":""ある""}},
                   {""id"":""OK"",""is_active"":true,""data"":{""English"":""FINE""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsFalse(overlay.Rows.ContainsKey("BLANK_EN"));
            Assert.IsFalse(overlay.Rows.ContainsKey("WS_EN"),
                "Get()'s Japanese→English fallback reads `english`; a whitespace-only value would " +
                "render a Japanese player a blank label rather than the English they would have had.");
            Assert.AreEqual("FINE", overlay.Rows["OK"].english, "Good rows in the same payload still apply.");
            Assert.AreEqual(2, overlay.SkippedUnusable);
        }

        [Test]
        public void Map_MissingJapanese_KeepsTheRow_WithAnEmptyJapanese()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""EN_ONLY"",""is_active"":true,""data"":{""English"":""ONLY""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.AreEqual("ONLY", overlay.Rows["EN_ONLY"].english);
            Assert.AreEqual(string.Empty, overlay.Rows["EN_ONLY"].japanese,
                "Get() falls back to english when japanese is empty, so an EN-only row is a " +
                "perfectly good row — not a reason to drop it.");
        }

        [Test]
        public void Map_UnknownColumns_AreIgnored_PerI4()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""K"",""is_active"":true,
                    ""data"":{""English"":""E"",""Korean"":""K"",""notes"":""admin only""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsTrue(overlay.Parsed, "A new admin column must never break an installed build.");
            Assert.AreEqual("E", overlay.Rows["K"].english);
        }

        [Test]
        public void Map_LowerCaseColumnNames_AreAccepted()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""K"",""is_active"":true,""data"":{""english"":""E"",""japanese"":""J""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.AreEqual("E", overlay.Rows["K"].english);
            Assert.AreEqual("J", overlay.Rows["K"].japanese);
        }

        [Test]
        public void Map_MissingId_FallsBackToTheKeyColumn()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""is_active"":true,""data"":{""key"":""FROM_DATA"",""English"":""E""}}]}}}}";

            Assert.AreEqual("E", ContentTextsMapper.Map(json).Rows["FROM_DATA"].english);
        }

        [Test]
        public void Map_RowWithNoUsableKey_IsSkipped()
        {
            const string json =
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""is_active"":true,""data"":{""English"":""E""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);
            Assert.AreEqual(0, overlay.Rows.Count);
            Assert.AreEqual(1, overlay.SkippedUnusable);
        }

        [Test]
        public void Map_KillSwitch_ReportsDisabled_AndCarriesNoRows()
        {
            // The server omits a disabled catalog AND sets enabled:false. The client must read
            // that as "undo remote text", never as "the catalog is now empty".
            const string json = @"{""data"":{""enabled"":false,""catalogs"":{}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsTrue(overlay.Parsed, "The body was understood — that is how we know to drop the cache.");
            Assert.IsFalse(overlay.Enabled);
            Assert.AreEqual(0, overlay.Rows.Count);
        }

        [Test]
        public void Map_KillSwitch_ShortCircuitsBeforeReadingCatalogs()
        {
            // Belt: even if a future server sent rows alongside enabled:false, none may be applied.
            const string json =
                @"{""data"":{""enabled"":false,""catalogs"":{""texts"":{""version"":12,""changed"":[
                   {""id"":""K"",""is_active"":true,""data"":{""English"":""SHOULD NOT APPLY""}}]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsFalse(overlay.Enabled);
            Assert.AreEqual(0, overlay.Rows.Count);
        }

        [Test]
        public void Map_MissingEnabledFlag_DefaultsToTrue()
        {
            const string json =
                @"{""data"":{""catalogs"":{""texts"":{""version"":11,""changed"":[]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsTrue(overlay.Enabled,
                "An older server that predates the flag must not read as 'kill everything'.");
        }

        [Test]
        public void Map_EmptyDelta_IsTheCommonCase_AndParsesCleanly()
        {
            // Verbatim from the live endpoint on 2026-08-26 with since=texts:11.
            const string json =
                @"{""data"":{""fetched_at"":""2026-08-25T23:36:27.512950+00:00"",""enabled"":true,
                   ""latest_version"":11,""catalogs"":{""texts"":{""version"":11,""full"":false,""changed"":[]}}}}";

            var overlay = ContentTextsMapper.Map(json);

            Assert.IsTrue(overlay.Parsed);
            Assert.IsTrue(overlay.Enabled);
            Assert.AreEqual(11, overlay.Version);
            Assert.AreEqual(0, overlay.Rows.Count, "changed:[] is the steady state, not a failure.");
        }

        [Test]
        public void Map_CorruptOrTruncated_IsUnparsed_NotAnException()
        {
            foreach (string junk in new[]
            {
                null,
                "",
                "   ",
                "not json at all",
                @"{""data"":{""enabled"":true,""catalogs"":{""texts"":{""version"":11,""chan", // truncated
                "[1,2,3]",
                @"{""data"":null}",
            })
            {
                var overlay = ContentTextsMapper.Map(junk);
                Assert.IsFalse(overlay.Parsed, $"Should not have parsed: {junk ?? "<null>"}");
                Assert.AreEqual(0, overlay.Rows.Count);
            }
        }

        [Test]
        public void Map_BodyWithoutTheTextsCatalog_IsUnparsed_SoAGoodCacheIsNotOverwritten()
        {
            const string json = @"{""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""changed"":[]}}}}";

            Assert.IsFalse(ContentTextsMapper.Map(json).Parsed,
                "A response carrying no texts catalog says nothing about texts. Treating it as an " +
                "empty overlay would let it replace a good cache with a body containing no strings.");
        }
    }
}
