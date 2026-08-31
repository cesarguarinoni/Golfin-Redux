// Assets/Scripts/UI/Gacha/GachaBannerCard.cs
// gacha_screen Stage 2 — §3c Bind script for GachaBannerCard.prefab
// gacha_client_real_pull §3 — the card now renders the ADMIN'S row rather than the CSV's
// placeholders: the authored title (JA/EN, title only — no tagline, Cesar 2026-08-31), the art
// through the CatalogArtCache ladder, the NUMERIC costs, the banner's ticket icon, and the two
// guarantee lines bound to pityThreshold / pityMinRarity / guaranteeMinRarityX10.
//
// EVERY LINE THIS FILE WRITES IS DATA THE OPERATOR CAN CHANGE WITHOUT A BUILD. That is the whole
// point of the task, and it is why the card no longer carries a single authored string it also
// writes to: a label the code overwrites and the prefab also spells is a label that will one day
// disagree with itself.
//
// The card does NOT decide whether it should exist — GachaBannerCatalog.GetLiveBanners has already
// withheld anything unrollable (§3.1), including a banner whose art does not resolve, so Bind can
// draw unconditionally instead of guarding every slot with a fallback that would show a broken card.
#nullable enable
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Bind script attached to GachaBannerCard.prefab.
    /// Bind() must be called once after spawn with the catalog entry.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class GachaBannerCard : MonoBehaviour
    {
        // ── SerializeField refs — wired by GachaCarouselController.SetupCardRefs() ──

        [SerializeField] private Image?           _artImage;
        [SerializeField] private TextMeshProUGUI? _titleText;
        [SerializeField] private TextMeshProUGUI? _countdownLabel;

        /// <summary>CostArea/CostRow1/CountLabel — the x1 PRICE, not the "COST" word.
        /// See <see cref="BindCosts"/> for why this moved.</summary>
        [SerializeField] private TextMeshProUGUI? _costX1Text;

        /// <summary>CostArea/CostRow2/CountLabel — the x10 price.</summary>
        [SerializeField] private TextMeshProUGUI? _costX10Text;

        [SerializeField] private Button?          _pullX1Button;
        [SerializeField] private Button?          _pullX10Button;
        [SerializeField] private Button?          _rulesButton;

        [Header("Ticket icons (one per cost row) — gacha_client_real_pull §3")]
        [SerializeField] private Image?           _ticketIconX1;
        [SerializeField] private Image?           _ticketIconX10;

        [Header("Guarantee lines — gacha_client_real_pull §3")]
        [Tooltip("PitySection/PityRow1 — the PITY line. Hidden when pityThreshold is 0.")]
        [SerializeField] private GameObject?      _pityRow;
        [SerializeField] private TextMeshProUGUI? _pityLabel;
        [SerializeField] private TextMeshProUGUI? _pityCount;

        [Tooltip("PitySection/PityRow2 — the x10 GUARANTEE line. Hidden when the banner declares none.")]
        [SerializeField] private GameObject?      _guaranteeRow;
        [SerializeField] private TextMeshProUGUI? _guaranteeLabel;
        [Tooltip("PitySection/PityRow2/PityPill — always hidden: the x10 line carries no number.")]
        [SerializeField] private GameObject?      _guaranteePill;

        // ── Runtime state ──────────────────────────────────────────────────────

        private GachaBannerEntry? _entry;

        /// <summary>The catalog entry this card is bound to (read by GachaCarouselController).</summary>
        public GachaBannerEntry? Entry => _entry;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        // The title, the ticket name and both guarantee lines are resolved IMPERATIVELY, so — unlike
        // a LocalizedText label — nothing repaints them when the language changes. The toggle lives
        // in the Settings OVERLAY, which leaves this screen enabled, so OnEnable never re-runs and
        // the card would keep the old language until the Rewards Center was re-entered. Same scar,
        // same fix, as GachaPrizesScreenController.RefreshLocalizedText.
        private void OnEnable()  => LocalizationManager.OnLanguageChanged += ReBind;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= ReBind;

        private void ReBind()
        {
            if (_entry != null) Bind(_entry);
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Bind the card to a catalog entry: title, art, numeric costs, ticket icon, the two
        /// guarantee lines, and the pull / rules handlers.
        /// </summary>
        public void Bind(GachaBannerEntry entry)
        {
            _entry = entry;
            if (entry == null) return;

            BindTitle(entry);
            BindArt(entry);
            BindCosts(entry);
            BindTicketIcon(entry);
            BindGuaranteeLines(entry);
            WireButtons(entry);
        }

        /// <summary>Called each frame by GachaCarouselController to update the countdown display.</summary>
        public void SetCountdownText(string text)
        {
            if (_countdownLabel != null)
                _countdownLabel.text = text;
        }

        // ── Title ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The ladder is: the authored title in the player's language → the authored title in the
        /// other one → the localised <c>nameKey</c> → the <c>nameKey</c> literal.
        ///
        /// <para>
        /// The last two steps are TODAY'S BEHAVIOUR kept for rows nobody has edited. Every bundled
        /// banner ships with <c>nameEn</c> filled in, so in practice the ladder stops at step 1 —
        /// but a row published before the admin gained the name columns would otherwise render an
        /// empty title, which is worse than the shouty placeholder it renders now.
        /// </para>
        /// </summary>
        private void BindTitle(GachaBannerEntry entry)
        {
            if (_titleText == null) return;

            string title = GachaCsvMerge.PickLocalised(entry.NameEn, entry.NameJa);

            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(entry.NameKey))
            {
                // LocalizationManager.Get returns the KEY when the key is unknown, which is exactly
                // the fallback this wants — so the "does the key exist" question does not need
                // asking separately.
                title = LocalizationManager.Get(entry.NameKey);
            }

            _titleText.text = title;
        }

        // ── Art ────────────────────────────────────────────────────────────────

        private void BindArt(GachaBannerEntry entry)
        {
            if (_artImage == null) return;

            var sprite = GachaBannerArt.Resolve(entry);
            if (sprite == null)
            {
                // §3.1 says a banner whose art does not resolve is WITHHELD, so reaching here means
                // the catalog admitted a banner this card cannot draw — a defect in the pair, not a
                // content problem. Loud, and the card hides rather than showing an empty frame.
                Debug.LogError($"[GachaBannerCard] '{entry.BannerId}' has no resolvable art, but the " +
                               "catalog admitted it. GachaBannerCatalog.IsRollable and GachaBannerArt " +
                               "disagree — hiding the card.");
                gameObject.SetActive(false);
                return;
            }

            _artImage.sprite = sprite;
        }

        // ── Costs ──────────────────────────────────────────────────────────────

        /// <summary>
        /// The two price rows: <c>COST [ticket icon] x50</c> above PULL x1, <c>… x450</c> above
        /// PULL x10.
        ///
        /// <para>
        /// ⚠️ <b>The wiring moved, and deliberately.</b> Both fields used to point at the row's
        /// <c>CostText</c> — the authored word "COST" — which is the one label on the row that must
        /// NOT be overwritten (SPEC §3 says the authored label and the ticket icon stay). The price
        /// belongs in <c>CountLabel</c>, the slot after the icon, whose authored placeholder is
        /// "x1"/"x10": the multiplier is already on the button below it ("PULL x1"), so that slot
        /// was never the multiplier — it was the price, standing in for itself.
        /// </para>
        /// <para>
        /// The <c>x</c> is kept from the authored form: after a ticket icon, "x50" reads as fifty
        /// tickets and a bare "50" reads as a price in nothing. (SPEC §3 writes
        /// <c>CostX1.ToString()</c>; this is that number with the authored prefix, flagged in the
        /// implementer report for Cesar's sign-off on the screenshot.)
        /// </para>
        /// </summary>
        private void BindCosts(GachaBannerEntry entry)
        {
            if (_costX1Text  != null) _costX1Text.text  = "x" + entry.CostX1;
            if (_costX10Text != null) _costX10Text.text = "x" + entry.CostX10;
        }

        // ── Ticket icon ────────────────────────────────────────────────────────

        /// <summary>
        /// The banner's own currency. A ticket type with no art — every type but Standard today —
        /// KEEPS THE PREFAB'S AUTHORED ICON rather than blanking the row: a missing Gold ticket
        /// icon is an art gap (spec D), not a reason to render a currency-less price.
        /// </summary>
        private void BindTicketIcon(GachaBannerEntry entry)
        {
            var type = TicketTypeCatalog.Get(entry.TicketType);
            if (type == null) return;

            var sprite = Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl, type.IconUrl)
                      ?? LoadTicketSprite(type.IconSprite)
                      ?? Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl);

            if (sprite == null) return;   // keep the authored icon

            if (_ticketIconX1  != null) _ticketIconX1.sprite  = sprite;
            if (_ticketIconX10 != null) _ticketIconX10.sprite = sprite;
        }

        private static Sprite? LoadTicketSprite(string? name)
            => string.IsNullOrWhiteSpace(name) ? null : Resources.Load<Sprite>("Art/Gacha/Tickets/" + name!.Trim());

        // ── Guarantee lines ────────────────────────────────────────────────────

        /// <summary>
        /// The card's two authored guarantee lines, bound to the row that produced them.
        ///
        /// <para>
        /// Line 1 is PITY: "Guaranteed {rarity} or higher within [{n} pulls]". It exists only when
        /// the banner has a pity threshold — <c>pityThreshold = 0</c> means no pity at all, which is
        /// a real configuration (plan §9: "pity per banner, may be none"), not a missing value.
        /// </para>
        /// <para>
        /// Line 2 is the x10 FLOOR: "Every 10-pull includes at least one {rarity}", with no pill —
        /// the number in it is always ten, and it is already in the sentence.
        /// </para>
        /// <para>
        /// A banner with neither shows neither. The two rows are ABSOLUTELY POSITIONED inside
        /// PitySection (no layout group — verified against the prefab), so hiding one leaves the
        /// other exactly where it was authored instead of reflowing the card.
        /// </para>
        /// </summary>
        private void BindGuaranteeLines(GachaBannerEntry entry)
        {
            bool hasPity = entry.PityThreshold > 0;
            if (_pityRow != null) _pityRow.SetActive(hasPity);
            if (hasPity)
            {
                if (_pityLabel != null)
                    _pityLabel.text = string.Format(LocalizationManager.Get("GACHA_CARD_PITY"),
                                                    RarityName(entry.PityMinRarity));
                if (_pityCount != null)
                    _pityCount.text = string.Format(LocalizationManager.Get("GACHA_CARD_PULLS"),
                                                    entry.PityThreshold);
            }

            bool hasGuarantee = entry.HasGuaranteeX10;
            if (_guaranteeRow != null) _guaranteeRow.SetActive(hasGuarantee);
            if (hasGuarantee)
            {
                if (_guaranteeLabel != null)
                    _guaranteeLabel.text = string.Format(LocalizationManager.Get("GACHA_CARD_GUARANTEE_X10"),
                                                         RarityName(entry.GuaranteeMinRarityX10));
                // The x10 line has no number, so its authored pill is hidden rather than left
                // showing the placeholder "99 pulls".
                if (_guaranteePill != null) _guaranteePill.SetActive(false);
            }
        }

        /// <summary>The rarity's display name, through the RARITY_* keys the roster already ships.</summary>
        private static string RarityName(Golfin.Roster.CharacterRarity rarity)
            => LocalizationManager.Get("RARITY_" + rarity.ToString().ToUpperInvariant());

        // ── Buttons ────────────────────────────────────────────────────────────

        private void WireButtons(GachaBannerEntry entry)
        {
            if (_pullX1Button != null)
            {
                _pullX1Button.onClick.RemoveAllListeners();
                _pullX1Button.onClick.AddListener(OnPullX1);
            }
            if (_pullX10Button != null)
            {
                _pullX10Button.onClick.RemoveAllListeners();
                _pullX10Button.onClick.AddListener(OnPullX10);
            }

            if (_rulesButton != null)
            {
                _rulesButton.onClick.RemoveAllListeners();
                _rulesButton.onClick.AddListener(OnRules);

                // ALWAYS VISIBLE AGAIN (gacha_ops_polish §2). The button used to hide itself when
                // rulesUrl was blank, because opening a browser was the only thing it could do.
                // It now opens the in-app RATES modal, whose whole body is generated from the
                // banner's own published pool — so there is no configuration under which it has
                // nothing to show, and the odds disclosure must not be hideable by leaving a
                // free-text URL column empty.
                _rulesButton.gameObject.SetActive(true);
            }
        }

        // ── Private handlers ───────────────────────────────────────────────────

        // gacha_client_real_pull §4.2 — the ENTRY goes with the count, so the flow can price the
        // guard and name the banner without reaching back into a catalog that may have been
        // reloaded in between.
        private void OnPullX1()
        {
            if (_entry == null) return;
            Debug.Log($"[GachaBannerCard] Pull x1 tapped on {_entry.BannerId}.");
            GachaPullFlow.Pull(_entry, 1);
        }

        private void OnPullX10()
        {
            if (_entry == null) return;
            Debug.Log($"[GachaBannerCard] Pull x10 tapped on {_entry.BannerId}.");
            GachaPullFlow.Pull(_entry, 10);
        }

        /// <summary>
        /// RULES &amp; RATES opens the in-app modal (gacha_ops_polish §2), never the browser. The
        /// <c>rulesUrl</c> still has a job — the modal turns it into a "Full rules" row when it
        /// survives <c>BannerPolicy.IsLinkAllowed</c> — but it is no longer the button's ONLY
        /// destination, which is what let a blank column hide the odds.
        /// </summary>
        private void OnRules()
        {
            if (_entry == null) return;

            var modal = GachaRatesModalController.Instance;
            if (modal == null)
            {
                // No modal in the scene. Loud rather than silent: the rates screen is a disclosure
                // obligation, so a build that cannot show it is a defect and not a degraded mode.
                Debug.LogError("[GachaBannerCard] RULES tapped but there is no GachaRatesModalController " +
                               "in the scene — the rates cannot be shown. Check the ShellScene modal root.");
                return;
            }

            modal.Show(_entry);
        }
    }
}
