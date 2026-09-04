// ─────────────────────────────────────────────────────────────────────────────
// gps_gifts_votes §Client data bindings — the GPS Gift screen (Figma 14027:101843).
//
// The first GPS screen that SPENDS. Everything above the fold is a read — gift
// earnings, who sent them, who is worth sending to — and the two things that
// write (SEND GIFT, and a tap on a catalog cell) both go through one modal and
// one idempotency key, because a gift that fires twice is a bug the player pays
// for.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Globalization;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class GpsGiftScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsGift]";
        private const string Unknown = "—";

        [Header("Hero")]
        [SerializeField] private TextMeshProUGUI? _heroSub;
        [SerializeField] private TextMeshProUGUI? _heroValue;

        [Header("Top supporters")]
        [Tooltip("Three authored rows, top to bottom. A row with no supporter is deactivated.")]
        [SerializeField] private GameObject[] _supporterRows = new GameObject[0];

        [Header("Popular golfers")]
        [Tooltip("Five authored rows, top to bottom.")]
        [SerializeField] private GameObject[] _golferRows = new GameObject[0];

        [Tooltip("The SEND GIFT button of each golfer row, parallel to _golferRows.")]
        [SerializeField] private Button[] _golferSendButtons = new Button[0];

        [Header("Buy gift items")]
        [SerializeField] private GameObject[] _itemCells = new GameObject[0];
        [SerializeField] private Button[] _itemButtons = new Button[0];

        [Tooltip("The four category glyphs, in Glyph order: Heart, Star, Sparkle, Pin. Wired by " +
                 "the builder so the controller never touches an asset path — none of these live " +
                 "under Resources/, so a runtime load would silently return null and the cell " +
                 "would render an empty ring.")]
        [SerializeField] private Sprite[] _glyphSprites = new Sprite[0];

        [Header("Modal")]
        [SerializeField] private GiftSendModalController? _sendModal;

        /// <summary>The discover rows currently bound to <see cref="_golferRows"/>, so a tap
        /// knows who it is gifting without re-reading the labels.</summary>
        private readonly List<DiscoverUserDto> _golfers = new List<DiscoverUserDto>();

        /// <summary>The catalog rows currently bound to <see cref="_itemCells"/>.</summary>
        private readonly List<GiftItemDto> _items = new List<GiftItemDto>();

        private bool _wired;

        // ── gps_polish §D3 / §D4 / §D7 / §D8 ─────────────────────────────────
        /// <summary>Cache-vs-fetch memory, one per fetched region.</summary>
        private readonly PaintGate _supportersGate = new PaintGate(Tag, "supporters");
        private readonly PaintGate _golfersGate    = new PaintGate(Tag, "golfers");
        /// <summary>The catalog strip has no placeholder and no stagger — its gate exists only so
        /// the BUY GIFT ITEMS panel knows whether this open is a cold one.</summary>
        private readonly PaintGate _itemsGate      = new PaintGate(Tag, "items", staggers: false);

        /// <summary>
        /// §D4 — the three data panels. They FADE IN on a cold open, alongside the placeholder
        /// that stands in for their rows, and are instant on a cache hit. One rule for all three,
        /// and every paint path ends by showing them: a panel that could be stranded at alpha 0
        /// by a failed fetch would be a worse defect than no fade.
        /// </summary>
        private readonly PanelReveal _supportersPanel = new PanelReveal("ContentContainer/Supporters");
        private readonly PanelReveal _golfersPanel    = new PanelReveal("ContentContainer/Golfers");
        private readonly PanelReveal _itemsPanel      = new PanelReveal("ContentContainer/BuyGifts");

        /// <summary>The last GIFTS RECEIVED figure, so §D7 counts from a REAL previous number
        /// rather than parsing one back out of a localized run.</summary>
        private int? _lastGiftPts;

        private Coroutine? _heroCount;

        // ═════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            WireOnce();

            // Paint from cache BEFORE any request, so re-entering never flashes "—" over numbers
            // that were correct a moment ago (the hub's posture, for the same reason).
            _supportersGate.Rearm();
            _golfersGate.Rearm();
            _itemsGate.Rearm();
            _supportersPanel.Rearm(gameObject);
            _golfersPanel.Rearm(gameObject);
            _itemsPanel.Rearm(gameObject);

            ApplyDetail(UserService.Instance.LastDetail);
            ApplyGolfers(UserService.Instance.LastDiscover, PaintKind.Cache);
            ApplyItems(GiftService.Instance.LastItems, PaintKind.Cache);
            ApplySupporters(GiftService.Instance.LastSupporters, PaintKind.Cache);

            UserService.Instance.OnDetailChanged += ApplyDetail;
            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            ApiClient client = ApiClient.Instance;
            client.Run(UserService.Instance.Detail(OnDetailResult));
            client.Run(UserService.Instance.Discover(OnDiscoverResult));
            client.Run(GiftService.Instance.Items(OnItemsResult));
            client.Run(GiftService.Instance.Supporters(OnSupportersResult));

            // The modal reads the SENDABLE balance off PointsService's cache, so it has to be
            // fresh before the player can open it — not after they have picked an amount.
            PointsService.Instance.RefreshBalanceAsync();

            TelemetryService.Instance.RecordSafe("gps_gift_open",
                () => new Dictionary<string, object> { ["source"] = "gps_nav" });
        }

        private void OnDisable()
        {
            UserService.Instance.OnDetailChanged -= ApplyDetail;
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < _golferSendButtons.Length; i++)
            {
                int index = i;
                if (_golferSendButtons[i] == null) continue;
                _golferSendButtons[i].onClick.AddListener(() => OnSendGift(index));
            }

            for (int i = 0; i < _itemButtons.Length; i++)
            {
                int index = i;
                if (_itemButtons[i] == null) continue;
                _itemButtons[i].onClick.AddListener(() => OnBuyItem(index));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Hero
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// GIFTS RECEIVED is <c>profiles.gift_pts</c> — the lifetime total of what other players
        /// have given this account, which is a DIFFERENT number from the RP balance in the top
        /// bar (that one is <c>total_points</c>, and it also counts everything earned by playing).
        /// </summary>
        private void ApplyDetail(UserDetailDto? d)
        {
            if (_heroValue != null)
            {
                int? pts = d != null ? d.GiftPts : null;
                if (!pts.HasValue)
                {
                    _heroValue.text = Unknown;
                    _lastGiftPts = null;
                }
                else
                {
                    // §D7 — count UP, and only up, and only from a number that was really on
                    // screen. The first paint of the session comes from the em dash, and counting
                    // from 0 there would show a player a lifetime gift total climbing every time
                    // they open the screen. A total that went DOWN cannot happen on this column,
                    // but if it ever does it snaps rather than counting backwards.
                    string wrap = LocalizationManager.Get("GPS_GIFT_HERO_VALUE");
                    if (_lastGiftPts.HasValue && pts.Value > _lastGiftPts.Value)
                        UiMotion.Run(this, ref _heroCount,
                                     UiMotion.CountUp(_heroValue, _lastGiftPts.Value, pts.Value,
                                                      wrap: wrap));
                    else
                        _heroValue.text = UiMotion.Render(pts.Value, wrap: wrap);
                    _lastGiftPts = pts.Value;
                }
            }
            RepaintHeroSub();
        }

        /// <summary>"from N supporters" — the count of DISTINCT senders in the aggregation, not a
        /// gift count. Repainted whenever either half changes, because the two arrive from
        /// different requests and whichever lands second must not blank the first.</summary>
        private void RepaintHeroSub()
        {
            if (_heroSub == null) return;
            List<SupporterTotal>? s = GiftService.Instance.LastSupporters;
            _heroSub.text = string.Format(LocalizationManager.Get("GPS_GIFT_HERO_SUB"),
                                          (s != null ? s.Count : 0).ToString(CultureInfo.InvariantCulture));
        }

        private void OnDetailResult(ApiResult<UserDetailDto> result)
        {
            if (result == null || result.Success) return;
            Debug.LogWarning($"{Tag} /user/detail failed ({result.ErrorKind}) — hero stays '{Unknown}'.");
        }

        // ═════════════════════════════════════════════════════════════════════
        // Top supporters
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Bind the top three. An EMPTY list is the normal state on a fresh account and is
        /// rendered as three hidden rows under a live header — not as an error, and not as a
        /// hidden panel: a headline with nothing under it reads better than a hole in the layout
        /// (the same call the hub's rounds panel makes).
        /// </summary>
        /// <summary>The /gifts/received answer — a FETCH paint, unlike the cache paint that
        /// runs the same binder from <c>OnEnable</c>.</summary>
        private void OnSupportersResult(List<SupporterTotal>? supporters)
            => ApplySupporters(supporters, PaintKind.Fetch);

        private void ApplySupporters(List<SupporterTotal>? supporters, PaintKind kind)
        {
            int count = 0;
            var painted = new List<Transform>(_supporterRows.Length);
            if (supporters != null)
            {
                foreach (SupporterTotal s in supporters)
                {
                    if (count >= _supporterRows.Length) break;
                    if (s == null || string.IsNullOrWhiteSpace(s.DisplayName)) continue;
                    BindSupporterRow(_supporterRows[count], s, count);
                    painted.Add(_supporterRows[count].transform);
                    count++;
                }
            }
            for (int i = count; i < _supporterRows.Length; i++)
                if (_supporterRows[i] != null) _supporterRows[i].SetActive(false);

            RepaintHeroSub();

            bool stagger = _supportersGate.Should(kind, count);
            GpsPaintMotion.Shimmer(gameObject, ShimmerHost.Supporters, _supportersGate.IsCold);
            _supportersPanel.Reveal(this, _supportersGate.IsCold);
            if (stagger) GpsPaintMotion.StaggerRise(this, painted);

            Debug.Log($"{Tag} supporters: {count} bound (of {(supporters != null ? supporters.Count : 0)}).");
        }

        private static void BindSupporterRow(GameObject? row, SupporterTotal s, int index)
        {
            if (row == null) return;
            row.SetActive(true);
            SetText(row, "Name", s.DisplayName);
            // Follower counts are not on either source this aggregates (neither /gifts/received
            // nor the points ledger carries one), and fetching a profile per supporter would be
            // three extra round trips for one line — so the node's "N followers" run renders the
            // em dash the rest of the GPS surface uses for "not known".
            SetText(row, "Followers", Unknown);
            SetText(row, "Pts", string.Format(LocalizationManager.Get("GPS_GIFT_PTS"),
                                              s.Points.ToString("N0", CultureInfo.InvariantCulture)));
            SetInitial(row, s.DisplayName);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Popular golfers
        // ═════════════════════════════════════════════════════════════════════

        private void OnDiscoverResult(ApiResult<List<DiscoverUserDto>> result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                    Debug.LogWarning($"{Tag} /user/discover failed ({result.ErrorKind}) — golfer rows hidden.");
                ApplyGolfers(null, PaintKind.Fetch);
                return;
            }
            ApplyGolfers(result.Data, PaintKind.Fetch);
        }

        private void ApplyGolfers(List<DiscoverUserDto>? rows, PaintKind kind)
        {
            _golfers.Clear();
            int count = 0;
            var painted = new List<Transform>(_golferRows.Length);
            if (rows != null)
            {
                foreach (DiscoverUserDto u in rows)
                {
                    if (count >= _golferRows.Length) break;
                    if (u == null || string.IsNullOrWhiteSpace(u.DisplayName)) continue;
                    BindGolferRow(_golferRows[count], u, count);
                    painted.Add(_golferRows[count].transform);
                    _golfers.Add(u);
                    count++;
                }
            }
            for (int i = count; i < _golferRows.Length; i++)
                if (_golferRows[i] != null) _golferRows[i].SetActive(false);

            bool stagger = _golfersGate.Should(kind, count);
            GpsPaintMotion.Shimmer(gameObject, ShimmerHost.Golfers, _golfersGate.IsCold);
            _golfersPanel.Reveal(this, _golfersGate.IsCold);
            if (stagger) GpsPaintMotion.StaggerRise(this, painted);

            Debug.Log($"{Tag} discover: {count} golfers bound.");
        }

        private static void BindGolferRow(GameObject? row, DiscoverUserDto u, int index)
        {
            if (row == null) return;
            row.SetActive(true);
            SetText(row, "Name", u.DisplayName);
            SetText(row, "Followers",
                    string.Format(LocalizationManager.Get("GPS_GIFT_FOLLOWERS"),
                                  (u.FollowersCount ?? 0).ToString("N0", CultureInfo.InvariantCulture)));
            SetInitial(row, u.DisplayName);
        }

        private void OnSendGift(int index)
        {
            if (index < 0 || index >= _golfers.Count) return;
            DiscoverUserDto u = _golfers[index];
            Debug.Log($"{Tag} SEND GIFT -> {u.DisplayName} ({u.Id}).");

            if (_sendModal == null)
            {
                Debug.LogWarning($"{Tag} no send modal wired.");
                return;
            }
            _sendModal.OpenSend(u.Id, u.DisplayName, OnGiftCommitted);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Buy gift items
        // ═════════════════════════════════════════════════════════════════════

        private void OnItemsResult(ApiResult<List<GiftItemDto>> result)
        {
            if (result == null || !result.Success)
            {
                if (result != null)
                    Debug.LogWarning($"{Tag} /gifts/items failed ({result.ErrorKind}) — strip hidden.");
                ApplyItems(null, PaintKind.Fetch);
                return;
            }
            ApplyItems(result.Data, PaintKind.Fetch);
        }

        private void ApplyItems(List<GiftItemDto>? catalog, PaintKind kind)
        {
            _items.Clear();
            List<GiftItemDto> strip = GiftService.BuyStrip(catalog, _itemCells.Length);

            for (int i = 0; i < _itemCells.Length; i++)
            {
                if (_itemCells[i] == null) continue;
                bool has = i < strip.Count;
                _itemCells[i].SetActive(has);
                if (!has) continue;

                GiftItemDto item = strip[i];
                _items.Add(item);
                SetText(_itemCells[i], "ItemName", GiftItemName.Of(item).ToUpperInvariant());
                SetText(_itemCells[i], "ItemPrice",
                        string.Format(LocalizationManager.Get("GPS_GIFT_PTS"),
                                      (item.PriceActivityPts ?? 0).ToString("N0", CultureInfo.InvariantCulture)));
                SetIcon(_itemCells[i], item.Category);
            }

            _itemsGate.Should(kind, strip.Count);
            _itemsPanel.Reveal(this, _itemsGate.IsCold);

            Debug.Log($"{Tag} catalog: {strip.Count} of " +
                      $"{(catalog != null ? catalog.Count : 0)} rows on the strip.");
        }

        /// <summary>Set a cell's glyph from its item category.</summary>
        private void SetIcon(GameObject cell, string? category)
        {
            Transform? icon = cell.transform.Find("IconRing/Icon");
            if (icon == null) return;
            var img = icon.GetComponent<Image>();
            if (img == null) return;

            int i = Glyph(category);
            if (i >= 0 && i < _glyphSprites.Length && _glyphSprites[i] != null)
                img.sprite = _glyphSprites[i];
        }

        /// <summary>
        /// Category → glyph index (SPEC § Client data bindings): hat→Star, tops→Sparkle,
        /// shoes→Pin, everything else→Heart. Indices into <c>_glyphSprites</c>, which the builder
        /// fills as { Heart, Star, Sparkle, Pin }.
        ///
        /// <para>
        /// A pure function so an EditMode test can pin the mapping without a scene. Note what it
        /// produces on the LIVE catalog's three cheapest basic rows (gloves 30 / accessory 40 /
        /// hat 50): Heart, Heart, Star — two identical glyphs side by side, because the spec's
        /// mapping names three categories and sends the other four to Heart. Recorded as-is
        /// rather than quietly extended.
        /// </para>
        /// </summary>
        public static int Glyph(string? category)
        {
            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "hat":   return 1;   // Star
                case "tops":  return 2;   // Sparkle
                case "shoes": return 3;   // Pin
                default:      return 0;   // Heart
            }
        }

        private void OnBuyItem(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            GiftItemDto item = _items[index];
            Debug.Log($"{Tag} BUY -> {item.Name} ({item.Id}) for {item.PriceActivityPts}.");

            if (_sendModal == null)
            {
                Debug.LogWarning($"{Tag} no send modal wired.");
                return;
            }
            _sendModal.OpenPurchase(item, OnGiftCommitted);
        }

        /// <summary>
        /// After either write: re-read the profile (gift_pts moved on a RECEIVE, activity_pts on
        /// a SEND) and re-aggregate the supporters. The RP in the top bar is refreshed by the
        /// modal itself, which is closer to the write.
        /// </summary>
        private void OnGiftCommitted()
        {
            ApiClient client = ApiClient.Instance;
            client.Run(UserService.Instance.Detail(null));
            client.Run(GiftService.Instance.Supporters(OnSupportersResult));
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Re-resolve every string this screen formats itself. The static labels ride
        /// <c>LocalizedText</c>, which has its own subscription; these are the ones built with
        /// <c>string.Format(LocalizationManager.Get(...))</c> at bind time and would otherwise
        /// stay in the language they were bound in — the Settings overlay never disables this
        /// screen, so nothing else would ever re-run them.
        /// </summary>
        private void OnLanguageChanged()
        {
            ApplyDetail(UserService.Instance.LastDetail);
            ApplySupporters(GiftService.Instance.LastSupporters, PaintKind.Repaint);
            ApplyGolfers(UserService.Instance.LastDiscover, PaintKind.Repaint);
            ApplyItems(GiftService.Instance.LastItems, PaintKind.Repaint);
        }

        private static void SetText(GameObject row, string child, string? value)
        {
            Transform? t = row.transform.Find(child);
            if (t == null) return;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = value ?? string.Empty;
        }

        /// <summary>The avatar disc's letter, and the disc colour that goes with the name. There
        /// is no <c>avatar_color</c> on a discover row or on an aggregated supporter, so the
        /// colour is the authored one for that ROW INDEX — which is what the node does too (its
        /// eight avatars cycle the same four gradients).</summary>
        private static void SetInitial(GameObject row, string? name)
        {
            Transform? t = row.transform.Find("Avatar/Initial");
            if (t == null) return;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp == null) return;
            string n = (name ?? string.Empty).Trim();
            tmp.text = n.Length > 0 ? n.Substring(0, 1).ToUpperInvariant() : "?";
        }
    }
}
