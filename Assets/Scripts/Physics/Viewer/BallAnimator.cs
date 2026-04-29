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

        const float InstantRate = float.MaxValue;

        void Awake()   { if (Instance == null) Instance = this; }

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
                _instance.transform.position = ToVec3(fp);
            _playing = false;
        }

        static Vector3 ToVec3(fp3 p)
            => new Vector3(p.x.ToFloat(), p.y.ToFloat(), p.z.ToFloat());
    }
}
