// Order: login_signup_screens Phase 2 — auth transport + session unit tests
using NUnit.Framework;
using Golfin.Auth;

namespace Golfin.Auth.Tests
{
    public class MockSupabaseAuthClientTests
    {
        private MockSupabaseAuthClient _m;
        private AuthResult _r;

        [SetUp]
        public void SetUp()
        {
            _m = new MockSupabaseAuthClient { AutoConfirmOnSignUp = false };
            _r = null;
        }

        [Test]
        public void SignUp_NewEmail_SucceedsWithNoSession()
        {
            _m.SignUp("new@user.com", "Abc123!@", r => _r = r);
            Assert.IsTrue(_r.Success);
            Assert.IsFalse(_r.HasSession, "signup with confirmation on must not return a session");
            Assert.IsNotNull(_r.User);
            Assert.IsFalse(_r.User.EmailConfirmed);
        }

        [Test]
        public void SignUp_DuplicateEmail_FailsEmailAlreadyRegistered()
        {
            _m.SignUp("dup@user.com", "Abc123!@", _ => { });
            _m.SignUp("dup@user.com", "Abc123!@", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.EmailAlreadyRegistered, _r.Error);
        }

        [Test]
        public void SignUp_InvalidEmail_FailsInvalidEmail()
        {
            _m.SignUp("not-an-email", "Abc123!@", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.InvalidEmail, _r.Error);
        }

        [Test]
        public void SignUp_ShortPassword_FailsWeakPassword()
        {
            _m.SignUp("a@b.com", "short", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.WeakPassword, _r.Error);
        }

        [Test]
        public void Login_Unconfirmed_FailsEmailNotConfirmed()
        {
            _m.SignUp("u@user.com", "Abc123!@", _ => { });          // AutoConfirm off → unconfirmed
            _m.SignInWithPassword("u@user.com", "Abc123!@", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.EmailNotConfirmed, _r.Error);
        }

        [Test]
        public void Login_Confirmed_ReturnsSession()
        {
            _m.SeedAccount("c@user.com", "Abc123!@", confirmed: true);
            _m.SignInWithPassword("c@user.com", "Abc123!@", r => _r = r);
            Assert.IsTrue(_r.Success);
            Assert.IsTrue(_r.HasSession);
            Assert.IsNotEmpty(_r.AccessToken);
            Assert.IsNotEmpty(_r.RefreshToken);
            Assert.Greater(_r.ExpiresAtUnix, 0);
        }

        [Test]
        public void Login_WrongPassword_FailsInvalidCredentials()
        {
            _m.SeedAccount("c@user.com", "Abc123!@", confirmed: true);
            _m.SignInWithPassword("c@user.com", "wrong", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.InvalidCredentials, _r.Error);
        }

        [Test]
        public void Login_UnknownEmail_FailsInvalidCredentials()
        {
            _m.SignInWithPassword("ghost@user.com", "whatever", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.InvalidCredentials, _r.Error);
        }

        [Test]
        public void Resend_And_Reset_AlwaysSucceed_NoEnumeration()
        {
            _m.ResendConfirmation("anyone@user.com", r => _r = r);
            Assert.IsTrue(_r.Success);
            _m.RequestPasswordReset("anyone@user.com", r => _r = r);
            Assert.IsTrue(_r.Success);
        }

        [Test]
        public void UpdateDisplayName_SetsName()
        {
            _m.SeedAccount("c@user.com", "Abc123!@", confirmed: true);
            string token = null;
            _m.SignInWithPassword("c@user.com", "Abc123!@", r => token = r.AccessToken);
            _m.UpdateDisplayName(token, "Birdie99", r => _r = r);
            Assert.IsTrue(_r.Success);
            Assert.AreEqual("Birdie99", _r.User.DisplayName);
        }

        [Test]
        public void RefreshSession_ReturnsFreshSession()
        {
            _m.SeedAccount("c@user.com", "Abc123!@", confirmed: true);
            string refresh = null;
            _m.SignInWithPassword("c@user.com", "Abc123!@", r => refresh = r.RefreshToken);
            _m.RefreshSession(refresh, r => _r = r);
            Assert.IsTrue(_r.Success);
            Assert.IsTrue(_r.HasSession);
        }

        [Test]
        public void OAuth_ReturnsNotImplemented()
        {
            _m.SignInWithOAuth(OAuthProvider.Google, r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.NotImplemented, _r.Error);
        }

        [Test]
        public void SimulateNetwork_FailsNetwork()
        {
            _m.SimulateNetwork = true;
            _m.SignInWithPassword("c@user.com", "x", r => _r = r);
            Assert.IsFalse(_r.Success);
            Assert.AreEqual(AuthError.Network, _r.Error);
        }
    }

    public class AuthSessionTests
    {
        [Test]
        public void ApplyFrom_CopiesTokensAndUser()
        {
            var s = new AuthSession();
            var user = new AuthUser { Id = "u1", Email = "e@x.com", DisplayName = "Name", EmailConfirmed = true };
            s.ApplyFrom(AuthResult.Ok(user, "acc", "ref", 12345));
            Assert.IsTrue(s.IsAuthenticated);
            Assert.AreEqual("acc", s.AccessToken);
            Assert.AreEqual("ref", s.RefreshToken);
            Assert.AreEqual(12345, s.ExpiresAtUnix);
            Assert.AreEqual("u1", s.UserId);
            Assert.IsTrue(s.HasDisplayName);
        }

        [Test]
        public void ApplyFrom_NoSessionResult_KeepsUnauthenticated()
        {
            var s = new AuthSession();
            s.ApplyFrom(AuthResult.Ok(new AuthUser { Id = "u", Email = "e@x.com" })); // signup, no tokens
            Assert.IsFalse(s.IsAuthenticated);
            Assert.AreEqual("u", s.UserId);
        }

        [Test]
        public void IsExpired_RespectsSkew()
        {
            var s = new AuthSession { ExpiresAtUnix = 1000 };
            Assert.IsFalse(s.IsExpired(900, skewSeconds: 60)); // 900 < 940
            Assert.IsTrue(s.IsExpired(950, skewSeconds: 60));  // 950 >= 940
            Assert.IsTrue(s.IsExpired(1000, skewSeconds: 60));
        }

        [Test]
        public void SaveLoadClear_RoundTrips()
        {
            var s = new AuthSession();
            s.ApplyFrom(AuthResult.Ok(new AuthUser { Id = "u9", Email = "r@t.com", DisplayName = "Robin" }, "A", "R", 777));
            s.Save();

            var loaded = new AuthSession();
            loaded.Load();
            Assert.AreEqual("A", loaded.AccessToken);
            Assert.AreEqual("u9", loaded.UserId);
            Assert.AreEqual("Robin", loaded.DisplayName);

            loaded.Clear();
            var afterClear = new AuthSession();
            afterClear.Load();
            Assert.IsFalse(afterClear.IsAuthenticated);
        }
    }
}
