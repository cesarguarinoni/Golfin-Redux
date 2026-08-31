using System.Collections.Generic;
using NUnit.Framework;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime;
using Golfin.Physics.Runtime.Baked;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// Tests for the bridge_transplant surface path (SPEC Stage B).
    ///
    /// Covers BOTH halves of the bridge collision story:
    ///   Stage B — the SurfaceType.Bridge enum/config plumbing and the priority inversion that
    ///             makes the DECK outrank the water it spans (the ball stands ON the bridge).
    ///   Stage C — the BridgeObstacleProvider slab test that makes the RAILINGS and PIERS solid
    ///             (hit / miss / grazing / containment guard / yaw), plus fp determinism and the
    ///             bridges=null zero-behaviour-change gate.
    ///
    /// Synthetic ZoneData only — no scene loading, mirroring BakedZoneClassifierTests.
    /// </summary>
    [TestFixture]
    public class BridgeCollisionTests
    {
        private static fp F(float v) => fp.FromFloat(v);

        private static Polygon2D Square(float minX, float minZ, float maxX, float maxZ, float y)
        {
            var p = new Polygon2D();
            p.points.Add(new Point2D(minX, y, minZ));
            p.points.Add(new Point2D(maxX, y, minZ));
            p.points.Add(new Point2D(maxX, y, maxZ));
            p.points.Add(new Point2D(minX, y, maxZ));
            return p;
        }

        /// <summary>Two triangles covering the square, all vertices at the given Y.</summary>
        private static ZoneMesh QuadMesh(float minX, float minZ, float maxX, float maxZ, float y)
        {
            var m = new ZoneMesh();
            m.vertices.Add(new Point2D(minX, y, minZ));
            m.vertices.Add(new Point2D(maxX, y, minZ));
            m.vertices.Add(new Point2D(maxX, y, maxZ));
            m.vertices.Add(new Point2D(minX, y, maxZ));
            m.indices.AddRange(new[] { 0, 1, 2, 0, 2, 3 });
            return m;
        }

        private static ZonePolygonGroup Group(SurfaceType t, float yOffset,
                                              float minX, float minZ, float maxX, float maxZ, float y)
        {
            var g = new ZonePolygonGroup
            {
                type = t.ToString(),
                yOffsetFromTerrain = yOffset,
                mesh = QuadMesh(minX, minZ, maxX, maxZ, y),
            };
            g.polygons.Add(Square(minX, minZ, maxX, maxZ, y));
            return g;
        }

        /// <summary>
        /// A 40 x 40 m lake at Y = 10, spanned by a 6 x 30 m deck at Y = 14.
        /// Mirrors the real hole-7 geometry: the deck is fully INSIDE the water polygon.
        /// </summary>
        private static ZoneData WaterWithDeck()
        {
            var z = new ZoneData { holeId = "TEST_BRIDGE" };
            z.zones.Add(Group(SurfaceType.Water,  0f, -20f, -20f, 20f, 20f, 10f));
            z.zones.Add(Group(SurfaceType.Bridge, 0f,  -3f, -15f,  3f, 15f, 14f));
            return z;
        }

        // ── B1 / B2 — enum + SurfaceConfig ────────────────────────────────────

        [Test]
        public void SurfaceConfig_Default_HasARowForEverySurfaceType()
        {
            // SurfaceConfig.cs hardcodes the array length; a new enum value that misses it
            // throws IndexOutOfRangeException on the FIRST bridge classification, at runtime,
            // not at compile time (SPEC Risk 4).
            int enumCount = System.Enum.GetValues(typeof(SurfaceType)).Length;
            Assert.AreEqual(enumCount, SurfaceConfig.Default.Coefficients.Length,
                "SurfaceConfig.Default array length must match the SurfaceType value count.");
            Assert.AreEqual(12, enumCount, "SurfaceType gained Bridge = 11.");
        }

        [Test]
        public void SurfaceConfig_Bridge_ReturnsTheSpeccedCoefficients()
        {
            var c = SurfaceConfig.Default[SurfaceType.Bridge];
            Assert.AreEqual(0.45f, c.Restitution.ToFloat(),       1e-4f, "Restitution");
            Assert.AreEqual(0.35f, c.TangentFriction.ToFloat(),   1e-4f, "TangentFriction");
            Assert.AreEqual(0.12f, c.RollingResistance.ToFloat(), 1e-4f, "RollingResistance");
            Assert.AreEqual(0.10f, c.StopSpeed.ToFloat(),         1e-4f, "StopSpeed");
        }

        [Test]
        public void SurfaceConfig_Bridge_SitsBetweenCartPathAndFairway_OnFrictionAndRoll()
        {
            // SPEC B2's rationale, asserted rather than described — but only where it actually
            // holds. MEASURED, and reported for Cesar's feel pass: the SPEC's prose says the
            // Bridge row sits "between CartPath (0.70/0.18/0.06/0.08) and Fairway
            // (0.50/0.55/0.18/0.10)", and that is true of TangentFriction (0.35) and
            // RollingResistance (0.12) — but NOT of Restitution: the specced 0.45 is BELOW both
            // (it is GreenCollar's value). A timber deck being deader than fairway turf is
            // defensible, so the specced numbers are kept as written; the assertion is what
            // gets corrected.
            var bridge   = SurfaceConfig.Default[SurfaceType.Bridge];
            var cartPath = SurfaceConfig.Default[SurfaceType.CartPath];
            var fairway  = SurfaceConfig.Default[SurfaceType.Fairway];

            Assert.Greater(bridge.TangentFriction.ToFloat(), cartPath.TangentFriction.ToFloat());
            Assert.Less(bridge.TangentFriction.ToFloat(),    fairway.TangentFriction.ToFloat());
            Assert.Greater(bridge.RollingResistance.ToFloat(), cartPath.RollingResistance.ToFloat());
            Assert.Less(bridge.RollingResistance.ToFloat(),    fairway.RollingResistance.ToFloat());

            Assert.Less(bridge.Restitution.ToFloat(), cartPath.Restitution.ToFloat(),
                "a timber deck must be less lively than a concrete cart path");
            Assert.LessOrEqual(bridge.Restitution.ToFloat(), fairway.Restitution.ToFloat(),
                "specced at 0.45, i.e. at or below fairway — NOT between the two, despite the SPEC prose");
        }

        // ── B3 — PuttConfig ───────────────────────────────────────────────────

        [Test]
        public void PuttConfig_Bridge_IsNotAZeroRestitutionVoid()
        {
            var c = PuttConfig.Default.Coefficients[(int)SurfaceType.Bridge];
            Assert.AreEqual(System.Enum.GetValues(typeof(SurfaceType)).Length,
                            PuttConfig.Default.Coefficients.Length);
            Assert.Greater(c.RollingResistance.ToFloat(), 0f, "a putt on a deck must decelerate");
            Assert.Greater(c.StopSpeed.ToFloat(),         0f, "a putt on a deck must be able to stop");
            Assert.AreEqual(0.12f, c.RollingResistance.ToFloat(), 1e-4f);
        }

        // ── B4 — priority: Bridge outranks the Water it spans ─────────────────

        [Test]
        public void Classify_OnDeck_ReturnsBridge_NotWater()
        {
            var c = new BakedZoneClassifier(WaterWithDeck());
            Assert.AreEqual(SurfaceType.Bridge, c.Classify(F(0f), F(0f)),
                "A deck polygon drawn over water must win: Priority(Bridge)=95 > Priority(Water)=80.");
            Assert.AreEqual(SurfaceType.Bridge, c.Classify(F(2.9f), F(14.9f)), "deck corner");
        }

        [Test]
        public void Classify_FiveMetresOffTheDeckEdge_ReturnsWaterAgain()
        {
            var c = new BakedZoneClassifier(WaterWithDeck());
            Assert.AreEqual(SurfaceType.Water, c.Classify(F(8f),  F(0f)), "+5 m past the deck edge");
            Assert.AreEqual(SurfaceType.Water, c.Classify(F(-8f), F(0f)), "-5 m past the deck edge");
        }

        [Test]
        public void Priority_BridgeAlsoOutranksSand_ButNotGreen()
        {
            // Sand (90) is below Bridge (95); Green (100) is above it. Asserted through the
            // public surface because Priority() is private.
            var sandUnder = new ZoneData { holeId = "T" };
            sandUnder.zones.Add(Group(SurfaceType.Sand,   0f, -20f, -20f, 20f, 20f, 10f));
            sandUnder.zones.Add(Group(SurfaceType.Bridge, 0f,  -3f, -15f,  3f, 15f, 14f));
            Assert.AreEqual(SurfaceType.Bridge, new BakedZoneClassifier(sandUnder).Classify(F(0f), F(0f)));

            var greenOver = new ZoneData { holeId = "T" };
            greenOver.zones.Add(Group(SurfaceType.Bridge, 0f, -20f, -20f, 20f, 20f, 14f));
            greenOver.zones.Add(Group(SurfaceType.Green,  0f,  -3f, -15f,  3f, 15f, 10f));
            Assert.AreEqual(SurfaceType.Green, new BakedZoneClassifier(greenOver).Classify(F(0f), F(0f)));
        }

        // ── B5 / Fact 2 — the deck Y reaches the sim WITHOUT a heightmap re-bake ──

        [Test]
        public void TrySampleMeshY_OnDeck_ReturnsDeckY_NotWaterY()
        {
            var c = new BakedZoneClassifier(WaterWithDeck());
            Assert.IsTrue(c.TrySampleMeshY(F(0f), F(0f), out SurfaceType t, out float y));
            Assert.AreEqual(SurfaceType.Bridge, t);
            Assert.AreEqual(14f, y, 1e-3f, "barycentric Path β must return the deck plane, not the lake surface");
        }

        [Test]
        public void BakedHeightProvider_OnDeck_ReturnsDeckY_WithNoHeightmapAtAll()
        {
            // heightmap == null proves the deck Y comes from the zone mesh alone — which is
            // exactly why heightmap.bytes must NOT be re-baked for this feature (SPEC Fact 2).
            var provider = new BakedHeightProvider(null, new BakedZoneClassifier(WaterWithDeck()));
            Assert.AreEqual(14f, provider.SampleHeight(F(0f), F(0f)).ToFloat(), 1e-3f);
            Assert.AreEqual(10f, provider.SampleHeight(F(8f), F(0f)).ToFloat(), 1e-3f,
                "off the deck, the water mesh Y is still what the sim sees");
        }

        [Test]
        public void GetYOffset_Bridge_IsZero_TheDeckMeshIsTheSurface()
        {
            var c = new BakedZoneClassifier(WaterWithDeck());
            Assert.AreEqual(0f, c.GetYOffset(SurfaceType.Bridge), 1e-6f);
        }

        // ── Stage D (decision 2) — bots decline a deck for free ───────────────

        [Test]
        public void Bots_TreatABridgeAsAHazardAndNeverTargetIt()
        {
            // Decision 2: bots avoid bridges. That needs BOTH predicates, which is the thing the
            // SPEC got half right — see the comment on VersusBot.IsAvoidSurface.
            //   IsPlayableSurface(Bridge) == false  → the bot will not TARGET a deck.
            //   IsAvoidSurface(Bridge)    == true   → the H2 hazard path lays up short of one.
            // Reached by reflection on an uninitialised instance because both predicates are
            // private; the point is to lock the pair against a future edit.
            var t = typeof(Golfin.Physics.Viewer.VersusBot);
            const System.Reflection.BindingFlags F =
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var playable = t.GetMethod("IsPlayableSurface", F);
            var avoid    = t.GetMethod("IsAvoidSurface",    F);
            Assert.IsNotNull(playable, "VersusBot.IsPlayableSurface was renamed or removed.");
            Assert.IsNotNull(avoid,    "VersusBot.IsAvoidSurface was renamed or removed.");

            var bot = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(t);

            Assert.IsFalse((bool)playable.Invoke(bot, new object[] { SurfaceType.Bridge }),
                "adding Bridge to IsPlayableSurface would make bots aim AT bridge decks");
            Assert.IsTrue((bool)avoid.Invoke(bot, new object[] { SurfaceType.Bridge }),
                "removing Bridge from IsAvoidSurface stops bots laying up short of a bridge");

            // The pre-existing surfaces must be untouched by that change.
            Assert.IsTrue ((bool)playable.Invoke(bot, new object[] { SurfaceType.Fairway }));
            Assert.IsTrue ((bool)avoid.Invoke   (bot, new object[] { SurfaceType.Water   }));
            Assert.IsFalse((bool)avoid.Invoke   (bot, new object[] { SurfaceType.Fairway }));
            Assert.IsFalse((bool)avoid.Invoke   (bot, new object[] { SurfaceType.Sand    }));
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Stage C — railings and piers as fixed-point obstacles
        // ══════════════════════════════════════════════════════════════════════

        private static readonly BridgeCollisionProfile Railing =
            new BridgeCollisionProfile("railing", fp.FromFloat(0.35f), fp.FromFloat(0.75f));

        /// <summary>Axis-aligned box centred at the origin: 2 m wide (X), 10 m long (Z), 0–2 m tall.</summary>
        private static IBridgeObstacleProvider OneBox(float yawDeg = 0f)
        {
            double rad = yawDeg * System.Math.PI / 180.0;
            var box = new BridgeBox(
                fp.Zero, fp.Zero,                      // centre XZ
                fp.Zero, fp.FromFloat(2f),             // baseY, topY
                fp.One,  fp.FromFloat(5f),             // halfX, halfZ
                fp.FromDouble(System.Math.Cos(rad)), fp.FromDouble(System.Math.Sin(rad)),
                Railing);
            return BridgeObstacleProvider.Create(new List<BridgeBox> { box });
        }

        private static fp3 P(float x, float y, float z)
            => new fp3(fp.FromFloat(x), fp.FromFloat(y), fp.FromFloat(z));

        [Test]
        public void Slab_SegmentStraightAtAFace_HitsAtTheFaceWithAnOutwardNormal()
        {
            var p = OneBox();
            Assert.IsTrue(p.TestSegment(P(-5f, 1f, 0f), P(5f, 1f, 0f), out BridgeHit hit));
            // Enters the −X face at x = −1, i.e. 4 m into a 10 m segment.
            Assert.AreEqual(0.40f, hit.Frac.ToFloat(),     1e-3f, "entry fraction");
            Assert.AreEqual(-1.0f, hit.HitPos.x.ToFloat(), 1e-3f, "hit lands on the face, not inside");
            Assert.AreEqual(-1.0f, hit.NormalXZ.x.ToFloat(), 1e-3f, "normal points OUT of the box");
            Assert.AreEqual( 0.0f, hit.NormalXZ.z.ToFloat(), 1e-3f);
            Assert.AreEqual("railing", hit.Profile.PartName);
        }

        [Test]
        public void Slab_SegmentPassingOverTheTop_IsAMiss()
        {
            // The box top is Y=2. A ball at Y=5 sails over the railing — and a Y-face entry is
            // reported as a miss on purpose, so vertical resolution stays with the ground solver.
            var p = OneBox();
            Assert.IsFalse(p.TestSegment(P(-5f, 5f, 0f), P(5f, 5f, 0f), out _));
        }

        [Test]
        public void Slab_SegmentPassingBeside_IsAMiss()
        {
            var p = OneBox();
            Assert.IsFalse(p.TestSegment(P(-5f, 1f, 20f), P(5f, 1f, 20f), out _),
                "20 m off the end of a 10 m box");
        }

        [Test]
        public void Slab_GrazingAlongTheFace_DoesNotReportAnInteriorHit()
        {
            // Travelling parallel to the +X face, exactly on it. Whether the slab test calls this
            // a touch or a miss, what must NEVER happen is a hit reported INSIDE the box — that
            // would teleport a ball that merely brushed past.
            var p = OneBox();
            if (p.TestSegment(P(1f, 1f, -20f), P(1f, 1f, 20f), out BridgeHit hit))
                Assert.GreaterOrEqual(System.Math.Abs(hit.HitPos.x.ToFloat()), 1f - 1e-3f,
                    "a grazing hit must resolve on the face, never inside it");
        }

        [Test]
        public void Slab_ContainmentGuard_P0AlreadyInside_ReportsFracZeroAndPushesOut()
        {
            // The defect this guards: a ball rolling in Q16.16 micro-steps can walk through a
            // railing when no single step detects a face crossing. Cost a red-team iteration on
            // the tree trunk test; ported here rather than rediscovered.
            var p = OneBox();
            // p0 at x=0.6 — inside (halfX=1), nearest face is +X at 0.4 m.
            Assert.IsTrue(p.TestSegment(P(0.6f, 1f, 0f), P(0.7f, 1f, 0f), out BridgeHit hit));
            Assert.AreEqual(0f, hit.Frac.ToFloat(), 1e-6f, "containment reports frac=0");
            Assert.AreEqual(1f, hit.NormalXZ.x.ToFloat(), 1e-3f,
                "push-out along the SHALLOWEST axis: +X face is 0.4 m away, the Z faces are 5 m");
            Assert.AreEqual(0f, hit.NormalXZ.z.ToFloat(), 1e-3f);
        }

        [Test]
        public void Slab_YawedBox_RotatesTheFaceNormalBackIntoWorldSpace()
        {
            // Yaw 90° swaps the box's local axes: the 10 m length now runs along world X and the
            // 2 m width along world Z. A shot down world −Z must now hit the long face.
            var p = OneBox(90f);
            Assert.IsTrue(p.TestSegment(P(0f, 1f, -5f), P(0f, 1f, 5f), out BridgeHit hit));
            Assert.AreEqual(-1.0f, hit.HitPos.z.ToFloat(), 1e-2f, "enters at z = −1 after the yaw");
            Assert.AreEqual(-1.0f, hit.NormalXZ.z.ToFloat(), 1e-2f, "world-space outward normal");
            Assert.AreEqual( 0.0f, hit.NormalXZ.x.ToFloat(), 1e-2f);
        }

        [Test]
        public void Slab_EarliestHitWinsAcrossBoxes()
        {
            var near = new BridgeBox(fp.FromFloat(-2f), fp.Zero, fp.Zero, fp.FromFloat(2f),
                                     fp.Half, fp.FromFloat(5f), fp.One, fp.Zero, Railing);
            var far  = new BridgeBox(fp.FromFloat( 3f), fp.Zero, fp.Zero, fp.FromFloat(2f),
                                     fp.Half, fp.FromFloat(5f), fp.One, fp.Zero, Railing);
            var p = BridgeObstacleProvider.Create(new List<BridgeBox> { far, near }); // far listed FIRST
            Assert.IsTrue(p.TestSegment(P(-10f, 1f, 0f), P(10f, 1f, 0f), out BridgeHit hit));
            Assert.AreEqual(-2.5f, hit.HitPos.x.ToFloat(), 1e-2f,
                "the nearer box must win regardless of list order");
        }

        [Test]
        public void Provider_EmptyOrNullInput_ReturnsNull_NotAnEmptyProvider()
        {
            // Null is the "no bridges on this hole" signal BallSimulation gates on — an empty
            // provider would cost a spatial query per step on all 13 bridge-free holes.
            Assert.IsNull(BridgeObstacleProvider.Create(null));
            Assert.IsNull(BridgeObstacleProvider.Create(new List<BridgeBox>()));
        }

        [Test]
        public void Loader_RoundTripsACsvRow()
        {
            const string csv =
                "# bake_hash=test0001\n" +
                "centerX,centerZ,baseY,topY,halfX,halfZ,yawDeg,profileName\n" +
                "10.0000,-20.0000,5.0000,7.0000,0.2500,3.0000,90.0000,railing\n";
            var boxes = BridgeObstacleLoader.LoadBoxesFromText(csv);
            Assert.IsNotNull(boxes);
            Assert.AreEqual(1, boxes.Count);
            var b = boxes[0];
            Assert.AreEqual(10f,  b.CenterX.ToFloat(), 1e-3f);
            Assert.AreEqual(-20f, b.CenterZ.ToFloat(), 1e-3f);
            Assert.AreEqual(5f,   b.BaseY.ToFloat(),   1e-3f);
            Assert.AreEqual(7f,   b.TopY.ToFloat(),    1e-3f);
            Assert.AreEqual(0f,   b.CosYaw.ToFloat(),  1e-3f, "cos/sin are baked at load, not trig'd per step");
            Assert.AreEqual(1f,   b.SinYaw.ToFloat(),  1e-3f);
            Assert.AreEqual("railing", b.Profile.PartName);
        }

        // ── Sim-level: deflection, determinism, and the null gate ─────────────

        private static ShotInput RailingShot()
            // Rolling ball on flat ground heading +X straight into the box's −X face.
            => new ShotInput(P(-5f, 0.02f, 0f), P(12f, 0f, 0f), fp.FromInt(10));

        private static Trajectory Run(IBridgeObstacleProvider bridges)
            => BallSimulation.Simulate(
                RailingShot(), new FlatGround(fp.Zero), AeroConfig.Vacuum, WindConfig.Calm,
                new ConstantSurfaceProvider(SurfaceType.Fairway), SurfaceConfig.Default,
                PuttConfig.Default, BallPhysicsModifiers.Neutral, null, bridges, CupSpec.Disabled);

        [Test]
        public void Sim_BallFiredAtARailing_IsTurnedBack_NotPassedThrough()
        {
            var through = Run(null);
            var blocked = Run(OneBox());

            Assert.Greater(through.finalPosition.x.ToFloat(), 1f,
                "control: with no bridge the ball crosses the box footprint");
            Assert.Less(blocked.finalPosition.x.ToFloat(), -1f,
                "with the railing present the ball must be turned back, not pass through");
        }

        [Test]
        public void Sim_Determinism_TwoIdenticalShotsAreBitExact()
        {
            var bridges = OneBox();
            var a = Run(bridges);
            var b = Run(bridges);

            Assert.AreEqual(a.samples.Count, b.samples.Count, "sample count");
            Assert.AreEqual(a.finalPosition.x.raw, b.finalPosition.x.raw, "final X, raw fp");
            Assert.AreEqual(a.finalPosition.y.raw, b.finalPosition.y.raw, "final Y, raw fp");
            Assert.AreEqual(a.finalPosition.z.raw, b.finalPosition.z.raw, "final Z, raw fp");
            for (int i = 0; i < a.samples.Count; i++)
            {
                Assert.AreEqual(a.samples[i].position.x.raw, b.samples[i].position.x.raw, $"sample {i} X");
                Assert.AreEqual(a.samples[i].position.z.raw, b.samples[i].position.z.raw, $"sample {i} Z");
            }
        }

        [Test]
        public void Sim_BridgesNull_IsBitExactWithThePreStageCPath()
        {
            // The blocking gate for Stage C: adding the parameter must not move a single fp bit
            // on the 13 holes that have no bridge. Compares the new 11-arg entry (bridges=null)
            // against the pre-existing 10-arg Phase 8 entry.
            var input = RailingShot();
            var ground = new FlatGround(fp.Zero);
            var surf = new ConstantSurfaceProvider(SurfaceType.Fairway);

            var before = BallSimulation.Simulate(input, ground, AeroConfig.Vacuum, WindConfig.Calm,
                surf, SurfaceConfig.Default, PuttConfig.Default, BallPhysicsModifiers.Neutral,
                null, CupSpec.Disabled);
            var after = Run(null);

            Assert.AreEqual(before.samples.Count, after.samples.Count, "sample count");
            Assert.AreEqual(before.finalPosition.x.raw, after.finalPosition.x.raw, "final X, raw fp");
            Assert.AreEqual(before.finalPosition.y.raw, after.finalPosition.y.raw, "final Y, raw fp");
            Assert.AreEqual(before.finalPosition.z.raw, after.finalPosition.z.raw, "final Z, raw fp");
            for (int i = 0; i < before.samples.Count; i++)
                Assert.AreEqual(before.samples[i].position.x.raw, after.samples[i].position.x.raw,
                                $"sample {i} X must be bit-identical");
        }
    }
}
