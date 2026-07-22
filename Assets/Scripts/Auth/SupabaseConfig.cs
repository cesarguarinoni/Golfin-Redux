// Order: login_signup_screens Phase 2 — auth configuration asset
using UnityEngine;

namespace Golfin.Auth
{
    /// <summary>
    /// Runtime auth configuration. Loaded from Resources by <see cref="AuthService"/>.
    /// Ships with <see cref="useMockTransport"/> = true so the whole flow runs offline until Ken
    /// supplies the Supabase anon (public) key; flip the toggle to go live — no code change.
    ///
    /// The anon key is PUBLIC by design (the GPS Flutter client hardcodes it) — safe to store here.
    /// The service_role key must NEVER appear in the client.
    /// </summary>
    [CreateAssetMenu(fileName = "SupabaseConfig", menuName = "Golfin/Auth/Supabase Config", order = 0)]
    public sealed class SupabaseConfig : ScriptableObject
    {
        [Header("Transport")]
        [Tooltip("TRUE = offline mock (default, no key needed). FALSE = live Supabase (needs the anon key below).")]
        public bool useMockTransport = true;

        [Header("Supabase project (from GPS_INTEGRATION_REFERENCE.md §3)")]
        [Tooltip("Supabase project URL, e.g. https://wmszyghwwkaptgqdunel.supabase.co")]
        public string supabaseUrl = "https://wmszyghwwkaptgqdunel.supabase.co";

        [Tooltip("Supabase anon/public key (JWT). PUBLIC by design. Provided by Ken — see PHASE2_SERVER_SETUP_FOR_KEN.md.")]
        [TextArea(2, 4)]
        public string anonKey = "";

        [Header("Timeouts")]
        [Tooltip("Per-request timeout in seconds (Supabase can cold-start).")]
        public int requestTimeoutSeconds = 30;

        /// <summary>True when live transport is selected but the anon key is missing — AuthService falls back to mock and warns.</summary>
        public bool LiveButUnconfigured => !useMockTransport && string.IsNullOrWhiteSpace(anonKey);

        public string AuthBaseUrl => (supabaseUrl ?? "").TrimEnd('/') + "/auth/v1";
    }
}
