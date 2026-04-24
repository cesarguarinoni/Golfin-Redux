using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Phase 3 A-group tests for SceneGroundProvider.SampleHeight 3-arg surface-aware overload.
    /// Tests 1–6 must all pass before any Phase 5 stress run.
    /// </summary>
    public class GroundProviderSurfacePreferenceTests
    {
        readonly List<GameObject> _created = new List<GameObject>();

        GameObject Track(GameObject go) { _created.Add(go); return go; }

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _created)
                if (go != null) Object.DestroyImmediate(go);
            _created.Clear();
        }

        // Creates a thin BoxCollider whose top face is at worldY (center is at worldY - 0.01).
        GameObject CreateFlatCollider(float x, float worldY, float z, float size = 6f)
        {
            var go = Track(new GameObject("FlatCollider"));
            go.transform.position = new Vector3(x, worldY - 0.01f, z);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(size, 0.02f, size);
            return go;
        }

        SurfaceMarker AddMarker(GameObject go, SurfaceType type)
        {
            var m = go.AddComponent<SurfaceMarker>();
            m.Type = type;
            return m;
        }

        // ── Test 1 ──────────────────────────────────────────────────────────────────
        // Bug-1 reproduction scenario: green (Y=10.15) under fringe-on-fairway (Y=10.18).
        // With preferred=Green the lower green collider must win over the higher fringe.

        [UnityTest]
        public IEnumerator SampleHeight_PreferredGreen_OverFringe()
        {
            float x = 100f, z = 100f;

            var fringeGO = CreateFlatCollider(x, 10.18f, z);
            AddMarker(fringeGO, SurfaceType.Fairway); // fringe stored as Fairway

            var greenGO = CreateFlatCollider(x, 10.15f, z);
            AddMarker(greenGO, SurfaceType.Green);

            yield return null; // let PhysX register

            var provider = new SceneGroundProvider();
            float result = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();

            // Preferred Green collider top is ~10.15; fringe is ~10.18 and must NOT win.
            Assert.That(result, Is.LessThan(10.17f),
                $"Expected green hit ≈10.15 but got {result}. Fringe must not override preferred green.");
            Assert.That(result, Is.GreaterThan(10.13f),
                $"Expected green hit ≈10.15 but got {result}.");
        }

        // ── Test 2 ──────────────────────────────────────────────────────────────────
        // Bunker (Y=8.7, SurfaceMarker=Sand) below surrounding terrain (Y=10.0).
        // With preferred=Sand, the sunken bunker floor must win over the higher terrain.

        [UnityTest]
        public IEnumerator SampleHeight_PreferredBunker_UnderTerrain()
        {
            float x = 102f, z = 100f;

            var terrainGO = CreateFlatCollider(x, 10.0f, z, 8f);
            // No SurfaceMarker → falls back to Fairway in classification, no marker here

            var bunkerGO = CreateFlatCollider(x, 8.7f, z, 4f);
            AddMarker(bunkerGO, SurfaceType.Sand);

            yield return null;

            var provider = new SceneGroundProvider();
            float result = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Sand).ToFloat();

            Assert.That(result, Is.LessThan(8.72f),
                $"Expected bunker floor ≈8.7 but got {result}. Terrain must not override preferred Sand.");
            Assert.That(result, Is.GreaterThan(8.68f),
                $"Expected bunker floor ≈8.7 but got {result}.");
        }

        // ── Test 3 ──────────────────────────────────────────────────────────────────
        // When no hit carries the preferred SurfaceMarker type, fall back to max-Y of all hits.

        [UnityTest]
        public IEnumerator SampleHeight_PreferredNotFound_FallsBackToMaxY()
        {
            float x = 104f, z = 100f;

            var terrainGO = CreateFlatCollider(x, 10.0f, z, 6f);
            // No SurfaceMarker on terrain — no preferred match possible

            yield return null;

            var provider = new SceneGroundProvider();
            float result = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();

            Assert.That(result, Is.EqualTo(10.0f).Within(0.02f),
                $"Expected fallback to max-Y≈10.0 but got {result}.");
        }

        // ── Test 4 ──────────────────────────────────────────────────────────────────
        // Regression: 2-arg overload must return max-Y unchanged (protects airborne path).

        [UnityTest]
        public IEnumerator SampleHeight_TwoArg_Unchanged()
        {
            float x = 106f, z = 100f;

            var greenGO = CreateFlatCollider(x, 10.15f, z);
            AddMarker(greenGO, SurfaceType.Green);

            var fringeGO = CreateFlatCollider(x, 10.18f, z);
            AddMarker(fringeGO, SurfaceType.Fairway);

            yield return null;

            var provider = new SceneGroundProvider();
            float result2 = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z)).ToFloat();
            float result3 = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();

            // 2-arg must return the HIGHER of the two (max-Y = fringe ≈10.18).
            Assert.That(result2, Is.GreaterThan(10.16f),
                $"2-arg should return max-Y≈10.18 but got {result2}.");
            // 3-arg with Green must return the lower green ≈10.15.
            Assert.That(result3, Is.LessThan(10.17f),
                $"3-arg preferred=Green should return ≈10.15 but got {result3}.");
            // The two results must differ, proving the overload actually does something.
            Assert.That(result2, Is.GreaterThan(result3),
                "2-arg max-Y must exceed 3-arg preferred-Green result.");
        }

        // ── Test 5 ──────────────────────────────────────────────────────────────────
        // Empty scene (no colliders at test XZ) → fp.Zero regardless of preferred type.

        [Test]
        public void SampleHeight_EmptyScene_ReturnsZero()
        {
            var provider = new SceneGroundProvider();
            fp result = provider.SampleHeight(fp.FromFloat(99999f), fp.FromFloat(99999f), SurfaceType.Green);
            Assert.That(result, Is.EqualTo(fp.Zero),
                $"Expected fp.Zero for empty scene but got {result}.");
        }

        // ── Test 6 ──────────────────────────────────────────────────────────────────
        // Multiple overlapping Green colliders at same XZ — returns the HIGHEST among preferred.

        [UnityTest]
        public IEnumerator SampleHeight_MultipleGreens_ReturnsHighestOfPreferred()
        {
            float x = 108f, z = 100f;

            var g1 = CreateFlatCollider(x, 10.10f, z);
            AddMarker(g1, SurfaceType.Green);

            var g2 = CreateFlatCollider(x, 10.15f, z);
            AddMarker(g2, SurfaceType.Green);

            var g3 = CreateFlatCollider(x, 10.12f, z);
            AddMarker(g3, SurfaceType.Green);

            yield return null;

            var provider = new SceneGroundProvider();
            float result = provider.SampleHeight(fp.FromFloat(x), fp.FromFloat(z), SurfaceType.Green).ToFloat();

            Assert.That(result, Is.EqualTo(10.15f).Within(0.02f),
                $"Expected highest preferred-Green hit ≈10.15 but got {result}.");
        }
    }
}
