// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentArtService
// Downloads allowlisted tournament artwork, caches it on disk keyed by URL, and
// serves one Sprite per URL to every card and screen (SPEC §5.5).
//
// There was no image-download or texture-cache code anywhere in this project
// before this file — nothing to reuse, so this is net-new by necessity.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Golfin.Net;
using UnityEngine;
using UnityEngine.Networking;

namespace Golfin.Tournaments
{
    public sealed class TournamentArtService
    {
        private const string Tag = "[TournamentArt]";

        /// <summary>Bounded disk cache (SPEC §5.5). LRU by last access once exceeded.</summary>
        public const long MaxCacheBytes = 50L * 1024L * 1024L;

        /// <summary>
        /// Per-image download ceiling. The dashboard caps uploads at 500 KB, so 1 MB is generous for
        /// legitimate art — but <c>banner_url</c> is not exclusively the dashboard's (§2.3's
        /// static-key admin route writes this table too), and <see cref="Prefetch"/> fires these
        /// UNATTENDED at boot. Without this, one oversized object under <c>tournament-art/</c> is an
        /// out-of-memory kill on launch that no player action triggered.
        /// </summary>
        public const long MaxDownloadBytes = 1024L * 1024L;

        /// <summary>Art for a tournament that ended longer ago than this is dropped regardless of size.</summary>
        public static readonly TimeSpan EndedRetention = TimeSpan.FromDays(30);

        public static TournamentArtService Instance { get; } = new TournamentArtService();

        // URL → sprite. One texture serves every card and every screen.
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        // URL → waiting callbacks. Presence here IS the "download in flight" flag, which is what
        // coalesces the six cards that would otherwise each start the same request on the same frame.
        private readonly Dictionary<string, List<Action<Sprite>>> _inFlight =
            new Dictionary<string, List<Action<Sprite>>>(StringComparer.Ordinal);

        // URLs that failed this session. Retried on the next launch, not on every screen entry —
        // otherwise entering T7 repeatedly on a dead network re-hammers the same 404.
        private readonly HashSet<string> _failed = new HashSet<string>(StringComparer.Ordinal);

        private string? _cacheDir;

        /// <summary><c>&lt;persistentDataPath&gt;/tournament-art</c>. Main thread only on first call.</summary>
        public string CacheDir =>
            _cacheDir ??= Path.Combine(Application.persistentDataPath, TournamentArtPolicy.CacheDirName);

        // ── Query ─────────────────────────────────────────────────────────────

        /// <summary>Already-decoded sprite for this URL, if any. Never starts work.</summary>
        public bool TryGet(string? url, out Sprite? sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(url)) return false;
            return _sprites.TryGetValue(url!, out sprite) && sprite != null;
        }

        // ── Request ───────────────────────────────────────────────────────────

        /// <summary>
        /// Ensure the art for <paramref name="url"/> is available and hand it to
        /// <paramref name="onReady"/> (immediately when already in memory).
        /// <paramref name="onReady"/> is never invoked with null — a failure simply never calls back,
        /// so the caller's already-drawn bundled art stays on screen.
        /// </summary>
        public void Request(string? url, Action<Sprite>? onReady)
        {
            if (string.IsNullOrEmpty(url)) return;

            if (!TournamentArtPolicy.IsAllowed(url))
            {
                // Defence in depth: the mapper already nulls refused URLs, so reaching here means a
                // new call site skipped the mapper. Refuse anyway rather than trusting the caller.
                Debug.LogWarning($"{Tag} Refusing to fetch a URL outside the allowlisted Storage prefix.");
                return;
            }

            if (_sprites.TryGetValue(url!, out var cached) && cached != null)
            {
                onReady?.Invoke(cached);
                return;
            }

            if (_failed.Contains(url!)) return;

            if (_inFlight.TryGetValue(url!, out var waiters))
            {
                if (onReady != null) waiters.Add(onReady);   // coalesced — one download per URL
                return;
            }

            var list = new List<Action<Sprite>>(1);
            if (onReady != null) list.Add(onReady);
            _inFlight[url!] = list;

            // ApiClient owns the only coroutine host reachable from a plain C# object here
            // (NetCoroutineRunner is internal to Golfin.Net).
            ApiClient.Instance.Run(LoadRoutine(url!));
        }

        /// <summary>
        /// Warm the cache for a whole schedule at boot/sign-in, so T7 is already painted when opened.
        /// </summary>
        public void Prefetch(IEnumerable<TournamentDefinition>? defs)
        {
            if (defs == null) return;
            foreach (var d in defs)
                if (d != null && !string.IsNullOrEmpty(d.BannerUrl))
                    Request(d.BannerUrl, null);
        }

        // ── Load: disk first, then network ────────────────────────────────────

        private IEnumerator LoadRoutine(string url)
        {
            string path = Path.Combine(CacheDir, TournamentArtPolicy.CacheFileName(url));

            // ── Disk hit: zero network. Acceptance §6.4 asks for this to be logged. ──
            byte[]? bytes = null;
            try
            {
                if (File.Exists(path))
                {
                    bytes = File.ReadAllBytes(path);
                    // Explicit stamp — the LRU sweep sorts on last access, and relatime-style mounts
                    // do not reliably update it on read by themselves.
                    File.SetLastAccessTimeUtc(path, DateTime.UtcNow);
                    Debug.Log($"{Tag} Cache HIT ({bytes.Length / 1024} KB), no download: {LeafOf(url)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not read cached art '{path}': {ex.Message}");
                bytes = null;
            }

            if (bytes != null && bytes.Length > 0)
            {
                if (Decode(url, bytes)) yield break;
                // Corrupt cache file — drop it and fall through to a fresh download.
                TryDelete(path);
            }

            // ── Network ───────────────────────────────────────────────────────
            var handler = new SizeCappedDownloadHandler(new byte[16 * 1024], MaxDownloadBytes);
            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, handler, null))
            {
                req.timeout = 30;

                // Do NOT follow redirects. A 30x from the allowlisted origin would otherwise put
                // third-party bytes into the texture AND into the disk cache under an allowlisted
                // key, where they would reload every launch with no network check at all — the one
                // place the §5.2 allowlist cannot see. Unity's default here is 32.
                req.redirectLimit = 0;

                yield return req.SendWebRequest();

                if (handler.Rejected)
                {
                    Debug.LogWarning(
                        $"{Tag} REFUSED {LeafOf(url)}: {handler.RejectionReason} exceeds the " +
                        $"{MaxDownloadBytes / 1024} KB per-image cap. Nothing was buffered or cached; " +
                        "bundled art stays on the card.");
                    Fail(url);
                    yield break;
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"{Tag} Download failed for {LeafOf(url)}: {req.error}. Bundled art stays on the card.");
                    Fail(url);
                    yield break;
                }

                byte[] raw = handler.GetBytes();
                if (raw == null || raw.Length == 0)
                {
                    Debug.LogWarning($"{Tag} Empty response body for {LeafOf(url)}.");
                    Fail(url);
                    yield break;
                }

                // Persist the ORIGINAL bytes, not a re-encode of the decoded texture: re-encoding
                // would inflate a JPEG into a PNG and blow the 50 MB budget for no visual gain.
                WriteCacheFile(path, raw);

                if (!Decode(url, raw))
                {
                    Debug.LogWarning($"{Tag} Could not decode the downloaded image for {LeafOf(url)}.");
                    Fail(url);
                }
                else
                {
                    Debug.Log($"{Tag} Downloaded and cached ({raw.Length / 1024} KB): {LeafOf(url)}");
                }
            }
        }

        /// <summary>
        /// Streams the body into memory and <b>refuses before buffering</b> once the cap is passed.
        ///
        /// <para>
        /// <c>DownloadHandlerTexture</c> / <c>DownloadHandlerBuffer</c> buffer the ENTIRE body before
        /// any code of ours runs, so a size check after <c>SendWebRequest</c> is a check that happens
        /// after the damage. This handler sees <c>Content-Length</c> first
        /// (<see cref="ReceiveContentLengthHeader"/>) and refuses without reading a byte; it also
        /// enforces the cap against bytes ACTUALLY received, so a missing or lying header cannot get
        /// past it either.
        /// </para>
        /// </summary>
        private sealed class SizeCappedDownloadHandler : DownloadHandlerScript
        {
            private readonly long _cap;
            private readonly MemoryStream _stream = new MemoryStream();

            public bool   Rejected        { get; private set; }
            public string RejectionReason { get; private set; } = string.Empty;

            /// <param name="scratch">Pre-allocated read buffer — the no-per-chunk-GC form.</param>
            public SizeCappedDownloadHandler(byte[] scratch, long cap) : base(scratch) => _cap = cap;

            protected override void ReceiveContentLengthHeader(ulong contentLength)
            {
                if ((long)contentLength <= _cap) return;
                Rejected        = true;
                RejectionReason = $"declared Content-Length {(long)contentLength / 1024} KB";
            }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (Rejected) return false;                       // false aborts the transfer

                if (_stream.Length + dataLength > _cap)
                {
                    Rejected        = true;
                    RejectionReason = $"body passed {_cap / 1024} KB with no honest Content-Length";
                    return false;
                }

                _stream.Write(data, 0, dataLength);
                return true;
            }

            public byte[] GetBytes() => Rejected ? Array.Empty<byte>() : _stream.ToArray();

            protected override byte[] GetData() => GetBytes();

            protected override void CompleteContent() { }
        }

        private bool Decode(string url, byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return false;
            }
            return Publish(url, tex);
        }

        private bool Publish(string url, Texture2D tex)
        {
            if (tex.width <= 0 || tex.height <= 0) return false;

            tex.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(
                tex,
                new Rect(0f, 0f, tex.width, tex.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            _sprites[url] = sprite;

            if (_inFlight.TryGetValue(url, out var waiters))
            {
                _inFlight.Remove(url);
                foreach (var cb in waiters)
                {
                    // One bad subscriber (e.g. a card destroyed mid-flight) must not starve the rest.
                    try { cb?.Invoke(sprite); }
                    catch (Exception ex) { Debug.LogWarning($"{Tag} Art callback threw: {ex.Message}"); }
                }
            }
            return true;
        }

        private void Fail(string url)
        {
            _failed.Add(url);
            _inFlight.Remove(url);   // waiters are simply never called; bundled art stays visible
        }

        // ── Cache maintenance ─────────────────────────────────────────────────

        /// <summary>
        /// Trim the disk cache on a BACKGROUND thread (SPEC §5.5: never mid-frame). Two passes:
        /// art belonging to a tournament that ended over <see cref="EndedRetention"/> ago goes
        /// regardless of size, then plain LRU by last access down to <see cref="MaxCacheBytes"/>.
        /// <para>
        /// <paramref name="urlEndUtc"/> maps each CURRENTLY-SCHEDULED banner URL to its end instant.
        /// A cached file whose URL is absent from that map is not "expired" — it is just unknown to
        /// this schedule (a replaced image, a tournament dropped from the window) — so it is left to
        /// the LRU pass rather than deleted on a guess.
        /// </para>
        /// All arguments are resolved on the calling (main) thread; the worker touches only
        /// <c>System.IO</c>, never a Unity API.
        /// </summary>
        public void SweepCacheAsync(IReadOnlyDictionary<string, DateTime> urlEndUtc)
        {
            string dir = CacheDir;                 // resolve persistentDataPath here, on the main thread
            DateTime nowUtc = DateTime.UtcNow;

            // Snapshot to a plain dictionary keyed by cache FILE NAME so the worker needs no policy state.
            var expiredNames = new HashSet<string>(StringComparer.Ordinal);
            if (urlEndUtc != null)
            {
                foreach (var kv in urlEndUtc)
                {
                    if (string.IsNullOrEmpty(kv.Key)) continue;
                    if (nowUtc - kv.Value > EndedRetention)
                        expiredNames.Add(TournamentArtPolicy.CacheFileName(kv.Key));
                }
            }

            Task.Run(() => SweepCore(dir, expiredNames));
        }

        /// <summary>Pure <c>System.IO</c>. Static and internal so a test can call it directly.</summary>
        internal static void SweepCore(string dir, HashSet<string> expiredNames)
        {
            try
            {
                if (!Directory.Exists(dir)) return;

                var files = new List<FileInfo>();
                long total = 0;

                foreach (string path in Directory.GetFiles(dir))
                {
                    var fi = new FileInfo(path);

                    // Skip staging files. The sweep is issued on the SAME FRAME as Prefetch, so a
                    // '.tmp' here is very likely a download this service is mid-write on; deleting it
                    // makes the subsequent File.Replace throw and the art silently fail to cache.
                    // They are also not part of the budget — each is about to become a real entry.
                    if (fi.Extension.Equals(".tmp", StringComparison.OrdinalIgnoreCase)) continue;

                    if (expiredNames.Contains(fi.Name))
                    {
                        try { fi.Delete(); } catch { /* next sweep retries */ }
                        continue;
                    }

                    files.Add(fi);
                    total += fi.Length;
                }

                if (total <= MaxCacheBytes) return;

                files.Sort((a, b) => a.LastAccessTimeUtc.CompareTo(b.LastAccessTimeUtc)); // oldest first

                int evicted = 0;
                foreach (var fi in files)
                {
                    if (total <= MaxCacheBytes) break;
                    long len = fi.Length;
                    try
                    {
                        fi.Delete();
                        total -= len;
                        evicted++;
                    }
                    catch { /* skip and continue */ }
                }

                Debug.Log($"{Tag} Cache sweep evicted {evicted} file(s); {total / 1024} KB remain.");
            }
            catch (Exception ex)
            {
                // A background thread that throws would otherwise vanish silently.
                Debug.LogWarning($"{Tag} Cache sweep failed: {ex.Message}");
            }
        }

        // ── Small helpers ─────────────────────────────────────────────────────

        private static void WriteCacheFile(string path, byte[] bytes)
        {
            try
            {
                string? dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string tmp = path + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                // An uncacheable image still displays this session; it just re-downloads next launch.
                Debug.LogWarning($"{Tag} Could not cache art to '{path}': {ex.Message}");
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
        }

        /// <summary>Last path segment — enough to identify the image in a log without a 200-char line.</summary>
        private static string LeafOf(string url)
        {
            int cut = url.IndexOfAny(new[] { '?', '#' });
            string path = cut >= 0 ? url.Substring(0, cut) : url;
            int slash = path.LastIndexOf('/');
            return slash >= 0 && slash < path.Length - 1 ? path.Substring(slash + 1) : path;
        }
    }
}
