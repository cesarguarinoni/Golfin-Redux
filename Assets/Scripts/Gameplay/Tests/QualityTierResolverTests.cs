using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Golfin.Gameplay.UI.Quality;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// quality_tiers §2 — the device table. These are the ONLY gate on the resolver: it runs once
    /// per launch, before anything is on screen, on hardware we mostly do not own, and a wrong row
    /// is invisible (a High phone quietly running Low looks fine, it is just worse than it should
    /// be). So every rule in the spec table gets a row here, including the fallbacks.
    /// </summary>
    public class QualityTierResolverTests
    {
        static QualityTier Ios(string model)
        {
            return QualityTierResolver.Resolve(
                RuntimePlatform.IPhonePlayer, model, "Apple GPU",
                GraphicsDeviceType.Metal, 6144, out _);
        }

        static QualityTier Android(string gpu, GraphicsDeviceType api = GraphicsDeviceType.Vulkan, int memMb = 8192)
        {
            return QualityTierResolver.Resolve(
                RuntimePlatform.Android, "Some Phone", gpu, api, memMb, out _);
        }

        // ── iOS: the identifier major number is one generation AHEAD of the marketing name ──

        [Test] public void IPhone10_3_IsLow()  => Assert.AreEqual(QualityTier.Low,  Ios("iPhone10,3"));  // iPhone X, A11
        [Test] public void IPhone11_8_IsLow()  => Assert.AreEqual(QualityTier.Low,  Ios("iPhone11,8"));  // iPhone XR, A12
        [Test] public void IPhone12_8_IsMid()  => Assert.AreEqual(QualityTier.Mid,  Ios("iPhone12,8"));  // SE2, A13
        [Test] public void IPhone13_2_IsMid()  => Assert.AreEqual(QualityTier.Mid,  Ios("iPhone13,2"));  // iPhone 12, A14
        [Test] public void IPhone14_6_IsHigh() => Assert.AreEqual(QualityTier.High, Ios("iPhone14,6"));  // SE3, A15
        [Test] public void IPhone16_2_IsHigh() => Assert.AreEqual(QualityTier.High, Ios("iPhone16,2"));  // iPhone 15 Pro Max, A17 Pro

        [Test] public void IPad13_1_IsHigh() => Assert.AreEqual(QualityTier.High, Ios("iPad13,1"));
        [Test] public void IPad8_1_IsMid()   => Assert.AreEqual(QualityTier.Mid,  Ios("iPad8,1"));
        [Test] public void IPad7_1_IsLow()   => Assert.AreEqual(QualityTier.Low,  Ios("iPad7,1"));
        [Test] public void IPod9_1_IsLow()   => Assert.AreEqual(QualityTier.Low,  Ios("iPod9,1"));

        [Test] public void GarbageModel_FallsBackToMid()
        {
            Assert.AreEqual(QualityTier.Mid, Ios("not-a-model"));
            Assert.AreEqual(QualityTier.Mid, Ios(""));
            Assert.AreEqual(QualityTier.Mid, Ios(null));
            Assert.AreEqual(QualityTier.Mid, Ios("iPhone16"));   // no comma: not an identifier
        }

        // ── Android: GPU sets the tier, caps only ever move it DOWN ─────────────────────

        [Test] public void Adreno740_IsHigh()      => Assert.AreEqual(QualityTier.High, Android("Adreno (TM) 740"));
        [Test] public void Adreno830_IsHigh()      => Assert.AreEqual(QualityTier.High, Android("Adreno (TM) 830"));
        [Test] public void MaliG710_IsHigh()       => Assert.AreEqual(QualityTier.High, Android("Mali-G710"));
        [Test] public void Immortalis_IsHigh()     => Assert.AreEqual(QualityTier.High, Android("Mali-G715-Immortalis MC11"));
        [Test] public void Xclipse920_IsHigh()     => Assert.AreEqual(QualityTier.High, Android("Samsung Xclipse 920"));

        [Test] public void Adreno650_IsMid()       => Assert.AreEqual(QualityTier.Mid,  Android("Adreno (TM) 650"));
        [Test] public void MaliG78_IsMid()         => Assert.AreEqual(QualityTier.Mid,  Android("Mali-G78 MP20"));
        [Test] public void MaliG68_IsMid()         => Assert.AreEqual(QualityTier.Mid,  Android("Mali-G68 MC4"));

        [Test] public void Adreno630_IsLow()       => Assert.AreEqual(QualityTier.Low,  Android("Adreno (TM) 630"));
        [Test] public void Adreno530_IsLow()       => Assert.AreEqual(QualityTier.Low,  Android("Adreno (TM) 530"));
        [Test] public void MaliG52_IsLow()         => Assert.AreEqual(QualityTier.Low,  Android("Mali-G52 MC2"));
        [Test] public void MaliT880_IsLow()        => Assert.AreEqual(QualityTier.Low,  Android("Mali-T880"));
        [Test] public void PowerVR_IsLow()         => Assert.AreEqual(QualityTier.Low,  Android("PowerVR Rogue GE8320"));

        [Test] public void UnknownGpu_DefaultsToMid() => Assert.AreEqual(QualityTier.Mid, Android("Totally New GPU 9000"));

        /// The spec's named case: an Adreno 650 on a GLES3-only driver. Already Mid by GPU, and the
        /// cap must not push it anywhere else.
        [Test]
        public void Adreno650_OnGles3_IsMid() =>
            Assert.AreEqual(QualityTier.Mid, Android("Adreno (TM) 650", GraphicsDeviceType.OpenGLES3));

        [Test]
        public void HighGpu_OnGles3_CapsToMid() =>
            Assert.AreEqual(QualityTier.Mid, Android("Adreno (TM) 740", GraphicsDeviceType.OpenGLES3));

        [Test]
        public void ThreeGigabytes_ForcesLow_EvenOnAHighGpu() =>
            Assert.AreEqual(QualityTier.Low, Android("Adreno (TM) 740", GraphicsDeviceType.Vulkan, 3072));

        [Test]
        public void FourGigabytes_CapsHighToMid() =>
            Assert.AreEqual(QualityTier.Mid, Android("Adreno (TM) 740", GraphicsDeviceType.Vulkan, 4096));

        /// Caps only demote. A 4 GB phone on a Low GPU stays Low, it does not get promoted to Mid.
        [Test]
        public void MemoryCap_NeverPromotes() =>
            Assert.AreEqual(QualityTier.Low, Android("Mali-G52", GraphicsDeviceType.Vulkan, 4096));

        // ── Editor / desktop ───────────────────────────────────────────────────────────

        [Test]
        public void EditorIsHigh()
        {
            Assert.AreEqual(QualityTier.High, QualityTierResolver.Resolve(
                RuntimePlatform.OSXEditor, "Mac", "Apple M1", GraphicsDeviceType.Metal, 16384, out _));
            Assert.AreEqual(QualityTier.High, QualityTierResolver.Resolve(
                RuntimePlatform.WindowsPlayer, "PC", "NVIDIA", GraphicsDeviceType.Direct3D11, 16384, out _));
        }

        // ── The enum values ARE the quality level indices (see QualityTier docs) ───────

        [Test]
        public void TierValuesMatchQualityLevelIndices()
        {
            Assert.AreEqual(0, (int)QualityTier.Low);
            Assert.AreEqual(1, (int)QualityTier.Mid);
            Assert.AreEqual(2, (int)QualityTier.High);
        }

        [Test]
        public void ReasonIsAlwaysPopulated()
        {
            QualityTierResolver.Resolve(RuntimePlatform.IPhonePlayer, "iPhone16,2", "Apple GPU",
                                        GraphicsDeviceType.Metal, 8192, out string iosReason);
            QualityTierResolver.Resolve(RuntimePlatform.Android, "P", "Adreno (TM) 740",
                                        GraphicsDeviceType.Vulkan, 8192, out string androidReason);

            Assert.IsNotEmpty(iosReason);
            Assert.IsNotEmpty(androidReason);
        }
    }
}
