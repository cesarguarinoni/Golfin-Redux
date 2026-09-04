// Assets/Scripts/UI/Gacha/GachaHistoryScreenController.cs
// Stage 1 screen controller for the Gacha History / pull log screen.
// Spawns row prefabs (GachaHistoryRow / GachaHistoryRowBall) into the scroll content,
// one per GachaHistoryRecord. Inserts a divider between entries.
// CLOSE button navigates to ScreenId.GeneralShop.
#nullable enable
using System.Collections;
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

        // ── Paging (gacha_history_rebuild_stall) ───────────────────────────────
        //
        // WHY THIS SCREEN IS PAGED. `GachaHistoryStore.All` is the FLATTENED history page —
        // `FetchHistoryAsync(100, …)` is 100 PULLS, and an x10 flattens to ten records, so up to
        // ~1 000 rows. Every club row instantiates a whole `BagClubCard` (art, stat arc, buttons).
        // Building all of them in one frame is what `game_polish_a`'s A13 perf run measured:
        //
        //     GachaPrizes -> GachaHistory    297.7 MB over 2 frame(s), worst frame 1271.04 ms
        //
        // against 347 KB – 1.2 MB and 17–72 ms for every other screen in the same run. The push
        // finished before the screen had built.
        //
        // TWELVE, ARRIVED AT BY MEASURING — the spec proposed 40, and 40 does not fit the budget it
        // also asks for. Measured on the shipped build:
        //
        //     40 rows, one frame      72.8 MB   4 frames   195.8 ms
        //     40 rows, 8 per frame    32.2 MB   6 frames   100.5 ms
        //
        // both against a < 20 MB / < 50 ms / >= 10 frame gate. Chunking alone cannot close it, and
        // the reason matters: A13 measures the PUSH window, so slowing the fill moves cost OUT of
        // the window without the player's experience changing. The quantity that actually costs is
        // the number of `BagClubCard`s built, and `BagClubCard` is out of scope to change — so the
        // page is smaller, which reduces the work rather than redistributing it. Twelve rows is
        // still roughly double a viewport, and the scroll-append covers the rest.
        private const int PageSize = 12;

        /// <summary>
        /// How many rows one frame is allowed to build.
        ///
        /// <para>MEASURED, and the measurement corrected an estimate. Dividing a single-frame total
        /// by 40 suggested 5.1 ms per row; building eight per frame showed the true figure is
        /// <b>12.6 ms</b>, because adding children to a layout-driven <c>ScrollRect</c> forces a
        /// content-wide layout rebuild on every frame that adds any. Three rows is ≈ 38 ms, inside
        /// the &lt; 50 ms budget; eight was 100 ms.</para>
        /// </summary>
        private const int RowsPerFrame = 3;

        /// <summary>The in-flight page fill, so a rebuild can cancel a fill that is still running
        /// instead of interleaving two of them into the same content.</summary>
        private Coroutine? _fill;

        /// <summary>How many RECORDS (not child objects) are currently rendered.</summary>
        private int _renderedCount;

        /// <summary>
        /// The record instance that was first in the list at the last render, held BY REFERENCE.
        ///
        /// <para>This is the whole prepend-vs-rebuild discriminator, and reference identity is not a
        /// shortcut here — it is exact. <see cref="GachaHistoryStore.Prepend"/> builds a new list of
        /// the new prizes and then `AddRange(All)`, so every pre-existing record is the SAME object;
        /// <see cref="GachaHistoryStore.Refresh"/> replaces the list wholesale via `Map(page)`, so
        /// every record is a new object. Finding this instance at index k therefore means "k rows
        /// were added at the head and nothing else moved", and not finding it means the log was
        /// replaced. No field-by-field comparison, and no guessing.</para>
        /// </summary>
        private GachaHistoryRecord? _firstRenderedRecord;

        /// <summary>Resolved from the content's parents, so paging needs no new serialized field —
        /// and therefore no scene edit, which is what keeps this fix to two scripts.</summary>
        private ScrollRect? _scrollRect;

        /// <summary>Re-entrancy guard: appending grows the content, which fires
        /// <c>onValueChanged</c> again before the new rows have laid out.</summary>
        private bool _appending;

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

            if (_scrollRect == null && _scrollContent != null)
                _scrollRect = _scrollContent.GetComponentInParent<ScrollRect>();
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrolled);
                _scrollRect.onValueChanged.AddListener(OnScrolled);
            }

            RebuildList();                      // the FIRST paint is the rest state: no motion
            GachaHistoryStore.Refresh();
        }

        private void OnDisable()
        {
            GachaHistoryStore.OnChanged -= RepaintAnimated;
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrolled);
            // Disabling the object stops the coroutine anyway; clearing the handle keeps the
            // "is a fill running" flag honest for the next OnEnable.
            _fill = null;
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
        private void RepaintAnimated()
        {
            int prepend = PrependCount(GachaHistoryStore.All, _firstRenderedRecord);

            if (prepend == 0) return;           // the log did not change shape — nothing to draw

            if (prepend > 0)
            {
                // A pull just landed. Insert those rows at the top; the ~1 000 below them are
                // already on screen and correct, and destroying them to build them again is the
                // entire cost this fix exists to remove. No fade: nothing is being replaced.
                PrependRows(prepend);
                Debug.Log($"[GachaHistoryScreenController] prepend {prepend}");
                return;
            }

            // The server replaced the log. Rebuild the FIRST PAGE only — never all of it.
            Debug.Log("[GachaHistoryScreenController] rebuild");
            UiSelection.FadeSwap(this, ListGroup(), RebuildList);
        }

        /// <summary>
        /// How many records were added at the head, or −1 when the list must be rebuilt.
        ///
        /// <para>0 means "unchanged shape"; k &gt; 0 means the first k are new and everything after
        /// them is the same object graph that is already on screen. Static and record-typed so the
        /// decision is testable without a scene (`GachaHistoryPagingTests`).</para>
        /// </summary>
        internal static int PrependCount(IReadOnlyList<GachaHistoryRecord> all,
                                         GachaHistoryRecord? firstRendered)
        {
            if (firstRendered == null) return -1;          // nothing rendered yet ⇒ rebuild
            if (all == null || all.Count == 0) return -1;  // emptied ⇒ rebuild

            for (int i = 0; i < all.Count; i++)
                if (ReferenceEquals(all[i], firstRendered))
                    return i;                              // 0 = unchanged, k = k new at the head

            return -1;                                     // replaced wholesale ⇒ rebuild
        }

        /// <summary>The exclusive end index of the next page — clamped to the record count, so the
        /// last page is short rather than out of range.</summary>
        internal static int NextPageEnd(int rendered, int total, int pageSize)
        {
            if (pageSize <= 0) return rendered;
            int end = rendered + pageSize;
            return end > total ? total : end;
        }

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

        /// <summary>Draws the FIRST PAGE. Named `RebuildList` still because that is what every
        /// caller means by it — the difference is that it now stops at <see cref="PageSize"/>.</summary>
        private void RebuildList()
        {
            if (_scrollContent == null)
            {
                Debug.LogWarning("[GachaHistoryScreenController] _scrollContent not wired.");
                return;
            }

            // Cancel a fill that is still running — otherwise it keeps spawning rows into content
            // that is being cleared, and the two interleave.
            if (_fill != null) { StopCoroutine(_fill); _fill = null; }

            // Destroy existing dynamic rows (keep any authored children that are NOT rows).
            // Safest: just clear everything and respawn.
            foreach (Transform child in _scrollContent)
                Destroy(child.gameObject);

            _renderedCount = 0;
            _firstRenderedRecord = null;

            AppendPage();
        }

        /// <summary>
        /// Renders the next <see cref="PageSize"/> records after whatever is already drawn.
        ///
        /// <para>The divider is spawned BEFORE each row except the very first, rather than after
        /// each row except the last. The rendered sequence is identical — row, divider, row,
        /// divider, row — but it makes appending a straight continuation instead of needing to go
        /// back and add a divider after the row that used to be last.</para>
        /// </summary>
        private void AppendPage()
        {
            if (_scrollContent == null) return;

            int end = NextPageEnd(_renderedCount, GachaHistoryStore.All.Count, PageSize);
            if (end <= _renderedCount) return;

            if (_fill != null) StopCoroutine(_fill);
            _fill = StartCoroutine(FillTo(end));
        }

        /// <summary>
        /// Builds rows up to <paramref name="end"/>, <see cref="RowsPerFrame"/> per frame.
        ///
        /// <para><c>_renderedCount</c> advances as it goes rather than at the end, so a scroll that
        /// arrives mid-fill sees the true count and does not start a second page on top of this
        /// one. The record list is re-read every chunk: a <c>Refresh</c> landing mid-fill shortens
        /// or replaces it, and walking a stale snapshot would index off the end of the new one.</para>
        /// </summary>
        private IEnumerator FillTo(int end)
        {
            int spawnedThisFrame = 0;

            while (_renderedCount < end)
            {
                var records = GachaHistoryStore.All;
                if (_renderedCount >= records.Count) break;   // the log shrank under us

                int i = _renderedCount;
                if (i > 0 && _dividerPrefab != null)
                    Instantiate(_dividerPrefab, _scrollContent);
                SpawnRow(records[i]);

                _renderedCount = i + 1;
                _firstRenderedRecord = records[0];

                if (++spawnedThisFrame >= RowsPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;                       // hand the frame back
                }
            }

            _fill = null;
        }

        /// <summary>Inserts the <paramref name="count"/> newest records above everything already
        /// drawn, each followed by a divider so the row that used to be first gains one.</summary>
        private void PrependRows(int count)
        {
            if (_scrollContent == null) return;

            var records = GachaHistoryStore.All;
            int insertAt = 0;

            for (int i = 0; i < count && i < records.Count; i++)
            {
                var rowGo = SpawnRow(records[i]);
                if (rowGo != null) rowGo.transform.SetSiblingIndex(insertAt++);

                if (_dividerPrefab != null)
                {
                    var div = Instantiate(_dividerPrefab, _scrollContent);
                    div.transform.SetSiblingIndex(insertAt++);
                }
            }

            _renderedCount += count;
            _firstRenderedRecord = records.Count > 0 ? records[0] : null;
        }

        /// <summary>Appends the next page once the list is scrolled to the bottom. No button and no
        /// new string — reaching the end IS the request for more.</summary>
        private void OnScrolled(Vector2 _)
        {
            if (_appending || _scrollRect == null) return;
            if (_fill != null) return;                       // a page is still building
            if (_renderedCount >= GachaHistoryStore.All.Count) return;
            if (_scrollRect.verticalNormalizedPosition > 0.02f) return;

            _appending = true;
            int before = _renderedCount;
            int target = NextPageEnd(before, GachaHistoryStore.All.Count, PageSize);
            AppendPage();
            Debug.Log($"[GachaHistoryScreenController] append {before} -> {target} " +
                      $"of {GachaHistoryStore.All.Count}");
            _appending = false;
        }

        private GameObject? SpawnRow(GachaHistoryRecord record)
        {
            switch (record.RewardType)
            {
                case GachaRewardType.Club:
                {
                    if (_clubRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _clubRowPrefab not wired.");
                        return null;
                    }
                    var go = Instantiate(_clubRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRow>();
                    if (row == null) row = go.AddComponent<GachaHistoryRow>();
                    row.Bind(record);
                    return go;
                }
                case GachaRewardType.Ball:
                {
                    if (_ballRowPrefab == null)
                    {
                        Debug.LogWarning("[GachaHistoryScreenController] _ballRowPrefab not wired.");
                        return null;
                    }
                    var go = Instantiate(_ballRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRowBall>();
                    if (row == null) row = go.AddComponent<GachaHistoryRowBall>();
                    row.Bind(record);
                    return go;
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
                        return null;
                    }
                    var go = Instantiate(_clubRowPrefab, _scrollContent);
                    var row = go.GetComponent<GachaHistoryRow>();
                    if (row == null) row = go.AddComponent<GachaHistoryRow>();
                    row.BindGeneric(record, ResolveName(record), ResolveRarityLine(record));
                    return go;
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
