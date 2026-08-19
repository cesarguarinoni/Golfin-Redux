// Order: login_signup_screens Phase 2b — OAuth URL builder + callback parser tests
using NUnit.Framework;
using UnityEngine;
using Golfin.Auth;

namespace Golfin.Auth.Tests
{
    public class OAuthUrlBuilderTests
    {
        private SupabaseConfig Cfg()
        {
            var c = ScriptableObject.CreateInstance<SupabaseConfig>();
            c.supabaseUrl = "https://proj.supabase.co";
            c.oauthRedirect = "golfin://auth-callback";
            return c;
        }

        [Test]
        public void ProviderKey_MapsGoogleAndApple()
        {
            Assert.AreEqual("google", OAuthUrlBuilder.ProviderKey(OAuthProvider.Google));
            Assert.AreEqual("apple",  OAuthUrlBuilder.ProviderKey(OAuthProvider.Apple));
        }

        [Test]
        public void Authorize_BuildsUrlWithEscapedRedirect()
        {
            string url = OAuthUrlBuilder.Authorize(Cfg(), OAuthProvider.Google);
            Assert.AreEqual(
                "https://proj.supabase.co/auth/v1/authorize?provider=google&redirect_to=golfin%3A%2F%2Fauth-callback",
                url);
        }
    }

    /// <summary>auth_email_redirect — redirect_to must ride the query string on the email endpoints.</summary>
    public class AuthRedirectUrlTests
    {
        [Test]
        public void Append_UsesQuestionMark_WhenPathHasNoQuery()
        {
            Assert.AreEqual("/signup?redirect_to=https%3A%2F%2Fconfirm.golfin.world%2F",
                AuthRedirectUrl.Append("/signup", "https://confirm.golfin.world/"));
        }

        [Test]
        public void Append_UsesAmpersand_WhenPathAlreadyHasQuery()
        {
            Assert.AreEqual("/verify?type=recovery&redirect_to=golfin%3A%2F%2Fauth-callback",
                AuthRedirectUrl.Append("/verify?type=recovery", "golfin://auth-callback"));
        }

        [Test]
        public void Append_EscapesTheRecoveryQueryInsideTheRedirect()
        {
            // The '?type=recovery' belongs to the landing page, so it must survive as escaped payload.
            Assert.AreEqual("/recover?redirect_to=https%3A%2F%2Fconfirm.golfin.world%2F%3Ftype%3Drecovery",
                AuthRedirectUrl.Append("/recover", "https://confirm.golfin.world/?type=recovery"));
        }

        [Test]
        public void Append_ReturnsPathUnchanged_WhenRedirectIsEmpty()
        {
            // Clearing the SupabaseConfig field restores the old behaviour (Supabase Site URL fallback).
            Assert.AreEqual("/signup", AuthRedirectUrl.Append("/signup", ""));
            Assert.AreEqual("/signup", AuthRedirectUrl.Append("/signup", null));
        }
    }

    public class OAuthCallbackParserTests
    {
        private SupabaseConfig Cfg()
        {
            var c = ScriptableObject.CreateInstance<SupabaseConfig>();
            c.oauthRedirect = "golfin://auth-callback";
            return c;
        }

        [Test]
        public void IsCallback_MatchesScheme_RejectsOthers()
        {
            var cfg = Cfg();
            Assert.IsTrue(OAuthCallbackParser.IsCallback("golfin://auth-callback#access_token=x", cfg));
            Assert.IsFalse(OAuthCallbackParser.IsCallback("https://example.com/#access_token=x", cfg));
            Assert.IsFalse(OAuthCallbackParser.IsCallback("", cfg));
        }

        [Test]
        public void Parse_FragmentTokens_ProducesSessionWithExpiry()
        {
            string url = "golfin://auth-callback#access_token=AAA&refresh_token=RRR&expires_in=3600&token_type=bearer";
            var r = OAuthCallbackParser.Parse(url, nowUnix: 1000);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.HasSession);
            Assert.AreEqual("AAA", r.AccessToken);
            Assert.AreEqual("RRR", r.RefreshToken);
            Assert.AreEqual(1000 + 3600, r.ExpiresAtUnix);
        }

        [Test]
        public void Parse_ErrorFragment_Fails()
        {
            string url = "golfin://auth-callback#error=access_denied&error_description=User+cancelled";
            var r = OAuthCallbackParser.Parse(url, nowUnix: 0);
            Assert.IsFalse(r.Success);
            StringAssert.Contains("cancelled", r.Message);
        }

        [Test]
        public void Parse_ErrorInQuery_Fails()
        {
            var r = OAuthCallbackParser.Parse("golfin://auth-callback?error=server_error", 0);
            Assert.IsFalse(r.Success);
        }

        [Test]
        public void Parse_NoToken_Fails()
        {
            var r = OAuthCallbackParser.Parse("golfin://auth-callback#foo=bar", 0);
            Assert.IsFalse(r.Success);
        }
    }

    public class MockOAuthAndGetUserTests
    {
        [Test]
        public void GetUser_ReturnsProfileForValidToken()
        {
            var m = new MockSupabaseAuthClient();
            m.SeedAccount("c@user.com", "Abc123!@", confirmed: true, displayName: "Ace");
            string token = null;
            m.SignInWithPassword("c@user.com", "Abc123!@", r => token = r.AccessToken);
            AuthResult got = null;
            m.GetUser(token, r => got = r);
            Assert.IsTrue(got.Success);
            Assert.AreEqual("Ace", got.User.DisplayName);
        }

        [Test]
        public void OAuth_ComingSoonByDefault()
        {
            var m = new MockSupabaseAuthClient();
            AuthResult r = null;
            m.SignInWithOAuth(OAuthProvider.Apple, x => r = x);
            Assert.IsFalse(r.Success);
            Assert.AreEqual(AuthError.NotImplemented, r.Error);
        }

        [Test]
        public void OAuth_SimulatesSessionWhenEnabled()
        {
            var m = new MockSupabaseAuthClient { SimulateOAuthSuccess = true };
            AuthResult r = null;
            m.SignInWithOAuth(OAuthProvider.Google, x => r = x);
            Assert.IsTrue(r.Success);
            Assert.IsTrue(r.HasSession);
        }
    }
}
