// gps_gifts_votes — EditMode tests for the two new services.
//
// Pins the things that are EASY TO GET WRONG AND INVISIBLE ON SCREEN: the wire bodies (a
// misspelled snake_case field is a silent 422), the already-voted discrimination (a 400 that
// means "voted" vs a 400 that means "broken"), the buy-strip selection rule, and the supporter
// name extraction — which is the only place a counterparty is recorded at all.
//
// Every test drives the REAL service through the REAL ApiClient over a scripted transport; none
// of them re-implements a rule and then asserts its own copy.
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Golfin.Social.Tests
{
    public class GiftVoteServiceTests
    {
        private FakeHttpTransport _transport;
        private ApiClient _client;

        [SetUp]
        public void SetUp()
        {
            _transport = new FakeHttpTransport();
            _client = new ApiClient(_transport, new FakeAuthTokenProvider(), new ImmediateCoroutineRunner())
            {
                MaxTransientRetries = 0,
                RetryDelaySeconds = 0f,
                LogRequests = false,
            };
        }

        [TearDown]
        public void TearDown()
        {
            GiftService.ResetForTest();
            VoteService.ResetForTest();
            Endpoints.ResetToDefault();
        }

        // ════════════════════════════════════════════════════════════════════
        // Wire bodies — the fields are snake_case because they mirror the routers
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SendJson_CarriesReceiverAmountAndKey()
        {
            JObject o = JObject.Parse(GiftService.BuildSendJson("rcv-1", 250, "key-1", null));

            Assert.AreEqual("rcv-1", (string)o["receiver_id"]);
            Assert.AreEqual(250, (int)o["amount"]);
            Assert.AreEqual("key-1", (string)o["idempotency_key"]);
            // Omitted, not null: gifts.py declares `message: Optional[str] = None`, and an
            // explicit null would be indistinguishable from not sending it anyway.
            Assert.IsNull(o["message"], "an empty message must be OMITTED, not sent as null");
        }

        [Test]
        public void PurchaseJson_DefaultsCurrencyToActivity()
        {
            JObject o = JObject.Parse(GiftService.BuildPurchaseJson("item-9", null, "key-2"));

            Assert.AreEqual("item-9", (string)o["item_id"]);
            Assert.AreEqual("activity", (string)o["currency"]);
            Assert.AreEqual("key-2", (string)o["idempotency_key"]);
        }

        [Test]
        public void CastJson_IsJustTheOptionId()
        {
            JObject o = JObject.Parse(VoteService.BuildCastJson("opt-7"));
            Assert.AreEqual("opt-7", (string)o["option_id"]);
            Assert.AreEqual(1, o.Count, "voting.py::CastVoteRequest has exactly one field");
        }

        [Test]
        public void CreateJson_IsYesNoWithTwoOptions()
        {
            string json = VoteService.BuildCreateJson("Break 90?", new[] { "YES", "NO" },
                                                      "2026-09-09T00:00:00Z");
            JObject o = Parse(json);

            Assert.AreEqual("Break 90?", (string)o["question"]);
            Assert.AreEqual("yesNo", (string)o["vote_type"]);
            Assert.AreEqual(2, ((JArray)o["options"]).Count);
            // Asserted on the RAW json as well as the parsed token: JObject.Parse defaults to
            // DateParseHandling.DateTime, which turns an ISO-8601 string into a DateTime and hands
            // it back reformatted as "09/09/2026 00:00:00" — a property of the reader, not of what
            // went on the wire. `Parse` below turns that off; this line is the belt.
            StringAssert.Contains("\"expires_at\":\"2026-09-09T00:00:00Z\"", json);
            Assert.AreEqual("2026-09-09T00:00:00Z", (string)o["expires_at"]);
        }

        /// <summary>
        /// Parse WITHOUT date coercion. Newtonsoft's default <c>DateParseHandling.DateTime</c>
        /// rewrites any ISO-8601-looking string into a DateTime token, so a test that reads
        /// `expires_at` back through a plain <c>JObject.Parse</c> is measuring the reader rather
        /// than the request body.
        /// </summary>
        private static JObject Parse(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json))
                   { DateParseHandling = DateParseHandling.None })
                return JObject.Load(reader);
        }

        [Test]
        public void CreateJson_OmitsExpiryWhenThereIsNone()
        {
            JObject o = JObject.Parse(VoteService.BuildCreateJson("Q", new[] { "YES", "NO" }, null));
            Assert.IsNull(o["expires_at"], "a null expiry must be omitted so the column keeps its default");
        }

        // ════════════════════════════════════════════════════════════════════
        // Endpoints — the plurals do not match each other, and getting one wrong
        // is a 404 that reads like an auth problem
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void GiftEndpointsArePlural_VoteEndpointsAreSingular()
        {
            StringAssert.EndsWith("/api/v1/gifts/send-pts", Endpoints.GiftsSendPts);
            StringAssert.EndsWith("/api/v1/gifts/purchase", Endpoints.GiftsPurchase);
            StringAssert.EndsWith("/api/v1/gifts/items", Endpoints.GiftsItems);
            StringAssert.Contains("/api/v1/vote/list", Endpoints.VoteList(0, 20));
            StringAssert.EndsWith("/api/v1/vote/create", Endpoints.VoteCreate);
            StringAssert.Contains("/api/v1/vote/abc/cast", Endpoints.VoteCast("abc"));
            StringAssert.EndsWith("/api/v1/user/discover", Endpoints.UserDiscover);
            StringAssert.Contains("/points/earn?action=vote_cast", Endpoints.PointsEarn("vote_cast"));
        }

        // ════════════════════════════════════════════════════════════════════
        // Cast: 400 "Already voted" is a STATE, every other failure is a failure
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Cast_Success_RemembersTheVoteAndReturnsTheRepaintedRow()
        {
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":{\"id\":\"v1\",\"question\":\"Q\",\"vote_type\":\"yesNo\"," +
                "\"total_votes\":48,\"options\":[" +
                "{\"id\":\"o1\",\"label\":\"YES\",\"vote_count\":33,\"percentage\":68.8}," +
                "{\"id\":\"o2\",\"label\":\"NO\",\"vote_count\":15,\"percentage\":31.2}]}}"));

            var svc = new VoteService(_client);
            ApiResult<VoteDto> got = null;
            Pump.Drain(svc.Cast("v1", "o1", r => got = r));

            Assert.IsTrue(got.Success);
            Assert.AreEqual(48, got.Data.TotalVotes);
            Assert.AreEqual(68.8f, got.Data.Options[0].Percentage, 1e-3f);
            Assert.IsTrue(svc.VotedLocally("v1"), "a successful cast must be remembered for the session");
        }

        [Test]
        public void Cast_AlreadyVoted_IsRecognisedAndAlsoRemembered()
        {
            _transport.Enqueue(HttpResponse.Status(400, "{\"detail\":\"Already voted\"}"));

            var svc = new VoteService(_client);
            ApiResult<VoteDto> got = null;
            Pump.Drain(svc.Cast("v2", "o1", r => got = r));

            Assert.IsFalse(got.Success);
            Assert.IsTrue(VoteService.AlreadyVoted(got), "400 + 'Already voted' is the voted state");
            Assert.IsTrue(svc.VotedLocally("v2"), "the refusal is what teaches the client it voted");
        }

        [Test]
        public void Cast_OtherFailures_AreNotMistakenForAlreadyVoted()
        {
            // A 500 whose body happens to contain the phrase must NOT be read as "voted", or a
            // server fault would silently disable the button and eat the player's +10.
            _transport.Enqueue(HttpResponse.Status(500, "{\"detail\":\"Already voted\"}"));

            var svc = new VoteService(_client);
            ApiResult<VoteDto> got = null;
            Pump.Drain(svc.Cast("v3", "o1", r => got = r));

            Assert.IsFalse(VoteService.AlreadyVoted(got), "the STATUS is half the signal");
            Assert.IsFalse(svc.VotedLocally("v3"));
        }

        [Test]
        public void Cast_PlainBadRequest_IsNotAlreadyVoted()
        {
            _transport.Enqueue(HttpResponse.Status(400, "{\"detail\":\"Vote not found\"}"));
            var svc = new VoteService(_client);
            ApiResult<VoteDto> got = null;
            Pump.Drain(svc.Cast("v4", "o1", r => got = r));
            Assert.IsFalse(VoteService.AlreadyVoted(got), "the DETAIL is the other half");
        }

        // ════════════════════════════════════════════════════════════════════
        // The buy strip
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void BuyStrip_TakesTheCheapestThreeBasicRowsThatHaveAnActivityPrice()
        {
            var catalog = new List<GiftItemDto>
            {
                Item("a", "cap",     "hat",       "basic",   50,  null, true),
                Item("b", "crown",   "hat",       "premium", null, 900, true),   // wrong tier
                Item("c", "band",    "accessory", "basic",   40,  null, true),
                Item("d", "glove",   "gloves",    "basic",   30,  null, true),
                Item("e", "polo",    "tops",      "basic",   100, null, true),
                Item("f", "retired", "tops",      "basic",   10,  null, false),  // inactive
                Item("g", "noprice", "shoes",     "basic",   null, null, true),  // no activity price
            };

            List<GiftItemDto> strip = GiftService.BuyStrip(catalog);

            CollectionAssert.AreEqual(new[] { "d", "c", "a" }, strip.ConvertAll(i => i.Id),
                "cheapest first, basic tier only, active only, activity-priced only");
        }

        [Test]
        public void BuyStrip_IsStableWhenTwoRowsSharePrice()
        {
            var catalog = new List<GiftItemDto>
            {
                Item("zz", "b", "hat", "basic", 50, null, true),
                Item("aa", "a", "hat", "basic", 50, null, true),
            };
            // Ties break on id, so the strip does not shuffle between two fetches that happened
            // to return the catalog in a different order.
            CollectionAssert.AreEqual(new[] { "aa", "zz" },
                                      GiftService.BuyStrip(catalog).ConvertAll(i => i.Id));
            catalog.Reverse();
            CollectionAssert.AreEqual(new[] { "aa", "zz" },
                                      GiftService.BuyStrip(catalog).ConvertAll(i => i.Id));
        }

        [Test]
        public void BuyStrip_OfNothingIsEmptyRatherThanNull()
        {
            Assert.AreEqual(0, GiftService.BuyStrip(null).Count);
            Assert.AreEqual(0, GiftService.BuyStrip(new List<GiftItemDto>()).Count);
        }

        // ════════════════════════════════════════════════════════════════════
        // Supporter names — the ledger description is the ONLY counterparty record
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void SupporterName_ReadsBothWritersFormats()
        {
            // The pre-2026-09-02 router wrote the English form…
            Assert.AreEqual("Apple Reviewer", GiftService.SupporterName("Gift from Apple Reviewer"));
            // …and golfin_gift_pts writes the Japanese one.
            Assert.AreEqual("Cratilo", GiftService.SupporterName("ギフト受取: Cratilo"));
        }

        [Test]
        public void SupporterName_RejectsAnythingElse()
        {
            // The item-gift path writes "ギフト受取: <ITEM>" with the same prefix, which is why the
            // panel can show an item name as a supporter — but a description with NO recognised
            // prefix must be skipped rather than rendered.
            Assert.IsNull(GiftService.SupporterName("mode_entry_fee:practice"));
            Assert.IsNull(GiftService.SupporterName(null));
            Assert.IsNull(GiftService.SupporterName("   "));
            Assert.IsNull(GiftService.SupporterName("Gift from    "), "a blank name is not a supporter");
        }

        // ════════════════════════════════════════════════════════════════════
        // The supporters aggregation reads BOTH sources
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void Supporters_MergesItemGiftsAndTheRpLedger_SortedDescending()
        {
            // page 1 of /gifts/received (one item gift from Taro), then an empty page is not
            // needed because a short page ends the loop.
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"id\":\"g1\",\"sender_id\":\"u-taro\",\"gift_pts_awarded\":200," +
                "\"profiles\":{\"display_name\":\"Taro\"}}]}"));
            // page 1 of /points/history?currency=gift — two RP gifts, one of them from Taro too.
            _transport.Enqueue(HttpResponse.Status(200,
                "{\"data\":[" +
                "{\"id\":\"t1\",\"type\":\"gift_received\",\"amount\":100,\"currency\":\"gift\"," +
                "\"description\":\"Gift from Ken\"}," +
                "{\"id\":\"t2\",\"type\":\"gift_received\",\"amount\":50,\"currency\":\"gift\"," +
                "\"description\":\"ギフト受取: Taro\"}," +
                "{\"id\":\"t3\",\"type\":\"purchase\",\"amount\":-80,\"currency\":\"gift\"," +
                "\"description\":\"購入: cap\"}]}"));

            var svc = new GiftService(_client);
            List<SupporterTotal> got = null;
            Pump.Drain(svc.Supporters(s => got = s));

            Assert.AreEqual(2, got.Count, "a `purchase` row is not a gift and must not become a supporter");
            Assert.AreEqual("Taro", got[0].DisplayName);
            Assert.AreEqual(250, got[0].Points, "200 from the item gift + 50 from the ledger");
            Assert.AreEqual(2, got[0].GiftCount);
            Assert.AreEqual("u-taro", got[0].SenderId, "the id survives from whichever source has one");
            Assert.AreEqual("Ken", got[1].DisplayName);
            Assert.AreEqual(100, got[1].Points);
        }

        [Test]
        public void Supporters_SurvivesBothSourcesFailing()
        {
            _transport.Enqueue(HttpResponse.Status(500, "{}"), HttpResponse.Status(500, "{}"));

            var svc = new GiftService(_client);
            List<SupporterTotal> got = null;
            Pump.Drain(svc.Supporters(s => got = s));

            Assert.IsNotNull(got, "an empty panel, never a null the caller has to guard");
            Assert.AreEqual(0, got.Count);
        }

        // ════════════════════════════════════════════════════════════════════
        // VoteDto — the two derived values the card renders
        // ════════════════════════════════════════════════════════════════════

        [Test]
        public void DaysLeft_CeilsAndSurvivesNoExpiry()
        {
            var now = new System.DateTime(2026, 9, 2, 12, 0, 0, System.DateTimeKind.Utc);

            Assert.AreEqual(3, new VoteDto { ExpiresAt = "2026-09-05T00:00:00Z" }.DaysLeft(now),
                            "2.5 days rounds UP — the card says '3 days left' until the last one starts");
            Assert.IsNull(new VoteDto { ExpiresAt = null }.DaysLeft(now));
            Assert.IsNull(new VoteDto { ExpiresAt = "not a date" }.DaysLeft(now));
            Assert.Less(new VoteDto { ExpiresAt = "2026-09-01T00:00:00Z" }.DaysLeft(now).Value, 0,
                        "an expired-but-active row is a real state and stays negative here");
        }

        [Test]
        public void IsYesNo_IsAboutTheOPTIONS_NotTheTypeString()
        {
            Assert.IsTrue(new VoteDto
            {
                VoteType = "yesNo",
                Options = new List<VoteOptionDto> { new VoteOptionDto(), new VoteOptionDto() }
            }.IsYesNo);

            // A `yesNo` row with three options would render two bars and silently drop the third,
            // so the option count is what decides the card shape.
            Assert.IsFalse(new VoteDto
            {
                VoteType = "yesNo",
                Options = new List<VoteOptionDto> { new VoteOptionDto(), new VoteOptionDto(), new VoteOptionDto() }
            }.IsYesNo);
        }

        // ── helper ───────────────────────────────────────────────────────────

        private static GiftItemDto Item(string id, string name, string cat, string tier,
                                        int? act, int? gift, bool active)
            => new GiftItemDto
            {
                Id = id, Name = name, Category = cat, Tier = tier,
                PriceActivityPts = act, PriceGiftPts = gift, IsActive = active,
            };
    }
}
