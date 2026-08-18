// ─────────────────────────────────────────────────────────────────────────────
// NoticesRuntime — NoticeService
// Owns the live Home-notice set: reads the disk cache synchronously at Awake,
// then warms from the server off the critical path. Nothing on the boot path
// waits on a socket — a cold launch in airplane mode behaves exactly as it does
// today, except the panel is hidden instead of showing a stale bundled string.
//
// "No notice" ALWAYS means the panel is HIDDEN. That is the deliberate
// difference from BannerService: a banner slot has bundled art behind it, an
// unwritten announcement has nothing behind it, and the bundled
// HOME_MAINTENANCE_* strings are exactly the stale-date bug this feature
// exists to remove (SPEC §4.3). They are never read on this path.
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

namespace Golfin.Notices
{
    /// <summary>
    /// One notice page, ready to draw: title and body already resolved for the current language
    /// and already filtered for expiry. Handed out in order by <see cref="NoticeService.Pages"/>.
    /// </summary>
    public readonly struct NoticePage
    {
        /// <summary>Already resolved for the current language. May be empty if the body is not.</summary>
        public readonly string Title;
        /// <summary>Already resolved for the current language. May be empty if the title is not.</summary>
        public readonly string Body;
        /// <summary>The row's <c>end_at</c>, or null for no expiry.</summary>
        public readonly DateTime? ExpiresAtUtc;

        public NoticePage(string title, string body, DateTime? expiresAtUtc)
        {
            Title        = title;
            Body         = body;
            ExpiresAtUtc = expiresAtUtc;
        }
    }

    public sealed class NoticeService : MonoBehaviour
    {
        private const string Tag = "[Notices]";

        public static NoticeService? Instance { get; private set; }

        /// <summary>
        /// Raised on the main thread after a fetch REPLACED the set, so a screen that is already
        /// open repaints in place. Not raised for the boot-time cache load — no screen exists yet —
        /// and not raised when a fetch returns the same notices, which is the common case.
        /// </summary>
        public static event Action? OnNoticesChanged;

        /// <summary>Where the current set came from. Diagnostics only.</summary>
        public NoticeSource Source { get; private set; } = NoticeSource.None;

        /// <summary>
        /// The refetch cooldown. Home calls <see cref="Refresh"/> on every <c>OnEnable</c>; this is
        /// what keeps Home↔anywhere bouncing from becoming one request per bounce. Reused verbatim
        /// from the tournament schedule — rationale and in-flight guard live in
        /// <see cref="ScheduleRefreshThrottle"/>.
        /// </summary>
        public const double RefreshCooldownSeconds = ScheduleRefreshThrottle.DefaultCooldownSeconds;

        private readonly ScheduleRefreshThrottle _refreshThrottle =
            new ScheduleRefreshThrottle(RefreshCooldownSeconds);

        /// <summary>Monotonic seconds; unaffected by <c>Time.timeScale</c> or a paused game.</summary>
        private static double NowSeconds => Time.realtimeSinceStartupAsDouble;

        /// <summary>
        /// The parsed rows, in server order, BEFORE language resolution and expiry filtering.
        /// Empty is a normal, healthy state and means "hide the panel".
        /// </summary>
        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>One row as it arrived, both locales intact.</summary>
        private sealed class Entry
        {
            public string? TitleEn;
            public string? TitleJa;
            public string? BodyEn;
            public string? BodyJa;
            public DateTime? ExpiresAtUtc;
        }

        // ── Resolved view ─────────────────────────────────────────────────────

        private readonly List<NoticePage> _pages = new List<NoticePage>();

        /// <summary>Set whenever the resolved view can no longer be trusted; see <see cref="Pages"/>.</summary>
        private bool _pagesDirty = true;

        /// <summary>
        /// The earliest <c>expires_at</c> among the pages currently IN <see cref="_pages"/>, or
        /// null when none of them expire. Crossing it is the third rebuild trigger — see
        /// <see cref="Pages"/>.
        /// </summary>
        private DateTime? _nextExpiryUtc;

        /// <summary>
        /// The live pages, in order, already language-resolved and expiry-filtered.
        /// <b>An empty list is a normal state and means "hide the panel".</b>
        ///
        /// <para>
        /// The list is rebuilt — never handed back stale — on three triggers:
        /// </para>
        /// <list type="number">
        ///   <item>the set was replaced by a fetch (<see cref="Apply"/>);</item>
        ///   <item>the language changed (<see cref="OnLanguageChanged"/>);</item>
        ///   <item>the wall clock crossed the earliest expiry still in the list. This third one is
        ///   not in the resolution ladder's gift: SPEC §5.6 requires a page whose <c>end_at</c>
        ///   passes while the app is backgrounded to be gone on return WITH NO NETWORK, and neither
        ///   of the other two triggers fires in that scenario.</item>
        /// </list>
        /// <para>
        /// It is memoised rather than rebuilt per call because <c>HomeScreenController.Update</c>
        /// reads <c>Pages.Count</c> every frame to drive the auto-cycle; rebuilding there would
        /// allocate a list per frame for a set that changes on an admin's timescale.
        /// </para>
        /// </summary>
        public IReadOnlyList<NoticePage> Pages
        {
            get
            {
                if (_pagesDirty ||
                    (_nextExpiryUtc.HasValue && DateTime.UtcNow >= _nextExpiryUtc.Value))
                {
                    RebuildPages();
                }
                return _pages;
            }
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
            if (Apply(RemoteNoticeSource.ReadCache(), NoticeSource.DiskCache))
            {
                LogSource();

                // DELIBERATE DIVERGENCE from BannerService, which does not raise on the boot cache
                // load because "no screen exists yet". HomeScreen is ACTIVE in ShellScene at load,
                // so its OnEnable can run BEFORE this Awake — and if it does, it reads a null
                // Instance, hides the panel, and then never hears about the cache, because a
                // subsequent fetch that returns the SAME notices raises nothing. A banner slot that
                // misses the boot event still shows its authored sprite; a notice panel that misses
                // it shows nothing at all, for the whole visit.
                //
                // Raising here is correct in BOTH orders: if Home has not subscribed yet there are
                // no subscribers and this is a no-op (Home reads Pages itself moments later); if it
                // has, it repaints with the cached notices.
                RaiseChanged();
            }

            // Then warm from the server, off the critical path, through the SAME throttled entry
            // point Home uses — so opening Home in the first seconds of a session does not fire a
            // second request alongside the boot fetch.
            Refresh();
        }

        /// <summary>
        /// Language is global static state that can change while Home is already on screen (the
        /// Settings overlay does not disable it), so the resolved view has to be invalidated from
        /// the event rather than from a screen's <c>OnEnable</c>. Same subscribe/unsubscribe
        /// discipline as <c>BannerSlotBinder</c>.
        /// </summary>
        private void OnEnable()  => LocalizationManager.OnLanguageChanged += OnLanguageChanged;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= OnLanguageChanged;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Invalidate only. Repainting is the screen's job — <c>HomeScreenController</c> subscribes
        /// to the same event and re-reads <see cref="Pages"/>, which rebuilds on the way out.
        /// </summary>
        private void OnLanguageChanged() => _pagesDirty = true;

        // ── Resolution ────────────────────────────────────────────────────────

        private void RebuildPages()
        {
            _pages.Clear();
            _nextExpiryUtc = null;

            bool japanese  = LocalizationManager.CurrentLanguage == Language.Japanese;
            DateTime now   = DateTime.UtcNow;

            foreach (var e in _entries)
            {
                if (e == null) continue;
                if (!TryResolve(e.TitleEn, e.TitleJa, e.BodyEn, e.BodyJa,
                                japanese, e.ExpiresAtUtc, now,
                                out string title, out string body))
                    continue;

                _pages.Add(new NoticePage(title, body, e.ExpiresAtUtc));

                // Track the soonest expiry among the pages we KEPT, so the next read after that
                // instant rebuilds and drops it even with no network and no fetch.
                if (e.ExpiresAtUtc.HasValue &&
                    (!_nextExpiryUtc.HasValue || e.ExpiresAtUtc.Value < _nextExpiryUtc.Value))
                {
                    _nextExpiryUtc = e.ExpiresAtUtc;
                }
            }

            _pagesDirty = false;
        }

        /// <summary>
        /// The resolution ladder, as a pure function of its inputs — no MonoBehaviour, no clock, no
        /// socket, so it is directly unit-testable. <see cref="RebuildPages"/> is the only caller
        /// and supplies the live language and clock.
        ///
        /// <list type="number">
        ///   <item><b>Expiry.</b> The server already applied the whole scheduling rule at fetch
        ///   time; this is what covers a body cached on disk whose window closed since, with no
        ///   network to learn that from.</item>
        ///   <item><b>Japanese player.</b> <c>*_ja</c> if non-blank, else <c>*_en</c> — decided
        ///   INDEPENDENTLY for title and body. A row with a Japanese title and no Japanese body
        ///   shows a Japanese heading over English copy, which is correct and better than dropping
        ///   either half.</item>
        ///   <item><b>English player.</b> <c>*_en</c> ONLY. An English player must never fall into
        ///   Japanese copy — the same rule <c>TournamentDisplayName</c> enforces, for the same
        ///   reason.</item>
        ///   <item><b>Nothing left.</b> Title and body both blank → drop the page. The dashboard
        ///   refuses to activate such a row; this is the belt.</item>
        /// </list>
        /// </summary>
        /// <returns>False for "drop this page" — which always means it is not drawn at all.</returns>
        internal static bool TryResolve(
            string? titleEn, string? titleJa,
            string? bodyEn,  string? bodyJa,
            bool japanese, DateTime? expiresAtUtc, DateTime nowUtc,
            out string title, out string body)
        {
            title = string.Empty;
            body  = string.Empty;

            // 1. Expiry.
            if (expiresAtUtc.HasValue && nowUtc >= expiresAtUtc.Value) return false;

            // 2/3. Locale, per field, independently.
            title = Pick(titleEn, titleJa, japanese);
            body  = Pick(bodyEn,  bodyJa,  japanese);

            // 4. Nothing usable in either field.
            if (title.Length == 0 && body.Length == 0) return false;

            return true;
        }

        /// <summary>
        /// One field's locale choice. Japanese prefers <paramref name="ja"/> and falls back to
        /// <paramref name="en"/>; English takes <paramref name="en"/> or nothing — there is
        /// deliberately no ja→en-player fallback.
        /// </summary>
        private static string Pick(string? en, string? ja, bool japanese)
        {
            if (japanese)
            {
                if (!string.IsNullOrWhiteSpace(ja)) return ja!;
                return string.IsNullOrWhiteSpace(en) ? string.Empty : en!;
            }
            return string.IsNullOrWhiteSpace(en) ? string.Empty : en!;
        }

        // ── Refresh ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ask the server for a fresh notice set. <b>The caller never waits on this</b> — it returns
        /// on the same frame, having at most started a coroutine. Whoever is on screen keeps drawing
        /// what it already has; if a different set lands, <see cref="OnNoticesChanged"/> is raised
        /// and subscribers repaint in place.
        /// <para>
        /// <b>Failure is silent by construction.</b> Every failure path leaves the current set
        /// untouched and logs one warning — no toast, no retry, and in particular no empty state on
        /// a network error: only an actually-empty SERVER response hides the panel.
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
                IEnumerator fetch = RemoteNoticeSource.FetchRoutine(b => body = b);
                while (fetch.MoveNext()) yield return fetch.Current;

                if (string.IsNullOrWhiteSpace(body)) yield break;

                string before = Signature();
                if (!Apply(body, NoticeSource.Server)) yield break;
                LogSource();

                // Only when the set actually CHANGED. Home repainting on every 60s poll that
                // returned the same notice would be churn for nothing.
                if (Signature() == before) yield break;

                RaiseChanged();
            }
            finally
            {
                _refreshThrottle.Settle(NowSeconds);
            }
        }

        /// <summary>
        /// Fire <see cref="OnNoticesChanged"/>, containing any subscriber that throws — one bad
        /// subscriber must not abort the others or leave the throttle's in-flight guard set.
        /// </summary>
        private static void RaiseChanged()
        {
            try { OnNoticesChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnNoticesChanged subscriber threw: {ex}"); }
        }

        // ── Apply ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Parse <paramref name="json"/> and swap the set in WHOLESALE, preserving server order.
        /// Returns false — changing nothing — when the body is absent or unmappable, so a malformed
        /// payload leaves the player with whatever they already had.
        /// <para>
        /// A well-formed body carrying an EMPTY <c>notices</c> array is NOT a failure: it applies,
        /// empties the set, and hides the panel. That is the operator having deactivated
        /// everything, and it is the whole point of the feature.
        /// </para>
        /// </summary>
        private bool Apply(string? json, NoticeSource source)
        {
            var dto = Deserialize(json, source);
            if (dto?.Notices == null) return false;

            var next = new List<Entry>();
            foreach (var row in dto.Notices)
            {
                if (row == null) continue;
                next.Add(new Entry
                {
                    TitleEn      = row.TitleEn,
                    TitleJa      = row.TitleJa,
                    BodyEn       = row.BodyEn,
                    BodyJa       = row.BodyJa,
                    ExpiresAtUtc = ParseUtc(row.ExpiresAt),
                });
            }

            _entries.Clear();
            _entries.AddRange(next);
            Source      = source;
            _pagesDirty = true;
            return true;
        }

        private void LogSource()
        {
            string label = Source switch
            {
                NoticeSource.Server    => "SERVER (live fetch)",
                NoticeSource.DiskCache => "DISK CACHE (previous fetch)",
                _                      => "NONE (panel hidden)",
            };
            Debug.Log($"{Tag} Notice source: {label}. Rows={_entries.Count}");
        }

        /// <summary>
        /// Order-SENSITIVE fingerprint of the current set — the change test. Order matters here in
        /// a way it does not for banners: <c>sort_order</c> is what decides which notice is page 1,
        /// so a pure reorder from the dashboard is a real change Home must repaint for.
        /// </summary>
        private string Signature()
        {
            var parts = new List<string>();
            foreach (var e in _entries)
                parts.Add($"{e.TitleEn}|{e.TitleJa}|{e.BodyEn}|{e.BodyJa}|{e.ExpiresAtUtc:O}");
            return string.Join(";", parts);
        }

        // ── Parsing ───────────────────────────────────────────────────────────

        /// <summary>
        /// Absolute UTC or null. <c>AdjustToUniversal | AssumeUniversal</c> is what makes a
        /// timestamp mean the same instant for a player in UTC+9 and one in UTC−5 — the same
        /// discipline <c>BannerService</c> and <c>TournamentScheduleMapper</c> enforce.
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
        /// Deserialize the notices payload.
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
        internal static RemoteNoticesDto? Deserialize(string? json, NoticeSource source)
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
                return payload.ToObject<RemoteNoticesDto>(serializer);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not parse the {source} notice payload: {ex.Message}");
                return null;
            }
        }
    }
}
