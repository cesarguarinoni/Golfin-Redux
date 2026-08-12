// Order: reward_points_backend Slice 1 — {data:…} envelope unwrapping + {detail:…} error extraction.
using System;
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
                JToken root = JToken.Parse(body);

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
                JToken root = JToken.Parse(body);
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

        private static string Shorten(string s, int max = 400)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}
