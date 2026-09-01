// Order: gps_trust_core §Tests — the encoder must agree with backend/routers/venue.py::_geohash_encode.
// The precision-9 vector below was produced by running that exact Python function on the same input.
using System.Collections.Generic;
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class GeohashTests
    {
        // Tokyo Station.
        private const double Lat = 35.681236;
        private const double Lon = 139.767125;

        [Test]
        public void Encode_MatchesTheBackendVector()
        {
            Assert.AreEqual("xn76urx6606p", Geohash.Encode(Lat, Lon, 12));
            Assert.AreEqual("xn76urx66", Geohash.Encode(Lat, Lon, 9),
                "venue.py::_geohash_encode defaults to precision 9 — this is the string /venue/nearby scans");
            Assert.AreEqual("xn76", Geohash.Encode(Lat, Lon, 4));
        }

        [Test]
        public void Encode_PrefixesAreStable()
        {
            string full = Geohash.Encode(Lat, Lon, 12);
            for (int p = 1; p <= 12; p++)
                Assert.AreEqual(full.Substring(0, p), Geohash.Encode(Lat, Lon, p), "precision " + p);
        }

        [Test]
        public void Encode_UsesTheBase32AlphabetTheBackendUses()
        {
            Assert.AreEqual("0123456789bcdefghjkmnpqrstuvwxyz", Geohash.Base32);
        }

        [Test]
        public void Neighbors_AreEightDistinctCellsOfTheSameLength()
        {
            List<string> n = Geohash.Neighbors("xn76");

            Assert.AreEqual(8, n.Count);
            CollectionAssert.AllItemsAreUnique(n);
            CollectionAssert.DoesNotContain(n, "xn76", "the cell itself is not one of its neighbours");
            foreach (string h in n) Assert.AreEqual(4, h.Length, h);
        }

        [Test]
        public void Neighbors_AreActuallyAdjacent()
        {
            // Every neighbour's own neighbour set must contain the origin cell.
            foreach (string h in Geohash.Neighbors("xn76"))
                CollectionAssert.Contains(Geohash.Neighbors(h), "xn76", h + " should border xn76");
        }

        [Test]
        public void Neighbors_OfAnInvalidHashIsEmptyRatherThanAThrow()
        {
            Assert.AreEqual(0, Geohash.Neighbors("xn7!").Count);
            Assert.AreEqual(0, Geohash.Neighbors(null).Count);
        }

        [Test]
        public void NearbyPrefixes_IsNineCellsWithTheSelfCellLast()
        {
            string prefixes = Geohash.NearbyPrefixes(Lat, Lon);
            string[] parts = prefixes.Split(',');

            Assert.AreEqual(9, parts.Length, prefixes);
            CollectionAssert.Contains(parts, "xn76");
            Assert.AreEqual("xn76", parts[8], "Dart order is neighbours first, then the cell itself");
            CollectionAssert.AllItemsAreUnique(parts);
        }
    }
}
