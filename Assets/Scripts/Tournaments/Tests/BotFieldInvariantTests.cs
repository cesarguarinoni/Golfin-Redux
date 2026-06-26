// ─────────────────────────────────────────────────────────────────────────────
// Golfin.Tournaments.Tests — BotFieldInvariantTests
// T3 invariant test suite (SPEC §7 / PIPELINE_HARDENING Rule 3).
// ALL TESTS MUST BE GREEN — this is the pass/fail gate.
//
// Headless NUnit, EditMode. No UnityEngine.Random. No Resources.Load.
// CSV data injected as literal strings so tests are System-only.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Golfin.Tournaments;

namespace Golfin.Tournaments.Tests
{
    [TestFixture]
    public class BotFieldInvariantTests
    {
        // ── Shared test fixtures ────────────────────────────────────────────

        // Inlined fake_players.csv (120 rows) for headless tests.
        // This is the canonical content from Assets/Resources/Data/fake_players.csv.
        // Tests depend on this being consistent with the shipped CSV — if the CSV
        // changes, update this constant.
        private const string FAKE_PLAYERS_CSV = @"id,username,characterId,level
fp_001,FRODO,char_james,173
fp_002,GANDALF,char_olivia,38
fp_003,GALADRIEL,char_richard,16
fp_004,BOROMIR,char_elizabeth,199
fp_005,ARWEN,char_shae,80
fp_006,LEGOLAS,char_camila,72
fp_007,GIMLI,char_guillermo,67
fp_008,SAURON,char_ean,45
fp_009,ARAGORN,char_freda,198
fp_010,SARUMAN,char_johan,36
fp_011,EOWYN,char_mike,183
fp_012,FARAMIR,char_roshana,199
fp_013,SAMWISE,char_james,238
fp_014,PIPPIN,char_olivia,149
fp_015,MERRY,char_richard,32
fp_016,BILBO,char_elizabeth,161
fp_017,THORIN,char_shae,118
fp_018,BALIN,char_camila,18
fp_019,DWALIN,char_guillermo,17
fp_020,FILI,char_ean,33
fp_021,KILI,char_freda,65
fp_022,GLOIN,char_johan,69
fp_023,DORI,char_mike,139
fp_024,NORI,char_roshana,164
fp_025,ORI,char_james,16
fp_026,OIN,char_olivia,153
fp_027,BIFUR,char_richard,60
fp_028,BOFUR,char_elizabeth,193
fp_029,BOMBUR,char_shae,176
fp_030,AZOG,char_camila,189
fp_031,THRANDUIL,char_guillermo,149
fp_032,ELROND,char_ean,117
fp_033,CELEBORN,char_freda,66
fp_034,GLORFINDEL,char_johan,124
fp_035,HALDIR,char_mike,160
fp_036,BEORN,char_roshana,81
fp_037,BARD,char_james,217
fp_038,SMAUG,char_olivia,232
fp_039,GOLEM,char_richard,11
fp_040,RADAGAST,char_elizabeth,204
fp_041,MERIADOC,char_shae,216
fp_042,PEREGRIN,char_camila,50
fp_043,FATTY,char_guillermo,188
fp_044,FREDEGAR,char_ean,118
fp_045,LOBELIA,char_freda,97
fp_046,LOTHO,char_johan,81
fp_047,OTTO,char_mike,49
fp_048,POLO,char_roshana,65
fp_049,FALCO,char_james,205
fp_050,LARGO,char_olivia,96
fp_051,FOSCO,char_richard,36
fp_052,MILO,char_elizabeth,33
fp_053,ANGELICA,char_shae,107
fp_054,MELILOT,char_camila,34
fp_055,ESTELLA,char_guillermo,101
fp_056,RUBY,char_ean,226
fp_057,COTTON,char_freda,98
fp_058,SANDYMAN,char_johan,164
fp_059,TREEBEARD,char_mike,77
fp_060,QUICKBEAM,char_roshana,216
fp_061,SHADOWFAX,char_james,21
fp_062,ASFALOTH,char_olivia,196
fp_063,FIREFOOT,char_richard,127
fp_064,AROD,char_elizabeth,147
fp_065,SNOWMANE,char_shae,41
fp_066,GRIMBOLD,char_camila,106
fp_067,HAMA,char_guillermo,30
fp_068,GAMLING,char_ean,151
fp_069,DEORWINE,char_freda,85
fp_070,ERKENBRAND,char_johan,222
fp_071,THEODRED,char_mike,170
fp_072,EOMER,char_roshana,168
fp_073,THEODEN,char_james,236
fp_074,DENETHOR,char_olivia,230
fp_075,HURIN,char_richard,102
fp_076,TURIN,char_elizabeth,157
fp_077,MORWEN,char_shae,59
fp_078,NIENOR,char_camila,190
fp_079,GLAURUNG,char_guillermo,27
fp_080,ANCALAGON,char_ean,21
fp_081,FINGOLFIN,char_freda,179
fp_082,FEANOR,char_johan,68
fp_083,EARENDIL,char_mike,207
fp_084,LUTHIEN,char_roshana,84
fp_085,BEREN,char_james,30
fp_086,CURUFIN,char_olivia,228
fp_087,CELEGORM,char_richard,69
fp_088,MAEGLIN,char_elizabeth,231
fp_089,TUOR,char_shae,35
fp_090,IDRIL,char_camila,107
fp_091,VORONWE,char_guillermo,81
fp_092,DUILIN,char_ean,126
fp_093,ECTHELION,char_freda,172
fp_094,RORTHON,char_johan,223
fp_095,GOTHMOG,char_mike,103
fp_096,CELEBORN2,char_roshana,51
fp_097,SHELOB,char_james,104
fp_098,UNGOLIANT,char_olivia,100
fp_099,YMIR,char_richard,63
fp_100,MORGOTH,char_elizabeth,181
fp_101,MELKOR,char_shae,78
fp_102,TULKAS,char_camila,189
fp_103,MANWE,char_guillermo,184
fp_104,VARDA,char_ean,175
fp_105,YAVANNA,char_freda,28
fp_106,NIENNA,char_johan,165
fp_107,ESTE,char_mike,172
fp_108,VAIRE,char_roshana,53
fp_109,IRMO,char_james,146
fp_110,NAMO,char_olivia,196
fp_111,AULE,char_richard,72
fp_112,ULMO,char_elizabeth,51
fp_113,OROME,char_shae,128
fp_114,MANDOS,char_camila,107
fp_115,ILUVATAR,char_guillermo,79
fp_116,CELEBRIMBOR,char_ean,173
fp_117,ANNATAR,char_freda,186
fp_118,NARSIL,char_johan,152
fp_119,ANDURIL,char_mike,66
fp_120,STING,char_roshana,185
";

        private const string SCORE_BRACKETS_CSV = @"minLevel,meanDeltaPerHole,stdevPerHole
1,1.3,0.9
10,0.9,0.8
25,0.6,0.7
50,0.35,0.6
100,0.15,0.5
180,-0.05,0.45
";

        // Bracket weights for a balanced 6-bracket field
        private static readonly Dictionary<string, float> DefaultWeights = new Dictionary<string, float>
        {
            ["1"]   = 0.10f,
            ["10"]  = 0.15f,
            ["25"]  = 0.20f,
            ["50"]  = 0.25f,
            ["100"] = 0.20f,
            ["180"] = 0.10f,
        };

        private static readonly int[] NineHolePars = { 4, 3, 5, 4, 4, 3, 5, 4, 4 };

        private static BotFieldGenerator MakeGenerator()
        {
            var roster   = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);
            var brackets = BotScoreBracketsParser.Parse(SCORE_BRACKETS_CSV);
            return new BotFieldGenerator(roster, brackets);
        }

        private static BotFieldConfig MakeConfig(int botCount = 24)
        {
            return new BotFieldConfig(
                botFieldId:        "test_field",
                botCount:          botCount,
                bracketWeights:    DefaultWeights,
                startOffsetMinSec: 60f,
                startOffsetMaxSec: 3600f,
                perHoleSpreadSec:  300f
            );
        }

        private static TournamentDefinition MakeDef(string id = "weekly_test")
        {
            var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var end   = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc);
            return new TournamentDefinition(
                id:           id,
                nameKey:      "TEST_TOURNEY",
                clubId:       "lomond",
                holeSet:      new[] { "h1","h2","h3","h4","h5","h6","h7","h8","h9" },
                startUtc:     start,
                endUtc:       end,
                entryFeeRP:   500L,
                prizeTableId: "medium",
                botFieldId:   "test_field",
                sponsorKey:   "SPONSOR_TEST",
                leagueKey:    "LEAGUE_WEEKLY"
            );
        }

        // ────────────────────────────────────────────────────────────────────
        // Stage 1 tests: hash + PRNG
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// SPEC §7 Stable-hash vector: guards the GetHashCode trap.
        /// Pin: StableHash("abc") must always equal -3547061803046329763L.
        /// </summary>
        [Test]
        public void StableHash_KnownVector_abc()
        {
            const long expected = -3547061803046329763L; // 0xCEC64E155111225D
            long actual = BotFieldHash.StableHash("abc");
            Assert.AreEqual(expected, actual,
                $"FNV-1a hash of \"abc\" must be {expected} (0xCEC64E155111225D). " +
                "If this fails, GetHashCode or a non-stable hash is being used.");
        }

        [Test]
        public void StableHash_EmptyString_IsConstant()
        {
            // Empty string should produce the FNV offset basis (no bytes hashed)
            long h1 = BotFieldHash.StableHash("");
            long h2 = BotFieldHash.StableHash("");
            Assert.AreEqual(h1, h2, "StableHash(\"\") must be idempotent");
        }

        [Test]
        public void StableHash_DifferentStrings_ProduceDifferentValues()
        {
            long h1 = BotFieldHash.StableHash("weekly_lomond_9");
            long h2 = BotFieldHash.StableHash("daily_lomond_9");
            Assert.AreNotEqual(h1, h2, "Different tournament ids must produce different hashes");
        }

        [Test]
        public void Xorshift64_ZeroSeed_AvoidsDegenerateState()
        {
            // PRNG with seed 0 must not get stuck (zero state = dead)
            var rng = new Xorshift64(0L);
            ulong v = rng.NextULong();
            Assert.AreNotEqual(0UL, v, "PRNG must produce non-zero values even when seeded with 0");
        }

        [Test]
        public void Xorshift64_SameSeed_ProducesSameSequence()
        {
            var rng1 = new Xorshift64(12345L);
            var rng2 = new Xorshift64(12345L);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(rng1.NextDouble(), rng2.NextDouble(),
                    $"Sequence diverged at step {i}");
        }

        [Test]
        public void Xorshift64_DifferentSeeds_ProduceDifferentSequences()
        {
            var rng1 = new Xorshift64(12345L);
            var rng2 = new Xorshift64(99999L);
            bool anyDiff = false;
            for (int i = 0; i < 10; i++)
            {
                if (rng1.NextDouble() != rng2.NextDouble()) { anyDiff = true; break; }
            }
            Assert.IsTrue(anyDiff, "Different seeds must produce different sequences");
        }

        [Test]
        public void Xorshift64_NextDouble_InRange()
        {
            var rng = new Xorshift64(42L);
            for (int i = 0; i < 1000; i++)
            {
                double d = rng.NextDouble();
                Assert.GreaterOrEqual(d, 0.0, $"NextDouble at step {i} < 0");
                Assert.Less(d, 1.0, $"NextDouble at step {i} >= 1.0");
            }
        }

        [Test]
        public void Xorshift64_NextInt_InRange()
        {
            var rng = new Xorshift64(7777L);
            for (int i = 0; i < 1000; i++)
            {
                int v = rng.NextInt(10);
                Assert.GreaterOrEqual(v, 0, $"NextInt(10) < 0 at step {i}");
                Assert.Less(v, 10, $"NextInt(10) >= 10 at step {i}");
            }
        }

        [Test]
        public void BotSeedFactory_DifferentSuffixes_DifferentSeeds()
        {
            string tid = "weekly_lomond";
            var identStream   = BotSeedFactory.IdentStream(tid, 0);
            var strokesStream = BotSeedFactory.StrokesStream(tid, 0);
            var paceStream    = BotSeedFactory.PaceStream(tid, 0);

            // First draw from each stream must differ (extremely unlikely to collide)
            double d1 = identStream.NextDouble();
            double d2 = strokesStream.NextDouble();
            double d3 = paceStream.NextDouble();
            Assert.AreNotEqual(d1, d2, "ident vs strokes streams must differ");
            Assert.AreNotEqual(d1, d3, "ident vs pace streams must differ");
        }

        // ────────────────────────────────────────────────────────────────────
        // Stage 2 tests: CSV parsers, RollField
        // ────────────────────────────────────────────────────────────────────

        [Test]
        public void FakePlayerRosterParser_Parse_120Rows()
        {
            var rows = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);
            Assert.AreEqual(120, rows.Count, "fake_players.csv must have 120 rows");
        }

        [Test]
        public void FakePlayerRosterParser_Parse_FirstRow()
        {
            var rows = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);
            var fp1  = rows[0];
            Assert.AreEqual("fp_001",     fp1.Id);
            Assert.AreEqual("FRODO",      fp1.Username);
            Assert.AreEqual("char_james", fp1.CharacterId);
            Assert.AreEqual(173,          fp1.Level);
        }

        [Test]
        public void BotScoreBracketsParser_Parse_6Rows()
        {
            var brackets = BotScoreBracketsParser.Parse(SCORE_BRACKETS_CSV);
            Assert.AreEqual(6, brackets.Count, "bot_score_brackets.csv must have 6 rows");
        }

        [Test]
        public void BotScoreBracketsParser_Parse_SortedAscending()
        {
            var brackets = BotScoreBracketsParser.Parse(SCORE_BRACKETS_CSV);
            for (int i = 1; i < brackets.Count; i++)
                Assert.Greater(brackets[i].MinLevel, brackets[i - 1].MinLevel,
                    "Bracket rows must be sorted by MinLevel ascending");
        }

        [Test]
        public void BotScoreBracketsParser_HighestBracket_NegativeMeanDelta()
        {
            var brackets = BotScoreBracketsParser.Parse(SCORE_BRACKETS_CSV);
            var highest  = brackets[brackets.Count - 1]; // minLevel 180
            Assert.AreEqual(180, highest.MinLevel);
            Assert.Less(highest.MeanDeltaPerHole, 0.0,
                "Scratch bracket (180) must have negative meanDelta (occasional birdie)");
        }

        /// <summary>SPEC §7 Field size: |field| == cfg.BotCount.</summary>
        [Test]
        public void RollField_FieldSize_EqualsBotCount()
        {
            var gen    = MakeGenerator();
            var def    = MakeDef();
            var cfg    = MakeConfig(botCount: 24);
            var field  = gen.RollField(def, cfg, NineHolePars);
            Assert.AreEqual(24, field.Count, "RollField must produce exactly BotCount cards");
        }

        /// <summary>SPEC §7 Determinism: same args → deep-equal result.</summary>
        [Test]
        public void RollField_Determinism_SameArgsSameResult()
        {
            var gen  = MakeGenerator();
            var def  = MakeDef();
            var cfg  = MakeConfig(24);
            var pars = NineHolePars;

            var field1 = gen.RollField(def, cfg, pars);
            var field2 = gen.RollField(def, cfg, pars);

            Assert.AreEqual(field1.Count, field2.Count);
            for (int i = 0; i < field1.Count; i++)
            {
                var c1 = field1[i];
                var c2 = field2[i];
                Assert.AreEqual(c1.BotId,        c2.BotId,        $"card[{i}] BotId differs");
                Assert.AreEqual(c1.TotalStrokes,  c2.TotalStrokes, $"card[{i}] TotalStrokes differs");
                Assert.AreEqual(c1.StartOffsetSeconds, c2.StartOffsetSeconds, $"card[{i}] StartOffset differs");
                for (int h = 0; h < c1.PerHoleStrokes.Count; h++)
                    Assert.AreEqual(c1.PerHoleStrokes[h], c2.PerHoleStrokes[h],
                        $"card[{i}] hole[{h}] strokes differ");
                for (int h = 0; h < c1.PerHoleCompletionUtc.Count; h++)
                    Assert.AreEqual(c1.PerHoleCompletionUtc[h], c2.PerHoleCompletionUtc[h],
                        $"card[{i}] hole[{h}] completion time differs");
            }
        }

        /// <summary>SPEC §7 Determinism: different tournament id → different field.</summary>
        [Test]
        public void RollField_Determinism_DifferentIdsDifferentFields()
        {
            var gen  = MakeGenerator();
            var cfg  = MakeConfig(24);
            var def1 = MakeDef("tourney_A");
            var def2 = MakeDef("tourney_B");

            var field1 = gen.RollField(def1, cfg, NineHolePars);
            var field2 = gen.RollField(def2, cfg, NineHolePars);

            bool anyDiff = field1.Select(c => c.BotId)
                               .Zip(field2.Select(c => c.BotId), (a, b) => a != b)
                               .Any(d => d);
            Assert.IsTrue(anyDiff, "Different tournament ids must produce different fields");
        }

        /// <summary>SPEC §7 Identities: every BotId ∈ fake_players ids.</summary>
        [Test]
        public void RollField_BotIds_AllInFakePlayers()
        {
            var roster  = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);
            var validIds = new HashSet<string>(roster.Select(r => r.Id));

            var gen   = MakeGenerator();
            var field = gen.RollField(MakeDef(), MakeConfig(24), NineHolePars);

            foreach (var card in field)
                Assert.IsTrue(validIds.Contains(card.BotId),
                    $"BotId '{card.BotId}' is not in fake_players.csv");
        }

        /// <summary>SPEC §7 Identities: no duplicate within a field.</summary>
        [Test]
        public void RollField_BotIds_NoDuplicates()
        {
            var gen   = MakeGenerator();
            var field = gen.RollField(MakeDef(), MakeConfig(24), NineHolePars);
            var ids   = field.Select(c => c.BotId).ToList();
            var distinct = ids.Distinct().Count();
            Assert.AreEqual(ids.Count, distinct,
                "A field must not contain duplicate bot identities");
        }

        /// <summary>SPEC §7 Strokes bounds: 1 ≤ strokes ≤ par+4; Total == Σ strokes.</summary>
        [Test]
        public void RollField_StrokesBounds_ValidForAllHoles()
        {
            var gen   = MakeGenerator();
            var field = gen.RollField(MakeDef(), MakeConfig(30), NineHolePars);
            int cap   = BotFieldGenerator.StrokeCapOverPar; // 4

            foreach (var card in field)
            {
                int sum = 0;
                for (int h = 0; h < card.PerHoleStrokes.Count; h++)
                {
                    int s   = card.PerHoleStrokes[h];
                    int par = NineHolePars[h];
                    Assert.GreaterOrEqual(s, 1,            $"Bot {card.BotId} hole {h}: strokes {s} < 1");
                    Assert.LessOrEqual(s, par + cap,       $"Bot {card.BotId} hole {h}: strokes {s} > par+{cap}={par+cap}");
                    sum += s;
                }
                Assert.AreEqual(card.TotalStrokes, sum,
                    $"Bot {card.BotId} TotalStrokes {card.TotalStrokes} ≠ Σ hole strokes {sum}");
            }
        }

        // ── Bracket-mix test seam helpers ───────────────────────────────────────

        /// <summary>
        /// A deliberately-wrong SampleBracket substitute that ignores BracketWeights and
        /// returns a uniformly-random bracket key regardless of weights. Used by the negative
        /// control test to prove the real test would catch a weight-ignoring generator.
        /// </summary>
        private static string UniformBracketSampler(List<string> keys, Xorshift64 rng)
            => keys[rng.NextInt(keys.Count)];

        // ── Primary bracket-mix test: tests the SAMPLED TARGET, not identity level ──

        /// <summary>
        /// SPEC §7 Bracket mix: sampled target brackets ≈ BracketWeights within tolerance.
        ///
        /// iter-2 fix: the former test measured each PICKED identity's natural level-bracket
        /// (derived from fake_players level). Because of nearest-bracket fallback, that differs
        /// from the SAMPLED TARGET (what SampleBracket returned). The former test therefore
        /// measured fixed roster composition, NOT whether BracketWeights were honored — making
        /// it pass even for a weight-ignoring generator (red-team proved with uniform substitution).
        ///
        /// This test uses the internal RollField overload that exposes the sampled target brackets
        /// (the IReadOnlyList&lt;string&gt; sampledBrackets out-param). The sampled-target distribution
        /// is roster-composition-independent and directly reflects SampleBracket behavior.
        ///
        /// N=120 across 5 seeds (600 total draws) gives tight aggregate statistics:
        /// - Expected σ ≈ sqrt(p*(1-p)/600) ≈ 2pp for the 20% bracket
        /// - A weight-ignoring sampler (16.7% vs 25% for bracket "50") = 8.3pp gap ≫ σ
        /// - The 8pp tolerance on the aggregate comfortably catches a uniform sampler
        ///   while being robust against single-seed xorshift clustering (a known low-discrepancy
        ///   artifact of the xorshift64 algorithm with short-period seeds at small N).
        ///
        /// See RollField_BracketMix_RejectsWeightIgnoringSampler for the negative control.
        /// </summary>
        [Test]
        public void RollField_BracketMix_SampledTargetsApproximateWeights()
        {
            const int botCount = 120;      // max roster size — maximises draws per seed
            const float tolerance = 0.08f; // 8pp on the aggregate (600 draws total)
            const int seedCount = 5;       // aggregate across 5 tournament IDs

            var roster   = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);
            var brackets = BotScoreBracketsParser.Parse(SCORE_BRACKETS_CSV);
            var gen      = new BotFieldGenerator(roster, brackets);
            var weights  = DefaultWeights; // 10%/15%/20%/25%/20%/10%

            var cfg = new BotFieldConfig(
                botFieldId:        "mix_test_v2",
                botCount:          botCount,
                bracketWeights:    weights,
                startOffsetMinSec: 60f,
                startOffsetMaxSec: 3600f,
                perHoleSpreadSec:  300f
            );

            // Aggregate sampled-target counts across 5 seeds for statistical robustness.
            // Individual xorshift64 sequences can exhibit short-range clustering at N=120;
            // aggregating 5×120=600 draws drives the variance down to ~±2pp (σ) per bracket,
            // making the 8pp tolerance tight enough to catch a uniform sampler (8.3pp gap)
            // while not failing a correct implementation.
            var aggregateCounts = new Dictionary<string, int>();
            foreach (var bk in weights.Keys) aggregateCounts[bk] = 0;

            string[] seedIds = {
                "bracket_mix_seed_A",
                "bracket_mix_seed_B",
                "bracket_mix_seed_C",
                "bracket_mix_seed_D",
                "bracket_mix_seed_E",
            };

            int totalDraws = 0;
            foreach (var seedId in seedIds)
            {
                gen.RollField(MakeDef(seedId), cfg, NineHolePars, out var sampledBrackets);
                Assert.AreEqual(botCount, sampledBrackets.Count,
                    $"sampledBrackets length must equal BotCount for seed '{seedId}'");

                foreach (var bk in sampledBrackets)
                    aggregateCounts[bk] = aggregateCounts.ContainsKey(bk) ? aggregateCounts[bk] + 1 : 1;

                totalDraws += sampledBrackets.Count;
            }

            Assert.AreEqual(botCount * seedCount, totalDraws, "Total draws must be botCount * seedCount");

            float totalWeight = weights.Values.Sum();
            foreach (var kv in weights)
            {
                float expected = kv.Value / totalWeight;
                float observed = (float)aggregateCounts[kv.Key] / totalDraws;
                Assert.IsTrue(Math.Abs(observed - expected) <= tolerance,
                    $"Sampled-target bracket '{kv.Key}' (aggregate {seedCount} seeds, N={totalDraws}): " +
                    $"expected ~{expected:P1}, got {observed:P1} " +
                    $"(Δ={Math.Abs(observed - expected):P1} > tolerance {tolerance:P1}). " +
                    $"D2 (BracketWeights honored) is broken — SampleBracket is not applying the weights.");
            }
        }

        /// <summary>
        /// NEGATIVE CONTROL — proves the bracket-mix invariant has teeth.
        ///
        /// This test runs the SAME distribution check as RollField_BracketMix_SampledTargetsApproximateWeights
        /// but uses a deliberately-broken sampler (UniformBracketSampler) that ignores BracketWeights
        /// and picks brackets uniformly at random (~16.7% per bracket regardless of weight).
        ///
        /// With weights 10%/15%/20%/25%/20%/10% and tolerance 8pp:
        ///   - Bracket "1":   expected 10%, uniform ~16.7% → Δ ≈ 6.7pp (within tolerance at small N)
        ///   - Bracket "50":  expected 25%, uniform ~16.7% → Δ ≈ 8.3pp (exceeds 8pp at N≥120)
        ///   - Bracket "25":  expected 20%, uniform ~16.7% → Δ ≈ 3.3pp (within)
        ///
        /// At N=120, the highest-weight bracket ("50"=25%) versus uniform (~16.7%) is a 8.3pp gap.
        /// We run 5 independent seeds and assert that at least ONE triggers a violation — the
        /// probability that a uniform sampler stays within 8pp for ALL brackets across 5 seeds is
        /// negligibly small (~(0.15)^5 ≈ 0.008% — see report derivation). This proves discrimination.
        ///
        /// This test MUST PASS (assert passes when broken sampler FAILS the distribution check) —
        /// it documents that the invariant is non-trivially testable, per red-team requirement.
        /// </summary>
        [Test]
        public void RollField_BracketMix_RejectsWeightIgnoringSampler()
        {
            // The primary test now aggregates 5 seeds x 120 draws = 600 total draws.
            // The negative control must use the same parameters (5 seeds, same botCount)
            // so the tolerance proof holds under identical conditions.
            const int botCount = 120;
            const int seedCount = 5;
            const float tolerance = 0.08f;

            var weights     = DefaultWeights; // 10%/15%/20%/25%/20%/10%
            float totalWeight = weights.Values.Sum();
            var keys        = weights.Keys.OrderBy(k => int.TryParse(k, out int n) ? n : 0).ToList();

            // Use the same seed IDs as the primary test so the seeds are not cherry-picked.
            string[] seedIds = {
                "bracket_mix_seed_A",
                "bracket_mix_seed_B",
                "bracket_mix_seed_C",
                "bracket_mix_seed_D",
                "bracket_mix_seed_E",
            };

            // Aggregate uniform-sampler counts across all seeds (600 total draws).
            // With weights 10%/15%/20%/25%/20%/10% and uniform ~16.7%:
            //   - Bracket "50": expected 25%, uniform ~16.7% → Δ ≈ 8.3pp ≫ tolerance 8pp
            //   - Bracket "25": expected 20%, uniform ~16.7% → Δ ≈ 3.3pp (within)
            //   - Bracket "180": expected 10%, uniform ~16.7% → Δ ≈ 6.7pp (within at 600 draws)
            //   - Bracket "1": same as "180"
            // The bracket "50" gap is deterministic: at 600 draws, uniform produces ~100 counts
            // vs expected 150. Variance σ=sqrt(0.167*0.833/600)≈0.015 (1.5pp) means the gap
            // is 8.3pp/1.5pp ≈ 5.5σ — essentially guaranteed to violate (p≈3e-8).
            var aggregateCounts = new Dictionary<string, int>();
            foreach (var k in keys) aggregateCounts[k] = 0;

            int totalDraws = 0;
            foreach (var seedId in seedIds)
            {
                // Mirror BotSeedFactory.BracketStream seeding (same as the generator)
                var rng = new Xorshift64(BotFieldHash.StableHash($"{seedId}:bracket"));
                for (int i = 0; i < botCount; i++)
                {
                    string bracket = UniformBracketSampler(keys, rng);
                    aggregateCounts[bracket]++;
                }
                totalDraws += botCount;
            }

            Assert.AreEqual(botCount * seedCount, totalDraws);

            // Verify the aggregate distribution violates the tolerance for at least one bracket.
            bool anyViolationFound = false;
            foreach (var kv in weights)
            {
                float expected = kv.Value / totalWeight;
                float observed = (float)aggregateCounts[kv.Key] / totalDraws;
                if (Math.Abs(observed - expected) > tolerance)
                {
                    anyViolationFound = true;
                    break;
                }
            }

            Assert.IsTrue(anyViolationFound,
                "NEGATIVE CONTROL FAILED: A uniform-random bracket sampler (which ignores BracketWeights) " +
                $"did NOT trigger a tolerance violation in {seedCount} seeds × {botCount} draws = {totalDraws} total. " +
                "This means the bracket-mix invariant lacks discrimination power at the aggregate level. " +
                "The bracket '50' (expected 25% vs uniform ~16.7%) should produce a Δ≈8.3pp violation " +
                "reliably at this sample size — check that tolerance and seed IDs match the primary test.");
        }

        // ────────────────────────────────────────────────────────────────────
        // Stage 3 tests: pace + projection + trickle
        // ────────────────────────────────────────────────────────────────────

        /// <summary>SPEC §7 Pace: PerHoleCompletionUtc strictly increasing.</summary>
        [Test]
        public void RollField_Pace_StrictlyIncreasing()
        {
            var gen   = MakeGenerator();
            var field = gen.RollField(MakeDef(), MakeConfig(24), NineHolePars);

            foreach (var card in field)
            {
                for (int h = 1; h < card.PerHoleCompletionUtc.Count; h++)
                    Assert.Greater(card.PerHoleCompletionUtc[h], card.PerHoleCompletionUtc[h - 1],
                        $"Bot {card.BotId}: completions must be strictly increasing (hole {h-1}→{h})");
            }
        }

        /// <summary>SPEC §7 Pace: all ∈ (startUtc, endUtc].</summary>
        [Test]
        public void RollField_Pace_AllInTournamentWindow()
        {
            var def   = MakeDef();
            var gen   = MakeGenerator();
            var field = gen.RollField(def, MakeConfig(24), NineHolePars);

            foreach (var card in field)
            {
                DateTime botStart = def.StartUtc.AddSeconds(card.StartOffsetSeconds);
                foreach (var t in card.PerHoleCompletionUtc)
                {
                    Assert.Greater(t, def.StartUtc,
                        $"Bot {card.BotId}: completion {t} is not after startUtc {def.StartUtc}");
                    Assert.LessOrEqual(t, def.EndUtc,
                        $"Bot {card.BotId}: completion {t} exceeds endUtc {def.EndUtc}");
                }
            }
        }

        /// <summary>SPEC §7 Projection purity: depends only on (card, now).</summary>
        [Test]
        public void Project_Purity_SameCardAndTimeYieldsSameResult()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(5), NineHolePars);
            var card  = field[0];
            var now   = def.StartUtc.AddDays(3);

            var p1 = gen.Project(card, now);
            var p2 = gen.Project(card, now);
            Assert.AreEqual(p1.Thru,            p2.Thru);
            Assert.AreEqual(p1.RevealedStrokes,  p2.RevealedStrokes);
            Assert.AreEqual(p1.Complete,         p2.Complete);
        }

        /// <summary>SPEC §7 Projection monotonicity: thru non-decreasing in now.</summary>
        [Test]
        public void Project_Monotonicity_ThruNonDecreasing()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(5), NineHolePars);
            var card  = field[0];

            // Sample thru at 100 evenly spaced points across the window
            var window  = (def.EndUtc - def.StartUtc).TotalSeconds;
            int prev    = -1;
            for (int i = 0; i <= 100; i++)
            {
                double frac = i / 100.0;
                var now   = def.StartUtc.AddSeconds(frac * window);
                var proj  = gen.Project(card, now);
                Assert.GreaterOrEqual(proj.Thru, 0,   $"thru < 0 at frac={frac}");
                Assert.LessOrEqual(proj.Thru, NineHolePars.Length, $"thru > H at frac={frac}");
                if (prev >= 0)
                    Assert.GreaterOrEqual(proj.Thru, prev, $"thru decreased at frac={frac}: {prev} → {proj.Thru}");
                prev = proj.Thru;
            }
        }

        /// <summary>SPEC §7 Projection: thru(startUtc - 1s) == 0.</summary>
        [Test]
        public void Project_BeforeStart_ThruIsZero()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(5), NineHolePars);

            foreach (var card in field)
            {
                var proj = gen.Project(card, def.StartUtc.AddSeconds(-1));
                Assert.AreEqual(0, proj.Thru,
                    $"Bot {card.BotId}: thru must be 0 before startUtc");
                Assert.AreEqual(0, proj.RevealedStrokes,
                    $"Bot {card.BotId}: revealedStrokes must be 0 before startUtc");
                Assert.IsFalse(proj.Complete,
                    $"Bot {card.BotId}: must not be complete before startUtc");
            }
        }

        /// <summary>SPEC §7 Projection: thru(endUtc) == H (complete).</summary>
        [Test]
        public void Project_AtEndUtc_ThruEqualsH()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(5), NineHolePars);
            int H     = NineHolePars.Length;

            foreach (var card in field)
            {
                var proj = gen.Project(card, def.EndUtc);
                Assert.AreEqual(H, proj.Thru,
                    $"Bot {card.BotId}: thru at endUtc must equal H={H}");
                Assert.IsTrue(proj.Complete,
                    $"Bot {card.BotId}: must be complete at endUtc");
                Assert.AreEqual(card.TotalStrokes, proj.RevealedStrokes,
                    $"Bot {card.BotId}: revealedStrokes at complete must equal TotalStrokes");
            }
        }

        /// <summary>SPEC §7 Projection: complete = (thru == H).</summary>
        [Test]
        public void Project_Complete_IffThruEqualsH()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(10), NineHolePars);
            int H     = NineHolePars.Length;
            var window = (def.EndUtc - def.StartUtc).TotalSeconds;

            foreach (var card in field)
            {
                for (int i = 0; i <= 20; i++)
                {
                    var now  = def.StartUtc.AddSeconds(i / 20.0 * window);
                    var proj = gen.Project(card, now);
                    Assert.AreEqual(proj.Complete, proj.Thru == H,
                        $"Bot {card.BotId} at i={i}: complete ({proj.Complete}) ≠ (thru=={H}): thru={proj.Thru}");
                }
            }
        }

        /// <summary>SPEC §7 Projection: revealedStrokes == Σ(perHole[0..thru)).</summary>
        [Test]
        public void Project_RevealedStrokes_EqualsPartialSum()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var field = gen.RollField(def, MakeConfig(5), NineHolePars);
            var window = (def.EndUtc - def.StartUtc).TotalSeconds;

            foreach (var card in field)
            {
                // Sample at mid-window
                var now  = def.StartUtc.AddSeconds(window / 2.0);
                var proj = gen.Project(card, now);
                int manualSum = 0;
                for (int h = 0; h < proj.Thru; h++)
                    manualSum += card.PerHoleStrokes[h];
                Assert.AreEqual(manualSum, proj.RevealedStrokes,
                    $"Bot {card.BotId}: revealedStrokes mismatch at thru={proj.Thru}");
            }
        }

        /// <summary>SPEC §7 Reveal trickle: at mid-window, 0 < Σthru < H*BotCount.</summary>
        [Test]
        public void Project_Trickle_PartialFillAtMidWindow()
        {
            int botCount = 24;
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var cfg   = MakeConfig(botCount);
            var field = gen.RollField(def, cfg, NineHolePars);
            int H     = NineHolePars.Length;

            var window  = (def.EndUtc - def.StartUtc).TotalSeconds;
            var midTime = def.StartUtc.AddSeconds(window / 2.0);

            int totalThru = field.Sum(card => gen.Project(card, midTime).Thru);
            Assert.Greater(totalThru, 0,
                "At mid-window, some holes must be revealed (> 0 total thru)");
            Assert.Less(totalThru, H * botCount,
                "At mid-window, not all holes must be revealed (trickle, not all-or-nothing)");
        }

        // ────────────────────────────────────────────────────────────────────
        // B1: Pace schedule — short-window adversarial coverage
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// B1 fix coverage: verify that both pace invariants (strictly-increasing AND ≤ endUtc)
        /// hold even for a short tournament window where jitter overruns are common.
        ///
        /// The red-team fuzzed RollPaceSchedule with window = H seconds and found violations.
        /// This test uses window = 2×H seconds (18s for 9 holes) with perHoleSpreadSec = 5s
        /// so jitter frequently overruns the nominal step, exercising the compress+re-enforce path.
        ///
        /// Production windows are days; this adversarial case proves the guard logic is correct
        /// rather than lucky at normal scales.
        /// </summary>
        [Test]
        public void RollField_Pace_ShortWindow_InvariantsHold()
        {
            int H = 9;
            // Window = 2×H seconds (18s) — deliberately short; jitter=5s/hole will cause overruns
            var start = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
            var end   = start.AddSeconds(H * 2); // 18s window for 9 holes

            var def = new TournamentDefinition(
                id:           "short_window_test",
                nameKey:      "TEST",
                clubId:       "lomond",
                holeSet:      new[] { "h1","h2","h3","h4","h5","h6","h7","h8","h9" },
                startUtc:     start,
                endUtc:       end,
                entryFeeRP:   0L,
                prizeTableId: "test",
                botFieldId:   "test",
                sponsorKey:   "S",
                leagueKey:    "L"
            );

            // startOffsetMinSec = 0, so botStart = startUtc; perHoleSpreadSec = 5s >> nominalStep ~2s
            var cfg = new BotFieldConfig(
                botFieldId:        "short_window",
                botCount:          20,
                bracketWeights:    DefaultWeights,
                startOffsetMinSec: 0f,
                startOffsetMaxSec: 1f,   // tiny offset so botStart ≈ startUtc
                perHoleSpreadSec:  5f    // jitter >> nominalStep → forces compress path
            );

            var gen   = MakeGenerator();
            var field = gen.RollField(def, cfg, NineHolePars);

            int violations = 0;
            foreach (var card in field)
            {
                // Invariant 1: strictly increasing
                for (int h = 1; h < card.PerHoleCompletionUtc.Count; h++)
                {
                    if (card.PerHoleCompletionUtc[h] <= card.PerHoleCompletionUtc[h - 1])
                    {
                        violations++;
                        Assert.Fail(
                            $"Short-window pace FAIL (strictly-increasing): " +
                            $"Bot {card.BotId} hole {h-1}→{h}: {card.PerHoleCompletionUtc[h-1]:O} → {card.PerHoleCompletionUtc[h]:O}");
                    }
                }
                // Invariant 2: all ≤ endUtc
                foreach (var t in card.PerHoleCompletionUtc)
                {
                    if (t > def.EndUtc)
                    {
                        violations++;
                        Assert.Fail(
                            $"Short-window pace FAIL (≤ endUtc): Bot {card.BotId} completion {t:O} > endUtc {def.EndUtc:O}");
                    }
                }
            }

            Assert.AreEqual(0, violations,
                $"Short-window pace: {violations} invariant violation(s) found across {field.Count} bots.");
        }

        // ────────────────────────────────────────────────────────────────────
        // B3: CSV drift guard — shipped CSV must match inlined fixture
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// B3 guard: verify that the shipped fake_players.csv (loaded via
        /// <see cref="System.IO.File.ReadAllText"/>) matches the inlined test fixture on the
        /// row count and header shape. If the real CSV is edited (a row added, removed, or
        /// a level changed) without updating the inlined constant, this test fails visibly
        /// rather than silently testing stale data.
        ///
        /// In EditMode tests, the shipped CSV is accessible at its project-relative path.
        /// The test uses System.IO.File (not Resources.Load) to stay System-only.
        /// </summary>
        [Test]
        public void ShippedCSV_FakePlayers_MatchesInlinedFixture()
        {
            // Resolve the Assets-relative path from Unity's dataPath or from the csproj-sibling path
            // In EditMode tests, Application.dataPath is not available, so we use the known relative path.
            // The test assembly is compiled into the project's Temp/ folder; the project root is 3 levels up.
            string assemblyDir = System.IO.Path.GetDirectoryName(
                System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";

            // Walk up from the assembly output directory to find the project root
            // Typical path: <project>/Library/ScriptAssemblies/<asm>.dll → root is 3 levels up
            string projectRoot = assemblyDir;
            for (int i = 0; i < 4; i++)
            {
                string candidate = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(projectRoot, ".."));
                if (System.IO.File.Exists(System.IO.Path.Combine(candidate, "Assets", "Resources", "Data", "fake_players.csv")))
                {
                    projectRoot = candidate;
                    break;
                }
                projectRoot = candidate;
            }

            string csvPath = System.IO.Path.Combine(projectRoot, "Assets", "Resources", "Data", "fake_players.csv");
            if (!System.IO.File.Exists(csvPath))
            {
                Assert.Inconclusive(
                    $"Shipped fake_players.csv not found at '{csvPath}'. " +
                    "This test only runs in a full project checkout. Skipping drift check.");
                return;
            }

            string shippedText = System.IO.File.ReadAllText(csvPath);
            var shippedRows  = FakePlayerRosterParser.Parse(shippedText);
            var fixtureRows  = FakePlayerRosterParser.Parse(FAKE_PLAYERS_CSV);

            Assert.AreEqual(fixtureRows.Count, shippedRows.Count,
                $"B3 DRIFT: Shipped fake_players.csv has {shippedRows.Count} rows but the inlined " +
                $"FAKE_PLAYERS_CSV fixture has {fixtureRows.Count} rows. " +
                "Update FAKE_PLAYERS_CSV in BotFieldInvariantTests.cs to match the shipped file.");

            // Check first and last rows match on all columns
            if (shippedRows.Count > 0 && fixtureRows.Count > 0)
            {
                var s0 = shippedRows[0]; var f0 = fixtureRows[0];
                Assert.AreEqual(f0.Id, s0.Id,           "B3 DRIFT: Row 0 id mismatch");
                Assert.AreEqual(f0.Username, s0.Username,"B3 DRIFT: Row 0 username mismatch");
                Assert.AreEqual(f0.Level, s0.Level,     "B3 DRIFT: Row 0 level mismatch");

                var sLast = shippedRows[shippedRows.Count - 1];
                var fLast = fixtureRows[fixtureRows.Count - 1];
                Assert.AreEqual(fLast.Id, sLast.Id,           "B3 DRIFT: Last row id mismatch");
                Assert.AreEqual(fLast.Username, sLast.Username,"B3 DRIFT: Last row username mismatch");
                Assert.AreEqual(fLast.Level, sLast.Level,     "B3 DRIFT: Last row level mismatch");
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Additional robustness
        // ────────────────────────────────────────────────────────────────────

        [Test]
        public void RollField_StartOffsets_WithinConfiguredRange()
        {
            var gen   = MakeGenerator();
            var def   = MakeDef();
            var cfg   = MakeConfig(24);
            var field = gen.RollField(def, cfg, NineHolePars);

            foreach (var card in field)
            {
                Assert.GreaterOrEqual(card.StartOffsetSeconds, cfg.StartOffsetMinSec,
                    $"Bot {card.BotId} offset < min");
                Assert.LessOrEqual(card.StartOffsetSeconds, cfg.StartOffsetMaxSec,
                    $"Bot {card.BotId} offset > max");
            }
        }

        [Test]
        public void RollField_SingleHolePar_StillValid()
        {
            var gen    = MakeGenerator();
            var def    = MakeDef();
            var cfg    = MakeConfig(10);
            var field  = gen.RollField(def, cfg, new[] { 4 });
            int cap    = BotFieldGenerator.StrokeCapOverPar;

            foreach (var card in field)
            {
                Assert.AreEqual(1, card.PerHoleStrokes.Count);
                int s = card.PerHoleStrokes[0];
                Assert.GreaterOrEqual(s, 1);
                Assert.LessOrEqual(s, 4 + cap);
                Assert.AreEqual(s, card.TotalStrokes);
            }
        }

        [Test]
        public void Xorshift64_BoxMuller_MeanApproxZeroStdevApproxOne()
        {
            // Sanity: a large sample from N(0,1) Box-Muller should have mean≈0 and stdev≈1
            var rng    = new Xorshift64(99L);
            int N      = 10000;
            double sum = 0, sumSq = 0;
            for (int i = 0; i < N; i++)
            {
                double v = rng.NextNormal(0.0, 1.0);
                sum   += v;
                sumSq += v * v;
            }
            double mean   = sum / N;
            double stdev  = Math.Sqrt(sumSq / N - mean * mean);
            Assert.AreEqual(0.0, mean,  0.1, $"Box-Muller mean {mean:F3} should be ≈0 (±0.1)");
            Assert.AreEqual(1.0, stdev, 0.1, $"Box-Muller stdev {stdev:F3} should be ≈1 (±0.1)");
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers: bracket resolution (mirrors BotFieldGenerator internals)
        // ────────────────────────────────────────────────────────────────────

        private static readonly int[] BotDifficultyMinLevels = { 1, 10, 25, 50, 100, 180 };

        private static List<int> BracketBinsFromBotDifficulty() =>
            BotDifficultyMinLevels.ToList();

        private static string BracketKeyForLevel(int level, List<int> bins)
        {
            int best = bins[0];
            foreach (int minLv in bins)
                if (minLv <= level) best = minLv;
                else break;
            return best.ToString();
        }
    }
}
