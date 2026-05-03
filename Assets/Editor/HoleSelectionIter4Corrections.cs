#if UNITY_EDITOR
// Iteration 4 — Cesar's 8 corrections
// Run: GOLFIN/Iter4/Apply All Corrections

using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using GolfinRedux.UI.HoleSelection;

public static class HoleSelectionIter4Corrections
{
    [MenuItem("GOLFIN/Iter4/Apply All Corrections")]
    public static void ApplyAllCorrections()
    {
        Debug.Log("[Iter4] ===== STARTING ITERATION 4 CORRECTIONS =====");
        ReimportSprites();          // Step 1
        FixMatchmakingModalBG();    // Correction 1
        FixHoleCardPrefab();        // Corrections 2, 6, 8 + chevron + lock icons
        FixHoleSelectionScene();    // Corrections 3, 5 (YAITA width + filter row wiring)
        Debug.Log("[Iter4] ===== ALL CORRECTIONS COMPLETE =====");
    }

    // ── Step 1: Re-import art assets as sprites ──────────────────────────────

    static void ReimportSprites()
    {
        string[] paths = new[]
        {
            "Assets/Art/HoleSelectScreen/Arrow.png",
            "Assets/Art/HoleSelectScreen/Background.png",
            "Assets/Art/HoleSelectScreen/Button - Play.png",
            "Assets/Art/HoleSelectScreen/Button - Replay.png",
            "Assets/Art/HoleSelectScreen/Lock.png",
        };

        foreach (var path in paths)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) { Debug.LogWarning($"[Iter4] No importer for {path}"); continue; }

            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite)      { imp.textureType = TextureImporterType.Sprite;              changed = true; }
            if (imp.spriteImportMode != SpriteImportMode.Single)     { imp.spriteImportMode = SpriteImportMode.Single;            changed = true; }
            if (!imp.alphaIsTransparency)                            { imp.alphaIsTransparency = true;                           changed = true; }
            if (imp.mipmapEnabled)                                   { imp.mipmapEnabled = false;                                changed = true; }
            if (imp.npotScale != TextureImporterNPOTScale.None)     { imp.npotScale = TextureImporterNPOTScale.None;            changed = true; }

            // 9-slice border for button sprites (allows them to scale without distortion at 20px corner radius)
            if (path.Contains("Button") && imp.spriteBorder == Vector4.zero)
            {
                imp.spriteBorder = new Vector4(40, 40, 40, 40);
                changed = true;
            }

            if (changed)
            {
                imp.SaveAndReimport();
                Debug.Log($"[Iter4] Re-imported as sprite: {path}");
            }
            else
            {
                Debug.Log($"[Iter4] Already sprite (no change): {path}");
            }
        }
        AssetDatabase.Refresh();
    }

    // ── Correction 1: MatchMakingModal BG → Background.png ────────────────

    static void FixMatchmakingModalBG()
    {
        const string PREFAB = "Assets/Prefabs/UI/Matchmaking/MatchMakingModal.prefab";
        const string BG_SPRITE = "Assets/Art/HoleSelectScreen/Background.png";

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        if (prefab == null) { Debug.LogError("[Iter4] MatchMakingModal.prefab not found"); return; }

        // Find BG child
        var bgT = FindChild(prefab.transform, "BG");
        if (bgT == null) { Debug.LogError("[Iter4] BG not found in MatchMakingModal"); return; }

        var img = bgT.GetComponent<Image>();
        if (img == null) { Debug.LogError("[Iter4] No Image on BG"); return; }

        var bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BG_SPRITE);
        if (bgSprite == null) { Debug.LogError("[Iter4] Background.png not found as Sprite — did reimport run?"); return; }

        using (var so = new SerializedObject(img))
        {
            so.FindProperty("m_Sprite").objectReferenceValue = bgSprite;
            so.FindProperty("m_Color").colorValue = Color.white;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log("[Iter4] Correction 1 DONE: BG sprite = Background.png, color = white.");
    }

    // ── Corrections 2/6/8 + chevron + lock: HoleCard.prefab ──────────────

    static void FixHoleCardPrefab()
    {
        const string PREFAB      = "Assets/Prefabs/UI/HoleSelection/HoleCard.prefab";
        const string PANEL_SPRITE = "Assets/Art/HomeScreen/Next Hole Panel.png";
        const string PLAY_SPRITE  = "Assets/Art/HoleSelectScreen/Button - Play.png";
        const string REPLAY_SPRITE= "Assets/Art/HoleSelectScreen/Button - Replay.png";
        const string ARROW_SPRITE = "Assets/Art/HoleSelectScreen/Arrow.png";
        const string LOCK_SPRITE  = "Assets/Art/HoleSelectScreen/Lock.png";

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        if (prefab == null) { Debug.LogError("[Iter4] HoleCard.prefab not found"); return; }

        // ── Corrections 2/6: Rounded corners on card root ───────────────
        var panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PANEL_SPRITE);
        if (panelSprite != null)
        {
            var rootImg = prefab.GetComponent<Image>();
            if (rootImg == null) rootImg = prefab.AddComponent<Image>();

            using (var so = new SerializedObject(rootImg))
            {
                so.FindProperty("m_Sprite").objectReferenceValue = panelSprite;
                so.FindProperty("m_Type").intValue   = 1;   // Sliced
                so.FindProperty("m_Color").colorValue = Color.white;
                so.ApplyModifiedProperties();
            }
            Debug.Log("[Iter4] Corrections 2/6 DONE: HoleCard root Image = Next Hole Panel (sliced).");
        }
        else Debug.LogWarning("[Iter4] Next Hole Panel.png not found — skipping rounded corners.");

        // ── Correction 8: playButtonSprite / replayButtonSprite on HoleCardController ──
        var ctrl = prefab.GetComponent<HoleCardController>();
        if (ctrl != null)
        {
            var playSprite   = AssetDatabase.LoadAssetAtPath<Sprite>(PLAY_SPRITE);
            var replaySprite = AssetDatabase.LoadAssetAtPath<Sprite>(REPLAY_SPRITE);

            if (playSprite != null && replaySprite != null)
            {
                using (var so = new SerializedObject(ctrl))
                {
                    var pPlay   = so.FindProperty("playButtonSprite");
                    var pReplay = so.FindProperty("replayButtonSprite");
                    if (pPlay   != null) pPlay.objectReferenceValue   = playSprite;
                    if (pReplay != null) pReplay.objectReferenceValue = replaySprite;
                    so.ApplyModifiedProperties();
                }
                Debug.Log("[Iter4] Correction 8 DONE: playButtonSprite + replayButtonSprite wired.");
            }
            else Debug.LogWarning("[Iter4] Button - Play/Replay.png not yet sprites — run after reimport.");
        }
        else Debug.LogWarning("[Iter4] HoleCardController not found on HoleCard root.");

        // Also set ActionButton Image type to Sliced
        var actionBtnT = FindChildDeep(prefab.transform, "ActionButton");
        if (actionBtnT != null)
        {
            var btnImg = actionBtnT.GetComponent<Image>();
            if (btnImg != null)
            {
                using (var so = new SerializedObject(btnImg))
                {
                    so.FindProperty("m_Type").intValue = 1; // Sliced
                    so.ApplyModifiedProperties();
                }
                Debug.Log("[Iter4] ActionButton Image.type = Sliced.");
            }
        }

        // ── Chevron Arrow.png wiring ──────────────────────────────────────
        var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ARROW_SPRITE);
        if (arrowSprite != null)
        {
            // ChevronCollapsed: rotation (0,0,0) → points right >
            var chevColl = FindChildDeep(prefab.transform, "ChevronCollapsed");
            if (chevColl != null)
            {
                SetChevronArrow(chevColl, arrowSprite, new Vector3(0, 0, 0));
                Debug.Log("[Iter4] ChevronCollapsed → Arrow.png rotation (0,0,0).");
            }
            else Debug.LogWarning("[Iter4] ChevronCollapsed not found.");

            // ChevronExpanded: rotation (0,0,-90) → points down ↓
            var chevExp = FindChildDeep(prefab.transform, "ChevronExpanded");
            if (chevExp != null)
            {
                SetChevronArrow(chevExp, arrowSprite, new Vector3(0, 0, -90));
                Debug.Log("[Iter4] ChevronExpanded → Arrow.png rotation (0,0,-90).");
            }
            else Debug.LogWarning("[Iter4] ChevronExpanded not found.");
        }
        else Debug.LogWarning("[Iter4] Arrow.png not found as Sprite — run after reimport.");

        // ── Lock.png wiring on LockedOverlay/LockIcon ──────────────────
        var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LOCK_SPRITE);
        if (lockSprite != null)
        {
            var lockedOverlay = FindChildDeep(prefab.transform, "LockedOverlay");
            if (lockedOverlay != null)
            {
                var lockIcon = FindChildDeep(lockedOverlay, "LockIcon");
                if (lockIcon != null)
                {
                    var lockImg = lockIcon.GetComponent<Image>();
                    if (lockImg != null)
                    {
                        using (var so = new SerializedObject(lockImg))
                        {
                            so.FindProperty("m_Sprite").objectReferenceValue = lockSprite;
                            so.ApplyModifiedProperties();
                        }
                        Debug.Log("[Iter4] LockIcon in LockedOverlay → Lock.png.");
                    }
                }
                else Debug.LogWarning("[Iter4] LockIcon child not found in LockedOverlay.");
            }
            else Debug.LogWarning("[Iter4] LockedOverlay not found in HoleCard.");
        }
        else Debug.LogWarning("[Iter4] Lock.png not found as Sprite — run after reimport.");

        // Also wire Lock.png to LockIconCollapsed / LockIconExpanded on the card root level
        if (lockSprite != null)
        {
            foreach (var name in new[] { "LockIconCollapsed", "LockIconExpanded" })
            {
                var lockIconT = FindChildDeep(prefab.transform, name);
                if (lockIconT != null)
                {
                    var lockImg = lockIconT.GetComponent<Image>();
                    if (lockImg == null) lockImg = lockIconT.gameObject.AddComponent<Image>();
                    using (var so = new SerializedObject(lockImg))
                    {
                        so.FindProperty("m_Sprite").objectReferenceValue = lockSprite;
                        so.ApplyModifiedProperties();
                    }
                    Debug.Log($"[Iter4] {name} → Lock.png.");
                }
            }
        }

        EditorUtility.SetDirty(prefab);
        PrefabUtility.SavePrefabAsset(prefab);
        Debug.Log("[Iter4] HoleCard.prefab saved.");
    }

    static void SetChevronArrow(Transform t, Sprite arrowSprite, Vector3 eulerAngles)
    {
        // Find the Image on this transform or its child "ChevronIcon"
        var img = t.GetComponent<Image>();
        if (img == null)
        {
            var chevIcon = FindChildDeep(t, "ChevronIcon");
            if (chevIcon != null) img = chevIcon.GetComponent<Image>();
        }

        if (img != null)
        {
            using (var so = new SerializedObject(img))
            {
                so.FindProperty("m_Sprite").objectReferenceValue = arrowSprite;
                so.ApplyModifiedProperties();
            }
        }

        t.localEulerAngles = eulerAngles;
    }

    // ── Corrections 3/5: ShellScene HoleSelectionScreen ──────────────────

    static void FixHoleSelectionScene()
    {
        const string SCENE = "Assets/Scenes/ShellScene.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
            SCENE, UnityEditor.SceneManagement.OpenSceneMode.Single);

        GameObject holeSelScreen = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var found = FindChildDeep(root.transform, "HoleSelectionScreen");
            if (found != null) { holeSelScreen = found.gameObject; break; }
        }

        if (holeSelScreen == null)
        {
            Debug.LogError("[Iter4] HoleSelectionScreen not found in ShellScene."); return;
        }

        var ctrl = holeSelScreen.GetComponent<HoleSelectionScreenController>();
        if (ctrl == null) { Debug.LogError("[Iter4] HoleSelectionScreenController not found."); return; }

        using (var so = new SerializedObject(ctrl))
        {
            // Find filter rows
            var filtersT = FindChildDeep(holeSelScreen.transform, "Filters");
            if (filtersT != null)
            {
                // Row 1 = first child (course filter)
                // Row 2 = second child (tee filter)
                Transform row1 = null, row2 = null;

                // Try common names first
                row1 = FindChildDeep(filtersT, "CourseFilterRow")
                    ?? FindChildDeep(filtersT, "FilterRow1")
                    ?? FindChildDeep(filtersT, "Row1");

                row2 = FindChildDeep(filtersT, "TeeFilterRow")
                    ?? FindChildDeep(filtersT, "FilterRow2")
                    ?? FindChildDeep(filtersT, "Row2");

                // Fallback: index-based
                if (row1 == null && filtersT.childCount >= 1) row1 = filtersT.GetChild(0);
                if (row2 == null && filtersT.childCount >= 2) row2 = filtersT.GetChild(1);

                if (row1 != null)
                {
                    var prop = so.FindProperty("courseFilterRow");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = row1.GetComponent<RectTransform>();
                        Debug.Log($"[Iter4] courseFilterRow → {row1.name}");
                    }

                    // Correction 3: Fix YAITA pill word-wrap + width
                    foreach (Transform child in row1)
                    {
                        var tmp = child.GetComponentInChildren<TextMeshProUGUI>();
                        if (tmp != null) tmp.enableWordWrapping = false;

                        if (child.name.Contains("YAITA") || child.name.Contains("Yaita") || child.name.Contains("yaita"))
                        {
                            var le = child.GetComponent<LayoutElement>();
                            if (le == null) le = child.gameObject.AddComponent<LayoutElement>();
                            if (le.minWidth < 380f)
                            {
                                le.minWidth = 380f;
                                le.preferredWidth = 400f;
                                Debug.Log($"[Iter4] Correction 3: {child.name} minWidth=380, preferredWidth=400.");
                            }
                        }
                    }
                }
                else Debug.LogWarning("[Iter4] Filters row1 not found.");

                if (row2 != null)
                {
                    var prop = so.FindProperty("teeFilterRow");
                    if (prop != null)
                    {
                        prop.objectReferenceValue = row2.GetComponent<RectTransform>();
                        Debug.Log($"[Iter4] teeFilterRow → {row2.name}");
                    }
                }
                else Debug.LogWarning("[Iter4] Filters row2 not found.");
            }
            else Debug.LogWarning("[Iter4] Filters GameObject not found.");

            so.ApplyModifiedProperties();
        }

        // Fix LockIcon sprites in filter pills (replace S_Mission_IconLock with Lock.png)
        var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/HoleSelectScreen/Lock.png");
        if (lockSprite != null)
        {
            FixLockIconSpritesInScene(holeSelScreen, lockSprite);
        }
        else Debug.LogWarning("[Iter4] Lock.png not yet a sprite — LockIcon sprites not updated in scene.");

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("[Iter4] ShellScene saved.");
    }

    static void FixLockIconSpritesInScene(GameObject root, Sprite lockSprite)
    {
        // Find all LockIcon GameObjects in the HoleSelectionScreen and update their Image sprites
        var allImages = root.GetComponentsInChildren<Image>(includeInactive: true);
        int count = 0;
        foreach (var img in allImages)
        {
            if (img.gameObject.name == "LockIcon" && img.sprite != lockSprite)
            {
                using (var so = new SerializedObject(img))
                {
                    so.FindProperty("m_Sprite").objectReferenceValue = lockSprite;
                    so.ApplyModifiedProperties();
                }
                count++;
            }
        }
        Debug.Log($"[Iter4] Updated {count} LockIcon sprites in HoleSelectionScreen.");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    static Transform FindChild(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
            if (t.GetChild(i).name == name) return t.GetChild(i);
        return null;
    }

    static Transform FindChildDeep(Transform t, string name)
    {
        if (t == null) return null;
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindChildDeep(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
#endif
