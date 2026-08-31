// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentService
//
// The first time any of the content pipeline reaches the game. Everything built
// before this — catalogs, publish/rollback, the admin panels, the delta
// endpoint — is a system the client had never read.
//
// It does exactly what NoticeService does, for the same reasons: read the disk
// cache SYNCHRONOUSLY at Awake, apply it, then warm from the server OFF the
// critical path. Nothing on the boot path waits on a socket, so a cold launch
// in airplane mode behaves exactly as it did before this component existed.
//
// ⚠️ EXECUTION ORDER IS THE WHOLE BALLGAME.
//     LocalizationBootstrap  -1000   builds _textMap from the bundled table
//     ContentService          -900   merges the overlay on top of it   ← here
//     CharacterDatabaseCSV    -200
//     SaveDataHost            -100
//     CharacterManager         -95   ← was -100, a TIE with SaveDataHost
//     ClubDatabaseCSV          -90   BagDatabaseCSV -90   ItemDatabaseCSV -90
//     ClubManager              -80
//     BallDatabaseCSV          -70
//
//   CharacterManager moved off that tie on 2026-08-26
//   (content_kill_switch_and_order §2). It reads SaveDataHost.Instance.Data
//   behind a null guard, so losing the tie did not crash — it SKIPPED the Phase-2
//   clamp, leaving out-of-range saved values in place until a launch where the
//   tie happened to fall the other way. Non-deterministic clamping is harder to
//   diagnose than a crash, hence -95 (strictly after SaveDataHost, still ahead of
//   the club pair) plus a runtime assert on SaveDataHost.IsLoaded.
//
//   Getting this backwards is INVISIBLE: for texts, Initialize() rebuilds
//   _textMap from scratch, so an overlay applied first is silently wiped and the
//   game just shows bundled strings. For catalogs it is worse — a database that
//   parses before this component installs the store reads an EMPTY store, gets
//   bundled rows, and looks exactly like a working client. Hence two runtime
//   asserts rather than trust in an attribute:
//     • LocalizationManager.IsInitialized      (Phase 1, texts)
//     • ContentCatalogStore.RequireReady(…)    (Phase 2, every catalog DB)
//
//   ⚠️ WHERE THE DB-BEFORE-MANAGER GUARANTEE ACTUALLY COMES FROM (SPEC §"ASSERT it").
//     NOT [DefaultExecutionOrder] — ClubDatabaseCSV and ClubManager carry none.
//     NOT ProjectSettings — this project has no MonoManager.asset at all.
//     It is the `executionOrder:` field committed into each script's .cs.meta,
//     written ONCE by the GOLFIN ▸ Setup ▸ Club Managers menu item
//     (Assets/Scripts/UI/Inventory/Editor/ClubManagerSetup.cs) and never
//     re-asserted afterwards — unlike SaveDataHost's, which an [InitializeOnLoad]
//     hook re-applies on every reload. A regenerated or merge-mangled .meta
//     silently drops both to 0, where the relative order is UNDEFINED. That is
//     why ClubManager now asserts ClubDatabaseCSV.IsLoaded at runtime instead of
//     trusting the comment that used to say "runs before ClubManager".
//
// ⚠️ THE FETCH DOES NOT RE-APPLY THIS SESSION (I5). It writes the caches; the
//   change lands at the NEXT launch. Re-parsing the club DB mid-session with a
//   bag equipped and a round in flight is a re-entrancy problem with no upside.
//   DO NOT add the live swap here without a spec.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Golfin.Tournaments;   // ScheduleRefreshThrottle — see its header for why the namespace differs
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Golfin.Content
{
    /// <summary>
    /// Applies the admin-published overlay at boot and refreshes the caches for next launch.
    /// Texts merge into <c>LocalizationManager</c>; the six data catalogs are installed into
    /// <see cref="ContentCatalogStore"/>, which each <c>&lt;X&gt;DatabaseCSV</c> reads while it
    /// parses its bundled CSV.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    public sealed class ContentService : MonoBehaviour
    {
        private const string Tag = "[Content]";

        public static ContentService? Instance { get; private set; }

        /// <summary>Where the strings currently on screen came from. Diagnostics only.</summary>
        public ContentSourceKind Source { get; private set; } = ContentSourceKind.Bundled;

        /// <summary>How many rows the boot-time TEXTS overlay actually merged. Diagnostics only.</summary>
        public int AppliedTextCount { get; private set; }

        /// <summary>Rows installed into the catalog store at boot, per catalog. Diagnostics only.</summary>
        public IReadOnlyDictionary<string, int> AppliedCatalogCounts => _appliedCatalogCounts;
        private readonly Dictionary<string, int> _appliedCatalogCounts = ContentCatalogs.NewMap<int>();

        /// <summary>
        /// The cursor this session sent for texts, i.e. the bundled <c>texts=</c> line.
        /// Diagnostics only. <see cref="RequestedSince"/> is the full per-catalog string.
        /// </summary>
        public int RequestedSinceVersion { get; private set; }

        /// <summary>The whole <c>since=</c> value this session sent. Diagnostics only.</summary>
        public string RequestedSince { get; private set; } = string.Empty;

        /// <summary>
        /// Milliseconds this component added to the BOOT CRITICAL PATH — the whole synchronous
        /// Awake, i.e. every disk read + JSON map + merge. It is the complete cost: the fetch below
        /// is a coroutine and blocks nothing. Measured rather than asserted, because "off the
        /// critical path" is a claim about a number — and clubs is 799 rows (SPEC acceptance list
        /// says MEASURE, do not assert).
        /// </summary>
        public double BootCostMilliseconds { get; private set; }

        /// <summary>Per-catalog share of <see cref="BootCostMilliseconds"/>. Diagnostics only.</summary>
        public IReadOnlyDictionary<string, double> BootCostByCatalog => _bootCostByCatalog;
        private readonly Dictionary<string, double> _bootCostByCatalog = ContentCatalogs.NewMap<double>();

        /// <summary>Raised after a fetch has written a NEW cache — i.e. next launch will differ.
        /// <para>
        /// The live-swap seam it was reserved for now has one consumer: <c>GachaBannerCatalog</c>
        /// arms a pending flag here and calls <see cref="TryReinstallFromCache"/> for the four
        /// gacha catalogs on its next <c>Reload()</c> (gacha_client_real_pull §2, plan 5b). Every
        /// other catalog still takes effect at the NEXT launch — see <see cref="LiveSwappable"/>.
        /// </para></summary>
        public static event Action? OnCacheRefreshed;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Declared BEFORE anything is read, so a database that runs too early can tell
            // "ContentService has not finished" apart from "there is no ContentService".
            ContentCatalogStore.Declare();

            var bootClock = Stopwatch.StartNew();
            ApplyCachedTexts();
            ApplyCachedCatalogs();
            bootClock.Stop();

            ContentCatalogStore.MarkReady();

            BootCostMilliseconds = bootClock.Elapsed.TotalMilliseconds;
            Debug.Log($"{Tag} Boot critical-path cost: {BootCostMilliseconds:F2} ms " +
                      $"(synchronous cache reads + map + merge; the fetch below blocks nothing). " +
                      $"Per catalog: {FormatCosts()}");

            // Then warm from the server, off the critical path. Nothing above this line and
            // nothing below it waits on a socket.
            _refreshThrottle.TryBegin(Time.realtimeSinceStartupAsDouble);
            StartCoroutine(RefreshRoutine());
        }

        // ── 4c: the foreground refresh ────────────────────────────────────────

        /// <summary>
        /// The boot fetch, again, on demand — the other half of decision 5b.
        ///
        /// <para>
        /// Until this existed the fetch ran EXACTLY ONCE, in <see cref="Awake"/>. So
        /// <c>GachaBannerCatalog</c>'s <c>OnCacheRefreshed</c> subscription could only ever fire
        /// for a publish that landed BEFORE the app launched: a rate or cost published while the
        /// player was in the app waited for the next launch, which is precisely the "no build" claim
        /// 5b makes and could not keep.
        /// </para>
        /// <para>
        /// Guarded by the tournament schedule's own <see cref="ScheduleRefreshThrottle"/>, reused
        /// rather than re-implemented, for the same two reasons it exists there: a re-entry during
        /// a slow request must not queue a second one, and bouncing Home → Rewards Center → Home is
        /// ordinary play. The cooldown is armed when an attempt SETTLES, success or failure, so
        /// airplane mode cannot turn screen entry into a retry storm.
        /// </para>
        /// <para>
        /// It changes NOTHING about what is applied. The refresh writes caches exactly as the boot
        /// one does, and only the four gacha catalogs re-install live
        /// (<see cref="TryReinstallFromCache"/>'s allowlist); every other catalog still takes
        /// effect at the next launch (I5).
        /// </para>
        /// </summary>
        public void RefreshNow()
        {
            if (!isActiveAndEnabled) return;

            if (!_refreshThrottle.TryBegin(Time.realtimeSinceStartupAsDouble))
            {
                Debug.Log($"{Tag} RefreshNow ignored — " +
                          (_refreshThrottle.InFlight
                              ? "a fetch is already in flight."
                              : $"cooled down for another " +
                                $"{_refreshThrottle.SecondsUntilAllowed(Time.realtimeSinceStartupAsDouble):F0}s."));
                return;
            }

            Debug.Log($"{Tag} RefreshNow — re-fetching the content delta off the critical path.");
            StartCoroutine(RefreshRoutine());
        }

        /// <summary>
        /// Foregrounding is the other moment a publish can have landed unseen — the player was
        /// away long enough for an operator to change something, and comes back to a card priced
        /// from a catalog fetched at launch. The cooldown makes an app that is tabbed in and out
        /// repeatedly cost one request a minute, not one per switch.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) RefreshNow();
        }

        /// <summary>One instance, so BOTH callers — the Rewards Center and foregrounding — share a
        /// single cooldown rather than each getting their own.</summary>
        private readonly ScheduleRefreshThrottle _refreshThrottle =
            new ScheduleRefreshThrottle(ScheduleRefreshThrottle.DefaultCooldownSeconds);

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Boot: texts (Phase 1, unchanged) ──────────────────────────────────

        private void ApplyCachedTexts()
        {
            // THE ORDER ASSERT. If this ever fires, the overlay is about to be wiped by
            // LocalizationBootstrap.Initialize and every remote string silently reverts to
            // bundled. Error, not warning: unlike a failed fetch this is a code defect, not a
            // designed path.
            if (!LocalizationManager.IsInitialized)
            {
                Debug.LogError(
                    $"{Tag} EXECUTION ORDER BROKEN: LocalizationManager has not been initialised yet, " +
                    $"so LocalizationBootstrap.Initialize() will run AFTER this and wipe the overlay. " +
                    $"ContentService must stay at -900, i.e. after LocalizationBootstrap's -1000.");
                return;
            }

            Debug.Log($"{Tag} Awake — LocalizationManager already initialised " +
                      $"(order OK: LocalizationBootstrap -1000 → ContentService -900).");

            string? cached = RemoteContentSource.ReadCache(ContentCatalogs.Texts);
            if (cached == null)
            {
                Debug.Log($"{Tag} No texts cache; using bundled strings. " +
                          $"build={ContentBuildNumber.Current}");
                return;
            }

            TextsOverlay overlay = ContentTextsMapper.Map(cached);

            if (!overlay.Parsed)
            {
                // Corrupt / truncated / a shape this build cannot map. One warning, bundled
                // strings, no exception — a designed path, per the acceptance list.
                Debug.LogWarning($"{Tag} The texts cache could not be mapped; using bundled strings. " +
                                 $"It is left on disk in case a later build can read it.");
                return;
            }

            if (!overlay.Enabled)
            {
                // The kill switch reached disk somehow (it should never be cached). Undo it now
                // rather than waiting for a fetch that may never land.
                Debug.LogWarning($"{Tag} Cached payload has enabled=false; dropping the cache and " +
                                 $"using bundled strings.");
                RemoteContentSource.ClearCache(ContentCatalogs.Texts);
                return;
            }

            AppliedTextCount = LocalizationManager.ApplyOverlay(overlay.Rows);
            Source = AppliedTextCount > 0 ? ContentSourceKind.DiskCache : ContentSourceKind.Bundled;

            Debug.Log($"{Tag} Texts overlay applied from DISK CACHE: {AppliedTextCount} row(s) merged " +
                      $"over the bundled table (catalog v{overlay.Version}, full={overlay.Full}, " +
                      $"skipped inactive={overlay.SkippedInactive}, unusable={overlay.SkippedUnusable}).");
        }

        // ── Boot: the six data catalogs (Phase 2) ─────────────────────────────

        private void ApplyCachedCatalogs()
        {
            foreach (string catalog in ContentCatalogs.Data)
            {
                var clock = Stopwatch.StartNew();

                string? cached = RemoteContentSource.ReadCache(catalog);
                if (cached == null)
                {
                    clock.Stop();
                    _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;
                    continue;   // fresh install / never fetched / killed. Bundled, silently and correctly.
                }

                ContentPayload payload = ContentCatalogMapper.Map(cached);

                if (!payload.Parsed)
                {
                    Debug.LogWarning($"{Tag} The '{catalog}' cache could not be mapped; using the " +
                                     $"bundled CSV. It is left on disk in case a later build can read it.");
                    clock.Stop();
                    _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;
                    continue;
                }

                if (!payload.Enabled)
                {
                    // The kill switch reached disk (it should never be cached). Undo it now rather
                    // than waiting for a fetch that may never land — same reasoning as texts.
                    Debug.LogWarning($"{Tag} Cached '{catalog}' payload has enabled=false; dropping " +
                                     $"the cache and using the bundled CSV.");
                    RemoteContentSource.ClearCache(catalog);
                    clock.Stop();
                    _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;
                    continue;
                }

                if (payload.IsDisabled(catalog))
                {
                    // The PER-CATALOG kill reached disk. It should never get there — the refresh
                    // drops the cache instead of writing it — but the global equivalent above is
                    // guarded for the same reason: undo it now rather than waiting for a fetch that
                    // may never land. Only THIS catalog's cache goes.
                    Debug.LogWarning($"{Tag} Cached '{catalog}' payload reports the catalog as " +
                                     $"DISABLED; dropping that cache and using the bundled CSV. " +
                                     $"Every other catalog is untouched.");
                    RemoteContentSource.ClearCache(catalog);
                    clock.Stop();
                    _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;
                    continue;
                }

                ContentCatalog? rows = payload.Catalog(catalog);
                if (rows == null)
                {
                    // A cache file that does not carry the catalog it is named after. Not a crash,
                    // but not usable either — and NOT a reason to delete it, since a later build
                    // may read the shape this one cannot.
                    Debug.LogWarning($"{Tag} The '{catalog}' cache carries no '{catalog}' catalog; " +
                                     $"using the bundled CSV.");
                    clock.Stop();
                    _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;
                    continue;
                }

                ContentCatalogStore.Install(rows);
                _appliedCatalogCounts[catalog] = rows.Rows.Count;

                clock.Stop();
                _bootCostByCatalog[catalog] = clock.Elapsed.TotalMilliseconds;

                Debug.Log($"{Tag} '{catalog}' overlay installed from DISK CACHE: {rows.Rows.Count} row(s) " +
                          $"({rows.ActiveCount} active, {rows.Rows.Count - rows.ActiveCount} deactivated) " +
                          $"at catalog v{rows.Version}, full={rows.Full}, " +
                          $"{clock.Elapsed.TotalMilliseconds:F2} ms.");
            }
        }

        private string FormatCosts()
        {
            var parts = new List<string>();
            foreach (var pair in _bootCostByCatalog)
                if (pair.Value >= 0.005) parts.Add($"{pair.Key} {pair.Value:F2} ms");
            return parts.Count == 0 ? "(no catalog cache on disk)" : string.Join(", ", parts);
        }

        // ── Refresh: off the critical path ────────────────────────────────────

        /// <summary>
        /// Fetch the delta for every catalog and refresh the caches for NEXT launch.
        /// Deliberately does not re-apply (I5) — see the file header.
        /// </summary>
        private IEnumerator RefreshRoutine()
        {
            // The cooldown is armed when the attempt SETTLES, whatever it settled as — see
            // ScheduleRefreshThrottle on why a failure has to arm it too.
            try
            {
                var inner = RefreshRoutineCore();
                while (true)
                {
                    object current;
                    try { if (!inner.MoveNext()) break; current = inner.Current; }
                    catch (Exception ex)
                    {
                        Debug.LogError($"{Tag} RefreshRoutine threw: {ex}");
                        break;
                    }
                    yield return current;
                }
            }
            finally
            {
                _refreshThrottle.Settle(Time.realtimeSinceStartupAsDouble);
            }
        }

        private IEnumerator RefreshRoutineCore()
        {
            // THE CURSOR IS THE BUNDLED ONE, EVERY TIME — never the version from a previous
            // response, even though the endpoint would accept it.
            //
            // Each cache file is a WHOLE-CATALOG mirror replaced wholesale, so advancing the cursor
            // would make the next response a DIFFERENT subset of rows, and writing it would drop
            // everything applied by the previous delta. Replaying the bundled cursor keeps every
            // response self-sufficient: whatever is in the file is the complete overlay for this
            // build. The cost is re-sending rows already held, which is bounded by how far each
            // catalog has moved since the build shipped — and the export step (I3) resets every
            // catalog's cursor to parity on release.
            var cursors = ContentCatalogs.NewMap<int>();
            foreach (string catalog in ContentCatalogs.All)
                cursors[catalog] = ContentVersionFile.VersionFor(catalog);

            int build = ContentBuildNumber.Current;
            RequestedSinceVersion = cursors[ContentCatalogs.Texts];
            RequestedSince = RemoteContentSource.BuildSince(ContentCatalogs.All, cursors);

            Debug.Log($"{Tag} Fetching content delta: since={RequestedSince}, build={build}.");

            string? body = null;
            IEnumerator fetch = RemoteContentSource.FetchRoutine(
                cursors, build, ContentCatalogs.All, b => body = b);
            while (fetch.MoveNext()) yield return fetch.Current;

            // Airplane mode / server down. RemoteContentSource already logged one warning; every
            // cache and the applied overlay are untouched.
            if (string.IsNullOrWhiteSpace(body)) yield break;

            ContentPayload payload = ContentCatalogMapper.Map(body);

            if (!payload.Parsed)
            {
                // A 200 whose body this build cannot map. Do NOT overwrite good caches with it.
                Debug.LogWarning($"{Tag} Fetched a payload this build cannot map; " +
                                 $"every existing cache is left untouched.");
                yield break;
            }

            if (!payload.Enabled)
            {
                // GLOBAL KILL SWITCH (§7). One flag must fully undo remote content: ignore the
                // response entirely AND drop every cache, so the next launch is bundled-only.
                Debug.LogWarning($"{Tag} Kill switch: enabled=false. Dropping EVERY content cache — " +
                                 $"the next launch will use bundled catalogs only.");
                RemoteContentSource.ClearAllCaches();
                yield break;
            }

            var slices = ContentCatalogMapper.ExtractSlices(body, ContentCatalogs.All);
            int written = 0;

            foreach (string catalog in ContentCatalogs.All)
            {
                bool hasSlice = slices.TryGetValue(catalog, out string? slice)
                                && !string.IsNullOrWhiteSpace(slice);

                switch (DecideCatalogAction(payload, catalog, hasSlice))
                {
                    case CatalogRefreshAction.DropDisabled:
                        DropDisabledCatalog(catalog);
                        continue;

                    case CatalogRefreshAction.DropWithdrawn:
                        DropWithdrawnCatalog(catalog);
                        continue;
                }

                // Mirror the RAW SLICE, not a mapped view, so columns a LATER build understands
                // are kept verbatim for it.
                RemoteContentSource.WriteCache(catalog, RemoteContentSource.Envelope(catalog, slice!));
                written++;

                ContentCatalog? mapped = payload.Catalog(catalog);
                if (mapped != null && mapped.Rows.Count > 0)
                    Debug.Log($"{Tag} '{catalog}' cache refreshed from SERVER: v{mapped.Version}, " +
                              $"full={mapped.Full}, {mapped.Rows.Count} row(s). NOT applied this " +
                              $"session by design (§2 I5) — it takes effect at next launch.");
            }

            Debug.Log($"{Tag} Refresh complete: {written}/{ContentCatalogs.All.Length} catalog cache(s) " +
                      $"written, server latest_version={payload.LatestVersion}.");

            RaiseCacheRefreshed();
        }

        /// <summary>What the refresh does with ONE catalog's slice of a good payload.</summary>
        public enum CatalogRefreshAction
        {
            /// <summary>The server served it: mirror the slice into that catalog's cache.</summary>
            Write,

            /// <summary>The server NAMED it as killed — drop that catalog's cache, and only that one.</summary>
            DropDisabled,

            /// <summary>Requested, unexplained, absent — drop that catalog's cache. See
            /// <see cref="DropWithdrawnCatalog"/> for why absent is not "no update".</summary>
            DropWithdrawn,
        }

        /// <summary>
        /// The per-catalog refresh decision, as a pure function of the payload — so the BRANCH
        /// ORDER is a thing a test can drive, not a thing a reviewer has to read.
        ///
        /// <para>
        /// ⚠️ DISABLED IS ASKED FIRST, AND THAT ORDER IS LOAD-BEARING. A killed catalog is absent
        /// from <c>catalogs</c>, so it normally has no slice and the two drops would agree — but a
        /// server that named a catalog in <c>disabled</c> AND still served it would otherwise take
        /// the Write branch and cache content an operator has just switched off. The operator's
        /// switch wins over the payload.
        /// </para>
        /// <para>
        /// The GLOBAL kill never reaches here: <c>enabled:false</c> short-circuits the whole
        /// refresh and clears every cache before this is called. That separation is the entire
        /// point of content_kill_switch_and_order §1 — before it, one catalog's kill arrived as the
        /// global flag and dropped all seven.
        /// </para>
        /// </summary>
        public static CatalogRefreshAction DecideCatalogAction(ContentPayload payload, string catalog,
                                                               bool hasSlice)
        {
            if (payload.IsDisabled(catalog)) return CatalogRefreshAction.DropDisabled;
            return hasSlice ? CatalogRefreshAction.Write : CatalogRefreshAction.DropWithdrawn;
        }

        /// <summary>
        /// A catalog the server NAMED as killed — the top-level <c>disabled</c> list, which since
        /// content_cleanup_quick is the only per-catalog kill signal on the wire.
        ///
        /// <para>
        /// Identical in effect to <see cref="DropWithdrawnCatalog"/> — that catalog's cache is
        /// dropped and its next launch is bundled — and deliberately kept separate anyway, because
        /// the two differ in what they can SAY. This one is an operator's decision, reported as
        /// such at warning level with the catalog named; the other is an absence with three
        /// possible causes and no way to tell them apart. An operator reading the device log after
        /// flipping a switch should see the flip, not a paragraph of hedging.
        /// </para>
        /// <para>
        /// ONE cache, never all of them. The global kill (<c>enabled:false</c>) is the one that
        /// clears everything, and it is handled before this loop is reached.
        /// </para>
        /// </summary>
        private static void DropDisabledCatalog(string catalog)
        {
            string? existing = RemoteContentSource.ReadCache(catalog);
            if (existing == null)
            {
                Debug.Log($"{Tag} Catalog '{catalog}' is DISABLED server-side; nothing cached, so " +
                          $"this launch and the next both use the bundled CSV.");
                return;
            }

            Debug.LogWarning(
                $"{Tag} Catalog '{catalog}' is DISABLED server-side (per-catalog kill switch, §7.4). " +
                $"Dropping the '{catalog}' cache ONLY — every other catalog keeps applying — so the " +
                $"next launch uses the bundled CSV for this one until it is re-enabled.");

            RemoteContentSource.ClearCache(catalog);
        }

        /// <summary>
        /// A catalog this build EXPLICITLY REQUESTED that the response did not carry.
        ///
        /// <para>
        /// ⚠️ THE §7 KILL-SWITCH GAP, AND WHAT THIS CLIENT CAN AND CANNOT DO ABOUT IT.
        /// </para>
        /// <para>
        /// The plan (§7.4) promises one kill switch: "clients ignore all remote content and run
        /// bundled until it is flipped back". The GLOBAL <c>enabled:false</c> does exactly that.
        /// A PER-CATALOG <c>content_catalogs.is_enabled=false</c> does not — it makes that catalog
        /// merely <b>absent</b> from the payload, and a client that reads absent as "no update"
        /// keeps applying the last good overlay forever, with no network required to sustain it.
        /// </para>
        /// <para>
        /// What the wire CAN express, measured against prod 2026-08-26:
        /// <list type="bullet">
        ///   <item><c>since=clubs:1</c> at parity → <c>{"clubs":{"version":1,"full":false,"changed":[]}}</c>
        ///         — "no update" is PRESENT-AND-EMPTY.</item>
        ///   <item><c>catalogs=nosuchcatalog</c> → <c>{"catalogs":{}}</c> — absent.</item>
        /// </list>
        /// So absent is NOT "no update", and this client no longer treats it as one: a requested
        /// catalog that comes back absent is treated as WITHDRAWN and its cache is dropped, which
        /// closes the "applies forever" hole.
        /// </para>
        /// <para>
        /// What absent USED to conflate, and no longer does for the first case: absent could mean
        /// <c>is_enabled=false</c>, a catalog name this server has never heard of, or (in principle)
        /// a server-side omission bug. All three are still handled the same way — reverting to
        /// bundled is the safe answer to all three — but the kill is now NAMED in the top-level
        /// <c>disabled</c> list (content_kill_switch_and_order §1), so it is routed to
        /// <see cref="DropDisabledCatalog"/> and never reaches this method. What lands here is the
        /// genuinely unexplained absence, and the log below says so rather than listing three
        /// possibilities as if they were equally likely.
        /// </para>
        /// </summary>
        private static void DropWithdrawnCatalog(string catalog)
        {
            string? existing = RemoteContentSource.ReadCache(catalog);
            if (existing == null) return;   // nothing cached; absent is simply the steady state

            Debug.LogWarning(
                $"{Tag} Catalog '{catalog}' was REQUESTED but is absent from the response, while the " +
                $"global enabled flag is true AND the server did not name it in 'disabled'. Absent " +
                $"is not 'no update' (a catalog at cursor parity comes back present-and-empty), so " +
                $"this is read as WITHDRAWN: dropping the '{catalog}' cache ONLY, so the next launch " +
                $"uses the bundled CSV for it. This is now the UNEXPLAINED absence — an operator's " +
                $"kill arrives named — so it is either a catalog name this server has never heard " +
                $"of or a server-side omission. See SPEC §7.");

            RemoteContentSource.ClearCache(catalog);
        }

        private static void RaiseCacheRefreshed()
        {
            try { OnCacheRefreshed?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"{Tag} OnCacheRefreshed subscriber threw: {ex}"); }
        }

        // ── 5b: the gacha-only same-session re-apply ──────────────────────────

        /// <summary>
        /// The catalogs <see cref="TryReinstallFromCache"/> will re-install mid-session, and the
        /// ONLY ones.
        ///
        /// <para>
        /// I5 ("the fetch does not re-apply this session") is not a stylistic rule — it exists
        /// because re-parsing the club DB with a bag equipped, or the modes table with a round in
        /// flight, mutates state the player is standing on. THESE FOUR HAVE NO OWNED STATE. A
        /// banner is drawn from scratch every time the Rewards Center opens, the rates and pool
        /// tables are read-only inputs to a withhold decision, and a ticket type is a name and an
        /// icon. Nothing holds a reference to a row of any of them across a frame, so swapping
        /// them cannot pull the rug out from under anything — which is exactly what makes the
        /// carve-out safe here and unsafe everywhere else.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> LiveSwappable = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ContentCatalogs.GachaBanners,
            ContentCatalogs.GachaRates,
            ContentCatalogs.GachaPools,
            ContentCatalogs.TicketTypes,
        };

        /// <summary>
        /// Re-read ONE catalog's on-disk cache and install it into <see cref="ContentCatalogStore"/>
        /// mid-session (gacha_client_real_pull §2, plan decision 5b).
        ///
        /// <para>
        /// This is the same work <see cref="ApplyCachedCatalogs"/> does at boot, for one catalog,
        /// callable after <see cref="OnCacheRefreshed"/> has said a NEWER cache landed. It is
        /// refused for anything outside <see cref="LiveSwappable"/> — see that field for why the
        /// carve-out is safe for the four gacha catalogs and for nothing else.
        /// </para>
        /// </summary>
        /// <returns>True when a catalog was installed. False when the name is not live-swappable,
        /// when there is no cache on disk, or when the cache cannot be mapped — in every false case
        /// the store keeps whatever it already held, so the caller simply keeps rendering that.</returns>
        public static bool TryReinstallFromCache(string catalog)
        {
            if (string.IsNullOrWhiteSpace(catalog)) return false;

            if (!LiveSwappable.Contains(catalog))
            {
                Debug.LogWarning($"{Tag} TryReinstallFromCache('{catalog}') refused: only the gacha " +
                                 "catalogs may be re-installed mid-session (I5 — every other catalog " +
                                 "has owned state and takes effect at the NEXT launch).");
                return false;
            }

            string? cached = RemoteContentSource.ReadCache(catalog);
            if (cached == null) return false;   // never fetched / killed — bundled, silently and correctly

            ContentPayload payload = ContentCatalogMapper.Map(cached);
            if (!payload.Parsed || !payload.Enabled || payload.IsDisabled(catalog)) return false;

            ContentCatalog? rows = payload.Catalog(catalog);
            if (rows == null) return false;

            ContentCatalogStore.Install(rows);

            Debug.Log($"{Tag} '{catalog}' overlay RE-INSTALLED mid-session from the disk cache: " +
                      $"{rows.Rows.Count} row(s) ({rows.ActiveCount} active) at catalog v{rows.Version}.");
            return true;
        }
    }
}
