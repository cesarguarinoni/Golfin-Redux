// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the wire format: deltas from the catalog default.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §1
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Save;
using Newtonsoft.Json.Linq;

namespace Golfin.InventorySync
{
    /// <summary>
    /// <see cref="InventorySnapshot"/> ↔ the JSON stored in <c>profiles.golfin_inventory</c>.
    ///
    /// <para>
    /// THE ENTIRE COST CONSTRAINT LIVES IN THIS FILE (SPEC §1). "Keep save data to a minimum" has
    /// been Cesar's requirement since day one, and the shape that satisfies it is: <b>a club at its
    /// catalog default is written as a bare id string</b>, and only fields that DIFFER from the
    /// default are written at all. A tester who owns 40 starter-state clubs and levelled two of them
    /// pays 40 short strings plus two small objects, not 40 ten-field records.
    /// </para>
    ///
    /// <para>
    /// AND IT BUYS SOMETHING BEYOND BYTES. A bare id is not a frozen copy of today's default — it is
    /// a reference to whatever the catalog says at DECODE time. So a published rebalance (a new
    /// starting level, a new max durability) reaches every untouched instance for free, with no
    /// migration and no server write, while a club the player actually levelled keeps the level they
    /// earned. That is the same I1/I5 relationship the content overlay already has with the bundled
    /// CSVs.
    /// </para>
    ///
    /// <para>
    /// KEYS ARE SHORT ON PURPOSE (<c>lv</c>, <c>dur</c>, <c>slot</c>). At one blob per player this is
    /// noise; the reason to do it anyway is that the blob is stored per row on <c>profiles</c>, which
    /// the admin list query selects, and the field names are pure repetition across every owned row.
    /// They are FROZEN once a build ships — <see cref="FormatVersion"/> is what a future change goes
    /// through.
    /// </para>
    ///
    /// <para>
    /// NEWTONSOFT LINQ (<c>JObject</c>), NOT ATTRIBUTE SERIALISATION, because "omit this field when
    /// it equals a value computed from another object" is not something attributes can say. The
    /// alternative — a custom <c>JsonConverter</c> per type — is the same code with a harder-to-read
    /// signature.
    /// </para>
    /// </summary>
    public static class InventoryCodec
    {
        /// <summary>Wire-format version, written as <c>v</c>. Bumped only if a key's MEANING
        /// changes; adding a new optional key does not need it (a decoder ignores what it does not
        /// know, and an encoder omits what is at default).</summary>
        public const int FormatVersion = 1;

        // Frozen wire keys.
        private const string KVersion   = "v";
        private const string KClubs     = "clubs";
        private const string KChars     = "characters";
        private const string KItems     = "items";
        private const string KBalls     = "balls";
        private const string KTickets   = "tickets";
        private const string KHoles     = "holes";
        private const string KStarter   = "starter";
        private const string KSelected  = "selected";
        private const string KId        = "id";

        // ── Encode ────────────────────────────────────────────────────────────

        /// <summary>Serialise a snapshot, compressing every row that matches its catalog default.</summary>
        public static string Encode(InventorySnapshot snap, IInventoryCatalog? catalog)
            => EncodeToObject(snap, catalog).ToString(Newtonsoft.Json.Formatting.None);

        public static JObject EncodeToObject(InventorySnapshot snap, IInventoryCatalog? catalog)
        {
            catalog ??= EmptyInventoryCatalog.Instance;
            var root = new JObject { [KVersion] = FormatVersion };

            var clubs = new JArray();
            foreach (var c in snap.Clubs)
            {
                if (c == null || string.IsNullOrEmpty(c.clubId)) continue;
                clubs.Add(EncodeClub(c, catalog));
            }
            if (clubs.Count > 0) root[KClubs] = clubs;

            var chars = new JArray();
            foreach (var c in snap.Characters)
            {
                if (c == null || string.IsNullOrEmpty(c.characterId)) continue;
                chars.Add(EncodeCharacter(c, catalog));
            }
            if (chars.Count > 0) root[KChars] = chars;

            if (snap.Items.Count > 0)
            {
                var items = new JObject();
                foreach (var kv in snap.Items) items[kv.Key] = kv.Value;
                root[KItems] = items;
            }

            if (snap.Balls.Count > 0)
            {
                var balls = new JObject();
                foreach (var kv in snap.Balls) balls[kv.Key] = kv.Value;
                root[KBalls] = balls;
            }

            if (snap.Tickets.Count > 0)
            {
                var tickets = new JObject();
                foreach (var kv in snap.Tickets) tickets[kv.Key.ToString()] = kv.Value;
                root[KTickets] = tickets;
            }

            if (snap.UnlockedHoles.Count > 0)
            {
                var holes = new JArray();
                foreach (int h in snap.UnlockedHoles) holes.Add(h);
                root[KHoles] = holes;
            }

            if (!string.IsNullOrEmpty(snap.StarterCharacterId))  root[KStarter]  = snap.StarterCharacterId;
            if (!string.IsNullOrEmpty(snap.SelectedCharacterId)) root[KSelected] = snap.SelectedCharacterId;

            return root;
        }

        /// <summary>A bare id string when every field matches the catalog default, otherwise an
        /// object carrying <c>id</c> plus only the fields that differ.</summary>
        private static JToken EncodeClub(PersistedClub c, IInventoryCatalog catalog)
        {
            bool known = catalog.TryGetClubDefault(c.clubId, out var d);
            var o = new JObject { [KId] = c.clubId };

            Put(o, "lv",     c.currentLevel,       known ? d.currentLevel : int.MinValue);
            Put(o, "dur",    c.currentDurability,  known ? d.currentDurability : int.MinValue);
            Put(o, "maxDur", c.maxDurability,      known ? d.maxDurability : int.MinValue);
            Put(o, "slot",   c.equippedBagSlot,    known ? d.equippedBagSlot : int.MinValue);
            Put(o, "sp",     c.totalSPEarned,      known ? d.totalSPEarned : int.MinValue);
            Put(o, "sPow",   c.spentPower,         known ? d.spentPower : int.MinValue);
            Put(o, "sAcc",   c.spentAccuracy,      known ? d.spentAccuracy : int.MinValue);
            Put(o, "sLie",   c.spentLieResistance, known ? d.spentLieResistance : int.MinValue);
            Put(o, "sDur",   c.spentDurability,    known ? d.spentDurability : int.MinValue);

            // Exactly the id and nothing else → the whole row IS the default → write the id alone.
            return o.Count == 1 ? (JToken)new JValue(c.clubId) : o;
        }

        private static JToken EncodeCharacter(PersistedCharacter c, IInventoryCatalog catalog)
        {
            bool known = catalog.TryGetCharacterDefault(c.characterId, out var d);
            var o = new JObject { [KId] = c.characterId };

            Put(o, "lv",   c.currentLevel,     known ? d.currentLevel : int.MinValue);
            Put(o, "sp",   c.totalSPEarned,    known ? d.totalSPEarned : int.MinValue);
            Put(o, "sStr", c.spentStrength,    known ? d.spentStrength : int.MinValue);
            Put(o, "sCc",  c.spentClubControl, known ? d.spentClubControl : int.MinValue);
            Put(o, "sRec", c.spentRecovery,    known ? d.spentRecovery : int.MinValue);
            Put(o, "sSta", c.spentStamina,     known ? d.spentStamina : int.MinValue);

            // `own` is written only when FALSE. The default for a row in ownedCharacters is owned,
            // so a locked-with-progress row is the one that pays for itself. Writing `own:true`
            // everywhere would undo the compression on the most common row in the list.
            if (!c.isOwned) o["own"] = false;

            return o.Count == 1 ? (JToken)new JValue(c.characterId) : o;
        }

        /// <summary>Write <paramref name="value"/> under <paramref name="key"/> unless it equals the
        /// default. <c>int.MinValue</c> as the default means "no catalog entry" — nothing can equal
        /// it, so an unknown id encodes in full rather than against a guess.</summary>
        private static void Put(JObject o, string key, int value, int def)
        {
            if (value != def) o[key] = value;
        }

        // ── Decode ────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-expand a stored blob. Never throws on shape: a field of the wrong type, a null entry
        /// or an unparseable row is skipped, because the caller's alternative to a partial restore
        /// is no restore at all.
        /// </summary>
        public static InventorySnapshot Decode(string? json, IInventoryCatalog? catalog)
        {
            if (string.IsNullOrWhiteSpace(json)) return new InventorySnapshot();
            JObject root;
            try { root = JObject.Parse(json); }
            catch { return new InventorySnapshot(); }
            return Decode(root, catalog);
        }

        public static InventorySnapshot Decode(JObject? root, IInventoryCatalog? catalog)
        {
            var snap = new InventorySnapshot();
            if (root == null) return snap;
            catalog ??= EmptyInventoryCatalog.Instance;

            if (root[KClubs] is JArray clubs)
                foreach (var token in clubs)
                {
                    var club = DecodeClub(token, catalog);
                    if (club != null) snap.Clubs.Add(club);
                }

            if (root[KChars] is JArray chars)
                foreach (var token in chars)
                {
                    var c = DecodeCharacter(token, catalog);
                    if (c != null) snap.Characters.Add(c);
                }

            ReadInts(root[KItems] as JObject, snap.Items);
            ReadInts(root[KBalls] as JObject, snap.Balls);

            if (root[KTickets] is JObject tickets)
                foreach (var kv in tickets)
                    if (int.TryParse(kv.Key, out int kind) && TryInt(kv.Value, out int bal))
                        snap.Tickets[kind] = bal;

            if (root[KHoles] is JArray holes)
                foreach (var h in holes)
                    if (TryInt(h, out int n) && !snap.UnlockedHoles.Contains(n))
                        snap.UnlockedHoles.Add(n);
            snap.UnlockedHoles.Sort();

            snap.StarterCharacterId  = (string?)root[KStarter]  ?? "";
            snap.SelectedCharacterId = (string?)root[KSelected] ?? "";

            return snap;
        }

        private static PersistedClub? DecodeClub(JToken token, IInventoryCatalog catalog)
        {
            string id = token.Type == JTokenType.String
                ? (string?)token ?? ""
                : (string?)token[KId] ?? "";
            if (string.IsNullOrEmpty(id)) return null;

            PersistedClub club;
            if (catalog.TryGetClubDefault(id, out var d))
            {
                club = InventoryProjector.CloneClub(d);
            }
            else
            {
                // ⚠️ AN UNKNOWN ID IS STILL OWNED. The catalog can genuinely not know an id — a
                // club deactivated after this blob was written, or a save restored onto an older
                // build. Dropping the row here would be the one subtraction in the whole feature,
                // and it would be silent. So the row survives with a structural zero-state and the
                // decode carries on; a bare-id row of an unknown club is the only case where that
                // state is all we have, and I6 ("nothing is ever deleted, only deactivated") means
                // it is a case the catalog is supposed to keep answering anyway.
                club = new PersistedClub { clubId = id };
            }
            club.clubId = id;

            if (token is JObject o)
            {
                Read(o, "lv",     ref club.currentLevel);
                Read(o, "dur",    ref club.currentDurability);
                Read(o, "maxDur", ref club.maxDurability);
                Read(o, "slot",   ref club.equippedBagSlot);
                Read(o, "sp",     ref club.totalSPEarned);
                Read(o, "sPow",   ref club.spentPower);
                Read(o, "sAcc",   ref club.spentAccuracy);
                Read(o, "sLie",   ref club.spentLieResistance);
                Read(o, "sDur",   ref club.spentDurability);
            }
            return club;
        }

        private static PersistedCharacter? DecodeCharacter(JToken token, IInventoryCatalog catalog)
        {
            string id = token.Type == JTokenType.String
                ? (string?)token ?? ""
                : (string?)token[KId] ?? "";
            if (string.IsNullOrEmpty(id)) return null;

            PersistedCharacter c = catalog.TryGetCharacterDefault(id, out var d)
                ? InventoryProjector.CloneCharacter(d)
                : new PersistedCharacter { characterId = id, isOwned = true };
            c.characterId = id;

            if (token is JObject o)
            {
                Read(o, "lv",   ref c.currentLevel);
                Read(o, "sp",   ref c.totalSPEarned);
                Read(o, "sStr", ref c.spentStrength);
                Read(o, "sCc",  ref c.spentClubControl);
                Read(o, "sRec", ref c.spentRecovery);
                Read(o, "sSta", ref c.spentStamina);
                if (o["own"] != null) c.isOwned = (bool?)o["own"] ?? true;
            }
            return c;
        }

        private static void ReadInts(JObject? src, IDictionary<string, int> into)
        {
            if (src == null) return;
            foreach (var kv in src)
                if (!string.IsNullOrEmpty(kv.Key) && TryInt(kv.Value, out int n))
                    into[kv.Key] = n;
        }

        private static void Read(JObject o, string key, ref int field)
        {
            if (TryInt(o[key], out int n)) field = n;
        }

        private static bool TryInt(JToken? token, out int value)
        {
            value = 0;
            if (token == null) return false;
            if (token.Type == JTokenType.Integer) { value = (int)token; return true; }
            if (token.Type == JTokenType.Float)   { value = (int)(double)token; return true; }
            return token.Type == JTokenType.String && int.TryParse((string?)token, out value);
        }
    }
}
