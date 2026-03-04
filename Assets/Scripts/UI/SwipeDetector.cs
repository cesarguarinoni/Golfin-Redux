using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace GolfinRedux.UI
{
    /// <summary>
    /// Detects swipe left/right gestures for carousel navigation.
    /// Attach to the UI element you want to detect swipes on (e.g., News Panel).
    /// </summary>
    public class SwipeDetector : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [Header("Swipe Settings")]
        [SerializeField] private float swipeThreshold = 50f; // Minimum distance for a swipe

        [Header("Events")]
        public UnityEvent onSwipeLeft;
        public UnityEvent onSwipeRight;

        private Vector2 _dragStartPos;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragStartPos = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Vector2 dragEndPos = eventData.position;
            float deltaX = dragEndPos.x - _dragStartPos.x;

            if (Mathf.Abs(deltaX) >= swipeThreshold)
            {
                if (deltaX > 0)
                {
                    // Swipe right
                    onSwipeRight?.Invoke();
                    Debug.Log("[SwipeDetector] Swipe Right");
                }
                else
                {
                    // Swipe left
                    onSwipeLeft?.Invoke();
                    Debug.Log("[SwipeDetector] Swipe Left");
                }
            }
        }
    }
}
