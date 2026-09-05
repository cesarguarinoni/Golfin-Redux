using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.Controls.Needle
{
    /// <summary>
    /// The invisible rect that catches the second touch (scheme_needle §3.2). Alpha 0, raycast
    /// target on, enabled only while the needle is sweeping.
    ///
    /// <para>WHY A SEPARATE OBJECT AND NOT THE HANDLE. The tap is "anywhere on the shot area", not
    /// "on the club head" — the player has just let go of the club and their thumb is wherever the
    /// release left it, which on a 120% pull is 456px below where the club now is. Asking them to
    /// find a 178px sprite again in the ~1s the needle takes would make the scheme about
    /// reacquiring the handle rather than about timing.</para>
    ///
    /// <para>ITS RECT IS THE NODE'S <c>Shoot Controls</c> FRAME, not the whole canvas: 1074×1396,
    /// centred 263px above the ball rest. That deliberately stops short of the Spin / Fade-Draw /
    /// club-select buttons below it and the HUD above it, so a catcher that somehow outlived its
    /// phase could not swallow those taps. It is <c>SetActive(false)</c> between swings anyway —
    /// the raycast target of a disabled object is not in the raycast at all, which is a stronger
    /// guarantee than a zero alpha.</para>
    ///
    /// <para>It forwards rather than deciding: <see cref="NeedleSchemeDriver"/> owns the phase,
    /// and a catcher that knew about grades would be a second place the state machine lives.</para>
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class NeedleTapCatcher : MonoBehaviour, IPointerDownHandler
    {
        [Tooltip("The invisible raycast target. Alpha 0 — never a visible fill.")]
        [SerializeField] private Image _raycastTarget;

        /// <summary>Raised on pointer-down anywhere in the rect. Wired by the driver.</summary>
        public event Action OnTapped;

        /// <summary>Turn the catcher on for the needle phase and off again at commit. Toggles the
        /// GameObject, so an inactive catcher is genuinely outside the raycast.</summary>
        public void SetArmed(bool armed)
        {
            if (gameObject.activeSelf != armed) gameObject.SetActive(armed);
            if (_raycastTarget != null) _raycastTarget.raycastTarget = armed;
        }

        /// <summary>True while the catcher is listening. Read back by the tests and the acceptance
        /// run instead of inferring it from a screenshot.</summary>
        public bool IsArmed => gameObject.activeSelf;

        public void OnPointerDown(PointerEventData eventData) => OnTapped?.Invoke();

        /// <summary>EditMode wiring seam — a plain MonoBehaviour gets no Awake in EditMode.</summary>
        public void ConfigureForTests(Image raycastTarget) => _raycastTarget = raycastTarget;
    }
}
