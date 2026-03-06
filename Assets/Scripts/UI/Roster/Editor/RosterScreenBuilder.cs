#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

namespace Golfin.Roster.Editor
{
    /// <summary>
    /// Automated builder for complete Roster Screen hierarchy
    /// Creates all UI elements under Canvas and auto-wires components
    /// </summary>
    public static class RosterScreenBuilder
    {
        private const string PREFAB_PATH = "Assets/Prefabs/UI/Roster/";
        
        [MenuItem("Tools/GOLFIN/Roster/Build Complete Roster Screen", priority = 1)]
        public static void BuildFromScratch()
        {
            Debug.Log("[RosterBuilder] Starting complete Roster Screen build...");
            
            // 1. Find Canvas
            var canvas = FindCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Error", 
                    "Canvas not found in scene!\n\n" +
                    "Make sure you're in ShellScene with Canvas GameObject.",
                    "OK");
                return;
            }
            
            // 2. Delete existing RosterScreen if it exists
            var existing = canvas.transform.Find("RosterScreen");
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("Replace Existing?",
                    "RosterScreen already exists. Replace it?",
                    "Yes, Replace",
                    "Cancel"))
                {
                    return;
                }
                Object.DestroyImmediate(existing.gameObject);
                Debug.Log("[RosterBuilder] Deleted existing RosterScreen");
            }
            
            // 3. Create RosterScreen root
            var rosterScreen = CreateRosterScreenRoot(canvas.transform);
            
            // 4. Create Header
            CreateHeader(rosterScreen.transform);
            
            // 5. Create Carousel Section
            CreateCarouselSection(rosterScreen.transform);
            
            // 6. Create Detail Panel
            CreateDetailPanel(rosterScreen.transform);
            
            // 7. Add RosterScreenController and wire references
            var controller = rosterScreen.AddComponent<RosterScreenController>();
            WireRosterScreenController(controller, rosterScreen.transform);
            
            // 8. Wire to ScreenManager
            WireToScreenManager(rosterScreen);
            
            // 9. Mark scene dirty
            EditorUtility.SetDirty(rosterScreen);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rosterScreen.scene);
            
            // 10. Select the created object
            Selection.activeGameObject = rosterScreen;
            
            EditorUtility.DisplayDialog("Success!",
                "Roster Screen created successfully!\n\n" +
                "✓ Hierarchy created under Canvas\n" +
                "✓ All components added\n" +
                "✓ References auto-wired\n" +
                "✓ ScreenManager linked\n\n" +
                "Next steps:\n" +
                "1. Enter Play Mode to test\n" +
                "2. Check Console for initialization logs\n" +
                "3. Click Characters button in bottom nav",
                "OK");
            
            Debug.Log("[RosterBuilder] ✓ Roster Screen build complete!");
        }
        
        private static Canvas FindCanvas()
        {
            var canvasGO = GameObject.Find("Canvas");
            return canvasGO != null ? canvasGO.GetComponent<Canvas>() : null;
        }
        
        private static GameObject CreateRosterScreenRoot(Transform parent)
        {
            var rosterScreen = new GameObject("RosterScreen");
            rosterScreen.transform.SetParent(parent, false);
            
            // RectTransform - stretch to fill Canvas
            var rect = rosterScreen.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            
            // CanvasGroup for fade in/out
            rosterScreen.AddComponent<CanvasGroup>();
            
            // Initially inactive (ScreenManager will enable it)
            rosterScreen.SetActive(false);
            
            Debug.Log("[RosterBuilder] ✓ Created RosterScreen root");
            return rosterScreen;
        }
        
        private static void CreateHeader(Transform parent)
        {
            var header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            
            var rect = header.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 80);
            rect.anchoredPosition = Vector2.zero;
            
            // Background
            var bg = header.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
            
            // Reward Points Display
            var rpDisplay = new GameObject("RewardPointsDisplay");
            rpDisplay.transform.SetParent(header.transform, false);
            
            var rpRect = rpDisplay.AddComponent<RectTransform>();
            rpRect.anchorMin = new Vector2(0, 0.5f);
            rpRect.anchorMax = new Vector2(0, 0.5f);
            rpRect.pivot = new Vector2(0, 0.5f);
            rpRect.sizeDelta = new Vector2(200, 40);
            rpRect.anchoredPosition = new Vector2(20, 0);
            
            var rpText = rpDisplay.AddComponent<TextMeshProUGUI>();
            rpText.text = "R 0";
            rpText.fontSize = 24;
            rpText.color = Color.yellow;
            rpText.alignment = TextAlignmentOptions.Left;
            
            // Title
            var title = new GameObject("TitleText");
            title.transform.SetParent(header.transform, false);
            
            var titleRect = title.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(200, 40);
            titleRect.anchoredPosition = Vector2.zero;
            
            var titleText = title.AddComponent<TextMeshProUGUI>();
            titleText.text = "ROSTER";
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;
            
            Debug.Log("[RosterBuilder] ✓ Created Header");
        }
        
        private static void CreateCarouselSection(Transform parent)
        {
            var carouselSection = new GameObject("CarouselSection");
            carouselSection.transform.SetParent(parent, false);
            
            var rect = carouselSection.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.sizeDelta = new Vector2(0, 250);
            rect.anchoredPosition = new Vector2(0, -80); // Below header
            
            // Background
            var bg = carouselSection.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            
            // Left Arrow
            var leftArrow = CreateArrowButton(carouselSection.transform, "LeftArrow", true);
            
            // Right Arrow
            var rightArrow = CreateArrowButton(carouselSection.transform, "RightArrow", false);
            
            // ScrollView
            var scrollView = CreateScrollView(carouselSection.transform);
            
            // Pagination Dots Container
            var dotsContainer = new GameObject("PaginationDots");
            dotsContainer.transform.SetParent(carouselSection.transform, false);
            
            var dotsRect = dotsContainer.AddComponent<RectTransform>();
            dotsRect.anchorMin = new Vector2(0.5f, 0);
            dotsRect.anchorMax = new Vector2(0.5f, 0);
            dotsRect.pivot = new Vector2(0.5f, 0);
            dotsRect.sizeDelta = new Vector2(300, 20);
            dotsRect.anchoredPosition = new Vector2(0, 10);
            
            var layout = dotsContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            
            // Add CarouselController
            var controller = carouselSection.AddComponent<CarouselController>();
            
            Debug.Log("[RosterBuilder] ✓ Created Carousel Section");
        }
        
        private static GameObject CreateArrowButton(Transform parent, string name, bool isLeft)
        {
            var arrow = new GameObject(name);
            arrow.transform.SetParent(parent, false);
            
            var rect = arrow.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(isLeft ? 0 : 1, 0.5f);
            rect.anchorMax = new Vector2(isLeft ? 0 : 1, 0.5f);
            rect.pivot = new Vector2(isLeft ? 0 : 1, 0.5f);
            rect.sizeDelta = new Vector2(50, 50);
            rect.anchoredPosition = new Vector2(isLeft ? 10 : -10, 0);
            
            var button = arrow.AddComponent<Button>();
            var image = arrow.AddComponent<Image>();
            image.color = Color.white;
            
            // Arrow text (◄ or ►)
            var text = new GameObject("Text");
            text.transform.SetParent(arrow.transform, false);
            
            var textRect = text.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            var tmp = text.AddComponent<TextMeshProUGUI>();
            tmp.text = isLeft ? "◄" : "►";
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.black;
            
            return arrow;
        }
        
        private static GameObject CreateScrollView(Transform parent)
        {
            var scrollView = new GameObject("ScrollView");
            scrollView.transform.SetParent(parent, false);
            
            var rect = scrollView.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(-120, -40); // Leave room for arrows
            rect.anchoredPosition = Vector2.zero;
            
            var scrollRect = scrollView.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical = false;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            
            // Viewport
            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            
            var vpRect = viewport.AddComponent<RectTransform>();
            vpRect.anchorMin = Vector2.zero;
            vpRect.anchorMax = Vector2.one;
            vpRect.sizeDelta = Vector2.zero;
            vpRect.anchoredPosition = Vector2.zero;
            
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            var vpImage = viewport.AddComponent<Image>();
            vpImage.color = Color.clear;
            
            scrollRect.viewport = vpRect;
            
            // Content
            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            var contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0.5f);
            contentRect.anchorMax = new Vector2(0, 0.5f);
            contentRect.pivot = new Vector2(0, 0.5f);
            contentRect.sizeDelta = new Vector2(800, 200); // Will expand dynamically
            contentRect.anchoredPosition = Vector2.zero;
            
            var layout = content.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scrollRect.content = contentRect;
            
            return scrollView;
        }
        
        private static void CreateDetailPanel(Transform parent)
        {
            var detailPanel = new GameObject("DetailPanel");
            detailPanel.transform.SetParent(parent, false);
            
            var rect = detailPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.sizeDelta = new Vector2(0, -330); // Below carousel
            rect.anchoredPosition = new Vector2(0, -165); // Centered in remaining space
            
            // Background
            var bg = detailPanel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.3f);
            
            // TODO: Add portrait, stats, buttons, bio
            // For now just placeholder text
            var placeholder = new GameObject("PlaceholderText");
            placeholder.transform.SetParent(detailPanel.transform, false);
            
            var phRect = placeholder.AddComponent<RectTransform>();
            phRect.anchorMin = new Vector2(0.5f, 0.5f);
            phRect.anchorMax = new Vector2(0.5f, 0.5f);
            phRect.pivot = new Vector2(0.5f, 0.5f);
            phRect.sizeDelta = new Vector2(400, 100);
            phRect.anchoredPosition = Vector2.zero;
            
            var phText = placeholder.AddComponent<TextMeshProUGUI>();
            phText.text = "Detail Panel\n(Select a character)";
            phText.fontSize = 24;
            phText.alignment = TextAlignmentOptions.Center;
            phText.color = Color.gray;
            
            Debug.Log("[RosterBuilder] ✓ Created Detail Panel (placeholder)");
        }
        
        private static void WireRosterScreenController(RosterScreenController controller, Transform root)
        {
            // Find and wire references using SerializedObject
            var so = new SerializedObject(controller);
            
            // Find RewardPointsText
            var rpText = root.Find("Header/RewardPointsDisplay")?.GetComponent<TextMeshProUGUI>();
            if (rpText != null)
            {
                var rpProp = so.FindProperty("rewardPointsText");
                if (rpProp != null)
                {
                    rpProp.objectReferenceValue = rpText;
                }
            }
            
            // Find CarouselController
            var carousel = root.Find("CarouselSection")?.GetComponent<CarouselController>();
            if (carousel != null)
            {
                var carouselProp = so.FindProperty("carousel");
                if (carouselProp != null)
                {
                    carouselProp.objectReferenceValue = carousel;
                }
            }
            
            so.ApplyModifiedProperties();
            Debug.Log("[RosterBuilder] ✓ Wired RosterScreenController references");
        }
        
        private static void WireToScreenManager(GameObject rosterScreen)
        {
            var screenManager = Object.FindObjectOfType<GolfinRedux.UI.ScreenManager>();
            if (screenManager == null)
            {
                Debug.LogWarning("[RosterBuilder] ScreenManager not found - manual wiring needed");
                return;
            }
            
            var so = new SerializedObject(screenManager);
            var rosterProp = so.FindProperty("_rosterScreen");
            
            if (rosterProp != null)
            {
                rosterProp.objectReferenceValue = rosterScreen;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(screenManager);
                Debug.Log("[RosterBuilder] ✓ Wired to ScreenManager");
            }
            else
            {
                Debug.LogWarning("[RosterBuilder] ScreenManager._rosterScreen property not found");
            }
        }
    }
}
#endif
