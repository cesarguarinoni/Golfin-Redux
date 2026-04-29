using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Full-screen transparent Image that catches outside-taps and fires a callback.
    /// Must live in its own file so Unity can serialize it as a MonoBehaviour component.
    /// </summary>
    public class OutsideClickCatcher : MonoBehaviour, IPointerClickHandler
    {
        public System.Action OnOutsideClick;
        public void OnPointerClick(PointerEventData _) => OnOutsideClick?.Invoke();
    }
}
