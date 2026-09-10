// Assets/Tests/EditMode/StandaloneUrlSchemeTests.cs
// oauth_callback_per_app — the shell's Info.plist claims ONE scheme, and it is the runtime's.
//
// The first cut of StandaloneBuildPreprocessor ADDED golfingps to the game's scheme list, so the
// shell claimed golfin as well. With both apps installed, iOS then handed the game's own
// golfin://auth-callback to the shell, which signed itself in while the game's watchdog timed out
// (2026-09-11). Two strings decide this — the one the preprocessor stamps into the plist and the
// one Golfin.Auth asks Supabase to redirect to — and they live in different assemblies because an
// editor script cannot read a player define. These read both.

using System;
using System.Reflection;
using Golfin.Auth;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    public class StandaloneUrlSchemeTests
    {
        const string PreprocessorTypeName = "Golfin.EditorTools.StandaloneBuildPreprocessor";

        static Type Preprocessor()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(PreprocessorTypeName);
                if (t != null) return t;
            }
            Assert.Fail($"{PreprocessorTypeName} not found — did Assembly-CSharp-Editor compile?");
            return null;
        }

        static string StampedScheme()
            => (string)Preprocessor().GetField("UrlScheme", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        static string[] StampedSchemes()
            => (string[])Preprocessor().GetProperty("StandaloneUrlSchemes", BindingFlags.Public | BindingFlags.Static).GetValue(null);

        [Test]
        public void Preprocessor_StampsTheSchemeTheRuntimeRedirectsTo()
        {
            Assert.AreEqual(AppDeepLink.StandaloneScheme, StampedScheme(),
                "The scheme in the shell's Info.plist and the scheme its OAuth callback comes back on are two copies of one string.");
        }

        [Test]
        public void ShellClaimsExactlyItsOwnScheme_NeverTheGames()
        {
            var schemes = StampedSchemes();
            CollectionAssert.AreEqual(new[] { AppDeepLink.StandaloneScheme }, schemes);
            CollectionAssert.DoesNotContain(schemes, AppDeepLink.GameScheme,
                "A shell that also claims golfin steals the game's sign-in callback when both are installed.");
        }
    }
}
