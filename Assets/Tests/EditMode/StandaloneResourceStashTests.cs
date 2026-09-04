// Assets/Tests/EditMode/StandaloneResourceStashTests.cs
// gps_standalone_shell round 2 — the guard the Portraits regression earned.
//
// Build 2637 shipped with an EMPTY character roster because the standalone's Resources diet moved
// `Resources/Portraits` out, and `CharacterDatabaseCSV` gates every character's `renderable` flag
// on its portrait resolving — so the roster seeded from GetAvailableCharacters() came back empty,
// no selected character id resolved, and the GPS Avatar screen fell back to the placeholder.
//
// The R2 enumeration DID grep the Resources.Load call sites; it grepped the ones with LITERAL
// paths. `Portraits/Thumbnails` reaches Resources.Load as a const + a variable, so it was invisible
// to that grep. These tests read the constants themselves, which is the only form that cannot drift.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace GolfinRedux.Tests.EditMode
{
    public class StandaloneResourceStashTests
    {
        const string PreprocessorTypeName = "Golfin.EditorTools.StandaloneBuildPreprocessor";

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>The folders the standalone build moves out of <c>Assets/Resources</c>.</summary>
        static string[] MovedFolders()
        {
            var t = FindType(PreprocessorTypeName);
            Assert.IsNotNull(t, $"{PreprocessorTypeName} not found — did Assembly-CSharp-Editor compile?");
            var f = t.GetField("GolfOnlyResourceFolders", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, "StandaloneBuildPreprocessor.GolfOnlyResourceFolders not found.");
            return (string[])f.GetValue(null);
        }

        /// <summary>
        /// Every <c>Resources</c> path a catalog resolves its art from, read off the CONSTANTS
        /// rather than restated here — a renamed or added path is picked up without editing this
        /// file, which is the whole point.
        /// </summary>
        static IEnumerable<(string owner, string field, string path)> CatalogArtPathConstants()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException e) { types = e.Types.Where(x => x != null).ToArray(); }

                foreach (var t in types)
                {
                    if (t == null || !t.Name.EndsWith("DatabaseCSV", StringComparison.Ordinal)) continue;

                    foreach (var f in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public))
                    {
                        if (!f.IsLiteral || f.FieldType != typeof(string)) continue;

                        // ANY const whose name ends in "Path", not just "ResourcesPath". The five
                        // catalogs do not share one naming convention — CharacterDatabaseCSV has
                        // ThumbnailResourcesPath/FullBodyResourcesPath while Club/Ball/Item/Bag use
                        // PortraitPath, FullPath, ControlPath, ThumbnailPath. A test that matched
                        // only the first spelling would have covered exactly the one catalog that
                        // already broke and none of the four that could break next, which is the
                        // same too-narrow-pattern mistake that let this bug through in the first
                        // place.
                        if (!f.Name.EndsWith("Path", StringComparison.Ordinal)) continue;

                        var value = (string)f.GetRawConstantValue();
                        if (string.IsNullOrEmpty(value) || !value.Contains("/")) continue;
                        yield return (t.Name, f.Name, value);
                    }
                }
            }
        }

        [Test]
        public void TheCatalogArtPathConstants_AreDiscoverable()
        {
            // If this fails the other test is vacuous — it would be asserting over an empty set.
            var found = CatalogArtPathConstants().ToArray();
            Assert.IsNotEmpty(found,
                "No *ResourcesPath constants found on any *DatabaseCSV type. Either the naming " +
                "convention changed or reflection is looking in the wrong place — fix this before " +
                "trusting the stash test below, which would otherwise pass by finding nothing.");

            // The one that caused the outage, named explicitly so a rename is a visible edit here.
            CollectionAssert.Contains(found.Select(x => x.path).ToArray(), "Portraits/Thumbnails",
                "CharacterDatabaseCSV.ThumbnailResourcesPath is the constant this whole test exists for.");
        }

        /// <summary>
        /// Catalog-to-stashed-folder collisions that have been LOOKED AT and accepted, with the
        /// reason. Every one of these catalogs gates its rows on art resolving, so in the shell it
        /// comes up empty — which is harmless only because nothing the shell can reach reads it
        /// (verified by grepping the GPS screens, the auth screens, PersistentUIManager and the
        /// Settings modal for each manager: no hits).
        ///
        /// <para>The point of an allowlist rather than a blanket rule: a NEW catalog, or a new
        /// folder added to the stash, produces a collision nobody has judged — and that fails,
        /// loudly, until someone does. `Portraits` is deliberately absent: it is the one whose
        /// catalog the GPS Avatar screen reads, and stashing it is what broke build 2637.</para>
        /// </summary>
        static readonly Dictionary<string, string> AcceptedCollisions = new Dictionary<string, string>
        {
            ["ClubDatabaseCSV"] = "clubs are read only by golf screens StandaloneGate refuses",
            ["BallDatabaseCSV"] = "balls are read only by the shot UI, absent from the shell",
            ["ItemDatabaseCSV"] = "items are read only by the inventory screens",
            ["BagDatabaseCSV"]  = "bags are read only by the inventory screens",
        };

        [Test]
        public void EveryCatalogArtFolderTheStandaloneStashes_HasBeenJudged()
        {
            string[] moved = MovedFolders();
            var unjudged = new List<string>();

            foreach (var (owner, field, path) in CatalogArtPathConstants())
            {
                // "Portraits/Thumbnails" -> "Portraits": the stash moves TOP-LEVEL folders.
                string root = path.Split('/')[0];
                if (!moved.Contains(root, StringComparer.Ordinal)) continue;
                if (AcceptedCollisions.ContainsKey(owner)) continue;

                unjudged.Add($"{owner}.{field} = \"{path}\" resolves under Resources/{root}, which the " +
                             $"standalone build stashes — and nobody has recorded whether the shell needs it");
            }

            Assert.IsEmpty(unjudged,
                "A catalog resolves its art from a folder the standalone build moves out of Resources, " +
                "and that collision is not in AcceptedCollisions. This is not cosmetic: *DatabaseCSV " +
                "marks a row `renderable` only when its art resolves, CharacterManager seeds the roster " +
                "from the RENDERABLE view, and an empty roster means no selected character — which is " +
                "exactly how build 2637 shipped with a placeholder avatar figure and blank stats.\n" +
                "Decide, then either drop the folder from GolfOnlyResourceFolders or add the catalog " +
                "to AcceptedCollisions with the reason.\n  " + string.Join("\n  ", unjudged));
        }

        [Test]
        public void ThePortraitsFolder_IsNeverStashed_BecauseTheGpsAvatarScreenDependsOnIt()
        {
            // The regression itself, pinned as narrowly as it deserves:
            //   GpsAvatarScreenController -> CharacterManager.GetSelectedCharacterId()
            //   CharacterManager:86       -> CharacterDatabaseCSV.GetAvailableCharacters()
            //   CharacterDatabaseCSV:421  -> Where(isActive && renderable)
            //   CharacterDatabaseCSV:348  -> renderable = portraitSprite != null  (Portraits/Thumbnails)
            CollectionAssert.DoesNotContain(MovedFolders(), "Portraits",
                "Stashing Resources/Portraits makes every character unrenderable, which empties the " +
                "roster, which leaves no selected character — and the GPS Avatar screen then shows " +
                "the placeholder figure with no stats. Build 2637 shipped that way.");

            Assert.IsFalse(AcceptedCollisions.ContainsKey("CharacterDatabaseCSV"),
                "CharacterDatabaseCSV must never be an ACCEPTED collision — the shell reads it.");
        }

        [Test]
        public void TheGpsAvatarFigureFolderIsNeverStashed()
        {
            // GpsAvatarScreenController.BindCharacterFigure loads "Characters/Homescreen/{name}".
            // A PLAYLIFE screen reading golf art — the first thing the R2 enumeration saved, and
            // worth pinning so it cannot be undone by someone tidying the list.
            CollectionAssert.DoesNotContain(MovedFolders(), "Characters",
                "The GPS Avatar screen renders Characters/Homescreen/{name}; stashing that folder " +
                "blanks a PLAYLIFE screen.");
        }
    }
}
