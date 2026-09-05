using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Golfin.Gameplay.UI.Controls;
using NUnit.Framework;
using UnityEngine;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// <c>scheme_confirm_popup</c> § 5 — the confirm pop-up's decision rules, its content table,
    /// and the twelve captured tiles.
    ///
    /// <para>§ 5.1 is tested against <see cref="SchemeConfirmDecision"/> rather than against the
    /// <c>SchemeConfirmModalController</c> MonoBehaviour: the controller is a
    /// <c>ModalController</c> in Assembly-CSharp, an assembly-definition test assembly cannot
    /// reference Assembly-CSharp, and the controller is a thin shell that forwards every one of
    /// these decisions to this class. The end-to-end half — that a real CONFIRM in a real scene
    /// moves the scheme and a real CANCEL does not — is proved by
    /// <c>SchemeConfirmVerify</c> against the live game, not mocked here.</para>
    /// </summary>
    public class SchemeConfirmDecisionTests
    {
        [Test]
        public void TappingTheSchemeAlreadyInUse_OpensNothing()
        {
            var d = new SchemeConfirmDecision();

            Assert.IsFalse(d.Open(ControlScheme.Pendulum, ControlScheme.Pendulum, "settings_popup"),
                "tapping the live scheme must be a no-op — no pop-up, no write");
            Assert.IsFalse(d.Armed);
            Assert.IsFalse(d.Confirm(out _, out _), "nothing can be confirmed when nothing opened");
        }

        [Test]
        public void OpenThenConfirm_CommitsThePendingSchemeAndSourceExactlyOnce()
        {
            var d = new SchemeConfirmDecision();

            Assert.IsTrue(d.Open(ControlScheme.Flick, ControlScheme.FreeSwing, "ingame_popup"));
            Assert.AreEqual(ControlScheme.FreeSwing, d.Pending);
            Assert.AreEqual("ingame_popup", d.Source);

            Assert.IsTrue(d.Confirm(out var scheme, out var source));
            Assert.AreEqual(ControlScheme.FreeSwing, scheme);
            Assert.AreEqual("ingame_popup", source);

            // A double tap landing inside the modal's fade-out must not write a second time.
            Assert.IsFalse(d.Confirm(out _, out _), "CONFIRM must commit exactly once per open");
        }

        [Test]
        public void Cancel_DisarmsSoNothingCanCommitAfterwards()
        {
            var d = new SchemeConfirmDecision();
            d.Open(ControlScheme.Flick, ControlScheme.Needle, "settings_popup");

            d.Cancel();

            Assert.IsFalse(d.Armed);
            Assert.IsFalse(d.Confirm(out _, out _),
                "CANCEL / close / backdrop must make a later CONFIRM a no-op");
        }

        [Test]
        public void Cancel_IsIdempotentAndSafeBeforeAnyOpen()
        {
            var d = new SchemeConfirmDecision();
            Assert.DoesNotThrow(() => { d.Cancel(); d.Cancel(); });
            Assert.IsFalse(d.Armed);
        }

        [Test]
        public void ReOpeningAfterAConfirm_ArmsAgainWithTheNewSelection()
        {
            var d = new SchemeConfirmDecision();
            d.Open(ControlScheme.Flick, ControlScheme.Pendulum, "settings_popup");
            d.Confirm(out _, out _);

            Assert.IsTrue(d.Open(ControlScheme.Pendulum, ControlScheme.Needle, "ingame_popup"));
            Assert.IsTrue(d.Confirm(out var scheme, out var source));
            Assert.AreEqual(ControlScheme.Needle, scheme);
            Assert.AreEqual("ingame_popup", source);
        }
    }

    /// <summary>
    /// § 5.2 — every scheme resolves three tile sprites and twelve localisation keys. This is the
    /// gate that a missing tile or an unpublished key fails at BUILD time rather than in front of
    /// a player, so it reads the CSV and the Resources folder rather than trusting the table.
    /// </summary>
    public class SchemeConfirmContentTests
    {
        const string CsvPath = "Assets/Localization/LocalizationText.csv";

        static Dictionary<string, string[]> _csv;

        /// <summary>Read <c>LocalizationText.csv</c> directly. NOT
        /// <c>LocalizationManager.Get</c>: it is only <c>Initialize()</c>d at boot and returns the
        /// KEY in edit mode, which would make every assertion below vacuously pass.</summary>
        static Dictionary<string, string[]> Csv()
        {
            if (_csv != null) return _csv;

            _csv = new Dictionary<string, string[]>();
            Assert.IsTrue(File.Exists(CsvPath), CsvPath + " is missing");

            foreach (var line in File.ReadAllLines(CsvPath).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var cells = SplitCsv(line);
                if (cells.Count < 3) continue;
                _csv[cells[0]] = new[] { cells[1], cells[2] };
            }
            return _csv;
        }

        static List<string> SplitCsv(string line)
        {
            var cells = new List<string>();
            var cur = new System.Text.StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = !q; }
                else if (c == ',' && !q) { cells.Add(cur.ToString()); cur.Clear(); }
                else cur.Append(c);
            }
            cells.Add(cur.ToString());
            return cells;
        }

        static void AssertKeyShips(string key)
        {
            var csv = Csv();
            Assert.IsTrue(csv.ContainsKey(key), $"'{key}' is not in {CsvPath}");
            Assert.IsNotEmpty(csv[key][0], $"'{key}' has no English value");
            Assert.IsNotEmpty(csv[key][1], $"'{key}' has no Japanese value");
        }

        [Test]
        public void EveryScheme_HasATitleKeyThatShipsInBothLanguages()
        {
            foreach (var s in SchemeConfirmContent.AllSchemes)
                AssertKeyShips(SchemeConfirmContent.For(s).TitleKey);
        }

        [Test]
        public void EveryScheme_HasThreeCaptionKeysAndThreeLineKeysThatShipInBothLanguages()
        {
            foreach (var s in SchemeConfirmContent.AllSchemes)
            {
                var e = SchemeConfirmContent.For(s);
                Assert.AreEqual(3, e.CaptionKeys.Length, s + " caption keys");
                Assert.AreEqual(3, e.LineKeys.Length,    s + " line keys");
                foreach (var k in e.CaptionKeys) AssertKeyShips(k);
                foreach (var k in e.LineKeys)    AssertKeyShips(k);
            }
        }

        [Test]
        public void TheTwoSharedKeys_ShipInBothLanguages()
        {
            AssertKeyShips(SchemeConfirmContent.HowItWorksKey);
            AssertKeyShips(SchemeConfirmContent.FooterKey);
        }

        [Test]
        public void TheKeySetIsExactlyTheTwentySixTheSpecLists()
        {
            var keys = new HashSet<string>
            {
                SchemeConfirmContent.HowItWorksKey, SchemeConfirmContent.FooterKey
            };
            foreach (var s in SchemeConfirmContent.AllSchemes)
            {
                var e = SchemeConfirmContent.For(s);
                foreach (var k in e.CaptionKeys) keys.Add(k);
                foreach (var k in e.LineKeys)    keys.Add(k);
            }
            Assert.AreEqual(26, keys.Count,
                "SPEC § 3.3 lists 26 new keys (2 shared + 4 schemes x 6); the titles reuse the "
                + "existing SETTINGS_CONTROLS_* and the buttons reuse MODAL_CANCEL / MODAL_CONFIRM");
        }

        [Test]
        public void EveryScheme_HasThreeTileSpritesThatResolveFromResources()
        {
            foreach (var s in SchemeConfirmContent.AllSchemes)
            {
                for (int step = 1; step <= 3; step++)
                {
                    var sprite = SchemeConfirmContent.LoadTile(s, step);
                    Assert.IsNotNull(sprite,
                        $"Resources/{SchemeConfirmContent.TilePath(s, step)} does not resolve AS A SPRITE. "
                        + "Either the capture has never run (GOLFIN > Capture > Scheme Confirm Tiles) "
                        + "or the PNG imported as a Texture instead of a Sprite.");
                }
            }
        }

        [Test]
        public void NoContentValueIsAPlayerFacingLiteral()
        {
            // Everything the table hands the pop-up must be a KEY or a Resources path — never a
            // sentence. A key is SCREAMING_SNAKE; a path starts with the tiles folder.
            var keyShape = new Regex("^[A-Z0-9_]+$");
            foreach (var s in SchemeConfirmContent.AllSchemes)
            {
                var e = SchemeConfirmContent.For(s);
                Assert.IsTrue(keyShape.IsMatch(e.TitleKey), e.TitleKey + " is not a localisation key");
                foreach (var k in e.CaptionKeys.Concat(e.LineKeys))
                    Assert.IsTrue(keyShape.IsMatch(k), k + " is not a localisation key");
                foreach (var p in e.TilePaths)
                    Assert.IsTrue(p.StartsWith(SchemeConfirmContent.TileResourceFolder + "/"),
                        p + " is not under " + SchemeConfirmContent.TileResourceFolder);
            }
        }
    }

    /// <summary>
    /// § 5.3 — the capture manifest: twelve crops, each 628x680, each taken from a rect that
    /// touches no HUD chrome. The manifest is the gate on the tiles, not a look at them.
    /// </summary>
    public class SchemeConfirmTileManifestTests
    {
        /// <summary>
        /// Where the manifest may live. A task folder starts under <c>Docs/Specs/Active/</c> and is
        /// moved to <c>Docs/Specs/Completed/</c> at close-out, so pinning either one makes the
        /// close-out commit break this suite — which is exactly what <c>b8ef37ec0</c> did.
        /// Active is probed first so an in-flight re-run of the task wins over the archived copy.
        /// </summary>
        static readonly string[] ManifestCandidates =
        {
            "Docs/Specs/Active/scheme_confirm_popup/tiles_manifest.json",
            "Docs/Specs/Completed/scheme_confirm_popup/tiles_manifest.json",
        };

        static string Manifest()
        {
            string path = ManifestCandidates.FirstOrDefault(File.Exists);
            Assert.IsNotNull(path,
                "tiles_manifest.json is missing from both " + string.Join(" and ", ManifestCandidates)
                + " — run GOLFIN > Capture > Scheme Confirm Tiles");
            return File.ReadAllText(path);
        }

        [Test]
        public void TheManifestListsTwelveTiles()
        {
            var json = Manifest();
            int count = Regex.Matches(json, "\"tile\"\\s*:").Count;
            Assert.AreEqual(12, count, "expected 4 schemes x 3 steps");
        }

        [Test]
        public void EveryTileIs628x680()
        {
            var json = Manifest();
            var widths  = Regex.Matches(json, "\"width\"\\s*:\\s*(\\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToList();
            var heights = Regex.Matches(json, "\"height\"\\s*:\\s*(\\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToList();
            Assert.AreEqual(12, widths.Count);
            CollectionAssert.AreEqual(Enumerable.Repeat(628, 12).ToList(), widths);
            CollectionAssert.AreEqual(Enumerable.Repeat(680, 12).ToList(), heights);
        }

        [Test]
        public void NoCropOverlapsHudChrome()
        {
            var json = Manifest();
            var flags = Regex.Matches(json, "\"no_hud_chrome\"\\s*:\\s*(true|false)")
                             .Select(m => m.Groups[1].Value).ToList();
            Assert.AreEqual(12, flags.Count, "every tile must record the chrome assertion");
            CollectionAssert.DoesNotContain(flags, "false",
                "a tile was cropped over the player card / gear / wind / action buttons");
        }

        [Test]
        public void TheCaptureRunRecordedNoFailures()
        {
            var json = Manifest();
            var m = Regex.Match(json, "\"fails\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            Assert.IsTrue(m.Success, "the manifest must record a fails list");
            Assert.IsEmpty(m.Groups[1].Value.Trim(),
                "the capture run reported failures: " + m.Groups[1].Value);
        }

        [Test]
        public void EveryTilePngExistsAndIsNotAFlatFill()
        {
            foreach (var s in SchemeConfirmContent.AllSchemes)
            for (int step = 1; step <= 3; step++)
            {
                string path = $"Assets/Resources/{SchemeConfirmContent.TilePath(s, step)}.png";
                Assert.IsTrue(File.Exists(path), path + " is missing");
                Assert.Greater(new FileInfo(path).Length, 20_000,
                    path + " is suspiciously small — a flat-colour frame, not a game capture");
            }
        }

        [Test]
        public void NoTwoTilesAreTheSameFrame()
        {
            // Two byte-identical tiles mean a gesture silently did nothing and the second state
            // was never reached — the exact defect the first capture run shipped for Free Swing.
            var seen = new Dictionary<string, string>();
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                foreach (var s in SchemeConfirmContent.AllSchemes)
                for (int step = 1; step <= 3; step++)
                {
                    string path = $"Assets/Resources/{SchemeConfirmContent.TilePath(s, step)}.png";
                    if (!File.Exists(path)) continue;
                    using (var fs = File.OpenRead(path))
                    {
                        string hash = string.Join("", md5.ComputeHash(fs).Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
                        Assert.IsFalse(seen.ContainsKey(hash),
                            $"{path} is byte-identical to {(seen.ContainsKey(hash) ? seen[hash] : "")}");
                        seen[hash] = path;
                    }
                }
            }
        }
    }
}
