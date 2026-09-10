// Order: oauth_callback_per_app — the per-app redirect scheme
//
// The game and the PLAYLIFE shell install beside each other, and iOS hands a custom-scheme URL to
// whichever app claims it. These pin the two things that keep a sign-in in the app that started
// it: the OAuth callback is re-schemed for the current binary, and the confirm-email landing page
// is told which app to hop back into. The enabled (shell) branch is reached through the explicit
// scheme overloads, exactly as StandaloneGateTests reach StandaloneGate's — the define never
// reaches editor compilation.
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Auth.Tests
{
    public class AppDeepLinkTests
    {
        [Test]
        public void Scheme_IsOneOfTheTwoShippedSchemes()
        {
            Assert.That(AppDeepLink.Scheme, Is.EqualTo(AppDeepLink.GameScheme).Or.EqualTo(AppDeepLink.StandaloneScheme));
            Assert.AreNotEqual(AppDeepLink.GameScheme, AppDeepLink.StandaloneScheme,
                "Two apps claiming one scheme is the bug this file exists to prevent.");
        }

        [Test]
        public void WithScheme_ReplacesOnlyTheScheme()
        {
            Assert.AreEqual("golfingps://auth-callback",
                AppDeepLink.WithScheme("golfin://auth-callback", AppDeepLink.StandaloneScheme));
            Assert.AreEqual("golfin://auth-callback",
                AppDeepLink.WithScheme("golfingps://auth-callback", AppDeepLink.GameScheme));
            // Host, path, query and fragment ride along untouched.
            Assert.AreEqual("golfingps://auth-callback/x?y=1#access_token=t",
                AppDeepLink.WithScheme("golfin://auth-callback/x?y=1#access_token=t", "golfingps"));
        }

        [Test]
        public void WithScheme_LeavesUnschemedOrEmptyInputAlone()
        {
            // A cleared config field must keep meaning "no redirect" (Supabase Site URL fallback).
            Assert.IsNull(AppDeepLink.WithScheme(null, "golfingps"));
            Assert.AreEqual("", AppDeepLink.WithScheme("", "golfingps"));
            Assert.AreEqual("auth-callback", AppDeepLink.WithScheme("auth-callback", "golfingps"));
        }

        [Test]
        public void TagLandingPage_AppendsAppForTheShell_WithTheRightSeparator()
        {
            Assert.AreEqual("https://confirm.golfin.world/?app=golfingps",
                AppDeepLink.TagLandingPage("https://confirm.golfin.world/", AppDeepLink.StandaloneScheme));
            Assert.AreEqual("https://confirm.golfin.world/?type=recovery&app=golfingps",
                AppDeepLink.TagLandingPage("https://confirm.golfin.world/?type=recovery", AppDeepLink.StandaloneScheme));
        }

        [Test]
        public void TagLandingPage_LeavesTheGameUrlByteIdentical()
        {
            // What the game sends today is what is allow-listed and proven; the page defaults to golfin.
            Assert.AreEqual("https://confirm.golfin.world/",
                AppDeepLink.TagLandingPage("https://confirm.golfin.world/", AppDeepLink.GameScheme));
            Assert.AreEqual("https://confirm.golfin.world/?type=recovery",
                AppDeepLink.TagLandingPage("https://confirm.golfin.world/?type=recovery", AppDeepLink.GameScheme));
            Assert.AreEqual("", AppDeepLink.TagLandingPage("", AppDeepLink.StandaloneScheme));
            Assert.IsNull(AppDeepLink.TagLandingPage(null, AppDeepLink.StandaloneScheme));
        }

        [Test]
        public void Config_ForThisAppProperties_DeriveFromTheAuthoredFields()
        {
            var c = ScriptableObject.CreateInstance<SupabaseConfig>();
            c.oauthRedirect         = "golfin://auth-callback";
            c.emailConfirmRedirect  = "https://confirm.golfin.world/";
            c.passwordResetRedirect = "https://confirm.golfin.world/?type=recovery";

            Assert.AreEqual(AppDeepLink.WithScheme(c.oauthRedirect, AppDeepLink.Scheme), c.OAuthRedirectForThisApp);
            Assert.AreEqual(AppDeepLink.TagLandingPage(c.emailConfirmRedirect, AppDeepLink.Scheme), c.EmailConfirmRedirectForThisApp);
            Assert.AreEqual(AppDeepLink.TagLandingPage(c.passwordResetRedirect, AppDeepLink.Scheme), c.PasswordResetRedirectForThisApp);
            StringAssert.StartsWith(AppDeepLink.Scheme + "://", c.OAuthRedirectForThisApp);
        }

        [Test]
        public void IsCallback_AcceptsThisAppsSchemeOnly()
        {
            var c = ScriptableObject.CreateInstance<SupabaseConfig>();
            c.oauthRedirect = "golfin://auth-callback";

            string mine  = AppDeepLink.Scheme + "://auth-callback#access_token=x";
            string other = (AppDeepLink.Scheme == AppDeepLink.GameScheme ? AppDeepLink.StandaloneScheme : AppDeepLink.GameScheme)
                           + "://auth-callback#access_token=x";

            Assert.IsTrue(OAuthCallbackParser.IsCallback(mine, c));
            Assert.IsFalse(OAuthCallbackParser.IsCallback(other, c),
                "The other app's callback can never reach this binary (it does not claim that scheme) — and must not be accepted if it somehow did.");
        }
    }
}
