// Order: login_signup_screens Phase 2 — client-side auth session (tokens + identity)
using UnityEngine;

namespace Golfin.Auth
{
    /// <summary>
    /// Holds the current signed-in session (tokens + identity) and persists it across launches.
    ///
    /// PERSISTENCE NOTE (security): this uses PlayerPrefs (Base64, NOT encrypted). That is fine for the
    /// mockable phase and matches the GPS Flutter client's posture, but before a real production launch
    /// the tokens should move to a platform secure store (iOS Keychain / Android Keystore). Tracked as a
    /// go-live item in PHASE2_SERVER_SETUP_FOR_KEN.md. Do not treat PlayerPrefs as secure.
    /// </summary>
    public sealed class AuthSession
    {
        private const string Key = "golfin.auth.session.v1";

        public string AccessToken;
        public string RefreshToken;
        public long   ExpiresAtUnix;
        public string UserId;
        public string Email;
        public string DisplayName;
        public bool   EmailConfirmed;

        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        /// <summary>Has a username been chosen yet? Drives the login → CreateUsername-vs-Home branch.</summary>
        public bool HasDisplayName => !string.IsNullOrEmpty(DisplayName);

        /// <summary>Expired (or within <paramref name="skewSeconds"/> of expiry) relative to <paramref name="nowUnix"/>.</summary>
        public bool IsExpired(long nowUnix, long skewSeconds = 60)
            => ExpiresAtUnix > 0 && nowUnix >= (ExpiresAtUnix - skewSeconds);

        /// <summary>Copy the session-bearing fields of a successful result into this session.</summary>
        public void ApplyFrom(AuthResult r)
        {
            if (r == null || !r.Success) return;
            if (r.HasSession)
            {
                AccessToken   = r.AccessToken;
                RefreshToken  = r.RefreshToken;
                ExpiresAtUnix = r.ExpiresAtUnix;
            }
            if (r.User != null)
            {
                UserId         = r.User.Id;
                Email          = r.User.Email;
                if (!string.IsNullOrEmpty(r.User.DisplayName)) DisplayName = r.User.DisplayName;
                EmailConfirmed = r.User.EmailConfirmed;
            }
        }

        public void Clear()
        {
            AccessToken = RefreshToken = UserId = Email = DisplayName = null;
            ExpiresAtUnix = 0;
            EmailConfirmed = false;
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }

        // ── Persistence ──────────────────────────────────────────────────────────
        [System.Serializable]
        private struct Dto
        {
            public string a; public string r; public long e;
            public string uid; public string em; public string dn; public bool ec;
        }

        public void Save()
        {
            var dto = new Dto { a = AccessToken, r = RefreshToken, e = ExpiresAtUnix,
                                uid = UserId, em = Email, dn = DisplayName, ec = EmailConfirmed };
            string json = JsonUtility.ToJson(dto);
            string b64  = System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
            PlayerPrefs.SetString(Key, b64);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return;
            try
            {
                string b64  = PlayerPrefs.GetString(Key);
                string json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(b64));
                var dto = JsonUtility.FromJson<Dto>(json);
                AccessToken = dto.a; RefreshToken = dto.r; ExpiresAtUnix = dto.e;
                UserId = dto.uid; Email = dto.em; DisplayName = dto.dn; EmailConfirmed = dto.ec;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[AuthSession] Failed to load persisted session, clearing: {ex.Message}");
                Clear();
            }
        }
    }
}
