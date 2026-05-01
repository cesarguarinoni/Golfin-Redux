using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One-shot setup: creates PutterTimingSlab under PutterTrack and wires it to ShotConeView.
/// Run from GOLFIN > Setup > Create Putter Timing Slab.
/// Safe to re-run — skips creation if GO already exists.
/// </summary>
public static class PutterTimingSlabSetup
{
    [MenuItem("GOLFIN/Setup/Create Putter Timing Slab")]
    static void CreatePutterTimingSlab()
    {
        var putterTrackGO = GameObject.Find("PutterTrack");
        if (putterTrackGO == null)
        {
            Debug.LogError("[PutterTimingSlabSetup] PutterTrack GO not found in scene. Open LabScaffold.unity first.");
            return;
        }

        // Skip if already created.
        var existing = putterTrackGO.transform.Find("PutterTimingSlab");
        if (existing != null)
        {
            Debug.Log("[PutterTimingSlabSetup] PutterTimingSlab already exists — skipping creation, re-wiring only.");
            WireToShotConeView(existing.gameObject);
            return;
        }

        // Create child GO.
        var slabGO = new GameObject("PutterTimingSlab");
        slabGO.transform.SetParent(putterTrackGO.transform, false);

        // Set up RectTransform: anchor top-center, pivot center, 140×60.
        var rt = slabGO.AddComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 1f);
        rt.anchorMax        = new Vector2(0.5f, 1f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(140f, 60f);
        rt.anchoredPosition = Vector2.zero; // starts at top of track

        // Add white Image (ShotConeView will tint it via SlabColorFromProgress).
        var img = slabGO.AddComponent<Image>();
        img.color = Color.white;

        // Deactivate — ShotConeView activates it during Timing state only.
        slabGO.SetActive(false);

        WireToShotConeView(slabGO);

        Undo.RegisterCreatedObjectUndo(slabGO, "Create PutterTimingSlab");
        EditorUtility.SetDirty(putterTrackGO.transform.root.gameObject);
        Debug.Log("[PutterTimingSlabSetup] PutterTimingSlab created and wired successfully.");
    }

    static void WireToShotConeView(GameObject slabGO)
    {
        var coneView = Object.FindObjectOfType<Golfin.Gameplay.UI.ShotUI.ShotConeView>();
        if (coneView == null)
        {
            Debug.LogWarning("[PutterTimingSlabSetup] ShotConeView not found — wire _putterTimingSlabRT manually.");
            return;
        }

        var so      = new SerializedObject(coneView);
        var prop    = so.FindProperty("_putterTimingSlabRT");
        if (prop == null)
        {
            Debug.LogWarning("[PutterTimingSlabSetup] _putterTimingSlabRT field not found on ShotConeView. Has the script compiled?");
            return;
        }

        prop.objectReferenceValue = slabGO.GetComponent<RectTransform>();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(coneView);
        Debug.Log("[PutterTimingSlabSetup] ShotConeView._putterTimingSlabRT wired.");
    }
}
