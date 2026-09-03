// Order: reward_points_backend Slice 1 — {data:…} envelope unwrapping + {detail:…} error extraction.
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Golfin.Net
{
    /// <summary>
    /// PLAYLIFE wraps every successful <c>/api/v1</c> payload in <c>{"data": …}</c> and every FastAPI
    /// error in <c>{"detail": "…"}</c> (both verified against the live deployment 2026-08-12).
    ///
    /// Newtonsoft is used rather than <c>JsonUtility</c> because the payloads are snake_case and
    /// JsonUtility has no field-name mapping — the same reason <c>Golfin.Save</c> took the dependency.
    /// </summary>
    public static class ApiEnvelope
    {
        /// <summary>
        /// Unwraps <c>{"data": X}</c> to X and deserialises it into <typeparamref name="T"/>.
        ///
        /// A body that is NOT an object with a <c>data</c> key is deserialised as-is. That is what makes
        /// the root-mounted <c>/health</c> (<c>{"status":"ok","version":"0.1.0"}</c>, no envelope) work
        /// through the same path as the enveloped endpoints.
        ///
        /// An empty body succeeds with <c>default(T)</c> — a 204/empty 200 is not a parse failure.
        /// </summary>
        public static bool TryUnwrap<T>(string body, out T data, out string error)
        {
            data = default;
            error = null;

            if (string.IsNullOrWhiteSpace(body)) return true;

            try
            {
                JToken root = ParseRaw(body);

                JToken payload = root;
                if (root.Type == JTokenType.Object)
                {
                    JToken inner = ((JObject)root)["data"];
                    if (inner != null) payload = inner;
                }

                if (payload == null || payload.Type == JTokenType.Null) return true;

                // string T means "hand me the payload verbatim" — used for probes and diagnostics.
                if (typeof(T) == typeof(string))
                {
                    string s = payload.Type == JTokenType.String
                        ? payload.Value<string>()
                        : payload.ToString(Formatting.None);
                    data = (T)(object)s;
                    return true;
                }

                data = payload.ToObject<T>();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Pulls the human-readable message out of a FastAPI error body
        /// (<c>{"detail":"Authentication failed: …"}</c>), falling back to the raw body.
        /// <c>detail</c> can also be a validation ARRAY on 422, which is stringified rather than dropped.
        /// </summary>
        public static string ExtractErrorMessage(string body, string fallback = null)
        {
            if (string.IsNullOrWhiteSpace(body)) return fallback;

            try
            {
                JToken root = ParseRaw(body);
                if (root.Type != JTokenType.Object) return Shorten(body);

                var obj = (JObject)root;
                JToken detail = obj["detail"] ?? obj["message"] ?? obj["error"];
                if (detail == null) return Shorten(body);

                return detail.Type == JTokenType.String
                    ? detail.Value<string>()
                    : Shorten(detail.ToString(Formatting.None));
            }
            catch
            {
                return Shorten(body);
            }
        }

        /// <summary>
        /// Parse a body WITHOUT Newtonsoft's date handling.
        ///
        /// <para>Newtonsoft's default <see cref="DateParseHandling.DateTime"/> rewrites any
        /// ISO-8601-looking string into a <c>DateTime</c> token in the DEVICE'S LOCAL ZONE. A DTO
        /// field typed <c>string</c> then receives that token's <c>ToString()</c> — so the server's
        /// <c>"2026-09-03T03:26:19+00:00"</c> reaches the field as <c>"09/03/2026 12:26:19"</c>:
        /// local wall-clock, US format, and NO offset. Anything that parses it back as UTC shifts a
        /// second time. That is exactly how a round checked in at 12:26 JST rendered "Since 21:26".</para>
        ///
        /// <para>Timestamps here are carried as strings on purpose and parsed once, deliberately, at
        /// the point of use (<c>RoundSession.ParseTimestamp</c>). This keeps them verbatim so that
        /// parse is the ONLY one.</para>
        /// </summary>
        private static JToken ParseRaw(string body)
        {
            using (var sr = new StringReader(body))
            using (var reader = new JsonTextReader(sr) { DateParseHandling = DateParseHandling.None })
                return JToken.ReadFrom(reader);
        }

        private static string Shorten(string s, int max = 400)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
