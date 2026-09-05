using UnityEngine;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.UI.Controls.Bot;

namespace Golfin.Gameplay.UI.Controls
{
    /// <summary>
    /// Owns which control-scheme driver root is live under <c>ShotUI_Canvas</c>
    /// (control_scheme_seam §3.3).
    ///
    /// <para>One root per scheme, indexed by <see cref="ControlScheme"/>.
    /// <c>SchemeRoot_Flick</c> is the existing club handle + cone + slab + arrows, re-parented
    /// and otherwise untouched; the other three are empty roots carrying a
    /// <see cref="PlaceholderSchemeDriver"/> until their own specs land.</para>
    ///
    /// <para>NEVER SWAPS MID-SWING. A scheme change that arrives while the player is pulling or
    /// timing is latched and applied at the next <c>Idle</c>: yanking the cone out from under a
    /// half-pulled shot would strand <c>ShotController</c> in an external drag with no driver
    /// left to release it.</para>
    ///
    /// <para>UNIMPLEMENTED SCHEMES KEEP FLICK LIVE. Picking Needle or Free Swing on this build
    /// persists the preference and stamps it on the telemetry row, but the input still comes from
    /// the flick root — a tester who picks one gets a playable game rather than a dead screen.
    /// A scheme whose driver reports <c>IsImplemented</c> gets the flick root turned OFF instead:
    /// two live pointer handlers over one ball is two shots per swing.</para>
    /// </summary>
    public class ShotSchemeHost : MonoBehaviour
    {
        [Tooltip("One root per ControlScheme, in enum order: Flick, Pendulum, Needle, FreeSwing.")]
        [SerializeField] private GameObject[] _schemeRoots = new GameObject[4];

        [Tooltip("The shot controller whose state gates a mid-swing swap.")]
        [SerializeField] private ShotController _shotController;

        /// <summary>The scheme whose root is currently live. May lag
        /// <see cref="ControlSchemeService.Current"/> while a swing is in progress.</summary>
        public ControlScheme ActiveScheme { get; private set; } = ControlScheme.Flick;

        /// <summary>True while a scheme change is waiting for the shot to return to Idle.</summary>
        public bool HasPendingSwap => _hasPending;

        /// <summary>
        /// The bot side of whatever scheme is LIVE (bot_scheme_parity §3.1) — the answer
        /// <c>BotSwing.Play</c> resolves for every bot in the game.
        ///
        /// <para>It follows the INPUT root, not the selected scheme, which matters for exactly the
        /// case this host already special-cases: an unimplemented scheme keeps the flick root
        /// live underneath it, and a bot that swung Pendulum while the player's own finger was
        /// still on the flick cone would be playing a different game from them.</para>
        ///
        /// <para>DERIVED FROM THE DRIVER, NOT AUTHORED ON THE ROOT. A component per root would be
        /// four more Inspector references to wire and to keep wired through every prefab revision,
        /// and a missing one would silently degrade a scheme's bots to Flick — the exact failure
        /// this task exists to remove. A scene-authored <see cref="IBotSchemeExecutor"/> still
        /// wins if one is present, so an experiment can override a scheme's bot behaviour without
        /// touching this file.</para>
        /// </summary>
        public IBotSchemeExecutor ActiveExecutor => _activeExecutor ?? FlickBotExecutor.Instance;

        private IBotSchemeExecutor _activeExecutor;

        private bool          _hasPending;
        private ControlScheme _pending;
        private IShotSchemeDriver _activeDriver;

        /// <summary>Test seam — wire the roots and run the enable work by hand. EditMode does
        /// NOT call OnEnable on a plain MonoBehaviour, so a test that only assigned the fields
        /// would be testing an object that never started. Pair with
        /// <see cref="ReleaseForTests"/>: OnDisable does not fire in EditMode either, and the
        /// scheme-changed event is static, so an un-released host leaks into the next test.</summary>
        public void ConfigureForTests(GameObject[] roots, ShotController controller)
        {
            _schemeRoots    = roots;
            _shotController = controller;
            Bind();
        }

        /// <summary>Test seam — the OnDisable half. See <see cref="ConfigureForTests"/>.</summary>
        public void ReleaseForTests() => Unbind();

        private void OnEnable()  => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            Unbind();   // idempotent: never double-subscribe if both OnEnable and a test ran

            ControlSchemeService.OnSchemeChanged += OnSchemeChanged;
            if (_shotController != null) _shotController.OnStateChanged += OnShotStateChanged;
            _bound = true;

            _hasPending = false;
            Apply(ControlSchemeService.Current);
        }

        private void Unbind()
        {
            if (!_bound) return;
            ControlSchemeService.OnSchemeChanged -= OnSchemeChanged;
            if (_shotController != null) _shotController.OnStateChanged -= OnShotStateChanged;
            _bound = false;
        }

        private bool _bound;

        private void OnSchemeChanged(ControlScheme scheme)
        {
            // No controller wired (EditMode, or a scene that has not booted a shot yet) means
            // there is no swing to protect — apply straight away rather than latching forever.
            if (_shotController == null || _shotController.State == ShotState.Idle)
            {
                _hasPending = false;
                Apply(scheme);
                return;
            }

            _pending    = scheme;
            _hasPending = true;
            Debug.Log($"[ShotSchemeHost] scheme change to {scheme} deferred — shot state is {_shotController.State}.");
        }

        private void OnShotStateChanged(ShotInputState state)
        {
            if (!_hasPending || state.State != ShotState.Idle) return;
            _hasPending = false;
            Apply(_pending);
        }

        private void Apply(ControlScheme scheme)
        {
            if (_schemeRoots == null || _schemeRoots.Length == 0) return;

            GameObject wanted = RootFor(scheme);
            IShotSchemeDriver driver = wanted != null ? wanted.GetComponent<IShotSchemeDriver>() : null;

            // A scheme whose driver has not shipped yet borrows the flick root's input. The
            // driver answers for itself (scheme_pendulum §3.1) — a type test against the
            // placeholder would need a new case for every real driver that lands.
            bool implemented = driver != null && driver.IsImplemented;
            GameObject inputRoot = implemented ? wanted : RootFor(ControlScheme.Flick);

            if (_activeDriver != null) _activeDriver.Deactivate();

            for (int i = 0; i < _schemeRoots.Length; i++)
            {
                GameObject root = _schemeRoots[i];
                if (root == null) continue;
                bool live = root == wanted || root == inputRoot;
                if (root.activeSelf != live) root.SetActive(live);
            }

            // Bind/Activate the root that actually reads the player's finger. For an
            // unimplemented scheme that is Flick; its placeholder is still activated so it logs
            // the "not implemented" line exactly once.
            IShotSchemeDriver inputDriver = inputRoot != null ? inputRoot.GetComponent<IShotSchemeDriver>() : null;
            if (!implemented && driver != null)
            {
                driver.Bind(_shotController);
                driver.Activate();
            }
            if (inputDriver != null)
            {
                inputDriver.Bind(_shotController);
                inputDriver.Activate();
            }

            _activeDriver   = inputDriver;
            _activeExecutor = ResolveExecutor(inputRoot, inputDriver);
            ActiveScheme    = scheme;
        }

        /// <summary>See <see cref="ActiveExecutor"/>. A driver that has not shipped (or none at
        /// all) resolves to Flick, which is also the root the player's finger is on.</summary>
        private static IBotSchemeExecutor ResolveExecutor(GameObject inputRoot, IShotSchemeDriver driver)
        {
            var authored = inputRoot != null ? inputRoot.GetComponent<IBotSchemeExecutor>() : null;
            if (authored != null) return authored;

            switch (driver)
            {
                case Pendulum.PendulumSchemeDriver p:   return new PendulumBotExecutor(p);
                case Needle.NeedleSchemeDriver n:       return new NeedleBotExecutor(n);
                case FreeSwing.FreeSwingSchemeDriver f: return new FreeSwingBotExecutor(f);
                default:                                return FlickBotExecutor.Instance;
            }
        }

        private GameObject RootFor(ControlScheme scheme)
        {
            int idx = (int)scheme;
            return (_schemeRoots != null && idx >= 0 && idx < _schemeRoots.Length) ? _schemeRoots[idx] : null;
        }
    }
}
