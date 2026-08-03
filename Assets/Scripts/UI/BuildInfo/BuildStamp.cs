#if GOLFIN_TESTBUILD
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI.Core;

namespace GolfinRedux.UI.BuildInfo
{
    /// <summary>
    /// Anti-staleness build stamp (build_version_stamp task). Renders a small,
    /// always-on-top label — e.g. "v0.1.0 (2014) bb59d32+4f9e · 08-03 14:32" —
    /// so that after a Build &amp; Run Cesar can confirm AT A GLANCE that the binary
    /// on the device contains the code that was just written.
    ///
    /// The label string is captured entirely at BUILD time by
    /// BuildStampGenerator (editor) and baked into
    /// Resources/Data/build_stamp.txt. Nothing here is computed at runtime — the
    /// device has no git access, so this component only reads the baked text and
    /// draws it. In the editor the same generator regenerates the text on every
    /// play-mode enter, so play-mode verification shows a live stamp too.
    ///
    /// This whole file compiles out unless GOLFIN_TESTBUILD is defined (added
    /// additively to the Dev-iOS / Dev-Android / iOS-Demo build profiles, mirroring
    /// the GOLFIN_DEMO pattern). We deliberately do NOT gate on Debug.isDebugBuild:
    /// demo builds handed to stakeholders are release builds but still need the stamp.
    ///
    /// PLACEMENT: the shot UI lives on a SEPARATE canvas inside the additively
    /// loaded hole scene, so a ShellScene-parented label would not cover gameplay.
    /// This is a self-bootstrapping DontDestroyOnLoad canvas at max sortingOrder,
    /// so it survives every scene load — menu screens AND holes. Anchored
    /// bottom-right INSIDE the safe area (SafeAreaFitter) so it clears the home
    /// indicator on notch/Dynamic-Island iPhones. No prefab or ShellScene edits.
    /// </summary>
    [DefaultExecutionOrder(short.MaxValue)]
    public sealed class BuildStamp : MonoBehaviour
    {
        const string ResourcePath = "Data/build_stamp";      // Resources.Load key (no extension)
        const string Fallback     = "build stamp unavailable"; // txt missing = surface it, don't hide

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("BuildStamp");
            DontDestroyOnLoad(go);
            go.AddComponent<BuildStamp>();
        }

        void Awake()
        {
            string stamp = LoadStampText();
            Build(stamp);
        }

        static string LoadStampText()
        {
            var ta = Resources.Load<TextAsset>(ResourcePath);
            if (ta == null || string.IsNullOrWhiteSpace(ta.text))
                return Fallback;
            // The baked asset is a single line; trim trailing newline defensively.
            return ta.text.Trim();
        }

        void Build(string stamp)
        {
            // ── Overlay canvas at the very top of the draw order ──────────────
            var canvasGO = new GameObject("BuildStampCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);

            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue; // 32767 — above every gameplay/menu canvas

            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1170f, 2532f); // iPhone 14 portrait, project standard
            scaler.matchWidthOrHeight  = 0.5f;

            // The raycaster is required by Canvas plumbing but the stamp must never
            // intercept input — every Graphic below sets raycastTarget = false.

            // ── Safe-area inset panel (reuse the production SafeAreaFitter) ────
            var safeGO = new GameObject("SafeArea", typeof(RectTransform));
            safeGO.transform.SetParent(canvasGO.transform, false);
            var safeRT = (RectTransform)safeGO.transform;
            safeRT.anchorMin = Vector2.zero;
            safeRT.anchorMax = Vector2.one;
            safeRT.offsetMin = Vector2.zero;
            safeRT.offsetMax = Vector2.zero;
            safeGO.AddComponent<SafeAreaFitter>();

            // ── Auto-sized pill anchored to the bottom-right of the safe area ──
            var pillGO = new GameObject("StampPill",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            pillGO.transform.SetParent(safeRT, false);

            var pillRT = (RectTransform)pillGO.transform;
            pillRT.anchorMin        = new Vector2(1f, 0f); // bottom-right corner of the safe area
            pillRT.anchorMax        = new Vector2(1f, 0f);
            pillRT.pivot            = new Vector2(1f, 0f);
            pillRT.anchoredPosition = new Vector2(-16f, 12f); // small inset from the safe-area corner

            var bg = pillGO.GetComponent<Image>();
            bg.color         = new Color(0f, 0f, 0f, 0.55f);
            bg.raycastTarget = false;

            var layout = pillGO.GetComponent<HorizontalLayoutGroup>();
            layout.padding          = new RectOffset(12, 12, 6, 6);
            layout.childAlignment   = TextAnchor.MiddleCenter;
            layout.childControlWidth  = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;

            var fitter = pillGO.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            // ── Label ─────────────────────────────────────────────────────────
            var textGO = new GameObject("StampLabel", typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(pillRT, false);
            var label = textGO.AddComponent<TextMeshProUGUI>();
            label.text          = stamp;
            label.fontSize      = 24f;
            label.color         = new Color(1f, 1f, 1f, 0.95f);
            label.alignment     = TextAlignmentOptions.MidlineRight;
            label.enableWordWrapping = false;
            label.overflowMode  = TextOverflowModes.Overflow;
            label.raycastTarget = false;
        }
    }
}
#endif

