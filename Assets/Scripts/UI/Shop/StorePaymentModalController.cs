// Assets/Scripts/UI/Shop/StorePaymentModalController.cs
// iap_plumbing — the payment-choice modal a DUAL-priced store row opens (Figma 14289:33223).
//
// A SIBLING OF LoanReturnModalController's SHELL (Pop-up panel sprite + Main Buttons gold/silver),
// not a reuse of its prefab: that one is title + body + two buttons in a row; this node is an item
// row (art + description), a prompt line and THREE buttons stacked. Built by
// Editor/StorePaymentModalBuilder.cs from the palette atoms and saved to
// Resources/Prefabs/Shop/StorePaymentModal.prefab; the store instantiates it lazily under the root
// Canvas (no ShellScene edit).
//
// THE CHOICE IS THE WHOLE MODAL. Both options are gold (the node's two `Main Buttons`, 450×120,
// 24 px apart); CANCEL is the standard silver below. A payment method that is not available is
// REMOVED, never disabled — and a dual row with exactly one method available never opens this at
// all (GeneralShopScreenController routes straight to it).
#nullable enable
using System;
using Golfin.UI.Modals;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Shop
{
    public sealed class StorePaymentModalController : ModalController
    {
        [Header("Content")]
        [SerializeField] private TextMeshProUGUI? titleText;
        [SerializeField] private Image? itemArt;
        [SerializeField] private TextMeshProUGUI? descriptionText;
        [SerializeField] private TextMeshProUGUI? chooseText;

        [Header("Options")]
        [SerializeField] private Button? rpButton;
        [SerializeField] private TextMeshProUGUI? rpAmountText;
        [SerializeField] private Button? moneyButton;
        [SerializeField] private TextMeshProUGUI? moneyPriceText;
        [SerializeField] private Button? cancelButton;
        [SerializeField] private TextMeshProUGUI? cancelText;

        private Action? _onRp;
        private Action? _onMoney;

        /// <summary>True while the modal is open — the store latches BUY on it.</summary>
        public bool IsOpen => IsVisible();

        protected override void Awake()
        {
            base.Awake();
            if (rpButton != null)     rpButton.onClick.AddListener(() => Choose(_onRp));
            if (moneyButton != null)  moneyButton.onClick.AddListener(() => Choose(_onMoney));
            if (cancelButton != null) cancelButton.onClick.AddListener(Hide);
        }

        /// <summary>
        /// Open for one dual-priced row. Every string is already localized; <paramref name="rpCost"/>
        /// renders as coin + digits (never the word "RP"); <paramref name="moneyPrice"/> is
        /// StoreKit's localized string. Pass 0 / empty to REMOVE an option (the caller normally
        /// skips the modal entirely then — see the class note).
        /// </summary>
        public void Open(string title, Sprite? art, string description,
                         int rpCost, string moneyPrice,
                         Action onRp, Action onMoney)
        {
            _onRp = onRp;
            _onMoney = onMoney;

            if (titleText != null) titleText.text = title ?? string.Empty;
            if (itemArt != null)
            {
                itemArt.sprite = art;
                itemArt.enabled = art != null;
            }
            if (descriptionText != null) descriptionText.text = description ?? string.Empty;
            if (chooseText != null) chooseText.text = LocalizationManager.Get("STORE_CHOOSE_PAYMENT");
            if (cancelText != null) cancelText.text = LocalizationManager.Get("MODAL_CANCEL");

            bool hasRp = rpCost > 0;
            bool hasMoney = !string.IsNullOrEmpty(moneyPrice);

            if (rpButton != null) rpButton.gameObject.SetActive(hasRp);
            if (rpAmountText != null)
                rpAmountText.text = hasRp ? rpCost.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;

            if (moneyButton != null) moneyButton.gameObject.SetActive(hasMoney);
            if (moneyPriceText != null) moneyPriceText.text = hasMoney ? moneyPrice : string.Empty;

            Show();
        }

        private void Choose(Action? action)
        {
            Hide();
            action?.Invoke();
        }

        protected override void OnHide()
        {
            base.OnHide();
            _onRp = null;
            _onMoney = null;
        }
    }
}
