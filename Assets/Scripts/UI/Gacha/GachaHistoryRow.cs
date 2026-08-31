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
            BindMetadata(record, ClubName(record.RewardId), ClubRarityLine(record.RewardId));
            BindCurrency(record);
        }

        /// <summary>
        /// Bind a NON-club record onto this same row (gacha_client_real_pull §4.5).
        ///
        /// <para>
        /// The row is only club-specific in ONE place — the <c>BagClubCard</c> in Col1 — and
        /// everything else on it (six metadata lines, the ticket chip) is kind-agnostic. So a
        /// character, an item or a ticket binds the metadata and leaves Col1's card hidden rather
        /// than getting a third prefab: a prefab per kind is four prefabs to keep in step for a
        /// row whose only difference is one image.
        /// </para>
        /// </summary>
        public void BindGeneric(GachaHistoryRecord record, string displayName, string rarityLine)
        {
            if (_clubCard != null) _clubCard.gameObject.SetActive(false);
            BindMetadata(record, displayName, rarityLine);
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

        /// <summary>The club's display name, or the raw id when this build has no row for it.</summary>
        private static string ClubName(string clubId)
        {
            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            return template != null ? template.name : clubId;
        }

        /// <summary>"&lt;color=#RRGGBB&gt;RARE&lt;/color&gt; - Lv 1", or empty when unresolvable.</summary>
        private static string ClubRarityLine(string clubId)
        {
            var template = ClubDatabaseCSV.Instance?.GetClub(clubId);
            if (template == null) return "";
            string colorHex = ColorUtility.ToHtmlStringRGB(RarityHelper.GetRarityColor(template.rarity));
            return $"<color=#{colorHex}>{template.rarity.ToString().ToUpper()}</color> - Lv 1";
        }

        private void BindMetadata(GachaHistoryRecord record, string displayName, string rarityLine)
        {
            string clubName = displayName;
            string rarity   = rarityLine;

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

            // Line_4: the banner's AUTHORED title, in the player's language — the same ladder the
            // card's title uses, so the log and the card never name the same banner differently.
            string bannerName = "";
            var bannerEntry = GachaBannerCatalog.Entries.FirstOrDefault(e => e.BannerId == record.BannerId);
            if (bannerEntry != null)
            {
                bannerName = GachaCsvMerge.PickLocalised(bannerEntry.NameEn, bannerEntry.NameJa);
                if (string.IsNullOrWhiteSpace(bannerName))
                    bannerName = LocalizationManager.Get(bannerEntry.NameKey);
            }

            // Line_5: pull count e.g. "PULLS: 10", and — for a duplicate — what it paid instead.
            string pulls = "PULLS: " + record.PullCount;
            if (record.DupeRp > 0)
                pulls += "   " + string.Format(LocalizationManager.Get("GACHA_DUPE_RP"), record.DupeRp);

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
