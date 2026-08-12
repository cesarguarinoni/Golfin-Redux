// Order: reward_points_backend Slice 1 — auth seam. Reuses the auth epic's session; no second auth path.
using System;
using System.Collections;
using Golfin.Auth;
using UnityEngine;

namespace Golfin.Net
{
    /// <summary>
    /// What <see cref="ApiClient"/> needs from the signed-in session, and nothing more. Exists so the
    /// EditMode tests can drive the 401→refresh→retry branch without a Supabase round-trip.
    /// </summary>
    public interface IAuthTokenProvider
    {
        bool IsAuthenticated { get; }
        string AccessToken { get; }

        /// <summary>Exchange the refresh token for a fresh session. Invokes
        /// <paramref name="onDone"/> exactly once with whether a NEW session was established.</summary>
        IEnumerator Refresh(Action<bool> onDone);
    }

    /// <summary>
    /// The shipping provider: a thin adapter over the auth epic's <see cref="AuthService"/>
    /// (commits 2ffe0403f / 122842b8c / 847d7bced). No token storage, no refresh logic and no second
    /// Supabase client live here — SPEC §4 explicitly forbids hand-rolling a parallel auth path.
    ///
    /// Refresh IS already exposed by the auth epic (<c>AuthService.RefreshSession</c> →
    /// <c>ISupabaseAuthClient.RefreshSession</c> → <c>POST /auth/v1/token?grant_type=refresh_token</c>,
    /// with the session persisted by <c>AuthService.Wrap</c>), so nothing needed flagging here.
    ///
    /// <see cref="AuthService"/>.Instance self-bootstraps a DontDestroyOnLoad host on first touch, so
    /// every member is deliberately lazy — constructing this provider must stay side-effect-free while
    /// the <c>PointsBackendEnabled</c> flag is OFF.
    /// </summary>
    public sealed class AuthServiceTokenProvider : IAuthTokenProvider
    {
        /// <summary>How long to wait for the refresh callback before giving up (Supabase can cold-start).</summary>
        public float RefreshTimeoutSeconds = 30f;

        public bool IsAuthenticated => AuthService.Instance.Session.IsAuthenticated;

        public string AccessToken => AuthService.Instance.Session.AccessToken;

        public IEnumerator Refresh(Action<bool> onDone)
        {
            bool done = false;
            bool ok = false;

            AuthService.Instance.RefreshSession(result =>
            {
                ok = result != null && result.Success && result.HasSession;
                done = true;
            });

            float deadline = Time.realtimeSinceStartup + Mathf.Max(1f, RefreshTimeoutSeconds);
            while (!done && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!done)
                Debug.LogWarning("[ApiClient] Token refresh timed out — treating the session as unrecoverable.");

            onDone?.Invoke(done && ok);
        }
    }
}
