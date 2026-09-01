// Order: gps_trust_core §Tests — VenueService URL building and array unwrapping.
using System.Collections.Generic;
using Golfin.Net;
using Golfin.Net.Tests;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class VenueServiceTests
    {
        [Test]
        public void Nearby_BuildsTheEscapedPrefixQueryAndUnwrapsTheArray()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"id\":1,\"name\":\"Tokyo GC\",\"geohash\":\"xn76urx66\",\"latitude\":35.68,\"longitude\":139.76}," +
                "{\"id\":2,\"name\":\"Chiba CC\",\"geohash\":\"xn77abcde\"}]}"));

            ApiResult<List<VenueDto>> result = null;
            Pump.Drain(new VenueService(GpsTestApi.Client(transport)).Nearby("xn76,xn77", "ja", r => result = r));

            // NOTE the LOWERCASE %2c: UnityWebRequest.EscapeURL emits lowercase hex. Percent-encoding
            // is case-insensitive (RFC 3986 §6.2.2.1) so the router reads it identically — the spec's
            // "%2C" is the same URL, and this asserts what Unity actually produces.
            Assert.AreEqual(Endpoints.BaseUrl + "/venue/nearby?prefixes=xn76%2cxn77&language_code=ja",
                            transport.SentUrls[0]);
            Assert.AreEqual("GET", transport.SentMethods[0]);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Data.Count);
            Assert.AreEqual(1, result.Data[0].Id);
            Assert.AreEqual("Tokyo GC", result.Data[0].Name);
            Assert.AreEqual("xn76urx66", result.Data[0].Geohash);
            Assert.IsNull(result.Data[1].Latitude, "a sparse OSM-sourced row must parse, not throw");
        }

        [Test]
        public void List_BuildsTheLanguageQuery()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200, "{\"data\":[]}"));

            ApiResult<List<VenueDto>> result = null;
            Pump.Drain(new VenueService(GpsTestApi.Client(transport)).List("en", r => result = r));

            Assert.AreEqual(Endpoints.BaseUrl + "/venue/list?language_code=en", transport.SentUrls[0]);
            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.Data.Count);
        }

        [Test]
        public void VenueDto_IgnoresColumnsTheClientDoesNotKnowAbout()
        {
            // /venue/list and /nearby both select("*"), so the server can grow a column without a
            // client release. This test is the guard on that.
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":[{\"id\":7,\"name\":\"New CC\",\"a_column_from_the_future\":123}]}"));

            ApiResult<List<VenueDto>> result = null;
            Pump.Drain(new VenueService(GpsTestApi.Client(transport)).List("ja", r => result = r));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(7, result.Data[0].Id);
        }

        [Test]
        public void AutoRegister_NullDataIsASuccessNotAnError()
        {
            var transport = new FakeHttpTransport().Enqueue(HttpResponse.Status(200,
                "{\"data\":null,\"message\":\"No golf course found nearby. Fall back to manual selection.\"}"));

            ApiResult<VenueAutoRegisterResult> result = null;
            Pump.Drain(new VenueService(GpsTestApi.Client(transport)).AutoRegister(35.68, 139.76, r => result = r));

            Assert.IsTrue(result.Success, "200 {data:null} is the 'no course nearby' branch");
            Assert.IsNull(result.Data);
        }

        [Test]
        public void Endpoints_GpsSectionUrls()
        {
            Assert.AreEqual(Endpoints.BaseUrl + "/venue/auto-register", Endpoints.VenueAutoRegister);
            Assert.AreEqual(Endpoints.BaseUrl + "/venue/7?language_code=ja", Endpoints.VenueById(7));
            Assert.AreEqual(Endpoints.BaseUrl + "/score/submit", Endpoints.ScoreSubmit);
            Assert.AreEqual(Endpoints.BaseUrl + "/activity/checkin", Endpoints.ActivityCheckin);
            Assert.AreEqual(Endpoints.BaseUrl + "/activity/12/checkout", Endpoints.ActivityCheckout("12"));
            Assert.AreEqual(Endpoints.BaseUrl + "/activity/12/cancel", Endpoints.ActivityCancel("12"));
            Assert.AreEqual(Endpoints.BaseUrl + "/activity/history?skip=0&limit=20", Endpoints.ActivityHistory());
        }
    }
}
