// ─────────────────────────────────────────────────────────────────────────────
// UI/Account — UsernameClaim (unique_usernames, 2026-08-18)
//
// Usernames are globally unique. The name every OTHER player sees comes from the
// backend's profiles.display_name (leaderboards, tournament boards, search), so
// that row — guarded by a case-insensitive unique index — is where uniqueness
// lives. This helper claims the name there FIRST; only after the server says yes
// does the caller write Supabase Auth user_metadata via AuthService.
//
// Claim-then-metadata, in that order, is self-healing: if the metadata write
// fails after a successful claim, the retry re-claims the same name, the server
// answers "unchanged" (idempotent for the same user), and the flow proceeds to
// the metadata write again. The reverse order would let a metadata name exist
// that the profiles index had already given to someone else.
//
// Lives in Assembly-CSharp (namespace Golfin.UI.Account, next to UsernameRules):
// it needs Golfin.Net, which Golfin.Auth must never reference (Net → Auth is the
// existing one-way edge).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using Golfin.Auth;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.UI.Account
{
    /// <summary>How a username claim ended.</summary>
    public enum UsernameClaimStatus
    {
        /// <summary>The name is ours (claimed now, or already ours). Proceed to the metadata write.</summary>
        Ok,

        /// <summary>Someone else holds this name. Terminal for this input — pick another.</summary>
        Taken,

        /// <summary>Unreachable / timed out. Uniqueness cannot be verified offline, so the save
        /// does not proceed — a name allowed through blind could be a duplicate.</summary>
        Offline,

        /// <summary>The server rejected the request for another reason (auth, validation).</summary>
        Error
    }

    /// <summary>One claim attempt's result, with the message the screens show verbatim.</summary>
    public sealed class UsernameClaimOutcome
    {
        public readonly UsernameClaimStatus Status;
        public readonly string Message;

        public UsernameClaimOutcome(UsernameClaimStatus status, string message)
        {
            Status  = status;
            Message = message ?? string.Empty;
        }

        public bool MayProceed => Status == UsernameClaimStatus.Ok;
    }

    /// <summary>Wire shape of PUT /user/username's <c>data</c> object.</summary>
    public sealed class UsernameClaimResponse
    {
        [JsonProperty("updated")]   public bool    Updated;
        [JsonProperty("unchanged")] public bool    Unchanged;

        /// <summary><c>"taken"</c> when someone else holds the name; otherwise absent.</summary>
        [JsonProperty("status")]    public string  Status;

        [JsonProperty("username")]  public string  Username;
    }

    public static class UsernameClaim
    {
        private const string Tag = "[UsernameClaim]";

        /// <summary>Shown when the server says another player holds the name.
        /// Localised via AUTH_USERNAME_TAKEN (EN/JP in LocalizationText.csv).</summary>
        public static string TakenMessage => LocalizationManager.Get("AUTH_USERNAME_TAKEN");

        /// <summary>Same copy the auth transport uses for a dead connection, so the two failure
        /// modes on these screens read identically.
        /// Localised via AUTH_ERR_OFFLINE (EN/JP in LocalizationText.csv). A property, not a
        /// const: a const would bake the English into every call site at compile time.</summary>
        public static string OfflineMessage => LocalizationManager.Get("AUTH_ERR_OFFLINE");

        /// <summary>
        /// Claim <paramref name="username"/> on the backend, then report through
        /// <paramref name="onDone"/> (invoked exactly once, on the main thread).
        ///
        /// <para>MOCK sessions skip the round-trip and answer Ok: the mock transport's token is not
        /// a real JWT, so the server would 401 every claim and editor dev could never set a name.
        /// Uniqueness is meaningless against a fake account anyway. The same applies to the
        /// editor-only bot session override, whose token is a literal placeholder string.</para>
        /// </summary>
        public static void Claim(string username, Action<UsernameClaimOutcome> onDone)
        {
            if (onDone == null) return;

            bool mock = AuthService.Instance != null && AuthService.Instance.IsMockTransport;
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            // Whole-file-guarded type: callers must repeat the guard (BotSessionOverride's own rule).
            mock = mock || Golfin.Dev.BotSessionOverride.Active;
#endif
            if (mock)
            {
                Debug.Log($"{Tag} Mock/bot session — skipping the server uniqueness check for '{username}'.");
                onDone(new UsernameClaimOutcome(UsernameClaimStatus.Ok, string.Empty));
                return;
            }

            string body = "{\"username\":" + JsonConvert.ToString(username ?? string.Empty) + "}";

            ApiClient.Instance.Run(
                ApiClient.Instance.Put<UsernameClaimResponse>(
                    Endpoints.UserUsername, body,
                    result => onDone(Interpret(result))));
        }

        /// <summary>
        /// The pure half — result → outcome — so the mapping is gated by an EditMode test
        /// rather than a device pass.
        /// </summary>
        internal static UsernameClaimOutcome Interpret(ApiResult<UsernameClaimResponse> result)
        {
            if (result == null)
                return new UsernameClaimOutcome(UsernameClaimStatus.Offline, OfflineMessage);

            if (result.Success)
            {
                UsernameClaimResponse data = result.Data;

                // A taken name is a 200 with status:"taken" — a rule, not an HTTP error
                // (same envelope pattern as the tournament-enter "insufficient" answer).
                if (data != null && string.Equals(data.Status, "taken", StringComparison.OrdinalIgnoreCase))
                    return new UsernameClaimOutcome(UsernameClaimStatus.Taken, TakenMessage);

                return new UsernameClaimOutcome(UsernameClaimStatus.Ok, string.Empty);
            }

            // Defensive: /user/update's PLAYLIFE-side mapping answers 409 for the same index.
            if (result.StatusCode == 409)
                return new UsernameClaimOutcome(UsernameClaimStatus.Taken, TakenMessage);

            switch (result.ErrorKind)
            {
                case ApiErrorKind.Network:
                case ApiErrorKind.Timeout:
                case ApiErrorKind.NotConfigured:
                case ApiErrorKind.Disabled:
                    return new UsernameClaimOutcome(UsernameClaimStatus.Offline, OfflineMessage);

                default:
                    string message = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "Could not save username."
                        : result.ErrorMessage;
                    return new UsernameClaimOutcome(UsernameClaimStatus.Error, message);
            }
        }
    }
}
