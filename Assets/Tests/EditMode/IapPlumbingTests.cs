// Assets/Tests/EditMode/IapPlumbingTests.cs
// iap_plumbing — the client's money rules, pinned.
//
// WHAT THIS EXISTS TO CATCH.
//   * A StoreKit transaction is CONFIRMED only after the server answered 200 (ok |
//     already_processed). A 402, a 409, an offline answer leaves the order pending and grants
//     nothing — the whole "relaunch replays it, verify is idempotent, nothing is lost" property.
//   * The kill switch is fail-closed on the client too: `enabled=false`, an empty product
//     intersection, a store failure — none of them ever reports a product as available, so no
//     ¥ plate can draw.
//   * One purchase in flight; a second Purchase() answers Unavailable without touching the store.
//   * The listing gate: a `test.` row is listed ONLY while its product is buyable; a ¥-only row
//     likewise; a dual row with a real id stays listed (the card falls back to RP-only).
//   * The verify body mirrors routers/iap.py::GolfinVerifyRequest, and the HTTP→status map is
//     the one the router documents (409 → KillSwitchOff, 400/402/404 → Refused, else Offline).
//
// The IapFlow is REAL (Golfin.Economy); only the store driver and the verifier are fakes — the
// same posture as test_iap_golfin.py on the server, where Apple and Supabase are the fakes and the
// router is the code under test.
#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Golfin.Economy;
using Golfin.Net;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class IapPlumbingTests
    {
        private const string Sku = "test.tickets.x10";

        // ── fakes ──────────────────────────────────────────────────────────

        private sealed class FakeDriver : IIapStoreDriver
        {
            public event Action? Connected;
            public event Action<string>? Disconnected;
            public event Action<IReadOnlyList<IapProductInfo>>? ProductsFetched;
            public event Action<string>? ProductsFetchFailed;
            public event Action<IapPendingOrder>? PurchasePending;
            public event Action<IapFailedOrder>? PurchaseFailed;
            public event Action<string>? PurchaseDeferred;

            public readonly List<string> ConnectedWith = new List<string>();
            public readonly List<string> Purchased = new List<string>();
            public readonly List<IapPendingOrder> Confirmed = new List<IapPendingOrder>();

            public void Connect(IReadOnlyList<string> ids) { ConnectedWith.AddRange(ids); }
            public void Purchase(string productId) => Purchased.Add(productId);
            public void Confirm(IapPendingOrder order) => Confirmed.Add(order);

            // test drivers
            public void RaiseConnected() => Connected?.Invoke();
            public void RaiseDisconnected(string m) => Disconnected?.Invoke(m);
            public void RaiseProducts(params IapProductInfo[] p) => ProductsFetched?.Invoke(p);
            public void RaiseProductsFailed(string r) => ProductsFetchFailed?.Invoke(r);
            public void RaisePending(IapPendingOrder o) => PurchasePending?.Invoke(o);
            public void RaiseFailed(IapFailedOrder f) => PurchaseFailed?.Invoke(f);
            public void RaiseDeferred(string id) => PurchaseDeferred?.Invoke(id);
        }

        private sealed class FakeVerifier : IIapVerifier
        {
            public IapVerifyAnswer Answer = new IapVerifyAnswer { Status = IapVerifyStatus.Granted, HttpStatus = 200, Balance = 10 };
            public readonly List<(IapPendingOrder order, string? entryId)> Calls = new List<(IapPendingOrder, string?)>();
            public bool Hold;                      // when true, the answer is delivered by Release()
            private Action<IapVerifyAnswer>? _held;

            public void Verify(IapPendingOrder order, string? entryId, Action<IapVerifyAnswer> done)
            {
                Calls.Add((order, entryId));
                if (Hold) { _held = done; return; }
                done(Answer);
            }

            public void Release() { var d = _held; _held = null; d?.Invoke(Answer); }
        }

        private static IapProductInfo Product(string id = Sku, bool available = true, string price = "¥160")
            => new IapProductInfo { ProductId = id, Available = available, LocalizedPrice = price, CurrencyCode = "JPY" };

        private static IapPendingOrder Order(string id = Sku, string txn = "2000000123456789")
            => new IapPendingOrder { ProductId = id, TransactionId = txn, AppReceipt = "base64", Jws = "eyJ.jws" };

        private static (IapFlow flow, FakeDriver driver, FakeVerifier verifier) Ready()
        {
            var driver = new FakeDriver();
            var verifier = new FakeVerifier();
            var flow = new IapFlow(driver, verifier);
            flow.ApplyConfig(true, new[] { Sku }, new[] { Sku });
            driver.RaiseConnected();
            driver.RaiseProducts(Product());
            return (flow, driver, verifier);
        }

        // ── kill switch / availability ─────────────────────────────────────

        [Test]
        public void ServerDisabled_NeverConnectsTheStore_AndNoProductIsAvailable()
        {
            var driver = new FakeDriver();
            var flow = new IapFlow(driver, new FakeVerifier());
            flow.ApplyConfig(false, new[] { Sku }, new[] { Sku });

            Assert.AreEqual(IapState.Disabled, flow.State);
            Assert.IsEmpty(driver.ConnectedWith, "iap_enabled=false must never touch Unity IAP");
            Assert.IsFalse(flow.IsProductAvailable(Sku));
            Assert.IsFalse(flow.TryGetLocalizedPrice(Sku, out _));
        }

        [Test]
        public void OnlyTheIntersectionOfServerAndCatalogProductsIsFetched()
        {
            var driver = new FakeDriver();
            var flow = new IapFlow(driver, new FakeVerifier());
            flow.ApplyConfig(true, new[] { Sku, "golfin.only.on.server" }, new[] { Sku, "catalog.only" });

            Assert.AreEqual(IapState.Connecting, flow.State);
            CollectionAssert.AreEqual(new[] { Sku }, driver.ConnectedWith);
        }

        [Test]
        public void EnabledButNothingToSell_IsUnavailable_AndDoesNotConnect()
        {
            var driver = new FakeDriver();
            var flow = new IapFlow(driver, new FakeVerifier());
            flow.ApplyConfig(true, new[] { "server.sku" }, new[] { "catalog.sku" });

            Assert.AreEqual(IapState.Unavailable, flow.State);
            Assert.IsEmpty(driver.ConnectedWith);
        }

        [Test]
        public void ProductsFetched_MakesTheStoreReady_WithTheStoresOwnPriceString()
        {
            var (flow, _, _) = Ready();
            Assert.AreEqual(IapState.Ready, flow.State);
            Assert.IsTrue(flow.TryGetLocalizedPrice(Sku, out string price));
            Assert.AreEqual("¥160", price);
        }

        [Test]
        public void AnUnavailableProduct_OrAnEmptyPrice_IsNotAvailable()
        {
            var driver = new FakeDriver();
            var flow = new IapFlow(driver, new FakeVerifier());
            flow.ApplyConfig(true, new[] { Sku, "x.y" }, new[] { Sku, "x.y" });
            driver.RaiseConnected();
            driver.RaiseProducts(Product(available: false), Product(id: "x.y", price: ""));

            Assert.AreEqual(IapState.Ready, flow.State);
            Assert.IsFalse(flow.IsProductAvailable(Sku));
            Assert.IsFalse(flow.IsProductAvailable("x.y"));
        }

        [Test]
        public void StoreDisconnect_OrFetchFailure_DropsToUnavailable_AndRaisesTheEvent()
        {
            var (flow, driver, _) = Ready();
            int raised = 0;
            flow.AvailabilityChanged += () => raised++;

            driver.RaiseProductsFailed("boom");
            Assert.AreEqual(IapState.Unavailable, flow.State);
            Assert.IsFalse(flow.IsProductAvailable(Sku));

            driver.RaiseProducts(Product());
            Assert.AreEqual(IapState.Ready, flow.State);
            driver.RaiseDisconnected("gone");
            Assert.AreEqual(IapState.Unavailable, flow.State);
            Assert.AreEqual(3, raised);
        }

        // ── confirm only after the server granted ──────────────────────────

        [Test]
        public void Granted_ConfirmsTheOrder_RaisesGranted_AndResolvesTheCallback()
        {
            var (flow, driver, verifier) = Ready();
            IapPurchaseOutcome? outcome = null;
            int granted = 0;
            flow.Granted += () => granted++;

            flow.Purchase(Sku, "shop_ticket_gold_10_iap", o => outcome = o);
            CollectionAssert.AreEqual(new[] { Sku }, driver.Purchased);
            Assert.IsTrue(flow.PurchaseInFlight);

            var order = Order();
            driver.RaisePending(order);

            Assert.AreEqual(1, verifier.Calls.Count);
            Assert.AreEqual("shop_ticket_gold_10_iap", verifier.Calls[0].entryId, "the entry id labels the history row");
            CollectionAssert.AreEqual(new[] { order }, driver.Confirmed, "confirmed exactly once, after the 200");
            Assert.AreEqual(1, granted);
            Assert.AreEqual(IapPurchaseOutcome.Granted, outcome);
            Assert.IsFalse(flow.PurchaseInFlight);
        }

        [Test]
        public void AlreadyProcessed_AlsoConfirms_ButReportsAlreadyGranted()
        {
            var (flow, driver, verifier) = Ready();
            verifier.Answer = new IapVerifyAnswer { Status = IapVerifyStatus.AlreadyGranted, HttpStatus = 200 };
            IapPurchaseOutcome? outcome = null;
            flow.Purchase(Sku, null, o => outcome = o);
            driver.RaisePending(Order());
            Assert.AreEqual(1, driver.Confirmed.Count);
            Assert.AreEqual(IapPurchaseOutcome.AlreadyGranted, outcome);
        }

        [TestCase(IapVerifyStatus.Refused, 402, IapPurchaseOutcome.VerifyRefused)]
        [TestCase(IapVerifyStatus.KillSwitchOff, 409, IapPurchaseOutcome.KillSwitchOff)]
        [TestCase(IapVerifyStatus.Offline, 0, IapPurchaseOutcome.Offline)]
        public void AnythingButA200_LeavesTheOrderPending_AndGrantsNothing(IapVerifyStatus status, long http, IapPurchaseOutcome expected)
        {
            var (flow, driver, verifier) = Ready();
            verifier.Answer = new IapVerifyAnswer { Status = status, HttpStatus = http, Message = "no" };
            int granted = 0;
            flow.Granted += () => granted++;
            IapPurchaseOutcome? outcome = null;

            flow.Purchase(Sku, null, o => outcome = o);
            driver.RaisePending(Order());

            Assert.IsEmpty(driver.Confirmed, "NOT confirmed — StoreKit re-delivers it next launch");
            Assert.AreEqual(0, granted);
            Assert.AreEqual(expected, outcome);
            Assert.IsFalse(flow.PurchaseInFlight, "the button is released even though the order stays pending");
        }

        [Test]
        public void AReplayedOrderFromAPreviousSession_IsVerifiedAndConfirmed_WithoutAnActivePurchase()
        {
            var (flow, driver, verifier) = Ready();
            int granted = 0;
            flow.Granted += () => granted++;

            driver.RaisePending(Order(txn: "old-txn"));   // no Purchase() call preceded it

            Assert.AreEqual(1, verifier.Calls.Count);
            Assert.IsNull(verifier.Calls[0].entryId, "no card was tapped — no entry id to label with");
            Assert.AreEqual(1, driver.Confirmed.Count);
            Assert.AreEqual(1, granted);
        }

        // ── one purchase at a time / store failures ────────────────────────

        [Test]
        public void ASecondPurchaseWhileOneIsInFlight_IsUnavailable_AndNeverReachesTheStore()
        {
            var (flow, driver, verifier) = Ready();
            verifier.Hold = true;
            IapPurchaseOutcome? second = null;

            flow.Purchase(Sku, null, _ => { });
            driver.RaisePending(Order());              // verify is held: still in flight
            flow.Purchase(Sku, null, o => second = o);

            Assert.AreEqual(IapPurchaseOutcome.Unavailable, second);
            Assert.AreEqual(1, driver.Purchased.Count);

            verifier.Release();
            Assert.IsFalse(flow.PurchaseInFlight);
        }

        [Test]
        public void PurchaseWhenNotReady_IsUnavailable_WithoutTouchingTheStore()
        {
            var driver = new FakeDriver();
            var flow = new IapFlow(driver, new FakeVerifier());
            flow.ApplyConfig(false, Array.Empty<string>(), Array.Empty<string>());
            IapPurchaseOutcome? outcome = null;
            flow.Purchase(Sku, null, o => outcome = o);
            Assert.AreEqual(IapPurchaseOutcome.Unavailable, outcome);
            Assert.IsEmpty(driver.Purchased);
        }

        [Test]
        public void UserCancel_IsCancelled_OtherFailures_AreFailed_DeferredIsDeferred()
        {
            var (flow, driver, _) = Ready();
            IapPurchaseOutcome? outcome = null;

            flow.Purchase(Sku, null, o => outcome = o);
            driver.RaiseFailed(new IapFailedOrder { ProductId = Sku, UserCancelled = true, Reason = "UserCancelled" });
            Assert.AreEqual(IapPurchaseOutcome.Cancelled, outcome);

            flow.Purchase(Sku, null, o => outcome = o);
            driver.RaiseFailed(new IapFailedOrder { ProductId = Sku, Reason = "PaymentDeclined" });
            Assert.AreEqual(IapPurchaseOutcome.Failed, outcome);

            flow.Purchase(Sku, null, o => outcome = o);
            driver.RaiseDeferred(Sku);
            Assert.AreEqual(IapPurchaseOutcome.Deferred, outcome);
            Assert.IsFalse(flow.PurchaseInFlight);
        }

        // ── the wire ───────────────────────────────────────────────────────

        [Test]
        public void VerifyBody_MirrorsGolfinVerifyRequest()
        {
            string json = ApiIapVerifier.BuildVerifyJson(Order(), "shop_ticket_gold_10_iap", 2950);
            // This assembly has no Newtonsoft reference; the body is small enough to pin verbatim.
            StringAssert.Contains("\"platform\":\"apple\"", json);
            StringAssert.Contains("\"product_id\":\"" + Sku + "\"", json);
            StringAssert.Contains("\"transaction_id\":\"2000000123456789\"", json);
            StringAssert.Contains("\"receipt_data\":\"base64\"", json);
            StringAssert.Contains("\"jws\":\"eyJ.jws\"", json);
            StringAssert.Contains("\"entry_id\":\"shop_ticket_gold_10_iap\"", json);
            StringAssert.Contains("\"build\":2950", json);
            StringAssert.Contains("\"sandbox\":true", json);
            StringAssert.DoesNotContain("user_id", json, "the user comes from the token, never the body");
        }

        [TestCase(409, IapVerifyStatus.KillSwitchOff)]
        [TestCase(402, IapVerifyStatus.Refused)]
        [TestCase(400, IapVerifyStatus.Refused)]
        [TestCase(404, IapVerifyStatus.Refused)]
        [TestCase(401, IapVerifyStatus.Offline)]
        [TestCase(500, IapVerifyStatus.Offline)]
        [TestCase(0,   IapVerifyStatus.Offline)]
        public void HttpFailures_MapToTheRouterDocumentedStatuses(long http, IapVerifyStatus expected)
        {
            var result = ApiResult<IapVerifyResultDto>.Fail(ApiErrorKind.Server, "x", http, "{}", 1);
            Assert.AreEqual(expected, ApiIapVerifier.Map(result).Status);
            Assert.AreEqual(IapVerifyStatus.Offline, ApiIapVerifier.Map(null).Status);
        }

        [Test]
        public void A200_MapsToGranted_OrAlreadyGranted_ByTheBodyMarkers()
        {
            var ok = ApiResult<IapVerifyResultDto>.Ok(new IapVerifyResultDto { Verified = true, Status = "ok", Balance = 10 }, 200, "{}", 1);
            var again = ApiResult<IapVerifyResultDto>.Ok(new IapVerifyResultDto { Verified = true, AlreadyProcessed = true, Status = "already_processed" }, 200, "{}", 1);
            Assert.AreEqual(IapVerifyStatus.Granted, ApiIapVerifier.Map(ok).Status);
            Assert.AreEqual(10, ApiIapVerifier.Map(ok).Balance);
            Assert.AreEqual(IapVerifyStatus.AlreadyGranted, ApiIapVerifier.Map(again).Status);
        }

        // ── the catalog's listing gate + the card's helpers (Assembly-CSharp, by reflection) ──

        private static Type Catalog => Type.GetType("GolfinRedux.UI.Shop.GeneralShopCatalog, Assembly-CSharp")
                                       ?? throw new InvalidOperationException("GeneralShopCatalog not found");
        private static Type Entry => Type.GetType("GolfinRedux.UI.Shop.ShopCatalogEntry, Assembly-CSharp")
                                     ?? throw new InvalidOperationException("ShopCatalogEntry not found");

        private static object MakeEntry(int rpCost, string storeProductId)
        {
            object e = Activator.CreateInstance(Entry)!;
            Entry.GetProperty("EntryId")!.SetValue(e, "e");
            Entry.GetProperty("RpCost")!.SetValue(e, rpCost);
            Entry.GetProperty("StoreProductId")!.SetValue(e, storeProductId);
            return e;
        }

        private static bool ListedNow(object entry, Func<string, bool> availability)
        {
            var over = Catalog.GetMethod("OverrideStoreAvailabilityForTest", BindingFlags.Public | BindingFlags.Static)!;
            over.Invoke(null, new object?[] { availability });
            try
            {
                return (bool)Catalog.GetMethod("ListedNow", BindingFlags.Public | BindingFlags.Static)!
                                    .Invoke(null, new[] { entry })!;
            }
            finally { over.Invoke(null, new object?[] { null }); }
        }

        [Test]
        public void ListingGate_ARowWithNoProductId_IsAlwaysListed()
        {
            Assert.IsTrue(ListedNow(MakeEntry(200, ""), _ => false));
        }

        [Test]
        public void ListingGate_ASandboxRow_IsListedOnlyWhileBuyable()
        {
            var e = MakeEntry(450, "test.tickets.x10");
            Assert.IsTrue((bool)Entry.GetProperty("IsSandboxOnly")!.GetValue(e)!);
            Assert.IsFalse(ListedNow(e, _ => false), "flag off / not connected / product missing ⇒ withheld, NOT rp-only");
            Assert.IsTrue(ListedNow(e, id => id == "test.tickets.x10"));
        }

        [Test]
        public void ListingGate_AMoneyOnlyRow_IsListedOnlyWhileBuyable_ADualRowStaysListed()
        {
            var moneyOnly = MakeEntry(0, "golfin.tickets.x10");
            var dual = MakeEntry(450, "golfin.tickets.x10");
            Assert.IsFalse(ListedNow(moneyOnly, _ => false), "a BUY that cannot work is not shown");
            Assert.IsTrue(ListedNow(moneyOnly, _ => true));
            Assert.IsTrue(ListedNow(dual, _ => false), "dual falls back to RP-only on the card, so it stays listed");
        }

        [Test]
        public void DiscountLabel_IsRoundedWholePercent_NeverMinusZero()
        {
            var card = Type.GetType("GolfinRedux.UI.Shop.GeneralShopCard, Assembly-CSharp")!;
            var m = card.GetMethod("DiscountLabel", BindingFlags.Public | BindingFlags.Static)!;
            Assert.AreEqual("-25%", m.Invoke(null, new object[] { 600, 450 }));
            Assert.AreEqual("-33%", m.Invoke(null, new object[] { 300, 200 }));
            Assert.AreEqual("-1%",  m.Invoke(null, new object[] { 1000, 998 }), "a real discount never reads -0%");
            Assert.AreEqual("",     m.Invoke(null, new object[] { 450, 450 }));
            Assert.AreEqual("",     m.Invoke(null, new object[] { 0, 0 }));
        }

        [Test]
        public void StoreHistory_FormatMoney_IsYenWithGrouping_ForJpy()
        {
            var row = Type.GetType("GolfinRedux.UI.Shop.StoreHistoryRow, Assembly-CSharp")!;
            var m = row.GetMethod("FormatMoney", BindingFlags.Public | BindingFlags.Static)!;
            Assert.AreEqual("¥1,500", m.Invoke(null, new object[] { 1500, "JPY" }));
            Assert.AreEqual("¥160", m.Invoke(null, new object[] { 160, "jpy" }));
            Assert.AreEqual("12 USD", m.Invoke(null, new object[] { 12, "USD" }));
        }
    }
}
