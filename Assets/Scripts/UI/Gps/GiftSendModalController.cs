// gps_gifts_votes — the one modal both gift writes go through.
#nullable enable
using System;
using System.Globalization;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Social;
using Golfin.UI.Modals;
using Golfin.UI.Toast;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Confirms and performs an RP gift or a catalog purchase.
    ///
    /// <para>
    /// ONE CONTROLLER FOR BOTH because they differ only in what the header names and which
    /// service call CONFIRM makes. The recipient line, the balance line, the error line and the
    /// two actions are the same, and the amount row is simply hidden in purchase mode — the
    /// price is the item's, not the player's to choose.
    /// </para>
    /// <para>
    /// THE IDEMPOTENCY KEY IS MINTED WHEN THE MODAL OPENS, not when CONFIRM is pressed, and it is
    /// held until the call comes back. That is what makes a double-tap and a timeout-retry the
    /// same gift rather than two: the server keys the ledger on it
    /// (<c>2026_09_02_gift_atomic.sql</c>), so a second request with the same key returns the
    /// first one's outcome and moves nothing.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiftSendModalController : ModalController
    {
        private const string Tag = "[GiftSendModal]";

        /// <summary>The four RP amounts the modal offers (SPEC § Client data bindings).</summary>
        public static readonly int[] Presets = { 50, 100, 500, 1000 };

        [Header("Gift modal")]
        [SerializeField] private TextMeshProUGUI? _recipient;
        [SerializeField] private TextMeshProUGUI? _balance;
        [SerializeField] private TextMeshProUGUI? _status;

        [Tooltip("One button per entry in Presets, same order.")]
        [SerializeField] private Button[] _amountButtons = new Button[0];

        [Tooltip("The button roots, parallel to _amountButtons. Each carries a 'Selected' child " +
                 "that marks the chosen amount.")]
        [SerializeField] private GameObject[] _amountRoots = new GameObject[0];

        [SerializeField] private Button? _confirmButton;
        [SerializeField] private Button? _cancelButton;

        private enum Mode { SendPts, Purchase }

        private Mode _mode;
        private string? _receiverId;
        private string? _receiverName;
        private GiftItemDto? _item;
        private int _amount;
        private string? _key;
        private bool _inFlight;
        private bool _wired;
        private Action? _onCommitted;

        // ═════════════════════════════════════════════════════════════════════
        // Entry points
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Open in SEND mode for one recipient. <paramref name="onCommitted"/> fires only
        /// after the server has actually moved points.</summary>
        public void OpenSend(string receiverId, string receiverName, Action? onCommitted = null)
        {
            _mode = Mode.SendPts;
            _receiverId = receiverId;
            _receiverName = receiverName;
            _item = null;
            _amount = Presets[0];
            _onCommitted = onCommitted;
            Open();
        }

        /// <summary>Open in PURCHASE mode for one catalog item.</summary>
        public void OpenPurchase(GiftItemDto item, Action? onCommitted = null)
        {
            _mode = Mode.Purchase;
            _item = item;
            _receiverId = null;
            _receiverName = null;
            _amount = item != null && item.PriceActivityPts.HasValue ? item.PriceActivityPts.Value : 0;
            _onCommitted = onCommitted;
            Open();
        }

        private void Open()
        {
            WireOnce();
            _inFlight = false;
            // A FRESH key per OPEN — see the class remarks. Two separate gifts to the same player
            // must not share one, or the second would be swallowed as a replay of the first.
            _key = GiftService.NewKey();
            Show();
            Repaint();
            SetStatus(string.Empty);
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < _amountButtons.Length; i++)
            {
                int index = i;   // captured per iteration, not per loop
                if (_amountButtons[i] == null) continue;
                _amountButtons[i].onClick.AddListener(() => OnAmountPicked(index));
            }
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
            if (_cancelButton != null) _cancelButton.onClick.AddListener(Hide);
        }

        private void OnAmountPicked(int index)
        {
            if (_inFlight) return;
            if (index < 0 || index >= Presets.Length) return;
            _amount = Presets[index];
            Repaint();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Paint
        // ═════════════════════════════════════════════════════════════════════

        private void Repaint()
        {
            bool send = _mode == Mode.SendPts;

            if (_recipient != null)
                _recipient.text = send
                    ? (_receiverName ?? "—")
                    : (_item != null ? _item.Name : "—");

            if (_balance != null)
                _balance.text = string.Format(LocalizationManager.Get("GPS_GIFT_MODAL_BALANCE"),
                                              Balance().ToString("N0", CultureInfo.InvariantCulture));

            // The amount row is the player's choice only when they are sending; a purchase costs
            // what the item costs.
            for (int i = 0; i < _amountRoots.Length; i++)
            {
                if (_amountRoots[i] == null) continue;
                _amountRoots[i].SetActive(send);
                Transform? sel = _amountRoots[i].transform.Find("Selected");
                if (sel != null)
                    sel.gameObject.SetActive(send && i < Presets.Length && Presets[i] == _amount);
            }

            if (_confirmButton != null)
                _confirmButton.interactable = !_inFlight && _amount > 0 && _amount <= Balance();
        }

        /// <summary>
        /// The SENDABLE balance — <c>activity_pts</c>, not <c>total_points</c>.
        ///
        /// <para>
        /// <c>golfin_gift_pts</c> refuses on <c>activity_pts &lt; amount</c>: gift_pts are
        /// earnings and are not spendable on gifting somebody else. Showing RP here would let the
        /// player pick an amount the server is going to refuse, on an account whose balance
        /// includes gifts. A purchase in <c>activity</c> currency draws from the same bucket, so
        /// the same number is right in both modes.
        /// </para>
        /// </summary>
        private static int Balance()
        {
            PointsBalance? b = PointsService.Instance.LastBalance;
            return b != null ? b.ActivityPts : 0;
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Commit
        // ═════════════════════════════════════════════════════════════════════

        private void OnConfirm()
        {
            if (_inFlight) return;

            if (_amount > Balance())
            {
                SetStatus(LocalizationManager.Get("GPS_GIFT_INSUFFICIENT"));
                return;
            }

            _inFlight = true;
            Repaint();
            SetStatus(LocalizationManager.Get("GPS_GIFT_MODAL_SENDING"));

            if (_mode == Mode.SendPts) SendPts();
            else Purchase();
        }

        private void SendPts()
        {
            if (string.IsNullOrEmpty(_receiverId))
            {
                Fail("no receiver id");
                return;
            }

            ApiClient.Instance.Run(
                GiftService.Instance.SendPts(_receiverId!, _amount, _key!, OnSendResult));
        }

        private void OnSendResult(ApiResult<GiftSendResultDto> result)
        {
            _inFlight = false;

            if (result == null || !result.Success)
            {
                Fail(result != null ? result.ToString() : "no result", result);
                return;
            }

            Debug.Log($"{Tag} sent {_amount} to {_receiverName} " +
                      $"(replayed={result.Data?.Replayed}, key={_key}).");

            Toast(string.Format(LocalizationManager.Get("GPS_GIFT_SENT"),
                                _amount.ToString("N0", CultureInfo.InvariantCulture),
                                _receiverName ?? string.Empty));
            Committed();
        }

        private void Purchase()
        {
            if (_item == null || string.IsNullOrEmpty(_item.Id))
            {
                Fail("no item id");
                return;
            }

            ApiClient.Instance.Run(
                GiftService.Instance.Purchase(_item.Id, "activity", _key!, OnPurchaseResult));
        }

        private void OnPurchaseResult(ApiResult<GiftPurchaseResultDto> result)
        {
            _inFlight = false;

            if (result == null || !result.Success)
            {
                Fail(result != null ? result.ToString() : "no result", result);
                return;
            }

            Debug.Log($"{Tag} purchased {_item?.Name} for {_amount} " +
                      $"(replayed={result.Data?.Replayed}, key={_key}).");

            Toast(string.Format(LocalizationManager.Get("GPS_GIFT_PURCHASED"),
                                _item != null ? _item.Name : string.Empty));
            Committed();
        }

        /// <summary>
        /// The one place both flows finish. The balance refresh is NOT optional and NOT a
        /// nicety: the top bar's RP is <c>PointsService</c>'s cached number, and the server just
        /// changed it out from under the cache.
        /// </summary>
        private void Committed()
        {
            PointsService.Instance.RefreshBalanceAsync();
            _onCommitted?.Invoke();
            Hide();
        }

        private void Fail(string why, ApiResult<GiftSendResultDto>? r = null)
            => FailInner(why, r?.StatusCode ?? 0, r?.RawBody);

        private void Fail(string why, ApiResult<GiftPurchaseResultDto>? r)
            => FailInner(why, r?.StatusCode ?? 0, r?.RawBody);

        private void FailInner(string why, long status, string? body)
        {
            _inFlight = false;
            Debug.LogWarning($"{Tag} failed: {why} (status={status}).");

            // The server's two refusals the player can act on are both 400s and both carry their
            // reason in the body; everything else is "try again".
            bool insufficient = status == 400 &&
                                (body ?? "").IndexOf("Insufficient", StringComparison.OrdinalIgnoreCase) >= 0;
            SetStatus(LocalizationManager.Get(insufficient
                ? "GPS_GIFT_INSUFFICIENT"
                : "GPS_GIFT_FAILED"));
            Repaint();
        }

        private static void Toast(string message)
        {
            if (ToastController.Instance != null) ToastController.Instance.Show(message);
            else Debug.Log($"{Tag} {message}");
        }
    }
}
