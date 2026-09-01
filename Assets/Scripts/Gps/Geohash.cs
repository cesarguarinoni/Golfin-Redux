// Order: gps_trust_core §5 — standard geohash, replacing the Dart `dart_geohash` package.
// The encoder MUST agree character-for-character with backend/routers/venue.py::_geohash_encode:
// /venue/nearby is a `like 'prefix%'` scan over the geohash the SERVER wrote, so a one-character
// disagreement returns an empty venue list rather than an error anyone would notice.
using System.Collections.Generic;
using System.Text;

namespace Golfin.Gps
{
    public static class Geohash
    {
        /// <summary>Base32 alphabet, identical to <c>venue.py::_GEOHASH_BASE32</c>.</summary>
        public const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

        /// <summary>Precision <c>/venue/nearby</c> prefixes are cut to (Dart
        /// <c>geohash.substring(0, 4)</c>) — roughly a 40 km cell.</summary>
        public const int NearbyPrefixPrecision = 4;

        /// <summary>
        /// Encode a coordinate. Bit order and the <c>&gt;=</c> midpoint comparison are transcribed
        /// from <c>venue.py::_geohash_encode</c>: longitude first, then alternating, <c>lon &gt;= mid</c>
        /// takes the upper half. Verified against it at precision 4 / 9 / 12 by GeohashTests.
        /// </summary>
        public static string Encode(double lat, double lon, int precision = 12)
        {
            if (precision < 1) precision = 1;

            double latLo = -90.0, latHi = 90.0;
            double lonLo = -180.0, lonHi = 180.0;

            var chars = new StringBuilder(precision);
            int bit = 0;
            int ch = 0;
            bool even = true;   // true: longitude bit, false: latitude bit

            while (chars.Length < precision)
            {
                if (even)
                {
                    double mid = (lonLo + lonHi) / 2;
                    if (lon >= mid) { ch = (ch << 1) | 1; lonLo = mid; }
                    else            { ch = ch << 1;       lonHi = mid; }
                }
                else
                {
                    double mid = (latLo + latHi) / 2;
                    if (lat >= mid) { ch = (ch << 1) | 1; latLo = mid; }
                    else            { ch = ch << 1;       latHi = mid; }
                }

                even = !even;
                bit++;
                if (bit == 5)
                {
                    chars.Append(Base32[ch]);
                    bit = 0;
                    ch = 0;
                }
            }

            return chars.ToString();
        }

        /// <summary>The cell's bounding box. Returns false for a hash containing a non-base32 char.</summary>
        public static bool TryDecodeBounds(string hash, out double latMin, out double latMax,
                                           out double lonMin, out double lonMax)
        {
            latMin = -90.0; latMax = 90.0;
            lonMin = -180.0; lonMax = 180.0;
            if (string.IsNullOrEmpty(hash)) return false;

            bool even = true;
            foreach (char raw in hash)
            {
                int idx = Base32.IndexOf(char.ToLowerInvariant(raw));
                if (idx < 0) return false;

                for (int b = 4; b >= 0; b--)
                {
                    int bit = (idx >> b) & 1;
                    if (even)
                    {
                        double mid = (lonMin + lonMax) / 2;
                        if (bit == 1) lonMin = mid; else lonMax = mid;
                    }
                    else
                    {
                        double mid = (latMin + latMax) / 2;
                        if (bit == 1) latMin = mid; else latMax = mid;
                    }
                    even = !even;
                }
            }
            return true;
        }

        /// <summary>
        /// The 8 surrounding cells at the same precision, in N, NE, E, SE, S, SW, W, NW order.
        ///
        /// Computed by stepping one cell width from the centre and re-encoding rather than by the
        /// classic border/neighbour lookup tables — same answers, one algorithm to keep in step with
        /// <see cref="Encode"/>. Longitude wraps at the antimeridian; latitude clamps at the poles.
        /// </summary>
        public static List<string> Neighbors(string hash)
        {
            var result = new List<string>(8);
            if (!TryDecodeBounds(hash, out double latMin, out double latMax, out double lonMin, out double lonMax))
                return result;

            int precision = hash.Length;
            double latSpan = latMax - latMin;
            double lonSpan = lonMax - lonMin;
            double centerLat = (latMin + latMax) / 2;
            double centerLon = (lonMin + lonMax) / 2;

            int[,] steps =
            {
                {  1,  0 },   // N
                {  1,  1 },   // NE
                {  0,  1 },   // E
                { -1,  1 },   // SE
                { -1,  0 },   // S
                { -1, -1 },   // SW
                {  0, -1 },   // W
                {  1, -1 }    // NW
            };

            for (int i = 0; i < steps.GetLength(0); i++)
            {
                double lat = ClampLat(centerLat + steps[i, 0] * latSpan);
                double lon = WrapLon(centerLon + steps[i, 1] * lonSpan);
                result.Add(Encode(lat, lon, precision));
            }

            return result;
        }

        /// <summary>
        /// The <c>prefixes=</c> value for <c>GET /venue/nearby</c>: the cell's 8 neighbours followed
        /// by the cell itself, comma-joined. Order is the Dart one
        /// (<c>neighbors(...).values.toList()..add(self)</c>); the server dedupes by venue id, so
        /// order is cosmetic — matched anyway so a diff against the Flutter client stays readable.
        /// </summary>
        public static string NearbyPrefixes(double lat, double lon, int precision = NearbyPrefixPrecision)
        {
            string self = Encode(lat, lon, precision);
            List<string> all = Neighbors(self);
            all.Add(self);
            return string.Join(",", all);
        }

        private static double ClampLat(double lat) => lat > 90.0 ? 90.0 : (lat < -90.0 ? -90.0 : lat);

        private static double WrapLon(double lon)
        {
            while (lon > 180.0) lon -= 360.0;
            while (lon < -180.0) lon += 360.0;
            return lon;
        }
    }
}
