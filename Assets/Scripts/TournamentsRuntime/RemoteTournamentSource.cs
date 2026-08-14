// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — RemoteTournamentSource
// Fetches the schedule over ApiClient and mirrors the RAW response body to disk.
//
// Source precedence: live fetch → cached JSON → shipped CSV (SPEC §4.2).
// Server data replaces CSV data WHOLESALE or not at all — never a merge. A
// half-server/half-CSV schedule is unreproducible in a bug report (SPEC §3 D3).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.IO;
using Golfin.Net;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>Where the schedule the game is currently showing came from.</summary>
    public enum ScheduleSource
    {
        /// <summary>The shipped <c>Resources/Data/tournaments.csv</c>.</summary>
        BundledCsv,
        /// <summary>The JSON mirrored to disk by a previous successful fetch.</summary>
        DiskCache,
        /// <summary>A fetch that landed this session.</summary>
        Server,
    }

    public static class RemoteTournamentSource
    {
        private const string Tag = "[TournamentSchedule]";
        public const string CacheFileName = "tournaments_schedule.json";

        /// <summary>
        /// <c>&lt;persistentDataPath&gt;/tournaments_schedule.json</c>.
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
                Debug.LogWarning($"{Tag} Could not read the schedule cache: {ex.Message}");
                return null;
            }
        }

        // ── Write (atomic — same pattern as Economy/PendingOpsStore.Write) ────

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
                Debug.LogWarning($"{Tag} Could not write the schedule cache '{path}': {ex.Message}");
            }
        }

        /// <summary>Test/debug helper — drop the cache so the next boot takes the CSV path.</summary>
        public static void ClearCache()
        {
            try { if (File.Exists(CachePath)) File.Delete(CachePath); }
            catch (Exception ex) { Debug.LogWarning($"{Tag} Could not delete the schedule cache: {ex.Message}"); }
        }

        // ── Fetch ─────────────────────────────────────────────────────────────

        /// <summary>
        /// GET the schedule. On success the RAW body is mirrored to disk BEFORE mapping, so even a
        /// payload this build cannot map is available to a later build that can.
        /// <paramref name="onDone"/> receives null on any failure — the caller keeps its current source.
        /// </summary>
        public static IEnumerator FetchRoutine(Action<string?> onDone)
        {
            string url = Endpoints.TournamentsGolfin;
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
                    // Expected on a cold launch in airplane mode. Warning, not error: the CSV
                    // fallback below is a designed path, not a malfunction.
                    Debug.LogWarning(
                        $"{Tag} Schedule fetch failed ({result.ErrorKind}, HTTP {result.StatusCode}): " +
                        $"{result.ErrorMessage}. Falling back to the cached/bundled schedule.");
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
