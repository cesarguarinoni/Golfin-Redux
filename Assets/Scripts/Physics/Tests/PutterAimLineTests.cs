using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime.Baked;
using Golfin.Physics.Viewer;
using Golfin.Gameplay.Input;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// EditMode unit tests for <see cref="PutterAimLine"/>.
    ///
    /// SPEC: putter_aim_blue_line (Rev 2) — Definition of done items 3, 4, 5, 7, 8.
    /// DoD 2 (appears on entering putter aim / hides on shot start) is covered here at the
    /// event level by T7; its on-screen counterpart is the play-mode visual gate.
    ///
    /// The synthetic green mirrors PutterGreenReaderBakeTests: a 5 m × 5 m square from
    /// (0,0) to (5,5) in XZ with a constant 4% grade along +X, so every expected surface
    /// height is analytic (h = 0.04·x) and the assertions can be exact rather than "looks
    /// about right".
    /// </summary>
    public class PutterAimLineTests
    {
        private const float SlopeGradeX = 0.04f;
        private const float LineOffset  = 0.04f;   // PutterAimLine._surfaceYOffset default
        private const float GridOffset  = 0.02f;   // PutterGreenReader._surfaceYOffset default

        private BakedZoneClassifier _classifier;
        private PutterGreenReader   _reader;
        private PutterAimLine       _line;
        private GameObject          _go;

        [SetUp]
        public void SetUp()
        {
            var data = BuildSyntheticZoneData(0f, 5f, 0f, 5f, SlopeGradeX, 0f);
            _classifier = new BakedZoneClassifier(data);

            _go     = new GameObject("PutterAimLineTest");
            _reader = _go.AddComponent<PutterGreenReader>();
            _line   = _go.AddComponent<PutterAimLine>();   // OnEnable self-wires _greenReader

            _reader.BakeCells(_classifier);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // ── T1 — Geometry budget: 31 samples / 62 verts / 60 tris / 1 submesh ──
        //
        // SPEC §8.3 states the exact budget. A regression that changes the sample pitch
        // or doubles the strip silently multiplies the per-rebuild upload cost, so the
        // numbers are asserted literally.

        [Test]
        public void AimLine_GeometryMatchesSpecBudget()
        {
            _line.SetBallPositionOverride(new Vector3(2.5f, 0.1f, 2.5f));
            _line.SetAimYawOverride(0f);
            _line.RebuildForTest();

            Assert.AreEqual(31, _line.SampleCount,
                "SPEC §8.3: 15 m at a 0.5 m pitch is 31 samples.");
            Assert.AreEqual(62, _line.MeshVertexCount,
                "SPEC §8.3: 31 samples × 2 verts per rib = 62 vertices.");

            var mesh = GetLineMesh();
            Assert.AreEqual(60, mesh.triangles.Length / 3,
                "SPEC §8.3: 30 segments × 2 triangles = 60 triangles.");
            Assert.AreEqual(1, mesh.subMeshCount,
                "One submesh — SPEC §8.3 DoD 8 requires a single draw call.");
        }

        // ── T2 — DoD 3: the strip follows the aim heading, ball-anchored ───────
        //
        // Convention is (cos θ, 0, sin θ) — ShotInputState.AimYawRadians, as consumed by
        // ShotConeView:434. Checked at two headings so a swapped sin/cos cannot pass.

        [Test]
        public void AimLine_FollowsAimHeadingFromBall()
        {
            var ball = new Vector3(1.0f, 0.0f, 2.5f);

            // θ = 0 → +X.
            AssertHeading(ball, 0f, new Vector3(1f, 0f, 0f));
            // θ = π/2 → +Z.
            AssertHeading(ball, Mathf.PI * 0.5f, new Vector3(0f, 0f, 1f));
            // θ = π/4 → diagonal.
            AssertHeading(ball, Mathf.PI * 0.25f, new Vector3(0.70710678f, 0f, 0.70710678f));
        }

        private void AssertHeading(Vector3 ball, float yaw, Vector3 expectedDir)
        {
            _line.SetBallPositionOverride(ball);
            _line.SetAimYawOverride(yaw);
            _line.RebuildForTest();

            var verts = GetLineMesh().vertices;

            // Rib 0 straddles the ball; its midpoint is the ball XZ.
            Vector3 rib0 = (verts[0] + verts[1]) * 0.5f;
            Assert.AreEqual(ball.x, rib0.x, 1e-3f, "Line must start at the ball X.");
            Assert.AreEqual(ball.z, rib0.z, 1e-3f, "Line must start at the ball Z.");

            // Last rib is 15 m along the heading.
            Vector3 ribN = (verts[verts.Length - 2] + verts[verts.Length - 1]) * 0.5f;
            Vector3 travel = ribN - rib0; travel.y = 0f;
            Assert.AreEqual(15f, travel.magnitude, 1e-2f, "Line length must be the 15 m SPEC §4 fixed value.");
            Assert.AreEqual(expectedDir.x, travel.normalized.x, 1e-3f, $"Heading X at yaw {yaw}.");
            Assert.AreEqual(expectedDir.z, travel.normalized.z, 1e-3f, $"Heading Z at yaw {yaw}.");

            // Rib width is the SPEC §4 0.08 m, measured perpendicular to the heading.
            Vector3 rib = verts[1] - verts[0]; rib.y = 0f;
            Assert.AreEqual(0.08f, rib.magnitude, 1e-3f, "Strip width must be 0.08 m.");
            Assert.AreEqual(0f, Vector3.Dot(rib.normalized, expectedDir), 1e-3f,
                "The rib must be perpendicular to the aim heading.");
        }

        // ── T3 — DoD 5: SetBallPositionOverride is honoured ────────────────────
        //
        // With no PhysicsLabController wired, the un-overridden line sits at the world
        // origin. The override is what makes the visual-gate captures meaningful, so its
        // absence must be detectable, not silently "close enough".

        [Test]
        public void AimLine_BallPositionOverride_MovesTheLine()
        {
            _line.SetAimYawOverride(0f);

            _line.SetBallPositionOverride(null);
            _line.RebuildForTest();
            Vector3 atOrigin = FirstRibMidpoint();

            _line.SetBallPositionOverride(new Vector3(2.5f, 0f, 2.5f));
            _line.RebuildForTest();
            Vector3 atOverride = FirstRibMidpoint();

            Assert.AreEqual(0f, atOrigin.x, 1e-3f, "With no override and no lab controller, the line anchors at the origin.");
            Assert.AreEqual(2.5f, atOverride.x, 1e-3f, "The override must move the line's anchor X.");
            Assert.AreEqual(2.5f, atOverride.z, 1e-3f, "The override must move the line's anchor Z.");
        }

        // ── T4 — DoD 4 + 8: height comes from the bake, 2 cm above the grid ────
        //
        // The z-fight defence is the whole reason SPEC §8.4 mandates a shared height
        // source. This asserts the constant-gap property directly: at every vertex over
        // the baked green, lineY − gridSurfaceY == 0.04 m, i.e. exactly 0.02 m above the
        // grid mesh's own lift. A raycast-based or nearest-cell implementation would drift.

        [Test]
        public void AimLine_VertexHeights_ComeFromBake_AndClearTheGrid()
        {
            _line.SetBallPositionOverride(new Vector3(0.5f, 0f, 2.5f));
            _line.SetAimYawOverride(0f);           // +X, straight up the 4% grade
            _line.RebuildForTest();

            var verts = GetLineMesh().vertices;
            int checkedOnGreen = 0;

            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                if (!_reader.TrySampleBakedSurfaceY(v.x, v.z, out float surfaceY)) continue;

                Assert.AreEqual(surfaceY + LineOffset, v.y, 1e-4f,
                    $"Vertex {i} at ({v.x:F2},{v.z:F2}) must sit {LineOffset:F2} m above the baked surface.");
                Assert.AreEqual(GridOffset, v.y - (surfaceY + GridOffset), 1e-4f,
                    $"Vertex {i} must clear the grid mesh by {GridOffset * 100f:F0} cm.");
                checkedOnGreen++;
            }

            Assert.Greater(checkedOnGreen, 10,
                "Pre-condition: a meaningful stretch of the line must lie over the baked green.");

            // And the heights actually track the slope rather than being flat: the green
            // rises 4% along +X, so the far end over the green must be above the near end.
            Assert.Greater(verts[verts.Length - 2].y, verts[0].y + 0.05f,
                "Line Y must climb with the 4% green grade — a flat line would sink into any slope.");
        }

        [Test]
        public void AimLine_OffBakeTail_HoldsLastBakedHeight_NoRaycast()
        {
            // From (4.5, 2.5) heading +X, the line leaves the 5 m green after ~0.5 m and the
            // remaining ~14.5 m is off-bake. SPEC §8.4 permits carrying the last baked Y.
            _line.SetBallPositionOverride(new Vector3(4.5f, 0f, 2.5f));
            _line.SetAimYawOverride(0f);
            _line.RebuildForTest();

            var verts = GetLineMesh().vertices;
            float lastY = verts[verts.Length - 1].y;

            Assert.IsFalse(float.IsNaN(lastY), "Off-bake vertices must not be NaN.");
            Assert.AreEqual(verts[verts.Length - 3].y, lastY, 1e-5f,
                "Consecutive off-bake vertices must hold a constant carried-forward height.");
            Assert.Greater(lastY, 0f, "The carried height must be the last baked surface, not zero.");
        }

        // ── T5 — DoD 7: rebuild-on-dirty ──────────────────────────────────────

        [Test]
        public void AimLine_DirtyCheck_SkipsRebuildWhenNothingMoved()
        {
            _line.SetBallPositionOverride(new Vector3(2.5f, 0f, 2.5f));
            _line.SetAimYawOverride(0f);
            _line.RebuildForTest();

            int baseline = _line.RebuildCount;

            for (int i = 0; i < 60; i++)
                Assert.IsFalse(_line.TickIfDirty(), $"Tick {i} rebuilt with no aim or ball change.");

            Assert.AreEqual(baseline, _line.RebuildCount,
                "SPEC §8.1: holding an aim for 60 frames must cost zero mesh rebuilds.");

            // Sub-threshold movement is still a no-op: 0.01° of yaw, 5 mm of ball drift.
            _line.SetAimYawOverride(0.01f * Mathf.Deg2Rad);
            _line.SetBallPositionOverride(new Vector3(2.5f, 0f, 2.5f));
            // (both setters force _hasBuilt=false, so settle first)
            _line.TickIfDirty();
            baseline = _line.RebuildCount;

            SetPrivateFloatFreeTick(0.02f * Mathf.Deg2Rad, new Vector3(2.5045f, 0f, 2.5f));
            Assert.AreEqual(baseline, _line.RebuildCount,
                "SPEC §8.1: 0.01° of yaw and 4.5 mm of ball drift are below the dirty thresholds.");

            // Above threshold → exactly one rebuild.
            SetPrivateFloatFreeTick(5f * Mathf.Deg2Rad, new Vector3(2.5045f, 0f, 2.5f));
            Assert.AreEqual(baseline + 1, _line.RebuildCount,
                "A 5° aim change must trigger exactly one rebuild.");
        }

        // Sets the override fields directly (bypassing the setters' force-dirty flag) so the
        // test measures the threshold comparison itself rather than the setter's behaviour.
        private void SetPrivateFloatFreeTick(float yaw, Vector3 ball)
        {
            var t = typeof(PutterAimLine);
            t.GetField("_aimYawOverride", BindingFlags.NonPublic | BindingFlags.Instance)
             .SetValue(_line, (float?)yaw);
            t.GetField("_ballPositionOverride", BindingFlags.NonPublic | BindingFlags.Instance)
             .SetValue(_line, (Vector3?)ball);
            _line.TickIfDirty();
        }

        // ── T6 — Renderer setup: unlit overlay, no shadows (DoD 8) ────────────

        [Test]
        public void AimLine_Renderer_CastsNoShadows_SingleRenderer()
        {
            _line.SetBallPositionOverride(new Vector3(2.5f, 0f, 2.5f));
            _line.RebuildForTest();

            var renderers = _go.GetComponentsInChildren<MeshRenderer>(true);
            var lineRenderer = FindLineRenderer(renderers);

            Assert.IsNotNull(lineRenderer, "The aim line must own exactly one MeshRenderer child.");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, lineRenderer.shadowCastingMode,
                "SPEC §4/§8.3: the line must not cast shadows.");
            Assert.IsFalse(lineRenderer.receiveShadows,
                "SPEC §4/§8.3: the line must not receive shadows.");
            Assert.AreEqual(UnityEngine.Rendering.LightProbeUsage.Off, lineRenderer.lightProbeUsage,
                "Unlit overlay: light probes would add per-object setup for nothing.");
        }

        // ── T7 — DoD 2: gated by putter aim state ─────────────────────────────
        //
        // Drives a real ShotController through its external-drag API, which is the same
        // PublishState path production input uses, rather than poking _aimActive directly.

        [Test]
        public void AimLine_VisibilityFollowsPutterAimState()
        {
            var shotGO = new GameObject("ShotControllerTest");
            try
            {
                var shot = shotGO.AddComponent<ShotController>();
                shot.IsPutt = true;

                // Wire the controller and run the subscription path by hand: EditMode does not
                // invoke OnEnable for a plain MonoBehaviour, so `enabled = true` would not
                // subscribe. This still exercises the production OnEnable body.
                var t = typeof(PutterAimLine);
                t.GetField("_shotController", BindingFlags.NonPublic | BindingFlags.Instance)
                 .SetValue(_line, shot);
                InvokeLifecycle("OnEnable");

                Assert.IsFalse(_line.AimActive, "The line must start hidden.");

                shot.BeginExternalDrag();          // Idle → Aiming, publishes state
                Assert.IsTrue(_line.AimActive,
                    "DoD 2: the line must appear on entering putter aim.");

                // CancelExternalDrag transitions to Idle; the state is published on the next
                // Tick, which is the production path (Tick publishes every frame).
                shot.CancelExternalDrag();
                shot.Tick(0.016f);
                Assert.IsFalse(_line.AimActive,
                    "DoD 2: the line must hide when aim ends.");

                // Not a putt → never shown, even while aiming.
                shot.IsPutt = false;
                shot.BeginExternalDrag();
                Assert.IsFalse(_line.AimActive,
                    "SPEC §6: no aim line for iron/driver — the cone already covers those.");
            }
            finally
            {
                Object.DestroyImmediate(shotGO);
            }
        }

        [Test]
        public void AimLine_HidesMesh_OnComponentDisable()
        {
            _line.SetAimActiveForTest(true);
            _line.SetBallPositionOverride(new Vector3(2.5f, 0f, 2.5f));
            _line.RebuildForTest();

            var meshGO = GetLineMeshGO();
            Assert.IsTrue(meshGO.activeSelf, "Mesh GO should be active while aiming.");

            InvokeLifecycle("OnDisable");
            Assert.IsFalse(meshGO.activeSelf, "OnDisable must hide the line mesh.");
            Assert.IsFalse(_line.AimActive, "OnDisable must clear the aim flag.");
        }

        // ── T8 — the shared height accessor itself ────────────────────────────

        [Test]
        public void GreenReader_TrySampleBakedSurfaceY_MatchesAnalyticSlope()
        {
            // Mid-quad sample (not a cell centre) — this is the case a nearest-cell lookup
            // would get wrong and where the constant-gap guarantee actually lives.
            Assert.IsTrue(_reader.TrySampleBakedSurfaceY(2.37f, 2.63f, out float y),
                "A point well inside the synthetic green must resolve.");
            Assert.AreEqual(SlopeGradeX * 2.37f, y, 2e-3f,
                "Interpolated surface height must match the analytic 4% grade.");

            Assert.IsFalse(_reader.TrySampleBakedSurfaceY(-50f, -50f, out _),
                "A point far off the green must report no baked sample.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // EditMode never invokes MonoBehaviour lifecycle callbacks on a plain component, so the
        // tests call the real OnEnable / OnDisable bodies directly rather than asserting against
        // a shape that only exists in play mode.
        private void InvokeLifecycle(string method)
        {
            typeof(PutterAimLine)
                .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(_line, null);
        }

        // Rib 0 straddles the ball by ±half the strip width, so the anchor is the midpoint,
        // not vertex 0.
        private Vector3 FirstRibMidpoint()
        {
            var verts = GetLineMesh().vertices;
            return (verts[0] + verts[1]) * 0.5f;
        }

        private GameObject GetLineMeshGO()
        {
            var t = _go.transform.Find("PutterAimLineMesh");
            Assert.IsNotNull(t, "PutterAimLine must create a 'PutterAimLineMesh' child GO.");
            return t.gameObject;
        }

        private Mesh GetLineMesh()
        {
            var mf = GetLineMeshGO().GetComponent<MeshFilter>();
            Assert.IsNotNull(mf, "The line mesh GO must carry a MeshFilter.");
            Assert.IsNotNull(mf.sharedMesh, "The line MeshFilter must have a mesh after a rebuild.");
            return mf.sharedMesh;
        }

        private static MeshRenderer FindLineRenderer(MeshRenderer[] renderers)
        {
            foreach (var r in renderers)
                if (r.gameObject.name == "PutterAimLineMesh") return r;
            return null;
        }

        // Identical synthetic green to PutterGreenReaderBakeTests (kept local so the two
        // suites stay independently runnable).
        private static ZoneData BuildSyntheticZoneData(
            float greenMinX, float greenMaxX,
            float greenMinZ, float greenMaxZ,
            float slopeGradeX, float slopeGradeZ)
        {
            float H(float x, float z) => slopeGradeX * x + slopeGradeZ * z;

            int resolution = 6;
            float stepX = (greenMaxX - greenMinX) / (resolution - 1);
            float stepZ = (greenMaxZ - greenMinZ) / (resolution - 1);

            var mesh = new ZoneMesh();
            for (int iz = 0; iz < resolution; iz++)
                for (int ix = 0; ix < resolution; ix++)
                {
                    float wx = greenMinX + ix * stepX;
                    float wz = greenMinZ + iz * stepZ;
                    mesh.vertices.Add(new Point2D(wx, H(wx, wz), wz));
                }

            for (int iz = 0; iz < resolution - 1; iz++)
                for (int ix = 0; ix < resolution - 1; ix++)
                {
                    int a = iz * resolution + ix;
                    int b = a + 1;
                    int c = a + resolution;
                    int d = c + 1;
                    mesh.indices.Add(a); mesh.indices.Add(b); mesh.indices.Add(c);
                    mesh.indices.Add(b); mesh.indices.Add(d); mesh.indices.Add(c);
                }

            var poly = new Polygon2D();
            poly.points.Add(new Point2D(greenMinX, H(greenMinX, greenMinZ), greenMinZ));
            poly.points.Add(new Point2D(greenMaxX, H(greenMaxX, greenMinZ), greenMinZ));
            poly.points.Add(new Point2D(greenMaxX, H(greenMaxX, greenMaxZ), greenMaxZ));
            poly.points.Add(new Point2D(greenMinX, H(greenMinX, greenMaxZ), greenMaxZ));

            var group = new ZonePolygonGroup
            {
                type = "Green",
                yOffsetFromTerrain = 0f,
                mesh     = mesh,
                polygons = new List<Polygon2D> { poly },
            };

            return new ZoneData
            {
                holeId = "SyntheticAimLineTest",
                zones  = new List<ZonePolygonGroup> { group },
            };
        }
    }
}
