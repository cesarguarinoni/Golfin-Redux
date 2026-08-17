// ─────────────────────────────────────────────────────────────────────────────
// BannersRuntime — BannerService
// Owns the live banner set: reads the disk cache synchronously at Awake, then
// warms from the server off the critical path. Nothing on the boot path waits
// on a socket — a cold launch in airplane mode behaves exactly as it does today.
//
// "No banner" ALWAYS means the bundled sprite stays on screen. There is no
// empty state anywhere in this file.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Golfin.Tournaments;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Golfin.Banners
{
    /// <summary>
    /// The two in-game banner slots. Both are hard-coded in the build; a placement cannot be added
    /// from the dashboard, and the server's <c>placement</c> CHECK constraint agrees with this enum.
    /// </summary>
    public enum BannerPlacement
    {
        /// <summary><c>Canvas/ScreensRoot/HomeScreen/PromoBanner</c> — the Home promo strip.</summary>
        HomePromo,
        /// <summary><c>RankingsScreen/ContentArea/Banner</c>.</summary>
        Rankings,
    }

    /// <summary>
    /// A resolved banner, ready to draw: one image URL (already picked for the current language)
    /// and an optional link. Handed out by <see cref="BannerService.TryGet"/>, which only returns
    /// true when there is something to show.
    /// </summary>
    public readonly struct BannerDefinition
    {
        public readonly BannerPlacement Placement;
        /// <summary>The URL to fetch. Never null or empty on a definition that was returned.</summary>
        public readonly string ImageUrl;
        /// <summary>Tap-through target, or null. Still re-checked against the allowlist on click.</summary>
        public readonly string? LinkUrl;
        /// <summary>The row's <c>end_at</c>, or null for no expiry.</summary>
        public readonly DateTime? ExpiresAtUtc;

        public BannerDefinition(BannerPlacement placement, string imageUrl, string? linkUrl, DateTime? expiresAtUtc)
        {
            Placement    = placement;
            ImageUrl     = imageUrl;
            LinkUrl      = linkUrl;
            ExpiresAtUtc = expiresAtUtc;
        }
    }

    public sealed class BannerService : MonoBehaviour
    {
        private const string Tag = "[Banners]";

        public static BannerService? Instance { get; private set; }

        /// <summary>
        /// Raised on the main thread after a fetch REPLACED the set, so a screen that is already
        /// open repaints in place. Not raised for the boot-time cache load — no screen exists yet —
        /// and not raised when a fetch returns the same banners, which is the common case.
        /// </summary>
        public static event Action? OnBannersChanged;

        /// <summary>Where the current set came from. Diagnostics only.</summary>
        public BannerSource Source { get; private set; } = BannerSource.None;

        /// <summary>
        /// The refetch cooldown. Both screens call <see cref="Refresh"/> on every
        /// <c>OnEnable</c>; this is what keeps Home↔Rankings bouncing from becoming one request per
        /// bounce. Reused verbatim from the tournament schedule — the rationale and the in-flight
        /// guard live in <see cref="ScheduleRefreshThrottle"/>.
        /// </summary>
        public const double RefreshCooldownSeconds = ScheduleRefreshThrottle.DefaultCooldownSeconds;

        private readonly ScheduleRefreshThrottle _refreshThrottle =
            new ScheduleRefreshThrottle(RefreshCooldownSeconds);

        /// <summary>Monotonic seconds; unaffected by <c>Time.timeScale</c> or a paused game.</summary>
        private static double NowSeconds => Time.realtimeSinceStartupAsDouble;

        /// <summary>One entry per placement, at most. Empty is a normal, healthy state.</summary>
        private readonly Dictionary<BannerPlacement, Entry> _entries =
            new Dictionary<BannerPlacement, Entry>();

        /// <summary>The parsed row behind a placement, before language resolution.</summary>
        private sealed class Entry
        {
            public string? ImageUrlEn;
            public string? ImageUrlJa;
            public string? LinkUrl;
            public DateTime? ExpiresAtUtc;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Boot: the disk cache, SYNCHRONOUSLY. Nothing here may wait on a socket.
            if (Apply(RemoteBannerSource.ReadCache(), BannerSource.DiskCache))
                LogSource();

            // Then warm from the server, off the critical path, through the SAME throttled entry
            // point the screens use — so opening Home in the first seconds of a session does not
            // fire a second request alongside the boot fetch.
            Refresh();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>
        /// The banner to draw in <paramref name="placement"/>, if any.
        ///
        /// <para>The whole fallback ladder, in order:</para>
        /// <list type="number">
        ///   <item><c>expires_at</c> is set and now is past it → <b>no banner</b>. The cached row
        ///   outlived its window while the player was offline.</item>
        ///   <item>Current language is Japanese → <c>image_url_ja</c>, otherwise <c>image_url_en</c>.</item>
        ///   <item>That one is null/empty → the other one.</item>
        ///   <item>Still nothing → <b>no banner</b>.</item>
        /// </list>
        /// <para>
        /// "No banner" always means the bundled sprite stays on screen. There is no empty state.
        /// </para>
        /// </summary>
        public bool TryGet(BannerPlacement placement, out BannerDefinition banner)
        {
            banner = default;

            if (!_entries.TryGetValue(placement, out var e) || e == null) return false;

            string? url = ResolveImageUrl(
                e.ImageUrlEn,
                e.ImageUrlJa,
                LocalizationManager.CurrentLanguage == Language.Japanese,
                e.ExpiresAtUtc,
                DateTime.UtcNow);

            if (url == null) return false;

            banner = new BannerDefinition(placement, url, e.LinkUrl, e.ExpiresAtUtc);
            return true;
        }

        /// <summary>
        /// The ladder itself, as a pure function of its inputs — no MonoBehaviour, no clock, no
        /// socket, so it is directly unit-testable. <see cref="TryGet"/> is the only caller and
        /// supplies the live language and clock.
        /// </summary>
        /// <returns>The URL to draw, or null for "no banner" — which always means the bundled
        /// sprite stays on screen.</returns>
        internal static string? ResolveImageUrl(
            string? imageUrlEn,
            string? imageUrlJa,
            bool japanese,
            DateTime? expiresAtUtc,
            DateTime nowUtc)
        {
            // 1. Expiry. The server already filtered on this at fetch time; this is what covers a
            //    cached body whose window closed since, with no network to learn that from.
            if (expiresAtUtc.HasValue && nowUtc >= expiresAtUtc.Value) return null;

            // 2/3. Preferred locale, then the other one.
            string? preferred = japanese ? imageUrlJa : imageUrlEn;
            string? fallback  = japanese ? imageUrlEn : imageUrlJa;

            if (!string.IsNullOrEmpty(preferred)) return preferred;
            if (!string.IsNullOrEmpty(fallback))  return fallback;

            // 4. Nothing usable.
            return null;
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ask the server for a fresh banner set. <b>The caller never waits on this</b> — it returns
        /// on the same frame, having at most started a coroutine. Whoever is on screen keeps drawing
        /// what it already has; if a different set lands, <see cref="OnBannersChanged"/> is raised
        /// and subscribers repaint in place.
        /// <para>
        /// <b>Failure is silent by construction.</b> Every failure path leaves the current set
        /// untouched and logs one warning — no toast, no empty state, no retry.
        /// </para>
        /// </summary>
        /// <returns>True if a fetch was started; false if throttled (in flight or within cooldown).</returns>
        public bool Refresh()
        {
            if (!_refreshThrottle.TryBegin(NowSeconds)) return false;

            StartCoroutine(RefreshRoutine());
            return true;
        }

        /// <summary>
        /// Enter through <see cref="Refresh"/>, never directly — the <c>finally</c> below is what
        /// releases the throttle's in-flight guard, and it pairs with a <c>TryBegin</c> only that
        /// method performs. A guard left set would silently disable refresh for the session.
        /// </summary>
        private IEnumerator RefreshRoutine()
        {
            try
            {
                string? body = null;
                IEnumerator fetch = RemoteBannerSource.FetchRoutine(b => body = b);
                while (fetch.MoveNext()) yield return fetch.Current;

                if (string.IsNullOrWhiteSpace(body)) yield break;

                string before = Signature();
                if (!Apply(body, BannerSource.Server)) yield break;
                LogSource();

                // Only when the set actually CHANGED. A screen repainting on every 60s poll that
                // returned the same two rows would be churn for nothing.
                if (Signature() == before) yield break;

                try { OnBannersChanged?.Invoke(); }
                catch (Exception ex) { Debug.LogError($"{Tag} OnBannersChanged subscriber threw: {ex}"); }
            }
            finally
            {
                _refreshThrottle.Settle(NowSeconds);
            }
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Parse <paramref name="json"/> and swap the set in WHOLESALE. Returns false — changing
        /// nothing — when the body is absent or unmappable, so a malformed payload leaves the
        /// player with whatever they already had.
        /// </summary>
        private bool Apply(string? json, BannerSource source)
        {
            var dto = Deserialize(json, source);
            if (dto?.Banners == null) return false;

            var next = new Dictionary<BannerPlacement, Entry>();
            foreach (var row in dto.Banners)
            {
                if (row == null) continue;
                if (!TryParsePlacement(row.Placement, out var placement))
                {
                    // A placement this build does not know about. Not an error — a newer dashboard
                    // may legitimately be ahead of this client.
                    Debug.LogWarning($"{Tag} Ignoring a banner for unknown placement '{row.Placement}'.");
                    continue;
                }
                if (next.ContainsKey(placement)) continue;   // server sends one; be explicit anyway

                next[placement] = new Entry
                {
                    // Refuse off-allowlist art HERE as well as at download time, so a refused URL
                    // never even reaches the resolution ladder and the slot falls straight back to
                    // its bundled sprite.
                    ImageUrlEn   = Accept(row.ImageUrlEn),
                    ImageUrlJa   = Accept(row.ImageUrlJa),
                    LinkUrl      = BannerPolicy.IsLinkAllowed(row.LinkUrl) ? row.LinkUrl : null,
                    ExpiresAtUtc = ParseUtc(row.ExpiresAt),
                };
            }

            _entries.Clear();
            foreach (var kv in next) _entries[kv.Key] = kv.Value;
            Source = source;

            WarmArt();
            return true;
        }

        private static string? Accept(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            if (BannerPolicy.IsArtAllowed(url)) return url;
            Debug.LogWarning($"{Tag} Refusing a banner image outside the allowlisted Storage prefix.");
            return null;
        }

        /// <summary>
        /// Warm the art cache and trim it, on EVERY path (cache load and live fetch alike). The
        /// sweep is what enforces the 50 MB bound, and running it only on sessions that reached the
        /// server would mean the bound does not exist on exactly the launches where the disk cache
        /// is the only art there is.
        /// </summary>
        private void WarmArt()
        {
            var urls = new List<string?>();
            var retention = new Dictionary<string, DateTime>(StringComparer.Ordinal);

            foreach (var kv in _entries)
            {
                foreach (string? url in new[] { kv.Value.ImageUrlEn, kv.Value.ImageUrlJa })
                {
                    if (string.IsNullOrEmpty(url)) continue;
                    urls.Add(url);
                    // A URL with no expiry is not "unknown to the schedule" — it simply never
                    // expires, so it is left to the LRU pass rather than being aged out on a guess.
                    if (kv.Value.ExpiresAtUtc.HasValue) retention[url!] = kv.Value.ExpiresAtUtc.Value;
                }
            }

            // Both locales are prefetched, not just the current one: switching language must swap
            // the image without leaving the screen, which means the other one has to be there.
            TournamentArtService.Banners.Prefetch(urls);
            TournamentArtService.Banners.SweepCacheAsync(retention);
        }

        private void LogSource()
        {
            string label = Source switch
            {
                BannerSource.Server    => "SERVER (live fetch)",
                BannerSource.DiskCache => "DISK CACHE (previous fetch)",
                _                      => "NONE (bundled sprites)",
            };
            Debug.Log($"{Tag} Banner source: {label}. Placements={_entries.Count}");
        }

        /// <summary>Order-independent fingerprint of the current set — the change test.</summary>
        private string Signature()
        {
            var parts = new List<string>();
            foreach (BannerPlacement p in new[] { BannerPlacement.HomePromo, BannerPlacement.Rankings })
            {
                if (!_entries.TryGetValue(p, out var e)) continue;
                parts.Add($"{p}|{e.ImageUrlEn}|{e.ImageUrlJa}|{e.LinkUrl}|{e.ExpiresAtUtc:O}");
            }
            return string.Join(";", parts);
        }

        // ── Parsing ───────────────────────────────────────────────────────────

        internal static bool TryParsePlacement(string? wire, out BannerPlacement placement)
        {
            switch (wire)
            {
                case "home_promo": placement = BannerPlacement.HomePromo; return true;
                case "rankings":   placement = BannerPlacement.Rankings;  return true;
                default:           placement = default;                   return false;
            }
        }

        /// <summary>
        /// Absolute UTC or null. <c>AdjustToUniversal | AssumeUniversal</c> is what makes a
        /// timestamp mean the same instant for a player in UTC+9 and one in UTC−5 — the same
        /// discipline <c>TournamentScheduleMapper</c> enforces.
        /// </summary>
        internal static DateTime? ParseUtc(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;

            if (DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            Debug.LogWarning($"{Tag} Could not parse expires_at '{value}'; treating it as no expiry.");
            return null;
        }

        /// <summary>
        /// Deserialize the banners payload.
        /// <para>
        /// <c>DateParseHandling.None</c> keeps <c>expires_at</c> as the exact characters the server
        /// sent, so <see cref="ParseUtc"/> is the ONLY place a timestamp is interpreted. Without it
        /// the JSON reader converts anything date-shaped before the DTO's string field ever sees it.
        /// </para>
        /// <para>
        /// A cached RAW body still carries <c>{"data": …}</c>; a live fetch has already been
        /// unwrapped by <c>ApiEnvelope</c>. Both are accepted.
        /// </para>
        /// </summary>
        internal static RemoteBannersDto? Deserialize(string? json, BannerSource source)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var settings   = new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };
                var serializer = JsonSerializer.CreateDefault(settings);

                JToken root;
                using (var reader = new JsonTextReader(new StringReader(json!)) { DateParseHandling = DateParseHandling.None })
                    root = JToken.ReadFrom(reader);

                JToken payload = root;
                if (root.Type == JTokenType.Object)
                {
                    JToken? inner = ((JObject)root)["data"];
                    if (inner != null) payload = inner;
                }

                if (payload.Type == JTokenType.Null) return null;
                return payload.ToObject<RemoteBannersDto>(serializer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the {source} banner payload: {ex.Message}");
                return null;
            }
        }
    }
}
