// asset_loans_offers §3.3 — the offer modal (Figma 14261:107063, panel 14261:107143).
//
// Opened from the Home offer pill. This is the ONLY screen the recipient ever sees an offer on:
// nothing enters their roster, nothing appears in their bag, and no notice fires — the pill is
// the whole surface, and this is what it opens.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.EconomyRuntime;
using Golfin.Inventory;
using Golfin.Net;
using Golfin.Roster;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Modals;
using Golfin.UI.Polish;
using Golfin.UI.Toast;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Loans
{
    /// <summary>
    /// "KENJI wants to lend you ELIZABETH BLACKWOOD — RARE Lv 80 · 3 DAYS." Accept or decline.
    ///
    /// <para>
    /// EVERYTHING SHOWN COMES OFF THE OFFER ROW OR THE CATALOG, never off the player's own
    /// inventory — the recipient does not own this asset and has no local record of it. The
    /// portrait, the localised name and the rarity come from <c>CharacterDatabaseCSV</c> /
    /// <c>ClubDatabaseCSV</c> by <c>ref_id</c>; the LEVEL and the DAYS and the lender's SHARE come
    /// from the row, because those are facts about this particular offer and not about the asset.
    /// </para>
    /// <para>
    /// THE SHARE IS READ, NOT ASSUMED. <c>lender_share_bp</c> is frozen onto the row at offer time
    /// precisely so the terms a player agrees to are the terms they get — rendering a hardcoded
    /// 20 here would be the one number on screen that could quietly stop matching the money.
    /// </para>
    /// <para>
    /// NOTHING IS OPTIMISTIC. Neither button moves anything locally: ACCEPT sends, the refresh
    /// that follows reconciles the now-`active` row through the EXISTING <c>EnsureBorrowed</c>
    /// path, and that is what puts the asset in the roster. A modal that had already added it
    /// would have to take it back on `limit_in` — which the server re-checks at accept, and which
    /// is a genuinely reachable answer after an offer has sat for two days.
    /// </para>
    /// </summary>
    public sealed class LoanOfferModalController : ModalController
    {
        [Header("Header")]
        [SerializeField] private TextMeshProUGUI? titleText;
        [SerializeField] private TextMeshProUGUI? subtitleText;

        [Header("Asset row")]
        [SerializeField] private Image? assetPortrait;
        [SerializeField] private TextMeshProUGUI? assetNameText;
        [SerializeField] private TextMeshProUGUI? rarityText;
        [SerializeField] private TextMeshProUGUI? levelText;
        [SerializeField] private TextMeshProUGUI? daysText;

        [Header("Body")]
        [SerializeField] private TextMeshProUGUI? termsText;
        [SerializeField] private TextMeshProUGUI? finePrintText;

        [Header("Footer")]
        [SerializeField] private Button? declineButton;
        [SerializeField] private TextMeshProUGUI? declineButtonText;
        [SerializeField] private Button? acceptButton;
        [SerializeField] private TextMeshProUGUI? acceptButtonText;

        private LoanDto? _offer;
        private bool _pending;

        /// <summary>The offer currently on screen, for the pill's "is this one still up" check.</summary>
        public string? CurrentOfferId => _offer?.Id;

        protected override void Awake()
        {
            base.Awake();
            if (declineButton != null) declineButton.onClick.AddListener(OnDecline);
            if (acceptButton  != null) acceptButton.onClick.AddListener(OnAccept);
        }

        /// <summary>Open for one pending offer.</summary>
        public void Open(LoanDto offer)
        {
            if (offer == null) return;
            _offer = offer;
            _pending = false;

            string lender = offer.Lender != null ? offer.Lender.Name : LoanPartyDto.Fallback;

            if (titleText != null)
                titleText.text = LocalizationManager.Get("LOAN_OFFER_TITLE");
            if (subtitleText != null)
                subtitleText.text = string.Format(
                    LocalizationManager.Get("LOAN_OFFER_FROM_FMT"), lender);

            BindAsset(offer);

            if (termsText != null)
                termsText.text = string.Format(LocalizationManager.Get("LOAN_OFFER_TERMS_FMT"),
                                               lender, offer.SharePercent);

            if (finePrintText != null)
                finePrintText.text = string.Format(
                    LocalizationManager.Get("LOAN_OFFER_FINE_FMT"),
                    // HOURS, not FormatTimeLeft's d/h form. The node reads "Offer expires in
                    // 46h." and FormatTimeLeft would render that same instant as "1d 22h",
                    // because it switches to days-and-hours above 24 h — right for a loan that
                    // runs for a week, wrong for a window that is only ever 48 h long and that
                    // the ribbon states in hours two lines away. Same string, same unit.
                    string.Format(LocalizationManager.Get("LOAN_TIME_TO_ANSWER_HOURS_FMT"),
                                  offer.OfferHoursLeft()));

            if (declineButtonText != null)
                declineButtonText.text = LocalizationManager.Get("LOAN_BTN_DECLINE");
            if (acceptButtonText != null)
                acceptButtonText.text = LocalizationManager.Get("LOAN_BTN_ACCEPT");

            SetPending(false);
            Show();
        }

        /// <summary>
        /// Portrait, name, rarity, level, days — from the catalog for the first three and from
        /// the ROW for the last two.
        ///
        /// <para>
        /// A REF THIS BUILD CANNOT DRAW IS STILL ANSWERABLE. If the catalog has no row (an asset
        /// published after this client shipped) the name falls back to the ref id and the
        /// portrait slot is simply left empty — the player can still read the terms and still
        /// DECLINE, which is the outcome that must never be blocked. Refusing to open the modal
        /// would leave a pill that does nothing.
        /// </para>
        /// </summary>
        private void BindAsset(LoanDto offer)
        {
            string name = offer.RefId ?? "";
            Sprite? portrait = null;
            CharacterRarity rarity = CharacterRarity.Common;
            bool haveRarity = false;

            if (offer.IsCharacter)
            {
                CharacterDataRuntime? csv = CharacterDatabaseCSV.Instance?.GetCharacter(offer.RefId);
                if (csv != null)
                {
                    name = csv.GetLocalizedDisplayName(singleLine: true);
                    // The THUMBNAIL, not the full body: the row is a 100×100 square and the
                    // full-body sprite is a standing figure that would letterbox to a sliver.
                    portrait = csv.portraitSprite;
                    rarity = csv.rarity;
                    haveRarity = true;
                }
            }
            else if (offer.IsClub)
            {
                ClubDataRuntime? csv = ClubDatabaseCSV.Instance?.GetClub(offer.RefId);
                if (csv != null)
                {
                    if (!string.IsNullOrEmpty(csv.name)) name = csv.name;
                    portrait = csv.portraitSprite;
                    rarity = csv.rarity;
                    haveRarity = true;
                }
            }

            if (assetNameText != null) assetNameText.text = name.ToUpperInvariant();

            if (assetPortrait != null)
            {
                assetPortrait.sprite = portrait;
                // `enabled`, not SetActive: the slot keeps its size in the horizontal layout so a
                // missing sprite leaves a gap where the art belongs rather than sliding the text
                // 124px left and making the row look like a different design.
                assetPortrait.enabled = portrait != null;
            }

            if (rarityText != null)
            {
                // GetLocalizedRarityName, not GetRarityFullName: the word is a RARITY_* row and
                // JA needs it. The helper already falls back to English on a missing key rather
                // than leaking the key itself onto the screen.
                rarityText.text = haveRarity ? RarityHelper.GetLocalizedRarityName(rarity) : "";
                if (haveRarity) rarityText.color = RarityHelper.GetRarityColor(rarity);
            }

            // "Lv 80" — the project's own unlocalised convention for a level chip, used by the
            // roster cards, the detail panel, CompareController and LoanRecipientRow. A 35th
            // localization key here would make this ONE level read differently from every other
            // one on screen, which is a worse outcome than the literal. Flagged in the report.
            if (levelText != null) levelText.text = $"Lv {offer.Level}";

            // The EXISTING duration keys — "1 DAY" / "3 DAYS" / "7 DAYS" already say the number
            // and already have JA rows, so the modal reuses them rather than adding a fourth
            // spelling of the same three strings.
            if (daysText != null)
                daysText.text = "· " + LocalizationManager.Get(DurationKey(offer.Days));
        }

        /// <summary>The three durations the modal offers, as the keys that already exist. Any
        /// other value (the server's CHECK forbids one, but a future menu could add one) falls
        /// back to the 3-day row rather than rendering a raw key.</summary>
        private static string DurationKey(int days) => days == 1 ? "LOAN_DAYS_1"
                                                     : days == 7 ? "LOAN_DAYS_7"
                                                     : "LOAN_DAYS_3";

        /// <summary>The latch's reset; <see cref="PendingSpend"/> owns the in-flight half.</summary>
        private void SetPending(bool pending)
        {
            _pending = pending;
            if (declineButton != null) declineButton.interactable = !pending;
            if (acceptButton  != null) acceptButton.interactable  = !pending;
        }

        private void OnAccept()
        {
            if (_pending || _offer == null) return;
            StartCoroutine(AnswerRoutine(_offer, accept: true));
        }

        private void OnDecline()
        {
            if (_pending || _offer == null) return;
            StartCoroutine(AnswerRoutine(_offer, accept: false));
        }

        /// <summary>
        /// ONE ROUTINE FOR BOTH ANSWERS. The two differ in exactly three places — which endpoint,
        /// which button carries the ellipsis, and which toast — and everything else (the pending
        /// scope, the transport branch, the refusal branch, the refresh, the close) is identical.
        /// Two copies would be two places for the "restore first, then act" ordering to drift.
        /// </summary>
        private IEnumerator AnswerRoutine(LoanDto offer, bool accept)
        {
            ApiResult<LoanMutationDto>? result = null;

            Button? primary = accept ? acceptButton : declineButton;
            TextMeshProUGUI? primaryLabel = accept ? acceptButtonText : declineButtonText;
            Button? other = accept ? declineButton : acceptButton;

            // Scope BEFORE latch — see LoanModalController.ConfirmRoutine. The OTHER button rides
            // in `alsoDisable`: an answer is being recorded server-side and tapping the opposite
            // one mid-flight would send a second, contradictory answer for the same offer.
            using (PendingSpend.Begin(primary, primaryLabel, other!))
            {
                _pending = true;

                IEnumerator call = accept
                    ? LoanService.Instance.Accept(offer.Id, r => result = r)
                    : LoanService.Instance.Decline(offer.Id, r => result = r);
                while (call.MoveNext()) yield return call.Current;
            }

            SetPending(false);

            if (result == null || !result.Success || result.Data == null)
            {
                // Transport failure: say nothing about the offer, leave the modal open. Both
                // endpoints are idempotent, so a second tap is safe.
                if (!string.IsNullOrEmpty(PointsSpendGate.OfflineMessage))
                    ToastController.Instance?.Show(PointsSpendGate.OfflineMessage);
                yield break;
            }

            if (!result.Data.IsOk)
            {
                // `not_offered` — the lender rescinded, or it lapsed, while this was open.
                // `limit_in` / `borrower_has_it` — the server re-checked at accept and the world
                // moved since the offer went out. All three end the modal: the offer as shown no
                // longer exists, and leaving it up would invite a second doomed tap.
                ToastController.Instance?.Show(
                    LocalizationManager.Get(result.Data.AnswerErrorKey()));
                LoanSyncBehaviour.RequestRefresh("offer-refused");
                Hide();
                yield break;
            }

            TelemetryService.Instance?.RecordSafe("loan_offer_answered",
                () => new Dictionary<string, object>
                {
                    { "loan_id", offer.Id }, { "answer", accept ? "accept" : "decline" }
                });

            // ACCEPT: the refresh is what actually hands the asset over — the row is now `active`
            // and reconciles through the untouched EnsureBorrowed path, exactly as a v1 loan did.
            // DECLINE: the refresh is what makes the pill re-evaluate and vanish.
            LoanSyncBehaviour.RequestRefresh(accept ? "offer-accept" : "offer-decline");

            Hide();

            // THE RECIPIENT'S OWN TOASTS LIVE HERE, not in the reconciler, and that is the
            // opposite of the RETURN flow's rule — deliberately. A return can land from the other
            // device, so its toast belongs to whatever notices; an answer to an offer is always
            // this tap, and the reconciler cannot even see it (an accepted row arrives in `in`
            // looking exactly like any other borrowed asset, and a declined one arrives as
            // something the recipient was never told about).
            string assetName = LoanSyncBehaviour.AssetName(offer);
            ToastController.Instance?.Show(accept
                ? string.Format(LocalizationManager.Get("LOAN_TOAST_ACCEPTED_IN_FMT"),
                                assetName, offer.Days)
                : string.Format(LocalizationManager.Get("LOAN_TOAST_DECLINED_IN_FMT"), assetName));
        }
    }
}
