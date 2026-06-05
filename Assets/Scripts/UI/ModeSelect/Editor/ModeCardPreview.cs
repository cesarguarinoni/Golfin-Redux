#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GolfinRedux.UI.ModeSelect.EditorTools
{
    /// <summary>
    /// EDITOR-ONLY design helper. Renders ModeCard / ModeHomeCard in a representative state
    /// (sample data + a chosen Collapsed / Expanded / Locked / center / side preset) so the cards
    /// can be evaluated visually in Unity instead of looking blank/ambiguous in the Prefab stage.
    ///
    /// NON-PERSISTENCE BY CONSTRUCTION (the hard requirement):
    /// The prefab ASSET is only ever READ — we instantiate a linked prefab instance into a
    /// throwaway additive scene ("__ModeCardPreview") and apply the sample data + state to THAT
    /// instance. The prefab file is never written, so the preview can never bake into it. Edit the
    /// real prefab in the Prefab stage as usual; the live instance updates to match (it's a linked
    /// instance), giving a representative reference while you edit. "Clear Preview" closes the scene.
    ///
    /// (An earlier in-Prefab-stage snapshot/restore approach was abandoned: laying the card out in
    /// the stage bakes driven RectTransform sizes into the asset, which a property snapshot can't
    /// reliably revert — so a save could leak. Instancing sidesteps that entirely.)
    /// </summary>
    public static class ModeCardPreview
    {
        const string MenuRoot   = "GOLFIN/Mode Cards/";
        const string SceneName  = "__ModeCardPreview";
        const string HomePrefab = "Assets/Prefabs/UI/ModeSelect/ModeHomeCard.prefab";
        const string FullPrefab = "Assets/Prefabs/UI/ModeSelect/ModeCard.prefab";

        // ── Menu presets ──────────────────────────────────────────────────────
        [MenuItem(MenuRoot + "Preview/Home — Collapsed + PLAY (center)", priority = 10)]
        static void HomeCollapsed() => Show(HomePrefab, home: true, ModeCardState.Collapsed, center: true);

        [MenuItem(MenuRoot + "Preview/Home — Expanded (center)", priority = 11)]
        static void HomeExpanded() => Show(HomePrefab, home: true, ModeCardState.Expanded, center: true);

        [MenuItem(MenuRoot + "Preview/Home — Side (no PLAY)", priority = 12)]
        static void HomeSide() => Show(HomePrefab, home: true, ModeCardState.CollapsedNoPlay, center: false);

        [MenuItem(MenuRoot + "Preview/Home — Locked", priority = 13)]
        static void HomeLocked() => Show(HomePrefab, home: true, ModeCardState.Locked, center: false);

        [MenuItem(MenuRoot + "Preview/Full-screen — Collapsed", priority = 30)]
        static void FsCollapsed() => Show(FullPrefab, home: false, ModeCardState.Collapsed, center: false);

        [MenuItem(MenuRoot + "Preview/Full-screen — Expanded", priority = 31)]
        static void FsExpanded() => Show(FullPrefab, home: false, ModeCardState.Expanded, center: false);

        [MenuItem(MenuRoot + "Preview/Full-screen — Locked", priority = 32)]
        static void FsLocked() => Show(FullPrefab, home: false, ModeCardState.Locked, center: false);

        [MenuItem(MenuRoot + "Clear Preview", priority = 60)]
        static void ClearPreview()
        {
            var sc = FindPreviewScene();
            if (sc.IsValid() && sc.isLoaded)
            {
                EditorSceneManager.CloseScene(sc, removeScene: true);
                Debug.Log("[ModeCardPreview] Preview scene closed.");
            }
        }

        // ── Core ──────────────────────────────────────────────────────────────
        static void Show(string prefabPath, bool home, ModeCardState state, bool center)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError($"[ModeCardPreview] Prefab not found: {prefabPath}"); return; }

            var scene  = GetOrCreatePreviewScene();
            var canvas = GetOrCreateCanvas(scene);

            // Remove any prior preview card so presets replace, not stack.
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(canvas.transform.GetChild(i).gameObject);

            // Linked prefab INSTANCE — applying sample data here only overrides the instance in this
            // throwaway scene; the prefab asset is never modified.
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(canvas.transform, false);
            var rt = go.transform as RectTransform;
            if (rt != null) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = Vector2.zero; }

            var ctrl = go.GetComponent<ModeCardController>();
            if (ctrl != null)
            {
                // Force INSTANT state (coroutines don't tick in edit mode, so an animated SetState
                // would leave a full-screen card stuck at its start height).
                typeof(ModeCardController)
                    .GetField("_stateInitialized", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.SetValue(ctrl, false);

                ctrl.SetShowChevron(home);
                ctrl.SetHeights(484f, 822f);
                ctrl.Bind(SampleData(state), state);
                ctrl.SetCenter(center);

                if (ctrl.rootRect != null)
                {
                    if (home)
                    {
                        float w = state == ModeCardState.Expanded ? 764f : 556f;
                        ctrl.rootRect.sizeDelta = new Vector2(w, ctrl.rootRect.sizeDelta.y);
                    }
                    Canvas.ForceUpdateCanvases();
                    LayoutRebuilder.ForceRebuildLayoutImmediate(ctrl.rootRect);
                }
            }

            go.name = $"PREVIEW [{(home ? "Home" : "Full-screen")} / {state}]";
            Selection.activeGameObject = go;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) { sv.in2DMode = true; sv.FrameSelected(); }
            Debug.Log($"[ModeCardPreview] {go.name} — preview instance only; the prefab asset is untouched. " +
                      "Edit the prefab in the Prefab stage and this updates live. 'Clear Preview' to close.");
        }

        static ModeData SampleData(ModeCardState state) => new ModeData
        {
            id          = "preview",
            title       = "PREVIEW MODE",
            tagline     = "Short tagline line",
            description = "A two-to-three line description shown in the expanded card so the layout, "
                        + "spacing and wrapping can be eyeballed while you edit the prefab.",
            entryFee    = 100,
            rewards     = 200,
            locked      = state == ModeCardState.Locked,
            target      = "none",
            order       = 0,
        };

        // ── Preview scene + canvas ────────────────────────────────────────────
        static Scene FindPreviewScene()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (s.name == SceneName) return s;
            }
            return default;
        }

        static Scene GetOrCreatePreviewScene()
        {
            var s = FindPreviewScene();
            if (s.IsValid() && s.isLoaded) return s;
            s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            s.name = SceneName;   // stays untitled (no asset path) so it can't overwrite anything
            return s;
        }

        static Canvas GetOrCreateCanvas(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponent<Canvas>();
                if (c != null) return c;
            }
            var go = new GameObject("PreviewCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(go, scene);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var crt = canvas.transform as RectTransform;
            crt.sizeDelta = new Vector2(1200f, 2700f);
            crt.position  = Vector3.zero;
            crt.localScale = Vector3.one * 0.01f;   // 100px == 1 world unit, comfortable to frame
            return canvas;
        }
    }
}
#endif
