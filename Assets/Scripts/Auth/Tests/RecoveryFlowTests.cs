// Order: auth_recovery_flow — parser type/error extraction, hold-don't-persist, UpdatePassword
using NUnit.Framework;
using UnityEngine;
using Golfin.Auth;

namespace Golfin.Auth.Tests
{
    public class CallbackInfoTests
    {
        [Test]
        public void GetCallbackInfo_RecoveryFragment_ExposesType()
        {
            var info = OAuthCallbackParser.GetCallbackInfo(
                "golfin://auth-callback#access_token=abc&expires_in=3600&refresh_token=r&token_type=bearer&type=recovery");
            Assert.AreEqual("recovery", info.Type);
            Assert.IsTrue(info.IsRecovery);
            Assert.IsFalse(info.HasError);
        }

        [Test]
        public void GetCallbackInfo_SignupFragment_IsNotRecovery()
        {
            var info = OAuthCallbackParser.GetCallbackInfo(
                "golfin://auth-callback#access_token=abc&type=signup");
            Assert.AreEqual("signup", info.Type);
            Assert.IsFalse(info.IsRecovery);
            Assert.IsFalse(info.HasError);
        }

        [Test]
        public void GetCallbackInfo_ExpiredLinkFragment_ExposesErrorTriplet()
        {
            var info = OAuthCallbackParser.GetCallbackInfo(
                "golfin://auth-callback#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired");
            Assert.IsTrue(info.HasError);
            Assert.AreEqual("access_denied", info.Error);
            Assert.AreEqual("otp_expired", info.ErrorCode);
            Assert.AreEqual("Email link is invalid or has expired", info.ErrorDescription);
            Assert.IsFalse(info.IsRecovery);
        }

        [Test]
        public void GetCallbackInfo_NoTypeNoError_IsEmpty()
        {
            var info = OAuthCallbackParser.GetCallbackInfo("golfin://auth-callback#access_token=abc");
            Assert.IsNull(info.Type);
            Assert.IsFalse(info.IsRecovery);
            Assert.IsFalse(info.HasError);
        }
    }

    public class UpdatePasswordClientTests
    {
        [Test]
        public void PasswordBody_IsTopLevelPasswordField_WithEscaping()
        {
            Assert.AreEqual("{\"password\":\"NewPass123!\"}", SupabaseAuthClient.PasswordBody("NewPass123!"));
            Assert.AreEqual("{\"password\":\"a\\\"b\\\\c\"}",  SupabaseAuthClient.PasswordBody("a\"b\\c"));
        }

        [Test]
        public void Mock_UpdatePassword_ChangesThePassword()
        {
            var m = new MockSupabaseAuthClient();
            m.SeedAccount("p@user.com", "OldPass123!", confirmed: true);
            AuthResult login = null;
            m.SignInWithPassword("p@user.com", "OldPass123!", r => login = r);
            Assert.IsTrue(login.Success);

            AuthResult upd = null;
            m.UpdatePassword(login.AccessToken, "NewPass123!", r => upd = r);
            Assert.IsTrue(upd.Success);

            AuthResult oldLogin = null, newLogin = null;
            m.SignInWithPassword("p@user.com", "OldPass123!", r => oldLogin = r);
            m.SignInWithPassword("p@user.com", "NewPass123!", r => newLogin = r);
            Assert.IsFalse(oldLogin.Success, "old password must be rejected after the update");
            Assert.AreEqual(AuthError.InvalidCredentials, oldLogin.Error);
            Assert.IsTrue(newLogin.Success, "new password must work after the update");
        }

        [Test]
        public void Mock_UpdatePassword_BadToken_FailsInvalidCredentials()
        {
            var m = new MockSupabaseAuthClient();
            AuthResult r0 = null;
            m.UpdatePassword("mock-access.nope", "NewPass123!", r => r0 = r);
            Assert.IsFalse(r0.Success);
            Assert.AreEqual(AuthError.InvalidCredentials, r0.Error);
        }

        [Test]
        public void Mock_UpdatePassword_ShortPassword_FailsWeakPassword()
        {
            var m = new MockSupabaseAuthClient();
            m.SeedAccount("p@user.com", "OldPass123!", confirmed: true);
            AuthResult login = null;
            m.SignInWithPassword("p@user.com", "OldPass123!", r => login = r);
            AuthResult upd = null;
            m.UpdatePassword(login.AccessToken, "short", r => upd = r);
            Assert.IsFalse(upd.Success);
            Assert.AreEqual(AuthError.WeakPassword, upd.Error);
        }
    }

    /// <summary>
    /// The heart of the task: a recovery deep link must HOLD tokens (nothing persisted, no SignedIn)
    /// until the new password is accepted; error links surface a failure and never sign in; a plain
    /// signup-confirmation callback keeps its pre-task behavior (regression guard).
    /// Drives the real AuthService component through its public HandleAuthCallback seam.
    /// </summary>
    public class RecoveryDeepLinkTests
    {
        private const string PrefsKey = "golfin.auth.session.v1"; // AuthSession.Key

        private GameObject _go;
        private AuthService _svc;
        private MockSupabaseAuthClient _mock;
        private int _signedInCount;
        private AuthResult _recoveryEvent;
        private bool _hadKey;
        private string _savedPrefs;

        [SetUp]
        public void SetUp()
        {
            _hadKey = PlayerPrefs.HasKey(PrefsKey);
            _savedPrefs = _hadKey ? PlayerPrefs.GetString(PrefsKey) : null;
            PlayerPrefs.DeleteKey(PrefsKey);

            _go = new GameObject("[AuthService under test]");
            _svc = _go.AddComponent<AuthService>();
            _mock = new MockSupabaseAuthClient();
            _mock.SeedAccount("r@user.com", "OldPass123!", confirmed: true); // id mock-1000
            _svc.ConfigureForTest(_mock, new AuthSession());

            _signedInCount = 0;
            _recoveryEvent = null;
            AuthService.SignedIn += OnSignedIn;
            AuthService.PasswordRecovery += OnRecovery;
        }

        [TearDown]
        public void TearDown()
        {
            AuthService.SignedIn -= OnSignedIn;
            AuthService.PasswordRecovery -= OnRecovery;
            if (_go != null) Object.DestroyImmediate(_go);
            if (_hadKey) { PlayerPrefs.SetString(PrefsKey, _savedPrefs); PlayerPrefs.Save(); }
            else PlayerPrefs.DeleteKey(PrefsKey);
        }

        private void OnSignedIn(AuthSession s) => _signedInCount++;
        private void OnRecovery(AuthResult r) => _recoveryEvent = r;

        private const string RecoveryUrl =
            "golfin://auth-callback#access_token=mock-access.mock-1000&expires_in=3600&refresh_token=mock-refresh.mock-1000&token_type=bearer&type=recovery";

        [Test]
        public void RecoveryLink_HoldsTokens_DoesNotPersist_DoesNotSignIn()
        {
            _svc.HandleAuthCallback(RecoveryUrl);

            Assert.IsNotNull(_svc.PendingRecovery, "recovery tokens must be held");
            Assert.IsTrue(_svc.PendingRecovery.HasSession);
            Assert.IsFalse(_svc.Session.IsAuthenticated, "session must NOT be established by the link alone");
            Assert.IsFalse(PlayerPrefs.HasKey(PrefsKey), "nothing may be persisted before the password is set");
            Assert.AreEqual(0, _signedInCount, "SignedIn must not fire on a recovery link");
            Assert.IsNotNull(_recoveryEvent, "PasswordRecovery must fire so the UI can route");
            Assert.IsTrue(_recoveryEvent.Success);
        }

        [Test]
        public void UpdatePasswordWithRecovery_PersistsSession_SignsIn_ChangesPassword()
        {
            _svc.HandleAuthCallback(RecoveryUrl);

            AuthResult upd = null;
            _svc.UpdatePasswordWithRecovery("NewPass123!", r => upd = r);

            Assert.IsTrue(upd.Success);
            Assert.IsNull(_svc.PendingRecovery, "held tokens are consumed by the update");
            Assert.IsTrue(_svc.Session.IsAuthenticated, "session becomes real after the update");
            Assert.IsTrue(PlayerPrefs.HasKey(PrefsKey), "session is persisted after the update");
            Assert.AreEqual(1, _signedInCount, "SignedIn fires exactly once, after the update");

            AuthResult oldLogin = null, newLogin = null;
            _mock.SignInWithPassword("r@user.com", "OldPass123!", r => oldLogin = r);
            _mock.SignInWithPassword("r@user.com", "NewPass123!", r => newLogin = r);
            Assert.IsFalse(oldLogin.Success);
            Assert.IsTrue(newLogin.Success);
        }

        [Test]
        public void UpdatePasswordWithRecovery_WithoutALink_Fails()
        {
            AuthResult upd = null;
            _svc.UpdatePasswordWithRecovery("NewPass123!", r => upd = r);
            Assert.IsFalse(upd.Success);
            Assert.AreEqual(AuthError.InvalidCredentials, upd.Error);
            Assert.AreEqual(0, _signedInCount);
        }

        [Test]
        public void ExpiredLink_SurfacesFailure_NeverSignsIn()
        {
            _svc.HandleAuthCallback(
                "golfin://auth-callback#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired");

            Assert.IsNull(_svc.PendingRecovery);
            Assert.IsFalse(_svc.Session.IsAuthenticated);
            Assert.IsFalse(PlayerPrefs.HasKey(PrefsKey));
            Assert.AreEqual(0, _signedInCount);
            Assert.IsNotNull(_recoveryEvent, "failure must be surfaced");
            Assert.IsFalse(_recoveryEvent.Success);

            var consumed = _svc.ConsumeRecoveryFailure();
            Assert.IsNotNull(consumed, "cold-start seam holds the failure until a screen consumes it");
            Assert.IsNull(_svc.ConsumeRecoveryFailure(), "consumed means consumed");
        }

        [Test]
        public void CancelPasswordRecovery_DropsHeldTokens()
        {
            _svc.HandleAuthCallback(RecoveryUrl);
            Assert.IsNotNull(_svc.PendingRecovery);
            _svc.CancelPasswordRecovery();
            Assert.IsNull(_svc.PendingRecovery);
            Assert.IsFalse(_svc.Session.IsAuthenticated);
        }

        /// <summary>Regression guard — a plain signup-confirmation callback (tokens, type=signup)
        /// still establishes + persists the session and raises SignedIn, exactly as before.</summary>
        [Test]
        public void SignupConfirmationLink_StillSignsInAsBefore()
        {
            _svc.HandleAuthCallback(
                "golfin://auth-callback#access_token=mock-access.mock-1000&expires_in=3600&refresh_token=mock-refresh.mock-1000&token_type=bearer&type=signup");

            Assert.IsNull(_svc.PendingRecovery, "signup confirmation is not a recovery");
            Assert.IsTrue(_svc.Session.IsAuthenticated, "confirmation link signs in (pre-task behavior)");
            Assert.IsTrue(PlayerPrefs.HasKey(PrefsKey));
            Assert.AreEqual(1, _signedInCount);
            Assert.IsNull(_recoveryEvent, "PasswordRecovery must not fire for non-recovery links");
        }
    }
}
