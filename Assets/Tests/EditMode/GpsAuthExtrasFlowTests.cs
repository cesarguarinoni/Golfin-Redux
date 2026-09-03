// Assets/Tests/EditMode/GpsAuthExtrasFlowTests.cs
// auth_golf_profile — EditMode tests for the once-per-device post-signup offer.
// gps_profile_prompt_on_entry — plus the intercept that decides WHERE it fires. The trigger moved
// off Home and onto the first entry into the GPS hub, so two things are pinned here: the intercept
// table (which requested screen becomes which target, for both answers of ShouldOffer), and the
// absence of the old Home hand-off, which is the whole point of the change.
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
        Type _screenId;
        MethodInfo _intercept;

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

            _screenId = FindType("GolfinRedux.UI.ScreenId");
            Assert.IsNotNull(_screenId, "GolfinRedux.UI.ScreenId not found.");

            _intercept = _flow.GetMethod(
                "InterceptHubEntry",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { _screenId, typeof(bool) },
                null);
            Assert.IsNotNull(_intercept,
                "InterceptHubEntry(ScreenId requested, bool offer) not found — it is the seam " +
                "ScreenManager.Navigate and (later) the standalone GPS shell both route through.");
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

        object Screen(string name) => Enum.Parse(_screenId, name);

        string Intercept(string requested, bool offer)
            => _intercept.Invoke(null, new[] { Screen(requested), (object)offer }).ToString();

        // ── The shipped "punch it" build: the offer is a no-op, whatever else is true ──
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

        // ═══════════════════════════════════════════════════════════════════════════════════
        // gps_profile_prompt_on_entry — WHERE the offer fires
        // ═══════════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The intercept table, pinned in full. One row per (requested screen × offer) pair, so a
        /// change to which screens are diverted has to be made on purpose. The GPS screens other
        /// than the hub matter most: the capture and the tutorial ARE GPS screens, and a rule
        /// phrased as "any GPS screen" would divert GpsGolfProfile to itself forever.
        /// </summary>
        [Test]
        public void InterceptHubEntry_DivertsOnlyTheHub_AndOnlyWhenOffering()
        {
            // offer = true → the one diversion, and nothing else moves.
            Assert.AreEqual("GpsGolfProfile", Intercept("GpsHub", true),
                "The first entry into the GPS hub is the trigger — that is the whole feature.");
            Assert.AreEqual("GpsGolfProfile", Intercept("GpsGolfProfile", true),
                "Diverting the capture itself would bounce it to itself forever.");
            Assert.AreEqual("GpsWelcome", Intercept("GpsWelcome", true),
                "The tutorial is a GPS screen and must pass through untouched.");
            Assert.AreEqual("GpsProfile", Intercept("GpsProfile", true),
                "Only the hub is the entry point; deeper GPS screens are reached from inside it.");
            Assert.AreEqual("Home", Intercept("Home", true),
                "gps_profile_prompt_on_entry — Home NEVER offers. A fresh install lands on the " +
                "game and stays there; this assertion is the device-pass finding in code.");

            // offer = false → identity for everything, hub included.
            foreach (string s in new[] { "GpsHub", "GpsGolfProfile", "GpsWelcome", "GpsProfile", "Home" })
                Assert.AreEqual(s, Intercept(s, false),
                    $"With the offer spent (or a build without GPS), {s} must navigate to itself.");
        }

        /// <summary>
        /// The "punch it" build again, at the intercept rather than at ShouldOffer: with GPS off
        /// the offer is false, so a hub entry — if one were even reachable — is never diverted
        /// into a screen the build is supposed not to have.
        /// </summary>
        [Test]
        public void InterceptHubEntry_IsIdentity_ForEveryNonOfferingBuildState()
        {
            foreach (bool signedIn in new[] { true, false })
                foreach (bool prompted in new[] { true, false })
                {
                    bool offer = Offer(false, signedIn, prompted);
                    Assert.IsFalse(offer);
                    Assert.AreEqual("GpsHub", Intercept("GpsHub", offer),
                        $"signedIn={signedIn}, prompted={prompted}");
                }

            // ...and inside a GPS build, the two live reasons not to offer.
            Assert.AreEqual("GpsHub", Intercept("GpsHub", Offer(true, false, false)), "not signed in");
            Assert.AreEqual("GpsHub", Intercept("GpsHub", Offer(true, true, true)), "already answered");
            Assert.AreEqual("GpsGolfProfile", Intercept("GpsHub", Offer(true, true, false)),
                "signed in, GPS build, never answered — the one case that offers.");
        }

        /// <summary>
        /// The marker the standalone shell will read, and that both Welcome exits clear. Asserted
        /// as a settable public property rather than through a behaviour, because its only job in
        /// this change is to exist as a seam with a defined default.
        /// </summary>
        [Test]
        public void PendingHubEntry_IsAPublicResettableFlag()
        {
            var prop = _flow.GetProperty("PendingHubEntry", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(prop, "PendingHubEntry must be public — ScreenManager sets it and " +
                                   "GpsWelcomeScreenController clears it, from another namespace.");
            Assert.IsTrue(prop.CanRead && prop.CanWrite);

            bool original = (bool)prop.GetValue(null);
            try
            {
                prop.SetValue(null, true);
                Assert.IsTrue((bool)prop.GetValue(null));
                prop.SetValue(null, false);
                Assert.IsFalse((bool)prop.GetValue(null),
                    "Both Welcome exits clear it; a flag that cannot be cleared would say a hub " +
                    "entry is in flight for the rest of the session.");
            }
            finally { prop.SetValue(null, original); }
        }

        /// <summary>
        /// The removal, asserted rather than assumed. HomeScreenController used to hand off to the
        /// capture from a deferred coroutine on its OnEnable; the device-pass finding was that a
        /// fresh install must not do that. If someone re-adds the Home trigger, this fails —
        /// which is the only way an EditMode test can defend a deletion.
        /// </summary>
        [Test]
        public void Home_NoLongerCarriesTheOffer()
        {
            var home = FindType("GolfinRedux.UI.HomeScreenController");
            Assert.IsNotNull(home, "HomeScreenController not found — did Assembly-CSharp compile?");

            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static
                                   | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.IsNull(home.GetMethod("OfferGolfProfileNextFrame", all),
                "gps_profile_prompt_on_entry — the Home hand-off coroutine is gone for good. The " +
                "offer belongs on the first GPS entry (GpsAuthExtrasFlow.InterceptHubEntry, called " +
                "from ScreenManager.Navigate), not on the first Home entry.");
        }
    }
}
