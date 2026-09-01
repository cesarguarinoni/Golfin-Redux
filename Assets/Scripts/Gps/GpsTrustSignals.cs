// Order: gps_trust_core §3 — port of gps_trust_signals.dart (M1 mock detection, M2 simulator label).
using UnityEngine;

namespace Golfin.Gps
{
    /// <summary>
    /// M1. Whether the OS reports the location as mocked.
    ///
    /// This is a SEAM, not an implementation: Unity's <c>LocationService</c> exposes no
    /// <c>isMocked</c> equivalent to Geolocator's, so a real answer on Android needs a native
    /// plugin — a later task. <see cref="NeverMockDetector"/> ships everywhere until it lands.
    /// The iOS simulator is still caught, via <see cref="IClientPlatformProbe"/> below, because the
    /// server treats <c>client_platform == "ios-simulator"</c> as mock itself (score.py:183).
    /// </summary>
    public interface IMockLocationDetector
    {
        bool IsMock();
    }

    /// <summary>Shipping default on every platform. Replaced on Android by the mock-location plugin task.</summary>
    public sealed class NeverMockDetector : IMockLocationDetector
    {
        public bool IsMock() => false;
    }

    /// <summary>M2. The <c>client_platform</c> label: <c>ios</c> | <c>ios-simulator</c> |
    /// <c>android</c> | <c>editor</c> | <c>unknown</c>.</summary>
    public interface IClientPlatformProbe
    {
        string Label();
    }

    /// <summary>
    /// The real probe.
    ///
    /// Unity has no <c>iosInfo.isPhysicalDevice</c>, which is what the Dart original branched on, so
    /// the simulator is detected from <see cref="SystemInfo.deviceModel"/>: on hardware it is an
    /// <c>"iPhone16,2"</c>-style identifier, on the simulator it reports the HOST CPU
    /// (<c>"x86_64"</c> / <c>"arm64"</c>). Anything that does not start with iPhone/iPad/iPod is
    /// therefore treated as the simulator — the conservative direction, since a false
    /// "ios-simulator" costs the player Trust rather than handing an attacker a clean label.
    ///
    /// <c>editor</c> is an honest label the Dart client never had to emit; the server penalises only
    /// <c>ios-simulator</c> (score.py:183), so it is reported truthfully rather than disguised.
    /// </summary>
    public sealed class UnityClientPlatformProbe : IClientPlatformProbe
    {
        public const string Ios = "ios";
        public const string IosSimulator = "ios-simulator";
        public const string Android = "android";
        public const string Editor = "editor";
        public const string Unknown = "unknown";

        public string Label()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer)
                return LooksLikeIosHardware(SystemInfo.deviceModel) ? Ios : IosSimulator;

            if (Application.platform == RuntimePlatform.Android) return Android;
            if (Application.isEditor) return Editor;
            return Unknown;
        }

        /// <summary>Exposed for the report's device/simulator evidence, and so a test can pin the rule.</summary>
        public static bool LooksLikeIosHardware(string deviceModel)
        {
            if (string.IsNullOrEmpty(deviceModel)) return false;
            return deviceModel.StartsWith("iPhone") ||
                   deviceModel.StartsWith("iPad") ||
                   deviceModel.StartsWith("iPod");
        }
    }

    /// <summary>
    /// The two anti-cheat signals attached to every submit. Names map 1:1 onto
    /// <c>ScorePostRequest.gps_is_mock</c> / <c>client_platform</c> (score.py:138-139).
    /// </summary>
    public sealed class GpsTrustSignals
    {
        public bool IsMock;
        public string ClientPlatform = UnityClientPlatformProbe.Unknown;

        /// <summary>Never throws and never returns null: a probe that fails degrades to
        /// <c>{false, "unknown"}</c> rather than blocking the submit (Dart catches both).</summary>
        public static GpsTrustSignals Capture(IMockLocationDetector mock, IClientPlatformProbe platform)
        {
            bool isMock = false;
            try { isMock = mock != null && mock.IsMock(); }
            catch { isMock = false; }

            string label = UnityClientPlatformProbe.Unknown;
            try
            {
                if (platform != null) label = platform.Label();
            }
            catch { label = UnityClientPlatformProbe.Unknown; }

            if (string.IsNullOrEmpty(label)) label = UnityClientPlatformProbe.Unknown;

            return new GpsTrustSignals { IsMock = isMock, ClientPlatform = label };
        }

        /// <summary>Shipping defaults: no native mock detector yet, real platform probe.</summary>
        public static GpsTrustSignals CaptureDefault()
            => Capture(new NeverMockDetector(), new UnityClientPlatformProbe());

        public override string ToString() => $"GpsTrustSignals(mock={IsMock}, platform={ClientPlatform})";
    }
}
