// Assets/Scripts/UI/Shop/GeneralShopCard.cs
// Order 610 — general_shop_ui (Phase B)
// Binds a catalog entry onto the approved Rewards-Center card structure. DATA ONLY — never
// touches layout, so a generated card stays pixel-identical to the hand-approved template.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Inventory;
using Golfin.Roster;
using GolfinRedux.UI.Gacha;   // TicketTypeCatalog — the published ticket_types rows

namespace GolfinRedux.UI.Shop
{
    /// <summary>
    /// Runtime binder on a Rewards-Center card prefab (club or ball template). The controller
    /// instantiates the matching template per <see cref="ShopCatalogEntry"/> and calls <see cref="Bind"/>.
    /// Bar encodings reproduce the approved cards exactly:
    ///   • Club continuous bar  = Fill RectTransform width, ≈331px track at 60-unit full-scale
    ///     (durability = current/max); value shown as the raw stat / "cur/max".
    ///   • Ball segmented bar    = 21 cells (L0..L9, Div, R0..R9); +V lights R0..R(V-1),
    ///     −V lights the innermost |V| left cells; value shown signed ("+5").
    /// </summary>
    public class GeneralShopCard : MonoBehaviour
    {
        /// <summary>Raised when an enabled BUY is tapped; the controller runs the purchase.</summary>
        public event Action<GeneralShopCard> OnBuyClicked;

        public ShopCatalogEntry Entry { get; private set; }

        // ── Encoding constants (derived from the approved cards) ────────────────
        private const float ClubBarTrackPx = 331f;

        /// <summary>The template's authored Portrait rect, restored by the bindings that want it
        /// centred (club / ball / item). Only the character binding overrides it.</summary>
        private static readonly Vector2 PortraitAuthoredSize = new Vector2(150f, 222f);

        /// <summary>`tournament_image` is stretched to the card height minus 6px (274 - 6). Read from
        /// the live rect when it is resolved; this is the fallback for the first Bind, which can run
        /// before the layout group has sized anything.</summary>
        private const float PortraitTileHeightPx = 268f;

        /// <summary>How much taller than the tile the character portrait is drawn. 1.0 exactly fills
        /// it; above that the extra is cropped off the TOP by the tile's Mask.</summary>
        private const float CharacterPortraitZoom = 1.12f;

        /// <summary>
        /// Width of the name box, and therefore the gutter before the rarity/level block.
        ///
        /// <para>
        /// Was 242px, which is EXACTLY where the HDiv sits — so any name long enough to fill the box
        /// after auto-shrink ended up touching the divider. Measured on the live cards at 242:
        /// "IRON 9 KLYRO" −0.5px, "P.WEDGE ROYAL SWING" +0.2px, "OLIVIA GUARINONI" −0.3px. Auto-size
        /// cannot save you here: it shrinks text to fit the BOX, so if the box ends at the divider,
        /// a full name ends at the divider too. Narrowing the box is what buys the gutter.
        /// </para>
        /// </summary>
        private const float NameBoxWidthPx = 232f;
        private const float ClubBarFullScale = 60f;   // Val 40 → 218.5px, 9 → 49.7px ⇒ ~331/60
        private static readonly Color BallSegOn  = new Color32(0x33, 0x80, 0xE6, 0xFF);
        private static readonly Color BallSegOff = new Color32(0x40, 0x40, 0x4D, 0x80);

        private bool _isBall;

        // ── Stat-row icons for the non-club bindings (shop_server_purchase §3.4) ──
        //
        // The club template identifies each StatRow by an ICON, not by a text label — there is no
        // label child to write "STR / CTRL / REC / STA" into (verified against the prefab). So a
        // character row swaps the icon instead, which is the mechanism the card already has.
        // StatRow_0 is deliberately absent from this list: Strength is Strength, and the club
        // template's own IconStrenght is already the right sprite for it.
        //
        // These are SERIALIZED rather than Resources.Load'd because the art lives in
        // Assets/Art/RosterScreen/, outside any Resources folder, and copying four PNGs into
        // Resources to avoid four Inspector references would duplicate shipped art. Wired on
        // Resources/Prefabs/Shop/GeneralShopCard_Club.
        [Header("Character stat icons (wired on GeneralShopCard_Club)")]
        [SerializeField] private Sprite _iconClubControl;
        [SerializeField] private Sprite _iconRecovery;
        [SerializeField] private Sprite _iconStamina;

        [Header("Item row icon (wired on GeneralShopCard_Club)")]
        [SerializeField] private Sprite _iconItemRestore;

        // ── Public API ──────────────────────────────────────────────────────────

        public void Bind(ShopCatalogEntry entry)
        {
            Entry = entry;
            _isBall = entry != null && entry.Category == ShopCategory.Ball;

            if (entry == null) return;

            switch (entry.Category)
            {
                case ShopCategory.Ball:      BindBall(entry);      break;
                case ShopCategory.Character: BindCharacter(entry); break;
                case ShopCategory.Item:      BindItem(entry);      break;
                // A ticket listing sells N of one ticket type (gacha_server_pull §5.2, behind
                // TICKET_SHOP_BUILD). RefId is the ticket_types id as a decimal string, and the
                // quantity comes off the catalog row — the shop sells one bundle per listing.
                case ShopCategory.Ticket:    BindTicket(entry.RefId, entry.Quantity); break;
                default:                     BindClub(entry);      break;
            }

            ConstrainName();
            BindPrice(entry);
            WireBuy(entry);
        }

        /// <summary>
        /// Keep the name from overrunning the inline rarity/level block (HMid at x≈467). The approved
        /// 320px box overlaps it; clamp to ~242px + auto-shrink so short names stay 32px and long club
        /// names ("P.WEDGE ROYAL SWING") shrink to fit instead of colliding.
        /// </summary>
        private void ConstrainName()
        {
            var nl = Find("NameLabel") as RectTransform;
            var tmp = nl != null ? nl.GetComponent<TextMeshProUGUI>() : null;
            if (nl == null || tmp == null) return;
            nl.sizeDelta = new Vector2(NameBoxWidthPx, nl.sizeDelta.y);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = 32f;
            tmp.fontSizeMin = 12f;   // low floor so the FULL name shrinks to fit (no ellipsis, no overlap)
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        /// <summary>
        /// The referenced row is not in this build's database, so there is nothing to bind.
        ///
        /// <para>
        /// This branch should now be UNREACHABLE: <c>GeneralShopCatalog.Admit</c> resolves the same
        /// reference and withholds the entry before a card is ever instantiated (shop_stocking §6).
        /// It stays, and it got louder, because of what it used to do — return half-way through Bind
        /// and leave an instantiated card on screen with no art, no name and a live BUY button, on a
        /// row the server refuses anyway. An unreachable branch that still renders the bug it was
        /// supposed to prevent is not a safety net.
        /// </para>
        /// <para>
        /// So: hide the card outright, and log an ERROR rather than a warning — reaching here means
        /// Admit and Bind disagree about what is renderable, which is a defect in the pair, not a
        /// content problem an operator can fix.
        /// </para>
        /// </summary>
        private void HideUnbindable(ShopCatalogEntry entry, string kind)
        {
            Debug.LogError($"[GeneralShopCard] {kind} '{entry.RefId}' (entry '{entry.EntryId}') is not in " +
                           "this build's database, so the card cannot be bound. Hiding it — " +
                           "GeneralShopCatalog.Admit should have withheld this row (shop_stocking §6).");
            gameObject.SetActive(false);
        }

        // ── Club variant ──────────────────────────────────────────────────────────

        private void BindClub(ShopCatalogEntry entry)
        {
            var club = ClubDatabaseCSV.Instance != null ? ClubDatabaseCSV.Instance.GetClub(entry.RefId) : null;
            if (club == null) { HideUnbindable(entry, "club"); return; }

            int startLvl = StartingLevel(club.rarity);
            string rar = club.rarity.ToString();

            SetRarityTile(rar);
            SetImage("tournament_image/Portrait", club.portraitSprite != null ? club.portraitSprite : club.portraitFull);
            ResetPortraitRect();
            SetText("NameLabel", club.name.ToUpperInvariant());

            // Re-show whatever a previous Bind on this same card may have hidden. Bind is called a
            // SECOND time on the same instance after a purchase (HandleBuy re-binds to show OWNED),
            // and while a card never changes category today, leaving that as an unstated assumption
            // is how a re-bind silently ships a card with missing rows.
            SetActive("DistRow", true);
            SetActive("HLevel", true);
            for (int i = 0; i <= 4; i++) SetActive($"StatRow_{i}", true);

            SetText("DistRow/Txt", $"{club.baseDistance} yd");

            SetClubBar(0, club.basePower);
            SetClubBar(1, club.baseAccuracy);
            SetClubBar(2, club.baseLieResistance);
            SetClubBar(3, club.baseLoft);
            SetClubDurabilityBar(4, club.maxDurability, club.maxDurability);

            SetText("HMid", RarityLetter(club.rarity));
            SetText("HLevel", $"Lv {startLvl}/{club.maxLevel}");
        }

        private void SetClubBar(int i, int value)
        {
            SetText($"StatRow_{i}/Val", value.ToString());
            var fill = Find($"StatRow_{i}/BarBg/Fill") as RectTransform;
            if (fill == null) return;
            float w = Mathf.Clamp01(value / ClubBarFullScale) * ClubBarTrackPx;
            fill.offsetMin = new Vector2(0f, fill.offsetMin.y);
            fill.offsetMax = new Vector2(w, fill.offsetMax.y);
        }

        private void SetClubDurabilityBar(int i, int cur, int max)
        {
            SetText($"StatRow_{i}/Val", $"{cur}/{max}");
            var fill = Find($"StatRow_{i}/BarBg/Fill") as RectTransform;
            if (fill == null) return;
            float frac = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
            fill.offsetMin = new Vector2(0f, fill.offsetMin.y);
            fill.offsetMax = new Vector2(frac * ClubBarTrackPx, fill.offsetMax.y);
        }

        // ── Ball variant ──────────────────────────────────────────────────────────

        private void BindBall(ShopCatalogEntry entry)
        {
            var ball = BallDatabaseCSV.Instance != null ? BallDatabaseCSV.Instance.GetBall(entry.RefId) : null;
            if (ball == null) { HideUnbindable(entry, "ball"); return; }

            string rar = string.IsNullOrEmpty(entry.Rarity) ? "Common" : entry.Rarity;

            SetRarityTile(rar);
            SetImage("tournament_image/Portrait", ball.thumbnailSprite != null ? ball.thumbnailSprite : ball.fullSprite);
            ResetPortraitRect();
            SetText("NameLabel", ball.name.ToUpperInvariant());
            for (int i = 0; i <= 4; i++) SetActive($"StatRow_{i}", true);

            SetBallBar(0, ball.power);
            SetBallBar(1, ball.rebound);
            SetBallBar(2, ball.windResistance);
            SetBallBar(3, ball.roll);
            SetBallBar(4, ball.spin);

            SetText("HMid", rar);
        }

        private void SetBallBar(int i, int value)
        {
            SetText($"StatRow_{i}/Val", value > 0 ? $"+{value}" : value.ToString());
            var bg = Find($"StatRow_{i}/BarBg");
            if (bg == null) return;

            int v = Mathf.Clamp(value, -10, 10);
            for (int k = 0; k < 10; k++)
            {
                bool rightOn = v > 0 && k < v;            // R0..R(v-1)
                bool leftOn  = v < 0 && k >= (10 + v);    // innermost |v| left cells: L(10+v)..L9
                SetSeg(bg, $"R{k}", rightOn);
                SetSeg(bg, $"L{k}", leftOn);
            }
        }

        private static void SetSeg(Transform bg, string name, bool on)
        {
            var seg = bg.Find(name);
            var img = seg != null ? seg.GetComponent<Image>() : null;
            if (img != null) img.color = on ? BallSegOn : BallSegOff;
        }

        // ── Character variant (shop_server_purchase §3.4) ────────────────────────
        //
        // Reuses the CLUB template wholesale — there is no character card design and none is coming
        // (decision of record, Cesar 2026-08-27). Everything the club binding does that a character
        // has no analogue for is HIDDEN rather than left showing stale club data: DistRow (no yardage)
        // and StatRow_4 (no durability).

        private void BindCharacter(ShopCatalogEntry entry)
        {
            var ch = CharacterDatabaseCSV.Instance != null
                ? CharacterDatabaseCSV.Instance.GetCharacter(entry.RefId)
                : null;
            if (ch == null) { HideUnbindable(entry, "character"); return; }

            var portrait = ch.portraitSprite != null ? ch.portraitSprite : ch.portraitFullSprite;

            SetRarityTile(ch.rarity.ToString());
            SetImage("tournament_image/Portrait", portrait);
            FillPortraitFromBottom(portrait, CharacterPortraitZoom);

            // GetLocalizedDisplayName(singleLine: true) returns the FIRST NAME ONLY — which is why
            // this card used to read just "JAMES". The two-line form is the one that also localises
            // the LAST name, so take that and flatten its newline to a space: the card wants both
            // names on ONE line, the way the club cards read "P.WEDGE ROYAL SWING". It already
            // uppercases. ConstrainName's autosize shrinks a long pair to fit rather than letting it
            // collide with the rarity/level block.
            SetText("NameLabel", ch.GetLocalizedDisplayName().Replace("\n", " "));

            // A character has no distance and no durability. Left visible they would show the CLUB
            // template's authored placeholders ("180 yd", "100/100") as if they were data.
            SetActive("DistRow", false);
            SetActive("StatRow_4", false);

            // Full-scale is the character's OWN rarity cap per stat (RarityStatCaps), not the club
            // template's flat 60. A Common's 25-cap Strength and a Supreme's 50-cap Strength are
            // different scales, and drawing both against 60 would make every low-rarity character look
            // uniformly weak rather than "near their ceiling".
            SetCharacterBar(0, ch.baseStrength,   RarityStatCaps.GetStatCap(ch.rarity, "Strength"),    null);
            SetCharacterBar(1, ch.baseClubControl, RarityStatCaps.GetStatCap(ch.rarity, "ClubControl"), _iconClubControl);
            SetCharacterBar(2, ch.baseRecovery,   RarityStatCaps.GetStatCap(ch.rarity, "Recovery"),    _iconRecovery);
            SetCharacterBar(3, ch.baseStamina,    RarityStatCaps.GetStatCap(ch.rarity, "Stamina"),     _iconStamina);

            SetText("HMid", RarityLetter(ch.rarity));
            SetText("HLevel", $"Lv {ch.startLevel}/{ch.maxLevel}");
        }

        /// <summary>
        /// Draw the portrait so its BOTTOM edge sits on the tile's bottom, scaled to fill the tile's
        /// height (times <paramref name="zoom"/>).
        ///
        /// <para>
        /// Character art is framed head-and-shoulders with the body running off the bottom of the
        /// source image, so the template's centred 150×222 rect leaves a visible band of empty tile
        /// under the chin. Filling from the bottom pushes the overflow off the TOP instead, where
        /// cropping a little headroom reads as a tighter portrait rather than as a gap. The tile
        /// carries a Mask, so anything past its bounds is clipped, not drawn over the card.
        /// </para>
        /// <para>
        /// Width comes from the SPRITE's own aspect, not the authored rect, so a portrait with
        /// different proportions is cropped rather than stretched. Anchors and pivot are deliberately
        /// left alone (centre/centre) — that is what lets <see cref="ResetPortraitRect"/> put the
        /// template's framing back with two assignments.
        /// </para>
        /// </summary>
        private void FillPortraitFromBottom(Sprite sprite, float zoom)
        {
            var tile = Find("tournament_image") as RectTransform;
            var p    = Find("tournament_image/Portrait") as RectTransform;
            if (tile == null || p == null || sprite == null || sprite.rect.height <= 0f) return;

            // Bind can run before the layout group has sized the card, in which case the live rect
            // is not yet meaningful and the authored height is the better answer.
            float tileH = tile.rect.height > 1f ? tile.rect.height : PortraitTileHeightPx;

            float h = tileH * zoom;
            float aspect = sprite.rect.width / sprite.rect.height;

            p.sizeDelta = new Vector2(h * aspect, h);
            // Centre pivot: lifting the centre by (h - tileH)/2 puts the bottom edge exactly on the
            // tile's bottom, and the remaining (h - tileH) overflows off the top.
            p.anchoredPosition = new Vector2(0f, (h - tileH) * 0.5f);
        }

        /// <summary>Restore the template's centred portrait framing. Called by every binding that is
        /// NOT the character one, so a card re-bound after a purchase cannot inherit the zoom.</summary>
        private void ResetPortraitRect()
        {
            var p = Find("tournament_image/Portrait") as RectTransform;
            if (p == null) return;
            p.sizeDelta = PortraitAuthoredSize;
            p.anchoredPosition = Vector2.zero;
        }

        /// <param name="icon">Null leaves the template's own icon in place — correct for StatRow_0,
        /// whose IconStrenght already means Strength for a character too.</param>
        private void SetCharacterBar(int i, int value, int cap, Sprite icon)
        {
            SetText($"StatRow_{i}/Val", value.ToString());
            if (icon != null) SetImage($"StatRow_{i}/Icon", icon);

            var fill = Find($"StatRow_{i}/BarBg/Fill") as RectTransform;
            if (fill == null) return;
            float frac = cap > 0 ? Mathf.Clamp01((float)value / cap) : 0f;
            fill.offsetMin = new Vector2(0f, fill.offsetMin.y);
            fill.offsetMax = new Vector2(frac * ClubBarTrackPx, fill.offsetMax.y);
        }

        // ── Item variant (shop_server_purchase §3.4) ─────────────────────────────
        //
        // An item has ONE number worth showing (how much durability it restores) and no stat lanes at
        // all, so all five StatRows and the level chip are hidden and DistRow carries the restore
        // line. Items STACK, so there is deliberately no owned state — see WireBuy.

        private void BindItem(ShopCatalogEntry entry)
        {
            var item = ItemDatabaseCSV.Instance != null
                ? ItemDatabaseCSV.Instance.GetItem(entry.RefId)
                : null;
            if (item == null) { HideUnbindable(entry, "item"); return; }

            string rar = string.IsNullOrEmpty(item.rarity) ? "Common" : item.rarity;

            SetRarityTile(rar);
            SetImage("tournament_image/Portrait",
                     item.thumbnailSprite != null ? item.thumbnailSprite : item.fullSprite);
            ResetPortraitRect();
            SetText("NameLabel", (item.name ?? string.Empty).ToUpperInvariant());

            // "RESTORES 50%" — the two existing keys ItemDetailPanel already uses, rather than a new
            // literal or a new key that would have to be kept in step with them.
            SetActive("DistRow", true);
            SetText("DistRow/Txt", $"{LocalizationManager.Get("ITEM_RESTORES")} {item.restorePercent}%");
            if (_iconItemRestore != null) SetImage("DistRow/Icon", _iconItemRestore);

            for (int i = 0; i <= 4; i++) SetActive($"StatRow_{i}", false);

            SetText("HMid", rar);
            SetActive("HLevel", false);
        }

        // ── Ticket variant (gacha_client_real_pull §4.3) ─────────────────────────
        //
        // A ticket has no stats and no level: it is an icon, a name and a count. Every StatRow and
        // the level chip are hidden and DistRow carries the quantity, exactly the shape BindItem
        // uses for a restore percentage.
        //
        // This is the SAME method the shop's `category = ticket` rows bind with (spec B §5.2), so
        // a ticket bought in the store and a ticket won from a pull render identically by
        // construction rather than by two lists of paths agreeing.

        /// <summary>
        /// Bind a ticket. <paramref name="ticketTypeId"/> is the <c>ticket_types</c> id as a
        /// decimal string — the same string the grants queue uses as a ticket grant's <c>ref_id</c>.
        /// </summary>
        public void BindTicket(string ticketTypeId, int quantity)
        {
            if (!int.TryParse(ticketTypeId, out int id))
            {
                Debug.LogError($"[GeneralShopCard] Ticket ref '{ticketTypeId}' is not an integer " +
                               "ticket_types id — the card cannot be bound.");
                gameObject.SetActive(false);
                return;
            }

            var type = TicketTypeCatalog.Get(id);
            if (type == null)
            {
                Debug.LogError($"[GeneralShopCard] Ticket type {id} is not published in this build — " +
                               "hiding the card.");
                gameObject.SetActive(false);
                return;
            }

            // Tickets carry no rarity of their own. Common keeps the frame the shared tile expects
            // rather than leaving whatever the previous bind on this instance left behind.
            SetRarityTile("Common");

            var icon = Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl, type.IconUrl)
                    ?? (string.IsNullOrWhiteSpace(type.IconSprite)
                            ? null
                            : Resources.Load<Sprite>("Art/Gacha/Tickets/" + type.IconSprite.Trim()))
                    ?? Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl);
            if (icon != null) SetImage("tournament_image/Portrait", icon);
            ResetPortraitRect();

            SetText("NameLabel", (type.DisplayName ?? string.Empty).ToUpperInvariant());

            SetActive("DistRow", true);
            SetText("DistRow/Txt", "×" + Mathf.Max(1, quantity));

            for (int i = 0; i <= 4; i++) SetActive($"StatRow_{i}", false);

            SetText("HMid", string.Empty);
            SetActive("HLevel", false);

            ConstrainName();
            HidePriceAndBuy();
        }

        /// <summary>
        /// Bind a ball / character / item for DISPLAY only — no price, no BUY, nothing interactable
        /// (gacha_client_real_pull §4.3).
        ///
        /// <para>
        /// It routes through the SAME per-category binders <see cref="Bind"/> uses, on a synthetic
        /// entry, rather than through a second set of paths: a prize card and a shop card of the
        /// same thing must not be able to drift apart. What it deliberately does NOT do is call
        /// <c>BindPrice</c> or <c>WireBuy</c> — a prize has no price, and a BUY on it would be a
        /// purchase of something the player already owns.
        /// </para>
        /// </summary>
        public void BindForDisplay(ShopCategory category, string refId)
        {
            var entry = new ShopCatalogEntry
            {
                EntryId  = "prize:" + refId,
                Category = category,
                RefId    = refId,
                RpCost   = 0,
            };

            Entry = entry;
            _isBall = category == ShopCategory.Ball;

            switch (category)
            {
                case ShopCategory.Ball:      BindBall(entry);      break;
                case ShopCategory.Character: BindCharacter(entry); break;
                case ShopCategory.Item:      BindItem(entry);      break;
                default:                     BindClub(entry);      break;
            }

            ConstrainName();
            HidePriceAndBuy();
        }

        /// <summary>The price box and the BUY button, hidden the way the club prize card hides its
        /// action row — by path, on the instance, leaving the prefab untouched.</summary>
        private void HidePriceAndBuy()
        {
            SetActive("PriceBox", false);
            SetActive("CtaGoldButton", false);

            foreach (var btn in GetComponentsInChildren<Button>(includeInactive: true))
            {
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
            }
        }

        // ── Price ───────────────────────────────────────────────────────────────

        private static readonly Color PriceNavy = new Color32(0x00, 0x1E, 0x39, 0xFF);

        private void BindPrice(ShopCatalogEntry entry)
        {
            var box     = Find("PriceBox")?.GetComponent<Image>();
            var orig    = Find("PriceBox/Orig");
            var saleBg  = Find("PriceBox/SaleBG");
            var saleImg = saleBg != null ? saleBg.GetComponent<Image>() : null;
            var saleNum = Find("PriceBox/SaleBG/Sale/Num")?.GetComponent<TextMeshProUGUI>();

            var saleRt = saleBg as RectTransform;

            if (entry.HasSale)
            {
                // white box: struck original (dark) on top, navy "pay" price (white) in the bottom band.
                if (box != null)  box.color = Color.white;
                if (orig != null) orig.gameObject.SetActive(true);
                var origNum = Find("PriceBox/Orig/Num")?.GetComponent<TextMeshProUGUI>();
                if (origNum != null) { origNum.text = entry.RpCost.ToString("N0"); origNum.fontStyle = FontStyles.Strikethrough; }
                if (saleBg != null)  saleBg.gameObject.SetActive(true);
                if (saleImg != null) saleImg.color = PriceNavy;
                if (saleNum != null) saleNum.text = entry.SaleRpCost.ToString("N0");
                if (saleRt != null)   // restore the template's bottom band
                {
                    saleRt.anchorMin = new Vector2(0, 0); saleRt.anchorMax = new Vector2(1, 0);
                    saleRt.sizeDelta = new Vector2(-6, 84); saleRt.anchoredPosition = new Vector2(0, 3);
                }
            }
            else
            {
                // no discount: the whole box is the navy "pay" chip, price CENTERED in the square.
                if (box != null)  box.color = PriceNavy;
                if (orig != null) orig.gameObject.SetActive(false);
                if (saleBg != null)  saleBg.gameObject.SetActive(true);
                if (saleImg != null) saleImg.color = new Color(0, 0, 0, 0); // transparent — box already navy
                if (saleNum != null) saleNum.text = entry.RpCost.ToString("N0");
                if (saleRt != null)  // fill the box so the center-anchored price sits in the middle
                {
                    saleRt.anchorMin = new Vector2(0, 0); saleRt.anchorMax = new Vector2(1, 1);
                    saleRt.offsetMin = Vector2.zero; saleRt.offsetMax = Vector2.zero;
                }
            }
        }

        // ── BUY ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// The card's BUY control, so the controller can show the server round-trip on the button the
        /// player actually tapped (transaction_feedback §3.1). Resolved by the same path
        /// <see cref="WireBuy"/> uses rather than cached, because <c>Bind</c> can run before the
        /// template's children are addressable and a stale cache would silently do nothing.
        /// </summary>
        public Button BuyButton => Find("CtaGoldButton")?.GetComponent<Button>();

        /// <summary>The BUY label, for the same reason. See <see cref="BuyButton"/>.</summary>
        public TextMeshProUGUI BuyLabel => Find("CtaGoldButton/PlayLable")?.GetComponent<TextMeshProUGUI>();

        private void WireBuy(ShopCatalogEntry entry)
        {
            var btn   = BuyButton;
            var label = BuyLabel;
            if (btn == null) return;

            btn.onClick.RemoveAllListeners();

            // Clubs and characters are UNIQUE, so an owned one shows a disabled OWNED chip. Balls and
            // items STACK — buying a second is the normal case — with ONE exception: a stackable the
            // player already holds an UNLIMITED (-1) supply of. Every add path leaves -1 alone, so a
            // sale would debit and deliver nothing (unlimited_stackable_refusal, 2026-08-27). Showing BUY
            // on a card that can only take the player's RP is the bug; OWNED is the truth.
            bool owned =
                (entry.Category == ShopCategory.Club &&
                 ClubManager.Instance != null && ClubManager.Instance.IsOwned(entry.RefId)) ||
                (entry.Category == ShopCategory.Character &&
                 CharacterManager.Instance != null && CharacterManager.Instance.IsOwned(entry.RefId)) ||
                (entry.Category == ShopCategory.Item &&
                 ItemManager.Instance != null &&
                 ItemManager.Instance.GetItemData(entry.RefId)?.IsUnlimited == true) ||
                (entry.Category == ShopCategory.Ball &&
                 BallManager.Instance != null &&
                 BallManager.Instance.GetBallData(entry.RefId)?.IsUnlimited == true);

            if (owned)
            {
                if (label != null) label.text = LocalizationManager.Get("BALL_OWNED");
                btn.interactable = false;
            }
            else
            {
                if (label != null) label.text = LocalizationManager.Get("GACHA_BUY");
                btn.interactable = true;
                btn.onClick.AddListener(() => OnBuyClicked?.Invoke(this));
            }
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private Transform Find(string path) => transform.Find(path);

        private void SetText(string path, string text)
        {
            var t = Find(path)?.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = text;
        }

        private void SetImage(string path, Sprite sprite)
        {
            var img = Find(path)?.GetComponent<Image>();
            if (img != null && sprite != null) img.sprite = sprite;
        }

        private void SetActive(string path, bool active)
        {
            var t = Find(path);
            if (t != null) t.gameObject.SetActive(active);
        }

        private void SetRarityTile(string rarityName)
        {
            var sprite = Resources.Load<Sprite>($"Rarities/{rarityName}");
            if (sprite == null) return;
            var tile = Find("tournament_image");
            if (tile == null) return;
            foreach (Transform child in tile)
                if (child.name == "RarityGrad")
                {
                    var img = child.GetComponent<Image>();
                    if (img != null) img.sprite = sprite;
                }
        }

        private static string RarityLetter(CharacterRarity r) => r switch
        {
            CharacterRarity.Common    => "C",
            CharacterRarity.Uncommon  => "U",
            CharacterRarity.Rare      => "R",
            CharacterRarity.Mythic    => "M",
            CharacterRarity.Legendary => "L",
            CharacterRarity.Supreme   => "S",
            _                         => "C"
        };

        private static int StartingLevel(CharacterRarity r) => r switch
        {
            CharacterRarity.Common    => 10,
            CharacterRarity.Uncommon  => 40,
            CharacterRarity.Rare      => 80,
            CharacterRarity.Mythic    => 120,
            CharacterRarity.Legendary => 160,
            CharacterRarity.Supreme   => 200,
            _                         => 10
        };
    }
}
