// Assets/Tests/EditMode/UsernameClaimTests.cs
// unique_usernames — EditMode unit tests for the username-claim outcome mapping.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (references Golfin.Net for ApiResult/ApiErrorKind).
// The production types (UsernameClaim, UsernameClaimResponse, UsernameClaimOutcome) live in
// Assembly-CSharp, which an asmdef cannot reference — they are reached via System.Reflection,
// the same pattern as GachaStage1Tests next door. ApiResult<T> closed over the Assembly-CSharp
// DTO is likewise built with MakeGenericType.
//
// WHAT IS UNDER TEST. UsernameClaim.Interpret is the pure half of the claim: ApiResult →
// outcome. The rules it encodes:
//   • 200 {status:"taken"}  → Taken   (a taken name is a rule, not an HTTP error — the server
//                                      answers 200, mirroring tournament-enter "insufficient")
//   • 200 otherwise         → Ok      (claimed, or idempotent re-claim of one's own name)
//   • HTTP 409              → Taken   (the PLAYLIFE-side /user/update mapping of the same index)
//   • Network/Timeout/etc.  → Offline (uniqueness cannot be verified blind — save must stop)
//   • other failures        → Error, carrying the server's message

using System;
using System.Reflection;
using Golfin.Net;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class UsernameClaimTests
    {
        // ── Reflection: production types in Assembly-CSharp ───────────────────

        private static readonly Type ClaimType =
            Type.GetType("Golfin.UI.Account.UsernameClaim, Assembly-CSharp");

        private static readonly Type ResponseType =
            Type.GetType("Golfin.UI.Account.UsernameClaimResponse, Assembly-CSharp");

        /// <summary>ApiResult&lt;UsernameClaimResponse&gt;, closed over the Assembly-CSharp DTO.</summary>
        private static readonly Type ResultType =
            typeof(ApiResult<>).MakeGenericType(ResponseType);

        private static readonly MethodInfo InterpretMethod =
            ClaimType?.GetMethod("Interpret", BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void Production_types_exist_in_AssemblyCSharp()
        {
            Assert.IsNotNull(ClaimType,      "Golfin.UI.Account.UsernameClaim not found in Assembly-CSharp.");
            Assert.IsNotNull(ResponseType,   "Golfin.UI.Account.UsernameClaimResponse not found.");
            Assert.IsNotNull(InterpretMethod, "UsernameClaim.Interpret(ApiResult<…>) not found.");
        }

        // ── Builders ──────────────────────────────────────────────────────────

        private static object Response(bool updated = false, bool unchanged = false, string status = null)
        {
            object dto = Activator.CreateInstance(ResponseType);
            ResponseType.GetField("Updated").SetValue(dto, updated);
            ResponseType.GetField("Unchanged").SetValue(dto, unchanged);
            ResponseType.GetField("Status").SetValue(dto, status);
            return dto;
        }

        private static object OkResult(object data)
            => ResultType.GetMethod("Ok", BindingFlags.Public | BindingFlags.Static)
                   .Invoke(null, new object[] { data, 200L, "", 1, false });

        private static object FailResult(ApiErrorKind kind, string message, long status)
            => ResultType.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static)
                   .Invoke(null, new object[] { kind, message, status, null, 1, false });

        /// <summary>Run Interpret and hand back (statusName, message).</summary>
        private static (string status, string message) Interpret(object apiResult)
        {
            object outcome = InterpretMethod.Invoke(null, new[] { apiResult });
            Type t = outcome.GetType();
            string status  = t.GetField("Status").GetValue(outcome).ToString();
            string message = (string)t.GetField("Message").GetValue(outcome);
            return (status, message);
        }

        // ── 200 answers ───────────────────────────────────────────────────────

        [Test]
        public void A_taken_name_is_a_200_that_maps_to_Taken_with_a_message()
        {
            var (status, message) = Interpret(OkResult(Response(status: "taken")));

            Assert.AreEqual("Taken", status,
                "200 {status:'taken'} is the server's refusal — reading it as success would let a " +
                "duplicate through to the metadata write.");
            Assert.IsNotEmpty(message, "The screens show this message verbatim.");
        }

        [Test]
        public void A_claimed_name_maps_to_Ok()
        {
            var (status, _) = Interpret(OkResult(Response(updated: true)));
            Assert.AreEqual("Ok", status);
        }

        [Test]
        public void An_idempotent_reclaim_of_ones_own_name_maps_to_Ok()
        {
            // The retry path: metadata write failed after a successful claim; the re-claim answers
            // "unchanged" and the flow must proceed to the metadata write again.
            var (status, _) = Interpret(OkResult(Response(unchanged: true)));
            Assert.AreEqual("Ok", status);
        }

        [Test]
        public void A_200_with_no_body_maps_to_Ok_not_a_crash()
        {
            var (status, _) = Interpret(OkResult(null));
            Assert.AreEqual("Ok", status);
        }

        // ── Failures ──────────────────────────────────────────────────────────

        [Test]
        public void A_409_maps_to_Taken()
        {
            // /user/update (the PLAYLIFE app path) answers the same unique index as HTTP 409.
            var (status, message) = Interpret(FailResult(ApiErrorKind.Client, "Username already taken", 409));
            Assert.AreEqual("Taken", status);
            Assert.IsNotEmpty(message);
        }

        [Test]
        public void Network_and_timeout_map_to_Offline_so_the_save_stops()
        {
            foreach (ApiErrorKind kind in new[]
                     { ApiErrorKind.Network, ApiErrorKind.Timeout, ApiErrorKind.NotConfigured, ApiErrorKind.Disabled })
            {
                var (status, message) = Interpret(FailResult(kind, "boom", 0));
                Assert.AreEqual("Offline", status,
                    $"{kind}: uniqueness cannot be verified offline — a name allowed through blind " +
                    "could be a duplicate, so the save must not proceed.");
                Assert.IsNotEmpty(message);
            }
        }

        [Test]
        public void Other_rejections_map_to_Error_carrying_the_servers_message()
        {
            var (status, message) = Interpret(FailResult(ApiErrorKind.Client,
                "Username must be 3-20 characters: letters, numbers, underscore.", 400));

            Assert.AreEqual("Error", status);
            StringAssert.Contains("3-20", message);
        }

        [Test]
        public void A_null_result_maps_to_Offline()
        {
            var (status, _) = Interpret(null);
            Assert.AreEqual("Offline", status);
        }
    }
}
