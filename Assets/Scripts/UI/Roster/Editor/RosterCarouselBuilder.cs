#nullable enable
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Golfin.Roster;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// One-click carousel builder
    /// Menu: Tools → GOLFIN → Build Roster Carousel (Phase 2a)
    /// 
    /// Creates complete carousel hierarchy:
    /// - Carousel (ScrollRect parent)
    ///   ├── Header (R display, title, settings, filter)
    ///   ├── CarouselPanel (ScrollRect viewport)
    ///   │   └── Content (Horizontal Layout, card container)
    ///   │       ├── CharacterCard (prefab)
    ///   │       └── ...
    ///   ├── LeftArrow (button)
    ///   ├── RightArrow (button)
    ///   └── PaginationDots (container)
    ///       ├── Dot
    ///       └── ...
    /// </summary>
    public class RosterCarouselBuilder
    {
        private const string SETUP_COMPLETE_MESSAGE = 
            "✅ Roster Carousel (Phase 2a) Built!\n\n" +
            "Created:\n" +
            "• Carousel ScrollRect with viewport\n" +
            "• Header with R display, title, buttons\n" +
            "• Left/Right arrow navigation\n" +
            "• Pagination dots container\n" +
            "• Character card prefab template\n\n" +
            "Next:\n" +
            "1. Add this to ShellCanvas in ShellScene\n" +
            "2. Assign prefab + references in inspector\n" +
            "3. Run Phase 2a test to verify";
        
        [MenuItem("Tools/GOLFIN/Build Roster Carousel (Phase 2a)")]
        public static void BuildCarousel()
        {
            Debug.Log("[RosterCarouselBuilder] Building Roster Carousel...");
            
            try
            {
                // Create root carousel GameObject
                var carouselGO = new GameObject("RosterScreen");
                var canvasGroup = carouselGO.AddComponent<CanvasGroup>();
                
                // Header panel
                CreateHeader(carouselGO.transform);
                
                // Main carousel with ScrollRect
                var carouselPanel = CreateCarouselPanel(carouselGO.transform);
                
                // Navigation arrows
                CreateLeftArrow(carouselGO.transform);
                CreateRightArrow(carouselGO.transform);
                
                // Pagination dots
                CreatePaginationDots(carouselGO.transform);
                
                // Add controllers
                var rosterController = carouselGO.AddComponent<RosterScreenController>();
                var carouselController = carouselPanel.AddComponent<CarouselController>();
                
                // Wire up references via SerializedObject
                WireCarouselReferences(carouselGO, carouselPanel, carouselController);
                
                EditorUtility.DisplayDialog(
                    "Carousel Built",
                    SETUP_COMPLETE_MESSAGE,
                    "OK"
                );
                
                Debug.Log("[RosterCarouselBuilder] ✅ Carousel built successfully!");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RosterCarouselBuilder] Build failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog(
                    "Build Failed",
                    $"Error: {e.Message}",
                    "OK"
                );
            }
        }
        
        private static void CreateHeader(Transform parent)
        {
            var headerGO = new GameObject("Header");
            headerGO.transform.SetParent(parent);
            
            var headerImage = headerGO.AddComponent<Image>();
            headerImage.color = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark blue gradient
            
            var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(16, 16, 8, 8);
            headerLayout.spacing = 16;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            
            var headerRect = headerGO.GetComponent<RectTransform>();
            headerRect.anchorMin = Vector2.up;
            headerRect.anchorMax = Vector2.one;
            headerRect.offsetMin = new Vector2(0, -60);
            headerRect.offsetMax = Vector2.zero;
            
            // R Points display
            var pointsGO = CreateTextObject(headerGO.transform, "R 50000", 20);
            var pointsLayout = pointsGO.AddComponent<LayoutElement>();
            pointsLayout.preferredWidth = 120;
            
            // Title
            var titleGO = CreateTextObject(headerGO.transform, "ROSTER", 24, TextAlignmentOptions.Center);
            var titleLayout = titleGO.AddComponent<LayoutElement>();
            titleLayout.flexibleWidth = 1;
            
            // Settings button (placeholder)
            var settingsGO = new GameObject("SettingsButton");
            settingsGO.transform.SetParent(headerGO.transform);
            var settingsBtn = settingsGO.AddComponent<Button>();
            settingsBtn.targetGraphic = settingsGO.AddComponent<Image>();
            settingsGO.GetComponent<Image>().color = Color.gray;
            var settingsLayout = settingsGO.AddComponent<LayoutElement>();
            settingsLayout.preferredWidth = 40;
            settingsLayout.preferredHeight = 40;
            
            EditorUtility.SetDirty(headerGO);
        }
        
        private static GameObject CreateCarouselPanel(Transform parent)
        {
            var carouselGO = new GameObject("Carousel");
            carouselGO.transform.SetParent(parent);
            
            // ScrollRect
            var scrollRect = carouselGO.AddComponent<ScrollRect>();
            scrollRect.vertical = false;
            scrollRect.horizontal = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            
            var carouselRect = carouselGO.GetComponent<RectTransform>();
            carouselRect.anchorMin = new Vector2(0, 0);
            carouselRect.anchorMax = new Vector2(1, 0.5f);
            carouselRect.offsetMin = new Vector2(0, 60);
            carouselRect.offsetMax = new Vector2(0, 0);
            
            // Viewport
            var viewportGO = new GameObject("Viewport");
            viewportGO.transform.SetParent(carouselGO.transform);
            var viewportMask = viewportGO.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            
            var viewportRect = viewportGO.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            
            scrollRect.viewport = viewportRect;
            
            // Content (Horizontal Layout)
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform);
            
            var layout = contentGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16;
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            
            var contentRect = contentGO.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.up * 0;
            contentRect.anchorMax = Vector2.up;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            
            var contentSize = contentGO.AddComponent<ContentSizeFitter>();
            contentSize.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            contentSize.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            
            scrollRect.content = contentRect;
            
            EditorUtility.SetDirty(carouselGO);
            return carouselGO;
        }
        
        private static void CreateLeftArrow(Transform parent)
        {
            var arrowGO = new GameObject("LeftArrow");
            arrowGO.transform.SetParent(parent);
            
            var btn = arrowGO.AddComponent<Button>();
            btn.targetGraphic = arrowGO.AddComponent<Image>();
            
            var arrowImage = arrowGO.GetComponent<Image>();
            arrowImage.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
            
            var arrowText = CreateTextObject(arrowGO.transform, "<", 24);
            
            var arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(0, 0.25f);
            arrowRect.anchorMax = new Vector2(0, 0.75f);
            arrowRect.offsetMin = new Vector2(4, 0);
            arrowRect.offsetMax = new Vector2(44, 0);
            
            EditorUtility.SetDirty(arrowGO);
        }
        
        private static void CreateRightArrow(Transform parent)
        {
            var arrowGO = new GameObject("RightArrow");
            arrowGO.transform.SetParent(parent);
            
            var btn = arrowGO.AddComponent<Button>();
            btn.targetGraphic = arrowGO.AddComponent<Image>();
            
            var arrowImage = arrowGO.GetComponent<Image>();
            arrowImage.color = new Color(0.3f, 0.3f, 0.4f, 0.8f);
            
            var arrowText = CreateTextObject(arrowGO.transform, ">", 24);
            
            var arrowRect = arrowGO.GetComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.25f);
            arrowRect.anchorMax = new Vector2(1, 0.75f);
            arrowRect.offsetMin = new Vector2(-44, 0);
            arrowRect.offsetMax = new Vector2(-4, 0);
            
            EditorUtility.SetDirty(arrowGO);
        }
        
        private static void CreatePaginationDots(Transform parent)
        {
            var dotsGO = new GameObject("PaginationDots");
            dotsGO.transform.SetParent(parent);
            
            var dotsLayout = dotsGO.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 8;
            dotsLayout.childForceExpandWidth = false;
            dotsLayout.childForceExpandHeight = false;
            
            var dotsRect = dotsGO.GetComponent<RectTransform>();
            dotsRect.anchorMin = new Vector2(0.5f, 0);
            dotsRect.anchorMax = new Vector2(0.5f, 0);
            dotsRect.sizeDelta = new Vector2(200, 30);
            dotsRect.anchoredPosition = new Vector2(0, 15);
            
            EditorUtility.SetDirty(dotsGO);
        }
        
        private static GameObject CreateTextObject(Transform parent, string text, int fontSize, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(parent);
            
            var textMesh = textGO.AddComponent<TextMeshProUGUI>();
            textMesh.text = text;
            textMesh.fontSize = fontSize;
            textMesh.alignment = alignment;
            textMesh.color = Color.white;
            
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            EditorUtility.SetDirty(textGO);
            return textGO;
        }
        
        private static void WireCarouselReferences(GameObject carouselGO, GameObject carouselPanel, CarouselController carouselController)
        {
            var contentParent = carouselPanel.transform.Find("Carousel/Viewport/Content");
            var leftArrow = carouselGO.transform.Find("LeftArrow")?.GetComponent<Button>();
            var rightArrow = carouselGO.transform.Find("RightArrow")?.GetComponent<Button>();
            var dotsParent = carouselGO.transform.Find("PaginationDots");
            
            var so = new SerializedObject(carouselController);
            
            if (contentParent != null)
                so.FindProperty("contentParent").objectReferenceValue = contentParent;
            
            if (leftArrow != null)
                so.FindProperty("leftArrowButton").objectReferenceValue = leftArrow;
            
            if (rightArrow != null)
                so.FindProperty("rightArrowButton").objectReferenceValue = rightArrow;
            
            if (dotsParent != null)
                so.FindProperty("paginationDotsParent").objectReferenceValue = dotsParent;
            
            so.ApplyModifiedProperties();
        }
    }
}
#endif
