// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentCatalogStore
//
// The read surface every <X>DatabaseCSV consults while it parses. Static and
// installed once, at ContentService's Awake (-900), because the databases run
// at -200 … -70 and a MonoBehaviour lookup at that point would be a second
// ordering problem stacked on the one this spec exists to close.
//
// ⚠️ THE ORDER ASSERT LIVES HERE, not in six databases.
//   MarkReady() is called by ContentService after every cache has been applied.
//   A database that parses while State == NotRun is reading a store that will
//   be populated AFTER it — i.e. the overlay is silently absent and the game
//   shows bundled stats with no error anywhere. RequireReady() is what turns
//   that into a log line, and it is the Phase-2 analogue of Phase 1's
//   LocalizationManager.IsInitialized assert.
//
//   NotRun is NOT automatically an error: a physics lab / EditMode scene has no
//   ContentService and correctly runs bundled. The distinction the databases
//   need is "ContentService exists but has not run yet" vs "there is no
//   ContentService" — ContentService stamps Declared() in its own field
//   initialiser path so the store can tell them apart.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>Where a database's rows came from this launch.</summary>
    public enum ContentStoreState
    {
        /// <summary>No ContentService in the scene. Bundled catalogs, and that is correct.</summary>
        NotRun,

        /// <summary>ContentService exists and is mid-Awake — nothing installed yet.</summary>
        Declared,

        /// <summary>Every cached catalog has been installed; databases may read.</summary>
        Ready,
    }

    /// <summary>
    /// The applied overlay, per catalog, for the whole session. Populated once at boot (I5 — no
    /// live mid-session swap) and read by each database as it parses its bundled CSV.
    /// </summary>
    public static class ContentCatalogStore
    {
        private const string Tag = "[Content]";

        private static readonly Dictionary<string, ContentCatalog> Installed =
            ContentCatalogs.NewMap<ContentCatalog>();

        public static ContentStoreState State { get; private set; } = ContentStoreState.NotRun;

        /// <summary>True once every cached catalog has been installed and databases may read.</summary>
        public static bool IsReady => State == ContentStoreState.Ready;

        /// <summary>
        /// Called by <see cref="ContentService"/> at the very top of its Awake, before any catalog
        /// is read off disk. Between this and <see cref="MarkReady"/> the store is knowingly empty,
        /// which is what lets a database distinguish "too early" from "no ContentService".
        /// </summary>
        public static void Declare()
        {
            Installed.Clear();
            State = ContentStoreState.Declared;
        }

        /// <summary>Install one catalog's rows. Replaces any previous install of the same name.</summary>
        public static void Install(ContentCatalog catalog)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(catalog.Name)) return;
            Installed[catalog.Name] = catalog;
        }

        /// <summary>Databases may now read. Called once, after every cache has been applied.</summary>
        public static void MarkReady() => State = ContentStoreState.Ready;

        /// <summary>Drop everything. Tests, and the global kill switch.</summary>
        public static void Clear()
        {
            Installed.Clear();
            State = ContentStoreState.NotRun;
        }

        /// <summary>Install a hand-built catalog and mark ready (EditMode tests).</summary>
        public static void ConfigureForTest(params ContentCatalog[] catalogs)
        {
            Declare();
            foreach (var c in catalogs) Install(c);
            MarkReady();
        }

        // ── Reads ─────────────────────────────────────────────────────────────

        /// <summary>The overlay row for <paramref name="id"/>, or null when there is none.</summary>
        public static ContentRow? Row(string catalog, string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return Installed.TryGetValue(catalog, out var c) && c.ById.TryGetValue(id!, out var row)
                ? row : null;
        }

        /// <summary>Every overlay row for a catalog, in payload order. Empty when not overlaid.</summary>
        public static IReadOnlyList<ContentRow> Rows(string catalog)
            => Installed.TryGetValue(catalog, out var c) ? c.Rows : EmptyRows;

        /// <summary>The installed catalog, or null.</summary>
        public static ContentCatalog? Catalog(string catalog)
            => Installed.TryGetValue(catalog, out var c) ? c : null;

        /// <summary>True when this catalog has an overlay applied this session.</summary>
        public static bool IsOverlaid(string catalog) => Installed.ContainsKey(catalog);

        private static readonly ContentRow[] EmptyRows = new ContentRow[0];

        // ── The assert ────────────────────────────────────────────────────────

        /// <summary>
        /// Called by each database as it starts parsing. Returns true when the store may be read.
        /// <para>
        /// <b>State == Declared is an ERROR</b>, not a warning: it means the database's execution
        /// order puts it BEFORE ContentService (-900), so the overlay it is about to read is empty
        /// and will be filled in moments later, unread, for the rest of the session. That failure
        /// is completely silent otherwise — the game just shows bundled stats, which is exactly what
        /// a working client looks like.
        /// </para>
        /// </summary>
        public static bool RequireReady(string readerName)
        {
            switch (State)
            {
                case ContentStoreState.Ready:
                    return true;

                case ContentStoreState.Declared:
                    Debug.LogError(
                        $"{Tag} EXECUTION ORDER BROKEN: {readerName} is parsing before ContentService " +
                        $"finished installing the overlay. ContentService must stay at -900, i.e. " +
                        $"ahead of every <X>DatabaseCSV. The overlay will be SILENTLY ABSENT this " +
                        $"session — bundled rows only.");
                    return false;

                default:
                    // No ContentService in this scene (lab / EditMode / a bare gameplay scene).
                    // Bundled is the correct answer and this is not a malfunction.
                    return false;
            }
        }
    }
}
