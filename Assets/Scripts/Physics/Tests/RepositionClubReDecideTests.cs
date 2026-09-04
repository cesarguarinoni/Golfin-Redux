using NUnit.Framework;
using UnityEngine;
using Golfin.Physics;
using Golfin.Physics.Math;
using Golfin.Physics.Runtime.Baked;
using Golfin.Physics.Runtime;
using Golfin.Physics.Viewer;

namespace Golfin.Physics.Tests
{
    /// <summary>
    /// K11 follow-up: covers the classify→decide composition that
    /// PhysicsLabController.ReDecideClubAfterReposition performs after a REPOSITION
    /// (OB/water drop, PlaceBallAt).
    ///
    /// PutterModeSurfaceControllerTests already covers DecideTargetClub in isolation with
    /// hand-fed SurfaceType values. What was never covered — and what the defect actually
    /// was — is that a repositioned ball reaches that decision at all, and that the surface
    /// it is judged on comes from the SAME baked classifier the sim uses for EndSurface.
    /// These tests run that join against REAL baked hole data (Hole 6) rather than a stub,
    /// so a re-bake that moved the green out from under the rule would fail here.
    ///
    /// Points are DISCOVERED by scanning, not hardcoded, so the test survives a re-bake.
    /// </summary>
    public class RepositionClubReDecideTests
    {
        const int Putter = 3;
        const int Wood   = 0;

        const string ZonesPath = "HoleData/lomond-country-club/Hole_06/zones";

        BakedZoneClassifier _classifier;
        Vector2? _greenPoint;      // a point the classifier calls Green
        Vector2? _offGreenPoint;   // a playable point it does not

        [SetUp]
        public void SetUp()
        {
            var asset = Resources.Load<TextAsset>(ZonesPath);
            Assert.IsNotNull(asset, $"Baked zone data missing at Resources/{ZonesPath}");

            _classifier = new BakedZoneClassifier(ZoneData.FromJson(HoleDataIO.DecodeZonesText(asset)));

            for (float x = -200f; x <= 200f; x += 2f)
            {
                for (float z = -200f; z <= 200f; z += 2f)
                {
                    SurfaceType s = Classify(x, z);
                    if (s == SurfaceType.Green && _greenPoint == null)
                        _greenPoint = new Vector2(x, z);
                    else if ((s == SurfaceType.Rough || s == SurfaceType.Fairway) && _offGreenPoint == null)
                        _offGreenPoint = new Vector2(x, z);
                }
                if (_greenPoint != null && _offGreenPoint != null) break;
            }
        }

        SurfaceType Classify(float x, float z)
            => _classifier.Classify(fp.FromFloat(x), fp.FromFloat(z));

        /// <summary>Mirrors ReDecideClubAfterReposition: classify the drop point, then decide.</summary>
        int DecideAt(Vector2 p, int currentClub, int lastNonPutter)
            => PutterModeSurfaceController.DecideTargetClub(
                currentClubIndex: currentClub,
                putterIndex: Putter,
                endSurface: Classify(p.x, p.y),
                lastNonPutterClubIndex: lastNonPutter);

        [Test]
        public void BakedHoleData_ExposesBothAGreenAndAnOffGreenPoint()
        {
            Assert.IsNotNull(_greenPoint, "No Green found in Hole 6 baked zones — the fixture is broken.");
            Assert.IsNotNull(_offGreenPoint, "No Rough/Fairway found in Hole 6 baked zones.");
            Assert.AreEqual(SurfaceType.Green, Classify(_greenPoint.Value.x, _greenPoint.Value.y));
            Assert.AreNotEqual(SurfaceType.Green, Classify(_offGreenPoint.Value.x, _offGreenPoint.Value.y));
        }

        // The bug, direction 1: dropped ONTO the green holding a wood → must switch to putter.
        [Test]
        public void RepositionOntoGreen_FromWood_SwitchesToPutter()
        {
            Assert.AreEqual(Putter, DecideAt(_greenPoint.Value, currentClub: Wood, lastNonPutter: Wood));
        }

        // The bug, direction 2: dropped OFF the green still in putter mode → must revert.
        [Test]
        public void RepositionOffGreen_FromPutter_RevertsToLastNonPutter()
        {
            Assert.AreEqual(Wood, DecideAt(_offGreenPoint.Value, currentClub: Putter, lastNonPutter: Wood));
        }

        // Idempotent both ways — a reposition that does not cross the green boundary is a no-op,
        // so repositioning cannot thrash the club or spam SetClub.
        [Test]
        public void RepositionOntoGreen_AlreadyPutter_IsNoOp()
        {
            Assert.AreEqual(-1, DecideAt(_greenPoint.Value, currentClub: Putter, lastNonPutter: Wood));
        }

        [Test]
        public void RepositionOffGreen_AlreadyNonPutter_IsNoOp()
        {
            Assert.AreEqual(-1, DecideAt(_offGreenPoint.Value, currentClub: Wood, lastNonPutter: Wood));
        }

        /// <summary>
        /// Regression-pins the live boundary-OB capture (2026-08-05): stroke-and-distance drops
        /// the ball back at the previous shot origin, which on Hole 6 is the TEE. Tee is not
        /// Green, so a non-putter player correctly needs no switch — this is why that capture
        /// logged no §2f line. Documents that the quiet no-op was correct, not a silent failure.
        /// </summary>
        [Test]
        public void BoundaryOBDropAtTee_FromWood_IsNoOp()
        {
            var teeDrop = new Vector2(80.21f, -24.54f);   // drop point from the recorded capture
            Assert.AreEqual(SurfaceType.Tee, Classify(teeDrop.x, teeDrop.y),
                "Hole 6 stroke-and-distance drop point should classify as Tee.");
            Assert.AreEqual(-1, DecideAt(teeDrop, currentClub: Wood, lastNonPutter: Wood));
        }

        /// <summary>
        /// The case that actually bit in real play: a water penalty taken from the green moves
        /// the ball off it (lateral relief, never nearer the hole), which must drop putter mode.
        /// </summary>
        [Test]
        public void WaterReliefFromGreenToOffGreen_DropsPutterMode()
        {
            Assert.AreEqual(Wood, DecideAt(_offGreenPoint.Value, currentClub: Putter, lastNonPutter: Wood),
                "Relief that moves the ball off the green must revert from the putter.");
        }
    }
}
