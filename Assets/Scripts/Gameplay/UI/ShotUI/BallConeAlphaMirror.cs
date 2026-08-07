using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Part A — Translucency swap.
    /// Mirrors the ConeRoot CanvasGroup alpha onto the ball Image each LateUpdate.
    /// Never WRITES the ConeRoot CanvasGroup — that stays ConeAlphaController's job.
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

            // Normal (new) behaviour: handle ignores parent (done at Awake by CanvasGroup on ClubHandle)
            if (_handleCanvasGroup != null)
                _handleCanvasGroup.ignoreParentGroups = true;

            if (_image == null || _coneGroup == null) return;
            var c = _image.color;
            _image.color = new Color(c.r, c.g, c.b, _coneGroup.alpha * _baseAlpha);
        }
    }
}
