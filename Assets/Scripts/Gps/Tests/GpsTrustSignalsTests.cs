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
        public void IsSimulator_TheAppleSiliconSimulatorLooksLikeHardwareAndIsCaughtByTheEnvironment()
        {
            // Measured on the iPhone 14 simulator, Unity 6000.3.9f1, iOS 18.6
            // (score_upload_flow, Docs/Diagnostics/_capture/score_upload/sim_probe.log):
            //   deviceModel='iPhone14,7'  gpu='Apple iOS simulator GPU'
            //   SIMULATOR_UDID='CB1B2849-…'  SIMULATOR_MODEL_IDENTIFIER='iPhone14,7'
            // The model alone says "hardware" — which is exactly why the model alone is not the rule.
            Assert.IsTrue(UnityClientPlatformProbe.IsSimulator(
                "CB1B2849-80AC-4E35-87DB-7810B690442C", "iPhone14,7",
                "Apple iOS simulator GPU", "iPhone14,7"));

            // Real hardware: no SIMULATOR_* in the environment, a real GPU name, a real model.
            Assert.IsFalse(UnityClientPlatformProbe.IsSimulator(
                null, null, "Apple A16 GPU", "iPhone15,2"));

            // Each signal on its own is enough.
            Assert.IsTrue(UnityClientPlatformProbe.IsSimulator(null, null, "Apple iOS simulator GPU", "iPhone14,7"));
            Assert.IsTrue(UnityClientPlatformProbe.IsSimulator(null, "iPhone14,7", "Apple A16 GPU", "iPhone14,7"));

            // Last resort, with the environment stripped and no GPU hint: an unrecognised model
            // resolves to "simulator", which costs Trust rather than granting it.
            Assert.IsTrue(UnityClientPlatformProbe.IsSimulator(null, null, "", "x86_64"));
            Assert.IsFalse(UnityClientPlatformProbe.IsSimulator(null, null, "", "iPad13,1"));
        }

        [Test]
        public void PlatformProbe_ReportsEditorInTheEditor()
        {
            Assert.AreEqual("editor", new UnityClientPlatformProbe().Label());
        }
    }
}
