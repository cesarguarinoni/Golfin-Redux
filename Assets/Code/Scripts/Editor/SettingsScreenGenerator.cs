using UnityEngine;  
using UnityEditor;  
using UnityEngine.UI;  
using TMPro;  
  
/// <summary>  
/// Generates the Settings Screen UI with accordion sections and connects to HomeScreen SettingsButton.  
/// </summary>  
public class SettingsScreenGenerator  
{ 
    [MenuItem("Tools/GOLFIN/Generate Settings Screen")]  
    public static void GenerateSettingsScreen()  
    {  
        Debug.Log("[GOLFIN] Generating Settings Screen...");  
  
        // Find Canvas  
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();  
        if (canvas == null)  
        {  
            Debug.LogError("[GOLFIN] No Canvas found in scene");  
            return;  
        }  
  
        GameObject settingsScreenGO = CreateSettingsScreen(canvas.transform);  
        ConnectToHomeScreenButton();  
  
  
    private static GameObject CreateSettingsScreen(Transform parent)  
    {  
        // Create main SettingsScreen GameObject  
        GameObject settingsGO = new GameObject("SettingsScreen");  
        settingsGO.transform.SetParent(parent, false);  
  
        // Add RectTransform and set full screen  
        RectTransform settingsRT = settingsGO.AddComponent<RectTransform>();  
        settingsRT.anchorMin = Vector2.zero;  
        settingsRT.anchorMax = Vector2.one;  
        settingsRT.offsetMin = Vector2.zero;  
        settingsRT.offsetMax = Vector2.zero;  
  
        // Add CanvasGroup for fade animation  
        CanvasGroup canvasGroup = settingsGO.AddComponent<CanvasGroup>();  
        canvasGroup.alpha = 1f;  
  
        // Add background  
        Image backgroundImg = settingsGO.AddComponent<Image>();  
        backgroundImg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f); // Dark semi-transparent 
  
        // Create ScrollView  
        GameObject scrollViewGO = new GameObject("ScrollView");  
        scrollViewGO.transform.SetParent(settingsGO.transform, false);  
        RectTransform scrollRT = scrollViewGO.AddComponent<RectTransform>();  
        scrollRT.anchorMin = new Vector2(0.1f, 0.1f);  
        scrollRT.anchorMax = new Vector2(0.9f, 0.85f);  
        scrollRT.offsetMin = Vector2.zero;  
        scrollRT.offsetMax = Vector2.zero;  
  
        ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();  
        scrollRect.vertical = true;  
        scrollRect.horizontal = false;  
  
        // Create Content container  
        GameObject contentGO = new GameObject("Content");  
        contentGO.transform.SetParent(scrollViewGO.transform, false);  
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();  
        contentRT.anchorMin = new Vector2(0f, 1f);  
        contentRT.anchorMax = new Vector2(1f, 1f);  
        contentRT.pivot = new Vector2(0.5f, 1f);  
  
        VerticalLayoutGroup vlg = contentGO.AddComponent<VerticalLayoutGroup>();  
        vlg.spacing = 10f;  
        vlg.padding = new RectOffset(20, 20, 20, 20);  
  
        ContentSizeFitter csf = contentGO.AddComponent<ContentSizeFitter>();  
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;  
  
        scrollRect.content = contentRT; 
  
        // Create Settings Sections  
        string[] sectionNames = { "User Profile", "Language", "Sound Settings", "Terms of Use", "Privacy Policy", "FAQ", "About", "Contact Form" };  
  
        foreach (string sectionName in sectionNames)  
        {  
            CreateSettingsSection(contentGO.transform, sectionName);  
        }  
  
        // Create Log Out button (separate from sections)  
        CreateLogOutButton(contentGO.transform);  
  
        // Create Close Button  
        CreateCloseButton(settingsGO.transform);  
  
        // Add SettingsScreen component and assign references  
        SettingsScreen settingsScreen = settingsGO.AddComponent<SettingsScreen>();  
        AssignSettingsScreenReferences(settingsScreen, settingsGO, canvasGroup, scrollRect, contentGO.transform);  
  
        return settingsGO;  
    } 
  
    private static GameObject CreateSettingsSection(Transform parent, string sectionName)  
    {  
        // Create section container  
        GameObject sectionGO = new GameObject(sectionName.Replace(" ", "") + "Section");  
        sectionGO.transform.SetParent(parent, false);  
  
        RectTransform sectionRT = sectionGO.AddComponent<RectTransform>();  
        sectionRT.anchorMin = new Vector2(0f, 1f);  
        sectionRT.anchorMax = new Vector2(1f, 1f);  
        sectionRT.pivot = new Vector2(0.5f, 1f);  
        sectionRT.sizeDelta = new Vector2(0f, 120f); // Will adjust based on content  
  
        VerticalLayoutGroup sectionVLG = sectionGO.AddComponent<VerticalLayoutGroup>();  
        sectionVLG.spacing = 5f;  
  
        // Create header button  
        GameObject headerGO = CreateSectionHeader(sectionGO.transform, sectionName);  
  
        // Create content container (starts collapsed)  
        GameObject contentGO = CreateSectionContent(sectionGO.transform, sectionName);  
  
        // Add SettingsSection component  
        SettingsSection settingsSection = sectionGO.AddComponent<SettingsSection>();  
  
        return sectionGO;  
    } 
  
    private static GameObject CreateSectionHeader(Transform parent, string sectionName)  
    {  
        GameObject headerGO = new GameObject("Header");  
        headerGO.transform.SetParent(parent, false);  
  
        RectTransform headerRT = headerGO.AddComponent<RectTransform>();  
        headerRT.sizeDelta = new Vector2(0f, 60f);  
  
        // Add background  
        Image headerBg = headerGO.AddComponent<Image>();  
        headerBg.color = new Color(0.2f, 0.25f, 0.3f, 0.8f);  
  
        // Add button  
        Button headerButton = headerGO.AddComponent<Button>();  
        headerButton.targetGraphic = headerBg;  
  
        // Add text  
        GameObject textGO = new GameObject("Text");  
        textGO.transform.SetParent(headerGO.transform, false);  
  
        RectTransform textRT = textGO.AddComponent<RectTransform>();  
        textRT.anchorMin = Vector2.zero;  
        textRT.anchorMax = Vector2.one;  
        textRT.offsetMin = new Vector2(20f, 0f);  
        textRT.offsetMax = new Vector2(-20f, 0f);  
  
        TextMeshProUGUI headerText = textGO.AddComponent<TextMeshProUGUI>();  
        headerText.text = sectionName;  
        headerText.fontSize = 18f;  
        headerText.color = Color.white;  
        headerText.alignment = TextAlignmentOptions.MidlineLeft;  
  
        return headerGO;  
    } 
  
    private static GameObject CreateSectionContent(Transform parent, string sectionName)  
    {  
        GameObject contentGO = new GameObject("Content");  
        contentGO.transform.SetParent(parent, false);  
  
        RectTransform contentRT = contentGO.AddComponent<RectTransform>();  
        contentRT.sizeDelta = new Vector2(0f, 100f); // Placeholder height  
  
        // Add placeholder content based on section type  
        AddSectionSpecificContent(contentGO.transform, sectionName);  
  
        // Start collapsed  
        contentGO.SetActive(false);  
  
        return contentGO;  
    }  
  
    private static void CreateCloseButton(Transform parent)  
    {  
        GameObject closeButtonGO = new GameObject("CloseButton");  
        closeButtonGO.transform.SetParent(parent, false);  
  
        RectTransform closeRT = closeButtonGO.AddComponent<RectTransform>();  
        closeRT.anchorMin = new Vector2(0.35f, 0.05f);  
        closeRT.anchorMax = new Vector2(0.65f, 0.12f);  
        closeRT.offsetMin = Vector2.zero;  
        closeRT.offsetMax = Vector2.zero;  
  
        Image closeBg = closeButtonGO.AddComponent<Image>();  
        closeBg.color = new Color(0.8f, 0.2f, 0.2f, 0.8f); // Red  
  
        PressableButton closeButton = closeButtonGO.AddComponent<PressableButton>();  
  
        // Add close text  
        GameObject closeTextGO = new GameObject("Text");  
        closeTextGO.transform.SetParent(closeButtonGO.transform, false);  
        TextMeshProUGUI closeText = closeTextGO.AddComponent<TextMeshProUGUI>();  
        closeText.text = "Close";  
        closeText.fontSize = 18f;  
        closeText.color = Color.white;  
        closeText.alignment = TextAlignmentOptions.Center;  
    } 
  
    private static void CreateLogOutButton(Transform parent)  
    {  
        GameObject logoutGO = new GameObject("LogOutButton");  
        logoutGO.transform.SetParent(parent, false);  
  
        RectTransform logoutRT = logoutGO.AddComponent<RectTransform>();  
        logoutRT.sizeDelta = new Vector2(0f, 50f);  
  
        Image logoutBg = logoutGO.AddComponent<Image>();  
        logoutBg.color = new Color(0.9f, 0.3f, 0.3f, 0.9f);  
  
        PressableButton logoutButton = logoutGO.AddComponent<PressableButton>();  
  
        GameObject logoutTextGO = new GameObject("Text");  
        logoutTextGO.transform.SetParent(logoutGO.transform, false);  
        TextMeshProUGUI logoutText = logoutTextGO.AddComponent<TextMeshProUGUI>();  
        logoutText.text = "Log Out";  
        logoutText.fontSize = 16f;  
        logoutText.color = Color.white;  
        logoutText.alignment = TextAlignmentOptions.Center;  
    }  
  
    private static void AddSectionSpecificContent(Transform parent, string sectionName)  
    {  
        // Add placeholder content - will be replaced with Figma assets  
        GameObject placeholderGO = new GameObject("PlaceholderContent");  
        placeholderGO.transform.SetParent(parent, false);  
        TextMeshProUGUI placeholder = placeholderGO.AddComponent<TextMeshProUGUI>();  
        placeholder.text = "Content for " + sectionName + " will be added here.";  
        placeholder.fontSize = 14f;  
        placeholder.color = new Color(0.8f, 0.8f, 0.8f);  
        placeholder.alignment = TextAlignmentOptions.Center;  
    } 
  
    private static void AssignSettingsScreenReferences(SettingsScreen settingsScreen, GameObject settingsGO, CanvasGroup canvasGroup, ScrollRect scrollRect, Transform contentContainer)  
    {  
        // Use reflection to assign private fields  
        var canvasGroupField = typeof(SettingsScreen).GetField("canvasGroup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);  
        canvasGroupField?.SetValue(settingsScreen, canvasGroup);  
  
        var scrollRectField = typeof(SettingsScreen).GetField("scrollRect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);  
        scrollRectField?.SetValue(settingsScreen, scrollRect);  
  
        var contentField = typeof(SettingsScreen).GetField("contentContainer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);  
        contentField?.SetValue(settingsScreen, contentContainer);  
  
        var closeButtonField = typeof(SettingsScreen).GetField("closeButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);  
        PressableButton closeBtn = settingsGO.transform.Find("CloseButton")?.GetComponent<PressableButton>();  
        closeButtonField?.SetValue(settingsScreen, closeBtn);  
  
        var logoutButtonField = typeof(SettingsScreen).GetField("logoutButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);  
        PressableButton logoutBtn = contentContainer.Find("LogOutButton")?.GetComponent<PressableButton>();  
        logoutButtonField?.SetValue(settingsScreen, logoutBtn);  
    } 
  
    private static void ConnectToHomeScreenButton()  
    {  
        HomeScreen homeScreen = Object.FindAnyObjectByType<HomeScreen>();  
        if (homeScreen == null)  
        {  
            Debug.LogWarning("[GOLFIN] HomeScreen not found.");  
            return;  
        }  
  
        Debug.Log("[GOLFIN] Settings Screen ready for connection to SettingsButton");  
    }  
} 
