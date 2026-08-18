using UnityEngine;
using UnityEngine.EventSystems;

namespace Golfin.UI
{
    /// <summary>
    /// Stops pointer clicks raised inside an accordion submenu from bubbling up to the row's
    /// <see cref="UnityEngine.UI.Button"/> and collapsing the section the player is using.
    ///
    /// The accordion row carries the toggle Button, and each submenu lives INSIDE that row, so
    /// uGUI's handler walk (<c>ExecuteEvents.GetEventHandler</c>) climbs straight past any child
    /// that does not itself handle clicks and lands on the row. A <see cref="UnityEngine.UI.Slider"/>
    /// implements drag and pointer-down but NOT <see cref="IPointerClickHandler"/>, so releasing a
    /// volume drag toggled Sound Settings shut. The same applied to every non-interactive graphic in
    /// a submenu — tapping the About licence text collapsed About.
    ///
    /// Attaching this to a submenu container makes it the nearest click handler, so the walk stops
    /// there and does nothing. Children that DO handle clicks (language rows, the CHANGE button) are
    /// unaffected: the walk finds them first.
    /// </summary>
    public class SubmenuClickBoundary : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>Absorb the click. Intentionally empty — the point is that it goes no further.</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
        }
    }
}
