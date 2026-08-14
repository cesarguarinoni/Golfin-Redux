// ─────────────────────────────────────────────────────────────────────────────
// rp_balance_sync — the inbound half of the Slice-2 cutover.
//
// Slice 2 taught the game to WRITE to the ledger (earns enqueue, spends debit
// server-first) and never to read it back, so the nav-bar counter showed a stale
// local number forever and admin grants were invisible in game. This is the wire
// that closes the loop: server balance → RewardPointsManager → OnPointsChanged →
// every RP display that already exists.
//
// Lives in Assembly-CSharp (not the headless Golfin.Economy asmdef) because it
// touches RewardPointsManager, AuthService and ScreenManager; same split as
// PointsSpendGate next door. The RULES live in Golfin.Economy.ServerBalanceSync
// so they are testable without a scene.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Roster;
using GolfinRedux.UI;
using UnityEngine;

namespace Golfin.EconomyRuntime
{
    /// <summary>
    /// Keeps the RP the player sees equal to the RP the server holds.
    ///
    /// INERT WITH THE FLAG OFF. <see cref="Bootstrap"/> does not even create the GameObject when
    /// <c>PointsBackendEnabled</c> is off, so the local-only development loop is byte-identical to
    /// what it was before this feature: no subscriptions, no HTTP, no behaviour.
    ///
    /// REFRESH MOMENTS (rp_balance_sync §3.3) — event-driven, never polled:
    ///   • sign-in succeeds     → the first balance of the session;
    ///   • app returns to the foreground → catches grants made while backgrounded (the dashboard workflow);
    ///   • entering Home        → the screen the counter is most looked at on, throttled;
    ///   • earn / spend replies → already fold into the cache inside <see cref="PointsService"/>.
    /// </summary>
    public sealed class ServerBalanceSyncBehaviour : MonoBehaviour, IServerBalanceSink
    {
        /// <summary>Floor between Home-entry refreshes. Bouncing Home↔Roster is a normal thing for a
        /// player to do and must not turn into a request per tap. Sign-in and foreground are NOT
        /// throttled — those are the moments the balance is most likely to have actually moved.</summary>
        private const float HomeRefreshCooldownSeconds = 10f;

        private static ServerBalanceSyncBehaviour? _instance;

        private float _lastHomeRefresh = float.NegativeInfinity;
        private Coroutine? _deferredApply;
        private int _deferredTotal;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!PointsBackendFlag.Enabled) return;   // flag OFF: this type never runs
            if (_instance != null) return;

            var go = new GameObject("[ServerBalanceSync]");
            _instance = go.AddComponent<ServerBalanceSyncBehaviour>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            ServerBalanceSync.Bind(PointsService.Instance, this);
            AuthService.SignedIn += OnSignedIn;
            ScreenManager.ScreenChanged += OnScreenChanged;
        }

        private void OnDisable()
        {
            AuthService.SignedIn -= OnSignedIn;
            ScreenManager.ScreenChanged -= OnScreenChanged;
            ServerBalanceSync.Unbind();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Start()
        {
            // A returning player is already signed in from the saved session, so no SignedIn event will
            // ever fire for them — without this their counter would stay stale until they hit Home.
            if (AuthService.Instance.Session.IsAuthenticated)
                Refresh("startup");
        }

        // ── refresh triggers (§3.3) ───────────────────────────────────────────────

        private void OnSignedIn(AuthSession session) => Refresh("sign-in");

        /// <summary>Foreground, not background: <c>paused == false</c> is the app coming BACK, which is
        /// exactly when an admin grant made in the dashboard needs to appear without a restart.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (!paused) Refresh("resume");
        }

        private void OnScreenChanged(ScreenId screen)
        {
            if (screen != ScreenId.Home) return;

            if (Time.unscaledTime - _lastHomeRefresh < HomeRefreshCooldownSeconds) return;
            _lastHomeRefresh = Time.unscaledTime;
            Refresh("home");
        }

        /// <summary>
        /// Ask the server for the balance. Fire-and-forget: a failure logs and changes nothing, leaving
        /// the cached value on screen (§3.5 — "unknown" must never render as 0).
        /// <see cref="PointsService.RefreshBalanceRoutine"/> short-circuits with the flag off, so this
        /// is safe even if the flag is flipped mid-session.
        /// </summary>
        private void Refresh(string why)
        {
            if (!PointsBackendFlag.Enabled) return;
            if (!AuthService.Instance.Session.IsAuthenticated)
            {
                Debug.Log($"[ServerBalanceSync] Skipping {why} refresh — not signed in.");
                return;
            }

            PointsService.Instance.RefreshBalanceAsync(result =>
            {
                if (result == null || !result.Success)
                    Debug.LogWarning($"[ServerBalanceSync] {why} refresh failed: " +
                                     $"{(result != null ? result.ToString() : "no result")} — keeping the cached balance.");
            });
        }

        // ── IServerBalanceSink ────────────────────────────────────────────────────

        /// <summary>
        /// Hand the server's number to the manager every RP display already listens to.
        /// <paramref name="total"/> is the DISPLAYED total (server + queued earns) — see
        /// <see cref="PointsService.DisplayBalance"/>.
        /// </summary>
        public void ApplyServerBalance(int total)
        {
            if (RewardPointsManager.Instance == null)
            {
                // A balance can land before the roster managers finish booting. Holding it is right;
                // dropping it would leave the stale number up until the next refresh moment.
                _deferredTotal = total;
                if (_deferredApply == null && isActiveAndEnabled)
                    _deferredApply = StartCoroutine(ApplyWhenManagerExists());
                return;
            }

            RewardPointsManager.Instance.ApplyServerBalance(total);
        }

        private IEnumerator ApplyWhenManagerExists()
        {
            while (RewardPointsManager.Instance == null)
                yield return null;

            _deferredApply = null;
            RewardPointsManager.Instance.ApplyServerBalance(_deferredTotal);
            Debug.Log($"[ServerBalanceSync] Applied deferred balance {_deferredTotal}R once RewardPointsManager existed.");
        }
    }
}
