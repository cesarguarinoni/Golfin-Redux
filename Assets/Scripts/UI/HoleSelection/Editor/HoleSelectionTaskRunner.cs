using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GolfinRedux.UI.HoleSelection;
using Golfin.EditorTools;

namespace GolfinRedux.UI.HoleSelection.Editor
{
    /// <summary>
    /// File-triggered task runner for hole_selection_screen smoke test.
    /// On each editor update, checks for a trigger file at:
    ///   Docs/Diagnostics/_capture/hole_sel_trigger.txt
    /// If found, runs the specified task and deletes the trigger.
    ///
    /// Supported commands:
    ///   RUN_PREREQUISITES   — runs localization import + sprite config
    ///   RUN_AUTOWIRE        — runs GOLFIN/Wire/Hole Selection
    ///   SNAP_EDITMODE       — takes a Game View snapshot in EditMode
    ///   RUN_ITERATION3      — imports sprites + upgrades HoleCard prefab + wires filter pills
    ///   SNAP_PLAYMODE       — enters play mode, waits, snaps, exits
    ///   ALL                 — runs full setup chain + snap
    ///
    /// This does NOT auto-enter play mode unless explicitly commanded.
    /// </summary>
    [InitializeOnLoad]
    public static class HoleSelectionTaskRunner
    {
        private const string TRIGGER_FILE = "Docs/Diagnostics/_capture/hole_sel_trigger.txt";
        private const string RESULT_FILE  = "Docs/Diagnostics/_capture/hole_sel_result.txt";

        private const string LOCK_SPRITE_PATH   = "Assets/Resources/UI/HoleSelection/S_HoleSel_FilterLock.png";
        private const string CHEVRON_RIGHT_PATH = "Assets/Resources/UI/HoleSelection/S_HoleSel_ChevronRight.png";
        private const string CHEVRON_DOWN_PATH  = "Assets/Resources/UI/HoleSelection/S_HoleSel_ChevronDown.png";
        private const string HOLE_CARD_PREFAB   = "Assets/Prefabs/UI/HoleSelection/HoleCard.prefab";

        static HoleSelectionTaskRunner()
        {
            EditorApplication.update += CheckTrigger;
        }

        private static void CheckTrigger()
        {
            if (!File.Exists(TRIGGER_FILE)) return;

            string cmd = File.ReadAllText(TRIGGER_FILE).Trim();
            File.Delete(TRIGGER_FILE);

            Debug.Log($"[HoleSelectionTaskRunner] Got trigger: {cmd}");
            var result = new System.Text.StringBuilder();
            result.AppendLine($"COMMAND: {cmd}");
            result.AppendLine($"TIME: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            try
            {
                switch (cmd)
                {
                    case "RUN_AUTOWIRE":
                        bool wireOk = EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
                        result.AppendLine($"GOLFIN/Wire/Hole Selection: {(wireOk ? "executed" : "not found")}");
                        break;

                    case "RUN_PREREQUISITES":
                        bool locOk = EditorApplication.ExecuteMenuItem("Tools/Localization/Import Text CSV");
                        result.AppendLine($"Tools/Localization/Import Text CSV: {(locOk ? "executed" : "not found")}");
                        bool spriteOk = EditorApplication.ExecuteMenuItem("GOLFIN/Setup/Configure Hole Images as Sprites");
                        result.AppendLine($"GOLFIN/Setup/Configure Hole Images as Sprites: {(spriteOk ? "executed" : "not found")}");
                        break;

                    case "SNAP_EDITMODE":
                        string snap = CaptureHelper.SnapGameViewWithLabel("hole-sel-verification");
                        result.AppendLine($"SNAP: {snap}");
                        string dest = "Docs/Specs/Active/hole_selection_screen/screenshots";
                        Directory.CreateDirectory(dest);
                        string destFile = $"{dest}/editmode_verification.png";
                        File.Copy(snap, destFile, overwrite: true);
                        result.AppendLine($"COPIED_TO: {destFile}");
                        break;

                    case "RUN_ITERATION3":
                        RunIteration3(result);
                        break;

                    case "RUN_SMOKE":
                        bool smokeOk = EditorApplication.ExecuteMenuItem("GOLFIN/Smoke Test/Run Hole Selection Smoke Test");
                        result.AppendLine($"GOLFIN/Smoke Test/Run Hole Selection Smoke Test: {(smokeOk ? "executed" : "not found")}");
                        break;

                    case "ENTER_PLAY":
                        // Enter play mode — screenshot must be taken in a subsequent SNAP_EDITMODE call after settling
                        if (!EditorApplication.isPlaying && !EditorApplication.isCompiling)
                        {
                            EditorApplication.isPlaying = true;
                            result.AppendLine("EditorApplication.isPlaying = true");
                        }
                        else if (EditorApplication.isPlaying)
                        {
                            result.AppendLine("Already in play mode.");
                        }
                        else
                        {
                            result.AppendLine("WARN: Cannot enter play mode while compiling.");
                        }
                        break;

                    case "EXIT_PLAY":
                        EditorApplication.isPlaying = false;
                        result.AppendLine("EditorApplication.isPlaying = false");
                        break;

                    case "SHOW_HOLE_SEL":
                        // Navigate to HoleSelection screen in play mode
                        if (!EditorApplication.isPlaying)
                        {
                            result.AppendLine("WARN: Not in play mode. Enter play first.");
                        }
                        else
                        {
                            ScreenManager sm = Object.FindFirstObjectByType<ScreenManager>();
                            if (sm != null)
                            {
                                sm.ShowScreen(ScreenId.HoleSelection);
                                result.AppendLine("ScreenManager.ShowScreen(HoleSelection) called.");
                            }
                            else
                            {
                                result.AppendLine("ERROR: ScreenManager not found in scene.");
                            }
                        }
                        break;

                    case "TAP_PLAY_BTN":
                        // Simulate tapping the PLAY button on the expanded card
                        if (!EditorApplication.isPlaying)
                        {
                            result.AppendLine("WARN: Not in play mode.");
                        }
                        else
                        {
                            var hsc2 = Object.FindFirstObjectByType<GolfinRedux.UI.HoleSelection.HoleSelectionScreenController>();
                            if (hsc2 != null)
                            {
                                var cardsF2 = typeof(GolfinRedux.UI.HoleSelection.HoleSelectionScreenController)
                                    .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var cards2 = cardsF2?.GetValue(hsc2) as System.Collections.Generic.List<GolfinRedux.UI.HoleSelection.HoleCardController>;
                                // Find expanded card
                                GolfinRedux.UI.HoleSelection.HoleCardController expandedCard = null;
                                if (cards2 != null)
                                {
                                    foreach (var c in cards2)
                                        if (c.State == GolfinRedux.UI.HoleSelection.HoleCardState.Expanded) { expandedCard = c; break; }
                                }
                                if (expandedCard != null)
                                {
                                    var abF = typeof(GolfinRedux.UI.HoleSelection.HoleCardController)
                                        .GetField("actionButton", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                    var btn = abF?.GetValue(expandedCard) as UnityEngine.UI.Button;
                                    if (btn != null) { btn.onClick.Invoke(); result.AppendLine($"actionButton.onClick.Invoke() on Hole {expandedCard.HoleNumber}."); }
                                    else result.AppendLine("ERROR: actionButton is null.");
                                }
                                else result.AppendLine("ERROR: No expanded card found.");
                            }
                            else result.AppendLine("ERROR: HoleSelectionScreenController not found.");
                        }
                        break;

                    case "EXPAND_HOLE1":
                        // Expand Hole 1 card in play mode
                        if (!EditorApplication.isPlaying)
                        {
                            result.AppendLine("WARN: Not in play mode.");
                        }
                        else
                        {
                            var hsc = Object.FindFirstObjectByType<GolfinRedux.UI.HoleSelection.HoleSelectionScreenController>();
                            if (hsc != null)
                            {
                                var cardsF = typeof(GolfinRedux.UI.HoleSelection.HoleSelectionScreenController)
                                    .GetField("_cards", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                var cards = cardsF?.GetValue(hsc) as System.Collections.Generic.List<GolfinRedux.UI.HoleSelection.HoleCardController>;
                                if (cards != null && cards.Count >= 1)
                                {
                                    cards[0].SetState(GolfinRedux.UI.HoleSelection.HoleCardState.Expanded);
                                    result.AppendLine($"Card[0] (Hole {cards[0].HoleNumber}) set to Expanded.");
                                }
                                else
                                {
                                    result.AppendLine("ERROR: cards list empty or null.");
                                }
                            }
                            else
                            {
                                result.AppendLine("ERROR: HoleSelectionScreenController not found.");
                            }
                        }
                        break;

                    case "ALL":
                        bool w = EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
                        result.AppendLine($"GOLFIN/Wire/Hole Selection: {(w ? "executed" : "not found")}");
                        bool l = EditorApplication.ExecuteMenuItem("Tools/Localization/Import Text CSV");
                        result.AppendLine($"Tools/Localization/Import Text CSV: {(l ? "executed" : "not found")}");
                        bool s = EditorApplication.ExecuteMenuItem("GOLFIN/Setup/Configure Hole Images as Sprites");
                        result.AppendLine($"GOLFIN/Setup/Configure Hole Images as Sprites: {(s ? "executed" : "not found")}");
                        string snap2 = CaptureHelper.SnapGameViewWithLabel("hole-sel-after-setup");
                        result.AppendLine($"SNAP: {snap2}");
                        string dest2 = "Docs/Specs/Active/hole_selection_screen/screenshots";
                        Directory.CreateDirectory(dest2);
                        File.Copy(snap2, $"{dest2}/editmode_after_setup.png", overwrite: true);
                        result.AppendLine($"COPIED_TO: {dest2}/editmode_after_setup.png");
                        break;

                    default:
                        result.AppendLine($"UNKNOWN COMMAND: {cmd}");
                        break;
                }
                result.AppendLine("STATUS: OK");
            }
            catch (System.Exception ex)
            {
                result.AppendLine($"ERROR: {ex.Message}");
                result.AppendLine($"STACK: {ex.StackTrace}");
                result.AppendLine("STATUS: FAIL");
            }

            File.WriteAllText(RESULT_FILE, result.ToString());
            Debug.Log($"[HoleSelectionTaskRunner] Result written to {RESULT_FILE}");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Iteration 3 setup — all tasks in one method
        // ─────────────────────────────────────────────────────────────────────

        private static void RunIteration3(System.Text.StringBuilder result)
        {
            // 1. Set TextureType=Sprite on the 3 new PNGs
            int imported = ImportSprites(result);
            result.AppendLine($"Sprites imported/updated: {imported}");

            // 2. Upgrade HoleCard prefab
            UpgradeHoleCardPrefab(result);

            // 3+4. Add lock icons + wire filter pills in scene
            WireSceneFilterPills(result);

            // 5. Re-run auto-wire
            bool wireOk = EditorApplication.ExecuteMenuItem("GOLFIN/Wire/Hole Selection");
            result.AppendLine($"GOLFIN/Wire/Hole Selection after iteration3: {(wireOk ? "executed" : "not found")}");

            // Save scene
            EditorSceneManager.SaveOpenScenes();
            result.AppendLine("Scene saved.");
        }

        private static int ImportSprites(System.Text.StringBuilder result)
        {
            int imported = 0;
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            foreach (var path in new[] { LOCK_SPRITE_PATH, CHEVRON_RIGHT_PATH, CHEVRON_DOWN_PATH })
            {
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) { result.AppendLine($"WARN: Cannot get TextureImporter for {path}"); continue; }

                if (imp.textureType != TextureImporterType.Sprite ||
                    imp.spriteImportMode != SpriteImportMode.Single)
                {
                    imp.textureType         = TextureImporterType.Sprite;
                    imp.spriteImportMode    = SpriteImportMode.Single;
                    imp.alphaIsTransparency = true;
                    imp.mipmapEnabled       = false;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    result.AppendLine($"Imported as Sprite: {path}");
                    imported++;
                }
                else
                {
                    result.AppendLine($"Already Sprite: {path}");
                }
            }
            return imported;
        }

        private static void UpgradeHoleCardPrefab(System.Text.StringBuilder result)
        {
            var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(HOLE_CARD_PREFAB);
            if (prefabAsset == null) { result.AppendLine($"ERROR: HoleCard prefab not found at {HOLE_CARD_PREFAB}"); return; }

            var stage = PrefabStageUtility.OpenPrefab(HOLE_CARD_PREFAB);
            if (stage == null) { result.AppendLine("ERROR: Could not open HoleCard prefab in isolation."); return; }

            var root = stage.prefabContentsRoot;

            // 2a. Add ChevronIcon to CollapsedContainer/TitleArea
            AddChevronToTitleArea(root, result);

            // 2b-e. Upgrade ActionButton
            UpgradeActionButton(root, result);

            PrefabUtility.SaveAsPrefabAsset(root, HOLE_CARD_PREFAB);
            StageUtility.GoToMainStage();
            result.AppendLine("HoleCard prefab upgraded.");
        }

        private static void AddChevronToTitleArea(GameObject root, System.Text.StringBuilder result)
        {
            var titleAreaT = root.transform.Find("CollapsedContainer/TitleArea");
            if (titleAreaT == null) { result.AppendLine("WARN: CollapsedContainer/TitleArea not found — skip chevron."); return; }

            // Remove old chevron if exists
            var existing = titleAreaT.Find("ChevronIcon");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var chevRight = AssetDatabase.LoadAssetAtPath<Sprite>(CHEVRON_RIGHT_PATH);

            var chevGO = new GameObject("ChevronIcon");
            chevGO.transform.SetParent(titleAreaT, false);

            var rt = chevGO.AddComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(60f, 60f);
            rt.anchorMin        = new Vector2(1f, 0.5f);
            rt.anchorMax        = new Vector2(1f, 0.5f);
            rt.pivot            = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-24f, 0f);

            var img = chevGO.AddComponent<Image>();
            img.sprite         = chevRight;
            img.preserveAspect = true;
            img.color          = Color.white;
            img.raycastTarget  = false;

            chevGO.AddComponent<CanvasRenderer>();
            result.AppendLine("ChevronIcon added to CollapsedContainer/TitleArea.");
        }

        private static void UpgradeActionButton(GameObject root, System.Text.StringBuilder result)
        {
            var actionBtnT = root.transform.Find("ExpandedContainer/ActionButton");
            if (actionBtnT == null) { result.AppendLine("WARN: ExpandedContainer/ActionButton not found — skip button upgrade."); return; }

            // Update main Image background color: top-gold #FCF195
            var mainImg = actionBtnT.GetComponent<Image>();
            if (mainImg != null)
                mainImg.color = new Color(0.988f, 0.949f, 0.584f, 1f);

            // Remove old shadow/sheen/gradient children if they exist
            foreach (string childName in new[] { "ButtonShadow", "ButtonGradientBottom", "ButtonSheen" })
            {
                var old = actionBtnT.Find(childName);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }

            // Shadow behind button (first child — renders under everything else)
            var shadowGO = new GameObject("ButtonShadow");
            shadowGO.transform.SetParent(actionBtnT, false);
            shadowGO.transform.SetAsFirstSibling();
            var shadowRT = shadowGO.AddComponent<RectTransform>();
            shadowRT.anchorMin = new Vector2(0f, 0f);
            shadowRT.anchorMax = new Vector2(1f, 1f);
            shadowRT.offsetMin = new Vector2(-2f, -6f);
            shadowRT.offsetMax = new Vector2(2f, -2f);
            var shadowImg = shadowGO.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.25f);
            shadowImg.raycastTarget = false;
            shadowGO.AddComponent<CanvasRenderer>();

            // Gradient-bottom: bottom 60% with #BB7F1D
            var gradBotGO = new GameObject("ButtonGradientBottom");
            gradBotGO.transform.SetParent(actionBtnT, false);
            gradBotGO.transform.SetSiblingIndex(1);
            var gradBotRT = gradBotGO.AddComponent<RectTransform>();
            gradBotRT.anchorMin = new Vector2(0f, 0f);
            gradBotRT.anchorMax = new Vector2(1f, 0.6f);
            gradBotRT.offsetMin = Vector2.zero;
            gradBotRT.offsetMax = Vector2.zero;
            var gradBotImg = gradBotGO.AddComponent<Image>();
            gradBotImg.color = new Color(0.733f, 0.498f, 0.114f, 1f);
            gradBotImg.raycastTarget = false;
            gradBotGO.AddComponent<CanvasRenderer>();

            // Sheen: top 50% at 25% white
            var sheenGO = new GameObject("ButtonSheen");
            sheenGO.transform.SetParent(actionBtnT, false);
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

            // Update Label TMP: fontSize=66, color #321506, Bold
            var labelT = actionBtnT.Find("Label");
            if (labelT != null)
            {
                var tmp = labelT.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize  = 66f;
                    tmp.color     = new Color(0.196f, 0.082f, 0.024f, 1f);
                    tmp.fontStyle = FontStyles.Bold;
                }
            }

            result.AppendLine("ActionButton visuals upgraded (gradient + sheen + shadow + font 66 + #321506).");
        }

        private static void WireSceneFilterPills(System.Text.StringBuilder result)
        {
            HoleSelectionScreenController ctrl = null;
            foreach (var c in Resources.FindObjectsOfTypeAll<HoleSelectionScreenController>())
            {
                if (c.gameObject.scene.isLoaded) { ctrl = c; break; }
            }
            if (ctrl == null) { result.AppendLine("ERROR: HoleSelectionScreenController not in active scene."); return; }

            var lockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LOCK_SPRITE_PATH);
            if (lockSprite == null)
                result.AppendLine("WARN: Lock sprite not loaded — lock icons will have null sprite.");

            var so          = new SerializedObject(ctrl);
            var coursePillsProp = so.FindProperty("coursePills");
            var teePillsProp    = so.FindProperty("teePills");

            // Find pills by name in loaded objects
            var allRTs = Resources.FindObjectsOfTypeAll<RectTransform>();

            System.Func<string, GameObject> FindPillGO = nameContains =>
            {
                foreach (var rt in allRTs)
                {
                    if (!rt.gameObject.scene.isLoaded) continue;
                    if (rt.name.Contains(nameContains)) return rt.gameObject;
                }
                return null;
            };

            System.Func<GameObject, bool, GameObject> GetOrCreateLockIcon = (pillGO, addIcon) =>
            {
                if (!addIcon || pillGO == null) return null;
                var existing = pillGO.transform.Find("LockIcon");
                if (existing != null) return existing.gameObject;

                var go = new GameObject("LockIcon");
                go.transform.SetParent(pillGO.transform, false);
                go.transform.SetAsFirstSibling();

                var rt = go.AddComponent<RectTransform>();
                rt.sizeDelta        = new Vector2(21f, 26f);
                rt.anchorMin        = new Vector2(0f, 0.5f);
                rt.anchorMax        = new Vector2(0f, 0.5f);
                rt.pivot            = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(10f, 0f);

                var img = go.AddComponent<Image>();
                img.sprite         = lockSprite;
                img.preserveAspect = true;
                img.color          = Color.white;
                img.raycastTarget  = false;

                go.AddComponent<CanvasRenderer>();
                EditorUtility.SetDirty(go);
                return go;
            };

            System.Action<SerializedProperty, GameObject, string, bool> AddPill =
                (arr, pillGO, filterId, locked) =>
            {
                if (pillGO == null) { result.AppendLine($"WARN: Pill not found for filterId={filterId}"); return; }

                arr.arraySize++;
                var elem = arr.GetArrayElementAtIndex(arr.arraySize - 1);

                elem.FindPropertyRelative("button").objectReferenceValue =
                    pillGO.GetComponent<Button>();

                // Pill label may be at "Label" or "Inner/Label" depending on pill hierarchy
                var labelT = pillGO.transform.Find("Label")
                          ?? pillGO.transform.Find("Inner/Label");
                elem.FindPropertyRelative("label").objectReferenceValue =
                    labelT != null ? labelT.GetComponent<TextMeshProUGUI>() : null;

                elem.FindPropertyRelative("background").objectReferenceValue =
                    pillGO.GetComponent<Image>();

                var lockIconGO = GetOrCreateLockIcon(pillGO, locked);
                elem.FindPropertyRelative("lockIcon").objectReferenceValue = lockIconGO;

                elem.FindPropertyRelative("locked").boolValue     = locked;
                elem.FindPropertyRelative("filterId").stringValue = filterId;

                result.AppendLine($"FilterPill wired: filterId={filterId}, locked={locked}, pillGO={pillGO.name}");
            };

            // Clear existing lists
            coursePillsProp.arraySize = 0;
            teePillsProp.arraySize    = 0;

            // Course row pills
            AddPill(coursePillsProp, FindPillGO("Pill_LOMOND"), "Lomond", false);
            AddPill(coursePillsProp, FindPillGO("Pill_YAITA"),  "Yaita",  true);

            // Tee row pills
            AddPill(teePillsProp, FindPillGO("Pill_LADIES"),  "Ladies",  false);
            AddPill(teePillsProp, FindPillGO("Pill_FRONT"),   "Front",   false);
            AddPill(teePillsProp, FindPillGO("Pill_REGULAR"), "Regular", true);
            AddPill(teePillsProp, FindPillGO("Pill_BACK"),    "Back",    true);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(ctrl);

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            result.AppendLine("Filter pills wired: 2 course + 4 tee.");
        }
    }
}
