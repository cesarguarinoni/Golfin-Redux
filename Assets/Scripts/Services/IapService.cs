// Assets/Scripts/Services/IapService.cs
// iap_plumbing (2026-09-15) — the GOLFIN real-money purchase pipeline, client HOST.
//
// The decisions (confirm only after the server granted, fail-closed kill switch, one purchase
// in flight) live in Golfin.Economy.IapFlow, where the EditMode suite can drive them with fakes.
// This file is what only Assembly-CSharp can do: talk to Unity IAP 5 (UnityIapStoreDriver, below)
// and re-read the scene singletons after a grant (GachaTicketManager, StoreHistoryStore).
//
// NAMESPACE NOTE: the spec pins Assets/Scripts/Services/IapService.cs; there are no neighbouring
// services in that folder, so the type joins `Golfin.Economy` beside ShopPurchaseService /
// GachaPullService / PointsService — the services it is a sibling of in purpose. Flagged in the
// report.
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Content;
using Golfin.Net;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Golfin.Economy
{
    /// <summary>
    /// The scene-less host: created at boot, reads the server config, owns the shipping
    /// <see cref="IapFlow"/>. Static conveniences below are what the shop UI calls, and every one
    /// of them answers "no" when the service does not exist (EditMode, a bot session, flag off).
    /// </summary>
    public sealed class IapService : MonoBehaviour
    {
        private const string Tag = "[IapService]";

        public static IapService? Instance { get; private set; }

        /// <summary>Static, like <c>AuthService.SignedIn</c>: the store screen subscribes on its
        /// own schedule and must not race the host's creation.</summary>
        public static event Action? AvailabilityChanged;

        public IapFlow? Flow { get; private set; }

        public static IapState State => Instance?.Flow?.State ?? IapState.Off;
        public static bool IsReady => State == IapState.Ready;
        public static bool IsProductAvailable(string? productId) => Instance?.Flow?.IsProductAvailable(productId) ?? false;

        public static bool TryGetLocalizedPrice(string? productId, out string price)
        {
            price = string.Empty;
            return Instance?.Flow?.TryGetLocalizedPrice(productId, out price) ?? false;
        }

        public static bool PurchaseInFlight => Instance?.Flow?.PurchaseInFlight ?? false;

        public static void Purchase(string productId, string? entryId, Action<IapPurchaseOutcome> onResult)
        {
            if (Instance?.Flow == null) { onResult(IapPurchaseOutcome.Unavailable); return; }
            Instance.Flow.Purchase(productId, entryId, onResult);
        }

        /// <summary>Test seam: install a flow built on fakes; the boot path is skipped.</summary>
        public static void ConfigureForTest(IapFlow? flow)
        {
            if (Instance == null)
            {
                var go = new GameObject("[IapService(test)]");
                go.hideFlags = HideFlags.HideAndDontSave;
                Instance = go.AddComponent<IapService>();
            }
            Instance.Flow = flow;
            Instance._bootDone = true;
        }

        public static void ResetForTest()
        {
            if (Instance != null)
            {
                var go = Instance.gameObject;
                Instance = null;
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
        }

        private bool _bootDone;

#if UNITY_EDITOR
        /// <summary>
        /// EDITOR ONLY, OFF BY DEFAULT. <c>GOLFIN ▸ Store ▸ IAP: force FakeStore (Editor)</c> toggles this
        /// EditorPref. When on, <see cref="Boot"/> skips the server config and connects Unity IAP's
        /// FakeStore for every catalog <c>storeProductId</c>, so the ¥ plates and the payment modal
        /// can be rendered through real navigation in the Editor before the server arm is live. The
        /// verify call still goes to the real server (and is refused — the FakeStore's receipt is
        /// "ThisIsFakeReceiptData"), so nothing is ever granted through it. A player build has no
        /// such path: the symbol does not exist outside the Editor.
        /// </summary>
        public const string EditorForceFakeStorePref = "GOLFIN.Iap.ForceFakeStore";
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[IapService]");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<IapService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            if (_bootDone) return;
            _bootDone = true;
            Boot();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Boot()
        {
            if (!PointsBackendFlag.Enabled)
            {
                // Bot sessions and the offline dev path have no server to grant against, so there
                // is nothing a StoreKit purchase could ever be verified by. Off, and said so.
                Debug.Log($"{Tag} points backend is OFF this run — IAP stays off (no server to verify against).");
                return;
            }

            Flow = new IapFlow(new UnityIapStoreDriver(), new ApiIapVerifier(ApiClient.Instance, () => ContentBuildNumber.Current),
                               msg => Debug.Log($"{Tag} {msg}"));
            Flow.AvailabilityChanged += () => AvailabilityChanged?.Invoke();
            Flow.Granted += OnGranted;

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool(EditorForceFakeStorePref, false))
            {
                var ids = CatalogProductIds();
                Debug.LogWarning($"{Tag} EDITOR FORCE — FakeStore for {ids.Count} catalog product(s); the server " +
                                 "config was NOT consulted. Prices are the FakeStore's; verify is refused server-side.");
                Flow.ApplyConfig(true, ids, ids);
                return;
            }
#endif

            StartCoroutine(FetchConfigRoutine());
        }

        private IEnumerator FetchConfigRoutine()
        {
            ApiResult<IapConfigDto>? result = null;
            IEnumerator call = ApiClient.Instance.Get<IapConfigDto>(Endpoints.IapGolfinConfig, r => result = r);
            while (call.MoveNext()) yield return call.Current;

            if (Flow == null) yield break;

            if (result == null || !result.Success || result.Data == null)
            {
                // No answer is NOT "enabled". The switch is read fail-closed on both ends.
                Debug.LogWarning($"{Tag} could not read /iap/golfin/config ({(result != null ? result.ToString() : "no result")}) — IAP stays off this session.");
                Flow.ApplyConfig(false, Array.Empty<string>(), Array.Empty<string>());
                yield break;
            }

            var serverIds = new List<string>();
            if (result.Data.Products != null)
                foreach (var p in result.Data.Products)
                    if (p != null && !string.IsNullOrEmpty(p.ProductId)) serverIds.Add(p.ProductId);

            Flow.ApplyConfig(result.Data.Enabled, serverIds, CatalogProductIds());
        }

        /// <summary>Every <c>storeProductId</c> the bundled+overlay shop catalog names.</summary>
        private static IReadOnlyList<string> CatalogProductIds()
        {
            var ids = new List<string>();
            try
            {
                foreach (var e in GolfinRedux.UI.Shop.GeneralShopCatalog.Entries)
                    if (e.HasStoreProduct && !ids.Contains(e.StoreProductId)) ids.Add(e.StoreProductId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} could not read the shop catalog for product ids: {ex.Message}");
            }
            return ids;
        }

        private void OnGranted()
        {
            // The ledger moved server-side; the client only re-reads. Same posture as the RP ticket
            // sale (ShopTransaction.ApplyPurchaseGrant's KindTicket case).
            var tickets = GolfinRedux.UI.Gacha.GachaTicketManager.Instance;
            if (tickets != null) tickets.RefreshFromServer();
            GolfinRedux.UI.Shop.StoreHistoryStore.Refresh();
        }
    }

    /// <summary>
    /// Unity IAP 5.4.3, verbatim from the package's own <c>AiAssistantSkills/in-app-purchases</c>
    /// notes: subscribe to every success AND failure event before <c>Connect()</c>, two-step
    /// pending → confirm, <c>order.Info.Apple?.AppReceipt / jwsRepresentation</c> for server-side
    /// validation. No <c>UnityServices.InitializeAsync</c> is required by IAP 5.
    /// </summary>
    public sealed class UnityIapStoreDriver : IIapStoreDriver
    {
        public event Action? Connected;
        public event Action<string>? Disconnected;
        public event Action<IReadOnlyList<IapProductInfo>>? ProductsFetched;
        public event Action<string>? ProductsFetchFailed;
        public event Action<IapPendingOrder>? PurchasePending;
        public event Action<IapFailedOrder>? PurchaseFailed;
        public event Action<string>? PurchaseDeferred;

        private StoreController? _store;
        private List<ProductDefinition> _definitions = new List<ProductDefinition>();

        public async void Connect(IReadOnlyList<string> consumableProductIds)
        {
            _definitions = new List<ProductDefinition>(consumableProductIds.Count);
            foreach (var id in consumableProductIds)
                if (!string.IsNullOrEmpty(id))
                    _definitions.Add(new ProductDefinition(id, ProductType.Consumable));

            _store = UnityIAPServices.StoreController();

            // Every pair, BEFORE Connect — a purchase left pending by the previous session is
            // re-delivered the moment the store connects.
            _store.OnStoreConnected        += OnStoreConnected;
            _store.OnStoreDisconnected     += d => Disconnected?.Invoke(d.Message ?? "disconnected");
            _store.OnProductsFetched       += OnProductsFetched;
            _store.OnProductsFetchFailed   += f => ProductsFetchFailed?.Invoke(f.FailureReason ?? "products fetch failed");
            _store.OnPurchasesFetched      += _ => { };   // pending orders arrive through OnPurchasePending
            _store.OnPurchasesFetchFailed  += f => Debug.LogWarning($"[IapService] purchases fetch failed: {f.Message}");
            _store.OnPurchasePending       += OnPurchasePending;
            _store.OnPurchaseConfirmed     += OnPurchaseConfirmed;
            _store.OnPurchaseFailed        += OnPurchaseFailed;
            _store.OnPurchaseDeferred      += o => PurchaseDeferred?.Invoke(FirstProductId(o));

            try
            {
                await _store.Connect();
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(ex.Message);
            }
        }

        private void OnStoreConnected()
        {
            Connected?.Invoke();
            if (_store == null) return;
            _store.FetchProducts(_definitions);
            _store.FetchPurchases();
        }

        private void OnProductsFetched(List<Product> products)
        {
            var list = new List<IapProductInfo>(products.Count);
            foreach (var p in products)
            {
                if (p?.definition == null) continue;
                list.Add(new IapProductInfo
                {
                    ProductId      = p.definition.id,
                    LocalizedPrice = p.metadata?.localizedPriceString ?? string.Empty,
                    CurrencyCode   = p.metadata?.isoCurrencyCode ?? string.Empty,
                    Available      = p.availableToPurchase,
                });
            }
            ProductsFetched?.Invoke(list);
        }

        private void OnPurchasePending(PendingOrder order)
        {
            var apple = order.Info?.Apple;
            PurchasePending?.Invoke(new IapPendingOrder
            {
                ProductId     = FirstProductId(order),
                TransactionId = order.Info?.TransactionID ?? string.Empty,
                AppReceipt    = apple?.AppReceipt,
                Jws           = apple?.jwsRepresentation,
                Handle        = order,
            });
        }

        private static void OnPurchaseConfirmed(Order order)
        {
            if (order is FailedOrder failed)
                Debug.LogError($"[IapService] confirm failed: {failed.FailureReason} — {failed.Details}");
            else
                Debug.Log($"[IapService] transaction confirmed: {order.Info?.TransactionID}");
        }

        private void OnPurchaseFailed(FailedOrder failed)
        {
            PurchaseFailed?.Invoke(new IapFailedOrder
            {
                ProductId     = FirstProductId(failed),
                UserCancelled = failed.FailureReason == PurchaseFailureReason.UserCancelled,
                Reason        = failed.FailureReason.ToString(),
                Details       = failed.Details ?? string.Empty,
            });
        }

        public void Purchase(string productId)
        {
            if (_store == null) { PurchaseFailed?.Invoke(new IapFailedOrder { ProductId = productId, Reason = "StoreNotConnected" }); return; }
            _store.PurchaseProduct(productId);
        }

        public void Confirm(IapPendingOrder order)
        {
            if (_store != null && order.Handle is PendingOrder pending) _store.ConfirmPurchase(pending);
        }

        private static string FirstProductId(Order order)
        {
            var items = order?.CartOrdered?.Items();
            return items != null && items.Count > 0 ? items[0].Product?.definition?.id ?? string.Empty : string.Empty;
        }
    }
}
