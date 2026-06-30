// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments — SaveBackedEntryStore
// ITournamentEntryStore implementation backed by Golfin.Save (SaveDataHost).
// T5 (tournament_save_entry): replaces the in-memory InMemoryEntryStore in production.
// Wired into LocalTournamentBackend at T6.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Save;
using UnityEngine;

namespace Golfin.Tournaments
{
    /// <summary>
    /// <see cref="ITournamentEntryStore"/> backed by <see cref="SaveDataHost"/>.
    /// <para>
    /// All reads/writes go through <c>SaveDataHost.Instance.Data.tournamentEntries</c>.
    /// No direct disk I/O — <c>MarkDirty()</c> owns the debounced atomic write.
    /// </para>
    /// <para>
    /// Asmdef: <c>Golfin.Tournaments</c> → <c>Golfin.Save</c> (one-way; Save never refs Tournaments).
    /// </para>
    /// </summary>
    public sealed class SaveBackedEntryStore : ITournamentEntryStore
    {
        // ── Private access to SaveDataHost ────────────────────────────────────

        // Production path: use the singleton.
        // Test path: caller injects a SaveDataHost instance (created on a temp GameObject).
        private readonly SaveDataHost _host;

        /// <summary>
        /// Production constructor — uses <see cref="SaveDataHost.Instance"/>.
        /// Call only after <c>SaveDataHost</c> Awake has run (execution order -100).
        /// </summary>
        public SaveBackedEntryStore()
        {
            _host = SaveDataHost.Instance
                ?? throw new InvalidOperationException(
                    "[SaveBackedEntryStore] SaveDataHost.Instance is null. " +
                    "Ensure SaveDataHost is in the scene with exec-order -100.");
        }

        /// <summary>
        /// Test/injection constructor — accepts an explicit <see cref="SaveDataHost"/>.
        /// Allows EditMode tests to create a SaveDataHost on a temp GameObject and inject it.
        /// </summary>
        public SaveBackedEntryStore(SaveDataHost host)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        // ── ITournamentEntryStore ─────────────────────────────────────────────

        /// <inheritdoc/>
        public EntryState? Load(string tournamentId)
        {
            var row = FindRow(tournamentId);
            if (row == null) return null;

            // Map PersistedHoleResult list → List<HoleResult> (empty InputLog per D1)
            var holeResults = new List<HoleResult>(row.perHole.Count);
            foreach (var ph in row.perHole)
            {
                holeResults.Add(new HoleResult(
                    holeId:       ph.holeId,
                    strokes:      ph.strokes,
                    timeSeconds:  ph.timeSeconds,
                    completedUtc: DateTime.Parse(ph.completedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                    rngSeed:      ph.rngSeed,
                    inputLog:     new List<ShotCommand>()   // D1: inputLog not persisted in v1
                ));
            }

            // Map PersistedCharacterSnapshot → CharacterSnapshot (null if all-default/empty)
            CharacterSnapshot? snapshot = null;
            if (!string.IsNullOrEmpty(row.snapshot?.characterId))
            {
                snapshot = new CharacterSnapshot(
                    characterId: row.snapshot!.characterId,
                    level:       row.snapshot.level,
                    strength:    row.snapshot.strength,
                    clubControl: row.snapshot.clubControl,
                    recovery:    row.snapshot.recovery,
                    stamina:     row.snapshot.stamina
                );
            }

            // "" lastHoleUtc → null DateTime?
            DateTime? lastHoleUtc = string.IsNullOrEmpty(row.lastHoleUtc)
                ? (DateTime?)null
                : DateTime.Parse(row.lastHoleUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);

            DateTime startedUtc = DateTime.Parse(row.startedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind);

            return new EntryState(
                tournamentId:       row.tournamentId,
                characterId:        row.characterId,
                snapshot:           snapshot,
                perHole:            holeResults,
                startedUtc:         startedUtc,
                lastHoleUtc:        lastHoleUtc,
                status:             (EntryStatus)row.status,
                conditionRemaining: row.conditionRemaining  // -1f sentinel preserved (D2)
            );
        }

        /// <inheritdoc/>
        public void Save(EntryState entry)
        {
            var data = _host.Data;

            // Build the persisted hole results
            var perHole = new List<PersistedHoleResult>(entry.PerHole.Count);
            foreach (var hr in entry.PerHole)
            {
                perHole.Add(new PersistedHoleResult
                {
                    holeId       = hr.HoleId,
                    strokes      = hr.Strokes,
                    timeSeconds  = hr.TimeSeconds,
                    completedUtc = hr.CompletedUtc.ToString("O"),
                    rngSeed      = hr.RngSeed
                    // inputLog omitted (D1)
                });
            }

            // Map CharacterSnapshot → PersistedCharacterSnapshot
            var snapshot = new PersistedCharacterSnapshot();
            if (entry.Snapshot != null)
            {
                snapshot.characterId = entry.Snapshot.CharacterId;
                snapshot.level       = entry.Snapshot.Level;
                snapshot.strength    = entry.Snapshot.Strength;
                snapshot.clubControl = entry.Snapshot.ClubControl;
                snapshot.recovery    = entry.Snapshot.Recovery;
                snapshot.stamina     = entry.Snapshot.Stamina;
            }

            var row = new PersistedTournamentEntry
            {
                tournamentId       = entry.TournamentId,
                characterId        = entry.CharacterId,
                perHole            = perHole,
                startedUtc         = entry.StartedUtc.ToString("O"),
                lastHoleUtc        = entry.LastHoleUtc.HasValue ? entry.LastHoleUtc.Value.ToString("O") : "",
                status             = (int)entry.Status,
                snapshot           = snapshot,
                conditionRemaining = entry.ConditionRemaining  // -1f sentinel or real drain value (Phase 3)
                // claimed field is NOT reset here — preserve existing claimed flag on upsert
            };

            // UPSERT: replace existing row by tournamentId (never duplicate)
            int existingIndex = data.tournamentEntries.FindIndex(
                r => string.Equals(r.tournamentId, entry.TournamentId, StringComparison.Ordinal));

            if (existingIndex >= 0)
            {
                // Preserve the claimed flag from the existing row
                row.claimed = data.tournamentEntries[existingIndex].claimed;
                data.tournamentEntries[existingIndex] = row;
            }
            else
            {
                data.tournamentEntries.Add(row);
            }

            _host.MarkDirty();
        }

        /// <inheritdoc/>
        public bool IsClaimed(string tournamentId)
        {
            var row = FindRow(tournamentId);
            return row != null && row.claimed;
        }

        /// <inheritdoc/>
        public void MarkClaimed(string tournamentId)
        {
            var row = FindRow(tournamentId);
            if (row == null)
            {
                Debug.LogWarning($"[SaveBackedEntryStore] MarkClaimed: no entry found for '{tournamentId}'. " +
                                 "Cannot mark claimed — entry must be saved first.");
                return;
            }

            if (!row.claimed)
            {
                row.claimed = true;
                _host.MarkDirty();
            }
            // Idempotent: already claimed → no-op, no extra MarkDirty
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private PersistedTournamentEntry? FindRow(string tournamentId)
        {
            var entries = _host.Data.tournamentEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (string.Equals(entries[i].tournamentId, tournamentId, StringComparison.Ordinal))
                    return entries[i];
            }
            return null;
        }
    }
}
