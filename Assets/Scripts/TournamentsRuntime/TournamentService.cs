// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — TournamentService
// Singleton MonoBehaviour that owns the live ITournamentBackend.
// Composes LocalTournamentBackend with all real seams via a static Compose()
// so the wiring is unit-testable (the MonoBehaviour is a thin shell).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
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

            Backend = Compose();
            Debug.Log($"[TournamentService] Backend ready. Tournaments={Backend.GetTournaments().Count}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
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
        /// </summary>
        public static ITournamentBackend Compose()
        {
            // ── CSV loaders (reads Resources; headless) ───────────────────────
            var loader   = new TournamentCsvLoader();
            var defs     = loader.LoadTournaments();
            var prizes   = loader.LoadPrizeTables();
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
