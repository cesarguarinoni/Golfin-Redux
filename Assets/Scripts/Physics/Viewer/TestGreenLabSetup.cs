using UnityEngine;
using Golfin.Physics.Runtime.Baked;
using Golfin.Gameplay.UI.HUD;

// Keep in Golfin.Physics.Viewer (same asmdef as PutterGreenReader — needed for
// BakedZoneClassifier direct access without routing through PhysicsLabController).

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Standalone scene setup for PhysicsLab_TestGreen.
    ///
    /// Loads TestGreen zone data from Resources and triggers PutterGreenReader's
    /// bake step directly (bypassing the PhysicsLabController → HoleContext flow
    /// that normal PhysicsLab_Hole1 uses, since TestGreen has no separate Geo scene).
    ///
    /// Flow:
    ///   1. Start() loads HoleData/_test/TestGreen/zones.json via Resources.Load.
    ///   2. Creates a BakedZoneClassifier from that ZoneData.
    ///   3. Fires HoleContext.HoleNumber = 999 + HoleContext.Raise() so that
    ///      PutterGreenReader.OnHoleContextChanged triggers its bake via the
    ///      standard event path.
    ///   4. Also calls _greenReader.BakeCells(_classifier) directly as a fallback.
    ///
    /// SPEC: puttpath_predictor_perf_and_design §Architecture §§ Test green.
    /// </summary>
    [RequireComponent(typeof(PutterGreenReader))]
    public class TestGreenLabSetup : MonoBehaviour
    {
        // Synthetic hole number (high value avoids collision with real holes 1-18).
        private const int TestGreenHoleNumber = 999;

        [SerializeField] private PutterGreenReader _greenReader;

        private BakedZoneClassifier _classifier;

        private void Awake()
        {
            if (_greenReader == null)
                _greenReader = GetComponent<PutterGreenReader>();
        }

        private void Start()
        {
            LoadAndBake();
        }

        /// <summary>
        /// Loads the TestGreen zone data and triggers the bake on the PutterGreenReader.
        /// Can also be called from Editor scripts for scene setup.
        /// </summary>
        public void LoadAndBake()
        {
            // Load zones.json for TestGreen (non-course fixture lives under _test/).
            var zonesAsset = Resources.Load<TextAsset>("HoleData/_test/TestGreen/zones");
            if (zonesAsset == null)
            {
                Debug.LogError("[TestGreenLabSetup] Resources/HoleData/_test/TestGreen/zones not found. " +
                               "Run TestGreenSceneBuilder or ensure the zones.json is in Resources/_test/TestGreen.");
                return;
            }

            ZoneData zoneData = ZoneData.FromJson(zonesAsset.text);
            if (zoneData == null || zoneData.zones == null || zoneData.zones.Count == 0)
            {
                Debug.LogError("[TestGreenLabSetup] ZoneData.FromJson returned empty or null data.");
                return;
            }

            _classifier = new BakedZoneClassifier(zoneData);
            Debug.Log($"[TestGreenLabSetup] BakedZoneClassifier created from TestGreen zones.json.");

            // Fire HoleContext so PutterGreenReader.OnHoleContextChanged triggers.
            // This also exercises the standard event path.
            HoleContext.HoleNumber = TestGreenHoleNumber;
            HoleContext.Raise();

            // Also call BakeCells directly in case HoleNumber hadn't changed.
            if (_greenReader != null)
            {
                _greenReader.BakeCells(_classifier);
                Debug.Log($"[TestGreenLabSetup] PutterGreenReader.BakeCells complete: " +
                          $"baked={_greenReader.BakedCellCount} cells, " +
                          $"meshVerts={_greenReader.MeshVertexCount} vertices.");
            }
            else
            {
                Debug.LogWarning("[TestGreenLabSetup] _greenReader is null — cannot trigger bake.");
            }
        }

        /// <summary>
        /// Returns the classifier for external use (e.g., PhysicsLabController override).
        /// </summary>
        public BakedZoneClassifier Classifier => _classifier;
    }
}
