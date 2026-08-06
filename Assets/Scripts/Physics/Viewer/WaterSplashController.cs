using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.Input;
using Golfin.Physics.Math;
using Golfin.Physics;
using Golfin.Audio.Events;

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

        [Header("Ball sink (water_entry_presentation)")]
        [Tooltip("Metres the ball drops below the contact point before it is hidden.")]
        [SerializeField] float _sinkDepth = 0.6f;
        [Tooltip("Seconds the sink takes. Kept under the splash lifetime (~0.8s).")]
        [SerializeField] float _sinkDuration = 0.5f;

        [Header("Debug")]
        [SerializeField] bool _verboseLogging = false;

        // True once we've attempted the Resources.Load fallback (avoid repeated loads).
        bool _resourcesLoadAttempted;

        // ── Runtime refs ───────────────────────────────────────────────────────
        BallStateMachine _sm;

        // water_entry_presentation: source of the live ball transform for the sink.
        BallAnimator _anim;

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
            // shot unused by this controller but kept for signature parity.
            _ = shot;
            // water_entry_presentation: the animator owns the ball transform we sink.
            _anim = anim;

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

            // water_entry_presentation: drop the ball through the surface rather than
            // leaving it sitting on top for the whole OB hold.
            var ball = _anim != null ? _anim.CurrentBall : null;
            if (ball != null && isActiveAndEnabled)
                StartCoroutine(SinkBall(ball));
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
                ForceDrawOverWater(_splashInstance);
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

            // Order 350: publish via SfxBus instead of PlayClipAtPoint.
            // This fixes the latent bug where the SFX volume slider had no effect on splash audio.
            // _splashClip field is preserved for backward-compat inspector wiring but no longer used.
            SfxBus.Play(SfxId.LandWater);
        }

        // ── water_entry_presentation: draw order ───────────────────────────────

        /// <summary>
        /// Push every splash renderer past the water surface in the transparent queue.
        ///
        /// The splash spawns exactly ON the water plane (measured: splash y 7.27, Water_1
        /// y 7.27) and its three materials sit at renderQueue 3000 — the same queue as
        /// URPWater/Standard — so the water sorted over it and the splash was invisible.
        /// The .mat assets DO carry m_CustomRenderQueue: 3100, but Unity resets them to
        /// 3000 on every load (the recurring M_Splash*.mat churn), so the authored value
        /// never takes effect. Setting it here on the MATERIAL INSTANCE (renderer.material,
        /// not sharedMaterial) makes it stick for the session and leaves the shared assets
        /// untouched — they are under a standing no-edit rule.
        /// </summary>
        static void ForceDrawOverWater(ParticleSystem instance)
        {
            const int AboveWaterQueue = 3100;   // URPWater/Standard renders at 3000

            var renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var mat = renderers[i].material;   // instance, not the shared asset
                if (mat != null) mat.renderQueue = AboveWaterQueue;
            }
        }

        // ── water_entry_presentation: ball sink ────────────────────────────────

        /// <summary>
        /// Sink the ball through the surface and hide it, so a water landing reads as the
        /// ball going IN rather than resting on top of the water (Cesar 2026-08-06).
        /// BallAnimator.PlaceAtRest destroys and respawns the instance for the next shot,
        /// so deactivating this one is safe.
        /// </summary>
        System.Collections.IEnumerator SinkBall(Transform ball)
        {
            Vector3 start = ball.position;
            Vector3 end   = start + Vector3.down * _sinkDepth;

            float t = 0f;
            while (t < _sinkDuration)
            {
                if (ball == null) yield break;   // re-armed mid-sink
                t += Time.deltaTime;
                ball.position = Vector3.Lerp(start, end, Mathf.Clamp01(t / _sinkDuration));
                yield return null;
            }

            if (ball != null) ball.gameObject.SetActive(false);
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
