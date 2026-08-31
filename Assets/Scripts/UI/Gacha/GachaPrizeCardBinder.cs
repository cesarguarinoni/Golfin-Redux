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
        public static GameObject? Instantiate(PrizeRecord record, Transform parent, GameObject? clubPrefab)
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

            var go = Object.Instantiate(prefab, parent);
            go.name = "PrizeCard_" + record.Kind + "_" + record.RefId;
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
}
