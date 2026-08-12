// Order: reward_points_backend Slice 1 — test doubles for the transport + auth seams.
using System;
using System.Collections;
using System.Collections.Generic;

namespace Golfin.Net.Tests
{
    /// <summary>
    /// Scripted <see cref="IHttpTransport"/>: hands back a queued response per call and records every
    /// request it saw (including the Authorization header at send time, which is what proves the
    /// post-refresh replay used the NEW token).
    ///
    /// Yields nothing, so a plain <c>while (routine.MoveNext())</c> drains an ApiClient call in EditMode.
    /// </summary>
    public sealed class FakeHttpTransport : IHttpTransport
    {
        private readonly Queue<HttpResponse> _responses = new Queue<HttpResponse>();

        /// <summary>Used once the scripted queue runs dry (keeps a runaway retry loop from throwing).</summary>
        public HttpResponse Fallback = HttpResponse.Status(500, "{\"detail\":\"no scripted response\"}");

        public readonly List<string> SentAuthHeaders = new List<string>();
        public readonly List<string> SentUrls = new List<string>();
        public readonly List<string> SentBodies = new List<string>();
        public readonly List<string> SentMethods = new List<string>();

        public int CallCount => SentUrls.Count;

        public FakeHttpTransport Enqueue(params HttpResponse[] responses)
        {
            foreach (var r in responses) _responses.Enqueue(r);
            return this;
        }

        public IEnumerator Send(HttpRequest request, Action<HttpResponse> onResponse)
        {
            SentMethods.Add(request.Method);
            SentUrls.Add(request.Url);
            SentBodies.Add(request.Body);
            SentAuthHeaders.Add(request.Headers.TryGetValue("Authorization", out var a) ? a : null);

            onResponse?.Invoke(_responses.Count > 0 ? _responses.Dequeue() : Fallback);
            yield break;
        }
    }

    /// <summary>
    /// Scripted <see cref="IAuthTokenProvider"/>. <see cref="RefreshSucceeds"/> decides the outcome and
    /// <see cref="TokenAfterRefresh"/> is what the token becomes, so a test can assert that the replayed
    /// request carried the refreshed value rather than the stale one.
    /// </summary>
    public sealed class FakeAuthTokenProvider : IAuthTokenProvider
    {
        public string Token = "tok-initial";
        public string TokenAfterRefresh = "tok-refreshed";
        public bool RefreshSucceeds = true;
        public bool Authenticated = true;
        public int RefreshCallCount;

        public bool IsAuthenticated => Authenticated;

        public string AccessToken => Token;

        public IEnumerator Refresh(Action<bool> onDone)
        {
            RefreshCallCount++;
            if (RefreshSucceeds) Token = TokenAfterRefresh;
            onDone?.Invoke(RefreshSucceeds);
            yield break;
        }
    }

    /// <summary>Runner that executes a routine to completion inline (no coroutine engine).</summary>
    public sealed class ImmediateCoroutineRunner : ICoroutineRunner
    {
        public int RunCount;

        public void Run(IEnumerator routine)
        {
            RunCount++;
            if (routine == null) return;
            while (routine.MoveNext()) { }
        }
    }

    public static class Pump
    {
        /// <summary>Drive a coroutine to completion. Bounded so a regression that loops forever fails the
        /// test instead of hanging the Editor.</summary>
        public static int Drain(IEnumerator routine, int maxSteps = 10000)
        {
            int steps = 0;
            while (routine.MoveNext())
            {
                if (++steps > maxSteps)
                    throw new InvalidOperationException($"Coroutine did not finish within {maxSteps} steps — probable infinite retry loop.");
            }
            return steps;
        }
    }
}
