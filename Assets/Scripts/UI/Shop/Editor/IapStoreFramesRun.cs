// Assets/Scripts/UI/Shop/Editor/IapStoreFramesRun.cs
// iap_plumbing — shoots the STORE's dual-priced card, the payment-choice modal and the money
// round-trip through the REAL widgets (PLAY gate → Rewards Center → STORE → TICKETS → BUY → option),
// in play mode, at the Game View's resolution. Editor-only. Modelled on StoreFilterFramesRun.
//
// THE STORE IS UNITY IAP'S FAKESTORE. This run flips IapService.EditorForceFakeStorePref ON for
// its own play session and OFF again when it ends (feedback_restore_playable_state), so the ¥
// plates render with the FakeStore's own price string ("$0.01" — StoreKit's "¥160" only exists on a
// device) and the verify call is refused by the real server. Nothing is granted through it.
//
// Frames land in Docs/Diagnostics/_capture/iap_frames/, geometry in geometry.txt beside them.
#nullable enable
using System.Collections;
using System.IO;
using System.Reflection;
using Golfin.Diagnostics.Runtime;
using Golfin.Economy;
using GolfinRedux.UI;
using GolfinRedux.UI.Gacha;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class IapStoreFramesRun
    {
        private const string ArmedKey   = "GOLFIN.IapStoreFrames.Armed";
        private const string OutDir     = "Docs/Diagnostics/_capture/iap_frames";
        private const string ShellScene = "Assets/Scenes/ShellScene.unity";
        private const string DualCard   = "Card_shop_ticket_gold_10_iap";

        private const string RealConfigKey = "GOLFIN.IapStoreFrames.RealConfig";

        [MenuItem("GOLFIN/Store/Shoot IAP store + payment modal frames", priority = 266)]
        public static void Run() => Launch(realConfig: false, fullFlow: true);

        /// <summary>The same walk with the FakeStore force OFF: IapService reads the REAL server
        /// config (flag off / endpoint absent ⇒ IAP off), so the frames show the store with no ¥
        /// plate anywhere and the sandbox row withheld. Stops after the two store frames.</summary>
        [MenuItem("GOLFIN/Store/Shoot IAP store frames — real server config (IAP off)", priority = 267)]
        public static void RunRealConfig() => Launch(realConfig: true, fullFlow: false);

        /// <summary>Real server config (flag ON, products from the server) AND the whole walk —
        /// the live chain end to end minus Apple: the FakeStore's order is verified by the REAL
        /// server, which refuses its empty receipt with a 402 and audits it.</summary>
        [MenuItem("GOLFIN/Store/Shoot IAP store + modal frames — real server config, full flow", priority = 268)]
        public static void RunRealConfigFull() => Launch(realConfig: true, fullFlow: true);

        private const string FullFlowKey = "GOLFIN.IapStoreFrames.FullFlow";

        private static void Launch(bool realConfig, bool fullFlow)
        {
            Directory.CreateDirectory(OutDir);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != ShellScene)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ShellScene, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Application.runInBackground = true;
            EditorPrefs.SetBool(IapService.EditorForceFakeStorePref, !realConfig);   // restored OFF by the runner
            EditorPrefs.SetBool(RealConfigKey, realConfig);
            EditorPrefs.SetBool(FullFlowKey, fullFlow);
            EditorPrefs.SetBool(ArmedKey, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            if (!EditorPrefs.GetBool(ArmedKey, false)) return;
            EditorApplication.update += Pump;
        }

        private static bool _spawned;
        private static void Pump()
        {
            if (!Application.isPlaying) return;
            if (_spawned) { EditorApplication.update -= Pump; return; }
            _spawned = true;
            EditorPrefs.SetBool(ArmedKey, false);
            EditorApplication.update -= Pump;
            var host = new GameObject("[IapStoreFrames]");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>();
        }

        private sealed class Runner : MonoBehaviour
        {
            private readonly System.Text.StringBuilder _geo = new System.Text.StringBuilder();
            private void Start() => StartCoroutine(Sequence());
            private static ScreenId? Now => ScreenManager.Instance?.CurrentScreen;

            private IEnumerator Sequence()
            {
                try
                {
                    Application.runInBackground = true;
                    float t = 0f;
                    while (t < 25f)
                    {
                        var splash = FindFirstObjectByType<SplashScreenController>();
                        var btn = splash == null ? null : splash.transform.Find("StartButton");
                        if (btn != null && btn.gameObject.activeInHierarchy) { btn.GetComponent<Button>()?.onClick.Invoke(); break; }
                        t += Time.unscaledDeltaTime; yield return null;
                    }
                    yield return WaitFor(ScreenId.Home, 60f);
                    yield return Settle(1.5f);

                    bool realConfig = EditorPrefs.GetBool(RealConfigKey, false);
                    bool fullFlow   = EditorPrefs.GetBool(FullFlowKey, true);

                    // The store must be READY before the STORE tab paints, or the sandbox row is withheld.
                    // (Real-config run: wait for the config answer instead — any state but Off/Connecting.)
                    t = 0f;
                    while (t < 30f && (realConfig ? (IapService.State == IapState.Off || IapService.State == IapState.Connecting)
                                                  : IapService.State != IapState.Ready))
                    { t += Time.unscaledDeltaTime; yield return null; }
                    _geo.AppendLine($"IapService.State={IapService.State} after {t:F1}s; " +
                                    $"test.tickets.x10 available={IapService.IsProductAvailable("test.tickets.x10")} " +
                                    $"price='{(IapService.TryGetLocalizedPrice("test.tickets.x10", out var p) ? p : "")}'");

                    var pum = FindFirstObjectByType<Golfin.UI.PersistentUIManager>(FindObjectsInactive.Include);
                    pum?.shopPlusButton?.onClick.Invoke();
                    yield return WaitFor(ScreenId.GeneralShop, 20f);
                    yield return Settle(1.5f);
                    yield return DismissHints();
                    yield return Settle(2f);

                    var shop = FindFirstObjectByType<GeneralShopScreenController>(FindObjectsInactive.Include);
                    string prefix = realConfig ? (fullFlow ? "20_live_" : "10_iap_off_") : "0";
                    Save(realConfig ? (fullFlow ? "20_live_store_all" : "10_iap_off_store_all") : "01_store_all");
                    DumpPricePlates(prefix + "store_all");

                    Chip(shop, "TICKETSChip");
                    yield return Settle(2.5f);
                    Save(realConfig ? (fullFlow ? "21_live_store_tickets" : "11_iap_off_store_tickets") : "02_store_tickets");
                    DumpPricePlates(prefix + "store_tickets");

                    if (realConfig && !fullFlow)
                    {
                        _geo.AppendLine($"real-config run: State={IapService.State}; sandbox row on screen={FindCard(DualCard) != null}; " +
                                        $"cards showing a money plate={CountMoneyPlates()}");
                        yield break;
                    }

                    // ── BUY on the dual card → the payment-choice modal (real onClick) ──
                    var card = FindCard(DualCard);
                    if (card == null) { _geo.AppendLine("NO dual card on screen — aborting the modal steps"); yield break; }
                    card.BuyButton.onClick.Invoke();
                    yield return Settle(1.5f);
                    var modal = FindFirstObjectByType<StorePaymentModalController>(FindObjectsInactive.Include);
                    _geo.AppendLine($"modal open={(modal != null && modal.IsOpen)}");
                    Save(realConfig ? "23_live_payment_modal" : "03_payment_modal");
                    DumpModal(modal);

                    // CANCEL — the real button, by its serialized field.
                    Field<Button>(modal, "cancelButton")?.onClick.Invoke();
                    yield return Settle(1.0f);
                    _geo.AppendLine($"after CANCEL: modal open={(modal != null && modal.IsOpen)}");
                    Save(realConfig ? "24_live_modal_cancelled" : "04_modal_cancelled");

                    // ── BUY → ¥ option → FakeStore sheet, PendingSpend on the card the whole way ──
                    card = FindCard(DualCard);
                    card?.BuyButton.onClick.Invoke();
                    yield return Settle(1.0f);
                    Field<Button>(modal, "moneyButton")?.onClick.Invoke();
                    yield return Settle(1.5f);
                    _geo.AppendLine($"money option tapped: PurchaseInFlight={IapService.PurchaseInFlight} " +
                                    $"buyLabel='{card?.BuyLabel?.text}' buyInteractable={card?.BuyButton?.interactable}");
                    Save(realConfig ? "25_live_money_pending_fakestore_sheet" : "05_money_pending_fakestore_sheet");

                    // Approve the FakeStore's own dialog (stands in for the StoreKit sheet).
                    yield return ApproveFakeStore();
                    t = 0f;
                    while (t < 30f && IapService.PurchaseInFlight) { t += Time.unscaledDeltaTime; yield return null; }
                    _geo.AppendLine($"round trip ended after {t:F1}s: PurchaseInFlight={IapService.PurchaseInFlight} " +
                                    $"buyLabel='{card?.BuyLabel?.text}' buyInteractable={card?.BuyButton?.interactable}");
                    yield return Settle(0.6f);
                    Save(realConfig ? "26_live_after_server_answer" : "06_after_server_answer");

                    // ── the ¥-only plate, on a SYNTHETIC entry (no ¥-only row exists in this task's data) ──
                    if (card != null && !realConfig)
                    {
                        var e = new ShopCatalogEntry
                        {
                            EntryId = "harness:money_only", Category = ShopCategory.Ticket, RefId = "1",
                            RpCost = 0, Quantity = 10, StoreProductId = "test.tickets.x10",
                        };
                        card.Bind(e);
                        yield return Settle(0.8f);
                        Save("99_HARNESS_money_only_plate_synthetic_entry");
                        DumpPricePlates("99_HARNESS_money_only");
                        card.Bind(GeneralShopCatalog.GetByCategory(ShopCategory.Ticket).Find(x => x.EntryId == "shop_ticket_gold_10_iap")!);
                    }
                }
                finally
                {
                    bool rc = EditorPrefs.GetBool(RealConfigKey, false), ff = EditorPrefs.GetBool(FullFlowKey, true);
                    File.WriteAllText(Path.Combine(OutDir, rc ? (ff ? "geometry_live.txt" : "geometry_iap_off.txt") : "geometry.txt"), _geo.ToString());
                    EditorPrefs.SetBool(RealConfigKey, false);
                    EditorPrefs.SetBool(FullFlowKey, true);
                    EditorPrefs.SetBool(IapService.EditorForceFakeStorePref, false);   // restore playable state
                    File.WriteAllText("/tmp/iap_store_frames.txt", "DONE\n");
                    EditorApplication.isPlaying = false;
                }
            }

            // ── instruments ────────────────────────────────────────────────

            private void DumpPricePlates(string label)
            {
                foreach (var card in FindObjectsByType<GeneralShopCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (!card.gameObject.activeInHierarchy) continue;
                    var box = card.transform.Find("PriceBox") as RectTransform;
                    var border = card.transform.Find("PriceBorder") as RectTransform;
                    var cta = card.transform.Find("CtaGoldButton") as RectTransform;
                    var badge = card.transform.Find("PriceBox/DiscountBadge");
                    if (box == null) continue;
                    _geo.AppendLine($"[{label}] {card.name}: money={card.ShowsMoneyPrice} rp={card.ShowsRpPrice} " +
                                    $"box={Rect(box)} border={(border != null ? Rect(border) : "-")} cta={(cta != null ? Rect(cta) : "-")} " +
                                    $"badge={(badge != null && badge.gameObject.activeInHierarchy ? Rect((RectTransform)badge) + " '" + badge.Find("Label")?.GetComponent<TextMeshProUGUI>()?.text + "'" : "off")}");
                    foreach (var row in new[] { "PriceBox/Orig", "PriceBox/SaleBG/Sale" })
                    {
                        var r = card.transform.Find(row);
                        if (r == null || !r.gameObject.activeInHierarchy) continue;
                        var icon = r.Find("RpIcon") as RectTransform;
                        var num = r.Find("Num")?.GetComponent<TextMeshProUGUI>();
                        if (num == null) continue;
                        var numRt = (RectTransform)num.transform;
                        var tb = num.textBounds;
                        Vector3 tMin = numRt.TransformPoint(tb.min), tMax = numRt.TransformPoint(tb.max);
                        string iconS = icon != null && icon.gameObject.activeInHierarchy ? $"icon={Rect(icon)} size={icon.sizeDelta.x}" : "icon=off";
                        _geo.AppendLine($"      {row}: '{num.text}' fs={num.fontSize} color=#{ColorUtility.ToHtmlStringRGB(num.color)} " +
                                        $"glyphs x[{tMin.x:F1}..{tMax.x:F1}] y[{tMin.y:F1}..{tMax.y:F1}] {iconS} gap={(icon != null ? (numRt.anchoredPosition.x - (icon.anchoredPosition.x + icon.sizeDelta.x)).ToString("F1") : "-")}");
                    }
                }
            }

            private void DumpModal(StorePaymentModalController? modal)
            {
                if (modal == null) return;
                foreach (var name in new[] { "modalPanel", "backdrop" })
                {
                    var go = Field<GameObject>(modal, name);
                    if (go != null) _geo.AppendLine($"modal.{name}: active={go.activeInHierarchy} rect={Rect((RectTransform)go.transform)}");
                }
                foreach (var name in new[] { "titleText", "descriptionText", "chooseText", "rpAmountText", "moneyPriceText", "cancelText" })
                {
                    var tmp = Field<TextMeshProUGUI>(modal, name);
                    if (tmp == null) continue;
                    var rt = (RectTransform)tmp.transform;
                    var tb = tmp.textBounds;
                    Vector3 mn = rt.TransformPoint(tb.min), mx = rt.TransformPoint(tb.max);
                    _geo.AppendLine($"modal.{name}: '{tmp.text}' fs={tmp.fontSize} font={tmp.font?.name} color=#{ColorUtility.ToHtmlStringRGB(tmp.color)} " +
                                    $"gradient={tmp.enableVertexGradient} rect={Rect(rt)} glyphs x[{mn.x:F1}..{mx.x:F1}] y[{mn.y:F1}..{mx.y:F1}] h={mx.y - mn.y:F1}");
                }
                foreach (var name in new[] { "rpButton", "moneyButton", "cancelButton" })
                {
                    var b = Field<Button>(modal, name);
                    if (b == null) continue;
                    var img = b.GetComponent<Image>();
                    _geo.AppendLine($"modal.{name}: active={b.gameObject.activeInHierarchy} rect={Rect((RectTransform)b.transform)} " +
                                    $"sprite={(img != null && img.sprite != null ? img.sprite.name : "<NONE>")} pressFeedback={b.GetComponent<Golfin.UI.Polish.ButtonPressFeedback>() != null}");
                }
                var art = Field<Image>(modal, "itemArt");
                if (art != null) _geo.AppendLine($"modal.itemArt: sprite={(art.sprite != null ? art.sprite.name : "<NONE>")} rect={Rect((RectTransform)art.transform)}");
            }

            private static string Rect(RectTransform rt)
            {
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                // Canvas space (1170×2532, y up): report as x[min..max] y[min..max] + size.
                return $"x[{c[0].x:F1}..{c[2].x:F1}] y[{c[0].y:F1}..{c[2].y:F1}] w={c[2].x - c[0].x:F1} h={c[2].y - c[0].y:F1}";
            }

            private static T? Field<T>(object? target, string name) where T : class
            {
                if (target == null) return null;
                var f = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                return f?.GetValue(target) as T;
            }

            private static int CountMoneyPlates()
            {
                int n = 0;
                foreach (var c in FindObjectsByType<GeneralShopCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (c.gameObject.activeInHierarchy && c.ShowsMoneyPrice) n++;
                return n;
            }

            private static GeneralShopCard? FindCard(string name)
            {
                foreach (var c in FindObjectsByType<GeneralShopCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (c.name == name) return c;
                return null;
            }

            private static void Chip(GeneralShopScreenController? shop, string chip)
                => shop?.transform.Find("ContentArea/BarsArea/FilterGroup/CategoryRow/" + chip)?.GetComponent<Button>()?.onClick.Invoke();

            /// <summary>Unity IAP's FakeStore dialog is IMGUI; its OK is a private method on
            /// <c>UIFakeStoreWindow</c>. Stands in for the StoreKit sheet's Buy.</summary>
            private IEnumerator ApproveFakeStore()
            {
                for (int i = 0; i < 40; i++)
                {
                    var go = GameObject.Find("UIFakeStoreWindow");
                    var comp = go != null ? go.GetComponent("UIFakeStoreWindow") : null;
                    if (comp != null)
                    {
                        yield return Settle(0.5f);
                        var m = comp.GetType().GetMethod("OnOkClicked", BindingFlags.NonPublic | BindingFlags.Instance);
                        m?.Invoke(comp, null);
                        _geo.AppendLine("FakeStore dialog approved (OnOkClicked)");
                        yield break;
                    }
                    yield return Settle(0.25f);
                }
                _geo.AppendLine("FakeStore dialog never appeared");
            }

            private void Save(string label)
            {
                string dst = Path.Combine(OutDir, label + ".png");
                string src = CaptureCore.SnapPlayModeSafe(label);
                if (!string.IsNullOrEmpty(src) && File.Exists(src)) { File.Copy(src, dst, true); return; }
                var tex = CaptureCore.GrabGameViewRT();
                if (tex == null) { Debug.LogError("[IapStoreFrames] no frame for " + label); return; }
                File.WriteAllBytes(dst, tex.EncodeToPNG());
                Object.Destroy(tex);
            }

            private static IEnumerator DismissHints()
            {
                for (int i = 0; i < 8; i++)
                {
                    var modal = Golfin.UI.Modals.ScreenHintModalController.Instance;
                    if (modal == null || !modal.gameObject.activeInHierarchy) yield break;
                    var f = typeof(Golfin.UI.Modals.ScreenHintModalController).GetField("nextButton",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    var btn = f?.GetValue(modal) as Button;
                    if (btn == null || !btn.gameObject.activeInHierarchy) yield break;
                    btn.onClick.Invoke();
                    yield return Settle(0.8f);
                }
            }

            private static IEnumerator WaitFor(ScreenId id, float s) { float t = 0f; while (t < s && Now != id) { t += Time.unscaledDeltaTime; yield return null; } }
            private static IEnumerator Settle(float s) { float t = 0f; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
        }
    }
}
