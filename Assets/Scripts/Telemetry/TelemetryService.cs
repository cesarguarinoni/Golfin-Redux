// Order: beta_telemetry — batching event queue + flush against the PLAYLIFE API.
using System;
using System.Collections.Generic;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Telemetry
{
    /// <summary>One queued client event. <see cref="Attempts"/> is what makes the
    /// "re-enqueue exactly once" rule (SPEC §3 rule 2) precise per event rather than
    /// per batch.</summary>
    public sealed class TelemetryEvent
    {
        [JsonProperty("event_id")] public string EventId;
        [JsonProperty("name")]     public string Name;
        [JsonProperty("ts")]       public string Ts;
        [JsonProperty("payload")]  public Dictionary<string, object> Payload;

        /// <summary>How many flushes this event has already ridden and lost. Not serialised.</summary>
        [JsonIgnore] public int Attempts;
    }

    /// <summary>Server reply shape: <c>{"data":{"accepted":N,"duplicates":M}}</c>.</summary>
    public sealed class TelemetryAck
    {
        [JsonProperty("accepted")]   public int Accepted;
        [JsonProperty("duplicates")] public int Duplicates;
        [JsonProperty("rejected")]   public int Rejected;
    }

    /// <summary>
    /// The batching event sink. Plain C# singleton, NOT a MonoBehaviour, for exactly the
    /// reason <see cref="ApiClient"/> is one: it has to be constructible in an EditMode
    /// test with no play mode and no network. <see cref="TelemetryBehaviour"/> supplies
    /// the clock, the FPS samples and the pause/quit signals in a real build.
    ///
    /// TELEMETRY MUST NEVER BREAK GAMEPLAY (SPEC §3 rule 1). Every public entry point is
    /// wrapped: a hook that throws while building its payload logs and returns, and the
    /// shot it was observing proceeds untouched.
    ///
    /// It does NOT retry. <see cref="ApiClient"/> already retries transients and replays
    /// 401s after a token refresh; layering another retry on top of that would multiply,
    /// not add. The one thing this class adds is a single re-enqueue, which is safe
    /// because <c>event_id</c> is a client GUID with a unique index server-side.
    /// </summary>
    public sealed class TelemetryService
    {
        private static TelemetryService _instance;
        public static TelemetryService Instance => _instance ?? (_instance = new TelemetryService());

        /// <summary>Install a hand-built service as the singleton (EditMode tests).</summary>
        public static void ConfigureForTest(TelemetryService service) => _instance = service;

        /// <summary>Drop the singleton so the next <see cref="Instance"/> is fresh.</summary>
        public static void ResetForTest() => _instance = null;

        // ── Seams (defaults are the shipping behaviour; tests replace them) ────────

        /// <summary>Whether flushes actually reach the network. Defaults to the editor gate.</summary>
        public bool SendsEnabled = TelemetryConfig.DefaultSendsEnabled;

        /// <summary>Auth gate. Evaluated lazily so an EditMode test never touches the
        /// <c>AuthService</c> MonoBehaviour singleton.</summary>
        public Func<bool> IsAuthenticated = () => Golfin.Auth.AuthService.Instance.Session.IsAuthenticated;

        /// <summary>Current screen name, for <c>client_error</c>. Set by the hooks layer,
        /// which is the only assembly that can see <c>ScreenId</c>.</summary>
        public Func<string> CurrentScreenProvider = () => null;

        /// <summary>Sends the batch. Overridden wholesale in tests; the default posts
        /// through <see cref="ApiClient"/>.</summary>
        public Action<string, Action<bool>> Sender;

        // ── Batch envelope (SPEC §2.1) ────────────────────────────────────────────

        public string SessionId  = Guid.NewGuid().ToString();
        public string AppVersion = Application.version;
        public int?   BuildNumber;                       // set by the hooks layer (AppVersion.BuildNumber)
        public string Platform   = Application.platform.ToString();

        /// <summary>
        /// gps_standalone_shell §D6 — which shipped variant produced this session
        /// (<c>game</c> | <c>game-gps</c> | <c>ios-playlife</c>). Set by the hooks layer, which
        /// is the only assembly that can read the build defines.
        ///
        /// <para>Stamped into every event's PAYLOAD rather than added to the batch envelope: the
        /// envelope's fields are columns on <c>telemetry_events</c> and the ingest model only
        /// binds the ones it declares, so an envelope key the server does not know is dropped on
        /// the floor and never reaches a row. <c>payload</c> is jsonb — it stores whatever it is
        /// given and the admin explorer already renders it — so this is observable today,
        /// without a migration and without a server deploy.</para>
        ///
        /// <para>Null leaves every payload untouched, which is what an EditMode test sees.</para>
        /// </summary>
        public string AppVariant;
        public string DeviceModel = SystemInfo.deviceModel;
        public string Os          = SystemInfo.operatingSystem;

        // ── State ─────────────────────────────────────────────────────────────────

        private readonly List<TelemetryEvent> _queue = new List<TelemetryEvent>();
        private readonly HashSet<string> _seenErrorHashes = new HashSet<string>();
        private bool _inFlight;
        private int _errorsThisSession;
        private float _sinceLastFlush;

        public int QueuedCount => _queue.Count;
        public int ErrorsThisSession => _errorsThisSession;

        /// <summary>True between <c>round_start</c> and <c>hole_complete</c>. Drives both the
        /// abandon detector and FPS sampling.</summary>
        public bool RoundActive { get; set; }

        public TelemetryService()
        {
            Sender = PostBatch;
        }

        // ── Recording ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Queue one event. Never throws: a payload builder that blows up costs the event,
        /// not the caller's frame.
        /// </summary>
        public void Record(string name, Dictionary<string, object> payload = null)
        {
            if (!TelemetryConfig.Enabled) return;

            try
            {
                if (string.IsNullOrEmpty(name)) return;

                payload = payload ?? new Dictionary<string, object>();

                // §D6 — one key, every event. Never overwrites a payload that already carries it,
                // so a caller that wants to say something more specific still can.
                if (!string.IsNullOrEmpty(AppVariant) && !payload.ContainsKey("app_variant"))
                    payload["app_variant"] = AppVariant;

                _queue.Add(new TelemetryEvent
                {
                    EventId = Guid.NewGuid().ToString(),
                    Name    = name,
                    Ts      = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Payload = payload,
                });

                // Drop-oldest: the tail of a session is worth more than its head.
                while (_queue.Count > TelemetryConfig.QueueCap) _queue.RemoveAt(0);

                if (_queue.Count >= TelemetryConfig.FlushEventCount) Flush();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] Record('{name}') threw and was swallowed: {ex.Message}");
            }
        }

        /// <summary>
        /// Build a payload and record it, swallowing anything the builder throws. This is the
        /// form every hook uses — the builder runs INSIDE the try, so a null manager or a
        /// bad cast in a payload expression can never reach gameplay code.
        /// </summary>
        public void RecordSafe(string name, Func<Dictionary<string, object>> build)
        {
            if (!TelemetryConfig.Enabled) return;

            Dictionary<string, object> payload;
            try
            {
                payload = build != null ? build() : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] Payload builder for '{name}' threw and was swallowed: {ex.Message}");
                return;
            }

            Record(name, payload);
        }

        /// <summary>
        /// <c>client_error</c> (SPEC §1 #10). Capped per session and deduped by
        /// message + first stack line, so a per-frame exception contributes one row,
        /// not ten thousand.
        /// </summary>
        public void RecordException(string message, string stackTrace)
        {
            if (!TelemetryConfig.Enabled) return;

            try
            {
                if (_errorsThisSession >= TelemetryConfig.MaxClientErrorsPerSession) return;

                message = Truncate(message, TelemetryConfig.MaxErrorMessageChars);
                string stack = Truncate(stackTrace, TelemetryConfig.MaxErrorStackChars);

                string hash = message + "|" + FirstLine(stackTrace);
                if (!_seenErrorHashes.Add(hash)) return;

                _errorsThisSession++;

                Record(TelemetryEventNames.ClientError, new Dictionary<string, object>
                {
                    ["message"] = message,
                    ["stack"]   = stack,
                    ["screen"]  = SafeScreen(),
                });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Telemetry] RecordException threw and was swallowed: {ex.Message}");
            }
        }

        private string SafeScreen()
        {
            try { return CurrentScreenProvider != null ? CurrentScreenProvider() : null; }
            catch { return null; }
        }

        // ── Flushing ──────────────────────────────────────────────────────────────

        /// <summary>Advance the flush timer. Called from <see cref="TelemetryBehaviour"/>;
        /// tests call it directly to reach the 30s branch without waiting 30s.</summary>
        public void Tick(float deltaTime)
        {
            if (!TelemetryConfig.Enabled) return;
            if (_queue.Count == 0) { _sinceLastFlush = 0f; return; }

            _sinceLastFlush += deltaTime;
            if (_sinceLastFlush >= TelemetryConfig.FlushIntervalSeconds) Flush();
        }

        /// <summary>
        /// Drain up to one batch and send it. A no-op while a flush is in flight, while
        /// unauthenticated (the queue simply keeps accumulating under its cap), or with
        /// sends disabled.
        /// </summary>
        public void Flush()
        {
            if (!TelemetryConfig.Enabled) return;

            try
            {
                if (_inFlight || _queue.Count == 0) return;
                if (!SendsEnabled) return;

                bool authed;
                try { authed = IsAuthenticated == null || IsAuthenticated(); }
                catch { authed = false; }
                if (!authed) return;

                int take = Math.Min(_queue.Count, TelemetryConfig.MaxEventsPerBatch);
                var batch = _queue.GetRange(0, take);
                _queue.RemoveRange(0, take);

                string json = BuildBatchJson(batch);
                _inFlight = true;
                _sinceLastFlush = 0f;

                Sender?.Invoke(json, ok => OnFlushComplete(batch, ok));
            }
            catch (Exception ex)
            {
                _inFlight = false;
                Debug.LogWarning($"[Telemetry] Flush threw and was swallowed: {ex.Message}");
            }
        }

        private void OnFlushComplete(List<TelemetryEvent> batch, bool ok)
        {
            _inFlight = false;
            if (ok) return;

            // Re-enqueue ONCE. The event_id unique index makes a replay idempotent, so
            // the worst case of a false "failed" is a row the server already has.
            var retryable = new List<TelemetryEvent>();
            int dropped = 0;
            foreach (var e in batch)
            {
                if (e.Attempts == 0) { e.Attempts = 1; retryable.Add(e); }
                else dropped++;
            }

            if (retryable.Count > 0)
            {
                _queue.InsertRange(0, retryable);
                while (_queue.Count > TelemetryConfig.QueueCap) _queue.RemoveAt(_queue.Count - 1);
            }

            if (dropped > 0)
                Debug.LogWarning($"[Telemetry] Dropped {dropped} event(s) after a second failed flush.");
        }

        /// <summary>Serialise a batch into the §2.1 wire body. Newtonsoft rather than
        /// JsonUtility because the payloads are dictionaries and the keys are snake_case —
        /// the same call the rest of the Net layer already makes.</summary>
        public string BuildBatchJson(List<TelemetryEvent> batch)
        {
            var body = new Dictionary<string, object>
            {
                ["session_id"]   = SessionId,
                ["app_version"]  = AppVersion,
                ["build_number"] = BuildNumber,
                ["platform"]     = Platform,
                ["device_model"] = DeviceModel,
                ["os"]           = Os,
                ["events"]       = batch,
            };
            return JsonConvert.SerializeObject(body);
        }

        private static void PostBatch(string json, Action<bool> done)
        {
            var api = ApiClient.Instance;
            api.Run(api.Post<TelemetryAck>(Endpoints.TelemetryEvents, json, r => done?.Invoke(r != null && r.Success)));
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));

        private static string FirstLine(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            int nl = s.IndexOf('\n');
            return nl < 0 ? s : s.Substring(0, nl);
        }
    }
}
