// home_carousel_and_daily_timing — the daily "last answer" statics are forgotten on an ACCOUNT
// SWITCH and on nothing else. AuthService.SignedIn fires on a token refresh too, so the decision
// is keyed on the user id, and the pill must not blank mid-session.
//
// DailyMissionSessionReset lives in Assembly-CSharp (a predefined assembly), so it is reached by
// name — the same route ContentArtFetchTests takes.
using System;
using System.Reflection;
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Gameplay.Missions;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class DailyMissionSessionResetTests
    {
        private Type _reset;
        private MissionsClient _missions;

        [SetUp]
        public void SetUp()
        {
            _reset = Type.GetType("Golfin.UI.Home.DailyMissionSessionReset, Assembly-CSharp");
            Assert.NotNull(_reset, "DailyMissionSessionReset not found in Assembly-CSharp.");
            Invoke("ResetForTest");

            _missions = new MissionsClient(null);
            MissionsClient.ConfigureForTest(_missions);
            DailyMissionState.ResetForTest();
        }

        [TearDown]
        public void TearDown()
        {
            Invoke("ResetForTest");
            MissionsClient.ResetForTest();
            DailyMissionState.ResetForTest();
        }

        private object Invoke(string name, params object[] args)
            => _reset.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);

        private bool SignedIn(string userId)
            => (bool)Invoke("Handle", new AuthSession { UserId = userId, AccessToken = "tok" });

        private void SeedTodaysAnswer()
        {
            var answer = new DailyMissionResult { Date = "2026-09-11", Streak = 3, Claimed = false, RecipeHash = "abc" };
            typeof(MissionsClient).GetProperty("LastDaily").GetSetMethod(true).Invoke(_missions, new object[] { answer });
            DailyMissionState.Set("2026-09-11", 3, claimed: false, hasRecipe: true);
            Assert.IsNotNull(_missions.LastDaily);
            Assert.IsTrue(DailyMissionState.HasRecipe);
        }

        [Test]
        public void FirstSession_RemembersTheUser_ForgetsNothing()
        {
            SeedTodaysAnswer();
            Assert.IsFalse(SignedIn("user-a"));
            Assert.IsNotNull(_missions.LastDaily);
            Assert.IsTrue(DailyMissionState.HasRecipe);
        }

        [Test]
        public void TokenRefresh_SameUser_ForgetsNothing()
        {
            SignedIn("user-a");
            SeedTodaysAnswer();
            Assert.IsFalse(SignedIn("user-a"), "a refresh carries the same user");
            Assert.IsNotNull(_missions.LastDaily, "the card would otherwise hold an empty slot on the next entry");
            Assert.IsTrue(DailyMissionState.HasRecipe, "the Home pill would otherwise leave mid-session");
        }

        [Test]
        public void AnotherUser_ForgetsBoth()
        {
            SignedIn("user-a");
            SeedTodaysAnswer();
            Assert.IsTrue(SignedIn("user-b"));
            Assert.IsNull(_missions.LastDaily);
            Assert.IsFalse(DailyMissionState.HasRecipe);
            Assert.IsFalse(DailyMissionState.Known);
        }

        [Test]
        public void SessionWithoutAUserId_IsIgnored()
        {
            SignedIn("user-a");
            SeedTodaysAnswer();
            Assert.IsFalse(SignedIn(null));
            Assert.IsFalse(SignedIn(""));
            Assert.IsNotNull(_missions.LastDaily);
            // ...and did not overwrite the remembered user: user-b afterwards is still a switch.
            Assert.IsTrue(SignedIn("user-b"));
        }
    }
}
