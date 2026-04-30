using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Owns the hold/tap state machine on a trigger button (DriverButton or GolfinButton).
    /// Attach alongside the Button component on the trigger GO.
    ///
    /// Two interaction modes from a single PointerDown:
    ///   HOLD-MODE  – finger stays down; selector follows hover; release on card commits.
    ///   MODAL-MODE – quick tap (finger up on trigger, no significant drag, < holdThresholdMs)
    ///               → selector stays open; subsequent tap on card commits; outside tap closes.
    /// </summary>
    public class SelectorDragRouter : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IDragHandler
    {
        // ── Inspector knobs ────────────────────────────────────────────────────
        [SerializeField] private SelectorOverlayWidget _selectorOverlay;
        [SerializeField] private OtherButtonsFader     _fader;
        [SerializeField] private CanvasGroup           _myCanvasGroup;   // own CG to hide self

        [Tooltip("Milliseconds. Finger-down shorter than this counts as a tap → modal mode.")]
        [SerializeField] private float _holdThresholdMs     = 150f;

        [Tooltip("Unity units. Drag more than this → definitively HOLD mode, not modal.")]
        [SerializeField] private float _dragDistanceThreshold = 8f;

        [Tooltip("Scale applied to the card under the finger in hold mode.")]
        [SerializeField] private float _highlightScale      = 1.05f;

        [Tooltip("Seconds before arrow auto-scroll triggers in hold mode.")]
        [SerializeField] private float _arrowRepeatDelay    = 0.3f;

        [Tooltip("Seconds between subsequent arrow auto-scroll steps in hold mode.")]
        [SerializeField] private float _arrowRepeatInterval = 0.15f;

        // ── State ──────────────────────────────────────────────────────────────
        enum Mode { Idle, Hold, Modal }
        Mode _mode = Mode.Idle;

        float   _pointerDownTime;
        Vector2 _pointerDownPos;
        bool    _exceededDragThreshold;
        bool    _selectorOpen;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        void Awake()
        {
            if (_myCanvasGroup == null)
                _myCanvasGroup = GetComponent<CanvasGroup>();
        }

        // ── IPointerDownHandler ────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData ev)
        {
            if (_mode == Mode.Modal) { CloseSelector(); return; }  // tap button again → close
            if (_mode != Mode.Idle) return;  // already open; ignore re-down

            _pointerDownTime        = Time.realtimeSinceStartup;
            _pointerDownPos         = ev.position;
            _exceededDragThreshold  = false;
            _mode                   = Mode.Hold;

            OpenSelector();
        }

        // ── IDragHandler ───────────────────────────────────────────────────────

        public void OnDrag(PointerEventData ev)
        {
            if (_mode == Mode.Idle) return;

            float dist = Vector2.Distance(ev.position, _pointerDownPos);
            if (dist > _dragDistanceThreshold)
                _exceededDragThreshold = true;

            if (_mode == Mode.Hold && _selectorOpen)
                _selectorOverlay.UpdateHoldHover(ev.position, _arrowRepeatDelay, _arrowRepeatInterval);
        }

        // ── IPointerUpHandler ──────────────────────────────────────────────────

        public void OnPointerUp(PointerEventData ev)
        {
            if (_mode == Mode.Idle) return;

            if (_mode == Mode.Hold)
            {
                float elapsed = (Time.realtimeSinceStartup - _pointerDownTime) * 1000f;
                bool isTap    = !_exceededDragThreshold && elapsed < _holdThresholdMs;

                // Ask the overlay whether the finger is on a card or arrow
                SelectorOverlayWidget.ReleaseResult result = _selectorOpen
                    ? _selectorOverlay.EvaluateRelease(ev.position)
                    : SelectorOverlayWidget.ReleaseResult.Outside;

                switch (result)
                {
                    case SelectorOverlayWidget.ReleaseResult.OnCard:
                        // Commit
                        _selectorOverlay.CommitHighlighted();
                        CloseSelector();
                        break;

                    case SelectorOverlayWidget.ReleaseResult.OnArrow:
                        // Release on arrow → enter modal mode (selector stays open)
                        _mode = Mode.Modal;
                        _selectorOverlay.EnterModalMode();
                        break;

                    case SelectorOverlayWidget.ReleaseResult.Outside:
                        if (isTap)
                        {
                            // Quick tap on trigger button → modal mode
                            _mode = Mode.Modal;
                            _selectorOverlay.EnterModalMode();
                        }
                        else
                        {
                            // Drag to outside → cancel
                            CloseSelector();
                        }
                        break;
                }
            }
            // In Modal mode PointerUp on trigger itself counts as an outside tap
            else if (_mode == Mode.Modal)
            {
                CloseSelector();
            }
        }

        // ── IPointerExitHandler ────────────────────────────────────────────────

        public void OnPointerExit(PointerEventData ev)
        {
            // Finger moved off the trigger button itself — not off the selector.
            // Hold mode keeps tracking via OnDrag; no extra action needed here.
        }

        // ── Public API called from SelectorOverlayWidget ──────────────────────

        /// <summary>Called by the overlay when the user commits via modal-mode card tap.</summary>
        public void OnModalCommit()
        {
            CloseSelector();
        }

        /// <summary>Called by the overlay when the user cancels (outside tap in modal mode).</summary>
        public void OnModalCancel()
        {
            CloseSelector();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        void OpenSelector()
        {
            if (_selectorOverlay == null) return;
            _selectorOpen = true;

            _selectorOverlay.OpenFromRouter(this, _highlightScale);

            if (_fader != null) _fader.FadeAllExcept(_myCanvasGroup);
        }

        void CloseSelector()
        {
            _mode = Mode.Idle;
            if (_selectorOpen)
            {
                _selectorOpen = false;
                _selectorOverlay?.CloseFromRouter();
                _fader?.RestoreAll();
            }
        }
    }
}
