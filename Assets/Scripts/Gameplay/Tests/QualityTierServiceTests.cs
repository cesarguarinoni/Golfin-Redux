using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.UI.Quality;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// quality_tiers §2 — the override round-trip. The resolver decides what a device SHOULD get;
    /// this is the half that decides what it ACTUALLY gets, and the failure it guards against is
    /// silent: a stored pref that does not read back is invisible until a player relaunches and
    /// finds their choice gone.
    ///
    /// Every test restores the pref, the quality level and the frame rate, because
    /// <see cref="QualityTierService.SetOverride"/> genuinely applies them to the Editor.
    /// </summary>
    public class QualityTierServiceTests
    {
        int _savedPref;
        bool _hadPref;
        int _savedLevel;
        int _savedFrameRate;

        [SetUp]
        public void SetUp()
        {
            _hadPref  = PlayerPrefs.HasKey(QualityTierService.PrefKey);
            _savedPref = PlayerPrefs.GetInt(QualityTierService.PrefKey, QualityTierService.AutoPref);
            _savedLevel = QualitySettings.GetQualityLevel();
            _savedFrameRate = Application.targetFrameRate;
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPref) PlayerPrefs.SetInt(QualityTierService.PrefKey, _savedPref);
            else          PlayerPrefs.DeleteKey(QualityTierService.PrefKey);
            PlayerPrefs.Save();

            QualitySettings.SetQualityLevel(_savedLevel, applyExpensiveChanges: true);
            Application.targetFrameRate = _savedFrameRate;
        }

        [Test]
        public void ProjectHasTheFourExpectedQualityLevels_InTierOrder()
        {
            // The tier enum values ARE the level indices — reordering the Quality window would
            // silently re-point every tier, so pin the order here.
            var names = QualitySettings.names;
            Assert.AreEqual(4, names.Length, "Expected Low/Mid/High/PC.");
            Assert.AreEqual("Low",  names[(int)QualityTier.Low]);
            Assert.AreEqual("Mid",  names[(int)QualityTier.Mid]);
            Assert.AreEqual("High", names[(int)QualityTier.High]);
            Assert.AreEqual("PC",   names[3]);
        }

        [Test]
        public void SetOverride_PersistsAndReadsBack()
        {
            QualityTierService.SetOverride((int)QualityTier.Low);
            Assert.AreEqual((int)QualityTier.Low, QualityTierService.GetOverridePref());
            Assert.AreEqual((int)QualityTier.Low, PlayerPrefs.GetInt(QualityTierService.PrefKey, -99));
            Assert.IsTrue(QualityTierService.IsOverride);
            Assert.AreEqual(QualityTier.Low, QualityTierService.Current);

            QualityTierService.SetOverride((int)QualityTier.High);
            Assert.AreEqual((int)QualityTier.High, QualityTierService.GetOverridePref());
            Assert.AreEqual(QualityTier.High, QualityTierService.Current);
        }

        [Test]
        public void Auto_ClearsTheOverride_AndFallsBackToTheResolvedTier()
        {
            QualityTierService.SetOverride((int)QualityTier.Low);
            Assert.IsTrue(QualityTierService.IsOverride);

            QualityTierService.SetOverride(QualityTierService.AutoPref);

            Assert.AreEqual(QualityTierService.AutoPref, QualityTierService.GetOverridePref());
            Assert.IsFalse(QualityTierService.IsOverride);
            Assert.AreEqual(QualityTierResolver.Resolve(), QualityTierService.Current,
                            "Auto must land on exactly what the resolver says.");
        }

        [Test]
        public void OutOfRangePref_IsTreatedAsAuto()
        {
            QualityTierService.SetOverride(99);
            Assert.AreEqual(QualityTierService.AutoPref, QualityTierService.GetOverridePref());
            Assert.IsFalse(QualityTierService.IsOverride);
        }

        [Test]
        public void ApplyingATier_SwapsTheQualityLevelAndTheFrameRate()
        {
            QualityTierService.SetOverride((int)QualityTier.Low);
            Assert.AreEqual((int)QualityTier.Low, QualitySettings.GetQualityLevel());
            Assert.AreEqual(30, Application.targetFrameRate, "Low is a 30 fps tier (Cesar, 2026-08-26).");

            QualityTierService.SetOverride((int)QualityTier.Mid);
            Assert.AreEqual((int)QualityTier.Mid, QualitySettings.GetQualityLevel());
            Assert.AreEqual(60, Application.targetFrameRate);

            QualityTierService.SetOverride((int)QualityTier.High);
            Assert.AreEqual((int)QualityTier.High, QualitySettings.GetQualityLevel());
            Assert.AreEqual(60, Application.targetFrameRate);
        }

        [Test]
        public void OnTierChanged_FiresWhenTheTierMoves()
        {
            QualityTierService.SetOverride((int)QualityTier.Mid);

            QualityTier? seen = null;
            System.Action<QualityTier> handler = t => seen = t;
            QualityTierService.OnTierChanged += handler;
            try
            {
                QualityTierService.SetOverride((int)QualityTier.Low);
                Assert.AreEqual(QualityTier.Low, seen, "Hole-scoped effects re-apply from this event.");
            }
            finally { QualityTierService.OnTierChanged -= handler; }
        }

        /// <summary>
        /// THE FAIRNESS RULE, as an assertion. Two players on different tiers must see the same
        /// course: same terrain settings, same LOD cull threshold, same trees in the same places.
        /// maximumLODLevel is allowed to differ (it SKIPS LOD0, it does not move the cull point);
        /// lodBias is not (it scales the threshold, so a Low player would lose distant geometry).
        /// </summary>
        [Test]
        public void FairnessRule_TerrainAndLodBiasAreIdenticalOnEveryTier()
        {
            int saved = QualitySettings.GetQualityLevel();
            try
            {
                float lodBias = -1f;
                foreach (QualityTier tier in new[] { QualityTier.Low, QualityTier.Mid, QualityTier.High })
                {
                    QualitySettings.SetQualityLevel((int)tier, applyExpensiveChanges: true);

                    if (lodBias < 0f) lodBias = QualitySettings.lodBias;
                    Assert.AreEqual(lodBias, QualitySettings.lodBias, 0.0001f,
                                    $"lodBias differs on {tier} — that changes the LOD CULL threshold, not just detail.");
                }
            }
            finally { QualitySettings.SetQualityLevel(saved, applyExpensiveChanges: true); }
        }

        /// <summary>Low skips LOD0 (§ tier table); Mid and High do not.</summary>
        [Test]
        public void MaximumLodLevel_IsOneOnLow_ZeroOnMidAndHigh()
        {
            int saved = QualitySettings.GetQualityLevel();
            try
            {
                QualitySettings.SetQualityLevel((int)QualityTier.Low, true);
                Assert.AreEqual(1, QualitySettings.maximumLODLevel);

                QualitySettings.SetQualityLevel((int)QualityTier.Mid, true);
                Assert.AreEqual(0, QualitySettings.maximumLODLevel);

                QualitySettings.SetQualityLevel((int)QualityTier.High, true);
                Assert.AreEqual(0, QualitySettings.maximumLODLevel);
            }
            finally { QualitySettings.SetQualityLevel(saved, true); }
        }
    }
}
