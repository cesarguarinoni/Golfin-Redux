using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================
//  LESSON S (tasks/lessons.md): every player-facing Button gets this.
//  When adding a new Button to any production prefab or scene, attach
//  Golfin.UI.Polish.ButtonPressFeedback as a sibling component. Treat
//  Button + ButtonPressFeedback as a pair.
// =====================================================================

namespace Golfin.UI.Polish
{
    /// <summary>
    /// Universal tactile-feedback component. Drop on any GameObject with a Button.
    /// On pointer-down the host RectTransform scales from 1.0 → 0.95 over half the
    /// duration, then back to 1.0 over the other half. No DOTween, no animation
    /// clip — a single coroutine using unscaled time (so the pulse still plays if
    /// Time.timeScale is paused, e.g. during a modal fade-in).
    ///
    /// Wiring:
    ///   - Add the component next to a Button. If the Button is missing or
    ///     disabled, the pulse is suppressed (no exception).
    ///   - The component restores localScale to its initial value on disable,
    ///     so a click that interrupts the pulse never leaves the button
    ///     scaled.
    ///
    /// Performance: zero GC after the first press (the coroutine reuses its
    /// own enumerator state machine; no per-frame allocations).
    ///
    /// Stage F — Loop v2 polish. (Cesar 2026-05-22.)
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class ButtonPressFeedback : MonoBehaviour, IPointerDownHandler
    {
        // ── Tunables (kept conservative; mobile-feel default) ──────────────────

        [Tooltip("Scale factor at the deepest point of the press pulse. 0.95 = 5% shrink.")]
        [SerializeField, Range(0.5f, 1.0f)] float _pressedScale = 0.95f;

        [Tooltip("Total pulse duration in seconds (down + up combined). 0.12 = snappy mobile feel.")]
        [SerializeField, Range(0.04f, 0.5f)] float _duration = 0.12f;

        // ── Internal ──────────────────────────────────────────────────────────

        RectTransform _rt;
        Button        _button;        // optional — pulse still fires if absent
        Vector3       _initialScale;
        Coroutine     _running;

        // ── Lifecycle ────────────────────────────────────────────────────────

        void Awake()
        {
            _rt           = (RectTransform)transform;
            _button       = GetComponent<Button>();
            _initialScale = _rt.localScale;
        }

        void OnDisable()
        {
            // If the press is interrupted (button hidden mid-pulse), restore
            // the original scale so the button never looks "stuck small".
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
            if (_rt != null) _rt.localScale = _initialScale;
        }

        // ── Pointer event ─────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData eventData)
        {
            // Respect the Button's disabled state: a non-interactable button
            // should not emit tactile feedback (it can't be clicked anyway).
            if (_button != null && !_button.interactable) return;

            // Re-entrant press: cancel the running pulse and start a fresh one
            // so a fast double-tap still feels responsive.
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(PulseCoroutine());
        }

        // ── Coroutine ────────────────────────────────────────────────────────

        IEnumerator PulseCoroutine()
        {
            float half = _duration * 0.5f;

            // Down phase: initial → pressed.
            float t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / half);
                _rt.localScale = Vector3.LerpUnclamped(_initialScale, _initialScale * _pressedScale, u);
                yield return null;
            }

            // Up phase: pressed → initial.
            t = 0f;
            while (t < half)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / half);
                _rt.localScale = Vector3.LerpUnclamped(_initialScale * _pressedScale, _initialScale, u);
                yield return null;
            }

            _rt.localScale = _initialScale;
            _running       = null;
        }
    }
}
