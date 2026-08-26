#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Content;

namespace Golfin.Roster
{
    /// <summary>
    /// CSV-driven character database.
    /// Loads character data from Characters.csv at runtime and merges the admin-published
    /// <c>characters</c> overlay on top of it (content_overlay_catalogs §1).
    /// Much easier to edit and balance than ScriptableObjects.
    ///
    /// <para>
    /// Execution order -200 (from <c>CharacterDatabaseCSV.cs.meta</c>), i.e. ahead of
    /// CharacterManager's -100 and behind ContentService's -900.
    /// <see cref="IsLoaded"/> is the runtime assert CharacterManager checks rather than trusting
    /// that ordering — see the note in <c>ContentService</c>'s header.
    /// </para>
    /// </summary>
    public class CharacterDatabaseCSV : MonoBehaviour
    {
        public static CharacterDatabaseCSV Instance { get; private set; }

        [Header("CSV File")]
        [SerializeField] private TextAsset charactersCSV;

        /// <summary>True once the CSV has produced at least one row. See the class remarks.</summary>
        public bool IsLoaded { get; private set; }

        /// <summary>How many rows the characters overlay patched or appended. Diagnostics only.</summary>
        public int OverlaidRowCount { get; private set; }

        // Sprites are loaded at runtime from Resources/Portraits/ — no Inspector assignment needed.
        private const string ThumbnailResourcesPath = "Portraits/Thumbnails";
        private const string FullBodyResourcesPath  = "Portraits/FullBody";

        private Dictionary<string, CharacterDataRuntime> characterMap = new Dictionary<string, CharacterDataRuntime>();
        private List<CharacterDataRuntime> allCharacters = new List<CharacterDataRuntime>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadCharactersFromCSV();
        }

        private void LoadCharactersFromCSV()
        {
            if (charactersCSV == null)
            {
                Debug.LogError("[CharacterDatabaseCSV] charactersCSV is null! Please assign Characters.csv");
                return;
            }

            characterMap.Clear();
            allCharacters.Clear();
            IsLoaded = false;
            OverlaidRowCount = 0;

            // Logs an ERROR when ContentService exists but has not installed the store yet (i.e.
            // this database's execution order is ahead of -900); stays quiet when there is no
            // ContentService at all, which is a lab / EditMode scene correctly running bundled.
            ContentCatalog? overlay = ContentCatalogStore.RequireReady(nameof(CharacterDatabaseCSV))
                ? ContentCatalogStore.Catalog(ContentCatalogs.Characters)
                : null;

            string[] lines = charactersCSV.text.Split('\n');

            // Parse header to get column indices
            if (lines.Length < 2)
            {
                Debug.LogError("[CharacterDatabaseCSV] CSV is empty or has no data rows");
                return;
            }

            var headers = ParseCSVLine(lines[0]);
            var headerIndex = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
            {
                headerIndex[headers[i].Trim()] = i;
            }

            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            int vetoed = 0, dropped = 0, deactivated = 0;

            // Parse data rows
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var fields = ParseCSVLine(line);

                // Bundled first — the id has to exist before the overlay can be looked up, and the
                // bundled row is also what SPEC §5's sprite veto reverts to.
                var bundled = ParseCharacterFromCSV(ContentFields.Csv(fields, headerIndex));
                if (bundled == null) continue;

                seen.Add(bundled.characterId);

                ContentRow? patch = null;
                overlay?.ById.TryGetValue(bundled.characterId, out patch);

                var character = bundled;
                if (patch != null)
                {
                    var merged = ParseCharacterFromCSV(ContentFields.Csv(fields, headerIndex, patch));
                    if (merged != null)
                    {
                        // SPEC §5 — only names the overlay CHANGED are guarded.
                        string? unresolved = ContentSpriteGuard.FirstUnresolvedChange(new[]
                        {
                            new SpriteRef(ThumbnailResourcesPath, bundled.portraitSpriteName,     merged.portraitSpriteName),
                            new SpriteRef(FullBodyResourcesPath,  bundled.portraitFullSpriteName, merged.portraitFullSpriteName),
                        });

                        if (unresolved != null)
                        {
                            ContentSpriteGuard.LogVeto(ContentCatalogs.Characters, bundled.characterId,
                                                       unresolved, appended: false);
                            vetoed++;
                        }
                        else
                        {
                            character = merged;
                            OverlaidRowCount++;
                        }
                    }
                }

                if (!character.isActive) deactivated++;
                characterMap[character.characterId] = character;
                allCharacters.Add(character);
            }

            // Append overlay rows Characters.csv has never carried.
            if (overlay != null)
            {
                foreach (var row in overlay.Rows)
                {
                    if (seen.Contains(row.Id)) continue;

                    var appended = ParseCharacterFromCSV(ContentFields.OverlayOnly(row));
                    if (appended == null) continue;

                    // An appended row has no bundled counterpart to fall back to, so EVERY sprite
                    // it names is guarded and a miss drops the row: a blank portrait in the
                    // carousel is worse than an absent character.
                    string? unresolved = ContentSpriteGuard.FirstUnresolved(new[]
                    {
                        Path(ThumbnailResourcesPath, appended.portraitSpriteName),
                        Path(FullBodyResourcesPath,  appended.portraitFullSpriteName),
                    });

                    if (unresolved != null)
                    {
                        ContentSpriteGuard.LogVeto(ContentCatalogs.Characters, appended.characterId,
                                                   unresolved, appended: true);
                        dropped++;
                        continue;
                    }

                    if (!appended.isActive) deactivated++;
                    characterMap[appended.characterId] = appended;
                    allCharacters.Add(appended);
                    OverlaidRowCount++;
                }
            }

            IsLoaded = allCharacters.Count > 0;

            Debug.Log($"[CharacterDatabaseCSV] Loaded {allCharacters.Count} characters from CSV" +
                      (overlay == null
                          ? " — BUNDLED only, no characters overlay this launch."
                          : $" — overlay v{overlay.Version}: {OverlaidRowCount} row(s) patched/appended, " +
                            $"{deactivated} deactivated (still owned + renderable, I6), " +
                            $"{vetoed} reverted to bundled and {dropped} dropped by the sprite veto (§5)."));
        }

        private static string Path(string folder, string name)
            => string.IsNullOrEmpty(name) ? string.Empty : folder + "/" + name;

        /// <summary>
        /// Parse a CSV line handling quoted fields (which may contain commas)
        /// </summary>
        private List<string> ParseCSVLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // Handle escaped quotes ""
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++; // skip next quote
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        /// <summary>
        /// One row from whatever <see cref="ContentFields"/> is standing in front of it: a bundled
        /// CSV row, a bundled row patched by an overlay, or an overlay row on its own. The column
        /// names and defaults are declared ONCE, here, so a published row and a bundled row can
        /// never diverge on how a column is read (I4).
        /// </summary>
        private CharacterDataRuntime? ParseCharacterFromCSV(ContentFields f)
        {
            try
            {
                string id = f.Get("id");
                if (string.IsNullOrEmpty(id)) return null;

                var character = new CharacterDataRuntime
                {
                    characterId = id,
                    characterName = f.Get("name"),
                    characterLastName = f.Get("lastName"),
                    rarity = ParseRarity(f.Get("rarity", "Common")),
                    baseStrength = f.GetInt("baseStrength", 10),
                    baseClubControl = f.GetInt("baseClubControl", 10),
                    baseRecovery = f.GetInt("baseRecovery", 10),
                    baseStamina = f.GetInt("baseStamina", 10),
                    portraitSpriteName = f.Get("portraitSprite"),
                    portraitFullSpriteName = f.Get("portraitFull"),
                    startLevel = f.GetInt("startLevel", 0),
                    maxLevel = f.GetInt("maxLevel", 199),
                    bio = f.Get("bio"),
                    starterCandidate = f.GetInt("starterCandidate", 0) == 1,
                    isActive = f.IsActive
                };

                // Find sprites by name
                character.portraitSprite = FindSpriteByName(character.portraitSpriteName);
                character.portraitFullSprite = FindFullBodySpriteByName(character.portraitFullSpriteName);

                return character;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CharacterDatabaseCSV] Failed to parse character: {e.Message}");
                return null;
            }
        }

        private CharacterRarity ParseRarity(string rarityStr)
        {
            return rarityStr.ToLower() switch
            {
                "common" => CharacterRarity.Common,
                "uncommon" => CharacterRarity.Uncommon,
                "rare" => CharacterRarity.Rare,
                "mythic" => CharacterRarity.Mythic,
                "legendary" => CharacterRarity.Legendary,
                "supreme" => CharacterRarity.Supreme,
                _ => CharacterRarity.Common
            };
        }

        private Sprite? FindSpriteByName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            var sprite = Resources.Load<Sprite>($"{ThumbnailResourcesPath}/{spriteName}");
            if (sprite == null)
                Debug.LogWarning($"[CharacterDatabaseCSV] Thumbnail sprite '{spriteName}' not found in Resources/{ThumbnailResourcesPath}/");
            return sprite;
        }

        private Sprite? FindFullBodySpriteByName(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;

            var sprite = Resources.Load<Sprite>($"{FullBodyResourcesPath}/{spriteName}");
            if (sprite == null)
                Debug.LogWarning($"[CharacterDatabaseCSV] Full-body sprite '{spriteName}' not found in Resources/{FullBodyResourcesPath}/");
            return sprite;
        }

        /// <summary>
        /// Get character data by ID
        /// </summary>
        public CharacterDataRuntime? GetCharacter(string characterId)
        {
            if (characterMap.TryGetValue(characterId, out var data))
                return data;

            Debug.LogWarning($"[CharacterDatabaseCSV] Character {characterId} not found");
            return null;
        }

        /// <summary>
        /// EVERY character row, deactivated ones included. This is the roster / detail-panel view:
        /// I6 says a deactivated character stays fully renderable for a player who owns one.
        /// </summary>
        public List<CharacterDataRuntime> GetAllCharacters()
        {
            return allCharacters.ToList();
        }

        /// <summary>
        /// Only rows an operator has left ACTIVE — the "available" view for gacha pools and
        /// anything else that can hand a player a NEW character (I6).
        /// </summary>
        public List<CharacterDataRuntime> GetAvailableCharacters()
        {
            return allCharacters.Where(c => c.isActive).ToList();
        }
    }

    /// <summary>
    /// Runtime character data (loaded from CSV)
    /// Lightweight alternative to ScriptableObject CharacterData
    /// </summary>
    public class CharacterDataRuntime
    {
        public string characterId = "";
        public string characterName = "";
        public string characterLastName = "";
        public CharacterRarity rarity = CharacterRarity.Common;
        public int baseStrength = 10;
        public int baseClubControl = 10;
        public int baseRecovery = 10;
        public int baseStamina = 10;
        public string portraitSpriteName = "";
        public Sprite? portraitSprite = null;
        public string portraitFullSpriteName = "";
        public Sprite? portraitFullSprite = null;

        /// <summary>
        /// Characters.csv <c>startLevel</c>. 0 means the column was absent, in which case callers
        /// fall back to the rarity table (<c>CharacterManager.GetStartingLevel</c>). It is the lower
        /// bound of the level clamp in <c>ContentClamp.ClampCharacters</c>.
        /// </summary>
        public int startLevel = 0;

        public int maxLevel = 199;

        /// <summary>
        /// I6 — <b>deactivated, never deleted</b>. False means: gone from gacha pools and any
        /// "available" list, but still fully renderable in the roster of a player who owns one, and
        /// still selectable if it was selected. Never a reason to drop the row.
        /// </summary>
        public bool isActive = true;
        /// <summary>True if this character can be chosen as the player's starter.</summary>
        public bool starterCandidate = false;
        public string bio = "";

        public Color GetRarityColor() => RarityHelper.GetRarityColor(rarity);
        public string GetRarityLabel() => RarityHelper.GetRarityLabel(rarity);

        /// <summary>
        /// Localization key for this character's bio, e.g. "char_james" -> "CHAR_BIO_JAMES".
        /// Null when the id is empty so callers fall back to the CSV English <see cref="bio"/>.
        /// </summary>
        public string? BioLocalizationKey =>
            string.IsNullOrEmpty(characterId)
                ? null
                : "CHAR_BIO_" + (characterId.StartsWith("char_") ? characterId.Substring(5) : characterId).ToUpperInvariant();

        /// <summary>Localization key for the character's first name, e.g. "CHAR_NAME_JAMES".</summary>
        public string? NameLocalizationKey =>
            string.IsNullOrEmpty(characterId)
                ? null
                : "CHAR_NAME_" + (characterId.StartsWith("char_") ? characterId.Substring(5) : characterId).ToUpperInvariant();

        /// <summary>Localization key for the character's last name, e.g. "CHAR_LASTNAME_JAMES".</summary>
        public string? LastNameLocalizationKey =>
            string.IsNullOrEmpty(characterId)
                ? null
                : "CHAR_LASTNAME_" + (characterId.StartsWith("char_") ? characterId.Substring(5) : characterId).ToUpperInvariant();

        /// <summary>
        /// Get display name formatted as "FIRSTNAME\nLASTNAME" for the detail panel (unlocalized).
        /// </summary>
        public string GetDisplayName()
        {
            return string.IsNullOrEmpty(characterLastName)
                ? characterName.ToUpper()
                : $"{characterName.ToUpper()}\n{characterLastName.ToUpper()}";
        }

        /// <summary>
        /// Get localized display name. Falls back to CSV English names when no key is found.
        /// Format for detail panel (two-line): "FIRSTNAME\nLASTNAME".
        /// Format for card (single-line): "FIRSTNAME".
        /// </summary>
        public string GetLocalizedDisplayName(bool singleLine = false)
        {
            string firstName = characterName;
            string lastName  = characterLastName;

            if (NameLocalizationKey != null)
            {
                string loc = LocalizationManager.Get(NameLocalizationKey);
                // Only use the localized value when the manager returned something different from the key
                if (!string.IsNullOrEmpty(loc) && loc != NameLocalizationKey)
                    firstName = loc;
            }
            if (!singleLine && LastNameLocalizationKey != null)
            {
                string loc = LocalizationManager.Get(LastNameLocalizationKey);
                if (!string.IsNullOrEmpty(loc) && loc != LastNameLocalizationKey)
                    lastName = loc;
            }

            if (singleLine)
                return firstName.ToUpper();

            return string.IsNullOrEmpty(lastName)
                ? firstName.ToUpper()
                : $"{firstName.ToUpper()}\n{lastName.ToUpper()}";
        }

        public override string ToString()
        {
            return $"{characterName} ({rarity}): STR={baseStrength}, CTRL={baseClubControl}, REC={baseRecovery}, STAM={baseStamina}";
        }
    }
}
