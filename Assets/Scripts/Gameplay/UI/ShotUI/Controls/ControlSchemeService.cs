using System;
using UnityEngine;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// Owns the player's shot-control scheme: reads it once, persists a change, and tells the
    /// shot UI and telemetry when it moves (control_scheme_seam §3.2).
    ///
    /// <para>PERSISTENCE IS PlayerPrefs, NOT SaveData — the same argument
    /// <c>QualityTierService</c> and <c>AudioManager</c> make. Which control scheme a player
    /// wants is a property of the DEVICE and the hand holding it, not of the account: an iPad
    /// and an iPhone signed into the same account must not share one, and it has to be readable
    /// before any account exists.</para>
    ///
    /// <para>ASSEMBLY. This sits in <c>Golfin.Gameplay.UI</c> — literally next to
    /// <c>QualityTierService</c>, the service SPEC §3.2 says to clone — rather than in
    /// <c>Golfin.Gameplay.Config</c>, because Config is <c>autoReferenced:false</c> and the
    /// Settings screen (<c>SettingsController</c>, <c>ControlsSubmenu</c>), the in-game gear
    /// modal and <c>TelemetryHooks</c> all live in Assembly-CSharp, which therefore cannot see
    /// it. <c>GameSession.AppendShotTimingKeys</c> already documents that same wall. See
    /// IMPLEMENTER_REPORT § "Assembly placement".</para>
    /// </summary>
    public static class ControlSchemeService
    {
        /// <summary>PlayerPrefs key. Absent or out of range = <see cref="ControlScheme.Flick"/>.</summary>
        public const string PrefKey = "golfin.controlScheme";

        static ControlScheme _current;
        static bool _read;

        /// <summary>The live scheme. Read from PlayerPrefs on first access and cached — a
        /// PlayerPrefs read per shot would be a per-shot syscall for a value only this class
        /// ever writes.</summary>
        public static ControlScheme Current
        {
            get
            {
                if (!_read)
                {
                    _current = Sanitize(PlayerPrefs.GetInt(PrefKey, (int)ControlScheme.Flick));
                    _read    = true;
                }
                return _current;
            }
        }

        /// <summary>Raised ONLY when the value actually moves. Subscribers: the Settings
        /// submenu, the in-game modal, and <c>ShotSchemeHost</c> (which defers the swap to the
        /// next Idle).</summary>
        public static event Action<ControlScheme> OnSchemeChanged;

        /// <summary>The same change with its provenance, for telemetry. Separate from
        /// <see cref="OnSchemeChanged"/> because the telemetry layer lives in Assembly-CSharp
        /// and this assembly does not reference <c>Golfin.Telemetry</c> — the identical relay
        /// argument <c>ShotTelemetryRelay</c> makes for the flick signals.
        /// Arguments: from, to, where ("settings" | "ingame").</summary>
        public static event Action<ControlScheme, ControlScheme, string> OnSchemeChangedDetailed;

        /// <param name="source">"settings" or "ingame" — becomes the <c>where</c> field of the
        /// <c>controls_scheme_changed</c> row, which is how we tell whether players discover
        /// this in the menu or mid-round.</param>
        public static void Set(ControlScheme scheme, string source)
        {
            scheme = Sanitize((int)scheme);

            ControlScheme from = Current;   // forces the first read before we overwrite the cache
            if (from == scheme)
            {
                Debug.Log($"[ControlScheme] already {scheme} (source={source}) — no change.");
                return;
            }

            _current = scheme;
            _read    = true;

            // Persist immediately: a scheme the player picked must survive a crash, not just a
            // clean quit (QualityTierService.SetOverride makes the same call for the same reason).
            PlayerPrefs.SetInt(PrefKey, (int)scheme);
            PlayerPrefs.Save();

            Debug.Log($"[ControlScheme] {from} -> {scheme} (source={source}).");

            OnSchemeChanged?.Invoke(scheme);
            OnSchemeChangedDetailed?.Invoke(from, scheme, source);
        }

        /// <summary>Localisation KEY (never a literal) for a scheme's player-facing label.</summary>
        public static string LabelKey(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Pendulum:  return "SETTINGS_CONTROLS_PENDULUM";
                case ControlScheme.Needle:    return "SETTINGS_CONTROLS_TAPTIMING";
                case ControlScheme.FreeSwing: return "SETTINGS_CONTROLS_FREESWING";
                default:                      return "SETTINGS_CONTROLS_FLICK";
            }
        }

        /// <summary>Anything outside the enum reads as Flick — a garbage pref must never leave
        /// the player with no working control scheme.</summary>
        static ControlScheme Sanitize(int raw)
        {
            return (raw >= (int)ControlScheme.Flick && raw <= (int)ControlScheme.FreeSwing)
                 ? (ControlScheme)raw
                 : ControlScheme.Flick;
        }

        /// <summary>Test seam: drop the cached read so the next <see cref="Current"/> re-reads
        /// PlayerPrefs. Statics do not survive a domain reload, but they DO survive between
        /// EditMode tests in one run.</summary>
        public static void ResetCacheForTests()
        {
            _read = false;
            _current = ControlScheme.Flick;
        }
    }
}
