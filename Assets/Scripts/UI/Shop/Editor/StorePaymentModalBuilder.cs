// Assets/Scripts/UI/Shop/Editor/StorePaymentModalBuilder.cs
// iap_plumbing — builds Resources/Prefabs/Shop/StorePaymentModal.prefab from the palette atoms, at
// the node's own geometry (Figma 5gEAHjl6xAtW8iYY7NMvWd › 14289:33223 "Store — Purchase Modal
// (dual price)" › 14289:33227 "Pop-up", 1086×912, centred on the 1170×2532 frame).
//
// SAME SHELL AS LoanUiBuilder.BuildReturnPrefab (Pop-up panel + Main Buttons gold/silver +
// Rubik SemiBold), positioned ABSOLUTELY rather than through a VerticalLayoutGroup: every element
// of this node has a fixed box, and absolute placement is the one layout that cannot be argued
// with by a layout group (PIPELINE_HARDENING C3/C4/C6).
//
// Node geometry (px, inside the 1086×912 drawn panel, y down):
//   Title         (54,   0) 978×120   text "GOLDEN TICKET ×10", 66 SemiBold, silver gradient, centred
//   Separator     (54, 120) 978×2
//   ItemRow       (138,144) 810×173   art 158×173 at x0 · description 620 wide at x190, centred, 51 SemiBold white
//   Separator     (54, 341) 978×2
//   Choose line   (48, 365) 990×83    "CHOOSE HOW TO PAY", 51 SemiBold white, centred
//   RP option     (318,472) 450×120   gold Main Button; coin 56 + "450" 66 SemiBold #321506, gap 12
//   ¥ option      (318,616) 450×120   gold Main Button; "¥1,500" 66 SemiBold #321506
//   CANCEL        (318,760) 450×120   silver Main Button; "CANCEL" 66 SemiBold #1E293B
//
// Font sizes follow the SchemeConfirmModal precedent on the same 1086-wide shell (node 66 → 59);
// the § Figma fidelity table A/Bs the rendered cap heights against reference/purchase_modal.png.
#nullable enable
using System.Text;
using Golfin.UI.Polish;
using Golfin.Utilities;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Shop.EditorTools
{
    public static class StorePaymentModalBuilder
    {
        public const string PrefabPath = "Assets/Resources/Prefabs/Shop/StorePaymentModal.prefab";

        // ── Reuse sources (Rule 19 — every element cites the atom it was cloned from) ──
        /// <summary>The Figma "Pop-up" panel — UI_ELEMENT_PALETTE § Panels, guid 3663aafeba2bd1f42a04eabf9d34c220.</summary>
        private const string SpritePopupPanel    = "Assets/Art/HomeScreen/Next Hole Panel.png";
        /// <summary>GOLD big, 9-sliced border 25 — guid bc649f28836576548b310e79ce614a06 (LEVEL UP CONFIRM, LEND, RETURN).</summary>
        private const string SpriteButtonGoldBig   = "Assets/Art/RosterScreen/ButtonConfirm.png";
        /// <summary>SILVER big, 9-sliced border 25 — guid 6021c639e9c124b44a06c8ccd977896f (every CANCEL).</summary>
        private const string SpriteButtonSilverBig = "Assets/Art/RosterScreen/ButtonCancel.png";
        /// <summary>The RP coin — guid aab2dfa34afd9cf4abfe974a164268dc (RP chips everywhere; the card's PriceBox rows).</summary>
        private const string SpriteRpCoin          = "Assets/Art/HomeScreen/Reward Points Icon.png";
        /// <summary>Thin horizontal divider — guid 36b5ccd887d78864b9d3f0b36a18f339 (Home, cards).</summary>
        private const string SpriteDivider         = "Assets/Art/HomeScreen/Divider.png";
        private const string FontSemiBold          = "Assets/Fonts/Rubik-SemiBold SDF.asset";

        // ── Node tokens ──────────────────────────────────────────────────────
        private const float PanelW = 1086f, PanelH = 912f, PanelRadius = 50f;
        /// <summary>`Next Hole Panel.png` is 9-sliced at border 64; ppum = 64 / target radius.</summary>
        private const float PanelPpum = 64f / PanelRadius;
        /// <summary>The sprite draws INSIDE its rect: baked shadow margins L20 R20 T10 B30 sprite-px,
        /// scaled by effectiveBorder/64 = 50/64 (UI_ELEMENT_PALETTE § Panels, the drawn-body note).</summary>
        private const float PanelInsetL = 20f * PanelRadius / 64f, PanelInsetR = 20f * PanelRadius / 64f;
        private const float PanelInsetT = 10f * PanelRadius / 64f, PanelInsetB = 30f * PanelRadius / 64f;

        private static readonly Color Scrim       = new Color(0f, 0f, 0f, 0.50f);
        private static readonly Color GoldLabel   = Hex("321506");
        private static readonly Color SilverLabel = Hex("1E293B");
        private static readonly Color Divider     = new Color(1f, 1f, 1f, 0.35f);

        // Node → TMP sizes, settled by A/B of rendered CAP HEIGHTS against reference/purchase_modal.png
        // (iap_frames/03_payment_modal.png, 2026-09-15): title 47 px vs node 47 at 59; choose line 34 vs
        // 35 at 46; CANCEL 45 vs 46 at 59; the RP digits read 45 vs 48 at 59, hence 63 for them.
        private const float TitleSize  = 59f;    // node 66 → cap 47 = node 47
        private const float BodySize   = 46f;    // node 51 → cap 34 ≈ node 35
        private const float LabelSize  = 59f;    // node 66 → CANCEL cap 45 ≈ node 46
        private const float AmountSize = 63f;    // node 66 → the RP digits, 45 → 48 = node 48

        private static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("GOLFIN/Store/Build Store Payment Modal prefab", priority = 264)]
        public static void Build()
        {
            Log.Clear();
            Log.Append("== StorePaymentModal.prefab ==\n");

            var root = new GameObject("StorePaymentModal", typeof(RectTransform));
            SetRect(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            // Backdrop — ModalScrim.Apply re-parents/guarantees one at Show(); authored INACTIVE
            // (reference_modal_children_author_inactive).
            GameObject backdrop = Child(root.transform, "Backdrop");
            SetRect(backdrop, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            AddImage(backdrop, null, Scrim);
            backdrop.SetActive(false);

            // ModalPanel — the sprite's rect is the drawn 1086×912 box plus the baked shadow margins,
            // shifted so the DRAWN body is what sits centred on screen.
            GameObject panel = Child(root.transform, "ModalPanel");
            // The bottom margin is the larger one, so the drawn body sits HIGH inside the rect; the rect
            // moves DOWN by half the difference to put the drawn body on the screen's centre
            // (measured: +7.8 put it 15.6 px high, iap_frames geometry.txt).
            SetRect(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2((PanelInsetL - PanelInsetR) * 0.5f, (PanelInsetT - PanelInsetB) * 0.5f),
                    new Vector2(PanelW + PanelInsetL + PanelInsetR, PanelH + PanelInsetT + PanelInsetB));
            Image panelImg = AddImage(panel, Require<Sprite>(SpritePopupPanel), Color.white, Image.Type.Sliced);
            panelImg.pixelsPerUnitMultiplier = PanelPpum;

            // Body — exactly the drawn 1086×912 box; every node coordinate is relative to it.
            GameObject body = Child(panel.transform, "Body");
            SetRect(body, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(PanelInsetL, -PanelInsetT), new Vector2(PanelW, PanelH));

            // Title (14289:33231) — silver gradient, the design's EN/Title_2 style.
            TextMeshProUGUI title = Text(body, "Title", 54f + 16f, 24f, 946f, 84f, TitleSize, Color.white,
                                         TextAlignmentOptions.Center);
            title.text = "GOLDEN TICKET ×10";
            TextGradients.ApplySilver(title);

            Separator(body, "Separator1", 120f);

            // ItemRow (14293:33358): art + description.
            GameObject art = Child(body.transform, "ItemArt");
            SetRect(art, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(138f, -144f), new Vector2(158f, 173f));
            Image artImg = AddImage(art, null, Color.white, Image.Type.Simple, raycast: false);
            artImg.preserveAspect = true;

            TextMeshProUGUI desc = Text(body, "Description", 138f + 190f, 144f, 620f, 173f, BodySize, Color.white,
                                        TextAlignmentOptions.Center);
            desc.text = "GET 10 GOLDEN TICKETS FOR GACHA PULLS.";
            // The row is bound from the card's description, which is longer than the node's placeholder
            // copy; auto-size DOWN (never up) so three lines stay inside the 620×173 row.
            desc.enableAutoSizing = true;
            desc.fontSizeMax = BodySize;
            desc.fontSizeMin = 30f;

            Separator(body, "Separator2", 341f);

            // Choose line (14293:33345)
            TextMeshProUGUI choose = Text(body, "ChooseText", 48f, 365f, 990f, 83f, BodySize, Color.white,
                                          TextAlignmentOptions.Center);
            choose.text = "CHOOSE HOW TO PAY.";

            // RP option (14293:33346 + RpPriceRow 14297:33344): coin 56 + amount, gap 12, centred.
            GameObject rpGo = OptionButton(body, "RpButton", 472f, SpriteButtonGoldBig);
            GameObject rpRow = Child(rpGo.transform, "PriceRow");
            SetRect(rpRow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(198f, 78f));
            var rpLayout = rpRow.AddComponent<HorizontalLayoutGroup>();
            rpLayout.spacing = 12f;
            rpLayout.childAlignment = TextAnchor.MiddleCenter;
            rpLayout.childControlWidth = false;  rpLayout.childControlHeight = false;
            rpLayout.childForceExpandWidth = false; rpLayout.childForceExpandHeight = false;
            var rpFit = rpRow.AddComponent<ContentSizeFitter>();
            rpFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject coin = Child(rpRow.transform, "RpIcon");
            SetRect(coin, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(56f, 56f));
            AddImage(coin, Require<Sprite>(SpriteRpCoin), Color.white, Image.Type.Simple, raycast: false);
            var coinLe = coin.AddComponent<LayoutElement>(); coinLe.preferredWidth = 56f; coinLe.preferredHeight = 56f;

            GameObject amountGo = Child(rpRow.transform, "Amount");
            SetRect(amountGo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    Vector2.zero, new Vector2(130f, 78f));
            TextMeshProUGUI amount = AddText(amountGo, AmountSize, GoldLabel, TextAlignmentOptions.Left);
            amount.text = "450";
            amount.enableWordWrapping = false;
            var amountLe = amountGo.AddComponent<LayoutElement>(); amountLe.preferredHeight = 78f;
            var amountFit = amountGo.AddComponent<ContentSizeFitter>();
            amountFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ¥ option (14293:33351)
            GameObject moneyGo = OptionButton(body, "MoneyButton", 616f, SpriteButtonGoldBig);
            TextMeshProUGUI moneyLabel = ButtonLabel(moneyGo, "¥1,500", GoldLabel);

            // CANCEL (14289:33814)
            GameObject cancelGo = OptionButton(body, "CancelButton", 760f, SpriteButtonSilverBig);
            TextMeshProUGUI cancelLabel = ButtonLabel(cancelGo, "CANCEL", SilverLabel);

            panel.SetActive(false);   // ModalController.Awake forces it; authored inactive regardless

            var controller = root.AddComponent<StorePaymentModalController>();
            Wire(controller, "modalPanel", panel);
            Wire(controller, "backdrop", backdrop);
            Wire(controller, "titleText", title);
            Wire(controller, "itemArt", artImg);
            Wire(controller, "descriptionText", desc);
            Wire(controller, "chooseText", choose);
            Wire(controller, "rpButton", rpGo.GetComponent<Button>());
            Wire(controller, "rpAmountText", amount);
            Wire(controller, "moneyButton", moneyGo.GetComponent<Button>());
            Wire(controller, "moneyPriceText", moneyLabel);
            Wire(controller, "cancelButton", cancelGo.GetComponent<Button>());
            Wire(controller, "cancelText", cancelLabel);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Log.Append("  saved ").Append(PrefabPath).Append('\n');
            Debug.Log(Log.ToString());
        }

        // ── pieces ──────────────────────────────────────────────────────────

        private static GameObject OptionButton(GameObject body, string name, float top, string spritePath)
        {
            GameObject go = Child(body.transform, name);
            SetRect(go, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(318f, -top), new Vector2(450f, 120f));
            AddImage(go, Require<Sprite>(spritePath), Color.white, Image.Type.Sliced);
            Button b = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            b.transition = Selectable.Transition.ColorTint;
            // Rule 11: every new player-facing Button gets ButtonPressFeedback in the same operation.
            if (go.GetComponent<ButtonPressFeedback>() == null) go.AddComponent<ButtonPressFeedback>();
            return go;
        }

        private static TextMeshProUGUI ButtonLabel(GameObject button, string text, Color color)
        {
            GameObject label = Child(button.transform, "Label");
            SetRect(label, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmp = AddText(label, LabelSize, color, TextAlignmentOptions.Center);
            tmp.text = text;
            tmp.enableWordWrapping = false;
            return tmp;
        }

        private static void Separator(GameObject body, string name, float top)
        {
            GameObject go = Child(body.transform, name);
            SetRect(go, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(54f, -top), new Vector2(978f, 2f));
            AddImage(go, Require<Sprite>(SpriteDivider), Divider, Image.Type.Simple, raycast: false);
        }

        private static TextMeshProUGUI Text(GameObject body, string name, float left, float top, float w, float h,
                                            float size, Color color, TextAlignmentOptions align)
        {
            GameObject go = Child(body.transform, name);
            SetRect(go, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(left, -top), new Vector2(w, h));
            return AddText(go, size, color, align);
        }

        private static TextMeshProUGUI AddText(GameObject go, float size, Color color, TextAlignmentOptions align)
        {
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.font = Require<TMP_FontAsset>(FontSemiBold);
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        private static T Require<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new System.InvalidOperationException(
                    $"REUSE SOURCE MISSING: {path} ({typeof(T).Name}). Nothing is built from scratch — fix the path.");
            Log.Append("  reuse ").Append(typeof(T).Name).Append(' ').Append(path)
               .Append(" guid=").Append(AssetDatabase.AssetPathToGUID(path)).Append('\n');
            return asset;
        }

        private static GameObject Child(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        private static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = (RectTransform)go.transform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size; rt.localScale = Vector3.one;
        }

        private static Image AddImage(GameObject go, Sprite? sprite, Color color,
                                      Image.Type type = Image.Type.Simple, bool raycast = true)
        {
            Image img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.sprite = sprite; img.color = color; img.type = type; img.raycastTarget = raycast;
            return img;
        }

        private static void Wire(Object target, string field, Object? value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
                throw new System.InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        private static Color Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }
    }
}
