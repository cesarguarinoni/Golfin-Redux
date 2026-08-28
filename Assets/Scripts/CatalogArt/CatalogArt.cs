// ─────────────────────────────────────────────────────────────────────────────
// CatalogArt — CatalogArt
// Thin helper: one static method the four loaders call to resolve catalog art.
// Lives here (Assembly-CSharp) for the same reason CatalogArtPolicy does:
// TournamentArtService is in Assembly-CSharp and Golfin.Content cannot reference it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Diagnostics;
using Golfin.Net;
using Golfin.Tournaments;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Golfin.CatalogArt
{
    /// <summary>
    /// Resolution probe for catalog art URLs. The loaders call this during row parsing.
    ///
    /// Named <c>CatalogArtCache</c> rather than <c>CatalogArt</c> to avoid a C# name
    /// resolution ambiguity: a class with the same name as the last segment of its own
    /// namespace shadows itself when the namespace is imported with a <c>using</c> directive.
    ///
    /// <para>
    /// <b>It reads the DISK, not just memory</b> (ARCHITECT_DECISION §1). It used to probe only
    /// the in-memory dict, which is empty at <c>Awake</c> — so art downloaded on one launch was
    /// never read on the next, and catalog art rendered on NO launch. It still never starts a
    /// download; the async prefetch remains the only thing that fetches.
    /// </para>
    /// </summary>
    public static class CatalogArtCache
    {
        private const string Tag = "[CatalogArt]";

        /// <summary>
        /// Ceiling on synchronous decodes per session, so the boot path can never be held hostage
        /// by however many rows happen to carry a URL. Beyond it, rows fall back to the async
        /// prefetch and are withheld this launch exactly as a cold cache withholds them.
        ///
        /// <para>
        /// 24 is a starting number, not a law (ARCHITECT_DECISION §1.2). It bounds both decode
        /// time and uncompressed RGBA memory — a 537×900 full-body is ~1.9 MB decoded, so 24 is
        /// ~45 MB absolute worst case and far less for thumbnails. The set is self-draining by
        /// design: <c>content_art_bundling</c> pulls URL art into <c>Resources/</c> every release,
        /// so steady state is "rows added since the last build", not the whole catalog. Revisit
        /// against the measured numbers this class logs.
        /// </para>
        /// </summary>
        public const int MaxSyncDecodesPerSession = 24;

        private static int _decodes;
        private static long _bytes;
        private static readonly Stopwatch Clock = new Stopwatch();
        private static bool _capWarned;
        private static bool _summaryScheduled;

        /// <summary>Reset the session counters. Tests only.</summary>
        public static void ResetForTest()
        {
            _decodes = 0;
            _bytes = 0;
            Clock.Reset();
            _capWarned = false;
            _summaryScheduled = false;
        }

        /// <summary>
        /// Step 1 of the resolution ladder (SPEC §2, revised 2026-08-27):
        /// returns the cached sprite for <paramref name="url"/> ONLY when
        /// <paramref name="url"/> differs from <paramref name="bundledUrl"/>, signalling
        /// that the overlay URL has changed since this build was cut — i.e. art was
        /// re-uploaded after the build.
        ///
        /// <para>
        /// Modelled on <c>ContentSpriteGuard.SpriteRef.Changed</c> — the same
        /// "overlay named something other than what the bundled CSV named" comparison,
        /// applied to URLs instead of sprite names. All four loaders already hold both
        /// the bundled row and the overlaid one at the point of resolution (that is what
        /// feeds the sprite guard), so both URLs are in hand with no extra CSV read.
        /// </para>
        ///
        /// <para>
        /// Returns null when the URLs agree (bundled art wins at step 2) or when the
        /// art is in neither memory nor the on-disk cache. Empty/null
        /// <paramref name="url"/> returns null unconditionally.
        /// </para>
        /// </summary>
        public static Sprite? Cached(string? url, string? bundledUrl)
        {
            if (string.IsNullOrEmpty(url)) return null;
            // URLs agree → the overlay has NOT re-uploaded art since this build.
            // Let step 2 (bundled sprite by name) take priority.
            if (url == bundledUrl) return null;
            return Resolve(url!);
        }

        /// <summary>
        /// Step 3 of the resolution ladder (SPEC §2, revised 2026-08-27):
        /// returns the cached sprite for <paramref name="url"/> unconditionally
        /// (URL-unchanged-since-build path, or new row from admin with no bundled art).
        /// Never starts a download. Empty/null url returns null.
        /// </summary>
        public static Sprite? Cached(string? url)
        {
            if (string.IsNullOrEmpty(url)) return null;
            return Resolve(url!);
        }

        /// <summary>
        /// Memory first (free, uncapped), then the on-disk cache under the session decode cap.
        /// Every synchronous decode is timed, so the boot cost this adds is a measured number
        /// rather than an argument — SPEC §7 has asked for that delta since iteration 1.
        /// </summary>
        private static Sprite? Resolve(string url)
        {
            var svc = TournamentArtService.CatalogArt;

            // A dict hit costs nothing and must not consume the budget: one URL shared by several
            // rows (clubs reuse art across rarities) would otherwise burn the cap on itself.
            if (svc.TryGet(url, out Sprite? hit) && hit != null) return hit;

            if (_decodes >= MaxSyncDecodesPerSession)
            {
                if (!_capWarned)
                {
                    _capWarned = true;
                    Debug.LogWarning(
                        $"{Tag} Synchronous decode cap reached ({MaxSyncDecodesPerSession} this " +
                        $"session). First row over the cap: {Leaf(url)}. Its art — and any after " +
                        "it — stays on the async prefetch and is withheld this launch, exactly " +
                        "as a cold cache withholds it; it renders on the next launch. If this " +
                        "fires routinely, run content_art_bundling to pull the art into the " +
                        "build, or raise CatalogArtCache.MaxSyncDecodesPerSession.");
                }
                return null;
            }

            Clock.Start();
            bool ok = svc.TryGetOrLoadCached(url, out Sprite? sprite, out int bytes);
            Clock.Stop();

            if (!ok) return null;

            _decodes++;
            _bytes += bytes;
            ScheduleSummary();
            return sprite;
        }

        /// <summary>
        /// Log the boot cost ONCE, one frame after the first synchronous decode. All four loaders
        /// run their <c>Awake</c> in the same frame, so a single frame's delay is "after the
        /// loaders finish" without any of them having to know about this class. Reuses the
        /// coroutine host <see cref="TournamentArtService"/> already drives its own loads on.
        /// </summary>
        private static void ScheduleSummary()
        {
            if (_summaryScheduled) return;
            _summaryScheduled = true;

            // Diagnostics must never be able to break resolution. Outside play mode there is no
            // coroutine host to speak of (and reaching for one can construct or complain), so the
            // counters simply accumulate un-logged; EditMode tests read them directly.
            if (!Application.isPlaying) return;

            try
            {
                ApiClient.Instance?.Run(LogSummaryNextFrame());
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"{Tag} Could not schedule the boot-decode summary: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator LogSummaryNextFrame()
        {
            yield return null;
            Debug.Log(
                $"{Tag} Boot art decode: {_decodes} file(s), {Clock.Elapsed.TotalMilliseconds:F1} ms, " +
                $"{_bytes / 1024f / 1024f:F2} MB read from the on-disk cache " +
                $"(cap {MaxSyncDecodesPerSession}/session). This is the delta this feature adds to " +
                "the synchronous boot path.");
        }

        private static string Leaf(string url)
        {
            int slash = url.LastIndexOf('/');
            return slash >= 0 && slash < url.Length - 1 ? url.Substring(slash + 1) : url;
        }
    }
}
