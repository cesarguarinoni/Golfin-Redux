using UnityEngine;

/// <summary>
/// Ensures correct initial active states at runtime.
/// Catches Inspector mistakes (objects left on/off during editing).
/// Script Execution Order: -300 (before everything).
/// </summary>
public class RuntimeActiveStateManager : MonoBehaviour
{
    [Header("Force Active at Runtime Start")]
    [SerializeField] private GameObject[] forceActive;

    [Header("Force Inactive at Runtime Start")]
    [SerializeField] private GameObject[] forceInactive;

    private void Awake()
    {
        if (forceActive != null)
            foreach (var go in forceActive)
                if (go != null) go.SetActive(true);

        if (forceInactive != null)
            foreach (var go in forceInactive)
                if (go != null) go.SetActive(false);
    }
}
