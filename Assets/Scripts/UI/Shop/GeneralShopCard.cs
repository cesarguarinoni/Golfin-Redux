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

            // A card instance is REUSED across categories. The description belongs to items and
            // tickets only, so it goes off here and the two binders that own it turn it back on —
            // the same idempotency argument BindPrice makes for PriceBox.
            ResetPerKindChrome();

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

            // The rows' space carries the item's description — the SAME copy the history tile
            // and the Item screen show, from the one helper, so the store cannot say something
            // different from the log (Cesar, 2026-09-14: the store card had none).
            SetDescription(GachaPrizeCardBinder.ItemDescription(item));

            SetText("HMid", rar);
            SetActive("HLevel", false);

            // The two HDiv pipes bracket "| rarity | level". An item has no level, so the SECOND
            // pipe was left standing at x=496 — straight through the word "Common", which is
            // wider than the 21 px HMid rect authored for a single club rarity letter. Hide that
            // one; the first pipe, between the name and the rarity, stays. (Same shape BindTicket
            // already handles — it hides both, because a ticket has neither.)
            int seen = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name != "HDiv") continue;
                if (++seen == 2) child.gameObject.SetActive(false);
            }
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

            // One ladder — see GachaTicketArt (polish_regressions_0909 R3 audit).
            var icon = GolfinRedux.UI.Gacha.GachaTicketArt.Resolve(type);
            if (icon != null) SetImage("tournament_image/Portrait", icon);
            ResetPortraitRect();

            SetText("NameLabel", (type.DisplayName ?? string.Empty).ToUpperInvariant());

            SetActive("DistRow", true);
            SetText("DistRow/Txt", "×" + Mathf.Max(1, quantity));

            for (int i = 0; i <= 4; i++) SetActive($"StatRow_{i}", false);

            // Same source as the prize card's ticket description — see BindItem.
            SetDescription(GachaPrizeCardBinder.TicketDescription(type));

            SetText("HMid", string.Empty);
            SetActive("HLevel", false);

            // The two `HDiv` pipes bracket the rarity/level block — "IRON 9 KLYRO | U | Lv 40/79".
            // A ticket has neither, so HMid is blank and HLevel is off, and leaving the dividers on
            // renders a bare "TICKET | |". They are two SIBLINGS SHARING ONE NAME, so `Find` (which
            // returns the first) cannot hide them both — hence the explicit child walk.
            //
            // Same correction the prize card already took on this exact shape: gacha_client_real_pull
            // was rejected for leaving a distance arc on a non-club prize (5d412a17d). `DistRow/Icon`
            // is that arc; BindItem swaps it for the restore glyph and BindCharacter hides the whole
            // row. A "×50" needs no icon at all.
            SetActive("DistRow/Icon", false);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "HDiv") child.gameObject.SetActive(false);
            }

            ConstrainName();

            // ⚠️ NO HidePriceAndBuy() HERE (gacha_ops_polish §5). It used to be — copied from
            // BindForDisplay's ending, which is the DISPLAY path and where it belongs — and it
            // made a ticket listing UNBUYABLE. `Bind` calls BindPrice/WireBuy AFTER this switch,
            // and neither re-activated a GameObject that had been switched off, so the first
            // ticket card rendered "TICKET ×50" with no price and no BUY at all. None of the other
            // four per-category binders hides anything; BindTicket is only ever reached from
            // `Bind`, i.e. the shop.
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
            ResetPerKindChrome();

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
        //
        // iap_plumbing (2026-09-15): a row may carry an RP price, an App Store product, or both,
        // and the plate draws whichever the store can honour RIGHT NOW. Reconciled against the
        // Figma Store page (`14287:32861`, cards 1-3) rather than the spec's prose:
        //
        //   RP-only   (`14287:32988`)  navy plate, coin 36 + number, gap 8 — today's plate.
        //   ¥-only    (`14287:32962`)  navy plate 180×76 (shorter), the localized price alone, the
        //                              plate + BUY re-centred as a group.
        //   dual      (`14287:33012`)  WHITE plate: coin + RP number in navy on top, navy band with
        //                              the localized ¥ price below; a discount is the red corner
        //                              badge (`14287:33138`) ONLY — no struck original.
        //
        // WITHHOLD RULE. A product id the store cannot sell right now (flag off, not connected,
        // product missing) draws as RP-only when there is an RP price; ¥-only rows and sandbox rows
        // never reach a card in that state — GeneralShopCatalog.ListedNow drops them first.

        private static readonly Color PriceNavy = new Color32(0x00, 0x1E, 0x39, 0xFF);

        private enum PriceMode { RpOnly, MoneyOnly, Dual }

        /// <summary>The mode the last <see cref="BindPrice"/> chose — read by the controller to route
        /// BUY, and by the tests.</summary>
        public bool ShowsMoneyPrice { get; private set; }
        public bool ShowsRpPrice { get; private set; } = true;

        // Authored geometry, captured on the first bind of this instance so every later bind can put
        // the ¥-only shift back (Bind is idempotent; a card is re-bound after a purchase).
        private bool _priceGeometryCaptured;
        private Vector2 _boxAuthoredPos, _boxAuthoredSize, _borderAuthoredPos, _borderAuthoredSize, _ctaAuthoredPos;
        private Color _origNumAuthoredColor = Color.white;
        private float _origNumAuthoredSize = 30f;
        private Color _origIconAuthoredColor = Color.white;   // authored 85 % alpha — the struck row's dimmed coin

        /// <summary>The ¥-only plate: node `14287:32962` is 180×76 against the RP plate's 142; the
        /// Unity plate is 160 (approved), so the same +2 keeps the pair's ratio.</summary>
        private const float MoneyOnlyPlateHeight = 78f;

        /// <summary>
        /// The price number, all three plates: node 30 px Rubik Medium (`14287:32991` / `33014` /
        /// `33017` / `32967`), whose digits render 21 px tall in the node render. The prefab shipped
        /// 34, which renders 26 px (+24 %) — measured on iap_frames/01_store_all.png vs
        /// reference/iap_pricing_screen.png, 2026-09-15. 28 renders 21–22. SemiBold stands in for
        /// Medium project-wide (there is no Rubik-Medium SDF asset); flagged in the fidelity table.
        /// </summary>
        private const float PriceNumberFontSize = 28f;

        private void BindPrice(ShopCatalogEntry entry)
        {
            // A card instance is reused, and BindForDisplay switches PriceBox off. Re-showing it
            // here — rather than trusting whatever the previous bind left — is what makes `Bind`
            // idempotent: the SHOP path shows a price, always, whatever this instance was last.
            SetActive("PriceBox", true);
            CaptureAuthoredPriceGeometry();

            bool hasRp    = entry.HasRpPrice;
            string moneyPrice = string.Empty;
            bool hasMoney = entry.HasStoreProduct &&
                            Golfin.Economy.IapService.TryGetLocalizedPrice(entry.StoreProductId, out moneyPrice);
            if (!hasMoney) moneyPrice = string.Empty;

            PriceMode mode = hasMoney ? (hasRp ? PriceMode.Dual : PriceMode.MoneyOnly) : PriceMode.RpOnly;
            ShowsMoneyPrice = hasMoney;
            ShowsRpPrice    = mode != PriceMode.MoneyOnly;

            ApplyPriceGeometry(mode);

            var box     = Find("PriceBox")?.GetComponent<Image>();
            var orig    = Find("PriceBox/Orig");
            var origNum = Find("PriceBox/Orig/Num")?.GetComponent<TextMeshProUGUI>();
            var saleBg  = Find("PriceBox/SaleBG");
            var saleImg = saleBg != null ? saleBg.GetComponent<Image>() : null;
            var saleNum = Find("PriceBox/SaleBG/Sale/Num")?.GetComponent<TextMeshProUGUI>();
            var saleRt  = saleBg as RectTransform;
            var origRt  = orig as RectTransform;

            // The struck-original row is the legacy sale treatment; put its authored look back
            // before any mode decides otherwise. The pay row's number is the node's size in every mode.
            if (origNum != null) { origNum.color = _origNumAuthoredColor; origNum.fontSize = _origNumAuthoredSize; }
            var origIcon = Find("PriceBox/Orig/RpIcon")?.GetComponent<Image>();
            if (origIcon != null) origIcon.color = _origIconAuthoredColor;
            if (saleNum != null) saleNum.fontSize = PriceNumberFontSize;
            SetActive("PriceBox/Orig/Strike", false);
            SetDiscountBadge(null);

            switch (mode)
            {
                case PriceMode.RpOnly when entry.HasSale:
                    // white box: struck original (dark) on top, navy "pay" price (white) in the bottom band.
                    if (box != null)  box.color = Color.white;
                    if (orig != null) orig.gameObject.SetActive(true);
                    if (origNum != null) { origNum.text = Rp(entry.RpCost); origNum.fontStyle = FontStyles.Normal; }
                    if (saleBg != null)  saleBg.gameObject.SetActive(true);
                    if (saleImg != null) saleImg.color = PriceNavy;
                    if (saleNum != null) saleNum.text = Rp(entry.SaleRpCost);
                    SetActive("PriceBox/SaleBG/Sale/RpIcon", true);
                    if (origRt != null) origRt.anchoredPosition = new Vector2(0f, -16f);
                    CenterPriceRow("PriceBox/Orig", StruckIconPx, StruckIconGap);
                    CenterPriceRow("PriceBox/SaleBG/Sale", PriceIconPx, PriceIconGap);
                    StrikeOriginal(origNum);                  // after centring — it reads the coin/number x
                    if (saleRt != null)   // restore the template's bottom band
                    {
                        saleRt.anchorMin = new Vector2(0, 0); saleRt.anchorMax = new Vector2(1, 0);
                        saleRt.sizeDelta = new Vector2(-6, 84); saleRt.anchoredPosition = new Vector2(0, 3);
                    }
                    break;

                case PriceMode.RpOnly:
                    // no discount: the whole box is the navy "pay" chip, price CENTERED in the square.
                    if (box != null)  box.color = PriceNavy;
                    if (orig != null) orig.gameObject.SetActive(false);
                    if (saleBg != null)  saleBg.gameObject.SetActive(true);
                    if (saleImg != null) saleImg.color = new Color(0, 0, 0, 0); // transparent — box already navy
                    if (saleNum != null) saleNum.text = Rp(entry.RpCost);
                    SetActive("PriceBox/SaleBG/Sale/RpIcon", true);
                    CenterPriceRow("PriceBox/SaleBG/Sale", PriceIconPx, PriceIconGap);
                    FillBoxWithBand(saleRt);
                    break;

                case PriceMode.MoneyOnly:
                    // node 14287:32962 — the same navy chip, shorter, the localized price alone. No
                    // coin: money is not RP, and the plate never says "RP" or "¥" in words either.
                    if (box != null)  box.color = PriceNavy;
                    if (orig != null) orig.gameObject.SetActive(false);
                    if (saleBg != null)  saleBg.gameObject.SetActive(true);
                    if (saleImg != null) saleImg.color = new Color(0, 0, 0, 0);
                    if (saleNum != null) saleNum.text = moneyPrice;
                    SetActive("PriceBox/SaleBG/Sale/RpIcon", false);
                    CenterPriceText("PriceBox/SaleBG/Sale");
                    FillBoxWithBand(saleRt);
                    break;

                case PriceMode.Dual:
                    // node 14287:33012 — white plate, coin + RP price in NAVY on top (no strike), the
                    // navy band below carries the ¥ price in white. The plate splits 80/80 the way the
                    // node's splits 76/76.
                    if (box != null)  box.color = Color.white;
                    if (orig != null) orig.gameObject.SetActive(true);
                    if (origNum != null)
                    {
                        origNum.text = Rp(entry.EffectiveRpCost);
                        origNum.fontStyle = FontStyles.Normal;
                        origNum.color = PriceNavy;
                        origNum.fontSize = PriceNumberFontSize;
                    }
                    if (origIcon != null) origIcon.color = Color.white;   // a live price, not a struck one
                    if (saleBg != null)  saleBg.gameObject.SetActive(true);
                    if (saleImg != null) saleImg.color = PriceNavy;
                    if (saleNum != null) saleNum.text = moneyPrice;
                    SetActive("PriceBox/SaleBG/Sale/RpIcon", false);
                    // Top row centred in the upper half (box 160 → 80; the row is 54 tall).
                    if (origRt != null) origRt.anchoredPosition = new Vector2(0f, -13f);
                    CenterPriceRow("PriceBox/Orig", PriceIconPx, PriceIconGap);
                    CenterPriceText("PriceBox/SaleBG/Sale");
                    if (saleRt != null)
                    {
                        saleRt.anchorMin = new Vector2(0, 0); saleRt.anchorMax = new Vector2(1, 0);
                        saleRt.sizeDelta = new Vector2(-6, 77); saleRt.anchoredPosition = new Vector2(0, 3);
                    }
                    // Discount = the corner badge ONLY (Cesar 2026-09-14): no room for a struck
                    // original on a plate that already holds two prices.
                    if (entry.HasSale) SetDiscountBadge(DiscountLabel(entry.RpCost, entry.SaleRpCost));
                    break;
            }
        }

        /// <summary>"-25%" for list 600 / sale 450. Rounded to the nearest whole percent, never "-0%".</summary>
        public static string DiscountLabel(int listRp, int saleRp)
        {
            if (listRp <= 0 || saleRp >= listRp) return string.Empty;
            int pct = Mathf.Max(1, Mathf.RoundToInt(100f * (listRp - saleRp) / listRp));
            return "-" + pct.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>The bottom band stretched over the whole box, so a centre-anchored price sits in
        /// the middle of the square (the no-sale and ¥-only plates).</summary>
        private static void FillBoxWithBand(RectTransform saleRt)
        {
            if (saleRt == null) return;
            saleRt.anchorMin = new Vector2(0, 0); saleRt.anchorMax = new Vector2(1, 1);
            saleRt.offsetMin = Vector2.zero; saleRt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// The red `-N%` pill at the plate's top-right corner (node `14287:33138`: 109×47, sits 8 px
        /// past the plate's right edge and 18 px above its top). AUTHORED on both card prefabs as
        /// <c>PriceBox/DiscountBadge</c> (sprite `S_DiscountBadge`, baked from the node's tokens by
        /// Docs/Scripts/make_discount_badge.py) and only shown/labelled here. Null/empty hides it.
        /// </summary>
        private void SetDiscountBadge(string label)
        {
            var badge = Find("PriceBox/DiscountBadge");
            if (badge == null) return;
            bool show = !string.IsNullOrEmpty(label);
            var tmp = badge.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = show ? label : string.Empty;
            badge.gameObject.SetActive(show);
        }

        private void CaptureAuthoredPriceGeometry()
        {
            if (_priceGeometryCaptured) return;
            var box    = Find("PriceBox") as RectTransform;
            var border = Find("PriceBorder") as RectTransform;
            var cta    = Find("CtaGoldButton") as RectTransform;
            if (box == null) return;
            _boxAuthoredPos  = box.anchoredPosition;  _boxAuthoredSize  = box.sizeDelta;
            if (border != null) { _borderAuthoredPos = border.anchoredPosition; _borderAuthoredSize = border.sizeDelta; }
            if (cta != null) _ctaAuthoredPos = cta.anchoredPosition;
            var origNum = Find("PriceBox/Orig/Num")?.GetComponent<TextMeshProUGUI>();
            if (origNum != null) { _origNumAuthoredColor = origNum.color; _origNumAuthoredSize = origNum.fontSize; }
            var origIcon = Find("PriceBox/Orig/RpIcon")?.GetComponent<Image>();
            if (origIcon != null) _origIconAuthoredColor = origIcon.color;
            _priceGeometryCaptured = true;
        }

        /// <summary>
        /// The ¥-only plate is SHORTER (node: 76 against the RP plate's 142) and the node keeps the
        /// plate + BUY group vertically centred, so both move: the plate shrinks to
        /// <see cref="MoneyOnlyPlateHeight"/> and the pair is re-centred on the authored group's own
        /// centre. Every other mode puts the authored geometry back.
        /// </summary>
        private void ApplyPriceGeometry(PriceMode mode)
        {
            var box    = Find("PriceBox") as RectTransform;
            var border = Find("PriceBorder") as RectTransform;
            var cta    = Find("CtaGoldButton") as RectTransform;
            if (box == null || !_priceGeometryCaptured) return;

            if (mode != PriceMode.MoneyOnly)
            {
                box.anchoredPosition = _boxAuthoredPos;  box.sizeDelta = _boxAuthoredSize;
                if (border != null) { border.anchoredPosition = _borderAuthoredPos; border.sizeDelta = _borderAuthoredSize; }
                if (cta != null) cta.anchoredPosition = _ctaAuthoredPos;
                return;
            }

            // Authored group (top-left anchored, y grows downward as -y): plate top → BUY bottom.
            float boxTop     = -_boxAuthoredPos.y;
            float boxBottom  = boxTop + _boxAuthoredSize.y;
            float ctaTop     = cta != null ? -_ctaAuthoredPos.y : boxBottom;
            float ctaHeight  = cta != null ? cta.sizeDelta.y : 0f;
            float gap        = ctaTop - boxBottom;
            float centre     = (boxTop + ctaTop + ctaHeight) * 0.5f;
            float group      = MoneyOnlyPlateHeight + gap + ctaHeight;
            float newBoxTop  = centre - group * 0.5f;

            box.anchoredPosition = new Vector2(_boxAuthoredPos.x, -newBoxTop);
            box.sizeDelta        = new Vector2(_boxAuthoredSize.x, MoneyOnlyPlateHeight);
            if (border != null)
            {
                float inset = _borderAuthoredPos.y - _boxAuthoredPos.y;            // +3 authored
                float grow  = _borderAuthoredSize.y - _boxAuthoredSize.y;          // +6 authored
                border.anchoredPosition = new Vector2(_borderAuthoredPos.x, -newBoxTop + inset);
                border.sizeDelta        = new Vector2(_borderAuthoredSize.x, MoneyOnlyPlateHeight + grow);
            }
            if (cta != null) cta.anchoredPosition = new Vector2(_ctaAuthoredPos.x, -(newBoxTop + MoneyOnlyPlateHeight + gap));
        }

        /// <summary>
        /// An RP amount as PLAIN DIGITS — "1500", never "1,500" or "1.500".
        ///
        /// <para>This was <c>ToString("N0")</c>, which groups thousands with the CURRENT culture's
        /// separator: a comma in the Editor, a dot on a device set to ja-JP / most of Europe. So
        /// the same card read "1,500" in every capture and "1.500" in Cesar's hand (2026-09-14),
        /// and no separator is wanted on RP at all. <c>int.ToString()</c> with no format never
        /// groups, in any culture.</para>
        /// </summary>
        private static string Rp(int amount) => amount.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The coin on a price row — node `14292:33345/33346/33347`: 36 px, 8 px before the
        /// number (the prefab shipped 30/6 and 26/6; the spec's fidelity table pins the node).</summary>
        private const float PriceIconPx  = 36f;
        private const float PriceIconGap = 8f;

        /// <summary>The struck original on a legacy RP sale keeps its authored 26 px coin and 6 px gap —
        /// that row is not in the IAP node and "single-priced rows keep today's struck-orig treatment".</summary>
        private const float StruckIconPx  = 26f;
        private const float StruckIconGap = 6f;

        /// <summary>
        /// Centre the coin AND the number as one group in a price row.
        ///
        /// <para>Both are centre-anchored with a LEFT pivot at fixed authored x offsets, tuned for
        /// a five-character "2,000". The number's rect is centred; the coin is not part of it, so
        /// a two-digit price sat left of the box's centre with dead space on the right (Cesar,
        /// 2026-09-14: "only the numbers seem to be centered"). The group's width is the coin plus
        /// the gap plus the RENDERED number width, and its left edge is half that to the left of
        /// centre — for every price length, on both the struck row and the pay row.</para>
        /// </summary>
        private void CenterPriceRow(string rowPath, float iconPx, float gap)
        {
            var icon = Find(rowPath + "/RpIcon") as RectTransform;
            var num  = Find(rowPath + "/Num")?.GetComponent<TextMeshProUGUI>();
            if (icon == null || num == null) return;

            icon.sizeDelta = new Vector2(iconPx, iconPx);

            var numRt = (RectTransform)num.transform;
            float textW  = num.preferredWidth;
            float groupW = icon.sizeDelta.x + gap + textW;
            float left   = -groupW * 0.5f;

            icon.anchoredPosition  = new Vector2(left, icon.anchoredPosition.y);
            numRt.anchoredPosition = new Vector2(left + icon.sizeDelta.x + gap, numRt.anchoredPosition.y);
            // The number's rect is sized to its text so nothing to its right is a phantom
            // margin the centring would otherwise have to include.
            numRt.sizeDelta = new Vector2(textW, numRt.sizeDelta.y);
        }

        /// <summary>The number alone, centred — the ¥ rows have no coin.</summary>
        private void CenterPriceText(string rowPath)
        {
            var num = Find(rowPath + "/Num")?.GetComponent<TextMeshProUGUI>();
            if (num == null) return;
            var numRt = (RectTransform)num.transform;
            float textW = num.preferredWidth;
            numRt.anchoredPosition = new Vector2(-textW * 0.5f, numRt.anchoredPosition.y);
            numRt.sizeDelta = new Vector2(textW, numRt.sizeDelta.y);
        }

        /// <summary>
        /// The strike across the ORIGINAL price on a sale — a real 4 px bar from the coin's left
        /// edge to the end of the digits, in the digits' own colour.
        ///
        /// <para>It replaces <c>FontStyles.Strikethrough</c>, which was measured on the shipped
        /// frame at 1170 wide as a ONE-pixel line at 73 % of the glyph height (y=640 across digits
        /// spanning 624–645), stopping short of the coin. TMP's strike is placed by the font
        /// asset's underline metrics, not by the digits' visual centre, and its thickness is the
        /// font's underline thickness scaled by the 30 px size — a hairline on 22 px-tall SemiBold
        /// numerals, which is why the price did not read as slashed (Cesar, 2026-09-14). The bar is
        /// AUTHORED in both card prefabs (net-new; there is no strike element in the family to
        /// clone) and only sized here, so nothing is built at runtime per card.</para>
        /// </summary>
        private void StrikeOriginal(TextMeshProUGUI origNum)
        {
            var strike = Find("PriceBox/Orig/Strike") as RectTransform;
            var icon   = Find("PriceBox/Orig/RpIcon") as RectTransform;
            if (strike == null || icon == null || origNum == null) return;

            var numRt = (RectTransform)origNum.transform;
            // Left edge of the coin → right edge of the rendered digits. Both siblings are
            // centre-anchored with a left pivot, so anchoredPosition.x IS each one's left edge in
            // the same frame the strike uses.
            float left  = icon.anchoredPosition.x;
            float right = numRt.anchoredPosition.x + origNum.preferredWidth;

            strike.anchoredPosition = new Vector2(left, numRt.anchoredPosition.y);
            strike.sizeDelta        = new Vector2(Mathf.Max(0f, right - left), strike.sizeDelta.y);

            var img = strike.GetComponent<Image>();
            if (img != null) img.color = origNum.color;
            strike.gameObject.SetActive(true);
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

        // ── What the payment modal binds from (iap_plumbing) ──────────────────
        //
        // Read back off the bound card rather than re-resolved from the databases, so the modal can
        // never name or picture a row differently from the card the player just tapped.

        /// <summary>The card's rendered name ("GOLD TICKET", "IRON 9 KLYRO").</summary>
        public string DisplayName => Find("NameLabel")?.GetComponent<TextMeshProUGUI>()?.text ?? string.Empty;

        /// <summary>The card's tile art — the ticket icon, the club portrait.</summary>
        public Sprite TileSprite => Find("tournament_image/Portrait")?.GetComponent<Image>()?.sprite;

        /// <summary>The card's description block, empty for kinds that have none (clubs, balls).</summary>
        public string Description
        {
            get
            {
                var d = Find("Desc");
                var tmp = d != null && d.gameObject.activeSelf ? d.GetComponent<TextMeshProUGUI>() : null;
                return tmp != null ? tmp.text : string.Empty;
            }
        }

        private void WireBuy(ShopCatalogEntry entry)
        {
            // Same reason as BindPrice's first line: the SHOP path always has a BUY control.
            SetActive("CtaGoldButton", true);

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

        /// <summary>
        /// The description block in the stat rows' space, to the right of the art. AUTHORED on
        /// GeneralShopCard_Club.prefab as <c>Desc</c> (230,−100 · 490×144 · auto-size 14..24) —
        /// the template every non-ball kind rides. A ball card has no such object and this is
        /// a silent no-op there, which is correct: a ball's rows are its stat bars.
        /// </summary>
        /// <summary>Everything a per-category binder may switch OFF and no other binder switches
        /// back on — the description block and the two header pipes. Run at the top of both
        /// dispatchers so a re-bound instance starts from the prefab's state, not the last
        /// category's.</summary>
        private void ResetPerKindChrome()
        {
            SetDescription(string.Empty);
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name == "HDiv") child.gameObject.SetActive(true);
            }
        }

        private void SetDescription(string text)
        {
            var t = Find("Desc");
            if (t == null) return;
            bool show = !string.IsNullOrWhiteSpace(text);
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = show ? text : string.Empty;
            t.gameObject.SetActive(show);
        }

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
