// Assets/Scripts/UI/Gacha/GachaPrizeCardBinder.cs
// gacha_client_real_pull §4.3 — the ONE prize-card binder.
//
// It was GachaPrizesScreenController.BindCard, a static the reveal modal reached across to call.
// It moves out because a prize is no longer always a club: the pull can pay a ball, a character,
// an item or a ticket, and picking the prefab is now part of binding. Keeping ONE binder is what
// makes the card the player sees pop out of the bag and the card on the Prizes screen the same
// object built by the same code — the reason it was shared in the first place.
//
// TWO PREFAB FAMILIES, NO NEW ART:
//   club                          → BagClubCard.prefab (unchanged; the reveal's serialized prefab)
//   ball | character | item | ticket → GeneralShopCard_Club, the Rewards-Center card, in DISPLAY
//                                   mode: price row and BUY hidden, nothing interactable.
// The shop card already renders the first three kinds (shop_server_purchase §3.4). Reusing it is
// SPEC §4.3's instruction and is not a Figma-approved design — Cesar may replace it later.
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
        /// <summary>Resources path of the non-club prize card (the Rewards-Center club template,
        /// which is also the one the shop binds characters and items onto).</summary>
        private const string ShopCardPath = "Prefabs/Shop/GeneralShopCard_Club";

        /// <summary>Paths inside BagClubCard.prefab whose action row is hidden on a prize card.</summary>
        private static readonly string[] ClubActionButtonPaths =
        {
            "Mask/Background/ButtonRow/LevelUpBtn",
            "Mask/Background/ButtonRow/RepairBtn",
            "SwapBtn",
        };

        /// <summary>
        /// Instantiate the right card for <paramref name="record"/> under <paramref name="parent"/>
        /// and bind it. Returns null when the prefab for that kind cannot be loaded.
        /// </summary>
        /// <param name="clubPrefab">The caller's BagClubCard prefab. The reveal modal has it
        /// serialized; the Prizes screen reads it off one of its authored grid cards.</param>
        /// <param name="slotSize">The footprint the card must occupy, when the caller has one.
        /// See <see cref="WrapToFit"/> — this is SPEC §4.3's scale-to-fit.</param>
        /// <param name="siblingIndex">Where in the parent to insert it, so a spawned card takes the
        /// place of the authored card it replaces instead of being appended after all of them.</param>
        public static GameObject? Instantiate(PrizeRecord record, Transform parent, GameObject? clubPrefab,
                                              Vector2? slotSize = null, int siblingIndex = -1)
        {
            GameObject? prefab = record.Kind == PrizeRecord.KindClub
                ? clubPrefab
                : Resources.Load<GameObject>(ShopCardPath);

            if (prefab == null)
            {
                Debug.LogError($"[GachaPrizeCardBinder] No prefab for prize kind '{record.Kind}' " +
                               $"({record.RefId}) — the prize is granted but cannot be shown.");
                return null;
            }

            // A CLUB card is authored at the slot's size, so it goes straight in.
            if (record.Kind == PrizeRecord.KindClub || !slotSize.HasValue)
            {
                var plain = Object.Instantiate(prefab, parent);
                plain.name = "PrizeCard_" + record.Kind + "_" + record.RefId;
                if (siblingIndex >= 0) plain.transform.SetSiblingIndex(siblingIndex);
                Bind(plain, record);
                return plain;
            }

            return WrapToFit(prefab, record, parent, slotSize.Value, siblingIndex);
        }

        /// <summary>
        /// SPEC §4.3's scale-to-fit: put the card inside a slot-sized WRAPPER and scale it down to
        /// fit, centred.
        ///
        /// <para>
        /// ⚠️ <b>THE TWO CARDS ARE DIFFERENT SHAPES, AND THIS IS THE CONSEQUENCE.</b> Measured live
        /// on the Prizes grid: the club card is <b>181×374</b> (a tall portrait card) and the
        /// Rewards-Center card is <b>978×274</b> (a wide row card). Fitting one into the other is a
        /// uniform scale of about <b>0.19</b> — legible as a shape, not as text. The spec
        /// anticipated exactly this ("if it does not fit, scale-to-fit inside the slot and say so
        /// with a screenshot — do NOT rebuild a card"), so it is the instructed outcome, flagged in
        /// the implementer report. A real design for a non-club prize card is out of scope here.
        /// </para>
        /// <para>
        /// <b>Why a wrapper rather than scaling the card in place.</b> Two reasons, both measured.
        /// First, the rows are HorizontalLayoutGroups and <c>localScale</c> is invisible to layout —
        /// an unconstrained 978px child pushes every sibling, and the row's own COST/PULL block,
        /// off the panel. Second, the shop card carries its OWN anchors and pivot from the shop
        /// prefab, so scaling it around that pivot slid it out of its slot even once the layout was
        /// pinned. The wrapper is a plain slot-sized rect the layout understands, and the card is
        /// centred inside it with a pivot the wrapper controls — so neither problem can come back.
        /// </para>
        /// <para>
        /// The wrapper also restores the entrance animation for free: it rests at scale 1 like an
        /// authored card, so PopIn animates it 0 → 1 and the fit lives on the child, untouched.
        /// </para>
        /// </summary>
        private static GameObject WrapToFit(GameObject prefab, PrizeRecord record, Transform parent,
                                            Vector2 slot, int siblingIndex)
        {
            var slotGo = new GameObject("PrizeSlot_" + record.Kind + "_" + record.RefId,
                                        typeof(RectTransform));
            var slotRt = (RectTransform)slotGo.transform;
            slotRt.SetParent(parent, worldPositionStays: false);
            slotRt.anchorMin = slotRt.anchorMax = new Vector2(0.5f, 0.5f);
            slotRt.pivot     = new Vector2(0.5f, 0.5f);
            slotRt.sizeDelta = slot;
            slotRt.localScale = Vector3.one;

            var le = slotGo.AddComponent<LayoutElement>();
            le.preferredWidth  = slot.x;
            le.preferredHeight = slot.y;
            le.minWidth        = slot.x;
            le.minHeight       = slot.y;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;

            if (siblingIndex >= 0) slotRt.SetSiblingIndex(siblingIndex);

            var card   = Object.Instantiate(prefab, slotRt);
            card.name  = "PrizeCard_" + record.Kind + "_" + record.RefId;
            var cardRt = card.transform as RectTransform;

            if (cardRt != null)
            {
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot     = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;

                Vector2 own = cardRt.rect.size;
                if (own.x > 0f && own.y > 0f)
                {
                    float scale = Mathf.Min(slot.x / own.x, slot.y / own.y);
                    cardRt.localScale = new Vector3(scale, scale, 1f);

                    Debug.Log($"[GachaPrizeCardBinder] '{card.name}' is {own.x:F0}x{own.y:F0} in a " +
                              $"{slot.x:F0}x{slot.y:F0} slot — scaled to {scale:F2} to fit (SPEC §4.3).");
                }
            }

            Bind(card, record);
            return slotGo;
        }

        /// <summary>
        /// The scale a prize card rests at, for anything that animates it.
        ///
        /// <para>
        /// It is 1 for everything the binder returns today: a club card is authored at its slot's
        /// size, and a scaled-to-fit one comes back WRAPPED (see <see cref="WrapToFit"/>), so the
        /// object the animations touch is a slot-sized wrapper and the fit lives on its child.
        /// The question stays asked rather than assumed because assuming 1 is exactly what undid
        /// the fit before the wrapper existed — the entrance animation landed every card on a hard
        /// <c>Vector3.one</c> and the card overflowed its slot again, measured.
        /// </para>
        /// </summary>
        public static float HomeScaleOf(GameObject? card)
        {
            if (card == null) return 1f;
            var home = card.GetComponent<PrizeCardHomeScale>();
            return home != null ? home.Scale : 1f;
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
            if (club != null) { BindClubCard(club, record); return; }

            var shop = cardGo.GetComponent<GeneralShopCard>();
            if (shop != null) { BindShopCard(shop, record); return; }

            Debug.LogWarning($"[GachaPrizeCardBinder] '{cardGo.name}' carries neither BagClubCard nor " +
                             "GeneralShopCard — nothing to bind.");
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

            var playerClub = new PlayerClubData
            {
                clubId            = record.RefId,
                currentLevel      = 1,
                currentDurability = template.maxDurability,
                maxDurability     = template.maxDurability,
            };

            card.Initialize(playerClub, template, "");

            // Display-only. Hiding the action row is not cosmetic tidying: the Prizes screen's grid
            // cards ship with LevelUpBtn / RepairBtn / SwapBtn DEACTIVATED in the prefab, while a
            // fresh BagClubCard instance (which is what the reveal modal spawns) has them active —
            // so the same prize rendered two different ways depending on where it was shown. Doing
            // it here, in the one shared binder, is what makes them agree, and it matches the Figma
            // reveal card (13997:4503), which has no action row.
            foreach (var n in ClubActionButtonPaths)
            {
                var t = card.transform.Find(n);
                if (t != null) t.gameObject.SetActive(false);
            }

            foreach (var btn in card.GetComponentsInChildren<Button>(includeInactive: true))
                btn.interactable = false;

            ApplyDupePill(card.transform, record);
        }

        // ── Ball / character / item / ticket ───────────────────────────────────

        private static void BindShopCard(GeneralShopCard card, PrizeRecord record)
        {
            switch (record.Kind)
            {
                case PrizeRecord.KindTicket:
                    card.BindTicket(record.RefId, record.Quantity);
                    break;

                default:
                    card.BindForDisplay(ToShopCategory(record.Kind), record.RefId);
                    break;
            }

            ApplyDupePill(card.transform, record);
        }

        private static ShopCategory ToShopCategory(string kind) => kind switch
        {
            PrizeRecord.KindBall      => ShopCategory.Ball,
            PrizeRecord.KindCharacter => ShopCategory.Character,
            PrizeRecord.KindItem      => ShopCategory.Item,
            _                         => ShopCategory.Club,
        };

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

    /// <summary>
    /// Records the scale a prize card rests at, for anything that animates it.
    ///
    /// <para>
    /// Nothing attaches one today: since <see cref="GachaPrizeCardBinder"/> fits a card by WRAPPING
    /// it, the object the animations touch is a slot-sized wrapper that rests at 1. The component
    /// stays as the declared answer to "what scale does this card rest at", so the two animation
    /// paths keep ASKING — assuming 1 is precisely what undid the fit before the wrapper existed.
    /// </para>
    /// </summary>
    public sealed class PrizeCardHomeScale : MonoBehaviour
    {
        public float Scale = 1f;
    }
}
