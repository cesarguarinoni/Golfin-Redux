using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls;
using Golfin.Gameplay.UI.Controls.Bot;
using Golfin.Gameplay.UI.Controls.Pendulum;
using Golfin.Gameplay.UI.Controls.Needle;
using Golfin.Gameplay.UI.Controls.FreeSwing;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// bot_scheme_parity §6. The load-bearing assertion is §6.1: the Flick executor draws the
    /// same numbers, in the same order, and logs the same line as the <c>VersusBot</c> block it
    /// was cut from — because every 1v1 difficulty number in <c>bot_difficulty.csv</c> was
    /// calibrated against that exact sequence.
    /// </summary>
    public class BotSchemeParityTests
    {
        // ── A seeded uniform sampler standing in for UnityEngine.Random.Range ────
        //
        // Its own generator, not Unity's, so the golden sequence is reproducible on any machine
        // and cannot be perturbed by anything else the test run happens to draw.
        private sealed class SeededRange
        {
            private readonly System.Random _rng;
            public readonly List<float> Draws = new List<float>();
            public SeededRange(int seed) { _rng = new System.Random(seed); }
            public float Range(float min, float max)
            {
                float v = min + (float)_rng.NextDouble() * (max - min);
                Draws.Add(v);
                return v;
            }
        }

        private GameObject _go;
        private ShotController _sc;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("shot");
            _sc = _go.AddComponent<ShotController>();
            _sc.InjectConfig(ControlsConfig.Default);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        }

        // ── §6.1 — the Flick golden regression ───────────────────────────────────

        /// <summary>
        /// THE PRE-CUT ALGORITHM, transcribed from <c>VersusBot.TakeShot</c>'s "2b error
        /// injection" block as it stood before this task (the no-tree-sampler branch: a putt, a
        /// treeless hole, or DebugDisableTreeRecheck). Kept as a literal second implementation on
        /// purpose — a test that called the new code to compute its own expectation would pass
        /// however the new code drifted.
        /// </summary>
        private static string PreCutErrorLine(SeededRange rng, float aimErrorDegMax, float powerErrorMax,
                                              string clubNoiseNote,
                                              out float deltaAimDeg, out float deltaPow)
        {
            deltaAimDeg = 0f;
            deltaPow    = 0f;
            if (aimErrorDegMax > 0f || powerErrorMax > 0f)
            {
                deltaAimDeg = rng.Range(-aimErrorDegMax, aimErrorDegMax);
                deltaPow    = rng.Range(-powerErrorMax, powerErrorMax);
            }
            return $"[VersusBot] 2b error: Δaim={deltaAimDeg:+0.0;-0.0}° " +
                   $"Δpow={deltaPow:+0.000;-0.000} clubNoise={clubNoiseNote} " +
                   $"treeChecked=0 canopyContacts=0";
        }

        [Test]
        public void FlickExecutor_ReproducesThePreCutDrawSequenceAndLogLine_For50Plans()
        {
            // Two generators from the same seed: one feeds the executor, one feeds the reference.
            var live = new SeededRange(1337);
            var gold = new SeededRange(1337);

            var band = new BotExecutionBand(6.0f, 0.12f, 0f);   // the level-1 bracket

            for (int i = 0; i < 50; i++)
            {
                float intentYaw = 0.25f * i;
                float appliedYaw = float.NaN;

                var ctx = new BotExecutionContext
                {
                    Shot       = _sc,
                    Range      = live.Range,
                    ApplySwing = (club, yaw) => { appliedYaw = yaw; return club; }
                };
                var plan = new BotSwingPlan(1, 0.60f, intentYaw, false, 150f, "none");

                string expected = PreCutErrorLine(gold, band.AimErrorDegMax, band.PowerErrorMax,
                                                  "none", out float gAim, out float gPow);
                LogAssert.Expect(LogType.Log, expected);

                // MoveNext once: the sampling, the log and ApplySwing all happen before the first
                // yield, so one step is the whole error model.
                var it = FlickBotExecutor.Instance.Execute(plan, band, ctx);
                it.MoveNext();

                Assert.AreEqual(intentYaw + gAim * Mathf.Deg2Rad, appliedYaw, 1e-5f,
                    $"shot {i}: the camera must be pointed at the POST-error aim");
                Assert.That(gPow, Is.InRange(-band.PowerErrorMax, band.PowerErrorMax));
            }

            CollectionAssert.AreEqual(gold.Draws, live.Draws,
                "the executor must consume the generator in the same order the pre-cut block did");
            Assert.AreEqual(100, live.Draws.Count, "two draws per shot: aim, then power");
        }

        [Test]
        public void FlickExecutor_DrawsThePowerErrorEvenWhenTheBracketsPowerErrorIsZero()
        {
            // Range(0, 0) still advances the generator. A guard that skipped the draw would
            // desynchronise every later shot from the golden file while returning the same 0.
            var rng = new SeededRange(7);
            var ctx = new BotExecutionContext { Shot = _sc, Range = rng.Range };
            var band = new BotExecutionBand(4f, 0f, 0f);

            LogAssert.ignoreFailingMessages = true;
            FlickBotExecutor.Instance.Execute(
                new BotSwingPlan(0, 0.5f, 0f, false, 100f), band, ctx).MoveNext();
            LogAssert.ignoreFailingMessages = false;

            Assert.AreEqual(2, rng.Draws.Count, "aim and power are both drawn");
        }

        [Test]
        public void PerfectBand_InjectsNothingAndLogsNoErrorLine()
        {
            var rng = new SeededRange(11);
            var ctx = new BotExecutionContext { Shot = _sc, Range = rng.Range };

            FlickBotExecutor.Instance.Execute(
                new BotSwingPlan(0, 0.5f, 1.0f, false, 100f),
                BotExecutionBand.Perfect, ctx).MoveNext();

            Assert.AreEqual(0, rng.Draws.Count,
                "a perfect band must not consume the generator — smoke and perf bots have to be deterministic");
        }

        // ── §6.3b / §6.4 — executor resolution ───────────────────────────────────

        [Test]
        public void BotSwing_WithNoHostInTheScene_ResolvesToFlick()
        {
            Assert.AreSame(FlickBotExecutor.Instance, BotSwing.ResolveExecutor());
        }

        [Test]
        public void BotSwing_ForceFlick_ResolvesToFlickEvenWithAGradedSchemeLive()
        {
            var host = MakeHost(ControlScheme.Pendulum, out _);
            try
            {
                Assert.AreEqual(ControlScheme.Pendulum, host.ActiveExecutor.Scheme,
                    "sanity: the host is on Pendulum");
                Assert.AreSame(FlickBotExecutor.Instance,
                               BotSwing.ResolveExecutor(new BotSwingOptions { ForceFlick = true }));
            }
            finally { Cleanup(host); }
        }

        [Test]
        public void ActiveExecutor_FollowsTheLiveScheme()
        {
            foreach (var (scheme, expected) in new[]
            {
                (ControlScheme.Flick,     ControlScheme.Flick),
                (ControlScheme.Pendulum,  ControlScheme.Pendulum),
                (ControlScheme.Needle,    ControlScheme.Needle),
                (ControlScheme.FreeSwing, ControlScheme.FreeSwing),
            })
            {
                var host = MakeHost(scheme, out _);
                try { Assert.AreEqual(expected, host.ActiveExecutor.Scheme, $"scheme {scheme}"); }
                finally { Cleanup(host); }
            }
        }

        [Test]
        public void ActiveExecutor_ForASchemeWithNoDriver_FallsBackToFlick()
        {
            // The host keeps the flick root live under an unimplemented scheme, so the bot must
            // swing the widget the player's own finger is on.
            var host = MakeHost(ControlScheme.Pendulum, out var roots, driversImplemented: false);
            try { Assert.AreEqual(ControlScheme.Flick, host.ActiveExecutor.Scheme); }
            finally { Cleanup(host); }
        }

        // ── §6.2 — the tree sampler's new generic form ───────────────────────────

        [Test]
        public void TreeProbe_GenericOverload_MatchesTheUniformOneForTheSameSeed()
        {
            var a = new SeededRange(99);
            var b = new SeededRange(99);
            const float max = 5f;

            bool okA = Physics.Viewer.BotTreeProbe.TrySampleTreeAwareAimError(
                null, Vector3.zero, 0f, 100f, 20f, max, 5, a.Range, false,
                out float deltaA, out int canopyA);

            bool okB = Physics.Viewer.BotTreeProbe.TrySampleTreeAwareAimError(
                null, Vector3.zero, 0f, 100f, 20f, 5,
                () => b.Range(-max, max), raw => raw, false, true,
                out float deltaB, out int canopyB);

            Assert.IsTrue(okA); Assert.IsTrue(okB);
            Assert.AreEqual(deltaA, deltaB, 1e-6f);
            Assert.AreEqual(canopyA, canopyB);
            CollectionAssert.AreEqual(a.Draws, b.Draws);
        }

        /// <summary>
        /// THE DEFECT THIS TASK'S FIRST ACCEPTANCE RUN FOUND, pinned.
        ///
        /// <para>The scored sampler's "among equally clear lines, take the straightest" preference
        /// is a tertiary nicety under Flick, where the sampled value IS the aim error. Under a
        /// BANDED grader it is a re-roll of the swing: <c>E[min of 5 |N(0,sigma)|]</c> lands inside
        /// the JUST window at every shipped sigma, so the preference does not shrink the miss, it
        /// deletes it. Measured on hole 2: a level-1 bot's mean |Δaim| was 0.10° under Pendulum
        /// and 0.00° under Needle against Flick's 1.72°.</para>
        /// </summary>
        [Test]
        public void TreeProbe_TheStraightestLinePreference_IsFlickOnly()
        {
            // A banded yaw map — the shape every graded scheme has.
            const float Window = 0.30f;
            Func<float, float> banded = raw => Mathf.Abs(raw) <= Window ? 0f : raw * 10f;
            var trees = new NoTreeProvider();

            // 0.90 arrives first; 0.05 is the straightest. Both are trunk- and canopy-clear.
            float[] draws = { 0.90f, 0.25f, 0.05f, 0.60f, 0.20f };

            var qOff = new Queue<float>(draws);
            Assert.IsTrue(Physics.Viewer.BotTreeProbe.TrySampleTreeAwareAimError(
                trees, Vector3.zero, 0f, 100f, 20f, 5, () => qOff.Dequeue(), banded,
                disableCanopyPreference: false, preferStraightestSurvivor: false,
                out float acceptedGraded, out _));
            Assert.AreEqual(0.90f, acceptedGraded, 1e-6f,
                "a graded scheme keeps the FIRST surviving draw — the sampled distribution is the " +
                "difficulty model and the probe must not re-roll it");

            var qOn = new Queue<float>(draws);
            Assert.IsTrue(Physics.Viewer.BotTreeProbe.TrySampleTreeAwareAimError(
                trees, Vector3.zero, 0f, 100f, 20f, 5, () => qOn.Dequeue(), banded,
                disableCanopyPreference: false, preferStraightestSurvivor: true,
                out float acceptedFlick, out _));
            Assert.AreEqual(0.05f, acceptedFlick, 1e-6f,
                "Flick keeps its shipped preference for the straightest surviving line");
        }

        [Test]
        public void TreeProbe_UniformOverload_StillPrefersTheStraightestLine()
        {
            // The Flick wrapper must not have changed: its difficulty was calibrated with the
            // preference live, so the pinned entry point passes preferStraightestSurvivor: true.
            var trees = new NoTreeProvider();
            var draws = new Queue<float>(new[] { 5.0f, 1.0f, 3.0f, 4.0f, 2.0f });

            Assert.IsTrue(Physics.Viewer.BotTreeProbe.TrySampleTreeAwareAimError(
                trees, Vector3.zero, 0f, 100f, 20f, 6f, 5,
                (min, max) => draws.Dequeue(), false, out float accepted, out _));
            Assert.AreEqual(1.0f, accepted, 1e-6f);
        }

        /// <summary>A provider with nothing in it: every line is trunk-clear and canopy-free, so
        /// the scored path runs to completion and the preference is the only thing deciding.</summary>
        private sealed class NoTreeProvider : Golfin.Physics.ITreeObstacleProvider
        {
            public bool TestSegment(Golfin.Physics.Math.fp3 p0, Golfin.Physics.Math.fp3 p1,
                                    out Golfin.Physics.TreeHit hit)
            { hit = default; return false; }
        }

        // ── The sampler and the commit must grade with ONE implementation ────────

        [Test]
        public void PendulumGradeForBot_IsPendulumMathGrade()
        {
            var go = new GameObject("pend", typeof(RectTransform));
            var d  = go.AddComponent<PendulumSchemeDriver>();
            d.ConfigureForTests(go.GetComponent<RectTransform>(), null, null, null, null,
                                ControlsConfig.Default);
            d.Bind(_sc);
            try
            {
                foreach (float m in new[] { -0.9f, -0.3f, 0f, 0.12f, 0.55f, 0.99f })
                {
                    var expected = PendulumMath.Grade(m, _sc.ClubAccuracyNorm01, 0.8f,
                                                      _sc.ConeHalfAngleDeg * Mathf.Deg2Rad,
                                                      ControlsConfig.Default);
                    var actual = d.GradeForBot(m, 0.8f);
                    Assert.AreEqual(expected.ErrorYawRad, actual.ErrorYawRad, 1e-6f, $"m={m}");
                    Assert.AreEqual(expected.Grade, actual.Grade, $"m={m}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void NeedleGradeForBot_IsNeedleMathGrade()
        {
            var go = new GameObject("needle", typeof(RectTransform));
            var d  = go.AddComponent<NeedleSchemeDriver>();
            d.ConfigureForTests(go.GetComponent<RectTransform>(), null, null, null, null, null,
                                ControlsConfig.Default);
            d.Bind(_sc);
            try
            {
                foreach (float n in new[] { -0.95f, -0.2f, 0f, 0.31f, 0.9f })
                {
                    var expected = NeedleMath.Grade(n, _sc.ClubAccuracyNorm01, 0.8f,
                                                    _sc.ConeHalfAngleDeg * Mathf.Deg2Rad,
                                                    ControlsConfig.Default);
                    var actual = d.GradeForBot(n, 0.8f);
                    Assert.AreEqual(expected.ErrorYawRad, actual.ErrorYawRad, 1e-6f, $"n={n}");
                    Assert.AreEqual(expected.Grade, actual.Grade, $"n={n}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void FreeSwingImpactYawForBot_IsFreeSwingMathImpactYaw()
        {
            var go = new GameObject("free", typeof(RectTransform));
            var d  = go.AddComponent<FreeSwingSchemeDriver>();
            d.ConfigureForTests(go.GetComponent<RectTransform>(), null, null, null, null, null, null,
                                ControlsConfig.Default);
            d.Bind(_sc);
            try
            {
                foreach (float px in new[] { -200f, -60f, 0f, 45f, 180f })
                {
                    float expected = FreeSwingMath.ImpactYawRad(px, _sc.ClubAccuracyNorm01, 0.8f,
                                                                _sc.ConeHalfAngleDeg * Mathf.Deg2Rad,
                                                                ControlsConfig.Default);
                    Assert.AreEqual(expected, d.ImpactYawRadForBot(px, 0.8f), 1e-6f, $"px={px}");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ── The per-swing solve: a bot's miss is its LEVEL, not the player's bag ──

        /// <summary>
        /// The follow-up defect, pinned. <c>BotClubSync</c> reads <c>ClubContext.EquippedBag</c> —
        /// the LOCAL PLAYER'S bag — so a 1v1 opponent swings the player's clubs. Graded yaw is
        /// <c>m × halfConeRad</c>, so a single baked sigma made the opponent's difficulty a
        /// function of the player's equipment: a Supreme driver (Acc 120 → 20° cone) made the bot
        /// miss ~2.6× wider than a Common one (Acc 22 → 7.75°) at the same bracket.
        /// </summary>
        [Test]
        public void LiveSigmaSolve_HoldsTheBracketTarget_AcrossTheWholeAccuracyRange()
        {
            var cfg = ControlsConfig.Default;

            foreach (float aimErrorDegMax in new[] { 6.0f, 1.0f })
            {
                float target = aimErrorDegMax * 0.5f;

                foreach (float acc in new[] { 22f / 120f, 60f / 120f, 120f / 120f })
                {
                    float halfCone = Mathf.Lerp(cfg.ConeHalfAngleAtAcc0Deg,
                                                cfg.ConeHalfAngleAtAcc100Deg, acc) * Mathf.Deg2Rad;

                    float sigma = BotSchemeSigmaCalibrator.CalibrateForLiveShot(
                        m => PendulumMath.Grade(m, acc, 0.9f, halfCone, cfg).ErrorYawRad,
                        target, -1f, 1f, out float achieved);

                    Assert.Greater(sigma, 0f, $"acc={acc:F2} @ ±{aimErrorDegMax}°");
                    Assert.AreEqual(target, achieved, target * 0.05f,
                        $"acc={acc:F2} @ ±{aimErrorDegMax}°: the bracket target must hold whatever " +
                        "club the player has equipped — a bot's skill is its level, not your bag");
                }
            }
        }

        [Test]
        public void LiveSigmaSolve_IsDeterministic_SoDifficultyDoesNotShimmerBetweenSwings()
        {
            var cfg = ControlsConfig.Default;
            float halfCone = 7.75f * Mathf.Deg2Rad;
            System.Func<float, float> grader =
                m => PendulumMath.Grade(m, 22f / 120f, 0.9f, halfCone, cfg).ErrorYawRad;

            float a = BotSchemeSigmaCalibrator.CalibrateForLiveShot(grader, 3.0f, -1f, 1f, out _);
            float b = BotSchemeSigmaCalibrator.CalibrateForLiveShot(grader, 3.0f, -1f, 1f, out _);
            Assert.AreEqual(a, b, 0f, "the same shot must always solve to the same sigma");
        }

        [Test]
        public void LiveSigmaSolve_CompensatesTheCone_NarrowerConeNeedsMoreOffset()
        {
            // A narrow cone turns a given mistime into less yaw, so it takes a WIDER timing error
            // to produce the same miss. If this inverted, the solve would be amplifying the very
            // dependency it exists to remove.
            var cfg = ControlsConfig.Default;
            float SigmaFor(float acc)
            {
                float hc = Mathf.Lerp(cfg.ConeHalfAngleAtAcc0Deg,
                                      cfg.ConeHalfAngleAtAcc100Deg, acc) * Mathf.Deg2Rad;
                return BotSchemeSigmaCalibrator.CalibrateForLiveShot(
                    m => PendulumMath.Grade(m, acc, 0.9f, hc, cfg).ErrorYawRad, 3.0f, -1f, 1f, out _);
            }
            Assert.Greater(SigmaFor(22f / 120f), SigmaFor(120f / 120f),
                "a narrower cone must solve to a LARGER timing sigma");
        }

        // ── §6.6 — the calibration guard ─────────────────────────────────────────

        [Test]
        public void CalibratedSigma_ReproducesFlicksExpectedMiss_WithinThreePercent()
        {
            var cfg = ControlsConfig.Default;
            foreach (float aimErrorDegMax in new[] { 6.0f, 4.5f, 3.0f, 2.0f, 1.0f, 0.4f })
            {
                float target = aimErrorDegMax * 0.5f;
                foreach (var scheme in new[] { ControlScheme.Pendulum, ControlScheme.Needle,
                                               ControlScheme.FreeSwing })
                {
                    float sigma = BotSchemeSigmaCalibrator.Calibrate(
                        scheme, target, cfg, 5000, BotSchemeSigmaCalibrator.DefaultSeed,
                        out float achieved);
                    Assert.Greater(sigma, 0f, $"{scheme} @ ±{aimErrorDegMax}°");
                    Assert.AreEqual(target, achieved, target * 0.03f,
                        $"{scheme} @ ±{aimErrorDegMax}°: E|ErrorYaw| must land on Flick's half-width");
                }
            }
        }

        [Test]
        public void CalibratedSigma_IsMonotoneInBracketDifficulty()
        {
            // A harder bracket must not produce a WIDER miss than an easier one — the ladder is
            // the whole point of the table, and an inverted rung would be invisible in play.
            var cfg = ControlsConfig.Default;
            foreach (var scheme in new[] { ControlScheme.Pendulum, ControlScheme.Needle,
                                           ControlScheme.FreeSwing })
            {
                float prev = float.MaxValue;
                foreach (float aim in new[] { 6.0f, 4.5f, 3.0f, 2.0f, 1.0f, 0.4f })
                {
                    float sigma = BotSchemeSigmaCalibrator.Calibrate(
                        scheme, aim * 0.5f, cfg, 5000, BotSchemeSigmaCalibrator.DefaultSeed, out _);
                    Assert.Less(sigma, prev, $"{scheme}: sigma must shrink as the bracket tightens");
                    prev = sigma;
                }
            }
        }

        [Test]
        public void BotDifficultyCsv_CarriesTheThreeCalibratedColumns()
        {
            var csv = Resources.Load<TextAsset>("Data/bot_difficulty");
            Assert.IsNotNull(csv, "Assets/Resources/Data/bot_difficulty.csv must exist");
            Assert.IsTrue(csv.text.Contains("execSigmaPendulum01"), "header names the Pendulum column");
            Assert.IsTrue(csv.text.Contains("execSigmaNeedle01"),   "header names the Needle column");
            Assert.IsTrue(csv.text.Contains("execSigmaFreeSwing01"),"header names the FreeSwing column");

            int rows = 0;
            foreach (var raw in csv.text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("minLevel")) continue;
                var p = line.Split(',');
                Assert.GreaterOrEqual(p.Length, 7, $"row '{line}' must carry all seven columns");
                for (int c = 4; c <= 6; c++)
                {
                    Assert.IsTrue(float.TryParse(p[c].Trim(), System.Globalization.NumberStyles.Float,
                                                 System.Globalization.CultureInfo.InvariantCulture,
                                                 out float sigma), $"row '{line}' column {c}");
                    Assert.Greater(sigma, 0f, $"row '{line}' column {c}: a zero sigma is a bot that never misses");
                }
                rows++;
            }
            Assert.GreaterOrEqual(rows, 6, "every shipped bracket is calibrated");
        }

        // ── Fixture helpers ──────────────────────────────────────────────────────

        private ShotSchemeHost MakeHost(ControlScheme scheme, out GameObject[] roots,
                                        bool driversImplemented = true)
        {
            roots = new GameObject[4];
            for (int i = 0; i < 4; i++)
                roots[i] = new GameObject($"SchemeRoot_{(ControlScheme)i}", typeof(RectTransform));

            if (driversImplemented)
            {
                roots[1].AddComponent<PendulumSchemeDriver>();
                roots[2].AddComponent<NeedleSchemeDriver>();
                roots[3].AddComponent<FreeSwingSchemeDriver>();
            }

            ControlSchemeService.Set(scheme, "test");
            LogAssert.ignoreFailingMessages = true;
            var hostGo = new GameObject("ShotSchemeHost");
            var host   = hostGo.AddComponent<ShotSchemeHost>();
            host.ConfigureForTests(roots, _sc);
            _spawned.Add(hostGo);
            foreach (var r in roots) _spawned.Add(r);
            return host;
        }

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private void Cleanup(ShotSchemeHost host)
        {
            LogAssert.ignoreFailingMessages = false;
            host.ReleaseForTests();
            foreach (var go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
        }
    }
}
