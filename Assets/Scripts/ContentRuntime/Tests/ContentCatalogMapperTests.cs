// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Content.Tests — ContentCatalogMapperTests
//
// Hand-written payloads, shaped exactly like the ones the live endpoint returns
// (verified with curl 2026-08-26 and quoted in the file header of
// ContentCatalogMapper). The absent-vs-present-and-empty distinction gets the
// most attention here because SPEC §7's whole kill-switch argument rests on it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Content;
using NUnit.Framework;

namespace Golfin.Content.Tests
{
    [TestFixture]
    public class ContentCatalogMapperTests
    {
        // ── Fixtures ──────────────────────────────────────────────────────────

        private const string ClubsFull = @"
        {""data"":{""fetched_at"":""2026-08-26T00:00:00Z"",""enabled"":true,""latest_version"":5,
          ""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[
            {""id"":""club_driver_gf"",""is_active"":true,""min_build"":0,
             ""data"":{""id"":""club_driver_gf"",""name"":""Driver G&F"",""basePower"":""85""}}
          ]}}}}";

        /// <summary>A catalog at CURSOR PARITY: present, and empty. This is "no update".</summary>
        private const string ClubsAtParity = @"
        {""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":false,""changed"":[]}}}}";

        /// <summary>A catalog the server did not serve at all. This is NOT "no update".</summary>
        private const string ClubsAbsent = @"
        {""data"":{""enabled"":true,""catalogs"":{""items"":{""version"":1,""full"":true,""changed"":[]}}}}";

        // ── The distinction SPEC §7 rests on ──────────────────────────────────

        [Test]
        public void ACatalogAtCursorParity_IsPRESENT_AndEmpty()
        {
            var payload = ContentCatalogMapper.Map(ClubsAtParity);

            Assert.IsTrue(payload.Parsed);
            Assert.IsNotNull(payload.Catalog(ContentCatalogs.Clubs),
                "a catalog with nothing new comes back PRESENT with an empty changed[] — " +
                "measured against prod 2026-08-26. It is not absent.");
            Assert.AreEqual(0, payload.Catalog(ContentCatalogs.Clubs)!.Rows.Count);
            Assert.IsEmpty(payload.AbsentFrom(new[] { ContentCatalogs.Clubs }));
        }

        [Test]
        public void ARequestedCatalogTheServerDidNotServe_IsABSENT()
        {
            var payload = ContentCatalogMapper.Map(ClubsAbsent);

            Assert.IsTrue(payload.Parsed);
            Assert.IsNull(payload.Catalog(ContentCatalogs.Clubs));
            CollectionAssert.AreEqual(new[] { ContentCatalogs.Clubs },
                payload.AbsentFrom(new[] { ContentCatalogs.Clubs, ContentCatalogs.Items }),
                "absent is what the client reads as WITHDRAWN — the §7 kill-switch signal");
        }

        // ── Kill switch ───────────────────────────────────────────────────────

        [Test]
        public void GlobalEnabledFalse_ShortCircuitsBeforeAnyCatalogIsRead()
        {
            // enabled:false must mean "ignore this response entirely", never "every catalog came
            // back empty" — the second reading would wipe every overlay as if an operator had
            // deleted the rows.
            const string killed = @"
            {""data"":{""enabled"":false,""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[
              {""id"":""club_x"",""data"":{""id"":""club_x""}}]}}}}";

            var payload = ContentCatalogMapper.Map(killed);

            Assert.IsTrue(payload.Parsed, "a kill payload is well-formed, not unparsed");
            Assert.IsFalse(payload.Enabled);
            Assert.IsEmpty(payload.Catalogs, "no catalog is read at all once the switch is off");
        }

        [Test]
        public void AnAbsentEnabledFlag_DefaultsToTrue()
        {
            // A server that predates the flag must not be read as disabled.
            var payload = ContentCatalogMapper.Map(
                @"{""data"":{""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[]}}}}");

            Assert.IsTrue(payload.Enabled);
        }

        // ── Shapes ────────────────────────────────────────────────────────────

        [Test]
        public void BothTheRawEnvelopeAndTheUnwrappedPayloadParse()
        {
            // A cached raw body still carries {"data": …}; a live fetch has already been unwrapped
            // by ApiEnvelope. Both reach this mapper.
            var wrapped   = ContentCatalogMapper.Map(ClubsFull);
            var unwrapped = ContentCatalogMapper.Map(
                @"{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[]}}}");

            Assert.IsTrue(wrapped.Parsed);
            Assert.IsTrue(unwrapped.Parsed);
            Assert.IsNotNull(wrapped.Catalog(ContentCatalogs.Clubs));
            Assert.IsNotNull(unwrapped.Catalog(ContentCatalogs.Clubs));
        }

        [Test]
        public void GarbageAndEmptyInput_ReturnUnparsedWithoutThrowing()
        {
            // A corrupt cache is a designed path, not a malfunction. Nothing here may throw.
            foreach (string bad in new[] { null!, "", "   ", "not json", "[1,2,3]", "\"a string\"" })
                Assert.IsFalse(ContentCatalogMapper.Map(bad).Parsed, $"'{bad}' must map to Unparsed");
        }

        [Test]
        public void VersionAndFullSurviveTheMapping()
        {
            var catalog = ContentCatalogMapper.Map(ClubsFull).Catalog(ContentCatalogs.Clubs)!;
            Assert.AreEqual(1, catalog.Version, "the catalog's own version is the only valid cursor");
            Assert.IsTrue(catalog.Full);
        }

        // ── Rows ──────────────────────────────────────────────────────────────

        [Test]
        public void RowsAreKeyedById_AndKeepPayloadOrder()
        {
            const string json = @"
            {""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":2,""full"":false,""changed"":[
              {""id"":""b"",""data"":{""id"":""b""}},
              {""id"":""a"",""data"":{""id"":""a""}}]}}}}";

            var catalog = ContentCatalogMapper.Map(json).Catalog(ContentCatalogs.Clubs)!;

            Assert.AreEqual("b", catalog.Rows[0].Id, "payload order is preserved for the APPEND case");
            Assert.AreEqual("a", catalog.Rows[1].Id);
            Assert.IsTrue(catalog.ById.ContainsKey("a"));
            Assert.IsTrue(catalog.ById.ContainsKey("b"));
        }

        [Test]
        public void IsActiveDefaultsToTrue_AndFalseIsCarriedNotDropped()
        {
            // I6: a deactivated row is an UPDATE, never a delete. It must survive mapping so the
            // database can mark the row inactive rather than never hearing about it.
            const string json = @"
            {""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":2,""full"":false,""changed"":[
              {""id"":""on"",""data"":{""id"":""on""}},
              {""id"":""off"",""is_active"":false,""data"":{""id"":""off""}}]}}}}";

            var catalog = ContentCatalogMapper.Map(json).Catalog(ContentCatalogs.Clubs)!;

            Assert.AreEqual(2, catalog.Rows.Count, "a deactivated row is NOT dropped by the mapper");
            Assert.IsTrue(catalog.ById["on"].IsActive, "an absent is_active means active");
            Assert.IsFalse(catalog.ById["off"].IsActive);
            Assert.AreEqual(1, catalog.ActiveCount);
        }

        [Test]
        public void ShopCatalogRowsKeyOnEntryId()
        {
            // shop_catalog's id column is entryId, not id. The row envelope's `id` is the reliable
            // source; the column fallbacks exist for a hand-seeded row.
            const string json = @"
            {""data"":{""enabled"":true,""catalogs"":{""shop_catalog"":{""version"":3,""full"":true,""changed"":[
              {""data"":{""entryId"":""shop_club_iron9_klyro"",""rpCost"":""200""}}]}}}}";

            var catalog = ContentCatalogMapper.Map(json).Catalog(ContentCatalogs.ShopCatalog)!;

            Assert.AreEqual(1, catalog.Rows.Count);
            Assert.AreEqual("shop_club_iron9_klyro", catalog.Rows[0].Id);
        }

        [Test]
        public void ARowWithNoUsableId_IsDroppedWithAWarning()
        {
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("no usable id"));

            const string json = @"
            {""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[
              {""data"":{""name"":""nameless""}}]}}}}";

            Assert.AreEqual(0, ContentCatalogMapper.Map(json).Catalog(ContentCatalogs.Clubs)!.Rows.Count);
        }

        [Test]
        public void UnknownColumnsAreCarried_NotRejected()
        {
            // I4: the client parses by column NAME and ignores what it does not know. A new admin
            // column must not need a client change to be safe, and it must survive to the cache so
            // a LATER build can read it.
            const string json = @"
            {""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":true,""changed"":[
              {""id"":""club_x"",""data"":{""id"":""club_x"",""someFutureColumn"":""42""}}]}}}}";

            var row = ContentCatalogMapper.Map(json).Catalog(ContentCatalogs.Clubs)!.ById["club_x"];

            Assert.IsTrue(row.TryGet("someFutureColumn", out string value));
            Assert.AreEqual("42", value);
        }

        // ── ContentRow.TryGet — the sparse-patch rule ─────────────────────────

        [Test]
        public void APresentButBlankColumn_CountsAsABSENT()
        {
            // The overlay is a sparse PATCH, not a replacement row. A published empty cell must not
            // blank a bundled value the operator never meant to touch.
            var row = new ContentRow("x", true, 0, new Dictionary<string, string?>
            {
                { "name",  "" },
                { "brand", "   " },
                { "type",  null },
                { "real",  "Driver" },
            });

            Assert.IsFalse(row.TryGet("name",  out _), "an empty string must not override the bundled value");
            Assert.IsFalse(row.TryGet("brand", out _), "whitespace-only must not either");
            Assert.IsFalse(row.TryGet("type",  out _), "nor null");
            Assert.IsTrue(row.TryGet("real",   out string v));
            Assert.AreEqual("Driver", v);
        }

        // ── ExtractSlices (what actually reaches the cache) ───────────────────

        [Test]
        public void ExtractSlices_ReturnsOnlyPresentCatalogs_Verbatim()
        {
            var slices = ContentCatalogMapper.ExtractSlices(
                ClubsFull, new[] { ContentCatalogs.Clubs, ContentCatalogs.Items });

            Assert.IsTrue(slices.ContainsKey(ContentCatalogs.Clubs));
            Assert.IsFalse(slices.ContainsKey(ContentCatalogs.Items),
                "an absent catalog yields no slice — which is exactly the caller's WITHDRAWN signal");

            StringAssert.Contains("someFutureColumn", ContentCatalogMapper.ExtractSlices(
                @"{""data"":{""enabled"":true,""catalogs"":{""clubs"":{""version"":1,""full"":true,
                   ""changed"":[{""id"":""x"",""data"":{""id"":""x"",""someFutureColumn"":""42""}}]}}}}",
                new[] { ContentCatalogs.Clubs })[ContentCatalogs.Clubs],
                "the slice is stored VERBATIM so a column this build ignores survives for a later one");
        }

        [Test]
        public void AnExtractedSliceWrappedInAnEnvelope_RoundTripsThroughMap()
        {
            // This is precisely what the cache file holds, so it has to survive the round trip or
            // every catalog reverts to bundled on the next launch.
            string slice = ContentCatalogMapper.ExtractSlices(
                ClubsFull, new[] { ContentCatalogs.Clubs })[ContentCatalogs.Clubs];

            string cached = RemoteContentSource.Envelope(ContentCatalogs.Clubs, slice);
            var reread = ContentCatalogMapper.Map(cached);

            Assert.IsTrue(reread.Parsed);
            Assert.IsTrue(reread.Enabled);

            var catalog = reread.Catalog(ContentCatalogs.Clubs);
            Assert.IsNotNull(catalog, "the envelope must be a payload the mapper reads unchanged");
            Assert.AreEqual(1, catalog!.Version);
            Assert.IsTrue(catalog.ById.ContainsKey("club_driver_gf"));
            Assert.IsTrue(catalog.ById["club_driver_gf"].TryGet("basePower", out string power));
            Assert.AreEqual("85", power);
        }

        [Test]
        public void APhase1WholeBodyTextsCache_StillParses()
        {
            // The upgrade path. Phase 1 cached the WHOLE response body in content_texts.json; that
            // file is still on every existing player's device, and it must keep working or the
            // upgrade silently costs them their text overlay.
            const string phase1Cache = @"
            {""data"":{""fetched_at"":""2026-08-26T00:00:00Z"",""enabled"":true,""latest_version"":11,
              ""catalogs"":{""texts"":{""version"":11,""full"":false,""changed"":[
                {""id"":""BTN_START"",""is_active"":true,""min_build"":0,
                 ""data"":{""key"":""BTN_START"",""English"":""PLAY"",""Japanese"":""プレイ""}}]}}}}";

            var texts = ContentTextsMapper.Map(phase1Cache);
            Assert.IsTrue(texts.Parsed, "a Phase-1 whole-body cache must still map");
            Assert.IsTrue(texts.Rows.ContainsKey("BTN_START"));
            Assert.AreEqual("PLAY", texts.Rows["BTN_START"].english);
        }

        // ── The wire format ───────────────────────────────────────────────────

        [Test]
        public void BuildSince_EmitsThePerCatalogCursorForm()
        {
            var cursors = new Dictionary<string, int>
            {
                { ContentCatalogs.Texts, 11 },
                { ContentCatalogs.Clubs, 1 },
                { ContentCatalogs.Balls, -3 },   // negative clamps to 0, mirroring parse_since
            };

            string since = RemoteContentSource.BuildSince(
                new[] { ContentCatalogs.Texts, ContentCatalogs.Clubs,
                        ContentCatalogs.Items, ContentCatalogs.Balls },
                cursors);

            Assert.AreEqual("texts:11,clubs:1,items:0,balls:0", since,
                "a catalog with no cursor is 0, which asks for it in full; a negative one clamps to 0");
        }

        [Test]
        public void CacheFileNamesArePerCatalog()
        {
            Assert.AreEqual("content_texts.json", RemoteContentSource.CacheFileName(ContentCatalogs.Texts),
                "the Phase-1 file name must not change, or every existing cache is orphaned");
            Assert.AreEqual("content_clubs.json", RemoteContentSource.CacheFileName(ContentCatalogs.Clubs));
            Assert.AreEqual("content_shop_catalog.json",
                RemoteContentSource.CacheFileName(ContentCatalogs.ShopCatalog),
                "an underscore in a catalog name must survive into the file name");
        }

        [Test]
        public void TheRequestListNamesEveryCatalogThisBuildKnows()
        {
            // A typo here is invisible: an unknown catalog name is ignored server-side (200, not
            // 400), so a misspelled catalog just never arrives and the game runs bundled forever.
            //
            // `level_up_costs` joined the list on 2026-08-28 (progress_server_side §2). It is the
            // one catalog the SERVER also prices from, so a client that stopped asking for it would
            // preview bundled costs and be answered `cost_changed` on every single level-up.
            //
            // `modes` joined the same day (game_modes_admin §2) and is the SECOND of those: the
            // server prices a mode entry from the mirror a modes publish writes, so a client that
            // stopped asking would show the bundled fee and be answered `fee_changed` on every tap.
            //
            // The four gacha catalogs joined on 2026-08-31 (gacha_client_real_pull §2). Three of
            // them — banners, rates, pools — are read by the SERVER's `golfin_gacha_pull()` too, so
            // a client that stopped asking would price and withhold banners off the bundled floor
            // and be answered `cost_changed` / `not_available` on taps it thought were fine.
            Assert.AreEqual("texts,clubs,characters,items,bags,balls,shop_catalog,level_up_costs,modes," +
                            "gacha_banners,gacha_rates,gacha_pools,ticket_types",
                            ContentCatalogs.RequestList);
        }
    }
}
