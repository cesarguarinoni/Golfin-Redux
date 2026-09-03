// ─────────────────────────────────────────────────────────────────────────────
// gps_checkin §C4 — where a pin goes on the map tile.
//
// D5: the Rounds panel shows a REAL map, fetched as ONE raster tile from
// /venue/map (Static Maps behind a server proxy), and every pin, the player dot
// and the legend are OURS, drawn on top of it. That only works if the client
// can put a (lat, lon) on the exact pixel Google drew it at — which is what this
// file is, and why it is a pure static helper with EditMode tests rather than
// arithmetic inlined into the screen controller.
//
// THE PROJECTION IS NOT A CHOICE. Static Maps renders Web Mercator (EPSG:3857)
// on a 256 px tile grid: at zoom z the whole world is 256·2^z pixels wide, and
// latitude maps through the Gudermannian. Any other formula puts pins near the
// centre roughly right and pins at the edge visibly wrong — the failure that is
// hardest to see in a screenshot and easiest to see on a phone.
//
// SCALE IS SEPARATE FROM ZOOM, and conflating them is the trap. /venue/map asks
// Google for `size=459x210&scale=2`, which returns a 918x420 IMAGE covering the
// geographic area of a 459x210 one. So the projection runs in Google pixels and
// the result is multiplied by `scale` to reach the RawImage's own pixels. Zoom
// changes what the map covers; scale changes only how many pixels it is drawn
// with.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// Web Mercator, in the exact form Google Static Maps uses. Pure and static: an EditMode test
    /// pins it against three known points, and nothing here touches a scene.
    /// </summary>
    public static class MapProjection
    {
        /// <summary>Google's tile edge in pixels. The whole world is <c>TileSize · 2^zoom</c>
        /// pixels wide at zoom <c>zoom</c>.</summary>
        public const int TileSize = 256;

        /// <summary>
        /// Web Mercator clamps at ±85.051129°, where the Gudermannian would run to infinity.
        /// Latitudes beyond it are clamped rather than rejected: a venue row with a bad latitude
        /// should land at the edge of the map, not throw inside a paint loop.
        /// </summary>
        public const double MaxLatitude = 85.05112878;

        /// <summary>The default zoom the Rounds panel opens at (§C4), and the bounds the pinch
        /// gesture is allowed to move it between.</summary>
        public const int DefaultZoom = 13;
        public const int MinZoom = 13;
        public const int MaxZoom = 16;

        /// <summary>
        /// A point in the world pixel plane at <paramref name="zoom"/>. X grows EAST, Y grows
        /// SOUTH — the image convention, which is why <see cref="Offset"/> negates Y to reach
        /// Unity's Y-up rect space.
        /// </summary>
        public static Vector2d WorldPixels(double lat, double lon, int zoom)
        {
            double n = TileSize * Math.Pow(2, Math.Max(0, zoom));
            double clamped = lat > MaxLatitude ? MaxLatitude : (lat < -MaxLatitude ? -MaxLatitude : lat);
            double s = Math.Sin(clamped * Math.PI / 180.0);
            double x = (lon + 180.0) / 360.0 * n;
            double y = (0.5 - Math.Log((1.0 + s) / (1.0 - s)) / (4.0 * Math.PI)) * n;
            return new Vector2d(x, y);
        }

        /// <summary>
        /// Where to place a marker for (<paramref name="lat"/>, <paramref name="lon"/>) on a tile
        /// centred on (<paramref name="centerLat"/>, <paramref name="centerLon"/>), as an offset
        /// in the RawImage's own pixels from its CENTRE, Y up.
        ///
        /// <para><paramref name="scale"/> is the Static Maps <c>scale</c> parameter — 2 for the
        /// Rounds panel, which asks for a half-size image at 2× and draws it at full size.</para>
        /// </summary>
        public static Vector2 Offset(double centerLat, double centerLon,
                                     double lat, double lon,
                                     int zoom, float scale = 2f)
        {
            Vector2d c = WorldPixels(centerLat, centerLon, zoom);
            Vector2d p = WorldPixels(lat, lon, zoom);
            return new Vector2((float)((p.X - c.X) * scale),
                               (float)(-(p.Y - c.Y) * scale));
        }

        /// <summary>
        /// The inverse: what (lat, lon) sits under an offset from the tile centre. Used by the
        /// drag-to-pan gesture, which knows how far the finger moved in pixels and needs the new
        /// centre to re-fetch with.
        /// </summary>
        public static void LatLonAt(double centerLat, double centerLon,
                                    Vector2 offsetPx, int zoom, float scale,
                                    out double lat, out double lon)
        {
            double n = TileSize * Math.Pow(2, Math.Max(0, zoom));
            Vector2d c = WorldPixels(centerLat, centerLon, zoom);
            double x = c.X + offsetPx.x / Math.Max(0.0001f, scale);
            double y = c.Y - offsetPx.y / Math.Max(0.0001f, scale);

            lon = x / n * 360.0 - 180.0;
            double m = 0.5 - y / n;
            lat = 90.0 - 360.0 * Math.Atan(Math.Exp(-m * 2.0 * Math.PI)) / Math.PI;
        }

        /// <summary>
        /// Whether a marker at <paramref name="offset"/> is worth drawing on a surface of
        /// <paramref name="width"/> × <paramref name="height"/>.
        ///
        /// <para><paramref name="margin"/> is half the marker's own size: a pin whose CENTRE is
        /// just off the edge still has half of itself on screen, and hiding it would make markers
        /// pop rather than slide as the map pans.</para>
        /// </summary>
        public static bool IsVisible(Vector2 offset, float width, float height, float margin = 0f)
            => Mathf.Abs(offset.x) <= width * 0.5f + margin &&
               Mathf.Abs(offset.y) <= height * 0.5f + margin;

        /// <summary>
        /// Metres per RawImage pixel at a given latitude and zoom — the number that turns a
        /// venue's <c>gps_radius_m</c> into a radius on screen.
        /// </summary>
        public static double MetresPerPixel(double lat, int zoom, float scale = 2f)
        {
            double clamped = lat > MaxLatitude ? MaxLatitude : (lat < -MaxLatitude ? -MaxLatitude : lat);
            return 156543.03392 * Math.Cos(clamped * Math.PI / 180.0)
                   / Math.Pow(2, Math.Max(0, zoom)) / Math.Max(0.0001f, scale);
        }

        /// <summary>
        /// A double-precision 2-vector. <see cref="Vector2"/> is float, and world pixels at zoom
        /// 16 run past 16 million — where a float's 24-bit mantissa is already quantising to
        /// whole pixels. The conversion to float happens ONCE, on the small offset, after the
        /// subtraction that removes the large common part.
        /// </summary>
        public readonly struct Vector2d
        {
            public readonly double X;
            public readonly double Y;

            public Vector2d(double x, double y) { X = x; Y = y; }

            public override string ToString() => $"({X:F6}, {Y:F6})";
        }
    }
}
