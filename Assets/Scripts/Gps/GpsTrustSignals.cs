// Order: gps_trust_core §3 — port of gps_trust_signals.dart (M1 mock detection, M2 simulator label).
using System;
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

        /// <summary>
        /// gps_standalone_shell §D6 — the PLAYLIFE shell's label, so the backend and the admin
        /// dashboard can tell the two iOS apps apart on rows they both write (`activities`,
        /// `scores`). The server only ever branches on <see cref="IosSimulator"/>
        /// (<c>score.py:189</c> treats it as mock) and stores the rest verbatim, so a new value
        /// costs nothing there.
        /// <para>Kept in step with <c>GolfinRedux.AppVariantInfo.PlayLife</c>, which is the same
        /// string for telemetry. Two constants rather than one shared file because
        /// <c>Golfin.Gps</c> is an asmdef and cannot see Assembly-CSharp — the define is what
        /// they actually share.</para>
        /// </summary>
        public const string IosPlayLife = "ios-playlife";

        public string Label()
        {
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                // The simulator test comes FIRST in every variant. A shell build running on the
                // simulator must still report "ios-simulator" or it would hand an attacker a
                // clean label just by choosing the other app — the trust penalty is the whole
                // reason this probe exists.
                if (IsSimulator(EnvOrNull("SIMULATOR_UDID"),
                                EnvOrNull("SIMULATOR_MODEL_IDENTIFIER"),
                                SystemInfo.graphicsDeviceName,
                                SystemInfo.deviceModel))
                    return IosSimulator;

#if GOLFIN_STANDALONE
                return IosPlayLife;
#else
                return Ios;
#endif
            }

            if (Application.platform == RuntimePlatform.Android) return Android;
            if (Application.isEditor) return Editor;
            return Unknown;
        }

        /// <summary>
        /// Print the resolved label ONCE at boot.
        ///
        /// <para>
        /// The label decides a Trust penalty (score.py:183 docks a submit tagged
        /// <c>ios-simulator</c>) and it is derived by INFERENCE — Unity has no
        /// <c>isPhysicalDevice</c>, so this is INFERRED — and the inputs it infers from are worth
        /// having on the record, since the first rule this project shipped read one of them wrong.
        /// A guess that costs a player Trust has to be observable on the surface it runs on, and a
        /// TestFlight build has no console to read: one line in the device log, at boot, before any
        /// submit, is the whole cost of being able to answer "what did it think it was?".
        /// </para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void LogPlatformAtBoot()
        {
            Debug.Log("[UnityClientPlatformProbe] deviceModel='" + SystemInfo.deviceModel +
                      "' os='" + SystemInfo.operatingSystem +
                      "' gpu='" + SystemInfo.graphicsDeviceName +
                      "' SIMULATOR_MODEL_IDENTIFIER='" + (Environment.GetEnvironmentVariable("SIMULATOR_MODEL_IDENTIFIER") ?? "<null>") +
                      "' SIMULATOR_DEVICE_NAME='" + (Environment.GetEnvironmentVariable("SIMULATOR_DEVICE_NAME") ?? "<null>") +
                      "' SIMULATOR_UDID='" + (Environment.GetEnvironmentVariable("SIMULATOR_UDID") ?? "<null>") +
                      "' platform=" + Application.platform +
                      " -> " + new UnityClientPlatformProbe().Label());
        }

        /// <summary>
        /// The simulator rule, as a pure function so a test can pin it without an iOS build.
        ///
        /// <para>
        /// Three signals, most certain first: the CoreSimulator environment variables (injected
        /// into every process the simulator hosts, absent on a device), the software GPU's name,
        /// and finally the model identifier — which on Apple Silicon is NOT distinguishing and is
        /// kept only for the case where the environment is somehow stripped. That last one still
        /// errs toward "simulator" for an unrecognised model, which is the safe direction.
        /// </para>
        /// </summary>
        public static bool IsSimulator(string simulatorUdidEnv,
                                       string simulatorModelEnv,
                                       string graphicsDeviceName,
                                       string deviceModel)
        {
            if (!string.IsNullOrEmpty(simulatorUdidEnv) || !string.IsNullOrEmpty(simulatorModelEnv))
                return true;

            if (!string.IsNullOrEmpty(graphicsDeviceName) &&
                graphicsDeviceName.IndexOf("simulator", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return !LooksLikeIosHardware(deviceModel);
        }

        /// <summary>Environment reads are sandboxed on iOS and can throw; a probe must never be the
        /// thing that breaks a submit.</summary>
        private static string EnvOrNull(string name)
        {
            try { return Environment.GetEnvironmentVariable(name); }
            catch (Exception) { return null; }
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
