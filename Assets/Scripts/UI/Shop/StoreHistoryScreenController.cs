// Assets/Scripts/UI/Shop/StoreHistoryScreenController.cs
// store_history §4 — the STORE pillar's purchase log.
//
// A COPY OF GachaHistoryScreenController'S PAGING MODEL, DELIBERATELY (SPEC §4: "Copy, don't
// generalise"). Same constants, same discriminator, same gate. That controller is perf-tuned
// against a measured budget and `GachaHistoryPagingTests` pins it; folding a second screen into it
// would put both under one set of assumptions, and the two lists do NOT share the one that
// matters — the gacha log is up to ~1 000 flattened prize rows, this one is at most 100 purchases.
// Paging rationale (why 12, why 3 per frame): see GachaHistoryScreenController.
//
// WHAT IS GENUINELY DIFFERENT HERE, and why:
//   * ONE row prefab. Every category draws on the same StoreHistoryRow through
//     GachaPrizeCardBinder, so there is no per-kind switch to make.
//   * THE CHIPS ARE WIRED. §D3 names a filter change as the fade site, and the gacha controller's
//     own comment says the chips "will route through here and inherit the fade when wired". This
//     is that site: a chip tap is a FadeSwap + PaintKind.Repaint, never a shimmer — the records
//     are already in memory and a placeholder over them would be a loading animation over data
//     that never left.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI;
using Golfin.UI.Polish;

namespace GolfinRedux.UI.Shop
{
    /// <summary>Attached to the root of StoreHistoryScreen.prefab (or its scene instance).</summary>
    public class StoreHistoryScreenController : MonoBehaviour
    {
        private const string CatRowPath = "GameScreenContent/ContentContainer/FiltersBlock/CategoryRow";

        /// <summary>The Rewards Center's own chip tokens — the two strips must not drift apart.</summary>
        private static readonly Color ChipGold  = new Color32(0xEB, 0xD1, 0x70, 0xFF);
        private static readonly Color ChipWhite = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

        [Header("Prefabs")]
        [SerializeField] private GameObject? _rowPrefab;
        [SerializeField] private GameObject? _dividerPrefab;

        [Header("Scroll")]
        [SerializeField] private RectTransform? _scrollContent;

        [Header("Close")]
        [SerializeField] private Button? _closeButton;

        // ── Paging — see GachaHistoryScreenController for the measurements ──────
        private const int PageSize = 12;
        private const int RowsPerFrame = 3;

        /// <summary>The in-flight page fill, so a rebuild can cancel a fill that is still running
        /// instead of interleaving two of them into the same content.</summary>
        private Coroutine? _fill;

        /// <summary>How many RECORDS (not child objects) are currently rendered.</summary>
        private int _renderedCount;

        /// <summary>
        /// The record instance that was first in the FILTERED list at the last render, held BY
        /// REFERENCE — the prepend-vs-rebuild discriminator.
        ///
        /// <para>Reference identity is exact, not a shortcut: <see cref="StoreHistoryStore.Prepend"/>
        /// builds a new list whose tail is `AddRange(All)`, so every pre-existing record is the SAME
        /// object; <see cref="StoreHistoryStore.Refresh"/> replaces the list wholesale via
        /// `Map(page)`, so every record is a new object. Finding this instance at index k means "k
        /// rows were added at the head and nothing else moved".</para>
        ///
        /// <para>It runs against the FILTERED view, which is what makes the chips correct: a club
        /// bought while CLUBS is active prepends 1, and the same purchase made while TICKETS is
        /// active is simply not in that view, so the count is 0 and nothing is drawn — which is the
        /// behaviour the acceptance list asks for.</para>
        /// </summary>
        private StoreHistoryRecord? _firstRenderedRecord;

        /// <summary>Resolved from the content's parents, so paging needs no new serialized field.</summary>
        private ScrollRect? _scrollRect;

        /// <summary>Re-entrancy guard: appending grows the content, which fires
        /// <c>onValueChanged</c> again before the new rows have laid out.</summary>
        private bool _appending;

        /// <summary>
        /// The chip filter. null = ALL.
        ///
        /// <para>STATIC, so it survives leaving the screen — the nav_back_memory F10 posture the
        /// Rewards Center's own chip row has. A player who filtered to CLUBS, tapped into the
        /// Rewards Center and came back is still looking at clubs.</para>
        /// </summary>
        private static ShopCategory? _activeCategory;

        private readonly Golfin.Gps.UI.PaintGate _gate =
            new Golfin.Gps.UI.PaintGate("[StoreHistory]", GameShimmerSites.StoreHistory);

        /// <summary>Whether the fill now running is the one allowed to stagger. A field because the
        /// rows are spawned across frames, so the verdict is reached long before there is anything
        /// to animate.</summary>
        private bool _staggerThisFill;

        /// <summary>Rows spawned by the current fill, in order, for the §D6 stagger.</summary>
        private readonly List<Transform> _fillRows = new List<Transform>();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_closeButton != null)
                _closeButton.onClick.AddListener(OnClose);

            WireChip("ALLChip",        null);
            WireChip("TICKETSChip",    ShopCategory.Ticket);
            WireChip("CLUBSChip",      ShopCategory.Club);
            WireChip("CHARACTERSChip", ShopCategory.Character);
            WireChip("BALLSChip",      ShopCategory.Ball);
            WireChip("ITEMSChip",      ShopCategory.Item);
        }

        private void OnEnable()
        {
            _gate.Rearm();

            // Draw the disk mirror immediately, then re-draw when the server answers. The screen
            // never waits on a socket — an offline open shows the last log the server confirmed
            // rather than an empty list that reads as "you have never bought anything".
            StoreHistoryStore.OnChanged += RepaintAnimated;

            if (_scrollRect == null && _scrollContent != null)
                _scrollRect = _scrollContent.GetComponentInParent<ScrollRect>();
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrolled);
                _scrollRect.onValueChanged.AddListener(OnScrolled);
            }

            RestyleChips();
            RebuildList(Golfin.Gps.UI.PaintKind.Cache);   // the FIRST paint is the rest state: no motion
            StoreHistoryStore.Refresh();
        }

        private void OnDisable()
        {
            StoreHistoryStore.OnChanged -= RepaintAnimated;
            if (_scrollRect != null) _scrollRect.onValueChanged.RemoveListener(OnScrolled);
            _fill = null;
        }

        private void OnDestroy()
        {
            if (_closeButton != null)
                _closeButton.onClick.RemoveListener(OnClose);
        }

        // ── Filter chips (§D3) ─────────────────────────────────────────────────

        private void WireChip(string chipName, ShopCategory? category)
        {
            var btn = transform.Find($"{CatRowPath}/{chipName}")?.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[StoreHistoryScreenController] chip '{chipName}' not found at {CatRowPath}.");
                return;
            }
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (_activeCategory == category) return;   // re-tapping the lit chip is not a repaint
                _activeCategory = category;
                RestyleChips();
                // §D3 — the list FADES out, repaints through the new filter and fades back in.
                // PaintKind.Repaint, never Fetch: nothing was fetched, so nothing shimmers.
                UiSelection.FadeSwap(this, ListGroup(),
                                     () => RebuildList(Golfin.Gps.UI.PaintKind.Repaint));
            });
        }

        private void RestyleChips()
        {
            SetChipActive("ALLChip",        _activeCategory == null);
            SetChipActive("TICKETSChip",    _activeCategory == ShopCategory.Ticket);
            SetChipActive("CLUBSChip",      _activeCategory == ShopCategory.Club);
            SetChipActive("CHARACTERSChip", _activeCategory == ShopCategory.Character);
            SetChipActive("BALLSChip",      _activeCategory == ShopCategory.Ball);
            SetChipActive("ITEMSChip",      _activeCategory == ShopCategory.Item);
        }

        private void SetChipActive(string chipName, bool active)
        {
            var lbl = transform.Find($"{CatRowPath}/{chipName}/Label")?.GetComponent<TextMeshProUGUI>();
            if (lbl != null) lbl.color = active ? ChipGold : ChipWhite;
        }

        /// <summary>The records the list is currently showing — the whole log, or one category of
        /// it. Everything that counts, indexes or pages reads THIS, never
        /// <see cref="StoreHistoryStore.All"/>, or a filtered list would page against an unfiltered
        /// total.</summary>
        private IReadOnlyList<StoreHistoryRecord> Records =>
            _activeCategory == null
                ? StoreHistoryStore.All
                : StoreHistoryStore.Filter(r => r.Category == _activeCategory);

        // ── Repaint ────────────────────────────────────────────────────────────

        private void RepaintAnimated()
        {
            var records = Records;
            int prepend = PrependCount(records, _firstRenderedRecord);

            if (prepend == 0) return;           // the log did not change shape — nothing to draw

            if (prepend > 0)
            {
                // A purchase just landed. Insert those rows at the top; everything below them is
                // already on screen and correct.
                PrependRows(records, prepend);
                Debug.Log($"[StoreHistoryScreenController] prepend {prepend}");
                return;
            }

            // The server replaced the log. Rebuild the FIRST PAGE only — never all of it.
            Debug.Log("[StoreHistoryScreenController] rebuild");
            UiSelection.FadeSwap(this, ListGroup(), () => RebuildList(Golfin.Gps.UI.PaintKind.Fetch));
        }

        /// <summary>
        /// How many records were added at the head, or −1 when the list must be rebuilt.
        ///
        /// <para>0 means "unchanged shape"; k &gt; 0 means the first k are new and everything after
        /// them is the same object graph that is already on screen. Static and record-typed so the
        /// decision is testable without a scene (<c>StoreHistoryPagingTests</c>).</para>
        /// </summary>
        internal static int PrependCount(IReadOnlyList<StoreHistoryRecord> all,
                                         StoreHistoryRecord? firstRendered)
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

        private void RebuildList(Golfin.Gps.UI.PaintKind kind)
        {
            if (_scrollContent == null)
            {
                Debug.LogWarning("[StoreHistoryScreenController] _scrollContent not wired.");
                // An arm that ends the paint must end the WAIT too — unwired content is never
                // going to be filled, so a placeholder over it would stay for the whole session.
                _gate.Should(kind, 0);
                Golfin.Gps.UI.GpsPaintMotion.Shimmer(gameObject, GameShimmerSites.StoreHistory, cold: false);
                return;
            }

            // The verdict comes from the RECORD count, not the rendered count: the rows do not
            // exist yet (FillTo spawns them over several frames) and a gate asked "how many rows
            // are on screen?" here would always hear zero and call every paint cold.
            _staggerThisFill = _gate.Should(kind, Records.Count);
            Golfin.Gps.UI.GpsPaintMotion.Shimmer(gameObject, GameShimmerSites.StoreHistory, _gate.IsCold);
            _fillRows.Clear();

            // Cancel a fill that is still running — otherwise it keeps spawning rows into content
            // that is being cleared, and the two interleave.
            if (_fill != null) { StopCoroutine(_fill); _fill = null; }

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
        /// each row except the last. The rendered sequence is identical, but it makes appending a
        /// straight continuation instead of needing to go back and add a divider after the row that
        /// used to be last.</para>
        /// </summary>
        private void AppendPage()
        {
            if (_scrollContent == null) return;

            int end = NextPageEnd(_renderedCount, Records.Count, PageSize);
            if (end <= _renderedCount) return;

            if (_fill != null) StopCoroutine(_fill);
            _fill = StartCoroutine(FillTo(end));
        }

        /// <summary>
        /// Builds rows up to <paramref name="end"/>, <see cref="RowsPerFrame"/> per frame.
        ///
        /// <para><c>_renderedCount</c> advances as it goes rather than at the end, so a scroll that
        /// arrives mid-fill sees the true count and does not start a second page on top of this
        /// one. The record list is re-read every row: a <c>Refresh</c> landing mid-fill shortens or
        /// replaces it, and walking a stale snapshot would index off the end of the new one.</para>
        /// </summary>
        private IEnumerator FillTo(int end)
        {
            int spawnedThisFrame = 0;

            while (_renderedCount < end)
            {
                var records = Records;
                if (_renderedCount >= records.Count) break;   // the log shrank under us

                int i = _renderedCount;
                if (i > 0 && _dividerPrefab != null)
                    Instantiate(_dividerPrefab, _scrollContent);
                GameObject? spawned = SpawnRow(records[i]);
                if (_staggerThisFill && spawned != null) _fillRows.Add(spawned.transform);

                _renderedCount = i + 1;
                _firstRenderedRecord = records[0];

                if (++spawnedThisFrame >= RowsPerFrame)
                {
                    spawnedThisFrame = 0;
                    yield return null;                       // hand the frame back
                }
            }

            _fill = null;

            // §D6 — stagger the page that just landed, once, at the end. Doing it per row as they
            // spawn would fight FillTo's own frame budget.
            if (_staggerThisFill && _fillRows.Count > 0)
            {
                Golfin.Gps.UI.GpsPaintMotion.StaggerRise(this, _fillRows);
                _staggerThisFill = false;      // page 1 only; page 2 is a scroll, not an arrival
            }
        }

        /// <summary>Inserts the <paramref name="count"/> newest records above everything already
        /// drawn, each followed by a divider so the row that used to be first gains one.</summary>
        private void PrependRows(IReadOnlyList<StoreHistoryRecord> records, int count)
        {
            if (_scrollContent == null) return;

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
            int total = Records.Count;
            if (_renderedCount >= total) return;
            if (_scrollRect.verticalNormalizedPosition > 0.02f) return;

            _appending = true;
            int before = _renderedCount;
            int target = NextPageEnd(before, total, PageSize);
            AppendPage();
            Debug.Log($"[StoreHistoryScreenController] append {before} -> {target} of {total}");
            _appending = false;
        }

        /// <summary>ONE row prefab for every category — <see cref="StoreHistoryRow"/> binds through
        /// <c>GachaPrizeCardBinder</c>, which already draws all five kinds on one card shell.</summary>
        private GameObject? SpawnRow(StoreHistoryRecord record)
        {
            if (_rowPrefab == null)
            {
                Debug.LogWarning("[StoreHistoryScreenController] _rowPrefab not wired.");
                return null;
            }

            var go = Instantiate(_rowPrefab, _scrollContent);
            var row = go.GetComponent<StoreHistoryRow>();
            if (row == null) row = go.AddComponent<StoreHistoryRow>();
            row.Bind(record);
            return go;
        }

        // ── Close ──────────────────────────────────────────────────────────────

        // nav_back_memory §3 — history first, the Rewards Center as the fallback. Identical to
        // GachaHistory: the remembered STORE tab is where the player came from.
        private void OnClose()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.GoBack(ScreenId.GeneralShop);
            else
                Debug.LogWarning("[StoreHistoryScreenController] ScreenManager not found.");
        }
    }
}
