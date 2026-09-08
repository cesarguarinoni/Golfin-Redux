// Assets/Scripts/UI/Gacha/GachaCarouselController.cs
// gacha_screen Stage 2 — §3c Carousel + Countdown driver
// Horizontal drag/swipe, snap-to-center, INFINITE WRAP, distance-based scale/alpha falloff.
// ONE Update ticker for countdown and position lerp (not per-card coroutines).
// Dot indicators: dynamic count = live banners, center = active index.
// On expiry: RemoveBanner, rebuild dots, snap to nearest live; zero live → EmptyState.

using System;
using System.Collections.Generic;
using Golfin.Content;
using GolfinRedux.UI.Gacha;
using Golfin.Telemetry;
using Golfin.UI.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Drives the Gacha banner carousel in GachaTabContent.
    /// Attach to the GachaTabContent GameObject.
    /// Spawns one GachaBannerCard per live banner; manages positions, falloff, dots, countdown.
    /// </summary>
    public class GachaCarouselController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // ── Inspector refs ────────────────────────────────────────────────────

        [Header("Spawning")]
        [SerializeField] private GameObject _cardPrefab;       // GachaBannerCard.prefab
        [SerializeField] private Transform  _dotContainer;     // DotRow
        [SerializeField] private GameObject _dotPrefab;        // reused dot child (cloned for each banner)
        [SerializeField] private Sprite     _dotSprite;        // circular dot sprite (Dot Active.png) — applied to every spawned dot
        [SerializeField] private GameObject _emptyState;       // "No active banners" GO

        [Header("Card Layout")]
        [Tooltip("Horizontal gap between card centres (px).")]
        [SerializeField] private float _cardSpacing  = 800f;
        [Tooltip("Y offset for all card anchored positions.")]
        [SerializeField] private float _cardYOffset  = 42f;

        [Header("Falloff")]
        [Tooltip("Scale of side cards (0–1). 1 = same size as centre.")]
        [SerializeField] private float _sideScale    = 0.78f;
        [Tooltip("Alpha of side cards (0–1). 1 = fully opaque.")]
        [SerializeField] private float _sideAlpha    = 0.45f;

        [Header("Snap / Drag")]
        [Tooltip("Snap lerp speed (per-frame).")]
        [SerializeField] private float _snapSpeed    = 10f;
        [Tooltip("Min drag distance (px) to advance the index.")]
        [SerializeField] private float _dragThreshold = 80f;

        [Tooltip("Carousel wraps: swiping past the last banner continues onto the first, and past " +
                 "the first back onto the last, with no end stop. Needs at least two banners to " +
                 "mean anything. Defaults ON — a serialized bool rather than a constant so the " +
                 "behaviour can be turned off from the Inspector without a code change.")]
        [SerializeField] private bool _loop = true;

        // ── Internal state ────────────────────────────────────────────────────

        private readonly List<GachaBannerCard> _cards     = new();
        private readonly List<GachaBannerEntry> _entries  = new();
        private PaginationDotStrip              _dots;
        private int   _currentIndex  = 0;
        private float _currentOffset = 0f;   // continuous scroll position (canvas units)
        private float _targetOffset  = 0f;   // snap target scroll (nearest card * spacing)
        private float _dragStartX    = 0f;
        private float _dragStartScroll = 0f; // scroll position when the drag began
        private bool  _isDragging    = false;

        // ── Countdown update interval ──────────────────────────────────────────
        private float _countdownTimer = 0f;
        private const float CountdownInterval = 1f; // update text every second

        // ── Wrapping ──────────────────────────────────────────────────────────
        //
        // The carousel is a RING, not a strip. Card i sits at `i * spacing`, so the whole set
        // repeats every `count * spacing` — and if a card's offset from the centre is reduced
        // modulo that span into (-span/2, +span/2], every card is drawn at its NEAREST copy.
        // The last card is then one slot to the LEFT of the first, exactly as if there were an
        // endless run of them in both directions, and no card is ever cloned to achieve it.
        //
        // Scroll position is therefore unbounded while a gesture is in flight — that is what makes
        // the wrap feel continuous instead of snapping round — and is re-based to [0, span) the
        // moment it settles, so a long session cannot walk `_currentOffset` out to a magnitude
        // where a float stops resolving single pixels.

        /// <summary>The scroll distance after which the ring repeats.</summary>
        private float Span => _cards.Count * _cardSpacing;

        /// <summary>Whether the ring is closed. One banner has no ring — with a single card every
        /// position is the same position, and a "wrap" would be a swipe that never moves.</summary>
        private bool Wraps => _loop && _cards.Count >= 2 && _cardSpacing > 0f;

        /// <summary>Reduce a signed distance to the nearest equivalent on the ring.</summary>
        private float WrapDelta(float d) => Wraps ? WrapOnRing(d, _cards.Count, _cardSpacing) : d;

        /// <summary>
        /// The ring reduction itself, as a pure function of its inputs — <c>internal static</c> so
        /// the EditMode suite exercises THIS, the code that ships, rather than a mirror of it that
        /// can drift (feedback_tests_must_target_production_type). A count below two or a
        /// non-positive spacing is not a ring and is returned untouched.
        /// </summary>
        internal static float WrapOnRing(float delta, int count, float spacing)
        {
            if (count < 2 || spacing <= 0f) return delta;
            float span = count * spacing;
            return Mathf.Repeat(delta + span * 0.5f, span) - span * 0.5f;
        }

        /// <summary>Positive modulo — C#'s % keeps the sign of the dividend, so a scroll that has
        /// run left of zero would index backwards off the list. Internal for the same reason.</summary>
        internal static int Mod(int a, int n) => n <= 0 ? 0 : ((a % n) + n) % n;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        /// <summary>
        /// The carousel currently on screen, so <c>GachaPullFlow</c> can rebuild it when the server
        /// refuses a banner this build was still showing (gacha_client_real_pull §4.2).
        /// Null whenever the Rewards Center is not open, which is the normal state.
        /// </summary>
        public static GachaCarouselController Instance { get; private set; }

        // §3 — one gacha_banner_view per banner per Rewards Center open. The set is cleared on
        // OnEnable, which IS "per open": the screen is deactivated when the player leaves.
        private readonly HashSet<string> _viewedThisOpen = new();

        private void OnEnable()
        {
            Instance = this;
            _viewedThisOpen.Clear();

            // gacha_ops_polish §4c — ASK for a fresh catalog on the way in. Without this the fetch
            // ran once, at boot, so 5b's live re-install could only ever see a publish that landed
            // before launch. Throttled to one request a minute and off the critical path, so it
            // costs this open nothing: the reload below still serves whatever is already cached,
            // and the newer one lands on the NEXT open.
            ContentService.Instance?.RefreshNow();

            // Reload() is ALSO where the 5b same-session re-apply happens: a content refresh that
            // landed while the player was elsewhere is installed here, so a banner published
            // mid-session appears the next time the Rewards Center opens.
            GachaBannerCatalog.Reload();
            RebuildCarousel();

            // The counter above the carousel must be the LEDGER's number, not the last one this
            // client happened to write — a pull on another device, or an admin grant, moves it with
            // nothing local to notice (gacha_client_real_pull §4.4).
            GachaTicketManager.Instance?.RefreshFromServer();

            // polish_regressions_0909 R4 — repaint a card when its art finally lands.
            Golfin.Tournaments.TournamentArtService.CatalogArt.ArtCached += OnCatalogArtCached;
        }

        private void OnDisable()
        {
            Golfin.Tournaments.TournamentArtService.CatalogArt.ArtCached -= OnCatalogArtCached;
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        /// <summary>
        /// A banner's `artUrl` has just finished downloading — re-bind the card (or cards) drawing
        /// it, and nothing else.
        ///
        /// <para>
        /// WHY THIS EXISTS: <c>GachaBannerCatalog</c> warms the cache with a fire-and-forget
        /// <c>Prefetch</c> at catalog load, so a newly published banner — or one whose art was
        /// re-uploaded, which mints a NEW url because the bucket filename is content-hashed —
        /// resolved to nothing on the launch it appeared and drew its bundled `artSprite`
        /// placeholder. Nothing rebound it when the bytes arrived a few hundred ms later, so the
        /// real art could not show up until the NEXT launch. Now it shows up on this one.
        /// </para>
        /// <para>
        /// ONE CARD, NOT A REBUILD. <see cref="RebuildCarousel"/> destroys and re-spawns the whole
        /// strip and resets the scroll — visible, and actively hostile if the player is mid-swipe.
        /// <c>Bind</c> is idempotent and re-reads every slot from the same entry, so re-binding the
        /// matching card swaps the sprite and changes nothing else.
        /// </para>
        /// </summary>
        private void OnCatalogArtCached(string url)
        {
            if (string.IsNullOrEmpty(url)) return;

            int rebound = 0;
            foreach (var card in _cards)
            {
                if (card == null || card.Entry == null) continue;
                if (!string.Equals(card.Entry.ArtUrl, url, StringComparison.Ordinal)) continue;

                card.Bind(card.Entry);
                rebound++;
            }

            if (rebound > 0)
            {
                Debug.Log($"[GachaCarousel] Banner art arrived — re-bound {rebound} card(s) to the " +
                          $"downloaded sprite instead of the bundled placeholder: {url}");
                return;
            }

            // No card draws this url. It may still belong to a banner the catalog is WITHHOLDING
            // for want of art: a banner published since this build shipped has no bundled
            // `artSprite`, so GachaBannerArt.Resolve returned null, §3.1 withheld it, and it has
            // no card to re-bind. Now that its bytes are here it is rollable, and the only way it
            // can appear is a rebuild — the strip has to GAIN a card, which no re-bind can do.
            //
            // Guarded on the url actually belonging to a banner, so art landing for any other
            // catalog (characters, clubs, the shop) never rebuilds this strip.
            foreach (var entry in GachaBannerCatalog.Entries)
            {
                if (entry == null || !string.Equals(entry.ArtUrl, url, StringComparison.Ordinal)) continue;

                Debug.Log($"[GachaCarousel] Art arrived for '{entry.BannerId}', which was withheld for " +
                          "want of it — rebuilding the strip so the banner appears this launch.");
                RebuildCarousel();
                return;
            }
        }

        /// <summary>Re-read the catalog and rebuild the strip. Called after the server has told the
        /// client its copy of the catalog is stale.</summary>
        public void Rebuild()
        {
            GachaBannerCatalog.Reload();
            RebuildCarousel();
        }

        private void Update()
        {
            // Continuous scroll: ease to the snap target only when not actively dragging.
            if (!_isDragging)
            {
                _currentOffset = Mathf.Lerp(_currentOffset, _targetOffset, Time.deltaTime * _snapSpeed);

                // Re-base the ring ONLY once the ease has arrived. Doing it mid-lerp would move the
                // target the lerp is chasing and the cards would jump a whole span in one frame.
                if (Wraps && Mathf.Abs(_currentOffset - _targetOffset) < 0.01f)
                {
                    float span = Span;
                    float turns = Mathf.Floor(_targetOffset / span);
                    if (!Mathf.Approximately(turns, 0f))
                    {
                        _targetOffset  -= turns * span;
                        _currentOffset -= turns * span;
                    }
                }
            }
            UpdateCardTransforms();

            // Countdown tick
            _countdownTimer -= Time.deltaTime;
            if (_countdownTimer <= 0f)
            {
                _countdownTimer = CountdownInterval;
                TickCountdown();
            }
        }

        // ── Drag handlers ─────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            _dragStartX = eventData.position.x;
            _dragStartScroll = _currentOffset;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            // Cards follow the finger 1:1 (drag right → scroll decreases → cards slide right).
            float delta = eventData.position.x - _dragStartX;
            _currentOffset = _dragStartScroll - delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            // Snap to the nearest card from where the scroll landed — smooth ease, no binary index flip.
            int idx = Mathf.RoundToInt(_cardSpacing > 0f ? _currentOffset / _cardSpacing : 0f);
            if (Wraps)
            {
                // No clamp: the scroll is allowed off the end of the list, and the index it maps to
                // is taken modulo the count. THAT is the whole loop — a swipe left off card 0 snaps
                // to slot -1, which is the last banner, and the ease travels one card's width to
                // reach it rather than the width of the entire strip.
                _targetOffset = idx * _cardSpacing;
                _currentIndex = Mod(idx, _cards.Count);
            }
            else
            {
                idx = Mathf.Clamp(idx, 0, _cards.Count - 1);
                _currentIndex = idx;
                _targetOffset = idx * _cardSpacing;
            }
            UpdateDots();
        }

        // ── Tap-to-centre ─────────────────────────────────────────────────────

        /// <summary>
        /// Tapping a side banner slides it into the centre — the same result as swiping onto it.
        /// The click bubbles up from the card's graphic (the PULL / RULES buttons handle their own
        /// clicks, so they are unaffected); we hit-test the cards to find which one was hit.
        /// </summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            // A swipe that begins and ends over the same card also dispatches a click. The drag is
            // still in flight here (OnEndDrag runs AFTER the click), so the swipe owns the gesture.
            if (_isDragging || eventData.dragging) return;

            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                var rt = _cards[i].GetComponent<RectTransform>();
                if (rt == null) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, eventData.position, eventData.pressEventCamera))
                    continue;

                if (i == _currentIndex) return;   // already centred
                _currentIndex = i;
                // Ease to the card's NEAREST copy on the ring, not to its absolute slot: the player
                // tapped the banner they can see, so it must come in from the side it is on. Its
                // absolute slot could be a whole span away and the card would sail off the other
                // edge to arrive.
                _targetOffset = Wraps
                    ? _currentOffset + WrapDelta(i * _cardSpacing - _currentOffset)
                    : i * _cardSpacing;           // Update()'s lerp eases us there, same as a snap
                UpdateDots();
                return;
            }
        }

        // ── Build / Rebuild ───────────────────────────────────────────────────

        private void RebuildCarousel()
        {
            // Destroy existing cards
            foreach (var c in _cards)
                if (c != null) Destroy(c.gameObject);
            _cards.Clear();
            _entries.Clear();

            var live = GachaBannerCatalog.GetLiveBanners();

            if (live.Count == 0)
            {
                ShowEmptyState(true);
                ClearDots();
                return;
            }

            ShowEmptyState(false);

            foreach (var entry in live)
            {
                _entries.Add(entry);
                var go = Instantiate(_cardPrefab, transform);
                go.name = "BannerCard_" + entry.BannerId;
                SetupCardRefs(go);
                var card = go.GetComponent<GachaBannerCard>();
                card.Bind(entry);
                _cards.Add(card);
            }

            // Clamp current index
            _currentIndex  = Mathf.Clamp(_currentIndex, 0, _cards.Count - 1);
            _currentOffset = _currentIndex * _cardSpacing;
            _targetOffset  = _currentOffset;

            UpdateCardTransforms();
            UpdateDots();
        }

        /// <summary>
        /// Wire all child ref components from the spawned card GO.
        /// Matches GachaBannerCard hierarchy (same as _GachaCard_CesarTuned layout).
        /// </summary>
        private void SetupCardRefs(GameObject go)
        {
            var card = go.GetComponent<GachaBannerCard>();
            if (card == null) return;

            // Use SerializedObject to wire fields so they persist correctly.
            // In runtime we use Unity's GetComponent / Find approach instead.
            // GachaBannerCard.Bind() does its own path-lookup on the GO hierarchy.
            // All field wiring is done via SetField reflection for the prefab refs already wired at author time.
            // For runtime-spawned instances the fields are wired from the prefab; Bind() runs the logic.
        }

        // ── Position / falloff ────────────────────────────────────────────────

        private void UpdateCardTransforms()
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] == null) continue;
                var rt = _cards[i].GetComponent<RectTransform>();
                var cg = _cards[i].GetComponent<CanvasGroup>();
                if (rt == null || cg == null) continue;

                // Position: continuous scroll — card i is at i*spacing minus the scroll position,
                // reduced to its nearest copy on the ring so the strip has no ends (see § Wrapping).
                float targetX = WrapDelta(i * _cardSpacing - _currentOffset);
                rt.anchoredPosition = new Vector2(targetX, _cardYOffset);

                // Falloff: normalised distance from centre (0 = centre, 1 = one card away)
                float t = Mathf.Clamp01(Mathf.Abs(targetX) / _cardSpacing);
                float scale = Mathf.Lerp(1f, _sideScale, t);
                float alpha = Mathf.Lerp(1f, _sideAlpha, t);

                rt.localScale = new Vector3(scale, scale, 1f);
                cg.alpha = alpha;
            }
        }

        // ── Dot indicators ────────────────────────────────────────────────────

        private void UpdateDots()
        {
            // FIRST, and before the dot-container guard: the telemetry is about which banner is
            // centred, not about whether this scene wired a dot strip.
            RecordCentredBannerView();

            if (_dotContainer == null) return;

            // Ensure the circular dot sprite (Resources fallback — the controller lives in the
            // scene, so we avoid a serialized ref + scene save). Cached after first load.
            if (_dotSprite == null)
                _dotSprite = Resources.Load<Sprite>("Art/Gacha/GachaDot");

            // Pooled + windowed. Previously this grew one dot per banner with no ceiling; the strip
            // caps the row and reuses its dots instead of adding and destroying them per refresh.
            _dots ??= new PaginationDotStrip(
                _dotContainer,
                _dotPrefab != null ? _dotPrefab : FirstDotChild(),
                dotSprite: _dotSprite);
            _dots.Rebuild(_cards.Count, _currentIndex);
        }

        /// <summary>
        /// The scene authors an inactive DotTemplate under DotRow. Cloning it keeps the authored
        /// look when no explicit prefab is wired; it stays inactive itself and is never a pool member.
        /// </summary>
        private GameObject FirstDotChild()
            => _dotContainer != null && _dotContainer.childCount > 0
                ? _dotContainer.GetChild(0).gameObject
                : null;

        private void ClearDots()
        {
            // Pooled: the strip owns its dots for the lifetime of the container, so clearing is just
            // collapsing the row to nothing rather than destroying GameObjects.
            _dots?.Clear();
        }

        // ── Countdown ─────────────────────────────────────────────────────────

        private void TickCountdown()
        {
            var now = DateTime.UtcNow;
            bool anyExpired = false;

            for (int i = _cards.Count - 1; i >= 0; i--)
            {
                if (_cards[i] == null) continue;
                var entry = _entries[i];

                if (entry.EndUtc <= now)
                {
                    // Expired — remove
                    Debug.Log($"[GachaCarousel] Banner '{entry.BannerId}' expired. Removing.");
                    Destroy(_cards[i].gameObject);
                    _cards.RemoveAt(i);
                    _entries.RemoveAt(i);
                    anyExpired = true;
                    continue;
                }

                // Update countdown text
                var remaining = entry.EndUtc - now;
                _cards[i].SetCountdownText(FormatCountdown(remaining));
            }

            if (anyExpired)
            {
                if (_cards.Count == 0)
                {
                    ShowEmptyState(true);
                    ClearDots();
                    return;
                }
                // Re-base onto the shortened ring. The span just changed, so a scroll left over
                // from the old one no longer means the card it used to: rebuild it from the index.
                _currentIndex = Mathf.Clamp(_currentIndex, 0, _cards.Count - 1);
                _targetOffset = _currentIndex * _cardSpacing;
                _currentOffset = _targetOffset;
                UpdateDots();
            }
        }

        /// <summary>Format a TimeSpan into "ENDS IN: {d}d {h}h {m}m {ss} s". Public for tests.</summary>
        public static string FormatCountdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
                return "ENDS IN: 0s";

            int totalSeconds = (int)remaining.TotalSeconds;
            int d  = totalSeconds / 86400;
            int h  = (totalSeconds % 86400) / 3600;
            int m  = (totalSeconds % 3600) / 60;
            int s  = totalSeconds % 60;

            if (d > 0)
                return $"ENDS IN: {d}d {h}h {m}m {s:D2}s";
            if (h > 0)
                return $"ENDS IN: {h}h {m}m {s:D2}s";
            if (m > 0)
                return $"ENDS IN: {m}m {s:D2}s";
            return $"ENDS IN: {s:D2}s";
        }

        // ── Empty state ───────────────────────────────────────────────────────

        /// <summary>
        /// <c>gacha_banner_view</c> for whichever card is now centred (gacha_ops_polish §3).
        ///
        /// <para>
        /// It hangs off <see cref="UpdateDots"/> because that is called by EVERY path that changes
        /// which card is centred — the initial build, a swipe, a tap-to-centre and an expiry — and
        /// by nothing else. Wiring it to the three call sites separately is how one of them ends up
        /// missing it.
        /// </para>
        /// <para>
        /// ONCE PER BANNER PER OPEN. A player swiping back and forth is one player looking at two
        /// banners, and counting every pass would inflate the denominator of every conversion rate
        /// on the funnel card by however restless they were.
        /// </para>
        /// </summary>
        private void RecordCentredBannerView()
        {
            if (_currentIndex < 0 || _currentIndex >= _entries.Count) return;

            var entry = _entries[_currentIndex];
            if (entry == null || string.IsNullOrEmpty(entry.BannerId)) return;
            if (!_viewedThisOpen.Add(entry.BannerId)) return;

            int position = _currentIndex;
            int liveCount = _entries.Count;
            TelemetryService.Instance.RecordSafe(TelemetryEventNames.GachaBannerView,
                () => new Dictionary<string, object>
                {
                    ["banner_id"]  = entry.BannerId,
                    ["position"]   = position,
                    ["live_count"] = liveCount,
                });
        }

        private void ShowEmptyState(bool show)
        {
            if (_emptyState != null)
                _emptyState.SetActive(show);
        }
    }
}
