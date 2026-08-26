using UnityEngine;
using UnityEngine.Rendering;

namespace Golfin.Gameplay.UI.Quality
{
    /// <summary>
    /// Device -> <see cref="QualityTier"/>. Pure and static so the whole table is unit-testable
    /// without a device: the parameterised <see cref="Resolve(RuntimePlatform,string,string,GraphicsDeviceType,int,out string)"/>
    /// overload takes every input explicitly and the zero-arg overload is the only thing that
    /// touches <see cref="SystemInfo"/>.
    ///
    /// The table lives in code, not CSV (brief Q1): it has to run before any content loads, it is
    /// read exactly once per launch, and a bad row shipped in a CSV would be unfixable without a
    /// content push. UNKNOWN HARDWARE RESOLVES TO MID — never Low (a good phone must not be
    /// punished for being new) and never High (a bad phone must not be cooked for being obscure).
    /// </summary>
    public static class QualityTierResolver
    {
        public static QualityTier Resolve() => Resolve(out _);

        /// <summary>Resolve from this device's <see cref="SystemInfo"/>.</summary>
        public static QualityTier Resolve(out string reason) =>
            Resolve(Application.platform,
                    SystemInfo.deviceModel,
                    SystemInfo.graphicsDeviceName,
                    SystemInfo.graphicsDeviceType,
                    SystemInfo.systemMemorySize,
                    out reason);

        /// <summary>Pure form — the one the tests drive.</summary>
        public static QualityTier Resolve(RuntimePlatform platform, string deviceModel, string gpuName,
                                          GraphicsDeviceType gfxApi, int memoryMb, out string reason)
        {
            switch (platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    return ResolveIOS(deviceModel, out reason);
                case RuntimePlatform.Android:
                    return ResolveAndroid(gpuName, gfxApi, memoryMb, out reason);
                default:
                    // Editor and desktop standalone: development machines, always High. This is also
                    // what makes an Editor play-through show the High frame unless overridden.
                    reason = "editor-or-standalone";
                    return QualityTier.High;
            }
        }

        // ── iOS ─────────────────────────────────────────────────────────────────────────
        //
        // SystemInfo.deviceModel is the hardware identifier ("iPhone16,2"), NOT the marketing name.
        // The major number is one generation AHEAD of the marketing number for iPhones, so the
        // thresholds below read oddly until you line them up:
        //   iPhone10,x = iPhone X   iPhone11,x = XS/XR      -> A11/A12  -> Low
        //   iPhone12,x = iPhone 11 + SE2 (A13)
        //   iPhone13,x = iPhone 12  (A14)                   -> Mid
        //   iPhone14,x = iPhone 13 + SE3 + 14 (A15) and up  -> High
        static QualityTier ResolveIOS(string deviceModel, out string reason)
        {
            if (!TryParseAppleModel(deviceModel, out string family, out int major))
            {
                reason = "ios-unparseable-model";
                return QualityTier.Mid;
            }

            switch (family)
            {
                case "iPhone":
                    if (major <= 11) { reason = $"ios-iPhone{major}-le11-A11orA12"; return QualityTier.Low;  }
                    if (major <= 13) { reason = $"ios-iPhone{major}-12to13-A13orA14"; return QualityTier.Mid;  }
                                     { reason = $"ios-iPhone{major}-ge14-A15plus";  return QualityTier.High; }

                case "iPad":
                    if (major >= 13) { reason = $"ios-iPad{major}-ge13";  return QualityTier.High; }
                    if (major >= 8)  { reason = $"ios-iPad{major}-8to12"; return QualityTier.Mid;  }
                                     { reason = $"ios-iPad{major}-lt8";   return QualityTier.Low;  }

                case "iPod":
                    reason = "ios-ipod";
                    return QualityTier.Low;

                default:
                    reason = $"ios-unknown-family-{family}";
                    return QualityTier.Mid;
            }
        }

        /// <summary>"iPhone16,2" -> ("iPhone", 16). False when the string is not an Apple identifier.</summary>
        internal static bool TryParseAppleModel(string model, out string family, out int major)
        {
            family = null; major = 0;
            if (string.IsNullOrEmpty(model)) return false;

            int i = 0;
            while (i < model.Length && !char.IsDigit(model[i])) i++;
            if (i == 0 || i >= model.Length) return false;          // no letters, or no digits at all

            int digitStart = i;
            while (i < model.Length && char.IsDigit(model[i])) i++;
            if (i >= model.Length || model[i] != ',') return false;  // identifiers are always "<name><n>,<n>"

            if (!int.TryParse(model.Substring(digitStart, i - digitStart), out major)) return false;
            family = model.Substring(0, digitStart);
            return true;
        }

        // ── Android ─────────────────────────────────────────────────────────────────────
        //
        // Start at Mid, let the GPU move it, then let the CAPS only ever move it DOWN. An unknown
        // GPU therefore lands on Mid but still gets demoted by a 3 GB device or a GLES3-only driver,
        // which is the combination that actually predicts a bad time.
        static QualityTier ResolveAndroid(string gpuName, GraphicsDeviceType gfxApi, int memoryMb, out string reason)
        {
            string g = gpuName ?? string.Empty;
            QualityTier tier;
            string gpuRule;

            int adreno = AdrenoNumber(g);

            if (Has(g, "Immortalis") || Has(g, "Xclipse") ||
                (adreno >= 700 && adreno <= 899) ||
                Has(g, "Mali-G710") || Has(g, "Mali-G715") || Has(g, "Mali-G720"))
            {
                tier = QualityTier.High; gpuRule = "gpu-high";
            }
            else if ((adreno >= 640 && adreno <= 699) ||
                     Has(g, "Mali-G76") || Has(g, "Mali-G77") || Has(g, "Mali-G78") || Has(g, "Mali-G68"))
            {
                tier = QualityTier.Mid; gpuRule = "gpu-mid";
            }
            else if ((adreno >= 500 && adreno <= 639) ||
                     HasMaliSeries(g, '5') || HasMaliSeries(g, '3') || Has(g, "Mali-T") || Has(g, "PowerVR"))
            {
                tier = QualityTier.Low; gpuRule = "gpu-low";
            }
            else
            {
                tier = QualityTier.Mid; gpuRule = "gpu-unknown-default-mid";
            }

            string caps = "";

            // A GLES3-only driver means no Vulkan path; the render-scale + shadow budget High assumes
            // does not hold there.
            if (gfxApi == GraphicsDeviceType.OpenGLES3 && tier > QualityTier.Mid)
            {
                tier = QualityTier.Mid; caps += "+gles3-cap-mid";
            }

            if (memoryMb < 3500)
            {
                tier = QualityTier.Low; caps += "+mem-lt-3500-low";
            }
            else if (memoryMb < 5500 && tier > QualityTier.Mid)
            {
                tier = QualityTier.Mid; caps += "+mem-lt-5500-cap-mid";
            }

            reason = $"android-{gpuRule}{caps}";
            return tier;
        }

        static bool Has(string haystack, string needle) =>
            haystack.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>"Mali-G52 MC2" with series '5' -> true. Matches Mali-G&lt;digit&gt;&lt;digit&gt; only, so G710 never counts as G7x.</summary>
        static bool HasMaliSeries(string g, char series)
        {
            int idx = g.IndexOf("Mali-G", System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            int d = idx + 6;
            if (d >= g.Length || g[d] != series) return false;
            // Exactly two digits: G52 yes, G520 (does not exist, but be strict) no.
            if (d + 1 >= g.Length || !char.IsDigit(g[d + 1])) return false;
            return d + 2 >= g.Length || !char.IsDigit(g[d + 2]);
        }

        /// <summary>"Adreno (TM) 740" -> 740. -1 when the name is not an Adreno or carries no number.</summary>
        static int AdrenoNumber(string g)
        {
            int idx = g.IndexOf("Adreno", System.StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return -1;

            int i = idx + 6;
            while (i < g.Length && !char.IsDigit(g[i])) i++;   // skips the " (TM) "
            if (i >= g.Length) return -1;

            int start = i;
            while (i < g.Length && char.IsDigit(g[i])) i++;
            return int.TryParse(g.Substring(start, i - start), out int n) ? n : -1;
        }
    }
}
