// ─────────────────────────────────────────────────────────────────────────────
// ContentRuntime — ContentClamp  (SPEC §2 — THE HEART OF THIS TASK)
//
// Phase 1 could only ever publish a wrong STRING: visible, harmless, fixed by
// the next publish. Phase 2 can publish a maxDurability BELOW a club a player
// already owns, and that leaves a saved PersistedClub holding a value above a
// ceiling that no longer exists. Un-clamped application is the single most
// likely way this feature corrupts a save.
//
// THREE RULES, all of them deliberate:
//
//   1. ONCE, IN AN EXPLICIT STEP — after the overlay is applied and the save is
//      loaded, never at each read site. A clamp at the read site is a clamp
//      that some read site forgets, and the one that forgets is the one that
//      writes the value back.
//
//   2. EVERY CLAMP LOGS: id, field, old, new. A silent clamp is
//      indistinguishable from a bug report six weeks later. The log line is the
//      artifact, not a nicety.
//
//   3. NO REFUNDS. A rarity downgrade that orphans allocated SP is clamped and
//      LOGGED; nothing is handed back. Refunding is its own decision with its
//      own economy consequences, and inventing one here would make it
//      impossible to decide later. Explicitly out of scope per the SPEC.
//
// Pure: no MonoBehaviour, no Resources, no clock. The caller resolves the
// definitions (which needs Assembly-CSharp types — ClubDatabaseCSV,
// RarityStatCaps) and hands them in as plain numbers, so the whole clamp matrix
// is an EditMode test.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Save;
using UnityEngine;

namespace Golfin.Content
{
    /// <summary>The overlaid club definition one owned instance must fit inside.</summary>
    public readonly struct ClubClampDefinition
    {
        public readonly string Id;
        public readonly int MaxDurability;
        public readonly int StartLevel;
        public readonly int MaxLevel;

        /// <summary>Flat per-stat SP ceiling — <c>PlayerClubData.MAX_SP_PER_STAT</c>.</summary>
        public readonly int MaxSpentPerStat;

        public ClubClampDefinition(string id, int maxDurability, int startLevel, int maxLevel,
                                   int maxSpentPerStat)
        {
            Id              = id;
            MaxDurability   = maxDurability;
            StartLevel      = startLevel;
            MaxLevel        = maxLevel;
            MaxSpentPerStat = maxSpentPerStat;
        }
    }

    /// <summary>
    /// The overlaid character definition one owned instance must fit inside.
    /// <para>
    /// The SP ceilings are <b>SPENT</b> ceilings, not stat caps: RarityStatCaps caps
    /// <c>base + spent</c>, so the caller passes <c>max(0, cap − base)</c> for the row's CURRENT
    /// rarity. Doing that conversion here would need RarityStatCaps, which lives in
    /// Assembly-CSharp and this assembly cannot reference.
    /// </para>
    /// </summary>
    public readonly struct CharacterClampDefinition
    {
        public readonly string Id;
        public readonly int StartLevel;
        public readonly int MaxLevel;
        public readonly int MaxSpentStrength;
        public readonly int MaxSpentClubControl;
        public readonly int MaxSpentRecovery;
        public readonly int MaxSpentStamina;

        public CharacterClampDefinition(string id, int startLevel, int maxLevel,
                                        int maxSpentStrength, int maxSpentClubControl,
                                        int maxSpentRecovery, int maxSpentStamina)
        {
            Id                  = id;
            StartLevel          = startLevel;
            MaxLevel            = maxLevel;
            MaxSpentStrength    = maxSpentStrength;
            MaxSpentClubControl = maxSpentClubControl;
            MaxSpentRecovery    = maxSpentRecovery;
            MaxSpentStamina     = maxSpentStamina;
        }
    }

    /// <summary>One field that moved, as it will be logged and as a test can assert it.</summary>
    public readonly struct ClampEvent
    {
        public readonly string Catalog;
        public readonly string Id;
        public readonly string Field;
        public readonly int OldValue;
        public readonly int NewValue;

        public ClampEvent(string catalog, string id, string field, int oldValue, int newValue)
        {
            Catalog  = catalog;
            Id       = id;
            Field    = field;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public override string ToString() =>
            $"{Catalog} '{Id}': {Field} {OldValue} → {NewValue}";
    }

    /// <summary>
    /// Clamps every owned instance against the catalog definitions the overlay produced.
    /// </summary>
    public static class ContentClamp
    {
        private const string Tag = "[Content]";

        /// <summary>
        /// Clamp every persisted club against its (possibly overlaid) definition.
        /// <para>
        /// A club with NO definition is left ALONE — that is a row the catalog no longer carries,
        /// which is not the same as a row that was deactivated (I6), and inventing a ceiling for it
        /// would be worse than leaving the saved values as they are. It is logged once.
        /// </para>
        /// <para>
        /// <b><see cref="PersistedClub.equippedBagSlot"/> is never touched</b> (I6): a club whose row
        /// became <c>is_active=false</c> stays exactly as equipped as it was.
        /// </para>
        /// </summary>
        /// <returns>Every field that moved. Empty means nothing needed clamping.</returns>
        public static List<ClampEvent> ClampClubs(
            IList<PersistedClub>? clubs,
            IReadOnlyDictionary<string, ClubClampDefinition> definitions)
        {
            var events = new List<ClampEvent>();
            if (clubs == null || clubs.Count == 0) return events;

            var undefined = new List<string>();

            foreach (var club in clubs)
            {
                if (club == null || string.IsNullOrEmpty(club.clubId)) continue;

                if (!definitions.TryGetValue(club.clubId, out ClubClampDefinition def))
                {
                    undefined.Add(club.clubId);
                    continue;
                }

                // ── maxDurability: the saved ceiling follows the catalog ──────
                // This is the field that makes the whole spec necessary. The saved copy exists so a
                // club's ceiling is stable while it is owned; a PUBLISHED change overrides it, and
                // currentDurability then has to follow it down.
                if (def.MaxDurability > 0 && club.maxDurability != def.MaxDurability)
                    events.Add(Set(ref club.maxDurability, def.MaxDurability,
                                   ContentCatalogs.Clubs, club.clubId, nameof(club.maxDurability)));

                int durabilityCeiling = Math.Max(0, club.maxDurability);
                if (club.currentDurability < 0 || club.currentDurability > durabilityCeiling)
                    events.Add(Set(ref club.currentDurability,
                                   Clamp(club.currentDurability, 0, durabilityCeiling),
                                   ContentCatalogs.Clubs, club.clubId, nameof(club.currentDurability)));

                // ── currentLevel ─────────────────────────────────────────────
                LevelBounds(def.StartLevel, def.MaxLevel, out int lo, out int hi);
                if (club.currentLevel < lo || club.currentLevel > hi)
                    events.Add(Set(ref club.currentLevel, Clamp(club.currentLevel, lo, hi),
                                   ContentCatalogs.Clubs, club.clubId, nameof(club.currentLevel)));

                // ── allocated SP ─────────────────────────────────────────────
                // Clubs cap SP FLAT per stat (PlayerClubData.MAX_SP_PER_STAT), not by rarity — so a
                // rarity change cannot orphan club SP the way it can character SP. The clamp is
                // still run because a negative or corrupt value is a real state a save can be in.
                int spCeiling = Math.Max(0, def.MaxSpentPerStat);
                ClampSp(events, ContentCatalogs.Clubs, club.clubId, spCeiling,
                        ref club.spentPower,         nameof(club.spentPower));
                ClampSp(events, ContentCatalogs.Clubs, club.clubId, spCeiling,
                        ref club.spentAccuracy,      nameof(club.spentAccuracy));
                ClampSp(events, ContentCatalogs.Clubs, club.clubId, spCeiling,
                        ref club.spentLieResistance, nameof(club.spentLieResistance));
                ClampSp(events, ContentCatalogs.Clubs, club.clubId, spCeiling,
                        ref club.spentDurability,    nameof(club.spentDurability));

                if (club.totalSPEarned < 0)
                    events.Add(Set(ref club.totalSPEarned, 0,
                                   ContentCatalogs.Clubs, club.clubId, nameof(club.totalSPEarned)));
            }

            ReportUndefined(ContentCatalogs.Clubs, undefined);
            return events;
        }

        /// <summary>
        /// Clamp every persisted character against its (possibly overlaid) definition.
        /// <para>
        /// The SP clamp is the rarity-downgrade case: a Legendary demoted to Rare drops the
        /// Strength cap 40 → 30, so allocated SP above the new ceiling is orphaned. It is clamped
        /// and logged. <b>Nothing is refunded</b> — see rule 3 in the file header.
        /// </para>
        /// </summary>
        public static List<ClampEvent> ClampCharacters(
            IList<PersistedCharacter>? characters,
            IReadOnlyDictionary<string, CharacterClampDefinition> definitions)
        {
            var events = new List<ClampEvent>();
            if (characters == null || characters.Count == 0) return events;

            var undefined = new List<string>();

            foreach (var character in characters)
            {
                if (character == null || string.IsNullOrEmpty(character.characterId)) continue;

                if (!definitions.TryGetValue(character.characterId, out CharacterClampDefinition def))
                {
                    undefined.Add(character.characterId);
                    continue;
                }

                LevelBounds(def.StartLevel, def.MaxLevel, out int lo, out int hi);
                if (character.currentLevel < lo || character.currentLevel > hi)
                    events.Add(Set(ref character.currentLevel, Clamp(character.currentLevel, lo, hi),
                                   ContentCatalogs.Characters, character.characterId,
                                   nameof(character.currentLevel)));

                ClampSp(events, ContentCatalogs.Characters, character.characterId,
                        def.MaxSpentStrength,    ref character.spentStrength,    nameof(character.spentStrength));
                ClampSp(events, ContentCatalogs.Characters, character.characterId,
                        def.MaxSpentClubControl, ref character.spentClubControl, nameof(character.spentClubControl));
                ClampSp(events, ContentCatalogs.Characters, character.characterId,
                        def.MaxSpentRecovery,    ref character.spentRecovery,    nameof(character.spentRecovery));
                ClampSp(events, ContentCatalogs.Characters, character.characterId,
                        def.MaxSpentStamina,     ref character.spentStamina,     nameof(character.spentStamina));

                if (character.totalSPEarned < 0)
                    events.Add(Set(ref character.totalSPEarned, 0,
                                   ContentCatalogs.Characters, character.characterId,
                                   nameof(character.totalSPEarned)));
            }

            ReportUndefined(ContentCatalogs.Characters, undefined);
            return events;
        }

        // ── Logging ───────────────────────────────────────────────────────────

        /// <summary>
        /// One <c>Debug.LogWarning</c> per clamp, naming id / field / old / new, plus one summary.
        /// <para>
        /// Warning and not Log: a clamp means a publish invalidated data a player already had. It is
        /// a designed, correct outcome, but it is never routine, and it must be greppable from a
        /// device log six weeks after the fact.
        /// </para>
        /// </summary>
        public static void LogAll(IReadOnlyList<ClampEvent> events, string context)
        {
            if (events == null || events.Count == 0)
            {
                Debug.Log($"{Tag} Clamp ({context}): nothing to clamp — every owned instance already " +
                          $"fits its catalog definition.");
                return;
            }

            foreach (var e in events)
                Debug.LogWarning(
                    $"{Tag} CLAMPED {e.Catalog} '{e.Id}': {e.Field} {e.OldValue} → {e.NewValue} " +
                    $"(a published change put the saved value outside the catalog's bounds). " +
                    $"No refund is issued — SPEC §2 out of scope.");

            Debug.LogWarning($"{Tag} Clamp ({context}): {events.Count} field(s) clamped across " +
                             $"{CountIds(events)} owned instance(s). The save is written back clamped.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Resolve the [lo, hi] level band. A definition with no usable maxLevel yields a band that
        /// clamps nothing — better than clamping every owned instance to 1 because a column was
        /// missing from a hand-seeded row.
        /// </summary>
        private static void LevelBounds(int startLevel, int maxLevel, out int lo, out int hi)
        {
            lo = Math.Max(1, startLevel);
            hi = maxLevel > 0 ? maxLevel : int.MaxValue;
            if (hi < lo) hi = lo;      // an inverted band is an authoring error; do not invert it back
        }

        private static void ClampSp(List<ClampEvent> events, string catalog, string id, int ceiling,
                                    ref int value, string field)
        {
            int max = Math.Max(0, ceiling);
            if (value >= 0 && value <= max) return;
            events.Add(Set(ref value, Clamp(value, 0, max), catalog, id, field));
        }

        private static ClampEvent Set(ref int slot, int newValue, string catalog, string id, string field)
        {
            int old = slot;
            slot = newValue;
            return new ClampEvent(catalog, id, field, old, newValue);
        }

        private static int Clamp(int value, int lo, int hi)
            => value < lo ? lo : (value > hi ? hi : value);

        private static int CountIds(IReadOnlyList<ClampEvent> events)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in events) ids.Add(e.Id);
            return ids.Count;
        }

        private static void ReportUndefined(string catalog, List<string> ids)
        {
            if (ids.Count == 0) return;

            // NOT an error. A save can name a club the catalog has since stopped carrying; the row
            // is still renderable from the save's own copy of maxDurability, and inventing bounds
            // for it would be a guess with a player's data.
            Debug.LogWarning(
                $"{Tag} Clamp: {ids.Count} owned {catalog} instance(s) have no catalog definition and " +
                $"were left untouched: {string.Join(", ", ids.GetRange(0, Math.Min(8, ids.Count)))}" +
                (ids.Count > 8 ? $", +{ids.Count - 8} more" : ""));
        }
    }
}
