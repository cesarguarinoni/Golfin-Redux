// Order: reward_points_backend Slice 1 — transport seam so ApiClient is testable without a network.
using System;
using System.Collections;
using System.Collections.Generic;

namespace Golfin.Net
{
    /// <summary>One outbound HTTP request. Mutable: <see cref="ApiClient"/> re-stamps the
    /// Authorization header on every attempt, because a 401-refresh changes the token mid-call.</summary>
    public sealed class HttpRequest
    {
        public string Method = "GET";
        public string Url;
        public string Body;
        public string ContentType = "application/json";
        public int TimeoutSeconds = 30;
        public readonly Dictionary<string, string> Headers = new Dictionary<string, string>();

        public HttpRequest() { }

        public HttpRequest(string method, string url, string body = null)
        {
            Method = method;
            Url = url;
            Body = body;
        }

        public HttpRequest WithHeader(string key, string value)
        {
            if (!string.IsNullOrEmpty(key)) Headers[key] = value;
            return this;
        }
    }

    /// <summary>
    /// One inbound HTTP response. <see cref="IsConnectionError"/> is distinct from a status code:
    /// a DNS/TLS/socket failure has no status at all, and is retried on the same branch as 408.
    /// </summary>
    public sealed class HttpResponse
    {
        public long StatusCode;
        public string Body;
        public bool IsConnectionError;
        public string TransportError;

        public bool IsSuccessStatus => StatusCode >= 200 && StatusCode < 300;

        public static HttpResponse Status(long code, string body = null)
            => new HttpResponse { StatusCode = code, Body = body };

        public static HttpResponse ConnectionFailure(string error = "Connection error")
            => new HttpResponse { IsConnectionError = true, TransportError = error, StatusCode = 0 };
    }

    /// <summary>
    /// The seam that keeps <see cref="ApiClient"/> unit-testable. Implementations are coroutine-shaped
    /// rather than Task-shaped to stay Unity-idiomatic (same reasoning as <c>ISupabaseAuthClient</c>)
    /// and so EditMode tests can pump the enumerator by hand with no play mode and no network.
    ///
    /// Contract: <paramref name="onResponse"/> is invoked exactly once before the enumerator finishes.
    /// </summary>
    public interface IHttpTransport
    {
        IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse);
    }
}
