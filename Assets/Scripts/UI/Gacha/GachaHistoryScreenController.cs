// Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs
// Stage 1 screen controller for the Gacha History / pull log screen.
// Spawns row prefabs (GachaHistoryRow / GachaHistoryRowBall) into the scroll content,
// one per GachaHistoryRecord. Inserts a divider between entries.
// CLOSE button navigates to ScreenId.GeneralShop.
#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.UI.Polish;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Attached to the root of GachaHistoryScreen.prefab (or its scene instance).
    /// Drives the history scroll list; shows all history records sorted newest-first.
    /// </summary>
    public class GachaHistoryScreenController : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject? _clubRowPrefab;
        [SerializeField] private GameObject? _ballRowPrefab;
        [SerializeField] private GameObject? _dividerPrefab;

        [Header("Scroll")]
        [SerializeField] private RectTransform? _scrollContent;

        [Header("Close")]
        [SerializeField] private Button? _closeButton;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);
        }

        private void OnEnable()
        {
            // Draw the disk mirror immediately, then re-draw when the server answers. The screen
            // never waits on a socket — an offline open shows the last log the server confirmed
            // rather than an empty list that reads as "you have never pulled".
            GachaHistoryStore.OnChanged += RepaintAnimated;
            RebuildList();                      // the FIRST paint is the rest state: no motion
            GachaHistoryStore.Refresh();
        }

        private void OnDisable()
        {
            GachaHistoryStore.OnChanged -= RepaintAnimated;
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClose);
        }

        /// <summary>
        /// game_polish_a §D3 — the log fades out, repaints and fades back in when the server's
        /// answer changes it.
        ///
        /// <para>§D3 names the <c>FiltersIconRow</c> filter change as the site. There is no filter
        /// change to animate in this build: the chips under
        /// <c>GameScreenContent/ContentContainer/FiltersBlock/CategoryRow</c> exist in the prefab
        /// but nothing wires their onClick — this controller has no chip fields at all. The site
        /// that DOES repaint the list is the store's OnChanged, when the server's log arrives over
        /// the disk mirror, so that is what is animated. When the chips are wired they will route
        /// through here and inherit the fade. (Flagged in IMPLEMENTER_REPORT as a finding, not
        /// silently substituted.)</para>
        /// </summary>
        private void RepaintAnimated() => UiSelection.FadeSwap(this, ListGroup(), RebuildList);

        /// <summary>The scroll content's own CanvasGroup, made on first use — never the whole
        /// screen's, which would take the chrome down with the list.</summary>
        private CanvasGroup? _listGroup;
        private CanvasGroup? ListGroup()
        {
            if (_listGroup != null) return _listGroup;
            if (_scrollContent == null) return null;
            _listGroup = _scrollContent.GetComponent<CanvasGroup>();
            if (_listGroup == null) _listGroup = _scrollContent.gameObject.AddComponent<CanvasGroup>();
            return _listGroup;
        }

        // ── List population ────────────────────────────────────────────────────

        private void RebuildList()
        {
            if (_scrollContent == null)
            {
                Debug.LogWarning("[GachaHistoryScreenController] _scrollContent not wired.");
                return;
            }

            // Destroy existing dynamic rows (keep any authored children that are NOT rows).
            // Safest: just clear everything and respawn.
            foreach (Transform child in _scrollContent)
                Destroy(child.gameObject);

            var records = GachaHistoryStore.All;
            for (int i = 0; i < records.Count; i++)
            {
                var record = records[i];
                SpawnRow(record);

                // Divider between entries (not after the last one)
                if (i < records.Count - 1 && _dividerPrefab != null)
                    Instantiate(_dividerPrefab, _scrollContent);
            }
        }

        private void SpawnRow(GachaHistoryRecord record)
        {
            switch (record.RewardType)
            {
                case GachaRewardType.Club:
                {
                    if (_clubRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _clubRowPrefab not wired.");
                        return;
                    }
                    var go = Instantiate(_clubRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRow>();
                    if (row == null) row = go.AddComponent<GachaHistoryRow>();
                    row.Bind(record);
                    break;
                }
                case GachaRewardType.Ball:
                {
                    if (_ballRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _ballRowPrefab not wired.");
                        return;
                    }
                    var go = Instantiate(_ballRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRowBall>();
                    if (row == null) row = go.AddComponent<GachaHistoryRowBall>();
                    row.Bind(record);
                    break;
                }
                // Character / item / ticket. They render on the CLUB row prefab with its club card
                // hidden (GachaHistoryRow.BindGeneric): the row is only club-specific in that one
                // image, and a prefab per kind would be four to keep in step for one difference.
                // Skipping them — which is what this branch used to do — meant a real pull could
                // pay a prize the log silently omitted.
                default:
                {
                    if (_clubRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _clubRowPrefab not wired.");
                        return;
                    }
                    var go = Instantiate(_clubRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRow>();
                    if (row == null) row = go.AddComponent<GachaHistoryRow>();
                    row.BindGeneric(record, ResolveName(record), ResolveRarityLine(record));
                    break;
                }
            }
        }

        /// <summary>
        /// The prize's display name, resolved against whichever database owns its kind. Falls back
        /// to the raw ref id — a prize this build cannot name is still a prize the player won, and
        /// the id is more useful in the log than a blank line.
        /// </summary>
        private static string ResolveName(GachaHistoryRecord record)
        {
            switch (record.RewardType)
            {
                case GachaRewardType.Character:
                {
                    var ch = Golfin.Roster.CharacterDatabaseCSV.Instance?.GetCharacter(record.RewardId);
                    return ch != null ? ch.characterName : record.RewardId;
                }
                case GachaRewardType.Item:
                {
                    var item = Golfin.Inventory.ItemDatabaseCSV.Instance?.GetItem(record.RewardId);
                    return item != null ? item.name : record.RewardId;
                }
                case GachaRewardType.Ticket:
                {
                    if (int.TryParse(record.RewardId, out int id))
                    {
                        var type = TicketTypeCatalog.Get(id);
                        if (type != null) return type.DisplayName;
                    }
                    return record.RewardId;
                }
                default:
                    return record.RewardId;
            }
        }

        /// <summary>The coloured rarity line, matching the club row's format. Empty for a ticket,
        /// which has no rarity of its own.</summary>
        private static string ResolveRarityLine(GachaHistoryRecord record)
        {
            Golfin.Roster.CharacterRarity? rarity = record.RewardType switch
            {
                GachaRewardType.Character =>
                    Golfin.Roster.CharacterDatabaseCSV.Instance?.GetCharacter(record.RewardId)?.rarity,
                _ => null,
            };

            if (rarity == null) return string.Empty;

            string hex = ColorUtility.ToHtmlStringRGB(Golfin.Roster.RarityHelper.GetRarityColor(rarity.Value));
            return $"<color=#{hex}>{rarity.Value.ToString().ToUpper()}</color>";
        }

        // ── Close ──────────────────────────────────────────────────────────────

        // nav_back_memory §3 — history first, the Rewards Center as the fallback.
        private void OnClose()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.GoBack(ScreenId.GeneralShop);
            else
                Debug.LogWarning("[GachaHistoryScreenController] ScreenManager not found.");
        }
    }
}
