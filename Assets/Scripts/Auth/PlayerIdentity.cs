using UnityEngine;

namespace Golfin.Auth
{
    /// <summary>
    /// The local player's display name, for everything that shows "who am I" on screen.
    ///
    /// Before this existed each surface invented its own answer — the leaderboard and the tournament
    /// board hard-coded <c>"YOU"</c>, 1v1 used the <c>MATCH_YOU</c> localisation key, and Home wrote
    /// the literal <c>"Player"</c> — so a player who set a username saw it in exactly one place. They
    /// all read from here now, and here reads the auth session, which is the source of truth
    /// (Supabase Auth <c>user_metadata.display_name</c>, persisted by <see cref="AuthSession"/>).
    ///
    /// Every accessor is null-safe: this is called from leaderboards and result screens that must
    /// render for a signed-out or not-yet-named player, so callers supply the fallback they want
    /// rather than getting a hard-coded one. Deliberately free of UI and localisation dependencies
    /// so assemblies like Golfin.Tournaments can use it without pulling either in.
    ///
    /// Edit-mode safe: <see cref="AuthService"/>.Instance LAZILY CREATES a DontDestroyOnLoad
    /// singleton, which throws outside play mode. Callers here include UI that an editor tool or an
    /// inspector preview can drive, so the session is only consulted while playing; in the editor
    /// every caller simply gets its fallback.
    /// </summary>
    public static class PlayerIdentity
    {
        /// <summary>True when a signed-in player has actually chosen a display name.</summary>
        public static bool HasName
        {
            get
            {
                if (!Application.isPlaying) return false;

                var service = AuthService.Instance;
                return service != null && service.Session != null && service.Session.HasDisplayName;
            }
        }

        /// <summary>The player's display name, or an empty string when there isn't one yet.</summary>
        public static string DisplayName
        {
            get
            {
                if (!Application.isPlaying) return string.Empty;

                var service = AuthService.Instance;
                if (service == null || service.Session == null) return string.Empty;
                return service.Session.DisplayName ?? string.Empty;
            }
        }

        /// <summary>
        /// The signed-in player's Supabase user id, or an empty string when there is no session.
        ///
        /// <para>
        /// This is the SAME id every <c>profiles</c> row and every <c>creator_id</c> is keyed on,
        /// so it is what a client-side "is this mine?" test compares against (the Vote screen's
        /// MINE filter). Same null-safety and the same edit-mode guard as
        /// <see cref="DisplayName"/>: outside play mode there is no session and this is empty,
        /// which every caller must treat as "cannot tell", never as "not mine".
        /// </para>
        /// </summary>
        public static string UserId
        {
            get
            {
                if (!Application.isPlaying) return string.Empty;

                var service = AuthService.Instance;
                if (service == null || service.Session == null) return string.Empty;
                return service.Session.UserId ?? string.Empty;
            }
        }

        /// <summary>
        /// The player's display name, falling back to <paramref name="fallback"/> when none is set.
        /// Pass whatever that surface used to show — a localised "YOU", "Player", and so on — so a
        /// signed-out player sees exactly what they saw before.
        /// </summary>
        public static string DisplayNameOr(string fallback)
        {
            return HasName ? DisplayName : fallback;
        }
    }
}
