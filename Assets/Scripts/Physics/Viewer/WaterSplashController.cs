using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Input;
using Golfin.Physics.Math;
using Golfin.Physics;

// Allow the EditMode test assembly to call internal members directly so tests
// can wire HandleStateChanged via sm.OnStateChanged without going through Configure.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Golfin.Physics.Tests")]

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Spawns a water splash VFX (and optional SFX) when the ball enters water.
    /// Lives on the same GameObject as BallAnimator; wired via PhysicsLabController.Awake().
    ///
    /// Trigger condition: BallStateChange.OBReason == OBReason.Water (NOT Surface == Water alone).
    /// Single pooled ParticleSystem instance — reused via Clear()+Play() per splash.
    /// Null-safe: unassigned prefab slot is a silent no-op (logs once).
    ///
    /// Mirror of BallTrailController's Configure/OnDestroy/idempotent-subscribe pattern.
    /// </summary>
    public class WaterSplashController : MonoBehaviour
    {
        // Resources path for the splash prefab. Loaded lazily when _splashPrefab is
        // unassigned so the controller can be wired purely in code (no scene-baked
        // SerializeField reference) → keeps LabScaffold.unity at zero diff.
        const string SplashPrefabResourcePath = "FX/WaterSplash";

        [Header("Splash VFX")]
        [Tooltip("Optional explicit prefab. If null, falls back to Resources.Load(\"FX/WaterSplash\").")]
        [SerializeField] ParticleSystem _splashPrefab;

        [Header("Audio (clip assigned in Order 350)")]
        [SerializeField] AudioClip _splashClip;

        [Header("Debug")]
        [SerializeField] bool _verboseLogging = false;

        // True once we've attempted the Resources.Load fallback (avoid repeated loads).
        bool _resourcesLoadAttempted;

        // ── Runtime refs ───────────────────────────────────────────────────────
        BallStateMachine _sm;

        // Pooled PS instance (lazy-created on first water OB).
        ParticleSystem _splashInstance;

        // Prevent log spam when prefab slot is empty.
        bool _nullPrefabLogged;

        // Number of times the Water-OB predicate passed and PlaySplash was invoked.
        // Used by EditMode tests to assert the production handler fired (independent of
        // whether a prefab is actually present), and by bot capture for diagnostics.
        int _waterOBFireCount;

        // Test seam: when true, PlaySplash skips the Resources.Load fallback so EditMode
        // tests stay prefab-less (no ParticleSystem leaked into the editor scene).
        bool _suppressResourcesLoad;

        // ── Public config API ──────────────────────────────────────────────────

        /// <summary>
        /// Called by PhysicsLabController.Awake() after creating the BallStateMachine.
        /// Idempotent: safe to call on re-wire after domain reload.
        /// </summary>
        public void Configure(BallAnimator anim, BallStateMachine sm, ShotController shot)
        {
            // anim and shot unused by this controller but kept for signature parity.
            _ = anim;
            _ = shot;

            // Idempotent re-wire: unsubscribe first to avoid double-subscribe.
            if (_sm != null) _sm.OnStateChanged -= HandleStateChanged;
            _sm = sm;
            if (_sm != null) _sm.OnStateChanged += HandleStateChanged;
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnStateChanged -= HandleStateChanged;
        }

        // ── State handler ──────────────────────────────────────────────────────
        // Internal so EditMode tests can subscribe directly without going through Configure.
        // Production code always goes through Configure.
        internal void HandleStateChanged(BallStateChange change)
        {
            // Only trigger on the Water OB transition.
            if (change.Next != BallState.OB) return;
            if (!change.OBReason.HasValue)    return;
            if (change.OBReason.Value != OBReason.Water) return;

            _waterOBFireCount++;

            Vector3 worldPos = new Vector3(
                change.Position.x.ToFloat(),
                change.Position.y.ToFloat(),
                change.Position.z.ToFloat());

            if (_verboseLogging)
                Debug.Log($"[WaterSplash] HandleStateChanged: firing splash at worldPos={worldPos:F2}");
            PlaySplash(worldPos);
        }

        // ── Splash playback ────────────────────────────────────────────────────

        void PlaySplash(Vector3 worldPos)
        {
            // Lazy Resources fallback: if no prefab was assigned via the SerializeField,
            // try to load it from Resources once. This lets PhysicsLabController wire the
            // controller entirely in code (AddComponent + Configure) without a scene-baked
            // reference, keeping LabScaffold.unity at zero diff.
            if (_splashPrefab == null && !_resourcesLoadAttempted && !_suppressResourcesLoad)
            {
                _resourcesLoadAttempted = true;
                _splashPrefab = Resources.Load<ParticleSystem>(SplashPrefabResourcePath);
                if (_splashPrefab != null && _verboseLogging)
                    Debug.Log($"[WaterSplash] Loaded splash prefab from Resources/{SplashPrefabResourcePath}.");
            }

            if (_splashPrefab == null)
            {
                if (!_nullPrefabLogged)
                {
                    Debug.Log("[WaterSplash] Prefab slot is empty and Resources/" + SplashPrefabResourcePath +
                              " was not found — no splash will play.");
                    _nullPrefabLogged = true;
                }
                return;
            }

            // Lazy-create the pooled instance on first use.
            if (_splashInstance == null)
            {
                _splashInstance = Instantiate(_splashPrefab, worldPos, Quaternion.identity);
                _splashInstance.gameObject.name = "[WaterSplash_Pool]";
                // Don't parent to this GO — keeps world transform clean.
            }
            else
            {
                // Reuse: teleport to new position.
                _splashInstance.transform.position = worldPos;
            }

            // Always Clear()+Play() — including the first splash — so playback does NOT
            // depend on the prefab's Play-On-Awake setting. Children PS play with the root
            // (withChildren defaults to true).
            _splashInstance.Clear();
            _splashInstance.Play();

            // Audio hook (clip supplied by Order 350; null-safe).
            // AudioSource.PlayClipAtPoint mirrors AudioManager.PlaySFXAtPosition but avoids
            // a cross-assembly reference (AudioManager lives in Assembly-CSharp, not in
            // Golfin.Physics.Viewer). Volume defaults to 1f; Order 350 can tune via the clip.
            if (_splashClip != null)
                AudioSource.PlayClipAtPoint(_splashClip, worldPos);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor/test seam: fire the splash handler with a synthetic Water OB change.
        /// Used by WaterSplashTriggerTest and bot capture runners.
        /// </summary>
        public void FireWaterSplashForTest(fp3 position)
        {
            var change = new BallStateChange(
                BallState.Flying,
                BallState.OB,
                position,
                SurfaceType.Water,
                OBReason.Water,
                fp.Zero);
            HandleStateChanged(change);
        }

        /// <summary>
        /// Editor/test seam: whether a splash instance has been created (lazy-init happened).
        /// </summary>
        public bool SplashInstanceExists => _splashInstance != null;

        /// <summary>
        /// Editor/test seam: whether the null-prefab warning was logged (fired == HandleStateChanged called).
        /// When _splashPrefab is null AND Resources load is suppressed, this is set to true on the
        /// first call to HandleStateChanged. Tests without a prefab use this to verify the handler ran.
        /// </summary>
        public bool NullPrefabWarningLogged => _nullPrefabLogged;

        /// <summary>
        /// Editor/test seam: how many times the Water-OB predicate passed and PlaySplash was invoked.
        /// Independent of whether a prefab is present — the canonical "did the handler fire" signal.
        /// </summary>
        public int WaterOBFireCount => _waterOBFireCount;

        /// <summary>
        /// Editor/test seam: disable the Resources.Load fallback so EditMode tests stay prefab-less
        /// (no ParticleSystem leaked into the editor scene). Call BEFORE driving any state change.
        /// </summary>
        public void SuppressResourcesLoadForTests() => _suppressResourcesLoad = true;
#endif
    }
}
