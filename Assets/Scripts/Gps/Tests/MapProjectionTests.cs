// gps_checkin §C4 — the pin projection, pinned against values computed independently.
//
// WHY THE FIXTURES ARE NOT ROUND NUMBERS. Every expected offset below was computed from the
// Web Mercator formula in Python, OUTSIDE this code, against the same three points the acceptance
// list names — so the test compares two independent implementations rather than asserting that
// the code equals itself. A pin that lands 30 px off is invisible in a screenshot and obvious on
// a phone; the ≤2 px tolerance is what makes that catchable here instead.
using System;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Gps.Tests
{
    public class MapProjectionTests
    {
        // The Rounds panel's own tile: centred on TEST Office (venue 1993), zoom 13, scale 2.
        const double CentreLat = 35.654103;
        const double CentreLon = 139.779219;
        const int Zoom = 13;
        const float Scale = 2f;

        /// <summary>The acceptance list's tolerance (SPEC § Acceptance: "3 known points, ≤ 2 px").</summary>
        const float Tol = 2f;

        [Test]
        public void Centre_ProjectsToTheOrigin()
        {
            Vector2 o = MapProjection.Offset(CentreLat, CentreLon, CentreLat, CentreLon, Zoom, Scale);
            Assert.AreEqual(0f, o.x, 1e-3f);
            Assert.AreEqual(0f, o.y, 1e-3f);
        }

        [Test]
        public void ThreeKnownPoints_LandWhereWebMercatorPutsThem()
        {
            // name, lat, lon, expected x, expected y — computed in Python from
            //   n = 256 * 2^13;  x = (lon+180)/360*n;  y = (0.5 - ln((1+s)/(1-s))/(4pi))*n
            // then (p - c) * 2, with Y negated for Unity's Y-up rect space.
            var cases = new (string name, double lat, double lon, float x, float y)[]
            {
                ("TEST Home (Higashikanda) 1992", 35.695488,  139.7811596,   22.6096f,  593.5570f),
                ("Tokyo Station",                 35.681236,  139.767125,  -140.9053f,  389.1155f),
                ("焼肉 GREEN (demo seed)",         35.690,     139.625,    -1796.7816f, 514.8287f),
            };

            foreach (var c in cases)
            {
                Vector2 o = MapProjection.Offset(CentreLat, CentreLon, c.lat, c.lon, Zoom, Scale);
                Assert.AreEqual(c.x, o.x, Tol, c.name + " x");
                Assert.AreEqual(c.y, o.y, Tol, c.name + " y");
            }
        }

        [Test]
        public void NorthIsUp_AndEastIsRight()
        {
            // The single most damaging way to get this wrong is a sign flip, which looks plausible
            // in a screenshot and puts every pin on the wrong side of the player.
            Vector2 north = MapProjection.Offset(CentreLat, CentreLon, CentreLat + 0.01, CentreLon, Zoom, Scale);
            Vector2 east  = MapProjection.Offset(CentreLat, CentreLon, CentreLat, CentreLon + 0.01, Zoom, Scale);
            Assert.Greater(north.y, 0f, "a point NORTH of the centre must be ABOVE it");
            Assert.AreEqual(0f, north.x, 1e-3f);
            Assert.Greater(east.x, 0f, "a point EAST of the centre must be RIGHT of it");
            Assert.AreEqual(0f, east.y, 1e-3f);
        }

        [Test]
        public void OneZoomStep_DoublesTheOffset()
        {
            Vector2 a = MapProjection.Offset(CentreLat, CentreLon, 35.695488, 139.7811596, 13, Scale);
            Vector2 b = MapProjection.Offset(CentreLat, CentreLon, 35.695488, 139.7811596, 14, Scale);
            Assert.AreEqual(a.x * 2f, b.x, Tol);
            Assert.AreEqual(a.y * 2f, b.y, Tol);
        }

        [Test]
        public void ScaleMultipliesTheOffset_ButZoomDoesNot()
        {
            // The trap the header of MapProjection.cs names: /venue/map asks Google for a
            // half-size image at scale=2, so the projection runs in GOOGLE pixels and `scale` is
            // the only thing that converts them to the RawImage's.
            Vector2 at1 = MapProjection.Offset(CentreLat, CentreLon, 35.681236, 139.767125, Zoom, 1f);
            Vector2 at2 = MapProjection.Offset(CentreLat, CentreLon, 35.681236, 139.767125, Zoom, 2f);
            Assert.AreEqual(at1.x * 2f, at2.x, 1e-3f);
            Assert.AreEqual(at1.y * 2f, at2.y, 1e-3f);
        }

        [Test]
        public void LatLonAt_IsTheInverseOfOffset()
        {
            // The pan gesture depends on this: it knows how far the finger moved in pixels and
            // needs the new centre to re-fetch with.
            foreach (var p in new (double lat, double lon)[]
                     { (35.695488, 139.7811596), (35.681236, 139.767125), (35.690, 139.625) })
            {
                Vector2 o = MapProjection.Offset(CentreLat, CentreLon, p.lat, p.lon, Zoom, Scale);
                MapProjection.LatLonAt(CentreLat, CentreLon, o, Zoom, Scale,
                                       out double lat, out double lon);
                Assert.AreEqual(p.lat, lat, 1e-6, "lat round trip");
                Assert.AreEqual(p.lon, lon, 1e-6, "lon round trip");
            }
        }

        [Test]
        public void PolarLatitudes_ClampInsteadOfDivergingToInfinity()
        {
            // A venue row with a bad latitude must land at the edge of the map, not throw inside
            // a paint loop over 50 pins.
            Vector2 north = MapProjection.Offset(0, 0, 89.999, 0, Zoom, Scale);
            Vector2 south = MapProjection.Offset(0, 0, -89.999, 0, Zoom, Scale);
            Assert.IsFalse(float.IsNaN(north.y) || float.IsInfinity(north.y));
            Assert.IsFalse(float.IsNaN(south.y) || float.IsInfinity(south.y));
            Assert.AreEqual(MapProjection.Offset(0, 0, MapProjection.MaxLatitude, 0, Zoom, Scale).y,
                            north.y, 1e-3f);
        }

        [Test]
        public void IsVisible_CountsHalfAPinAsVisible()
        {
            // A pin whose CENTRE is just off the edge still has half of itself on screen; hiding
            // it would make markers pop rather than slide as the map pans.
            Assert.IsTrue(MapProjection.IsVisible(new Vector2(459f + 20f, 0f), 918f, 420f, 22f));
            Assert.IsFalse(MapProjection.IsVisible(new Vector2(459f + 24f, 0f), 918f, 420f, 22f));
            Assert.IsTrue(MapProjection.IsVisible(Vector2.zero, 918f, 420f));
        }

        [Test]
        public void MetresPerPixel_ShrinksWithZoomAndWithLatitude()
        {
            double atTokyo13 = MapProjection.MetresPerPixel(35.65, 13, 2f);
            double atTokyo14 = MapProjection.MetresPerPixel(35.65, 14, 2f);
            double atEquator13 = MapProjection.MetresPerPixel(0, 13, 2f);

            Assert.AreEqual(atTokyo13 / 2.0, atTokyo14, 1e-6, "one zoom step halves the ground scale");
            Assert.Less(atTokyo13, atEquator13, "Mercator stretches away from the equator");

            // Sanity, in units a human can check: at zoom 13 @2x around Tokyo a 500 m venue
            // radius is a few tens of pixels, not a few or a few thousand.
            double radiusPx = 500.0 / atTokyo13;
            Assert.Greater(radiusPx, 20.0);
            Assert.Less(radiusPx, 200.0);
        }
    }
}
