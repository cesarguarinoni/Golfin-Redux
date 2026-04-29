using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Builds the WindIndicator and HoleIndicator hierarchies in LabScaffold.unity
/// under ShotUI_Canvas, per spec 8_4_indicators Layer G.
/// Menu: GOLFIN/Build/Build Indicator Widgets (8.4)
/// </summary>
public static class IndicatorWidgetBuilder
{
    [MenuItem("GOLFIN/Build/Build Indicator Widgets (8.4)")]
    public static void BuildIndicatorWidgets()
    {
        // Ensure Unity has imported all assets before loading them
        AssetDatabase.Refresh();

        // Load sprites
        Sprite backplateSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Indicator - Wind-Hole.png");
        Sprite flagSprite      = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Flag.png");
        Sprite trailSprite     = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Indicator - Trail.png");
        TMP_FontAsset rubikFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Rubik-SemiBold SDF.asset");

        // Load wind arrow sprite from PNG (architect placed Icon - Wind Arrow.png, 128x120 white chevron)
        Sprite windArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Wind Arrow.png");
        if (windArrowSprite == null)
        {
            // PNG may need import settings update — ensure it imports as Sprite
            var importer = AssetImporter.GetAtPath("Assets/Art/In-Game UI/Icon - Wind Arrow.png") as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                AssetDatabase.Refresh();
                windArrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/In-Game UI/Icon - Wind Arrow.png");
                Debug.Log("[IndicatorWidgetBuilder] Set PNG importer to Sprite and reimported Icon - Wind Arrow.png");
            }
        }

        if (backplateSprite == null) Debug.LogWarning("[IndicatorWidgetBuilder] Could not load Indicator - Wind-Hole.png as Sprite");
        if (flagSprite      == null) Debug.LogWarning("[IndicatorWidgetBuilder] Could not load Icon - Flag.png as Sprite");
        if (trailSprite     == null) Debug.LogWarning("[IndicatorWidgetBuilder] Could not load Indicator - Trail.png as Sprite");
        if (windArrowSprite == null) Debug.LogWarning("[IndicatorWidgetBuilder] Could not load Icon - Wind Arrow.png as Sprite");
        if (rubikFont       == null) Debug.LogWarning("[IndicatorWidgetBuilder] Could not load Rubik-VariableFont_wght SDF.asset");

        // Find ShotUI_Canvas in the active scene
        var canvas = GameObject.Find("ShotUI_Canvas");
        if (canvas == null)
        {
            Debug.LogError("[IndicatorWidgetBuilder] ShotUI_Canvas not found in active scene. Open LabScaffold.unity first.");
            EditorUtility.DisplayDialog("Error", "ShotUI_Canvas not found. Open LabScaffold.unity first.", "OK");
            return;
        }

        // Remove any existing WindIndicator and HoleIndicator to avoid duplicates
        var existingWind = canvas.transform.Find("WindIndicator");
        if (existingWind != null) { Object.DestroyImmediate(existingWind.gameObject); Debug.Log("[IndicatorWidgetBuilder] Removed existing WindIndicator"); }
        var existingHole = canvas.transform.Find("HoleIndicator");
        if (existingHole != null) { Object.DestroyImmediate(existingHole.gameObject); Debug.Log("[IndicatorWidgetBuilder] Removed existing HoleIndicator"); }

        Color navyColor  = HexToColor("001E39");
        Color whiteColor = Color.white;

        // ─── WindIndicator ───────────────────────────────────────────────────

        // Root: 100x100, anchor top-left, pivot (0,1), pos (48, -362)
        var windRoot = CreateRectTransform("WindIndicator", canvas.transform, new Vector2(100, 100));
        SetAnchorTopLeft(windRoot, new Vector2(0, 1));
        windRoot.anchoredPosition = new Vector2(48f, -362f);

        // Backplate image: stretch-stretch to fill parent
        var windBackplate = CreateRectTransform("Backplate", windRoot);
        StretchFill(windBackplate);
        var windBackImg = windBackplate.gameObject.AddComponent<Image>();
        windBackImg.sprite = backplateSprite;
        windBackImg.type = Image.Type.Simple;

        // DirectionHalf: top 50px
        var dirHalf = CreateRectTransform("DirectionHalf", windRoot, new Vector2(100, 50));
        SetAnchorTopLeft(dirHalf, new Vector2(0, 1));
        dirHalf.anchoredPosition = new Vector2(0, 0);

        // Arrow: 32x30, anchor center, pivot (0.5, 0.5)
        var arrowRt = CreateRectTransform("Arrow", dirHalf, new Vector2(32, 30));
        SetAnchorCenter(arrowRt);
        arrowRt.anchoredPosition = Vector2.zero;
        if (windArrowSprite != null)
        {
            var arrowImg = arrowRt.gameObject.AddComponent<Image>();
            arrowImg.sprite = windArrowSprite;
            arrowImg.color  = whiteColor;
            arrowImg.preserveAspect = true;
        }
        else
        {
            // Fallback: placeholder white image so slot is not empty
            var arrowImg = arrowRt.gameObject.AddComponent<Image>();
            arrowImg.color = new Color(1f, 1f, 1f, 0.5f);
            Debug.LogWarning("[IndicatorWidgetBuilder] Wind arrow sprite unavailable; using white placeholder.");
        }

        // SpeedHalf: bottom 50px
        var speedHalf = CreateRectTransform("SpeedHalf", windRoot, new Vector2(100, 50));
        SetAnchorTopLeft(speedHalf, new Vector2(0, 1));
        speedHalf.anchoredPosition = new Vector2(0, -50);

        // SpeedText: TMP, stretch-stretch
        var speedTextGo = new GameObject("SpeedText");
        speedTextGo.transform.SetParent(speedHalf, false);
        var speedTextRt = speedTextGo.AddComponent<RectTransform>();
        StretchFill(speedTextRt);
        // Padding via offsetMin/offsetMax
        speedTextRt.offsetMin = new Vector2(9, 10);   // left, bottom
        speedTextRt.offsetMax = new Vector2(-9, -10); // -right, -top (inward)
        var speedTmp = speedTextGo.AddComponent<TextMeshProUGUI>();
        speedTmp.text = "0.0 mph";
        speedTmp.fontSize = 20;
        speedTmp.color = navyColor;
        speedTmp.alignment = TextAlignmentOptions.Center;
        if (rubikFont != null) speedTmp.font = rubikFont;

        // Add WindIndicatorWidget to WindIndicator root
        var windWidget = windRoot.gameObject.AddComponent<Golfin.Gameplay.UI.ShotUI.WindIndicatorWidget>();

        // Wire SerializeField refs via SerializedObject
        var windSo = new SerializedObject(windWidget);
        windSo.FindProperty("_arrow").objectReferenceValue = arrowRt;
        windSo.FindProperty("_speedText").objectReferenceValue = speedTmp;
        windSo.ApplyModifiedProperties();

        Debug.Log("[IndicatorWidgetBuilder] WindIndicator built and wired.");

        // ─── HoleIndicator (v3 — top-left anchored, sliding root) ───────────────

        // Root: 100x200 (100 chip + 100 tail), anchor TOP-LEFT, pivot top-left.
        // Initial X is just an inspector default; the widget overwrites it each LateUpdate.
        var holeRoot = CreateRectTransform("HoleIndicator", canvas.transform, new Vector2(100, 200));
        holeRoot.anchorMin = new Vector2(0, 1);
        holeRoot.anchorMax = new Vector2(0, 1);
        holeRoot.pivot     = new Vector2(0, 1);
        holeRoot.anchoredPosition = new Vector2(1170f - 100f - 48f, -362f); // initial: top-right corner with 48px padding

        // ArrowLine first so it renders BEHIND DataChip (lower sibling index = drawn first)
        // 6x110, anchoredPosition.y=-90 (10px inside chip bottom so tail hides behind rounded corner)
        var arrowLineRt = CreateRectTransform("ArrowLine", holeRoot, new Vector2(6, 110));
        arrowLineRt.anchorMin = new Vector2(0, 1);
        arrowLineRt.anchorMax = new Vector2(0, 1);
        arrowLineRt.pivot     = new Vector2(0.5f, 1f);
        arrowLineRt.anchoredPosition = new Vector2(50f, -90f); // X=center, Y=-90 = 10px inside chip bottom
        var trailImg = arrowLineRt.gameObject.AddComponent<Image>();
        trailImg.sprite = trailSprite;
        trailImg.color  = whiteColor;
        trailImg.type   = Image.Type.Simple;

        // DataChip: 100x100, anchor top-left of root, pivot top-left
        var dataChip = CreateRectTransform("DataChip", holeRoot, new Vector2(100, 100));
        dataChip.anchorMin = new Vector2(0, 1);
        dataChip.anchorMax = new Vector2(0, 1);
        dataChip.pivot     = new Vector2(0, 1);
        dataChip.anchoredPosition = new Vector2(0, 0);

        // DataChip Backplate
        var holeBackplate = CreateRectTransform("Backplate", dataChip);
        StretchFill(holeBackplate);
        var holeBackImg = holeBackplate.gameObject.AddComponent<Image>();
        holeBackImg.sprite = backplateSprite;
        holeBackImg.type   = Image.Type.Simple;

        // FlagHalf: top 50px (anchored top-left of DataChip)
        var flagHalf = CreateRectTransform("FlagHalf", dataChip, new Vector2(100, 50));
        flagHalf.anchorMin = new Vector2(0, 1);
        flagHalf.anchorMax = new Vector2(0, 1);
        flagHalf.pivot     = new Vector2(0, 1);
        flagHalf.anchoredPosition = new Vector2(0, 0);

        // FlagIcon: 83x42, anchor center within FlagHalf
        var flagIconRt = CreateRectTransform("FlagIcon", flagHalf, new Vector2(83, 42));
        SetAnchorCenter(flagIconRt);
        flagIconRt.anchoredPosition = Vector2.zero;
        var flagImg = flagIconRt.gameObject.AddComponent<Image>();
        flagImg.sprite = flagSprite;
        flagImg.preserveAspect = true;

        // DistanceHalf: bottom 50px
        var distHalf = CreateRectTransform("DistanceHalf", dataChip, new Vector2(100, 50));
        distHalf.anchorMin = new Vector2(0, 1);
        distHalf.anchorMax = new Vector2(0, 1);
        distHalf.pivot     = new Vector2(0, 1);
        distHalf.anchoredPosition = new Vector2(0, -50);

        // DistanceText: TMP, stretch within DistanceHalf
        var distTextGo = new GameObject("DistanceText");
        distTextGo.transform.SetParent(distHalf, false);
        var distTextRt = distTextGo.AddComponent<RectTransform>();
        StretchFill(distTextRt);
        distTextRt.offsetMin = new Vector2(9, 10);
        distTextRt.offsetMax = new Vector2(-9, -10);
        var distTmp = distTextGo.AddComponent<TextMeshProUGUI>();
        distTmp.text = "0 yds";
        distTmp.fontSize = 20;
        distTmp.color = navyColor;
        distTmp.alignment = TextAlignmentOptions.Center;
        if (rubikFont != null) distTmp.font = rubikFont;


        // Add HoleIndicatorWidget and wire SerializeFields
        var holeWidget = holeRoot.gameObject.AddComponent<Golfin.Gameplay.UI.ShotUI.HoleIndicatorWidget>();
        var holeSo = new SerializedObject(holeWidget);
        holeSo.FindProperty("_root").objectReferenceValue         = holeRoot;
        holeSo.FindProperty("_dataChip").objectReferenceValue     = dataChip;
        holeSo.FindProperty("_arrowLine").objectReferenceValue    = arrowLineRt;
        holeSo.FindProperty("_distanceText").objectReferenceValue = distTmp;
        holeSo.ApplyModifiedProperties();

        Debug.Log("[IndicatorWidgetBuilder] HoleIndicator built and wired (v3 hierarchy: top-left anchored sliding root).");

        // ─── HoleDatabaseLoader ─────────────────────────────────────────────
        // Ensure a HoleDatabaseLoader GameObject is present in the scene so
        // WindContext can be populated from the per-hole CSV at runtime.
        EnsureHoleDatabaseLoaderInScene(SceneManager.GetActiveScene());

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("[IndicatorWidgetBuilder] Scene saved. DONE — WindIndicator, HoleIndicator, and HoleDatabaseLoader built and wired.");
    }

    static void EnsureHoleDatabaseLoaderInScene(Scene scene)
    {
        // Check if one already exists
        var existing = Object.FindObjectsOfType<GolfinRedux.UI.HoleDatabaseLoader>();
        if (existing != null && existing.Length > 0)
        {
            Debug.Log("[IndicatorWidgetBuilder] HoleDatabaseLoader already in scene; skipping.");
            return;
        }

        var go = new GameObject("HoleDatabaseLoader");
        SceneManager.MoveGameObjectToScene(go, scene);
        var loader = go.AddComponent<GolfinRedux.UI.HoleDatabaseLoader>();

        // Find the CSV TextAsset
        var csv = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/HoleDatabase.csv");
        if (csv == null)
        {
            Debug.LogError("[IndicatorWidgetBuilder] HoleDatabase.csv not found at Assets/Data/HoleDatabase.csv");
            return;
        }

        // Assign the CSV via SerializedObject
        var so = new SerializedObject(loader);
        // HoleDatabaseLoader field is named "holeDatabaseCSV"
        var prop = so.FindProperty("holeDatabaseCSV");
        if (prop == null)
        {
            Debug.LogError("[IndicatorWidgetBuilder] HoleDatabaseLoader serialized field 'holeDatabaseCSV' not found. Check loader field names.");
            return;
        }
        prop.objectReferenceValue = csv;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(loader);
        Debug.Log("[IndicatorWidgetBuilder] HoleDatabaseLoader added to scene with HoleDatabase.csv assigned.");
    }

    /// <summary>
    /// Non-interactive version for MCP script execution (no dialog boxes).
    /// </summary>
    public static void BuildIndicatorWidgetsNoDialog()
    {
        BuildIndicatorWidgets();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    static RectTransform CreateRectTransform(string name, Transform parent, Vector2 size = default)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        if (size != default) rt.sizeDelta = size;
        return rt;
    }

    // Anchor top-left with given anchor point
    static void SetAnchorTopLeft(RectTransform rt, Vector2 anchor)
    {
        rt.anchorMin = anchor == new Vector2(0, 1) ? new Vector2(0, 1) : anchor;
        rt.anchorMax = anchor == new Vector2(0, 1) ? new Vector2(0, 1) : anchor;
        rt.pivot     = new Vector2(0, 1);
    }

    static void SetAnchorTopRight(RectTransform rt, Vector2 anchor)
    {
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot     = new Vector2(1, 1);
    }

    static void SetAnchorCenter(RectTransform rt)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
    }

    static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString("#" + hex, out Color c)) return c;
        return Color.white;
    }
}
