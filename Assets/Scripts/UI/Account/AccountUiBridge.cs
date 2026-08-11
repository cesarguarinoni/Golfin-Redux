// Order: account_flow_wiring — session → shell-UI bridge
using Golfin.Auth;

namespace Golfin.UI.Account
{
    /// <summary>
    /// Pushes auth-session identity into the persistent shell UI. Lives in Assembly-CSharp because
    /// Golfin.Auth (its own asmdef) cannot reference PersistentUIManager; the UI layer bridges instead.
    /// Call after any event that establishes or changes the signed-in user (login, OAuth, Create
    /// Username, boot session restore).
    /// </summary>
    public static class AccountUiBridge
    {
        /// <summary>Top bar shows the session display_name (replaces the designer placeholder "CHOTO").</summary>
        public static void SyncUsername()
        {
            var session = AuthService.Instance.Session;
            if (!session.HasDisplayName) return;
            if (PersistentUIManager.Instance != null)
                PersistentUIManager.Instance.UpdateUsername(session.DisplayName);
        }
    }
}
