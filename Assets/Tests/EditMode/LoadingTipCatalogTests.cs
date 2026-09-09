// ─────────────────────────────────────────────────────────────────────────────
// loading_tips — the SHIPPED catalog, not a fixture.
//
// This fixture is the reason the index-matched Sprite[] is gone: it holds the CSV,
// the PNGs on disk and the card's wiring to one another, so "34 rows, 34 sprites,
// every name matching" is checked on every EditMode run instead of by eye.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfinRedux.Tests.EditMode
{
    [TestFixture]
    public class LoadingTipCatalogTests
    {
        const string CsvPath   = "Assets/Resources/Data/LoadingTips.csv";
        const string ArtFolder = "Assets/Art/LoadingScreen";
        const string ScenePath = "Assets/Scenes/ShellScene.unity";

        static IList Rows() => Tips.Parse(File.ReadAllText(CsvPath));

        static List<object> RowList() => Rows().Cast<object>().ToList();

        [Test]
        public void ShippedCsv_HasTheSpeccedShape()
        {
            List<object> rows = RowList();

            Assert.That(rows.Count, Is.EqualTo(34), "§2.2 ships 34 tips");
            Assert.That(rows.Count(Tips.First), Is.EqualTo(8), "§2.1 first pool is 8 tips");
            Assert.That(rows.Count(r => !Tips.Active(r)), Is.EqualTo(2),
                "§2.3 — TIP_STORE and TIP_REPAIR ship active=0 until their systems land");

            CollectionAssert.AreEqual(
                new[] { "TIP_SWING", "TIP_ACCURACY", "TIP_GRADES", "TIP_VIEW",
                        "TIP_CLUB", "TIP_FORECAST", "TIP_RARITIES", "TIP_CONTROLS" },
                rows.Where(Tips.First).OrderBy(Tips.Order).Select(Tips.Key).ToArray(),
                "the first pool is the tutorial order from §2.2");

            CollectionAssert.AreEquivalent(new[] { "TIP_STORE", "TIP_REPAIR" },
                rows.Where(r => !Tips.Active(r)).Select(Tips.Key).ToArray());

            CollectionAssert.AllItemsAreUnique(rows.Select(Tips.Key).ToArray());
            CollectionAssert.AllItemsAreUnique(rows.Select(Tips.Order).ToArray());
        }

        [Test]
        public void EveryRow_NamesItsOwnSprite()
        {
            foreach (object r in RowList())
            {
                string key = Tips.Key(r);
                Assert.That(Tips.Sprite(r), Is.EqualTo("Tip_" + key.Substring("TIP_".Length)),
                    "§3.1 — sprite is `Tip_` + the key without its prefix, for " + key);
            }
        }

        [Test]
        public void EveryRow_HasAPngOnDisk_AndThereAreNoStrays()
        {
            string[] files = Directory.GetFiles(ArtFolder, "Tip_*.png")
                                      .Select(Path.GetFileNameWithoutExtension)
                                      .OrderBy(n => n, StringComparer.Ordinal)
                                      .ToArray()!;

            string[] wanted = RowList().Select(Tips.Sprite)
                                       .OrderBy(n => n, StringComparer.Ordinal).ToArray();

            Assert.That(files.Length, Is.EqualTo(34),
                "§3.6 — exactly one authored diagram per tip: " + string.Join(", ", files));
            CollectionAssert.AreEqual(wanted, files,
                "a CSV row with no PNG (or a PNG no row names) — the two lists must agree exactly");
        }

        [Test]
        public void OldTipArt_IsGone()
        {
            // §3.6 — the 2025 set is superseded by the authored exports. A leftover would be an
            // orphan nothing can reach, exactly like `Tip Leaderboard.png` was.
            // "Tip Card Background.png" is the CARD's own panel art, not a diagram — it stays.
            string[] stale = Directory.GetFiles(ArtFolder, "Tip *.png")
                                      .Select(Path.GetFileName)
                                      .Where(n => n != "Tip Card Background.png")
                                      .ToArray()!;
            Assert.That(stale, Is.Empty, "old space-named tip art still present: " + string.Join(", ", stale));
        }

        [Test]
        public void RetiredTipTiming_IsInactiveInTexts_NotDeleted()
        {
            // Invariant I6 — the pipeline never deletes a row.
            string[] lines = File.ReadAllLines("Assets/Localization/LocalizationText.csv");
            string? row = lines.FirstOrDefault(l => l.StartsWith("TIP_TIMING,", StringComparison.Ordinal));
            Assert.That(row, Is.Not.Null, "TIP_TIMING must stay in the texts CSV (§2.4)");
            Assert.That(row!.TrimEnd().EndsWith(",false", StringComparison.Ordinal), Is.True,
                "TIP_TIMING must be inactive, not deleted: " + row);
        }

        [Test]
        public void EveryTipKey_HasEnAndJaText()
        {
            var texts = new Dictionary<string, string[]>();
            foreach (string line in File.ReadAllLines("Assets/Localization/LocalizationText.csv").Skip(1))
            {
                if (line.Length == 0) continue;
                string key = line.Split(',')[0];
                if (key.StartsWith("TIP_", StringComparison.Ordinal)) texts[key] = SplitCsv(line);
            }

            var missing = new List<string>();
            foreach (object r in RowList())
            {
                string key = Tips.Key(r);
                if (!texts.TryGetValue(key, out string[] cells)) { missing.Add(key + " (no row)"); continue; }
                if (cells.Length < 3 || cells[1].Trim().Length == 0) missing.Add(key + " (no EN)");
                else if (cells[2].Trim().Length == 0)               missing.Add(key + " (no JA)");
            }
            Assert.That(missing, Is.Empty, "tips with no shippable string: " + string.Join(", ", missing));
        }

        /// <summary>Minimal RFC4180 split — the tip strings carry no embedded commas today but the
        /// rich-text colour tags make that a matter of luck rather than design.</summary>
        static string[] SplitCsv(string line)
        {
            var cells = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool q = false;
            foreach (char c in line)
            {
                if (c == '"') { q = !q; continue; }
                if (c == ',' && !q) { cells.Add(cur.ToString()); cur.Clear(); continue; }
                cur.Append(c);
            }
            cells.Add(cur.ToString());
            return cells.ToArray();
        }

        [Test]
        public void ShellSceneCard_IsWiredToTheCatalogAndAllThirtyFourSprites()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Type cardType = Tips.Find("ProTipCard");
                Component? card = scene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren(cardType, includeInactive: true))
                    .FirstOrDefault();
                Assert.That(card, Is.Not.Null, "ShellScene has no ProTipCard");

                const BindingFlags priv = BindingFlags.Instance | BindingFlags.NonPublic;

                var csv = (TextAsset)cardType.GetField("tipsCsv", priv)!.GetValue(card)!;
                Assert.That(csv, Is.Not.Null, "§3.7 — tipsCsv is unwired");
                Assert.That(AssetDatabase.GetAssetPath(csv), Is.EqualTo(CsvPath));

                var sprites = (Array)cardType.GetField("tipSprites", priv)!.GetValue(card)!;
                Assert.That(sprites.Length, Is.EqualTo(34), "§3.7 — one tipSprites entry per tip");

                Type entry = sprites.GetType().GetElementType()!;
                FieldInfo nameF = entry.GetField("name")!, spriteF = entry.GetField("sprite")!;

                var byName = new Dictionary<string, Sprite>();
                for (int i = 0; i < sprites.Length; i++)
                {
                    object e = sprites.GetValue(i)!;
                    string n = (string)nameF.GetValue(e)!;
                    var s = (Sprite)spriteF.GetValue(e)!;
                    Assert.That(n, Is.Not.Null.And.Not.Empty, "tipSprites[" + i + "] has no name");
                    Assert.That(s, Is.Not.Null, "tipSprites[" + i + "] (" + n + ") has no sprite");
                    Assert.That(byName.ContainsKey(n), Is.False, "duplicate tipSprites name " + n);
                    byName[n] = s;
                }

                var unwired = RowList().Select(Tips.Sprite).Where(n => !byName.ContainsKey(n)).ToArray();
                Assert.That(unwired, Is.Empty, "CSV rows whose sprite is not on the card: " +
                                               string.Join(", ", unwired));

                // Name-keyed, so the file behind each name must be the one the CSV meant.
                foreach (KeyValuePair<string, Sprite> kv in byName)
                    Assert.That(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(kv.Value)),
                        Is.EqualTo(kv.Key), "tipSprites entry '" + kv.Key + "' points at the wrong file");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }
    }
}
