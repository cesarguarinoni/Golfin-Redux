#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Inventory
{
    /// <summary>
    /// Club card used in the Bag detail panel grid AND the Swap/Equip modal.
    /// Same visual layout as ItemUseClubCard (portrait, stats, buttons)
    /// but action button label is configurable ("SWAP" or "EQUIP").
    /// Level Up and Repair buttons are always disabled.
    ///
    /// Inspector wiring note:
    ///   cardTopImage  → CardTop   (the rarity-coloured image, NOT the Background container)
    ///   portraitImage → CardTop/Portrait  (or wherever the club portrait lives)
    /// </summary>
    public class BagClubCard : MonoBehaviour
    {
        [Header("Card Top")]
        [SerializeField] private Image           cardTopImage     = null!;  // wire to CardTop, NOT Background
        [SerializeField] private Image           portraitImage    = null!;
        [SerializeField] private TextMeshProUGUI nameText         = null!;
        [SerializeField] private TextMeshProUGUI rarityBadgeText  = null!;
        [SerializeField] private TextMeshProUGUI levelText        = null!;

        [Header("Stat Bars")]
        [SerializeField] private Image           statBarPower      = null!;
        [SerializeField] private TextMeshProUGUI statNumPower      = null!;
        [SerializeField] private Image           statBarAccuracy   = null!;
        [SerializeField] private TextMeshProUGUI statNumAccuracy   = null!;
        [SerializeField] private Image           statBarLieRes     = null!;
        [SerializeField] private TextMeshProUGUI statNumLieRes     = null!;
        [SerializeField] private Image           statBarLoft       = null!;
        [SerializeField] private TextMeshProUGUI statNumLoft       = null!;
        [SerializeField] private Image           statBarDurability = null!;
        [SerializeField] private TextMeshProUGUI statNumDurability = null!;

        [Header("Distance")]
        [SerializeField] private TextMeshProUGUI? distanceValue;

        [Header("Action Buttons")]
        [SerializeField] private Button          levelUpButton    = null!;
        [SerializeField] private Button          repairButton     = null!;
        [SerializeField] private Button          actionButton     = null!;  // SWAP or EQUIP
        [SerializeField] private TextMeshProUGUI actionButtonText = null!;

        public event System.Action? OnActionClicked;

        private const int STAT_MAX = 100;
        private static readonly Color StatBarColor       = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityOkColor  = new Color(0.2f, 0.5f, 0.9f, 1f);
        private static readonly Color DurabilityLowColor = new Color(0.9f, 0.2f, 0.2f, 1f);

        public void Initialize(PlayerClubData playerClub, ClubDataRuntime template, string actionLabel)
        {
            // Portrait
            if (portraitImage != null && template.portraitSprite != null)
                portraitImage.sprite = template.portraitSprite;

            // Rarity bg — goes on CardTop image, not the Background container
            if (cardTopImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{template.rarity}");
                if (bgSprite != null) { cardTopImage.sprite = bgSprite; cardTopImage.color = Color.white; }
            }

            // Name
            if (nameText != null)
            {
                string typeLine = template.GetTypeLabel();
                string brand = template.brand.ToUpper();
                nameText.text = string.IsNullOrEmpty(brand) ? typeLine : $"{typeLine}\n{brand}";
            }

            // Rarity badge
            if (rarityBadgeText != null)
            {
                rarityBadgeText.text = RarityHelper.GetRarityLabel(template.rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(template.rarity);
            }

            // Level
            if (levelText != null)
                levelText.text = $"Lv{playerClub.currentLevel}";

            // Stats
            SetBar(statBarPower,    statNumPower,    playerClub.GetPower(template),          STAT_MAX, StatBarColor);
            SetBar(statBarAccuracy, statNumAccuracy, playerClub.GetAccuracy(template),        STAT_MAX, StatBarColor);
            SetBar(statBarLieRes,   statNumLieRes,   playerClub.GetLieResistance(template),   STAT_MAX, StatBarColor);
            SetBar(statBarLoft,     statNumLoft,     playerClub.GetLoft(template),            STAT_MAX, StatBarColor);

            int curDur = playerClub.currentDurability;
            int maxDur = playerClub.maxDurability;
            if (statBarDurability != null)
            {
                statBarDurability.fillAmount = maxDur > 0 ? (float)curDur / maxDur : 0f;
                statBarDurability.color = playerClub.IsDurabilityLow ? DurabilityLowColor : DurabilityOkColor;
            }
            if (statNumDurability != null) statNumDurability.text = $"{curDur}";

            // Distance
            if (distanceValue != null)
                distanceValue.text = $"{playerClub.GetDistance(template)} yd";

            // Buttons
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;

            if (actionButton != null)
            {
                actionButton.interactable = true;
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => OnActionClicked?.Invoke());
            }
            if (actionButtonText != null)
                actionButtonText.text = actionLabel;
        }

        // ── Prize binding (gacha_client_real_pull, after Cesar's rejection 2026-08-31) ──
        //
        // A gacha pull can pay a ball, an item, a character or a ticket, and Cesar's call is that
        // EVERY prize card is the same size and shape as the club one. So they all use THIS card —
        // which is not a stretch of it: `GachaHistoryRowBall.prefab` has nested a BagClubCard and
        // bound ball data into it since gacha_history Stage 1. This method is that pattern given a
        // name, so the four kinds share one shell instead of four hand-bound copies of its paths.
        //
        // The first attempt reused the wide Rewards-Center shop card scaled to 0.19 to fit. It was
        // the SPEC's instruction and it was wrong on sight: a 978x274 row card in a 183x410 portrait
        // slot is legible as a shape and not as text. Rejected, and rightly.

        /// <summary>Everything this card needs to draw a prize of any kind.</summary>
        public readonly struct PrizeView
        {
            public readonly Sprite?         Portrait;
            /// <summary>Card name. A <c>\n</c> gives the two-line form the club binding uses.</summary>
            public readonly string          Name;
            public readonly CharacterRarity Rarity;
            /// <summary>The top-right chip — <c>"Lv1"</c> for a club, <c>"x3"</c> for a stack.
            /// Empty hides it.</summary>
            public readonly string          Badge;
            /// <summary>The line the club card uses for distance — a restore percentage, a ticket
            /// count. Empty hides the row.</summary>
            public readonly string          Detail;
            /// <summary>Up to five stat values, or null to hide every stat row. Shorter than five
            /// hides the surplus rows rather than drawing them at zero.</summary>
            public readonly int[]?          Stats;
            /// <summary>What a full bar means for <see cref="Stats"/> — 10 for a ball, 60 for a club.</summary>
            public readonly int             StatMax;
            /// <summary>Body copy for a prize that has NO stat lanes, filling the space they would
            /// have taken. Empty leaves that space blank, which is what a stat-less card looked
            /// like before. See <see cref="DescriptionName"/>.</summary>
            public readonly string          Description;

            public PrizeView(Sprite? portrait, string name, CharacterRarity rarity,
                             string badge = "", string detail = "", int[]? stats = null, int statMax = 60,
                             string description = "")
            {
                Portrait = portrait; Name = name ?? string.Empty; Rarity = rarity;
                Badge = badge ?? string.Empty; Detail = detail ?? string.Empty;
                Stats = stats; StatMax = statMax > 0 ? statMax : 60;
                Description = description ?? string.Empty;
            }
        }

        /// <summary>
        /// Draw an arbitrary prize on this card: same rect, same rarity frame, same badge, same
        /// stat lanes as a club — only the CONTENT differs.
        ///
        /// <para>
        /// The stat rows and the distance row are resolved BY PATH rather than by new
        /// <c>[SerializeField]</c>s, because the card already ships wired for a club and adding
        /// five row references to every existing instance to hide five rows would be a prefab
        /// change for no gain. The paths are the same ones the prize binder already uses to hide
        /// the action row, verified against BagClubCard.prefab.
        /// </para>
        /// </summary>
        public void InitializePrize(in PrizeView view)
        {
            if (portraitImage != null)
            {
                portraitImage.sprite  = view.Portrait;
                // A kind with no art must not show the PREVIOUS prize's portrait — this card is
                // re-bound in place on every pull.
                portraitImage.enabled = view.Portrait != null;
            }

            if (cardTopImage != null)
            {
                var bgSprite = Resources.Load<Sprite>($"Rarities/{view.Rarity}");
                if (bgSprite != null) { cardTopImage.sprite = bgSprite; cardTopImage.color = Color.white; }
            }

            if (nameText != null) nameText.text = view.Name;

            if (rarityBadgeText != null)
            {
                rarityBadgeText.text  = RarityHelper.GetRarityLabel(view.Rarity);
                rarityBadgeText.color = RarityHelper.GetRarityBadgeTextColor(view.Rarity);
            }

            if (levelText != null)
            {
                levelText.text = view.Badge;
                levelText.gameObject.SetActive(!string.IsNullOrEmpty(view.Badge));
            }

            // Distance row — the card's one free-text line.
            //
            // It is sized for "250 yd". A prize can put a longer string in it ("RESTORES 75%"),
            // which WRAPPED to three lines and spilled down the card. Auto-size with wrapping off
            // shrinks it to one line instead, and leaves a club's own "250 yd" pixel-identical
            // because it already fits at the authored size — so no restore is needed when this
            // same card instance is later re-bound to a club.
            if (distanceValue != null)
            {
                distanceValue.text              = view.Detail;
                distanceValue.textWrappingMode  = TMPro.TextWrappingModes.NoWrap;
                distanceValue.enableAutoSizing  = true;
                distanceValue.fontSizeMax       = distanceValue.fontSize;
                distanceValue.fontSizeMin       = 8f;
            }

            // ⚠️ THE ROW STAYS FOR A CARD THAT HAS STAT LANES, EVEN WITH NOTHING TO SAY.
            //
            // StatsPanel is a vertical layout, so hiding this row pulls every stat lane up by its
            // height — and a ball's five bars then sat one row HIGHER than a club's, which is
            // exactly what they are meant to be compared against side by side in the grid. Kept as
            // a blank spacer, the lanes line up across kinds. A card with no lanes at all (item,
            // ticket) has nothing to align, so there the row goes when it has no text.
            bool hasDetail = !string.IsNullOrEmpty(view.Detail);
            SetActiveAt(DistanceRowPath, hasDetail || view.Stats != null);

            // The icon is a DISTANCE arc. No prize kind that reaches this method has a distance —
            // a repair kit read "⌒ RESTORES 100%" — so it is hidden here and put back by
            // RestoreClubRows for the one kind that does.
            SetActiveAt(DistanceIconPath, false);

            // Stat lanes.
            var bars = new[] { statBarPower, statBarAccuracy, statBarLieRes, statBarLoft, statBarDurability };
            var nums = new[] { statNumPower, statNumAccuracy, statNumLieRes, statNumLoft, statNumDurability };

            for (int i = 0; i < StatRowPaths.Length; i++)
            {
                bool has = view.Stats != null && i < view.Stats.Length;
                SetActiveAt(StatRowPaths[i], has);
                if (has) SetBar(bars[i], nums[i], view.Stats![i], view.StatMax, StatBarColor);
            }

            BindDescription(view.Description);

            // Display only, always — a prize card is never actionable.
            if (levelUpButton != null) levelUpButton.interactable = false;
            if (repairButton  != null) repairButton.interactable  = false;
            if (actionButton  != null) { actionButton.onClick.RemoveAllListeners(); actionButton.interactable = false; }
        }

        /// <summary>Restore the stat lanes and the distance row a <see cref="InitializePrize"/> may
        /// have hidden, so re-binding this card to a CLUB shows a whole club card again.</summary>
        public void RestoreClubRows()
        {
            BindDescription(string.Empty);
            SetActiveAt(DistanceRowPath, true);
            SetActiveAt(DistanceIconPath, true);   // a club DOES have a distance
            foreach (var path in StatRowPaths) SetActiveAt(path, true);
            if (levelText != null) levelText.gameObject.SetActive(true);
            if (portraitImage != null) portraitImage.enabled = true;
        }

        /// <summary>Name of the description label this card builds on demand. Looked up before
        /// creating one so a re-bind reuses it.</summary>
        private const string DescriptionName = "PrizeDescription";

        private const string StatsPanelPath   = "Mask/Background/StatsPanel";
        private const string DistanceRowPath  = "Mask/Background/StatsPanel/DistanceRow";
        private const string DistanceIconPath = "Mask/Background/StatsPanel/DistanceRow/Image";

        /// <summary>
        /// Fill the space the hidden stat lanes leave with the prize's own description — the same
        /// copy the Item screen shows under ITEM INFO.
        ///
        /// <para>
        /// It is BUILT rather than cloned because BagClubCard carries no body-copy label to clone:
        /// every text on it (NameText, RarityBadge, LevelBadge, the five StatNums, DistanceValue)
        /// is a short single-line field that is already bound. Rule 19 asks for provenance on a
        /// MANDATED clone; there is nothing here to clone, so it is stated rather than claimed.
        /// </para>
        /// <para>
        /// It goes in as the LAST child of StatsPanel, which is a VerticalLayoutGroup — so it lands
        /// under the distance row and takes the height the hidden rows freed, with no arithmetic
        /// here about how much that is.
        /// </para>
        /// </summary>
        private void BindDescription(string description)
        {
            var panel = transform.Find(StatsPanelPath) as RectTransform;
            if (panel == null) return;

            // ⚠️ THE LABEL IS A SIBLING OF StatsPanel, NOT A CHILD OF IT.
            //
            // StatsPanel is a VerticalLayoutGroup, and a LayoutGroup REWRITES its children's
            // anchors and sizeDelta on every layout pass — measured: whatever anchors and width
            // this method wrote came back as (0,1)..(0,1) at 1228x14, the width of the whole
            // unwrapped string, so the text never wrapped and ran off the side of the card. There
            // is no combination of sizeDelta, anchors or LayoutElement that survives that while the
            // group does not control size, so the label is parented OUTSIDE the group and
            // positioned over the area the hidden stat rows vacated.
            var host = panel.parent as RectTransform;
            if (host == null) return;

            var existing = host.Find(DescriptionName) as RectTransform;

            if (string.IsNullOrWhiteSpace(description))
            {
                if (existing != null) existing.gameObject.SetActive(false);
                return;
            }

            TextMeshProUGUI? label;
            RectTransform rt;

            if (existing != null)
            {
                existing.gameObject.SetActive(true);
                rt    = existing;
                label = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var go = new GameObject(DescriptionName, typeof(RectTransform));
                rt = (RectTransform)go.transform;
                rt.SetParent(host, worldPositionStays: false);

                label = go.AddComponent<TextMeshProUGUI>();
                label.alignment        = TextAlignmentOptions.TopLeft;
                label.textWrappingMode = TextWrappingModes.Normal;
                label.enableAutoSizing = true;
                label.fontSizeMax      = 11f;
                label.fontSizeMin      = 6f;
                label.color            = DescriptionColor;
                label.raycastTarget    = false;

                // ⚠️ THE ONE THAT ACTUALLY CAUSED IT. TMP resizes its OWN RectTransform to the
                // text's preferred size when this is on, which is why every width written here
                // came back as the width of the whole unwrapped string (1228, then 955 — measured
                // twice, with different parents and anchors) and the text never wrapped. Layout
                // groups and anchors were never the culprit.
                label.autoSizeTextContainer = false;
            }

            // Sit exactly where StatsPanel sits, minus the height of whatever is still visible in
            // it (the distance row) off the top. Copying the panel's own anchoring is what keeps
            // this correct without hard-coding the card's geometry.
            float inset = VisibleRowsHeight(panel, null);

            if (label != null)
            {
                label.autoSizeTextContainer = false;   // also for a label built by an older build
                label.text = description;
            }

            rt.anchorMin        = panel.anchorMin;
            rt.anchorMax        = panel.anchorMax;
            rt.pivot            = panel.pivot;
            rt.sizeDelta        = new Vector2(panel.sizeDelta.x, Mathf.Max(0f, panel.sizeDelta.y - inset));
            rt.anchoredPosition = new Vector2(panel.anchoredPosition.x,
                                              panel.anchoredPosition.y - inset * 0.5f);
        }

        /// <summary>Height of the panel's VISIBLE children plus the spacing between them — the
        /// strip at the top of StatsPanel the description must sit below.</summary>
        private static float VisibleRowsHeight(Transform panel, RectTransform? ignore)
        {
            var vlg = panel.GetComponent<VerticalLayoutGroup>();
            float spacing = vlg != null ? vlg.spacing : 0f;

            float used = 0f;
            int counted = 0;
            foreach (RectTransform child in panel)
            {
                if (child == ignore || !child.gameObject.activeSelf) continue;
                used += child.rect.height;
                counted++;
            }
            return counted == 0 ? 0f : used + spacing * counted;
        }

        /// <summary>Muted against the card's dark stats panel — body copy, not a headline.</summary>
        private static readonly Color DescriptionColor = new Color(0.78f, 0.82f, 0.88f, 1f);

        private static readonly string[] StatRowPaths =
        {
            "Mask/Background/StatsPanel/StatRow_Power",
            "Mask/Background/StatsPanel/StatRow_Accuracy",
            "Mask/Background/StatsPanel/StatRow_LieRes",
            "Mask/Background/StatsPanel/StatRow_Loft",
            "Mask/Background/StatsPanel/StatRow_Durability",
        };

        private void SetActiveAt(string path, bool active)
        {
            var t = transform.Find(path);
            if (t != null) t.gameObject.SetActive(active);
        }

        private static void SetBar(Image? bar, TextMeshProUGUI? num, int value, int cap, Color color)
        {
            if (bar != null) { bar.fillAmount = cap > 0 ? (float)value / cap : 0f; bar.color = color; }
            if (num != null) num.text = $"{value}";
        }
    }
}
