using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Part A — Translucency swap.
    /// Mirrors the ConeRoot CanvasGroup alpha onto the central ball Image, and keeps the
    /// ClubHandle opted OUT of that alpha (<c>ignoreParentGroups = true</c>) so the handle stays
    /// fully opaque while the cone sits at ConeIdleAlpha — the player must be able to see the
    /// thing they grab. Never WRITES the ConeRoot CanvasGroup — that stays ConeAlphaController's job.
    ///
    /// This component does NOT drive the handle's own alpha, and must not be made to
    /// (Cesar, 2026-08-10). It lives on CentralBall, and CentralBallWidget — same GameObject —
    /// SetActive(false)s itself on <see cref="Golfin.Gameplay.Input.ShotState.Resolving"/>, so
    /// LateUpdate here does not run during a shot at all. Hiding the handle for the duration of
    /// the shot is owned by ShotInProgressUiGate (ClubHandle is in its _hideGroupsDuringShot
    /// list), which lives on ShotUI_Canvas and therefore stays active while the ball is away.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class BallConeAlphaMirror : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _coneGroup;

        [Tooltip("Base alpha multiplier — set to 1 to match the cone exactly.")]
        [SerializeField] private float _baseAlpha = 1f;

        [Tooltip("When true: stop driving ball alpha (restores authored value). " +
                 "Also hooks into ClubHandle CanvasGroup to re-enable ignoreParentGroups=false " +
                 "so the handle goes translucent again. Useful to compare old vs new look.")]
        [SerializeField] private bool debugLegacyTranslucency = false;

        private Image _image;

        // Reference to the ClubHandle CanvasGroup for debug-mode reversal
        [SerializeField] private CanvasGroup _handleCanvasGroup;

        void Awake()
        {
            _image = GetComponent<Image>();
        }

        void LateUpdate()
        {
            if (debugLegacyTranslucency)
            {
                // Restore legacy look: handle re-inherits parent alpha; ball stays authored
                if (_handleCanvasGroup != null)
                    _handleCanvasGroup.ignoreParentGroups = false;
                return;
            }

            // Normal (new) behaviour: handle ignores parent (done at Awake by CanvasGroup on ClubHandle).
            // Opt-out only — the handle's alpha is never written here; ShotInProgressUiGate owns
            // hiding it during the shot.
            if (_handleCanvasGroup != null)
                _handleCanvasGroup.ignoreParentGroups = true;

            if (_image == null || _coneGroup == null) return;
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, _coneGroup.alpha * _baseAlpha);
        }
    }
}
