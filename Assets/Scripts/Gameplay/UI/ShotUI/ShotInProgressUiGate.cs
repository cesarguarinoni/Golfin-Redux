using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Input;
using Golfin.Gameplay.Session;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Hides the shot-input UI for the duration of a shot and brings it back when the
    /// next shot is armed (Cesar, 2026-08-06: "Shoot UI buttons and all the shooting UI
    /// should only be seen and interactable during shooting").
    ///
    /// "In progress" = <see cref="ShotState.Flicking"/> or <see cref="ShotState.Resolving"/>,
    /// i.e. from the moment the flick commits until <c>ShotController.CompleteShot()</c>
    /// drops the state back to Idle once the ball settles.
    ///
    /// Widgets that already own their own visibility keep owning it (ShotConeView, ConeAlphaController,
    /// CentralBallWidget, PowerGaugeWidget) — this gate covers the pieces that have no live owner:
    ///   - the ActionButtons_Cluster CanvasGroup (alpha 0 + inert). NOTE: ActionButtonsRoot is the
    ///     component nominally responsible for that cluster, but its _shotController reference is
    ///     null in LabScaffold.unity, so it has never run; wiring it also changes pre-shot
    ///     interaction semantics, which is out of scope here.
    ///   - PutterTrack / PuttPathRoot (owned by PhysicsLabController's putt-mode toggle)
    ///   - HoleCard/HoleMapContainer — the round map thumbnail, VERSUS ONLY (Cesar, 2026-08-25:
    ///     the map-view icon must NOT be on screen during the ball's flight). The defect this
    ///     fixed is specific to Versus, where VersusHudController hides the ChipStack and
    ///     repositions the lone icon so it reads as a live control. In Practice/solo the
    ///     thumbnail sits top-left as part of the full HoleCard and must STAY ON SCREEN for the
    ///     whole shot (Cesar, 2026-08-27) — it is only made non-clickable, never hidden.
    ///     The rest of the HoleCard (course / hole / par chips) always stays up.
    ///   - the selector / spin overlays (defensive close; they cannot normally survive a flick)
    ///   - the HoleCard map button, kept inert as well so no tap can land on the frame the
    ///     container goes back on
    ///
    /// The hide is edge-triggered and remembers each object's <c>activeSelf</c>, so re-arming
    /// never switches on a putter track that putt mode had legitimately turned off.
    /// </summary>
    public class ShotInProgressUiGate : MonoBehaviour
    {
        [SerializeField] private ShotController _shotController;

        [Header("Hidden while the shot is in progress (active state is restored at re-arm)")]
        [SerializeField] private List<GameObject> _hideDuringShot = new List<GameObject>();

        [Header("Faded out + made inert while the shot is in progress (ActionButtons_Cluster)")]
        [SerializeField] private List<CanvasGroup> _hideGroupsDuringShot = new List<CanvasGroup>();

        [Header("Force-closed when the shot commits")]
        [SerializeField] private SelectorOverlayWidget _clubSelector;
        [SerializeField] private SelectorOverlayWidget _ballSelector;
        [SerializeField] private SpinPanelWidget       _spinPanel;
        [SerializeField] private OtherButtonsFader     _actionButtonsFader;

        [Header("Made inert while the shot is in progress (its container is hidden too)")]
        [SerializeField] private Button _holeMapButton;

        /// <summary>
        /// True from flick-commit until the ball settles and the next shot is armed.
        /// Read by HoleCardWidget so no code path can open the map view mid-flight.
        /// </summary>
        public static bool ShotInProgress { get; private set; }

        // Parallel lists of exactly what Apply() switched off and the activeSelf it had — the
        // hide list is filtered per mode (see IsHoleMapContainer), so an index into
        // _hideDuringShot is NOT a valid index into the restore state.
        private readonly List<GameObject> _hidden    = new List<GameObject>();
        private readonly List<bool>       _wasActive = new List<bool>();
        private bool _gated;

        private void OnEnable()
        {
            if (_shotController != null) _shotController.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_shotController != null) _shotController.OnStateChanged -= HandleStateChanged;
            // Never leave the static latched behind us (scene unload, match end, domain reload).
            Release();
        }

        private void HandleStateChanged(ShotInputState state)
        {
            bool inProgress = state.State is ShotState.Flicking or ShotState.Resolving;
            if (inProgress == _gated) return;   // OnStateChanged fires every frame — edge only

            if (inProgress) Apply();
            else            Release();
        }

        private void Apply()
        {
            _gated          = true;
            ShotInProgress  = true;

            // Practice/solo (and tournament) keep the map thumbnail on screen for the whole
            // shot — only Versus hides it. Everything else in the list hides in every mode.
            bool hideHoleMap = GameSession.IsVersus;

            _hidden.Clear();
            _wasActive.Clear();
            foreach (var go in _hideDuringShot)
            {
                if (go == null) continue;
                if (!hideHoleMap && IsHoleMapContainer(go)) continue;

                _hidden.Add(go);
                _wasActive.Add(go.activeSelf);
                go.SetActive(false);
            }

            foreach (var cg in _hideGroupsDuringShot)
            {
                if (cg == null) continue;
                cg.alpha          = 0f;
                cg.interactable   = false;
                cg.blocksRaycasts = false;
            }

            CloseIfOpen(_clubSelector != null ? _clubSelector.gameObject : null, () => _clubSelector.Close());
            CloseIfOpen(_ballSelector != null ? _ballSelector.gameObject : null, () => _ballSelector.Close());
            CloseIfOpen(_spinPanel    != null ? _spinPanel.gameObject    : null, () => _spinPanel.Close());

            if (_holeMapButton != null) _holeMapButton.interactable = false;
        }

        private void Release()
        {
            ShotInProgress = false;
            if (!_gated) return;
            _gated = false;

            for (int i = 0; i < _hidden.Count; i++)
            {
                var go = _hidden[i];
                if (go != null) go.SetActive(_wasActive[i]);
            }
            _hidden.Clear();
            _wasActive.Clear();

            foreach (var cg in _hideGroupsDuringShot)
            {
                if (cg == null) continue;
                cg.alpha          = 1f;
                cg.interactable   = true;
                cg.blocksRaycasts = true;
            }

            // A force-closed selector leaves the per-button CanvasGroups faded; put them back.
            if (_actionButtonsFader != null) _actionButtonsFader.RestoreAll();

            if (_holeMapButton != null) _holeMapButton.interactable = true;
        }

        /// <summary>
        /// True when <paramref name="go"/> is the HoleCard map thumbnail container — i.e. the
        /// wired <see cref="_holeMapButton"/> is that GameObject or lives under it.
        ///
        /// Derived from the button rather than a second serialized reference so the two can
        /// never drift apart in the scene: the container the gate skips is by construction the
        /// one that owns the map button it also makes inert.
        /// </summary>
        private bool IsHoleMapContainer(GameObject go)
        {
            if (go == null || _holeMapButton == null) return false;
            return _holeMapButton.transform.IsChildOf(go.transform);   // IsChildOf is true for self
        }

        private static void CloseIfOpen(GameObject go, System.Action close)
        {
            // Calling Close() on an already-closed overlay would re-activate the central ball
            // and aim cone that the shot-state widgets have just hidden, so only close what is open.
            if (go != null && go.activeSelf) close();
        }
    }
}
