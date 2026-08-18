// Order: reward_points_backend Slice 1 — PLAYLIFE API client (Bearer + envelope + retry + 401 refresh).
using System;
using System.Collections;
using UnityEngine;

namespace Golfin.Net
{
    /// <summary>
    /// The single HTTP entry point for the PLAYLIFE API (SPEC §4 Slice 1).
    ///
    /// Responsibilities, all of them testable against <see cref="IHttpTransport"/> rather than a socket:
    ///   1. attach <c>Authorization: Bearer &lt;supabase JWT&gt;</c> from the auth epic's session;
    ///   2. unwrap the <c>{data:…}</c> envelope into a typed payload;
    ///   3. retry transient failures (HTTP 408 + connection failures) up to <see cref="MaxTransientRetries"/>;
    ///   4. on HTTP 401, refresh the session ONCE and replay the request with the new token.
    ///
    /// COROUTINE PUMPING. Every nested enumerator is driven with an explicit
    /// <c>while (inner.MoveNext()) yield return inner.Current;</c> rather than <c>yield return inner</c>.
    /// Unity's coroutine engine would auto-nest either way, but only the explicit form also works when a
    /// plain <c>while (routine.MoveNext())</c> pumps it — which is exactly how the EditMode tests exercise
    /// the retry and refresh branches with no play mode and no network.
    ///
    /// This is a plain C# singleton, NOT a MonoBehaviour: it must be constructible in an EditMode test.
    /// It borrows <see cref="NetCoroutineRunner"/> only for fire-and-forget <see cref="Run"/> calls.
    /// </summary>
    public sealed class ApiClient
    {
        private static ApiClient _instance;

        /// <summary>Lazily-built default: UnityWebRequest transport + the AuthService-backed token provider.
        /// Nothing here runs while <c>PointsBackendEnabled</c> is OFF, because nothing calls it.</summary>
        public static ApiClient Instance => _instance ?? (_instance = new ApiClient(
            new UnityWebRequestTransport(),
            new AuthServiceTokenProvider(),
            NetCoroutineRunner.Instance));

        public IHttpTransport Transport { get; set; }
        public IAuthTokenProvider Auth { get; set; }
        public ICoroutineRunner Runner { get; set; }

        /// <summary>Extra attempts after the first on 408 / connection failure. 2 → up to 3 requests.</summary>
        public int MaxTransientRetries = 2;

        /// <summary>Pause between transient retries. Tests set this to 0 so the pump does not spin on wall-clock.</summary>
        public float RetryDelaySeconds = 0.75f;

        public int TimeoutSeconds = 30;

        /// <summary>Verbose per-request logging. Never logs the token itself.</summary>
        public bool LogRequests = true;

        public ApiClient(IHttpTransport transport, IAuthTokenProvider auth, ICoroutineRunner runner)
        {
            Transport = transport;
            Auth = auth;
            Runner = runner;
        }

        /// <summary>Install a hand-built client as the singleton (EditMode tests, staging harnesses).</summary>
        public static void ConfigureForTest(ApiClient client) => _instance = client;

        /// <summary>Drop the singleton so the next <see cref="Instance"/> rebuilds the shipping default.</summary>
        public static void ResetForTest() => _instance = null;

        // ── Public API ────────────────────────────────────────────────────────────

        public IEnumerator Get<T>(string url, Action<ApiResult<T>> onResult)
            => SendRoutine(new HttpRequest("GET", url) { TimeoutSeconds = TimeoutSeconds }, onResult);

        public IEnumerator Post<T>(string url, string jsonBody, Action<ApiResult<T>> onResult)
            => SendRoutine(new HttpRequest("POST", url, jsonBody) { TimeoutSeconds = TimeoutSeconds }, onResult);

        /// <summary>Same path as <see cref="Post{T}"/> with the PUT verb — the API's idempotent
        /// "replace this resource" calls (<c>/user/golfin-character</c>). Nothing else differs:
        /// auth, envelope, transient retries and the single 401 replay are all shared.</summary>
        public IEnumerator Put<T>(string url, string jsonBody, Action<ApiResult<T>> onResult)
            => SendRoutine(new HttpRequest("PUT", url, jsonBody) { TimeoutSeconds = TimeoutSeconds }, onResult);

        /// <summary>Fire-and-forget: hand a routine to the coroutine host. Used by callers that are not
        /// themselves coroutines (e.g. <c>PointsService.RefreshBalanceAsync</c>).</summary>
        public void Run(IEnumerator routine)
        {
            if (routine == null) return;
            var runner = Runner ?? (Runner = NetCoroutineRunner.Instance);
            runner.Run(routine);
        }

        // ── Core ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// One logical call: send, retry transients, refresh-and-replay a single 401, then interpret.
        /// The transient budget and the 401 replay are counted SEPARATELY — a refresh must not eat the
        /// retry budget, and a retry must not re-arm the refresh (that pair is what would loop forever).
        /// </summary>
        public IEnumerator SendRoutine<T>(HttpRequest request, Action<ApiResult<T>> onResult)
        {
            if (Transport == null)
            {
                onResult?.Invoke(ApiResult<T>.Fail(ApiErrorKind.NotConfigured,
                    "ApiClient has no transport configured.", 0, null, 0));
                yield break;
            }

            int calls = 0;
            int transientRetries = 0;
            bool refreshAttempted = false;
            bool didRefresh = false;
            HttpResponse response = null;

            while (true)
            {
                // Re-stamped every attempt: after a refresh the access token is a different string.
                ApplyAuthHeader(request);

                response = null;
                calls++;

                IEnumerator send = Transport.Send(request, r => response = r);
                while (send.MoveNext()) yield return send.Current;

                if (response == null)
                {
                    onResult?.Invoke(ApiResult<T>.Fail(ApiErrorKind.Network,
                        "Transport completed without producing a response.", 0, null, calls, didRefresh));
                    yield break;
                }

                // ── transient: connection failure or an explicit 408 ──
                if (response.IsConnectionError || response.StatusCode == 408)
                {
                    if (transientRetries < MaxTransientRetries)
                    {
                        transientRetries++;
                        if (LogRequests)
                            Debug.Log($"[ApiClient] Transient failure on {request.Method} {request.Url} " +
                                      $"({(response.IsConnectionError ? response.TransportError : "HTTP 408")}) — " +
                                      $"retry {transientRetries}/{MaxTransientRetries}.");

                        IEnumerator wait = Wait(RetryDelaySeconds);
                        while (wait.MoveNext()) yield return wait.Current;
                        continue;
                    }

                    var kind = response.StatusCode == 408 ? ApiErrorKind.Timeout : ApiErrorKind.Network;
                    string msg = response.StatusCode == 408
                        ? "The server took too long to respond."
                        : (string.IsNullOrEmpty(response.TransportError)
                            ? "No internet connection."
                            : response.TransportError);

                    onResult?.Invoke(ApiResult<T>.Fail(kind, msg, response.StatusCode, response.Body, calls, didRefresh));
                    yield break;
                }

                // ── 401: token rejected. Refresh once, then replay with the new token. ──
                // (403 is NOT this branch — the live API returns 403 when no Authorization header was
                // sent at all, which a refresh cannot fix.)
                if (response.StatusCode == 401 && !refreshAttempted && Auth != null)
                {
                    refreshAttempted = true;
                    if (LogRequests) Debug.Log("[ApiClient] 401 — refreshing the Supabase session and retrying once.");

                    bool refreshed = false;
                    IEnumerator refresh = Auth.Refresh(ok => refreshed = ok);
                    while (refresh.MoveNext()) yield return refresh.Current;

                    if (refreshed)
                    {
                        didRefresh = true;
                        continue;
                    }

                    onResult?.Invoke(ApiResult<T>.Fail(ApiErrorKind.Unauthorized,
                        "Session expired and could not be refreshed. Please sign in again.",
                        401, response.Body, calls, false));
                    yield break;
                }

                break;
            }

            onResult?.Invoke(Interpret<T>(response, calls, didRefresh));
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        private void ApplyAuthHeader(HttpRequest request)
        {
            string token = Auth != null && Auth.IsAuthenticated ? Auth.AccessToken : null;
            if (string.IsNullOrEmpty(token)) request.Headers.Remove("Authorization");
            else request.Headers["Authorization"] = "Bearer " + token;
        }

        private static ApiResult<T> Interpret<T>(HttpResponse response, int calls, bool didRefresh)
        {
            if (response.IsSuccessStatus)
            {
                if (ApiEnvelope.TryUnwrap<T>(response.Body, out T data, out string parseError))
                    return ApiResult<T>.Ok(data, response.StatusCode, response.Body, calls, didRefresh);

                return ApiResult<T>.Fail(ApiErrorKind.Parse,
                    $"Could not read the response body: {parseError}",
                    response.StatusCode, response.Body, calls, didRefresh);
            }

            ApiErrorKind kind;
            switch (response.StatusCode)
            {
                case 401: kind = ApiErrorKind.Unauthorized; break;
                case 403: kind = ApiErrorKind.Forbidden; break;
                case 404: kind = ApiErrorKind.NotFound; break;
                default:
                    kind = response.StatusCode >= 500 ? ApiErrorKind.Server
                         : response.StatusCode >= 400 ? ApiErrorKind.Client
                         : ApiErrorKind.Server;
                    break;
            }

            string message = ApiEnvelope.ExtractErrorMessage(response.Body, $"HTTP {response.StatusCode}");
            return ApiResult<T>.Fail(kind, message, response.StatusCode, response.Body, calls, didRefresh);
        }

        /// <summary>Realtime pause that survives both Unity's coroutine engine and a manual pump.
        /// A non-positive delay yields nothing at all, which is what the tests configure.</summary>
        private static IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
