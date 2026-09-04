// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C3 — the one live round, and everything that must survive the
// app being killed while it is open.
//
// THREE THINGS LIVE HERE AND NOWHERE ELSE.
//
//  1. WHICH ROUND IS OPEN. The server is the source of truth (/activity/active
//     on screen entry and on resume), and PlayerPrefs is a MIRROR so the live
//     card paints on frame one instead of after a round trip. When the two
//     disagree the server wins — a round checked out on another device, or
//     expired by the 8 h rule, must not keep ticking here.
//
//  2. THE IDEMPOTENCY KEYS. Minted and PERSISTED BEFORE the request leaves, one
//     per intent. This is the only reason a force-quit mid-check-in is safe: a
//     key generated inside the request would die with the process, the retry
//     would carry a NEW key, and the server would open — or refuse — a second
//     round the player can neither see nor close. Cleared only when a response
//     actually lands.
//
//  3. THE FOREGROUND GPS TRAIL. D3: no background location entitlement in this
//     task. A fix is taken on entry and on every resume and handed to
//     GpsSessionTracker.RecordFix, whose own 5-minute / 100-metre throttle
//     decides whether it is worth keeping. K4's threshold is 3 fixes, which a
//     normal round with one resume reaches.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Globalization;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// The player's one open round. Plain C# singleton in the shape of
    /// <see cref="VenueService"/> — constructible in an EditMode test, no MonoBehaviour, and the
    /// clock and the store are injected so a test can move time without sleeping.
    /// </summary>
    public sealed class RoundSession
    {
        private const string Tag = "[Round]";

        /// <summary>Deserialise WITHOUT Newtonsoft's date handling, so a `string` field holding an
        /// ISO timestamp round-trips verbatim instead of becoming local wall-clock text.
        /// See <c>Golfin.Net.ApiEnvelope.ParseRaw</c> for the full failure this prevents.</summary>
        private static readonly JsonSerializerSettings RawDates =
            new JsonSerializerSettings { DateParseHandling = DateParseHandling.None };

        /// <summary>The mirrored active round (an <see cref="ActivityDto"/> as JSON).</summary>
        public const string PrefsRound = "gps_active_round";

        /// <summary>The pending check-in key, written BEFORE the request (see the header).</summary>
        public const string PrefsCheckInKey = "gps_checkin_key";

        /// <summary>The pending check-out key, same rule.</summary>
        public const string PrefsCheckOutKey = "gps_checkout_key";

        /// <summary>A round open longer than this is expired — the same 8 h the check-out RPC
        /// applies. Duplicated on the client ONLY to render "ROUND EXPIRED" before the server is
        /// asked; the server's verdict is what pays (or does not).</summary>
        public const double ExpirySeconds = 8 * 60 * 60;

        /// <summary>GPS accuracy thresholds behind the card's "● HIGH / MED / LOW" stat.</summary>
        public const float AccuracyHighM = 15f;
        public const float AccuracyMedM = 50f;

        // ── Wiring ────────────────────────────────────────────────────────────

        private static RoundSession? _instance;

        public static RoundSession Instance =>
            _instance ?? (_instance = new RoundSession(new PlayerPrefsKeyValueStore(),
                                                       () => DateTimeOffset.UtcNow));

        public static void ConfigureForTest(RoundSession session) => _instance = session;

        public static void ResetForTest() => _instance = null;

        private readonly IKeyValueStore _store;
        private readonly Func<DateTimeOffset> _now;

        public RoundSession(IKeyValueStore store, Func<DateTimeOffset> now)
        {
            _store = store ?? new InMemoryKeyValueStore();
            _now = now ?? (() => DateTimeOffset.UtcNow);
            _active = ReadMirror();
        }

        // ── State ─────────────────────────────────────────────────────────────

        private ActivityDto? _active;

        /// <summary>The open round, or null. Painted from the PlayerPrefs mirror on construction
        /// and replaced by whatever <see cref="Refresh"/> hears from the server.</summary>
        public ActivityDto? Active => _active;

        public bool HasActive => _active != null;

        /// <summary>Raised whenever <see cref="Active"/> changes identity — opened, closed, or
        /// replaced by the server's answer. NOT raised by the per-second elapsed tick, which is
        /// the screen's own business.</summary>
        public event Action<ActivityDto?>? OnActiveChanged;

        /// <summary>The most recent fix this session recorded, for the card's GPS stat and for
        /// the check-out request. Null until a fix lands.</summary>
        public LocationFix? LastFix { get; private set; }

        /// <summary>How many fixes the tracker holds for the active round's venue — the card's
        /// GPS FIXES stat, and the number handed to check-out.</summary>
        public int FixCount { get; private set; } = 1;

        // ═════════════════════════════════════════════════════════════════════
        // The mirror
        // ═════════════════════════════════════════════════════════════════════

        private ActivityDto? ReadMirror()
        {
            string json = _store.GetString(PrefsRound, string.Empty);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                // Newtonsoft's default DateParseHandling rewrites an ISO timestamp into a LOCAL DateTime
                // token, so a `string` field receives "09/03/2026 12:26:19" instead of the UTC
                // text that was written. Settings keep the round-trip verbatim. See ApiEnvelope.ParseRaw.
                var row = JsonConvert.DeserializeObject<ActivityDto>(json, RawDates);
                // A mirror that is not `active` is stale by definition — it was written by an
                // older build, or the round was closed on another device while this one was
                // asleep. Dropping it here means the card never paints a round that is over.
                return row != null && row.Status == "active" ? row : null;
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"{Tag} could not read the mirrored round: {e.Message}");
                return null;
            }
        }

        private void WriteMirror(ActivityDto? row)
        {
            if (row == null) _store.DeleteKey(PrefsRound);
            else _store.SetString(PrefsRound, JsonConvert.SerializeObject(row));
            _store.Save();
        }

        /// <summary>Adopt a row as the active round (or null to clear), mirror it, and notify.</summary>
        public void SetActive(ActivityDto? row)
        {
            ActivityDto? next = row != null && row.Status == "active" ? row : null;
            bool changed = (next?.Id ?? 0) != (_active?.Id ?? 0);
            _active = next;
            WriteMirror(next);
            if (changed)
            {
                Debug.Log($"{Tag} active round -> {(next == null ? "none" : "#" + next.Id + " " + next.VenueName)}");
                OnActiveChanged?.Invoke(next);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Idempotency keys
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The key for the NEXT check-in: the one already persisted if a previous attempt never
        /// heard back, otherwise a fresh one, persisted before this method returns.
        ///
        /// <para>Reusing the stored key is the entire safety property. The server keys replay
        /// detection on <c>activities.checkin_key</c>, so a retry after a force-quit lands on the
        /// SAME row and returns <c>replayed:true</c> with <c>awarded:0</c> — instead of opening a
        /// second round, or being refused <c>already_active</c> for a round the player never saw
        /// succeed.</para>
        /// </summary>
        public string BeginCheckInKey()
        {
            string existing = _store.GetString(PrefsCheckInKey, string.Empty);
            if (!string.IsNullOrEmpty(existing))
            {
                Debug.Log($"{Tag} replaying the persisted check-in key {existing}");
                return existing;
            }
            string key = Guid.NewGuid().ToString();
            _store.SetString(PrefsCheckInKey, key);
            _store.Save();
            return key;
        }

        /// <summary>Same contract, for check-out.</summary>
        public string BeginCheckOutKey()
        {
            string existing = _store.GetString(PrefsCheckOutKey, string.Empty);
            if (!string.IsNullOrEmpty(existing)) return existing;
            string key = Guid.NewGuid().ToString();
            _store.SetString(PrefsCheckOutKey, key);
            _store.Save();
            return key;
        }

        /// <summary>
        /// Drop a pending key. Called ONLY when a response actually landed — success, replay, or a
        /// business refusal. A network failure deliberately KEEPS the key, because the request may
        /// have reached the server.
        /// </summary>
        public void ClearCheckInKey() { _store.DeleteKey(PrefsCheckInKey); _store.Save(); }

        public void ClearCheckOutKey() { _store.DeleteKey(PrefsCheckOutKey); _store.Save(); }

        // ═════════════════════════════════════════════════════════════════════
        // The GPS trail (D3 — foreground only)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Feed a fix to the session tracker and refresh the card's two GPS stats.
        ///
        /// <para>The tracker's own AND-throttle (5 min AND 100 m) decides whether the fix is
        /// worth storing, so calling this on every entry and resume cannot inflate
        /// <c>gps_check_count</c> — which is exactly the anti-cheat property score.py pays Trust
        /// +20 for.</para>
        /// </summary>
        /// <summary>
        /// Remember a fix for the ACCURACY READOUT only, without feeding the trail.
        ///
        /// <para>The confirm modal shows "● HIGH / MED / LOW" BEFORE a round exists, so it cannot
        /// wait for <see cref="RecordFix"/> — that one is gated on an active round because the
        /// trail is what the check-out is paid on, and fixes taken while no round is open must not
        /// count toward it. Splitting the two is the whole point: the label is honest from the
        /// first fix, and the trail still only ever holds fixes taken during a round.</para>
        /// </summary>
        public void NoteFix(LocationFix? fix)
        {
            if (fix == null) return;
            LastFix = fix;
        }

        public void RecordFix(LocationFix? fix)
        {
            if (fix == null) return;
            LastFix = fix;
            GpsSessionTracker.Instance.RecordFix(fix.Lat, fix.Lon);
            FixCount = Math.Max(1, GpsSessionTracker.Instance.SessionNear(fix.Lat, fix.Lon).CheckCount);
        }

        /// <summary>"● HIGH" / "● MED" / "● LOW" — the card's GPS stat, from the last fix's
        /// accuracy. Never null: with no fix at all the answer is LOW, which is honest.</summary>
        public GpsQuality Quality =>
            LastFix == null ? GpsQuality.Low
            : LastFix.AccuracyM < AccuracyHighM ? GpsQuality.High
            : LastFix.AccuracyM < AccuracyMedM ? GpsQuality.Medium
            : GpsQuality.Low;

        // ═════════════════════════════════════════════════════════════════════
        // Elapsed
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// How long the active round has been open, from the SERVER's <c>check_in_at</c>.
        ///
        /// <para>Never from a client stopwatch started at check-in: the app is killed and
        /// relaunched during a four-hour round, and a stopwatch would restart at zero. Zero when
        /// no round is open or the timestamp cannot be parsed.</para>
        /// </summary>
        public TimeSpan Elapsed
        {
            get
            {
                DateTimeOffset? start = CheckInAt;
                if (start == null) return TimeSpan.Zero;
                TimeSpan d = _now() - start.Value;
                return d < TimeSpan.Zero ? TimeSpan.Zero : d;
            }
        }

        public DateTimeOffset? CheckInAt => ParseTimestamp(_active?.CheckInAt);

        /// <summary>The round has been open past the server's 8 h cut-off, so check-out will pay
        /// nothing. The card says so BEFORE the player taps (§C4).</summary>
        public bool IsExpired => HasActive && Elapsed.TotalSeconds > ExpirySeconds;

        /// <summary>
        /// A PostgREST timestamptz. Parsed with <see cref="DateTimeStyles.AdjustToUniversal"/> so
        /// a string carrying no offset is read as UTC rather than as the device's local time —
        /// which on a phone in JST would make every round read as nine hours old.
        /// </summary>
        public static DateTimeOffset? ParseTimestamp(string? iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture,
                                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                        out DateTimeOffset parsed))
                return parsed;
            return null;
        }

        /// <summary>"1:24" — hours:minutes, the format the Flutter tab used and the frame shows.
        /// Fixed-width minutes so the digits tick without re-flowing the stat (§ motion).</summary>
        public static string FormatElapsed(TimeSpan d)
            => ((int)d.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" +
               d.Minutes.ToString("00", CultureInfo.InvariantCulture);

        /// <summary>"08:12" — the local wall-clock time a round started, for "Since 08:12".</summary>
        public static string FormatClock(DateTimeOffset? t)
            => t == null ? "--:--" : t.Value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        // ═════════════════════════════════════════════════════════════════════
        // Server sync
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// <c>GET /activity/active</c>, and adopt the answer.
        ///
        /// <para>A FAILED fetch leaves the mirror alone: the player keeps seeing their round while
        /// the tunnel is down, which is right, because the round IS still open. Only a successful
        /// answer may clear it.</para>
        /// </summary>
        public IEnumerator Refresh(ActivityService service, Action<bool>? onDone = null)
        {
            bool ok = false;
            IEnumerator call = (service ?? ActivityService.Instance).Active(r =>
            {
                if (r == null || !r.Success)
                {
                    Debug.LogWarning($"{Tag} /activity/active failed — keeping the mirrored round.");
                    return;
                }
                ok = true;
                SetActive(r.Data);           // Data == null is the legitimate "no round open"
            });
            while (call.MoveNext()) yield return call.Current;
            onDone?.Invoke(ok);
        }
    }

    /// <summary>The card's GPS stat, in the order the frame's colours run.</summary>
    public enum GpsQuality
    {
        Low,
        Medium,
        High,
    }

    /// <summary>
    /// The tiny slice of PlayerPrefs this class needs, behind an interface so an EditMode test can
    /// run without touching the Editor's own prefs file. Same reason
    /// <see cref="IGpsFixStore"/> exists.
    /// </summary>
    public interface IKeyValueStore
    {
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsKeyValueStore : IKeyValueStore
    {
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    public sealed class InMemoryKeyValueStore : IKeyValueStore
    {
        private readonly System.Collections.Generic.Dictionary<string, string> _map =
            new System.Collections.Generic.Dictionary<string, string>();

        public string GetString(string key, string fallback)
            => _map.TryGetValue(key, out string v) ? v : fallback;

        public void SetString(string key, string value) => _map[key] = value;
        public void DeleteKey(string key) => _map.Remove(key);
        public void Save() { }
    }
}
