using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Input;

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
    ///   - HoleCard/HoleMapContainer — the round map thumbnail (Cesar, 2026-08-25: the map-view
    ///     icon must NOT be on screen during the ball's flight; flagged in Versus, where
    ///     VersusHudController hides the ChipStack so the lone icon reads as a live control,
    ///     but the hide applies in solo too). The rest of the HoleCard (course / hole / par
    ///     chips) still stays up, per the original scope call.
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

        private readonly List<bool> _wasActive = new List<bool>();
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

            _wasActive.Clear();
            foreach (var go in _hideDuringShot)
            {
                _wasActive.Add(go != null && go.activeSelf);
                if (go != null) go.SetActive(false);
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

            for (int i = 0; i < _hideDuringShot.Count; i++)
            {
                var go = _hideDuringShot[i];
                if (go == null) continue;
                go.SetActive(i < _wasActive.Count && _wasActive[i]);
            }
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

        private static void CloseIfOpen(GameObject go, System.Action close)
        {
            // Calling Close() on an already-closed overlay would re-activate the central ball
            // and aim cone that the shot-state widgets have just hidden, so only close what is open.
            if (go != null && go.activeSelf) close();
        }
    }
}
