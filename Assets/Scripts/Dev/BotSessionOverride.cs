// ─────────────────────────────────────────────────────────────────────────────
// points_cutover_followups item 1 — dev-only bot auth bypass.
//
// WHOLE-FILE GUARD. This type does not exist in a player build: the guard below is
// the same one BotDriver and every bot host under Assets/Scripts/Physics/Viewer/Bot/
// already carry. Every reference to it elsewhere carries the identical guard, so the
// player build compiles with those branches simply absent (see the iOS lesson about
// #if UNITY_EDITOR seams in runtime assemblies — the seam is safe only when the
// CALLERS are guarded too, which is why AuthGate/SplashScreenController repeat it).
// ─────────────────────────────────────────────────────────────────────────────
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
using System;
using Golfin.Auth;
using Golfin.Economy;
using UnityEngine;

namespace Golfin.Dev
{
    /// <summary>
    /// Lets an automated bot run past the hard sign-in gate without credentials.
    ///
    /// THE TWO HALVES ARE ONE DECISION. A bot that is "signed in" must never be signed in
    /// to anything real, so activating the override does both of these together:
    ///   1. installs a FAKE LOCAL identity in <see cref="AuthService"/> — enough for
    ///      <c>Session.IsAuthenticated</c> and the <c>HasDisplayName</c> branch, backed by
    ///      no server and no credentials; and
    ///   2. forces <see cref="PointsBackendFlag"/> OFF for the run, so the bot plays the
    ///      deterministic offline economy: no HTTP, no queued ops, no writes to the live
    ///      PLAYLIFE ledger under a fake token.
    /// Half of this would be worse than neither — a fake session with the backend still ON
    /// would aim real spend/earn calls at the production ledger with a token the server
    /// will reject, which is exactly the failure this exists to prevent.
    ///
    /// NOTHING IS PERSISTED. The fake session is never <c>Save()</c>d, and the flag is
    /// forced through <see cref="PointsBackendFlag.SessionForcedOff"/> — a non-persisting
    /// switch — precisely so a bot run cannot leave Cesar's Editor signed in as a bot or
    /// with the points backend silently disabled the next morning.
    ///
    /// NOT-PERSISTED IS NOT ENOUGH, THOUGH. This project runs with domain reload DISABLED, so
    /// statics survive both leaving AND re-entering play mode. Measured after the first harness
    /// run: <c>SessionForcedOff</c> was still true back in the Editor — i.e.
    /// <c>PointsBackendFlag.Enabled</c> reported false against a compiled default of true, the
    /// exact silent-disable this was built to prevent — and the same leak would have carried a
    /// fake "Bot" session into the next NORMAL play session. The <c>EditorHooks</c> block below
    /// disarms on every play-mode edge; it is the load-bearing half of the guarantee, not tidy-up.
    ///
    /// ARMING, two ways:
    ///   • <see cref="Arm"/> — explicit, for any harness that can call it. New harnesses
    ///     should use this. <c>TournamentLoopCaptureHarness</c> does.
    ///   • auto-detect — a live bot host from the <c>Golfin.Physics.Viewer.Bot</c> namespace
    ///     counts as armed. Those hosts (LoopV2SmokeBot, ObBoundaryCaptureBot, …) all live
    ///     under <c>Assets/Scripts/Physics/</c>, which is a standing ZERO-EDIT zone, so they
    ///     cannot be taught to call <see cref="Arm"/>. They are all whole-file
    ///     <c>#if UNITY_EDITOR</c> types, so their mere existence is a sound editor-only
    ///     signal — the contract this detection relies on.
    /// </summary>
    public static class BotSessionOverride
    {
        /// <summary>Namespace whose live MonoBehaviours are treated as an armed bot run.</summary>
        private const string BotHostNamespace = "Golfin.Physics.Viewer.Bot";

        private const string FakeUserId      = "bot-local-00000000-0000-0000-0000-000000000000";
        private const string FakeEmail       = "bot@local.golfin";
        private const string FakeDisplayName = "Bot";

        /// <summary>Deliberately obvious: if this ever reaches a server, the log says why.</summary>
        private const string FakeAccessToken = "BOT_SESSION_OVERRIDE_NOT_A_REAL_TOKEN";

        private static bool _armed;
        private static bool _applied;

        /// <summary>Cached auto-detect answer — the scan is a FindObjectsByType, not free, and the
        /// gate is consulted on every screen transition.</summary>
        private static bool _autoDetected;
        private static int  _autoDetectFrame = -1;

        /// <summary>
        /// True while a bot run owns this session. Consulted by the auth gate and the splash gate.
        /// Reading it APPLIES the override (idempotently) so no caller has to remember to.
        /// </summary>
        public static bool Active
        {
            get
            {
                if (!_armed && !AutoDetectBotHost()) return false;
                Apply();
                return true;
            }
        }

        /// <summary>
        /// Explicitly arm the override for this run. Call from a harness before the bot reaches
        /// the splash gate — <c>EnteredPlayMode</c> is the right moment.
        /// </summary>
        public static void Arm(string reason)
        {
            if (_armed) return;
            _armed = true;
            Debug.LogWarning($"[BotSessionOverride] ARMED ({reason}) — fake local session, " +
                             "points backend forced OFF for this run. Editor/harness builds only.");
            Apply();
        }

        /// <summary>
        /// Drop the override: restores the real session and hands the points flag back to its
        /// stored/compiled value. Called automatically on every play-mode edge by
        /// <c>EditorHooks</c> — do NOT assume a domain reload will do it, because this project has
        /// domain reload disabled and these statics outlive a play session in both directions.
        /// Public so a harness can also reset mid-session.
        /// </summary>
        public static void Disarm()
        {
            if (!_armed && !_applied) return;

            if (_applied)
            {
                // Only reach into the runtime session while there IS one. In edit mode
                // AuthService.Instance would self-bootstrap a GameObject into the open scene and
                // dirty it (this runs on the play-mode-exit edge, so edit mode is the normal case).
                if (Application.isPlaying)
                {
                    var session = AuthService.Instance.Session;
                    if (session.UserId == FakeUserId)
                    {
                        // Deliberately NOT session.Clear(): that deletes the PlayerPrefs entry, and
                        // the entry on disk is Cesar's REAL session — Apply() only ever overwrote the
                        // in-memory copy. Clearing here would sign him out for real at the end of a
                        // bot run. Wipe the fake fields, then re-Load whatever was genuinely stored
                        // (a no-op that leaves the fields blank when nothing was).
                        session.AccessToken = session.RefreshToken = null;
                        session.UserId = session.Email = session.DisplayName = null;
                        session.ExpiresAtUnix = 0;
                        session.EmailConfirmed = false;
                        session.Load();
                    }
                }

                PointsBackendFlag.SessionForcedOff = false;
            }

            _armed = _applied = false;
            _autoDetectFrame = -1;
            Debug.Log("[BotSessionOverride] Disarmed — fake session cleared, points flag restored.");
        }

        /// <summary>Install the fake session + force the flag off. Idempotent.</summary>
        private static void Apply()
        {
            if (_applied) return;
            _applied = true;

            // Forced OFF before anything else: from here on PointsService short-circuits, so even
            // if a spend fires during the same frame it cannot reach the network.
            PointsBackendFlag.SessionForcedOff = true;

            var session = AuthService.Instance.Session;
            session.AccessToken  = FakeAccessToken;
            session.RefreshToken = null;   // nothing to refresh — the splash gate must not try
            session.ExpiresAtUnix = 0;
            session.UserId       = FakeUserId;
            session.Email        = FakeEmail;
            session.DisplayName  = FakeDisplayName;
            session.EmailConfirmed = true;
            // NOT session.Save() — see the class note: this must never outlive the run.

            Debug.LogWarning("[BotSessionOverride] Applied: fake local session " +
                             $"('{FakeDisplayName}'), PointsBackendEnabled forced OFF (not persisted).");
        }

        /// <summary>
        /// Is a bot host alive right now? Cached per frame. Matches on the declaring namespace
        /// rather than a name pattern so it cannot be tripped by an unrelated MonoBehaviour that
        /// happens to be called something-Bot.
        /// </summary>
        private static bool AutoDetectBotHost()
        {
            if (_autoDetectFrame == Time.frameCount) return _autoDetected;
            _autoDetectFrame = Time.frameCount;
            _autoDetected = false;

            var hosts = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (var host in hosts)
            {
                if (host == null) continue;
                Type t = host.GetType();
                if (t.Namespace != null && t.Namespace.StartsWith(BotHostNamespace, StringComparison.Ordinal))
                {
                    _autoDetected = true;
                    if (!_armed)
                        Debug.LogWarning($"[BotSessionOverride] Auto-armed — live bot host '{t.FullName}'.");
                    break;
                }
            }

            return _autoDetected;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Disarm on every play-mode edge. REQUIRED, not housekeeping: with domain reload disabled
        /// these statics outlive a play session in both directions, so without this an armed bot run
        /// leaves the Editor with a fake session and the points backend forced off — and the next
        /// ordinary Play would inherit both.
        ///
        /// Entering is reset too (not just exiting) so a harness that armed and then crashed, or an
        /// Editor closed mid-run, cannot poison the following session.
        /// </summary>
        [UnityEditor.InitializeOnLoad]
        private static class EditorHooks
        {
            static EditorHooks()
            {
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeChanged;
            }

            private static void OnPlayModeChanged(UnityEditor.PlayModeStateChange state)
            {
                // ExitingEditMode fires BEFORE a harness's EnteredPlayMode Arm(), so resetting here
                // clears stale state without racing the arm that is about to happen.
                if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                    state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                {
                    Disarm();
                }
            }
        }
#endif
    }
}
#endif
