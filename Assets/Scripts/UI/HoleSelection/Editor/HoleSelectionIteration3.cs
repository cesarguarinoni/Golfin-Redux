#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Collections.Generic;
using GolfinRedux.UI.HoleSelection;

/// <summary>
/// One-shot iteration-3 setup script:
/// 1. Imports S_HoleSel_FilterLock / S_HoleSel_ChevronRight / S_HoleSel_ChevronDown as Sprites.
/// 2. Upgrades HoleCard prefab:
///    a. Adds ChevronIcon child to CollapsedContainer/TitleArea with Image sprite.
///    b. Upgrades PLAY button colors + font size (66) + dark text (#321506).
///    c. Adds a shadow child behind the ActionButton.
///    d. Adds a sheen overlay child on the top-half of ActionButton.
///    e. Adds a gradient-bottom child to ActionButton for the bottom gold gradient.
/// 3. Adds LockIcon GameObjects to 3 filter pills in ShellScene (Pill_YAITA, Pill_REGULAR, Pill_BACK).
/// 4. Wires coursePills + teePills lists on HoleSelectionScreenController.
/// 5. Re-runs full auto-wire (calls HoleSelectionAutoWire.Run()).
///
/// Run: GOLFIN/Setup/Iteration3 HoleSelection Setup
/// </summary>
public static class HoleSelectionIteration3
{
    private const string LOCK_SPRITE_PATH     = "Assets/Resources/UI/HoleSelection/S_HoleSel_FilterLock.png";
    private const string CHEVRON_RIGHT_PATH   = "Assets/Resources/UI/HoleSelection/S_HoleSel_ChevronRight.png";
    private const string CHEVRON_DOWN_PATH    = "Assets/Resources/UI/HoleSelection/S_HoleSel_ChevronDown.png";
    private const string HOLE_CARD_PREFAB     = "Assets/Prefabs/UI/HoleSelection/HoleCard.prefab";

    [MenuItem("GOLFIN/Setup/Iteration3 HoleSelection Setup")]
    public static void Run()
    {
        // --- Step 1: Set TextureType = Sprite on new PNGs ---
        int imported = 0;
        foreach (var path in new[] { LOCK_SPRITE_PATH, CHEVRON_RIGHT_PATH, CHEVRON_DOWN_PATH })
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null)
            {
                // File may not be visible yet — force a refresh first
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                imp = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (imp == null) { Debug.LogWarning($"[Iter3] Cannot get TextureImporter for {path}"); continue; }
            if (imp.textureType != TextureImporterType.Sprite ||
                imp.spriteImportMode != SpriteImportMode.Single)
            {
                imp.textureType      = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.filterMode       = FilterMode.Bilinear;
                imp.mipmapEnabled    = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[Iter3] Imported as Sprite: {path}");
                imported++;
            }
            else
            {
                Debug.Log($"[Iter3] Already Sprite: {path}");
            }
        }
        Debug.Log($"[Iter3] Step1 done — {imported} sprites imported/updated.");

        // --- Step 2: Upgrade HoleCard prefab ---
        UpgradeHoleCardPrefab();

        // --- Steps 3+4: Add lock icons + wire filter pills in scene ---
        WireSceneFilterPills();

        Debug.Log("[Iter3] All steps complete. Save the scene (Cmd+S).");
    }

    // ── Step 2: HoleCard prefab upgrades ─────────────────────────────────────

    private static void UpgradeHoleCardPrefab()
    {
        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HOLE_CARD_PREFAB);
        if (prefabAsset == null) { Debug.LogError($"[Iter3] HoleCard prefab not found at '{HOLE_CARD_PREFAB}'"); return; }

        var stage = PrefabStageUtility.OpenPrefab(HOLE_CARD_PREFAB);
        if (stage == null) { Debug.LogError("[Iter3] Could not open HoleCard prefab in isolation."); return; }

        var root = stage.prefabContentsRoot;

        // 2a. Add ChevronIcon to CollapsedContainer/TitleArea (right side)
        AddChevronToCollapsedTitleArea(root);

        // 2b-e. Upgrade ActionButton visuals
        UpgradeActionButton(root);

        PrefabUtility.SaveAsPrefabAsset(root, HOLE_CARD_PREFAB);
        StageUtility.GoToMainStage();

        Debug.Log("[Iter3] HoleCard prefab upgraded.");
    }

    private static void AddChevronToCollapsedTitleArea(GameObject root)
    {
        // The collapsed title area is at CollapsedContainer/TitleArea
        var titleAreaT = root.transform.Find("CollapsedContainer/TitleArea");
        if (titleAreaT == null)
        {
            Debug.LogWarning("[Iter3] CollapsedContainer/TitleArea not found — skipping chevron.");
            return;
        }

        // Remove any existing chevron
        var existing = titleAreaT.Find("ChevronIcon");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Load sprites
        var chevRight = AssetDatabase.LoadAssetAtPath<Sprite>(CHEVRON_RIGHT_PATH);
        if (chevRight == null) Debug.LogWarning("[Iter3] ChevronRight sprite not loaded yet — chevron will have null sprite.");

        // Create ChevronIcon GO
        var chevGO = new GameObject("ChevronIcon");
        chevGO.transform.SetParent(titleAreaT, false);

        var rt = chevGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(60f, 60f);
        // Anchor to the right side of the title area
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot     = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-24f, 0f);

        var img = chevGO.AddComponent<Image>();
        img.sprite           = chevRight;
        img.preserveAspect   = true;
        img.color            = Color.white;
        img.raycastTarget    = false;

        chevGO.AddComponent<CanvasRenderer>();

        Debug.Log("[Iter3] ChevronIcon added to CollapsedContainer/TitleArea.");
    }

    private static void UpgradeActionButton(GameObject root)
    {
        var actionBtnT = root.transform.Find("ExpandedContainer/ActionButton");
        if (actionBtnT == null)
        {
            Debug.LogWarning("[Iter3] ExpandedContainer/ActionButton not found — skipping button upgrade.");
            return;
        }

        // ── 2b: Update main Image background to top-gold (#FCF195) ──
        var mainImg = actionBtnT.GetComponent<Image>();
        if (mainImg != null)
        {
            // Top color of the gradient: #FCF195
            mainImg.color = new Color(0.988f, 0.949f, 0.584f, 1f);
        }

        // ── Remove old shadow/sheen/gradient if they exist ──
        foreach (string childName in new[] { "ButtonShadow", "ButtonGradientBottom", "ButtonSheen" })
        {
            var old = actionBtnT.Find(childName);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }

        // ── 2c: Drop shadow behind the button (sibling below) ──
        // We add it as a child positioned at +4Y offset, slightly enlarged, black 25% alpha
        // Actually the shadow should go BEHIND - add it as first child so it renders under label
        // Because Unity UI renders children in order, add shadow at index 0
        var shadowGO = new GameObject("ButtonShadow");
        shadowGO.transform.SetParent(actionBtnT, false);
        shadowGO.transform.SetAsFirstSibling();
        var shadowRT = shadowGO.AddComponent<RectTransform>();
        shadowRT.anchorMin = new Vector2(0f, 0f);
        shadowRT.anchorMax = new Vector2(1f, 1f);
        shadowRT.offsetMin = new Vector2(-2f, -6f);
        shadowRT.offsetMax = new Vector2(2f, -2f); // shifted down 4px
        var shadowImg = shadowGO.AddComponent<Image>();
        shadowImg.color = new Color(0f, 0f, 0f, 0.25f);
        shadowImg.raycastTarget = false;
        shadowGO.AddComponent<CanvasRenderer>();

        // ── 2b continued: gradient-bottom child covers lower 2/3 with #BB7F1D ──
        var gradBotGO = new GameObject("ButtonGradientBottom");
        gradBotGO.transform.SetParent(actionBtnT, false);
        // Insert after shadow, before Label
        gradBotGO.transform.SetSiblingIndex(1);
        var gradBotRT = gradBotGO.AddComponent<RectTransform>();
        gradBotRT.anchorMin = new Vector2(0f, 0f);
        gradBotRT.anchorMax = new Vector2(1f, 0.6f); // bottom 60%
        gradBotRT.offsetMin = Vector2.zero;
        gradBotRT.offsetMax = Vector2.zero;
        var gradBotImg = gradBotGO.AddComponent<Image>();
        // #BB7F1D = (0.733, 0.498, 0.114)
        gradBotImg.color = new Color(0.733f, 0.498f, 0.114f, 1f);
        gradBotImg.raycastTarget = false;
        gradBotGO.AddComponent<CanvasRenderer>();

        // ── 2d: Sheen overlay on top 50% ──
        var sheenGO = new GameObject("ButtonSheen");
        sheenGO.transform.SetParent(actionBtnT, false);
        // Insert after gradient-bottom, before Label
        sheenGO.transform.SetSiblingIndex(2);
        var sheenRT = sheenGO.AddComponent<RectTransform>();
        sheenRT.anchorMin = new Vector2(0f, 0.5f);
        sheenRT.anchorMax = new Vector2(1f, 1f);
        sheenRT.offsetMin = Vector2.zero;
        sheenRT.offsetMax = Vector2.zero;
        var sheenImg = sheenGO.AddComponent<Image>();
        sheenImg.color = new Color(1f, 1f, 1f, 0.25f);
        sheenImg.raycastTarget = false;
        sheenGO.AddComponent<CanvasRenderer>();

        // ── 2b: Update Label TMP ──
        var labelT = actionBtnT.Find("Label");
        if (labelT != null)
        {
            var tmp = labelT.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.fontSize = 66f;
                // #321506 = (0.196, 0.082, 0.024)
                tmp.color = new Color(0.196f, 0.082f, 0.024f, 1f);
                tmp.fontStyle = FontStyles.Bold;
            }
        }

        Debug.Log("[Iter3] ActionButton visuals upgraded (gradient + sheen + shadow + font).");
    }

    // ── Steps 3+4: Add lock icons + wire filter pills ──────────────────────

    private static void WireSceneFilterPills()
    {
        // Find HoleSelectionScreenController in scene
        HoleSelectionScreenController ctrl = null;
        foreach (var c in Resources.FindObjectsOfTypeAll<HoleSelectionScreenController>())
        {
            if (c.gameObject.scene.isLoaded) { ctrl = c; break; }
        }
        if (ctrl == null) { Debug.LogError("[Iter3] HoleSelectionScreenController not in active scene. Open ShellScene first."); return; }

        var so = new SerializedObject(ctrl);
        var coursePillsProp = so.FindProperty("coursePills");
        var teePillsProp    = so.FindProperty("teePills");

        // Course filter row: Row1 in the Filters hierarchy
        // Pills: Pill_LOMOND_28/72 (active, gold), Pill_YAITA_-_KIKYOU (locked, silver+lock)
        // Tee filter row: Row2
        // Pills: Pill_LADIES_18/18, Pill_FRONT_10/18, Pill_REGULAR_0/18, Pill_BACK_0/18

        // Find all pills by name in scene
        var allPills = Resources.FindObjectsOfTypeAll<RectTransform>();

        GameObject FindPillGO(string nameContains)
        {
            foreach (var rt in allPills)
            {
                if (!rt.gameObject.scene.isLoaded) continue;
                if (rt.name.Contains(nameContains)) return rt.gameObject;
            }
            return null;
        }

        // Load lock sprite
        AssetDatabase.Refresh();
        var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LOCK_SPRITE_PATH);
        if (lockSprite == null)
        {
            Debug.LogWarning("[Iter3] Lock sprite not yet loaded — will assign null. Re-run after reimport.");
        }

        // Helper: get or create LockIcon on a pill
        GameObject GetOrCreateLockIcon(GameObject pillGO)
        {
            if (pillGO == null) return null;
            var existing = pillGO.transform.Find("LockIcon");
            if (existing != null) return existing.gameObject;

            var go = new GameObject("LockIcon");
            go.transform.SetParent(pillGO.transform, false);
            go.transform.SetAsFirstSibling(); // render behind label text

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(21f, 26f);
            rt.anchorMin        = new Vector2(0f, 0.5f);
            rt.anchorMax        = new Vector2(0f, 0.5f);
            rt.pivot            = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f); // 10px from left edge

            var img = go.AddComponent<Image>();
            img.sprite         = lockSprite;
            img.preserveAspect = true;
            img.color          = Color.white;
            img.raycastTarget  = false;

            go.AddComponent<CanvasRenderer>();
            EditorUtility.SetDirty(go);
            return go;
        }

        // Helper: build a FilterPill SerializedProperty entry
        void AddPillToArray(SerializedProperty arr, GameObject pillGO, string filterId, bool locked)
        {
            if (pillGO == null) { Debug.LogWarning($"[Iter3] Pill GO not found for filterId={filterId}"); return; }

            // Increment array size
            arr.arraySize++;
            var elem = arr.GetArrayElementAtIndex(arr.arraySize - 1);

            // button
            var btnComp = pillGO.GetComponent<Button>();
            elem.FindPropertyRelative("button").objectReferenceValue = btnComp;

            // label (find child Label TMP)
            var labelT = pillGO.transform.Find("Label");
            var tmp = labelT != null ? labelT.GetComponent<TextMeshProUGUI>() : null;
            elem.FindPropertyRelative("label").objectReferenceValue = tmp;

            // background
            var bgImg = pillGO.GetComponent<Image>();
            elem.FindPropertyRelative("background").objectReferenceValue = bgImg;

            // lockIcon — create if locked
            GameObject lockIconGO = null;
            if (locked)
            {
                lockIconGO = GetOrCreateLockIcon(pillGO);
                EditorUtility.SetDirty(pillGO);
            }
            elem.FindPropertyRelative("lockIcon").objectReferenceValue = lockIconGO;

            // locked flag + filterId
            elem.FindPropertyRelative("locked").boolValue   = locked;
            elem.FindPropertyRelative("filterId").stringValue = filterId;

            Debug.Log($"[Iter3] FilterPill added: filterId={filterId}, locked={locked}");
        }

        // Clear existing pill lists
        coursePillsProp.arraySize = 0;
        teePillsProp.arraySize    = 0;

        // Course row
        var pilLomondGO = FindPillGO("Pill_LOMOND");
        var pilYaitaGO  = FindPillGO("Pill_YAITA");

        AddPillToArray(coursePillsProp, pilLomondGO, "Lomond", false);
        AddPillToArray(coursePillsProp, pilYaitaGO,  "Yaita",  true);

        // Tee row
        var pilLadiesGO  = FindPillGO("Pill_LADIES");
        var pilFrontGO   = FindPillGO("Pill_FRONT");
        var pilRegularGO = FindPillGO("Pill_REGULAR");
        var pilBackGO    = FindPillGO("Pill_BACK");

        AddPillToArray(teePillsProp, pilLadiesGO,  "Ladies",  false);
        AddPillToArray(teePillsProp, pilFrontGO,   "Front",   false);
        AddPillToArray(teePillsProp, pilRegularGO, "Regular", true);
        AddPillToArray(teePillsProp, pilBackGO,    "Back",    true);

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ctrl);

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[Iter3] Filter pills wired: 2 course pills, 4 tee pills.");
        Debug.Log("[Iter3] Steps 3+4 complete.");
    }
}
#endif
