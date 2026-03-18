using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class RaycastDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            Debug.Log($"=== Click at {Input.mousePosition} hit {results.Count} objects ===");
            foreach (var r in results)
            {
                Debug.Log($"  -> {r.gameObject.name} (depth: {r.depth}, sortOrder: {r.sortingOrder})");
            }
        }
    }
}
