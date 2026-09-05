using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// Tap the scrim, the modal closes.
    ///
    /// <para>Deliberately NOT a <c>Button</c>. A scrim is not a control: giving it a Button would
    /// drag in the project's "every new player-facing Button gets
    /// <c>Golfin.UI.Polish.ButtonPressFeedback</c>" rule (CLAUDE.md hard rule 11), and a scrim that
    /// scales to 0.95 on press is a full-screen dim visibly shrinking away from the screen edges.
    /// An <see cref="IPointerClickHandler"/> gets the dismissal with none of that.</para>
    ///
    /// <para><see cref="ModalScrim"/> already guarantees the scrim is <c>raycastTarget</c> and
    /// covers the whole root canvas, so this receives the click wherever outside the panel the
    /// player taps.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ModalBackdropDismiss : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("The modal to hide. Left empty, the nearest ModalController above this object " +
                 "in the hierarchy is used — which is the normal authoring, since a scrim is a " +
                 "child of its modal root.")]
        [SerializeField] private ModalController modal;

        public void OnPointerClick(PointerEventData eventData)
        {
            var target = modal != null ? modal : GetComponentInParent<ModalController>();
            if (target == null || !target.IsVisible()) return;
            target.Hide();
        }
    }
}
