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

            // gps_profile_prompt_server_flag — the core took a FOURTH argument when the offer
            // became once per ACCOUNT: the local PlayerPrefs cache and the server's
            // golf_profile_prompted_at are now separate inputs, because either one alone means
            // answered and the interesting cases are the ones where they disagree.
            _shouldOffer = _flow.GetMethod(
                "ShouldOffer",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) },
                null);
            Assert.IsNotNull(_shouldOffer,
                "ShouldOffer(bool gpsEnabled, bool signedIn, bool promptedLocally, bool promptedOnAccount) " +
                "not found — without it neither the disabled-build branch nor the server flag can be tested.");

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

        /// <summary>The pre-server-flag shape: "prompted" means the LOCAL flag, server unknown.
        /// Every test written before gps_profile_prompt_server_flag keeps meaning what it meant.</summary>
        bool Offer(bool gpsEnabled, bool signedIn, bool prompted)
            => Offer(gpsEnabled, signedIn, prompted, false);

        bool Offer(bool gpsEnabled, bool signedIn, bool promptedLocally, bool promptedOnAccount)
            => (bool)_shouldOffer.Invoke(null,
                   new object[] { gpsEnabled, signedIn, promptedLocally, promptedOnAccount });

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

        // ── gps_profile_prompt_server_flag — once per ACCOUNT, not per device ───────────

        /// <summary>
        /// The whole local × server truth table, in one place.
        ///
        /// <para>Cesar, 2026-09-03: "if I log in for the first time to GPS but had already logged
        /// in from Game and selected my user/colour, that screen should be skipped (and vice
        /// versa)." The row that encodes that sentence is <c>local=false, server=true</c> — a
        /// fresh install of either app on an account that has already answered — and it must NOT
        /// offer.</para>
        ///
        /// <para>OR, never AND. The <c>local=true, server=false</c> row is the offline case and
        /// the returning player: requiring both flags would re-ask everyone the moment the network
        /// was down.</para>
        /// </summary>
        [Test]
        public void ShouldOffer_TruthTable_LocalTimesServer()
        {
            //                                       gps   signedIn  local  server   expect
            Assert.IsTrue (Offer(true,  true,  false, false), "never answered anywhere — the ONLY offer");
            Assert.IsFalse(Offer(true,  true,  true,  false), "answered on this device (offline-safe)");
            Assert.IsFalse(Offer(true,  true,  false, true ), "answered on the ACCOUNT — the fresh-install case");
            Assert.IsFalse(Offer(true,  true,  true,  true ), "answered both ways");

            // The two pre-existing reasons still dominate every column.
            foreach (bool local in new[] { true, false })
                foreach (bool server in new[] { true, false })
                {
                    Assert.IsFalse(Offer(false, true, local, server),
                        $"a \"punch it\" build never offers (local={local}, server={server})");
                    Assert.IsFalse(Offer(true, false, local, server),
                        $"no session means nothing to write to (local={local}, server={server})");
                }
        }

        /// <summary>
        /// The server flag is read from the CACHED profile row, and "no row" is not "no flag":
        /// an unfetched row must never be read as "never answered", because that is the reading
        /// that re-asks a player who already answered on their other app.
        ///
        /// <para>Exercised through the live property with a row injected into
        /// <c>UserService</c> — the flag's whole job is to translate one nullable column into one
        /// bool, and the translation is where a null-vs-empty mistake would live.</para>
        /// </summary>
        [Test]
        public void PromptedOnAccount_ReadsTheColumn_NullAndEmptyBothMeanNeverAsked()
        {
            var svcType = FindType("Golfin.Social.UserService");
            var dtoType = FindType("Golfin.Social.UserDetailDto");
            Assert.IsNotNull(svcType, "Golfin.Social.UserService not found.");
            Assert.IsNotNull(dtoType, "Golfin.Social.UserDetailDto not found.");

            var promptedOnAccount = _flow.GetProperty("PromptedOnAccount",
                                                      BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(promptedOnAccount, "GpsAuthExtrasFlow.PromptedOnAccount not found.");

            var field = dtoType.GetField("GolfProfilePromptedAt");
            Assert.IsNotNull(field, "UserDetailDto.GolfProfilePromptedAt not found.");
            Assert.AreEqual(typeof(string), field.FieldType,
                "The column is carried as a STRING and never parsed — ApiEnvelope reads with " +
                "DateParseHandling.None precisely so it arrives verbatim.");

            var reset = svcType.GetMethod("ResetForTest", BindingFlags.Static | BindingFlags.Public);
            var configure = svcType.GetMethod("ConfigureForTest", BindingFlags.Static | BindingFlags.Public);
            var setDetail = svcType.GetMethod("SetDetailForTest", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(setDetail, "UserService.SetDetailForTest(UserDetailDto, bool) not found.");
            Assert.IsNotNull(configure, "UserService.ConfigureForTest(UserService) not found.");

            try
            {
                // INJECT the singleton rather than letting `Instance` construct one: the lazy path
                // builds an ApiClient, whose coroutine runner calls DontDestroyOnLoad — legal only
                // in play mode, so touching it here throws before a single assertion runs.
                // A null transport is safe because nothing in this test issues a request.
                object svc = Activator.CreateInstance(svcType, new object[] { null });
                configure.Invoke(null, new[] { svc });

                // No row at all — not fetched yet.
                setDetail.Invoke(svc, new object[] { null, false });
                Assert.IsFalse((bool)promptedOnAccount.GetValue(null),
                    "An unfetched row must read as 'unknown', which here means 'do not claim answered'.");

                // Row present, column NULL — genuinely never asked.
                object row = Activator.CreateInstance(dtoType);
                setDetail.Invoke(svc, new object[] { row, true });
                Assert.IsFalse((bool)promptedOnAccount.GetValue(null), "NULL column = never asked.");

                // Row present, column empty string — the same thing, and the reason this is
                // IsNullOrEmpty rather than != null.
                field.SetValue(row, "");
                setDetail.Invoke(svc, new object[] { row, true });
                Assert.IsFalse((bool)promptedOnAccount.GetValue(null), "Empty column = never asked.");

                // Row present, column stamped — answered, on some device, at some point.
                field.SetValue(row, "2026-09-03T21:28:21.295827+00:00");
                setDetail.Invoke(svc, new object[] { row, true });
                Assert.IsTrue((bool)promptedOnAccount.GetValue(null),
                    "A stamped column is the account saying 'already answered' — the whole feature.");
            }
            finally
            {
                reset.Invoke(null, null);
            }
        }

        /// <summary>
        /// The third axis: FETCHED. <c>NeedsAccountCheck</c> is what makes the intercept wait for
        /// <c>/user/detail</c> instead of guessing, and it must be true in exactly one situation —
        /// a hub entry, on a device with no local flag, before the row has arrived.
        ///
        /// <para>Only the two states this test can reach without a live session are asserted here;
        /// the session-dependent rows are covered by the play-mode proof in the report. What
        /// matters and IS reachable: a device that already has the local flag must never wait,
        /// because that is every returning player and every offline launch.</para>
        /// </summary>
        [Test]
        public void NeedsAccountCheck_IsFalse_ForEveryScreenThatIsNotTheHub()
        {
            var m = _flow.GetMethod("NeedsAccountCheck", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(m, "GpsAuthExtrasFlow.NeedsAccountCheck(ScreenId) not found.");

            foreach (string name in new[] { "Home", "GpsProfile", "GpsRounds", "GpsGolfProfile",
                                            "GpsWelcome", "Login", "Splash" })
                Assert.IsFalse((bool)m.Invoke(null, new[] { Screen(name) }),
                    $"{name} is not a hub entry — it must never hold a navigation for a round trip.");
        }

        /// <summary>
        /// Skip WRITES now, and the body it writes is the contract. Pinned on
        /// <c>UserService.BuildUpdateJson</c>, the same seam the wire shape has always been pinned
        /// on: a skip must carry the flag and MUST NOT carry a profile value, because skipping
        /// means the player declined to give one.
        /// </summary>
        [Test]
        public void SkipBody_CarriesTheFlagAndNothingElse()
        {
            var svcType = FindType("Golfin.Social.UserService");
            var build = svcType.GetMethod("BuildUpdateJson", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(build, "UserService.BuildUpdateJson not found.");

            string skip = (string)build.Invoke(null, new object[] { "Cratilo", null, null, null, true });
            StringAssert.Contains("\"display_name\":\"Cratilo\"", skip, "display_name is required by the endpoint.");
            StringAssert.Contains("\"golf_profile_prompted\":true", skip, "the whole point of the Skip write.");
            Assert.IsFalse(skip.Contains("avatar_color"), "Skip must not write a colour the player declined to pick.");
            Assert.IsFalse(skip.Contains("golf_experience"), "Skip must not write an experience band.");
            Assert.IsFalse(skip.Contains("handicap"), "Skip must not write a handicap.");

            // SAVE carries the flag in the SAME put as the profile — never a second write.
            string save = (string)build.Invoke(null, new object[] { "Cratilo", 18.4, "advanced", "green", true });
            StringAssert.Contains("\"golf_profile_prompted\":true", save);
            StringAssert.Contains("\"avatar_color\":\"green\"", save);

            // And every other caller sends a body byte-identical to the pre-feature one.
            string untouched = (string)build.Invoke(null, new object[] { "Cratilo", null, null, null, null });
            Assert.IsFalse(untouched.Contains("golf_profile_prompted"),
                "A caller with no opinion about the prompt must not mention it at all.");
        }

        // ── gps_standalone_shell §7 — the shell's boot goes through this same seam ──────

        /// <summary>
        /// The shell boots to the hub, so its FIRST navigation of a fresh install is exactly the
        /// one the intercept exists for: the capture is offered on the boot itself, not after the
        /// player has found their way somewhere.
        ///
        /// <para>Asserted on the two-arg core rather than through StandaloneShellBoot, which reads
        /// the live session and would need a MonoBehaviour singleton in EditMode. What is being
        /// pinned here is the CONTRACT the shell's boot depends on: a hub entry with the offer
        /// live becomes the capture, and once answered, the hub.</para>
        /// </summary>
        [Test]
        public void ShellBoot_FirstRun_LandsOnTheCapture_ThenOnTheHubOnceAnswered()
        {
            Assert.AreEqual("GpsGolfProfile", Intercept("GpsHub", offer: true),
                "The shell's boot asks for GpsHub; on a first run that must become the capture.");
            Assert.AreEqual("GpsHub", Intercept("GpsHub", offer: false),
                "Once answered, the shell's boot must land on the hub itself.");
        }

        /// <summary>
        /// The shell reaches this seam through <c>StandaloneShellBoot</c>, which the four post-auth
        /// routers call BEFORE StarterGate. Pinned by reflection because the class lives in
        /// Assembly-CSharp: what matters is that the single entry point still exists with the
        /// signature those call sites use — deleting it would silently restore the starter round
        /// trip and the dead-end into a screen the shell refuses.
        /// </summary>
        [Test]
        public void ShellBoot_HasTheSingleSharedEntryPoint()
        {
            var boot = FindType("GolfinRedux.UI.StandaloneShellBoot");
            Assert.IsNotNull(boot, "GolfinRedux.UI.StandaloneShellBoot not found.");

            var m = boot.GetMethod("TryGetPostAuthScreen", BindingFlags.Static | BindingFlags.Public);
            Assert.IsNotNull(m, "StandaloneShellBoot.TryGetPostAuthScreen(out ScreenId) not found.");
            Assert.AreEqual(typeof(bool), m.ReturnType);

            var ps = m.GetParameters();
            Assert.AreEqual(1, ps.Length);
            Assert.IsTrue(ps[0].IsOut, "The screen must be an out parameter — callers branch on the bool.");
            Assert.AreEqual(_screenId, ps[0].ParameterType.GetElementType());

            // gps_profile_prompt_server_flag — the boot must name the HUB and let Navigate decide
            // the offer. It used to resolve GpsAuthExtrasFlow.InterceptHubEntry itself, which
            // jumped over the account-flag wait and re-offered the capture on a fresh install of an
            // account that had already answered in the game. StandaloneGate.Enabled is false in the
            // Editor so the call returns false, but the out-param is still the boot's destination —
            // and it must be the hub, never the capture.
            object[] args = { null };
            bool taken = (bool)m.Invoke(null, args);
            Assert.IsFalse(taken, "The gate is off in the Editor, so the shell branch must not be taken.");
            Assert.AreEqual("GpsHub", args[0].ToString(),
                "The shell's post-auth destination is the hub. Resolving the Golf Profile offer here " +
                "instead of in Navigate is the round-2 defect — do not put it back.");
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
