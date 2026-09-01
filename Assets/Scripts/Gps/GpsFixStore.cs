// Order: gps_trust_core §1 — the persisted GPS fix log. Wire schema ported byte-for-byte from
// playlife/lib/common/presentation/controller/gps_session_tracker.dart (`_Fix.toJson`).
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// One recorded location fix.
    ///
    /// The JSON keys are NOT a style choice: the Dart client wrote
    /// <c>{'lat': …, 'lon': …, 't': …}</c> into shared_preferences and a player carrying a
    /// half-finished round across the Flutter→Unity boundary must find their own fixes still
    /// readable. <c>t</c> is Unix epoch MILLISECONDS (Dart <c>DateTime.millisecondsSinceEpoch</c>).
    /// </summary>
    public sealed class GpsFix
    {
        [JsonProperty("lat")] public double Lat;
        [JsonProperty("lon")] public double Lon;
        [JsonProperty("t")]   public long   T;

        public GpsFix() { }

        public GpsFix(double lat, double lon, long t)
        {
            Lat = lat;
            Lon = lon;
            T = t;
        }

        public override string ToString() => $"GpsFix({Lat:F6}, {Lon:F6} @ {T})";
    }

    /// <summary>
    /// Where the fix log lives. Two implementations ship: PlayerPrefs (the app) and in-memory
    /// (tests, and the standalone PLAYLIFE shell's editor harness later).
    /// </summary>
    public interface IGpsFixStore
    {
        /// <summary>NEVER null. A malformed or absent payload reads as an EMPTY list rather than an
        /// error — the Dart <c>_load</c> swallows parse failures the same way, because a corrupt log
        /// must not be able to block a score submit.</summary>
        List<GpsFix> Load();

        void Save(List<GpsFix> fixes);
    }

    /// <summary>Shared (de)serialisation so both stores speak exactly the same wire format.</summary>
    public static class GpsFixJson
    {
        public static string Serialize(List<GpsFix> fixes)
            => JsonConvert.SerializeObject(fixes ?? new List<GpsFix>());

        /// <summary>Never throws; anything unreadable is an empty log.</summary>
        public static List<GpsFix> Deserialize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return new List<GpsFix>();
            try
            {
                return JsonConvert.DeserializeObject<List<GpsFix>>(raw) ?? new List<GpsFix>();
            }
            catch (Exception)
            {
                return new List<GpsFix>();
            }
        }
    }

    /// <summary>
    /// Shipping store. Key <c>gps_session_fixes_v1</c> is the Dart <c>_prefsKey</c> verbatim.
    /// </summary>
    public sealed class PlayerPrefsGpsFixStore : IGpsFixStore
    {
        public const string PrefsKey = "gps_session_fixes_v1";

        public List<GpsFix> Load() => GpsFixJson.Deserialize(PlayerPrefs.GetString(PrefsKey, null));

        public void Save(List<GpsFix> fixes)
        {
            PlayerPrefs.SetString(PrefsKey, GpsFixJson.Serialize(fixes));
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// String-backed in-memory store. Deliberately holds the SERIALISED payload rather than the
    /// object graph, so a test exercises the same encoder the shipping store does and can assert the
    /// wire text (<see cref="Raw"/>) instead of post-parse equality.
    /// </summary>
    public sealed class InMemoryGpsFixStore : IGpsFixStore
    {
        /// <summary>The stored JSON. Settable so a test can plant a malformed payload.</summary>
        public string Raw;

        public int SaveCount;

        public InMemoryGpsFixStore(string raw = null) { Raw = raw; }

        public List<GpsFix> Load() => GpsFixJson.Deserialize(Raw);

        public void Save(List<GpsFix> fixes)
        {
            Raw = GpsFixJson.Serialize(fixes);
            SaveCount++;
        }
    }
}
