#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Gameplay.Loop;

namespace Golfin.Physics.Viewer
{
    [DefaultExecutionOrder(1000)]
    public sealed class CameraModeDebugHUD : MonoBehaviour
    {
        IModeSetter _modeSetter;
        PhysicsLabController _lab;
        TextMeshProUGUI _label;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<ChaseCamera>() == null) return;
            var go = new GameObject("CameraModeDebugHUD");
            DontDestroyOnLoad(go);
            go.AddComponent<CameraModeDebugHUD>();
        }

        void Awake()
        {
            BuildCanvas();
            _modeSetter = FindFirstObjectByType<ChaseCamera>();
            _lab        = FindFirstObjectByType<PhysicsLabController>();
        }

        void LateUpdate()
        {
            if (_modeSetter == null) _modeSetter = FindFirstObjectByType<ChaseCamera>();
            if (_lab == null)        _lab        = FindFirstObjectByType<PhysicsLabController>();
            if (_label == null) return;

            string mode = _modeSetter != null ? _modeSetter.CurrentMode.ToString() : "—";
            string sm   = _lab != null && _lab.BallSM != null ? _lab.BallSM.State.ToString() : "—";
            _label.text = $"CAM: <b>{mode}</b>   BALL: <b>{sm}</b>";
        }

        void BuildCanvas()
        {
            var canvasGO = new GameObject("CameraModeDebugCanvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            var bgGO = new GameObject("BG",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            var bgRT = (RectTransform)bgGO.transform;
            bgRT.anchorMin = new Vector2(0.5f, 1f);
            bgRT.anchorMax = new Vector2(0.5f, 1f);
            bgRT.pivot     = new Vector2(0.5f, 1f);
            bgRT.anchoredPosition = new Vector2(0f, -12f);
            bgRT.sizeDelta = new Vector2(560f, 44f);
            bgGO.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var textGO = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer));
            textGO.transform.SetParent(bgGO.transform, false);
            var textRT = (RectTransform)textGO.transform;
            textRT.anchorMin        = Vector2.zero;
            textRT.anchorMax        = Vector2.one;
            textRT.offsetMin        = new Vector2(8f, 4f);
            textRT.offsetMax        = new Vector2(-8f, -4f);
            _label = textGO.AddComponent<TextMeshProUGUI>();
            _label.alignment    = TextAlignmentOptions.Center;
            _label.fontSize     = 24f;
            _label.color        = new Color(1f, 0.92f, 0.4f, 1f);
            _label.richText     = true;
            _label.raycastTarget = false;
            _label.text         = "CAM: —   BALL: —";
        }
    }
}
#endif
