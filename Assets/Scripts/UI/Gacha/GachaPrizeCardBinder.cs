// Assets/Scripts/UI/Gacha/GachaPrizeCardBinder.cs
// gacha_client_real_pull §4.3 — the ONE prize-card binder.
//
// It was GachaPrizesScreenController.BindCard, a static the reveal modal reached across to call.
// It moves out because a prize is no longer always a club: the pull can pay a ball, a character,
// an item or a ticket, and picking the prefab is now part of binding. Keeping ONE binder is what
// makes the card the player sees pop out of the bag and the card on the Prizes screen the same
// object built by the same code — the reason it was shared in the first place.
//
// ONE PREFAB, EVERY KIND: BagClubCard.prefab.
//
// ⚠️ THIS REPLACES THE SPEC'S INSTRUCTION, ON CESAR'S CALL (2026-08-31). §4.3 said to render a
// non-club prize on the Rewards-Center shop card and, if it did not fit the slot, to scale it to
// fit. It does not fit — 978x274 into 183x410 is a uniform 0.19 — and the result was legible as a
// shape and not as text. Cesar rejected it on sight: "They should be the same size and shape as
// club." So every kind now draws on the CLUB card, through BagClubCard.InitializePrize.
//
// That is not a new design either. `GachaHistoryRowBall.prefab` has nested a BagClubCard and bound
// BALL data into it since gacha_history Stage 1 — portrait, name, an "x3" badge, the five stat
// lanes re-pointed at the ball's stats. This is that pattern, given a name on the card itself so
// four kinds share one shell instead of four hand-bound copies of its child paths.
#nullable enable
using Golfin.Inventory;
using Golfin.Roster;
using GolfinRedux.UI.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>Builds and binds the card for one prize, whatever kind it is.</summary>
    public static class GachaPrizeCardBinder
    {
        /// <summary>Paths inside BagClubCard.prefab whose action row is hidden on a prize card.</summary>
        private static readonly string[] ClubActionButtonPaths =
        {
            "Mask/Background/ButtonRow/LevelUpBtn",
            "Mask/Background/ButtonRow/RepairBtn",
            "SwapBtn",
        };

        /// <summary>Ball stats are 0..10, not the club scale.</summary>
        private const int BallStatMax = 10;

        /// <summary>Character stats share the club bar scale closely enough to read; the roster's
        /// own caps are rarity-dependent and a prize card is not a stat sheet.</summary>
        private const int CharacterStatMax = 50;

        /// <summary>
        /// Instantiate the card for <paramref name="record"/> under <paramref name="parent"/> and
        /// bind it. EVERY kind uses <paramref name="clubPrefab"/>, so a mixed pull is a grid of
        /// identically-shaped cards.
        /// </summary>
        /// <param name="clubPrefab">The BagClubCard prefab. The reveal modal has it serialized; the
        /// Prizes screen reads it off one of its authored grid cards.</param>
        /// <param name="siblingIndex">Where in the parent to insert it, so a spawned card takes the
        /// place of the authored card it replaces instead of being appended after all of them.</param>
        public static GameObject? Instantiate(PrizeRecord record, Transform parent, GameObject? clubPrefab,
                                              int siblingIndex = -1)
        {
            if (clubPrefab == null)
            {
                Debug.LogError($"[GachaPrizeCardBinder] No BagClubCard prefab to build the " +
                               $"{record.Kind} prize '{record.RefId}' on — the prize is granted but " +
                               "cannot be shown.");
                return null;
            }

            var go = Object.Instantiate(clubPrefab, parent);
            go.name = "PrizeCard_" + record.Kind + "_" + record.RefId;

            // ⚠️ THE TEMPLATE IS OFTEN AN INACTIVE SCENE OBJECT, NOT A PROJECT PREFAB. The Prizes
            // screen passes the authored club card of the slot this prize replaces, and it hides
            // that card BEFORE cloning it — so the clone is born inactive and the slot renders
            // empty. Measured: slots 6 and 7 of a mixed x10 were blank, both the club card and its
            // replacement inactive. Instantiate copies activeSelf; only this makes the clone show.
            go.SetActive(true);

            if (siblingIndex >= 0) go.transform.SetSiblingIndex(siblingIndex);

            Bind(go, record);
            return go;
        }

        /// <summary>
        /// Bind an already-instantiated card GO to <paramref name="record"/>. Dispatches on the
        /// component the GO actually carries, so the same call works for a reveal card, a grid slot
        /// and a history row without any of them knowing which prefab they got.
        /// </summary>
        public static void Bind(GameObject? cardGo, PrizeRecord record)
        {
            if (cardGo == null) return;

            var club = cardGo.GetComponent<BagClubCard>();
            if (club == null)
            {
                Debug.LogWarning($"[GachaPrizeCardBinder] '{cardGo.name}' carries no BagClubCard — " +
                                 "nothing to bind.");
                return;
            }

            if (record.Kind == PrizeRecord.KindClub) BindClubCard(club, record);
            else                                    BindOtherKind(club, record);

            HideActionRow(club);
            ApplyDupePill(club.transform, record);
        }

        // ── Club ───────────────────────────────────────────────────────────────

        private static void BindClubCard(BagClubCard card, PrizeRecord record)
        {
            var template = ClubDatabaseCSV.Instance?.GetClub(record.RefId);
            if (template == null)
            {
                // The prize IS granted — the server rolled it against its own published pool. This
                // build simply cannot draw it, which means the club row has not been exported into
                // a build yet.
                Debug.LogWarning($"[GachaPrizeCardBinder] Club not found in this build: {record.RefId}. " +
                                 "The prize is granted server-side; the card cannot render it.");
                return;
            }

            // A card re-bound from a ball back to a club has hidden rows to put back.
            card.RestoreClubRows();

            var playerClub = new PlayerClubData
            {
                clubId            = record.RefId,
                currentLevel      = 1,
                currentDurability = template.maxDurability,
                maxDurability     = template.maxDurability,
            };

            card.Initialize(playerClub, template, "");
        }

        // ── Ball / character / item / ticket, on the SAME card ─────────────────

        /// <summary>
        /// Draw a non-club prize on the club card. Each kind fills the same five slots — portrait,
        /// name, rarity frame, badge, one free-text line — and supplies stat lanes only when it has
        /// stats worth a bar.
        ///
        /// <para>
        /// The RARITY is the SERVER's, off the record, never the local database's: a prize rolled
        /// at a tier this build has not seen still shows the frame it was actually rolled at.
        /// </para>
        /// </summary>
        private static void BindOtherKind(BagClubCard card, PrizeRecord record)
        {
            string qty = record.Quantity > 1 ? "x" + record.Quantity : string.Empty;

            switch (record.Kind)
            {
                case PrizeRecord.KindBall:
                {
                    var ball = BallDatabaseCSV.Instance?.GetBall(record.RefId);
                    if (ball == null) { Missing(record, "ball"); return; }

                    card.InitializePrize(new BagClubCard.PrizeView(
                        ball.thumbnailSprite != null ? ball.thumbnailSprite : ball.fullSprite,
                        BuildName(ball.name, ball.brand),
                        record.Rarity,
                        badge:   qty,
                        detail:  string.Empty,
                        stats:   new[] { ball.power, ball.rebound, ball.windResistance, ball.roll, ball.spin },
                        statMax: BallStatMax));
                    return;
                }

                case PrizeRecord.KindCharacter:
                {
                    var ch = Golfin.Roster.CharacterDatabaseCSV.Instance?.GetCharacter(record.RefId);
                    if (ch == null) { Missing(record, "character"); return; }

                    card.InitializePrize(new BagClubCard.PrizeView(
                        ch.portraitSprite,
                        BuildName(ch.characterName, ch.characterLastName),
                        record.Rarity,
                        badge:   qty,
                        detail:  string.Empty,
                        stats:   new[] { ch.baseStrength, ch.baseClubControl, ch.baseRecovery, ch.baseStamina },
                        statMax: CharacterStatMax));
                    return;
                }

                case PrizeRecord.KindItem:
                {
                    var item = ItemDatabaseCSV.Instance?.GetItem(record.RefId);
                    if (item == null) { Missing(record, "item"); return; }

                    // An item has ONE number worth showing, and it goes on the free-text line —
                    // the same two keys ItemDetailPanel and the shop card already use for it.
                    card.InitializePrize(new BagClubCard.PrizeView(
                        item.thumbnailSprite != null ? item.thumbnailSprite : item.fullSprite,
                        (item.name ?? string.Empty).ToUpperInvariant(),
                        record.Rarity,
                        badge:  qty,
                        detail: $"{LocalizationManager.Get("ITEM_RESTORES")} {item.restorePercent}%",
                        stats:  null));
                    return;
                }

                case PrizeRecord.KindTicket:
                {
                    if (!int.TryParse(record.RefId, out int id)) { Missing(record, "ticket"); return; }
                    var type = TicketTypeCatalog.Get(id);
                    if (type == null) { Missing(record, "ticket"); return; }

                    var icon = Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl, type.IconUrl)
                            ?? (string.IsNullOrWhiteSpace(type.IconSprite)
                                    ? null
                                    : Resources.Load<Sprite>("Art/Gacha/Tickets/" + type.IconSprite.Trim()))
                            ?? Golfin.CatalogArt.CatalogArtCache.Cached(type.IconUrl);

                    card.InitializePrize(new BagClubCard.PrizeView(
                        icon,
                        (type.DisplayName ?? string.Empty).ToUpperInvariant(),
                        record.Rarity,
                        badge:  qty,
                        detail: string.Empty,
                        stats:  null));
                    return;
                }

                default:
                    Missing(record, record.Kind);
                    return;
            }
        }

        /// <summary>
        /// Two lines when there is a second part, one when there is not — the shape the club
        /// binding gives a name.
        ///
        /// <para>
        /// A second part EQUAL to the first is dropped, not printed twice. `ball_golfin` is named
        /// "Golfin" by brand "GOLFIN", and the card read "GOLFIN / GOLFIN".
        /// </para>
        /// </summary>
        private static string BuildName(string? first, string? second)
        {
            string a = (first ?? string.Empty).Trim().ToUpperInvariant();
            string b = (second ?? string.Empty).Trim().ToUpperInvariant();
            return string.IsNullOrEmpty(b) || b == a ? a : a + "\n" + b;
        }

        /// <summary>The prize was granted server-side but this build has no row for it. Loud, and
        /// the card is hidden rather than left showing the previous prize.</summary>
        private static void Missing(PrizeRecord record, string kind)
            => Debug.LogWarning($"[GachaPrizeCardBinder] {kind} '{record.RefId}' is not in this " +
                                "build's database. The prize IS granted server-side; the card " +
                                "cannot render it.");

        /// <summary>Display-only. Hiding the action row is not cosmetic tidying: the Prizes screen's
        /// grid cards ship with LevelUpBtn / RepairBtn / SwapBtn DEACTIVATED in the prefab, while a
        /// fresh BagClubCard instance (which is what the reveal modal spawns) has them active — so
        /// the same prize rendered two different ways depending on where it was shown. Doing it here,
        /// in the one shared binder, is what makes them agree, and it matches the Figma reveal card
        /// (13997:4503), which has no action row.</summary>
        private static void HideActionRow(BagClubCard card)
        {
            foreach (var n in ClubActionButtonPaths)
            {
                var t = card.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            foreach (var btn in card.GetComponentsInChildren<Button>(includeInactive: true))
                btn.interactable = false;
        }

        // ── The duplicate pill ─────────────────────────────────────────────────

        /// <summary>Name of the pill object this adds. Looked up before creating one so a re-bind
        /// (the Prizes screen re-binds its ten slots on every open) reuses it.</summary>
        private const string DupePillName = "GachaDupePill";

        /// <summary>
        /// A duplicate paid RP instead of the prize, so the card says so: a small gold pill reading
        /// "+120 RP" at the top-right.
        ///
        /// <para>
        /// It is BUILT rather than cloned because neither prefab family carries a pill to clone —
        /// the shop card's <c>Popular</c>/<c>Offer</c> flags are documented as "v1 unused" and no
        /// pill object was ever authored for them (verified against
        /// <c>Resources/Prefabs/Shop/GeneralShopCard_Club.prefab</c>). Rule 19 asks for provenance
        /// on a MANDATED clone; there is nothing here to clone, so it is stated plainly instead of
        /// a clone being claimed. It is deliberately tiny — one Image, one TMP, no layout group —
        /// so the card it sits on is untouched.
        /// </para>
        /// </summary>
        private static void ApplyDupePill(Transform card, PrizeRecord record)
        {
            var existing = card.Find(DupePillName);

            if (!record.IsDupe || record.DupeRp <= 0)
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            GameObject pill;
            TextMeshProUGUI? label;

            if (existing != null)
            {
                pill  = existing.gameObject;
                pill.SetActive(true);
                label = pill.GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);
            }
            else
            {
                pill = new GameObject(DupePillName, typeof(RectTransform), typeof(Image));
                pill.transform.SetParent(card, worldPositionStays: false);

                var rt = (RectTransform)pill.transform;
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot     = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-8f, -8f);
                rt.sizeDelta = new Vector2(120f, 40f);

                var bg = pill.GetComponent<Image>();
                bg.color = DupePillColor;
                bg.raycastTarget = false;

                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(pill.transform, worldPositionStays: false);
                var lrt = (RectTransform)labelGo.transform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;

                label = labelGo.AddComponent<TextMeshProUGUI>();
                label.alignment     = TextAlignmentOptions.Center;
                label.fontSize      = 22f;
                label.color         = Color.black;
                label.raycastTarget = false;
            }

            pill.transform.SetAsLastSibling();   // above the card art, whichever family it is

            if (label != null)
                label.text = string.Format(LocalizationManager.Get("GACHA_DUPE_RP"), record.DupeRp);
        }

        /// <summary>The gold the rest of the Rewards Center uses for a "you gained" chip
        /// (GachaTabController's ActiveTabColor).</summary>
        private static readonly Color DupePillColor = new Color(1f, 0.816f, 0.137f, 1f);
    }
}
