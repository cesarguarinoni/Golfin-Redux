// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the MonoBehaviour host: clock, save hook, pause/quit flush.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §3
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using Golfin.Auth;
using Golfin.Save;
using UnityEngine;

namespace Golfin.InventorySync
{
    /// <summary>
    /// Everything <see cref="InventorySyncService"/> needs from Unity, and nothing more: a frame
    /// clock, the <c>SaveDataHost.OnSaved</c> subscription, and the pause/quit signals.
    ///
    /// <para>
    /// SELF-BOOTSTRAPPING, like <c>NetCoroutineRunner</c> and <c>GolfinCharacterSync</c>. There is no
    /// natural owner for this on any screen, it must survive every scene load, and a scene/prefab
    /// edit for a component with no inspector state would be a merge conflict waiting to happen.
    /// </para>
    ///
    /// <para>
    /// THE SUBSCRIPTION IS <c>SaveDataHost.OnSaved</c>, NOT PER-MANAGER EVENTS. OnSaved fires after
    /// every successful disk write, which is by definition every change that made it into the save —
    /// so it cannot miss a mutation the way a hand-maintained list of manager events would, and the
    /// 30 s coalescing window is what keeps the burst of them from becoming a burst of requests.
    /// </para>
    /// </summary>
    public sealed class InventorySyncBehaviour : MonoBehaviour
    {
        private const string Tag = "[InventorySync]";

        private static InventorySyncBehaviour? _instance;

        private SaveDataHost? _subscribedTo;
        private bool _booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;

            var go = new GameObject("[InventorySync]");
            _instance = go.AddComponent<InventorySyncBehaviour>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // Force the singleton to be built HERE, on the main thread. OnSaved fires on a
            // thread-pool thread (SaveDataHost.FlushNow awaits with ConfigureAwait(false)), and if
            // that were ever the first touch of `Instance`, the lazy init would race Update()'s.
            // Update() would win in practice on frame 1 — this makes it not a matter of practice.
            _ = InventorySyncService.Instance;
        }

        private void OnEnable()
        {
            AuthService.SignedIn += OnSignedIn;
            StartCoroutine(BindWhenReady());
        }

        private void OnDisable()
        {
            AuthService.SignedIn -= OnSignedIn;
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // ── Binding ───────────────────────────────────────────────────────────

        /// <summary>
        /// <c>SaveDataHost</c> is a scene singleton that may not exist yet at
        /// <c>AfterSceneLoad</c>, so the subscription is polled into place rather than assumed — the
        /// same shape as <c>GolfinCharacterSync.SubscribeToRosterWhenReady</c>. It waits for
        /// <c>IsLoaded</c>, not merely for <c>Instance</c>: Instance is assigned before
        /// <c>LoadData()</c>, and syncing against a save that has not been read yet would push the
        /// field initialiser over the player's real inventory.
        /// </summary>
        private IEnumerator BindWhenReady()
        {
            while (_subscribedTo == null)
            {
                SaveDataHost host = SaveDataHost.Instance;
                if (host != null && host.IsLoaded)
                {
                    host.OnSaved += OnSaved;
                    _subscribedTo = host;
                    TryBoot();
                    yield break;
                }
                yield return null;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedTo == null) return;
            _subscribedTo.OnSaved -= OnSaved;
            _subscribedTo = null;
        }

        /// <summary>
        /// A sign-in mid-session is the OTHER way the boot read becomes possible: a tester who
        /// launches signed out reaches <see cref="BindWhenReady"/> with no token, so
        /// <c>Boot()</c> no-ops and leaves <c>BootCompleted</c> false. This is what runs it for real
        /// once the token exists.
        /// </summary>
        private void OnSignedIn(AuthSession session) => TryBoot();

        /// <summary>
        /// Re-run the boot read after a FAILED one (starter_restore_gate §1).
        ///
        /// <para>
        /// Static because the caller is <c>StarterGate</c>, which is a static helper on the far side
        /// of the assembly line and has no handle on this component. A no-op when the behaviour has
        /// not bootstrapped yet — in that case <see cref="BindWhenReady"/> is about to boot anyway.
        /// </para>
        /// </summary>
        public static void RetryBoot() => _instance?.TryBoot();

        private void TryBoot()
        {
            var service = InventorySyncService.Instance;
            if (_subscribedTo == null) return;

            // A request is already out — every caller here (bind, sign-in, the starter-gate retry)
            // can fire in the same frame, and a second GET would answer the same question twice.
            if (service.BootInFlight) return;

            // A FAILED boot is re-runnable: that IS the retry the starter gate offers, and it is
            // also what makes a second sign-in after an offline first one restore properly. A
            // SUCCEEDED one is not — the answer is already in the save.
            if (_booted && service.BootCompleted &&
                service.LastBootOutcome != BootOutcome.Failed) return;

            _booted = true;
            service.Boot();
        }

        // ── Triggers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Fired after every successful disk write.
        ///
        /// <para>
        /// ⚠️ THIS RUNS ON A THREAD-POOL THREAD. <c>SaveDataHost.FlushNow</c> awaits with
        /// <c>ConfigureAwait(false)</c> — required, or the pause-time sync-over-async deadlocks — so
        /// its continuation, and therefore this handler, does NOT come back to the main thread. Only
        /// <c>WriteBehind.MarkDirty()</c> is called here: it sets one bool. Nothing that touches a
        /// Unity object, a coroutine, or <c>Time</c> may be added to this method. The actual push
        /// happens in <see cref="Update"/>, on the main thread, where all of that is legal.
        /// </para>
        /// </summary>
        private void OnSaved() => InventorySyncService.Instance.MarkDirty();

        private void Update()
        {
            InventorySyncService.Instance.Tick(Time.realtimeSinceStartup);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush("pause");
        }

        private void OnApplicationQuit() => Flush("quit");

        /// <summary>
        /// The one flush that bypasses the 30 s window (SPEC §3).
        ///
        /// <para>
        /// FIRE-AND-FORGET, unlike <c>SaveDataHost</c>'s pause flush, which blocks so the bytes are
        /// on disk before the OS can kill the process. A network round trip cannot be made to finish
        /// inside a pause callback on either platform, and blocking on one would be an ANR on
        /// Android and a watchdog kill on iOS. If the request does not survive the backgrounding,
        /// nothing is lost: the save on disk is authoritative and the next launch pushes it.
        /// </para>
        /// </summary>
        private void Flush(string why)
        {
            var service = InventorySyncService.Instance;
            if (!service.WriteBehind.IsDirty) return;
            Debug.Log($"{Tag} Flushing on {why}.");
            service.FlushNow(Time.realtimeSinceStartup);
        }
    }
}
