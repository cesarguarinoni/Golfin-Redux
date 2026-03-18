using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class RaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = mousePos
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            Debug.Log($"=== Click at {mousePos} hit {results.Count} objects ===");
            foreach (var r in results)
            {
                Debug.Log($"  -> {r.gameObject.name} (depth: {r.depth}, sortOrder: {r.sortingOrder})");
            }
        }
    }
}
