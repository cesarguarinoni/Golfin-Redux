// Assets/Tests/EditMode/GpsAuthExtrasFlowTests.cs
// auth_golf_profile — EditMode tests for the once-per-device post-signup offer.
//
// ASSEMBLY: GolfinRedux.Tests.EditMode (asmdef, autoReferenced:false). GpsAuthExtrasFlow lives in
// Assembly-CSharp, which an asmdef cannot reference, so everything goes through reflection — the
// same pattern GpsGateTests uses and for the same reason.
//
// WHY THE THREE-ARG OVERLOAD IS THE ONE UNDER TEST: GpsGate.Enabled is a const that is `true`
// under UNITY_EDITOR, so the "punch it" branch — where this whole feature must be a no-op — is
// unreachable from the Editor through the public ShouldOffer(). Passing the build state in is the
// only way to assert the behaviour of the build that actually ships without GPS.

using System;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    public class GpsAuthExtrasFlowTests
    {
        const string FlowTypeName = "Golfin.Gps.UI.GpsAuthExtrasFlow";

        Type _flow;
        MethodInfo _shouldOffer;

        [SetUp]
        public void SetUp()
        {
            _flow = FindType(FlowTypeName);
            Assert.IsNotNull(_flow, $"{FlowTypeName} not found — did Assembly-CSharp compile?");

            _shouldOffer = _flow.GetMethod(
                "ShouldOffer",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(bool), typeof(bool), typeof(bool) },
                null);
            Assert.IsNotNull(_shouldOffer,
                "ShouldOffer(bool gpsEnabled, bool signedIn, bool alreadyPrompted) not found — " +
                "without it the disabled-build branch cannot be tested at all.");
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        bool Offer(bool gpsEnabled, bool signedIn, bool prompted)
            => (bool)_shouldOffer.Invoke(null, new object[] { gpsEnabled, signedIn, prompted });

        // ── The shipped "punch it" build: the Home trigger is a no-op, whatever else is true ──
        [Test]
        public void NeverOffers_WhenGpsIsDisabled()
        {
            foreach (bool signedIn in new[] { true, false })
                foreach (bool prompted in new[] { true, false })
                    Assert.IsFalse(Offer(false, signedIn, prompted),
                        $"A build without GOLFIN_GPS must never offer the Golf Profile capture " +
                        $"(signedIn={signedIn}, prompted={prompted}). Both new screens are on " +
                        $"GpsGate's list, so showing one there would be a screen the build " +
                        $"is supposed not to have.");
        }

        // ── The one case that offers ─────────────────────────────────────────────────────────
        [Test]
        public void Offers_OnlyWhenEnabledAndSignedInAndNotYetPrompted()
        {
            Assert.IsTrue(Offer(true, true, false), "GPS build + signed in + never answered => offer.");
        }

        [Test]
        public void NeverOffers_WhenNotSignedIn()
        {
            // The screen writes to the caller's own profiles row over a bearer token; with no
            // session the offer would end in a 403 rather than in a saved profile.
            Assert.IsFalse(Offer(true, false, false));
            Assert.IsFalse(Offer(true, false, true));
        }

        [Test]
        public void NeverOffers_Twice()
        {
            Assert.IsFalse(Offer(true, true, true),
                "Once the player has answered the screen (SAVE or Skip) it must not come back.");
        }

        /// <summary>
        /// The flag is set by ANSWERING, not by SHOWING — so a player who force-quits while
        /// looking at the screen still gets their one offer. Asserted on the API rather than on a
        /// behaviour so a refactor that starts marking on-show has to change this test on purpose.
        /// </summary>
        [Test]
        public void MarkPrompted_IsSeparateFromShouldOffer()
        {
            var mark = _flow.GetMethod("MarkPrompted", BindingFlags.Static | BindingFlags.Public);
            var clear = _flow.GetMethod("ClearPrompted", BindingFlags.Static | BindingFlags.Public);
            var key = _flow.GetField("PromptedKey", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(mark, "MarkPrompted() must exist — it is the only writer of the flag.");
            Assert.IsNotNull(clear, "ClearPrompted() must exist — the capture run and QA need it.");
            Assert.IsNotNull(key, "PromptedKey must be public so tooling can clear the right key.");
            Assert.AreEqual("gps_profile_prompted", key.GetRawConstantValue());
        }
    }
}
