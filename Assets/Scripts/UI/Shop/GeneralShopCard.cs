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
        private const float ClubBarFullScale = 60f;   // Val 40 → 218.5px, 9 → 49.7px ⇒ ~331/60
        private static readonly Color BallSegOn  = new Color32(0x33, 0x80, 0xE6, 0xFF);
        private static readonly Color BallSegOff = new Color32(0x40, 0x40, 0x4D, 0x80);

        private bool _isBall;

        // ── Public API ──────────────────────────────────────────────────────────

        public void Bind(ShopCatalogEntry entry)
        {
            Entry = entry;
            _isBall = entry != null && entry.Category == ShopCategory.Ball;

            if (entry == null) return;
            if (_isBall) BindBall(entry);
            else         BindClub(entry);

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
            nl.sizeDelta = new Vector2(242f, nl.sizeDelta.y);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMax = 32f;
            tmp.fontSizeMin = 12f;   // low floor so the FULL name shrinks to fit (no ellipsis, no overlap)
            tmp.overflowMode = TextOverflowModes.Overflow;
        }

        // ── Club variant ──────────────────────────────────────────────────────────

        private void BindClub(ShopCatalogEntry entry)
        {
            var club = ClubDatabaseCSV.Instance != null ? ClubDatabaseCSV.Instance.GetClub(entry.RefId) : null;
            if (club == null) { Debug.LogWarning($"[GeneralShopCard] club '{entry.RefId}' missing."); return; }

            int startLvl = StartingLevel(club.rarity);
            string rar = club.rarity.ToString();

            SetRarityTile(rar);
            SetImage("tournament_image/Portrait", club.portraitSprite != null ? club.portraitSprite : club.portraitFull);
            SetText("NameLabel", club.name.ToUpperInvariant());
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
            if (ball == null) { Debug.LogWarning($"[GeneralShopCard] ball '{entry.RefId}' missing."); return; }

            string rar = string.IsNullOrEmpty(entry.Rarity) ? "Common" : entry.Rarity;

            SetRarityTile(rar);
            SetImage("tournament_image/Portrait", ball.thumbnailSprite != null ? ball.thumbnailSprite : ball.fullSprite);
            SetText("NameLabel", ball.name.ToUpperInvariant());

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

        private void WireBuy(ShopCatalogEntry entry)
        {
            var btn   = Find("CtaGoldButton")?.GetComponent<Button>();
            var label = Find("CtaGoldButton/PlayLable")?.GetComponent<TextMeshProUGUI>();
            if (btn == null) return;

            btn.onClick.RemoveAllListeners();

            bool owned = entry.Category == ShopCategory.Club &&
                         ClubManager.Instance != null && ClubManager.Instance.IsOwned(entry.RefId);

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
