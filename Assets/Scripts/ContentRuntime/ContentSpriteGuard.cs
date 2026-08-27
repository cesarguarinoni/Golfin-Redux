// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentSpriteGuard  (SPEC §5)
//
// min_build matters for catalogs in a way it never did for texts. A text always
// renders; a club row references a SPRITE NAME, and a name this build does not
// ship resolves to `Placeholder`. The server filters min_build, so this mostly
// takes care of itself — but "mostly" is not a guarantee an operator can see,
// and the failure mode is a grid of grey boxes.
//
// THE RULE: if an overlay row CHANGES a sprite name and the new name does not
// resolve, keep the BUNDLED row entirely. A silently-wrong club beats an
// obviously broken one; the operator hears about it through the art-coverage
// path, and the player never sees Placeholder where art used to be.
//
// ⚠️ ONLY NAMES THE OVERLAY CHANGED ARE GUARDED. The bundled roster already has
//   missing art on purpose while the club_art_batches specs fill in brand × type
//   combos — ClubDatabaseCSV logs a summary line about it every boot. Guarding
//   bundled names too would reject every overlay row for a club whose art has
//   not landed yet, which is the opposite of the intent.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>One sprite reference on a row: where it lives, and what each side calls it.</summary>
    public readonly struct SpriteRef
    {
        /// <summary>Resources folder, e.g. <c>Clubs/Portraits</c>. Empty means "no folder prefix".</summary>
        public readonly string Folder;

        /// <summary>The name the BUNDLED CSV row carries.</summary>
        public readonly string Bundled;

        /// <summary>The name after the overlay merge.</summary>
        public readonly string Overlaid;

        /// <summary>
        /// True when the row carries an allowlisted remote URL for this sprite column.
        /// Set by the CALLER (the loader), because <c>ContentSpriteGuard</c> lives in
        /// <c>Golfin.Content</c> and cannot evaluate <c>CatalogArtPolicy</c> directly
        /// (assembly boundary — <c>Golfin.Content</c> cannot reference Assembly-CSharp).
        /// When true, the guard treats the column as satisfied regardless of whether the
        /// sprite NAME resolves — the row is not an appended row that simply has no bundled
        /// art, it is a row whose art is intentionally delivered by URL (SPEC §4.4).
        /// </summary>
        public readonly bool HasRemote;

        public SpriteRef(string folder, string bundled, string overlaid, bool hasRemote = false)
        {
            Folder     = folder ?? string.Empty;
            Bundled    = bundled ?? string.Empty;
            Overlaid   = overlaid ?? string.Empty;
            HasRemote  = hasRemote;
        }

        /// <summary>True when the overlay named something other than what the CSV named.</summary>
        public bool Changed => !string.IsNullOrEmpty(Overlaid) &&
                               !string.Equals(Overlaid, Bundled, System.StringComparison.Ordinal);

        public string ResourcePath => string.IsNullOrEmpty(Folder) ? Overlaid : Folder + "/" + Overlaid;
    }

    public static class ContentSpriteGuard
    {
        private const string Tag = "[Content]";

        // Resolution is memoised for the whole session: the roster shares art across brand × type,
        // so a few hundred distinct names back 799 rows and the un-cached form would be thousands of
        // Resources.Load calls on the boot path. Mirrors ClubDatabaseCSV's own sprite cache.
        private static readonly Dictionary<string, bool> Resolved = new Dictionary<string, bool>();

        /// <summary>Drop the memoised resolutions. Tests only.</summary>
        public static void ResetForTest() => Resolved.Clear();

        /// <summary>
        /// The first sprite the OVERLAY introduced that does not resolve, or null when every changed
        /// name is present (and when the overlay changed no names at all).
        /// <para>
        /// A column is satisfied when either (a) its sprite NAME resolves or (b) the caller set
        /// <see cref="SpriteRef.HasRemote"/> — the row carries an allowlisted URL for that column,
        /// so a missing bundled name is expected and correct (SPEC §4.4).
        /// </para>
        /// </summary>
        public static string? FirstUnresolvedChange(IReadOnlyList<SpriteRef> refs)
        {
            for (int i = 0; i < refs.Count; i++)
            {
                SpriteRef r = refs[i];
                if (!r.Changed) continue;
                if (r.HasRemote) continue;          // URL satisfies this column (SPEC §4.4)
                if (!ResolvesInternal(r.ResourcePath)) return r.ResourcePath;
            }
            return null;
        }

        /// <summary>
        /// Convenience for the APPEND case — a brand-new overlay row has no bundled counterpart to
        /// fall back to, so EVERY sprite it names is guarded and a miss drops the row. Does not
        /// support <see cref="SpriteRef.HasRemote"/>; use the <see cref="SpriteRef"/> overload when
        /// URL columns are present.
        /// </summary>
        public static string? FirstUnresolved(IReadOnlyList<string> resourcePaths)
        {
            for (int i = 0; i < resourcePaths.Count; i++)
            {
                string path = resourcePaths[i];
                if (string.IsNullOrEmpty(path)) continue;
                if (!ResolvesInternal(path)) return path;
            }
            return null;
        }

        /// <summary>
        /// APPEND case variant that respects <see cref="SpriteRef.HasRemote"/> — a column whose URL
        /// is allowlisted is satisfied even when no bundled sprite name resolves (SPEC §4.4).
        /// <para>
        /// Used by loaders that pass URL information alongside the name. The <see cref="string"/>
        /// overload is kept for call sites that have no URL information.
        /// </para>
        /// </summary>
        public static string? FirstUnresolved(IReadOnlyList<SpriteRef> refs)
        {
            for (int i = 0; i < refs.Count; i++)
            {
                SpriteRef r = refs[i];
                // An empty Overlaid means the row has no name for this column — skip.
                if (string.IsNullOrEmpty(r.Overlaid)) continue;
                if (r.HasRemote) continue;              // URL satisfies this column (SPEC §4.4)
                if (!ResolvesInternal(r.ResourcePath)) return r.ResourcePath;
            }
            return null;
        }

        /// <summary>True when <c>Resources.Load&lt;Sprite&gt;(path)</c> finds something. Memoised.</summary>
        public static bool Resolves(string? resourcePath)
            => !string.IsNullOrEmpty(resourcePath) && ResolvesInternal(resourcePath!);

        private static bool ResolvesInternal(string path)
        {
            if (Resolved.TryGetValue(path, out bool hit)) return hit;
            bool found = Resources.Load<Sprite>(path) != null;
            Resolved[path] = found;
            return found;
        }

        /// <summary>
        /// One warning naming the row, the unresolved path, and what was done instead. Kept here so
        /// every database reports a vetoed overlay in the same words.
        /// </summary>
        public static void LogVeto(string catalog, string id, string unresolvedPath, bool appended)
        {
            if (appended)
                Debug.LogWarning(
                    $"{Tag} {catalog}: DROPPED new overlay row '{id}' — its sprite " +
                    $"'{unresolvedPath}' is not in this build (SPEC §5). There is no bundled row to " +
                    $"fall back to, and a Placeholder card is worse than an absent one.");
            else
                Debug.LogWarning(
                    $"{Tag} {catalog}: KEPT THE BUNDLED ROW for '{id}' — the overlay's sprite " +
                    $"'{unresolvedPath}' is not in this build (SPEC §5). A silently-stale row beats " +
                    $"a Placeholder. The operator side of this is the art-coverage report.");
        }
    }
}
