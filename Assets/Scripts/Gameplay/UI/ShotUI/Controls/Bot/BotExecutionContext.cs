using System;
using UnityEngine;
using Golfin.Gameplay.Input;

namespace Golfin.Gameplay.UI.Controls.Bot
{
    /// <summary>
    /// Rejection-samples one scheme's execution error until the aim it produces is trunk-clear.
    ///
    /// <para><paramref name="sample"/> draws ONE raw value on that scheme's timing axis;
    /// <paramref name="yawDegFor"/> turns a raw value into the degrees of aim error it would
    /// produce once the scheme's own grader has seen it. The ACCEPTED raw value is what gets
    /// handed to <c>DriveBot</c>, so the shot the player watches is the one the sampler cleared —
    /// not a second, unchecked draw (bot_scheme_parity §3.3).</para>
    ///
    /// <para>A delegate rather than a direct <c>BotTreeProbe</c> call because the probe lives in
    /// <c>Golfin.Physics.Viewer</c>, which references THIS assembly. Handing the executor a
    /// closure is what lets the whole bot seam live next to the scheme drivers without inverting
    /// that reference.</para>
    /// </summary>
    /// <returns>false when every sample was trunk-blocked; the executor then fires the
    /// already-validated pre-error line, exactly as the Flick sampler has always done.</returns>
    public delegate bool BotTreeAwareSampler(Func<float> sample, Func<float, float> yawDegFor,
                                             out float accepted, out int canopyContacts);

    /// <summary>
    /// Everything an <see cref="IBotSchemeExecutor"/> needs from the world it is swinging in
    /// (bot_scheme_parity §3.1).
    ///
    /// <para>NAMES NO PHYSICS TYPE, ON PURPOSE. The spec's sketch had <c>PhysicsLabController</c>
    /// and a tree provider as fields, which cannot compile: <c>Golfin.Physics.Viewer</c> already
    /// references <c>Golfin.Gameplay.UI</c>, so a field of that type here would be a reference
    /// cycle. Everything the executor needs from the lab arrives as a DELEGATE the bot supplies
    /// — which also means a bot with no lab at all (a smoke rig, an EditMode test) simply leaves
    /// them null and still gets a real swing. Noted in the implementer report as the §4 "check
    /// the reference direction" item.</para>
    /// </summary>
    public sealed class BotExecutionContext
    {
        /// <summary>The controller the swing is committed through. Required.</summary>
        public ShotController Shot;

        /// <summary>Whether <see cref="Resolve"/> actually found one. A bool and not a null check
        /// on <see cref="Shot"/> because the callers that most need it — PerfBaselineBot, the
        /// editor capture rigs — live in assemblies that do not reference
        /// <c>Golfin.Gameplay.Input</c> and so cannot name <c>ShotController</c> even to compare
        /// it against null.</summary>
        public bool HasShot => Shot != null;

        /// <summary>Uniform sampler, <c>(min, max) =&gt; value</c>. Defaults to
        /// <c>UnityEngine.Random.Range</c>. Injectable so a golden-file regression can seed it and
        /// get the same delta sequence the pre-refactor VersusBot produced.</summary>
        public Func<float, float, float> Range = UnityEngine.Random.Range;

        /// <summary>
        /// Points the world at the shot the executor has just finished sampling: set the club,
        /// sync <c>ClubContext</c>, clear the stat-bundle override and turn the camera to the
        /// FINAL aim. Returns the club actually resolved (the bag may not carry the lab index).
        ///
        /// <para>Called by the executor AFTER the error is folded in, which is the order
        /// <c>VersusBot</c> used before the cut and the order the camera has to see: pointing it
        /// at the pre-error line and then perturbing the aim would fire the ball somewhere the
        /// chase camera was never looking.</para>
        ///
        /// <para>null (smoke / perf / capture bots) = the bot has already aimed itself.</para>
        /// </summary>
        public Func<int, float, int> ApplySwing;

        /// <summary>True once the ball's own state machine is ready to be hit. null = skip the
        /// gate (no lab in the scene).</summary>
        public Func<bool> BallReady;

        /// <summary>Tree-aware rejection sampler, or null for "accept the first draw" — which is
        /// what a putt, a treeless hole and <c>DebugDisableTreeRecheck</c> all want, and is
        /// byte-identical to the pre-cut else-branch (one draw, no probe).</summary>
        public BotTreeAwareSampler TreeSampler;

        /// <summary>Seconds the club handle takes to reach the planned power. 0.85 is the number
        /// every bot has ramped at since <c>versus_bot_hardening</c>; changing it changes the look
        /// of every bot swing, so it is a field and not a literal.</summary>
        public float RampSeconds = 0.85f;

        /// <summary>Seconds the Flick executor holds at full power before releasing.</summary>
        public float HoldSeconds = 0.18f;

        /// <summary>Flick only: release without measuring the flick. Capture rigs that must not
        /// lose a take to a gate they never gestured set this; VersusBot never does.</summary>
        public bool BypassFlickGate;

        /// <summary>Prefix on this bot's log lines. Left as the VersusBot tag by default because
        /// the "2b error" line is a golden-file artefact of that bot and its format is pinned.</summary>
        public string LogTag = "[VersusBot]";

        /// <summary>
        /// Build a context around whatever <c>ShotController</c> is in the scene. The entry point
        /// for bots that live in assemblies which do not reference <c>Golfin.Gameplay.Input</c>
        /// and so cannot name the controller type at all (PerfBaselineBot, the editor capture
        /// rigs) — they used reflection for the whole drag path before this existed.
        /// </summary>
        public static BotExecutionContext Resolve()
        {
            var shot = UnityEngine.Object.FindFirstObjectByType<ShotController>();
            if (shot == null)
                Debug.LogWarning("[BotSwing] No ShotController in the scene — the swing will no-op.");
            return new BotExecutionContext { Shot = shot };
        }
    }
}
