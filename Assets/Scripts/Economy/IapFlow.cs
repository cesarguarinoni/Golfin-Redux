// Assets/Scripts/Economy/IapFlow.cs
// iap_plumbing (2026-09-15) — the GOLFIN real-money purchase pipeline, the DECISIONS.
//
//   StoreKit (Unity IAP 5.4.3) ──▶ POST /api/v1/iap/golfin/verify ──▶ golfin_iap_grant()
//                                        (Apple receipt check)          (ticket ledger, one txn)
//
// THREE RULES, AND EACH IS LOAD-BEARING FOR MONEY:
//
//   1. THE SERVER DECIDES WHETHER IAP EXISTS AT ALL. `GET /iap/golfin/config` reads
//      `content_settings.iap_enabled` fail-closed. A false answer means: Unity IAP is never
//      initialised, no ¥ plate is ever drawn, no dual row can open the payment modal. The flag
//      is read live from the server rather than from the content delta because the delta is
//      applied from cache at boot and would lag a launch; a money switch must not lag.
//
//   2. A TRANSACTION IS CONFIRMED ONLY AFTER THE SERVER GRANTED IT. Unity IAP 5's two-step flow
//      hands us a PendingOrder; we send its receipt to the server and call ConfirmPurchase ONLY on
//      a 200 (`ok` or `already_processed`). A 402/409/offline leaves the order pending, StoreKit
//      re-delivers it on the next launch, the verify is idempotent by transaction id, and nothing
//      is lost or double-granted. There is no client-side credit path: the ticket balance moves
//      because the ledger moved, and the client merely re-reads it.
//
//   3. NOTHING HERE IS A PRICE. The ¥ string on a card is StoreKit's `localizedPriceString`, the
//      granted quantity comes off the server's `iap_products` row, and the RP price of a dual row
//      keeps flowing through `golfin_shop_purchase` exactly as before.
//
// WHY THIS FILE LIVES IN Golfin.Economy AND THE HOST IN Assets/Scripts/Services. Everything
// decided here is pinned by the EditMode suite (GolfinRedux.Tests.EditMode references this
// assembly); Unity IAP itself and the scene singletons the host re-reads after a grant are
// Assembly-CSharp territory, so IapService.cs (the MonoBehaviour host + UnityIapStoreDriver) sits
// there, beside the shop UI that calls it.
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Economy
{
    /// <summary>A store product as the driver reports it: the store's own localized price string.</summary>
    public sealed class IapProductInfo
    {
        public string ProductId = string.Empty;
        /// <summary>StoreKit's <c>localizedPriceString</c> ("¥160"); the FakeStore's "$0.01" in the Editor.</summary>
        public string LocalizedPrice = string.Empty;
        public string CurrencyCode = string.Empty;
        public bool Available;
    }

    /// <summary>A pending StoreKit order — everything the server verify needs, plus the handle
    /// <see cref="IIapStoreDriver.Confirm"/> takes back.</summary>
    public sealed class IapPendingOrder
    {
        public string ProductId = string.Empty;
        public string TransactionId = string.Empty;
        /// <summary>Apple App Receipt, base64 — what <c>/verifyReceipt</c> validates. Null when the
        /// store is not Apple (Editor FakeStore).</summary>
        public string? AppReceipt;
        /// <summary>StoreKit 2 signed transaction (JWS). Stored server-side, not verified yet.</summary>
        public string? Jws;
        /// <summary>The driver's own order object, handed back verbatim to <see cref="IIapStoreDriver.Confirm"/>.</summary>
        public object? Handle;
    }

    /// <summary>What the store said when a purchase did not go through.</summary>
    public sealed class IapFailedOrder
    {
        public string ProductId = string.Empty;
        public bool UserCancelled;
        public string Reason = string.Empty;
        public string Details = string.Empty;
    }

    /// <summary>The store as IapService sees it. One real implementation, one test fake.</summary>
    public interface IIapStoreDriver
    {
        event Action? Connected;
        event Action<string>? Disconnected;
        event Action<IReadOnlyList<IapProductInfo>>? ProductsFetched;
        event Action<string>? ProductsFetchFailed;
        event Action<IapPendingOrder>? PurchasePending;
        event Action<IapFailedOrder>? PurchaseFailed;
        event Action<string>? PurchaseDeferred;

        /// <summary>Subscribe, connect, and — on connect — fetch <paramref name="consumableProductIds"/>
        /// and any purchases left pending by a previous session.</summary>
        void Connect(IReadOnlyList<string> consumableProductIds);
        void Purchase(string productId);
        /// <summary>Finish the StoreKit transaction. Called ONLY after the server answered 200.</summary>
        void Confirm(IapPendingOrder order);
    }

    public enum IapState
    {
        /// <summary>Not started, or the backend flag is off (bot / offline dev mode).</summary>
        Off,
        /// <summary>The server said <c>iap_enabled = false</c>. Unity IAP was never touched.</summary>
        Disabled,
        /// <summary>Server enabled; waiting on the store connection / product fetch.</summary>
        Connecting,
        /// <summary>Connected and products fetched — ¥ plates may draw.</summary>
        Ready,
        /// <summary>Server enabled but the store could not be reached or the fetch failed.</summary>
        Unavailable,
    }

    /// <summary>What a <see cref="IapFlow.Purchase"/> ends with. Exactly one per call.</summary>
    public enum IapPurchaseOutcome
    {
        Granted,
        AlreadyGranted,
        Cancelled,
        Deferred,
        Failed,
        /// <summary>Apple, or our server, refused the receipt. NOT confirmed; re-delivered next launch.</summary>
        VerifyRefused,
        /// <summary>The kill switch went off between the card and the verify. NOT confirmed.</summary>
        KillSwitchOff,
        /// <summary>No server answer. NOT confirmed; StoreKit replays it and the verify is idempotent.</summary>
        Offline,
        /// <summary>IAP is not ready, or another purchase is in flight.</summary>
        Unavailable,
    }

    public enum IapVerifyStatus { Granted, AlreadyGranted, Refused, KillSwitchOff, Offline }

    public sealed class IapVerifyAnswer
    {
        public IapVerifyStatus Status;
        public long HttpStatus;
        public int Balance;
        public string Message = string.Empty;
    }

    /// <summary>The server round-trip, behind an interface so the flow's confirm rule is testable.</summary>
    public interface IIapVerifier
    {
        void Verify(IapPendingOrder order, string? entryId, Action<IapVerifyAnswer> done);
    }

    // ── Wire DTOs (snake_case, mirror routers/iap.py's golfin arm) ─────────────

    public sealed class IapConfigDto
    {
        [JsonProperty("enabled")]  public bool Enabled;
        [JsonProperty("platform")] public string? Platform;
        [JsonProperty("products")] public IapConfigProductDto[]? Products;
    }

    public sealed class IapConfigProductDto
    {
        [JsonProperty("product_id")] public string ProductId = string.Empty;
        [JsonProperty("kind")]       public string? Kind;
        [JsonProperty("grant_ref")]  public string? GrantRef;
        [JsonProperty("grant_qty")]  public int GrantQty;
        [JsonProperty("price_jpy")]  public int PriceJpy;
        [JsonProperty("currency")]   public string? Currency;
    }

    public sealed class IapVerifyResultDto
    {
        [JsonProperty("verified")]          public bool Verified;
        [JsonProperty("already_processed")] public bool AlreadyProcessed;
        [JsonProperty("status")]            public string? Status;
        [JsonProperty("balance")]           public int Balance;
        [JsonProperty("granted")]           public IapGrantedDto? Granted;
    }

    public sealed class IapGrantedDto
    {
        [JsonProperty("kind")]   public string? Kind;
        [JsonProperty("ref_id")] public string? RefId;
        [JsonProperty("amount")] public int Amount;
    }

    /// <summary>
    /// The decisions, with no Unity IAP and no HTTP in them — those come in through
    /// <see cref="IIapStoreDriver"/> and <see cref="IIapVerifier"/>. <see cref="IapService"/> hosts
    /// the shipping instance; the EditMode suite drives one with fakes.
    /// </summary>
    public sealed class IapFlow
    {
        private readonly IIapStoreDriver _driver;
        private readonly IIapVerifier _verifier;
        private readonly Action<string> _log;

        private readonly Dictionary<string, IapProductInfo> _products =
            new Dictionary<string, IapProductInfo>(StringComparer.Ordinal);

        private string? _activeProductId;
        private string? _activeEntryId;
        private Action<IapPurchaseOutcome>? _activeResult;

        public IapState State { get; private set; } = IapState.Off;
        public bool ServerEnabled { get; private set; }
        public bool PurchaseInFlight => _activeResult != null;

        /// <summary>State or product availability changed — the store screen re-binds its cards.</summary>
        public event Action? AvailabilityChanged;

        /// <summary>A purchase was granted server-side and confirmed with the store. The host
        /// re-reads the ticket ledger and the purchase log on it.</summary>
        public event Action? Granted;

        public IapFlow(IIapStoreDriver driver, IIapVerifier verifier, Action<string>? log = null)
        {
            _driver = driver;
            _verifier = verifier;
            _log = log ?? (_ => { });

            _driver.Connected           += OnConnected;
            _driver.Disconnected        += OnDisconnected;
            _driver.ProductsFetched     += OnProductsFetched;
            _driver.ProductsFetchFailed += OnProductsFetchFailed;
            _driver.PurchasePending     += OnPurchasePending;
            _driver.PurchaseFailed      += OnPurchaseFailed;
            _driver.PurchaseDeferred    += OnPurchaseDeferred;
        }

        // ── Boot ───────────────────────────────────────────────────────────────

        /// <summary>
        /// The server's answer, applied. <paramref name="serverProductIds"/> is what the server will
        /// grant; <paramref name="catalogProductIds"/> is every <c>storeProductId</c> the shop catalog
        /// names. Only the INTERSECTION is fetched from the store: a product the server would refuse
        /// must never be sold, and a product the catalog has no row for has no card to sell it from.
        /// </summary>
        public void ApplyConfig(bool enabled, IReadOnlyList<string> serverProductIds,
                                IReadOnlyList<string> catalogProductIds)
        {
            ServerEnabled = enabled;
            if (!enabled)
            {
                _products.Clear();
                SetState(IapState.Disabled);
                _log("iap_enabled is OFF on the server — Unity IAP not initialised; no ¥ plates this session.");
                return;
            }

            var server = new HashSet<string>(serverProductIds, StringComparer.Ordinal);
            var sell = new List<string>();
            foreach (var id in catalogProductIds)
                if (!string.IsNullOrEmpty(id) && server.Contains(id) && !sell.Contains(id)) sell.Add(id);

            if (sell.Count == 0)
            {
                // Enabled, but nothing this build can both show AND the server will grant.
                _products.Clear();
                SetState(IapState.Unavailable);
                _log($"iap_enabled is ON but no catalog storeProductId is in the server's list " +
                     $"(server: {string.Join(",", serverProductIds)}; catalog: {string.Join(",", catalogProductIds)}). Nothing to sell.");
                return;
            }

            SetState(IapState.Connecting);
            _log($"iap_enabled is ON — connecting the store for {sell.Count} product(s): {string.Join(",", sell)}");
            _driver.Connect(sell);
        }

        // ── Queries the cards ask ─────────────────────────────────────────────

        public bool IsProductAvailable(string? productId)
            => State == IapState.Ready && !string.IsNullOrEmpty(productId)
               && _products.TryGetValue(productId!, out var p) && p.Available
               && !string.IsNullOrEmpty(p.LocalizedPrice);

        public bool TryGetLocalizedPrice(string? productId, out string price)
        {
            price = string.Empty;
            if (!IsProductAvailable(productId)) return false;
            price = _products[productId!].LocalizedPrice;
            return true;
        }

        // ── Purchase ──────────────────────────────────────────────────────────

        /// <summary><paramref name="onResult"/> is invoked exactly once. <paramref name="entryId"/>
        /// labels the Store History row server-side and is not otherwise trusted.</summary>
        public void Purchase(string productId, string? entryId, Action<IapPurchaseOutcome> onResult)
        {
            if (!IsProductAvailable(productId))
            {
                _log($"Purchase('{productId}') refused — state {State}, product not available.");
                onResult(IapPurchaseOutcome.Unavailable);
                return;
            }
            if (PurchaseInFlight)
            {
                _log($"Purchase('{productId}') ignored — '{_activeProductId}' is still in flight.");
                onResult(IapPurchaseOutcome.Unavailable);
                return;
            }

            _activeProductId = productId;
            _activeEntryId   = entryId;
            _activeResult    = onResult;
            _driver.Purchase(productId);
        }

        // ── Driver events ─────────────────────────────────────────────────────

        private void OnConnected()
        {
            _log("store connected; fetching products.");
        }

        private void OnDisconnected(string message)
        {
            _log($"store disconnected: {message}");
            if (State != IapState.Disabled) SetState(IapState.Unavailable);
        }

        private void OnProductsFetched(IReadOnlyList<IapProductInfo> products)
        {
            _products.Clear();
            foreach (var p in products)
                if (!string.IsNullOrEmpty(p.ProductId)) _products[p.ProductId] = p;

            SetState(IapState.Ready);
            foreach (var p in _products.Values)
                _log($"product '{p.ProductId}' → {(p.Available ? "available" : "NOT available")} at '{p.LocalizedPrice}' ({p.CurrencyCode})");
        }

        private void OnProductsFetchFailed(string reason)
        {
            _log($"product fetch failed: {reason}");
            _products.Clear();
            SetState(IapState.Unavailable);
        }

        private void OnPurchasePending(IapPendingOrder order)
        {
            bool isActive = _activeResult != null && string.Equals(order.ProductId, _activeProductId, StringComparison.Ordinal);
            string? entryId = isActive ? _activeEntryId : null;
            _log($"pending order for '{order.ProductId}' txn {order.TransactionId} — " +
                 (isActive ? "verifying." : "REPLAYED from a previous session; verifying (idempotent)."));

            _verifier.Verify(order, entryId, answer =>
            {
                switch (answer.Status)
                {
                    case IapVerifyStatus.Granted:
                    case IapVerifyStatus.AlreadyGranted:
                        // The ONLY branch that finishes the StoreKit transaction.
                        _driver.Confirm(order);
                        _log($"server {answer.Status} txn {order.TransactionId}; balance {answer.Balance}. Confirmed with the store.");
                        Granted?.Invoke();
                        Resolve(order.ProductId, answer.Status == IapVerifyStatus.Granted
                            ? IapPurchaseOutcome.Granted : IapPurchaseOutcome.AlreadyGranted);
                        return;

                    case IapVerifyStatus.KillSwitchOff:
                        _log($"server 409 for txn {order.TransactionId} — kill switch off. Left PENDING.");
                        Resolve(order.ProductId, IapPurchaseOutcome.KillSwitchOff);
                        return;

                    case IapVerifyStatus.Refused:
                        _log($"server refused txn {order.TransactionId} (HTTP {answer.HttpStatus}: {answer.Message}). Left PENDING, nothing granted.");
                        Resolve(order.ProductId, IapPurchaseOutcome.VerifyRefused);
                        return;

                    default:
                        _log($"no server answer for txn {order.TransactionId} ({answer.Message}). Left PENDING; replays next launch.");
                        Resolve(order.ProductId, IapPurchaseOutcome.Offline);
                        return;
                }
            });
        }

        private void OnPurchaseFailed(IapFailedOrder failed)
        {
            _log($"purchase failed for '{failed.ProductId}': {failed.Reason} {failed.Details}");
            Resolve(failed.ProductId, failed.UserCancelled ? IapPurchaseOutcome.Cancelled : IapPurchaseOutcome.Failed);
        }

        private void OnPurchaseDeferred(string productId)
        {
            _log($"purchase deferred for '{productId}' (Ask to Buy) — it arrives as a pending order when approved.");
            Resolve(productId, IapPurchaseOutcome.Deferred);
        }

        private void Resolve(string productId, IapPurchaseOutcome outcome)
        {
            if (_activeResult == null) return;
            if (!string.IsNullOrEmpty(_activeProductId) &&
                !string.Equals(productId, _activeProductId, StringComparison.Ordinal)) return;

            var cb = _activeResult;
            _activeResult = null;
            _activeProductId = null;
            _activeEntryId = null;
            cb(outcome);
        }

        private void SetState(IapState state)
        {
            State = state;
            AvailabilityChanged?.Invoke();
        }
    }

    /// <summary>
    /// The shipping verifier: <c>POST /iap/golfin/verify</c> through <see cref="ApiClient"/>
    /// (bearer token, retries, envelope unwrap). Maps the HTTP shape the router documents.
    /// </summary>
    public sealed class ApiIapVerifier : IIapVerifier
    {
        private readonly ApiClient _client;
        private readonly Func<int> _build;

        /// <param name="build">The running build number (<c>ContentBuildNumber.Current</c>) — a
        /// Func because this assembly does not reference Golfin.Content, same as
        /// ShopPurchaseService taking <c>build</c> as a parameter.</param>
        public ApiIapVerifier(ApiClient client, Func<int>? build = null)
        {
            _client = client;
            _build = build ?? (() => 0);
        }

        public void Verify(IapPendingOrder order, string? entryId, Action<IapVerifyAnswer> done)
            => _client.Run(Routine(order, entryId, done));

        public IEnumerator Routine(IapPendingOrder order, string? entryId, Action<IapVerifyAnswer> done)
        {
            string body = BuildVerifyJson(order, entryId, _build());
            ApiResult<IapVerifyResultDto>? result = null;
            IEnumerator call = _client.Post<IapVerifyResultDto>(Endpoints.IapGolfinVerify, body, r => result = r);
            while (call.MoveNext()) yield return call.Current;
            done(Map(result));
        }

        public static string BuildVerifyJson(IapPendingOrder order, string? entryId, int build)
            => JsonConvert.SerializeObject(new VerifyBody
            {
                platform       = "apple",
                product_id     = order.ProductId,
                transaction_id = order.TransactionId,
                receipt_data   = order.AppReceipt ?? string.Empty,
                jws            = order.Jws,
                entry_id       = entryId,
                build          = Mathf.Max(0, build),
                sandbox        = true,
            });

        public static IapVerifyAnswer Map(ApiResult<IapVerifyResultDto>? result)
        {
            if (result == null)
                return new IapVerifyAnswer { Status = IapVerifyStatus.Offline, Message = "no result" };

            if (result.Success && result.Data != null)
            {
                bool already = result.Data.AlreadyProcessed || result.Data.Status == "already_processed";
                return new IapVerifyAnswer
                {
                    Status     = already ? IapVerifyStatus.AlreadyGranted : IapVerifyStatus.Granted,
                    HttpStatus = result.StatusCode,
                    Balance    = result.Data.Balance,
                };
            }

            var answer = new IapVerifyAnswer { HttpStatus = result.StatusCode, Message = result.ErrorMessage ?? result.ToString() };
            switch (result.StatusCode)
            {
                case 409: answer.Status = IapVerifyStatus.KillSwitchOff; break;
                case 400:
                case 402:
                case 404: answer.Status = IapVerifyStatus.Refused; break;
                default:  answer.Status = IapVerifyStatus.Offline; break;   // 401/403/5xx/transport: retry later
            }
            return answer;
        }

        // Mirrors routers/iap.py::GolfinVerifyRequest — snake_case on purpose.
        private sealed class VerifyBody
        {
            public string platform = "apple";
            public string product_id = string.Empty;
            public string transaction_id = string.Empty;
            public string receipt_data = string.Empty;
            public string? jws;
            public string? entry_id;
            public int build;
            public bool sandbox;
        }
    }
}
