// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentService
// Singleton MonoBehaviour that owns the live ITournamentBackend.
// Composes LocalTournamentBackend with all real seams via a static Compose()
// so the wiring is unit-testable (the MonoBehaviour is a thin shell).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using Golfin.Auth;
using Golfin.UI.Rankings;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// Singleton MonoBehaviour that constructs and exposes <see cref="ITournamentBackend"/>
    /// to the rest of the game.
    /// <para>
    /// <b>Usage:</b> <c>TournamentService.Instance.Backend.GetTournaments()</c>
    /// </para>
    /// <para>
    /// <b>Init-order:</b> <see cref="Compose"/> only touches <c>Resources</c> and the
    /// headless loaders/parsers at construction — the singleton-backed adapters resolve
    /// lazily at call-time, so <c>TournamentService</c> has no Awake-order dependency on
    /// <c>SaveDataHost</c>, <c>CharacterManager</c>, <c>HoleDatabaseLoader</c>, or
    /// <c>RewardPointsManager</c>. Default script execution order is fine.
    /// </para>
    /// <para>
    /// <b>Clock note:</b> uses <see cref="TimeProviderClock"/> wrapping
    /// <see cref="NetworkTimeProvider.Instance"/> (the existing Rankings time seam).
    /// This is the same concrete provider the leaderboard uses.
    /// </para>
    /// </summary>
    public sealed class TournamentService : MonoBehaviour
    {
        // ── Singleton ─────────────────────────────────────────────────────────
        public static TournamentService? Instance { get; private set; }

        /// <summary>The live backend. Available after <c>Awake</c> completes.</summary>
        public ITournamentBackend Backend { get; private set; } = null!;

        /// <summary>
        /// The RP debit seam (reward_points_backend Slice 2). Exposed so the sign-up modal can settle
        /// the entry fee — server first, then local — BEFORE calling <c>Register</c>, which it then
        /// calls with a fee of 0.
        ///
        /// A distinct instance from the one handed to the backend, which is fine and deliberate:
        /// <see cref="RewardPointsServiceAdapter"/> is stateless and resolves
        /// <c>RewardPointsManager.Instance</c> lazily per call, so both see the same balance.
        /// Available immediately — no <c>Awake</c> dependency.
        /// </summary>
        public IRewardPointsService RewardPoints { get; } = new RewardPointsServiceAdapter();

        // ── Shared navigation handoff (T5 → T6) ───────────────────────────────
        /// <summary>
        /// Set by the Selection screen CTA before navigating to the Leaderboard or
        /// HoleSelection screen. Read by the Leaderboard (and later T6 hole-select).
        /// Null-guard all consumers.
        /// </summary>
        public string? SelectedTournamentId { get; set; }

        // ── Prize table cache (resolved at Compose time; read by GetTopPrizeRP) ─
        // Cached separately from the backend so the UI runtime layer can call
        // GetTopPrizeRP without reaching into LocalTournamentBackend internals.
        private IReadOnlyDictionary<string, PrizeTable> _prizeTables
            = new Dictionary<string, PrizeTable>();

        /// <summary>
        /// Raised on the main thread after a server fetch has REPLACED the schedule.
        /// Not raised for the boot-time cache/CSV load — the screen has not been built yet then.
        /// <para>
        /// <c>TournamentSelectionScreenController.OnEnable</c> already rebuilds on entry, so
        /// subscribing to this is only what covers the case where the fetch lands while the player
        /// is already looking at the screen.
        /// </para>
        /// </summary>
        public static event Action? OnScheduleChanged;

        /// <summary>Where the schedule currently in <see cref="Backend"/> came from. Diagnostics.</summary>
        public ScheduleSource Source { get; private set; } = ScheduleSource.BundledCsv;

        // ── Refresh throttling ────────────────────────────────────────────────

        /// <summary>
        /// The one place the refetch cooldown is defined. The screen calls
        /// <see cref="RefreshSchedule"/> on every entry; this is what keeps that from becoming one
        /// request per Home↔Tournaments bounce. Rationale and the in-flight guard live in
        /// <see cref="ScheduleRefreshThrottle"/>.
        /// </summary>
        public const double ScheduleRefreshCooldownSeconds = ScheduleRefreshThrottle.DefaultCooldownSeconds;

        private readonly ScheduleRefreshThrottle _refreshThrottle =
            new ScheduleRefreshThrottle(ScheduleRefreshCooldownSeconds);

        /// <summary>Monotonic seconds; unaffected by <c>Time.timeScale</c> or a paused game.</summary>
        private static double NowSeconds => Time.realtimeSinceStartupAsDouble;

        // Bot fields stay bundled (the server does not own them yet), and the mapper validates
        // every server botFieldId against this. Loaded once at Awake.
        private IReadOnlyDictionary<string, BotFieldConfig> _botFields
            = new Dictionary<string, BotFieldConfig>();

        // ── Async-multiplayer backend selection (tournament_async_board SPEC §3) ──

        /// <summary>The composed local backend. Kept even on the remote path — the remote wrapper
        /// delegates definitions, state derivation and the entry store straight back to it.</summary>
        private LocalTournamentBackend? _localBackend;

        /// <summary>The remote wrapper, built once per session and REUSED across schedule swaps and
        /// sign-in re-evaluations. Rebuilding it would drop the cached board snapshots and re-read
        /// the submit queue off disk for no reason.</summary>
        private RemoteTournamentBackend? _remoteBackend;

        private TournamentBackendKind _backendKind = TournamentBackendKind.Local;

        /// <summary>Which backend this session is on. Diagnostics + the leaderboard screen's
        /// "is this the shared board?" check.</summary>
        public TournamentBackendKind BackendKind => _backendKind;

        /// <summary>The remote backend when this session is on it, else null. The signup modal and
        /// the leaderboard screen use it for the async register path and the served board; every
        /// other caller goes through <see cref="Backend"/> and never knows the difference.</summary>
        public RemoteTournamentBackend? Remote
            => _backendKind == TournamentBackendKind.Remote ? _remoteBackend : null;

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

            var loader = new TournamentCsvLoader();
            _botFields = loader.LoadBotFields();

            // ── Boot: cache-or-CSV, SYNCHRONOUSLY, exactly as before this phase ──
            // Nothing on the boot path may wait on a socket. A cold launch in airplane mode must
            // behave precisely as it does today (SPEC §3 D3).
            var cached = TournamentScheduleMapper.TryMapJson(
                RemoteTournamentSource.ReadCache(), _botFields, "cache");

            if (cached != null) Apply(cached, ScheduleSource.DiskCache);
            else                ApplyBundledCsv(loader);

            // ── Then warm from the server, off the critical path ──
            // Through the SAME throttled entry point the screen uses, so opening Tournaments in the
            // first seconds of a session does not fire a second request alongside the boot fetch.
            RefreshSchedule();

            // A player who signs in mid-session must move onto the shared board without a relaunch.
            // A returning player is already authenticated by the time Awake runs and never fires the
            // event, which is why EnsureBackendForSession also runs inside Apply* above.
            AuthService.SignedIn += OnSignedIn;
        }

        private void OnDestroy()
        {
            AuthService.SignedIn -= OnSignedIn;

            if (Instance == this)
                Instance = null;
        }

        // ── Backend selection + offline catch-up (tournament_async_board SPEC §3) ──

        private void OnSignedIn(AuthSession session)
        {
            EnsureBackendForSession();
            CatchUpWithServer("sign-in");
        }

        /// <summary>Foreground, not background: <c>paused == false</c> is the app coming BACK, which
        /// is when a round played in airplane mode finally has signal to flush, and when a
        /// tournament resumed on another device needs pulling down.</summary>
        private void OnApplicationPause(bool paused)
        {
            if (paused) return;
            CatchUpWithServer("resume");
        }

        /// <summary>
        /// Drain the pending-hole queue and reconcile every ENTERED tournament from the server.
        /// Scoped to entered tournaments deliberately: reconciling one the player never entered
        /// would be a request per row on the schedule for an answer that is always <c>null</c>.
        /// </summary>
        private void CatchUpWithServer(string why)
        {
            RemoteTournamentBackend? remote = Remote;
            if (remote == null) return;

            Debug.Log($"[TournamentService] Catching up with the server ({why}): " +
                      $"{remote.Queue.Count} hole(s) queued.");

            remote.FlushSubmitQueue();

            var defs = Backend != null ? Backend.GetTournaments() : null;
            if (defs == null) return;

            for (int i = 0; i < defs.Count; i++)
            {
                if (remote.GetMyEntry(defs[i].Id) != null)
                    remote.ReconcileEntry(defs[i].Id);
            }
        }

        /// <summary>
        /// Point <see cref="Backend"/> at whatever this session should play on, and do nothing when
        /// that has not changed. Safe to call repeatedly — every schedule swap does.
        /// <para>
        /// The RULE lives in <see cref="TournamentBackendPolicy"/> so an EditMode test can exercise
        /// it without a scene; this method only supplies the three facts it needs.
        /// </para>
        /// </summary>
        public void EnsureBackendForSession()
        {
            if (_localBackend == null) return;

            bool botOverride = false;
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            // Whole-file-guarded type: the caller must repeat the guard (see BotSessionOverride's
            // header and the iOS lesson about #if UNITY_EDITOR seams in runtime assemblies).
            botOverride = Golfin.Dev.BotSessionOverride.Active;
#endif
            // AuthService.Instance lazily creates a DontDestroyOnLoad singleton, which throws outside
            // play mode — and this MonoBehaviour is reachable from editor tooling.
            bool signedIn = Application.isPlaying
                            && AuthService.Instance != null
                            && AuthService.Instance.Session != null
                            && AuthService.Instance.Session.IsAuthenticated;

            _backendKind = TournamentBackendPolicy.Choose(botOverride, signedIn, GolfinRedux.Demo.DemoGate.IsDemo);

            if (_backendKind == TournamentBackendKind.Remote)
            {
                _remoteBackend ??= new RemoteTournamentBackend(
                    local:       _localBackend,
                    store:       new SaveBackedEntryStore(),
                    rp:          new RewardPointsServiceAdapter(),
                    items:       new ItemRewardServiceAdapter(),
                    prizeTables: _prizeTables,
                    clock:       new TimeProviderClock(NetworkTimeProvider.Instance));

                Backend = _remoteBackend;
            }
            else
            {
                Backend = _localBackend;
            }

            Debug.Log($"[TournamentService] Backend = {_backendKind} " +
                      $"(bot override: {botOverride}, signed in: {signedIn}, demo: {GolfinRedux.Demo.DemoGate.IsDemo}).");
        }

        // ── Schedule application ──────────────────────────────────────────────

        private void ApplyBundledCsv(TournamentCsvLoader loader)
        {
            _prizeTables = loader.LoadPrizeTables();
            SetBackend(Compose());
            Source       = ScheduleSource.BundledCsv;
            LogSource();
            WarmArt();
        }

        private void Apply(TournamentSchedule schedule, ScheduleSource source)
        {
            _prizeTables = schedule.PrizeTables;
            SetBackend(ComposeFrom(schedule.Definitions, schedule.PrizeTables));
            Source       = source;
            LogSource();
            WarmArt();
        }

        /// <summary>
        /// Install a freshly-composed local backend and re-apply the session's backend policy on top
        /// of it. Every schedule swap goes through here, so a live refetch cannot silently drop a
        /// signed-in player back onto the local sim.
        /// </summary>
        private void SetBackend(ITournamentBackend composed)
        {
            _localBackend = composed as LocalTournamentBackend;

            if (_localBackend == null)
            {
                // Compose() is the only producer and it always builds a LocalTournamentBackend, so
                // this is a wiring bug rather than a runtime condition. Use what we were given.
                Debug.LogWarning("[TournamentService] Composed backend is not a LocalTournamentBackend — " +
                                 "the remote wrapper cannot be applied to it.");
                Backend      = composed;
                _backendKind = TournamentBackendKind.Local;
                return;
            }

            EnsureBackendForSession();
        }

        /// <summary>
        /// Warm the art cache and trim it — on <b>every</b> boot path (live, cache, CSV), not only
        /// after a successful fetch.
        /// <para>
        /// The sweep is what enforces the 50 MB bound. Running it only on sessions that reach the
        /// server would mean the bound does not exist on exactly the launches where the disk cache
        /// is the only art there is — and Risk 2 (free-tier Supabase auto-pauses) says those are not
        /// rare. Prefetch is a no-op on the CSV path, since CSV rows carry no <c>BannerUrl</c>.
        /// </para>
        /// </summary>
        private void WarmArt()
        {
            var defs = Backend.GetTournaments();
            TournamentArtService.Instance.Prefetch(defs);
            TournamentArtService.Instance.SweepCacheAsync(BuildArtRetentionMap(defs));
        }

        private void LogSource()
        {
            string label = Source switch
            {
                ScheduleSource.Server    => "SERVER (live fetch)",
                ScheduleSource.DiskCache => "DISK CACHE (previous fetch)",
                _                        => "BUNDLED CSV (offline fallback)",
            };
            Debug.Log($"[TournamentService] Schedule source: {label}. Tournaments={Backend.GetTournaments().Count}");
        }

        /// <summary>
        /// Ask for a fresh schedule from the server. <b>The caller never waits on this</b> — it
        /// returns on the same frame, having at most started a coroutine. Whoever is on screen keeps
        /// rendering whatever it already has; if a new schedule lands,
        /// <see cref="OnScheduleChanged"/> is raised and subscribers repaint in place.
        /// <para>
        /// Called from <c>Awake</c> (the boot warm) and from
        /// <c>TournamentSelectionScreenController.OnEnable</c> (screen entry). Both go through the
        /// throttle, so re-entering the screen during a slow request, or five times in ten seconds,
        /// is still one request.
        /// </para>
        /// <para>
        /// <b>Failure is silent by construction.</b> Every failure path inside the routine leaves the
        /// current schedule untouched and logs a warning once — no toast, no empty state, no retry.
        /// </para>
        /// </summary>
        /// <returns>True if a fetch was started; false if throttled (in flight or within cooldown).</returns>
        public bool RefreshSchedule()
        {
            if (!_refreshThrottle.TryBegin(NowSeconds)) return false;

            StartCoroutine(RefreshScheduleRoutine());
            return true;
        }

        /// <summary>
        /// Fetch the schedule and, if it maps, swap it in wholesale and raise
        /// <see cref="OnScheduleChanged"/>. Any failure leaves the current schedule untouched.
        /// <para>
        /// Enter through <see cref="RefreshSchedule"/>, never directly — the <c>finally</c> below is
        /// what releases the throttle's in-flight guard, and it pairs with a <c>TryBegin</c> that
        /// only that method performs.
        /// </para>
        /// </summary>
        private IEnumerator RefreshScheduleRoutine()
        {
            // try/finally (no catch) is legal around `yield`, and is what guarantees the in-flight
            // guard is released on EVERY exit — including the four `yield break`s below. A guard left
            // set would silently disable refresh for the rest of the session.
            try
            {
                string? body = null;
                IEnumerator fetch = RemoteTournamentSource.FetchRoutine(b => body = b);
                while (fetch.MoveNext()) yield return fetch.Current;

                if (string.IsNullOrWhiteSpace(body)) yield break;

                var schedule = TournamentScheduleMapper.TryMapJson(body, _botFields, "server");
                if (schedule == null) yield break;   // errors already logged; keep what we have

                schedule = PreserveEnteredTournaments(schedule);

                // Apply() also warms + sweeps the art cache — including art for tournaments that are
                // new to this payload, which is what gets a brand-new card its remote banner without
                // a relaunch.
                Apply(schedule, ScheduleSource.Server);

                try { OnScheduleChanged?.Invoke(); }
                catch (Exception ex) { Debug.LogError($"[TournamentService] OnScheduleChanged subscriber threw: {ex}"); }
            }
            finally
            {
                _refreshThrottle.Settle(NowSeconds);
            }
        }

        /// <summary>
        /// A tournament the player has ALREADY ENTERED must not change under them mid-session, and
        /// must not VANISH either (SPEC §4.2, D3 as amended). The union logic — and the reason it is
        /// a union rather than a filter over the incoming list — lives in
        /// <see cref="TournamentScheduleMapper.MergePreservingEntered"/>, which is pure and tested.
        /// This wrapper only supplies the live backend's entry lookup.
        /// </summary>
        private TournamentSchedule PreserveEnteredTournaments(TournamentSchedule incoming)
        {
            var backend = Backend;
            if (backend == null) return incoming;

            return TournamentScheduleMapper.MergePreservingEntered(
                incoming,
                backend.GetTournaments(),
                _prizeTables,
                id => backend.GetMyEntry(id) != null);
        }

        private static IReadOnlyDictionary<string, DateTime> BuildArtRetentionMap(
            IReadOnlyList<TournamentDefinition> defs)
        {
            var map = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            foreach (var d in defs)
                if (!string.IsNullOrEmpty(d.BannerUrl))
                    map[d.BannerUrl!] = d.EndUtc;
            return map;
        }

        // ── Safe definition lookup ────────────────────────────────────────────

        /// <summary>
        /// The non-throwing counterpart of <c>Backend.GetTournament(id)</c>, which throws
        /// <see cref="KeyNotFoundException"/> for an unknown id.
        /// <para>
        /// With a refetch on every screen entry AND an admin Activate/Deactivate switch, "the id I am
        /// holding is no longer in the schedule" stops being exotic: a tournament the player has NOT
        /// entered legitimately disappears mid-session, and any UI still holding its id — a signup
        /// modal left open across a refresh — would take the exception. Entered tournaments are a
        /// different case and never vanish: see <see cref="PreserveEnteredTournaments"/>.
        /// </para>
        /// </summary>
        /// <returns>The definition, or null when the id is unknown to the current schedule.</returns>
        public TournamentDefinition? TryGetTournament(string? id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            var backend = Backend;
            if (backend == null) return null;

            var defs = backend.GetTournaments();
            for (int i = 0; i < defs.Count; i++)
                if (string.Equals(defs[i].Id, id, StringComparison.Ordinal)) return defs[i];

            return null;
        }

        // ── Prize accessor (T5 → T6 reuse) ────────────────────────────────────

        /// <summary>
        /// Returns the top-prize RP for the tournament (rank 1, band covering rank 1).
        /// Used by the Selection card to display the headline reward.
        /// Returns 0 if the prize table or rank-1 band is absent.
        /// </summary>
        public long GetTopPrizeRP(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return 0L;

            // Resolve the prizeTableId from the definition
            var defs = Backend.GetTournaments();
            string prizeTableId = string.Empty;
            foreach (var d in defs)
            {
                if (string.Equals(d.Id, tournamentId, System.StringComparison.Ordinal))
                {
                    prizeTableId = d.PrizeTableId;
                    break;
                }
            }
            if (string.IsNullOrEmpty(prizeTableId)) return 0L;

            if (!_prizeTables.TryGetValue(prizeTableId, out var table)) return 0L;

            // Find the band that covers rank 1
            foreach (var band in table.Bands)
            {
                if (band.RankFrom <= 1 && band.RankTo >= 1)
                    return band.RpReward;
            }
            return 0L;
        }

        // ── Composition root (static + testable) ──────────────────────────────

        /// <summary>
        /// Pure-ish factory — constructs and returns a fully-wired
        /// <see cref="LocalTournamentBackend"/> with all real seams.
        /// <para>
        /// Called from <see cref="Awake"/> in production; also called directly in
        /// PlayMode tests (which boot into the real Unity runtime so Resources.Load
        /// works and the singleton-backed adapters can resolve).
        /// </para>
        /// <para>
        /// <b>stats param:</b> <see cref="CharacterManagerStatsProvider"/> is
        /// EXPLICITLY passed — do NOT omit it. Omission silently makes all snapshots
        /// null (the <c>_stats?.</c> optional-injection trap). This is the composition
        /// guard against that regression.
        /// </para>
        /// <para>
        /// <b>The server seam is here and nowhere else.</b> Definitions and prize tables are
        /// INJECTED into <c>LocalTournamentBackend</c>, so making the schedule server-authoritative
        /// only changes where these two arguments come from. <c>ITournamentBackend</c>,
        /// <c>LocalTournamentBackend</c> and <c>DeriveState</c> are untouched by that change, and
        /// state stays client-derived from <c>StartUtc</c>/<c>EndUtc</c> (SPEC §3 D4).
        /// Passing null for either falls back to the shipped CSV, which is what a no-network launch does.
        /// </para>
        /// </summary>
        /// <summary>
        /// CSV composition — the shipping default and the offline path.
        /// <para>
        /// Deliberately NOT an overload of <see cref="ComposeFrom"/> and deliberately not given
        /// optional parameters: the wire-up regression guard resolves this by REFLECTION as
        /// <c>GetMethod("Compose")</c> and invokes it with zero arguments. An optional-parameter
        /// signature throws <c>TargetParameterCountException</c> there, and a second overload named
        /// <c>Compose</c> throws <c>AmbiguousMatchException</c>. A distinct name for the server-fed
        /// variant keeps that guard working untouched.
        /// </para>
        /// </summary>
        public static ITournamentBackend Compose() => ComposeFrom(null, null);

        /// <inheritdoc cref="Compose()"/>
        public static ITournamentBackend ComposeFrom(
            IReadOnlyList<TournamentDefinition>?     definitions,
            IReadOnlyDictionary<string, PrizeTable>? prizeTables)
        {
            // ── CSV loaders (reads Resources; headless) ───────────────────────
            var loader   = new TournamentCsvLoader();
            var defs     = definitions ?? loader.LoadTournaments();
            var prizes   = prizeTables ?? loader.LoadPrizeTables();
            var fields   = loader.LoadBotFields();

            // ── Bot field generator ───────────────────────────────────────────
            var roster   = FakePlayerRosterParser.Parse(LoadText("Data/fake_players"));
            var brackets = BotScoreBracketsParser.Parse(LoadText("Data/bot_score_brackets"));
            var botGen   = new BotFieldGenerator(roster, brackets);

            // ── Clock: adapt NetworkTimeProvider (existing Rankings seam) ─────
            // TimeProviderClock(ITimeProvider) requires an ITimeProvider arg.
            // NetworkTimeProvider.Instance is the authoritative singleton that
            // wraps DateTime.UtcNow + HTTP Date offset (offline-safe fallback).
            var clock = new TimeProviderClock(NetworkTimeProvider.Instance);

            return new LocalTournamentBackend(
                definitions:  defs,
                prizeTables:  prizes,
                botFields:    fields,
                botGen:       botGen,
                clock:        clock,
                store:        new SaveBackedEntryStore(),
                rp:           new RewardPointsServiceAdapter(),
                items:        new ItemRewardServiceAdapter(),
                pars:         new HoleParProviderAdapter(),
                stats:        new CharacterManagerStatsProvider()); // MUST be passed — guards null-snapshot trap
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static string LoadText(string path)
        {
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null)
                throw new System.InvalidOperationException(
                    $"[TournamentService] Missing TextAsset at Resources/{path}. " +
                    "Ensure the CSV is placed in Assets/Resources/Data/.");
            return asset.text;
        }
    }
}
