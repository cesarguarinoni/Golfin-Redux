// Order: login_signup_screens Phase 2 — cross-screen flow carry-forward
namespace Golfin.Auth
{
    /// <summary>
    /// Lightweight cross-screen state for the account flow — replaces the "LoginFlowCoordinator"
    /// placeholder referenced in the Phase 1 stubs. Set on one screen, read on the next, so screens
    /// stay decoupled (each keeps its own ScreenManager navigation).
    ///
    /// e.g. Sign Up sets <see cref="PendingEmail"/>; Email Confirmation reads it to show the address
    /// and to drive Resend.
    /// </summary>
    public static class AuthFlowState
    {
        /// <summary>Email submitted on Sign Up, carried to the Email Confirmation screen.</summary>
        public static string PendingEmail;

        public static void Clear()
        {
            PendingEmail = null;
        }
    }
}
