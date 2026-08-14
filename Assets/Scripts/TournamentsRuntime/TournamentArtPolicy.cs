// ─────────────────────────────────────────────────────────────────────────────
// TournamentsRuntime — TournamentArtPolicy
// The host allowlist (SPEC §5.2) and the disk-cache key derivation (SPEC §5.5).
// Pure static functions, no Unity lifecycle, so both are directly unit-testable.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Decides whether a server-supplied artwork URL may be fetched onto a player's device,
    /// and where a permitted one is cached.
    /// </summary>
    public static class TournamentArtPolicy
    {
        /// <summary>
        /// The ONLY prefix the client will download tournament art from — scheme, host AND path.
        ///
        /// ⚠️ Do not relax this to "any https URL", and do not move the check to the server alone.
        /// <c>banner_url</c> is a free-text column and <c>POST /tournaments/admin/create</c> can still
        /// write these tables behind a single static key (playlife <c>tournaments.py:193</c>, a
        /// non-constant-time compare). Without this prefix check, that column is an arbitrary content
        /// channel into every player's device and a way to harvest every player's IP address.
        /// This constant is the whole control.
        /// </summary>
        public const string AllowedArtPrefix =
            "https://wmszyghwwkaptgqdunel.supabase.co/storage/v1/object/public/tournament-art/";

        /// <summary>Cache directory name under <c>Application.persistentDataPath</c>.</summary>
        public const string CacheDirName = "tournament-art";

        /// <summary>The allowlist parsed once, so the comparison below is Uri-vs-Uri.</summary>
        private static readonly Uri AllowedRoot = new Uri(AllowedArtPrefix);

        /// <summary>
        /// True when <paramref name="url"/> resolves — <b>after normalization</b> — strictly inside
        /// the allowlisted bucket.
        ///
        /// <para>
        /// ⚠️ This is deliberately NOT <c>url.StartsWith(AllowedArtPrefix)</c>. A raw string prefix
        /// check passes this:
        /// <code>
        /// https://…supabase.co/storage/v1/object/public/tournament-art/../../../../../rest/v1/rpc/x
        /// </code>
        /// and then <c>UnityWebRequest</c> builds a <see cref="Uri"/>, which collapses the dot
        /// segments, and the device actually GETs <c>/rest/v1/rpc/x</c>. Scheme and host survive that;
        /// the path prefix — half of what D6 pins — does not. So the check must run on the same
        /// normalized form the HTTP stack will use: parse first, then compare
        /// <see cref="Uri.Scheme"/> / <see cref="Uri.Host"/> / <see cref="Uri.AbsolutePath"/>.
        /// </para>
        /// Everything unrecognised fails closed.
        /// </summary>
        public static bool IsAllowed(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri == null) return false;

            // Uri lower-cases scheme and host, so an ordinal compare here is exact, not lenient.
            if (!string.Equals(uri.Scheme, AllowedRoot.Scheme, StringComparison.Ordinal)) return false;
            if (!string.Equals(uri.Host,   AllowedRoot.Host,   StringComparison.Ordinal)) return false;

            // `https://host@evil.com/…` parses with Host=evil.com, so the Host check already covers
            // userinfo — but reject it outright rather than relying on that being true forever.
            if (!string.IsNullOrEmpty(uri.UserInfo)) return false;
            if (!uri.IsDefaultPort) return false;

            string path    = uri.AbsolutePath;      // dot segments already collapsed
            string bucket  = AllowedRoot.AbsolutePath;

            if (!path.StartsWith(bucket, StringComparison.Ordinal)) return false;
            if (path.Length <= bucket.Length) return false;   // the bucket root itself names no object

            // Belt and braces: refuse any dot segment that survived normalization in escaped form,
            // rather than assuming every platform's Uri implementation unescapes identically.
            if (path.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            if (path.IndexOf("%2e", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            return true;
        }

        /// <summary>
        /// The cache file name for a permitted URL: first 16 hex chars of SHA-256(url) plus the
        /// URL's own extension.
        /// <para>
        /// <b>The URL is the cache key, and nothing ever invalidates it.</b> The dashboard writes
        /// content-hashed immutable object names (<c>{slug}-{hash}.jpg</c>), so replacing a
        /// tournament's art produces a NEW url and therefore a new cache entry; the old one ages out
        /// through the LRU sweep. There is no staleness window to reason about.
        /// </para>
        /// </summary>
        public static string CacheFileName(string url)
        {
            if (string.IsNullOrEmpty(url)) throw new ArgumentException("url is empty", nameof(url));

            byte[] hash;
            using (var sha = SHA256.Create())
                hash = sha.ComputeHash(Encoding.UTF8.GetBytes(url));

            var sb = new StringBuilder(24);
            for (int i = 0; i < 8; i++) sb.Append(hash[i].ToString("x2")); // 8 bytes → 16 hex chars
            sb.Append(ExtensionOf(url));
            return sb.ToString();
        }

        /// <summary>
        /// The extension of the URL's last path segment, lower-cased, query/fragment stripped.
        /// Falls back to <c>.img</c> — the decoder sniffs the bytes, so the extension is a
        /// human-readable convenience, never load-bearing.
        /// </summary>
        internal static string ExtensionOf(string url)
        {
            int cut = url.IndexOfAny(new[] { '?', '#' });
            string path = cut >= 0 ? url.Substring(0, cut) : url;

            int slash = path.LastIndexOf('/');
            string leaf = slash >= 0 ? path.Substring(slash + 1) : path;

            int dot = leaf.LastIndexOf('.');
            if (dot <= 0 || dot == leaf.Length - 1) return ".img";

            string ext = leaf.Substring(dot).ToLowerInvariant();
            foreach (char c in ext)
                if (!(char.IsLetterOrDigit(c) || c == '.')) return ".img";

            return ext.Length > 6 ? ".img" : ext;
        }
    }
}
