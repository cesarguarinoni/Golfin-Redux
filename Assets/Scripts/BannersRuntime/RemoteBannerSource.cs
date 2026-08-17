// ─────────────────────────────────────────────────────────────────────────────
// BannersRuntime — RemoteBannerSource
// Fetches the live banner set over ApiClient and mirrors the RAW response body
// to disk. Shape-for-shape the same as RemoteTournamentSource, for the same
// reasons: cache the raw body (not a mapped view), write it atomically, and
// hand back null on ANY failure so the caller keeps what it already has.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.IO;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Banners
{
    /// <summary>Where the banner set the game is currently showing came from.</summary>
    public enum BannerSource
    {
        /// <summary>Nothing fetched and nothing cached — every slot shows its bundled sprite.</summary>
        None,
        /// <summary>The JSON mirrored to disk by a previous successful fetch.</summary>
        DiskCache,
        /// <summary>A fetch that landed this session.</summary>
        Server,
    }

    public static class RemoteBannerSource
    {
        private const string Tag = "[Banners]";
        public const string CacheFileName = "game_banners.json";

        /// <summary>
        /// <c>&lt;persistentDataPath&gt;/game_banners.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.
        /// </summary>
        public static string CachePath => Path.Combine(Application.persistentDataPath, CacheFileName);

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>The cached raw body, or null when there is no cache / it is unreadable.</summary>
        public static string? ReadCache()
        {
            try
            {
                string path = CachePath;
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not read the banner cache: {ex.Message}");
                return null;
            }
        }

        // ── Write (atomic — .tmp + replace) ───────────────────────────────────

        /// <summary>
        /// Mirror the raw body to disk via <c>.tmp</c> + replace, so a kill mid-write leaves the
        /// previous good cache intact rather than a truncated file that fails to parse on next boot.
        /// </summary>
        public static void WriteCache(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            string path = CachePath;
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);

                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // A cache we could not write is a slower next boot, not a broken session.
                Debug.LogWarning($"{Tag} Could not write the banner cache '{path}': {ex.Message}");
            }
        }

        /// <summary>Test/debug helper — drop the cache so the next boot starts from bundled art.</summary>
        public static void ClearCache()
        {
            try { if (File.Exists(CachePath)) File.Delete(CachePath); }
            catch (Exception ex) { Debug.LogWarning($"{Tag} Could not delete the banner cache: {ex.Message}"); }
        }

        // ── Fetch ─────────────────────────────────────────────────────────────

        /// <summary>
        /// GET the live banner set. On success the RAW body is mirrored to disk BEFORE mapping, so
        /// even a payload this build cannot map is available to a later build that can.
        /// <paramref name="onDone"/> receives null on any failure — the caller keeps its current set,
        /// which at worst means every slot keeps its bundled sprite.
        /// </summary>
        public static IEnumerator FetchRoutine(Action<string?> onDone)
        {
            string url = Endpoints.Banners;
            string? body = null;

            // T = string asks ApiEnvelope for the unwrapped payload verbatim; ApiResult.RawBody
            // still carries the full enveloped body, which is what we cache.
            IEnumerator get = ApiClient.Instance.Get<string>(url, result =>
            {
                if (result.Success)
                {
                    body = result.RawBody;
                }
                else
                {
                    // Expected on a cold launch in airplane mode. Warning, not error: falling back
                    // to the bundled sprite is a designed path, not a malfunction.
                    Debug.LogWarning(
                        $"{Tag} Banner fetch failed ({result.ErrorKind}, HTTP {result.StatusCode}): " +
                        $"{result.ErrorMessage}. Keeping the cached/bundled banners.");
                }
            });

            // Explicit pump, matching ApiClient's own convention, so this routine also works when
            // driven by a plain while(MoveNext()) in an EditMode test.
            while (get.MoveNext()) yield return get.Current;

            if (!string.IsNullOrWhiteSpace(body)) WriteCache(body!);

            onDone?.Invoke(body);
        }
    }
}
