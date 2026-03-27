#nullable enable
using UnityEngine;

namespace Golfin.Inventory
{
    /// <summary>
    /// Marker component for an empty ball slot in the carousel.
    /// Not selectable — instantiated by BallCarouselController to pad the grid
    /// to a minimum of 5 cards when the player owns fewer than 5 ball types.
    /// </summary>
    public class BallThumbnailEmptyCard : MonoBehaviour
    {
        // No fields — visual is set entirely in the prefab.
        // Presence of this component signals "not selectable" to the carousel.
    }
}
