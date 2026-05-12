using System.Linq;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Animates a ball prefab along a pre-computed Trajectory at configurable play-rate.
    /// The prefab's Rigidbody (if any) is made kinematic and Colliders are disabled —
    /// the trajectory is deterministic and pre-computed; PhysX must not interfere.
    /// </summary>
    public class BallAnimator : MonoBehaviour
    {
        [SerializeField] GameObject ballPrefab;

        public static BallAnimator Instance { get; private set; }

        public float PlayRate { get; set; } = 1f;  // 0.25, 1, 4, or Instant (float.MaxValue)

        public Transform CurrentBall => _instance == null ? null : _instance.transform;
        public bool IsPlaying => _playing;

        Trajectory  _trajectory;
        GameObject  _instance;
        float       _currentSimTime;
        bool        _playing;

        // §controls_i: ball visual rotation derived from position delta
        Vector3 _previousPos;
        const float BallRadiusMeters = 0.0215f;  // 43mm diameter golf ball / 2

        const float InstantRate = float.MaxValue;

        void Awake()
        {
            if (Instance == null) Instance = this;

            // Sweep persisted ghost clones (2026-05-12 fix).
            // PhysicsLabHolePicker calls PhysicsLabController.OnHoleLoaded in Edit Mode,
            // which chains through SetupAtTee → ballAnimator.PlaceAtRest → SpawnInstance,
            // and SpawnInstance does Instantiate(ballPrefab, transform). In Edit Mode this
            // makes the new clone a serialized child of BallAnimator — persisted to
            // LabScaffold.unity on next scene save. DestroyInstance only clears _instance;
            // every Edit-Mode invocation leaves a ghost child behind. Over many picker
            // actions, dozens of ghosts accumulate at whatever tee positions they were
            // spawned at, and they all render on PlayMode entry until a real shot or
            // PlaceAtRest spawns a 9th (correct) ball that obscures one of them.
            //
            // Sweep them here so PlayMode always starts with zero ball clones; the next
            // SetupAtTee in OnHoleLoaded spawns the single correct ball. Scoped to
            // children matching the ball prefab name so unrelated future children stay.
            string ballPrefabName = ballPrefab != null ? ballPrefab.name : null;
            var orphans = new System.Collections.Generic.List<GameObject>();
            foreach (Transform child in transform)
            {
                if (ballPrefabName != null && !child.name.StartsWith(ballPrefabName)) continue;
                if (ballPrefabName == null && !child.name.Contains("(Clone)")) continue;
                orphans.Add(child.gameObject);
            }
            for (int i = 0; i < orphans.Count; i++)
            {
#if UNITY_EDITOR
                DestroyImmediate(orphans[i]);
#else
                Destroy(orphans[i]);
#endif
            }
            if (orphans.Count > 0)
                Debug.Log($"[BallAnimator] Awake: swept {orphans.Count} persisted ghost ball clone(s).");
            _instance = null;
            _playing = false;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DestroyInstance(); // clean up spawned ball clone so it doesn't orphan on scene unload
        }

        // ── Public API ─────────────────────────────────────────────────────────

        public void SetBallPrefab(GameObject prefab) => ballPrefab = prefab;

        public void Play(Trajectory t)
        {
            if (t == null || t.samples == null || t.samples.Count == 0) return;
            _trajectory = t;

            DestroyInstance();
            SpawnInstance(t.samples[0].position);

            _currentSimTime = 0f;
            _playing = true;

            // Instant mode: snap straight to rest position
            if (PlayRate >= InstantRate * 0.5f)
            {
                SnapToEnd();
                return;
            }
        }

        public void Stop()
        {
            _playing = false;
        }

        // Place a resting ball at a world position without starting animation.
        // Called by PhysicsLabController.Start() so the ball is visible before the first shot.
        public void PlaceAtRest(Vector3 worldPos)
        {
            DestroyInstance();
            SpawnInstance(new fp3(
                fp.FromFloat(worldPos.x),
                fp.FromFloat(worldPos.y),
                fp.FromFloat(worldPos.z)));
        }

        // ── Unity loop ─────────────────────────────────────────────────────────

        void Update()
        {
            if (!_playing || _trajectory == null || _instance == null) return;

            var samples = _trajectory.samples;
            float endTime = samples[samples.Count - 1].time.ToFloat();

            _currentSimTime += Time.unscaledDeltaTime * PlayRate;

            if (_currentSimTime >= endTime)
            {
                SnapToEnd();
                return;
            }

            // Binary search for bracket
            int lo = 0, hi = samples.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) / 2;
                if (samples[mid].time.ToFloat() <= _currentSimTime) lo = mid;
                else hi = mid;
            }

            float tA = samples[lo].time.ToFloat();
            float tB = samples[hi].time.ToFloat();
            float frac = (tB > tA) ? (_currentSimTime - tA) / (tB - tA) : 0f;

            var posA = ToVec3(samples[lo].position);
            var posB = ToVec3(samples[hi].position);
            _instance.transform.position = Vector3.Lerp(posA, posB, frac);

            // §controls_i: derive rotation from position delta. ~2–3 µs/frame on mid-tier mobile.
            // Skip when delta is below ~0.1mm (prevents NaN from normalizing zero vector when ball is effectively stationary).
            Vector3 currentPos = _instance.transform.position;
            Vector3 delta = currentPos - _previousPos;
            float deltaMag = delta.magnitude;

            if (deltaMag > 0.0001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, delta / deltaMag);
                float axisMag = axis.magnitude;
                if (axisMag > 0.0001f)  // skip if delta is purely vertical (axis would be zero)
                {
                    axis /= axisMag;
                    float angleDegrees = (deltaMag / BallRadiusMeters) * Mathf.Rad2Deg;
                    _instance.transform.Rotate(axis, angleDegrees, Space.World);
                }
            }
            _previousPos = currentPos;
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        void SpawnInstance(fp3 startPos)
        {
            if (ballPrefab == null)
            {
                // Fallback: plain sphere — parent to this transform so it's cleaned up on destroy
                _instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _instance.transform.SetParent(transform);
                _instance.transform.localScale = Vector3.one * 0.043f; // 43mm diameter golf ball
                var col = _instance.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
            else
            {
                _instance = Instantiate(ballPrefab, transform); // parent to this transform

                // Disable physics — trajectory is pre-computed
                foreach (var rb in _instance.GetComponentsInChildren<Rigidbody>())
                    rb.isKinematic = true;
                foreach (var col in _instance.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                // Log prefab component inventory (architecture reference)
                var comps = _instance.GetComponentsInChildren<Component>();
                Debug.Log($"[BallAnimator] Ball prefab components: {string.Join(", ", comps.Select(c => c.GetType().Name))}");
            }

            _instance.transform.position = ToVec3(startPos);
            _instance.transform.rotation = Quaternion.identity;  // §controls_i: reset orientation per shot
            _previousPos = _instance.transform.position;          // §controls_i: seed rotation derivation

#if UNITY_EDITOR
            // Edit-Mode spawns (from PhysicsLabHolePicker → OnHoleLoaded → SetupAtTee chain)
            // must NOT be persisted to LabScaffold.unity. DontSaveInEditor prevents the
            // scene-dirty propagation; combined with the Awake sweep, this kills the ghost
            // clone accumulation entirely.
            if (!Application.isPlaying)
                _instance.hideFlags = HideFlags.DontSaveInEditor;
#endif
        }

        void DestroyInstance()
        {
            if (_instance != null)
            {
#if UNITY_EDITOR
                DestroyImmediate(_instance);
#else
                Destroy(_instance);
#endif
                _instance = null;
            }
            _playing = false;
        }

        void SnapToEnd()
        {
            var fp = _trajectory.finalPosition;
            if (_instance != null)
            {
                _instance.transform.position = ToVec3(fp);
                _previousPos = _instance.transform.position;  // §controls_i: keep delta-derivation invariant clean
            }
            _playing = false;
        }

        static Vector3 ToVec3(fp3 p)
            => new Vector3(p.x.ToFloat(), p.y.ToFloat(), p.z.ToFloat());

        // ── Internal test seams (§controls_i) ─────────────────────────────────

        // §controls_i: internal seam so EditMode tests can drive a single frame's rotation logic
        // without instantiating Unity's runtime Update loop. Mirrors private Update; do NOT call from production code.
        internal void DriveUpdateForTests()
        {
            if (_instance == null) return;
            // Reuse the same rotation-derivation block as Update; tests will set _instance.transform.position
            // BEFORE calling this so the delta is non-zero.
            Vector3 currentPos = _instance.transform.position;
            Vector3 delta = currentPos - _previousPos;
            float deltaMag = delta.magnitude;
            if (deltaMag > 0.0001f)
            {
                Vector3 axis = Vector3.Cross(Vector3.up, delta / deltaMag);
                float axisMag = axis.magnitude;
                if (axisMag > 0.0001f)
                {
                    axis /= axisMag;
                    float angleDegrees = (deltaMag / BallRadiusMeters) * Mathf.Rad2Deg;
                    _instance.transform.Rotate(axis, angleDegrees, Space.World);
                }
            }
            _previousPos = currentPos;
        }

        // §controls_i: internal seam to spawn a ball at a known position without a trajectory
        // (so tests can drive rotation without setting up a full Trajectory).
        internal void SpawnAtForTests(Vector3 worldPos)
            => SpawnInstance(new fp3(fp.FromFloat(worldPos.x), fp.FromFloat(worldPos.y), fp.FromFloat(worldPos.z)));

        internal Transform InstanceForTests => _instance == null ? null : _instance.transform;
    }
}
