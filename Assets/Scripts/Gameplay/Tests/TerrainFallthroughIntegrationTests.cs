using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Phase 4 B-group integration tests (tests 7–11).
    /// Validate that BallSimulation.Simulate end-to-end keeps trajectories on
    /// the correct surface by routing ground Y through SceneGroundProvider's
    /// 3-arg surface-aware SampleHeight.
    ///
    /// Tests use synthetic BoxCollider geometry (same approach as Phase 3
    /// A-group) so they are deterministic and don't depend on hole scene content.
    /// Test 11 smoke-tests the real Hole_01_Geo scene for basic sanity.
    /// </summary>
    public class TerrainFallthroughIntegrationTests
    {
        const float Epsilon = 0.005f;

        readonly List<GameObject> _created = new List<GameObject>();

        GameObject Track(GameObject go) { _created.Add(go); return go; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        // Flat BoxCollider whose top face is at worldY.
        GameObject CreateFlat(float x, float worldY, float z, SurfaceType type, float size = 10f)
        {
            var go = Track(new GameObject($"Flat_{type}"));
            go.transform.position = new Vector3(x, worldY - 0.01f, z);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size, 0.02f, size);
            var marker = go.AddComponent<SurfaceMarker>();
            marker.Type = type;
            return go;
        }

        // Runs Simulate and returns the min Y-gap observed for samples classified as `surface`.
        // gap = ballY - groundY(surface). Negative values are fall-throughs.
        float MinYGap(Trajectory traj, SurfaceType surface, SceneGroundProvider ground, SceneSurfaceProvider surfaces)
        {
            float minGap = float.MaxValue;
            foreach (var s in traj.samples)
            {
                fp bx = s.position.x, bz = s.position.z;
                if (surfaces.Classify(bx, bz) != surface) continue;
                float groundY = ground.SampleHeight(bx, bz, surface).ToFloat();
                float gap = s.position.y.ToFloat() - groundY;
                if (gap < minGap) minGap = gap;
            }
            return minGap == float.MaxValue ? 0f : minGap;
        }

        // ── Test 7 ──────────────────────────────────────────────────────────────
        // Putt on green (Y=10.15) with overlapping fringe/Fairway (Y=10.18).
        // Ball must stay above green Y, not get lifted to the fringe collider.

        [UnityTest]
        public IEnumerator T7_Putt_StaysOnGreenNotFringe()
        {
            float x = 200f, z = 200f;
            CreateFlat(x, 10.18f, z, SurfaceType.Fairway); // fringe higher
            CreateFlat(x, 10.15f, z, SurfaceType.Green, 8f);

            yield return null; // let PhysX register

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();

            float gy = ground.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();
            var origin = new fp3(fp.FromFloat(x), fp.FromFloat(gy + 0.021f), fp.FromFloat(z));

            for (int i = 0; i < 4; i++)
            {
                float yaw = i * Mathf.PI * 0.5f;
                float speed = 2.0f + i * 0.5f;
                float pitch = 3f * Mathf.Deg2Rad;
                var vel = new fp3(
                    fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                    fp.FromFloat(speed * Mathf.Sin(pitch)),
                    fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Sin(yaw)));

                var input = new ShotInput(origin, vel, fp.FromInt(30), SpinState.None);
                var traj  = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                float gap = MinYGap(traj, SurfaceType.Green, ground, surfaces);
                Assert.That(gap, Is.GreaterThanOrEqualTo(-Epsilon),
                    $"T7 putt#{i}: ball fell {-gap:F4}m below green surface (fringe must not pull it up).");
            }
        }

        // ── Test 8 ──────────────────────────────────────────────────────────────
        // Shot landing on a green that is LOWER than a surrounding fairway/collar.
        // After landing, roll-out on green must not bounce up to the collar.

        [UnityTest]
        public IEnumerator T8_ApproachLanding_RollsOnGreenNotCollar()
        {
            float x = 202f, z = 200f;
            CreateFlat(x, 10.0f, z, SurfaceType.Fairway, 14f); // collar higher and wider
            CreateFlat(x, 9.8f, z, SurfaceType.Green, 8f);      // green lower

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();

            // Start 15m above the green, angled slightly downward toward center.
            float gy = ground.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();
            var origin = new fp3(fp.FromFloat(x - 10f), fp.FromFloat(gy + 15f), fp.FromFloat(z));
            float speed = 20f;
            float pitch = -0.15f;
            float hYaw  = Mathf.Atan2(0f, 10f); // toward +X (center)
            var vel = new fp3(
                fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Cos(hYaw)),
                fp.FromFloat(speed * Mathf.Sin(pitch)),
                fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Sin(hYaw)));

            var input = new ShotInput(origin, vel, fp.FromInt(30), SpinState.None);
            var traj  = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

            float gap = MinYGap(traj, SurfaceType.Green, ground, surfaces);
            Assert.That(gap, Is.GreaterThanOrEqualTo(-Epsilon),
                $"T8: ball fell {-gap:F4}m below green surface after approach landing.");
        }

        // ── Test 9 ──────────────────────────────────────────────────────────────
        // Ball starting on a bunker/Sand surface (lower than surrounding terrain).
        // Putt-like low shot stays on sand, doesn't snap to terrain above it.

        [UnityTest]
        public IEnumerator T9_BunkerBall_StaysOnBunkerFloor()
        {
            float x = 204f, z = 200f;
            CreateFlat(x, 10.0f, z, SurfaceType.Fairway, 14f); // terrain above
            CreateFlat(x, 8.7f,  z, SurfaceType.Sand, 6f);      // bunker floor below

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();

            float gy = ground.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Sand).ToFloat();
            Assert.That(gy, Is.LessThan(9.0f), $"T9 setup: expected bunker floor near 8.7 but got {gy:F3}");

            var origin = new fp3(fp.FromFloat(x), fp.FromFloat(gy + 0.021f), fp.FromFloat(z));

            for (int i = 0; i < 3; i++)
            {
                float yaw = i * Mathf.PI * 2f / 3f;
                // Low speed so ball stays near bunker (< 8 m/s so it may be treated as putt if surface qualifies).
                float speed = 3f;
                float pitch = 5f * Mathf.Deg2Rad;
                var vel = new fp3(
                    fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Cos(yaw)),
                    fp.FromFloat(speed * Mathf.Sin(pitch)),
                    fp.FromFloat(speed * Mathf.Cos(pitch) * Mathf.Sin(yaw)));

                var input = new ShotInput(origin, vel, fp.FromInt(20), SpinState.None);
                var traj  = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                    surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

                float gap = MinYGap(traj, SurfaceType.Sand, ground, surfaces);
                Assert.That(gap, Is.GreaterThanOrEqualTo(-Epsilon),
                    $"T9 bunker#{i}: ball fell {-gap:F4}m below sand surface.");
            }
        }

        // ── Test 10 ─────────────────────────────────────────────────────────────
        // Fairway shot that stays on fairway (no overlapping surfaces).
        // Verifies the 3-arg fix doesn't regress normal shots.

        [UnityTest]
        public IEnumerator T10_FairwayShot_NoSubsurface()
        {
            float x = 206f, z = 200f;
            // Large flat fairway — big enough the ball doesn't roll off during short shot.
            CreateFlat(x, 9.5f, z, SurfaceType.Fairway, 30f);

            yield return null;

            var ground   = new SceneGroundProvider();
            var surfaces = new SceneSurfaceProvider();

            float gy = ground.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Fairway).ToFloat();
            var origin = new fp3(fp.FromFloat(x), fp.FromFloat(gy + 0.021f), fp.FromFloat(z));

            // Short low shot — stays within the 30m collider.
            float speed = 6f;
            float pitch = 10f * Mathf.Deg2Rad;
            var vel = new fp3(fp.FromFloat(speed * Mathf.Cos(pitch)), fp.FromFloat(speed * Mathf.Sin(pitch)), fp.Zero);

            var input = new ShotInput(origin, vel, fp.FromInt(20), SpinState.None);
            var traj  = BallSimulation.Simulate(input, ground, AeroConfig.Default, WindConfig.Calm,
                surfaces, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral);

            float gap = MinYGap(traj, SurfaceType.Fairway, ground, surfaces);
            Assert.That(gap, Is.GreaterThanOrEqualTo(-Epsilon),
                $"T10: ball fell {-gap:F4}m below fairway surface.");
        }

        // ── Test 11 ─────────────────────────────────────────────────────────────
        // Smoke test against real Hole_01_Geo scene.
        // For each surface type found, verify 3-arg SampleHeight returns a plausible Y.
        // Does NOT run BallSimulation — just validates the data migration worked.

        [UnityTest]
        public IEnumerator T11_Hole01_SurfaceMarkersHaveNonTrivialY()
        {
            string[] guids = AssetDatabase.FindAssets("t:Scene Hole_01_Geo");
            if (guids.Length == 0) { Assert.Ignore("Hole_01_Geo not found."); yield break; }

            string scenePath = null;
            foreach (var g in guids)
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!p.Contains("Video")) { scenePath = p; break; }
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            yield return null; // let PhysX register

            var ground = new SceneGroundProvider();
            var checkedTypes = new HashSet<SurfaceType>();

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var marker in root.GetComponentsInChildren<SurfaceMarker>(true))
                {
                    if (checkedTypes.Contains(marker.Type)) continue;
                    // Find a Collider anywhere in this marker GO's hierarchy to get a world position.
                    var col = marker.GetComponentInChildren<Collider>();
                    if (col == null) continue;

                    float cx = col.bounds.center.x;
                    float cz = col.bounds.center.z;
                    fp sampledY = ground.SampleHeight(fp.FromFloat(cx), fp.FromFloat(cz), marker.Type);

                    // Y should be non-zero (hole terrain is 8–12m above sea level).
                    Assert.That(sampledY.ToFloat(), Is.GreaterThan(1f),
                        $"T11: SurfaceType={marker.Type} at ({cx:F1},{cz:F1}) returned Y={sampledY.ToFloat():F3} — expected >1m (hole terrain).");

                    checkedTypes.Add(marker.Type);
                    if (checkedTypes.Count >= 5) break;
                }
                if (checkedTypes.Count >= 5) break;
            }

            EditorSceneManager.CloseScene(scene, true);

            Assert.That(checkedTypes.Count, Is.GreaterThanOrEqualTo(2),
                $"T11: Expected at least 2 surface types in Hole_01_Geo, found {checkedTypes.Count}.");
        }
    }
}
