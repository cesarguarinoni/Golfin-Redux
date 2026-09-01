// Order: gps_trust_core §Tests — GpsTrustSignals copies both seams and never throws.
using NUnit.Framework;

namespace Golfin.Gps.Tests
{
    public class GpsTrustSignalsTests
    {
        [Test]
        public void Capture_CopiesBothSeams()
        {
            GpsTrustSignals s = GpsTrustSignals.Capture(new FakeMockDetector(true), new FakePlatformProbe("android"));

            Assert.IsTrue(s.IsMock);
            Assert.AreEqual("android", s.ClientPlatform);
        }

        [Test]
        public void Capture_SimulatorLabelWithTheShippingNeverMockDetector()
        {
            GpsTrustSignals s = GpsTrustSignals.Capture(new NeverMockDetector(), new FakePlatformProbe("ios-simulator"));

            Assert.IsFalse(s.IsMock, "no native mock detector ships yet");
            Assert.AreEqual("ios-simulator", s.ClientPlatform,
                "the server treats this label as mock itself (score.py:183), which is what covers the simulator");
        }

        [Test]
        public void Capture_DegradesToUnknownWhenAProbeThrows()
        {
            GpsTrustSignals s = GpsTrustSignals.Capture(
                new FakeMockDetector { Throws = true },
                new FakePlatformProbe { Throws = true });

            Assert.IsFalse(s.IsMock);
            Assert.AreEqual("unknown", s.ClientPlatform);
        }

        [Test]
        public void Capture_WithNoSeamsIsStillUsable()
        {
            GpsTrustSignals s = GpsTrustSignals.Capture(null, null);

            Assert.IsFalse(s.IsMock);
            Assert.AreEqual("unknown", s.ClientPlatform);
        }

        [Test]
        public void IosHardwareHeuristic_SeparatesDeviceModelsFromSimulatorHostCpus()
        {
            Assert.IsTrue(UnityClientPlatformProbe.LooksLikeIosHardware("iPhone16,2"));
            Assert.IsTrue(UnityClientPlatformProbe.LooksLikeIosHardware("iPad13,1"));
            Assert.IsTrue(UnityClientPlatformProbe.LooksLikeIosHardware("iPod9,1"));
            Assert.IsFalse(UnityClientPlatformProbe.LooksLikeIosHardware("x86_64"));
            Assert.IsFalse(UnityClientPlatformProbe.LooksLikeIosHardware("arm64"));
            Assert.IsFalse(UnityClientPlatformProbe.LooksLikeIosHardware(null));
        }

        [Test]
        public void PlatformProbe_ReportsEditorInTheEditor()
        {
            Assert.AreEqual("editor", new UnityClientPlatformProbe().Label());
        }
    }
}
