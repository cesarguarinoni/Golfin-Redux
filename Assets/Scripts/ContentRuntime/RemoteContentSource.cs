// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — RemoteContentSource
// Fetches the admin-published content delta over ApiClient and mirrors it to
// disk. Shape-for-shape the same as RemoteNoticeSource, for the same reasons:
// cache the body (not a mapped view), write it atomically, and hand back null
// on ANY failure so the caller keeps what it already has.
//
// ⚠️ ONE FILE PER CATALOG — content_texts.json, content_clubs.json, …
//
//   Phase 1 cached the WHOLE response body in content_texts.json, which was
//   correct while texts was the only catalog requested. Phase 2 asks for seven
//   in one round trip, so a whole-body mirror would put a 610 KB payload in
//   every catalog's file and, worse, make the §7 kill semantics unimplementable:
//   "drop THAT catalog's cache" needs that catalog to HAVE a cache of its own.
//
//   Each file holds a MINIMAL PAYLOAD ENVELOPE carrying exactly one catalog:
//       {"enabled":true,"catalogs":{"clubs":{"version":1,"full":true,"changed":[…]}}}
//   which is a valid RemoteContentDto, so ContentTextsMapper and
//   ContentCatalogMapper both read it unchanged — and a Phase-1 whole-body
//   content_texts.json ALSO still parses, so no player loses their text overlay
//   on the upgrade. The slice is stored VERBATIM, so unknown columns a later
//   build understands survive the round trip exactly as they did in Phase 1.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Golfin.Net;
using Newtonsoft.Json;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>Where the overlay the game is currently applying came from.</summary>
    public enum ContentSourceKind
    {
        /// <summary>Nothing cached — the bundled table stands alone. The normal fresh-install state.</summary>
        Bundled,
        /// <summary>The JSON mirrored to disk by a previous successful fetch.</summary>
        DiskCache,
    }

    /// <summary>
    /// Disk cache + fetch for the content catalogs. The cache file is PER CATALOG
    /// (<c>content_texts.json</c>, <c>content_clubs.json</c>, …) so a clubs payload that fails to
    /// map cannot cost the player their text overlay, and so §7's "drop that catalog's cache" has
    /// something per-catalog to drop.
    /// </summary>
    public static class RemoteContentSource
    {
        private const string Tag = "[Content]";

        /// <summary>
        /// The texts catalog name. Kept as its own constant because Phase 1 code and tests name it
        /// here; <see cref="ContentCatalogs"/> is the full list.
        /// </summary>
        public const string TextsCatalog = ContentCatalogs.Texts;

        public const string TextsCacheFileName = "content_texts.json";

        /// <summary>
        /// <c>&lt;persistentDataPath&gt;/content_texts.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.
        /// </summary>
        public static string TextsCachePath => CachePath(TextsCatalog);

        /// <summary><c>content_&lt;catalog&gt;.json</c> — the file name only, no directory.</summary>
        public static string CacheFileName(string catalog) => "content_" + Sanitise(catalog) + ".json";

        /// <summary>
        /// <c>&lt;persistentDataPath&gt;/content_&lt;catalog&gt;.json</c>.
        /// Touches <c>Application.persistentDataPath</c>, so main thread only.
        /// </summary>
        public static string CachePath(string catalog)
            => Path.Combine(Application.persistentDataPath, CacheFileName(catalog));

        /// <summary>
        /// Catalog names come from <see cref="ContentCatalogs"/>, never from the wire — but a name
        /// reaching a file path is worth one line of paranoia regardless, and it also normalises
        /// the case so <c>Texts</c> and <c>texts</c> cannot end up in two different files.
        /// </summary>
        private static string Sanitise(string? catalog)
        {
            if (string.IsNullOrWhiteSpace(catalog)) return "unknown";
            var sb = new System.Text.StringBuilder(catalog!.Length);
            foreach (char c in catalog.Trim().ToLowerInvariant())
                sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
            return sb.ToString();
        }

        /// <summary>
        /// Wrap one catalog's slice in a minimal payload envelope, so the cached file is a valid
        /// <see cref="RemoteContentDto"/> that both mappers read unchanged. The slice JSON is
        /// embedded VERBATIM — unknown fields inside it survive for a later build.
        /// </summary>
        public static string Envelope(string catalog, string sliceJson)
            => "{\"enabled\":true,\"catalogs\":{" +
               JsonConvert.ToString(catalog) + ":" + sliceJson + "}}";

        // ── Read ──────────────────────────────────────────────────────────────

        /// <summary>The cached texts body, or null when there is no cache / it is unreadable.</summary>
        public static string? ReadCache() => ReadCache(TextsCatalog);

        /// <summary>One catalog's cached body, or null when there is no cache / it is unreadable.</summary>
        public static string? ReadCache(string catalog)
        {
            try
            {
                string path = CachePath(catalog);
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception ex)
            {
                // Warning, not error: no cache is the fresh-install state, and an unreadable one
                // just means this launch is bundled-only. Neither is a malfunction.
                Debug.LogWarning($"{Tag} Could not read the '{catalog}' cache: {ex.Message}");
                return null;
            }
        }

        // ── Write (atomic — .tmp + replace) ───────────────────────────────────

        /// <summary>
        /// Mirror the raw body to disk via <c>.tmp</c> + replace, so a kill mid-write leaves the
        /// previous good cache intact rather than a truncated file that fails to parse on next boot.
        /// </summary>
        public static void WriteCache(string json) => WriteCache(TextsCatalog, json);

        /// <summary>Mirror one catalog's body to disk. Same atomic .tmp + replace as above.</summary>
        public static void WriteCache(string catalog, string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;

            string path = CachePath(catalog);
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
                // A cache we could not write is a bundled-only next boot, not a broken session.
                Debug.LogWarning($"{Tag} Could not write the '{catalog}' cache '{path}': {ex.Message}");
            }
        }

        /// <summary>
        /// Drop the cache so the next launch is bundled-only. This is the kill switch's teeth
        /// (§7) as well as a test/debug helper — <c>enabled:false</c> must fully undo remote text,
        /// and a cache left on disk would keep re-applying it forever with no network required.
        /// </summary>
        public static void ClearCache() => ClearCache(TextsCatalog);

        /// <summary>Drop ONE catalog's cache. The per-catalog half of the kill switch (§7).</summary>
        public static void ClearCache(string catalog)
        {
            try
            {
                string path = CachePath(catalog);
                if (File.Exists(path)) File.Delete(path);
                // The .tmp only exists if a previous write was killed mid-flight; leaving it would
                // be harmless but it is the same generation of data, so it goes too.
                string tmp = path + ".tmp";
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not delete the '{catalog}' cache: {ex.Message}");
            }
        }

        /// <summary>Drop EVERY catalog's cache. The global kill switch's teeth (§7).</summary>
        public static void ClearAllCaches()
        {
            foreach (string catalog in ContentCatalogs.All) ClearCache(catalog);
        }

        // ── Fetch ─────────────────────────────────────────────────────────────

        /// <summary>
        /// GET the texts delta for <paramref name="sinceVersion"/> / <paramref name="build"/>.
        /// On success the RAW body is handed back so the caller can mirror it to disk BEFORE
        /// mapping — a payload this build cannot map is then still available to a later build
        /// that can.
        /// <para>
        /// <paramref name="onDone"/> receives null on any failure. That is a <b>warning, not an
        /// error</b>: a cold launch in airplane mode is a designed path, and the caller simply
        /// keeps the bundled strings (or the previous cache).
        /// </para>
        /// <para>
        /// Unlike <c>RemoteNoticeSource.FetchRoutine</c> this does NOT write the cache itself.
        /// The kill switch has to be able to REJECT a body and delete the cache instead of storing
        /// it, and that decision needs the parsed payload — so the write lives one level up, in
        /// <see cref="ContentService"/>.
        /// </para>
        /// </summary>
        public static IEnumerator FetchRoutine(int sinceVersion, int build, Action<string?> onDone)
        {
            // Per-catalog cursor form — "texts:11". A bare int would apply to every catalog, which
            // is exactly the lossy scalar content_cursor_per_catalog exists to remove.
            var cursors = new Dictionary<string, int> { { TextsCatalog, sinceVersion } };
            IEnumerator inner = FetchRoutine(cursors, build, new[] { TextsCatalog }, onDone);
            while (inner.MoveNext()) yield return inner.Current;
        }

        /// <summary>
        /// GET the delta for SEVERAL catalogs in ONE round trip.
        /// <para>
        /// One request, not seven: the boot path already pays for a socket, and seven of them would
        /// be seven chances to half-apply an update. The <c>since</c> value is the per-catalog form
        /// the endpoint documents — <c>"texts:11,clubs:1,characters:5"</c> — and a catalog left out
        /// of it comes back in full (verified against prod 2026-08-26).
        /// </para>
        /// <para>
        /// Same failure contract as the single-catalog overload: <paramref name="onDone"/> receives
        /// null on ANY failure, and the caller keeps every cache it already has.
        /// </para>
        /// </summary>
        public static IEnumerator FetchRoutine(IReadOnlyDictionary<string, int> cursors, int build,
                                               IReadOnlyList<string> catalogs, Action<string?> onDone)
        {
            string since = BuildSince(catalogs, cursors);
            string url   = Endpoints.Content(since, build, string.Join(",", catalogs));
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
                    Debug.LogWarning(
                        $"{Tag} Content fetch failed ({result.ErrorKind}, HTTP {result.StatusCode}): " +
                        $"{result.ErrorMessage}. Keeping the bundled catalogs and every existing cache.");
                }
            });

            // Explicit pump, matching ApiClient's own convention, so this routine also works when
            // driven by a plain while(MoveNext()) in an EditMode test.
            while (get.MoveNext()) yield return get.Current;

            onDone?.Invoke(body);
        }

        /// <summary>
        /// <c>"texts:11,clubs:1,…"</c> — one <c>&lt;catalog&gt;:&lt;version&gt;</c> pair per requested
        /// catalog, in request order. A negative cursor clamps to 0, mirroring the server's own
        /// <c>parse_since</c>. Pure, so the wire format is an EditMode assertion rather than a curl.
        /// </summary>
        public static string BuildSince(IReadOnlyList<string> catalogs,
                                        IReadOnlyDictionary<string, int> cursors)
        {
            var parts = new List<string>(catalogs.Count);
            foreach (string catalog in catalogs)
            {
                if (string.IsNullOrWhiteSpace(catalog)) continue;
                int version = cursors != null && cursors.TryGetValue(catalog, out int v) ? v : 0;
                parts.Add(catalog + ":" +
                          Mathf.Max(0, version).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            return string.Join(",", parts);
        }
    }
}
