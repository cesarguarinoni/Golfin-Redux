// Assets/Scripts/UI/Gacha/GachaHistoryRow.cs
// Binds a club-type GachaHistoryRecord into the GachaHistoryRow prefab.
// Stage 1: uses BagClubCard.Initialize() for the club card, then disables all buttons.
// The prefab hierarchy is cloned from Stage 0 — do NOT rebuild, only bind.
#nullable enable
using System.Linq;
using Golfin.Inventory;
using Golfin.Roster;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Binder for a club-type GachaHistoryRow prefab instance.
    /// Must be attached to the root of GachaHistoryRow.prefab.
    /// </summary>
    public class GachaHistoryRow : MonoBehaviour
    {
        [Header("Col1 — Club card")]
        [SerializeField] private BagClubCard? _clubCard;

        [Header("Col2 — Metadata")]
        [Tooltip("Line_0: club name, Line_1: rarity, Line_2: date, Line_3: time, Line_4: banner, Line_5: pulls")]
        [SerializeField] private TMP_Text[] _metaLines = System.Array.Empty<TMP_Text>();

        [Header("Col3 — Currency")]
        [SerializeField] private TMP_Text? _ticketLabel;
        [SerializeField] private Image?    _ticketIcon;

        // ── Bind ─────────────────────────────────────────────────────────────────

        public void Bind(GachaHistoryRecord record)
        {
            if (record.RewardType != GachaRewardType.Club)
            {
                Debug.LogWarning($"[GachaHistoryRow] Bind called with {record.RewardType} — expected Club. Row: {name}");
                return;
            }

            BindClubCard(record.RewardId);
            BindMetadata(record);
            BindCurrency(record);
        }

        // ── Club card ─────────────────────────────────────────────────────────────

        private void BindClubCard(string clubId)
        {
            if (_clubCard == null) { Debug.LogWarning("[GachaHistoryRow] _clubCard not wired."); return; }

            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            if (template == null)
            {
                Debug.LogWarning($"[GachaHistoryRow] Club not found: {clubId}");
                return;
            }

            var playerClub = new PlayerClubData
            {
                clubId           = clubId,
                currentLevel     = 1,
                currentDurability = template.maxDurability,
                maxDurability    = template.maxDurability,
            };

            _clubCard.Initialize(playerClub, template, "");

            // Disable all buttons — display-only row.
            foreach (var btn in _clubCard.GetComponentsInChildren<Button>(includeInactive: true))
                btn.interactable = false;
        }

        // ── Metadata lines ────────────────────────────────────────────────────────

        private void BindMetadata(GachaHistoryRecord record)
        {
            // Line_0: club name (look up via ClubDatabaseCSV)
            var template = ClubDatabaseCSV.Instance?.GetClub(record.RewardId);
            string clubName = template != null ? template.name : record.RewardId;

            // Line_1: rarity with color + level, e.g. "<color=#RRGGBB>RARE</color> - Lv 1"
            string rarity = "";
            if (template != null)
            {
                var rarityColor = RarityHelper.GetRarityColor(template.rarity);
                string colorHex = ColorUtility.ToHtmlStringRGB(rarityColor);
                rarity = $"<color=#{colorHex}>{template.rarity.ToString().ToUpper()}</color> - Lv 1";
            }

            // Line_2 / Line_3: date & time from ISO-8601 UTC
            // Format: Line_2 = "PULLED yyyy/MM/dd", Line_3 = "HH:MM:SS AM/PM" (12-hour uppercase)
            string date = "";
            string time = "";
            if (!string.IsNullOrEmpty(record.PulledUtc))
            {
                if (System.DateTime.TryParse(record.PulledUtc,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out var dt))
                {
                    date = "PULLED " + dt.ToString("yyyy/MM/dd");
                    time = dt.ToString("hh:mm:ss tt").ToUpper();
                }
            }

            // Line_4: banner name key → display text
            string bannerName = "";
            var bannerEntry = GachaBannerCatalog.Entries.FirstOrDefault(e => e.BannerId == record.BannerId);
            if (bannerEntry != null)
                bannerName = bannerEntry.NameKey; // TODO: run through LocalizationManager once wired

            // Line_5: pull count e.g. "PULLS: 10"
            string pulls = "PULLS: " + record.PullCount;

            SetLine(0, clubName.ToUpper());
            SetLine(1, rarity);
            SetLine(2, date);
            SetLine(3, time);
            SetLine(4, bannerName);
            SetLine(5, pulls);
        }

        private void SetLine(int index, string text)
        {
            if (index < _metaLines.Length && _metaLines[index] != null)
                _metaLines[index].text = text;
        }

        // ── Currency ──────────────────────────────────────────────────────────────

        private void BindCurrency(GachaHistoryRecord record)
        {
            // _ticketLabel shows static "TICKET" text set in the prefab; do NOT overwrite it.
            // Ticket icon sprite — optional; loaded from Resources if needed
            if (_ticketIcon != null)
            {
                var entry = TicketCatalog.Get(record.TicketType);
                if (entry != null && !string.IsNullOrEmpty(entry.IconSprite))
                {
                    var spr = Resources.Load<Sprite>(entry.IconSprite);
                    if (spr != null) _ticketIcon.sprite = spr;
                }
            }
        }
    }
}
