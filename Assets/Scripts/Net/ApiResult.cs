// Order: reward_points_backend Slice 1 — transport-agnostic result type for every PLAYLIFE API call.
namespace Golfin.Net
{
    /// <summary>
    /// Why a call failed, in terms the caller can branch on without parsing HTTP status codes.
    ///
    /// Status→kind mapping is derived from the LIVE playlife-api behaviour probed 2026-08-12:
    ///   • missing Authorization header  → 403  (<see cref="Forbidden"/>)
    ///   • malformed / expired JWT       → 401  (<see cref="Unauthorized"/>) — this is the refresh trigger
    ///   • request timeout               → 408  (<see cref="Timeout"/>)
    /// </summary>
    public enum ApiErrorKind
    {
        None = 0,
        /// <summary>The <c>PointsBackendEnabled</c> feature flag is OFF — the call was never made.</summary>
        Disabled,
        /// <summary>Connection/DNS/TLS failure, or the transport produced no response at all.</summary>
        Network,
        /// <summary>HTTP 408, or the retry budget was exhausted on a timing-out request.</summary>
        Timeout,
        /// <summary>HTTP 401 — token rejected. Survives only after a refresh+retry has already failed.</summary>
        Unauthorized,
        /// <summary>HTTP 403 — no credentials were presented (or the account lacks access).</summary>
        Forbidden,
        NotFound,
        /// <summary>Any other 4xx.</summary>
        Client,
        /// <summary>Any 5xx.</summary>
        Server,
        /// <summary>2xx whose body could not be parsed into the expected shape.</summary>
        Parse,
        /// <summary>No transport/runner configured — a wiring bug, not a server condition.</summary>
        NotConfigured
    }

    /// <summary>
    /// Outcome of one logical API call (which may have involved several HTTP attempts —
    /// see <see cref="Attempts"/>). Never null-returns: every code path produces one of these.
    /// </summary>
    public sealed class ApiResult<T>
    {
        public bool Success;
        public T Data;

        /// <summary>HTTP status of the final attempt; 0 when no response was ever received.</summary>
        public long StatusCode;

        public ApiErrorKind ErrorKind = ApiErrorKind.None;
        public string ErrorMessage;

        /// <summary>Raw response body of the final attempt, kept for diagnostics/logging.</summary>
        public string RawBody;

        /// <summary>How many HTTP requests were actually sent (retries included). Asserted by the tests.</summary>
        public int Attempts;

        /// <summary>True when a 401 forced a token refresh during this call.</summary>
        public bool DidRefreshToken;

        public static ApiResult<T> Ok(T data, long status, string raw, int attempts, bool refreshed = false) => new ApiResult<T>
        {
            Success = true, Data = data, StatusCode = status, RawBody = raw,
            Attempts = attempts, DidRefreshToken = refreshed, ErrorKind = ApiErrorKind.None
        };

        public static ApiResult<T> Fail(ApiErrorKind kind, string message, long status, string raw, int attempts, bool refreshed = false) => new ApiResult<T>
        {
            Success = false, Data = default, StatusCode = status, RawBody = raw,
            Attempts = attempts, DidRefreshToken = refreshed, ErrorKind = kind, ErrorMessage = message
        };

        public override string ToString()
            => Success
                ? $"ApiResult<{typeof(T).Name}> OK ({StatusCode}, attempts={Attempts})"
                : $"ApiResult<{typeof(T).Name}> {ErrorKind} ({StatusCode}, attempts={Attempts}): {ErrorMessage}";
    }
}
