#nullable enable
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GolfinRedux.UI.MissionSelection
{
    /// <summary>
    /// World (x, z) → normalised position on a hole's thumbnail illustration.
    ///
    /// WHY THIS IS A LOOKUP TABLE AND NOT MATHS. `Resources/HoleImages/…/Hole_NN.png` are
    /// STYLISED top-down illustrations, not renders of the tracked geometry, so no single
    /// transform recovers them. Four hypotheses were tested against all 18 holes and every one
    /// failed: axis-aligned zone bounds, an oriented box along tee→green, the same over the play
    /// surface only, and a hidden-margin (opaque-bbox) variant.
    ///
    /// What DOES hold is a per-hole similarity transform anchored on two features that can be
    /// located in both spaces — the putting green and the tee. `Docs/Scripts/bake_hole_map_calibration.py`
    /// finds them in the art by segmenting mown grass, pairs them with the world centroids from
    /// `zones.json`, and writes the transform here.
    ///
    /// HOW GOOD IS IT. Measured by projecting every bunker in `zones.json` and comparing against
    /// the bunkers drawn in the art — an independent third feature the fit never saw:
    ///
    ///     along the hole   6.3% of hole length (median)
    ///     across the hole 10.6%
    ///
    /// Which is why this is a START MARKER and not a lie detector: it is good enough to say
    /// "you begin about two thirds down the fairway", and nowhere near good enough to measure by.
    ///
    /// Holes whose along-axis error exceeds 12% carry `ok=0` and get NO marker — the words
    /// ("Fairway approach", "On the green") are better than a dot in the wrong place. Today that
    /// is holes 4, 6, 11 and 15, all par 3s, where the start is the tee anyway.
    /// </summary>
    public static class HoleMapCalibration
    {
        private struct Fit
        {
            public bool  Ok;
            public float TeeWorldX, TeeWorldZ;   // world anchor
            public float TeeU, TeeV;             // same point, normalised on the image
            public float PxPerMetre;             // image pixels per world metre
            public float RotRad;                 // world→image rotation
            public float ImgW, ImgH;
        }

        private static Dictionary<int, Fit>? _fits;

        private static void EnsureLoaded()
        {
            if (_fits != null) return;
            _fits = new Dictionary<int, Fit>();
            var csv = Resources.Load<TextAsset>("Data/hole_map_calibration");
            if (csv == null) { Debug.LogWarning("[HoleMapCalibration] table missing — no start markers."); return; }

            string[] lines = csv.text.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                string[] c = line.Split(',');
                if (c.Length < 10) continue;
                if (!int.TryParse(c[0], out int hole)) continue;
                var f = new Fit { Ok = c[1] == "1" };
                if (f.Ok)
                {
                    f.TeeWorldX  = P(c[2]); f.TeeWorldZ = P(c[3]);
                    f.TeeU       = P(c[4]); f.TeeV      = P(c[5]);
                    f.ImgW       = P(c[6]); f.ImgH      = P(c[7]);
                    f.PxPerMetre = P(c[8]); f.RotRad    = P(c[9]);
                }
                _fits[hole] = f;
            }
        }

        private static float P(string s) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;

        /// <summary>
        /// Where a world point sits on the hole's thumbnail, as (0..1, 0..1) with V measured from
        /// the BOTTOM (Unity anchor convention; the bake works in image space, where Y runs down).
        /// Null when the hole has no trustworthy fit, or the point lands outside the art.
        /// </summary>
        public static Vector2? Normalised(int holeNumber, Vector3 world)
        {
            EnsureLoaded();
            if (_fits == null || !_fits.TryGetValue(holeNumber, out var f) || !f.Ok) return null;

            float dx = world.x - f.TeeWorldX;
            float dz = world.z - f.TeeWorldZ;
            float ca = Mathf.Cos(f.RotRad), sa = Mathf.Sin(f.RotRad);

            float u = f.TeeU + f.PxPerMetre * (dx * ca - dz * sa) / f.ImgW;
            float vTop = f.TeeV + f.PxPerMetre * (dx * sa + dz * ca) / f.ImgH;

            // A marker outside the drawing is worse than none: it would sit on the card's
            // background with nothing to relate to.
            if (u < 0f || u > 1f || vTop < 0f || vTop > 1f) return null;
            return new Vector2(u, 1f - vTop);
        }
    }
}
